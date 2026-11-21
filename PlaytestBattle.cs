using System.Text;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// The playtest harness's battles (docs/PLAYTEST_HARNESS.md): a campaign battle fought by text. It starts in deployment as
/// the battle screen does; the agent lays out lines, starts the fight, gives orders and steps time. The captain stands in
/// command view (he guards himself). When the battle is decided the result goes back to the campaign by `FinishBattle`.
public sealed partial class PlaySession
{
    /// The battle being fought, if any, its setup, and whether it is still in deployment.
    public Battle? B;
    BattleSetup? setup;
    bool deploying;
    /// Allied lords' companies (led by the AI general from the start): not yours to order.
    readonly HashSet<Squad> allies = new();

    static readonly string[] BattleVerbs = { "bstate", "line", "start", "order", "step", "auto", "withdraw", "goal" };

    string StartBattle()
    {
        if (C.Encounter is not Party e) return "ERROR: no encounter";
        if (C.Meeting(e)) return "ERROR: a friendly meeting: `talk` or `leave`";
        return BeginBattle(C.PrepareBattle(e));
    }

    string BeginBattle(BattleSetup s)
    {
        setup = s;
        B = new Battle(s.Seed, s.Player, s.Enemy, s.Options) { Commanding = true };
        allies.Clear();
        allies.UnionWith(B.SquadsOf(0).Where(q => q.AiControlled || q.Company?.Allied == true));
        if (C.S is { DeployAsLast: true, LastDeploy.Count: > 0 } && !s.Options.Duel) B.ApplyDeployment(C.S.LastDeploy);
        deploying = !s.Options.Duel;
        return $"BATTLE {(s.Field != "" ? "of " + s.Field + " " : "")}against {s.EnemyName}: {s.Options.Ground?.Name ?? "open"} ground, {s.Options.Weather.Describe}{(s.Options.Siege ? ", ASSAULT on the walls" : "")}.\n"
            + "Your side deploys at y < -12, the enemy is toward +y. Lay lines with `line`, then `start`. `help battle` lists the orders.\n" + BattleState();
    }

    string BattleCommand(string verb, string[] a)
    {
        var b = B!;
        switch (verb)
        {
            case "bstate": return BattleState();
            case "line":
            {
                var sq = SquadArg(a, 0);
                var p1 = new Vector2(F(Need(a, 1, "x1")), F(Need(a, 2, "y1")));
                var p2 = new Vector2(F(Need(a, 3, "x2")), F(Need(a, 4, "y2")));
                if (deploying) { p1.Y = MathF.Min(p1.Y, -12); p2.Y = MathF.Min(p2.Y, -12); } // your third of the field, as the screen keeps it
                foreach (var s in sq) Send(s, q => { q.Queue.Clear(); q.StepTarget = null; q.SetLine(p1, p2, b.Centroid(1)); });
                return $"{Names(sq)}: form a line {P(p1)}-{P(p2)}";
            }
            case "start":
                if (!deploying) return "ERROR: the battle has begun";
                deploying = false;
                C.S.LastDeploy = b.DeploymentOf();
                b.Log("The battle begins!");
                return "the battle begins. `step <seconds>` to let it run.";
            case "order": return Order(a);
            case "goal":
            {
                var d = b.DivisionsOf(0).FirstOrDefault(x => x.Id == I(Need(a, 0, "division id"))) ?? throw Usage("no such division of yours (see bstate)");
                var g = Goal.FromName(Need(a, 1, "Post:Stance, e.g. Front:Advance"));
                b.IssueGoal(d, g);
                return $"{d.Name}: {g.Post} {g.Stance}";
            }
            case "step": return Step(a.Length > 0 ? F(a[0]) : 10);
            case "auto":
                foreach (var s in b.SquadsOf(0)) s.AiControlled = true; // allies already are
                deploying = false;
                return Step(a.Length > 0 ? F(a[0]) : 900);
            case "withdraw":
                if (deploying) return "ERROR: the battle has not begun; `start` first (or fight on)";
                b.Withdraw();
                return Step(0.1f);
        }
        return "ERROR: unknown battle command";
    }

    /// `order <squad|all> <hold|charge [enemy squad]|follow|advance|fallback|move x y|form shape|fire on|off|ai on|off|loose>`
    string Order(string[] a)
    {
        var b = B!;
        var list = SquadArg(a, 0);
        string what = Need(a, 1, "order").ToLowerInvariant();
        var enemy = b.Centroid(1);
        Action<Squad> act = what switch
        {
            "hold" => s => s.HoldInPlace(),
            "charge" => a.Length > 2 ? Charge(EnemySquad(a[2])) : s => { s.Order = Ironfield.Order.Charge; s.Target = null; },
            "follow" => s => { s.Order = Ironfield.Order.Follow; s.Target = null; },
            "advance" => s => s.Shift(enemy, a.Length > 2 ? F(a[2]) : 15),
            "fallback" => s => s.Shift(enemy, -(a.Length > 2 ? F(a[2]) : 20)),
            "move" => MoveOrder(new Vector2(F(Need(a, 2, "x")), F(Need(a, 3, "y")))),
            "form" => Enum.TryParse<Formation>(Need(a, 2, "Line|ShieldWall|Square|Wedge|Loose"), true, out var f)
                ? s => { if (s.Units.Count > 0 && s.Choices(s.Units[0].Type.HasShield).Contains(f)) s.Formation = f; } : throw Usage("formation Line|ShieldWall|Square|Wedge|Loose"),
            "fire" => s => s.FireAtWill = Need(a, 2, "on|off") == "on",
            "ai" => s => s.AiControlled = Need(a, 2, "on|off") == "on",
            "loose" => s => s.Loose = !s.Loose,
            _ => throw Usage("orders: hold, charge [enemy squad], follow, advance [m], fallback [m], move x y, form <shape>, fire on|off, ai on|off, loose"),
        };
        foreach (var s in list) if (what is "ai" or "fire" or "form" or "loose") act(s); else Send(s, q => { q.Queue.Clear(); q.StepTarget = null; act(q); });
        return $"{Names(list)}: {string.Join(" ", a.Skip(1))}";

        Action<Squad> Charge(Squad foe) => s => { s.Order = Ironfield.Order.Charge; s.Target = foe; };
        Action<Squad> MoveOrder(Vector2 to) => s => { var d = to - s.Anchor; s.HoldInPlace(); if (d.LengthSquared() > 1) s.Front = Vector2.Normalize(B!.Centroid(1) - to); s.Anchor = to; };
    }

    /// An order lands at once on your retinue (and on anyone in deployment); a division's company takes it on the next tick.
    void Send(Squad s, Action<Squad> act) { if (deploying || s.Division == null) act(s); else B!.Direct(s, act); }

    string Step(float seconds)
    {
        var b = B!;
        if (deploying) return "ERROR: still deploying: `start` first";
        if (seconds <= 0 || seconds > 1800) throw Usage("step between 0 and 1800 seconds");
        float t0 = b.Time;
        int events = b.Events.Count;
        for (int i = 0, n = (int)MathF.Ceiling(seconds * 60); i < n && b.Winner < 0; i++) b.Update(1 / 60f);
        // A decided battle plays on a moment so the rout is counted, as the screen's ending beat does.
        if (b.Winner >= 0) for (int i = 0; i < 120; i++) b.Update(1 / 60f);
        var sb = new StringBuilder($"{b.Time - t0:0.0}s passed (battle time {b.Time:0})\n");
        foreach (var (text, _) in b.Events.Skip(events).TakeLast(15)) sb.AppendLine("  " + text);
        if (b.Winner < 0) return sb + BattleState();
        return sb + FinishBattle();
    }

    string FinishBattle()
    {
        var b = B!;
        var r = b.Result();
        var report = C.FinishBattle(r, setup!);
        B = null; setup = null;
        C.Report = null;
        if (report.Spoil != "") C.TakeSpoil();
        return $"THE BATTLE IS {(b.Winner == 0 ? "WON" : "LOST")}.\n" + ReportText(report, Me) + Pending();
    }

    string BattleState()
    {
        var b = B!;
        var sb = new StringBuilder($"BATTLE t={b.Time:0}s {(deploying ? "DEPLOYING" : b.Winner >= 0 ? "OVER" : "FIGHTING")} | captain {(b.Player.Active ? $"hp {b.Player.Hp:0}" : "DOWN")} at {P(b.Player.Pos)} | field radius {b.MapRadius:0}\n");
        foreach (var d in b.DivisionsOf(0)) sb.AppendLine($"division {d.Id} {d.Name}: {d.Goal.Post}:{d.Goal.Stance}, squads {string.Join(",", d.Squads.Select(s => b.Squads.IndexOf(s)))}");
        foreach (var s in b.Squads.Where(s => s.Units.Count > 0 && s.FoldedInto == null))
        {
            int i = b.Squads.IndexOf(s);
            if (s.Team == 1 && !b.Visible(s, 0)) { sb.AppendLine($"  [{i}] enemy {s.Name}: not seen"); continue; }
            var alive = s.Units.Where(u => u.Active && !u.Routing).ToList();
            int routing = s.Units.Count(u => u.Active && u.Routing);
            float morale = alive.Count > 0 ? alive.Average(u => u.Morale) : 0;
            string side = s.Team == 1 ? "enemy" : allies.Contains(s) ? "ALLY" : "OURS";
            string order = s.Order + (s.Target != null ? $"->[{b.Squads.IndexOf(s.Target)}]" : "");
            string co = s.Company is { Id: > 0 } c && s.Team == 0 && !allies.Contains(s) ? $" of company #{c.Id} {c.Name}" : "";
            sb.AppendLine($"  [{i}] {side} {s.Name}{co} ({s.Units[0].Type.Id}{(s.Cavalry ? ", horse" : "")}): {alive.Count}/{s.StartCount} fit"
                + (routing > 0 ? $", {routing} ROUTING" : "") + $", morale {morale:0}, at {P(s.Centroid())}, {order}, {s.Formation}"
                + (s.Team == 0 && s.AiControlled && !allies.Contains(s) ? ", AI" : "") + (s.Division != null ? $", div {s.Division.Id}" : ""));
        }
        return sb.ToString();
    }

    /// Squads of yours by index (`3`), list (`1,2,5`), `all`, or `div:<id>`.
    List<Squad> SquadArg(string[] a, int i)
    {
        var b = B!;
        string s = Need(a, i, "squad index (see bstate), a list 1,2 or all");
        var mine = b.SquadsOf(0).Where(q => q.Alive > 0 && q.Works == null && q.FoldedInto == null && !allies.Contains(q)).ToList();
        if (s == "all") return mine;
        if (s.StartsWith("div:")) return mine.Where(q => q.Division?.Id == I(s[4..])).ToList();
        var list = s.Split(',').Select(x => I(x)).Select(n => n >= 0 && n < b.Squads.Count ? b.Squads[n] : throw Usage($"no squad [{n}]")).ToList();
        if (list.Any(q => q.Team != 0 || allies.Contains(q))) throw Usage("you can only order your own squads (not the enemy's, not an ally's)");
        return list;
    }

    Squad EnemySquad(string s)
    {
        int n = I(s);
        return n >= 0 && n < B!.Squads.Count && B.Squads[n].Team == 1 ? B.Squads[n] : throw Usage($"[{n}] is not an enemy squad");
    }

    static string Names(List<Squad> list) => string.Join(", ", list.Select(s => s.Name));
}
