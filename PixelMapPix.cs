using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class PixelMap
{
    /// A finished sprite: its pixels and where its foot stands (in its own pixels).
    sealed record Sprite(Color[] Px, int W, int H, int FootX, int FootY);

    /// A pixel canvas for sprites: whole pixels only (no shapes scaled to a size, no smoothing), colours picked from
    /// five-tone ramps by hand, and an outline added round the finished silhouette in a darkened shade of whatever
    /// it borders (a selective outline, as hand-made sprites have).
    sealed class Pix
    {
        public readonly int W, H;
        public readonly Color[] P;
        public Pix(int w, int h) { W = w; H = h; P = new Color[w * h]; }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public void Set(int x, int y, Color c) { if (In(x, y)) P[y * W + x] = c; }
        public Color Get(int x, int y) => In(x, y) ? P[y * W + x] : Color.Transparent;
        public bool Has(int x, int y) => In(x, y) && P[y * W + x].A != 0;
        public void Clear(int x, int y) { if (In(x, y)) P[y * W + x] = Color.Transparent; }

        /// A filled rectangle, both corners inclusive.
        public void Rect(int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c);
        }

        public void HLine(int x0, int x1, int y, Color c) => Rect(x0, y, x1, y, c);
        public void VLine(int x, int y0, int y1, Color c) => Rect(x, y0, x, y1, c);

        /// A filled ellipse, its colour chosen per pixel (so a ball can be lit from one side).
        public void Oval(float cx, float cy, float rx, float ry, Func<int, int, Color> col)
        {
            for (int y = (int)MathF.Floor(cy - ry); y <= (int)MathF.Ceiling(cy + ry); y++)
                for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1) Set(x, y, col(x, y));
                }
        }

        public void Oval(float cx, float cy, float rx, float ry, Color c) => Oval(cx, cy, rx, ry, (_, _) => c);

        /// Another canvas laid on this one with its top left at (x, y).
        public void Put(Pix o, int x, int y, bool flip = false)
        {
            for (int yy = 0; yy < o.H; yy++)
                for (int xx = 0; xx < o.W; xx++)
                {
                    var c = o.P[yy * o.W + (flip ? o.W - 1 - xx : xx)];
                    if (c.A != 0) Set(x + xx, y + yy, c);
                }
        }

        /// The outline: every empty pixel touching the silhouette (not on a diagonal) takes a dark shade of its neighbour.
        public Pix Outline()
        {
            var add = new List<(int, Color)>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (Has(x, y)) continue;
                    Color? n = Has(x, y + 1) ? Get(x, y + 1) : Has(x - 1, y) ? Get(x - 1, y) : Has(x + 1, y) ? Get(x + 1, y) : Has(x, y - 1) ? Get(x, y - 1) : null;
                    if (n is { } c) add.Add((y * W + x, Ink(c)));
                }
            foreach (var (i, c) in add) P[i] = c;
            return this;
        }

        public Sprite Done(int footX, int footY) => new(P, W, H, footX, footY);
    }

    /// The outline's shade of a colour: much darker, pulled toward a deep purple so it never reads as flat black.
    static Color Ink(Color c) => new(Vector3.Lerp(c.ToVector3() * 0.32f, new Vector3(0.1f, 0.07f, 0.12f), 0.5f));

    /// Five tones from one colour, darkest first (hue-shifted: cool shadows, warm lights).
    static Color[] Tones(Color c, float spread = 1.15f) => Ramps.Ramp(c.ToVector3(), spread);
    static Color[] Tones(int r, int g, int b, float spread = 1.15f) => Tones(new Color(r, g, b), spread);

    static readonly Dictionary<string, Sprite> sprites = new();

    static Sprite Cached(string key, Func<Sprite> make)
    {
        lock (sprites)
        {
            if (!sprites.TryGetValue(key, out var s)) sprites[key] = s = make();
            return s;
        }
    }

    static uint Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    /// A realm's charge in pixels on a flag, shield or caparison (the area x0..x1, y0..y1 inclusive).
    static void Charge(Pix cv, int x0, int y0, int x1, int y1, Emblem e, Color c)
    {
        int cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, w = x1 - x0, h = y1 - y0;
        switch (e)
        {
            case Emblem.Chevron:
                for (int i = 0; i <= w / 2; i++) { cv.Set(cx - i, cy - 1 + i * h / Math.Max(1, w), c); cv.Set(cx + i, cy - 1 + i * h / Math.Max(1, w), c); }
                break;
            case Emblem.Saltire:
                for (int i = 0; i <= h; i++) { cv.Set(x0 + i * w / Math.Max(1, h), y0 + i, c); cv.Set(x1 - i * w / Math.Max(1, h), y0 + i, c); }
                break;
            case Emblem.Bend:
                for (int i = 0; i <= h; i++) { int x = x0 + i * w / Math.Max(1, h); cv.Set(x, y0 + i, c); cv.Set(x + 1, y0 + i, c); }
                break;
            case Emblem.Cross: cv.VLine(cx, y0, y1, c); cv.HLine(x0, x1, cy, c); break;
            case Emblem.Pale: cv.Rect(cx - 1, y0, cx, y1, c); break;
            case Emblem.Fess: cv.Rect(x0, cy - 1, x1, cy, c); break;
            case Emblem.Lozenge: cv.Set(cx, cy - 1, c); cv.HLine(cx - 1, cx + 1, cy, c); cv.Set(cx, cy + 1, c); break;
            case Emblem.Sun: cv.HLine(cx - 1, cx, cy - 1, c); cv.HLine(cx - 1, cx, cy, c); break;
        }
    }
}
