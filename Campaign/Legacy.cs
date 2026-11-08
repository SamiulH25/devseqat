using Microsoft.Xna.Framework;

namespace Ironfield;

/// The company's legend (docs/LEGACY.md L5-L7): named battles and battle honours on the banner (lost when a company
/// breaks before a realm's army, won back by beating that realm), the company's fame and what it brings, and the ways
/// the story ends: retiring, or handing the company to a man who rose from its ranks.
public sealed partial class Campaign
{
    /// Battle honours a company needs, and battles, before you may retire on its legend (L7).
    public const int RetireHonours = 3, RetireBattles = 40;

    // ---------- L5: named battles, battle honours ----------

    static readonly string[] FieldWords = { "Field", "Moor", "Cross", "Heath", "Lea", "Green" };

    /// A name for a battle fought at `pos`: the nearest place and the lie of the land ("Ashby Ford", "Hollin Hill"), or the
    /// walls of a town under assault. No draw is made: the name comes from the place and the day.
    public string BattleName(Vector2 pos, bool siege = false)
    {
        var near = S.Settlements.OrderBy(t => Vector2.DistanceSquared(t.Pos, pos)).FirstOrDefault();
        if (near == null) return "the field";
        if (siege) return $"the walls of {near.Name}";
        string kind = Map.ProvinceAt(pos).Kind.ToString();
        string word = Map.RiverDist(pos) < 6 * Map.Spread ? "Ford"
            : kind.Equals("Hills", StringComparison.OrdinalIgnoreCase) || kind.Equals("Mountains", StringComparison.OrdinalIgnoreCase) ? "Hill"
            : kind.Equals("Forest", StringComparison.OrdinalIgnoreCase) ? "Wood"
            : kind.Equals("Marsh", StringComparison.OrdinalIgnoreCase) ? "Fen"
            : FieldWords[(near.Id * 7 + Day) % FieldWords.Length];
        return $"{near.Name} {word}";
    }

    /// After a battle your company fought (L5): a notable win puts the battle's name on its banner; breaking before a realm's
    /// army loses the newest one to that realm; beating that realm wins back what it took.
    internal void BannerAfterBattle(CompanyRecord c, CompanyResult cr, bool won, bool notable, string field, int foeFaction)
    {
        if (!cr.Deployed) return;
        if (won && foeFaction >= 0)
            foreach (var lost in c.LostHonours.Where(l => l.Faction == foeFaction).ToList())
            {
                c.LostHonours.Remove(lost);
                c.BattleHonours.Add(lost.Name);
                rollNews.Add(("HONOUR", $"{c.Name} win back {lost.Name} from the {FactionNames[foeFaction]}."));
                Log($"{c.Name} win back their banner honour, {lost.Name}.");
            }
        if (won && notable && c.Men > 0 && !c.BattleHonours.Contains(field))
        {
            c.BattleHonours.Add(field);
            rollNews.Add(("HONOUR", $"{field} is sewn on the banner of {c.Name}."));
            Log($"{c.Name} carry a new battle honour: {field}.");
        }
        if (!won && cr.Routed && foeFaction >= 0 && c.BattleHonours.Count > 0)
        {
            string name = c.BattleHonours[^1];
            c.BattleHonours.RemoveAt(c.BattleHonours.Count - 1);
            c.LostHonours.Add(new LostHonour { Name = name, Faction = foeFaction });
            rollNews.Add(("LOST", $"{c.Name} broke, and lost {name} to the {FactionNames[foeFaction]}. Beat them to win it back."));
            Log($"{c.Name} broke, and the {FactionNames[foeFaction]} took their banner honour, {name}.");
        }
    }

    // ---------- L6: fame ----------

    /// A company's fame: its battles, honours and battle honours.
    public static int CompanyFame(CompanyRecord c) => c.Battles + 10 * c.Honours.Count + 15 * c.BattleHonours.Count;

    /// Your fame: your three most famous companies.
    public int Fame => Player.Companies.Select(CompanyFame).OrderByDescending(f => f).Take(3).Sum();

    /// What fame brings: contracts pay more (up to half again), lords think better of you (up to +10), more of a place's
    /// men will follow you (up to a quarter more), and a famous company's own people join it knowing the work.
    public float FamePay => 1 + MathF.Min(0.5f, Fame / 400f);
    public float FameRegard => MathF.Min(10, Fame / 30f);
    public float FameShare => MathF.Min(0.25f, Fame / 800f);
    public static float FameXp(CompanyRecord c) => MathF.Min(40, CompanyFame(c) / 4f);

    /// A word for a fame, for the screens.
    public static string FameWord(int fame) => fame >= 300 ? "LEGENDARY" : fame >= 150 ? "FAMOUS" : fame >= 60 ? "KNOWN" : fame >= 20 ? "SPOKEN OF" : "UNKNOWN";

    // ---------- L7: how the story ends ----------

    /// The company whose legend you could retire on, if any.
    public CompanyRecord? LegendCompany => Player.Companies.Where(c => c.BattleHonours.Count >= RetireHonours && c.Battles >= RetireBattles)
        .OrderByDescending(CompanyFame).FirstOrDefault();

    /// Why you may not retire yet (null: you may).
    public string? CannotRetire() => S.GameOver ? "THE WAR IS OVER" : Encounter != null ? "NOT WITH AN ENEMY IN FRONT OF YOU"
        : LegendCompany == null ? $"A COMPANY NEEDS {RetireHonours} BATTLE HONOURS AND {RetireBattles} BATTLES" : null;

    /// The man who would take command if you handed over: a companion who rose from the ranks first, then any companion.
    public Character? Successor => Companions.Where(k => S.Hours >= k.WoundedUntil).OrderByDescending(k => k.Ranks != null).ThenByDescending(k => k.Ranks?.Battles ?? 0).FirstOrDefault();

    /// Retire on the company's legend: the campaign is won, and its story is the end summary.
    public string? Retire()
    {
        if (CannotRetire() is string why) return why;
        var c = LegendCompany!;
        Deed($"{Me.Name} hangs up the sword. {c.Name} ride on under {N(c.BattleHonours.Count, "battle honour")}.");
        S.Won = true;
        S.Retired = true;
        S.GameOver = true;
        S.EndReason = $"YOU HANG UP YOUR SWORD. {c.Name.ToUpperInvariant()} RIDE ON WITHOUT YOU.";
        return null;
    }

    /// Hand the company to your successor and play on as him: the same company, a new captain who knew its men. Half your
    /// renown goes with you; your kit and the company stay.
    public string? HandOver()
    {
        if (CannotRetire() is string why) return why;
        if (Successor is not Character heir) return "NO ONE TO TAKE COMMAND: A COMPANION MUST";
        var c = LegendCompany!;
        string old = Me.Name;
        Deed($"{old} hangs up the sword, and gives {c.Name} to {heir.Name}.");
        Me.Name = heir.Name;
        Me.Age = heir.Age;
        Me.Traits = heir.Traits.ToList();
        heir.Dead = true; // he is the captain now, not a companion
        heir.Kind = "retired";
        foreach (var co in Player.Companies.Where(co => co.Id == heir.CompanyId)) co.Hero = "";
        Captain.Renown *= 0.5f;
        Captain.WoundedUntil = 0;
        S.Generation++;
        S.Won = true; // the legend counts as won; play on
        RefreshHeroes();
        return null;
    }
}
