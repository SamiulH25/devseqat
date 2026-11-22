using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: company storage and captain kit (key I). The captain large on the left in what he wears, a sprite-backed slot
/// round him for each piece; on the right the company baggage train (filterable, sortable, scrollable grid of gear pieces and
/// goods stacks, plus the Keep Vault when at your own town or castle) and a load/stores bar below.
partial class MapScreen
{
    /// Picked entry in the storage grid: gear item instance (preferred), free gear def id, or goods kind id.
    GearItem? invItem;
    string invPick = "";
    string invGoods = "";
    bool invFromVault;
    /// Storage filter tab (0 ALL, 1 WEAPONS, 2 ARMOUR, 3 HORSES, 4 GOODS, 5 VAULT) and sort mode (0 SLOT, 1 MAKE, 2 VALUE).
    int invTab, invSort, invScroll;
    /// Which fitting mark the smith offers to strike next (cycled with NEXT).
    int smithMark;

    static readonly string[] InvTabs = { "ALL", "WEAPONS", "ARMOUR", "HORSES", "GOODS" };
    static readonly string[] InvTabsVault = { "ALL", "WEAPONS", "ARMOUR", "HORSES", "GOODS", "VAULT" };
    static readonly string[] InvSortNames = { "SLOT", "MAKE", "VALUE" };

    void InventoryPanel()
    {
        var r = Modal(1080, 636, "COMPANY STORAGE AND KIT");
        int gap = ui.U(10), lw = ui.U(470), top = r.Y + ui.U(30), h = r.Height - ui.U(94);
        var vault = C.VaultAt();
        if (vault == null && invTab == 5) { invTab = 0; invFromVault = false; }
        KitDoll(Well(new Rectangle(r.X + ui.U(20), top, lw, h), "CAPTAIN'S KIT", Kit.Gilt));
        var right = new Rectangle(r.X + ui.U(20) + lw + gap, top, r.Width - lw - gap - ui.U(40), h);
        int footH = ui.U(96);
        string title = invTab == 5 && vault != null ? $"KEEP VAULT . {vault.Name.ToUpperInvariant()}" : "COMPANY BAGGAGE TRAIN";
        BaggageGrid(Well(new Rectangle(right.X, right.Y, right.Width, right.Height - footH - gap), title, Kit.Gilt), vault);
        BaggageTrainFooter(Well(new Rectangle(right.X, right.Bottom - footH, right.Width, footH), "BAGGAGE TRAIN AND STORES", Kit.Gilt), vault);
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.InventoryScreen))) panel = CameFrom(PanelKind.Inventory);
    }

    /// The colour of a make: dim worn, the text's plain, green fine, blue masterwork, gold named.
    static Color MakeColour(ItemQuality q) => q switch
    {
        ItemQuality.Worn => Style.Dim, ItemQuality.Fine => new Color(120, 205, 120), ItemQuality.Masterwork => new Color(110, 170, 240),
        ItemQuality.Named => Kit.Gilt, _ => Style.Text,
    };

    /// The captain in his kit under a night sky, three sprite-backed slots each side.
    void KitDoll(Rectangle c)
    {
        var cap = C.Captain;
        int sw = ui.U(132), sh = ui.U(48);
        var stage = new Rectangle(c.X + sw + ui.U(6), c.Y, c.Width - 2 * (sw + ui.U(6)), ui.U(184));
        ui.VGrad(stage, Kit.Mul(Style.Sides[2].Cloth, 0.6f), new Color(18, 16, 14), 2);
        ui.Stars(new Rectangle(stage.X, stage.Y, stage.Width, stage.Height / 3), (float)C.S.Hours, 24);
        ui.Hills(stage, stage.Bottom - ui.U(28), ui.U(10), 1.5f, new Color(30, 24, 20));
        ui.Portrait(Rules.CaptainType(cap), new Rectangle(stage.X, stage.Y + ui.U(10), stage.Width, stage.Height - ui.U(14)), 3 * ui.S);
        ui.Frame(stage, Color.Black * 0.6f, ui.U(1));
        string[] left = { "weapon", "side", "shield" }, rightSlots = { "helm", "body", "horse" };
        int sy = c.Y + ui.U(8), step = sh + ui.U(12);
        for (int i = 0; i < 3; i++)
        {
            KitSlot(left[i], new Rectangle(c.X, sy + i * step, sw, sh));
            KitSlot(rightSlots[i], new Rectangle(c.Right - sw, sy + i * step, sw, sh));
        }
        var t = Rules.CaptainType(cap);
        string line = $"DMG {t.Damage:0} . ARMOUR {t.Armor:0} . BLOCK {t.BlockChance * 100:0}% . SPEED {t.Speed:0.0}";
        ui.TextFitC(line, c.Center.X, stage.Bottom + ui.U(7), c.Width, Style.Text, 1);
        PieceDetail(new Rectangle(c.X, stage.Bottom + ui.U(24), c.Width, c.Bottom - stage.Bottom - ui.U(24)));
    }

    /// The picked piece or goods stack in full under the doll: large sprite, make, stats, marks, story, and smith actions.
    void PieceDetail(Rectangle r)
    {
        ui.Rect(r, Color.Black * 0.25f);
        ui.Frame(r, Kit.Mul(Kit.Gilt, 0.3f), ui.U(1));
        int x = r.X + ui.U(8), y = r.Y + ui.U(8), w = r.Width - ui.U(16);

        // Goods stack detail
        if (invGoods != "")
        {
            var gk = Campaign.GoodsOf(invGoods);
            var vault = C.VaultAt();
            int n = invFromVault && vault != null ? vault.VaultGoods.GetValueOrDefault(invGoods) : C.S.Goods.GetValueOrDefault(invGoods);
            var sbox = new Rectangle(x, y, ui.U(44), ui.U(44));
            ui.Rect(sbox, Color.Black * 0.45f);
            ui.Frame(sbox, Kit.Mul(Kit.Gilt, 0.6f), ui.U(1));
            ui.GoodsSprite(gk.Id, Kit.Inset(sbox, ui.U(4)));
            int tx = sbox.Right + ui.U(8), tw = r.Right - tx - ui.U(8);
            ui.TextFit($"{gk.Name.ToUpperInvariant()} (X{n})", tx, y + ui.U(2), tw, Kit.Gilt, 2);
            ui.TextFit($"TRADE GOODS . BASE VALUE {gk.Value} CROWNS / LOT", tx, y + ui.U(22), tw, Style.Text, 1);
            ui.TextFit($"LOAD: {(n + 9) / 10} (10 LOTS PER LOAD POINT)", tx, y + ui.U(34), tw, Style.Dim, 1);
            y = sbox.Bottom + ui.U(10);
            var town = C.SettlementAt(C.Player.Pos);
            if (town is { Kind: SettlementKind.Town } && !C.Refuses(town))
            {
                int price = C.GoodsPrice(gk, town);
                bool wanted = C.WantedGoods(town) == gk.Id;
                ui.TextFit($"{town.Name.ToUpperInvariant()} PAYS {price} CROWNS / LOT" + (wanted ? " (HIGH DEMAND)" : ""), x, y, w, wanted ? Style.Ok : Style.Text, 1);
                y += ui.U(14);
            }
            foreach (var l in ui.Wrap("LOOTED FROM BEATEN FOES. SELL AT ANY TOWN MARKET, OR SPEND ARMS AND ARMOUR AT A SMITH TO IMPROVE GEAR.", w))
            { if (y + ui.U(12) > r.Bottom - ui.U(8)) break; ui.Text(l, x, y, Style.Dim, 1); y += ui.U(12); }
            return;
        }

        // Gear piece detail
        var item = invItem;
        if (item != null && !C.Captain.Baggage.Contains(item) && !(invFromVault && C.VaultAt() is Settlement vs && vs.Vault.Contains(item)))
        { invItem = null; item = null; }
        string defId = item?.Def ?? invPick;
        if (Gear.Find(defId) is not GearDef g)
        {
            ui.Text("PICK A PIECE OR GOODS STACK TO INSPECT IT HERE.", x, y, Style.Dim, 1);
            return;
        }
        item ??= Campaign.ItemOf(C.Captain, g.Id);
        var q = item?.Quality ?? ItemQuality.Plain;
        var col = MakeColour(q);

        var ibox = new Rectangle(x, y, ui.U(46), ui.U(46));
        ui.VGrad(ibox, new Color(34, 30, 28), new Color(16, 14, 15), 2);
        ui.Frame(ibox, item is { Battered: true } ? Style.Bad : Kit.Mul(col, 0.8f), ui.U(1));
        ui.GearSprite(g, Kit.Inset(ibox, ui.U(4)));

        int hx = ibox.Right + ui.U(8), hw = r.Right - hx - ui.U(8);
        ui.TextFit(Campaign.ItemName(item, g).ToUpperInvariant(), hx, y, hw, col, 2);
        string make = Campaign.QualityName(q);
        ui.TextFit((make == "" ? "PLAIN" : make.ToUpperInvariant()) + $" . {g.Slot.ToUpperInvariant()}" + (Campaign.QualityScale(q) != 1 ? $" . {(Campaign.QualityScale(q) - 1) * 100:+0;-0}% MAKE" : ""), hx, y + ui.U(19), hw, col, 1);
        ui.TextFit(GearStats(g), hx, y + ui.U(32), hw, Style.Text, 1);

        y = ibox.Bottom + ui.U(6);
        int cx = x;
        foreach (var (text, good) in Compare(g))
        {
            if (cx + ui.TW(text, 1) > r.Right - ui.U(8)) break;
            ui.Text(text, cx, y, good ? Style.Ok : Style.Bad, 1);
            cx += ui.TW(text, 1) + ui.U(8);
        }
        if (cx > x) y += ui.U(13);

        if (item != null)
            foreach (var m in item.Marks.Where(Effects.Has).Select(Effects.Get))
                foreach (var l in ui.Wrap($"{m.Name.ToUpperInvariant()}: {m.Line.ToUpperInvariant()}", w))
                { if (y + ui.U(11) > r.Bottom) return; ui.Text(l, x, y, Kit.Gilt, 1); y += ui.U(11); }

        if (item is { Battered: true })
        { ui.TextFit("BATTERED: FIGHTS ONE MAKE LOWER UNTIL A SMITH MENDS IT.", x, y, w, Style.Bad, 1); y += ui.U(13); }

        // Weapon naming right from the inventory when earned
        if (item != null && !invFromVault && Campaign.MayName(item))
        {
            string offer = Campaign.BladeNames[bladeName % Campaign.BladeNames.Length];
            int nw = ui.U(56);
            ui.TextFit($"EARNED A NAME: {offer.ToUpperInvariant()}?", x, y + ui.U(4), w - nw * 2 - ui.U(10), Kit.Gilt, 1);
            if (ui.Button(new Rectangle(r.Right - ui.U(8) - nw * 2 - ui.U(4), y, nw, ui.U(18)), "NAME"))
                note = C.NameItem(item, offer) ?? $"IT IS {offer.ToUpperInvariant()} NOW";
            if (ui.Button(new Rectangle(r.Right - ui.U(8) - nw, y, nw, ui.U(18)), "OTHER")) bladeName++;
            y += ui.U(22);
        }

        string story = item == null ? g.Text : item.From + (item.Battles > 0 ? $". {item.Battles} battles" : "") + (item.Kills > 0 ? $", {item.Kills} kills" : "") + ".";
        if (item is { From: "" }) story = g.Text;
        var smith = !invFromVault ? C.Smithy() : null;
        int bottom = smith != null ? r.Bottom - ui.U(64) : r.Bottom - ui.U(6);
        foreach (var l in ui.Wrap(story.ToUpperInvariant(), w))
        { if (y + ui.U(11) > bottom) break; ui.Text(l, x, y, Style.Dim, 1); y += ui.U(11); }
        if (smith != null) SmithRow(g, item, new Rectangle(r.X + ui.U(8), Math.Min(y + ui.U(6), r.Bottom - ui.U(60)), r.Width - ui.U(16), ui.U(54)), smith);
    }

    /// The smith of the town you stand in (C11): mend, raise the make, strike a mark.
    void SmithRow(GearDef g, GearItem? existing, Rectangle r, Settlement s)
    {
        ui.Rect(new Rectangle(r.X, r.Y, r.Width, 1), Kit.Mul(Kit.Gilt, 0.35f));
        ui.Text($"THE SMITH OF {s.Name.ToUpperInvariant()}" + (C.GreatSmith(s) ? " . WORKS MASTERWORK" : ""), r.X, r.Y + ui.U(4), Kit.Gilt, 1);
        var i = existing ?? C.SmithItem(g.Id);
        int bw = (r.Width - ui.U(12)) / 3, by = r.Y + ui.U(30), bh = ui.U(20);
        var b1 = new Rectangle(r.X, by, bw, bh); var b2 = new Rectangle(b1.Right + ui.U(6), by, bw, bh); var b3 = new Rectangle(b2.Right + ui.U(6), by, bw, bh);
        string? mend = C.CannotMend(i), up = C.CannotImprove(i);
        if (i.Battered && ui.Button(b1, $"MEND {Campaign.MendCost(g)}", mend == null)) note = C.Mend(i) ?? "MENDED";
        else if (!i.Battered) ui.Button(b1, "WHOLE", false);
        var (crowns, goods, lots) = i.Quality < ItemQuality.Masterwork ? Campaign.ImproveCost(g, i.Quality) : (0, "", 0);
        string upLabel = i.Quality >= ItemQuality.Masterwork || g.Slot == "horse" ? "IMPROVE" : $"MAKE {Campaign.QualityName(i.Quality + 1).ToUpperInvariant()}";
        string costs = up != null ? $"BETTER MAKE: {up}" : $"BETTER MAKE: {crowns} CROWNS" + (lots > 0 ? $" AND {lots} LOTS OF {goods.ToUpperInvariant()}" : "");
        if (i.Battered) costs = mend != null ? $"MEND: {mend}" : $"MEND: {Campaign.MendCost(g)} CROWNS. " + costs;
        ui.TextFit(costs, r.X, r.Y + ui.U(16), r.Width, up != null && (!i.Battered || mend != null) ? Style.Warn : Style.Text, 1);
        if (ui.Button(b2, ui.Fit(upLabel, bw - ui.U(8)), up == null))
        {
            note = C.Improve(i) ?? $"IT IS {Campaign.QualityName(i.Quality).ToUpperInvariant()} NOW";
            if (C.Captain.Baggage.Contains(i)) invItem = i;
        }
        if (ui.Hover(b2)) ui.Tooltip(b2.Center.X, b2.Y - ui.U(2), "RAISE ITS MAKE", new[] { up ?? (lots > 0 ? $"{crowns} CROWNS AND {lots} LOTS OF {goods.ToUpperInvariant()}." : $"{crowns} CROWNS.") });
        var marks = Campaign.MarksFor(g.Slot).ToList();
        if (marks.Count > 0)
        {
            var m = marks[smithMark % marks.Count];
            string? why = C.CannotRemark(i, m.Id);
            int nw = ui.U(38);
            if (ui.Button(new Rectangle(b3.X, b3.Y, b3.Width - nw - ui.U(4), bh), ui.Fit(m.Name.ToUpperInvariant(), b3.Width - nw - ui.U(12)), why == null)) note = C.Remark(i, m.Id) ?? $"MARKED {m.Name.ToUpperInvariant()}";
            if (ui.Button(new Rectangle(b3.Right - nw, b3.Y, nw, bh), "NEXT")) smithMark++;
            if (ui.Hover(b3)) ui.Tooltip(b3.Center.X, b3.Y - ui.U(2), m.Name.ToUpperInvariant(), ui.Wrap(m.Line.ToUpperInvariant() + " " + (why ?? $"{Campaign.RemarkCost(g)} CROWNS."), ui.U(200)).ToArray());
        }
        if (mend != null && i.Battered && ui.Hover(b1)) ui.Tooltip(b1.Center.X, b1.Y - ui.U(2), "MEND", new[] { mend });
    }

    static readonly Dictionary<string, string> SlotNames = new()
    {
        ["weapon"] = "IN HAND", ["side"] = "SECOND", ["shield"] = "SHIELD", ["body"] = "ARMOUR", ["helm"] = "HELM", ["horse"] = "HORSE",
    };

    /// One slot by the doll: its baked sprite, name, make and marks; click to inspect (or take off if already picked).
    void KitSlot(string slot, Rectangle r)
    {
        string id = Campaign.InSlot(C.Captain.Kit, slot);
        var g = id == "" ? null : Gear.Find(id);
        var item = g == null ? null : Campaign.ItemInSlot(C.Captain, slot);
        bool picked = g != null && !invFromVault && invGoods == "" && (item != null ? invItem == item : invPick == g.Id);
        bool hover = ui.Hover(r);
        ui.Rect(r, picked ? Kit.Mul(Kit.Gilt, 0.24f) : Color.Black * (hover ? 0.55f : 0.4f));
        var qcol = MakeColour(item?.Quality ?? ItemQuality.Plain);
        ui.Frame(r, g == null ? Style.Dim * 0.5f : picked ? Kit.Gilt : item is { Battered: true } ? Style.Bad : Kit.Mul(qcol, 0.7f), ui.U(picked ? 2 : 1));

        int sSize = r.Height - ui.U(8);
        var sbox = new Rectangle(r.X + ui.U(4), r.Y + ui.U(4), sSize, sSize);
        ui.Rect(sbox, Color.Black * 0.45f);
        ui.Frame(sbox, Color.White * 0.12f, ui.U(1));
        if (g != null) ui.GearSprite(g, Kit.Inset(sbox, ui.U(2)));

        int tx = sbox.Right + ui.U(5), tw = r.Right - tx - ui.U(4);
        ui.TextFit(SlotNames[slot], tx, r.Y + ui.U(4), tw, Style.Dim, 1);
        if (g == null) { ui.Text("NONE", tx, r.Y + ui.U(18), Style.Dim, 1); return; }
        ui.TextFit(Campaign.ItemName(item, g).ToUpperInvariant(), tx, r.Y + ui.U(16), tw, qcol, 1);
        string sub = item is { Marks.Count: > 0 } ? string.Join(" . ", item.Marks.Where(Effects.Has).Select(m => Effects.Get(m).Name.ToUpperInvariant())) : "";
        if (item is { Kills: > 0 }) sub = (sub == "" ? "" : sub + " . ") + $"{item.Kills}K";
        if (item is { Battered: true }) ui.TextFit("BATTERED", tx, r.Y + ui.U(29), tw, Style.Bad, 1);
        else if (sub != "") ui.TextFit(sub, tx, r.Y + ui.U(29), tw, Kit.Gilt, 1);
        if (hover)
        {
            var lines = new List<string> { GearStats(g) };
            if (item is { From.Length: > 0 }) lines.AddRange(ui.Wrap(item.From.ToUpperInvariant() + ".", ui.U(200)));
            lines.Add(slot == "weapon" ? "CLICK TO INSPECT. WEAR ANOTHER TO SWAP." : "CLICK TO INSPECT; CLICK AGAIN TO TAKE OFF.");
            ui.Tooltip(r.Center.X, r.Y - ui.U(2), Campaign.ItemName(item, g).ToUpperInvariant(), lines.ToArray());
        }
        if (ui.Clicked(r))
        {
            if (picked && slot != "weapon") note = C.TakeOff(slot) ?? $"YOU TAKE OFF THE {Campaign.ItemName(item, g).ToUpperInvariant()}";
            else { invItem = item; invPick = g.Id; invGoods = ""; invFromVault = false; }
        }
    }

    sealed record StorageEntry(GearDef? Gear, GearItem? Item, string GoodsKind, int Count, bool FromVault);

    List<StorageEntry> BuildStorageEntries(Settlement? vault)
    {
        var list = new List<StorageEntry>();
        bool showVault = invTab == 5 && vault != null;
        if (showVault)
        {
            foreach (var i in vault!.Vault)
                if (Gear.Find(i.Def) is GearDef g) list.Add(new(g, i, "", 1, true));
            foreach (var gk in Campaign.GoodsKinds)
                if (vault.VaultGoods.GetValueOrDefault(gk.Id) is int n and > 0) list.Add(new(null, null, gk.Id, n, true));
            return list;
        }

        bool wantGear = invTab != 4;
        bool wantGoods = invTab is 0 or 4;
        if (wantGear)
        {
            bool Fits(GearDef g) => invTab switch
            {
                1 => g.Slot == "weapon",
                2 => g.Slot is "body" or "helm" or "shield",
                3 => g.Slot == "horse",
                _ => true,
            };
            // Every owned GearItem instance in baggage
            var inBagDefs = new HashSet<string>();
            foreach (var i in C.Captain.Baggage)
            {
                if (Gear.Find(i.Def) is not GearDef g || g.Look == "none") continue;
                inBagDefs.Add(g.Id);
                if (Fits(g)) list.Add(new(g, i, "", 1, false));
            }
            // Plus free starting pieces not represented as an item instance (so you can always put them back on)
            foreach (var g in Gear.All.Where(g => g.Free && g.Look != "none" && !inBagDefs.Contains(g.Id)))
                if (Fits(g)) list.Add(new(g, null, "", 1, false));
        }
        if (wantGoods)
        {
            foreach (var gk in Campaign.GoodsKinds)
                if (C.S.Goods.GetValueOrDefault(gk.Id) is int n and > 0)
                    list.Add(new(null, null, gk.Id, n, false));
        }

        // Sort gear entries cleanly; goods follow gear in ALL view
        return invSort switch
        {
            1 => list.OrderBy(e => e.Gear == null ? 1 : 0)
                     .ThenByDescending(e => (int)(e.Item?.Quality ?? ItemQuality.Plain))
                     .ThenByDescending(e => e.Gear?.Cost ?? 0).ToList(),
            2 => list.OrderBy(e => e.Gear == null ? 1 : 0)
                     .ThenByDescending(e => e.Gear != null ? Campaign.PriceOf(e.Gear, e.Item?.Quality ?? ItemQuality.Plain, e.Item?.Marks.Count ?? 0) : Campaign.GoodsOf(e.GoodsKind).Value * e.Count).ToList(),
            _ => list.OrderBy(e => e.Gear == null ? 1 : 0)
                     .ThenBy(e => e.Gear != null ? Array.IndexOf(Gear.Slots, e.Gear.Slot) : 99)
                     .ThenByDescending(e => e.Item != null && Campaign.IsWorn(C.Captain, e.Item) ? 1 : 0)
                     .ThenByDescending(e => e.Gear?.Cost ?? 0).ToList(),
        };
    }

    /// Filterable, sortable, scrollable grid of gear items and goods stacks, with action bar below.
    void BaggageGrid(Rectangle c, Settlement? vault)
    {
        var tabs = vault != null ? InvTabsVault : InvTabs;
        if (ui.WordTabs(c.X, c.Y, tabs, invTab) is int nt and >= 0) { invTab = nt; invScroll = 0; }
        int sortW = ui.U(92);
        if (ui.Button(new Rectangle(c.Right - sortW, c.Y - ui.U(1), sortW, ui.U(18)), $"SORT: {InvSortNames[invSort]}"))
        { invSort = (invSort + 1) % InvSortNames.Length; invScroll = 0; }

        var entries = BuildStorageEntries(vault);
        int top = c.Y + ui.U(24), bar = ui.U(28), gap = ui.U(6);
        int cols = 4, cw = (c.Width - (cols - 1) * gap) / cols, ch = ui.U(74);
        int gridH = c.Bottom - bar - top - ui.U(4);
        int visRows = Math.Max(1, (gridH + gap) / (ch + gap));
        int totalRows = (entries.Count + cols - 1) / cols;
        int maxScroll = Math.Max(0, totalRows - visRows);

        var gridArea = new Rectangle(c.X, top, c.Width, gridH);
        int wheel = ms.ScrollWheelValue - pms.ScrollWheelValue;
        if (wheel != 0 && ui.Hover(gridArea)) invScroll = Math.Clamp(invScroll - Math.Sign(wheel), 0, maxScroll);
        invScroll = Math.Clamp(invScroll, 0, maxScroll);

        if (entries.Count == 0)
        {
            string empty = invTab == 5 ? "THE KEEP VAULT IS EMPTY. STORE SPARE GEAR OR GOODS HERE." : invTab == 4 ? "NO GOODS CARRIED. A WON FIELD FILLS THE CARTS." : "NO ITEMS IN THIS CATEGORY.";
            ui.Text(empty, c.X + ui.U(4), top + ui.U(10), Style.Dim, 1);
        }

        int start = invScroll * cols, shown = visRows * cols;
        for (int i = 0; i < shown && start + i < entries.Count; i++)
        {
            var e = entries[start + i];
            var cell = new Rectangle(c.X + i % cols * (cw + gap), top + i / cols * (ch + gap), cw, ch);
            StorageCell(e, cell);
        }

        // Scroll indicator / buttons when more rows exist
        int by = c.Bottom - bar + ui.U(4);
        int scrollRight = c.Right;
        if (maxScroll > 0)
        {
            int sbw = ui.U(22);
            if (ui.Button(new Rectangle(c.Right - sbw, by, sbw, ui.U(20)), "V", invScroll < maxScroll)) invScroll++;
            if (ui.Button(new Rectangle(c.Right - sbw * 2 - ui.U(3), by, sbw, ui.U(20)), "^", invScroll > 0)) invScroll--;
            string pg = $"{invScroll + 1}/{maxScroll + 1}";
            ui.TextR(pg, c.Right - sbw * 2 - ui.U(8), by + ui.U(6), Style.Dim, 1);
            scrollRight = c.Right - sbw * 2 - ui.U(12) - ui.TW(pg, 1);
        }

        // Action bar for the selected item or goods stack
        StorageActions(new Rectangle(c.X, by, Math.Max(ui.U(100), scrollRight - c.X), ui.U(20)), vault);
    }

    void StorageCell(StorageEntry e, Rectangle r)
    {
        bool hover = ui.Hover(r);
        if (e.GoodsKind != "")
        {
            var gk = Campaign.GoodsOf(e.GoodsKind);
            bool picked = invGoods == e.GoodsKind && invFromVault == e.FromVault;
            ui.Rect(r, picked ? Kit.Mul(Kit.Gilt, 0.28f) : Color.Black * (hover ? 0.52f : 0.36f));
            ui.Frame(r, picked ? Kit.Gilt : Color.White * (hover ? 0.35f : 0.14f), ui.U(picked ? 2 : 1));
            var sbox = new Rectangle(r.X + (r.Width - ui.U(38)) / 2, r.Y + ui.U(5), ui.U(38), ui.U(38));
            ui.GoodsSprite(gk.Id, sbox);
            // Quantity pill in top-right
            string qty = $"X{e.Count}";
            int qw = ui.TW(qty, 1) + ui.U(6);
            ui.Rect(new Rectangle(r.Right - qw - ui.U(3), r.Y + ui.U(3), qw, ui.U(12)), Color.Black * 0.72f);
            ui.Text(qty, r.Right - qw, r.Y + ui.U(5), Kit.Gilt, 1);
            ui.TextFitC(gk.Name.ToUpperInvariant(), r.Center.X, r.Bottom - ui.U(22), r.Width - ui.U(6), Style.Text, 1);
            ui.TextFitC($"{gk.Value}C . LOAD {(e.Count + 9) / 10}", r.Center.X, r.Bottom - ui.U(11), r.Width - ui.U(6), Style.Dim, 1);
            if (hover)
                ui.Tooltip(r.Center.X, r.Y - ui.U(2), $"{gk.Name.ToUpperInvariant()} (X{e.Count})", new[] { $"BASE VALUE {gk.Value} CROWNS PER LOT.", "SELL IN A TOWN MARKET OR STORE IN YOUR KEEP VAULT." });
            if (ui.Clicked(r))
            {
                if (picked) { invGoods = ""; invFromVault = false; }
                else { invGoods = e.GoodsKind; invItem = null; invPick = ""; invFromVault = e.FromVault; }
            }
            return;
        }

        var g = e.Gear!;
        var item = e.Item;
        var q = item?.Quality ?? ItemQuality.Plain;
        var qcol = MakeColour(q);
        string wornSlot = !e.FromVault ? (item != null ? Campaign.WornSlotOf(C.Captain, item) : (C.Wears(g.Id) && Campaign.ItemOf(C.Captain, g.Id) == null ? (C.Captain.Kit.Weapon == g.Id ? "weapon" : C.Captain.Kit.Side == g.Id ? "side" : g.Slot) : "")) : "";
        bool worn = wornSlot != "";
        bool pickedGear = invGoods == "" && invFromVault == e.FromVault && (item != null ? invItem == item : invItem == null && invPick == g.Id);

        ui.Rect(r, pickedGear ? Kit.Mul(Kit.Gilt, 0.28f) : Color.Black * (hover ? 0.52f : 0.36f));
        var edge = pickedGear ? Kit.Gilt : item is { Battered: true } ? Kit.Mul(Style.Bad, 0.85f) : q != ItemQuality.Plain ? Kit.Mul(qcol, 0.65f) : Color.White * (hover ? 0.35f : 0.14f);
        ui.Frame(r, edge, ui.U(pickedGear ? 2 : 1));

        var gbox = new Rectangle(r.X + ui.U(8), r.Y + ui.U(4), r.Width - ui.U(16), ui.U(40));
        ui.GearSprite(g, gbox);

        // Top-left status badge (HAND / 2ND / WORN) or top-right battered/marks pip
        if (worn)
        {
            string tag = wornSlot == "weapon" ? "HAND" : wornSlot == "side" ? "2ND" : "WORN";
            int tw = ui.TW(tag, 1) + ui.U(6);
            ui.Rect(new Rectangle(r.X + ui.U(3), r.Y + ui.U(3), tw, ui.U(11)), Color.Black * 0.75f);
            ui.Text(tag, r.X + ui.U(6), r.Y + ui.U(4), Kit.Gilt, 1);
        }
        if (item is { Battered: true })
        {
            ui.Rect(new Rectangle(r.Right - ui.U(14), r.Y + ui.U(3), ui.U(11), ui.U(11)), Style.Bad * 0.85f);
            ui.Text("!", r.Right - ui.U(10), r.Y + ui.U(4), Color.White, 1);
        }
        else if (item is { Marks.Count: > 0 })
        {
            string mk = new string('*', item.Marks.Count);
            ui.TextR(mk, r.Right - ui.U(4), r.Y + ui.U(4), Kit.Gilt, 1);
        }

        // Tile label: custom name when named, else the piece's own name (make is already shown by colour and frame).
        string tileName = item is { Name.Length: > 0 } ? item.Name : g.Name;
        ui.TextFitC(tileName.ToUpperInvariant(), r.Center.X, r.Bottom - ui.U(22), r.Width - ui.U(6), qcol, 1);
        string sub = item is { Battered: true } ? "BATTERED"
            : C.CannotUse(g) is string why ? why
            : item is { Marks.Count: > 0 } ? string.Join(" ", item.Marks.Where(Effects.Has).Select(m => Effects.Get(m).Name.ToUpperInvariant()))
            : g.Slot.ToUpperInvariant();
        var subCol = item is { Battered: true } ? Style.Bad : C.CannotUse(g) != null ? Style.Warn : item is { Marks.Count: > 0 } ? Kit.Gilt : Style.Dim;
        ui.TextFitC(sub, r.Center.X, r.Bottom - ui.U(11), r.Width - ui.U(6), subCol, 1);

        if (hover)
        {
            var lines = new List<string> { GearStats(g) };
            var cmp = Compare(g);
            if (cmp.Count > 0) lines.Add(string.Join("  ", cmp.Select(x => x.Text)) + " AGAINST WORN");
            if (item != null)
                foreach (var m in item.Marks.Where(Effects.Has).Select(Effects.Get)) lines.AddRange(ui.Wrap($"{m.Name.ToUpperInvariant()}: {m.Line.ToUpperInvariant()}", ui.U(220)));
            if (item is { From.Length: > 0 }) lines.AddRange(ui.Wrap(item.From.ToUpperInvariant() + (item.Battles > 0 ? $". {item.Battles} BATTLES." : "."), ui.U(220)));
            ui.Tooltip(r.Center.X, r.Y - ui.U(2), Campaign.ItemName(item, g).ToUpperInvariant(), lines.ToArray());
        }
        if (ui.Clicked(r))
        {
            if (pickedGear) { invItem = null; invPick = ""; invFromVault = false; }
            else { invItem = item; invPick = g.Id; invGoods = ""; invFromVault = e.FromVault; }
        }
    }

    void StorageActions(Rectangle r, Settlement? vault)
    {
        int bx = r.X, by = r.Y, bh = r.Height, bw = ui.U(66);
        // Goods actions
        if (invGoods != "")
        {
            if (invFromVault && vault != null)
            {
                if (ui.Button(new Rectangle(bx, by, ui.U(74), bh), "TAKE 5")) { note = C.TakeStoredGoods(vault, invGoods, 5) ?? "TAKEN"; if (vault.VaultGoods.GetValueOrDefault(invGoods) <= 0) invGoods = ""; }
                bx += ui.U(78);
                if (ui.Button(new Rectangle(bx, by, ui.U(84), bh), "TAKE ALL")) { note = C.TakeStoredGoods(vault, invGoods, int.MaxValue) ?? "TAKEN ALL"; invGoods = ""; }
                return;
            }
            if (vault != null)
            {
                if (ui.Button(new Rectangle(bx, by, ui.U(74), bh), "STORE 5")) { note = C.StoreGoods(vault, invGoods, 5) ?? "STORED"; if (C.S.Goods.GetValueOrDefault(invGoods) <= 0) invGoods = ""; }
                bx += ui.U(78);
                if (ui.Button(new Rectangle(bx, by, ui.U(86), bh), "STORE ALL")) { note = C.StoreGoods(vault, invGoods, int.MaxValue) ?? "STORED IN VAULT"; invGoods = ""; }
                bx += ui.U(90);
            }
            var town = C.SettlementAt(C.Player.Pos);
            if (town is { Kind: SettlementKind.Town } && !C.Refuses(town))
            {
                if (ui.Button(new Rectangle(bx, by, ui.U(66), bh), "SELL")) { note = C.SellGoods(town, invGoods) ?? "SOLD"; invGoods = ""; }
                bx += ui.U(70);
            }
            if (ui.Button(new Rectangle(bx, by, ui.U(66), bh), "DROP 5")) { note = C.DropGoods(invGoods, 5) ?? "DROPPED"; if (C.S.Goods.GetValueOrDefault(invGoods) <= 0) invGoods = ""; }
            return;
        }

        // Gear actions
        string defId = invItem?.Def ?? invPick;
        if (Gear.Find(defId) is not GearDef pick)
        {
            ui.TextFit("CLICK A PIECE OR GOODS STACK TO EQUIP, STORE, SELL OR DROP.", bx, by + ui.U(6), r.Width, Style.Dim, 1);
            return;
        }
        if (invFromVault && vault != null && invItem != null)
        {
            if (ui.Button(new Rectangle(bx, by, ui.U(90), bh), "TAKE TO BAG"))
            {
                note = C.TakeStoredItem(vault, invItem) ?? "TAKEN TO BAGGAGE";
                invFromVault = false;
            }
            return;
        }

        string? cant = C.CannotUse(pick);
        var item = invItem;
        string wornSlot = item != null ? Campaign.WornSlotOf(C.Captain, item) : (C.Wears(pick.Id) && Campaign.ItemOf(C.Captain, pick.Id) == null ? (C.Captain.Kit.Weapon == pick.Id ? "weapon" : C.Captain.Kit.Side == pick.Id ? "side" : pick.Slot) : "");
        bool worn = wornSlot != "";

        if (ui.Button(new Rectangle(bx, by, bw, bh), "WEAR", cant == null && wornSlot != "weapon" && !(pick.Slot != "weapon" && worn)))
            note = (item != null ? C.EquipItem(item) : C.Equip(pick.Id)) ?? "";
        bx += bw + ui.U(4);
        if (pick.Slot == "weapon")
        {
            if (ui.Button(new Rectangle(bx, by, bw, bh), "SECOND", cant == null && wornSlot != "side"))
                note = (item != null ? C.EquipItem(item, side: true) : C.Equip(pick.Id, side: true)) ?? "";
            bx += bw + ui.U(4);
        }
        if (worn && wornSlot != "weapon")
        {
            if (ui.Button(new Rectangle(bx, by, ui.U(72), bh), "TAKE OFF"))
                note = C.TakeOff(wornSlot) ?? "TAKEN OFF";
            bx += ui.U(76);
        }
        if (vault != null && item != null && !worn)
        {
            if (ui.Button(new Rectangle(bx, by, ui.U(64), bh), "STORE"))
            {
                note = C.StoreItem(vault, item) ?? "STORED IN VAULT";
                invItem = null; invPick = "";
                return;
            }
            bx += ui.U(68);
        }
        if (C.Smithy() != null && item != null && !worn)
        {
            int sells = Campaign.SellPrice(pick, item);
            var sb = new Rectangle(bx, by, ui.U(74), bh);
            if (ui.Button(sb, $"SELL {sells}"))
            {
                note = C.SellGearItem(item) ?? $"SOLD FOR {sells} CROWNS";
                invItem = null; invPick = "";
                return;
            }
            bx += ui.U(78);
        }
        if (item != null && !worn && item.Name == "" && bx + bw <= r.Right)
        {
            if (ui.Button(new Rectangle(bx, by, bw, bh), "DROP"))
            {
                note = C.DropGearItem(item) ?? "LEFT BEHIND";
                invItem = null; invPick = "";
                return;
            }
            bx += bw + ui.U(6);
        }
        if (cant != null && bx < r.Right - ui.U(30)) ui.TextFit(cant, bx, by + ui.U(6), r.Right - bx, Style.Warn, 1);
    }

    /// Segmented load bar + sprite-backed company stores summary (food, pack horses, wagons, keep vault).
    void BaggageTrainFooter(Rectangle c, Settlement? vault)
    {
        var (gearLoad, goodsLoad, total, room, drag) = C.BaggageLoadBreakdown();
        bool over = total > room;
        string head = over
            ? $"LOAD {total} / {room} (GEAR {gearLoad} . GOODS {goodsLoad}) . MARCH {(1 - drag) * 100:0}% SLOWER"
            : $"LOAD {total} / {room} (GEAR {gearLoad} . GOODS {goodsLoad})";
        ui.TextFit(head, c.X, c.Y + ui.U(1), c.Width, over ? Style.Bad : Style.Text, 1);

        // Segmented load gauge: gear in gilt, goods in warm bronze, overflow in red.
        var bar = new Rectangle(c.X, c.Y + ui.U(14), c.Width, ui.U(8));
        ui.Rect(bar, Color.Black * 0.55f);
        float cap = Math.Max(1, room);
        int gw = (int)(bar.Width * MathF.Min(1f, gearLoad / cap));
        int mw = (int)(bar.Width * MathF.Min(1f - gw / (float)bar.Width, goodsLoad / cap));
        if (gw > 0) ui.Rect(new Rectangle(bar.X, bar.Y, gw, bar.Height), over ? Style.Bad : Kit.Gilt);
        if (mw > 0) ui.Rect(new Rectangle(bar.X + gw, bar.Y, mw, bar.Height), over ? Kit.Mul(Style.Bad, 0.75f) : new Color(188, 132, 74));
        ui.Frame(bar, Color.White * 0.16f, ui.U(1));

        // Sprite-backed store chips below the bar
        int cy = c.Y + ui.U(28), ch = ui.U(34), gap = ui.U(6);
        int chips = vault != null ? 4 : 3;
        int cw = (c.Width - (chips - 1) * gap) / chips;
        void Chip(int idx, string sprite, string title, string sub, Color col)
        {
            var cr = new Rectangle(c.X + idx * (cw + gap), cy, cw, ch);
            ui.Rect(cr, Color.Black * 0.32f);
            ui.Frame(cr, Color.White * 0.12f, ui.U(1));
            ui.StoreSprite(sprite, new Rectangle(cr.X + ui.U(4), cr.Y + ui.U(4), ui.U(26), ui.U(26)));
            int tx = cr.X + ui.U(34), tw = cr.Right - tx - ui.U(4);
            ui.TextFit(title, tx, cr.Y + ui.U(5), tw, col, 1);
            ui.TextFit(sub, tx, cr.Y + ui.U(18), tw, Style.Dim, 1);
        }

        int horseBonus = (int)(Math.Min(6, C.Player.Horses) * Balance.Get().BaggagePerHorse);
        int wagonBonus = (int)(C.Player.Wagons * Balance.Get().BaggagePerWagon);
        Chip(0, "food", $"FOOD {C.Player.Food:0}D", "PROVISIONS", C.Player.Food < 3 ? Style.Bad : Style.Text);
        Chip(1, "horse", $"HORSES {C.Player.Horses}", $"+{horseBonus} ROOM (TO 6)", Style.Text);
        Chip(2, "wagon", $"WAGONS {C.Player.Wagons}/{Balance.Get().MaxWagons}", $"+{wagonBonus} ROOM", C.Player.Wagons > 0 ? Kit.Gilt : Style.Text);
        if (vault != null)
            Chip(3, "vault", $"VAULT {Campaign.VaultCount(vault)}", vault.Name.ToUpperInvariant(), Kit.Gilt);
    }
}
