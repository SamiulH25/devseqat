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
    /// How far toward the foe your companies may be drawn up (Pillar 2 P2.1): your side deploys toward -Y, up to this line.
    public const float DeployFront = -12;
    /// How close to the field's edges a company may be drawn up.
    public float DeployEdge => Terrain.Half - 20;

    /// Where your companies may stand in deployment: the one rule the clamps, the screen and the line on the ground share.
    public (float MinX, float MaxX, float MinY, float MaxY) DeployBounds() => (-DeployEdge, DeployEdge, -DeployEdge, DeployFront);

    /// `p` brought onto your side of the deployment line and inside the field.
    public Vector2 ClampDeployPos(Vector2 p)
    {
        var (x0, x1, y0, y1) = DeployBounds();
        return new Vector2(Math.Clamp(p.X, x0, x1), Math.Clamp(p.Y, y0, y1));
    }

    /// Whether `p` lies where your companies may be drawn up.
    public bool InDeployZone(Vector2 p) => ClampDeployPos(p) == p;

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
        int n = 0;
        foreach (var s in Squads)
        {
            if (s.Team != 0 || s.AiControlled || s.Company is not { Id: > 0 } c || !by.TryGetValue(c.Id, out var p)) continue;
            s.Anchor = ClampDeployPos(new Vector2(p.X, p.Y));
            var f = new Vector2(p.FX, p.FY);
            if (f.LengthSquared() > 0.01f) s.Front = Vector2.Normalize(f);
            s.Columns = Math.Max(1, p.Columns);
            if (s.Units.Count > 0 && s.Choices(s.Units[0].Type.HasShield).Contains(p.Formation)) s.Formation = p.Formation;
            s.Order = Order.Hold;
            s.Target = null;
            if (s.Division is { Post: not Post.Front }) s.Override = true;
            SnapSquad(s);
            n++;
        }
        if (n > 0) UpdateCentroids();
        return n;
    }

    /// Snap a company's standing men directly onto their formation slots (used at spawn and during deployment so companies
    /// stand in the exact line, shape and facing they were given instead of marching into place on tick 1).
    public void SnapSquad(Squad s)
    {
        s.Loosen = 1 + (1 - Terrain.GroundDefAt(s.Anchor).Order) * 0.5f;
        var men = new List<Unit>(s.Units.Count);
        foreach (var u in s.Units) if (u.Active && !u.Routing) men.Add(u);
        int n = men.Count;
        if (n == 0) { Bounds(s); s.FrontPos = s.Anchor; return; }
        men.Sort((a, b) =>
        {
            int c = (a.Place < 0 ? int.MaxValue : a.Place).CompareTo(b.Place < 0 ? int.MaxValue : b.Place);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });
        int cols = Math.Clamp(s.Columns, 1, Math.Max(1, n));
        s.LayoutCols = cols;
        s.LayoutForm = s.Formation;
        s.LayoutN = n;
        Vector2 front = Vector2.Zero;
        int nf = 0;
        for (int i = 0; i < n; i++)
        {
            var u = men[i];
            u.Place = i;
            u.Rank = s.Formation switch { Formation.Square => 0, Formation.Wedge => (int)MathF.Sqrt(i), _ => i / cols };
            var slot = s.Slot(i, n);
            u.Pos = slot;
            u.Face(s.FaceAt(slot));
            u.Heading = u.ViewFacing = u.ViewHeading = u.Facing;
            u.Vel = u.Wish = Vector2.Zero;
            u.Speed = 0;
            if (u.Rank == 0) { front += u.Pos; nf++; }
        }
        Bounds(s);
        s.FrontPos = nf > 0 ? front / nf : s.Anchor;
        s.LastSeen = s.Anchor;
    }

    /// Lay out a team's non-Front divisions at their posts' starting spots (wings beside the line, skirmishers ahead,
    /// reserve behind) and snap their men onto those slots so they do not reform across the field on tick 1.
    void PlaceDivisions(int team)
    {
        if (Options.Siege || Options.Duel) return;
        var line = FrontLine(team);
        var placed = new HashSet<Squad>(SquadsOf(team).Where(s => s.Alive > 0));
        bool Overlaps(List<Squad> live)
        {
            foreach (var s in live)
                foreach (var o in placed)
                {
                    if (live.Contains(o)) continue;
                    if (Vector2.DistanceSquared(s.Center, o.Center) > (s.Radius + o.Radius + 2) * (s.Radius + o.Radius + 2)) continue;
                    foreach (var u in s.Units)
                        if (u.Active)
                            foreach (var v in o.Units)
                                if (v.Active && Vector2.DistanceSquared(u.Pos, v.Pos) < 1f) return true;
                }
            return false;
        }
        foreach (var d in DivisionsOf(team))
        {
            if (d.Post == Post.Front || d.Squads.Count == 0) continue;
            var live = d.Squads.Where(s => s.Alive > 0).ToList();
            if (live.Count == 0) continue;
            Vector2? at = d.Post switch
            {
                Post.LeftWing or Post.RightWing => WingSpot(line, d.Goal.Side == 0 ? 1 : d.Goal.Side, live),
                Post.Skirmish => line.Mid + line.Fwd * (d.Stance == Stance.Screen ? ScreenAhead : SkirmishAhead),
                Post.Reserve => d.Stance == Stance.Follow ? (team == 0 ? Player : EnemyCaptain).Pos + line.Fwd * 7 : line.Mid - line.Fwd * ReserveBehind,
                _ => null,
            };
            if (at == null) continue;
            var nudge = d.Post switch
            {
                Post.LeftWing => -line.Right * 8,
                Post.RightWing => line.Right * 8,
                Post.Skirmish => line.Fwd * 6,
                _ => -line.Fwd * 8,
            };
            var pos = at.Value;
            for (int tries = 0; tries < 12; tries++)
            {
                Form(d, live, pos, line.Fwd);
                foreach (var s in live) SnapSquad(s);
                if (!Overlaps(live)) break;
                pos += nudge;
            }
        }
    }
}
