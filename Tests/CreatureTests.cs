using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of creatures and monsters (docs/CREATURES.md): goblin holdouts on the map.
public static partial class SelfTest
{
    /// Goblin holdouts: raised in the wilds away from settlements, hostile to all men, they grow, raid and plunder
    /// villages, and burning one out pays its hoard; the save keeps them.
    /// Debug: holdouts day by day on a fresh map (`--selftest HoldoutTrace`).
    static void HoldoutTrace()
    {
        var c = Campaign.New(61);
        Console.WriteLine($"start {c.Holdouts.Count()} of {c.HoldoutsWanted}, men {string.Join(",", c.Holdouts.Select(h => h.Men))}");
        for (int d = 0; d < 40; d++)
        {
            Days(c, 1);
            Console.WriteLine($"day {c.Day}: {c.Holdouts.Count()} holdouts men {string.Join(",", c.Holdouts.Select(h => h.Men))}; bands {c.S.Parties.Count(p => p.Kind == PartyKind.Monster && !p.Lair)}");
        }
        foreach (var l in c.S.Log.Where(l => l.Text.Contains("burn") || l.Text.Contains("overrun") || l.Text.Contains("oblin"))) Console.WriteLine(l.Text);
    }

    static void Holdouts()
    {
        var c = Campaign.New(61);
        var lairs = c.Holdouts.ToList();
        Check(lairs.Count >= 1, "the world starts with goblin holdouts");
        Check(Troops.Get("goblin").Wild && Troops.Get("goblin").Breed == "goblin", "goblins are wild troops");
        foreach (var h in lairs)
        {
            Check(c.Map.ProvinceAt(h.Pos).Kind is "Forest" or "Hills", $"{h.Name} is in forest or hill country");
            Check(c.S.Settlements.All(s => Vector2.Distance(s.Pos, h.Pos) >= Balance.Get().HoldoutClearance * c.Map.Spread - 1e-3f), $"{h.Name} keeps away from settlements");
            Check(h.Kind == PartyKind.Monster && h.Breed == "goblin" && h.Men > 0, $"{h.Name} is held by goblins");
        }
        var lord = c.S.Parties.First(p => p.Kind == PartyKind.Lord);
        var band = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        var lair = lairs.OrderBy(h => c.S.Settlements.Where(v => v.Kind == SettlementKind.Village).Min(v => Vector2.Distance(v.Pos, h.Pos))).First();
        Check(c.Hostile(lord, lair) && c.Hostile(band, lair) && c.Hostile(c.Player, lair), "goblins are hostile to lords, brigands and you");
        Check(c.ParleyPrice(lair) < 0, "goblins cannot be bought off");

        // Raids: a full holdout sends a band out of its own men at the nearest village; the band plunders it.
        foreach (var co in lair.Companies) co.Stack(co.Troop).Men = 0;
        lair.Companies.Add(c.NewCompany("goblin", "infantry", c.Scaled(Balance.Get().HoldoutMaxMen), 0));
        int before = lair.Men;
        lair.NextRaid = 0;
        c.SendRaiders(lair);
        var raiders = c.S.Parties.FirstOrDefault(p => p.LairId == lair.Id);
        Check(raiders != null && raiders.Task == "raid" && raiders.RaidOn >= 0, "a full holdout sends out raiders");
        if (raiders == null) return;
        Check(lair.Men + raiders.Men == before && lair.Men > 0, "raiders leave from the holdout's own men");
        var village = c.SettlementById(raiders.RaidOn);
        float prosperity = village.Prosperity;
        raiders.Pos = village.Pos;
        c.MonsterThink(raiders);
        Check(village.ThreatUntil > c.S.Hours && village.Recruits == 0 && village.Prosperity < prosperity, "raiders plunder the village");
        Check(raiders.Task == "home" && raiders.Gold > 0, "and carry the loot home");

        // A save with goblins in it loads the same.
        string json = c.S.Serialize();
        var loaded = CampaignState.Deserialize(json);
        Check(loaded.Serialize() == json && loaded.Parties.Any(p => p.Lair && p.Breed == "goblin"), "a save keeps its holdouts");

        // Burning one out: you take the hoard and a bounty, and its raiders roam leaderless.
        foreach (var co in lair.Companies) co.Stack(co.Troop).Men = 0;
        lair.Companies.RemoveAll(co => co.Men == 0);
        lair.Companies.Add(c.NewCompany("goblin", "infantry", 1, 0));
        lair.Gold = 500;
        lair.Pos = c.Player.Pos;
        c.Encounter = lair;
        float gold = c.S.Gold;
        var report = c.AutoResolveEncounter();
        Check(report.Won, "your company beats a single goblin");
        Check(c.PartyById(lair.Id) == null, "a beaten holdout is gone");
        Check(c.S.Gold >= gold + 500 && report.Loot >= 500 && report.Bounty >= Balance.Get().BountyHoldout, "you take its hoard and a bounty");
        Check(raiders.LairId < 0, "its raiders no longer have a home");
        Check(!c.Player.Prisoners.Any(g => Troops.Get(g.Troop).Wild), "goblins are not taken for ransom");

        // Left alone, the wilds keep their goblins: holdouts are replaced and still raid.
        Days(c, 60);
        int holdouts = c.Holdouts.Count(), bands = c.S.Parties.Count(p => p.Kind == PartyKind.Monster && !p.Lair && !p.Down);
        int plundered = c.S.Settlements.Count(v => v.Kind == SettlementKind.Village && v.ThreatUntil > c.S.Hours);
        Console.WriteLine($"holdouts: {holdouts} of {c.HoldoutsWanted} wanted, {bands} goblin bands out, {plundered} villages under threat, "
            + $"largest {c.Holdouts.Select(h => h.Men).DefaultIfEmpty(0).Max()} goblins, biggest hoard {c.Holdouts.Select(h => h.Gold).DefaultIfEmpty(0).Max():0}");
        Check(holdouts >= 1, "holdouts come back after being cleared");
        Check(c.S.Parties.Where(p => p.Kind == PartyKind.Monster).All(p => p.Faction < 0 && p.Companies.All(co => co.Stacks.All(st => st.Type.Wild))), "goblin parties hold only goblins");
    }
}
