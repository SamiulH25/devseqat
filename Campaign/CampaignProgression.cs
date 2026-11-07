using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap A5: makes the campaign's progression legible. One place decides what the captain should do next, so the map
/// HUD, the party screen and the tips all say the same thing, and a new player never has to guess what the game wants.
public sealed partial class Campaign
{
    /// How far the captain has come: the career rank (F5), in the words the game uses everywhere.
    public string Stage => RankName(Rank);

    /// The one thing the game most wants you to look at now, in priority order: something is broken, then something is
    /// ready and free, then something is ready and needs a town or gold, then the long game (renown and contracts).
    /// Returns a short line and where it leads, so the HUD and the party screen can point at the same place.
    public (string Text, string Where) NextStep()
    {
        var cap = Captain;
        // 1. The army is falling apart: unpaid men, no food, wounded captain.
        if (S.UnpaidWeeks > 0) return ("YOUR MEN GO UNPAID: SOME WILL DESERT", "MARKET");
        if (Player.Food <= 0) return ("YOUR MEN ARE STARVING: BUY FOOD", "FOOD");
        if (S.Hours < cap.WoundedUntil) return ("YOU ARE WOUNDED: LET IT PASS OR REST IN A TOWN", "REST");
        // 2. A perk or a company upgrade is ready: the core of the game's progress.
        if (Skills.PicksTotal(cap) > 0) return ("A PERK IS READY: CHOOSE IT IN YOUR SKILLS (K)", "SKILLS");
        if (MostReady() is var (rc, rs))
        {
            int n = rs.Ready;
            string men = n == 1 ? "1 MAN" : $"{n} MEN";
            string ready = $"{men} OF {rc.Name.ToUpperInvariant()} READY ({rs.Type.Name.ToUpperInvariant()})";
            return (CanTrainHere(Player) ? ($"{ready}: PROMOTE THEM HERE", "TRAIN") : ($"{ready}: TAKE THEM TO A TOWN", "TRAIN"));
        }
        // 3. Room in the party for more men or another company (up to 10 companies).
        if (RoomForMen >= 8 && Player.Companies.Count < Slots && InTown(Player))
            return ("YOU HAVE ROOM FOR MORE MEN: HIRE MERCENARIES OR RECRUIT IN A VILLAGE", "MERCENARIES");
        // 4. Nothing to spend XP on yet: point at the work that earns it.
        if (S.Contracts.Count == 0)
            return cap.Contract >= 0 ? ("NO CONTRACT IN HAND: SEE A TOWN'S STEWARD", "CONTRACT") : ("SIGN WITH A FACTION FOR PAY AND WORK", "CONTRACT");
        if (Player.Wounded > Player.Men / 2) return ("HALF YOUR MEN ARE WOUNDED: REST IN A TOWN", "REST");
        // 5. The career (F5): an oath or a crown within reach, else the next rank's road, else the next rung of renown.
        if (CanSwearFealty(out _)) return ("YOU MAY SWEAR FEALTY AND HOLD LAND: SEE YOUR REALM (R)", "REALM");
        if (CanDeclareIndependence(out _) && !S.Won) return ("YOU COULD RULE IN YOUR OWN NAME: SEE YOUR REALM (R)", "REALM");
        if (NextRank() is var (rank, needs)) return (rank == Rank ? needs : $"{RankName(rank)}: {needs}", "REALM");
        var (what, at) = Rules.NextRenownUnlock(cap);
        return ($"FIGHT ON: {at - cap.Renown:0} MORE RENOWN BUYS {what}", "PARTY");
    }

    /// Promotions the party could make if it were in a town: every ready stack, with its cheapest branch.
    public IEnumerable<(CompanyRecord Company, TroopStack Stack, string To)> PromotionChoices() =>
        Player.Companies.SelectMany(co => co.Stacks.Where(s => s.Ready > 0).Select(s => (co, s,
            s.Type.UpgradesTo.OrderBy(t => Rules.PromoteCost(s.Troop, t, Player.Horses > 0)).First())));

    /// How many of a stack's ready men could be promoted with `gold`, setting aside where you are.
    public int PromotableAnywhere(CompanyRecord co, TroopStack s, string to, float gold, out int cost)
    {
        var p = Player;
        // Promotable checks the place through CanTrainHere; for a dry run from the field count as if in a town.
        int n = Promotable(p, co, s, to, s.Ready, gold, out cost, out _, ignorePlace: true);
        return n;
    }

    /// A campaign-seeded random number in [0, max), for headless helpers that must stay deterministic.
    public float Rand(float max) => rng.Float() * max;
}
