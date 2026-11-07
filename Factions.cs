using System.Text.Json;

namespace Ironfield;

/// A culture (CAMPAIGN_OVERHAUL.md 4.1): what its villages raise, and the lines only its men may enter.
public sealed class CultureDef
{
    public string Id = "", Name = "", Root = "levies", Text = "";
}

/// A realm as Data/factions.json describes it: its name, heraldry palette, culture and the troop each role fields per tier.
public sealed class FactionDef
{
    public string Name = "", Culture = "common";
    public int Palette;
    public Dictionary<string, string[]> Lines = new();
}

/// Realms and cultures, loaded once from Data/factions.json (copied next to the game).
public static class Factions
{
    public const string FileName = "Data/factions.json";
    public const string Common = "common";
    static List<FactionDef>? factions;
    static List<CultureDef>? cultures;

    sealed class FileDto
    {
        public List<CultureDef> Cultures = new();
        public List<FactionDef> Factions = new();
    }

    public static IReadOnlyList<FactionDef> All { get { Load(); return factions!; } }
    public static IReadOnlyList<CultureDef> Cultures { get { Load(); return cultures!; } }
    public static FactionDef Get(int faction)
    {
        Load();
        if (faction >= factions!.Count && bound != null && faction < bound.Count && bound[faction].Founded)
        {
            if (!founded.TryGetValue(faction, out var d))
            {
                var st = bound[faction];
                founded[faction] = d = new FactionDef { Name = st.Name, Culture = st.Culture, Palette = st.Palette, Lines = factions[Math.Clamp(st.Template, 0, factions.Count - 1)].Lines };
            }
            return d;
        }
        return factions[Math.Clamp(faction, 0, factions.Count - 1)];
    }

    // Realms founded in play (F5) live in the save, not in factions.json: the running campaign binds its list here so
    // the static look-ups (lines, culture, heraldry) see them. Only one campaign is live at a time.
    static IReadOnlyList<FactionState>? bound;
    static readonly Dictionary<int, FactionDef> founded = new();

    /// Make a campaign's realms the ones Get answers for (called when a campaign is built or a realm is founded).
    public static void Bind(IReadOnlyList<FactionState> states) { bound = states; founded.Clear(); }
    public static CultureDef? Culture(string id) { Load(); return cultures!.FirstOrDefault(c => c.Id == id); }
    public static string CultureOf(int faction) => faction < 0 ? Common : Get(faction).Culture;

    /// The T1 troop a culture's villages raise ("common" and unknown cultures raise levies).
    public static string Root(string culture) => Culture(culture)?.Root ?? "levies";

    /// The troop a realm's line fields at a tier (1-7).
    public static string Line(int faction, string role, int tier) =>
        Get(Math.Max(0, faction)).Lines.TryGetValue(role, out var l) ? l[Math.Clamp(tier, 1, 7) - 1] : Get(Math.Max(0, faction)).Lines["infantry"][Math.Clamp(tier, 1, 7) - 1];

    static string Path()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var p = System.IO.Path.Combine(dir, FileName);
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("faction data not found", FileName);
    }

    static void Load()
    {
        if (factions != null) return;
        var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(Path()), opts)!;
        cultures = file.Cultures;
        factions = file.Factions;
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("factions.json: " + string.Join("; ", problems));
    }

    static readonly string[] Roles = { "infantry", "archers", "spears", "horse" };

    /// Data checks: cultures and roots exist; every line has seven troops of the realm's culture (or common), each one an
    /// upgrade of the one before it.
    public static IEnumerable<string> Validate()
    {
        if (factions == null) Load();
        if (factions.Count < 3) yield return "the campaign needs three realms";
        foreach (var c in cultures!)
            if (!Troops.All.Any(t => t.Id == c.Root)) yield return $"culture {c.Id}: unknown root {c.Root}";
        foreach (var f in factions)
        {
            if (cultures.All(c => c.Id != f.Culture)) yield return $"{f.Name}: unknown culture {f.Culture}";
            foreach (var role in Roles)
            {
                if (!f.Lines.TryGetValue(role, out var line) || line.Length != 7) { yield return $"{f.Name}: the {role} line needs seven troops"; continue; }
                for (int i = 0; i < 7; i++)
                {
                    if (!Troops.All.Any(t => t.Id == line[i])) { yield return $"{f.Name} {role}: unknown troop {line[i]}"; continue; }
                    var t = Troops.Get(line[i]);
                    if (t.Culture != Common && t.Culture != f.Culture) yield return $"{f.Name} {role}: {t.Id} is {t.Culture}, not {f.Culture}";
                    if (i > 0 && line[i] != line[i - 1] && !Troops.Get(line[i - 1]).UpgradesTo.Contains(line[i])) yield return $"{f.Name} {role}: {line[i - 1]} does not upgrade to {line[i]}";
                }
            }
        }
    }
}
