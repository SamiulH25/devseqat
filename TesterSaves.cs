using Microsoft.Xna.Framework;

namespace Ironfield;

/// `--tester-saves [filter]`: writes a set of playtest saves into the save folder (`IRONFIELD_SAVES`, else the real one), one
/// campaign folder each (`test-NN-...`), so they show on the title's LOAD screen. Where a lived-in state matters the
/// stand-in captain (`Pilot`) plays the days first; then the scenario is set up and saved as a named save. Running it again
/// replaces only the `test-` folders. Headless: no thumbnails (the card shows the arms instead).
public static class TesterSaves
{
    record Scenario(string Id, string Label, string What, Action<Campaign> Setup, int Seed, int Realm, int Days);

    static readonly Scenario[] All =
    {
        new("01-fresh-start", "TEST 01: FRESH START", "Day one in the Crown, the guided first fight close by.", _ => { }, 101, 0, 0),
        new("02-first-month", "TEST 02: FIRST MONTH", "A month in: promotions due, a few battles behind you.", Rested, 102, 1, 30),
        new("03-sellsword", "TEST 03: SEASONED SELLSWORD", "Three months of war in someone else's pay.", Rested, 103, 2, 90),
        new("04-late-career", "TEST 04: LATE CAREER", "Most of two years played: rank, renown and a big company.", Rested, 104, 0, 200),
        new("05-sandbox", "TEST 05: RICH SANDBOX", "A full company of tiers IV-VI, an engine train, 20,000 crowns.", Sandbox, 105, 0, 10),
        new("06-big-battle", "TEST 06: BIG BATTLE AHEAD", "A strong company beside the biggest enemy host: unpause and it comes.", BigBattle, 106, 0, 40),
        new("07-siege", "TEST 07: AT THE WALLS", "Beside an enemy castle with engines in the train: besiege it.", Siege, 107, 1, 60),
        new("08-circle-aura", "TEST 08: CIRCLE AND AURA", "A circle of mages and a waking aura: learn spells, choose a path.", CircleAndAura, 108, 2, 45),
        new("09-knight-order", "TEST 09: AT THE CHAPTER HOUSE", "An order knows you well enough to set its trial.", KnightOrder, 109, 0, 45),
        new("10-monsters", "TEST 10: MONSTER COUNTRY", "A strong company near a wyvern, troll or monster band.", Monsters, 110, 4, 45),
        new("11-port", "TEST 11: AT A PORT", "In a friendly port with gold for a passage.", Port, 111, 3, 20),
        new("12-castle-muster", "TEST 12: CASTLE MUSTER", "At a friendly castle with gold: muster, stables, the yard.", CastleMuster, 112, 5, 30),
    };

    public static void Run(string? filter)
    {
        Directory.CreateDirectory(Saves.Root);
        // Newest first on the LOAD screen: write them last to first so TEST 01 heads the list.
        foreach (var sc in All.Where(s => filter == null || s.Id.Contains(filter)).Reverse())
        {
            foreach (var old in Directory.GetDirectories(Saves.Root, $"test-{sc.Id}*")) Directory.Delete(old, true);
            var c = Build(sc);
            string path = Saves.Save(c.S, SaveKind.Named, sc.Label);
            var back = Saves.Load(path) ?? throw new Exception($"{sc.Id}: the save did not load back");
            var bc = new Campaign(back);
            Console.WriteLine($"{sc.Label}: day {bc.Day}, {bc.Stage}, {bc.Player.Men} men in {bc.Player.Companies.Count} companies "
                + $"(tiers {string.Join(" ", bc.Player.Companies.Select(k => k.Type.Tier))}), {bc.S.Gold:0} crowns, renown {bc.Captain.Renown:0}, "
                + $"serving {(bc.Captain.Contract >= 0 ? bc.FactionNames[bc.Captain.Contract] : "nobody")}, near {Nearest(bc)}");
            var close = bc.S.Parties.Where(p => p != bc.Player && !p.Down && Vector2.Distance(p.Pos, bc.Player.Pos) < 25)
                .OrderBy(p => Vector2.Distance(p.Pos, bc.Player.Pos)).Take(3)
                .Select(p => $"{p.Name} ({p.Kind}{(p.Breed != "" ? " " + p.Breed : "")}, {p.Men} men, {Vector2.Distance(p.Pos, bc.Player.Pos):0} away{(bc.Hostile(bc.Player, p) ? ", hostile" : "")})");
            Console.WriteLine("    close by: " + string.Join("; ", close.DefaultIfEmpty("nobody"))
                + (bc.Player.Train.Count > 0 ? $"; train: {string.Join(", ", bc.Player.Train)}" : "")
                + (bc.S.Orders.FirstOrDefault(o => o.Standing >= Campaign.TrialStanding) is OrderState os ? $"; {os.Id} order standing {os.Standing:0}, trial: {bc.CannotSeekTrial(os.Id) ?? "open"}" : "")
                + (bc.Circle is CompanyRecord circle ? $"; circle {circle.Troop} x{circle.Men}" : "")
                + (bc.Captain.AuraTier > 0 ? $"; aura tier {bc.Captain.AuraTier}" : ""));
        }
        Saves.Changed();
        Console.WriteLine($"saved in {Saves.Root}");
    }

    /// The scenarios' ids ("01-fresh-start"...), for the playtest harness (`--play --scenario`).
    public static IEnumerable<(string Id, string What)> Ids => All.Select(s => (s.Id, s.What));

    /// The campaign of the first scenario whose id contains `filter` (null: none matches), played and set up as its save is.
    public static Campaign? Build(string filter) => All.FirstOrDefault(s => s.Id.Contains(filter)) is Scenario sc ? Build(sc) : null;

    static Campaign Build(Scenario sc)
    {
        var c = Campaign.New(sc.Seed, sc.Realm);
        if (sc.Days > 0) new Pilot(c).Run(sc.Days * 24);
        Settle(c);
        sc.Setup(c);
        c.S.CampaignId = $"test-{sc.Id}";
        c.Player.Name = sc.Label.Replace(":", "");
        c.Log($"Tester save: {sc.What}");
        return c;
    }

    /// Clear what the stand-in left half done: a fight in the road, a report, a wound, an empty larder.
    static void Settle(Campaign c)
    {
        c.Encounter = null; c.Arrived = null; c.Report = null; c.Beat = null;
        c.Player.Food = MathF.Max(c.Player.Food, 20);
        c.Player.Voyage.Clear();
        c.Player.Moving = false;
        c.Player.Goal = c.Player.Pos;
    }

    static void Rested(Campaign c) => c.Captain.WoundedUntil = 0;

    static string Nearest(Campaign c) => c.S.Settlements.OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).First() is var s
        ? $"{s.Name} ({s.Kind.ToString().ToLowerInvariant()}, {c.FactionNames[Math.Max(0, s.Faction)]})" : "";

    static void Place(Campaign c, Vector2 at)
    {
        c.Player.Pos = at; c.Player.Goal = at; c.Player.Moving = false;
        c.Player.Voyage.Clear();
    }

    /// Replace the company with one of the realm's lines: (role, tier, base men) per company, as many as the slots allow.
    static void Army(Campaign c, params (string Role, int Tier, int Men)[] list)
    {
        int realm = Math.Max(0, c.Captain.Contract >= 0 ? c.Captain.Contract : c.Player.Faction);
        var home = c.S.Settlements.Where(s => s.Faction == realm && s.Kind == SettlementKind.Town).OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).FirstOrDefault();
        c.Player.Companies.Clear();
        foreach (var (role, tier, men) in list.Take(Rules.CompanySlots(c.Captain)))
            c.Player.Companies.Add(c.NewCompany(Armies.LineTroop(role, tier, realm), role, c.Scaled(men), 0, home));
        c.RefreshHeroes();
    }

    static void Rich(Campaign c, float gold, float renown)
    {
        c.S.Gold = MathF.Max(c.S.Gold, gold);
        c.Captain.Renown = MathF.Max(c.Captain.Renown, renown);
        c.Captain.WoundedUntil = 0;
        c.Player.Food = MathF.Max(c.Player.Food, 40);
    }

    static readonly (string, int, int)[] Strong =
    {
        ("infantry", 6, 18), ("infantry", 5, 20), ("infantry", 4, 22), ("spears", 5, 20), ("spears", 4, 22),
        ("archers", 5, 20), ("archers", 4, 22), ("horse", 6, 14), ("horse", 5, 15), ("horse", 4, 16),
    };

    static void Sandbox(Campaign c)
    {
        Rich(c, 20000, 600);
        Army(c, Strong);
        c.Player.Train.AddRange(new[] { "scorpion", "springald" });
    }

    static bool AtWarWith(Campaign c, int f) => c.FactionsAtWar(c.Captain.Contract, f);

    static void BigBattle(Campaign c)
    {
        Rich(c, 3000, 400);
        Army(c, Strong);
        var host = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && !p.Captive && AtWarWith(c, p.Faction))
            .OrderByDescending(p => p.ArmyId >= 0 ? c.S.Parties.Where(q => q.ArmyId == p.ArmyId).Sum(q => q.Men) : p.Men).FirstOrDefault();
        if (host != null) Place(c, host.Pos + new Vector2(10, 0));
    }

    static void Siege(Campaign c)
    {
        Rich(c, 5000, 300);
        Army(c, ("infantry", 5, 20), ("infantry", 4, 22), ("infantry", 4, 22), ("spears", 4, 22), ("archers", 5, 20), ("archers", 4, 22), ("horse", 4, 16));
        c.Player.Train.AddRange(new[] { "scorpion", "springald" });
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && AtWarWith(c, s.Faction))
            .OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).FirstOrDefault();
        if (castle != null) Place(c, castle.Pos + new Vector2(4, 0));
    }

    static void CircleAndAura(Campaign c)
    {
        Rich(c, 4000, 250);
        int realm = Math.Max(0, c.Captain.Contract >= 0 ? c.Captain.Contract : c.Player.Faction);
        var mage = Troops.All.Where(t => t.Class == UnitClass.Caster && t.Tier == 5 && t.Culture == Factions.CultureOf(realm)).Select(t => t.Id).FirstOrDefault() ?? "magisters";
        Army(c, ("infantry", 4, 22), ("infantry", 3, 24), ("spears", 4, 22), ("archers", 4, 22), ("horse", 4, 16));
        c.Player.Companies.Add(c.NewCompany(mage, "casters", c.Scaled(6), 0));
        c.RefreshHeroes();
        c.Captain.AuraTier = 1;
        c.Captain.AuraXp = c.AuraNeed * 0.6f;
        c.Captain.AuraWall = false;
    }

    static void KnightOrder(Campaign c)
    {
        Rich(c, 2500, 200);
        var o = c.S.Orders.Where(x => !x.Gone && x.Chapter >= 0 && !KnightOrders.Get(x.Id).Hidden && !AtWarWith(c, x.Patron))
            .OrderByDescending(x => x.Patron == c.Captain.Contract).FirstOrDefault();
        if (o == null) return;
        o.Standing = Campaign.TrialStanding + 10;
        Place(c, c.SettlementById(o.Chapter).Pos + new Vector2(2, 0));
    }

    static void Monsters(Campaign c)
    {
        Rich(c, 2000, 250);
        Army(c, ("infantry", 5, 20), ("infantry", 4, 22), ("spears", 5, 20), ("spears", 4, 22), ("archers", 5, 20), ("archers", 4, 22), ("horse", 5, 15));
        var beast = c.S.Parties.Where(p => p.Kind == PartyKind.Monster && !p.Down && !p.Lair)
            .OrderBy(p => p.Breed switch { "wyvern" => 0, "troll" => 1, "bear" => 2, _ => 3 }).ThenBy(p => Vector2.Distance(p.Pos, c.Player.Pos)).FirstOrDefault();
        if (beast != null) Place(c, beast.Pos + new Vector2(14, 0));
    }

    static void Port(Campaign c)
    {
        Rich(c, 2500, 150);
        var port = c.S.Settlements.Where(s => c.IsPort(s) && !c.Refuses(s)).OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).FirstOrDefault();
        if (port != null) Place(c, port.Pos + new Vector2(2, 0));
    }

    static void CastleMuster(Campaign c)
    {
        Rich(c, 4000, 200);
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && !c.Refuses(s) && !AtWarWith(c, s.Faction)
                && c.S.Settlements.Any(v => v.Holder == s.Id))
            .OrderByDescending(s => s.Faction == c.Captain.Contract).ThenBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).FirstOrDefault();
        if (castle != null) Place(c, castle.Pos + new Vector2(2, 0));
    }
}
