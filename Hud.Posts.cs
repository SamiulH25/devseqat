using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: your officers' posts on the field (docs/archive/OFFICERS.md OF4). While you deploy (and for the officer you picked) each post
/// is a marked zone where his post lies, in his colour, named; drop companies on it (drag a card there, or pick companies and
/// click the zone) to put them under him.
sealed partial class Hud
{
    /// The ground under the cursor (BattleScreen sets it each frame), for hit-testing zones.
    public Vector2 MouseGround;
    /// The company card being dragged in deployment, if any.
    public Squad? Dragging;

    /// A post's zone on the ground: its middle, facing, right, and half its width and depth.
    (Vector2 C, Vector2 F, Vector2 R, float HalfW, float HalfD)? Zone(Division d)
    {
        var line = B.FrontLine(d.Team);
        var live = d.Squads.Where(s => s.Alive > 0).ToList();
        if (d.Post == Post.Front && live.Count > 0) return null; // the front stands where its companies stand: their rings show it
        var c = B.SpotOf(d, d.Goal) ?? (live.Count > 0 ? live.Aggregate(Vector2.Zero, (a, s) => a + s.Center) / live.Count : (Vector2?)null);
        if (c is not Vector2 at) return null;
        float width = live.Sum(s => Math.Clamp(s.Columns, 1, Math.Max(1, s.Alive)) * s.Spacing) + 4 * Math.Max(0, live.Count - 1);
        float depth = live.Count == 0 ? 8 : live.Max(s => (s.Alive + Math.Max(1, s.Columns) - 1) / Math.Max(1, s.Columns) * s.Spacing);
        return (at, line.Fwd, line.Right, Math.Clamp(width / 2 + 3, 10, 45), Math.Clamp(depth / 2 + 3, 6, 18));
    }

    bool ZonesShown(Division d) => Command && !ShowResult && (Deploying || d == Div);

    /// The officer whose post zone lies under this point of the ground (yours, shown now), if any.
    public Division? ZoneAt(Vector2 ground)
    {
        foreach (var d in B.DivisionsOf(0))
        {
            if (!ZonesShown(d)) continue;
            if (d.Post == Post.Front && d.Squads.Any(s => s.Alive > 0))
            {
                // A front officer's place is his companies: drop onto one of them.
                if (d.Squads.Any(s => s.Alive > 0 && Vector2.Distance(s.Center, ground) < s.Radius + 3)) return d;
                continue;
            }
            if (Zone(d) is not var (c, f, r, hw, hd)) continue;
            var o = ground - c;
            if (MathF.Abs(Vector2.Dot(o, r)) <= hw && MathF.Abs(Vector2.Dot(o, f)) <= hd) return d;
        }
        return null;
    }

    /// Each post as a dashed box on the ground with its name and officer above; the zone under a dragged card lights up.
    void PostZones()
    {
        var vp = new Viewport(0, 0, W, H);
        bool Screen(Vector2 p, out Vector2 s)
        {
            var v = vp.Project(new Vector3(p.X, B.Terrain.Height(p) + 0.25f, p.Y), Proj, View, Matrix.Identity);
            s = new Vector2(v.X, v.Y);
            return v.Z is > 0 and < 1;
        }
        var under = (Dragging != null || Deploying && Selected.Count > 0) ? ZoneAt(MouseGround) : null;
        foreach (var d in B.DivisionsOf(0))
        {
            if (!ZonesShown(d)) continue;
            if (Zone(d) is not var (c, f, r, hw, hd))
            {
                // The front: its name just over the company its officer stands in (his companies may be spread far apart).
                var live = d.Squads.Where(s => s.Alive > 0).ToList();
                if (live.Count == 0) continue;
                var own = d.Leader?.Squad is { Alive: > 0 } ls && live.Contains(ls) ? ls : live[0];
                float wide = Math.Clamp(own.Columns, 1, Math.Max(1, own.Alive)) * own.Spacing;
                // On the company itself (so it is plainly his), off to one side of the middle, where its own flag stands.
                if (Screen(own.Center + B.FrontLine(0).Right * (wide * 0.3f), out var lp)) PostLabel(d, lp, d == under);
                continue;
            }
            bool hot = d == under;
            var col = hot ? Gold : d == Div ? Gold * 0.9f : DivisionColour(d) * 0.85f;
            var corners = new[] { c - r * hw - f * hd, c + r * hw - f * hd, c + r * hw + f * hd, c - r * hw + f * hd };
            for (int i = 0; i < 4; i++)
            {
                // Dashes along each side, 2 m on and 1.5 m off, so the zone reads as a place and not as a company.
                var a = corners[i]; var b = corners[(i + 1) % 4];
                float len = Vector2.Distance(a, b);
                for (float t = 0; t < len; t += 3.5f)
                    if (Screen(a + (b - a) * (t / len), out var p0) && Screen(a + (b - a) * (MathF.Min(len, t + 2) / len), out var p1))
                        Line(p0, p1, col, hot ? U(2) : Math.Max(1, U(1)));
            }
            if (hot && Screen(c, out var mid)) Rect(new Rectangle((int)mid.X - U(2), (int)mid.Y - U(2), U(4), U(4)), Gold);
            // The post's name at the zone's back edge, and his name under it.
            if (Screen(c + f * hd, out var top)) PostLabel(d, top, hot);
        }
    }

    /// A post's name and its officer's, in a dark tag standing on the point.
    void PostLabel(Division d, Vector2 top, bool hot)
    {
        var col = DivisionColour(d); // as on his card: the post white, the officer in his colour
        string post = Goal.Label(d.Post), who = d.Name.ToUpperInvariant();
        int tw = Math.Max(TW(post, 1), TW(who, 1)) + U(8);
        var box = new Rectangle((int)top.X - tw / 2, (int)top.Y - U(24), tw, U(21));
        // Never under a panel: a tag the HUD would cut is left out (the officer's card names him).
        if (Over(box.Location) || Over(new Point(box.Right, box.Y)) || Over(new Point(box.X, box.Bottom)) || Over(new Point(box.Right, box.Bottom))) return;
        Rect(box, Color.Black * 0.55f);
        if (hot || d == Div) Frame(box, Gold, U(1)); // picked or a drop target: the gold is the frame
        TextC(post, top.X, box.Y + U(3), Color.White, 1);
        TextC(who, top.X, box.Y + U(12), col, 1);
    }

    /// The card being dragged follows the cursor, naming the post it would go to.
    void DragGhost()
    {
        var under = Dragging != null ? ZoneAt(MouseGround) ?? DivisionAt(Mouse) : null;
        if (Dragging != null)
        {
            string n = Dragging.Name.ToUpperInvariant() + (under != null ? $" > {Goal.Label(under.Post)}" : "");
            var r = new Rectangle(Mouse.X + U(10), Mouse.Y + U(6), TW(n, 1) + U(10), U(13));
            Panel(r, under != null ? Gold : Edge);
            Text(n, r.X + U(5), r.Y + U(3), under != null ? Gold : Color.White, 1);
        }
    }
}
