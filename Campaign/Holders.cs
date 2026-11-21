using Microsoft.Xna.Framework;

namespace Ironfield;

/// Villages hold castles together (docs/archive/HOLDS.md HD3). Each village has one holder (`Settlement.Holder`): the castle or town
/// whose demesne it is, chosen once. Its men fill the holder's muster, its harvest the granary, its foals the stables, and
/// stripped or burnt villages starve all three, so a siege can begin by raiding round a castle. The castle gives back: its
/// garrison rides out at brigands near its villages, and villages held in peace keep the seat's unrest down.
public sealed partial class Campaign
{
    /// Bind every village without a holder to the nearest castle or town in its province, or where its province has none,
    /// the nearest of its realm (once; kept in the save).
    void BindVillages()
    {
        var seats = S.Settlements.Where(t => t.Kind != SettlementKind.Village).ToList();
        foreach (var v in S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.Holder < 0))
            v.Holder = (seats.Where(t => t.Province == v.Province).MinBy(t => Vector2.DistanceSquared(t.Pos, v.Pos))
                ?? seats.Where(t => t.Faction == v.Faction).MinBy(t => Vector2.DistanceSquared(t.Pos, v.Pos))
                ?? seats.MinBy(t => Vector2.DistanceSquared(t.Pos, v.Pos)))?.Id ?? -1;
    }

    /// The villages of a seat's demesne that are not stripped or burnt.
    IEnumerable<Settlement> HeldVillages(Settlement seat) => Demesne(seat).Where(v => v.ThreatUntil <= S.Hours);

    /// How much of a seat's demesne is whole (1 with no villages): it scales the castle's garrison refill and its foals.
    public float HeldShare(Settlement seat)
    {
        int all = Demesne(seat).Count();
        return all == 0 ? 1 : HeldVillages(seat).Count() / (float)all;
    }

    /// Days of stores a castle's villages bring in a week: each whole village by its prosperity.
    public float VillageFood(Settlement castle) => HeldVillages(castle).Sum(v => (0.5f + v.Prosperity / 100f) * B.VillageFood);

    /// Daily: a castle's garrison rides out at brigands and beasts near its villages when it clearly outnumbers them.
    internal void CastellansDaily()
    {
        float reach = B.SallyReach * Map.Spread;
        var bands = S.Parties.Where(p => !p.Down && !p.Lair && p.CharId < 0 && p.Kind is PartyKind.Bandit or PartyKind.Monster && p.Breed is not ("troll" or "wyvern" or "undead")).ToList();
        if (bands.Count == 0) return;
        var demesnes = S.Settlements.Where(v => v.Kind == SettlementKind.Village).GroupBy(v => SeatOf(v).Id).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var castle in S.Settlements.Where(c => c.Kind == SettlementKind.Castle && !c.Besieged && c.Garrison.Count > 0))
        {
            if (!demesnes.TryGetValue(castle.Id, out var villages)) continue;
            var near = bands.Where(p => !p.Down && villages.Any(v => Vector2.DistanceSquared(v.Pos, p.Pos) < reach * reach)).ToList();
            if (near.Count == 0) continue;
            var g = GarrisonParty(castle);
            float mine = Rules.Strength(g);
            var band = near.Where(p => Rules.Strength(p) * B.SallyOdds < mine).OrderBy(p => Vector2.Distance(p.Pos, castle.Pos)).FirstOrDefault();
            if (band == null) continue;
            var r = Rules.AutoResolve(new List<Party> { g }, new List<Party> { band }, rng, 1, Edge(band));
            Apply(r, new List<Party> { g }, new List<Party> { band }, "");
            AiBattles++;
            if (InSight(band.Pos) || castle.Owner == Player.Id)
                LogAll(r.Winner == 0 ? $"The garrison of {castle.Name} rides out and scatters {band.Name}." : $"{band.Name} throw back a sally from {castle.Name}.");
        }
    }
}
