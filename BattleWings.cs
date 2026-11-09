using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: horse and bows by grade (docs/MELEE.md D6). Rabble horse bog down; veteran horse rally as a body behind their
/// own side before charging again, and take a fighting company in the flank; elite horse ride a lane round to a foe's
/// open side and go for archers left open. Veteran bows fall back behind their own foot when foot come at them and hold
/// fire into a melee their own men are in; elite bows shoot wavering companies and the enemy's archers first.
public partial class Battle
{
    /// How long a bogged rider hacks before breaking off: rabble stay stuck longer.
    static float MeleeTime(Squad s) => s.Grade == Grade.Rabble ? 8 : 5;

    /// Veteran horse and better (not horse archers) rally as a body between charges.
    static bool Rallies(Squad s) => s.Grade >= Grade.Veteran && s.Units.Count > 0 && s.Units[0].Type.Ammo == 0;

    /// Where a company's horse rally: 20 m behind its own side's centre, on its own wing.
    Vector2 RallyPoint(Squad s)
    {
        var mine = centroid[s.Team];
        var toE = centroid[1 - s.Team] - mine;
        toE = toE.LengthSquared() > 1 ? Vector2.Normalize(toE) : s.Front;
        var perp = new Vector2(-toE.Y, toE.X);
        var at = mine - toE * 20 + perp * Vector2.Dot(s.Center - mine, perp);
        if (at.Length() > MapRadius - 30) at = Vector2.Normalize(at) * (MapRadius - 30);
        return at;
    }

    /// A withdrawing rider of a rallying company: make for the rally point and wait there, facing the enemy.
    void RideToRally(Unit u, Squad s)
    {
        if (!s.Rallying) { s.Rallying = true; s.RallySince = Time; s.RallyAt = RallyPoint(s); }
        u.Couched = false;
        float far = Vector2.Distance(u.Pos, s.RallyAt);
        if (far > 6) { Ride(u, Via(u, s.RallyAt), far > 15 ? u.Horse!.Gallop : u.Horse!.Trot); return; }
        u.Wish = Vector2.Zero;
        var toE = centroid[1 - s.Team] - u.Pos;
        if (toE.LengthSquared() > 1) u.Face(toE);
    }

    /// The horse company's own wits: once most of a rallying company is in (or after 15 s) it charges again together, a
    /// veteran's at a company already fighting, an elite's at archers left open.
    void HorseWits(Squad s)
    {
        if (!s.Rallying || s.Alive == 0) return;
        int near = 0, all = 0;
        foreach (var u in s.Units)
        {
            if (!u.Active || u.Routing) continue;
            all++;
            if (u.Cav == CavState.Withdraw && Vector2.DistanceSquared(u.Pos, s.RallyAt) < 15 * 15) near++;
        }
        if (all == 0 || near < all * 0.7f && Time - s.RallySince < 15) return;
        s.Rallying = false;
        s.Rallied++;
        foreach (var u in s.Units) if (u.Active && u.Cav == CavState.Withdraw) { u.Cav = CavState.Charge; u.Contact = false; }
        if (s.Target == null || !s.Target.InContact) s.Target = HorsePrey(s) ?? s.Target;
        SetPlan(s, Plan.Advance);
    }

    /// What rallied horse go for next: an elite's first choice is enemy archers with no foot company near them; then a foot
    /// company already fighting (its flank is open), nearest first.
    Squad? HorsePrey(Squad s)
    {
        Squad? best = null;
        float bd = 80;
        foreach (var e in Squads)
        {
            if (e.Team == s.Team || e.Alive == 0 || e.Cavalry) continue;
            bool open = s.Grade == Grade.Elite && e.Units[0].Type.Ammo > 0 && !Squads.Any(o => o.Team == e.Team && o != e && o.Alive > 0 && !o.Cavalry && o.Units[0].Type.Ammo == 0 && Vector2.Distance(o.Center, e.Center) < o.Radius + e.Radius + 12);
            if (!open && !e.InContact) continue;
            float d = Vector2.Distance(e.Center, s.Center) - (open ? 30 : 0);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    /// A veteran's or elite's charge lane: riding at a foot company from its front (a veteran only when it is already
    /// fighting; an elite always, braced spears above all), go round to its side first. Null: charge straight.
    Vector2? ChargeLane(Unit u, Squad s, Unit t)
    {
        if (s.Grade < Grade.Veteran || t.Squad is not { Cavalry: false } ts || ts.Swarm || ts.Formation == Formation.Square) return null;
        if (s.Grade == Grade.Veteran && !ts.InContact) return null;
        var rel = u.Pos - ts.Center;
        float dist = rel.Length();
        if (dist < ts.Radius + 8 || Vector2.Dot(rel / dist, ts.Front) < 0.35f) return null; // already off its front
        var perp = new Vector2(-ts.Front.Y, ts.Front.X);
        float sign = Vector2.Dot(rel, perp) >= 0 ? 1 : -1;
        return ts.Center + perp * sign * (ts.Radius + 12) - ts.Front * ts.Radius * 0.5f;
    }

    /// Bows of a veteran company and better hold their shot at a man fighting their own side (too near their friends).
    static bool HoldsFire(Squad s, Unit enemy) => s.Grade >= Grade.Veteran && enemy.EnemyDist < 2.5f;

    /// Veteran bows and better, holding with enemy foot coming within 20 m: fall back behind the nearest friendly foot
    /// company, rather than meet it with knives.
    void BowsWits(Squad s)
    {
        if (s.Grade < Grade.Veteran || s.Order != Order.Hold || s.InContact) return;
        Squad? threat = null;
        float td = 20;
        foreach (var e in Squads)
        {
            if (e.Team == s.Team || e.Alive == 0 || e.Cavalry || e.Units[0].Type.Ammo > 0) continue;
            float gap = Vector2.Distance(e.Center, s.Center) - e.Radius - s.Radius;
            if (gap < td && e.Order == Order.Charge) { td = gap; threat = e; }
        }
        if (threat == null) { if (s.Plan == Plan.Shelter) SetPlan(s, Plan.Hold); return; }
        Squad? cover = null;
        float cd = 60;
        foreach (var o in Squads)
        {
            if (o.Team != s.Team || o == s || o.Alive < 4 || o.Cavalry || o.Units[0].Type.Ammo > 0) continue;
            float d = Vector2.Distance(o.Center, s.Center);
            if (d < cd) { cd = d; cover = o; }
        }
        if (cover == null) return;
        var away = cover.Center - threat.Center;
        away = away.LengthSquared() > 1 ? Vector2.Normalize(away) : -s.Front;
        s.Anchor = cover.Center + away * (cover.Radius + s.Radius + 4);
        s.Front = -away;
        SetPlan(s, Plan.Shelter);
    }

    /// An elite archer company's pick of the companies in reach: wavering ones first, then the enemy's own archers.
    static int Focus(Squad archers, Squad e) => archers.Grade != Grade.Elite ? 0 : AvgMorale(e) < 45 ? 0 : e.Units[0].Type.Ammo > 0 ? 1 : 2;
}
