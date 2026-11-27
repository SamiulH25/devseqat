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
        new() { Id = "Leadership", Name = "Leadership", Rises = "BATTLES COMMANDED, MEN LED", Governs = "MEN LIMIT +1 PER 3 PTS, WIDER BANNER AURA, STRONGER RALLY", Perks =
        {
            new() { Id = "Bannerman", Name = "Bannerman", Text = "AURA RADIUS +4 M" },
            new() { Id = "IronWill", Name = "Iron Will", Text = "COMPANIES NEAR YOU HOLD UNTIL 30% STRENGTH" },
            new() { Id = "BornCaptain", Name = "Born Captain", Text = "+10 MEN LIMIT" },
            new() { Id = "Rallying", Name = "Rallying Cry", Text = "EVERY COMPANY STARTS A BATTLE WITH +5 MORALE" },
            new() { Id = "Warlord", Name = "Warlord", Text = "NEAR YOU, NO COMPANY BREAKS WHILE YOU STAND" },
        } },
        new() { Id = "Tactics", Name = "Tactics", Rises = "BATTLES WON, CLEAN VICTORIES", Governs = "SCOUT VISION +0.3%/PT, FLANK DAMAGE +0.2%/PT, AUTO-RESOLVE +0.2%/PT", Perks =
        {
            new() { Id = "Tactician", Name = "Tactician", Text = "AUTO-RESOLVE +10%, SEE FOES DEPLOYING, SEE 25% FURTHER" },
            new() { Id = "Ambusher", Name = "Ambusher", Text = "RETREATS COST HALF THE REARGUARD, AMBUSHES SHAKE MORE" },
            new() { Id = "Siegecraft", Name = "Siegecraft", Text = "LADDERS AND RAM IN 6 HOURS, NOT 12" },
            new() { Id = "Pursuit", Name = "Pursuit", Text = "+25% PRISONERS AFTER A WIN" },
            new() { Id = "MasterOfWar", Name = "Master of War", Text = "AUTO-RESOLVE +15%, YOUR COMPANIES DEAL +12% FLANK DAMAGE" },
        } },
        new() { Id = "Logistics", Name = "Logistics", Rises = "MARCHING AND KEEPING SUPPLY", Governs = "MAP SPEED +0.2%/PT, BAGGAGE +0.5%/PT, LESS FOOD EATEN", Perks =
        {
            new() { Id = "Quartermaster", Name = "Quartermaster", Text = "UPKEEP -10%, BAGGAGE +20%" },
            new() { Id = "Horsemaster", Name = "Horsemaster", Text = "HORSES -30%, PARTY SPEED +5%" },
            new() { Id = "Forager", Name = "Forager", Text = "THE COMPANY EATS 20% LESS" },
            new() { Id = "Pathfinder", Name = "Pathfinder", Text = "PARTY SPEED +5%, WOODS AND MARSH SLOW YOUR MEN LESS" },
            new() { Id = "ForcedMarch", Name = "Forced March", Text = "PARTY SPEED +15%" },
        } },
        new() { Id = "Training", Name = "Training", Rises = "DAILY DRILL AND PROMOTIONS", Governs = "DRILL XP +1%/PT, PROMOTION COST -0.2%/PT", Perks =
        {
            new() { Id = "Drillmaster", Name = "Drillmaster", Text = "PROMOTE MEN OF ANY TIER IN CAMP" },
            new() { Id = "VeteranCadre", Name = "Veteran Cadre", Text = "RECRUITS JOIN WITH 20 XP EACH" },
            new() { Id = "CheapSteel", Name = "Cheap Steel", Text = "PROMOTIONS COST 20% LESS" },
            new() { Id = "Sergeants", Name = "Sergeants' Mess", Text = "COMPANIES FIND SERGEANTS TWICE AS OFTEN" },
            new() { Id = "Academy", Name = "Academy", Text = "DRILL XP +75%, NEW RECRUITS START WITH +40 XP" },
        } },
        new() { Id = "Medicine", Name = "Medicine", Rises = "BATTLES SURVIVED AND WOUNDED HEALED", Governs = "SURVIVAL +0.2%/PT, HEAL SPEED +0.5%/PT", Perks =
        {
            new() { Id = "Surgeon", Name = "Surgeon", Text = "+10% OF FALLEN SURVIVE WOUNDED" },
            new() { Id = "FieldHospital", Name = "Field Hospital", Text = "WOUNDED HEAL 50% FASTER OUTSIDE TOWNS" },
            new() { Id = "Bonesetter", Name = "Bonesetter", Text = "YOUR OWN WOUNDS HEAL IN HALF THE TIME" },
            new() { Id = "Triage", Name = "Triage", Text = "COMPANIONS WHO FALL ARE NEVER KILLED" },
            new() { Id = "FieldSurgeon", Name = "Field Surgeon", Text = "ONCE A BATTLE YOU GET BACK UP AT HALF HEALTH" },
        } },
        new() { Id = "Stewardship", Name = "Stewardship", Rises = "PAYING WAGES, FIEF INCOME, DAYS RULING", Governs = "UPKEEP -0.25%/PT, FIEF INCOME +0.3%/PT, CHEAPER BUILDINGS", Perks =
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
            new() { Id = "Kingmaker", Name = "Kingmaker", Text = "EVERY LORD +15 OPINION, MERCENARIES AND HORSES 20% CHEAPER" },
        } },
        new() { Id = "Arms", Name = "Arms", Rises = "MELEE KILLS (FOOT OR SADDLE), PARRIES AND RIPOSTES", Governs = "HP +1.5%/PT, MELEE DMG +0.85%/PT, FASTER SWINGS, UNLOCKS HEAVY GEAR", Perks =
        {
            new() { Id = "WarCry", Name = "War Cry", Text = "RALLY +10 MORALE, 3 S FASTER" },
            new() { Id = "Duellist", Name = "Duellist", Text = "YOUR DAMAGE +15%" },
            new() { Id = "IronGrip", Name = "Iron Grip", Text = "BLOCKING COSTS YOU 30% LESS STAMINA" },
            new() { Id = "Hardened", Name = "Hardened", Text = "YOUR HEALTH +20%" },
            new() { Id = "Champion", Name = "Champion", Text = "YOUR RIPOSTE SLIPS ANY GUARD AND LASTS LONGER" },
        } },
        new() { Id = "Riding", Name = "Riding", Rises = "RIDING ON THE MARCH, MOUNTED KILLS AND LANCE HITS", Governs = "HORSE HP +0.6%/PT, FASTER GALLOP AND TURNS, UNLOCKS WARHORSES", Perks =
        {
            new() { Id = "MountedLord", Name = "Mounted Lord", Text = "YOUR HORSE HAS 30% MORE HEALTH" },
            new() { Id = "Lancer", Name = "Lancer", Text = "YOUR COUCHED LANCE HITS 25% HARDER" },
            new() { Id = "SaddleFast", Name = "Saddle-Fast", Text = "YOUR HORSE GALLOPS 10% FASTER" },
            new() { Id = "HorseBreaker", Name = "Horse Breaker", Text = "YOUR BLOWS AGAINST RIDERS +25%" },
            new() { Id = "KnightOfTheCharge", Name = "Knight of the Charge", Text = "SPEARS BELOW TIER IV CANNOT STOP YOUR CHARGE" },
        } },
        new() { Id = "Bowcraft", Name = "Bowcraft", Rises = "YOUR ARROW HITS AND KILLS", Governs = "BOW DMG +0.9%/PT, FASTER DRAW, TIGHTER AIM, UNLOCKS WAR BOWS", Perks =
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
        if (Picks(c, s.Id) <= 0 && c.FreePerks <= 0) return $"NEEDS {s.Name.ToUpperInvariant()} {(c.Perks.Count(id => Of(id)?.Id == s.Id) + 1) * (int)Step}";
        return null;
    }

    /// Perk choices open on a skill's branch: one per 25 points, less those taken.
    public static int Picks(CaptainState c, string skill) =>
        (int)(Level(c, skill) / Step) - c.Perks.Count(p => Of(p)?.Id == skill && !c.BonusPerks.Contains(p));

    public static int PicksTotal(CaptainState c) => All.Sum(s => Math.Max(0, Picks(c, s.Id))) + c.FreePerks;

    /// Raise a skill by use, with diminishing returns as it climbs; returns the new level.
    public const int MaxFocus = 3;
    public static int FocusOf(CaptainState c, string skill) => Math.Clamp(c.Focus.GetValueOrDefault(skill), 0, MaxFocus);
    public static float FocusMult(CaptainState c, string skill) => 1f + 0.5f * FocusOf(c, skill);
    public static float XpForLevel(int level) => 100f + (level - 1) * 75f + (level - 1) * (level - 1) * 15f;

    /// Live numeric readout of what this skill currently grants the captain (shown on the K sheet).
    public static string CurrentBonus(CaptainState c, string id)
    {
        float v = Level(c, id);
        return id switch
        {
            "Leadership" => $"NOW: +{(int)(v / 3)} MEN CAP, +{v * 0.08f:0.0}M AURA, +{v * 0.25f:0} RALLY",
            "Tactics" => $"NOW: +{v * 0.3f:0}% VISION, +{v * 0.2f:0}% FLANK DMG, +{v * 0.2f:0}% AUTO-RES",
            "Logistics" => $"NOW: +{v * 0.2f:0}% SPEED, +{v * 0.5f:0}% BAGGAGE, -{v * 0.25f:0}% FOOD",
            "Training" => $"NOW: +{v:0}% DRILL XP, -{v * 0.2f:0}% PROMOTE COST",
            "Medicine" => $"NOW: +{v * 0.2f:0}% SURVIVAL, +{v * 0.5f:0}% HEAL RATE",
            "Stewardship" => $"NOW: -{v * 0.25f:0}% UPKEEP, +{v * 0.3f:0}% FIEF INCOME",
            "Charm" => $"NOW: +{v * 0.4f:0}% GOODWILL, -{v * 0.15f:0}% PRICES",
            "Arms" => $"NOW: +{v * 1.5f:0}% HP, +{v * 0.85f:0}% MELEE DMG, -{v * 0.35f:0}% SWING",
            "Riding" => $"NOW: +{v * 0.6f:0}% HORSE HP, +{v * 0.15f:0}% GALLOP",
            "Bowcraft" => $"NOW: +{v * 0.9f:0}% BOW DMG, -{v * 0.4f:0}% DRAW TIME",
            _ => "",
        };
    }

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

    /// Your edge in an auto-resolved fight: Tactics skill (+0.2% per point) and Tactics perks.
    public float TacticsEdge => 1 + Skill("Tactics") / 500f + (Has("Tactician") ? 0.1f : 0) + (Has("MasterOfWar") ? 0.15f : 0) + DivisionEdge;

    /// Starting skills for a new Day-1 captain: green and unproven, built up through play and Skill Points.
    internal static void StartingSkills(CaptainState c)
    {
        foreach (var s in Skills.All) c.Skills[s.Id] = 2;
        c.Skills["Leadership"] = 5;
        c.Skills["Arms"] = 5;
        c.Skills["Tactics"] = 3;
    }

    /// Award Captain XP; each level-up grants +3 Skill Points and +1 Focus Point every 2 levels.
    internal void GainXp(float amount)
    {
        if (amount <= 0 || S.GameOver) return;
        Captain.Xp += amount;
        while (Captain.Xp >= Skills.XpForLevel(Captain.Level))
        {
            Captain.Xp -= Skills.XpForLevel(Captain.Level);
            Captain.Level++;
            Captain.SkillPoints += 3;
            bool gotFocus = Captain.Level % 2 == 0;
            if (gotFocus) Captain.FocusPoints++;
            LogYours($"You reach level {Captain.Level}: +3 skill points{(gotFocus ? ", +1 focus" : "")} (K).");
        }
    }

    /// Spend 1 Skill Point to directly raise a skill by +5 (up to 100), opening perks on 25-point marks.
    public string? InvestSkill(string skill)
    {
        if (Captain.SkillPoints <= 0) return "NO SKILL POINTS";
        float before = Skill(skill);
        if (before >= Skills.Max) return "ALREADY MASTERED";
        Captain.SkillPoints--;
        float after = Captain.Skills[skill] = MathF.Min(Skills.Max, before + 5f);
        if ((int)(after / Skills.Step) > (int)(before / Skills.Step))
            LogYours($"Your {skill.ToLowerInvariant()} reaches {(int)after}: a perk may be chosen (K).");
        return null;
    }

    /// Spend 1 Focus Point to boost a skill's organic training speed (+50% per pip, max 3).
    public string? InvestFocus(string skill)
    {
        if (Captain.FocusPoints <= 0) return "NO FOCUS POINTS";
        int cur = Skills.FocusOf(Captain, skill);
        if (cur >= Skills.MaxFocus) return "MAX FOCUS";
        Captain.FocusPoints--;
        Captain.Focus[skill] = cur + 1;
        LogYours($"You focus on {skill.ToLowerInvariant()} (x{Skills.FocusMult(Captain, skill):0.#} learning).");
        return null;
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
        if (Skills.Picks(Captain, skill.Id) <= 0) { Captain.FreePerks--; Captain.BonusPerks.Add(id); } // a rank's free choice (P5)
        Captain.Perks.Add(id);
        LogYours($"You learn {Skills.Perk(id)!.Name}.");
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
        if (Captain.BonusPerks.Remove(id)) Captain.FreePerks++; // a free choice comes back free
        LogYours($"You unlearn {Skills.Perk(id)!.Name}. The choice is open again.");
        return null;
    }

    /// A win that lost fewer than this share of your men adds a point of Tactics (P3).
    public const float CheapWin = 0.1f;

    /// The skill rises by use; crossing a 25-point mark opens a perk choice (logged).
    internal void Practise(string skill, float amount)
    {
        if (amount <= 0 || S.GameOver) return;
        amount *= AgeEdge(skill) * Skills.FocusMult(Captain, skill);
        float before = Skill(skill), after = Skills.Raise(Captain, skill, amount);
        float gained = after - before;
        if (gained > 0) GainXp(gained * 16f);
        if ((int)(after / Skills.Step) > (int)(before / Skills.Step))
            LogYours($"Your {skill.ToLowerInvariant()} reaches {(int)after}: a perk may be chosen (K).");
    }

    /// After a battle you fought: Leadership for commanding, Tactics for winning, Medicine for tending survivors,
    /// Arms for all melee kills (foot or saddle) + parries/ripostes, Riding for mounted work, Bowcraft for arrows, plus Captain XP.
    internal void SkillsAfterBattle(BattleResult r, bool won, float own, float foe, int menLed, int team = 0)
    {
        Practise("Leadership", 1.8f + Rules.PerMan(menLed / 35f));
        float odds = foe / MathF.Max(1, own);
        Practise("Tactics", won ? 1.5f + 4 * Math.Clamp(odds - 0.7f, 0, 2) : 0.6f);
        Practise("Medicine", 0.5f);
        var mine = r.Companies.Where(x => x.Team == team).ToList();
        int start = mine.Sum(x => x.Start), fell = mine.Sum(x => x.Fallen);
        if (won && start > 0 && fell < start * CheapWin) Practise("Tactics", 1.2f);
        GainXp((won ? 35f : 15f) + r.PlayerKills * 8f + (r.Parries + r.Ripostes) * 3f);
        if (r.Duration > 0)
        {
            int meleeKills = Math.Max(0, r.PlayerKills - r.ArrowKills);
            Practise("Arms", meleeKills * 0.55f + (r.Parries + r.Ripostes) * 0.22f);
            Practise("Riding", r.SaddleKills * 0.55f + r.LanceHits * 0.4f);
            Practise("Bowcraft", r.ArrowKills * 0.5f + r.ArrowHits * 0.22f);
            return;
        }
        int kills = won ? rng.Next(1, 5) : rng.Next(0, 2);
        Practise(Gear.Get(Captain.Kit.Weapon).Ranged ? "Bowcraft" : "Arms", kills * 0.5f);
        if (Captain.Kit.Horse != "on_foot") Practise("Riding", 0.4f);
    }
}
