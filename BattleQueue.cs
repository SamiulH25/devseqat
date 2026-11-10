using Microsoft.Xna.Framework;

namespace Ironfield;

/// One queued order (docs/archive/COMBAT_QOL.md Q3): what the company does once its current order is done. Target is a company's
/// index in Battle.Squads (-1 = none), so a queue replays.
public sealed record OrderStep(Order Order, Vector2 Anchor, Vector2 Front, int Columns, int Target = -1);

/// A rule a company keeps until you change it (docs/archive/COMBAT_QOL.md Q3).
public enum StandingRule { None, ChargeWhenClose, FallBackWhenWorn, Skirmish, NoChase }

/// Queued and standing orders (docs/archive/COMBAT_QOL.md Q3).
public sealed partial class Battle
{
    /// Charge-when-close distance, skirmish distance, and how far a worn company falls back.
    public const float CloseCharge = 25, SkirmishGap = 35, WornFallBack = 30;

    /// The rules a company can keep, in the order the rule key cycles them (skirmishing is for bows).
    public static StandingRule[] RulesFor(Squad s) => s.Units.Count > 0 && s.Units[0].Type.Ammo > 0
        ? new[] { StandingRule.None, StandingRule.Skirmish, StandingRule.FallBackWhenWorn, StandingRule.NoChase }
        : new[] { StandingRule.None, StandingRule.ChargeWhenClose, StandingRule.FallBackWhenWorn, StandingRule.NoChase };

    public static string RuleText(StandingRule r) => r switch
    {
        StandingRule.ChargeWhenClose => "charge when they close", StandingRule.FallBackWhenWorn => "fall back when worn",
        StandingRule.Skirmish => "skirmish: keep your distance", StandingRule.NoChase => "do not chase", _ => "no standing order",
    };

    /// The short tag on the company's card.
    public static string RuleTag(StandingRule r) => r switch
    {
        StandingRule.ChargeWhenClose => "WAIT", StandingRule.FallBackWhenWorn => "WORN", StandingRule.Skirmish => "SKIRM", StandingRule.NoChase => "STAY", _ => "",
    };

    /// The order a company is following now, as a step (the start of its queue line on the field).
    public OrderStep StepOf(Squad s) => new(s.Order, s.Anchor, s.Front, s.Columns, s.Target != null ? Squads.IndexOf(s.Target) : -1);

    /// Is the company's current order done? Holding: it stands formed where it was sent. Attacking a company: it broke.
    /// A plain charge or follow never ends on its own.
    bool StepDone(Squad s)
    {
        if (s.Alive == 0) return false;
        if (s.Order == Order.Charge) return s.StepTarget is { } t && t.Alive == 0;
        if (s.Order != Order.Hold) return false;
        int cols = Math.Clamp(s.Columns, 1, Math.Max(1, s.Alive)), rows = (s.Alive + cols - 1) / cols;
        var mid = s.Anchor - s.Front * (rows - 1) * s.Spacing / 2;
        return Vector2.DistanceSquared(s.Center, mid) < 16 && s.Formed > 0.5f;
    }

    /// Each tick: the next queued step once the current one is done; then the company's standing rule.
    void UpdateOrders(Squad s)
    {
        if (s.Queue.Count > 0 && StepDone(s))
        {
            var st = s.Queue[0];
            s.Queue.RemoveAt(0);
            s.Order = st.Order;
            s.Anchor = st.Anchor;
            s.Front = st.Front;
            s.Columns = st.Columns;
            s.Target = s.StepTarget = st.Target >= 0 && st.Target < Squads.Count ? Squads[st.Target] : null;
        }
        if (s.Rule != StandingRule.None && s.Alive > 0 && (s.Division == null || s.Override)) KeepRule(s);
    }

    void KeepRule(Squad s)
    {
        var foe = NearestFoe(s);
        float gap = foe == null ? float.MaxValue : Vector2.Distance(foe.Center, s.Center) - foe.Radius - s.Radius;
        var away = foe == null ? -s.Front : s.Center - foe.Center;
        if (away.LengthSquared() < 1e-3f) away = -s.Front;
        away.Normalize();
        switch (s.Rule)
        {
            case StandingRule.ChargeWhenClose when s.Order == Order.Hold && s.Queue.Count == 0 && foe != null && gap < CloseCharge:
                s.Order = Order.Charge;
                s.Target = foe;
                Log($"{s.Name}: they are close, charge!");
                break;
            case StandingRule.FallBackWhenWorn when !s.RuleFired && (s.Alive < s.StartCount * 0.5f || AvgMorale(s) < 35):
                s.RuleFired = true;
                s.Queue.Clear();
                s.HoldInPlace();
                s.Front = -away;
                s.Anchor = s.Center + away * WornFallBack;
                s.BreakOffUntil = Time + 8; // they walk out of the fight rather than stand in it
                Log($"{s.Name} are worn: they fall back.");
                break;
            case StandingRule.Skirmish when foe != null && gap < SkirmishGap && Time >= s.RuleAt && foe.Units[0].Type.Ammo == 0: // archers out-shoot each other; it is foot and horse they run from
                s.RuleAt = Time + 2;
                s.Queue.Clear();
                s.Order = Order.Hold;
                s.Front = -away;
                s.Anchor = s.Center + away * (SkirmishGap - gap + 8);
                break;
            case StandingRule.NoChase:
                s.Pursue = false;
                // The company it fought broke, and nothing stands near: hold there instead of running after them.
                if (s.Order == Order.Charge && s.Fought is { Alive: 0 } && s.Queue.Count == 0 && (foe == null || gap > 20) && !s.InContact)
                {
                    s.HoldInPlace();
                    s.Fought = null;
                    if (s.Plan == Plan.Pursue) s.Plan = Plan.None;
                    Log($"{s.Name} hold: they do not chase.");
                }
                break;
        }
    }
}
