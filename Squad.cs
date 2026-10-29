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
    public Vector2 Anchor, Front = Vector2.UnitY; // Anchor = centre of the front rank
    public int Columns = 8, StartCount, Alive;    // Alive = active and not routing, refreshed each tick
    public bool Loose;

    public Squad(string name, int team) { Name = name; Team = team; }

    public float Spacing => Loose ? 2.6f : 1.3f;

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

    public void HoldInPlace()
    {
        int cols = Math.Clamp(Columns, 1, Math.Max(1, Alive));
        int rows = (Alive + cols - 1) / cols;
        Anchor = Centroid() + Front * (rows - 1) * Spacing / 2;
        Order = Order.Hold;
    }
}
