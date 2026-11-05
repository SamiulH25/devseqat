using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Draws a unit as a puppet: every body part is its own pixel-art sprite placed on a 3D skeleton.
/// - Billboard parts (head, helm, torso, hands, boots) face the camera and pick a front/side/back sprite.
/// - Limbs and weapons stretch between two joints, turning around their own axis to face the camera.
/// - "Long" parts (horse body, neck, head) also span two joints but keep their top edge up.
/// - Shields are flat quads fixed in the world.
/// Parts move in 3D, so turning and swings stay continuous from any camera angle.
/// Unit space: +X right hand, +Y up, +Z facing; metres.
sealed class Puppet
{
    const float Saddle = 0.87f; // rider origin height when mounted

    readonly Art art;
    readonly Verts<VertexPositionColorTexture> verts;
    Vector3 camPos, camRight, camUp, camFwd;

    // Current frame.
    Vector3 origin, fwd, right;
    string pal = "", view = "f";
    bool flip;
    float scale;
    Color tint;

    public Puppet(Art art, Verts<VertexPositionColorTexture> verts) { this.art = art; this.verts = verts; }

    public void Begin(Matrix view)
    {
        var inv = Matrix.Invert(view);
        camPos = inv.Translation;
        camRight = inv.Right;
        camUp = inv.Up;
        camFwd = inv.Forward;
    }

    static float Rad(float deg) => MathHelper.ToRadians(deg);
    Vector3 W(Vector3 l) => origin + (right * l.X + Vector3.Up * l.Y + fwd * l.Z) * scale;

    void Frame(Vector2 pos, float yaw, float y, float s)
    {
        scale = s;
        origin = new Vector3(pos.X, y, pos.Y);
        fwd = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        right = new Vector3(-fwd.Z, 0, fwd.X);
        var toCam = camPos - origin;
        toCam.Y = 0;
        if (toCam.LengthSquared() < 1e-4f) toCam = new Vector3(-camFwd.X, 0, -camFwd.Z);
        float facing = Vector3.Dot(fwd, Vector3.Normalize(toCam));
        view = facing > 0.7f ? "f" : facing < -0.7f ? "b" : "s";
        flip = Vector3.Dot(fwd, camRight) < 0; // side sprites face right
    }

    /// Draw a unit standing on ground height `ground`.
    public void Draw(Unit u, float ground)
    {
        if (u.Fled) return;
        pal = "/" + (u.IsPlayer ? 2 : u.Team);
        tint = u.HitFlash > 0 ? new Color(255, 110, 110) : u.Routing ? new Color(165, 165, 165) : Color.White;
        float lift = ground;
        if (u.Mounted && !u.Dead) lift = ground + Saddle + Horse(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, true);
        Frame(u.Pos, u.ViewFacing, lift, u.Type.Class == UnitClass.Captain ? 1.12f : 1);

        if (u.Dead) { Corpse(u); return; }
        var p = PoseOf(u);
        var cls = u.Type.Class;

        if (!u.Mounted)
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * 0.12f, 0.68f, 0);
                float s = Rad(p.Leg * side);
                var foot = hip + new Vector3(0, -0.6f * MathF.Cos(s), 0.6f * MathF.Sin(s));
                Axial(hip, foot, "leg");
                Bill(foot + new Vector3(0, 0.02f, 0.03f), "boot", true);
            }
        Bill(new(0, 1.0f, 0), "torso", true);
        Bill(new(0, 1.53f, 0.02f), "head", true);
        Bill(new(0, 1.63f, 0.02f), cls == UnitClass.Skirmisher ? "hood" : "helm", true, 0.04f);
        if (cls == UnitClass.Captain)
        {
            Bill(new(0, 1.86f, -0.02f), "plume", false, 0.05f);
            Bill(new(0, 0.92f, -0.17f), "cape", false);
        }
        if (cls == UnitClass.Cavalry) Bill(new(0, 1.8f, -0.02f), "plume", false, 0.05f);

        var hand = Arm(1, p.ArmA, p.Yaw);
        var off = Arm(-1, p.OffA, 0);
        var dir = RotY(new Vector3(0, MathF.Sin(Rad(p.WeapB)), MathF.Cos(Rad(p.WeapB))), p.Yaw);
        if (u.Couched)
        {
            // Lance lowered and tucked under the arm, pointing along the horse.
            float rel = MathHelper.WrapAngle(u.ViewHeading - u.ViewFacing);
            Span(hand, RotY(Vector3.Normalize(new Vector3(0, -0.06f, 1)), MathHelper.ToDegrees(rel)), "lance", 0.3f);
            if (cls == UnitClass.Captain) Plane(off + new Vector3(-0.06f, 0.2f, 0.08f), Vector3.Normalize(new Vector3(-0.87f, 0, 0.5f)), "shield");
            return;
        }
        switch (cls)
        {
            case UnitClass.Archer:
                Bill(new(-0.1f, 1.15f, -0.2f), "quiver", false);
                Span(off, Vector3.Up, "bow", 0.5f);
                if (p.Aim && u.Action != UnitAction.Recover) Span(hand, Vector3.UnitZ, "arrow", 0.2f);
                break;
            case UnitClass.Skirmisher:
                Span(hand, dir, "spear", 0.3f);
                break;
            case UnitClass.Cavalry:
                Span(hand, dir, "lance", 0.3f);
                break;
            default:
                Span(hand, dir, "sword", 3.5f / 15);
                if (p.ShieldUp) Plane(off + new Vector3(0.2f, 0.02f, 0.1f), Vector3.UnitZ, "shield");
                else Plane(off + new Vector3(-0.06f, 0.2f, 0.08f), Vector3.Normalize(new Vector3(-0.87f, 0, 0.5f)), "shield");
                break;
        }
    }

    /// A horse (with the rider's legs when `riderLegs`), drawn in the current palette. Returns the gait bob height.
    float Horse(Vector2 pos, float ground, float heading, float anim, float speed, bool riderLegs)
    {
        Frame(pos, heading, ground, 1);
        float gait = Math.Clamp(MathF.Abs(speed) / 11, 0, 1);
        float amp = MathF.Abs(speed) < 0.3f ? 0 : 14 + 30 * gait;
        float bob = MathF.Sin(anim * MathF.Tau) * 0.05f * gait;
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.3f, 0.55f, 0.85f };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1 : 1;
            float a = Rad(amp) * MathF.Sin(anim * MathF.PI + phase[i] * MathF.Tau);
            var hip = new Vector3(side * 0.2f, 1.0f + bob, front ? 0.6f : -0.6f);
            Axial(hip, hip + new Vector3(0, -0.98f * MathF.Cos(a), 0.98f * MathF.Sin(a)), "hleg");
        }
        float y = 1.25f + bob;
        Long(new(0, y, -0.82f), new(0, y, 0.82f), "hbody");
        Bill(new(0, y, 0.6f), "hchest", false, -0.05f);
        Bill(new(0, y, -0.55f), "hrump", false, -0.05f);
        Long(new(0, y + 0.17f, 0.62f), new(0, y + 0.73f, 1.0f), "hneck");
        Long(new(0, y + 0.73f, 0.95f), new(0, y + 0.37f, 1.42f), "hhead");
        Bill(new(0, y + 0.6f, 1.15f), "hface", false, -0.04f);
        Axial(new(0, y + 0.2f, -0.82f), new(0, 0.85f + bob + gait * 0.4f, -1.05f - gait * 0.35f), "htail");
        if (riderLegs)
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * 0.2f, Saddle + 0.68f + bob, 0);
                var foot = new Vector3(side * 0.36f, 1.0f + bob, 0.18f);
                Axial(hip, foot, "leg");
                Bill(foot, "boot", true);
            }
        return bob;
    }

    public void DrawLooseHorse(LooseHorse h, float ground)
    {
        pal = "/" + h.Team;
        tint = Color.White;
        Horse(h.Pos, ground, h.Heading, 0, 0, false);
    }

    public void DrawDeadHorse(Vector2 pos, float heading, int team, float ground)
    {
        pal = "/" + team;
        tint = Color.White;
        Frame(pos, heading, ground, 1);
        Flat(new(0.3f, 0.02f, 0), "blood", false);
        Flat(new(0, 0.05f, 0), "hbody", true);
        Flat(new(0.2f, 0.06f, 1.2f), "hhead", true);
        Flat(new(-0.55f, 0.04f, 0.5f), "hleg", false);
        Flat(new(-0.55f, 0.04f, -0.5f), "hleg", false);
    }

    static Vector3 RotY(Vector3 v, float deg)
    {
        float c = MathF.Cos(Rad(deg)), s = MathF.Sin(Rad(deg));
        return new Vector3(v.X * c + v.Z * s, v.Y, v.Z * c - v.X * s); // +yaw turns forward toward the right hand
    }

    // Arm swung `alpha` degrees forward from hanging, its swing plane turned `yaw` degrees; returns the hand position.
    Vector3 Arm(int side, float alpha, float yaw)
    {
        var shoulder = new Vector3(side * 0.3f, 1.31f, 0);
        var hand = shoulder + RotY(new Vector3(side * 0.04f, -0.55f * MathF.Cos(Rad(alpha)), 0.55f * MathF.Sin(Rad(alpha))), yaw);
        Axial(shoulder, hand, "sleeve");
        Bill(hand, "hand", false, 0.01f);
        return hand;
    }

    // A long sprite through `grip` along `dir`; `gripFrac` = how far up the sprite the hand holds it.
    void Span(Vector3 grip, Vector3 dir, string name, float gripFrac)
    {
        float len = art[name + pal].Height * Art.Px;
        var a = grip - dir * len * gripFrac;
        Axial(a, a + dir * len, name);
    }

    record struct Pose(float Leg, float ArmA, float WeapB, float Yaw, float OffA, bool ShieldUp, bool Aim);
    record struct Key(float Alpha, float Pitch, float Yaw);

    static Key Lerp(Key x, Key y, float t) =>
        new(MathHelper.Lerp(x.Alpha, y.Alpha, t), MathHelper.Lerp(x.Pitch, y.Pitch, t), MathHelper.Lerp(x.Yaw, y.Yaw, t));

    /// Key poses (idle, raised, struck) for a weapon class and swing direction.
    static (Key idle, Key raise, Key strike) Keys(UnitClass cls, AttackDir swing)
    {
        if (cls is UnitClass.Skirmisher or UnitClass.Cavalry)
            return (new(15, 80, 0), new(-25, 5, 0), new(65, -5, 0)); // polearms always thrust
        var idle = new Key(20, 60, 0);
        return swing switch
        {
            AttackDir.Right => (idle, new(95, 15, 75), new(85, 0, -45)),
            AttackDir.Left => (idle, new(115, 20, -75), new(85, 0, 50)),
            AttackDir.Thrust => (idle, new(35, 5, 10), new(95, 0, 0)),
            _ => (idle, new(160, 150, 0), new(75, -35, 0)),
        };
    }

    /// Continuous pose from the unit's action state: key poses blended by action progress.
    static Pose PoseOf(Unit u)
    {
        bool moving = u.Vel.LengthSquared() > 0.1f;
        float leg = moving && !u.Mounted ? MathF.Sin(u.Anim * MathF.PI) * 28 : 0;
        var t = u.Type;
        if (t.Class == UnitClass.Archer)
        {
            bool aim = u.Action == UnitAction.Recover || (u.Ammo > 0 && u.EnemyDist < t.Range && !moving);
            return aim ? new Pose(leg, 90, 0, 0, 90, false, true) : new Pose(leg, 15 - leg * 0.5f, 0, 0, 10 + leg * 0.5f, false, false);
        }
        var (idle, raise, strike) = Keys(t.Class, u.Swing);
        var arm = idle;
        bool shield = false;
        switch (u.Action)
        {
            case UnitAction.Windup:
            {
                float k = 1 - u.ActionTimer / (u.IsPlayer ? (u.Swing == AttackDir.Overhead ? 0.2f : 0.15f) : t.Windup);
                arm = u.IsPlayer ? Lerp(raise, strike, k) : Lerp(idle, raise, k); // player already raised while charging
                break;
            }
            case UnitAction.Recover:
            {
                float k = 1 - u.ActionTimer / (u.IsPlayer ? 0.3f : t.Recover);
                arm = !u.IsPlayer && k < 0.25f ? Lerp(raise, strike, k / 0.25f) : Lerp(strike, idle, u.IsPlayer ? k : (k - 0.25f) / 0.75f);
                break;
            }
            case UnitAction.Block: arm = new(30, 70, 0); shield = t.HasShield; break;
            case UnitAction.Stagger: arm = new(-10, 90, 0); break;
            default: if (u.Charging) arm = Lerp(idle, raise, u.Charge); break;
        }
        return new Pose(leg, arm.Alpha, arm.Pitch, arm.Yaw, shield ? 85 : 15 + leg * 0.4f, shield, false);
    }

    void Corpse(Unit u)
    {
        // Parts laid flat on the ground, body pointing backwards from the feet.
        Flat(new(0, 0.06f, -0.7f), "blood", false);
        Flat(new(0.12f, 0.08f, -0.2f), "leg", false);
        Flat(new(-0.12f, 0.08f, -0.2f), "leg", false);
        Flat(new(0, 0.09f, -0.75f), "torso.f", false);
        Flat(new(0, 0.1f, -1.25f), u.Type.Class == UnitClass.Skirmisher ? "hood.b" : "helm.b", false);
        if (u.Type.HasShield) Flat(new(-0.5f, 0.11f, -0.6f), "shield", true);
    }

    // ---------- quads ----------

    void Quad(Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Rectangle src, bool mirror)
    {
        float aw = art.Atlas.Width, ah = art.Atlas.Height, e = 0.01f; // inset stops neighbouring sprites bleeding
        float u0 = (src.Left + e) / aw, u1 = (src.Right - e) / aw, v0 = (src.Top + e) / ah, v1 = (src.Bottom - e) / ah;
        if (mirror) (u0, u1) = (u1, u0);
        verts.Add(new(tl, tint, new(u0, v0))); verts.Add(new(tr, tint, new(u1, v0))); verts.Add(new(bl, tint, new(u0, v1)));
        verts.Add(new(tr, tint, new(u1, v0))); verts.Add(new(br, tint, new(u1, v1))); verts.Add(new(bl, tint, new(u0, v1)));
    }

    // Camera-facing sprite; `bias` moves it toward (+) or away from (-) the camera so layered parts never z-fight.
    void Bill(Vector3 local, string name, bool viewed, float bias = 0)
    {
        var src = art[viewed ? $"{name}.{view}{pal}" : name + pal];
        var c = W(local);
        if (bias != 0) c += Vector3.Normalize(camPos - c) * bias;
        var hw = camRight * src.Width * Art.Px * scale / 2;
        var hh = camUp * src.Height * Art.Px * scale / 2;
        Quad(c - hw - hh, c + hw - hh, c - hw + hh, c + hw + hh, src, viewed && view == "s" && flip);
    }

    // Sprite stretched from a (bottom of sprite) to b (top), rotated about that axis to face the camera.
    void Axial(Vector3 aLocal, Vector3 bLocal, string name)
    {
        var src = art[name + pal];
        Vector3 a = W(aLocal), b = W(bLocal), axis = b - a;
        var side = Vector3.Cross(axis, camPos - (a + b) / 2);
        side = side.LengthSquared() < 1e-8f ? camRight : Vector3.Normalize(side);
        side *= src.Width * Art.Px * scale / 2;
        Quad(a - side, a + side, b - side, b + side, src, false);
    }

    // Sprite running from a (left column) to b (right column), turned about that axis to face the camera, top edge kept up.
    void Long(Vector3 aLocal, Vector3 bLocal, string name)
    {
        var src = art[name + pal];
        Vector3 a = W(aLocal), b = W(bLocal), axis = b - a;
        var side = Vector3.Cross(axis, camPos - (a + b) / 2);
        side = side.LengthSquared() < 1e-8f ? Vector3.Up : Vector3.Normalize(side);
        if (side.Y < 0) side = -side;
        side *= src.Height * Art.Px * scale / 2;
        Quad(a - side, b - side, a + side, b + side, src, false);
    }

    // Flat sprite in the world: `normal` is the facing of its painted side (unit space).
    void Plane(Vector3 local, Vector3 normal, string name)
    {
        var src = art[name + pal];
        var n = Vector3.Normalize(right * normal.X + Vector3.Up * normal.Y + fwd * normal.Z);
        var r = Vector3.Normalize(Vector3.Cross(n, Vector3.Up)) * src.Width * Art.Px * scale / 2;
        var up = Vector3.Up * src.Height * Art.Px * scale / 2;
        var c = W(local);
        Quad(c - r - up, c + r - up, c - r + up, c + r + up, src, false);
    }

    // Sprite lying on the ground, its top pointing backwards (or sideways when `across`).
    void Flat(Vector3 local, string key, bool across)
    {
        var src = art[key + pal];
        var top = (across ? right : -fwd) * src.Height * Art.Px * scale / 2;
        var r = (across ? fwd : right) * src.Width * Art.Px * scale / 2;
        var c = W(local);
        Quad(c - r - top, c + r - top, c - r + top, c + r + top, src, false);
    }
}
