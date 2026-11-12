namespace Ironfield;

/// A man of his own (docs/COMPANY.md C2): each named man has a nature, drawn when he is named and weighted by the deed that
/// named him. His company feels one nature, its senior man's (most battles, not hurt): six through a small battle effect
/// (`nature_*` in Data/effects.json, carried as the company's standing), Merry in camp (cohesion heals faster) and Gentle on
/// a lost field (more of the fallen live). Natures are drawn on the roll's own dice, so the main stream never moves.
public sealed partial class Campaign
{
    public static readonly string[] Natures = { "Steady", "Bold", "Grim", "Merry", "Pious", "Sly", "Proud", "Gentle" };

    /// A nature for a man named for this deed: the deed tips it (a man who stood is likelier steady, one who killed bold or
    /// grim), any nature can come.
    string DrawNature(string deed)
    {
        var w = Natures.ToDictionary(n => n, _ => 1f);
        void Tip(string n, float by) => w[n] += by;
        if (deed.Contains("Stood") || deed.Contains("first to take")) Tip("Steady", 2);
        if (deed.StartsWith("Killed") || deed.Contains("front rank") || deed.Contains("First over")) { Tip("Bold", 1.5f); Tip("Proud", 1); }
        if (deed.Contains("Cut down") || deed.Contains("lost field")) Tip("Grim", 2);
        if (deed.Contains("horseman")) Tip("Sly", 1.5f);
        float roll = RollRng.Float() * w.Values.Sum();
        foreach (var n in Natures) { roll -= w[n]; if (roll <= 0) return n; }
        return Natures[^1];
    }

    /// An old save's named men had no nature: one from their id, without touching any dice.
    internal void FillNatures()
    {
        var mine = S.Parties.Where(p => p.Kind == PartyKind.Player).SelectMany(p => p.Companies); // none yet while a new campaign is made
        foreach (var m in mine.SelectMany(k => k.Roll).Concat(S.Fallen))
            if (m.Nature == "") m.Nature = Natures[(int)((uint)(m.Id * 2654435761u) % (uint)Natures.Length)];
    }

    /// The man whose nature his company feels: the most battles among the fit, the earliest named on a tie.
    public static Soldier? Senior(CompanyRecord c) => c.Roll.Where(m => !m.Hurt).OrderByDescending(m => m.Battles).ThenBy(m => m.Id).FirstOrDefault();

    public static string SeniorNature(CompanyRecord c) => Senior(c)?.Nature ?? "";

    /// The battle effect of a company's senior nature, or null (Merry and Gentle act off the field).
    public static string? NatureEffect(CompanyRecord c) => SeniorNature(c) is { Length: > 0 } n && Effects.Has("nature_" + n.ToLowerInvariant()) ? "nature_" + n.ToLowerInvariant() : null;

    /// One line of what a nature does, for the roll's tooltip.
    public static string NatureLine(string nature) => nature switch
    {
        "Merry" => "Keeps the camp in good heart: cohesion mends faster in a town.",
        "Gentle" => "Carries the hurt off: more of the company's fallen live.",
        _ => Effects.Has("nature_" + nature.ToLowerInvariant()) ? Effects.Get("nature_" + nature.ToLowerInvariant()).Line : "",
    };
}
