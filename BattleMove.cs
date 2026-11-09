using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: unit movement (steering, separation, ground, slope) and collisions.
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
            u.Slope = Slope(Terrain, u.Pos, u.Vel);
            float pace = SlopeFactor(u.Slope, u.Mounted) * GroundSpeed(u); // the ground and the slope set the pace
            if (pace < 1) pace += (1 - pace) * u.Mods.Footing;              // sure-footed men shrug off part of it
            u.GroundPace = pace * (1 + u.Mods.Speed);
            if (u.Mounted) Stumble(u, dt);
            u.Vel *= u.GroundPace;
            u.Pos += u.Vel * dt;
            u.Anim += dt * u.Vel.Length() * (u.Mounted ? 0.45f : 0.9f); // stride-locked walk / gallop cycle
        }
        Separate(dt); // near pairs only, through the spatial grid (BattleGrid.cs)
        KeepRing(); // the men give two clashing champions room (MA7)
        // Trees and boulders are solid; a horse that rides into one pulls up hard.
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            float r = u.Mounted ? 0.9f : BodyRadius(u) - 0.025f;
            if (Terrain.PushOut(ref u.Pos, r, u.Mounted) && u.Mounted && u.Speed > 3) u.Speed *= 0.6f;
            // Pressed against a fence or hedge: edge along it toward the nearer end.
            if (Terrain.Features.Count > 0 && u.Vel.LengthSquared() > 0.1f && Terrain.GoRound(u.Pos, r, Vector2.Normalize(u.Vel), u.Mounted) is var round && round != Vector2.Zero)
            {
                u.Pos += round * u.Vel.Length() * 0.8f * dt;
                Terrain.PushOut(ref u.Pos, r, u.Mounted);
            }
        }
        EnforceWall(before);
    }

    /// The ground slows men and horses (Data/ground.json): water a lot, a ford or a wood less; a road is quick.
    /// (Pathfinder halves what woods and marsh cost your side.)
    /// Deep water also disorders a formation (no bracing).
    float GroundSpeed(Unit u)
    {
        var g = Terrain.GroundDefAt(u.Pos);
        float pace = u.Mounted ? g.HorseSpeed : g.FootSpeed;
        // Pathfinder: your men lose half the pace woods, scrub and marsh cost them.
        if (pace < 1 && u.Team == 0 && Perk("Pathfinder") && Terrain.GroundAt(u.Pos) is GroundKind.Wood or GroundKind.DenseWood or GroundKind.Scrub or GroundKind.Mud) pace = 1 - (1 - pace) / 2;
        return pace;
    }

    /// A horse galloping over stones, scrub or mud may stumble: it checks hard and the lance comes up (no harm done).
    void Stumble(Unit u, float dt)
    {
        float chance = Terrain.GroundDefAt(u.Pos).Stumble;
        if (chance <= 0 || u.Speed < u.Horse!.Gallop * 0.7f || rng.NextSingle() >= chance * dt) return;
        u.Speed *= 0.4f;
        u.Couched = false;
        if (u.IsPlayer) PlayerHit = ("STUMBLE", Time);
    }

    /// Rise per metre along the way he is going (negative downhill).
    static float Slope(Terrain t, Vector2 pos, Vector2 vel) => vel.LengthSquared() > 0.01f ? Vector2.Dot(t.Gradient(pos), Vector2.Normalize(vel)) : 0;

    /// Uphill is slow, downhill a little quicker: a multiplier on ground speed. A horse feels the slope more than a man.
    internal static float SlopeFactor(Terrain t, Vector2 pos, Vector2 vel, bool mounted = false) => SlopeFactor(Slope(t, pos, vel), mounted);
    static float SlopeFactor(float slope, bool mounted) => mounted ? Math.Clamp(1 - slope * 2f, 0.5f, 1.2f) : Math.Clamp(1 - slope * 1.5f, 0.55f, 1.15f);

    /// A horse at speed running into an enemy footman: knockdown and damage, unless the man is braced.
    void Impact(Unit r, Unit f, Vector2 n, float dt)
    {
        r.Speed *= 1 - 1.2f * dt; // pushing through bodies bogs a charge down
        float closing = Vector2.Dot(r.Vel - f.Vel, n);
        if (f.ImpactCd > 0 || closing < 4) return;
        f.ImpactCd = 0.8f;
        Sound(Cue.Impact, f.Pos);
        r.Contact = true;
        if (f.Type.Huge || Braced(f) && Vector2.Dot(-n, f.Dir) > 0.4f) // a troll stands a horse off like a spear wall
        {
            r.Speed *= 0.35f; // the horse balks at the wall
            HitHorse(r, closing * 2.6f, f);
            return;
        }
        r.Speed *= f.Type.Class == UnitClass.Infantry && f.Squad is { Loose: false } ? 0.55f : 0.75f;
        // A spearman shoved by a horse still gets his point into it.
        if (f.Type.Class == UnitClass.Skirmisher && f.Type.VsMounted > 1) HitHorse(r, closing * 1.6f * f.Type.VsMounted, f);
        float real = Damage(r, f, closing * 2.6f * (r.Squad?.Formation == Formation.Wedge ? 1.3f : 1) * Terrain.GroundDefAt(f.Pos).Charge, 5); // no charge over bad ground
        if (f.Active)
        {
            Stagger(f, 1.1f); // knocked flat
            f.Pos += n * 0.6f;
        }
        if (r.IsPlayer) PlayerHit = ($"TRAMPLE {real:0}", Time);
        foreach (var u in Near(f.Pos, 5, nearBrace))
            if (u.Active && u.Team == f.Team && Vector2.DistanceSquared(u.Pos, f.Pos) < 25) u.Morale -= 2; // the shock spreads
    }
}
