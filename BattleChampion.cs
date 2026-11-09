using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: an enemy champion's aura (docs/KNIGHT_ORDERS.md 6.1, K6): an order knight leading his army, or met in a duel,
/// with a pool like yours, a blade he lights when he is in reach of a foe, aura armour from tier 3, and his order's forms,
/// fired by simple rules as mages cast (enough foes in reach, a foe ahead to leap at). Thrown forms warn as yours do.
public partial class Battle
{
    public float FoeAura;
    public int FoeTier => Options.EnemyAura;
    /// The forms he has used this battle, in order (tests, the log).
    public readonly List<string> FoeFormsUsed = new();
    readonly Dictionary<string, float> foeReady = new();
    float foeFought = -9, foeThink;
    int foeGreat;

    void SetupChampion() => FoeAura = FoeTier > 0 ? AuraMax : 0;

    /// His blade burns while a foe is in reach and he has aura for it (tier 2+).
    bool FoeBladeLit => FoeTier >= 2 && FoeAura >= AuraLow && EnemyCaptain is { Active: true, EnemyDist: < 4 };
    bool FoeBlade(Unit a, bool ranged) => !ranged && FoeTier >= 2 && a == EnemyCaptain && FoeBladeLit;

    void UpdateChampion(float dt)
    {
        for (int i = PendingForms.Count - 1; i >= 0; i--)
            if (PendingForms[i].Foe && Time >= PendingForms[i].At) { var pf = PendingForms[i]; PendingForms.RemoveAt(i); if (EnemyCaptain.Active) Land(EnemyCaptain, pf); }
        if (FoeTier <= 0 || !EnemyCaptain.Active) return;
        bool fighting = Time - foeFought < 4;
        if (FoeBladeLit) FoeAura = MathF.Max(0, FoeAura - BladeDrain * dt);
        else FoeAura = MathF.Min(AuraMax, FoeAura + (fighting ? (FoeTier >= 4 ? 1.5f : 0.5f) : 2f) * dt);
        foeThink -= dt;
        if (foeThink > 0) return;
        foeThink = 1;
        ChampionForms(EnemyCaptain);
    }

    /// His aura armour (tier 3+, not spent) takes a share of a blow, as yours does.
    float FoeSoak(Unit a, Unit t, float real, bool ranged)
    {
        if (a == EnemyCaptain || t == EnemyCaptain) foeFought = Time;
        if (t != EnemyCaptain || FoeTier < 3 || FoeAura < AuraLow) return real;
        float soaked = real * (ranged ? ArrowSoak : ArmourSoak);
        FoeAura = MathF.Max(0, FoeAura - soaked * SoakCost);
        return real - soaked;
    }

    /// One form a second at most: the first his tier, aura and cooldowns allow whose rule fits.
    void ChampionForms(Unit c)
    {
        if (c.Action is UnitAction.Windup or UnitAction.Recover or UnitAction.Stagger) return;
        foreach (var id in Options.EnemyForms)
        {
            if (!Forms.Has(id)) continue;
            var f = Forms.Get(id);
            if (f.Tier > FoeTier || FoeAura < f.Aura || Time < foeReady.GetValueOrDefault(id) || f.Great && foeGreat >= 1) continue;
            if (!ChampionWants(c, f)) continue;
            FoeAura -= f.Aura;
            foeReady[id] = Time + f.Cooldown;
            if (f.Great) foeGreat++;
            FoeFormsUsed.Add(id);
            Sound(Cue.Shout, c.Pos);
            if (f.Great) Log($"{c.Type.Name}: {f.Name}!");
            if (f.Kind is "slash" or "volley") PendingForms.Add(new PendingForm(f, Time, Time + f.Warn, c.Pos, c.Dir, 1, Foe: true));
            else Use(c, f, 1);
            return;
        }
    }

    /// The rule for each kind he can use; the kinds that need a player's timing (focus, guard, edge, counter, shot) he never does.
    bool ChampionWants(Unit c, FormDef f)
    {
        switch (f.Kind)
        {
            case "burst" or "shout" or "drain": return FoesNear(c.Pos, f.Radius, 99, c.Team).Count >= 3;
            case "volley" or "burn": return FoesNear(c.Pos + c.Dir * f.Range, f.Radius, 99, c.Team).Count >= 3;
            case "buff": return c.EnemyDist < 15;
            case "slash":
                return Near(c.Pos, f.Range + 1, nearKill).Count(t => t.Active && t.Team != c.Team && InCone(c.Pos, c.Dir, t.Pos, f.Range, f.Radius)) >= 2;
            case "leap":
                if (c.Mounted) return false;
                var prey = FoesNear(c.Pos, f.Range, 1, c.Team).FirstOrDefault(t => Vector2.DistanceSquared(t.Pos, c.Pos) > 4);
                if (prey == null) return false;
                c.Face(prey.Pos - c.Pos);
                return true;
            default: return false;
        }
    }
}
