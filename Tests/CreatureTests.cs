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

    /// Goblins in battle: a loose mob that breaks early and comes back, until its boss falls.
    static void GoblinBattle()
    {
        var b = Battle.Empty(71);
        var mob = b.AddSquad(1, Troops.Get("goblin"), 40, new(0, 30), 10, -Vector2.UnitY);
        var boss = b.AddSquad(1, Troops.Get("goblin_boss"), 1, new(0, 36), 1, -Vector2.UnitY).Units[0];
        var men = b.AddSquad(0, UnitType.Shieldman, 20, new(0, -10), 10, Vector2.UnitY);
        Check(mob.Swarm && mob.Formation == Formation.Loose && !men.Swarm, "goblins come on as a loose mob");
        Check(Troops.Get("goblin").Size < 1 && Troops.Get("goblin_boss").Size > 1 && Troops.Get("goblin").Look.HeadKey == "head.goblin", "goblins are small, the boss big, with goblin heads");

        // A goblin breaks where a man stands.
        var g = mob.Units[0];
        var m = men.Units[0];
        g.Morale = m.Morale = Battle.RoutAt + Battle.CravenRout - 4;
        Run(b, 0.1f);
        Check(g.Routing && !m.Routing, "a goblin breaks at a scare a man shrugs off");

        // ...and comes back while its boss lives.
        g.Pos = new Vector2(0, 90);
        g.Morale = Battle.RallyAt + 5;
        g.Routs = 3;
        Run(b, 0.1f);
        Check(!g.Routing, "a goblin rallies again and again while its boss lives");

        // The boss falls: the mob loses heart and stops rallying.
        float before = mob.Units.Where(u => u.Active).Average(u => u.Morale);
        boss.Action = UnitAction.Stagger; // caught off guard: no block
        b.Hit(m, boss, 9999, false);
        Check(boss.Dead && b.BossDown[1], "the boss can be slain");
        Check(mob.Units.Where(u => u.Active).Average(u => u.Morale) < before - 20, "his fall shakes every goblin");
        g.Routing = true; g.Morale = Battle.RallyAt + 5;
        Run(b, 0.1f);
        Check(g.Routing, "with the boss dead, goblins no longer rally");

        // A whole fight: men against twice as many goblins.
        var f = Battle.Empty(72);
        var horde = f.AddSquad(1, Troops.Get("goblin"), 60, new(0, 25), 12, -Vector2.UnitY);
        f.AddSquad(1, Troops.Get("goblin_boss"), 1, new(0, 30), 1, -Vector2.UnitY);
        f.AddSquad(0, UnitType.Shieldman, 30, new(0, -5), 10, Vector2.UnitY).Order = Order.Charge;
        for (int i = 0; i < 60 * 240 && f.Winner < 0; i++) f.Update(1 / 60f);
        int back = horde.Units.Count(u => u.Routs >= 2);
        Console.WriteLine($"goblins: 30 shieldmen vs 60 goblins and a boss: winner {f.Winner} at {f.Time:0} s, {horde.Units.Count(u => u.Dead)} goblins dead, {back} broke and came back, boss down {f.BossDown[1]}");
        Check(back > 0, "goblins break and come back during a fight");
    }
}
