using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: effects (docs/COMPANIONS_MAGIC.md Part A). A man's totals (`Unit.Mods`) are his troop's passives
/// (`UnitType.Mods`, fixed at load) plus his company's (`Squad.Mods`: auras of his side's bearers), rebuilt every 0.5 s,
/// a quarter of the companies each tick-group so the cost stays per company, not per man.
public partial class Battle
{
    /// The passive traits a tier V-VII troop gets from its archetype when troops.json names none (D3): the first is its
    /// main trait, the second comes at tier VI.
    public static string[] PassivesFor(UnitType t)
    {
        if (t.Mount != null)
            return t.Look.Weapon switch
            {
                "lance" => new[] { "heavy_charge", "iron_skin" },
                "bow" or "longbow" or "crossbow" => new[] { "swift", "steady_aim" },
                _ => new[] { "swift", "fearsome" },
            };
        return t.Look.Weapon switch
        {
            "daneaxe" => new[] { "fearsome", "iron_skin" },
            "greatsword" => new[] { "fearsome", "tireless" },
            "longsword" when !t.HasShield => new[] { "fearsome", "tireless" },
            "pike" => new[] { "set_spears", "unshakeable" },
            "spear" => new[] { "set_spears", "tireless" },
            "longbow" or "bow" => new[] { "steady_aim", "sure_footed" },
            "crossbow" => t.Look.Shield == "pavise" ? new[] { "pavise", "steady_aim" } : new[] { "steady_aim", "iron_skin" },
            _ => t.HasShield ? new[] { "locked_shields", "unshakeable" } : new[] { "tireless", "sure_footed" },
        };
    }

    /// A culture's signature (D4): a third passive for its tier VII troops. Null: none yet (the rest come in T2b).
    public static string? SignatureOf(string culture) => culture switch
    {
        "order" => "zeal",
        _ => null,
    };

    /// A troop's passives: its archetype's two, and its culture's signature at tier VII.
    public static string[] PassivesWithSignature(UnitType t) =>
        t.Tier >= 7 && SignatureOf(t.Culture) is string sig ? PassivesFor(t).Append(sig).ToArray() : PassivesFor(t);

    /// How strongly a troop of this tier has its i-th passive: tier V its first at 0.7; VI the first in full and the
    /// second at 0.7; VII all three (the third is its culture's signature) in full. Never falls as the tier rises (D0).
    public static float PassiveWeight(int tier, int i) => tier switch
    {
        5 => i == 0 ? 0.7f : 0,
        6 => i == 0 ? 1 : i == 1 ? 0.7f : 0,
        >= 7 => i <= 2 ? 1 : 0,
        _ => 0,
    };

    /// A troop's fixed totals from its passives (auras are left to the company pass).
    public static Mods TroopMods(UnitType t)
    {
        var m = new Mods();
        for (int i = 0; i < t.Passives.Length; i++)
        {
            var e = Effects.Get(t.Passives[i]);
            float k = PassiveWeight(t.Tier, i);
            if (k > 0 && e.Radius <= 0) m.Add(e, k);
        }
        return m;
    }

    readonly List<Unit> auraBearers = new();
    /// Some man on the field bears an aura (set at Spawn). Without one, a man's totals are his troop's and nothing is rebuilt.
    bool anyAura;

    /// Every 0.5 s per company (staggered): its auras from bearers of its side in reach, then each man's totals.
    void UpdateEffects()
    {
        if (!anyAura) return;
        auraBearers.Clear();
        foreach (var u in Units)
            if (u.Active && u.Type.HasAura) auraBearers.Add(u);
        for (int i = 0; i < Squads.Count; i++)
        {
            var s = Squads[i];
            if ((tick + i) % 30 != 0) continue;
            s.Mods = AurasOn(s);
            foreach (var u in s.Units) u.Mods = u.Type.Mods + s.Mods;
        }
    }

    /// The auras on a company: each bearer of its side whose aura reaches the company's centre (its radius plus the company's).
    Mods AurasOn(Squad s)
    {
        var m = new Mods();
        foreach (var b in auraBearers)
        {
            if (b.Team != s.Team) continue;
            for (int i = 0; i < b.Type.Passives.Length; i++)
            {
                var e = Effects.Get(b.Type.Passives[i]);
                float k = PassiveWeight(b.Type.Tier, i);
                if (e.Radius > 0 && k > 0 && Vector2.DistanceSquared(b.Pos, s.Center) < (e.Radius + s.Radius) * (e.Radius + s.Radius)) m.Add(e, k);
            }
        }
        return m;
    }
}
