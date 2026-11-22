namespace Ironfield;

/// Campaign: the camp's events (docs/archive/COMPANY.md C5), now storylets at the `camp` moment (docs/pre-demo/EVENTS.md):
/// their cast names a living named man (`man`, with conditions on his nature, his ties, his years) and a second (`other`,
/// found from his ties); they act on them with the verbs `man_deed` (the outcome's `deed` goes on his record), `man_hurt`,
/// `man_leaves`, `tie` (the two become brothers) and `mention`.
public sealed partial class Campaign
{
    /// The deed of the outcome being applied, for `man_deed`.
    string choiceDeed = "";

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

}
