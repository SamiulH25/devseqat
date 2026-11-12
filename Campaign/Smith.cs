namespace Ironfield;

/// The smith (docs/COMPANY.md C11): in a town or castle, mend a battered piece, raise its make one step for crowns and goods
/// lots, or have a mark struck anew. A worn piece becomes plain, plain fine and fine masterwork; masterwork needs a town of
/// prosperity 60 or more (or one with a market built). Named pieces are beyond any smith's improving. And falling in
/// battle may batter one worn piece, so better kit is a thing to keep, not only to buy.
public sealed partial class Campaign
{
    /// Prosperity a town needs before its smith can work masterwork.
    public const float MasterworkProsperity = 60;

    /// The smith where you stand (a town or castle), or null.
    public Settlement? Smithy() => SettlementAt(Player.Pos) is { Kind: SettlementKind.Town or SettlementKind.Castle } s && !Refuses(s) ? s : null;

    /// Whether a place's smith can work masterwork.
    public bool GreatSmith(Settlement s) => s.Kind == SettlementKind.Town && (s.Prosperity >= MasterworkProsperity || Level(s, "market") > 0);

    /// Mending a battered piece: a tenth of its price.
    public static int MendCost(GearDef g) => Math.Max(5, g.Cost / 10);

    /// Raising a piece one make: the price difference between the two makes, and lots of arms or armour.
    public static (int Crowns, string Goods, int Lots) ImproveCost(GearDef g, ItemQuality from)
    {
        int crowns = Math.Max(20, PriceOf(g, from + 1) - PriceOf(g, from));
        string goods = g.Slot is "body" or "helm" or "shield" ? "armour" : g.Slot == "horse" ? "" : "arms";
        return (crowns, goods, goods == "" ? 0 : from >= ItemQuality.Fine ? 4 : 2);
    }

    /// Striking a new mark: a quarter of the piece's price.
    public static int RemarkCost(GearDef g) => Math.Max(15, g.Cost / 4);

    /// Why the smith will not mend this piece, or null.
    public string? CannotMend(GearItem i)
    {
        if (Smithy() == null) return "NO SMITH HERE";
        if (!i.Battered) return "IT IS WHOLE";
        int cost = MendCost(Gear.Get(i.Def));
        return S.Gold < cost ? $"NEEDS {cost} CROWNS" : null;
    }

    public string? Mend(GearItem i)
    {
        if (CannotMend(i) is string why) return why;
        var g = Gear.Get(i.Def);
        S.Gold -= MendCost(g);
        i.Battered = false;
        Log($"The smith mends your {ItemName(i, g).ToLowerInvariant()}.");
        AudioEngine.Current?.Play(Cue.AnvilRing, 0.6f);
        return null;
    }

    /// Why the smith will not raise this piece's make, or null. A horse is bred, not forged.
    public string? CannotImprove(GearItem i)
    {
        var g = Gear.Get(i.Def);
        if (Smithy() is not Settlement s) return "NO SMITH HERE";
        if (g.Slot == "horse") return "A HORSE IS BRED, NOT FORGED";
        if (i.Quality >= ItemQuality.Masterwork) return "NO SMITH CAN BETTER IT";
        if (i.Battered) return "MEND IT FIRST";
        if (i.Quality == ItemQuality.Fine && !GreatSmith(s)) return "MASTERWORK NEEDS A GREAT TOWN'S SMITH";
        var (crowns, goods, lots) = ImproveCost(g, i.Quality);
        if (S.Gold < crowns) return $"NEEDS {crowns} CROWNS";
        if (lots > 0 && S.Goods.GetValueOrDefault(goods) < lots) return $"NEEDS {lots} LOTS OF {goods.ToUpperInvariant()}";
        return null;
    }

    /// Raise a piece's make one step; a free starting piece becomes a thing in the baggage.
    public string? Improve(GearItem i)
    {
        if (CannotImprove(i) is string why) return why;
        var g = Gear.Get(i.Def);
        var (crowns, goods, lots) = ImproveCost(g, i.Quality);
        S.Gold -= crowns;
        if (lots > 0) { S.Goods[goods] -= lots; if (S.Goods[goods] <= 0) S.Goods.Remove(goods); }
        i.Quality++;
        if (!Captain.Baggage.Contains(i)) Captain.Baggage.Add(i);
        Log($"The smith makes your {g.Name.ToLowerInvariant()} {QualityName(i.Quality)}.");
        AudioEngine.Current?.Play(Cue.AnvilRing, 0.8f);
        return null;
    }

    /// Why the smith will not strike this mark on this piece, or null: fine pieces and up take a mark, one at a time here.
    public string? CannotRemark(GearItem i, string mark)
    {
        var g = Gear.Get(i.Def);
        if (Smithy() == null) return "NO SMITH HERE";
        if (i.Quality < ItemQuality.Fine) return "ONLY A FINE PIECE TAKES A MARK";
        if (!MarksFor(g.Slot).Any(e => e.Id == mark)) return "THAT MARK DOES NOT FIT IT";
        if (i.Marks.Contains(mark)) return "IT BEARS THAT MARK";
        int cost = RemarkCost(g);
        return S.Gold < cost ? $"NEEDS {cost} CROWNS" : null;
    }

    /// Strike a mark: it replaces the piece's first mark (a masterwork or named piece may carry two).
    public string? Remark(GearItem i, string mark)
    {
        if (CannotRemark(i, mark) is string why) return why;
        var g = Gear.Get(i.Def);
        S.Gold -= RemarkCost(g);
        int most = i.Quality >= ItemQuality.Masterwork ? MostMarks : 1;
        if (i.Marks.Count >= most) i.Marks.RemoveAt(0);
        i.Marks.Add(mark);
        Log($"The smith strikes a new mark on your {ItemName(i, g).ToLowerInvariant()}: {Effects.Get(mark).Name.ToLowerInvariant()}.");
        AudioEngine.Current?.Play(Cue.AnvilRing, 0.6f);
        return null;
    }

    /// The piece a smith works on: the one in the baggage, or a new plain thing for a free starting piece.
    public GearItem SmithItem(string def) => ItemOf(Captain, def) ?? new GearItem { Def = def };

    /// You fell in the battle: one piece you wore (not a horse, not a worn-out one) may be battered, half the time.
    void BatterOne()
    {
        var pool = Captain.Kit.Worn.Where(id => Gear.Find(id) is { Slot: not "horse" }).Select(id => SmithItem(id)).Where(i => !i.Battered && i.Quality > ItemQuality.Worn).ToList();
        if (pool.Count == 0 || !rng.Chance(0.5f)) return;
        var hit = rng.Pick(pool);
        hit.Battered = true;
        if (!Captain.Baggage.Contains(hit)) Captain.Baggage.Add(hit);
        Log($"Your {ItemName(hit, Gear.Get(hit.Def)).ToLowerInvariant()} is battered in your fall. A smith can mend it.");
    }
}
