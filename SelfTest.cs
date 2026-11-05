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

    static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("selftest failed: " + what);
    }
}
