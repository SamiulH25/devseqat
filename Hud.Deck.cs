using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud, the command view (docs/PLAYTEST.md PT4, "As rebuilt"): you are the general, with a cursor. One plate along the top
/// (time on the left, the balance in the middle, the clock on the right) and one row along the bottom: the captain's plate
/// (how he fares, FIGHT to step into him), the company cards, and the order deck, a grid of picture buttons that shows only
/// what the chosen companies can do and lights what they are doing.
sealed partial class Hud
{
    /// An order as the deck and the ring show it: name, the short word under its picture, picture, key, and a line for the hover card.
    public record OrderInfo(string Name, string Short, string Icon, Act? Key, string Tip);

    public static readonly Dictionary<Cmd, OrderInfo> Info = new()
    {
        [Cmd.Charge] = new("CHARGE", "CHARGE", "sword", Act.Charge, "RUN AT THE NEAREST ENEMY AND FIGHT"),
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
    static readonly Cmd[] MoveRow = { Cmd.Charge, Cmd.AttackMove, Cmd.Advance, Cmd.Hold, Cmd.Follow, Cmd.FallBack };
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

    void LayoutDeck()
    {
        // Top: one plate. Time in its left wing, the clock in its right.
        int bw = U(300), wing = U(96);
        topBar = new Rectangle((W - bw) / 2 - U(56) - wing, U(6), bw + U(112) + 2 * wing, U(46));
        if (!Deploying && !ShowResult)
        {
            pauseBtn = new Rectangle(topBar.X + U(8), topBar.Y + U(12), U(22), U(22));
            for (int i = 0; i < 5; i++) speedBtns.Add((new Rectangle(pauseBtn.Right + U(6) + i * U(11), topBar.Y + U(14), U(11), U(26)), i));
        }

        // Bottom left: the captain's plate (the tray of reserves sits on it, Hud.Command.cs).
        int ph = U(100);
        player = new Rectangle(U(10), H - ph - U(10), U(214), ph);
        captainBtn = new Rectangle(player.X + U(6), player.Y + U(6), U(54), ph - U(12));
        if (B.Player.Active && !Deploying) modeBtn = new Rectangle(captainBtn.Right + U(8), player.Bottom - U(28), player.Right - captainBtn.Right - U(14), U(22));

        // Bottom right: the order deck, or the chosen sergeant's goals (Hud.Divisions.cs).
        int rightEdge = W - U(240);
        if (Div == null) { LayoutOrders(); rightEdge = deck.X - U(8); }

        // Between them: the cards. They shrink to fit a big retinue; past what fits, the rest are summed on the last card.
        var mine = Mine;
        int left = player.Right + U(10), room = rightEdge - left, gap = mine.Count > 8 ? U(4) : U(6), ch = U(94);
        int cw = Math.Clamp((room - gap * Math.Max(0, mine.Count - 1)) / Math.Max(1, mine.Count), U(60), U(78));
        int fit = Math.Max(1, (room + gap) / (cw + gap));
        int shown = Math.Min(mine.Count, fit), total = shown * cw + Math.Max(0, shown - 1) * gap;
        int cx0 = left + Math.Max(0, (room - total) / 2);
        for (int i = 0; i < shown; i++) cards.Add((new Rectangle(cx0 + i * (cw + gap), H - ch - U(10), cw, ch), mine[i]));
        hiddenCards = mine.Count - shown;
    }

    /// The deck's buttons for the companies that listen: orders, the formations any of them can take, and what else fits
    /// (bows get FIRE, longbows STAKES, mages CAST; SPLIT, FOLD IN and RELIEVE come with one, two and exactly two chosen).
    void LayoutOrders()
    {
        var who = Listening();
        var rows = new List<List<Cmd>> { MoveRow.ToList() };
        var shapes = ShapeRow.Where(c => who.Any(s => s.Units.Count > 0 && s.Choices(s.Units[0].Type.HasShield).Contains(ShapeOf(c)!.Value))).ToList();
        if (shapes.Count > 0) rows.Add(shapes);
        var more = new List<Cmd>();
        if (who.Any(Shoots)) more.Add(Cmd.FireAtWill);
        more.Add(Cmd.Spacing);
        if (!Deploying) more.Add(Cmd.Rule);
        if (B.Player.Active && !Deploying) more.Add(Cmd.FormHere);
        if (who.Any(s => s.Units.Count > 0 && s.Units[0].Type.Has("stakes"))) more.Add(Cmd.Stakes);
        if (!Deploying && who.Any(s => s.Spells.Length > 0)) more.Add(Cmd.Cast);
        if (Selected.Count >= 1) more.Add(Cmd.Split);
        if (Selected.Count >= 2) more.Add(Cmd.Merge);
        if (Selected.Count == 2) more.Add(Cmd.Relieve);
        const int cols = 6;
        for (int i = 0; i < more.Count; i += cols) rows.Add(more.Skip(i).Take(cols).ToList());

        int bs = U(44), bh = U(38), gap = U(3), gw = cols * bs + (cols - 1) * gap;
        int x0 = W - gw - U(18), y0 = H - U(18) - rows.Count * (bh + gap) + gap;
        deck = new Rectangle(x0 - U(8), y0 - U(22), gw + U(16), H - U(10) - (y0 - U(22)));
        for (int r = 0; r < rows.Count; r++)
            for (int i = 0; i < rows[r].Count; i++)
                buttons.Add((new Rectangle(x0 + i * (bs + gap), y0 + r * (bh + gap), bs, bh), rows[r][i]));
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
    void TopBar()
    {
        var (f, mine, foes) = Tally();
        int bw = U(300), x = (W - bw) / 2, y = U(14);
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
        Text($"{mine}", x, y, SideArms(0).Ui);
        TextR($"{foes}", x + bw, y, Foe);
        TextC(f > 0.6f ? "WINNING" : f < 0.4f ? "LOSING" : "EVEN", W / 2f, y + U(4), f > 0.6f ? Ok : f < 0.4f ? Bad : Dim, 1);
        int t = (int)B.Time, rx = x + bw + U(60);
        kit.Icon("clock", rx, y + U(3), Dim, 1);
        Text($"{t / 60}:{t % 60:00}", rx + U(12), y, Color.White);
        string state = Deploying ? "DRAW UP" : Paused ? "PAUSED" : Speed != 1 ? $"SPEED X{Speed:0.##}" : "";
        Text(state, rx, y + U(19), Paused ? Gold : Speed != 1 ? Warn : Dim, 1);
        if (B.Arcane)
        {
            // The Tide (docs/ARCANA.md AR3), under the tug bar while any company can cast: the power left in this ground.
            var tb = new Rectangle(x + bw / 4, y + U(31), bw / 2, U(3));
            kit.Gauge(tb, B.TideCap <= 0 ? 0 : B.Tide / B.TideCap, new Color(150, 140, 255));
            if (new Rectangle(tb.X, tb.Y - U(4), tb.Width, tb.Height + U(8)).Contains(Mouse))
                hint = $"THE TIDE {B.Tide:0} OF {B.TideCap:0}{(B.Options.TideTradition != "" ? $": A WELL OF THE {B.Options.TideTradition.ToUpperInvariant()}" : "")}";
        }
        if (B.Wall is SiegeWall w && !Deploying)
        {
            // The gate's strength under the bar, while it stands.
            var g = new Rectangle(x + bw / 4, topBar.Bottom + U(4), bw / 2, U(16));
            Panel(g);
            if (w.GateOpen || w.Breaches.Any()) TextC(w.GateOpen ? "THE GATE IS BROKEN" : "THE WALL IS BREACHED", W / 2f, g.Y + U(5), Gold, 1);
            else
            {
                kit.Icon("tower", g.X + U(6), g.Y + U(5), Color.White, 1);
                Text("GATE", g.X + U(18), g.Y + U(5), Color.White, 1);
                kit.Gauge(new Rectangle(g.X + U(48), g.Y + U(5), g.Width - U(54), U(6)), w.GateHp / w.GateMax, Style.HorseBar);
            }
        }
        if (hint != null)
        {
            int hw = TW(hint, 1) + U(14);
            var hr = new Rectangle(Math.Clamp(Mouse.X - hw / 2, U(4), W - hw - U(4)), topBar.Bottom + U(3), hw, U(16));
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

    bool Lit(Cmd c, List<Squad> who) => who.Count > 0 && c switch
    {
        Cmd.Charge => who.All(s => s.Order == Order.Charge),
        Cmd.Hold => who.All(s => s.Order == Order.Hold),
        Cmd.Follow => who.All(s => s.Order == Order.Follow),
        Cmd.AttackMove => AttackMoveMode,
        Cmd.FireAtWill => who.Where(Shoots).All(s => s.FireAtWill && !s.HoldSpells),
        Cmd.Spacing => who.All(s => s.Loose),
        Cmd.Rule => who.Any(s => s.Rule != StandingRule.None),
        Cmd.Cast => CastBy != null,
        _ => ShapeOf(c) is Formation f && who.All(s => s.Formation == f),
    };

    /// Bottom right: who listens, then the picture buttons. The one under the mouse says what it does on a card above.
    void OrderDeck()
    {
        if (deck.IsEmpty) return;
        Panel(deck);
        var who = Listening();
        string head = Selected.Count == 0 ? "ALL COMPANIES" : who.Count == 1 ? (who[0].Company?.Name is { Length: > 0 } cn ? cn : who[0].Name).ToUpperInvariant() : $"{who.Count} COMPANIES";
        string men = $"{who.Sum(s => s.Alive)} MEN";
        Text(kit.Fit(head, deck.Width - TW(men, 1) - U(24)), deck.X + U(8), deck.Y + U(8), Gold, 1);
        TextR(men, deck.Right - U(8), deck.Y + U(8), Dim, 1);
        Cmd? over = null;
        foreach (var (r, c) in buttons)
        {
            bool lit = Lit(c, who), hover = r.Contains(Mouse);
            if (hover) over = c;
            var info = Info[c];
            Rect(new Rectangle(r.X + U(1), r.Y + U(2), r.Width, r.Height), Color.Black * 0.4f);
            kit.VGrad(r, lit ? new Color(104, 84, 36) : hover ? new Color(60, 60, 68) : new Color(38, 40, 46), lit ? new Color(54, 44, 20) : Kit.IronDark, 1);
            Frame(r, lit || hover ? Gold : Color.Black * 0.7f, U(1));
            Rect(new Rectangle(r.X + U(1), r.Y + U(1), r.Width - U(2), U(1)), Color.White * (hover ? 0.3f : 0.1f));
            var col = lit ? Gold : hover ? Color.White : Style.Text;
            kit.Icon(info.Icon, r.X + (r.Width - U(21)) / 2, r.Y + U(4), col, 3);
            TextC(kit.Fit(info.Short, r.Width - U(2)), r.Center.X, r.Bottom - U(10), col, 1);
            if (info.Key is Act k && Ctl.Name(k) is { Length: > 0 and <= 2 } key) TextR(key, r.Right - U(3), r.Y + U(3), hover ? Kit.Parchment : Dim, 1);
        }
        if (over is not Cmd oc) { TextR($"{Ctl.Name(Act.Help)} KEYS", deck.Right - U(8), deck.Y - U(10), Color.White * 0.6f, 1); return; }
        var oi = Info[oc];
        string tip = oc == Cmd.Rule && who.FirstOrDefault(s => s.Rule != StandingRule.None) is { } ruled ? "NOW: " + Battle.RuleText(ruled.Rule).ToUpperInvariant() : oi.Tip;
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
            var dock = new Rectangle(cards[0].R.X - U(8), cards[0].R.Y - U(6), cards[^1].R.Right - cards[0].R.X + U(16), cards[0].R.Height + U(12));
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
            var port = new Rectangle(r.X + U(3), r.Y + U(3), r.Width - U(6), U(46));
            kit.VGrad(port, Kit.Mul(cloth.Cloth, 0.5f), Kit.IronDark, 2);
            var type = s.Units.Count > 0 ? s.Units[0].Type : UnitType.Shieldman;
            Icon(type, new Rectangle(port.X + U(12), port.Y + U(1), port.Width - U(24), port.Height - U(1)));
            Frame(r, sel ? Gold : state is "STEADY" ? Color.Black * 0.7f : sc, U(sel ? 2 : 1));
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
            if (state != "STEADY") kit.Chip(r.X + r.Width / 2 - (TW(state, 1) + U(10)) / 2, r.Y - U(20), state, sc);
            EffectMarks(s, r);
        }
        if (hiddenCards > 0 && cards.Count > 0)
        {
            var last = cards[^1].R;
            TextC($"+{hiddenCards} MORE", last.X + last.Width / 2f, last.Y - U(12), Gold, 1);
        }
    }
}
