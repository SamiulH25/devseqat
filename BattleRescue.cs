using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: fighting for your named men (docs/archive/LEGACY.md L8). A named man of yours who falls can be got out of the press:
/// stand over him (within RescueReach) for RescueTime while the fighting goes on, within RescueWindow of his fall, and he is
/// carried off wounded instead of dying. One of them may be the battle's ward: the goal "bring him home".
partial class Battle
{
    public const float RescueReach = 2.5f, RescueTime = 2, RescueWindow = 60;

    /// The named man the "bringhome" goal is about (null: none).
    public Unit? Ward;
    bool anyNamed;
    /// Your named men who have fallen this battle, in the order they fell.
    public readonly List<Unit> Downed = new();

    /// A named man's name, from his company's spec ("" for other men).
    public static string NameOf(Unit u) => u.Squad?.Company?.Named?.FirstOrDefault(m => m.Id == u.SoldierId)?.Name ?? "";

    /// May this fallen named man still be got out?
    public bool Rescuable(Unit u) => u.SoldierId != 0 && u.Team == 0 && u.Dead && !u.Saved && Time - u.DiedAt < RescueWindow;

    /// Each tick: you, standing over a fallen named man, get him out.
    void UpdateRescue(float dt)
    {
        if (!anyNamed || !Player.Active || Winner >= 0) return;
        foreach (var u in Downed)
        {
            if (!Rescuable(u)) continue;
            if (Vector2.Distance(u.Pos, Player.Pos) > RescueReach) { u.Rescue = 0; continue; }
            u.Rescue += dt;
            if (u.Rescue < RescueTime) continue;
            u.Saved = true;
            Log($"You drag {NameOf(u)} out of the press.");
            PlayerHit = ("YOU GET HIM OUT", Time);
        }
    }

    /// Kill a man as the battle would (for tests).
    internal void KillForTest(Unit t, Unit killer) => Kill(t, killer);

    /// The goal's text, with the ward's name.
    public string GoalText(BattleGoal g) => g.Id == "bringhome" && Ward != null ? $"BRING {NameOf(Ward).ToUpperInvariant()} HOME" : g.Text;
}
