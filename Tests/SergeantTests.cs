using Microsoft.Xna.Framework;

namespace Ironfield;

/// Sergeants that matter (docs/SERGEANTS.md SG1-SG7).
public static partial class SelfTest
{
    static ArmySpec BigArmy(int seed, int team) => Armies.Divide(Armies.Large(new Random(seed), 2, team, 2000));

    /// SG1: in deployment companies move between the captain and his sergeants within the caps, the reserve is called up
    /// (at once before the battle, by rider during it), nothing changes hands after the start, and it all replays.
    static void CommandTray()
    {
        var army = BigArmy(21, 0);
        // Two of the first sergeant's companies are left to the captain, so the sergeant has room and the captain is full.
        var comps = army.Companies.ToList();
        foreach (int i in Enumerable.Range(0, comps.Count).Where(i => comps[i].Division == 0).Take(2).ToList()) comps[i] = comps[i] with { Division = -1 };
        var b = new Battle(21, army with { Companies = comps }, BigArmy(22, 1), new BattleOptions { Cap = 4000, AutoPlayer = true, PlayerWounded = true });
        b.StartRecording(21);
        var div = b.DivisionsOf(0).First();
        int led = b.LedCount(0);
        Check(led == Armies.CommandCap && b.ReservesOf(0).Count > 2, $"a big army fills the captain's places and holds the rest back ({led} led, {b.ReservesOf(0).Count} waiting)");
        Check(b.CannotCallUp(0, 0, null) != null, "no reserve company comes to a captain with no free place");
        var mine = b.Commanded.First();
        Check(b.Assign(mine, div) == null && mine.Division == div && b.LedCount(0) == led - 1, "a company moves to a sergeant in deployment");
        Check(b.CallUp(0, 0, null) == null && b.LedCount(0) == led, "the freed place takes a company from the reserve at once");
        var full = b.DivisionsOf(0).OrderByDescending(d => d.Squads.Count).First();
        while (full.Squads.Count < Armies.DivisionCap && b.ReservesOf(0).Count > 0 && b.CallUp(0, 0, full) == null) { }
        Check(b.CannotCallUp(0, 0, full) != null && b.CannotAssign(b.Commanded.First(), full) != null, $"a sergeant leads at most {Armies.DivisionCap} companies");
        b.Update(1 / 60f);
        Check(b.CannotAssign(b.Commanded.First(), div) != null, "nothing changes hands once the battle has begun");
        var spare = b.DivisionsOf(0).First(d => d.Squads.Count < Armies.DivisionCap);
        int before = spare.Squads.Count;
        Check(b.CallUp(0, 0, spare) == null && b.CallUpsOf(0).Count() == 1, "during the battle a rider goes back for a company");
        Run(b, Battle.CallUpDelay - 1);
        Check(spare.Squads.Count == before, "it is not there before the rider is back");
        Run(b, 2);
        Check(spare.Squads.Count == before + 1, "it marches on to its sergeant");
        b.SoundHorn(0);
        for (int i = 0; i < 60 * 70 && b.Units.Count(u => u.Dead) < 20; i++) b.Update(1 / 60f);
        var rec = b.FinishRecording()!;
        var again = Replay.Deserialize(rec.Serialize()).Play();
        Console.WriteLine($"command tray: {b.Squads.Count} companies, {b.Units.Count(u => u.Dead)} dead; replay {again.Units.Count(u => u.Dead)} dead");
        Check(b.Units.Count(u => u.Dead) > 0 && again.Squads.Count == b.Squads.Count && again.Units.Count(u => u.Dead) == b.Units.Count(u => u.Dead) && again.Time == b.Time, "assignments, call-ups and the horn replay exactly");
    }

    /// A division of one company 60 m off on an empty field, and an enemy company far away.
    static (Battle B, Division D, Squad S) OneDivision(int seed, Vector2 at, Temper temper = Temper.Steady, int skill = 5)
    {
        var b = Battle.Empty(seed);
        var s = b.AddSquad(0, Troops.Get("levies"), 20, at, 10, Vector2.UnitY);
        var d = b.AddDivision(0, "Hal", new[] { s }, temper, skill);
        return (b, d, s);
    }

    /// SG2: a plan set before the battle waits for the horn and then lands at once at any distance; one set during the
    /// battle rides out first; the plan is spent when sounded.
    static void HornPlans()
    {
        var (b, d, s) = OneDivision(3, new Vector2(60, 0));
        b.AddSquad(1, Troops.Get("levies"), 10, new Vector2(60, 120), 5, -Vector2.UnitY);
        b.SetHornPlan(d, new Goal(GoalKind.Take, new Vector2(60, 30)));
        Check(d.Horn?.Kind == GoalKind.Take, "a plan set in deployment is his at once");
        Run(b, 3);
        Check(d.Goal.Kind == GoalKind.Hold && b.Report(d) == "AWAITS THE HORN", $"he holds and waits for the horn ({b.Report(d)})");
        Check(b.SoundHorn(0) == 1 && d.Horn == null, "the horn sends the division that had a plan");
        Run(b, 1);
        Check(d.Goal.Kind == GoalKind.Take, "and the plan lands within a second, with no rider");
        float late = b.SetHornPlan(d, new Goal(GoalKind.FallBack, new Vector2(60, -20)));
        Check(late > 1 && d.Horn == null && d.PendingHorn != null, $"a plan given during the battle rides out ({late:0.0}s)");
        Run(b, late + 0.5f);
        Check(d.Horn?.Kind == GoalKind.FallBack, "and becomes his plan when the rider arrives");
    }

    /// SG3: a sergeant whose flank is threatened by horse asks; unanswered he decides by his temper; answered, the
    /// answer rides out to him. He does not ask again at once.
    static void SergeantsAsk()
    {
        Query? Ask(Battle b, Division d) { for (int i = 0; i < 60 * 4 && d.Query == null; i++) b.Update(1 / 60f); return d.Query; }
        foreach (var temper in new[] { Temper.Steady, Temper.Cautious })
        {
            var (b, d, s) = OneDivision(4, new Vector2(0, 0), temper);
            var horse = b.AddSquad(1, Troops.Get("lancers"), 8, new Vector2(-38, 4), 4, Vector2.UnitX);
            horse.Order = Order.Charge; horse.Target = s;
            var q = Ask(b, d);
            Check(q != null && q.Text.Contains("horse"), $"a {temper} sergeant asks when horse comes at his flank ({q?.Text})");
            Check(b.Report(d) == "ASKS FOR ORDERS", "his card says he is asking");
            Run(b, Battle.QueryWait + 1);
            var want = temper == Temper.Cautious ? GoalKind.FallBack : GoalKind.Hold;
            Check(d.Query == null && d.Goal.Kind == want, $"unanswered, a {temper} sergeant chooses {want} himself ({d.Goal.Kind})");
        }
        {
            var (b, d, s) = OneDivision(5, new Vector2(0, 0));
            var horse = b.AddSquad(1, Troops.Get("lancers"), 8, new Vector2(-38, 4), 4, Vector2.UnitX);
            horse.Order = Order.Charge; horse.Target = s;
            Ask(b, d);
            Check(b.Answer(d, false) == null && d.Pending?.Kind == GoalKind.FallBack && d.Query == null, "your answer rides out to him as a goal");
            Run(b, 3);
            Check(d.Query == null, "he does not ask again at once");
        }
    }

    /// SG4: a rabble company under a skilled sergeant thinks like veterans; once he falls it is rabble again.
    static void SergeantLiftsWits()
    {
        var (b, d, s) = OneDivision(6, Vector2.Zero, skill: 8);
        s.Drill = 0.1f;
        Check(s.Grade == Grade.Rabble && Battle.WitsGrade(s) == Grade.Elite, "a skill 8 sergeant gives rabble an elite's wits");
        Check(Battle.SergeantGrade(3) == Grade.Trained && Battle.SergeantGrade(5) == Grade.Veteran && Battle.SergeantGrade(2) == Grade.Rabble, "sergeant grade by skill");
        d.Leader!.Hp = 0; d.Leader.Dead = true;
        Check(Battle.WitsGrade(s) == Grade.Rabble, "without him they are rabble again");
    }

    /// SG5: near the sergeant your word is shouted; far off a rider takes it, and later while you are fighting.
    static void VoiceOrders()
    {
        var (b, d, _) = OneDivision(7, new Vector2(0, 0));
        b.Player.Pos = new Vector2(8, 0);
        Check(b.InVoice(d) && MathF.Abs(b.OrderDelay(d) - 0.6f) < 1e-3f, "in earshot the order is shouted");
        b.Player.Pos = new Vector2(0, -90);
        float far = b.OrderDelay(d);
        Check(!b.InVoice(d) && far > 2, $"far off a rider takes it ({far:0.0}s)");
        b.Player.EnemyDist = 2;
        Check(b.OrderDelay(d) > far + 2, "and longer while you fight");
    }

    /// SG7: quick-battle sergeants have a nature; a Drillmaster drills his men harder; a Veteran thinks a grade up.
    static void SergeantNatures()
    {
        var army = BigArmy(8, 0);
        var b = new Battle(8, army, BigArmy(9, 1), new BattleOptions { Cap = 4000 });
        Check(b.DivisionsOf(0).All(d => d.Traits.Length > 0), "every quick-battle sergeant has a nature");
        var bare = army with { Divisions = army.Divisions!.Select(x => x with { Traits = Array.Empty<string>() }).ToList() };
        var plain = new Battle(8, bare, BigArmy(9, 1), new BattleOptions { Cap = 4000 });
        var drill = b.DivisionsOf(0).First(d => d.Has("Drillmaster"));
        var same = plain.Divisions[b.Divisions.IndexOf(drill)];
        Check(drill.Squads[0].Drill > same.Squads[0].Drill, $"a Drillmaster drills his men harder ({drill.Squads[0].Drill:0.00} vs {same.Squads[0].Drill:0.00})");
        if (b.DivisionsOf(0).FirstOrDefault(d => d.Has("Veteran")) is { } vet)
            Check(vet.Skill == plain.Divisions[b.Divisions.IndexOf(vet)].Skill + 1, "a Veteran sergeant counts a point more skill");
    }
}
