using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Orders the player can give, from hotkeys, the order wheel or the command-view buttons.
public enum Cmd { Charge, Advance, FormHere, Spacing, FallBack, FireAtWill, Hold, Follow }

public enum MenuItem { Resume, Controls, NewBattle, Quit, Withdraw, Continue, Title, Settings }

/// All 2D interface: unit cards, player panel, balance of power, minimap, event feed, crosshair, order wheel,
/// command buttons, squad banners and the menu / controls / result overlays. Layout is in 720p units times `Ui`.
sealed class Hud
{
    // Colours come from the design tokens (Style.cs, docs/DESIGN.md).
    public static readonly Color Gold = Style.Accent, Friendly = Style.Friendly, Foe = Style.Foe, Ok = Style.Ok, Warn = Style.Warn,
        Bad = Style.Bad, Dim = Style.Dim, Orange = Style.Orange, HorseCol = Style.HorseBar;
    static readonly Color PanelBg = Style.Panel, Edge = Style.PanelEdge;

    public static readonly Dictionary<Cmd, (string Label, Act Key)> Orders = new()
    {
        [Cmd.Charge] = ("CHARGE", Act.Charge), [Cmd.Advance] = ("ADVANCE", Act.Advance), [Cmd.FormHere] = ("FORM ON ME", Act.FormHere),
        [Cmd.Spacing] = ("SPACING", Act.Spacing), [Cmd.FallBack] = ("FALL BACK", Act.FallBack), [Cmd.FireAtWill] = ("FIRE / HOLD", Act.FireAtWill),
        [Cmd.Hold] = ("HOLD", Act.HoldPosition), [Cmd.Follow] = ("FOLLOW ME", Act.Follow),
    };

    readonly SpriteBatch sb;
    readonly Art art;
    readonly PixelFont font;

    // Per-frame state, set by the game before Draw.
    public int Ui = 1;
    public Battle B = null!;
    public HashSet<Squad> Selected = null!;
    public Controls Ctl = null!;
    public Texture2D? Minimap;
    public bool Command, Paused, MenuOpen, HelpOpen, WheelOpen, ShowResult, InCampaign, Deploying;
    Rectangle start;
    public bool StartAt(Point p) => Deploying && start.Contains(p);
    public int WheelSector = -1;
    public AttackDir Aim;
    public Matrix View, Proj;
    public float CamYaw;
    public Vector2 CamFocus;
    public Point Mouse;
    public Rectangle? Box;

    // Layout, rebuilt every frame.
    readonly List<(Rectangle R, Squad S)> cards = new();
    readonly List<(Rectangle R, Cmd C)> buttons = new();
    readonly List<(Rectangle R, MenuItem M)> menu = new();
    Rectangle minimap, player;
    int W, H;

    public Hud(SpriteBatch sb, Art art, PixelFont font) { this.sb = sb; this.art = art; this.font = font; }

    int U(float v) => (int)(v * Ui);
    List<Squad> Mine => B.SquadsOf(0).ToList();

    // ---------- layout & hit testing ----------

    public void Layout(Viewport vp)
    {
        W = vp.Width;
        H = vp.Height;
        Ui = Settings.UiFor(H);
        cards.Clear(); buttons.Clear(); menu.Clear();

        var mine = Mine;
        int cw = U(80), ch = U(94), gap = U(6), total = mine.Count * cw + (mine.Count - 1) * gap;
        for (int i = 0; i < mine.Count; i++) cards.Add((new Rectangle((W - total) / 2 + i * (cw + gap), H - ch - U(10), cw, ch), mine[i]));

        player = new Rectangle(U(10), H - U(104), U(280), U(94));
        int ms = U(150);
        minimap = new Rectangle(W - ms - U(10), U(10), ms, ms);

        if (Command)
        {
            int bw = U(112), bh = U(24);
            var order = new[] { Cmd.Charge, Cmd.Hold, Cmd.Advance, Cmd.FallBack, Cmd.Follow, Cmd.FormHere, Cmd.Spacing, Cmd.FireAtWill };
            int x0 = W - 2 * bw - U(16), y0 = H - 4 * (bh + U(4)) - U(10);
            for (int i = 0; i < order.Length; i++)
                buttons.Add((new Rectangle(x0 + (i % 2) * (bw + U(4)), y0 + (i / 2) * (bh + U(4)), bw, bh), order[i]));
        }

        var items = ShowResult ? (InCampaign ? new[] { MenuItem.Continue } : new[] { MenuItem.NewBattle, MenuItem.Title })
            : MenuOpen ? (InCampaign ? new[] { MenuItem.Resume, MenuItem.Controls, MenuItem.Settings, MenuItem.Withdraw } : new[] { MenuItem.Resume, MenuItem.Controls, MenuItem.Settings, MenuItem.NewBattle, MenuItem.Title })
            : Array.Empty<MenuItem>();
        start = Deploying ? new Rectangle(W / 2 - U(130), U(96), U(260), U(30)) : Rectangle.Empty;
        int mw = U(220), mh = U(30), top = ShowResult ? H / 2 + U(70) : H / 2 - items.Length * (mh + U(8)) / 2 + U(10);
        for (int i = 0; i < items.Length; i++) menu.Add((new Rectangle((W - mw) / 2, top + i * (mh + U(8)), mw, mh), items[i]));
    }

    public Squad? CardAt(Point p) => cards.FirstOrDefault(c => c.R.Contains(p)).S;
    public Cmd? ButtonAt(Point p) => buttons.Where(b => b.R.Contains(p)).Select(b => (Cmd?)b.C).FirstOrDefault();
    public MenuItem? MenuAt(Point p) => menu.Where(m => m.R.Contains(p)).Select(m => (MenuItem?)m.M).FirstOrDefault();
    public Vector2? MinimapAt(Point p) => minimap.Contains(p)
        ? World.MinimapToWorld(new Vector2((p.X - minimap.X) / (float)minimap.Width, (p.Y - minimap.Y) / (float)minimap.Height)) : null;
    public bool Over(Point p) => CardAt(p) != null || ButtonAt(p) != null || minimap.Contains(p) || player.Contains(p) || start.Contains(p);

    // ---------- primitives ----------

    void Rect(Rectangle r, Color c) => sb.Draw(art.Pixel, r, c);

    /// The one panel style: slate fill, a hairline edge, and a faint top light (the "iron plate" bevel).
    void Panel(Rectangle r, Color? edge = null, int thick = 1)
    {
        Rect(r, PanelBg);
        Rect(new Rectangle(r.X + U(thick), r.Y + U(thick), r.Width - 2 * U(thick), U(1)), Style.PanelLight);
        Frame(r, edge ?? Edge, U(thick));
    }

    void Frame(Rectangle r, Color c, int t)
    {
        Rect(new Rectangle(r.X, r.Y, r.Width, t), c);
        Rect(new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        Rect(new Rectangle(r.X, r.Y, t, r.Height), c);
        Rect(new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    void Line(Vector2 a, Vector2 b, Color c, float w)
    {
        var d = b - a;
        sb.Draw(art.Pixel, a, null, c, MathF.Atan2(d.Y, d.X), new Vector2(0, 0.5f), new Vector2(d.Length(), w), SpriteEffects.None, 0);
    }

    void Bar(Rectangle r, float frac, Color c)
    {
        Rect(r, Color.Black * 0.55f);
        Rect(new Rectangle(r.X, r.Y, (int)(r.Width * Math.Clamp(frac, 0, 1)), r.Height), c);
    }

    // Text scales: 1 = small, 2 = normal, 4 = big (all times Ui).
    void Text(string s, float x, float y, Color c, int scale = 2) => font.Draw(sb, s, new Vector2(x, y), c, scale * Ui);
    int TW(string s, int scale = 2) => PixelFont.Width(s, scale * Ui);
    void TextC(string s, float cx, float y, Color c, int scale = 2) => Text(s, cx - TW(s, scale) / 2f, y, c, scale);
    void TextR(string s, float rx, float y, Color c, int scale = 2) => Text(s, rx - TW(s, scale), y, c, scale);

    static Color MoraleColor(float m) => m < 30 ? Bad : m < 50 ? Warn : Ok;

    static (string Text, Color Col) StateOf(Squad s, int active, int routing, int morale) =>
        active == 0 ? ("LOST", Dim)
        : routing * 2 > active ? ("ROUTING", Bad)
        : morale < 30 ? ("WAVERING", Warn)
        : s.Pressure > 1.5f ? ("FLANKED", Orange)
        : ("STEADY", Ok);

    static int AvgMorale(IEnumerable<Unit> us)
    {
        var a = us.Where(u => u.Active).ToList();
        return a.Count == 0 ? 0 : (int)a.Average(u => u.Morale);
    }

    // ---------- draw ----------

    public void Draw()
    {
        sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        if (Command) Banners();
        Balance();
        Feed();
        MinimapPanel();
        PlayerPanel();
        Cards();
        if (Command) CommandPanel(); else Crosshair();
        if (WheelOpen) Wheel();
        Hints();
        if (Box is Rectangle box) { Rect(box, Gold * 0.12f); Frame(box, Gold, 1); }
        if (Deploying)
        {
            var banner = new Rectangle(W / 2 - U(230), U(60), U(460), U(72));
            Panel(banner, Gold);
            TextC("DEPLOYMENT: PLACE YOUR COMPANIES IN YOUR THIRD OF THE FIELD", W / 2f, banner.Y + U(10), Gold, 1);
            bool hover = start.Contains(Mouse);
            Rect(start, hover ? Gold * 0.45f : Gold * 0.2f);
            Frame(start, Gold, U(1));
            TextC($"START BATTLE ({Ctl.Name(Act.Pause)})", start.X + start.Width / 2f, start.Y + U(8), Color.White);
        }
        else if (Paused && !MenuOpen && !HelpOpen && !ShowResult)
        {
            var r = new Rectangle(W / 2 - U(90), U(64), U(180), U(30));
            Panel(r, Gold);
            TextC("PAUSED", W / 2f, r.Y + U(8), Gold);
        }
        if (ShowResult) Result();
        else if (HelpOpen) Help();
        else if (MenuOpen) Menu();
        sb.End();
    }

    void Balance()
    {
        float Strength(int team) => B.Units.Where(u => u.Team == team && u.Active && !u.Routing)
            .Sum(u => u.Hp / u.Type.MaxHp * (u.Type.Class switch { UnitClass.Cavalry => 2.2f, UnitClass.Captain => 3f, UnitClass.Archer => 0.9f, _ => 1f }));
        float s0 = Strength(0), s1 = Strength(1), f = s0 + s1 > 0 ? s0 / (s0 + s1) : 0.5f;
        int bw = U(360), x = (W - bw) / 2, y = U(14);
        var panel = new Rectangle(x - U(8), y - U(6), bw + U(16), U(44));
        Panel(panel);
        Rect(new Rectangle(x, y + U(18), bw, U(10)), Foe);
        Rect(new Rectangle(x, y + U(18), (int)(bw * f), U(10)), Friendly);
        Rect(new Rectangle(x + bw / 2 - U(1), y + U(15), U(2), U(16)), Color.White * 0.8f);
        int mine = B.Units.Count(u => u.Team == 0 && u.Active && !u.Routing), foes = B.Units.Count(u => u.Team == 1 && u.Active && !u.Routing);
        Text($"{mine}", x, y, Friendly);
        TextR($"{foes}", x + bw, y, Foe);
        int t = (int)B.Time;
        string state = Paused ? "PAUSED" : Command && B.Player.Active ? "X0.25" : "";
        TextC($"{t / 60}:{t % 60:00} {state}".Trim(), W / 2f, y, Color.White);
    }

    void Feed()
    {
        int y = U(12), shown = 0;
        for (int i = B.Events.Count - 1; i >= 0 && shown < 6; i--, shown++)
        {
            var (text, at) = B.Events[i];
            float alpha = Math.Clamp((6 - (B.Time - at)) / 1.5f, 0, 1);
            var up = text.ToUpperInvariant();
            var c = up.StartsWith("VICTORY") || up.Contains("CAPTAIN IS SLAIN") ? Gold
                : up.StartsWith("DEFEAT") || up.Contains("YOU HAVE FALLEN") || up.Contains("HORSE IS SLAIN") || up.Contains("GUARD BROKEN") ? Bad
                : up.StartsWith("ENEMY") || up.StartsWith("THE ENEMY") ? Foe : Color.White;
            // Keep clear of the balance bar in the middle: long lines drop to caption size.
            int room = (W - U(376)) / 2 - U(26), scale = TW(up) <= room ? Style.Body : Style.Caption;
            int w = TW(up, scale) + U(12);
            Rect(new Rectangle(U(10), y - U(4), w, U(scale == Style.Body ? 22 : 15)), Color.Black * (0.45f * alpha));
            Text(up, U(16), y, c * alpha, scale);
            y += U(scale == Style.Body ? 24 : 17);
        }
    }

    void MinimapPanel()
    {
        var r = minimap;
        Panel(new Rectangle(r.X - U(3), r.Y - U(3), r.Width + U(6), r.Height + U(6)));
        if (Minimap != null) sb.Draw(Minimap, r, Color.White);
        Vector2 M(Vector2 w) { var m = World.WorldToMinimap(w); return new Vector2(r.X + m.X * r.Width, r.Y + m.Y * r.Height); }
        bool Inside(Vector2 p) => r.Contains((int)p.X, (int)p.Y);
        foreach (var u in B.Units)
        {
            if (!u.Active || u.IsPlayer) continue;
            var p = M(u.Pos);
            if (!Inside(p)) continue;
            var c = u.Routing ? Dim : u.Team == 1 ? Foe : u.Squad != null && Selected.Contains(u.Squad) ? Gold : Friendly;
            int s = U(u.Mounted ? 3 : 2);
            Rect(new Rectangle((int)p.X - s / 2, (int)p.Y - s / 2, s, s), c);
        }
        if (B.Player.Active)
        {
            var p = M(B.Player.Pos);
            Rect(new Rectangle((int)p.X - U(2), (int)p.Y - U(2), U(5), U(5)), Gold);
        }
        // Camera wedge: where you are looking.
        var origin = M(Command ? CamFocus : B.Player.Pos);
        for (int k = -1; k <= 1; k += 2)
        {
            float a = CamYaw + k * 0.45f;
            var tip = M((Command ? CamFocus : B.Player.Pos) + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 22);
            Line(origin, tip, Color.White * 0.7f, U(1));
        }
    }

    void PlayerPanel()
    {
        var p = B.Player;
        var r = player;
        Panel(r);
        int x = r.X + U(10), y = r.Y + U(8), bw = U(170);
        if (!p.Active)
        {
            Text(B.Options.PlayerWounded ? "WOUNDED" : "YOU HAVE FALLEN", x, y, Bad);
            Text("COMMANDING FROM AFAR", x, y + U(22), Dim, 1);
            Text($"KILLS {B.PlayerKills}", x, y + U(40), Color.White);
            return;
        }
        string mount = p.Mounted ? (p.Couched ? "LANCE COUCHED" : $"{p.Horse!.Name.ToUpperInvariant()}  {MathF.Abs(p.Speed):0.0} M/S") : "ON FOOT";
        Text(mount, x, y, p.Couched ? Gold : Color.White);
        y += U(20);
        void Row(string label, float frac, Color c)
        {
            Text(label, x, y + U(1), Dim, 1);
            Bar(new Rectangle(x + U(44), y, bw, U(8)), frac, c);
            y += U(14);
        }
        Row("HEALTH", p.Hp / p.Type.MaxHp, Bad);
        Row("STAMINA", p.Stamina / 100, Ok);
        if (p.Mounted) Row("HORSE", p.HorseHp / p.Horse!.Hp, HorseCol);
        else Text("NO HORSE", x, y + U(1), Dim, 1);
        int rx = r.Right - U(10);
        TextR(B.RallyCooldown > 0 ? $"RALLY {B.RallyCooldown:0}" : "RALLY", rx, r.Y + U(28), B.RallyCooldown > 0 ? Dim : Gold, 1);
        TextR($"KILLS {B.PlayerKills}", rx, r.Y + U(42), Color.White, 1);
    }

    void Cards()
    {
        foreach (var (r, s) in cards)
        {
            int active = s.Units.Count(u => u.Active), routing = s.Units.Count(u => u.Active && u.Routing), morale = AvgMorale(s.Units);
            var (state, sc) = StateOf(s, active, routing, morale);
            bool sel = Selected.Contains(s);
            bool hover = Command && r.Contains(Mouse);
            Panel(r, sel ? Gold : state is "STEADY" ? (hover ? Color.White * 0.5f : Edge) : sc, sel ? 2 : 1);
            if (active == 0) Rect(r, Color.Black * 0.4f);

            int idx = cards.FindIndex(c => c.S == s) + 1;
            Rect(new Rectangle(r.X + U(3), r.Y + U(3), U(13), U(13)), sel ? Gold : Color.White * 0.2f);
            Text($"{idx}", r.X + U(6), r.Y + U(6), sel ? Color.Black : Color.White, 1);
            var type = s.Units.FirstOrDefault()?.Type ?? UnitType.Shieldman;
            Text(Style.Numerals[Math.Clamp(type.Tier, 0, 5)], r.X + U(4), r.Y + U(20), Gold, 1); // tier
            string tag = s.Order switch { Order.Follow => "FLW", Order.Charge => s.Target != null ? "ATK" : "CHG", _ => "HLD" };
            TextR(tag, r.Right - U(4), r.Y + U(5), s.Order == Order.Charge ? Foe : Dim, 1);
            if (s.Loose) TextR("LOOSE", r.Right - U(4), r.Y + U(14), Dim, 1);
            if (!s.FireAtWill) TextR("HOLD", r.Right - U(4), r.Y + U(23), Warn, 1);

            Icon(type, new Rectangle(r.X + U(16), r.Y + U(4), r.Width - U(32), U(44)));
            TextC(s.Name.ToUpperInvariant(), r.X + r.Width / 2f, r.Y + U(50), Dim, 1);
            TextC($"{active}/{s.StartCount}", r.X + r.Width / 2f, r.Y + U(60), Color.White);
            Bar(new Rectangle(r.X + U(6), r.Y + U(77), r.Width - U(12), U(4)), active / (float)Math.Max(1, s.StartCount), Friendly);
            Bar(new Rectangle(r.X + U(6), r.Y + U(84), r.Width - U(12), U(4)), morale / 100f, MoraleColor(morale));
            if (state != "STEADY") TextC(state, r.X + r.Width / 2f, r.Y - U(12), sc, 1);
        }
    }

    /// A portrait built from the troop's own part sprites, laid out by the same body metrics as the 3D puppet
    /// (so every loadout and tier is drawn correctly), cropped to `box`.
    void Icon(UnitType t, Rectangle box) => Portrait(sb, art, t, box, Ui);

    public static void Portrait(SpriteBatch sb, Art art, UnitType t, Rectangle box, int k)
    {
        var l = t.Look;
        const float top = 1.92f; // metres at the top of the box
        void Part(string key, float xm, float ym, string pal = "/0", Color? tint = null)
        {
            var src = art[key + pal];
            var dest = new Rectangle((int)(box.Center.X + xm / Art.Px * k - src.Width * k / 2f), (int)(box.Y + (top - ym) / Art.Px * k - src.Height * k / 2f),
                src.Width * k, src.Height * k);
            var d = Rectangle.Intersect(dest, box);
            if (d.IsEmpty) return;
            var s2 = new Rectangle(src.X + (d.X - dest.X) / k, src.Y + (d.Y - dest.Y) / k, d.Width / k, d.Height / k);
            sb.Draw(art.Atlas, new Rectangle(d.X, d.Y, s2.Width * k, s2.Height * k), s2, tint ?? Color.White);
        }
        float WeaponY(string w) => 1.0f + (0.5f - SpriteSheet.Grip[w]) * art[w + "/0"].Height * Art.Px;
        if (t.Mount != null) Part("hhead", -0.25f, 0.78f, "/n", new Color(Style.Coats[0]));
        if (t.Class == UnitClass.Archer) Part(l.Weapon, 0.42f, 1.2f);
        if (l.Cape) Part("cape", 0, 1.0f);
        Part($"torso.{l.Body}.f", 0, 1.0f);
        if (l.Tabard) Part("tabard.f", 0, 0.98f);
        Part("head.f", 0, 1.53f);
        if (l.Head != "none") Part($"helm.{l.Head}.f", 0, 1.81f - art[$"helm.{l.Head}.f/0"].Height * Art.Px / 2);
        if (l.Plume) Part("plume", 0, 1.81f + art["plume/0"].Height * Art.Px / 2 - 0.06f);
        if (t.Class != UnitClass.Archer) Part(l.Weapon, -0.42f, WeaponY(l.Weapon));
        if (l.Shield != "none") Part($"shield.{l.Shield}", 0.36f, 0.95f);
    }

    void Crosshair()
    {
        if (!B.Player.Active) return;
        int cx = W / 2, cy = H / 2;
        Rect(new Rectangle(cx - U(1), cy - U(1), U(3), U(3)), Color.White);
        var incoming = new bool[4];
        foreach (var u in B.Units)
            if (u.Active && u.Team != 0 && u.Target == B.Player && u.Action == UnitAction.Windup && Vector2.DistanceSquared(u.Pos, B.Player.Pos) < 16)
                incoming[(int)Dirs.Mirror(u.Swing)] = true;
        float rad = U(22), len = U(7);
        for (int i = 0; i < 4; i++)
        {
            var d = (AttackDir)i;
            var dir = d switch { AttackDir.Overhead => -Vector2.UnitY, AttackDir.Thrust => Vector2.UnitY, AttackDir.Left => -Vector2.UnitX, _ => Vector2.UnitX };
            var side = new Vector2(-dir.Y, dir.X);
            var tip = new Vector2(cx, cy) + dir * (rad + len);
            var c = incoming[i] ? Foe : d == Aim ? Gold : Color.White * 0.35f;
            float w = incoming[i] || d == Aim ? U(3) : U(2);
            Line(tip, tip - dir * len + side * len, c, w); // chevron pointing outward
            Line(tip, tip - dir * len - side * len, c, w);
        }
        var p = B.Player;
        if (p.Charging) Bar(new Rectangle(cx - U(24), cy + U(40), U(48), U(4)), p.Charge, Gold);
        var (hit, at) = B.PlayerHit;
        if (B.Time - at < 1.2f) TextC(hit, cx, cy + U(50), hit.StartsWith("PARRY") ? Gold : Color.White);

        // Context prompts.
        string? prompt = null;
        if (!p.Mounted && B.LooseHorses.Any(h => Vector2.DistanceSquared(h.Pos, p.Pos) < 2.8f * 2.8f)) prompt = $"{Ctl.Name(Act.Interact)}  MOUNT HORSE";
        else if (p.Mounted && !p.Couched && p.Speed > 6) prompt = $"{Ctl.Name(Act.Couch)}  COUCH LANCE";
        else if (p.Mounted && MathF.Abs(p.Speed) < 1.5f) prompt = $"{Ctl.Name(Act.Interact)}  DISMOUNT";
        if (prompt != null)
        {
            int pw = TW(prompt, 1) + U(14), py = H / 2 + (H / 2 - U(120)) / 2; // between the crosshair and the cards
            Rect(new Rectangle(cx - pw / 2, py - U(5), pw, U(17)), Color.Black * 0.5f);
            TextC(prompt, cx, py, Color.White, 1);
        }
    }

    void CommandPanel()
    {
        if (buttons.Count == 0) return;
        var first = buttons[0].R;
        var area = new Rectangle(first.X - U(6), first.Y - U(22), buttons[^1].R.Right - first.X + U(12), buttons[^1].R.Bottom - first.Y + U(28));
        Panel(area);
        Text(Selected.Count == 0 ? "ORDERS: ALL TROOPS" : "ORDERS: " + string.Join(" ", cards.Select((c, i) => Selected.Contains(c.S) ? $"{i + 1}" : "").Where(x => x != "")),
            area.X + U(6), area.Y + U(6), Gold, 1);
        foreach (var (r, c) in buttons)
        {
            bool hover = r.Contains(Mouse);
            Rect(r, hover ? Gold * 0.35f : Color.White * 0.06f);
            Frame(r, hover ? Gold : Edge, U(1));
            var (label, act) = Orders[c];
            Text(label, r.X + U(6), r.Y + U(8), Color.White, 1);
            TextR(Ctl.Name(act), r.Right - U(5), r.Y + U(8), Dim, 1);
        }
    }

    void Wheel()
    {
        Rect(new Rectangle(0, 0, W, H), Color.Black * 0.25f);
        var c = new Vector2(W / 2f, H / 2f);
        var order = WheelOrder;
        for (int i = 0; i < order.Length; i++)
        {
            float a = -MathHelper.PiOver2 + i * MathHelper.TwoPi / order.Length;
            var at = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * U(130);
            var r = new Rectangle((int)at.X - U(58), (int)at.Y - U(14), U(116), U(28));
            bool on = i == WheelSector;
            Panel(r, on ? Gold : Edge, on ? 2 : 1);
            if (on) Rect(r, Gold * 0.25f);
            var (label, act) = Orders[order[i]];
            TextC(label, at.X, at.Y - U(9), on ? Gold : Color.White, 1);
            TextC(Ctl.Name(act), at.X, at.Y + U(2), Dim, 1);
        }
        var inner = new Rectangle((int)c.X - U(60), (int)c.Y - U(16), U(120), U(32));
        Panel(inner);
        TextC("ORDERS TO", c.X, c.Y - U(10), Dim, 1);
        TextC(Selected.Count == 0 ? "ALL TROOPS" : string.Join(" ", cards.Select((cd, i) => Selected.Contains(cd.S) ? $"{i + 1}" : "").Where(x => x != "")),
            c.X, c.Y + U(1), Gold, 1);
    }

    /// Wheel sectors clockwise from the top.
    public static readonly Cmd[] WheelOrder = { Cmd.Charge, Cmd.Advance, Cmd.FormHere, Cmd.Spacing, Cmd.FallBack, Cmd.FireAtWill, Cmd.Hold, Cmd.Follow };

    void Hints()
    {
        string s = Command
            ? $"{Ctl.Name(Act.Help)} CONTROLS   {Ctl.Name(Act.Pause)} PAUSE   {Ctl.Name(Act.CommandView)} {(B.Player.Active ? "BACK TO BODY" : "")}"
            : $"{Ctl.Name(Act.Help)} CONTROLS   {Ctl.Name(Act.OrderWheel)} ORDERS   {Ctl.Name(Act.CommandView)} COMMAND   {Ctl.Name(Act.Menu)} MENU";
        int y = Command && buttons.Count > 0 ? buttons[0].R.Y - U(40) : H - U(18);
        TextR(s.Trim(), W - U(12), y, Color.White * 0.6f, 1);
    }

    void Banners()
    {
        var vp = new Viewport(0, 0, W, H);
        var mine = Mine;
        foreach (var s in B.Squads)
        {
            if (s.Alive == 0) continue;
            var c = s.Centroid();
            var sp = vp.Project(new Vector3(c.X, B.Terrain.Height(c) + 3.4f, c.Y), Proj, View, Matrix.Identity);
            if (sp.Z < 0 || sp.Z > 1) continue;
            bool own = s.Team == 0, sel = Selected.Contains(s);
            var col = own ? Friendly : Foe;
            int x = (int)sp.X, y = (int)sp.Y;
            Rect(new Rectangle(x, y - U(24), U(2), U(26)), Color.Black * 0.7f); // pole
            var flag = new Rectangle(x + U(2), y - U(24), U(24), U(15));
            Rect(flag, col * 0.9f);
            Frame(flag, sel ? Gold : Color.Black * 0.6f, U(sel ? 2 : 1));
            string label = own ? $"{mine.IndexOf(s) + 1}" : s.Name[..1];
            TextC(label, flag.X + flag.Width / 2f, flag.Y + U(4), Color.White, 1);
            int morale = AvgMorale(s.Units);
            Bar(new Rectangle(flag.X, flag.Bottom + U(2), flag.Width, U(3)), morale / 100f, MoraleColor(morale));
            if (s.Target != null && own) Text("ATK", flag.Right + U(3), flag.Y + U(4), Foe, 1);
        }
    }

    void Overlay(Rectangle r, string title, Color tc)
    {
        Rect(new Rectangle(0, 0, W, H), Color.Black * 0.45f);
        Panel(r, Edge, 1);
        TextC(title, W / 2f, r.Y + U(14), tc, 3);
    }

    void MenuButtons()
    {
        foreach (var (r, m) in menu)
        {
            bool hover = r.Contains(Mouse);
            Rect(r, hover ? Gold * 0.35f : Color.White * 0.06f);
            Frame(r, hover ? Gold : Edge, U(1));
            TextC(m switch { MenuItem.Resume => "RESUME", MenuItem.Controls => "CONTROLS", MenuItem.NewBattle => "NEW BATTLE", MenuItem.Withdraw => "WITHDRAW",
                    MenuItem.Continue => "CONTINUE", MenuItem.Title => "QUIT TO TITLE", MenuItem.Settings => "SETTINGS", _ => "QUIT" },
                r.X + r.Width / 2f, r.Y + U(8), Color.White);
        }
    }

    void Menu()
    {
        var top = menu.Count > 0 ? menu[0].R.Y : H / 2;
        Overlay(new Rectangle(W / 2 - U(140), top - U(60), U(280), menu.Count * U(38) + U(74)), "IRONFIELD", Gold);
        MenuButtons();
    }

    void Result()
    {
        bool won = B.Winner == 0;
        var r = new Rectangle(W / 2 - U(220), H / 2 - U(150), U(440), U(300));
        Overlay(r, "", Color.White);
        TextC(won ? "VICTORY" : "DEFEAT", W / 2f, r.Y + U(18), won ? Gold : Bad, 5);
        int y = r.Y + U(80), lx = r.X + U(40), c1 = r.X + U(250), c2 = r.X + U(350);
        Text("", lx, y, Dim);
        Text("YOURS", c1 - U(10), y, Friendly, 1);
        Text("ENEMY", c2 - U(10), y, Foe, 1);
        y += U(18);
        void Row(string label, Func<int, int> f)
        {
            Text(label, lx, y, Dim, 1);
            Text($"{f(0)}", c1, y, Color.White, 1);
            Text($"{f(1)}", c2, y, Color.White, 1);
            y += U(16);
        }
        Row("MEN", t => B.Units.Count(u => u.Team == t));
        Row("FALLEN", t => B.Units.Count(u => u.Team == t && u.Dead));
        Row("FLED", t => B.Units.Count(u => u.Team == t && (u.Fled || (u.Active && u.Routing))));
        Row("STANDING", t => B.Units.Count(u => u.Team == t && u.Active && !u.Routing));
        Row("HORSES LOST", t => B.DeadHorses.Count(h => h.Team == t));
        y += U(6);
        int time = (int)B.Time;
        Text($"YOUR KILLS {B.PlayerKills}     BATTLE TIME {time / 60}:{time % 60:00}", lx, y, Color.White, 1);
        MenuButtons();
    }

    void Help()
    {
        var r = new Rectangle(W / 2 - U(420), H / 2 - U(250), U(840), U(500));
        Overlay(r, "CONTROLS", Gold);
        var cols = new (string Title, (string Label, string Key)[] Rows)[]
        {
            ("IN THE FIELD", new[] { Act.Forward, Act.Back, Act.Left, Act.Right, Act.Sprint, Act.Attack, Act.Block, Act.Couch, Act.Interact, Act.Rally, Act.OrderWheel }
                .Select(a => (Controls.Labels[a], Ctl.Name(a)))
                .Append(("SWING / BLOCK DIRECTION", "MOVE MOUSE"))
                .Append(("FEINT", "BLOCK IN WINDUP")).ToArray()),
            ("COMMAND VIEW", new (string, string)[]
            {
                ("SELECT SQUAD / BOX", "LMB / DRAG"), ("ADD TO SELECTION", Ctl.Name(Act.AddToSelection)), ("MOVE (KEEP WIDTH)", "RMB"),
                ("DRAW FORMATION LINE", "RMB DRAG"), ("ATTACK ENEMY SQUAD", "RMB ON ENEMY"), ("PAN", $"{Ctl.First(Act.Forward)}{Ctl.First(Act.Left)}{Ctl.First(Act.Back)}{Ctl.First(Act.Right)} / SCREEN EDGES"),
                ("ROTATE", $"{Ctl.First(Act.RotateLeft)} {Ctl.First(Act.RotateRight)} / MMB DRAG"), ("ZOOM", "WHEEL"), ("JUMP CAMERA", "CLICK MINIMAP"),
                ("FOCUS SQUAD", "DOUBLE-CLICK CARD"),
            }),
            ("ORDERS", new[] { Act.Charge, Act.Follow, Act.FormHere, Act.HoldPosition, Act.Advance, Act.FallBack, Act.Spacing, Act.FireAtWill, Act.SelectAll,
                    Act.CommandView, Act.Pause, Act.Menu, Act.Help, Act.Graphics }
                .Select(a => (Controls.Labels[a], Ctl.Name(a)))
                .Prepend(("SELECT SQUAD", "1-9")).ToArray()),
        };
        int cw = (r.Width - U(40)) / 3;
        for (int i = 0; i < cols.Length; i++)
        {
            int x = r.X + U(20) + i * cw, y = r.Y + U(56);
            Text(cols[i].Title, x, y, Gold);
            y += U(24);
            foreach (var (label, key) in cols[i].Rows)
            {
                Text(label, x, y, Dim, 1);
                Text(key, x, y + U(9), Color.White, 1);
                y += U(24);
            }
        }
        TextC($"REBIND IN {Controls.FileName.ToUpperInvariant()}  (RUN WITH --WRITE-CONTROLS FOR A TEMPLATE)    {Ctl.Name(Act.Help)} / {Ctl.Name(Act.Menu)} CLOSE",
            W / 2f, r.Bottom - U(20), Dim, 1);
    }
}
