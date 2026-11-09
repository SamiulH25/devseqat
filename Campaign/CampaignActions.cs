using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: your actions in towns and on the march (recruit, hire, horses, gear, split, contracts, rest). Each returns null on success or the reason shown in the UI.
public partial class Campaign
{
    // ---------- your actions ----------

    public void MoveTo(Vector2 p) { S.GoalSite = -1; Player.Goal = p; Player.Moving = true; Player.ChaseId = -1; Player.GoalSettlement = -1; Player.Resting = false; }
    public void Chase(Party q) { S.GoalSite = -1; Player.ChaseId = q.Id; Player.Moving = true; Player.GoalSettlement = -1; Player.Resting = false; }
    public void GoTo(Settlement s) { S.GoalSite = -1; Player.Goal = s.Pos; Player.Moving = true; Player.ChaseId = -1; Player.GoalSettlement = s.Id; Player.Resting = false; }

    int Slots => Rules.CompanySlots(Captain);
    int RoomForMen => Rules.MenLimit(Captain) - Player.Men - Player.Wounded;

    public string? RecruitLevies(Settlement s, int n)
    {
        // Only some of them will follow you: how many depends on what their lord thinks of you (4.2).
        if (s.Kind == SettlementKind.Village && Captain.Corruption >= HuntedAt) return "THE VILLAGE BARS ITS GATES TO YOU"; // the Black Flame (K7)
        if (OmenShunned(s)) return "THE VILLAGE FEARS WHAT YOU WORK: IT BARS ITS GATES"; // the Omen's teeth (MA8)
        n = Math.Min(n, RecruitsForYou(s));
        if (n <= 0) return s.Recruits > 0 ? "THEY WILL NOT FOLLOW YOU: THEIR LORD THINKS ILL OF YOU" : "NO RECRUITS";
        string levy = RecruitTroopFor(s);
        float each = Rules.PerMan(Troops.Get(levy).Cost) * RecruitPriceFactor(s);
        int cost = (int)MathF.Ceiling(n * each);
        if (S.Gold < cost) return $"NEEDS {cost} CROWNS";
        if (n > RoomForMen) return "PARTY IS FULL (RENOWN RAISES THE LIMIT)";
        // Men join a company of their own people first (no cohesion lost, and they keep their culture's lines), then a new
        // company of their own, and only when no slot is free a company of another culture (whose ways they take up).
        int left = n;
        void Fill(IEnumerable<CompanyRecord> into)
        {
            foreach (var co in into.Where(co => Rules.Fits(co, levy)).OrderByDescending(co => co.Stacks.Any(x => x.Troop == levy)).ToList())
            {
                int k = Math.Min(left, Rules.Room(co, levy));
                if (k <= 0) continue;
                Enlist(co, levy, k, s);
                left -= k;
                if (left == 0) break;
            }
        }
        Fill(Player.Companies.Where(co => co.Culture == s.Culture || co.Culture == ""));
        while (left > 0 && Player.Companies.Count < Slots)
        {
            var co = NewCompany(levy, "infantry", 0, 0, s);
            Player.Companies.Add(co);
            int k = Math.Min(left, Rules.Room(co, levy));
            Enlist(co, levy, k, s);
            left -= k;
        }
        if (left > 0) Fill(Player.Companies);
        int took = n - left;
        if (took == 0) return $"MAX {Slots} COMPANIES, ALL FULL";
        s.Recruits -= took;
        S.Gold -= MathF.Ceiling(took * each);
        return null;
    }

    /// The levy price for a band of ArmyScale men here (shown in the recruit tab).
    public int LevyPrice(Settlement s) => (int)MathF.Ceiling(Troops.Get(RecruitTroopFor(s)).Cost * RecruitPriceFactor(s));

    public int MercenaryPrice(CompanyRecord m) => (int)(m.Stacks.Sum(x => x.Men * Rules.PerMan(x.Type.Cost)) * B.MercMarkup * (Has("Haggler") ? 0.85f : 1));

    /// Mages are super units: a captain keeps one circle of them at most.
    public bool HasCircle => Player.Companies.Any(c => Rules.RoleOf(c) == "casters");
    public const string OneCircle = "YOU CAN KEEP ONLY ONE CIRCLE OF MAGES";

    public string? Hire(Settlement s, CompanyRecord m)
    {
        int price = MercenaryPrice(m);
        if (S.Gold < price) return $"NEEDS {price} CROWNS";
        if (Player.Companies.Count >= Slots) return "NO FREE COMPANY";
        if (m.Men > RoomForMen) return "PARTY IS FULL";
        if (Rules.RoleOf(m) == "casters" && HasCircle) return OneCircle;
        S.Gold -= price;
        s.Mercenaries.Remove(m);
        Player.Companies.Add(m);
        Log($"{m.Name} ({m.Men} {m.Type.Name}) take your coin.");
        return null;
    }

    public int HorsePrice => (int)(Rules.PerMan(Troops.Mount("courser").Hp * B.HorseCostRate) * (Has("Horsemaster") ? 0.7f : 1) * (Has("Haggler") ? 0.85f : 1)
        * (SettlementAt(Player.Pos) is Settlement st && Level(st, "stables") > 0 ? 0.7f : 1));

    public string? BuyHorses(Settlement s, int n)
    {
        n = Math.Min(n, s.Horses);
        if (n <= 0) return "NO HORSES FOR SALE";
        if (S.Gold < n * HorsePrice) return $"NEEDS {n * HorsePrice} CROWNS";
        S.Gold -= n * HorsePrice;
        s.Horses -= n;
        Player.Horses += n;
        return null;
    }

    public int HoundPrice => (int)MathF.Ceiling(Rules.PerMan(Troops.Get("war_hounds").Cost));

    /// War hounds bred at the kennel of a fief of yours join your company: into a pack you have, or as a new company.
    public string? RaiseHounds(Settlement s, int n)
    {
        if (s.Owner != Player.Id || Level(s, "kennel") == 0) return "NO KENNEL OF YOURS HERE";
        n = Math.Min(n, s.Hounds);
        if (n <= 0) return "NO HOUNDS READY";
        int room = Rules.MenLimit(Captain) - Player.Men - Player.Wounded;
        if (room <= 0) return "PARTY IS FULL";
        var pack = Player.Companies.FirstOrDefault(c => c.Troop == "war_hounds" && Rules.Room(c, "war_hounds") > 0);
        if (pack == null && Player.Companies.Count >= Rules.CompanySlots(Captain)) return "NO FREE COMPANY";
        string role = Rules.TroopRole("war_hounds");
        n = Math.Min(n, Math.Min(room, pack != null ? Rules.Room(pack, "war_hounds") : Rules.CapOf("war_hounds", role)));
        if (S.Gold < n * HoundPrice) return $"NEEDS {n * HoundPrice} CROWNS";
        S.Gold -= n * HoundPrice;
        s.Hounds -= n;
        if (pack != null) Rules.AddMen(pack, "war_hounds", n);
        else
        {
            pack = NewCompany("war_hounds", role, n, 0, s);
            pack.Name = "War Hounds";
            Player.Companies.Add(pack);
        }
        return null;
    }

    public void Merge(CompanyRecord a, CompanyRecord b) { Rules.Merge(a, b); Player.Companies.Remove(b); foreach (var m in FallenOf(b).ToList()) m.Company = a.Id; }

    /// Split a company in two: half of every stack goes to the new company (with its share of each pool).
    public string? Split(CompanyRecord c)
    {
        if (Player.Companies.Count >= Slots) return "NO FREE COMPANY";
        if (c.Men < 2) return "TOO FEW MEN";
        if (Rules.RoleOf(c) == "casters") return OneCircle;
        var n = NewCompany(c.Troop, Rules.RoleOf(c), 0, 0);
        n.Stacks.Clear();
        n.Cohesion = c.Cohesion; n.Culture = c.Culture; n.Home = c.Home;
        n.Name = c.Name + " II";
        foreach (var st in c.Stacks.ToList())
        {
            int half = st.Men / 2;
            if (half <= 0) continue;
            float xp = st.Xp * half / st.Men;
            st.Take(half);
            var t = n.Stack(st.Troop);
            t.Men = half;
            t.Xp = Math.Clamp(xp, 0, t.PoolCap);
        }
        if (n.Men == 0) return "TOO FEW MEN";
        c.Prune();
        Player.Companies.Insert(Player.Companies.IndexOf(c) + 1, n);
        return null;
    }

    public void Disband(CompanyRecord c) { Player.Companies.Remove(c); Log($"{c.Name} are paid off and go home."); }

    public void MoveUp(CompanyRecord c)
    {
        int i = Player.Companies.IndexOf(c);
        if (i > 0) (Player.Companies[i - 1], Player.Companies[i]) = (Player.Companies[i], Player.Companies[i - 1]);
    }

    /// Sign with a realm (or -1: end your contract). Leaving gives your fiefs back, breaks any oath of fealty and starts
    /// the service clock again (F5). A king serves no one.
    public string? SignContract(int faction)
    {
        if (Rank == Rank.King) return "A KING SERVES NO ONE";
        if (faction == Captain.Contract) return null;
        foreach (var f in S.Settlements.Where(t => t.Owner == Player.Id))
        {
            f.Owner = -1; // fiefs go back to the liege you leave
            Log($"You give {f.Name} back to the {FactionNames[f.Faction]}.");
        }
        if (Captain.Contract >= 0) LeaveRealm(Captain.Contract);
        Captain.Contract = faction;
        Captain.ContractSince = (float)S.Hours;
        Me.Faction = faction;
        Log(faction < 0 ? "You are a free captain again." : $"You sign on with the {FactionNames[faction]}.");
        return null;
    }

    /// Rest where you are for some hours (healing is faster in a town).
    public void Rest(float hours)
    {
        Player.Resting = true;
        Player.Moving = false;
        Tick(hours);
        Player.Resting = false;
    }
}
