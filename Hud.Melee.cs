using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: man against man in the field view. Big numbers pop over the man you hit (and in red over you when you are hit),
/// and the foe you are fighting gets his health bar and a plain word on what he is doing: winding up (and where to block),
/// blocking, reeling (strike now) or spent. In single combat both fighters' health stand at the top. Drawing only.
sealed partial class Hud
{
    /// How long a number stays up, in battle seconds.
    const float BlowShown = 1.1f;

    /// Big numbers over each man your blows found, rising and fading: white, gold for a heavy blow, red-gold FELLED for a kill.
    /// What made it bite (flank, rear...) sits small under the number. Blows on you rise red over your own head.
    void BlowNumbers()
    {
        var vp = new Viewport(0, 0, W, H);
        foreach (var b in B.Blows)
        {
            float age = B.Time - b.At;
            if (age < 0 || age > BlowShown || b.Amount < 0.5f) continue;
            var t = b.Target;
            float lift = (t.Mounted ? 4.1f : 3.2f) + age * 0.9f; // above his name and health bar
            var v = vp.Project(new Vector3(t.Pos.X, B.Terrain.Height(t.Pos) + lift, t.Pos.Y), Proj, View, Matrix.Identity);
            if (v.Z is <= 0 or >= 1) continue;
            float fade = age < BlowShown - 0.35f ? 1 : (BlowShown - age) / 0.35f;
            bool heavy = b.Amount >= t.Type.MaxHp * 0.4f;
            int scale = b.Fatal || heavy ? 4 : 3;
            if (age < 0.08f) scale++; // the pop as it lands
            var col = b.OnPlayer ? Bad : b.Fatal ? Orange : heavy ? Gold : Color.White;
            string num = b.OnPlayer ? $"-{b.Amount:0}" : $"{b.Amount:0}";
            Shadowed(num, v.X, v.Y - scale * U(7), col * fade, scale, fade);
            string note = b.Fatal ? "FELLED" : b.Tag;
            if (note != "" && !b.OnPlayer) Shadowed(note, v.X, v.Y + U(2), (b.Fatal ? Orange : Gold) * fade, 1, fade);
        }
    }

    /// Centred text with a dark edge so it reads over sky, grass and men alike.
    void Shadowed(string s, float cx, float y, Color c, int scale, float fade)
    {
        int o = Math.Max(1, U(scale >= 3 ? 2 : 1));
        TextC(s, cx + o, y + o, Color.Black * (0.8f * fade), scale);
        TextC(s, cx, y, c, scale);
    }

    /// The man you are fighting: in single combat the champion; else the last man you struck while he is close, else the
    /// nearest foe in front of you or coming for you, within a few paces.
    Unit? Opponent()
    {
        var p = B.Player;
        if (B.Options.Duel && B.EnemyCaptain.Active) return B.EnemyCaptain;
        for (int i = B.Blows.Count - 1; i >= 0; i--)
        {
            var b = B.Blows[i];
            if (!b.OnPlayer && b.Target.Active && b.Target.Team != p.Team && B.Time - b.At < 3 && Vector2.DistanceSquared(b.Target.Pos, p.Pos) < 36) return b.Target;
        }
        Unit? best = null;
        float bestScore = float.MaxValue;
        foreach (var u in B.Units)
        {
            if (!u.Active || u.Team == p.Team || u.Routing) continue;
            var to = u.Pos - p.Pos;
            float d = to.Length();
            if (d > 5 || d < 1e-3f) continue;
            float score = d - (u.Target == p ? 2 : 0) - (Vector2.Dot(to / d, p.Dir) > 0.5f ? 1 : 0);
            if (score < bestScore) { bestScore = score; best = u; }
        }
        return best;
    }

    /// What he is doing, in a word you can act on, and its colour.
    (string Text, Color Col) Intent(Unit u)
    {
        var p = B.Player;
        if (u.Action == UnitAction.Windup && u.Target == p)
            return ($"SWINGS: BLOCK {BlockWord(Dirs.Mirror(u.Swing))}", Foe);
        if (u.Action == UnitAction.Stagger) return ("REELING: STRIKE NOW", Gold);
        if (u.Action == UnitAction.Block) return ("GUARD UP", Dim);
        if (u.Action == UnitAction.Recover) return ("OPEN", Ok);
        if (u.Stamina < 25) return ("SPENT", Ok);
        return ("", Dim);
    }

    /// The direction to guard, as the crosshair chevron that lights for it.
    static string BlockWord(AttackDir d) => d switch { AttackDir.Overhead => "UP", AttackDir.Thrust => "DOWN", AttackDir.Left => "LEFT", _ => "RIGHT" };

    /// Over the foe you are fighting: his name, his health and what he is doing.
    void OpponentMark()
    {
        if (!B.Player.Active || Opponent() is not Unit u) return;
        var vp = new Viewport(0, 0, W, H);
        var v = vp.Project(new Vector3(u.Pos.X, B.Terrain.Height(u.Pos) + (u.Mounted ? 3.6f : 2.6f), u.Pos.Y), Proj, View, Matrix.Identity);
        if (v.Z is <= 0 or >= 1) return;
        int bw = U(64), x = (int)Math.Clamp(v.X, bw / 2 + U(4), W - bw / 2 - U(4)), y = (int)Math.Clamp(v.Y, U(70), H - U(140));
        string name = Battle.NameOf(u) is { Length: > 0 } n ? n : u.Type.Name;
        Shadowed(name.ToUpperInvariant(), x, y - U(12), u.IsLeader || u.Hero ? Gold : Color.White, 1, 1);
        var bar = new Rectangle(x - bw / 2, y, bw, U(5));
        Rect(new Rectangle(bar.X - U(1), bar.Y - U(1), bar.Width + U(2), bar.Height + U(2)), Color.Black * 0.75f);
        Bar(bar, Math.Clamp(u.Hp / u.Type.MaxHp, 0, 1), Foe);
        var (word, col) = Intent(u);
        if (word != "") Shadowed(word, x, bar.Bottom + U(4), col, 1, 1);
    }

    /// Single combat: both fighters' health at the top, yours on the left, his on the right, with the numbers.
    void DuelBars()
    {
        if (!B.Options.Duel) return;
        int bw = U(170), y = U(78), gap = U(20);
        var mine = new Rectangle(W / 2 - gap / 2 - bw, y, bw, U(10));
        var his = new Rectangle(W / 2 + gap / 2, y, bw, U(10));
        Panel(new Rectangle(mine.X - U(8), y - U(16), bw * 2 + gap + U(16), U(32)), Gold);
        TextC("SINGLE COMBAT", W / 2f, y - U(12), Gold, 1);
        Fighter(mine, B.Player, false);
        Fighter(his, B.EnemyCaptain, true);
    }

    void Fighter(Rectangle r, Unit u, bool foe)
    {
        float f = Math.Clamp(u.Hp / u.Type.MaxHp, 0, 1);
        Rect(r, Color.Black * 0.6f);
        int w = (int)(r.Width * f);
        Rect(foe ? new Rectangle(r.Right - w, r.Y, w, r.Height) : new Rectangle(r.X, r.Y, w, r.Height), foe ? Foe : SideArms(0).Ui);
        string hp = $"{MathF.Max(0, u.Hp):0}";
        if (foe) Text(hp, r.X + U(3), r.Y + U(2), Color.White, 1);
        else TextR(hp, r.Right - U(3), r.Y + U(2), Color.White, 1);
    }
}
