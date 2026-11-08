namespace Ironfield;

/// Easing curves for animation (headless: plain floats). Each maps t in [0, 1] to [0, 1] (Back overshoots in between).
static class Ease
{
    static float C(float t) => Math.Clamp(t, 0, 1);
    /// Slow start, fast finish.
    public static float In(float t) { t = C(t); return t * t; }
    /// Fast start, slow finish.
    public static float Out(float t) { t = 1 - C(t); return 1 - t * t; }
    public static float OutCubic(float t) { t = 1 - C(t); return 1 - t * t * t; }
    /// Slow at both ends (smoothstep).
    public static float InOut(float t) { t = C(t); return t * t * (3 - 2 * t); }
    /// Fast start that runs past the end and settles back (a follow-through).
    public static float OutBack(float t, float over = 1.7f) { t = C(t) - 1; return 1 + t * t * ((over + 1) * t + over); }
    /// A damped spring from 0 to 1: overshoots and rings down.
    public static float Spring(float t, float damping = 6f) => 1 - MathF.Exp(-damping * C(t)) * MathF.Cos(C(t) * 14f);
    /// A kick that rises fast and decays back to 0 (hit reactions): 0 at t = 0 and t = 1, peak near t = 0.15.
    public static float Kick(float t) { t = C(t); return MathF.Sin(MathF.Min(1, t / 0.3f) * MathF.PI * 0.5f) * (1 - Out((t - 0.15f) / 0.85f)); }
    /// A stable 0..1 number from an integer (per-man phase so walks and idles are never in step).
    public static float Hash(int n) { uint h = (uint)n * 2654435761u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; return (h & 0xFFFF) / 65535f; }
}
