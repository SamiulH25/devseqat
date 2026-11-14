using System.Globalization;
using System.Text;

namespace Ironfield;

/// `--arcana [n]` (docs/ARCANA.md AR0): how much magic and aura a campaign holds. n seeds, each played twice for 240 days by the
/// stand-in captain: once seeking the arcane (vigils, its circle's rites) and once not. Writes balance/arcana.csv and prints
/// each run's line against the targets.
public static partial class BalanceReport
{
    public const int ArcanaDays = 240;

    /// One monthly sample of the arcane in a campaign.
    public sealed record ArcanaSample(int Day, int Lords, int LordCircles, int Offers, int Battles, int MagicBattles, int EnemyMagic,
        int Aura, float AuraXp, bool Circle, int Forms, float Omen, int Vigils, int Rites)
    {
        public static string Header => "seed,seeker,day,lords,lord_circles,offers,battles,magic_battles,enemy_magic,aura,aura_xp,circle,forms,omen,vigils,rites";
        public string Csv(int seed, bool seeker) => string.Join(",", seed, seeker ? 1 : 0, Day, Lords, LordCircles, Offers, Battles, MagicBattles, EnemyMagic,
            Aura, AuraXp.ToString("0", CultureInfo.InvariantCulture), Circle ? 1 : 0, Forms, Omen.ToString("0.0", CultureInfo.InvariantCulture), Vigils, Rites);
    }

    /// The arcane as it stands now.
    public static ArcanaSample Arcana(Campaign c, Pilot p)
    {
        var lords = c.S.Parties.Where(q => q.Kind == PartyKind.Lord && !q.Down).ToList();
        return new ArcanaSample(c.Day, lords.Count, lords.Count(q => q.Companies.Any(k => Rules.RoleOf(k) == "casters")),
            c.S.Settlements.Sum(t => t.Mercenaries.Count(m => Rules.RoleOf(m) == "casters")), p.Battles, p.MagicBattles, p.EnemyMagic,
            c.Captain.AuraTier, c.Captain.AuraXp, c.HasCircle, c.Captain.Forms.Count, c.S.Omen, p.Vigils, p.Rites);
    }

    /// Play one campaign for `days`, sampling every 30 days.
    public static (Pilot Pilot, List<ArcanaSample> Samples) ArcanaRun(int seed, bool seeker, int days)
    {
        var c = Campaign.New(seed);
        var p = new Pilot(c) { Seeker = seeker };
        var samples = new List<ArcanaSample>();
        for (int d = 30; d <= days && !c.S.GameOver; d += 30)
        {
            p.Run(30 * 24);
            samples.Add(Arcana(c, p));
        }
        return (p, samples);
    }

    public static void Arcana(int campaigns)
    {
        Directory.CreateDirectory("balance");
        var csv = new StringBuilder(ArcanaSample.Header + "\n");
        for (int seed = 1; seed <= campaigns; seed++)
            foreach (bool seeker in new[] { true, false })
            {
                var (p, samples) = ArcanaRun(4000 + seed, seeker, ArcanaDays);
                foreach (var s in samples) csv.AppendLine(s.Csv(seed, seeker));
                var day120 = samples.LastOrDefault(s => s.Day <= 125) ?? samples.Last();
                var last = samples.Last();
                string Tier(int t) => p.AuraDay[t] < 0 ? "-" : p.AuraDay[t].ToString();
                Console.WriteLine($"arcana {seed} {(seeker ? "seeker" : "plain ")}: by day {day120.Day} magic in {day120.MagicBattles}/{day120.Battles} battles"
                    + $" ({Share(day120.MagicBattles, day120.Battles)}, target 1 in 6), enemy {day120.EnemyMagic}; all run {last.MagicBattles}/{last.Battles} ({Share(last.MagicBattles, last.Battles)}); against lords (all run) {p.LordMagic}/{p.LordBattles} (target 1 in 3); lords with a circle {last.LordCircles}/{last.Lords};"
                    + $" aura tier days Kindled {Tier(1)} (target {(seeker ? "~30" : "~90")}), Knight {Tier(2)} (~90), Expert {Tier(3)} (~180);"
                    + $" circle {(last.Circle ? "yes" : "no")}, vigils {last.Vigils}, rites {last.Rites}, Omen {last.Omen:0}");
            }
        File.WriteAllText("balance/arcana.csv", csv.ToString());
    }

    static string Share(int n, int of) => of == 0 ? "-" : n == 0 ? "none" : $"1 in {of / (float)n:0.0}";
}
