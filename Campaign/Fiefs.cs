using Microsoft.Xna.Framework;

namespace Ironfield;

/// A building a town or castle can raise (CAMPAIGN_OVERHAUL.md 6.2). Data: `Buildings` in Data/campaign.json overrides the defaults.
public sealed class BuildingDef
{
    public string Id = "", Name = "", Text = "";
    public int Cost = 400, Days = 6, Max = 1;
    /// Where it may stand.
    public bool Town = true, Castle = true;
}

/// Fiefs (step F6): prosperity, unrest and revolts, taxes, buildings, vassals with standing orders, and defection.
public sealed partial class Campaign
{
    public static readonly string[] TaxNames = { "LOW", "NORMAL", "HIGH" };
    public static readonly float[] TaxIncome = { 0.7f, 1, 1.35f };
    public const float RevoltAt = 70, DefectAt = 100;
    public static readonly string[] Orders = { "defend", "follow", "host" };

    static readonly List<BuildingDef> DefaultBuildings = new()
    {
        new() { Id = "yard", Name = "Training yard", Cost = 400, Days = 6, Max = 3, Text = "PROMOTE MEN HERE. LEVELS II AND III CUT PROMOTION GOLD 10% AND 20%" },
        new() { Id = "barracks", Name = "Barracks", Cost = 700, Days = 10, Text = "OPENS THIS PLACE'S CULTURE'S TIER V-VII TROOPS TO ITS REALM" },
        new() { Id = "walls", Name = "Walls", Cost = 800, Days = 12, Max = 3, Text = "HARDER TO STORM, A FULLER GARRISON, 10 MORE DAYS OF SUPPLIES PER LEVEL" },
        new() { Id = "market", Name = "Market", Cost = 600, Days = 8, Castle = false, Text = "INCOME +20%, FOOD 20% CHEAPER HERE" },
        new() { Id = "granary", Name = "Granary", Cost = 300, Days = 5, Text = "15 MORE DAYS OF SUPPLIES, AND WINTER DOES NOT THIN THE PEOPLE" },
        new() { Id = "stables", Name = "Stables", Cost = 400, Days = 6, Castle = false, Text = "MORE HORSES FOR SALE, 30% CHEAPER HERE" },
        new() { Id = "chapel", Name = "Chapel", Cost = 350, Days = 6, Text = "UNREST FALLS FASTER; PIOUS LORDS APPROVE OF YOU" },
        new() { Id = "watchtower", Name = "Watchtower", Cost = 250, Days = 4, Text = "RAIDS IN THE PROVINCE HURT HALF AS MUCH" },
    };

    public static IReadOnlyList<BuildingDef> BuildingDefs => B.Buildings.Count > 0 ? B.Buildings : DefaultBuildings;
    public static BuildingDef? BuildingOf(string id) => BuildingDefs.FirstOrDefault(d => d.Id == id);

    public static int Level(Settlement s, string id) => s.Buildings.GetValueOrDefault(id);

    /// The seat of a settlement's province (a village pays its seat's tax and shares its buildings' reach).
    public Settlement SeatOf(Settlement s) => s.Kind != SettlementKind.Village ? s
        : S.Settlements.FirstOrDefault(t => t.Province == s.Province && t.Kind != SettlementKind.Village) ?? s;

    /// What prosperity, tax and a market do to a settlement's income.
    float FiefFactor(Settlement s)
    {
        var seat = SeatOf(s);
        return (0.5f + s.Prosperity / 100f) * TaxIncome[Math.Clamp(seat.Tax, 0, 2)] * (Level(seat, "market") > 0 ? 1.2f : 1);
    }

    /// Every town starts with a barracks of its own culture (its realm's elite lines need one, F2 to F6).
    void SeedBuildings()
    {
        foreach (var s in S.Settlements.Where(s => s.Kind == SettlementKind.Town)) s.Buildings["barracks"] = 1;
        foreach (var s in S.Settlements.Where(s => s.Kind != SettlementKind.Village)) s.Prosperity = s.Kind == SettlementKind.Town ? 60 : 50;
    }

    // ---------- your choices ----------

    public bool CanBuild(Settlement s, BuildingDef d, out string why, out int cost)
    {
        int lv = Level(s, d.Id);
        cost = (int)(d.Cost * (1 + lv * 0.5f) * (1 - Skill("Stewardship") / 400f));
        why = s.Owner != Player.Id ? "ONLY IN YOUR OWN FIEFS"
            : s.Kind == SettlementKind.Village ? "BUILD IN THE TOWN OR CASTLE"
            : s.Kind == SettlementKind.Town && !d.Town || s.Kind == SettlementKind.Castle && !d.Castle ? $"NOT IN A {s.Kind.ToString().ToUpperInvariant()}"
            : lv >= d.Max ? "BUILT"
            : s.Building != "" ? $"BUILDING {BuildingOf(s.Building)?.Name.ToUpperInvariant()}"
            : s.Besieged ? "NOT UNDER SIEGE"
            : S.Gold < cost ? $"NEEDS {cost} CROWNS" : "";
        return why == "";
    }

    /// Start a building (or its next level) in your fief; it is done after its days.
    public string? Build(Settlement s, string id)
    {
        if (BuildingOf(id) is not BuildingDef d) return "NO SUCH BUILDING";
        if (!CanBuild(s, d, out string why, out int cost)) return why;
        S.Gold -= cost;
        StartBuilding(s, d);
        Log($"Work begins on {(Level(s, id) > 0 ? "more " : "")}{d.Name.ToLowerInvariant()} in {s.Name}.");
        return null;
    }

    void StartBuilding(Settlement s, BuildingDef d)
    {
        s.Building = d.Id;
        s.BuildUntil = (float)S.Hours + d.Days * 24;
    }

    /// Set a fief's tax: low taxes win the people over, high ones fill the purse and stir them.
    public string? SetTax(Settlement s, int level)
    {
        if (s.Owner != Player.Id) return "ONLY IN YOUR OWN FIEFS";
        s.Tax = Math.Clamp(level, 0, 2);
        foreach (var v in S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village)) v.Tax = s.Tax;
        return null;
    }

    // ---------- the daily life of a fief ----------

    /// The realm whose culture a settlement's people share, if one other than its holder still stands (where a revolt goes).
    int KinRealm(Settlement s) => Enumerable.Range(0, S.Factions.Count).FirstOrDefault(f => f != s.Faction && Alive(f) && Factions.CultureOf(f) == s.Culture, -1);

    /// How a seat's unrest moves a day, and why (for the fief tab).
    public (float Drift, List<string> Why) UnrestDrift(Settlement s)
    {
        var why = new List<string>();
        float d = -0.15f;
        void Add(float x, string w) { d += x; why.Add($"{w} {x:+0.0;-0.0}"); }
        if (Factions.CultureOf(s.Faction) != s.Culture && s.Culture != Factions.Common) Add(0.35f, "FOREIGN RULE");
        if (s.Tax == 2) Add(0.3f, "HIGH TAXES"); else if (s.Tax == 0) Add(-0.2f, "LOW TAXES");
        if (s.Besieged) Add(0.5f, "SIEGE");
        if (S.Settlements.Any(v => v.Province == s.Province && v.Kind == SettlementKind.Village && v.ThreatUntil > S.Hours)) Add(0.25f, "RAIDS");
        if (GarrisonMen(s) >= 30) Add(-0.2f, "GARRISON");
        if (Level(s, "chapel") > 0) Add(-0.25f, "CHAPEL");
        if (s.Prosperity < 25) Add(0.2f, "POVERTY");
        if (s.Owner == Player.Id)
        {
            if (Has("JustRuler")) Add(-0.2f, "JUST RULER");
            float st = Skill("Stewardship") / 300f;
            if (st >= 0.05f) Add(-st, "STEWARDSHIP");
        }
        if (Rebels(s) != null) Add(0.3f, "REBELS ABROAD");
        return (d, why);
    }

    /// How prosperity moves a day.
    public float ProsperityDrift(Settlement s)
    {
        var seat = SeatOf(s);
        if (s.Besieged) return -1;
        if (s.ThreatUntil > S.Hours) return -0.3f;
        float d = 0.12f + 0.03f * seat.Buildings.Values.Sum();
        if (seat.Tax == 0) d += 0.1f; else if (seat.Tax == 2) d -= 0.18f;
        if (Winter && Level(seat, "granary") == 0) d -= 0.1f;
        if (seat.Unrest > 50) d -= 0.1f;
        return d;
    }

    /// A rebel band raised by a settlement that is still in the field.
    public Party? Rebels(Settlement s) => S.Parties.FirstOrDefault(p => p.Kind == PartyKind.Bandit && !p.Down && p.HomeId == s.Id);

    /// Daily: buildings finish, prosperity and unrest drift, and an unhappy seat rises.
    void FiefsDaily()
    {
        foreach (var s in S.Settlements)
        {
            s.Prosperity = Math.Clamp(s.Prosperity + ProsperityDrift(s), 0, 100);
            if (s.Kind == SettlementKind.Village) continue;
            if (s.Building != "" && S.Hours >= s.BuildUntil)
            {
                var d = BuildingOf(s.Building);
                s.Buildings[s.Building] = Level(s, s.Building) + 1;
                if (s.Owner == Player.Id) Log($"The {d?.Name.ToLowerInvariant()} in {s.Name} is finished.");
                s.Building = "";
            }
            s.Unrest = Math.Clamp(s.Unrest + UnrestDrift(s).Drift, 0, 100);
            if (s.Unrest >= DefectAt && KinRealm(s) is int kin and >= 0) { Defects(s, kin); continue; }
            if (s.Unrest >= RevoltAt && Rebels(s) == null && rng.Chance(0.1f)) Revolt(s);
        }
    }

    /// The people take up arms: a rebel band of the settlement's own culture gathers outside the walls.
    void Revolt(Settlement s)
    {
        string root = Factions.Root(s.Culture);
        int men = 14 + (int)(s.Unrest / 5) + Day / 12;
        var p = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Bandit, Name = $"Rebels of {s.Name}", HomeId = s.Id, Tier = 2,
            Pos = s.Pos + new Vector2(rng.Range(-8, 8), rng.Range(-8, 8)) * Map.Spread, Moving = false, TaskUntil = (float)S.Hours + 24,
        };
        p.Companies.Add(NewCompany(root, "infantry", Math.Min(men, Rules.CapOf(root, "infantry")), 0, s));
        int more = men - p.Men;
        if (more > 0) p.Companies.Add(NewCompany(root, "spears", Math.Min(more, Rules.CapOf(root, "spears")), 0, s));
        p.Goal = p.Pos;
        S.Parties.Add(p);
        Log(s.Owner == Player.Id ? $"Your fief {s.Name} rises in revolt! {p.Men} rebels take the field." : $"{s.Name} rises against the {FactionNames[s.Faction]}.");
    }

    /// Rebels beaten in the field: their seat calms.
    void RebelsBeaten(Party p)
    {
        if (p.HomeId < 0 || p.HomeId >= S.Settlements.Count) return;
        var s = SettlementById(p.HomeId);
        s.Unrest = MathF.Max(0, s.Unrest - 20);
        Log($"The rebels of {s.Name} are scattered.");
    }

    /// At the end of its patience a seat goes over to the realm of its own people.
    void Defects(Settlement s, int kin)
    {
        if (s.Owner == Player.Id) Log($"{s.Name} throws open its gates to the {FactionNames[kin]}. Your fief is lost.");
        s.Garrison.Clear();
        Capture(s, kin, -1);
        s.Unrest = 30;
    }

    // ---------- AI building ----------

    /// Weekly: a realm with money builds one thing in one seat: walls on the border, else a market, else a barracks, else a yard.
    void BuildWeekly()
    {
        for (int f = 0; f < S.Factions.Count; f++)
        {
            var fs = S.Factions[f];
            if (!Alive(f) || fs.Treasury < 1400) continue;
            foreach (var s in S.Settlements.Where(s => s.Faction == f && s.Kind != SettlementKind.Village && s.Owner != Player.Id && s.Building == "" && !s.Besieged).OrderBy(_ => rng.Next(1000)))
            {
                bool border = S.Provinces[s.Province].Neighbors.Any(n => FactionsAtWar(f, S.Provinces[n].Faction));
                string? want = border && Level(s, "walls") < 2 ? "walls"
                    : s.Kind == SettlementKind.Town && Level(s, "market") == 0 ? "market"
                    : Level(s, "barracks") == 0 ? "barracks"
                    : Level(s, "yard") == 0 ? "yard" : null;
                if (want == null || BuildingOf(want) is not BuildingDef d) continue;
                int cost = (int)(d.Cost * (1 + Level(s, want) * 0.5f));
                // A lord who holds the seat pays from his own purse when he can.
                var lord = s.Owner >= 0 ? PartyById(s.Owner) : null;
                if (lord != null && lord.Gold >= cost + 300) lord.Gold -= cost;
                else if (fs.Treasury >= cost + 1000) fs.Treasury -= cost;
                else continue;
                StartBuilding(s, d);
                break;
            }
        }
    }

    // ---------- vassals (6.3) ----------

    /// Lords who answer to you: your own realm's lords as king, and companions you raised to land.
    public IEnumerable<Party> Vassals => S.Parties.Where(p => p.Kind == PartyKind.Lord && CharacterOf(p) is Character ch && !ch.Dead
        && (ch.Liege == S.CaptainChar || Rank == Rank.King && p.Faction == Captain.Contract));

    public bool IsVassal(Party p) => p.Kind == PartyKind.Lord && CharacterOf(p) is Character ch && (ch.Liege == S.CaptainChar || Rank == Rank.King && p.Faction == Captain.Contract);

    /// The fief you would give a new vassal: a castle before a town, never your last town.
    public Settlement? FiefToGrant()
    {
        var seats = Fiefs.Where(t => t.Kind != SettlementKind.Village).ToList();
        if (seats.Count < 2) return null;
        var keep = seats.Where(t => t.Kind == SettlementKind.Town).OrderBy(t => t.Id).FirstOrDefault() ?? seats.OrderBy(t => t.Id).First();
        return seats.Where(t => t != keep).OrderBy(t => t.Kind == SettlementKind.Town).ThenBy(t => t.Id).First();
    }

    public bool CanRaiseVassal(Character ch, out string why)
    {
        why = ch.Kind != "companion" || ch.Dead ? "NOT A COMPANION"
            : Rank < Rank.Lord ? $"ONLY A LORD MAY RAISE VASSALS ({RankName(Rank)} NOW)"
            : FiefToGrant() == null ? "YOU NEED TWO FIEFS: ONE TO KEEP, ONE TO GIVE"
            : S.Hours < ch.WoundedUntil ? "HE IS WOUNDED" : "";
        return why == "";
    }

    /// Give a companion a fief and a party: he becomes a lord of your realm, loyal to you, under your standing orders.
    public string? RaiseVassal(Character ch)
    {
        if (!CanRaiseVassal(ch, out string why)) return why;
        var fief = FiefToGrant()!;
        int f = Captain.Contract;
        var p = new Party { Id = S.NextId++, Kind = PartyKind.Lord, Faction = f, HomeId = fief.Id, Pos = fief.Pos + new Vector2(2, 0), Gold = 300, Tier = 2, Order = "defend" };
        p.Goal = p.Pos;
        // The company he led rides with him.
        if (Player.Companies.FirstOrDefault(c => c.Id == ch.CompanyId) is CompanyRecord led && Player.Companies.Count > 1)
        {
            Player.Companies.Remove(led);
            led.Hero = "";
            p.Companies.Add(led);
        }
        string troop = Armies.LineTroop("infantry", 2, f);
        p.Companies.Add(NewCompany(troop, "infantry", 20, 0, fief));
        S.Parties.Add(p);
        ch.Kind = "lord";
        ch.Faction = f;
        ch.Liege = S.CaptainChar;
        ch.CompanyId = -1;
        ch.PartyId = p.Id;
        ch.Name = ch.Name.StartsWith("Lord ") ? ch.Name : "Lord " + ch.Name;
        p.CharId = ch.Id;
        p.Name = ch.Name;
        fief.Owner = p.Id;
        foreach (var v in S.Settlements.Where(v => v.Province == fief.Province && v.Kind == SettlementKind.Village)) v.Owner = p.Id;
        Remember(ch, Me, "you raised me to a lord", 20);
        Log($"You raise {ch.Name} to a lord and grant him {fief.Name}.");
        RefreshHeroes();
        return null;
    }

    /// Give a vassal a standing order: defend your lands, follow you, or join the realm's host.
    public string? SetOrder(Party p, string order)
    {
        if (!IsVassal(p)) return "NOT YOUR VASSAL";
        if (!Orders.Contains(order)) return "NO SUCH ORDER";
        p.Order = order;
        p.TaskUntil = 0;
        p.PatrolProvince = -1;
        return null;
    }

    /// A vassal's standing order shapes where he patrols (null: no order, think as any lord).
    Province? OrderedProvince(Party p)
    {
        if (!IsVassal(p)) return null;
        return p.Order switch
        {
            "defend" => S.Provinces[(Fiefs.Where(t => t.Kind != SettlementKind.Village).OrderBy(t => Vector2.Distance(t.Pos, p.Pos)).FirstOrDefault() ?? SettlementById(p.HomeId)).Province],
            "host" => ObjectiveOf(p.Faction) is Settlement o ? S.Provinces[o.Province] : null,
            _ => null,
        };
    }

    // ---------- defection (6.3) ----------

    /// At each council: a lord who thinks very ill of his liege and well of another ruler goes over to him with his fiefs.
    List<string> Defections(int f)
    {
        var said = new List<string>();
        var ruler = RulerOf(f);
        if (ruler == null) return said;
        foreach (var lord in S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == f && c != ruler && !c.Traits.Contains("Loyal") && c.Liege != S.CaptainChar).ToList())
        {
            if (OpinionOf(lord, ruler) >= -30) continue;
            var to = Enumerable.Range(0, S.Factions.Count).Where(g => g != f && Alive(g) && RulerOf(g) is Character r && OpinionOf(lord, r) > 30)
                .OrderByDescending(g => OpinionOf(lord, RulerOf(g)!)).Cast<int?>().FirstOrDefault();
            if (to is not int g) continue;
            Defect(lord, g);
            said.Add($"{lord.Name} renounces the {FactionNames[f]} and goes over to the {FactionNames[g]}.");
        }
        return said;
    }

    /// Weekly: Pious lords of your realm approve of a chapel in your lands.
    void PiousWeekly()
    {
        if (!Fiefs.Any(t => Level(t, "chapel") > 0)) return;
        foreach (var l in S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == Captain.Contract && c.Traits.Contains("Pious")))
            l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) + 1, -100, 100);
    }

    /// A village raided (threatened by an enemy lord, or foraged): its people and its seat suffer; a watchtower halves it.
    void Raided(Settlement v)
    {
        var seat = SeatOf(v);
        float k = Level(seat, "watchtower") > 0 ? 0.5f : 1;
        v.Prosperity = MathF.Max(0, v.Prosperity - 6 * k);
        if (seat != v) { seat.Prosperity = MathF.Max(0, seat.Prosperity - 2 * k); seat.Unrest = MathF.Min(100, seat.Unrest + 2 * k); }
    }

    /// The troop a settlement's people enlist as for you: its culture's root, or a trained tier II at high prosperity (4.2).
    public string RecruitTroopFor(Settlement s)
    {
        string root = Factions.Root(s.Culture);
        if (s.Prosperity < 75) return root;
        int kin = Enumerable.Range(0, Factions.All.Count).FirstOrDefault(i => Factions.All[i].Culture == s.Culture, -1);
        return kin >= 0 ? Factions.All[kin].Lines["infantry"][1] : root;
    }
}
