using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: kit and baggage (docs/COMPANY.md C10, key I). The captain large on the left in what he wears, a slot round him
/// for each piece (click one to take it off); on the right everything he can put on as a grid, the make in its colour and the
/// smith's marks, with what it changes against what you wear on hover; under it the goods and the baggage's load. Kit
/// changes anywhere outside a battle.
partial class MapScreen
{
    /// The piece picked in the grid (a gear id), for the WEAR / 2ND / DROP buttons.
    string invPick = "";

    void InventoryPanel()
    {
        var r = Modal(1060, 620, "KIT AND BAGGAGE");
        int gap = ui.U(10), lw = ui.U(470), top = r.Y + ui.U(30), h = r.Height - ui.U(96);
        KitDoll(Well(new Rectangle(r.X + ui.U(20), top, lw, h), "WHAT YOU WEAR", Kit.Gilt));
        var right = new Rectangle(r.X + ui.U(20) + lw + gap, top, r.Width - lw - gap - ui.U(40), h);
        int goodsH = ui.U(110);
        BaggageGrid(Well(new Rectangle(right.X, right.Y, right.Width, right.Height - goodsH - gap), "THE BAGGAGE", Kit.Gilt));
        GoodsAndLoad(Well(new Rectangle(right.X, right.Bottom - goodsH, right.Width, goodsH), "GOODS AND LOAD", Kit.Gilt));
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.InventoryScreen))) panel = CameFrom(PanelKind.Inventory);
    }

    /// The colour of a make: dim worn, the text's plain, green fine, blue masterwork, gold named.
    static Color MakeColour(ItemQuality q) => q switch
    {
        ItemQuality.Worn => Style.Dim, ItemQuality.Fine => new Color(120, 205, 120), ItemQuality.Masterwork => new Color(110, 170, 240),
        ItemQuality.Named => Kit.Gilt, _ => Style.Text,
    };

    static string SlotIcon(GearDef g) => g.Slot switch { "weapon" => g.Ranged ? "arrow" : g.Thrust ? "spear" : "sword", "helm" => "crown", "horse" => "horse", "body" => "chain", _ => "shield" };

    /// The captain in his kit under a night sky, three slots each side.
    void KitDoll(Rectangle c)
    {
        var cap = C.Captain;
        int sw = ui.U(120), sh = ui.U(46);
        var stage = new Rectangle(c.X + sw + ui.U(6), c.Y, c.Width - 2 * (sw + ui.U(6)), ui.U(190));
        ui.VGrad(stage, Kit.Mul(Style.Sides[2].Cloth, 0.6f), new Color(18, 16, 14), 2);
        ui.Stars(new Rectangle(stage.X, stage.Y, stage.Width, stage.Height / 3), (float)C.S.Hours, 24);
        ui.Hills(stage, stage.Bottom - ui.U(30), ui.U(10), 1.5f, new Color(30, 24, 20));
        ui.Portrait(Rules.CaptainType(cap), new Rectangle(stage.X, stage.Y + ui.U(10), stage.Width, stage.Height - ui.U(14)), 3 * ui.S);
        ui.Frame(stage, Color.Black * 0.6f, ui.U(1));
        string[] left = { "weapon", "side", "shield" }, rightSlots = { "helm", "body", "horse" };
        int sy = c.Y + ui.U(10), step = sh + ui.U(14);
        for (int i = 0; i < 3; i++)
        {
            KitSlot(left[i], new Rectangle(c.X, sy + i * step, sw, sh));
            KitSlot(rightSlots[i], new Rectangle(c.Right - sw, sy + i * step, sw, sh));
        }
        // Under him: what he brings to a fight, in one line.
        var t = Rules.CaptainType(cap);
        string line = $"DMG {t.Damage:0} . ARMOUR {t.Armor:0} . BLOCK {t.BlockChance * 100:0}% . SPEED {t.Speed:0.0}";
        ui.TextFitC(line, c.Center.X, stage.Bottom + ui.U(8), c.Width, Style.Text, 1);
        PieceDetail(new Rectangle(c.X, stage.Bottom + ui.U(26), c.Width, c.Bottom - stage.Bottom - ui.U(26)));
    }

    /// The picked piece in full under the doll: make, numbers against what you wear, its marks and its story.
    void PieceDetail(Rectangle r)
    {
        ui.Rect(r, Color.Black * 0.25f);
        ui.Frame(r, Kit.Mul(Kit.Gilt, 0.3f), ui.U(1));
        int x = r.X + ui.U(8), y = r.Y + ui.U(8), w = r.Width - ui.U(16);
        if (Gear.Find(invPick) is not GearDef g) { ui.Text("PICK A PIECE IN THE BAGGAGE TO SEE IT HERE.", x, y, Style.Dim, 1); return; }
        var item = Campaign.ItemOf(C.Captain, g.Id);
        var q = item?.Quality ?? ItemQuality.Plain;
        ui.Icon(SlotIcon(g), x, y, MakeColour(q), 2);
        ui.TextFit(Campaign.ItemName(item, g).ToUpperInvariant(), x + ui.U(22), y, w - ui.U(22), MakeColour(q), 2);
        y += ui.U(22);
        string make = Campaign.QualityName(q);
        ui.Text((make == "" ? "PLAIN" : make.ToUpperInvariant()) + $" . {g.Slot.ToUpperInvariant()}" + (Campaign.QualityScale(q) != 1 ? $" . {(Campaign.QualityScale(q) - 1) * 100:+0;-0}% FROM ITS MAKE" : ""), x, y, MakeColour(q), 1);
        y += ui.U(14);
        ui.TextFit(GearStats(g), x, y, w, Style.Text, 1);
        y += ui.U(14);
        int cx = x;
        foreach (var (text, good) in Compare(g)) { ui.Text(text, cx, y, good ? Style.Ok : Style.Bad, 1); cx += ui.TW(text, 1) + ui.U(8); }
        if (cx > x) y += ui.U(14);
        if (item != null)
            foreach (var m in item.Marks.Where(Effects.Has).Select(Effects.Get))
                foreach (var l in ui.Wrap($"{m.Name.ToUpperInvariant()}: {m.Line.ToUpperInvariant()}", w)) { if (y + ui.U(12) > r.Bottom) return; ui.Text(l, x, y, Kit.Gilt, 1); y += ui.U(12); }
        string story = item == null ? g.Text : item.From + (item.Battles > 0 ? $". {item.Battles} battles" : "") + (item.Kills > 0 ? $", {item.Kills} kills" : "") + ".";
        if (item is { From: "" }) story = g.Text;
        y += ui.U(4);
        foreach (var l in ui.Wrap(story.ToUpperInvariant(), w)) { if (y + ui.U(12) > r.Bottom) return; ui.Text(l, x, y, Style.Dim, 1); y += ui.U(12); }
    }

    static readonly Dictionary<string, string> SlotNames = new()
    {
        ["weapon"] = "IN HAND", ["side"] = "SECOND", ["shield"] = "SHIELD", ["body"] = "ARMOUR", ["helm"] = "HELM", ["horse"] = "HORSE",
    };

    /// One slot by the doll: what it holds, its make and marks; click to take it off.
    void KitSlot(string slot, Rectangle r)
    {
        string id = Campaign.InSlot(C.Captain.Kit, slot);
        var g = id == "" ? null : Gear.Find(id);
        var item = g == null ? null : Campaign.ItemOf(C.Captain, g.Id);
        bool hover = ui.Hover(r);
        ui.Rect(r, Color.Black * (hover ? 0.55f : 0.4f));
        ui.Frame(r, g == null ? Style.Dim * 0.5f : Kit.Mul(MakeColour(item?.Quality ?? ItemQuality.Plain), 0.7f), ui.U(1));
        ui.Text(SlotNames[slot], r.X + ui.U(5), r.Y + ui.U(4), Style.Dim, 1);
        if (g == null) { ui.Text("NONE", r.X + ui.U(5), r.Y + ui.U(18), Style.Dim, 1); return; }
        ui.Icon(SlotIcon(g), r.Right - ui.U(18), r.Y + ui.U(4), MakeColour(item?.Quality ?? ItemQuality.Plain), 1);
        ui.TextFit(Campaign.ItemName(item, g).ToUpperInvariant(), r.X + ui.U(5), r.Y + ui.U(17), r.Width - ui.U(10), MakeColour(item?.Quality ?? ItemQuality.Plain), 1);
        string sub = item is { Marks.Count: > 0 } ? string.Join(" . ", item.Marks.Where(Effects.Has).Select(m => Effects.Get(m).Name.ToUpperInvariant())) : "";
        if (item is { Kills: > 0 }) sub = (sub == "" ? "" : sub + " . ") + $"{item.Kills} KILLS";
        if (sub != "") ui.TextFit(sub, r.X + ui.U(5), r.Y + ui.U(30), r.Width - ui.U(10), Kit.Gilt, 1);
        if (hover)
        {
            var lines = new List<string> { GearStats(g) };
            if (item is { From.Length: > 0 }) lines.AddRange(ui.Wrap(item.From.ToUpperInvariant() + ".", ui.U(200)));
            lines.Add(slot == "weapon" ? "WEAR ANOTHER WEAPON TO CHANGE IT." : "CLICK TO TAKE IT OFF.");
            ui.Tooltip(r.Center.X, r.Y - ui.U(2), Campaign.ItemName(item, g).ToUpperInvariant(), lines.ToArray());
        }
        if (slot != "weapon" && ui.Clicked(r)) note = C.TakeOff(slot) ?? $"YOU TAKE OFF THE {Campaign.ItemName(item, g).ToUpperInvariant()}";
    }

    /// Everything you can put on, as cells; the picked one gets WEAR, 2ND and DROP.
    void BaggageGrid(Rectangle c)
    {
        var all = C.Wardrobe().Where(g => g.Look != "none").ToList(); // bare hands are no piece
        int cols = 3, cw = (c.Width - (cols - 1) * ui.U(6)) / cols, ch = ui.U(44);
        int bar = ui.U(26), rows = Math.Max(1, (c.Height - bar) / (ch + ui.U(6)));
        if (invPick != "" && !C.Owns(invPick)) invPick = "";
        for (int i = 0; i < all.Count && i < rows * cols; i++)
        {
            var g = all[i];
            var cell = new Rectangle(c.X + i % cols * (cw + ui.U(6)), c.Y + i / cols * (ch + ui.U(6)), cw, ch);
            BaggageCell(g, cell);
        }
        if (all.Count > rows * cols) ui.TextR($"+{all.Count - rows * cols} MORE", c.Right, c.Bottom - bar - ui.U(12), Style.Dim, 1);
        // The picked piece's buttons.
        int bw = ui.U(70), by = c.Bottom - bar + ui.U(4), bx = c.X;
        if (Gear.Find(invPick) is not GearDef pick) { ui.Text("CLICK A PIECE TO PUT IT ON, CARRY IT SECOND OR LEAVE IT BEHIND.", c.X, by + ui.U(6), Style.Dim, 1); return; }
        string? cant = C.CannotUse(pick);
        bool worn = C.Wears(pick.Id);
        if (ui.Button(new Rectangle(bx, by, bw, ui.U(20)), "WEAR", cant == null && C.Captain.Kit.Weapon != pick.Id && !(pick.Slot != "weapon" && worn))) note = C.Equip(pick.Id) ?? "";
        bx += bw + ui.U(4);
        if (pick.Slot == "weapon" && ui.Button(new Rectangle(bx, by, bw, ui.U(20)), "SECOND", cant == null && C.Captain.Kit.Side != pick.Id)) note = C.Equip(pick.Id, side: true) ?? "";
        if (pick.Slot == "weapon") bx += bw + ui.U(4);
        var item = Campaign.ItemOf(C.Captain, pick.Id);
        if (ui.Button(new Rectangle(bx, by, bw, ui.U(20)), "DROP", item != null && !worn && item.Name == "")) { note = C.DropItem(pick.Id) ?? "LEFT BEHIND"; invPick = ""; }
        bx += bw + ui.U(8);
        if (cant != null) ui.Text(cant, bx, by + ui.U(6), Style.Warn, 1);
    }

    void BaggageCell(GearDef g, Rectangle r)
    {
        var k = C.Captain.Kit;
        var item = Campaign.ItemOf(C.Captain, g.Id);
        var q = item?.Quality ?? ItemQuality.Plain;
        bool worn = C.Wears(g.Id), picked = invPick == g.Id, hover = ui.Hover(r);
        ui.Rect(r, picked ? Kit.Mul(Kit.Gilt, 0.28f) : Color.Black * (hover ? 0.5f : 0.35f));
        ui.Frame(r, picked ? Kit.Gilt : Color.White * (hover ? 0.3f : 0.12f), ui.U(picked ? 2 : 1));
        ui.Icon(SlotIcon(g), r.X + ui.U(5), r.Y + ui.U(6), MakeColour(q), 2);
        int tx = r.X + ui.U(26), tw = r.Right - tx - ui.U(4);
        ui.TextFit(Campaign.ItemName(item, g).ToUpperInvariant(), tx, r.Y + ui.U(5), tw, MakeColour(q), 1);
        string state = k.Weapon == g.Id ? "IN HAND" : k.Side == g.Id ? "SECOND" : worn ? "WORN" : "";
        string marks = item is { Marks.Count: > 0 } ? string.Join(" ", item.Marks.Where(Effects.Has).Select(m => Effects.Get(m).Name.ToUpperInvariant())) : "";
        if (marks != "") ui.TextFit(marks, tx, r.Y + ui.U(17), tw, Kit.Gilt, 1);
        if (state != "") ui.TextFit(state, tx, r.Y + ui.U(29), tw, Kit.Mul(Kit.Gilt, 0.85f), 1);
        else if (C.CannotUse(g) is string why) ui.TextFit(why, tx, r.Y + ui.U(29), tw, Style.Warn, 1);
        if (hover)
        {
            var lines = new List<string> { GearStats(g) };
            var cmp = Compare(g);
            if (cmp.Count > 0) lines.Add(string.Join("  ", cmp.Select(x => x.Text)) + " AGAINST WHAT YOU WEAR");
            if (item != null)
                foreach (var m in item.Marks.Where(Effects.Has).Select(Effects.Get)) lines.AddRange(ui.Wrap($"{m.Name.ToUpperInvariant()}: {m.Line.ToUpperInvariant()}", ui.U(220)));
            if (item is { From.Length: > 0 }) lines.AddRange(ui.Wrap(item.From.ToUpperInvariant() + (item.Battles > 0 ? $". {item.Battles} BATTLES." : "."), ui.U(220)));
            ui.Tooltip(r.Center.X, r.Y - ui.U(2), Campaign.ItemName(item, g).ToUpperInvariant(), lines.ToArray());
        }
        if (ui.Clicked(r)) invPick = picked ? "" : g.Id;
    }

    /// The goods lots you carry and the baggage's load against its room.
    void GoodsAndLoad(Rectangle c)
    {
        int load = C.BaggageLoad(), room = C.BaggageRoom();
        bool over = load > room;
        string head = over ? $"LOAD {load} / {room} . THE MARCH IS {(1 - C.BaggageDrag()) * 100:0}% SLOWER" : $"LOAD {load} / {room}";
        ui.Text(head, c.X, c.Y + ui.U(2), over ? Style.Bad : Style.Text, 1);
        ui.Gauge(new Rectangle(c.X, c.Y + ui.U(14), c.Width, ui.U(8)), load / (float)Math.Max(1, room), over ? Style.Bad : Kit.Gilt);
        int x = c.X, y = c.Y + ui.U(30);
        if (C.S.Goods.Count == 0) ui.Text("NO GOODS. A WON FIELD FILLS THE CARTS.", x, y, Style.Dim, 1);
        foreach (var (kind, n) in C.S.Goods)
        {
            if (n <= 0) continue;
            string text = $"{n} {kind.ToUpperInvariant()}";
            int w = ui.TW(text, 1) + ui.U(14);
            if (x + w > c.Right) { x = c.X; y += ui.U(16); }
            if (y + ui.U(12) > c.Bottom) break;
            ui.Rect(new Rectangle(x, y - ui.U(2), w - ui.U(4), ui.U(13)), Color.Black * 0.3f);
            ui.Text(text, x + ui.U(5), y + ui.U(1), Style.Text, 1);
            x += w;
        }
        ui.Text($"{C.Player.Horses} SPARE HORSES CARRY {Balance.Get().BaggagePerHorse:0} EACH (TO SIX). SELL GOODS IN A MARKET.", c.X, c.Bottom - ui.U(12), Style.Dim, 1);
    }
}
