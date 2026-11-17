using Microsoft.Xna.Framework;

namespace Ironfield;

/// Where an officer stands his division in the army (docs/OFFICERS.md OF1). Where a post lies is worked out from the army
/// (`Battle.FrontLine`), never set by hand: wings beside the front line's ends, skirmishers ahead of it, the reserve behind.
public enum Post { Front, LeftWing, RightWing, Skirmish, Reserve }

/// What an officer does at his post. Each post has three (`Goal.StancesOf`); he never changes it himself.
public enum Stance { Hold, Advance, Charge, Guard, Swing, Loose, Harass, Screen, Wait, Plug, Follow }

/// How a sergeant fights: Bold charges early and chases far; Steady keeps formation and waits for contact;
/// Cautious keeps a short leash and lets the enemy come to him.
public enum Temper { Bold, Steady, Cautious }

/// An officer's orders: his post and the stance he keeps there (also a horn plan, kept until the horn).
public sealed record Goal(Post Post, Stance Stance)
{
    static readonly Stance[] FrontStances = { Stance.Hold, Stance.Advance, Stance.Charge },
        WingStances = { Stance.Guard, Stance.Swing, Stance.Charge },
        SkirmishStances = { Stance.Loose, Stance.Harass, Stance.Screen },
        ReserveStances = { Stance.Wait, Stance.Plug, Stance.Follow };

    /// The stances an officer at this post can keep, the first his default.
    public static Stance[] StancesOf(Post p) => p switch
    {
        Post.Front => FrontStances,
        Post.LeftWing or Post.RightWing => WingStances,
        Post.Skirmish => SkirmishStances,
        _ => ReserveStances,
    };

    public static bool Fits(Post p, Stance s) => Array.IndexOf(StancesOf(p), s) >= 0;

    /// A post with this stance if it fits there, else the post's default.
    public static Goal Of(Post p, Stance? s = null) => new(p, s is { } x && Fits(p, x) ? x : StancesOf(p)[0]);

    /// +1 on the right wing, -1 on the left, 0 elsewhere.
    public int Side => Post == Post.RightWing ? 1 : Post == Post.LeftWing ? -1 : 0;

    public string Describe => Stance switch
    {
        Stance.Hold => "hold the line",
        Stance.Advance => "advance in order",
        Stance.Charge => "charge",
        Stance.Guard => Side < 0 ? "guard the left flank" : "guard the right flank",
        Stance.Swing => Side < 0 ? "swing round their right" : "swing round their left",
        Stance.Loose => "loose, and give way to their foot",
        Stance.Harass => "harass them",
        Stance.Screen => "screen the line",
        Stance.Wait => "wait in reserve",
        Stance.Plug => "plug the line where it breaks",
        _ => "follow the captain",
    };

    public static string Label(Post p) => p switch { Post.LeftWing => "LEFT WING", Post.RightWing => "RIGHT WING", _ => p.ToString().ToUpperInvariant() };
    public static string Label(Stance s) => s.ToString().ToUpperInvariant();
    /// "LEFT WING: GUARD", for cards and tags.
    public string Tag => $"{Label(Post)}: {Label(Stance)}";

    /// A goal from its saved name: "Front:Hold" (a post and a stance), or an old goal name mapped to the nearest post.
    public static Goal FromName(string name)
    {
        var parts = name.Split(':');
        if (parts.Length == 2 && Enum.TryParse<Post>(parts[0], out var p))
            return Of(p, Enum.TryParse<Stance>(parts[1], out var st) ? st : null);
        return name switch
        {
            "Attack" or "Pursue" => new(Post.Front, Stance.Charge),
            "Take" => new(Post.Front, Stance.Advance),
            "FlankLeft" => new(Post.LeftWing, Stance.Swing),
            "FlankRight" or "Flank" => new(Post.RightWing, Stance.Swing),
            "Reserve" => new(Post.Reserve, Stance.Plug),
            "FallBack" => new(Post.Reserve, Stance.Wait),
            "Guard" => new(Post.Reserve, Stance.Follow),
            "Screen" => new(Post.Skirmish, Stance.Screen),
            "Harass" => new(Post.Skirmish, Stance.Harass),
            _ => new(Post.Front, Stance.Hold), // Hold, Ambush, anything unknown
        };
    }

    /// The name FromName reads back.
    public string Name => $"{Post}:{Stance}";
}

/// A division: a few companies under an officer at a post, who keeps the stance he was given (docs/OFFICERS.md).
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
    /// Step within the stance (Swing: 0 swing wide, 1 come alongside, 2 hit them; Loose and Screen: 1 while given way).
    public int Phase;
    /// Swing: the direction toward the enemy when the stance began, so the swing does not chase its own tail.
    public Vector2 Axis;
    /// Front: where the line stood when the stance began (null: take it at the next look), and each company's place
    /// from it, so Hold and Advance keep the line as you drew it up.
    public Vector2? Spot;
    public readonly Dictionary<Squad, Vector2> Offsets = new();
    /// Plug: reserve companies sent in, and the line company each went to help.
    public readonly Dictionary<Squad, Squad> Plugs = new();
    /// OF2: how each company has wandered from its place under a poor officer; the order a poor officer sends his companies
    /// in on Charge; the spent companies a good one has pulled back to rest.
    public readonly Dictionary<Squad, Vector2> Drift = new();
    public readonly List<Squad> Sequence = new();
    public readonly HashSet<Squad> Resting = new();
    public Post Post => Goal.Post;
    public Stance Stance => Goal.Stance;

    public Division(int team, int id, string name, Temper temper, int skill, Goal goal)
    {
        Team = team; Id = id; Name = name; Temper = temper; Skill = Math.Clamp(skill, 0, 10); Goal = goal;
    }

    public int Alive => Squads.Sum(s => s.Alive);
    public int Start => Squads.Sum(s => s.StartCount);
    public bool Leaderless => LeaderlessSince >= 0;

    /// The horn plan (docs/SERGEANTS.md SG2): a goal held back until you sound the horn. PendingHorn is one still riding out.
    public Goal? Horn, PendingHorn;
    public float HornAt;
    /// A question he has put to you (SG3), and when he may ask again.
    /// Men standing at the last loss check, and how many fell since the one before (SG3: under arrows).
    public int AliveThen = -1, Losing;
    public float LossCheck;
    /// His nature (SG7): officer traits of his company, or the spec's.
    public string[] Traits = Array.Empty<string>();
    public bool Has(string trait) => Array.IndexOf(Traits, trait) >= 0;
}

/// Battle: divisions (docs/archive/COMBAT_OVERHAUL.md 1.3-1.4, docs/OFFICERS.md). Each officer looks again every half second or
/// so (quicker with skill) and sets his companies' orders from his post and stance (BattlePosts.cs); the unit AI does the
/// rest. Orders land at once while `Riders` is off.
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
        var d = new Division(team, Divisions.Count, name, temper, skill, goal ?? new Goal(Post.Front, Stance.Hold));
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
            var lead = squads.FirstOrDefault(s => spec.LeadCompany != 0 && s.Company?.Id == spec.LeadCompany) ?? squads.FirstOrDefault(s => s.Units.Contains(hero!));
            var traits = spec.Traits ?? lead?.Company?.Traits ?? Array.Empty<string>();
            var d = AddDivision(team, spec.Name, squads, temper, spec.Skill + (traits.Contains("Veteran") ? 1 : 0), null, hero);
            d.SpecId = spec.Id;
            d.Traits = traits;
            if (d.Has("Drillmaster")) foreach (var s in squads) if (!s.Swarm) s.Drill = MathF.Min(1, s.Drill + d.Skill * 0.01f); // he drills them twice as hard
            d.Goal = Ironfield.Goal.FromName(spec.Goal);
        }
    }

    /// Riders carry orders (off for now: docs/OFFICERS.md OF0, every order lands at once).
    public const bool Riders = false;

    /// Seconds an order takes to reach the sergeant: 0 while `Riders` is off. With riders: a base, the ride from the
    /// general, longer if he is fighting or dead (his men wait for someone to take charge), shorter with his skill and with
    /// the Horns perk.
    public float OrderDelay(Division d)
    {
#pragma warning disable CS0162 // unreachable while Riders is off
        if (!Riders) return 0;
        var from = GeneralPos(d.Team);
        var to = CourierGoal(d);
        if (InVoice(d)) return 0.6f; // near enough to shout (docs/SERGEANTS.md SG5)
        float t = 1.5f + Vector2.Distance(from, to) / CourierSpeed;
        if ((d.Team == 0 ? Player : EnemyCaptain) is { Active: true, EnemyDist: < 4 }) t += 3; // you are fighting: the rider waits for your word
        if (d.Leader is { Active: true, EnemyDist: < 5 }) t += 2;
        if (d.Leader is not { Active: true }) t *= 2;
        t *= 1 - 0.05f * d.Skill;
        if (d.Team == 0 && Options.Horns) t *= 0.7f;
        if (d.Team == 0 && Perk("MasterOfWar")) t *= 0.5f;
        return t;
#pragma warning restore CS0162
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
            (goalRecords ??= new()).Add(new Replay.GoalOrder { Division = d.Id, Post = g.Post, Stance = g.Stance });
        if (d.Team == 0 && delay > 0) Log(countermand ? $"New orders ride to {d.Name}: {g.Describe} ({delay:0}s)." : $"Orders ride to {d.Name}: {g.Describe} ({delay:0}s).");
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
        UpdateCallUps();
        if (Time >= nextAiCommand && Divisions.Count > 0)
        {
            nextAiCommand = Time + 4;
            AiCommand(1);
            if (Options.AutoPlayer) AiCommand(0);
        }
        foreach (var d in Divisions)
        {
            if (d.Squads.Count == 0) continue; // emptied in deployment: a card waiting for companies
            UpdateHornPlans(d);
            CountLosses(d);
            if (d.Pending != null && Time >= d.PendingAt)
            {
                d.Goal = d.Pending;
                d.Pending = null;
                d.GoalSince = Time;
                d.Phase = 0;
                d.Axis = Vector2.Zero;
                d.Spot = null;
                d.Offsets.Clear();
                d.Plugs.Clear();
                d.Sequence.Clear();
                d.Resting.Clear();
                d.NextThink = Time;
                foreach (var s in d.Squads) { s.Override = false; s.Pursue = false; }
                if (d.Team == 0) Log($"{d.Name}: \"{Capital(d.Goal.Describe)}!\"");
            }
            if (Time < d.NextThink) continue;
            d.NextThink = Time + Reaction(d); // a sharp officer looks again sooner (OF2)
            if (!LeaderCheck(d) || d.Alive == 0) continue;
            Brain(d);
        }
    }

    float nextAiCommand = 4;

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
