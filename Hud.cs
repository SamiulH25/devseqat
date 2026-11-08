using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Orders the player can give, from hotkeys, the order wheel or the command-view buttons.
public enum Cmd { Charge, Advance, FormHere, Spacing, FallBack, FireAtWill, Hold, Follow, Formation }

public enum MenuItem { Resume, Controls, NewBattle, Quit, Withdraw, Continue, Title, Settings }

/// All 2D interface: unit cards, player panel, balance of power, minimap, event feed, crosshair, order wheel,
/// command buttons, squad banners and the menu / controls / result overlays. Layout is in 720p units times `Ui`.
sealed partial class Hud
{
    // Colours come from the design tokens (Style.cs, docs/DESIGN.md).
    public static readonly Color Gold = Style.Accent, Friendly = Style.Friendly, Foe = Style.Foe, Ok = Style.Ok, Warn = Style.Warn,
        Bad = Style.Bad, Dim = Style.Dim, Orange = Style.Orange, HorseCol = Style.HorseBar;
    static readonly Color PanelBg = Style.Panel, Edge = Style.PanelEdge;

    public static readonly Dictionary<Cmd, (string Label, Act Key)> Orders = new()
    {
        [Cmd.Charge] = ("CHARGE", Act.Charge), [Cmd.Advance] = ("ADVANCE", Act.Advance), [Cmd.FormHere] = ("FORM ON ME", Act.FormHere),
        [Cmd.Spacing] = ("SPACING", Act.Spacing), [Cmd.FallBack] = ("FALL BACK", Act.FallBack), [Cmd.FireAtWill] = ("FIRE / HOLD", Act.FireAtWill),
        [Cmd.Hold] = ("HOLD", Act.HoldPosition), [Cmd.Follow] = ("FOLLOW ME", Act.Follow), [Cmd.Formation] = ("FORMATION", Act.Formation),
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
    /// The company under the mouse in command view (for the tooltip).
    public Squad? HoverSquad;
    /// The division you are giving goals to (cards, goal buttons and the goal wheel), or null.
    public Division? Div;

    // Layout, rebuilt every frame.
    readonly List<(Rectangle R, Squad S)> cards = new();
    readonly List<(Rectangle R, Cmd C)> buttons = new();
    readonly List<(Rectangle R, MenuItem M)> menu = new();
    Rectangle minimap, player;
    int W, H;

    /// The shared interface kit (Ui + Kit), drawing into this HUD's SpriteBatch; the HUD does its own hit-testing.
    readonly Ui kit;

    public Hud(SpriteBatch sb, Art art, PixelFont font) { this.sb = sb; this.art = art; this.font = font; kit = new Ui(sb, art, font); }

    int U(float v) => (int)(v * Ui);
    List<Squad> Mine => B.Commanded.ToList();

    // ---------- layout & hit testing ----------

    public void Layout(Viewport vp)
    {
        W = vp.Width;
        H = vp.Height;
        Ui = Settings.UiFor(H);
        cards.Clear(); buttons.Clear(); menu.Clear();

        var mine = Mine;
        // Cards shrink to fit a big retinue; past what fits, the rest are summed up on the last card.
        int room = W - U(296) - U(250), gap = mine.Count > 8 ? U(4) : U(6), ch = U(94);
        int cw = Math.Clamp((room - gap * Math.Max(0, mine.Count - 1)) / Math.Max(1, mine.Count), U(64), mine.Count > 8 ? U(68) : U(80));
        int fit = Math.Max(1, (room + gap) / (cw + gap));
        int shown = Math.Min(mine.Count, fit), total = shown * cw + Math.Max(0, shown - 1) * gap;
        int cx0 = Math.Max(U(296), (W - total) / 2);
        for (int i = 0; i < shown; i++) cards.Add((new Rectangle(cx0 + i * (cw + gap), H - ch - U(10), cw, ch), mine[i]));
        hiddenCards = mine.Count - shown;

        player = new Rectangle(U(10), H - U(104), U(280), U(94));
        int ms = U(150);
        minimap = new Rectangle(W - ms - U(10), U(10), ms, ms);
        LayoutDivisions();

        if (Command && Div != null) LayoutGoals();
        else if (Command)
        {
            int bw = U(112), bh = U(24);
            var order = new[] { Cmd.Charge, Cmd.Hold, Cmd.Advance, Cmd.FallBack, Cmd.Follow, Cmd.FormHere, Cmd.Spacing, Cmd.FireAtWill, Cmd.Formation };
            int x0 = W - 2 * bw - U(16), y0 = H - (order.Length + 1) / 2 * (bh + U(4)) - U(10);
            for (int i = 0; i < order.Length; i++)
                buttons.Add((new Rectangle(x0 + (i % 2) * (bw + U(4)), y0 + (i / 2) * (bh + U(4)), bw, bh), order[i]));
        }

        var items = ShowResult ? (InCampaign ? new[] { MenuItem.Continue } : new[] { MenuItem.NewBattle, MenuItem.Title })
            : MenuOpen ? (InCampaign ? new[] { MenuItem.Resume, MenuItem.Controls, MenuItem.Settings, MenuItem.Withdraw } : new[] { MenuItem.Resume, MenuItem.Controls, MenuItem.Settings, MenuItem.NewBattle, MenuItem.Title })
            : Array.Empty<MenuItem>();
        start = Deploying ? new Rectangle(W / 2 - U(130), U(96), U(260), U(30)) : Rectangle.Empty;
        int mw = U(220), mh = U(30), top = H / 2 - items.Length * (mh + U(8)) / 2 + U(10);
        if (ShowResult) // side by side along the bottom of the result
            for (int i = 0; i < items.Length; i++) menu.Add((new Rectangle(W / 2 - (items.Length * (mw + U(10)) - U(10)) / 2 + i * (mw + U(10)), ResultBox.Bottom - U(44), mw, mh), items[i]));
        else for (int i = 0; i < items.Length; i++) menu.Add((new Rectangle((W - mw) / 2, top + i * (mh + U(8)), mw, mh), items[i]));
    }

    public Squad? CardAt(Point p) => cards.FirstOrDefault(c => c.R.Contains(p)).S;
    public Cmd? ButtonAt(Point p) => buttons.Where(b => b.R.Contains(p)).Select(b => (Cmd?)b.C).FirstOrDefault();
    public MenuItem? MenuAt(Point p) => menu.Where(m => m.R.Contains(p)).Select(m => (MenuItem?)m.M).FirstOrDefault();
    public Vector2? MinimapAt(Point p) => minimap.Contains(p)
        ? World.MinimapToWorld(new Vector2((p.X - minimap.X) / (float)minimap.Width, (p.Y - minimap.Y) / (float)minimap.Height)) : null;
    public bool Over(Point p) => CardAt(p) != null || ButtonAt(p) != null || DivisionAt(p) != null || GoalAt(p) != null || minimap.Contains(p) || player.Contains(p) || start.Contains(p);

    // ---------- primitives ----------

    void Rect(Rectangle r, Color c) => sb.Draw(art.Pixel, r, c);

    /// The one panel style, the iron plate of the shared kit (Ui.Panel).
    void Panel(Rectangle r, Color? edge = null, int thick = 1) => kit.Panel(r, edge == Edge ? null : edge, thick);

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

    void Bar(Rectangle r, float frac, Color c) => kit.Bar(r, frac, c);

    // Text scales: 1 = small, 2 = normal, 4 = big (all times Ui).
    void Text(string s, float x, float y, Color c, int scale = 2) => font.Draw(sb, s, new Vector2(x, y), c, scale * Ui);
    int TW(string s, int scale = 2) => PixelFont.Width(s, scale * Ui);
    void TextC(string s, float cx, float y, Color c, int scale = 2) => Text(s, cx - TW(s, scale) / 2f, y, c, scale);
    void TextR(string s, float rx, float y, Color c, int scale = 2) => Text(s, rx - TW(s, scale), y, c, scale);

    static Color MoraleColor(float m) => m < Battle.RoutAt ? Bad : m < Battle.RallyAt ? Warn : Ok;

    static (string Text, Color Col) StateOf(Squad s, int active, int routing, int morale) =>
        active == 0 ? ("LOST", Dim)
        : routing * 2 > active ? ("ROUTING", Bad)
        : morale < Battle.RallyAt ? ("WAVERING", Warn)
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
        kit.Reset(Ui, W, H, Mouse);
        SampleStrength();
        sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        SergeantPennants();
        BannerMark();
        if (Command) Banners();
        Balance();
        GoalLines();
        Feed();
        MinimapPanel();
        PlayerPanel();
        if (Command) DivisionMarkers(); // under the cards and panels
        Cards();
        DivisionCards();
        if (Command && Div != null) GoalPanel();
        if (Command) { CommandPanel(); Detail(); if (!ShowResult && !MenuOpen) SquadTooltip(); } else Crosshair();
        if (WheelOpen) { if (Div != null) GoalWheel(); else Wheel(); }
        Hints();
        if (B.DownSince >= 0) // the last stand (P6)
        {
            var r = new Rectangle(W / 2 - U(160), H / 2 - U(60), U(320), U(34));
            Panel(r, Bad);
            TextC($"YOU ARE DOWN: {Ctl.Name(Act.Rally)} TO FIGHT ON ({MathF.Max(0, Battle.LastStandWindow - (B.Time - B.DownSince)):0})", W / 2f, r.Y + U(12), Color.White, 1);
        }
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

    /// The fighting strength of a side: men standing, weighted by health and arm (a rider counts for more than a bowman).
    float Strength(int team) => B.Units.Where(u => u.Team == team && u.Active && !u.Routing)
        .Sum(u => u.Hp / u.Type.MaxHp * (u.Type.Class switch { UnitClass.Cavalry => 2.2f, UnitClass.Captain => 3f, UnitClass.Archer => 0.9f, _ => 1f }));

    Heraldry SideArms(int team) => Style.Sides[Math.Clamp(B.Options.Palette[Math.Clamp(team, 0, 1)], 0, Style.Sides.Length - 1)];

    /// Top centre: the two sides' strength pulling on one bar between their shields, the men standing and the clock.
    /// Your personal goals for this battle (P6), under the balance bar: open in white, met in gold, lost struck grey.
    void GoalLines()
    {
        int y = U(58);
        foreach (var g in B.Goals)
        {
            int st = B.GoalState(g);
            string mark = st > 0 ? "DONE: " : st < 0 ? "LOST: " : "GOAL: ";
            TextC(mark + g.Text, W / 2f, y, st > 0 ? Gold : st < 0 ? Dim * 0.6f : Color.White, 1);
            y += U(11);
        }
    }

    void Balance()
    {
        float s0 = Strength(0), s1 = Strength(1), f = s0 + s1 > 0 ? s0 / (s0 + s1) : 0.5f;
        int bw = U(360), x = (W - bw) / 2, y = U(14);
        var panel = new Rectangle(x - U(56), y - U(8), bw + U(112), U(46));
        Panel(panel);
        kit.Shield(new Rectangle(x - U(48), y - U(4), U(30), U(36)), SideArms(0));
        kit.Shield(new Rectangle(x + bw + U(18), y - U(4), U(30), U(36)), SideArms(1));
        kit.TugBar(new Rectangle(x, y + U(20), bw, U(8)), f, SideArms(0).Ui, Foe);
        int mine = B.Units.Count(u => u.Team == 0 && u.Active && !u.Routing), foes = B.Units.Count(u => u.Team == 1 && u.Active && !u.Routing);
        Text($"{mine}", x, y, SideArms(0).Ui);
        TextR($"{foes}", x + bw, y, Foe);
        int t = (int)B.Time;
        string state = Paused ? "PAUSED" : Command && B.Player.Active ? "X0.25" : "";
        kit.Icon("clock", W / 2 - TW($"{t / 60}:{t % 60:00} {state}".Trim()) / 2 - U(12), y + U(2), Dim, 1);
        TextC($"{t / 60}:{t % 60:00} {state}".Trim(), W / 2f, y, Color.White);
        if (B.Wall is SiegeWall w && !Deploying)
        {
            // The gate's strength under the balance bar, while it stands.
            var g = new Rectangle(x + bw / 4, panel.Bottom + U(4), bw / 2, U(16));
            Panel(g);
            if (w.GateOpen) TextC("THE GATE IS BROKEN", W / 2f, g.Y + U(5), Gold, 1);
            else
            {
                kit.Icon("tower", g.X + U(6), g.Y + U(5), Color.White, 1);
                Text("GATE", g.X + U(18), g.Y + U(5), Color.White, 1);
                kit.Gauge(new Rectangle(g.X + U(48), g.Y + U(5), g.Width - U(54), U(6)), w.GateHp / w.GateMax, Style.HorseBar);
            }
        }
    }

    /// The event feed as toasts down the left: an edge colour and an icon for what kind of news it is.
    void Feed()
    {
        int y = U(10), shown = 0;
        for (int i = B.Events.Count - 1; i >= 0 && shown < 6; i--, shown++)
        {
            var (text, at) = B.Events[i];
            float alpha = Math.Clamp((6 - (B.Time - at)) / 1.5f, 0, 1);
            var up = text.ToUpperInvariant();
            var (c, icon) = up.StartsWith("VICTORY") || up.Contains("CAPTAIN IS SLAIN") ? (Gold, "crown")
                : up.StartsWith("DEFEAT") || up.Contains("YOU HAVE FALLEN") ? (Bad, "skull")
                : up.Contains("HORSE IS SLAIN") ? (Bad, "horse")
                : up.Contains("GUARD BROKEN") ? (Bad, "shield")
                : up.StartsWith("ENEMY") || up.StartsWith("THE ENEMY") ? (Foe, up.Contains("HORSE") || up.Contains("CAVALRY") ? "horse" : "sword")
                : up.Contains("ROUT") || up.Contains("BREAK") || up.Contains("FLEE") ? (Warn, "flag")
                : up.Contains("ARROW") || up.Contains("VOLLEY") ? (Ok, "arrow")
                : (Color.White, "scroll");
            // Keep clear of the balance bar in the middle.
            int room = (W - U(472)) / 2 - U(40);
            if (TW(up, 1) > room) { int fit = Math.Max(4, room / U(6)); up = up[..Math.Min(up.Length, fit)]; }
            kit.Toast(U(10), y, up, c, icon, alpha);
            y += U(21);
        }
    }

    void MinimapPanel()
    {
        var r = minimap;
        var frame = new Rectangle(r.X - U(5), r.Y - U(5), r.Width + U(10), r.Height + U(10));
        Panel(frame);
        if (Minimap != null) sb.Draw(Minimap, r, Color.White);
        Frame(r, Color.Black * 0.7f, U(1));
        foreach (var (cx, cy) in new[] { (frame.X, frame.Y), (frame.Right - U(4), frame.Y), (frame.X, frame.Bottom - U(4)), (frame.Right - U(4), frame.Bottom - U(4)) })
            Rect(new Rectangle(cx, cy, U(4), U(4)), Kit.Gilt * 0.8f);
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

    /// Bottom left: your own captain: mount, health, stamina and horse as bevelled gauges, rally and kills.
    void PlayerPanel()
    {
        var p = B.Player;
        var r = player;
        Panel(r);
        kit.Shield(new Rectangle(r.X + U(8), r.Y + U(10), U(26), U(31)), SideArms(0));
        int x = r.X + U(42), y = r.Y + U(9), bw = U(150);
        if (!p.Active)
        {
            Text(B.Options.PlayerWounded ? "WOUNDED" : "YOU HAVE FALLEN", x, y, Bad);
            Text("COMMANDING FROM AFAR", x, y + U(22), Dim, 1);
            Text($"KILLS {B.PlayerKills}", x, y + U(40), Color.White);
            return;
        }
        string mount = p.Mounted ? (p.Couched ? "LANCE COUCHED" : $"{p.Horse!.Name.ToUpperInvariant()}  {MathF.Abs(p.Speed):0.0} M/S") : "ON FOOT";
        Text(mount, x, y, p.Couched ? Gold : Color.White, 1);
        if (p.Type.Ammo > 0) TextR($"{p.Ammo} SHOTS", x + U(60) + bw, y, p.Ammo > 0 ? Gold : Bad, 1); // your bow (P1)
        y += U(14);
        void Row(string icon, string label, float frac, Color c)
        {
            kit.Icon(icon, x, y, c, 1);
            Text(label, x + U(12), y + U(1), Dim, 1);
            kit.Gauge(new Rectangle(x + U(60), y, bw, U(8)), frac, c);
            y += U(14);
        }
        Row("heart", "HEALTH", p.Hp / p.Type.MaxHp, Bad);
        Row("boot", "STAMINA", p.Stamina / 100, Ok);
        if (p.Mounted) Row("horse", "HORSE", p.HorseHp / p.Horse!.Hp, HorseCol);
        else Text("NO HORSE", x + U(12), y + U(1), Dim, 1);
        int rx = r.Right - U(10);
        bool ready = B.RallyCooldown <= 0;
        var rally = new Rectangle(rx - U(64), r.Bottom - U(30), U(64), U(18));
        Rect(rally, ready ? Kit.Mul(Gold, 0.35f) : Color.Black * 0.4f);
        Frame(rally, ready ? Gold : Color.White * 0.15f, U(1));
        kit.Icon("flag", rally.X + U(5), rally.Y + U(6), ready ? Gold : Dim, 1);
        TextR(ready ? $"RALLY {Ctl.Name(Act.Rally)}" : $"{B.RallyCooldown:0}", rally.Right - U(5), rally.Y + U(6), ready ? Gold : Dim, 1);
        kit.Icon("skull", rx - U(64), r.Y + U(10), Dim, 1);
        TextR($"{B.PlayerKills}", rx, r.Y + U(10), Color.White, 1);
        // What is in your hands (P7), and the swap key when a second weapon waits.
        string weapon = B.PlayerWeapon.ToUpperInvariant() + (B.HasSecondWeapon ? $" ({Ctl.Name(Act.SwapWeapon)})" : "");
        if (TW(weapon, 1) < rally.X - x - U(8)) Text(weapon, x, r.Bottom - U(16), Dim, 1);
        // The swing (P2): a riposte window open, a chain of blows under way.
        if (!Command)
        {
            if (B.Riposting) TextC("RIPOSTE!", W / 2f, H / 2f + U(36), Gold, 2);
            else if (B.Chain >= 1 && p.Action is UnitAction.Windup or UnitAction.Recover) TextC($"CHAIN X{B.Chain + 1}", W / 2f, H / 2f + U(36), B.Chain >= 3 ? Bad : Color.White, 1);
        }
    }

    /// The unit cards as a dock along the bottom: portrait in its side's cloth, number, tier, order, men and morale gauges.
    void Cards()
    {
        if (cards.Count > 0)
        {
            var dock = new Rectangle(cards[0].R.X - U(8), cards[0].R.Y - U(6), cards[^1].R.Right - cards[0].R.X + U(16), cards[0].R.Height + U(12));
            kit.PlateBody(dock, Kit.Rivet, 1, 0.8f);
        }
        var cloth = SideArms(0);
        foreach (var (r, s) in cards)
        {
            int active = s.Units.Count(u => u.Active), routing = s.Units.Count(u => u.Active && u.Routing), morale = AvgMorale(s.Units);
            var (state, sc) = StateOf(s, active, routing, morale);
            bool sel = Selected.Contains(s);
            bool hover = Command && r.Contains(Mouse);
            Rect(new Rectangle(r.X + U(1), r.Y + U(2), r.Width, r.Height), Color.Black * 0.4f);
            kit.VGrad(r, sel ? new Color(70, 60, 36) : hover ? new Color(46, 46, 50) : new Color(30, 32, 38), Kit.IronDark, 2);
            var port = new Rectangle(r.X + U(3), r.Y + U(3), r.Width - U(6), U(46));
            kit.VGrad(port, Kit.Mul(cloth.Cloth, 0.5f), Kit.IronDark, 2);
            var type = s.Units.FirstOrDefault()?.Type ?? UnitType.Shieldman;
            Icon(type, new Rectangle(port.X + U(12), port.Y + U(1), port.Width - U(24), port.Height - U(1)));
            Frame(r, sel ? Gold : state is "STEADY" ? Color.Black * 0.7f : sc, U(sel ? 2 : 1));
            if (active == 0) Rect(r, Color.Black * 0.45f);

            int idx = cards.FindIndex(c => c.S == s) + 1;
            int bw = idx >= 10 ? U(18) : U(13);
            Rect(new Rectangle(r.X + U(3), r.Y + U(3), bw, U(13)), sel ? Gold : Color.Black * 0.75f);
            Text($"{idx}", r.X + U(5), r.Y + U(6), sel ? Kit.IronDark : Color.White, 1);
            Text(Style.Numerals[Math.Clamp(type.Tier, 0, 7)], r.X + U(4), r.Y + U(20), Gold, 1); // tier
            string tag = s.Order switch { Order.Follow => "FLW", Order.Charge => s.Target != null ? "ATK" : "CHG", _ => "HLD" };
            TextR(tag, r.Right - U(4), r.Y + U(5), s.Order == Order.Charge ? Orange : Dim, 1);
            if (s.Formation != Formation.Line)
                TextR(s.Formation switch { Formation.ShieldWall => "WALL", Formation.Square => "SQUARE", Formation.Wedge => "WEDGE", _ => "LOOSE" }, r.Right - U(4), r.Y + U(14), Dim, 1);
            if (!s.FireAtWill) TextR("HOLD", r.Right - U(4), r.Y + U(23), Warn, 1);

            string nm = s.Name.ToUpperInvariant();
            int fits = Math.Max(3, (r.Width - U(4)) / U(6));
            TextC(nm.Length > fits ? nm[..fits] : nm, r.X + r.Width / 2f, r.Y + U(52), Dim, 1);
            TextC($"{active}/{s.StartCount}", r.X + r.Width / 2f, r.Y + U(61), Color.White);
            kit.Gauge(new Rectangle(r.X + U(4), r.Y + U(76), r.Width - U(8), U(6)), active / (float)Math.Max(1, s.StartCount), cloth.Ui);
            kit.Gauge(new Rectangle(r.X + U(4), r.Y + U(84), r.Width - U(8), U(6)), morale / 100f, MoraleColor(morale));
            if (state != "STEADY") kit.Chip(r.X + r.Width / 2 - (TW(state, 1) + U(10)) / 2, r.Y - U(20), state, sc);
        }
        if (hiddenCards > 0 && cards.Count > 0)
        {
            var last = cards[^1].R;
            TextC($"+{hiddenCards} MORE", last.X + last.Width / 2f, last.Y - U(12), Gold, 1);
        }
    }
    int hiddenCards;

    /// A portrait built from the troop's own part sprites, laid out by the same body metrics as the 3D puppet
    /// (so every loadout and tier is drawn correctly), cropped to `box`.
    void Icon(UnitType t, Rectangle box) => Portrait(sb, art, t, box, Ui);

    public static void Portrait(SpriteBatch sb, Art art, UnitType t, Rectangle box, int k)
    {
        var l = t.Look;
        const float top = 1.92f; // metres at the top of the box
        if (l.Face == "wolf") // a beast's portrait is its head, big
        {
            var src = art["whead/n"];
            int kk = Math.Max(1, Math.Min(box.Width / src.Width, box.Height / src.Height));
            var dst = new Rectangle(box.Center.X - src.Width * kk / 2, box.Center.Y - src.Height * kk / 2, src.Width * kk, src.Height * kk);
            sb.Draw(art.Atlas, dst, src, t.Size > 1.1f ? new Color(150, 140, 135) : Color.White);
            return;
        }
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
        if (t.Mount is { Barding: "wolf" }) Part("whead", -0.3f, 0.8f, "/n", new Color(150, 140, 135));
        else if (t.Mount != null) Part("hhead", -0.25f, 0.78f, "/n", new Color(Style.Coats[0]));
        if (t.Class == UnitClass.Archer) Part(l.Weapon, 0.42f, 1.2f);
        if (l.Cape) Part("cape", 0, 1.0f);
        Part($"torso.{l.Body}.f", 0, 1.0f);
        if (l.Tabard) Part("tabard.f", 0, 0.98f);
        Part(l.HeadKey + ".f", 0, 1.53f);
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

    static readonly Dictionary<Cmd, string> OrderIcons = new()
    {
        [Cmd.Charge] = "sword", [Cmd.Advance] = "up", [Cmd.FormHere] = "crown", [Cmd.Spacing] = "chain", [Cmd.FallBack] = "boot",
        [Cmd.FireAtWill] = "arrow", [Cmd.Hold] = "shield", [Cmd.Follow] = "flag", [Cmd.Formation] = "star",
    };

    /// The order keys in command view: an icon, the order and its key on each.
    void CommandPanel()
    {
        if (buttons.Count == 0) return;
        var first = buttons[0].R;
        var area = new Rectangle(first.X - U(8), first.Y - U(24), buttons[^1].R.Right - first.X + U(16), buttons[^1].R.Bottom - first.Y + U(32));
        Panel(area);
        Text(Selected.Count == 0 ? "ORDERS: ALL TROOPS" : "ORDERS: " + string.Join(" ", cards.Select((c, i) => Selected.Contains(c.S) ? $"{i + 1}" : "").Where(x => x != "")),
            area.X + U(8), area.Y + U(8), Gold, 1);
        foreach (var (r, c) in buttons)
        {
            bool hover = r.Contains(Mouse);
            Rect(new Rectangle(r.X + U(1), r.Y + U(2), r.Width, r.Height), Color.Black * 0.4f);
            kit.VGrad(r, hover ? Kit.Mul(Gold, 0.5f) : new Color(36, 36, 42), hover ? Kit.Mul(Gold, 0.2f) : Kit.IronDark, 1);
            Frame(r, hover ? Gold : Color.Black * 0.7f, U(1));
            Rect(new Rectangle(r.X + U(1), r.Y + U(1), r.Width - U(2), U(1)), Color.White * (hover ? 0.3f : 0.1f));
            var (label, act) = Orders[c];
            kit.Icon(OrderIcons[c], r.X + U(5), r.Y + U(8), hover ? Color.White : Style.Text, 1);
            Text(label, r.X + U(17), r.Y + U(9), hover ? Color.White : Style.Text, 1);
            TextR(Ctl.Name(act), r.Right - U(5), r.Y + U(9), hover ? Kit.Parchment : Dim, 1);
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
            string label = !own ? s.Name[..1] : s.Division != null ? DivisionTag(s.Division) : $"{mine.IndexOf(s) + 1}";
            if (own && s.Division != null && s.Division == Div) Frame(new Rectangle(flag.X - U(1), flag.Y - U(1), flag.Width + U(2), flag.Height + U(2)), Gold, U(1));
            TextC(label, flag.X + flag.Width / 2f, flag.Y + U(4), Color.White, 1);
            int morale = AvgMorale(s.Units);
            Bar(new Rectangle(flag.X, flag.Bottom + U(2), flag.Width, U(3)), morale / 100f, MoraleColor(morale));
            if (s.Target != null && own) Text("ATK", flag.Right + U(3), flag.Y + U(4), Foe, 1);
        }
    }

    static string StateOf(Squad s) => s.Alive == 0 ? "BROKEN" : s.Units.Count(u => u.Active && u.Routing) > s.Alive ? "ROUTING"
        : s.Order switch { Order.Charge => "CHARGING", Order.Follow => "FOLLOWING", _ => "HOLDING" };

    static string FormationName(Formation f) => f switch { Formation.ShieldWall => "SHIELD WALL", Formation.Square => "SQUARE", Formation.Wedge => "WEDGE", Formation.Loose => "LOOSE", _ => "LINE" };

    /// Hovering a company in command view: what it is and how it stands.
    void SquadTooltip()
    {
        if (HoverSquad is not Squad s || s.Units.Count == 0) return;
        var t = s.Units[0].Type;
        var act = s.Units.Where(u => u.Active).ToList();
        int morale = act.Count == 0 ? 0 : (int)act.Average(u => u.Morale), stamina = act.Count == 0 ? 0 : (int)act.Average(u => u.Stamina);
        var lines = new List<(string, Color)>
        {
            ((s.Company?.Name is { Length: > 0 } n ? n + ": " : "") + t.Name.ToUpperInvariant(), s.Team == 0 ? Friendly : Foe),
            ($"TIER {Style.Numerals[Math.Clamp(t.Tier, 0, 7)]} {t.Class.ToString().ToUpperInvariant()}   {s.Alive}/{s.StartCount} MEN", Color.White),
            ($"{StateOf(s)}   {FormationName(s.Formation)}", Dim),
            ($"MORALE {morale}   STAMINA {stamina}" + (t.Ammo > 0 ? $"   ARROWS {act.Sum(u => u.Ammo)}" : ""), morale < 30 ? Bad : Dim),
            ($"ARMOUR {t.Armor:0}  DAMAGE {t.Damage:0}" + (t.Range > 0 ? $"  RANGE {t.Range:0} M" : ""), Dim),
        };
        if (s.Company?.Traits is { Length: > 0 } tr) lines.Add(("OFFICER: " + string.Join(", ", tr).ToUpperInvariant(), Gold));
        int w = lines.Max(l => TW(l.Item1, 1)) + U(16), h = lines.Count * U(13) + U(10);
        var r = new Rectangle(Math.Min(Mouse.X + U(18), W - w - 4), Math.Min(Mouse.Y + U(18), H - h - 4), w, h);
        Panel(r);
        for (int i = 0; i < lines.Count; i++) Text(lines[i].Item1, r.X + U(8), r.Y + U(6) + i * U(13), lines[i].Item2, 1);
    }

    /// With one company selected: its full detail above the cards.
    void Detail()
    {
        if (Selected.Count != 1) return;
        var s = Selected.First();
        if (s.Units.Count == 0) return;
        var t = s.Units[0].Type;
        var act = s.Units.Where(u => u.Active).ToList();
        var r = new Rectangle(U(10), H - U(104) - U(118), U(280), U(110));
        Panel(r, Friendly);
        int x = r.X + U(8), y = r.Y + U(6);
        Text((s.Company?.Name ?? s.Name).ToUpperInvariant(), x, y, Gold, 1);
        y += U(14);
        Text($"{t.Name.ToUpperInvariant()}  {s.Alive}/{s.StartCount}  KILLS {s.Kills}", x, y, Color.White, 1);
        y += U(14);
        Text($"HP {t.MaxHp:0}  ARMOUR {t.Armor:0}  DMG {t.Damage:0}  REACH {t.Reach:0.0}", x, y, Dim, 1);
        y += U(14);
        Text($"{StateOf(s)}, {FormationName(s.Formation)}" + (t.Ammo > 0 ? $", {act.Sum(u => u.Ammo)} ARROWS" : ""), x, y, Dim, 1);
        y += U(14);
        float morale = act.Count == 0 ? 0 : act.Average(u => u.Morale) / 100, stamina = act.Count == 0 ? 0 : act.Average(u => u.Stamina) / 100;
        Text("MORALE", x, y, Dim, 1); Bar(new Rectangle(x + U(60), y + U(1), U(170), U(5)), morale, morale < 0.3f ? Bad : Ok);
        y += U(12);
        Text("STAMINA", x, y, Dim, 1); Bar(new Rectangle(x + U(60), y + U(1), U(170), U(5)), stamina, Warn);
        y += U(12);
        if (s.Company?.Traits is { Length: > 0 } tr) Text("OFFICER: " + string.Join(", ", tr).ToUpperInvariant(), x, y, Gold, 1);
    }
}
