using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the battle sim: mechanics scenarios (charges, parries, flanks, cavalry, ranged), troops, sprites, full battles, replays.
public static partial class SelfTest
{
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
    static bool Fight(Battle b, IReadOnlyList<Unit> foes, bool parry)
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
        Check(m.Rivers.Count == 4 && m.Rivers.All(rv => rv.Fords.Length > 0), "the map has four rivers with bridges");
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
        Check(MathF.Abs(t.Height(new Vector2(-Terrain.BattleHalf + 10 * Terrain.BattleStep, -Terrain.BattleHalf + 7 * Terrain.BattleStep)) - t.GridHeight(10, 7)) < 1e-4f,
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
        Check(Troops.All.Where(t => !t.Leader && !t.Wild).All(t => reachable.Contains(t.Id)), "every troop can be reached by upgrading a culture's recruits");
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

    /// Battle scale (docs/COMBAT_OVERHAUL.md 2.7): a 1,200-man battle deploys without companies on top of each other,
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

    /// Divisions (docs/COMBAT_OVERHAUL.md 1.8): goals arrive late and a newer one cancels an older; a Hold goal keeps the
    /// division near its ground; Flank swings wide and hits a side; a fallen sergeant is replaced; direct orders wait
    /// for the delay too; and a battle with divisions and goals replays exactly.
    /// Orders ride out as couriers (docs/COMBAT_OVERHAUL.md 1.4): a rider who meets the enemy on the road is cut down and
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

