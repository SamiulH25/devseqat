using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of effects and passive traits (docs/COMPANIONS_MAGIC.md Part A, D3; Effects.cs, BattleEffects.cs).
public static partial class SelfTest
{
    /// A troop with its stats and style but no passive traits.
    static UnitType NoPassives(string troop)
    {
        var t = Troops.Get(troop).Clone();
        t.Passives = Array.Empty<string>();
        t.Mods = new Mods();
        t.HasAura = false;
        return t;
    }

    /// Every effect in the data is sound, its icon can be drawn, and every troop's passives exist.
    static void EffectsValidate()
    {
        Check(!Effects.Validate().Any(), "effects.json validates: " + string.Join("; ", Effects.Validate()));
        Check(Effects.All.All(e => Kit.IconNames.Contains(e.Icon)), "every effect's icon is one the kit can draw");
        Check(Troops.All.All(t => t.Passives.All(Effects.Has)), "every troop's passives are effects in the data");
    }

    /// D3: tier V-VII troops carry passives (two, and a signature at VII where the culture has one); lower tiers none;
    /// an upgrade that keeps its weapon keeps its passives, and the weights never fall with the tier.
    static void PassivesByTier()
    {
        foreach (var t in Troops.All.Where(t => !t.Wild && !t.Leader))
        {
            if (t.Tier < 5) Check(t.Passives.Length == 0, $"{t.Name} (tier {t.Tier}) has no passives");
            else Check(t.Passives.Length >= 2, $"{t.Name} (tier {t.Tier}) has its passives");
            foreach (var n in t.UpgradesTo.Select(Troops.Get).Where(n => t.Passives.Length > 0 && n.Look.Weapon == t.Look.Weapon && n.HasShield == t.HasShield && n.Mount == null == (t.Mount == null)))
                Check(t.Passives.Take(2).SequenceEqual(n.Passives.Take(2)), $"{n.Name} keeps {t.Name}'s passives");
        }
        for (int tier = 5; tier < 7; tier++)
            for (int i = 0; i < 3; i++)
                Check(Battle.PassiveWeight(tier + 1, i) >= Battle.PassiveWeight(tier, i), $"passive {i} is no weaker at tier {tier + 1} than at {tier}");
        Check(Troops.All.Where(t => t.Culture == "order" && t.Tier == 7).All(t => t.Passives.Contains("zeal")), "the Order's tier VII troops carry Zeal");
        Console.WriteLine($"effects: {Troops.All.Count(t => t.Passives.Length > 0)} troops with passive traits");
    }

    /// D3: iron skin turns arrows (a man with it loses less to the same arrow); a fearsome man's blow shakes more.
    static void PassivesBite()
    {
        float Lost(UnitType target, bool ranged)
        {
            var (b, a, _) = StyleScene("levies");
            var t = b.AddSquad(1, target, 1, new(0, 1.3f), 1, Vector2.UnitY).Units[0];
            b.Update(0);
            t.Pos = new(0, 1.3f); t.Face(Vector2.UnitY); // his back to the blow: no block
            float hp = t.Hp;
            b.Hit(a, t, 40, ranged);
            return hp - t.Hp;
        }
        float with = Lost(Troops.Get("valhalla_reavers"), true), without = Lost(NoPassives("valhalla_reavers"), true); // tier VI: iron skin is his second trait
        Check(with < without, $"iron skin turns part of an arrow ({with:0.0} against {without:0.0})");
        Check(MathF.Abs(Lost(Troops.Get("valhalla_reavers"), false) - Lost(NoPassives("valhalla_reavers"), false)) < 0.01f, "but not a blade");

        float Shaken(UnitType striker)
        {
            var (b, a, foes) = StyleScene(striker, new Vector2(0, 1.3f));
            float m = foes[0].Morale;
            b.Hit(a, foes[0], 1, false);
            return m - foes[0].Morale;
        }
        Check(Shaken(Troops.Get("doomcleavers")) > Shaken(NoPassives("doomcleavers")), "a fearsome man's blow shakes the man he strikes more");
    }

    /// Part A: an aura reaches the companies of its side within its radius and no others, and stops when its bearer falls.
    static void AuraReachesCompanies()
    {
        var b = Battle.Empty(71);
        var saints = b.AddSquad(0, Troops.Get("ashen_saints"), 6, new(0, 0), 3, Vector2.UnitY);
        var near = b.AddSquad(0, Troops.Get("levies"), 6, new(12, 0), 3, Vector2.UnitY);
        var far = b.AddSquad(0, Troops.Get("levies"), 6, new(70, 0), 3, Vector2.UnitY);
        var foe = b.AddSquad(1, Troops.Get("levies"), 6, new(0, 14), 3, -Vector2.UnitY);
        foreach (var s in b.Squads) s.Order = Order.Hold;
        Run(b, 1);
        Check(near.Units.All(u => u.Mods.MoralePerSec > 0), "Zeal reaches a company of its side 12 m away");
        Check(far.Units.All(u => u.Mods.MoralePerSec == 0), "but not one 70 m away");
        Check(foe.Units.All(u => u.Mods.MoralePerSec == 0), "nor the enemy");
        foreach (var u in saints.Units) b.Kill(u, foe.Units[0]);
        Run(b, 1);
        Check(near.Units.All(u => u.Mods.MoralePerSec == 0), "it ends when its bearers fall");
    }

    /// D0 for passives: each archetype's traits make its men fight at least as well as the same men without them
    /// (20 against 30 close foes, three seeds; score as StylesOnlyAdd).
    static void PassivesOnlyAdd()
    {
        foreach (var (troop, foe) in new[] { ("allfather_guard", "iron_legionaries"), ("doomcleavers", "citadel_sentinels"), ("imperial_pikemen", "grand_phalanx"),
                     ("grail_knights", "paladin_knights"), ("tempest_spears", "storm_riders") })
        {
            float Score(UnitType t)
            {
                float score = 0;
                for (int seed = 1; seed <= 3; seed++)
                {
                    var b = Battle.Empty(960 + seed);
                    var sa = b.AddSquad(0, t, 20, new(0, -12), 10, Vector2.UnitY);
                    var sb = b.AddSquad(1, Troops.Get(foe), 30, new(0, 12), 10, -Vector2.UnitY);
                    sa.Order = sb.Order = Order.Charge;
                    sa.Target = sb; sb.Target = sa;
                    for (int i = 0; i < 40 * 60; i++) b.Update(1 / 60f);
                    score += Standing(sa) + sb.StartCount - Standing(sb);
                }
                return score;
            }
            var real = Troops.Get(troop);
            float with = Score(real), without = Score(NoPassives(troop));
            Console.WriteLine($"effects: {real.Name} ({string.Join(", ", real.Passives)}) against 30 {Troops.Get(foe).Name}: {with:0} with passives, {without:0} without");
            Check(with >= without - 1, $"passives do not make {real.Name} fight worse");
        }
    }
}
