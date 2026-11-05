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
if (args.Contains("--selftest"))
{
    Ironfield.SelfTest.Run();
    return;
}

Ironfield.CrashLog.Install();
using var game = new Ironfield.ScreenHost();
game.Run();
