using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ironfield;

/// One of the captain's forms from Data/forms.json (docs/archive/KNIGHT_ORDERS.md 5.6): a technique the player presses, paid in
/// aura. Used by BattleForms.cs; learned in Campaign/Aura.cs and Campaign/Orders.cs.
public sealed class FormDef
{
    public string Id = "", Name = "", Icon = "", Line = "", Kind = "", Order = "", Effect = "";
    /// A path's form (docs/ARCANA.md AR6: "banner", "hearth"; "" = none), and a hearth form's act ("lend", "anchor", "kindle").
    public string Path = "", Act = "";
    public int Tier = 1, MaxHit = 1, Count;
    public bool Great;
    public float Aura, Cooldown = 20, Warn, Range, Radius, Duration, Damage, Shock, Dps;
    /// Black Flame forms: corruption each use adds (docs/archive/KNIGHT_ORDERS.md 7).
    public float Corruption;
    public FormDef Clone() => (FormDef)MemberwiseClone();
}

/// An aspect a form of rank 3 can take (docs/archive/MAGIC_AURA.md 4.5): one of two per kind, chosen once. Each number multiplies
/// the form's own (1 = unchanged); MaxHit and Count add.
public sealed class AspectDef
{
    public string Id = "", Kind = "", Name = "", Line = "";
    public float Radius = 1, Range = 1, Duration = 1, Damage = 1, Shock = 1, Cooldown = 1, Aura = 1;
    public int MaxHit, Count;

    /// The form with this aspect: a copy with its numbers changed.
    public FormDef On(FormDef f)
    {
        var g = f.Clone();
        g.Radius *= Radius; g.Range *= Range; g.Duration *= Duration; g.Damage *= Damage; g.Shock *= Shock;
        g.Cooldown *= Cooldown; g.Aura *= Aura; g.MaxHit += MaxHit; g.Count += Count;
        return g;
    }
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
    public static IEnumerable<FormDef> Generic => All.Where(f => f.Order == "" && f.Path == "").OrderBy(f => f.Tier);
    /// A path's forms (AR6), in tier order.
    public static IEnumerable<FormDef> OfPath(string path) => All.Where(f => f.Path == path && path != "").OrderBy(f => f.Tier);
    /// The captain's paths (AR6).
    public static readonly string[] Paths = { "sword", "banner", "hearth" };
    /// An order's forms, in tier order.
    public static IEnumerable<FormDef> OfOrder(string order) => All.Where(f => f.Order == order).OrderBy(f => f.Tier);

    /// Your own form (MA6) is made in play, not read from the file: the campaign registers it under this id.
    public const string OwnId = "own";
    public static void Register(FormDef f) { Load(); all![f.Id] = f; }

    static List<AspectDef>? aspects;
    /// Every aspect, and the two a form's kind offers.
    public static IReadOnlyList<AspectDef> Aspects { get { Load(); return aspects!; } }
    public static IEnumerable<AspectDef> AspectsFor(FormDef f) => Aspects.Where(a => a.Kind == f.Kind);
    public static AspectDef? Aspect(string id) => Aspects.FirstOrDefault(a => a.Id == id);

    public static readonly string[] Kinds = { "focus", "slash", "guard", "edge", "shout", "leap", "shot", "counter", "buff", "burst", "volley", "burn", "drain", "hearth" };

    sealed class FileDto { public List<FormDef> Forms = new(); public List<AspectDef> Aspects = new(); }

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
        aspects = file.Aspects;
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
            if (f.Path != "" && (!Paths.Contains(f.Path) || f.Order != "")) yield return $"{f.Id}: path must be sword, banner or hearth, and not an order's";
            if (f.Kind == "hearth" != (f.Act is "lend" or "anchor" or "kindle")) yield return $"{f.Id}: a hearth form needs an act (lend, anchor, kindle), and only it";
        }
        foreach (var k in Kinds)
            if (aspects!.Count(a => a.Kind == k) != 2) yield return $"the {k} kind needs two aspects";
        foreach (var a in aspects!)
        {
            if (a.Name == "" || a.Line == "") yield return $"aspect {a.Id}: needs a name and a line";
            if (aspects.Count(b => b.Id == a.Id) > 1) yield return $"aspect {a.Id}: twice";
        }
    }
}
