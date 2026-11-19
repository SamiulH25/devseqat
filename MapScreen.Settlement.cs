using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the settlement screen and its tabs (train, recruit, market, food, rest, contracts, fief). Its frame (the
/// painted place, the rail of tabs, cards and stats) is in MapScreen.Town.cs.
partial class MapScreen
{
    void SettlementPanel()
    {
        var s = at!;
        var r = Modal(980, 620, s.Kind.ToString().ToUpperInvariant(), SideColor(s.Faction));
        bool fief = s.Owner == C.Player.Id && s.Kind != SettlementKind.Village;
        PlaceHero(s, new Rectangle(r.X + ui.U(14), r.Y + ui.U(16), r.Width - ui.U(28), ui.U(150)));
        var tabs = TownTabs(s, fief);
        if (Array.IndexOf(tabs, tab) < 0) tab = tabs[0]; // a tab this place lacks, or one with nothing in it now
        int top = PlaceRail(new Rectangle(r.X + ui.U(14), r.Y + ui.U(172), r.Width - ui.U(28), ui.U(38)), tabs, s) + ui.U(6);
        var well = new Rectangle(r.X + ui.U(14), top, r.Width - ui.U(28), r.Bottom - ui.U(66) - top);
        ui.Rect(well, Color.Black * 0.22f);
        ui.Frame(well, Color.White * 0.06f, ui.U(1));
        var body = Kit.Inset(well, ui.U(12));
        if (tab is "REST" or "FOOD" or "FORAGE" or "RECRUIT" or "ESQUIRES" or "CAPTIVES" or "TRAIN" or "ELDERS" or "WORKSHOP" or "HARBOUR") Watermark(well);
        switch (tab)
        {
            case "RECRUIT": RecruitTab(s, body); break;
            case "ESQUIRES": NoblesTab(s, body); break;
            case "MERCENARIES": MercenariesTab(s, body); break;
            case "TAVERN": TavernTab(s, body); break;
            case "HALL": HallTab(s, body); break;
            case "MARKET": MarketTab(s, body); break;
            case "ARMOURER": ArmourerWell(s, body); break;
            case "WORKSHOP": WorkshopTab(s, body); break;
            case "TRAIN": TrainTab(s, body); break;
            case "REST": RestTab(s, body); break;
            case "FOOD": FoodTab(s, body); break;
            case "FORAGE": ForageTab(s, body); break;
            case "CAPTIVES": CaptivesTab(s, body); break;
            case "GARRISON": GarrisonTab(s, body); break;
            case "FIEF": FiefTab(s, body); break;
            case "CONTRACT": ContractTab(s, body); break;
            case "ELDERS": EldersTab(s, body); break;
            case "CHAPTER": ChapterTab(s, body); break;
            case "HARBOUR": HarbourTab(s, body); break;
        }
        Note(r);
        Purse(r);
        if (ui.Gilded(new Rectangle(r.Right - ui.U(200), r.Bottom - ui.U(40), ui.U(176), ui.U(28)), "LEAVE", ctl.Name(Act.Menu))) panel = PanelKind.None;
    }

    /// The tabs a place shows: what its kind offers, less the ones with nothing in them now (no company for hire, no
    /// prisoners to sell), plus its chapter house and, if it is your fief, the fief's own.
    string[] TownTabs(Settlement s, bool fief)
    {
        var tabs = s.Kind switch
        {
            SettlementKind.Village when C.Refuses(s) => new[] { "FORAGE" },
            SettlementKind.Village => new[] { "RECRUIT", "FOOD", "FORAGE", "REST", "ELDERS" },
            SettlementKind.Castle => new[] { "HALL", "ESQUIRES", "REST", "FOOD", "CAPTIVES", "CONTRACT" },
            _ => new[] { "TRAIN", "RECRUIT", "MERCENARIES", "TAVERN", "HALL", "MARKET", "ARMOURER", "WORKSHOP", "FOOD", "REST", "CAPTIVES", "CONTRACT" },
        };
        return tabs.Where(t => t switch { "MERCENARIES" => s.Mercenaries.Count > 0, "CAPTIVES" => C.PrisonerCount > 0, _ => true })
            .Concat(C.IsPort(s) && !C.Refuses(s) ? new[] { "HARBOUR" } : Array.Empty<string>())
            .Concat(C.ChapterAt(s) != null ? new[] { "CHAPTER" } : Array.Empty<string>())
            .Concat(fief ? new[] { "FIEF", "GARRISON" } : Array.Empty<string>()).ToArray();
    }

    /// Harbour tab (docs/WORLDMAP.md WM7): the ports ships sail to from here, a card each: whose it is and where it lies,
    /// how long at sea, the fare for the whole company.
    void HarbourTab(Settlement s, Rectangle body)
    {
        int y = Heading(body.X, body.Y, body.Width, "SHIPS SAIL FROM HERE TO", $"PASSAGE FOR {C.Player.Men + C.Player.Wounded} MEN");
        foreach (var (to, lane) in C.Sailings(s).OrderBy(x => x.Lane.Length))
        {
            int rh = ui.U(56);
            if (y + rh > body.Bottom) break;
            var row = new Rectangle(body.X, y, body.Width, rh - ui.U(6));
            Card(row, SideColor(to.Faction), true);
            ui.Shield(new Rectangle(row.X + ui.U(10), row.Y + ui.U(7), ui.U(30), ui.U(36)), Style.Sides[Style.PaletteOf(to.Faction)]);
            int tx = row.X + ui.U(50), tw = row.Right - ui.U(196) - tx;
            ui.TextFit(to.Name.ToUpperInvariant(), tx, row.Y + ui.U(5), tw, Style.Text, 2);
            ui.TextFit($"{to.Kind.ToString().ToUpperInvariant()} OF THE {C.FactionNames[to.Faction].ToUpperInvariant()}, TO THE {Campaign.Bearing(s.Pos, to.Pos).ToUpperInvariant()}", tx, row.Y + ui.U(23), tw, Style.Dim, 1);
            float hours = C.SailHours(lane), days = hours / 24;
            string time = hours < 24 ? $"{hours:0} HOURS AT SEA" : $"{days:0.#} DAYS AT SEA";
            ui.Icon("clock", tx, row.Bottom - ui.U(13), Style.Dim, 1);
            ui.Text(time, tx + ui.U(11), row.Bottom - ui.U(13), Style.Dim, 1);
            if (C.Player.Food < days) ui.Text($"YOU CARRY {C.Player.Food:0} DAYS OF FOOD", tx + ui.U(22) + ui.TW(time, 1), row.Bottom - ui.U(13), Style.Bad, 1);
            string? why = C.CannotSail(s, to);
            if (ui.Button(new Rectangle(row.Right - ui.U(180), row.Center.Y - ui.U(13), ui.U(172), ui.U(26)), why ?? "SAIL", why == null, why == null ? $"{C.Fare(lane)}" : ""))
            {
                note = C.Sail(s, to) ?? "";
                if (C.Sailing) { panel = PanelKind.None; follow = true; if (speed == 0) speed = 1; }
            }
            y += rh;
        }
    }

    /// A row in a town list: a faint plate that lights under the mouse.
    void TownRow(Rectangle row) => ui.Rect(row, Color.White * (ui.Hover(row) ? 0.07f : 0.03f));

    /// A portrait in a framed well tinted with a side's cloth.
    void PortraitWell(UnitType t, Rectangle port, int palette)
    {
        ui.VGrad(port, Kit.Mul(Style.Sides[palette].Cloth, 0.5f), Kit.IronDark, 2);
        ui.Portrait(t, port, Math.Max(1, port.Height / ui.U(36)) * ui.S);
        ui.Frame(port, Color.Black * 0.6f, ui.U(1));
    }

    /// A troop's fighting numbers as icons: damage, armour, speed.
    void TroopStats(UnitType t, int x, int y)
    {
        foreach (var (icon, v) in new[] { ("sword", $"{t.Damage:0}"), ("shield", $"{t.Armor:0}"), ("boot", $"{t.Speed:0.0}") })
        { ui.Icon(icon, x, y, Style.Dim, 1); ui.Text(v, x + ui.U(11), y, Style.Text, 1); x += ui.U(40); }
    }

    /// A troop on offer as a card: the man, his tier in pips, his numbers, how many are ready, what taking them means
    /// as pictured lines, the price and the buttons.
    void TroopOffer(Rectangle row, UnitType t, int palette, string kind, int ready, int cap, int price, (string Icon, string Text, int Good)[] facts, bool can, Func<int, string?> buy, Func<string> done)
    {
        Card(row, Style.Sides[palette].Cloth, true);
        int ph = row.Height - ui.U(12), per = (int)Balance.Get().ArmyScale;
        PortraitWell(t, new Rectangle(row.X + ui.U(8), row.Y + ui.U(6), ph, ph), palette);
        int tx = row.X + ph + ui.U(20), ty = row.Y + ui.U(8);
        ui.TextFit(t.Name.ToUpperInvariant(), tx, ty, ui.U(320), Style.Text, 2);
        for (int i = 0; i < 7; i++) ui.Rect(new Rectangle(tx + i * ui.U(7), ty + ui.U(21), ui.U(5), ui.U(5)), i < t.Tier ? Kit.Gilt : Color.White * 0.1f);
        ui.Text($"TIER {Style.Numerals[t.Tier]} . {kind}", tx + ui.U(54), ty + ui.U(20), Style.Dim, 1);
        TroopStats(t, tx, ty + ui.U(36));
        ui.Pips(new Rectangle(tx, ty + ui.U(54), ui.U(200), ui.U(7)), cap == 0 ? 0 : ready / (float)cap, Style.Friendly, 12);
        ui.Text($"{ready} OF {cap} READY", tx + ui.U(208), ty + ui.U(54), ready > 0 ? Style.Text : Style.Dim, 1);
        int fx = tx + ui.U(336), fy = ty + ui.U(2), fw = row.Right - ui.U(170) - fx;
        foreach (var (icon, text, good) in facts) fy = Outcome(fx, fy, fw, icon, text, good);
        int bx = row.Right - ui.U(158), by = row.Y + ui.U(8);
        Price(bx, by, price, $"FOR {per} MEN");
        by += ui.U(13);
        foreach (var (label, n) in new[] { ("RECRUIT 1", 1), ("RECRUIT 5", 5), ("RECRUIT ALL", 99) })
        {
            if (ui.Button(new Rectangle(bx, by, ui.U(150), ui.U(22)), label, can, "", n == 99)) note = buy(n) ?? done();
            by += ui.U(25);
        }
    }

    /// Recruit tab: local levies who will take your coin.
    void RecruitTab(Settlement s, Rectangle body)
    {
        var levy = Troops.Get(C.RecruitTroopFor(s));
        int willing = C.RecruitsForYou(s);
        var row = new Rectangle(body.X, body.Y, body.Width, ui.U(104));
        TroopOffer(row, levy, Style.PaletteOf(s.Faction), $"{s.Culture.ToUpperInvariant()} MEN", willing, s.Recruits, C.LevyPrice(s), new[]
        {
            ("helm", "JOIN A COMPANY OF THEIR KIND, OR FORM ONE", 0),
            ("up", $"TRAIN INTO {s.Culture.ToUpperInvariant()} AND COMMON LINES", 1),
            ("star", "NEW MEN BRING NO XP", -1),
        }, willing > 0, n => C.RecruitLevies(s, n), () => $"RECRUITED. {s.Recruits} LEFT");
        if (s.ThreatUntil > C.S.Hours) Outcome(body.X, row.Bottom + ui.U(12), body.Width, "fire", "THE VILLAGE WAS THREATENED: NO RECRUITS FOR NOW", -1);
    }

    /// Esquires tab (docs/NOBILITY.md N2): a castle's house sends noble recruits, on foot and mounted, with its own captains.
    void NoblesTab(Settlement s, Rectangle body)
    {
        string? why = C.CannotRecruitNobles(s);
        int cap = Math.Max(1, C.Scaled(Balance.Get().NobleRecruitsMax)), y = body.Y;
        foreach (bool mounted in new[] { false, true })
        {
            var t = Troops.Get(Campaign.NobleTroop(mounted));
            TroopOffer(new Rectangle(body.X, y, body.Width, ui.U(104)), t, Style.PaletteOf(s.Faction), "NOBLE", s.Nobles, cap, C.NoblePrice(mounted), new[]
            {
                ("crown", "TRAIN ONLY INTO THE NOBLE LINES: KNIGHTS, ORDERS", 1),
                ("chain", "NO DRILL MAKES COMMON MEN NOBLE, NOR THEM COMMON", 0),
            }, why == null, n => C.RecruitNobles(s, mounted, n), () => $"THEY RIDE WITH YOU. {s.Nobles} LEFT");
            y += ui.U(112);
        }
        Outcome(body.X, y + ui.U(4), body.Width, why == null ? "flag" : "chain", why ?? "THE HOUSE TRUSTS ITS OWN CAPTAINS: SWORN TO ITS REALM, OR HOLDING THE CASTLE.", why == null ? 0 : -1);
    }

    /// Train tab: promote men with enough XP up their line; each row shows the man he is and the man he becomes.
    void TrainTab(Settlement s, Rectangle body)
    {
        int y = Heading(body.X, body.Y, body.Width, "THE DRILL YARD", "MEN WITH THE XP RISE UP THEIR LINE"), rh = ui.U(52);
        foreach (var (c, st) in C.Player.Companies.SelectMany(c => c.Stacks.Select(st => (c, st))).OrderByDescending(x => x.st.Ready).ToList())
        {
            if (y + rh - ui.U(4) > body.Bottom) break;
            var row = new Rectangle(body.X, y, body.Width, rh - ui.U(4));
            var nextIds = st.Type.UpgradesTo.Where(to => C.CanEnterLine(C.Player, c, st.Troop, to, out _)).ToList();
            bool top = nextIds.Count == 0, ready = st.Ready > 0;
            Card(row, ready ? Kit.Gilt : null, true);
            int ph = row.Height - ui.U(8), px = row.X + ui.U(8);
            PortraitWell(st.Type, new Rectangle(px, row.Y + ui.U(4), ph, ph), 2);
            if (!top)
            {
                ui.Text(">", px + ph + ui.U(6), row.Center.Y - ui.U(7), ready ? Kit.Gilt : Style.Dim, 2);
                var next = new Rectangle(px + ph + ui.U(22), row.Y + ui.U(4), ph, ph);
                PortraitWell(Troops.Get(nextIds[0]), next, 2);
                if (!ready) ui.Rect(next, Kit.IronDark * 0.55f);
                if (nextIds.Count > 1) ui.Text($"+{nextIds.Count - 1}", next.Right + ui.U(3), next.Bottom - ui.U(9), Style.Dim, 1);
            }
            int tx = px + ph * 2 + ui.U(44), tw = row.Right - ui.U(250) - tx;
            ui.TextFit($"{st.Men} {st.Type.Name.ToUpperInvariant()} {Style.Numerals[st.Type.Tier]}", tx, row.Y + ui.U(6), tw, Style.Text, 1);
            ui.TextFit(c.Name.ToUpperInvariant(), tx, row.Y + ui.U(18), tw, Style.Dim, 1);
            if (top) ui.Text("AT THE TOP OF ITS LINE", tx, row.Y + ui.U(31), Style.Dim, 1);
            else
            {
                ui.Gauge(new Rectangle(tx, row.Y + ui.U(32), Math.Min(tw, ui.U(260)), ui.U(6)), st.PoolCap <= 0 ? 0 : st.Xp / st.PoolCap, ready ? Kit.Gilt : Style.Friendly);
                ui.Text($"{st.Xp:0} / {st.PoolCap:0} XP", tx + Math.Min(tw, ui.U(260)) + ui.U(8), row.Y + ui.U(31), Style.Dim, 1);
                int bx = row.Right - ui.U(236);
                ui.Text($"{st.Ready}", bx, row.Y + ui.U(10), ready ? Kit.Gilt : Style.Dim, 2);
                ui.Text("READY", bx, row.Y + ui.U(28), Style.Dim, 1);
                if (ui.Button(new Rectangle(row.Right - ui.U(158), row.Center.Y - ui.U(13), ui.U(150), ui.U(26)), "PROMOTE...", ready, "", ready))
                { upgrading = c; upStack = st; panel = PanelKind.Upgrade; note = ""; }
            }
            y += rh;
        }
    }

    /// Rest tab: the fit and the wounded as one bar, how fast they heal here, and the two ways to wait.
    void RestTab(Settlement s, Rectangle body)
    {
        bool town = s.Kind == SettlementKind.Town;
        int fit = C.Player.Men, hurt = C.Player.Wounded, all = Math.Max(1, fit + hurt);
        float coh = C.Player.Companies.Count == 0 ? 0 : C.Player.Companies.Average(k => k.Cohesion);
        var card = new Rectangle(body.X, body.Y, body.Width, ui.U(132));
        var c = Card(card, hurt > 0 ? Style.Warn : Style.Ok);
        ui.Ring(new Vector2(c.X + ui.U(44), c.Y + ui.U(46)), ui.U(36), fit / (float)all, hurt > 0 ? Style.Warn : Style.Ok, $"{hurt}", "WOUNDED");
        int tx = c.X + ui.U(104), tw = c.Right - tx;
        ui.Text(town ? "A TOWN'S BEDS AND BARBERS" : s.Kind == SettlementKind.Castle ? "A COLD BILLET" : "BARNS AND STRAW", tx, c.Y, Style.Text, 2);
        var bar = new Rectangle(tx, c.Y + ui.U(24), tw, ui.U(12));
        int fw = (int)(bar.Width * (fit / (float)all));
        ui.Rect(bar, Color.Black * 0.5f);
        ui.VGrad(new Rectangle(bar.X, bar.Y, fw, bar.Height), Style.Ok, Kit.Mul(Style.Ok, 0.6f), 1);
        if (hurt > 0) ui.VGrad(new Rectangle(bar.X + fw, bar.Y, bar.Width - fw, bar.Height), Style.Warn, Kit.Mul(Style.Warn, 0.6f), 1);
        ui.Frame(bar, Color.Black * 0.6f, ui.U(1));
        ui.Text($"{fit} FIT", tx, bar.Bottom + ui.U(4), Style.Ok, 1);
        ui.TextR($"{hurt} WOUNDED", bar.Right, bar.Bottom + ui.U(4), hurt > 0 ? Style.Warn : Style.Dim, 1);
        int oy = bar.Bottom + ui.U(22), half = tw / 2;
        ui.Text("HEALING", tx, oy + ui.U(4), Style.Dim, 1);
        for (int i = 0; i < 3; i++) ui.Icon("up", tx + ui.U(54) + i * ui.U(17), oy, i < (town ? 3 : 1) ? Style.Ok : new Color(52, 56, 64), 2);
        ui.Text(town ? "FAST" : "SLOW", tx + ui.U(110), oy + ui.U(4), town ? Style.Ok : Style.Warn, 1);
        ui.Text("COHESION", tx + half, oy + ui.U(4), Style.Dim, 1);
        ui.Gauge(new Rectangle(tx + half + ui.U(60), oy + ui.U(4), half - ui.U(96), ui.U(7)), Math.Clamp(coh / 25f, 0, 1), Style.Friendly);
        ui.TextR($"{coh:0}", c.Right, oy + ui.U(4), Style.Text, 1);
        Outcome(tx, oy + ui.U(22), tw, "heart", "RESTING IN A TOWN ALSO RESTORES COHESION, UP TO 20", town ? 1 : 0);
        int by = card.Bottom + ui.U(12), bw = (body.Width - ui.U(12)) / 2;
        if (BigButton(new Rectangle(body.X, by, bw, ui.U(44)), "clock", "REST 1 DAY")) { C.Rest(24); note = $"RESTED. WOUNDED: {C.Player.Wounded}"; }
        if (BigButton(new Rectangle(body.X + bw + ui.U(12), by, bw, ui.U(44)), "heart", "REST UNTIL HEALED", hurt > 0, "", hurt > 0))
        {
            for (int d = 0; d < 10 && C.Player.Wounded > 0 && C.Encounter == null; d++) C.Rest(24);
            note = $"RESTED. WOUNDED: {C.Player.Wounded}";
        }
        // Each company: its fit and wounded as a bar, its cohesion beside it.
        int y = Heading(body.X, by + ui.U(56), body.Width, "YOUR COMPANIES"), n = 0, colW = (body.Width - ui.U(12)) / 2;
        foreach (var k in C.Player.Companies)
        {
            var row = new Rectangle(body.X + n % 2 * (colW + ui.U(12)), y + n / 2 * ui.U(34), colW, ui.U(30));
            n++;
            if (row.Bottom > body.Bottom) break;
            Card(row, k.Wounded > 0 ? Style.Warn : null);
            PortraitWell(k.Main?.Type ?? UnitType.Captain, new Rectangle(row.X + ui.U(5), row.Y + ui.U(3), ui.U(24), ui.U(24)), 2);
            ui.TextFit(k.Name.ToUpperInvariant(), row.X + ui.U(34), row.Y + ui.U(4), row.Width - ui.U(170), Style.Text, 1);
            int m = Math.Max(1, k.Men + k.Wounded), mw = row.Width - ui.U(250), mf = mw * k.Men / m;
            ui.Rect(new Rectangle(row.X + ui.U(34), row.Y + ui.U(17), mw, ui.U(6)), Color.Black * 0.5f);
            ui.Rect(new Rectangle(row.X + ui.U(34), row.Y + ui.U(17), mf, ui.U(6)), Kit.Mul(Style.Ok, 0.85f));
            if (k.Wounded > 0) ui.Rect(new Rectangle(row.X + ui.U(34) + mf, row.Y + ui.U(17), mw - mf, ui.U(6)), Style.Warn);
            ui.Text(k.Wounded > 0 ? $"{k.Wounded} HURT" : $"{k.Men} FIT", row.X + ui.U(40) + mw, row.Y + ui.U(16), k.Wounded > 0 ? Style.Warn : Style.Dim, 1);
            ui.Icon("heart", row.Right - ui.U(96), row.Y + ui.U(5), Style.Friendly, 1);
            ui.Gauge(new Rectangle(row.Right - ui.U(84), row.Y + ui.U(6), ui.U(76), ui.U(5)), Math.Clamp(k.Cohesion / 25f, 0, 1), Style.Friendly);
            ui.TextR($"COHESION {k.Cohesion:0}", row.Right - ui.U(8), row.Y + ui.U(16), Style.Dim, 1);
        }
    }

    /// Food tab: the larder as three weeks of days (a lot under the mouse shows what it would add), then three lots to buy.
    void FoodTab(Settlement s, Rectangle body)
    {
        float days = C.Player.Food;
        var lots = new[] { (Days: 3, Name: "BREAD AND CHEESE", Loaves: 1), (Days: 7, Name: "GRAIN AND SALT PORK", Loaves: 2), (Days: 14, Name: "A WAGON OF STORES", Loaves: 3) };
        int cw = (body.Width - ui.U(24)) / 3, cy = body.Y + ui.U(120), preview = 0;
        Rectangle Lot(int i) => new(body.X + i * (cw + ui.U(12)), cy, cw, Math.Min(ui.U(150), body.Bottom - cy));
        for (int i = 0; i < 3; i++) if (ui.Hover(Lot(i))) preview = lots[i].Days;
        var top = Card(new Rectangle(body.X, body.Y, body.Width, ui.U(108)), days < 3 ? Style.Bad : Style.Ok);
        ui.Ring(new Vector2(top.X + ui.U(40), top.Y + ui.U(40)), ui.U(32), Math.Clamp(days / 14f, 0, 1), days < 3 ? Style.Bad : Style.Ok, $"{days:0}", "DAYS");
        int tx = top.X + ui.U(96), tw = top.Right - tx;
        ui.TextFit($"{C.Season.ToUpperInvariant()}{(C.Winter ? ": MEN EAT HALF AGAIN AS MUCH" : "")}", tx, top.Y, tw, C.Winter ? Style.Warn : Style.Text, 2);
        const int cells = 21;
        int gap = ui.U(3), cellW = (tw - gap * (cells - 1)) / cells;
        for (int i = 0; i < cells; i++)
        {
            var cell = new Rectangle(tx + i * (cellW + gap), top.Y + ui.U(24), cellW, ui.U(16));
            bool have = i < (int)days, more = !have && i < (int)days + preview;
            ui.Rect(cell, have ? (days < 3 ? Style.Bad : Kit.Gilt) : more ? Kit.Mul(Style.Ok, 0.75f) : Color.White * 0.07f);
            if (have || more) ui.Rect(new Rectangle(cell.X, cell.Bottom - ui.U(4), cell.Width, ui.U(4)), Color.Black * 0.25f);
            if (i % 7 == 6) ui.TextR($"{(i + 1) / 7} WEEK{(i > 6 ? "S" : "")}", cell.Right, cell.Bottom + ui.U(3), Style.Dim, 1);
        }
        int oy = top.Y + ui.U(62);
        Outcome(tx, oy, tw / 2 - ui.U(8), "skull", "STARVING MEN LOSE COHESION, THEN DESERT", days < 3 ? -1 : 0);
        if (C.PrisonerCount > 0) Outcome(tx + tw / 2, oy, tw / 2, "chain", $"YOUR {C.PrisonerCount} PRISONERS EAT TOO", -1);
        for (int i = 0; i < 3; i++)
        {
            var k = Card(Lot(i), Kit.GiltDark, true);
            for (int j = 0; j < lots[i].Loaves; j++) ui.Icon("bread", k.X + j * ui.U(32), k.Y, Kit.Gilt, 4);
            ui.Text($"+{lots[i].Days} DAYS", k.X, k.Y + ui.U(36), Kit.Gilt, 3);
            ui.TextFit(lots[i].Name, k.X, k.Y + ui.U(62), k.Width, Style.Dim, 1);
            int price = C.FoodPrice(lots[i].Days);
            if (ui.Button(new Rectangle(k.X, k.Bottom - ui.U(26), k.Width, ui.U(26)), "BUY", C.S.Gold >= price, $"{price}")) note = C.BuyFood(lots[i].Days) ?? "BOUGHT";
        }
    }

    /// Workshop tab (docs/SIEGECRAFT.md A4): light engines for your train; each takes a crew from a foot company in battle.
    void WorkshopTab(Settlement s, Rectangle body)
    {
        var train = C.Player.Train;
        var top = Card(new Rectangle(body.X, body.Y, body.Width, ui.U(50)), Kit.GiltDark);
        ui.Text("YOUR TRAIN", top.X, top.Y, Kit.Gilt, 1);
        int cx = top.X;
        foreach (var g in train.GroupBy(id => id)) cx += ui.Chip(cx, top.Y + ui.U(14), $"{g.Count()} {Engines.Get(g.Key).Name.ToUpperInvariant()}", Style.Text, "hammer") + ui.U(4);
        if (train.Count == 0) ui.Text("NO ENGINES YET", top.X, top.Y + ui.U(16), Style.Dim, 1);
        Stat(top.Right - ui.U(120), top.Y, "boot", $"-{train.Count * Campaign.TrainDrag * 100:0}%", "MARCH SPEED", train.Count > 0 ? Style.Warn : Style.Dim);
        int half = (body.Width - ui.U(12)) / 2, y = body.Y + ui.U(60);
        Outcome(body.X, y, half, "helm", "EACH ENGINE TAKES ITS CREW FROM A FOOT COMPANY");
        Outcome(body.X + half + ui.U(12), y, half, "skull", "A LOST BATTLE LOSES THE TRAIN", -1);
        y += ui.U(24);
        float far = Math.Max(1, Campaign.TrainDefs.Max(d => d.Range));
        int n = 0;
        foreach (var def in Campaign.TrainDefs)
        {
            var row = new Rectangle(body.X + n % 2 * (half + ui.U(12)), y + n / 2 * ui.U(92), half, ui.U(84));
            n++;
            if (row.Bottom > body.Bottom) break;
            int owned = train.Count(id => id == def.Id);
            var k = Card(row, owned > 0 ? Kit.Gilt : null, true);
            int tw = k.Width - ui.U(130);
            ui.TextFit(def.Name.ToUpperInvariant(), k.X, k.Y, tw - ui.U(40), Style.Text, 2);
            if (owned > 0) ui.TextR($"X{owned}", k.X + tw, k.Y + ui.U(4), Kit.Gilt, 1);
            ui.TextFit(def.Line.ToUpperInvariant(), k.X, k.Y + ui.U(20), tw, Style.Dim, 1);
            int sx = k.X;
            foreach (var (icon, v) in new[] { ("arrow", $"{def.Range:0} M"), ("helm", $"CREW {def.Crew}"), ("spear", $"THROUGH {def.Pierce}") })
            { ui.Icon(icon, sx, k.Y + ui.U(36), Style.Dim, 1); ui.Text(v, sx + ui.U(11), k.Y + ui.U(36), Style.Text, 1); sx += ui.U(22) + ui.TW(v, 1); }
            ui.Gauge(new Rectangle(k.X, k.Y + ui.U(52), tw, ui.U(6)), def.Range / far, Style.Friendly);
            var why = C.CannotBuyEngine(s, def.Id);
            var buy = new Rectangle(k.Right - ui.U(120), k.Y + ui.U(2), ui.U(120), ui.U(26));
            if (ui.Button(buy, "BUY", why == null, $"{def.Cost:0}")) note = C.BuyEngine(s, def.Id) ?? "BOUGHT";
            if (why != null && ui.Hover(buy)) note = why;
            if (ui.Button(new Rectangle(buy.X, buy.Bottom + ui.U(6), buy.Width, ui.U(26)), "SELL", owned > 0, $"{def.Cost / 2:0}")) note = C.SellEngine(def.Id) ?? "SOLD";
        }
    }

    /// Forage tab: take food from a village by force; what it gains and costs you as pictured lines.
    void ForageTab(Settlement s, Rectangle body)
    {
        bool foe = C.Refuses(s), spent = s.ThreatUntil > C.S.Hours;
        var card = new Rectangle(body.X, body.Y, body.Width, ui.U(124));
        var c = Card(card, foe ? Style.Bad : Style.Warn);
        ui.Icon("wheat", c.X + ui.U(6), c.Y + ui.U(10), Kit.Gilt, 7);
        int tx = c.X + ui.U(76), tw = c.Right - tx;
        ui.Text(foe ? "AN ENEMY VILLAGE" : "TAKE IT BY FORCE", tx, c.Y, Style.Text, 2);
        ui.Text(foe ? "ITS PEOPLE BAR THEIR DOORS." : "YOUR MEN CAN TAKE WHAT THEY NEED.", tx, c.Y + ui.U(20), Style.Dim, 1);
        int y = c.Y + ui.U(38);
        y = Outcome(tx, y, tw, "bread", "A WEEK OF FOOD", 1);
        y = Outcome(tx, y, tw, "helm", "THE VILLAGE GIVES NO RECRUITS FOR A WEEK", -1);
        if (foe) Outcome(tx, y, tw, "sword", "RAIDING AN ENEMY'S VILLAGE COUNTS IN THE WAR", 1);
        else Outcome(tx, y, tw, "star", "-5 RENOWN: YOUR LIEGE'S PEOPLE WILL REMEMBER IT", -1);
        if (BigButton(new Rectangle(body.X, card.Bottom + ui.U(12), ui.U(320), ui.U(44)), "wheat", spent ? "ALREADY STRIPPED" : "FORAGE", !spent)) note = C.Forage(s) ?? "FORAGED";
    }

    /// Captives tab: the prisoners by kind with what they fetch, and the lords you hold, each under his realm's arms.
    void CaptivesTab(Settlement s, Rectangle body)
    {
        int y = Heading(body.X, body.Y, body.Width, "PRISONERS", $"{C.PrisonerCount} HELD");
        if (C.PrisonerCount == 0)
        {
            var e = Card(new Rectangle(body.X, y, body.Width, ui.U(40)));
            ui.Icon("chain", e.X, e.Y + ui.U(3), Style.Dim, 2);
            ui.TextFit("NONE. MEN CAPTURED IN BATTLE ARE KEPT UNTIL YOU SELL THEM HERE.", e.X + ui.U(24), e.Y + ui.U(7), e.Width - ui.U(24), Style.Dim, 1);
            y += ui.U(52);
        }
        else
        {
            var sale = Card(new Rectangle(body.Right - ui.U(230), y, ui.U(230), ui.U(84)), Kit.Gilt);
            ui.Icon("coin", sale.X, sale.Y + ui.U(2), Kit.Gilt, 3);
            ui.Text($"{C.PrisonerValue:0}", sale.X + ui.U(28), sale.Y + ui.U(5), Kit.Gilt, 2);
            ui.Text("CROWNS FOR THEM ALL", sale.X, sale.Y + ui.U(27), Style.Dim, 1);
            if (ui.Button(new Rectangle(sale.X, sale.Bottom - ui.U(24), sale.Width, ui.U(24)), "SELL THEM ALL")) note = C.SellPrisoners(s) ?? "SOLD";
            int gw = ui.U(164), cols = Math.Max(1, (body.Width - ui.U(240)) / (gw + ui.U(6))), i = 0;
            foreach (var g in C.Player.Prisoners.Take(cols * 2))
            {
                var cell = new Rectangle(body.X + i % cols * (gw + ui.U(6)), y + i / cols * ui.U(44), gw, ui.U(38));
                var t = Troops.Get(g.Troop);
                Card(cell);
                PortraitWell(t, new Rectangle(cell.X + ui.U(4), cell.Y + ui.U(4), ui.U(30), ui.U(30)), 3);
                ui.Text($"{g.Count}", cell.X + ui.U(40), cell.Y + ui.U(5), Style.Text, 2);
                ui.TextFit(t.Name.ToUpperInvariant(), cell.X + ui.U(40), cell.Y + ui.U(24), gw - ui.U(44), Style.Dim, 1);
                i++;
            }
            if (C.Player.Prisoners.Count > cols * 2) ui.Text($"+{C.Player.Prisoners.Count - cols * 2} MORE KINDS", body.X, y + ui.U(90), Style.Dim, 1);
            y += ui.U(104);
        }
        y = Heading(body.X, y, body.Width, "LORDS YOU HOLD FOR RANSOM");
        if (C.Player.Captives.Count == 0)
        {
            var e = Card(new Rectangle(body.X, y, body.Width, ui.U(40)));
            ui.Icon("crown", e.X, e.Y + ui.U(3), Style.Dim, 2);
            ui.TextFit("NONE. A LORD BEATEN BY YOUR HAND MAY BE TAKEN.", e.X + ui.U(24), e.Y + ui.U(7), e.Width - ui.U(24), Style.Dim, 1);
        }
        foreach (int id in C.Player.Captives.ToList())
        {
            if (C.PartyById(id) is not Party lord || y + ui.U(40) > body.Bottom) continue;
            var row = new Rectangle(body.X, y, body.Width, ui.U(40));
            Card(row, SideColor(lord.Faction), true);
            ui.Shield(new Rectangle(row.X + ui.U(10), row.Y + ui.U(5), ui.U(25), ui.U(30)), Style.Sides[Style.PaletteOf(lord.Faction)]);
            ui.TextFit(lord.Name.ToUpperInvariant(), row.X + ui.U(44), row.Y + ui.U(7), row.Width - ui.U(430), Style.Text, 2);
            ui.Text(C.FactionNames[lord.Faction].ToUpperInvariant(), row.X + ui.U(44), row.Y + ui.U(26), SideColor(lord.Faction), 1);
            if (ui.Button(new Rectangle(row.Right - ui.U(372), row.Y + ui.U(7), ui.U(200), ui.U(26)), "RANSOM", true, $"{C.LordRansom(lord)}")) note = C.RansomLord(lord, s) ?? "RANSOMED";
            if (ui.Button(new Rectangle(row.Right - ui.U(164), row.Y + ui.U(7), ui.U(156), ui.U(26)), "SET FREE", true, "+RENOWN")) C.FreeLord(lord);
            y += ui.U(46);
        }
    }

    /// Garrison tab (your fief): what the place pays and holds, the companies on its walls and the ones with you.
    void GarrisonTab(Settlement s, Rectangle body)
    {
        float income = C.Income(s) + C.S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village).Sum(C.Income);
        int men = s.Garrison.Sum(g => g.Men);
        var top = Card(new Rectangle(body.X, body.Y, body.Width, ui.U(50)), Kit.Gilt);
        Stat(top.X, top.Y, "coin", $"{income:0}", "CROWNS A WEEK", Kit.Gilt);
        Stat(top.X + top.Width / 3, top.Y, "bread", $"{s.Supplies:0}", "DAYS OF SUPPLIES", s.Supplies < 5 ? Style.Warn : Style.Text);
        Stat(top.X + top.Width * 2 / 3, top.Y, "tower", $"{men}", "MEN ON THE WALLS", men == 0 ? Style.Warn : Style.Text);
        int half = (body.Width - ui.U(12)) / 2, wy = body.Y + ui.U(60), wh = body.Bottom - wy;
        var left = Well(new Rectangle(body.X, wy, half, wh), "ON THE WALLS", Kit.Gilt);
        var right = Well(new Rectangle(body.X + half + ui.U(12), wy, half, wh), "WITH YOU", Style.Friendly);
        int y = left.Y;
        if (s.Garrison.Count == 0) Outcome(left.X, y + ui.U(4), left.Width, "tower", "NO MEN HOLD THE WALLS. STATION A COMPANY.", -1);
        foreach (var g in s.Garrison.ToList())
        {
            if (y + ui.U(38) > left.Bottom) break;
            var row = new Rectangle(left.X, y, left.Width, ui.U(38));
            Card(row, null, true);
            PortraitWell(g.Type, new Rectangle(row.X + ui.U(4), row.Y + ui.U(4), ui.U(30), ui.U(30)), 2);
            ui.TextFit(g.Name.ToUpperInvariant(), row.X + ui.U(40), row.Y + ui.U(7), row.Width - ui.U(150), Style.Text, 1);
            ui.TextFit($"{g.Men} {g.Type.Name.ToUpperInvariant()}", row.X + ui.U(40), row.Y + ui.U(20), row.Width - ui.U(150), Style.Dim, 1);
            if (ui.Button(new Rectangle(row.Right - ui.U(104), row.Y + ui.U(7), ui.U(98), ui.U(24)), "TAKE")) note = C.Withdraw(g, s) ?? "";
            y += ui.U(42);
        }
        y = right.Y;
        foreach (var c in C.Player.Companies.ToList())
        {
            if (y + ui.U(38) > right.Bottom) break;
            var row = new Rectangle(right.X, y, right.Width, ui.U(38));
            Card(row, null, true);
            PortraitWell(c.Main?.Type ?? UnitType.Captain, new Rectangle(row.X + ui.U(4), row.Y + ui.U(4), ui.U(30), ui.U(30)), 2);
            ui.TextFit(c.Name.ToUpperInvariant(), row.X + ui.U(40), row.Y + ui.U(7), row.Width - ui.U(150), Style.Text, 1);
            ui.TextFit($"{c.Men} MEN", row.X + ui.U(40), row.Y + ui.U(20), row.Width - ui.U(150), Style.Dim, 1);
            if (ui.Button(new Rectangle(row.Right - ui.U(104), row.Y + ui.U(7), ui.U(98), ui.U(24)), "STATION", C.Player.Companies.Count > 1)) note = C.Station(c, s) ?? "";
            y += ui.U(42);
        }
    }

    /// Contract tab: whom you serve under their arms, what a contract means as pictured lines, and the steward's board of jobs.
    void ContractTab(Settlement s, Rectangle body)
    {
        int cur = C.Captain.Contract;
        bool town = s.Kind == SettlementKind.Town;
        int lw = town ? ui.U(372) : body.Width;
        var c = Card(new Rectangle(body.X, body.Y, lw, body.Height), cur >= 0 ? SideColor(cur) : Style.Dim);
        if (cur >= 0) ui.Shield(new Rectangle(c.X, c.Y, ui.U(46), ui.U(55)), Style.Sides[Style.PaletteOf(cur)]);
        else ui.Icon("flag", c.X + ui.U(6), c.Y + ui.U(8), Style.Dim, 5);
        int tx = c.X + ui.U(58), tw = c.Right - tx;
        ui.Text(cur >= 0 ? "YOU SERVE" : "YOU SERVE NO ONE", tx, c.Y + ui.U(6), Style.Dim, 1);
        string who = cur >= 0 ? "THE " + C.FactionNames[cur].ToUpperInvariant() : "A FREE CAPTAIN";
        ui.TextFit(who, tx, c.Y + ui.U(20), tw, cur >= 0 ? SideColor(cur) : Style.Text, ui.FitScale(who, tw));
        if (cur < 0) ui.TextFit("NO PAY, NO ENEMIES BUT BRIGANDS", tx, c.Y + ui.U(42), tw, Style.Dim, 1);
        int y = Heading(c.X, c.Y + ui.U(70), c.Width, "WHAT A CONTRACT MEANS");
        y = Outcome(c.X, y, c.Width, "coin", "PAYS 85% OF YOUR UPKEEP, EVERY WEEK", 1);
        y = Outcome(c.X, y, c.Width, "skull", "BOUNTIES FOR BRIGANDS AND ENEMY LORDS", 1);
        y = Outcome(c.X, y, c.Width, "sword", "YOUR EMPLOYER'S ENEMIES BECOME YOURS", -1);
        y += ui.U(10);
        int bw = Math.Min(c.Width, ui.U(420));
        if (cur != s.Faction)
        {
            if (BigButton(new Rectangle(c.X, y, bw, ui.U(38)), "scroll", $"SIGN WITH THE {C.FactionNames[s.Faction].ToUpperInvariant()}")) note = C.SignContract(s.Faction) ?? "";
            y += ui.U(46);
        }
        if (cur >= 0 && ui.Button(new Rectangle(c.X, y, bw, ui.U(26)), C.Captain.Fealty ? "RENOUNCE YOUR OATH" : "END YOUR CONTRACT")) note = C.SignContract(-1) ?? "";
        if (!town) return;
        var board = Well(new Rectangle(body.X + lw + ui.U(12), body.Y, body.Width - lw - ui.U(12), body.Height), "THE STEWARD'S BOARD", Kit.Gilt);
        if (cur == s.Faction) JobCards(s, board, board.Y, C.Offers(s).ToList(), "NO WORK ON THE BOARD TODAY.");
        else
        {
            ui.Icon("chain", board.Center.X - ui.U(17), board.Y + ui.U(40), Style.Dim, 5);
            int ly = board.Y + ui.U(90);
            foreach (var line in ui.Wrap($"THE BOARD IS FOR THOSE WHO SERVE THE {C.FactionNames[s.Faction].ToUpperInvariant()}. SIGN WITH THEM TO SEE IT.", board.Width - ui.U(40)))
            { ui.TextC(line, board.Center.X, ly, Style.Dim, 1); ly += ui.U(12); }
        }
    }

    /// Elders tab (villages, S3): the village's own small jobs, open to any captain.
    void EldersTab(Settlement s, Rectangle body)
    {
        var top = Card(new Rectangle(body.X, body.Y, body.Width, ui.U(50)), Kit.GiltDark);
        ui.Icon("house", top.X, top.Y, Kit.Gilt, 4);
        ui.TextFit("THE ELDERS OF " + s.Name.ToUpperInvariant() + " ASK FOR HELP", top.X + ui.U(38), top.Y, top.Width - ui.U(38), Style.Text, 2);
        ui.TextFit("THEIR JOBS NEED NO CONTRACT. THEY PAY LITTLE, BUT THE VILLAGE PROSPERS.", top.X + ui.U(38), top.Y + ui.U(20), top.Width - ui.U(38), Style.Dim, 1);
        JobCards(s, body, body.Y + ui.U(60), C.ElderOffers(s).ToList(), "NOTHING TODAY. COME BACK IN A FEW DAYS.");
    }

    /// Tavern tab (S5): a wanderer looking for a captain (3.3), and what people are saying (rumours of real things nearby).
    void TavernTab(Settlement s, Rectangle body)
    {
        // Every action button on the tab is one size, on one right edge; sections are headed and evenly spaced.
        int y = body.Y, bw = ui.U(220), bh = ui.U(24), gap = ui.U(10), textW = body.Width - bw - ui.U(28);
        Rectangle Act(Rectangle row) => new(row.Right - bw - ui.U(6), row.Center.Y - bh / 2, bw, bh);
        void Head(string title)
        {
            ui.Text(title, body.X, y, Kit.Gilt, 1);
            ui.Divider(body.X + ui.TW(title, 1) + ui.U(8), y + ui.U(3), body.Width - ui.TW(title, 1) - ui.U(8), Kit.Mul(Kit.Gilt, 0.62f));
            y += ui.U(14);
        }
        float hour = (float)(C.S.Hours % 24);
        bool night = hour is >= 20 or < 5;
        Head(night ? "TONIGHT" : "TODAY");
        if (C.WandererAt(s) is Character w)
        {
            var row = new Rectangle(body.X, y, body.Width, ui.U(56));
            TavernRow(row);
            var port = new Rectangle(row.X + ui.U(4), row.Y + ui.U(4), row.Height - ui.U(8), row.Height - ui.U(8));
            PortraitWell(UnitType.Captain, port, 2);
            ui.Frame(port, Kit.GiltDark, ui.U(1));
            int tx = row.X + row.Height + ui.U(6), tw = row.Right - bw - ui.U(18) - tx; // clear of the button
            ui.TextFit($"{w.Name.ToUpperInvariant()}, A WANDERER", tx, row.Y + ui.U(10), tw, Kit.Gilt, 2);
            string traits = w.Traits.Count > 0 ? string.Join(", ", w.Traits).ToUpperInvariant() + ". " : "";
            ui.TextFit($"{w.Role.ToUpperInvariant()} ({RoleTextOf(w.Role)}). {traits}LEADS A COMPANY AS A HERO.", tx, row.Y + ui.U(32), tw, Style.Dim, 1);
            int wp = C.WandererPrice(w);
            if (ui.Button(Act(row), "TAKE INTO SERVICE", C.S.Gold >= wp, $"{wp} CROWNS"))
                note = C.HireWanderer(s) ?? "HE JOINS YOUR HOUSEHOLD";
            y = row.Bottom + gap;
        }
        else
        {
            var row = new Rectangle(body.X, y, body.Width, ui.U(30));
            TavernRow(row);
            ui.TextFit("NO WANDERER IS DRINKING HERE TONIGHT. THEY MOVE BETWEEN TOWNS.", row.X + ui.U(8), row.Y + ui.U(11), row.Width - ui.U(16), Style.Dim, 1);
            y = row.Bottom + gap;
        }
        // A feast (A5): three days in good heart, carried into battle.
        y -= gap - ui.U(4);
        var feast = new Rectangle(body.X, y, body.Width, ui.U(30));
        TavernRow(feast);
        Goblet(feast.X + ui.U(9), feast.Y + ui.U(4));
        string fl = C.HasBuff("feasted") ? $"FEASTED: {C.BuffDays("feasted"):0.0} DAYS LEFT" : "HOLD A FEAST: THREE DAYS STRONGER AND STEADIER, IN BATTLE TOO";
        ui.TextFit(fl, feast.X + ui.U(36), feast.Y + ui.U(11), textW - ui.U(28), Kit.Parchment, 1);
        if (ui.Button(Act(feast), "FEAST THE COMPANY", C.S.Gold >= C.FeastPrice && !C.HasBuff("feasted"), $"{C.FeastPrice} CROWNS"))
            note = C.Feast(s) ?? "THE COMPANY FEASTS";
        y = feast.Bottom + gap;
        Head("TALK IN THE TAVERN");
        foreach (var talk in C.Rumours(s))
        {
            if (y + ui.U(30) > body.Bottom) break;
            var row = new Rectangle(body.X, y, body.Width, ui.U(30));
            TavernRow(row);
            ui.Icon("scroll", row.X + ui.U(7), row.Y + ui.U(5), Color.Black * 0.8f, 3);
            ui.Icon("scroll", row.X + ui.U(6), row.Y + ui.U(4), Kit.Mul(Kit.Gilt, 0.85f), 3);
            ui.TextFit("\"" + talk.Text.ToUpperInvariant() + "\"", row.X + ui.U(36), row.Y + ui.U(11), textW - ui.U(28), Kit.Parchment, 1);
            if (talk.At != s.Pos && ui.Button(Act(row), "RIDE THERE", true, Campaign.Bearing(s.Pos, talk.At).ToUpperInvariant()))
            { C.Follow(talk); panel = PanelKind.None; at = null; }
            y += ui.U(34);
        }
    }

    /// A gilt goblet, drawn for the feast row (the icon set has none).
    void Goblet(int x, int y)
    {
        var gold = new Color(255, 214, 120);
        for (int pass = 0; pass < 2; pass++)
        {
            var c = pass == 0 ? Color.Black * 0.8f : gold;
            int o = pass == 0 ? ui.U(1) : 0;
            ui.Rect(new Rectangle(x + o, y + o, ui.U(14), ui.U(2)), c);                       // the rim
            ui.Rect(new Rectangle(x + ui.U(1) + o, y + ui.U(2) + o, ui.U(12), ui.U(5)), c);    // the bowl
            ui.Rect(new Rectangle(x + ui.U(3) + o, y + ui.U(7) + o, ui.U(8), ui.U(2)), c);
            ui.Rect(new Rectangle(x + ui.U(6) + o, y + ui.U(9) + o, ui.U(2), ui.U(8)), c);     // the stem
            ui.Rect(new Rectangle(x + ui.U(3) + o, y + ui.U(17) + o, ui.U(8), ui.U(3)), c);    // the foot
        }
        ui.Rect(new Rectangle(x + ui.U(2), y + ui.U(2), ui.U(10), ui.U(2)), new Color(150, 40, 50)); // wine
        ui.Rect(new Rectangle(x + ui.U(2), y + ui.U(4), ui.U(2), ui.U(3)), Color.White * 0.6f);       // a glint
    }

    /// A row in the tavern: a plate warm as old wood, a gilt edge on the left, lighter under the mouse.
    void TavernRow(Rectangle row)
    {
        ui.Rect(row, new Color(110, 72, 40) * (ui.Hover(row) ? 0.26f : 0.16f));
        ui.Rect(new Rectangle(row.X, row.Y, ui.U(2), row.Height), Kit.Mul(Kit.GiltDark, 1.1f));
    }

    /// Mercenaries tab: companies for hire.
    void MercenariesTab(Settlement s, Rectangle body)
    {
        if (s.Mercenaries.Count == 0) ui.Text("NO COMPANIES FOR HIRE THIS WEEK. NEW ONES COME EVERY WEEK.", body.X, body.Y, Style.Dim, 1);
        int y = body.Y, rh = ui.U(66);
        Rectangle Next() { var row = new Rectangle(body.X, y, body.Width, rh - ui.U(6)); y += rh; TownRow(row); return row; }
        foreach (var m in s.Mercenaries.ToList())
        {
            if (y + rh - ui.U(6) > body.Bottom) break;
            var row = Next();
            PortraitWell(m.Type, new Rectangle(row.X + ui.U(4), row.Y + ui.U(4), row.Height - ui.U(8), row.Height - ui.U(8)), 3);
            int tx = row.X + row.Height + ui.U(6);
            ui.Text(m.Name.ToUpperInvariant(), tx, row.Y + ui.U(8), Style.Text, 2);
            int cx = tx;
            foreach (var st in m.Stacks) cx += ui.Chip(cx, row.Y + ui.U(26), $"{st.Men} {st.Type.Name.ToUpperInvariant()} {Style.Numerals[st.Type.Tier]}", Style.Dim, "shield") + ui.U(4);
            if (m.Honours.Count > 0)
                ui.Text("HONOURS: " + string.Join(", ", m.Honours.Select(h => Honours.Get(h)?.Name.ToUpperInvariant() ?? h)), tx, row.Y + ui.U(44), Kit.Gilt, 1);
            int price = C.MercenaryPrice(m);
            if (ui.Button(new Rectangle(row.Right - ui.U(196), row.Center.Y - ui.U(14), ui.U(190), ui.U(28)), "HIRE", C.S.Gold >= price, $"{price} CROWNS"))
                note = C.Hire(s, m) ?? "HIRED";
        }
    }

    /// Market tab: horses on the left, the armourer (your own gear) on the right.
    void MarketTab(Settlement s, Rectangle body)
    {
        int half = (body.Width - ui.U(12)) / 2, y = body.Y;
        var c = Well(new Rectangle(body.X, y, half, body.Height), "THE STABLES", Kit.Gilt);
        var row = new Rectangle(c.X, c.Y, c.Width, ui.U(70));
        TownRow(row);
        ui.Icon("horse", row.X + ui.U(10), row.Y + ui.U(14), Style.Text, 3);
        ui.Text("HORSES", row.X + ui.U(48), row.Y + ui.U(10), Style.Text, 2);
        ui.Text($"{s.Horses} FOR SALE . YOU HAVE {C.Player.Horses}", row.X + ui.U(48), row.Y + ui.U(30), Style.Dim, 1);
        ui.Icon("coin", row.X + ui.U(48), row.Y + ui.U(44), Kit.Gilt, 1);
        ui.Text($"{C.HorsePrice} A HORSE", row.X + ui.U(60), row.Y + ui.U(44), Kit.Gilt, 1);
        if (ui.Button(new Rectangle(row.Right - ui.U(84), row.Y + ui.U(8), ui.U(78), ui.U(24)), "BUY 1", s.Horses > 0)) note = C.BuyHorses(s, 1) ?? "BOUGHT";
        if (ui.Button(new Rectangle(row.Right - ui.U(84), row.Y + ui.U(38), ui.U(78), ui.U(24)), "BUY 5", s.Horses > 0)) note = C.BuyHorses(s, 5) ?? "BOUGHT";
        ui.Text("MEN PROMOTED INTO CAVALRY NEED A HORSE EACH,", c.X, row.Bottom + ui.U(10), Style.Dim, 1);
        ui.Text("OR PAY FOR ONE WHEN PROMOTED.", c.X, row.Bottom + ui.U(22), Style.Dim, 1);
        bool kennel = s.Owner == C.Player.Id && Campaign.Level(s, "kennel") > 0;
        if (kennel)
        {
            // Your kennel: war hounds join your company as a pack (docs/archive/CREATURES.md phase 4).
            var k = new Rectangle(c.X, row.Bottom + ui.U(40), c.Width, ui.U(70));
            TownRow(k);
            PortraitWell(Troops.Get("war_hounds"), new Rectangle(k.X + ui.U(4), k.Y + ui.U(4), k.Height - ui.U(8), k.Height - ui.U(8)), 2);
            ui.Text("WAR HOUNDS", k.X + k.Height + ui.U(6), k.Y + ui.U(10), Style.Text, 2);
            ui.Text($"{s.Hounds} READY AT YOUR KENNEL", k.X + k.Height + ui.U(6), k.Y + ui.U(30), Style.Dim, 1);
            ui.Icon("coin", k.X + k.Height + ui.U(6), k.Y + ui.U(44), Kit.Gilt, 1);
            ui.Text($"{C.HoundPrice} A HOUND", k.X + k.Height + ui.U(18), k.Y + ui.U(44), Kit.Gilt, 1);
            if (ui.Button(new Rectangle(k.Right - ui.U(84), k.Y + ui.U(8), ui.U(78), ui.U(24)), "TAKE 3", s.Hounds > 0)) note = C.RaiseHounds(s, 3) ?? "THE HOUNDS JOIN YOU";
            if (ui.Button(new Rectangle(k.Right - ui.U(84), k.Y + ui.U(38), ui.U(78), ui.U(24)), "TAKE ALL", s.Hounds > 0)) note = C.RaiseHounds(s, s.Hounds) ?? "THE HOUNDS JOIN YOU";
        }
        var g = Well(new Rectangle(body.X + half + ui.U(12), y, half, body.Height), "SELL", Kit.Gilt);
        GoodsList(s, g);
    }

    /// The goods from the field you carry: a line a kind, its price here, and a button to sell it; then SELL ALL.
    void GoodsList(Settlement s, Rectangle c)
    {
        var want = Campaign.GoodsOf(C.WantedGoods(s));
        ui.Text("GOODS FROM THE FIELD", c.X, c.Y, Kit.Gilt, 1);
        ui.TextR($"THIS TOWN PAYS MOST FOR {want.Name.ToUpperInvariant()}", c.Right, c.Y, Style.Dim, 1);
        int y = c.Y + ui.U(14), rh = ui.U(22);
        if (C.GoodsCount == 0) { ui.Text("NONE. WIN A BATTLE AND BRING ITS GOODS HERE.", c.X, y + ui.U(4), Style.Dim, 1); return; }
        int total = 0;
        foreach (var k in Campaign.GoodsKinds)
        {
            int n = C.S.Goods.GetValueOrDefault(k.Id), price = C.GoodsPrice(k, s);
            if (n <= 0) continue;
            total += n * price;
            if (y + rh * 2 > c.Bottom) continue;
            var row = new Rectangle(c.X, y, c.Width, rh - ui.U(2));
            TownRow(row);
            ui.Text($"{n} {k.Name.ToUpperInvariant()}", row.X + ui.U(6), row.Y + ui.U(6), k == want ? Kit.Gilt : Style.Text, 1);
            ui.TextR($"{price} A LOT", row.Right - ui.U(66), row.Y + ui.U(6), Kit.Gilt, 1);
            if (ui.Button(new Rectangle(row.Right - ui.U(60), row.Y + ui.U(2), ui.U(56), row.Height - ui.U(4)), "SELL")) note = C.SellGoods(s, k.Id) ?? "SOLD";
            y += rh;
        }
        if (ui.Button(new Rectangle(c.X, y + ui.U(2), c.Width, ui.U(20)), $"SELL ALL FOR {total} CROWNS")) note = C.SellGoods(s) ?? "SOLD";
    }

    /// Your accepted contracts, under the overview map.
    void Contracts()
    {
        if (C.S.Contracts.Count == 0) return;
        // One quest only (docs/PLAYTEST.md PT3): the pinned one, else the one due soonest; the rest are on the quests screen.
        var show = C.S.Contracts.FirstOrDefault(c => c.Id == C.S.PinnedQuest) ?? C.S.Contracts.OrderBy(c => c.Deadline).First();
        int more = C.S.Contracts.Count - 1;
        int w = ui.U(230), x = ui.W - w - ui.U(10), y = ui.U(194 + 9 * 23 + 8); // under the minimap and the menu rail (Hints)
        var lines = ui.Wrap(show.Text.ToUpperInvariant(), w - ui.U(16)).Take(2).ToList();
        var r = new Rectangle(x, y, w, ui.U(35) + lines.Count * ui.U(11) + (more > 0 ? ui.U(12) : 0));
        ui.Panel(r);
        ui.Text(show.Id == C.S.PinnedQuest ? "PINNED QUEST" : "QUEST", x + ui.U(8), y + ui.U(6), Style.Accent, 1);
        ui.TextR(ctl.First(Act.QuestsScreen), r.Right - ui.U(8), y + ui.U(6), Style.Dim, 1);
        if (ui.Clicked(r)) OpenScreen(PanelKind.Quests);
        if (more > 0) ui.Text($"+{more} MORE ON THE QUESTS SCREEN", x + ui.U(8), r.Bottom - ui.U(13), Kit.Gilt, 1);
        y += ui.U(20);
        foreach (var c in new[] { show })
        {
            foreach (var t in lines) { ui.Text(t, x + ui.U(8), y, Style.Text, 1); y += ui.U(11); }
            y -= ui.U(11);
            float days = (c.Deadline - (float)C.S.Hours) / 24;
            // Reward and time as on the quests screen: a coin, a clock.
            int qx = x + ui.U(8);
            if (c.Reward > 0) { ui.Icon("coin", qx, y + ui.U(11), Kit.Gilt, 1); ui.Text($"{c.Reward:0}", qx + ui.U(11), y + ui.U(11), Kit.Gilt, 1); qx += ui.U(18) + ui.TW($"{c.Reward:0}", 1); }
            ui.Icon("clock", qx, y + ui.U(11), days < 2 ? Style.Warn : Style.Dim, 1);
            ui.Text((days < 2 ? $"{days:0.0} DAYS" : $"{days:0} DAYS") + (c.Kind switch { "defend" => $"  {c.Progress:0}/24 H", "militia" => $"  {c.Progress:0}/{Campaign.MilitiaHours} H", _ => c.Then != null ? "  MORE TO DO" : "" }), qx + ui.U(11), y + ui.U(11), days < 2 ? Style.Warn : Style.Text, 1);
            y += ui.U(26);
        }
    }

    /// Your fief (F6): prosperity and unrest as dials with where they are heading and why, taxes as three settings with
    /// what each does, and the buildings as tiles with their levels.
    void FiefTab(Settlement s, Rectangle body)
    {
        int third = (body.Width - ui.U(20)) / 3, ch = ui.U(100);
        float income = C.Income(s) + C.S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village && v.Owner == C.Player.Id).Sum(C.Income);
        // Prosperity.
        var a = Card(new Rectangle(body.X, body.Y, third, ch), Style.Ok);
        float pd = C.ProsperityDrift(s);
        ui.Ring(new Vector2(a.X + ui.U(28), a.Y + ui.U(30)), ui.U(26), s.Prosperity / 100f, Style.Ok, $"{s.Prosperity:0}");
        ui.Text("PROSPERITY", a.X + ui.U(64), a.Y, Style.Text, 2);
        ui.Text($"{pd:+0.00;-0.00} A DAY", a.X + ui.U(64), a.Y + ui.U(20), pd >= 0 ? Style.Ok : Style.Warn, 1);
        Price(a.X + ui.U(64), a.Y + ui.U(36), income, "A WEEK");
        ui.TextFit("WITH ITS VILLAGES", a.X + ui.U(64), a.Y + ui.U(48), a.Width - ui.U(64), Style.Dim, 1);
        // Unrest.
        bool revolt = s.Unrest >= Campaign.RevoltAt;
        var (drift, why) = C.UnrestDrift(s);
        var b = Card(new Rectangle(body.X + third + ui.U(10), body.Y, third, ch), revolt ? Style.Bad : Style.Warn);
        ui.Ring(new Vector2(b.X + ui.U(28), b.Y + ui.U(30)), ui.U(26), s.Unrest / 100f, revolt ? Style.Bad : Style.Warn, $"{s.Unrest:0}");
        ui.Text(revolt ? "REVOLT" : "UNREST", b.X + ui.U(64), b.Y, revolt ? Style.Bad : Style.Text, 2);
        ui.Text($"{drift:+0.00;-0.00} A DAY", b.X + ui.U(64), b.Y + ui.U(20), drift > 0 ? Style.Warn : Style.Ok, 1);
        int wy = b.Y + ui.U(36);
        foreach (var line in ui.Wrap(why.Count == 0 ? "THE PEOPLE ARE CONTENT." : string.Join("  ", why), b.Width - ui.U(64)).Take(3)) { ui.Text(line, b.X + ui.U(64), wy, Style.Dim, 1); wy += ui.U(11); }
        if (C.Rebels(s) is Party reb) ui.TextFit($"{reb.Men} REBELS IN THE FIELD: BEAT THEM", b.X, b.Bottom - ui.U(8), b.Width, Style.Bad, 1);
        // Taxes.
        var t = Card(new Rectangle(body.X + (third + ui.U(10)) * 2, body.Y, body.Width - (third + ui.U(10)) * 2, ch), Kit.Gilt);
        ui.Text("TAXES", t.X, t.Y, Kit.Gilt, 1);
        int bw = (t.Width - ui.U(8)) / 3, show = s.Tax;
        for (int i = 0; i < 3; i++)
        {
            var tb = new Rectangle(t.X + i * (bw + ui.U(4)), t.Y + ui.U(12), bw, ui.U(24));
            if (ui.Hover(tb)) show = i;
            if (ui.Button(tb, Campaign.TaxNames[i], true, "", s.Tax == i)) note = C.SetTax(s, i) ?? "";
        }
        int ty = t.Y + ui.U(44);
        if (show == 0) { ty = Outcome(t.X, ty, t.Width, "coin", "INCOME -30%", -1); Outcome(t.X, ty, t.Width, "heart", "CALMER, GROWS FASTER", 1); }
        else if (show == 2) { ty = Outcome(t.X, ty, t.Width, "coin", "INCOME +35%", 1); Outcome(t.X, ty, t.Width, "fire", "UNREST, SLOWER GROWTH", -1); }
        else Outcome(t.X, ty, t.Width, "coin", "INCOME AND TEMPER AS THEY STAND");
        // Buildings.
        var doing = s.Building != "" ? Campaign.BuildingOf(s.Building) : null;
        float left = MathF.Max(0, (s.BuildUntil - (float)C.S.Hours) / 24);
        int y = Heading(body.X, body.Y + ch + ui.U(12), body.Width, doing != null ? $"BUILDING {doing.Name.ToUpperInvariant()}: {left:0.0} DAYS LEFT" : "BUILD", "ONE AT A TIME");
        if (doing != null) { ui.Gauge(new Rectangle(body.X, y - ui.U(3), body.Width, ui.U(5)), Math.Clamp(1 - left / Math.Max(1, doing.Days), 0, 1), Kit.Gilt); y += ui.U(8); }
        int cols = 3, tw2 = (body.Width - ui.U(8) * (cols - 1)) / cols, n = 0;
        foreach (var d in Campaign.BuildingDefs)
        {
            var row = new Rectangle(body.X + n % cols * (tw2 + ui.U(8)), y + n / cols * ui.U(36), tw2, ui.U(31));
            n++;
            if (row.Bottom > body.Bottom) break;
            int lv = Campaign.Level(s, d.Id);
            bool can = C.CanBuild(s, d, out string why2, out int cost);
            // While something is building, the heading says so once; each tile shows its cost, not the same reason.
            string side = lv >= d.Max ? "BUILT" : can || why2.StartsWith("BUILDING ") ? $"{cost}" : why2;
            if (ui.Button(row, d.Name.ToUpperInvariant(), can, side, lv > 0)) note = C.Build(s, d.Id) ?? $"WORK BEGINS ON THE {d.Name.ToUpperInvariant()}";
            for (int i = 0; i < d.Max; i++) ui.Rect(new Rectangle(row.X + ui.U(6) + i * ui.U(8), row.Bottom - ui.U(6), ui.U(6), ui.U(3)), i < lv ? Kit.Gilt : Color.White * 0.16f);
            if (ui.Hover(row)) note = d.Text;
        }
    }
}
