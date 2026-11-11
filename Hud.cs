using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Orders the player can give, from hotkeys, the order wheel or the command-view buttons.
public enum Cmd { Charge, Advance, FormHere, Spacing, FallBack, FireAtWill, Hold, Follow, Formation, Merge, Relieve, Split, Rule }

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
        [Cmd.Merge] = ("FOLD IN", Act.Merge), [Cmd.Relieve] = ("RELIEVE", Act.Relieve), [Cmd.Split] = ("SPLIT", Act.SplitCompany), [Cmd.Rule] = ("STANDING", Act.StandingOrder),
    };

    readonly SpriteBatch sb;
    readonly Art art;
    readonly PixelFont font;

    // Per-frame state, set by the game before Draw.
    public int Ui = 1;
    public Battle B = null!;
    public HashSet<Squad> Selected = null!;
    /// The caster you are directing and the spell you picked for it (-1 = none; docs/archive/MAGIC_AURA.md 3.1).
    public Squad? CastBy;
    public int CastPick = -1;
    public Controls Ctl = null!;
    public Texture2D? Minimap;
    /// The battle speed chosen with [ and ] (1 = normal).
    public float Speed = 1;
    public bool Command, Paused, MenuOpen, HelpOpen, WheelOpen, ShowResult, InCampaign, Deploying;
    Rectangle start;
    public bool StartAt(Point p) => Deploying && start.Contains(p);
    /// The deployment toggle (docs/archive/COMBAT_QOL.md Q6): null when no layout is remembered, else whether it is in use.
    public bool? DeployLast;
    Rectangle deployToggle;
    public bool DeployToggleAt(Point p) => Deploying && DeployLast != null && deployToggle.Contains(p);
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

        int ph = B.AuraTier > 0 ? U(108) : U(94); // a row more for the aura (docs/archive/KNIGHT_ORDERS.md K1)
        player = new Rectangle(U(10), H - ph - U(10), U(280), ph);
        int ms = U(150);
        minimap = new Rectangle(W - ms - U(10), U(10), ms, ms);
        LayoutDivisions();

        if (Command && Div != null) LayoutGoals();
        else if (Command)
        {
            // On a narrow screen the keys shrink to icon and key in three columns (the order's name shows on hover).
            int cols = CompactOrders ? 3 : 2, bw = CompactOrders ? U(62) : U(112), bh = U(24);
            var order = new[] { Cmd.Charge, Cmd.Hold, Cmd.Advance, Cmd.FallBack, Cmd.Follow, Cmd.FormHere, Cmd.Spacing, Cmd.FireAtWill, Cmd.Formation, Cmd.Merge, Cmd.Relieve, Cmd.Split, Cmd.Rule };
            int rows = (order.Length + cols - 1) / cols;
            int x0 = W - cols * bw - (cols - 1) * U(4) - U(12), y0 = H - rows * (bh + U(4)) - U(10);
            for (int i = 0; i < order.Length; i++)
                buttons.Add((new Rectangle(x0 + (i % cols) * (bw + U(4)), y0 + (i / cols) * (bh + U(4)), bw, bh), order[i]));
        }

        var items = ShowResult ? (InCampaign ? new[] { MenuItem.Continue } : new[] { MenuItem.NewBattle, MenuItem.Title })
            : MenuOpen ? (InCampaign ? new[] { MenuItem.Resume, MenuItem.Controls, MenuItem.Settings, MenuItem.Withdraw } : new[] { MenuItem.Resume, MenuItem.Controls, MenuItem.Settings, MenuItem.NewBattle, MenuItem.Title })
            : Array.Empty<MenuItem>();
        start = Deploying ? new Rectangle(W / 2 - U(130), U(96), U(260), U(30)) : Rectangle.Empty;
        deployToggle = Deploying && DeployLast != null ? new Rectangle(W / 2 - U(130), U(138), U(260), U(20)) : Rectangle.Empty;
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
    public bool Over(Point p) => AlertAt(p) != null || CardAt(p) != null || ButtonAt(p) != null || DivisionAt(p) != null || GoalAt(p) != null || minimap.Contains(p) || player.Contains(p) || start.Contains(p) || deployToggle.Contains(p);

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
    void Text(string s, float x, float y, Color c, int scale = 2)
    {
        Overflow.Text(s, x, y, TW(s, scale));
        font.Draw(sb, s, new Vector2(x, y), c, scale * Ui);
    }
    int TW(string s, int scale = 2) => PixelFont.Width(s, scale * Ui);
    void TextC(string s, float cx, float y, Color c, int scale = 2) => Text(s, cx - TW(s, scale) / 2f, y, c, scale);
    void TextR(string s, float rx, float y, Color c, int scale = 2) => Text(s, rx - TW(s, scale), y, c, scale);

    static Color MoraleColor(float m) => m < Battle.RoutAt ? Bad : m < Battle.RallyAt ? Warn : Ok;

    static (string Text, Color Col) StateOf(Squad s, int active, int routing, int morale) =>
        active == 0 ? ("LOST", Dim)
        : routing * 2 > active ? ("ROUTING", Bad)
        : morale < Battle.RallyAt ? ("WAVERING", Warn)
        : s.Pressure > 1.5f ? ("FLANKED", Orange)
        : s.Plan == Plan.Reform ? ("RE-FORMING", Warn)
        : s.Broken && !s.Cavalry ? ("RANKS BROKEN", Warn)
        : s.BreakOffUntil > 0 ? ("FALLING BACK", Dim)
        : s.InContact && s.Units.Where(u => u.Active).Select(u => u.Stamina).DefaultIfEmpty(100).Average() < 25 ? ("TIRING", Warn)
        : s.Units.Count > 0 && s.Units[0].Type.Ammo > 0 && s.Units.All(u => !u.Active || u.Ammo == 0) ? ("NO ARROWS", Warn)
        : ("STEADY", Ok);

    /// A company's grade as a card tag, its colour (the higher, the brighter), and its ranks in a word or two.
    static string GradeTag(Grade g) => g switch { Grade.Rabble => "RAB", Grade.Trained => "TRN", Grade.Veteran => "VET", _ => "ELT" };
    static Color GradeColor(Grade g) => g switch { Grade.Rabble => Dim, Grade.Trained => Color.White, Grade.Veteran => Gold, _ => Orange };
    static string RanksOf(Squad s) => s.Cavalry || s.Swarm ? "" : s.Plan == Plan.Reform ? "RE-FORMING" : s.Broken ? "RANKS BROKEN" : s.Formed < 0.7f ? "WAVERING RANKS" : "FORMED";
    static string PlanOf(Squad s) => s.Plan switch
    {
        Plan.Refuse => "FACING THE FLANK", Plan.Flank => s.Bend > 0 ? "WRAPPING THEIR FLANK" : "SWINGING ONTO A FLANK", Plan.Pin => "PINNING",
        Plan.Regroup => "RE-FORMING AFTER A WIN", Plan.RollUp => "ROLLING UP THE LINE", Plan.Pursue => "CHASING THE BROKEN", Plan.Shelter => "FALLING BACK BEHIND THE FOOT",
        _ => s.Rallying ? "RALLYING" : "",
    };

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
        NamedMarks();
        BannerMark();
        if (Command) Banners();
        Balance();
        GoalLines();
        Feed();
        AlertStrip();
        MinimapPanel();
        PlayerPanel();
        FormBar();
        SpellFan();
        if (Command) DivisionMarkers(); // under the cards and panels
        Cards();
        DivisionCards();
        if (Command && Div != null) GoalPanel();
        if (GroundView && !ShowResult && !MenuOpen) GroundPanel();
        if (Command) { CommandPanel(); Detail(); if (!ShowResult && !MenuOpen) { SquadTooltip(); GroundHints(); } } else Crosshair();
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
            if (DeployLast is bool last)
            {
                bool th = deployToggle.Contains(Mouse);
                Panel(deployToggle, th ? Gold : Dim);
                TextC(last ? "DEPLOYED AS LAST TIME (CLICK: DEFAULT)" : "DEFAULT DEPLOYMENT (CLICK: AS LAST TIME)", W / 2f, deployToggle.Y + U(6), th ? Gold : Dim, 1);
            }
        }
        else if (Paused && !MenuOpen && !HelpOpen && !ShowResult)
        {
            // A planning pause (docs/archive/COMBAT_QOL.md Q4): orders given now go out together when the battle runs on.
            int riding = B.PendingDirects.Count();
            var r = new Rectangle(W / 2 - U(150), U(64), U(300), U(riding > 0 ? 54 : 42));
            Panel(r, Gold);
            TextC("PAUSED", W / 2f, r.Y + U(6), Gold);
            TextC($"GIVE YOUR ORDERS. {Ctl.Name(Act.Pause)} SENDS THEM OFF", W / 2f, r.Y + U(24), Dim, 1);
            if (riding > 0) TextC($"{riding} ORDER{(riding == 1 ? "" : "S")} WITH THE RIDERS", W / 2f, r.Y + U(36), Dim, 1);
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
            TextC(mark + B.GoalText(g), W / 2f, y, st > 0 ? Gold : st < 0 ? Dim * 0.6f : Color.White, 1);
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
        float pace = (Command && B.Player.Active ? 0.25f : 1) * Speed;
        string state = Paused ? "PAUSED" : pace != 1 ? $"X{pace:0.###}" : "";
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
            up = kit.Fit(up, room);
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
            if (!u.Active || u.IsPlayer || u.Team == 1 && !u.IsLeader && !B.Shown(u.Squad)) continue;
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
        Row("boot", B.Fevered ? "FEVER" : "STAMINA", p.Stamina / 100, B.Fevered ? Bad : Ok); // a curse on you shows in red (MA9)
        if (B.AuraTier > 0) Row("star", B.AuraBlade ? "BLADE" : B.Quenched ? "QUENCHED" : B.Spent ? "SPENT" : B.Surge >= 1 ? $"SURGE {B.Surge:0}" : "AURA", B.Aura / Battle.AuraMax, B.Quenched ? Bad : B.Spent ? Dim : Style.Aura);
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
            if (B.Mad) TextC("THE FLAME HAS YOU", W / 2f, H / 2f + U(36), Bad, 2);
            else if (B.Riposting) TextC("RIPOSTE!", W / 2f, H / 2f + U(36), Gold, 2);
            else if (B.Chain >= 1 && p.Action is UnitAction.Windup or UnitAction.Recover) TextC($"CHAIN X{B.Chain + 1}", W / 2f, H / 2f + U(36), B.Chain >= 3 ? Bad : Color.White, 1);
        }
    }

    /// Above your panel: your form slots (docs/archive/KNIGHT_ORDERS.md K2): key, name, and the wait or the aura it lacks.
    void FormBar()
    {
        if (Command || !B.Player.Active || B.FormSlot.All(f => f == null) && !B.HasRelic) return;
        var keys = new[] { Act.Form1, Act.Form2, Act.Form3, Act.GreatForm };
        int bw = U(68), bh = U(22), x = player.X, y = player.Y - bh - U(4);
        if (B.HasRelic) RelicSlot(new Rectangle(x + 4 * (bw + U(3)), y, bw, bh));
        for (int i = 0; i < 4; i++)
        {
            var f = B.FormSlot[i];
            if (f == null) continue;
            var r = new Rectangle(x, y, bw, bh);
            x += bw + U(3);
            string? why = B.CannotForm(i);
            bool ready = why == null, spent = i == 3 && B.GreatUsed;
            Rect(r, ready ? Kit.Mul(Style.Aura, 0.3f) : Color.Black * 0.5f);
            Frame(r, ready ? Style.Aura : Color.White * 0.15f, U(1));
            if (!ready && !spent && f.Cooldown > 0) Rect(new Rectangle(r.X, r.Bottom - U(2), (int)(r.Width * (1 - B.FormWait(i) / f.Cooldown)), U(2)), Style.Aura * 0.6f);
            Text(Ctl.Name(keys[i]), r.X + U(4), r.Y + U(3), ready ? Gold : Dim, 1);
            string name = f.Name.ToUpperInvariant();
            while (name.Length > 2 && TW(name, 1) > bw - U(8)) name = name[..^1];
            Text(name, r.X + U(4), r.Y + U(12), ready ? Color.White : Dim, 1);
            string cost = spent ? "SPENT" : B.FormWait(i) > 0 ? $"{B.FormWait(i):0}" : $"{f.Aura:0}";
            TextR(cost, r.Right - U(4), r.Y + U(3), ready ? Style.Aura : Dim, 1);
        }
    }

    /// The relic's slot after the forms (docs/archive/MAGIC_AURA.md 3.4): ready once a battle.
    void RelicSlot(Rectangle r)
    {
        bool ready = B.CannotRaise(B.Player) == null;
        var holy = new Color(255, 230, 150);
        Rect(r, ready ? Kit.Mul(holy, 0.3f) : Color.Black * 0.5f);
        Frame(r, ready ? holy : Color.White * 0.15f, U(1));
        Text(kit.Fit(Ctl.Name(Act.Relic) == "BACKSPACE" ? "BKSP" : Ctl.Name(Act.Relic), r.Width - U(32)), r.X + U(4), r.Y + U(3), ready ? Gold : Dim, 1);
        Text("RELIC", r.X + U(4), r.Y + U(12), ready ? Color.White : Dim, 1);
        TextR(ready ? "+30" : "SPENT", r.Right - U(4), r.Y + U(3), ready ? holy : Dim, 1);
    }

    /// The spells of the caster you are directing (docs/archive/MAGIC_AURA.md 3.1): the picked one bright, the rest with why not.
    void SpellFan()
    {
        if (!Command || CastBy is not { } s || s.Spells.Length == 0) return;
        int bw = U(150), bh = U(16), n = s.Spells.Length;
        int x = W / 2 - bw / 2, y = H / 3 - (n + 1) * bh / 2;
        var r = new Rectangle(x - U(6), y - U(20), bw + U(12), (n + 1) * bh + U(26));
        Rect(r, Color.Black * 0.6f);
        Frame(r, Style.Aura * 0.6f, U(1));
        Text($"{kit.Fit(s.Name.ToUpperInvariant(), bw - U(60))}: CAST", x, y - U(14), Gold, 1);
        TextR($"WILL {s.Will:0}", x + bw, y - U(14), new Color(120, 150, 255), 1);
        for (int i = 0; i < n; i++)
        {
            var a = s.Spells[i];
            string? why = B.CannotCast(s, i);
            bool pick = i == CastPick;
            var row = new Rectangle(x - U(2), y + i * bh, bw + U(4), bh - U(2));
            if (pick) Rect(row, Kit.Mul(Style.Aura, 0.35f));
            Text(kit.Fit(a.Name.ToUpperInvariant(), bw - U(56)), x, y + i * bh + U(3), why == null ? Color.White : Dim, 1);
            string cost = why ?? (s.ByHero[i] ? "READY" : $"{a.Will + Battle.OrderedWill:0} WILL");
            TextR(cost, x + bw, y + i * bh + U(3), why == null ? Style.Aura : Warn, 1);
        }
        TextC($"CLICK THE GROUND.  {Ctl.Name(Act.Cast)} NEXT SPELL, RMB CANCEL", W / 2f, y + n * bh + U(3), Dim, 1);
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
            if (!s.Swarm) { Rect(new Rectangle(r.X + U(3), r.Y + U(28), TW(GradeTag(s.Grade), 1) + U(3), U(10)), Color.Black * 0.6f); Text(GradeTag(s.Grade), r.X + U(4), r.Y + U(29), GradeColor(s.Grade), 1); } // drill grade
            string tag = s.Order switch { Order.Follow => "FLW", Order.Charge => s.Target != null ? "ATK" : "CHG", _ => "HLD" };
            TextR(tag, r.Right - U(4), r.Y + U(5), s.Order == Order.Charge ? Orange : Dim, 1);
            if (s.Formation != Formation.Line)
                TextR(s.Formation switch { Formation.ShieldWall => "WALL", Formation.Square => "SQUARE", Formation.Wedge => "WEDGE", _ => "LOOSE" }, r.Right - U(4), r.Y + U(14), Dim, 1);
            if (!s.FireAtWill || s.HoldSpells) TextR("HOLD", r.Right - U(4), r.Y + U(23), Warn, 1);
            if (s.Rule != StandingRule.None) TextR(Battle.RuleTag(s.Rule), r.Right - U(4), r.Y + U(32), Gold, 1); // its standing order
            if (s.Queue.Count > 0) TextR($"+{s.Queue.Count}", r.Right - U(4), r.Y + U(41), Dim, 1); // orders queued

            // The troop's name by measured width; too long, its last word ("YEOMAN ARCHERS" -> "ARCHERS"), then cut.
            string nm = s.Name.ToUpperInvariant();
            if (TW(nm, 1) > r.Width - U(2)) nm = kit.Fit(nm.Split(' ').Last(), r.Width - U(2));
            TextC(nm, r.X + r.Width / 2f, r.Y + U(52), Dim, 1);
            TextC($"{active}/{s.StartCount}", r.X + r.Width / 2f, r.Y + U(61), Color.White);
            kit.Gauge(new Rectangle(r.X + U(4), r.Y + U(76), r.Width - U(8), U(6)), active / (float)Math.Max(1, s.StartCount), cloth.Ui);
            kit.Gauge(new Rectangle(r.X + U(4), r.Y + U(84), r.Width - U(8), U(6)), morale / 100f, MoraleColor(morale));
            if (state != "STEADY") kit.Chip(r.X + r.Width / 2 - (TW(state, 1) + U(10)) / 2, r.Y - U(20), state, sc);
            EffectMarks(s, r);
        }
        if (hiddenCards > 0 && cards.Count > 0)
        {
            var last = cards[^1].R;
            TextC($"+{hiddenCards} MORE", last.X + last.Width / 2f, last.Y - U(12), Gold, 1);
        }
    }
    int hiddenCards;

    /// The effects on a company (docs/archive/COMPANIONS_MAGIC.md A6): up to four icons along the foot of its portrait, gold for
    /// a blessing, red for a curse; a mage company's Will as a thin bar under it.
    void EffectMarks(Squad s, Rectangle r)
    {
        int x = r.Right - U(4);
        foreach (var e in s.Timed.Select(t => t.Effect).Concat(s.StandingIds.Select(Effects.Get)).Distinct().Take(4))
        {
            x -= U(11);
            Rect(new Rectangle(x - U(1), r.Y + U(37), U(11), U(11)), Color.Black * 0.6f);
            kit.Icon(e.Icon, x, r.Y + U(38), e.Harmful ? Style.Bad : Gold, 1);
        }
        if (Battle.CasterCompany(s))
            kit.Gauge(new Rectangle(r.X + U(4), r.Y + U(91), r.Width - U(8), U(3)), s.Will / 100f, new Color(120, 150, 255));
    }

    /// A portrait built from the troop's own part sprites, laid out by the same body metrics as the 3D puppet
    /// (so every loadout and tier is drawn correctly), cropped to `box`.
    void Icon(UnitType t, Rectangle box) => Portrait(sb, art, t, box, Ui);

    /// `top`: metres at the top of the box (higher shows the weapon's tip, lower closes in on the face).
    public static void Portrait(SpriteBatch sb, Art art, UnitType t, Rectangle box, int k, float top = 1.92f)
    {
        var l = t.Look;
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
        Part(l.HeadKey + ".f", 0, 1.53f, l.NeutralHead ? "/n" : "/0");
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
        [Cmd.FireAtWill] = "arrow", [Cmd.Hold] = "shield", [Cmd.Follow] = "flag", [Cmd.Formation] = "star", [Cmd.Merge] = "chain", [Cmd.Relieve] = "heart", [Cmd.Split] = "tower", [Cmd.Rule] = "scroll",
    };

    /// Order keys as icons only: below about 1440 wide (at this interface scale) the full panel crowds the cards.
    bool CompactOrders => W < U(1440);

    /// The order keys in command view: an icon, the order and its key on each.
    void CommandPanel()
    {
        if (buttons.Count == 0) return;
        var first = buttons[0].R;
        int right = buttons.Max(b => b.R.Right), bottom = buttons.Max(b => b.R.Bottom); // the keys fill by column, so the last is not the far corner
        var area = new Rectangle(first.X - U(8), first.Y - U(24), right - first.X + U(16), bottom - first.Y + U(32));
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
            if (!CompactOrders) Text(label, r.X + U(17), r.Y + U(9), hover ? Color.White : Style.Text, 1);
            TextR(Ctl.Name(act), r.Right - U(5), r.Y + U(9), hover ? Kit.Parchment : Dim, 1);
        }
        if (CompactOrders && buttons.FirstOrDefault(b => b.R.Contains(Mouse)) is { R.Width: > 0 } h)
        {
            string label = Orders[h.C].Label;
            int lw = TW(label, 1) + U(12);
            var tr = new Rectangle(Math.Min(h.R.Center.X - lw / 2, W - lw - U(4)), area.Y - U(22), lw, U(18));
            Panel(tr);
            TextC(label, tr.Center.X, tr.Y + U(5), Color.White, 1);
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
        // Keep off the company cards: if the line would run into them, it sits just above them.
        s = s.Trim();
        if (cards.Count > 0 && !(Command && buttons.Count > 0) && W - U(12) - TW(s, 1) < cards[^1].R.Right + U(14)) y = cards[^1].R.Y - U(26);
        TextR(s, W - U(12), y, Color.White * 0.6f, 1);
    }

    void Banners()
    {
        var vp = new Viewport(0, 0, W, H);
        var mine = Mine;
        // Place the chosen and hovered flags first so they keep their spot; a flag that would sit on another steps up, and if
        // there is still no room it shrinks to a plain small flag. Drawn in reverse, so the chosen ones are in front (V7).
        var order = B.Squads.Where(s => s.Alive > 0).OrderBy(s => Selected.Contains(s) || s == HoverSquad ? 0 : s.Team == 0 ? 1 : 2).ToList();
        var placed = new List<(Squad S, int X, int Y, bool Small, Rectangle R)>();
        foreach (var s in order)
        {
            bool seen = B.Shown(s);
            var c = seen ? s.Centroid() : s.LastSeen; // an enemy you cannot see: its flag stays where you last saw it, faded
            var sp = vp.Project(new Vector3(c.X, B.Terrain.Height(c) + 3.4f, c.Y), Proj, View, Matrix.Identity);
            if (sp.Z < 0 || sp.Z > 1) continue;
            int x = (int)sp.X, y = (int)sp.Y;
            bool small = false;
            Rectangle R(int yy) => new(x, yy - U(24), U(26), U(22));
            for (int step = 0; ; step++)
            {
                var r = R(y);
                if (!placed.Any(o => o.R.Intersects(r))) break;
                if (step == 2) { small = true; y = (int)sp.Y; break; }
                y -= U(12);
            }
            placed.Add((s, x, y, small, small ? new Rectangle(x, y - U(12), U(12), U(12)) : R(y)));
        }
        for (int i = placed.Count - 1; i >= 0; i--) Banner(placed[i].S, placed[i].X, placed[i].Y, placed[i].Small, mine);
    }

    /// One company's flag on its pole: colour, number or letter, morale bar, honours. Wavering flags shake; routed ones droop grey.
    void Banner(Squad s, int x, int y, bool small, List<Squad> mine)
    {
        bool seen = B.Shown(s), own = s.Team == 0, sel = Selected.Contains(s);
        int morale = AvgMorale(s.Units);
        bool routed = s.Units.Count(u => u.Active && u.Routing) * 2 > s.Alive, wavering = !routed && morale < 35;
        var col = own ? Friendly : seen ? Foe : Color.Lerp(Foe, Dim, 0.6f);
        if (routed) { col = Color.Lerp(col, Dim, 0.65f); y += U(8); }
        if (wavering) x += (int)(MathF.Sin(Ironfield.Ui.Now * 11 + s.GetHashCode()) * U(1.5f));
        if (small)
        {
            Rect(new Rectangle(x, y - U(12), U(1), U(13)), Color.Black * 0.6f);
            Rect(new Rectangle(x + U(1), y - U(12), U(8), U(5)), col * 0.8f);
            return;
        }
        Rect(new Rectangle(x, y - U(24), U(2), U(26)), Color.Black * 0.7f); // pole
        var flag = new Rectangle(x + U(2), y - U(24), U(24), U(15));
        if (routed) // the flag hangs: a narrow drooping cloth, no letter
        {
            Rect(new Rectangle(flag.X, flag.Y, U(9), U(18)), col * 0.8f);
            return;
        }
        Rect(flag, col * 0.9f);
        Frame(flag, sel ? Gold : Color.Black * 0.6f, U(sel ? 2 : 1));
        string label = !own ? s.Name[..1] : s.Division != null ? DivisionTag(s.Division) : $"{mine.IndexOf(s) + 1}";
        if (own && s.Division != null && s.Division == Div) Frame(new Rectangle(flag.X - U(1), flag.Y - U(1), flag.Width + U(2), flag.Height + U(2)), Gold, U(1));
        if (flag.X >= 0 && flag.Right <= W) TextC(label, flag.X + flag.Width / 2f, flag.Y + U(4), Color.White, 1); // cut by the edge: no letter
        // The company's history on its flag (L5): a gold fringe after ten battles, a streamer for each battle honour.
        if (own && s.Company is Company co)
        {
            if (co.Fringe) Rect(new Rectangle(flag.X, flag.Bottom - U(2), flag.Width, U(2)), Gold);
            for (int i = 0; i < Math.Min(3, co.Streamers); i++) Rect(new Rectangle(flag.Right, flag.Y + U(2) + i * U(4), U(6 - i), U(2)), Gold);
        }
        Bar(new Rectangle(flag.X, flag.Bottom + U(2), flag.Width, U(3)), morale / 100f, MoraleColor(morale));
        if (s.Target != null && own) Text("ATK", flag.Right + U(3), flag.Y + U(4), Foe, 1);
    }

    static string StateOf(Squad s) => s.Alive == 0 ? "BROKEN" : s.Units.Count(u => u.Active && u.Routing) > s.Alive ? "ROUTING"
        : s.Order switch { Order.Charge => "CHARGING", Order.Follow => "FOLLOWING", _ => "HOLDING" };

    static string FormationName(Formation f) => f switch { Formation.ShieldWall => "SHIELD WALL", Formation.Square => "SQUARE", Formation.Wedge => "WEDGE", Formation.Loose => "LOOSE", _ => "LINE" };

    /// Hovering a company in command view: what it is and how it stands.
    void SquadTooltip()
    {
        if (HoverSquad is not Squad s || s.Units.Count == 0) return;
        if (s.Team != 0) { EnemyGlass(s); return; }
        var t = s.Units[0].Type;
        var act = s.Units.Where(u => u.Active).ToList();
        int morale = act.Count == 0 ? 0 : (int)act.Average(u => u.Morale), stamina = act.Count == 0 ? 0 : (int)act.Average(u => u.Stamina);
        var lines = new List<(string, Color)>
        {
            ((s.Company?.Name is { Length: > 0 } n ? n + ": " : "") + t.Name.ToUpperInvariant(), s.Team == 0 ? Friendly : Foe),
            ($"TIER {Style.Numerals[Math.Clamp(t.Tier, 0, 7)]} {t.Class.ToString().ToUpperInvariant()}   {s.Alive}/{s.StartCount} MEN", Color.White),
            ($"{StateOf(s)}   {FormationName(s.Formation)}", Dim),
            ((s.Swarm ? "A MOB" : Battle.GradeName(s.Grade).ToUpperInvariant() + (RanksOf(s) is { Length: > 0 } rk ? "   " + rk : "")) + (PlanOf(s) is { Length: > 0 } pl ? "   " + pl : ""), s.Swarm ? Dim : GradeColor(s.Grade)),
            ($"MORALE {morale}   STAMINA {stamina}" + (t.Ammo > 0 ? $"   ARROWS {act.Sum(u => u.Ammo)}" : ""), morale < 30 ? Bad : Dim),
            ($"ARMOUR {t.Armor:0}  DAMAGE {t.Damage:0}" + (t.Range > 0 ? $"  RANGE {t.Range:0} M" : ""), Dim),
        };
        if (s.Company?.Traits is { Length: > 0 } tr) lines.Add(("OFFICER: " + string.Join(", ", tr).ToUpperInvariant(), Gold));
        int w = lines.Max(l => TW(l.Item1, 1)) + U(16), h = lines.Count * U(13) + U(10);
        var r = new Rectangle(Math.Min(Mouse.X + U(18), W - w - 4), Math.Min(Mouse.Y + U(18), H - h - 4), w, h);
        Panel(r);
        for (int i = 0; i < lines.Count; i++) Text(lines[i].Item1, r.X + U(8), r.Y + U(6) + i * U(13), lines[i].Item2, 1);
    }

    /// Hovering an enemy company (docs/archive/COMBAT_QOL.md Q5): what your side can tell of it. Its numbers are a guess, its
    /// heart a word; one not seen of late shows only what it is.
    void EnemyGlass(Squad s)
    {
        var t = s.Units[0].Type;
        bool seen = B.Visible(s, 0);
        int alive = s.Alive, about = alive < 10 ? alive : (int)MathF.Round(alive / 5f) * 5;
        float morale = s.Units.Where(u => u.Active && !u.Routing).Select(u => u.Morale).DefaultIfEmpty(0).Average();
        string heart = morale >= 60 ? "STEADY" : morale >= Battle.RallyAt ? "SHAKEN" : "WAVERING";
        var lines = new List<(string, Color)>
        {
            ((s.Company?.Name is { Length: > 0 } n ? n + ": " : "") + t.Name.ToUpperInvariant(), Foe),
            ($"TIER {Style.Numerals[Math.Clamp(t.Tier, 0, 7)]} {t.Class.ToString().ToUpperInvariant()}   " + (seen ? (alive < 10 ? $"{alive} MEN" : $"ABOUT {about} MEN") : "? MEN"), Color.White),
            (seen ? $"{StateOf(s)}   {FormationName(s.Formation)}   {heart}" : "NOT SEEN OF LATE", seen && heart != "STEADY" ? Warn : Dim),
            ($"ARMOUR {t.Armor:0}  DAMAGE {t.Damage:0}" + (t.Range > 0 ? $"  RANGE {t.Range:0} M" : ""), Dim),
        };
        if (t.Style != AttackStyle.None) lines.Add(("ATTACK: " + Battle.StyleText(t.Style).name.ToUpperInvariant(), Gold));
        if (t.Passives is { Length: > 0 } ps) lines.Add(("TRAITS: " + string.Join(", ", ps.Select(p => Effects.Has(p) ? Effects.Get(p).Name : p)).ToUpperInvariant(), Gold));
        if (seen && s.Swarm) lines.Add(("A MOB", Dim)); else if (seen) lines.Add((Battle.GradeName(s.Grade).ToUpperInvariant(), GradeColor(s.Grade)));
        int w = lines.Max(l => TW(l.Item1, 1)) + U(16), h = lines.Count * U(13) + U(10);
        var r = new Rectangle(Math.Min(Mouse.X + U(18), W - w - 4), Math.Min(Mouse.Y + U(18), H - h - 4), w, h);
        Panel(r, Foe);
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
        Text($"{(s.Swarm ? "" : Battle.GradeName(s.Grade).ToUpperInvariant() + ", ")}{StateOf(s)}, {FormationName(s.Formation)}" + (t.Ammo > 0 ? $", {act.Sum(u => u.Ammo)} ARROWS" : ""), x, y, Dim, 1);
        y += U(14);
        float morale = act.Count == 0 ? 0 : act.Average(u => u.Morale) / 100, stamina = act.Count == 0 ? 0 : act.Average(u => u.Stamina) / 100;
        Text("MORALE", x, y, Dim, 1); Bar(new Rectangle(x + U(60), y + U(1), U(170), U(5)), morale, morale < 0.3f ? Bad : Ok);
        y += U(12);
        Text("STAMINA", x, y, Dim, 1); Bar(new Rectangle(x + U(60), y + U(1), U(170), U(5)), stamina, Warn);
        y += U(12);
        if (s.Company?.Traits is { Length: > 0 } tr) Text("OFFICER: " + string.Join(", ", tr).ToUpperInvariant(), x, y, Gold, 1);
    }
}
