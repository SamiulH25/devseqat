using Microsoft.Xna.Framework;

namespace Ironfield;

/// A person in the campaign (CAMPAIGN_OVERHAUL.md 3.1): a lord, a companion, a wanderer waiting in a tavern, or you.
public sealed class Character
{
    public int Id;
    public string Name = "", Culture = "common";
    /// "lord", "companion", "wanderer" or "captain".
    public string Kind = "lord";
    public int Faction = -1, Age = 30;
    public List<string> Traits = new();
    /// What this character thinks of others, by character id, from events (the base from culture and traits is added on read).
    public Dictionary<int, float> Opinion = new();
    public List<Memory> Memories = new();
    /// The lord's party, or the company a companion leads (-1: none).
    public int PartyId = -1, CompanyId = -1;
    /// A companion's role (3.3): Quartermaster, Surgeon, Scout, Drillmaster or Steward.
    public string Role = "";
    public bool Dead;
    /// A companion who fell in battle is out of the fighting until then (campaign hours).
    public float WoundedUntil;
    /// The settlement a wanderer waits in (-1: none).
    public int AtSettlement = -1;
    /// The character this lord holds his land from, when it is not simply his realm's ruler: your vassals (F6).
    public int Liege = -1;
    /// A companion's skill at leading a division (0-10): one more for every two battles he leads one.
    public int Command = 2, CommandBattles;
    /// How he leads a division: "Bold", "Steady" or "Cautious" ("" = from his traits).
    public string Temper = "";
}

/// Something a character remembers about another (3.2): shown as the reasons behind an opinion; fades with time.
public sealed class Memory
{
    public int Day, About;
    public string What = "";
    public float Weight;
}

/// Characters, opinion and companions (step F3).
public sealed partial class Campaign
{
    public static readonly string[] LordTraits = { "Honourable", "Cruel", "Cautious", "Reckless", "Greedy", "Generous", "Pious", "Ambitious", "Loyal", "Fickle" };
    static readonly (string A, string B)[] TraitClashes = { ("Honourable", "Cruel"), ("Cautious", "Reckless"), ("Greedy", "Generous"), ("Loyal", "Fickle") };
    public static readonly string[] CompanionRoles = { "Quartermaster", "Surgeon", "Scout", "Drillmaster", "Steward" };
    public static readonly Dictionary<string, string> RoleText = new()
    {
        ["Quartermaster"] = "FOOD -15%, UPKEEP -5%", ["Surgeon"] = "WOUNDED HEAL 30% FASTER", ["Scout"] = "SEES 25% FURTHER",
        ["Drillmaster"] = "DRILL XP +25%", ["Steward"] = "FIEF INCOME +15%",
    };
    public const int MaxCompanions = 6, CompanionBattles = 15, LordSlainPercent = 5;

    public Character Me => CharacterById(S.CaptainChar)!;
    public Character? CharacterById(int id) => S.Characters.FirstOrDefault(c => c.Id == id);
    public Character? CharacterOf(Party p) => p.CharId >= 0 ? CharacterById(p.CharId) : null;
    public IEnumerable<Character> Companions => S.Characters.Where(c => c.Kind == "companion" && !c.Dead);

    Character NewCharacter(string kind, string name, int faction, string culture, int traits)
    {
        var ch = new Character { Id = S.NextId++, Kind = kind, Name = name, Faction = faction, Culture = culture, Age = 20 + rng.Next(35) };
        while (ch.Traits.Count < traits)
        {
            var t = rng.Pick(LordTraits);
            if (!ch.Traits.Contains(t) && !TraitClashes.Any(x => x.A == t && ch.Traits.Contains(x.B) || x.B == t && ch.Traits.Contains(x.A))) ch.Traits.Add(t);
        }
        S.Characters.Add(ch);
        return ch;
    }

    /// The captain's own record (made once, with the campaign).
    void MakeCaptainCharacter(string culture)
    {
        var me = new Character { Id = S.NextId++, Kind = "captain", Name = "You", Culture = culture, Faction = Captain.Contract, PartyId = Player.Id, Age = 30 };
        S.Characters.Add(me);
        S.CaptainChar = me.Id;
    }

    /// A first name of the culture and an epithet that no living character already bears (S1). It always takes exactly two
    /// draws (a clash steps to the next epithet), so name lists never shift the campaign's random numbers.
    public string PersonName(string culture)
    {
        var n = Names.Get();
        string first = rng.Pick(n.LordsOf(culture));
        int e = rng.Next(n.Epithets.Count);
        for (int k = 0; k < n.Epithets.Count; k++)
        {
            string name = first + " " + n.Epithets[(e + k) % n.Epithets.Count];
            if (!S.Characters.Any(c => !c.Dead && c.Name.EndsWith(name))) return name;
        }
        return first + " " + n.Epithets[e];
    }

    /// A lord for a new party: 2-3 traits, his realm's culture; a lord of your liege starts warm towards you (you are sworn to the same crown).
    Character MakeLord(Party p)
    {
        var n = Names.Get();
        var ch = NewCharacter("lord", "Lord " + PersonName(Factions.CultureOf(p.Faction)), p.Faction, Factions.CultureOf(p.Faction), 2 + rng.Next(2));
        ch.PartyId = p.Id;
        p.CharId = ch.Id;
        p.Name = ch.Name;
        if (S.CaptainChar >= 0 && Captain.Contract >= 0 && p.Faction == Captain.Contract) ch.Opinion[S.CaptainChar] = 15;
        return ch;
    }

    // ---------- opinion ----------

    /// The standing part of an opinion: a foreign culture and clashing traits (3.2).
    float BaseOpinion(Character a, Character b)
    {
        float o = 0;
        if (a.Culture != b.Culture && a.Culture != Factions.Common && b.Culture != Factions.Common) o -= 10;
        foreach (var (x, y) in TraitClashes)
            if (a.Traits.Contains(x) && b.Traits.Contains(y) || a.Traits.Contains(y) && b.Traits.Contains(x)) o -= 15;
        return o;
    }

    /// What `a` thinks of `b`, -100..100.
    public float OpinionOf(Character a, Character b) => Math.Clamp(BaseOpinion(a, b) + a.Opinion.GetValueOrDefault(b.Id) + (b.Id == S.CaptainChar && Has("Kingmaker") ? 10 : 0), -100, 100);

    /// What a character thinks of you.
    public float OpinionOfYou(Character a) => OpinionOf(a, Me);

    /// A lord party's opinion of you (0 for parties with no character: brigands, garrisons).
    public float OpinionOfYou(Party p) => CharacterOf(p) is Character ch ? OpinionOfYou(ch) : 0;

    /// `who` remembers something `about` someone: the weight moves the opinion now, and the memory explains it later.
    public void Remember(Character who, Character about, string what, float weight)
    {
        if (who == about || who.Dead) return;
        // Traits colour the weight: the Fickle swing harder, the Loyal forgive less easily a wrong but hold to a kindness.
        if (who.Traits.Contains("Fickle")) weight *= 1.5f;
        if (who.Traits.Contains("Honourable") && what.Contains("free")) weight *= 1.3f;
        if (who.Traits.Contains("Greedy") && what.Contains("gold")) weight *= 1.3f;
        // Charm (F4): goodwill you earn counts more, and earning it teaches you.
        if (about == Me && weight > 0)
        {
            weight *= 1 + Skill("Charm") / 400f + (Has("SilverTongue") ? 0.25f : 0);
            Practise("Charm", weight * 0.1f);
        }
        who.Opinion[about.Id] = Math.Clamp(who.Opinion.GetValueOrDefault(about.Id) + weight, -100, 100);
        who.Memories.Add(new Memory { Day = Day, About = about.Id, What = what, Weight = weight });
        if (who.Memories.Count > 8) who.Memories.Remove(who.Memories.OrderBy(m => MathF.Abs(m.Weight)).ThenBy(m => m.Day).First());
    }

    /// The memories behind an opinion, strongest first.
    public IEnumerable<Memory> MemoriesOf(Character who, Character about) =>
        who.Memories.Where(m => m.About == about.Id).OrderByDescending(m => MathF.Abs(m.Weight));

    /// Weekly: opinions and memories fade towards the base (5% a week); sharing a liege warms (+1 a week).
    internal void RelationsWeekly()
    {
        foreach (var ch in S.Characters.Where(c => !c.Dead))
        {
            foreach (var k in ch.Opinion.Keys.ToList())
            {
                ch.Opinion[k] *= 0.95f;
                if (MathF.Abs(ch.Opinion[k]) < 0.5f) ch.Opinion.Remove(k);
            }
            foreach (var m in ch.Memories) m.Weight *= 0.95f;
            ch.Memories.RemoveAll(m => MathF.Abs(m.Weight) < 2);
            if (ch.Kind == "lord" && Captain.Contract >= 0 && ch.Faction == Captain.Contract)
                ch.Opinion[S.CaptainChar] = Math.Clamp(ch.Opinion.GetValueOrDefault(S.CaptainChar) + 1, -100, 100);
        }
        Me.Faction = Captain.Contract;
    }

    /// Who remembers what after a battle you fought in (3.2): the lords you beat, and the lords who fought beside you.
    internal void RelationsAfterBattle(IReadOnlyList<Party> winners, IReadOnlyList<Party> losers, bool won)
    {
        var mine = won ? winners : losers;
        var theirs = won ? losers : winners;
        foreach (var p in mine.Where(p => p != Player))
            if (CharacterOf(p) is Character ally)
            {
                Remember(ally, Me, "we fought side by side", 6);
                if (won) Remember(ally, Me, "we won a battle together", 4);
            }
        if (won)
            foreach (var p in theirs)
                if (CharacterOf(p) is Character foe) Remember(foe, Me, "you beat me in the field", -12);
    }

    /// A lord routed in the field may fall (3.1): he is gone, and an heir takes up his banner, house and grudges.
    void MaybeSlain(Party p, bool byPlayer)
    {
        if (CharacterOf(p) != null && rng.Chance(LordSlainPercent / 100f)) SlayLord(p, byPlayer);
    }

    internal void SlayLord(Party p, bool byPlayer)
    {
        if (CharacterOf(p) is not Character old) return;
        old.Dead = true;
        old.PartyId = -1;
        var heir = MakeLord(p);
        heir.Name = old.Name.Split(' ') is var parts && parts.Length >= 2 ? $"Lord {rng.Pick(Names.Get().LordsOf(old.Culture))} {string.Join(' ', parts.Skip(2))}".Trim() : heir.Name;
        p.Name = heir.Name;
        heir.Age = 18 + rng.Next(10);
        // The heir inherits his father's view of you, and adds his own if you were the one who killed him.
        foreach (var (k, v) in old.Opinion) heir.Opinion[k] = v * 0.5f;
        if (byPlayer) Remember(heir, Me, "you slew my father", -30);
        Log($"{old.Name} falls in battle. {heir.Name} takes up his banner.");
    }

    // ---------- companions (3.3) ----------

    /// A companion in your service with this role, fit to work (not lying wounded).
    public bool HasCompanion(string role) => Companions.Any(c => c.Role == role && S.Hours >= c.WoundedUntil);

    /// An officer who has survived enough battles becomes a companion, if there is room (3.3).
    void OfficerToCompanion(CompanyRecord c)
    {
        if (!Player.Companies.Contains(c) || c.Officer == "" || c.OfficerBattles < CompanionBattles) return;
        if (Companions.Count() >= MaxCompanions || Companions.Any(k => k.CompanyId == c.Id)) return;
        var ch = NewCharacter("companion", c.Officer.Replace("Sgt ", ""), Captain.Contract, c.Culture == "" ? Me.Culture : c.Culture, 2);
        ch.Role = rng.Pick(CompanionRoles.Where(r => !Companions.Any(k => k != ch && k.Role == r)).DefaultIfEmpty("Quartermaster").ToList());
        ch.Temper = rng.Pick(new[] { "Bold", "Steady", "Steady", "Cautious" });
        ch.Command = 3; // he has led men already, as a sergeant
        ch.CompanyId = c.Id;
        ch.PartyId = Player.Id;
        ch.Opinion[S.CaptainChar] = 40;
        Remember(ch, Me, "you made me one of your household", 10);
        Log($"{c.Officer} of {c.Name} has fought {c.OfficerBattles} battles at your side and joins your household as {ch.Role.ToLowerInvariant()}.");
        c.Officer = "";
        c.Traits.Clear();
        c.OfficerBattles = 0;
        RefreshHeroes();
    }

    /// Weekly: one town in four has a wanderer looking for a captain (3.3).
    void WanderersWeekly()
    {
        S.Characters.RemoveAll(c => c.Kind == "wanderer");
        foreach (var t in S.Settlements.Where(t => t.Kind == SettlementKind.Town))
        {
            if (!rng.Chance(0.25f)) continue;
            var n = Names.Get();
            var ch = NewCharacter("wanderer", PersonName(t.Culture), -1, t.Culture, 2);
            ch.Role = rng.Pick(CompanionRoles);
            ch.AtSettlement = t.Id;
        }
    }

    public Character? WandererAt(Settlement s) => S.Characters.FirstOrDefault(c => c.Kind == "wanderer" && c.AtSettlement == s.Id);
    public int WandererPrice(Character w) => 150 + 25 * Companions.Count();

    /// Take a wanderer into your household: he leads the first company without a companion.
    public string? HireWanderer(Settlement s)
    {
        if (WandererAt(s) is not Character w) return "NOBODY IS LOOKING FOR WORK";
        if (Companions.Count() >= MaxCompanions) return $"YOUR HOUSEHOLD IS FULL ({MaxCompanions})";
        int price = WandererPrice(w);
        if (S.Gold < price) return $"NEEDS {price} CROWNS";
        S.Gold -= price;
        w.Kind = "companion";
        w.Faction = Captain.Contract;
        w.AtSettlement = -1;
        w.PartyId = Player.Id;
        w.CompanyId = Player.Companies.FirstOrDefault(c => !Companions.Any(k => k.CompanyId == c.Id))?.Id ?? -1;
        w.Opinion[S.CaptainChar] = 25;
        Log($"{w.Name} joins your household as {w.Role.ToLowerInvariant()}.");
        RefreshHeroes();
        return null;
    }

    /// Put a companion at the head of another company.
    public void AssignCompanion(Character ch, CompanyRecord c)
    {
        foreach (var k in Companions.Where(k => k.CompanyId == c.Id)) k.CompanyId = ch.CompanyId;
        ch.CompanyId = c.Id;
        RefreshHeroes();
    }

    /// Each company's hero for battle: the fit companion who leads it (2.4 / 3.3), shown in battle as a hero unit.
    public void RefreshHeroes()
    {
        foreach (var c in Player.Companies)
            c.Hero = Companions.FirstOrDefault(k => k.CompanyId == c.Id && S.Hours >= k.WoundedUntil)?.Name ?? "";
        // A companion whose company is gone takes the first company without one.
        foreach (var k in Companions.Where(k => Player.Companies.All(c => c.Id != k.CompanyId)).ToList())
            if (Player.Companies.FirstOrDefault(c => !Companions.Any(o => o.CompanyId == c.Id)) is CompanyRecord free) { k.CompanyId = free.Id; free.Hero = S.Hours >= k.WoundedUntil ? k.Name : ""; }
        PruneDivisions();
    }

    /// A hero who fell in battle is wounded for five days; one time in ten he does not get up.
    void HeroFell(CompanyRecord c)
    {
        if (Companions.FirstOrDefault(k => k.CompanyId == c.Id) is not Character k) return;
        if (!Has("Triage") && rng.Chance(0.1f)) { k.Dead = true; Log($"{k.Name}, your {k.Role.ToLowerInvariant()}, has fallen leading {c.Name}."); }
        else { k.WoundedUntil = (float)S.Hours + 5 * 24; Log($"{k.Name} is carried from the field, wounded."); }
        RefreshHeroes();
    }

    // ---------- recruits and opinion (4.2) ----------

    /// The character whose say decides how a settlement's people take to you: the lord seated in its province, else none.
    public Character? HolderOf(Settlement s) =>
        S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == s.Faction && p.HomeId >= 0 && SettlementById(p.HomeId).Province == s.Province)
            .Select(CharacterOf).FirstOrDefault(c => c != null && !c.Dead);

    /// The share of a settlement's recruits who will follow you (4.2): -50 opinion none, 0 half, +50 all; your own fiefs, all.
    public float RecruitShare(Settlement s)
    {
        if (s.Owner == Player.Id) return 1;
        float recruiter = Has("Recruiter") ? 0.2f : 0;
        float op = HolderOf(s) is Character h ? OpinionOfYou(h)
            : S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == s.Faction).Select(OpinionOfYou).DefaultIfEmpty(0).Average();
        return Math.Clamp(0.5f + op / 100f + recruiter, 0, 1);
    }

    /// How many of a settlement's recruits will take your coin now.
    public int RecruitsForYou(Settlement s) => (int)MathF.Floor(s.Recruits * RecruitShare(s));
}
