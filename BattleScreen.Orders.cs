using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// BattleScreen: squad orders (command buttons and keys, drawn lines, slot previews).
partial class BattleScreen
{
    // ---------- orders ----------

    List<Squad> Targets() =>
        (selected.Count > 0 ? selected : battle.Commanded).Where(s => s.Alive > 0).ToList();

    void Issue(string what, Action<Squad> act)
    {
        var list = Targets();
        if (list.Count == 0) return;
        float late = 0;
        foreach (var s in list) late = MathF.Max(late, Send(s, act));
        battle.Log((selected.Count == 0 ? "All troops" : string.Join(", ", list.Select(s => s.Name))) + ": " + what + (late > 0 ? $" (a rider takes {late:0} s)" : ""));
    }

    /// Give one company an order: at once for your retinue (and anyone before the battle starts); a company in a
    /// division gets it when the rider arrives, and then follows it instead of its sergeant. Returns the delay.
    float Send(Squad s, Action<Squad> act)
    {
        if (deploying || s.Division == null) { act(s); return 0; }
        return battle.Direct(s, act);
    }

    /// Companies you can pick: your retinue and the companies of your divisions (their orders arrive late).
    IEnumerable<Squad> Selectable => battle.SquadsOf(0).Where(s => !s.AiControlled);

    /// Give a division a goal (it reaches the sergeant after a delay; before the battle starts, at once).
    void GiveGoal(Division d, Goal g) => battle.IssueGoal(d, g, now: deploying);

    void GoalCommand(Division d, GoalKind kind, int side)
    {
        var mid = d.Squads.Where(s => s.Alive > 0).Select(s => s.Center).DefaultIfEmpty(Vector2.Zero).Aggregate((a, b) => a + b) / Math.Max(1, d.Squads.Count(s => s.Alive > 0));
        var foe = battle.SquadsOf(1).Where(s => s.Alive > 0).OrderBy(s => Vector2.DistanceSquared(s.Center, mid)).FirstOrDefault();
        var enemy = battle.Centroid(1);
        var away = mid - enemy;
        if (away.LengthSquared() < 1) away = -Vector2.UnitY;
        away.Normalize();
        Goal g = kind switch
        {
            GoalKind.Attack or GoalKind.Harass => new Goal(kind, Target: foe),
            GoalKind.Flank => new Goal(kind, Target: foe, Side: side),
            GoalKind.Screen => new Goal(kind, Target: battle.SquadsOf(0).Where(s => s.Alive > 0 && s.Division != d).OrderBy(s => Vector2.DistanceSquared(s.Center, mid)).FirstOrDefault()),
            GoalKind.Take => new Goal(kind, mid - away * 25),
            GoalKind.FallBack => new Goal(kind, mid + away * 30),
            GoalKind.Hold => new Goal(kind, mid),
            GoalKind.Ambush => new Goal(kind, battle.BestPlace(mid, 60, p => p.Hide)?.Pos ?? mid), // the nearest wood to hide in
            _ => new Goal(kind, mid),
        };
        GiveGoal(d, g);
    }

    void Command(Cmd c)
    {
        var enemy = battle.Centroid(1);
        switch (c)
        {
            case Cmd.Charge: Issue("charge!", s => { s.Order = Order.Charge; s.Target = null; }); break;
            case Cmd.Follow: Issue("follow me!", s => { s.Order = Order.Follow; s.Target = null; }); break;
            case Cmd.Hold: Issue("hold position!", s => s.HoldInPlace()); break;
            case Cmd.Advance: Issue("advance!", s => s.Shift(enemy, 15)); break;
            case Cmd.FallBack: Issue("fall back!", s => s.Shift(enemy, -20)); break;
            case Cmd.Spacing: Issue("change spacing", s => s.Loose = !s.Loose); break;
            case Cmd.Formation:
            {
                var list = Targets();
                if (list.Count == 0) break;
                // Cycle from the first company's formation; each company takes the next one it can (horse has no shield wall).
                var first = list[0].Choices(list[0].Units[0].Type.HasShield);
                var next = first[(Array.IndexOf(first, list[0].Formation) + 1) % first.Length];
                foreach (var s in list)
                {
                    var ok = s.Choices(s.Units[0].Type.HasShield);
                    var f = ok.Contains(next) ? next : Formation.Line;
                    Send(s, q => q.Formation = f);
                }
                battle.Log((selected.Count == 0 ? "All troops" : string.Join(", ", list.Select(s => s.Name))) + ": " + next switch
                {
                    Formation.ShieldWall => "shield wall!", Formation.Square => "form square!", Formation.Wedge => "wedge!", Formation.Loose => "open ranks!", _ => "form line!",
                });
                break;
            }
            case Cmd.FireAtWill:
            {
                var archers = Targets().Where(s => s.Units.Any(u => u.Type.Ammo > 0)).ToList();
                if (archers.Count == 0) { battle.Log("No archers selected."); break; }
                bool fire = !archers[0].FireAtWill;
                foreach (var s in archers) Send(s, q => q.FireAtWill = fire);
                battle.Log(fire ? "Archers: fire at will!" : "Archers: hold your arrows!");
                break;
            }
            case Cmd.FormHere:
            {
                var p = battle.Player;
                if (!p.Active) break;
                var center = p.Pos + p.Dir * 3;
                LineAt(center, center + p.Dir * 10);
                battle.Log("Form up on me!");
                break;
            }
        }
    }

    void OrderKeys()
    {
        var mine = battle.Commanded.ToList();
        for (int i = 0; i < mine.Count && i < 10; i++)
            if (Pressed(i == 9 ? Keys.D0 : Keys.D1 + i) && !selected.Remove(mine[i])) selected.Add(mine[i]);
        if (ctl.Pressed(Act.SelectAll)) { selected.Clear(); selectedDivision = null; }
        if (ctl.Pressed(Act.NextDivision)) NextDivision();
        if (ctl.Pressed(Act.GroundView)) groundView = !(groundView ?? deploying);
        if (ctl.Pressed(Act.Stakes)) PlantStakes();
        foreach (var (cmd, (_, act)) in Hud.Orders)
            if (ctl.Pressed(act)) Command(cmd);
    }

    /// Your selected longbowmen (or all of them) plant their stakes in front of their line (docs/TERRAIN.md T6).
    void PlantStakes()
    {
        var bows = Targets().Where(s => s.Units[0].Type.Has("stakes")).ToList();
        if (bows.Count == 0) { battle.Log("Only longbowmen carry stakes."); return; }
        string? why = null;
        int planted = 0;
        foreach (var s in bows) { var no = battle.PlantStakes(s); if (no == null) planted++; else why ??= no; }
        if (planted == 0 && why != null) battle.Log($"No stakes: {why.ToLowerInvariant()}.");
        else Tips.Show("stakes", "Stakes stop horse: riders cannot cross them, but your men on foot slip between.\nEach company of longbowmen plants them once, while it holds and no enemy is within 30 m.");
    }

    /// Cycle the division you give goals to: none, then each living one in turn, then none again.
    void NextDivision()
    {
        var list = battle.DivisionsOf(0).Where(d => d.Alive > 0).ToList();
        if (list.Count == 0) { battle.Log("You have no divisions. Make them on the campaign map (V)."); return; }
        int i = selectedDivision == null ? -1 : list.IndexOf(selectedDivision);
        selectedDivision = i + 1 < list.Count ? list[i + 1] : null;
        if (selectedDivision != null) { selected.Clear(); battle.Log($"Goals for {selectedDivision.Name}: {selectedDivision.Goal.Describe}"); }
    }

    /// Line of the selected squads' combined width, centred on `center`, facing `toward`.
    void LineAt(Vector2 center, Vector2 toward)
    {
        var (a, b) = LineEnds(center, toward);
        Distribute(a, b, toward);
    }

    (Vector2, Vector2) LineEnds(Vector2 center, Vector2 toward)
    {
        float w = Targets().Sum(s => Math.Min(s.Columns, s.Alive) * s.Spacing);
        var to = toward - center;
        if (to.LengthSquared() < 1e-3f) to = Vector2.UnitY;
        to.Normalize();
        var side = new Vector2(-to.Y, to.X);
        return (center - side * w / 2, center + side * w / 2);
    }

    /// Split the segment a-b between target squads, proportional to their size.
    void Distribute(Vector2 a, Vector2 b, Vector2 toward)
    {
        if (deploying) { a.Y = MathF.Min(a.Y, -12); b.Y = MathF.Min(b.Y, -12); } // your third of the field
        foreach (var (s, a1, b1) in Split(a, b).ToList()) Send(s, q => q.SetLine(a1, b1, toward));
    }

    IEnumerable<(Squad S, Vector2 A, Vector2 B)> Split(Vector2 a, Vector2 b)
    {
        var list = Targets();
        float total = list.Sum(s => s.Alive), acc = 0;
        foreach (var s in list)
        {
            var a1 = Vector2.Lerp(a, b, acc / total);
            acc += s.Alive;
            yield return (s, a1, Vector2.Lerp(a, b, acc / total));
        }
    }

    /// Where each man would stand if the line a-b were ordered now (drawn while dragging).
    IEnumerable<Vector2> PreviewSlots(Vector2 a, Vector2 b, Vector2 toward)
    {
        foreach (var (s, a1, b1) in Split(a, b))
        {
            var ghost = new Squad(s.Name, s.Team) { Cavalry = s.Cavalry, Loose = s.Loose, Columns = s.Columns, Front = s.Front, Anchor = s.Anchor };
            ghost.SetLine(a1, b1, toward);
            for (int i = 0; i < s.Alive; i++) yield return ghost.Slot(i, s.Alive);
        }
    }
}
