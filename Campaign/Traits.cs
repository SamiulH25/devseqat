namespace Ironfield;

/// Campaign: traits the captain earns from what they have done, and growing old (docs/archive/PROGRESSION.md P4).
/// Earned traits sit on the captain's own `Character` beside nothing else; they are worked out again each week from the
/// tally (`Record.cs`), so they can be lost as well as won.
partial class Campaign
{
    public static readonly string[] EarnedTraits = { "Lionheart", "Steadfast", "Merciful", "Ruthless", "Coward" };

    /// How lords take each earned trait: (yours, theirs, opinion); "" is every lord.
    static readonly (string Mine, string Theirs, int D)[] TraitRegard =
    {
        ("Lionheart", "", 5), ("Lionheart", "Reckless", 5), ("Lionheart", "Cautious", -5),
        ("Steadfast", "", 5), ("Steadfast", "Loyal", 5),
        ("Merciful", "Honourable", 10), ("Merciful", "Pious", 10), ("Merciful", "Cruel", -10),
        ("Ruthless", "Cruel", 10), ("Ruthless", "Honourable", -15), ("Ruthless", "Pious", -10),
        ("Coward", "", -10), ("Coward", "Reckless", -10),
    };

    /// What `lord` makes of the captain's earned traits.
    public float RegardOfTraits(Character lord) =>
        TraitRegard.Where(x => Me.Traits.Contains(x.Mine) && (x.Theirs == "" || lord.Traits.Contains(x.Theirs))).Sum(x => x.D);

    /// The traits the tally earns now: Lionheart for duels won or battles fought at the front (5+ of your own kills),
    /// Steadfast for battles won outnumbered, Merciful or Ruthless for mercy or robbery (whichever leads by two),
    /// Coward for fleeing weaker foes (`Retreat`) more than half as often as winning.
    public List<string> TraitsEarned()
    {
        var t = new List<string>();
        if (Tally("duels") >= 2 || Tally("frontline") >= 8) t.Add("Lionheart");
        if (Tally("outnumbered") >= 4) t.Add("Steadfast");
        int mercy = Tally("freed"), cruelty = Tally("robbed");
        if (mercy >= 3 && mercy >= cruelty + 2) t.Add("Merciful");
        if (cruelty >= 3 && cruelty >= mercy + 2) t.Add("Ruthless");
        if (Tally("fled") >= 4 && Tally("fled") * 2 > Tally("victories")) t.Add("Coward");
        return t;
    }

    /// Weekly: earn and lose traits; a new one is a deed, a lost one a line in the log.
    internal void TraitsWeekly()
    {
        if (CharacterById(S.CaptainChar) is not Character me) return;
        var now = TraitsEarned();
        foreach (var t in now.Where(t => !me.Traits.Contains(t)))
        {
            me.Traits.Add(t);
            if (t == "Coward") Log("Men have started to call you a coward.");
            else Deed($"Men have started to call you {t}.");
        }
        foreach (var t in EarnedTraits.Where(t => me.Traits.Contains(t) && !now.Contains(t)).ToList())
        {
            me.Traits.Remove(t);
            Log($"Men no longer call you {t}.");
        }
    }

    /// Renown past which people give the captain a title (P5).
    public const float TitleRenown = 300;

    /// What people call the captain once renown passes `TitleRenown`, from their name (traits) or else their deeds; "" before.
    public string Title
    {
        get
        {
            if (Captain.Renown < TitleRenown || CharacterById(S.CaptainChar) is not Character me) return "";
            foreach (var (trait, title) in new[] { ("Coward", "the Craven"), ("Lionheart", "the Lionheart"), ("Ruthless", "the Cruel"), ("Merciful", "the Merciful"), ("Steadfast", "the Steadfast") })
                if (me.Traits.Contains(trait)) return title;
            return Tally("duels") >= 3 ? "the Champion" : Tally("tournaments") >= 3 ? "of the Lists" : Tally("holdouts") >= 3 ? "Goblin-Bane"
                : Tally("victories") >= 20 ? "the Hammer" : "the Bold";
        }
    }

    /// The age past which the captain's body slips and their head sharpens.
    public const int OldAge = 45;
    public const int DaysPerYear = 120;

    /// Daily: a year on the captain at each new year; past `OldAge`, each new season costs a point of Arms.
    internal void AgeDaily()
    {
        if (Day > 1 && (Day - 1) % DaysPerYear == 0) HousesYearly();
        if (CharacterById(S.CaptainChar) is not Character me || Day <= 1) return;
        if ((Day - 1) % DaysPerYear == 0) me.Age++;
        if (me.Age > OldAge && (Day - 1) % 30 == 0 && Skill("Arms") > 1)
        {
            Captain.Skills["Arms"] = Skill("Arms") - 1;
            Log("Your arm is not what it was.");
        }
    }

    /// Past `OldAge`, Leadership and Tactics rise a quarter faster.
    float AgeEdge(string skill) => skill is "Leadership" or "Tactics" && CharacterById(S.CaptainChar) is { Age: > OldAge } ? 1.25f : 1;
}
