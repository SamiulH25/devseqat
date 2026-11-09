using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ironfield;

/// One of the captain's forms from Data/forms.json (docs/KNIGHT_ORDERS.md 5.6): a technique the player presses, paid in
/// aura. Used by BattleForms.cs; learned in Campaign/Aura.cs and Campaign/Orders.cs.
public sealed class FormDef
{
    public string Id = "", Name = "", Icon = "", Line = "", Kind = "", Order = "", Effect = "";
    public int Tier = 1, MaxHit = 1, Count;
    public bool Great;
    public float Aura, Cooldown = 20, Warn, Range, Radius, Duration, Damage, Shock, Dps;
    /// Black Flame forms: corruption each use adds (docs/KNIGHT_ORDERS.md 7).
    public float Corruption;
}

/// The form registry, loaded once from Data/forms.json.
public static class Forms
{
    public const string FileName = "Data/forms.json";
    static Dictionary<string, FormDef>? all;

    public static IReadOnlyCollection<FormDef> All { get { Load(); return all!.Values; } }
    public static FormDef Get(string id) { Load(); return all!.TryGetValue(id, out var f) ? f : throw new KeyNotFoundException("no form " + id); }
    public static bool Has(string id) { Load(); return all!.ContainsKey(id); }
    /// The forms anyone may learn by tier (no order), in tier order.
    public static IEnumerable<FormDef> Generic => All.Where(f => f.Order == "").OrderBy(f => f.Tier);
    /// An order's forms, in tier order.
    public static IEnumerable<FormDef> OfOrder(string order) => All.Where(f => f.Order == order).OrderBy(f => f.Tier);

    public static readonly string[] Kinds = { "focus", "slash", "guard", "edge", "shout", "leap", "shot", "counter", "buff", "burst", "volley", "burn", "drain" };

    sealed class FileDto { public List<FormDef> Forms = new(); }

    static void Load()
    {
        if (all != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("form data not found", FileName);
        var opts = new JsonSerializerOptions
        {
            IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        var file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!;
        all = file.Forms.ToDictionary(f => f.Id);
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("forms.json: " + string.Join("; ", problems));
    }

    /// Data checks: kinds known, effects exist, costs and tiers in range, ranged attacks warn (as spells do).
    public static IEnumerable<string> Validate()
    {
        Load();
        foreach (var f in all!.Values)
        {
            if (f.Name == "" || f.Line == "" || f.Icon == "") yield return $"{f.Id}: needs a name, a line and an icon";
            if (!Kinds.Contains(f.Kind)) yield return $"{f.Id}: unknown kind {f.Kind}";
            if (f.Effect != "" && !Effects.Has(f.Effect)) yield return $"{f.Id}: unknown effect {f.Effect}";
            if (f.Kind is "buff" or "burn" && f.Effect == "") yield return $"{f.Id}: a {f.Kind} needs an effect";
            if (f.Kind is "slash" or "volley" && f.Warn <= 0) yield return $"{f.Id}: a thrown attack must warn first";
            if (f.Kind is "slash" or "burst" or "volley" or "shot" or "leap" or "drain" && f.Damage <= 0) yield return $"{f.Id}: needs damage";
            if (f.Tier is < 1 or > 5 || f.Aura is <= 0 or > 80 || f.Cooldown < 5) yield return $"{f.Id}: tier, aura or cooldown out of range";
            if (f.Great && f.Tier < 3) yield return $"{f.Id}: a great form needs tier 3 or more";
            if (f.Corruption > 0 != (f.Order == "hollow")) yield return $"{f.Id}: only Black Flame forms feed corruption";
        }
    }
}
