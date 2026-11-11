using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// World: the campaign map's ground materials (docs/WORLD_LIFE.md, the grounded look). One tileable 256x256 texture,
/// made in code, holds four greyscale patterns the world shader colours and blends by slope and height: R grass (fine
/// blades and tufts), G earth (pebbles and clods), B rock (strata and cracks), A broad variation (sampled at a few
/// hundred metres, it breaks the tiling and shifts the hue across the land). Battles keep their plain grey detail.
sealed partial class World
{
    const int MaterialSize = 256;

    Texture2D BuildMaterials()
    {
        int s = MaterialSize, seed = t.Seed + 5;
        var data = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float grass = Fbm(u, v, 32, 3, seed) * 0.6f + Fbm(u, v, 96, 2, seed + 1) * 0.4f;
                grass = Math.Clamp(0.5f + (grass - 0.5f) * 1.6f, 0, 1);
                float earth = Fbm(u, v, 16, 3, seed + 2) * 0.7f + Fbm(u, v, 64, 2, seed + 3) * 0.3f;
                if (Periodic(u, v, 48, seed + 4) > 0.82f) earth -= 0.25f;                              // a stone
                float rock = 1 - MathF.Abs(Fbm(u, v * 0.35f + Fbm(u, v, 8, 2, seed + 6) * 0.25f, 12, 3, seed + 5) * 2 - 1); // strata
                rock = rock * 0.7f + Fbm(u, v, 48, 2, seed + 7) * 0.3f;
                float broad = Fbm(u, v, 3, 3, seed + 8);
                data[y * s + x] = new Color(Math.Clamp(grass, 0, 1), Math.Clamp(earth, 0, 1), Math.Clamp(rock, 0, 1), Math.Clamp(broad, 0, 1));
            }
        return Art.Mipped(gd, data, s);
    }

    /// Value noise that wraps every `freq` cells across the unit square (so the texture tiles).
    static float Periodic(float u, float v, int freq, int seed)
    {
        float x = u * freq, y = v * freq;
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float H(int i, int j)
        {
            i = ((i % freq) + freq) % freq; j = ((j % freq) + freq) % freq;
            uint h = (uint)(i * 374761393 + j * 668265263 + seed * 2246822519u);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
        float a = MathHelper.Lerp(H(x0, y0), H(x0 + 1, y0), fx), b = MathHelper.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx);
        return MathHelper.Lerp(a, b, fy);
    }

    static float Fbm(float u, float v, int freq, int octaves, int seed)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < octaves; o++, freq *= 2, amp *= 0.5f)
        {
            sum += Periodic(u, v, freq, seed + o * 31) * amp;
            norm += amp;
        }
        return sum / norm;
    }

    /// How sunk a terrain vertex lies against its neighbours (0 = open ground, 1 = a deep fold): stored in the vertex's
    /// alpha on the campaign map so hollows, gullies and valley floors take a little shade.
    float Cavity(int x, int z)
    {
        int n = t.N;
        float h = t.GridHeight(x, z), sum = 0;
        int c = 0;
        for (int r = 1; r <= 3; r += 2)
            foreach (var (dx, dz) in new[] { (r, 0), (-r, 0), (0, r), (0, -r) })
            {
                int xx = Math.Clamp(x + dx, 0, n - 1), zz = Math.Clamp(z + dz, 0, n - 1);
                sum += t.GridHeight(xx, zz); c++;
            }
        return Math.Clamp((sum / c - h) * 0.45f, 0, 1);
    }
}
