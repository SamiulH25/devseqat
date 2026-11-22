namespace Ironfield;

/// Company storage system: item instance IDs, baggage wagons, road jettisoning, and the Keep Vault at owned towns/castles.
public sealed partial class Campaign
{
    /// Ensure every GearItem in baggage and settlement vaults has a unique positive Id (migrates old saves cleanly).
    internal void EnsureItemIds()
    {
        if (S.NextItemId < 1) S.NextItemId = 1;
        void Stamp(List<GearItem> list)
        {
            foreach (var i in list)
            {
                if (i.Id <= 0) i.Id = S.NextItemId++;
                else if (i.Id >= S.NextItemId) S.NextItemId = i.Id + 1;
            }
        }
        Stamp(Captain.Baggage);
        foreach (var s in S.Settlements)
        {
            s.Vault.RemoveAll(i => Gear.Find(i.Def) == null);
            Stamp(s.Vault);
        }
    }

    /// Add a new distinct piece to the baggage (allows holding duplicates of the same gear kind).
    public GearItem AddItem(string def, ItemQuality q = ItemQuality.Plain, string mark = "", string from = "")
    {
        Gear.Get(def);
        var i = new GearItem { Id = S.NextItemId++, Def = def, Quality = q, From = from };
        if (mark != "") i.Marks.Add(mark);
        Captain.Baggage.Add(i);
        return i;
    }

    /// Equip a specific GearItem instance from baggage (supports holding multiple copies of the same gear kind).
    public string? EquipItem(GearItem item, bool side = false)
    {
        if (!Captain.Baggage.Contains(item)) return "NOT IN YOUR BAGGAGE";
        if (Gear.Find(item.Def) is not GearDef g) return "NO SUCH GEAR";
        if (CannotUse(g) is string why) return why;
        EnsureItemIds();
        var k = Captain.Kit;
        switch (g.Slot)
        {
            case "weapon":
                if (side)
                {
                    if (ItemInSlot(Captain, "weapon") == item)
                    {
                        if (k.Side == "") return "IN YOUR MAIN HAND";
                        var prevSide = ItemInSlot(Captain, "side");
                        k.Weapon = k.Side;
                        if (prevSide is { Id: > 0 }) k.WornIds["weapon"] = prevSide.Id; else k.WornIds.Remove("weapon");
                    }
                    k.Side = item.Def;
                    k.WornIds["side"] = item.Id;
                }
                else
                {
                    if (ItemInSlot(Captain, "side") == item)
                    {
                        var prevWep = ItemInSlot(Captain, "weapon");
                        k.Side = k.Weapon;
                        if (prevWep is { Id: > 0 }) k.WornIds["side"] = prevWep.Id; else k.WornIds.Remove("side");
                    }
                    k.Weapon = item.Def;
                    k.WornIds["weapon"] = item.Id;
                }
                break;
            case "shield": k.Shield = item.Def; k.WornIds["shield"] = item.Id; break;
            case "body": k.Body = item.Def; k.WornIds["body"] = item.Id; break;
            case "helm": k.Helm = item.Def; k.WornIds["helm"] = item.Id; break;
            default: k.Horse = item.Def; k.WornIds["horse"] = item.Id; break;
        }
        AudioEngine.Current?.Play(g.Slot == "weapon" ? Cue.EquipBlade : g.Slot == "horse" ? Cue.HorseTack : Cue.EquipArmor, 0.7f);
        return null;
    }

    /// Sell a specific GearItem instance from baggage at a town or castle market.
    public string? SellGearItem(GearItem item)
    {
        if (!Captain.Baggage.Contains(item) || Gear.Find(item.Def) is not GearDef g) return "NOT YOURS";
        if (IsWorn(Captain, item)) return "TAKE IT OFF FIRST";
        S.Gold += SellPrice(g, item);
        LogYours($"You sell your {ItemName(item, g).ToLowerInvariant()}.");
        Captain.Baggage.Remove(item);
        AudioEngine.Current?.Play(Cue.Coin, 0.7f);
        return null;
    }

    /// Drop a specific GearItem instance on the road (refused when worn or named).
    public string? DropGearItem(GearItem item)
    {
        if (!Captain.Baggage.Contains(item) || Gear.Find(item.Def) is not GearDef g) return "NOT YOURS";
        if (IsWorn(Captain, item)) return "TAKE IT OFF FIRST";
        if (item.Name != "") return "A NAMED PIECE IS NOT LEFT BEHIND";
        Captain.Baggage.Remove(item);
        LogYours($"You leave the {ItemName(item, g).ToLowerInvariant()} behind.");
        return null;
    }

    /// Leave excess goods lots behind on the road to lighten the baggage train.
    public string? DropGoods(string kind, int count)
    {
        if (count <= 0) return "NOTHING TO DROP";
        int have = S.Goods.GetValueOrDefault(kind);
        if (have <= 0) return "NONE CARRIED";
        int take = Math.Min(have, count);
        S.Goods[kind] = have - take;
        if (S.Goods[kind] <= 0) S.Goods.Remove(kind);
        LogYours($"You leave {take} lots of {GoodsOf(kind).Name.ToLowerInvariant()} behind.");
        return null;
    }

    // ---------- baggage wagons ----------

    public string? CannotBuyWagon(Settlement s)
    {
        if (s.Kind != SettlementKind.Town) return "ONLY TOWNS BUILD WAGONS";
        if (Refuses(s)) return "THIS TOWN WILL NOT TRADE WITH YOU";
        if (Player.Wagons >= B.MaxWagons) return $"AT MOST {B.MaxWagons} WAGONS";
        return S.Gold < B.WagonPrice ? $"NEEDS {B.WagonPrice} CROWNS" : null;
    }

    public string? BuyWagon(Settlement s)
    {
        if (CannotBuyWagon(s) is string why) return why;
        S.Gold -= B.WagonPrice;
        Player.Wagons++;
        LogYours($"You buy a baggage wagon in {s.Name} (+{B.BaggagePerWagon:0} baggage room).");
        AudioEngine.Current?.Play(Cue.Coin, 0.7f);
        return null;
    }

    public string? SellWagon(Settlement s)
    {
        if (s.Kind != SettlementKind.Town || Refuses(s)) return "ONLY TOWN MARKETS BUY WAGONS";
        if (Player.Wagons <= 0) return "NO WAGONS TO SELL";
        Player.Wagons--;
        int pay = B.WagonPrice / 2;
        S.Gold += pay;
        LogYours($"You sell a baggage wagon in {s.Name} for {pay} crowns.");
        AudioEngine.Current?.Play(Cue.Coin, 0.6f);
        return null;
    }

    // ---------- keep vault (your own town or castle) ----------

    /// The owned town or castle whose keep vault you can access right now, or null.
    public Settlement? VaultAt() => SettlementAt(Player.Pos) is { Kind: SettlementKind.Town or SettlementKind.Castle } s && s.Owner == Player.Id ? s : null;

    /// Total pieces and goods lots stored in a settlement's vault.
    public static int VaultCount(Settlement s) => s.Vault.Count + s.VaultGoods.Values.Sum();

    public string? CannotStoreItem(Settlement s, GearItem item)
    {
        if (s.Owner != Player.Id || s.Kind == SettlementKind.Village) return "ONLY YOUR OWN KEEP HAS A VAULT";
        if (!Captain.Baggage.Contains(item)) return "NOT IN YOUR BAGGAGE";
        if (IsWorn(Captain, item)) return "TAKE IT OFF FIRST";
        return null;
    }

    public string? StoreItem(Settlement s, GearItem item)
    {
        if (CannotStoreItem(s, item) is string why) return why;
        EnsureItemIds();
        Captain.Baggage.Remove(item);
        s.Vault.Add(item);
        LogYours($"You store {ItemName(item, Gear.Get(item.Def)).ToLowerInvariant()} in {s.Name}'s vault.");
        return null;
    }

    public string? TakeStoredItem(Settlement s, GearItem item)
    {
        if (s.Owner != Player.Id || !s.Vault.Contains(item)) return "NOT IN THIS VAULT";
        s.Vault.Remove(item);
        Captain.Baggage.Add(item);
        LogYours($"You take {ItemName(item, Gear.Get(item.Def)).ToLowerInvariant()} from {s.Name}'s vault.");
        return null;
    }

    public string? StoreGoods(Settlement s, string kind, int count)
    {
        if (s.Owner != Player.Id || s.Kind == SettlementKind.Village) return "ONLY YOUR OWN KEEP HAS A VAULT";
        int have = S.Goods.GetValueOrDefault(kind);
        if (count <= 0 || have <= 0) return "NO GOODS TO STORE";
        int move = Math.Min(have, count);
        S.Goods[kind] = have - move;
        if (S.Goods[kind] <= 0) S.Goods.Remove(kind);
        s.VaultGoods[kind] = s.VaultGoods.GetValueOrDefault(kind) + move;
        return null;
    }

    public string? TakeStoredGoods(Settlement s, string kind, int count)
    {
        if (s.Owner != Player.Id) return "NOT YOUR VAULT";
        int have = s.VaultGoods.GetValueOrDefault(kind);
        if (count <= 0 || have <= 0) return "NONE IN THIS VAULT";
        int move = Math.Min(have, count);
        s.VaultGoods[kind] = have - move;
        if (s.VaultGoods[kind] <= 0) s.VaultGoods.Remove(kind);
        S.Goods[kind] = S.Goods.GetValueOrDefault(kind) + move;
        return null;
    }
}
