using Microsoft.Xna.Framework;

namespace Ironfield;

/// The kinds of cinematic beat, lowest first: a higher one may cut into a lower one that is running (docs/SPECTACLE.md S1).
public enum BeatKind { None, Charge, FoeWorking, OwnWorking, Clash, Breakthrough }

/// The battle's one cinematic beat (docs/SPECTACLE.md rule 6): a slowed view, bars top and bottom, a quieter field.
/// Drawing only; it runs on the screen's real clock. One beat a battle, first come first served, except that a higher kind
/// may take over a lower one while it runs. Headless so the self-test can check the rule.
public sealed class Beats
{
    public BeatKind Kind { get; private set; }
    public Vector2 At { get; private set; }
    float start = float.NegativeInfinity, length; // no beat yet
    bool spent;

    /// How slow the view runs at the height of a beat (1 = normal).
    public const float Slow = 0.35f;
    const float In = 0.35f, Out = 0.6f;

    /// Ask for the beat at `now` (real seconds); true if granted.
    public bool Ask(BeatKind kind, Vector2 at, float now, float length = 2.6f)
    {
        if (kind == BeatKind.None) return false;
        if (Running(now)) { if (kind <= Kind) return false; }
        else if (spent) return false;
        Kind = kind; At = at; start = now; this.length = length; spent = true;
        return true;
    }

    public bool Running(float now) => now - start < length;
    public bool Spent => spent;

    /// How far into the beat the view is: eases in, holds, eases out (0 = no beat).
    public float Amount(float now)
    {
        if (!Running(now)) return 0;
        float t = now - start;
        return Ease.InOut(MathF.Min(1, MathF.Min(t / In, (length - t) / Out)));
    }

    /// The view's time scale now.
    public float Scale(float now) => MathHelper.Lerp(1, Slow, Amount(now));

    /// A new battle: the beat is unspent again.
    public void Reset() { Kind = BeatKind.None; start = float.NegativeInfinity; spent = false; }
}
