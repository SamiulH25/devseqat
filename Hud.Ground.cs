using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: the ground for the player (docs/archive/TERRAIN.md T6): the ground map (Act.GroundView, shown while deploying), the line
/// under the mouse saying what the ground does, and a warning on an order that sends men onto bad ground.
sealed partial class Hud
{
    public Texture2D? GroundMap;
    public bool GroundView;
    /// What the ground under the mouse does (command view, nothing hovered), and a warning for the order being drawn.
    public string? GroundLine, OrderWarning;
    Battle? kindsOf;
    List<GroundKind> kinds = new();

    /// The ground map: the field by kind of ground, with contours, your companies and the enemies you have seen.
    void GroundPanel()
    {
        if (GroundMap == null) return;
        int size = Math.Clamp(Math.Min(H - U(380), U(360)), U(160), U(360)), legend = U(120);
        var frame = new Rectangle(U(14), U(146), size + legend + U(24), size + U(40));
        Panel(frame, Gold);
        Text("THE GROUND", frame.X + U(10), frame.Y + U(8), Gold, 1);
        TextR($"{Ctl.Name(Act.GroundView)} CLOSES", frame.Right - U(10), frame.Y + U(8), Dim, 1);
        var r = new Rectangle(frame.X + U(10), frame.Y + U(28), size, size);
        sb.Draw(GroundMap, r, Color.White);
        Frame(r, Color.Black * 0.7f, U(1));
        Vector2 M(Vector2 w) { var m = World.WorldToMinimap(w); return new Vector2(r.X + m.X * r.Width, r.Y + m.Y * r.Height); }
        foreach (var s in B.Squads)
        {
            if (s.Alive == 0 || s.Team == 1 && !B.Shown(s)) continue;
            var p = M(s.Team == 1 && !B.Visible(s, 0) ? s.LastSeen : s.Center);
            if (!r.Contains((int)p.X, (int)p.Y)) continue;
            int d = U(s.Cavalry ? 6 : 5);
            Rect(new Rectangle((int)p.X - d / 2, (int)p.Y - d / 2, d, d), s.Team == 1 ? Foe : Selected.Contains(s) ? Gold : Friendly);
        }
        // The legend: the kinds of ground on this field.
        if (kindsOf != B)
        {
            kindsOf = B;
            var t = B.Terrain;
            var seen = new HashSet<GroundKind>();
            for (float z = -B.MapRadius; z <= B.MapRadius; z += 5)
                for (float x = -B.MapRadius; x <= B.MapRadius; x += 5) seen.Add(t.GroundAt(new Vector2(x, z)));
            kinds = seen.OrderBy(k => k).ToList();
        }
        int lx = r.Right + U(10), ly = r.Y;
        foreach (var k in kinds)
        {
            Rect(new Rectangle(lx, ly + U(1), U(10), U(10)), World.KindColor(k));
            Text(Grounds.Name(k), lx + U(15), ly + U(2), Color.White, 1);
            ly += U(15);
        }
        Text("LINES: 2 M OF HEIGHT", lx, ly + U(6), Dim, 1);
    }

    /// Under the mouse: a warning for the order being drawn, else what the ground does.
    void GroundHints()
    {
        string? text = OrderWarning ?? (HoverSquad == null ? GroundLine : null);
        if (text == null) return;
        bool warn = OrderWarning != null;
        int w = TW(text, 1) + U(14), h = U(18);
        var r = new Rectangle(Math.Min(Mouse.X + U(16), W - w - 4), Math.Min(Mouse.Y + U(26), H - h - 4), w, h);
        Panel(r, warn ? Bad : null);
        Text(text, r.X + U(7), r.Y + U(5), warn ? Color.White : Dim, 1);
    }
}
