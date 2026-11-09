using Microsoft.Xna.Framework;

namespace Ironfield;

/// What a sergeant can be told to do (docs/COMBAT_OVERHAUL.md 1.2).
public enum GoalKind { Hold, Take, Attack, Flank, Screen, Guard, Reserve, Harass, Pursue, FallBack, Ambush }

/// How a sergeant fights: Bold charges early and chases far; Steady keeps formation and waits for contact;
/// Cautious keeps a short leash and lets the enemy come to him.
public enum Temper { Bold, Steady, Cautious }

/// A sergeant's goal. Point is used by Hold, Take, FallBack and Ambush; Target by Attack (an enemy squad) and Screen (a friendly
/// one); Side by Flank (+1 his right, -1 his left). Null Target: he picks one himself.
public sealed record Goal(GoalKind Kind, Vector2 Point = default, Squad? Target = null, int Side = 1)
{
    public string Describe => Kind switch
    {
        GoalKind.Hold => "hold this ground",
        GoalKind.Take => "take that ground",
        GoalKind.Attack => Target != null ? $"attack the {Target.Name}" : "attack",
        GoalKind.Flank => Side > 0 ? "go round their flank on our right" : "go round their flank on our left",
        GoalKind.Screen => Target != null ? $"screen the {Target.Name}" : "screen our line",
        GoalKind.Guard => "guard the captain",
        GoalKind.Reserve => "wait in reserve",
        GoalKind.Harass => "harass them",
        GoalKind.Pursue => "run them down",
        GoalKind.Ambush => "lie in wait",
        _ => "fall back",
    };

    /// Short name for buttons and cards.
    public static string Label(GoalKind k) => k switch { GoalKind.FallBack => "FALL BACK", _ => k.ToString().ToUpperInvariant() };
}

/// A division: a few companies led by a sergeant who turns a goal into orders, and hears new goals late.
public sealed class Division
{
    public readonly int Team, Id;
    /// The DivisionSpec.Id it was built from (-1 for one made in a test or scenario).
    public int SpecId = -1;
    public readonly string Name;
    public readonly Temper Temper;
    public readonly int Skill;
    public readonly List<Squad> Squads = new();
    public Unit? Leader;
    public Goal Goal;
    /// A goal on its way (a rider or a horn call): it takes over at PendingAt. A newer goal replaces it.
    public Goal? Pending;
    public float PendingAt, GoalSince, LeaderlessSince = -1, NextThink;
    /// Step within the goal (Flank: 0 swing wide, 1 come alongside, 2 hit them; Attack: 1 once charging).
    public int Phase;
    /// Flank: the direction toward the enemy when the goal began, so the swing does not chase its own tail.
    public Vector2 Axis;

    public Division(int team, int id, string name, Temper temper, int skill, Goal goal)
    {
        Team = team; Id = id; Name = name; Temper = temper; Skill = Math.Clamp(skill, 0, 10); Goal = goal;
    }

    public int Alive => Squads.Sum(s => s.Alive);
    public int Start => Squads.Sum(s => s.StartCount);
    public bool Leaderless => LeaderlessSince >= 0;
}

/// Battle: divisions (docs/COMBAT_OVERHAUL.md 1.3-1.4). Each sergeant thinks every half second or so (quicker with
/// skill) and sets his companies' orders from his goal; the unit AI does the rest. Goals and your direct orders to his
/// companies arrive after a delay that grows with the distance from you.
public partial class Battle
{
    public readonly List<Division> Divisions = new();
    public IEnumerable<Division> DivisionsOf(int team) => Divisions.Where(d => d.Team == team);
    /// Metres a second an order travels (a rider at the gallop).
    public const float CourierSpeed = 14;

    /// Make a division of these companies. Leader: the given man, else the company's companion, else its best man.
    public Division AddDivision(int team, string name, IEnumerable<Squad> squads, Temper temper, int skill, Goal? goal = null, Unit? leader = null)
    {
        var list = squads.ToList();
        var at = list.Count > 0 ? list.Aggregate(Vector2.Zero, (a, s) => a + s.Centroid()) / list.Count : centroid[team];
        var d = new Division(team, Divisions.Count, name, temper, skill, goal ?? new Goal(GoalKind.Hold, at));
        foreach (var s in list) { d.Squads.Add(s); s.Division = d; if (!s.Swarm) s.Drill = MathF.Min(1, s.Drill + Math.Clamp(skill, 0, 10) * 0.01f); } // a sergeant drills his men
        d.Leader = leader ?? PickLeader(d);
        if (d.Leader != null) d.Leader.Sergeant = true;
        Divisions.Add(d);
        return d;
    }

    /// The man who leads: a companion if there is one, else the best-armed, healthiest man standing.
    static Unit? PickLeader(Division d) => d.Squads.SelectMany(s => s.Units).Where(u => u.Active && !u.Routing)
        .OrderByDescending(u => u.Hero).ThenByDescending(u => u.Type.Tier).ThenByDescending(u => u.Hp).ThenBy(u => u.Index).FirstOrDefault();

    /// Divisions from the army's DivisionSpecs (not in an assault: the wall fight keeps its own orders).
    void BuildDivisions(int team, ArmySpec army)
    {
        if (army.Divisions == null || Options.Siege) return;
        foreach (var spec in army.Divisions)
        {
            var squads = Squads.Where(s => s.Team == team && s.Company?.Division == spec.Id).ToList();
            var temper = Enum.TryParse<Temper>(spec.Temper, out var t) ? t : Temper.Steady;
            var hero = spec.Leader.Length > 0 ? squads.SelectMany(s => s.Units).FirstOrDefault(u => u.Hero && u.Squad?.Company?.Hero == spec.Leader) : null;
            hero ??= (squads.FirstOrDefault(s => spec.LeadCompany != 0 && s.Company?.Id == spec.LeadCompany) ?? squads.FirstOrDefault())?.Units
                .Where(u => u.Active).OrderByDescending(u => u.Type.Tier).ThenBy(u => u.Index).FirstOrDefault(); // the sergeant's own company
            var d = AddDivision(team, spec.Name, squads, temper, spec.Skill, null, hero);
            d.SpecId = spec.Id;
            d.Goal = StartGoal(d, spec.Goal);
        }
    }

    /// A goal from its saved name ("Hold", "Attack", "FlankLeft", "FlankRight", ...), placed where the division stands.
    Goal StartGoal(Division d, string name)
    {
        var at = d.Squads.Count > 0 ? d.Squads.Aggregate(Vector2.Zero, (a, s) => a + s.Centroid()) / d.Squads.Count : centroid[d.Team];
        if (name == "FlankLeft") return new Goal(GoalKind.Flank, at, Side: -1);
        if (name == "FlankRight") return new Goal(GoalKind.Flank, at, Side: 1);
        return new Goal(Enum.TryParse<GoalKind>(name, out var k) ? k : GoalKind.Hold, at);
    }

    /// Seconds an order takes to reach the sergeant: a base, the ride from the general, longer if he is fighting or
    /// dead (his men wait for someone to take charge), shorter with his skill and with the Horns perk.
    public float OrderDelay(Division d)
    {
        var from = GeneralPos(d.Team);
        var to = CourierGoal(d);
        float t = 1.5f + Vector2.Distance(from, to) / CourierSpeed;
        if (d.Leader is { Active: true, EnemyDist: < 5 }) t += 2;
        if (d.Leader is not { Active: true }) t *= 2;
        t *= 1 - 0.05f * d.Skill;
        if (d.Team == 0 && Options.Horns) t *= 0.7f;
        if (d.Team == 0 && Perk("MasterOfWar")) t *= 0.5f;
        return t;
    }

    /// Send a division a new goal: it rides out now and takes over when it arrives (replacing any goal still on its way).
    public float IssueGoal(Division d, Goal g, bool now = false, bool ai = false)
    {
        float delay = now ? 0 : OrderDelay(d);
        bool countermand = d.Pending != null;
        d.Pending = g;
        d.PendingAt = Time + delay;
        Couriers.RemoveAll(c => c.Div == d && c.Goal); // a countermand overtakes the rider still on the road
        SendCourier(d, Time + delay, true);
        if (Recording != null && d.Team == 0 && !ai)
            (goalRecords ??= new()).Add(new Replay.GoalOrder { Division = d.Id, Kind = g.Kind, Point = g.Point, Target = g.Target != null ? Squads.IndexOf(g.Target) : -1, Side = g.Side });
        if (d.Team == 0) Log(countermand ? $"New orders ride to {d.Name}: {g.Describe} ({delay:0}s)." : $"Orders ride to {d.Name}: {g.Describe} ({delay:0}s).");
        return delay;
    }
    List<Replay.GoalOrder>? goalRecords;

    /// An order to one company. Yours (not in a division) obey at once; a division's hear it after the order delay,
    /// then follow it and not their sergeant until he gets a new goal. Returns the delay in seconds.
    public float Direct(Squad s, Action<Squad> act)
    {
        if (s.Division == null) { act(s); return 0; }
        float delay = OrderDelay(s.Division);
        directs.Add((Time + delay, s, act));
        if (!Couriers.Any(c => c.Div == s.Division && !c.Goal && c.At == Time + delay)) SendCourier(s.Division, Time + delay, false);
        return delay;
    }
    readonly List<(float At, Squad S, Action<Squad> Act)> directs = new();
    /// Your direct orders still riding to a division's companies (for the HUD).
    public IEnumerable<(Squad S, float Eta)> PendingDirects => directs.Select(x => (x.S, x.At - Time));

    void ApplyDirects()
    {
        for (int i = 0; i < directs.Count; i++)
            if (Time >= directs[i].At)
            {
                directs[i].Act(directs[i].S);
                directs[i].S.Override = true;
                directs.RemoveAt(i--);
            }
    }

    void UpdateDivisions()
    {
        UpdateCouriers();
        if (Time >= nextAiCommand && Divisions.Count > 0)
        {
            nextAiCommand = Time + 4;
            AiCommand(1);
            if (Options.AutoPlayer) AiCommand(0);
        }
        foreach (var d in Divisions)
        {
            if (d.Pending != null && Time >= d.PendingAt)
            {
                d.Goal = d.Pending;
                d.Pending = null;
                d.GoalSince = Time;
                d.Phase = 0;
                d.Axis = Vector2.Zero;
                d.NextThink = Time;
                foreach (var s in d.Squads) { s.Override = false; s.Pursue = false; }
                if (d.Team == 0) Log($"{d.Name}: \"{Capital(d.Goal.Describe)}!\"");
            }
            if (Time < d.NextThink) continue;
            d.NextThink = Time + 0.5f + (10 - d.Skill) * 0.06f; // a sharp sergeant reacts quicker
            if (!LeaderCheck(d) || d.Alive == 0) continue;
            Brain(d);
        }
    }

    float nextAiCommand = 4;
    /// How near an enemy company must come before a division lying in wait falls on it.
    public const float AmbushReach = 40;
    /// The AI's divisions that have already been offered the chance to lie in wait.
    readonly HashSet<Division> ambushTried = new();

    /// The AI general's divisions (and yours when the AI plays you): wings that hold send in their men once the
    /// lines close, the fight starts, or they have waited long enough; a broken division falls back.
    void AiCommand(int team)
    {
        int foe = 1 - team;
        bool fighting = SquadsOf(team).Any(s => s.Alive > 0 && s.Division == null && s.Units.Any(u => u.Active && u.EnemyDist < 5));
        foreach (var d in DivisionsOf(team))
        {
            if (d.Pending != null || d.Alive == 0) continue;
            var live = d.Squads.Where(s => s.Alive > 0).ToList();
            var at = live.Aggregate(Vector2.Zero, (a, s) => a + s.Center) / live.Count;
            var near = EnemySquadNear(at, float.MaxValue, foe);
            if (near == null) continue;
            float gap = Vector2.Distance(at, near.Center) - near.Radius;
            float wait = d.Temper switch { Temper.Bold => 15, Temper.Steady => 30, _ => 45 };
            if (d.Goal.Kind == GoalKind.Hold && d.Temper != Temper.Bold && !fighting && gap > 50 && ambushTried.Add(d) && AmbushWood(d, live, at, foe) is Vector2 wood)
                IssueGoal(d, new Goal(GoalKind.Ambush, wood), ai: true); // a wood near by on the enemy's way: wait in it
            else if (d.Goal.Kind is GoalKind.Hold or GoalKind.Reserve && (fighting || gap < 30 || Time - d.GoalSince > wait))
                IssueGoal(d, new Goal(GoalKind.Attack, Target: near), ai: true);
            else if (d.Goal.Kind == GoalKind.Ambush && Time - d.GoalSince > wait * 3)
                IssueGoal(d, new Goal(GoalKind.Attack, Target: near), ai: true); // nobody came: go and find them
            else if (d.Goal.Kind == GoalKind.Attack && d.Alive < d.Start / 4 && d.Temper != Temper.Bold)
                IssueGoal(d, new Goal(GoalKind.FallBack, at + Toward(at, centroid[foe], team) * -30), ai: true);
        }
    }

    /// A wood edge to lie in wait at: within 50 m of the division, no further back than it stands, and of foot only.
    Vector2? AmbushWood(Division d, List<Squad> live, Vector2 at, int foe)
    {
        if (live.Any(s => s.Cavalry || Ranged(s))) return null;
        var fwd = Toward(at, centroid[foe], d.Team);
        return BestPlace(at, 50, p => p.Kind == PlaceKind.WoodEdge && Vector2.Dot(p.Pos - at, fwd) > -5 ? p.Hide : 0)?.Pos;
    }

    static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// True when the division has someone to lead it. A fallen sergeant leaves his men on their last orders (a morale
    /// blow, slower orders); after 10 s the best man left takes charge.
    bool LeaderCheck(Division d)
    {
        if (d.Leader is { Active: true }) return true;
        if (d.LeaderlessSince < 0)
        {
            d.LeaderlessSince = Time;
            foreach (var s in d.Squads) foreach (var u in s.Units) if (u.Active) u.Morale -= 8;
            Log(d.Team == 0 ? $"{d.Name} has fallen! His men fight on without orders." : $"An enemy sergeant falls: {d.Name}'s men waver.");
            return false;
        }
        if (Time - d.LeaderlessSince < 10) return false;
        var next = PickLeader(d);
        if (next == null) return false;
        d.Leader = next;
        next.Sergeant = true;
        d.LeaderlessSince = -1;
        if (d.Team == 0) Log($"A {next.Type.Name} takes command of {d.Name}'s men.");
        return true;
    }

    /// The sergeant's half-second decision: from his goal and what he sees, set each company's order.
    void Brain(Division d)
    {
        var live = d.Squads.Where(s => s.Alive > 0 && !s.Override).ToList();
        if (live.Count == 0) return;
        int foe = 1 - d.Team;
        var center = live.Aggregate(Vector2.Zero, (a, s) => a + s.Center) / live.Count;
        var dir = Toward(center, centroid[foe], d.Team);
        float chargeAt = d.Temper switch { Temper.Bold => 22, Temper.Steady => 14, _ => 9 };
        float leash = d.Temper switch { Temper.Bold => 35, Temper.Steady => 20, _ => 12 };
        var g = d.Goal;
        switch (g.Kind)
        {
            case GoalKind.Hold:
            {
                var at = SnapToGround(g.Point); // the best ground near where he was told (BattleGround.cs)
                HoldGround(d, live, at, Toward(at, centroid[foe], d.Team), chargeAt * 0.7f, leash);
                break;
            }
            case GoalKind.Take:
            {
                var at = SnapToGround(g.Point);
                var threat = EnemySquadNear(at, 15, foe);
                if (threat != null) Engage(d, live, threat);
                else if (Vector2.Distance(center, at) > 6) Form(d, live, at, dir);
                else HoldGround(d, live, at, dir, chargeAt * 0.7f, leash);
                break;
            }
            case GoalKind.Ambush:
            {
                // Wait still and hidden at the point; when an enemy company comes within 40 m, fall on it.
                var prey = EnemySquadNear(center, AmbushReach, foe);
                if (prey != null)
                {
                    d.Goal = new Goal(GoalKind.Attack, Target: prey);
                    d.Phase = 1;
                    Engage(d, live, prey);
                    if (d.Team == 0) Log($"{d.Name} springs the ambush!");
                    else Log("An ambush! The enemy breaks cover.");
                }
                else Form(d, live, g.Point, Toward(g.Point, centroid[foe], d.Team));
                foreach (var s in live) s.FireAtWill = false; // no arrows to give them away
                break;
            }
            case GoalKind.Attack:
            {
                var t = g.Target is { Alive: > 0 } ? g.Target : EnemySquadNear(g.Target?.Center ?? center, float.MaxValue, foe);
                if (t == null) { HoldGround(d, live, center, dir, chargeAt, leash); break; }
                if (t != g.Target) d.Goal = g with { Target = t };
                float gap = Vector2.Distance(center, t.Center) - t.Radius;
                if (d.Phase == 1 || gap < chargeAt) { d.Phase = 1; Engage(d, live, t); }
                else Form(d, live, center + Toward(center, t.Center, d.Team) * MathF.Min(10, gap), Toward(center, t.Center, d.Team));
                break;
            }
            case GoalKind.Flank: Flank(d, live, center, g.Side); break;
            case GoalKind.Screen:
            {
                var friend = g.Target is { Alive: > 0 } f ? f : SquadsOf(d.Team).Where(s => s.Division != d && s.Alive > 0 && s.Company != null)
                    .OrderByDescending(s => s.Units[0].Type.Ammo > 0).ThenBy(s => Vector2.Distance(s.Center, center)).FirstOrDefault();
                if (friend == null) { HoldGround(d, live, center, dir, chargeAt, leash); break; }
                var threat = EnemySquadNear(friend.Center, float.MaxValue, foe);
                var to = threat != null ? Toward(friend.Center, threat.Center, d.Team) : dir;
                HoldGround(d, live, friend.Center + to * (friend.Radius + 9), to, 12, 22);
                break;
            }
            case GoalKind.Guard:
            {
                var general = d.Team == 0 ? Player : EnemyCaptain;
                if (!general.Active) { HoldGround(d, live, center, dir, chargeAt, leash); break; }
                var threat = EnemySquadNear(general.Pos, 14, foe);
                if (threat != null) Engage(d, live, threat);
                else Form(d, live, general.Pos + Toward(general.Pos, centroid[foe], d.Team) * 7, Toward(general.Pos, centroid[foe], d.Team));
                break;
            }
            case GoalKind.Reserve: Reserve(d, live, dir); break;
            case GoalKind.Harass: Harass(d, live, center, foe); break;
            case GoalKind.Pursue:
            {
                bool any = Units.Any(u => u.Team == foe && u.Active && Vector2.DistanceSquared(u.Pos, center) < 60 * 60);
                foreach (var s in live)
                {
                    s.Pursue = any;
                    if (any) { s.Order = Order.Charge; s.Target = null; }
                }
                if (!any) Form(d, live, center, dir); // nobody left to catch: rally where they stand
                break;
            }
            default: // FallBack
                Form(d, live, g.Point, Toward(g.Point, centroid[foe], d.Team));
                break;
        }
    }

    /// Unit vector from a toward b (the side's own forward when they are on top of each other).
    static Vector2 Toward(Vector2 a, Vector2 b, int team)
    {
        var v = b - a;
        return v.LengthSquared() > 1 ? Vector2.Normalize(v) : new Vector2(0, team == 0 ? 1 : -1);
    }

    /// The enemy squad whose edge is nearest p, if it is within `within` metres.
    Squad? EnemySquadNear(Vector2 p, float within, int foe)
    {
        Squad? best = null;
        float bd = within;
        foreach (var s in Squads)
        {
            if (s.Team != foe || s.Alive == 0 || s == eGuard) continue;
            float d = Vector2.Distance(s.Center, p) - s.Radius;
            if (d < bd) { bd = d; best = s; }
        }
        return best;
    }

    static bool Ranged(Squad s) => s.Units.Count > 0 && s.Units[0].Type.Ammo > 0 && s.Units[0].Type.Class == UnitClass.Archer;

    /// Stand in formation at `at`, facing `front`: foot side by side, archers behind them, horse on both wings.
    void Form(Division d, List<Squad> live, Vector2 at, Vector2 front)
    {
        var right = new Vector2(front.Y, -front.X);
        var foot = live.Where(s => !s.Cavalry && !Ranged(s)).ToList();
        var bows = live.Where(Ranged).ToList();
        var horse = live.Where(s => s.Cavalry && !Ranged(s)).ToList();
        float Width(Squad s) => Math.Clamp(s.Columns, 1, Math.Max(1, s.Alive)) * s.Spacing;
        float Depth(Squad s) => (s.Alive + Math.Max(1, s.Columns) - 1) / Math.Max(1, s.Columns) * s.Spacing;
        void Row(List<Squad> row, Vector2 c)
        {
            float total = row.Sum(Width) + 4 * Math.Max(0, row.Count - 1), x = -total / 2;
            foreach (var s in row) { Place(s, c + right * (x + Width(s) / 2), front); x += Width(s) + 4; }
        }
        // With no foot, the archers are the front line.
        if (foot.Count == 0) { foot = bows; bows = new(); }
        Row(foot, at);
        float footHalf = (foot.Sum(Width) + 4 * Math.Max(0, foot.Count - 1)) / 2;
        float back = foot.Count > 0 ? foot.Max(Depth) + 5 : 0;
        if (bows.Count > 0) Row(bows, at - front * back);
        for (int i = 0; i < horse.Count; i++)
        {
            float side = i % 2 == 0 ? 1 : -1, w = Width(horse[i]);
            Place(horse[i], at + right * side * (footHalf + 6 + w / 2 + i / 2 * (w + 4)), front);
        }
        foreach (var s in live) s.Pursue = false;
    }

    /// One company to its place: an order to hold there (moved only when the place changes, so the men do not shuffle).
    static void Place(Squad s, Vector2 anchor, Vector2 front)
    {
        if (s.BrokeRanks) return; // it has broken ranks and charged: it comes back when it has re-formed
        if (s.Order != Order.Hold || Vector2.DistanceSquared(s.Anchor, anchor) > 2.25f || Vector2.Dot(s.Front, front) < 0.98f)
        {
            s.Order = Order.Hold;
            s.Target = null;
            s.Anchor = anchor;
            s.Front = front;
        }
    }

    /// Close with the target: foot and horse charge it, archers shoot it from where they are (and draw blades when it
    /// comes within 7 m or their arrows are gone).
    void Engage(Division d, List<Squad> live, Squad target)
    {
        bool lineIn = live.Any(s => !s.Cavalry && !Ranged(s) && s.Order == Order.Charge);
        foreach (var s in live)
        {
            if (Ranged(s))
            {
                bool dry = s.Units.All(u => !u.Active || u.Ammo == 0);
                float gap = Vector2.Distance(s.Center, target.Center) - target.Radius - s.Radius;
                if (dry || gap < 7) { s.Order = Order.Charge; s.Target = target; continue; }
                s.Order = Order.Hold;
                s.Target = target;
                s.FireAtWill = true;
                float range = s.Units[0].Type.Range;
                if (gap > range - 4) { s.Front = Toward(s.Center, target.Center, d.Team); s.Anchor = s.Center + s.Front * 5; }
                continue;
            }
            // Steady and cautious sergeants hold their horse until the foot are in; bold ones send everyone at once.
            if (s.Cavalry && d.Temper != Temper.Bold && !lineIn && live.Any(o => !o.Cavalry && !Ranged(o))) { s.Order = Order.Charge; s.Target = SoftTarget(s, 1 - d.Team) ?? target; continue; }
            s.Order = Order.Charge;
            s.Target = target;
        }
    }

    /// Hold `at`: form there; charge an enemy company that comes within `reach` (a cautious sergeant never does: his men
    /// still fight what walks into them); come back when the fight has pulled them `leash` metres away. Shields lock
    /// against horse.
    void HoldGround(Division d, List<Squad> live, Vector2 at, Vector2 front, float reach, float leash)
    {
        int foe = 1 - d.Team;
        var threat = d.Temper == Temper.Cautious ? null : EnemySquadNear(at, reach, foe);
        var center = live.Aggregate(Vector2.Zero, (a, s) => a + s.Center) / live.Count;
        if (threat != null && Vector2.Distance(center, at) < leash) Engage(d, live, threat);
        else Form(d, live, at, front);
        bool horseNear = Squads.Any(s => s.Team == foe && s.Cavalry && s.Alive > 0 && Vector2.Distance(s.Center, at) - s.Radius < 35);
        foreach (var s in live.Where(s => !s.Cavalry && !Ranged(s) && !s.Swarm && s.Units[0].Type.HasShield))
            s.Formation = horseNear && d.Temper != Temper.Bold && s.Order == Order.Hold ? Formation.ShieldWall : Formation.Line;
        foreach (var s in live.Where(Ranged))
            if (s.Order == Order.Hold) { s.Target = SoftTarget(s, foe); s.FireAtWill = true; }
    }

    /// Go round the enemy's flank: swing wide on `side`, come alongside, then hit the company nearest.
    void Flank(Division d, List<Squad> live, Vector2 center, int side)
    {
        int foe = 1 - d.Team;
        var enemy = centroid[foe];
        if (d.Axis == Vector2.Zero) d.Axis = Toward(center, enemy, d.Team);
        var fwd = d.Axis;
        var right = new Vector2(fwd.Y, -fwd.X) * side;
        float half = 10;
        foreach (var s in Squads)
            if (s.Team == foe && s.Alive > 0) half = MathF.Max(half, Vector2.Dot(s.Center - enemy, right) + s.Radius);
        float apart = MathF.Max(20, Vector2.Dot(enemy - center, fwd));
        var wide = enemy + right * (half + 25) - fwd * apart;
        var beside = enemy + right * (half + 12);
        var close = EnemySquadNear(center, 10, foe);
        if (close != null) d.Phase = 2;
        if (d.Phase == 0 && Vector2.Distance(center, wide) < 12) d.Phase = 1;
        if (d.Phase == 1 && Vector2.Distance(center, beside) < 12) d.Phase = 2;
        if (d.Phase < 2) { Form(d, live, d.Phase == 0 ? wide : beside, d.Phase == 0 ? fwd : -right); return; }
        var target = close ?? EnemySquadNear(beside, float.MaxValue, foe);
        if (target != null) Engage(d, live, target);
    }

    /// Wait behind the line, then commit when a friendly company is breaking, or when a bold sergeant has waited long enough.
    void Reserve(Division d, List<Squad> live, Vector2 dir)
    {
        var line = SquadsOf(d.Team).Where(s => s.Division != d && s.Alive > 0 && s.Company != null).ToList();
        var behind = line.Count > 0 ? line.Aggregate(Vector2.Zero, (a, s) => a + s.Center) / line.Count : centroid[d.Team];
        var breaking = line.FirstOrDefault(s => s.Alive < s.StartCount * 0.5f || s.Pressure > 3);
        float patience = d.Temper switch { Temper.Bold => 45, Temper.Steady => 90, _ => 150 };
        if (breaking != null || Time - d.GoalSince > patience && charged[d.Team])
        {
            var target = EnemySquadNear((breaking ?? live[0]).Center, float.MaxValue, 1 - d.Team);
            if (target != null)
            {
                d.Goal = new Goal(GoalKind.Attack, target.Center, target);
                d.Phase = 1;
                Log(d.Team == 0 ? $"{d.Name} commits the reserve!" : "The enemy commits a reserve!");
                Sound(Cue.Shout, behind);
                Engage(d, live, target);
                return;
            }
        }
        Form(d, live, behind - dir * 25, dir);
    }

    /// Harass: archers keep at three-quarters of their range and shoot, horse archers skirmish, lancers hit soft
    /// companies and ride out again, foot stand off just out of reach as a threat.
    void Harass(Division d, List<Squad> live, Vector2 center, int foe)
    {
        var e = EnemySquadNear(center, float.MaxValue, foe);
        if (e == null) return;
        foreach (var s in live)
        {
            var to = Toward(s.Center, e.Center, d.Team);
            float gap = Vector2.Distance(s.Center, e.Center) - e.Radius;
            var t = s.Units[0].Type;
            if (s.Cavalry) { s.Order = Order.Charge; s.Target = t.Ammo > 0 ? e : SoftTarget(s, foe) ?? e; continue; }
            float keep = t.Ammo > 0 ? t.Range * 0.75f : 18;
            if (t.Ammo > 0) { s.Target = e; s.FireAtWill = true; }
            if (MathF.Abs(gap - keep) > 4) Place(s, e.Center - to * (keep + e.Radius), to);
        }
    }
}
