namespace Ironfield;

/// A personal goal for one battle (docs/PROGRESSION.md P6): what it asks, and the skill it teaches when met.
public sealed record BattleGoal(string Id, string Text, string Skill);

/// Battle: up to two personal goals drawn for a field battle (`BattleOptions.Goals`), tracked live for the HUD and handed
/// back in the result. The draw uses its own seeded generator so the battle's random numbers do not shift.
partial class Battle
{
    public static readonly BattleGoal[] GoalKinds =
    {
        new("captain", "SLAY THEIR CAPTAIN", "Arms"),
        new("hand", "FELL FIVE WITH YOUR OWN HAND", "Arms"),
        new("cavalry", "BREAK THEIR HORSE", "Tactics"),
        new("norout", "NO COMPANY OF YOURS ROUTS", "Leadership"),
        new("quick", "WIN WITHIN THREE MINUTES", "Tactics"),
        new("banner", "KEEP YOUR BANNER FLYING", "Leadership"),
        new("bringhome", "BRING HIM HOME", "Leadership"), // L8: one of your named men lives through it
    };

    /// Seconds within which a quick win must come.
    public const float QuickWin = 180;
    /// Kills the "hand" goal asks of you.
    public const int HandKills = 5;

    public readonly List<BattleGoal> Goals = new();

    public static BattleGoal Goal(string id) => GoalKinds.First(g => g.Id == id);

    /// Two goals that fit the field: their horse only if they have any; none in sieges for the quick win.
    void DrawGoals(int seed)
    {
        if (!Options.Goals || Options.Duel || Options.Tournament) return;
        var fit = GoalKinds.Where(g => g.Id switch
        {
            "cavalry" => Squads.Any(s => s.Team == 1 && s.Cavalry),
            "quick" => !Options.Siege,
            "hand" => !Options.PlayerWounded,
            "captain" => !scripted,
            "banner" => Options.Banner && Bearer != null,
            "bringhome" => anyNamed,
            _ => true,
        }).ToList();
        var r = new Random(seed * 31 + 7);
        while (Goals.Count < 2 && fit.Count > 0)
        {
            var g = fit[r.Next(fit.Count)];
            Goals.Add(g);
            fit.Remove(g);
            if (g.Id == "bringhome") { var named = Units.Where(u => u.SoldierId != 0 && u.Team == 0).ToList(); Ward = named[r.Next(named.Count)]; }
        }
    }

    /// 1 met, -1 lost, 0 still open (final once the battle has a winner).
    public int GoalState(BattleGoal g)
    {
        bool over = Winner >= 0;
        switch (g.Id)
        {
            case "captain": return EnemyCaptain.Dead ? 1 : over ? -1 : 0;
            case "hand": return PlayerKills >= HandKills ? 1 : over ? -1 : 0;
            case "cavalry":
                bool broken = Squads.Where(s => s.Team == 1 && s.Cavalry).All(s => s.Units.All(u => !u.Active || u.Routing));
                return broken ? 1 : over ? -1 : 0;
            case "norout":
                if (Squads.Any(s => s.Team == 0 && s.Company != null && s.RoutSounded)) return -1;
                return over ? (Winner == 0 ? 1 : -1) : 0;
            case "banner": return BannerEverFell ? -1 : over ? 1 : 0;
            case "bringhome": return Ward == null ? -1 : Ward.Dead && !Ward.Saved && Time - Ward.DiedAt >= RescueWindow ? -1 : over ? (Ward.Dead && !Ward.Saved ? -1 : 1) : 0;
            case "quick":
                if (over) return Winner == 0 && Time <= QuickWin ? 1 : -1;
                return Time > QuickWin ? -1 : 0;
        }
        return 0;
    }

    /// The ids of the goals met, for the result.
    IReadOnlyList<string> GoalsMet() => Goals.Where(g => GoalState(g) == 1).Select(g => g.Id).ToList();
}
