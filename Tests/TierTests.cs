using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the rule "a higher tier is always better" (docs/archive/COMPANIONS_MAGIC.md D0).
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
        Check(Troops.All.All(t => t.Tier <= Troops.MaxTier && t.Pierce is >= 0 and <= 1), $"all troops are tier <= {Troops.MaxTier} with pierce in 0..1");
        Check(Troops.All.All(t => t.UpgradesTo.Select(Troops.Get).All(n => Troops.WorseUpgrade(t, n) == null)), "no upgrade in troops.json is worse than its parent");
        var guard = Troops.Get("iron_thanes");
        var weaker = new UnitType { Id = "x", Class = guard.Class, Tier = 7, Look = guard.Look, MaxHp = guard.MaxHp + 10, Damage = guard.Damage + 5, Armor = guard.Armor - 1, BlockChance = guard.BlockChance };
        Check(Troops.WorseUpgrade(guard, weaker) is string why && why.Contains("armour"), "an upgrade that loses armour is refused");
        var same = new UnitType { Id = "y", Class = guard.Class, Tier = 7, Look = guard.Look, MaxHp = guard.MaxHp + 10, Damage = guard.Damage, Armor = guard.Armor, BlockChance = guard.BlockChance };
        Check(Troops.WorseUpgrade(guard, same) != null, "an upgrade that gains in only one stat is refused");
        foreach (var t in Troops.All.Where(t => !t.Wild && !t.Leader))
            foreach (var n in t.UpgradesTo.Select(Troops.Get))
                Check(Rules.Might(n, 10) > Rules.Might(t, 10), $"auto-resolve rates {n.Name} above {t.Name}");
    }

    /// D0: an upgrade beats the troop it came from, 30 against 30 (one link per class and tier step): it wins at least two
    /// of three fights and ends ahead over the three (a close bow duel can still go to the dice once).
    static void HigherTierWins()
    {
        int links = 0;
        foreach (var (from, to) in TierSample())
        {
            links++;
            int wins = 0;
            float margin = 0;
            for (int seed = 1; seed <= 3; seed++)
            {
                var (up, down) = TierFight(to.Id, from.Id, 910 + seed);
                if (up > down) wins++;
                margin += up - down;
            }
            Check(wins >= 2 && margin > 0, $"{to.Name} (tier {to.Tier}) beat {from.Name} (tier {from.Tier}): won {wins} of 3, margin {margin / 3:+0.00;-0.00}");
        }
        Console.WriteLine($"tiers: {links} upgrade links fought against their parents, every upgrade won");
    }

    /// D0: the gap is felt: a tier VI company beats twice as many tier III men of its culture and class (heavy horse
    /// needed T1's Ride down for this: before it, 40 lancers riding round 20 knights was a coin flip).
    static void TierGap()
    {
        foreach (var (culture, cls) in new[] { ("northern", UnitClass.Infantry), ("azure", UnitClass.Cavalry), ("crimson", UnitClass.Archer), ("azure", UnitClass.Skirmisher) })
        {
            var top = Troops.All.Where(t => t.Culture == culture && t.Class == cls && t.Tier == 6).OrderBy(t => t.Id).First();
            var low = Troops.All.Where(t => (t.Culture == culture || Factions.Open(culture, t.Culture)) && t.Class == cls && t.Tier == 3 && (t.Ammo > 0) == (top.Ammo > 0)).OrderBy(t => t.Culture == culture ? 0 : 1).ThenBy(t => t.Id).First();
            const int many = 40;
            var (a, b) = TierFight(top.Id, low.Id, 920, 20, many);
            Console.WriteLine($"tiers: 20 {top.Name} against {many} {low.Name}: {a:P0} against {b:P0} standing");
            Check(a * 20 > b * many, $"20 {top.Name} (VI) beat {many} {low.Name} (III)");
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

    /// TI0: the `--tiers` matrix: links keep their role, the three matchups set the parent's men as documented, and one bout
    /// of an upgrade against its parent at equal men goes to the upgrade.
    static void TierMatrix()
    {
        var links = BalanceReport.TierLinks();
        Check(links.Count > 100 && links.All(l => l.to.Tier > l.from.Tier && l.to.Class == l.from.Class), $"{links.Count} upgrade links, each up a tier in its class");
        var (from, to) = links.First(l => l.from.Id == "levies" && l.to.Class == UnitClass.Infantry);
        Check(BalanceReport.ParentMen(from, to, "men") == 30, "equal men: thirty a side");
        Check(BalanceReport.ParentMen(from, to, "upkeep") == (int)MathF.Round(30 * Rules.WageCost(to) / Rules.WageCost(from)), "equal upkeep: the parent's men by the wage ratio");
        Check(BalanceReport.ParentMen(from, to, "places") == (int)MathF.Round(30 * Rules.TierWeight(to.Tier) / Rules.TierWeight(from.Tier)), "equal places: by the tier weight");
        var (up, down, secs) = BalanceReport.TierBout(to, from, 30, 30, 7100);
        Check(up > down && secs > 0, $"{to.Name} beat as many {from.Name}: {up:P0} against {down:P0} in {secs:0} s");
    }

    /// TI1: the ladder: what each tier may do. Levies keep line, open ranks and the brace but learn no shield wall; tier II
    /// locks shields; the square and the wedge come at IV; relieving the line at III; levies never rally, V and VI twice;
    /// the grade follows the tier.
    static void TierLadder()
    {
        var b = Battle.Empty(5);
        Squad Of(string id, int x) => b.AddSquad(0, Troops.Get(id), 12, new(x, -10), 6, Vector2.UnitY);
        var levies = Of("levies", -30); var shields = Of("shieldmen", -10); var maa = Of("men_at_arms", 10);
        var four = Of(Troops.All.First(t => t.Tier == 4 && t.Class == UnitClass.Infantry && t.HasShield && !t.Wild).Id, 30);
        Check(!levies.Choices(true).Contains(Formation.ShieldWall) && levies.CannotForm(Formation.ShieldWall) == "NEEDS TIER II", "levies cannot lock shields");
        Check(levies.Choices(true).Contains(Formation.Line) && levies.Choices(true).Contains(Formation.Loose), "levies keep a line and open ranks");
        levies.Order = Order.Hold;
        Check(Battle.CanBrace(levies.Units[0]), "levies holding their line still brace against horse (the owner's call)");
        Check(shields.Choices(true).Contains(Formation.ShieldWall) && !shields.Choices(true).Contains(Formation.Square), "tier II locks shields, no square yet");
        Check(four.Choices(true).Contains(Formation.Square) && four.Choices(true).Contains(Formation.Wedge), "tier IV forms square and wedge");
        Check(b.CannotRelieve(shields, levies) != null && b.CannotSplit(maa) != "SPLITTING NEEDS TIER III", "relief and splitting from tier III");
        Check(Battle.RalliesOf(Troops.Get("levies")) == 0 && Battle.RalliesOf(Troops.Get("shieldmen")) == 1 && Battle.RalliesOf(Troops.All.First(t => t.Tier == 5 && !t.Wild)) == 2, "rallies by tier: none, once, twice");
        var grades = Enumerable.Range(1, 6).Select(t => Battle.GradeOf(Battle.DrillOf(Troops.All.First(x => x.Tier == t && !x.Wild && x.Drill < 0)))).ToArray();
        Check(grades.SequenceEqual(new[] { Grade.Rabble, Grade.Trained, Grade.Trained, Grade.Veteran, Grade.Veteran, Grade.Elite }), "grades follow the ladder: " + string.Join(", ", grades));
    }
}
