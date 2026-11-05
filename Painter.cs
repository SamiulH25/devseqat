using Microsoft.Xna.Framework;

namespace Ironfield;

/// Surface texture applied when a material is rendered, in tone steps.
public enum Pattern { None, Flat, Cloth, Quilt, Mail, Plate, Wood, Hair }

/// A material: a 5-tone ramp (deep shadow, shadow, base, light, highlight) built from one base colour with
/// hue-shifted ends (cool shadows, warm lights), plus a surface pattern.
public sealed class Mat
{
    public readonly Color[] Ramp;
    public readonly Pattern Pat;

    public Mat(Color c, Pattern p = Pattern.None)
    {
        Pat = p;
        c = new Color(c.R, c.G, c.B); // always opaque (scaling a Color also scales its alpha)
        if (p == Pattern.Flat) { Ramp = new[] { c, c, c, c, c }; return; }
        var b = c.ToVector3();
        Vector3 cool = new(0.1f, 0.08f, 0.22f), warm = new(1f, 0.95f, 0.78f);
        Ramp = new[]
        {
            new Color(Vector3.Lerp(b * 0.45f, cool, 0.25f)),
            new Color(Vector3.Lerp(b * 0.72f, cool, 0.12f)),
            c,
            new Color(Vector3.Min(Vector3.One, Vector3.Lerp(b * 1.16f, warm, 0.12f))),
            new Color(Vector3.Min(Vector3.One, Vector3.Lerp(b * 1.34f, warm, 0.3f))),
        };
    }

    public static Mat Flat(Color c) => new(c, Pattern.Flat);
}

/// A sprite being painted: each pixel holds a material and a light value; Render() turns that into banded,
/// patterned colour with a selective outline (each edge takes a darkened tone of the material it borders).
public sealed class Canvas
{
    public readonly int W, H;
    readonly Mat?[] mat;
    readonly float[] lit;

    public Canvas(int w, int h) { W = w; H = h; mat = new Mat?[w * h]; lit = new float[w * h]; }

    public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
    public bool Has(int x, int y) => In(x, y) && mat[y * W + x] != null;
    public void Set(int x, int y, Mat m, float l = 0.45f) { if (In(x, y)) { mat[y * W + x] = m; lit[y * W + x] = l; } }
    public void Clear(int x, int y) { if (In(x, y)) mat[y * W + x] = null; }
    public void Shade(int x, int y, float d) { if (Has(x, y)) lit[y * W + x] += d; }

    /// Paint every pixel whose centre is inside, lit by the surface normal there.
    public void Fill(Func<float, float, bool> inside, Func<float, float, Vector3> normal, Mat m, float bias = 0)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                if (!inside(px, py)) continue;
                Set(x, y, m, Vector3.Dot(Vector3.Normalize(normal(px, py)), Style.SpriteLight) + bias);
            }
    }

    static Vector3 Dome(float nx, float ny) => new(nx, ny, MathF.Sqrt(MathF.Max(0.05f, 1 - nx * nx - ny * ny)));

    /// A lit ellipse (a sphere seen from the front).
    public void Ellipse(float cx, float cy, float rx, float ry, Mat m, float bias = 0) =>
        Fill((x, y) => Sq((x - cx) / rx) + Sq((y - cy) / ry) <= 1, (x, y) => Dome((x - cx) / rx * 0.9f, (y - cy) / ry * 0.9f), m, bias);

    /// Rows y0..y1, each spanning span(y): shaded like a cylinder across, slightly darker toward the bottom.
    public void Body(float y0, float y1, Func<float, (float L, float R)> span, Mat m, float bias = 0)
    {
        Fill((x, y) => { if (y < y0 || y > y1) return false; var (l, r) = span(y); return x >= l && x <= r; },
            (x, y) =>
            {
                var (l, r) = span(y);
                float u = r > l ? (x - l) / (r - l) * 2 - 1 : 0;
                return Dome(u * 0.85f, ((y - y0) / MathF.Max(1, y1 - y0) * 2 - 1) * 0.3f);
            }, m, bias);
    }

    public void Rect(float x0, float y0, float x1, float y1, Mat m, float bias = 0) => Body(y0, y1, _ => (x0, x1), m, bias);

    /// Columns x0..x1, each spanning span(x) vertically: shaded like a horizontal cylinder (horse bodies, necks).
    public void Columns(float x0, float x1, Func<float, (float T, float B)> span, Mat m, float bias = 0)
    {
        Fill((x, y) => { if (x < x0 || x > x1) return false; var (t, b) = span(x); return y >= t && y <= b; },
            (x, y) =>
            {
                var (t, b) = span(x);
                float v = b > t ? (y - t) / (b - t) * 2 - 1 : 0;
                float u = (x - x0) / MathF.Max(1, x1 - x0) * 2 - 1;
                return Dome(u * u * u * 0.6f, v * 0.9f);
            }, m, bias);
    }

    /// Paint, with a flat light, every pixel inside.
    public void Area(Func<float, float, bool> inside, Mat m, float l = 0.45f) => Fill(inside, (_, _) => Vector3.UnitZ, m, l - Style.SpriteLight.Z);

    public void Line(float x0, float y0, float x1, float y1, Mat m, float l = 0.45f, float width = 1)
    {
        int n = (int)MathF.Ceiling(MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)) * 2) + 1;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, x = MathHelper.Lerp(x0, x1, t), y = MathHelper.Lerp(y0, y1, t);
            for (int k = 0; k < width; k++) Set((int)(x + k * 0.5f), (int)(y + k * 0.5f), m, l);
        }
    }

    /// Recolour the painted pixels on the edge of the shape (next to empty) with `m`: trims and rims.
    public void Border(Mat m, float l = 0.45f)
    {
        var edge = new List<(int, int)>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (Has(x, y) && (!Has(x - 1, y) || !Has(x + 1, y) || !Has(x, y - 1) || !Has(x, y + 1))) edge.Add((x, y));
        foreach (var (x, y) in edge) Set(x, y, m, lit[y * W + x] * 0.5f + l * 0.5f);
    }

    static float Sq(float v) => v * v;
    static readonly (int X, int Y)[] Around = { (-1, 0), (1, 0), (0, -1), (0, 1) };

    static int Tone(float l) => Math.Clamp((int)MathF.Round(2 + (l - 0.45f) * 4), 0, 4);

    static int Patterned(Pattern p, int tone, int x, int y) => p switch
    {
        Pattern.Flat => 2,
        Pattern.Mail => (x + y) % 2 == 0 ? tone - 1 : tone,
        Pattern.Quilt => (x + y) % 4 == 0 || (x - y + 400) % 4 == 0 ? tone - 1 : tone,
        Pattern.Plate => Math.Clamp((int)MathF.Round((tone - 2) * 1.5f + 2), 0, 4),
        Pattern.Wood => y % 5 == 0 && x % 2 == 0 ? tone - 1 : tone,
        Pattern.Cloth => x % 6 == 3 && y % 3 != 0 ? tone - 1 : tone,
        Pattern.Hair => (x + 2 * y) % 3 == 0 ? tone - 1 : tone,
        _ => tone,
    };

    /// The finished sprite with a 1 px outline border: (W + 2) x (H + 2).
    public Color[] Render()
    {
        int ow = W + 2, oh = H + 2;
        var px = new Color[ow * oh];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (mat[y * W + x] is Mat m)
                    px[(y + 1) * ow + x + 1] = m.Ramp[Math.Clamp(Patterned(m.Pat, Tone(lit[y * W + x]), x, y), 0, 4)];
        // Selective outline: empty pixels next to paint take the darkest tone of what they touch, darkened.
        for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                if (px[y * ow + x].A != 0) continue;
                Mat? best = null;
                foreach (var (dx, dy) in Around)
                {
                    int sx = x - 1 + dx, sy = y - 1 + dy;
                    if (Has(sx, sy)) { var m = mat[sy * W + sx]!; if (best == null || m.Ramp[0].ToVector3().Length() < best.Ramp[0].ToVector3().Length()) best = m; }
                }
                if (best != null) px[y * ow + x] = new Color(best.Ramp[0].ToVector3() * 0.55f);
            }
        return px;
    }
}
