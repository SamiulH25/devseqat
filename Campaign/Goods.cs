namespace Ironfield;

/// A kind of goods taken from a beaten foe, and what one lot of it is worth at an ordinary market.
public sealed record GoodsKind(string Id, string Name, int Value);

/// Campaign: goods from the field. Most of a won battle's loot comes as goods you carry (CampaignState.Goods, kind -> lots)
/// and sell in a town's market; only a share (Balance.LootCoinShare) comes as crowns at once.
public partial class Campaign
{
    public static readonly GoodsKind[] GoodsKinds =
    {
        new("arms", "Arms", 12), new("armour", "Armour", 30), new("harness", "Harness", 20),
        new("silver", "Silver plate", 45), new("trinkets", "Trinkets", 8), new("furs", "Furs and hides", 10),
    };

    public static GoodsKind GoodsOf(string id) => GoodsKinds.First(k => k.Id == id);

    /// What a fallen man of this troop leaves on the field.
    public static string GoodsFrom(UnitType t) => t.Wild ? (t.Breed == "goblin" ? "trinkets" : "furs") : t.Mount != null ? "harness" : t.Tier >= 3 ? "armour" : "arms";

    /// Lots of goods you carry.
    public int GoodsCount => S.Goods.Values.Sum();

    /// What a town's market pays for one lot: more in a prosperous town, a third more for the kind it wants this week.
    /// 0 where it will not buy (villages, castles, towns that refuse you).
    public int GoodsPrice(GoodsKind k, Settlement s)
    {
        if (s.Kind != SettlementKind.Town || Refuses(s)) return 0;
        float mul = 0.85f + s.Prosperity / 400f;
        if (WantedGoods(s) == k.Id) mul *= 1.3f;
        return Math.Max(1, (int)MathF.Round(k.Value * mul));
    }

    /// The kind of goods a town is short of this week (it changes each week and differs from town to town).
    public string WantedGoods(Settlement s) => GoodsKinds[(int)((s.Id * 7 + (long)(S.Hours / (24 * 7))) % GoodsKinds.Length)].Id;

    /// Sell every lot of one kind (or of every kind when kind is null) in a town's market. Returns why not.
    public string? SellGoods(Settlement s, string? kind = null)
    {
        if (s.Kind != SettlementKind.Town) return "ONLY TOWN MARKETS BUY GOODS";
        if (Refuses(s)) return "THIS TOWN WILL NOT TRADE WITH YOU";
        var kinds = S.Goods.Where(g => g.Value > 0 && (kind == null || g.Key == kind)).ToList();
        if (kinds.Count == 0) return "NO GOODS TO SELL";
        int gold = 0, lots = 0;
        foreach (var (id, n) in kinds) { gold += n * GoodsPrice(GoodsOf(id), s); lots += n; S.Goods.Remove(id); }
        S.Gold += gold;
        Log($"You sell {lots} lots of goods in {s.Name} for {gold} crowns.");
        AudioEngine.Current?.Play(Cue.Coin);
        return null;
    }

    /// A won battle's loot, by kind of goods, into crowns and goods: LootCoinShare comes as crowns, the rest as whole lots
    /// (the odd change as crowns). The goods go into your pack. Returns the crowns and the lots taken.
    (float Coin, List<(string Kind, int Count)> Goods) TakeLoot(Dictionary<string, float> worth)
    {
        float coin = 0;
        var taken = new List<(string Kind, int Count)>();
        foreach (var k in GoodsKinds)
        {
            if (!worth.TryGetValue(k.Id, out float w) || w <= 0) continue;
            coin += w * B.LootCoinShare;
            float rest = w * (1 - B.LootCoinShare);
            int n = (int)(rest / k.Value);
            coin += rest - n * k.Value;
            if (n <= 0) continue;
            S.Goods[k.Id] = S.Goods.GetValueOrDefault(k.Id) + n;
            taken.Add((k.Id, n));
        }
        return (coin, taken);
    }
}
