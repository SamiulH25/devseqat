using System.Text.Json;

namespace Ironfield;

/// A companion's calling from Data/companions.json (docs/COMPANIONS_MAGIC.md B1-B3): the aura on the company he leads, his
/// ability (and a second from level 5), and one talent (an effect on his company) for each level.
public sealed class CallingDef
{
    public string Id = "", Line = "", Aura = "", Ability = "", Second = "";
    public string[] Talents = Array.Empty<string>();
}

public static class Callings
{
    public const string FileName = "Data/companions.json";
    static List<CallingDef>? all;

    public static IReadOnlyList<CallingDef> All { get { Load(); return all!; } }
    public static CallingDef? Get(string id) => All.FirstOrDefault(c => c.Id == id);

    sealed class FileDto { public List<CallingDef> Callings = new(); }

    static void Load()
    {
        if (all != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("companion data not found", FileName);
        var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        all = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!.Callings;
    }

    /// Data checks: auras, talents and abilities exist; ten talents each.
    public static IEnumerable<string> Validate()
    {
        foreach (var c in All)
        {
            if (c.Aura != "" && !Effects.Has(c.Aura)) yield return $"{c.Id}: unknown aura {c.Aura}";
            if (!Abilities.Has(c.Ability) || !Abilities.Has(c.Second)) yield return $"{c.Id}: unknown ability";
            if (c.Talents.Length != Campaign.MaxCompanionLevel) yield return $"{c.Id}: needs {Campaign.MaxCompanionLevel} talents";
            foreach (var t in c.Talents) if (!Effects.Has(t)) yield return $"{c.Id}: unknown talent {t}";
        }
    }
}

/// Companions in the field (docs/COMPANIONS_MAGIC.md B1-B3): callings, XP and levels, what a companion brings into battle,
/// and the champions enemy lords bring.
public sealed partial class Campaign
{
    public const int MaxCompanionLevel = 10;
    /// XP for each level, 1 to 10.
    static readonly int[] LevelXp = { 10, 25, 45, 70, 100, 135, 175, 220, 270, 325 };

    public static int LevelFor(int xp) => LevelXp.Count(x => xp >= x);
    /// XP still needed for his next level (0 at the top).
    public static int XpToNext(Character ch) => ch.Level >= MaxCompanionLevel ? 0 : LevelXp[ch.Level] - ch.Xp;

    /// The calling a man takes when he joins, from who he is: the pious pray, the reckless duel, the cautious hold, the loyal
    /// carry a banner, a scout shoots; a few from the wild lands know hedge-craft. No dice, so nothing else shifts.
    public static string CallingFor(Character ch)
    {
        if (ch.Traits.Contains("Pious")) return "Priest";
        if (ch.Culture is "crimson" or "steppe" && ch.Id % 3 == 0) return "Cunning";
        if (ch.Traits.Contains("Reckless")) return "Sword";
        if (ch.Traits.Contains("Cautious")) return "Shield";
        if (ch.Traits.Contains("Loyal")) return "Banner";
        if (ch.Role == "Scout") return "Bow";
        if (ch.Traits.Contains("Honourable")) return "Lance";
        return new[] { "Sword", "Shield", "Banner", "Bow", "Lance" }[ch.Id % 5];
    }

    /// Every companion has a calling (an old save's companions take theirs on load).
    void EnsureCallings()
    {
        foreach (var k in Companions) if (k.Calling == "") k.Calling = CallingFor(k);
    }

    /// His abilities in battle: his calling's, and its second from level 5.
    public static string[] GiftsOf(Character ch) => Callings.Get(ch.Calling) is CallingDef c
        ? (ch.Level >= 5 ? new[] { c.Ability, c.Second } : new[] { c.Ability }) : Array.Empty<string>();

    /// The effects his company carries into battle: the calling's aura and his talents so far.
    public static IEnumerable<string> TalentsOf(Character ch) => Callings.Get(ch.Calling) is CallingDef c
        ? (c.Aura == "" ? Array.Empty<string>() : new[] { c.Aura }).Concat(c.Talents.Take(ch.Level)) : Array.Empty<string>();

    /// Your companies with their heroes' callings: abilities, level, aura and talents, and a sworn brother's bond when both lead in this battle.
    ArmySpec WithHeroes(ArmySpec army)
    {
        var leading = army.Companies.Where(co => co.Hero != "").Select(co => Companions.FirstOrDefault(k => k.CompanyId == co.Id)).Where(k => k != null).Select(k => k!.Id).ToHashSet();
        var list = army.Companies.Select(co =>
        {
            if (co.Hero == "" || Companions.FirstOrDefault(k => k.CompanyId == co.Id) is not Character k) return co;
            var standing = TalentsOf(k).ToList();
            if (k.Sworn >= 0 && leading.Contains(k.Sworn)) standing.Add("sworn");
            return co with { HeroCalling = k.Calling, HeroGifts = GiftsOf(k), HeroLevel = k.Level, Standing = standing.ToArray() };
        }).ToList();
        return army with { Companies = list };
    }

    /// An enemy lord of standing brings a champion (B4): a named hero with a calling, at the head of his best company. A
    /// companion who left you on bad terms may be the man.
    ArmySpec WithChampion(ArmySpec army, Party enemy)
    {
        if (enemy.Kind != PartyKind.Lord || enemy.Tier < 2 || CharacterOf(enemy) is not Character lord) return army;
        int at = army.Companies.ToList().FindIndex(co => co.Hero == "" && co.Role is "infantry" or "horse");
        if (at < 0) return army;
        var turned = S.Characters.FirstOrDefault(c => c.Kind == "gone" && c.Faction == enemy.Faction);
        var champ = new Character { Id = lord.Id, Traits = lord.Traits, Culture = lord.Culture, Name = turned?.Name ?? $"{lord.Name.Replace("Lord ", "")}'s champion" };
        champ.Calling = turned?.Calling is { Length: > 0 } tc ? tc : CallingFor(champ);
        champ.Level = Math.Clamp(1 + enemy.Tier + Day / 60, 1, MaxCompanionLevel);
        var list = army.Companies.ToList();
        list[at] = list[at] with { Hero = champ.Name, HeroCalling = champ.Calling, HeroGifts = GiftsOf(champ), HeroLevel = champ.Level, Standing = TalentsOf(champ).ToArray() };
        return army with { Companies = list };
    }

    /// After a battle you fought: each companion whose company took the field earns XP (fought, won, his company's kills) and
    /// may rise a level; companions who fought together grow closer (B2, B4).
    void CompanionsAfterBattle(BattleResult r, int playerTeam)
    {
        bool won = r.Winner == playerTeam;
        var fought = new List<Character>();
        foreach (var k in Companions.ToList())
        {
            var cr = r.Companies.FirstOrDefault(x => x.Id == k.CompanyId && x.Team == playerTeam);
            if (cr == null || !cr.Deployed) continue;
            fought.Add(k);
            GainXp(k, 5 + (won ? 3 : 0) + Math.Min(5, cr.Kills / 5));
            if (k.Story == 1 && won) StoryWin(k);
        }
        BondsAfterBattle(fought);
    }

    /// XP for a companion; each level gained adds his next talent (and at 5 his second ability).
    public void GainXp(Character k, int xp)
    {
        k.Xp += xp;
        int level = LevelFor(k.Xp);
        while (k.Level < level)
        {
            k.Level++;
            var c = Callings.Get(k.Calling);
            string talent = c != null && k.Level <= c.Talents.Length ? Effects.Get(c.Talents[k.Level - 1]).Name.ToLowerInvariant() : "";
            Log(k.Level == 5 && c != null ? $"{k.Name} reaches level 5 and learns {Abilities.Get(c.Second).Name}." : $"{k.Name} reaches level {k.Level}{(talent != "" ? $" ({talent})" : "")}.");
            if (k.Level >= 3 && k.Story == 0) OpenStory(k);
            if (k.Level >= 4 && k.Student == 0) TakeStudent(k);
        }
    }
}
