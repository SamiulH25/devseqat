using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud, the command view (docs/archive/PLAYTEST.md PT4, "As rebuilt"): you are the general, with a cursor. The company cards along
/// the top (left of the minimap), and one row along the bottom: the captain's plate (how he fares, FIGHT to step into him),
/// the plate with time, the balance and the clock, and the order deck, a grid of picture buttons that shows only what the
/// chosen companies can do and lights what they are doing. (The fight view has it the other way: balance and speed at the
/// top, its arms along the bottom.)
sealed partial class Hud
{
    /// An order as the deck and the ring show it: name, the short word under its picture, picture, key, and a line for the hover card.
    public record OrderInfo(string Name, string Short, string Icon, Act? Key, string Tip);

    public static readonly Dictionary<Cmd, OrderInfo> Info = new()
    {
        [Cmd.Charge] = new("FULL CHARGE", "CHARGE", "fire", Act.FullCharge, "EVERY MAN RUNS IN: NO LINE KEPT"),
        [Cmd.Engage] = new("ENGAGE", "ENGAGE", "sword", Act.Charge, "ATTACK THE NEAREST ENEMY, KEEPING RANKS IF DRILLED"),
        [Cmd.AttackMove] = new("ATTACK MOVE", "A-MOVE", "up", null, "CLICK: MARCH THERE, CHARGE WHAT COMES NEAR"),
        [Cmd.Advance] = new("ADVANCE", "ADVANCE", "step", Act.Advance, "STEP 15 M TOWARD THE ENEMY"),
        [Cmd.Hold] = new("HOLD", "HOLD", "shield", Act.HoldPosition, "STAND HERE AND KEEP YOUR RANKS"),
        [Cmd.Follow] = new("FOLLOW ME", "FOLLOW", "flag", Act.Follow, "FALL IN BEHIND THE CAPTAIN"),
        [Cmd.FallBack] = new("FALL BACK", "RETREAT", "back", Act.FallBack, "STEP 20 M AWAY FROM THE ENEMY"),
        [Cmd.FormHere] = new("FORM ON ME", "ON ME", "crown", Act.FormHere, "DRAW UP IN FRONT OF THE CAPTAIN"),
        [Cmd.ShapeLine] = new("LINE", "LINE", "f_line", null, "THE PLAIN LINE: GOOD AT MOST THINGS"),
        [Cmd.ShapeWall] = new("SHIELD WALL", "WALL", "f_wall", null, "LOCKED SHIELDS: TURNS ARROWS, MOVES SLOWLY"),
        [Cmd.ShapeSquare] = new("SQUARE", "SQUARE", "f_square", null, "NO FLANK TO TURN: STANDS AGAINST HORSE"),
        [Cmd.ShapeWedge] = new("WEDGE", "WEDGE", "f_wedge", null, "A POINT TO BREAK INTO A LINE"),
        [Cmd.ShapeLoose] = new("OPEN ORDER", "OPEN", "f_loose", null, "SPREAD OUT: FEWER ARROWS FIND A MAN"),
        [Cmd.Formation] = new("NEXT FORMATION", "SHAPE", "f_line", Act.Formation, "CYCLE THE FORMATION"),
        [Cmd.FireAtWill] = new("FIRE AT WILL", "FIRE", "arrow", Act.FireAtWill, "LIT: SHOOT FREELY. DARK: HOLD FIRE"),
        [Cmd.Spacing] = new("SPACING", "SPACING", "spread", Act.Spacing, "WIDE OR CLOSE FILES"),
        [Cmd.Rule] = new("STANDING ORDER", "RULE", "scroll", Act.StandingOrder, "A RULE THE COMPANY KEEPS BY ITSELF"),
        [Cmd.Stakes] = new("PLANT STAKES", "STAKES", "stakes", Act.Stakes, "LONGBOWMEN STAKE THEIR FRONT AGAINST HORSE"),
        [Cmd.Cast] = new("CAST", "CAST", "star", Act.Cast, "PICK A SPELL, THEN CLICK THE GROUND"),
        [Cmd.Split] = new("SPLIT", "SPLIT", "split", Act.SplitCompany, "THE COMPANY STANDS AS TWO HALVES"),
        [Cmd.Merge] = new("FOLD IN", "FOLD IN", "merge", Act.Merge, "THE WORN FALL IN WITH THE FULLEST"),
        [Cmd.Relieve] = new("RELIEVE", "RELIEVE", "relieve", Act.Relieve, "THE FRESH TAKE THE TIRED ONES' LINE"),
    };

    /// The deck's first row: where to go and whom to fight.
    static readonly Cmd[] MoveRow = { Cmd.Charge, Cmd.Engage, Cmd.AttackMove, Cmd.Advance, Cmd.Hold, Cmd.Follow, Cmd.FallBack };
    static readonly Cmd[] ShapeRow = { Cmd.ShapeLine, Cmd.ShapeWall, Cmd.ShapeSquare, Cmd.ShapeWedge, Cmd.ShapeLoose };

    public static Formation? ShapeOf(Cmd c) => c switch
    {
        Cmd.ShapeLine => Formation.Line, Cmd.ShapeWall => Formation.ShieldWall, Cmd.ShapeSquare => Formation.Square,
        Cmd.ShapeWedge => Formation.Wedge, Cmd.ShapeLoose => Formation.Loose, _ => null,
    };

    static bool Shoots(Squad s) => s.Units.Count > 0 && s.Units[0].Type.Ammo > 0 || Battle.CasterCompany(s);

    /// An order as a picture: the icon a card and a flag use for what a company was told.
    static (string Icon, Color Col) OrderMark(Squad s) => s.Order switch
    {
        Order.Charge => ("sword", s.Target != null ? Bad : Orange),
        Order.Follow => ("flag", Friendly),
        _ => ("shield", Style.Text),
    };

    readonly List<Squad> listening = new();
    /// The companies an order would go to now: the chosen ones, or all you lead (as BattleScreen.Targets).
    List<Squad> Listening()
    {
        listening.Clear();
        foreach (var s in Selected.Count > 0 ? Selected : (IEnumerable<Squad>)B.Commanded) if (s.Alive > 0 && s.Works == null) listening.Add(s);
        return listening;
    }

    int hiddenCards;
    /// The horse's bar on the two HUDs: a lighter tan than the cards' so a thin bar still reads.
    static readonly Color HorseBright = new(226, 178, 112);

    /// Where a deck button is on screen (shots point the mouse at one to show its hover card).
    public Point? ButtonCentre(Cmd c) => buttons.Where(b => b.C == c).Select(b => (Point?)b.R.Center).FirstOrDefault();

    // ---------- layout ----------

    /// How far the command view's top-of-screen panels (deployment, the pause box, goals) sit below the top-center bar.
    int Drop => Command ? U(56) : 0;

    void LayoutDeck()
    {
        // Bottom left: the captain's plate (the tray of reserves sits on it, Hud.Command.cs).
        int ph = U(100);
        player = new Rectangle(U(10), H - ph - U(10), U(214), ph);
        captainBtn = new Rectangle(player.X + U(6), player.Y + U(6), U(54), ph - U(12));
        if (B.Player.Active && !Deploying) modeBtn = new Rectangle(captainBtn.Right + U(8), player.Bottom - U(28), player.Right - captainBtn.Right - U(14), U(22));

        // Left-edge vertical company roster (shared with FieldCommand) above the reserve tray/captain plate.
        int reserveRows = Math.Max(1, (Math.Min(8, B.ReservesOf(0).Count + B.CallUpsOf(0).Count()) + 1) / 2);
        int trayH = U(30) + reserveRows * U(18);
        LayoutRosterLeft(player.Y - trayH - U(12));

        // Bottom-left selected company card beside captain plate when a company is selected.
        if (Selected.Count > 0) LayoutSelectedCard(player.Right + U(8), H - U(10));
        else { selectedCard = Rectangle.Empty; cardBtns.Clear(); }

        // Bottom right: the order deck, or the chosen sergeant's goals (Hud.Divisions.cs).
        if (Div == null) LayoutOrders();

        // Top centre: one plate with pause/speeds, heraldic shields + balance tug bar, and clock (clear of left roster & right minimap).
        int wing = U(96), fixedW = U(112) + 2 * wing;
        int availW = minimap.X - U(216) - U(20);
        barW = Math.Clamp(availW - fixedW, U(140), U(260));
        int tw = barW + fixedW, tx = Math.Clamp((W - tw) / 2, U(212), Math.Max(U(212), minimap.X - U(12) - tw));
        topBar = new Rectangle(tx, U(10), tw, U(46));
        if (!Deploying && !ShowResult)
        {
            pauseBtn = new Rectangle(topBar.X + U(8), topBar.Y + U(12), U(22), U(22));
            for (int i = 0; i < 5; i++) speedBtns.Add((new Rectangle(pauseBtn.Right + U(6) + i * U(11), topBar.Y + U(14), U(11), U(26)), i));
        }
    }

    readonly List<Cmd> layoutShapes = new(8), layoutMore = new(12);

    /// The deck's buttons for the companies that listen: orders, the formations any of them can take, and what else fits
    /// (bows get FIRE, longbows STAKES, mages CAST; SPLIT, FOLD IN and RELIEVE come with one, two and exactly two chosen).
    void LayoutOrders()
    {
        var who = Listening();
        layoutShapes.Clear();
        for (int ci = 0; ci < ShapeRow.Length; ci++)
        {
            var c = ShapeRow[ci];
            var shape = ShapeOf(c)!.Value;
            for (int wi = 0; wi < who.Count; wi++)
            {
                var s = who[wi];
                if (s.Units.Count > 0 && Array.IndexOf(s.Shapes(s.Units[0].Type.HasShield), shape) >= 0) { layoutShapes.Add(c); break; }
            }
        }
        layoutMore.Clear();
        bool anyShoots = false, anyStakes = false, anySpells = false;
        for (int wi = 0; wi < who.Count; wi++)
        {
            var s = who[wi];
            if (!anyShoots && Shoots(s)) anyShoots = true;
            if (!anyStakes && s.Units.Count > 0 && s.Units[0].Type.Has("stakes")) anyStakes = true;
            if (!anySpells && s.Spells.Length > 0) anySpells = true;
        }
        if (anyShoots) layoutMore.Add(Cmd.FireAtWill);
        layoutMore.Add(Cmd.Spacing);
        if (!Deploying) layoutMore.Add(Cmd.Rule);
        if (B.Player.Active && !Deploying) layoutMore.Add(Cmd.FormHere);
        if (anyStakes) layoutMore.Add(Cmd.Stakes);
        if (!Deploying && anySpells) layoutMore.Add(Cmd.Cast);
        if (Selected.Count >= 1) layoutMore.Add(Cmd.Split);
        if (Selected.Count >= 2) layoutMore.Add(Cmd.Merge);
        if (Selected.Count == 2) layoutMore.Add(Cmd.Relieve);

        int cols = MoveRow.Length;
        int rowCount = 1 + (layoutShapes.Count > 0 ? 1 : 0) + (layoutMore.Count + cols - 1) / cols;
        int bs = U(50), bh = U(42), gap = U(3), gw = cols * bs + (cols - 1) * gap;
        int x0 = W - gw - U(18), y0 = H - U(18) - rowCount * (bh + gap) + gap;
        deck = new Rectangle(x0 - U(8), y0 - U(22), gw + U(16), H - U(10) - (y0 - U(22)));
        int r = 0;
        for (int i = 0; i < MoveRow.Length; i++)
            buttons.Add((new Rectangle(x0 + i * (bs + gap), y0 + r * (bh + gap), bs, bh), MoveRow[i]));
        r++;
        if (layoutShapes.Count > 0)
        {
            for (int i = 0; i < layoutShapes.Count; i++)
                buttons.Add((new Rectangle(x0 + i * (bs + gap), y0 + r * (bh + gap), bs, bh), layoutShapes[i]));
            r++;
        }
        for (int i = 0; i < layoutMore.Count; i++)
            buttons.Add((new Rectangle(x0 + (i % cols) * (bs + gap), y0 + (r + i / cols) * (bh + gap), bs, bh), layoutMore[i]));
    }

    // ---------- the top bar ----------

    (float F, int Mine, int Foes) Tally()
    {
        float s0 = 0, s1 = 0; int mine = 0, foes = 0;
        foreach (var u in B.Units)
        {
            if (!u.Active || u.Routing) continue;
            float wt = u.Hp / u.Type.MaxHp * (u.Type.Class switch { UnitClass.Cavalry => 2.2f, UnitClass.Captain => 3f, UnitClass.Archer => 0.9f, _ => 1f });
            if (u.Team == 0) { s0 += wt; mine++; } else { s1 += wt; foes++; }
        }
        return (s0 + s1 > 0 ? s0 / (s0 + s1) : 0.5f, mine, foes);
    }

    static readonly string[] SpeedNames = { "QUARTER SPEED", "HALF SPEED", "NORMAL SPEED", "DOUBLE SPEED", "TRIPLE SPEED" };

    /// Top centre: pause and the five speeds, the two sides' strength pulling on one bar between their shields, the clock.
    int barW;

    void TopBar()
    {
        var (f, mine, foes) = Tally();
        int bw = barW, x = topBar.X + U(56) + U(96), y = topBar.Y + U(8);
        Panel(topBar);
        string? hint = null;
        if (!pauseBtn.IsEmpty)
        {
            bool ph = pauseBtn.Contains(Mouse);
            Rect(pauseBtn, Paused ? Kit.Mul(Gold, 0.35f) : Color.Black * 0.35f);
            Frame(pauseBtn, Paused || ph ? Gold : Color.White * 0.15f, U(1));
            kit.Icon(Paused ? "play" : "pause", pauseBtn.X + U(4), pauseBtn.Y + U(4), Paused || ph ? Gold : Style.Text, 2);
            Text("SPEED", pauseBtn.Right + U(8), topBar.Y + U(6), Dim, 1);
            if (ph) hint = (Paused ? "RUN ON" : "PAUSE AND GIVE ORDERS") + $" ({Ctl.Name(Act.Pause)})";
            foreach (var (r, i) in speedBtns)
            {
                bool on = i <= SpeedIdx, sh = r.Contains(Mouse);
                int h = U(5 + 4 * i);
                Rect(new Rectangle(r.X + U(2), r.Bottom - U(3) - h, r.Width - U(3), h), sh ? Color.White : on ? (Paused ? Dim : Gold) : Color.White * 0.16f);
                if (sh) hint = SpeedNames[i] + $" ({Ctl.Name(Act.Slower)} {Ctl.Name(Act.Faster)})";
            }
        }
        kit.Shield(new Rectangle(x - U(48), y - U(4), U(30), U(36)), SideArms(0));
        kit.Shield(new Rectangle(x + bw + U(18), y - U(4), U(30), U(36)), SideArms(1));
        kit.TugBar(new Rectangle(x, y + U(20), bw, U(8)), f, SideArms(0).Ui, Foe);
        Text($"{mine}", x, y, Kit.Parchment);
        TextR($"{foes}", x + bw, y, Kit.Parchment);
        TextC(f > 0.6f ? "WINNING" : f < 0.4f ? "LOSING" : "EVEN", x + bw / 2f, y + U(4), f > 0.6f ? Ok : f < 0.4f ? Bad : Dim, 1);
        int t = (int)B.Time, rx = x + bw + U(60);
        kit.Icon("clock", rx, y + U(3), Dim, 1);
        Text($"{t / 60}:{t % 60:00}", rx + U(12), y, Color.White);
        string state = Deploying ? "DRAW UP" : Paused ? "PAUSED" : Speed != 1 ? $"SPEED X{Speed:0.##}" : "";
        Text(state, rx, y + U(19), Paused ? Gold : Speed != 1 ? Warn : Dim, 1);
        if (B.Arcane)
        {
            // The Tide (docs/archive/ARCANA.md AR3), under the tug bar while any company can cast: the power left in this ground.
            var tb = new Rectangle(x + bw / 4, y + U(31), bw / 2, U(3));
            kit.Gauge(tb, B.TideCap <= 0 ? 0 : B.Tide / B.TideCap, new Color(150, 140, 255));
            if (new Rectangle(tb.X, tb.Y - U(4), tb.Width, tb.Height + U(8)).Contains(Mouse))
                hint = $"THE TIDE {B.Tide:0} OF {B.TideCap:0}{(B.Options.TideTradition != "" ? $": A WELL OF THE {B.Options.TideTradition.ToUpperInvariant()}" : "")}";
        }
        if (B.Wall is Fortress w && !Deploying)
        {
            // Siege status strip (SE3): time left, gate status, and heart capture/hold bar.
            var g = new Rectangle(x + bw / 6, topBar.Bottom + U(4), bw * 2 / 3, U(18));
            Panel(g);
            int mins = (int)(B.SiegeTimeLeft / 60), secs = (int)(B.SiegeTimeLeft % 60);
            Text($"{mins:00}:{secs:00}", g.X + U(6), g.Y + U(5), B.SiegeTimeLeft < 180 ? Warn : Dim, 1);

            if (w.HeartOwner == 0)
            {
                Text("HEART", g.X + U(46), g.Y + U(5), Gold, 1);
                kit.Gauge(new Rectangle(g.X + U(84), g.Y + U(6), g.Width - U(124), U(6)), w.HeartHoldTimer / 60f, Gold);
                Text($"{(int)MathF.Ceiling(60f - w.HeartHoldTimer)}S", g.Right - U(34), g.Y + U(5), Gold, 1);
            }
            else if (w.AnyGateOpen || w.AnyBreach)
            {
                string msg = w.AnyGateOpen ? "GATE OPEN" : "WALL BREACHED";
                TextC(msg, g.Center.X, g.Y + U(5), Gold, 1);
                // Heart shield on the right.
                kit.Icon("crown", g.Right - U(18), g.Y + U(4), w.HeartCapture < 0.99f ? Warn : Style.Sides[B.Options.Palette[1]].Cloth, 1);
            }
            else
            {
                kit.Icon("tower", g.X + U(46), g.Y + U(5), Color.White, 1);
                Text("GATE", g.X + U(58), g.Y + U(5), Color.White, 1);
                var mg = w.MainGate;
                kit.Gauge(new Rectangle(g.X + U(88), g.Y + U(6), g.Width - U(114), U(6)), mg.MaxHp > 0 ? mg.Hp / mg.MaxHp : 0, Style.HorseBar);
                kit.Icon("crown", g.Right - U(18), g.Y + U(4), Style.Sides[B.Options.Palette[1]].Cloth, 1);
            }
        }
        if (hint != null)
        {
            int hw = TW(hint, 1) + U(14);
            var hr = new Rectangle(Math.Clamp(Mouse.X - hw / 2, U(4), W - hw - U(4)), topBar.Bottom + U(6) + (B.Wall != null && !Deploying ? U(20) : 0), hw, U(16));
            Panel(hr);
            TextC(hint, hr.Center.X, hr.Y + U(5), Color.White, 1);
        }
    }

    // ---------- the captain's plate ----------

    /// Bottom left: the captain while you command. His picture (a click looks at him), how he fares, and FIGHT to step into
    /// his body. He keeps his own guard meanwhile (Battle.SelfGuard) and the plate turns red while blows come at him.
    void CaptainPlate()
    {
        var p = B.Player;
        bool pressed = p.Active && !Deploying && B.SelfGuard;
        Panel(player, pressed ? Bad : null, pressed ? 2 : 1);
        bool over = captainBtn.Contains(Mouse);
        kit.VGrad(captainBtn, Kit.Mul(SideArms(0).Cloth, p.Active ? 0.55f : 0.2f), Kit.IronDark, 2);
        Portrait(sb, art, p.Type, new Rectangle(captainBtn.X + U(2), captainBtn.Y + U(8), captainBtn.Width - U(4), captainBtn.Height - U(9)), Ui);
        if (!p.Active) Rect(captainBtn, Color.Black * 0.55f);
        Frame(captainBtn, over ? Gold : Color.Black * 0.7f, U(1));
        int x = captainBtn.Right + U(8), y = player.Y + U(8), w = player.Right - U(8) - x;
        if (!p.Active)
        {
            Text(B.Options.PlayerWounded ? "WOUNDED" : "YOU HAVE FALLEN", x, y, Bad, 1);
            Text("YOUR WORD STILL", x, y + U(16), Dim, 1);
            Text("REACHES THE MEN", x, y + U(27), Dim, 1);
            kit.Icon("skull", x, player.Bottom - U(18), Dim, 1);
            Text($"{B.PlayerKills}", x + U(12), player.Bottom - U(18), Color.White, 1);
            return;
        }
        Text("THE CAPTAIN", x, y, Gold, 1);
        kit.Icon("skull", player.Right - U(8) - TW($"{B.PlayerKills}", 1) - U(11), y, Dim, 1);
        TextR($"{B.PlayerKills}", player.Right - U(8), y, Color.White, 1);
        string word = Deploying ? "WITH HIS MEN" : pressed ? "UNDER ATTACK" : p.EnemyDist < 12 ? "FOES ARE NEAR" : p.Mounted ? "IN THE SADDLE" : "ON FOOT";
        Text(word, x, y + U(11), pressed ? Bad : p.EnemyDist < 12 && !Deploying ? Warn : Dim, 1);
        int gx = x + U(11), gw = w - U(11);
        kit.Icon("heart", x, y + U(23), Bad, 1); kit.Gauge(new Rectangle(gx, y + U(23), gw, U(7)), p.Hp / p.Type.MaxHp, Bad);
        kit.Icon("boot", x, y + U(32), Ok, 1); kit.Gauge(new Rectangle(gx, y + U(33), gw, U(5)), p.Stamina / 100, Ok);
        if (p.Mounted) { kit.Icon("horse", x, y + U(41), HorseBright, 1); kit.Gauge(new Rectangle(gx, y + U(42), gw, U(5)), p.HorseHp / p.Horse!.Hp, HorseBright); }
        if (modeBtn.IsEmpty) return;
        bool mh = modeBtn.Contains(Mouse);
        Rect(new Rectangle(modeBtn.X + U(1), modeBtn.Y + U(2), modeBtn.Width, modeBtn.Height), Color.Black * 0.4f);
        kit.VGrad(modeBtn, mh ? new Color(150, 118, 44) : new Color(96, 78, 34), mh ? new Color(92, 70, 26) : new Color(52, 42, 20), 1);
        Frame(modeBtn, Gold, U(1));
        kit.Icon("sword", modeBtn.X + U(6), modeBtn.Y + U(4), Color.White, 2);
        Text("FIGHT", modeBtn.X + U(26), modeBtn.Y + U(4), Color.White);
        TextR(Ctl.Name(Act.CommandView), modeBtn.Right - U(6), modeBtn.Y + U(8), Kit.Parchment, 1);
    }

    // ---------- the order deck ----------

    /// Why none of the listening companies can take this shape yet (TI1: the tier ladder), or null.
    static string? Unlearned(Cmd c, List<Squad> who) =>
        ShapeOf(c) is Formation f && who.Count > 0 && who.All(s => s.Units.Count == 0 || s.CannotForm(f) != null)
            ? $"{FormationName(f)} {who.Select(s => s.CannotForm(f)).FirstOrDefault(w => w != null)}. PROMOTE THE MEN TO TEACH IT." : null;

    bool Lit(Cmd c, List<Squad> who) => who.Count > 0 && c switch
    {
        Cmd.Charge => who.All(s => s.Order == Order.Charge && s.Reckless),
        Cmd.Engage => who.All(s => s.Order == Order.Charge && !s.Reckless),
        Cmd.Hold => who.All(s => s.Order == Order.Hold),
        Cmd.Follow => who.All(s => s.Order == Order.Follow),
        Cmd.AttackMove => AttackMoveMode,
        Cmd.FireAtWill => who.Where(Shoots).All(s => s.FireAtWill && !s.HoldSpells),
        Cmd.Spacing => who.All(s => s.Loose),
        Cmd.Rule => who.Any(s => s.Rule != StandingRule.None),
        Cmd.Cast => CastBy != null,
        _ => ShapeOf(c) is Formation f && who.All(s => s.Formation == f),
    };

    /// Dedicated siege engine status and control panel when one or more engine crews are selected.
    void EngineDeck(List<Squad> crews)
    {
        var e = crews[0].Works!;
        int x = deck.X + U(8), y = deck.Y + U(8), w = deck.Width - U(16);
        string title = crews.Count == 1 ? e.Def.Name.ToUpperInvariant() : $"{crews.Count} SIEGE ENGINES";
        string crewTxt = $"CREW {B.CrewAt(e)}/{e.Def.Crew}";
        bool titleHover = DeckTitleAt(Mouse);
        Text("< " + title, x, y, titleHover ? Color.White : Gold, 1);
        TextR(crewTxt, deck.Right - U(8), y, B.CrewAt(e) >= e.Def.Crew ? Ok : Warn, 1);
        y += U(14);
        kit.Gauge(new Rectangle(x, y, w, U(5)), Math.Clamp(e.Hp / e.Def.Hp, 0, 1), Ok);
        y += U(10);

        if (e.Def.Rolls)
        {
            float dist = Vector2.Distance(e.Pos, e.Goal);
            string status = e.Docked ? "DOCKED: RAMP OPEN TO WALLS"
                : dist < 1f && e.Def.Shot == "ram" ? "BATTERING THE GATE"
                : $"ROLLING TO TARGET ({dist:0} M)";
            Text(status, x, y, e.Docked ? Ok : Gold, 1); y += U(13);
            Text("AUTO-PUSHES TO GATE OR WALL WHILE CREWED.", x, y, Style.Text, 1); y += U(11);
            Text("CREW IS SHELTERED UNDER IT (-75% ARROW DMG).", x, y, Dim, 1); y += U(11);
            Text("ESCORT WITH FOOT TO GUARD AGAINST SALLIES.", x, y, Dim, 1);
        }
        else
        {
            float ready = e.Reload <= 0 ? 1 : 1 - Math.Clamp(e.Reload / e.Def.Reload, 0, 1);
            string tgt = e.Mark != null ? "WALL / GROUND MARK" : e.Crew.Target is { Alive: > 0 } t ? t.Name.ToUpperInvariant() : "AUTO-TARGETING";
            Text($"TARGET: {kit.Fit(tgt, w - U(48))}", x, y, e.Mark != null || e.Crew.Target != null ? Gold : Style.Text, 1); y += U(12);
            Text(ready >= 1 ? "READY TO FIRE" : $"RELOADING {ready * 100:0}%", x, y, ready >= 1 ? Ok : Dim, 1); y += U(10);
            kit.Gauge(new Rectangle(x, y, w, U(4)), ready, ready >= 1 ? Gold : Friendly); y += U(10);
            Text("RIGHT-CLICK WALL, GATE OR FOE TO AIM.", x, y, Style.Text, 1); y += U(11);
            Text($"IN FIGHT VIEW: WALK UP + [{Ctl.Name(Act.Interact)}] TO MAN.", x, y, Dim, 1);
        }
    }

    /// Bottom right: who listens, then the picture buttons (or clear siege engine controls when engine crews are selected).
    void OrderDeck()
    {
        if (deck.IsEmpty) return;
        Panel(deck);
        var crews = Selected.Where(s => s.Alive > 0 && s.Works is { Wrecked: false }).ToList();
        if (Selected.Count > 0 && crews.Count == Selected.Count)
        {
            EngineDeck(crews);
            return;
        }
        var who = Listening();
        string head = Selected.Count == 0 ? "ALL COMPANIES" : who.Count == 1 ? (who[0].Company?.Name is { Length: > 0 } cn ? cn : who[0].Name).ToUpperInvariant() : $"{who.Count} COMPANIES";
        string men = $"{who.Sum(s => s.Alive)} MEN";
        bool titleHover = DeckTitleAt(Mouse);
        if (Selected.Count > 0) head = "< " + head; // a click on the title goes back to all companies
        Text(kit.Fit(head, deck.Width - TW(men, 1) - U(24)), deck.X + U(8), deck.Y + U(8), titleHover ? Color.White : Gold, 1);
        if (titleHover) Text("ALL COMPANIES", deck.X + U(8), deck.Y - U(12), Color.White, 1);
        TextR(men, deck.Right - U(8), deck.Y + U(8), Dim, 1);
        Cmd? over = null;
        var chrome = UiChrome.For(sb.GraphicsDevice);
        int bb = Math.Max(3, U(5));
        foreach (var (r, c) in buttons)
        {
            if (!deck.Contains(r.Center)) continue;
            bool lit = Lit(c, who), hover = r.Contains(Mouse), locked = Unlearned(c, who) != null;
            if (hover) over = c;
            var info = Info[c];
            var fill = locked ? new Color(26, 25, 29) : lit ? new Color(118, 90, 36) : hover ? new Color(82, 68, 42) : new Color(44, 42, 48);
            chrome.Draw9(sb, r, UiChrome.BtnFill, 8, bb, fill);
            chrome.Draw9(sb, r, UiChrome.BtnFrame, 8, bb, lit || hover ? Gold : Kit.GiltDark * 0.85f);
            var col = locked ? Kit.Mul(Dim, 0.7f) : lit ? Color.White : hover ? Kit.Parchment : Style.Text;
            kit.Icon(info.Icon, r.X + (r.Width - U(21)) / 2, r.Y + U(5), col, 3);
            TextC(kit.Fit(info.Short, r.Width - U(4)), r.Center.X, r.Bottom - U(12), col, 1);
            if (info.Key is Act k && Ctl.Name(k) is { Length: > 0 and <= 2 } key) TextR(key, r.Right - U(4), r.Y + U(3), lit ? Kit.Parchment : hover ? Gold : Dim, 1);
        }
        if (over is not Cmd oc)
        {
            string kh = $"{Ctl.Name(Act.Help)} KEYS";
            int kw = TW(kh, 1) + U(12);
            var kp = new Rectangle(deck.Right - U(2) - kw, deck.Y - U(15), kw, U(13));
            Rect(kp, new Color(12, 14, 20) * 0.78f);
            Frame(kp, Color.White * 0.2f, U(1));
            TextR(kh, kp.Right - U(6), kp.Y + U(3), Style.Text * 0.9f, 1);
            return;
        }
        var oi = Info[oc];
        string tip = Unlearned(oc, who) is string why ? why : oc == Cmd.Rule && who.FirstOrDefault(s => s.Rule != StandingRule.None) is { } ruled ? "NOW: " + Battle.RuleText(ruled.Rule).ToUpperInvariant() : oi.Tip;
        var card = new Rectangle(deck.X, deck.Y - U(38), deck.Width, U(34));
        Panel(card, Gold);
        Text(oi.Name, card.X + U(8), card.Y + U(7), Color.White, 1);
        if (oi.Key is Act ok)
        {
            // The key as a keycap after the name (alone in the corner it read as a close button).
            string kn = "KEY " + Ctl.Name(ok);
            var cap = new Rectangle(card.Right - U(8) - TW(kn, 1) - U(8), card.Y + U(4), TW(kn, 1) + U(8), U(13));
            Rect(cap, Color.Black * 0.5f); Frame(cap, Kit.Mul(Gold, 0.7f), U(1));
            Text(kn, cap.X + U(4), cap.Y + U(3), Gold, 1);
        }
        Text(kit.Fit(tip, card.Width - U(16)), card.X + U(8), card.Y + U(20), Dim, 1);
    }

    // ---------- the company cards ----------

    /// The unit cards as a dock along the bottom: portrait in its side's cloth, number, tier, order, men and morale gauges.
    void Cards()
    {
        if (cards.Count > 0)
        {
            var dock = new Rectangle(cards[0].R.X - U(8), U(8), cards[^1].R.Right - cards[0].R.X + U(16), U(94) + U(12));
            kit.PlateBody(dock, Kit.Rivet, 1, 0.8f);
        }
        var cloth = SideArms(0);
        for (int ci = 0; ci < cards.Count; ci++)
        {
            var (r, s) = cards[ci];
            int active = 0, routing = 0;
            float moraleSum = 0;
            foreach (var u in s.Units) if (u.Active) { active++; if (u.Routing) routing++; moraleSum += u.Morale; }
            int morale = active == 0 ? 0 : (int)(moraleSum / active);
            var (state, sc) = StateOf(s, active, routing, morale);
            bool sel = Selected.Contains(s);
            bool hover = r.Contains(Mouse);
            Rect(new Rectangle(r.X + U(1), r.Y + U(2), r.Width, r.Height), Color.Black * 0.4f);
            kit.VGrad(r, sel ? new Color(70, 60, 36) : hover ? new Color(46, 46, 50) : new Color(30, 32, 38), Kit.IronDark, 2);
            var coHd = CompanyArms(s);
            var port = new Rectangle(r.X + U(3), r.Y + U(3), r.Width - U(6), U(46));
            kit.VGrad(port, Kit.Mul(coHd.Cloth, 0.52f), Kit.IronDark, 2);
            var type = s.Units.Count > 0 ? s.Units[0].Type : UnitType.Shieldman;
            Icon(type, new Rectangle(port.X + U(12), port.Y + U(1), port.Width - U(24), port.Height - U(1)));
            // Company's own generated little banner on the card so each company is recognizable at a glance.
            kit.MiniBanner(new Rectangle(r.X + U(4), r.Y + U(18), U(13), U(16)), coHd);
            Frame(r, sel ? Gold : state is "STEADY" ? Color.Black * 0.7f : sc, U(sel ? 3 : 1));
            if (sel) Rect(new Rectangle(r.X + U(6), r.Bottom + U(2), r.Width - U(12), U(3)), Gold); // a gold tab under the chosen
            if (active == 0) Rect(r, Color.Black * 0.45f);

            int idx = ci + 1;
            int bw = idx >= 10 ? U(18) : U(13);
            Rect(new Rectangle(r.X + U(3), r.Y + U(3), bw, U(13)), sel ? Gold : Color.Black * 0.75f);
            Text($"{idx}", r.X + U(5), r.Y + U(6), sel ? Kit.IronDark : Color.White, 1);
            // A plate across the portrait's foot: the men's itch to go in (RST, BattleUrge.cs), else what its sergeant is doing (OF5).
            if (s.Restless > 0.15f) Rect(new Rectangle(r.X + U(3), r.Y + U(46), (int)((r.Width - U(6)) * Math.Min(1, s.Restless / 0.75f)), U(3)), s.Restless > 0.5f ? Bad : Orange);
            var (plate, pc) = s.Restless > 0.3f ? ("RESTLESS", Orange) // never red: red is NO SERGEANT
                : s.SgtDoing != SgtTask.None && active > 0 ? (Battle.SergeantTag(s.SgtDoing), s.SgtDoing == SgtTask.Down ? new Color(255, 106, 85) : s.SgtDoing == SgtTask.Rallying ? Warn : s.SgtDoing == SgtTask.Reforming ? Orange : Style.Text)
                : ("", Color.White);
            if (plate != "")
            {
                var band = new Rectangle(r.X + U(3), r.Y + U(35), r.Width - U(6), U(11));
                Rect(band, Color.Black * 0.8f);
                Rect(new Rectangle(band.X, band.Y, band.Width, U(1)), pc * 0.8f);
                TextC(kit.Fit(plate, band.Width - U(4)), band.Center.X, band.Y + U(3), pc, 1);
            }
            // Its order as one picture in the corner (PT4); a crossed bow when it holds its fire, a dot for orders queued or a
            // standing rule. Formation, drill, tier and the rest are on the hover card.
            var (oi, oc) = OrderMark(s);
            Rect(new Rectangle(r.Right - U(20), r.Y + U(3), U(18), U(18)), Color.Black * 0.65f);
            kit.Icon(oi, r.Right - U(18), r.Y + U(5), oc, 2);
            if (!s.FireAtWill || s.HoldSpells)
            {
                Rect(new Rectangle(r.Right - U(20), r.Y + U(23), U(18), U(18)), Color.Black * 0.65f);
                kit.Icon("arrow", r.Right - U(18), r.Y + U(25), Style.Text, 2);
                Line(new Vector2(r.Right - U(18), r.Y + U(25)), new Vector2(r.Right - U(4), r.Y + U(39)), Bad, U(2)); // struck through: holding fire
            }
            if (s.Rule != StandingRule.None || s.Queue.Count > 0) Rect(new Rectangle(r.Right - U(13), r.Y + U(44), U(5), U(5)), Gold);
            // A gold flash when a new order reaches it (PT4 6): you see the order land, not only hear it.
            if (OrderFlash(s) is float fl and > 0) { Rect(r, Gold * (0.35f * fl)); Frame(r, Gold, U(2)); }

            // The troop's name by measured width; too long, its last word ("YEOMAN ARCHERS" -> "ARCHERS"), then cut.
            string nm = s.Name.ToUpperInvariant();
            if (TW(nm, 1) > r.Width - U(2)) nm = kit.Fit(nm.Split(' ').Last(), r.Width - U(2));
            TextC(nm, r.X + r.Width / 2f, r.Y + U(52), Dim, 1);
            TextC($"{active}/{s.StartCount}", r.X + r.Width / 2f, r.Y + U(61), Color.White);
            kit.Gauge(new Rectangle(r.X + U(4), r.Y + U(76), r.Width - U(8), U(6)), active / (float)Math.Max(1, s.StartCount), cloth.Ui);
            kit.Gauge(new Rectangle(r.X + U(4), r.Y + U(84), r.Width - U(8), U(6)), morale / 100f, MoraleColor(morale));
            if (state == "STEADY" && fights.TryGetValue(s, out var fight)) (state, sc) = Standing(s, fight); // how its fight goes
            if (state != "STEADY") kit.Chip(r.X + r.Width / 2 - (TW(state, 1) + U(10)) / 2, r.Bottom + U(7), state, sc);
            EffectMarks(s, r);
            if (!sel && Selected.Count > 0 && OrderFlash(s) is not > 0) Rect(r, Color.Black * 0.4f); // with some chosen, the rest step back
        }
        if (hiddenCards > 0 && cards.Count > 0)
        {
            var last = cards[^1].R;
            TextC($"+{hiddenCards} MORE", last.X + last.Width / 2f, last.Bottom + U(26), Gold, 1);
        }
    }
}
