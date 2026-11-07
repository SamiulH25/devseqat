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
        var m = WorldMap.Generate(11);
        Check(m.Provinces.Count >= 50, $"a big map: enough provinces ({m.Provinces.Count})");
        Check(m.Provinces.Count is >= WorldMap.ProvinceCount - 8 and <= WorldMap.ProvinceCount + 8,
            $"about {WorldMap.ProvinceCount} provinces on the enlarged map ({m.Provinces.Count})");
        Check(m.ExtentUsed >= WorldMap.Extent && m.Settlements.Max(s => MathF.Abs(s.Pos.X)) > WorldMap.LegacyExtent,
            "the map is bigger than the old one, not just denser");
        for (int f = 0; f < 3; f++) Check(m.Settlements.Any(s => s.Faction == f && s.Kind == SettlementKind.Town), $"faction {f} has towns");
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

    /// A level-2 army on contract, sitting still, stays solvent for 8 weeks.
    static void Economy()
    {
        var c = Campaign.New(41);
        c.Player.Companies.Clear();
        foreach (var co in Armies.Standard(0).Companies) c.Player.Companies.Add(c.NewCompany(co.Troop, co.Role, co.Count, 0));
        c.Player.Cooldown = 1e6f;
        for (int h = 0; h < 8 * 168; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; }
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

    /// Auto-resolve picks the same winner as the battle simulation at least 75% of the time.
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
            var b = new Battle(200 + i, Ids(a0, 1000), Ids(a1, 2000), new BattleOptions { AutoPlayer = true, PlayerWounded = true });
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
        Check(c.S.Factions.Count == 3 && c.FactionsAtWar(0, 1) && !c.FactionsAtWar(1, 2) && !c.FactionsAtWar(0, 2), "three realms, one war and two truces");
        Check(c.S.Settlements.Where(s => s.Kind != SettlementKind.Village).All(s => s.Garrison.Count > 0), "every town and castle has a garrison");
        Check(c.S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == 2).SelectMany(p => p.Companies).Any(co => co.Troop is "clansmen" or "axemen" or "huscarls" or "horse_archers"), "Northmarch lords field their own troops");
        // Your own siege, on paper: a strong company takes a castle and, as a sworn vassal (F5), holds it as its first fief.
        c.Captain.Fealty = true;
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && s.Faction == 1).OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).First();
        c.Player.Companies.Clear();
        foreach (var t in new[] { "men_at_arms", "men_at_arms", "longbowmen", "pikemen" }) c.Player.Companies.Add(c.NewCompany(t, Rules.TroopRole(t), Rules.CapOf(t, Rules.TroopRole(t)), 0));
        c.Player.Pos = castle.Pos + new Vector2(3, 0);
        // Clear the ground around the castle first: on a big map neighbours and reinforcements stand much further out than
        // the old test's 12, and an encounter would cut the night's rest short before the ladders are built.
        float clear = 30 * c.Map.Spread;
        foreach (var p in c.S.Parties.Where(p => p != c.Player && Vector2.Distance(p.Pos, castle.Pos) < clear)) p.Pos += Vector2.Normalize(p.Pos - castle.Pos + new Vector2(0.01f, 0)) * clear;
        Check(c.CanBesiege(castle, out string why), "you can besiege an enemy castle: " + why);
        c.Besiege(castle);
        for (int h = 0; h < 13 && c.Encounter == null; h++) c.Rest(1);
        c.Encounter = null;
        Check(c.LaddersIn(castle) == 0 && c.Besieging == castle, "ladders are built overnight");
        c.Encounter = null;
        c.AutoResolveAssault(castle);
        Console.WriteLine($"assault on {castle.Name}: {(castle.Faction == 0 ? "taken" : "repulsed")}, prisoners {c.PrisonerCount}");
        if (castle.Faction == 0)
        {
            Check(castle.Owner == c.Player.Id && c.S.Provinces[castle.Province].Faction == 0, "the castle and its province change hands");
            Check(c.S.Settlements.Where(v => v.Province == castle.Province).All(v => v.Faction == 0), "its villages follow");
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
        Check(c.S.Provinces.All(p => c.S.Settlements.First(s => s.Province == p.Id && s.Kind != SettlementKind.Village).Faction == p.Faction), "a province belongs to whoever holds its seat");
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
            Check(CampaignState.Load("old") == null && CampaignState.LoadError == CampaignState.TooOld, "loading an old save from disk returns nothing, with the reason");
            c.S.Save("new");
            Check(CampaignState.Load("new") != null && CampaignState.LoadError == null, "a new save loads from disk");
        }
        finally { Environment.SetEnvironmentVariable("IRONFIELD_SAVES", was); Directory.Delete(dir, true); }
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
        // Promotion needs a town; out in the field it is refused with a reason.
        c.Player.Pos = town.Pos + new Vector2(40, 40);
        Check(c.Promote(co, lev, "shieldmen", 1) == "NEEDS A TOWN", "promotion out in the field is refused");
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
    static void Cultures()
    {
        var errs = Factions.Validate().ToList();
        Check(errs.Count == 0, "factions.json is valid: " + string.Join("; ", errs));
        Check(Troops.All.All(t => t.Culture is "common" or "azure" or "crimson" or "northern"), "every troop has a culture");
        foreach (var cul in Factions.Cultures)
        {
            var seen = new HashSet<string>();
            var open = new Queue<string>(new[] { cul.Root });
            while (open.Count > 0)
            {
                var t = Troops.Get(open.Dequeue());
                if (!seen.Add(t.Id)) continue;
                foreach (var u in t.UpgradesTo)
                    if (Troops.Get(u).Culture is var uc && (uc == Factions.Common || uc == cul.Id)) open.Enqueue(u);
            }
            var top = seen.Select(Troops.Get).Where(t => t.Tier == 7 && t.Culture == cul.Id).ToList();
            Console.WriteLine($"culture {cul.Id}: {seen.Count} troops open from {cul.Root}, tier VII: {string.Join(", ", top.Select(t => t.Id))}");
            Check(top.Count >= 3, $"{cul.Id} men can climb from {cul.Root} to tier VII lines of their own");
            foreach (var role in new[] { "infantry", "archers", "horse" })
                Check(top.Any(t => Rules.TroopRole(t.Id) == role), $"{cul.Id} has a tier VII {role} line");
        }
        var c = Campaign.New(83);
        foreach (var v in c.S.Settlements.Where(v => v.Kind == SettlementKind.Village))
        {
            Check(v.Culture == Factions.CultureOf(v.Faction), "a village starts with its realm's culture");
            Check(Campaign.RecruitTroop(v) == Factions.Root(v.Culture), "a village raises its culture's root");
        }
        // A northern village held by the Azure Crown still raises clansmen, and they form a northern company.
        var north = c.S.Settlements.First(v => v.Kind == SettlementKind.Village && v.Culture == "northern");
        north.Faction = 0;
        north.Recruits = 10;
        c.Player.Pos = north.Pos;
        c.S.Gold = 1000;
        Check(c.RecruitLevies(north, 6) == null, "you can recruit in a conquered northern village");
        var clans = c.Player.Companies.FirstOrDefault(k => k.Stacks.Any(st => st.Troop == "clansmen"));
        Check(clans != null && clans.Culture == "northern" && clans.Stack("clansmen").Men == 6, "its men are clansmen, in a northern company");
        Check(c.Player.Companies.Where(k => k != clans).All(k => k.Stacks.All(st => st.Troop != "clansmen")), "and they did not dilute the azure companies");
        // Culture lines: azure shieldmen may become men-at-arms but not crimson greatswords.
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && t.Faction == 0);
        c.Player.Pos = town.Pos;
        var az = c.NewCompany("shieldmen", "infantry", 10, 1000, town);
        c.Player.Companies.Add(az);
        var sh = az.Stack("shieldmen");
        Check(az.Culture == "azure" && sh.Ready > 0, "an azure company of ready shieldmen");
        Check(c.Promotable(c.Player, az, sh, "greatswords", 1, 1000, out _, out string why) == 0 && why.Contains("CANNOT TRAIN"), "azure men cannot train as crimson greatswords: " + why);
        Check(c.Promotable(c.Player, az, sh, "men_at_arms", 1, 1000, out _, out _) == 1, "but can as azure men-at-arms");
        // Elite lines: azure ironguard -> paladins (tier V) needs an azure barracks in your realm.
        var ig = c.NewCompany("ironguard", "infantry", 6, 1000, town);
        c.Player.Companies.Add(ig);
        Check(c.Promotable(c.Player, ig, ig.Stack("ironguard"), "paladins", 1, 5000, out _, out _) == 1, "a captain sworn to the Crown may train paladins");
        c.Captain.Contract = -1;
        Check(c.Promotable(c.Player, ig, ig.Stack("ironguard"), "paladins", 1, 5000, out _, out why) == 0 && why.Contains("BARRACKS"), "a free captain may not: " + why);
        // Each realm's lords field their own culture's troops.
        var w = Campaign.New(84);
        w.Player.Cooldown = 1e6f;
        for (int h = 0; h < 60 * 24; h++) { w.Tick(1); w.Encounter = null; w.Arrived = null; }
        for (int f = 0; f < 3; f++)
        {
            var stacks = w.S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == f).SelectMany(p => p.Companies).SelectMany(k => k.Stacks).Where(st => st.Type.Culture != Factions.Common).ToList();
            int own = stacks.Where(st => st.Type.Culture == Factions.CultureOf(f)).Sum(st => st.Men), all = stacks.Sum(st => st.Men);
            Console.WriteLine($"realm {f} ({Factions.CultureOf(f)}): {own}/{all} of its lords' cultured men are its own");
            Console.WriteLine("  " + string.Join(", ", w.S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == f).SelectMany(p => p.Companies).SelectMany(k => k.Stacks).GroupBy(st => st.Troop).Select(g => $"{g.Key}:{g.Sum(st => st.Men)}")));
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
            var foe = w.S.Parties.First(p => p.Kind == PartyKind.Lord && p.Faction == 1);
            var ch = w.CharacterOf(lord)!;
            ch.Traits.Clear(); ch.Traits.Add(trait);
            var spot = new Vector2(0, 0);
            foreach (var p in w.S.Parties.Where(p => p != lord && p != foe)) p.Pos = new Vector2(5000, 5000);
            lord.Pos = spot; foe.Pos = spot + new Vector2(12, 0);
            lord.Companies.Clear(); foe.Companies.Clear();
            foreach (var role in new[] { "infantry", "archers", "spears" }) lord.Companies.Add(w.NewCompany(Armies.LineTroop(role, 3, 0), role, 16, 0));
            foreach (var role in new[] { "infantry", "archers", "spears" }) foe.Companies.Add(w.NewCompany(Armies.LineTroop(role, 3, 1), role, 16, 0));
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
        Check(c.MemoriesOf(heir, c.Me).Any(m => m.What == "you slew my father"), "and remembers who killed his father");
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
    /// Divisions on the map (docs/COMBAT_OVERHAUL.md 1.5): make one under a sergeant, assign a company, keep it in the
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
        c.SetDivisionGoal(d, "FlankLeft");
        Check(c.CommandOf(d) == 3, $"a sergeant's command grows with his battles ({c.CommandOf(d)})");
        float edge = c.TacticsEdge;
        lead.OfficerBattles = 15;
        Check(c.TacticsEdge > edge, "a skilled division leader helps when you auto-resolve");
        var back = CampaignState.Deserialize(c.S.Serialize());
        Check(back.Divisions.Count == 1 && back.Divisions[0].Goal == "FlankLeft" && back.Parties[0].Companies.Count(x => x.Division == d.Id) == 2,
            "the save keeps divisions and who is in them");
        var setup = c.PrepareBattle(bandit);
        Check(setup.Player.Divisions?.Count == 1 && setup.Player.Companies.Count(x => x.Division == d.Id) == 2, "the battle army carries the division");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        var bd = b.DivisionsOf(0).SingleOrDefault();
        Check(bd != null && bd.Squads.Count == 2 && bd.Goal.Kind == GoalKind.Flank && bd.Goal.Side < 0 && bd.Leader?.Sergeant == true,
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

    static void SkillTree()
    {
        var a = Campaign.New(91);
        var b = Campaign.New(91);
        var rngA = new Rng(new CampaignState { Seed = 5 });
        Check(a.Skill("Leadership") > 0 && a.Captain.Perks.Count == 0, "a new captain has some skill and no perks");
        Check(a.TakePerk("Duellist") != null, "no perk before its skill reaches 25");
        Check(a.BuyGear(0) is string g0 && g0.Contains("ARMS"), "plate armour needs Arms");
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

    /// Buying a horse costs HorsePrice (also the example in docs/DEVELOPING.md 9).
    static void HorsesCostGold()
    {
        var c = Campaign.New(3);
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Horses > 0);
        float gold = c.S.Gold;
        Check(c.BuyHorses(town, 1) == null, "can buy a horse");
        Check(c.S.Gold == gold - c.HorsePrice, "a horse costs HorsePrice");
    }
}
