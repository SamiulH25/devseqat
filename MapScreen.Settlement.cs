using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the settlement panel and its tabs (train, recruit, market, food, rest, contracts, fief).
partial class MapScreen
{
    void SettlementPanel()
    {
        var s = at!;
        var r = Modal(900, 540, s.Name.ToUpperInvariant(), SideColor(s.Faction));
        bool fief = s.Owner == C.Player.Id && s.Kind != SettlementKind.Village;
        TownHeader(s, r);
        var tabs = (s.Kind switch
        {
            SettlementKind.Village when C.Refuses(s) => new[] { "FORAGE" },
            SettlementKind.Village => new[] { "RECRUIT", "FOOD", "FORAGE", "REST", "ELDERS" },
            SettlementKind.Castle => new[] { "HALL", "REST", "FOOD", "CAPTIVES", "CONTRACT" },
            _ => new[] { "TRAIN", "RECRUIT", "MERCENARIES", "TAVERN", "HALL", "MARKET", "FOOD", "REST", "CAPTIVES", "CONTRACT" },
        }).Concat(C.ChapterAt(s) != null ? new[] { "CHAPTER" } : Array.Empty<string>()).Concat(fief ? new[] { "FIEF", "GARRISON" } : Array.Empty<string>()).ToArray();
        int tw = Math.Min(ui.U(104), (r.Width - ui.U(40)) / tabs.Length);
        int pick = ui.Tabs(new Rectangle(r.X + ui.U(20), r.Y + ui.U(80), tw * tabs.Length, ui.U(22)), tabs, Array.IndexOf(tabs, tab));
        if (pick >= 0) { tab = tabs[pick]; note = ""; }
        var well = new Rectangle(r.X + ui.U(20), r.Y + ui.U(102), r.Width - ui.U(40), r.Height - ui.U(170));
        ui.Rect(well, Color.Black * 0.22f);
        ui.Frame(well, Color.White * 0.06f, ui.U(1));
        var body = Kit.Inset(well, ui.U(12));
        switch (tab)
        {
            case "RECRUIT": RecruitTab(s, body); break;
            case "MERCENARIES": MercenariesTab(s, body); break;
            case "TAVERN": TavernTab(s, body); break;
            case "HALL": HallTab(s, body); break;
            case "MARKET": MarketTab(s, body); break;
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
        }
        Note(r);
        // The purse, and the way out.
        int by = r.Bottom - ui.U(40);
        ui.Icon("coin", r.X + ui.U(24), by + ui.U(7), Kit.Gilt, 2);
        ui.Text($"{C.S.Gold:N0}", r.X + ui.U(44), by + ui.U(7), Kit.Gilt, 2);
        ui.Text($"CROWNS   {C.Player.Men} MEN   FOOD {C.Player.Food:0} DAYS   {C.Player.Horses} HORSES", r.X + ui.U(48) + ui.TW($"{C.S.Gold:N0}", 2), by + ui.U(11), Style.Dim, 1);
        if (ui.Gilded(new Rectangle(r.Right - ui.U(200), by, ui.U(176), ui.U(28)), "LEAVE", ctl.Name(Act.Menu))) panel = PanelKind.None;
    }

    /// The town's arms, name, standing and, where it has them, its prosperity and unrest.
    void TownHeader(Settlement s, Rectangle r)
    {
        int x = r.X + ui.U(22), y = r.Y + ui.U(20);
        ui.Shield(new Rectangle(x, y, ui.U(40), ui.U(48)), Style.Sides[Style.PaletteOf(s.Faction)]);
        ui.Text(s.Name.ToUpperInvariant(), x + ui.U(52), y + ui.U(2), Style.Text, 3);
        int cx = x + ui.U(52), cy = y + ui.U(30);
        cx += ui.Chip(cx, cy, s.Kind.ToString().ToUpperInvariant(), Style.Dim, s.Kind == SettlementKind.Village ? "bread" : "tower") + ui.U(4);
        cx += ui.Chip(cx, cy, C.FactionNames[s.Faction].ToUpperInvariant(), SideColor(s.Faction), "flag") + ui.U(4);
        if (s.Owner == C.Player.Id) cx += ui.Chip(cx, cy, "YOUR FIEF", Kit.Gilt, "crown") + ui.U(4);
        if (C.Refuses(s)) cx += ui.Chip(cx, cy, "HOSTILE", Style.Bad) + ui.U(4);
        if (C.HolderOf(s) is Character holder)
        {
            float o = C.OpinionOfYou(holder);
            ui.Chip(cx, cy, $"{holder.Name.ToUpperInvariant()}: {OpinionWord(o)}", OpinionColor(o), "heart");
        }
        ui.Text(C.Flavour(s).ToUpperInvariant(), x + ui.U(52), y + ui.U(50), Style.Dim, 1);
        float rr = ui.U(16);
        bool revolt = s.Unrest >= Campaign.RevoltAt;
        ui.Ring(new Vector2(r.Right - ui.U(130), y + ui.U(20)), rr, s.Prosperity / 100f, Style.Ok, $"{s.Prosperity:0}");
        ui.TextC(s.Prosperity >= 70 ? "PROSPEROUS" : s.Prosperity >= 40 ? "PROSPERITY" : "POOR", r.Right - ui.U(130), y + ui.U(42), Style.Dim, 1);
        ui.Ring(new Vector2(r.Right - ui.U(50), y + ui.U(20)), rr, s.Unrest / 100f, revolt ? Style.Bad : Style.Warn, $"{s.Unrest:0}");
        ui.TextC(revolt ? "REVOLT" : "UNREST", r.Right - ui.U(50), y + ui.U(42), revolt ? Style.Bad : Style.Dim, 1);
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

    /// Recruit tab: local levies who will take your coin.
    void RecruitTab(Settlement s, Rectangle body)
    {
        var levy = Troops.Get(C.RecruitTroopFor(s));
        int willing = C.RecruitsForYou(s), per = (int)Balance.Get().ArmyScale;
        var row = new Rectangle(body.X, body.Y, body.Width, ui.U(104));
        TownRow(row);
        PortraitWell(levy, new Rectangle(row.X + ui.U(6), row.Y + ui.U(6), ui.U(92), ui.U(92)), Style.PaletteOf(s.Faction));
        int tx = row.X + ui.U(110), ty = row.Y + ui.U(10);
        ui.Text(levy.Name.ToUpperInvariant(), tx, ty, Style.Text, 2);
        ui.Text($"TIER {Style.Numerals[levy.Tier]} . {s.Culture.ToUpperInvariant()} MEN . {willing} OF {s.Recruits} WILLING", tx, ty + ui.U(20), Style.Dim, 1);
        ui.Icon("coin", tx, ty + ui.U(34), Kit.Gilt, 1);
        ui.Text($"{C.LevyPrice(s)} CROWNS FOR {per} MEN", tx + ui.U(12), ty + ui.U(34), Kit.Gilt, 1);
        TroopStats(levy, tx + ui.U(160), ty + ui.U(34));
        ui.Pips(new Rectangle(tx, ty + ui.U(50), ui.U(220), ui.U(7)), s.Recruits == 0 ? 0 : willing / (float)s.Recruits, Style.Friendly, 12);
        ui.Text("THEY JOIN A COMPANY OF THEIR KIND WITH ROOM, OR FORM A NEW ONE.", tx, ty + ui.U(66), Style.Dim, 1);
        ui.Text($"THEY CAN TRAIN INTO {s.Culture.ToUpperInvariant()} AND COMMON LINES. NEW MEN BRING NO XP.", tx, ty + ui.U(78), Style.Dim, 1);
        int bx = row.Right - ui.U(150), by = row.Y + ui.U(8);
        foreach (var (label, n) in new[] { ("RECRUIT 1", 1), ("RECRUIT 5", 5), ("RECRUIT ALL", 99) })
        {
            if (ui.Button(new Rectangle(bx, by, ui.U(142), ui.U(26)), label, willing > 0, "", n == 99)) note = C.RecruitLevies(s, n) ?? $"RECRUITED. {s.Recruits} LEFT";
            by += ui.U(31);
        }
        if (s.ThreatUntil > C.S.Hours) ui.Text("THE VILLAGE WAS THREATENED: NO RECRUITS FOR NOW", body.X, row.Bottom + ui.U(12), Style.Foe, 1);
    }

    /// Tavern tab (S5): a wanderer looking for a captain (3.3), and what people are saying (rumours of real things nearby).
    void TavernTab(Settlement s, Rectangle body)
    {
        int y = body.Y, rh = ui.U(66);
        if (C.WandererAt(s) is Character w)
        {
            var row = new Rectangle(body.X, y, body.Width, rh - ui.U(6));
            TownRow(row);
            PortraitWell(UnitType.Captain, new Rectangle(row.X + ui.U(4), row.Y + ui.U(4), row.Height - ui.U(8), row.Height - ui.U(8)), 2);
            int tx = row.X + row.Height + ui.U(6);
            ui.Text($"{w.Name.ToUpperInvariant()}, A WANDERER", tx, row.Y + ui.U(8), Kit.Gilt, 2);
            ui.Text($"{w.Role.ToUpperInvariant()} ({RoleTextOf(w.Role)}). {string.Join(", ", w.Traits).ToUpperInvariant()}. LEADS A COMPANY AS A HERO.", tx, row.Y + ui.U(28), Style.Dim, 1);
            int wp = C.WandererPrice(w);
            if (ui.Button(new Rectangle(row.Right - ui.U(196), row.Center.Y - ui.U(14), ui.U(190), ui.U(28)), "TAKE INTO SERVICE", C.S.Gold >= wp, $"{wp} CROWNS"))
                note = C.HireWanderer(s) ?? "HE JOINS YOUR HOUSEHOLD";
            y += rh;
        }
        else
        {
            ui.Text("NO WANDERER IS DRINKING HERE TONIGHT. THEY MOVE BETWEEN TOWNS.", body.X, y + ui.U(4), Style.Dim, 1);
            y += ui.U(24);
        }
        // A feast (A5): three days in good heart, carried into battle.
        var feast = new Rectangle(body.X, y, body.Width, ui.U(34));
        TownRow(feast);
        string fl = C.HasBuff("feasted") ? $"FEASTED: {C.BuffDays("feasted"):0.0} DAYS LEFT" : "HOLD A FEAST: THREE DAYS STRONGER AND STEADIER, IN BATTLE TOO";
        ui.Text(ui.Fit(fl, feast.Width - ui.U(220)), feast.X + ui.U(8), feast.Y + ui.U(12), Kit.Parchment, 1);
        if (ui.Button(new Rectangle(feast.Right - ui.U(200), feast.Y + ui.U(5), ui.U(194), ui.U(24)), "FEAST THE COMPANY", C.S.Gold >= C.FeastPrice && !C.HasBuff("feasted"), $"{C.FeastPrice} CROWNS"))
            note = C.Feast(s) ?? "THE COMPANY FEASTS";
        y += ui.U(40);
        ui.Text("TALK IN THE TAVERN", body.X, y + ui.U(4), Style.Accent, 1);
        y += ui.U(22);
        foreach (var talk in C.Rumours(s))
        {
            var row = new Rectangle(body.X, y, body.Width, ui.U(34));
            TownRow(row);
            ui.Text("\"" + talk.Text.ToUpperInvariant() + "\"", row.X + ui.U(8), row.Y + ui.U(12), Kit.Parchment, 1);
            if (talk.At != s.Pos && ui.Button(new Rectangle(row.Right - ui.U(130), row.Y + ui.U(5), ui.U(124), ui.U(24)), "RIDE THERE"))
            { C.Follow(talk); panel = PanelKind.None; at = null; }
            y += ui.U(38);
        }
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
        GoodsList(s, new Rectangle(c.X, row.Bottom + ui.U(kennel ? 120 : 44), c.Width, c.Bottom - row.Bottom - ui.U(kennel ? 120 : 44)));

        ArmourerWell(s, Well(new Rectangle(body.X + half + ui.U(12), y, half, body.Height), "THE ARMOURER", Kit.Gilt));
    }

    /// The goods from the field you carry: a line a kind, its price here, and a button to sell it; then SELL ALL.
    void GoodsList(Settlement s, Rectangle c)
    {
        var want = Campaign.GoodsOf(C.WantedGoods(s));
        ui.Text("GOODS FROM THE FIELD", c.X, c.Y, Kit.Gilt, 1);
        ui.TextR($"WANTED HERE: {want.Name.ToUpperInvariant()}", c.Right, c.Y, Style.Dim, 1);
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

    /// Train tab: promote men with enough XP up their line.
    void TrainTab(Settlement s, Rectangle body)
    {
        ui.Text("MEN WITH ENOUGH XP CAN BE PROMOTED UP THEIR LINE HERE, ONE BY ONE OR ALL AT ONCE.", body.X, body.Y, Style.Dim, 1);
        int y = body.Y + ui.U(16), rh = ui.U(46);
        foreach (var (c, st) in C.Player.Companies.SelectMany(c => c.Stacks.Select(st => (c, st))).OrderByDescending(x => x.st.Ready).ToList())
        {
            if (y + rh - ui.U(4) > body.Bottom) break;
            var row = new Rectangle(body.X, y, body.Width, rh - ui.U(4));
            TownRow(row);
            PortraitWell(st.Type, new Rectangle(row.X + ui.U(3), row.Y + ui.U(3), row.Height - ui.U(6), row.Height - ui.U(6)), 2);
            int tx = row.X + row.Height + ui.U(6);
            ui.Text($"{c.Name.ToUpperInvariant()}: {st.Men} {st.Type.Name.ToUpperInvariant()} {Style.Numerals[st.Type.Tier]}", tx, row.Y + ui.U(6), Style.Text, 1);
            bool top = st.Type.UpgradesTo.Length == 0;
            if (!top) ui.Gauge(new Rectangle(tx, row.Y + ui.U(19), ui.U(160), ui.U(6)), st.PoolCap <= 0 ? 0 : st.Xp / st.PoolCap, st.Ready > 0 ? Kit.Gilt : Style.Friendly);
            ui.Text(top ? "AT THE TOP OF ITS LINE" : $"{st.Ready} READY   POOL {st.Xp:0} / {st.PoolCap:0} XP ({st.Type.UpgradeXp} A MAN)", tx + (top ? 0 : ui.U(170)), row.Y + ui.U(19), st.Ready > 0 ? Style.Accent : Style.Dim, 1);
            if (!top && ui.Button(new Rectangle(row.Right - ui.U(150), row.Center.Y - ui.U(12), ui.U(144), ui.U(24)), "PROMOTE...", st.Ready > 0, "", st.Ready > 0))
            { upgrading = c; upStack = st; panel = PanelKind.Upgrade; note = ""; }
            y += rh;
        }
    }

    /// Rest tab: wait here to heal the wounded.
    void RestTab(Settlement s, Rectangle body)
    {
        bool town = s.Kind == SettlementKind.Town;
        float frac = C.Player.Men + C.Player.Wounded == 0 ? 1 : C.Player.Men / (float)(C.Player.Men + C.Player.Wounded);
        ui.Ring(new Vector2(body.X + ui.U(50), body.Y + ui.U(48)), ui.U(34), frac, C.Player.Wounded > 0 ? Style.Warn : Style.Ok, $"{C.Player.Wounded}", "WOUNDED");
        int tx = body.X + ui.U(110);
        ui.Text(town ? "A TOWN'S BEDS AND BARBERS" : "A COLD BILLET", tx, body.Y + ui.U(14), Style.Text, 2);
        ui.Text($"YOUR WOUNDED HEAL {(town ? "QUICKLY IN A TOWN" : "SLOWLY HERE")}.", tx, body.Y + ui.U(36), Style.Dim, 1);
        ui.Text("RESTING IN A TOWN ALSO RESTORES COHESION (UP TO 20).", tx, body.Y + ui.U(48), Style.Dim, 1);
        int y = body.Y + ui.U(104);
        if (ui.Button(new Rectangle(body.X, y, ui.U(200), ui.U(28)), "REST 1 DAY")) { C.Rest(24); note = $"RESTED. WOUNDED: {C.Player.Wounded}"; }
        if (ui.Button(new Rectangle(body.X + ui.U(210), y, ui.U(250), ui.U(28)), "REST UNTIL HEALED", C.Player.Wounded > 0, "", C.Player.Wounded > 0))
        {
            for (int d = 0; d < 10 && C.Player.Wounded > 0 && C.Encounter == null; d++) C.Rest(24);
            note = $"RESTED. WOUNDED: {C.Player.Wounded}";
        }
    }

    /// Food tab: buy days of food, as rows of provisions.
    void FoodTab(Settlement s, Rectangle body)
    {
        ui.Ring(new Vector2(body.X + ui.U(50), body.Y + ui.U(48)), ui.U(34), Math.Clamp(C.Player.Food / 14f, 0, 1), C.Player.Food < 3 ? Style.Bad : Style.Ok, $"{C.Player.Food:0}", "DAYS");
        int tx = body.X + ui.U(110);
        ui.Text($"{C.Season.ToUpperInvariant()}{(C.Winter ? ": MEN EAT HALF AGAIN AS MUCH" : "")}", tx, body.Y + ui.U(14), C.Winter ? Style.Warn : Style.Text, 2);
        ui.Text("PRISONERS EAT TOO. A STARVING COMPANY LOSES", tx, body.Y + ui.U(36), Style.Dim, 1);
        ui.Text("COHESION AND MEN EVERY DAY.", tx, body.Y + ui.U(48), Style.Dim, 1);
        int y = body.Y + ui.U(96);
        foreach (var (days, name) in new[] { (3, "BREAD AND CHEESE"), (7, "GRAIN AND SALT PORK"), (14, "A WAGON OF STORES") })
        {
            var row = new Rectangle(body.X, y, body.Width, ui.U(40));
            TownRow(row);
            ui.Icon("bread", row.X + ui.U(10), row.Center.Y - ui.U(7), Style.Text, 2);
            ui.Text(name, row.X + ui.U(36), row.Y + ui.U(9), Style.Text, 1);
            ui.Text($"+{days} DAYS", row.X + ui.U(36), row.Y + ui.U(21), Style.Dim, 1);
            int price = C.FoodPrice(days);
            if (ui.Button(new Rectangle(row.Right - ui.U(160), row.Y + ui.U(7), ui.U(154), ui.U(26)), $"BUY {days} DAYS", C.S.Gold >= price, $"{price}")) note = C.BuyFood(days) ?? "BOUGHT";
            y += ui.U(46);
        }
    }

    /// Forage tab: take food from a village by force.
    void ForageTab(Settlement s, Rectangle body)
    {
        ui.Text(C.Refuses(s) ? "AN ENEMY VILLAGE. ITS PEOPLE BAR THEIR DOORS." : "YOUR MEN CAN TAKE WHAT THEY NEED BY FORCE.", body.X, body.Y, Style.Text);
        ui.Text("FORAGING GIVES A WEEK OF FOOD, AND LEAVES THE VILLAGE WITH NO RECRUITS FOR A WEEK.", body.X, body.Y + ui.U(20), Style.Dim, 1);
        ui.Text(C.Refuses(s) ? "RAIDING AN ENEMY'S VILLAGE COUNTS IN THE WAR." : "YOUR LIEGE'S PEOPLE WILL REMEMBER IT: -5 RENOWN.", body.X, body.Y + ui.U(34), C.Refuses(s) ? Style.Ok : Style.Warn, 1);
        if (ui.Button(new Rectangle(body.X, body.Y + ui.U(56), ui.U(240), ui.U(28)), "FORAGE", s.ThreatUntil <= C.S.Hours)) note = C.Forage(s) ?? "FORAGED";
    }

    /// Captives tab: sell prisoners, ransom or free captive lords.
    void CaptivesTab(Settlement s, Rectangle body)
    {
        int y = body.Y;
        if (C.PrisonerCount == 0) ui.Text("YOU HOLD NO PRISONERS. MEN CAPTURED IN BATTLE ARE KEPT UNTIL YOU SELL THEM HERE.", body.X, y, Style.Dim, 1);
        else
        {
            ui.Text($"{C.PrisonerCount} PRISONERS: " + string.Join(", ", C.Player.Prisoners.Select(g => $"{g.Count} {Troops.Get(g.Troop).Name.ToUpperInvariant()}")), body.X, y, Style.Text, 1);
            if (ui.Button(new Rectangle(body.X, y + ui.U(18), ui.U(340), ui.U(28)), "SELL THEM ALL", true, $"{C.PrisonerValue} CROWNS")) note = C.SellPrisoners(s) ?? "SOLD";
        }
        y += ui.U(64);
        ui.Text("LORDS YOU HOLD FOR RANSOM", body.X, y, Style.Accent, 1);
        y += ui.U(16);
        foreach (int id in C.Player.Captives.ToList())
        {
            if (C.PartyById(id) is not Party lord) continue;
            ui.Text($"{lord.Name.ToUpperInvariant()} OF THE {C.FactionNames[lord.Faction].ToUpperInvariant()}", body.X, y + ui.U(6), SideColor(lord.Faction), 1);
            if (ui.Button(new Rectangle(body.Right - ui.U(360), y, ui.U(200), ui.U(24)), "RANSOM", true, $"{C.LordRansom(lord)}")) note = C.RansomLord(lord, s) ?? "RANSOMED";
            if (ui.Button(new Rectangle(body.Right - ui.U(150), y, ui.U(150), ui.U(24)), "FREE (+RENOWN)")) C.FreeLord(lord);
            y += ui.U(30);
        }
        if (C.Player.Captives.Count == 0) ui.Text("NONE. A LORD BEATEN BY YOUR HAND MAY BE TAKEN.", body.X, y, Style.Dim, 1);
    }

    /// Garrison tab (your fief): station or withdraw companies.
    void GarrisonTab(Settlement s, Rectangle body)
    {
        ui.Text($"YOUR FIEF. IT PAYS YOU {C.Income(s) + C.S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village).Sum(C.Income):0} CROWNS A WEEK. SUPPLIES {s.Supplies:0} DAYS.", body.X, body.Y, Style.Text, 1);
        int y = body.Y + ui.U(18);
        if (s.Garrison.Count == 0) ui.Text("NO MEN ARE STATIONED HERE. STATION A COMPANY TO HOLD THE WALLS.", body.X, y + ui.U(6), Style.Warn, 1);
        foreach (var g in s.Garrison.ToList())
        {
            ui.Text($"{g.Name.ToUpperInvariant()}: {g.Men} {g.Type.Name.ToUpperInvariant()}", body.X, y + ui.U(6), Style.Text, 1);
            if (ui.Button(new Rectangle(body.Right - ui.U(150), y, ui.U(150), ui.U(22)), "TAKE WITH YOU")) note = C.Withdraw(g, s) ?? "";
            y += ui.U(26);
        }
        y += ui.U(8);
        ui.Text("LEAVE A COMPANY TO HOLD THE WALLS:", body.X, y, Style.Dim, 1);
        y += ui.U(16);
        foreach (var c in C.Player.Companies.ToList())
        {
            if (y > body.Bottom - ui.U(20)) break;
            if (ui.Button(new Rectangle(body.X, y, ui.U(420), ui.U(22)), $"STATION {c.Name.ToUpperInvariant()} ({c.Men})", C.Player.Companies.Count > 1)) note = C.Station(c, s) ?? "";
            y += ui.U(26);
        }
    }

    /// Contract tab: sign or end a contract, and the steward's board of jobs.
    void ContractTab(Settlement s, Rectangle body)
    {
        int cur = C.Captain.Contract;
        if (s.Kind == SettlementKind.Town && cur != s.Faction)
            ui.Text("THE STEWARD'S BOARD IS FOR THOSE WHO SERVE THE " + C.FactionNames[s.Faction].ToUpperInvariant() + ". SIGN WITH THEM TO SEE IT.", body.X, body.Y + ui.U(130), Style.Dim, 1);
        if (s.Kind == SettlementKind.Town && cur == s.Faction)
        {
            ui.Text("THE STEWARD'S BOARD", body.X, body.Y + ui.U(130), Style.Accent, 1);
            int y = body.Y + ui.U(146);
            foreach (var o in C.Offers(s).ToList())
            {
                ui.Text(o.Text.ToUpperInvariant(), body.X, y + ui.U(6), Style.Text, 1);
                if (ui.Button(new Rectangle(body.Right - ui.U(200), y, ui.U(200), ui.U(24)), "ACCEPT", C.S.Contracts.Count < 3, $"{o.Reward:0}")) note = C.Accept(s, o) ?? "ACCEPTED";
                y += ui.U(28);
                if (y > body.Bottom - ui.U(24)) break;
            }
        }
        ui.Text(cur >= 0 ? $"YOU SERVE THE {C.FactionNames[cur].ToUpperInvariant()}." : "YOU ARE A FREE CAPTAIN: NO PAY, NO ENEMIES BUT BRIGANDS.", body.X, body.Y, Style.Text);
        ui.Text("A CONTRACT PAYS 85% OF YOUR UPKEEP WEEKLY, AND BOUNTIES FOR BRIGANDS AND ENEMY LORDS.", body.X, body.Y + ui.U(20), Style.Dim, 1);
        ui.Text("YOUR EMPLOYER'S ENEMIES BECOME YOURS.", body.X, body.Y + ui.U(34), Style.Dim, 1);
        if (cur != s.Faction && ui.Button(new Rectangle(body.X, body.Y + ui.U(56), ui.U(380), ui.U(28)), $"SIGN WITH THE {C.FactionNames[s.Faction].ToUpperInvariant()}"))
            note = C.SignContract(s.Faction) ?? "";
        if (cur >= 0 && ui.Button(new Rectangle(body.X, body.Y + ui.U(92), ui.U(380), ui.U(28)), C.Captain.Fealty ? "RENOUNCE YOUR OATH" : "END YOUR CONTRACT")) note = C.SignContract(-1) ?? "";
    }

    /// Elders tab (villages, S3): the village's own small jobs, open to any captain.
    void EldersTab(Settlement s, Rectangle body)
    {
        ui.Text("THE ELDERS OF " + s.Name.ToUpperInvariant() + " ASK FOR HELP.", body.X, body.Y, Style.Text);
        ui.Text("THEIR JOBS NEED NO CONTRACT. THEY PAY LITTLE, BUT THE VILLAGE PROSPERS.", body.X, body.Y + ui.U(20), Style.Dim, 1);
        int y = body.Y + ui.U(48);
        var jobs = C.ElderOffers(s).ToList();
        if (jobs.Count == 0) ui.Text("NOTHING TODAY. COME BACK IN A FEW DAYS.", body.X, y + ui.U(6), Style.Dim, 1);
        foreach (var o in jobs)
        {
            ui.Text(o.Text.ToUpperInvariant(), body.X, y + ui.U(6), Style.Text, 1);
            float pay = o.Reward + (o.Then?.Reward ?? 0);
            if (ui.Button(new Rectangle(body.Right - ui.U(200), y, ui.U(200), ui.U(24)), "ACCEPT", C.S.Contracts.Count < 3, $"{pay:0}")) note = C.Accept(s, o) ?? "ACCEPTED";
            y += ui.U(28);
        }
    }

    /// Your accepted contracts, under the overview map.
    void Contracts()
    {
        if (C.S.Contracts.Count == 0) return;
        int w = ui.U(230), x = ui.W - w - ui.U(10), y = ui.U(194);
        var r = new Rectangle(x, y, w, ui.U(20) + C.S.Contracts.Count * ui.U(26));
        ui.Panel(r);
        ui.Text("CONTRACTS", x + ui.U(8), y + ui.U(6), Style.Accent, 1);
        y += ui.U(20);
        foreach (var c in C.S.Contracts)
        {
            string t = c.Text.ToUpperInvariant();
            if (t.Length > 36) t = t[..35] + ".";
            ui.Text(t, x + ui.U(8), y, Style.Text, 1);
            float days = (c.Deadline - (float)C.S.Hours) / 24;
            ui.Text($"{days:0.0} DAYS  {c.Reward:0} CROWNS" + (c.Kind switch { "defend" => $"  {c.Progress:0}/24 H", "militia" => $"  {c.Progress:0}/{Campaign.MilitiaHours} H", _ => c.Then != null ? "  MORE TO DO" : "" }), x + ui.U(8), y + ui.U(11), days < 2 ? Style.Warn : Style.Dim, 1);
            y += ui.U(26);
        }
    }

    /// Your fief (F6): its prosperity and unrest and why they move, its taxes, and its buildings.
    void FiefTab(Settlement s, Rectangle body)
    {
        int x = body.X, y = body.Y, half = body.Width / 2 - ui.U(10);
        ui.Text($"PROSPERITY {s.Prosperity:0}", x, y, Style.Text);
        ui.TextR($"{C.ProsperityDrift(s):+0.00;-0.00} A DAY", x + half, y + ui.U(2), Style.Dim, 1);
        ui.Bar(new Rectangle(x, y + ui.U(18), half, ui.U(5)), s.Prosperity / 100f, Style.Ok);
        ui.Text($"INCOME {C.Income(s) + C.S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village && v.Owner == C.Player.Id).Sum(C.Income):0} A WEEK WITH ITS VILLAGES", x, y + ui.U(28), Style.Dim, 1);
        int ux = x + half + ui.U(20);
        var (drift, why) = C.UnrestDrift(s);
        ui.Text($"UNREST {s.Unrest:0}", ux, y, s.Unrest >= Campaign.RevoltAt ? Style.Bad : Style.Text);
        ui.TextR($"{drift:+0.00;-0.00} A DAY", ux + half, y + ui.U(2), drift > 0 ? Style.Warn : Style.Dim, 1);
        ui.Bar(new Rectangle(ux, y + ui.U(18), half, ui.U(5)), s.Unrest / 100f, s.Unrest >= Campaign.RevoltAt ? Style.Bad : Style.Warn);
        ui.TextFit(why.Count == 0 ? "THE PEOPLE ARE CONTENT." : string.Join("  ", why), ux, y + ui.U(28), half, Style.Dim, 1);
        if (C.Rebels(s) is Party reb) ui.Text($"{reb.Men} REBELS ARE IN THE FIELD: BEAT THEM TO CALM THE PLACE.", ux, y + ui.U(40), Style.Bad, 1);
        y += ui.U(56);
        ui.Text("TAXES:", x, y + ui.U(6), Style.Dim, 1);
        for (int i = 0; i < 3; i++)
            if (ui.Button(new Rectangle(x + ui.U(60) + i * ui.U(110), y, ui.U(104), ui.U(24)), Campaign.TaxNames[i], true, "", s.Tax == i)) note = C.SetTax(s, i) ?? "";
        ui.Text("LOW: INCOME -30%, CALMER, GROWS FASTER.", x + ui.U(400), y, Style.Dim, 1);
        ui.Text("HIGH: INCOME +35%, UNREST, SLOWER GROWTH.", x + ui.U(400), y + ui.U(12), Style.Dim, 1);
        y += ui.U(34);
        string? busy = s.Building != "" ? Campaign.BuildingOf(s.Building)?.Name.ToUpperInvariant() : null;
        if (busy != null) ui.Text($"BUSY: BUILDING {busy}, {MathF.Max(0, (s.BuildUntil - (float)C.S.Hours) / 24):0.0} DAYS LEFT. ONE BUILDING AT A TIME.", x, y, Style.Accent, 1);
        else ui.Text("BUILD (ONE AT A TIME):", x, y, Style.Accent, 1);
        y += ui.U(16);
        int bw = (body.Width - ui.U(8)) / 2, i2 = 0;
        foreach (var d in Campaign.BuildingDefs)
        {
            var row = new Rectangle(x + (i2 % 2) * (bw + ui.U(8)), y + (i2 / 2) * ui.U(28), bw, ui.U(24));
            int lv = Campaign.Level(s, d.Id);
            bool can = C.CanBuild(s, d, out string why2, out int cost);
            string label = $"{d.Name.ToUpperInvariant()}{(d.Max > 1 ? $" {Style.Numerals[Math.Min(d.Max, lv + (lv < d.Max ? 1 : 0))]}" : "")}";
            // While something is building, the line above says so once; each button shows its cost, not the same reason.
            string side = can || why2.StartsWith("BUILDING ") ? $"{cost}" : why2;
            if (ui.Button(row, lv >= d.Max ? label + " (BUILT)" : label, can, side, lv > 0)) note = C.Build(s, d.Id) ?? $"WORK BEGINS ON THE {d.Name.ToUpperInvariant()}";
            if (ui.Hover(row)) note = d.Text;
            i2++;
        }
    }
}
