using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ironfield;

/// One engine of war from Data/engines.json (docs/SIEGECRAFT.md A1): what it throws, how far, how hard and how many work it.
/// Built and fired by BattleEngines.cs.
public sealed class EngineDef
{
    public string Id = "", Name = "", Line = "";
    /// "bolt": a flat shot that runs through up to Pierce men of the foe; "stone": a lobbed shot that lands among everyone
    /// within Splash, its own side too; "ram": a roofed ram its crew rolls to the gate (Damage a second off it, the crew
    /// sheltered from arrows); "tower": a siege tower its crew rolls to the wall's face, where it becomes a way up.
    public string Shot = "";
    public float Range, MinRange, Damage, Splash, ArmourPierce, Reload, Hp, Speed, Spread, Shock, WallDamage;
    /// Rolling engines (ram, tower): metres a second with a full crew.
    public float Move;
    /// Crowns it costs; hours a siege camp takes to build it (a siege work, docs/SIEGECRAFT.md A3); whether it is a light engine
    /// a company buys in a town and takes to the field (its train, A4).
    public float Cost, Build;
    public bool Train;
    public bool Rolls => Shot is "ram" or "tower";
    public int Pierce = 1, Crew;
}

/// The engine registry, loaded once from Data/engines.json.
public static class Engines
{
    public const string FileName = "Data/engines.json";
    static Dictionary<string, EngineDef>? all;

    public static IReadOnlyCollection<EngineDef> All { get { Load(); return all!.Values; } }
    public static EngineDef Get(string id) { Load(); return all!.TryGetValue(id, out var e) ? e : throw new KeyNotFoundException("no engine " + id); }
    public static bool Has(string id) { Load(); return all!.ContainsKey(id); }

    sealed class FileDto { public List<EngineDef> Engines = new(); }

    static void Load()
    {
        if (all != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("engine data not found", FileName);
        var opts = new JsonSerializerOptions
        {
            IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        var file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!;
        all = file.Engines.ToDictionary(e => e.Id);
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("engines.json: " + string.Join("; ", problems));
    }

    /// Data checks: a known shot, sane ranges, a crew, and what each shot needs (a bolt pierces, a stone splashes).
    public static IEnumerable<string> Validate()
    {
        Load();
        foreach (var e in all!.Values)
        {
            if (e.Name == "" || e.Line == "") yield return $"{e.Id}: needs a name and a line";
            if (e.Shot is not ("bolt" or "stone" or "ram" or "tower")) yield return $"{e.Id}: unknown shot {e.Shot}";
            if (e.Hp <= 0 || e.Crew < 1) yield return $"{e.Id}: hp and crew must be positive";
            if (e.Rolls && (e.Move <= 0 || e.Move > 3)) yield return $"{e.Id}: a rolling engine moves at walking pace or less";
            if (e.Shot == "ram" && e.Damage <= 0) yield return $"{e.Id}: a ram needs damage";
            if (e.Train == e.Build > 0 || e.Cost <= 0) yield return $"{e.Id}: either a train engine or a siege work, with a cost";
            if (e.Train && e.Shot != "bolt") yield return $"{e.Id}: only bolt-throwers march with a company";
            if (e.Rolls) continue;
            if (e.Range <= 0 || e.MinRange < 0 || e.MinRange >= e.Range) yield return $"{e.Id}: range out of order";
            if (e.Damage <= 0 || e.Reload <= 0 || e.Speed <= 0) yield return $"{e.Id}: damage, reload and speed must be positive";
            if (e.ArmourPierce is < 0 or > 1 || e.Spread < 0 || e.Shock < 0 || e.WallDamage < 0) yield return $"{e.Id}: pierce, spread, shock or wall damage out of range";
            if (e.Shot == "bolt" && e.Pierce < 1) yield return $"{e.Id}: a bolt strikes one man or more";
            if (e.Shot == "stone" && e.Splash <= 0) yield return $"{e.Id}: a stone needs a splash";
        }
    }
}
