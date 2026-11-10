namespace Ironfield;

/// Campaign: the captain's household guard, the reward of rank (docs/archive/PROGRESSION.md P5). It comes with the first oath of
/// fealty and grows with the captain's best rank: it takes no company slot and no room under the men limit, never
/// deserts, refills itself a little each week, and follows the captain in battle.
partial class Campaign
{
    public const string HouseholdTrait = "Household";
    public const string HouseholdName = "Household Guard";

    /// The guard's men (before the army scale) and its troops' tier for a best rank: none below Vassal; 15 tier III;
    /// a Lord's 25 tier IV; a Marshal's ride as knights; a King's 40 tier V.
    public static (int Men, int Tier, bool Horse) GuardFor(int bestRank) => (Rank)bestRank switch
    {
        < Rank.Vassal => (0, 0, false),
        Rank.Vassal => (15, 3, false),
        Rank.Lord => (25, 4, false),
        Rank.Marshal => (25, 4, true),
        _ => (40, 5, true),
    };

    /// Renown and skill practice for each personal goal met in a battle (P6).
    public const float GoalRenown = 5, GoalPractice = 1.5f;

    /// Metres a Lord's banner adds to the captain's aura.
    public const float LordBanner = 4;

    /// Men the guard refills each week.
    public const int GuardRefill = 3;

    public CompanyRecord? Guard => Player.Companies.FirstOrDefault(c => c.Traits.Contains(HouseholdTrait));

    /// The guard's troop: of your culture at its tier (horse if it rides), else the nearest any culture has.
    public string GuardTroop(int tier, bool horse)
    {
        var all = Troops.All.Where(t => t.Culture != "wild" && t.Tier == tier).OrderBy(t => t.Id).ToList();
        bool Rides(UnitType t) => t.Class == UnitClass.Cavalry;
        bool Foot(UnitType t) => t.Class == UnitClass.Infantry;
        string culture = CharacterById(S.CaptainChar)?.Culture ?? "common";
        var pick = all.FirstOrDefault(t => t.Culture == culture && (horse ? Rides(t) : Foot(t)))
            ?? all.FirstOrDefault(t => horse ? Rides(t) : Foot(t)) ?? all.FirstOrDefault(Foot) ?? all.First();
        return pick.Id;
    }

    /// Weekly: raise the guard when it is due, bring it up to its rank's troops, and refill it.
    internal void GuardWeekly()
    {
        var (men, tier, horse) = GuardFor(Captain.BestRank);
        if (men == 0) return;
        string troop = GuardTroop(tier, horse);
        var g = Guard;
        if (g == null)
        {
            g = NewCompany(troop, Rules.TroopRole(troop), Scaled(men), 150);
            g.Name = HouseholdName;
            g.Traits.Add(HouseholdTrait);
            g.Cohesion = 20;
            Player.Companies.Add(g);
            Log($"Your household guard takes the field: {Scaled(men)} {Troops.Get(troop).Name.ToLowerInvariant()} in your colours.");
            return;
        }
        // Promoted with your rank: every man of the guard takes the new troop.
        if (g.Stacks.All(s => s.Troop != troop) && g.Stacks.Where(s => s.Men + s.Wounded > 0).All(s => s.Type.Tier <= tier))
        {
            int n = g.Stacks.Sum(s => s.Men), w = g.Stacks.Sum(s => s.Wounded);
            g.Stacks.Clear();
            var st = g.Stack(troop);
            (st.Men, st.Wounded) = (n, w);
            g.Role = Rules.TroopRole(troop);
            Log($"Your household guard is now {Troops.Get(troop).Name.ToLowerInvariant()}.");
        }
        int room = Scaled(men) - g.Men - g.Wounded;
        if (room > 0) g.Stack(troop).Men += Math.Min(room, Scaled(GuardRefill));
    }
}
