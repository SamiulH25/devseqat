using Microsoft.Xna.Framework;

namespace Ironfield;

/// A question a sergeant puts to you (docs/SERGEANTS.md SG3): two answers, each a goal. Unanswered by Until, he takes
/// his own choice (A when AByDefault).
public sealed record Query(string Text, string LabelA, Goal A, string LabelB, Goal B, bool AByDefault, float Until);

/// Battle: the command tray, horn plans, sergeants' reports and questions, wits, voice and nature (docs/SERGEANTS.md).
public partial class Battle
{
    // ---------- SG1: who leads whom, and the reserve ----------

    /// Seconds a rider takes to fetch a company from the reserve once the battle has begun.
    public const float CallUpDelay = 8;
    /// Call-ups on their way: the reserve company and the division it joins (null: the captain's own).
    readonly List<(float At, int Team, Company C, string Role, Division? Into)> callUps = new();

    /// The companies the captain leads himself that still stand.
    public int LedCount(int team) => Ironfield.Armies.CommandCap - OpenSlots(team);
    /// Free places under the captain, less the call-ups already riding to him.
    int FreeSlots(int team) => OpenSlots(team) - callUps.Count(x => x.Team == team && x.Into == null);
    /// The companies waiting off the field (Held: kept back by the command cap rather than the field's size).
    public IReadOnlyList<(Company C, string Role, bool Held)> ReservesOf(int team) => reserves[team];
    /// Reserve companies a rider has gone back for.
    public IEnumerable<(Company C, float Eta)> CallUpsOf(int team) => callUps.Where(x => x.Team == team).Select(x => (x.C, x.At - Time));

    int Under(Division d) => d.Squads.Count(s => s.Alive > 0 || Time <= 0) + callUps.Count(x => x.Into == d);

    /// Why reserve company i of `team` cannot be called up (to `into`, or to the captain when null); null if it can.
    public string? CannotCallUp(int team, int i, Division? into)
    {
        if (i < 0 || i >= reserves[team].Count) return "No such company in the reserve.";
        if (into == null) return FreeSlots(team) > 0 ? null : $"You already lead {Ironfield.Armies.CommandCap} companies. Call it up to a sergeant.";
        if (into.Team != team) return "Not your sergeant.";
        if (into.Squads.Count > 0 && into.Alive == 0 && Time > 0) return $"{into.Name} has no men left.";
        return Under(into) >= Ironfield.Armies.DivisionCap ? $"{into.Name} already leads {Ironfield.Armies.DivisionCap} companies." : null;
    }

    /// Call reserve company i up: before the battle it stands on the field at once; during it a rider goes back for it.
    public string? CallUp(int team, int i, Division? into)
    {
        if (CannotCallUp(team, i, into) is string why) return why;
        var (c, role, _) = reserves[team][i];
        reserves[team].RemoveAt(i);
        int div = into == null ? -1 : Divisions.IndexOf(into);
        if (Time <= 0)
        {
            if (Recording != null) (Recording.Setup ??= new()).Add(new[] { 1, team, i, div });
            MarchOn(team, c, role, into, auto: false);
            return null;
        }
        if (Recording != null && team == 0) (callUpRecords ??= new()).Add(new[] { team, i, div });
        callUps.Add((Time + CallUpDelay, team, c, role, into));
        if (team == 0) Log($"A rider goes back for the {Troops.Get(c.Troop).Name} ({CallUpDelay:0}s).");
        return null;
    }
    List<int[]>? callUpRecords;

    void UpdateCallUps()
    {
        for (int i = 0; i < callUps.Count; i++)
        {
            if (Time < callUps[i].At) continue;
            var (_, team, c, role, into) = callUps[i];
            callUps.RemoveAt(i--);
            MarchOn(team, c, role, into is { } d && (d.Alive > 0 || d.Squads.Count == 0) ? d : null, auto: false);
            if (team == 0) Log($"Called up: {Troops.Get(c.Troop).Name} {(into != null ? $"join {into.Name}" : "march on")}.");
        }
    }

    /// A reserve company takes the field from behind its side's lines. auto: it joins the division it was given in the
    /// army (a reinforcement); else it goes where it was called (into, or the captain's own when null).
    Squad MarchOn(int team, Company c, string role, Division? into, bool auto)
    {
        float f = team == 0 ? 1 : -1;
        var t = Troops.Get(c.Troop);
        var s = AddSquad(team, t, c.Count, new Vector2(lateral.Count % 3 * 12 - 12, -rearLine[team]) * f, ColumnsFor(c.Count), new Vector2(0, f), c);
        s.Anchor = new Vector2(0, -30 - Back) * f;
        Role(team, role).Add(s);
        lateral[s] = 0;
        if (auto) into = Divisions.FirstOrDefault(d => d.Team == team && d.SpecId >= 0 && d.SpecId == c.Division);
        if (into != null)
        {
            into.Squads.Add(s);
            s.Division = into;
            if (into.Leader is not { Active: true } && (s.Units.FirstOrDefault(u => u.Hero) ?? (into.Squads.Count == 1 ? PickLeader(into) : null)) is Unit h) { into.Leader = h; h.Sergeant = true; }
        }
        if (team == 1 && charged[1] || team == 0 && Options.AutoPlayer) s.Order = Order.Charge;
        return s;
    }

    /// Why company s cannot move to `to` (a sergeant, or the captain when null); null if it can. Before the battle only.
    public string? CannotAssign(Squad s, Division? to)
    {
        if (Time > 0) return "Companies change hands only before the battle.";
        if (s.Company is not { Allied: false }) return "Not yours to give.";
        if (s.Division == to) return to == null ? "You lead them already." : $"{to.Name} leads them already.";
        if (to == null) return FreeSlots(s.Team) > 0 ? null : $"You already lead {Ironfield.Armies.CommandCap} companies.";
        if (to.Team != s.Team) return "Not your sergeant.";
        return Under(to) >= Ironfield.Armies.DivisionCap ? $"{to.Name} already leads {Ironfield.Armies.DivisionCap} companies." : null;
    }

    /// Move a company to a sergeant (or to the captain when null) before the battle; it takes his drill.
    public string? Assign(Squad s, Division? to)
    {
        if (CannotAssign(s, to) is string why) return why;
        if (Recording != null) (Recording.Setup ??= new()).Add(new[] { 0, Squads.IndexOf(s), to == null ? -1 : Divisions.IndexOf(to) });
        if (s.Division is { } from)
        {
            from.Squads.Remove(s);
            if (!s.Swarm) s.Drill = MathF.Max(0, s.Drill - from.Skill * (from.Has("Drillmaster") ? 0.02f : 0.01f));
            if (from.Leader != null && s.Units.Contains(from.Leader))
            {
                from.Leader.Sergeant = false;
                from.Leader = from.Squads.Count > 0 ? PickLeader(from) : null;
                if (from.Leader != null) from.Leader.Sergeant = true;
            }
        }
        s.Division = to;
        if (to != null)
        {
            to.Squads.Add(s);
            if (!s.Swarm) s.Drill = MathF.Min(1, s.Drill + to.Skill * (to.Has("Drillmaster") ? 0.02f : 0.01f));
            if (to.Leader is not { Active: true }) { to.Leader = PickLeader(to); if (to.Leader != null) to.Leader.Sergeant = true; }
        }
        s.Override = false;
        return null;
    }

    // ---------- SG2: plans and horns ----------

    /// Give a division a horn plan: before the battle at once, during it by rider like any order. Returns the delay.
    public float SetHornPlan(Division d, Goal g, bool now = false)
    {
        float delay = now || Time <= 0 ? 0 : OrderDelay(d);
        if (Recording != null && d.Team == 0)
            (goalRecords ??= new()).Add(new Replay.GoalOrder { Division = d.Id, Kind = g.Kind, Point = g.Point, Target = g.Target != null ? Squads.IndexOf(g.Target) : -1, Side = g.Side, Plan = true });
        if (delay <= 0) { d.Horn = g; d.PendingHorn = null; }
        else
        {
            d.PendingHorn = g;
            d.HornAt = Time + delay;
            SendCourier(d, d.HornAt, false);
        }
        if (d.Team == 0) Log(delay <= 0 ? $"{d.Name} will {g.Describe} on the horn." : $"A plan rides to {d.Name}: on the horn, {g.Describe} ({delay:0}s).");
        return delay;
    }

    /// Sound the horn: every division of the side with a plan takes it now (no rider). Returns how many moved.
    public int SoundHorn(int team)
    {
        if (team == 0 && Recording != null) hornRecorded = true;
        int n = 0;
        foreach (var d in DivisionsOf(team))
        {
            if (d.Horn == null || d.Alive == 0) continue;
            d.Pending = d.Horn;
            d.PendingAt = Time + 0.5f;
            d.Horn = null;
            Couriers.RemoveAll(c => c.Div == d && c.Goal);
            n++;
        }
        Sound(Cue.HornCharge, GeneralPos(team));
        if (team == 0) Log(n > 0 ? $"The horn! {n} division{(n == 1 ? "" : "s")} move{(n == 1 ? "s" : "")} on the plan." : "The horn sounds, but no sergeant has a plan.");
        return n;
    }
    bool hornRecorded;

    /// Horn plans still on the road land when their rider arrives.
    void UpdateHornPlans(Division d)
    {
        if (d.PendingHorn == null || Time < d.HornAt) return;
        d.Horn = d.PendingHorn;
        d.PendingHorn = null;
        if (d.Team == 0) Log($"{d.Name}: \"On the horn, {d.Horn.Describe}.\"");
    }

    // ---------- SG3: reports and questions ----------

    /// How long a question waits for your answer, and how long before he asks again.
    public const float QueryWait = 12, QueryEvery = 25;

    /// What the division is doing, in a few words for its card.
    public string Report(Division d)
    {
        if (d.Squads.Count == 0) return "NO COMPANIES";
        if (d.Alive == 0) return "BROKEN";
        if (d.Leaderless) return "SERGEANT DOWN";
        if (d.Query != null) return "ASKS FOR ORDERS";
        if (d.Squads.Any(s => s.Alive > 0 && s.InContact)) return "ENGAGED";
        if (d.Losing * 12 >= Math.Max(12, d.Alive)) return "UNDER ARROWS";
        if (d.Horn != null && d.Goal.Kind is GoalKind.Hold or GoalKind.Reserve or GoalKind.Ambush) return "AWAITS THE HORN";
        var live = d.Squads.Where(s => s.Alive > 0).ToList();
        if (live.Count > 0 && live.Average(s => Vector2.Distance(s.Center, s.Anchor)) > 8) return "ON THE MOVE";
        return d.Goal.Kind switch { GoalKind.Hold or GoalKind.Take => "HOLDING", GoalKind.Reserve => "IN RESERVE", GoalKind.Ambush => "LYING IN WAIT", _ => "ADVANCING" };
    }

    /// Every 5 s: how many of the division fell since the last look (for UNDER ARROWS and the arrows question).
    void CountLosses(Division d)
    {
        if (Time < d.LossCheck) return;
        d.LossCheck = Time + 5;
        int alive = d.Alive;
        d.Losing = d.AliveThen < 0 ? 0 : Math.Max(0, d.AliveThen - alive);
        d.AliveThen = alive;
    }

    /// Answer a question: A or B rides out to him as a goal.
    public string? Answer(Division d, bool a)
    {
        if (d.Query is not { } q) return "He has asked nothing.";
        d.Query = null;
        d.QueryAfter = Time + QueryEvery;
        IssueGoal(d, a ? q.A : q.B);
        return null;
    }

    /// He asks when something needs your word (yours only, and not when the AI plays you); unanswered, he decides.
    void Questions(Division d, List<Squad> live, Vector2 center, Vector2 dir)
    {
        if (d.Query is { } q)
        {
            if (Time < q.Until) return;
            d.Query = null;
            d.QueryAfter = Time + QueryEvery;
            var pick = q.AByDefault ? q.A : q.B;
            d.Pending = pick;
            d.PendingAt = Time; // he is there: no rider
            Log($"{d.Name} decides: {(q.AByDefault ? q.LabelA : q.LabelB).ToLowerInvariant()}.");
            return;
        }
        if (d.Team != 0 || Options.AutoPlayer || Time < d.QueryAfter || d.Pending != null || d.Leader is not { Active: true }) return;
        int foe = 1 - d.Team;
        bool bold = d.Temper == Temper.Bold || d.Has("Hothead"), shy = d.Temper == Temper.Cautious && !d.Has("Stubborn");
        var back = new Goal(GoalKind.FallBack, center - dir * 25);
        var stay = new Goal(GoalKind.Hold, center);
        Query? ask = null;
        // Their horse comes at our flank.
        if (!live.All(s => s.Cavalry) && d.Goal.Kind != GoalKind.FallBack
            && Squads.FirstOrDefault(s => s.Team == foe && s.Cavalry && s.Alive > 0 && s.Order == Order.Charge && Vector2.Distance(s.Center, center) < 45
                && Vector2.Dot(Vector2.Normalize(s.Center - center + new Vector2(1e-3f, 0)), Facing(live, dir)) < 0.6f) is Squad horse)
            ask = new Query("Their horse comes at our flank!", "STAND FAST", stay, "FALL BACK", back, !shy, Time + QueryWait);
        // We are taking arrows and nobody is close enough to hit back.
        else if (d.Goal.Kind is GoalKind.Hold or GoalKind.Take or GoalKind.Reserve && d.Losing * 12 >= Math.Max(12, d.Alive) && EnemySquadNear(center, 20, foe) == null
            && Squads.Where(s => s.Team == foe && s.Alive > 0 && Ranged(s)).OrderBy(s => Vector2.DistanceSquared(s.Center, center)).FirstOrDefault() is Squad bows)
            ask = new Query($"We are taking arrows from the {bows.Name}!", "GO AT THEM", new Goal(GoalKind.Attack, Target: bows), "PULL BACK", back, !shy, Time + QueryWait);
        // The company we were sent at breaks.
        else if (d.Goal.Kind == GoalKind.Attack && d.Goal.Target is { } t && t.Alive == 0 && t.Units.Any(u => u.Active && u.Routing))
            ask = new Query($"The {t.Name} break and run!", "RUN THEM DOWN", new Goal(GoalKind.Pursue), "RE-FORM", stay, bold, Time + QueryWait);
        if (ask == null) return;
        d.Query = ask;
        Log($"{d.Name}: \"{ask.Text}\" ({ask.LabelA} / {ask.LabelB})");
        Sound(Cue.Shout, center);
    }

    /// Which way the division faces: its companies' fronts together (the way to the enemy if they cancel out).
    static Vector2 Facing(List<Squad> live, Vector2 dir)
    {
        var f = live.Aggregate(Vector2.Zero, (a, s) => a + s.Front);
        return f.LengthSquared() > 0.01f ? Vector2.Normalize(f) : dir;
    }

    // ---------- SG4: a sergeant lifts his companies' wits ----------

    /// The grade of thinking a sergeant of this skill brings (the companies' fighting drill stays their own).
    public static Grade SergeantGrade(int skill) => skill >= 8 ? Grade.Elite : skill >= 5 ? Grade.Veteran : skill >= 3 ? Grade.Trained : Grade.Rabble;

    /// The grade a company thinks at: its own, or its living sergeant's if better.
    public static Grade WitsGrade(Squad s) => s.Division is { Leader.Active: true } d && !s.Swarm && SergeantGrade(d.Skill) > s.Grade ? SergeantGrade(d.Skill) : s.Grade;

    // ---------- SG5: command by being near ----------

    /// Within this of his sergeant the captain's word is shouted, not ridden.
    public const float VoiceReach = 25;
    /// Can the general shout to this division's sergeant?
    public bool InVoice(Division d) => GeneralActive(d.Team) && Vector2.Distance(GeneralPos(d.Team), CourierGoal(d)) <= VoiceReach;
    bool GeneralActive(int team) => (team == 0 ? Player : EnemyCaptain).Active;

    // ---------- SG7: his name ----------

    /// The sergeant's name when he has one: a named man of the roll, or a companion; else empty.
    public static string SergeantName(Division d)
    {
        if (d.Leader is not { } l) return "";
        if (l.SoldierId != 0 && l.Squad?.Company?.Named?.FirstOrDefault(m => m.Id == l.SoldierId) is NamedSpec m) return m.Name;
        return l.Hero && l.Squad?.Company is { Hero.Length: > 0 } c ? c.Hero : "";
    }
}
