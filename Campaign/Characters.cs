using Microsoft.Xna.Framework;

namespace Ironfield;

/// A person in the campaign (CAMPAIGN_OVERHAUL.md 3.1): a lord, a companion, a wanderer waiting in a tavern, or you.
public sealed class Character
{
    public int Id;
    public string Name = "", Culture = "common";
    /// "lord", "companion", "wanderer", "captain" or "knight" (a knight order's, docs/archive/KNIGHT_ORDERS.md 6.1).
    public string Kind = "lord";
    /// A knight's order and rank in it (OrderRanks index), and his aura tier (0-5).
    public string Order = "";
    public int OrderRank, Aura;
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
    /// A companion who rose from the ranks (docs/archive/LEGACY.md L4): his record as a named man and the company he came from (-1: none).
    public Soldier? Ranks;
    public int FromCompany = -1;
    /// His calling in the field (docs/archive/COMPANIONS_MAGIC.md B1; Data/companions.json), fixed when he joins; his XP from deeds and
    /// level (1-10: one talent each, a second ability at 5; Campaign/Callings.cs).
    public string Calling = "";
    public int Xp, Level;
    /// Battles fought alongside each other companion (by id), and his sworn brother (-1: none; B4).
    public Dictionary<int, int> Together = new();
    public int Sworn = -1;
    /// His story (B4): 0 untold, 1 open (since StoryDay, wins so far), 2 done, 3 let go.
    public int Story, StoryWins, StoryDay;
    /// A named man of his company he teaches (B5; Soldier id, 0: none); warned you once that he may leave.
    public int Student;
    public bool Warned;
    /// His house and kin (docs/archive/LORDS.md LD0; Campaign/Houses.cs): ids of characters, -1 for none.
    public int House = -1, Father = -1, Mother = -1, Spouse = -1;
    public List<int> Children = new();
    public bool Female;
    /// Talk (LD3): the day you last raised each topic with him (asking again too soon annoys).
    public SortedDictionary<string, int> Asked = new();
    /// What he wants (LD4; Campaign/Wants.cs): "fief", "revenge", "peace", "marshal", "wealth", "match", "glory", "monster" or "";
    /// and who or what it is about (a house, character or settlement id; -1: none). Known to you once he told you.
    public string Want = "";
    public int WantOn = -1;
    public bool WantKnown;
    /// A promise you made him to help his want (the want and the day; "": none), and how far you have courted her (LD6, 0-3).
    public string Promise = "";
    public int PromiseDay, Courted, CourtedDay;
    /// The given name alone ("Aldric"), for titles and heirs; "" for characters from before houses.
    public string First = "";
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
    /// He founds a house with a family around him; once the campaign runs, a grown younger son of the realm's houses may get the banner instead.
    Character MakeLord(Party p)
    {
        if (Day > 1 && p.KnightOrder == "" && SpareKin(p.Faction) is Character spare && KRng.Chance(0.5f))
        {
            Raise(spare, p);
            return spare;
        }
        string culture = Factions.CultureOf(p.Faction);
        var ch = NewCharacter("lord", "Lord " + PersonName(culture), p.Faction, culture, 2 + rng.Next(2));
        ch.First = FirstOf(ch);
        if (Names.Get().LadiesOf(culture).Contains(ch.First)) { ch.Female = true; ch.Name = "Lady" + ch.Name[4..]; }
        ch.PartyId = p.Id;
        p.CharId = ch.Id;
        p.Name = ch.Name;
        if (S.CaptainChar >= 0 && Captain.Contract >= 0 && p.Faction == Captain.Contract) ch.Opinion[S.CaptainChar] = 15;
        if (p.KnightOrder == "") SeedFamily(ch, FoundHouse(ch));
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
        return o + BondRegard(a, b); // kin and feuds (LordBonds.cs)
    }

    /// What `a` thinks of `b`, -100..100.
    public float OpinionOf(Character a, Character b) => Math.Clamp(BaseOpinion(a, b) + a.Opinion.GetValueOrDefault(b.Id) + (b.Id == S.CaptainChar ? (Has("Kingmaker") ? 10 : 0) + RegardOfTraits(a) + FameRegard + AuraRegard(a) : 0), -100, 100);

    /// Lords of your own realm know your aura (docs/archive/MAGIC_AURA.md 4.3, MA8): +5 for an Aura Expert, +10 for a Master.
    float AuraRegard(Character a) => Captain.Contract < 0 || a.Faction != Captain.Contract ? 0 : Captain.AuraTier >= 4 ? 10 : Captain.AuraTier >= 3 ? 5 : 0;

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
    void MaybeSlain(Party p, bool byPlayer, Character? killer = null)
    {
        if (CharacterOf(p) != null && rng.Chance(LordSlainPercent / 100f)) SlayLord(p, byPlayer, killer: killer);
    }

    /// A lord dies (in battle, or of old age with `cause`): the next of his house takes up the banner and grudges; a house with
    /// nobody left to ride gives the banner to a new lord.
    internal void SlayLord(Party p, bool byPlayer, string cause = "falls in battle", Character? killer = null)
    {
        if (CharacterOf(p) is not Character old) return;
        old.Dead = true;
        old.PartyId = -1;
        if (killer != null && !byPlayer) StartFeud(old, killer); // a blood debt between houses (LD2)
        if (byPlayer) HelpedWant("revenge", c => c.WantOn == old.House && old.House >= 0, "you avenged my house");
        Character heir;
        string kin = "";
        if (p.KnightOrder != "") heir = KnightHeir(p);
        else if (HouseHeir(old) is Character next) { kin = KinWord(old, next); Raise(next, p); heir = next; }
        else { heir = MakeLord(p); if (heir.Age < 18) heir.Age = 18; }
        p.Name = heir.Name;
        // The heir inherits his father's view of you, and adds his own if you were the one who killed him.
        foreach (var (k, v) in old.Opinion) heir.Opinion[k] = heir.Opinion.GetValueOrDefault(k) + v * 0.5f;
        if (byPlayer) Remember(heir, Me, $"you slew my {(kin == "" ? "lord" : kin)}", -30);
        Log(kin == "" ? $"{old.Name} {cause}. {heir.Name} takes up {His(old)} banner." : $"{old.Name} {cause}. {His(old).ToUpperInvariant()[0]}{His(old)[1..]} {KinWord(heir, old)} {heir.Name} takes up the banner.");
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
        ch.Ranks = c.OfficerMan; ch.FromCompany = c.OfficerMan != null ? c.Id : -1; // L4: he rose from its ranks
        c.OfficerMan = null;
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
        EnsureCallings();
        foreach (var c in Player.Companies)
            c.Hero = Companions.FirstOrDefault(k => k.CompanyId == c.Id && S.Hours >= k.WoundedUntil)?.Name ?? "";
        // A companion whose company is gone takes the first company without one.
        foreach (var k in Companions.Where(k => Player.Companies.All(c => c.Id != k.CompanyId)).ToList())
            if (Player.Companies.FirstOrDefault(c => !Companions.Any(o => o.CompanyId == c.Id)) is CompanyRecord free) { k.CompanyId = free.Id; free.Hero = S.Hours >= k.WoundedUntil ? k.Name : ""; }
        PruneDivisions();
    }

    /// A hero who fell in battle is wounded for five days; one time in ten he does not get up.
    void HeroFell(CompanyRecord c, string foe = "")
    {
        if (Companions.FirstOrDefault(k => k.CompanyId == c.Id) is not Character k) return;
        if (!Has("Triage") && rng.Chance(0.1f)) { k.Dead = true; Log($"{k.Name}, your {k.Role.ToLowerInvariant()}, has fallen leading {c.Name}."); CompanionFell(k, foe); BrotherFell(k); StudentRises(k); GriefTrial(k); }
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
    public int RecruitsForYou(Settlement s) => (int)MathF.Floor(s.Recruits * MathF.Min(1, RecruitShare(s) + FameShare)); // L6: fame draws men
}
