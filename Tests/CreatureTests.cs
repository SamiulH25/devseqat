using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of creatures and monsters (docs/archive/CREATURES.md): goblin holdouts on the map.
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
            Check(Campaign.Wild(c.Map.ProvinceAt(h.Pos)), $"{h.Name} is in wild country");
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
            Check(c.Map.ProvinceAt(w.Pos).Kind is "Forest" or "Taiga", $"{w.Name} lives in a forest or the taiga"); // WM3: the taiga's woods have wolves too
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

    /// Auto-resolve weighs great beasts as fought battles find them (docs in Rules.MightAgainst). The aim: a company of
    /// 200-300 beats one of the map's beast parties and loses a good part of itself doing it. Each case is a typical company
    /// (three tenths levies, a quarter bowmen, a quarter men-at-arms, a fifth horse) against the party as the map spawns it,
    /// with the winner of four fought battles at that hour (Campaign.Edge: TrollNight, TrollDay).
    static void BeastsWeighed()
    {
        Party Side(string troop, int n, int tier)
        {
            var p = new Party { Tier = tier };
            p.Companies.Add(new CompanyRecord { Role = "infantry" });
            p.Companies[0].Stack(troop).Men = n;
            return p;
        }
        Party Company(int men)
        {
            var p = new Party { Tier = 2 };
            foreach (var (t, r, k) in new[] { (Armies.LineTroop("infantry", 1), "infantry", men * 3 / 10), (Armies.LineTroop("archers", 2), "archers", men / 4),
                                               (Armies.LineTroop("infantry", 3), "infantry", men / 4), (Armies.LineTroop("horse", 3), "horse", men / 5) })
            { var c = new CompanyRecord { Role = r }; c.Stack(t).Men = k; p.Companies.Add(c); }
            return p;
        }
        var bal = Balance.Get();
        foreach (var (men, beast, n, night, win) in new[]
        {
            (120, "troll", 6, true, false), (120, "troll", 6, false, false), (200, "troll", 6, true, false), (200, "troll", 6, false, false), (300, "troll", 6, false, true),
            (120, "wyvern", 3, false, false), (200, "wyvern", 3, false, true), (300, "wyvern", 3, false, true),
            (120, "ogre", 6, false, false), (200, "ogre", 6, false, true), (300, "ogre", 6, false, true),
            (120, "bear", 6, false, true),
        })
        {
            var (a, b) = Rules.AutoWeights(new[] { Company(men) }, new[] { Side(beast, n, Troops.Get(beast).Tier) });
            float hour = beast == "troll" ? (night ? bal.TrollNight : bal.TrollDay) : 1;
            Check(a > b * hour == win, $"{men} men against {n} {beast}{(beast == "troll" ? night ? " by night" : " by day" : "")}: the field says {(win ? "a win" : "a loss")}, auto-resolve {a / (b * hour):0.00}");
        }
        var levies = Side("levies", 60, 1);
        var foot = Side(Armies.LineTroop("infantry", 3), 40, 3);
        Check(Math.Abs(Rules.MightAgainst(new[] { levies }, new[] { foot }) - Rules.Might(levies)) < 1e-3f, "men against men are weighed as before");
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
        Check(trolls.Count == 3 && c.RaresWanted == 3, $"the world starts with three trolls ({trolls.Count})");
        foreach (var t in trolls)
            Check(Campaign.Wild(c.Map.ProvinceAt(t.Pos)) && t.Companies.All(co => co.Troop == "troll"), $"{t.Name} lives in the wilds");
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
        Check(c.RaresWanted == 5, $"one more every 60 days ({c.RaresWanted})");
        c.S.Hours += 24 * 1000;
        Check(c.RaresWanted == 8, "at most eight");
        var d = Campaign.New(96);
        Days(d, 120);
        Console.WriteLine($"trolls: after 120 days {d.Rares.Count()} trolls abroad (wanted {d.RaresWanted}): {string.Join(", ", d.Rares.Select(r => $"{r.Name} {r.Men}"))}");
        Check(d.Rares.Count() >= 3 && d.Rares.Count() <= d.RaresWanted, "the world keeps its rare monsters");
    }

    /// Phase 5b: a big battle leaves a grave field; buried it lies quiet, left alone it rises some nights later as an undead
    /// host that lies in the earth by day and hunts the living by night.
    static void UndeadAbroad()
    {
        var c = Campaign.New(98);
        Party Fight()
        {
            foreach (var co in c.Player.Companies) foreach (var st in co.Stacks) st.Men = 0;
            c.Player.Companies.Clear();
            c.Player.Companies.Add(c.NewCompany("ironguard", "infantry", c.Scaled(40), 0));
            var band = c.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
            foreach (var co in band.Companies) foreach (var st in co.Stacks) st.Men = 0;
            band.Companies.Clear();
            band.Companies.Add(c.NewCompany("levies", "infantry", c.Scaled(90), 0));
            band.Pos = c.Player.Pos;
            foreach (var p in c.S.Parties) // no passer-by joins: the test wants a fight of this size
                if (p != c.Player && p != band && Vector2.Distance(p.Pos, c.Player.Pos) < Balance.Get().JoinRadius * 2)
                    p.Pos += Vector2.Normalize(p.Pos - c.Player.Pos + new Vector2(0.01f, 0)) * Balance.Get().JoinRadius * 3;
            c.Encounter = band;
            c.S.Event = null;
            c.AutoResolveEncounter();
            c.Encounter = null;
            return band;
        }
        Fight();
        Check(c.S.Graves.Count == 1 && c.S.Event?.Id == "bury_dead", $"a big battle leaves a grave field, and you are asked to bury it ({c.S.Graves.Count}, {c.S.Event?.Id})");
        Check(c.Choose(0) == null && c.S.Graves.Count == 0, "buried, the dead lie quiet");
        Fight();
        Check(c.S.Event == null && c.S.Graves.Count == 1, "you are asked once a week; the next field the same day is not put to you");
        c.S.Graves.Clear();
        c.S.EventsFired["bury_dead"] -= 7 * 24; // a week on
        Fight();
        Check(c.Choose(2) == null && c.S.Graves.Count == 1, "march on and the field stays unburied");
        int dead = c.S.Graves[0].Dead, gid = c.S.Graves[0].Id;
        string where = c.Map.ProvinceAt(c.S.Graves[0].Pos).Name;
        Party? risen = null;
        int waited = 0;
        while (waited < 15 && risen == null) { Days(c, 1); waited++; risen = c.UndeadHosts.FirstOrDefault(h => h.Name == $"The Dead of {where}"); }
        Console.WriteLine($"undead: a field of {dead} dead {(risen != null ? $"rose after {waited} days as {risen.Men} undead" : "did not rise")}");
        Check(risen != null && !c.S.Graves.Any(g => g.Id == gid), "an unburied field rises within days");

        // A risen host: skeletons, ghouls and a necromancer; it lies still by day and hunts by night.
        var host = c.RaiseHost(new Grave { Pos = c.Player.Pos + new Vector2(30, 0) * c.Map.Spread, Dead = 200, Hours = c.S.Hours });
        Check(host.Companies.Any(co => co.Troop == "skeleton") && host.Companies.Any(co => co.Troop == "ghoul") && host.Companies.Single(co => co.Troop == "necromancer").Men == 1,
            "a host of skeletons and ghouls led by one necromancer");
        c.Player.Pos = host.Pos + new Vector2(4, 0) * c.Map.Spread;
        c.S.Hours = Math.Floor(c.S.Hours / 24) * 24 + 13;
        c.MonsterThink(host);
        Check(!host.Moving && host.Task == "rest", "the dead lie in the earth by day");
        c.S.Hours += 10;
        foreach (var co in c.Player.Companies) foreach (var st in co.Stacks) st.Men = Math.Min(st.Men, 3);
        c.MonsterThink(host);
        Check(host.ChaseId == c.Player.Id, "and hunt the living by night");
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
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
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

        // Beast hunters, a branch of the spear line, kill a troll that breaks shieldmen, and do not fear it.
        Check(Troops.Get("spearrunners").UpgradesTo.Contains("beast_hunters") && Troops.Get("beast_hunters").VsMonster > 1, "spearrunners can train as beast hunters");
        bool Kills(string foe, out Squad men)
        {
            var f = Battle.Empty(98);
            f.Options.Weather = new Weather(23);
            var tr = f.AddSquad(1, Troops.Get("troll"), 1, new(0, 20), 1, -Vector2.UnitY).Units[0];
            men = f.AddSquad(0, Troops.Get(foe), 20, new(0, -5), 10, Vector2.UnitY);
            men.Order = Order.Charge;
            for (int i = 0; i < 60 * 120 && !tr.Dead && (i == 0 || men.Alive > 0); i++) f.Update(1 / 60f);
            return tr.Dead;
        }
        bool hunters = Kills("beast_hunters", out var hs), shields = Kills("shieldmen", out var ss);
        Console.WriteLine($"creatures: 20 beast hunters kill a troll by night {hunters} ({hs.Units.Count(u => u.Routing)} routed); 20 shieldmen {shields} ({ss.Units.Count(u => u.Routing)} routed)");
        Check(hunters && !shields && hs.Units.Count(u => u.Routing) == 0, "beast hunters kill a troll that breaks shieldmen, and stand");

        // Ogres turn up for hire now and then.
        CompanyRecord? ogres = null;
        Settlement? board = null;
        for (int week = 0; week < 40 && ogres == null; week++)
        {
            Days(c, 7);
            board = c.S.Settlements.FirstOrDefault(s => s.Mercenaries.Any(m => m.Troop == "ogre"));
            ogres = board?.Mercenaries.First(m => m.Troop == "ogre");
        }
        Check(ogres != null && ogres.Type.Huge && ogres.Name == "Ogre Sellswords", $"ogres are offered as sellswords (by day {c.Day})");
        c.S.Gold = 100000;
        c.Player.Companies.RemoveAll(co => co.Troop == "war_hounds");
        c.Player.Pos = board!.Pos;
        Check(c.Hire(board, ogres!) == null && c.Player.Companies.Contains(ogres!), "you can hire the ogres");
    }

    /// Debug: one troll against companies of shieldmen by night and by day (`--selftest TrollTrace`).
    static void TrollTrace()
    {
        foreach (var foe in new[] { "shieldmen", "spearrunners", "beast_hunters" })
        foreach (float hour in new[] { 23f, 13f })
            foreach (int n in new[] { 10, 20, 30, 45 })
            {
                var f = Battle.Empty(91);
                f.Options.Weather = new Weather(hour);
                var troll = f.AddSquad(1, Troops.Get("troll"), 1, new(0, 20), 1, -Vector2.UnitY).Units[0];
                var men = f.AddSquad(0, Troops.Get(foe), n, new(0, -5), 10, Vector2.UnitY);
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
                Console.WriteLine($"troll at {hour:0}h vs {n} {foe}: {f.Time:0} s, troll {(troll.Dead ? "dead" : $"{troll.Hp:0} hp")}, healed {healed:0}, men dead {men.Units.Count(u => u.Dead)}, routed {men.Units.Count(u => u.Routing)}, swings {swings}, men hp lost {menHp - men.Units.Sum(u => MathF.Max(0, u.Hp)):0}, target {troll.Target?.Index}");
            }
    }

    /// Phase 5: skeletons shrug off arrows, ghouls feed, a necromancer raises the fallen and his death drops them all.
    static void Undead()
    {
        var sk = Troops.Get("skeleton");
        Check(sk.Wild && sk.Breed == "undead" && sk.Has("fearless") && sk.Look.HeadKey == "head.skull", "a skeleton is a fearless wild creature with a skull");

        // Arrows: the same volley hurts a skeleton far less than a man of the same armour.
        float Shot(UnitType t)
        {
            var b = Battle.Empty(95);
            var mark = b.AddSquad(1, t, 10, new(0, 20), 5, -Vector2.UnitY);
            mark.Order = Order.Hold;
            foreach (var u in mark.Units) u.Hp = 1000; // nobody falls, so the whole volley counts
            var bows = b.AddSquad(0, UnitType.Bowman, 20, new(0, 0), 10, Vector2.UnitY);
            bows.Order = Order.Hold;
            bows.Target = mark;
            Run(b, 20);
            return mark.Units.Sum(u => 1000 - u.Hp);
        }
        var man = Troops.Get("shieldmen");
        float bone = Shot(sk), flesh = Shot(man);
        Console.WriteLine($"undead: 20 bows for 20 s do {bone:0} to skeletons, {flesh:0} to shieldmen");
        Check(bone < flesh * 0.5f, "arrows do little to skeletons");

        // A necromancer raises the fallen near him; they charge; kill him and every one of them falls.
        var f = Battle.Empty(96);
        var necro = f.AddSquad(1, Troops.Get("necromancer"), 1, new(0, 30), 1, -Vector2.UnitY).Units[0];
        necro.Squad!.Order = Order.Hold;
        var dead = f.AddSquad(0, Troops.Get("levies"), 6, new(0, 24), 6, Vector2.UnitY);
        foreach (var u in dead.Units) { u.Dead = true; u.DiedAt = -5; u.Hp = 0; }
        Run(f, 6);
        var risen = f.Squads.FirstOrDefault(q => q.Name == "The Risen");
        Check(risen is { StartCount: >= 4 } && risen.Units.All(u => u.Type.Id == "skeleton" && u.Team == 1), $"a necromancer raises the fallen as skeletons ({risen?.StartCount ?? 0})");
        Check(dead.Units.Count(u => u.Raised) == risen?.StartCount, "each corpse rises once");
        var walker = risen!.Units[0];
        walker.Morale = 0;
        Run(f, 0.5f);
        Check(!walker.Routing, "the undead never break");
        int fallenBefore = f.Units.Count(u => u.Dead);
        var men = f.AddSquad(0, UnitType.Shieldman, 30, new(0, 10), 10, Vector2.UnitY);
        men.Order = Order.Charge;
        men.Target = necro.Squad;
        for (int i = 0; i < 60 * 120 && !necro.Dead; i++) f.Update(1 / 60f);
        Run(f, 0.1f);
        Console.WriteLine($"undead: 30 shieldmen cut down the necromancer after {f.Time:0} s; {risen.StartCount} risen, {risen.Units.Count(u => u.Active)} still up");
        Check(necro.Dead && risen.Units.All(u => !u.Active), "when the necromancer falls his dead fall with him");

        // A ghoul heals on a kill.
        var g = Battle.Empty(97);
        var ghoul = g.AddSquad(1, Troops.Get("ghoul"), 1, new(0, 1.2f), 1, -Vector2.UnitY).Units[0];
        ghoul.Hp = ghoul.Type.MaxHp / 2;
        var prey = g.AddSquad(0, Troops.Get("levies"), 1, new(0, 0), 1, Vector2.UnitY).Units[0];
        prey.Hp = 1;
        prey.Squad!.Order = Order.Hold;
        ghoul.Squad!.Order = Order.Charge;
        for (int i = 0; i < 60 * 5 && !prey.Dead; i++) g.Update(1 / 60f);
        Check(prey.Dead && ghoul.Hp > ghoul.Type.MaxHp * 0.7f, $"a ghoul feeds on its kill ({ghoul.Hp:0})");
    }

    /// Debug: an undead host (skeletons, ghouls, a necromancer) against an army of men, with both generals (`--selftest UndeadTrace`).
    static void UndeadTrace()
    {
        foreach (int men in new[] { 20, 40, 60 })
            foreach (bool bows in new[] { true, false })
            {
                float hour = 23;
                var host = new ArmySpec("The Dead", 3, new List<Company> { new("skeleton", 30, Id: 1), new("ghoul", 8, Id: 2), new("necromancer", 1, Id: 3) });
                var army = new ArmySpec("Men", 3, new List<Company> { new("shieldmen", men, Id: 1), new("bowmen", bows ? men / 2 : 0, Id: 2) });
                var b = new Battle(31, army, host, new BattleOptions { AutoPlayer = true, PlayerWounded = true, Weather = new Weather(hour) });
                var necro = b.Units.First(u => u.Type.Has("raise"));
                float behind = 0; int n = 0;
                for (int i = 0; b.Winner < 0 && i < 60 * 300; i++)
                {
                    b.Update(1 / 60f);
                    if (i % 60 == 0 && necro.Active) { behind += b.Units.Where(u => u.Team == 0 && u.Active).Min(u => Vector2.Distance(u.Pos, necro.Pos)); n++; }
                }
                var risen = b.Squads.FirstOrDefault(q => q.Name == "The Risen");
                Console.WriteLine($"undead host vs {men} shieldmen + {(bows ? men / 2 : 0)} bows: winner {b.Winner} after {b.Time:0} s, necromancer {(necro.Dead ? $"slain at {necro.DiedAt:0} s" : "lives")}, mean gap to foe {behind / Math.Max(1, n):0} m, "
                    + $"risen {risen?.StartCount ?? 0}, men dead {b.Units.Count(u => u.Team == 0 && u.Dead)}, undead down {b.Units.Count(u => u.Team == 1 && u.Dead)}");
            }
    }

    /// Debug: grave fields and undead hosts in a world left to itself (`--selftest GraveTrace`).
    static void GraveTrace()
    {
        var c = Campaign.New(61);
        var seen = new HashSet<int>();
        for (int day = 1; day <= 90; day++)
        {
            Days(c, 1);
            foreach (var h in c.UndeadHosts) seen.Add(h.Id);
            if (day % 10 == 0)
                Console.WriteLine($"day {day}: graves {c.S.Graves.Count}, hosts {string.Join(", ", c.UndeadHosts.Select(h => $"{h.Name} {h.Men}"))}; risen so far {seen.Count}");
        }
    }

    /// WI1-WI2: province wildness rises with lairs, feeds unrest, cuts on clearing, persists in saves; lairs grow up
    /// through warren/hold, spider brood/webbed wood (slowing travel), and barrows.
    static void WildnessAndLairs()
    {
        var c = Campaign.New(81);
        var lair = c.Holdouts.First();
        var prov = c.Map.ProvinceAt(lair.Pos);
        float w0 = c.WildnessOf(prov.Id);
        for (int i = 0; i < 15; i++) c.WildnessDaily();
        float w1 = c.WildnessOf(prov.Id);
        Check(w1 > w0 + 5, $"lair raises province wildness ({w0:0.0} -> {w1:0.0})");
        var saved = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        Check(MathF.Abs(saved.WildnessOf(prov.Id) - w1) < 0.01f, "province wildness survives save/load");
        // Clearing cuts wildness at once.
        c.OnMonsterCleared(lair);
        Check(c.WildnessOf(prov.Id) < w1 - 10, $"clearing lair cuts wildness ({w1:0.0} -> {c.WildnessOf(prov.Id):0.0})");
        // High wildness raises fief unrest.
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
        c.S.Provinces[town.Province].Wildness = 80;
        Check(c.UnrestDrift(town).Why.Any(w => w.Contains("WILDNESS")), "high wildness adds unrest drift");
        Check(c.RoadClosedByWild(town.Province), "overrun province closes roads to caravans");
        // Goblin holdout grows to Warren (stage 1) and Hold (stage 2).
        lair.Companies[0].Stacks[0].Men = c.Scaled(Balance.Get().HoldoutMaxMen);
        lair.Tier = 4;
        c.LairsGrowDaily();
        Check(lair.Stage == 1 && lair.Name.Contains("Warren"), $"holdout grows into Warren ({lair.Name})");
        c.LairsGrowDaily();
        Check(lair.Stage == 2 && lair.Name.Contains("Hold"), $"warren grows into Hold ({lair.Name})");
        // Spider nest grows to Stage 2 webbed wood and slows travel.
        var nest = c.Nests.FirstOrDefault() ?? (c.SpawnNest() ? c.Nests.First() : null);
        if (nest != null)
        {
            var nProv = c.Map.ProvinceAt(nest.Pos);
            nest.Companies[0].Stacks[0].Men = c.Scaled(Balance.Get().NestMaxMen);
            c.Player.Pos = nest.Pos;
            float fast = c.Speed(c.Player);
            c.LairsGrowDaily();
            c.LairsGrowDaily();
            Check(nest.Stage == 2 && c.IsProvinceWebbed(nProv.Id), "spider nest grows into stage 2 webbed wood");
            Check(c.Speed(c.Player) < fast * 0.8f, "webbed province slows travel");
        }
        // Barrow raised from grave field.
        var barrow = c.RaiseBarrow(new Grave { Id = 999, Pos = prov.Center, Dead = 200, Hours = 0 });
        Check(barrow.Lair && barrow.Breed == "undead" && barrow.Stage == 2 && Campaign.LairStageWord(barrow) == "BARROW", "grave field rises as a barrow lair");

        // WI3: monster spoils, wild smith marks, order standing on undead, and aura breakthrough on great beasts.
        Check(Campaign.GoodsFrom(Troops.Get("troll")) == "troll_hide" && Campaign.GoodsFrom(Troops.Get("wyvern")) == "wyvern_sting"
            && Campaign.GoodsFrom(Troops.Get("spider")) == "spider_silk" && Campaign.GoodsFrom(Troops.Get("skeleton")) == "grave_dust", "monsters drop their own spoils");
        c.Player.Pos = town.Pos;
        c.S.Gold = 5000;
        var fineBlade = new GearItem { Id = c.S.NextItemId++, Def = "arming_sword", Quality = ItemQuality.Fine };
        c.Captain.Baggage.Add(fineBlade);
        c.S.Goods.Remove("wyvern_sting");
        Check(c.CannotRemark(fineBlade, "sting_edged")!.Contains("WYVERN STING"), "wild smith mark requires its monster spoil");
        c.S.Goods["wyvern_sting"] = 1;
        Check(c.Remark(fineBlade, "sting_edged") == null && fineBlade.Marks.Contains("sting_edged") && !c.S.Goods.ContainsKey("wyvern_sting"), "wild smith mark consumes spoil and applies mark");
        c.Captain.AuraTier = 1;
        c.Captain.AuraWall = true;
        var troll = new Party { Kind = PartyKind.Monster, Breed = "troll", Name = "Old Jaw", Pos = prov.Center };
        c.OnMonsterCleared(troll, playerWon: true);
        Check(!c.Captain.AuraWall && c.Captain.AuraTier == 2, "slaying a great beast breaks an aura wall");
    }

    /// WI4-WI6: all new wild monsters load and validate, carry distinct tactical rules (wraith spectral resist vs holy,
    /// basilisk gaze facing vs flank, harpy targeting bows, boar trample, hill giant boulders), and plug into monster contracts & hunts.
    static void WildRosterAndHunts()
    {
        var ids = new[] { "boar", "tusker", "great_boar", "hill_giant", "giant_chief", "goblin_shaman", "harpy", "griffon", "basilisk", "sand_wurm", "drowned_dead", "werewolf", "wraith", "barrow_king", "bear_king" };
        foreach (var id in ids) Check(Troops.Get(id).Wild, $"new wild monster {id} loads");
        Check(Troops.Get("goblin_shaman").Spells.Length == 2, "goblin shaman casts spells");
        Check(Campaign.GoodsFrom(Troops.Get("basilisk")) == "basilisk_eye" && Campaign.GoodsFrom(Troops.Get("harpy")) == "harpy_quills", "basilisk and harpy drop their own spoils");

        // Wraith spectral resist: mundane men deal far less than blessed/holy men.
        float WraithDmg(bool blessed)
        {
            var b = Battle.Empty(95);
            if (blessed) b.Options.Standing = new[] { "bless_rite" };
            var men = b.AddSquad(0, Troops.Get("men_at_arms"), 4, new(0, -2), 4, Vector2.UnitY);
            var wr = b.AddSquad(1, Troops.Get("wraith"), 1, new(0, 0), 1, -Vector2.UnitY).Units[0];
            men.Order = Order.Charge; men.Target = wr.Squad;
            float startHp = wr.Hp;
            Run(b, 5);
            return startHp - wr.Hp;
        }
        float plainW = WraithDmg(false), holyW = WraithDmg(true);
        Check(holyW > plainW * 2f, $"wraiths resist mundane steel ({plainW:0} dmg) vs holy blessing ({holyW:0} dmg)");

        // Basilisk gaze slows men facing it head-on, not men facing away / flanking.
        var bg = Battle.Empty(96);
        var bas = bg.AddSquad(1, Troops.Get("basilisk"), 1, new(0, 0), 1, -Vector2.UnitY).Units[0];
        var facing = bg.AddSquad(0, UnitType.Shieldman, 1, new(0, -6), 1, Vector2.UnitY).Units[0];
        var away = bg.AddSquad(0, UnitType.Shieldman, 1, new(0, 6), 1, Vector2.UnitY).Units[0];
        bas.Spit = 0.05f;
        Run(bg, 0.2f);
        Check(bg.Webbed(facing) && !bg.Webbed(away), "basilisk gaze slows men facing it, not flankers");

        // WI4: monster contracts have plain-words stakes and track the quarry for PrepareBattle + AutoResolve edge.
        var c = Campaign.New(82);
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
        var quarry = c.S.Parties.First(p => !p.Down && p.Kind == PartyKind.Monster);
        quarry.Pos = town.Pos + new Vector2(10, 0);
        c.TrackQuarry(quarry.Id);
        Check(c.KnowsQuarry(quarry) && c.HuntEdge(quarry) > 1f, "tracking a quarry grants HuntEdge");
        Check(Campaign.QuarryHint(quarry).Length > 10 && c.MonsterStake(quarry, town).Contains(town.Name), "quarry hint and contract stake are plain words");
        var setup = c.PrepareBattle(quarry);
        Check(setup.Options.Standing.Contains("quarry_known"), "tracked quarry carries quarry_known into battle");
    }
}
