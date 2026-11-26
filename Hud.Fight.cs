using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud, the fight view (docs/archive/PLAYTEST.md PT4, "As rebuilt"): you are the captain, with a reticle and no cursor. Bottom left
/// your vitals as bare bars; bottom right the arms that listen to you (1 foot, 2 bows, 3 horse, 4 mages, 0 all); a thin
/// balance bar and the battle's speed at the top (the command view has them at the bottom); the order ring while its key is
/// held. No cards, no map, no buttons: those are the command view's.
sealed partial class Hud
{
    public static readonly string[] ArmNames = { "FOOT", "BOWS", "HORSE", "MAGES" };
    static readonly string[] ArmIcons = { "shield", "arrow", "horse", "star" };

    /// The arm a company answers with in the fight view: 0 foot (spears too), 1 bows and other shot, 2 horse, 3 mages.
    public static int ArmOf(Squad s) => Battle.CasterCompany(s) ? 3 : s.Cavalry ? 2
        : s.Units.Count > 0 && s.Units[0].Type.Class == UnitClass.Archer ? 1 : 0;

    readonly List<(Rectangle R, int Arm)> armChips = new();

    void LayoutFight()
    {
        player = new Rectangle(U(12), H - U(62), U(300), U(52));
        int cw = U(86), ch = U(38), x = W - U(12);
        for (int arm = 3; arm >= 0; arm--)
        {
            if (!B.Commanded.Any(s => s.Alive > 0 && s.Works == null && ArmOf(s) == arm)) continue;
            x -= cw;
            armChips.Add((new Rectangle(x, H - ch - U(12), cw, ch), arm));
            x -= U(4);
        }
    }

    void ShadowL(string s, float x, float y, Color c, int scale)
    {
        int o = Math.Max(1, U(1));
        Text(s, x + o, y + o, Color.Black * 0.8f, scale);
        Text(s, x, y, c, scale);
    }
    void ShadowR(string s, float rx, float y, Color c, int scale) => ShadowL(s, rx - TW(s, scale), y, c, scale);

    /// Who an order goes to, in a word or two: the arms picked whole, else a count.
    public string ListenWord()
    {
        if (Selected.Count == 0) return "ALL COMPANIES";
        var arms = new List<string>();
        int inArms = 0;
        for (int arm = 0; arm < 4; arm++)
        {
            int n = 0, picked = 0;
            foreach (var s in B.Commanded) if (s.Alive > 0 && s.Works == null && ArmOf(s) == arm) { n++; if (Selected.Contains(s)) picked++; }
            if (n > 0 && picked == n) { arms.Add(ArmNames[arm]); inArms += n; }
        }
        int live = Selected.Count(s => s.Alive > 0);
        return arms.Count > 0 && inArms == live ? string.Join(" + ", arms) : live == 1 ? Selected.First(s => s.Alive > 0).Name.ToUpperInvariant() : $"{live} COMPANIES";
    }

    /// Top centre: the two sides on one thin bar, with the men standing, and under it the battle's speed: the five steps
    /// as rising bars (keys [ and ]), a pause mark when paused.
    void FightTop()
    {
        if (B.Options.Duel) return;
        var (f, mine, foes) = Tally();
        int bw = U(200), x = (W - bw) / 2, y = U(12);
        int lw = TW($"{mine}", 1) + U(12), rw = TW($"{foes}", 1) + U(12);
        var cap = new Rectangle(x - lw, y - U(5), bw + lw + rw, U(15));
        Rect(cap, new Color(12, 14, 20) * 0.72f);
        Frame(cap, Kit.Gilt * 0.35f, U(1));
        Rect(new Rectangle(x - U(1), y - U(1), bw + U(2), U(7)), Color.Black * 0.6f);
        kit.TugBar(new Rectangle(x, y, bw, U(5)), f, SideArms(0).Ui, Foe);
        ShadowR($"{mine}", x - U(6), y - U(1), SideArms(0).Ui, 1);
        ShadowL($"{foes}", x + bw + U(6), y - U(1), Foe, 1);
        if (Deploying) return;
        string word = Paused ? "PAUSED" : Speed != 1 ? $"X{Speed:0.##}" : "SPEED";
        int sx = W / 2 - (5 * U(6) + U(6) + TW(word, 1)) / 2, sy = y + U(10);
        var shade = new Rectangle(sx - U(4), sy - U(2), 5 * U(6) + U(14) + TW(word, 1), U(20));
        Rect(shade, Color.Black * 0.5f);
        for (int i = 0; i < 5; i++)
        {
            int h = U(4 + 3 * i);
            Rect(new Rectangle(sx + i * U(6), sy + U(16) - h, U(4), h), i <= SpeedIdx ? (Paused ? Dim : Gold) : Color.White * 0.18f);
        }
        ShadowL(word, sx + 5 * U(6) + U(6), sy + U(6), Paused ? Gold : Speed != 1 ? Warn : Dim, 1);
    }

    /// Bottom left: health, breath, aura and horse as bare bars, what is in your hands, the rally and your kills.
    void Vitals()
    {
        var p = B.Player;
        if (!p.Active) return;
        int x = player.X, y = player.Y, bx = x + U(42), bw = U(170);
        Rect(new Rectangle(x - U(6), y - U(6), player.Width + U(12), player.Height + U(10)), Color.Black * 0.62f); // a shade, so the bars read over men and grass
        kit.Shield(new Rectangle(x, y + U(1), U(24), U(29)), SideArms(0));
        void Row(string icon, float frac, Color c, int h)
        {
            Rect(new Rectangle(bx - U(1), y - U(1), bw + U(2), h + U(2)), Color.Black * 0.55f);
            kit.Gauge(new Rectangle(bx, y, bw, h), Math.Clamp(frac, 0, 1), c);
            kit.Icon(icon, bx - U(11), y + (h - U(7)) / 2, c, 1);
            y += h + U(4);
        }
        Row("heart", p.Hp / p.Type.MaxHp, Bad, U(9));
        Row("boot", p.Stamina / 100, B.Fevered ? Bad : Ok, U(5)); // a curse on you shows in red (MA9)
        if (B.AuraTier > 0) Row("star", B.Aura / Battle.AuraMax, B.Quenched ? Bad : B.Spent ? Dim : Style.Aura, U(5));
        if (p.Mounted) Row("horse", p.HorseHp / p.Horse!.Hp, HorseBright, U(5));
        // What is in your hands (P7), the swap key when a second weapon waits, your shots (P1).
        string hands = p.Couched ? "LANCE COUCHED" : B.PlayerWeapon.ToUpperInvariant() + (B.HasSecondWeapon ? $" ({Ctl.Name(Act.SwapWeapon)})" : "");
        ShadowL(hands, bx, y, p.Couched ? Gold : Color.White, 1);
        if (p.Type.Ammo > 0) ShadowR($"{p.Ammo} SHOTS", bx + bw, y, p.Ammo > 0 ? Gold : Bad, 1);
        int rx = bx + bw + U(13), ry = player.Y;
        bool ready = B.RallyCooldown <= 0;
        kit.Icon("flag", rx, ry, ready ? Gold : Dim, 1);
        ShadowL(ready ? $"RALLY {Ctl.Name(Act.Rally)}" : $"RALLY IN {B.RallyCooldown:0}", rx + U(11), ry, ready ? Gold : Dim, 1);
        kit.Icon("skull", rx, ry + U(12), Dim, 1);
        ShadowL($"{B.PlayerKills} KILLS", rx + U(11), ry + U(12), Color.White, 1);
        if (B.AuraTier > 0 && (B.AuraBlade ? "BLADE" : B.Quenched ? "QUENCHED" : B.Spent ? "SPENT" : B.Surge >= 1 ? $"SURGE {B.Surge:0}" : "") is { Length: > 0 } aura)
            ShadowL(aura, rx, ry + U(24), B.Quenched ? Bad : B.Spent ? Dim : Style.Aura, 1);
    }

    /// The swing (P2) under the reticle: a riposte window open, a chain of blows under way, the Black Flame on you.
    void SwingWords()
    {
        var p = B.Player;
        if (!p.Active) return;
        if (B.Mad) TextC("THE FLAME HAS YOU", W / 2f, H / 2f + U(36), Bad, 2);
        else if (B.Riposting) TextC("RIPOSTE!", W / 2f, H / 2f + U(36), Gold, 2);
        else if (B.Chain >= 1 && p.Action is UnitAction.Windup or UnitAction.Active or UnitAction.Recover) TextC($"COMBO X{B.Chain + 1}", W / 2f, H / 2f + U(42), Gold, 1);
    }

    /// Bottom right: your arms. A number key picks who listens (gold); with none picked all of them do. Each chip shows the
    /// arm's picture, its men, its strength and what it was last told.
    void ArmChips()
    {
        if (armChips.Count == 0 || Deploying) return;
        bool all = Selected.Count == 0;
        var cloth = SideArms(0).Ui;
        foreach (var (r, arm) in armChips)
        {
            int men = 0, began = 0, n = 0, picked = 0;
            Squad? first = null;
            foreach (var s in B.Commanded)
            {
                if (s.Alive == 0 || s.Works != null || ArmOf(s) != arm) continue;
                n++; men += s.Alive; began += s.StartCount; first ??= s;
                if (Selected.Contains(s)) picked++;
            }
            bool whole = !all && picked == n, some = picked > 0, on = all || some;
            Rect(r, whole ? Kit.Mul(Gold, 0.3f) * 0.85f : Color.Black * (on ? 0.6f : 0.4f)); // the chosen arm glows gold
            Frame(r, whole ? Gold : some ? Kit.Mul(Gold, 0.6f) : Color.White * (all ? 0.3f : 0.1f), U(whole ? 3 : 1));
            if (whole) Rect(new Rectangle(r.X + U(6), r.Y - U(5), r.Width - U(12), U(3)), Gold);
            Rect(new Rectangle(r.X + U(3), r.Y + U(3), U(11), U(12)), whole ? Gold : Color.Black * 0.6f);
            Text($"{arm + 1}", r.X + U(5), r.Y + U(6), whole ? Kit.IronDark : on ? Color.White : Dim, 1);
            kit.Icon(ArmIcons[arm], r.X + U(18), r.Y + U(3), on ? cloth : Kit.Mul(cloth, 0.6f), 2);
            TextR($"{men}", r.Right - U(4), r.Y + U(3), on ? Color.White : Style.Text * 0.75f);
            if (first != null) { var (oi, oc) = OrderMark(first); kit.Icon(oi, r.X + U(4), r.Y + U(20), on ? oc : Dim, 1); }
            Text(ArmNames[arm], r.X + U(15), r.Y + U(21), on ? Style.Text : Style.Text * 0.6f, 1);
            TextR("MEN", r.Right - U(4), r.Y + U(21), Dim, 1);
            kit.Gauge(new Rectangle(r.X + U(3), r.Bottom - U(7), r.Width - U(6), U(4)), men / (float)Math.Max(1, began), on ? cloth : Dim);
        }
        string who = all ? "ORDERS GO TO ALL" : "ORDERS GO TO " + ListenWord() + "  (PRESS 0 FOR ALL)";
        int ww = TW(who, 1) + U(12), wx = Math.Min(armChips[0].R.Right, W - U(10)) - ww, wy = armChips[0].R.Y - U(19);
        var wp = new Rectangle(wx, wy, ww, U(13));
        Rect(wp, new Color(12, 14, 20) * 0.74f);
        Frame(wp, (all ? Color.White * 0.18f : Gold * 0.55f), U(1));
        TextR(who, wp.Right - U(6), wy + U(3), all ? Style.Text * 0.85f : Gold, 1);
    }

    /// The order you just gave, over the arms, fading: you hear the horn and read the word.
    void OrderToast()
    {
        if (string.IsNullOrEmpty(CombatPrompt)) return;
        float age = Ironfield.Ui.Now - CombatPromptAt;
        if (age is < 0 or > 2.6f) return;
        float a = Math.Clamp((2.6f - age) / 0.6f, 0, 1);
        int y = (armChips.Count > 0 ? armChips[0].R.Y : H - U(20)) - U(44);
        string s = kit.Fit(CombatPrompt, U(460), 2);
        int tw = TW(s, 2), o = Math.Max(1, U(1)), x = W - U(14) - tw;
        Rect(new Rectangle(x - U(8), y - U(3), tw + U(16), U(20)), new Color(10, 12, 18) * (0.62f * a));
        Text(s, x + o, y + o, Color.Black * (0.85f * a), 2);
        Text(s, x, y, Color.Lerp(Color.Transparent, Gold, a), 2);
    }

    void FightHints()
    {
        string s = $"{Ctl.Name(Act.CommandView)} COMMAND   {Ctl.Name(Act.OrderWheel)} ORDERS   {Ctl.Name(Act.Help)} KEYS";
        Rect(new Rectangle(W - U(16) - TW(s, 1), U(5), TW(s, 1) + U(12), U(16)), Color.Black * 0.5f); // a plate: the sky is pale
        Text(s, W - U(10) - TW(s, 1), U(10), Color.White, 1);
    }

    /// The order ring (hold its key in the fight view; time slows): eight pictures round the reticle, the one the mouse
    /// leans toward in gold with its name in the middle. Let go to give it.
    void Wheel()
    {
        Rect(new Rectangle(0, 0, W, H), Color.Black * 0.3f);
        var c = WheelPos();
        var order = WheelOrder;
        for (int i = 0; i < order.Length; i++)
        {
            float a = -MathHelper.PiOver2 + i * MathHelper.TwoPi / order.Length;
            bool on = i == WheelSector;
            var at = c + new Vector2(MathF.Cos(a) * 1.25f, MathF.Sin(a)) * U(on ? 118 : 110);
            int tw = U(on ? 66 : 58), ts = U(on ? 50 : 44);
            var r = new Rectangle((int)at.X - tw / 2, (int)at.Y - ts / 2, tw, ts);
            var info = Info[order[i]];
            Rect(new Rectangle(r.X + U(2), r.Y + U(3), r.Width, r.Height), Color.Black * 0.45f);
            kit.VGrad(r, on ? new Color(104, 84, 36) : new Color(38, 40, 46), on ? new Color(54, 44, 20) : Kit.IronDark, 1);
            Frame(r, on ? Gold : Color.White * 0.22f, U(on ? 2 : 1));
            kit.Icon(info.Icon, r.Center.X - U(10), r.Y + (on ? U(8) : U(5)), on ? Gold : Style.Text, 3);
            TextC(info.Short, r.Center.X, r.Bottom - U(12), on ? Gold : Color.White, 1);
            if (info.Key is Act k && Ctl.Name(k) is { Length: <= 2 } kn)
            {
                if (on) { var kb = new Rectangle(r.Right - TW(kn, 1) - U(6), r.Y + U(2), TW(kn, 1) + U(4), U(10)); Rect(kb, Gold); TextR(kn, r.Right - U(4), r.Y + U(3), Kit.IronDark, 1); }
                else TextR(kn, r.Right - U(3), r.Y + U(3), Style.Text * 0.85f, 1);
            }
        }
        var mid = new Rectangle((int)c.X - U(74), (int)c.Y - U(30), U(148), U(44));
        Rect(mid, Color.Black * 0.6f);
        if (WheelSector >= 0 && WheelSector < order.Length)
        {
            var info = Info[order[WheelSector]];
            Shadowed(info.Name, c.X, c.Y - U(24), Gold, 2, 1);
        }
        else Shadowed("ORDERS", c.X, c.Y - U(24), Color.White, 2, 1);
        Shadowed("TO " + ListenWord(), c.X, c.Y - U(2), Style.Text, 1, 1);
    }

    // ---------- the captain's ring ----------

    /// What the captain's ring holds: the things only he does, kept off the keyboard (each keeps its own key as a shortcut).
    public enum CapCmd { Swap, Form1, Form2, Form3, Great, Relic, Mount, Couch }
    /// Round the ring from the top, clockwise.
    public static readonly CapCmd[] CaptainRing = { CapCmd.Swap, CapCmd.Form1, CapCmd.Form2, CapCmd.Form3, CapCmd.Great, CapCmd.Relic, CapCmd.Mount, CapCmd.Couch };

    /// One slot of the captain's ring as it stands now: its word, picture, a line under it, whether it can be done, and its key.
    (string Name, string Icon, string Sub, bool Ready, Act Key) CapSlot(CapCmd c)
    {
        var p = B.Player;
        switch (c)
        {
            case CapCmd.Swap:
                return ("SWAP", "sword", B.HasSecondWeapon ? "WEAPON" : "ONLY ONE", B.HasSecondWeapon, Act.SwapWeapon);
            case CapCmd.Form1 or CapCmd.Form2 or CapCmd.Form3 or CapCmd.Great:
            {
                int i = (int)c - (int)CapCmd.Form1;
                var key = i switch { 0 => Act.Form1, 1 => Act.Form2, 2 => Act.Form3, _ => Act.GreatForm };
                var f = i < B.FormSlot.Length ? B.FormSlot[i] : null;
                if (f == null) return (i == 3 ? "GREAT FORM" : $"FORM {i + 1}", i == 3 ? "sun" : "star", "NONE SET", false, key);
                bool ready = B.CannotForm(i) == null;
                string sub = i == 3 && B.GreatUsed ? "SPENT" : B.FormWait(i) > 0 ? $"IN {B.FormWait(i):0}" : $"AURA {B.FormCost(f):0}";
                return (f.Name.ToUpperInvariant(), i == 3 ? "sun" : "star", sub, ready, key);
            }
            case CapCmd.Relic:
                return ("RELIC", "cross", !B.HasRelic ? "NONE" : B.CannotRaise(p) == null ? "RAISE IT" : "RAISED", B.HasRelic && B.CannotRaise(p) == null, Act.Relic);
            case CapCmd.Mount:
                return (p.Mounted ? "DISMOUNT" : "MOUNT", "horse", p.Mounted ? "SLOW FIRST" : "HORSE NEAR", p.Mounted ? MathF.Abs(p.Speed) <= 1.5f : true, Act.Interact);
            default:
                return (p.Couched ? "LANCE UP" : "COUCH", "spear", Battle.CanCouch(p) ? "AT GALLOP" : "NEED LANCE", Battle.CanCouch(p) && (p.Couched || p.Speed > 6), Act.Couch);
        }
    }

    /// The captain's ring (hold its key in the fight view; time slows): his weapons, forms, the relic and his horse round the
    /// reticle, greyed when he cannot do them now. Let go over one to do it.
    void CaptainWheel()
    {
        Rect(new Rectangle(0, 0, W, H), Color.Black * 0.3f);
        var c = WheelPos();
        var ring = CaptainRing;
        for (int i = 0; i < ring.Length; i++)
        {
            var (name, icon, sub, ready, _) = CapSlot(ring[i]);
            float a = -MathHelper.PiOver2 + i * MathHelper.TwoPi / ring.Length;
            bool on = i == WheelSector;
            var at = c + new Vector2(MathF.Cos(a) * 1.3f, MathF.Sin(a)) * U(on ? 122 : 114);
            int tw = U(on ? 82 : 74), ts = U(on ? 58 : 52);
            var r = new Rectangle((int)at.X - tw / 2, (int)at.Y - ts / 2, tw, ts);
            var lit = ready ? (on ? Gold : Style.Aura) : Dim * 0.55f;
            Rect(new Rectangle(r.X + U(2), r.Y + U(3), r.Width, r.Height), Color.Black * (ready ? 0.45f : 0.25f));
            kit.VGrad(r, on && ready ? new Color(104, 84, 36) : ready ? new Color(40, 42, 48) : new Color(22, 24, 28) * 0.82f, on && ready ? new Color(54, 44, 20) : ready ? Kit.IronDark : new Color(14, 15, 18) * 0.82f, 1);
            Frame(r, on ? (ready ? Gold : Dim * 0.6f) : Color.White * (ready ? 0.26f : 0.1f), U(on ? 2 : 1));
            kit.Icon(icon, r.Center.X - U(10), r.Y + U(5), lit, 3);
            TextC(kit.Fit(name, r.Width - U(6)), r.Center.X, r.Bottom - U(22), ready ? (on ? Gold : Color.White) : Dim * 0.62f, 1);
            TextC(kit.Fit(sub, r.Width - U(6)), r.Center.X, r.Bottom - U(12), ready ? Style.Text * 0.8f : Dim * 0.5f, 1);
        }
        var mid = new Rectangle((int)c.X - U(74), (int)c.Y - U(30), U(148), U(44));
        Rect(mid, Color.Black * 0.6f);
        if (WheelSector >= 0 && WheelSector < ring.Length)
        {
            var (name, _, sub, ready, key) = CapSlot(ring[WheelSector]);
            Shadowed(kit.Fit(name, U(144), 2), c.X, c.Y - U(24), ready ? Gold : Dim, 2, 1);
            Shadowed(ready ? $"KEY {Ctl.Name(key)}" : sub, c.X, c.Y - U(2), Style.Text, 1, 1);
        }
        else
        {
            Shadowed("CAPTAIN", c.X, c.Y - U(24), Color.White, 2, 1);
            Shadowed("LEAN TO ONE, LET GO", c.X, c.Y - U(2), Style.Text, 1, 1);
        }
    }
}
