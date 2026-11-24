using Microsoft.Xna.Framework;

namespace Ironfield;

/// Noble recruits (docs/pre-demo/NOBILITY.md N2): the only way a man of noble stock joins you besides knighting (N3). A castle's house
/// sends esquires with a captain it trusts: sworn to its realm (or its holder), and renowned enough. They are noble by
/// birth, so they train up the noble lines; commoners never do (CanEnterLine).
public partial class Campaign
{
    public const string EsquireFoot = "esquires", EsquireHorse = "mounted_esquires";

    /// The troop a castle's house sends: on foot or mounted.
    public static string NobleTroop(bool mounted) => mounted ? EsquireHorse : EsquireFoot;

    /// Why this castle will not send esquires with you now; null if it will.
    public string? CannotRecruitNobles(Settlement s)
    {
        if (s.Kind != SettlementKind.Castle) return "ONLY CASTLES RAISE ESQUIRES";
        bool ours = s.Owner == Player.Id || Captain.Contract >= 0 && s.Faction == Captain.Contract;
        if (!ours) return "THE HOUSE SENDS ESQUIRES ONLY WITH ITS OWN CAPTAINS";
        float need = MathF.Max(40, B.NobleRenown - (Captain.Birth == "highborn" || Captain.Origin == "disinherited" ? 40 : 0));
        if (Captain.Renown < need) return $"NEEDS {need:0} RENOWN (YOU HAVE {Captain.Renown:0})";
        if (s.Nobles <= 0) return "NO ESQUIRES READY";
        return null;
    }

    /// Crowns for one batch of ArmyScale esquires (shown in the castle's recruit tab).
    public int NoblePrice(bool mounted) => (int)MathF.Ceiling(Troops.Get(NobleTroop(mounted)).Cost * B.NobleMarkup);

    /// Take up to `n` esquires from this castle into companies of their role, or a new one.
    public string? RecruitNobles(Settlement s, bool mounted, int n)
    {
        if (CannotRecruitNobles(s) is string why) return why;
        string troop = NobleTroop(mounted), role = mounted ? "horse" : "infantry";
        n = Math.Min(n, s.Nobles);
        float each = Rules.PerMan(Troops.Get(troop).Cost) * B.NobleMarkup;
        int cost = (int)MathF.Ceiling(n * each);
        if (S.Gold < cost) return $"NEEDS {cost} CROWNS";
        if (n > RoomForMen) return "PARTY IS FULL (RENOWN RAISES THE LIMIT)";
        int left = n;
        void Fill(IEnumerable<CompanyRecord> into)
        {
            foreach (var co in into.Where(co => Rules.RoleOf(co) == role && Rules.Fits(co, troop)).OrderByDescending(co => co.Stacks.Any(x => x.Troop == troop)).ToList())
            {
                int k = Math.Min(left, Rules.Room(co, troop));
                if (k <= 0) continue;
                Enlist(co, troop, k, s);
                left -= k;
                if (left == 0) break;
            }
        }
        Fill(Player.Companies.Where(co => co.Culture == s.Culture || co.Culture == ""));
        while (left > 0 && Player.Companies.Count < Slots)
        {
            var co = NewCompany(troop, role, 0, 0, s);
            Player.Companies.Add(co);
            int k = Math.Min(left, Rules.Room(co, troop));
            Enlist(co, troop, k, s);
            left -= k;
        }
        if (left > 0) Fill(Player.Companies);
        int took = n - left;
        if (took == 0) return $"MAX {Slots} COMPANIES, ALL FULL";
        s.Nobles -= took;
        S.Gold -= MathF.Ceiling(took * each);
        Log($"{took} {Troops.Get(troop).Name.ToLowerInvariant()} ride out of {s.Name} under your banner.");
        return null;
    }

    // ---------- N3: knighting a named veteran ----------

    public const int KnightingBattles = 6, KnightCost = 350;
    public const float KnightRenown = 120;

    /// The noble upgrade a named commoner can be knighted into (ignores the common->noble stock gate, keeps culture/barracks/order gates).
    public string? KnightTarget(CompanyRecord c, Soldier m)
    {
        var cur = Troops.Get(m.Troop);
        if (cur.Noble) return null;
        foreach (var to in cur.UpgradesTo)
        {
            var nt = Troops.Get(to);
            if (!nt.Noble || nt.Order != "") continue;
            string man = ManCulture(c, m.Troop);
            if (nt.Culture != Factions.Common && man != Factions.Common && !Factions.Open(man, nt.Culture)) continue;
            if (nt.Tier >= 5 && nt.Culture != Factions.Common && !HasBarracks(Player, nt.Culture) && !(man != nt.Culture && man != Factions.Common && HasBarracks(Player, man))) continue;
            return to;
        }
        return null;
    }

    /// Why this named veteran cannot be knighted now; null if he can (N3).
    public string? CannotKnight(CompanyRecord c, Soldier m)
    {
        if (!c.Roll.Contains(m) || m.Hurt) return "HE MUST BE FIT AND ON THE ROLL";
        if (Troops.Get(m.Troop).Noble) return "HE IS ALREADY OF NOBLE RANK";
        if (KnightTarget(c, m) == null) return "NO NOBLE UPGRADE FROM THIS TROOP";
        if (!c.Stacks.Any(s => s.Troop == m.Troop && s.Men > 0)) return "HE IS NOT IN THE RANKS";
        if (m.Battles < KnightingBattles && !OfBanner(m)) return $"NEEDS {KnightingBattles} BATTLES (OR OF THE BANNER)";
        if (Captain.Renown < KnightRenown) return $"NEEDS {KnightRenown:0} RENOWN";
        if (S.Gold < KnightCost) return $"NEEDS {KnightCost} CROWNS";
        return null;
    }

    /// Knight one named veteran into his noble upgrade (N3): bypasses the common->noble gate for that man alone.
    public string? KnightMan(CompanyRecord c, Soldier m)
    {
        if (CannotKnight(c, m) is string why) return why;
        string target = KnightTarget(c, m)!;
        var st = c.Stacks.First(s => s.Troop == m.Troop && s.Men > 0);
        S.Gold -= KnightCost;
        st.Take(1);
        Rules.AddMen(c, target, 1);
        c.Prune();
        m.Troop = target;
        m.Knight = true;
        string title = Troops.Get(target).Name;
        m.Deed($"Knighted as {title} on day {Day}.");
        c.Cohesion = MathF.Min(30, c.Cohesion + 2);
        Deed($"Knighted {m.Name} of {c.Name} as {title}.", "company");
        RecordVerse("maker_knights", SettlementNear(Player.Pos)?.Name ?? "camp", Player.Pos, $"Knighted {m.Name} of {c.Name} as {title}.", m.Name, m.Id);
        LogYours($"You knight {m.Name} of {c.Name} as {title}.");
        return null;
    }
}
