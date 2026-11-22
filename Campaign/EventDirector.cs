using Microsoft.Xna.Framework;

namespace Ironfield;

/// The events' director (docs/pre-demo/EVENTS.md EV1): every hour it notes the moments that came (a battle just fought, a
/// place reached, a stay in town, a new province, the evening's camp, the road, the week's turn), and when the quiet after the
/// last event has passed it may draw one that fits: odds by weight, freshness and fit. Events the world raises wait in a queue
/// when another is up, and follow-ups come due there; the queue drains before anything is drawn. Its dice are its own.
public sealed partial class Campaign
{
    Rng? eventDice;
    Rng EventDice => eventDice ??= Rng.Events(S);
    /// The weekly tick sets it; the next hour's moments take it.
    bool weekDue;
    /// How often each event came up in this session (for the spread test and the --events report).
    public readonly Dictionary<string, int> EventTally = new();
    /// Hours a follow-up waits after another event before it comes up.
    public const float FollowGap = 12;

    /// Hourly: the queue, then the moments, then perhaps a draw.
    void EventsHourly()
    {
        if (S.GameOver || TutorialActive) return;
        var moments = NoteMoments();
        if (S.Event == null) DrainQueue();
        if (S.Event != null || Sailing || moments.Count == 0) return;
        if (S.Hours < S.NextEvent) return;
        if (S.EventHistory.Count(h => S.Hours - h.At < 7 * 24 && Drawn(h.Id)) >= B.EventWeekCap) return;
        float overdue = (float)Math.Max(0, (S.Hours - S.NextEvent) / 24);
        float chance = moments.Max(MomentChance) * (1 + overdue * B.EventOverdue);
        if (!EventDice.Chance(chance)) return;
        if (Draw(moments, new PendingEvent()) is (EventDef e, PendingEvent pe)) Raise(pe, e);
    }

    /// Is this an event the director draws (not one the world raises, a site's, or a follow-up)? Only these count to the weekly cap.
    public static bool Drawn(string id) => EventOf(id) is EventDef e && e.Moments.Any(m => m != "site");

    float MomentChance(string m) => m switch
    {
        "road" => B.EventRoad, "town" => B.EventTown, "camp" => B.EventCamp, "arrive" => B.EventArrive,
        "battle" => B.EventBattle, "province" => B.EventProvince, "week" => B.EventWeek, _ => 0,
    };

    /// The moments of this hour, noted once each as they happen (road and town hold while you are on them).
    List<string> NoteMoments()
    {
        var m = new List<string>();
        var here = SettlementAt(Player.Pos);
        int place = here?.Id ?? -1;
        if (S.LastBattleAt > S.EventBattle && S.Hours - S.LastBattleAt < 12) { S.EventBattle = S.LastBattleAt; m.Add("battle"); }
        if (place != S.EventPlace)
        {
            if (place >= 0) m.Add("arrive");
            S.EventPlace = place;
            S.EventPlaceSince = (float)S.Hours;
        }
        else if (place >= 0 && S.Hours - S.EventPlaceSince >= 6) m.Add("town");
        int prov = Map.ProvinceAt(Player.Pos).Id;
        if (prov != S.EventProvince) { if (S.EventProvince >= 0 && place < 0) m.Add("province"); S.EventProvince = prov; }
        if (place < 0 && S.Hours % 24 is >= 20 and < 23 && S.EventCampDay != Day) { S.EventCampDay = Day; m.Add("camp"); }
        if (place < 0 && Player.Moving) m.Add("road");
        if (weekDue) { weekDue = false; m.Add("week"); }
        return m;
    }

    /// The best of what fits these moments, drawn by weight x freshness x fit, with its cast; null when nothing fits.
    (EventDef, PendingEvent)? Draw(IReadOnlyCollection<string> moments, PendingEvent seed)
    {
        var fits = new List<(EventDef E, PendingEvent Pe, float W)>();
        var recent = S.EventHistory.TakeLast(6).Select(h => EventOf(h.Id)?.Theme).ToList();
        foreach (var e in Events)
        {
            if (!e.Moments.Any(moments.Contains)) continue;
            if (S.EventsFired.TryGetValue(e.Id, out float at) && (e.Once || S.Hours - at < e.Cooldown * 24)) continue;
            var pe = seed.Copy(e.Id);
            if (!HoldsAll(e.When, pe) || !BindCast(e, pe)) continue;
            int times = S.EventHistory.Count(h => h.Id == e.Id);
            float fresh = MathF.Pow(0.6f, recent.Count(t => t == e.Theme)) / (1 + 0.6f * times);
            float fit = 1 + 0.2f * (e.When.Count + e.Cast.Values.Sum(c => c.Where.Count));
            fits.Add((e, pe, e.Weight * fresh * fit));
        }
        if (fits.Count == 0) return null;
        float roll = EventDice.Float() * fits.Sum(f => f.W);
        foreach (var f in fits) { roll -= f.W; if (roll <= 0) return (f.E, f.Pe); }
        return (fits[^1].E, fits[^1].Pe);
    }

    /// Up it comes: waiting for your answer, remembered, and the quiet after it begins.
    void Raise(PendingEvent pe, EventDef? e = null)
    {
        e ??= EventOf(pe.Id);
        S.Event = pe;
        S.EventsFired[pe.Id] = (float)S.Hours;
        EventTally[pe.Id] = EventTally.GetValueOrDefault(pe.Id) + 1;
        float quiet = EventDice.Range(B.EventQuietMin, B.EventQuietMax) + (e?.Heavy == true ? B.EventHeavyQuiet : 0);
        S.NextEvent = MathF.Max(S.NextEvent, (float)S.Hours + quiet * 24);
    }

    /// An event the world raises (a pretender, an envoy, the dead to bury): up at once if nothing waits, else queued for a few
    /// days. Its cast is filled from what the code gives, the rest found as the data says.
    void Force(string id, PendingEvent pe, float days = 5)
    {
        if (EventOf(id) is not EventDef e) return;
        pe.Id = id;
        if (!BindCast(e, pe)) return;
        if (S.Event == null) { Raise(pe, e); return; }
        S.EventQueue.Add(new QueuedEvent { Event = pe, Priority = 2, At = (float)S.Hours, Until = (float)S.Hours + days * 24 });
    }

    /// Queue an event the code starts (a rival's first meeting): due in `days`, about this cast, kept until it can come.
    internal void QueueEvent(string id, float days, PendingEvent? pe = null) =>
        S.EventQueue.Add(new QueuedEvent { Event = (pe ?? new PendingEvent()).Copy(id), Priority = 1, At = (float)S.Hours + days * 24 });

    /// A follow-up: due some days on, about the same cast, if its conditions hold by then.
    void FollowUp(FollowDef t, PendingEvent pe)
    {
        float at = (float)S.Hours + EventDice.Range(t.Days[0], t.Days[1]) * 24;
        S.EventQueue.Add(new QueuedEvent { Event = pe.Copy(t.Id), Priority = 1, At = at, Until = at + 10 * 24, When = t.When.ToList() });
    }

    /// The queue: the lapsed go; the first that is due, holds and can still find its cast comes up.
    void DrainQueue()
    {
        S.EventQueue.RemoveAll(q => q.Until > 0 && S.Hours > q.Until || EventOf(q.Event.Id) == null);
        float last = S.EventsFired.Values.DefaultIfEmpty(-9999).Max();
        foreach (var q in S.EventQueue.OrderByDescending(q => q.Priority).ThenBy(q => q.At).ToList())
        {
            if (q.At > S.Hours || q.Priority < 2 && (S.Hours - last < FollowGap || Sailing)) continue;
            var e = EventOf(q.Event.Id)!;
            if (!HoldsAll(q.When, q.Event) || !BindCast(e, q.Event)) continue;
            S.EventQueue.Remove(q);
            Raise(q.Event, e);
            return;
        }
    }

    /// For tests, shots and the playtest harness: raise an event now, its cast found as the data says (its conditions are not
    /// asked). Returns the id, or null when no cast fits.
    internal string? RaiseEvent(string id, PendingEvent? seed = null)
    {
        if (EventOf(id) is not EventDef e) return null;
        var pe = (seed ?? new PendingEvent()).Copy(id);
        if (!BindCast(e, pe)) return null;
        S.Event = null;
        Raise(pe, e);
        return id;
    }

    /// Could this event come up now (its conditions hold and a cast can be found)? For tests and the --events report.
    public bool Eligible(string id) => EventOf(id) is EventDef e && HoldsAll(e.When, new PendingEvent { Id = id }) && BindCast(e, new PendingEvent { Id = id });

    // ---------- the cast ----------

    /// Fill each role the event names that the code did not: a candidate that meets the role's conditions, on the events'
    /// dice. False when a role that must be filled cannot be.
    internal bool BindCast(EventDef e, PendingEvent pe)
    {
        foreach (var role in new[] { "settlement", "site", "band", "lord", "companion", "man", "other", "company" })
        {
            if (!e.Cast.TryGetValue(role, out var cast) || RoleBound(role, pe)) continue;
            if (cast.Optional && cast.Where.Count == 0) continue; // a role only the code fills (the cairn's company)
            var pool = Candidates(role, cast, e, pe).ToList();
            if (pool.Count == 0) { if (cast.Optional) continue; return false; }
            Bind(role, pool[EventDice.Next(pool.Count)], pe);
        }
        return true;
    }

    IEnumerable<object> Candidates(string role, CastDef cast, EventDef e, PendingEvent pe)
    {
        bool hurtOk = cast.Where.Any(w => w.TrimStart('!').StartsWith("hurt"));
        IEnumerable<object> all = role switch
        {
            "lord" => S.Characters.Where(c => c.Kind == "lord" && !c.Dead),
            "companion" => Companions,
            "man" => Player.Companies.SelectMany(k => k.Roll.Where(m => hurtOk || !m.Hurt).Select(m => (object)(m, k))),
            "other" => LivingMan(pe.Soldier) is Soldier me
                ? Player.Companies.SelectMany(k => k.Roll.Where(m => m != me && !m.Hurt).Select(m => (object)(m, k)))
                    .Where(o => TieWant(cast) is not string kind || me.Ties.Any(t => t.Other == ((ValueTuple<Soldier, CompanyRecord>)o).Item1.Id && TieKind(t.Kind, kind)))
                : Enumerable.Empty<object>(),
            "settlement" => S.Settlements,
            "site" => Sites,
            "band" => S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down),
            "company" => Player.Companies.Where(k => k.Men > 0),
            _ => Enumerable.Empty<object>(),
        };
        var where = role == "other" ? cast.Where.Where(w => !w.StartsWith("tie:")).ToList() : cast.Where;
        var fit = all.Where(o => Fits(role, o, where, pe));
        // A man whose pair the event needs must have one: the other is found from his ties.
        if (role == "man" && e.Cast.TryGetValue("other", out var oc) && !oc.Optional && TieWant(oc) is string want)
            fit = fit.Where(o => o is ValueTuple<Soldier, CompanyRecord> t && t.Item1.Ties.Any(x => TieKind(x.Kind, want) && LivingMan(x.Other) is { Hurt: false }));
        return fit;
    }

    static string? TieWant(CastDef c) => c.Where.FirstOrDefault(w => w.StartsWith("tie:"))?[4..];

    void Bind(string role, object o, PendingEvent pe)
    {
        switch (role)
        {
            case "lord": pe.Lord = ((Character)o).Id; break;
            case "companion": pe.Companion = ((Character)o).Id; break;
            case "man": var (m, k) = ((Soldier, CompanyRecord))o; pe.Soldier = m.Id; pe.Company = k.Id; break;
            case "other": pe.Other = (((Soldier, CompanyRecord))o).Item1.Id; break;
            case "settlement": pe.Settlement = ((Settlement)o).Id; break;
            case "site": pe.Site = ((Site)o).Id; break;
            case "band": pe.Party = ((Party)o).Id; break;
            case "company": pe.Company = ((CompanyRecord)o).Id; break;
        }
    }

    /// A site reached (Sites.cs): one of the events for its kind, drawn as the director draws, at once if nothing waits.
    void SiteEvent(Site site)
    {
        if (S.Event != null) return;
        var seed = new PendingEvent { Site = site.Id, Settlement = SettlementNear(site.Pos)?.Id ?? -1 };
        if (Draw(new[] { "site" }, seed) is (EventDef e, PendingEvent pe)) { S.Event = pe; S.EventsFired[pe.Id] = (float)S.Hours; EventTally[pe.Id] = EventTally.GetValueOrDefault(pe.Id) + 1; }
    }
}
