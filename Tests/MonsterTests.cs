using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the beasts of docs/archive/MONSTERS.md: monster hosts led by their own kind, spiders' poison and webs,
/// the brood mother's young, the wounded bear's rage, the wyvern's flight, and the new beasts on the map.
public static partial class SelfTest
{
    /// A monster host is led into battle by one of its own (a goblin chieftain, an alpha wolf, an elder troll...), never a
    /// man; lords and brigands keep their captains. The chieftain's fall breaks his goblins' hearts.
    static void MonsterLeaders()
    {
        var c = Campaign.New(61);
        var lair = c.Holdouts.First();
        c.Player.Pos = lair.Pos + new Vector2(2, 0);
        var setup = c.PrepareBattle(lair);
        Check(setup.Options.EnemyLeader?.Id == "goblin_chief", $"a goblin holdout is led by its chieftain ({setup.Options.EnemyLeader?.Id})");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        Check(b.EnemyCaptain.Type.Wild && b.EnemyCaptain.Type.Breed == "goblin" && b.EnemyCaptain.IsLeader, "the enemy leader on the field is a goblin, not a man");
        foreach (var p in new[] { c.WolfPacks.First(), c.Rares.First(), c.Nests.First(), c.Wyverns.First() })
        {
            var lead = Campaign.MonsterLeader(p);
            Check(lead != "" && Troops.Get(lead).Wild && Troops.Get(lead).Breed == p.Breed, $"{p.Name} ({p.Breed}) is led by its own kind ({lead})");
        }
        var lord = c.S.Parties.First(p => p.Kind == PartyKind.Lord);
        Check(Campaign.MonsterLeader(lord) == "" && c.PrepareBattle(lord).Options.EnemyLeader == null, "a lord leads his own men");

        // Kill the chieftain and the goblins lose heart, as at a boss's fall.
        var f = Battle.Empty(73, new BattleOptions { EnemyLeader = Troops.Get("goblin_chief") });
        var mob = f.AddSquad(1, Troops.Get("goblin"), 30, new(0, 30), 10, -Vector2.UnitY);
        var men = f.AddSquad(0, UnitType.Shieldman, 10, new(0, -10), 10, Vector2.UnitY);
        Run(f, 0.5f);
        float before = mob.Units.Average(u => u.Morale);
        f.EnemyCaptain.Action = UnitAction.Stagger;
        f.Hit(men.Units[0], f.EnemyCaptain, 99999, false);
        Check(f.EnemyCaptain.Dead && f.BossDown[1], "the chieftain can be slain");
        Check(mob.Units.Average(u => u.Morale) < before - 30, "his fall shakes every goblin");
        Check(f.History.Any(h => h.Text.Contains("goblin chieftain is slain")), "and the field hears it by name");
    }

    /// Giant spiders: a bite poisons (the poison keeps working through armour, and can kill); a web pins the men it lands
    /// on (they barely move and can hardly guard); a brood mother lays spiderlings as she fights, up to a cap.
    static void SpiderVenom()
    {
        var b = Battle.Empty(201);
        var spider = b.AddSquad(1, Troops.Get("spider"), 1, new(0, 1.2f), 1, -Vector2.UnitY).Units[0];
        var man = b.AddSquad(0, Troops.Get("men_at_arms"), 1, new(0, 0), 1, Vector2.UnitY).Units[0];
        man.Action = UnitAction.Stagger;
        b.Hit(spider, man, 17, false);
        float bitten = man.Hp;
        Check(man.Poison > 0 && man.PoisonedBy == spider, $"a spider's bite poisons ({man.Poison:0})");
        man.Squad!.Order = Order.Hold; spider.Squad!.Order = Order.Hold;
        spider.Pos = new Vector2(0, 40);
        Run(b, 3);
        Check(man.Hp < bitten - 5 && man.Poison <= 0.01f, $"the poison works on after the bite, through his armour ({bitten:0} -> {man.Hp:0})");
        var weak = b.AddSquad(0, Troops.Get("levies"), 1, new(5, 0), 1, Vector2.UnitY).Units[0];
        weak.Hp = 3; weak.Poison = 20; weak.PoisonedBy = spider;
        Run(b, 1);
        Check(weak.Dead, "poison can kill");
        var skel = b.AddSquad(0, Troops.Get("skeleton"), 1, new(-5, 0), 1, Vector2.UnitY).Units[0];
        skel.Action = UnitAction.Stagger;
        b.Hit(spider, skel, 17, false);
        Check(skel.Poison == 0, "the dead feel no poison");

        // A web: spiders spit at men a few strides off; the men in it are held.
        var w = Battle.Empty(202);
        var line = w.AddSquad(0, UnitType.Shieldman, 10, new(0, 0), 5, Vector2.UnitY);
        line.Order = Order.Hold;
        var spinners = w.AddSquad(1, Troops.Get("spider"), 4, new(0, 11), 4, -Vector2.UnitY);
        spinners.Order = Order.Hold;
        float firstWeb = -1;
        for (int i = 0; i < 60 * 6 && firstWeb < 0; i++) { w.Update(1 / 60f); if (line.Units.Any(w.Webbed)) firstWeb = w.Time; }
        Check(firstWeb > 0, $"spiders spit webs at men in reach ({firstWeb:0.0} s)");
        var held = line.Units.First(w.Webbed);
        held.Squad!.Order = Order.Charge;
        var start = held.Pos;
        Run(w, 0.5f);
        var free = Battle.Empty(203);
        var runner = free.AddSquad(0, UnitType.Shieldman, 1, new(0, 0), 1, Vector2.UnitY).Units[0];
        free.AddSquad(1, UnitType.Shieldman, 1, new(0, 30), 1, -Vector2.UnitY);
        runner.Squad!.Order = Order.Charge;
        Run(free, 0.5f);
        Console.WriteLine($"spiders: first web at {firstWeb:0.0} s; a webbed man moves {Vector2.Distance(start, held.Pos):0.00} m in 0.5 s, a free one {runner.Pos.Length():0.00} m");
        Check(Vector2.Distance(start, held.Pos) < runner.Pos.Length() * 0.5f, "a man in a web barely moves");

        // The brood mother lays her young while foes are near; they swarm at once and stop at the cap.
        var m = Battle.Empty(204);
        var mother = m.AddSquad(1, Troops.Get("brood_mother"), 1, new(0, 12), 1, -Vector2.UnitY).Units[0];
        mother.Squad!.Order = Order.Charge;
        m.AddSquad(0, UnitType.Shieldman, 30, new(0, 0), 10, Vector2.UnitY).Order = Order.Hold;
        Run(m, 12);
        var brood = m.Squads.FirstOrDefault(s => s.Name == "The Brood");
        Check(brood != null && brood.StartCount >= 2 && brood.Units.All(u => u.Type.Id == "spiderling"), $"the brood mother lays spiderlings ({brood?.StartCount ?? 0})");
        Run(m, 90);
        Check(brood!.StartCount <= Battle.BroodMax, "but no more than the cap");

        // A whole fight: ten spiders against twenty levies.
        var f = Battle.Empty(205);
        var pack = f.AddSquad(1, Troops.Get("spider"), 10, new(0, 25), 5, -Vector2.UnitY);
        pack.Order = Order.Charge;
        var lev = f.AddSquad(0, Troops.Get("levies"), 20, new(0, -5), 10, Vector2.UnitY);
        for (int i = 0; i < 60 * 90 && f.Winner < 0; i++) f.Update(1 / 60f);
        Console.WriteLine($"spiders: 10 spiders vs 20 levies: winner {f.Winner} at {f.Time:0} s, {lev.Units.Count(u => u.Dead)} levies dead, {pack.Units.Count(u => u.Dead)} spiders dead");
        Check(lev.Units.Count(u => u.Dead) > pack.Units.Count(u => u.Dead), "spiders kill more levies than they lose");
    }

    /// A cave bear in a rage: below half its hp it strikes harder and quicker.
    static void BearRage()
    {
        var b = Battle.Empty(211);
        var bear = b.AddSquad(1, Troops.Get("bear"), 1, new(0, 1.5f), 1, -Vector2.UnitY).Units[0];
        var a = b.AddSquad(0, Troops.Get("men_at_arms"), 2, new(0, 0), 2, Vector2.UnitY);
        float Blow(Unit t) { t.Action = UnitAction.Stagger; float hp = t.Hp; b.Hit(bear, t, bear.Type.Damage, false); return hp - t.Hp; }
        float calm = Blow(a.Units[0]);
        bear.Hp = bear.Type.MaxHp * 0.4f;
        float angry = Blow(a.Units[1]);
        Console.WriteLine($"bears: a blow at full hp {calm:0}, in a rage {angry:0}");
        Check(angry > calm * 1.25f, "a wounded bear strikes harder");
        Check(Troops.Get("bear").Look.Beast && Troops.Get("bear").Has("knockback"), "a bear is a beast that throws men down");
    }

    /// A wyvern on the wing: no blade reaches it aloft, arrows do; it dives on its prey, strikes, and climbs away.
    static void WyvernFlight()
    {
        var b = Battle.Empty(221);
        var wy = b.AddSquad(1, Troops.Get("wyvern"), 1, new(0, 30), 1, -Vector2.UnitY).Units[0];
        var man = b.AddSquad(0, UnitType.Shieldman, 1, new(0, 29), 1, Vector2.UnitY).Units[0];
        Check(wy.Altitude >= Battle.FlyHigh - 0.01f && Battle.Airborne(wy), "a wyvern takes the field on the wing");
        float hp = wy.Hp;
        b.Hit(man, wy, 80, false);
        Check(wy.Hp == hp, "no blade reaches it aloft");
        b.Hit(man, wy, 80, true);
        Check(wy.Hp < hp, "an arrow does");

        // Over a fight with a shield line it comes down to strike and climbs away again, more than once.
        var f = Battle.Empty(222);
        var w = f.AddSquad(1, Troops.Get("wyvern"), 1, new(0, 30), 1, -Vector2.UnitY).Units[0];
        w.Squad!.Order = Order.Charge;
        var line = f.AddSquad(0, UnitType.Shieldman, 20, new(0, 0), 10, Vector2.UnitY);
        line.Order = Order.Hold;
        int dives = 0;
        bool low = false;
        float lowest = 99;
        for (int i = 0; i < 60 * 40; i++)
        {
            f.Update(1 / 60f);
            lowest = MathF.Min(lowest, w.Altitude);
            if (!low && w.Altitude < Battle.Aloft) { low = true; dives++; }
            else if (low && w.Altitude > Battle.FlyHigh * 0.7f) low = false;
        }
        int hurt = line.Units.Count(u => u.Dead || u.Hp < u.Type.MaxHp);
        Console.WriteLine($"wyverns: in 40 s against 20 shieldmen it dives {dives} times (lowest {lowest:0.0} m), hurts {hurt} men, has {w.Hp:0} of {w.Type.MaxHp} hp");
        Check(dives >= 3, "a wyvern dives, climbs away and dives again");
        Check(hurt >= 3, "its strikes land");

        // Archers bring it down; a line of foot alone can only catch it as it strikes.
        int Bows(string troop, out Battle fb, out Unit wyv)
        {
            fb = Battle.Empty(223);
            wyv = fb.AddSquad(1, Troops.Get("wyvern"), 1, new(0, 40), 1, -Vector2.UnitY).Units[0];
            wyv.Squad!.Order = Order.Charge;
            var men = fb.AddSquad(0, Troops.Get(troop), 30, new(0, 0), 10, Vector2.UnitY);
            men.Order = Order.Hold;
            for (int i = 0; i < 60 * 120 && fb.Winner < 0; i++) fb.Update(1 / 60f);
            return men.Units.Count(u => u.Dead);
        }
        int lostToBows = Bows("bowmen", out var fbw, out var wb), lostFoot = Bows("shieldmen", out var fsh, out var ws);
        Console.WriteLine($"wyverns: 30 bowmen lose {lostToBows} men, wyvern {(wb.Dead ? "down" : $"{wb.Hp:0} hp")} at {fbw.Time:0} s; 30 shieldmen lose {lostFoot}, wyvern {(ws.Dead ? "down" : $"{ws.Hp:0} hp")} at {fsh.Time:0} s");
        Check(wb.Hp < ws.Hp || wb.Dead && !ws.Dead || wb.Dead && fbw.Time < fsh.Time, "archers hurt a wyvern more than foot do");
        Check(Rules.Strength(Troops.Get("wyvern"), 1) > Rules.Strength(UnitType.Shieldman, 15), "auto-resolve weighs a wyvern as many men");
    }

    /// The beasts on the map: spider nests in the woods and fens that grow and raid by night, cave bears in the hills,
    /// a wyvern that hunts by day and roosts by night, and great beasts that ravage villages when no prey is about.
    static void MonstersAbroad()
    {
        var c = Campaign.New(231);
        var nests = c.Nests.ToList();
        Check(nests.Count >= 1 && nests.Count <= c.NestsWanted, $"the woods have their spider nests ({nests.Count} of {c.NestsWanted})");
        foreach (var n in nests)
            Check(c.Map.ProvinceAt(n.Pos).Kind is "Forest" or "Marsh" && n.Companies.All(co => co.Troop == "spider"), $"{n.Name} is a nest of spiders in a wood or fen");
        Check(c.Holdouts.All(h => h.Breed == "goblin") && c.Lairs.Count() == c.Holdouts.Count() + nests.Count, "holdouts are goblins', nests spiders'");
        Check(c.Bears.Count() == c.BearsWanted && c.BearsWanted > 0, $"the hills have their bears ({c.Bears.Count()})");
        Check(c.Wyverns.Count() == 1, "a wyvern roosts somewhere in the world");
        var wy = c.Wyverns.First();
        Check(c.Speed(wy) > c.Speed(c.Player) * 1.2f, "a wyvern outflies a company");
        c.Encounter = wy;
        Check(!c.CanRetreat(out string why) && why.Contains("WINGS"), "you cannot outrun one");
        c.Encounter = null;

        // By night it roosts; by day it stoops on weak prey.
        c.Player.Pos = wy.Pos + new Vector2(10, 0) * c.Map.Spread;
        foreach (var q in c.S.Parties.Where(q => q != wy && q != c.Player && Vector2.Distance(q.Pos, wy.Pos) < 40 * c.Map.Spread)) q.Pos += new Vector2(90, 90) * c.Map.Spread;
        c.S.Hours = Math.Floor(c.S.Hours / 24) * 24 + 23;
        c.MonsterThink(wy);
        Check(!wy.Moving && wy.Task == "rest", "a wyvern roosts by night");
        c.S.Hours += 13; // noon
        foreach (var co in c.Player.Companies) foreach (var st in co.Stacks) st.Men = Math.Min(st.Men, 3);
        c.MonsterThink(wy);
        Check(wy.ChaseId == c.Player.Id, "and stoops on easy prey by day");

        // A great beast with nothing to hunt ravages a village; the realm then hunts it.
        var troll = c.Rares.First();
        var village = c.S.Settlements.First(v => v.Kind == SettlementKind.Village && v.ThreatUntil <= c.S.Hours);
        troll.Pos = village.Pos + new Vector2(1, 0);
        troll.RaidOn = village.Id;
        c.Player.Pos = troll.Pos + new Vector2(80, 80) * c.Map.Spread;
        foreach (var q in c.S.Parties.Where(q => q != troll && q.Kind != PartyKind.Player && Vector2.Distance(q.Pos, troll.Pos) < 30 * c.Map.Spread)) q.Pos += new Vector2(70, -70) * c.Map.Spread;
        c.S.Hours = Math.Floor(c.S.Hours / 24) * 24 + 23;
        float prosperity = village.Prosperity;
        c.MonsterThink(troll);
        Check(village.ThreatUntil > c.S.Hours && village.Prosperity < prosperity, "a troll ravages a village");
        Check(troll.HuntedBy == village.Faction && troll.HuntedUntil > c.S.Hours, "and the village's realm rides after it");
        Check(c.S.Log.Any(l => l.Text.Contains("troll ravages")), "the land hears of it");

        // Bears keep their country by day and sleep by night.
        var bear = c.Bears.First();
        c.MonsterThink(bear);
        Check(bear.Task == "rest" && !bear.Moving, "bears sleep by night");

        // Nests grow and send broods out; a save keeps the beasts.
        var nest = nests[0];
        foreach (var co in nest.Companies) co.Stack(co.Troop).Men = c.Scaled(Balance.Get().NestMaxMen);
        nest.NextRaid = 0;
        c.SendRaiders(nest);
        var brood = c.S.Parties.FirstOrDefault(p => p.LairId == nest.Id);
        Check(brood != null && brood.Breed == "spider" && brood.Companies.All(co => co.Troop == "spider"), "a full nest sends a brood out");
        string json = c.S.Serialize();
        Check(CampaignState.Deserialize(json).Serialize() == json, "a save keeps the beasts");

        Days(c, 90);
        Console.WriteLine($"beasts: after 90 days {c.Holdouts.Count()} holdouts, {c.Nests.Count()} nests, {c.WolfPacks.Count()} packs, {c.Bears.Count()} bears, {c.Rares.Count()} trolls, {c.Wyverns.Count()} wyverns (wanted {c.WyvernsWanted})");
        Check(c.Wyverns.Count() <= c.WyvernsWanted && c.Nests.Any(), "the world keeps its beasts");
    }

    /// Debug: the world's beasts every ten days on a fresh map (`--selftest BeastTrace`).
    static void BeastTrace()
    {
        var c = Campaign.New(231);
        for (int d = 0; d <= 120; d += 10)
        {
            Console.WriteLine($"day {c.Day}: holdouts {c.Holdouts.Count()}/{c.HoldoutsWanted}, nests {c.Nests.Count()}/{c.NestsWanted}, packs {c.WolfPacks.Count()}/{c.WolfPacksWanted}, "
                + $"bears {c.Bears.Count()}/{c.BearsWanted}, trolls {c.Rares.Count()}/{c.RaresWanted}, wyverns {c.Wyverns.Count()}/{c.WyvernsWanted}, undead {c.UndeadHosts.Count()}, "
                + $"bands {c.S.Parties.Count(p => p.Kind == PartyKind.Monster && !p.Lair && !p.Down)}, villages under threat {c.S.Settlements.Count(v => v.ThreatUntil > c.S.Hours)}");
            Days(c, 10);
        }
        foreach (var l in c.S.Log.Where(l => l.Text.Contains("ravage") || l.Text.Contains("wyvern") || l.Text.Contains("pider") || l.Text.Contains("slain near")).Take(20)) Console.WriteLine(l.Text);
    }
}
