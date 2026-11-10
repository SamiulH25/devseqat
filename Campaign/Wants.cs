namespace Ironfield;

/// Campaign: lords' wants (docs/archive/LORDS.md LD4). Each lord wants one thing at a time, from his nature and his lot: a fief, revenge
/// on a house, peace, the marshal's staff, wealth, a match for his daughter, glory, or a monster gone from his land. You learn
/// it by asking; helping it is the surest way to win him, and a promise you make and break is remembered. Wants drive his AI
/// (revenge picks his prey, peace weighs on his realm's war, a fief he wants is asked for at the council).
public sealed partial class Campaign
{
    public const int WantDays = 15, PromiseDays = 40;

    /// The want in words, as he would tell it.
    public string WantText(Character ch) => ch.Want switch
    {
        "fief" => "I want land of my own.",
        "revenge" => $"I want {HouseById(ch.WantOn)?.Name ?? "my enemies"} to pay for my kin.",
        "peace" => "I want this war to end.",
        "marshal" => "I want the marshal's staff.",
        "wealth" => "I want gold. A lord cannot live on honour.",
        "match" => $"I want a good match for {CharacterById(ch.WantOn)?.Name ?? "my daughter"}.",
        "glory" => "I want a name in the songs: a great battle won.",
        "monster" => $"I want {PartyById(ch.WantOn)?.Name ?? "the beasts"} gone from our land.",
        _ => "I want for nothing.",
    };

    /// Whether his want has come true (or no longer makes sense).
    bool WantDone(Character ch) => ch.Want switch
    {
        "fief" => SeatsOf(ch) > 0,
        "revenge" => HouseById(ch.WantOn) is not House h || h.Extinct || !(HouseOf(ch)?.Feuds.Contains(h.Id) ?? false),
        "peace" => !Enumerable.Range(0, S.Factions.Count).Any(g => FactionsAtWar(ch.Faction, g)),
        "marshal" => MarshalOf(ch.Faction) == ch,
        "match" => CharacterById(ch.WantOn) is not { Dead: false, Spouse: < 0 },
        "monster" => PartyById(ch.WantOn) is not { Down: false },
        _ => false,
    };

    /// Every WantDays: wants come true or lapse, new ones form, promises are kept or broken.
    internal void WantsDaily()
    {
        if (Day % WantDays != 0) return;
        foreach (var ch in S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction >= 0).ToList())
        {
            if (ch.Want != "" && WantDone(ch)) WantMet(ch, null);
            else if (ch.Want is "wealth" or "glory" && KRng.Chance(0.25f)) ch.Want = ""; // a passing fancy
            if (ch.Promise != "" && ch.Promise == ch.Want && Day - ch.PromiseDay > PromiseDays)
            {
                Remember(ch, Me, "you broke your word", -15);
                ch.Promise = "";
            }
            if (ch.Want == "") ChooseWant(ch);
        }
    }

    void ChooseWant(Character ch)
    {
        bool Is(string t) => ch.Traits.Contains(t);
        var options = new List<(string Want, int On, float Weight)>();
        if (HouseOf(ch) is House h && h.Feuds.Count > 0) options.Add(("revenge", h.Feuds[0], 3));
        bool atWar = Enumerable.Range(0, S.Factions.Count).Any(g => FactionsAtWar(ch.Faction, g));
        if (atWar && (Is("Cautious") || Exhaustion(ch.Faction) > 40)) options.Add(("peace", -1, Is("Cautious") ? 2 : 1));
        if (SeatsOf(ch) == 0) options.Add(("fief", -1, Is("Ambitious") || Is("Greedy") ? 2 : 0.6f));
        if (Is("Ambitious") && MarshalOf(ch.Faction) != ch) options.Add(("marshal", -1, 1));
        if (Is("Greedy")) options.Add(("wealth", -1, 1.5f));
        if (ch.Children.Select(CharacterById).FirstOrDefault(k => k is { Dead: false, Female: true, Spouse: < 0 } && k.Age >= 16) is Character d) options.Add(("match", d.Id, 1.2f));
        if (atWar && (Is("Reckless") || Is("Ambitious"))) options.Add(("glory", -1, 1));
        if (Holdouts.FirstOrDefault(m => !m.Down && Map.ProvinceAt(m.Pos) is Province pr && S.Provinces[pr.Id].Faction == ch.Faction) is Party lair) options.Add(("monster", lair.Id, 1));
        if (options.Count == 0) return;
        float total = options.Sum(o => o.Weight), roll = KRng.Float() * total;
        foreach (var o in options)
        {
            roll -= o.Weight;
            if (roll > 0) continue;
            ch.Want = o.Want; ch.WantOn = o.On; ch.WantKnown = false; ch.Promise = "";
            return;
        }
    }

    /// His want came true. If you brought it about (`byYou`), or had promised to, he remembers it.
    void WantMet(Character ch, string? byYou)
    {
        if (byYou != null) Remember(ch, Me, byYou, 25);
        else if (ch.Promise == ch.Want && ch.Promise != "") Remember(ch, Me, "you kept your word", 10);
        ch.Want = ""; ch.WantOn = -1; ch.Promise = ""; ch.WantKnown = false;
    }

    /// How hard a realm's lords press for peace (LD4): 4 for each lord who wants it, to its weariness.
    float PeaceWanted(int f) => 4 * S.Characters.Count(c => c.Kind == "lord" && !c.Dead && c.Faction == f && c.Want == "peace");

    /// A lord who wants revenge on q's house hunts q first.
    bool Revenge(Party p, Party q) => CharacterOf(p) is { Want: "revenge" } ch && CharacterOf(q) is Character t && t.House == ch.WantOn;

    /// You did something lords may have wanted: credit each lord whose want it was.
    void HelpedWant(string want, Func<Character, bool> fits, string what)
    {
        foreach (var ch in S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Want == want && fits(c)).ToList()) WantMet(ch, what);
    }
}
