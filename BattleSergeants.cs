using Microsoft.Xna.Framework;

namespace Ironfield;

/// What a company's sergeant is doing now (shown on its card).
public enum SgtTask { None, Dressing, Reforming, Rallying, HoldingBack, Down }

/// Battle: every company's sergeant keeps it in good order (docs/OFFICERS.md OF5). He never changes its orders. He dresses
/// the ranks (they re-form a quarter quicker), re-forms them after a charge, rallies wavering men near him and holds back men
/// straining to break ranks. When he falls the company fights a grade worse until another man steps up.
public partial class Battle
{
    /// How much drill a company loses while its sergeant is down (about one grade).
    public const float SergeantLoss = 0.25f;

    /// Seconds before another man steps up: 8 in an elite company, 24 among rabble.
    public static float StepUpTime(Squad s) => 8 + 16 * (1 - Math.Clamp(s.Drill + s.SgtPenalty, 0, 1));

    /// The man who keeps the company's order: a named man first (the roll's), then the best of its men by tier.
    static Unit? PickSergeant(Squad s) => s.Units
        .Where(u => u.Active && !u.Routing && !u.Hero && !u.IsLeader && !u.IsPlayer && !u.Sergeant)
        .OrderByDescending(u => u.SoldierId != 0).ThenByDescending(u => u.Type.Tier).ThenBy(u => u.Index).FirstOrDefault();

    /// Whether this company has a sergeant at all: not beasts, the raised dead or a swarm.
    static bool HasSergeant(Squad s) => !s.Swarm && s.Units.Count > 0 && !s.Units[0].Type.Wild && !s.Units[0].Type.Has("raised");

    /// Whether the company's sergeant is up and about.
    public static bool SergeantUp(Squad s) => s.Sgt is { Active: true, Routing: false };

    /// Gives every company that has none yet its sergeant now (they are otherwise appointed at their first look).
    public void AppointSergeants()
    {
        foreach (var s in Squads) if (s.Sgt == null && s.SgtDownAt < 0 && HasSergeant(s)) s.Sgt = PickSergeant(s);
    }

    /// Once a second for each company: appoint, lose and replace its sergeant, choose what he does, and rally men near him.
    void UpdateSergeant(Squad s)
    {
        if (!HasSergeant(s) || s.Alive == 0) { s.SgtDoing = SgtTask.None; return; }
        if (s.Sgt == null && s.SgtDownAt < 0) s.Sgt = PickSergeant(s);
        if (!SergeantUp(s))
        {
            if (s.SgtDownAt < 0)
            {
                // He falls: the company loses a grade of its drill until another man steps up.
                s.SgtDownAt = Time;
                s.SgtPenalty = MathF.Min(SergeantLoss, s.Drill);
                s.Drill -= s.SgtPenalty;
                if (s.Team == 0 && s.Sgt != null) Log($"The {s.Name} have lost their sergeant.");
                s.Sgt = null;
            }
            if (Time - s.SgtDownAt >= StepUpTime(s) && PickSergeant(s) is Unit next)
            {
                s.Sgt = next;
                s.Drill = MathF.Min(1, s.Drill + s.SgtPenalty);
                s.SgtPenalty = 0;
                s.SgtDownAt = -1;
                if (s.Team == 0) Log($"A man of the {s.Name} steps up as sergeant.");
            }
            else { s.SgtDoing = SgtTask.Down; return; }
        }
        var sgt = s.Sgt!;
        int wavering = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing && u.Morale < 40) wavering++;
        s.SgtDoing = s.Broken || s.BrokeRanks ? SgtTask.Reforming
            : s.Restless > 0.3f ? SgtTask.HoldingBack
            : wavering * 10 >= s.Alive && wavering > 0 ? SgtTask.Rallying
            : s.Formed < 0.9f ? SgtTask.Dressing : SgtTask.None;
        if (s.SgtDoing != SgtTask.Rallying) return;
        foreach (var u in s.Units)
            if (u.Active && !u.Routing && u.Morale < 55 && Vector2.DistanceSquared(u.Pos, sgt.Pos) < 100) u.Morale += 2.5f;
    }

    /// The sergeant's work in a few words, for the card and the hover card.
    public static string SergeantText(SgtTask t) => t switch
    {
        SgtTask.Dressing => "DRESSING THE RANKS",
        SgtTask.Reforming => "RE-FORMING",
        SgtTask.Rallying => "RALLYING",
        SgtTask.HoldingBack => "HOLDING THEM BACK",
        SgtTask.Down => "SERGEANT DOWN",
        _ => "",
    };

    /// The same, short enough for a company card.
    public static string SergeantTag(SgtTask t) => t switch
    {
        SgtTask.Dressing => "DRESSING",
        SgtTask.Reforming => "RE-FORMING",
        SgtTask.Rallying => "RALLYING",
        SgtTask.HoldingBack => "HOLDING",
        SgtTask.Down => "NO SERGEANT",
        _ => "",
    };
}
