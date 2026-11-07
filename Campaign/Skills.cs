namespace Ironfield;

/// A captain's perk (CAMPAIGN_OVERHAUL.md 5.1): one choice on a skill's branch.
public sealed class PerkDef
{
    public string Id = "", Name = "", Text = "";
    /// 1-4 (P3): the skill needs Tier x 25 points; 0 = by place on the branch (the first two tier I, then II, III, and the
    /// fifth, the branch's capstone, tier IV, which also needs two of the branch's other perks).
    public int Tier;
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
            new() { Id = "Warlord", Name = "Warlord", Text = "NEAR YOU, NO COMPANY BREAKS WHILE YOU STAND" },
        } },
        new() { Id = "Tactics", Name = "Tactics", Rises = "BATTLES WON OUTNUMBERED", Governs = "AUTO-RESOLVE +1% PER 10 POINTS", Perks =
        {
            new() { Id = "Tactician", Name = "Tactician", Text = "AUTO-RESOLVE +10%, SEE FOES WHEN DEPLOYING" },
            new() { Id = "Ambusher", Name = "Ambusher", Text = "A RETREAT COSTS HALF THE REARGUARD" },
            new() { Id = "Siegecraft", Name = "Siegecraft", Text = "LADDERS AND RAM IN 6 HOURS, NOT 12" },
            new() { Id = "Pursuit", Name = "Pursuit", Text = "+25% PRISONERS AFTER A WIN" },
            new() { Id = "MasterOfWar", Name = "Master of War", Text = "AUTO-RESOLVE +10%, ORDERS REACH YOUR SERGEANTS TWICE AS FAST" },
        } },
        new() { Id = "Logistics", Name = "Logistics", Rises = "DAYS ON THE MARCH", Governs = "PARTY SPEED +1% PER 20 POINTS", Perks =
        {
            new() { Id = "Quartermaster", Name = "Quartermaster", Text = "UPKEEP -10%" },
            new() { Id = "Horsemaster", Name = "Horsemaster", Text = "HORSES -30%, PARTY SPEED +5%" },
            new() { Id = "Forager", Name = "Forager", Text = "THE COMPANY EATS 20% LESS" },
            new() { Id = "Pathfinder", Name = "Pathfinder", Text = "PARTY SPEED +5%" },
            new() { Id = "ForcedMarch", Name = "Forced March", Text = "PARTY SPEED +10%" },
        } },
        new() { Id = "Training", Name = "Training", Rises = "MEN PROMOTED", Governs = "DRILL XP +1% PER 2 POINTS", Perks =
        {
            new() { Id = "Drillmaster", Name = "Drillmaster", Text = "PROMOTE MEN IN CAMP, NOT ONLY IN TOWNS" },
            new() { Id = "VeteranCadre", Name = "Veteran Cadre", Text = "RECRUITS JOIN WITH 20 XP EACH" },
            new() { Id = "CheapSteel", Name = "Cheap Steel", Text = "PROMOTIONS COST 20% LESS" },
            new() { Id = "Sergeants", Name = "Sergeants' Mess", Text = "COMPANIES FIND SERGEANTS TWICE AS OFTEN" },
            new() { Id = "Academy", Name = "Academy", Text = "DRILL XP +50%" },
        } },
        new() { Id = "Medicine", Name = "Medicine", Rises = "WOUNDED HEALED", Governs = "+1% OF FALLEN SURVIVE PER 10 POINTS", Perks =
        {
            new() { Id = "Surgeon", Name = "Surgeon", Text = "+10% OF FALLEN SURVIVE WOUNDED" },
            new() { Id = "FieldHospital", Name = "Field Hospital", Text = "WOUNDED HEAL 50% FASTER OUTSIDE TOWNS" },
            new() { Id = "Bonesetter", Name = "Bonesetter", Text = "YOUR OWN WOUNDS HEAL IN HALF THE TIME" },
            new() { Id = "Triage", Name = "Triage", Text = "COMPANIONS WHO FALL ARE NEVER KILLED" },
            new() { Id = "FieldSurgeon", Name = "Field Surgeon", Text = "ONCE A BATTLE YOU GET BACK UP AT HALF HEALTH" },
        } },
        new() { Id = "Stewardship", Name = "Stewardship", Rises = "FIEF INCOME, DAYS RULING", Governs = "FIEF INCOME +1% PER 5 POINTS", Perks =
        {
            new() { Id = "TaxRoll", Name = "Tax Roll", Text = "FIEF INCOME +15%" },
            new() { Id = "Provisioner", Name = "Provisioner", Text = "FOOD COSTS 25% LESS" },
            new() { Id = "JustRuler", Name = "Just Ruler", Text = "YOUR FIEFS RAISE TWICE THE RECRUITS" },
            new() { Id = "Castellan", Name = "Castellan", Text = "YOUR VILLAGES PAY EVEN WHILE RAIDERS ARE NEAR" },
            new() { Id = "Builder", Name = "Builder", Text = "BUILDINGS COST HALF" },
        } },
        new() { Id = "Charm", Name = "Charm", Rises = "GOODWILL WON, RANSOMS", Governs = "GOODWILL +1% PER 4 POINTS", Perks =
        {
            new() { Id = "SilverTongue", Name = "Silver Tongue", Text = "GOODWILL YOU EARN COUNTS 25% MORE" },
            new() { Id = "RansomBroker", Name = "Ransom Broker", Text = "RANSOMS PAY 50% MORE" },
            new() { Id = "Recruiter", Name = "Recruiter", Text = "+20% OF A SETTLEMENT'S RECRUITS FOLLOW YOU" },
            new() { Id = "Haggler", Name = "Haggler", Text = "MERCENARIES AND HORSES 15% CHEAPER" },
            new() { Id = "Kingmaker", Name = "Kingmaker", Text = "EVERY LORD THINKS 10 BETTER OF YOU" },
        } },
        new() { Id = "Arms", Name = "Arms", Rises = "YOUR KILLS ON FOOT, PARRIES AND RIPOSTES", Governs = "YOUR HEALTH +2% PER 10 POINTS, QUICKER BLOWS, HEAVY GEAR NEEDS ARMS", Perks =
        {
            new() { Id = "WarCry", Name = "War Cry", Text = "RALLY +10 MORALE, 3 S FASTER" },
            new() { Id = "Duellist", Name = "Duellist", Text = "YOUR DAMAGE +15%" },
            new() { Id = "IronGrip", Name = "Iron Grip", Text = "BLOCKING COSTS YOU 30% LESS STAMINA" },
            new() { Id = "Hardened", Name = "Hardened", Text = "YOUR HEALTH +20%" },
            new() { Id = "Champion", Name = "Champion", Text = "YOUR RIPOSTE SLIPS ANY GUARD AND LASTS LONGER" },
        } },
        new() { Id = "Riding", Name = "Riding", Rises = "KILLS AND LANCE HITS FROM THE SADDLE", Governs = "YOUR HORSE +1% HEALTH PER 4 POINTS, TURNS AND STRIKES BETTER, GREAT HORSES NEED RIDING", Perks =
        {
            new() { Id = "MountedLord", Name = "Mounted Lord", Text = "YOUR HORSE HAS 30% MORE HEALTH" },
            new() { Id = "Lancer", Name = "Lancer", Text = "YOUR COUCHED LANCE HITS 25% HARDER" },
            new() { Id = "SaddleFast", Name = "Saddle-Fast", Text = "YOUR HORSE GALLOPS 10% FASTER" },
            new() { Id = "HorseBreaker", Name = "Horse Breaker", Text = "YOUR BLOWS AGAINST RIDERS +25%" },
            new() { Id = "KnightOfTheCharge", Name = "Knight of the Charge", Text = "SPEARS BELOW TIER IV CANNOT STOP YOUR CHARGE" },
        } },
        new() { Id = "Bowcraft", Name = "Bowcraft", Rises = "YOUR OWN ARROW HITS AND KILLS", Governs = "QUICKER DRAW, TIGHTER AIM, THE WAR BOW NEEDS BOWCRAFT", Perks =
        {
            new() { Id = "Fletcher", Name = "Fletcher", Text = "50% MORE ARROWS FOR YOUR BOW" },
            new() { Id = "SteadyAim", Name = "Steady Aim", Text = "YOUR SHOTS SCATTER 30% LESS" },
            new() { Id = "HorseArcher", Name = "Horse Archer", Text = "NO PENALTY SHOOTING FROM THE SADDLE" },
            new() { Id = "Marksman", Name = "Marksman", Text = "YOUR ARROWS HIT 20% HARDER" },
            new() { Id = "Deadeye", Name = "Deadeye", Text = "AT FULL DRAW YOUR SHOTS DO NOT SCATTER" },
        } },
    };

    public static IReadOnlyList<SkillDef> All => Balance.Get().Skills is { Count: > 0 } s ? s : Defaults;
    public static SkillDef Get(string id) => All.First(s => s.Id == id);
    public static SkillDef? Of(string perk) => All.FirstOrDefault(s => s.Perks.Any(p => p.Id == perk));
    public static PerkDef? Perk(string id) => All.SelectMany(s => s.Perks).FirstOrDefault(p => p.Id == id);

    public static float Level(CaptainState c, string skill) => c.Skills.GetValueOrDefault(skill);

    public static int TierOf(SkillDef s, PerkDef p) { int i = s.Perks.IndexOf(p); return p.Tier > 0 ? p.Tier : i < 2 ? 1 : Math.Min(4, i); }
    public static bool Capstone(SkillDef s, PerkDef p) => TierOf(s, p) == 4;

    /// One line per skill that rose a whole point since `before`, for the battle report ("ARMS 42 -> 44"); a crossed
    /// 25-point mark adds "PERK" (P3).
    public static List<string> Rises(CaptainState c, IReadOnlyDictionary<string, float> before) =>
        All.Select(s => (s, a: (int)before.GetValueOrDefault(s.Id), b: (int)Level(c, s.Id))).Where(x => x.b > x.a)
            .Select(x => $"{x.s.Name.ToUpperInvariant()} {x.a} -> {x.b}" + ((int)(x.b / Step) > (int)(x.a / Step) ? " . PERK" : "")).ToList();

    /// Why this perk cannot be taken now (P3): taken, no choice open, the skill short of its tier, a capstone without two
    /// of the branch's other perks; null if it can.
    public static string? Locked(CaptainState c, SkillDef s, PerkDef p)
    {
        if (c.Perks.Contains(p.Id)) return "TAKEN";
        int tier = TierOf(s, p);
        if (Level(c, s.Id) < tier * Step) return $"NEEDS {s.Name.ToUpperInvariant()} {tier * (int)Step}";
        if (tier == 4 && c.Perks.Count(id => id != p.Id && Of(id) == s) < 2) return "NEEDS TWO OTHER PERKS OF THIS SKILL";
        if (Picks(c, s.Id) <= 0) return $"NEEDS {s.Name.ToUpperInvariant()} {(c.Perks.Count(id => Of(id)?.Id == s.Id) + 1) * (int)Step}";
        return null;
    }

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
    public float TacticsEdge => 1 + Skill("Tactics") / 1000f + (Has("Tactician") ? 0.1f : 0) + (Has("MasterOfWar") ? 0.1f : 0) + DivisionEdge;

    /// Starting skills for a new captain (a soldier who has led men before).
    internal static void StartingSkills(CaptainState c)
    {
        foreach (var s in Skills.All) c.Skills[s.Id] = 5;
        c.Skills["Leadership"] = 15;
        c.Skills["Arms"] = 15;
        c.Skills["Tactics"] = 10;
    }

    /// A save from before Riding and Bowcraft (P3): they start at half the captain's Arms (at least 5).
    internal void MigrateSkills()
    {
        foreach (var id in new[] { "Riding", "Bowcraft" })
            if (!Captain.Skills.ContainsKey(id)) Captain.Skills[id] = MathF.Max(5, Skill("Arms") / 2);
    }

    /// Take a perk on a branch with a choice open; returns why not.
    public string? TakePerk(string id)
    {
        var skill = Skills.Of(id);
        if (skill == null) return "NO SUCH PERK";
        if (Skills.Locked(Captain, skill, Skills.Perk(id)!) is string why) return why;
        Captain.Perks.Add(id);
        Log($"You learn {Skills.Perk(id)!.Name}.");
        return null;
    }

    /// Crowns to forget a perk (P3): the first costs `RetrainBase`, each one after that `RetrainBase` more.
    public const int RetrainBase = 150;
    public int RetrainCost => RetrainBase * (1 + Captain.Retrained);

    /// Why this perk cannot be forgotten now: not taken, not in a town (the masters live there), a capstone still
    /// leaning on it, too little gold; null if it can.
    public string? CannotForget(string id)
    {
        var skill = Skills.Of(id);
        if (skill == null || !Captain.Perks.Contains(id)) return "NOT TAKEN";
        if (SettlementAt(Player.Pos) is not Settlement t || t.Kind != SettlementKind.Town || Refuses(t)) return "ONLY IN A FRIENDLY TOWN";
        var capstone = skill.Perks.FirstOrDefault(p => Skills.Capstone(skill, p) && p.Id != id && Captain.Perks.Contains(p.Id));
        if (capstone != null && Captain.Perks.Count(p => p != id && p != capstone.Id && Skills.Of(p) == skill) < 2)
            return $"{capstone.Name.ToUpperInvariant()} NEEDS IT";
        if (S.Gold < RetrainCost) return $"NEEDS {RetrainCost} CROWNS";
        return null;
    }

    /// Forget a perk for gold at a town's masters of arms and letters; the choice opens again on its branch.
    public string? ForgetPerk(string id)
    {
        if (CannotForget(id) is string why) return why;
        S.Gold -= RetrainCost;
        Captain.Retrained++;
        Captain.Perks.Remove(id);
        Log($"You unlearn {Skills.Perk(id)!.Name}. The choice is open again.");
        return null;
    }

    /// A win that lost fewer than this share of your men adds a point of Tactics (P3).
    public const float CheapWin = 0.1f;

    /// The skill rises by use; crossing a 25-point mark opens a perk choice (logged).
    internal void Practise(string skill, float amount)
    {
        if (amount <= 0 || S.GameOver) return;
        amount *= AgeEdge(skill);
        float before = Skill(skill), after = Skills.Raise(Captain, skill, amount);
        if ((int)(after / Skills.Step) > (int)(before / Skills.Step))
            Log($"Your {skill.ToLowerInvariant()} reaches {(int)after}: a perk may be chosen (K).");
    }

    /// After a battle you fought: Leadership for commanding, Tactics for winning against the odds, and your own hands:
    /// Arms for kills on foot, Riding for the saddle and the lance, Bowcraft for your arrows (P3).
    internal void SkillsAfterBattle(BattleResult r, bool won, float own, float foe, int menLed, int team = 0)
    {
        Practise("Leadership", 1.5f + Rules.PerMan(menLed / 40f));
        float odds = foe / MathF.Max(1, own);
        Practise("Tactics", won ? 1 + 4 * Math.Clamp(odds - 0.8f, 0, 2) : 0.4f);
        // A win that cost little is a lesson in itself.
        var mine = r.Companies.Where(x => x.Team == team).ToList();
        int start = mine.Sum(x => x.Start), fell = mine.Sum(x => x.Fallen);
        if (won && start > 0 && fell < start * CheapWin) Practise("Tactics", 1);
        if (r.Duration > 0)
        {
            int onFoot = Math.Max(0, r.PlayerKills - r.SaddleKills - r.ArrowKills);
            Practise("Arms", onFoot * 0.45f + (r.Parries + r.Ripostes) * 0.15f);
            Practise("Riding", r.SaddleKills * 0.4f + r.LanceHits * 0.3f);
            Practise("Bowcraft", r.ArrowKills * 0.35f + r.ArrowHits * 0.15f);
            return;
        }
        // An auto-resolved battle assumes you did your part, with what you carry, from the saddle you lead from.
        int kills = won ? rng.Next(1, 5) : rng.Next(0, 2);
        Practise(Gear.Get(Captain.Kit.Weapon).Ranged ? "Bowcraft" : "Arms", kills * 0.45f);
        Practise("Riding", 0.3f);
    }
}
