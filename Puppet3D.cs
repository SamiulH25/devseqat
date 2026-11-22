using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// Full 3D articulated puppet renderer for Ironfield's Real-Time 3D Pixel Art pipeline (`spike/3d-pixel-art`).
/// Zero per-frame heap allocations, cached Look->Mesh tables, stack-allocated per-unit palettes, procedural surface
/// micro-detail, per-realm 3D heraldry, full combat/ranged/siege poses, 3D banners & missiles, and 3-tier LOD:
///   Detail == 0 (< 26m): full articulated low-poly 3D skeleton + heraldry + quivers + stuck arrows + swing glints
///   Detail == 1 (26-62m): streamlined articulated 3D (no capes/plumes/sub-knees/props)
///   Detail == 2 (> 62m / far shadows): single-mesh 3D silhouette (~50 tris/unit)
sealed partial class Puppet3D
{
    readonly MeshKit3D kit;
    readonly Verts<VertexPNCT> verts;
    readonly Vector3[] palCols = new Vector3[UnitMatTable.Slots];

    public float Now;
    public float Rear;
    public int Winner = -1;
    public int Detail;
    public float Scale = 1f;
    /// Bypass per-soldier Variety randomization (used by showcase sheets to inspect exact Look meshes).
    public bool ExactLook;
    /// Optional 2D Puppet reference for sharing soft-particle swing glints and active Awe sources.
    public Puppet? FxPuppet;
    /// Shadow-pass fast path: skips normal transforms, palette lookups, patterns, and slash glints.
    public bool ShadowOnly;
    /// Defer soft-particle weapon glints into PendingHaze so TryDraw is 100% thread-safe across Parallel.For workers.
    public bool DeferHaze;
    public readonly List<(Vector3 Pos, float Size, Color Col, float Alpha, float Seed)> PendingHaze = new();

    const float RearDeg = 38f, Saddle = 0.87f, Shank = 0.34f, UpperArm = 0.29f, Forearm = 0.28f;

    Vector3 origin, fwd, right;
    Vector3 camRight = Vector3.UnitX;
    float scale, tilt, tiltX, tiltZ, tiltCos = 1f, tiltSin, twist;
    float unitIdHash;

    static readonly byte[] PatAlpha = BuildPatAlpha();
    static byte[] BuildPatAlpha()
    {
        var table = new byte[5 * UnitMatTable.Slots];
        var metalQ = UnitMatTable.MetalQ;
        for (int p = 0; p < 5; p++)
            for (int m = 0; m < UnitMatTable.Slots; m++)
            {
                byte pat = 0;
                if (p == MeshKit3D.PatMail && m == 5) pat = MeshKit3D.PatMail;
                else if (p == MeshKit3D.PatScale && (m == 4 || m == 5 || m == 6 || m == 7)) pat = MeshKit3D.PatScale;
                else if (p == MeshKit3D.PatQuilt && (m == 1 || m == 2)) pat = MeshKit3D.PatQuilt;
                else if (m == 4 || m == 8) pat = MeshKit3D.PatBrushed;
                table[p * UnitMatTable.Slots + m] = (byte)((pat << 4) | metalQ[m]);
            }
        return table;
    }

    public Puppet3D(MeshKit3D kit, Verts<VertexPNCT> verts)
    {
        this.kit = kit;
        this.verts = verts;
    }

    public void BeginCamera(Matrix view)
    {
        var inv = Matrix.Invert(view);
        var r = new Vector3(inv.Right.X, 0, inv.Right.Z);
        camRight = r.LengthSquared() > 1e-4f ? Vector3.Normalize(r) : Vector3.UnitX;
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

    void WorldFrame(Vector3 pos, float s = 1f)
    {
        scale = s;
        origin = pos;
        right = Vector3.UnitX;
        fwd = Vector3.UnitZ;
        tilt = 0; twist = 0;
    }

    void Lean(float x, float z)
    {
        float m2 = x * x + z * z;
        if (m2 < 1e-4f) { tilt = 0; tiltCos = 1f; tiltSin = 0f; return; }
        float m = MathF.Sqrt(m2);
        tilt = Rad(m); tiltX = x / m; tiltZ = z / m;
        (tiltSin, tiltCos) = MathF.SinCos(tilt);
    }

    Vector3 W(Vector3 local)
    {
        if (tilt != 0)
        {
            float h = local.X * tiltX + local.Z * tiltZ;
            float d = h * (tiltCos - 1f) + local.Y * tiltSin;
            local = new Vector3(local.X + d * tiltX, local.Y * tiltCos - h * tiltSin, local.Z + d * tiltZ);
        }
        return origin + (right * local.X + Vector3.Up * local.Y + fwd * local.Z) * scale;
    }

    Vector3 WDir(Vector3 dir)
    {
        if (tilt != 0)
        {
            float h = dir.X * tiltX + dir.Z * tiltZ;
            float d = h * (tiltCos - 1f) + dir.Y * tiltSin;
            dir = new Vector3(dir.X + d * tiltX, dir.Y * tiltCos - h * tiltSin, dir.Z + d * tiltZ);
        }
        return new Vector3(
            right.X * dir.X + fwd.X * dir.Z,
            dir.Y,
            right.Z * dir.X + fwd.Z * dir.Z);
    }

    /// Hot-path zero-allocation mesh emission into `verts.A` with packed (patternId, metallic) in Color.A and meter-scale UVs.
    void EmitLocal(MeshKit3D.Vert[] part, Vector3 pos, Vector3 rx, Vector3 uy, Vector3 fz, float sx = 1f, float sy = 1f, float sz = 1f, byte partPat = 0)
    {
        int len = part.Length;
        if (len == 0) return;

        Vector3 wPos = W(pos);
        Vector3 nR = WDir(rx), nU = WDir(uy), nF = WDir(fz);
        float sRx = scale * sx, sUy = scale * sy, sFz = scale * sz;
        float wRx = nR.X * sRx, wRy = nR.Y * sRx, wRz = nR.Z * sRx;
        float wUx = nU.X * sUy, wUy = nU.Y * sUy, wUz = nU.Z * sUy;
        float wFx = nF.X * sFz, wFy = nF.Y * sFz, wFz = nF.Z * sFz;
        float oX = wPos.X, oY = wPos.Y, oZ = wPos.Z;

        verts.Reserve(len);
        var dst = verts.A;
        int n = verts.N;
        if (ShadowOnly)
        {
            for (int i = 0; i < len; i++)
            {
                ref readonly var v = ref part[i];
                dst[n++] = new VertexPNCT(
                    new Vector3(
                        oX + wRx * v.Pos.X + wUx * v.Pos.Y + wFx * v.Pos.Z,
                        oY + wRy * v.Pos.X + wUy * v.Pos.Y + wFy * v.Pos.Z,
                        oZ + wRz * v.Pos.X + wUz * v.Pos.Y + wFz * v.Pos.Z),
                    Vector3.Up, Color.White, Vector2.Zero);
            }
            verts.N = n;
            return;
        }

        float nRx = nR.X, nRy = nR.Y, nRz = nR.Z;
        float nUx = nU.X, nUy = nU.Y, nUz = nU.Z;
        float nFx = nF.X, nFy = nF.Y, nFz = nF.Z;
        var cols = palCols;
        var patAlpha = PatAlpha;
        int patBase = partPat * UnitMatTable.Slots;

        for (int i = 0; i < len; i++)
        {
            ref readonly var v = ref part[i];
            float vx = v.Pos.X, vy = v.Pos.Y, vz = v.Pos.Z;
            float px = oX + wRx * vx + wUx * vy + wFx * vz;
            float py = oY + wRy * vx + wUy * vy + wFy * vz;
            float pz = oZ + wRz * vx + wUz * vy + wFz * vz;

            float vnx = v.Norm.X, vny = v.Norm.Y, vnz = v.Norm.Z;
            float nx = nRx * vnx + nUx * vny + nFx * vnz;
            float ny = nRy * vnx + nUy * vny + nFy * vnz;
            float nz = nRz * vnx + nUz * vny + nFz * vnz;

            int m = v.MatId;
            ref readonly var mc = ref cols[m];
            int cr = (int)(mc.X * v.R); if ((uint)cr > 255u) cr = cr < 0 ? 0 : 255;
            int cg = (int)(mc.Y * v.G); if ((uint)cg > 255u) cg = cg < 0 ? 0 : 255;
            int cb = (int)(mc.Z * v.B); if ((uint)cb > 255u) cb = cb < 0 ? 0 : 255;
            uint packed = (uint)(cr | (cg << 8) | (cb << 16) | (patAlpha[patBase + m] << 24));

            dst[n++] = new VertexPNCT(
                new Vector3(px, py, pz),
                new Vector3(nx, ny, nz),
                new Color { PackedValue = packed },
                new Vector2(v.U, v.V));
        }
        verts.N = n;
    }

    /// Emit a bone mesh oriented from local `a` to `b` (part's +Y axis aligns with b - a, +Z aligns toward `fwdHint`).
    void EmitBone(MeshKit3D.Vert[] part, Vector3 a, Vector3 b, Vector3 fwdHint, float radialScale = 1f, byte partPat = 0)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 1e-4f) return;
        var uy = d / len;
        var rx = Vector3.Cross(uy, fwdHint);
        if (rx.LengthSquared() < 1e-4f) rx = Vector3.Cross(uy, Vector3.UnitX);
        rx.Normalize();
        var fz = Vector3.Normalize(Vector3.Cross(rx, uy));
        // Keep +Z pointing toward fwdHint even on downward bones (e.g. legs, so boot toes always point forward!).
        if (Vector3.Dot(fz, fwdHint) < 0) { rx = -rx; fz = -fz; }
        EmitLocal(part, a, rx, uy, fz, radialScale, len, radialScale, partPat);
    }

    /// Emit a directional prop/weapon mesh whose local +Y points along `dir` with right-handed basis.
    void EmitDir(MeshKit3D.Vert[] part, Vector3 pos, Vector3 dir, float sx = 1f, float sy = 1f, float sz = 1f, byte partPat = 0)
    {
        if (dir.LengthSquared() < 1e-5f) dir = Vector3.UnitY;
        else dir = Vector3.Normalize(dir);
        var rx = Vector3.Cross(dir, Vector3.UnitY);
        if (rx.LengthSquared() < 1e-4f) rx = Vector3.UnitX;
        else rx.Normalize();
        var fz = Vector3.Normalize(Vector3.Cross(rx, dir));
        EmitLocal(part, pos, rx, dir, fz, sx, sy, sz, partPat);
    }

    /// Emit a weapon mesh with its cutting edge (local +Z for axes/scimitars/polearms, local +X for straight swords) aligned to `edgeHint`.
    void EmitWeapon(MeshKit3D.Vert[] part, Vector3 pos, Vector3 dir, Vector3 edgeHint, bool edgeAlongZ, float sx = 1f, float sy = 1f, float sz = 1f)
    {
        if (dir.LengthSquared() < 1e-5f) dir = Vector3.UnitY;
        else dir = Vector3.Normalize(dir);
        var e = edgeHint - dir * Vector3.Dot(edgeHint, dir);
        if (e.LengthSquared() < 1e-4f)
        {
            e = Vector3.Cross(dir, Vector3.UnitY);
            if (e.LengthSquared() < 1e-4f) e = Vector3.UnitX;
        }
        e.Normalize();
        Vector3 rx, fz;
        if (edgeAlongZ)
        {
            fz = e;
            rx = Vector3.Normalize(Vector3.Cross(dir, fz));
        }
        else
        {
            rx = e;
            fz = Vector3.Normalize(Vector3.Cross(rx, dir));
        }
        EmitLocal(part, pos, rx, dir, fz, sx, sy, sz);
    }

    static Vector3 RotX(Vector3 v, float deg)
    {
        float r = Rad(deg), c = MathF.Cos(r), s = MathF.Sin(r);
        return new Vector3(v.X, v.Y * c - v.Z * s, v.Y * s + v.Z * c);
    }

    static Vector3 RotZ(Vector3 v, float deg)
    {
        float r = Rad(deg), c = MathF.Cos(r), s = MathF.Sin(r);
        return new Vector3(v.X * c - v.Y * s, v.X * s + v.Y * c, v.Z);
    }

    static Vector3 RotY(Vector3 v, float deg)
    {
        float r = Rad(deg), c = MathF.Cos(r), s = MathF.Sin(r);
        return new Vector3(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c);
    }

    static Vector3 Elbow(Vector3 shoulder, ref Vector3 hand, Vector3 pole)
    {
        var d = hand - shoulder;
        float len = d.Length(), most = (UpperArm + Forearm) * 0.999f;
        if (len < 1e-4f) return shoulder + Vector3.Normalize(pole) * UpperArm;
        var axis = d / len;
        if (len > most) { len = most; hand = shoulder + axis * len; }
        len = MathF.Max(len, MathF.Abs(UpperArm - Forearm) + 0.01f);
        float along = (UpperArm * UpperArm - Forearm * Forearm + len * len) / (2 * len);
        float up = MathF.Sqrt(MathF.Max(0, UpperArm * UpperArm - along * along));
        var bend = pole - axis * Vector3.Dot(pole, axis);
        bend = bend.LengthSquared() < 1e-6f ? Vector3.Cross(axis, Vector3.UnitY) : Vector3.Normalize(bend);
        return shoulder + axis * along + bend * up;
    }

    static (float Thigh, float Bend) Knee(int side, Puppet.Pose p)
    {
        if (p.Kneel) return side > 0 ? (85, 85) : (-5, 92);
        float a = p.Leg * side + p.Crouch * 50;
        return (a, p.Crouch * 95 + MathF.Max(0, -p.Leg * side) * p.Knees);
    }

    static float Sink(Puppet.Pose p)
    {
        if (p.Kneel) return Shank;
        if (p.Crouch <= 0) return 0;
        var (a, bend) = Knee(1, p with { Leg = 0 });
        return 2 * Shank - Shank * (MathF.Cos(Rad(a)) + MathF.Cos(Rad(a - bend)));
    }

    /// Draw a unit (human, non-human, mounted, or beast) in full 3D. Returns false only for ascended dead spirit wisps.
    public bool TryDraw(Unit u, float ground, Look? preLook = null)
    {
        if (u.Fled || u.Raised) return true;
        var look = preLook ?? (ExactLook ? u.Type.Look : Variety.Of(u));
        float phase = Ease.Hash(u.Index);
        if (u.Dead)
        {
            float time = (look.Beast ? Fall.Plain : u.DiedFall) switch { Fall.Saddle => 0.75f, Fall.Flung => 0.6f, _ => 0.45f + phase * 0.3f };
            float k = Now > 0 ? (Now - u.DiedAt) / time : 1f;
            if (k >= 1f) return false; // let rising spirit wisps draw via Puppet
        }

        int palIdx = u.IsPlayer ? 2 : u.Palette;
        Color tint = u.HitFlash > 0 ? new Color(255, 110, 110) : u.Routing ? new Color(165, 165, 165) : Color.White;
        if (u.Poison > 0 && u.HitFlash <= 0) tint = Color.Lerp(tint, new Color(150, 230, 110), 0.4f);
        unitIdHash = Ease.Hash(u.Index * 37 + 11);

        if (look.Beast) return DrawBeast3D(u, ground, tint, look);

        bool camel = u.Mounted && u.Horse?.Barding == "camel";
        if (!ShadowOnly) UnitMatTable.Fill(palIdx, look.Visage, u.Coat, camel, tint, palCols, look.Face);

        float size = (u.Type.Class == UnitClass.Captain ? 1.12f : 1f) * u.Type.Size * Scale * Variety.Stature(u);

        // Far LOD (Detail == 2): ~50-100 tris total, zero skeletal overhead and no Resolve needed!
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
            else if (u.Mounted && u.Horse!.Barding == "wolf")
            {
                liftFar = ground + DrawWarg3D(u, ground, tint);
                if (!ShadowOnly) UnitMatTable.Fill(palIdx, look.Visage, u.Coat, false, tint, palCols, look.Face);
            }
            Frame(u.Pos, u.ViewFacing, liftFar, size);
            EmitLocal(kit.LodFoot, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return true;
        }

        var lm = kit.Resolve(look);

        if (u.Dead)
        {
            DrawFallen3D(u, ground, size, phase, palIdx, look, lm);
            return true;
        }

        float mountTurn = u.Mounted ? MathHelper.WrapAngle(u.Heading - u.ViewHeading) : 0f;
        float lift = ground;
        if (u.Mounted && u.Horse!.Barding == "wolf")
        {
            lift = ground + DrawWarg3D(u, ground, tint, mountTurn);
            UnitMatTable.Fill(palIdx, look.Visage, u.Coat, false, tint, palCols, look.Face);
        }
        else if (u.Mounted)
            lift = ground + DrawHorse3D(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, u.Horse!.Barding, Ease.Hash(u.Index), mountTurn);

        var p = Puppet.PoseOf(u, Now, phase, Winner);
        if (FxPuppet is { Awe.Count: > 0 }) p = Puppet.Awed(FxPuppet.Awe, u, p, phase);

        // Phase 3.5 locomotion, banking, breathing, gallop crouch & combat recoil (3D-only, preserves headless PoseOf!)
        float speed = u.Mounted ? u.Speed : u.Vel.Length();
        float walkFactor = !u.Mounted ? Math.Clamp(speed / 1.5f, 0f, 1f) : 0f;
        float runFactor = !u.Mounted ? Math.Clamp((speed - 1.8f) / 2.8f, 0f, 1f) : 0f;
        float gallop = u.Mounted ? Math.Clamp((u.Speed - 2.2f) / 6.2f, 0f, 1f) : 0f;
        float legNorm = !u.Mounted ? Math.Clamp(p.Leg / 40f, -1f, 1f) : 0f;
        float turnDelta = MathHelper.WrapAngle(u.Facing - u.ViewFacing);
        float bankX = u.Mounted
            ? Math.Clamp(mountTurn * (10f + 16f * gallop), -15f, 15f)
            : Math.Clamp(turnDelta * (8f + 15f * runFactor), -13f, 13f);
        float hitRecoil = u.HitFlash > 0 ? Math.Clamp(u.HitFlash / 0.15f, 0f, 1f) : 0f;
        float stagger = u.Action == UnitAction.Stagger ? 1f : 0f;
        float extraCrouch = !u.Mounted ? MathF.Max(p.ShieldUp ? 0.11f : 0f, stagger * 0.26f) : 0f;
        if (extraCrouch > 0f && p.Crouch < extraCrouch) p = p with { Crouch = extraCrouch };

        float gallopCrouch = u.Mounted && Rear <= 0 && u.Action != UnitAction.Windup
            ? gallop * (u.Couched ? 17f : 11f) + MathF.Sin(u.Anim * MathF.Tau - 0.6f) * 3.5f * gallop
            : 0f;
        float stirrupBounce = u.Mounted ? MathF.Sin(u.Anim * MathF.Tau - 0.35f) * 0.036f * gallop : 0f;
        bool idleBreathOn = !u.Mounted && speed < 0.35f && u.Action == UnitAction.None;
        float breath = idleBreathOn ? MathF.Sin(Now * 2.1f + phase * MathF.Tau) : 0f;

        var at = u.Pos;
        float leanBack = 0;
        if (u.Mounted && Rear > 0)
        {
            float a = Rad(RearDeg * Rear);
            at -= new Vector2(MathF.Cos(u.ViewHeading), MathF.Sin(u.ViewHeading)) * Saddle * MathF.Sin(a) * Scale * 0.6f;
            lift += (MathF.Sin(a) * 0.7f - Saddle * (1 - MathF.Cos(a))) * Scale;
            leanBack = RearDeg * Rear * 0.8f;
        }

        float extraLeanZ = gallopCrouch - hitRecoil * 4f - stagger * 4f + (p.ShieldUp ? 5f : 0f);
        float extraLeanX = bankX + stagger * (phase > 0.5f ? 5f : -5f);
        // A rider sits on his horse: hips and legs go where the horse goes, and he turns from the waist toward where he
        // looks (the sim keeps that within MountTwist of the horse's heading; a couched lance points along the horse).
        float frameYaw = u.Mounted ? u.ViewHeading : u.ViewFacing;
        float seatTwist = u.Mounted && !u.Couched ? Math.Clamp(MathHelper.ToDegrees(MathHelper.WrapAngle(u.ViewFacing - u.ViewHeading)), -110f, 110f) : 0f;
        Frame(at, frameYaw, lift + (p.Bob + stirrupBounce - (u.Mounted ? 0 : Sink(p))) * size, size);
        Lean(p.LeanX + extraLeanX, p.LeanZ - leanBack + extraLeanZ);

        // Spider web snare wrapping a caught unit.
        if (Now > 0 && Now < u.WebbedUntil && kit.WebSnare.Length > 0)
            EmitLocal(kit.WebSnare, new Vector3(0, u.Mounted ? 0.35f : 0f, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

        // 1. Legs with pelvis counter-rotation, lateral weight shift onto planted foot, and heel-strike / toe-off feel.
        float pelvisYaw = !u.Mounted ? -legNorm * (6.5f + 5.5f * runFactor) : 0f;
        float hipShiftX = !u.Mounted ? legNorm * (0.018f + 0.016f * runFactor) : 0f;
        if (!u.Mounted)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var (a, bend) = Knee(side, p);
                // Subtle heel-strike extension on forward-reaching leg & toe-off tuck on trailing leg.
                float legSign = legNorm * side;
                if (walkFactor > 0.05f && !p.Kneel)
                {
                    if (legSign > 0f) bend = MathF.Max(4f, bend - legSign * 8f * walkFactor);
                    else bend += -legSign * (10f + 12f * runFactor);
                }
                var hip = RotY(new Vector3(side * 0.125f, 0.68f + legSign * 0.012f * walkFactor, 0), pelvisYaw) + new Vector3(hipShiftX, 0, 0);
                var knee = hip + RotY(new Vector3(0, -Shank * MathF.Cos(Rad(a)), Shank * MathF.Sin(Rad(a))), pelvisYaw * 0.6f);
                var foot = knee + RotY(new Vector3(0, -Shank * MathF.Cos(Rad(a - bend)), Shank * MathF.Sin(Rad(a - bend))), pelvisYaw * 0.4f);
                var toeFwd = RotY(new Vector3(side * 0.14f, legSign > 0.15f ? 0.22f * walkFactor : -0.18f * walkFactor, 1f), pelvisYaw * 0.4f);
                if (Detail == 0)
                {
                    EmitBone(lm.Thigh, hip, knee, toeFwd, 1f, lm.LegPat);
                    EmitBone(lm.Shin, knee, foot, toeFwd, 1f, lm.LegPat);
                }
                else
                {
                    EmitBone(lm.Shin, hip, foot, Vector3.UnitZ, 1f, lm.LegPat);
                }
            }
        }
        else if (Detail == 0)
        {
            // Astride the barrel: the hips close together in the saddle, the thighs run out and forward over the horse's
            // sides, the knee grips just outside the barrel, and the shin hangs back along the flank to the stirrup.
            float barrel = 0.33f * HorseScale(u.Horse!.Barding) * Scale / size; // the barrel's half width in the rider's units
            float legTuck = gallop * 0.06f;
            float legBob = -stirrupBounce * 0.65f;
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * 0.12f, 0.68f, -0.02f - legTuck * 0.4f);
                var knee = new Vector3(side * (barrel + 0.07f), 0.30f + legBob + legTuck * 0.5f, 0.22f + legTuck);
                var foot = new Vector3(side * (barrel + 0.01f), -0.10f + legBob * 1.2f + legTuck * 0.7f, 0.04f - legTuck * 0.8f);
                EmitBone(lm.Thigh, hip, knee, Vector3.UnitZ, 1f, lm.LegPat);
                EmitBone(lm.Shin, knee, foot, Vector3.UnitZ, 1f, lm.LegPat);
            }
        }

        // 2. Determine combat + stride counter-rotation twist BEFORE torso!
        var grip = Look.GripOf(look.Weapon);
        bool ranged = Look.Ranged(look.Weapon);
        bool isCrossbow = p.Armed && (p.Haul >= 0 || (grip == Grip.Crossbow && !u.Mounted));
        float gaitTwist = !u.Mounted ? legNorm * (9f + 9f * runFactor) : 0f;
        float combatTwist = isCrossbow ? 0f : (ranged && p.Aim) ? 28f : (ranged || !p.Armed) ? 0f : Puppet.TwistOf(p);
        if (p.TwoHand && u.Action != UnitAction.None) combatTwist = Math.Clamp(combatTwist * 1.28f, -44f, 44f);
        if (p.ShieldUp && u.Action == UnitAction.Block) combatTwist -= 14f;
        twist = combatTwist + ((u.Action == UnitAction.None && !(ranged && p.Aim)) ? gaitTwist : gaitTwist * 0.2f) + seatTwist;

        Vector3 torsoPos = new(hipShiftX * 0.75f, breath * 0.008f, 0);
        Vector3 tR = RotY(Vector3.UnitX, twist);
        Vector3 tF = RotY(Vector3.UnitZ, twist);
        float syB = 1f + breath * 0.012f, szB = 1f + breath * 0.018f;
        var chestMesh = lm.Chest;
        var skirtMesh = lm.Skirt;
        var shldMesh = lm.Shoulders;
        var beltMesh = lm.Belt;
        // Subtle deterministic per-soldier modular mix-and-match within companies (zero GC, off for player & showcase!)
        if (!ExactLook && !u.IsPlayer && !u.IsLeader && !u.Hero)
        {
            int seed = Variety.Seed(u);
            if (lm.ShldVarGroup == 1) // Heavy plate knights vary Gothic, Fluted, Dark & Long-Tasset plate harnesses!
            {
                if ((seed % 3) == 1 && kit.ChestPoolPlate.Length > 0) chestMesh = kit.ChestPoolPlate[Math.Abs(seed * 5 + 1) % kit.ChestPoolPlate.Length];
                if ((seed % 3) == 2 && kit.SkirtPoolPlate.Length > 0) skirtMesh = kit.SkirtPoolPlate[Math.Abs(seed * 13 + 2) % kit.SkirtPoolPlate.Length];
            }
            if (Detail == 0)
            {
                if (lm.BeltVarGroup > 0 && (seed & 3) != 0)
                {
                    var pool = lm.BeltVarGroup == 1 ? kit.BeltPoolLight : lm.BeltVarGroup == 2 ? kit.BeltPoolHeavy : kit.BeltPoolNorth;
                    if (pool.Length > 0) beltMesh = pool[Math.Abs(seed * 7 + 3) % pool.Length];
                }
                if (lm.ShldVarGroup > 0 && (seed % 4) == 1)
                {
                    var pool = lm.ShldVarGroup == 1 ? kit.ShldPoolKnight : kit.ShldPoolFoot;
                    if (pool.Length > 0) shldMesh = pool[Math.Abs(seed * 11 + 5) % pool.Length];
                }
            }
        }
        EmitLocal(chestMesh, torsoPos, tR, Vector3.UnitY, tF, 1f, syB, szB, lm.ChestPat);
        if (skirtMesh.Length > 0)
            EmitLocal(skirtMesh, torsoPos, tR, Vector3.UnitY, tF, 1f, 1f, 1f, lm.SkirtPat);
        if (Detail == 0)
        {
            if (shldMesh.Length > 0) EmitLocal(shldMesh, torsoPos, tR, Vector3.UnitY, tF, 1f, syB, szB, lm.ShldPat);
            if (lm.TabardPanel.Length > 0) EmitLocal(lm.TabardPanel, torsoPos, tR, Vector3.UnitY, tF, 1f, 1f, 1f, MeshKit3D.PatQuilt);
            if (beltMesh.Length > 0) EmitLocal(beltMesh, torsoPos, tR, Vector3.UnitY, tF, 1f, 1f, szB);
            if (lm.HasTabard)
            {
                var emb = EmblemOf(palIdx);
                if (emb.Length > 0)
                    EmitLocal(emb, torsoPos + RotY(new Vector3(0, 1.03f, 0.186f), twist), tR, Vector3.UnitY, tF, 0.56f, 0.56f, 0.7f);
            }
        }
        if (lm.HasCape && Detail == 0)
        {
            // Secondary motion: speed-driven cape billow, flutter, and turn-delta whip lag around the shoulder pin!
            float capeSpeed = Math.Clamp(speed, 0f, 10f);
            float capePitch = Math.Clamp(capeSpeed * 3.8f + (u.Action == UnitAction.Recover ? 9f : 0f), 0f, 34f)
                + MathF.Sin(Now * (5.2f + capeSpeed * 1.25f) + phase * 11f) * (3.0f + capeSpeed * 0.9f);
            float capeYaw = twist + Math.Clamp(-MathHelper.ToDegrees(u.Mounted ? mountTurn : turnDelta) * 0.65f, -24f, 24f)
                + MathF.Sin(Now * 3.6f + phase * 7f) * (2.4f + capeSpeed * 0.65f);
            Vector3 cR = RotY(Vector3.UnitX, capeYaw);
            Vector3 cU = RotY(RotX(Vector3.UnitY, capePitch), capeYaw);
            Vector3 cF = RotY(RotX(Vector3.UnitZ, capePitch), capeYaw);
            Vector3 pin = torsoPos + RotY(new Vector3(0, 1.28f, -0.16f), twist);
            Vector3 capeOrigin = pin - (cU * 1.28f + cF * -0.16f);
            EmitLocal(kit.Cape, capeOrigin, cR, cU, cF, 1f, 1f, 1f, MeshKit3D.PatQuilt);
        }

        // 3. Head + Helm + Plume (with turn look-ahead, hit recoil snap, and secondary plume whip lag)
        float headYaw = p.Look + Math.Clamp(MathHelper.ToDegrees(turnDelta) * 0.45f, -22f, 22f) + (ranged && p.Aim ? seatTwist : (twist - seatTwist) * 0.4f + seatTwist);
        float headPitch = -hitRecoil * 18f - stagger * 10f + (p.ShieldUp ? 6f : 0f) - gallopCrouch * 0.45f;
        Vector3 hR = RotY(Vector3.UnitX, headYaw);
        Vector3 hU = RotY(RotX(Vector3.UnitY, headPitch), headYaw);
        Vector3 hF = RotY(RotX(Vector3.UnitZ, headPitch), headYaw);
        Vector3 headPos = torsoPos + new Vector3(0, 1.53f + breath * 0.01f, 0.01f);
        if (!lm.FullEnclosedHelm)
            EmitLocal(lm.Head, headPos, hR, hU, hF);
        if (lm.Helm.Length > 0)
            EmitLocal(lm.Helm, headPos, hR, hU, hF);
        if (lm.HasPlume && Detail == 0)
        {
            float pSpeed = Math.Clamp(speed, 0f, 10f);
            float pPitch = Math.Clamp(pSpeed * 3.6f, 0f, 30f) + MathF.Sin(Now * (7.2f + pSpeed * 1.5f) + phase * 13f) * (2.8f + pSpeed * 0.85f) - hitRecoil * 16f;
            float pYaw = headYaw + Math.Clamp(-MathHelper.ToDegrees(u.Mounted ? mountTurn : turnDelta) * 0.75f, -26f, 26f)
                + MathF.Sin(Now * 4.8f + phase * 9f) * (2.5f + pSpeed * 0.6f);
            Vector3 pR = RotY(Vector3.UnitX, pYaw);
            Vector3 pU = RotY(RotX(Vector3.UnitY, headPitch - pPitch), pYaw);
            Vector3 pF = RotY(RotX(Vector3.UnitZ, headPitch - pPitch), pYaw);
            Vector3 plumePos = headPos + hU * 0.25f - hF * 0.01f;
            EmitLocal(kit.Plume, plumePos, pR, pU, pF);
        }

        // 4. Arms, Weapon, Shield, Quivers & Special Combat Stances
        Vector3 Shoulder(int side) => torsoPos + RotY(new Vector3(side * 0.23f, 1.24f + breath * 0.01f, 0), twist);
        Vector3 ArmEnd(int side, float alphaDeg, float yawDeg, float reach)
        {
            var sh = Shoulder(side);
            float a = Rad(alphaDeg);
            return sh + RotY(new Vector3(0, -MathF.Cos(a), MathF.Sin(a)), yawDeg + twist) * (UpperArm + Forearm) * reach;
        }
        void DrawArm(int side, Vector3 hand, Vector3? customPole = null)
        {
            var sh = Shoulder(side);
            if (Detail == 0)
            {
                var pole = customPole ?? RotY(new Vector3(side * 0.5f, -0.5f, -0.7f), twist);
                var el = Elbow(sh, ref hand, pole);
                EmitBone(lm.UArm, sh, el, Vector3.UnitZ, 1f, lm.LimbPat);
                EmitBone(lm.FArm, el, hand, Vector3.UnitZ, 1f, lm.LimbPat);
            }
            else
            {
                EmitBone(lm.FArm, sh, hand, Vector3.UnitZ, 1f, lm.LimbPat);
            }
        }

        // Crossbow spanning & aiming
        if (isCrossbow)
        {
            if (Detail == 0 && kit.Quiver.Length > 0)
                EmitLocal(kit.Quiver, new Vector3(-0.10f, 1.12f, -0.21f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            if (u.Type.HasShield && lm.HasShield && Detail == 0 && look.Shield != "pavise")
                EmitShield(lm, palIdx, new Vector3(0.05f, 1.00f, -0.22f), 180f, 0, true);
            else if (u.Type.HasShield && lm.HasShield && look.Shield == "pavise" && Detail == 0)
                EmitShield(lm, palIdx, new Vector3(0.04f, 0.96f, -0.23f), 180f, u.StuckArrows, true);

            if (p.Haul >= 0)
            {
                // Spanning: prod down at feet, both hands hauling the bowstring up to the nut.
                var down = Vector3.Normalize(new Vector3(0, -1f, 0.08f));
                var butt = new Vector3(0.02f, 0.92f, 0.42f);
                float y = MathHelper.Lerp(0.35f, 0.82f, p.Haul);
                DrawArm(1, new Vector3(0.08f, y, 0.44f));
                DrawArm(-1, new Vector3(-0.06f, y, 0.44f));
                EmitDir(lm.Weapon, butt + down * 0.14f, down);
                return true;
            }

            var xDir = Vector3.Normalize(Vector3.Lerp(new Vector3(-0.28f, 0.16f, 0.94f), Vector3.UnitZ, p.Draw));
            var xAt = Vector3.Lerp(new Vector3(0.16f, 1.02f, 0.14f), new Vector3(0.12f, 1.34f, 0.06f), p.Draw);
            DrawArm(1, xAt + xDir * 0.12f);
            DrawArm(-1, xAt + xDir * 0.40f);
            EmitDir(lm.Weapon, xAt + xDir * 0.06f, xDir);
            if (p.Draw > 0.95f && u.Action != UnitAction.Recover && Detail == 0 && kit.Arrow.Length > 0)
                EmitDir(kit.Arrow, xAt + xDir * 0.24f + Vector3.Up * 0.04f, xDir, 1.15f, 0.62f, 1.15f);
            return true;
        }

        // Bow drawing & loose
        if (ranged && p.Aim)
        {
            if (Detail == 0 && kit.Quiver.Length > 0)
                EmitLocal(kit.Quiver, RotY(new Vector3(-0.10f, 1.12f, -0.21f), twist), tR, Vector3.UnitY, tF);
            if (u.Type.HasShield && lm.HasShield && Detail == 0)
                EmitShield(lm, palIdx, RotY(new Vector3(0.05f, 1.00f, -0.22f), twist), 180f + twist, 0, true);

            var bowHand = Shoulder(-1) + RotY(Vector3.Normalize(new Vector3(0.12f, 0.08f, 1f)), -twist) * (UpperArm + Forearm) * 0.96f;
            DrawArm(-1, bowHand);
            EmitLocal(lm.Weapon.Length > 0 ? lm.Weapon : kit.Bow, bowHand, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

            bool loosed = u.Action == UnitAction.Recover;
            var cheek = new Vector3(0.06f, 1.44f, 0.09f);
            var drawHand = loosed ? cheek + new Vector3(0.08f, 0.02f, -0.12f) : Vector3.Lerp(bowHand + new Vector3(0.02f, -0.02f, -0.08f), cheek, p.Draw);
            DrawArm(1, drawHand, new Vector3(0.6f, 0.15f + 0.5f * p.Draw, -1f));
            if (!loosed && Detail == 0 && kit.Arrow.Length > 0)
            {
                var arrowDir = Vector3.Normalize(bowHand - drawHand);
                EmitDir(kit.Arrow, drawHand, arrowDir, 1f, 0.95f, 1f);
            }
            return true;
        }

        var wHand = ArmEnd(1, p.ArmA + (stagger > 0 ? 18f : 0f), p.Yaw - twist + (stagger > 0 ? 16f : 0f), p.Reach);
        var wDir = RotY(new Vector3(0, MathF.Sin(Rad(p.WeapB)), MathF.Cos(Rad(p.WeapB))), p.Yaw);
        if (wDir.LengthSquared() < 1e-4f) wDir = Vector3.UnitY;
        wDir.Normalize();

        // Tighter two-handed grip alignment: pull overly wide windup hands slightly toward torso center so both arms stay solidly on the hilt/shaft.
        bool poleForwardGrip = p.TwoHand && p.Armed && lm.OffHandForward && p.WeapB < 75f;
        Vector3 offHandTarget = p.TwoHand && p.Armed
            ? (poleForwardGrip ? wHand + wDir * lm.OffHandOffset : wHand - wDir * lm.OffHandOffset)
            : Vector3.Zero;
        if (p.TwoHand && p.Armed)
        {
            var lSh = Shoulder(-1);
            float maxTwoReach = (UpperArm + Forearm) * 0.97f;
            var toOff = offHandTarget - lSh;
            float distOff = toOff.Length();
            if (distOff > maxTwoReach)
            {
                var pull = toOff * ((distOff - maxTwoReach) / distOff);
                wHand -= pull * 0.85f;
                offHandTarget -= pull * 0.85f;
            }
        }

        DrawArm(1, wHand);

        bool shieldArm = p.Armed && u.Type.HasShield && !ranged && !p.TwoHand && !u.Couched;
        float shieldStrideBob = !u.Mounted ? legNorm * (0.032f + 0.025f * runFactor) : 0f;
        Vector3 offHand = p.TwoHand && p.Armed
            ? offHandTarget
            : shieldArm
                ? (p.ShieldUp
                    ? torsoPos + RotY(new Vector3(-0.07f, 1.18f + hitRecoil * 0.06f, 0.37f - hitRecoil * 0.07f), twist * 0.35f)
                    : torsoPos + RotY(new Vector3(-0.24f - stagger * 0.12f, 0.98f + p.OffA * 0.002f + MathF.Abs(shieldStrideBob) * 0.35f, 0.24f - shieldStrideBob - hitRecoil * 0.08f), twist * 0.45f))
                : ArmEnd(-1, p.OffA + stagger * 22f, -twist - stagger * 18f, p.OffReach);
        Vector3? offPole = p.TwoHand && p.Armed
            ? RotY(new Vector3(-0.68f, -0.42f, -0.45f), twist * 0.65f)
            : shieldArm && p.ShieldUp
                ? new Vector3(-0.75f, -0.55f, -0.35f)
                : null;
        DrawArm(-1, offHand, offPole);

        if (!p.Armed) return true; // routed man has thrown away his weapon and shield

        // Couched lance (heavy cavalry at gallop)
        if (u.Couched)
        {
            float rel = MathHelper.WrapAngle(u.ViewHeading - frameYaw);
            var lanceDir = RotY(Vector3.Normalize(new Vector3(0, -0.06f, 1f)), MathHelper.ToDegrees(rel));
            var lanceMesh = lm.Weapon.Length > 0 ? lm.Weapon : kit.Get("weapon_lance");
            EmitDir(lanceMesh, wHand, lanceDir);
            if (u.Type.HasShield && lm.HasShield)
                EmitShield(lm, palIdx, offHand + new Vector3(-0.07f, 0.02f, 0.06f), -58f, u.StuckArrows, false);
            return true;
        }

        // Ranged unit marching / idle (not aiming)
        if (ranged)
        {
            if (Detail == 0 && kit.Quiver.Length > 0)
                EmitLocal(kit.Quiver, new Vector3(-0.10f, 1.12f, -0.21f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            if (u.Type.HasShield && lm.HasShield && Detail == 0)
                EmitShield(lm, palIdx, new Vector3(0.05f, 1.00f, -0.22f), 180f, 0, true);
            EmitLocal(lm.Weapon.Length > 0 ? lm.Weapon : kit.Bow, offHand, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return true;
        }

        // Spare javelins strapped diagonally across skirmisher's back
        if (grip == Grip.Thrown && u.Ammo > 1 && Detail == 0 && kit.Javelin.Length > 0)
        {
            for (int i = 0; i < 2; i++)
                EmitDir(kit.Javelin, RotY(new Vector3(-0.06f + i * 0.08f, 1.02f, -0.21f), twist),
                    RotY(Vector3.Normalize(new Vector3(0.30f - i * 0.15f, 1f, -0.10f)), twist));
        }

        // Slinger whirling sling overhead
        if (p.Whirl)
        {
            float w = Now * (9f + 9f * p.Draw) + phase * 6f;
            wDir = Vector3.Normalize(new Vector3(MathF.Cos(w), 0.22f, MathF.Sin(w)));
        }

        if (!p.Empty && lm.Weapon.Length > 0)
        {
            // Align weapon cutting edge cleanly into the active swing plane (Right/Left horizontal, Overhead vertical, Thrust sleek).
            Vector3 edgeHint = (u.Action is UnitAction.Windup or UnitAction.Recover)
                ? u.Swing switch
                {
                    AttackDir.Right => RotY(new Vector3(-0.96f, -0.26f, 0f), p.Yaw),
                    AttackDir.Left => RotY(new Vector3(0.96f, -0.26f, 0f), p.Yaw),
                    AttackDir.Thrust => lm.WeaponEdgeZ ? Vector3.Down : RotY(new Vector3(0.88f, -0.45f, 0f), p.Yaw),
                    _ => RotY(new Vector3(0f, -MathF.Cos(Rad(p.WeapB)), MathF.Sin(Rad(p.WeapB))), p.Yaw), // Overhead chop edge forward/down
                }
                : RotY(new Vector3(0.28f, -0.30f, 0.91f), p.Yaw);
            EmitWeapon(lm.Weapon, wHand, wDir, edgeHint, lm.WeaponEdgeZ);

            // Crisp 3D steel slash-arc ribbons (Detail 0, works in battle AND AnimSheet) + optional battle haze glint.
            if (Detail == 0 && grip is Grip.One or Grip.Two or Grip.Pole)
            {
                float strikeK = -1f;
                if (!u.IsPlayer && u.Action == UnitAction.Recover && u.Type.Recover > 0)
                {
                    float rk = 1f - u.ActionTimer / u.Type.Recover;
                    if (rk < 0.30f) strikeK = rk / 0.30f;
                }
                else if (u.IsPlayer && u.Action == UnitAction.Windup)
                {
                    float wk = 1f - u.ActionTimer / (u.Swing == AttackDir.Overhead ? 0.2f : 0.15f);
                    if (wk > 0.55f) strikeK = (wk - 0.55f) / 0.45f;
                }
                if (strikeK >= 0f && !ShadowOnly)
                {
                    EmitSlashArc(wHand, wDir, p.Yaw, p.WeapB, u.Swing, lm.BladeReach, strikeK, p.TwoHand || grip == Grip.Pole);
                    if (FxPuppet?.Soft != null)
                    {
                        float reachLen = lm.BladeReach;
                        var tip = W(wHand + wDir * (reachLen * 0.85f));
                        var mid = W(wHand + wDir * (reachLen * 0.52f));
                        float a = MathF.Sin(strikeK * MathF.PI) * (p.TwoHand ? 0.42f : 0.28f);
                        var col = new Color(235, 242, 255);
                        if (DeferHaze)
                        {
                            PendingHaze.Add((tip, (p.TwoHand ? 0.48f : 0.32f) * scale, col, a, strikeK * 6f + u.Index));
                            PendingHaze.Add((mid, (p.TwoHand ? 0.32f : 0.20f) * scale, col, a * 0.6f, strikeK * 4f + u.Index));
                        }
                        else
                        {
                            FxPuppet.Haze(tip, (p.TwoHand ? 0.48f : 0.32f) * scale, col, a, strikeK * 6f + u.Index);
                            FxPuppet.Haze(mid, (p.TwoHand ? 0.32f : 0.20f) * scale, col, a * 0.6f, strikeK * 4f + u.Index);
                        }
                    }
                }
            }
        }

        if (u.Type.HasShield && lm.HasShield)
        {
            Vector3 shPos = p.ShieldUp ? offHand + new Vector3(0.05f, 0.02f, 0.12f) : offHand + new Vector3(-0.07f, 0.02f, 0.06f);
            float shYaw = p.ShieldUp ? (14f - hitRecoil * 16f) : (-58f + twist * 0.35f - stagger * 22f);
            float shPitch = p.ShieldUp ? (-14f + hitRecoil * 22f) : (runFactor * 12f + stagger * 18f);
            float shRoll = p.ShieldUp ? (8f - hitRecoil * 14f) : (-legNorm * 6f * walkFactor - stagger * 16f);
            EmitShield(lm, palIdx, shPos, shYaw, u.StuckArrows, false, shPitch, shRoll);
        }

        return true;
    }

    MeshKit3D.Vert[] EmblemOf(int palIdx, int chargeOverride = -1)
    {
        if (kit.Emblems.Length == 0) return Array.Empty<MeshKit3D.Vert>();
        Emblem e = chargeOverride >= 0 && chargeOverride < kit.Emblems.Length
            ? (Emblem)chargeOverride
            : Style.Sides[Math.Clamp(palIdx, 0, Style.Sides.Length - 1)].Charge;
        return kit.Emblems[(int)e];
    }

    /// Emit a crisp 3D cel-shaded tapered steel slash-arc ribbon along the active swing plane (zero-alloc, 6-8 tris).
    void EmitSlashArc(Vector3 hand, Vector3 wDir, float yawDeg, float pitchDeg, AttackDir swing, float reach, float strikeK, bool heavy)
    {
        float intensity = MathF.Sin(strikeK * MathF.PI);
        if (intensity < 0.22f) return;

        // Sun-facing normal ensures steel slash streaks always gleam crisp silver-white instead of shading muddy grey.
        Vector3 nFlash = Vector3.Normalize(-World.SunDir + Vector3.UnitY * 0.35f);
        float rOuter = reach * 1.03f;
        float rInner = reach * (heavy ? 0.66f : 0.72f);
        byte alpha = (byte)((MeshKit3D.PatSmooth << 4) | 15);

        if (swing == AttackDir.Thrust)
        {
            // Sleek needle-sharp steel thrust glint along the point!
            Vector3 side = Vector3.Cross(wDir, Vector3.UnitY);
            if (side.LengthSquared() < 1e-4f) side = Vector3.UnitX; else side.Normalize();
            float halfW = (heavy ? 0.055f : 0.042f) * intensity;
            Vector3 tip = W(hand + wDir * (rOuter + 0.14f * intensity));
            Vector3 tail = W(hand + wDir * (reach * 0.35f));
            Vector3 midL = W(hand + wDir * (reach * 0.80f) - side * halfW);
            Vector3 midR = W(hand + wDir * (reach * 0.80f) + side * halfW);
            var cTip = new Color((byte)255, (byte)255, (byte)255, alpha);
            var cMid = new Color((byte)236, (byte)244, (byte)255, alpha);
            var cTail = new Color((byte)195, (byte)214, (byte)240, alpha);
            verts.Reserve(6);
            var dst = verts.A; int n = verts.N;
            dst[n++] = new VertexPNCT(tip, nFlash, cTip, Vector2.Zero);
            dst[n++] = new VertexPNCT(midL, nFlash, cMid, Vector2.Zero);
            dst[n++] = new VertexPNCT(midR, nFlash, cMid, Vector2.Zero);
            dst[n++] = new VertexPNCT(tail, nFlash, cTail, Vector2.Zero);
            dst[n++] = new VertexPNCT(midR, nFlash, cMid, Vector2.Zero);
            dst[n++] = new VertexPNCT(midL, nFlash, cMid, Vector2.Zero);
            verts.N = n;
            return;
        }

        const int segs = 4;
        float arcSpan = (heavy ? 64f : 52f) * (0.55f + 0.45f * intensity);
        verts.Reserve(segs * 6);
        var aDst = verts.A; int an = verts.N;
        Vector3 prevIn = default, prevOut = default;
        Color prevC = default;

        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs; // 0 = leading blade edge, 1 = razor-tapered trailing tail
            float dyaw = swing == AttackDir.Right ? t * arcSpan : swing == AttackDir.Left ? -t * arcSpan : 0f;
            float dpitch = swing == AttackDir.Overhead ? t * arcSpan : t * 18f;
            Vector3 d = RotY(new Vector3(0, MathF.Sin(Rad(pitchDeg + dpitch)), MathF.Cos(Rad(pitchDeg + dpitch))), yawDeg + dyaw);
            float radIn = MathHelper.Lerp(rInner, rOuter * 0.985f, MathF.Sqrt(t));
            float radOut = MathHelper.Lerp(rOuter, rOuter * 0.995f, t);
            Vector3 pIn = W(hand + d * radIn);
            Vector3 pOut = W(hand + d * radOut);
            int cr = (int)MathHelper.Lerp(255, 198, t);
            int cg = (int)MathHelper.Lerp(255, 218, t);
            int cb = (int)MathHelper.Lerp(255, 245, t);
            var col = new Color((byte)cr, (byte)cg, (byte)cb, alpha);

            if (i > 0)
            {
                aDst[an++] = new VertexPNCT(prevOut, nFlash, prevC, Vector2.Zero);
                aDst[an++] = new VertexPNCT(prevIn, nFlash, prevC, Vector2.Zero);
                aDst[an++] = new VertexPNCT(pOut, nFlash, col, Vector2.Zero);
                aDst[an++] = new VertexPNCT(pOut, nFlash, col, Vector2.Zero);
                aDst[an++] = new VertexPNCT(prevIn, nFlash, prevC, Vector2.Zero);
                aDst[an++] = new VertexPNCT(pIn, nFlash, col, Vector2.Zero);
            }
            prevIn = pIn; prevOut = pOut; prevC = col;
        }
        verts.N = an;
    }

    /// Emit a shield mesh with the side's exact 3D relief heraldic emblem and any arrows stuck into its face.
    void EmitShield(MeshKit3D.LookMeshes lm, int palIdx, Vector3 pos, float yawDeg, int stuckArrows, bool backSlung, float pitchDeg = 0f, float rollDeg = 0f)
    {
        Vector3 bR = RotZ(Vector3.UnitX, rollDeg);
        Vector3 bU = RotX(RotZ(Vector3.UnitY, rollDeg), pitchDeg);
        Vector3 bF = RotX(Vector3.UnitZ, pitchDeg);
        Vector3 sR = RotY(bR, yawDeg);
        Vector3 sU = RotY(bU, yawDeg);
        Vector3 sF = RotY(bF, yawDeg);
        EmitLocal(lm.Shield, pos, sR, sU, sF);

        if (Detail <= 1)
        {
            var emb = EmblemOf(palIdx);
            if (emb.Length > 0)
            {
                (float ey, float ez, float sx, float sy) = lm.ShieldKind switch
                {
                    "shield_pavise" => (-0.02f, 0.080f, 1.15f, 1.55f),
                    "shield_tower" => (0.14f, 0.082f, 0.92f, 1.05f),
                    "shield_kite" => (0.04f, 0.070f, 0.86f, 1.05f),
                    "shield_round" => (0.00f, 0.068f, 0.85f, 0.85f),
                    "shield_kalkan" => (0.00f, 0.095f, 0.72f, 0.72f),
                    _ => (0.02f, 0.070f, 0.94f, 0.94f), // heater
                };
                Vector3 ePos = pos + sU * ey + sF * ez;
                EmitLocal(emb, ePos, sR, sU, sF, sx, sy, 1f);
            }
        }

        if (!backSlung && Detail == 0 && stuckArrows > 0 && kit.Arrow.Length > 0)
        {
            int count = Math.Min(stuckArrows, 4);
            for (int i = 0; i < count; i++)
            {
                float ox = (i % 2 - 0.5f) * 0.16f;
                float oy = (i / 2 - 0.5f) * 0.18f;
                Vector3 basePos = pos + sR * ox + sU * oy + sF * 0.06f;
                Vector3 dir = Vector3.Normalize(sF + sR * (0.20f * (i % 2 * 2 - 1)) + sU * 0.24f);
                // Arrow shaft points outward from shield face (head buried in shield, fletching sticking out!)
                EmitDir(kit.Arrow, basePos + dir * 0.42f, -dir, 0.9f, 0.65f, 0.9f);
            }
        }
    }

    /// Full directional 3D death animation matching `Puppet.Fallen` (Fall.Cut spin, Thrust crumple, Flung arc, Saddle slide).
    void DrawFallen3D(Unit u, float ground, float size, float phase, int palIdx, Look look, MeshKit3D.LookMeshes lm)
    {
        var away = u.Pos - u.DiedFrom;
        float yaw = u.ViewFacing;
        Vector2 fwd2 = new(MathF.Cos(yaw), MathF.Sin(yaw)), right2 = new(-fwd2.Y, fwd2.X);
        Vector2 dir = u.DiedFrom == Vector2.Zero || away.LengthSquared() < 1e-4f ? -fwd2 : Vector2.Normalize(away);
        var fall = u.DiedFall;
        Vector2 saddleSide = new(MathF.Sin(u.Heading), -MathF.Cos(u.Heading));
        if (fall == Fall.Thrust) dir = -dir;
        if (fall == Fall.Saddle) dir = saddleSide;
        float turn = (phase - 0.5f) * (fall == Fall.Saddle ? 0.5f : 1.2f);
        dir = new Vector2(dir.X * MathF.Cos(turn) - dir.Y * MathF.Sin(turn), dir.X * MathF.Sin(turn) + dir.Y * MathF.Cos(turn));
        float time = fall switch { Fall.Saddle => 0.75f, Fall.Flung => 0.6f, _ => 0.45f + phase * 0.3f };
        float k = Math.Clamp((Now - u.DiedAt) / time, 0f, 1f);

        bool crumple = fall == Fall.Thrust || (fall == Fall.Plain && phase < 0.3f);
        float sink = crumple ? Ease.Out(MathF.Min(1f, k * 2f)) * 0.35f : 0f;
        var at = u.Pos;
        float y = ground - sink * size;
        if (fall == Fall.Cut)
        {
            yaw += MathF.Sign(Vector2.Dot(dir, right2) + 0.01f) * 1.4f * Ease.Out(k);
            fwd2 = new(MathF.Cos(yaw), MathF.Sin(yaw)); right2 = new(-fwd2.Y, fwd2.X);
        }
        else if (fall == Fall.Flung)
        {
            at -= dir * 2.2f * (1f - Ease.Out(k));
            y += MathF.Sin(k * MathF.PI) * 0.5f * size;
        }
        else if (fall == Fall.Saddle)
        {
            at -= saddleSide * (1f - Ease.In(k));
            y += Saddle * (1f - Ease.In(k)) * size;
        }

        Frame(at, yaw, y, size);
        float a = 85f * (fall == Fall.Flung ? Ease.Out(k) : crumple ? Ease.In(MathF.Max(0f, k * 1.4f - 0.4f)) : Ease.In(k));
        Lean(Vector2.Dot(dir, right2) * a, Vector2.Dot(dir, fwd2) * a);

        if (Detail >= 2)
        {
            EmitLocal(kit.LodFoot, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return;
        }

        float kneeFold = crumple ? 75f * Ease.Out(k) : 28f * Ease.Out(k);
        float legSpread = 16f * Ease.Out(k);
        for (int side = -1; side <= 1; side += 2)
        {
            var hip = new Vector3(side * 0.125f, 0.68f, 0);
            float thighA = Rad((crumple ? 35f * k : 14f * side * k));
            var knee = hip + new Vector3(side * 0.04f * k, -Shank * MathF.Cos(thighA), Shank * MathF.Sin(thighA));
            float shinA = thighA - Rad(kneeFold);
            var foot = knee + new Vector3(side * 0.03f * k, -Shank * MathF.Cos(shinA), Shank * MathF.Sin(shinA));
            if (Detail == 0)
            {
                EmitBone(lm.Thigh, hip, knee, Vector3.UnitZ, 1f, lm.LegPat);
                EmitBone(lm.Shin, knee, foot, Vector3.UnitZ, 1f, lm.LegPat);
            }
            else EmitBone(lm.Shin, hip, foot, Vector3.UnitZ, 1f, lm.LegPat);
        }

        EmitLocal(lm.Chest, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 1f, 1f, 1f, lm.ChestPat);
        if (lm.Skirt.Length > 0) EmitLocal(lm.Skirt, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 1f, 1f, 1f, lm.SkirtPat);
        if (Detail == 0)
        {
            if (lm.Shoulders.Length > 0) EmitLocal(lm.Shoulders, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 1f, 1f, 1f, lm.ShldPat);
            if (lm.TabardPanel.Length > 0) EmitLocal(lm.TabardPanel, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 1f, 1f, 1f, MeshKit3D.PatQuilt);
            if (lm.Belt.Length > 0) EmitLocal(lm.Belt, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        }
        float hPitch = -22f * Ease.Out(k);
        Vector3 hU = RotX(Vector3.UnitY, hPitch), hF = RotX(Vector3.UnitZ, hPitch);
        if (!lm.FullEnclosedHelm && (Detail == 0 || lm.Helm.Length == 0))
            EmitLocal(lm.Head, new Vector3(0, 1.53f, 0.01f), Vector3.UnitX, hU, hF);
        if (lm.Helm.Length > 0)
            EmitLocal(lm.Helm, new Vector3(0, 1.53f, 0.01f), Vector3.UnitX, hU, hF);

        float limp = Ease.Out(k);
        var rSh = new Vector3(0.23f, 1.24f, 0);
        var lSh = new Vector3(-0.23f, 1.24f, 0);
        var rHand = rSh + new Vector3(0.22f + 0.18f * limp, -0.30f + 0.25f * limp, 0.24f - 0.10f * limp);
        var lHand = lSh + new Vector3(-0.20f - 0.22f * limp, -0.26f + 0.18f * limp, 0.20f - 0.14f * limp);
        if (Detail == 0)
        {
            var rEl = Elbow(rSh, ref rHand, new Vector3(0.6f, -0.4f, -0.6f));
            var lEl = Elbow(lSh, ref lHand, new Vector3(-0.6f, -0.4f, -0.6f));
            EmitBone(lm.UArm, rSh, rEl, Vector3.UnitZ, 1f, lm.LimbPat);
            EmitBone(lm.FArm, rEl, rHand, Vector3.UnitZ, 1f, lm.LimbPat);
            EmitBone(lm.UArm, lSh, lEl, Vector3.UnitZ, 1f, lm.LimbPat);
            EmitBone(lm.FArm, lEl, lHand, Vector3.UnitZ, 1f, lm.LimbPat);
        }
        else
        {
            EmitBone(lm.FArm, rSh, rHand, Vector3.UnitZ, 1f, lm.LimbPat);
            EmitBone(lm.FArm, lSh, lHand, Vector3.UnitZ, 1f, lm.LimbPat);
        }

        if (k < 0.4f && lm.Weapon.Length > 0)
            EmitDir(lm.Weapon, rHand, RotY(new Vector3(0, 0.5f, 0.87f), 20f));
    }

    /// 3D company banner on the unified pixel grid: wooden pole + brass finial + 3-strip rippling 3D cloth + heraldic emblem!
    /// The gold of a company's battle honours: streamers and the fringe (C6).
    static readonly Vector3 HonourGold = new(1f, 0.8f, 0.3f), FringeSeam = new(0.3f, 0.2f, 0.08f);

    public void Banner(Vector2 pos, float ground, int palette, float wave, int charge = -1, float stream = 0, float lean = 0, Vector2 fallDir = default, int streamers = 0, bool fringe = false)
    {
        int palIdx = Math.Clamp(palette, 0, Style.Sides.Length - 1);
        UnitMatTable.Fill(palIdx, -1, 0, false, Color.White, palCols);
        WorldFrame(new Vector3(pos.X, ground, pos.Y));

        Vector3 up = Vector3.UnitY;
        if (lean != 0f)
        {
            var d = fallDir.LengthSquared() > 1e-4f ? Vector2.Normalize(fallDir) : Vector2.UnitX;
            up = Vector3.Normalize(Vector3.UnitY * MathF.Cos(lean) + new Vector3(d.X, 0, d.Y) * MathF.Sin(lean));
        }

        // Align banner cloth to face the camera cleanly while staying a genuine 3D cel-shaded mesh with depth & shadows.
        Vector3 side = camRight - up * Vector3.Dot(camRight, up);
        side = side.LengthSquared() > 1e-4f ? Vector3.Normalize(side) : Vector3.UnitX;
        Vector3 faceN = Vector3.Normalize(Vector3.Cross(side, up));

        // 1. 3D Wooden pole + brass finial & crossbar.
        if (kit.BannerPole.Length > 0)
            EmitLocal(kit.BannerPole, Vector3.Zero, side, up, faceN);

        // 2. Rippling 3-strip 3D cloth flag with bottom trim border + true wavy normals.
        const int strips = 3;
        const float w = 0.82f, h = 0.56f, trimH = 0.085f;
        float amp = 0.045f + 0.075f * stream;
        Vector3 tl = up * 3.16f + side * 0.02f;

        Vector3 Edge(int i, out float rippleZ)
        {
            float f = i / (float)strips;
            float ph = wave * (1.6f + stream) - f * 3.2f;
            float dy = MathF.Sin(ph) * amp * f * 1.7f - 0.11f * f * (1f - stream) * (lean != 0 ? 0f : 1f);
            rippleZ = MathF.Cos(ph) * (0.04f + 0.06f * stream) * f;
            return tl + side * (w * f) + up * dy + faceN * rippleZ;
        }

        var clothCol = palCols[1];
        var trimCol = palCols[3];
        byte clothAlpha = (byte)(MeshKit3D.PatQuilt << 4);
        byte trimAlpha = 0;

        verts.Reserve(strips * 36 + 3 * 4 * 12);
        var dst = verts.A;
        int n = verts.N;

        void AddQuad(Vector3 a0, Vector3 b0, Vector3 a1, Vector3 b1, Vector3 nrm, Vector3 mc, float shade, byte alpha, float u0, float u1, float v0, float v1)
        {
            int cr = Math.Min(255, (int)(mc.X * 255f * shade));
            int cg = Math.Min(255, (int)(mc.Y * 255f * shade));
            int cb = Math.Min(255, (int)(mc.Z * 255f * shade));
            var c = new Color(cr, cg, cb, alpha);
            dst[n++] = new VertexPNCT(origin + a0, nrm, c, new Vector2(u0, v0));
            dst[n++] = new VertexPNCT(origin + b0, nrm, c, new Vector2(u1, v0));
            dst[n++] = new VertexPNCT(origin + a1, nrm, c, new Vector2(u0, v1));
            dst[n++] = new VertexPNCT(origin + b0, nrm, c, new Vector2(u1, v0));
            dst[n++] = new VertexPNCT(origin + b1, nrm, c, new Vector2(u1, v1));
            dst[n++] = new VertexPNCT(origin + a1, nrm, c, new Vector2(u0, v1));
        }

        var prev = Edge(0, out _);
        Vector3 midCenter = Vector3.Zero;
        Vector3 midSide = side, midNorm = faceN;
        for (int i = 1; i <= strips; i++)
        {
            var next = Edge(i, out _);
            var segDir = Vector3.Normalize(next - prev);
            var segNorm = Vector3.Normalize(Vector3.Cross(segDir, up));
            float u0 = (i - 1) * (w / strips), u1 = i * (w / strips);
            float shade = 1.0f - 0.06f * (i - 1);

            // Main cloth strip (front & back face with slight thickness so both sides cel-shade cleanly)
            var tA = prev; var tB = next;
            var mA = prev - up * (h - trimH); var mB = next - up * (h - trimH);
            var bA = prev - up * h; var bB = next - up * h;

            AddQuad(tA + segNorm * 0.012f, tB + segNorm * 0.012f, mA + segNorm * 0.012f, mB + segNorm * 0.012f, segNorm, clothCol, shade, clothAlpha, u0, u1, 0f, h - trimH);
            AddQuad(tB - segNorm * 0.012f, tA - segNorm * 0.012f, mB - segNorm * 0.012f, mA - segNorm * 0.012f, -segNorm, clothCol, shade * 0.94f, clothAlpha, u1, u0, 0f, h - trimH);

            // Bottom heraldic trim border
            AddQuad(mA + segNorm * 0.013f, mB + segNorm * 0.013f, bA + segNorm * 0.013f, bB + segNorm * 0.013f, segNorm, trimCol, shade, trimAlpha, u0, u1, h - trimH, h);
            AddQuad(mB - segNorm * 0.013f, mA - segNorm * 0.013f, bB - segNorm * 0.013f, bA - segNorm * 0.013f, -segNorm, trimCol, shade * 0.94f, trimAlpha, u1, u0, h - trimH, h);

            if (fringe) // ten battles: a gold fringe under the trim (docs/archive/COMPANY.md C6)
            {
                // A dark seam, then the fringe in tassels of two lengths, so it reads apart from the trim.
                var sA = bA - up * 0.02f; var sB = bB - up * 0.02f;
                AddQuad(bA + segNorm * 0.014f, bB + segNorm * 0.014f, sA + segNorm * 0.014f, sB + segNorm * 0.014f, segNorm, FringeSeam, shade, trimAlpha, u0, u1, h - trimH, h);
                AddQuad(bB - segNorm * 0.014f, bA - segNorm * 0.014f, sB - segNorm * 0.014f, sA - segNorm * 0.014f, -segNorm, FringeSeam, shade * 0.94f, trimAlpha, u1, u0, h - trimH, h);
                float tassel = i % 2 == 0 ? 0.13f : 0.09f;
                var fA = sA - up * tassel; var fB = sB - up * tassel;
                AddQuad(sA + segNorm * 0.014f, sB + segNorm * 0.014f, fA + segNorm * 0.014f, fB + segNorm * 0.014f, segNorm, HonourGold, shade, trimAlpha, u0, u1, h - trimH, h);
                AddQuad(sB - segNorm * 0.014f, sA - segNorm * 0.014f, fB - segNorm * 0.014f, fA - segNorm * 0.014f, -segNorm, HonourGold, shade * 0.94f, trimAlpha, u1, u0, h - trimH, h);
            }
            if (i == 2)
            {
                midCenter = (tA + tB + mA + mB) * 0.25f;
                midSide = segDir;
                midNorm = segNorm;
            }
            prev = next;
        }
        // Battle honours (C6): up to three gold streamers from under the finial on the far side of the pole, a little apart,
        // hanging and lifting with the wind as the cloth does.
        for (int j = 0; j < Math.Min(3, streamers); j++)
        {
            const int segs = 4;
            const float sw = 0.06f;
            float len = 0.8f - j * 0.16f;
            Vector3 At(int k)
            {
                float f = k / (float)segs, ph = wave * (1.6f + stream) - f * 3.4f - j * 0.9f;
                float outward = (0.03f + 0.5f * stream) * f;
                return up * 3.12f - side * (0.05f + j * 0.15f + outward) - up * (len * f * (1 - 0.6f * stream)) + faceN * MathF.Sin(ph) * 0.06f * f;
            }
            var p0 = At(0);
            for (int k = 1; k <= segs; k++)
            {
                var p1 = At(k);
                float shade = 1f - 0.05f * k;
                // A ribbon, its width across the pole's side, both faces.
                AddQuad(p0 + faceN * 0.01f, p0 + faceN * 0.01f - side * sw, p1 + faceN * 0.01f, p1 + faceN * 0.01f - side * sw, faceN, HonourGold, shade * 1.15f, trimAlpha, 0, 1, 0, 1);
                AddQuad(p0 - side * sw - faceN * 0.01f, p0 - faceN * 0.01f, p1 - side * sw - faceN * 0.01f, p1 - faceN * 0.01f, -faceN, HonourGold, shade * 0.9f, trimAlpha, 0, 1, 0, 1);
                p0 = p1;
            }
        }
        verts.N = n;

        // 3. 3D Heraldic Emblem mounted on both faces of the banner cloth!
        var emb = EmblemOf(palIdx, charge);
        if (emb.Length > 0)
        {
            EmitLocal(emb, midCenter + midNorm * 0.016f, midSide, up, midNorm, 0.85f, 0.82f, 0.85f);
            EmitLocal(emb, midCenter - midNorm * 0.016f, -midSide, up, -midNorm, 0.85f, 0.82f, 0.85f);
        }
    }

    /// Crisp 3D arrow, crossbow bolt, or javelin in flight or embedded in the terrain on the unified pixel grid.
    public void DrawMissile(Vector3 head, Vector3 tail, Grip kind, int palette)
    {
        var diff = head - tail;
        float len = diff.Length();
        if (len < 1e-4f) return;
        int palIdx = Math.Clamp(palette, 0, Style.Sides.Length - 1);
        UnitMatTable.Fill(palIdx, -1, 0, false, Color.White, palCols);
        WorldFrame(tail);
        var dir = diff / len;
        if (kind == Grip.Thrown && kit.Javelin.Length > 0)
            EmitDir(kit.Javelin, dir * (len * 0.42f), dir, 1.05f, len / 1.30f, 1.05f);
        else if (kit.Arrow.Length > 0)
        {
            float thick = kind == Grip.Crossbow ? 1.25f : 1.0f;
            EmitDir(kit.Arrow, Vector3.Zero, dir, thick, len / 0.74f, thick);
        }
    }

    static float HorseScale(string barding) => barding switch
    {
        "steppetack" or "camel" => 1.08f,
        "saddle" or "halfbarding" or "furbarding" => 1.18f,
        "scalebarding" or "gildedcaparison" => 1.32f,
        _ => 1.26f,
    };

    /// Two-bone leg: where the middle joint of a leg from `hip` to `hoof` sits (segment lengths `upper`, `lower`). A
    /// foreleg's knee bends forward of the line (the cannon folds back under it); a hind leg's hock points back.
    static Vector3 Joint(Vector3 hip, Vector3 hoof, float upper, float lower, bool front)
    {
        var d = hoof - hip;
        float len = d.Length();
        if (len < 1e-4f) return hip + new Vector3(0, -upper, 0);
        len = MathF.Min(len, (upper + lower) * 0.999f);
        var dir = Vector3.Normalize(d);
        float cosA = Math.Clamp((upper * upper + len * len - lower * lower) / (2 * upper * len), -1, 1);
        float sinA = MathF.Sqrt(1 - cosA * cosA);
        var side = Vector3.Normalize(new Vector3(0, dir.Z, -dir.Y)); // in the leg's plane, square to the line; +Z when the leg hangs down
        if (!front) side = -side;
        return hip + dir * (upper * cosA) + side * (upper * sinA);
    }

    float DrawHorse3D(Vector2 pos, float ground, float heading, float anim, float speed, string barding, float idle = -1, float turnDelta = 0f)
    {
        float hScale = HorseScale(barding);
        Frame(pos, heading, ground, Scale * hScale);

        float gait = Math.Clamp(speed / 9f, 0, 1);
        float trot = Math.Clamp(speed / 2.2f, 0, 1);
        float horseBank = Math.Clamp(turnDelta * (8f + 14f * gait), -14f, 14f);
        if (Rear > 0)
        {
            Lean(horseBank * 0.3f, -RearDeg * Rear);
            origin.Y += MathF.Sin(Rad(RearDeg * Rear)) * 0.7f * Scale * hScale;
        }
        else if (MathF.Abs(horseBank) > 0.2f)
        {
            Lean(horseBank, 0f);
        }

        bool camel = barding == "camel";
        bool scaleB = barding == "scalebarding";
        bool furB = barding == "furbarding";
        bool barded = barding is "barding" or "halfbarding" or "gildedcaparison";

        var bodyMesh = camel ? kit.CamelBody : scaleB ? kit.HorseBodyScale : furB ? kit.HorseBodyFur : barded ? kit.HorseBodyBarded : kit.HorseBody;
        var neckMesh = camel ? kit.CamelNeck : scaleB ? kit.HorseNeckScale : barded ? kit.HorseNeckBarded : kit.HorseNeck;
        var headMesh = camel ? kit.CamelHead : scaleB ? kit.HorseHeadScale : barded ? kit.HorseHeadBarded : kit.HorseHead;
        byte bodyPat = scaleB ? MeshKit3D.PatScale : barded ? MeshKit3D.PatQuilt : MeshKit3D.PatSmooth;

        // Fuller gallop suspension + rocking pitch across the barrel!
        float susp = MathF.Max(0f, MathF.Sin(anim * MathF.Tau - 0.25f)) * 0.055f * gait;
        float bob = (MathF.Sin(anim * MathF.Tau) * 0.048f + MathF.Sin(anim * MathF.Tau * 2) * 0.014f) * gait + susp;
        float rock = MathF.Sin(anim * MathF.Tau - 0.55f) * 0.072f * gait;
        float y = 0.85f + bob;
        bool still = speed < 0.3f && idle >= 0;
        float shift = still ? MathF.Sin(Now * 0.65f + idle * 6.28f) * 0.014f : 0;
        float nod = still ? MathF.Max(0, MathF.Sin(Now * 0.55f + idle * 11f) - 0.72f) * 0.30f * MathF.Sin(Now * 4.5f) : 0;
        float paw = still ? MathF.Max(0, MathF.Sin(Now * 0.45f + idle * 17f) - 0.86f) * 7f : 0;

        // The gait (front left, front right, hind left, hind right): a four-beat walk, a trot on diagonal pairs, a gallop
        // led by the hinds. Each hoof is placed, not swung: on the ground it stays put while the body passes over it (it
        // sweeps back under the horse), then it lifts, the joint folds and it reaches forward for the next step. The knee
        // and hock are solved from where the hoof is, so a leg bends as a leg does instead of swinging stiff from the hip.
        float walkK = 1 - Math.Clamp((speed - 1.8f) / 1.6f, 0, 1), gallopK = Math.Clamp((speed - 5.5f) / 2.5f, 0, 1), trotK = 1 - walkK - gallopK;
        ReadOnlySpan<float> walkP = stackalloc float[] { 0.25f, 0.75f, 0f, 0.5f };
        ReadOnlySpan<float> trotP = stackalloc float[] { 0f, 0.5f, 0.5f, 0f };
        ReadOnlySpan<float> gallopP = stackalloc float[] { 0.42f, 0.52f, 0f, 0.1f };
        float duty = 0.62f * walkK + 0.45f * trotK + 0.36f * gallopK;      // share of the stride a hoof is on the ground
        float sweep = speed < 0.3f ? 0 : 0.17f * walkK + 0.25f * trotK + 0.31f * gallopK; // half the hoof's travel, horse units
        sweep *= Math.Clamp(speed / 1.2f, 0, 1);
        const float Upper = 0.41f, Lower = 0.41f, Sole = 0.02f;

        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1f : 1f;
            // The top of the leg sits up inside the barrel and is drawn thick (forearm, gaskin), so it grows out of the body.
            var hip = new Vector3(sd * 0.19f + shift, y - 0.06f + (front ? rock : -rock), front ? 0.56f : -0.58f);
            Vector3 hoof;
            if ((front && Rear > 0) || (i == 0 && paw > 0))
            {
                // Rearing forelegs paw the air; a standing horse paws the ground.
                float a = front && Rear > 0 ? Rad(58 * Rear) : Rad(20 * paw), fold = front && Rear > 0 ? 0.9f * Rear : 0.4f * paw;
                var k0 = hip + new Vector3(0, -Upper * MathF.Cos(a), Upper * MathF.Sin(a));
                hoof = k0 + new Vector3(0, -Lower * MathF.Cos(a - fold), Lower * MathF.Sin(a - fold));
            }
            else
            {
                float ph = anim + walkP[i] * walkK + trotP[i] * trotK + gallopP[i] * gallopK;
                ph -= MathF.Floor(ph);
                float stand = (front ? 0.04f : -0.06f); // where the hoof stands under its hip (a hind hoof a little behind it)
                if (ph < duty || sweep <= 0)
                {
                    float k = duty > 0 ? ph / duty : 0;
                    hoof = new Vector3(hip.X, Sole, hip.Z + stand + MathHelper.Lerp(sweep, -sweep, k)); // planted: the body moves over it
                }
                else
                {
                    float k = (ph - duty) / (1 - duty);
                    float e = k * k * (3 - 2 * k); // the swing gathers and slows into the next footfall
                    float lift = (front ? 0.10f + 0.16f * gallopK + 0.06f * trotK : 0.07f + 0.08f * gallopK + 0.04f * trotK) * MathF.Sin(k * MathF.PI);
                    hoof = new Vector3(hip.X, Sole + lift, hip.Z + stand + MathHelper.Lerp(-sweep, sweep, e) - (front ? 0.05f : 0.02f) * MathF.Sin(k * MathF.PI));
                }
            }
            var knee = Joint(hip, hoof, Upper, Lower, front);
            if (Detail == 0)
            {
                var into = Vector3.Normalize(knee - hoof) * 0.05f; // the cannon starts inside the forearm: no gap at the joint
                EmitBone(kit.HorseLegLow, knee + into, hoof, Vector3.UnitZ);
                EmitBone(kit.HorseLegUp, hip, knee, Vector3.UnitZ, front ? 1.25f : 1.4f);
            }
            else
            {
                EmitBone(kit.HorseLegLow, hip, hoof, Vector3.UnitZ);
            }
        }

        // Pitch the horse barrel smoothly with its stride rock!
        float bodyPitchDeg = -rock * 70f;
        Vector3 bU = RotX(Vector3.UnitY, bodyPitchDeg);
        Vector3 bF = RotX(Vector3.UnitZ, bodyPitchDeg);
        EmitLocal(bodyMesh, new Vector3(shift, y, 0), Vector3.UnitX, bU, bF, 1f, 1f, 1f, bodyPat);

        float neckPump = MathF.Sin(anim * MathF.Tau - 0.85f) * 0.085f * gait;
        float neckOut = gait * 0.19f + neckPump;
        float neckDrop = gait * 0.08f + neckPump * 0.45f;
        var neckBase = new Vector3(shift, y + rock + (camel ? 0.02f : 0.14f), 0.72f);
        var poll = new Vector3(0, y + rock + (camel ? 0.88f : 0.74f) + nod - neckDrop, (camel ? 1.24f : 1.16f) + neckOut);
        EmitBone(neckMesh, neckBase, poll, Vector3.UnitZ, 1f, bodyPat);

        var muzzle = new Vector3(0, y + rock + (camel ? 0.62f : 0.36f) + nod * 1.5f - neckDrop * 1.35f, (camel ? 1.62f : 1.52f) + neckOut * 1.45f);
        Vector3 headFwd = Vector3.Normalize(muzzle - poll);
        Vector3 headUp = Vector3.Normalize(Vector3.Cross(headFwd, Vector3.UnitX));
        if (headUp.Y < 0) headUp = -headUp;
        EmitLocal(headMesh, poll, Vector3.UnitX, headUp, headFwd);

        if (Detail == 0)
        {
            float swish = MathF.Sin(Now * 2.1f + pos.X * 0.7f) * 0.09f * (1 - gait)
                + MathF.Sin(anim * MathF.Tau) * 0.13f * gait
                - Math.Clamp(turnDelta * 0.25f, -0.25f, 0.25f);
            float tailLift = 0.82f + bob - rock + gait * 0.56f + MathF.Sin(anim * MathF.Tau - 1.1f) * 0.08f * gait;
            EmitBone(kit.HorseTail, new Vector3(0, y - rock + 0.18f, -0.88f), new Vector3(swish, tailLift, -1.18f - gait * 0.58f), Vector3.UnitZ);
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
