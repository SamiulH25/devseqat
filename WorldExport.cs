using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// `--export-world out.json [seed]`: a new campaign's map as the world model (docs/world/README.md): realms, provinces,
/// settlements, rivers, ridges, lakes, roads and sites, in world units (x east, y south, the middle at 0,0). The world
/// page (docs/world/index.html) draws it from above. Headless; the game does not read the file back yet.
public static class WorldExport
{
    public static void Run(string path, int seed)
    {
        var c = Campaign.New(seed);
        var m = c.Map;
        var sb = new StringBuilder();
        string N(float v) => MathF.Round(v, 1).ToString(CultureInfo.InvariantCulture);
        string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        string Pts(IEnumerable<Vector2> p) => "[" + string.Join(",", p.Select(v => $"[{N(v.X)},{N(v.Y)}]")) + "]";
        string Hex(Color k) => $"\"#{k.R:x2}{k.G:x2}{k.B:x2}\"";
        void List<T>(string name, IEnumerable<T> items, Func<T, string> row, bool last = false)
        {
            sb.Append($"  {Q(name)}: [\n");
            sb.Append(string.Join(",\n", items.Select(i => "    " + row(i))));
            sb.Append(last ? "\n  ]\n" : "\n  ],\n");
        }
        sb.Append("{\n");
        sb.Append($"  \"version\": 2,\n  \"title\": \"Ironfield: the world\",\n  \"revision\": 1,\n  \"updated\": {Q(DateTime.Today.ToString("yyyy-MM-dd"))},\n");
        sb.Append($"  \"source\": {Q($"the game's map for seed {seed} (layout {m.Layout})")},\n  \"extent\": {N(m.ExtentUsed)},\n");
        sb.Append("  \"kinds\": [" + string.Join(", ", WorldMap.Kinds.Select(Q)) + "],\n");
        List("realms", Enumerable.Range(0, c.S.Factions.Count), f =>
            $"{{\"id\": {f}, \"name\": {Q(c.FactionNames[f])}, \"culture\": {Q(Factions.Get(f).Culture)}, \"colour\": {Hex(Style.Sides[Style.PaletteOf(f)].Cloth)}}}");
        List("provinces", c.S.Provinces, p =>
            $"{{\"id\": {p.Id}, \"name\": {Q(p.Name)}, \"kind\": {Q(p.Kind)}, \"realm\": {p.Faction}, \"x\": {N(p.Center.X)}, \"y\": {N(p.Center.Y)}}}");
        List("settlements", c.S.Settlements, s =>
            $"{{\"id\": {s.Id}, \"name\": {Q(s.Name)}, \"kind\": {Q(s.Kind.ToString().ToLowerInvariant())}, \"realm\": {s.Faction}, \"culture\": {Q(s.Culture)}, \"x\": {N(s.Pos.X)}, \"y\": {N(s.Pos.Y)}}}");
        List("sites", m.Sites, s => $"{{\"id\": {s.Id}, \"name\": {Q(s.Name)}, \"kind\": {Q(s.Kind)}, \"x\": {N(s.Pos.X)}, \"y\": {N(s.Pos.Y)}}}");
        List("rivers", m.Rivers.Select((r, i) => (r, i)), t => $"{{\"id\": {t.i}, \"width\": {N(t.r.Width)}, \"points\": {Pts(t.r.Path)}}}");
        List("ridges", m.Ridges.Select((r, i) => (r, i)), t => $"{{\"id\": {t.i}, \"width\": {N(t.r.Width)}, \"points\": {Pts(t.r.Path)}}}");
        List("lakes", m.Lakes.Select((l, i) => (l, i)), t => $"{{\"id\": {t.i}, \"x\": {N(t.l.Center.X)}, \"y\": {N(t.l.Center.Y)}, \"radius\": {N(t.l.Radius)}}}");
        List("roads", m.RoadLines.Select((l, i) => (l, i)), t => $"{{\"id\": {t.i}, \"minor\": {(t.l.Village ? "true" : "false")}, \"points\": {Pts(t.l.Points)}}}");
        sb.Append("  \"notes\": []\n}\n");
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"world -> {path}: {c.S.Factions.Count} realms, {c.S.Provinces.Count} provinces, {c.S.Settlements.Count} settlements, {m.Rivers.Count} rivers, {m.Lakes.Count} lakes, {m.RoadLines.Count} roads, {m.Sites.Count} sites");
    }
}
