using Microsoft.Xna.Framework;

namespace Ironfield;

public enum Order { Hold, Follow, Charge }

/// A group of units that takes orders together and keeps a rectangular formation.
public sealed class Squad
{
    public readonly string Name;
    public readonly int Team;
    public readonly List<Unit> Units = new();
    public Order Order = Order.Hold;
    public Squad? Target;                          // Charge at this squad (null = nearest enemy)
    public Vector2 Anchor, Front = Vector2.UnitY; // Anchor = centre of the front rank
    public int Columns = 8, StartCount, Alive;    // Alive = active and not routing, refreshed each tick
    public bool Loose, Cavalry, FireAtWill = true; // FireAtWill off: archers hold their arrows
    public float Pressure;                        // recent flank/rear hits; drains morale, decays over time
    public int Kills;                             // enemies this company has killed (campaign XP)
    public readonly int[] KillsByTier = new int[5]; // kills by the victim's tier (0 = leaders)
    public Company? Company;
    public bool RoutSounded;                      // the rout horn has blown for this company
    public bool AiControlled;                     // an allied lord's company: the AI general leads it, not you

    public Squad(string name, int team) { Name = name; Team = team; }

    public float Spacing => Cavalry ? (Loose ? 4.5f : 3.0f) : Loose ? 2.6f : 1.3f;

    public Vector2 Slot(int i, int count)
    {
        int cols = Math.Clamp(Columns, 1, Math.Max(1, count));
        int row = i / cols, col = i % cols;
        var right = new Vector2(-Front.Y, Front.X);
        return Anchor + right * (col - (cols - 1) / 2f) * Spacing - Front * row * Spacing;
    }

    public Vector2 Centroid()
    {
        var sum = Vector2.Zero;
        int n = 0;
        foreach (var u in Units) if (u.Active && !u.Routing) { sum += u.Pos; n++; }
        return n > 0 ? sum / n : Anchor;
    }

    /// Form a line from a to b, front facing whichever side `toward` is on.
    public void SetLine(Vector2 a, Vector2 b, Vector2 toward)
    {
        Order = Order.Hold;
        Target = null;
        var center = (a + b) / 2;
        float width = Vector2.Distance(a, b);
        Anchor = center;
        if (width < 1)
        {
            var t = toward - center;
            if (t.LengthSquared() > 1e-4f) Front = Vector2.Normalize(t);
            return;
        }
        var d = (b - a) / width;
        var perp = new Vector2(-d.Y, d.X);
        Front = Vector2.Dot(perp, toward - center) >= 0 ? perp : -perp;
        Columns = (int)(width / Spacing) + 1;
    }

    /// Hold position, then move the formation `metres` along its front (negative = fall back), facing `toward`.
    public void Shift(Vector2 toward, float metres)
    {
        HoldInPlace();
        var to = toward - Anchor;
        if (to.LengthSquared() > 1) Front = Vector2.Normalize(to);
        Anchor += Front * metres;
    }

    public void HoldInPlace()
    {
        int cols = Math.Clamp(Columns, 1, Math.Max(1, Alive));
        int rows = (Alive + cols - 1) / cols;
        Anchor = Centroid() + Front * (rows - 1) * Spacing / 2;
        Order = Order.Hold;
        Target = null;
    }
}
