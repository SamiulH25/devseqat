using Microsoft.Xna.Framework;

namespace Ironfield;

public enum Order { Hold, Follow, Charge }

/// How a company stands (roadmap C1). Line is the default; ShieldWall locks shields (blocks more, braces, moves slowly);
/// Square faces every way (horse cannot find a flank, but arrows find the crowd); Wedge drives into a line (more shock);
/// Loose opens the ranks (arrows miss, horse cuts through).
public enum Formation { Line, ShieldWall, Square, Wedge, Loose }

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
    public bool Cavalry, FireAtWill = true;        // FireAtWill off: archers hold their arrows
    public Formation Formation;
    public bool Loose
    {
        get => Formation == Formation.Loose;
        set => Formation = value ? Formation.Loose : Formation == Formation.Loose ? Formation.Line : Formation;
    }
    /// A swarm of craven creatures (goblins): it fights as a loose mob and never forms a shield wall.
    public bool Swarm => Units.Count > 0 && Units[0].Type.Bound;
    public float Pressure;                        // recent flank/rear hits; drains morale, decays over time
    public int Kills;                             // enemies this company has killed (campaign XP)
    public readonly int[] KillsByTier = new int[8]; // kills by the victim's tier (0 = leaders, 1-7 = troop tiers)
    public int HorseKills;                        // mounted enemies killed (the Horse-breakers honour)
    /// The pace the company keeps in formation: its slowest man's (a mixed company marches together).
    public float MarchSpeed = float.MaxValue;
    public Company? Company;
    public bool RoutSounded;                      // the rout horn has blown for this company
    public bool AiControlled;                     // an allied lord's company: the AI general leads it, not you
    /// The division this company belongs to (null: you, or the AI general, command it directly).
    public Division? Division;
    /// A division's company following your direct order: its sergeant leaves it alone until his next goal arrives.
    public bool Override;
    /// Charging men also cut down routers (a division told to pursue).
    public bool Pursue;
    /// A circle round every active man (routing too), refreshed at the start of each tick: cheap squad-to-squad distance bounds.
    public Vector2 Center;
    public float Radius;

    public Squad(string name, int team) { Name = name; Team = team; }

    public float Spacing => Formation switch
    {
        Formation.Loose => Cavalry ? 4.5f : 2.6f,
        Formation.ShieldWall => 1.0f,
        Formation.Wedge => Cavalry ? 2.6f : 1.4f,
        _ => Cavalry ? 3.0f : 1.3f,
    };

    /// Ground speed multiplier for the formation (shield walls shuffle; squares barely move).
    public float Pace => Formation switch { Formation.ShieldWall => 0.6f, Formation.Square => 0.45f, _ => 1 };

    /// The formations this company can take, in the order J cycles them.
    public Formation[] Choices(bool shields) => Cavalry ? new[] { Formation.Line, Formation.Wedge, Formation.Loose }
        : shields ? new[] { Formation.Line, Formation.ShieldWall, Formation.Square, Formation.Wedge, Formation.Loose }
        : new[] { Formation.Line, Formation.Square, Formation.Wedge, Formation.Loose };

    /// Which way a man in slot `slot` faces: out from the centre in a square, else the company's front.
    public Vector2 FaceAt(Vector2 slot)
    {
        if (Formation != Formation.Square) return Front;
        var d = slot - Anchor;
        return d.LengthSquared() > 0.01f ? Vector2.Normalize(d) : Front;
    }

    public Vector2 Slot(int i, int count)
    {
        var rt = new Vector2(-Front.Y, Front.X);
        if (Formation == Formation.Square && count >= 4)
        {
            // A hollow square around the anchor, men spread evenly round its edge.
            int side = Math.Max(2, (count + 3) / 4);
            float half = side * Spacing / 2;
            float t = i / (float)count * 4, edge = MathF.Floor(t), k = t - edge;
            var (a, b) = ((int)edge % 4) switch
            {
                0 => (Front * half - rt * half, Front * half + rt * half),
                1 => (Front * half + rt * half, -Front * half + rt * half),
                2 => (-Front * half + rt * half, -Front * half - rt * half),
                _ => (-Front * half - rt * half, Front * half - rt * half),
            };
            return Anchor + Vector2.Lerp(a, b, k);
        }
        if (Formation == Formation.Wedge)
        {
            // Row r holds 2r+1 men; the anchor is the point.
            int r = (int)MathF.Sqrt(i), c = i - r * r - r;
            return Anchor - Front * r * Spacing * 0.9f + rt * c * Spacing;
        }
        int cols = Math.Clamp(Columns, 1, Math.Max(1, count));
        int row = i / cols, col = i % cols;
        return Anchor + rt * (col - (cols - 1) / 2f) * Spacing - Front * row * Spacing;
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
