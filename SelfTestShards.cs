using System.Diagnostics;
using System.Globalization;

namespace Ironfield;

/// `--selftest` on several cores: the tests split into one shard per core, each run in its own process (tests share static
/// state, so a process each keeps them apart). Each test's time is remembered between runs, and the next run deals the
/// slowest tests out first so the shards finish together. `IRONFIELD_SELFTEST_SERIAL=1` runs them one after another in this process.
public static partial class SelfTest
{
    static string TimesFile => Path.Combine(Path.GetTempPath(), "ironfield-selftest-times.txt");
    const string TimeMark = "##time ";
    const string FailMark = "FAILED ";

    /// Run every test across the cores, print each shard's output in turn, and end "selftest ok" only if all passed.
    public static void RunParallel()
    {
        int n = Math.Clamp(Environment.ProcessorCount, 1, 8);
        if (n == 1 || Environment.GetEnvironmentVariable("IRONFIELD_SELFTEST_SERIAL") == "1") { Run(); return; }
        var times = LoadTimes();
        var names = Tests.Select(t => t.Method.Name).ToList();
        // Longest first onto the least loaded shard; a test never timed counts as two seconds. Each shard keeps the serial order.
        var load = new double[n];
        var shards = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        foreach (int i in Enumerable.Range(0, names.Count).OrderByDescending(i => times.GetValueOrDefault(names[i], 2)))
        {
            int s = Array.IndexOf(load, load.Min());
            shards[s].Add(i);
            load[s] += times.GetValueOrDefault(names[i], 2);
        }
        var clock = Stopwatch.StartNew();
        var runs = shards.Where(s => s.Count > 0).Select(s => Start(string.Join(",", s.Order().Select(i => names[i])))).ToList();
        bool ok = true;
        var failed = new List<string>();
        foreach (var (proc, output) in runs)
        {
            proc.WaitForExit();
            string text = output.Result;
            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith(TimeMark))
                {
                    var p = line[TimeMark.Length..].Trim().Split(' ');
                    if (p.Length == 2 && double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double sec)) times[p[0]] = sec;
                }
                else if (line.TrimEnd() is { Length: > 0 } l && l != "selftest ok" && !l.StartsWith("selftest failed: "))
                {
                    if (l.StartsWith(FailMark)) failed.Add(l[FailMark.Length..].Split(':')[0]);
                    Console.WriteLine(l);
                }
            }
            ok &= proc.ExitCode == 0 && text.Contains("selftest ok");
        }
        SaveTimes(times);
        Console.WriteLine($"{names.Count} tests on {runs.Count} cores in {clock.Elapsed.TotalSeconds:0}s");
        if (!ok)
        {
            Console.WriteLine(failed.Count > 0
                ? $"selftest failed: {failed.Count} of {names.Count} ({string.Join(", ", failed)})"
                : "selftest failed: a shard died (see above)");
            Environment.Exit(1);
        }
        Console.WriteLine("selftest ok");
    }

    /// One shard: these tests in this order, each timed for the next run's dealing. A failing test is reported and the rest
    /// still run; the shard ends "selftest ok" only if none failed.
    public static void RunShard(string list) => RunNames(list.Split(',', StringSplitOptions.RemoveEmptyEntries));

    /// Run the named tests, each caught on its own so one failure never hides the rest; exit 1 if any failed.
    static void RunNames(IReadOnlyList<string> names)
    {
        var byName = Tests.ToDictionary(t => t.Method.Name);
        var failed = new List<string>();
        foreach (var name in names)
        {
            var clock = Stopwatch.StartNew();
            try { byName[name](); }
            catch (Exception e)
            {
                failed.Add(name);
                Console.WriteLine(FailMark + name + ": " + (e.Message.StartsWith("selftest failed: ") ? e.Message[17..] : e.ToString()));
            }
            Console.WriteLine(TimeMark + name + " " + clock.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        }
        if (failed.Count > 0)
        {
            Console.WriteLine($"selftest failed: {failed.Count} of {names.Count} ({string.Join(", ", failed)})");
            Environment.Exit(1);
        }
        Console.WriteLine("selftest ok");
    }

    static (Process, Task<string>) Start(string list)
    {
        string self = Environment.ProcessPath!;
        var psi = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        // Run as `dotnet Ironfield.dll`: hand the dll back to dotnet.
        if (Path.GetFileNameWithoutExtension(self) == "dotnet") psi.ArgumentList.Add(typeof(SelfTest).Assembly.Location);
        psi.ArgumentList.Add("--selftest-shard");
        psi.ArgumentList.Add(list);
        var proc = Process.Start(psi)!;
        var err = proc.StandardError.ReadToEndAsync();
        var output = Task.Run(async () => await proc.StandardOutput.ReadToEndAsync() + await err);
        return (proc, output);
    }

    static Dictionary<string, double> LoadTimes()
    {
        var times = new Dictionary<string, double>();
        try
        {
            foreach (var line in File.ReadAllLines(TimesFile))
                if (line.Split(' ') is [var name, var sec] && double.TryParse(sec, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) times[name] = v;
        }
        catch (IOException) { }
        return times;
    }

    static void SaveTimes(Dictionary<string, double> times)
    {
        try { File.WriteAllLines(TimesFile, times.OrderBy(t => t.Key).Select(t => $"{t.Key} {t.Value.ToString("0.000", CultureInfo.InvariantCulture)}")); }
        catch (IOException) { }
    }
}
