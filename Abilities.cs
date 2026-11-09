using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ironfield;

/// One spell or ability from Data/abilities.json (docs/COMPANIONS_MAGIC.md B3, C3, E3): what it does, when its caster
/// uses it (`When`, the AI rule; casters never wait for an order) and what it costs. Cast by BattleAbilities.cs.
public sealed class AbilityDef
{
    public string Id = "", Name = "", Icon = "", Line = "";
    /// "faith", "craft" (adds Omen), "arms" (a companion's, no Will) or "necromancy".
    public string Tradition = "";
    /// buff (own companies in Radius), debuff (enemy companies), field (a patch on the ground: `On` foes/own/all),
    /// attack (damage to the foes in Radius, at most MaxHit), bolt (one man: the tallest in Radius), heal, rally, officer.
    public string Kind = "";
    /// The effect it lays (Data/effects.json) and on whom a field lays it.
    public string Effect = "", On = "foes";
    /// The rule that picks a target (BattleAbilities.Target): closing, fighting, wavering, hurt, archers, massed, foes,
    /// shot, horse, undead, officer, self, routing, losing.
    public string When = "";
    /// "dry": no rain and burnable ground; "rain": rain, or a storm laid on the spot.
    public string Needs = "";
    public float Range = 40, Radius = 10, Duration, Cooldown = 30, Will, Warn = 1;
    /// Attack and bolt damage, heal per man, burn per second of a field, seconds of burning ground an attack leaves, morale an attack takes.
    public float Damage, Heal, Dps, Burn, Shock;
    /// Damage multiplier on men of this breed ("undead"); a field with a Tag burns only them.
    public string Tag = "";
    public float VsTag = 1;
    public int MaxHit = 8;
    /// A great working: once a battle, tier VI only, most of the pool.
    public bool Great;
    /// Omen a cast adds on the map (craft workings, docs/COMPANIONS_MAGIC.md C2).
    public float Omen;
    /// The cast only moves the caster's own company (Hold the line, Wheel).
    public bool Self;
}

/// The ability registry, loaded once from Data/abilities.json.
public static class Abilities
{
    public const string FileName = "Data/abilities.json";
    static Dictionary<string, AbilityDef>? all;

    public static IReadOnlyCollection<AbilityDef> All { get { Load(); return all!.Values; } }
    public static AbilityDef Get(string id) { Load(); return all!.TryGetValue(id, out var a) ? a : throw new KeyNotFoundException("no ability " + id); }
    public static bool Has(string id) { Load(); return all!.ContainsKey(id); }

    public static readonly string[] Kinds = { "buff", "debuff", "field", "attack", "bolt", "heal", "rally", "officer" };
    public static readonly string[] Rules = { "closing", "fighting", "wavering", "hurt", "archers", "massed", "foes", "shot", "horse", "undead", "officer", "self", "routing", "losing" };

    sealed class FileDto { public List<AbilityDef> Abilities = new(); }

    static void Load()
    {
        if (all != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("ability data not found", FileName);
        var opts = new JsonSerializerOptions
        {
            IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        var file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!;
        all = file.Abilities.ToDictionary(a => a.Id);
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("abilities.json: " + string.Join("; ", problems));
    }

    /// Data checks: kinds and rules known, effects exist, attacks warn first (E4: a company that moves off the glow takes nothing).
    public static IEnumerable<string> Validate()
    {
        Load();
        foreach (var a in all!.Values)
        {
            if (a.Name == "" || a.Line == "" || a.Icon == "") yield return $"{a.Id}: needs a name, a line and an icon";
            if (!Kinds.Contains(a.Kind)) yield return $"{a.Id}: unknown kind {a.Kind}";
            if (!Rules.Contains(a.When)) yield return $"{a.Id}: unknown rule {a.When}";
            if (a.Tradition is not ("faith" or "craft" or "arms" or "necromancy")) yield return $"{a.Id}: unknown tradition {a.Tradition}";
            if (a.Effect != "" && !Effects.Has(a.Effect)) yield return $"{a.Id}: unknown effect {a.Effect}";
            if (a.Kind is "buff" or "debuff" or "field" && a.Effect == "" && a.Dps <= 0) yield return $"{a.Id}: a {a.Kind} needs an effect";
            if (a.Kind is "attack" or "bolt" && (a.Warn < 1 || a.Damage <= 0)) yield return $"{a.Id}: an attack needs damage and a warning of a second or more";
            if (a.On is not ("foes" or "own" or "all")) yield return $"{a.Id}: unknown target {a.On}";
            if (a.Needs is not ("" or "dry" or "rain")) yield return $"{a.Id}: unknown need {a.Needs}";
            if (a.Will is < 0 or > 80 || a.Cooldown < 5 || a.Radius <= 0) yield return $"{a.Id}: will, cooldown or radius out of range";
            if (a.Great && a.Tradition == "arms") yield return $"{a.Id}: a companion has no great working";
        }
    }
}
