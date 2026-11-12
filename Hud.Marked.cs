using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: the enemy sees you (docs/COMPANY.md C12). In the field view each foe who holds you as his mark gets a red chevron
/// over his head, or a red arrow at the screen's edge when he is out of sight; the man a veteran company sends to meet you
/// gets a gold one marked MEETS YOU. Above your panel, how many are on you. Drawing only.
sealed partial class Hud
{
    void MarkedMarks()
    {
        var p = B.Player;
        if (!p.Active || B.MarkedBy == 0) return;
        var vp = new Viewport(0, 0, W, H);
        int edge = U(18);
        foreach (var u in B.Units)
        {
            if (!u.OnCaptain || !u.Active) continue;
            bool meets = u.Squad is Squad s && s.Meets == u;
            var col = meets ? Gold : Bad;
            var v = vp.Project(new Vector3(u.Pos.X, B.Terrain.Height(u.Pos) + (u.Mounted ? 3.0f : 2.15f), u.Pos.Y), Proj, View, Matrix.Identity);
            bool ahead = v.Z is > 0 and < 1;
            if (ahead && v.X > edge && v.X < W - edge && v.Y > edge && v.Y < H - edge)
            {
                Chevron(new Vector2(v.X, v.Y), new Vector2(0, 1), col, U(7));
                if (meets)
                {
                    int lw = TW("MEETS YOU", 1) + U(8);
                    Rect(new Rectangle((int)v.X - lw / 2, (int)v.Y - U(25), lw, U(12)), Color.Black * 0.7f);
                    Shadowed("MEETS YOU", v.X, v.Y - U(22), Gold, 1, 1);
                }
                continue;
            }
            // Out of sight: an arrow on the edge, pointing the way he comes from.
            var c = new Vector2(W / 2f, H / 2f);
            var dir = new Vector2(v.X, v.Y) - c;
            if (!ahead) dir = -dir; // behind the camera the projection flips
            if (dir.LengthSquared() < 1) dir = new Vector2(0, 1);
            dir.Normalize();
            float sx = (W / 2f - edge) / MathF.Max(1e-3f, MathF.Abs(dir.X)), sy = (H / 2f - edge) / MathF.Max(1e-3f, MathF.Abs(dir.Y));
            Chevron(c + dir * MathF.Min(sx, sy), dir, col, U(10));
        }
        var r = player;
        int meeting = B.Units.Count(u => u.OnCaptain && u.Active && u.Squad is Squad q && q.Meets == u);
        string count = $"{B.MarkedBy - meeting} ON YOU";
        int tw = TW(count, 2) + U(16), h = U(22);
        var box = new Rectangle(r.X, r.Y - h - U(10), tw, h);
        Rect(box, Color.Black * 0.8f);
        Frame(box, B.MarkedBy >= B.MarkCap ? Bad : Kit.Mul(Bad, 0.7f), U(2));
        Shadowed(count, box.Center.X, box.Y + U(5), Color.Lerp(Bad, Color.White, 0.35f), 2, 1);
        if (meeting == 0) return;
        string meet = meeting == 1 ? "1 STEPS OUT TO MEET YOU" : $"{meeting} STEP OUT TO MEET YOU";
        var mb = new Rectangle(box.Right + U(6), box.Y + U(4), TW(meet, 1) + U(12), U(14));
        Rect(mb, Color.Black * 0.8f);
        Frame(mb, Kit.Mul(Gold, 0.7f), U(1));
        Text(meet, mb.X + U(6), mb.Y + U(4), Gold, 1);
    }

    /// A sharp V whose point is at `tip`, pointing along `dir` (screen space): two thick strokes with a dark edge.
    void Chevron(Vector2 tip, Vector2 dir, Color c, float size)
    {
        var side = new Vector2(-dir.Y, dir.X);
        var l = tip - dir * size * 1.3f - side * size;
        var r = tip - dir * size * 1.3f + side * size;
        float w = MathF.Max(2, size * 0.45f);
        Line(l, tip, Color.Black * 0.85f, w + U(2));
        Line(r, tip, Color.Black * 0.85f, w + U(2));
        Line(l, tip, c, w);
        Line(r, tip, c, w);
    }
}
