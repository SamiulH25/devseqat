using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// What a piece of battlefield is underfoot (docs/TERRAIN.md 3). The sim reads it through `Terrain.GroundAt`.
public enum GroundKind : byte { Grass, Road, Crops, Scrub, Stony, Mud, Wood, DenseWood, Ford, Water, Bridge, Street, Sand }

/// How one kind of ground plays, from Data/ground.json. Speeds, fatigue, charge and reach are multipliers (1 = no change);
/// order is how well a formation keeps its shape (1 = parade ground); cover and stumble are 0..1.
public sealed class GroundDef
{
    public string Id = "", Name = "", Text = "";
    public float FootSpeed = 1, HorseSpeed = 1, Fatigue = 1, Order = 1, Charge = 1, Reach = 1, Cover, Stumble;
    public GroundKind Kind => Enum.Parse<GroundKind>(Id);
    public override string ToString() => Name;
}

/// The ground table: one entry per `GroundKind`.
public static class Grounds
{
    public const string FileName = "Data/ground.json";
    static GroundDef[]? table;

    public static GroundDef Get(GroundKind k) { Load(); return table![(int)k]; }
    public static IReadOnlyList<GroundDef> All { get { Load(); return table!; } }

    sealed class FileDto { public List<GroundDef> Ground = new(); }
    static List<GroundDef> raw = new();

    static void Load()
    {
        if (table != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("ground data not found", FileName);
        var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        raw = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!.Ground;
        var problems = Check(raw).ToList();
        if (problems.Count > 0) throw new InvalidDataException("ground.json: " + string.Join("; ", problems));
        table = new GroundDef[Enum.GetValues<GroundKind>().Length];
        foreach (var g in raw) table[(int)g.Kind] = g;
    }

    /// Data checks: every kind once, known names, values in range.
    public static IEnumerable<string> Validate() { Load(); return Check(raw); }

    static IEnumerable<string> Check(List<GroundDef> list)
    {
        foreach (var k in Enum.GetNames<GroundKind>())
        {
            int n = list.Count(g => g.Id == k);
            if (n != 1) yield return $"{k}: {n} entries (want one)";
        }
        foreach (var g in list)
        {
            if (!Enum.TryParse<GroundKind>(g.Id, out _)) { yield return $"{g.Id}: unknown ground kind"; continue; }
            if (g.Name.Length == 0 || g.Text.Length == 0) yield return $"{g.Id}: needs a name and a line of text";
            if (g.FootSpeed is <= 0 or > 1.5f || g.HorseSpeed is <= 0 or > 1.5f) yield return $"{g.Id}: speed out of range";
            if (g.Fatigue is < 0.5f or > 3) yield return $"{g.Id}: fatigue out of range";
            if (g.Order is < 0 or > 1 || g.Charge is < 0 or > 1.2f || g.Reach is < 0 or > 1) yield return $"{g.Id}: order, charge or reach out of range";
            if (g.Cover is < 0 or > 1 || g.Stumble is < 0 or > 1) yield return $"{g.Id}: cover or stumble out of range";
        }
    }
}

/// Terrain: the ground layer, one kind and one cover value per vertex cell (docs/TERRAIN.md 3).
public sealed partial class Terrain
{
    /// Bump when battlefield generation changes, so replays made on older ground refuse to play (Replay.TerrainVersion).
    public const int Version = 1;

    /// Woods placed on this field (centre, radius); the ground layer marks them.
    readonly List<(Vector2 C, float R)> woods = new();
    byte[]? ground, cover;

    int CellOf(Vector2 p)
    {
        int x = Math.Clamp((int)MathF.Round((p.X + Half) / Step), 0, N - 1), z = Math.Clamp((int)MathF.Round((p.Y + Half) / Step), 0, N - 1);
        return z * N + x;
    }

    /// The ground at p (Grass on flat test fields and the campaign map).
    public GroundKind GroundAt(Vector2 p) => ground == null ? GroundKind.Grass : (GroundKind)ground[CellOf(p)];
    /// How much the ground at p hides men and stops arrows, 0..1.
    public float CoverAt(Vector2 p) => cover == null ? 0 : cover[CellOf(p)] / 255f;
    /// The rules of the ground at p.
    public GroundDef GroundDefAt(Vector2 p) => Grounds.Get(GroundAt(p));

    /// Fill the ground layer from what is on the field: water and fords first, then the road, then woods; grass elsewhere.
    void PaintGround()
    {
        ground = new byte[N * N];
        cover = new byte[N * N];
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2(-Half + x * Step, -Half + z * Step);
                float water = WaterDepth(p);
                var k = water > 0 ? (water < 0.5f ? GroundKind.Ford : GroundKind.Water)
                    : RoadDist(p) < 2.5f ? GroundKind.Road
                    : InWood(p) ? GroundKind.Wood
                    : GroundKind.Grass;
                ground[z * N + x] = (byte)k;
                cover[z * N + x] = (byte)MathF.Round(Grounds.Get(k).Cover * 255);
            }
    }

    bool InWood(Vector2 p)
    {
        foreach (var (c, r) in woods) if (Vector2.DistanceSquared(p, c) < r * r) return true;
        return false;
    }

    /// A short fingerprint of the ground layer (for the determinism test).
    public int GroundHash()
    {
        if (ground == null) return 0;
        uint h = 2166136261; // FNV-1a: the same in every run (HashCode is not)
        foreach (var b in ground) h = (h ^ b) * 16777619;
        foreach (var b in cover!) h = (h ^ b) * 16777619;
        return unchecked((int)h);
    }
}
