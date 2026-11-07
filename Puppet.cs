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
    Vector3 W(Vector3 l)
    {
        if (fall != 0) l = new Vector3(l.X, l.Y * MathF.Cos(fall) + l.Z * MathF.Sin(fall), l.Z * MathF.Cos(fall) - l.Y * MathF.Sin(fall)); // toppling backwards
        return origin + (right * l.X + Vector3.Up * l.Y + fwd * l.Z) * scale;
    }

    /// The battle clock (falls are animated over FallTime after death; 0 = draw the dead lying at once).
    public float Now;
    const float FallTime = 0.5f;
    float fall;

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

    Look look = null!;
    /// Size multiplier (the campaign map draws its banner-bearers larger than life).
    public float Scale = 1;
    Color coatTint;

    /// Level of detail for the next Draw (set per unit by distance): 0 every part; 1 drops small kit (boots, capes,
    /// tabards, plumes, quivers, stuck arrows, horse details); 2 also drops hands and sleeves, for men far off.
    public int Detail;

    /// Draw a unit standing on ground height `ground`, dressed by its troop's Look.
    public void Draw(Unit u, float ground)
    {
        if (u.Fled) return;
        look = u.Type.Look;
        pal = "/" + (u.IsPlayer ? 2 : u.Palette);
        tint = u.HitFlash > 0 ? new Color(255, 110, 110) : u.Routing ? new Color(165, 165, 165) : Color.White;
        float lift = ground;
        if (u.Mounted && !u.Dead) lift = ground + (Saddle + Horse(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, u.Coat, u.Horse!.Barding, true)) * Scale;
        Frame(u.Pos, u.ViewFacing, lift, (u.Type.Class == UnitClass.Captain ? 1.12f : 1) * Scale);

        fall = 0;
        if (u.Dead)
        {
            float k = Now > 0 ? (Now - u.DiedAt) / FallTime : 1;
            if (k >= 1) { Corpse(); return; }
            fall = k * k * MathHelper.ToRadians(80);
        }
        var p = PoseOf(u);
        var cls = u.Type.Class;

        if (!u.Mounted)
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * 0.12f, 0.68f, 0);
                float s = Rad(p.Leg * side);
                var foot = hip + new Vector3(0, -0.6f * MathF.Cos(s), 0.6f * MathF.Sin(s));
                Axial(hip, foot, $"leg.{look.Legs}");
                if (Detail == 0) Bill(foot + new Vector3(0, 0.02f, 0.03f), $"boot.{look.Feet}", true);
            }
        if (look.Cape && Detail == 0) Bill(new(0, 0.92f, -0.17f), "cape", false);
        Bill(new(0, 1.0f, 0), $"torso.{look.Body}", true);
        if (look.Tabard && Detail == 0) Bill(new(0, 0.98f, 0.01f), "tabard", true, 0.02f);
        Bill(new(0, 1.53f, 0.02f), "head", true);
        if (look.Head != "none")
        {
            // Every helmet's crown sits just above the head, whatever its height.
            float h = art[$"helm.{look.Head}.f{pal}"].Height * Art.Px;
            Bill(new(0, 1.81f - h / 2, 0.02f), $"helm.{look.Head}", true, 0.04f);
        }
        if (look.Plume && Detail == 0) Bill(new(0, 1.81f + art["plume" + pal].Height * Art.Px / 2 - 0.06f, -0.03f), "plume", false, 0.05f);

        var hand = Arm(1, p.ArmA, p.Yaw);
        var off = Arm(-1, p.OffA, 0);
        var dir = RotY(new Vector3(0, MathF.Sin(Rad(p.WeapB)), MathF.Cos(Rad(p.WeapB))), p.Yaw);
        void ShieldOnArm()
        {
            if (!u.Type.HasShield) return;
            var key = $"shield.{look.Shield}";
            var at = p.ShieldUp ? off + new Vector3(0.2f, 0.02f, 0.1f) : off + new Vector3(-0.06f, 0.2f, 0.08f);
            var n = p.ShieldUp ? Vector3.UnitZ : Vector3.Normalize(new Vector3(-0.87f, 0, 0.5f));
            Plane(at, n, key);
            for (int i = 0; i < (Detail == 0 ? u.StuckArrows : 0); i++) // shafts caught in the shield, sticking out of its face
            {
                var j = at + new Vector3((i % 2 - 0.5f) * 0.16f, (i / 2 - 0.5f) * 0.18f, 0);
                Axial(j, j + Vector3.Normalize(n + new Vector3(0.2f * (i % 2 * 2 - 1), 0.25f, 0)) * 0.4f, "arrow");
            }
        }
        if (u.Couched)
        {
            // Lance lowered and tucked under the arm, pointing along the horse.
            float rel = MathHelper.WrapAngle(u.ViewHeading - u.ViewFacing);
            Span(hand, RotY(Vector3.Normalize(new Vector3(0, -0.06f, 1)), MathHelper.ToDegrees(rel)), "lance", SpriteSheet.Grip["lance"]);
            ShieldOnArm();
            return;
        }
        if (Look.Ranged(look.Weapon))
        {
            if (Detail == 0) Bill(new(-0.1f, 1.15f, -0.2f), "quiver", false);
            Span(off, Vector3.Up, look.Weapon, SpriteSheet.Grip[look.Weapon]);
            if (p.Aim && u.Action != UnitAction.Recover) Span(hand, Vector3.UnitZ, "arrow", 0.2f);
            return;
        }
        Span(hand, dir, look.Weapon, SpriteSheet.Grip[look.Weapon]);
        ShieldOnArm();
    }

    /// A horse (with the rider's legs when `riderLegs`): coat parts tinted by `coat`, gear in the side's colours.
    /// Returns the gait bob height.
    float Horse(Vector2 pos, float ground, float heading, float anim, float speed, int coat, string barding, bool riderLegs)
    {
        Frame(pos, heading, ground, Scale);
        var side = pal;
        var plain = tint;
        coatTint = new Color(tint.ToVector3() * Style.Coats[coat % Style.Coats.Length]);
        bool caparison = barding == "caparison";
        float gait = Math.Clamp(MathF.Abs(speed) / 11, 0, 1);
        float amp = MathF.Abs(speed) < 0.3f ? 0 : 14 + 30 * gait;
        float bob = MathF.Sin(anim * MathF.Tau) * 0.05f * gait;
        float y = 1.25f + bob;

        pal = "/n";
        tint = coatTint;
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.3f, 0.55f, 0.85f };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float a = Rad(amp) * MathF.Sin(anim * MathF.PI + phase[i] * MathF.Tau);
            var hip = new Vector3(sd * 0.2f, 1.0f + bob, front ? 0.6f : -0.6f);
            Axial(hip, hip + new Vector3(0, -0.98f * MathF.Cos(a), 0.98f * MathF.Sin(a)), "hleg");
        }
        Long(new(0, y, -0.82f), new(0, y, 0.82f), "hbody");
        if (Detail == 0) Bill(new(0, y, 0.6f), "hchest", false, -0.05f);
        if (Detail == 0) Bill(new(0, y, -0.55f), "hrump", false, -0.05f);
        Long(new(0, y + 0.17f, 0.62f), new(0, y + 0.73f, 1.0f), "hneck");
        Long(new(0, y + 0.73f, 0.95f), new(0, y + 0.37f, 1.42f), "hhead");
        if (Detail == 0) Bill(new(0, y + 0.6f, 1.15f), "hface", false, -0.04f);
        if (Detail == 0) Axial(new(0, y + 0.2f, -0.82f), new(0, 0.85f + bob + gait * 0.4f, -1.05f - gait * 0.35f), "htail");

        // Gear over the coat.
        pal = side;
        tint = plain;
        Long(new(0, y, -0.82f), new(0, y, 0.82f), caparison ? "hbody.caparison" : "hbody.saddle", 0.03f);
        if (caparison && Detail == 0)
        {
            Bill(new(0, y, 0.6f), "hchest.caparison", false, -0.035f);
            Bill(new(0, y, -0.55f), "hrump.caparison", false, -0.035f);
            Long(new(0, y + 0.17f, 0.62f), new(0, y + 0.73f, 1.0f), "hneck.caparison", 0.02f);
            Long(new(0, y + 0.73f, 0.95f), new(0, y + 0.37f, 1.42f), "hhead.caparison", 0.02f);
        }
        if (riderLegs)
            for (int sd = -1; sd <= 1; sd += 2)
            {
                var hip = new Vector3(sd * 0.2f, Saddle + 0.68f + bob, 0);
                var foot = new Vector3(sd * 0.36f, 1.0f + bob, 0.18f);
                Axial(hip, foot, $"leg.{look.Legs}");
                if (Detail == 0) Bill(foot, $"boot.{look.Feet}", true);
            }
        return bob;
    }

    public void DrawLooseHorse(LooseHorse h, float ground)
    {
        pal = "/" + h.Team;
        tint = Color.White;
        Horse(h.Pos, ground, h.Heading, 0, 0, h.Coat, h.Mount.Barding, false);
    }

    public void DrawDeadHorse(DeadHorse h, float ground)
    {
        Frame(h.Pos, h.Heading, ground, 1);
        pal = "/" + h.Team;
        tint = Color.White;
        Flat(new(0.3f, 0.02f, 0), "blood", false);
        pal = "/n";
        tint = new Color(Style.Coats[h.Coat % Style.Coats.Length]);
        Flat(new(0, 0.05f, 0), "hbody", true);
        Flat(new(0.2f, 0.06f, 1.2f), "hhead", true);
        Flat(new(-0.55f, 0.04f, 0.5f), "hleg", false);
        Flat(new(-0.55f, 0.04f, -0.5f), "hleg", false);
        pal = "/" + h.Team;
        tint = Color.White;
        Flat(new(0, 0.07f, 0), h.Barding == "caparison" ? "hbody.caparison" : "hbody.saddle", true);
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
        if (Detail >= 2) return hand; // far off: the weapon alone shows the arm
        Axial(shoulder, hand, $"sleeve.{look.Body}");
        Bill(hand, $"hand.{look.Hands}", false, 0.01f);
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

    void Corpse()
    {
        // Parts laid flat on the ground, body pointing backwards from the feet.
        Flat(new(0, 0.06f, -0.7f), "blood", false);
        if (Detail > 0) { Flat(new(0, 0.09f, -0.75f), $"torso.{look.Body}.f", false); return; } // far off: a body and its blood
        Flat(new(0.12f, 0.08f, -0.2f), $"leg.{look.Legs}", false);
        Flat(new(-0.12f, 0.08f, -0.2f), $"leg.{look.Legs}", false);
        Flat(new(0, 0.09f, -0.75f), $"torso.{look.Body}.f", false);
        if (look.Tabard) Flat(new(0, 0.1f, -0.75f), "tabard.f", false);
        Flat(new(0, 0.11f, -1.25f), look.Head == "none" ? "head.b" : $"helm.{look.Head}.b", false);
        if (look.Shield != "none") Flat(new(-0.5f, 0.12f, -0.6f), $"shield.{look.Shield}", true);
    }

    // ---------- effects ----------

    /// A solid-colour square facing the camera: dust, blood and sparks (pixel-art particles).
    public void Particle(Vector3 c, float size, Color col)
    {
        var src = art["white/0"];
        var hw = camRight * size / 2;
        var hh = camUp * size / 2;
        var keep = tint;
        tint = col;
        Quad(c - hw - hh, c + hw - hh, c - hw + hh, c + hw + hh, src, false);
        tint = keep;
    }

    /// A company banner: a pole rising behind the bearer, the flag at its head turned to the camera.
    public void Banner(Vector2 pos, float ground, int palette, float wave, int charge = -1)
    {
        var foot = new Vector3(pos.X, ground, pos.Y);
        var top = foot + Vector3.Up * 3.3f;
        var side = Vector3.Normalize(new Vector3(camRight.X, 0, camRight.Z));
        var keep = tint;
        tint = new Color(92, 64, 40);
        var white = art["white/0"];
        Quad(foot - side * 0.03f, foot + side * 0.03f, top - side * 0.03f, top + side * 0.03f, white, false);
        tint = Color.White;
        var flag = art[(charge >= 0 ? $"flag.{charge}/" : "flag/") + palette];
        float w = flag.Width * Art.Px * 1.6f, h = flag.Height * Art.Px * 1.6f, sag = MathF.Sin(wave) * 0.05f;
        var tl = top - Vector3.Up * 0.05f;
        Quad(tl - Vector3.Up * h, tl + side * w - Vector3.Up * (h + sag), tl, tl + side * w + Vector3.Up * sag, flag, false);
        tint = keep;
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
    // `bias` pulls it toward the camera (gear over the coat).
    void Long(Vector3 aLocal, Vector3 bLocal, string name, float bias = 0)
    {
        var src = art[name + pal];
        Vector3 a = W(aLocal), b = W(bLocal), axis = b - a;
        if (bias != 0) { var toward = Vector3.Normalize(camPos - (a + b) / 2) * bias; a += toward; b += toward; }
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
