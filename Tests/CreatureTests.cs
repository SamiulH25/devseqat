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
        // Nobody else about: a band near a stronger party runs, and near a weaker one hunts it instead.
        foreach (var q in c.S.Parties.Where(q => q != raiders && Vector2.Distance(q.Pos, village.Pos) < 30 * c.Map.Spread)) q.Pos += new Vector2(60, 60) * c.Map.Spread;
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
        Check(!c.Player.Prisoners.Any(g => Troops.Get(g.Troop).Wild && Troops.Get(g.Troop).Breed != "goblin"), "of the creatures, only goblins are taken alive");

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

    /// Wolves: packs keep to the forests, outrun men on foot, and in battle circle to the flank; the pack breaks when
    /// its dire wolf falls.
    /// Debug: a pack against shieldmen, second by second (`--selftest WolfTrace`).
    static void WolfTrace()
    {
        foreach (var foe in new[] { "shieldmen", "levies", "bowmen" })
        {
            var f = Battle.Empty(83);
            var wolves = f.AddSquad(1, Troops.Get("wolf"), 16, new(0, 25), 8, -Vector2.UnitY);
            f.AddSquad(1, Troops.Get("dire_wolf"), 1, new(0, 29), 1, -Vector2.UnitY);
            var men = f.AddSquad(0, Troops.Get(foe), 20, new(0, -5), 10, Vector2.UnitY);
            wolves.Order = Order.Charge;
            int hits = 0;
            for (int sec = 1; sec <= 30; sec++)
            {
                for (int i = 0; i < 60; i++) { f.Update(1 / 60f); }
                if (sec % 5 == 0) Console.WriteLine($"{foe} {sec}s: men {men.Units.Count(u => u.Active)} hp {men.Units.Where(u => u.Active).Sum(u => u.Hp):0}, wolves {wolves.Units.Count(u => u.Active)} morale {wolves.Units.Where(u => u.Active).Select(u => u.Morale).DefaultIfEmpty(0).Average():0} staggered {wolves.Units.Count(u => u.Action == UnitAction.Stagger)}");
            }
        }
    }

    static void Wolves()
    {
        var c = Campaign.New(81);
        var packs = c.WolfPacks.ToList();
        Check(packs.Count == c.WolfPacksWanted && packs.Count > 0, $"the forests have their wolf packs ({packs.Count})");
        foreach (var w in packs)
        {
            Check(c.Map.ProvinceAt(w.Pos).Kind == "Forest", $"{w.Name} lives in a forest");
            Check(w.Companies.Any(co => co.Troop == "dire_wolf"), $"{w.Name} is led by a dire wolf");
        }
        var pack = packs[0];
        Check(c.Speed(pack) > c.Speed(c.Player), "wolves outrun a company on foot");
        pack.Pos = c.Player.Pos;
        c.Encounter = pack;
        Check(!c.CanRetreat(out string why) && why.Contains("WOLVES"), "you cannot run from wolves on foot");
        c.Encounter = null;
        Check(Troops.Get("wolf").Look.Beast && Troops.Get("dire_wolf").Has("boss"), "a wolf is a beast, its dire wolf the pack's boss");

        // A wolf goes round to a man's flank before it bites.
        var b = Battle.Empty(82);
        var man = b.AddSquad(0, UnitType.Shieldman, 1, new(0, 0), 1, Vector2.UnitY).Units[0];
        var wolf = b.AddSquad(1, Troops.Get("wolf"), 1, new(0.3f, 9), 1, -Vector2.UnitY).Units[0];
        man.Face(Vector2.UnitY);
        wolf.Squad!.Order = Order.Charge;
        float angle = -1, t = 0;
        for (int i = 0; i < 60 * 8 && angle < 0 && !wolf.Dead && !man.Dead; i++)
        {
            man.Face(Vector2.UnitY); man.Wish = Vector2.Zero;
            b.Update(1 / 60f);
            if (wolf.Action == UnitAction.Windup) { angle = MathHelper.ToDegrees(MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(wolf.Pos - man.Pos), Vector2.UnitY), -1, 1))); t = b.Time; }
        }
        Console.WriteLine($"wolves: a wolf charging a man head-on first bites at {t:0.0} s, {angle:0} degrees off his front");
        Check(angle > 60, "a wolf bites a man from the flank, not his face");

        // A pack tears through archers but breaks on a shield line; its alpha's fall breaks its heart.
        int Fight(string foe, out Battle f, out Squad wolves, out Unit alpha)
        {
            f = Battle.Empty(83);
            wolves = f.AddSquad(1, Troops.Get("wolf"), 16, new(0, 25), 8, -Vector2.UnitY);
            alpha = f.AddSquad(1, Troops.Get("dire_wolf"), 1, new(0, 29), 1, -Vector2.UnitY).Units[0];
            var men = f.AddSquad(0, Troops.Get(foe), 20, new(0, -5), 10, Vector2.UnitY);
            wolves.Order = Order.Charge;
            Check(wolves.Swarm && wolves.Formation == Formation.Loose, "a pack runs loose");
            return men.Units.Count;
        }
        Fight("bowmen", out var fa, out var pa, out _);
        for (int i = 0; i < 60 * 30; i++) fa.Update(1 / 60f);
        int archersLeft = fa.SquadsOf(0).Sum(q => q.Units.Count(u => u.Active && !u.Routing && !u.IsLeader));
        Fight("shieldmen", out var fs, out var ps, out var alpha);
        for (int i = 0; i < 60 * 30; i++) fs.Update(1 / 60f);
        int shieldsLeft = fs.SquadsOf(0).Sum(q => q.Units.Count(u => u.Active && !u.Routing && !u.IsLeader));
        Console.WriteLine($"wolves: 16 wolves and an alpha leave {archersLeft} of 20 bowmen and {shieldsLeft} of 20 shieldmen standing after 30 s");
        Check(archersLeft < 10, "a pack tears through archers");
        Check(shieldsLeft > 15 && ps.Units.Count(u => u.Active) < 4, "a pack breaks on a shield line");

        Fight("levies", out var fl, out var pl, out var alpha2);
        for (int i = 0; i < 60 * 3; i++) fl.Update(1 / 60f);
        float before = pl.Units.Where(u => u.Active).Average(u => u.Morale);
        alpha2.Action = UnitAction.Stagger;
        fl.Hit(fl.SquadsOf(0).First().Units[0], alpha2, 9999, false);
        Check(alpha2.Dead && fl.BossDown[1], "the dire wolf can fall");
        Check(pl.Units.Where(u => u.Active).Average(u => u.Morale) < before - 20, "the pack loses heart when its alpha falls");
    }

    /// Goblin wolf riders: cavalry on wargs. A warg whose rider falls runs off: no loose horse, no horse taken.
    static void WolfRiders()
    {
        var rider = Troops.Get("goblin_wolf_rider");
        Check(rider.Class == UnitClass.Cavalry && rider.Mount is { Barding: "wolf" } && rider.Has("craven"), "wolf riders are craven goblins on wargs");
        var b = Battle.Empty(91);
        var foe = b.AddSquad(0, UnitType.Shieldman, 1, new(0, 0), 1, Vector2.UnitY).Units[0];
        var u = b.AddSquad(1, rider, 1, new(0, 3), 1, -Vector2.UnitY).Units[0];
        Check(u.Mounted && u.Horse!.Barding == "wolf", "a wolf rider rides");
        u.Action = UnitAction.Stagger;
        b.Hit(foe, u, 9999, false);
        Check(u.Dead && b.LooseHorses.Count == 0 && foe.Squad!.HorseKills == 0, "his warg runs off: nothing to catch or count");
        var f = Battle.Empty(92);
        var riders = f.AddSquad(1, rider, 12, new(0, 30), 6, -Vector2.UnitY);
        f.AddSquad(0, Troops.Get("bowmen"), 20, new(0, -5), 10, Vector2.UnitY);
        riders.Order = Order.Charge;
        for (int i = 0; i < 60 * 40 && f.Winner < 0; i++) f.Update(1 / 60f);
        Console.WriteLine($"wolf riders: 12 riders vs 20 bowmen after {f.Time:0} s: {f.SquadsOf(0).Sum(q => q.Units.Count(x => x.Dead))} archers dead, {riders.Units.Count(x => x.Dead)} riders dead");
        Check(f.SquadsOf(0).Sum(q => q.Units.Count(x => x.Dead)) > 0, "wolf riders ride down archers");
    }

    /// Trolls in battle: a huge body whose blows sweep and throw men, that heals in the dark, never breaks,
    /// unnerves men near it, stands off horses, and goes slow and weak in sunlight.
    static void Trolls()
    {
        var tt = Troops.Get("troll");
        Check(tt.Huge && tt.Wild && tt.Breed == "troll" && tt.Look.HeadKey == "head.troll" && tt.Has("fearless"), "a troll is a huge wild creature with a troll's head");

        // It heals when left alone, but not under the sun.
        foreach (float hour in new[] { 23f, 13f })
        {
            var h = Battle.Empty(92);
            h.Options.Weather = new Weather(hour);
            var tr = h.AddSquad(1, tt, 1, new(0, 40), 1, -Vector2.UnitY).Units[0];
            h.AddSquad(0, UnitType.Shieldman, 1, new(0, -40), 1, Vector2.UnitY);
            tr.Squad!.Order = Order.Hold;
            tr.Hp = tt.MaxHp / 2;
            Run(h, 6);
            if (hour > 20) Check(tr.Hp > tt.MaxHp * 0.6f, $"a troll heals in the dark ({tr.Hp:0})");
            else Check(tr.Hp <= tt.MaxHp / 2 + 1, "a troll does not heal by day");
        }

        // One blow sweeps through the men beside its target and throws them back.
        var b = Battle.Empty(93);
        b.Options.Weather = new Weather(23);
        var troll = b.AddSquad(1, tt, 1, new(0, 1.9f), 1, -Vector2.UnitY).Units[0];
        var line = b.AddSquad(0, UnitType.Shieldman, 3, new(0, 0), 3, Vector2.UnitY);
        line.Order = Order.Hold;
        var start = line.Units.Select(u => u.Pos).ToList();
        for (int i = 0; i < 60 * 4 && troll.Action != UnitAction.Recover; i++) b.Update(1 / 60f);
        int hurt = line.Units.Count(u => u.Hp < u.Type.MaxHp || u.Dead);
        float thrown = line.Units.Select((u, k) => Vector2.Distance(u.Pos, start[k])).Max();
        Console.WriteLine($"trolls: one blow hurts {hurt} of 3 men, throws one {thrown:0.0} m");
        Check(hurt >= 2, "a troll's blow hits more than one man");
        Check(thrown > 1, "a troll's blow throws men back");
        troll.Morale = 0;
        Run(b, 0.2f);
        Check(!troll.Routing, "a troll never breaks");

        // A horse that rides into a troll comes off worse.
        var c = Battle.Empty(94);
        c.Options.Weather = new Weather(23);
        var big = c.AddSquad(1, tt, 1, new(0, 0), 1, -Vector2.UnitY).Units[0];
        big.Squad!.Order = Order.Hold;
        var rider = c.AddSquad(0, UnitType.Lancer, 1, new(0, -30), 1, Vector2.UnitY).Units[0];
        rider.Squad!.Order = Order.Charge;
        float horse = rider.HorseHp;
        for (int i = 0; i < 60 * 8 && !(rider.HorseHp < horse); i++) c.Update(1 / 60f);
        Check(rider.HorseHp < horse && big.Action != UnitAction.Stagger, "a charging horse cannot trample a troll");

        // At night a troll breaks a company; at midday the same company kills it.
        bool Beats(float hour, out Battle f, out Squad men)
        {
            f = Battle.Empty(91);
            f.Options.Weather = new Weather(hour);
            var tr = f.AddSquad(1, tt, 1, new(0, 20), 1, -Vector2.UnitY).Units[0];
            men = f.AddSquad(0, UnitType.Shieldman, 30, new(0, -5), 10, Vector2.UnitY);
            men.Order = Order.Charge;
            for (int i = 0; i < 60 * 180 && !tr.Dead && (i == 0 || men.Alive > 0); i++) f.Update(1 / 60f);
            return !tr.Dead;
        }
        bool night = Beats(23, out var fn, out var mn), day = Beats(13, out var fd, out _);
        Console.WriteLine($"trolls: a troll against 30 shieldmen wins by night {night} ({mn.Units.Count(u => u.Routing)} routed), by day {day}");
        Check(night && !day, "a troll is terrible by night and beatable by day");
        Check(Rules.Strength(tt, 1) > Rules.Strength(UnitType.Shieldman, 20), "auto-resolve weighs a troll as many men");
    }

    /// Trolls on the map: a few rare monsters that sleep by day and hunt by night; more wake as the years pass.
    static void TrollsAbroad()
    {
        var c = Campaign.New(95);
        var trolls = c.Rares.ToList();
        Check(trolls.Count == 2 && c.RaresWanted == 2, $"the world starts with two trolls ({trolls.Count})");
        foreach (var t in trolls)
            Check(c.Map.ProvinceAt(t.Pos).Kind is "Forest" or "Hills" && t.Companies.All(co => co.Troop == "troll"), $"{t.Name} lives in the wilds");
        var troll = trolls[0];
        c.Player.Pos = troll.Pos + new Vector2(4, 0) * c.Map.Spread;
        c.S.Hours = Math.Floor(c.S.Hours / 24) * 24 + 13;
        c.MonsterThink(troll);
        Check(!troll.Moving && troll.Task == "rest", "a troll sleeps by day");
        c.S.Hours += 10; // 23h
        foreach (var co in c.Player.Companies) foreach (var st in co.Stacks) st.Men = Math.Min(st.Men, 3); // easy prey
        c.MonsterThink(troll);
        Check(troll.ChaseId == c.Player.Id, "and hunts easy prey by night");

        // More wake over the years, up to the cap.
        c.S.Hours += 24 * 120;
        Check(c.RaresWanted == 4, $"one more every 60 days ({c.RaresWanted})");
        c.S.Hours += 24 * 1000;
        Check(c.RaresWanted == 6, "at most six");
        var d = Campaign.New(96);
        Days(d, 120);
        Console.WriteLine($"trolls: after 120 days {d.Rares.Count()} trolls abroad (wanted {d.RaresWanted}): {string.Join(", ", d.Rares.Select(r => $"{r.Name} {r.Men}"))}");
        Check(d.Rares.Count() >= 2 && d.Rares.Count() <= d.RaresWanted, "the world keeps its rare monsters");
    }

    /// Creatures for you (phase 4): goblins taken from a burnt-out holdout work as thralls; a kennel in your fief breeds
    /// war hounds.
    static void PlayerCreatures()
    {
        var c = Campaign.New(97);
        c.S.Gold = 5000;
        foreach (var co in c.Player.Companies) foreach (var st in co.Stacks) st.Men = 0;
        c.Player.Companies.Clear();
        c.Player.Companies.Add(c.NewCompany("ironguard", "infantry", c.Scaled(30), 0));
        var lair = c.Holdouts.First();
        foreach (var co in lair.Companies) foreach (var st in co.Stacks) st.Men = 0;
        lair.Companies.Clear();
        lair.Companies.Add(c.NewCompany("goblin", "infantry", c.Scaled(30), 0));
        lair.Pos = c.Player.Pos;
        c.Encounter = lair;
        var report = c.AutoResolveEncounter();
        var caught = c.Player.Prisoners.FirstOrDefault(g => g.Troop == "goblin");
        Console.WriteLine($"creatures: burning out a holdout of {c.Scaled(30)} goblins takes {caught?.Count ?? 0} alive");
        Check(report.Won && caught is { Count: > 0 }, "goblins are taken alive from a burnt-out holdout");
        Check(c.RecruitPrisoners(caught!) == null, "captured goblins can be put to work");
        var thralls = c.Player.Companies.Last();
        Check(thralls.Name == "Goblin Thralls" && thralls.Troop == "goblin" && thralls.Cohesion == 0 && thralls.Type.Has("craven"), "as a company of goblin thralls, cowardly and loveless");

        // A kennel in your fief breeds hounds that join you as a pack.
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town);
        var other = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s != town);
        Check(c.RaiseHounds(town, 3) != null, "no kennel, no hounds");
        town.Owner = c.Player.Id;
        town.Buildings["kennel"] = 1;
        other.Buildings["kennel"] = 1;
        Days(c, 7);
        Check(town.Hounds > 0, $"a kennel breeds hounds ({town.Hounds})");
        Check(c.RaiseHounds(other, 3) != null, "only at a kennel of yours");
        int companies = c.Player.Companies.Count;
        Check(c.RaiseHounds(town, 3) == null, "you raise hounds at your kennel");
        var pack = c.Player.Companies.First(co => co.Troop == "war_hounds");
        Check(c.Player.Companies.Count == companies + 1 && pack.Men == 3 && pack.Name == "War Hounds", "a new pack of war hounds");
        Check(c.RaiseHounds(town, 2) == null && pack.Men == 5 && c.Player.Companies.Count == companies + 1, "more hounds join the pack you have");
        var hound = Troops.Get("war_hounds");
        Check(hound.Wild && hound.Has("pack") && hound.Look.Beast && hound.UpgradesTo.Length == 0, "a war hound hunts like a wolf and never becomes a man");
    }

    /// Debug: one troll against companies of shieldmen by night and by day (`--selftest TrollTrace`).
    static void TrollTrace()
    {
        foreach (float hour in new[] { 23f, 13f })
            foreach (int n in new[] { 10, 20, 30, 45 })
            {
                var f = Battle.Empty(91);
                f.Options.Weather = new Weather(hour);
                var troll = f.AddSquad(1, Troops.Get("troll"), 1, new(0, 20), 1, -Vector2.UnitY).Units[0];
                var men = f.AddSquad(0, UnitType.Shieldman, n, new(0, -5), 10, Vector2.UnitY);
                men.Order = Order.Charge;
                float healed = 0; int swings = 0; var was = UnitAction.None; float menHp = men.Units.Sum(u => u.Hp);
                for (int i = 0; i < 60 * 180 && f.Winner < 0 && !troll.Dead && (i == 0 || men.Alive > 0); i++)
                {
                    float hp = troll.Hp;
                    f.Update(1 / 60f);
                    if (troll.Hp > hp) healed += troll.Hp - hp;
                    if (troll.Action == UnitAction.Windup && was != UnitAction.Windup) swings++;
                    was = troll.Action;
                }
                Console.WriteLine($"troll at {hour:0}h vs {n} shieldmen: {f.Time:0} s, troll {(troll.Dead ? "dead" : $"{troll.Hp:0} hp")}, healed {healed:0}, men dead {men.Units.Count(u => u.Dead)}, routed {men.Units.Count(u => u.Routing)}, swings {swings}, men hp lost {menHp - men.Units.Sum(u => MathF.Max(0, u.Hp)):0}, target {troll.Target?.Index}");
            }
    }
}
