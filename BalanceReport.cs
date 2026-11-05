using System.Globalization;
using System.Text;

namespace Ironfield;

/// `dotnet run -- --balance [n]` (A7): runs n headless campaigns and a grid of battles, and writes CSVs to ./balance/
/// for tuning Data/campaign.json and troops.json. Nothing here needs a display.
public static class BalanceReport
{
    public static void Run(int campaigns)
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;

        // 1. Campaign economy and progression, week by week, with an idle contracted player.
        var eco = new StringBuilder("seed,week,player_gold,player_men,lord_parties,lord_men_avg,lord_tier_avg,lord_tier3plus,war0,war1,at_war,bandits\n");
        for (int seed = 1; seed <= campaigns; seed++)
        {
            var c = Campaign.New(1000 + seed);
            c.Player.Cooldown = 1e9f; // the player sits out: this measures the world
            for (int week = 1; week <= 20; week++)
            {
                for (int h = 0; h < 168; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; }
                var lords = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down).ToList();
                var cos = lords.SelectMany(l => l.Companies).ToList();
                eco.AppendLine(string.Join(",", seed, week, c.S.Gold.ToString("0", inv), c.Player.Men, lords.Count,
                    (lords.Count > 0 ? lords.Average(l => l.Men) : 0).ToString("0.0", inv),
                    (cos.Count > 0 ? cos.Average(co => co.Type.Tier) : 0).ToString("0.00", inv), cos.Count(co => co.Type.Tier >= 3),
                    c.S.Factions[0].WarScore.ToString("0", inv), c.S.Factions[1].WarScore.ToString("0", inv), c.S.Wars.Count(w => w.Active),
                    c.S.Parties.Count(p => p.Kind == PartyKind.Bandit)));
            }
            Console.WriteLine($"campaign {seed}: done");
        }
        File.WriteAllText("balance/economy.csv", eco.ToString());

        // 2. Battle win rates by army level (AI against AI), and how long they take.
        var battles = new StringBuilder("level_a,level_b,seed,winner,seconds,dead\n");
        for (int a = 1; a <= 4; a++)
            for (int b = 1; b <= 4; b++)
                for (int seed = 0; seed < 2; seed++)
                {
                    var rng = new Random(a * 100 + b * 10 + seed);
                    var battle = new Battle(seed, Armies.Random(rng, a, 0), Armies.Random(rng, b, 0), new BattleOptions { AutoPlayer = true, PlayerWounded = true });
                    for (int step = 0; battle.Winner < 0 && step < 60 * 600; step++) battle.Update(1 / 60f);
                    battles.AppendLine(string.Join(",", a, b, seed, battle.Winner, battle.Time.ToString("0", inv), battle.Units.Count(u => u.Dead)));
                }
        File.WriteAllText("balance/battles.csv", battles.ToString());

        // 3. The troop table, as the data has it.
        var troops = new StringBuilder("id,tier,class,hp,armor,damage,cost,upgrade_xp,strength_per_man\n");
        foreach (var t in Troops.All)
            troops.AppendLine(string.Join(",", t.Id, t.Tier, t.Class, t.MaxHp, t.Armor, t.Damage, t.Cost, t.UpgradeXp, Rules.Strength(t, 1).ToString("0.00", inv)));
        File.WriteAllText("balance/troops.csv", troops.ToString());
        Console.WriteLine("wrote balance/economy.csv, balance/battles.csv, balance/troops.csv");
    }
}
