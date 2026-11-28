using Microsoft.Xna.Framework;

namespace Ironfield;

/// Who listens and whom they fight (the fight view's number keys, ENGAGE with a target). Foot answers by where it stands
/// in the line, not as one arm: left, centre, right, each third of the foot ranked across the field facing the enemy. And
/// an engage on one company spreads: enough companies to beat it take it, the rest take the foe in front of them, so a
/// line meets a line instead of piling on one victim. Read only: the screen turns the answers into orders.
public sealed partial class Battle
{
    /// Foot wings: 0 left, 1 centre, 2 right.
    public const int WingLeft = 0, WingCentre = 1, WingRight = 2;
    /// Men sent onto one foe, as a share of its men, before the next company looks for a foe of its own.
    public const float EngageOdds = 1.3f;

    /// The wing each of these foot companies stands on, seen facing the enemy: ranked left to right, the first third
    /// left, the middle centre, the last third right (one company is centre, two are left and right).
    public Dictionary<Squad, int> FootWings(IEnumerable<Squad> foot, int team = 0)
    {
        var list = foot.Where(s => s.Alive > 0).ToList();
        var wings = new Dictionary<Squad, int>();
        if (list.Count == 0) return wings;
        var mine = Mean(list);
        var toFoe = Centroid(1 - team) - mine;
        if (toFoe.LengthSquared() < 1 || SquadsOf(1 - team).All(s => s.Alive == 0)) toFoe = Mean(list.Select(s => s.Front), list.Count);
        if (toFoe.LengthSquared() < 1e-6f) toFoe = team == 0 ? Vector2.UnitY : -Vector2.UnitY;
        var right = new Vector2(-toFoe.Y, toFoe.X); // the screen's right when looking at the foe (BattleScreen.RightOf)
        var ranked = list.Select((s, i) => (s, x: Vector2.Dot(s.Centroid() - mine, right), i)).OrderBy(t => t.x).ThenBy(t => t.i).ToList();
        for (int i = 0; i < ranked.Count; i++)
        {
            float at = (i + 0.5f) / ranked.Count;
            wings[ranked[i].s] = at < 1 / 3f ? WingLeft : at < 2 / 3f ? WingCentre : WingRight;
        }
        return wings;
    }

    /// An engage on `foe` by these companies, spread along the enemy line: the nearest companies take `foe` until their
    /// men outnumber its men by EngageOdds (at least one goes); each of the rest, nearest first, takes the enemy company
    /// closest to it (near ties go toward `foe`) that is not yet outnumbered, else the closest one at all.
    public Dictionary<Squad, Squad> SpreadAttack(IReadOnlyList<Squad> mine, Squad foe)
    {
        var to = new Dictionary<Squad, Squad>();
        var load = new Dictionary<Squad, int>();
        var foes = SquadsOf(foe.Team).Where(e => e.Alive > 0 && e.Works == null).ToList();
        if (!foes.Contains(foe)) foes.Add(foe);
        var at = foe.Centroid();
        var order = mine.Where(s => s.Alive > 0).OrderBy(s => Vector2.DistanceSquared(s.Centroid(), at)).ToList();
        bool Full(Squad e) => load.GetValueOrDefault(e) >= EngageOdds * Math.Max(1, e.Alive);
        void Take(Squad s, Squad e) { to[s] = e; load[e] = load.GetValueOrDefault(e) + s.Alive; }

        foreach (var s in order)
        {
            if (to.Count == 0 || !Full(foe)) { Take(s, foe); continue; }
            var here = s.Centroid();
            float Cost(Squad e) => Vector2.Distance(here, e.Centroid()) + 0.1f * Vector2.Distance(e.Centroid(), at);
            var pick = foes.Where(e => !Full(e)).OrderBy(Cost).FirstOrDefault() ?? foes.OrderBy(Cost).First();
            Take(s, pick);
        }
        foreach (var s in mine) to.TryAdd(s, foe);
        return to;
    }

    static Vector2 Mean(List<Squad> list) => Mean(list.Select(s => s.Centroid()), list.Count);
    static Vector2 Mean(IEnumerable<Vector2> v, int n) { var sum = Vector2.Zero; foreach (var p in v) sum += p; return sum / Math.Max(1, n); }
}
