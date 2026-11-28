using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: companies march in formation. An order moves the company's Anchor and Front at once; its formation frame
/// (`Squad.FramePos`, `FrameFront`) follows at the pace of its slowest man, and the men's slots are laid from the frame,
/// so the block moves as a block. It wheels no faster than its outer file can walk, waits for men who fall behind, and
/// turns about on the spot when told to face more than a third of a turn away.
public partial class Battle
{
    /// Move the company's formation frame one tick toward its Anchor and Front.
    void March(Squad s, float dt)
    {
        bool free = s.Order == Order.Charge || s.Alive < 2 || s.Swarm || s.Works != null || s.Path != null || Wall != null || s.Alive == 0
            || s.Broken || s.InContact || s.RelievedBy != null || s.BreakOffUntil > Time;
        if (!s.Framed || free) { Snap(s); return; }
        float dist = Vector2.Distance(s.Anchor, s.FramePos);
        if (dist < 0.3f && Turn(s.FrameFront, s.Front) < 0.03f) { Snap(s); return; }
        float pace = MarchPace(s, dist);
        if (pace <= 0) { Snap(s); return; }
        s.Marching = true;
        int cols = Math.Clamp(s.Columns, 1, Math.Max(1, s.Alive)), rows = (s.Alive + cols - 1) / cols;
        if (Turn(s.FrameFront, s.Front) > 2.1f)
        {
            // About face: each man turns where he stands, the block keeping its ground (its middle stays put).
            var mid = s.FramePos - s.FrameFront * (rows - 1) * s.Spacing / 2;
            s.FrameFront = s.Front;
            s.FramePos = mid + s.Front * (rows - 1) * s.Spacing / 2;
            dist = Vector2.Distance(s.Anchor, s.FramePos);
        }

        // Wait for the men: full pace while they keep up, slowing to a halt as the ranks fall more than a few metres behind.
        float keep = Math.Clamp(1.75f - RankError(s) / 2.5f, 0.2f, 1); // never quite still: a man stuck on a fence must not hold the march for ever
        float half = MathF.Max(2, (cols - 1) * s.Spacing / 2);
        float wheel = MathF.Min(1.2f, pace * 0.9f / half) * MathF.Max(0.25f, keep); // the outer file must keep up as it swings
        s.FrameFront = TurnToward(s.FrameFront, s.Front, wheel * dt);
        if (dist > 1e-3f) s.FramePos += (s.Anchor - s.FramePos) / dist * MathF.Min(dist, pace * keep * dt);
    }

    /// The frame is where the orders put the company: nothing to march.
    static void Snap(Squad s)
    {
        s.FramePos = s.Anchor;
        s.FrameFront = s.Front;
        s.Framed = true;
        s.Marching = false;
    }

    /// How fast the frame goes: a little under the slowest man's pace, so every man can hold his place. Horse go at their
    /// slowest horse's trot near the end and nearly at its gallop on a long ride, as they rode to their places before.
    static float MarchPace(Squad s, float dist)
    {
        float v = float.MaxValue;
        foreach (var u in s.Units)
            if (u.Active && !u.Routing && !u.IsLeader && !u.Hero) v = MathF.Min(v, u.Horse is { } h ? (dist > 12 ? h.Gallop * 0.8f : h.Trot) : u.Type.Speed);
        if (v == float.MaxValue) return 0;
        return v * s.Pace * 0.92f;
    }

    /// The angle in radians between two unit vectors.
    static float Turn(Vector2 a, Vector2 b) => MathF.Acos(Math.Clamp(Vector2.Dot(a, b), -1, 1));

    /// Turn unit vector `from` toward `to` by at most `max` radians.
    static Vector2 TurnToward(Vector2 from, Vector2 to, float max)
    {
        float a = MathF.Atan2(from.Y, from.X), b = MathF.Atan2(to.Y, to.X);
        float d = MathHelper.WrapAngle(b - a);
        if (MathF.Abs(d) <= max) return to;
        a += MathF.Sign(d) * max;
        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }
}
