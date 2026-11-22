using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// The events' conditions (docs/pre-demo/EVENTS.md): a small language, `key`, `!key`, `key:arg`, `key<op>number` (op one of
/// < <= > >= = !=). Each key is one entry in a table that gives a number (true/false as 1/0); a bare key holds when it is not 0.
/// The global table reads the captain, the party, the place, the time and what the events remember; each role of a cast has
/// its own table, read on a candidate (a lord, a named man, a settlement...). Also the checks' odds.
public sealed partial class Campaign
{
    readonly record struct Cond(bool Not, string Key, string Arg, string Op, float N);

    static readonly Regex CondPattern = new(@"^(!?)([a-z_]+)(?::([A-Za-z0-9_.\-]+))?(?:(<=|>=|!=|<|>|=)(-?[0-9]+(?:\.[0-9]+)?))?$", RegexOptions.Compiled);

    static Cond? ParseCond(string text)
    {
        var m = CondPattern.Match(text.Trim());
        if (!m.Success) return null;
        return new Cond(m.Groups[1].Value == "!", m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value,
            m.Groups[5].Success ? float.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture) : 0);
    }

    static bool Compare(float v, Cond c) => c.Op switch
    {
        "" => c.Not ? v == 0 : v != 0,
        "<" => v < c.N, "<=" => v <= c.N, ">" => v > c.N, ">=" => v >= c.N, "=" => MathF.Abs(v - c.N) < 1e-4f, _ => MathF.Abs(v - c.N) >= 1e-4f,
    };

    static float B01(bool b) => b ? 1 : 0;

    /// A global key: does it need an argument, and its value now.
    sealed record CondKey(bool Arg, Func<Campaign, PendingEvent, string, float> Value);

    static readonly Dictionary<string, CondKey> GlobalConds = new()
    {
        // Time.
        ["season"] = new(true, (c, _, a) => B01(c.Season.Equals(a, StringComparison.OrdinalIgnoreCase))),
        ["night"] = new(false, (c, _, _) => B01(c.S.Hours % 24 is >= 20 or < 5)),
        ["hour"] = new(false, (c, _, _) => (float)(c.S.Hours % 24)),
        ["day"] = new(false, (c, _, _) => c.Day),
        // The party.
        ["gold"] = new(false, (c, _, _) => c.S.Gold),
        ["food"] = new(false, (c, _, _) => c.Player.Food),
        ["men"] = new(false, (c, _, _) => c.Player.Men),
        ["wounded"] = new(false, (c, _, _) => c.Player.Wounded),
        ["horses"] = new(false, (c, _, _) => c.Player.Horses),
        ["prisoners"] = new(false, (c, _, _) => c.PrisonerCount),
        ["renown"] = new(false, (c, _, _) => c.Captain.Renown),
        ["cohesion"] = new(false, (c, _, _) => c.Player.Companies.Count == 0 ? 0 : c.Player.Companies.Average(k => k.Cohesion)),
        ["unpaid"] = new(false, (c, _, _) => c.S.UnpaidWeeks),
        ["omen"] = new(false, (c, _, _) => c.S.Omen),
        ["companions"] = new(false, (c, _, _) => c.Companions.Count()),
        ["named"] = new(false, (c, _, _) => c.Player.Companies.Sum(k => k.Roll.Count(m => !m.Hurt))),
        ["companies"] = new(false, (c, _, _) => c.Player.Companies.Count),
        ["fiefs"] = new(false, (c, _, _) => c.Fiefs.Count()),
        ["relic"] = new(false, (c, _, _) => B01(c.S.Relic)),
        ["battle_hours"] = new(false, (c, _, _) => (float)(c.S.Hours - c.S.LastBattleAt)),
        // The captain.
        ["rank"] = new(false, (c, _, _) => (int)c.Rank),
        ["contract"] = new(false, (c, _, _) => B01(c.Captain.Contract >= 0)),
        ["fealty"] = new(false, (c, _, _) => B01(c.Captain.Fealty)),
        ["at_war"] = new(false, (c, _, _) => B01(c.Captain.Contract >= 0 && c.S.Wars.Any(w => w.Active && (w.A == c.Captain.Contract || w.B == c.Captain.Contract)))),
        ["exhaustion"] = new(false, (c, _, _) => c.Captain.Contract >= 0 ? c.Exhaustion(c.Captain.Contract) : 0),
        ["trait"] = new(true, (c, _, a) => B01(c.Me.Traits.Contains(a))),
        ["skill"] = new(true, (c, _, a) => c.Skill(a)),
        ["origin"] = new(true, (c, _, a) => B01(c.Captain.Birth == a || c.Captain.Origin == a)),
        ["path"] = new(true, (c, _, a) => B01(c.Captain.Path == a)),
        ["sworn"] = new(false, (c, _, _) => B01(c.Captain.Order != "")),
        ["aura"] = new(false, (c, _, _) => c.Captain.AuraTier),
        ["wall"] = new(false, (c, _, _) => B01(c.Captain.AuraWall)),
        ["corruption"] = new(false, (c, _, _) => c.Captain.Corruption),
        ["pact"] = new(false, (c, _, _) => B01(c.Captain.Pact)),
        ["rival"] = new(false, (c, _, _) => B01(c.CharacterById(c.Captain.Rival) is { Dead: false })),
        // The place.
        ["in"] = new(false, (c, _, a) => c.SettlementAt(c.Player.Pos) is Settlement s && (a == "" || s.Kind.ToString().Equals(a, StringComparison.OrdinalIgnoreCase)) ? 1 : 0),
        ["land"] = new(true, (c, _, a) => B01(c.Map.ProvinceAt(c.Player.Pos).Kind.Equals(a, StringComparison.OrdinalIgnoreCase))),
        ["river"] = new(false, (c, _, _) => B01(c.Map.RiverDist(c.Player.Pos) < 6 * c.Map.Spread)),
        ["road"] = new(false, (c, _, _) => B01(c.Map.RoadDist(c.Player.Pos) < 3 * c.Map.Spread)),
        ["sea"] = new(false, (c, _, _) => B01(c.Map.NearSea(c.Player.Pos))),
        ["moving"] = new(false, (c, _, _) => B01(c.Player.Moving)),
        ["people"] = new(true, (c, _, a) => c.S.Settlements.OrderBy(s => Vector2.DistanceSquared(s.Pos, c.Player.Pos)).FirstOrDefault() is Settlement s
            && (s.Culture == a || Factions.PeopleOf(s.Culture) == a) ? 1 : 0),
        ["own_land"] = new(false, (c, _, _) => B01(c.Captain.Contract >= 0 && c.Map.ProvinceAt(c.Player.Pos) is var p && c.S.Settlements.Where(s => c.Map.ProvinceAt(s.Pos) == p).Any(s => s.Faction == c.Captain.Contract))),
        // What the events remember.
        ["flag"] = new(true, (c, _, a) => B01(c.S.Flags.ContainsKey(a))),
        ["seen"] = new(true, (c, _, a) => B01(c.S.EventsFired.ContainsKey(a))),
        ["days_since"] = new(true, (c, _, a) => c.S.EventsFired.TryGetValue(a, out float at) ? (float)((c.S.Hours - at) / 24) : 9999),
        ["chose"] = new(true, (c, _, a) => a.LastIndexOf('.') is int dot and > 0 && int.TryParse(a[(dot + 1)..], out int n)
            && c.S.EventHistory.Any(h => h.Id == a[..dot] && h.Choice == n - 1) ? 1 : 0),
        ["cast"] = new(true, (c, pe, a) => B01(c.RoleBound(a, pe))),
        ["at_site"] = new(true, (c, pe, a) => B01(c.SiteById(pe.Site)?.Kind == a)),
    };

    /// Is a role of the cast filled?
    internal bool RoleBound(string role, PendingEvent pe) => role switch
    {
        "lord" => CharacterById(pe.Lord) != null,
        "companion" => CharacterById(pe.Companion) is { Dead: false },
        "man" => LivingMan(pe.Soldier) != null,
        "other" => LivingMan(pe.Other) != null,
        "settlement" => pe.Settlement >= 0,
        "site" => SiteById(pe.Site) != null,
        "band" => PartyById(pe.Party) is { Down: false },
        "company" => Player.Companies.Any(k => k.Id == pe.Company),
        _ => false,
    };

    /// What is wrong with a condition, or null: unknown key, an argument missing or not wanted, a trait or skill that does not exist.
    static string? CondProblem(string text, string? role)
    {
        if (ParseCond(text) is not Cond c) return $"cannot read the condition \"{text}\"";
        if (role != null)
        {
            var table = CastConds.GetValueOrDefault(role == "other" ? "man" : role);
            if (table == null || !table.TryGetValue(c.Key, out var ck)) return $"unknown condition {c.Key} for a {role}";
            if (ck.Arg != (c.Arg != "")) return ck.Arg ? $"{c.Key} needs an argument" : $"{c.Key} takes no argument";
            if (c.Key == "nature" && !Natures.Contains(c.Arg)) return $"unknown nature {c.Arg}";
            return null;
        }
        if (!GlobalConds.TryGetValue(c.Key, out var g)) return $"unknown condition {c.Key}";
        if (g.Arg && c.Arg == "") return $"{c.Key} needs an argument";
        if (!g.Arg && c.Arg != "" && c.Key != "in") return $"{c.Key} takes no argument";
        if (c.Key == "trait" && !EarnedTraits.Contains(c.Arg)) return $"unknown trait {c.Arg}";
        if (c.Key == "skill" && Skills.All.All(s => s.Id != c.Arg)) return $"unknown skill {c.Arg}";
        if (c.Key == "origin" && Origins.Births.Concat(Origins.Pasts).All(o => o.Id != c.Arg)) return $"unknown origin {c.Arg}";
        if (c.Key == "season" && !Seasons.Any(s => s.Equals(c.Arg, StringComparison.OrdinalIgnoreCase))) return $"unknown season {c.Arg}";
        if (c.Key == "cast" && !Roles.Contains(c.Arg)) return $"unknown role {c.Arg}";
        if (c.Key == "in" && c.Arg != "" && !Enum.GetNames<SettlementKind>().Any(k => k.Equals(c.Arg, StringComparison.OrdinalIgnoreCase))) return $"unknown place {c.Arg}";
        return null;
    }

    /// Does a global condition hold now, for this event's cast?
    public bool Holds(string text, PendingEvent? pe = null)
    {
        if (ParseCond(text) is not Cond c || !GlobalConds.TryGetValue(c.Key, out var k)) return false;
        return Compare(k.Value(this, pe ?? new PendingEvent(), c.Arg), c);
    }

    bool HoldsAll(IEnumerable<string> conds, PendingEvent pe) => conds.All(w => Holds(w, pe));

    /// Why a need fails, in a few words for the struck-through choice.
    static string NeedText(string text)
    {
        if (ParseCond(text) is not Cond c) return "NOT NOW";
        string n = c.N.ToString("0", CultureInfo.InvariantCulture), arg = c.Arg.ToUpperInvariant();
        bool atLeast = c.Op is ">=" or ">";
        return c.Key switch
        {
            "gold" when atLeast => $"NEEDS {n} CROWNS",
            "food" when atLeast => $"NEEDS {n} DAYS OF FOOD",
            "horses" when atLeast => $"NEEDS {n} HORSES",
            "men" when atLeast => $"NEEDS {n} MEN",
            "renown" when atLeast => $"NEEDS {n} RENOWN",
            "skill" when atLeast => $"NEEDS {arg} {n}",
            "trait" => c.Not ? $"NOT FOR THE {arg}" : $"ONLY FOR THE {arg}",
            "companions" => "NEEDS A COMPANION",
            "named" => "NEEDS A NAMED MAN",
            "relic" => c.Not ? "NOT WITH THE RELIC" : "NEEDS THE RELIC",
            "fiefs" => "NEEDS A FIEF",
            "sworn" => c.Not ? "NOT FOR A SWORN KNIGHT" : "ONLY FOR A SWORN KNIGHT",
            "contract" => c.Not ? "NOT UNDER CONTRACT" : "NEEDS A CONTRACT",
            "in" => "ONLY IN A SETTLEMENT",
            "prisoners" => "NEEDS PRISONERS",
            _ => "NOT NOW",
        };
    }

    // ---------- the cast's conditions ----------

    /// A role's key: does it need an argument, and its value on a candidate.
    sealed record CastKey(bool Arg, Func<Campaign, object, PendingEvent, string, float> Value);

    static float Lord(object o, Func<Character, float> f) => o is Character ch ? f(ch) : 0;
    static float Man(object o, Func<Soldier, float> f) => o is (Soldier m, CompanyRecord) ? f(m) : 0;

    static readonly Dictionary<string, Dictionary<string, CastKey>> CastConds = new()
    {
        ["lord"] = new()
        {
            ["near"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.PartyById(ch.PartyId) is Party p && Vector2.Distance(p.Pos, c.Player.Pos) < 60 * c.Map.Spread))),
            ["opinion"] = new(false, (c, o, _, _) => Lord(o, c.OpinionOfYou)),
            ["friend"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.OpinionOfYou(ch) >= 35))),
            ["cold"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.OpinionOfYou(ch) <= -20))),
            ["realm"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.Captain.Contract >= 0 && ch.Faction == c.Captain.Contract))),
            ["enemy"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.Captain.Contract >= 0 && c.FactionsAtWar(ch.Faction, c.Captain.Contract)))),
            ["ruler"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.RulerOf(ch.Faction) == ch))),
            ["hates_liege"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.RulerOf(c.Captain.Contract) is Character r && r != ch && c.OpinionOf(ch, r) <= -20))),
            ["trait"] = new(true, (c, o, _, a) => Lord(o, ch => B01(ch.Traits.Contains(a)))),
            ["free"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.PartyById(ch.PartyId) is { Captive: false, Down: false }))),
        },
        ["companion"] = new()
        {
            ["calling"] = new(true, (c, o, _, a) => Lord(o, ch => B01(ch.Role == a))),
            ["level"] = new(false, (c, o, _, _) => Lord(o, ch => ch.Level)),
            ["fit"] = new(false, (c, o, _, _) => Lord(o, ch => B01(c.S.Hours >= ch.WoundedUntil))),
        },
        ["man"] = new()
        {
            ["nature"] = new(true, (c, o, _, a) => Man(o, m => B01(m.Nature.Equals(a, StringComparison.OrdinalIgnoreCase)))),
            ["battles"] = new(false, (c, o, _, _) => Man(o, m => m.Battles)),
            ["veteran"] = new(false, (c, o, _, _) => Man(o, m => B01(m.Battles >= 10))),
            ["old"] = new(false, (c, o, _, _) => Man(o, m => B01(Soldier.AgeOf(m) >= 40 || m.Wounds >= 3))),
            ["young"] = new(false, (c, o, _, _) => Man(o, m => B01(Soldier.AgeOf(m) <= 21))),
            ["home"] = new(false, (c, o, _, _) => Man(o, m => B01(m.Home != "" && c.S.Settlements.Any(s => s.Name == m.Home)))),
            ["avenging"] = new(false, (c, o, _, _) => Man(o, m => B01(m.Avenging))),
            ["hurt"] = new(false, (c, o, _, _) => Man(o, m => B01(m.Hurt))),
            ["mentions"] = new(false, (c, o, _, _) => Man(o, m => m.Mentions)),
            ["tie"] = new(true, (c, o, _, a) => Man(o, m => B01(m.Ties.Any(t => TieKind(t.Kind, a) && c.LivingMan(t.Other) is { Hurt: false })))),
        },
        ["settlement"] = new()
        {
            ["here"] = new(false, (c, o, _, _) => o is Settlement s && c.SettlementAt(c.Player.Pos) == s ? 1 : 0),
            ["near"] = new(false, (c, o, _, _) => o is Settlement s && Vector2.Distance(s.Pos, c.Player.Pos) < 40 * c.Map.Spread ? 1 : 0),
            ["fief"] = new(false, (c, o, _, _) => o is Settlement s && s.Owner == c.Player.Id ? 1 : 0),
            ["kind"] = new(true, (c, o, _, a) => o is Settlement s && s.Kind.ToString().Equals(a, StringComparison.OrdinalIgnoreCase) ? 1 : 0),
            ["foreign"] = new(false, (c, o, _, _) => o is Settlement s && s.Culture != c.Me.Culture && s.Culture != "common" ? 1 : 0),
            ["unrest"] = new(false, (c, o, _, _) => o is Settlement s ? s.Unrest : 0),
            ["prosperity"] = new(false, (c, o, _, _) => o is Settlement s ? s.Prosperity : 0),
            ["open"] = new(false, (c, o, _, _) => o is Settlement s && !c.Refuses(s) ? 1 : 0),
            ["realm"] = new(false, (c, o, _, _) => o is Settlement s && c.Captain.Contract >= 0 && s.Faction == c.Captain.Contract ? 1 : 0),
        },
        ["site"] = new()
        {
            ["near"] = new(false, (c, o, _, _) => o is Site s && Vector2.Distance(s.Pos, c.Player.Pos) < 40 * c.Map.Spread ? 1 : 0),
            ["unseen"] = new(false, (c, o, _, _) => o is Site s && !c.SiteSeen(s) ? 1 : 0),
            ["kind"] = new(true, (c, o, _, a) => o is Site s && s.Kind == a ? 1 : 0),
        },
        ["band"] = new()
        {
            ["near"] = new(false, (c, o, _, _) => o is Party p && Vector2.Distance(p.Pos, c.Player.Pos) < 80 * c.Map.Spread ? 1 : 0),
            ["men"] = new(false, (c, o, _, _) => o is Party p ? p.Men : 0),
            ["big"] = new(false, (c, o, _, _) => o is Party p && p.Men >= c.Scaled(35) ? 1 : 0),
        },
        ["company"] = new()
        {
            ["veteran"] = new(false, (c, o, _, _) => o is CompanyRecord k && k.Battles >= 12 && k.Cohesion >= 18 ? 1 : 0),
            ["battles"] = new(false, (c, o, _, _) => o is CompanyRecord k ? k.Battles : 0),
            ["cohesion"] = new(false, (c, o, _, _) => o is CompanyRecord k ? k.Cohesion : 0),
            ["men"] = new(false, (c, o, _, _) => o is CompanyRecord k ? k.Men : 0),
        },
    };

    /// A tie of the kind the data names: "Brothers" takes kin too.
    static bool TieKind(string kind, string want) => kind == want || want == "Brothers" && kind == "Kin";

    /// Does a candidate meet every condition of its role?
    bool Fits(string role, object candidate, IEnumerable<string> where, PendingEvent pe)
    {
        var table = CastConds[role == "other" ? "man" : role];
        foreach (var w in where)
        {
            if (ParseCond(w) is not Cond c || !table.TryGetValue(c.Key, out var k)) return false;
            if (!Compare(k.Value(this, candidate, pe, c.Arg), c)) return false;
        }
        return true;
    }

    // ---------- checks ----------

    /// The odds of a check, as rolled (EV2): the score of what it tests against its difficulty.
    public float Odds(CheckDef k, PendingEvent pe)
    {
        float score = 50;
        if (k.Skill != "") score = SkillScoreBase + Skill(k.Skill); // a new captain's 5-15 reads 25-35: against 35, unlikely to even
        else if (k.Man is "man" or "other" && LivingMan(k.Man == "man" ? pe.Soldier : pe.Other) is Soldier m)
            score = 25 + 4 * Math.Min(m.Battles, 12) + 5 * Math.Min(m.Mentions, 3) - 8 * Math.Min(m.Wounds, 2)
                + (k.Nature != "" && m.Nature.Equals(k.Nature, StringComparison.OrdinalIgnoreCase) ? 20 : 0);
        else if (k.Man == "companion" && CharacterById(pe.Companion) is Character ch)
            score = 40 + 5 * ch.Level;
        if (k.Trait != "")
        {
            bool against = k.Trait.StartsWith('!');
            if (Me.Traits.Contains(k.Trait.TrimStart('!'))) score += against ? -EventTraitEdge : EventTraitEdge;
        }
        float floor = B.EventCheckFloor;
        return Math.Clamp(0.5f + (score - k.Against) / B.EventCheckSpread, floor, 1 - floor);
    }

    /// What a skill check adds to the captain's skill before it meets the check's `against`.
    public const float SkillScoreBase = 20;

    /// What a trait the check names is worth.
    public const float EventTraitEdge = 20;

    /// The odds as a word: what the page shows beside the bar.
    public static string OddsWord(float p) => p >= 0.85f ? "NEAR SURE" : p >= 0.65f ? "LIKELY" : p >= 0.45f ? "EVEN" : p >= 0.25f ? "UNLIKELY" : "DESPERATE";

    /// What a check tests, in a few words: "ARMS 34", "GIB THE STERN", "LIONHEART".
    public string CheckText(CheckDef k, PendingEvent pe)
    {
        var parts = new List<string>();
        if (k.Skill != "") parts.Add($"{(Skills.All.FirstOrDefault(s => s.Id == k.Skill)?.Name ?? k.Skill).ToUpperInvariant()} {Skill(k.Skill):0}");
        if (k.Man is "man" or "other") parts.Add((LivingMan(k.Man == "man" ? pe.Soldier : pe.Other)?.Name ?? "HIM").ToUpperInvariant());
        if (k.Man == "companion") parts.Add((CharacterById(pe.Companion)?.Name ?? "YOUR COMPANION").ToUpperInvariant());
        if (k.Trait != "" && Me.Traits.Contains(k.Trait.TrimStart('!'))) parts.Add(k.Trait.TrimStart('!').ToUpperInvariant());
        return string.Join(", ", parts);
    }
}
