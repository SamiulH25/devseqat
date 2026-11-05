using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap A5: makes the campaign's progression legible. One place decides what the captain should do next, so the map
/// HUD, the party screen and the tips all say the same thing, and a new player never has to guess what the game wants.
public sealed partial class Campaign
{
    /// How far the company has come, in the words the game uses everywhere: raw levies, a paid band, a lord's host.
    public string Stage => Captain.Renown switch
    {
        < 80 => "MERCENARY BAND",
        < 200 => "SWORN COMPANY",
        < 400 => "BATTLE-HARDENED",
        < 700 => "FEARED CAPTAIN",
        _ => "LORD OF WAR",
    };

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
        if (Rules.PerkPoints(cap) > 0) return ("A PERK IS READY: TAKE IT IN YOUR PARTY (P)", "PARTY");
        if (Player.Companies.FirstOrDefault(Rules.UpgradeReady) is CompanyRecord ready)
        {
            int cost = Rules.UpgradeCost(ready, ready.Type.UpgradesTo[0], Player.Horses);
            return (InTown(Player)
                ? ($"{ready.Name.ToUpperInvariant()} IS READY: TRAIN IT HERE ({cost} CROWNS)", "TRAIN")
                : ($"{ready.Name.ToUpperInvariant()} IS READY: TAKE IT TO A TOWN ({cost} CROWNS)", "TRAIN"));
        }
        // 3. Room in the party for more men or another company (up to 10 companies).
        if (RoomForMen >= 8 && Player.Companies.Count < Slots && InTown(Player))
            return ("YOU HAVE ROOM FOR MORE MEN: HIRE MERCENARIES OR RECRUIT IN A VILLAGE", "MERCENARIES");
        // 4. Nothing to spend XP on yet: point at the work that earns it.
        if (S.Contracts.Count == 0)
            return cap.Contract >= 0 ? ("NO CONTRACT IN HAND: SEE A TOWN'S STEWARD", "CONTRACT") : ("SIGN WITH A FACTION FOR PAY AND WORK", "CONTRACT");
        if (Player.Wounded > Player.Men / 2) return ("HALF YOUR MEN ARE WOUNDED: REST IN A TOWN", "REST");
        // 5. Steady state: the next rung of renown.
        var (what, at) = Rules.NextRenownUnlock(cap);
        return ($"FIGHT ON: {at - cap.Renown:0} MORE RENOWN BUYS {what}", "PARTY");
    }
}
