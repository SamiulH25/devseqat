using Microsoft.Xna.Framework;

namespace Ironfield;

/// `dotnet run -- --selftest`: formation math, mechanic scenarios, and full headless battles that must reach a result.
public static class SelfTest
{
    public static void Run()
    {
        var s = new Squad("t", 0) { Anchor = Vector2.Zero, Front = Vector2.UnitY, Columns = 4 };
        Check(Vector2.Distance(s.Slot(0, 8) + s.Slot(3, 8), Vector2.Zero) < 1e-4f, "front rank centred on anchor");
        Check(s.Slot(4, 8).Y < 0, "second rank behind the front");
        s.SetLine(new(-5, 10), new(5, 10), new(0, 20));
        Check(s.Front.Y > 0.99f && s.Anchor == new Vector2(0, 10), "drawn line faces the target side");
        s.SetLine(new(-5, 10), new(5, 10), new(0, 0));
        Check(s.Front.Y < -0.99f, "drawn line flips to face a target behind it");

        Charge();
        Flanks();
        Parry();
        HorseDown();
        Ground();
        HoldFire();
        TroopTree();
        Sprites();
        BigBattle();
        CampaignMap();
        CampaignPersistence();
        CampaignRun();
        Economy();
        Bridge();
        Calibration();

        for (int seed = 1; seed <= 3; seed++)
        {
            var b = new Battle(seed);
            for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
            Check(b.Winner >= 0, $"battle {seed} resolves within 10 minutes");
            Console.WriteLine($"battle {seed}: team {b.Winner} won after {b.Time:0}s, {b.Units.Count(u => u.Dead)} dead, "
                + $"{b.Units.Count(u => u.Fled)} fled, {b.DeadHorses.Count} horses lost");
        }
        // Campaign-style armies at every level (both sides upgraded unevenly) still fight to a result.
        for (int level = 1; level <= 4; level++)
        {
            var rng = new Random(level);
            var b = new Battle(level, Armies.Random(rng, level, 0), Armies.Random(rng, level, 1));
            for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
            Check(b.Winner >= 0, $"level {level} battle resolves");
            Console.WriteLine($"level {level}: {string.Join(", ", b.Armies[0]!.Companies.Select(c => $"{c.Count} {Troops.Get(c.Troop).Name}"))}"
                + $" vs {string.Join(", ", b.Armies[1]!.Companies.Select(c => $"{c.Count} {Troops.Get(c.Troop).Name}"))}: team {b.Winner} won after {b.Time:0}s");
        }
        Console.WriteLine("selftest ok");
    }

    static Unit Lancer(Battle b)
    {
        var cav = b.AddSquad(0, UnitType.Lancer, 1, new(0, -25), 1, Vector2.UnitY);
        cav.Order = Order.Charge;
        var l = cav.Units[0];
        l.Speed = l.Horse!.Gallop;
        return l;
    }

    static void Run(Battle b, float seconds) { for (int i = 0; i < seconds * 60; i++) b.Update(1 / 60f); }

    /// A gallop into loose infantry hurts it; a braced spear line hurts the horse instead.
    static void Charge()
    {
        var b = Battle.Empty(1);
        var l = Lancer(b);
        var foot = b.AddSquad(1, UnitType.Shieldman, 1, Vector2.Zero, 1, -Vector2.UnitY);
        foot.Loose = true; // not braced
        var f = foot.Units[0];
        Run(b, 3);
        Check(f.Hp < f.Type.MaxHp, "couched lance / trample hurts unbraced infantry");
        Check(l.Mounted && l.HorseHp == l.Horse!.Hp, "an unbraced man does not hurt the horse");

        b = Battle.Empty(2);
        l = Lancer(b);
        var spear = b.AddSquad(1, UnitType.Spearrunner, 1, Vector2.Zero, 1, -Vector2.UnitY).Units[0];
        Run(b, 3);
        Check(!l.Mounted || l.HorseHp < UnitType.Lancer.Mount!.Hp, "a braced spear catches the horse");
        Check(spear.Active, "the braced spearman survives the charge");
    }

    /// Blows from behind land harder than blows from the side.
    static void Flanks()
    {
        var b = Battle.Empty(3);
        var a = b.AddSquad(0, UnitType.Shieldman, 1, new(0, 0), 1, Vector2.UnitY).Units[0];
        var t = b.AddSquad(1, UnitType.Shieldman, 2, new(0, 2), 2, Vector2.UnitY).Units[0]; // faces away from a
        float hp = t.Hp;
        b.Hit(a, t, 30, false);
        float rear = hp - t.Hp;
        t.Facing = 0; // now a stands at its side
        hp = t.Hp;
        b.Hit(a, t, 30, false);
        float flank = hp - t.Hp;
        Check(rear > flank && flank > 0, $"rear hits ({rear:0}) beat flank hits ({flank:0})");
    }

    /// Blocking in the direction the blow comes from is a parry: no damage, the attacker staggers.
    static void Parry()
    {
        var b = Battle.Empty(4);
        var p = b.Player;
        var foe = b.AddSquad(1, UnitType.Shieldman, 1, p.Pos + new Vector2(0, 1.5f), 1, -Vector2.UnitY).Units[0];
        p.Facing = MathHelper.PiOver2;
        p.Action = UnitAction.Block;
        foe.Swing = AttackDir.Right;
        b.Input.Dir = Dirs.Mirror(foe.Swing);
        float hp = p.Hp, st = p.Stamina;
        b.Hit(foe, p, 30, false);
        Check(p.Hp == hp && foe.Action == UnitAction.Stagger && p.Stamina > st - 5, "matched block parries");
        b.Input.Dir = AttackDir.Overhead;
        foe.Action = UnitAction.None;
        st = p.Stamina;
        b.Hit(foe, p, 30, false);
        Check(p.Hp == hp && p.Stamina < st - 5, "unmatched shield block holds but costs stamina");
    }

    static void HorseDown()
    {
        var b = Battle.Empty(5);
        var l = Lancer(b);
        b.HitHorse(l, 999, b.EnemyCaptain);
        Check(!l.Mounted && b.DeadHorses.Count == 1 && l.Action == UnitAction.Stagger, "a slain horse throws its rider");
    }

    /// The heightmap matches its grid, the centre field is open, trunks are solid and slopes slow you down.
    static void Ground()
    {
        var t = new Terrain(42);
        Check(MathF.Abs(t.Height(new Vector2(-Terrain.Half + 10 * Terrain.Step, -Terrain.Half + 7 * Terrain.Step)) - t.GridHeight(10, 7)) < 1e-4f,
            "height samples the mesh vertices exactly");
        for (float x = -200; x <= 200; x += 7)
            for (float z = -200; z <= 200; z += 7)
                Check(float.IsFinite(t.Height(new Vector2(x, z))) && MathF.Abs(t.Height(new Vector2(x, z))) < 60, "heights stay sane");
        Check(t.Foliage.Count(f => f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine) > 150, "the land is wooded");
        Check(!t.Foliage.Any(f => f.Radius > 0 && Terrain.Core(f.Pos)), "no trees or boulders where the armies deploy");
        var tree = t.Foliage.First(f => f.Kind == FoliageKind.Broadleaf);
        var p = tree.Pos + new Vector2(0.05f, 0);
        Check(t.PushOut(ref p, 0.35f) && Vector2.Distance(p, tree.Pos) >= tree.Radius + 0.34f, "trunks push units out");

        // Walking up a slope is slower than walking down it.
        var steepest = Vector2.Zero;
        for (float x = -45; x <= 45; x += 3)
            for (float z = -50; z <= 50; z += 3)
                if (t.Gradient(new Vector2(x, z)).Length() > t.Gradient(steepest).Length()) steepest = new Vector2(x, z);
        var up = Vector2.Normalize(t.Gradient(steepest));
        Check(Battle.SlopeFactor(t, steepest, up) < 1 && Battle.SlopeFactor(t, steepest, -up) > 1, "uphill is slower than downhill");
    }

    /// Archers told to hold their arrows do not shoot.
    static void HoldFire()
    {
        var b = Battle.Empty(6);
        var bows = b.AddSquad(0, UnitType.Bowman, 4, new(0, -10), 4, Vector2.UnitY);
        bows.FireAtWill = false;
        b.AddSquad(1, UnitType.Shieldman, 4, new(0, 15), 4, -Vector2.UnitY);
        Run(b, 3);
        Check(b.Arrows.Count == 0, "held arrows stay on the string");
        bows.FireAtWill = true;
        Run(b, 3);
        Check(b.Arrows.Count > 0, "fire at will looses arrows");
    }

    /// The upgrade trees are sound: every line climbs in tier, and higher tiers are better soldiers.
    static void TroopTree()
    {
        Check(!Troops.Validate().Any(), "troops.json validates: " + string.Join("; ", Troops.Validate()));
        var levies = Troops.Get("levies");
        Check(levies.Tier == 1 && levies.UpgradesTo.Length >= 3, "levies branch into several lines");
        foreach (var t in Troops.All)
            foreach (var next in t.UpgradesTo.Select(Troops.Get))
                Check(next.MaxHp + next.Armor * 5 + next.Damage > t.MaxHp + t.Armor * 5 + t.Damage, $"{t.Name} -> {next.Name} is an upgrade");
        Check(Troops.Tree("levies").Any(t => t.Tier == 4), "the tree reaches tier IV");
        var reachable = Troops.Tree("levies").Select(t => t.Id).ToHashSet();
        Check(Troops.All.Where(t => !t.Leader).All(t => reachable.Contains(t.Id)), "every troop can be reached by upgrading levies");
    }

    /// Every sprite any troop's look can ask for exists, for every side.
    static void Sprites()
    {
        var sheet = new SpriteSheet();
        IEnumerable<string> Keys(UnitType t)
        {
            var l = t.Look;
            foreach (var v in new[] { "f", "s", "b" })
            {
                yield return $"torso.{l.Body}.{v}";
                yield return $"boot.{l.Feet}.{v}";
                yield return $"head.{v}";
                if (l.Head != "none") yield return $"helm.{l.Head}.{v}";
                if (l.Tabard) yield return $"tabard.{v}";
            }
            yield return $"sleeve.{l.Body}"; yield return $"leg.{l.Legs}"; yield return $"hand.{l.Hands}"; yield return l.Weapon;
            if (l.Shield != "none") yield return $"shield.{l.Shield}";
            if (l.Plume) yield return "plume";
            if (l.Cape) yield return "cape";
            if (t.Mount != null) { yield return "hbody.saddle"; yield return "hbody.caparison"; }
        }
        foreach (var t in Troops.All)
            foreach (var k in Keys(t))
                for (int p = 0; p < Style.Sides.Length; p++)
                    Check(sheet.Rects.ContainsKey($"{k}/{p}"), $"sprite {k}/{p} for {t.Name}");
        foreach (var k in new[] { "hbody", "hneck", "hhead", "hchest", "hrump", "hface", "hleg", "htail" })
            Check(sheet.Rects.ContainsKey(k + "/n"), $"horse coat sprite {k}");
        var torso = sheet.Rects["torso.mail.f/0"];
        Check(MathF.Abs((torso.Height - 2) * Style.Px - 0.675f) < 0.05f, "a torso keeps its size in metres");
        Check(sheet.Rects.Values.Any(r => r.Height > 90), "polearms are painted at full length");
    }

    // ---------- campaign (docs/CAMPAIGN.md section 11) ----------

    static ArmySpec Scaled(ArmySpec a, float k, int idBase) =>
        a with { Companies = a.Companies.Select((c, i) => c with { Count = (int)(c.Count * k), Id = idBase + i }).ToList() };

    /// A battle bigger than the field holds: reserves wait, march on, and every man is accounted for; 200 men run in real time.
    static void BigBattle()
    {
        var rng = new Random(5);
        var b = new Battle(5, Scaled(Armies.Random(rng, 2, 0), 2.2f, 100), Scaled(Armies.Random(rng, 2, 1), 2.2f, 200),
            new BattleOptions { AutoPlayer = true, PlayerWounded = true });
        int fielded = b.Units.Count(u => !u.IsLeader), firstCompanies = b.Squads.Count(q => q.Company != null);
        Check(fielded <= 210, $"the field holds about 200 men ({fielded})");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 600; i++) b.Update(1 / 60f);
        double tps = 600 / watch.Elapsed.TotalSeconds;
        Console.WriteLine($"big battle: {fielded} men fielded, {tps:0} ticks/s");
        Check(tps >= 60, $"a 200-man battle runs in real time ({tps:0} ticks/s)");
        for (int step = 0; b.Winner < 0 && step < 60 * 900; step++) b.Update(1 / 60f);
        var r = b.Result();
        Check(r.Winner >= 0, "the big battle ends");
        Check(r.Companies.All(c => c.Standing + c.Fled + c.Fallen == c.Start), "every man is standing, fled or fallen");
        Check(r.Companies.Count(c => c.Deployed) > firstCompanies, $"reserves marched on ({firstCompanies} -> {r.Companies.Count(c => c.Deployed)} companies)");
    }

    static void CampaignMap()
    {
        var m = WorldMap.Generate(11);
        Check(m.Provinces.Count >= 18, $"enough provinces ({m.Provinces.Count})");
        for (int f = 0; f < 2; f++) Check(m.Settlements.Any(s => s.Faction == f && s.Kind == SettlementKind.Town), $"faction {f} has towns");
        var seen = new HashSet<int> { 0 };
        var todo = new Queue<int>(new[] { 0 });
        while (todo.Count > 0) foreach (int n in m.Provinces[todo.Dequeue()].Neighbors) if (seen.Add(n)) todo.Enqueue(n);
        Check(seen.Count == m.Provinces.Count, "every province is reachable by road");
        Check(m.Settlements.Count(s => s.Kind == SettlementKind.Village) >= m.Provinces.Count * 2 - 4, "provinces have villages");
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
                Check(Troops.All.Any(t => t.Id == co.Troop), "troop ids stay valid");
            }
        Check(c.S.Gold >= 0 && c.S.Factions.All(f => f.Treasury > -1), "no negative purses");
        Check(c.S.Parties.Count(p => p.Kind == PartyKind.Lord) >= 8, "lords keep the field");
        int upgraded = c.S.Parties.Where(p => p.Kind == PartyKind.Lord).SelectMany(p => p.Companies).Count(co => co.Type.Tier >= 3);
        Console.WriteLine($"campaign: day {c.Day}, {c.S.Parties.Count} parties, {upgraded} lord companies at tier III+, your battles {battles}, war score {c.S.Factions[0].WarScore:0}/{c.S.Factions[1].WarScore:0}, at war {c.S.AtWar}");
        var lords = c.S.Parties.Where(p => p.Kind == PartyKind.Lord).ToList();
        Console.WriteLine($"lords: gold avg {lords.Average(l => l.Gold):0}, max company xp {lords.SelectMany(l => l.Companies).Max(co => co.Xp):0}, "
            + $"ready {lords.SelectMany(l => l.Companies).Count(Rules.UpgradeReady)}, tiers {string.Join(",", lords.SelectMany(l => l.Companies).GroupBy(co => co.Type.Tier).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}"))}, "
            + $"tasks {string.Join(",", lords.GroupBy(l => l.Task).Select(g => $"{g.Key}:{g.Count()}"))}, treasury {c.S.Factions[0].Treasury:0}/{c.S.Factions[1].Treasury:0}");
        Check(upgraded > 0, "lords upgrade their companies too");
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
            Party P(ArmySpec a) { var p = new Party(); foreach (var co in a.Companies) p.Companies.Add(new CompanyRecord { Id = co.Id, Troop = co.Troop, Men = co.Count }); return p; }
            float s0 = Rules.Strength(P(a0)), s1 = Rules.Strength(P(a1));
            int predicted = s0 >= s1 ? 0 : 1;
            n++;
            if (predicted == b.Winner) agree++;
        }
        Console.WriteLine($"auto-resolve agrees with the battle sim in {agree}/{n} matchups");
        Check(agree >= n * 0.75f, $"auto-resolve calibration ({agree}/{n})");
    }

    static ArmySpec Ids(ArmySpec a, int idBase) => a with { Companies = a.Companies.Select((c, i) => c with { Id = idBase + i }).ToList() };

    static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("selftest failed: " + what);
    }
}
