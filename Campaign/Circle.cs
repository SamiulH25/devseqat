namespace Ironfield;

/// Your circle of mages on the map (docs/archive/MAGIC_AURA.md 3.2-3.3): its spellbook (spells known beyond its line's, the ones
/// prepared for the next battle) and its master, a named man who rises with battles and whose fall leaves it leaderless.
public sealed partial class Campaign
{
    /// Spells a circle can learn beyond its line's, and the crowns each costs (times how many it has learned before, plus one).
    public const int MaxLearned = 2, LearnCost = 150;
    /// Battles that raise the circle's master a rank (1-3), and the days a circle stays leaderless after its master falls.
    public static readonly int[] MasterBattles = { 3, 8, 15 };
    public const int LeaderlessDays = 10;
    static readonly string[] LearnSites = { "shrine", "stones", "hermit" };

    /// Your circle (the one company of casters you may keep), or null.
    public CompanyRecord? Circle => Player.Companies.FirstOrDefault(c => Rules.RoleOf(c) == "casters");

    /// The circle's best casters: the troop whose line of spells it fights with.
    public static UnitType? CircleTroop(CompanyRecord c) =>
        c.Stacks.Where(s => s.Men > 0 && s.Type.Spells.Length > 0).Select(s => s.Type).OrderByDescending(t => t.Tier).FirstOrDefault();

    /// Its tradition: faith or craft, from its line's own spells (every line knows Mend, which is faith).
    public static string CircleTradition(CompanyRecord c) =>
        CircleTroop(c)?.Spells.Where(id => id != "mend" && Abilities.Has(id)).Select(id => Abilities.Get(id).Tradition).LastOrDefault() ?? "faith";

    /// Every spell the circle knows: its line's and those it learned.
    public static List<string> Known(CompanyRecord c) => (CircleTroop(c)?.Spells ?? Array.Empty<string>()).Concat(c.Spells).Distinct().ToList();

    /// How many spells it can prepare for a battle: as many as its tier knows by its line.
    public static int PrepareSlots(CompanyRecord c) => CircleTroop(c)?.Spells.Length ?? 0;

    /// The spells it takes into the next battle: your choice where it still holds, else its line's.
    public static string[] Prepared(CompanyRecord c)
    {
        var known = Known(c);
        var mine = c.Prepared.Where(known.Contains).Distinct().Take(PrepareSlots(c)).ToArray();
        return mine.Length > 0 ? mine : CircleTroop(c)?.Spells ?? Array.Empty<string>();
    }

    /// Spells the circle could learn now: of its tradition (and Mend), not great workings, not known yet.
    public static IEnumerable<AbilityDef> Learnable(CompanyRecord c)
    {
        var known = Known(c);
        string trad = CircleTradition(c);
        return Abilities.All.Where(a => (a.Tradition == trad || a.Id == "mend") && !a.Great && !known.Contains(a.Id) && a.Tradition is "faith" or "craft")
            .OrderBy(a => a.Name);
    }

    /// Why the circle cannot learn this spell now, or null.
    public string? CannotLearn(string id)
    {
        if (Circle is not CompanyRecord c) return "YOU HAVE NO CIRCLE";
        if (!Abilities.Has(id) || !Learnable(c).Any(a => a.Id == id)) return "THEY CANNOT LEARN THAT";
        if (c.Spells.Count >= MaxLearned) return $"THEY KNOW {MaxLearned} SPELLS BEYOND THEIR LINE";
        string trad = CircleTradition(c);
        bool teacher = Companions.Any(k => S.Hours >= k.WoundedUntil && k.Calling == (trad == "faith" ? "Priest" : "Cunning"))
            || SiteAt(Player.Pos) is { } site && LearnSites.Contains(site.Kind) || trad == "faith" && RelicReady;
        if (!teacher) return trad == "faith" ? "NEEDS A PRIEST, A SHRINE, STONES OR A HERMIT, OR THE RELIC" : "NEEDS A CUNNING ONE, A SHRINE, STONES OR A HERMIT";
        int cost = LearnCost * (c.Spells.Count + 1);
        if (S.Gold < cost) return $"NEEDS {cost} CROWNS";
        return null;
    }

    /// The circle learns a spell (MA4). Returns null or why not.
    public string? LearnSpell(string id)
    {
        if (CannotLearn(id) is string why) return why;
        var c = Circle!;
        S.Gold -= LearnCost * (c.Spells.Count + 1);
        c.Spells.Add(id);
        if (CircleTradition(c) == "craft") BreakVow("no_craft", "work the craft");
        Log($"{c.Name} learn {Abilities.Get(id).Name}.");
        return null;
    }

    /// Prepare a known spell (or set it aside). Returns null or why not.
    public string? Prepare(string id, bool on)
    {
        if (Circle is not CompanyRecord c) return "YOU HAVE NO CIRCLE";
        if (!Known(c).Contains(id)) return "THEY DO NOT KNOW IT";
        var now = Prepared(c).ToList();
        if (on == now.Contains(id)) return null;
        if (on && now.Count >= PrepareSlots(c)) return $"ONLY {PrepareSlots(c)} PREPARED: SET ONE ASIDE FIRST";
        if (!on && now.Count <= 1) return "THEY MUST PREPARE ONE AT LEAST";
        if (on) now.Add(id); else now.Remove(id);
        c.Prepared = now;
        return null;
    }

    /// The weather a battle with this party would be fought in (the same roll PrepareBattle makes).
    public Weather WeatherFor(Party enemy) => Weather.For((float)(S.Hours % 24), SeasonIndex, HashCode.Combine(S.Seed, (int)S.Hours, enemy.Id));

    /// What the prepared spells make of the weather for the encounter panel: a spell it blocks (rain spells in the dry,
    /// fire in the rain).
    public string SpellNote(Party enemy) => SpellNote(WeatherFor(enemy).Rain >= 0.3f);

    public string SpellNote(bool raining)
    {
        if (Circle is not CompanyRecord c) return "";
        var prep = Prepared(c).Where(Abilities.Has).Select(Abilities.Get).ToList();
        var blocked = prep.Where(a => a.Needs == "rain" && !raining || a.Needs == "dry" && raining).Select(a => a.Name).ToList();
        string list = string.Join(", ", prep.Select(a => a.Name));
        return blocked.Count > 0 ? $"CIRCLE: {list}. NO USE HERE: {string.Join(", ", blocked)}" : $"CIRCLE: {list}";
    }

    // ---- the circle's master ----

    /// The circle's master, if he lives and is fit.
    public static Soldier? MasterOf(CompanyRecord c) => c.Master == 0 ? null : c.Roll.FirstOrDefault(m => m.Id == c.Master);

    /// His rank by the battles he has fought (0-3).
    public static int MasterRank(Soldier? m) => m == null ? 0 : MasterBattles.Count(b => m.Battles >= b);

    /// His title, from the circle's tradition.
    public static string MasterTitle(CompanyRecord c) => CircleTradition(c) == "faith" ? "Father of the Circle" : "Stormcaller";

    public bool Leaderless(CompanyRecord c) => c.LeaderlessUntil > S.Hours;

    /// Each day and after each battle: a circle without a master names one (its most seasoned named caster, else a new name);
    /// one whose master fell is leaderless for a while first.
    internal void CircleDaily()
    {
        if (Circle is not CompanyRecord c || CircleTroop(c) is not UnitType t) return;
        if (c.Master != 0 && MasterOf(c) == null)
        {
            c.Master = 0;
            c.LeaderlessUntil = S.Hours + LeaderlessDays * 24;
            Log($"{c.Name} have lost their master: no great working for {LeaderlessDays} days, until another rises.");
            return;
        }
        if (c.Master != 0 || Leaderless(c)) return;
        var next = c.Roll.Where(m => !m.Hurt && Troops.Has(m.Troop) && Troops.Get(m.Troop).Spells.Length > 0).OrderByDescending(m => m.Battles).ThenBy(m => m.Id).FirstOrDefault()
            ?? NameMan(c, $"became {MasterTitle(c)}", t.Id);
        if (next == null) return;
        c.Master = next.Id;
        if (next.Deeds.Count == 0 || !next.Deeds[^1].Contains(MasterTitle(c))) next.Deed($"became {MasterTitle(c)}");
        Log($"{next.Name} leads {c.Name} as {MasterTitle(c)}.");
    }

    /// The circle as it goes into battle: its prepared spells, its master's rank, and whether it is leaderless.
    ArmySpec WithCircle(ArmySpec army)
    {
        if (Circle is not CompanyRecord c) return army;
        var list = army.Companies.Select(co => co.Id != c.Id ? co
            : co with { Spells = Prepared(c), MasterRank = MasterRank(MasterOf(c)), Leaderless = Leaderless(c) }).ToList();
        return army with { Companies = list };
    }
}
