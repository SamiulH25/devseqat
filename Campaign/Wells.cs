using Microsoft.Xna.Framework;

namespace Ironfield;

/// Wells: the arcane map (docs/ARCANA.md AR2). Holy and old places are wells of power with a tradition (faith: a shrine,
/// a spring, a hermit's cell; craft: standing stones, a ruin, an old battlefield) and a strength 1-3. A well can be
/// consecrated (faith) or bound (craft): it turns to that tradition and grows a step stronger. The strongest well near a
/// spot is its Tide: it decides where circles are for hire, and (AR3) how much power a battle fought there holds.
/// Wells are read from the map's sites, so every map and every old save has them; only the marks are saved.
public sealed partial class Campaign
{
    /// A site kind's tradition as a well ("" = not a well).
    public static string WellTradition(string kind) => kind switch
    {
        "shrine" or "spring" or "hermit" => "faith",
        "stones" or "ruin" or "battlefield" => "craft",
        _ => "",
    };

    /// A well's own strength (1-3), fixed for the site: most are faint, some steady, few strong. Shrines and stones,
    /// the places built for it, are a step stronger at the bottom.
    public static int WellBase(Site s)
    {
        if (WellTradition(s.Kind) == "") return 0;
        int roll = (int)((uint)(s.Id * 2654435761u) >> 24) % 10; // the site's own number, not the campaign's dice
        int n = roll < 6 ? 1 : roll < 9 ? 2 : 3;
        return s.Kind is "shrine" or "stones" ? Math.Min(3, n + (roll % 2)) : n;
    }

    /// The well a site is now: its tradition and strength, as marked.
    public (string Tradition, int Strength) Well(Site s)
    {
        string own = WellTradition(s.Kind);
        if (own == "") return ("", 0);
        int b = WellBase(s);
        if (!S.WellMarks.TryGetValue(s.Id, out var m)) return (own, b);
        return (m.Tradition, Math.Min(3, b + 1)); // turned or strengthened: either way a step up
    }

    /// How far a well's tide reaches (times `Map.Spread`): its full strength within it, a step less within twice it.
    public const float WellReach = 25;

    /// The Tide at a spot: the tradition and strength (0-3) of the strongest well that reaches it, and that well.
    public (string Tradition, int Strength, Site? Well) TideAt(Vector2 p)
    {
        (string, int, Site?) best = ("", 0, null);
        float bestD = float.MaxValue, reach = WellReach * Map.Spread;
        foreach (var s in Sites)
        {
            var (t, n) = Well(s);
            if (n == 0) continue;
            float d = Vector2.Distance(s.Pos, p);
            if (d > reach * 2) continue;
            int at = d <= reach ? n : n - 1;
            if (at <= 0) continue;
            if (at > best.Item2 || at == best.Item2 && d < bestD) { best = (t, at, s); bestD = d; }
        }
        return best;
    }

    /// A word for a strength.
    public static string TideWord(int n) => n switch { >= 3 => "STRONG", 2 => "STEADY", 1 => "FAINT", _ => "NO" };

    /// The well you stand at, if any.
    public Site? WellHere => SiteAt(Player.Pos, 6 * Map.Spread) is Site s && WellTradition(s.Kind) != "" ? s : null;

    /// Why you cannot consecrate (faith) or bind (craft) the well you stand at; null if you can. Rite costs are checked by `Rite`.
    public string? CannotMarkWell(string tradition)
    {
        if (WellHere is not Site s) return "STAND AT A SHRINE, SPRING, HERMIT'S CELL, STONES, RUIN OR OLD BATTLEFIELD";
        var (t, n) = Well(s);
        if (S.WellMarks.TryGetValue(s.Id, out var m) && m.Tradition == tradition) return tradition == "faith" ? "THIS WELL IS CONSECRATED ALREADY" : "THIS WELL IS BOUND ALREADY";
        var pr = S.Provinces.FirstOrDefault(p => p.Id == s.Province);
        bool ours = pr != null && (pr.Faction == Captain.Contract && Captain.Contract >= 0 || Fiefs.Any(f => f.Province == s.Province));
        if (!ours) return "ONLY IN A PROVINCE OF YOUR REALM";
        return null;
    }

    /// The rite's work: the well turns to `tradition` and grows a step, marked by your realm.
    void MarkWell(string tradition)
    {
        var s = WellHere!;
        S.WellMarks[s.Id] = new WellMark { Tradition = tradition, Faction = Captain.Contract, Day = Day };
        var (_, n) = Well(s);
        Deed(tradition == "faith" ? $"You consecrate the {s.Name}: a {TideWord(n).ToLowerInvariant()} well of the faith." : $"You bind the {s.Name} to the craft: a {TideWord(n).ToLowerInvariant()} well.", "wells");
    }

    /// A line on the tide at a spot for the encounter panel and the map ("" where there is none).
    public string TideNote(Vector2 p)
    {
        var (t, n, w) = TideAt(p);
        return n == 0 || w == null ? "" : $"A {TideWord(n)} WELL OF THE {t.ToUpperInvariant()} NEAR: {w.Name.ToUpperInvariant()}.";
    }
}
