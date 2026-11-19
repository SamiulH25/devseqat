using Microsoft.Xna.Framework;

namespace Ironfield;

/// Engines in the campaign and in field battles (docs/SIEGECRAFT.md A3, A4).
public static partial class SelfTest
{
    /// Trains on both sides of a field battle: crews come off foot companies and report with them, the engines stand behind
    /// their own line and shoot, and a replay brings them back the same.
    static void FieldEngines()
    {
        var rng = new Random(9);
        var o = new BattleOptions { AutoPlayer = true, PlayerWounded = true };
        o.Engines.Add(new EngineSpec { Team = 0, Id = "scorpion" });
        o.Engines.Add(new EngineSpec { Team = 0, Id = "springald" });
        o.Engines.Add(new EngineSpec { Team = 1, Id = "scorpion" });
        o.Engines.Add(new EngineSpec { Team = 1, Id = "trebuchet" }); // a siege work: it stays in camp
        var b = new Battle(41, Ids(Armies.Random(rng, 2, 0), 3000), Ids(Armies.Random(rng, 2, 1), 4000), o);
        Check(b.Engines.Count == 3 && b.Engines.All(e => e.Def.Shot == "bolt"), $"three bolt-throwers take the field ({b.Engines.Count})");
        Check(b.Engines.All(e => e.Crew.Company != null && e.Crew.SplitFrom != null && b.CrewAt(e) == e.Def.Crew), "each crew comes off a foot company and stands at its engine");
        foreach (var e in b.Engines)
        {
            var line = b.SquadsOf(e.Team).Where(s => s.Company != null && s.Works == null && !s.Units.Any(u => u.Ammo > 0)).Select(s => s.Centroid().Y).DefaultIfEmpty(0).Average();
            Check(e.Team == 0 ? e.Pos.Y < line : e.Pos.Y > line, "an engine stands behind its own line");
        }
        b.StartRecording(41);
        for (int step = 0; b.Winner < 0 && step < 60 * 400; step++) b.Update(1 / 60f);
        Console.WriteLine($"field engines: {string.Join(", ", b.Engines.Select(e => $"{e.Def.Id}/{e.Team} {e.Shots} shots {e.Kills} kills{(e.Wrecked ? " wrecked" : "")}"))}, winner {b.Winner}");
        Check(b.Engines.Where(e => e.Team == 0).Sum(e => e.Shots) > 0, "the engines shoot");
        var r = b.Result();
        Check(r != null, "the battle reports");
        var rec = b.FinishRecording()!;
        var back = Replay.Deserialize(rec.Serialize());
        Check(back.Matches(back.Play()), "a replay with engines reproduces its battle");
    }

    /// Horse with nothing to chase rides for an engine's crew it can see.
    static void HorseRidesForEngines()
    {
        var b = Battle.Empty(15);
        var crew = b.AddSquad(0, Troops.Get("levies"), 3, new Vector2(0, -42), 3, Vector2.UnitY);
        b.AddEngine(0, "scorpion", new Vector2(0, -40), Vector2.UnitY, crew);
        var horse = b.AddSquad(1, UnitType.Lancer, 6, new Vector2(20, 0), 6, -Vector2.UnitY);
        b.Update(1 / 60f);
        Check(b.EnginePrey(horse, 0) == crew, "horse picks the engine's crew");
        crew.Works!.Hp = 0;
        Check(b.EnginePrey(horse, 0) == null, "a wrecked engine is no prey");
    }

    /// Your siege camp: works queue and get built, the walls are battered, the assault takes them along, and its odds on paper improve.
    static void SiegeCamp()
    {
        var c = Campaign.New(61);
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && s.Faction == 2).OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).First();
        c.Player.Companies.Clear();
        foreach (var t in new[] { "men_at_arms", "men_at_arms", "longbowmen", "pikemen" }) c.Player.Companies.Add(c.NewCompany(t, Rules.TroopRole(t), Rules.CapOf(t, Rules.TroopRole(t)), 0));
        c.Player.Pos = castle.Pos + new Vector2(3, 0);
        float clear = 30 * c.Map.Spread;
        foreach (var p in c.S.Parties.Where(p => p != c.Player && Vector2.Distance(p.Pos, castle.Pos) < clear)) p.Pos += Vector2.Normalize(p.Pos - castle.Pos + new Vector2(0.01f, 0)) * clear;
        Check(c.CannotBuildWork(castle, "mangonel") != null, "no works before the siege");
        c.Besiege(castle);
        c.S.Gold = 5000;
        float plain = c.WallFactor(castle, true, Array.Empty<string>());
        foreach (var id in new[] { "mangonel", "trebuchet", "ram", "tower" }) Check(c.BuildWork(castle, id) == null, "a work is ordered: " + id);
        Check(c.CannotBuildWork(castle, "mangonel") != null, "the camp holds four works at most");
        Check(c.S.Gold == 5000 - 150 - 400 - 60 - 250, "works are paid for");
        Check(c.ReadyWorks(c.Player).Count() == 0, "nothing is built at once");
        castle.Supplies = 99;
        // Allied lords stay home this week: one storming the castle would end our siege and test the world, not the works.
        foreach (var l in c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !c.Hostile(c.Player, p))) { l.Down = true; l.DownUntil = (float)c.S.Hours + 8 * 24; }
        for (double end = c.S.Hours + 24 * 7; c.S.Hours < end - 1e-3 && c.Besieging == castle;) { c.Rest(1); c.Encounter = null; c.Arrived = null; }
        Check(c.Besieging == castle, $"the siege holds for the week ({castle.Name}, realm {castle.Faction}, besieged {castle.Besieged}, men {c.Player.Men}, food {c.Player.Food:0}, supplies {castle.Supplies:0})");
        var ready = c.ReadyWorks(c.Player).ToList();
        Console.WriteLine($"siege camp: ready {string.Join(",", ready)}, walls battered {castle.Battered:0.00}, wall factor {plain:0.00} -> {c.WallFactor(castle, true, ready):0.00}");
        Check(ready.Count == 4, "a week builds all four");
        Check(castle.Battered > 0.3f, "the stone engines batter the walls");
        Check(c.WallFactor(castle, true, ready) < plain - 0.2f, "engines and a battered wall shorten the odds on paper");
        var setup = c.PrepareAssault(castle);
        Check(new[] { "mangonel", "trebuchet", "ram", "tower" }.All(id => setup.Options.Engines.Any(e => e.Team == 0 && e.Id == id)), "the assault brings the camp's works");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        Check(b.Engines.Count(e => e.Team == 0) >= 3, $"the works stand before the wall ({b.Engines.Count(e => e.Team == 0)})");
        c.LiftSiege();
        Check(c.WorksOf(c.Player).Count == 0, "lifting the siege abandons the works");
        string json = c.S.Serialize();
        Check(CampaignState.Deserialize(json).Settlements[castle.Id].Battered == castle.Battered, "a battered wall is saved");
        // Each day it stands unbesieged the wall mends (another lord may lay siege to it in between: only the end state was
        // checked, and a siege of eight of the twelve days left it battered).
        int mendDays = 0, siegeDays = 0;
        bool mended = true;
        for (int day = 0; day < 12; day++)
        {
            float before = castle.Battered;
            bool sieged = castle.Besieged;
            for (double end = c.S.Hours + 24; c.S.Hours < end - 1e-3;) { c.Rest(1); c.Encounter = null; c.Arrived = null; sieged |= castle.Besieged; }
            if (sieged) siegeDays++;
            else if (before > 0) { mendDays++; mended &= castle.Battered < before; }
        }
        Console.WriteLine($"after the siege: {siegeDays} of 12 days besieged, {mendDays} mending, battered {castle.Battered:0.00}");
        Check(mendDays > 0 && mended, "an unbesieged wall is mended");
    }

    /// Your train: bought in a town, slows the march, goes into battle, is saved, and is lost with a lost battle.
    static void CompanyTrain()
    {
        var c = Campaign.New(62);
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town);
        var castle = c.S.Settlements.First(s => s.Kind == SettlementKind.Castle);
        c.S.Gold = 3000;
        float pace = c.Speed(c.Player);
        Check(c.CannotBuyEngine(castle, "scorpion") != null, "castles sell no engines");
        Check(c.CannotBuyEngine(town, "trebuchet") != null, "a siege work is not bought");
        Check(c.BuyEngine(town, "scorpion") == null && c.BuyEngine(town, "springald") == null, "a town sells a scorpion and a springald");
        Check(c.S.Gold == 3000 - 400 - 700 && c.Player.Train.Count == 2, "they are paid for and join the train");
        Check(c.Speed(c.Player) < pace, "the train slows the march");
        Check(CampaignState.Deserialize(c.S.Serialize()).Parties.First(p => p.Kind == PartyKind.Player).Train.Count == 2, "the train is saved");
        var foe = c.S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down).OrderByDescending(p => p.Men).First();
        foe.Pos = c.Player.Pos;
        c.Encounter = foe;
        var setup = c.PrepareBattle(foe);
        Check(setup.Options.Engines.Count(e => e.Team == 0) >= 2, "the train goes into battle"); // an ally's may join it
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        Check(b.Engines.Count(e => e.Team == 0) >= 2, "both engines are set up and crewed");
        Check(c.SellEngine("springald") == null && c.Player.Train.Count == 1 && c.S.Gold == 3000 - 400 - 700 + 350, "an engine sells back for half");
        c.FinishBattle(Rules.AutoResolve(setup.Side0, setup.Side1, new Rng(new CampaignState { Seed = 3 })) with { Winner = 1, Captain = LeaderFate.Unhurt }, setup);
        Check(c.Player.Train.Count == 0, "a lost battle loses the train");
        var lord = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down).OrderByDescending(p => p.Men).First();
        Console.WriteLine($"train: pace {pace:0.00} -> slower; biggest lord ({lord.Men} men) brings {string.Join(",", c.TrainOf(lord))}");
    }
}
