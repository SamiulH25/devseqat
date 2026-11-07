using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: your actions in towns and on the march (recruit, hire, horses, gear, split, contracts, rest). Each returns null on success or the reason shown in the UI.
public partial class Campaign
{
    // ---------- your actions ----------

    public void MoveTo(Vector2 p) { Player.Goal = p; Player.Moving = true; Player.ChaseId = -1; Player.GoalSettlement = -1; Player.Resting = false; }
    public void Chase(Party q) { Player.ChaseId = q.Id; Player.Moving = true; Player.GoalSettlement = -1; Player.Resting = false; }
    public void GoTo(Settlement s) { Player.Goal = s.Pos; Player.Moving = true; Player.ChaseId = -1; Player.GoalSettlement = s.Id; Player.Resting = false; }

    int Slots => Rules.CompanySlots(Captain);
    int RoomForMen => Rules.MenLimit(Captain) - Player.Men - Player.Wounded;

    public string? RecruitLevies(Settlement s, int n)
    {
        // Only some of them will follow you: how many depends on what their lord thinks of you (4.2).
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

    public string? Hire(Settlement s, CompanyRecord m)
    {
        int price = MercenaryPrice(m);
        if (S.Gold < price) return $"NEEDS {price} CROWNS";
        if (Player.Companies.Count >= Slots) return "NO FREE COMPANY";
        if (m.Men > RoomForMen) return "PARTY IS FULL";
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

    /// The captain's gear: heavier pieces need the Arms skill to wear (F4).
    public (string Name, int Cost, bool Owned, int Arms)[] Gear() => new[]
    {
        ("PLATE ARMOUR", (int)B.ArmorCost, Captain.ArmorTier > 0, 25),
        ("GREAT HELM", (int)B.HelmCost, Captain.HelmTier > 0, 15),
        ("WARHORSE", (int)B.WarhorseCost, Captain.HorseTier >= 1, 0),
        ("DESTRIER", (int)B.DestrierCost, Captain.HorseTier >= 2, 35),
    };

    public string? BuyGear(int i)
    {
        var (name, cost, owned, arms) = Gear()[i];
        if (owned) return "OWNED";
        if (i == 3 && Captain.HorseTier < 1) return "BUY A WARHORSE FIRST";
        if (Skill("Arms") < arms) return $"NEEDS ARMS {arms}";
        if (S.Gold < cost) return $"NEEDS {cost} CROWNS";
        S.Gold -= cost;
        switch (i) { case 0: Captain.ArmorTier = 1; break; case 1: Captain.HelmTier = 1; break; case 2: Captain.HorseTier = 1; break; default: Captain.HorseTier = 2; break; }
        Log($"You buy a {name.ToLowerInvariant()}.");
        return null;
    }

    public void Merge(CompanyRecord a, CompanyRecord b) { Rules.Merge(a, b); Player.Companies.Remove(b); }

    /// Split a company in two: half of every stack goes to the new company (with its share of each pool).
    public string? Split(CompanyRecord c)
    {
        if (Player.Companies.Count >= Slots) return "NO FREE COMPANY";
        if (c.Men < 2) return "TOO FEW MEN";
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
