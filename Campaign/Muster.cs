using Microsoft.Xna.Framework;

namespace Ironfield;

/// The castle's own work (docs/archive/HOLDS.md HD2): the MUSTER. A castle gathers the men, horses and food of its villages, so a
/// captain can keep a company going from castles and needs a town only to trade, hire and train past the yard's tier.
public sealed partial class Campaign
{
    /// The villages a castle or town holds: those whose seat it is.
    public IEnumerable<Settlement> Demesne(Settlement seat) =>
        S.Settlements.Where(v => v.Kind == SettlementKind.Village && SeatOf(v) == seat);

    /// Men of the demesne who would follow you, pooled at the castle.
    public int MusterRecruits(Settlement castle) => Demesne(castle).Where(v => v.ThreatUntil <= S.Hours).Sum(RecruitsForYou);

    /// What a band of ArmyScale men costs at the muster: the villages' levy price and the castellan's cut.
    public int MusterPrice(Settlement castle) =>
        Demesne(castle).Where(v => v.ThreatUntil <= S.Hours && RecruitsForYou(v) > 0).Select(v => (int)MathF.Ceiling(LevyPrice(v) * B.MusterMarkup)).DefaultIfEmpty(0).Max();

    /// Why you cannot use this castle's muster, stables, granary or yard; null if you can.
    public string? CannotMuster(Settlement s) =>
        s.Kind != SettlementKind.Castle ? "ONLY CASTLES MUSTER"
        : Refuses(s) ? "THE CASTLE IS AT WAR WITH YOUR REALM"
        : s.Besieged ? "THE CASTLE IS UNDER SIEGE" : null;

    /// Recruit up to `n` men of the castle's villages in one place, the fullest village first, each at its own price with the
    /// castellan's cut. Null on success, else the reason.
    public string? Muster(Settlement castle, int n)
    {
        if (CannotMuster(castle) is string why) return why;
        if (MusterRecruits(castle) <= 0) return "NO MEN TO MUSTER: THE VILLAGES ARE EMPTY";
        int took = 0;
        string? last = null;
        foreach (var v in Demesne(castle).Where(v => v.ThreatUntil <= S.Hours).OrderByDescending(RecruitsForYou).ToList())
        {
            int k = Math.Min(n - took, RecruitsForYou(v));
            if (k <= 0) continue;
            int before = v.Recruits;
            last = RecruitLevies(v, k, B.MusterMarkup);
            took += before - v.Recruits;
            if (took >= n || last != null) break;
        }
        return took > 0 ? null : last ?? "NO MEN TO MUSTER";
    }

    /// May men be promoted into a troop of this tier at a castle's yard (a realm you serve or are at peace with)?
    public bool CastleYard(Settlement? s, int tier) => s is { Kind: SettlementKind.Castle } && CannotMuster(s) == null && tier <= B.CastleYardTier;

    /// How many days of food the castle's granary can spare your company, keeping its garrison's reserve.
    public int GranaryDays(Settlement castle) =>
        Math.Max(0, (int)MathF.Floor((castle.Supplies - B.GranaryReserve) * Scaled(B.GranaryMouths) / (float)Math.Max(1, Mouths)));

    /// What `days` of food cost from a castle's granary: the castellan's price, below the market's.
    public int GranaryPrice(int days) => (int)MathF.Ceiling(FoodPrice(days) * B.GranaryPrice);

    /// Buy `days` of food from a castle's stores (cheaper than the market, while they last); it draws down the garrison's supplies.
    public string? BuyFromGranary(Settlement castle, int days)
    {
        if (CannotMuster(castle) is string why) return why;
        int spare = GranaryDays(castle);
        if (spare <= 0) return "THE GRANARY IS BARE";
        days = Math.Min(days, spare);
        int price = GranaryPrice(days);
        if (S.Gold < price) return $"NEEDS {price} CROWNS";
        S.Gold -= price;
        Player.Food += days;
        castle.Supplies -= days * Math.Max(1, Mouths) / (float)Scaled(B.GranaryMouths);
        return null;
    }

    /// Castles breed horses for their lord (weekly), fewer than a town's market but enough for remounts.
    internal void CastleStablesWeekly()
    {
        foreach (var c in S.Settlements.Where(c => c.Kind == SettlementKind.Castle))
            c.Horses = Math.Min(Scaled(B.CastleHorsesMax), c.Horses + Scaled(B.CastleHorsesWeek * HeldShare(c))); // foals from its villages (HD3)
    }
}
