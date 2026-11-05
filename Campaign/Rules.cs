using Microsoft.Xna.Framework;

namespace Ironfield;

/// One company's line in a battle report (also the results screen).
public sealed record CompanyReport(int Id, string Name, string Troop, int Team, int MenBefore, int MenAfter, int Killed, int Wounded, int Captured,
    int FledLost, float XpGain, bool UpgradeReady, bool Deployed);

/// Everything a finished battle changed, for the results screen.
public sealed record BattleReport(bool Won, string Enemy, IReadOnlyList<CompanyReport> Companies, float Loot, float Ransom, float Bounty,
    float Renown, int Horses, LeaderFate Captain);

/// A battle about to be fought between map parties: the armies, options, and how its companies map back to campaign records.
public sealed class BattleSetup
{
    public int Seed;
    public ArmySpec Player = null!, Enemy = null!;
    public BattleOptions Options = new();
    public readonly List<Party> Side0 = new(), Side1 = new();
    public string EnemyName = "";
}

/// Company rules (caps, strength, XP, upgrades), auto-resolve, and the battle bridge (docs/CAMPAIGN.md 4-6).
public static class Rules
{
    static Balance B => Balance.Get();

    public static string RoleOf(CompanyRecord c) => c.Role is { Length: > 0 } r ? r : Armies.RoleOf(new Company(c.Troop, c.Men));

    /// Most men a company of this troop may hold: the line's base size times the tier factor (elite companies are smaller).
    public static int Cap(string troop, string role)
    {
        int baseSize = role switch { "archers" => 16, "spears" => 12, "horse" => 10, _ => 24 };
        float k = Troops.Get(troop).Tier switch { 1 => 1.25f, 2 => 1f, 3 => 0.85f, _ => 0.7f };
        return (int)MathF.Round(baseSize * k);
    }

    public static float Strength(UnitType t, int men)
    {
        float cls = t.Class switch { UnitClass.Cavalry => 1.6f, UnitClass.Archer => 0.9f, _ => 1f };
        return men * (t.MaxHp + 5 * t.Armor + 2 * t.Damage) * cls / 100f;
    }

    public static float Strength(CompanyRecord c) => Strength(c.Type, c.Men);
    public static float Strength(Party p) => p.Companies.Sum(Strength);

    public static int Upkeep(CompanyRecord c) => (int)MathF.Ceiling((c.Men + c.Wounded) * c.Type.Cost * B.UpkeepRate);

    public static bool UpgradeReady(CompanyRecord c) => c.Type.UpgradesTo.Length > 0 && c.Xp >= c.Type.UpgradeXp && c.Men > 0;

    /// Can company c become troop `to` now? Gold covers the price difference per man (and horses if it becomes cavalry);
    /// it needs a town's training yard unless the captain has Drillmaster. `reason` explains a refusal.
    public static bool CanUpgrade(CompanyRecord c, string to, float gold, int horses, bool inTown, bool drillmaster, out int cost, out string reason)
    {
        var from = c.Type;
        var next = Troops.Get(to);
        cost = UpgradeCost(c, to, horses);
        reason = "";
        if (!from.UpgradesTo.Contains(to)) reason = "NOT ON THIS LINE";
        else if (c.Xp < from.UpgradeXp) reason = $"NEEDS {from.UpgradeXp} XP";
        else if (!inTown && !drillmaster) reason = "NEEDS A TOWN";
        else if (gold < cost) reason = $"NEEDS {cost} CROWNS";
        _ = next;
        return reason == "";
    }

    public static int UpgradeCost(CompanyRecord c, string to, int horses)
    {
        var from = c.Type;
        var next = Troops.Get(to);
        int cost = Math.Max(0, next.Cost - from.Cost) * (c.Men + c.Wounded);
        if (next.Mount != null && from.Mount == null) cost += (int)(Math.Max(0, c.Men + c.Wounded - horses) * next.Mount.Hp * B.HorseCostRate);
        return cost;
    }

    /// The whole company changes troop; XP starts again. Returns horses used.
    public static int Upgrade(CompanyRecord c, string to, int horses)
    {
        var from = c.Type;
        var next = Troops.Get(to);
        int used = next.Mount != null && from.Mount == null ? Math.Min(horses, c.Men + c.Wounded) : 0;
        c.Troop = to;
        c.Xp = 0;
        if (c.Role == "" || from.Tier == 1) c.Role = Armies.RoleOf(new Company(to, c.Men));
        return used;
    }

    /// Add men of the company's own troop: they arrive green and dilute the average XP.
    public static void AddMen(CompanyRecord c, int n)
    {
        if (n <= 0) return;
        c.Xp = c.Men + n > 0 ? c.Xp * c.Men / (c.Men + n) : 0;
        c.Men += n;
    }

    public static bool CanMerge(CompanyRecord a, CompanyRecord b) =>
        a != b && a.Troop == b.Troop && a.Men + b.Men <= Cap(a.Troop, RoleOf(a));

    public static void Merge(CompanyRecord into, CompanyRecord from)
    {
        int men = into.Men + from.Men;
        into.Xp = men > 0 ? (into.Xp * into.Men + from.Xp * from.Men) / men : 0;
        into.Cohesion = men > 0 ? (into.Cohesion * into.Men + from.Cohesion * from.Men) / men : into.Cohesion;
        into.Men = men;
        into.Wounded += from.Wounded;
        into.Battles = Math.Max(into.Battles, from.Battles);
        into.Kills += from.Kills;
    }

    public static string CompanyName(Rng rng, string role)
    {
        var n = Names.Get();
        var nouns = n.Nouns.TryGetValue(role, out var l) ? l : n.Nouns["infantry"];
        return rng.Pick(n.Places) + " " + rng.Pick(nouns);
    }

    // ---------- the captain ----------

    /// Your troop type in battle: the captain's data with your bought armour.
    public static UnitType CaptainType(CaptainState c)
    {
        var t = Troops.Get("captain").Clone();
        var l = t.Look;
        t.Look = l with
        {
            Body = c.ArmorTier > 0 ? "plate" : l.Body, Legs = c.ArmorTier > 0 ? "plate" : l.Legs, Feet = c.ArmorTier > 0 ? "sabatons" : l.Feet,
            Head = c.HelmTier > 0 ? "greathelm" : l.Head,
        };
        t.Armor += 5 * c.ArmorTier + 2 * c.HelmTier;
        return t;
    }

    public static Mount CaptainMount(CaptainState c) => Troops.Mount(c.HorseTier switch { 2 => "destrier", 1 => "warhorse", _ => "courser" });

    public static int CompanySlots(CaptainState c) => Math.Min(9, (int)(B.CompanySlotsBase + c.Renown / B.CompanySlotRenown));
    public static int MenLimit(CaptainState c) => (int)(B.MenBase + c.Renown / B.MenRenownDivisor);
    public static int PerkPoints(CaptainState c) => (int)(c.Renown / B.PerkEvery) - c.Perks.Count;

    public static readonly Dictionary<Perk, string> PerkText = new()
    {
        [Perk.Bannerman] = "AURA RADIUS +4 M", [Perk.WarCry] = "RALLY +10 MORALE, 3 S FASTER", [Perk.Tactician] = "AUTO-RESOLVE +10%, SEE FOES WHEN DEPLOYING",
        [Perk.Quartermaster] = "UPKEEP -10%", [Perk.Surgeon] = "+10% OF FALLEN SURVIVE WOUNDED", [Perk.Drillmaster] = "UPGRADE COMPANIES IN CAMP",
        [Perk.Horsemaster] = "HORSES -30%, PARTY SPEED +5%", [Perk.IronWill] = "COMPANIES NEAR YOU HOLD UNTIL 30% STRENGTH",
    };

    // ---------- auto-resolve (5.4) ----------

    /// Fight it out on paper: the result has the same shape as a played battle.
    public static BattleResult AutoResolve(IReadOnlyList<Party> side0, IReadOnlyList<Party> side1, Rng rng, bool tactician0)
    {
        float s0 = side0.Sum(Strength) * (tactician0 ? 1.1f : 1) * rng.Range(0.85f, 1.15f);
        float s1 = side1.Sum(Strength) * rng.Range(0.85f, 1.15f);
        int t0 = side0.Max(p => p.Tier), t1 = side1.Max(p => p.Tier);
        s0 *= 1 + 0.1f * (t0 - t1);
        int winner = s0 >= s1 ? 0 : 1;
        float ratio = winner == 0 ? s1 / MathF.Max(1, s0) : s0 / MathF.Max(1, s1); // loser / winner
        float loserLoss = rng.Range(0.45f, 0.75f), winnerLoss = Math.Clamp(ratio * 0.45f, 0.03f, 0.6f);
        var list = new List<CompanyResult>();
        var kills = new float[2];
        for (int team = 0; team < 2; team++)
        {
            var side = team == 0 ? side0 : side1;
            float loss = team == winner ? winnerLoss : loserLoss;
            var cos = side.SelectMany(p => p.Companies).Where(c => c.Men > 0).ToList();
            float weight = cos.Sum(c => c.Men / (float)c.Type.Tier);
            int totalLoss = (int)MathF.Round(cos.Sum(c => c.Men) * loss);
            foreach (var c in cos)
            {
                int fallen = Math.Min(c.Men, (int)MathF.Round(totalLoss * (c.Men / (float)c.Type.Tier) / MathF.Max(1, weight)));
                int fled = team == winner ? 0 : (int)((c.Men - fallen) * 0.15f);
                kills[1 - team] += fallen;
                list.Add(new CompanyResult(team, c.Id, c.Men, c.Men - fallen - fled, fled, fallen, 0, new int[5], true));
            }
        }
        // Kills go to each side's companies by strength, as victims of the enemy's average tier.
        for (int team = 0; team < 2; team++)
        {
            var side = team == 0 ? side0 : side1;
            var foes = (team == 0 ? side1 : side0).SelectMany(p => p.Companies).Where(c => c.Men > 0).ToList();
            int foeTier = foes.Count == 0 ? 1 : (int)MathF.Round((float)foes.Average(c => c.Type.Tier));
            float total = side.Sum(Strength);
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r.Team != team) continue;
                var c = side.SelectMany(p => p.Companies).First(c => c.Id == r.Id);
                int k = (int)MathF.Round(kills[team] * Strength(c) / MathF.Max(1, total));
                var byTier = new int[5];
                byTier[Math.Clamp(foeTier, 1, 4)] = k;
                list[i] = r with { Kills = k, KillsByTier = byTier };
            }
        }
        return new BattleResult(winner, 0, list, LeaderFate.Unhurt, LeaderFate.Unhurt, 0);
    }

    // ---------- the bridge (5.2, 5.5) ----------

    public static ArmySpec Army(string name, IEnumerable<Party> parties, float moraleBonus = 0) => new(name, 0,
        parties.SelectMany(p => p.Companies).Where(c => c.Men > 0)
            .Select(c => new Company(c.Troop, c.Men, RoleOf(c), c.Id, c.Name, Math.Clamp(70 + c.Cohesion + moraleBonus, 20, 100))).ToList());
}
