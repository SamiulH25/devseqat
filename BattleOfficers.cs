using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: an officer's skill decides how well he keeps his stance (docs/archive/OFFICERS.md OF2), for both sides alike. Skill is
/// 0-10 (a companion's Command, a sergeant's battles, an enemy lord's army quality). It changes how often he looks again,
/// how far out he sees a threat, how well he keeps his line, whether his companies go in together, and whether he rests
/// a spent company. His nature changes how well, not what: Hothead goes in quicker, Stubborn holds longer, Drillmaster keeps
/// the line tighter. A poor officer on the enemy's line is something to see and use.
public partial class Battle
{
    /// How far behind its place a spent company is pulled back to rest.
    public const float RestBack = 15;

    /// Seconds between his looks: about 2 s at skill 0, 0.4 s at 10 (25% faster under a Companion or banner sergeant, PF3).
    public static float Reaction(Division d) => (2f - 0.16f * d.Skill) * (d.Distinguished && d.Leader is { Active: true } ? 0.75f : 1);

    /// How far out he sees horse coming or a foe rounding the line's end: 15 m at skill 0, 55 m at 10.
    public static float Notice(Division d) => 15 + 4 * d.Skill;

    /// His skill at keeping a line: his skill, three better for a Drillmaster.
    public static int LineSkill(Division d) => Math.Min(10, d.Skill + (d.Has("Drillmaster") ? 3 : 0));

    /// Seconds between his companies going in on Charge: none at skill 10, a tenth of one at 9, 2 s at 5, 8 s at 0; half for
    /// a Hothead.
    public static float Stagger(Division d) => (10 - d.Skill) * (10 - d.Skill) * 0.08f * (d.Has("Hothead") ? 0.5f : 1);

    /// How far a company under this officer has wandered from its place (none past line skill 5).
    Vector2 Drifted(Division d, Squad s) => d.Drift.TryGetValue(s, out var v) ? v : Vector2.Zero;

    /// Each look on the line. A poor officer (line skill under 5) lets his companies wander a little from their places, so
    /// gaps open and close by chance. A good one closes gaps in his front rank: a company whose neighbour fell or was pulled
    /// back steps in toward the middle, shoulder to shoulder (3 m apart).
    void Dress(Division d, List<Squad> live, Line line)
    {
        int skill = LineSkill(d);
        if (skill < 5)
        {
            float step = (5 - skill) * 0.3f, cap = (5 - skill) * 1.6f;
            foreach (var s in live)
            {
                var v = Drifted(d, s) + line.Right * (rng.NextSingle() * 2 - 1) * step;
                if (v.Length() > cap) v = Vector2.Normalize(v) * cap;
                d.Drift[s] = v;
            }
            return;
        }
        d.Drift.Clear();
        // The front rank: companies within 6 m of the most forward place, and not resting.
        var placed = live.Where(s => !d.Resting.Contains(s) && d.Offsets.ContainsKey(s)).ToList();
        if (placed.Count < 2) return;
        float front = placed.Max(s => Vector2.Dot(d.Offsets[s], line.Fwd));
        var rank = placed.Where(s => Vector2.Dot(d.Offsets[s], line.Fwd) > front - 6).OrderBy(s => Vector2.Dot(d.Offsets[s], line.Right)).ToList();
        if (rank.Count < 2) return;
        float Width(Squad s) => Math.Clamp(s.Columns, 1, Math.Max(1, s.Alive)) * s.Spacing;
        int mid = rank.Count / 2;
        for (int i = mid + 1; i < rank.Count; i++) CloseUp(rank[i - 1], rank[i], +1);
        for (int i = mid - 1; i >= 0; i--) CloseUp(rank[i + 1], rank[i], -1);
        void CloseUp(Squad inner, Squad outer, int side)
        {
            float xi = Vector2.Dot(d.Offsets[inner], line.Right), xo = Vector2.Dot(d.Offsets[outer], line.Right);
            float want = xi + side * (Width(inner) / 2 + 3 + Width(outer) / 2);
            float over = (xo - want) * side; // how much wider the gap is than it should be
            if (over > 2) d.Offsets[outer] -= line.Right * side * MathF.Min(over, 3);
        }
    }

    /// Each look on the line: a good officer (skill 6 and up) pulls a spent company (under 40% of its men, or its nerve
    /// nearly gone) back `RestBack` metres to get its breath, and sends it back once it has its nerve again. A company he
    /// keeps behind the line steps into the place it left. A Stubborn officer's men hold longer: they are pulled back later,
    /// and while he holds, his shaken men take heart a little each look.
    void RestSpent(Division d, List<Squad> live, Line line)
    {
        bool stubborn = d.Has("Stubborn");
        if (stubborn && d.Stance == Stance.Hold)
            foreach (var s in live) foreach (var u in s.Units) if (u.Active && !u.Routing && u.Morale < 60) u.Morale += 0.5f;
        if (d.Skill < 6) { d.Resting.Clear(); return; }
        foreach (var s in live)
        {
            float nerve = Nerve(s);
            if (d.Resting.Contains(s))
            {
                if (nerve > 55 && s.Alive >= s.StartCount * 0.25f) d.Resting.Remove(s);
                continue;
            }
            bool spent = s.Alive < s.StartCount * (stubborn ? 0.3f : 0.4f) || nerve < (stubborn ? 25 : 35);
            if (!spent || !s.InContact || d.Resting.Count + 1 >= live.Count) continue;
            d.Resting.Add(s);
            // A fresh company standing behind the line takes the spent one's place.
            float front = Vector2.Dot(d.Offsets[s], line.Fwd);
            var fresh = live.Where(q => q != s && !d.Resting.Contains(q) && d.Offsets.ContainsKey(q) && Vector2.Dot(d.Offsets[q], line.Fwd) < front - 6 && !q.InContact)
                .OrderBy(q => Vector2.DistanceSquared(d.Offsets[q], d.Offsets[s])).FirstOrDefault();
            if (fresh != null) (d.Offsets[s], d.Offsets[fresh]) = (d.Offsets[fresh], d.Offsets[s]);
            if (d.Team == 0) Log($"{d.Name} pulls the {s.Name} back to get their breath{(fresh != null ? $"; the {fresh.Name} take their place" : "")}.");
        }
    }

    /// The company's nerve: its standing men's average morale.
    static float Nerve(Squad s)
    {
        float sum = 0;
        int n = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing) { sum += u.Morale; n++; }
        return n == 0 ? 0 : sum / n;
    }
}
