using System.Globalization;
using System.Text;

namespace Ironfield;

/// `dotnet run -- --balance [n]` (A7): runs n headless campaigns and a grid of battles, and writes CSVs to ./balance/
/// for tuning Data/campaign.json and troops.json. Nothing here needs a display.
public static class BalanceReport
{
    /// How long each pacing run lasts (weeks of campaign time).
    public const int PacingWeeks = 40;

    /// Play time per campaign day (F9): about 4.5 minutes, counting the map at mixed speeds, a share of a fought battle every
    /// few days and the town screens. It turns CAMPAIGN_OVERHAUL.md 9's target hours into target days.
    public const float MinutesPerDay = 4.5f;
    public static float Hours(int day) => day * MinutesPerDay / 60;

    /// The spec's target play hours from the start, per career stage, as campaign days (9: 3-5 h to Vassal, 4-6 more to Lord,
    /// 6-10 more to Marshal or King).
    public static readonly (string Stage, int From, int To)[] Targets =
    {
        ("VASSAL", 40, 67), ("LORD", 93, 147), ("MARSHAL", 173, 280),
    };

    /// `--pacing [n]`: only the stand-in captain's runs (1b), for tuning the career's pace quickly.
    public static void Pacing(int campaigns)
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;
        // 1b. Pacing (F0): a scripted stand-in captain plays each campaign; record the day it reaches each career stage, and a
        // weekly line of its progress. Every later overhaul step is tuned against these numbers (CAMPAIGN_OVERHAUL.md 9).
        var stages = new StringBuilder("seed,stage,day,play_hours,battles,won,renown,men,promotions\n");
        var weekly = new StringBuilder("seed,week,day,stage,renown,men,wounded,companies,tier_avg,gold,battles,won\n");
        for (int seed = 1; seed <= campaigns; seed++)
        {
            var c = Campaign.New(2000 + seed);
            var pilot = new Pilot(c);
            var seen = new HashSet<string>();
            for (int week = 1; week <= PacingWeeks && !c.S.GameOver; week++)
            {
                pilot.Run(168);
                foreach (var (stage, day) in pilot.StageDay.Where(kv => seen.Add(kv.Key)))
                    stages.AppendLine(string.Join(",", seed, Csv(stage), day, Hours(day).ToString("0.0", inv), pilot.Battles, pilot.Won, c.Captain.Renown.ToString("0", inv), c.Player.Men, pilot.Promotions));
                var cos = c.Player.Companies;
                weekly.AppendLine(string.Join(",", seed, week, c.Day, Csv(c.Stage), c.Captain.Renown.ToString("0", inv), c.Player.Men, c.Player.Wounded, cos.Count,
                    (cos.Count > 0 ? cos.Average(co => co.Type.Tier) : 0).ToString("0.00", inv), c.S.Gold.ToString("0", inv), pilot.Battles, pilot.Won));
            }
            Console.WriteLine($"pacing {seed}: day {c.Day}, {c.Stage}, renown {c.Captain.Renown:0}, {pilot.Battles} battles ({pilot.Won} won), "
                + $"{pilot.Promotions} promoted ({pilot.Promotions / (float)Math.Max(1, pilot.Battles):0.0} a battle), first tier V day {pilot.FirstT5Day}, {pilot.Taken} seats stormed, vassal renown day {pilot.RenownForVassalDay}, "
                + string.Join(", ", pilot.StageDay.Select(kv => $"{kv.Key} day {kv.Value}")));
            if (c.Rank == Rank.SwornSword && !c.CanSwearFealty(out string why)) Console.WriteLine($"  fealty refused: {why}");
            Console.WriteLine($"  name: {string.Join(" ", c.Me.Traits)} {c.Title}; fled {c.Tally("fled")}, wounds {string.Join(" ", c.Captain.Wounds)}, perks {string.Join(" ", c.Captain.Perks)}");
            foreach (var (stage, from, to) in Targets)
            {
                string got = pilot.StageDay.TryGetValue(stage, out int d) ? $"day {d} ({Hours(d):0.0} h)" : "not reached";
                bool ok = pilot.StageDay.TryGetValue(stage, out d) && d >= from && d <= to;
                Console.WriteLine($"  target {stage} day {from}-{to}: {got} {(ok ? "OK" : "OFF")}");
            }
        }
        File.WriteAllText("balance/pacing.csv", stages.ToString());
        File.WriteAllText("balance/pacing_weekly.csv", weekly.ToString());

    }

    static string Csv(string s) => s.Contains(',') ? $"\"{s}\"" : s;

    public static void Run(int campaigns)
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;

        // 1. Campaign economy and progression, week by week, with an idle contracted player.
        var eco = new StringBuilder("seed,week,player_gold,player_men,lord_parties,lord_men_avg,lord_tier_avg,lord_tier3plus,war0,war1,at_war,bandits,ai_battles,treas0,treas1,treas2\n");
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
                    c.S.Parties.Count(p => p.Kind == PartyKind.Bandit), c.AiBattles,
                    c.S.Factions[0].Treasury.ToString("0", inv), c.S.Factions[1].Treasury.ToString("0", inv),
                    c.S.Factions.Count > 2 ? c.S.Factions[2].Treasury.ToString("0", inv) : "0"));
            }
            Console.WriteLine($"campaign {seed}: done");
        }
        File.WriteAllText("balance/economy.csv", eco.ToString());

        Pacing(campaigns);

        // 2. Battle win rates by army level (AI against AI), and how long they take. Each side is built by the same
        // rule (Armies.Random), so a fair system should sit near 50% on the diagonal; drift shows a balance problem.
        var battles = new StringBuilder("level_a,level_b,seed,winner,seconds,dead\n");
        for (int a = 1; a <= 4; a++)
            for (int b = 1; b <= 4; b++)
                for (int seed = 0; seed < 6; seed++)
                {
                    var rng = new Random(a * 100 + b * 10 + seed);
                    var battle = new Battle(seed, Armies.Random(rng, a, 0), Armies.Random(rng, b, 0), new BattleOptions { AutoPlayer = true, PlayerWounded = true });
                    for (int step = 0; battle.Winner < 0 && step < 60 * 600; step++) battle.Update(1 / 60f);
                    battles.AppendLine(string.Join(",", a, b, seed, battle.Winner, battle.Time.ToString("0", inv), battle.Units.Count(u => u.Dead)));
                }
        File.WriteAllText("balance/battles.csv", battles.ToString());

        // 2b. The same level on both sides: an even fight should be close to a coin toss.
        int even = 0, evenN = 0;
        for (int level = 1; level <= 4; level++)
            for (int seed = 0; seed < 8; seed++)
            {
                var rng = new Random(level * 1000 + seed);
                var battle = new Battle(seed, Armies.Random(rng, level, 0), Armies.Random(rng, level, 1), new BattleOptions { AutoPlayer = true, PlayerWounded = true });
                for (int step = 0; battle.Winner < 0 && step < 60 * 600; step++) battle.Update(1 / 60f);
                evenN++;
                if (battle.Winner == 0) even++;
            }
        Console.WriteLine($"even fights: your side wins {even}/{evenN} ({100f * even / evenN:0}%) with identical army levels");

        // 3. The troop table, as the data has it.
        var troops = new StringBuilder("id,tier,class,hp,armor,damage,cost,upgrade_xp,strength_per_man\n");
        foreach (var t in Troops.All)
            troops.AppendLine(string.Join(",", t.Id, t.Tier, t.Class, t.MaxHp, t.Armor, t.Damage, t.Cost, t.UpgradeXp, Rules.Strength(t, 1).ToString("0.00", inv)));
        File.WriteAllText("balance/troops.csv", troops.ToString());
        Console.WriteLine("wrote balance/economy.csv, balance/pacing.csv, balance/pacing_weekly.csv, balance/battles.csv, balance/troops.csv");
    }
}
