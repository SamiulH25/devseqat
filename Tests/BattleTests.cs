using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the battle sim: mechanics scenarios (charges, parries, flanks, cavalry, ranged), troops, sprites, full battles, replays.
public static partial class SelfTest
{
    /// Animation curves and poses (docs/archive/VISUALS.md V1-V2): easing keeps its ends, a windup ends raised, a strike overshoots,
    /// and the drawn pose never touches the sim (a killed man only remembers where the blow came from).
    static void Animation()
    {
        foreach (var f in new Func<float, float>[] { Ease.In, Ease.Out, Ease.OutCubic, Ease.InOut, t => Ease.OutBack(t), t => Ease.Spring(t) })
            Check(MathF.Abs(f(0)) < 0.01f && MathF.Abs(f(1) - 1) < 0.02f, "easing curves run from 0 to 1");
        for (float t = 0; t < 1; t += 0.05f) Check(Ease.InOut(t + 0.05f) >= Ease.InOut(t) && Ease.Out(t + 0.05f) >= Ease.Out(t), "InOut and Out never go back");
        Check(Ease.OutBack(0.6f) > 1, "OutBack overshoots (the follow-through)");
        Check(Ease.Kick(0) == 0 && Ease.Kick(1) < 0.01f && Ease.Kick(0.25f) > 0.5f, "a kick rises and falls back");
        Check(Ease.Hash(1) != Ease.Hash(2) && Ease.Hash(7) == Ease.Hash(7), "the per-man phase is stable and varied");
        var b = Battle.Empty(1);
        var u = b.AddSquad(0, UnitType.Shieldman, 1, Vector2.Zero, 1, Vector2.UnitY).Units[0];
        var (_, raise, strike) = Puppet.Keys(Look.GripOf(u.Type.Look.Weapon), AttackDir.Right);
        u.Action = UnitAction.Windup; u.Swing = AttackDir.Right; u.ActionTimer = u.Type.Windup * 0.1f;
        Check(MathF.Abs(Puppet.PoseOf(u, 1, 0.5f).ArmA - raise.Alpha) < 1, "the windup holds at the top");
        u.Action = UnitAction.Recover; u.ActionTimer = u.Type.Recover * (1 - 0.15f);
        float through = Puppet.PoseOf(u, 1, 0.5f).Yaw;
        Check(through < strike.Yaw, $"the strike swings past its mark ({through:0} < {strike.Yaw:0})");
        u.Action = UnitAction.None; u.Routing = true;
        Check(!Puppet.PoseOf(u, 1, 0.5f).Armed, "a routed man has thrown his weapon away");
        var two = b.AddSquad(0, Troops.Get("greatswords"), 1, new Vector2(4, 0), 1, Vector2.UnitY).Units[0];
        Check(Puppet.PoseOf(two, 1, 0.5f).TwoHand, "a greatsword is held in both hands");
        Check(!Puppet.PoseOf(b.AddSquad(0, Troops.Get("dune_spears"), 1, new Vector2(6, 0), 1, Vector2.UnitY).Units[0], 1, 0.5f).TwoHand,
            "a spear with a shield is held in one hand");
        var rider = b.AddSquad(0, Troops.Get("mamluks"), 1, new Vector2(8, 0), 1, Vector2.UnitY).Units[0];
        rider.Action = UnitAction.Recover; rider.Swing = AttackDir.Thrust; rider.ActionTimer = rider.Type.Recover * 0.8f;
        Check(Puppet.PoseOf(rider, 1, 0.2f).WeapB < 0, "a sword in the saddle cuts down, it does not thrust");
        var xbow = b.AddSquad(0, Troops.Get("crossbowmen"), 1, new Vector2(10, 0), 1, Vector2.UnitY).Units[0];
        xbow.Ammo = 5; xbow.EnemyDist = 20; xbow.Cooldown = 3;
        Check(Puppet.PoseOf(xbow, 1, 0.5f).Haul >= 0, "a crossbowman spans his bow between shots");
        xbow.Cooldown = 0.01f;
        Check(Puppet.PoseOf(xbow, 1, 0.5f) is { Aim: true, Draw: > 0.95f, Haul: < 0 }, "then levels it at the shoulder");
        var jav = b.AddSquad(0, Troops.Get("sun_javelins"), 1, new Vector2(12, 0), 1, Vector2.UnitY).Units[0];
        jav.Ammo = 3; jav.EnemyDist = 15; jav.Cooldown = 0.1f;
        Check(Puppet.PoseOf(jav, 1, 0.5f).ArmA > 160, "a javelin is drawn back over the shoulder");
        jav.Action = UnitAction.Recover; jav.ActionTimer = 0.1f;
        Check(Puppet.PoseOf(jav, 1, 0.5f).Empty, "and leaves the hand");
        var mage = b.AddSquad(0, Troops.Get("hedge_witches"), 1, new Vector2(14, 0), 1, Vector2.UnitY).Units[0];
        mage.CastStart = 0.5f; mage.CastFire = 2;
        Check(Puppet.PoseOf(mage, 1.5f, 0.5f) is { Cast: > 0 and < 1, ArmA: > 120 }, "a caster lifts his staff as he gathers a spell");
        var wall = b.AddSquad(0, UnitType.Shieldman, 1, new Vector2(16, 0), 1, Vector2.UnitY).Units[0];
        wall.Squad!.Formation = Formation.ShieldWall;
        Check(Puppet.PoseOf(wall, 1, 0.5f) is { ShieldUp: true, Crouch: > 0 }, "a shield wall crouches behind its shields");
        var pike = b.AddSquad(0, Troops.Get("pikemen"), 1, new Vector2(18, 0), 1, Vector2.UnitY).Units[0];
        float upright = Puppet.PoseOf(pike, 1, 0.5f).WeapB;
        pike.EnemyDist = 7; pike.Rank = 0;
        Check(Puppet.PoseOf(pike, 1, 0.5f).WeapB < upright - 40, "the front rank sets its pikes at a foe close by");
        pike.Rank = 4;
        Check(Puppet.PoseOf(pike, 1, 0.5f).WeapB > upright - 10, "the ranks behind hold theirs up");
        var bow = b.AddSquad(0, Troops.Get("longbowmen"), 4, new Vector2(20, 0), 2, Vector2.UnitY).Units[0];
        bow.Ammo = 5; bow.EnemyDist = 20; bow.Rank = 0; bow.Squad!.Alive = 4;
        Check(Puppet.PoseOf(bow, 1, 0.5f).Kneel, "the front rank of bows kneels to shoot");
        var foe = b.AddSquad(1, UnitType.Shieldman, 1, new Vector2(0, 2), 1, -Vector2.UnitY).Units[0];
        b.Kill(foe, u);
        Check(foe.Dead && foe.DiedFrom == u.Pos, "a dead man remembers where the blow came from");
    }

    /// Javelin men throw what they carry as the foe comes on, then close with the last one in hand (docs/ANIMATION_ROSTER.md R1).
    static void Javelins()
    {
        var b = Battle.Empty(3);
        var jav = b.AddSquad(0, Troops.Get("sun_javelins"), 12, new Vector2(0, -12), 6, Vector2.UnitY);
        var foe = b.AddSquad(1, Troops.Get("levies"), 12, new Vector2(0, 12), 6, -Vector2.UnitY);
        jav.Order = foe.Order = Order.Charge; jav.Target = foe; foe.Target = jav;
        bool flew = false;
        for (int i = 0; i < 60 * 20 && !flew; i++) { b.Update(1 / 60f); flew |= b.Arrows.Any(a => a.Kind == Grip.Thrown && a.Stuck < 0); }
        Check(flew, "javelins fly as javelins");
        for (int i = 0; i < 60 * 25; i++) b.Update(1 / 60f);
        Check(jav.Units.Where(u => u.Active).All(u => u.Ammo < jav.Units[0].Type.Ammo), "every javelin man has thrown");
        Check(b.Units.Any(u => u.Team == 1 && u.Dead), "the javelins and the spears kill");
    }

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

    /// Balance guards: a horse is not a free win, and a broken company can come back instead of always dissolving.
    static void CavalryBalance()
    {
        // A rider's reach in a standing fight is a man's reach, not its lance's: foot can hit back.
        Check(Battle.MountedMeleeReach < UnitType.Lancer.Reach && Battle.MountedMeleeReach >= UnitType.Shieldman.Reach,
            $"a rider reaches as far as a footman, not as far as its lance ({Battle.MountedMeleeReach})");

        // The same charge costs the horse more against a set shield wall than against loose ranks.
        float HorseLoss(Formation f)
        {
            var b = Battle.Empty(40);
            var foot = b.AddSquad(0, UnitType.Shieldman, 12, new(0, 0), 6, Vector2.UnitY);
            foot.Formation = f;
            var horse = b.AddSquad(1, Troops.Get("knights"), 5, new(0, 24), 5, -Vector2.UnitY);
            horse.Order = Order.Charge;
            float before = horse.Units.Sum(u => u.HorseHp);
            Run(b, 20);
            return before - horse.Units.Sum(u => u.Active ? u.HorseHp : 0);
        }
        float wall = HorseLoss(Formation.ShieldWall), open = HorseLoss(Formation.Loose);
        Console.WriteLine($"knights into a shield wall lose {wall:0} horse hp, into loose ranks {open:0}");
        Check(wall > open, $"a set shield wall hurts the horse more than loose ranks ({wall:0} vs {open:0})");

        // Morale: a broken man's nerve returns once he is clear of the fight, but a man who breaks twice is done.
        var m = Battle.Empty(50);
        var squad = m.AddSquad(0, UnitType.Shieldman, 1, new(0, 0), 1, Vector2.UnitY);
        m.AddSquad(1, UnitType.Shieldman, 1, new(0, 20), 1, -Vector2.UnitY);
        var man = squad.Units[0];
        man.Pos = new Vector2(0, -40); // clear of the enemy, with room before the map's edge
        man.Routing = true;
        man.Routs = 1;
        man.Morale = 20;
        Run(m, 10);
        Check(!man.Routing && man.Morale > Battle.RallyAt, $"a broken man comes back once he is clear ({man.Morale:0})");
        man.Routing = true;
        man.Routs = 2;
        man.Morale = 5;
        Run(m, 2);
        Check(man.Routing, "a man who breaks twice stays broken");
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

    /// The captain's kit in battle (P1): a thrusting spear, a crossbow as the second weapon with its own bolts, a mace through plate.
    static void CaptainKit()
    {
        var cap = new CaptainState();
        cap.Kit.Weapon = "boar_spear";
        cap.Kit.Side = "crossbow";
        var b = Battle.Empty(12, new BattleOptions { PlayerType = Rules.CaptainType(cap), PlayerSide = Rules.CaptainType(cap, side: true) });
        var p = b.Player;
        p.Horse = null;
        float up = MathHelper.PiOver2;
        b.Input = new PlayerInput { Facing = up, Attack = true, Dir = AttackDir.Left };
        b.Update(1 / 60f);
        Check(p.Charging && p.Swing == AttackDir.Thrust, "the boar spear only thrusts");
        b.Input = new PlayerInput { Facing = up };
        Run(b, 1);
        b.Input = new PlayerInput { Facing = up, Swap = true };
        b.Update(1 / 60f);
        b.Input = new PlayerInput { Facing = up };
        Run(b, 0.6f);
        Check(p.Type.Look.Weapon == "crossbow" && p.Ammo == 16 && b.TwoHanded, "swapping takes up the crossbow, with its 16 bolts");
        var foe = b.AddSquad(1, Troops.Get("levies"), 1, p.Pos + new Vector2(0, 25), 1, -Vector2.UnitY).Units[0];
        foe.Type = foe.Type.Clone();
        foe.Type.BlockChance = 0;
        int shots = 0;
        while (shots < 6 && foe.Hp >= foe.Type.MaxHp && !foe.Dead)
        {
            b.Input = new PlayerInput { Facing = up, Attack = true };
            Run(b, 0.6f);
            b.Input = new PlayerInput { Facing = up };
            Run(b, 2.2f);
            shots++;
        }
        Check(p.Ammo == 16 - shots, $"each shot costs a bolt ({p.Ammo} left after {shots})");
        Check(foe.Hp < foe.Type.MaxHp || foe.Dead, $"the captain's bolts find the man in front ({shots} shots)");
        Check(b.ArrowHits >= 1 && b.Result().ArrowHits == b.ArrowHits, "your arrow hits are counted for Bowcraft");
        b.Input = new PlayerInput { Facing = up, Swap = true };
        b.Update(1 / 60f);
        b.Input = new PlayerInput { Facing = up };
        Run(b, 0.6f);
        Check(p.Type.Look.Weapon == "spear" && p.Ammo == 0, "back to the spear");
        // A mace and a sword strike a knight with the same blow: the mace does more through his plate.
        float Blow(string weapon)
        {
            var k = Battle.Empty(13);
            var me = k.Player;
            me.Type = Rules.CaptainType(new CaptainState { Kit = { Weapon = weapon } });
            var knight = k.AddSquad(1, Troops.Get("ironguard"), 1, me.Pos + new Vector2(0, 1.5f), 1, -Vector2.UnitY).Units[0];
            knight.Type = knight.Type.Clone();
            knight.Type.BlockChance = 0;
            float hp = knight.Hp;
            k.Hit(me, knight, 30, false);
            return hp - knight.Hp;
        }
        float mace = Blow("mace"), sword = Blow("arming_sword");
        Check(mace > sword * 1.3f, $"a mace bites through plate ({mace:0.0} against the sword's {sword:0.0})");
    }

    /// Skill beats spam (roadmap C3, PROGRESSION P2): a scripted captain who parries and ripostes against two tier III
    /// men-at-arms, and one who only swings, ten bouts each; then the riposte and the Arms skill on their own.
    static void SkillBouts()
    {
        int parried = 0, spammed = 0;
        for (int seed = 0; seed < 10; seed++)
        {
            if (Bout(300 + seed, parry: true, men: 2)) parried++;
            if (Bout(300 + seed, parry: false, men: 2)) spammed++;
        }
        Console.WriteLine($"bouts: a parrying captain wins {parried}/10, a swinging one {spammed}/10");
        Check(parried >= 8, $"a captain who parries and ripostes wins most bouts against two men-at-arms ({parried}/10)");
        Check(spammed <= 5, $"a captain who only swings loses at least half ({spammed}/10)");

        // A parry opens a riposte: a quick blow a tier III man cannot block.
        var b = Battle.Empty(14);
        var p = b.Player;
        p.Horse = null;
        var foe = b.AddSquad(1, Troops.Get("men_at_arms"), 1, p.Pos + new Vector2(0, 1.6f), 1, -Vector2.UnitY).Units[0];
        float up = MathHelper.PiOver2;
        foe.Swing = AttackDir.Left;
        b.Input = new PlayerInput { Facing = up, Block = true, Dir = Dirs.Mirror(foe.Swing) };
        b.Update(1 / 60f);
        b.Hit(foe, p, 26, false);
        Check(b.Riposting && foe.Action == UnitAction.Stagger, "a parry staggers him and opens a riposte");
        foe.Action = UnitAction.None; // he recovers at once, guard up
        b.Input = new PlayerInput { Facing = up, Attack = true, Dir = AttackDir.Right };
        b.Update(1 / 60f);
        b.Input = new PlayerInput { Facing = up };
        b.Update(1 / 60f);
        float windup = p.ActionTimer;
        Run(b, 0.3f);
        Check(foe.Hp < foe.Type.MaxHp, "the riposte slips his guard");
        Check(!b.Riposting, "and is spent with one blow");
        // Arms: a practised captain's blows come quicker and his parry window is wider.
        var green = Battle.Empty(15);
        var old = Battle.Empty(15, new BattleOptions { Arms = 100 });
        Check(old.ArmsEdge == 1 && green.ArmsEdge == 0, "Arms 15 is a new captain, 100 a master");
        float Windup(Battle k)
        {
            k.Player.Horse = null;
            k.Input = new PlayerInput { Facing = up, Attack = true };
            k.Update(1 / 60f);
            k.Input = new PlayerInput { Facing = up };
            k.Update(1 / 60f);
            return k.Player.ActionTimer;
        }
        Check(Windup(old) < Windup(green) * 0.9f, "a master's blows come quicker");
        Check(windup < Windup(Battle.Empty(16)) * 0.6f, "a riposte winds up in half the time");
    }

    /// One bout on flat ground against `men` men-at-arms; true if the captain is standing when the last falls.
    static bool Bout(int seed, bool parry, int men = 1)
    {
        var b = Battle.Empty(seed);
        var p = b.Player;
        p.Horse = null;
        b.EnemyCaptain.Pos = new Vector2(0, 400); // the enemy's own captain stays out of it
        var foes = b.AddSquad(1, Troops.Get("men_at_arms"), men, p.Pos + new Vector2(0, 3), men, -Vector2.UnitY).Units;
        bool won = Fight(b, foes, parry);
        if (Environment.GetEnvironmentVariable("BOUT_DEBUG") != null) Console.WriteLine($"bout {seed} parry={parry} men={men}: t={b.Time:0.0} hp {p.Hp:0}/{p.Type.MaxHp:0} left {foes.Count(f => f.Active)} stam {p.Stamina:0} chain {b.Chain}");
        return won;
    }

    /// A scripted captain fights `foes` on foot for up to a minute: parrying and riposting, or only swinging.
    /// True if he is standing when the last of them falls.
    static bool Fight(Battle b, IReadOnlyList<Unit> foes, bool parry, bool aura = false)
    {
        var p = b.Player;
        float hold = 0;
        for (int i = 0; i < 60 * 60 && p.Active && foes.Any(f => f.Active); i++)
        {
            var foe = foes.Where(f => f.Active).MinBy(f => Vector2.DistanceSquared(f.Pos, p.Pos))!;
            var threat = foes.Where(f => f.Active && f.Action == UnitAction.Windup && f.Target == p).MinBy(f => f.ActionTimer);
            if (threat != null) foe = threat;
            var d = foe.Pos - p.Pos;
            var inp = new PlayerInput { Facing = MathF.Atan2(d.Y, d.X), Dir = AttackDir.Left };
            if (d.Length() > p.Type.Reach) inp.Move = Vector2.Normalize(d);
            if (parry && threat != null)
            {
                // Read the blow and meet it from the mirrored side just before it lands.
                if (threat.ActionTimer < 0.25f) { inp.Block = true; inp.Dir = Dirs.Mirror(threat.Swing); }
                hold = 0;
            }
            else if (!parry || foe.Action is UnitAction.Stagger or UnitAction.Recover || b.Riposting)
            {
                // Swing: the spammer always, the parrier into an opening.
                hold += 1 / 60f;
                inp.Attack = hold < (parry ? 0.12f : 0.3f);
                if (!inp.Attack) hold = 0;
                inp.Dir = (AttackDir)(i / 40 % 4);
            }
            inp.Aura = aura && (b.AuraBlade ? b.Aura > Battle.AuraLow + 10 : b.Aura > 60); // light the blade when full, let it rest before the pool is spent
            b.Input = inp;
            b.Update(1 / 60f);
        }
        return p.Active && foes.All(f => !f.Active);
    }

    /// Single combat (P2): the two leaders alone on foot; the lord comes for you; a fall is a yield.
    static void BattleGoals()
    {
        Battle Make(bool goals) => new(51, Armies.Standard(0), Armies.Standard(1), new BattleOptions { Goals = goals, AutoPlayer = true, Cap = 200 });
        var b = Make(true);
        Check(b.Goals.Count == 2 && b.Goals.Distinct().Count() == 2, "a field battle draws two personal goals");
        Check(Make(true).Goals.SequenceEqual(b.Goals), "the same battle draws the same goals");
        Check(Make(false).Goals.Count == 0, "no goals unless asked for");
        Check(new Battle(51, Armies.Standard(0), Armies.Standard(1), new BattleOptions { Goals = true, Duel = true }).Goals.Count == 0, "none in single combat");
        // Live state: the hand goal fills with your kills; the quick win lapses after three minutes.
        Check(b.GoalState(Battle.Goal("hand")) == 0, "a goal starts open");
        b.PlayerKills = Battle.HandKills;
        Check(b.GoalState(Battle.Goal("hand")) == 1, "five by your own hand");
        b.Time = Battle.QuickWin + 1;
        Check(b.GoalState(Battle.Goal("quick")) == -1, "too late for a quick win");
        // To the end: the result names the goals and those met.
        var c = Make(true);
        for (int i = 0; i < 60 * 600 && c.Winner < 0; i++) c.Update(1 / 60f);
        var r = c.Result();
        Check(r.Goals.SequenceEqual(c.Goals.Select(g => g.Id)) && r.GoalsMet.All(r.Goals.Contains) && c.Goals.All(g => c.GoalState(g) != 0),
            $"every goal is settled at the end ({string.Join(",", r.GoalsMet)} of {string.Join(",", r.Goals)})");
    }

    static void BannerAndLastStand()
    {
        var b = new Battle(52, Armies.Standard(0), Armies.Standard(1), new BattleOptions { Banner = true, Goals = true, Cap = 200 });
        Check(b.Bearer is { Team: 0, IsPlayer: false }, "a man of yours carries your banner");
        var bearer = b.Bearer!;
        var mate = bearer.Squad!.Units.First(u => u != bearer);
        b.Update(1 / 60f);
        float morale = mate.Morale;
        bearer.Dead = true; // cut down
        b.Update(1 / 60f);
        Check(b.BannerDown && b.BannerEverFell && mate.Morale < morale - 5, $"the banner falls with him, and the men lose heart ({b.BannerDown} {morale:0} -> {mate.Morale:0})");
        b.Player.Pos = b.BannerPos;
        b.Update(1 / 60f);
        Check(!b.BannerDown && b.Bearer == b.Player, "you pick it up");
        Check(!b.Goals.Any(g => g.Id == "banner") || b.GoalState(Battle.Goal("banner")) == -1, "a banner that fell is a goal lost");
        // Left alone, a fallen banner is picked up by the nearest man.
        var c = new Battle(53, Armies.Standard(0), Armies.Standard(1), new BattleOptions { Banner = true, Cap = 200 });
        c.Bearer!.Dead = true;
        for (int i = 0; i < 60 * 5; i++) c.Update(1 / 60f);
        Check(c.Bearer is { IsPlayer: false, Active: true } && !c.BannerDown, "after a moment one of your men raises it");
        // The last stand: down, then rally to fight on at a quarter of your health; a second fall is the end.
        var d = Battle.Empty(54, new BattleOptions { LastStand = true });
        d.Hit(d.EnemyCaptain, d.Player, 5000, false);
        Check(d.Player.Active && d.DownSince >= 0, "your first fall puts you down, not out");
        d.Input = new PlayerInput { Rally = true };
        d.Update(1 / 60f);
        Check(d.DownSince < 0 && d.FoughtOn && MathF.Abs(d.Player.Hp - d.Player.Type.MaxHp * Battle.LastStandHealth) < 1, "rally to fight on at a quarter of your health");
        d.Input = new PlayerInput();
        d.Hit(d.EnemyCaptain, d.Player, 5000, false);
        Check(!d.Player.Active, "a second fall is the end");
        Check(d.Result().Captain != LeaderFate.Unhurt, "fighting on still counts as a fall");
        var e = Battle.Empty(55, new BattleOptions { LastStand = true });
        e.Hit(e.EnemyCaptain, e.Player, 5000, false);
        for (int i = 0; i < 60 * 5; i++) e.Update(1 / 60f);
        Check(!e.Player.Active && e.PlayerKills == 0, "without a rally you are carried off when the moment passes");
    }

    static void DuelFight()
    {
        int won = 0;
        for (int seed = 0; seed < 6; seed++)
        {
            var none = new ArmySpec("none", 0, new List<Company>());
            var lordParty = new Party { Tier = 4 };
            var b = new Battle(400 + seed, none, none, new BattleOptions { Duel = true, Couriers = false, EnemyLeader = Campaign.LordChampion(lordParty), PlayerMount = Mount.Warhorse });
            Check(!b.Player.Mounted && b.Units.Count(u => u.Active) == 2, "a duel is the two leaders, on foot");
            Check(b.EnemyCaptain.Type.MaxHp > UnitType.Captain.MaxHp, "a lord of a strong army fights better than a plain captain");
            bool w = Fight(b, new[] { b.EnemyCaptain }, parry: true);
            for (int i = 0; i < 120 && b.Winner < 0; i++) b.Update(1 / 60f);
            var r = b.Result();
            Check(b.Winner >= 0, "the duel ends");
            Check(r.Captain != LeaderFate.Captured && r.EnemyLeader != LeaderFate.Slain, "a fall in single combat is a yield, not a death or capture");
            if (w) won++;
        }
        Console.WriteLine($"duels: a parrying captain beats a tier IV lord {won}/6");
        Check(won >= 3, $"a captain who parries well wins most duels against a strong lord ({won}/6)");
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

    /// The ground layer (docs/archive/TERRAIN.md T0): the data validates, the same seed gives the same ground, and the layer marks
    /// what is on the field: water and fords on the river, the road, the woods; grass on a flat test field.
    static void GroundLayer()
    {
        Check(!Grounds.Validate().Any(), "ground.json validates: " + string.Join("; ", Grounds.Validate()));
        Check(Grounds.All.Count == Enum.GetValues<GroundKind>().Length, "every kind of ground has its rules");
        var p = TerrainParams.Meadow with { River = true };
        var a = new Terrain(21, p);
        Check(a.GroundHash() == new Terrain(21, p).GroundHash() && a.GroundHash() != new Terrain(22, p).GroundHash(), "the same seed gives the same ground");
        var r = a.Rivers.Single();
        var open = r.Path.Skip(55).First(q => !r.AtBridge(q) && !r.AtFord(q) && !r.AtBridge(q + new Vector2(3, 0)) && !r.AtBridge(q - new Vector2(3, 0)));
        Check(a.GroundAt(open) == GroundKind.Water && a.GroundAt(r.Fords[0]) == GroundKind.Ford, "the river is water, shallow at its fords");
        var road = Enumerable.Range(0, 40).Select(i => new Vector2(-200 + i * 10, a.RoadCenter(-200 + i * 10))).First(q => a.Rivers.All(rv => rv.Dist(q) > 12));
        Check(a.GroundAt(road) == GroundKind.Road, "the road is road (away from the river: no bridges yet)");
        var trunks = a.Foliage.Where(f => f.Kind is FoliageKind.Pine or FoliageKind.Broadleaf && f.Radius > 0).ToList();
        int inWood = trunks.Count(f => a.GroundAt(f.Pos) == GroundKind.Wood);
        Check(inWood > trunks.Count / 2, $"most trees stand on wood ground ({inWood} of {trunks.Count})");
        Check(a.GroundAt(Vector2.Zero) is not (GroundKind.Wood or GroundKind.DenseWood), "the middle of the field is open");
        var flat = Terrain.Flat();
        Check(flat.GroundAt(new Vector2(30, 30)) == GroundKind.Grass && flat.CoverAt(Vector2.Zero) == 0, "a flat test field is all grass");
        var b = Battle.Empty(3);
        b.StartRecording(3);
        Check(b.Recording!.TerrainVersion == Terrain.Version && Replay.Deserialize("{\"Seed\":1}").TerrainVersion == 0, "a replay keeps the ground version it was made on");
    }

    /// Ground that changes how men move (docs/archive/TERRAIN.md T1): a wood slows horse far more than foot, a road is quick,
    /// mud tires an armoured man, a slope tells on a horse more than on a man.
    static void GroundMoves()
    {
        float Ride(UnitType t, GroundKind k, Func<Battle, Unit, float>? measure = null)
        {
            var b = Battle.Empty(5);
            b.Terrain.Paint(new Vector2(40, 0), 30, k);
            var s = b.AddSquad(0, t, 1, new(40, -10), 1, Vector2.UnitY);
            s.Order = Order.Hold; s.Anchor = new(40, 60); s.Front = Vector2.UnitY;
            var u = s.Units[0];
            if (u.Mounted) u.Speed = u.Horse!.Gallop;
            u.Stamina = 50;
            Run(b, 2);
            return measure?.Invoke(b, u) ?? u.Pos.Y + 10;
        }
        float horseGrass = Ride(UnitType.Lancer, GroundKind.Grass), horseWood = Ride(UnitType.Lancer, GroundKind.Wood);
        float footGrass = Ride(UnitType.Shieldman, GroundKind.Grass), footWood = Ride(UnitType.Shieldman, GroundKind.Wood), footRoad = Ride(UnitType.Shieldman, GroundKind.Road);
        Console.WriteLine($"ground: 2 s ride grass {horseGrass:0.0} m, wood {horseWood:0.0}; walk grass {footGrass:0.0}, wood {footWood:0.0}, road {footRoad:0.0}");
        Check(horseWood < horseGrass * 0.7f && footWood < footGrass && footWood / footGrass > horseWood / horseGrass, "a wood slows horse more than foot");
        Check(footRoad > footGrass, "the road is quicker");
        var heavy = Troops.Get("iron_legionaries");
        float Tired(GroundKind k) => Ride(heavy, k, (b, u) => u.Stamina);
        Check(Tired(GroundKind.Mud) < Tired(GroundKind.Grass), "mud tires an armoured man");
        var t = new Terrain(11, TerrainParams.Hills);
        var steep = Enumerable.Range(0, 900).Select(i => new Vector2(-45 + i % 30 * 3, -45 + i / 30 * 3)).MaxBy(q => t.Gradient(q).Length());
        var up = Vector2.Normalize(t.Gradient(steep));
        Check(Battle.SlopeFactor(t, steep, up, true) < Battle.SlopeFactor(t, steep, up), "a horse feels a climb more than a man");
    }

    /// Ground that changes how men fight (docs/archive/TERRAIN.md T1): spears in a wood cannot brace, a horse on stones stumbles
    /// (and the same seed stumbles the same way), high ground is a smooth curve, a downhill charge hits harder,
    /// height carries arrows further.
    static void GroundFights()
    {
        float HorseHurt(GroundKind k)
        {
            var b = Battle.Empty(7);
            b.Terrain.Paint(new Vector2(0, 0), 12, k);
            b.AddSquad(1, Troops.Get("spearrunners"), 12, new(0, 0), 6, -Vector2.UnitY);
            var l = Lancer(b);
            float hp = l.HorseHp;
            Run(b, 3.5f);
            return l.Mounted ? hp - l.HorseHp : hp; // the braced points wound the horse first
        }
        float grass = HorseHurt(GroundKind.Grass), wood = HorseHurt(GroundKind.Wood);
        Console.WriteLine($"ground: a lancer's horse riding into spears loses {grass:0} hp on grass, {wood:0} in a wood");
        Check(wood < grass, "spears in a wood cannot brace against horse");
        (float speed, Vector2 pos) Gallop(int seed)
        {
            var b = Battle.Empty(seed);
            b.Terrain.Paint(new Vector2(0, 40), 70, GroundKind.Stony);
            var s = b.AddSquad(0, UnitType.Lancer, 1, new(0, -10), 1, Vector2.UnitY);
            s.Order = Order.Hold; s.Anchor = new(0, 90); s.Front = Vector2.UnitY;
            var u = s.Units[0];
            float slowest = float.MaxValue;
            for (int i = 0; i < 60 * 8; i++) { u.Speed = MathF.Max(u.Speed, i % 30 == 0 ? u.Horse!.Gallop : 0); b.Update(1 / 60f); slowest = MathF.Min(slowest, u.Speed); }
            return (slowest, u.Pos);
        }
        var g1 = Gallop(4);
        Check(g1.speed < UnitType.Lancer.Mount!.Gallop * 0.5f, "a horse galloping over stones stumbles");
        Check(Gallop(4).pos == g1.pos, "and the same seed stumbles the same way");
        Check(Battle.HighGround(1, 0, 0) > 0 && Battle.HighGround(1, 0, 0) < Battle.HighGround(3, 0, 0) && Battle.HighGround(30, 0, 0) <= 0.15f
            && Battle.HighGround(-3, 0, 0) < 0 && Battle.HighGround(0, 0, 0) == 0, "high ground: a smooth edge up to 15%");
        Check(Battle.HighGround(1, -0.1f, 6) > Battle.HighGround(1, -0.1f, 1) + 0.05f && Battle.HighGround(1, 0.1f, 6) == Battle.HighGround(1, 0.1f, 1), "a charge down a slope hits harder");
        Check(Battle.HeightRange(0) == 1 && Battle.HeightRange(-5) == 1 && Battle.HeightRange(3) > 1 && Battle.HeightRange(50) == 1.25f, "height carries arrows further, up to a quarter");
    }

    /// Fields built to a layout (docs/archive/TERRAIN.md T2): the deployment ground stays clear at every size of battle, a ridge
    /// rises in front of one army, two hills stand on the wings, broken ground is stony and the fen is mud.
    static void Layouts()
    {
        foreach (var g in new[] { TerrainParams.Meadow, TerrainParams.Forest, TerrainParams.Hills, TerrainParams.Marsh, TerrainParams.Badlands })
            foreach (float scale in new[] { 1f, 2.4f })
            {
                var t = new Terrain(13, g with { Scale = scale });
                Check(!t.Foliage.Any(f => f.Radius > 0 && t.Core(f.Pos)), $"{g.Name} at scale {scale}: no trees or boulders where the armies deploy");
                Check(t.Core(new Vector2(0, -30 * scale)) && t.Core(new Vector2(48 * scale, 0)), $"{g.Name} at scale {scale}: the clear ground grows with the battle");
            }
        Terrain Built(FieldLayout l, int seed = 13) => new(seed, TerrainParams.Hills with { Layouts = new[] { l }, HillsMin = 0, HillsMax = 0 });
        var ridge = Built(FieldLayout.Ridge);
        float side = ridge.RidgeSide == 0 ? -1 : 1;
        Check(ridge.Layout == FieldLayout.Ridge && MathF.Abs(ridge.RidgeZ) > 20 && MathF.Sign(ridge.RidgeZ) == side, "a ridge lies on one army's side");
        Check(ridge.Height(new Vector2(0, ridge.RidgeZ)) > ridge.Height(Vector2.Zero) + 2.5f && ridge.Height(new Vector2(0, ridge.RidgeZ)) > ridge.Height(new Vector2(0, ridge.RidgeZ + side * 28)) + 1,
            "the ridge stands above the field and above the ground behind it");
        var two = Built(FieldLayout.TwoHills);
        Check(two.Height(new Vector2(-48, 0)) > two.Height(Vector2.Zero) + 2 || two.Height(new Vector2(48, 0)) > two.Height(Vector2.Zero) + 2, "two hills stand on the wings");
        int Count(Terrain t, GroundKind k) => Enumerable.Range(0, 41 * 41).Count(i => t.GroundAt(new Vector2(-100 + i % 41 * 5, -100 + i / 41 * 5)) == k);
        var broken = new Terrain(13, TerrainParams.Badlands with { Layouts = new[] { FieldLayout.Broken } });
        var fen = new Terrain(13, TerrainParams.Marsh);
        var forest = new Terrain(13, TerrainParams.Forest);
        Console.WriteLine($"layouts: broken {Count(broken, GroundKind.Stony)} stony, {Count(broken, GroundKind.Scrub)} scrub; fen {Count(fen, GroundKind.Mud)} mud; forest {Count(forest, GroundKind.DenseWood)} thick wood (of 1681)");
        Check(Count(broken, GroundKind.Stony) > 20 && Count(broken, GroundKind.Scrub) > 100, "broken ground is stony and scrubby");
        Check(fen.Layout == FieldLayout.Fen && Count(fen, GroundKind.Mud) > 400, "the fen is mud");
        Check(Count(forest, GroundKind.DenseWood) > 0, "big woods have a thick heart");
        Check(new Terrain(13, TerrainParams.Meadow with { Layouts = null }).Layout == FieldLayout.Open, "no layouts means an open field");
    }

    /// Line features and the rest of the layouts (docs/archive/TERRAIN.md T2): a hedge stops a man except at its gaps, a road over
    /// a river gets a bridge you can walk across dry-shod, a village has houses and fences, a pass has high sides, and a
    /// company in a wood opens its ranks.
    static void HedgesAndBridges()
    {
        var b = Battle.Empty(5);
        b.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(-20, 0), new Vector2(20, 0));
        var s = b.AddSquad(0, UnitType.Shieldman, 1, new(0, -6), 1, Vector2.UnitY);
        s.Order = Order.Hold; s.Anchor = new(0, 10); s.Front = Vector2.UnitY;
        Run(b, 6);
        Check(s.Units[0].Pos.Y < 0, "a hedge stops a man");
        var gap = b.AddSquad(0, UnitType.Shieldman, 1, new(25, -6), 1, Vector2.UnitY);
        gap.Order = Order.Hold; gap.Anchor = new(25, 10); gap.Front = Vector2.UnitY;
        Run(b, 8);
        Check(gap.Units[0].Pos.Y > 8, "but not past its end");
        Check(b.Terrain.CrossesFeature(new Vector2(0, -5), new Vector2(0, 5)) && !b.Terrain.CrossesFeature(new Vector2(25, -5), new Vector2(25, 5)), "a line across a hedge crosses it");

        var river = new Terrain(21, TerrainParams.Meadow with { Layouts = new[] { FieldLayout.RiverLine } });
        var r = river.Rivers.Single();
        Check(river.Layout == FieldLayout.RiverLine && r.Bridges.Length == 1, "a river line has a bridge where the road crosses");
        var (at, along) = r.Bridges[0];
        Check(river.WaterDepth(at) == 0 && river.GroundAt(at) == GroundKind.Bridge && river.Features.Count(f => f.Kind == FeatureKind.Rail) == 2, "the bridge is dry, with a rail each side");
        int seg = Enumerable.Range(0, r.Path.Length - 1).MinBy(i => Vector2.DistanceSquared(r.Path[i], at));
        var aside = at + Vector2.Normalize(r.Path[seg + 1] - r.Path[seg]) * 7; // along the river, off the deck
        Check(river.WaterDepth(aside) > 0.5f || r.AtFord(aside), "beside the bridge is the river");
        Check(MathF.Abs(river.Height(at) - river.Height(at + along * 8)) < 1.2f, "the bridge runs level from bank to bank");

        var village = new Terrain(9, TerrainParams.Farmland with { Layouts = new[] { FieldLayout.Village } });
        Check(village.Foliage.Count(f => f.Kind == FoliageKind.House) >= 6 && village.Features.Any(f => f.Kind == FeatureKind.Fence), "a village has houses and fences");
        Check(village.GroundAt(village.VillageAt) is GroundKind.Street or GroundKind.Road, "with streets between the houses");
        Check(!village.Foliage.Any(f => f.Radius > 0 && MathF.Abs(f.Pos.X) < 50 && MathF.Abs(f.Pos.Y) is > 22 and < 54), "and none of it where the armies deploy");
        var hedges = new Terrain(9, TerrainParams.Farmland with { Layouts = new[] { FieldLayout.Hedgerows } });
        Check(hedges.Features.Count(f => f.Kind == FeatureKind.Hedge) > 8, "hedgerows have hedges");
        Check(!hedges.Features.Any(f => MathF.Abs(f.A.Y) > 22 || MathF.Abs(f.B.Y) > 22), "but not where the armies deploy");
        var pass = new Terrain(9, TerrainParams.Hills with { Layouts = new[] { FieldLayout.Pass }, HillsMin = 0, HillsMax = 0 });
        Check(pass.Height(new Vector2(-110, 0)) > pass.Height(Vector2.Zero) + 6 && pass.Height(new Vector2(110, 0)) > pass.Height(Vector2.Zero) + 6, "a pass has high ground both sides");
        foreach (var l in new[] { FieldLayout.WoodedFlank, FieldLayout.ForestClearing })
        {
            var w = new Terrain(9, TerrainParams.Forest with { Layouts = new[] { l } });
            Check(!w.Foliage.Any(f => f.Radius > 0 && w.Core(f.Pos)), $"{l}: the deployment ground is clear");
        }

        var wood = Battle.Empty(5);
        wood.Terrain.Paint(new Vector2(0, 0), 30, GroundKind.Wood);
        var inWood = wood.AddSquad(0, UnitType.Shieldman, 12, new(0, 0), 6, Vector2.UnitY);
        var open = wood.AddSquad(0, UnitType.Shieldman, 12, new(0, 60), 6, Vector2.UnitY);
        Run(wood, 0.1f);
        Check(inWood.Spacing > open.Spacing * 1.15f, "a company in a wood opens its ranks");
    }

    /// Every layout fights to a result with an idle player (nobody stuck behind a fence, in a village or a bog).
    static void EveryLayoutResolves()
    {
        foreach (var l in Enum.GetValues<FieldLayout>())
        {
            var g = (l switch
            {
                FieldLayout.Hedgerows or FieldLayout.Village => TerrainParams.Farmland,
                FieldLayout.Fen => TerrainParams.Marsh,
                FieldLayout.ForestClearing or FieldLayout.WoodedFlank => TerrainParams.Forest,
                FieldLayout.Broken => TerrainParams.Badlands,
                _ => TerrainParams.Hills,
            }) with { Layouts = new[] { l } };
            var rng = new Random(2);
            var b = new Battle(2, Armies.Random(rng, 3, 0), Armies.Random(rng, 3, 1), new BattleOptions { Ground = g });
            for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
            Check(b.Winner >= 0, $"a battle on a {l} field resolves");
        }
    }

    /// Sight and cover (docs/archive/TERRAIN.md T3): a wood soaks up arrows, a hedge stops flat shots, a crest hides what is behind
    /// it, men in a wood are unseen from afar, and a hidden company that is found close to the enemy is an ambush.
    static void SightAndCover()
    {
        int Hits(Action<Battle> ground)
        {
            var b = Battle.Empty(8);
            ground(b);
            var bows = b.AddSquad(0, UnitType.Bowman, 12, new(0, -28), 6, Vector2.UnitY);
            var mark = b.AddSquad(1, UnitType.Shieldman, 12, new(0, 0), 6, -Vector2.UnitY);
            bows.Target = mark;
            foreach (var u in mark.Units) u.Hp = 100000; // a mark that does not fall (the troop type itself is shared: leave it be)
            Run(b, 20);
            return mark.Units.Sum(u => (int)(100000 - u.Hp));
        }
        int open = Hits(_ => { }), wood = Hits(b => b.Terrain.Paint(Vector2.Zero, 20, GroundKind.Wood)),
            hedge = Hits(b => b.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(-20, -3.5f), new Vector2(20, -3.5f)));
        Console.WriteLine($"cover: arrow damage on a mark in the open {open}, in a wood {wood}, behind a hedge {hedge}");
        Check(wood < open * 0.8f, "a wood soaks up arrows");
        Check(hedge < open * 0.8f, "a hedge stops flat shots");

        var g = TerrainParams.Hills with { Layouts = new[] { FieldLayout.Ridge }, HillsMin = 0, HillsMax = 0 };
        var none = new ArmySpec("a", 1, new List<Company>());
        var rb = new Battle(13, none, none, new BattleOptions { Ground = g });
        float side = rb.Terrain.RidgeSide == 0 ? -1 : 1;
        var behind = new Vector2(0, rb.Terrain.RidgeZ + side * 22);
        Check(!rb.ClearLine(new Vector2(0, -side * 35), behind), "a crest hides what is behind it");
        Check(rb.ClearLine(new Vector2(0, -side * 35), new Vector2(0, -side * 5)), "but not the open field in front");

        var flat = TerrainParams.Meadow with { Layouts = null, HillsMin = 0, HillsMax = 0, Road = false, Woods = 0, LoneTrees = 0 };
        var wb = new Battle(14, none, none, new BattleOptions { Ground = flat, PlayerWounded = true });
        wb.Terrain.Paint(new Vector2(0, 20), 22, GroundKind.DenseWood);
        var lurk = wb.AddSquad(1, UnitType.Shieldman, 12, new(0, 20), 6, -Vector2.UnitY);
        var walk = wb.AddSquad(0, UnitType.Shieldman, 12, new(0, -40), 6, Vector2.UnitY);
        Run(wb, 1.2f);
        Check(!wb.Visible(lurk, 0) && wb.Visible(walk, 1), "men in a wood are not seen from afar; men in the open are");
        foreach (var u in walk.Units) u.Pos += new Vector2(0, 46); // walk up to the edge of the wood
        walk.Anchor += new Vector2(0, 46);
        Run(wb, 1.2f);
        Check(wb.Visible(lurk, 0), "close up you see them");
        Check(wb.History.Any(h => h.Text.Contains("from cover")), "and a company found that close is an ambush");
    }

    /// A battlefield river: carved below its banks, slow to wade, quicker at the fords; the map has rivers with bridges.
    static void Rivers()
    {
        var t = new Terrain(21, TerrainParams.Meadow with { River = true });
        var r = t.Rivers.Single();
        var mid = r.Path.Skip(55).First(q => !r.AtBridge(q) && !r.AtBridge(q + new Vector2(6, 0)) && !r.AtBridge(q - new Vector2(6, 0)) && !r.AtFord(q)); // open water, off the bridge
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
        Check(m.Rivers.Count == 4 && m.Rivers.All(rv => rv.Fords.Length > 0), "the map has four rivers with bridges");
        Check(m.Settlements.All(s => m.RiverDist(s.Pos) > 4), "no settlement stands in a river");
    }

    /// The AI reads the ground (docs/archive/TERRAIN.md T5): on a ridge, the side on the crest lets the other come up to it, and
    /// still fights to a result.
    static void HoldsTheCrest()
    {
        var g = TerrainParams.Hills with { Layouts = new[] { FieldLayout.Ridge }, HillsMin = 0, HillsMax = 0 };
        for (int seed = 1; seed <= 3; seed++)
        {
            var b = new Battle(seed, Armies.Random(new Random(seed), 2, 0), Armies.Random(new Random(seed), 2, 1), new BattleOptions { Ground = g, AutoPlayer = true, PlayerWounded = true });
            Check(b.Places.Any(p => p.Kind is PlaceKind.Crest or PlaceKind.Knoll), "the survey finds the ridge");
            int r = b.Terrain.RidgeSide, low = 1 - r;
            Vector2 Line(int team) { var l = b.SquadsOf(team).Where(s => s.Company != null && Armies.RoleOf(s.Company) == "infantry" && s.Alive > 0).ToList(); return l.Count == 0 ? b.Centroid(team) : l.Aggregate(Vector2.Zero, (a, s) => a + s.Center) / l.Count; }
            var start = Line(r);
            var fwd = Vector2.Normalize(Line(low) - start);
            float moved = 0;
            while (b.Winner < 0 && b.Time < 600)
            {
                b.Update(1 / 60f);
                if (Vector2.Distance(Line(r), Line(low)) < 28) break;
                moved = MathF.Max(moved, Vector2.Dot(Line(r) - start, fwd));
            }
            Console.WriteLine($"crest {seed}: the side on the ridge came {moved:0.0} m forward before the lines met at {b.Time:0}s");
            Check(moved < 8, "the side on the crest does not leave it first");
            while (b.Winner < 0 && b.Time < 600) b.Update(1 / 60f);
            Check(b.Winner >= 0, "a battle on a ridge resolves");
        }
    }

    /// The AI's horse picks a company on good going over one standing in mud.
    static void HorseAvoidsBadGround()
    {
        var flat = TerrainParams.Steppe with { HillsMin = 0, HillsMax = 0, Layouts = new[] { FieldLayout.Open } };
        var mine = new ArmySpec("a", 1, new List<Company> { new("bowmen", 16, "archers"), new("bowmen", 16, "archers") });
        var theirs = new ArmySpec("b", 1, new List<Company> { new("lancers", 12, "horse") });
        foreach (int muddy in new[] { 0, 1 })
        {
            var b = new Battle(4, mine, theirs, new BattleOptions { Ground = flat, PlayerWounded = true });
            var bows = b.SquadsOf(0).Where(s => s.Company?.Troop == "bowmen").ToList();
            b.Terrain.Paint(bows[muddy].Center, 6, GroundKind.Mud);
            var horse = b.SquadsOf(1).First(s => s.Cavalry && s.Company != null);
            while (horse.Target == null && b.Time < 60) b.Update(1 / 60f);
            Check(horse.Target == bows[1 - muddy], "the horse charges the company on firm ground, not the one in mud");
        }
    }

    /// A division lying in wait stays still until an enemy company comes within reach, then falls on it.
    static void AmbushWaits()
    {
        var b = Battle.Empty(5);
        b.Terrain.Paint(Vector2.Zero, 16, GroundKind.Wood);
        var s = b.AddSquad(0, UnitType.Shieldman, 12, Vector2.Zero, 6, Vector2.UnitY);
        var d = b.AddDivision(0, "Hal", new[] { s }, Temper.Steady, 5, new Goal(GoalKind.Ambush, Vector2.Zero));
        var foe = b.AddSquad(1, UnitType.Shieldman, 12, new(0, 90), 6, -Vector2.UnitY);
        foe.Order = Order.Hold; foe.Anchor = new(0, 90);
        Run(b, 6);
        Check(d.Goal.Kind == GoalKind.Ambush && s.Order == Order.Hold && Vector2.Distance(s.Center, Vector2.Zero) < 6, "a division lying in wait stays put while the enemy is far");
        foe.Anchor = new(0, -20);
        float gapAtSpring = -1;
        for (int i = 0; i < 60 * 40 && d.Goal.Kind == GoalKind.Ambush; i++) { b.Update(1 / 60f); gapAtSpring = Vector2.Distance(s.Center, foe.Center) - foe.Radius; }
        Run(b, 1);
        Console.WriteLine($"ambush: sprung with the enemy {gapAtSpring:0} m off");
        Check(d.Goal.Kind == GoalKind.Attack && d.Goal.Target == foe && gapAtSpring <= Battle.AmbushReach + 1, "it falls on the enemy when he comes within reach");
        Check(s.Order == Order.Charge, "and charges");
    }

    /// On a river line the AI never sets a company's ground in deep water: it goes over by the crossing.
    static void TakesTheFord()
    {
        var g = TerrainParams.Meadow with { Layouts = new[] { FieldLayout.RiverLine } };
        var rng = new Random(6);
        var b = new Battle(6, Armies.Random(rng, 2, 0), Armies.Random(rng, 2, 1), new BattleOptions { Ground = g, AutoPlayer = true, PlayerWounded = true });
        Check(b.Places.Any(p => p.Kind is PlaceKind.Ford or PlaceKind.Bridge), "the survey finds the crossings");
        int wet = 0, looks = 0;
        while (b.Winner < 0 && b.Time < 600)
        {
            b.Update(1 / 60f);
            if (b.Time < 90 && (int)(b.Time * 60) % 30 == 0)
                foreach (var s in b.Squads.Where(s => s.Alive > 0 && s.Order == Order.Hold && s.Company != null))
                {
                    looks++;
                    if (b.Terrain.WaterDepth(s.Anchor) > 0.6f) wet++;
                }
        }
        Check(looks > 0 && wet == 0, "no company is told to stand in deep water");
        Check(b.Winner >= 0, "a battle on a river line resolves");
    }

    /// Tools for the player (docs/archive/TERRAIN.md T6): the ground's line, order warnings, stakes, and the Pathfinder perk.
    static void GroundForThePlayer()
    {
        Check(Grounds.Describe(GroundKind.Grass) == "GRASS. GOOD GOING.", "plain grass is good going");
        var wood = Grounds.Describe(GroundKind.Wood);
        Check(wood.StartsWith("WOOD. COVER FROM ARROWS. HORSE SLOWED. NO FORMATION."), "a wood says what it does: " + wood);
        Check(Grounds.Describe(GroundKind.Road).Contains("QUICK GOING"), "a road is quick");

        var b = Battle.Empty(5);
        b.Terrain.Paint(new Vector2(0, 40), 12, GroundKind.Wood);
        var horse = b.AddSquad(0, Troops.Get("lancers"), 6, Vector2.Zero, 6, Vector2.UnitY);
        var foot = b.AddSquad(0, UnitType.Shieldman, 6, new(20, 0), 6, Vector2.UnitY);
        Run(b, 0.1f);
        Check(b.GroundWarning(new[] { horse }, new Vector2(0, 40)) == "HORSE IN THE WOOD: SLOWED, NO CHARGE.", "sending horse into a wood warns you");
        Check(b.GroundWarning(new[] { foot }, new Vector2(20, 40)) == null, "open ground does not");
        b.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(-30, 20), new Vector2(30, 20));
        Check(b.GroundWarning(new[] { foot }, new Vector2(20, 40)) == null, "a hedge in the way is no warning (they go round)");
    }

    /// Longbowmen's stakes stop horse but let men on foot through; each company plants them once.
    static void Stakes()
    {
        var bows = Troops.Get("longbowmen");
        Check(bows.Has("stakes") && !Troops.Get("bowmen").Has("stakes"), "longbowmen carry stakes");
        float Crossed(bool mounted, out string? second)
        {
            var b = Battle.Empty(5);
            var archers = b.AddSquad(0, bows, 10, new(0, -4), 10, Vector2.UnitY);
            archers.Order = Order.Hold; archers.Anchor = new(0, -4); archers.Front = Vector2.UnitY;
            Run(b, 0.1f);
            var shield = b.AddSquad(0, UnitType.Shieldman, 4, new(-60, -60), 4, Vector2.UnitY);
            Check(b.CannotStake(shield) != null, "only longbowmen can plant stakes");
            Check(b.PlantStakes(archers) == null, "longbowmen plant stakes");
            second = b.PlantStakes(archers);
            foreach (var u in archers.Units) u.Pos += new Vector2(0, -30); // out of the way
            archers.Anchor = new(0, -34);
            var came = b.AddSquad(1, mounted ? Troops.Get("lancers") : UnitType.Shieldman, 8, new(0, 14), 8, -Vector2.UnitY);
            came.Order = Order.Hold; came.Anchor = new(0, -12); came.Front = -Vector2.UnitY;
            var stakes = b.Terrain.Features.Where(f => f.Kind == FeatureKind.Stakes).ToList();
            float half = stakes.Max(f => MathF.Max(MathF.Abs(f.A.X), MathF.Abs(f.B.X))), line = stakes[0].A.Y;
            var through = new HashSet<Unit>();
            for (int i = 0; i < 60 * 12; i++)
            {
                var was = came.Units.Select(u => u.Pos).ToList();
                b.Update(1 / 60f);
                for (int k = 0; k < came.Units.Count; k++) // crossed the staked front itself, not round its end
                    if (was[k].Y >= line && came.Units[k].Pos.Y < line && MathF.Abs(came.Units[k].Pos.X) < half) through.Add(came.Units[k]);
            }
            return through.Count;
        }
        float riders = Crossed(true, out var again), men = Crossed(false, out _);
        Console.WriteLine($"stakes: {riders} of 8 riders and {men} of 8 men on foot got through");
        Check(again != null, "a company plants its stakes once");
        Check(riders == 0, "horse cannot cross stakes");
        Check(men >= 4, "men on foot slip between them");
    }

    /// Pathfinder: your men lose half the pace a wood costs.
    static void Pathfinder()
    {
        float Walk(bool perk)
        {
            var o = new BattleOptions();
            if (perk) o.Perks.Add("Pathfinder");
            var b = Battle.Empty(5, o);
            b.Terrain.Paint(new Vector2(0, 15), 25, GroundKind.Wood);
            var s = b.AddSquad(0, UnitType.Shieldman, 1, Vector2.Zero, 1, Vector2.UnitY);
            s.Order = Order.Hold; s.Anchor = new(0, 30); s.Front = Vector2.UnitY;
            float time = 0;
            while (s.Units[0].Pos.Y < 28 && time < 60) { b.Update(1 / 60f); time += 1 / 60f; }
            return time;
        }
        float plain = Walk(false), finder = Walk(true);
        Console.WriteLine($"pathfinder: 30 m of wood in {plain:0.0}s, {finder:0.0}s with the perk");
        Check(finder < plain * 0.93f, "Pathfinder men cross a wood quicker");
    }

    /// Pathing (docs/archive/TERRAIN.md T4): an order across a river goes by the ford when one is near, rather than wading the deep.
    static void GoesToFord()
    {
        var g = TerrainParams.Meadow with { River = true, Road = false, Layouts = new[] { FieldLayout.Open } };
        var r = new Terrain(3, g with { Scale = 1 }).Rivers.Single();
        var ford = r.Fords[0];
        int fi = Enumerable.Range(0, r.Path.Length).MinBy(i => Vector2.DistanceSquared(r.Path[i], ford));
        var at = r.Path[Math.Min(r.Path.Length - 1, fi + 7)]; // up the river from the ford
        float Crossing(bool paths, out float deepest)
        {
            var b = new Battle(3, new ArmySpec("a", 1, new List<Company>()), new ArmySpec("b", 1, new List<Company>()), new BattleOptions { Ground = g, PlayerWounded = true });
            var s = b.AddSquad(0, UnitType.Shieldman, 6, at - new Vector2(0, 16), 3, Vector2.UnitY);
            s.Order = Order.Hold; s.Anchor = at + new Vector2(0, 16); s.Front = Vector2.UnitY;
            if (!paths) s.PathTimer = float.MaxValue;
            float time = 0;
            deepest = 0;
            while (s.Units.Any(u => !u.Dead && Vector2.Distance(u.Pos, s.Anchor) > 6) && time < 90)
            {
                b.Update(1 / 60f); time += 1 / 60f;
                foreach (var u in s.Units) if (b.Terrain.WaterDepth(u.Pos) > 0.6f) deepest += 1 / 60f; // man-seconds in deep water
            }
            return time;
        }
        float straight = Crossing(false, out float wade), routed = Crossing(true, out float ford2);
        Console.WriteLine($"river: straight over {straight:0.0}s ({wade:0} man-s in deep water), by the ford {routed:0.0}s ({ford2:0} man-s)");
        Check(ford2 < wade * 0.5f, "a company sent over a river crosses at the ford"); // its outer files may still step off the ford
        Check(routed < 90, "and gets there");
    }

    /// Pathing: a wide company sent through a long hedge finds the gap and all of it gets through.
    static void FilesThroughGap()
    {
        var b = Battle.Empty(5);
        b.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(-60, 0), new Vector2(18, 0));
        b.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(26, 0), new Vector2(60, 0));
        var s = b.AddSquad(0, UnitType.Shieldman, 16, new(-20, -14), 8, Vector2.UnitY);
        s.Order = Order.Hold; s.Anchor = new(-20, 14); s.Front = Vector2.UnitY;
        bool routed = false;
        float time = 0;
        while (s.Units.Any(u => u.Pos.Y < 4) && time < 60) { b.Update(1 / 60f); time += 1 / 60f; routed |= s.Path != null; }
        Console.WriteLine($"hedge: a company of 16 through the gap in {time:0.0}s");
        Check(routed && s.Units.All(u => u.Pos.Y > 4), "a company finds the gap in a hedge and all of it gets through");
        Check(!b.WayBlocked(new Vector2(-20, -14), new Vector2(-20, -40), false) && b.WayBlocked(new Vector2(-20, -14), new Vector2(-20, 14), false), "a hedge blocks the way, open ground does not");
        // The dotted line you see while drawing an order goes the same way: through the gap.
        var c = Battle.Empty(5);
        c.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(-60, 0), new Vector2(18, 0));
        c.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(26, 0), new Vector2(60, 0));
        var q = c.AddSquad(0, UnitType.Shieldman, 16, new(6, -14), 8, Vector2.UnitY);
        c.Update(1 / 60f);
        var route = c.Route(q, new(6, 14));
        Check(route != null && route.Any(p => MathF.Abs(p.Y) < 2 && p.X > 17 && p.X < 27) && c.Route(q, new(6, -40)) == null, "the order's dotted line shows the way through the gap, and none on open ground");
    }

    /// Pathing: a charge with no company left to hit goes round the hedge for the captain behind it (a battle once stalled
    /// with the last enemy company pressed against a hedge, the lone captain on the far side).
    static void ChargeFindsCaptain()
    {
        var b = Battle.Empty(5);
        b.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(-60, 0), new Vector2(18, 0));
        b.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(26, 0), new Vector2(60, 0));
        b.Player.Pos = new(-20, -10);
        var s = b.AddSquad(1, UnitType.Shieldman, 8, new(-20, 10), 4, -Vector2.UnitY);
        s.Order = Order.Charge;
        float time = 0;
        while (!b.Player.Dead && s.Units.All(u => Vector2.Distance(u.Pos, b.Player.Pos) > 3) && time < 60) { b.Update(1 / 60f); time += 1 / 60f; }
        Console.WriteLine($"hedge: the charge reaches the captain in {time:0.0}s");
        Check(time < 60, "a charge with no company to hit goes round the hedge for the captain");
    }

    /// Pathing: flow fields are cached, the cache stays small, and the same goal is not built twice.
    static void FlowCacheBound()
    {
        var b = Battle.Empty(5);
        b.Terrain.AddFeature(FeatureKind.Hedge, new Vector2(-80, 0), new Vector2(80, 0));
        var s = b.AddSquad(0, UnitType.Shieldman, 4, new(0, -20), 2, Vector2.UnitY);
        s.Order = Order.Hold; s.Front = Vector2.UnitY;
        for (int i = 0; i < 80; i++) { s.Anchor = new(-60 + i * 1.5f, 30); s.PathTimer = 0; s.Path = null; b.Update(1 / 60f); }
        Check(b.FlowCached <= 48 && b.FlowBuilds > 0, "the flow cache stays bounded");
        int built = b.FlowBuilds;
        s.Anchor = new(-60, 30); s.PathTimer = 0; s.Path = null; b.Update(1 / 60f);
        s.Anchor = new(-60.5f, 30.5f); s.PathTimer = 0; s.Path = null; b.Update(1 / 60f);
        Check(b.FlowBuilds <= built + 1, "a goal near one already built uses the cached field");
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
        Check(MathF.Abs(t.Height(new Vector2(-Terrain.BattleHalf + 10 * Terrain.BattleStep, -Terrain.BattleHalf + 7 * Terrain.BattleStep)) - t.GridHeight(10, 7)) < 1e-4f,
            "height samples the mesh vertices exactly");
        for (float x = -200; x <= 200; x += 7)
            for (float z = -200; z <= 200; z += 7)
                Check(float.IsFinite(t.Height(new Vector2(x, z))) && MathF.Abs(t.Height(new Vector2(x, z))) < 60, "heights stay sane");
        Check(t.Foliage.Count(f => f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine) > 150, "the land is wooded");
        Check(!t.Foliage.Any(f => f.Radius > 0 && t.Core(f.Pos)), "no trees or boulders where the armies deploy");
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
        var clansmen = Troops.Get("clansmen");
        Check(levies.Tier == 1 && levies.UpgradesTo.Length >= 4, "levies branch into several lines");
        Check(clansmen.Tier == 1 && clansmen.UpgradesTo.Length >= 4, "clansmen branch into several lines");
        foreach (var t in Troops.All)
        {
            Check(t.UpgradesTo.Length == 0 || t.UpgradeXp > 0, $"{t.Name} has upgradeXp when it upgrades");
            foreach (var next in t.UpgradesTo.Select(Troops.Get))
                Check(next.MaxHp + next.Armor * 5 + next.Damage > t.MaxHp + t.Armor * 5 + t.Damage, $"{t.Name} -> {next.Name} is an upgrade");
        }
        for (int tier = 1; tier <= 7; tier++)
        {
            Check(Troops.Tree("levies").Any(t => t.Tier == tier), $"levies tree reaches tier {Style.Numerals[tier]}");
            Check(Troops.Tree("clansmen").Any(t => t.Tier == tier), $"clansmen tree reaches tier {Style.Numerals[tier]}");
        }
        Check(Troops.All.Count(t => !t.Leader && !t.Wild) >= 80, $"massive 7-tier troop tree ({Troops.All.Count(t => !t.Leader)} troops)");
        var reachable = Troops.Tree("levies").Concat(Troops.Tree("clansmen")).Select(t => t.Id).ToHashSet();
        Check(Troops.All.Where(t => !t.Leader && !t.Wild && t.Class != UnitClass.Caster).All(t => reachable.Contains(t.Id)), "every troop can be reached by upgrading a culture's recruits");
        var magi = Troops.Tree("acolytes").Select(t => t.Id).ToHashSet(); // mages are super units: hired as Acolytes, never drilled from recruits
        Check(Troops.All.Where(t => t.Class == UnitClass.Caster).All(t => magi.Contains(t.Id) && !reachable.Contains(t.Id)), "every mage rises from Acolytes, and no recruit becomes one");
    }

    /// Every sprite any troop's look can ask for exists, for every side.
    static void Sprites()
    {
        var sheet = new SpriteSheet();
        IEnumerable<string> Keys(UnitType t)
        {
            var l = t.Look;
            if (l.Beast) yield break; // a beast is drawn from neutral w* parts, checked below
            foreach (var v in new[] { "f", "s", "b" })
            {
                yield return $"torso.{l.Body}.{v}";
                yield return $"boot.{l.Feet}.{v}";
                yield return $"{l.HeadKey}.{v}";
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
        foreach (var k in new[] { "hbody", "hneck", "hhead", "hchest", "hrump", "hface", "hleg", "htail", "wbody", "whead", "wleg", "wtail" })
            Check(sheet.Rects.ContainsKey(k + "/n"), $"horse coat sprite {k}");
        foreach (var e in Enum.GetValues<Emblem>())
            for (int p = 0; p < Style.Sides.Length; p++) Check(sheet.Rects.ContainsKey($"flag.{(int)e}/{p}"), $"company banner flag.{(int)e}/{p}");
        var torso = sheet.Rects["torso.mail.f/0"];
        Check(MathF.Abs((torso.Height - 2) * Style.Px - 0.675f) < 0.05f, "a torso keeps its size in metres");
        Check(sheet.Rects.Values.Any(r => r.Height > 90), "polearms are painted at full length");
        Console.WriteLine($"sprites: {sheet.Rects.Count} parts for {Style.Sides.Length} palettes, atlas {sheet.Filled * 100 / SpriteSheet.H}% full");
    }

    /// Two men of one troop can look different (face, helm, weapon, height), the same man always looks the same,
    /// the player and creatures are drawn as their look says, and every varied look has its sprites.
    static void TroopVariety()
    {
        var sheet = new SpriteSheet();
        var b = Battle.Empty(3);
        var men = b.AddSquad(1, Troops.Get("shieldmen"), 40, new(0, 20), 8, -Vector2.UnitY).Units;
        var looks = men.Select(Variety.Of).ToList();
        Check(looks.Select(l => l.Visage).Distinct().Count() >= 12, $"forty shieldmen show many faces ({looks.Select(l => l.Visage).Distinct().Count()})");
        Check(looks.Select(l => l.Head).Distinct().Count() > 1 && looks.Select(l => l.Weapon).Distinct().Count() > 1, "some wear another helm or carry another weapon");
        Check(looks.Count(l => l.Head == men[0].Type.Look.Head) > 15, "most keep the troop's own helm");
        Check(men.All(u => ReferenceEquals(Variety.Of(u), Variety.Of(u))), "a man's look is the same every frame");
        Check(men.Select(Variety.Stature).All(s => s >= 0.95f && s <= 1.05f), "heights stay within five in a hundred");
        var player = b.Units.First(u => u.IsPlayer);
        Check(Variety.Of(player) == player.Type.Look && Variety.Stature(player) == 1, "the player is drawn exactly as his kit");
        var wolf = Troops.All.First(t => t.Look.Beast);
        Check(Variety.Of(wolf, 5) == wolf.Look, "a creature is drawn as its troop");
        var named = men[0];
        named.SoldierId = 42;
        var first = Variety.Of(named);
        named.Index = 999;
        Check(Variety.Of(named) == first, "a named man keeps his looks wherever he stands");
        int missing = 0;
        foreach (var t in Troops.All)
            for (int seed = 0; seed < 60; seed++)
            {
                var l = Variety.Of(t, seed);
                if (l.NeutralHead) foreach (var v in new[] { "f", "s", "b" }) if (!sheet.Rects.ContainsKey($"{l.HeadKey}.{v}/n")) missing++;
                if (l.Head != "none" && !sheet.Rects.ContainsKey($"helm.{l.Head}.f/0")) missing++;
                if (!sheet.Rects.ContainsKey(l.Weapon + "/0") || !SpriteSheet.Grip.ContainsKey(l.Weapon)) missing++;
            }
        Check(missing == 0, $"every varied look has its sprites ({missing} missing)");
        Console.WriteLine($"variety: {Variety.Visages} faces, {looks.Select(l => (l.Visage, l.Head, l.Weapon)).Distinct().Count()} looks among 40 shieldmen");
    }

    // ---------- campaign (docs/CAMPAIGN.md section 11) ----------

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

    static void StackBattle()
    {
        var c = Campaign.New(82);
        c.Player.Pos = c.S.Settlements.First(t => t.Kind == SettlementKind.Town && !c.Refuses(t)).Pos;
        c.S.Gold = 2000;
        var co = c.Player.Companies.First(k => Rules.RoleOf(k) == "infantry");
        co.Stack("levies").AddXp(1e6f);
        c.Promote(co, co.Stack("levies"), "shieldmen", 5);
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        bandit.Pos = c.Player.Pos;
        c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        setup.Options.AutoPlayer = true;
        setup.Options.PlayerWounded = true;
        var spec = setup.Player.Companies.First(k => k.Id == co.Id);
        Check(spec.Mix != null && spec.Mix.Length == 2, "the battle gets both stacks of the company");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        var squads = b.Squads.Where(q => q.Company?.Id == co.Id).ToList();
        Check(squads.Count == 1, "a mixed company is one squad");
        Check(squads[0].Units.Select(u => u.Type.Id).Distinct().Count() == 2, "of two kinds of men");
        for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
        var result = b.Result();
        var cr = result.Companies.First(r => r.Id == co.Id);
        Check(cr.Stacks != null && cr.Stacks.Count == 2 && cr.Stacks.Sum(st => st.Start) == cr.Start, "its result comes back per stack");
        Check(cr.Stacks!.All(st => st.Start == st.Standing + st.Fled + st.Fallen), "every man of every stack is accounted for");
        int before = co.Men + co.Wounded;
        var report = c.FinishBattle(result, setup);
        var line = report.Companies.First(r => r.Name == co.Name);
        Check(line.MenBefore == line.MenAfter + line.Killed + line.Wounded + line.Captured + line.FledLost, "the report accounts for every man");
        Check(co.Ledger.Count == 1 && co.Battles >= 1, "the company's ledger records the fight");
        Console.WriteLine($"stack battle: {(report.Won ? "won" : "lost")}, {co.Name} {before} -> {co.Men}+{co.Wounded}W [{string.Join(", ", cr.Stacks.Select(st => $"{st.Troop} {st.Start}->{st.Standing}"))}]");
    }

    /// Named men in battle (docs/archive/LEGACY.md L2): placed as real units, their kills and fates come back by id and reach the roll,
    /// the company's best man may earn a name, and a battle with named men replays exactly.
    static void NamedMenFight()
    {
        var c = Campaign.New(84);
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        foreach (var k in c.Player.Companies) for (int i = 0; i < 3; i++) c.NameMan(k, "test");
        int named = c.Player.Companies.Sum(k => k.Roll.Count);
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
        bandit.Pos = c.Player.Pos;
        c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        setup.Options.AutoPlayer = true;
        setup.Options.PlayerWounded = true;
        Check(setup.Player.Companies.Sum(k => k.Named?.Length ?? 0) == named, "the battle gets every fit named man");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        b.StartRecording(setup.Seed);
        int placed = b.Units.Count(u => u.SoldierId != 0);
        int onField = setup.Player.Companies.Where(k => b.Squads.Any(q => q.Company?.Id == k.Id)).Sum(k => k.Named?.Length ?? 0);
        Check(placed == onField && placed > 0, $"each named man of a company on the field is one of its units ({placed} of {onField})");
        Check(b.Units.Where(u => u.SoldierId != 0).All(u => setup.Player.Companies.Any(k => k.Named?.Any(m => m.Id == u.SoldierId && m.Troop == u.Type.Id) == true)), "of his own troop");
        for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
        var result = b.Result();
        var fates = result.Companies.Where(r => r.Named != null).SelectMany(r => r.Named!).ToList();
        Check(fates.Count == placed, "each named man's fate comes back");
        Check(result.Companies.Where(r => r.Named != null).All(r => r.Named!.Sum(n => n.Kills) <= r.Kills), "his kills are among the company's");
        var rec = b.FinishRecording()!;
        var back = Replay.Deserialize(rec.Serialize());
        Check(back.Matches(back.Play()), "a battle with named men replays exactly");
        var before = c.Player.Companies.SelectMany(k => k.Roll).ToDictionary(m => m.Id, m => m.Kills);
        c.FinishBattle(result, setup);
        foreach (var f in fates)
        {
            var m = c.Player.Companies.SelectMany(k => k.Roll).Concat(c.S.Fallen).FirstOrDefault(x => x.Id == f.Id);
            Check(m != null && m.Kills == before[f.Id] + f.Kills, "his kills reach the roll");
            Check(!f.Fell || m!.Hurt || m.Fate != "", "one who fell is hurt or lost");
            Check(f.Fell || f.Fled || m!.Fate is "" or "left", "one who stood comes home");
        }
        Console.WriteLine($"named men fight: {(result.Winner == 0 ? "won" : "lost")}, {placed} placed, {fates.Count(f => f.Fell)} fell, {fates.Sum(f => f.Kills)} kills; " +
            $"best man {result.Companies.Max(r => r.BestKills)} kills");
    }

    /// Auto-resolve (L2): named men share their stack's odds, so a hard-lost fight costs named men too.
    static void AutoResolveNamedMen()
    {
        int lost = 0, men = 0;
        for (int seed = 0; seed < 6; seed++)
        {
            var c = Campaign.New(90 + seed);
            c.Player.Cooldown = 1e6f;
            c.Tick(24); c.Encounter = null; c.Arrived = null;
            foreach (var k in c.Player.Companies) for (int i = 0; i < 5; i++) c.NameMan(k, "test");
            int had = c.Player.Companies.Sum(k => k.Roll.Count);
            men += had;
            var lord = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && c.Hostile(c.Player, p)).OrderByDescending(p => p.Men).First();
            lord.Pos = c.Player.Pos;
            c.Encounter = lord;
            var setup = c.PrepareBattle(lord);
            var r = Rules.AutoResolve(setup.Side0, setup.Side1, new Rng(new CampaignState { Seed = seed + 1 }));
            Check(r.Companies.All(x => x.Named == null), "auto-resolve hands back no fought fates");
            c.FinishBattle(r with { Winner = 1, Captain = LeaderFate.Wounded }, setup);
            lost += had - c.Player.Companies.Sum(k => k.Roll.Count); // a company wiped out takes its roll with it
        }
        Console.WriteLine($"auto-resolve named men: {lost} of {men} lost in six lost fights");
        Check(lost > 0, "auto-resolving a lost fight costs named men too");
    }

    /// Fighting for your men (docs/archive/LEGACY.md L8): a fallen named man you stand over is got out (saved), which the result
    /// carries; the "bring him home" goal names him and is lost if he dies; your named men are heard in the log.
    static void PullBack()
    {
        var spec = new Company("shieldmen", 10, "infantry", 7, "Test Guard", Named: new[] { new NamedSpec(5, "shieldmen", "Hob the Red") });
        var b = new Battle(5, new ArmySpec("mine", 0, new List<Company> { spec }), new ArmySpec("theirs", 0, new List<Company> { new("levies", 30, "infantry", 9) }),
            new BattleOptions { Goals = true });
        var hob = b.Units.First(u => u.SoldierId == 5);
        Check(Battle.NameOf(hob) == "Hob the Red", "a named man is known by name in the battle");
        b.Ward = hob;
        var goal = Battle.GoalKinds.First(g => g.Id == "bringhome");
        Check(b.GoalText(goal) == "BRING HOB THE RED HOME", "the goal names him");
        // He falls; you stand over him.
        b.KillForTest(hob, b.SquadsOf(1).First().Units[0]);
        Check(b.Events.Any(e => e.Text.Contains("Hob the Red is down")), "his fall is heard");
        Check(b.Rescuable(hob), "he can still be got out");
        b.Player.Pos = hob.Pos + new Vector2(1, 0);
        b.Player.Fled = false;
        for (int i = 0; i < 60 * 3 && !hob.Saved && b.Winner < 0; i++) { b.Player.Pos = hob.Pos + new Vector2(1, 0); b.Update(1 / 60f); }
        Check(hob.Saved && !b.Rescuable(hob), "standing over him gets him out");
        Check(b.Result().Companies.First(r => r.Id == 7).Named!.First(n => n.Id == 5) is { Fell: true, Saved: true }, "the result says he fell but was saved");
        Check(b.GoalState(goal) >= 0, "and the goal is not lost");
        // A second battle: he dies and nobody comes.
        var b2 = new Battle(6, new ArmySpec("mine", 0, new List<Company> { spec }), new ArmySpec("theirs", 0, new List<Company> { new("levies", 30, "infantry", 9) }), new BattleOptions());
        var hob2 = b2.Units.First(u => u.SoldierId == 5);
        b2.Ward = hob2;
        b2.KillForTest(hob2, b2.SquadsOf(1).First().Units[0]);
        b2.Player.Pos = hob2.Pos + new Vector2(30, 0);
        for (int i = 0; i < 60 * 65 && b2.Winner < 0; i++) b2.Update(1 / 60f);
        Check(!hob2.Saved && b2.GoalState(goal) == -1, "left where he fell, he is lost, and the goal with him");
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
        // With the banner, the last stand and goals (P6), rallying now and then.
        var p6 = new Battle(32, Armies.Random(rng, 2, 0), Armies.Random(rng, 2, 1), new BattleOptions { Banner = true, LastStand = true, Goals = true });
        p6.StartRecording(32);
        for (int step = 0; p6.Winner < 0 && step < 60 * 300; step++)
        {
            p6.Input = new PlayerInput { Facing = MathHelper.PiOver2, Move = new Vector2(0, 1), Attack = step % 50 < 20, Rally = step % 400 == 399 };
            p6.Update(1 / 60f);
        }
        var rec6 = p6.FinishRecording()!;
        var back6 = Replay.Deserialize(rec6.Serialize());
        Check(back6.Matches(back6.Play()), "and with a banner, a last stand and goals");
    }

    /// Braced spears hurt a lancer charging into them (also the example in docs/DEVELOPING.md 9).
    static void SpearsStopHorse()
    {
        var b = Battle.Empty(7);
        b.AddSquad(1, Troops.Get("spearrunners"), 12, new(0, 0), 6, -Vector2.UnitY);
        var l = Lancer(b);                       // team 0, galloping at the spears
        Run(b, 6);
        Check(l.Dead || l.Hp < l.Type.MaxHp, "braced spears hurt a charging lancer");
    }

    /// Battle scale (docs/archive/COMBAT_OVERHAUL.md 2.7): a 1,200-man battle deploys without companies on top of each other,
    /// stays on a wider field, runs well under a frame per tick, and replays exactly.
    static void ScaleBattle()
    {
        var rng = new Random(12);
        var b = new Battle(12, Armies.Divide(Armies.Large(rng, 2, 0, 600)), Armies.Divide(Armies.Large(rng, 3, 1, 600)), new BattleOptions { Cap = 1500 });
        Check(b.DivisionsOf(0).Count() >= 2 && b.DivisionsOf(1).Count() >= 2 && b.Divisions.All(d => d.Leader?.Sergeant == true), $"big armies split into divisions under sergeants ({b.Divisions.Count})");
        int men = b.Units.Count(u => u.Active);
        Check(men >= 1200, $"a big battle puts everyone on the field ({men})");
        Check(b.MapRadius > 120, "a big battle is fought on a wider field");
        var sq = b.Squads.Where(q => q.Company != null).ToList();
        int overlaps = 0;
        for (int i = 0; i < sq.Count; i++)
            for (int j = i + 1; j < sq.Count; j++)
                if (sq[i].Units.Any(u => sq[j].Units.Any(o => Vector2.DistanceSquared(u.Pos, o.Pos) < 0.5f))) overlaps++;
        Check(overlaps == 0, $"no two companies deploy on top of each other ({overlaps})");
        b.StartRecording(12);
        var mine = b.Commanded.ToList();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int ticks = 0;
        for (; b.Winner < 0 && ticks < 60 * 40; ticks++)
        {
            if (ticks == 30) foreach (var s in mine) s.Order = Order.Charge;
            b.Update(1 / 60f);
        }
        double ms = clock.Elapsed.TotalMilliseconds / ticks;
        Console.WriteLine($"scale: {men} men, field {b.MapRadius:0} m, {ms:0.00} ms per tick, {b.Units.Count(u => u.Dead)} dead after {b.Time:0}s");
        Check(ms < 12, $"a 1,200-man battle runs under 12 ms per tick ({ms:0.0})");
        Check(b.Units.All(u => float.IsFinite(u.Pos.X) && float.IsFinite(u.Pos.Y)), "nobody's position goes bad in a big battle");
        Check(b.Units.Count(u => u.Dead) > 50, "the big armies meet and fight");
        var rec = b.FinishRecording()!;
        var again = Replay.Deserialize(rec.Serialize()).Play();
        Check(rec.Winner == again.Winner && again.Units.Count(u => u.Dead) == rec.Dead, "a big battle replays exactly");
    }

    /// Divisions (docs/archive/COMBAT_OVERHAUL.md 1.8): goals arrive late and a newer one cancels an older; a Hold goal keeps the
    /// division near its ground; Flank swings wide and hits a side; a fallen sergeant is replaced; direct orders wait
    /// for the delay too; and a battle with divisions and goals replays exactly.
    /// Orders ride out as couriers (docs/archive/COMBAT_OVERHAUL.md 1.4): a rider who meets the enemy on the road is cut down and
    /// the order lands later; with couriers off the same order lands on time.
    static void Couriers()
    {
        float Landed(bool couriers, bool blocked)
        {
            var b = Battle.Empty(31);
            b.Options.Couriers = couriers;
            var far = b.AddSquad(0, Troops.Get("shieldmen"), 12, new(60, -20), 6, Vector2.UnitY);
            var d = b.AddDivision(0, "Sgt Far", new[] { far }, Temper.Steady, 3);
            if (blocked) b.AddSquad(1, Troops.Get("levies"), 16, new(30, -50), 8, Vector2.UnitX); // across the road (you stand at 0,-80)
            Run(b, 0.2f);
            b.IssueGoal(d, new Goal(GoalKind.Take, new(60, -10)));
            if (couriers) Check(b.Couriers.Count == 1, "a goal sends a rider");
            for (int i = 0; i < 60 * 30 && d.Goal.Kind != GoalKind.Take; i++) b.Update(1 / 60f);
            return d.Goal.Kind == GoalKind.Take ? b.Time : float.MaxValue;
        }
        float plain = Landed(false, false), clear = Landed(true, false), cut = Landed(true, true);
        Console.WriteLine($"couriers: order lands at {plain:0.0}s plain, {clear:0.0}s by rider, {cut:0.0}s with the road blocked");
        Check(MathF.Abs(plain - clear) < 0.1f, "a rider on a clear road arrives when the delay says");
        Check(cut > clear + 1 && cut < clear + 30, "a rider cut down on the road makes the order late, and the next goes round");
    }

    static void DivisionGoals()
    {
        // Delay and countermand.
        var b = Battle.Empty(21);
        var a = b.AddSquad(0, Troops.Get("shieldmen"), 16, new(0, -20), 8, Vector2.UnitY);
        var d = b.AddDivision(0, "Sgt Test", new[] { a }, Temper.Steady, 3);
        Run(b, 0.5f);
        float delay = b.IssueGoal(d, new Goal(GoalKind.Take, new(30, -20)));
        Check(delay > 1.5f, $"an order takes time to reach a sergeant ({delay:0.0}s)");
        Run(b, delay * 0.5f);
        Check(d.Goal.Kind == GoalKind.Hold && d.Pending != null, "the goal has not arrived halfway through the delay");
        float again = b.IssueGoal(d, new Goal(GoalKind.FallBack, new(0, -40)));
        bool sawTake = false;
        for (int i = 0; i < (again + 1) * 60; i++) { b.Update(1 / 60f); sawTake |= d.Goal.Kind == GoalKind.Take; }
        Check(!sawTake && d.Goal.Kind == GoalKind.FallBack, "a newer goal cancels one still on its way");
        Run(b, 15);
        Check(Vector2.Distance(a.Centroid(), new Vector2(0, -40)) < 6, "the division falls back to the point it was given");

        // Direct order: waits for the delay, then the company follows it and not its sergeant.
        float wait = b.Direct(a, s => { s.Order = Order.Charge; });
        Check(wait > 1 && a.Order == Order.Hold, "a direct order to a division's company is not obeyed at once");
        Run(b, wait + 0.2f);
        Check(a.Override && a.Order == Order.Charge, "the direct order lands after the delay and the sergeant leaves it alone");
        b.IssueGoal(d, new Goal(GoalKind.Hold, new(0, -40)));
        Run(b, 6);
        Check(!a.Override, "a new goal hands the company back to its sergeant");

        // Hold against an enemy that comes close and then draws off: the division does not follow it far.
        b = Battle.Empty(22);
        var mine = b.AddSquad(0, Troops.Get("men_at_arms"), 20, new(0, 0), 10, Vector2.UnitY);
        var bait = b.AddSquad(1, Troops.Get("levies"), 12, new(0, 14), 6, -Vector2.UnitY);
        d = b.AddDivision(0, "Sgt Hold", new[] { mine }, Temper.Steady, 5, new Goal(GoalKind.Hold, new(0, 0)));
        bait.Order = Order.Charge;
        float farthest = 0;
        for (int i = 0; i < 60 * 40; i++)
        {
            if (i == 60 * 4) { bait.Order = Order.Hold; bait.Anchor = new(0, 70); } // draw off
            b.Update(1 / 60f);
            farthest = MathF.Max(farthest, mine.Centroid().Length());
        }
        Console.WriteLine($"divisions: hold leash {farthest:0} m");
        Check(mine.Centroid().Length() < 12, "a Hold division comes back to its ground");
        Check(farthest < 40, "a steady sergeant does not chase far from the ground he holds");

        // Flank: around the side of a holding line, then into it.
        b = Battle.Empty(23);
        var line = b.AddSquad(1, Troops.Get("shieldmen"), 30, new(0, 30), 15, -Vector2.UnitY);
        var wing = b.AddSquad(0, Troops.Get("men_at_arms"), 16, new(0, -10), 8, Vector2.UnitY);
        d = b.AddDivision(0, "Sgt Wing", new[] { wing }, Temper.Bold, 6, new Goal(GoalKind.Flank, Side: 1));
        float wide = 0, pressure = 0;
        for (int i = 0; i < 60 * 70 && (i < 2 || line.Alive > 0); i++)
        {
            b.Update(1 / 60f);
            wide = MathF.Max(wide, MathF.Abs(wing.Centroid().X));
            pressure = MathF.Max(pressure, line.Pressure);
        }
        Console.WriteLine($"divisions: flank swung {wide:0} m wide, line pressure {pressure:0.0}");
        Check(wide > 20, "a flanking division swings wide of the enemy line");
        Check(pressure > 0.5f, "the flank attack lands on the side or rear");

        // A fallen sergeant: the men fight on, then someone takes over.
        b = Battle.Empty(24);
        var c1 = b.AddSquad(0, Troops.Get("shieldmen"), 12, new(-10, 0), 6, Vector2.UnitY);
        var c2 = b.AddSquad(0, Troops.Get("bowmen"), 10, new(10, 0), 5, Vector2.UnitY);
        d = b.AddDivision(0, "Sgt Doomed", new[] { c1, c2 }, Temper.Steady, 3);
        var first = d.Leader!;
        Run(b, 1);
        float before = b.OrderDelay(d);
        first.Dead = true;
        Run(b, 1);
        Check(d.Leaderless && b.OrderDelay(d) > before * 1.5f, "a division without its sergeant hears orders late");
        Run(b, 11);
        Check(!d.Leaderless && d.Leader != first && d.Leader!.Active, "the best man left takes command after a while");

        // Replay with divisions built from the army and goals sent during the fight.
        var rng = new Random(25);
        ArmySpec Div(ArmySpec x) => x with
        {
            Companies = x.Companies.Select((c, i) => c with { Id = 100 + i, Division = i < 2 ? 0 : i == 2 ? 1 : -1 }).ToList(),
            Divisions = new[] { new DivisionSpec(0, "Sgt Left", "Bold", 4, "Hold"), new DivisionSpec(1, "Sgt Bows", "Cautious", 2, "Hold") },
        };
        b = new Battle(25, Div(Armies.Random(rng, 2, 0)), Armies.Random(rng, 2, 1), new BattleOptions());
        Check(b.DivisionsOf(0).Count() == 2 && b.DivisionsOf(0).All(x => x.Squads.Count > 0 && x.Leader != null), "divisions are built from the army's specs");
        b.StartRecording(25);
        for (int step = 0; b.Winner < 0 && step < 60 * 200; step++)
        {
            if (step == 90) b.IssueGoal(b.Divisions[0], new Goal(GoalKind.Attack, Target: b.SquadsOf(1).First(s => s.Company != null)));
            if (step == 200) b.IssueGoal(b.Divisions[1], new Goal(GoalKind.Harass));
            if (step == 300) b.Direct(b.Divisions[1].Squads[0], s => s.Formation = Formation.Loose);
            if (step == 400) b.IssueGoal(b.Divisions[0], new Goal(GoalKind.Pursue));
            b.Update(1 / 60f);
        }
        var rec = b.FinishRecording()!;
        var back = Replay.Deserialize(rec.Serialize());
        var replayed = back.Play();
        Check(back.Matches(replayed), "a battle with divisions and goals replays exactly");
    }
}

