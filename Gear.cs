using System.Text.Json;

namespace Ironfield;

/// One piece of the captain's kit from Data/gear.json (docs/PROGRESSION.md P1).
public sealed class GearDef
{
    public string Id = "", Slot = "", Name = "", Look = "", Legs = "cloth", Feet = "boots", Mount = "", Text = "";
    /// The skill `Needs` counts (Arms by default; Riding for great horses, Bowcraft for the war bow).
    public string Skill = "Arms";
    public int Cost, Needs, Rank, Ammo;
    public float Damage, Reach, Windup, Recover, Pierce, Range, Reload = 1, Block, Armor, Speed;
    /// Two hands on the weapon (no shield); a spear that only thrusts.
    public bool TwoHanded, Thrust;
    public bool Ranged => Ammo > 0;
    /// Gear that costs nothing is the captain's from the start.
    public bool Free => Cost == 0;
    public override string ToString() => Name;
}

/// The captain's gear data: weapons, shields, body armour, helms and horses.
public static class Gear
{
    public const string FileName = "Data/gear.json";
    public static readonly string[] Slots = { "weapon", "shield", "body", "helm", "horse" };
    static Dictionary<string, GearDef>? gear;

    public static IReadOnlyCollection<GearDef> All { get { Load(); return gear!.Values; } }
    public static GearDef Get(string id) { Load(); return gear!.TryGetValue(id, out var g) ? g : throw new KeyNotFoundException("no gear " + id); }
    public static GearDef? Find(string id) { Load(); return gear!.GetValueOrDefault(id); }
    public static IEnumerable<GearDef> InSlot(string slot) => All.Where(g => g.Slot == slot);

    sealed class FileDto { public List<GearDef> Gear = new(); }

    static void Load()
    {
        if (gear != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("gear data not found", FileName);
        var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!;
        gear = new Dictionary<string, GearDef>();
        foreach (var g in file.Gear) gear[g.Id] = g;
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("gear.json: " + string.Join("; ", problems));
    }

    /// Data checks: known slots and sprite parts, mounts exist, bows have arrows, and every slot has free starting gear.
    public static IEnumerable<string> Validate()
    {
        Load();
        foreach (var g in gear!.Values)
        {
            if (!Slots.Contains(g.Slot)) { yield return $"{g.Id}: unknown slot {g.Slot}"; continue; }
            if (g.Cost < 0 || g.Needs is < 0 or > 100 || g.Rank is < 0 or > 5) yield return $"{g.Id}: cost, needs or rank out of range";
            if (!Skills.All.Any(s => s.Id == g.Skill)) yield return $"{g.Id}: unknown skill {g.Skill}";
            switch (g.Slot)
            {
                case "weapon":
                    if (!Ironfield.Look.Weapons.Contains(g.Look)) yield return $"{g.Id}: unknown weapon look {g.Look}";
                    if (Ironfield.Look.Ranged(g.Look) != g.Ranged) yield return $"{g.Id}: a bow needs arrows, and only bows have them";
                    if (g.Ranged && (g.Range <= 0 || !g.TwoHanded)) yield return $"{g.Id}: a bow needs range and both hands";
                    if (g.Damage <= 0 || g.Reach <= 0 || g.Windup <= 0 || g.Recover <= 0) yield return $"{g.Id}: a weapon needs damage, reach, windup and recover";
                    break;
                case "shield":
                    if (!Ironfield.Look.Shields.Contains(g.Look)) yield return $"{g.Id}: unknown shield look {g.Look}";
                    if (g.Block is <= 0 or > 1) yield return $"{g.Id}: block chance out of range";
                    break;
                case "body":
                    if (!Ironfield.Look.Bodies.Contains(g.Look) || !Ironfield.Look.LegsKinds.Contains(g.Legs) || !Ironfield.Look.FeetKinds.Contains(g.Feet))
                        yield return $"{g.Id}: unknown body, legs or feet look";
                    break;
                case "helm":
                    if (!Ironfield.Look.Heads.Contains(g.Look)) yield return $"{g.Id}: unknown head look {g.Look}";
                    break;
                case "horse":
                    if (!Troops.HasMount(g.Mount)) yield return $"{g.Id}: unknown mount {g.Mount}";
                    break;
            }
        }
        foreach (var slot in Slots)
            if (!gear.Values.Any(g => g.Slot == slot && g.Free)) yield return $"slot {slot} has no free starting gear";
        foreach (var id in new CaptainKit().Worn)
            if (!gear.ContainsKey(id)) yield return $"the starting kit names unknown gear {id}";
    }
}
