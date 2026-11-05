using Microsoft.Xna.Framework;

namespace Ironfield;

/// A veteran honour (CAMPAIGN_OVERHAUL.md 2.4), read from Data/campaign.json `Honours`. A company earns it when the history
/// counter named by Stat reaches At; the other fields are what it changes in battle and camp.
public sealed class HonourDef
{
    public string Id = "", Name = "", Text = "", Stat = "";
    public float At, RoutBonus, VsMounted = 1, DrillMul = 1, Morale, SiegeMorale;
}

/// The honours' combined effect on one company.
public readonly record struct HonourMods(float RoutBonus, float VsMounted, float DrillMul, float Morale, float SiegeMorale);

public static class Honours
{
    public const int Max = 3;

    /// The default honours, used when campaign.json lists none.
    static readonly HonourDef[] Defaults =
    {
        new() { Id = "unbroken", Name = "Unbroken", Text = "10 battles without a rout: holds 4 morale longer", Stat = "SinceRout", At = 10, RoutBonus = 4 },
        new() { Id = "horsebreakers", Name = "Horse-breakers", Text = "40 riders killed: +10% damage against mounted", Stat = "HorseKills", At = 40, VsMounted = 1.1f },
        new() { Id = "oldhands", Name = "Old Hands", Text = "25 battles: drill XP x1.5", Stat = "Battles", At = 25, DrillMul = 1.5f },
        new() { Id = "stormers", Name = "Stormers", Text = "3 assaults on walls: +10 morale in an assault", Stat = "Assaults", At = 3, SiegeMorale = 10 },
        new() { Id = "bloodied", Name = "Bloodied", Text = "won a battle that cost half its men: +5 starting morale", Stat = "Bloodied", At = 1, Morale = 5 },
    };

    public static IReadOnlyList<HonourDef> All => Balance.Get().Honours is { Count: > 0 } h ? h : Defaults;

    public static HonourDef? Get(string id) => All.FirstOrDefault(h => h.Id == id);

    public static HonourMods Of(CompanyRecord c)
    {
        float rout = 0, vs = 1, drill = 1, morale = 0, siege = 0;
        foreach (var id in c.Honours)
            if (Get(id) is HonourDef h) { rout += h.RoutBonus; vs *= h.VsMounted; drill *= h.DrillMul; morale += h.Morale; siege += h.SiegeMorale; }
        return new HonourMods(rout, vs, drill, morale, siege);
    }

    static float Stat(CompanyRecord c, string stat, bool bloodied) => stat switch
    {
        "SinceRout" => c.SinceRout, "HorseKills" => c.HorseKills, "Battles" => c.Battles, "Assaults" => c.Assaults, "Kills" => c.Kills,
        "Bloodied" => bloodied ? 1 : 0, _ => 0,
    };

    /// Grant any honour the company's history has earned (after a battle). Returns the newly earned ones.
    public static List<HonourDef> Earn(CompanyRecord c, bool bloodiedNow)
    {
        var got = new List<HonourDef>();
        foreach (var h in All)
        {
            if (c.Honours.Count >= Max) break;
            if (c.Honours.Contains(h.Id) || Stat(c, h.Stat, bloodiedNow) < h.At) continue;
            c.Honours.Add(h.Id);
            got.Add(h);
        }
        return got;
    }
}

/// Troop stacks and per-man promotion (step F1).
public sealed partial class Campaign
{
    /// The charges a company banner may carry (the field is its side's colour).
    static readonly Emblem[] BannerCharges = { Emblem.Chevron, Emblem.Saltire, Emblem.Lozenge, Emblem.Bend };

    public static string CultureName(int faction) => Factions.CultureOf(faction);

    /// A new company with one stack (of `men` men with `xp` each in its pool), an origin, a banner and perhaps a sergeant.
    public CompanyRecord NewCompany(string troop, string role, int men, float xp, Settlement? home = null)
    {
        var c = new CompanyRecord { Id = S.NextId++, Role = role, Name = Rules.CompanyName(rng, role), Banner = (int)rng.Pick(BannerCharges) };
        var s = c.Stack(troop);
        s.Men = men;
        s.Xp = Math.Clamp(xp * men, 0, s.PoolCap);
        if (home != null) { c.Home = home.Name; c.Culture = home.Culture; }
        return WithOfficer(c);
    }

    /// Add recruits from a settlement to a company: men of another culture unsettle it for a while (cohesion falls).
    public void Enlist(CompanyRecord c, string troop, int n, Settlement? from)
    {
        if (n <= 0) return;
        Rules.AddMen(c, troop, n);
        if (Has("VeteranCadre") && Player.Companies.Contains(c)) c.Stack(troop).AddXp(20 * n); // F4: recruits who know a little
        if (from != null)
        {
            string culture = from.Culture;
            if (c.Culture == "") { c.Culture = culture; c.Home = from.Name; }
            else if (c.Culture != culture) c.Cohesion = MathF.Max(0, c.Cohesion - 5);
        }
    }

    // ---------- promotion ----------

    /// Where a man promoted out of company `from` (a `fromTroop`) into troop `to` stands (2.2): the same company if he fits and
    /// there is room once he leaves his old place; else the first company of his new role with room; else a new company if a
    /// slot is free (`free` and `extra` let a dry run count places and companies as it goes). Null when there is nowhere for him.
    CompanyRecord? Destination(Party p, CompanyRecord from, string fromTroop, string to, Dictionary<CompanyRecord, float> free, ref int extra, out string reason)
    {
        reason = "";
        float Free(CompanyRecord k) => free.TryGetValue(k, out var f) ? f : free[k] = Rules.Cap(Rules.RoleOf(k)) - Rules.Places(k);
        float w = Rules.TierWeight(Troops.Get(to).Tier), wFrom = Rules.TierWeight(Troops.Get(fromTroop).Tier);
        if (Rules.Fits(from, to) && Free(from) + wFrom >= w - 1e-3f) return from;
        var other = p.Companies.FirstOrDefault(o => o != from && Rules.RoleOf(o) == Rules.TroopRole(to) && Rules.Fits(o, to) && Free(o) >= w - 1e-3f);
        if (other != null) return other;
        var raised = free.Keys.FirstOrDefault(k => k.Id == -1 && k.Role == Rules.TroopRole(to) && free[k] >= w - 1e-3f);
        if (raised != null) return raised;
        int slots = p == Player ? Rules.CompanySlots(Captain) : 5;
        if (p.Companies.Count + extra >= slots)
        {
            reason = Rules.Fits(from, to) ? "THE COMPANY IS FULL" : $"NO {RoleWord(Rules.TroopRole(to))} COMPANY FOR THESE MEN";
            return null;
        }
        extra++;
        var stand = new CompanyRecord { Id = -1, Role = Rules.TroopRole(to) }; // a company that would be raised for him
        free[stand] = Rules.Cap(stand.Role);
        return stand;
    }

    static string RoleWord(string role) => role switch { "archers" => "BOW", "spears" => "SPEAR", "horse" => "HORSE", _ => "FOOT" };

    /// The culture a company's men train as (4.2): their troop's own culture, else the company's origin. Men of no known
    /// culture (brigands, a company with no home) count as common and may enter any line.
    public static string ManCulture(CompanyRecord c, string troop)
    {
        string t = Troops.Get(troop).Culture;
        return t != Factions.Common ? t : c.Culture is "" ? Factions.Common : c.Culture;
    }

    static string CultureWord(string culture) => (Factions.Culture(culture)?.Name ?? culture).ToUpperInvariant();

    /// May men of this stack train into `to` (4.2)? Common troops are open to all; a culture's own troops only to its men; and its
    /// elite lines (tier V and up) need one of that culture's towns held by your realm or by you (its barracks; F6 adds the building).
    public bool CanEnterLine(Party p, CompanyRecord c, string from, string to, out string reason)
    {
        reason = "";
        var t = Troops.Get(to);
        if (t.Culture == Factions.Common) return true;
        string man = ManCulture(c, from);
        if (man != Factions.Common && man != t.Culture) { reason = $"{CultureWord(man)} MEN CANNOT TRAIN AS {CultureWord(t.Culture)} TROOPS"; return false; }
        if (t.Tier >= 5 && !HasBarracks(p, t.Culture)) { reason = $"NEEDS A {CultureWord(t.Culture)} TOWN IN YOUR REALM"; return false; }
        return true;
    }

    /// Does the party's realm (your contract, or a lord's faction), or the party itself, hold a town of this culture?
    public bool HasBarracks(Party p, string culture)
    {
        int realm = p == Player ? Captain.Contract : p.Faction;
        return S.Settlements.Any(s => s.Kind == SettlementKind.Town && s.Culture == culture && (realm >= 0 && s.Faction == realm || s.Owner == p.Id));
    }

    /// What one promotion costs this party: Cheap Steel takes 20% off yours (F4).
    int PromoteCostFor(Party p, string from, string to, bool horse) =>
        (int)MathF.Ceiling(Rules.PromoteCost(from, to, horse) * (p == Player && Has("CheapSteel") ? 0.8f : 1));

    /// Where promotions may happen: a town's training yard, or anywhere with the Drillmaster perk.
    public bool CanTrainHere(Party p) => p != Player || InTown(Player) || Has("Drillmaster");

    /// How many men of a stack could be promoted to `to` now (at most `want`), and what that costs; a reason when it is none.
    public int Promotable(Party p, CompanyRecord c, TroopStack s, string to, int want, float gold, out int cost, out string reason, bool ignorePlace = false)
    {
        cost = 0;
        reason = "";
        var from = s.Type;
        if (!from.UpgradesTo.Contains(to)) { reason = "NOT ON THIS LINE"; return 0; }
        if (!CanEnterLine(p, c, s.Troop, to, out reason)) return 0;
        if (s.Ready <= 0) { reason = $"NEEDS {from.UpgradeXp} XP"; return 0; }
        if (!ignorePlace && !CanTrainHere(p)) { reason = "NEEDS A TOWN"; return 0; }
        var free = new Dictionary<CompanyRecord, float>();
        int horses = p.Horses, extra = 0, n = 0;
        float w = Rules.TierWeight(Troops.Get(to).Tier), wFrom = Rules.TierWeight(from.Tier);
        for (; n < Math.Min(want, s.Ready); n++)
        {
            bool horse = Rules.NeedsHorse(s.Troop, to) && horses > 0;
            int each = PromoteCostFor(p, s.Troop, to, horse);
            if (cost + each > gold) { if (n == 0) reason = $"NEEDS {each} CROWNS"; break; }
            var into = Destination(p, c, s.Troop, to, free, ref extra, out string why);
            if (into == null) { if (n == 0) reason = why; break; }
            free[c] = free.GetValueOrDefault(c, Rules.Cap(Rules.RoleOf(c)) - Rules.Places(c)) + wFrom; // he leaves his place
            free[into] -= w;                                                                             // and takes one
            cost += each;
            if (horse) horses--;
        }
        return n;
    }

    /// Promote up to `want` men of a stack to `to` (2.2): each spends upgradeXp from the pool and his price in gold, and moves to
    /// the stack of his new troop (in this company, or one of his new role). `pay` takes the gold; returns how many went.
    int PromoteMen(Party p, CompanyRecord c, TroopStack s, string to, int want, Func<int, bool> pay)
    {
        int done = 0;
        var from = s.Type;
        if (!CanEnterLine(p, c, s.Troop, to, out _)) return 0;
        while (done < want && s.Ready > 0)
        {
            int extra = 0;
            var into = Destination(p, c, s.Troop, to, new Dictionary<CompanyRecord, float>(), ref extra, out _);
            if (into == null) break;
            bool horse = Rules.NeedsHorse(s.Troop, to) && p.Horses > 0;
            if (!pay(PromoteCostFor(p, s.Troop, to, horse))) break;
            if (into.Id == -1)
            {
                // Raise the company he founds, of his new role, from the same country.
                into = NewCompany(to, Rules.TroopRole(to), 0, 0);
                into.Culture = c.Culture; into.Home = c.Home;
                p.Companies.Insert(p.Companies.IndexOf(c) + 1, into);
                if (p == Player) Log($"{into.Name} are raised from the ranks of {c.Name}.");
            }
            if (horse) p.Horses--;
            s.Xp -= from.UpgradeXp;
            s.Men--;
            into.Stack(to).Men++;
            s.Xp = Math.Clamp(s.Xp, 0, s.PoolCap);
            done++;
        }
        c.Prune();
        return done;
    }

    /// Your promotion: `want` men of a stack (1, 5, or many for "all").
    public string? Promote(CompanyRecord c, TroopStack s, string to, int want)
    {
        int n = Promotable(Player, c, s, to, want, S.Gold, out _, out string reason);
        if (n <= 0) return reason;
        string fromName = s.Type.Name;
        int done = PromoteMen(Player, c, s, to, n, cost => { if (S.Gold < cost) return false; S.Gold -= cost; return true; });
        if (done > 0) Log($"{done} {fromName} of {c.Name} become {Troops.Get(to).Name}.");
        Practise("Training", done * 0.2f);
        return done > 0 ? null : "CANNOT PROMOTE";
    }

    /// The AI's promotions at home (2.2): every ready man, cheapest branch first within the culture's lines, paid from the
    /// lord's purse and then the realm's chest share `budget`.
    void PromoteAll(Party p, ref float budget)
    {
        var fs = S.Factions[Math.Max(0, p.Faction)];
        float chest = budget;
        foreach (var c in p.Companies.ToList())
            foreach (var s in c.Stacks.Where(s => s.Ready > 0).ToList())
            {
                // The realm's own line first, then any branch of the company's role, then the rest; cheapest within each.
                var options = s.Type.UpgradesTo.Where(t => CanEnterLine(p, c, s.Troop, t, out _))
                    .OrderBy(t => BranchRank(p.Faction, Rules.RoleOf(c), t)).ThenBy(t => Rules.PromoteCost(s.Troop, t, true)).ToList();
                foreach (var to in options)
                {
                    int got = PromoteMen(p, c, s, to, s.Ready, cost =>
                    {
                        int fromChest = Math.Clamp(cost - (int)p.Gold, 0, (int)chest); // his purse first
                        if (p.Gold + fromChest < cost) return false;
                        chest -= fromChest; fs.Treasury -= fromChest;
                        p.Gold -= cost - fromChest;
                        return true;
                    });
                    if (s.Ready <= 0) break;
                }
            }
        budget = chest;
    }

    /// How much a faction's lords like a branch for a company of this role: 0 its own line troop, 1 another troop of the role, 2 the rest.
    static int BranchRank(int faction, string role, string to)
    {
        var t = Troops.Get(to);
        return Armies.LineTroop(role, t.Tier, faction) == to ? 0 : Rules.TroopRole(to) == role ? 1 : 2;
    }

    /// The stack with the most men ready for promotion in your party (for NextStep and the party screen).
    public (CompanyRecord Company, TroopStack Stack)? MostReady() =>
        Player.Companies.SelectMany(c => c.Stacks.Select(s => (c, s))).Where(x => x.s.Ready > 0).OrderByDescending(x => x.s.Ready).Select(x => ((CompanyRecord, TroopStack)?)x).FirstOrDefault();

    // ---------- moving men between companies ----------

    /// Move a whole stack (fit and wounded, with its pool) to another company of the same role.
    public string? MoveStack(CompanyRecord from, TroopStack s, CompanyRecord to)
    {
        if (from == to) return "SAME COMPANY";
        if (!Rules.Fits(to, s.Troop)) return "WRONG ROLE";
        float need = (s.Men + s.Wounded) * Rules.TierWeight(s.Type.Tier);
        if (Rules.Places(to) + need > Rules.Cap(Rules.RoleOf(to)) + 1e-3f) return "NO ROOM";
        var t = to.Stack(s.Troop);
        t.Men += s.Men; t.Wounded += s.Wounded; t.Xp = Math.Clamp(t.Xp + s.Xp, 0, t.PoolCap);
        from.Stacks.Remove(s);
        if (from.Men + from.Wounded == 0) Player.Companies.Remove(from);
        return null;
    }

    /// Split n fit men off a stack into a new company of the stack's role, with their share of the pool.
    public string? SplitStack(CompanyRecord c, TroopStack s, int n)
    {
        if (Player.Companies.Count >= Slots) return "NO FREE COMPANY";
        n = Math.Min(n, s.Men);
        if (n <= 0) return "TOO FEW MEN";
        if (n == s.Men && s.Wounded == 0 && c.Stacks.Count == 1) return "THAT IS THE WHOLE COMPANY";
        var nc = NewCompany(s.Troop, Rules.Fits(c, s.Troop) ? Rules.RoleOf(c) : Rules.TroopRole(s.Troop), 0, 0);
        nc.Culture = c.Culture; nc.Home = c.Home; nc.Cohesion = c.Cohesion;
        nc.Name = c.Name + " II";
        float xp = s.Men > 0 ? s.Xp * n / s.Men : 0;
        s.Take(n);
        var t = nc.Stack(s.Troop);
        t.Men = n;
        t.Xp = Math.Clamp(xp, 0, t.PoolCap);
        c.Prune();
        Player.Companies.Insert(Player.Companies.IndexOf(c) + 1, nc);
        return null;
    }

    // ---------- after a battle ----------

    /// The company's history after a battle: ledger, honour counters, new honours.
    void Chronicle(CompanyRecord c, CompanyResult cr, bool won, int lost, string foe, bool siege, Party owner)
    {
        if (!cr.Deployed) return;
        c.Ledger.Add(new LedgerEntry { Day = Day, Foe = foe, Lost = lost, Kills = cr.Kills, Won = won });
        if (c.Ledger.Count > CompanyRecord.LedgerSize) c.Ledger.RemoveAt(0);
        c.HorseKills += cr.HorseKills;
        if (siege && owner == Player) c.Assaults++;
        c.SinceRout = cr.Routed ? 0 : c.SinceRout + 1;
        bool bloodied = won && cr.Start > 0 && lost * 2 >= cr.Start;
        foreach (var h in Honours.Earn(c, bloodied))
            if (owner == Player) Log($"{c.Name} earn an honour: {h.Name}.");
    }
}
