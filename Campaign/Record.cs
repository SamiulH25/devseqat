namespace Ironfield;

/// Your record (docs/SIDE_CONTENT.md S7): deeds worth remembering go in the chronicle for good (the log keeps only the last
/// sixty lines), a tally of what you did, and the summary shown when the war ends.
public sealed partial class Campaign
{
    /// A deed: into the log and the chronicle, counted under key ("" = not counted).
    void Deed(string text, string key = "")
    {
        Log(text);
        S.Deeds.Add(new LogEntry { At = S.Hours, Text = text });
        if (S.Deeds.Count > 200) S.Deeds.RemoveAt(0);
        if (key != "") Count(key);
    }

    /// Count something you did (battles, victories, quests, sites, tournaments, robberies, holdouts).
    void Count(string key) => S.Tally[key] = S.Tally.GetValueOrDefault(key) + 1;
    public int Tally(string key) => S.Tally.GetValueOrDefault(key);

    /// The chronicle: the recent log and your older deeds, newest first.
    public IEnumerable<LogEntry> ChronicleEntries => S.Log.Concat(S.Deeds.Where(d => !S.Log.Any(l => l.At == d.At && l.Text == d.Text))).OrderByDescending(e => e.At);
    public bool IsDeed(LogEntry e) => S.Deeds.Any(d => d.At == e.At && d.Text == e.Text);

    /// The company with the longest record: most battles, then honours.
    public CompanyRecord? BestCompany => Player.Companies.OrderByDescending(c => c.Battles).ThenByDescending(c => c.Honours.Count).FirstOrDefault();

    /// The war in a few lines, for the end of the campaign.
    public List<string> Summary()
    {
        var lines = new List<string>
        {
            $"{N(Day, "DAY")} IN THE FIELD. {N(Tally("battles"), "BATTLE")}, {Tally("victories")} WON. {Captain.Renown:0} RENOWN.",
            $"RANK: {RankName(Rank)}. FIEFS: {FiefSeats}. GOLD: {S.Gold:0}.",
            $"{N(Tally("quests"), "CONTRACT")} AND QUESTS DONE, {N(Tally("sites"), "PLACE")} FOUND, {N(Tally("tournaments"), "TOURNAMENT")} WON.",
        };
        if (Tally("holdouts") + Tally("robbed") > 0) lines.Add($"{N(Tally("holdouts"), "GOBLIN HOLE")} BURNT OUT. {N(Tally("robbed"), "CARAVAN")} ROBBED.");
        if (BestCompany is CompanyRecord best) lines.Add($"YOUR BEST: {best.Name.ToUpperInvariant()}, {N(best.Battles, "BATTLE")}" + (best.Honours.Count > 0 ? $", {N(best.Honours.Count, "HONOUR")}." : "."));
        return lines;
    }

    static string N(int n, string word) => $"{n} {word}{(n == 1 ? "" : "S")}";
}
