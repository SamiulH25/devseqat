using Microsoft.Xna.Framework;

namespace Ironfield;

/// The front line a side's posts are laid out from: its middle, the way it faces, and how far its ends reach to each
/// side of the middle (Left is negative) along Right.
public readonly record struct Line(Vector2 Mid, Vector2 Fwd, Vector2 Right, float Left, float RightEnd)
{
    public Vector2 LeftEnd => Mid + Right * Left;
    public Vector2 RightEndPos => Mid + Right * RightEnd;
}

/// Battle: officers at posts (docs/OFFICERS.md OF1). Every look (`Brain`) turns a division's post and stance into its
/// companies' orders. An officer does what his stance says and nothing else: he never charges because a foe came close
/// or commits the reserve on a timer. Men at blows still fight what is in front of them (the companies' own melee).
public partial class Battle
{
    /// How far ahead of the line skirmishers stand (Loose, Harass), and a screen (Screen).
    public const float SkirmishAhead = 22, ScreenAhead = 12;
    /// How far behind the line the reserve waits.
    public const float ReserveBehind = 30;
    /// Loose: go out again when enemy foot is this far from the skirmish spot (when it gives way is `Notice`, OF2).
    public const float LooseReturn = 45;
    /// Guard: how far a wing follows a foe it went to meet before it comes back.
    public const float WingLeash = 35;

    /// The companies that make the side's front line: the Front post's, and the foot the captain leads himself.
    bool OnLine(Squad s, int team) => s.Team == team && s.Alive > 0 && s.FoldedInto == null && s != eGuard && s != Player.Squad && s.Works == null
        && (s.Division is { } d ? d.Post == Post.Front : !s.Cavalry && !Ranged(s));

    /// The side's front line (see Line), facing the way the side deployed. With no line, a 20 m stretch at the side's middle.
    public Line FrontLine(int team)
    {
        // The way the side was drawn up to face (sides deploy along Z), not the way the centroids happen to lie: a company
        // wandering off would swing every post round with it.
        var fwd = new Vector2(0, team == 0 ? 1 : -1);
        var right = new Vector2(fwd.Y, -fwd.X);
        Vector2 sum = Vector2.Zero;
        int n = 0;
        foreach (var s in Squads) if (OnLine(s, team)) { sum += s.Center; n++; }
        if (n == 0) return new Line(centroid[team], fwd, right, -10, 10);
        var mid = sum / n;
        float l = 0, r = 0;
        foreach (var s in Squads)
        {
            if (!OnLine(s, team)) continue;
            float x = Vector2.Dot(s.Center - mid, right);
            l = MathF.Min(l, x - s.Radius);
            r = MathF.Max(r, x + s.Radius);
        }
        return new Line(mid, fwd, right, l, r);
    }

    static Vector2 Mid(List<Squad> live) => live.Aggregate(Vector2.Zero, (a, s) => a + s.Center) / live.Count;

    /// The width a division takes standing in one row, for laying a wing beside the line.
    static float Frontage(List<Squad> live) =>
        live.Sum(s => Math.Clamp(s.Columns, 1, Math.Max(1, s.Alive)) * s.Spacing) + 4 * Math.Max(0, live.Count - 1);

    /// Where a wing stands: beside the line's end on its side, facing the way the line faces.
    Vector2 WingSpot(Line line, int side, List<Squad> live)
    {
        float half = MathF.Min(40, Frontage(live) / 2);
        return side < 0 ? line.LeftEnd - line.Right * (8 + half) : line.RightEndPos + line.Right * (8 + half);
    }

    /// Where this division would stand on these orders (for the line drawn to it in command view); null when the stance
    /// goes in at the enemy (Charge, Swing, Harass).
    public Vector2? SpotOf(Division d, Goal g)
    {
        var live = d.Squads.Where(s => s.Alive > 0).ToList();
        if (live.Count == 0) return null;
        var line = FrontLine(d.Team);
        return g.Stance switch
        {
            Stance.Charge or Stance.Swing or Stance.Harass => null,
            Stance.Hold or Stance.Advance => g == d.Goal && d.Spot is Vector2 at ? at : g.Post == d.Post ? Mid(live) : line.Mid,
            Stance.Guard => WingSpot(line, g.Side, live),
            Stance.Loose => line.Mid + line.Fwd * SkirmishAhead,
            Stance.Screen => line.Mid + line.Fwd * ScreenAhead,
            Stance.Follow => (d.Team == 0 ? Player : EnemyCaptain).Pos,
            _ => line.Mid - line.Fwd * ReserveBehind,
        };
    }

    /// The officer's look: from his post and stance, set each company's order.
    void Brain(Division d)
    {
        var live = d.Squads.Where(s => s.Alive > 0 && !s.Override && !d.Plugs.ContainsKey(s)).ToList();
        int foe = 1 - d.Team;
        var line = FrontLine(d.Team);
        if (d.Stance == Stance.Plug) Plug(d, line, live, foe);
        if (live.Count == 0) return;
        var center = Mid(live);
        switch (d.Stance)
        {
            case Stance.Hold: HoldLine(d, live, line, foe, advance: false); break;
            case Stance.Advance: HoldLine(d, live, line, foe, advance: true); break;
            case Stance.Charge: ChargeIn(d, live, foe); break;
            case Stance.Guard: GuardFlank(d, live, line, center, foe); break;
            case Stance.Swing: Flank(d, live, center, d.Goal.Side == 0 ? 1 : d.Goal.Side); break;
            case Stance.Loose: Loose(d, live, line, center, foe); break;
            case Stance.Harass: Harass(d, live, center, foe); break;
            case Stance.Screen: ScreenLine(d, live, line, center, foe); break;
            case Stance.Follow: FollowCaptain(d, live, center, foe); break;
            default: // Wait, and Plug's companies not sent
                Form(d, live, line.Mid - line.Fwd * ReserveBehind, line.Fwd);
                break;
        }
    }

    /// Front, Hold and Advance: each company keeps its place in the line as it stood when the stance began. Advance walks
    /// the line forward in order and stops it at contact; Hold stands. Shields lock when horse comes near; bows shoot.
    void HoldLine(Division d, List<Squad> live, Line line, int foe, bool advance)
    {
        if (d.Spot == null)
        {
            d.Spot = Mid(live);
            d.Offsets.Clear();
        }
        var spot = d.Spot.Value;
        foreach (var s in live)
            if (!d.Offsets.ContainsKey(s))
                d.Offsets[s] = (s.Order == Order.Hold && Vector2.DistanceSquared(s.Anchor, s.Center) < 144 ? s.Anchor : s.Center) - spot;
        if (advance)
        {
            float gap = float.MaxValue;
            foreach (var s in live)
                if (EnemySquadNear(s.Center, 200, foe) is Squad e) gap = MathF.Min(gap, Vector2.Distance(s.Center, e.Center) - e.Radius - s.Radius);
            if (gap > 3 && gap < float.MaxValue)
            {
                // A walk a few metres ahead of where the line stands now (never further), and a halt at contact. A good
                // officer steps from his rearmost company and keeps them level; a poor one from his foremost, and the
                // line comes on ragged (OF2).
                var pace = live.Where(s => !d.Resting.Contains(s)).Select(s => Vector2.Dot(s.Center - d.Offsets[s] - spot, line.Fwd)).DefaultIfEmpty(0);
                var now = spot + line.Fwd * (LineSkill(d) >= 5 ? pace.Min() : pace.Max());
                var step = now + line.Fwd * MathF.Min(6, gap - 1.5f);
                if (Vector2.Dot(step - spot, line.Fwd) > 0) d.Spot = spot = step;
            }
        }
        Dress(d, live, line);
        RestSpent(d, live, line);
        foreach (var s in live) Place(s, spot + d.Offsets[s] + Drifted(d, s) - (d.Resting.Contains(s) ? line.Fwd * RestBack : Vector2.Zero), line.Fwd);
        Steady(d, live, spot, foe);
    }

    /// Lock shields when horse comes near a standing company; bows on Hold pick the softest target in reach.
    void Steady(Division d, List<Squad> live, Vector2 at, int foe)
    {
        float notice = Notice(d);
        bool horseNear = Squads.Any(s => s.Team == foe && s.Cavalry && s.Alive > 0 && Vector2.Distance(s.Center, at) - s.Radius < notice);
        foreach (var s in live.Where(s => !s.Cavalry && !Ranged(s) && !s.Swarm && s.Units[0].Type.HasShield))
            s.Formation = horseNear && d.Temper != Temper.Bold && s.Order == Order.Hold ? Formation.ShieldWall : Formation.Line;
        foreach (var s in live.Where(Ranged))
            if (s.Order == Order.Hold) { s.Target = SoftTarget(s, foe); s.FireAtWill = true; }
    }

    /// Charge: every company goes in at the enemy company nearest it (horse at the softest it can reach), bows shoot
    /// until the foe is on them. A company keeps its target while that stands. A good officer sends them in together; a
    /// poor one one after another, nearest first (`Stagger` apart, OF2); those not yet sent stand where they are.
    void ChargeIn(Division d, List<Squad> live, int foe)
    {
        if (d.Sequence.Count == 0)
            d.Sequence.AddRange(live.OrderBy(s => EnemySquadNear(s.Center, float.MaxValue, foe) is Squad e ? Vector2.Distance(s.Center, e.Center) : 0).ThenBy(s => Squads.IndexOf(s)));
        foreach (var s in live)
        {
            int turn = d.Sequence.IndexOf(s);
            if (turn > 0 && Time < d.GoalSince + turn * Stagger(d))
            {
                if (s.Order != Order.Hold) Place(s, s.Center, Toward(s.Center, centroid[foe], d.Team));
                continue;
            }
            if (s.Order == Order.Charge && s.Target is { Alive: > 0 } && !Ranged(s)) continue;
            var t = EnemySquadNear(s.Center, float.MaxValue, foe);
            if (t == null) continue;
            if (Ranged(s)) { Engage(d, new List<Squad> { s }, t); continue; }
            s.Order = Order.Charge;
            s.Target = s.Cavalry ? SoftTarget(s, foe) ?? t : t;
            s.Pursue = false;
        }
    }

    /// Wing, Guard: stand beside the line's end and meet what comes round it; come back when the fight pulls too far.
    void GuardFlank(Division d, List<Squad> live, Line line, Vector2 center, int foe)
    {
        int side = d.Goal.Side == 0 ? 1 : d.Goal.Side;
        var spot = WingSpot(line, side, live);
        var end = side < 0 ? line.LeftEnd : line.RightEndPos;
        float reach = Notice(d) * 0.7f; // a sharp officer sees them coming round the end sooner (OF2)
        var threat = EnemySquadNear(spot, reach, foe) ?? EnemySquadNear(end, reach * 0.6f, foe);
        if (threat != null && Vector2.Distance(center, spot) < WingLeash) Engage(d, live, threat);
        else Form(d, live, spot, line.Fwd);
        Steady(d, live, spot, foe);
    }

    /// Skirmish, Loose: stand ahead of the line and shoot; when enemy foot comes near, give way behind the line, and go
    /// out again once it has gone.
    void Loose(Division d, List<Squad> live, Line line, Vector2 center, int foe)
    {
        var spot = line.Mid + line.Fwd * SkirmishAhead;
        float near = FootGap(center, foe);
        if (d.Phase == 0 && near < 10 + Notice(d) * 0.4f) d.Phase = 1; // a poor officer sees the foot late (OF2)
        else if (d.Phase == 1 && FootGap(spot, foe) > LooseReturn) d.Phase = 0;
        Form(d, live, d.Phase == 0 ? spot : line.Mid - line.Fwd * 12, line.Fwd);
        Shoot(live, foe);
    }

    /// Skirmish, Screen: stand close in front of the line; when the enemy closes, give way round the nearer end.
    void ScreenLine(Division d, List<Squad> live, Line line, Vector2 center, int foe)
    {
        var spot = line.Mid + line.Fwd * ScreenAhead;
        var e = EnemySquadNear(center, float.MaxValue, foe);
        float gap = e != null ? Vector2.Distance(center, e.Center) - e.Radius : float.MaxValue;
        if (d.Phase == 0 && gap < 18) d.Phase = 1;
        else if (d.Phase == 1 && (EnemySquadNear(spot, LooseReturn, foe) == null)) d.Phase = 0;
        if (d.Phase == 0) Form(d, live, spot, line.Fwd);
        else
        {
            bool right = Vector2.Dot(center - line.Mid, line.Right) >= 0;
            Form(d, live, right ? line.RightEndPos + line.Right * 12 : line.LeftEnd - line.Right * 12, line.Fwd);
        }
        Shoot(live, foe);
    }

    /// The gap from p to the nearest enemy foot company's edge (not bows, not horse).
    float FootGap(Vector2 p, int foe)
    {
        float best = float.MaxValue;
        foreach (var s in Squads)
            if (s.Team == foe && s.Alive > 0 && !s.Cavalry && !Ranged(s) && s != eGuard)
                best = MathF.Min(best, Vector2.Distance(s.Center, p) - s.Radius);
        return best;
    }

    void Shoot(List<Squad> live, int foe)
    {
        foreach (var s in live.Where(Ranged)) { s.Target = SoftTarget(s, foe); s.FireAtWill = true; }
    }

    /// Reserve, Follow: stay by the captain, between him and the foe, and meet what comes for him.
    void FollowCaptain(Division d, List<Squad> live, Vector2 center, int foe)
    {
        var general = d.Team == 0 ? Player : EnemyCaptain;
        var dir = Toward(center, centroid[foe], d.Team);
        if (!general.Active) { Form(d, live, center, dir); return; }
        var threat = EnemySquadNear(general.Pos, 14, foe);
        if (threat != null) Engage(d, live, threat);
        else Form(d, live, general.Pos + Toward(general.Pos, centroid[foe], d.Team) * 7, Toward(general.Pos, centroid[foe], d.Team));
    }

    /// Reserve, Plug: when a company of the line breaks (half gone, pressed hard, or running), send the nearest reserve
    /// company still waiting in at the foe it faced. Sent companies leave `live`; the rest wait behind the line.
    void Plug(Division d, Line line, List<Squad> live, int foe)
    {
        foreach (var (sent, at) in d.Plugs)
            if (sent.Alive > 0 && !sent.Override && sent.Target is not { Alive: > 0 })
                if (EnemySquadNear(at.Center, float.MaxValue, foe) is Squad t) { sent.Order = Order.Charge; sent.Target = t; }
        foreach (var s in Squads)
        {
            if (!OnLine(s, d.Team) || d.Plugs.ContainsValue(s)) continue;
            bool breaking = s.Alive < s.StartCount * 0.5f || s.Pressure > 3 || s.Units.Count(u => u.Active && u.Routing) * 2 > s.Alive;
            if (!breaking) continue;
            var fresh = live.Where(q => !Ranged(q)).OrderBy(q => Vector2.DistanceSquared(q.Center, s.Center)).FirstOrDefault();
            var foeAt = EnemySquadNear(s.Center, float.MaxValue, foe);
            if (fresh == null || foeAt == null) return;
            d.Plugs[fresh] = s;
            live.Remove(fresh);
            fresh.Order = Order.Charge;
            fresh.Target = foeAt;
            Log(d.Team == 0 ? $"{d.Name} sends the {fresh.Name} to plug the line." : "The enemy's reserve plugs a gap in his line.");
            Sound(Cue.Shout, fresh.Center);
        }
    }

    /// The general's stances for his divisions (the AI's, and yours when the AI plays you). Front lines hold, then charge
    /// once the lines close, the fight starts, or they have waited long enough; wings guard, then swing round; the reserve
    /// plugs. An officer never changes his own stance: this is the general's word.
    void AiCommand(int team)
    {
        int foe = 1 - team;
        bool fighting = SquadsOf(team).Any(s => s.Alive > 0 && s.Division == null && s.Units.Any(u => u.Active && u.EnemyDist < 5));
        foreach (var d in DivisionsOf(team))
        {
            if (d.Pending != null || d.Alive == 0) continue;
            var live = d.Squads.Where(s => s.Alive > 0).ToList();
            var at = Mid(live);
            var near = EnemySquadNear(at, float.MaxValue, foe);
            if (near == null) continue;
            float gap = Vector2.Distance(at, near.Center) - near.Radius;
            float wait = d.Temper switch { Temper.Bold => 15, Temper.Steady => 30, _ => 45 } * (d.Has("Hothead") ? 0.5f : 1);
            bool waited = Time - d.GoalSince > wait;
            Stance? next = d.Stance switch
            {
                Stance.Hold or Stance.Advance when fighting || gap < 30 || waited => Stance.Charge,
                Stance.Guard when fighting || waited => Stance.Swing,
                Stance.Wait => Stance.Plug,
                _ => null,
            };
            if (next is { } st) IssueGoal(d, Ironfield.Goal.Of(d.Post, st), ai: true);
        }
    }
}
