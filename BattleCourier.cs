using Microsoft.Xna.Framework;

namespace Ironfield;

/// A rider carrying an order from the general to a division's sergeant (docs/archive/COMBAT_OVERHAUL.md 1.4). He is not a unit:
/// he rides the straight line so that he arrives when the order delay says, and an enemy within reach of the road cuts
/// him down. Then the order is lost and a new rider sets out from the general, so the order lands later; each new rider
/// swings wider round the danger (alternate sides), so a blocked road slows orders but does not stop them.
public sealed class Courier
{
    public Division Div = null!;
    /// True for a goal; false for direct orders to the division's companies.
    public bool Goal;
    public Vector2 From, Pos;
    public float Start, At;
    public int Lost;
}

public partial class Battle
{
    public readonly List<Courier> Couriers = new();
    /// Seconds a fresh rider is safe while he gets clear of his own lines.
    const float CourierGrace = 1.5f, CourierReach = 2.5f;
    readonly List<Unit> nearCourier = new();

    Vector2 GeneralPos(int team)
    {
        var general = team == 0 ? Player : EnemyCaptain;
        return general.Active ? general.Pos : new Vector2(0, -rearLine[team] * (team == 0 ? 1 : -1));
    }

    Vector2 CourierGoal(Division d) =>
        d.Leader is { Active: true } l ? l.Pos : d.Squads.Where(s => s.Alive > 0).Select(s => s.Center).DefaultIfEmpty(GeneralPos(d.Team)).First();

    void SendCourier(Division d, float at, bool goal)
    {
        if (!Options.Couriers || at - Time < CourierGrace + 0.5f) return;
        var from = GeneralPos(d.Team);
        Couriers.Add(new Courier { Div = d, Goal = goal, From = from, Pos = from, Start = Time, At = at });
    }

    /// Move the riders; one with an enemy in reach is cut down and the order he carried waits for the next rider.
    void UpdateCouriers()
    {
        for (int i = 0; i < Couriers.Count; i++)
        {
            var c = Couriers[i];
            if (Time >= c.At) { Couriers.RemoveAt(i--); continue; }
            float k = Math.Clamp((Time - c.Start) / MathF.Max(0.01f, c.At - c.Start), 0, 1);
            var to = CourierGoal(c.Div);
            var side = to - c.From;
            side = side.LengthSquared() > 1 ? Vector2.Normalize(new Vector2(-side.Y, side.X)) : Vector2.UnitX;
            float swing = c.Lost == 0 ? 0 : (c.Lost % 2 == 1 ? 1 : -1) * 30 * ((c.Lost + 1) / 2);
            var bend = (c.From + to) / 2 + side * swing; // a curve through a point off the straight road
            c.Pos = Vector2.Lerp(Vector2.Lerp(c.From, bend, k), Vector2.Lerp(bend, to, k), k);
            if (Time - c.Start < CourierGrace || !Caught(c)) continue;
            float again = Time + OrderDelay(c.Div) * (1 + 0.25f * (c.Lost + 1)); // the long way round
            if (c.Goal) { if (c.Div.Pending != null && c.Div.PendingAt == c.At) c.Div.PendingAt = again; }
            else
                for (int j = 0; j < directs.Count; j++)
                    if (directs[j].At == c.At && directs[j].S.Division == c.Div) directs[j] = (again, directs[j].S, directs[j].Act);
            if (c.Div.Team == 0) Log($"Your rider to {c.Div.Name} is cut down. Another rides out.");
            Sound(Cue.Hit, c.Pos);
            c.From = c.Pos = GeneralPos(c.Div.Team);
            c.Start = Time;
            c.At = again;
            c.Lost++;
        }
    }

    bool Caught(Courier c)
    {
        foreach (var u in Near(c.Pos, CourierReach, nearCourier))
            if (u.Team != c.Div.Team && !u.Routing && Vector2.DistanceSquared(u.Pos, c.Pos) < CourierReach * CourierReach) return true;
        return false;
    }
}
