using System.Globalization;
using System.Text;

namespace Ironfield;

/// `--events N` (docs/pre-demo/EVENTS.md EV1, EV6): N campaigns of 240 days with the stand-in captain; how often each event came
/// up, by what answer, and which never did -> balance/events.csv.
public static partial class BalanceReport
{
    public static void EventReport(int campaigns)
    {
        Directory.CreateDirectory("balance");
        var fired = new Dictionary<string, int>();
        var results = new Dictionary<string, int>();
        int total = 0;
        for (int seed = 1; seed <= campaigns; seed++)
        {
            var c = Campaign.New(5100 + seed);
            new Pilot(c).Run(240 * 24);
            foreach (var h in c.S.EventHistory)
            {
                fired[h.Id] = fired.GetValueOrDefault(h.Id) + 1;
                if (h.Result != "") results[h.Result] = results.GetValueOrDefault(h.Result) + 1;
            }
            total += c.S.EventHistory.Count;
            Console.WriteLine($"events {seed}: {c.S.EventHistory.Count} in 240 days, {c.S.EventHistory.Select(h => h.Id).Distinct().Count()} kinds");
        }
        var csv = new StringBuilder("id,theme,moments,fired,per_campaign,share\n");
        foreach (var e in Campaign.Events.OrderByDescending(e => fired.GetValueOrDefault(e.Id)))
        {
            int n = fired.GetValueOrDefault(e.Id);
            csv.AppendLine(string.Join(",", e.Id, e.Theme, string.Join("|", e.Moments), n,
                (n / (float)campaigns).ToString("0.00", CultureInfo.InvariantCulture), (n / (float)Math.Max(1, total)).ToString("0.000", CultureInfo.InvariantCulture)));
        }
        File.WriteAllText("balance/events.csv", csv.ToString());
        var never = Campaign.Events.Where(e => e.Moments.Count > 0 && !fired.ContainsKey(e.Id)).Select(e => e.Id).ToList();
        var top = fired.OrderByDescending(k => k.Value).Take(5).Select(k => $"{k.Key} {k.Value * 100f / Math.Max(1, total):0.0}%");
        Console.WriteLine($"events: {total / (float)campaigns:0.0} a campaign ({total / (float)campaigns / (240 / 7f):0.0} a week); most: {string.Join(", ", top)}");
        Console.WriteLine($"checks: {string.Join(", ", results.Select(k => $"{k.Key} {k.Value}"))}");
        Console.WriteLine($"never drawn ({never.Count} of {Campaign.Events.Count(e => e.Moments.Count > 0)}): {string.Join(", ", never)}");
        Console.WriteLine("-> balance/events.csv");
    }
}
