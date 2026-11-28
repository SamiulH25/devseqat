using System.Text.Json;

namespace Ironfield;

/// One piece of kit from Data/gear.json: the captain's (docs/archive/PROGRESSION.md P1) and every part a soldier wears
/// (docs/pre-demo/KIT.md KT1; `Troop` pieces are not sold to the captain).
public sealed class GearDef
{
    public string Id = "", Slot = "", Name = "", Look = "", Legs = "cloth", Feet = "boots", Mount = "", Text = "";
    /// The skill `Needs` counts (Arms by default; Riding for great horses, Bowcraft for the war bow).
    public string Skill = "Arms";
    public int Cost, Needs, Rank, Ammo;
    public float Damage, Reach, Windup, Recover, Pierce, Range, Reload = 1, Block, Armor, Speed, VsMounted = 1;
    /// What a weapon's blow does (`BlowKind`: cut, pierce, blunt) and what a body piece is made of (`CoatKind`); see Armour.cs.
    public string Hits = "", Kind = "";
    /// How well a weapon parries without a shield (the bare block times this): a bow is no blade.
    public float Parry = 1;
    /// A soldier's piece: no armourer sells it to the captain.
    public bool Troop;
    /// Two hands on the weapon (no shield); a spear that only thrusts.
    public bool TwoHanded, Thrust;
    public bool Ranged => Ammo > 0;
    /// Gear that costs nothing is the captain's from the start.
    public bool Free => Cost == 0;
    public override string ToString() => Name;
}

/// The gear data: the captain's weapons, shields, body armour, helms and horses, every soldier's kit piece (KT1) and the
/// fighters' bases (`FighterBase`, read by `Fighter.Build`).
public static class Gear
{
    public const string FileName = "Data/gear.json";
    /// The captain's slots (his kit, the armourer, the baggage).
    public static readonly string[] Slots = { "weapon", "shield", "body", "helm", "horse" };
    /// Slots only soldiers fill: the captain's legs, feet and hands come with his body piece.
    public static readonly string[] TroopSlots = { "legs", "feet", "hands" };
    static Dictionary<string, GearDef>? gear;
    static Dictionary<(string, string), GearDef>? byLook;
    static Dictionary<string, FighterBase>? bases;

    /// The captain's pieces (no `Troop` piece).
    public static IReadOnlyCollection<GearDef> All { get { Load(); return captains!; } }
    /// Every piece, the soldiers' too.
    public static IReadOnlyCollection<GearDef> Every { get { Load(); return gear!.Values; } }
    static List<GearDef>? captains;
    public static GearDef Get(string id) { Load(); return gear!.TryGetValue(id, out var g) ? g : throw new KeyNotFoundException("no gear " + id); }
    public static GearDef? Find(string id) { Load(); return gear!.GetValueOrDefault(id); }
    public static IEnumerable<GearDef> InSlot(string slot) => All.Where(g => g.Slot == slot);
    /// The piece a man wearing sprite part `look` in `slot` has on (body, helm, legs, feet, hands, shield, weapon); null if none.
    public static GearDef? PieceOf(string slot, string look) { Load(); return byLook!.GetValueOrDefault((slot, look)); }
    public static GearDef Piece(string slot, string look) => PieceOf(slot, look) ?? throw new KeyNotFoundException($"no {slot} piece for look {look}");
    public static FighterBase Base(string id) { Load(); return bases!.TryGetValue(id, out var b) ? b : throw new KeyNotFoundException("no fighter base " + id); }
    public static bool HasBase(string id) { Load(); return bases!.ContainsKey(id); }

    sealed class FileDto { public List<GearDef> Gear = new(); public List<FighterBase> Bases = new(); }

    static void Load()
    {
        if (gear != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("gear data not found", FileName);
        var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!;
        gear = new Dictionary<string, GearDef>();
        byLook = new();
        foreach (var g in file.Gear) { gear[g.Id] = g; byLook.TryAdd((g.Slot, g.Look), g); }
        captains = gear.Values.Where(g => !g.Troop).ToList();
        bases = file.Bases.ToDictionary(b => b.Id);
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("gear.json: " + string.Join("; ", problems));
    }

    /// Data checks: known slots and sprite parts, mounts exist, bows have arrows, and every slot has free starting gear.
    public static IEnumerable<string> Validate()
    {
        Load();
        foreach (var g in gear!.Values)
        {
            if (!Slots.Contains(g.Slot) && !TroopSlots.Contains(g.Slot)) { yield return $"{g.Id}: unknown slot {g.Slot}"; continue; }
            if (TroopSlots.Contains(g.Slot) && !g.Troop) yield return $"{g.Id}: {g.Slot} pieces are soldiers' only (troop)";
            if (g.Slot != "horse" && byLook![(g.Slot, g.Look)] != g) yield return $"{g.Id}: another {g.Slot} piece already has look {g.Look}";
            if (g.Armor < 0 || g.VsMounted <= 0) yield return $"{g.Id}: armour or vs horse out of range";
            if (g.Cost < 0 || g.Needs is < 0 or > 100 || g.Rank is < 0 or > 5) yield return $"{g.Id}: cost, needs or rank out of range";
            if (!Skills.All.Any(s => s.Id == g.Skill)) yield return $"{g.Id}: unknown skill {g.Skill}";
            switch (g.Slot)
            {
                case "weapon":
                    if (!Ironfield.Look.Weapons.Contains(g.Look)) yield return $"{g.Id}: unknown weapon look {g.Look}";
                    if (Ironfield.Look.Missile(g.Look) != g.Ranged) yield return $"{g.Id}: a bow, sling or javelins need ammo, and only they have it";
                    if (g.Ranged && g.Range <= 0 || Ironfield.Look.Ranged(g.Look) && !g.TwoHanded) yield return $"{g.Id}: a missile needs range, a bow both hands";
                    if (g.Damage <= 0 || g.Reach <= 0 || g.Windup <= 0 || g.Recover <= 0) yield return $"{g.Id}: a weapon needs damage, reach, windup and recover";
                    if (!Armour.TryBlow(g.Hits, out _)) yield return $"{g.Id}: hits must be cut, pierce or blunt";
                    break;
                case "shield":
                    if (!Ironfield.Look.Shields.Contains(g.Look)) yield return $"{g.Id}: unknown shield look {g.Look}";
                    if (g.Block is <= 0 or > 1) yield return $"{g.Id}: block chance out of range";
                    break;
                case "body":
                    if (!Ironfield.Look.Bodies.Contains(g.Look) || !Ironfield.Look.LegsKinds.Contains(g.Legs) || !Ironfield.Look.FeetKinds.Contains(g.Feet))
                        yield return $"{g.Id}: unknown body, legs or feet look";
                    if (!Armour.TryCoat(g.Kind, out _)) yield return $"{g.Id}: kind must be cloth, padded, leather, mail, scale or plate";
                    break;
                case "helm":
                    if (!Ironfield.Look.Heads.Contains(g.Look)) yield return $"{g.Id}: unknown head look {g.Look}";
                    break;
                case "horse":
                    if (g.Mount != "" && !Troops.HasMount(g.Mount)) yield return $"{g.Id}: unknown mount {g.Mount}";
                    break;
                case "legs": if (!Ironfield.Look.LegsKinds.Contains(g.Look)) yield return $"{g.Id}: unknown legs look {g.Look}"; break;
                case "feet": if (!Ironfield.Look.FeetKinds.Contains(g.Look)) yield return $"{g.Id}: unknown feet look {g.Look}"; break;
                case "hands": if (!Ironfield.Look.HandsKinds.Contains(g.Look)) yield return $"{g.Id}: unknown hands look {g.Look}"; break;
            }
        }
        foreach (var slot in Slots)
            if (!captains!.Any(g => g.Slot == slot && g.Free)) yield return $"slot {slot} has no free starting gear";
        foreach (var id in new[] { "man", "captain" })
            if (!bases!.ContainsKey(id)) yield return $"no fighter base {id}";
        // The captain's suit: his legs, feet and hands come with his body piece, so each must have a piece of its own.
        foreach (var g in captains!.Where(g => g.Slot == "body"))
            if (PieceOf("legs", g.Legs) == null || PieceOf("feet", g.Feet) == null) yield return $"{g.Id}: its legs or feet have no piece";
        foreach (var id in new CaptainKit().Worn)
            if (!gear.ContainsKey(id)) yield return $"the starting kit names unknown gear {id}";
    }
}
