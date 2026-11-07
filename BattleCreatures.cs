using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the rules of big creatures (docs/CREATURES.md phase 3): a troll's sweeping, throwing blows, its healing, and the sun.
public partial class Battle
{
    /// Open daylight: a troll is slow and weak in it and does not heal. Dawn, dusk, night and thick fog are troll weather.
    public bool Sunlit => Options.Weather is { Night: false, Hour: > 7.5f and < 18.5f, Fog: < 0.5f };

    /// Body radius on the ground (a huge creature takes more room) and its weight when bodies shove each other.
    static float BodyRadius(Unit u) => u.Mounted ? 1.0f : u.Type.Huge ? 0.375f * u.Type.Size : 0.375f;
    static float BodyMass(Unit u) => u.Mounted ? 6 : u.Type.Huge ? 8 : 1;

    /// A sweeping blow lands on its target and carries on into the men beside it (up to two more).
    void Sweep(Unit a, Unit first, float dmg, float reach)
    {
        int more = 0;
        foreach (var t in Near(a.Pos, reach + 1, nearBrace).Where(t => t != first && t.Team != a.Team && t.Active)
                     .OrderBy(t => Vector2.DistanceSquared(t.Pos, a.Pos)).ThenBy(t => t.Index).ToList())
            if (InArc(a, t, reach, 0.1f) && more++ < 2) Hit(a, t, dmg * 0.8f, false);
    }

    /// A blow from something huge throws a man off his feet and back, and shakes the men around him.
    void Knock(Unit a, Unit t)
    {
        if (!t.Active || t.Type.Huge || t.Mounted || Wall != null) return; // no throwing men through a siege wall
        var away = t.Pos - a.Pos;
        if (away.LengthSquared() < 1e-4f) away = a.Dir;
        t.Pos += Vector2.Normalize(away) * 1.3f;
        Stagger(t, 1.0f);
        foreach (var u in Near(t.Pos, 4, nearBrace))
            if (u.Active && u.Team == t.Team && Vector2.DistanceSquared(u.Pos, t.Pos) < 16) u.Morale -= 1.5f;
    }

    /// A troll heals while nothing has hurt it for a few seconds, but not under the sun.
    void Regen(Unit u, float dt)
    {
        if (Sunlit || Time - u.HurtAt < 3 || u.Hp >= u.Type.MaxHp) return;
        u.Hp = MathF.Min(u.Type.MaxHp, u.Hp + u.Type.MaxHp * 0.04f * dt);
    }
}
