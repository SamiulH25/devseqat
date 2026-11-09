using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// One event as Data/events.json describes it (CAMPAIGN_OVERHAUL.md 8).
public sealed class EventDef
{
    public string Id = "", Trigger = "", Title = "", Text = "";
    /// The picture (S6): "camp", "town", "court", "burning" or "site_<kind>"; empty = chosen from the id.
    public string Scene = "";
    public int Cooldown = 30;
    public List<ChoiceDef> Choices = new();
}

public sealed class ChoiceDef
{
    public string Label = "", Need = "";
    public List<string> Effects = new();
}

/// An event waiting for your answer, with who and where it is about (ids; -1 = none).
public sealed class PendingEvent
{
    public string Id = "";
    public int Settlement = -1, Lord = -1, Party = -1, Company = -1, Grave = -1, Site = -1, Soldier = -1;
}

/// Events, lord quests and named threats (step F8).
public sealed partial class Campaign
{
    public const string EventsFile = "Data/events.json";
    public const int BrigandKingMen = 40, EventGapMin = 3, EventGapMax = 6;

    /// Triggers the data may name: each finds what the event is about, or fails.
    public static readonly string[] Triggers =
    {
        "fief_unrest", "fief_poor", "fief_rich", "lord_friend", "lord_hates_liege", "big_band", "veterans", "hungry_winter",
        "unpaid", "prisoners", "war_weary", "in_town", "brigand_king", "pretender", "graves",
        "peace_offer", "site_ruin", "site_shrine", "site_stones", "site_battlefield", "site_mine", "site_hermit", "site_watchtower", "site_spring", "site_mill",
        "start", "crown", "fallen", "spring", "summer", "autumn", "winter", "forest", "hills", "river", "near_site", "after_battle", "companion", "culture", "night", "dark_omens",
    };
    /// Days after a "flag:name" effect before the "flag:name" event follows.
    public const int FlagDays = 7;
    /// Triggers that only fire when the world forces them (a brigand king rises, a pretender appears).
    internal static bool Forced(string trigger) => trigger is "brigand_king" or "pretender" or "graves" or "peace_offer" or "start" or "crown" or "fallen" || trigger.StartsWith("site_");
    static readonly string[] Verbs =
    {
        "gold", "renown", "food", "horses", "opinion", "opinion_liege", "opinion_realm", "unrest", "prosperity", "tax", "cohesion", "xp",
        "exhaustion", "gamble", "quest", "bounty", "revolt", "prisoners_free", "prisoners_recruit", "disband", "bury",
        "spent", "heal", "find", "learn", "men", "search", "omen", "spy", "lists", "sick", "reveal", "flag", "man", "farsight", "peace", "mend", "road_sword", "road_lance", "road_bow", "crown", "calm",
    };
    static readonly string[] NoNumber = { "quest", "bounty", "revolt", "prisoners_free", "prisoners_recruit", "disband", "bury", "search", "omen", "spy", "lists", "reveal", "man", "peace", "mend", "road_sword", "road_lance", "road_bow", "crown" };
    static readonly string[] Holes = { "settlement", "lord", "band", "company", "realm", "liege", "site", "companion", "man" };

    static List<EventDef>? events;
    public static IReadOnlyList<EventDef> Events
    {
        get
        {
            if (events != null) return events;
            foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var p = Path.Combine(dir, EventsFile);
                if (!File.Exists(p)) continue;
                var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
                using var doc = JsonDocument.Parse(File.ReadAllText(p), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                return events = JsonSerializer.Deserialize<List<EventDef>>(doc.RootElement.GetProperty("events").GetRawText(), opts)!;
            }
            return events = new List<EventDef>();
        }
    }

    public static EventDef? EventOf(string id) => Events.FirstOrDefault(e => e.Id == id);

    /// Data checks (11, F8): known triggers and verbs, numbers where a verb needs one, two or three choices, known placeholders.
    public static IEnumerable<string> ValidateEvents()
    {
        if (Events.Count == 0) yield return "no events loaded";
        foreach (var e in Events)
        {
            if (Events.Count(x => x.Id == e.Id) > 1) yield return $"{e.Id}: duplicate id";
            if (!Triggers.Contains(e.Trigger) && !e.Trigger.StartsWith("flag:") && !(e.Trigger.StartsWith("trait:") && EarnedTraits.Contains(e.Trigger[6..])))
                yield return $"{e.Id}: unknown trigger {e.Trigger}";
            if (e.Trigger.StartsWith("flag:") && !Events.Any(x => x.Choices.Any(c => c.Effects.Contains(e.Trigger)))) yield return $"{e.Id}: nothing sets {e.Trigger}";
            if (e.Choices.Count is < 2 or > 3) yield return $"{e.Id}: needs two or three choices";
            foreach (var text in new[] { e.Title, e.Text }.Concat(e.Choices.Select(c => c.Label)))
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\{(\w+)\}"))
                    if (!Holes.Contains(m.Groups[1].Value)) yield return $"{e.Id}: unknown placeholder {m.Value}";
            foreach (var c in e.Choices)
            {
                if (c.Need != "" && !(c.Need.StartsWith("gold:") && float.TryParse(c.Need[5..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)))
                    yield return $"{e.Id}: bad need {c.Need}";
                foreach (var fx in c.Effects)
                {
                    var (verb, n, ok) = ParseEffect(fx);
                    if (!Verbs.Contains(verb)) yield return $"{e.Id}: unknown effect {fx}";
                    else if (!ok) yield return $"{e.Id}: effect {fx} needs a number";
                    else if (verb == "flag" && !Events.Any(x => x.Trigger == fx)) yield return $"{e.Id}: no event follows {fx}";
                }
            }
        }
        // Every verb the code knows is used by some event, so none is left behind untested.
        foreach (var verb in Verbs)
            if (!Events.Any(e => e.Choices.Any(c => c.Effects.Any(fx => ParseEffect(fx).Verb == verb)))) yield return $"no event uses the effect {verb}";
    }

    static (string Verb, float N, bool Ok) ParseEffect(string fx)
    {
        int i = fx.IndexOf(':');
        string verb = i < 0 ? fx : fx[..i];
        if (NoNumber.Contains(verb)) return (verb, 0, i < 0);
        if (verb == "flag") return (verb, 0, i > 0 && fx.Length > i + 1);
        bool ok = i >= 0 && float.TryParse(fx[(i + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n);
        return (verb, ok ? float.Parse(fx[(i + 1)..], System.Globalization.CultureInfo.InvariantCulture) : 0, ok);
    }

    // ---------- finding what an event is about ----------

    /// The subject of a trigger in the world now, or null when it does not hold.
    internal PendingEvent? Find(string trigger)
    {
        var ev = new PendingEvent();
        var seats = Fiefs.Where(t => t.Kind != SettlementKind.Village).ToList();
        switch (trigger)
        {
            case "fief_unrest": return Pick(seats.Where(t => t.Unrest >= 50), t => ev.Settlement = t.Id, ev);
            case "fief_poor": return Pick(seats.Where(t => t.Prosperity < 30), t => ev.Settlement = t.Id, ev);
            case "fief_rich": return Pick(seats.Where(t => t.Prosperity >= 80), t => ev.Settlement = t.Id, ev);
            case "lord_friend":
                return Pick(S.Characters.Where(c => c.Kind == "lord" && !c.Dead && OpinionOfYou(c) >= 35 && PartyById(c.PartyId) is { Captive: false, Down: false }
                    && !FactionsAtWar(c.Faction, Captain.Contract)), c => ev.Lord = c.Id, ev);
            case "lord_hates_liege":
                if (!Captain.Fealty || RulerOf(Captain.Contract) is not Character ruler) return null;
                return Pick(S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == Captain.Contract && c != ruler && OpinionOf(c, ruler) <= -20), c => ev.Lord = c.Id, ev);
            case "big_band":
                return Pick(S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down && p.Men >= Scaled(35) && Vector2.Distance(p.Pos, Player.Pos) < 80 * Map.Spread), p => ev.Party = p.Id, ev);
            case "veterans": return Pick(Player.Companies.Where(c => c.Battles >= 12 && c.Cohesion >= 18), c => ev.Company = c.Id, ev);
            case "hungry_winter": return Winter && Player.Food < 6 ? ev : null;
            case "unpaid": return S.UnpaidWeeks > 0 ? ev : null;
            case "prisoners": return PrisonerCount >= Scaled(25) ? ev : null;
            case "war_weary": return Captain.Contract >= 0 && Exhaustion(Captain.Contract) >= 40 ? ev : null;
            case "spring" or "summer" or "autumn" or "winter": return Season.ToLowerInvariant() == trigger ? ev : null;
            case "dark_omens": return S.Omen >= OmenEvents ? ev : null;
            case "night": return S.Hours % 24 is >= 20 or < 5 && SettlementAt(Player.Pos) == null ? ev : null;
            case "forest" or "hills": return SettlementAt(Player.Pos) == null && Map.ProvinceAt(Player.Pos).Kind.Equals(trigger, StringComparison.OrdinalIgnoreCase) ? ev : null;
            case "river": return SettlementAt(Player.Pos) == null && Map.RiverDist(Player.Pos) < 6 * Map.Spread ? ev : null;
            case "near_site": return Pick(Sites.Where(x => !SiteSeen(x) && Vector2.Distance(x.Pos, Player.Pos) < 40 * Map.Spread), x => ev.Site = x.Id, ev);
            case "after_battle": return S.Hours - S.LastBattleAt < 24 ? Pick(Player.Companies.Where(c => c.Men > 0), c => ev.Company = c.Id, ev) : null;
            case "companion": return Pick(Companions, c => ev.Lord = c.Id, ev);
            case "culture":
                return Pick(S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.Culture != Me.Culture && v.Culture != "common" && !Refuses(v)
                    && Vector2.Distance(v.Pos, Player.Pos) < 14 * Map.Spread), v => ev.Settlement = v.Id, ev);
            case var tr when tr.StartsWith("trait:"): return Me.Traits.Contains(tr[6..]) ? ev : null;
            case var f when f.StartsWith("flag:"): return S.Flags.TryGetValue(f[5..], out float set) && S.Hours - set >= FlagDays * 24 ? ev : null;
            case "in_town": return SettlementAt(Player.Pos) is { Kind: SettlementKind.Town } town && !Refuses(town) ? (ev.Settlement = town.Id) >= 0 ? ev : null : null;
            default: return null;
        }
    }

    PendingEvent? Pick<T>(IEnumerable<T> list, Action<T> set, PendingEvent ev)
    {
        var l = list.ToList();
        if (l.Count == 0) return null;
        set(l[rng.Next(l.Count)]);
        return ev;
    }

    /// Daily: when the gap has passed and nothing is waiting, one eligible event (off cooldown) comes up.
    void EventsDaily()
    {
        BrigandKingsDaily();
        if (S.Event != null || S.Hours < S.NextEvent) return;
        var ready = new List<PendingEvent>();
        foreach (var e in Events.Where(e => !Forced(e.Trigger)))
        {
            if (S.EventsFired.TryGetValue(e.Id, out float at) && S.Hours - at < e.Cooldown * 24) continue;
            if (Find(e.Trigger) is PendingEvent pe) { pe.Id = e.Id; ready.Add(pe); }
        }
        if (ready.Count == 0) return; // nothing fits today: try again tomorrow rather than wait out another gap
        S.NextEvent = (float)S.Hours + rng.Range(EventGapMin, EventGapMax) * 24;
        Raise(ready[rng.Next(ready.Count)]);
    }

    void Raise(PendingEvent pe)
    {
        S.Event = pe;
        S.EventsFired[pe.Id] = (float)S.Hours;
        EventTally[pe.Id] = EventTally.GetValueOrDefault(pe.Id) + 1;
        if (EventOf(pe.Id)?.Trigger is string t && t.StartsWith("flag:")) S.Flags.Remove(t[5..]);
    }

    /// How often each event came up in this session (for the spread test and the balance report).
    public readonly Dictionary<string, int> EventTally = new();

    /// An event the world forces (it does not wait for the gap); dropped if another is already waiting.
    void Force(string id, PendingEvent pe)
    {
        if (S.Event != null || EventOf(id) == null) return;
        pe.Id = id;
        Raise(pe);
    }

    /// An event's text with its names filled in.
    public string Fill(string text, PendingEvent pe)
    {
        string realm = Captain.Contract >= 0 ? FactionNames[Captain.Contract] : "realm";
        return text.Replace("{settlement}", pe.Settlement >= 0 ? SettlementById(pe.Settlement).Name : "the town")
            .Replace("{lord}", CharacterById(pe.Lord)?.Name ?? "a lord")
            .Replace("{band}", PartyById(pe.Party)?.Name ?? "brigands")
            .Replace("{company}", Player.Companies.FirstOrDefault(c => c.Id == pe.Company)?.Name ?? "a company")
            .Replace("{realm}", realm)
            .Replace("{liege}", RulerOf(Captain.Contract)?.Name ?? "your liege")
            .Replace("{site}", SiteById(pe.Site)?.Name ?? "the place")
            .Replace("{companion}", CharacterById(pe.Lord)?.Name ?? "your companion")
            .Replace("{man}", S.Fallen.FirstOrDefault(m => m.Id == pe.Soldier)?.Name ?? "one of your men");
    }

    /// Why a choice cannot be taken now ("" = it can).
    public string ChoiceBlocked(ChoiceDef c) =>
        c.Need.StartsWith("gold:") && float.TryParse(c.Need[5..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g) && S.Gold < g ? $"NEEDS {g:0} CROWNS" : "";

    /// The first choice of the waiting event that can be taken (for the stand-in captain).
    public int EventOf0() => S.Event is PendingEvent pe && EventOf(pe.Id) is EventDef e ? Math.Max(0, e.Choices.FindIndex(c => ChoiceBlocked(c) == "")) : 0;

    /// Answer the waiting event with choice i: its effects apply, and a line goes to the log.
    public string? Choose(int i)
    {
        if (S.Event is not PendingEvent pe || EventOf(pe.Id) is not EventDef e) return "NO EVENT";
        if (i < 0 || i >= e.Choices.Count) return "NO SUCH CHOICE";
        var c = e.Choices[i];
        if (ChoiceBlocked(c) is { Length: > 0 } why) return why;
        S.Event = null;
        var lines = new List<string>();
        foreach (var fx in c.Effects) if (ApplyEffect(fx, pe) is string l) lines.Add(l);
        Log($"{Fill(e.Title, pe).ToLowerInvariant()}: {Fill(c.Label, pe).ToLowerInvariant()}." + (lines.Count > 0 ? " " + string.Join(" ", lines) : ""));
        return null;
    }

    /// Apply one effect; returns a short note for the log when there is something to say.
    string? ApplyEffect(string fx, PendingEvent pe)
    {
        var (verb, n, _) = ParseEffect(fx);
        var s = pe.Settlement >= 0 ? SettlementById(pe.Settlement) : null;
        var lord = CharacterById(pe.Lord);
        var co = Player.Companies.FirstOrDefault(x => x.Id == pe.Company);
        string? said = SiteEffect(verb, n, pe, out bool siteVerb);
        if (siteVerb) return said;
        switch (verb)
        {
            case "gold": S.Gold = MathF.Max(0, S.Gold + n); break;
            case "renown": Captain.Renown = MathF.Max(0, Captain.Renown + n); break;
            case "food": Player.Food = MathF.Max(0, Player.Food + n); break;
            case "horses": Player.Horses = Math.Max(0, Player.Horses + (int)n); break;
            case "opinion": if (lord != null) Remember(lord, Me, n >= 0 ? "you did right by me" : "you turned me away", n); break;
            case "opinion_liege": if (RulerOf(Captain.Contract) is Character r) Remember(r, Me, "you warned me of a plot", n); break;
            case "opinion_realm":
                foreach (var l in S.Characters.Where(x => x.Kind == "lord" && !x.Dead && x.Faction == Captain.Contract))
                    l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) + n, -100, 100);
                break;
            case "unrest": if (s != null) s.Unrest = Math.Clamp(s.Unrest + n, 0, 100); break;
            case "prosperity": if (s != null) s.Prosperity = Math.Clamp(s.Prosperity + n, 0, 100); break;
            case "tax": if (s != null) SetTax(s, (int)n); break;
            case "cohesion": foreach (var x in co != null ? new List<CompanyRecord> { co } : Player.Companies) x.Cohesion = Math.Clamp(x.Cohesion + n, 0, 30); break;
            case "xp": foreach (var x in co != null ? new List<CompanyRecord> { co } : Player.Companies) foreach (var st in x.Stacks) st.AddXp(st.Men * n); break;
            case "exhaustion":
                foreach (var w in S.Wars.Where(w => w.Active && (w.A == Captain.Contract || w.B == Captain.Contract))) AddExhaustion(w, Captain.Contract, n);
                break;
            case "calm": S.Omen = MathF.Max(0, S.Omen - n); break;
            case "flag": S.Flags[fx[5..]] = (float)S.Hours; break;
            case "farsight": S.FarSightUntil = (float)S.Hours + n * 24; return $"For {n:0} days you will see much further.";
            case "peace": return lord != null ? AcceptPeace(lord.Faction) : null;
            case "man": return GiftMen(1, SettlementNear(Player.Pos)) > 0 ? "He joins your company." : "You have no room for him.";
            case "sick":
                int ill = 0;
                foreach (var st in Player.Companies.SelectMany(x => x.Stacks))
                {
                    int k = (int)MathF.Round(st.Men * n / 100);
                    st.Men -= k; st.Wounded += k; ill += k;
                }
                return ill > 0 ? $"{ill} men fall sick." : null;
            case "reveal":
                var site = SiteById(pe.Site) is Site named && !SiteSeen(named) ? named
                    : Sites.Where(x => !SiteSeen(x)).OrderBy(x => Vector2.Distance(x.Pos, Player.Pos)).FirstOrDefault();
                if (site == null) return null;
                S.SitesSeen.Add(site.Id);
                return $"You learn the way to the {site.Name}, to the {Bearing(Player.Pos, site.Pos)}.";
            case "lists": if (s != null) { Lists = s; return "The heralds cry your name. You ride into the lists."; } break;
            case "gamble":
                // A stake of n crowns at dice: the charming read the table better.
                if (rng.Chance(0.4f + Skill("Charm") / 500f)) { S.Gold += n; return $"The dice love you tonight: you win {n:0} crowns."; }
                S.Gold = MathF.Max(0, S.Gold - n);
                return $"The dice go against you. {n:0} crowns gone.";
            case "quest": return lord != null ? GiveQuest(lord) : null;
            case "bounty": return pe.Party >= 0 ? Bounty(PartyById(pe.Party)) : s != null ? Bounty(Rebels(s)) : null;
            case "revolt": if (s != null && Rebels(s) == null) Revolt(s); break;
            case "prisoners_free": FreePrisoners(); break;
            case "road_sword" or "road_lance" or "road_bow": return ChooseRoad(verb[5..]);
            case "crown": return s != null ? ClaimCrown(s) ?? $"{s.Name} is yours, and you are its king." : null;
            case "prisoners_recruit":
                foreach (var g in Player.Prisoners.ToList()) RecruitPrisoners(g);
                break;
            case "bury": Bury(pe.Grave); CompanionsReact("bury"); break;
            case "disband":
                if (s != null && Rebels(s) is Party reb) { reb.Down = true; S.Parties.Remove(reb); return "The pretender takes your gold and is gone."; }
                break;
        }
        return null;
    }

    // ---------- lord quests (8) ----------

    /// A lord's quest, on the contract machinery: find his father's sword (S3), hunt a lord at war with his realm, or clear the
    /// worst band near his home.
    /// The reward is mostly his favour.
    string? GiveQuest(Character giver)
    {
        var home = PartyById(giver.PartyId) is Party gp && gp.HomeId >= 0 ? SettlementById(gp.HomeId) : null;
        var at = home?.Pos ?? Player.Pos;
        float deadline = (float)S.Hours + 12 * 24;
        Contract? q = null;
        if (rng.Chance(0.35f) && RelicQuest(giver, at) is Contract relic)
            q = relic;
        else if (S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && FactionsAtWar(giver.Faction, p.Faction) && Hostile(Player, p))
                .OrderBy(p => Vector2.Distance(p.Pos, at)).FirstOrDefault() is Party rival)
            q = new Contract { Kind = "hunt", Target = rival.Id, Reward = 150, Text = $"Defeat {rival.Name} for {giver.Name}." };
        else if (S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down).OrderBy(p => Vector2.Distance(p.Pos, at)).FirstOrDefault() is Party band)
            q = new Contract { Kind = "clear", Target = band.Id, Reward = 100, Text = $"Destroy the {band.Name} for {giver.Name}." };
        if (q == null) return "He finds there is nothing you can do after all.";
        q.Id = S.NextId++;
        q.Faction = Captain.Contract;
        q.Deadline = deadline;
        q.Giver = giver.Id;
        S.Contracts.Add(q);
        return $"Quest: {q.Text}";
    }

    /// A price on a band's head, as a contract of your own.
    string? Bounty(Party? band)
    {
        if (band == null || band.Down || S.Contracts.Any(k => k.Target == band.Id)) return null;
        S.Contracts.Add(new Contract
        {
            Id = S.NextId++, Kind = "clear", Target = band.Id, Faction = Captain.Contract, Deadline = (float)S.Hours + 15 * 24,
            Reward = 200 + Rules.PerMan(Rules.Strength(band) / 2) + (band.CharId >= 0 ? 400 : 0), Text = $"Destroy the {band.Name}.",
        });
        return $"A bounty is posted on the {band.Name}.";
    }

    // ---------- named threats (8) ----------

    /// Daily: the largest band, grown past a size, crowns its chief a brigand king; a king grows, and burns villages near him.
    void BrigandKingsDaily()
    {
        var king = S.Parties.FirstOrDefault(p => p.Kind == PartyKind.Bandit && !p.Down && p.CharId >= 0 && CharacterById(p.CharId) is { Kind: "brigand" });
        if (king == null && Day >= 30)
        {
            var big = S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down && p.HomeId < 0 && p.CharId < 0 && p.Men >= Scaled(BrigandKingMen)).OrderByDescending(p => p.Men).FirstOrDefault();
            if (big != null) Crown(big);
        }
        else if (king != null)
        {
            if (king.Men < Scaled(150) && king.Companies.Count > 0) { var st = king.Companies[0].Stacks[0]; if (Rules.Room(king.Companies[0], st.Troop) > 0) st.Men += Math.Min(Rules.Room(king.Companies[0], st.Troop), Scaled(1)); else if (king.Companies.Count < 5) king.Companies.Add(NewCompany(st.Troop, "infantry", Scaled(6), 0)); }
            foreach (var v in S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.ThreatUntil < S.Hours && Vector2.Distance(v.Pos, king.Pos) < 15 * Map.Spread))
            {
                v.ThreatUntil = (float)S.Hours + 7 * 24;
                v.Recruits = 0;
                Raided(v);
            }
        }
    }

    /// A band's chief becomes a brigand king: a named character with a growing host.
    internal void Crown(Party band)
    {
        var n = Names.Get();
        var ch = NewCharacter("brigand", PersonName(Factions.Common), -1, Factions.Common, 2);
        ch.PartyId = band.Id;
        band.CharId = ch.Id;
        band.Name = $"{ch.Name.Split(' ')[0]}'s Host";
        string troop = band.Companies.FirstOrDefault()?.Troop ?? "levies";
        band.Companies.Add(NewCompany(troop, "infantry", Scaled(16), 0));
        Log($"{ch.Name} gathers the brigands of the land and calls himself their king.");
        Force("brigand_king", new PendingEvent { Party = band.Id, Lord = ch.Id });
    }

    /// A brigand king or a pretender beaten in the field is gone for good.
    void ThreatFalls(Party p, bool byYou)
    {
        if (CharacterOf(p) is not Character ch) return;
        ch.Dead = true;
        ch.PartyId = -1;
        if (ch.Kind == "brigand") Log($"{ch.Name}, the brigand king, is dead{(byYou ? " by your hand" : "")}.");
        if (ch.Kind == "pretender") Log($"{ch.Name} is beaten and the rising is over.");
        if (byYou) Captain.Renown += ch.Kind == "brigand" ? 25 : 15;
    }

    /// A rising led by a pretender: a named leader, and a seat that opens its gates sooner while he is at large.
    void RaisePretender(Settlement s, Party rebels)
    {
        var n = Names.Get();
        var ch = NewCharacter("pretender", rng.Pick(n.LordsOf(s.Culture)) + " of " + s.Name, s.Faction, s.Culture, 2);
        ch.PartyId = rebels.Id;
        rebels.CharId = ch.Id;
        rebels.Name = $"{ch.Name.Split(' ')[0]}'s Rising";
        rebels.Companies.Add(NewCompany(Factions.Root(s.Culture), "infantry", 12, 0, s));
        Log($"{ch.Name} claims {s.Name} and leads its rebels.");
        if (s.Owner == Player.Id) Force("pretender", new PendingEvent { Settlement = s.Id, Party = rebels.Id, Lord = ch.Id });
    }

    /// While a pretender is abroad his seat goes over at 85 unrest rather than 100.
    float DefectThreshold(Settlement s) => Rebels(s) is Party r && CharacterOf(r) is { Kind: "pretender" } ? 85 : DefectAt;
}
