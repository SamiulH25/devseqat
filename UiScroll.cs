using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// The parchment scroll: a sheet of painted paper (mottled, fibred, foxed, with a stain or two) hung from a roller, unrolled
/// from the top by a second roller that travels down as it opens, with burnt, ragged edges.
static partial class Kit
{
    const int PaperSize = 400;
    static Texture2D? paper;

    /// The paper, painted once: blotchy tone in three sizes of noise, fibres, foxing spots and a faint ring where a cup stood.
    static Texture2D Paper(GraphicsDevice gd)
    {
        if (paper != null) return paper;
        const int S = PaperSize;
        var tone = new float[S * S];
        float Noise(float x, float y, int salt)
        {
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float H(int i, int j) => Ease.Hash(i * 7919 + j * 104729 + salt * 15485863);
            return MathHelper.Lerp(MathHelper.Lerp(H(x0, y0), H(x0 + 1, y0), fx), MathHelper.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                tone[y * S + x] = 0.5f * Noise(x / 48f, y / 40f, 1) + 0.32f * Noise(x / 14f, y / 11f, 2) + 0.18f * Noise(x / 3.5f, y / 3f, 3);
        var rnd = new Random(7);
        // Fibres: short streaks, mostly along the sheet, a shade lighter or darker.
        for (int i = 0; i < 1400; i++)
        {
            float x = rnd.Next(S), y = rnd.Next(S), len = 3 + rnd.Next(12), slope = (float)(rnd.NextDouble() - 0.5) * 0.4f, d = rnd.Next(2) == 0 ? -0.09f : 0.07f;
            for (int t = 0; t < len; t++) { int fx = ((int)(x + t) % S + S) % S, fy = ((int)(y + t * slope) % S + S) % S; tone[fy * S + fx] += d; }
        }
        // A ring where a cup once stood, and foxing.
        var stain = new float[S * S];
        for (int r = 0; r < 2; r++)
        {
            float cx = 60 + rnd.Next(S - 120), cy = 60 + rnd.Next(S - 120), rad = 22 + rnd.Next(18);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dist = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (MathF.Abs(dist - rad) < 1.6f && Noise(x / 5f, y / 5f, 9 + r) > 0.35f) stain[y * S + x] += 0.35f;
                else if (dist < rad) stain[y * S + x] += 0.05f;
            }
        }
        for (int i = 0; i < 40; i++)
        {
            float cx = rnd.Next(S), cy = rnd.Next(S), rad = 0.8f + (float)rnd.NextDouble() * (i % 8 == 0 ? 5 : 2.2f);
            for (int y = (int)(cy - rad - 1); y <= cy + rad + 1; y++) for (int x = (int)(cx - rad - 1); x <= cx + rad + 1; x++)
            {
                float dist = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist <= rad && x >= 0 && y >= 0 && x < S && y < S) stain[y * S + x] += 0.5f * (1 - dist / (rad + 0.5f));
            }
        }
        var px = new Color[S * S];
        Color light = new(242, 230, 196), dark = new(184, 158, 110), brown = new(140, 92, 46);
        for (int i = 0; i < px.Length; i++)
            px[i] = Color.Lerp(Color.Lerp(dark, light, Math.Clamp(tone[i], 0, 1)), brown, Math.Clamp(stain[i], 0, 0.6f));
        paper = new Texture2D(gd, S, S);
        paper.SetData(px);
        return paper;
    }

    /// A scroll in `r` unrolled `open` (0 shut, 1 fully open) from the top. Returns how far down the paper shows so far:
    /// draw writing only above it, so the words come into view as the roller passes.
    public static int Scroll(this Ui ui, Rectangle r, float open)
    {
        int roll = ui.U(r.Height > ui.U(200) ? 20 : 15), k = Math.Max(1, ui.U(2));
        int top = r.Y + roll / 2, h = (int)MathF.Round((r.Height - roll) * Math.Clamp(open, 0, 1)), bottom = top + h;
        if (h > 0)
        {
            var tex = Paper(ui.Sb.GraphicsDevice);
            var sheet = new Rectangle(r.X, top, r.Width, h);
            ui.Sb.Draw(tex, sheet, new Rectangle(0, 0, Math.Min(PaperSize, (sheet.Width + k - 1) / k), Math.Min(PaperSize, Math.Max(1, (h + k - 1) / k))), Color.White);
            // Age at the edges: darker towards the sides, a shadow under the top roller, the curl into the bottom one.
            int e = ui.U(12);
            ui.HGrad(new Rectangle(sheet.X, top, e, h), Ink * 0.32f, Ink * 0f);
            ui.HGrad(new Rectangle(sheet.Right - e, top, e, h), Ink * 0f, Ink * 0.32f);
            ui.VGrad(new Rectangle(sheet.X, top, sheet.Width, Math.Min(h, ui.U(10))), Ink * 0.35f, Ink * 0f, 1);
            if (h > ui.U(12)) ui.VGrad(new Rectangle(sheet.X, bottom - ui.U(12), sheet.Width, ui.U(12)), Ink * 0f, Ink * 0.4f, 1);
            // Burnt, ragged sides, a notch here and there.
            for (int y = top; y < bottom; y += k)
            {
                int row = (y - r.Y) / k, ln = 1 + (int)(Ease.Hash(row * 31 + 5) * 2.6f), rn = 1 + (int)(Ease.Hash(row * 37 + 11) * 2.6f);
                int hh = Math.Min(k, bottom - y);
                ui.Rect(new Rectangle(sheet.X, y, ln * k, hh), Mul(Parchment, 0.58f));
                ui.Rect(new Rectangle(sheet.X, y, k / 2 + 1, hh), Ink * 0.8f);
                ui.Rect(new Rectangle(sheet.Right - rn * k, y, rn * k, hh), Mul(Parchment, 0.5f));
                ui.Rect(new Rectangle(sheet.Right - k / 2 - 1, y, k / 2 + 1, hh), Ink * 0.8f);
            }
        }
        Roller(ui, r.X, r.Y, r.Width, roll);
        // The bottom roll is fatter while it still holds the paper not yet shown.
        int fat = roll + (int)(roll * 0.6f * (1 - Math.Clamp(open, 0, 1)));
        ui.Rect(new Rectangle(r.X + ui.U(4), bottom + fat / 2, r.Width - ui.U(8), ui.U(4)), Color.Black * 0.3f); // the shadow it casts
        Roller(ui, r.X, bottom - fat / 2, r.Width, fat);
        return bottom - fat / 2;
    }

    /// One roller: the rolled paper as a shaded cylinder, a wooden dowel out of each end with gilt caps.
    static void Roller(Ui ui, int x, int y, int w, int h)
    {
        int over = ui.U(10), dh = Math.Max(2, h * 11 / 20), cap = ui.U(5);
        foreach (int dx in new[] { x - over, x + w })
        {
            ui.VGrad(new Rectangle(dx, y + (h - dh) / 2, over, dh), new Color(130, 86, 48), new Color(56, 34, 20), 1);
            int cx = dx == x - over ? dx - cap : dx + over;
            ui.VGrad(new Rectangle(cx, y + h / 6, cap, h - h / 3), Gilt, GiltDark, 1);
        }
        int s = Math.Max(1, ui.U(1));
        for (int j = 0; j < h; j += s)
        {
            // A cylinder lit from above: a bright band a third of the way down, deep shade underneath.
            float t = (j + 0.5f) / h, lit = MathF.Pow(MathF.Sin(MathF.PI * t), 0.8f) * (1.1f - 0.45f * t) + (MathF.Abs(t - 0.3f) < 0.08f ? 0.15f : 0);
            ui.Rect(new Rectangle(x, y + j, w, Math.Min(s, h - j)), Color.Lerp(Mul(Parchment, 0.4f), Mul(Parchment, 1.06f), Math.Clamp(lit, 0, 1)));
        }
        ui.Rect(new Rectangle(x, y + h * 2 / 3, w, s), Mul(Parchment, 0.68f)); // the edge of the next turn of paper
        ui.Rect(new Rectangle(x, y, w, s), Ink * 0.7f);
        ui.Rect(new Rectangle(x, y + h - s, w, s), Ink * 0.8f);
        ui.Rect(new Rectangle(x, y, s, h), Ink * 0.8f);
        ui.Rect(new Rectangle(x + w - s, y, s, h), Ink * 0.8f);
    }
}
