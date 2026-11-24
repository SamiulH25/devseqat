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
}
