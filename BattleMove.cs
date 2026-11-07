using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: unit movement (steering, separation, terrain, wading, slope) and collisions.
public partial class Battle
{
    void Move(float dt)
    {
        var before = Wall != null ? Units.Select(u => u.Pos).ToArray() : Array.Empty<Vector2>();
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            if (u.Mounted)
            {
                if (!u.IsPlayer) Steer(u, dt);
                u.Vel = u.HeadingDir * u.Speed;
            }
            else u.Vel = u.Wish * (u.Squad is { } fs && !u.Routing && fs.Order != Order.Charge ? fs.Pace : 1) * (u.Mounted ? 1 : Fatigue(u));
            u.Pos += u.Vel * SlopeFactor(Terrain, u.Pos, u.Vel) * Wading(u) * dt;
            u.Anim += dt * u.Vel.Length() * (u.Mounted ? 0.45f : 0.9f); // stride-locked walk / gallop cycle
        }
        Separate(dt); // near pairs only, through the spatial grid (BattleGrid.cs)
        // Trees and boulders are solid; a horse that rides into one pulls up hard.
        foreach (var u in Units)
            if (u.Active && Terrain.PushOut(ref u.Pos, u.Mounted ? 0.9f : 0.35f) && u.Mounted && u.Speed > 3) u.Speed *= 0.6f;
        EnforceWall(before);
    }

    /// Water slows men and horses: a ford a little, the river itself a lot (and it disorders a formation: no bracing).
    float Wading(Unit u)
    {
        if (Terrain.Rivers.Count == 0) return 1;
        float d = Terrain.WaterDepth(u.Pos);
        return d == 0 ? 1 : d < 0.5f ? 0.8f : u.Mounted ? 0.55f : 0.45f;
    }

    /// Uphill is slow, downhill a little quicker: a multiplier on ground speed.
    internal static float SlopeFactor(Terrain t, Vector2 pos, Vector2 vel)
    {
        float slope = vel.LengthSquared() > 0.01f ? Vector2.Dot(t.Gradient(pos), Vector2.Normalize(vel)) : 0;
        return Math.Clamp(1 - slope * 1.5f, 0.55f, 1.15f);
    }

    /// A horse at speed running into an enemy footman: knockdown and damage, unless the man is braced.
    void Impact(Unit r, Unit f, Vector2 n, float dt)
    {
        r.Speed *= 1 - 1.2f * dt; // pushing through bodies bogs a charge down
        float closing = Vector2.Dot(r.Vel - f.Vel, n);
        if (f.ImpactCd > 0 || closing < 4) return;
        f.ImpactCd = 0.8f;
        Sound(Cue.Impact, f.Pos);
        r.Contact = true;
        if (Braced(f) && Vector2.Dot(-n, f.Dir) > 0.4f)
        {
            r.Speed *= 0.35f; // the horse balks at the wall
            HitHorse(r, closing * 2.6f, f);
            return;
        }
        r.Speed *= f.Type.Class == UnitClass.Infantry && f.Squad is { Loose: false } ? 0.55f : 0.75f;
        // A spearman shoved by a horse still gets his point into it.
        if (f.Type.Class == UnitClass.Skirmisher && f.Type.VsMounted > 1) HitHorse(r, closing * 1.6f * f.Type.VsMounted, f);
        float real = Damage(r, f, closing * 2.6f * (r.Squad?.Formation == Formation.Wedge ? 1.3f : 1), 5);
        if (f.Active)
        {
            Stagger(f, 1.1f); // knocked flat
            f.Pos += n * 0.6f;
        }
        if (r.IsPlayer) PlayerHit = ($"TRAMPLE {real:0}", Time);
        foreach (var u in Units)
            if (u.Active && u.Team == f.Team && Vector2.DistanceSquared(u.Pos, f.Pos) < 25) u.Morale -= 2; // the shock spreads
    }
}
