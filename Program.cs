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
if (args.Contains("--selftest"))
{
    // --selftest Name runs one test by its method name (for debugging); no name runs them all.
    var only = args.SkipWhile(a => a != "--selftest").Skip(1).FirstOrDefault();
    if (only != null) Ironfield.SelfTest.RunOne(only); else Ironfield.SelfTest.Run();
    return;
}

Ironfield.CrashLog.Install();
using var game = new Ironfield.ScreenHost();
game.Run();
