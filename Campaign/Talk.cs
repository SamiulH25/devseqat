using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// A subject you can raise with a lord (docs/archive/LORDS.md LD3): its group (Ask, Request, Persuade, Threaten, Gift, Insult, Marriage).
public sealed record TalkTopic(string Id, string Group, string Label, string Text);

/// One line a lord may say (Data/talk.json): for a topic ("greet" for the greeting), a mood towards you ("warm", "even",
/// "cold", "hostile" or "" for any), a temper (a trait, or "" for any), an outcome ("yes", "no" or "" for any), and "war"
/// (true: only when his realm is at war with yours). {name}, {house}, {realm}, {you} and {other} are filled in.
public sealed class TalkLine
{
    public string Topic = "greet", Mood = "", Temper = "", Outcome = "", Text = "";
    public bool War;
}

/// The talk lines from Data/talk.json, loaded once.
public static class TalkLines
{
    public const string File = "Data/talk.json";
    static List<TalkLine>? lines;
    public static IReadOnlyList<TalkLine> All
    {
        get
        {
            if (lines != null) return lines;
            foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var p = Path.Combine(dir, File);
                if (!System.IO.File.Exists(p)) continue;
                var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
                using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(p), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                return lines = JsonSerializer.Deserialize<List<TalkLine>>(doc.RootElement.GetProperty("lines").GetRawText(), opts)!;
            }
            return lines = new List<TalkLine>();
        }
    }
}

/// Campaign: talking to lords (docs/archive/LORDS.md LD3). Meet a lord on the road (ride up to a friendly one, or TALK from an
/// encounter), find him in a hall, or speak to your prisoner; then raise topics. Asking is free; requests and persuasion are
/// checks of Charm, his opinion of you and the arguments you bring, against the size of the ask. Lines come from Data/talk.json.
public sealed partial class Campaign
{
    public static readonly TalkTopic[] Topics =
    {
        new("news", "ASK", "NEWS OF THE WAR", "What his realm is fighting, and where its armies march."),
        new("lords", "ASK", "ABOUT THE LORDS", "Whom he counts a friend, a rival or a blood enemy."),
        new("wants", "ASK", "WHAT HE WANTS", "What he is after. Help him get it and he will not forget."),
        new("work", "REQUEST", "ASK FOR WORK", "A task for crowns and his goodwill."),
        new("ride", "REQUEST", "RIDE WITH ME", "He follows you for three days."),
        new("council", "PERSUADE", "BACK ME IN COUNCIL", "He speaks for you when the realm's council sits."),
        new("peace", "PERSUADE", "MAKE PEACE", "His realm and yours stop fighting. A lord presses his king; a ruler decides."),
        new("defect", "PERSUADE", "SWEAR TO MY REALM", "He leaves his king and swears to yours, with his men."),
        new("demand", "THREATEN", "YIELD OR FIGHT", "He pays you to ride on, or he fights."),
        new("gift", "GIFT", "A GIFT OF GOLD", "100 crowns. The greedy like it best."),
        new("gift_horse", "GIFT", "A GIFT OF A HORSE", "One of your horses. The bold like it best."),
        new("insult", "INSULT", "INSULT HIM", "He will hate you for it. The reckless may make it a feud."),
        new("court", "MARRIAGE", "COURT HER", "Pay court three times, on different days, before you ask for her hand."),
        new("hand", "MARRIAGE", "ASK FOR HER HAND", "Ask the head of her house for her hand."),
    };

    /// Arguments you can bring to a request or a persuasion (once each per ask): gold (100 crowns), honour, glory, faith,
    /// fear, and a promise to help what he wants (once you know it).
    public static readonly string[] Arguments = { "gold", "honour", "glory", "faith", "fear", "promise" };

    public static TalkTopic TopicOf(string id) => Topics.First(t => t.Id == id);

    /// Problems in Data/talk.json (SelfTest): unknown topics, moods, tempers or outcomes; a mood with no greeting; a topic
    /// with no line for a yes (and, when it is checked, for a no).
    public static IEnumerable<string> ValidateTalk()
    {
        var lines = TalkLines.All;
        if (lines.Count == 0) { yield return "no talk lines"; yield break; }
        foreach (var l in lines)
        {
            if (l.Topic != "greet" && !Topics.Any(t => t.Id == l.Topic)) yield return $"unknown topic {l.Topic}";
            if (l.Mood is not ("" or "warm" or "even" or "cold" or "hostile")) yield return $"unknown mood {l.Mood}";
            if (l.Temper != "" && !LordTraits.Contains(l.Temper)) yield return $"unknown temper {l.Temper}";
            if (l.Outcome is not ("" or "yes" or "no")) yield return $"unknown outcome {l.Outcome}";
            if (l.Text.Trim() == "") yield return $"empty line for {l.Topic}";
        }
        foreach (var mood in new[] { "warm", "even", "cold", "hostile" })
            if (!lines.Any(l => l.Topic == "greet" && l.Mood == mood && l.Temper == "" && !l.War)) yield return $"no plain greeting for mood {mood}";
        foreach (var t in Topics)
        {
            if (!lines.Any(l => l.Topic == t.Id && l.Outcome is "yes" or "" && l.Mood == "" && l.Temper == "" && !l.War)) yield return $"no plain yes line for {t.Id}";
            if (Checked(t.Id) && !lines.Any(l => l.Topic == t.Id && l.Outcome is "no" or "" && l.Mood == "" && l.Temper == "" && !l.War)) yield return $"no plain no line for {t.Id}";
        }
    }
    static bool Checked(string topic) => topic is "ride" or "council" or "peace" or "defect" or "demand" or "hand";

    /// The party a character leads, if it is on the map.
    Party? PartyOfChar(Character ch) => ch.PartyId >= 0 ? PartyById(ch.PartyId) : null;

    /// Why you cannot talk to him now (null: you can): he is near you on the map, the lord you met, your prisoner, or in
    /// the hall of the town you are in.
    public string? CannotTalk(Character ch)
    {
        if (ch.Dead) return "HE IS DEAD";
        if (ch.Kind is not ("lord" or "kin")) return "YOU CANNOT TALK TO HIM HERE";
        var p = PartyOfChar(ch);
        if (p != null && Encounter == p) return null;
        if (p != null && p.Captive) return null; // your prisoner
        if (p != null && !p.Down && Vector2.Distance(p.Pos, Player.Pos) <= TalkReach) return null;
        if (InHall(ch) is Settlement) return null;
        return p is { Down: true } ? "HIS MEN ARE SCATTERED; NOBODY KNOWS WHERE HE IS" : "RIDE UP TO HIM, OR FIND HIM IN HIS HALL";
    }
    public const float TalkReach = 8;

    /// The town or castle whose hall he is in while you are there too (LD5): the lord resting at his seat, or kin at their house's seat.
    public Settlement? InHall(Character ch)
    {
        var here = SettlementAt(Player.Pos, 4);
        if (here == null || here.Kind == SettlementKind.Village || Hostile(Player, GarrisonParty(here))) return null;
        return HallFolk(here).Contains(ch) ? here : null;
    }

    /// Who is in a settlement's hall (LD5): lords whose parties stand at it, and the kin of the house whose seat it is.
    public List<Character> HallFolk(Settlement s)
    {
        var folk = new List<Character>();
        foreach (var p in S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && !p.Captive && Vector2.Distance(p.Pos, s.Pos) < 4))
            if (CharacterOf(p) is { Kind: "lord" } l) folk.Add(l);
        if (s.Owner != Player.Id && HolderOf(s) is Character holder && HouseOf(holder) is House h)
            folk.AddRange(Members(h).Where(m => m.Kind == "kin" && !folk.Contains(m)));
        return folk.OrderBy(c => c.Kind == "kin").ThenByDescending(c => c.Age).ToList();
    }

    static string MoodWord(int mood) => mood switch { 0 => "warm", 1 => "even", 2 => "cold", _ => "hostile" };

    /// A line from Data/talk.json for this topic and outcome that fits him; the same each day (no dice).
    public string Line(Character ch, string topic, string outcome = "", string other = "")
    {
        string mood = MoodWord(PortraitPainter.MoodOf(OpinionOfYou(ch)));
        bool war = Captain.Contract >= 0 && FactionsAtWar(ch.Faction, Captain.Contract);
        var fits = TalkLines.All.Where(l => l.Topic == topic && (l.Mood == "" || l.Mood == mood) && (l.Temper == "" || ch.Traits.Contains(l.Temper))
            && (l.Outcome == "" || l.Outcome == outcome) && (!l.War || war)).ToList();
        // The most fitting lines first: those naming his temper, his mood or the war.
        int Score(TalkLine l) => (l.Temper != "" ? 2 : 0) + (l.Mood != "" ? 1 : 0) + (l.War ? 2 : 0);
        if (fits.Count == 0) return "";
        int best = fits.Max(Score);
        var top = fits.Where(l => Score(l) == best).ToList();
        var line = top[(int)(Ease.Hash(ch.Id * 131 + Day * 7 + topic.Length) * top.Count) % top.Count];
        return Fill(line.Text, ch, other);
    }

    string Fill(string text, Character ch, string other) => text
        .Replace("{name}", ch.Name).Replace("{house}", HouseOf(ch)?.Name ?? "my house")
        .Replace("{realm}", ch.Faction >= 0 && ch.Faction < FactionNames.Length ? FactionNames[ch.Faction] : "my people")
        .Replace("{you}", Captain.Contract >= 0 && this.Rank >= Ironfield.Rank.Lord ? "my lord" : "captain").Replace("{other}", other);

    public string Greeting(Character ch) => Line(ch, "greet");

    /// The topics that make sense with him now (what he can be asked; whether you can afford it is CannotAsk).
    public IEnumerable<TalkTopic> TopicsFor(Character ch)
    {
        var p = PartyOfChar(ch);
        bool hostile = p != null && Hostile(Player, p);
        foreach (var t in Topics)
        {
            bool fits = t.Id switch
            {
                "work" or "ride" or "council" => !hostile && ch.Kind == "lord",
                "peace" => Captain.Contract >= 0 && FactionsAtWar(ch.Faction, Captain.Contract),
                "defect" => ch.Kind == "lord" && Captain.Contract >= 0 && ch.Faction >= 0 && ch.Faction != Captain.Contract && !hostile,
                "demand" => hostile && Encounter == p,
                "court" or "hand" => Suitor(ch),
                "lords" or "wants" or "insult" or "gift" or "gift_horse" or "news" => true,
                _ => false,
            };
            if (fits) yield return t;
        }
    }

    /// Why you cannot raise this topic now (null: you can).
    public string? CannotAsk(Character ch, string topic)
    {
        if (CannotTalk(ch) is string why) return why;
        int since = ch.Asked.TryGetValue(topic, out int d) ? Day - d : 999;
        int wait = topic switch { "news" or "lords" or "wants" => 3, "gift" or "gift_horse" => 7, "court" => 1, "insult" => 30, _ => 14 };
        if (since < wait) return $"YOU ASKED HIM THAT {(since == 0 ? "TODAY" : since == 1 ? "YESTERDAY" : $"{since} DAYS AGO")}";
        switch (topic)
        {
            case "gift" when S.Gold < 100: return "YOU NEED 100 CROWNS";
            case "gift_horse" when Player.Horses < 1: return "YOU HAVE NO SPARE HORSE";
            case "work" when S.Contracts.Count >= 3: return "YOU HAVE ENOUGH WORK";
            case "work" when OpinionOfYou(ch) < -10: return "HE WILL NOT HIRE YOU";
            case "ride" when PartyOfChar(ch) is not Party pp || OnDuty(pp): return "HE HAS DUTIES OF HIS OWN";
            case "council" when ch.Faction != Captain.Contract || !Captain.Fealty: return "ONLY A LORD OF YOUR OWN REALM SITS IN YOUR COUNCIL";
            case "defect" when ch.Traits.Contains("Loyal"): return "HE IS LOYAL TO HIS KING";
            case "defect" when RulerOf(ch.Faction) == ch: return "HE IS THE KING";
            case "hand" when ch.Courted < 3: return $"COURT HER FIRST ({ch.Courted}/3)";
            case "hand" when HeadOf(ch) is null: return "NOBODY CAN GIVE HER HAND";
            case "court" when ch.CourtedDay == Day: return "YOU CALLED ON HER TODAY";
        }
        return null;
    }

    /// The chance a checked topic succeeds with these arguments (1 for topics that are not checked).
    public float Odds(Character ch, string topic, IReadOnlyCollection<string>? args = null)
    {
        if (!Checked(topic)) return 1;
        float op = OpinionOfYou(ch);
        float odds = topic switch
        {
            "ride" => 0.45f, "council" => 0.45f, "peace" => RulerOf(ch.Faction) == ch ? 0.12f : 0.25f, "defect" => 0.06f, "hand" => 0.35f,
            "demand" => DemandOdds(ch),
            _ => 0.5f,
        };
        odds += op / 160f + Skill("Charm") / 300f + (Has("SilverTongue") ? 0.05f : 0);
        if (topic == "peace" && WarBetween(ch.Faction, Captain.Contract) is War w) odds += ExhaustionOf(w, ch.Faction) / 150f;
        if (topic == "defect" && RulerOf(ch.Faction) is Character king) odds += -OpinionOf(ch, king) / 200f;
        foreach (var a in args ?? Array.Empty<string>()) odds += ArgumentWeight(ch, topic, a);
        return Math.Clamp(odds, 0.02f, 0.95f);
    }

    float DemandOdds(Character ch)
    {
        if (PartyOfChar(ch) is not Party p) return 0;
        float ratio = Rules.Strength(Player) / MathF.Max(1, SideStrength(p));
        return (ratio - 1) * 0.35f + (ch.Traits.Contains("Cautious") ? 0.15f : 0) - (ch.Traits.Contains("Reckless") ? 0.2f : 0) + Captain.Renown / 4000f;
    }

    /// How much an argument moves him: it depends on his nature (and, for a threat, on your strength).
    public float ArgumentWeight(Character ch, string topic, string arg)
    {
        bool Is(string t) => ch.Traits.Contains(t);
        return arg switch
        {
            "gold" => S.Gold < 100 ? 0 : Is("Greedy") ? 0.2f : Is("Generous") || Is("Honourable") ? -0.03f : 0.07f,
            "honour" => Is("Honourable") ? 0.18f : Is("Loyal") ? 0.06f : Is("Cruel") || Is("Fickle") ? -0.1f : 0.02f,
            "glory" => Is("Ambitious") || Is("Reckless") ? 0.18f : Is("Cautious") ? -0.1f : 0.03f,
            "faith" => Is("Pious") ? 0.2f : -0.03f,
            "fear" => (Is("Cautious") || Is("Fickle") ? 0.12f : Is("Honourable") || Is("Reckless") ? -0.15f : 0)
                + (PartyOfChar(ch) is Party p ? Math.Clamp(Rules.Strength(Player) / MathF.Max(1, Rules.Strength(p)) - 1, -0.5f, 1) * 0.1f : 0),
            "promise" => ch.WantKnown && ch.Want != "" && ch.Promise == "" ? 0.2f : 0,
            _ => 0,
        };
    }

    /// Raise a topic. Returns whether it went your way and what he said. Arguments only count for checked topics;
    /// gold given as an argument is spent whatever the answer.
    public (bool Ok, string Line) Say(Character ch, string topic, IReadOnlyCollection<string>? args = null)
    {
        if (CannotAsk(ch, topic) is string why) return (false, why);
        args ??= Array.Empty<string>();
        ch.Asked[topic] = Day;
        if (Checked(topic))
        {
            float odds = Odds(ch, topic, args);
            if (args.Contains("gold") && S.Gold >= 100) S.Gold -= 100;
            if (args.Contains("promise") && ch.WantKnown && ch.Want != "" && ch.Promise == "") { ch.Promise = ch.Want; ch.PromiseDay = Day; }
            bool ok = rng.Chance(odds);
            if (!ok)
            {
                Remember(ch, Me, topic == "demand" ? "you threatened me" : "you pressed me too hard", topic == "demand" ? -10 : -4);
                FailedAsk(ch, topic);
                return (false, Line(ch, topic, "no"));
            }
            Practise("Charm", 1 + (1 - odds) * 3);
        }
        string extra = Grant(ch, topic);
        string line = Line(ch, topic, "yes", extra);
        return (true, extra != "" && !line.Contains(extra) ? (line + " " + extra).Trim() : line);
    }

    /// What a refusal costs beyond his memory of it: a refused hand loses a courting.
    void FailedAsk(Character ch, string topic)
    {
        if (topic == "hand") ch.Courted = Math.Max(0, ch.Courted - 1);
    }

    /// What a granted topic does; the words it adds to his line ("" when the line says it all).
    string Grant(Character ch, string topic)
    {
        var p = PartyOfChar(ch);
        switch (topic)
        {
            case "news": return WarNews(ch);
            case "lords": return LordsNews(ch);
            case "wants": ch.WantKnown = true; return WantText(ch);
            case "work": return GiveQuest(ch) ?? "";
            case "ride":
                if (p == null) return "";
                p.Task = "follow"; p.TaskUntil = (float)S.Hours + 72; p.ChaseId = -1;
                Remember(ch, Me, "we rode together", 3);
                return "";
            case "council":
                Remember(ch, Me, "promised to back you in council", 10);
                return "";
            case "peace":
                if (RulerOf(ch.Faction) == ch) return AcceptPeace(ch.Faction) ?? "";
                if (WarBetween(ch.Faction, Captain.Contract) is War w) AddExhaustion(w, ch.Faction, 15);
                Log($"{ch.Name} will press {His(ch)} king for peace with the {FactionNames[Captain.Contract]}.");
                return "";
            case "defect":
                int was = ch.Faction;
                Defect(ch, Captain.Contract);
                Log($"{ch.Name} renounces the {FactionNames[was]} and swears to the {FactionNames[Captain.Contract]}.");
                Remember(ch, Me, "you showed me a better king", 10);
                return "";
            case "demand":
                if (p == null) return "";
                int pay = (int)MathF.Min(p.Gold, 60 + Rules.Strength(p) / 4);
                p.Gold -= pay; S.Gold += pay;
                p.Cooldown = 6; Player.Cooldown = 2;
                Encounter = null; Player.ChaseId = -1;
                return $"He pays you {pay} crowns.";
            case "gift":
                S.Gold -= 100;
                Remember(ch, Me, "a gift of gold", ch.Traits.Contains("Greedy") ? 12 : 7);
                if (ch.Want == "wealth") WantMet(ch, "you filled my purse");
                return "";
            case "gift_horse":
                Player.Horses--;
                Remember(ch, Me, "the gift of a horse", ch.Traits.Contains("Reckless") || ch.Traits.Contains("Ambitious") ? 12 : 6);
                return "";
            case "insult":
                Remember(ch, Me, "you insulted me", ch.Traits.Contains("Reckless") ? -30 : -20);
                if (ch.Traits.Contains("Reckless") && HouseOf(ch) is House h) Log($"{ch.Name} swears {h.Name} will not forget it.");
                return "";
            case "court": return Court(ch);
            case "hand": return MarryInto(ch);
        }
        return "";
    }

    /// News of the war from his side: his realm's wars and armies.
    string WarNews(Character ch)
    {
        if (ch.Faction < 0) return "";
        var foes = Enumerable.Range(0, S.Factions.Count).Where(g => FactionsAtWar(ch.Faction, g)).Select(g => FactionNames[g]).ToList();
        var armies = S.Armies.Where(a => a.Faction == ch.Faction).Select(a => (a, t: SettlementById(a.Target))).Where(x => x.t != null)
            .Select(x => (x.a.Aim == "relieve" ? "relieving " : "marching on ") + x.t!.Name).ToList();
        string war = foes.Count == 0 ? "We are at peace." : "We are at war with the " + string.Join(" and the ", foes) + ".";
        return armies.Count == 0 ? war : $"{war} Our armies are {string.Join("; ", armies)}.";
    }

    /// What he thinks of the other lords: his best friend, his worst rival, his house's feuds.
    string LordsNews(Character ch)
    {
        var others = S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c != ch).ToList();
        if (others.Count == 0) return "";
        var friend = others.Where(c => BondOf(ch, c) == "friend").OrderByDescending(c => OpinionOf(ch, c)).FirstOrDefault();
        var rival = others.Where(c => BondOf(ch, c) is "rival" or "feud").OrderBy(c => OpinionOf(ch, c)).FirstOrDefault();
        var parts = new List<string>();
        if (friend != null) parts.Add($"{friend.Name} is a friend.");
        if (rival != null) parts.Add(BondOf(ch, rival) == "feud" ? $"{HouseOf(rival)?.Name ?? rival.Name} owes my house blood." : $"I have no love for {rival.Name}.");
        return parts.Count == 0 ? "I keep my own counsel about the others." : string.Join(" ", parts);
    }

    // ---------- halls (LD5) ----------

    public const int HallFeastDays = 30;

    /// The lords who would come to a feast in your hall: up to five of your realm's (or friendly) lords in reach and free.
    public List<Character> FeastGuests(Settlement s) => S.Parties
        .Where(p => p.Kind == PartyKind.Lord && !p.Down && !p.Captive && !Hostile(Player, p) && !OnDuty(p) && Vector2.Distance(p.Pos, s.Pos) < 120 * Map.Spread)
        .Select(CharacterOf).OfType<Character>().Where(l => OpinionOfYou(l) > -20)
        .OrderByDescending(l => l.Faction == Captain.Contract).ThenByDescending(OpinionOfYou).Take(5).ToList();

    public int HallFeastPrice(Settlement s) => 80 + 40 * FeastGuests(s).Count;

    /// Why you cannot feast lords in this hall now (null: you can).
    public string? CannotHoldFeast(Settlement s)
    {
        if (s.Owner != Player.Id || s.Kind == SettlementKind.Village) return "ONLY IN A HALL OF YOUR OWN";
        if (S.Flags.TryGetValue("hall_feast", out float last) && S.Hours - last < HallFeastDays * 24) return $"YOU FEASTED THE LORDS {(int)((S.Hours - last) / 24)} DAYS AGO";
        if (FeastGuests(s).Count == 0) return "NO LORD IS NEAR ENOUGH TO COME";
        if (S.Gold < HallFeastPrice(s)) return $"A FEAST COSTS {HallFeastPrice(s)} CROWNS";
        return null;
    }

    /// Feast the lords in your hall: each thinks better of you, guests who sit together warm to each other, and rivals
    /// at one table may quarrel. Returns why not.
    public string? HoldFeast(Settlement s)
    {
        if (CannotHoldFeast(s) is string why) return why;
        var guests = FeastGuests(s);
        S.Gold -= HallFeastPrice(s);
        S.Flags["hall_feast"] = (float)S.Hours;
        foreach (var g in guests) Remember(g, Me, "feasted in your hall", 8);
        foreach (var a in guests)
            foreach (var b in guests)
            {
                if (a.Id >= b.Id) continue;
                if (BondOf(a, b) is "rival" or "feud" && KRng.Chance(0.5f))
                {
                    Remember(a, b, "quarrelled at the feast", -10); Remember(b, a, "quarrelled at the feast", -10);
                    Log($"{a.Name} and {b.Name} quarrel at your table.");
                }
                else { Remember(a, b, "feasted together", 3); Remember(b, a, "feasted together", 3); }
            }
        Log($"You feast {guests.Count} lords in {s.Name}.");
        Practise("Charm", guests.Count);
        AudioEngine.Current?.Play(Cue.FeastToast, 0.85f);
        return null;
    }

    // ---------- marriage (LD6) ----------

    /// An unwed grown member of a house you could court (a woman for a man and a man for a woman; the captain is a man
    /// unless the creator said otherwise), when you are unwed.
    public bool Suitor(Character ch) => !ch.Dead && ch.Kind is "kin" or "lord" && ch.Spouse < 0 && ch.Age >= 18 && ch.Age <= 45 && ch.House >= 0
        && ch.Female != Captain.Female && Me.Spouse < 0;

    /// Who gives her hand: her father if he lives, else the head of her house (its lord).
    public Character? HeadOf(Character ch) => CharacterById(ch.Father) is { Dead: false } f ? f
        : HouseOf(ch) is House h ? Members(h).FirstOrDefault(m => m.Kind == "lord" && m != ch) ?? (ch.Kind == "lord" ? ch : null) : null;

    string Court(Character ch)
    {
        float odds = 0.45f + OpinionOfYou(ch) / 150f + Skill("Charm") / 250f + Captain.Renown / 3000f;
        ch.CourtedDay = Day;
        if (!rng.Chance(Math.Clamp(odds, 0.1f, 0.95f))) return "She is polite, and no more.";
        ch.Courted = Math.Min(3, ch.Courted + 1);
        Remember(ch, Me, "you courted me", 4);
        return ch.Courted >= 3 ? "She smiles: you may speak to her house." : "";
    }

    /// The match is made: you marry into her house (every member is kin to you), with a dowry and the realm's regard.
    string MarryInto(Character ch)
    {
        var head = HeadOf(ch);
        Wed(Me, ch);
        var h = HouseOf(ch)!;
        int dowry = 150 + (int)h.Prestige * 5;
        S.Gold += dowry;
        foreach (var m in Members(h)) Remember(m, Me, "kin by marriage", 20);
        foreach (var l in S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == ch.Faction && c.House != h.Id)) Remember(l, Me, "married well", 3);
        if (head != null && head != ch) Remember(head, Me, "married my daughter", 5);
        if (head is { Want: "match" } && head.WantOn == ch.Id) WantMet(head, "you married my daughter");
        h.Prestige += 5;
        Log($"You wed {ch.Name} of {h.Name}. The dowry is {dowry} crowns.");
        Deed($"wed {ch.Name} of {h.Name}");
        AudioEngine.Current?.PlayStinger("sting_marriage");
        return $"The dowry is {dowry} crowns.";
    }
}
