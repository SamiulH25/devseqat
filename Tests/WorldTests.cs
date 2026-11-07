using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the side content (docs/SIDE_CONTENT.md): names, sites, quests, roads, town life, events.
public static partial class SelfTest
{
    /// S1: no place name repeats on a map, and a realm's lords carry its culture's names.
    static void WorldNames()
    {
        foreach (int seed in new[] { 11, 12, 13 })
        {
            var m = WorldMap.Generate(seed);
            var dup = m.Settlements.GroupBy(s => s.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Check(dup.Count == 0, $"seed {seed}: no repeated place names ({string.Join(", ", dup.Take(5))})");
            Check(m.Provinces.All(p => m.Settlements.Any(s => s.Province == p.Id && s.Name == p.Name && s.Kind != SettlementKind.Village)),
                "a province takes its seat's name");
        }
        var c = Campaign.New(5);
        var names = Names.Get();
        var lords = c.S.Characters.Where(x => x.Kind == "lord" && x.Faction is >= 0 and < 3).ToList();
        int native = lords.Count(l => names.LordsOf(Factions.CultureOf(l.Faction)).Contains(l.Name.Split(' ')[1]));
        Check(lords.Count > 0 && native == lords.Count, $"lords carry their culture's names ({native}/{lords.Count})");
        Check(lords.Select(l => l.Name).Distinct().Count() >= lords.Count - 1, "lords' names hardly ever repeat");
        var m11 = WorldMap.Generate(11);
        Console.WriteLine($"names: {m11.Settlements.Count} places on seed 11, e.g. {string.Join(", ", m11.Settlements.Take(4).Select(s => s.Name))}; "
            + $"{c.S.Provinces[0].Title}; {lords[0].Name}");
    }

    /// S2: sites sit off the roads and clear of settlements, every kind has its event, a visit raises it once, its effects
    /// apply, the site rests afterwards, and what you did there survives a save.
    static void Sites()
    {
        var kinds = new HashSet<string>();
        foreach (int seed in new[] { 11, 12, 13 })
        {
            var m = WorldMap.Generate(seed);
            float span = m.ExtentUsed / WorldMap.LegacyExtent;
            Check(m.Sites.Count >= m.Provinces.Count, $"seed {seed}: at least a site a province ({m.Sites.Count} for {m.Provinces.Count})");
            Check(m.Sites.All(x => m.RoadDist(x.Pos) >= 4 && m.Settlements.All(s => Vector2.Distance(s.Pos, x.Pos) >= 9 * span)), "sites sit off roads and clear of settlements");
            Check(m.Sites.Select((x, i) => x.Id == i).All(b => b), "site ids are their index");
            foreach (var x in m.Sites) kinds.Add(x.Kind);
        }
        foreach (var k in Campaign.SiteKinds)
        {
            Check(kinds.Contains(k), $"some map has a {k}");
            Check(Campaign.Events.Any(e => e.Trigger == "site_" + k), $"a {k} has an event");
        }
        var c = Campaign.New(7);
        // A spring: ride there, the event comes up, resting heals, and the spring rests for two weeks.
        var spring = c.Sites.First(x => x.Kind == "spring");
        var st = c.Player.Companies[0].Stacks[0];
        st.Wounded = 20; st.Men -= 20;
        c.Player.Pos = spring.Pos + new Vector2(6, 0);
        c.GoTo(spring);
        for (int i = 0; i < 40 && c.S.Event == null; i++) { c.Tick(0.5f); c.Encounter = null; }
        Check(c.S.Event is { Site: var sid } && sid == spring.Id, "reaching a site raises its event");
        int wounded = c.Player.Wounded;
        Check(c.Choose(0) == null && c.Player.Wounded < wounded, $"the spring heals ({wounded} -> {c.Player.Wounded})");
        Check(!c.SiteReady(spring) && c.SiteSeen(spring) && c.SiteWait(spring).StartsWith("AGAIN IN"), "the spring rests after a visit");
        c.Player.Pos = spring.Pos + new Vector2(6, 0);
        c.GoTo(spring);
        for (int i = 0; i < 40; i++) { c.Tick(0.5f); c.Encounter = null; }
        Check(c.S.Event == null || c.S.Event.Site != spring.Id, "a resting site raises nothing");
        var back = CampaignState.Deserialize(c.S.Serialize());
        Check(back.SiteUntil.ContainsKey(spring.Id) && back.SitesSeen.Contains(spring.Id), "site state survives a save");
        // Every kind's first choice can be taken.
        c.S.Gold = 5000;
        foreach (var k in Campaign.SiteKinds)
        {
            if (c.Sites.FirstOrDefault(x => x.Kind == k) is not Site site) continue;
            c.S.Event = null;
            c.S.SiteUntil.Clear();
            c.Player.Pos = site.Pos + new Vector2(5, 0);
            c.GoTo(site);
            for (int i = 0; i < 40 && c.S.Event == null; i++) { c.Tick(0.5f); c.Encounter = null; }
            Check(c.S.Event?.Site == site.Id, $"a {k} raises its event");
            Check(c.Choose(0) == null && c.S.Event == null, $"a {k}'s first choice can be taken");
        }
        Console.WriteLine($"sites: {c.Sites.Count} on seed 7 ({string.Join(", ", Campaign.SiteKinds.Select(k => $"{c.Sites.Count(x => x.Kind == k)} {k}"))}); log: {c.S.Log.LastOrDefault()?.Text}");
    }

    /// S4: caravans fill the roads, an arrival makes towns richer, a robbed caravan makes them poorer, and meeting one on the
    /// road lets you buy, ask for news, or rob it (its realm's lords think less of you).
    static void Roads()
    {
        var c = Campaign.New(9);
        for (int d = 0; d < 4; d++) { c.Tick(24); c.Encounter = null; c.S.Event = null; }
        int caravans = c.Caravans.Count();
        Console.WriteLine($"roads day 4: {caravans} caravans, {c.CaravansArrived} arrived, {c.CaravansLost} lost");
        Check(caravans >= c.CaravansWanted * 2 / 3, $"caravans take to the roads ({caravans} of {c.CaravansWanted})");
        // An arrival.
        var car = c.SpawnCaravan()!;
        var dest = c.SettlementById(car.GoalSettlement);
        dest.Prosperity = 50;
        car.Pos = dest.Pos + new Vector2(0.5f, 0);
        int arrived = c.CaravansArrived;
        c.Tick(1.5f);
        Check(c.CaravansArrived > arrived && dest.Prosperity > 50 && c.PartyById(car.Id) == null, $"a caravan that arrives makes its town richer ({dest.Prosperity:0})");
        // Robbed by brigands.
        var car2 = c.SpawnCaravan()!;
        var home = c.SettlementById(car2.HomeId);
        home.Prosperity = 50;
        car2.Pos = c.Player.Pos + new Vector2(200, 200);
        car2.Pos = Vector2.Clamp(car2.Pos, new Vector2(-c.Map.ExtentUsed + 20), new Vector2(c.Map.ExtentUsed - 20));
        var band = new Party { Id = c.S.NextId++, Kind = PartyKind.Bandit, Name = "Test Band", Pos = car2.Pos + new Vector2(1, 0), Tier = 3 };
        band.Companies.Add(c.NewCompany("men_at_arms", "infantry", c.Scaled(40), 0));
        c.S.Parties.Add(band);
        int lost = c.CaravansLost;
        for (int i = 0; i < 8 && c.CaravansLost == lost; i++) { c.Tick(0.5f); c.Encounter = null; }
        Check(c.CaravansLost > lost && home.Prosperity < 50, $"a robbed caravan makes its home poorer ({home.Prosperity:0})");
        // Meeting one of your own realm's caravans: buy food, then rob it.
        var mine = c.SpawnCaravan()!;
        mine.Faction = c.Captain.Contract;
        mine.Pos = c.Player.Pos + new Vector2(5, 0);
        mine.Goal = mine.Pos; mine.Moving = false; mine.TaskUntil = (float)c.S.Hours + 48;
        c.Chase(mine);
        for (int i = 0; i < 20 && c.Encounter == null; i++) c.Tick(0.25f);
        Check(c.Encounter == mine && c.Meeting(mine), "riding up to a friendly caravan is a meeting");
        float food = c.Player.Food;
        c.S.Gold = 500;
        Check(c.BuyFromCaravan() == null && c.Player.Food > food, "you can buy food from a caravan");
        var lord = c.S.Characters.First(x => x.Kind == "lord" && !x.Dead && x.Faction == mine.Faction);
        float before = c.OpinionOfYou(lord), gold = c.S.Gold;
        c.Rob();
        Check(!c.Meeting(mine) && c.Hostile(c.Player, mine), "robbing makes them your enemy");
        Check(c.OpinionOfYou(lord) < before - 10, "robbing your own realm's caravan angers its lords");
        c.AutoResolveEncounter();
        Check(c.PartyById(mine.Id) == null && c.S.Gold > gold, $"the robbed caravan is gone and its purse is yours ({gold:0} -> {c.S.Gold:0})");
        // A long run keeps them going.
        for (int d = 0; d < 30; d++) { c.Tick(24); c.Encounter = null; c.S.Event = null; }
        Console.WriteLine($"roads: {c.Caravans.Count()} caravans and {c.Pilgrims.Count()} pilgrim bands on the map; {c.CaravansArrived} arrived, {c.CaravansLost} robbed in 34 days");
        Check(c.CaravansArrived > 0, "caravans arrive over a month");
    }

    /// More quests (S3): elders' jobs, escorts, ransoms, rescues, the relic chain; each can be done, and chains move on.
    static void Quests()
    {
        var c = Campaign.New(9);
        void Hours(int n) { for (int i = 0; i < n; i++) { c.Player.Moving = false; c.Tick(1); c.Encounter = null; c.S.Event = null; } }
        Contract Job(string kind, int target, Settlement from) => new() { Id = c.S.NextId++, Kind = kind, Target = target, Faction = -1, From = from.Id, Deadline = (float)c.S.Hours + 72, Reward = 50 };
        bool Holds(string kind) => c.S.Contracts.Any(k => k.Kind == kind);
        var v = c.S.Settlements.First(s => s.Kind == SettlementKind.Village && !c.Refuses(s));
        var jobs = c.ElderOffers(v);
        Check(jobs.Count > 0 && jobs.All(j => j.Faction < 0 && j.From == v.Id), $"the elders of {v.Name} have work for anyone ({jobs.Count})");
        var first = jobs[0];
        Check(c.Accept(v, first) == null && c.S.Contracts.Contains(first), "an elders' job needs no contract");
        c.S.Contracts.Clear();
        // Militia: stay by the village twelve hours; the village prospers.
        v.Prosperity = 50;
        c.Player.Pos = v.Pos;
        c.S.Contracts.Add(Job("militia", v.Id, v));
        float gold = c.S.Gold;
        Hours(Campaign.MilitiaHours + 1);
        Check(!Holds("militia") && c.S.Gold > gold && v.Prosperity > 50, $"drilling the militia is paid and the village prospers ({v.Prosperity:0.0})");
        // A chain: fetch grain from the market, then bring it home.
        var market = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
        var fetch = Job("visit", market.Id, v);
        fetch.Reward = 0;
        fetch.Then = Job("bring", v.Id, v);
        c.S.Contracts.Add(fetch);
        c.Player.Pos = market.Pos;
        Hours(1);
        Check(!Holds("visit") && Holds("bring"), "a quest of two steps moves on to its second step");
        c.Player.Pos = v.Pos;
        c.Player.Food = 10;
        Hours(1);
        Check(!Holds("bring") && c.Player.Food < 8, $"the grain is handed over ({c.Player.Food:0.0} days left)");
        // A ransom carried to a town.
        c.S.Contracts.Add(Job("deliver", market.Id, market));
        c.Player.Pos = market.Pos;
        Hours(1);
        Check(!Holds("deliver"), "the ransom is delivered on arrival");
        // An escort: accepting it sends a caravan; riding in with it pays; losing it fails.
        var esc = new Contract { Id = c.S.NextId++, Kind = "escort", Target = -1, Faction = -1, Deadline = (float)c.S.Hours + 240 };
        Check(c.Accept(market, esc) == null && c.PartyById(esc.Target) is { Kind: PartyKind.Caravan }, "an escort job sends a caravan out");
        var car = c.PartyById(esc.Target)!;
        var dest = c.SettlementById(car.GoalSettlement);
        car.Pos = dest.Pos + new Vector2(0.5f, 0);
        c.Player.Pos = car.Pos;
        gold = c.S.Gold;
        Hours(2);
        Check(!Holds("escort") && c.S.Gold > gold, $"the escort is paid when the caravan arrives ({c.S.Gold - gold:0})");
        var esc2 = new Contract { Id = c.S.NextId++, Kind = "escort", Target = -1, Faction = -1, Deadline = (float)c.S.Hours + 240 };
        c.Accept(market, esc2);
        var car2 = c.PartyById(esc2.Target)!;
        car2.Down = true; c.S.Parties.Remove(car2);
        gold = c.S.Gold;
        Hours(1);
        Check(!Holds("escort") && c.S.Gold == gold, "an escort fails if the caravan is lost");
        // A rescue: beat the band.
        var band = new Party { Id = c.S.NextId++, Kind = PartyKind.Bandit, Name = "Test Band", Pos = c.Player.Pos + new Vector2(150, 150), Tier = 2 };
        band.Companies.Add(c.NewCompany("levies", "infantry", c.Scaled(10), 0));
        c.S.Parties.Add(band);
        c.S.Contracts.Add(Job("rescue", band.Id, v));
        c.S.LastBattleFoe = band.Id;
        Hours(1);
        Check(!Holds("rescue"), "beating the band rescues the captive");
        // The relic chain: the hermit, then the ruin.
        var lord = c.S.Characters.First(x => x.Kind == "lord" && !x.Dead);
        var ruin = c.Sites.First(x => x.Kind == "ruin");
        var relic = c.RelicQuest(lord, ruin.Pos);
        Check(relic is { Kind: "visitsite" }, "a lord can ask you to find his father's sword");
        relic!.Deadline = (float)c.S.Hours + 240;
        c.S.Contracts.Add(relic);
        for (int step = 0; step < 2 && Holds("visitsite"); step++)
        {
            var site = c.SiteById(c.S.Contracts.First(k => k.Kind == "visitsite").Target)!;
            c.Player.Pos = site.Pos + new Vector2(3, 0);
            c.GoTo(site);
            for (int i = 0; i < 6 && c.S.Contracts.Any(k => k.Kind == "visitsite" && k.Target == site.Id); i++) { c.Tick(1); c.Encounter = null; c.S.Event = null; }
        }
        Check(!Holds("visitsite"), "the sword is found at the end of the chain");
        Console.WriteLine($"quests: relic had {(relic.Then != null ? 2 : 1)} steps; gold {c.S.Gold:0}");
    }

    /// Town life (S5): the tavern talks of real things; a tournament is a real small battle with no deaths and a purse.
    static void TownLife()
    {
        var c = Campaign.New(9);
        var t = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
        var talk = c.Rumours(t);
        Console.WriteLine($"tavern at {t.Name}: " + string.Join(" | ", talk.Select(r => r.Text)));
        Check(talk.Count is >= 1 and <= 3 && talk.All(r => r.Text.Length > 0), "the tavern has two or three rumours");
        Check(talk.SequenceEqual(c.Rumours(t)), "the talk does not change from one visit to the next on the same day");
        Check(c.Flavour(t).Length > 20, "a town has a line that says what it is like");
        // Enter the lists through the event, then fight it as a real battle with veterans at your side.
        c.S.Event = new PendingEvent { Id = "tournament", Settlement = t.Id };
        c.S.Gold = 500;
        Check(c.Choose(0) != "" && c.Lists == t, "entering the tournament sends you to the lists");
        var vets = c.NewCompany(Armies.LineTroop("infantry", 5, Math.Max(0, t.Faction)), "infantry", 8, 0);
        c.Player.Companies.Add(vets);
        var setup = c.PrepareTournament(t);
        Check(setup.Options.Tournament && setup.Player.Companies.Sum(k => k.Count) == Campaign.TournamentMen, "your captain and a few of your best men ride in");
        setup.Options.AutoPlayer = true;
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        while (b.Winner < 0 && b.Time < 240) b.Update(1 / 60f);
        var r = b.Result();
        Console.WriteLine($"tournament: winner {r.Winner} after {b.Time:0} s");
        Check(r.Winner >= 0, "the tournament ends");
        int men = c.Player.Men + c.Player.Wounded;
        float gold = c.S.Gold, renown = c.Captain.Renown;
        var report = c.FinishBattle(r, setup);
        Check(c.Player.Men + c.Player.Wounded == men, "no one dies in the lists");
        Check(c.Captain.Renown > renown && (report.Won ? c.S.Gold >= gold + Campaign.TournamentPurse : c.S.Gold == gold), $"the winner takes the purse (won {report.Won})");
        var won = c.FinishBattle(r with { Winner = 0 }, setup);
        Check(won.Won && won.Loot == Campaign.TournamentPurse, "a won tournament pays its purse");
        Check(c.Lists == null || c.AutoTournament(t) != null, "the stand-in captain can enter without a fight");
    }
}
