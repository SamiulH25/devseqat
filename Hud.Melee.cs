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
        if (LockTarget is { Active: true, Routing: false } locked && locked.Team != p.Team) return locked;
        int duelFoes = B.Options.Duel ? B.Units.Count(u => u.Active && u.Team != p.Team) : 0;
        if (B.Options.Duel && duelFoes <= 1 && B.EnemyCaptain.Active) return B.EnemyCaptain;
        for (int i = B.Blows.Count - 1; i >= 0; i--)
        {
            var b = B.Blows[i];
            if (!b.OnPlayer && b.Target.Active && b.Target.Team != p.Team && B.Time - b.At < 3 && Vector2.DistanceSquared(b.Target.Pos, p.Pos) < 36) return b.Target;
        }
        Unit? best = null;
        float bestScore = float.MaxValue;
        float maxRange = B.Options.Duel ? 18f : 5f;
        foreach (var u in B.Units)
        {
            if (!u.Active || u.Team == p.Team || u.Routing) continue;
            var to = u.Pos - p.Pos;
            float d = to.Length();
            if (d > maxRange || d < 1e-3f) continue;
            float score = d - (u.Target == p ? 2 : 0) - (Vector2.Dot(to / d, p.Dir) > 0.5f ? 1 : 0);
            if (score < bestScore) { bestScore = score; best = u; }
        }
        return best;
    }

    /// CR6: 4-chevron directional combat reticle around centre screen + compact melee stamina/posture bar.
    /// Gilt = your active attack/guard direction; flashing crimson = incoming enemy swing to block/parry; bright gold pulse = riposte/chamber window.
    void CombatReticle(int cx, int cy)
    {
        var p = B.Player;
        bool ripostePulse = B.Riposting || (B.Time - p.ClashedAt < 0.35f);
        float pulse = 0.65f + 0.35f * MathF.Sin(B.Time * 22f);

        // Centre dot (bright gold during riposte/chamber window) + brief hitmarker X on hit/kill/parry.
        Rect(new Rectangle(cx - U(1), cy - U(1), U(3), U(3)), ripostePulse ? Gold : Color.White * 0.9f);
        float hitAge = B.Time - B.PlayerHit.At;
        if (hitAge is >= 0 and < 0.16f)
        {
            var ht = B.PlayerHit.Text;
            bool isKill = B.Blows.Count > 0 && B.Blows[^1] is { OnPlayer: false, Fatal: true } fb && B.Time - fb.At < 0.18f;
            bool isSpecial = ht.StartsWith("PARRY") || ht.StartsWith("CHAMBER") || ht.Contains("GUARD BREAK");
            bool isDmg = ht.Contains("DMG");
            if (isKill || isSpecial || isDmg)
            {
                float hf = 1f - hitAge / 0.16f;
                var hc = (isKill ? Foe : isSpecial ? Gold : Color.White) * hf;
                float g0 = U(5), g1 = U(11);
                Line(new Vector2(cx - g1, cy - g1), new Vector2(cx - g0, cy - g0), hc, U(2));
                Line(new Vector2(cx + g1, cy - g1), new Vector2(cx + g0, cy - g0), hc, U(2));
                Line(new Vector2(cx - g1, cy + g1), new Vector2(cx - g0, cy + g0), hc, U(2));
                Line(new Vector2(cx + g1, cy + g1), new Vector2(cx + g0, cy + g0), hc, U(2));
            }
        }

        var incoming = new bool[4];
        bool foeNear = B.Options.Duel;
        foreach (var u in B.Units)
        {
            if (!u.Active || u.Team == p.Team || u.Routing) continue;
            float d2 = Vector2.DistanceSquared(u.Pos, p.Pos);
            if (d2 < 36f) foeNear = true;
            if (d2 < 20f && u.Action is UnitAction.Windup or UnitAction.Active && (u.Target == p || d2 < 9f))
                incoming[(int)Dirs.Mirror(u.Swing)] = true;
        }

        float rad = U(21), len = U(8);
        var activeDir = p.Action == UnitAction.Block ? p.GuardDir : p.Charging || p.Action is UnitAction.Windup or UnitAction.Active ? p.Swing : Aim;
        for (int i = 0; i < 4; i++)
        {
            var d = (AttackDir)i;
            var dir = d switch { AttackDir.Overhead => -Vector2.UnitY, AttackDir.Thrust => Vector2.UnitY, AttackDir.Left => -Vector2.UnitX, _ => Vector2.UnitX };
            var side = new Vector2(-dir.Y, dir.X);
            bool isInc = incoming[i];
            bool isMine = d == activeDir;
            float extra = isInc ? U(2) * pulse : isMine ? U(1) : 0;
            var tip = new Vector2(cx, cy) + dir * (rad + len + extra);
            var col = ripostePulse && isMine ? Color.Lerp(Gold, Color.White, pulse * 0.55f)
                : isInc ? Color.Lerp(Foe, new Color(255, 110, 90), pulse)
                : isMine ? (p.Action == UnitAction.Block ? Kit.Gilt : Gold)
                : Color.White * 0.28f;
            float w = isInc ? U(3.5f) : isMine ? U(3f) : U(1.8f);
            // Dark outline under active/incoming chevrons so they pop over bright sky or snow.
            if (isInc || isMine)
            {
                Line(tip, tip - dir * len + side * len, Color.Black * 0.65f, w + U(1.6f));
                Line(tip, tip - dir * len - side * len, Color.Black * 0.65f, w + U(1.6f));
            }
            Line(tip, tip - dir * len + side * len, col, w);
            Line(tip, tip - dir * len - side * len, col, w);
        }

        // Melee swing charge bar + compact Stamina / Posture bar beneath the reticle.
        int barY = cy + U(34);
        if (p.Charging && p.Charge > 0.02f)
        {
            int bw = U(52), bh = U(3), bx = cx - bw / 2;
            Rect(new Rectangle(bx - U(1), barY - U(1), bw + U(2), bh + U(2)), Color.Black * 0.65f);
            Bar(new Rectangle(bx, barY, bw, bh), Math.Clamp(p.Charge, 0f, 1f), p.Charge >= 0.85f ? Gold : Color.White * 0.9f);
            barY += U(6);
        }
        if (foeNear || p.Stamina < 99.5f)
        {
            int bw = U(52), bh = U(4), bx = cx - bw / 2;
            float frac = Math.Clamp(p.Stamina / 100f, 0f, 1f);
            var barCol = frac < 0.25f ? Bad : frac < 0.5f ? Warn : Ok;
            Rect(new Rectangle(bx - U(1), barY - U(1), bw + U(2), bh + U(2)), Color.Black * 0.65f);
            Bar(new Rectangle(bx, barY, bw, bh), frac, barCol);
        }
    }

    /// Dynamic ranged reticle: 4-tick spread cross that tightens as you draw, glows gold in the bow sweet spot,
    /// includes a subtle drop tick for long shots, and displays crossbow/bow reload state.
    void RangedReticle(int cx, int cy)
    {
        var p = B.Player;
        var kind = Look.GripOf(p.Type.Look.Weapon);
        bool isCrossbow = kind == Grip.Crossbow;
        bool sweetBow = !isCrossbow && p.Charging && p.Charge is >= 0.78f and <= 0.96f;
        bool reloading = p.Cooldown > 0.05f;
        bool empty = p.Ammo <= 0;

        float spreadGap = empty ? U(20)
            : reloading ? U(22)
            : p.Charging ? MathHelper.Lerp(isCrossbow ? U(11) : U(26), isCrossbow ? U(4) : U(5), p.Charge)
            : (isCrossbow ? U(11) : U(22));
        float tickLen = U(8);
        var col = empty ? Bad : reloading ? Dim : sweetBow ? Gold : p.Charging ? Color.Lerp(Color.White, Gold, p.Charge * 0.7f) : Color.White * 0.85f;
        float w = sweetBow ? U(2.6f) : U(2f);

        // Centre dot + subtle long-range drop tick below centre.
        Rect(new Rectangle(cx - U(1), cy - U(1), U(3), U(3)), col);
        Rect(new Rectangle(cx - U(2), cy + U(14), U(5), U(1)), Color.White * 0.45f);

        Vector2[] dirs = { -Vector2.UnitY, Vector2.UnitY, -Vector2.UnitX, Vector2.UnitX };
        foreach (var d in dirs)
        {
            var a = new Vector2(cx, cy) + d * spreadGap;
            var b = new Vector2(cx, cy) + d * (spreadGap + tickLen);
            Line(a, b, Color.Black * 0.65f, w + U(1.5f));
            Line(a, b, col, w);
        }

        int bw = U(52), bh = U(4), bx = cx - bw / 2, by = cy + U(34);
        if (empty)
            Shadowed("NO AMMO", cx, by - U(2), Bad, 1, 1);
        else if (reloading)
        {
            float maxCd = isCrossbow ? MathF.Max(0.9f, 1.35f * p.Type.Reload) : MathF.Max(0.3f, 0.30f + (p.Type.Reload - 1f) * 1.1f);
            float prog = 1f - Math.Clamp(p.Cooldown / maxCd, 0f, 1f);
            Rect(new Rectangle(bx - U(1), by - U(1), bw + U(2), bh + U(2)), Color.Black * 0.65f);
            Bar(new Rectangle(bx, by, bw, bh), prog, Dim);
            Shadowed(isCrossbow ? "SPANNING" : "RELOAD", cx, by + U(7), Dim, 1, 1);
        }
        else if (p.Charging)
        {
            Rect(new Rectangle(bx - U(1), by - U(1), bw + U(2), bh + U(2)), Color.Black * 0.65f);
            Bar(new Rectangle(bx, by, bw, bh), p.Charge, sweetBow ? Gold : Color.White * 0.9f);
            if (!isCrossbow)
            {
                // Sweet-spot bracket markers at 78% and 96% draw tension.
                Rect(new Rectangle(bx + (int)(bw * 0.78f), by - U(2), U(1), bh + U(4)), Gold);
                Rect(new Rectangle(bx + (int)(bw * 0.96f), by - U(2), U(1), bh + U(4)), Gold);
            }
        }
    }

    /// What he is doing, in a crisp word you can act on (direction is shown on the 4-chevron reticle; CR6).
    (string Text, Color Col) Intent(Unit u)
    {
        if (u.Action == UnitAction.Stagger) return (u.GuardBroken ? "GUARD BROKEN!" : "STAGGERED", Gold);
        if (u.Action is UnitAction.Windup or UnitAction.Active && u.Target == B.Player) return (u.Action == UnitAction.Active ? "STRIKING" : "WINDING UP", Foe);
        if (u.Action == UnitAction.Block) return (u.GuardDir switch { AttackDir.Overhead => "GUARD HIGH", AttackDir.Thrust => "GUARD LOW", AttackDir.Left => "GUARD LEFT", _ => "GUARD RIGHT" }, Dim);
        if (u.Action == UnitAction.Recover) return ("OPEN", Ok);
        if (u.Stamina < 25) return ("SPENT", Ok);
        return ("", Dim);
    }

    /// Over the foe you are fighting: his name, his health and what he is doing.
    void OpponentMark()
    {
        if (!B.Player.Active || Opponent() is not Unit u) return;
        var vp = new Viewport(0, 0, W, H);
        var v = vp.Project(new Vector3(u.Pos.X, B.Terrain.Height(u.Pos) + (u.Mounted ? 3.6f : 2.6f), u.Pos.Y), Proj, View, Matrix.Identity);
        if (v.Z is <= 0 or >= 1) return;
        int bw = U(64), x = (int)Math.Clamp(v.X, bw / 2 + U(4), W - bw / 2 - U(4)), y = (int)Math.Clamp(v.Y, U(70), H - U(140));
        bool isLocked = u == LockTarget;
        string name = Battle.NameOf(u) is { Length: > 0 } n ? n : u.Type.Name;
        Shadowed(isLocked ? $"[ {name.ToUpperInvariant()} ]" : name.ToUpperInvariant(), x, y - U(12), isLocked || u.IsLeader || u.Hero ? Gold : Color.White, 1, 1);
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
        int myAlive = B.Units.Count(u => u.Active && u.Team == 0);
        int foeAlive = B.Units.Count(u => u.Active && u.Team == 1);
        string title = B.Units.Count <= 2 ? "SINGLE COMBAT" : $"DUEL  {myAlive}V{foeAlive}";
        TextC(title, W / 2f, y - U(12), Gold, 1);
        Fighter(mine, B.Player, false);
        var foe = Opponent() ?? (B.EnemyCaptain.Active ? B.EnemyCaptain : B.Units.FirstOrDefault(u => u.Active && u.Team == 1) ?? B.EnemyCaptain);
        Fighter(his, foe, true);
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
