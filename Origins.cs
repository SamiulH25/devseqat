using System.Text.Json;

namespace Ironfield;

/// A birth, a past or a fighting road from Data/origins.json (the character creator).
public sealed class OriginDef
{
    public string Id = "", Name = "", Icon = "", Text = "", Line = "", Perk = "";
    /// Years this adds to the captain's age (20 + birth + past).
    public int Age;
    public List<string> Effects = new();
}

/// What the player chose in the character creator: who the captain is, where they come from and how they look.
public sealed class CaptainMaking
{
    public string Name = "", Company = "", Birth = "camp", Past = "sellsword", Road = "sword";
    public int Realm, Skin = 1, Hair = 2;
    /// The painted face (CaptainLook numbers) and the charge on your banner (an `Emblem`).
    public int Cut, Beard = 1, Eye, Brow = 1, Nose = 1, Jaw = 1, Scar, Arms = (int)Emblem.Chevron;
    /// The battle head's style from the cut and beard: 0 clean, 1 full beard, 2 moustache, 3 bald and bearded.
    public int Style => Beard == 0 ? 0 : Cut == 4 ? 3 : Beard == 2 ? 2 : 1;
    /// The face as Variety numbers it (skin x hair x style), painted once in the atlas.
    public int Visage => Skin + Variety.Skins.Length * (Hair + Variety.Hairs.Length * Style);
    public CaptainLook Look => new() { Cut = Cut, Beard = Beard, Eye = Eye, Brow = Brow, Nose = Nose, Jaw = Jaw, Scar = Scar };
}

/// The captain's origins: data, checks and the parts of a making that shape the captain alone (headless).
public static class Origins
{
    public const string FileName = "Data/origins.json";
    public const int BaseAge = 20;
    /// Effect verbs: those that shape the captain (`Shape`) and those that shape the new campaign (`Campaign.ApplyMaking`).
    public static readonly string[] CaptainVerbs = { "skill", "gear", "wear" };
    public static readonly string[] WorldVerbs = { "gold", "renown", "food", "horses", "opinion_realm", "men", "free", "buff" };

    sealed class FileDto { public List<OriginDef> Births = new(), Pasts = new(), Roads = new(); }
    static FileDto? data;

    static FileDto Data
    {
        get
        {
            if (data != null) return data;
            string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException("origins data not found", FileName);
            var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            return data = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!;
        }
    }

    public static IReadOnlyList<OriginDef> Births => Data.Births;
    public static IReadOnlyList<OriginDef> Pasts => Data.Pasts;
    public static IReadOnlyList<OriginDef> Roads => Data.Roads;
    public static OriginDef? Find(string id) => Births.Concat(Pasts).Concat(Roads).FirstOrDefault(o => o.Id == id);
    static IEnumerable<OriginDef> Chosen(CaptainMaking m) => new[] { Find(m.Birth), Find(m.Past) }.OfType<OriginDef>();

    /// One effect split into its verb and arguments ("skill:Arms:10" -> skill, [Arms, 10]).
    public static (string Verb, string[] Args) Parse(string fx) { var p = fx.Split(':'); return (p[0], p[1..]); }

    static float Num(string s) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n) ? n : 0;

    /// The sum of one numeric verb over the birth and the past ("gold" -> +350).
    public static float Sum(CaptainMaking m, string verb) =>
        Chosen(m).SelectMany(o => o.Effects).Select(Parse).Where(e => e.Verb == verb && e.Args.Length == 1).Sum(e => Num(e.Args[0]));

    /// Base men (before ArmyScale) the birth and the past bring, by troop.
    public static IEnumerable<(string Troop, int Men)> Men(CaptainMaking m) =>
        Chosen(m).SelectMany(o => o.Effects).Select(Parse).Where(e => e.Verb == "men").Select(e => (e.Args[0], (int)Num(e.Args[1])));

    /// The boons an origin lays on the map when the campaign begins: an effect and its days (0 = the whole campaign).
    public static IEnumerable<(EffectDef Effect, float Days)> Boons(OriginDef o) =>
        o.Effects.Select(Parse).Where(e => e.Verb == "buff" && e.Args.Length == 2 && Effects.Has(e.Args[0])).Select(e => (Effects.Get(e.Args[0]), Num(e.Args[1])));
    public static IEnumerable<(EffectDef Effect, float Days)> Boons(CaptainMaking m) => Chosen(m).SelectMany(Boons);
    /// How long a boon lasts, in the creator's words.
    public static string BoonSpan(float days) => days <= 0 ? "LASTING" : $"{days:0} DAYS";

    public static bool Free(CaptainMaking m) => Chosen(m).Any(o => o.Effects.Contains("free"));
    public static int Age(CaptainMaking m) => BaseAge + Chosen(m).Sum(o => o.Age);
    public static IEnumerable<string> Perks(CaptainMaking m) => Chosen(m).Select(o => o.Perk).Where(p => p != "");

    /// Shape the captain from a making: the face, the road, skills, free perks and gear. The same call drives the creator's
    /// preview and the new campaign, so what the ledger shows is what you get.
    public static void Shape(CaptainState c, CaptainMaking m)
    {
        c.Birth = m.Birth;
        c.Origin = m.Past;
        c.Visage = m.Visage;
        c.Likeness = m.Look;
        c.Arms = m.Arms;
        Campaign.TakeRoad(c, m.Road);
        foreach (var o in Chosen(m))
        {
            foreach (var (verb, a) in o.Effects.Select(Parse))
                switch (verb)
                {
                    case "skill": c.Skills[a[0]] = MathF.Min(Skills.Max, Skills.Level(c, a[0]) + Num(a[1])); break;
                    case "gear": Campaign.Own(c, a[0]); break;
                    case "wear": Campaign.Own(c, a[0]); Wear(c.Kit, Gear.Get(a[0])); break;
                }
            // An origin's perk is free of its branch's marks, like a rank's (BonusPerks): forgetting it opens a free choice.
            if (o.Perk != "" && !c.Perks.Contains(o.Perk)) { c.Perks.Add(o.Perk); c.BonusPerks.Add(o.Perk); }
        }
    }

    static void Wear(CaptainKit k, GearDef g)
    {
        switch (g.Slot)
        {
            case "weapon": k.Weapon = g.Id; break;
            case "shield": k.Shield = g.Id; break;
            case "body": k.Body = g.Id; break;
            case "helm": k.Helm = g.Id; break;
            case "horse": k.Horse = g.Id; break;
        }
    }

    /// A captain shaped from the making on top of the usual starting skills: the creator's preview.
    public static CaptainState Preview(CaptainMaking m)
    {
        var c = new CaptainState();
        Campaign.StartingSkills(c);
        Shape(c, m);
        return c;
    }

    /// What an origin gives, one line each for the creator ("ARMS +10", "+18 SHIELDMEN", "PERK: QUARTERMASTER, UPKEEP -10%").
    /// `scale` turns base men into the men you will lead (Balance.ArmyScale).
    public static List<string> Describe(OriginDef o, float scale)
    {
        string Signed(float n) => (n >= 0 ? "+" : "") + n.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        var lines = new List<string>();
        if (Roads.Contains(o)) lines.AddRange(RoadGives(o.Id));
        if (o.Perk != "" && Skills.Perk(o.Perk) is PerkDef p) lines.Add($"PERK: {p.Name.ToUpperInvariant()}, {p.Text}");
        foreach (var (verb, a) in o.Effects.Select(Parse))
            lines.Add(verb switch
            {
                "skill" => $"{(Skills.All.FirstOrDefault(s => s.Id == a[0])?.Name ?? a[0]).ToUpperInvariant()} {Signed(Num(a[1]))}",
                "gold" => $"{Signed(Num(a[0]))} CROWNS",
                "renown" => $"{Signed(Num(a[0]))} RENOWN",
                "food" => $"{Signed(Num(a[0]))} FOOD",
                "horses" => $"{Signed(Num(a[0]))} HORSES",
                "opinion_realm" => $"YOUR HOMELAND'S LORDS {Signed(Num(a[0]))}",
                "men" => $"+{MathF.Round(Num(a[1]) * scale):0} {Troops.Get(a[0]).Name.ToUpperInvariant()} FOLLOW YOU",
                "gear" or "wear" => $"YOUR OWN {Gear.Get(a[0]).Name.ToUpperInvariant()}",
                "free" => "NO CONTRACT: A FREE COMPANY, NO UPKEEP PAID",
                "buff" => $"BOON: {(Effects.Has(a[0]) ? Effects.Get(a[0]).Name : a[0]).ToUpperInvariant()} ({BoonSpan(Num(a[1]))})",
                _ => fx(verb),
            });
        return lines.Distinct().ToList();
        static string fx(string v) => v.ToUpperInvariant();
    }

    /// What a road gives, read off the captain it makes (roads act in code, `Campaign.TakeRoad`): skills raised and the kit carried.
    public static List<string> RoadGives(string road)
    {
        var (a, b) = (new CaptainState(), new CaptainState());
        Campaign.TakeRoad(b, road);
        var lines = Skills.All.Where(s => Skills.Level(b, s.Id) > Skills.Level(a, s.Id)).Select(s => $"{s.Name.ToUpperInvariant()} {Skills.Level(b, s.Id):0}").ToList();
        void Kit(string slot, string was, string now) { if (now != was && now != "") lines.Add($"{slot}: {Gear.Get(now).Name.ToUpperInvariant()}"); }
        Kit("IN HAND", a.Kit.Weapon, b.Kit.Weapon);
        Kit("SHIELD", a.Kit.Shield, b.Kit.Shield);
        Kit("HORSE", a.Kit.Horse, b.Kit.Horse);
        Kit("AT YOUR SIDE", a.Kit.Side, b.Kit.Side);
        if (Gear.Get(b.Kit.Weapon) is { TwoHanded: true, Ranged: false }) lines.Add("TWO HANDS: NO SHIELD");
        return lines;
    }

    /// Data checks: ids unique, known verbs, skills, perks, gear and troops, and numbers where a verb needs them.
    public static IEnumerable<string> Validate()
    {
        if (Births.Count < 2 || Pasts.Count < 2 || Roads.Count < 1) yield return "too few births, pasts or roads";
        var all = Births.Concat(Pasts).Concat(Roads).ToList();
        foreach (var o in all)
        {
            if (all.Count(x => x.Id == o.Id) > 1) yield return $"{o.Id}: duplicate id";
            if (o.Name == "" || o.Text == "") yield return $"{o.Id}: needs a name and a text";
            if (o.Perk != "" && Skills.Perk(o.Perk) == null) yield return $"{o.Id}: unknown perk {o.Perk}";
            if (o.Age is < 0 or > 25) yield return $"{o.Id}: age out of range";
            foreach (var fx in o.Effects)
            {
                var (verb, a) = Parse(fx);
                bool ok = verb switch
                {
                    "free" => a.Length == 0,
                    "skill" => a.Length == 2 && Skills.All.Any(s => s.Id == a[0]) && Num(a[1]) > 0,
                    "gear" or "wear" => a.Length == 1 && Gear.Find(a[0]) != null,
                    "buff" => a.Length == 2 && Effects.Has(a[0]) && Num(a[1]) >= 0 && Num(a[1]) <= 365 && !Effects.Get(a[0]).Harmful,
                    "men" => a.Length == 2 && Troops.Has(a[0]) && Troops.Get(a[0]).Culture == "common" && Num(a[1]) is > 0 and <= 10,
                    "gold" or "renown" or "food" or "horses" or "opinion_realm" => a.Length == 1 && float.TryParse(a[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _),
                    _ => false,
                };
                if (!ok) yield return $"{o.Id}: bad effect {fx}";
            }
        }
        foreach (var r in Roads)
            if (r.Id is not ("sword" or "lance" or "bow" or "great" or "pole")) yield return $"{r.Id}: unknown road";
    }
}
