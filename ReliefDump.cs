using Microsoft.Xna.Framework;

namespace Ironfield;

/// `--relief out.png [layout] [pixels]`: the campaign map's land seen from above as shaded relief (docs/pre-demo/WORLD_SCALE.md
/// WS2), so heights, uplands and cliffs can be checked without the 3D map. Headless: the map's own Height, tinted by height
/// (lowland green to rock and snow), lit from the north-west, with roads, cliffs and places drawn over it.
public static class ReliefDump
{
    public static void Run(string path, int layout, int size)
    {
        var t0 = DateTime.UtcNow;
        var m = WorldMap.Generate(7, layout: layout);
        float half = m.GroundHalf, per = 2 * half / size;
        var hgt = new float[size * size];
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++) hgt[y * size + x] = m.Height(-half + (x + 0.5f) * per, -half + (y + 0.5f) * per);
        });
        float top = hgt.Max();
        var px = new Color[size * size];
        var light = Vector3.Normalize(new Vector3(-1, 1.4f, -1));
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                float h = hgt[y * size + x];
                float hx = hgt[y * size + Math.Min(size - 1, x + 1)] - hgt[y * size + Math.Max(0, x - 1)];
                float hz = hgt[Math.Min(size - 1, y + 1) * size + x] - hgt[Math.Max(0, y - 1) * size + x];
                var n = Vector3.Normalize(new Vector3(-hx, 2 * per, -hz));
                float shade = Math.Clamp(0.35f + 0.85f * Vector3.Dot(n, light), 0.2f, 1.25f);
                var p = new Vector2(-half + (x + 0.5f) * per, -half + (y + 0.5f) * per);
                Vector3 c;
                if (h < WorldMap.SeaLevel) c = Vector3.Lerp(new Vector3(0.22f, 0.42f, 0.58f), new Vector3(0.08f, 0.18f, 0.32f), Math.Clamp(-h / WorldMap.SeaDepth, 0, 1));
                else
                {
                    float t = Math.Clamp(h / MathF.Max(30, top), 0, 1);
                    c = t < 0.25f ? Vector3.Lerp(new Vector3(0.36f, 0.55f, 0.28f), new Vector3(0.62f, 0.62f, 0.38f), t / 0.25f)
                      : t < 0.6f ? Vector3.Lerp(new Vector3(0.62f, 0.62f, 0.38f), new Vector3(0.55f, 0.45f, 0.36f), (t - 0.25f) / 0.35f)
                      : Vector3.Lerp(new Vector3(0.55f, 0.45f, 0.36f), new Vector3(0.95f, 0.95f, 0.95f), (t - 0.6f) / 0.4f);
                    c *= shade;
                }
                px[y * size + x] = new Color(c);
            }
        });
        int To(float v) => (int)((v + half) / per);
        void Dot(Vector2 p, int r, Color col)
        {
            int cx = To(p.X), cy = To(p.Y);
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (x >= 0 && y >= 0 && x < size && y < size) px[y * size + x] = col;
        }
        void Line(Vector2 a, Vector2 b, Color col, int r)
        {
            int steps = Math.Max(1, (int)(Vector2.Distance(a, b) / per));
            for (int i = 0; i <= steps; i++) Dot(Vector2.Lerp(a, b, i / (float)steps), r, col);
        }
        foreach (var (a, b) in m.Roads) Line(a, b, new Color(225, 205, 150), 0);
        foreach (var c in m.Cliffs)
            for (int i = 1; i < c.Points.Length; i++) Line(c.Points[i - 1], c.Points[i], new Color(25, 18, 12), 1);
        foreach (var s in m.Settlements)
            Dot(s.Pos, s.Kind == SettlementKind.Town ? 3 : s.Kind == SettlementKind.Castle ? 2 : 1, s.Kind == SettlementKind.Village ? new Color(240, 235, 220) : new Color(160, 30, 30));
        File.WriteAllBytes(path, Png.Encode(px, size, size));
        Console.WriteLine($"relief of layout {layout}: {2 * half:0} units across at {per:0.0} a pixel, highest {top:0.0}, {(DateTime.UtcNow - t0).TotalSeconds:0.0} s -> {path}");
    }
}
