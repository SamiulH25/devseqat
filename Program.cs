if (args.Contains("--selftest"))
{
    Ironfield.SelfTest.Run();
    return;
}

using var game = new Ironfield.BattleGame();
game.Run();
