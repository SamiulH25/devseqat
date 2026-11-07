using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the armourer in a town's market (docs/PROGRESSION.md P1): this week's stock and your own gear by slot,
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
        ui.Text(GearStats(g), r.X + ui.U(30), r.Y + ui.U(19), Style.Dim, 1);
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
