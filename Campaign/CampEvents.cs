namespace Ironfield;

/// Campaign: the camp's events (docs/pre-demo/COMPANY.md C5). A trigger family, `men_*`, that finds one or two living named men
/// it is true of (two rivals, brothers, a lad and his master, an old man, a man of one nature...); the events in Data/events.json
/// name them with {man} and {other}, their company with {company}, his village with {home}, and act on them with the verbs
/// `man_deed` (the choice's `deed` goes on his record), `man_hurt`, `man_leaves`, `tie` (the two become brothers) and `mention`.
/// They come on their own timer and the roll's own dice (`RollRng`), never from the main stream, and only when no other event waits.
public sealed partial class Campaign
{
    public static readonly string[] MenTriggers =
    {
        "men_rivals", "men_brothers", "men_lad", "men_veteran", "men_old", "men_home", "men_in_town", "men_hurt", "men_avenging", "men_named",
        "men_steady", "men_bold", "men_grim", "men_merry", "men_pious", "men_sly", "men_proud", "men_gentle",
    };
    internal static bool MenTrigger(string trigger) => trigger.StartsWith("men_");
    /// Days between two camp events (on the roll's dice), and before the first.
    public const int MenEventGapMin = 4, MenEventGapMax = 8;

    /// The deed of the choice being applied, for `man_deed`.
    string choiceDeed = "";

    IEnumerable<(Soldier M, CompanyRecord K)> MenOnRoll(bool hurt) =>
        Player.Companies.SelectMany(k => k.Roll.Where(m => m.Hurt == hurt).Select(m => (m, k)));

    /// The man (and the other) a camp trigger is true of now, or null. Only living men on the roll; picks on the roll's dice.
    internal PendingEvent? FindMen(string trigger)
    {
        var fit = MenOnRoll(false).ToList();
        PendingEvent? One(IEnumerable<(Soldier M, CompanyRecord K)> pool)
        {
            var l = pool.ToList();
            if (l.Count == 0) return null;
            var (m, k) = l[RollRng.Next(l.Count)];
            return new PendingEvent { Soldier = m.Id, Company = k.Id };
        }
        PendingEvent? Pair(Func<SoldierTie, bool> kind)
        {
            var pairs = (from a in fit from t in a.M.Ties where kind(t) let o = LivingMan(t.Other) where o != null && !o.Hurt select (a.M, a.K, O: o)).ToList();
            if (pairs.Count == 0) return null;
            var (m, k, other) = pairs[RollRng.Next(pairs.Count)];
            return new PendingEvent { Soldier = m.Id, Company = k.Id, Other = other.Id };
        }
        switch (trigger)
        {
            case "men_rivals": return Pair(t => t.Kind == "Rivals");
            case "men_brothers": return Pair(t => t.Kind is "Brothers" or "Kin");
            case "men_lad": return Pair(t => t.Kind == "Lad"); // he is the lad, the other his master
            case "men_veteran": return One(fit.Where(x => x.M.Battles >= 10));
            case "men_old": return One(fit.Where(x => Soldier.AgeOf(x.M) >= 40 || x.M.Wounds >= 3));
            case "men_home": return One(fit.Where(x => x.M.Home != "" && S.Settlements.Any(s => s.Name == x.M.Home)));
            case "men_in_town":
                if (SettlementAt(Player.Pos) is not { Kind: SettlementKind.Town } town || Refuses(town)) return null;
                return One(fit) is PendingEvent ev ? Set(ev, e => e.Settlement = town.Id) : null;
            case "men_hurt": return One(MenOnRoll(true));
            case "men_avenging": return One(fit.Where(x => x.M.Avenging));
            case "men_named": return One(fit);
            case var n when n.StartsWith("men_") && Natures.Any(z => z.Equals(n[4..], StringComparison.OrdinalIgnoreCase)):
                return One(fit.Where(x => x.M.Nature.Equals(n[4..], StringComparison.OrdinalIgnoreCase)));
            default: return null;
        }
    }

    static PendingEvent Set(PendingEvent ev, Action<PendingEvent> set) { set(ev); return ev; }

    /// Daily, after the realm's events: when no event waits and the camp's gap has passed, one camp event that is true comes up.
    void MenEventsDaily()
    {
        if (S.Event != null || Sailing) return;
        if (S.NextMenEvent <= 0) { S.NextMenEvent = (float)S.Hours + MenEventGapMax * 24; return; } // a new campaign, or a save from before the camp
        if (S.Hours < S.NextMenEvent) return;
        var ready = new List<PendingEvent>();
        foreach (var e in Events.Where(e => MenTrigger(e.Trigger)))
        {
            if (S.EventsFired.TryGetValue(e.Id, out float at) && S.Hours - at < e.Cooldown * 24) continue;
            if (FindMen(e.Trigger) is PendingEvent pe) { pe.Id = e.Id; ready.Add(pe); }
        }
        if (ready.Count == 0) return;
        S.NextMenEvent = (float)S.Hours + RollRng.Range(MenEventGapMin, MenEventGapMax) * 24;
        Raise(ready[RollRng.Next(ready.Count)]);
    }

    /// The man a waiting event is about, if he is still on the roll, and his company.
    (Soldier M, CompanyRecord K)? EventMan(PendingEvent pe) =>
        LivingMan(pe.Soldier) is Soldier m && Player.Companies.FirstOrDefault(k => k.Roll.Contains(m)) is CompanyRecord k ? (m, k) : null;

    /// The camp's verbs (`man_deed`, `man_hurt`, `man_leaves`, `tie`, `mention`); null when `verb` is not one, else the log line
    /// ("" for none). A man gone from the roll since the event came up is left alone.
    string? MenEffect(string verb, PendingEvent pe)
    {
        if (verb is not ("man_deed" or "man_hurt" or "man_leaves" or "tie" or "mention")) return null;
        if (EventMan(pe) is not var (m, k)) return "";
        switch (verb)
        {
            case "man_deed":
                if (choiceDeed != "") m.Deed(FillMan(choiceDeed, m, k, LivingMan(pe.Other)?.Name ?? ""));
                return "";
            case "man_hurt":
                if (m.Hurt) return "";
                m.Hurt = true; m.Wounds++;
                if (k.Stacks.FirstOrDefault(s => s.Troop == m.Troop && s.Men > 1) is TroopStack st) { st.Men--; st.Wounded++; }
                return $"{m.Name} is hurt.";
            case "man_leaves":
                if (k.Stacks.FirstOrDefault(s => s.Troop == m.Troop) is TroopStack ls) { if (m.Hurt && ls.Wounded > 0) ls.Wounded--; else ls.Take(1); }
                Lose(k, m, "left", "");
                k.Prune();
                return "";
            case "tie":
                if (LivingMan(pe.Other) is not Soldier o) return "";
                m.Ties.RemoveAll(t => t.Other == o.Id); o.Ties.RemoveAll(t => t.Other == m.Id);
                return Tie(m, o, "Brothers", "Brothers") ? $"{m.Name} and {o.Name} are brothers now." : "";
            default: // mention
                m.Mentions++;
                k.Cohesion = MathF.Min(30, k.Cohesion + 1);
                m.Deed(OfBanner(m) && m.Mentions == BannerMentions ? "Mentioned a third time: one of the banner." : $"Mentioned by the captain on day {Day}.");
                return $"You mention {m.Name} before the company.";
        }
    }

    /// For the camp test only: raise a camp event now, as the daily pass would when it comes up.
    internal string? RaiseMenEvent(string id)
    {
        if (EventOf(id) is not EventDef e || FindMen(e.Trigger) is not PendingEvent pe) return null;
        pe.Id = id;
        S.Event = null;
        Raise(pe);
        return id;
    }
}
