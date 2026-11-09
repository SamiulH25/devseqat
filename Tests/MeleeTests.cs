using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of melee from the ranks and company tactics (docs/MELEE.md): lines that hold, rear ranks that wait,
/// men stepping into gaps, companies fighting their own foe.
public static partial class SelfTest
{
    /// Two companies of `troop` 30 m apart, charging each other: (yours, theirs).
    static (Battle b, Squad a, Squad e) Clash(int seed, string troopA, string troopE, int n = 24, int cols = 8, float gap = 24)
    {
        var b = Battle.Empty(seed);
        var a = b.AddSquad(0, Troops.Get(troopA), n, new(0, -gap / 2), cols, Vector2.UnitY);
        var e = b.AddSquad(1, Troops.Get(troopE), n, new(0, gap / 2), cols, -Vector2.UnitY);
        a.Order = e.Order = Order.Charge;
        a.Target = e;
        e.Target = a;
        return (b, a, e);
    }

    /// M1: two companies meet as two lines and fight front to front, not as one blob.
    static void HoldsRanks()
    {
        var (b, a, e) = Clash(41, "ironguard", "ironguard");
        float err = 0, mix = 0;
        int samples = 0;
        for (int i = 0; i < 60 * 25; i++)
        {
            b.Update(1 / 60f);
            if (i >= 60 * 8 && i % 30 == 0) { err += Battle.RankError(a) + Battle.RankError(e); mix += Battle.Intermix(a, e) + Battle.Intermix(e, a); samples += 2; }
        }
        err /= samples; mix /= samples;
        Console.WriteLine($"melee: ironguard clash, rank error {err:0.00} m, intermix {mix:P0}, {a.Alive} vs {e.Alive} standing");
        Check(err < 2f, $"drilled companies keep their ranks in contact ({err:0.00} m off their places)");
        Check(mix < 0.15f, $"the lines do not soak into each other ({mix:P0} mixed)");
        Check(a.Alive < a.StartCount && e.Alive < e.StartCount, "and they fight");
    }

    /// M1: in the first seconds of contact the back ranks of a company hold their places: only the front fights.
    static void RearRanksWait()
    {
        var (b, a, e) = Clash(42, "ironguard", "ironguard");
        a.Drill = e.Drill = 0.95f; // well drilled on both sides: no stragglers come round to the back ranks
        bool contact = false;
        float since = 0;
        int rearBlows = 0, frontBlows = 0;
        for (int i = 0; i < 60 * 30 && since < 5; i++)
        {
            b.Update(1 / 60f);
            foreach (var u in a.Units)
                if (u.Active && u.Action == UnitAction.Windup && u.ActionTimer >= u.Type.Windup - 1 / 60f - 1e-4f) { if (u.Rank == 0) frontBlows++; else if (u.Rank >= 2) rearBlows++; }
            if (!contact && frontBlows > 0) contact = true;
            if (contact) since += 1 / 60f;
        }
        Console.WriteLine($"melee: first 5 s of contact, front-rank swings {frontBlows}, back-rank swings {rearBlows}");
        Check(contact && frontBlows > 0, "the front rank fights");
        Check(rearBlows == 0, $"the back ranks wait their turn ({rearBlows} swings from the third rank back)");
    }

    /// M1: a front-rank man falls; the man behind him in his file steps into his place and the rest stay put.
    static void StepsUp()
    {
        var b = Battle.Empty(43);
        var s = b.AddSquad(0, Troops.Get("men_at_arms"), 24, Vector2.Zero, 8, Vector2.UnitY);
        Run(b, 3);
        var fallen = s.Units.First(u => u.Rank == 0 && u.Place == 3);
        var behind = s.Units.First(u => u.Place == 3 + 8);
        var before = s.Units.ToDictionary(u => u, u => u.Slot);
        fallen.Dead = true;
        Run(b, 3);
        Check(behind.Place == 3 && Vector2.Distance(behind.Slot, before[fallen]) < 0.01f, "the man behind takes the fallen man's place");
        int moved = s.Units.Count(u => u.Active && u != behind && Vector2.Distance(u.Slot, before[u]) > 0.5f);
        Check(moved <= 2, $"the rest keep their places ({moved} others moved)");
    }

    /// M1: two companies side by side charge the two opposite them: nearly every blow lands on the company each was sent at.
    static void FightsItsTarget()
    {
        var b = Battle.Empty(44);
        var a1 = b.AddSquad(0, Troops.Get("men_at_arms"), 16, new(-7, -12), 8, Vector2.UnitY);
        var a2 = b.AddSquad(0, Troops.Get("men_at_arms"), 16, new(7, -12), 8, Vector2.UnitY);
        var e1 = b.AddSquad(1, Troops.Get("men_at_arms"), 16, new(-7, 12), 8, -Vector2.UnitY);
        var e2 = b.AddSquad(1, Troops.Get("men_at_arms"), 16, new(7, 12), 8, -Vector2.UnitY);
        foreach (var s in new[] { a1, a2, e1, e2 }) s.Order = Order.Charge;
        (a1.Target, a2.Target, e1.Target, e2.Target) = (e1, e2, a1, a2);
        int own = 0, other = 0;
        for (int i = 0; i < 60 * 20; i++)
        {
            b.Update(1 / 60f);
            foreach (var s in new[] { a1, a2 })
                foreach (var u in s.Units)
                    if (u.Active && u.Action == UnitAction.Windup && u.ActionTimer >= u.Type.Windup - 1 / 60f - 1e-4f && u.Target?.Squad is Squad t)
                        { if (t == s.Target) own++; else other++; }
        }
        float share = own / (float)Math.Max(1, own + other);
        Console.WriteLine($"melee: {own} swings at their own targets, {other} at others ({share:P0})");
        Check(own > 0 && share > 0.9f, $"companies fight the company they were sent at ({share:P0})");
    }

    /// Rank error and intermix of a clash of `troop`, averaged from 8 s to 25 s.
    static (float err, float mix) ClashOrder(int seed, string troop)
    {
        var (b, a, e) = Clash(seed, troop, troop);
        float err = 0, mix = 0;
        int n = 0;
        for (int i = 0; i < 60 * 25; i++)
        {
            b.Update(1 / 60f);
            if (i >= 60 * 8 && i % 30 == 0) { err += Battle.RankError(a) + Battle.RankError(e); mix += Battle.Intermix(a, e) + Battle.Intermix(e, a); n += 2; }
        }
        return (err / n, mix / n);
    }

    /// M2: levies start in a line but cannot keep it at blows; drilled men can.
    static void LeviesLoseRanks()
    {
        var levies = ClashOrder(45, "levies");
        var drilled = ClashOrder(45, "ironguard");
        Console.WriteLine($"melee: levies rank error {levies.err:0.00} m, intermix {levies.mix:P0}; ironguard {drilled.err:0.00} m, {drilled.mix:P0}");
        Check(Battle.GradeOf(Battle.DrillOf(Troops.Get("levies"))) == Grade.Rabble && Battle.GradeOf(Battle.DrillOf(Troops.Get("immortals"))) == Grade.Elite, "levies are rabble, tier 7 elite");
        Check(levies.err > drilled.err * 1.5f && levies.mix > drilled.mix, "levies end up a crowd, drilled men a line");
        var (b, a, _) = Clash(46, "levies", "levies");
        for (int i = 0; i < 60 * 20 && !a.Broken; i++) b.Update(1 / 60f);
        Check(a.Broken, "a rabble's ranks break in a long melee");
    }

    /// M2: drill wins fights. The same men, one side drilled like elites, the other like rabble: at even numbers the
    /// drilled side wins, and clearly (a fifth of its men or more still standing). Outnumbered: ElitePincer (M4).
    static void TierWins()
    {
        foreach (int foes in new[] { 24 })
        {
            int won = 0;
            for (int seed = 0; seed < 3; seed++)
            {
                var b = Battle.Empty(50 + seed);
                var a = b.AddSquad(0, Troops.Get("men_at_arms"), 24, new(0, -12), 8, Vector2.UnitY);
                var e = b.AddSquad(1, Troops.Get("men_at_arms"), foes, new(0, 12), 8, -Vector2.UnitY);
                a.Drill = 0.92f;
                e.Drill = 0.22f;
                a.Order = e.Order = Order.Charge;
                a.Target = e; e.Target = a;
                for (int i = 0; i < 60 * 120 && a.Alive > 0 && e.Alive > 0; i++) b.Update(1 / 60f);
                Console.WriteLine($"melee: drilled 24 vs rabble {foes} (seed {seed}): {a.Alive} vs {e.Alive} standing after {b.Time:0} s");
                if (a.Alive > e.Alive && a.Alive >= 5) won++;
            }
            Check(won >= 2, $"drilled men clearly beat as many rabble of the same troop ({won} of 3)");
        }
    }

    /// M3: the AI line goes in company by company, each when it nears the company opposite, not all at once.
    static void PartsByCompany()
    {
        var rng = new Random(5);
        var mine = Armies.Random(rng, 3, 0);
        var theirs = Armies.Random(rng, 3, 1);
        theirs = theirs with { Companies = theirs.Companies.Concat(new[] { new Company("men_at_arms", 16, "infantry"), new Company("men_at_arms", 16, "infantry") }).ToList() };
        var b = new Battle(21, mine, theirs, new BattleOptions { AutoPlayer = true, PlayerWounded = true });
        var first = new Dictionary<Squad, float>();
        for (int step = 0; b.Winner < 0 && step < 60 * 240; step++)
        {
            b.Update(1 / 60f);
            foreach (var s in b.SquadsOf(1))
                if (s.Company != null && Armies.RoleOf(s.Company) is "infantry" or "spears" && s.Order == Order.Charge && !first.ContainsKey(s)) first[s] = b.Time;
        }
        var times = first.Values.OrderBy(t => t).ToList();
        Console.WriteLine($"melee: enemy line companies charge at {string.Join(", ", times.Select(t => t.ToString("0.0")))} s");
        Check(times.Count >= 3 && times[^1] - times[0] > 1.5f, "the line's companies charge at different times");
    }

    /// M3: levies holding near the enemy break ranks and charge on their own; a sergeant beside them keeps them in hand.
    static void LeviesBreakRanks()
    {
        bool Breaks(bool sergeant)
        {
            var b = Battle.Empty(47);
            var lev = b.AddSquad(0, Troops.Get("levies"), 20, new(0, -6), 10, Vector2.UnitY);
            b.AddSquad(1, Troops.Get("men_at_arms"), 20, new(0, 6), 10, -Vector2.UnitY);
            if (sergeant) b.AddDivision(0, "Sergeant Test", new[] { lev }, Temper.Cautious, 3, new Goal(GoalKind.Hold, new Vector2(0, -6)));
            for (int i = 0; i < 60 * 15 && lev.Order == Order.Hold; i++) b.Update(1 / 60f);
            return lev.Order == Order.Charge && lev.BrokeRanks;
        }
        bool alone = Breaks(false), led = Breaks(true);
        Console.WriteLine($"melee: levies 10 m from the enemy break ranks alone {alone}, with a sergeant {led}");
        Check(alone, "levies near the enemy break ranks and charge");
        Check(!led, "a sergeant beside them holds them");
        var c = Battle.Empty(48);
        var drilled = c.AddSquad(0, Troops.Get("men_at_arms"), 20, new(0, -6), 10, Vector2.UnitY);
        c.AddSquad(1, Troops.Get("men_at_arms"), 20, new(0, 6), 10, -Vector2.UnitY);
        Run(c, 15);
        Check(drilled.Order == Order.Hold, "trained men hold until told");
    }

    /// M4: a trained company holding its ground wheels to face an enemy coming at its flank; rabble do not wheel.
    static void RefusesFlank()
    {
        (float dot, Plan plan) Face(string troop)
        {
            var b = Battle.Empty(51);
            var s = b.AddSquad(0, Troops.Get(troop), 24, Vector2.Zero, 8, Vector2.UnitY);
            s.HoldInPlace();
            var e = b.AddSquad(1, Troops.Get("men_at_arms"), 16, new(24, 0), 8, -Vector2.UnitX);
            e.Order = Order.Charge;
            e.Target = s;
            var plan = Plan.None;
            for (int i = 0; i < 60 * 20 && !s.InContact; i++) { b.Update(1 / 60f); if (s.Plan == Plan.Refuse) plan = Plan.Refuse; }
            return (Vector2.Dot(s.Front, Vector2.Normalize(e.Center - s.Center)), plan);
        }
        var trained = Face("men_at_arms");
        var rabble = Face("levies");
        Console.WriteLine($"melee: struck from the flank, men-at-arms face it {trained.dot:0.00} ({trained.plan}), levies {rabble.dot:0.00}");
        Check(trained.dot > 0.8f && trained.plan == Plan.Refuse, "a trained company turns to face a flank attack");
        Check(rabble.plan != Plan.Refuse, "rabble do not see it coming (they may break ranks and charge it, but they do not wheel)");
    }

    /// One wide company against a narrow one of the same men: (blows in the flank or rear, own alive, foe alive), drill set.
    static (int flank, int mine, int theirs, float bend) WideClash(float drill, int seed)
    {
        var b = Battle.Empty(seed);
        var a = b.AddSquad(0, Troops.Get("men_at_arms"), 32, new(0, -12), 16, Vector2.UnitY);
        var e = b.AddSquad(1, Troops.Get("men_at_arms"), 24, new(0, 12), 6, -Vector2.UnitY);
        a.Drill = drill;
        e.Drill = 0.6f;
        e.HoldInPlace();
        a.Order = Order.Charge;
        a.Target = e;
        float bend = 0;
        for (int i = 0; i < 60 * 25; i++) { b.Update(1 / 60f); bend = MathF.Max(bend, a.Bend); }
        return (a.FlankHits, a.Alive, e.Alive, bend);
    }

    /// M4: a veteran company wider than its foe bends its outer files round his ends, strikes his flanks and trades better
    /// than a trained one (whose men spill round the ends as a crowd).
    static void VeteransWrap()
    {
        var vet = WideClash(0.75f, 52);
        var trained = WideClash(0.45f, 52);
        Console.WriteLine($"melee: wide company on a narrow one, veterans bend {vet.bend:0.00}, {vet.flank} flank blows ({vet.mine} vs {vet.theirs}); trained {trained.flank} ({trained.mine} vs {trained.theirs})");
        Check(vet.bend > 0.3f && trained.bend == 0, "veterans wrap round the ends, trained men do not");
        float Trade((int flank, int mine, int theirs, float bend) r) => (24f - r.theirs) / MathF.Max(1, 32 - r.mine);
        Check(vet.flank >= 10, $"and land blows in the foe's flanks ({vet.flank})");
        Check(Trade(vet) > Trade(trained), $"and trade better ({Trade(vet):0.0} kills a loss against {Trade(trained):0.0})");
    }

    /// M4: two elite companies against one: the one behind goes round to his flank while the other pins him. And elites
    /// beat more men than themselves: 24 against 31 of the same troop at a rabble's drill.
    static void ElitePincer()
    {
        var b = Battle.Empty(53);
        var a1 = b.AddSquad(0, Troops.Get("men_at_arms"), 12, new(0, -10), 6, Vector2.UnitY);
        var a2 = b.AddSquad(0, Troops.Get("men_at_arms"), 12, new(-4, -22), 6, Vector2.UnitY);
        var e = b.AddSquad(1, Troops.Get("men_at_arms"), 16, new(0, 10), 8, -Vector2.UnitY);
        a1.Drill = a2.Drill = 0.92f;
        e.HoldInPlace();
        a1.Order = a2.Order = Order.Charge;
        bool flanked = false;
        for (int i = 0; i < 60 * 30; i++) { b.Update(1 / 60f); flanked |= a2.Plan == Plan.Flank; }
        Console.WriteLine($"melee: elite pincer, second company flanks {flanked}, {a2.FlankHits} flank blows, {a1.FlankHits} from the first");
        Check(flanked && a2.FlankHits > 5, "the second elite company takes the pinned foe in the flank");
        int won = 0;
        for (int seed = 0; seed < 3; seed++)
        {
            var c = Battle.Empty(60 + seed);
            var x = c.AddSquad(0, Troops.Get("men_at_arms"), 24, new(0, -12), 8, Vector2.UnitY);
            var y = c.AddSquad(1, Troops.Get("men_at_arms"), 31, new(0, 12), 8, -Vector2.UnitY);
            x.Drill = 0.92f;
            y.Drill = 0.22f;
            x.Order = y.Order = Order.Charge;
            x.Target = y; y.Target = x;
            for (int i = 0; i < 60 * 150 && x.Alive > 0 && y.Alive > 0; i++) c.Update(1 / 60f);
            Console.WriteLine($"melee: 24 elites vs 31 rabble (seed {seed}): {x.Alive} vs {y.Alive} after {c.Time:0} s");
            if (x.Alive > y.Alive) won++;
        }
        Check(won >= 2, $"elites beat a third more men of their own troop ({won} of 3)");
    }

    /// M4: a veteran company whose ranks broke on the way in steps back, re-forms, and goes in again at the same foe.
    static void Reforms()
    {
        var b = Battle.Empty(54);
        var a = b.AddSquad(0, Troops.Get("men_at_arms"), 24, new(0, -20), 8, Vector2.UnitY);
        var e = b.AddSquad(1, Troops.Get("men_at_arms"), 24, new(0, 20), 8, -Vector2.UnitY);
        a.Drill = 0.75f;
        e.HoldInPlace();
        a.Order = Order.Charge;
        a.Target = e;
        Run(b, 1);
        a.Formed = 0.3f;
        a.Broken = true;
        bool fellBack = false;
        for (int i = 0; i < 60 * 20 && !(fellBack && a.Order == Order.Charge); i++) { b.Update(1 / 60f); fellBack |= a.Plan == Plan.Reform && a.Order == Order.Hold; }
        Console.WriteLine($"melee: broken veterans fall back {fellBack}, formed {a.Formed:0.00}, going in again {a.Order} at {a.Target?.Name}");
        Check(fellBack, "broken veterans stop to re-form");
        Check(a.Order == Order.Charge && a.Target == e && !a.Broken, "then go in again at the same foe");
    }
}
