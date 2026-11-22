using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// How far men keep their detail in the 3D battle, set by the frame rate (drawing only). `LodGovernor` holds one level
/// (0..1) that slides every distance between its floor (the cheap one, what a weak machine falls back to) and its ceiling
/// (the full look from before the speed pass). `IRONFIELD_LOD=0..1` pins it (benchmarks); shots always draw at full detail.
sealed partial class BattleScreen
{
    static readonly float? PinnedLod = float.TryParse(Environment.GetEnvironmentVariable("IRONFIELD_LOD"),
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var l) ? Math.Clamp(l, 0f, 1f) : null;

    readonly LodGovernor lod = new();
    long lodLast;

    /// The level now (0 = the floor distances, 1 = the ceiling).
    public float LodLevel => lod.Level;

    void ResetLod() { lod.Reset(); lodLast = 0; }

    /// Once a drawn frame: time it against the last and move the level.
    void UpdateLod()
    {
        long now = Stopwatch.GetTimestamp();
        float sec = lodLast == 0 ? 0 : (now - lodLast) / (float)Stopwatch.Frequency;
        lodLast = now;
        if (PinnedLod is { } pin) lod.Pin(pin);
        else if (HasShots) lod.Pin(1f);
        else lod.Frame(sec);
    }

    const float AlwaysFull = LodGovernor.AlwaysFull;
    (float Near, float Mid, int FullCap) LodDistances() => lod.Distances();
}

/// The frame-rate governor behind the battle's detail distances (headless, so `SelfTest.LodFollowsFrameRate` drives it with
/// made-up frame times). It drops fast while frames are slow, climbs back slowly once they have been smooth a while, and
/// remembers where it last had to drop so it does not climb straight back into the same stutter.
public sealed class LodGovernor
{
    // The clamps: full detail (Detail 0) out to NearMin..NearMax m, the middle look (Detail 1) to MidMin..MidMax m, the far
    // look beyond; past AlwaysFull m from the eye the full look also stops at FullCapMin..FullCapMax men.
    public const float NearMin = 15f, NearMax = 40f, MidMin = 38f, MidMax = 125f, AlwaysFull = 10f;
    public const int FullCapMin = 48, FullCapMax = 600;
    /// Below SlowFps the level drops; at SmoothFps or better for CalmFor seconds it climbs.
    public const float SlowFps = 50f, SmoothFps = 54f, CalmFor = 1.2f, DropRate = 0.5f, ClimbRate = 0.16f, CeilingRelax = 0.12f;

    float calm;

    /// 0 = the floor distances, 1 = the ceiling. Starts at the ceiling: a machine that copes never sees it move.
    public float Level { get; private set; } = 1f;
    /// The most the level may climb to now: just above where it last had to drop, easing back up to 1.
    public float Ceiling { get; private set; } = 1f;
    /// The smoothed frame rate (about a quarter second of memory).
    public float Fps { get; private set; } = 60f;

    public void Pin(float level) => Level = Ceiling = Math.Clamp(level, 0f, 1f);
    public void Reset() { Level = Ceiling = 1f; Fps = 60f; calm = 0; }

    /// One drawn frame that took `sec` seconds.
    public void Frame(float sec)
    {
        if (sec <= 0 || sec > 0.25f) return; // the first frame, a load or a window drag: not a measure of drawing
        Fps += (1f / sec - Fps) * MathF.Min(1f, sec * 4f);
        if (Fps < SlowFps)
        {
            calm = 0;
            Level = MathF.Max(0f, Level - DropRate * sec * (1f + (SlowFps - Fps) / SlowFps * 2f));
            Ceiling = MathF.Min(Ceiling, Level + 0.08f);
        }
        else if (Fps >= SmoothFps)
        {
            calm += sec;
            if (calm >= CalmFor)
            {
                Ceiling = MathF.Min(1f, Ceiling + CeilingRelax * 1.5f * sec);
                Level = MathF.Min(Ceiling, Level + ClimbRate * sec);
            }
            else Ceiling = MathF.Min(1f, Ceiling + CeilingRelax * 0.5f * sec);
        }
        else
        {
            calm = MathF.Max(0f, calm - sec);
            Ceiling = MathF.Min(1f, Ceiling + CeilingRelax * 0.25f * sec);
        }
    }

    /// The distances at the level now: full detail within Near m, the middle look within Mid m; FullCap men at most get the
    /// full look past AlwaysFull m.
    public (float Near, float Mid, int FullCap) Distances() =>
        (MathHelper.Lerp(NearMin, NearMax, Level), MathHelper.Lerp(MidMin, MidMax, Level), (int)MathHelper.Lerp(FullCapMin, FullCapMax, Level));
}
