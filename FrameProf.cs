namespace Ironfield;

using System.Diagnostics;

/// Frame profiler for the campaign map (IRONFIELD_FRAMEPROF=1): average milliseconds a frame spent in named sections,
/// printed every 120 frames, with the frame interval (what the player calls fps). Off, `Begin`/`End` do nothing.
public static class FrameProf
{
    public static readonly bool On = Environment.GetEnvironmentVariable("IRONFIELD_FRAMEPROF") is { Length: > 0 } || Environment.GetEnvironmentVariable("IRONFIELD_MAP_BENCH") == "1";
    static readonly Dictionary<string, double> sums = new();
    static readonly Dictionary<string, long> starts = new();
    static int frames;
    static long lastFrame;

    /// Add a count (draw calls, sprites) under `name`, averaged per frame like the timings.
    public static void Count(string name, long n) { if (On) sums[name] = sums.GetValueOrDefault(name) + n; }

    public static void Begin(string name) { if (On) starts[name] = Stopwatch.GetTimestamp(); }

    public static void End(string name)
    {
        if (!On || !starts.TryGetValue(name, out long t0)) return;
        sums[name] = sums.GetValueOrDefault(name) + Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    /// Reset accumulators (used between benchmark stages).
    public static void Reset() { sums.Clear(); starts.Clear(); frames = 0; lastFrame = 0; }

    /// Record one frame's interval without auto-flushing.
    public static void Tick()
    {
        if (!On) return;
        long now = Stopwatch.GetTimestamp();
        if (lastFrame != 0) sums["interval"] = sums.GetValueOrDefault("interval") + Stopwatch.GetElapsedTime(lastFrame, now).TotalMilliseconds;
        lastFrame = now;
        frames++;
    }

    /// Print the current accumulated averages immediately and reset.
    public static void Flush(string label)
    {
        if (!On || frames <= 0) return;
        double interval = sums.GetValueOrDefault("interval") / Math.Max(1, frames - 1);
        Console.WriteLine($"frameprof {label} ({frames}f): {1000 / Math.Max(0.001, interval):0.0} fps ({interval:0.00} ms/frame); " + string.Join(", ", sums.Where(kv => kv.Key != "interval").OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / frames:0.00}")));
        Reset();
    }

    /// Call once at the end of each drawn frame.
    public static void Frame(string label)
    {
        if (!On) return;
        long now = Stopwatch.GetTimestamp();
        if (lastFrame != 0) sums["interval"] = sums.GetValueOrDefault("interval") + Stopwatch.GetElapsedTime(lastFrame, now).TotalMilliseconds;
        lastFrame = now;
        if (++frames < 120) return;
        double interval = sums.GetValueOrDefault("interval") / frames;
        Console.WriteLine($"frameprof {label}: {1000 / Math.Max(0.001, interval):0.0} fps ({interval:0.00} ms/frame); " + string.Join(", ", sums.Where(kv => kv.Key != "interval").OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / frames:0.00}")));
        sums.Clear(); frames = 0;
    }
}
