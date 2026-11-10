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
    const float RearDeg = 32;   // how far a horse rears

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
        if (tilt != 0)
        {
            // The whole body leans `tilt` radians about the feet, toward (tiltX, tiltZ) in unit space: a lean, a flinch, a fall.
            float along = l.X * tiltX + l.Z * tiltZ, c = MathF.Cos(tilt), sn = MathF.Sin(tilt);
            float moved = along * c + l.Y * sn - along;
            l = new Vector3(l.X + moved * tiltX, l.Y * c - along * sn, l.Z + moved * tiltZ);
        }
        return origin + (right * l.X + Vector3.Up * l.Y + fwd * l.Z) * scale;
    }

    /// The battle clock (falls are animated after death; 0 = draw the dead lying at once, as the campaign map does).
    public float Now;
    /// How far the next mounted unit's horse is reared up (0..1; set by the battle screen just before drawing it).
    public float Rear;
    /// The winning team once the battle is over (its men cheer), else -1.
    public int Winner = -1;
    /// Sources of awe this frame (docs/SPECTACLE.md S1): a place, a reach and how strong it is now (0..1). Set by the battle
    /// screen; men within reach lean back and raise an arm against it. Drawing only.
    public readonly List<(Vector2 At, float Radius, float Strength, int Team)> Awe = new(); // Team -1: everyone; else only that side

    /// A man near a working leans back from it and lifts his free arm against the light. Men in a swing or a parry keep
    /// fighting; the rest react fully, each a moment apart (his phase).
    Pose Awed(Unit u, Pose p, float phase)
    {
        float k = 0;
        if (u.IsPlayer) return p; // the source of it does not flinch
        foreach (var (at, r, str, team) in Awe)
        {
            if (team >= 0 && u.Team != team) continue;
            float d = Vector2.Distance(at, u.Pos);
            if (d < r) k = MathF.Max(k, str * (1 - d / r) * 1.6f);
        }
        if (k <= 0 || u.Routing) return p;
        k = Math.Clamp(k - phase * 0.25f, 0, 1) * (u.Action == UnitAction.None ? 1 : 0.3f);
        return p with { LeanZ = p.LeanZ - 9 * k, OffA = MathHelper.Lerp(p.OffA, 125, k * 0.8f), ShieldUp = p.ShieldUp || k > 0.6f };
    }
    float tilt, tiltX, tiltZ;

    /// Lean the body by a vector in degrees (x = to the right, z = forward).
    void Lean(float x, float z)
    {
        float m = MathF.Sqrt(x * x + z * z);
        if (m < 0.01f) { tilt = 0; return; }
        tilt = Rad(m); tiltX = x / m; tiltZ = z / m;
    }

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
        if (u.Fled || u.Raised) return; // a raised corpse walks again as a new man
        tilt = 0;
        look = Variety.Of(u);
        pal = "/" + (u.IsPlayer ? 2 : u.Palette);
        tint = u.HitFlash > 0 ? new Color(255, 110, 110) : u.Routing ? new Color(165, 165, 165) : Color.White;
        float lift = ground;
        if (u.Mounted && !u.Dead && u.Horse!.Barding == "wolf") lift = ground + Warg(u, ground);
        else if (u.Mounted && !u.Dead) lift = ground + (Saddle + Horse(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, u.Coat, u.Horse!.Barding, true)) * Scale;
        float size = (u.Type.Class == UnitClass.Captain ? 1.12f : 1) * u.Type.Size * Scale * Variety.Stature(u);
        float phase = Ease.Hash(u.Index);
        if (u.Dead) { Fallen(u, lift, size, phase); return; }
        if (look.Face == "wolf") { Frame(u.Pos, u.ViewFacing, lift, size); Beast(u, ground); return; }
        var p = PoseOf(u, Now, phase, Winner);
        if (Awe.Count > 0) p = Awed(u, p, phase);
        var at = u.Pos;
        float leanBack = 0;
        if (u.Mounted && Rear > 0)
        {
            // The rider goes up and back with the horse: the saddle swings round the hind hooves.
            float a = Rad(RearDeg * Rear);
            at -= new Vector2(MathF.Cos(u.ViewHeading), MathF.Sin(u.ViewHeading)) * Saddle * MathF.Sin(a) * Scale * 0.6f;
            lift += (MathF.Sin(a) * 0.7f - Saddle * (1 - MathF.Cos(a))) * Scale;
            leanBack = RearDeg * Rear * 0.8f;
        }
        Frame(at, u.ViewFacing, lift + p.Bob * size, size);
        Lean(p.LeanX, p.LeanZ - leanBack);

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
        HeadBill();
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
        if (!p.Armed) return; // a routed man has thrown away his weapon and shield
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
            if (u.Type.HasShield && Detail == 0) Plane(new(0.05f, 1.0f, -0.24f), -Vector3.UnitZ, $"shield.{look.Shield}"); // slung on the back
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
        // Rearing: the horse pivots back onto its hind legs, lifted so the hind hooves stay on the ground.
        if (Rear > 0) { Lean(0, -RearDeg * Rear); origin.Y += MathF.Sin(Rad(RearDeg * Rear)) * 0.7f * Scale; }
        var side = pal;
        var plain = tint;
        bool caparison = barding == "caparison", camel = barding == "camel";
        coatTint = new Color(tint.ToVector3() * (camel ? Style.CamelCoat : Style.Coats[coat % Style.Coats.Length]));
        float gait = Math.Clamp(MathF.Abs(speed) / 11, 0, 1);
        float amp = MathF.Abs(speed) < 0.3f ? 0 : 14 + 30 * gait;
        float bob = MathF.Sin(anim * MathF.Tau) * 0.05f * gait;
        float y = 1.25f + bob;
        // The gait by speed (legs: front left, front right, rear left, rear right): a four-beat walk, a trot on diagonal pairs,
        // then a gallop with the hind legs driving and the body rocking front to back.
        float sp = MathF.Abs(speed), trot = Math.Clamp((sp - 2.5f) / 2, 0, 1), gallop = Math.Clamp((sp - 6.5f) / 2.5f, 0, 1);
        ReadOnlySpan<float> walkP = stackalloc float[] { 0, 0.5f, 0.75f, 0.25f };
        ReadOnlySpan<float> trotP = stackalloc float[] { 0, 0.5f, 0.5f, 0 };
        ReadOnlySpan<float> gallopP = stackalloc float[] { 0.45f, 0.55f, 0, 0.1f };
        float rock = MathF.Sin(anim * MathF.PI * 2 + 1.2f) * 0.09f * gallop; // front end rises as the hind legs push
        float nod = MathF.Sin(anim * MathF.PI * 2 + 0.6f) * (0.05f + 0.07f * gallop) * MathF.Min(1, sp / 2); // the head nods with the stride

        pal = "/n";
        tint = coatTint;
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float ph = MathHelper.Lerp(MathHelper.Lerp(walkP[i], trotP[i], trot), gallopP[i], gallop);
            float a = Rad(amp) * MathF.Sin(anim * MathF.PI + ph * MathF.Tau);
            if (front && Rear > 0) a = MathHelper.Lerp(a, Rad(sd < 0 ? 75 : 55), Rear); // forelegs pawing the air
            var hip = new Vector3(sd * 0.2f, 1.0f + bob + (front ? rock : -rock), front ? 0.6f : -0.6f);
            Axial(hip, hip + new Vector3(0, -0.98f * MathF.Cos(a), 0.98f * MathF.Sin(a)), "hleg");
        }
        Long(new(0, y - rock, -0.82f), new(0, y + rock, 0.82f), "hbody");
        if (camel) Long(new(0, y + 0.4f, -0.5f), new(0, y + 0.4f, 0.2f), "hhump", 0.01f);
        if (Detail == 0) Bill(new(0, y + rock, 0.6f), "hchest", false, -0.05f);
        if (Detail == 0) Bill(new(0, y - rock, -0.55f), "hrump", false, -0.05f);
        float neckOut = gallop * 0.12f; // stretched out at the gallop
        Long(new(0, y + rock + 0.17f, 0.62f), new(0, y + rock + 0.73f + nod - neckOut, 1.0f + neckOut), "hneck");
        Long(new(0, y + rock + 0.73f + nod - neckOut, 0.95f + neckOut), new(0, y + rock + 0.37f + nod * 1.5f - neckOut, 1.42f + neckOut * 1.4f), "hhead");
        if (Detail == 0) Bill(new(0, y + rock + 0.6f + nod * 1.2f - neckOut, 1.15f + neckOut), "hface", false, -0.04f);
        float swish = MathF.Sin(Now * 2.1f + pos.X * 0.7f) * 0.07f * (1 - gait); // a standing horse flicks its tail
        if (Detail == 0) Axial(new(0, y + 0.2f, -0.82f), new(swish, 0.85f + bob + gait * 0.4f, -1.05f - gait * 0.35f), "htail");

        // Gear over the coat.
        pal = side;
        tint = plain;
        Long(new(0, y - rock, -0.82f), new(0, y + rock, 0.82f), caparison ? "hbody.caparison" : "hbody.saddle", 0.03f);
        if (caparison && Detail == 0)
        {
            Bill(new(0, y + rock, 0.6f), "hchest.caparison", false, -0.035f);
            Bill(new(0, y - rock, -0.55f), "hrump.caparison", false, -0.035f);
            Long(new(0, y + rock + 0.17f, 0.62f), new(0, y + rock + 0.73f + nod - neckOut, 1.0f + neckOut), "hneck.caparison", 0.02f);
            Long(new(0, y + rock + 0.73f + nod - neckOut, 0.95f + neckOut), new(0, y + rock + 0.37f + nod * 1.5f - neckOut, 1.42f + neckOut * 1.4f), "hhead.caparison", 0.02f);
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

    /// A wolf on four legs: it trots, and lunges with its head in the strike (docs/CREATURES.md).
    void Beast(Unit u, float ground)
    {
        float lunge = u.Action == UnitAction.Windup ? -0.12f : u.Action == UnitAction.Recover ? 0.28f : 0;
        var grey = tint;
        if (u.Type.Breed == "hound") tint = Color.Lerp(tint, HoundCoat, 0.6f); // a war hound is a brown dog, not a grey wolf
        WolfBody(u.Pos, ground, u.ViewFacing, u.Anim, u.Vel.Length(), u.Type.Size * Scale, lunge, u.Type.Size > 1.1f);
        tint = grey;
    }

    /// The wolf rig at `size` (1 = a wolf; a warg is bigger). Returns its back's height above the ground, in metres.
    float WolfBody(Vector2 pos, float ground, float heading, float anim, float speed, float size, float lunge, bool dark)
    {
        Frame(pos, heading, ground, size);
        var side = pal;
        var plain = tint;
        pal = "/n";
        if (dark) tint = Color.Lerp(tint, new Color(70, 64, 60), 0.45f); // a dire wolf or warg is darker
        float gait = Math.Clamp(speed / 7, 0, 1);
        float amp = speed < 0.3f ? 0 : 18 + 30 * gait;
        float bob = MathF.Sin(anim * MathF.Tau * 1.5f) * 0.03f * gait;
        float y = 0.6f + bob;
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.5f, 0.3f, 0.8f };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float a = Rad(amp) * MathF.Sin(anim * MathF.PI * 1.5f + phase[i] * MathF.Tau);
            var hip = new Vector3(sd * 0.11f, y - 0.05f, front ? 0.36f : -0.38f);
            Axial(hip, hip + new Vector3(0, -0.55f * MathF.Cos(a), 0.55f * MathF.Sin(a)), "wleg");
        }
        Long(new(0, y, -0.5f), new(0, y + 0.04f, 0.5f), "wbody");
        Long(new(0, y + 0.16f, 0.42f + lunge), new(0, y + 0.08f + (lunge > 0 ? -0.08f : 0), 0.92f + lunge * 1.3f), "whead");
        if (Detail == 0) Axial(new(0, y + 0.08f, -0.5f), new(0, y - 0.22f + gait * 0.22f, -0.86f - gait * 0.2f), "wtail");
        pal = side;
        tint = plain;
        return (y + 0.06f) * size;
    }

    /// A ridden warg (a mount with "wolf" barding) and the rider's legs astride it. Returns the rider's seat height.
    float Warg(Unit u, float ground)
    {
        const float size = 1.5f;
        float seat = WolfBody(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, size * Scale, 0, true);
        Frame(u.Pos, u.ViewHeading, ground, u.Type.Size * Scale);
        float hipY = seat / (u.Type.Size * Scale);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            var hip = new Vector3(sd * 0.14f, hipY + 0.05f, 0);
            var foot = new Vector3(sd * 0.3f, hipY - 0.45f, 0.15f);
            Axial(hip, foot, $"leg.{look.Legs}");
            if (Detail == 0) Bill(foot, $"boot.{look.Feet}", true);
        }
        return seat - 0.63f * u.Type.Size * Scale; // the rider's origin sits so his hips are on its back
    }

    static readonly Color HoundCoat = new(150, 100, 58);

    void BeastCorpse(Unit u)
    {
        Flat(new(0, 0.04f, -0.2f), "blood", false);
        pal = "/n";
        if (u.Type.Breed == "hound") tint = Color.Lerp(tint, HoundCoat, 0.6f);
        if (u.Type.Size > 1.1f) tint = Color.Lerp(tint, new Color(70, 64, 60), 0.45f);
        Flat(new(0, 0.06f, 0), "wbody", true);
        Flat(new(0.05f, 0.07f, 0.65f), "whead", true);
        Flat(new(-0.25f, 0.05f, 0.25f), "wleg", false);
        Flat(new(-0.25f, 0.05f, -0.3f), "wleg", false);
    }

    public void DrawLooseHorse(LooseHorse h, float ground)
    {
        tilt = 0;
        pal = "/" + h.Team;
        tint = Color.White;
        if (h.Mount.Barding == "wolf") { WolfBody(h.Pos, ground, h.Heading, 0, 0, 1.5f, 0, true); return; }
        Horse(h.Pos, ground, h.Heading, 0, 0, h.Coat, h.Mount.Barding, false);
    }

    public void DrawDeadHorse(DeadHorse h, float ground)
    {
        tilt = 0;
        // Going down: the knees buckle and it rolls onto its side, then lies flat (below).
        float k = Now > 0 && h.Barding != "wolf" ? (Now - h.At) / 0.7f : 1;
        if (k < 1)
        {
            pal = "/" + h.Team;
            tint = Color.White;
            Lean(90 * Ease.In(MathF.Max(0, k * 1.25f - 0.25f)), 0);
            Horse(h.Pos, ground - Ease.Out(MathF.Min(1, k * 2)) * 0.3f * Scale, h.Heading, 0, 0, h.Coat, h.Barding, false);
            tilt = 0;
            return;
        }
        Frame(h.Pos, h.Heading, ground, h.Barding == "wolf" ? 1.5f : 1);
        pal = "/" + h.Team;
        tint = Color.White;
        Flat(new(0.3f, 0.02f, 0), "blood", false);
        if (h.Barding == "wolf")
        {
            pal = "/n"; tint = new Color(150, 140, 135);
            Flat(new(0, 0.06f, 0), "wbody", true); Flat(new(0.05f, 0.07f, 0.65f), "whead", true); Flat(new(-0.25f, 0.05f, 0.25f), "wleg", false);
            pal = "/" + h.Team; tint = Color.White;
            return;
        }
        pal = "/n";
        tint = new Color(h.Barding == "camel" ? Style.CamelCoat : Style.Coats[h.Coat % Style.Coats.Length]);
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

    /// A body pose: leg swing, weapon arm (alpha, weapon pitch, swing-plane yaw), shield arm, the lean (degrees, x right, z forward),
    /// the bob (metres), and for archers how far the string is drawn.
    internal record struct Pose(float Leg, float ArmA, float WeapB, float Yaw, float OffA, bool ShieldUp, bool Aim,
        float LeanX = 0, float LeanZ = 0, float Bob = 0, float Draw = 0, bool Armed = true);
    internal record struct Key(float Alpha, float Pitch, float Yaw);

    static Key Lerp(Key x, Key y, float t) =>
        new(MathHelper.Lerp(x.Alpha, y.Alpha, t), MathHelper.Lerp(x.Pitch, y.Pitch, t), MathHelper.Lerp(x.Yaw, y.Yaw, t));

    /// Key poses (idle, raised, struck) for a weapon class and swing direction.
    internal static (Key idle, Key raise, Key strike) Keys(UnitClass cls, AttackDir swing)
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

    /// Continuous pose from the unit's action state (docs/VISUALS.md V2). `now` is the battle clock and `phase` a per-man
    /// 0..1 number, so idles and strides never move in step; `winner` is the team that has won (it cheers), or -1.
    /// Windups ease in and hold at the top; the strike snaps through and overshoots (follow-through); the body leans into it.
    internal static Pose PoseOf(Unit u, float now, float phase, int winner = -1)
    {
        float speed = u.Vel.Length();
        bool moving = speed > 0.32f;
        float run = Math.Clamp((speed - 2.2f) / 2.5f, 0, 1); // 0 walking, 1 running
        float stride = MathF.Sin(u.Anim * MathF.PI);
        float leg = moving && !u.Mounted ? stride * (24 + 12 * run) : 0;
        float swing = moving && !u.Mounted ? -stride * (10 + 14 * run) : 0; // arms swing against the legs
        float bob = moving && !u.Mounted ? MathF.Abs(stride) * (0.025f + 0.03f * run) : 0;
        float leanZ = moving ? 3 + 7 * run : 0, leanX = 0;
        var t = u.Type;
        if (!moving && !u.Mounted)
        {
            bob = MathF.Sin(now * 1.7f + phase * 9) * 0.008f; // breathing
            leanX = MathF.Sin(now * 0.31f + phase * 17) * 1.6f; // shifting his weight
        }
        if (t.Size > 1.5f && moving) leanX += stride * 5; // a troll or an ogre lumbers from foot to foot
        float hurt = u.HitFlash > 0 ? Ease.Kick(1 - u.HitFlash / 0.15f) : 0; // a flinch away from the blow
        leanZ -= hurt * 12;
        if (u.Routing)
            return new Pose(leg * 1.2f, 150 + swing * 2, 0, 0, 140 - swing * 2, false, false, leanX, 14, bob, 0, false); // arms up, running for his life
        if (winner >= 0 && u.Team == winner && !moving && u.Action == UnitAction.None && now > 0)
        {
            // The battle is won: the men raise their weapons and cheer, each in his own time.
            float c = MathF.Max(0, MathF.Sin(now * 4.5f + phase * 6.3f));
            return new Pose(0, 140 + c * 30, 60 + c * 40, 0, t.HasShield ? 40 + c * 50 : 20, false, false, 0, -4 * c, c * 0.06f);
        }
        if (t.Class == UnitClass.Archer)
        {
            bool aim = u.Action == UnitAction.Recover || (u.Ammo > 0 && u.EnemyDist < t.Range && !moving);
            if (!aim) return new Pose(leg, 15 + swing - leg * 0.3f, 0, 0, 10 - swing + leg * 0.3f, false, false, leanX, leanZ, bob);
            if (u.Action == UnitAction.Recover) return new Pose(leg, 100, 0, 0, 92, false, true, leanX, leanZ - 2, bob, 0); // the release: the hand flies back
            float draw = Ease.InOut(1 - u.Cooldown / 1.1f); // nock low, then draw as the next shot comes up
            return new Pose(leg, 62 + 28 * draw, 0, 0, 88, false, true, leanX, leanZ - 3 * draw, bob, draw);
        }
        var (idle, raise, strike) = Keys(t.Class, u.Swing);
        idle = idle with { Alpha = idle.Alpha + (phase - 0.5f) * 14 + MathF.Sin(now * 0.4f + phase * 11) * 3 }; // each man rests his weapon his own way
        var arm = idle;
        bool shield = false;
        switch (u.Action)
        {
            case UnitAction.Windup:
            {
                float k = 1 - u.ActionTimer / (u.IsPlayer ? (u.Swing == AttackDir.Overhead ? 0.2f : 0.15f) : t.Windup);
                if (u.IsPlayer) arm = Lerp(raise, strike, Ease.In(k)); // player already raised while charging: the blow accelerates
                else { float up = Ease.InOut(k / 0.85f); arm = Lerp(idle, raise, up); leanZ -= 5 * up; } // held at the top for the last moment
                break;
            }
            case UnitAction.Recover:
            {
                float k = 1 - u.ActionTimer / (u.IsPlayer ? 0.3f : t.Recover);
                const float hit = 0.25f;
                if (!u.IsPlayer && k < hit) { float f = Ease.OutBack(k / hit); arm = Lerp(raise, strike, f); leanZ += 9 * f; }
                else { float f = Ease.InOut(u.IsPlayer ? k : (k - hit) / (1 - hit)); arm = Lerp(strike, idle, f); leanZ += 9 * (1 - f); }
                break;
            }
            case UnitAction.Block: arm = new(30, 70, 0); shield = t.HasShield; leanZ -= 3; break;
            case UnitAction.Stagger:
            {
                arm = new(-10 + 25 * hurt, 90, 0);
                leanZ -= 10 + MathF.Sin(now * 9 + phase) * 3; // stumbling back
                break;
            }
            default:
                if (u.Charging) { arm = Lerp(idle, raise, Ease.Out(u.Charge)); leanZ -= 4 * u.Charge; }
                else if (moving) arm = arm with { Alpha = arm.Alpha + swing * 0.6f };
                break;
        }
        if (look(u) == "bones") arm = arm with { Alpha = arm.Alpha + MathF.Sin(now * 23 + phase * 40) * 3 }; // dry joints rattle
        return new Pose(leg, arm.Alpha, arm.Pitch, arm.Yaw, shield ? 85 : 15 + leg * 0.4f - swing * 0.6f, shield, false, leanX, leanZ, bob);
        static string look(Unit u) => u.Type.Look.Body;
    }

    /// A dead man: he topples away from the killing blow (a stagger back, a crumple at the knees, or a twist to the side),
    /// then lies where he fell, laid out along that line with his weapon thrown beside him.
    void Fallen(Unit u, float ground, float size, float phase)
    {
        // Which way he goes: away from his killer, turned a little by chance, in his own frame.
        var away = u.Pos - u.DiedFrom;
        float yaw = u.ViewFacing;
        Vector2 fwd2 = new(MathF.Cos(yaw), MathF.Sin(yaw)), right2 = new(-fwd2.Y, fwd2.X);
        Vector2 dir = u.DiedFrom == Vector2.Zero || away.LengthSquared() < 1e-4f ? -fwd2 : Vector2.Normalize(away);
        float turn = (phase - 0.5f) * 1.2f;
        dir = new Vector2(dir.X * MathF.Cos(turn) - dir.Y * MathF.Sin(turn), dir.X * MathF.Sin(turn) + dir.Y * MathF.Cos(turn));
        float time = 0.45f + phase * 0.3f;
        float k = Now > 0 ? (Now - u.DiedAt) / time : 1;
        if (k >= 1)
        {
            // Lying down: the corpse's "back" points along the fall.
            Frame(u.Pos, MathF.Atan2(-dir.Y, -dir.X), ground, size);
            if (look.Face == "wolf") BeastCorpse(u); else Corpse(phase);
            return;
        }
        bool crumple = phase < 0.3f;
        float sink = crumple ? Ease.Out(MathF.Min(1, k * 2)) * 0.35f : 0; // the knees go first
        Frame(u.Pos, yaw, ground - sink * size, size);
        float a = 85 * (crumple ? Ease.In(MathF.Max(0, k * 1.4f - 0.4f)) : Ease.In(k));
        Lean(Vector2.Dot(dir, right2) * a, Vector2.Dot(dir, fwd2) * a);
        if (look.Face == "wolf") { Beast(u, ground); return; }
        var p = new Pose(crumple ? 30 * k : 10, 40 + 60 * k, 20, 0, 50 + 40 * k, false, false);
        for (int side = -1; side <= 1; side += 2)
        {
            var hip = new Vector3(side * 0.12f, 0.68f, 0);
            float s = Rad(p.Leg * side);
            Axial(hip, hip + new Vector3(0, -0.6f * MathF.Cos(s), 0.6f * MathF.Sin(s)), $"leg.{look.Legs}");
        }
        Bill(new(0, 1.0f, 0), $"torso.{look.Body}", true);
        if (look.Tabard && Detail == 0) Bill(new(0, 0.98f, 0.01f), "tabard", true, 0.02f);
        HeadBill();
        if (look.Head != "none") Bill(new(0, 1.81f - art[$"helm.{look.Head}.f{pal}"].Height * Art.Px / 2, 0.02f), $"helm.{look.Head}", true, 0.04f);
        var hand = Arm(1, p.ArmA, 0);
        Arm(-1, p.OffA, 0);
        if (k < 0.4f) Span(hand, RotY(new Vector3(0, 0.5f, 0.87f), 20), look.Weapon, SpriteSheet.Grip[look.Weapon]); // still in his hand
        tilt = 0;
    }

    void Corpse(float phase = 0.5f)
    {
        // Parts laid flat on the ground, body pointing backwards from the feet; each man's limbs lie a little differently.
        float j = (phase - 0.5f) * 0.16f, j2 = (Ease.Hash((int)(phase * 9973)) - 0.5f) * 0.16f;
        if (look.Body != "bones") Flat(new(j2, 0.06f, -0.7f), "blood", false); // a skeleton falls in a heap of dry bones
        if (Detail > 0) { Flat(new(0, 0.09f, -0.75f), $"torso.{look.Body}.f", false); return; } // far off: a body and its blood
        Flat(new(0.12f + j, 0.08f, -0.2f), $"leg.{look.Legs}", false);
        Flat(new(-0.12f - j2, 0.08f, -0.2f + j), $"leg.{look.Legs}", false);
        Flat(new(0, 0.09f, -0.75f), $"torso.{look.Body}.f", false);
        if (look.Tabard) Flat(new(0, 0.1f, -0.75f), "tabard.f", false);
        if (look.Head != "none") Flat(new(j, 0.11f, -1.25f), $"helm.{look.Head}.b", false);
        else { var side = pal; if (look.NeutralHead) pal = "/n"; Flat(new(j, 0.11f, -1.25f), look.HeadKey + ".b", false); pal = side; }
        if (look.Shield != "none") Flat(new(-0.5f - j, 0.12f, -0.6f + j2 * 2), $"shield.{look.Shield}", true);
        Flat(new(0.55f + j2 * 2, 0.105f, -0.55f + j * 3), look.Weapon, true); // the weapon thrown down beside him
    }

    // ---------- effects ----------

    /// A solid-colour square facing the camera: dust, blood and sparks (pixel-art particles).
    public void Particle(Vector3 c, float size, Color col)
    {
        var src = art[size > 0.15f ? "puff/0" : "white/0"]; // a large mote is round
        var hw = camRight * size / 2;
        var hh = camUp * size / 2;
        var keep = tint;
        tint = col;
        Quad(c - hw - hh, c + hw - hh, c - hw + hh, c + hw + hh, src, false);
        tint = keep;
    }

    /// A company banner: a pole rising behind the bearer, the flag at its head turned to the camera. The cloth ripples in
    /// three strips (more as the bearer moves: `stream` 0..1); `lean` tips the pole over (radians, a falling banner) toward `fallDir`.
    public void Banner(Vector2 pos, float ground, int palette, float wave, int charge = -1, float stream = 0, float lean = 0, Vector2 fallDir = default)
    {
        var foot = new Vector3(pos.X, ground, pos.Y);
        var side = Vector3.Normalize(new Vector3(camRight.X, 0, camRight.Z));
        var up = Vector3.Up;
        if (lean != 0)
        {
            var d = new Vector3(fallDir.X, 0, fallDir.Y);
            up = Vector3.Normalize(Vector3.Up * MathF.Cos(lean) + d * MathF.Sin(lean));
        }
        var top = foot + up * 3.3f;
        var keep = tint;
        tint = new Color(92, 64, 40);
        var white = art["white/0"];
        Quad(foot - side * 0.03f, foot + side * 0.03f, top - side * 0.03f, top + side * 0.03f, white, false);
        tint = Color.White;
        var flag = art[(charge >= 0 ? $"flag.{charge}/" : "flag/") + palette];
        float w = flag.Width * Art.Px * 1.6f, h = flag.Height * Art.Px * 1.6f;
        var tl = top - up * 0.05f;
        // Three strips of cloth, each fold a little behind the last, rippling out from the pole.
        const int strips = 3;
        float amp = 0.05f + 0.07f * stream;
        Vector3 Edge(int i, out float off)
        {
            float f = i / (float)strips;
            off = MathF.Sin(wave * (1.6f + stream) - f * 3.2f) * amp * f * 2 - 0.12f * f * (1 - stream) * (lean != 0 ? 0 : 1); // hangs slack when still
            return tl + side * w * f + up * off;
        }
        var src = flag;
        float aw = art.Atlas.Width, ah = art.Atlas.Height, e = 0.01f;
        var a = Edge(0, out _);
        for (int i = 1; i <= strips; i++)
        {
            var b = Edge(i, out _);
            float u0 = (src.Left + e + (src.Width - 2 * e) * (i - 1) / strips) / aw, u1 = (src.Left + e + (src.Width - 2 * e) * i / strips) / aw;
            float v0 = (src.Top + e) / ah, v1 = (src.Bottom - e) / ah;
            Vector3 atl = a, atr = b, abl = a - up * h, abr = b - up * h;
            verts.Add(new(atl, tint, new(u0, v0))); verts.Add(new(atr, tint, new(u1, v0))); verts.Add(new(abl, tint, new(u0, v1)));
            verts.Add(new(atr, tint, new(u1, v0))); verts.Add(new(abr, tint, new(u1, v1))); verts.Add(new(abl, tint, new(u0, v1)));
            a = b;
        }
        tint = keep;
    }

    /// A mark lying on the ground: `key` a sprite (blood, splat), `size` metres across, turned by `rot`, tinted.
    public void Decal(Vector2 pos, float ground, float size, float rot, Color col, string key)
    {
        tilt = 0;
        var keepTint = tint; var keepPal = pal;
        tint = col; pal = "/0";
        Frame(pos, rot, ground, size);
        Flat(new(0, 0.035f, 0), key, true);
        tint = keepTint; pal = keepPal;
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

    /// The bare head; a man's own visage is painted in the neutral palette.
    void HeadBill()
    {
        var side = pal;
        if (look.NeutralHead) pal = "/n";
        Bill(new(0, 1.53f, 0.02f), look.HeadKey, true);
        pal = side;
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
