namespace Ironfield;

/// A noble house (docs/archive/LORDS.md LD0): its lords and kin share its name ("the Grim" after their given names), its arms and its fate.
/// Its seat is whatever its head's party holds (`SeatOf`).
public sealed class House
{
    public int Id, Faction = -1, Founded, Charge, Field;
    /// "House Vael"; and the family name its members carry after their given names ("the Grim").
    public string Name = "", Epithet = "", Culture = "";
    public float Prestige;
    public bool Extinct;
    /// Houses this one is at feud with (LD2): kin slain; heirs inherit it, as the house does not die with a lord.
    public List<int> Feuds = new();
}

/// Campaign: houses and kin. Every lord belongs to a house with a family around him (a wife, children, maybe a brother);
/// family members who lead no party are characters of kind "kin". Heirs come from the house; people age each year, marry,
/// have children and die of old age. All dice come from the houses' own stream (CampaignState.KinRng).
public sealed partial class Campaign
{
    Rng? kinRng;
    Rng KRng => kinRng ??= Rng.Kin(S);

    /// Age at which a child is grown (may lead, marry, inherit); past OldDeath a year may be the last.
    public const int ComeOfAge = 16, OldDeath = 52;

    public House? HouseById(int id) => id < 0 ? null : S.Houses.FirstOrDefault(h => h.Id == id);
    public House? HouseOf(Character c) => HouseById(c.House);
    /// The living members of a house, eldest first.
    public IEnumerable<Character> Members(House h) => S.Characters.Where(c => c.House == h.Id && !c.Dead).OrderByDescending(c => c.Age);
    /// The settlement the house holds through its lords' parties (null: none).
    public Settlement? SeatOf(House h) => S.Settlements.FirstOrDefault(s => s.Owner >= 0 && PartyById(s.Owner) is Party p && CharacterOf(p) is { Dead: false } c && c.House == h.Id);

    public static string His(Character c) => c.Female ? "her" : "his";

    /// What `who` is to `to` in words: father, mother, son, daughter, brother, sister, husband, wife or kinsman.
    public string KinWord(Character who, Character to)
    {
        if (to.Father == who.Id || to.Mother == who.Id) return who.Female ? "mother" : "father";
        if (who.Father == to.Id || who.Mother == to.Id) return who.Female ? "daughter" : "son";
        if (to.Spouse == who.Id) return who.Female ? "wife" : "husband";
        if (who.Father >= 0 && who.Father == to.Father || who.Mother >= 0 && who.Mother == to.Mother) return who.Female ? "sister" : "brother";
        return who.Female ? "kinswoman" : "kinsman";
    }

    /// His family in a line: "wed to Lady Mira the Grim; 2 sons, 1 daughter" ("" when he has none living).
    public string FamilyLine(Character c)
    {
        var parts = new List<string>();
        if (CharacterById(c.Spouse) is { Dead: false } w) parts.Add("wed to " + w.Name);
        var kids = c.Children.Select(CharacterById).OfType<Character>().Where(k => !k.Dead).ToList();
        int sons = kids.Count(k => !k.Female), daughters = kids.Count - sons;
        if (sons > 0) parts.Add($"{sons} son{(sons > 1 ? "s" : "")}");
        if (daughters > 0) parts.Add($"{daughters} daughter{(daughters > 1 ? "s" : "")}");
        return string.Join(", ", parts);
    }

    /// A new house for a lord: named after a place of his culture that no living house bears; his epithet becomes its family name.
    House FoundHouse(Character lord)
    {
        var places = Names.Get().PlacesOf(lord.Culture);
        int k = KRng.Next(places.Count);
        string name = "House " + places[k];
        for (int i = 1; i < places.Count && S.Houses.Any(h => !h.Extinct && h.Name == name); i++) name = "House " + places[(k + i) % places.Count];
        var house = new House
        {
            Id = S.NextId++, Faction = lord.Faction, Culture = lord.Culture, Founded = Day, Name = name, Charge = KRng.Next(8), Field = KRng.Next(4),
            Epithet = EpithetOf(lord), Prestige = 10 + KRng.Next(30),
        };
        S.Houses.Add(house);
        lord.House = house.Id;
        if (lord.First == "") lord.First = FirstOf(lord);
        return house;
    }

    /// "Lord Aldric the Grim" -> "Aldric"; "Lord Aldric the Grim" -> "the Grim".
    static string FirstOf(Character c) => c.Name.Split(' ') is var w && w.Length >= 2 && w[0] is "Lord" or "Lady" or "Ser" ? w[1] : c.Name.Split(' ')[0];
    static string EpithetOf(Character c)
    {
        var w = c.Name.Split(' ');
        int skip = w.Length >= 2 && w[0] is "Lord" or "Lady" or "Ser" ? 2 : 1;
        return string.Join(' ', w.Skip(skip));
    }

    /// The name a member of a house carries: "Lord/Lady First Epithet" when grown, "First Epithet" as a child.
    void Retitle(Character c)
    {
        if (HouseOf(c) is not House h || c.First == "") return;
        string title = c.Age < ComeOfAge ? "" : c.Female ? "Lady " : "Lord ";
        c.Name = (title + c.First + " " + h.Epithet).Trim();
    }

    /// A given name of the culture for a new member of a house, one no living member already has.
    string KinName(string culture, bool female, House h)
    {
        var n = Names.Get();
        var list = female ? n.LadiesOf(culture) : n.LordsOf(culture).Where(x => !n.LadiesOf(culture).Contains(x)).ToList();
        if (list.Count == 0) list = n.LordsOf(culture);
        int k = KRng.Next(list.Count);
        var taken = Members(h).Select(m => m.First).ToHashSet();
        for (int i = 0; i < list.Count; i++) if (!taken.Contains(list[(k + i) % list.Count])) return list[(k + i) % list.Count];
        return list[k];
    }

    /// A family member who leads no party: 1-2 traits, from the houses' dice.
    Character NewKin(House h, bool female, int age)
    {
        var ch = new Character { Id = S.NextId++, Kind = "kin", Faction = h.Faction, Culture = h.Culture, Age = age, Female = female, House = h.Id };
        ch.First = KinName(h.Culture, female, h);
        int traits = 1 + KRng.Next(2);
        for (int tries = 0; ch.Traits.Count < traits && tries < 20; tries++)
        {
            var t = KRng.Pick(LordTraits);
            if (!ch.Traits.Contains(t) && !TraitClashes.Any(x => x.A == t && ch.Traits.Contains(x.B) || x.B == t && ch.Traits.Contains(x.A))) ch.Traits.Add(t);
        }
        S.Characters.Add(ch);
        Retitle(ch);
        return ch;
    }

    static void Wed(Character a, Character b) { a.Spouse = b.Id; b.Spouse = a.Id; }

    /// Children of a couple (either may be null for an unknown parent).
    void Parent(Character child, Character? x, Character? y)
    {
        foreach (var p in new[] { x, y })
        {
            if (p == null) continue;
            if (p.Female) child.Mother = p.Id; else child.Father = p.Id;
            p.Children.Add(child.Id);
        }
    }

    /// The family around a new lord: a spouse (often, past 22), 0-4 children younger than the couple, maybe a brother or sister.
    void SeedFamily(Character lord, House h)
    {
        Character? spouse = null;
        if (lord.Age >= 22 && KRng.Chance(0.75f))
        {
            spouse = NewKin(h, !lord.Female, Math.Max(ComeOfAge + 2, lord.Age + KRng.Next(-8, 4)));
            Wed(lord, spouse);
        }
        if (spouse != null)
        {
            int eldest = Math.Min(lord.Age, spouse.Age) - 18;
            int kids = eldest <= 0 ? 0 : KRng.Next(5);
            for (int i = 0; i < kids; i++)
            {
                var child = NewKin(h, KRng.Chance(0.5f), KRng.Next(0, eldest + 1));
                Parent(child, lord, spouse);
            }
        }
        if (KRng.Chance(0.3f))
        {
            NewKin(h, KRng.Chance(0.35f), Math.Max(ComeOfAge, lord.Age + KRng.Next(-10, 6)));
        }
    }

    /// Saves from before houses: every lord gets a house and a family (only once, when the save has none).
    void EnsureHouses()
    {
        if (S.Houses.Count > 0) return;
        foreach (var lord in S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction >= 0 && c.Order == "").ToList())
        {
            lord.Female = Names.Get().LadiesOf(lord.Culture).Contains(FirstOf(lord));
            SeedFamily(lord, FoundHouse(lord));
        }
    }

    /// An unlanded grown member of a realm's houses who could be given a banner: younger sons first, then daughters.
    Character? SpareKin(int faction) => S.Characters
        .Where(c => c.Kind == "kin" && !c.Dead && c.Faction == faction && c.Age >= ComeOfAge + 2 && c.Age < 50 && c.PartyId < 0 && c.House >= 0)
        .OrderBy(c => c.Female).ThenByDescending(c => c.Age).FirstOrDefault();

    /// Who takes up a fallen lord's banner from his house: grown sons (eldest first), then grown daughters, then his brothers,
    /// sisters and other grown kin, then his widow. Null when nobody is left who can ride.
    Character? HouseHeir(Character old)
    {
        if (HouseOf(old) is not House h) return null;
        bool Can(Character c) => !c.Dead && c.Kind == "kin" && c.PartyId < 0 && c.Age >= ComeOfAge;
        var kids = old.Children.Select(CharacterById).OfType<Character>().Where(Can).ToList();
        return kids.Where(c => !c.Female).OrderByDescending(c => c.Age).FirstOrDefault()
            ?? kids.Where(c => c.Female).OrderByDescending(c => c.Age).FirstOrDefault()
            ?? Members(h).Where(c => Can(c) && c.Id != old.Spouse && c.Age < 60).OrderBy(c => c.Female).ThenByDescending(c => c.Age).FirstOrDefault()
            ?? (CharacterById(old.Spouse) is Character w && Can(w) && w.House == h.Id ? w : null);
    }

    /// A member of a house takes a party's banner: he is a lord now.
    void Raise(Character c, Party p)
    {
        c.Kind = "lord";
        c.PartyId = p.Id;
        c.Faction = p.Faction;
        p.CharId = c.Id;
        Retitle(c);
        p.Name = c.Name;
    }

    /// The houses' year (with the captain's birthday): everyone ages, the old may die, couples have children, the unwed marry,
    /// houses with nobody left die out.
    internal void HousesYearly()
    {
        foreach (var c in S.Characters.Where(c => !c.Dead && c.Kind != "captain").ToList())
        {
            c.Age++;
            if (c.Age == ComeOfAge) Retitle(c);
        }
        // Old age takes lords and kin (knights and your own people are spared it).
        foreach (var c in S.Characters.Where(c => !c.Dead && c.Kind is "lord" or "kin" && c.House >= 0 && c.Age > OldDeath).ToList())
            if (KRng.Chance((c.Age - OldDeath) * 0.03f)) DiesOld(c);
        Births();
        Marriages();
        foreach (var h in S.Houses.Where(h => !h.Extinct))
            if (!S.Characters.Any(c => c.House == h.Id && !c.Dead))
            {
                h.Extinct = true;
                Log($"{h.Name} dies out.");
            }
    }

    void DiesOld(Character c)
    {
        if (c.Kind == "lord" && PartyById(c.PartyId) is Party p) { SlayLord(p, false, "dies of old age"); return; }
        c.Dead = true;
        c.PartyId = -1;
    }

    void Births()
    {
        foreach (var mother in S.Characters.Where(c => !c.Dead && c.Female && c.Spouse >= 0 && c.Age is >= 17 and <= 42).ToList())
        {
            if (CharacterById(mother.Spouse) is not Character father || father.Dead) continue;
            var h = HouseOf(father) ?? HouseOf(mother);
            if (h == null || mother.Children.Count >= 5 || Members(h).Count() >= 8) continue;
            if (!KRng.Chance(0.25f)) continue;
            var child = NewKin(h, KRng.Chance(0.5f), 0);
            Parent(child, father, mother);
        }
    }

    /// The unwed of the realm's houses marry each other: same realm, another house, ages close. A wife joins her husband's
    /// house unless she leads a banner of her own (then he joins hers, or each keeps their own when both lead).
    void Marriages()
    {
        var unwed = S.Characters.Where(c => !c.Dead && c.Kind is "lord" or "kin" && c.House >= 0 && c.Spouse < 0 && c.Age is >= 18 and <= 45).ToList();
        foreach (var a in unwed.Where(c => !c.Female))
        {
            if (a.Spouse >= 0 || !KRng.Chance(0.2f)) continue;
            var b = unwed.Where(c => c.Female && c.Spouse < 0 && c.Faction == a.Faction && c.House != a.House && Math.Abs(c.Age - a.Age) <= 12)
                .OrderBy(c => Math.Abs(c.Age - a.Age)).FirstOrDefault();
            if (b == null) continue;
            Wed(a, b);
            // A match between houses ends a feud between them and warms the two families (LD2).
            if (HouseOf(a) is House ha && HouseOf(b) is House hb) { ha.Feuds.Remove(hb.Id); hb.Feuds.Remove(ha.Id); }
            Remember(a, b, "married into my house", 10); Remember(b, a, "married into my house", 10);
            bool aLeads = a.PartyId >= 0, bLeads = b.PartyId >= 0;
            if (!bLeads) b.House = a.House;
            else if (!aLeads) a.House = b.House;
            Retitle(a); Retitle(b);
            if (PartyById(a.PartyId) is Party pa) pa.Name = a.Name;
            if (PartyById(b.PartyId) is Party pb) pb.Name = b.Name;
            if (aLeads || bLeads) Log($"{a.Name} weds {b.Name}.");
        }
    }
}
