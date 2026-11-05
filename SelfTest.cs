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
        SiegeBattle();
        Formations();
        CaptainCombat();
        RangedRoles();
        FatigueAndSupply();
        Generals();
        Rivers();
        CampaignWar();
        Migration();
        ReplayExact();

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

    public static void RunOne(string name)
    {
        var m = typeof(SelfTest).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
            ?? throw new ArgumentException("no test named " + name);
        m.Invoke(null, null);
        Console.WriteLine(name + " ok");
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
        // A guard held too long is only a block: parries need timing.
        b.Time += 1;
        b.Input.Dir = Dirs.Mirror(foe.Swing);
        foe.Action = UnitAction.None;
        p.Action = UnitAction.Block;
        st = p.Stamina;
        b.Hit(foe, p, 30, false);
        Check(p.Stamina < st - 5, "a stale guard does not parry (it blocks, at a cost)");
    }

    /// Roadmap C1-C7 mechanics, each in a small scenario.
    static void Formations()
    {
        float ArrowLoss(Formation f)
        {
            var b = Battle.Empty(8);
            var wall = b.AddSquad(0, UnitType.Shieldman, 16, new(0, 0), 8, -Vector2.UnitY);
            wall.Formation = f;
            var bows = b.AddSquad(1, UnitType.Bowman, 16, new(0, -26), 8, Vector2.UnitY);
            float before = wall.Units.Sum(u => u.Hp);
            Run(b, 20);
            return before - wall.Units.Sum(u => u.Hp);
        }
        float line = ArrowLoss(Formation.Line), shields = ArrowLoss(Formation.ShieldWall), square = ArrowLoss(Formation.Square);
        Console.WriteLine($"arrows: line loses {line:0} hp, shield wall {shields:0}, square {square:0}");
        Check(shields < line * 0.8f, "a shield wall stops more arrows");
        Check(square > shields, "a square is a better mark for archers than a shield wall");
        var s = new Squad("t", 0) { Anchor = Vector2.Zero, Front = Vector2.UnitY, Formation = Formation.Square };
        var dirs = Enumerable.Range(0, 16).Select(i => s.FaceAt(s.Slot(i, 16))).ToList();
        Check(dirs.Any(d => d.Y > 0.9f) && dirs.Any(d => d.Y < -0.9f) && dirs.Any(d => d.X > 0.9f) && dirs.Any(d => d.X < -0.9f), "a square faces every way");
        s.Formation = Formation.Wedge;
        Check(s.Slot(0, 9) == Vector2.Zero && s.Slot(1, 9).Y < 0 && s.Slot(4, 9).Y < s.Slot(1, 9).Y, "a wedge narrows to its point");
        // Rear charge on a square: no flank pressure.
        var c = Battle.Empty(9);
        var sq = c.AddSquad(0, UnitType.Shieldman, 16, new(0, 0), 4, Vector2.UnitY);
        sq.Formation = Formation.Square;
        var t = sq.Units[^1];
        c.Hit(c.EnemyCaptain, t, 5, false);
        Check(sq.Pressure == 0, "a square cannot be taken in the flank");
    }

    static void CaptainCombat()
    {
        var b = Battle.Empty(10);
        var p = b.Player;
        p.Horse = null;
        var foe = b.AddSquad(1, UnitType.Shieldman, 1, p.Pos + new Vector2(0, 1.2f), 1, -Vector2.UnitY).Units[0];
        p.Facing = MathHelper.PiOver2;
        b.Input = new PlayerInput { Facing = MathHelper.PiOver2, Bash = true };
        b.Update(1 / 60f);
        Check(foe.Action == UnitAction.Stagger, "a shield bash staggers the man in front");
        b.Input = new PlayerInput { Facing = MathHelper.PiOver2 };
        Run(b, 2);
        b.Input = new PlayerInput { Facing = MathHelper.PiOver2, Swap = true };
        b.Update(1 / 60f);
        Check(b.TwoHanded && p.Type.Look.Weapon == "greatsword" && !p.Type.HasShield, "N swaps to the two-handed sword");
        // Chambering: striking into a blow from the mirrored side turns it.
        p.Action = UnitAction.Windup;
        foe.Action = UnitAction.None;
        foe.Swing = AttackDir.Left;
        p.Swing = Dirs.Mirror(foe.Swing);
        float hp = p.Hp;
        b.Hit(foe, p, 30, false);
        Check(p.Hp == hp && foe.Action == UnitAction.Stagger, "a chambered blow does no harm");
    }

    static void RangedRoles()
    {
        var b = Battle.Empty(11);
        var xb = b.AddSquad(0, Troops.Get("crossbowmen"), 1, new(0, 0), 1, Vector2.UnitY).Units[0];
        var bow = b.AddSquad(0, Troops.Get("bowmen"), 1, new(4, 0), 1, Vector2.UnitY).Units[0];
        var knight = b.AddSquad(1, Troops.Get("ironguard"), 1, new(0, 20), 1, -Vector2.UnitY).Units[0];
        knight.Type = knight.Type.Clone();
        knight.Type.BlockChance = 0;
        float h0 = knight.Hp; b.Hit(xb, knight, xb.Type.Damage, true); float bolt = h0 - knight.Hp;
        h0 = knight.Hp; b.Hit(bow, knight, bow.Type.Damage, true); float arrow = h0 - knight.Hp;
        Check(bolt > arrow * 2, $"a bolt punches through armour an arrow cannot ({bolt:0} vs {arrow:0})");
        var c = Battle.Empty(12);
        var ha = c.AddSquad(0, Troops.Get("horse_archers"), 6, new(0, -10), 6, Vector2.UnitY);
        ha.Order = Order.Charge;
        c.AddSquad(1, UnitType.Shieldman, 12, new(0, 15), 6, -Vector2.UnitY);
        Run(c, 12);
        Check(ha.Units.Sum(u => u.Type.Ammo - u.Ammo) > 6, "horse archers shoot from the saddle");
        Check(ha.Units.All(u => u.Mounted), "and keep their distance (no horse lost)");
    }

    static void FatigueAndSupply()
    {
        var b = Battle.Empty(13);
        var runners = b.AddSquad(0, UnitType.Shieldman, 4, new(0, 0), 4, Vector2.UnitY);
        foreach (var u in runners.Units) u.Wish = Vector2.UnitY * u.Type.Speed;
        runners.Anchor = new(0, 70);
        Run(b, 20);
        Check(runners.Units.All(u => u.Stamina < 80), "running tires men");
        var archers = b.AddSquad(0, UnitType.Bowman, 4, b.Wagons[0], 4, Vector2.UnitY);
        foreach (var u in archers.Units) u.Ammo = 0;
        Run(b, 5);
        Check(archers.Units.All(u => u.Ammo > 0) && b.WagonStock[0] < 300, "archers at the wagon draw arrows");
        var w = new Weather(Hour: 23, Rain: 1, Fog: 0);
        Check(w.Night && w.Vision <= 40 && w.Spread > 1.5f && w.MudFactor < 1, "night and rain blind archers and bog horses");
    }

    /// A battlefield river: carved below its banks, slow to wade, quicker at the fords; the map has rivers with bridges.
    static void Rivers()
    {
        var t = new Terrain(21, TerrainParams.Meadow with { River = true });
        var r = t.Rivers.Single();
        var mid = r.Path[65];
        Check(t.Height(mid) < t.Height(mid + new Vector2(0, 12)) - 0.8f || t.Height(mid) < t.Height(mid - new Vector2(0, 12)) - 0.8f, "the river runs below its banks");
        Check(t.WaterDepth(mid) > 1 && t.WaterDepth(r.Fords[0]) is > 0 and < 0.5f, "deep in the stream, shallow at a ford");
        Check(!t.Foliage.Any(f => f.Radius > 0 && t.WaterDepth(f.Pos) > 0), "no trees in the water");
        float Cross(Vector2 at)
        {
            var b = new Battle(3, new ArmySpec("a", 1, new List<Company>()), new ArmySpec("b", 1, new List<Company>()), new BattleOptions { Ground = TerrainParams.Meadow with { River = true }, PlayerWounded = true });
            var s = b.AddSquad(0, UnitType.Shieldman, 1, at - new Vector2(0, 8), 1, Vector2.UnitY);
            s.Anchor = at + new Vector2(0, 8);
            float time = 0;
            while (s.Units[0].Pos.Y < at.Y + 6 && time < 30) { b.Update(1 / 60f); time += 1 / 60f; }
            return time;
        }
        var br = new Terrain(3, TerrainParams.Meadow with { River = true }).Rivers.Single();
        float deep = Cross(br.Path[50]), ford = Cross(br.Fords[0]);
        Console.WriteLine($"river: wading takes {deep:0.0}s, the ford {ford:0.0}s");
        Check(deep > ford * 1.2f, "a ford is the quick way over");
        var m = WorldMap.Generate(11);
        Check(m.Rivers.Count == 2 && m.Rivers.All(rv => rv.Fords.Length > 0), "the map has rivers with bridges");
        Check(m.Settlements.All(s => m.RiverDist(s.Pos) > 4), "no settlement stands in a river");
    }

    /// Two AI generals: the reserve stays back until it is needed, and the field still resolves.
    static void Generals()
    {
        var rng = new Random(3);
        var a = Armies.Random(rng, 2, 0);
        a = a with { Companies = a.Companies.Concat(new[] { new Company("shieldmen", 16, "infantry"), new Company("shieldmen", 12, "infantry") }).ToList() };
        var b = new Battle(14, a, Armies.Random(rng, 2, 1), new BattleOptions { AutoPlayer = true, PlayerWounded = true });
        bool heldBack = false;
        for (int step = 0; b.Winner < 0 && step < 60 * 600; step++)
        {
            b.Update(1 / 60f);
            var inf = b.SquadsOf(0).Where(s => s.Company != null && s.Alive > 0 && Armies.RoleOf(s.Company) == "infantry").ToList();
            if (inf.Any(s => s.Order == Order.Charge) && inf.Any(s => s.Order == Order.Hold)) heldBack = true; // one company waits while the line charges
        }
        Check(b.Winner >= 0, "an AI-vs-AI battle with a reserve resolves");
        Check(heldBack, "the AI keeps an infantry reserve back");
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
        var reachable = Troops.Tree("levies").Concat(Troops.Tree("clansmen")).Select(t => t.Id).ToHashSet();
        Check(Troops.All.Where(t => !t.Leader).All(t => reachable.Contains(t.Id)), "every troop can be reached by upgrading a culture's recruits");
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
            new BattleOptions { AutoPlayer = true, PlayerWounded = true, Cap = 200 });
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
        // Roadmap E1: 400 men on the field still run faster than real time.
        var h = new Battle(6, Scaled(Armies.Random(rng, 3, 0), 3.4f, 300), Scaled(Armies.Random(rng, 3, 1), 3.4f, 400),
            new BattleOptions { AutoPlayer = true, PlayerWounded = true });
        int huge = h.Units.Count(u => !u.IsLeader);
        for (int i = 0; i < 120; i++) h.Update(1 / 60f); // warm up
        watch.Restart();
        for (int i = 0; i < 600; i++) h.Update(1 / 60f);
        double htps = 600 / watch.Elapsed.TotalSeconds;
        Console.WriteLine($"huge battle: {huge} men fielded, {htps:0} ticks/s");
        Check(huge is > 320 and <= 410, $"the field holds about 400 men ({huge})");
        Check(htps >= 60, $"a 400-man battle runs in real time ({htps:0} ticks/s)");
    }

    static void CampaignMap()
    {
        var m = WorldMap.Generate(11);
        Check(m.Provinces.Count >= 18, $"enough provinces ({m.Provinces.Count})");
        for (int f = 0; f < 3; f++) Check(m.Settlements.Any(s => s.Faction == f && s.Kind == SettlementKind.Town), $"faction {f} has towns");
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
        Console.WriteLine($"campaign: day {c.Day}, {c.S.Parties.Count} parties, {upgraded} lord companies at tier III+, your battles {battles}, war score {string.Join("/", c.S.Factions.Select(f => f.WarScore.ToString("0")))}, wars {c.S.Wars.Count(w => w.Active)}");
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
        // Your own siege, on paper: a strong company takes a castle and holds it as a fief.
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && s.Faction == 1).OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).First();
        c.Player.Companies.Clear();
        foreach (var t in new[] { "men_at_arms", "men_at_arms", "longbowmen", "pikemen" }) c.Player.Companies.Add(c.NewCompany(t, Armies.RoleOf(new Company(t, 1)), Rules.Cap(t, "infantry"), 0));
        c.Player.Pos = castle.Pos + new Vector2(3, 0);
        foreach (var p in c.S.Parties.Where(p => p != c.Player && Vector2.Distance(p.Pos, castle.Pos) < 12)) p.Pos += new Vector2(40, 40);
        Check(c.CanBesiege(castle, out string why), "you can besiege an enemy castle: " + why);
        c.Besiege(castle);
        c.Rest(13);
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

    /// A version-1 save (one war, no Northmarch) loads as version 2.
    static void Migration()
    {
        var c = Campaign.New(71);
        var json = c.S.Serialize().Replace("\"Version\": 2", "\"Version\": 1").Replace("\"AtWar\": null", "\"AtWar\": false").Replace("\"TruceUntil\": 0,", "\"TruceUntil\": 1000,");
        var s = CampaignState.Deserialize(json);
        Check(s.Version == CampaignState.CurrentVersion && s.Wars.Count == 1 && !s.Wars[0].Active, "a v1 save migrates its war to the new format");
        var m = new Campaign(s);
        m.Player.Cooldown = 1e6f; // nobody may stop the clock
        for (int h = 0; h < 48; h++) { m.Tick(1); m.Encounter = null; m.Arrived = null; }
        Console.WriteLine($"migrated: day {m.Day}, wars {string.Join(" ", m.S.Wars.Select(w => $"{w.A}-{w.B}:{w.Active}:{w.TruceUntil}"))}");
        Check(m.Day >= 2 && m.FactionsAtWar(0, 1) == false, "a migrated campaign runs, its truce intact");
    }

    /// A recorded battle with player orders and input, saved to JSON and played back, ends exactly the same.
    static void ReplayExact()
    {
        var rng = new Random(9);
        var b = new Battle(31, Armies.Random(rng, 2, 0), Armies.Random(rng, 2, 1), new BattleOptions { Weather = new Weather(17, 0.5f) });
        b.StartRecording(31);
        var mine = b.Commanded.ToList();
        for (int step = 0; b.Winner < 0 && step < 60 * 300; step++)
        {
            if (step == 60) foreach (var s in mine) { s.Order = Order.Charge; }
            if (step == 120) mine[0].Formation = Formation.ShieldWall;
            if (step == 200) mine[^1].Target = b.SquadsOf(1).First();
            b.Input = new PlayerInput { Facing = MathHelper.PiOver2, Move = new Vector2(0, step % 300 < 150 ? 1 : 0), Attack = step % 50 < 20, Dir = (AttackDir)(step / 70 % 4) };
            b.Update(1 / 60f);
        }
        var rec = b.FinishRecording()!;
        var back = Replay.Deserialize(rec.Serialize());
        var again = back.Play();
        Console.WriteLine($"replay: {rec.Frames.Count} ticks, winner {again.Winner} after {again.Time:0.0}s, {again.Units.Count(u => u.Dead)} dead (recorded {rec.Winner}, {rec.Time:0.0}s, {rec.Dead})");
        Check(back.Matches(again), "a replay reproduces its battle exactly");
    }

    static ArmySpec Ids(ArmySpec a, int idBase) => a with { Companies = a.Companies.Select((c, i) => c with { Id = idBase + i }).ToList() };

    static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("selftest failed: " + what);
    }
}
