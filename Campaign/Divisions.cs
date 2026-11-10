namespace Ironfield;

/// Your divisions (docs/archive/COMBAT_OVERHAUL.md 1.5): companies grouped under a companion or a company's sergeant, who
/// leads them in battle by goals. Companies in no division are your retinue.
public partial class Campaign
{
    public const int MaxDivisions = 6;
    public static readonly string[] DivisionGoals = { "Hold", "Attack", "FlankLeft", "FlankRight", "Reserve", "Screen", "Harass", "Guard" };

    public IEnumerable<DivisionRecord> Divisions => S.Divisions;

    /// Who could lead a division: fit companions, then sergeants of your companies, not already leading one.
    public IEnumerable<(string Name, int Char, int Company)> DivisionLeaders() =>
        Companions.Where(k => S.Hours >= k.WoundedUntil && S.Divisions.All(d => d.LeaderChar != k.Id)).Select(k => (k.Name, k.Id, -1))
            .Concat(Player.Companies.Where(c => c.Officer != "" && S.Divisions.All(d => d.LeaderCompany != c.Id)).Select(c => (c.Officer, -1, c.Id)));

    public string DivisionName(DivisionRecord d) =>
        d.LeaderChar >= 0 ? S.Characters.FirstOrDefault(c => c.Id == d.LeaderChar)?.Name ?? "?"
        : Player.Companies.FirstOrDefault(c => c.Id == d.LeaderCompany)?.Officer ?? "?";

    public IEnumerable<CompanyRecord> CompaniesOf(DivisionRecord d) => Player.Companies.Where(c => c.Division == d.Id);

    /// The company the leader fights in: the companion's, or the sergeant's own.
    public CompanyRecord? LeaderCompanyOf(DivisionRecord d) =>
        Player.Companies.FirstOrDefault(c => c.Id == (d.LeaderChar >= 0 ? S.Characters.FirstOrDefault(k => k.Id == d.LeaderChar)?.CompanyId ?? -1 : d.LeaderCompany));

    /// Make a new division under this leader; his own company joins it.
    public string? MakeDivision(int leaderChar, int leaderCompany)
    {
        if (S.Divisions.Count >= MaxDivisions) return $"AT MOST {MaxDivisions} DIVISIONS";
        if (!DivisionLeaders().Any(l => l.Char == leaderChar && l.Company == leaderCompany)) return "HE CANNOT LEAD A DIVISION NOW";
        var d = new DivisionRecord { Id = S.Divisions.Count == 0 ? 0 : S.Divisions.Max(x => x.Id) + 1, LeaderChar = leaderChar, LeaderCompany = leaderCompany };
        S.Divisions.Add(d);
        if (LeaderCompanyOf(d) is CompanyRecord own) own.Division = d.Id;
        Log($"{DivisionName(d)} takes command of a division.");
        return null;
    }

    /// Disband a division: its companies come back to your retinue.
    public void DisbandDivision(DivisionRecord d)
    {
        foreach (var c in CompaniesOf(d)) c.Division = -1;
        S.Divisions.Remove(d);
    }

    /// Put a company in a division (-1: your retinue). The leader's own company stays with him.
    public string? AssignDivision(CompanyRecord c, int division)
    {
        if (S.Divisions.FirstOrDefault(d => d.Id == c.Division) is DivisionRecord from && LeaderCompanyOf(from) == c && division != from.Id)
            return $"{DivisionName(from).ToUpperInvariant()} FIGHTS WITH THIS COMPANY: DISBAND HIS DIVISION FIRST";
        if (division >= 0 && S.Divisions.All(d => d.Id != division)) return "NO SUCH DIVISION";
        c.Division = division;
        return null;
    }

    public void SetDivisionGoal(DivisionRecord d, string goal) { if (DivisionGoals.Contains(goal)) d.Goal = goal; }

    /// How a division's leader fights: a companion's temper (or one from his traits), a sergeant's from his.
    public string TemperOf(DivisionRecord d)
    {
        if (S.Characters.FirstOrDefault(k => k.Id == d.LeaderChar) is Character ch)
            return ch.Temper != "" ? ch.Temper : ch.Traits.Contains("Reckless") || ch.Traits.Contains("Ambitious") ? "Bold" : ch.Traits.Contains("Cautious") ? "Cautious" : "Steady";
        var c = Player.Companies.FirstOrDefault(x => x.Id == d.LeaderCompany);
        return c?.Traits.Contains("Hothead") == true ? "Bold" : c?.Traits.Contains("Stubborn") == true || c?.Traits.Contains("Steady") == true ? "Steady" : "Cautious";
    }

    /// His skill at it: a companion's Command; a sergeant's grows with the battles he has seen (at most 6).
    public int CommandOf(DivisionRecord d) =>
        S.Characters.FirstOrDefault(k => k.Id == d.LeaderChar) is Character ch ? ch.Command
        : Math.Min(6, 1 + (Player.Companies.FirstOrDefault(x => x.Id == d.LeaderCompany)?.OfficerBattles ?? 0) / 3);

    /// Drop divisions whose leader is gone (fallen, wounded, left with his company); their companies return to you.
    public void PruneDivisions()
    {
        foreach (var d in S.Divisions.ToList())
        {
            bool ok = d.LeaderChar >= 0
                ? Companions.Any(k => k.Id == d.LeaderChar && S.Hours >= k.WoundedUntil)
                : Player.Companies.Any(c => c.Id == d.LeaderCompany && c.Officer != "");
            if (ok) continue;
            Log($"{DivisionName(d)}'s division is broken up: its companies return to your command.");
            DisbandDivision(d);
        }
        foreach (var c in Player.Companies) if (c.Division >= 0 && S.Divisions.All(d => d.Id != c.Division)) c.Division = -1;
    }

    /// Your army for battle with each company's division and the division specs.
    ArmySpec WithDivisions(ArmySpec army)
    {
        PruneDivisions();
        if (S.Divisions.Count == 0) return army;
        var mine = Player.Companies.ToDictionary(c => c.Id, c => c.Division);
        var specs = S.Divisions.Where(d => CompaniesOf(d).Any(c => c.Men > 0)).Select(d =>
        {
            var lead = LeaderCompanyOf(d);
            string hero = d.LeaderChar >= 0 && lead?.Hero == DivisionName(d) && lead.Division == d.Id ? lead.Hero : "";
            return new DivisionSpec(d.Id, DivisionName(d), TemperOf(d), CommandOf(d), d.Goal, hero, lead?.Division == d.Id ? lead.Id : 0);
        }).ToList();
        return army with
        {
            Companies = army.Companies.Select(c => mine.TryGetValue(c.Id, out var div) && !c.Allied ? c with { Division = div } : c).ToList(),
            Divisions = specs,
        };
    }

    /// Auto-resolve edge from your divisions: each leader past command 3 adds 1%, at most 8% in all.
    public float DivisionEdge => MathF.Min(0.08f, S.Divisions.Where(d => CompaniesOf(d).Any(c => c.Men > 0)).Sum(d => Math.Max(0, CommandOf(d) - 3)) * 0.01f);

    /// After a battle: every companion who led a division that took the field gets better at it.
    internal void DivisionsAfterBattle(BattleResult r)
    {
        foreach (var d in S.Divisions)
        {
            if (S.Characters.FirstOrDefault(k => k.Id == d.LeaderChar) is not Character ch) continue;
            if (!r.Companies.Any(c => c.Team == 0 && c.Deployed && CompaniesOf(d).Any(x => x.Id == c.Id))) continue;
            if (++ch.CommandBattles % 2 == 0 && ch.Command < 10) { ch.Command++; Log($"{ch.Name} leads a division better now (command {ch.Command})."); }
        }
    }
}
