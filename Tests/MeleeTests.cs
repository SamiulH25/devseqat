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
        a.Drill = 0.95f; // a well drilled company: none of its back ranks break out
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
}
