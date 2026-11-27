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
    readonly List<(Unit U, float DistSq)> nearbyEnemies = new();

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

    /// Top centre: friendly and enemy heraldic shields flanking live headcounts and a forged-brass-framed tug bar (Pillar 1 P1.2).
    void FightTop()
    {
        if (B.Options.Duel) return;
        var (f, mine, foes) = Tally();
        int bw = U(210), x = (W - bw) / 2, y = U(15);
        int sw = U(22), sh = U(26);
        int lw = TW($"{mine}", 1) + sw + U(16), rw = TW($"{foes}", 1) + sw + U(16);
        var cap = new Rectangle(x - lw, y - U(8), bw + lw + rw, U(26));
        var chrome = UiChrome.For(sb.GraphicsDevice);
        int cb = Math.Max(4, U(6));
        chrome.Draw9(sb, cap, UiChrome.PlateFill, 10, cb, Color.White * 0.88f);
        chrome.Draw9(sb, cap, UiChrome.PlateFrame, 10, cb, Kit.Gilt * 0.85f);

        kit.Shield(new Rectangle(cap.X + U(5), cap.Y + (cap.Height - sh) / 2, sw, sh), SideArms(0));
        kit.Shield(new Rectangle(cap.Right - sw - U(5), cap.Y + (cap.Height - sh) / 2, sw, sh), SideArms(1));
        ShadowR($"{mine}", x - U(7), y + U(1), Kit.Parchment, 1);
        ShadowL($"{foes}", x + bw + U(7), y + U(1), Kit.Parchment, 1);

        var barFrame = new Rectangle(x - U(2), y, bw + U(4), U(10));
        Rect(barFrame, Color.Black * 0.72f);
        kit.TugBar(new Rectangle(x, y + U(2), bw, U(6)), f, SideArms(0).Ui, Foe);
        Frame(barFrame, Kit.Gilt * 0.75f, U(1));

        if (Deploying || (!Paused && Speed == 1 && !FieldCommand)) return;
        string word = Paused ? "PAUSED" : Speed != 1 ? $"X{Speed:0.##}" : "COMMAND MODE";
        int sx = W / 2 - (5 * U(6) + U(6) + TW(word, 1)) / 2, sy = cap.Bottom + U(3);
        var shade = new Rectangle(sx - U(5), sy - U(1), 5 * U(6) + U(16) + TW(word, 1), U(18));
        Rect(shade, new Color(12, 14, 18) * 0.72f);
        Frame(shade, Kit.GiltDark * 0.6f, U(1));
        for (int i = 0; i < 5; i++)
        {
            int h = U(4 + 2 * i);
            Rect(new Rectangle(sx + i * U(6), sy + U(14) - h, U(4), h), i <= SpeedIdx ? (Paused ? Dim : Gold) : Color.White * 0.18f);
        }
        ShadowL(word, sx + 5 * U(6) + U(6), sy + U(4), Paused ? Gold : Speed != 1 ? Warn : Gold, 1);
    }

    /// Bottom left: minimal player vitals (HP, stamina, aura, horse) with no heavy box chrome during combat (Pillar 1 P1.2).
    void Vitals()
    {
        var p = B.Player;
        if (!p.Active) return;
        int x = player.X, y = player.Y + U(6), bx = x + U(14), bw = U(164);
        void Row(string icon, float frac, Color c, int h)
        {
            Rect(new Rectangle(bx - U(1), y - U(1), bw + U(2), h + U(2)), Color.Black * 0.72f);
            kit.Gauge(new Rectangle(bx, y, bw, h), Math.Clamp(frac, 0, 1), c);
            kit.Icon(icon, bx - U(12), y + (h - U(7)) / 2, c, 1);
            y += h + U(4);
        }
        Row("heart", p.Hp / p.Type.MaxHp, Bad, U(8));
        Row("boot", p.Stamina / 100, B.Fevered ? Bad : Ok, U(5));
        if (B.AuraTier > 0) Row("star", B.Aura / Battle.AuraMax, B.Quenched ? Bad : B.Spent ? Dim : Style.Aura, U(5));
        if (p.Mounted) Row("horse", p.HorseHp / p.Horse!.Hp, HorseBright, U(5));
        bool lanceReady = Battle.CanCouch(p) && !p.Couched && p.Speed > 6;
        string hands = p.Couched ? "LANCE COUCHED"
            : lanceReady ? $"{B.PlayerWeapon.ToUpperInvariant()}  [{Ctl.Name(Act.Couch)} COUCH]"
            : B.PlayerWeapon.ToUpperInvariant() + (B.HasSecondWeapon ? $" ({Ctl.Name(Act.SwapWeapon)})" : "");
        ShadowL(hands, bx, y + U(1), p.Couched || lanceReady ? Gold : Color.White * 0.92f, 1);
        if (p.Type.Ammo > 0) ShadowR($"{p.Ammo}", bx + bw, y + U(1), p.Ammo > 0 ? Gold : Bad, 1);
        int rx = bx + bw + U(10), ry = player.Y + U(6);
        bool ready = B.RallyCooldown <= 0;
        kit.Icon("flag", rx, ry, ready ? Gold : Dim, 1);
        ShadowL(ready ? $"{Ctl.Name(Act.Rally)}" : $"{B.RallyCooldown:0}S", rx + U(10), ry, ready ? Gold : Dim, 1);
        kit.Icon("skull", rx, ry + U(12), Dim, 1);
        ShadowL($"{B.PlayerKills}", rx + U(10), ry + U(12), Color.White * 0.9f, 1);
        if (B.AuraTier > 0 && (B.AuraBlade ? "BLADE" : B.Quenched ? "QUENCHED" : B.Spent ? "SPENT" : B.Surge >= 1 ? $"SURGE {B.Surge:0}" : "") is { Length: > 0 } aura)
            ShadowL(aura, rx, ry + U(24), B.Quenched ? Bad : B.Spent ? Dim : Style.Aura, 1);
    }

    /// Subtle mid-left control hint tag in default combat (hideable in Settings).
    void ControlHints()
    {
        if (FieldCommand || Deploying || Settings.Current.HideControlHints) return;
        int x = U(12), y = H / 2 - U(18);
        void Tag(string key, string label, int ty)
        {
            string s = $"[{key}] {label}";
            int tw = TW(s, 1) + U(10);
            var r = new Rectangle(x, ty, tw, U(14));
            Rect(r, new Color(12, 14, 18) * 0.52f);
            Frame(r, Kit.GiltDark * 0.45f, U(1));
            Text(s, r.X + U(5), r.Y + U(4), Style.Text * 0.85f, 1);
        }
        Tag(Ctl.First(Act.FieldCommand), "COMMAND MODE", y);
        Tag(Ctl.First(Act.InfoMode), "INFO MODE", y + U(17));
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

    /// Contextual nearby-only enemy health bars in Field View (Pillar 3 P3.3):
    /// slim crimson bars above enemies within 10 m that are wounded or within 4.5 m in the player's aim cone (capped at 6).
    void DrawNearbyEnemyBars()
    {
        var p = B.Player;
        if (!p.Active || Deploying) return;
        nearbyEnemies.Clear();
        var fwd = new Vector2(MathF.Cos(CamYaw), MathF.Sin(CamYaw));
        const float maxR2 = 10f * 10f;
        const float coneR2 = 4.5f * 4.5f;
        foreach (var u in B.Units)
        {
            if (u.Team != 1 || !u.Active || u.IsPlayer) continue;
            var d = u.Pos - p.Pos;
            float d2 = d.LengthSquared();
            if (d2 > maxR2) continue;
            bool damaged = u.Hp < u.Type.MaxHp - 0.5f;
            bool inCone = d2 <= coneR2 && (d2 < 0.04f || Vector2.Dot(d / MathF.Sqrt(d2), fwd) > 0.65f);
            if (!damaged && !inCone) continue;
            nearbyEnemies.Add((u, d2));
        }
        if (nearbyEnemies.Count == 0) return;
        nearbyEnemies.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
        int count = Math.Min(6, nearbyEnemies.Count);
        var vp = new Viewport(0, 0, W, H);
        for (int i = 0; i < count; i++)
        {
            var u = nearbyEnemies[i].U;
            float headY = (u.Mounted ? 2.78f : 2.12f) * u.Type.Size;
            var sp = vp.Project(new Vector3(u.Pos.X, B.Terrain.Height(u.Pos) + headY, u.Pos.Y), Proj, View, Matrix.Identity);
            if (sp.Z is <= 0 or >= 1) continue;
            int bw = U(u.IsLeader ? 48 : 38), bh = U(u.IsLeader ? 6 : 5);
            int bx = (int)sp.X - bw / 2, by = (int)sp.Y - bh - U(4);
            if (bx < U(12) || bx + bw > W - U(12) || by < U(40) || by + bh > H - U(24)) continue;
            float frac = Math.Clamp(u.Hp / MathF.Max(1f, u.Type.MaxHp), 0f, 1f);
            var outer = new Rectangle(bx - U(1), by - U(1), bw + U(2), bh + U(2));
            Rect(outer, new Color(14, 15, 19) * 0.94f);
            int fillW = (int)MathF.Round(bw * frac);
            if (fillW > 0) Rect(new Rectangle(bx, by, fillW, bh), new Color(204, 48, 44));
            Frame(outer, u.IsLeader ? Kit.Gilt * 0.9f : new Color(102, 108, 122) * 0.95f, U(1));
        }
    }

    /// Contextual world interaction prompts near bottom-center (Pillar 3 P3.3):
    /// loose horse mount prompt with horse HP bar, siege engine prompt, couch lance, dismount, and look-orders.
    void DrawInteractPrompt()
    {
        var p = B.Player;
        if (!p.Active || WheelOpen) return;

        string? key = null, label = null;
        float? horseHpFrac = null;

        if (B.Manned is { } me)
        {
            key = Ctl.Name(Act.Interact);
            string ready = me.Reload > 0 ? $"WINDING {100 - me.Reload / me.Def.Reload * 100:0}%" : "READY";
            label = $"LEAVE   [{Ctl.Name(Act.Attack)}] LOOSE   {me.Def.Name.ToUpperInvariant()} {ready}";
        }
        else if (!p.Mounted)
        {
            LooseHorse? bestHorse = null;
            float bestD2 = 2.8f * 2.8f;
            foreach (var lh in B.LooseHorses)
            {
                float d2 = Vector2.DistanceSquared(lh.Pos, p.Pos);
                if (d2 < bestD2) { bestD2 = d2; bestHorse = lh; }
            }
            if (bestHorse != null)
            {
                key = Ctl.Name(Act.Interact);
                label = bestHorse.Team == p.Team ? "MOUNT HORSE" : "SEIZE HORSE";
                horseHpFrac = Math.Clamp(bestHorse.Hp / MathF.Max(1f, bestHorse.Mount.Hp), 0f, 1f);
            }
            else if (B.EngineNear(p) is { } near)
            {
                key = Ctl.Name(Act.Interact);
                label = $"MAN {near.Def.Name.ToUpperInvariant()}";
            }
        }
        else if (Battle.CanCouch(p) && !p.Couched && p.Speed > 6)
        {
            key = Ctl.Name(Act.Couch);
            label = "COUCH LANCE";
        }

        if (HoldPlace >= 0.25f) { key = null; label = "LET GO: HOLD THAT GROUND"; horseHpFrac = null; }
        else if (label == null && LookFoe is { Alive: > 0 } lf) { key = Ctl.Name(Act.Charge); label = $"ATTACK {lf.Name.ToUpperInvariant()}"; }

        if (label == null) return;

        int keyW = key != null ? Math.Max(U(18), TW(key, 1) + U(10)) : 0;
        int labelW = TW(label, 1);
        int innerW = keyW + (key != null ? U(8) : 0) + labelW;
        int pw = Math.Max(U(148), innerW + U(24));
        int ph = horseHpFrac.HasValue ? U(36) : U(24);
        int px = (W - pw) / 2, py = H - U(156) - ph;
        var card = new Rectangle(px, py, pw, ph);
        var chrome = UiChrome.For(sb.GraphicsDevice);
        int cb = Math.Max(4, U(6));
        chrome.Draw9(sb, card, UiChrome.PlateFill, 10, cb, Color.White * 0.94f);
        chrome.Draw9(sb, card, UiChrome.PlateFrame, 10, cb, Kit.Gilt * 0.88f);

        int tx = px + (pw - innerW) / 2;
        int ty = py + (horseHpFrac.HasValue ? U(5) : U(5));
        if (key != null)
        {
            var kb = new Rectangle(tx, ty, keyW, U(14));
            Rect(kb, Kit.Gilt);
            Frame(kb, Kit.IronDark, U(1));
            TextC(key, kb.Center.X, kb.Y + U(4), Kit.IronDark, 1);
            tx += keyW + U(8);
        }
        ShadowL(label, tx, ty + U(4), Kit.Parchment, 1);

        if (horseHpFrac is float hf)
        {
            int ix = px + U(10), gy = py + U(23);
            Color hpCol = hf > 0.6f ? Ok : hf > 0.3f ? Warn : Bad;
            kit.Icon("horse", ix, gy - U(1), Kit.Parchment, 1);
            int gx = ix + U(13), gw = pw - U(24) - U(13);
            var trough = new Rectangle(gx - U(1), gy - U(1), gw + U(2), U(8));
            Rect(trough, new Color(22, 24, 30) * 0.92f);
            kit.Gauge(new Rectangle(gx, gy, gw, U(6)), hf, hpCol);
            Frame(trough, new Color(96, 102, 114) * 0.9f, U(1));
        }
    }

    /// Mid-right forged-brass skill-up toasts emitted live during combat (Pillar 3 P3.4).
    void DrawSkillToasts()
    {
        if (B.SkillToasts.Count == 0) return;
        const float life = 3.5f;
        int shown = 0;
        int baseY = U(46);
        var chrome = UiChrome.For(sb.GraphicsDevice);
        int cb = Math.Max(4, U(6));
        for (int i = B.SkillToasts.Count - 1; i >= 0 && shown < 3; i--)
        {
            var toast = B.SkillToasts[i];
            float age = B.Time - toast.Time;
            if (age < 0f || age > life) continue;
            float slideIn = Ease.Out(Math.Clamp(age / 0.25f, 0f, 1f));
            float alpha = Math.Clamp((life - age) / 0.6f, 0f, 1f) * slideIn;
            string title = $"{toast.Skill.ToUpperInvariant()} {toast.NewLevel}";
            string sub = "SKILL INCREASED";
            int tw = Math.Max(U(186), Math.Max(TW(title, 2), TW(sub, 1)) + U(46));
            int th = U(38);
            int slideOff = (int)((1f - slideIn) * U(42));
            int x = W - U(14) - tw + slideOff;
            int y = baseY + shown * (th + U(6));
            var r = new Rectangle(x, y, tw, th);
            chrome.Draw9(sb, r, UiChrome.PlateFill, 10, cb, Color.White * (0.95f * alpha));
            chrome.Draw9(sb, r, UiChrome.PlateFrame, 10, cb, Kit.Gilt * (0.96f * alpha));
            kit.Icon("star", r.X + U(9), r.Y + U(11), Gold * alpha, 3);
            ShadowL(title, r.X + U(34), r.Y + U(7), Gold * alpha, 2);
            ShadowL(sub, r.X + U(34), r.Y + U(24), Kit.Parchment * (0.88f * alpha), 1);
            shown++;
        }
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
            string title = order[WheelSector] == Cmd.Charge && LookFoe != null ? $"CHARGE {LookFoe.Name.ToUpperInvariant()}" : info.Name;
            Shadowed(title, c.X, c.Y - U(24), order[WheelSector] == Cmd.Charge && LookFoe != null ? Foe : Gold, title.Length > 14 ? 1 : 2, 1);
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
