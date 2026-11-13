if (args.Contains("--sprites"))
{
    var rest = args.SkipWhile(a => a != "--sprites").Skip(1).ToArray();
    Ironfield.SpriteDump.Run(rest.FirstOrDefault() ?? "sprites.png", rest.Skip(1).ToArray());
    return;
}
if (args.Contains("--portraits"))
{
    var rest = args.SkipWhile(a => a != "--portraits").Skip(1).ToArray();
    Ironfield.SpriteDump.Portraits(rest.FirstOrDefault() ?? "portraits.png", rest.Length > 1 && int.TryParse(rest[1], out int ps) ? ps : 7);
    return;
}
if (args.Contains("--sounds"))
{
    var rest = args.SkipWhile(a => a != "--sounds").Skip(1).ToArray();
    Ironfield.SoundDump.Run(rest.FirstOrDefault() ?? "sounds", rest.ElementAtOrDefault(1));
    return;
}
if (args.Contains("--write-controls"))
{
    Ironfield.Controls.WriteDefaults(Ironfield.Controls.FileName);
    Console.WriteLine("wrote " + Ironfield.Controls.FileName);
    return;
}
if (args.Contains("--balance"))
{
    int n = args.SkipWhile(a => a != "--balance").Skip(1).Select(a => int.TryParse(a, out var v) ? v : 0).FirstOrDefault();
    Ironfield.BalanceReport.Run(n > 0 ? n : 4);
    return;
}
if (args.Contains("--pacing"))
{
    int n = args.SkipWhile(a => a != "--pacing").Skip(1).Select(a => int.TryParse(a, out var v) ? v : 0).FirstOrDefault();
    Ironfield.BalanceReport.Pacing(n > 0 ? n : 4);
    return;
}
if (args.Contains("--realms"))
{
    int n = args.SkipWhile(a => a != "--realms").Skip(1).Select(a => int.TryParse(a, out var v) ? v : 0).FirstOrDefault();
    Ironfield.BalanceReport.Realms(n > 0 ? n : 8);
    return;
}
if (args.Contains("--arcana"))
{
    int n = args.SkipWhile(a => a != "--arcana").Skip(1).Select(a => int.TryParse(a, out var v) ? v : 0).FirstOrDefault();
    Ironfield.BalanceReport.Arcana(n > 0 ? n : 4);
    return;
}
if (args.Contains("--agree"))
{
    var nums = args.SkipWhile(a => a != "--agree").Skip(1).Select(a => float.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : -1).TakeWhile(v => v >= 0).ToList();
    Ironfield.BalanceReport.Agree(nums.Count > 0 ? (int)nums[0] : 4, nums.Count > 1 ? nums[1] : 0.5f);
    return;
}
if (args.Contains("--grounds"))
{
    int n = args.SkipWhile(a => a != "--grounds").Skip(1).Select(a => int.TryParse(a, out var v) ? v : 0).FirstOrDefault();
    Ironfield.BalanceReport.Grounds(n > 0 ? n : 8);
    return;
}
if (args.Contains("--campaign-bench"))
{
    int n = args.SkipWhile(a => a != "--campaign-bench").Skip(1).Select(a => int.TryParse(a, out var v) ? v : 0).FirstOrDefault();
    Ironfield.Bench.Campaign(n > 0 ? n : 30);
    return;
}
if (args.Contains("--bench"))
{
    int n = args.SkipWhile(a => a != "--bench").Skip(1).Select(a => int.TryParse(a, out var v) ? v : 0).FirstOrDefault();
    Ironfield.Bench.Run(n > 0 ? n : 750);
    return;
}
if (args.Contains("--replay"))
{
    Ironfield.Replay.Run(args.SkipWhile(a => a != "--replay").Skip(1).FirstOrDefault() ?? Path.Combine(Ironfield.Replay.Folder, "last-battle.json"));
    return;
}
if (args.Contains("--selftest-shard"))
{
    Ironfield.SelfTest.RunShard(args.SkipWhile(a => a != "--selftest-shard").Skip(1).First()); // one core's share of --selftest
    return;
}
if (args.Contains("--selftest"))
{
    // --selftest Name runs one test by its method name (for debugging); no name runs them all, spread over the cores.
    var only = args.SkipWhile(a => a != "--selftest").Skip(1).FirstOrDefault();
    if (only != null) Ironfield.SelfTest.RunOne(only); else Ironfield.SelfTest.RunParallel();
    return;
}

Ironfield.CrashLog.Install();
using var game = new Ironfield.ScreenHost();
game.Run();
