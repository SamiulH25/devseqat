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
}
