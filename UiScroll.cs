using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// The parchment scroll, in whole interface pixels: a sheet of paper in four dithered tones (blotched, fibred, foxed, a ring
/// where a cup stood) with torn edges you can see through, a ruled ink border, hung from a turned wooden roller and unrolled
/// from the top by a second one that travels down as it opens.
static partial class Kit
{
    const int PaperSize = 384;
    static Texture2D? paper;
    static readonly Color[] PaperTones = { new(204, 180, 130), new(220, 200, 154), new(232, 216, 176), new(241, 229, 196) };
    static readonly Color Scorch = new(84, 54, 28), Wood = new(124, 80, 44), WoodLight = new(178, 126, 74), WoodDark = new(52, 30, 16);
    static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    /// The paper, painted once and made to tile: blotchy tone in three sizes of noise cut into four tones with an ordered
    /// dither, fibres, foxing spots and a faint ring where a cup stood.
    static Texture2D Paper(GraphicsDevice gd)
    {
        if (paper != null) return paper;
        const int S = PaperSize;
        var tone = new float[S * S];
        float Noise(float x, float y, int cell, int salt)
        {
            int per = S / cell;
            x /= cell; y /= cell;
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float H(int i, int j) => Ease.Hash((i % per + per) % per * 7919 + (j % per + per) % per * 104729 + salt * 15485863);
            return MathHelper.Lerp(MathHelper.Lerp(H(x0, y0), H(x0 + 1, y0), fx), MathHelper.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                tone[y * S + x] = 0.5f * Noise(x, y, 48, 1) + 0.34f * Noise(x, y, 12, 2) + 0.16f * Noise(x, y, 3, 3);
        var rnd = new Random(7);
        // Fibres: short streaks, mostly along the sheet, a shade lighter or darker.
        for (int i = 0; i < 900; i++)
        {
            float x = rnd.Next(S), y = rnd.Next(S), len = 3 + rnd.Next(10), slope = (float)(rnd.NextDouble() - 0.5) * 0.4f, d = rnd.Next(2) == 0 ? -0.12f : 0.10f;
            for (int t = 0; t < len; t++) { int fx = ((int)(x + t) % S + S) % S, fy = ((int)(y + t * slope) % S + S) % S; tone[fy * S + fx] += d; }
        }
        // A ring where a cup once stood, and foxing.
        var stain = new float[S * S];
        {
            float cx = 250, cy = 150, rad = 26;
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dist = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (MathF.Abs(dist - rad) < 1.3f && Noise(x, y, 6, 9) > 0.4f) stain[y * S + x] += 0.3f;
            }
        }
        for (int i = 0; i < 26; i++)
        {
            float cx = 6 + rnd.Next(S - 12), cy = 6 + rnd.Next(S - 12), rad = 0.8f + (float)rnd.NextDouble() * (i % 9 == 0 ? 3.5f : 1.6f);
            for (int y = (int)(cy - rad - 1); y <= cy + rad + 1; y++) for (int x = (int)(cx - rad - 1); x <= cx + rad + 1; x++)
            {
                float dist = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist <= rad && x >= 0 && y >= 0 && x < S && y < S) stain[y * S + x] += 0.45f;
            }
        }
        var px = new Color[S * S];
        Color brown = new(150, 104, 56);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                float v = (tone[i] - 0.30f) / 0.40f * 2.2f + 0.9f + Bayer4[(y & 3) * 4 + (x & 3)] / 16f - 0.5f;
                Color c = PaperTones[Math.Clamp((int)MathF.Floor(v), 0, 3)];
                px[i] = stain[i] > 0.2f ? Color.Lerp(c, brown, stain[i] > 0.4f ? 0.42f : 0.22f) : c;
            }
        paper = new Texture2D(gd, S, S);
        paper.SetData(px);
        return paper;
    }

    /// Paper laid over `dest` at `p` screen pixels a texel, tiled, starting from texel (`tx`, `ty`).
    static void Tile(Ui ui, Texture2D tex, Rectangle dest, int p, int tx, int ty)
    {
        for (int y = 0; y < dest.Height;)
        {
            int sy = (ty + y / p) % PaperSize, hh = Math.Min(dest.Height - y, (PaperSize - sy) * p);
            for (int x = 0; x < dest.Width;)
            {
                int sx = (tx + x / p) % PaperSize, ww = Math.Min(dest.Width - x, (PaperSize - sx) * p);
                ui.Sb.Draw(tex, new Rectangle(dest.X + x, dest.Y + y, ww, hh), new Rectangle(sx, sy, Math.Max(1, ww / p), Math.Max(1, hh / p)), Color.White);
                x += ww;
            }
            y += hh;
        }
    }

    /// How far a row of the sheet is torn in from its side, in interface pixels: runs of two rows, now and then a deeper bite.
    static int Torn(int row, int salt)
    {
        float v = Ease.Hash(row / 2 * 31 + salt);
        return v < 0.46f ? 0 : v < 0.86f ? 1 : v < 0.97f ? 2 : 3;
    }

    /// A scroll in `r` unrolled `open` (0 shut, 1 fully open) from the top. Returns how far down the paper shows so far:
    /// draw writing only above it, so the words come into view as the roller passes. Keep writing `U(12)` in from the
    /// sides and `U(24)` below `r.Y`: the ruled border runs outside that.
    public static int Scroll(this Ui ui, Rectangle r, float open)
    {
        int p = Math.Max(1, ui.U(1)), roll = ui.U(r.Height > ui.U(200) ? 18 : 14);
        open = Math.Clamp(open, 0, 1);
        int top = r.Y + roll / 2, h = (int)MathF.Round((r.Height - roll) * open) / p * p, bottom = top + h;
        if (h > 0) Sheet(ui, r, top, h, p, roll);
        // The bottom roll is fatter while it still holds the paper not yet shown.
        int fat = roll + (int)(roll * 0.5f * (1 - open)) / (2 * p) * 2 * p;
        ui.Rect(new Rectangle(r.X + 2 * p, bottom + fat / 2, r.Width - 4 * p, 2 * p), Color.Black * 0.38f); // the shadow it casts
        ui.Rect(new Rectangle(r.X + 4 * p, bottom + fat / 2 + 2 * p, r.Width - 8 * p, p), Color.Black * 0.18f);
        Roller(ui, r.X, r.Y, r.Width, roll, p);
        Roller(ui, r.X, bottom - fat / 2, r.Width, fat, p);
        return bottom - fat / 2;
    }

    /// The open part of the sheet: paper, torn and scorched sides, the shade of the rollers, the ruled border.
    static void Sheet(Ui ui, Rectangle r, int top, int h, int p, int roll)
    {
        var tex = Paper(ui.Sb.GraphicsDevice);
        int x0 = r.X + 2 * p, w = (r.Width - 4 * p) / p * p, bottom = top + h, deep = 3 * p;
        Tile(ui, tex, new Rectangle(x0 + deep, top, w - 2 * deep, h), p, 3, 0);
        for (int i = 0; i < h / p; i++)
        {
            int y = top + i * p, ln = Torn(i, 5), rn = Torn(i, 11);
            if (ln < 3) Tile(ui, tex, new Rectangle(x0 + ln * p, y, (3 - ln) * p, p), p, ln, i);
            if (rn < 3) Tile(ui, tex, new Rectangle(x0 + w - deep, y, (3 - rn) * p, p), p, 3 + (w - 2 * deep) / p, i);
            int lx = x0 + ln * p, rx = x0 + w - rn * p;
            ui.Rect(new Rectangle(lx, y, p, p), Scorch); ui.Rect(new Rectangle(lx + p, y, p, p), Scorch * 0.45f); ui.Rect(new Rectangle(lx + 2 * p, y, 2 * p, p), Scorch * 0.16f);
            ui.Rect(new Rectangle(rx - p, y, p, p), Scorch); ui.Rect(new Rectangle(rx - 2 * p, y, p, p), Scorch * 0.5f); ui.Rect(new Rectangle(rx - 4 * p, y, 2 * p, p), Scorch * 0.2f);
        }
        // The shade under the top roller and the curl into the bottom one, in steps.
        var inner = new Rectangle(x0 + p, top, w - 2 * p, h);
        int[] band = { 2, 2, 3 }; float[] dark = { 0.42f, 0.24f, 0.10f };
        for (int b = 0, at = 0; b < 3 && at * p < h; at += band[b], b++)
            ui.Rect(new Rectangle(inner.X, top + at * p, inner.Width, Math.Min(band[b] * p, h - at * p)), Scorch * dark[b]);
        if (h > 16 * p)
            for (int b = 0, at = 0; b < 3; at += band[b], b++)
                ui.Rect(new Rectangle(inner.X, bottom - (at + band[b]) * p, inner.Width, band[b] * p), Scorch * (dark[b] * 0.8f));
        // A ruled border in faded ink, fixed to the paper: it comes into view as the scroll opens.
        if (r.Width < ui.U(110)) return;
        int mx = ui.U(7), ry0 = r.Y + roll + ui.U(5), ry1 = r.Bottom - roll - ui.U(5);
        if (ry1 - ry0 < ui.U(20)) return;
        void Rule(int inset, float alpha)
        {
            Color ink = new Color(110, 70, 38) * alpha;
            int a = x0 + mx + inset, b2 = x0 + w - mx - inset, y0 = ry0 + inset, y1 = ry1 - inset;
            void Clip(Rectangle q) { int hh = Math.Min(q.Bottom, bottom) - q.Y; if (hh > 0) ui.Rect(new Rectangle(q.X, q.Y, q.Width, hh), ink); }
            Clip(new Rectangle(a, y0, b2 - a, p)); Clip(new Rectangle(a, y1 - p, b2 - a, p));
            Clip(new Rectangle(a, y0 + p, p, y1 - y0 - 2 * p)); Clip(new Rectangle(b2 - p, y0 + p, p, y1 - y0 - 2 * p));
        }
        Rule(0, 0.6f); Rule(2 * p, 0.3f);
        foreach (int cx in new[] { x0 + mx, x0 + w - mx - p })
            foreach (int cy in new[] { ry0, ry1 - p })
                if (cy + 2 * p <= bottom) ui.Rect(new Rectangle(cx - p, cy - p, 3 * p, 3 * p), new Color(110, 70, 38) * 0.75f); // corner studs
    }

    /// One roller: the rolled paper as a banded cylinder with its turns showing at the ends, on a wooden rod with a gilt
    /// collar and a turned knob at each end.
    static void Roller(Ui ui, int x, int y, int w, int h, int p)
    {
        int over = ui.U(6), dh = Math.Max(2 * p, h * 2 / 5 / p * p), cy = y + h / 2, rk = Math.Max(2 * p, h * 3 / 8);
        foreach (int side in new[] { -1, 1 })
        {
            int rx = side < 0 ? x - over : x + w;
            ui.Rect(new Rectangle(rx, cy - dh / 2 - p, over, dh + 2 * p), WoodDark);
            ui.Rect(new Rectangle(rx, cy - dh / 2, over, dh), Wood);
            ui.Rect(new Rectangle(rx, cy - dh / 2, over, p), WoodLight);
            ui.Rect(new Rectangle(rx, cy + dh / 2 - p, over, p), Mul(Wood, 0.6f));
            var knob = new Vector2(side < 0 ? x - over - rk + p : x + w + over + rk - p, cy);
            ui.Disc(knob, rk + p, WoodDark);
            ui.Disc(knob, rk, Wood);
            ui.Disc(knob + new Vector2(-rk * 0.28f, -rk * 0.3f), Math.Max(p, rk / 2), WoodLight);
            int gx = side < 0 ? x - 2 * p : x + w;
            ui.Rect(new Rectangle(gx, cy - dh / 2 - 2 * p, 2 * p, dh + 4 * p), WoodDark);
            ui.Rect(new Rectangle(gx, cy - dh / 2 - p, 2 * p, dh + 2 * p), GiltDark);
            ui.Rect(new Rectangle(gx, cy - dh / 2 - p, 2 * p, (dh + 2 * p) / 2), Gilt);
        }
        // A cylinder lit from above, in bands: a rim, the light, the paper's own tone, then shade.
        (float To, Color C)[] bands = { (0.14f, PaperTones[1]), (0.34f, PaperTones[3]), (0.56f, PaperTones[2]), (0.76f, PaperTones[0]), (0.9f, Mul(PaperTones[0], 0.78f)), (1.01f, Mul(PaperTones[0], 0.58f)) };
        for (int j = 0; j < h; j += p)
        {
            float t = (j + p * 0.5f) / h;
            ui.Rect(new Rectangle(x, y + j, w, Math.Min(p, h - j)), bands.First(b => t < b.To).C);
        }
        ui.Rect(new Rectangle(x + 3 * p, y + h * 3 / 5 / p * p, w - 6 * p, p), Scorch * 0.4f); // the edge of the next turn of paper
        foreach (int ex in new[] { x + p, x + w - 3 * p }) // the turns of paper seen at each end
        {
            ui.Rect(new Rectangle(ex, y + p, 2 * p, h - 2 * p), Scorch * 0.22f);
            ui.Rect(new Rectangle(ex + (ex == x + p ? p : 0), y + p, p, h - 2 * p), Scorch * 0.3f);
        }
        ui.Rect(new Rectangle(x + p, y, w - 2 * p, p), Scorch);
        ui.Rect(new Rectangle(x + p, y + h - p, w - 2 * p, p), WoodDark);
        ui.Rect(new Rectangle(x, y + p, p, h - 2 * p), Scorch);
        ui.Rect(new Rectangle(x + w - p, y + p, p, h - 2 * p), Scorch);
    }
}
