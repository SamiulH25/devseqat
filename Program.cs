if (args.Contains("--write-controls"))
{
    Ironfield.Controls.WriteDefaults(Ironfield.Controls.FileName);
    Console.WriteLine("wrote " + Ironfield.Controls.FileName);
    return;
}
if (args.Contains("--selftest"))
{
    Ironfield.SelfTest.Run();
    return;
}

using var game = new Ironfield.BattleGame();
game.Run();
