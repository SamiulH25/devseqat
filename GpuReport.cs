using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Roadmap E3: `--gpu-report` (or IRONFIELD_GPU_REPORT=1) opens a quick battle, draws it at each graphics quality for a
/// couple of seconds, and writes what the driver is and how fast each path ran to gpu-report.txt next to the saves.
/// Testers on real hardware send that file; the README keeps the known-good list.
sealed class GpuReport
{
    public static bool Requested => Environment.GetCommandLineArgs().Contains("--gpu-report")
        || Environment.GetEnvironmentVariable("IRONFIELD_GPU_REPORT") == "1"
        || QuickBench;
    public static bool QuickBench => Environment.GetEnvironmentVariable("IRONFIELD_QUICK_BENCH") == "1";

    readonly ScreenHost host;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly List<string> lines = new();
    readonly (string Name, Quality Q, int BenchStage)[] stages;
    int stage = -1, frames;
    double stageStart;

    public GpuReport(ScreenHost host, bool shadersBuilt, string? shaderError)
    {
        this.host = host;
        stages = QuickBench
            ? new[]
            {
                ("Deploy-Overview-High", Quality.High, 0),
                ("Melee-Field-High", Quality.High, 1),
                ("Melee-Overview-High", Quality.High, 2),
                ("Melee-Overview-Med", Quality.Medium, -1),
                ("Melee-Overview-Low", Quality.Low, -1),
            }
            : new[]
            {
                ("High", Quality.High, -1),
                ("Medium", Quality.Medium, -1),
                ("Low", Quality.Low, -1),
            };
        var gd = host.GraphicsDevice;
        var a = GraphicsAdapter.DefaultAdapter;
        lines.Add($"Ironfield GPU report, {DateTime.Now:yyyy-MM-dd HH:mm}");
        lines.Add($"OS: {Environment.OSVersion}  64-bit: {Environment.Is64BitProcess}  .NET {Environment.Version}");
        lines.Add($"Adapter: {a.Description}  display {a.CurrentDisplayMode.Width}x{a.CurrentDisplayMode.Height}");
        lines.Add($"Profile: {gd.GraphicsProfile}  back buffer {gd.PresentationParameters.BackBufferWidth}x{gd.PresentationParameters.BackBufferHeight}");
        lines.Add(shadersBuilt ? "Shaders: compiled (GLSL effects loaded)" : $"Shaders: FAILED ({shaderError}) - the game runs on the Low path");
    }

    /// Called once per drawn frame; returns true when the report is done.
    public bool Frame(Quality current, BattleScreen battle)
    {
        bool fellBack = stage >= 0 && current == Quality.Low && stages[stage].Q != Quality.Low;
        double now = clock.Elapsed.TotalSeconds;
        if (stage < 0) { Next(now, battle); return false; }
        frames++;
        if (now - stageStart < 3.0) return false;
        var st = stages[stage];
        double dt = now - stageStart;
        string result = fellBack && st.Q != Quality.Low ? "FAILED (fell back to Low)" : $"{frames / dt:0.0} fps ({dt * 1000 / Math.Max(1, frames):0.0} ms/frame)";
        lines.Add($"{st.Name,-22}: {result}");
        if (fellBack) host.Quality = Quality.Low;
        if (++stage >= stages.Length) { Write(); return true; }
        Next(now, battle, false);
        return false;
    }

    void Next(double now, BattleScreen battle, bool first = true)
    {
        if (first) stage = 0;
        var st = stages[stage];
        host.Quality = host.Shading == null && st.Q != Quality.Low ? Quality.Low : st.Q;
        if (st.BenchStage >= 0) battle.BenchStage(st.BenchStage);
        frames = 0;
        stageStart = clock.Elapsed.TotalSeconds;
    }

    void Write()
    {
        var path = Path.Combine(Settings.Folder, "gpu-report.txt");
        try { Directory.CreateDirectory(Settings.Folder); File.WriteAllLines(path, lines); lines.Add("written to " + path); }
        catch (Exception e) { lines.Add("could not write the report: " + e.Message); }
        foreach (var l in lines) Console.WriteLine(l);
    }
}
