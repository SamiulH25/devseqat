using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ironfield;

/// One event, a storylet (docs/pre-demo/EVENTS.md): read from Data/events/*.json. It comes at one of its moments when every
/// condition in `When` holds and its cast can be found, or when the code raises it by id; it offers two to four choices.
public sealed class EventDef
{
    public string Id = "", Title = "", Text = "";
    /// The picture at the head of the page: "camp", "road", "ford", "town", "court", "village", "burning", "shrine", "field",
    /// "sea", "flame", "site_<kind>"... (MapScreen.Event.cs); empty = the camp, or the site's own.
    public string Scene = "";
    /// When it may be drawn (Campaign.Moments); none = only ever raised by the code or followed from another event.
    public List<string> Moments = new();
    /// Conditions that must all hold (Campaign/EventConditions.cs).
    public List<string> When = new();
    /// Who and what it is about: role (lord, companion, man, other, settlement, site, band, company) -> how to find one.
    public Dictionary<string, CastDef> Cast = new();
    /// Odds against the others that fit the same moment; days before it may come again; once in a campaign; a weighty one
    /// buys a longer quiet after it.
    public float Weight = 1;
    public int Cooldown = 30;
    public bool Once, Heavy;
    public List<ChoiceDef> Choices = new();
    /// The file it came from (road, camp, court...): the director spreads its draws across themes.
    public string Theme = "";
}

/// How a role of the cast is found: conditions on the candidate (Campaign/EventCast.cs). An optional role may stay empty,
/// and the choices that name it are then not offered.
public sealed class CastDef
{
    public List<string> Where = new();
    public bool Optional;
}

/// One choice: what it needs (shown struck through with the reason when it fails), what it costs (paid on choosing, shown),
/// and either one outcome or a check with an outcome for each side of it.
public sealed class ChoiceDef
{
    public string Label = "", Hint = "", Why = "";
    public List<string> Needs = new(), Cost = new();
    public CheckDef? Check;
    public OutcomeDef? Outcome, Pass, Fail, Great, Awful;
}

/// A test of the captain's skill or trait, or of a man of the cast, against a difficulty (EV2). The odds shown are the odds rolled.
public sealed class CheckDef
{
    /// The captain's skill (an id from Skills.All), or a role ("man", "other", "companion") whose worth is tested, or neither.
    public string Skill = "", Man = "";
    /// A captain's trait that helps (+EventTraitEdge) or, written "!Trait", hinders; a man's nature that helps him.
    public string Trait = "", Nature = "";
    public float Against = 50;
}

/// What happens: the words on the outcome page, the effects, a deed on the man's record, and a follow-up later.
public sealed class OutcomeDef
{
    public string Text = "", Deed = "";
    public List<string> Do = new();
    public FollowDef? Then;
}

/// An event that follows this one some days later, about the same cast, if its conditions hold by then.
public sealed class FollowDef
{
    public string Id = "";
    public List<float> Days = new() { 5, 10 };
    public List<string> When = new();
}

/// An event waiting for your answer, with its cast (ids; -1 = none).
public sealed class PendingEvent
{
    public string Id = "";
    public int Settlement = -1, Lord = -1, Party = -1, Company = -1, Grave = -1, Site = -1, Soldier = -1, Companion = -1;
    /// A camp event's second man (C5: {other}), a soldier id.
    public int Other = -1;
    /// C7: the cairn a visit is to, and for the year's day the battle and the men who fell in it ({field}, {dead}).
    public int Cairn = -1;
    public string Field = "";
    public List<int>? Dead;

    public PendingEvent Copy(string id) => new()
    {
        Id = id, Settlement = Settlement, Lord = Lord, Party = Party, Company = Company, Grave = Grave, Site = Site, Soldier = Soldier,
        Companion = Companion, Other = Other, Cairn = Cairn, Field = Field, Dead = Dead?.ToList(),
    };
}

/// An event in the queue: raised by the world while another waited (priority 2), or a follow-up due later (priority 1).
public sealed class QueuedEvent
{
    public PendingEvent Event = new();
    public int Priority;
    /// Not before this hour; dropped after `Until` (0 = never).
    public float At, Until;
    /// A follow-up's conditions, checked when it is due.
    public List<string> When = new();
}

/// What you chose in an event (the `chose:` and `seen:` conditions read it).
public sealed class EventMemory
{
    public string Id = "", Result = "";
    public int Choice;
    public float At;
}

/// One line of an outcome or a choice's cost: an icon, the words, and whether it is good (1), bad (-1) or neither.
public readonly record struct EventLine(string Icon, string Text, int Sign);

/// The outcome page: shown once you choose, until you go on (not saved; the effects are already applied).
public sealed class EventResult
{
    public string Id = "", Title = "", Choice = "", Text = "", Result = "", Scene = "";
    public float Odds = -1;
    public bool More;
    public PendingEvent Event = new();
    public List<EventLine> Lines = new();
}

/// The events (docs/pre-demo/EVENTS.md): the data, its checks, the words, and answering an event.
public sealed partial class Campaign
{
    public const string EventsDir = "Data/events";
    /// The moments the director draws at (Campaign/EventDirector.cs); "site" is a site reached, raised by Sites.cs.
    public static readonly string[] Moments = { "road", "camp", "arrive", "town", "battle", "province", "week", "site" };
    /// Events the code raises by id; each must be in the data.
    public static readonly string[] CodeRaised =
    {
        "start_road", "aura_vigil", "aura_grief", "flame_bargain", "flame_end", "flame_rival", "rival_meet", "bury_named", "bury_dead",
        "peace_offer", "brigand_king", "pretender", "free_crown", "cairn_visit", "men_day_fell",
    };
    static readonly string[] Roles = { "lord", "companion", "man", "other", "settlement", "site", "band", "company" };
    /// Words in the text that are not roles: filled from the captain, the man, the place and the moment.
    static readonly string[] FreeHoles = { "realm", "liege", "rival", "home", "field", "dead", "here", "province", "season", "fallen" };

    static List<EventDef>? events;
    public static IReadOnlyList<EventDef> Events => events ??= LoadEvents();

    static List<EventDef> LoadEvents()
    {
        var all = new List<EventDef>();
        foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = Path.Combine(root, EventsDir);
            if (!Directory.Exists(dir)) continue;
            var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var list = JsonSerializer.Deserialize<List<EventDef>>(doc.RootElement.GetProperty("events").GetRawText(), opts)!;
                foreach (var e in list) e.Theme = Path.GetFileNameWithoutExtension(file);
                all.AddRange(list);
            }
            return all;
        }
        return all;
    }

    public static EventDef? EventOf(string id) => Events.FirstOrDefault(e => e.Id == id);

    /// The outcomes a choice can have, with their keys ("" for a plain one).
    public static IEnumerable<(string Key, OutcomeDef O)> OutcomesOf(ChoiceDef c)
    {
        if (c.Outcome != null) yield return ("", c.Outcome);
        if (c.Pass != null) yield return ("pass", c.Pass);
        if (c.Fail != null) yield return ("fail", c.Fail);
        if (c.Great != null) yield return ("great", c.Great);
        if (c.Awful != null) yield return ("awful", c.Awful);
    }

    /// Every text of an event that may hold {holes}.
    static IEnumerable<string> TextsOf(EventDef e) =>
        new[] { e.Title, e.Text }.Concat(e.Choices.SelectMany(c => new[] { c.Label, c.Hint }.Concat(OutcomesOf(c).Select(o => o.O.Text))));

    /// Data checks: moments, conditions, cast, holes, checks, costs, verbs and follow-ups, as the engine reads them.
    public static IEnumerable<string> ValidateEvents()
    {
        if (Events.Count == 0) yield return "no events loaded";
        foreach (var id in CodeRaised) if (EventOf(id) == null) yield return $"the code raises {id}, which is not in the data";
        var followed = Events.SelectMany(e => e.Choices).SelectMany(c => OutcomesOf(c)).Select(o => o.O.Then?.Id).OfType<string>().ToHashSet();
        foreach (var e in Events)
        {
            string at = $"{e.Theme}/{e.Id}";
            if (Events.Count(x => x.Id == e.Id) > 1) yield return $"{at}: duplicate id";
            if (e.Title == "" || e.Text == "") yield return $"{at}: needs a title and a text";
            foreach (var m in e.Moments) if (!Moments.Contains(m)) yield return $"{at}: unknown moment {m}";
            if (e.Moments.Count == 0 && !CodeRaised.Contains(e.Id) && !followed.Contains(e.Id)) yield return $"{at}: no moment, and nothing raises it";
            if (e.Choices.Count is < 2 or > 4) yield return $"{at}: needs two to four choices";
            if (e.Weight <= 0) yield return $"{at}: weight must be above 0";
            foreach (var w in e.When) if (CondProblem(w, null) is string p) yield return $"{at}: {p}";
            foreach (var (role, cast) in e.Cast)
            {
                if (!Roles.Contains(role)) { yield return $"{at}: unknown role {role}"; continue; }
                foreach (var w in cast.Where) if (CondProblem(w, role) is string p) yield return $"{at}: {role}: {p}";
            }
            if (e.Cast.ContainsKey("other") && !e.Cast.ContainsKey("man")) yield return $"{at}: other needs a man";
            foreach (var text in TextsOf(e))
                foreach (Match m in Regex.Matches(text, @"\{(\w+)\}"))
                {
                    string h = m.Groups[1].Value;
                    if (Roles.Contains(h) ? !e.Cast.ContainsKey(h) && !(h == "company" && e.Cast.ContainsKey("man")) && !(h == "site" && e.Moments.Contains("site")) && !(h == "settlement" && e.Moments.Contains("site")) : !FreeHoles.Contains(h)) yield return $"{at}: {{{h}}} is not in its cast";
                    if (h == "home" && !e.Cast.ContainsKey("man")) yield return $"{at}: {{home}} needs a man";
                }
            if (e.Choices.Count > 0 && e.Choices.All(c => c.Needs.Any(n => n.StartsWith("cast:")))) yield return $"{at}: every choice hangs on a role; one must always be open";
            for (int i = 0; i < e.Choices.Count; i++)
            {
                var c = e.Choices[i];
                string ci = $"{at} choice {i + 1}";
                if (c.Label == "") yield return $"{ci}: no label";
                foreach (var n in c.Needs)
                    if (n.StartsWith("cast:")) { if (!e.Cast.ContainsKey(n[5..])) yield return $"{ci}: needs {n}, not in its cast"; }
                    else if (CondProblem(n, null) is string p) yield return $"{ci}: {p}";
                foreach (var fx in c.Cost)
                {
                    var (verb, _, n, _) = ParseEffect(fx);
                    if (verb is not ("gold" or "food" or "horses") || n <= 0) yield return $"{ci}: a cost is gold, food or horses, more than 0 ({fx})";
                }
                bool plain = c.Outcome != null, check = c.Check != null;
                if (plain == check) yield return $"{ci}: one outcome, or a check with pass and fail";
                if (check && (c.Pass == null || c.Fail == null)) yield return $"{ci}: a check needs pass and fail";
                if (!check && (c.Pass ?? c.Fail ?? c.Great ?? c.Awful) != null) yield return $"{ci}: pass and fail need a check";
                if (c.Check is CheckDef k)
                {
                    if (k.Skill != "" && Skills.All.All(s => s.Id != k.Skill)) yield return $"{ci}: unknown skill {k.Skill}";
                    if (k.Man != "" && (k.Man is not ("man" or "other" or "companion") || !e.Cast.ContainsKey(k.Man))) yield return $"{ci}: the check's man {k.Man} is not in its cast";
                    if (k.Trait != "" && !EarnedTraits.Contains(k.Trait.TrimStart('!'))) yield return $"{ci}: unknown trait {k.Trait}";
                    if (k.Nature != "" && (!Natures.Contains(k.Nature) || k.Man is not ("man" or "other"))) yield return $"{ci}: a nature needs a man ({k.Nature})";
                    if (k.Skill == "" && k.Man == "" && k.Trait == "") yield return $"{ci}: the check tests nothing";
                    if (c.Hint == "") yield return $"{ci}: a check needs a hint of what may go wrong";
                }
                foreach (var (key, o) in OutcomesOf(c))
                {
                    string oi = key == "" ? ci : $"{ci} {key}";
                    if (o.Text == "") yield return $"{oi}: an outcome needs words";
                    if (o.Do.Contains("man_deed") != (o.Deed != "")) yield return $"{oi}: a deed and man_deed go together";
                    foreach (Match m in Regex.Matches(o.Deed, @"\{(\w+)\}"))
                        if (m.Groups[1].Value is not ("man" or "other" or "company" or "home" or "foe")) yield return $"{oi}: unknown {m.Value} in a deed";
                    foreach (var fx in o.Do) if (VerbProblem(fx, e) is string p) yield return $"{oi}: {p}";
                    if (o.Then is FollowDef t)
                    {
                        if (EventOf(t.Id) == null) yield return $"{oi}: follows {t.Id}, not in the data";
                        if (t.Days.Count != 2 || t.Days[0] < 0 || t.Days[1] < t.Days[0]) yield return $"{oi}: follow-up days are [min, max]";
                        foreach (var w in t.When) if (CondProblem(w, null) is string p) yield return $"{oi}: then: {p}";
                    }
                }
            }
        }
        // Every verb the engine knows is used by some event, so none is left behind untested.
        foreach (var v in Verbs.Keys)
            if (!Events.Any(e => e.Choices.Any(c => OutcomesOf(c).Any(o => o.O.Do.Any(fx => ParseEffect(fx).Verb == v))))) yield return $"no event uses the effect {v}";
    }

    // ---------- the words ----------

    /// An event's text with its names filled in.
    public string Fill(string text, PendingEvent pe)
    {
        if (!text.Contains('{')) return text;
        string realm = Captain.Contract >= 0 ? FactionNames[Captain.Contract] : "realm";
        var here = SettlementAt(Player.Pos);
        return text.Replace("{settlement}", pe.Settlement >= 0 ? SettlementById(pe.Settlement).Name : "the town")
            .Replace("{lord}", CharacterById(pe.Lord)?.Name ?? "a lord")
            .Replace("{band}", PartyById(pe.Party)?.Name ?? "brigands")
            .Replace("{company}", Player.Companies.FirstOrDefault(c => c.Id == pe.Company)?.Name ?? "the company")
            .Replace("{realm}", realm)
            .Replace("{liege}", RulerOf(Captain.Contract)?.Name ?? "your liege")
            .Replace("{site}", SiteById(pe.Site)?.Name ?? "the place")
            .Replace("{companion}", CharacterById(pe.Companion)?.Name ?? CharacterById(pe.Lord)?.Name ?? "your companion")
            .Replace("{rival}", CharacterById(Captain.Rival)?.Name ?? "your rival")
            .Replace("{man}", (LivingMan(pe.Soldier) ?? S.Fallen.FirstOrDefault(m => m.Id == pe.Soldier))?.Name ?? "one of your men")
            .Replace("{fallen}", S.Fallen.FirstOrDefault(m => m.Id == pe.Soldier)?.Name ?? "one of your men")
            .Replace("{other}", LivingMan(pe.Other)?.Name ?? "his friend")
            .Replace("{field}", pe.Cairn >= 0 && S.Cairns.FirstOrDefault(c => c.Id == pe.Cairn) is Cairn fc ? fc.Field : pe.Field != "" ? pe.Field : "the field")
            .Replace("{dead}", ListNames(pe.Cairn >= 0 && S.Cairns.FirstOrDefault(c => c.Id == pe.Cairn) is Cairn dc ? CairnNames(dc).ToList() : (pe.Dead ?? new()).Select(id => S.Fallen.FirstOrDefault(m => m.Id == id)?.Name).OfType<string>().ToList()))
            .Replace("{home}", EventMan(pe) is (Soldier hm, CompanyRecord hk) ? (hm.Home != "" ? hm.Home : hk.Home != "" ? hk.Home : "his village") : "his village")
            .Replace("{here}", here?.Name ?? Map.ProvinceAt(Player.Pos).Name)
            .Replace("{province}", Map.ProvinceAt(Player.Pos).Name)
            .Replace("{season}", Season.ToLowerInvariant());
    }

    /// "A", "A and B", "A, B and C".
    static string ListNames(List<string> names) => names.Count == 0 ? "your men" : names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

    // ---------- answering ----------

    /// Is the choice offered at all? A choice that names a role the cast left empty is not.
    public bool Offered(ChoiceDef c, PendingEvent? pe = null)
    {
        pe ??= S.Event;
        return pe == null || c.Needs.Where(n => n.StartsWith("cast:")).All(n => RoleBound(n[5..], pe));
    }

    /// The choices of the waiting event that are offered, with their index in the data.
    public List<(int Index, ChoiceDef Choice)> OfferedChoices(PendingEvent pe) =>
        EventOf(pe.Id) is EventDef e ? e.Choices.Select((c, i) => (i, c)).Where(x => Offered(x.c, pe)).ToList() : new();

    /// Why a choice cannot be taken now ("" = it can): a need that fails, or a cost you cannot pay.
    public string ChoiceBlocked(ChoiceDef c, PendingEvent? pe = null)
    {
        pe ??= S.Event ?? new PendingEvent();
        if (!Offered(c, pe)) return "NOT OPEN TO YOU";
        foreach (var n in c.Needs.Where(n => !n.StartsWith("cast:")))
            if (!Holds(n, pe)) return c.Why != "" ? c.Why : NeedText(n);
        foreach (var fx in c.Cost)
        {
            var (verb, _, n, _) = ParseEffect(fx);
            if (verb == "gold" && S.Gold < n) return $"NEEDS {n:0} CROWNS";
            if (verb == "food" && Player.Food < n) return $"NEEDS {n:0} DAYS OF FOOD";
            if (verb == "horses" && Player.Horses < n) return $"NEEDS {n:0} HORSES";
        }
        return "";
    }

    /// The first choice of the waiting event that can be taken (for the stand-in captain).
    public int EventOf0() => S.Event is PendingEvent pe && EventOf(pe.Id) is EventDef e ? Math.Max(0, e.Choices.FindIndex(c => ChoiceBlocked(c, pe) == "")) : 0;

    /// A cost ("gold:300") as the effect that pays it ("gold:-300").
    static string Spent(string fx) { var (verb, _, n, _) = ParseEffect(fx); return $"{verb}:{(-n).ToString(System.Globalization.CultureInfo.InvariantCulture)}"; }
    /// A cost as a line on the page: what it takes from you.
    public EventLine CostLine(string fx, PendingEvent pe) => Describe(Spent(fx), pe);

    /// The outcome page of the last answer, until the map screen shows it and you go on.
    public EventResult? EventOutcome;

    /// Answer the waiting event with choice i: pay its cost, roll its check, apply the outcome, queue what follows, and keep
    /// the page for the map. Returns null, or why not.
    public string? Choose(int i)
    {
        if (S.Event is not PendingEvent pe || EventOf(pe.Id) is not EventDef e) return "NO EVENT";
        if (i < 0 || i >= e.Choices.Count) return "NO SUCH CHOICE";
        var c = e.Choices[i];
        if (ChoiceBlocked(c, pe) is { Length: > 0 } why) return why;
        S.Event = null;
        var res = new EventResult { Id = e.Id, Title = Fill(e.Title, pe), Choice = Fill(c.Label, pe), Scene = e.Scene, Event = pe };
        foreach (var fx in c.Cost) { ApplyEffect(Spent(fx), pe); res.Lines.Add(CostLine(fx, pe)); }
        var (key, o) = Outcome(c, pe, out res.Odds);
        res.Result = key;
        res.Text = Fill(o.Text, pe);
        choiceDeed = o.Deed;
        foreach (var fx in o.Do)
        {
            var line = Describe(fx, pe);
            string? said = ApplyEffect(fx, pe);
            if (said != null && said != "") res.Lines.Add(line with { Text = said });
            else if (line.Text != "") res.Lines.Add(line);
        }
        choiceDeed = "";
        if (o.Then is FollowDef t) { FollowUp(t, pe); res.More = true; }
        S.EventHistory.Add(new EventMemory { Id = e.Id, Choice = i, Result = key, At = (float)S.Hours });
        if (S.EventHistory.Count > 300) S.EventHistory.RemoveAt(0);
        string first = Regex.Match(res.Text, @"^.*?[.!?](\s|$)").Value.Trim();
        Log($"{res.Title}: {(first != "" ? first : res.Choice.ToLowerInvariant() + ".")}");
        EventOutcome = res;
        return null;
    }

    /// The outcome a choice comes to: its one outcome, or its check rolled on the events' dice (great and awful at the edges).
    (string Key, OutcomeDef O) Outcome(ChoiceDef c, PendingEvent pe, out float odds)
    {
        odds = -1;
        if (c.Check is not CheckDef k) return ("", c.Outcome!);
        odds = Odds(k, pe);
        float roll = EventDice.Float(), edge = B.EventCheckEdge;
        if (roll < odds)
            return c.Great != null && roll < odds * edge ? ("great", c.Great) : ("pass", c.Pass!);
        return c.Awful != null && roll >= 1 - (1 - odds) * edge ? ("awful", c.Awful) : ("fail", c.Fail!);
    }
}
