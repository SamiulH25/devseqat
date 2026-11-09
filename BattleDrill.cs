namespace Ironfield;

/// Battle: drill and order (docs/MELEE.md D1-D2). Drill is a company's training, fixed for the battle.
public partial class Battle
{
    /// Drill by troop tier 1-7.
    static readonly float[] TierDrill = { 0.10f, 0.10f, 0.22f, 0.38f, 0.50f, 0.64f, 0.78f, 0.92f };

    /// A troop's own drill, from its tier. Creatures have none (the raised dead a little: they keep their places).
    public static float DrillOf(UnitType t) => t.Has("raised") ? 0.3f : t.Wild ? 0 : TierDrill[Math.Clamp(t.Tier, 0, 7)];

    /// A company's drill: its men's, averaged over a mixed company.
    public static float DrillOf(Squad s)
    {
        float sum = 0;
        int n = 0;
        foreach (var u in s.Units) if (!u.Hero) { sum += DrillOf(u.Type); n++; }
        return s.Swarm || n == 0 ? 0 : Math.Clamp(sum / n, 0, 1);
    }
}
