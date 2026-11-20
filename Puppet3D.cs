using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// Full 3D articulated puppet renderer for Ironfield's Real-Time 3D Pixel Art pipeline (`spike/3d-pixel-art`).
/// Zero per-frame heap allocations, cached Look->Mesh tables, stack-allocated per-unit palettes, and 3-tier LOD:
///   Detail == 0 (< 45m): full articulated low-poly 3D skeleton (~400 tris/knight, ~450 tris/horse)
///   Detail == 1 (45-95m): streamlined articulated 3D (no capes/plumes/sub-knees, ~240 tris/knight)
///   Detail == 2 (> 95m / far shadows): single-mesh 3D silhouette (~55 tris/unit)
sealed class Puppet3D
{
    readonly MeshKit3D kit;
    readonly Verts<VertexPNCT> verts;
    readonly Vector3[] palCols = new Vector3[UnitMatTable.Slots];

    public float Now;
    public float Rear;
    public int Winner = -1;
    public int Detail;
    public float Scale = 1f;

    const float RearDeg = 38f, Saddle = 0.87f, Shank = 0.34f, UpperArm = 0.29f, Forearm = 0.28f;

    Vector3 origin, fwd, right;
    float scale, tilt, tiltX, tiltZ, twist;
    float unitIdHash;

    public Puppet3D(MeshKit3D kit, Verts<VertexPNCT> verts)
    {
        this.kit = kit;
        this.verts = verts;
    }

    static float Rad(float deg) => MathHelper.ToRadians(deg);

    void Frame(Vector2 pos, float yaw, float y, float s)
    {
        scale = s;
        origin = new Vector3(pos.X, y, pos.Y);
        fwd = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        right = new Vector3(-fwd.Z, 0, fwd.X);
        tilt = 0; twist = 0;
    }

    void Lean(float x, float z)
    {
        float m = MathF.Sqrt(x * x + z * z);
        if (m < 0.01f) { tilt = 0; return; }
        tilt = Rad(m); tiltX = x / m; tiltZ = z / m;
    }

    Vector3 W(Vector3 local)
    {
        if (tilt != 0)
        {
            float c = MathF.Cos(tilt), s = MathF.Sin(tilt);
            float h = local.X * tiltX + local.Z * tiltZ;
            float d = h * (c - 1) + local.Y * s;
            local = new Vector3(local.X + d * tiltX, local.Y * c - h * s, local.Z + d * tiltZ);
        }
        return origin + (right * local.X + Vector3.Up * local.Y + fwd * local.Z) * scale;
    }

    Vector3 WDir(Vector3 dir)
    {
        if (tilt != 0)
        {
            float c = MathF.Cos(tilt), s = MathF.Sin(tilt);
            float h = dir.X * tiltX + dir.Z * tiltZ;
            float d = h * (c - 1) + dir.Y * s;
            dir = new Vector3(dir.X + d * tiltX, dir.Y * c - h * s, dir.Z + d * tiltZ);
        }
        return Vector3.Normalize(right * dir.X + Vector3.Up * dir.Y + fwd * dir.Z);
    }

    /// Hot-path zero-allocation mesh emission into `verts.A`.
    void EmitLocal(MeshKit3D.Vert[] part, Vector3 pos, Vector3 rx, Vector3 uy, Vector3 fz, float sx = 1f, float sy = 1f, float sz = 1f)
    {
        int len = part.Length;
        if (len == 0) return;

        Vector3 wPos = W(pos);
        Vector3 nR = WDir(rx), nU = WDir(uy), nF = WDir(fz);
        Vector3 wR = nR * (scale * sx), wU = nU * (scale * sy), wF = nF * (scale * sz);

        verts.Reserve(len);
        var dst = verts.A;
        int n = verts.N;
        var cols = palCols;
        var metals = UnitMatTable.Metals;
        float uid = unitIdHash;

        for (int i = 0; i < len; i++)
        {
            ref readonly var v = ref part[i];
            float px = wPos.X + wR.X * v.Pos.X + wU.X * v.Pos.Y + wF.X * v.Pos.Z;
            float py = wPos.Y + wR.Y * v.Pos.X + wU.Y * v.Pos.Y + wF.Y * v.Pos.Z;
            float pz = wPos.Z + wR.Z * v.Pos.X + wU.Z * v.Pos.Y + wF.Z * v.Pos.Z;

            float nx = nR.X * v.Norm.X + nU.X * v.Norm.Y + nF.X * v.Norm.Z;
            float ny = nR.Y * v.Norm.X + nU.Y * v.Norm.Y + nF.Y * v.Norm.Z;
            float nz = nR.Z * v.Norm.X + nU.Z * v.Norm.Y + nF.Z * v.Norm.Z;

            int m = v.MatId;
            ref readonly var mc = ref cols[m];
            int cr = (int)(mc.X * v.R); if (cr > 255) cr = 255;
            int cg = (int)(mc.Y * v.G); if (cg > 255) cg = 255;
            int cb = (int)(mc.Z * v.B); if (cb > 255) cb = 255;

            dst[n++] = new VertexPNCT(
                new Vector3(px, py, pz),
                new Vector3(nx, ny, nz),
                new Color(cr, cg, cb, 255),
                new Vector2(metals[m], uid));
        }
        verts.N = n;
    }

    void EmitBone(MeshKit3D.Vert[] part, Vector3 a, Vector3 b, Vector3 fwdHint, float thick = 1f)
    {
        if (part.Length == 0) return;
        Vector3 axis = b - a;
        float len = axis.Length();
        if (len < 1e-4f) return;
        Vector3 uy = axis / len;
        Vector3 rx = Vector3.Cross(uy, fwdHint);
        if (rx.LengthSquared() < 1e-5f) rx = Vector3.Cross(uy, Vector3.UnitX);
        rx.Normalize();
        Vector3 fz = Vector3.Normalize(Vector3.Cross(rx, uy));
        if (Vector3.Dot(fz, fwdHint) < 0) { rx = -rx; fz = -fz; }
        EmitLocal(part, a, rx, uy, fz, thick, len, thick);
    }

    static Vector3 RotY(Vector3 v, float deg)
    {
        float c = MathF.Cos(Rad(deg)), s = MathF.Sin(Rad(deg));
        return new Vector3(v.X * c + v.Z * s, v.Y, v.Z * c - v.X * s);
    }

    static Vector3 Elbow(Vector3 shoulder, ref Vector3 hand, Vector3 pole)
    {
        var d = hand - shoulder;
        float dist = d.Length();
        float max = (UpperArm + Forearm) * 0.995f;
        if (dist > max) { d *= max / dist; hand = shoulder + d; dist = max; }
        dist = MathF.Max(dist, 0.05f);
        var dir = d / dist;
        var bend = pole - dir * Vector3.Dot(pole, dir);
        bend = bend.LengthSquared() < 1e-4f ? new Vector3(0, -1, 0) : Vector3.Normalize(bend);
        float along = (dist * dist + UpperArm * UpperArm - Forearm * Forearm) / (2 * dist);
        float outD = MathF.Sqrt(MathF.Max(0, UpperArm * UpperArm - along * along));
        return shoulder + dir * along + bend * outD;
    }

    static (float Thigh, float Bend) Knee(int side, Puppet.Pose p)
    {
        if (p.Kneel) return side > 0 ? (82, 82) : (-8, 86);
        float a = side * p.Leg;
        float bend = MathF.Max(0, -a) * p.Knees + p.Crouch * 65;
        return (a + p.Crouch * 35, bend);
    }

    static float Sink(Puppet.Pose p)
    {
        if (p.Kneel) return Shank;
        if (p.Crouch <= 0) return 0;
        var (a, bend) = Knee(1, p with { Leg = 0 });
        return 2 * Shank - Shank * (MathF.Cos(Rad(a)) + MathF.Cos(Rad(a - bend)));
    }

    /// Returns true if drawn in 3D, false if caller should fall back to 2D `Puppet` (beasts / ascending spirits).
    public bool TryDraw(Unit u, float ground)
    {
        if (u.Fled || u.Raised) return true;
        var look = Variety.Of(u);
        if (look.Beast) return false;
        if (u.Dead)
        {
            float time = u.DiedFall switch { Fall.Saddle => 0.75f, Fall.Flung => 0.6f, _ => 0.45f + Ease.Hash(u.Index) * 0.3f };
            float k = Now > 0 ? (Now - u.DiedAt) / time : 1;
            if (k >= 1) return false;
        }

        int palIdx = u.IsPlayer ? 2 : u.Palette;
        Color tint = u.HitFlash > 0 ? new Color(255, 110, 110) : u.Routing ? new Color(165, 165, 165) : Color.White;
        if (u.Poison > 0 && u.HitFlash <= 0) tint = Color.Lerp(tint, new Color(150, 230, 110), 0.4f);
        bool camel = u.Mounted && u.Horse?.Barding == "camel";
        UnitMatTable.Fill(palIdx, look.Visage, u.Coat, camel, tint, palCols);
        unitIdHash = Ease.Hash(u.Index * 37 + 11);

        // Far LOD (Detail == 2): ~55-100 tris total, zero skeletal overhead!
        if (Detail >= 2 && !u.Dead)
        {
            float liftFar = ground;
            if (u.Mounted && u.Horse!.Barding != "wolf")
            {
                float hScale = HorseScale(u.Horse!.Barding);
                Frame(u.Pos, u.ViewHeading, ground, Scale * hScale);
                EmitLocal(kit.LodHorse, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
                liftFar = ground + Saddle * hScale * Scale;
            }
            else if (u.Mounted && u.Horse!.Barding == "wolf") return false;
            float szFar = (u.Type.Class == UnitClass.Captain ? 1.12f : 1f) * u.Type.Size * Scale * Variety.Stature(u);
            Frame(u.Pos, u.ViewFacing, liftFar, szFar);
            EmitLocal(kit.LodFoot, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return true;
        }

        float lift = ground;
        if (u.Mounted && !u.Dead && u.Horse!.Barding != "wolf")
            lift = ground + DrawHorse3D(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, u.Horse!.Barding, Ease.Hash(u.Index));
        else if (u.Mounted && !u.Dead && u.Horse!.Barding == "wolf")
            return false;

        float size = (u.Type.Class == UnitClass.Captain ? 1.12f : 1f) * u.Type.Size * Scale * Variety.Stature(u);
        float phase = Ease.Hash(u.Index);

        var p = Puppet.PoseOf(u, Now, phase, Winner);
        var at = u.Pos;
        float leanBack = 0;
        if (u.Mounted && Rear > 0)
        {
            float a = Rad(RearDeg * Rear);
            at -= new Vector2(MathF.Cos(u.ViewHeading), MathF.Sin(u.ViewHeading)) * Saddle * MathF.Sin(a) * Scale * 0.6f;
            lift += (MathF.Sin(a) * 0.7f - Saddle * (1 - MathF.Cos(a))) * Scale;
            leanBack = RearDeg * Rear * 0.8f;
        }

        if (u.Dead)
        {
            float time = u.DiedFall switch { Fall.Saddle => 0.75f, Fall.Flung => 0.6f, _ => 0.45f + phase * 0.3f };
            float k = Math.Clamp((Now - u.DiedAt) / time, 0, 1);
            Frame(at, u.ViewFacing, lift, size);
            Lean(0, -85 * Ease.In(k));
        }
        else
        {
            Frame(at, u.ViewFacing, lift + (p.Bob - (u.Mounted ? 0 : Sink(p))) * size, size);
            Lean(p.LeanX, p.LeanZ - leanBack);
        }

        var lm = kit.Resolve(look);

        // 1. Legs (Detail 0: articulated thigh+shin; Detail 1: streamlined single bone!)
        if (!u.Mounted)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var (a, bend) = Knee(side, p);
                var hip = new Vector3(side * 0.125f, 0.68f, 0);
                var knee = hip + new Vector3(0, -Shank * MathF.Cos(Rad(a)), Shank * MathF.Sin(Rad(a)));
                var foot = knee + new Vector3(0, -Shank * MathF.Cos(Rad(a - bend)), Shank * MathF.Sin(Rad(a - bend)));
                if (Detail == 0)
                {
                    EmitBone(lm.Thigh, hip, knee, Vector3.UnitZ);
                    EmitBone(lm.Shin, knee, foot, Vector3.UnitZ);
                }
                else
                {
                    EmitBone(lm.Shin, hip, foot, Vector3.UnitZ);
                }
            }
        }
        else if (Detail == 0)
        {
            float spread = 0.27f * HorseScale(u.Horse!.Barding);
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * spread * 0.85f, 0.68f, -0.02f);
                var knee = new Vector3(side * (spread + 0.08f), 0.36f, 0.14f);
                var foot = new Vector3(side * (spread + 0.10f), 0.05f, 0.08f);
                EmitBone(lm.Thigh, hip, knee, Vector3.UnitZ);
                EmitBone(lm.Shin, knee, foot, Vector3.UnitZ);
            }
        }

        // 2. Torso + Cape
        bool ranged = Look.Ranged(look.Weapon);
        twist = ranged || !p.Armed ? 0 : Puppet.TwistOf(p);
        Vector3 tR = RotY(Vector3.UnitX, twist);
        Vector3 tF = RotY(Vector3.UnitZ, twist);
        EmitLocal(lm.Torso, Vector3.Zero, tR, Vector3.UnitY, tF);
        if (lm.HasCape && Detail == 0) EmitLocal(kit.Cape, Vector3.Zero, tR, Vector3.UnitY, tF);

        // 3. Head + Helm + Plume
        Vector3 hR = RotY(Vector3.UnitX, p.Look + twist * 0.4f);
        Vector3 hF = RotY(Vector3.UnitZ, p.Look + twist * 0.4f);
        if (!lm.FullEnclosedHelm && (Detail == 0 || lm.Helm.Length == 0))
            EmitLocal(lm.Head, new Vector3(0, 1.53f, 0.01f), hR, Vector3.UnitY, hF);
        if (lm.Helm.Length > 0)
            EmitLocal(lm.Helm, new Vector3(0, 1.53f, 0.01f), hR, Vector3.UnitY, hF);
        if (lm.HasPlume && Detail == 0)
            EmitLocal(kit.Plume, new Vector3(0, 1.78f, 0.0f), hR, Vector3.UnitY, hF);

        // 4. Arms, Weapon & Shield
        Vector3 Shoulder(int side) => RotY(new Vector3(side * 0.23f, 1.24f, 0), twist);
        Vector3 ArmEnd(int side, float alphaDeg, float yawDeg, float reach)
        {
            var sh = Shoulder(side);
            float a = Rad(alphaDeg);
            return sh + RotY(new Vector3(0, -MathF.Cos(a), MathF.Sin(a)), yawDeg + twist) * (UpperArm + Forearm) * reach;
        }
        void DrawArm(int side, Vector3 hand)
        {
            var sh = Shoulder(side);
            if (Detail == 0)
            {
                var pole = RotY(new Vector3(side * 0.5f, -0.5f, -0.7f), twist);
                var el = Elbow(sh, ref hand, pole);
                EmitBone(lm.UArm, sh, el, Vector3.UnitZ);
                EmitBone(lm.FArm, el, hand, Vector3.UnitZ);
            }
            else
            {
                EmitBone(lm.FArm, sh, hand, Vector3.UnitZ);
            }
        }

        var wHand = ArmEnd(1, p.ArmA, p.Yaw - twist, p.Reach);
        var wDir = RotY(new Vector3(0, MathF.Sin(Rad(p.WeapB)), MathF.Cos(Rad(p.WeapB))), p.Yaw);
        if (wDir.LengthSquared() < 1e-4f) wDir = Vector3.UnitY;
        wDir.Normalize();

        if (ranged && p.Aim)
        {
            var bowHand = new Vector3(-0.08f, 1.26f, 0.48f);
            var drawHand = new Vector3(0.16f, 1.26f, 0.42f - 0.34f * p.Draw);
            DrawArm(-1, bowHand);
            DrawArm(1, drawHand);
            EmitLocal(kit.Bow, bowHand, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return true;
        }

        DrawArm(1, wHand);

        bool shieldArm = p.Armed && u.Type.HasShield && !ranged && !p.TwoHand && !u.Couched;
        Vector3 offHand = p.TwoHand && p.Armed
            ? wHand - wDir * 0.22f
            : shieldArm
                ? (p.ShieldUp ? new Vector3(-0.08f, 1.20f, 0.36f) : new Vector3(-0.24f, 0.98f, 0.24f) + new Vector3(0, p.OffA * 0.002f, 0))
                : ArmEnd(-1, p.OffA, -twist, p.OffReach);
        DrawArm(-1, offHand);

        if (p.Armed && !p.Empty && lm.Weapon.Length > 0)
        {
            Vector3 wRight = Vector3.Cross(wDir, Vector3.UnitY);
            if (wRight.LengthSquared() < 1e-4f) wRight = Vector3.UnitX;
            wRight.Normalize();
            Vector3 wFwd = Vector3.Normalize(Vector3.Cross(wRight, wDir));
            EmitLocal(lm.Weapon, wHand, wRight, wDir, wFwd);
        }

        if (u.Type.HasShield && lm.HasShield)
        {
            Vector3 shPos = p.ShieldUp ? offHand + new Vector3(0.05f, 0.02f, 0.12f) : offHand + new Vector3(-0.07f, 0.02f, 0.06f);
            float shYaw = p.ShieldUp ? 12f : -58f;
            EmitLocal(lm.Shield, shPos, RotY(Vector3.UnitX, shYaw), Vector3.UnitY, RotY(Vector3.UnitZ, shYaw));
        }

        return true;
    }

    static float HorseScale(string barding) => barding switch
    {
        "steppetack" or "camel" => 1.08f,
        "saddle" or "halfbarding" or "furbarding" => 1.18f,
        "scalebarding" or "gildedcaparison" => 1.32f,
        _ => 1.26f,
    };

    float DrawHorse3D(Vector2 pos, float ground, float heading, float anim, float speed, string barding, float idle = -1)
    {
        float hScale = HorseScale(barding);
        Frame(pos, heading, ground, Scale * hScale);
        if (Rear > 0) { Lean(0, -RearDeg * Rear); origin.Y += MathF.Sin(Rad(RearDeg * Rear)) * 0.7f * Scale * hScale; }

        float sp = MathF.Abs(speed);
        float gait = Math.Clamp(sp / 11f, 0, 1);
        float amp = sp < 0.3f ? 0 : 17 + 42 * gait;
        float breathe = idle >= 0 && Now > 0 ? MathF.Sin(Now * 2.4f + idle * 13f) * 0.012f : 0;
        float bob = MathF.Sin(anim * MathF.Tau) * 0.075f * gait + breathe;
        float y = 1.25f + bob;
        float trot = Math.Clamp((sp - 2.5f) / 2f, 0, 1), gallop = Math.Clamp((sp - 6.0f) / 2.5f, 0, 1);
        ReadOnlySpan<float> walkP = stackalloc float[] { 0, 0.5f, 0.75f, 0.25f };
        ReadOnlySpan<float> trotP = stackalloc float[] { 0, 0.5f, 0.5f, 0 };
        ReadOnlySpan<float> gallopP = stackalloc float[] { 0.45f, 0.55f, 0, 0.1f };
        float rock = MathF.Sin(anim * MathF.Tau + 1.2f) * (0.03f * trot + 0.145f * gallop);
        float nod = MathF.Sin(anim * MathF.Tau + 0.5f) * (0.06f + 0.10f * gallop) * MathF.Min(1, sp / 1.8f);
        var (still, stillK) = idle >= 0 && sp < 0.3f && Now > 0 && Rear <= 0 ? Puppet.HorseIdle(idle, Now) : (0, 0f);
        if (still == 1) nod += 0.14f * MathF.Sin(Math.Clamp(stillK * 5, 0, 1) * MathF.PI);
        if (still == 3) rock -= 0.04f * MathF.Sin(stillK * MathF.PI);

        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float ph = MathHelper.Lerp(MathHelper.Lerp(walkP[i], trotP[i], trot), gallopP[i], gallop);
            float a = Rad(amp) * MathF.Sin(anim * MathF.PI + ph * MathF.Tau);
            if (front && Rear > 0) a = MathHelper.Lerp(a, Rad(sd < 0 ? 75 : 55), Rear);
            if (i == 1 && still == 2 && stillK < 0.5f) a = Rad(35) * MathF.Max(0, MathF.Sin(stillK * MathF.PI * 6));
            var hip = new Vector3(sd * 0.23f, 1.05f + bob + (front ? rock : -rock), front ? 0.58f : -0.62f);
            float lift = Rad(amp) * MathF.Max(0, MathF.Cos(anim * MathF.PI + ph * MathF.Tau)) * (front && Rear <= 0 ? 1.45f + 0.25f * gallop : 0.85f + 0.25f * gallop);
            float upA = front ? a : a - Rad(16), lowA = front ? a - lift : a + Rad(12) - lift * 0.6f;
            if (front && Rear > 0) lowA = a - Rad(70) * Rear;
            float upL = front ? 0.49f : 0.50f, lowL = front ? 0.52f : 0.54f;
            var knee = hip + new Vector3(0, -upL * MathF.Cos(upA), upL * MathF.Sin(upA));
            var foot = knee + new Vector3(0, -lowL * MathF.Cos(lowA), lowL * MathF.Sin(lowA));
            if (Detail == 0)
            {
                EmitBone(kit.HorseLegUp, hip, knee, Vector3.UnitZ);
                EmitBone(kit.HorseLegLow, knee, foot, Vector3.UnitZ);
            }
            else
            {
                EmitBone(kit.HorseLegLow, hip, foot, Vector3.UnitZ, 1.25f);
            }
        }

        bool barded = barding is not ("" or "saddle" or "steppetack" or "camel");
        float pitchAngle = MathF.Atan2(2 * rock, 1.76f);
        float cp = MathF.Cos(pitchAngle), sp2 = MathF.Sin(pitchAngle);
        Vector3 bUp = new(0, cp, -sp2), bFwd = new(0, sp2, cp);
        EmitLocal(barded ? kit.HorseBodyBarded : kit.HorseBody, new Vector3(0, y, 0), Vector3.UnitX, bUp, bFwd);

        float neckOut = gallop * (0.14f + 0.08f * MathF.Sin(anim * MathF.Tau + 0.4f));
        var neckBase = new Vector3(0, y + rock + 0.14f, 0.62f);
        var poll = new Vector3(0, y + rock + 0.82f + nod - neckOut, 1.02f + neckOut);
        EmitBone(barded ? kit.HorseNeckBarded : kit.HorseNeck, neckBase, poll, Vector3.UnitZ);

        var muzzle = new Vector3(0, y + rock + 0.36f + nod * 1.5f - neckOut, 1.52f + neckOut * 1.4f);
        Vector3 headFwd = Vector3.Normalize(muzzle - poll);
        Vector3 headUp = Vector3.Normalize(Vector3.Cross(headFwd, Vector3.UnitX));
        if (headUp.Y < 0) headUp = -headUp;
        EmitLocal(barded ? kit.HorseHeadBarded : kit.HorseHead, poll, Vector3.UnitX, headUp, headFwd);

        if (Detail == 0)
        {
            float swish = MathF.Sin(Now * 2.1f + pos.X * 0.7f) * 0.08f * (1 - gait) + MathF.Sin(anim * MathF.Tau) * 0.10f * gait;
            EmitBone(kit.HorseTail, new Vector3(0, y - rock + 0.18f, -0.88f), new Vector3(swish, 0.82f + bob + gait * 0.44f, -1.16f - gait * 0.44f), Vector3.UnitZ);
        }

        return (Saddle + bob) * hScale * Scale;
    }

    public void DrawLooseHorse(LooseHorse h, float ground)
    {
        if (h.Mount.Barding == "wolf") return;
        UnitMatTable.Fill(h.Team, -1, h.Coat, h.Mount.Barding == "camel", Color.White, palCols);
        unitIdHash = Ease.Hash((int)(h.Pos.X * 13 + h.Pos.Y * 29));
        if (Detail >= 2)
        {
            Frame(h.Pos, h.Heading, ground, Scale * HorseScale(h.Mount.Barding));
            EmitLocal(kit.LodHorse, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return;
        }
        DrawHorse3D(h.Pos, ground, h.Heading, 0, 0, h.Mount.Barding, Ease.Hash((int)(h.Pos.X * 7) + (int)(h.Pos.Y * 13) * 31));
    }

    /// Headless CPU vertex-build benchmark for 3D Pixel Art (`--bench-3d [menPerSide]`).
    public static void Bench(int menPerSide)
    {
        var kit = MeshKit3D.TryLoad() ?? throw new FileNotFoundException("Data/meshes/units3d.bin missing");
        var verts = new Verts<VertexPNCT>();
        var p3 = new Puppet3D(kit, verts);

        var rng = new Random(1);
        ArmySpec ScaleArmy(ArmySpec a, int target)
        {
            int cur = Math.Max(1, a.Companies.Sum(c => c.Count));
            float f = (float)target / cur;
            return a with { Companies = a.Companies.Select(c => c with { Count = Math.Max(1, (int)MathF.Round(c.Count * f)) }).ToList() };
        }
        var b = new Battle(1, ScaleArmy(Armies.Random(rng, 4, 0), menPerSide), ScaleArmy(Armies.Random(rng, 4, 1), menPerSide));
        for (int i = 0; i < 60; i++) b.Update(1 / 60f);

        int totalUnits = b.Units.Count(u => !u.Fled);
        Console.WriteLine($"bench-3d: {totalUnits} units ({b.Units.Count(u => u.Mounted)} mounted)");

        void RunCase(string label, Vector2 camPos, float near, float far)
        {
            // Warm up look cache & vertex buffer
            for (int w = 0; w < 3; w++)
            {
                verts.N = 0; p3.Now = b.Time;
                foreach (var u in b.Units)
                {
                    if (u.Fled) continue;
                    float d2 = Vector2.DistanceSquared(u.Pos, camPos);
                    p3.Detail = d2 < near * near ? 0 : d2 < far * far ? 1 : 2;
                    p3.TryDraw(u, 0);
                }
            }
            const int frames = 40;
            long alloc0 = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            int d0 = 0, d1 = 0, d2c = 0;
            for (int f = 0; f < frames; f++)
            {
                verts.N = 0; p3.Now = b.Time + f * 0.016f;
                d0 = d1 = d2c = 0;
                foreach (var u in b.Units)
                {
                    if (u.Fled) continue;
                    float d2 = Vector2.DistanceSquared(u.Pos, camPos);
                    int det = d2 < near * near ? 0 : d2 < far * far ? 1 : 2;
                    if (det == 0) d0++; else if (det == 1) d1++; else d2c++;
                    p3.Detail = det;
                    p3.TryDraw(u, 0);
                }
            }
            sw.Stop();
            long allocPerFrame = (GC.GetAllocatedBytesForCurrentThread() - alloc0) / frames;
            double ms = sw.Elapsed.TotalMilliseconds / frames;
            Console.WriteLine($"  {label,-18}: {ms,6:F2} ms/frame | {verts.N / 3,8:N0} tris | LOD {d0}/{d1}/{d2c} | GC {allocPerFrame} B/frame");
        }

        RunCase("Field View (all)", b.Player.Pos, 26f, 62f);
        RunCase("Command Overview", b.Centroid(0), 22f, 52f);
        RunCase("Shadow Pass", b.Player.Pos, 0f, 15f);
        RunCase("All Detail 0", b.Player.Pos, 999f, 999f);
        RunCase("All Detail 2", b.Player.Pos, 0f, 0f);
    }
}
