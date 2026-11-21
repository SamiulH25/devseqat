using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Orders the player can give, from hotkeys, the order wheel or the command-view buttons.
public enum Cmd
{
    Charge, AttackMove, Advance, FormHere, Spacing, FallBack, FireAtWill, Hold, Follow, Formation, Merge, Relieve, Split, Rule,
    // the order deck only (command view): stakes, a cast, and a formation picked outright
    Stakes, Cast, ShapeLine, ShapeWall, ShapeSquare, ShapeWedge, ShapeLoose,
}

public enum MenuItem { Resume, Controls, NewBattle, Quit, Withdraw, Continue, Title, Settings }

/// All 2D interface of a battle. It is two HUDs that never show together (docs/archive/PLAYTEST.md PT4, "As rebuilt"):
/// the command view (Hud.Deck.cs: top bar with the clock and speeds, minimap, company cards, the captain's plate, the order
/// deck) and the fight view (Hud.Fight.cs: vitals, the arms that listen, the order ring). This file holds what both share:
/// layout and hit-testing, primitives, flags on the field, hover cards. Layout is in 720p units times `Ui`.
sealed partial class Hud
{
    // Colours come from the design tokens (Style.cs, docs/DESIGN.md).
    public static readonly Color Gold = Style.Accent, Friendly = Style.Friendly, Foe = Style.Foe, Ok = Style.Ok, Warn = Style.Warn,
        Bad = Style.Bad, Dim = Style.Dim, Orange = Style.Orange, HorseCol = Style.HorseBar;
    static readonly Color PanelBg = Style.Panel, Edge = Style.PanelEdge;

    public static readonly Dictionary<Cmd, (string Label, Act? Key)> Orders = new()
    {
        [Cmd.Charge] = ("CHARGE", Act.Charge), [Cmd.AttackMove] = ("ATTACK MOVE", null), [Cmd.Advance] = ("ADVANCE", Act.Advance),
        [Cmd.FormHere] = ("FORM ON ME", Act.FormHere), [Cmd.Spacing] = ("SPACING", Act.Spacing), [Cmd.FallBack] = ("FALL BACK", Act.FallBack),
        [Cmd.FireAtWill] = ("FIRE / HOLD", Act.FireAtWill), [Cmd.Hold] = ("HOLD", Act.HoldPosition), [Cmd.Follow] = ("FOLLOW ME", Act.Follow),
        [Cmd.Formation] = ("FORMATION", Act.Formation), [Cmd.Merge] = ("FOLD IN", Act.Merge), [Cmd.Relieve] = ("RELIEVE", Act.Relieve),
        [Cmd.Split] = ("SPLIT", Act.SplitCompany), [Cmd.Rule] = ("STANDING", Act.StandingOrder),
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
    public int SpeedIdx = 1;
    public bool CapWheelOpen;
    public bool Command, Paused, MenuOpen, HelpOpen, WheelOpen, ShowResult, InCampaign, Deploying, AttackMoveMode;
    public string? CombatPrompt;
    public float CombatPromptAt = -99;
    /// Fight view: how long the hold key has been down (under 0 = not held), and the enemy company you look at.
    public float HoldPlace = -1;
    public Squad? LookFoe;
    Rectangle start;
    public bool StartAt(Point p) => Deploying && start.Contains(p);
    /// The deployment toggle (docs/archive/COMBAT_QOL.md Q6): null when no layout is remembered, else whether it is in use.
    public bool? DeployLast;
    Rectangle deployToggle;
    public bool DeployToggleAt(Point p) => Deploying && DeployLast != null && deployToggle.Contains(p);
    public int WheelSector = -1;
    public Point? WheelCenter;
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
    Rectangle minimap, player, pauseBtn, modeBtn, captainBtn, topBar, deck;
    readonly List<(Rectangle R, int I)> speedBtns = new();
    int W, H;

    /// The shared interface kit (Ui + Kit), drawing into this HUD's SpriteBatch; the HUD does its own hit-testing.
    readonly Ui kit;

    public Hud(SpriteBatch sb, Art art, PixelFont font) { this.sb = sb; this.art = art; this.font = font; kit = new Ui(sb, art, font); }

    int U(float v) => (int)(v * Ui);
    readonly List<Squad> mineCache = new();
    readonly List<Squad> bannerOrder = new();
    readonly List<(Squad S, int X, int Y, bool Small, Rectangle R)> bannerPlaced = new();
    const int MiniGrid = 75;
    readonly byte[] miniGrid = new byte[MiniGrid * MiniGrid];
    readonly List<EffectDef> cardEffects = new();
    List<Squad> Mine
    {
        get { mineCache.Clear(); foreach (var s in B.Commanded) mineCache.Add(s); return mineCache; }
    }

    // ---------- layout & hit testing ----------

    public void Layout(Viewport vp)
    {
        W = vp.Width;
        H = vp.Height;
        Ui = Settings.UiFor(H);
        cards.Clear(); buttons.Clear(); menu.Clear(); speedBtns.Clear(); armChips.Clear();
        pauseBtn = modeBtn = captainBtn = topBar = deck = Rectangle.Empty;
        hiddenCards = 0;
        int ms = U(150);
        minimap = new Rectangle(W - ms - U(10), U(10), ms, ms);
        mapBtns.Clear();
        if (Command)
        {
            // Under the minimap: turn the view left, the ground map, turn it right.
            int bw = (ms - U(8)) / 3;
            for (int i = 0; i < 3; i++) mapBtns.Add(new Rectangle(minimap.X + i * (bw + U(4)), minimap.Bottom + U(8), bw, U(13)));
        }
        if (Command) LayoutDeck(); else LayoutFight();
        LayoutDivisions();
        if (Command && Div != null) LayoutGoals();

        var items = ShowResult ? (InCampaign ? new[] { MenuItem.Continue } : new[] { MenuItem.NewBattle, MenuItem.Title })
            : MenuOpen ? (InCampaign ? new[] { MenuItem.Resume, MenuItem.Controls, MenuItem.Settings, MenuItem.Withdraw } : new[] { MenuItem.Resume, MenuItem.Controls, MenuItem.Settings, MenuItem.NewBattle, MenuItem.Title })
            : Array.Empty<MenuItem>();
        start = Deploying ? new Rectangle(W / 2 - U(130), U(96) + Drop, U(260), U(30)) : Rectangle.Empty;
        deployToggle = Deploying && DeployLast != null ? new Rectangle(W / 2 - U(130), U(138) + Drop, U(260), U(20)) : Rectangle.Empty;
        LayoutCommand(); // the tray, ADD on the cards, a sergeant's question (Hud.Command.cs)
        int mw = U(220), mh = U(30), top = H / 2 - items.Length * (mh + U(8)) / 2 + U(10);
        if (ShowResult) // side by side along the bottom of the result
            for (int i = 0; i < items.Length; i++) menu.Add((new Rectangle(W / 2 - (items.Length * (mw + U(10)) - U(10)) / 2 + i * (mw + U(10)), ResultBox.Bottom - U(44), mw, mh), items[i]));
        else for (int i = 0; i < items.Length; i++) menu.Add((new Rectangle((W - mw) / 2, top + i * (mh + U(8)), mw, mh), items[i]));
    }

    public Squad? CardAt(Point p) => cards.FirstOrDefault(c => c.R.Contains(p)).S;
    readonly List<Rectangle> mapBtns = new();
    /// The buttons under the minimap: 0 turn left, 1 the ground map, 2 turn right.
    public int? MapButtonAt(Point p) { for (int i = 0; i < mapBtns.Count; i++) if (mapBtns[i].Contains(p)) return i; return null; }
    /// The order deck's title, a button back to all companies while some are chosen.
    public bool DeckTitleAt(Point p) => Command && !deck.IsEmpty && Selected.Count > 0 && new Rectangle(deck.X, deck.Y, deck.Width, U(20)).Contains(p);
    public Cmd? ButtonAt(Point p) => buttons.Where(b => b.R.Contains(p)).Select(b => (Cmd?)b.C).FirstOrDefault();
    public MenuItem? MenuAt(Point p) => menu.Where(m => m.R.Contains(p)).Select(m => (MenuItem?)m.M).FirstOrDefault();
    public bool PauseAt(Point p) => Command && !ShowResult && pauseBtn.Contains(p);
    public int? SpeedAt(Point p) => Command && !ShowResult ? speedBtns.Where(s => s.R.Contains(p)).Select(s => (int?)s.I).FirstOrDefault() : null;
    /// The captain's plate: FIGHT takes you into his body, his picture looks at him.
    public bool ModeAt(Point p) => Command && !ShowResult && modeBtn.Contains(p);
    public bool CaptainAt(Point p) => Command && !ShowResult && captainBtn.Contains(p);
    public Vector2? MinimapAt(Point p) => Command && minimap.Contains(p)
        ? World.MinimapToWorld(new Vector2((p.X - minimap.X) / (float)minimap.Width, (p.Y - minimap.Y) / (float)minimap.Height)) : null;
    /// The mouse is on the interface, not the field (command view; the fight view has no cursor).
    public bool Over(Point p) => Command && (topBar.Contains(p) || deck.Contains(p) || player.Contains(p) || minimap.Contains(p) || MapButtonAt(p) != null)
        || AlertAt(p) != null || CardAt(p) != null || ButtonAt(p) != null || DivisionAt(p) != null || GoalAt(p) != null || GoalCloseAt(p) || OverCommand(p)
        || start.Contains(p) || deployToggle.Contains(p);

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

    static (string Text, Color Col) StateOf(Squad s, int active, int routing, int morale)
    {
        if (active == 0) return ("LOST", Dim);
        if (routing * 2 > active) return ("ROUTING", Bad);
        if (morale < Battle.RallyAt) return ("WAVERING", Warn);
        if (s.Pressure > 1.5f) return ("FLANKED", Orange);
        if (s.Plan == Plan.Reform) return ("RE-FORMING", Warn);
        if (s.Broken && !s.Cavalry) return ("RANKS BROKEN", Warn);
        if (s.BreakOffUntil > 0) return ("FALLING BACK", Dim);
        float stamSum = 0; int actN = 0;
        bool hasAmmo = s.Units.Count > 0 && s.Units[0].Type.Ammo > 0, anyAmmo = false;
        foreach (var u in s.Units)
        {
            if (!u.Active) continue;
            actN++;
            stamSum += u.Stamina;
            if (u.Ammo > 0) anyAmmo = true;
        }
        if (s.InContact && (actN == 0 ? 100f : stamSum / actN) < 25) return ("TIRING", Warn);
        if (hasAmmo && !anyAmmo) return ("NO ARROWS", Warn);
        return ("STEADY", Ok);
    }

    /// A company's grade colour (the higher, the brighter), and its ranks in a word or two.
    static Color GradeColor(Grade g) => g switch { Grade.Rabble => Dim, Grade.Trained => Color.White, Grade.Veteran => Gold, _ => Orange };
    static string RanksOf(Squad s) => s.Cavalry || s.Swarm ? "" : s.Plan == Plan.Reform ? "RE-FORMING" : s.Broken ? "RANKS BROKEN" : s.Formed < 0.7f ? "WAVERING RANKS" : "FORMED";
    static string PlanOf(Squad s) => s.Plan switch
    {
        Plan.Refuse => "FACING THE FLANK", Plan.Flank => s.Bend > 0 ? "WRAPPING THEIR FLANK" : "SWINGING ONTO A FLANK", Plan.Pin => "PINNING",
        Plan.Regroup => "RE-FORMING AFTER A WIN", Plan.RollUp => "ROLLING UP THE LINE", Plan.Pursue => "CHASING THE BROKEN", Plan.Shelter => "FALLING BACK BEHIND THE FOOT",
        _ => s.Rallying ? "RALLYING" : "",
    };

    static int AvgMorale(List<Unit> us)
    {
        float sum = 0; int n = 0;
        foreach (var u in us) if (u.Active) { sum += u.Morale; n++; }
        return n == 0 ? 0 : (int)(sum / n);
    }

    static int AvgMorale(IReadOnlyList<Squad> squads)
    {
        float sum = 0; int n = 0;
        foreach (var s in squads) foreach (var u in s.Units) if (u.Active) { sum += u.Morale; n++; }
        return n == 0 ? 0 : (int)(sum / n);
    }

    // ---------- draw ----------

    public void Draw()
    {
        kit.Reset(Ui, W, H, Mouse);
        SampleStrength();
        SampleFights();
        sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        SergeantPennants();
        BannerMark();
        if (!Deploying) FightMarks();
        NamedMarks(); // over the fight marks: a named man's tag, on its dark strip, reads over the crossed swords
        VolleyRings(); // a volley marked on you (C13): on the ground, under the panels
        DuelBars();
        if (Command) DrawCommand(); else DrawFight();
        if (WheelOpen) { if (Div != null) GoalWheel(); else Wheel(); }
        else if (CapWheelOpen) CaptainWheel();
        if (B.DownSince >= 0) // the last stand (P6)
        {
            var r = new Rectangle(W / 2 - U(160), H / 2 - U(60), U(320), U(34));
            Panel(r, Bad);
            TextC($"YOU ARE DOWN: {Ctl.Name(Act.Rally)} TO FIGHT ON ({MathF.Max(0, Battle.LastStandWindow - (B.Time - B.DownSince)):0})", W / 2f, r.Y + U(12), Color.White, 1);
        }
        if (Box is Rectangle box) { Rect(box, Gold * 0.12f); Frame(box, Gold, 1); }
        if (Deploying)
        {
            var banner = new Rectangle(W / 2 - U(230), U(60) + Drop, U(460), U(72));
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
            var r = PauseBox;
            Panel(r, Gold);
            TextC("PAUSED", W / 2f, r.Y + U(6), Gold);
            TextC($"GIVE YOUR ORDERS. {Ctl.Name(Act.Pause)} SENDS THEM OFF", W / 2f, r.Y + U(24), Dim, 1);
            if (riding > 0) TextC($"{riding} ORDER{(riding == 1 ? "" : "S")} WAITING", W / 2f, r.Y + U(36), Dim, 1);
        }
        if (ShowResult) Result();
        else if (HelpOpen) Help();
        else if (MenuOpen) Menu();
        sb.End();
    }

    Rectangle PauseBox => new(W / 2 - U(150), U(64) + Drop, U(300), U(B.PendingDirects.Any() ? 54 : 42));

    /// The command view: the field's flags and marks, then the top bar, the minimap and sergeants, and the bottom row
    /// (captain, companies, orders). Nothing of the fight view shows here.
    void DrawCommand()
    {
        Banners();
        TopBar();
        GoalLines();
        AlertStrip();
        MinimapPanel();
        MapButtons();
        PostZones();
        DivisionRings();
        DivisionMarkers();
        Cards();
        CaptainPlate();
        Tray();
        DivisionCards();
        if (Div != null) GoalPanel(); else OrderDeck();
        if (GroundView && !ShowResult && !MenuOpen) GroundPanel();
        SpellFan();
        if (!ShowResult && !MenuOpen) { SquadTooltip(); DivisionTooltip(); GroundHints(); }
        DragGhost();
    }

    /// The fight view: you are the captain. Vitals, the arms that listen to you and the reticle; no cards, no map, no buttons.
    void DrawFight()
    {
        FightTop();
        AlertStrip(2);
        Vitals();
        if (B.AuraTier > 0) FormBar();
        ArmChips();
        if (Div != null) SergeantFieldBadge();
        OpponentMark();
        BlowNumbers();
        MarkedMarks();
        Crosshair();
        SwingWords();
        OrderToast();
        FightHints();
    }

    /// The fighting strength of a side: men standing, weighted by health and arm (a rider counts for more than a bowman).
    float Strength(int team)
    {
        float s = 0;
        foreach (var u in B.Units)
        {
            if (u.Team != team || !u.Active || u.Routing) continue;
            s += u.Hp / u.Type.MaxHp * (u.Type.Class switch { UnitClass.Cavalry => 2.2f, UnitClass.Captain => 3f, UnitClass.Archer => 0.9f, _ => 1f });
        }
        return s;
    }

    Heraldry SideArms(int team) => Style.Sides[Math.Clamp(B.Options.Palette[Math.Clamp(team, 0, 1)], 0, Style.Sides.Length - 1)];

    /// Your personal goals for this battle (P6), under the balance bar: open in white, met in gold, lost struck grey.
    void GoalLines()
    {
        int y = Deploying ? (DeployLast != null ? U(186) : U(158)) + Drop : Paused ? PauseBox.Bottom + U(6) : (B.Wall != null && !Command ? U(80) : U(60)) + Drop;
        foreach (var g in B.Goals)
        {
            int st = B.GoalState(g);
            string mark = st > 0 ? "DONE: " : st < 0 ? "LOST: " : "GOAL: ";
            TextC(mark + B.GoalText(g), W / 2f, y, st > 0 ? Gold : st < 0 ? Dim * 0.6f : Color.White, 1);
            y += U(11);
        }
    }

    /// Under the minimap: turn the view (hold), and the ground map (lit while it is shown).
    void MapButtons()
    {
        for (int i = 0; i < mapBtns.Count; i++)
        {
            var r = mapBtns[i];
            bool hover = r.Contains(Mouse), on = i == 1 && GroundView;
            Rect(r, on ? Kit.Mul(Gold, 0.35f) : hover ? new Color(60, 60, 68) : Color.Black * 0.6f);
            Frame(r, on || hover ? Gold : Color.White * 0.2f, U(1));
            string t = i == 0 ? "< TURN" : i == 1 ? "GROUND" : "TURN >";
            TextC(t, r.Center.X, r.Y + U(4), on ? Gold : hover ? Color.White : Style.Text, 1);
        }
    }

    void MinimapPanel()
    {
        var r = minimap;
        var frame = new Rectangle(r.X - U(5), r.Y - U(5), r.Width + U(10), r.Height + U(10));
        Panel(frame);
        if (Minimap != null) sb.Draw(Minimap, r, Color.White);
        Frame(r, Color.Black * 0.7f, U(1));
        int c4 = U(4);
        var cornerCol = Kit.Gilt * 0.8f;
        Rect(new Rectangle(frame.X, frame.Y, c4, c4), cornerCol);
        Rect(new Rectangle(frame.Right - c4, frame.Y, c4, c4), cornerCol);
        Rect(new Rectangle(frame.X, frame.Bottom - c4, c4, c4), cornerCol);
        Rect(new Rectangle(frame.Right - c4, frame.Bottom - c4, c4, c4), cornerCol);
        Vector2 M(Vector2 w) { var m = World.WorldToMinimap(w); return new Vector2(r.X + m.X * r.Width, r.Y + m.Y * r.Height); }
        Array.Clear(miniGrid);
        foreach (var u in B.Units)
        {
            if (!u.Active || u.IsPlayer || u.Team == 1 && !u.IsLeader && !B.Shown(u.Squad)) continue;
            var wm = World.WorldToMinimap(u.Pos);
            if (wm.X < 0 || wm.X >= 1 || wm.Y < 0 || wm.Y >= 1) continue;
            int gx = Math.Clamp((int)(wm.X * MiniGrid), 0, MiniGrid - 1);
            int gy = Math.Clamp((int)(wm.Y * MiniGrid), 0, MiniGrid - 1);
            byte prio = u.Team == 0 && u.Squad != null && Selected.Contains(u.Squad) ? (byte)3 : u.Routing ? (byte)1 : (byte)2;
            int idx = gy * MiniGrid + gx;
            if (miniGrid[idx] >= prio) continue;
            miniGrid[idx] = prio;
            int px = (int)(r.X + wm.X * r.Width), py = (int)(r.Y + wm.Y * r.Height);
            var c = u.Routing ? Dim : u.Team == 1 ? Foe : prio == 3 ? Gold : Friendly;
            int s = U(u.Mounted ? 3 : 2);
            Rect(new Rectangle(px - s / 2, py - s / 2, s, s), c);
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
            string cost = spent ? "SPENT" : B.FormWait(i) > 0 ? $"{B.FormWait(i):0}" : $"{B.FormCost(f):0}";
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
        Text($"{kit.Fit(s.Name.ToUpperInvariant(), bw - U(118))}: CAST", x, y - U(14), Gold, 1);
        TextR($"WILL {s.Will:0}  TIDE {B.Tide:0}", x + bw, y - U(14), new Color(120, 150, 255), 1);
        for (int i = 0; i < n; i++)
        {
            var a = s.Spells[i];
            string? why = B.CannotCast(s, i);
            bool pick = i == CastPick;
            var row = new Rectangle(x - U(2), y + i * bh, bw + U(4), bh - U(2));
            if (pick) Rect(row, Kit.Mul(Style.Aura, 0.35f));
            Text(kit.Fit(a.Name.ToUpperInvariant(), bw - U(56)), x, y + i * bh + U(3), why == null ? Color.White : Dim, 1);
            bool over = why == null && B.WouldOverreach(s, i); // the Tide is short: twice the Will, and a risk (BattleTide.cs)
            string cost = why ?? (s.ByHero[i] ? "READY" : over ? $"{(a.Will + Battle.OrderedWill) * 2:0} OVERREACH" : $"{a.Will + Battle.OrderedWill:0} WILL");
            TextR(cost, x + bw, y + i * bh + U(3), why == null && !over ? Style.Aura : Warn, 1);
        }
        TextC($"CLICK THE GROUND.  {Ctl.Name(Act.Cast)} NEXT SPELL, RMB CANCEL", W / 2f, y + n * bh + U(3), Dim, 1);
    }

    /// What each of your companies was last told (its order, target, formation, place), and when that changed: a new order
    /// flashes its card and flag for half a second (docs/archive/PLAYTEST.md PT4 6).
    readonly Dictionary<Squad, (int Key, float At)> orderSeen = new();

    float? OrderFlash(Squad s)
    {
        int key = (int)s.Order * 7919 + (s.Target?.Id ?? -1) * 31 + (int)s.Formation * 131 + (int)(s.Anchor.X * 3) * 17 + (int)(s.Anchor.Y * 3);
        if (!orderSeen.TryGetValue(s, out var seen) || Ironfield.Ui.Instant) { orderSeen[s] = (key, -9); return null; } // shots freeze the clock: no flash stuck on
        if (seen.Key != key) orderSeen[s] = seen = (key, Ironfield.Ui.Now);
        float t = Ironfield.Ui.Now - seen.At;
        return t < 0.5f ? 1 - t / 0.5f : null;
    }

    /// The effects on a company (docs/archive/COMPANIONS_MAGIC.md A6): up to four icons along the foot of its portrait, gold for
    /// a blessing, red for a curse; a mage company's Will as a thin bar under it.
    void EffectMarks(Squad s, Rectangle r)
    {
        int x = r.Right - U(4);
        cardEffects.Clear();
        foreach (var t in s.Timed) if (cardEffects.Count < 4 && !cardEffects.Contains(t.Effect)) cardEffects.Add(t.Effect);
        foreach (var id in s.StandingIds) { if (cardEffects.Count >= 4) break; var ef = Effects.Get(id); if (!cardEffects.Contains(ef)) cardEffects.Add(ef); }
        foreach (var e in cardEffects)
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
    /// `palette` paints the coats and shields in a realm's colours (a `Style.Sides` index).
    public static void Portrait(SpriteBatch sb, Art art, UnitType t, Rectangle box, int k, float top = 1.92f, int palette = 0)
    {
        var l = t.Look;
        if (l.Beast) // a beast's portrait is its head, big
        {
            var src = art[l.Face switch { "spider" => "shead/n", "bear" => "bhead/n", "wyvern" => "yhead/n", _ => "whead/n" }];
            int kk = Math.Max(1, Math.Min(box.Width / src.Width, box.Height / src.Height));
            var dst = new Rectangle(box.Center.X - src.Width * kk / 2, box.Center.Y - src.Height * kk / 2, src.Width * kk, src.Height * kk);
            var tint = t.Id == "alpha_wolf" ? new Color(235, 235, 240) : l.Face == "wolf" && t.Size > 1.1f ? new Color(150, 140, 135) : Color.White;
            sb.Draw(art.Atlas, dst, src, tint);
            return;
        }
        string side = "/" + palette;
        void Part(string key, float xm, float ym, string? pal = null, Color? tint = null)
        {
            var src = art[key + (pal ?? side)];
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
        Part(l.HeadKey + ".f", 0, 1.53f, l.NeutralHead ? "/n" : side);
        if (l.Head != "none") Part($"helm.{l.Head}.f", 0, Puppet.HelmTop(l.Head) - art[$"helm.{l.Head}.f/0"].Height * Art.Px / 2);
        if (l.Plume) Part("plume", 0, Puppet.HelmTop(l.Head) + art["plume/0"].Height * Art.Px / 2 - 0.06f);
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
        hit = System.Text.RegularExpressions.Regex.Replace(hit, @"\d+ DMG\s*", "").Trim(); // the number itself pops over the man hit (Hud.Melee.cs)
        if (B.Time - at < 1.2f && hit != "") TextC(hit, cx, cy + U(50), hit.StartsWith("PARRY") ? Gold : Color.White);

        // Context prompts.
        string? prompt = null;
        if (B.Manned is { } me)
        {
            prompt = $"{Ctl.Name(Act.Interact)}  LEAVE   {Ctl.Name(Act.Attack)}  LOOSE   {me.Def.Name.ToUpperInvariant()} " + (me.Reload > 0 ? $"WINDING {100 - me.Reload / me.Def.Reload * 100:0}%" : "READY");
            Bar(new Rectangle(cx - U(24), cy + U(40), U(48), U(4)), me.Reload > 0 ? 1 - Math.Clamp(me.Reload / me.Def.Reload, 0, 1) : 1, Gold);
        }
        else if (!p.Mounted && B.EngineNear(p) is { } near) prompt = $"{Ctl.Name(Act.Interact)}  WORK {near.Def.Name.ToUpperInvariant()}";
        else if (!p.Mounted && B.LooseHorses.Any(h => Vector2.DistanceSquared(h.Pos, p.Pos) < 2.8f * 2.8f)) prompt = $"{Ctl.Name(Act.Interact)}  MOUNT HORSE";
        else if (p.Mounted && !p.Couched && p.Speed > 6) prompt = $"{Ctl.Name(Act.Couch)}  COUCH LANCE";
        else if (p.Mounted && MathF.Abs(p.Speed) < 1.5f) prompt = $"{Ctl.Name(Act.Interact)}  DISMOUNT";
        // Orders by looking (PT4): hold the hold key to plant the line where you look; charge sends them at the company you look at.
        if (HoldPlace >= 0.25f) prompt = "LET GO: HOLD THAT GROUND";
        else if (prompt == null && LookFoe is { Alive: > 0 } lf) prompt = $"{Ctl.Name(Act.Charge)}  ATTACK {lf.Name.ToUpperInvariant()}";
        if (prompt != null && !WheelOpen)
        {
            int pw = TW(prompt, 1) + U(14), py = H / 2 + U(132);
            Rect(new Rectangle(cx - pw / 2, py - U(5), pw, U(17)), Color.Black * 0.5f);
            TextC(prompt, cx, py, Color.White, 1);
        }
    }

    Vector2 WheelPos() => WheelCenter is Point p
        ? new Vector2(Math.Clamp(p.X, U(200), Math.Max(U(200), W - U(200))), Math.Clamp(p.Y, U(170), Math.Max(U(170), H - U(170))))
        : new Vector2(W / 2f, H / 2f - U(Div == null ? 96 : 0)); // the order ring sits over the captain's head, not on him

    /// Wheel sectors clockwise from the top.
    public static readonly Cmd[] WheelOrder = { Cmd.Charge, Cmd.Advance, Cmd.FormHere, Cmd.Spacing, Cmd.FallBack, Cmd.FireAtWill, Cmd.Hold, Cmd.Follow };

    void Banners()
    {
        var vp = new Viewport(0, 0, W, H);
        var mine = Mine;
        // Place the chosen and hovered flags first so they keep their spot; a flag that would sit on another steps up, and if
        // there is still no room it shrinks to a plain small flag. Drawn in reverse, so the chosen ones are in front (V7).
        bannerOrder.Clear();
        foreach (var s in B.Squads) if (s.Alive > 0) bannerOrder.Add(s);
        bannerOrder.Sort((a, b) =>
        {
            int ka = Selected.Contains(a) || a == HoverSquad ? 0 : a.Team == 0 ? 1 : 2;
            int kb = Selected.Contains(b) || b == HoverSquad ? 0 : b.Team == 0 ? 1 : 2;
            return ka.CompareTo(kb);
        });
        bannerPlaced.Clear();
        foreach (var s in bannerOrder)
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
                bool hit = false;
                for (int j = 0; j < bannerPlaced.Count; j++) if (bannerPlaced[j].R.Intersects(r)) { hit = true; break; }
                if (!hit) break;
                if (step == 2) { small = true; y = (int)sp.Y; break; }
                y -= U(12);
            }
            bannerPlaced.Add((s, x, y, small, small ? new Rectangle(x, y - U(12), U(12), U(12)) : R(y)));
        }
        for (int i = bannerPlaced.Count - 1; i >= 0; i--) Banner(bannerPlaced[i].S, bannerPlaced[i].X, bannerPlaced[i].Y, bannerPlaced[i].Small, mine);
    }

    /// One company's flag on its pole: colour, number or letter, morale bar, honours. Wavering flags shake; routed ones droop grey.
    void Banner(Squad s, int x, int y, bool small, List<Squad> mine)
    {
        bool seen = B.Shown(s), own = s.Team == 0, sel = Selected.Contains(s);
        int active = 0, routing = 0;
        float moraleSum = 0;
        foreach (var u in s.Units) if (u.Active) { active++; if (u.Routing) routing++; moraleSum += u.Morale; }
        int morale = active == 0 ? 0 : (int)(moraleSum / active);
        bool routed = routing * 2 > s.Alive, wavering = !routed && morale < 35;
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
        if (sel) // chosen: a gold glow round the flag and a pointer over it
        {
            Rect(new Rectangle(flag.X - U(3), flag.Y - U(3), flag.Width + U(6), flag.Height + U(6)), Gold * 0.45f);
            for (int i = 0; i < 4; i++) Rect(new Rectangle(flag.Center.X - U(4 - i), flag.Y - U(12) + U(i), U(8 - 2 * i), U(1)), Gold);
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
        // Its order as a picture beside the flag (PT4): sword to attack, shield to hold, flag to follow; at blows, its fight
        // mark says how it goes. The chosen ones' attack also draws a line to the foe.
        if (own && !fights.ContainsKey(s))
        {
            var (oi, oc) = OrderMark(s);
            float a = sel ? 1f : 0.6f;
            Rect(new Rectangle(flag.Right + U(2), flag.Y - U(1), U(17), U(17)), Color.Black * 0.6f * a);
            kit.Icon(oi, flag.Right + U(4), flag.Y + U(1), oc * a, 2);
            if (sel && s.Target is Squad foe && foe.Alive > 0)
            {
                var c = foe.Centroid();
                var tp = new Viewport(0, 0, W, H).Project(new Vector3(c.X, B.Terrain.Height(c) + 1, c.Y), Proj, View, Matrix.Identity);
                if (tp.Z is > 0 and < 1)
                {
                    var from = new Vector2(x, y + U(2));
                    var to = new Vector2(tp.X, tp.Y);
                    // A foe off the screen: the line stops at the edge, where its sword shows the way.
                    float m = U(14), k = 1;
                    if (to.X < m) k = MathF.Min(k, (from.X - m) / MathF.Max(1, from.X - to.X));
                    if (to.X > W - m) k = MathF.Min(k, (W - m - from.X) / MathF.Max(1, to.X - from.X));
                    float topM = U(74);
                    if (to.Y < topM) k = MathF.Min(k, (from.Y - topM) / MathF.Max(1, from.Y - to.Y));
                    if (to.Y > H - m) k = MathF.Min(k, (H - m - from.Y) / MathF.Max(1, to.Y - from.Y));
                    to = from + (to - from) * Math.Clamp(k, 0, 1);
                    var step = to - from; float len = step.Length();
                    for (float t0 = 0; t0 < len; t0 += U(12))
                    {
                        var p0 = from + step * (t0 / len); var p1 = from + step * (MathF.Min(len, t0 + U(7)) / len);
                        Line(p0, p1, Color.Black * 0.6f, U(4)); // a dark edge, so the line reads on grass and snow
                        Line(p0, p1, Bad, U(2));
                    }
                    Rect(new Rectangle((int)to.X - U(9), (int)to.Y - U(9), U(18), U(18)), Color.Black * 0.6f);
                    kit.Icon("sword", (int)to.X - U(7), (int)to.Y - U(7), Bad, 2);
                }
            }
        }
        if (own && OrderFlash(s) is float fl and > 0) Frame(new Rectangle(flag.X - U(2), flag.Y - U(2), flag.Width + U(4), flag.Height + U(4)), Gold * fl, U(2));
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
            ($"{StateOf(s)}   {FormationName(s.Formation)}   " + s.Order switch { Order.Follow => "FOLLOWING YOU", Order.Charge => s.Target != null ? "ATTACKING" : "CHARGING", _ => "HOLDING" }
                + (!s.FireAtWill ? "   HOLDING FIRE" : "") + (s.Rule != StandingRule.None ? "   RULE " + Battle.RuleTag(s.Rule) : "") + (s.Queue.Count > 0 ? $"   +{s.Queue.Count} QUEUED" : ""), Dim),
            ((s.Swarm ? "A MOB" : Battle.GradeName(s.Grade).ToUpperInvariant() + (RanksOf(s) is { Length: > 0 } rk ? "   " + rk : "")) + (PlanOf(s) is { Length: > 0 } pl ? "   " + pl : ""), s.Swarm ? Dim : GradeColor(s.Grade)),
            ($"MORALE {morale}   STAMINA {stamina}" + (t.Ammo > 0 ? $"   ARROWS {act.Sum(u => u.Ammo)}" : ""), morale < 30 ? Bad : Dim),
            ($"ARMOUR {t.Armor:0}  DAMAGE {t.Damage:0}" + (t.Range > 0 ? $"  RANGE {t.Range:0} M" : ""), Dim),
        };
        if (fights.TryGetValue(s, out var fight)) lines.Insert(3, (FightLine(s, fight), Standing(s, fight).Col));
        if (s.Company?.Traits is { Length: > 0 } tr) lines.Add(("OFFICER: " + string.Join(", ", tr).ToUpperInvariant(), Gold));
        // Its sergeant (OF5): what he is doing, or how long until another man steps up.
        if (s.SgtDoing == SgtTask.Down) lines.Add(($"SERGEANT DOWN: A GRADE WORSE FOR {MathF.Max(0, s.SgtDownAt + Battle.StepUpTime(s) - B.Time):0} S", Bad));
        else if (Battle.SergeantUp(s)) lines.Add(("SERGEANT: " + (s.SgtDoing == SgtTask.None ? "KEEPING ORDER" : Battle.SergeantText(s.SgtDoing)), Dim));
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

}
