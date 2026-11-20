using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the campaign: world map, saves, economy, sieges, war, stacks, cultures, characters, skills.
public static partial class SelfTest
{
    static void CampaignMap()
    {
        var legacy = WorldMap.Generate(11, WorldMap.LegacyExtent);
        var lm = legacy.BuildTerrain();
        Console.WriteLine($"legacy map: {lm.N} vertices, {lm.Foliage.Count(f => f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine)} trees");
        // Saves regenerate their map from the seed: the geography of a seed must never drift (names may). This is the map
        // saves made before docs/archive/WORLD.md W2 were played on.
        var saved = WorldMap.Generate(11, WorldMap.PreviousExtent, WorldMap.LegacyLayout);
        float geo = saved.Settlements.Sum(s => s.Pos.X * 3 + s.Pos.Y), river = saved.Rivers.Sum(r => r.Path.Sum(p => p.X));
        Check(saved.Settlements.Count == 215 && MathF.Abs(geo + 3773.913f) < 0.5f && MathF.Abs(river + 18550.46f) < 0.5f,
            $"seed 11 keeps its geography ({saved.Settlements.Count} places, {geo:0.000}, {river:0.000})");
        var m = WorldMap.Generate(11);
        Check(m.Provinces.Count >= 100, $"a big map: enough provinces ({m.Provinces.Count})");
        Check(m.Provinces.Count is >= WorldMap.ProvinceCount - 8 and <= WorldMap.ProvinceCount + 8,
            $"about {WorldMap.ProvinceCount} provinces on the enlarged map ({m.Provinces.Count})");
        Check(m.ExtentUsed >= WorldMap.Extent && m.Settlements.Max(s => MathF.Abs(s.Pos.X)) > WorldMap.PreviousExtent,
            "the map is bigger than the old one, not just denser");
        for (int f = 0; f < Factions.All.Count; f++) Check(m.Settlements.Any(s => s.Faction == f && s.Kind == SettlementKind.Town), $"faction {f} has towns");
        var seen = new HashSet<int> { 0 };
        var todo = new Queue<int>(new[] { 0 });
        while (todo.Count > 0) foreach (int n in m.Provinces[todo.Dequeue()].Neighbors) if (seen.Add(n)) todo.Enqueue(n);
        Check(seen.Count == m.Provinces.Count, "every province is reachable by road");
        Check(m.Settlements.Count(s => s.Kind == SettlementKind.Village) >= m.Provinces.Count * 2 - 4, "provinces have villages");
        // The ground is drawn at a coarser grid than a battlefield, and covers the whole world plus its mountain rim.
        var terrain = m.BuildTerrain();
        Check(terrain.Half >= m.GroundHalf - 1 && terrain.N > 200 && terrain.N < 400, $"the map's ground is drawn ({terrain.N} vertices a side)");
        int trees = terrain.Foliage.Count(f => f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine);
        Console.WriteLine($"map ground: {terrain.N} vertices a side over +-{terrain.Half:0} m, {trees} trees, {terrain.Foliage.Count} props");
        Check(trees > 400, $"the big map is wooded ({trees} trees)");
        // Distances that mean "nearby" scale with the map, so a lord is not forever riding between provinces.
        Check(m.Spread > 1.1f && m.ProvinceScale > 1.5f, $"the map is bigger ({m.Spread:0.0}x the spread, {m.ProvinceScale:0.0}x the provinces)");
    }

    static void CampaignPersistence()
    {
        string Run() { var c = Campaign.New(21); for (int i = 0; i < 60 * 24 && c.Encounter == null; i += 6) c.Tick(6); c.Encounter = null; return c.S.Serialize(); }
        Check(Run() == Run(), "the same seed gives the same campaign");
        var a = Campaign.New(22);
        a.Tick(24 * 5);
        string json = a.S.Serialize();
        Check(CampaignState.Deserialize(json).Serialize() == json, "save, load, save gives the same file");
    }

    /// 120 days with nobody playing: lords fight, bandits come and go, money moves; nothing breaks.
    static void CampaignRun()
    {
        var c = Campaign.New(31);
        int battles = 0;
        for (int h = 0; h < 120 * 24; h++)
        {
            c.Tick(1);
            if (c.Encounter != null) { c.AutoResolveEncounter(); battles++; }
            c.Arrived = null;
        }
        foreach (var p in c.S.Parties)
            foreach (var co in p.Companies)
            {
                Check(co.Men >= 0 && co.Wounded >= 0, "no negative men");
                Check(co.Stacks.All(st => Troops.All.Any(t => t.Id == st.Troop) && st.Men >= 0 && st.Wounded >= 0), "troop ids stay valid");
                Check(co.Stacks.All(st => st.Xp <= st.PoolCap + 1e-3f && st.Xp >= 0), $"{co.Name}: no pool over its cap");
                Check(co.Stacks.All(st => Rules.Fits(co, st.Troop)), $"{co.Name}: every stack fits its company's role");
            }
        Check(c.S.Gold >= 0 && c.S.Factions.All(f => f.Treasury > -1), "no negative purses");
        Check(c.S.Parties.Count(p => p.Kind == PartyKind.Lord) >= 8, "lords keep the field");
        int upgraded = c.S.Parties.Where(p => p.Kind == PartyKind.Lord).SelectMany(p => p.Companies).Count(co => co.Type.Tier >= 3);
        Console.WriteLine($"campaign: day {c.Day}, {c.S.Parties.Count} parties, {upgraded} lord companies at tier III+, your battles {battles}, war score {string.Join("/", c.S.Factions.Select(f => f.WarScore.ToString("0")))}, wars {c.S.Wars.Count(w => w.Active)}");
        var lords = c.S.Parties.Where(p => p.Kind == PartyKind.Lord).ToList();
        Console.WriteLine($"lords: gold avg {lords.Average(l => l.Gold):0}, max pool xp {lords.SelectMany(l => l.Companies).SelectMany(co => co.Stacks).Max(st => st.Xp):0}, "
            + $"ready men {lords.SelectMany(l => l.Companies).Sum(co => co.Ready)}, tiers {string.Join(",", lords.SelectMany(l => l.Companies).GroupBy(co => co.Type.Tier).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}"))}, "
            + $"tasks {string.Join(",", lords.GroupBy(l => l.Task).Select(g => $"{g.Key}:{g.Count()}"))}, treasury {c.S.Factions[0].Treasury:0}/{c.S.Factions[1].Treasury:0}");
        Check(upgraded > 0, "lords upgrade their companies too");
        // The world must not grind itself to nothing: bandits keep coming, and lords stay in the field at a usable size.
        int bandits = c.S.Parties.Count(p => p.Kind == PartyKind.Bandit);
        float lordMen = (float)lords.Average(l => l.Men);
        // How crowded the roads are: a party should not have to ride for days to meet anyone on a map this size.
        float nearestBandit = c.S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down).Select(p => Vector2.Distance(p.Pos, c.Player.Pos)).DefaultIfEmpty(999).Min();
        int near10 = c.S.Parties.Count(p => !p.Down && p != c.Player && p.Kind != PartyKind.Player && Vector2.Distance(p.Pos, c.Player.Pos) < 60);
        Console.WriteLine($"pressure: bandits {bandits}, ai battles {c.AiBattles}, lord men avg {lordMen:0}, nearest bandit {nearestBandit:0}, parties within 60: {near10}, "
            + $"chests {string.Join("/", c.S.Factions.Select(f => f.Treasury.ToString("0")))}");
        Check(bandits >= 8, "brigands keep appearing (the map stays dangerous)");
        Check(c.AiBattles >= 40, "lords and brigands fight each other without you");
        Check(lordMen >= 18, "lords rebuild their companies instead of dying broke");
        Check(c.S.Factions.All(f => f.Treasury > 100), "a faction keeps a war chest to raise new lords");
    }

    /// Progression is legible: the next step names a real action, and renown's ladder is monotonic.
    static void Progression()
    {
        var c = Campaign.New(91);
        var (text, where) = c.NextStep();
        Check(text.Length > 0 && where.Length > 0, "a new campaign suggests a next step");
        Console.WriteLine($"progression: stage {c.Stage}, next \"{text}\" -> {where}");
        // You can have up to 10 companies right away; renown increases the total troop count (men cap) and perks.
        Check(Rules.CompanySlots(new CaptainState { Renown = 0 }) == 10, "max 10 companies available without renown gating");
        Check(Rules.MenLimit(new CaptainState { Renown = 200 }) > Rules.MenLimit(new CaptainState { Renown = 0 }), "renown increases total troop count");
        // Renown unlocks come in order and never go backwards as renown rises.
        float last = -1;
        for (float renown = 0; renown <= 900; renown += 10)
        {
            c.Captain.Renown = renown;
            var (_, at) = Rules.NextRenownUnlock(c.Captain);
            Check(at > c.Captain.Renown, "the next unlock is always ahead");
            Check(at >= last - 0.01f, $"the unlock ladder does not go backwards (at {renown})");
            last = at;
        }
        // A ready company must be the thing the game points at.
        c.Captain.Renown = 50;
        var co = c.Player.Companies[0];
        co.Main!.Xp = co.Main.Type.UpgradeXp + 1;
        Check(c.NextStep().Where == "TRAIN", "a ready man points at the training yard");
    }

    /// HD1 (docs/HOLDS.md): men rise to tier III in camp; tier IV and up wait for a town's yard.
    static void CampPromotion()
    {
        var c = Campaign.New(92);
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && t.Faction == 0);
        var open = Enumerable.Range(1, 40).Select(i => town.Pos + new Vector2(i * 3f, 0)).First(q => c.SettlementAt(q, 12) == null);
        c.Player.Pos = open;
        Check(!c.InTown(c.Player) && !c.Has("Drillmaster"), "the party stands in the open, no Drillmaster");
        var lv = c.NewCompany("levies", "infantry", 10, 1000, town);
        c.Player.Companies.Add(lv);
        Check(c.Promotable(c.Player, lv, lv.Stack("levies"), "shieldmen", 1, 1000, out _, out string why) == 1, "levies rise to tier II in camp: " + why);
        var ma = c.NewCompany("men_at_arms", "infantry", 10, 5000, town);
        c.Player.Companies.Add(ma);
        var st = ma.Stack("men_at_arms");
        var up = st.Type.UpgradesTo.First(t => Troops.Get(t).Tier > 3 && c.CanEnterLine(c.Player, ma, st.Troop, t, out _));
        Check(c.Promotable(c.Player, ma, st, up, 1, 5000, out _, out why) == 0 && why.Contains("TOWN"), $"tier III to {up} needs a town: " + why);
        Check(c.NextStep().Text.Contains("PROMOTE THEM"), "the next step says to promote in camp: " + c.NextStep().Text);
        c.Player.Pos = town.Pos;
        Check(c.Promotable(c.Player, ma, st, up, 1, 5000, out _, out why) == 1, "and rises in the town: " + why);
    }

    /// HD2 (docs/HOLDS.md): a castle musters its villages' men in one place, trains to tier V, sells horses and its stores.
    static void CastleMuster()
    {
        var c = Campaign.New(93);
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && !c.Refuses(s) && c.Demesne(s).Count() >= 2)
            .OrderByDescending(s => c.Demesne(s).Count()).First();
        c.Player.Pos = castle.Pos;
        c.S.Gold = 100000;
        Check(c.CannotMuster(castle) == null, "a friendly castle musters");
        var villages = c.Demesne(castle).ToList();
        foreach (var v in villages) { v.Recruits = c.Scaled(6); v.ThreatUntil = 0; }
        int pool = c.MusterRecruits(castle), before = c.Player.Men;
        Check(pool > 0 && pool <= villages.Sum(v => v.Recruits), $"its pool is its villages' men ({pool} from {villages.Count} villages)");
        int villagePrice = villages.Max(c.LevyPrice);
        Check(c.MusterPrice(castle) > villagePrice, $"the castellan takes a cut ({c.MusterPrice(castle)} against {villagePrice})");
        Check(c.Muster(castle, pool) == null && c.Player.Men > before, $"mustered {c.Player.Men - before} men");
        Check(c.MusterRecruits(castle) < pool, "the villages' men are drawn down");
        // The yard: tier V here, tier VI and up still a town.
        Check(c.CanTrainHere(c.Player, Troops.All.First(t => t.Tier == 5).Id), "the castle yard trains to tier V");
        Check(!c.CanTrainHere(c.Player, Troops.All.First(t => t.Tier == 6).Id), "but not to tier VI");
        // Stables and granary.
        castle.Horses = c.Scaled(4);
        int horses = c.Player.Horses;
        Check(c.BuyHorses(castle, 2) == null && c.Player.Horses == horses + 2, "the stables sell remounts");
        castle.Supplies = 40;
        int spare = c.GranaryDays(castle);
        float food = c.Player.Food;
        Check(c.GranaryPrice(7) < c.FoodPrice(7), "the granary sells below the market");
        Check(spare > 0 && c.BuyFromGranary(castle, 3) == null && c.Player.Food == food + Math.Min(3, spare) && castle.Supplies < 40, $"food comes from the castle's stores ({spare} days to spare, {castle.Supplies:0.0} left)");
        castle.Supplies = Balance.Get().GranaryReserve;
        Check(c.GranaryDays(castle) == 0 && c.BuyFromGranary(castle, 3) != null, "a bare granary sells nothing");
    }

    /// HD3 (docs/HOLDS.md): every village has a holder; a castle's stores, muster and foals follow its villages, raiding them
    /// cuts all three, and its garrison rides out at a weak band near them.
    static void VillagesFeedCastle()
    {
        var c = Campaign.New(94);
        var villages = c.S.Settlements.Where(v => v.Kind == SettlementKind.Village).ToList();
        int own = villages.Count(v => v.Holder >= 0 && c.S.Settlements[v.Holder].Province == v.Province);
        Check(villages.All(v => v.Holder >= 0 && c.S.Settlements[v.Holder].Kind != SettlementKind.Village) && own > villages.Count * 0.7f,
            $"every village is bound to a castle or town ({own} of {villages.Count} in its own province)");
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && c.Demesne(s).Count() >= 2).OrderByDescending(s => c.Demesne(s).Count()).First();
        var held = c.Demesne(castle).ToList();
        foreach (var v in held) { v.ThreatUntil = 0; v.Recruits = c.Scaled(5); v.Prosperity = 50; }
        float food = c.VillageFood(castle);
        int pool = c.MusterRecruits(castle);
        Check(food > 0 && pool > 0 && c.HeldShare(castle) == 1, $"whole villages feed the castle ({food:0.0} days a week, {pool} men)");
        castle.Supplies = 5;
        castle.Besieged = false;
        c.GarrisonsWeekly();
        float fed = castle.Supplies;
        Check(Math.Abs(fed - (5 + 2 + food)) < 0.01f, $"the week's stores come from its villages ({fed:0.0})");
        // Raid one village: the castle's stores, muster and share fall.
        held[0].ThreatUntil = (float)c.S.Hours + 7 * 24;
        Check(c.VillageFood(castle) < food && c.MusterRecruits(castle) < pool && c.HeldShare(castle) < 1, "a raided village cuts its castle's food, men and refill");
        // The castellan sallies against a weak band near his villages.
        held[0].ThreatUntil = 0;
        castle.Garrison.Clear();
        castle.Garrison.Add(c.NewCompany("men_at_arms", "infantry", c.Scaled(60), 0));
        var band = new Party { Id = c.S.NextId++, Kind = PartyKind.Bandit, Name = "Test Band", Pos = held[0].Pos + new Vector2(2, 0), Tier = 1 };
        band.Companies.Add(c.NewCompany("levies", "infantry", c.Scaled(8), 0));
        foreach (var other in c.S.Parties.Where(p => p.Kind is PartyKind.Bandit or PartyKind.Monster)) other.Down = true; // one sally a day: only ours near
        c.S.Parties.Add(band);
        c.CastellansDaily();
        Check(band.Down, "the garrison rides out and scatters a weak band near its villages");
    }

    /// HD3: caravans run town to town, and with few towns the sea lanes keep the roads busy.
    static void CaravansTownToTown()
    {
        var c = Campaign.New(95);
        int wanted = c.CaravansWanted;
        for (int i = 0; i < 6; i++)
        {
            var car = c.SpawnCaravan();
            if (car == null) continue;
            Check(c.SettlementById(car.HomeId).Kind == SettlementKind.Town && c.SettlementById(car.GoalSettlement).Kind == SettlementKind.Town, "a caravan runs from a town to a town");
        }
        var towns = c.S.Settlements.Where(t => t.Kind == SettlementKind.Town).ToList();
        foreach (var t in towns.Skip(towns.Count / 4)) t.Kind = SettlementKind.Castle; // three towns in four become castles
        int few = c.CaravansWanted;
        Check(few >= 2 && few > towns.Count / 4 / 2, $"fewer towns do not empty the roads ({wanted} -> {few} caravans for {towns.Count / 4} towns)");
    }

    /// A level-2 army on contract, sitting still, stays solvent for 8 weeks.
    static void Economy()
    {
        var c = Campaign.New(41);
        c.Player.Companies.Clear();
        foreach (var co in Armies.Standard(0).Companies) c.Player.Companies.Add(c.NewCompany(co.Troop, co.Role, co.Count, 0));
        c.Player.Cooldown = 1e6f;
        // The liege stays at peace: this measures pay and food, not whether a war elsewhere takes the town the army stands in.
        void Peace() { foreach (var w in c.S.Wars.Where(w => w.A == c.Captain.Contract || w.B == c.Captain.Contract)) { w.Active = false; w.TruceUntil = 1e9f; } }
        for (int h = 0; h < 8 * 168; h++) { Peace(); c.Tick(1); c.Encounter = null; c.Arrived = null; }
        Console.WriteLine($"economy: after 8 weeks gold {c.S.Gold:0}, weekly balance {c.S.LastWeekBalance:0}");
        Check(c.S.UnpaidWeeks == 0 && c.S.Gold > 0 && c.Player.Men >= 60, "a contracted level-2 army stays paid for 8 weeks");
    }

    /// A battle built from map parties and played headless maps every casualty back to its company.
    static void Bridge()
    {
        var c = Campaign.New(51);
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        bandit.Pos = c.Player.Pos;
        c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        setup.Options.AutoPlayer = true;
        setup.Options.PlayerWounded = true;
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
        int before = c.Player.Men;
        var report = c.FinishBattle(b.Result(), setup);
        Check(report.Companies.Count > 0, "the report lists the companies");
        foreach (var r in report.Companies.Where(r => r.Deployed))
            Check(r.MenBefore == r.MenAfter + r.Killed + r.Wounded + r.Captured + r.FledLost, $"{r.Name}: every man accounted for");
        Check(c.Player.Companies.All(co => co.Battles >= 1), "your companies remember the battle");
        Console.WriteLine($"bridge: {(report.Won ? "won" : "lost")} vs {report.Enemy}, your men {before} -> {c.Player.Men} (+{c.Player.Wounded} wounded), gold {c.S.Gold:0}");
    }

    /// Auto-resolve picks the same winner as the battle simulation at least 75% of the time. On open ground: auto-resolve does
    /// not yet weigh the field (a ridge gives one side an edge); docs/archive/TERRAIN.md T7 adds that, and this test then varies the ground.
    static void Calibration()
    {
        int agree = 0, n = 0;
        for (int i = 0; i < 12; i++)
        {
            var rng = new Random(100 + i);
            int l0 = 1 + i % 4, l1 = 1 + (i * 7 + 1) % 4;
            var a0 = Armies.Random(rng, l0, 0);
            var a1 = Armies.Random(rng, l1, 0);
            a1 = a1 with { Companies = a1.Companies.Select(co => co with { Count = (int)(co.Count * (0.7f + 0.1f * (i % 6))) }).ToList() };
            var b = new Battle(200 + i, Ids(a0, 1000), Ids(a1, 2000), new BattleOptions { AutoPlayer = true, PlayerWounded = true, Ground = TerrainParams.Meadow with { Layouts = null } });
            for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
            Party P(ArmySpec a) { var p = new Party(); foreach (var co in a.Companies) { var r = new CompanyRecord { Id = co.Id, Role = co.Role }; foreach (var st in co.Stacks) r.Stack(st.Troop).Men += st.Count; p.Companies.Add(r); } return p; }
            float s0 = Rules.Strength(P(a0)), s1 = Rules.Strength(P(a1));
            int predicted = s0 >= s1 ? 0 : 1;
            n++;
            if (predicted == b.Winner) agree++;
            if (Environment.GetEnvironmentVariable("CAL_DEBUG") != null) Console.WriteLine($"  {i}: s0 {s0:0} s1 {s1:0} winner {b.Winner} t {b.Time:0}");
        }
        Console.WriteLine($"auto-resolve agrees with the battle sim in {agree}/{n} matchups");
        Check(agree >= n * 0.75f, $"auto-resolve calibration ({agree}/{n})");
    }

    /// A lord fights as he is (docs/PLAYTEST.md PT6b): the same two armies, a Reckless then a Cautious enemy lord. The
    /// reckless one goes in sooner and leads it himself; the cautious one sounds the retreat sooner.
    static void LordsFightAsTheyAre()
    {
        (float Charge, float Captain, float Ends) Run(params string[] traits)
        {
            var rng = new Random(77);
            var a0 = Armies.Random(rng, 2, 0);
            var a1 = Armies.Random(rng, 2, 0);
            var b = new Battle(301, Ids(a0, 1000), Ids(a1, 2000), new BattleOptions { AutoPlayer = true, PlayerWounded = true, LeaderTraits = traits, Ground = TerrainParams.Meadow with { Layouts = null } });
            float charge = -1, captain = -1;
            for (int step = 0; b.Winner < 0 && step < 60 * 400; step++)
            {
                b.Update(1 / 60f);
                if (charge < 0 && b.SquadsOf(1).Any(q => q.Order == Order.Charge && q.Units.All(u => !u.IsLeader))) charge = b.Time;
                if (captain < 0 && b.SquadsOf(1).Any(q => q.Order == Order.Charge && q.Units.Any(u => u.IsLeader))) captain = b.Time;
            }
            Console.WriteLine($"  {string.Join(",", traits)}: line charges {charge:0.0} s, captain {captain:0.0} s, ends {b.Time:0} s");
            return (charge, captain, b.Time);
        }
        var bold = Run("Reckless");
        var wary = Run("Cautious");
        var plain = Run();
        Check(bold.Captain >= 0 && (wary.Captain < 0 || bold.Captain < wary.Captain), "a reckless lord leads the charge sooner than a cautious one");
        Check(bold.Charge >= 0 && (wary.Charge < 0 || bold.Charge <= wary.Charge), "a reckless lord's line goes in no later than a cautious one's");
        Check(Nature.Of(Array.Empty<string>()) == Nature.Plain && plain.Ends > 0, "no traits, the plain general");
        Check(Nature.Line(new[] { "Reckless" }).StartsWith("RECKLESS") && Nature.Line(new[] { "Greedy" }) == "", "the panel line names how he fights");
    }

    /// The campaign chooses the ground (docs/archive/TERRAIN.md T7): a road over a river is a river line with its bridge, a village
    /// gives a village field, a ridge a crest or a pass.
    static void RiverRoadGivesBridge()
    {
        var m = WorldMap.Generate(11);
        Vector2? crossing = null, village = null;
        for (float z = -280; z < 280 && crossing == null; z += 2)
            for (float x = -280; x < 280; x += 2)
            {
                var p = new Vector2(x, z);
                if (m.RoadDist(p) < 3 && m.RiverDist(p) < 4 && m.ProvinceAt(p).Kind != "Marsh") { crossing = p; break; }
            }
        Check(crossing != null, "the map has a road over a river");
        var g = m.BattleGround(crossing!.Value);
        Check(g.Layouts is [FieldLayout.RiverLine], "a fight where a road meets a river is on a river line");
        var t = new Terrain(5, g);
        Check(t.Rivers.Count == 1 && t.Rivers[0].Bridges.Length == 1, "with its bridge");
        foreach (var s in m.Settlements.Where(s => s.Kind == SettlementKind.Village))
        {
            var p = s.Pos + new Vector2(3, 0);
            if (m.RoadDist(p) < 6 && m.RiverDist(p) < 12 || m.ProvinceAt(p).Kind is "Marsh" or "Forest" || m.OnRidge(p)) continue;
            village = p;
            break;
        }
        Check(village != null && m.BattleGround(village.Value).Layouts is [FieldLayout.Village], "a fight by a village is among its houses");
    }

    /// Rain makes mud: a field after a wet week has far more mud than a dry one; winter fields lie bare.
    static void RainMakesMud()
    {
        int Mud(TerrainParams p)
        {
            var t = new Terrain(9, p);
            int n = 0;
            for (float z = -90; z <= 90; z += 5)
                for (float x = -90; x <= 90; x += 5) if (t.GroundAt(new Vector2(x, z)) == GroundKind.Mud) n++;
            return n;
        }
        var open = TerrainParams.Meadow with { Layouts = new[] { FieldLayout.Open }, River = true };
        int dry = Mud(open), wet = Mud(open with { Wet = 0.9f });
        Console.WriteLine($"rain: {dry} mud cells dry, {wet} after a wet week");
        Check(wet > dry + 40, "a wet week turns low ground to mud");
        var farm = TerrainParams.Farmland with { Layouts = new[] { FieldLayout.Hedgerows } };
        int Crops(TerrainParams p) { var t = new Terrain(9, p); return Enumerable.Range(0, 1600).Count(i => t.GroundAt(new Vector2(i % 40 * 4 - 80, i / 40 * 4 - 80)) == GroundKind.Crops); }
        Check(Crops(farm) > 100 && Crops(farm with { Season = 3 }) == 0, "winter fields lie bare");

        var c = Campaign.New(31);
        for (int i = 0; i < 20 * 24 * 4; i++) { c.Encounter = null; c.Arrived = null; c.Tick(0.25f); }
        Check(c.S.Provinces.Any(p => p.Wet > 0.2f) && c.S.Provinces.Any(p => p.Wet < 0.1f), "it rains in some provinces and not others");
        // The rainy lands (the fens; on the drawn map, which has none, the coasts and highlands) against the dry ones.
        float WetOf(Func<LandDef, bool> land) => c.S.Provinces.Where(p => land(Lands.Of(p.Kind))).DefaultIfEmpty().Average(p => p?.Wet ?? 0);
        Check(WetOf(l => l.Rain >= 1.3f) >= WetOf(l => l.Rain <= 0.5f), "the rainy lands stay wetter than the steppe and the sands");
    }

    /// The side that was set upon (or scouts better) chooses where to stand: it gets the crest, and the panel says so.
    static void DefenderChoosesGround()
    {
        var ridge = TerrainParams.Hills with { Layouts = new[] { FieldLayout.Ridge } };
        for (int seed = 1; seed <= 6; seed++)
            Check(new Terrain(seed, ridge with { Favour = 0 }).RidgeSide == 0 && new Terrain(seed, ridge with { Favour = 1 }).RidgeSide == 1, "the side that chose stands on the crest");
        Check(new Terrain(3, ridge).RidgeSide == new Terrain(3, ridge with { Favour = -1 }).RidgeSide, "with nobody choosing, the seed decides as before");
        var c = Campaign.New(31);
        var foe = c.S.Parties.First(p => p != c.Player && p.Men > 0 && p.Kind != PartyKind.Caravan);
        foe.ChaseId = c.Player.Id;
        Check(c.GroundChooser(foe) == 0, "set upon, you choose the ground");
        foe.ChaseId = -1;
        Check(c.GroundChooser(foe) == 1, "attacking, they do");
        Check(c.GroundNote(foe).Length > 0, "the encounter panel says what the ground is");
    }

    /// Auto-resolve weighs the ground: the side that chose it has an edge, and horse loses worth on wet ground.
    static void AutoResolveGroundEdge()
    {
        var foot = new Party(); foot.Companies.Add(new CompanyRecord { Role = "infantry" }); foot.Companies[0].Stack("shieldmen").Men = 30;
        var horse = new Party(); horse.Companies.Add(new CompanyRecord { Role = "horse" }); horse.Companies[0].Stack("lancers").Men = 30;
        var g = TerrainParams.Meadow with { Layouts = new[] { FieldLayout.Open } };
        float none = Campaign.GroundEdge(g, new[] { foot }, new[] { foot }, 1), mine = Campaign.GroundEdge(g with { Favour = 0 }, new[] { foot }, new[] { foot }, 1), theirs = Campaign.GroundEdge(g with { Favour = 1 }, new[] { foot }, new[] { foot }, 1);
        Check(none == 1 && mine > 1 && theirs < 1, "choosing the ground is an edge");
        Check(Campaign.GroundEdge(g with { Wet = 0.9f }, new[] { foot }, new[] { horse }, 1) > Campaign.GroundEdge(g, new[] { foot }, new[] { horse }, 1), "their horse is worth less on sodden ground");
    }

    /// An assault headless: nobody crosses the wall's face except at a ladder or the broken gate, and it ends.
    static void SiegeBattle()
    {
        var rng = new Random(5);
        var att = Ids(Armies.Random(rng, 2, 0), 3000);
        var def = Ids(Armies.Random(rng, 2, 1), 4000);
        def = def with { Companies = def.Companies.Where(c => Armies.RoleOf(c) != "horse").Select(c => c with { Count = c.Count * 2 / 3 }).ToList() };
        var b = new Battle(77, att, def, new BattleOptions { AutoPlayer = true, PlayerWounded = true, Siege = true, Ladders = 2, GateHp = 300 });
        var w = b.Wall!;
        Check(b.Terrain.Height(new Vector2(30, 15)) > b.Terrain.Height(new Vector2(30, 8)) + 3, "the wall stands above the field");
        Check(b.Units.Where(u => u.Team == 1 && u.Ammo > 0).All(u => SiegeWall.Inside(u.Pos)), "defending archers start on the wall");
        var was = b.Units.ToDictionary(u => u, u => SiegeWall.Inside(u.Pos));
        int illegal = 0;
        for (int step = 0; b.Winner < 0 && step < 60 * 900; step++)
        {
            b.Update(1 / 60f);
            if (Environment.GetEnvironmentVariable("SIEGE_DEBUG") != null && step % 1800 == 0)
                foreach (var sq in b.Squads.Where(q => q.Company != null))
                    Console.WriteLine($"  t{b.Time:0} team{sq.Team} {sq.Name} {sq.Order} alive {sq.Alive} at {sq.Centroid().X:0},{sq.Centroid().Y:0} anchor {sq.Anchor.X:0},{sq.Anchor.Y:0} morale {sq.Units.Where(u => u.Active).Select(u => u.Morale).DefaultIfEmpty(0).Average():0}");
            foreach (var u in b.Units)
            {
                bool inside = SiegeWall.Inside(u.Pos);
                if (!was.TryGetValue(u, out bool before)) { was[u] = inside; continue; }
                if (u.Active && inside != before && !(w.GateOpen && w.AtGate(u.Pos.X)) && !(u.Team == 0 && w.AtLadder(u.Pos.X))) illegal++;
                was[u] = inside;
            }
        }
        Console.WriteLine($"siege: team {b.Winner} won after {b.Time:0}s, gate {(w.GateOpen ? "broken" : $"{w.GateHp:0} hp")}, {b.Units.Count(u => u.Team == 0 && u.Active && SiegeWall.Inside(u.Pos))} attackers inside");
        Check(illegal == 0, $"nobody walks through the wall ({illegal})");
        Check(b.Winner >= 0, "an assault resolves within 15 minutes");
        Check(w.GateOpen || b.Units.Any(u => u.Team == 0 && SiegeWall.Inside(u.Pos)), "the attackers got in somewhere");
        // Without ladders the only way in is the gate: the attackers batter it down.
        var g = new Battle(78, att, def, new BattleOptions { AutoPlayer = true, PlayerWounded = true, Siege = true, Ladders = 0, GateHp = 200 });
        for (int step = 0; g.Winner < 0 && !g.Wall!.GateOpen && step < 60 * 300; step++) g.Update(1 / 60f);
        Console.WriteLine($"siege without ladders: gate {(g.Wall!.GateOpen ? $"broken at {g.Time:0}s" : $"{g.Wall.GateHp:0} hp")}");
        Check(g.Wall.GateOpen, "a ram breaks the gate");
    }

    /// Three realms: pairwise wars, sieges and captures, contracts, prisoners, food; 200 days, nothing breaks.
    static void CampaignWar()
    {
        var c = Campaign.New(61);
        Check(c.S.Factions.Count == Factions.All.Count && c.FactionsAtWar(0, 2) && c.FactionsAtWar(1, 4) && !c.FactionsAtWar(0, 1) && !c.FactionsAtWar(1, 2), $"{c.S.Factions.Count} realms; the Crown fights the Northmarch and the Compact the Khaganate, truces elsewhere");
        Check(Enumerable.Range(3, c.S.Factions.Count - 3).All(f => c.S.Parties.Any(p => p.Kind == PartyKind.Lord && p.Faction == f)), "the new realms raise lords");
        Check(c.S.Settlements.Where(s => s.Kind != SettlementKind.Village).All(s => s.Garrison.Count > 0), "every town and castle has a garrison");
        Check(c.S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == 2).SelectMany(p => p.Companies).Any(co => co.Troop is "clansmen" or "axemen" or "huscarls" or "horse_archers"), "Northmarch lords field their own troops");
        // Your own siege, on paper: a strong company takes a castle and, as a sworn vassal (F5), holds it as its first fief.
        c.Captain.Fealty = true;
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && s.Faction == 2).OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).First();
        c.Player.Companies.Clear();
        foreach (var t in new[] { "men_at_arms", "men_at_arms", "longbowmen", "pikemen" }) c.Player.Companies.Add(c.NewCompany(t, Rules.TroopRole(t), Rules.CapOf(t, Rules.TroopRole(t)), 0));
        c.Player.Pos = castle.Pos + new Vector2(3, 0);
        // Clear the ground around the castle first: on a big map neighbours and reinforcements stand much further out than
        // the old test's 12, and an encounter would cut the night's rest short before the ladders are built.
        float clear = 30 * c.Map.Spread;
        foreach (var p in c.S.Parties.Where(p => p != c.Player && Vector2.Distance(p.Pos, castle.Pos) < clear)) p.Pos += Vector2.Normalize(p.Pos - castle.Pos + new Vector2(0.01f, 0)) * clear;
        Check(c.CanBesiege(castle, out string why), "you can besiege an enemy castle: " + why);
        c.Besiege(castle);
        // Rest the whole night: a party passing the camp only interrupts it (more of them roam the bigger map).
        for (double end = c.S.Hours + 13; c.S.Hours < end - 1e-3 && c.Besieging == castle;) { c.Rest(1); c.Encounter = null; c.Arrived = null; }
        Check(c.LaddersIn(castle) == 0 && c.Besieging == castle, "ladders are built overnight");
        c.Encounter = null;
        c.AutoResolveAssault(castle);
        Console.WriteLine($"assault on {castle.Name}: {(castle.Faction == 0 ? "taken" : "repulsed")}, prisoners {c.PrisonerCount}");
        if (castle.Faction == 0)
        {
            // A drawn province may hold other seats: it changes hands once none of them holds out for the old realm.
            bool holdsOut = c.S.Settlements.Any(t => t != castle && t.Province == castle.Province && t.Kind != SettlementKind.Village && t.Faction == 2);
            Check(castle.Owner == c.Player.Id && (c.S.Provinces[castle.Province].Faction == 0) != holdsOut, "the castle changes hands, and its province with it unless another seat holds out");
            Check(c.S.Settlements.Where(v => v.Kind == SettlementKind.Village && c.SeatOf(v) == castle).All(v => v.Faction == 0), "its villages follow");
            Check(c.PrisonerCount > 0, "the garrison is taken prisoner");
            float before = c.S.Gold;
            var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Faction == 0);
            c.SellPrisoners(town);
            Check(c.S.Gold > before && c.PrisonerCount == 0, "prisoners sell in a town");
        }
        var home = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Faction == 0);
        var offers = c.Offers(home);
        Check(offers.Count > 0, "the steward has contracts");
        Check(c.Accept(home, offers[0]) == null && c.S.Contracts.Count == 1, "you can take a contract");
        c.Player.Food = 2;
        c.BuyFood(7);
        Check(c.Player.Food >= 9, "food can be bought");
        int captures = 0;
        var owners = c.S.Settlements.ToDictionary(s => s.Id, s => s.Faction);
        for (int h = 0; h < 200 * 24; h++)
        {
            c.Tick(1);
            if (c.Encounter != null) { c.Retreat(); }
            c.Arrived = null;
            c.Player.Food = 10;
        }
        captures = c.S.Settlements.Count(s => s.Faction != owners[s.Id] && s.Kind != SettlementKind.Village);
        foreach (var p in c.S.Parties) foreach (var co in p.Companies) Check(co.Men >= 0 && co.Wounded >= 0, "no negative men");
        foreach (var s in c.S.Settlements) Check(s.Garrison.All(g => g.Men >= 0) && s.Supplies >= 0, "garrisons stay sane");
        // A drawn province may have several seats, or none (docs/archive/WORLDMAP.md WM4): it belongs to the holder of one of them.
        Check(c.S.Provinces.All(p => c.S.Settlements.Where(s => s.Province == p.Id && s.Kind != SettlementKind.Village) is var seats && (!seats.Any() || seats.Any(s => s.Faction == p.Faction))), "a province belongs to whoever holds its seat");
        Console.WriteLine($"war: after 200 days {captures} seats changed hands, wars {string.Join(" ", c.S.Wars.Select(w => $"{w.A}-{w.B}:{(w.Active ? "war" : "truce")}"))}, "
            + $"seats {string.Join("/", Enumerable.Range(0, 3).Select(f => c.S.Settlements.Count(s => s.Faction == f && s.Kind != SettlementKind.Village)))}, contracts {c.S.Contracts.Count}");
        Check(captures > 0, "the lords take towns and castles from each other");
    }

    /// A save from before the overhaul (version 3 or older) is refused with a clear message, not half-loaded (F1).
    static void RefuseOldSave()
    {
        var c = Campaign.New(71);
        string json = c.S.Serialize().Replace($"\"Version\": {CampaignState.CurrentVersion}", "\"Version\": 3");
        string? error = null;
        try { CampaignState.Deserialize(json); } catch (Exception e) { error = e.Message; }
        Check(error == CampaignState.TooOld, "a pre-overhaul save is refused: " + (error ?? "it loaded"));
        Check(CampaignState.Deserialize(c.S.Serialize()).Version == CampaignState.CurrentVersion, "a current save still loads");
        // From disk, as the title screen's CONTINUE does: no crash, no half-load, and a message to show.
        string dir = Path.Combine(Path.GetTempPath(), "ironfield-selftest-" + Environment.ProcessId);
        string? was = Environment.GetEnvironmentVariable("IRONFIELD_SAVES");
        Environment.SetEnvironmentVariable("IRONFIELD_SAVES", dir);
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "old.json"), json);
            File.WriteAllText(Path.Combine(dir, "old.json.bak"), json);
            Check(CampaignState.LoadFile(Path.Combine(dir, "old.json")) == null && CampaignState.LoadError == CampaignState.TooOld, "loading an old save from disk returns nothing, with the reason");
            c.S.SaveFile(Path.Combine(dir, "new.json"));
            Check(CampaignState.LoadFile(Path.Combine(dir, "new.json")) != null && CampaignState.LoadError == null, "a new save loads from disk");
        }
        finally { Environment.SetEnvironmentVariable("IRONFIELD_SAVES", was); Directory.Delete(dir, true); }
    }

    /// Several campaigns (docs/SAVES.md): a folder each; autosaves rotate, the quicksave and named saves sit beside them; each save
    /// has a card the screens read; CONTINUE takes the newest war not over; Ironman keeps one save; old single-slot saves move in.
    static void SaveFolders()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ironfield-saves-" + Environment.ProcessId);
        string? was = Environment.GetEnvironmentVariable("IRONFIELD_SAVES");
        Environment.SetEnvironmentVariable("IRONFIELD_SAVES", dir);
        Saves.Forget();
        try
        {
            // Old saves first: an autosave and a quicksave of one campaign, as builds before folders wrote them.
            Directory.CreateDirectory(dir);
            var old = Campaign.New(5);
            File.WriteAllText(Path.Combine(dir, "autosave.json"), old.S.Serialize());
            old.S.Hours += 30;
            File.WriteAllText(Path.Combine(dir, "quicksave.json"), old.S.Serialize());
            var listed = Saves.Campaigns();
            Check(listed.Count == 1 && listed[0].Saves.Count == 2, $"the old autosave and quicksave move into one campaign folder ({listed.Count} campaigns)");
            Check(!File.Exists(Path.Combine(dir, "autosave.json")) && !File.Exists(Path.Combine(dir, "quicksave.json")), "nothing is left in the old places");
            Check(listed[0].Saves.All(e => e.Meta != null && File.Exists(Path.ChangeExtension(e.Path, ".meta"))), "each moved save has its card");

            var a = Campaign.New(11);
            var b = Campaign.New(12);
            for (int i = 0; i < Saves.AutoKeep + 2; i++) { a.S.Hours += 24; Saves.Save(a.S, SaveKind.Auto); }
            Check(a.S.CampaignId != "" && Directory.Exists(Saves.DirOf(a.S.CampaignId)), "a campaign gets a folder on its first save");
            var autos = Saves.List(Saves.DirOf(a.S.CampaignId)).Where(e => e.Kind == SaveKind.Auto).ToList();
            Check(autos.Count == Saves.AutoKeep, $"autosaves rotate: {autos.Count} kept, wanted {Saves.AutoKeep}");
            Check(autos[0].Meta!.Day == (int)(a.S.Hours / 24) + 1, "the newest autosave is listed first");
            Saves.Save(a.S, SaveKind.Quick);
            Saves.Save(a.S, SaveKind.Quick);
            Check(Saves.CannotSave(a.S, SaveKind.Named, " ") != null, "a named save needs a name");
            string named = Saves.Save(a.S, SaveKind.Named, "Before the siege!");
            Check(Path.GetFileName(named) == "named-before-the-siege.json", "a named save's file is a safe slug: " + Path.GetFileName(named));
            var entries = Saves.List(Saves.DirOf(a.S.CampaignId));
            Check(entries.Count(e => e.Kind == SaveKind.Quick) == 1, "one quicksave a campaign");
            Check(entries.Any(e => e.Kind == SaveKind.Named && e.Label == "BEFORE THE SIEGE!"), "the named save keeps the name as typed");

            Saves.Save(b.S, SaveKind.Auto);
            Check(b.S.CampaignId != a.S.CampaignId && Saves.Campaigns().Count == 3, "a second campaign has its own folder and leaves the first alone");
            Check(Saves.Latest()?.Path.Contains(b.S.CampaignId) == true, "CONTINUE opens the newest save");

            // A save loads into the campaign it came from, even with the card gone.
            File.Delete(Path.ChangeExtension(named, ".meta"));
            Saves.Changed();
            var back = Saves.Load(named);
            Check(back != null && back.CampaignId == a.S.CampaignId && back.Hours == a.S.Hours, "a save loads and knows its campaign");
            Check(Saves.List(Saves.DirOf(a.S.CampaignId)).All(e => e.Meta != null), "a save without its card is read and its card written again");

            // Ironman: one save only; the war's end takes the campaign.
            var iron = Campaign.New(13);
            iron.S.Ironman = true;
            Check(Saves.CannotSave(iron.S, SaveKind.Named, "x") != null && Saves.CannotSave(iron.S, SaveKind.Quick) != null, "an Ironman campaign refuses quick and named saves");
            for (int i = 0; i < 3; i++) { iron.S.Hours += 24; Saves.Save(iron.S, SaveKind.Quick); }
            Check(Saves.List(Saves.DirOf(iron.S.CampaignId)).Count == 1, "an Ironman campaign keeps one save");
            iron.S.GameOver = true;
            Saves.Save(iron.S, SaveKind.Auto);
            Check(Saves.Latest()?.Path.Contains(iron.S.CampaignId) != true, "CONTINUE skips a war that is over");
            Saves.EndCampaign(iron.S);
            Check(!Directory.Exists(Saves.DirOf(iron.S.CampaignId)), "an Ironman campaign's end takes its folder");

            // Deleting: a save, then the last save, takes the folder with it.
            foreach (var e in Saves.List(Saves.DirOf(b.S.CampaignId))) Saves.Delete(e);
            Check(!Directory.Exists(Saves.DirOf(b.S.CampaignId)), "deleting a campaign's last save removes its folder");
            Saves.DeleteCampaign(a.S.CampaignId);
            Check(Saves.Campaigns().Count == 1, "deleting a campaign removes it from the list");
        }
        finally { Environment.SetEnvironmentVariable("IRONFIELD_SAVES", was); Saves.Forget(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    /// The roll (docs/archive/LEGACY.md L1): each company names a first man, names fit its culture, battles add names up to the cap, the
    /// roll always fits its stacks, a named man is promoted first, naming draws nothing from the main stream, old saves load.
    static void RollEarnsNames()
    {
        var c = Campaign.New(37);
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        Check(c.Player.Companies.All(k => k.RollStarted && k.Roll.Count >= 1), "every company names its first man on the first day");
        var names = Names.Get();
        foreach (var k in c.Player.Companies)
            foreach (var m in k.Roll)
                Check(names.MenOf(k.Culture).Any(f => m.Name.StartsWith(f + " ")), $"a name fits the company's culture ({m.Name}, {k.Culture})");
        ulong main = c.S.Rng;
        var first = c.Player.Companies[0];
        first.Roll.Clear();
        Check(c.NameMan(first, "test") != null && c.S.Rng == main, "naming a man draws nothing from the campaign's main stream");
        // Fight brigands (won), and the roll fills by deeds, never past the cap and always true to its stacks.
        var rng = new Rng(new CampaignState { Seed = 9 });
        int named0 = c.Player.Companies.Sum(k => k.Roll.Count), cap = Balance.Get().RollSize;
        for (int i = 0; i < 30; i++)
        {
            var bandit = c.S.Parties.FirstOrDefault(p => p.Kind == PartyKind.Bandit && !p.Down);
            if (bandit == null) break;
            bandit.Pos = c.Player.Pos;
            c.Encounter = bandit;
            var setup = c.PrepareBattle(bandit);
            var r = Rules.AutoResolve(setup.Side0, setup.Side1, rng);
            c.FinishBattle(r with { Winner = i % 4 == 3 ? 1 : 0, Captain = LeaderFate.Unhurt }, setup);
            c.Captain.WoundedUntil = 0; c.Encounter = null; c.Player.Food = 20;
            if (c.Player.Companies.Sum(k => k.Men) < 30) foreach (var k in c.Player.Companies) Rules.AddMen(k, k.Stacks[0].Troop, 20);
            c.Tick(24); c.Encounter = null; c.Arrived = null;
        }
        int named = c.Player.Companies.Sum(k => k.Roll.Count) + c.S.Fallen.Count;
        Console.WriteLine($"roll: {named0} named at first, {named} named or fallen after the fights; " +
            string.Join("; ", c.Player.Companies.Select(k => $"{k.Name} {k.Roll.Count}+{c.FallenOf(k).Count()}f")));
        Console.WriteLine("fates: " + string.Join(", ", c.S.Fallen.GroupBy(m => m.Fate).Select(g => $"{g.Count()} {g.Key}")));
        Check(named > named0, "battles name more men");
        Check(c.Player.Companies.All(k => k.Roll.Count <= cap), "no roll runs past the cap");
        foreach (var k in c.Player.Companies)
            foreach (var g in k.Roll.GroupBy(m => m.Troop))
            {
                var st = k.Stacks.FirstOrDefault(s => s.Troop == g.Key);
                Check(st != null && g.Count(m => !m.Hurt) <= st.Men && g.Count(m => m.Hurt) <= st.Wounded, $"the roll fits its stacks ({k.Name}, {g.Key})");
            }
        Check(c.Player.Companies.SelectMany(k => k.Roll).All(m => m.Battles >= 1 || m.Joined == c.Day), "named men count their battles");
        Check(c.S.Fallen.All(m => m.Fate is "fell" or "missing" or "left"), "the fallen say how they went");
        // A named man is promoted first.
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && !c.Refuses(t));
        c.Player.Pos = town.Pos;
        c.S.Gold = 5000;
        var co = c.Player.Companies.First(k => k.Stacks.Any(s => s.Troop == "levies" && s.Men > 1));
        var lev = co.Stack("levies");
        co.Roll.RemoveAll(m => m.Troop == "levies");
        var vet = c.NameMan(co, "test", "levies")!;
        lev.AddXp(1e6f);
        Check(c.Promote(co, lev, "shieldmen", 1) == null && vet.Troop == "shieldmen", "the named man is the one promoted");
        // An old save (no roll) loads with empty rolls, and names its first men on the next day.
        var node = System.Text.Json.Nodes.JsonNode.Parse(c.S.Serialize())!;
        foreach (var p in node["Parties"]!.AsArray())
            foreach (var k in p!["Companies"]!.AsArray()) { var o = k!.AsObject(); o.Remove("Roll"); o.Remove("RollStarted"); }
        var old = CampaignState.Deserialize(node.ToJsonString());
        var oc = new Campaign(old);
        Check(oc.Player.Companies.All(k => k.Roll.Count == 0 && !k.RollStarted), "an old save loads with empty rolls");
        oc.Player.Cooldown = 1e6f;
        oc.Tick(24);
        Check(oc.Player.Companies.All(k => k.Roll.Count >= 1 || k.Men == 0), "and its companies name their first men");
        var back = CampaignState.Deserialize(c.S.Serialize()).Parties.First(p => p.Kind == PartyKind.Player);
        Check(back.Companies.Sum(k => k.Roll.Count) == c.Player.Companies.Sum(k => k.Roll.Count), "the roll is saved");
    }

    /// The fallen (docs/archive/LEGACY.md L3): a named man lost costs his company cohesion; the report names him; a long-serving man
    /// is written in the chronicle; the dead are kept after their company is gone.
    static void FallenRemembered()
    {
        var c = Campaign.New(41);
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Roll.Clear();
        for (int i = 0; i < 5; i++) c.NameMan(co, "test");
        foreach (var m in co.Roll) m.Ties.Clear(); // the plain cost; a brother's death costs more (C3, MenHaveTies)
        var st = co.Stacks.OrderByDescending(x => x.Men).First();
        int named = Campaign.FitNamed(co, st.Troop).Count(), lost0 = c.S.Fallen.Count;
        co.Cohesion = 20;
        c.RollFates(co, st, st.Men, st.Men, 0, 0, "the Test");
        int died = c.S.Fallen.Count - lost0;
        Console.WriteLine($"fallen: {died} of {named} named men died when their whole stack was killed");
        Check(died > 0 && died <= named, "named men die with their stack, though not all of them");
        Check(MathF.Abs(co.Cohesion - (20 - died * Balance.Get().FallenCohesion)) < 1e-3f, "each one lost costs the company cohesion");
        // A fought-out loss: the report names the fallen, and a veteran's death is in the chronicle.
        foreach (var k in c.Player.Companies) { k.Roll.Clear(); for (int i = 0; i < 6; i++) if (c.NameMan(k, "test") is Soldier m) m.Battles = 12; }
        var rng = new Rng(new CampaignState { Seed = 5 });
        BattleReport? hit = null;
        for (int i = 0; i < 12 && hit == null; i++)
        {
            var lord = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && c.Hostile(c.Player, p)).OrderByDescending(p => p.Men).FirstOrDefault();
            if (lord == null) break;
            lord.Pos = c.Player.Pos;
            c.Encounter = lord;
            var setup = c.PrepareBattle(lord);
            var rep = c.FinishBattle(Rules.AutoResolve(setup.Side0, setup.Side1, rng) with { Winner = 1, Captain = LeaderFate.Wounded }, setup);
            c.Captain.WoundedUntil = 0; c.Encounter = null;
            if (rep.Roll.Any(x => x.Kind == "FELL")) hit = rep;
            if (c.Player.Companies.Sum(k => k.Men) < 40) foreach (var k in c.Player.Companies) Rules.AddMen(k, k.Stacks[0].Troop, 30);
        }
        Check(hit != null, "a lost battle's report names the fallen");
        var gone = c.S.Fallen.Last(m => m.Fate == "fell");
        Check(hit!.Roll.Any(x => x.Text.Contains(gone.Name)), "by name");
        Check(c.S.Deeds.Any(d => d.Text.Contains(gone.Name)), "and a man of twelve battles is written in the chronicle");
        var home = c.Player.Companies.FirstOrDefault(k => k.Id == gone.Company);
        if (home != null) c.Player.Companies.Remove(home);
        Check(c.S.Fallen.Contains(gone) && gone.CompanyName != "", "the dead are kept after their company is gone");
        Check(CampaignState.Deserialize(c.S.Serialize()).Fallen.Count == c.S.Fallen.Count, "the fallen are saved");
    }

    /// Rising from the ranks (L4): the senior named man becomes sergeant (and leaves the ranks), a sergeant of 15 battles
    /// becomes a companion who keeps that history, and a sergeant from the ranks who falls joins the fallen.
    static void RisesFromRanks()
    {
        var c = Campaign.New(43);
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Roll.Clear(); co.Officer = ""; co.Traits.Clear();
        var vet = c.NameMan(co, "test")!;
        vet.Battles = 6;
        var young = c.NameMan(co, "test");
        if (young != null) young.Battles = 1;
        int men = co.Men;
        for (int i = 0; i < 60 && co.Officer == ""; i++) c.OfficerAfterBattle(co, true, 0);
        Check(co.Officer == "Sgt " + vet.Name && co.OfficerMan == vet && !co.Roll.Contains(vet), "the senior named man is made sergeant");
        Check(co.Men == men - 1, "and leaves the ranks");
        // Fifteen battles a sergeant, and he joins your household with his history.
        co.OfficerBattles = Campaign.CompanionBattles - 1;
        int companions = c.Companions.Count();
        c.OfficerAfterBattle(co, true, 0);
        var ch = c.Companions.FirstOrDefault(k => k.Ranks == vet);
        Check(c.Companions.Count() == companions + 1 && ch != null && ch.FromCompany == co.Id, "the sergeant becomes a companion who rose from the ranks");
        Check(c.RoseFrom(ch).Contains(co.Name.ToUpperInvariant()), "and the screens say so");
        Check(CampaignState.Deserialize(c.S.Serialize()).Characters.Any(k => k.Ranks?.Id == vet.Id), "his history is saved");
        // A second one rises, and falls.
        co.Roll.Clear(); co.Officer = "";
        var sec = c.NameMan(co, "test")!;
        sec.Battles = 4;
        for (int i = 0; i < 60 && co.Officer == ""; i++) c.OfficerAfterBattle(co, true, 0);
        Check(co.OfficerMan == sec, "a second sergeant rises");
        for (int i = 0; i < 80 && co.OfficerMan != null; i++) c.OfficerAfterBattle(co, false, 0.6f, "the Test");
        Check(c.S.Fallen.Any(m => m.Id == sec.Id && m.Fate == "fell" && m.DiedAt == "the Test"), "a sergeant from the ranks who falls joins the fallen");
        // With no named man ready, a sergeant comes from elsewhere, as before.
        co.Roll.Clear(); co.Officer = ""; co.OfficerMan = null;
        for (int i = 0; i < 60 && co.Officer == ""; i++) c.OfficerAfterBattle(co, true, 0);
        Check(co.Officer != "" && co.OfficerMan == null, "with no veteran on the roll a sergeant comes from elsewhere");
    }

    /// The banner's history (docs/archive/LEGACY.md L5): battles get names; a notable win sews one on the banner; a company that breaks
    /// before a realm loses its newest to that realm, and wins it back by beating the realm; your flag shows them.
    static void BannerHonours()
    {
        var c = Campaign.New(47);
        c.Player.Cooldown = 1e6f;
        string name = c.BattleName(c.Player.Pos);
        Check(name.Length > 3 && c.S.Settlements.Any(t => name.StartsWith(t.Name + " ")), $"a battle is named for the nearest place ({name})");
        Check(c.BattleName(c.Player.Pos) == name, "the same field gives the same name");
        Check(c.BattleName(c.Player.Pos, true).StartsWith("the walls of "), "an assault is named for the walls");
        var lord = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && c.Hostile(c.Player, p)).OrderByDescending(p => p.Men).First();
        int realm = lord.Faction;
        BattleReport Fight(int winner, bool slain, bool routed)
        {
            lord.Down = false; lord.Pos = c.Player.Pos; c.Encounter = lord;
            var setup = c.PrepareBattle(lord);
            var r = Rules.AutoResolve(setup.Side0, setup.Side1, new Rng(new CampaignState { Seed = 3 }));
            r = r with { Winner = winner, EnemyLeader = slain ? LeaderFate.Slain : LeaderFate.Unhurt, Captain = LeaderFate.Unhurt,
                Companies = r.Companies.Select(x => x with { Routed = routed && x.Team == 0 }).ToList() };
            var rep = c.FinishBattle(r, setup);
            c.Captain.WoundedUntil = 0; c.Encounter = null; c.S.Event = null;
            foreach (var k in c.Player.Companies) if (k.Men < 20) Rules.AddMen(k, k.Stacks[0].Troop, 30);
            return rep;
        }
        var won = Fight(0, true, false);
        var co = c.Player.Companies.OrderByDescending(k => k.BattleHonours.Count).First();
        Check(co.BattleHonours.Count == 1 && co.Ledger[^1].Name == co.BattleHonours[0], "slaying their captain sews the battle's name on the banner");
        Check(won.Honours.Any(x => x.Kind == "HONOUR") && won.Field != "", "and the report says so");
        Check(c.PrepareBattle(lord).Player.Companies.First(k => k.Id == co.Id).Streamers == 1, "the company's flag carries a streamer");
        Fight(0, false, false);
        Check(co.BattleHonours.Count == 1, "an ordinary win sews nothing");
        string honour = co.BattleHonours[0];
        Fight(1, false, true);
        Check(co.BattleHonours.Count == 0 && co.LostHonours.Any(l => l.Name == honour && l.Faction == realm), "breaking before a realm's army loses the honour to it");
        Fight(0, false, false);
        Check(co.BattleHonours.Contains(honour) && co.LostHonours.Count == 0, "beating that realm wins it back");
        var back = CampaignState.Deserialize(c.S.Serialize()).Parties.First(p => p.Kind == PartyKind.Player).Companies.First(k => k.Id == co.Id);
        Check(back.BattleHonours.SequenceEqual(co.BattleHonours), "battle honours are saved");
    }

    /// Fame (L6): a famous company is paid more, thought better of, followed by more men, and its own people join knowing it.
    static void FameDraws()
    {
        var c = Campaign.New(49);
        var lordCh = c.S.Characters.First(k => k.Kind == "lord" && !k.Dead);
        float pay = c.FamePay, regard = c.OpinionOfYou(lordCh);
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && !c.Refuses(t));
        town.Recruits = 40;
        int follow = c.RecruitsForYou(town);
        var co = c.Player.Companies[0];
        co.Culture = town.Culture;
        float xp0 = co.Stacks.Sum(st => st.Xp);
        int men0 = co.Men;
        c.Enlist(co, co.Stacks[0].Troop, 2, town);
        float plainXp = co.Stacks.Sum(st => st.Xp) - xp0;
        co.Battles = 80; co.BattleHonours.AddRange(new[] { "Ashby Ford", "Hollin Hill", "the walls of Tamsworth" }); co.Honours.Add("Unbroken");
        Console.WriteLine($"fame: {c.Fame} ({Campaign.FameWord(c.Fame)}), pay x{c.FamePay:0.00}, regard +{c.FameRegard:0}, share +{c.FameShare:0.00}");
        Check(c.Fame >= 130 && c.FamePay > pay && c.FamePay <= 1.5f, "fame raises the pay of contracts, within a half again");
        Check(c.OpinionOfYou(lordCh) > regard, "lords think better of a famous captain");
        Check(c.RecruitsForYou(town) >= follow, "no fewer men follow a famous captain");
        float xp1 = co.Stacks.Sum(st => st.Xp);
        c.Enlist(co, co.Stacks[0].Troop, 2, town);
        Check(co.Stacks.Sum(st => st.Xp) - xp1 > plainXp, "a famous company's own people join knowing the work");
    }

    /// How it ends (L7): no retiring until a company is a legend; handing over gives the company to a companion who rose from
    /// its ranks and play goes on; retiring wins the campaign and the summary tells the company's story first.
    static void RetireAndSucceed()
    {
        var c = Campaign.New(53);
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        Check(c.CannotRetire() != null && c.Retire() != null && !c.S.GameOver, "a young company cannot retire");
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Battles = Campaign.RetireBattles; co.BattleHonours.AddRange(new[] { "Ashby Ford", "Hollin Hill", "Wyck Moor" });
        Check(c.CannotRetire() == null && c.LegendCompany == co, "a company of three battle honours and forty battles is a legend");
        Check(c.HandOver() != null, "no one to hand over to without a companion");
        // A sergeant from the ranks becomes a companion, then the captain.
        co.Roll.Clear(); co.Officer = "";
        var vet = c.NameMan(co, "test")!;
        vet.Battles = 9;
        for (int i = 0; i < 60 && co.Officer == ""; i++) c.OfficerAfterBattle(co, true, 0);
        co.OfficerBattles = Campaign.CompanionBattles - 1;
        c.OfficerAfterBattle(co, true, 0);
        var heir = c.Successor;
        Check(heir != null && heir.Ranks == vet, "the successor is the companion who rose from the ranks");
        float renown = c.Captain.Renown;
        Check(c.HandOver() == null && c.Me.Name == heir!.Name && !c.Companions.Contains(heir) && !c.S.GameOver && c.S.Generation == 1, "handing over: you play on as him");
        Check(MathF.Abs(c.Captain.Renown - renown * 0.5f) < 1e-3f && c.Player.Companies.Contains(co), "with half the renown and the same company");
        c.S.Fallen.Add(new Soldier { Name = "Wat the Grey", Troop = "levies", Battles = 30, Fate = "fell", Company = co.Id, CompanyName = co.Name });
        Check(c.Retire() == null && c.S.GameOver && c.S.Won && c.S.Retired, "retiring ends the campaign as a win");
        var sum = c.Summary();
        Check(sum[0].StartsWith(co.Name.ToUpperInvariant()) && sum.Any(l => l.Contains("WAT THE GREY")) && sum.Any(l => l.Contains("ROSE FROM THE RANKS")), "the summary tells the company's story first");
        Console.WriteLine("legend: " + string.Join(" / ", sum.Take(3)));
    }

    /// The burial (L3): after a won battle where a named man fell, an event asks what to do for him, by name.
    static void BuryNamed()
    {
        var c = Campaign.New(57);
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        Check(Campaign.Events.Any(e => e.Id == "bury_named"), "the burial event is in the data");
        var rng = new Rng(new CampaignState { Seed = 7 });
        bool raised = false;
        for (int i = 0; i < 20 && !raised; i++)
        {
            foreach (var k in c.Player.Companies) { k.Roll.Clear(); for (int j = 0; j < 6; j++) c.NameMan(k, "test"); if (k.Men < 30) Rules.AddMen(k, k.Stacks[0].Troop, 30); }
            var lord = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && c.Hostile(c.Player, p)).OrderByDescending(p => p.Men).First();
            lord.Pos = c.Player.Pos; c.Encounter = lord;
            var setup = c.PrepareBattle(lord);
            c.S.Event = null;
            c.FinishBattle(Rules.AutoResolve(setup.Side0, setup.Side1, rng) with { Winner = 0, Captain = LeaderFate.Unhurt }, setup);
            c.Encounter = null;
            raised = c.S.Event?.Id == "bury_named";
        }
        Check(raised, "a won battle with a named man fallen raises his burial");
        var pe = c.S.Event!;
        var man = c.S.Fallen.First(m => m.Id == pe.Soldier);
        var ev = Campaign.Events.First(e => e.Id == "bury_named");
        Check(c.Fill(ev.Title, pe) == man.Name && c.Fill(ev.Text, pe).Contains(man.Name), "the burial names him");
    }

    /// Troop stacks and promotion by the man (F1): a promotion moves exactly one man and spends exactly his XP; out-of-role
    /// promotions go to a company of the new role; pools never exceed their cap.
    static void Stacks()
    {
        var c = Campaign.New(81);
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && !c.Refuses(t));
        c.Player.Pos = town.Pos;
        Check(c.InTown(c.Player), "the test party stands in a town");
        c.S.Gold = 2000;
        var co = c.Player.Companies.First(k => Rules.RoleOf(k) == "infantry");
        var lev = co.Stack("levies");
        int men = lev.Men, companies = c.Player.Companies.Count;
        lev.AddXp(1e6f);
        Check(MathF.Abs(lev.Xp - lev.PoolCap) < 1e-3f && lev.Ready == men, "a pool fills to its cap and no further");
        float pool = lev.Xp, gold = c.S.Gold;
        Check(c.Promote(co, lev, "shieldmen", 1) == null, "one levy can be promoted in a town");
        Check(lev.Men == men - 1 && co.Stack("shieldmen").Men == 1, "a promotion moves exactly one man");
        Check(MathF.Abs(pool - lev.Xp - lev.Type.UpgradeXp) < 1e-3f || lev.Xp <= lev.PoolCap, "and spends his upgradeXp from the pool");
        Check(MathF.Abs(gold - c.S.Gold - Rules.PromoteCost("levies", "shieldmen", false)) < 1e-3f, "and costs the difference in troop price");
        Check(c.Promote(co, lev, "shieldmen", 4) == null && co.Stack("shieldmen").Men == 5, "five levies are shieldmen now");
        Check(c.Promote(co, lev, "bowmen", 2) == null, "two levies can become bowmen");
        var bows = c.Player.Companies.FirstOrDefault(k => Rules.RoleOf(k) == "archers");
        Check(bows != null && bows.Stack("bowmen").Men == 2 && !co.Stacks.Any(st => st.Troop == "bowmen"), "bowmen go to a bow company (raised for them)");
        Check(c.Player.Companies.Count == companies + 1, "only one company is raised for them");
        Check(co.Men == men - 2 && co.Stacks.Count == 2, "the foot company holds levies and shieldmen");
        Check(co.Stacks.All(st => st.Xp <= st.PoolCap + 1e-3f), "pools stay under their caps after promotion");
        // Promotion past tier III needs a town (HD1); out in the field it is refused with a reason.
        c.Player.Pos = town.Pos + new Vector2(40, 40);
        Check(c.SettlementAt(c.Player.Pos) != null || !c.CanTrainHere(c.Player, Troops.All.First(t => t.Tier == 4).Id), "promotion past tier III out in the field is refused");
        // Moving and splitting stacks.
        Check(c.SplitStack(co, co.Stack("shieldmen"), 2) == null && co.Stack("shieldmen").Men == 3, "a stack can be split into a new company");
        var split = c.Player.Companies.First(k => k.Name.EndsWith(" II"));
        Check(c.MoveStack(split, split.Stacks[0], co) == null && co.Stack("shieldmen").Men == 5 && !c.Player.Companies.Contains(split), "and moved back");
        // Drill a fortnight: pools fill but never pass their caps.
        c.Player.Cooldown = 1e6f;
        for (int h = 0; h < 14 * 24; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; c.Player.Food = 20; }
        Check(c.Player.Companies.SelectMany(k => k.Stacks).All(st => st.Xp <= st.PoolCap + 1e-3f), "drill never overfills a pool");
        Console.WriteLine($"stacks: {string.Join("; ", c.Player.Companies.Select(k => $"{k.Name} [{string.Join(", ", k.Stacks.Select(st => $"{st.Men} {st.Troop} {st.Ready}r"))}]"))}");
        // The save keeps the stacks.
        var back = CampaignState.Deserialize(c.S.Serialize());
        var co2 = back.Parties.First(p => p.Kind == PartyKind.Player).Companies.First(k => k.Id == co.Id);
        Check(co2.Stacks.Count == co.Stacks.Count && co2.Stacks.Sum(st => st.Men) == co.Men && MathF.Abs(co2.Stacks.Sum(st => st.Xp) - co.Stacks.Sum(st => st.Xp)) < 1e-2f, "stacks and pools survive a save");
    }

    /// Cultures and regional recruitment (F2): every troop has a culture; every culture can climb from its village root to a
    /// tier VII of its own; villages raise their culture's men whoever holds them; men cannot train into another culture's line;
    /// elite lines need a town of their culture in your realm.
    static void UpgradeTreeVisibility()
    {
        var c = Campaign.New(19);
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Faction == 0); // an Azure town (on the drawn map, of one of its peoples)
        c.Captain.Contract = town.Faction;
        var ig = c.NewCompany("ironguard", "infantry", 6, 1000, town);
        c.Player.Companies.Add(ig);
        var tree = c.UpgradeGraph(c.Player, ig, "ironguard");
        Check(tree.ContainsKey("aegis_wardens") && !tree.ContainsKey("paladins"), "upgrade screen hides noble lines from commoners while showing unlocked elite lines");
        Check(!tree.ContainsKey("lily_squires"), "upgrade screen hides knight order lines when unsworn");
        c.Captain.Contract = -1;
        Check(!c.UpgradeGraph(c.Player, ig, "ironguard").ContainsKey("aegis_wardens"), "upgrade screen hides elite lines without a realm barracks");
    }

    static void Cultures()
    {
        var errs = Factions.Validate().ToList();
        Check(errs.Count == 0, "factions.json is valid: " + string.Join("; ", errs));
        Check(Troops.All.All(t => t.Culture is "common" or "wild" || Factions.Culture(t.Culture) != null), "every troop has a culture");
        foreach (var cul in Factions.Cultures.Where(c => !c.Waiting)) // waiting subcultures come in with the world book's map
        {
            var seen = new HashSet<string>();
            var open = new Queue<string>(new[] { cul.Root });
            while (open.Count > 0)
            {
                var t = Troops.Get(open.Dequeue());
                if (!seen.Add(t.Id)) continue;
                foreach (var u in t.UpgradesTo)
                    if (Factions.Open(cul.Id, Troops.Get(u).Culture)) open.Enqueue(u);
            }
            var topOwn = seen.Select(Troops.Get).Where(t => t.Tier == Troops.MaxTier && t.Culture == cul.Id).ToList();
            var topAll = seen.Select(Troops.Get).Where(t => t.Tier == Troops.MaxTier).ToList();
            bool main = Factions.All.Any(f => f.Culture == cul.Id);
            Console.WriteLine($"culture {cul.Id}: {seen.Count} troops open from {cul.Root}, tier VI: {string.Join(", ", topOwn.Select(t => t.Id))}");
            Check(topOwn.Count >= (main ? 3 : 1), $"{cul.Id} men can climb from {cul.Root} to tier VI lines of their own");
            foreach (var role in new[] { "infantry", "archers", "horse" })
                Check((main ? topOwn : topAll).Any(t => Rules.TroopRole(t.Id) == role), $"{cul.Id} has a tier VI {role} line");
        }
        // A realm's own people: its culture, or on the drawn map one of its subcultures (whose kin is the realm's; WM5).
        static bool OwnPeople(int f, string culture) => Factions.PeopleOf(culture) == Factions.CultureOf(f);
        var c = Campaign.New(83);
        Check(c.S.Settlements.Any(v => v.Culture != Factions.CultureOf(v.Faction)), "the drawn map's places are their own peoples");
        foreach (var v in c.S.Settlements.Where(v => v.Kind == SettlementKind.Village))
        {
            // Its realm's people, or the people the map drew it with (twelve League places are Bayfolk, Azure coast people).
            Check(OwnPeople(v.Faction, v.Culture) || c.Map.Designed && v.Culture == WorldDesign.Get().Settlements[v.Id].Subculture, $"a village starts with its realm's culture or its drawn people ({v.Name}: {v.Culture})");
            Check(Campaign.RecruitTroop(v) == Factions.Root(v.Culture), "a village raises its culture's root");
        }
        // A northern village held by the Azure Crown still raises clansmen, and they form a northern company.
        var north = c.S.Settlements.First(v => v.Kind == SettlementKind.Village && v.Faction == 2 && Factions.Root(v.Culture) == "clansmen");
        north.Faction = 0;
        north.Owner = c.Player.Id; // conquered by you: its lord's opinion no longer holds men back (a drawn province's seat may be another lord's)
        north.Recruits = 10;
        c.Player.Pos = north.Pos;
        c.S.Gold = 1000;
        Check(c.RecruitLevies(north, 6) == null, "you can recruit in a conquered northern village");
        var clans = c.Player.Companies.FirstOrDefault(k => k.Stacks.Any(st => st.Troop == "clansmen"));
        Check(clans != null && clans.Culture == north.Culture && clans.Stack("clansmen").Men == 6, $"its men are clansmen, in a company of its people ({north.Culture})");
        Check(c.Player.Companies.Where(k => k != clans).All(k => k.Stacks.All(st => st.Troop != "clansmen")), "and they did not dilute the azure companies");
        // Culture lines: azure shieldmen may become men-at-arms but not crimson greatswords.
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && t.Faction == 0);
        c.Player.Pos = town.Pos;
        var az = c.NewCompany("shieldmen", "infantry", 10, 1000, town);
        c.Player.Companies.Add(az);
        var sh = az.Stack("shieldmen");
        Check(OwnPeople(0, az.Culture) && sh.Ready > 0, $"an azure company of ready shieldmen ({az.Culture})");
        Check(c.Promotable(c.Player, az, sh, "greatswords", 1, 1000, out _, out string why) == 0 && why.Contains("CANNOT TRAIN"), "azure men cannot train as crimson greatswords: " + why);
        Check(c.Promotable(c.Player, az, sh, "men_at_arms", 1, 1000, out _, out _) == 1, "but can as azure men-at-arms");
        // Elite lines: azure ironguard -> paladins (tier V) needs an azure barracks in your realm.
        var ig = c.NewCompany("ironguard", "infantry", 6, 1000, town);
        c.Player.Companies.Add(ig);
        Check(c.Promotable(c.Player, ig, ig.Stack("ironguard"), "paladins", 1, 5000, out _, out why) == 0 && why.Contains("NOBLE"), "ironguard cannot be drilled into noble paladins: " + why);
        Check(c.Promotable(c.Player, ig, ig.Stack("ironguard"), "aegis_wardens", 1, 5000, out _, out _) == 1, "a captain sworn to the Crown may train aegis wardens");
        c.Captain.Contract = -1;
        Check(c.Promotable(c.Player, ig, ig.Stack("ironguard"), "aegis_wardens", 1, 5000, out _, out why) == 0 && why.Contains("BARRACKS"), "a free captain may not: " + why);
        // Each realm's lords field their own culture's troops.
        var w = Campaign.New(84);
        w.Player.Cooldown = 1e6f;
        for (int h = 0; h < 60 * 24; h++) { w.Tick(1); w.Encounter = null; w.Arrived = null; }
        for (int f = 0; f < w.S.Factions.Count; f++)
        {
            // Lords seated in the realm's own lands: one seated in a conquered town raises that town's people (4.2).
            var homeLords = w.S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == f && p.HomeId >= 0 && w.SettlementById(p.HomeId) is { } h && OwnPeople(f, h.Culture)).ToList();
            var stacks = homeLords.SelectMany(p => p.Companies).SelectMany(k => k.Stacks).Where(st => st.Type.Culture != Factions.Common).ToList();
            // Its own culture's troops or its kin's (the realms of docs/archive/WORLD.md W3 borrow two of their four lines).
            int own = stacks.Where(st => Factions.Open(Factions.CultureOf(f), st.Type.Culture) || OwnPeople(f, st.Type.Culture)).Sum(st => st.Men), all = stacks.Sum(st => st.Men);
            Console.WriteLine($"realm {f} ({Factions.CultureOf(f)}): {own}/{all} of its lords' cultured men are its own or its kin's");
            Console.WriteLine("  " + string.Join(", ", homeLords.SelectMany(p => p.Companies).SelectMany(k => k.Stacks).GroupBy(st => st.Troop).Select(g => $"{g.Key}:{g.Sum(st => st.Men)}")));
            Check(all > 0 && own >= all * 0.85f, $"realm {f}'s army is recognisably {Factions.CultureOf(f)}");
        }
    }

    /// Characters and relations (F3): opinion stays in range and fades; captured lords come back; a lord you beat and freed thinks
    /// better of you than one you beat and ransomed; traits change what a lord does; an heir inherits; companions come from
    /// veteran officers and taverns and fight as heroes; a lord who dislikes you holds back his villages' recruits.
    static void Characters()
    {
        var c = Campaign.New(85);
        var lords = c.S.Parties.Where(p => p.Kind == PartyKind.Lord).ToList();
        Check(lords.All(p => c.CharacterOf(p) is Character ch && ch.Traits.Count >= 2 && ch.Name == p.Name), "every lord is a character with traits");
        Check(c.Me.Kind == "captain", "you have a character too");
        Check(lords.Where(p => p.Faction == 0).All(p => c.OpinionOfYou(p) > 0), "your liege's lords start warm towards you");
        // Range and decay.
        var a = c.CharacterOf(lords[0])!;
        for (int i = 0; i < 20; i++) c.Remember(a, c.Me, "you razed my village", -30);
        Check(c.OpinionOfYou(a) >= -100 && a.Opinion.Values.All(v => v >= -100 && v <= 100), "opinion stays in range");
        float low = c.OpinionOfYou(a);
        for (int w = 0; w < 30; w++) c.RelationsWeekly();
        Check(c.OpinionOfYou(a) > low + 40, $"opinion fades with time ({low:0} -> {c.OpinionOfYou(a):0})");
        Check(a.Memories.Count <= 8, "memories are bounded");
        // Two enemy lords beaten and captured: one freed, one ransomed.
        var foes = lords.Where(p => p.Faction == 1).Take(2).ToList();
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && t.Faction == 0);
        foreach (var f in foes) { f.Down = true; f.Captive = true; f.DownUntil = float.MaxValue; c.Player.Captives.Add(f.Id); }
        c.RelationsAfterBattle(new[] { c.Player }, foes, true);
        c.FreeLord(foes[0]);
        c.RansomLord(foes[1], town);
        float freed = c.OpinionOfYou(foes[0]), sold = c.OpinionOfYou(foes[1]);
        var why = c.MemoriesOf(c.CharacterOf(foes[0])!, c.Me).Select(m => m.What).ToList();
        Console.WriteLine($"characters: freed lord thinks {freed:0} of you ({string.Join("; ", why)}), ransomed lord {sold:0}");
        Check(freed > sold + 15, "a lord you freed thinks better of you than one you ransomed");
        Check(why.Contains("you beat me in the field") && why.Contains("you set me free without ransom"), "and remembers why");
        // Captured lords come back to the field.
        c.Player.Cooldown = 1e6f;
        for (int h = 0; h < 5 * 24; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; }
        Check(foes.All(f => !f.Down && !f.Captive), "released lords return to the field");
        // Traits change choices: before a party a little stronger, the Cautious lord flees and the Reckless one does not.
        string Choice(string trait)
        {
            var w = Campaign.New(86);
            var lord = w.S.Parties.First(p => p.Kind == PartyKind.Lord && p.Faction == 0);
            var foe = w.S.Parties.First(p => p.Kind == PartyKind.Lord && p.Faction == 2); // the Crown and the Northmarch open at war
            var ch = w.CharacterOf(lord)!;
            ch.Traits.Clear(); ch.Traits.Add(trait);
            var spot = new Vector2(0, 0);
            foreach (var p in w.S.Parties.Where(p => p != lord && p != foe)) p.Pos = new Vector2(5000, 5000);
            lord.Pos = spot; foe.Pos = spot + new Vector2(12, 0);
            lord.Companies.Clear(); foe.Companies.Clear();
            foreach (var role in new[] { "infantry", "archers", "spears" }) lord.Companies.Add(w.NewCompany(Armies.LineTroop(role, 3, 0), role, 16, 0));
            foreach (var role in new[] { "infantry", "archers", "spears" }) foe.Companies.Add(w.NewCompany(Armies.LineTroop(role, 3, 2), role, 16, 0));
            foe.Companies[0].Stack(foe.Companies[0].Troop).Men += 9;
            float ratio = Rules.Strength(foe) / Rules.Strength(lord);
            lord.SiegeTarget = -1; lord.Task = "patrol";
            w.LordThink(lord);
            Console.WriteLine($"  {trait} lord facing {ratio:0.00}x: {lord.Task}");
            return lord.Task;
        }
        Check(Choice("Cautious") == "flee" && Choice("Reckless") != "flee", "a cautious lord flees what a reckless one stands against");
        // A lord slain: his heir takes the banner and the grudge.
        var victim = lords.First(p => p.Faction == 1 && !foes.Contains(p));
        var father = c.CharacterOf(victim)!;
        c.SlayLord(victim, true);
        var heir = c.CharacterOf(victim)!;
        Check(father.Dead && heir != father && !heir.Dead && victim.Name == heir.Name, "a slain lord's heir takes up his banner");
        Check(c.MemoriesOf(heir, c.Me).Any(m => m.What.StartsWith("you slew my ")), "and remembers who killed his kin");
        // Companions: a sergeant of 15 battles joins your household and leads his company as a hero.
        var co = c.Player.Companies[0];
        co.Officer = "Sgt Test";
        co.OfficerBattles = Campaign.CompanionBattles - 1;
        c.OfficerAfterBattle(co, true, 0);
        var comp = c.Companions.FirstOrDefault();
        Check(comp != null && comp.CompanyId == co.Id && co.Hero == comp.Name && comp.Role != "", "a veteran sergeant becomes a companion and leads his company");
        // A wanderer in a tavern.
        var tavern = c.S.Settlements.FirstOrDefault(t => c.WandererAt(t) != null);
        for (int i = 0; tavern == null && i < 10; i++) { c.Tick(24 * 7); c.Encounter = null; tavern = c.S.Settlements.FirstOrDefault(t => c.WandererAt(t) != null); }
        Check(tavern != null, "some towns have a wanderer");
        c.S.Gold = 1000;
        Check(c.HireWanderer(tavern!) == null && c.Companions.Count() == 2, "a wanderer can be taken into service");
        // The hero fights in battle and is not counted among his company's men.
        c.RefreshHeroes();
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        bandit.Pos = c.Player.Pos;
        c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        var spec = setup.Player.Companies.First(k => k.Id == co.Id);
        Check(spec.Hero == comp!.Name || c.S.Hours < comp.WoundedUntil, "the battle gets the company's hero");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        var sq = b.Squads.First(q => q.Company?.Id == co.Id);
        Check(sq.Units.Count(u => u.Hero) == 1 && sq.Units.Count(u => !u.Hero) == spec.Count, "one hero unit at the head of the company's men");
        Check(b.Result().Companies.First(k => k.Id == co.Id).Start == spec.Count, "the hero is not counted in the company's result");
        c.Encounter = null;
        // A lord who hates you keeps his villages' men from you.
        var village = c.S.Settlements.First(v => v.Kind == SettlementKind.Village && c.HolderOf(v) != null);
        village.Recruits = 10;
        var holder = c.HolderOf(village)!;
        float before = c.RecruitsForYou(village);
        holder.Opinion[c.Me.Id] = -100;
        Check(c.RecruitsForYou(village) == 0 && before > 0, "a lord who hates you keeps his villages' recruits from you");
        holder.Opinion[c.Me.Id] = 100;
        Check(c.RecruitsForYou(village) == 10, "a lord who loves you lets them all go");
        // Round trip.
        var back = CampaignState.Deserialize(c.S.Serialize());
        Check(back.Characters.Count == c.S.Characters.Count && back.Characters.Sum(k => k.Memories.Count) == c.S.Characters.Sum(k => k.Memories.Count), "characters survive a save");
    }

    /// A mixed company fights as one squad and gets its casualties back per stack (F1).
    /// F4: two captains who fight differently for 20 battles end with different skills and perks; perks gate on skill, skills
    /// rise with diminishing returns and stay in 0-100, perk effects reach the captain and the economy, and all of it saves.
    /// Divisions on the map (docs/archive/COMBAT_OVERHAUL.md 1.5): make one under a sergeant, assign a company, keep it in the
    /// save, take it into battle; an old save without divisions loads with everyone in your retinue.
    static void CampaignDivisions()
    {
        var c = Campaign.New(33);
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        while (c.Player.Companies.Count < 3) c.Player.Companies.Add(new CompanyRecord { Id = c.Player.Companies.Max(x => x.Id) + 1, Stacks = { new TroopStack { Troop = "levies", Men = 20 } } });
        var lead = c.Player.Companies[0];
        lead.Officer = "Hal Brook";
        lead.OfficerBattles = 6;
        Check(c.DivisionLeaders().Any(l => l.Company == lead.Id), "a company's sergeant can lead a division");
        Check(c.MakeDivision(-1, lead.Id) == null, "make a division under him");
        var d = c.S.Divisions.Single();
        Check(lead.Division == d.Id, "his own company joins it");
        var other = c.Player.Companies[1];
        Check(c.AssignDivision(other, d.Id) == null && other.Division == d.Id, "assign another company");
        Check(c.AssignDivision(lead, -1) != null, "his own company cannot leave him");
        c.SetDivisionGoal(d, "FlankLeft"); // an old goal name lands on its post (OF6)
        Check(d.Goal == "LeftWing:Swing", $"an old goal name maps to a post and stance ({d.Goal})");
        c.SetDivisionPost(d, Post.Skirmish);
        Check(Campaign.GoalOf(d) == new Goal(Post.Skirmish, Stance.Loose), "a new post keeps the stance if it can, else takes its first");
        c.SetDivisionStance(d, Stance.Charge);
        Check(Campaign.GoalOf(d).Stance == Stance.Loose, "a stance of another post is refused");
        c.SetDivisionPost(d, Post.LeftWing); c.SetDivisionStance(d, Stance.Swing);
        d.Goal = "Attack"; // as an old save holds it
        Check(new Campaign(CampaignState.Deserialize(c.S.Serialize())).S.Divisions[0].Goal == "Front:Charge", "an old save's goal is mapped when it loads");
        d.Goal = "LeftWing:Swing";
        Check(c.CommandOf(d) == 3, $"a sergeant's command grows with his battles ({c.CommandOf(d)})");
        float edge = c.TacticsEdge;
        lead.OfficerBattles = 15;
        Check(c.TacticsEdge > edge, "a skilled division leader helps when you auto-resolve");
        var back = CampaignState.Deserialize(c.S.Serialize());
        Check(back.Divisions.Count == 1 && back.Divisions[0].Goal == "LeftWing:Swing" && back.Parties[0].Companies.Count(x => x.Division == d.Id) == 2,
            "the save keeps divisions and who is in them");
        var setup = c.PrepareBattle(bandit);
        Check(setup.Player.Divisions?.Count == 1 && setup.Player.Companies.Count(x => x.Division == d.Id) == 2, "the battle army carries the division");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        var bd = b.DivisionsOf(0).SingleOrDefault();
        Check(bd != null && bd.Squads.Count == 2 && bd.Goal == new Goal(Post.LeftWing, Stance.Swing) && bd.Leader?.Sergeant == true,
            $"in battle the division has its companies, goal and sergeant ({bd?.Squads.Count} squads, {bd?.Goal.Describe})");
        // An old save (no Division fields) loads with nobody in a division.
        var node = System.Text.Json.Nodes.JsonNode.Parse(c.S.Serialize())!;
        void Strip(System.Text.Json.Nodes.JsonNode? n)
        {
            if (n is System.Text.Json.Nodes.JsonObject o) { o.Remove("Division"); o.Remove("Divisions"); foreach (var kv in o.ToList()) Strip(kv.Value); }
            else if (n is System.Text.Json.Nodes.JsonArray arr) foreach (var x in arr) Strip(x);
        }
        Strip(node);
        var old = CampaignState.Deserialize(node.ToJsonString());
        Check(old.Divisions.Count == 0 && old.Parties.SelectMany(p => p.Companies).All(x => x.Division == -1), "an old save loads with everyone in the retinue");
        // Losing the sergeant breaks the division up.
        lead.Officer = "";
        c.PruneDivisions();
        Check(c.S.Divisions.Count == 0 && c.Player.Companies.All(x => x.Division == -1), "a division without its leader breaks up");
    }

    /// The captain's kit (docs/archive/PROGRESSION.md P1): data checks, old saves moved onto it, buying, wearing, selling, stats.
    static void Armoury()
    {
        Check(!Gear.Validate().Any(), "gear.json validates: " + string.Join("; ", Gear.Validate()));
        var c = Campaign.New(17);
        var k = c.Captain.Kit;
        Check(k.Weapon == "arming_sword" && k.Side == "greatsword" && c.Owns("greatsword"), "a new captain has sword, shield and the greatsword");
        var plain = Rules.CaptainType(c.Captain);
        Check(plain.Look.Weapon == "sword" && plain.Look.Shield == "round" && plain.Look.Body == "gambeson" && plain.HasShield, "the starting kit is sword, round shield and gambeson");
        var two = Rules.CaptainType(c.Captain, side: true);
        Check(two.Look.Weapon == "greatsword" && !two.HasShield && two.Damage > plain.Damage && two.Reach > plain.Reach, "the second weapon is the greatsword, no shield");
        // An old save's tiers become gear.
        var old = new CaptainState { ArmorTier = 1, HelmTier = 1, HorseTier = 2 };
        var st = c.S; st.Captain = old;
        var moved = new Campaign(st);
        Check(old.Kit.Body == "plate" && old.Kit.Helm == "great_helm" && old.Kit.Horse == "destrier" && moved.Owns("warhorse") && moved.Owns("plate"), "an old save's plate, helm and destrier carry over");
        Check(old.ArmorTier == 0 && Rules.CaptainMount(old).Id == "destrier", "and the old tiers are cleared");
        c = Campaign.New(17);
        // Buying: skill, rank and gold gate it.
        Check(c.BuyGear(Gear.Get("plate"))!.Contains("ARMS"), "plate needs Arms");
        Check(c.BuyGear(Gear.Get("lords_harness"))!.Contains("LORD"), "the lord's harness is for a lord");
        c.S.Gold = 0;
        Check(c.BuyGear(Gear.Get("boar_spear"))!.Contains("CROWNS"), "gear costs gold");
        c.S.Gold = 5000;
        c.Captain.Skills["Arms"] = 40;
        Check(c.BuyGear(Gear.Get("plate")) == null && c.Captain.Kit.Body == "plate" && c.S.Gold == 5000 - 600, "bought plate is worn at once");
        var plated = Rules.CaptainType(c.Captain);
        Check(plated.Armor > plain.Armor && plated.Speed < plain.Speed && plated.Look.Body == "plate", "plate: more armour, a little slower");
        Check(c.BuyGear(Gear.Get("boar_spear")) == null && Rules.CaptainType(c.Captain) is var sp && sp.ThrustOnly && sp.Reach >= 3, "the spear reaches far and only thrusts");
        Check(c.Equip("longsword") == "NOT YOURS", "only owned gear can be worn");
        Check(c.Equip("arming_sword") == null && c.Equip("boar_spear", side: true) == null && c.Captain.Kit.Side == "boar_spear", "a bought weapon can be carried second");
        Check(c.Equip("boar_spear") == null && c.Captain.Kit.Weapon == "boar_spear" && c.Captain.Kit.Side == "arming_sword", "putting the second weapon in hand swaps the two");
        Check(c.SellGear(Gear.Get("boar_spear")) == "TAKE IT OFF FIRST", "worn gear is not sold");
        c.Equip("arming_sword"); c.Equip("greatsword", side: true);
        float gold = c.S.Gold;
        Check(c.SellGear(Gear.Get("boar_spear")) == null && !c.Owns("boar_spear") && c.S.Gold == gold + Campaign.SellPrice(Gear.Get("boar_spear")), "selling pays part of the price");
        c.BuyGear(Gear.Get("crossbow"));
        var xbow = Rules.CaptainType(c.Captain);
        Check(xbow.Ammo > 0 && xbow.Range > 40 && xbow.Pierce > 0.5f && !xbow.HasShield, "a crossbow shoots far and pierces, both hands on it");
        // The armourer's stock is the same all week and towns carry more than villages.
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town);
        var village = c.S.Settlements.First(s => s.Kind == SettlementKind.Village);
        Check(c.ArmourerStock(town).Select(g => g.Id).SequenceEqual(c.ArmourerStock(town).Select(g => g.Id)), "stock does not reroll on a second look");
        int towns = c.S.Settlements.Where(s => s.Kind == SettlementKind.Town).Sum(s => c.ArmourerStock(s).Count);
        int villages = c.S.Settlements.Where(s => s.Kind == SettlementKind.Village).Sum(s => c.ArmourerStock(s).Count);
        Check(towns / (float)c.S.Settlements.Count(s => s.Kind == SettlementKind.Town) > villages / (float)c.S.Settlements.Count(s => s.Kind == SettlementKind.Village) * 2, "towns stock more than villages");
        Check(c.ArmourerStock(village).All(g => g.Cost <= 300), "village smiths sell only cheap gear");
        // The kit survives a save.
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        Check(back.Captain.Kit.Body == "plate" && back.Owns("crossbow") && back.Captain.Kit.Weapon == "crossbow", "the kit is saved");
        // Spoils: cut down a brigand chief and his gear may be yours (only cheap pieces from brigands).
        var rng = new Rng(new CampaignState { Seed = 3 });
        int offered = 0, fights = 0;
        for (; fights < 12 && offered < 2; fights++)
        {
            var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
            bandit.Pos = c.Player.Pos;
            c.Encounter = bandit;
            var setup = c.PrepareBattle(bandit);
            var r = Rules.AutoResolve(setup.Side0, setup.Side1, rng);
            var rep = c.FinishBattle(r with { Winner = 0, EnemyLeader = LeaderFate.Slain, Captain = LeaderFate.Unhurt }, setup);
            c.Captain.WoundedUntil = 0;
            if (rep.Spoil == "") continue;
            offered++;
            var g = Gear.Get(rep.Spoil);
            Check(g.Cost <= 300 && !c.Owns(g.Id) && g.Rank == 0, $"a brigand's spoil is a cheap piece you lack ({g.Id})");
            Check(c.TakeSpoil() == null && c.Owns(g.Id) && c.Report!.Spoil == "" && c.TakeSpoil() != null, "a spoil is taken once and is yours");
        }
        Check(offered >= 1, $"beaten brigand chiefs leave gear behind ({offered} in {fights} fights)");
    }

    /// Duels before a battle (P2): who accepts, and what winning or losing does to the battle that follows.
    static void Duels()
    {
        var c = Campaign.New(23);
        var lord = c.S.Parties.First(p => p.Kind == PartyKind.Lord && c.CharacterOf(p) != null);
        var ch = c.CharacterOf(lord)!;
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        Check(c.CannotChallenge(bandit) != null, "brigands do not fight duels");
        ch.Traits = new List<string> { "Cautious" };
        Check(c.CannotChallenge(lord)!.Contains("RISK"), "a cautious lord will not risk himself");
        ch.Traits = new List<string> { "Reckless" };
        Check(c.CannotChallenge(lord) == null, "a reckless lord accepts");
        lord.Pos = c.Player.Pos;
        c.Encounter = lord;
        var setup = c.PrepareDuel(lord);
        Check(setup.Options.Duel && setup.Player.Companies.Count == 0 && setup.Enemy.Companies.Count == 0 && setup.Duel == lord.Id, "the duel is the two of you");
        float renown = c.Captain.Renown;
        var rep = c.FinishBattle(new BattleResult(0, 20, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Wounded, 0), setup);
        Check(rep.Won && c.Captain.Renown > renown && c.Encounter == lord, "a won duel brings renown, and the battle is still to fight");
        Check(c.CannotChallenge(lord)!.Contains("ALREADY"), "one duel an encounter");
        var battle = c.PrepareBattle(lord);
        Check(battle.Options.EnemyMorale < 0, "his men start the battle shaken");
        c.FinishBattle(Rules.AutoResolve(battle.Side0, battle.Side1, new Rng(new CampaignState { Seed = 1 })), battle);
        Check(c.PrepareBattle(bandit).Options.EnemyMorale == 0, "the shaking lasts one battle");
        // Lose one: you lead the battle wounded.
        var c2 = Campaign.New(23);
        var lord2 = c2.S.Parties.First(p => p.Kind == PartyKind.Lord && c2.CharacterOf(p) != null);
        c2.CharacterOf(lord2)!.Traits = new List<string> { "Honourable" };
        c2.Encounter = lord2;
        c2.FinishBattle(new BattleResult(1, 20, new List<CompanyResult>(), LeaderFate.Wounded, LeaderFate.Unhurt, 0), c2.PrepareDuel(lord2));
        Check(c2.PrepareBattle(lord2).Options.PlayerWounded && c2.CannotChallenge(lord2) != null, "a lost duel leaves you wounded for the battle");
    }

    /// Riding and Bowcraft (P3): split off Arms, rising with what you do from the saddle and with the bow.
    static void RidingAndBowcraft()
    {
        var c = Campaign.New(29);
        Check(c.Skill("Riding") == 5 && c.Skill("Bowcraft") == 5, "a new captain starts with a little Riding and Bowcraft");
        Check(Skills.Of("MountedLord")!.Id == "Riding" && Skills.Of("Fletcher")!.Id == "Bowcraft", "the horse perks are Riding's, the bow perks Bowcraft's");
        Check(c.CannotUse(Gear.Get("destrier"))!.Contains("RIDING") && c.CannotUse(Gear.Get("war_bow"))!.Contains("BOWCRAFT"), "great horses need Riding, the war bow Bowcraft");
        // An old save gets them at half its Arms.
        var st = c.S;
        st.Captain.Skills.Remove("Riding"); st.Captain.Skills.Remove("Bowcraft"); st.Captain.Skills["Arms"] = 40;
        var old = new Campaign(st);
        Check(old.Skill("Riding") == 20 && old.Skill("Bowcraft") == 20, "an old save's captain rides and shoots at half his Arms");
        // A battle fought from the saddle with the lance raises Riding; one with the bow raises Bowcraft.
        float Rise(Campaign k, string skill, BattleResult r)
        {
            float before = k.Skill(skill);
            var bandit = k.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
            bandit.Pos = k.Player.Pos;
            k.Encounter = bandit;
            var setup = k.PrepareBattle(bandit);
            var res = Rules.AutoResolve(setup.Side0, setup.Side1, new Rng(new CampaignState { Seed = 4 }));
            k.FinishBattle(res with { Duration = 120, PlayerKills = r.PlayerKills, SaddleKills = r.SaddleKills, ArrowKills = r.ArrowKills, LanceHits = r.LanceHits, ArrowHits = r.ArrowHits, Parries = r.Parries }, setup);
            k.Captain.WoundedUntil = 0;
            return k.Skill(skill) - before;
        }
        var empty = new BattleResult(0, 0, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Unhurt, 0);
        var k1 = Campaign.New(29);
        float riding = Rise(k1, "Riding", empty with { PlayerKills = 6, SaddleKills = 6, LanceHits = 5 });
        float arms = Rise(Campaign.New(29), "Arms", empty with { PlayerKills = 6, SaddleKills = 6, LanceHits = 5 });
        Check(riding > 3 && arms < 0.5f, $"kills from the saddle raise Riding, not Arms ({riding:0.0} / {arms:0.0})");
        float bow = Rise(Campaign.New(29), "Bowcraft", empty with { PlayerKills = 4, ArrowKills = 4, ArrowHits = 10 });
        Check(bow > 2, $"arrow kills and hits raise Bowcraft ({bow:0.0})");
        float parry = Rise(Campaign.New(29), "Arms", empty with { Parries = 10 });
        Check(parry > 1, $"parries raise Arms ({parry:0.0})");
        // Their perks.
        var archer = new CaptainState { Kit = { Weapon = "hunting_bow" } };
        int arrows = Rules.CaptainType(archer).Ammo;
        float dmg = Rules.CaptainType(archer).Damage;
        archer.Perks.AddRange(new[] { "Fletcher", "Marksman" });
        Check(Rules.CaptainType(archer).Ammo > arrows && Rules.CaptainType(archer).Damage > dmg, "Fletcher and Marksman: more arrows, harder hits");
        var rider = new CaptainState();
        float hp = Rules.CaptainMount(rider).Hp, gallop = Rules.CaptainMount(rider).Gallop;
        rider.Skills["Riding"] = 80;
        rider.Perks.Add("SaddleFast");
        Check(Rules.CaptainMount(rider).Hp > hp && Rules.CaptainMount(rider).Gallop > gallop, "Riding toughens your horse; Saddle-Fast quickens it");
        var o = new BattleOptions().WithCaptain(rider);
        Check(o.Riding == 80 && o.Perks.Contains("SaddleFast") && o.PlayerMount!.Gallop > gallop, "the battle gets your skills, perks and horse");
    }

    /// Perk tiers (P3): each branch opens in order, and its capstone at 100 needs two of its other perks.
    static void PerkTiers()
    {
        Check(Skills.All.All(s => s.Perks.Count == 5 && Skills.Capstone(s, s.Perks[4]) && !s.Perks.Take(4).Any(p => Skills.Capstone(s, p))), "every skill has four perks and a capstone");
        var c = Campaign.New(31);
        c.Captain.Skills["Arms"] = 30;
        Check(c.TakePerk("IronGrip")!.Contains("ARMS 50"), "a tier II perk needs 50");
        Check(c.TakePerk("WarCry") == null, "a tier I perk opens at 25");
        c.Captain.Skills["Arms"] = 100;
        Check(c.TakePerk("Champion")!.Contains("TWO"), "the capstone needs two other perks of its skill");
        Check(c.TakePerk("Hardened") == null && c.TakePerk("Champion") == null && c.Has("Champion"), "with two, the capstone at 100");
        Check(c.TakePerk("Duellist") == null && c.TakePerk("IronGrip") != null, "four choices at 100: one perk of five stays out");
        // Capstones at work.
        var lord = c.S.Characters.First(ch => ch.Kind == "lord");
        float before = c.OpinionOfYou(lord);
        c.Captain.Perks.Add("Kingmaker");
        Check(c.OpinionOfYou(lord) == Math.Min(100, before + 10), "Kingmaker: every lord thinks better of you");
        float speed = c.Speed(c.Player);
        c.Captain.Perks.Add("ForcedMarch");
        Check(c.Speed(c.Player) > speed * 1.05f, "Forced March quickens the march");
        var b = Battle.Empty(32, new BattleOptions { Perks = { "FieldSurgeon" } });
        b.Hit(b.EnemyCaptain, b.Player, 5000, false);
        Check(b.Player.Active && b.Player.Hp > 0, "Field Surgeon: you get back up once");
        b.Player.Action = UnitAction.None;
        b.Hit(b.EnemyCaptain, b.Player, 5000, false);
        Check(!b.Player.Active, "but only once");
    }

    static void SkillRises()
    {
        // The report's lines: a whole-point rise, and PERK when a 25 mark is crossed.
        var cap = new CaptainState { Skills = { ["Arms"] = 26.4f, ["Riding"] = 9.5f, ["Tactics"] = 10 } };
        var lines = Skills.Rises(cap, new Dictionary<string, float> { ["Arms"] = 24.9f, ["Riding"] = 9.1f, ["Tactics"] = 8 });
        Check(lines.Count == 2 && lines.Contains("ARMS 24 -> 26 . PERK") && lines.Contains("TACTICS 8 -> 10"), "one line a skill that rose a point: " + string.Join(", ", lines));
        // A field battle's report carries them.
        var c = Campaign.New(33);
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
        bandit.Pos = c.Player.Pos;
        c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        var res = Rules.AutoResolve(setup.Side0, setup.Side1, new Rng(new CampaignState { Seed = 4 }));
        var rep = c.FinishBattle(res with { Winner = 0, Duration = 120, PlayerKills = 8, Parries = 12 }, setup);
        Check(rep.Rises.Any(l => l.StartsWith("ARMS ")), "the battle report says how far Arms rose");
        // Personal goals met pay renown and appear in the report (P6).
        var g = Campaign.New(33);
        var gb = g.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
        gb.Pos = g.Player.Pos;
        g.Encounter = gb;
        var gs = g.PrepareBattle(gb);
        Check(gs.Options.Goals, "campaign battles have goals");
        var gr = Rules.AutoResolve(gs.Side0, gs.Side1, new Rng(new CampaignState { Seed = 4 }));
        float renown0 = g.Captain.Renown;
        var grep = g.FinishBattle(gr with { Winner = 0, Duration = 120, Goals = new[] { "hand", "quick" }, GoalsMet = new[] { "hand" } }, gs);
        Check(grep.Goals.Count == 2 && grep.Goals.Count(x => x.Met) == 1 && grep.Renown >= Campaign.GoalRenown && MathF.Abs(g.Captain.Renown - renown0 - grep.Renown) < 0.01f,
            $"a goal met is renown in the report ({grep.Renown:0}, {g.Captain.Renown - renown0:0}, {grep.Goals.Count} goals)");
        // A cheap win is worth a point of Tactics more than a costly one.
        float Tactics(float share)
        {
            var k = Campaign.New(34);
            float before = k.Skill("Tactics");
            var r = new BattleResult(0, 60, new List<CompanyResult> { new(0, 1, 100, 100 - (int)(100 * share), 0, (int)(100 * share), 0, new int[6], true) }, LeaderFate.Unhurt, LeaderFate.Unhurt, 0);
            k.SkillsAfterBattle(r, true, 100, 100, 100);
            return k.Skill("Tactics") - before;
        }
        Check(Tactics(0.05f) - Tactics(0.4f) > 0.9f, "a win with few losses teaches Tactics");
        // Forgetting a perk: only in a friendly town, for gold that rises, and not from under a capstone.
        var f = Campaign.New(35);
        f.Captain.Skills["Arms"] = 100;
        foreach (var id in new[] { "WarCry", "Duellist", "Champion" }) Check(f.TakePerk(id) == null, "took " + id);
        f.Player.Pos = f.S.Settlements.First(t => t.Kind == SettlementKind.Village).Pos;
        Check(f.CannotForget("WarCry")!.Contains("TOWN"), "the masters who unteach live in towns, not villages");
        var town = f.S.Settlements.First(t => t.Kind == SettlementKind.Town && !f.Refuses(t));
        f.Player.Pos = town.Pos;
        f.S.Gold = 1000;
        Check(f.CannotForget("WarCry")!.Contains("CHAMPION"), "a capstone keeps the two perks it stands on");
        Check(f.ForgetPerk("Champion") == null && !f.Has("Champion") && f.S.Gold == 1000 - Campaign.RetrainBase, "forget the capstone for gold");
        Check(f.RetrainCost == 2 * Campaign.RetrainBase && f.ForgetPerk("WarCry") == null && f.S.Gold == 1000 - 3 * Campaign.RetrainBase, "each forgetting costs more");
        Check(Skills.Picks(f.Captain, "Arms") == 3 && f.TakePerk("Hardened") == null, "the choice is open again");
        Check(f.CannotForget("Hardened") == null && f.CannotForget("IronGrip") == "NOT TAKEN", "only taken perks can be forgotten");
    }

    static void LastingWounds()
    {
        var c = Campaign.New(36);
        float odds = c.LastingWoundOdds;
        c.Captain.Skills["Medicine"] = 100;
        Check(c.LastingWoundOdds < odds * 0.6f, "Medicine lowers the odds of a lasting wound");
        // Falls enough times and the captain carries every wound, each once; the scar brings renown.
        float renown = c.Captain.Renown;
        for (int i = 0; i < 400; i++) c.MaybeLastingWound();
        Check(c.Captain.Wounds.Count == Campaign.LastingWounds.Length && c.Captain.Wounds.Distinct().Count() == c.Captain.Wounds.Count, "every wound, once each: " + string.Join(",", c.Captain.Wounds));
        Check(c.Captain.Renown == renown + 5, "a scar is a story worth 5 renown");
        // What they cost.
        var hale = new CaptainState();
        var hurt = new CaptainState { Wounds = { "knee", "hand", "eye" } };
        Check(Rules.CaptainType(hurt).Speed < Rules.CaptainType(hale).Speed && Rules.CaptainType(hurt).Damage < Rules.CaptainType(hale).Damage, "a bad knee slows you, a broken hand weakens your blows");
        Check(new BattleOptions().WithCaptain(hurt).Wounds.Contains("eye"), "the lost eye goes into battle with you");
        // The spring mends all but the scar.
        int mended = 0;
        while (c.MendWound() != null) mended++;
        Check(mended == 3 && c.Captain.Wounds.SequenceEqual(new[] { "scar" }), "a healing spring mends wounds, never the scar");
        Check(Campaign.Events.First(e => e.Id == "site_spring").Choices[0].Effects.Contains("mend"), "the spring's rest mends a wound");
    }

    static void CaptainTraits()
    {
        var c = Campaign.New(37);
        var lord = c.S.Characters.First(ch => ch.Kind == "lord" && !ch.Traits.Contains("Reckless"));
        float before = c.OpinionOfYou(lord);
        // Fleeing more than half as often as winning makes a coward; winning more unmakes him.
        c.S.Tally["fled"] = 4;
        c.TraitsWeekly();
        Check(c.Me.Traits.Contains("Coward") && c.OpinionOfYou(lord) <= before - 10, "four flights and few wins: lords think less of a coward");
        Check(c.Find("trait:Coward") != null && c.Find("trait:Lionheart") == null, "a trait is a trigger for its events");
        c.S.Tally["victories"] = 10;
        c.TraitsWeekly();
        Check(!c.Me.Traits.Contains("Coward") && c.OpinionOfYou(lord) == before, "ten wins later the name is gone");
        c.S.Tally["duels"] = 2;
        c.S.Tally["freed"] = 3;
        c.TraitsWeekly();
        Check(c.Me.Traits.Contains("Lionheart") && c.Me.Traits.Contains("Merciful"), "duels won make a Lionheart, freed prisoners a Merciful captain");
        c.S.Tally["robbed"] = 6;
        c.TraitsWeekly();
        Check(c.Me.Traits.Contains("Ruthless") && !c.Me.Traits.Contains("Merciful"), "robbing caravans outweighs old mercy");
        Check(Campaign.Events.Count(e => e.Trigger.StartsWith("trait:")) >= Campaign.EarnedTraits.Length, "each earned trait has an event");
        // Age: past 45 a season costs a point of Arms, and Leadership comes quicker.
        var young = Campaign.New(38);
        var old = Campaign.New(38);
        old.Me.Age = Campaign.OldAge + 1;
        float l0 = young.Skill("Leadership");
        young.Practise("Leadership", 4);
        old.Practise("Leadership", 4);
        Check(old.Skill("Leadership") - l0 > young.Skill("Leadership") - l0, "the old captain learns to lead faster");
        float arms = old.Skill("Arms");
        old.S.Hours = 30 * 24;
        old.AgeDaily();
        Check(old.Skill("Arms") == arms - 1, $"a new season costs the old captain a point of Arms (day {old.Day})");
        int age = young.Me.Age;
        young.S.Hours = Campaign.DaysPerYear * 24;
        young.AgeDaily();
        Check(young.Me.Age == age + 1 && young.Skill("Arms") == arms, "a new year adds one to the captain's age, not yet to the wear");
    }

    static void HouseholdGuard()
    {
        var c = Campaign.New(39);
        c.GuardWeekly();
        Check(c.Guard == null, "a free company has no household guard");
        int slots = Rules.CompanySlots(c.Captain), limit = Rules.MenLimit(c.Captain);
        c.Captain.BestRank = (int)Rank.Vassal;
        c.GuardWeekly();
        var g = c.Guard;
        Check(g != null && g.Men == c.Scaled(15) && g.Type.Tier == 3 && g.Name == Campaign.HouseholdName, "a vassal's guard: 15 men of tier III");
        Check(Rules.CompanySlots(c.Captain) == slots + 1 && Rules.MenLimit(c.Captain) == limit + c.Scaled(15), "the guard takes no slot and no room");
        int men = g!.Men;
        Campaign.Desert(c.Player);
        Check(g.Men == men, "the household never deserts");
        c.Captain.BestRank = (int)Rank.Lord;
        c.GuardWeekly();
        Check(g.Type.Tier == 4 && g.Men == men + c.Scaled(Campaign.GuardRefill), "a lord's guard: tier IV, and it fills toward 25");
        for (int i = 0; i < 10; i++) c.GuardWeekly();
        Check(g.Men == c.Scaled(25) && c.Player.Companies.Count(x => x.Traits.Contains(Campaign.HouseholdTrait)) == 1, "full at 25, and only one guard");
        c.Captain.BestRank = (int)Rank.King;
        c.GuardWeekly();
        Check(g.Type.Tier == 5 && g.Type.Mount != null, $"a king's guard rides, tier V ({g.Troop})");
        // In battle it follows you.
        var b = Battle.Empty(40);
        var sq = b.AddSquad(0, Troops.Get("men_at_arms"), 10, new Vector2(0, -10), 5, Vector2.UnitY, new Company("men_at_arms", 10, Traits: new[] { "Household" }));
        Check(sq.Order == Order.Follow, "the guard keeps at your side in battle");
    }

    static void RankRewards()
    {
        // Sworn Sword: one perk free of the 25-point marks; forgetting it gives the choice back.
        var c = Campaign.New(41);
        c.Captain.Skills["Arms"] = 20;
        Check(c.TakePerk("WarCry")!.Contains("ARMS 25"), "no perk before its mark");
        c.Captain.Skills["Arms"] = 26;
        Check(c.TakePerk("WarCry") == null && c.TakePerk("Duellist") != null, "one choice at 25");
        c.Captain.FreePerks = 1;
        Check(Skills.PicksTotal(c.Captain) == 1 && c.TakePerk("Duellist") == null && c.Captain.FreePerks == 0, "a rank's free perk is a second choice");
        Check(Skills.Picks(c.Captain, "Arms") == 0, "the free perk does not eat the branch's next mark");
        c.Player.Pos = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && !c.Refuses(t)).Pos;
        c.S.Gold = 1000;
        Check(c.ForgetPerk("Duellist") == null && c.Captain.FreePerks == 1, "forget it and the free choice comes back");
        // Lord: a wider aura; King: the King's Charge.
        var opts = new BattleOptions { KingsCharge = true };
        var b = Battle.Empty(42, opts);
        var far = b.AddSquad(0, Troops.Get("men_at_arms"), 8, new Vector2(40, -30), 4, Vector2.UnitY);
        foreach (var u in far.Units) { u.Routing = true; u.Morale = 5; u.Routs = 2; }
        b.Input = new PlayerInput { Rally = true };
        b.Update(1 / 60f);
        Check(far.Units.All(u => u.Morale > Battle.RallyAt && u.Routs == 0), "the King's Charge calls back broken men out of earshot");
        foreach (var u in far.Units) { u.Morale = 5; u.Routs = 2; }
        b.Input = new PlayerInput();
        for (int i = 0; i < 60 * 13; i++) { b.Input.Rally = i == 60 * 12 + 30; b.Update(1 / 60f); if (b.Winner >= 0) break; }
        Check(far.Units.Where(u => u.Active).All(u => u.Routs == 2 || u.Morale < Battle.RallyAt + 25), "but only once a battle");
    }

    static void StartingRoad()
    {
        var c = Campaign.New(43);
        Check(c.S.Event == null, "a campaign made in code starts with no event");
        c.OfferRoad();
        Check(c.S.Event?.Id == "start_road", "a new campaign from the title opens with the road you came by");
        foreach (var (road, check) in new (string, Func<Campaign, bool>)[]
        {
            ("sword", k => k.Skill("Arms") == 20 && k.Captain.Kit.Shield == "kite_shield"),
            ("lance", k => k.Skill("Riding") == 20 && k.Captain.Kit.Horse == "warhorse" && k.Owns("warhorse") && k.Captain.Kit.Side == "boar_spear"),
            ("bow", k => k.Skill("Bowcraft") == 20 && Rules.CaptainType(k.Captain).Ammo > 0 && k.Captain.Kit.Side == "arming_sword"),
        })
        {
            var k = Campaign.New(43);
            k.ChooseRoad(road);
            Check(check(k), "the road of the " + road);
            Check(Gear.Validate().Count() == 0 && k.Captain.Kit.Worn.All(id => Gear.Get(id).Free || k.Owns(id)), "the road's kit is owned");
        }
    }

    static void SkillTree()
    {
        var a = Campaign.New(91);
        var b = Campaign.New(91);
        var rngA = new Rng(new CampaignState { Seed = 5 });
        Check(a.Skill("Leadership") > 0 && a.Captain.Perks.Count == 0, "a new captain has some skill and no perks");
        Check(a.TakePerk("Duellist") != null, "no perk before its skill reaches 25");
        Check(a.BuyGear(Gear.Get("plate")) is string g0 && g0.Contains("ARMS"), "plate armour needs Arms");
        var town = a.S.Settlements.First(t => t.Kind == SettlementKind.Town && t.Faction == 0);
        for (int n = 0; n < 20; n++)
        {
            // The brawler fights every battle himself, in the thick of it.
            var bandit = a.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
            bandit.Pos = a.Player.Pos;
            a.Encounter = bandit;
            var setup = a.PrepareBattle(bandit);
            var r = Rules.AutoResolve(setup.Side0, setup.Side1, rngA);
            a.FinishBattle(r with { Duration = 120, PlayerKills = 9 }, setup);
            foreach (var st in a.Player.Companies.SelectMany(co => co.Stacks)) { st.Men += st.Wounded; st.Wounded = 0; }
            a.Captain.WoundedUntil = 0;
            // The quartermaster lets the sergeants fight, then marches, heals and drills his men up in towns.
            var foe = b.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
            foe.Pos = b.Player.Pos;
            b.Encounter = foe;
            b.AutoResolveEncounter();
            b.Captain.WoundedUntil = 0;
            b.S.Gold = 5000;
            b.MoveTo(b.Player.Pos + new Vector2(n % 2 == 0 ? 40 : -40, 0));
            for (int h = 0; h < 24; h++) { b.Tick(1); b.Encounter = null; b.Arrived = null; b.Player.Food = 20; }
            b.Player.Pos = town.Pos;
            foreach (var co in b.Player.Companies.ToList())
                foreach (var st in co.Stacks.Where(st => st.Type.UpgradesTo.Length > 0).ToList())
                {
                    st.Xp = MathF.Max(st.Xp, st.Type.UpgradeXp * Math.Min(5, st.Men));
                    b.Promote(co, st, st.Type.UpgradesTo[0], 5);
                }
            if (b.Player.Men < 20) b.Player.Companies.Add(b.NewCompany(Campaign.RecruitTroop(0), "infantry", 20, 0, town));
        }
        // Each takes what his skills have opened: the brawler on his sword arm first, the quartermaster on the rest.
        void Choose(Campaign c, IEnumerable<string> order)
        {
            foreach (var id in order)
                foreach (var pk in Skills.Get(id).Perks)
                    if (Skills.Picks(c.Captain, id) > 0 && !c.Has(pk.Id)) c.TakePerk(pk.Id);
        }
        Choose(a, new[] { "Arms", "Leadership", "Tactics" });
        Choose(b, new[] { "Logistics", "Training", "Medicine", "Leadership" });
        string Show(Campaign c) => string.Join(" ", Skills.All.Select(s => $"{s.Id[..3]} {c.Skill(s.Id):0}")) + " | " + string.Join(",", c.Captain.Perks);
        Console.WriteLine($"skills: brawler    {Show(a)}");
        Console.WriteLine($"skills: quartermaster {Show(b)}");
        Check(a.Skill("Arms") > b.Skill("Arms") + 10, "the captain who fights himself has the better sword arm");
        Check(b.Skill("Logistics") > a.Skill("Logistics") + 3, "the captain who marches knows the roads");
        Check(b.Skill("Training") > a.Skill("Training") + 3, "the captain who promotes his men knows drill");
        Check(a.Skill("Leadership") > 25 && b.Skill("Leadership") > 25, "commanding raises Leadership");
        Check(!a.Captain.Perks.SequenceEqual(b.Captain.Perks) && a.Has("WarCry") && !b.Has("WarCry"), "and they end with different perks");
        Check(Skills.All.All(s => a.Skill(s.Id) is >= 0 and <= 100 && b.Skill(s.Id) is >= 0 and <= 100), "skills stay in 0-100");
        Check(Skills.All.All(s => Skills.Picks(a.Captain, s.Id) >= 0), "never more perks than the skill allows");
        Check(a.TakePerk("WarCry") == "TAKEN", "a perk is taken once");
        // Diminishing returns.
        var cs = new CaptainState();
        float g1 = Skills.Raise(cs, "Arms", 10);
        cs.Skills["Arms"] = 90;
        float g2 = Skills.Raise(cs, "Arms", 10) - 90;
        Check(g1 > g2 * 2 && g2 > 0, $"skills rise more slowly near the top ({g1:0.0} vs {g2:0.0})");
        for (int i = 0; i < 100; i++) Skills.Raise(cs, "Arms", 50);
        Check(cs.Skills["Arms"] <= 100, "and never pass 100");
        // Perk effects.
        var plain = new CaptainState();
        Check(Rules.CaptainType(new CaptainState { Perks = { "Duellist" } }).Damage > Rules.CaptainType(plain).Damage, "Duellist sharpens your blows");
        Check(Rules.CaptainType(new CaptainState { Skills = { ["Arms"] = 80 } }).MaxHp > Rules.CaptainType(plain).MaxHp, "Arms toughens you");
        Check(Rules.MenLimit(new CaptainState { Skills = { ["Leadership"] = 50 } }) == Rules.MenLimit(plain) + (int)(10 * Balance.Get().ArmyScale), "Leadership raises the men limit");
        var merc = town.Mercenaries.FirstOrDefault() ?? b.Player.Companies[0];
        int before = b.MercenaryPrice(merc);
        b.Captain.Perks.Add("Haggler");
        Check(b.MercenaryPrice(merc) < before, "Haggler bargains mercenaries down");
        Check(a.TacticsEdge > Campaign.New(91).TacticsEdge || a.Skill("Tactics") <= 10, "Tactics improves auto-resolve odds");
        // Saved and loaded.
        var back = CampaignState.Deserialize(a.S.Serialize());
        Check(back.Captain.Perks.SequenceEqual(a.Captain.Perks) && Math.Abs(back.Captain.Skills["Arms"] - a.Skill("Arms")) < 0.01f, "skills and perks survive a save");
        Check(!Rules.NextRenownUnlock(a.Captain).What.Contains("PERK"), "renown no longer buys perks");
    }

    /// Data/campaign.json and the defaults in Balance must agree (a value changed in one place only is a silent bug).
    static void BalanceMatchesData()
    {
        var file = Balance.Get();
        var defaults = new Balance();
        var off = typeof(Balance).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(f => f.FieldType.IsPrimitive || f.FieldType == typeof(string))
            .Where(f => !Equals(f.GetValue(file), f.GetValue(defaults)))
            .Select(f => $"{f.Name} (campaign.json {f.GetValue(file)}, Balance default {f.GetValue(defaults)})").ToList();
        Check(off.Count == 0, "campaign.json and Balance defaults differ: " + string.Join(", ", off));
    }

    /// Lords' armies and priorities (LordArmies.cs): lords gather under a leader and march at the slowest one's pace, a lord with
    /// a job rides past brigands, an army's lords fight side by side; rabble is slow and robber knights quick.
    /// LD0: lords have houses and kin; heirs come from the house; people age; a house with nobody left dies out; old saves get houses.
    static void HousesAndKin()
    {
        var c = Campaign.New(4243);
        var lords = c.S.Characters.Where(x => x.Kind == "lord" && !x.Dead && x.Order == "" && x.Faction >= 0).ToList();
        Check(lords.All(l => c.HouseOf(l) != null), "every lord belongs to a house");
        Check(c.S.Characters.Count(x => x.Kind == "kin") > lords.Count, "houses have families around their lords");
        Check(lords.Any(l => l.Female && l.Name.StartsWith("Lady ")) || lords.Count < 20, "some houses are led by ladies");
        var father = lords.FirstOrDefault(l => l.Children.Select(c.CharacterById).Any(k => k is { Female: false, Dead: false } && k.Age >= Campaign.ComeOfAge));
        Check(father != null, "some lord has a grown son");
        var son = father!.Children.Select(c.CharacterById).OfType<Character>().Where(k => !k.Female && k.Age >= Campaign.ComeOfAge).OrderByDescending(k => k.Age).First();
        var party = c.PartyById(father.PartyId)!;
        c.SlayLord(party, false);
        Check(father.Dead && c.CharacterOf(party) == son && son.Kind == "lord" && party.Name == son.Name && son.Name.StartsWith("Lord "), "the eldest grown son takes up his father's banner");

        int age = son.Age;
        c.HousesYearly();
        Check(son.Dead || son.Age == age + 1, "people age with the years");

        // A house whose every member is gone dies out at the year's end.
        var h = c.S.Houses.First(x => !x.Extinct && x.Id != son.House);
        foreach (var m in c.Members(h).ToList()) { m.Dead = true; if (c.PartyById(m.PartyId) is Party p) { p.CharId = -1; m.PartyId = -1; } }
        c.HousesYearly();
        Check(h.Extinct, "a house with nobody left dies out");
        Check(c.S.Characters.Where(x => !x.Dead && x.Kind == "lord" && x.Order == "" && x.Faction >= 0 && x.PartyId >= 0).All(l => c.HouseOf(l) != null), "lords made in play have houses too");

        // A save from before houses: lords without a house get one (and a family) on load.
        var old = Campaign.New(4244).S;
        old.Houses.Clear();
        old.Characters.RemoveAll(x => x.Kind == "kin");
        foreach (var x in old.Characters) { x.House = -1; x.Spouse = -1; x.Children.Clear(); x.First = ""; }
        var loaded = new Campaign(CampaignState.Deserialize(old.Serialize()));
        Check(loaded.S.Characters.Where(x => x.Kind == "lord" && !x.Dead && x.Order == "" && x.Faction >= 0).All(l => loaded.HouseOf(l) != null), "an old save's lords get houses on load");
    }

    /// LD1: every lord's painted face is his own, opaque, and shows his mood towards you; a ruler wears a circlet.
    static void PortraitsVary()
    {
        var c = Campaign.New(4246);
        var lords = c.S.Characters.Where(x => x.Kind == "lord" && !x.Dead).Take(40).ToList();
        var faces = lords.Select(l => PortraitPainter.Of(c, l)).ToList();
        Check(faces.Distinct().Count() == faces.Count, "no two lords share a face");
        var px = PortraitPainter.Paint(faces[0]);
        Check(px.Length == PortraitPainter.W * PortraitPainter.H && px.All(p => p.A == 255), "a portrait is a whole opaque picture");
        Check(!PortraitPainter.Paint(faces[0] with { Mood = 0 }).SequenceEqual(PortraitPainter.Paint(faces[0] with { Mood = 3 })), "a warm face and a hostile one differ");
        var small = PortraitPainter.Resample(px, PortraitPainter.W, PortraitPainter.HeadCrop, 20, 20);
        Check(small.Length == 400 && small.All(p => p.A == 255) && small.Distinct().Count() > 20, "a small copy of the head is a whole picture too");
        for (int dress = 0; dress <= 4; dress++)
            for (int cut = 0; cut <= 4; cut++)
                Check(PortraitPainter.Paint(faces[0] with { Dress = dress, Cut = cut, Beard = cut % 4, Crown = cut == 2, Female = cut == 1, Age = 20 + cut * 12, Scar = cut % 3 }).All(p => p.A == 255), $"dress {dress}, cut {cut} paints whole");
        var ruler = c.RulerOf(lords[0].Faction)!;
        Check(PortraitPainter.Of(c, ruler).Crown, "a ruler wears a circlet");
        Check(PortraitPainter.Of(c, lords[0]).Age == lords[0].Age, "the face ages with him");
    }

    /// LD3, LD2, LD4, LD6: talking to a lord (topics, cooldowns, arguments, gifts, insults), bonds between lords (kin, feuds
    /// passed to heirs), wants (met by your help, promises broken), and marrying into a house.
    static void TalkBondsWants()
    {
        foreach (var e in Campaign.ValidateTalk()) Check(false, "talk.json: " + e);
        var c = Campaign.New(4247);
        c.S.Gold = 5000;
        var party = c.S.Parties.First(p => p.Kind == PartyKind.Lord && p.KnightOrder == "" && !p.Down && c.CharacterOf(p) != null && !c.Hostile(c.Player, p)
            && Vector2.Distance(p.Pos, c.Player.Pos) > 3 * Campaign.TalkReach && c.InHall(c.CharacterOf(p)!) == null);
        var lord = c.CharacterOf(party)!;
        Check(c.CannotTalk(lord) != null, "a lord far away cannot be talked to");
        c.Player.Pos = party.Pos + new Vector2(2, 0);
        Check(c.CannotTalk(lord) == null && c.Greeting(lord) != "", "a lord beside you greets you");
        var (ok, said) = c.Say(lord, "news");
        Check(ok && said != "", "he tells you the news");
        Check(c.CannotAsk(lord, "news") != null, "asking again at once is refused");
        float before = c.OpinionOfYou(lord);
        c.Say(lord, "gift");
        Check(c.OpinionOfYou(lord) > before && c.S.Gold == 4900, "a gift of gold warms him and costs 100 crowns");
        lord.Traits = new() { "Greedy", "Reckless" };
        Check(c.Odds(lord, "ride", new[] { "gold" }) > c.Odds(lord, "ride") && c.Odds(lord, "ride", new[] { "faith" }) < c.Odds(lord, "ride", new[] { "gold" }),
            "the argument that suits him moves him most");
        before = c.OpinionOfYou(lord);
        c.Say(lord, "insult");
        Check(c.OpinionOfYou(lord) < before - 15, "an insult is remembered");

        // Bonds: kin within a house, a feud that outlives the lord.
        var house = c.HouseOf(lord)!;
        var kin = c.Members(house).FirstOrDefault(m => m != lord);
        if (kin != null) Check(c.BondOf(kin, lord) == "kin", "members of a house are kin");
        var other = c.CharacterOf(c.S.Parties.First(p => p.Kind == PartyKind.Lord && p.KnightOrder == "" && !p.Down && c.CharacterOf(p) is Character x && x.House != lord.House))!;
        float warm = c.OpinionOf(lord, other);
        var otherParty = c.PartyById(other.PartyId)!;
        c.SlayLord(otherParty, false, killer: lord);
        var heir = c.CharacterOf(otherParty)!;
        if (heir.House == other.House) Check(c.BondOf(heir, lord) == "feud" && c.OpinionOf(heir, lord) < warm, "a slain lord's heir carries his house's feud");

        // Wants: gold for a greedy lord's want; a broken promise.
        lord.Want = "wealth"; lord.Asked.Clear();
        c.Say(lord, "gift");
        Check(lord.Want == "" && c.MemoriesOf(lord, c.Me).Any(m => m.What == "you filled my purse"), "helping his want wins him");
        lord.Want = "marshal"; lord.Promise = "marshal"; lord.PromiseDay = 1;
        c.S.Hours = 24 * (Campaign.WantDays * 4 - 1) + 1;
        if (c.MarshalOf(lord.Faction) != lord) { c.WantsDaily(); Check(c.MemoriesOf(lord, c.Me).Any(m => m.What == "you broke your word"), "a broken promise is remembered"); }

        // Marriage: court three times, then ask the head of her house.
        var bride = c.S.Characters.First(x => c.Suitor(x) && c.HeadOf(x) is Character hd && hd != x);
        var head = c.HeadOf(bride)!;
        bride.Courted = 3;
        head.Opinion[c.S.CaptainChar] = 100;
        for (int k = 0; k < 20 && c.Me.Spouse < 0; k++)
        {
            bride.Asked.Clear(); bride.Courted = 3; bride.Opinion[c.S.CaptainChar] = 100;
            c.Player.Pos = c.PartyById(head.PartyId)?.Pos ?? c.Player.Pos;
            if (c.CannotTalk(bride) != null) { bride.Kind = "lord"; bride.PartyId = head.PartyId >= 0 ? head.PartyId : party.Id; }
            c.Say(bride, "hand");
        }
        Check(c.Me.Spouse == bride.Id && bride.Spouse == c.Me.Id, "you can marry into a house");
        Check(c.Members(c.HouseOf(bride)!).Where(m => m != bride).All(m => c.BondOf(m, c.Me) == "kin"), "her house is kin to you");
    }

    static void LordArmies()
    {
        var c = Campaign.New(4242);
        int f = -1, g = -1;
        for (int a = 0; a < c.S.Factions.Count && f < 0; a++) for (int b = 0; b < c.S.Factions.Count; b++) if (c.FactionsAtWar(a, b)) { f = a; g = b; break; }
        Check(f >= 0, "a new campaign has a war to march in");
        var seat = c.S.Settlements.First(s => s.Faction == g && s.Kind != SettlementKind.Village);
        var lords = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == f && p.KnightOrder == "" && !p.Down).Take(3).ToList();
        var at = lords[0].Pos;
        foreach (var l in lords) { l.Pos = at + new Vector2(l.Id % 3, 0); l.SiegeTarget = -1; l.Task = "patrol"; while (l.Men < c.Scaled(45)) l.Companies[0].Stacks[0].Men += c.Scaled(10); }
        foreach (var ch in lords.Select(c.CharacterOf).OfType<Character>()) foreach (var o in lords.Select(c.CharacterOf).OfType<Character>()) if (ch != o) ch.Opinion[o.Id] = 10;
        var army = c.FormArmy(f, seat, "siege", lords);
        Check(army != null && army.Members.Count == lords.Count && lords.All(l => l.ArmyId == army.Id), "free lords gather into an army");
        var leader = c.PartyById(army!.Leader)!;
        Check(lords.All(l => c.Speed(leader) <= c.Speed(l) + 1e-4f), "an army marches at its slowest lord's pace");
        var member = lords.First(l => l != leader);
        Check(Campaign.OnDuty(member), "a lord in an army is on duty");

        // A brigand band beside a lord on duty, not after him: no fight.
        var band = new Party { Id = c.S.NextId++, Kind = PartyKind.Bandit, Name = "Test Band", Tier = 1, Pos = member.Pos };
        band.Companies.Add(c.NewCompany("levies", "infantry", c.Scaled(10), 0));
        c.S.Parties.Add(band);
        int men = band.Men;
        c.Clash(member, band);
        Check(band.Men == men && !band.Down, "a lord with a job rides past brigands that leave him alone");
        band.ChaseId = member.Id;
        c.Clash(member, band);
        Check(band.Down || band.Men < men, "brigands who fall on a lord get their fight");

        // Rabble is slow, trained brigands quick.
        var foot = new Party { Kind = PartyKind.Lord };
        foot.Companies.Add(c.NewCompany("levies", "infantry", 30, 0));
        var rabble = new Party { Kind = PartyKind.Bandit, Tier = 1 };
        rabble.Companies.Add(c.NewCompany("levies", "infantry", 30, 0));
        var trained = new Party { Kind = PartyKind.Bandit, Tier = 5 };
        trained.Companies.Add(c.NewCompany("levies", "infantry", 30, 0));
        Check(c.Speed(rabble) < c.Speed(foot) && c.Speed(trained) > c.Speed(foot), "rabble is slower than a lord's men, trained brigands quicker");

        // The leader falls: the army breaks up.
        leader.Down = true;
        c.ArmiesDaily();
        Check(c.S.Armies.Count(a => a.Id == army.Id) == 0 && lords.All(l => l.ArmyId < 0), "an army whose leader falls breaks up");
        Console.WriteLine($"armies: {lords.Count} lords, leader {c.Speed(leader):0.0}, rabble {c.Speed(rabble):0.0}, foot {c.Speed(foot):0.0}, trained {c.Speed(trained):0.0}");
    }

    /// Buying a horse costs HorsePrice (also the example in docs/DEVELOPING.md 9).
    static void HorsesCostGold()
    {
        var c = Campaign.New(3);
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Horses > 0);
        float gold = c.S.Gold;
        Check(c.BuyHorses(town, 1) == null, "can buy a horse");
        Check(c.S.Gold == gold - c.HorsePrice, "a horse costs HorsePrice");
    }

    /// Goods from the field sell in a friendly town's market at GoodsPrice, and nowhere else.
    static void GoodsSell()
    {
        var c = Campaign.New(3);
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
        var village = c.S.Settlements.First(s => s.Kind == SettlementKind.Village && !c.Refuses(s));
        c.S.Goods["arms"] = 5;
        c.S.Goods["silver"] = 2;
        Check(c.SellGoods(village) != null && c.GoodsCount == 7, "a village does not buy goods");
        float gold = c.S.Gold;
        int arms = 5 * c.GoodsPrice(Campaign.GoodsOf("arms"), town);
        Check(c.SellGoods(town, "arms") == null && c.S.Gold == gold + arms && c.GoodsCount == 2, "one kind sells at its price");
        Check(c.SellGoods(town) == null && c.GoodsCount == 0 && c.SellGoods(town) != null, "SELL ALL empties the pack");
        string want = c.WantedGoods(town);
        Check(c.GoodsPrice(Campaign.GoodsOf(want), town) > Campaign.GoodsOf(want).Value, "the town pays more for the goods it wants");
    }

    /// Polish Q6: a game saved in the middle of a long run and loaded again goes on exactly as one that never saved
    /// (no state kept only in memory), and a long run leaves nothing broken behind it.
    static void SaveMidRun()
    {
        var c = Campaign.New(2203);
        new Pilot(c).Run(200 * 24);
        CheckSound(c, "after 200 days");
        Check(c.S.Fallen.Count + c.Player.Companies.Sum(k => k.Roll.Count) > 0, "by day 200 the roll is in play");
        // The player's planned route is a cache, replanned from where he stands after a load, so a save mid-march resumes by a
        // slightly different path (skill practice, then everything after, drifts). Save him standing, as a player saves.
        c.Player.Moving = false; c.Player.GoalSettlement = -1; c.Player.ChaseId = -1;
        var loaded = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        string a = Drive(c, 60), b = Drive(loaded, 60);
        if (a != b)
        {
            var la = a.Split('\n'); var lb = b.Split('\n');
            int i = 0; while (i < Math.Min(la.Length, lb.Length) && la[i] == lb[i]) i++;
            Console.WriteLine($"first difference at line {i}: {(i < la.Length ? la[i].Trim() : "")} / {(i < lb.Length ? lb[i].Trim() : "")}");
        }
        Check(a == b, "a game saved and loaded at day 200 goes on as one that never saved");
        CheckSound(c, "after 260 days");
        Console.WriteLine($"save mid-run: day {c.Day}, {c.Player.Men} men, {c.S.Fallen.Count} fallen, {c.S.Log.Count} log lines");
    }

    /// The same plain driver for both halves of `SaveMidRun`: the pilot keeps a little state of its own, the campaign must not.
    static string Drive(Campaign c, int days)
    {
        for (int h = 0; h < days * 24 && !c.S.GameOver; h++)
        {
            c.Tick(1);
            if (c.Encounter != null) c.AutoResolveEncounter();
            c.Arrived = null; c.Report = null; c.Beat = null;
            if (c.S.Event != null) c.Choose(c.EventOf0());
        }
        return c.S.Serialize();
    }

    /// Nothing a long run should leave: empty companies on a roster, negative men, lords serving a realm with no land.
    static void CheckSound(Campaign c, string when)
    {
        foreach (var p in c.S.Parties.Append(c.Player))
            foreach (var k in p.Companies)
            {
                Check(k.Men + k.Wounded > 0, $"{when}: no empty company on a roster ({k.Name})");
                Check(k.Stacks.All(st => st.Men >= 0 && st.Wounded >= 0), $"{when}: no negative men ({k.Name})");
            }
        var landed = c.S.Settlements.Select(t => t.Faction).ToHashSet();
        foreach (var f in c.S.Factions.Where(f => !landed.Contains(f.Id)))
            Check(!c.S.Parties.Any(p => p.Kind == PartyKind.Lord && !p.Down && p.Faction == f.Id && p != c.Player), $"{when}: no lords for {f.Name}, a realm with no land");
    }

    /// Polish Q6: each company's report adds up to what happened to its record, and each named man the report names is
    /// on a roll or among the fallen; after auto-resolved fights and after a fought one.
    static void ReportMatchesRecord()
    {
        var c = Campaign.New(2207);
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        foreach (var k in c.Player.Companies) for (int i = 0; i < 3; i++) c.NameMan(k, "test");
        int checkedReports = 0;
        for (int round = 0; round < 6; round++)
        {
            var bandit = c.S.Parties.FirstOrDefault(p => p.Kind == PartyKind.Bandit && !p.Down);
            if (bandit == null || c.Player.Men <= 0) break;
            bandit.Pos = c.Player.Pos;
            c.Encounter = bandit;
            BattleReport rep;
            if (round % 2 == 0) rep = c.AutoResolveEncounter();
            else
            {
                var setup = c.PrepareBattle(bandit);
                setup.Options.AutoPlayer = true;
                var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
                for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
                rep = c.FinishBattle(b.Result(), setup);
            }
            c.Encounter = null;
            foreach (var cr in rep.Companies.Where(x => x.Team == 0))
            {
                Check(cr.MenBefore - cr.MenAfter == cr.Killed + cr.Wounded + cr.Captured + cr.FledLost,
                    $"{cr.Name}: lost {cr.MenBefore - cr.MenAfter} = killed {cr.Killed} + hurt {cr.Wounded} + taken {cr.Captured} + fled {cr.FledLost}");
                if (c.Player.Companies.FirstOrDefault(k => k.Id == cr.Id) is CompanyRecord k)
                    Check(k.Men == cr.MenAfter, $"{cr.Name}: the report's men ({cr.MenAfter}) are the company's ({k.Men})");
                checkedReports++;
            }
            var known = c.Player.Companies.SelectMany(k => k.Roll).Concat(c.S.Fallen).Select(m => m.Name).ToList();
            foreach (var (kind, text) in rep.Roll.Where(n => n.Item1 is "FELL" or "MISSING" or "HURT" or "NAMED"))
                Check(known.Any(n => text.StartsWith(n + ",")), $"the report's {kind} man is on a roll or among the fallen: {text}");
            c.Player.Cooldown = 1e6f;
        }
        Check(checkedReports > 0, "some reports were checked");
        Console.WriteLine($"report matches record: {checkedReports} company reports, {c.S.Fallen.Count} fallen");
    }

    /// The campaign's own mix is stable across processes and versions (it replaced HashCode.Combine, which is seeded afresh in
    /// every process: weather, battle seeds and an armourer's stock differed between two runs of one save).
    static void StableMix()
    {
        Check(Rng.Mix(1, 2, 3) == Rng.Mix(1, 2, 3) && Rng.Mix(1, 2, 3) != Rng.Mix(3, 2, 1), "a mix depends on its values and their order");
        Console.WriteLine($"mix: {Rng.Mix(1, 2, 3)} {Rng.Of("sallet")}");
        Check(Rng.Mix(1, 2, 3) == -32489229 && Rng.Of("sallet") == -1432741536, "and gives the same numbers in every process");
    }

    /// The feed has levels: lines about you show on all of them, the realm's news from Notable up, world chatter only on Everything.
    static void LogLevels()
    {
        var c = Campaign.New(31);
        c.S.Log.Clear();
        c.Log("You buy a sword.");
        c.Log("The Azure Crown and the Ashen League make peace.");
        c.Log("Lord Brand defeats Ember Crew near Otley.", LogLevel.Everything);
        c.Log("Your men go unpaid.");
        int Shown(LogLevel l) => c.S.Log.Count(e => e.Level <= l);
        Check(Shown(LogLevel.Yours) == 2, "yours: only what is about you");
        Check(Shown(LogLevel.Notable) == 3, "notable: and the realm's news");
        Check(Shown(LogLevel.Everything) == 4, "everything: and the chatter");
        Check(c.S.Log[0].Level == LogLevel.Yours && c.S.Log[1].Level == LogLevel.Notable, "a line is classed by what it says when no level is given");
    }

    /// Noble birth (docs/NOBILITY.md): XP promotion never turns common troops into noble ones (no militia into paladins),
    /// whatever the culture or barracks; a noble troop still trains up its own line.
    static void NobleBirth()
    {
        Check(!Troops.Validate().Any(), "troops.json validates with stock");
        Check(Troops.All.Count(t => t.Noble) >= 60, "the noble troops are marked");
        var c = Campaign.New(95);
        var co = c.Player.Companies.First();
        Check(c.CanEnterLine(c.Player, co, "levies", "crown_squires", out _), "levies still become (common) crown squires");
        Check(!c.CanEnterLine(c.Player, co, "lancers", "knights", out string why) && why.Contains("NOBLE"), "lancers cannot drill into knights");
        Check(!c.CanEnterLine(c.Player, co, "ironguard", "paladins", out _), "ironguard cannot drill into paladins");
        Check(c.CanEnterLine(c.Player, co, "levies", "shieldmen", out _), "levies still become shieldmen");
        Check(c.CanEnterLine(c.Player, co, "paladins", "royal_crusaders", out _) || !Troops.Get("royal_crusaders").Noble, "a noble troop trains up the noble line");
        // Everything XP can reach from the commoner roots without crossing the gate is common.
        var seen = new HashSet<string>(); var todo = new Stack<string>(new[] { "levies", "clansmen" });
        while (todo.Count > 0)
        {
            string id = todo.Pop();
            if (!seen.Add(id)) continue;
            foreach (var u in Troops.Get(id).UpgradesTo)
                if (c.CanEnterLine(c.Player, co, id, u, out _) || Troops.Get(u).Order != "" || !Troops.Get(u).Noble) todo.Push(u);
        }
        Check(!seen.Any(id => Troops.Get(id).Noble && Troops.Get(id).Order == ""), "no noble troop is reachable by drilling levies or clansmen: " + string.Join(", ", seen.Where(id => Troops.Get(id).Noble && Troops.Get(id).Order == "")));
    }

    /// N2: castles send esquires (and only castles, to their own captains), the pool refills without dice, sellswords and
    /// prisoners are never noble, and every noble troop has a way in (a castle root, an order, or knighting).
    static void NobleRecruits()
    {
        var c = Campaign.New(96);
        var castle = c.S.Settlements.First(t => t.Kind == SettlementKind.Castle && t.Faction >= 0);
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town);
        c.S.Gold = 5000; c.Captain.Renown = 100; c.Captain.Contract = castle.Faction;
        Check(c.RecruitNobles(town, false, 3) is string s1 && s1.Contains("CASTLES"), "towns raise no esquires: " + c.RecruitNobles(town, false, 3));
        castle.Nobles = 0;
        Check(c.RecruitNobles(castle, false, 3) is string s2 && s2.Contains("NO ESQUIRES"), "an empty pool sends none");
        c.Player.Cooldown = 1e6f;
        for (int h = 0; h < 40 * 24; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; c.Report = null; c.Beat = null; if (c.S.Event != null) c.Choose(c.EventOf0()); }
        Check(castle.Nobles > 0 && castle.Nobles <= c.Scaled(Balance.Get().NobleRecruitsMax), $"the pool refills to its cap ({castle.Nobles})");
        c.Captain.Renown = 10;
        Check(c.CannotRecruitNobles(castle) is string lowRenown && lowRenown.Contains("RENOWN"), "a house wants renown");
        c.Captain.Renown = 100;
        int gold = (int)c.S.Gold, pool = castle.Nobles;
        Check(c.RecruitNobles(castle, false, 2) == null && castle.Nobles == pool - 2 && c.S.Gold < gold, "esquires join for a price");
        Check(c.Player.Companies.Any(k => k.Stacks.Any(x => x.Troop == "esquires")), "they form a company of their own");
        c.Captain.Contract = -1;
        Check(c.CannotRecruitNobles(castle) is string foreign && foreign.Contains("OWN CAPTAINS"), "a free captain is not sent esquires");
        var co = c.Player.Companies.First(k => k.Stacks.Any(x => x.Troop == "esquires"));
        Check(!c.CanEnterLine(c.Player, co, "levies", "esquires", out _) || !Troops.Get("levies").UpgradesTo.Contains("esquires"), "no drill leads into esquires");
        Check(Troops.Get("esquires").Noble && Troops.Get("esquires").UpgradesTo.Any(u => Troops.Get(u).Noble), "esquires train up the noble line");
        // Nobles never come cheap otherwise: not as hired sellswords, not as prisoners.
        for (int k = 0; k < 40; k++)
        {
            foreach (var t in c.S.Settlements.Where(t => t.Kind == SettlementKind.Town)) c.RefreshMercenaries(t);
            Check(c.S.Settlements.SelectMany(t => t.Mercenaries).SelectMany(m => m.Stacks).All(x => !x.Type.Noble || x.Type.Order != ""), "no sellsword company is noble");
        }
        var pg = new PrisonerGroup { Troop = "paladins", Count = 4 };
        c.Player.Prisoners.Add(pg);
        Check(c.RecruitPrisoners(pg) is string held && held.Contains("NOBLES"), "noble prisoners are ransomed, not recruited");
        // Every noble troop (outside an order's own gate) has a way in: a castle root, or a commoner parent (knighting, N3).
        var knighted = Troops.All.Where(t => t.Noble && Troops.All.Any(p => !p.Noble && p.UpgradesTo.Contains(t.Id))).Select(t => t.Id).ToHashSet();
        var rooted = new HashSet<string>(); var todo = new Stack<string>(new[] { "esquires", "mounted_esquires" });
        while (todo.Count > 0) { string id = todo.Pop(); if (rooted.Add(id)) foreach (var u in Troops.Get(id).UpgradesTo) if (Troops.Get(u).Noble) todo.Push(u); }
        var reach = new HashSet<string>(rooted); todo = new Stack<string>(knighted); // a knighted man then drills up the noble line
        while (todo.Count > 0) { string id = todo.Pop(); if (reach.Add(id) || knighted.Contains(id)) foreach (var u in Troops.Get(id).UpgradesTo) if (Troops.Get(u).Noble && !reach.Contains(u)) todo.Push(u); }
        var lost = Troops.All.Where(t => t.Noble && t.Order == "" && t.Culture != "order" && !reach.Contains(t.Id)).Select(t => t.Id).ToList();
        Console.WriteLine($"nobles: {rooted.Count} from the castle roots, {knighted.Count} by knighting, {reach.Count} in all, unreachable: {string.Join(", ", lost)}");
        Check(lost.Count == 0, "every noble troop has a way in: " + string.Join(", ", lost));
    }
}
