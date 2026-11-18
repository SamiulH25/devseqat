using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class PixelMap
{
    /// The ground's palette: every land colour is snapped to a coarse grid of hue, saturation and value before it gets its
    /// ramp, so the map holds a few dozen ramps rather than a smooth gradient, and each ramp is hue-shifted as the sprites
    /// are (cool shadows, warm lights).
    static class Ramps
    {
        public static readonly Vector3 Grass = new(0.43f, 0.56f, 0.27f);

        public static readonly Color[] Sea = Of(new(14, 34, 58), new(22, 56, 86), new(32, 84, 112), new(52, 122, 136), new(190, 222, 214));
        public static readonly Color[] Fresh = Of(new(22, 50, 66), new(34, 74, 92), new(48, 102, 118), new(76, 138, 146), new(200, 228, 222));
        public static readonly Color[] Road = Ramp(new Vector3(0.6f, 0.5f, 0.34f), 0.8f);
        public static readonly Color[] Hedge = Ramp(new Vector3(0.22f, 0.32f, 0.15f), 0.9f);

        static Color[] Of(params Color[] c) => c;

        static readonly Dictionary<int, Color[]> cache = new();

        /// A field plot's ramp by what it grows (a little variety by plot); autumn browns the green ones.
        public static Color[] Field(FieldKind kind, int seed, int season)
        {
            float j = (seed & 7) / 7f - 0.5f;
            var b = kind switch
            {
                FieldKind.Grain => new Vector3(0.78f + 0.05f * j, 0.66f, 0.32f),
                FieldKind.Green => new Vector3(0.44f, 0.6f + 0.05f * j, 0.26f),
                FieldKind.Ploughed => new Vector3(0.46f, 0.34f + 0.03f * j, 0.22f),
                FieldKind.Fallow => new Vector3(0.58f, 0.55f + 0.04f * j, 0.33f),
                FieldKind.Pasture => new Vector3(0.5f, 0.64f, 0.31f + 0.04f * j),
                _ => new Vector3(0.36f, 0.5f, 0.24f),
            };
            if (season == 2 && kind is FieldKind.Green or FieldKind.Pasture) b = Vector3.Lerp(b, new Vector3(0.62f, 0.52f, 0.28f), 0.5f);
            return Land(b);
        }

        /// The ramp for a land colour, snapped first (hue in 9 degree steps, saturation and value in coarse steps).
        public static Color[] Land(Vector3 c)
        {
            var (h, s, v) = Hsv(Vector3.Clamp(c, Vector3.Zero, Vector3.One));
            int hi = (int)MathF.Round(h / 9) % 40, si = (int)MathF.Round(s / 0.07f), vi = (int)MathF.Round(v / 0.085f);
            int key = hi * 10000 + si * 100 + vi;
            lock (cache)
            {
                if (cache.TryGetValue(key, out var r)) return r;
                r = Ramp(Rgb(hi * 9, si * 0.07f, vi * 0.085f), 1);
                cache[key] = r;
                return r;
            }
        }

        /// Five tones from one base: the ground's ramp is gentler than a sprite's (land is broad, men are small).
        public static Color[] Ramp(Vector3 b, float spread)
        {
            Vector3 cool = new(0.16f, 0.16f, 0.3f), warm = new(1f, 0.93f, 0.72f);
            Color C(Vector3 v) => new(Vector3.Clamp(v, Vector3.Zero, Vector3.One));
            return new[]
            {
                C(Vector3.Lerp(b * (1 - 0.4f * spread), cool, 0.22f * spread)),
                C(Vector3.Lerp(b * (1 - 0.2f * spread), cool, 0.1f * spread)),
                C(b),
                C(Vector3.Lerp(b * (1 + 0.13f * spread), warm, 0.1f * spread)),
                C(Vector3.Lerp(b * (1 + 0.26f * spread), warm, 0.24f * spread)),
            };
        }

        public static Color Darken(Color c, float f) => new(Vector3.Lerp(c.ToVector3() * f, new Vector3(0.1f, 0.1f, 0.2f), 0.12f));
        public static Color Foam(Color c) => new(Vector3.Lerp(c.ToVector3(), new Vector3(0.82f, 0.9f, 0.86f), 0.55f));

        static (float H, float S, float V) Hsv(Vector3 c)
        {
            float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z)), d = max - min;
            float h = d < 1e-5f ? 0 : max == c.X ? 60 * (((c.Y - c.Z) / d) % 6) : max == c.Y ? 60 * ((c.Z - c.X) / d + 2) : 60 * ((c.X - c.Y) / d + 4);
            if (h < 0) h += 360;
            return (h, max < 1e-5f ? 0 : d / max, max);
        }

        static Vector3 Rgb(float h, float s, float v)
        {
            float cc = v * s, x = cc * (1 - MathF.Abs(h / 60 % 2 - 1)), m = v - cc;
            var (r, g, b) = (int)(h / 60) switch
            {
                0 => (cc, x, 0f), 1 => (x, cc, 0f), 2 => (0f, cc, x), 3 => (0f, x, cc), 4 => (x, 0f, cc), _ => (cc, 0f, x),
            };
            return new Vector3(r + m, g + m, b + m);
        }
    }
}
