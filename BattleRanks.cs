using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: companies fight from their ranks (docs/archive/MELEE.md D3). Each man keeps his place for the battle; a death moves the
/// man behind into the gap; a charging company closes as a body; the front rank fights while the ranks behind wait.
public partial class Battle
{
    readonly List<Unit> placing = new(64);
    List<Unit>[] files = Array.Empty<List<Unit>>();

    /// A fixed 0-1 draw per man from his place in the battle (no rng: adding a man must not shift the battle's dice).
    static float GritOf(int index)
    {
        uint h = (uint)index * 2654435761u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    /// Deal places to the company's standing men and set their slots. A full re-deal only when the layout changes
    /// (columns, formation); otherwise each file closes up from behind and the deepest files lend men to the shallowest.
    void PlaceMen(Squad s, int n)
    {
        placing.Clear();
        foreach (var u in s.Units)
        {
            if (u.Active && !u.Routing) placing.Add(u);
            else u.Place = -1;
        }
        int cols = Math.Clamp(s.Columns, 1, Math.Max(1, n));
        bool ranked = s.Formation is not (Formation.Square or Formation.Wedge);
        bool full = !ranked || cols != s.LayoutCols || s.Formation != s.LayoutForm;
        bool changed = full || n != s.LayoutN || !Ranked(s);
        if (!changed) foreach (var u in placing) if (u.Place < 0) { changed = true; break; }
        if (changed)
        {
            if (full || cols == 1 || !Ranked(s)) Redeal(placing); // mobs and rabble keep no files
            else CloseFiles(placing, cols);
            s.LayoutCols = cols;
            s.LayoutForm = s.Formation;
            s.LayoutN = n;
        }
        Vector2 front = Vector2.Zero, all = Vector2.Zero;
        int nf = 0, na = 0;
        foreach (var u in placing)
        {
            u.Rank = s.Formation switch { Formation.Square => 0, Formation.Wedge => (int)MathF.Sqrt(u.Place), _ => u.Place / cols };
            u.Slot = s.Slot(u.Place, n);
            if (u.Rank != 0) continue;
            all += u.Pos; na++;
            if (Vector2.DistanceSquared(u.Pos, u.Slot) < 36) { front += u.Pos; nf++; } // a man caught far behind (on a house) does not hold the line back
        }
        s.FrontPos = nf > 0 ? front / nf : na > 0 ? all / na : s.Anchor;
    }

    /// Places 0..n-1 in the order the men already stood (new men last).
    static void Redeal(List<Unit> men)
    {
        men.Sort((a, b) => (a.Place < 0 ? int.MaxValue : a.Place).CompareTo(b.Place < 0 ? int.MaxValue : b.Place));
        for (int i = 0; i < men.Count; i++) men[i].Place = i;
    }

    /// Each file closes up toward the front; new men, then men from the back of the deepest files, fill the shallowest
    /// (the centre first), until no file is more than one man deeper than another.
    void CloseFiles(List<Unit> men, int cols)
    {
        if (files.Length < cols) files = Enumerable.Range(0, cols).Select(_ => new List<Unit>(8)).ToArray();
        for (int f = 0; f < cols; f++) files[f].Clear();
        var loose = new List<Unit>();
        foreach (var u in men) { if (u.Place < 0) loose.Add(u); else files[u.Place % cols].Add(u); }
        for (int f = 0; f < cols; f++) files[f].Sort((a, b) => a.Place.CompareTo(b.Place));
        float mid = (cols - 1) / 2f;
        int Shallowest()
        {
            int best = 0;
            for (int f = 1; f < cols; f++)
                if (files[f].Count < files[best].Count || files[f].Count == files[best].Count && MathF.Abs(f - mid) < MathF.Abs(best - mid)) best = f;
            return best;
        }
        foreach (var u in loose) files[Shallowest()].Add(u);
        for (int guard = 0; guard < men.Count; guard++)
        {
            int lo = Shallowest(), hi = -1;
            for (int f = 0; f < cols; f++)
                if (files[f].Count > files[lo].Count + 1 && (hi < 0 || files[f].Count > files[hi].Count || files[f].Count == files[hi].Count && Math.Abs(f - lo) < Math.Abs(hi - lo))) hi = f;
            if (hi < 0) break;
            var m = files[hi][^1];
            files[hi].RemoveAt(files[hi].Count - 1);
            files[lo].Add(m);
        }
        for (int f = 0; f < cols; f++)
            for (int r = 0; r < files[f].Count; r++) files[f][r].Place = r * cols + f;
    }

    /// Fights from its ranks: foot companies of men in the open field (rabble too, until their ranks break: BattleDrill.cs),
    /// and the raised dead. Beasts and swarms (goblins, packs, trolls, ghouls) fight as mobs, and a siege's walls and
    /// ladders take men one by one.
    bool Ranked(Squad s) => !s.Swarm && !s.Cavalry && Wall == null && (s.Units.Count == 0 || !s.Units[0].Type.Wild || DrillOf(s.Units[0].Type) > 0);

    /// A charging company closes with its foe as a body: its front-rank line walks up to a sword's length short of the foe's
    /// nearest man, turning to face him, never more than a few metres ahead of where the front rank is. In contact it holds.
    void Close(Squad s, float dt)
    {
        if (s.Order != Order.Charge || !Ranked(s) || s.Alive == 0) { s.Foe = null; return; }
        s.Foe = s.Target is { Alive: > 0 } t ? t : NearestFoe(s);
        if (s.Foe == null) return;
        var f = s.FrontPos;
        Unit? near = null;
        float nd = float.MaxValue;
        foreach (var o in s.Foe.Units)
        {
            if (!o.Active || o.Routing) continue;
            float d2 = Vector2.DistanceSquared(o.Pos, f);
            if (d2 < nd) { nd = d2; near = o; }
        }
        if (near == null) return;
        var toC = s.Foe.Center - f;
        if (toC.LengthSquared() < 0.01f) toC = near.Pos - f;
        if (toC.LengthSquared() < 0.01f) return;
        var dir = Vector2.Normalize(toC);
        float standoff = MathF.Max(1, s.Units[0].Type.Reach * 0.8f);
        // Taking the foe in the flank (BattleTactics.cs): the front makes for a point beside his end, then turns in on him.
        float walk = -1;
        if (s.Plan == Plan.Flank && s.Bend == 0 && !s.InContact)
        {
            var (at, face) = FlankPoint(s, s.Foe, standoff);
            var to = at - f;
            float dist = to.Length();
            if (dist > 2) { dir = to / dist; walk = MathF.Min(dist, 3); }
            else dir = face;
        }
        // On a route round water or hedges, the front follows the route until it is near.
        if (s.Path != null && nd > 100 && s.Path.Dir(f) is var pd && pd != Vector2.Zero) dir = pd;
        float turn = (s.InContact ? 0.15f : 1.5f) * dt; // at blows a line barely turns: wheeling would open it
        float a = MathF.Atan2(s.Front.Y, s.Front.X), b = MathF.Atan2(dir.Y, dir.X);
        a += Math.Clamp(MathHelper.WrapAngle(b - a), -turn, turn);
        s.Front = new Vector2(MathF.Cos(a), MathF.Sin(a));
        // At blows the line holds where it stands (it never gives ground by itself); otherwise it walks on to a sword's
        // length short of the foe.
        if (s.InContact) return;
        float ahead = walk >= 0 ? walk : Math.Clamp(Vector2.Dot(near.Pos - f, dir) - standoff, 0, 3);
        s.Anchor = f + dir * ahead;
    }

    /// Its foe is a huge beast or a handful of men (a quarter of its own number or fewer): ranks are no use, it closes round them.
    static bool Overwhelms(Squad s) => s.Foe is { } f && (f.Alive * 4 <= s.Alive || f.Units.Count > 0 && f.Units[0].Type.Huge);

    /// The enemy company whose edge is nearest (routed companies left out).
    Squad? NearestFoe(Squad s)
    {
        Squad? best = null;
        float bd = float.MaxValue;
        foreach (var e in Squads)
        {
            if (e.Team == s.Team || e.Alive == 0) continue;
            float d = Vector2.Distance(e.Center, s.Center) - e.Radius;
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    /// How far a man of this company steps off his place to strike: half a pace in an elite company, far in a rabble.
    static float Lunge(Squad s) => 0.4f + 3.4f * MathF.Pow(1 - s.Discipline, 1.5f);

    /// How hard a man is to shove (x his body's mass): up to 4x for a man on his place in a well-ordered company.
    float Footing(Unit u) => u.Squad is { } s && !u.Routing && !s.Broken && Ranked(s) && Vector2.DistanceSquared(u.Pos, u.Slot) < 1 ? 1 + 3 * s.Discipline : 1;

    /// One man of a ranked company (Think has already handled routing, swings, arrows and braced spears).
    /// He fights whoever is within reach where he stands, and keeps his guard toward a foe close by; a front-rank man (or a
    /// long spear in the second rank) steps a short way off his place to reach a foe, the second rank a shorter way through
    /// the gaps, and anyone a step toward a foe at arm's length from his place; the rest keep their places. In a poorly
    /// drilled company some men in the back ranks cannot wait and go looking for a fight.
    void FightFromRanks(Unit u, Squad s, Unit? enemy, float d, float dt)
    {
        bool charge = s.Order == Order.Charge;
        float reach = enemy == null ? 0 : ReachOf(u) + (enemy.Mounted ? 0.9f : 0);
        bool front = u.Rank == 0 || u.Rank == 1 && ReachOf(u) > 2.2f;
        bool spill = charge && !front && u.Grit < (1 - s.Discipline) * (1 - s.Discipline);
        if (enemy != null && s.Foe != null && enemy.Squad != s.Foe && enemy.Target != u && (front || spill))
        {
            var (fu, fd) = Nearest(u, s.Foe); // his company's foe first, unless someone else is on him
            if (fu != null && fd < Lunge(s) + reach + 1) { (enemy, d) = (fu, fd); reach = ReachOf(u) + (enemy.Mounted ? 0.9f : 0); }
        }
        if (enemy != null)
        {
            if (d <= reach)
            {
                if (u.Action == UnitAction.Block) { if (u.Target is { } th) u.Face(th.Pos - u.Pos); return; } // guard up
                if (Guard(u, s)) return;
                u.Face(enemy.Pos - u.Pos);
                if (u.Cooldown <= 0) StartSwing(u, enemy);
                return;
            }
            float allowed = spill && d < 8 ? 99 : front ? Lunge(s) : u.Rank == 1 ? Lunge(s) * 0.6f : 0.8f;
            if (Vector2.Distance(u.Slot, enemy.Pos) < allowed + reach)
            {
                var to = Via(u, enemy.Pos) - u.Pos;
                u.Face(to);
                u.Wish = to / MathF.Max(0.01f, to.Length()) * u.Type.Speed;
                return;
            }
        }
        float toSlot = Vector2.Distance(u.Pos, u.Slot);
        if (toSlot > 0.4f)
        {
            var to = Via(u, u.Slot) - u.Pos;
            u.Wish = to / MathF.Max(0.01f, to.Length()) * MathF.Min(u.Type.Speed, s.MarchSpeed) * MathF.Min(1, toSlot);
            u.Face(enemy != null && d < 3 ? enemy.Pos - u.Pos : charge && toSlot < 2 ? s.Front : to);
        }
        else u.Face(enemy != null && d < 3 ? enemy.Pos - u.Pos : s.FaceAt(u.Slot)); // guard toward a foe close by
    }

    readonly List<Unit> nearThreat = new(16);

    /// A man who sees a foe winding up a blow at him may raise his guard instead of swinging: a trained company's men
    /// do it as often as their discipline allows, rabble almost never. The guard is held until the blow falls.
    bool Guard(Unit u, Squad s)
    {
        if (u.Action != UnitAction.None || s.Grade == Grade.Rabble) return false;
        Unit? threat = null;
        foreach (var o in Near(u.Pos, 3.5f, nearThreat))
            if (o.Team != u.Team && o.Active && o.Action == UnitAction.Windup && o.Target == u && (threat == null || o.ActionTimer < threat.ActionTimer)) threat = o;
        if (threat == null || threat == u.Target && u.Guard || rng.NextSingle() >= s.Discipline * 0.8f) return false;
        u.Action = UnitAction.Block;
        u.ActionTimer = threat.ActionTimer + 0.15f;
        u.Guard = true;
        u.Target = threat;
        u.Face(threat.Pos - u.Pos);
        return true;
    }

    /// Mean distance of a company's standing men from their places (0 = a perfect formation).
    internal static float RankError(Squad s)
    {
        float sum = 0;
        int n = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing) { sum += Vector2.Distance(u.Pos, u.Slot); n++; }
        return n == 0 ? 0 : sum / n;
    }

    /// The share of a's standing men that have one of b's men behind them (relative to a's front): how far the two
    /// companies have soaked into each other. Two lines meeting score near 0; a blob scores high.
    internal static float Intermix(Squad a, Squad b)
    {
        int n = 0, mixed = 0;
        foreach (var u in a.Units)
        {
            if (!u.Active || u.Routing) continue;
            n++;
            foreach (var o in b.Units)
                if (o.Active && !o.Routing && Vector2.Dot(o.Pos - u.Pos, a.Front) < -0.5f && Vector2.DistanceSquared(o.Pos, u.Pos) < 9) { mixed++; break; }
        }
        return n == 0 ? 0 : mixed / (float)n;
    }
}
