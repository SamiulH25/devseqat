using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the armourer in a town's market (docs/archive/PROGRESSION.md P1): this week's stock and your own gear by slot,
/// to buy, wear, carry as a second weapon, or sell.
partial class MapScreen
{
    int gearSlot;
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
            if (y + rh - ui.U(4) > c.Bottom) break;
            GearRow(g, new Rectangle(c.X, y, c.Width, rh - ui.U(4)));
            y += rh;
        }
        if (list.All(g => C.Owns(g.Id)) && y + ui.U(12) < c.Bottom)
            ui.Text("NOTHING MORE OF THIS KIND FOR SALE HERE THIS WEEK.", c.X, y + ui.U(2), Style.Dim, 1);
    }

    void GearRow(GearDef g, Rectangle r)
    {
        var k = C.Captain.Kit;
        bool owned = C.Owns(g.Id), main = k.Weapon == g.Id || g.Slot != "weapon" && C.Wears(g.Id), second = k.Side == g.Id;
        TownRow(r);
        string icon = g.Slot switch { "weapon" => g.Ranged ? "arrow" : "sword", "helm" => "crown", "horse" => "horse", _ => "shield" };
        var col = main || second ? Kit.Gilt : Style.Text;
        ui.Icon(icon, r.X + ui.U(8), r.Y + ui.U(8), col, 2);
        string state = main ? (g.Slot == "weapon" ? " . IN HAND" : " . WORN") : second ? " . SECOND" : "";
        ui.Text(g.Name.ToUpperInvariant() + state, r.X + ui.U(30), r.Y + ui.U(6), col, 1);
        string stats = GearStats(g);
        ui.Text(stats, r.X + ui.U(30), r.Y + ui.U(19), Style.Dim, 1);
        // Against what you wear now (P7): better in green, worse in red.
        int cx = r.X + ui.U(30) + ui.TW(stats, 1) + ui.U(8);
        if (!main && !second)
            foreach (var (text, good) in Compare(g))
            {
                if (cx + ui.TW(text, 1) > r.Right - ui.U(112)) break; // clear of the buttons
                ui.Text(text, cx, r.Y + ui.U(19), good ? Style.Ok : Style.Bad, 1);
                cx += ui.TW(text, 1) + ui.U(6);
            }
        int bw = ui.U(50), bx = r.Right - ui.U(4) - bw, by = r.Center.Y - ui.U(10);
        Rectangle B() { var b = new Rectangle(bx, by, bw, ui.U(20)); bx -= bw + ui.U(4); return b; }
        string info = "";
        Color infoCol = Kit.Gilt;
        if (!owned)
        {
            string? why = C.CannotBuy(g);
            if (ui.Button(B(), "BUY", why == null)) note = C.BuyGear(g) ?? $"YOU WEAR THE {g.Name.ToUpperInvariant()}";
            (info, infoCol) = why == null || why.Contains("CROWNS") ? ($"{g.Cost} CROWNS", why == null ? Kit.Gilt : Style.Warn) : (why, Style.Warn);
        }
        else
        {
            string? cant = C.CannotUse(g);
            if (second && ui.Button(B(), "DROP")) C.ClearSide();
            if (!main && !second && !g.Free && ui.Button(B(), "SELL")) note = C.SellGear(g) ?? $"SOLD FOR {Campaign.SellPrice(g)} CROWNS";
            if (g.Slot == "weapon" && !second && !main && ui.Button(B(), "2ND", cant == null)) note = C.Equip(g.Id, true) ?? "";
            if (!main && ui.Button(B(), "WEAR", cant == null)) note = C.Equip(g.Id) ?? "";
            if (cant != null) (info, infoCol) = (cant, Style.Warn);
            else if (!main && !second && !g.Free) (info, infoCol) = ($"SELLS {Campaign.SellPrice(g)}", Style.Dim);
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
