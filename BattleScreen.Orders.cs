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

    void Issue(string what, Action<Squad> act, bool move = false)
    {
        var list = Targets();
        if (list.Count == 0) return;
        float late = 0;
        foreach (var s in list) late = MathF.Max(late, move ? Move(s, act) : Send(s, act));
        if (move && queueing) what = "then " + what;
        host.Audio?.Play(Cue.Order, 0.6f); // the horn calls the order
        battle.Log((selected.Count == 0 ? "All troops" : string.Join(", ", list.Select(s => s.Name))) + ": " + what + (late > 0 ? $" (a rider takes {late:0} s)" : ""));
    }

    /// Give one company an order: at once for your retinue (and anyone before the battle starts); a company in a
    /// division gets it when the rider arrives, and then follows it instead of its sergeant. Returns the delay.
    float Send(Squad s, Action<Squad> act)
    {
        if (deploying || s.Division == null) { act(s); return 0; }
        return battle.Direct(s, act);
    }

    /// Shift held as a movement order is given: it is queued after the company's other orders (docs/archive/COMBAT_QOL.md Q3).
    bool queueing;

    /// A movement order: queued behind the company's orders while Shift is held, else it replaces them.
    float Move(Squad s, Action<Squad> act)
    {
        if (!queueing || deploying) return Send(s, q => { q.Queue.Clear(); q.StepTarget = null; act(q); });
        // Work the order out on a stand-in, then queue the result as a step.
        var last = s.Queue.Count > 0 ? s.Queue[^1] : battle.StepOf(s);
        var ghost = new Squad(s.Name, s.Team) { Cavalry = s.Cavalry, Formation = s.Formation, Columns = last.Columns, Front = last.Front, Anchor = last.Anchor, Order = last.Order };
        act(ghost);
        var step = new OrderStep(ghost.Order, ghost.Anchor, ghost.Front, ghost.Columns, ghost.Target != null ? battle.Squads.IndexOf(ghost.Target) : -1);
        return Send(s, q => q.Queue.Add(step));
    }

    /// Cycle the standing rule of the selected companies (docs/archive/COMBAT_QOL.md Q3); each takes the next rule it can keep.
    void NextRule()
    {
        var list = Targets();
        if (list.Count == 0) return;
        var first = Battle.RulesFor(list[0]);
        var next = first[(Array.IndexOf(first, list[0].Rule) + 1) % first.Length];
        foreach (var s in list)
        {
            var r = Battle.RulesFor(s).Contains(next) ? next : StandingRule.None;
            Send(s, q => { q.Rule = r; q.RuleFired = false; });
        }
        battle.Log((selected.Count == 0 ? "All troops" : string.Join(", ", list.Select(s => s.Name))) + ": " + Battle.RuleText(next));
        Tips.Show("rules", "A standing order holds until you change it. Charge when they close: hold, then charge whoever comes within 25 m.\nFall back when worn: under half strength or shaken, walk out of the fight. Skirmish: bows keep their distance.");
    }

    /// Companies you can pick: your retinue and the companies of your divisions (their orders arrive late).
    IEnumerable<Squad> Selectable => battle.SquadsOf(0).Where(s => !s.AiControlled);

    /// Give a division a goal (it reaches the sergeant after a delay; before the battle starts, at once).
    void GiveGoal(Division d, Goal g) { battle.IssueGoal(d, g, now: deploying); host.Audio?.Play(Cue.Order, 0.6f); }

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
            case Cmd.Charge: Issue("charge!", s => { s.Order = Order.Charge; s.Target = null; }, move: true); break;
            case Cmd.Follow: Issue("follow me!", s => { s.Order = Order.Follow; s.Target = null; }, move: true); break;
            case Cmd.Hold: Issue("hold position!", s => s.HoldInPlace(), move: true); break;
            case Cmd.Advance: Issue("advance!", s => s.Shift(enemy, 15), move: true); break;
            case Cmd.FallBack: Issue("fall back!", s => s.Shift(enemy, -20), move: true); break;
            case Cmd.Rule: NextRule(); break;
            case Cmd.Spacing: Issue("change spacing", s => s.Loose = !s.Loose); break;
            case Cmd.Merge: MergeSelected(); break;
            case Cmd.Relieve: RelieveSelected(); break;
            case Cmd.Split: SplitSelected(); break;
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
                var casters = Targets().Where(Battle.CasterCompany).ToList();
                if (casters.Count > 0)
                {
                    // Casters hold their spells for your word, or cast at will again (docs/archive/MAGIC_AURA.md 3.1).
                    bool hold = !casters[0].HoldSpells;
                    foreach (var s in casters) Send(s, q => q.HoldSpells = hold);
                    battle.Log(hold ? "Casters: hold your spells for my word!" : "Casters: cast at will!");
                }
                if (archers.Count == 0) { if (casters.Count == 0) battle.Log("No archers selected."); break; }
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

    /// Fold the selected companies into one (docs/archive/COMBAT_QOL.md Q1): the fullest takes in the rest (a company a
    /// companion leads always takes them in). A company in a division folds when the rider reaches it.
    void MergeSelected()
    {
        var list = selected.Where(s => s.Alive > 0 && s.FoldedInto == null).ToList();
        if (list.Count < 2) { battle.Log("Select two or more companies to fold together: the weaker fall in with the fullest."); return; }
        var into = list.OrderByDescending(s => s.Units.Any(u => u.Hero && u.Active)).ThenByDescending(s => s.Alive).First();
        int sent = 0;
        foreach (var s in list.Where(s => s != into))
        {
            if (battle.CannotMerge(s, into) is string why) { battle.Log($"{s.Name} cannot fold in: {why.ToLowerInvariant()}."); continue; }
            float late = Send(s, q => battle.Merge(q, into));
            if (late > 0) battle.Log($"{s.Name}: fall in with {into.Name}! (a rider takes {late:0} s)");
            sent++;
        }
        selected.RemoveWhere(s => s.FoldedInto != null);
        if (sent == 0) return;
        host.Audio?.Play(Cue.Order, 0.6f);
        Tips.Show("merge", "Folded companies fight as one: the newcomers fill the ranks from behind.\nThe fallen keep their own colours, so each company's men still go home to it after the battle.");
    }

    /// Relieve the line (docs/archive/COMBAT_QOL.md Q2): of two selected companies, the one fighting (else the more tired) is
    /// relieved by the other, which takes its line; the tired one falls back to the fresh one's place.
    void RelieveSelected()
    {
        var list = selected.Where(s => s.Alive > 0 && s.FoldedInto == null).ToList();
        if (list.Count != 2) { battle.Log("Select two companies: a tired one in the line and a fresh one to take its place."); return; }
        static float Weary(Squad s) => (s.InContact ? 1000 : 0) - s.Units.Where(u => u.Active).Select(u => u.Stamina).DefaultIfEmpty(100).Average();
        var tired = list.MaxBy(Weary)!;
        var fresh = list.First(s => s != tired);
        if (battle.CannotRelieve(tired, fresh) is string why) { battle.Log($"{fresh.Name} cannot relieve {tired.Name}: {why.ToLowerInvariant()}."); return; }
        float late = Send(fresh, q => battle.Relieve(tired, q));
        if (late > 0) battle.Log($"{fresh.Name}: relieve {tired.Name}! (a rider takes {late:0} s)");
        host.Audio?.Play(Cue.Order, 0.6f);
        Tips.Show("relieve", "The fresh company marches onto the tired one's line. When it is close behind, the tired men\nbreak off and walk back to where the fresh ones stood. Tired men fight badly: relieve them before they break.");
    }

    /// Split each selected company in two, side by side (docs/archive/COMBAT_QOL.md Q2). FOLD IN joins them again.
    void SplitSelected()
    {
        var list = Targets();
        if (selected.Count == 0) { battle.Log("Select the company to split."); return; }
        foreach (var s in list)
        {
            if (battle.CannotSplit(s) is string why) { battle.Log($"{s.Name} cannot split: {why.ToLowerInvariant()}."); continue; }
            Send(s, q => battle.Split(q));
        }
        host.Audio?.Play(Cue.Order, 0.6f);
        Tips.Show("split", "A split company stands as two halves side by side, each with its own card.\nAfter the battle both count as the one company. FOLD IN joins the halves again.");
    }

    /// A company's key in a control group: its campaign company id, else its place in the battle (negative).
    int GroupKey(Squad s) => s.Company is { Id: > 0 } c && s.SplitFrom == null ? c.Id : -(battle.Squads.IndexOf(s) + 1);

    /// Ctrl+digit keeps the selection as a group; the digit picks it again, twice quickly looks at it (docs/archive/COMBAT_QOL.md Q6).
    void Group(int digit, bool save)
    {
        if (save)
        {
            if (selected.Count == 0) { groups.Remove(digit); battle.Log($"Group {digit} cleared."); return; }
            groups[digit] = selected.Select(GroupKey).ToList();
            battle.Log($"Group {digit}: " + string.Join(", ", selected.Select(s => s.Name)));
            Tips.Show("groups", "Ctrl and a number keeps the companies you have selected as a group; the number picks them again.\nPress it twice to look at them. In the campaign your groups carry on to the next battle.");
            return;
        }
        var keys = groups[digit];
        var members = Selectable.Where(s => s.Alive > 0 && keys.Contains(GroupKey(s))).ToList();
        if (members.Count == 0) { battle.Log($"Group {digit} has no one left."); return; }
        if (lastGroup == digit && realTime - lastGroupAt < DoubleClick) camFocus = members.Aggregate(Vector2.Zero, (a, s) => a + s.Centroid()) / members.Count;
        lastGroup = digit;
        lastGroupAt = realTime;
        selected.Clear();
        selectedDivision = null;
        foreach (var s in members) selected.Add(s);
    }

    void OrderKeys()
    {
        selected.RemoveWhere(s => s.FoldedInto != null);
        queueing = ctl.Down(Act.AddToSelection);
        var mine = battle.Commanded.ToList();
        bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        for (int i = 0; i < 10; i++)
        {
            if (!Pressed(i == 9 ? Keys.D0 : Keys.D1 + i)) continue;
            int digit = (i + 1) % 10;
            if (digit > 0 && (ctrl || groups.ContainsKey(digit))) { Group(digit, ctrl); continue; } // control groups (Q6)
            if (i < mine.Count && !selected.Remove(mine[i])) selected.Add(mine[i]);
        }
        if (ctl.Pressed(Act.SelectAll)) { selected.Clear(); selectedDivision = null; }
        if (ctl.Pressed(Act.NextDivision)) NextDivision();
        if (ctl.Pressed(Act.GroundView)) groundView = !(groundView ?? deploying);
        if (ctl.Pressed(Act.Stakes)) PlantStakes();
        if (ctl.Pressed(Act.Cast)) NextSpell();
        foreach (var (cmd, (_, act)) in Hud.Orders)
            if (ctl.Pressed(act)) Command(cmd);
    }

    /// The caster you direct and the spell picked (docs/archive/MAGIC_AURA.md 3.1). The next left click on the field casts it there.
    Squad? castBy;
    int castPick = -1;

    /// Pick the next spell of a selected caster (a mage circle, or a company whose hero has abilities); past the last, none.
    void NextSpell()
    {
        var who = Targets().Where(s => s.Spells.Length > 0 && s.Team == 0).OrderByDescending(Battle.CasterCompany).FirstOrDefault();
        if (who == null) { battle.Log("Select a circle of mages, or a company led by a companion, to cast."); castBy = null; castPick = -1; return; }
        if (!command) { battle.Log($"Open the command view ({ctl.Name(Act.CommandView)}) to direct your casters."); return; }
        if (castBy != who) { castBy = who; castPick = -1; }
        castPick = castPick + 1 >= who.Spells.Length ? -1 : castPick + 1;
        if (castPick < 0) castBy = null;
        else Tips.Show("cast", "Click the ground to cast the spell there; attacks still glow before they land.\nAn ordered spell costs more Will. X makes your casters hold their spells for your word.");
    }

    /// Cast the picked spell on a spot: the order reaches a company in a division late, like any order.
    void CastAt(Vector2 spot)
    {
        if (castBy is not { } s || castPick < 0) return;
        int i = castPick;
        var a = s.Spells[i];
        float late = Send(s, q => { q.CastSpell = i; q.CastAt = spot; q.CastSince = battle.Time; });
        if (battle.CannotCast(s, i) is string why) battle.Log($"{s.Name} will cast {a.Name} when they can ({why.ToLowerInvariant()}).");
        else battle.Log($"{s.Name}: cast {a.Name} there!" + (late > 0 ? $" (a rider takes {late:0} s)" : ""));
        arrows.Add((s.Centroid(), spot, Ui.Now));
        castBy = null;
        castPick = -1;
    }

    /// Your selected longbowmen (or all of them) plant their stakes in front of their line (docs/archive/TERRAIN.md T6).
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
        foreach (var (s, a1, b1) in Split(a, b).ToList()) Move(s, q => q.SetLine(a1, b1, toward));
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
