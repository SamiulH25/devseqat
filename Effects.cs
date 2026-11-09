using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ironfield;

/// One effect from Data/effects.json (docs/COMPANIONS_MAGIC.md Part A): a named bonus or malus that reads the same
/// everywhere. Fractions add (+0.1 = 10 % more); armour and morale a second are flat. A stat with no hook is not here.
public sealed class EffectDef
{
    public string Id = "", Name = "", Icon = "", Line = "";
    public string[] Tags = Array.Empty<string>();
    /// Blows strike harder (melee and arrows).
    public float Damage;
    /// Block chance (a fraction of the man's own); BlockStill only while he stands (locked shields).
    public float Block, BlockStill;
    /// Flat armour, and more against arrows and bolts only.
    public float Armour, ArrowArmour;
    /// Pace on the move; Footing = the share of the ground's and slope's slowing he shrugs off (1 = all).
    public float Speed, Footing;
    /// Stamina spent (-0.3 = 30 % less).
    public float Tire;
    /// Morale he loses to flank hits, routing neighbours, charging horse and fear (-0.5 = half); morale his blows take.
    public float Shock, ShockDealt;
    /// Morale a second, flat.
    public float MoralePerSec;
    /// Arrow scatter and time between shots.
    public float Spread, Reload;
    /// Braced spears reach further and hit the horse harder; a couched lance hits harder.
    public float Brace, Charge;
    /// > 0: an aura of this radius (m) round its bearer, on the companies of his side whose centre is inside it.
    public float Radius;
}

/// A man's effect totals: his troop's passives plus his company's auras and timed effects (BattleEffects.cs).
/// Read in the damage path, morale, movement, fatigue and shooting: one field each, no look-ups.
public struct Mods
{
    public float Damage, Block, BlockStill, Armour, ArrowArmour, Speed, Footing, Tire, Shock, ShockDealt, MoralePerSec, Spread, Reload, Brace, Charge;

    public void Add(EffectDef e, float k = 1)
    {
        Damage += e.Damage * k; Block += e.Block * k; BlockStill += e.BlockStill * k; Armour += e.Armour * k; ArrowArmour += e.ArrowArmour * k;
        Speed += e.Speed * k; Footing += e.Footing * k; Tire += e.Tire * k; Shock += e.Shock * k; ShockDealt += e.ShockDealt * k;
        MoralePerSec += e.MoralePerSec * k; Spread += e.Spread * k; Reload += e.Reload * k; Brace += e.Brace * k; Charge += e.Charge * k;
    }

    public static Mods operator +(Mods a, Mods b) => new()
    {
        Damage = a.Damage + b.Damage, Block = a.Block + b.Block, BlockStill = a.BlockStill + b.BlockStill, Armour = a.Armour + b.Armour,
        ArrowArmour = a.ArrowArmour + b.ArrowArmour, Speed = a.Speed + b.Speed, Footing = a.Footing + b.Footing, Tire = a.Tire + b.Tire,
        Shock = a.Shock + b.Shock, ShockDealt = a.ShockDealt + b.ShockDealt, MoralePerSec = a.MoralePerSec + b.MoralePerSec,
        Spread = a.Spread + b.Spread, Reload = a.Reload + b.Reload, Brace = a.Brace + b.Brace, Charge = a.Charge + b.Charge,
    };
}

/// The effect registry, loaded once from Data/effects.json.
public static class Effects
{
    public const string FileName = "Data/effects.json";
    static Dictionary<string, EffectDef>? effects;

    public static IReadOnlyCollection<EffectDef> All { get { Load(); return effects!.Values; } }
    public static EffectDef Get(string id) { Load(); return effects!.TryGetValue(id, out var e) ? e : throw new KeyNotFoundException("no effect " + id); }
    public static bool Has(string id) { Load(); return effects!.ContainsKey(id); }

    sealed class FileDto { public List<EffectDef> Effects = new(); }

    static void Load()
    {
        if (effects != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("effect data not found", FileName);
        var opts = new JsonSerializerOptions
        {
            IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, // a misspelt stat fails loudly instead of doing nothing
        };
        var file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!;
        effects = new Dictionary<string, EffectDef>();
        foreach (var e in file.Effects) effects[e.Id] = e;
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("effects.json: " + string.Join("; ", problems));
    }

    /// Data checks: every effect has a name, an icon and a tooltip line; numbers stay in sane bounds (that the kit can
    /// draw the icon is checked by SelfTest.EffectsValidate, so this file stays headless).
    public static IEnumerable<string> Validate()
    {
        Load();
        foreach (var e in effects!.Values)
        {
            if (e.Name == "" || e.Line == "") yield return $"{e.Id}: needs a name and a line";
            if (e.Icon == "") yield return $"{e.Id}: needs an icon";
            if (e.Footing is < 0 or > 1) yield return $"{e.Id}: footing {e.Footing} outside 0-1";
            if (e.Shock < -1 || e.Tire < -1 || e.Spread < -1 || e.Reload < -1) yield return $"{e.Id}: a reduction past -1";
            if (e.Radius is < 0 or > 60) yield return $"{e.Id}: radius {e.Radius} out of range";
        }
    }
}
