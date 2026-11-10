using Microsoft.Xna.Framework;

namespace Ironfield;

/// How the blows of the best troops land (docs/archive/COMPANIONS_MAGIC.md D2). All passive: the men fight this way on their own.
public enum AttackStyle { None, Crush, Sweep, Riposte, Bash, Thrust, HoldOff, RideDown, CutWheel, Plunge, QuickLoose, Punch }

/// Battle: attack styles of tier V-VII troops. Each style hooks one place in the damage path (BattleCombat.cs) and
/// reuses the creature blows (`Sweep`, `Knock`, BattleCreatures.cs) sized for men. Rank 1-3 = tier V-VII.
public partial class Battle
{
    /// The style a troop's weapon gives it, when troops.json names none.
    public static AttackStyle StyleFor(UnitType t) => t.Look.Weapon switch
    {
        "daneaxe" or "warhammer" => AttackStyle.Crush,
        "greatsword" => AttackStyle.Sweep,
        "longsword" => t.HasShield ? AttackStyle.Riposte : AttackStyle.Sweep,
        "scimitar" => t.Mounted() ? AttackStyle.CutWheel : t.HasShield ? AttackStyle.Riposte : AttackStyle.Sweep, // the curved blade turns a blow and cuts back
        "sword" or "axe" or "mace" or "falchion" => t.Mounted() ? AttackStyle.CutWheel : t.HasShield ? AttackStyle.Bash : AttackStyle.None,
        "pike" or "halberd" => AttackStyle.Thrust,
        "spear" or "glaive" or "javelin" => AttackStyle.HoldOff,
        "sling" => AttackStyle.QuickLoose,
        "lance" => AttackStyle.RideDown,
        "longbow" => AttackStyle.Plunge,
        "bow" => AttackStyle.QuickLoose,
        "crossbow" => AttackStyle.Punch,
        _ => AttackStyle.None,
    };

    /// Player-facing name and one line on what the style does (tooltips).
    public static (string name, string line) StyleText(AttackStyle s) => s switch
    {
        AttackStyle.Crush => ("Crush", "Overhead blows that get past a shield and stagger the man."),
        AttackStyle.Sweep => ("Sweep", "A wide cut that carries into the man beside."),
        AttackStyle.Riposte => ("Riposte", "After a block, strikes back at once and harder."),
        AttackStyle.Bash => ("Shield bash", "Every third blow is a bash that gets past his guard and staggers him."),
        AttackStyle.Thrust => ("Thrust through", "A thrust that lands carries on to the man behind."),
        AttackStyle.HoldOff => ("Hold off", "A horse coming on shies from the point; a man struck mid-swing loses his blow."),
        AttackStyle.RideDown => ("Ride down", "A lance hit throws the man down and the horse tramples the next."),
        AttackStyle.CutWheel => ("Cut and wheel", "From the saddle each cut carries into the man beside."),
        AttackStyle.Plunge => ("Plunging shot", "At long range arrows drop past shields held to the front."),
        AttackStyle.QuickLoose => ("Quick loose", "Shoots faster."),
        AttackStyle.Punch => ("Punch", "Close bolts throw men back and can bring a horse down."),
        _ => ("", ""),
    };

    /// Set while a style's follow-up blow is struck, so a follow-up does not start another (no chains through a crowd).
    bool followUp;

    static bool Styled(Unit a, AttackStyle s) => a.Type.Style == s && !a.IsPlayer;

    /// The swing a styled man favours: an axe comes down overhead, a greatsword cuts from the side, a pike thrusts (a spear
    /// keeps his mix: thrusting only made spearmen worse, StylesOnlyAdd).
    AttackDir? StyleSwing(Unit u) => u.IsPlayer ? null : u.Type.Style switch
    {
        AttackStyle.Crush => AttackDir.Overhead,
        AttackStyle.Sweep => rng.Next(2) == 0 ? AttackDir.Left : AttackDir.Right,
        AttackStyle.Thrust => AttackDir.Thrust,
        _ => null,
    };

    /// A swing starts: a riposte owed comes fast; every third blow of a shield man is a bash.
    void StyleSwingStarts(Unit u)
    {
        if (u.IsPlayer) return;
        if (u.RiposteReady) u.ActionTimer = u.Type.Windup * 0.3f;
        if (u.Type.Style == AttackStyle.Bash) u.Blows++;
    }

    /// Damage of a styled man's blow as it leaves his hand (a riposte strikes harder).
    float StyleBlow(Unit a, float dmg)
    {
        if (a.IsPlayer) return dmg;
        if (a.RiposteReady) { a.RiposteReady = false; return dmg * (1.1f + 0.1f * a.Type.StyleRank); }
        return dmg; // a bash lands as hard as a cut: a style only adds (D0)
    }

    static bool Bashing(Unit a) => a.Type.Style == AttackStyle.Bash && a.Blows % 3 == 0 && a.Blows > 0;

    /// How much of the target's block chance is left against this blow: a crush gets past part of any guard, a bash half,
    /// and a plunging arrow drops past a shield held to the front.
    static float StyleGuard(Unit a, Unit t, bool ranged, bool plunge)
    {
        if (a.IsPlayer) return 1;
        int r = a.Type.StyleRank;
        return a.Type.Style switch
        {
            AttackStyle.Crush when !ranged => 0.75f - 0.1f * r,
            AttackStyle.Bash when !ranged && Bashing(a) => 0.5f,
            AttackStyle.Plunge when ranged && plunge && t.Type.HasShield => 0.6f - 0.1f * r,
            _ => 1,
        };
    }

    /// A man blocked a blow: a riposte troop strikes back at once.
    static void StyleBlocked(Unit t)
    {
        if (t.Type.Style != AttackStyle.Riposte || t.IsPlayer) return;
        t.RiposteReady = true;
        t.Cooldown = 0;
    }

    /// A styled blow landed on t (after its damage): what it does beyond the damage.
    void StyleLanded(Unit a, Unit t, float dmg, bool ranged, bool lance)
    {
        if (followUp || a.IsPlayer || (a.Type.StyleRank == 0 && a.Mods.Whirl <= 0)) return;
        int r = a.Type.StyleRank;
        followUp = true;
        switch (a.Type.Style)
        {
            case AttackStyle.Crush when !ranged:
                if (t.Active) Stagger(t, 0.25f + 0.1f * r);
                break;
            case AttackStyle.Sweep when !ranged && !a.Mounted:
                Sweep(a, t, dmg, ReachOf(a) + 0.4f, r >= 3 ? 2 : 1, 0.6f);
                break;
            case AttackStyle.CutWheel when !ranged && a.Mounted:
                Sweep(a, t, dmg, MountedMeleeReach + 0.35f, 1, 0.5f + 0.1f * r);
                break;
            case AttackStyle.Bash when !ranged && Bashing(a):
                Knock(a, t, 0.15f, 0.6f + 0.1f * r, 0.5f); // a shove: he loses his turn, not his place (a longer push only gave him room)
                break;
            case AttackStyle.Thrust when !ranged:
                if (Behind(a, t, 1.6f) is Unit next) Hit(a, next, dmg * (0.3f + 0.1f * r), false);
                break;
            case AttackStyle.HoldOff when !ranged && t.Active:
                // Only what the point stops: a horse coming on shies, a man mid-swing loses his blow. Pushing or
                // staggering every man struck only broke the spearmen's own line (StylesOnlyAdd).
                if (t.Mounted && Closing(t, a) > 1) { Stagger(t, 0.3f + 0.1f * r); t.Speed *= 0.5f; }
                else if (!t.Mounted && t.Action == UnitAction.Stagger) t.ActionTimer = MathF.Max(t.ActionTimer, 0.5f + 0.15f * r); // his blow was cut off (Damage): he stays off balance longer
                break;
            case AttackStyle.RideDown when lance:
                var trampled = Behind(a, t, 2.2f); // found before the man is thrown back past him
                if (t.Active) Knock(a, t, 1.0f + 0.3f * r, 1.0f, 1.5f);
                if (trampled != null) Hit(a, trampled, dmg * (0.25f + 0.1f * r), false);
                break;
            case AttackStyle.Punch when ranged && Vector2.DistanceSquared(a.Pos, t.Pos) < 20 * 20:
                if (t.Mounted && rng.NextSingle() < 0.05f * r) HitHorse(t, t.HorseHp + 1, a);
                else Knock(a, t, 0.4f + 0.2f * r, 0.4f, 0.5f);
                break;
        }
        // Whirlwind (a signature, D4): every blow carries into the man beside, on top of the style.
        if (!ranged && !lance && a.Mods.Whirl > 0 && a.Type.Style is not (AttackStyle.Sweep or AttackStyle.CutWheel))
            Sweep(a, t, dmg, ReachOf(a) + 0.4f, 1, a.Mods.Whirl);
        followUp = false;
    }

    /// How fast t closes on a (m/s along the line between them).
    static float Closing(Unit t, Unit a)
    {
        var to = a.Pos - t.Pos;
        float l = to.Length();
        return l < 0.01f ? 0 : Vector2.Dot(t.Vel - a.Vel, to / l);
    }

    /// The nearest foe of a standing behind t, on a's line through t, within `depth` metres of t.
    Unit? Behind(Unit a, Unit t, float depth)
    {
        var line = t.Pos - a.Pos;
        if (line.LengthSquared() < 1e-4f) line = a.Dir;
        line.Normalize();
        Unit? best = null;
        float bestD = float.MaxValue;
        foreach (var u in Near(t.Pos, depth + 0.5f, nearBrace))
        {
            if (u == t || !u.Active || u.Team == a.Team) continue;
            var d = u.Pos - t.Pos;
            float along = Vector2.Dot(d, line);
            if (along <= 0.2f || along > depth || MathF.Abs(d.X * line.Y - d.Y * line.X) > 0.7f) continue;
            if (along < bestD || (along == bestD && u.Index < best!.Index)) (best, bestD) = (u, along);
        }
        return best;
    }

    /// A styled bow's reload: a quick-loose archer spans and draws faster.
    static float StyleReload(Unit u) => Styled(u, AttackStyle.QuickLoose) ? 1 - 0.08f * u.Type.StyleRank : 1;
}

static class StyleExtensions
{
    /// A troop that rides (the style table runs before a Unit exists).
    public static bool Mounted(this UnitType t) => t.Mount != null;
}
