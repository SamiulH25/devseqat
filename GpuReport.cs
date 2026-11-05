using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Roadmap E3: `--gpu-report` (or IRONFIELD_GPU_REPORT=1) opens a quick battle, draws it at each graphics quality for a
/// couple of seconds, and writes what the driver is and how fast each path ran to gpu-report.txt next to the saves.
/// Testers on real hardware send that file; the README keeps the known-good list.
sealed class GpuReport
{
    public static bool Requested => Environment.GetCommandLineArgs().Contains("--gpu-report") || Environment.GetEnvironmentVariable("IRONFIELD_GPU_REPORT") == "1";

    readonly ScreenHost host;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly List<string> lines = new();
    readonly Quality[] order = { Quality.High, Quality.Medium, Quality.Low };
    int stage = -1, frames;
    double stageStart;

    public GpuReport(ScreenHost host, bool shadersBuilt, string? shaderError)
    {
        this.host = host;
        var gd = host.GraphicsDevice;
        var a = GraphicsAdapter.DefaultAdapter;
        lines.Add($"Ironfield GPU report, {DateTime.Now:yyyy-MM-dd HH:mm}");
        lines.Add($"OS: {Environment.OSVersion}  64-bit: {Environment.Is64BitProcess}  .NET {Environment.Version}");
        lines.Add($"Adapter: {a.Description}  display {a.CurrentDisplayMode.Width}x{a.CurrentDisplayMode.Height}");
        lines.Add($"Profile: {gd.GraphicsProfile}  back buffer {gd.PresentationParameters.BackBufferWidth}x{gd.PresentationParameters.BackBufferHeight}");
        lines.Add(shadersBuilt ? "Shaders: compiled (GLSL effects loaded)" : $"Shaders: FAILED ({shaderError}) - the game runs on the Low path");
    }

    /// Called once per drawn frame; returns true when the report is done.
    public bool Frame(Quality current)
    {
        bool fellBack = stage >= 0 && current == Quality.Low && order[stage] != Quality.Low;
        double now = clock.Elapsed.TotalSeconds;
        if (stage < 0) { Next(now); return false; }
        frames++;
        if (now - stageStart < 2.5) return false;
        var q = order[stage];
        string result = fellBack && q != Quality.Low ? "FAILED (fell back to Low)" : $"{frames / (now - stageStart):0} fps";
        lines.Add($"{q,-7}: {result}");
        if (fellBack) host.Quality = Quality.Low;
        if (++stage >= order.Length) { Write(); return true; }
        Next(now, false);
        return false;
    }

    void Next(double now, bool first = true)
    {
        if (first) stage = 0;
        host.Quality = host.Shading == null && order[stage] != Quality.Low ? Quality.Low : order[stage];
        frames = 0;
        stageStart = now;
    }

    void Write()
    {
        var path = Path.Combine(Settings.Folder, "gpu-report.txt");
        try { Directory.CreateDirectory(Settings.Folder); File.WriteAllLines(path, lines); lines.Add("written to " + path); }
        catch (Exception e) { lines.Add("could not write the report: " + e.Message); }
        foreach (var l in lines) Console.WriteLine(l);
    }
}
