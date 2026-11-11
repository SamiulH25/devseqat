using System.Globalization;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// The interface kit beyond the basics in `Ui`, as extensions on it (docs/DESIGN.md 4): gradients, the iron plate with rivets
/// and a ribbon title, painted heater shields, 7x7 pixel icons, ring gauges, segmented bars, toggles, sliders, chips, charts
/// and tooltips. Everything is drawn from the 1x1 pixel and the pixel font, so it keeps the hard-edged pixel look.
/// The UI lab (F3 on the title) shows every widget.
static class Kit
{
    // ---------- colours ----------

    public static readonly Color Iron = new(30, 34, 44), IronDark = new(15, 17, 23), Rivet = new(120, 112, 96),
        Gilt = new(214, 172, 70), GiltDark = new(120, 88, 34), Parchment = new(226, 210, 172), Ink = new(58, 44, 30),
        Night = new(12, 16, 32), Dusk = new(64, 44, 70), Ember = new(236, 120, 48);

    public static Color Mul(Color c, float k) => new((int)Math.Min(255, c.R * k), (int)Math.Min(255, c.G * k), (int)Math.Min(255, c.B * k));

    public static string N(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    // ---------- shapes ----------

    /// A vertical gradient in bands of `step` units.
    public static void VGrad(this Ui ui, Rectangle r, Color top, Color bottom, int step = 2)
    {
        int s = Math.Max(1, ui.U(step));
        for (int y = r.Y; y < r.Bottom; y += s)
            ui.Rect(new Rectangle(r.X, y, r.Width, Math.Min(s, r.Bottom - y)), Color.Lerp(top, bottom, (y - r.Y) / (float)Math.Max(1, r.Height - 1)));
    }

    public static void HGrad(this Ui ui, Rectangle r, Color left, Color right, int step = 2)
    {
        int s = Math.Max(1, ui.U(step));
        for (int x = r.X; x < r.Right; x += s)
            ui.Rect(new Rectangle(x, r.Y, Math.Min(s, r.Right - x), r.Height), Color.Lerp(left, right, (x - r.X) / (float)Math.Max(1, r.Width - 1)));
    }

    public static void Line(this Ui ui, Vector2 a, Vector2 b, Color c, float thick = 1)
    {
        var d = b - a;
        ui.Sb.Draw(ui.Pixel, a, null, c, MathF.Atan2(d.Y, d.X), new Vector2(0, 0.5f), new Vector2(d.Length(), ui.U(thick)), default, 0);
    }

    /// A filled disc made of horizontal runs (pixel-stepped edge).
    public static void Disc(this Ui ui, Vector2 c, float r, Color col)
    {
        int s = Math.Max(1, ui.U(1));
        for (float y = -r; y <= r; y += s)
        {
            float w = MathF.Sqrt(MathF.Max(0, r * r - y * y));
            ui.Rect(new Rectangle((int)(c.X - w), (int)(c.Y + y), (int)(2 * w), s), col);
        }
    }

    /// A dark wash that fades in from the screen edges.
    public static void Vignette(this Ui ui, float a = 0.55f)
    {
        int n = 12, bw = ui.W / 2 / n, bh = ui.H / 2 / n;
        for (int i = 0; i < n; i++)
        {
            var c = Color.Black * (a * (1 - i / (float)n) * 0.16f);
            ui.Rect(new Rectangle(0, i * bh, ui.W, bh), c);
            ui.Rect(new Rectangle(0, ui.H - (i + 1) * bh, ui.W, bh), c);
            ui.Rect(new Rectangle(i * bw, 0, bw, ui.H), c);
            ui.Rect(new Rectangle(ui.W - (i + 1) * bw, 0, bw, ui.H), c);
        }
    }

    /// Rolling hills as a silhouette inside `box`: `y` is the mean ridge line, `amp` the swell, `seed` shifts the shape.
    public static void Hills(this Ui ui, Rectangle box, int y, float amp, float seed, Color c, int step = 4)
    {
        int s = ui.U(step);
        for (int x = box.X; x < box.Right; x += s)
        {
            int h = Math.Max(ui.HillY(box, x, y, amp, seed), box.Y);
            if (h < box.Bottom) ui.Rect(new Rectangle(x, h, Math.Min(s, box.Right - x), box.Bottom - h), c);
        }
    }

    /// The ridge height of `Hills` at screen x.
    public static int HillY(this Ui ui, Rectangle box, int x, int y, float amp, float seed)
    {
        float t = (x - box.X) / (float)ui.U(100) + seed;
        return (int)(y - amp * (0.55f * MathF.Sin(t * 1.3f) + 0.3f * MathF.Sin(t * 2.9f + 1.7f) + 0.15f * MathF.Sin(t * 6.1f)));
    }

    /// Stars scattered over the top of `box`, twinkling with `time`.
    public static void Stars(this Ui ui, Rectangle box, float time, int count = 90)
    {
        var rng = new Random(7);
        for (int i = 0; i < count; i++)
        {
            int x = box.X + rng.Next(box.Width), y = box.Y + (int)(box.Height * MathF.Pow((float)rng.NextDouble(), 1.6f));
            float a = 0.35f + 0.4f * (float)rng.NextDouble() + 0.25f * MathF.Sin(time * 2 + i);
            int sz = rng.Next(5) == 0 ? ui.U(2) : ui.U(1);
            ui.Rect(new Rectangle(x, y, sz, sz), Color.White * a);
        }
    }

    /// A keep with two towers and crenellations, standing on `ground` (centre x).
    public static void Castle(this Ui ui, int cx, int ground, int scale, Color c, Color? window = null)
    {
        int k = ui.U(scale);
        void B(int x, int y, int w, int h) => ui.Rect(new Rectangle(cx + x * k, ground - y * k, w * k, h * k), c);
        B(-10, 9, 20, 9);                                  // curtain wall
        for (int i = -10; i < 10; i += 2) B(i, 10, 1, 1);  // merlons
        B(-14, 16, 5, 16); B(9, 16, 5, 16);                // towers
        for (int i = 0; i < 3; i++) { B(-14 + i * 2, 17, 1, 1); B(9 + i * 2, 17, 1, 1); }
        B(-3, 20, 6, 20); B(-3, 21, 1, 1); B(-1, 21, 2, 1); B(2, 21, 1, 1); // keep
        B(0, 26, 1, 5);                                    // flagpole
        ui.Rect(new Rectangle(cx + k, ground - 26 * k, 4 * k, 2 * k), window ?? Gilt);
        if (window is Color w)
        {
            ui.Rect(new Rectangle(cx - k, ground - 15 * k, k, 2 * k), w);
            ui.Rect(new Rectangle(cx - 12 * k, ground - 12 * k, k, k), w * 0.7f);
            ui.Rect(new Rectangle(cx + 11 * k, ground - 11 * k, k, k), w * 0.7f);
        }
    }

    // ---------- the plate ----------

    /// The ornamented panel: a drop shadow, a gradient iron fill, a dark outer line, a coloured edge, an inner hairline
    /// and four rivets. With a title, a ribbon sits across the top edge. Returns the content rectangle inside the margins.
    public static Rectangle Plate(this Ui ui, Rectangle r, string? title = null, Color? edge = null, bool solid = true)
    {
        var e = edge ?? Rivet;
        ui.PlateBody(r, e, 1, solid ? 1f : 0.88f);
        int top = ui.U(14);
        if (title != null) { ui.Ribbon(r.Center.X, r.Y - ui.U(9), title, e); top = ui.U(28); }
        return new Rectangle(r.X + ui.U(14), r.Y + top, r.Width - ui.U(28), r.Height - top - ui.U(14));
    }

    /// The plate itself: a drop shadow, a gradient iron fill, a dark outer line, the edge colour (`thick` units), a top light,
    /// an inner hairline and, on plates big enough to carry them, four rivets. `alpha` lets the map show through a little.
    public static void PlateBody(this Ui ui, Rectangle r, Color e, int thick = 1, float alpha = 1)
    {
        bool big = r.Width >= ui.U(80) && r.Height >= ui.U(48);
        Overflow.Box(r);
        if (big) ui.Rect(new Rectangle(r.X + ui.U(3), r.Y + ui.U(4), r.Width, r.Height), Color.Black * 0.3f);
        ui.VGrad(r, Iron * alpha, IronDark * alpha, 3);
        ui.Frame(r, Color.Black * 0.7f, ui.U(1));
        var inner = Inset(r, ui.U(1));
        ui.Frame(inner, Mul(e, 0.8f), ui.U(thick));
        ui.Rect(new Rectangle(inner.X + ui.U(thick), inner.Y + ui.U(thick), inner.Width - ui.U(2 * thick), ui.U(1)), Color.White * 0.1f);
        if (!big) { ui.Block(r); return; }
        ui.Frame(Inset(r, ui.U(4)), Color.White * 0.05f, ui.U(1));
        foreach (var p in new[] { new Point(r.X, r.Y), new Point(r.Right, r.Y), new Point(r.X, r.Bottom), new Point(r.Right, r.Bottom) })
        {
            int x = p.X == r.X ? r.X + ui.U(6) : r.Right - ui.U(9), y = p.Y == r.Y ? r.Y + ui.U(6) : r.Bottom - ui.U(9);
            ui.Rect(new Rectangle(x, y, ui.U(3), ui.U(3)), Mul(e, 0.6f));
            ui.Rect(new Rectangle(x, y, ui.U(2), ui.U(1)), Mul(e, 1.3f));
        }
        ui.Block(r);
    }

    /// `s` cut to fit `width` pixels at `scale`: whole words first, then letters, ending "..". Unchanged when it fits.
    public static string Fit(this Ui ui, string s, int width, int scale = 1)
    {
        if (ui.TW(s, scale) <= width) return s;
        string cut = s;
        while (cut.Length > 1 && ui.TW(cut + "..", scale) > width)
        {
            int sp = cut.LastIndexOf(' ');
            cut = sp > cut.Length / 2 ? cut[..sp].TrimEnd(',', '.', ':', ' ') : cut[..^1];
        }
        return cut.TrimEnd() + "..";
    }

    /// The largest scale (2, then 1) at which `s` fits `width`, or 1 when neither does.
    public static int FitScale(this Ui ui, string s, int width, int scale = 2) => scale > 1 && ui.TW(s, scale) > width ? 1 : scale;

    /// Text cut to fit `width` (see Fit), left-aligned or centred.
    public static void TextFit(this Ui ui, string s, float x, float y, int width, Color c, int scale = 1)
        => ui.Text(ui.Fit(s, width, scale), x, y, c, scale);
    public static void TextFitC(this Ui ui, string s, float cx, float y, int width, Color c, int scale = 1)
        => ui.TextC(ui.Fit(s, width, scale), cx, y, c, scale);

    public static Rectangle Inset(Rectangle r, int d) => new(r.X + d, r.Y + d, r.Width - 2 * d, r.Height - 2 * d);

    /// A cloth ribbon with notched tails, centred on cx, with the text in it.
    /// A band of light sweeping once across `r` as `t` runs 0..1 (a ribbon or plate catching the light as it appears).
    public static void Shine(Ui ui, Rectangle r, float t)
    {
        if (t <= 0 || t >= 1) return;
        int band = ui.U(14), x = r.X - band + (int)((r.Width + band) * Ease.InOut(t));
        for (int i = 0; i < band; i += ui.U(1))
        {
            int px = x + i;
            if (px < r.X || px >= r.Right) continue;
            float a = MathF.Sin(i / (float)band * MathF.PI) * 0.35f * MathF.Sin(t * MathF.PI);
            ui.Rect(new Rectangle(px, r.Y, ui.U(1), r.Height), Color.White * a);
        }
    }

    public static void Ribbon(this Ui ui, int cx, int y, string text, Color c, int scale = 2)
    {
        int h = ui.U(7 * scale + 8), w = ui.TW(text, scale) + ui.U(28), x = cx - w / 2, tail = ui.U(16), drop = ui.U(4), s = Math.Max(1, ui.U(1));
        var dark = Mul(c, 0.45f);
        for (int row = 0; row < h; row += s)
        {
            int notch = (int)((h / 2f - Math.Abs(row - h / 2f)) * 0.7f);
            ui.Rect(new Rectangle(x - tail + notch, y + drop + row, tail - notch + ui.U(4), s), dark);
            ui.Rect(new Rectangle(x + w - ui.U(4), y + drop + row, tail - notch + ui.U(4), s), dark);
        }
        ui.Rect(new Rectangle(x, y + h, ui.U(4), drop), Mul(c, 0.25f));
        ui.Rect(new Rectangle(x + w - ui.U(4), y + h, ui.U(4), drop), Mul(c, 0.25f));
        ui.VGrad(new Rectangle(x, y, w, h), Mul(c, 0.95f), Mul(c, 0.62f));
        ui.Rect(new Rectangle(x, y + ui.U(2), w, ui.U(1)), Color.White * 0.25f);
        ui.Rect(new Rectangle(x, y + h - ui.U(3), w, ui.U(1)), Color.Black * 0.3f);
        ui.TextC(text, cx, y + (h - ui.U(7 * scale)) / 2f, new Color(255, 248, 228), scale);
    }

    /// A rule with a diamond in the middle.
    public static void Divider(this Ui ui, int x, int y, int w, Color c)
    {
        ui.HGrad(new Rectangle(x, y, w / 2, ui.U(1)), c * 0f, c);
        ui.HGrad(new Rectangle(x + w / 2, y, w - w / 2, ui.U(1)), c, c * 0f);
        for (int i = 0; i < 4; i++) ui.Rect(new Rectangle(x + w / 2 - ui.U(i), y - ui.U(3 - i), ui.U(2 * i + 1), ui.U(1)), c);
        for (int i = 0; i < 3; i++) ui.Rect(new Rectangle(x + w / 2 - ui.U(i), y + ui.U(3 - i), ui.U(2 * i + 1), ui.U(1)), c);
    }

    // ---------- heraldry ----------

    /// A heater shield painted cell by cell in a side's colours with its charge, a dark rim and a light from the upper left.
    public static void Shield(this Ui ui, Rectangle r, Heraldry h)
    {
        const int gw = 16, gh = 19;
        int px = Math.Max(1, Math.Min(r.Width / gw, r.Height / gh));
        int ox = r.X + (r.Width - px * gw) / 2, oy = r.Y + (r.Height - px * gh) / 2;
        bool In(int i, int j)
        {
            if (i < 0 || i >= gw || j < 0 || j >= gh) return false;
            float hw = j < 9 ? 8 : 8 * MathF.Sqrt(MathF.Max(0, 1 - MathF.Pow((j - 8.5f) / 10.5f, 2)));
            return MathF.Abs(i + 0.5f - gw / 2f) < hw;
        }
        bool Charge(float u, float v) => h.Charge switch
        {
            Emblem.Chevron => MathF.Abs(v - (0.78f - MathF.Abs(u - 0.5f) * 0.95f)) < 0.09f,
            Emblem.Saltire => MathF.Abs(u - 0.5f - (v - 0.5f) * 0.84f) < 0.075f || MathF.Abs(u - 0.5f + (v - 0.5f) * 0.84f) < 0.075f,
            Emblem.Lozenge => MathF.Abs(u - 0.5f) + MathF.Abs(v - 0.45f) * 0.8f < 0.2f,
            Emblem.Bend => MathF.Abs(u - v * 0.84f - 0.08f) < 0.085f,
            Emblem.Cross => MathF.Abs(u - 0.5f) < 0.07f || MathF.Abs(v - 0.4f) < 0.07f,
            Emblem.Pale => MathF.Abs(u - 0.5f) < 0.12f,
            Emblem.Fess => MathF.Abs(v - 0.45f) < 0.11f,
            Emblem.Sun => (u - 0.5f) * (u - 0.5f) + (v - 0.45f) * (v - 0.45f) < 0.02f
                || MathF.Abs(MathF.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.45f) * (v - 0.45f)) - 0.22f) < 0.04f && ((int)((MathF.Atan2(v - 0.45f, u - 0.5f) + MathF.PI) / (MathF.PI / 4)) % 2 == 0),
            _ => false,
        };
        for (int j = 0; j < gh; j++)
            for (int i = 0; i < gw; i++)
            {
                if (!In(i, j)) continue;
                float u = (i + 0.5f) / gw, v = (j + 0.5f) / gh;
                bool rim = !In(i - 1, j) || !In(i + 1, j) || !In(i, j - 1) || !In(i, j + 1);
                Color c = rim ? Mul(h.ClothDark, 0.6f)
                    : Charge(u, v) ? h.Trim
                    : h.Charge == Emblem.None && u > 0.5f ? h.ClothDark : h.Cloth;
                if (!rim) c = Color.Lerp(c, Color.White, MathF.Max(0, 0.22f - (u + v) * 0.22f));
                if (!rim && i + j > 24) c = Mul(c, 0.82f);
                ui.Rect(new Rectangle(ox + i * px, oy + j * px, px, px), c);
            }
    }

    /// A hanging banner on a crossbar: the side's cloth with a swallowtail and its charge's colour as a stripe.
    public static void Banner(this Ui ui, int cx, int y, int w, int h, Heraldry hd, float sway = 0)
    {
        ui.Rect(new Rectangle(cx - w / 2 - ui.U(4), y, w + ui.U(8), ui.U(3)), Mul(Kit.Rivet, 0.8f));
        ui.Rect(new Rectangle(cx - w / 2 - ui.U(6), y - ui.U(1), ui.U(3), ui.U(5)), Gilt);
        ui.Rect(new Rectangle(cx + w / 2 + ui.U(3), y - ui.U(1), ui.U(3), ui.U(5)), Gilt);
        int s = Math.Max(1, ui.U(1));
        for (int row = 0; row < h; row += s)
        {
            float t = row / (float)h;
            int dx = (int)(MathF.Sin(t * 3 + sway) * ui.U(2) * t);
            int cut = row > h - w / 2 ? (int)((row - (h - w / 2)) * 1f) : 0;
            var cloth = Color.Lerp(hd.Cloth, hd.ClothDark, t * 0.6f);
            int x0 = cx - w / 2 + dx, half = w / 2;
            if (cut < half) { ui.Rect(new Rectangle(x0, y + ui.U(3) + row, half - cut, s), cloth); ui.Rect(new Rectangle(x0 + half + cut, y + ui.U(3) + row, half - cut, s), Mul(cloth, 0.86f)); }
            if (row > ui.U(4) && row < ui.U(7)) ui.Rect(new Rectangle(x0, y + ui.U(3) + row, w, s), hd.Trim);
        }
        ui.Shield(new Rectangle(cx - w * 2 / 5, y + ui.U(12), w * 4 / 5, (int)(w * 4 / 5 * 1.2f)), hd);
    }

    // ---------- icons ----------

    /// 7x7 pixel icons: X is the colour, o a darker shade of it.
    static readonly Dictionary<string, string[]> Icons = new()
    {
        ["coin"] = new[] { "..XXX..", ".XoooX.", "XoXXXoX", "XoXoXoX", "XoXXXoX", ".XoooX.", "..XXX.." },
        ["heart"] = new[] { ".XX.XX.", "XXXXXoX", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." },
        ["shield"] = new[] { "XXXXXXX", "XXXoXXX", "XXXoXXX", "XoooooX", "XXXoXXX", ".XXoXX.", "..XXX.." },
        ["sword"] = new[] { "......X", ".....XX", "....XX.", "X..XX..", ".XXX...", ".oX....", "o..X..." },
        ["skull"] = new[] { ".XXXXX.", "XXXXXXX", "X..X..X", "XXXXXXX", ".XX.XX.", ".XXXXX.", ".X.X.X." },
        ["crown"] = new[] { ".......", "X..X..X", "XX.X.XX", "XXXXXXX", "XoXoXoX", "XXXXXXX", "......." },
        ["tower"] = new[] { "X.X.X.X", "XXXXXXX", ".XXXXX.", ".XoXoX.", ".XXXXX.", ".XX.XX.", ".XX.XX." },
        ["bread"] = new[] { ".......", "..XXX..", ".XXXXX.", "XXoXoXX", "XXXXXXX", ".ooooo.", "......." },
        ["horse"] = new[] { "...XX..", "..XXXX.", ".XXXoXX", "XXXX.XX", "XXX....", ".XX....", ".XX...." },
        ["arrow"] = new[] { "...XXXX", ".....XX", "....X.X", "...X..X", "..X....", "oo.....", "oo....." },
        ["star"] = new[] { "...X...", "...X...", "XXXXXXX", ".XXXXX.", "..XXX..", ".XX.XX.", "X.....X" },
        ["flag"] = new[] { "XXXXXXX", "XooooX.", "XXXXXXX", "X......", "X......", "X......", "X......" },
        ["clock"] = new[] { "..XXX..", ".X.X.X.", "X..X..X", "X..XX.X", "X.....X", ".X...X.", "..XXX.." },
        ["up"] = new[] { "...X...", "..XXX..", ".XX.XX.", "XX...XX", "...X...", "..XXX..", ".XX.XX." },
        ["chain"] = new[] { ".XX....", "X..X...", "X..XX..", ".XX..X.", "...X..X", "....X.X", ".....X." },
        ["sun"] = new[] { "X..X..X", ".X...X.", "..XXX..", "XXXXXXX", "..XXX..", ".X...X.", "X..X..X" },
        ["rain"] = new[] { ".XXXX..", "XXXXXX.", "XXXXXXX", ".......", "X.X.X..", ".X.X.X.", "X.X.X.." },
        ["fire"] = new[] { "...X...", "..XX...", "..XXX.X", ".XXoXXX", "XXooXXX", "XXoooXX", ".XXXXX." },
        ["scroll"] = new[] { "XXXXXX.", "XoooooX", ".XoooX.", ".XoooX.", ".XoooX.", "XoooooX", ".XXXXXX" },
        ["spear"] = new[] { ".....X.", "....XXX", "...XXoX", "..X.XX.", ".X.....", "X......", "......." },
        ["boot"] = new[] { "..XXX..", "..XXX..", "..XXX..", "..XXX..", "..XXXX.", "XXXXXXX", "XXXXXXX" },
    };

    public static void Icon(this Ui ui, string name, int x, int y, Color c, int scale = 2)
    {
        var rows = Icons[name];
        int p = ui.U(scale);
        var dark = Mul(c, 0.55f);
        for (int j = 0; j < 7; j++)
            for (int i = 0; i < 7; i++)
            {
                char ch = rows[j][i];
                if (ch == '.') continue;
                ui.Rect(new Rectangle(x + i * p + ui.U(1), y + j * p + ui.U(1), p, p), Color.Black * 0.5f);
                ui.Rect(new Rectangle(x + i * p, y + j * p, p, p), ch == 'o' ? dark : c);
            }
    }

    public static IEnumerable<string> IconNames => Icons.Keys;

    // ---------- readouts ----------

    /// A ring of square segments filled clockwise from the top, with the value in the middle.
    public static void Ring(this Ui ui, Vector2 c, float r, float frac, Color col, string? label = null, string? caption = null)
    {
        const int n = 32;
        int sz = ui.U(4);
        frac = Ui.Anim(HashCode.Combine((int)c.X, (int)c.Y, 0x41), frac, 8);
        for (int i = 0; i < n; i++)
        {
            float a = -MathF.PI / 2 + i / (float)n * MathF.Tau;
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            bool on = i < frac * n - 0.01f;
            ui.Rect(new Rectangle((int)p.X - sz / 2, (int)p.Y - sz / 2, sz, sz), on ? col : Color.White * 0.08f);
        }
        // A caption too wide for the ring's inside, or any on a small ring, goes under it, clear of the dots.
        bool below = caption != null && (r < ui.U(24) || ui.TW(caption, 1) > r * 1.2f);
        if (label != null) ui.TextC(label, c.X, c.Y - ui.U(caption == null || below ? 7 : 10), Style.Text, 2);
        if (caption != null) ui.TextC(caption, c.X, below ? c.Y + r + ui.U(5) : c.Y + ui.U(6), Style.Dim, 1);
    }

    /// A bar in separate cells (pips), on a dark trough.
    public static void Pips(this Ui ui, Rectangle r, float frac, Color c, int cells = 10)
    {
        ui.Rect(r, Color.Black * 0.55f);
        int gap = ui.U(1), cw = (r.Width - gap * (cells + 1)) / cells;
        for (int i = 0; i < cells; i++)
        {
            var cell = new Rectangle(r.X + gap + i * (cw + gap), r.Y + gap, cw, r.Height - 2 * gap);
            float f = Math.Clamp(frac * cells - i, 0, 1);
            ui.Rect(cell, Color.White * 0.05f);
            if (f > 0) { ui.Rect(new Rectangle(cell.X, cell.Y, (int)(cell.Width * f), cell.Height), c); ui.Rect(new Rectangle(cell.X, cell.Y, (int)(cell.Width * f), ui.U(1)), Color.White * 0.3f); }
        }
    }

    /// A bar with a bevel: a lit top line and a dark bottom line over the fill.
    public static void Gauge(this Ui ui, Rectangle r, float frac, Color c)
    {
        ui.Rect(r, Color.Black * 0.6f);
        var f = new Rectangle(r.X + ui.U(1), r.Y + ui.U(1), (int)((r.Width - ui.U(2)) * Ui.Anim(Ui.Key(r, 0x9a), Math.Clamp(frac, 0, 1), 10)), r.Height - ui.U(2));
        ui.VGrad(f, Mul(c, 1.15f), Mul(c, 0.7f), 1);
        ui.Rect(new Rectangle(f.X, f.Y, f.Width, ui.U(1)), Color.White * 0.35f);
    }

    /// Two forces pulling on one bar from either end, with the split marked.
    public static void TugBar(this Ui ui, Rectangle r, float left, Color lc, Color rc)
    {
        ui.Rect(Inset(r, -ui.U(1)), Color.Black * 0.7f);
        int split = r.X + (int)(r.Width * Math.Clamp(left, 0, 1));
        ui.VGrad(new Rectangle(r.X, r.Y, split - r.X, r.Height), Mul(lc, 1.1f), Mul(lc, 0.6f), 1);
        ui.VGrad(new Rectangle(split, r.Y, r.Right - split, r.Height), Mul(rc, 1.1f), Mul(rc, 0.6f), 1);
        ui.Rect(new Rectangle(r.X, r.Y, r.Width, ui.U(1)), Color.White * 0.25f);
        ui.Rect(new Rectangle(split - ui.U(1), r.Y - ui.U(3), ui.U(3), r.Height + ui.U(6)), Style.Text);
    }

    /// A line chart with a soft fill beneath; values are 0..1.
    public static void Chart(this Ui ui, Rectangle r, float[] v, Color c, bool fill = true)
    {
        if (v.Length < 2) return;
        Vector2 P(int i) => new(r.X + i * (r.Width - 1) / (float)(v.Length - 1), r.Bottom - v[i] * r.Height);
        if (fill)
        {
            int s = Math.Max(1, ui.U(2));
            for (int x = r.X; x < r.Right; x += s)
            {
                float t = (x - r.X) / (float)r.Width * (v.Length - 1);
                int i = Math.Min((int)t, v.Length - 2);
                float y = r.Bottom - MathHelper.Lerp(v[i], v[i + 1], t - i) * r.Height;
                ui.VGrad(new Rectangle(x, (int)y, s, r.Bottom - (int)y), c * 0.35f, c * 0.02f, 3);
            }
        }
        for (int i = 0; i < v.Length - 1; i++) ui.Line(P(i), P(i + 1), c, 2);
        for (int i = 0; i < v.Length; i += Math.Max(1, v.Length / 6)) ui.Rect(new Rectangle((int)P(i).X - ui.U(2), (int)P(i).Y - ui.U(2), ui.U(4), ui.U(4)), c);
    }

    // ---------- controls ----------

    /// A small coloured tag; returns its width.
    public static int Chip(this Ui ui, int x, int y, string text, Color c, string? icon = null)
    {
        int iw = icon != null ? ui.U(10) : 0, w = ui.TW(text, 1) + ui.U(10) + iw, h = ui.U(13);
        ui.Rect(new Rectangle(x, y, w, h), Mul(c, 0.3f) * 0.9f);
        ui.Frame(new Rectangle(x, y, w, h), Mul(c, 0.85f), ui.U(1));
        if (icon != null) ui.Icon(icon, x + ui.U(4), y + ui.U(3), c, 1);
        ui.Text(text, x + ui.U(5) + iw, y + ui.U(3), Color.Lerp(c, Color.White, 0.55f), 1);
        return w;
    }

    /// The gilded button: a bevelled gold-edged plate that lights on hover. Returns true on click.
    public static bool Gilded(this Ui ui, Rectangle r, string label, string key = "", bool enabled = true, Color? tone = null)
    {
        bool hover = enabled && ui.Hover(r);
        var t = tone ?? Gilt;
        Overflow.Box(r);
        ui.Rect(new Rectangle(r.X + ui.U(2), r.Y + ui.U(3), r.Width, r.Height), Color.Black * 0.4f);
        ui.VGrad(r, hover ? Mul(t, 0.55f) : new Color(44, 42, 44), hover ? Mul(t, 0.25f) : new Color(24, 24, 28), 1);
        ui.Frame(r, enabled ? (hover ? Mul(t, 1.2f) : Mul(t, 0.75f)) : Color.White * 0.15f, ui.U(1));
        ui.Rect(new Rectangle(r.X + ui.U(1), r.Y + ui.U(1), r.Width - ui.U(2), ui.U(1)), Color.White * (hover ? 0.35f : 0.14f));
        ui.Rect(new Rectangle(r.X + ui.U(1), r.Bottom - ui.U(2), r.Width - ui.U(2), ui.U(1)), Color.Black * 0.4f);
        if (hover) { ui.Rect(new Rectangle(r.X + ui.U(5), r.Center.Y - ui.U(2), ui.U(4), ui.U(4)), t); ui.Rect(new Rectangle(r.Right - ui.U(9), r.Center.Y - ui.U(2), ui.U(4), ui.U(4)), t); }
        int scale = r.Height >= ui.U(26) ? 2 : 1;
        var c = enabled ? (hover ? new Color(255, 246, 220) : Style.Text) : Style.Dim * 0.5f;
        ui.TextC(label, r.Center.X, r.Y + (r.Height - ui.U(7 * scale)) / 2f, c, scale);
        if (key != "") ui.TextR(key, r.Right - ui.U(hover ? 14 : 8), r.Y + (r.Height - ui.U(7)) / 2f, Style.Dim, 1);
        ui.Block(r);
        bool hit = enabled && ui.Clicked(r);
        if (hit) Ui.OnClick?.Invoke();
        return hit;
    }

    public static bool Toggle(this Ui ui, Rectangle r, string label, ref bool on)
    {
        var sw = new Rectangle(r.Right - ui.U(30), r.Y + (r.Height - ui.U(14)) / 2, ui.U(30), ui.U(14));
        ui.Text(label, r.X, r.Y + (r.Height - ui.U(7)) / 2f, Style.Text, 1);
        ui.Rect(sw, on ? Mul(Style.Ok, 0.45f) : Color.Black * 0.6f);
        ui.Frame(sw, on ? Style.Ok : Style.PanelEdge, ui.U(1));
        var knob = new Rectangle(on ? sw.Right - ui.U(12) : sw.X + ui.U(2), sw.Y + ui.U(2), ui.U(10), ui.U(10));
        ui.VGrad(knob, Style.Text, Style.Dim, 1);
        bool hit = ui.Clicked(r);
        if (hit) { on = !on; Ui.OnClick?.Invoke(); }
        return hit;
    }

    public static void Check(this Ui ui, Rectangle r, string label, ref bool on)
    {
        var box = new Rectangle(r.X, r.Y + (r.Height - ui.U(12)) / 2, ui.U(12), ui.U(12));
        ui.Rect(box, Color.Black * 0.6f);
        ui.Frame(box, on ? Gilt : Style.PanelEdge, ui.U(1));
        if (on) { ui.Rect(Inset(box, ui.U(3)), Gilt); ui.Rect(new Rectangle(box.X + ui.U(3), box.Y + ui.U(3), ui.U(6), ui.U(1)), Color.White * 0.5f); }
        ui.Text(label, box.Right + ui.U(8), r.Y + (r.Height - ui.U(7)) / 2f, Style.Text, 1);
        if (ui.Clicked(r)) { on = !on; Ui.OnClick?.Invoke(); }
    }

    /// A slider with a diamond knob; dragging is a click per frame while the button is held.
    public static void Slider(this Ui ui, Rectangle r, string label, ref float v, bool held)
    {
        ui.Text(label, r.X, r.Y, Style.Text, 1);
        ui.TextR($"{(int)(v * 100)}%", r.Right, r.Y, Gilt, 1);
        var track = new Rectangle(r.X, r.Y + ui.U(14), r.Width, ui.U(4));
        ui.Rect(track, Color.Black * 0.6f);
        ui.HGrad(new Rectangle(track.X, track.Y, (int)(track.Width * v), track.Height), GiltDark, Gilt);
        int kx = track.X + (int)(track.Width * v);
        for (int i = 0; i < 5; i++) ui.Rect(new Rectangle(kx - ui.U(i), track.Center.Y - ui.U(5 - i), ui.U(2 * i + 1), ui.U(1)), Style.Text);
        for (int i = 0; i < 5; i++) ui.Rect(new Rectangle(kx - ui.U(i), track.Center.Y + ui.U(5 - i), ui.U(2 * i + 1), ui.U(1)), Style.Dim);
        var hit = new Rectangle(track.X, track.Y - ui.U(6), track.Width, track.Height + ui.U(12));
        if (held && ui.Hover(hit)) v = Math.Clamp((ui.Mouse.X - track.X) / (float)track.Width, 0, 1);
        ui.Block(hit);
    }

    /// A number with minus and plus; returns the change (-1, 0, +1).
    public static int Stepper(this Ui ui, Rectangle r, int value)
    {
        int bw = r.Height;
        var minus = new Rectangle(r.X, r.Y, bw, r.Height); var plus = new Rectangle(r.Right - bw, r.Y, bw, r.Height);
        ui.Rect(r, Color.Black * 0.5f);
        ui.Frame(r, Style.PanelEdge, ui.U(1));
        int d = 0;
        if (ui.Button(minus, "-")) d = -1;
        if (ui.Button(plus, "+")) d = 1;
        ui.TextC(value.ToString(), r.Center.X, r.Y + (r.Height - ui.U(7)) / 2f, Style.Text, 1);
        return d;
    }

    /// Tabs as raised cards; the active one joins the line beneath. Returns the clicked index or -1.
    public static int Tabs(this Ui ui, Rectangle r, string[] labels, int active)
    {
        int w = r.Width / labels.Length, hit = -1;
        ui.Rect(new Rectangle(r.X, r.Bottom - ui.U(1), r.Width, ui.U(1)), Gilt * 0.7f);
        for (int i = 0; i < labels.Length; i++)
        {
            bool on = i == active;
            var t = new Rectangle(r.X + i * w + ui.U(1), r.Y + (on ? 0 : ui.U(3)), w - ui.U(2), r.Height - (on ? 0 : ui.U(3)));
            bool hover = ui.Hover(t);
            ui.VGrad(t, on ? new Color(52, 50, 48) : hover ? new Color(40, 40, 44) : new Color(28, 30, 36), on ? Iron : IronDark, 1);
            ui.Frame(t, on ? Gilt * 0.8f : Style.PanelEdge, ui.U(1));
            if (on) ui.Rect(new Rectangle(t.X + ui.U(1), t.Bottom - ui.U(1), t.Width - ui.U(2), ui.U(1)), Iron);
            ui.TextC(labels[i], t.Center.X, t.Y + (t.Height - ui.U(7)) / 2f, on ? Gilt : hover ? Style.Text : Style.Dim, 1);
            if (ui.Clicked(t)) { hit = i; Ui.OnClick?.Invoke(); }
        }
        // A gold mark along the top of the chosen tab slides over when the choice changes.
        float x = Ui.Anim(Ui.Key(r, 0x7ab), active * w, 22);
        if (active >= 0) ui.Rect(new Rectangle(r.X + (int)x + ui.U(2), r.Y, w - ui.U(4), ui.U(2)), Gilt);
        return hit;
    }

    /// A line of the event feed: a coloured edge, an icon and the text, fading to the right.
    public static void Toast(this Ui ui, int x, int y, string text, Color c, string icon, float alpha = 1)
    {
        int w = ui.TW(text, 1) + ui.U(30);
        ui.HGrad(new Rectangle(x, y, w + ui.U(30), ui.U(18)), Mul(c, 0.35f) * (0.9f * alpha), c * 0f);
        ui.Rect(new Rectangle(x, y, ui.U(2), ui.U(18)), c * alpha);
        ui.Icon(icon, x + ui.U(8), y + ui.U(5), c * alpha, 1);
        ui.Text(text, x + ui.U(22), y + ui.U(6), Color.Lerp(c, Color.White, 0.6f) * alpha, 1);
    }

    /// A square order key: an icon over its key letter, gold when chosen.
    public static bool OrderKey(this Ui ui, Rectangle r, string icon, string key, bool on)
    {
        bool hover = ui.Hover(r);
        ui.Rect(new Rectangle(r.X + ui.U(2), r.Y + ui.U(3), r.Width, r.Height), Color.Black * 0.4f);
        ui.VGrad(r, on ? Mul(Gilt, 0.55f) : hover ? new Color(50, 50, 54) : new Color(30, 32, 38), on ? Mul(Gilt, 0.2f) : IronDark, 1);
        ui.Frame(r, on ? Gilt : Color.Black * 0.7f, ui.U(1));
        ui.Icon(icon, r.Center.X - ui.U(7), r.Y + ui.U(5), on ? Color.White : Style.Text, 2);
        ui.TextC(key, r.Center.X, r.Bottom - ui.U(10), on ? Parchment : Style.Dim, 1);
        ui.Block(r);
        bool hit = ui.Clicked(r);
        if (hit) Ui.OnClick?.Invoke();
        return hit;
    }

    /// A tooltip box with a pointer under it, its bottom tip at (x, y).
    public static void Tooltip(this Ui ui, int x, int y, string title, params string[] lines)
    {
        if (Ui.Since(HashCode.Combine(title, 0x7f)) < 0.25f) return; // a tooltip waits a moment, so a passing mouse does not flash one
        int w = Math.Max(ui.TW(title, 1), lines.Select(l => ui.TW(l, 1)).DefaultIfEmpty(0).Max()) + ui.U(16);
        int h = ui.U(18 + 11 * lines.Length);
        var r = new Rectangle(x - w / 2, y - h - ui.U(6), w, h);
        ui.Rect(new Rectangle(r.X + ui.U(2), r.Y + ui.U(3), r.Width, r.Height), Color.Black * 0.4f);
        ui.Rect(r, new Color(10, 10, 14) * 0.95f);
        ui.Frame(r, Gilt * 0.8f, ui.U(1));
        for (int i = 0; i < 6; i++) ui.Rect(new Rectangle(x - ui.U(6 - i), r.Bottom + ui.U(i) - ui.U(1), ui.U(2 * (6 - i)), ui.U(1)), i == 0 ? new Color(10, 10, 14) : Gilt * 0.8f);
        ui.Text(title, r.X + ui.U(8), r.Y + ui.U(6), Gilt, 1);
        for (int i = 0; i < lines.Length; i++) ui.Text(lines[i], r.X + ui.U(8), r.Y + ui.U(18 + 11 * i), Style.Dim, 1);
    }

    /// Words broken into lines no wider than `w`.
    public static List<string> Wrap(this Ui ui, string text, int w, int scale = 1)
    {
        var lines = new List<string>();
        string cur = "";
        foreach (var word in text.Split(' '))
        {
            string next = cur == "" ? word : cur + " " + word;
            if (ui.TW(next, scale) > w && cur != "") { lines.Add(cur); cur = word; }
            else cur = next;
        }
        if (cur != "") lines.Add(cur);
        return lines;
    }
}
