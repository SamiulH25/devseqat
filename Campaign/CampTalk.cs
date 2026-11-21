namespace Ironfield;

/// Campaign: talking with your own men at the camp (docs/archive/COMPANY.md C5). A named man answers a few plain questions:
/// how the men are (a true line about food, pay, the last battle and the company's spirit), where he is from, and the man he
/// is tied to. Lines live in Data/talk.json beside the lords', under the `man_` topics, picked by his nature the way a
/// lord's are picked by his temper, and by what is true now (`when`). No dice: the same man says the same thing all day.
public sealed partial class Campaign
{
    /// The questions you can put to a man, and his greeting ("man_greet").
    public static readonly (string Id, string Label)[] MenTopics =
    {
        ("man_men", "HOW ARE THE MEN?"), ("man_home", "WHERE ARE YOU FROM?"), ("man_tie", "ABOUT "),
    };

    /// What a line's `when` may be, by topic: for the men, what is most true of the camp now; for a tie, its kind or "gone"
    /// (the other is dead or gone; never asked of a living tie, kept for the camp events' words).
    public static readonly Dictionary<string, string[]> TalkWhens = new()
    {
        ["man_greet"] = new[] { "", "won", "lost", "hurt" },
        ["man_men"] = new[] { "", "hungry", "unpaid", "won", "lost", "low", "high" },
        ["man_home"] = new[] { "", "far", "near" },
        ["man_tie"] = new[] { "Brothers", "Kin", "Rivals", "Master", "Lad" },
        // In battle (C6, BattleNamed.cs): by nature only.
        ["man_felled_captain"] = new[] { "" }, ["man_felled_horse"] = new[] { "" }, ["man_brother_down"] = new[] { "" }, ["man_saved"] = new[] { "" },
    };

    /// What is most true of the camp, for "how are the men?": hunger first, then pay, then the last battle, then the spirit.
    public string CampState(CompanyRecord k)
    {
        if (Player.Food < 2) return "hungry";
        if (S.UnpaidWeeks > 0) return "unpaid";
        var mood = MoodOfCamp();
        if (mood == CampMood.Won) return "won";
        if (mood == CampMood.Lost) return "lost";
        return k.Cohesion < 7 ? "low" : k.Cohesion >= 18 ? "high" : "";
    }

    /// What is true for a topic now, for him (the `when` a line must match, or "" for any).
    string TalkWhen(Soldier m, CompanyRecord k, string topic, SoldierTie? tie) => topic switch
    {
        "man_greet" => m.Hurt ? "hurt" : MoodOfCamp() switch { CampMood.Won => "won", CampMood.Lost => "lost", _ => "" },
        "man_men" => CampState(k),
        "man_home" => HomeOf(m, k) is Settlement h ? (Microsoft.Xna.Framework.Vector2.Distance(h.Pos, Player.Pos) < 60 * Map.Spread ? "near" : "far") : "",
        "man_tie" => tie?.Kind ?? "",
        _ => "",
    };

    /// The place a man comes from, if it is on the map.
    Settlement? HomeOf(Soldier m, CompanyRecord k)
    {
        string home = m.Home != "" ? m.Home : k.Home;
        return home == "" ? null : S.Settlements.FirstOrDefault(s => s.Name == home);
    }

    /// The topics you can raise with him now: always the men and his home; his first living tie.
    public IEnumerable<(string Id, string Label)> TopicsForMan(Soldier m)
    {
        yield return MenTopics[0];
        yield return MenTopics[1];
        if (m.Ties.FirstOrDefault(t => LivingMan(t.Other) != null) is SoldierTie tie)
            yield return ("man_tie", "ABOUT " + LivingMan(tie.Other)!.Name.ToUpperInvariant());
    }

    /// His line on a topic ("man_greet" for the greeting): the most fitting lines from Data/talk.json by his nature and what is
    /// true now; the same each day.
    public string ManLine(Soldier m, CompanyRecord k, string topic)
    {
        var tie = topic == "man_tie" ? m.Ties.FirstOrDefault(t => LivingMan(t.Other) != null) : null;
        string when = TalkWhen(m, k, topic, tie);
        var fits = TalkLines.All.Where(l => l.Topic == topic && (l.Nature == "" || l.Nature == m.Nature) && (l.When == "" || l.When == when)).ToList();
        if (fits.Count == 0) return "";
        int Score(TalkLine l) => (l.When != "" ? 3 : 0) + (l.Nature != "" ? 2 : 0);
        int best = fits.Max(Score);
        var top = fits.Where(l => Score(l) == best).ToList();
        var line = top[(int)(Ease.Hash(m.Id * 131 + Day * 7 + topic.Length) * top.Count) % top.Count];
        string other = tie != null ? LivingMan(tie.Other)?.Name ?? "" : "";
        return FillMan(line.Text, m, k, other);
    }

    /// A man's line or a camp event's words with the holes filled: {man}, {other}, {company}, {home}, {foe}, {you}.
    string FillMan(string text, Soldier? m, CompanyRecord? k, string other)
    {
        string home = m != null && m.Home != "" ? m.Home : k?.Home is { Length: > 0 } h ? h : "my village";
        string foe = Player.Companies.Select(c => c.Ledger.Count > 0 ? c.Ledger[^1] : null).Where(e => e != null).OrderByDescending(e => e!.Day).FirstOrDefault()?.Foe ?? "the enemy";
        return text.Replace("{man}", m?.Name ?? "one of your men").Replace("{other}", other == "" ? "his friend" : other)
            .Replace("{company}", k?.Name ?? "the company").Replace("{home}", home).Replace("{foe}", foe)
            .Replace("{you}", Captain.Contract >= 0 && this.Rank >= Ironfield.Rank.Lord ? "my lord" : "captain");
    }

    /// Problems with the men's lines in Data/talk.json (SelfTest `CampTalk`): an unknown nature or `when`, a topic or a `when`
    /// with no line any man could say.
    public static IEnumerable<string> ValidateMenTalk()
    {
        var lines = TalkLines.All.Where(l => l.Topic.StartsWith("man_")).ToList();
        foreach (var l in lines)
        {
            if (!TalkWhens.ContainsKey(l.Topic)) yield return $"unknown topic {l.Topic}";
            else if (!TalkWhens[l.Topic].Contains(l.When)) yield return $"{l.Topic}: unknown when {l.When}";
            if (l.Nature != "" && !Natures.Contains(l.Nature)) yield return $"{l.Topic}: unknown nature {l.Nature}";
            if (l.Mood != "" || l.Temper != "" || l.Outcome != "" || l.War) yield return $"{l.Topic}: a man's line takes nature and when, not mood, temper, outcome or war";
            foreach (System.Text.RegularExpressions.Match h in System.Text.RegularExpressions.Regex.Matches(l.Text, @"\{(\w+)\}"))
                if (h.Groups[1].Value is not ("man" or "other" or "company" or "home" or "foe" or "you")) yield return $"{l.Topic}: unknown placeholder {h.Value}";
        }
        foreach (var (topic, whens) in TalkWhens)
            foreach (var w in whens)
                if (!lines.Any(l => l.Topic == topic && l.When == w && l.Nature == "")) yield return $"{topic}: no plain line for when \"{w}\"";
    }
}
