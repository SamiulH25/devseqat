namespace Ironfield;

/// The captain's armoury (docs/PROGRESSION.md P1): owning, buying, selling and wearing gear from Data/gear.json.
public sealed partial class Campaign
{
    /// What the armourer pays back for a piece you sell.
    public const float SellBack = 0.4f;

    /// A save from before the armoury: its plate, helm and horse tiers become the matching gear, and every worn piece is owned.
    internal void MigrateGear()
    {
        var c = Captain;
        var k = c.Kit;
        if (c.ArmorTier > 0) k.Body = "plate";
        if (c.HelmTier > 0) k.Helm = "great_helm";
        if (c.HorseTier >= 1) { k.Horse = "warhorse"; Own("warhorse"); }
        if (c.HorseTier >= 2) k.Horse = "destrier";
        c.ArmorTier = c.HelmTier = c.HorseTier = 0;
        foreach (var id in k.Worn.ToList())
            if (Gear.Find(id) == null) Unequip(id);
            else Own(id);
    }

    void Own(string id) { if (!Gear.Get(id).Free && !Captain.Kit.Owned.Contains(id)) Captain.Kit.Owned.Add(id); }

    /// A worn piece the data no longer has goes back to the free piece of its slot.
    void Unequip(string id)
    {
        var k = Captain.Kit;
        if (k.Side == id) { k.Side = ""; return; }
        string Free(string slot) => Gear.InSlot(slot).First(g => g.Free).Id;
        if (k.Weapon == id) k.Weapon = Free("weapon");
        if (k.Shield == id) k.Shield = Free("shield");
        if (k.Body == id) k.Body = Free("body");
        if (k.Helm == id) k.Helm = Free("helm");
        if (k.Horse == id) k.Horse = Free("horse");
    }

    public bool Owns(string id) => Gear.Find(id) is GearDef g && (g.Free || Captain.Kit.Owned.Contains(id));
    public bool Wears(string id) => Captain.Kit.Worn.Contains(id);

    /// Why the captain may not use this piece (the Arms skill it needs), or null.
    public string? CannotUse(GearDef g) => Skill("Arms") < g.Needs ? $"NEEDS ARMS {g.Needs}" : null;

    /// Why the captain may not buy it: owned, rank, skill or gold.
    public string? CannotBuy(GearDef g)
    {
        if (Owns(g.Id)) return "OWNED";
        if ((int)Rank < g.Rank) return $"FOR A {RankName((Rank)g.Rank)}";
        return CannotUse(g) ?? (S.Gold < g.Cost ? $"NEEDS {g.Cost} CROWNS" : null);
    }

    /// What the armourer of this place has this week: most of the list in a town, less in a castle, little in a village.
    /// The same all week (seeded by place and week), so looking again does not reroll it.
    public List<GearDef> ArmourerStock(Settlement s)
    {
        int week = (int)(S.Hours / (24 * 7));
        int chance = s.Kind switch { SettlementKind.Town => 70, SettlementKind.Castle => 45, _ => 20 };
        return Gear.All.Where(g => !g.Free && (s.Kind != SettlementKind.Village || g.Cost <= 300)
            && (uint)HashCode.Combine(S.Seed, s.Id, week, g.Id.GetHashCode(StringComparison.Ordinal)) % 100 < chance).ToList();
    }

    /// Buy a piece and wear it at once (a weapon goes in the main hand).
    public string? BuyGear(GearDef g)
    {
        if (CannotBuy(g) is string why) return why;
        S.Gold -= g.Cost;
        Own(g.Id);
        Log($"You buy a {g.Name.ToLowerInvariant()}.");
        return Equip(g.Id);
    }

    /// Wear a piece you own; `side` puts a weapon in the second hand (the one N swaps to).
    public string? Equip(string id, bool side = false)
    {
        if (Gear.Find(id) is not GearDef g) return "NO SUCH GEAR";
        if (!Owns(id)) return "NOT YOURS";
        if (CannotUse(g) is string why) return why;
        var k = Captain.Kit;
        switch (g.Slot)
        {
            case "weapon":
                if (side)
                {
                    if (k.Weapon == id) { if (k.Side == "") return "IN YOUR MAIN HAND"; k.Weapon = k.Side; }
                    k.Side = id;
                }
                else { if (k.Side == id) k.Side = k.Weapon; k.Weapon = id; }
                break;
            case "shield": k.Shield = id; break;
            case "body": k.Body = id; break;
            case "helm": k.Helm = id; break;
            default: k.Horse = id; break;
        }
        return null;
    }

    /// Carry no second weapon.
    public void ClearSide() => Captain.Kit.Side = "";

    /// Sell a piece you own and do not wear, for part of its price.
    public string? SellGear(GearDef g)
    {
        if (!Owns(g.Id) || g.Free) return "NOT FOR SALE";
        if (Wears(g.Id)) return "TAKE IT OFF FIRST";
        Captain.Kit.Owned.Remove(g.Id);
        S.Gold += SellPrice(g);
        Log($"You sell your {g.Name.ToLowerInvariant()}.");
        return null;
    }

    public static int SellPrice(GearDef g) => (int)(g.Cost * SellBack);
}
