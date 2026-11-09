using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the rule "a higher tier is always better" (docs/COMPANIONS_MAGIC.md D0).
public static partial class SelfTest
{
    /// Men of a squad still in the fight: alive, on the field and not running.
    static int Standing(Squad s) => s.Units.Count(u => u.Active && !u.Routing);

    /// `na` men of troop `a` against `nb` of troop `b` on flat ground until one side is beaten (or `secs` pass):
    /// the share of each side still standing. Foot charge each other; bows face off at range and shoot.
    static (float a, float b) TierFight(string a, string b, int seed, int na = 30, int nb = 30, float secs = 120)
    {
        var ta = Troops.Get(a);
        var tb = Troops.Get(b);
        bool shoot = ta.Ammo > 0 && tb.Ammo > 0;
        float gap = shoot ? 0.8f * MathF.Min(ta.Range, tb.Range) : 30;
        var battle = Battle.Empty(seed);
        var sa = battle.AddSquad(0, ta, na, new(0, -gap / 2), 10, Vector2.UnitY);
        var sb = battle.AddSquad(1, tb, nb, new(0, gap / 2), 10, -Vector2.UnitY);
        sa.Order = sb.Order = shoot ? Order.Hold : Order.Charge;
        sa.Target = sb;
        sb.Target = sa;
        for (int i = 0; i < secs * 60 && Standing(sa) > 0 && Standing(sb) > 0; i++) battle.Update(1 / 60f);
        return (Standing(sa) / (float)na, Standing(sb) / (float)nb);
    }

    /// An upgrade fights its parent only when it keeps its role (both melee or both shooting, same class); a role change
    /// (lancers to horse archers) is held to the power sum in TroopTree instead.
    static bool SameRole(UnitType t, UnitType next) => t.Class == next.Class && (t.Ammo > 0) == (next.Ammo > 0);

    /// One upgrade link per class and tier step (the first by id, so the pick is stable): (parent, upgrade).
    static IEnumerable<(UnitType from, UnitType to)> TierSample() =>
        Troops.All.Where(t => !t.Wild && !t.Leader)
            .SelectMany(t => t.UpgradesTo.Select(Troops.Get).Where(n => SameRole(t, n)).Select(n => (from: t, to: n)))
            .GroupBy(l => (l.to.Class, l.to.Ammo > 0, l.to.Tier))
            .Select(g => g.OrderBy(l => l.from.Id).ThenBy(l => l.to.Id).First())
            .OrderBy(l => l.to.Tier).ThenBy(l => l.to.Class);

    /// D0: an upgrade that keeps its weapon may not lose a stat, and must gain in two; the check bites on a worse copy.
    static void UpgradeNeverWorse()
    {
        Check(Troops.All.All(t => t.UpgradesTo.Select(Troops.Get).All(n => Troops.WorseUpgrade(t, n) == null)), "no upgrade in troops.json is worse than its parent");
        var guard = Troops.Get("allfather_guard");
        var weaker = new UnitType { Id = "x", Class = guard.Class, Tier = 8, Look = guard.Look, MaxHp = guard.MaxHp + 10, Damage = guard.Damage + 5, Armor = guard.Armor - 1, BlockChance = guard.BlockChance };
        Check(Troops.WorseUpgrade(guard, weaker) is string why && why.Contains("armour"), "an upgrade that loses armour is refused");
        var same = new UnitType { Id = "y", Class = guard.Class, Tier = 8, Look = guard.Look, MaxHp = guard.MaxHp + 10, Damage = guard.Damage, Armor = guard.Armor, BlockChance = guard.BlockChance };
        Check(Troops.WorseUpgrade(guard, same) != null, "an upgrade that gains in only one stat is refused");
        foreach (var t in Troops.All.Where(t => !t.Wild && !t.Leader))
            foreach (var n in t.UpgradesTo.Select(Troops.Get))
                Check(Rules.Might(n, 10) > Rules.Might(t, 10), $"auto-resolve rates {n.Name} above {t.Name}");
    }

    /// D0: an upgrade beats the troop it came from, 30 against 30, on two seeds (one link per class and tier step).
    static void HigherTierWins()
    {
        int links = 0;
        foreach (var (from, to) in TierSample())
        {
            links++;
            for (int seed = 1; seed <= 2; seed++)
            {
                var (up, down) = TierFight(to.Id, from.Id, 910 + seed);
                Check(up > down, $"{to.Name} (tier {to.Tier}) beat {from.Name} (tier {from.Tier}): {up:P0} against {down:P0} standing");
            }
        }
        Console.WriteLine($"tiers: {links} upgrade links fought against their parents, every upgrade won");
    }

    /// D0: the gap is felt: a tier VII company beats twice as many tier IV men of its culture and class (horse: one and a
    /// half times as many, since in the open the many ride round the few; T1's charge styles should let this tighten).
    static void TierGap()
    {
        foreach (var (culture, cls) in new[] { ("northern", UnitClass.Infantry), ("azure", UnitClass.Cavalry), ("crimson", UnitClass.Archer), ("azure", UnitClass.Skirmisher) })
        {
            var top = Troops.All.Where(t => t.Culture == culture && t.Class == cls && t.Tier == 7).OrderBy(t => t.Id).First();
            var low = Troops.All.Where(t => t.Culture == culture && t.Class == cls && t.Tier == 4 && (t.Ammo > 0) == (top.Ammo > 0)).OrderBy(t => t.Id).First();
            int many = cls == UnitClass.Cavalry ? 30 : 40;
            var (a, b) = TierFight(top.Id, low.Id, 920, 20, many);
            Console.WriteLine($"tiers: 20 {top.Name} against {many} {low.Name}: {a:P0} against {b:P0} standing");
            Check(a * 20 > b * many, $"20 {top.Name} (VII) beat {many} {low.Name} (IV)");
        }
    }

    /// A survey, not a test: every upgrade link fought against its parent, printed (`--selftest TierSurvey`).
    static void TierSurvey()
    {
        int lost = 0, n = 0;
        foreach (var t in Troops.All.Where(t => !t.Wild && !t.Leader).OrderBy(t => t.Tier))
            foreach (var next in t.UpgradesTo.Select(Troops.Get).Where(n => SameRole(t, n)))
            {
                n++;
                float wins = 0, left = 0;
                for (int seed = 1; seed <= 3; seed++)
                {
                    var (up, down) = TierFight(next.Id, t.Id, 900 + seed);
                    if (up > down) wins++;
                    left += up - down;
                }
                bool ok = wins >= 2 && left > 0;
                if (!ok) lost++;
                Console.WriteLine($"{(ok ? "  " : "!!")} {t.Id}({t.Tier},{t.Class},{t.Look.Weapon}) -> {next.Id}({next.Tier},{next.Class},{next.Look.Weapon}): won {wins}/3, margin {left / 3:+0.00;-0.00}");
            }
        Console.WriteLine($"tier survey: {lost} of {n} upgrades lose to their parent");
    }
}
