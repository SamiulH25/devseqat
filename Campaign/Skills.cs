namespace Ironfield;

/// A captain's perk (CAMPAIGN_OVERHAUL.md 5.1): one choice on a skill's branch.
public sealed class PerkDef
{
    public string Id = "", Name = "", Text = "";
}

/// A captain's skill: how it rises, what it governs, and the perks on its branch (one more may be taken per 25 points).
public sealed class SkillDef
{
    public string Id = "", Name = "", Rises = "", Governs = "";
    public List<PerkDef> Perks = new();
}

/// The skill tree (step F4), replacing the old renown-bought perks. Data: Data/campaign.json `Skills` (defaults built in).
public static class Skills
{
    public const float Max = 100, Step = 25;

    static readonly SkillDef[] Defaults =
    {
        new() { Id = "Leadership", Name = "Leadership", Rises = "BATTLES COMMANDED, MEN LED", Governs = "MEN LIMIT +1 PER 5 POINTS", Perks =
        {
            new() { Id = "Bannerman", Name = "Bannerman", Text = "AURA RADIUS +4 M, ORDERS REACH SERGEANTS 30% SOONER" },
            new() { Id = "IronWill", Name = "Iron Will", Text = "COMPANIES NEAR YOU HOLD UNTIL 30% STRENGTH" },
            new() { Id = "BornCaptain", Name = "Born Captain", Text = "+10 MEN LIMIT" },
            new() { Id = "Rallying", Name = "Rallying Cry", Text = "EVERY COMPANY STARTS A BATTLE WITH +5 MORALE" },
        } },
        new() { Id = "Tactics", Name = "Tactics", Rises = "BATTLES WON OUTNUMBERED", Governs = "AUTO-RESOLVE +1% PER 10 POINTS", Perks =
        {
            new() { Id = "Tactician", Name = "Tactician", Text = "AUTO-RESOLVE +10%, SEE FOES WHEN DEPLOYING" },
            new() { Id = "Ambusher", Name = "Ambusher", Text = "A RETREAT COSTS HALF THE REARGUARD" },
            new() { Id = "Siegecraft", Name = "Siegecraft", Text = "LADDERS AND RAM IN 6 HOURS, NOT 12" },
            new() { Id = "Pursuit", Name = "Pursuit", Text = "+25% PRISONERS AFTER A WIN" },
        } },
        new() { Id = "Logistics", Name = "Logistics", Rises = "DAYS ON THE MARCH", Governs = "PARTY SPEED +1% PER 20 POINTS", Perks =
        {
            new() { Id = "Quartermaster", Name = "Quartermaster", Text = "UPKEEP -10%" },
            new() { Id = "Horsemaster", Name = "Horsemaster", Text = "HORSES -30%, PARTY SPEED +5%" },
            new() { Id = "Forager", Name = "Forager", Text = "THE COMPANY EATS 20% LESS" },
            new() { Id = "Pathfinder", Name = "Pathfinder", Text = "PARTY SPEED +5%" },
        } },
        new() { Id = "Training", Name = "Training", Rises = "MEN PROMOTED", Governs = "DRILL XP +1% PER 2 POINTS", Perks =
        {
            new() { Id = "Drillmaster", Name = "Drillmaster", Text = "PROMOTE MEN IN CAMP, NOT ONLY IN TOWNS" },
            new() { Id = "VeteranCadre", Name = "Veteran Cadre", Text = "RECRUITS JOIN WITH 20 XP EACH" },
            new() { Id = "CheapSteel", Name = "Cheap Steel", Text = "PROMOTIONS COST 20% LESS" },
            new() { Id = "Sergeants", Name = "Sergeants' Mess", Text = "COMPANIES FIND SERGEANTS TWICE AS OFTEN" },
        } },
        new() { Id = "Medicine", Name = "Medicine", Rises = "WOUNDED HEALED", Governs = "+1% OF FALLEN SURVIVE PER 10 POINTS", Perks =
        {
            new() { Id = "Surgeon", Name = "Surgeon", Text = "+10% OF FALLEN SURVIVE WOUNDED" },
            new() { Id = "FieldHospital", Name = "Field Hospital", Text = "WOUNDED HEAL 50% FASTER OUTSIDE TOWNS" },
            new() { Id = "Bonesetter", Name = "Bonesetter", Text = "YOUR OWN WOUNDS HEAL IN HALF THE TIME" },
            new() { Id = "Triage", Name = "Triage", Text = "COMPANIONS WHO FALL ARE NEVER KILLED" },
        } },
        new() { Id = "Stewardship", Name = "Stewardship", Rises = "FIEF INCOME, DAYS RULING", Governs = "FIEF INCOME +1% PER 5 POINTS", Perks =
        {
            new() { Id = "TaxRoll", Name = "Tax Roll", Text = "FIEF INCOME +15%" },
            new() { Id = "Provisioner", Name = "Provisioner", Text = "FOOD COSTS 25% LESS" },
            new() { Id = "JustRuler", Name = "Just Ruler", Text = "YOUR FIEFS RAISE TWICE THE RECRUITS" },
            new() { Id = "Castellan", Name = "Castellan", Text = "YOUR VILLAGES PAY EVEN WHILE RAIDERS ARE NEAR" },
        } },
        new() { Id = "Charm", Name = "Charm", Rises = "GOODWILL WON, RANSOMS", Governs = "GOODWILL +1% PER 4 POINTS", Perks =
        {
            new() { Id = "SilverTongue", Name = "Silver Tongue", Text = "GOODWILL YOU EARN COUNTS 25% MORE" },
            new() { Id = "RansomBroker", Name = "Ransom Broker", Text = "RANSOMS PAY 50% MORE" },
            new() { Id = "Recruiter", Name = "Recruiter", Text = "+20% OF A SETTLEMENT'S RECRUITS FOLLOW YOU" },
            new() { Id = "Haggler", Name = "Haggler", Text = "MERCENARIES AND HORSES 15% CHEAPER" },
        } },
        new() { Id = "Arms", Name = "Arms", Rises = "YOUR OWN KILLS IN BATTLE", Governs = "YOUR HEALTH +2% PER 10 POINTS, HEAVY GEAR NEEDS ARMS", Perks =
        {
            new() { Id = "WarCry", Name = "War Cry", Text = "RALLY +10 MORALE, 3 S FASTER" },
            new() { Id = "Duellist", Name = "Duellist", Text = "YOUR DAMAGE +15%" },
            new() { Id = "MountedLord", Name = "Mounted Lord", Text = "YOUR HORSE HAS 30% MORE HEALTH" },
            new() { Id = "Hardened", Name = "Hardened", Text = "YOUR HEALTH +20%" },
        } },
    };

    public static IReadOnlyList<SkillDef> All => Balance.Get().Skills is { Count: > 0 } s ? s : Defaults;
    public static SkillDef Get(string id) => All.First(s => s.Id == id);
    public static SkillDef? Of(string perk) => All.FirstOrDefault(s => s.Perks.Any(p => p.Id == perk));
    public static PerkDef? Perk(string id) => All.SelectMany(s => s.Perks).FirstOrDefault(p => p.Id == id);

    public static float Level(CaptainState c, string skill) => c.Skills.GetValueOrDefault(skill);

    /// Perk choices open on a skill's branch: one per 25 points, less those taken.
    public static int Picks(CaptainState c, string skill) =>
        (int)(Level(c, skill) / Step) - c.Perks.Count(p => Of(p)?.Id == skill);

    public static int PicksTotal(CaptainState c) => All.Sum(s => Math.Max(0, Picks(c, s.Id)));

    /// Raise a skill by use, with diminishing returns as it climbs; returns the new level.
    public static float Raise(CaptainState c, string skill, float amount)
    {
        if (amount <= 0) return Level(c, skill);
        float now = Level(c, skill);
        float gain = amount * MathF.Max(0.25f, 1 - now / 125f);
        return c.Skills[skill] = MathF.Min(Max, now + gain);
    }
}

public sealed partial class Campaign
{
    /// Does the captain have this perk (step F4)?
    public bool Has(string perk) => Captain.Perks.Contains(perk);
    public float Skill(string skill) => Skills.Level(Captain, skill);

    /// Your edge in an auto-resolved fight: Tactics skill (+1% per 10 points) and the Tactician perk (+10%).
    public float TacticsEdge => 1 + Skill("Tactics") / 1000f + (Has("Tactician") ? 0.1f : 0) + DivisionEdge;

    /// Starting skills for a new captain (a soldier who has led men before).
    internal static void StartingSkills(CaptainState c)
    {
        foreach (var s in Skills.All) c.Skills[s.Id] = 5;
        c.Skills["Leadership"] = 15;
        c.Skills["Arms"] = 15;
        c.Skills["Tactics"] = 10;
    }

    /// Take a perk on a branch with a choice open; returns why not.
    public string? TakePerk(string id)
    {
        var skill = Skills.Of(id);
        if (skill == null) return "NO SUCH PERK";
        if (Has(id)) return "TAKEN";
        if (Skills.Picks(Captain, skill.Id) <= 0)
            return $"NEEDS {skill.Name.ToUpperInvariant()} {(int)((Captain.Perks.Count(p => Skills.Of(p)?.Id == skill.Id) + 1) * Skills.Step)}";
        Captain.Perks.Add(id);
        Log($"You learn {Skills.Perk(id)!.Name}.");
        return null;
    }

    /// The skill rises by use; crossing a 25-point mark opens a perk choice (logged).
    internal void Practise(string skill, float amount)
    {
        if (amount <= 0 || S.GameOver) return;
        float before = Skill(skill), after = Skills.Raise(Captain, skill, amount);
        if ((int)(after / Skills.Step) > (int)(before / Skills.Step))
            Log($"Your {skill.ToLowerInvariant()} reaches {(int)after}: a perk may be chosen (K).");
    }

    /// After a battle you fought: Leadership for commanding, Tactics for winning against the odds, Arms for your own kills.
    internal void SkillsAfterBattle(BattleResult r, bool won, float own, float foe, int menLed)
    {
        Practise("Leadership", 1.5f + Rules.PerMan(menLed / 40f));
        float odds = foe / MathF.Max(1, own);
        Practise("Tactics", won ? 1 + 4 * Math.Clamp(odds - 0.8f, 0, 2) : 0.4f);
        // A played battle counts your own kills; an auto-resolved one assumes you did your part.
        int kills = r.Duration > 0 ? r.PlayerKills : won ? rng.Next(1, 5) : rng.Next(0, 2);
        Practise("Arms", kills * 0.45f);
    }
}
