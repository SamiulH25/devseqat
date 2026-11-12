using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the armourer in a town's market (docs/archive/PROGRESSION.md P1): this week's stock and your own gear by slot,
/// to buy, wear, carry as a second weapon, or sell.
partial class MapScreen
{
    int gearSlot, bladeName;
    static readonly string[] GearTabs = { "WEAPONS", "SHIELDS", "ARMOUR", "HELMS", "HORSES" };

    void ArmourerWell(Settlement s, Rectangle c)
    {
        if (ui.Tabs(new Rectangle(c.X, c.Y, c.Width, ui.U(20)), GearTabs, gearSlot) is int t and >= 0) gearSlot = t;
        string slot = Gear.Slots[gearSlot];
        var stock = C.ArmourerStock(s);
        var list = Gear.InSlot(slot).Where(g => C.Owns(g.Id) || stock.Contains(g)).OrderBy(g => g.Cost).ToList();
        int y = c.Y + ui.U(26), rh = ui.U(40);
        foreach (var g in list)
        {
            if (y + rh - ui.U(4) > c.Bottom - ui.U(16)) break; // clear of the baggage line
            GearRow(g, new Rectangle(c.X, y, c.Width, rh - ui.U(4)), s);
            y += rh;
        }
        if (list.All(g => C.Owns(g.Id)) && y + ui.U(12) < c.Bottom)
            ui.Text("NOTHING MORE OF THIS KIND FOR SALE HERE THIS WEEK.", c.X, y + ui.U(2), Style.Dim, 1);
        Baggage(new Rectangle(c.X, c.Bottom - ui.U(14), c.Width, ui.U(14)));
    }

    /// The baggage line under the armourer: what it carries against its room (C9), red when the march is slowed.
    void Baggage(Rectangle r)
    {
        int load = C.BaggageLoad(), room = C.BaggageRoom();
        bool over = load > room;
        string text = over ? $"BAGGAGE {load} / {room} . OVERLOADED: THE MARCH IS {(1 - C.BaggageDrag()) * 100:0}% SLOWER" : $"BAGGAGE {load} / {room}";
        ui.Rect(new Rectangle(r.X, r.Y - ui.U(3), r.Width, 1), Kit.Mul(Kit.Gilt, 0.35f)); // a hairline over it
        var col = over ? Style.Bad : Style.Text;
        ui.Icon("horse", r.X, r.Y + ui.U(1), col, 1);
        ui.Text(text, r.X + ui.U(12), r.Y + ui.U(3), col, 1);
        // A small bar of the load against the room, red past it.
        var bar = new Rectangle(r.Right - ui.U(90), r.Y + ui.U(3), ui.U(90), ui.U(7));
        if (r.X + ui.U(12) + ui.TW(text, 1) + ui.U(8) < bar.X)
        {
            ui.Rect(bar, Color.Black * 0.5f);
            ui.Rect(new Rectangle(bar.X, bar.Y, (int)(bar.Width * MathF.Min(1, load / (float)Math.Max(1, room))), bar.Height), over ? Style.Bad : Kit.Mul(Kit.Gilt, 0.8f));
        }
    }

    void GearRow(GearDef g, Rectangle r, Settlement s)
    {
        var k = C.Captain.Kit;
        bool owned = C.Owns(g.Id), main = k.Weapon == g.Id || g.Slot != "weapon" && C.Wears(g.Id), second = k.Side == g.Id;
        var item = Campaign.ItemOf(C.Captain, g.Id);
        var (make, mark) = owned ? (item?.Quality ?? ItemQuality.Plain, "") : C.StockMake(s, g);
        var marks = owned ? item?.Marks ?? new List<string>() : mark == "" ? new List<string>() : new List<string> { mark };
        string name = owned ? Campaign.ItemName(item, g) : Campaign.ItemName(new GearItem { Def = g.Id, Quality = make }, g);
        TownRow(r);
        string icon = g.Slot switch { "weapon" => g.Ranged ? "arrow" : "sword", "helm" => "crown", "horse" => "horse", _ => "shield" };
        var col = main || second ? Kit.Gilt : Style.Text;
        ui.Icon(icon, r.X + ui.U(8), r.Y + ui.U(8), col, 2);
        string state = main ? (g.Slot == "weapon" ? " . IN HAND" : " . WORN") : second ? " . SECOND" : "";
        var makeCol = make == ItemQuality.Named ? Kit.Gilt : make == ItemQuality.Masterwork ? new Color(150, 200, 235) : make == ItemQuality.Fine ? new Color(170, 215, 170) : make == ItemQuality.Worn ? Style.Dim : col;
        if (!main && !second) col = makeCol; // the make tints a piece you do not wear; gold stays for what you wear
        string title = name.ToUpperInvariant() + state;
        ui.Text(title, r.X + ui.U(30), r.Y + ui.U(6), col, 1);
        // The smith's marks after the name (C9), gold; their line in the tooltip.
        int mx = r.X + ui.U(30) + ui.TW(title, 1) + ui.U(8);
        foreach (var m in marks.Where(Effects.Has).Select(Effects.Get))
        {
            var mr = new Rectangle(mx, r.Y + ui.U(4), ui.TW(m.Name.ToUpperInvariant(), 1) + ui.U(8), ui.U(11));
            if (mr.Right > r.Right - ui.U(owned ? 170 : 150)) break; // clear of the price and buttons
            ui.Rect(mr, Color.Black * 0.35f);
            ui.Text(m.Name.ToUpperInvariant(), mr.X + ui.U(4), r.Y + ui.U(6), Kit.Gilt, 1);
            if (ui.Hover(mr)) ui.Tooltip(mr.Center.X, mr.Y - ui.U(2), m.Name.ToUpperInvariant(), ui.Wrap(m.Line.ToUpperInvariant(), ui.U(170)).ToArray());
            mx = mr.Right + ui.U(4);
        }
        string stats = GearStats(g);
        var story = new Rectangle(r.X, r.Y + ui.U(16), r.Width - ui.U(112), r.Height - ui.U(16));
        if (item != null && item.From != "" && ui.Hover(story))
            ui.Tooltip(story.X + ui.U(60), story.Y - ui.U(2), name.ToUpperInvariant(), ui.Wrap((item.From + "." + (item.Battles > 0 ? $" {item.Battles} BATTLES." : "")).ToUpperInvariant(), ui.U(190)).ToArray());
        ui.Text(stats, r.X + ui.U(30), r.Y + ui.U(19), Style.Dim, 1);
        if (item is { Kills: > 0 }) // the blade's tally, as a trophy after its stats
        {
            string kills = $"{item.Kills} KILLS";
            int kx = r.X + ui.U(30) + ui.TW(stats, 1) + ui.U(8);
            ui.Icon("skull", kx, r.Y + ui.U(18), Kit.Gilt, 1);
            ui.Text(kills, kx + ui.U(10), r.Y + ui.U(19), Kit.Gilt, 1);
            stats += "   " + kills + "  ";
        }
        // Against what you wear now (P7): better in green, worse in red.
        int cx = r.X + ui.U(30) + ui.TW(stats, 1) + ui.U(8);
        if (!main && !second)
            foreach (var (text, good) in Compare(g))
            {
                if (cx + ui.TW(text, 1) > r.Right - ui.U(owned ? 170 : 112)) break; // clear of the buttons (an owned piece has three)
                ui.Text(text, cx, r.Y + ui.U(19), good ? Style.Ok : Style.Bad, 1);
                cx += ui.TW(text, 1) + ui.U(6);
            }
        int bw = ui.U(50), bx = r.Right - ui.U(4) - bw, by = r.Center.Y - ui.U(10);
        Rectangle B() { var b = new Rectangle(bx, by, bw, ui.U(20)); bx -= bw + ui.U(4); return b; }
        string info = "";
        Color infoCol = Kit.Gilt;
        if (!owned)
        {
            string? why = C.CannotBuy(g, make, mark);
            int cost = Campaign.PriceOf(g, make, mark == "" ? 0 : 1);
            if (ui.Button(B(), "BUY", why == null)) note = C.BuyGear(g, make, mark) ?? $"YOU WEAR THE {name.ToUpperInvariant()}";
            (info, infoCol) = why == null || why.Contains("CROWNS") ? ($"{cost} CROWNS", why == null ? Kit.Gilt : Style.Warn) : (why, Style.Warn);
        }
        else
        {
            string? cant = C.CannotUse(g);
            if (second && ui.Button(B(), "DROP")) C.ClearSide();
            int sells = Campaign.SellPrice(g, item);
            if (item != null && Campaign.MayName(item))
            {
                string offer = Campaign.BladeNames[bladeName % Campaign.BladeNames.Length];
                if (ui.Button(B(), "OTHER")) bladeName++;
                if (ui.Button(B(), "NAME")) note = C.NameItem(item, offer) ?? $"IT IS {offer.ToUpperInvariant()} NOW";
                ui.TextR($"EARNED A NAME: {offer.ToUpperInvariant()}?", bx + bw, r.Y + ui.U(6), Kit.Gilt, 1);
                return;
            }
            if (!main && !second && (!g.Free || item != null))
            {
                var sb = B();
                if (ui.Button(sb, "SELL")) note = C.SellGear(g) ?? $"SOLD FOR {sells} CROWNS";
                if (ui.Hover(sb)) ui.Tooltip(sb.Center.X, sb.Y - ui.U(2), "SELL", new[] { $"THE ARMOURER PAYS {sells} CROWNS." });
            }
            if (g.Slot == "weapon" && !second && !main && ui.Button(B(), "2ND", cant == null)) note = C.Equip(g.Id, true) ?? "";
            if (!main && ui.Button(B(), "WEAR", cant == null)) note = C.Equip(g.Id) ?? "";
            if (cant != null) (info, infoCol) = (cant, Style.Warn);
        }
        if (info != "") ui.TextR(info, bx + bw, r.Y + ui.U(6), infoCol, 1);
    }

    /// How a piece differs from the one worn in its slot (the weapon in hand for weapons): the stats that change.
    List<(string Text, bool Good)> Compare(GearDef g)
    {
        var k = C.Captain.Kit;
        var worn = Gear.Find(g.Slot switch { "weapon" => k.Weapon, "shield" => k.Shield, "body" => k.Body, "helm" => k.Helm, _ => k.Horse });
        var d = new List<(string, bool)>();
        if (worn == null || worn.Id == g.Id) return d;
        void Add(string what, float delta, string fmt, bool more = true) { if (MathF.Abs(delta) >= 0.05f) d.Add(($"{delta.ToString(fmt)} {what}", delta > 0 == more)); }
        switch (g.Slot)
        {
            case "weapon": Add("DMG", g.Damage - worn.Damage, "+0;-0"); if (!g.Ranged && !worn.Ranged) Add("REACH", g.Reach - worn.Reach, "+0.0;-0.0"); break;
            case "shield": Add("BLOCK", (g.Block - worn.Block) * 100, "+0;-0"); break;
            case "horse":
                var (a, b) = (Troops.Mount(g.Mount), Troops.Mount(worn.Mount));
                Add("HEALTH", a.Hp - b.Hp, "+0;-0"); Add("GALLOP", a.Gallop - b.Gallop, "+0.0;-0.0");
                break;
            default: Add("ARMOUR", g.Armor - worn.Armor, "+0;-0"); Add("SPEED", g.Speed - worn.Speed, "+0.0;-0.0"); break;
        }
        return d;
    }

    /// One short line of what a piece does.
    static string GearStats(GearDef g) => g.Slot switch
    {
        "weapon" => g.Ranged ? $"DMG {g.Damage:0} . RANGE {g.Range:0} M . {g.Ammo} SHOTS" + (g.Pierce > 0 ? $" . PIERCE {g.Pierce * 100:0}%" : "")
            : $"DMG {g.Damage:0} . REACH {g.Reach:0.0} M" + (g.Pierce > 0 ? $" . PIERCE {g.Pierce * 100:0}%" : "") + (g.TwoHanded ? " . 2 HANDS" : "") + (g.Thrust ? " . THRUSTS" : ""),
        "shield" => $"BLOCK {g.Block * 100:0}%" + (g.Speed != 0 ? $" . SPEED {g.Speed:+0.0;-0.0}" : ""),
        "horse" => Troops.Mount(g.Mount) is var m ? $"HEALTH {m.Hp:0} . GALLOP {m.Gallop:0.0} M/S" : "",
        _ => $"ARMOUR {g.Armor:+0;-0;0}" + (g.Speed != 0 ? $" . SPEED {g.Speed:+0.0;-0.0}" : ""),
    };
}
