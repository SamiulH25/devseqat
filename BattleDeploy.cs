using Microsoft.Xna.Framework;

namespace Ironfield;

/// Where one campaign company stood when your last battle began (docs/archive/COMBAT_QOL.md Q6): its line's centre, facing,
/// width and formation. Plain fields, so it saves with the campaign.
public sealed class DeploySpot
{
    public int Company;
    public float X, Y, FX, FY;
    public int Columns;
    public Formation Formation;
}

/// Remembered deployments (docs/archive/COMBAT_QOL.md Q6): read your companies' lines as the battle begins, and lay them out
/// again at the next deployment. Your side always deploys toward -Y, so places carry from field to field.
public sealed partial class Battle
{
    /// Your companies' lines now, by campaign company (allies', split halves' and one-off squads' left out).
    public List<DeploySpot> DeploymentOf() => Squads
        .Where(s => s.Team == 0 && !s.AiControlled && s.SplitFrom == null && s.FoldedInto == null && s.Company is { Id: > 0 })
        .Select(s => new DeploySpot { Company = s.Company!.Id, X = s.Anchor.X, Y = s.Anchor.Y, FX = s.Front.X, FY = s.Front.Y, Columns = s.Columns, Formation = s.Formation })
        .ToList();

    /// Lay out your companies as `spots` remember them, kept to your third of this field; companies not remembered keep
    /// their places. Returns how many were placed. Not for assaults, the lists or single combat.
    public int ApplyDeployment(IEnumerable<DeploySpot> spots)
    {
        if (Options.Siege || Options.Tournament || Options.Duel) return 0;
        var by = spots.GroupBy(p => p.Company).ToDictionary(g => g.Key, g => g.First());
        float edge = Terrain.Half - 20;
        int n = 0;
        foreach (var s in Squads)
        {
            if (s.Team != 0 || s.AiControlled || s.Company is not { Id: > 0 } c || !by.TryGetValue(c.Id, out var p)) continue;
            s.Anchor = new Vector2(Math.Clamp(p.X, -edge, edge), Math.Clamp(p.Y, -edge, -12));
            var f = new Vector2(p.FX, p.FY);
            if (f.LengthSquared() > 0.01f) s.Front = Vector2.Normalize(f);
            s.Columns = Math.Max(1, p.Columns);
            if (s.Units.Count > 0 && s.Choices(s.Units[0].Type.HasShield).Contains(p.Formation)) s.Formation = p.Formation;
            s.Order = Order.Hold;
            s.Target = null;
            n++;
        }
        return n;
    }
}
