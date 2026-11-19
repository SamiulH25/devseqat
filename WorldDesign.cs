using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// The designed world (`Data/world.json`, docs/world/README.md): the map the owner and the model draw in the world page,
/// which layout 5 builds the campaign map from (docs/WORLDMAP.md). Headless: plain data and the page's province rule.
public sealed class WorldDesign
{
    public const string FileName = "Data/world.json";

    public sealed class Realm { public int Id; public string Name = "", Culture = "", Colour = ""; }
    public sealed class Province { public int Id, Realm = -1; public string Name = "", Kind = ""; public float X, Y; public string? Subculture; }
    public sealed class Settlement { public int Id, Realm = -1, Rank; public string Name = "", Kind = "", Culture = ""; public float X, Y; public bool Port; public string? Subculture; }
    public sealed class Site { public int Id; public string Name = "", Kind = ""; public float X, Y; }
    public sealed class Line { public int Id; public float Width; public bool Minor; public float[][] Points = Array.Empty<float[]>(); }
    public sealed class Lake { public int Id; public float X, Y, Radius; }
    public sealed class Subculture { public string Id = "", Parent = "", Name = "", Colour = "", Flavour = ""; }

    public int Version, Revision;
    public string Title = "";
    public float Extent;
    public string[] Kinds = Array.Empty<string>();
    public Realm[] Realms = Array.Empty<Realm>();
    public Province[] Provinces = Array.Empty<Province>();
    public Settlement[] Settlements = Array.Empty<Settlement>();
    public Site[] Sites = Array.Empty<Site>();
    public Line[] Rivers = Array.Empty<Line>(), Ridges = Array.Empty<Line>(), Roads = Array.Empty<Line>(), Sealanes = Array.Empty<Line>();
    public Lake[] Lakes = Array.Empty<Lake>();
    public Subculture[] Subcultures = Array.Empty<Subculture>();

    static WorldDesign? loaded;
    /// The file, read once.
    public static WorldDesign Get() => loaded ??= Load(Path.Combine(AppContext.BaseDirectory, FileName));

    public static WorldDesign Load(string path)
    {
        var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<WorldDesign>(File.ReadAllText(path), opts) ?? throw new InvalidDataException(path + ": empty");
    }

    public static Vector2 At(float[] p) => new(p[0], p[1]);
    public bool IsSea(int province) => Provinces[province].Kind == "Sea";

    /// Which province holds a point: the nearest centre to the point after the page's warp (index.html `provinceAt`).
    /// Returns an index into `Provinces`.
    public int ProvinceAt(Vector2 p)
    {
        var (wx, wy) = Warp(p.X, p.Y);
        int best = -1;
        double bd = double.MaxValue;
        for (int i = 0; i < Provinces.Length; i++)
        {
            double dx = Provinces[i].X - wx, dy = Provinces[i].Y - wy, d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // ---------- the page's noise, ported exactly (JavaScript number semantics) ----------

    /// JavaScript's ToUint32 of an integer-valued double (|d| < 2^63).
    static uint U32(double d) => unchecked((uint)(long)d);

    /// index.html `hash2`: a value in 0..1 for a lattice point.
    static double Hash2(double ix, double iy)
    {
        double h = (double)unchecked((int)ix * 374761393) + unchecked((int)iy * 668265263); // two Math.imul, added as doubles
        uint hu = U32(h);
        int x = (int)hu ^ (int)(hu >> 13);
        double p = x * 1274126177.0; // a double product, rounded as JavaScript rounds it
        uint pu = U32(p);
        return (uint)((int)pu ^ (int)(pu >> 16)) / 4294967295.0;
    }

    /// index.html `vnoise`: smooth value noise, -0.5..0.5.
    static double Noise(double x, double y)
    {
        double ix = Math.Floor(x), iy = Math.Floor(y), fx = x - ix, fy = y - iy, ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
        double a = Hash2(ix, iy), b = Hash2(ix + 1, iy), c = Hash2(ix, iy + 1), d = Hash2(ix + 1, iy + 1);
        return (a * (1 - ux) + b * ux) * (1 - uy) + (c * (1 - ux) + d * ux) * uy - .5;
    }

    /// index.html `warp`: where a point is looked up, so province borders wander like coasts.
    public static (double X, double Y) Warp(double x, double y) =>
        (x + Noise(x / 65, y / 65) * 24 + Noise(x / 24 + 17, y / 24 - 9) * 9 + Noise(x / 9 - 3, y / 9 + 5) * 3.5,
         y + Noise(x / 65 + 31, y / 65 + 13) * 24 + Noise(x / 24 - 7, y / 24 + 23) * 9 + Noise(x / 9 + 11, y / 9 - 13) * 3.5);

    // ---------- checks ----------

    /// Every fault in the file that would make a bad map, as plain lines (`tools/world.py --check` finds the same and more).
    public IEnumerable<string> Validate()
    {
        if (Version != 2) yield return $"version {Version}: the game reads version 2";
        if (Extent <= 0) yield return "no extent";
        var realms = Realms.Select(r => r.Id).ToHashSet();
        for (int i = 0; i < Provinces.Length; i++)
        {
            var p = Provinces[i];
            if (p.Id != i) yield return $"province {p.Name}: id {p.Id} is not its place in the list ({i})";
            if (!Kinds.Contains(p.Kind)) yield return $"province {p.Name}: unknown land {p.Kind}";
            if (p.Kind == "Sea" ? p.Realm != -1 : !realms.Contains(p.Realm)) yield return $"province {p.Name}: realm {p.Realm}";
        }
        foreach (var s in Settlements)
        {
            if (s.Kind is not ("town" or "castle" or "village")) yield return $"settlement {s.Name}: kind {s.Kind}";
            if (!realms.Contains(s.Realm)) yield return $"settlement {s.Name}: realm {s.Realm}";
            if (IsSea(ProvinceAt(new Vector2(s.X, s.Y)))) yield return $"settlement {s.Name}: in the sea";
            if (s.Subculture != null && Subcultures.All(c => c.Id != s.Subculture)) yield return $"settlement {s.Name}: unknown subculture {s.Subculture}";
        }
        foreach (var l in Rivers.Concat(Ridges).Concat(Roads).Concat(Sealanes))
            if (l.Points.Length < 2 || l.Points.Any(q => q.Length != 2)) yield return $"line {l.Id}: needs two or more [x, y] points";
    }
}
