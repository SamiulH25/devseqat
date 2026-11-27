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
    /// Continuous 0..1 detail slider (1.0 = full hero geometry & all accessories, 0.0 = minimal animated silhouette).
    public float Lod = 1f;
    /// Legacy tier bridge for callers that still set Detail = 0/1/2.
    public int Detail
    {
        get => Lod >= 0.65f ? 0 : Lod >= 0.14f ? 1 : 2;
        set => Lod = value <= 0 ? 1f : value == 1 ? 0.44f : 0.08f;
    }
    public float Scale = 1f;
    /// Bypass per-soldier Variety randomization (used by showcase sheets to inspect exact Look meshes).
    public bool ExactLook;
    /// Optional 2D Puppet reference for sharing soft-particle swing glints and active Awe sources.
    public Puppet? FxPuppet;
    /// Shadow-pass fast path: skips normal transforms, palette lookups, patterns, and slash glints.
    public bool ShadowOnly;
    /// Defer soft-particle weapon glints into PendingHaze so TryDraw is 100% thread-safe across Parallel.For workers.
    public bool DeferHaze;
    /// Optional GPU hardware instancing target: when set, EmitLocal emits 64-byte instances instead of transforming vertices on CPU.
    public MeshAtlas3D? Atlas;
    public InstanceBatch? InstBatch;
    bool palDirty = true;
    public readonly List<(Vector3 Pos, float Size, Color Col, float Alpha, float Seed)> PendingHaze = new();

    const float RearDeg = 38f, Saddle = 0.62f, Shank = 0.41f, UpperArm = 0.305f, Forearm = 0.295f;
    // Heroic athletic humanoid proportions (slimmer tapered torso, longer legs, tighter shoulders, proportional head).
    const float HipY = 0.82f, HipHalfX = 0.115f, WaistY = 0.90f;
    const float TorsoScaleX = 0.84f, TorsoScaleY = 0.96f, TorsoScaleZ = 0.88f, TorsoLiftY = 0.16f;
    const float HeadScale = 0.88f, HeadY = 1.595f, ShoulderX = 0.208f, ShoulderY = 1.355f;
    const float LegRadial = 0.86f, ArmRadial = 0.85f;

    Vector3 origin, fwd, right;
    Vector3 camRight = Vector3.UnitX;
    float scale, tilt, tiltX, tiltZ, tiltCos = 1f, tiltSin, twist;
    float unitIdHash;

    static readonly byte[] PatAlpha = BuildPatAlpha();
    static byte[] BuildPatAlpha()
    {
        var table = new byte[16 * UnitMatTable.Slots];
        var metalQ = UnitMatTable.MetalQ;
        for (int p = 0; p < 16; p++)
            for (int m = 0; m < UnitMatTable.Slots; m++)
            {
                byte pat = 0;
                if (p == MeshKit3D.PatMail && m == 5) pat = MeshKit3D.PatMail;
                else if (p == MeshKit3D.PatScale && (m == 4 || m == 5 || m == 6 || m == 7)) pat = MeshKit3D.PatScale;
                else if (p == MeshKit3D.PatQuilt && (m == 1 || m == 2)) pat = MeshKit3D.PatQuilt;
                else if (p == MeshKit3D.PatClothCheck && (m == 1 || m == 2)) pat = MeshKit3D.PatClothCheck;
                else if (p == MeshKit3D.PatFur && (m == 10 || m == 11 || m == 12 || m == 13)) pat = MeshKit3D.PatFur;
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
        if (Lod < 0.95f) part = kit.ForLod(part, Lod);
        int len = part.Length;
        if (len == 0) return;

        Vector3 wPos = W(pos);
        Vector3 nR = WDir(rx), nU = WDir(uy), nF = WDir(fz);
        float sRx = scale * sx, sUy = scale * sy, sFz = scale * sz;
        float wRx = nR.X * sRx, wRy = nR.Y * sRx, wRz = nR.Z * sRx;
        float wUx = nU.X * sUy, wUy = nU.Y * sUy, wUz = nU.Z * sUy;
        float wFx = nF.X * sFz, wFy = nF.Y * sFz, wFz = nF.Z * sFz;
        float oX = wPos.X, oY = wPos.Y, oZ = wPos.Z;

        if (Atlas != null && InstBatch != null && Atlas.TryGetPart(part, out int pid))
        {
            if (!ShadowOnly && palDirty) { InstBatch.PushPalette(palCols); palDirty = false; }
            InstBatch.Add(pid, new Instance3D
            {
                Row0 = new Vector4(wRx, wUx, wFx, oX),
                Row1 = new Vector4(wRy, wUy, wFy, oY),
                Row2 = new Vector4(wRz, wUz, wFz, oZ),
                Meta = new Vector4(ShadowOnly ? 0 : InstBatch.CurrentPalRow, partPat / 255f, 0f, 0f),
            });
            return;
        }

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

    static Vector3 LegIK(Vector3 hip, ref Vector3 foot, Vector3 pole)
    {
        var d = foot - hip;
        float len = d.Length(), most = (Shank + Shank) * 0.996f;
        if (len < 1e-4f) return hip + new Vector3(0, -Shank, 0.05f);
        var axis = d / len;
        if (len > most) { len = most; foot = hip + axis * len; }
        len = MathF.Max(len, 0.08f);
        float along = len * 0.5f;
        float outDist = MathF.Sqrt(MathF.Max(0f, Shank * Shank - along * along));
        var bend = pole - axis * Vector3.Dot(pole, axis);
        bend = bend.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(bend);
        return hip + axis * along + bend * outDist;
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

        int palIdx = u.Palette;
        Color tint = u.HitFlash > 0 ? new Color(255, 110, 110) : u.Routing ? new Color(165, 165, 165) : Color.White;
        if (u.Poison > 0 && u.HitFlash <= 0) tint = Color.Lerp(tint, new Color(150, 230, 110), 0.4f);
        unitIdHash = Ease.Hash(u.Index * 37 + 11);

        if (look.Beast || u.Type.Breed is "troll" or "ogre" or "giant") return DrawBeast3D(u, ground, tint, look);

        bool camel = u.Mounted && u.Horse?.Barding == "camel";
        if (!ShadowOnly) { UnitMatTable.Fill(palIdx, look.Visage, u.Coat, camel, tint, palCols, look.Face); palDirty = true; }

        float size = (u.Type.Class == UnitClass.Captain ? 1.12f : 1f) * u.Type.Size * Scale * Variety.Stature(u);

        // Shadow-map silhouette proxy (ShadowOnly && Lod < 0.14): ~68 tris keeps sun shadow passes fast while visible units always show their real gear.
        if (ShadowOnly && Lod < 0.14f && !u.Dead)
        {
            float spd = u.Mounted ? u.Speed : u.Vel.Length();
            float stride = spd > 0.3f ? MathF.Sin(u.Anim * MathF.PI) : 0f;
            float bob = spd > 0.3f ? MathF.Abs(stride) * 0.055f : 0f;
            float liftFar = ground;
            if (u.Mounted && u.Horse!.Barding != "wolf")
            {
                float hScale = HorseScale(u.Horse!.Barding);
                Frame(u.Pos, u.ViewHeading, ground + bob * 1.4f * Scale, Scale * hScale);
                EmitLocal(kit.LodHorse, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
                liftFar = ground + (Saddle * hScale + bob * 1.2f) * Scale;
            }
            else if (u.Mounted && u.Horse!.Barding == "wolf")
            {
                liftFar = ground + DrawWarg3D(u, ground, tint);
                if (!ShadowOnly) { UnitMatTable.Fill(palIdx, look.Visage, u.Coat, false, tint, palCols, look.Face); palDirty = true; }
            }
            Frame(u.Pos, u.ViewFacing, liftFar + (u.Mounted ? 0f : bob * size), size);
            if (!u.Mounted && spd > 0.3f) Lean(stride * 3.5f, Math.Clamp(spd * 2.5f, 0f, 10f));
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

        // Mounted melee cuts lean out over the stirrup toward the struck side; gallop barrel rock pitches rider smoothly.
        float saddleSwingLeanX = u.Mounted && u.Action is UnitAction.Windup or UnitAction.Active
            ? (u.Swing == AttackDir.Left ? -11f : u.Swing == AttackDir.Right ? 11f : 0f)
            : 0f;
        float saddleSwingLeanZ = u.Mounted && u.Action == UnitAction.Active ? 9f : 0f;
        float horseRockDeg = u.Mounted && Rear <= 0 ? MathF.Sin(u.Anim * MathF.Tau - 0.55f) * 3.8f * gallop : 0f;
        float extraLeanZ = gallopCrouch + saddleSwingLeanZ + horseRockDeg - hitRecoil * 4f - stagger * 4f + (p.ShieldUp ? 5f : 0f);
        float extraLeanX = bankX + saddleSwingLeanX + stagger * (phase > 0.5f ? 5f : -5f);
        // A rider sits on his horse: hips and legs go where the horse goes, and he turns from the waist toward where he
        // looks (the sim keeps that within MountTwist of the horse's heading; a couched lance points along the horse).
        float frameYaw = u.Mounted ? u.ViewHeading : u.ViewFacing;
        float seatTwist = u.Mounted && !u.Couched ? Math.Clamp(MathHelper.ToDegrees(MathHelper.WrapAngle(u.ViewFacing - u.ViewHeading)), -110f, 110f) : 0f;
        float totalLeanX = p.LeanX + extraLeanX;
        float totalLeanZ = p.LeanZ - leanBack + extraLeanZ;
        // Split lean between grounded lower body and articulated spine from the waist!
        // On foot, keep the feet planted flat (only tiny base lean while running/staggered) and bend through the waist.
        float baseLeanFrac = u.Mounted ? 0.55f : (u.Action == UnitAction.Stagger ? 0.30f : walkFactor * 0.22f);
        float spineRoll = Math.Clamp(totalLeanX * (1f - baseLeanFrac), -28f, 28f);
        float spinePitch = Math.Clamp(totalLeanZ * (1f - baseLeanFrac), -26f, 34f);
        Frame(at, frameYaw, lift + (p.Bob + stirrupBounce - (u.Mounted ? 0 : Sink(p))) * size, size);
        Lean(totalLeanX * baseLeanFrac, totalLeanZ * baseLeanFrac);

        // Spider web snare wrapping a caught unit.
        if (Now > 0 && Now < u.WebbedUntil && kit.WebSnare.Length > 0)
            EmitLocal(kit.WebSnare, new Vector3(0, u.Mounted ? 0.35f : 0f, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

        // 1. Semi-procedural directional foot-planting leg IK: feet plant and sweep along the actual local travel vector
        // (forward, diagonal, strafe, backpedal, or turn-in-place) so legs never moonwalk or ice-skate.
        float travelX = 0f, travelZ = 1f, dirBlend = 0f, travelPelvis = 0f;
        if (!u.Mounted && speed > 0.15f)
        {
            Vector2 vf = new(MathF.Cos(u.ViewFacing), MathF.Sin(u.ViewFacing)), vr = new(-vf.Y, vf.X);
            float vFwd = Vector2.Dot(u.Vel, vf), vSide = Vector2.Dot(u.Vel, vr);
            float inv = 1f / MathF.Max(speed, 1e-4f);
            travelX = vSide * inv;
            travelZ = vFwd * inv;
            dirBlend = Math.Clamp((speed - 0.18f) / 0.55f, 0f, 1f);
            // Align pelvis naturally toward forward-diagonal / strafe travel while keeping upper torso on target.
            float relAng = MathHelper.ToDegrees(MathF.Atan2(travelX, MathF.Abs(travelZ) + 0.15f));
            travelPelvis = Math.Clamp(relAng * (travelZ < -0.15f ? -0.45f : 0.62f), -32f, 32f) * dirBlend;
        }
        // Combat footwork & athletic contrapposto: coil hips on windup, lunge lead foot on active strike, brace on block.
        float aiStrikeK = (!u.IsPlayer && u.Action == UnitAction.Recover && u.Type.Recover > 0)
            ? Math.Clamp((1f - u.ActionTimer / u.Type.Recover) / 0.25f, 0f, 1f) : -1f;
        bool isStriking = u.Action == UnitAction.Active || (aiStrikeK >= 0f && aiStrikeK < 1f);
        float activeProg = u.Action == UnitAction.Active ? Math.Clamp(u.SwingProgress, 0f, 1f) : Math.Clamp(aiStrikeK, 0f, 1f);
        float swingSign = u.Swing == AttackDir.Left ? -1f : u.Swing == AttackDir.Right ? 1f : 0f;
        float combatPelvis = !u.Mounted ? (isStriking
            ? MathHelper.Lerp(swingSign * 14f, -swingSign * 16f, activeProg)
            : u.Action switch
            {
                UnitAction.Windup => swingSign * 16f,
                UnitAction.Block => -14f,
                _ => -6f * (1f - dirBlend), // subtle natural three-quarter contrapposto when standing
            }) : 0f;
        float pelvisYaw = !u.Mounted ? travelPelvis + combatPelvis - legNorm * (5.5f + 5.0f * runFactor) * (1f - 0.35f * MathF.Abs(travelX) * dirBlend) : 0f;
        float idleWeightShift = idleBreathOn ? MathF.Sin(Now * 0.55f + phase * MathF.Tau) * 0.014f + 0.012f : 0f;
        float hipShiftX = !u.Mounted ? legNorm * (0.016f + 0.014f * runFactor) + idleWeightShift : 0f;
        float stanceDrop = !u.Mounted && !p.Kneel ? (isStriking ? 0.052f : u.Action == UnitAction.Block ? 0.046f : u.Action == UnitAction.Windup ? 0.034f : 0.024f * (1f - dirBlend)) : 0f;
        if (!u.Mounted)
        {
            bool useProcedural = !p.Kneel && u.Action != UnitAction.Stagger && (dirBlend > 0.02f || MathF.Abs(turnDelta) > 0.12f);
            float sinkY = Sink(p);
            float duty = 0.58f - 0.16f * runFactor;
            bool backpedal = travelZ < -0.15f;
            float strideHalf = (0.27f + 0.17f * runFactor) * (backpedal ? 0.76f : 1f - 0.14f * MathF.Abs(travelX)) * dirBlend;
            float liftPeak = (0.095f + 0.14f * runFactor) * (backpedal ? 0.72f : 1f) * dirBlend;
            float turnStep = dirBlend < 0.25f && MathF.Abs(turnDelta) > 0.12f ? MathF.Min(1f, MathF.Abs(turnDelta) * 2.5f) * (1f - dirBlend * 4f) : 0f;
            float groundY = MathF.Max(0f, sinkY);

            for (int side = -1; side <= 1; side += 2)
            {
                float legSign = legNorm * side;
                var hip = RotY(new Vector3(side * HipHalfX, HipY - stanceDrop + legSign * 0.014f * walkFactor, 0), pelvisYaw) + new Vector3(hipShiftX, 0, 0);
                Vector3 knee, foot, toeFwd;
                if (useProcedural)
                {
                    float cyc = (u.Anim * 0.5f + (side > 0 ? 0f : 0.5f)) % 1f;
                    if (cyc < 0f) cyc += 1f;
                    float along, footLift, swingClear = 0f;
                    if (cyc < duty)
                    {
                        // Planted stance: foot sweeps backward along -travel so it stays pinned to the ground.
                        float k = cyc / duty;
                        along = MathHelper.Lerp(strideHalf, -strideHalf, k);
                        footLift = 0f;
                    }
                    else
                    {
                        // Swing phase: foot lifts and reaches forward along +travel for the next step.
                        float k = (cyc - duty) / (1f - duty);
                        float e = k * k * (3f - 2f * k);
                        along = MathHelper.Lerp(-strideHalf, strideHalf, e);
                        float arc = MathF.Sin(k * MathF.PI);
                        footLift = liftPeak * arc;
                        swingClear = side * 0.026f * arc * (0.4f + 0.6f * MathF.Abs(travelX));
                    }
                    // Turn-in-place step lift when pivoting on the spot.
                    if (turnStep > 0.01f)
                    {
                        float ts = MathF.Max(0f, MathF.Sin(Now * 11f + (side > 0 ? 0f : MathF.PI)));
                        footLift = MathF.Max(footLift, ts * 0.08f * turnStep);
                        along += MathF.Cos(Now * 11f + (side > 0 ? 0f : MathF.PI)) * 0.045f * turnStep;
                    }
                    float baseX = side * (HipHalfX + 0.008f) + swingClear;
                    float baseZ = p.ShieldUp ? (side < 0 ? 0.09f : -0.08f) : 0f;
                    foot = new Vector3(baseX + travelX * along, groundY + footLift, baseZ + travelZ * along);
                    var kneePole = RotY(new Vector3(side * 0.22f, 0.08f, 1f), pelvisYaw * 0.7f);
                    knee = LegIK(hip, ref foot, kneePole);
                    toeFwd = RotY(new Vector3(side * 0.24f, footLift > 0.02f ? 0.16f : 0f, 1f), pelvisYaw * 0.6f);
                }
                else if (!p.Kneel && u.Action != UnitAction.Stagger && p.Crouch < 0.35f)
                {
                    // Grounded athletic stance (contrapposto idle, combat lunge, or shield brace) solved via LegIK so feet stay flat!
                    float lungeZ = isStriking
                        ? (side < 0 ? 0.15f : -0.12f) * MathF.Sin(activeProg * MathF.PI * 0.85f + 0.15f)
                        : u.Action == UnitAction.Windup
                            ? (side < 0 ? 0.04f : -0.11f)
                            : u.Action == UnitAction.Block || p.ShieldUp
                                ? (side < 0 ? 0.12f : -0.10f)
                                : (side < 0 ? 0.075f : -0.065f);
                    float stanceX = side * (HipHalfX + (isStriking || u.Action == UnitAction.Block ? 0.034f : 0.016f));
                    foot = new Vector3(stanceX, groundY, lungeZ);
                    var kneePole = RotY(new Vector3(side * 0.28f, 0.06f, 1f), pelvisYaw * 0.65f);
                    knee = LegIK(hip, ref foot, kneePole);
                    toeFwd = RotY(new Vector3(side * (side < 0 ? 0.16f : 0.34f), 0f, 1f), pelvisYaw * 0.45f);
                }
                else
                {
                    var (a, bend) = Knee(side, p);
                    knee = hip + RotY(new Vector3(side * 0.015f, -Shank * MathF.Cos(Rad(a)), Shank * MathF.Sin(Rad(a))), pelvisYaw * 0.6f);
                    foot = knee + RotY(new Vector3(side * 0.018f, -Shank * MathF.Cos(Rad(a - bend)), Shank * MathF.Sin(Rad(a - bend))), pelvisYaw * 0.4f);
                    toeFwd = RotY(new Vector3(side * 0.22f, 0f, 1f), pelvisYaw * 0.4f);
                }
                if (Lod >= 0.36f)
                {
                    EmitBone(lm.Thigh, hip, knee, toeFwd, LegRadial, lm.LegPat);
                    EmitBone(lm.Shin, knee, foot, toeFwd, LegRadial, lm.LegPat);
                }
                else
                {
                    EmitBone(lm.Shin, hip, foot, Vector3.UnitZ, LegRadial, lm.LegPat);
                }
            }
        }
        else if (Lod >= 0.36f)
        {
            // Astride the barrel: the hips close together in the saddle, the thighs run out and forward over the horse's
            // sides, the knee grips just outside the barrel, and the shin hangs back along the flank to the stirrup.
            float barrel = 0.33f * HorseScale(u.Horse!.Barding) * Scale / size; // the barrel's half width in the rider's units
            float legTuck = gallop * 0.06f;
            float legBob = -stirrupBounce * 0.65f;
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * 0.11f, 0.68f, -0.16f - legTuck * 0.4f);
                var knee = new Vector3(side * (barrel + 0.07f), 0.34f + legBob + legTuck * 0.5f, 0.09f + legTuck);
                var foot = new Vector3(side * (barrel + 0.02f), 0.00f + legBob * 1.2f + legTuck * 0.7f, -0.09f - legTuck * 0.6f);
                EmitBone(lm.Thigh, hip, knee, Vector3.UnitZ, LegRadial, lm.LegPat);
                EmitBone(lm.Shin, knee, foot, Vector3.UnitZ, LegRadial, lm.LegPat);
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

        float mountOffsetY = u.Mounted ? -0.14f : -stanceDrop;
        // Articulated upper-body basis (pitch/roll/yaw around the waist pivot so feet stay flat on the ground!).
        static void Basis(float yawDeg, float pitchDeg, float rollDeg, out Vector3 rx, out Vector3 uy, out Vector3 fz)
        {
            rx = RotY(RotX(RotZ(Vector3.UnitX, -rollDeg), pitchDeg), yawDeg);
            uy = RotY(RotX(RotZ(Vector3.UnitY, -rollDeg), pitchDeg), yawDeg);
            fz = RotY(RotX(RotZ(Vector3.UnitZ, -rollDeg), pitchDeg), yawDeg);
        }
        Basis(twist, spinePitch, spineRoll, out var tR, out var tU, out var tF);
        float hipBlendYaw = MathHelper.Lerp(pelvisYaw, twist, 0.40f);
        Basis(hipBlendYaw, spinePitch * 0.35f, spineRoll * 0.35f, out var pelR, out var pelU, out var pelF);

        Vector3 waistPivot = new(hipShiftX * 0.75f, WaistY + mountOffsetY + breath * 0.008f, u.Mounted ? -0.14f : 0f);
        float waistInMesh = (WaistY - TorsoLiftY) / TorsoScaleY;
        Vector3 torsoOrigin = waistPivot - tU * (waistInMesh * TorsoScaleY);
        Vector3 pelvisOrigin = waistPivot - pelU * (waistInMesh * TorsoScaleY);
        Vector3 TorsoPoint(Vector3 local) => waistPivot + tR * local.X + tU * (local.Y - WaistY) + tF * local.Z;
        Vector3 torsoPos = new(hipShiftX * 0.75f, mountOffsetY + breath * 0.008f, u.Mounted ? -0.14f : 0f);
        float syB = TorsoScaleY * (1f + breath * 0.012f), szB = TorsoScaleZ * (1f + breath * 0.018f);
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
        static byte VaryPat(byte pat, int seed, int salt)
        {
            int h = Math.Abs(seed * 17 + salt * 31);
            return pat switch
            {
                MeshKit3D.PatBrushed => (h % 5) switch { 0 => MeshKit3D.PatBrushed, 1 => MeshKit3D.PatPlateFluted, 2 => MeshKit3D.PatPlateRiveted, 3 => MeshKit3D.PatPlateWorn, _ => MeshKit3D.PatSmooth },
                MeshKit3D.PatMail => (h & 1) == 0 ? MeshKit3D.PatMail : MeshKit3D.PatMailHeavy,
                MeshKit3D.PatScale => (h & 1) == 0 ? MeshKit3D.PatScale : MeshKit3D.PatScaleOrnate,
                MeshKit3D.PatQuilt => (h & 3) switch { 0 => MeshKit3D.PatQuilt, 1 => MeshKit3D.PatClothHerring, 2 => MeshKit3D.PatClothPadded, _ => MeshKit3D.PatClothCheck },
                _ => pat,
            };
        }
        bool varyTex = !ExactLook && !u.IsPlayer && !u.IsLeader && !u.Hero;
        int texSeed = varyTex ? Variety.Seed(u) : 0;
        byte chestPat = varyTex ? VaryPat(lm.ChestPat, texSeed, 1) : lm.ChestPat;
        byte skirtPat = varyTex ? VaryPat(lm.SkirtPat, texSeed, 2) : lm.SkirtPat;
        byte shldPat = varyTex ? VaryPat(lm.ShldPat, texSeed, 3) : lm.ShldPat;
        byte tabardPat = varyTex ? VaryPat(MeshKit3D.PatQuilt, texSeed, 4) : MeshKit3D.PatQuilt;
        EmitLocal(chestMesh, torsoOrigin, tR, tU, tF, TorsoScaleX, syB, szB, chestPat);
        if (skirtMesh.Length > 0 && Lod >= 0.24f)
            EmitLocal(skirtMesh, pelvisOrigin, pelR, pelU, pelF, TorsoScaleX * 1.02f, TorsoScaleY, TorsoScaleZ, skirtPat);
        if (shldMesh.Length > 0 && Lod >= 0.48f) EmitLocal(shldMesh, torsoOrigin, tR, tU, tF, TorsoScaleX, syB, szB, shldPat);
        if (lm.TabardPanel.Length > 0 && Lod >= 0.36f) EmitLocal(lm.TabardPanel, pelvisOrigin, pelR, pelU, pelF, TorsoScaleX, TorsoScaleY, TorsoScaleZ, tabardPat);
        if (beltMesh.Length > 0 && Lod >= 0.60f) EmitLocal(beltMesh, pelvisOrigin, pelR, pelU, pelF, TorsoScaleX, TorsoScaleY, szB);
        if (lm.HasTabard && Lod >= 0.72f)
        {
            var emb = EmblemOf(palIdx);
            if (emb.Length > 0)
                EmitLocal(emb, TorsoPoint(new Vector3(0, 1.03f * TorsoScaleY + TorsoLiftY, 0.186f * TorsoScaleZ)), tR, tU, tF, 0.48f, 0.48f, 0.65f);
        }
        if (lm.HasCape && Lod >= 0.60f)
        {
            // Secondary motion: speed-driven cape billow, flutter, and turn-delta whip lag around the shoulder pin!
            float capeSpeed = Math.Clamp(speed, 0f, 10f);
            float capePitch = Math.Clamp(capeSpeed * 2.2f + (u.Action == UnitAction.Recover ? 7f : 0f), 0f, 20f)
                + MathF.Sin(Now * (5.2f + capeSpeed * 1.25f) + phase * 11f) * (1.8f + capeSpeed * 0.55f);
            float capeYaw = twist + Math.Clamp(-MathHelper.ToDegrees(u.Mounted ? mountTurn : turnDelta) * 0.65f, -24f, 24f)
                + MathF.Sin(Now * 3.6f + phase * 7f) * (1.8f + capeSpeed * 0.55f);
            Vector3 cR = RotY(Vector3.UnitX, capeYaw);
            Vector3 cU = RotY(RotX(Vector3.UnitY, capePitch), capeYaw);
            Vector3 cF = RotY(RotX(Vector3.UnitZ, capePitch), capeYaw);
            Vector3 pin = TorsoPoint(new Vector3(0, 1.29f * TorsoScaleY + TorsoLiftY, -0.145f * TorsoScaleZ));
            Vector3 capeOrigin = pin - (cU * (1.29f * TorsoScaleY) + cF * (-0.145f * TorsoScaleZ));
            EmitLocal(kit.Cape, capeOrigin, cR, cU, cF, TorsoScaleX * 0.96f, TorsoScaleY, TorsoScaleZ, MeshKit3D.PatQuilt);
        }

        // 3. Head + Helm + Plume (with turn look-ahead, counter-stabilized pitch, hit recoil snap, and plume whip lag)
        float headYaw = p.Look + Math.Clamp(MathHelper.ToDegrees(turnDelta) * 0.45f, -22f, 22f) + (ranged && p.Aim ? seatTwist : (twist - seatTwist) * 0.4f + seatTwist);
        float headPitch = -hitRecoil * 18f - stagger * 10f + (p.ShieldUp ? 6f : 0f) - gallopCrouch * 0.45f - spinePitch * 0.25f;
        Vector3 hR = RotY(Vector3.UnitX, headYaw);
        Vector3 hU = RotY(RotX(Vector3.UnitY, headPitch), headYaw);
        Vector3 hF = RotY(RotX(Vector3.UnitZ, headPitch), headYaw);
        Vector3 headPos = TorsoPoint(new Vector3(0, HeadY + breath * 0.008f, 0.012f));
        if (!lm.FullEnclosedHelm)
        {
            if (lm.IsHumanFace && Lod >= 0.60f && kit.HeadJaw.Length == 3 && kit.HeadJaw[1].Length > 0)
                EmitHumanHead(look, lm, Variety.Seed(u), headPos, hR, hU, hF, HeadScale);
            else
                EmitLocal(lm.Head, headPos, hR, hU, hF, HeadScale, HeadScale, HeadScale);
        }
        if (lm.Helm.Length > 0)
            EmitLocal(lm.Helm, headPos, hR, hU, hF, HeadScale, HeadScale, HeadScale);
        if (lm.HasPlume && Lod >= 0.72f)
        {
            float pSpeed = Math.Clamp(speed, 0f, 10f);
            float pPitch = Math.Clamp(pSpeed * 3.6f, 0f, 30f) + MathF.Sin(Now * (7.2f + pSpeed * 1.5f) + phase * 13f) * (2.4f + pSpeed * 0.75f) - hitRecoil * 16f;
            float pYaw = headYaw + Math.Clamp(-MathHelper.ToDegrees(u.Mounted ? mountTurn : turnDelta) * 0.75f, -26f, 26f)
                + MathF.Sin(Now * 4.8f + phase * 9f) * (2.0f + pSpeed * 0.55f);
            Vector3 pR = RotY(Vector3.UnitX, pYaw);
            Vector3 pU = RotY(RotX(Vector3.UnitY, headPitch - pPitch), pYaw);
            Vector3 pF = RotY(RotX(Vector3.UnitZ, headPitch - pPitch), pYaw);
            Vector3 plumePos = headPos + hU * (0.24f * HeadScale) - hF * 0.01f;
            EmitLocal(kit.Plume, plumePos, pR, pU, pF, 0.80f, 0.80f, 0.80f);
        }

        // 4. Arms, Weapon, Shield, Quivers & Special Combat Stances
        Vector3 Shoulder(int side) => TorsoPoint(new Vector3(side * ShoulderX, ShoulderY + breath * 0.008f, 0));
        Vector3 ArmEnd(int side, float alphaDeg, float yawDeg, float reach)
        {
            var sh = Shoulder(side);
            float a = Rad(alphaDeg);
            return sh + RotY(new Vector3(0, -MathF.Cos(a), MathF.Sin(a)), yawDeg + twist) * (UpperArm + Forearm) * reach;
        }
        void DrawArm(int side, Vector3 hand, Vector3? customPole = null)
        {
            var sh = Shoulder(side);
            if (Lod >= 0.48f)
            {
                var pole = customPole ?? RotY(new Vector3(side * 0.55f, -0.45f, -0.72f), twist);
                var el = Elbow(sh, ref hand, pole);
                EmitBone(lm.UArm, sh, el, Vector3.UnitZ, ArmRadial, lm.LimbPat);
                EmitBone(lm.FArm, el, hand, Vector3.UnitZ, ArmRadial, lm.LimbPat);
            }
            else
            {
                EmitBone(lm.FArm, sh, hand, Vector3.UnitZ, ArmRadial, lm.LimbPat);
            }
        }

        // Crossbow spanning & aiming
        if (isCrossbow)
        {
            if (Detail == 0 && kit.Quiver.Length > 0)
                EmitLocal(kit.Quiver, TorsoPoint(new Vector3(-0.09f, 1.24f, -0.17f)), tR, tU, tF);
            if (u.Type.HasShield && lm.HasShield && Detail == 0 && look.Shield != "pavise")
                EmitShield(lm, palIdx, TorsoPoint(new Vector3(0.04f, 1.14f, -0.19f)), 180f + twist, 0, true);
            else if (u.Type.HasShield && lm.HasShield && look.Shield == "pavise" && Detail == 0)
                EmitShield(lm, palIdx, TorsoPoint(new Vector3(0.03f, 1.10f, -0.20f)), 180f + twist, u.StuckArrows, true);

            if (p.Haul >= 0)
            {
                // Spanning: prod down at feet, both hands hauling the bowstring up to the nut.
                var down = Vector3.Normalize(new Vector3(0, -1f, 0.08f));
                var butt = TorsoPoint(new Vector3(0.02f, 1.05f, 0.36f));
                float y = MathHelper.Lerp(0.46f, 0.94f, p.Haul);
                DrawArm(1, new Vector3(0.07f, y, 0.38f));
                DrawArm(-1, new Vector3(-0.06f, y, 0.38f));
                EmitDir(lm.Weapon, butt + down * 0.14f, down);
                return true;
            }

            var xDir = Vector3.Normalize(Vector3.Lerp(new Vector3(-0.22f, 0.12f, 0.96f), Vector3.UnitZ, p.Draw));
            var xAt = TorsoPoint(Vector3.Lerp(new Vector3(0.14f, 1.16f, 0.14f), new Vector3(0.11f, 1.44f, 0.08f), p.Draw));
            DrawArm(1, xAt + xDir * 0.12f);
            DrawArm(-1, xAt + xDir * 0.38f);
            EmitDir(lm.Weapon, xAt + xDir * 0.06f, xDir);
            if (p.Draw > 0.95f && u.Action != UnitAction.Recover && Detail == 0 && kit.Arrow.Length > 0)
                EmitDir(kit.Arrow, xAt + xDir * 0.24f + Vector3.Up * 0.04f, xDir, 1.15f, 0.62f, 1.15f);
            return true;
        }

        // Bow drawing & loose
        if (ranged && p.Aim)
        {
            if (Detail == 0 && kit.Quiver.Length > 0)
                EmitLocal(kit.Quiver, TorsoPoint(new Vector3(-0.09f, 1.24f, -0.17f)), tR, tU, tF);
            if (u.Type.HasShield && lm.HasShield && Detail == 0)
                EmitShield(lm, palIdx, TorsoPoint(new Vector3(0.04f, 1.14f, -0.19f)), 180f + twist, 0, true);

            var bowHand = Shoulder(-1) + RotY(Vector3.Normalize(new Vector3(0.10f, 0.08f, 1f)), -twist) * (UpperArm + Forearm) * 0.94f;
            DrawArm(-1, bowHand);
            EmitLocal(lm.Weapon.Length > 0 ? lm.Weapon : kit.Bow, bowHand, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

            bool loosed = u.Action == UnitAction.Recover;
            var cheek = TorsoPoint(new Vector3(0.06f, 1.46f, 0.06f));
            var nockStart = bowHand + new Vector3(0.02f, -0.01f, -0.22f);
            var drawHand = loosed ? cheek + new Vector3(0.08f, 0.02f, -0.12f) : Vector3.Lerp(nockStart, cheek, p.Draw);
            DrawArm(1, drawHand, new Vector3(0.65f, 0.15f + 0.5f * p.Draw, -1f));
            if (!loosed && Detail == 0 && kit.Arrow.Length > 0)
            {
                var arrowVec = bowHand - drawHand;
                var arrowDir = arrowVec.LengthSquared() > 1e-4f ? Vector3.Normalize(arrowVec) : Vector3.UnitZ;
                EmitDir(kit.Arrow, drawHand, arrowDir, 1f, 0.95f, 1f);
            }
            return true;
        }

        var wHand = ArmEnd(1, p.ArmA + (stagger > 0 ? 18f : 0f), p.Yaw - twist + (stagger > 0 ? 16f : 0f), p.Reach);
        var wDir = RotY(new Vector3(0, MathF.Sin(Rad(p.WeapB)), MathF.Cos(Rad(p.WeapB))), p.Yaw);
        if (wDir.LengthSquared() < 1e-4f) wDir = Vector3.UnitY;
        wDir.Normalize();

        // Tighter two-handed grip alignment: pull overly wide windup hands slightly toward torso center so both arms stay solidly on the hilt/shaft.
        bool poleForwardGrip = p.TwoHand && p.Armed && lm.OffHandForward && p.WeapB <= 85f;
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

        // When couched, tuck the weapon hand snugly under the right armpit instead of hanging at the idle side.
        if (u.Couched)
        {
            wHand = TorsoPoint(new Vector3(0.20f, 1.20f, 0.26f));
            DrawArm(1, wHand, new Vector3(0.65f, 0.12f, -0.85f));
        }
        else DrawArm(1, wHand);

        bool shieldArm = p.Armed && u.Type.HasShield && !ranged && !p.TwoHand && !u.Couched;
        float shieldStrideBob = !u.Mounted ? legNorm * (0.032f + 0.025f * runFactor) : 0f;
        Vector3 offHand = p.TwoHand && p.Armed
            ? offHandTarget
            : shieldArm
                ? (p.ShieldUp
                    ? TorsoPoint(new Vector3(-0.06f, 1.26f + hitRecoil * 0.06f, 0.35f - hitRecoil * 0.07f))
                    : TorsoPoint(new Vector3(-0.24f - stagger * 0.10f, 1.07f + p.OffA * 0.002f + MathF.Abs(shieldStrideBob) * 0.35f, 0.19f - shieldStrideBob - hitRecoil * 0.08f)))
                : ArmEnd(-1, p.OffA + stagger * 22f, -twist - stagger * 18f, p.OffReach);
        Vector3? offPole = p.TwoHand && p.Armed
            ? RotY(new Vector3(-0.68f, -0.42f, -0.45f), twist * 0.65f)
            : shieldArm && p.ShieldUp
                ? new Vector3(-0.75f, -0.55f, -0.35f)
                : null;
        DrawArm(-1, offHand, offPole);

        if (!p.Armed) return true; // routed man has thrown away his weapon and shield

        // Couched lance (heavy cavalry at gallop): tucked under right arm, angled slightly toward aim within +/-15 deg.
        if (u.Couched)
        {
            float aimRel = u.IsPlayer ? Math.Clamp(MathHelper.WrapAngle(u.ViewFacing - u.ViewHeading), -0.26f, 0.26f) : 0f;
            float rel = MathHelper.WrapAngle(u.ViewHeading + aimRel - frameYaw);
            var lanceDir = RotY(Vector3.Normalize(new Vector3(0, -0.08f, 1f)), MathHelper.ToDegrees(rel));
            var lanceMesh = lm.Weapon.Length > 0 ? lm.Weapon : kit.Get("weapon_lance");
            EmitDir(lanceMesh, wHand - lanceDir * 0.35f, lanceDir);
            if (u.Type.HasShield && lm.HasShield)
                EmitShield(lm, palIdx, offHand + new Vector3(-0.07f, 0.02f, 0.06f), -58f, u.StuckArrows, false);
            return true;
        }

        // Ranged unit marching / idle (not aiming)
        if (ranged)
        {
            if (Detail == 0 && kit.Quiver.Length > 0)
                EmitLocal(kit.Quiver, TorsoPoint(new Vector3(-0.09f, 1.24f, -0.17f)), tR, tU, tF);
            if (u.Type.HasShield && lm.HasShield && Detail == 0)
                EmitShield(lm, palIdx, TorsoPoint(new Vector3(0.04f, 1.14f, -0.19f)), 180f + twist, 0, true);
            EmitLocal(lm.Weapon.Length > 0 ? lm.Weapon : kit.Bow, offHand, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return true;
        }

        // Spare javelins strapped diagonally across skirmisher's back (not for throwing axes)
        if (grip == Grip.Thrown && u.Type.Look.Weapon != "throwing_axe" && u.Ammo > 1 && Detail == 0 && kit.Javelin.Length > 0)
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
            Vector3 edgeHint = (u.Action is UnitAction.Windup or UnitAction.Active or UnitAction.Recover)
                ? u.Swing switch
                {
                    AttackDir.Right => RotY(new Vector3(-0.96f, -0.26f, 0f), p.Yaw),
                    AttackDir.Left => RotY(new Vector3(0.96f, -0.26f, 0f), p.Yaw),
                    AttackDir.Thrust => lm.WeaponEdgeZ ? Vector3.Down : RotY(new Vector3(0.88f, -0.45f, 0f), p.Yaw),
                    _ => RotY(new Vector3(0f, -MathF.Cos(Rad(p.WeapB)), MathF.Sin(Rad(p.WeapB))), p.Yaw), // Overhead chop edge forward/down
                }
                : RotY(new Vector3(0.62f, -0.22f, 0.75f), p.Yaw);
            EmitWeapon(lm.Weapon, wHand, wDir, edgeHint, lm.WeaponEdgeZ);

            // Crisp 3D steel slash-arc ribbons (CR6: driven directly by UnitAction.Active SwingProgress 0..1).
            if (Detail == 0 && grip is Grip.One or Grip.Two or Grip.Pole)
            {
                float strikeK = u.Action == UnitAction.Active ? Math.Clamp(u.SwingProgress, 0f, 1f) : -1f;
                if (strikeK < 0f && !u.IsPlayer && u.Action == UnitAction.Recover && u.Type.Recover > 0)
                {
                    float rk = 1f - u.ActionTimer / u.Type.Recover;
                    // Only emit the slash ribbon during the actual sweep (not before the blade leaves the windup!)
                    if (rk >= 0.08f && rk <= 0.27f) strikeK = Math.Clamp((rk - 0.06f) / 0.21f, 0.12f, 0.98f);
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
            Vector3 shPos = p.ShieldUp ? offHand + new Vector3(0.04f, 0.02f, 0.11f) : offHand + new Vector3(-0.03f, 0.00f, 0.06f);
            float shYaw = p.ShieldUp ? (14f - hitRecoil * 16f) : (-34f + twist * 0.35f - stagger * 22f);
            float shPitch = p.ShieldUp ? (-12f + hitRecoil * 22f) : (-6f + runFactor * 12f + stagger * 18f);
            float shRoll = p.ShieldUp ? (8f - hitRecoil * 14f) : (6f - legNorm * 6f * walkFactor - stagger * 16f);
            EmitShield(lm, palIdx, shPos, shYaw, u.StuckArrows, false, shPitch, shRoll);
        }

        return true;
    }

    MeshKit3D.Vert[] EmblemOf(int palIdx, int chargeOverride = -1)
    {
        if (kit.Emblems.Length == 0) return Array.Empty<MeshKit3D.Vert>();
        Emblem e = chargeOverride >= 0 && chargeOverride < Style.EmblemCount
            ? (Emblem)chargeOverride
            : Style.Sides[Math.Clamp(palIdx, 0, Style.Sides.Length - 1)].Charge;
        int idx = (int)e;
        if (idx >= kit.Emblems.Length) idx = e switch
        {
            Emblem.Chevrons or Emblem.Pile => (int)Emblem.Chevron,
            Emblem.BendSinister => (int)Emblem.Bend,
            Emblem.Quartered or Emblem.Checky or Emblem.Gyronny => (int)Emblem.Cross,
            Emblem.PerPale or Emblem.Paly or Emblem.Sword or Emblem.Tower or Emblem.Axe => (int)Emblem.Pale,
            Emblem.PerFess or Emblem.Chief or Emblem.Barry or Emblem.Bordure => (int)Emblem.Fess,
            Emblem.Star or Emblem.Rose or Emblem.Crown or Emblem.Crescent or Emblem.Chalice => (int)Emblem.Sun,
            _ => (int)Emblem.Lozenge,
        };
        return idx >= 0 && idx < kit.Emblems.Length ? kit.Emblems[idx] : Array.Empty<MeshKit3D.Vert>();
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
        byte shieldPat = ((int)MathF.Round(pos.X * 13f + pos.Z * 7f) & 3) switch
        {
            0 => MeshKit3D.PatQuilt,
            1 => MeshKit3D.PatWoodPlank,
            2 => MeshKit3D.PatPlateRiveted,
            _ => MeshKit3D.PatClothCheck,
        };
        EmitLocal(lm.Shield, pos, sR, sU, sF, 1f, 1f, 1f, shieldPat);

        if (Detail <= 1 && lm.ShieldKind is not ("shield_rotella" or "shield_kalkan" or "shield_lantern_pavise" or "shield_tarn_kite"))
        {
            var emb = EmblemOf(palIdx);
            if (emb.Length > 0)
            {
                (float ey, float ez, float sx, float sy) = lm.ShieldKind switch
                {
                    "shield_pavise" or "shield_lantern_pavise" => (-0.02f, 0.080f, 1.15f, 1.55f),
                    "shield_tower" => (0.14f, 0.082f, 0.92f, 1.05f),
                    "shield_kite" or "shield_tarn_kite" => (0.04f, 0.070f, 0.86f, 1.05f),
                    "shield_round" => (0.00f, 0.068f, 0.85f, 0.85f),
                    "shield_kalkan" or "shield_sun_kalkan" => (0.00f, 0.095f, 0.72f, 0.72f),
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
    /// Compose a human head from portrait-aligned modular parts (jaw, eyes, lips, nose, brows, scar, hair cut, beard).
    void EmitHumanHead(Look look, MeshKit3D.LookMeshes lm, int seed, Vector3 pos, Vector3 rx, Vector3 uy, Vector3 fz, float hs = 1f)
    {
        int style = look.Visage >= 0 ? Variety.Parts(look.Visage).Style : 0;
        var feat = look.Features >= 0 ? Variety.UnpackFeatures(look.Features) : Variety.DeriveFeatures(seed, style);

        // Temporarily repurpose unused mount coat slots (11 = iris, 12 = lip/scar) for facial detail (zero allocations).
        Vector3 saved11 = palCols[11], saved12 = palCols[12];
        if (!ShadowOnly)
        {
            var tv = palCols[0];
            var eyeC = PortraitPainter.EyeColours[Math.Clamp(feat.Eye, 0, PortraitPainter.EyeColours.Length - 1)];
            palCols[11] = new Vector3(eyeC.R / 255f * tv.X, eyeC.G / 255f * tv.Y, eyeC.B / 255f * tv.Z);
            var skin = palCols[9];
            palCols[12] = Vector3.Lerp(skin * 0.62f, new Vector3(150f / 255f, 52f / 255f, 56f / 255f) * tv, 0.34f); palDirty = true;
        }

        var headMesh = kit.HeadJaw[Math.Clamp(feat.Jaw, 0, 2)];
        EmitLocal(headMesh.Length > 0 ? headMesh : lm.Head, pos, rx, uy, fz, hs, hs, hs);

        if (Lod >= 0.85f)
        {
            var nose = kit.NoseStyle[Math.Clamp(feat.Nose, 0, 2)];
            if (nose.Length > 0) EmitLocal(nose, pos, rx, uy, fz, hs, hs, hs);
            var brow = kit.BrowStyle[Math.Clamp(feat.Brow, 0, 2)];
            if (brow.Length > 0) EmitLocal(brow, pos, rx, uy, fz, hs, hs, hs);
            if (feat.Scar > 0 && feat.Scar < kit.ScarStyle.Length && kit.ScarStyle[feat.Scar].Length > 0)
            {
                if (!ShadowOnly)
                    palCols[12] = Vector3.Lerp(palCols[9], new Vector3(150f / 255f, 56f / 255f, 60f / 255f) * palCols[0], 0.70f); palDirty = true;
                EmitLocal(kit.ScarStyle[feat.Scar], pos, rx, uy, fz, hs, hs, hs);
            }
        }

        if (feat.Beard > 0 && feat.Beard < kit.BeardStyle.Length && kit.BeardStyle[feat.Beard].Length > 0)
            EmitLocal(kit.BeardStyle[feat.Beard], pos, rx, uy, fz, hs, hs, hs);

        if (lm.Helm.Length == 0)
        {
            int cut = Math.Clamp(feat.Cut, 0, 4);
            // Bald (4): young shaved heads stay bare; older/selected men keep the portrait's side fringe.
            bool emitHair = cut != 4 || feat.Age >= 30 || Ease.Hash(seed * 31 + 42 * 977) >= 0.6f;
            if (emitHair && kit.HairCut[cut].Length > 0)
                EmitLocal(kit.HairCut[cut], pos, rx, uy, fz, hs, hs, hs);
        }
        else if (feat.Cut == 1 && !lm.HoodOrCoif && kit.HairLongUnder.Length > 0)
        {
            // Long hair flows out cleanly from beneath open helms without crown clipping.
            EmitLocal(kit.HairLongUnder, pos, rx, uy, fz, hs, hs, hs);
        }

        palCols[11] = saved11;
        palCols[12] = saved12; palDirty = true;
    }

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

        if (ShadowOnly && Detail >= 2)
        {
            EmitLocal(kit.LodFoot, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return;
        }

        float kneeFold = crumple ? 85f * Ease.Out(k) : 52f * Ease.Out(k);
        float legSpread = 14f * Ease.Out(k);
        for (int side = -1; side <= 1; side += 2)
        {
            var hip = new Vector3(side * HipHalfX, HipY, 0);
            float thighA = Rad((crumple ? 35f * k : 14f * side * k));
            var knee = hip + new Vector3(side * 0.04f * k, -Shank * MathF.Cos(thighA), Shank * MathF.Sin(thighA));
            float shinA = thighA - Rad(kneeFold);
            var foot = knee + new Vector3(side * 0.03f * k, -Shank * MathF.Cos(shinA), Shank * MathF.Sin(shinA));
            if (Detail == 0)
            {
                EmitBone(lm.Thigh, hip, knee, Vector3.UnitZ, LegRadial, lm.LegPat);
                EmitBone(lm.Shin, knee, foot, Vector3.UnitZ, LegRadial, lm.LegPat);
            }
            else EmitBone(lm.Shin, hip, foot, Vector3.UnitZ, LegRadial, lm.LegPat);
        }

        var fallTorsoPos = new Vector3(0, TorsoLiftY, 0);
        EmitLocal(lm.Chest, fallTorsoPos, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, TorsoScaleX, TorsoScaleY, TorsoScaleZ, lm.ChestPat);
        if (lm.Skirt.Length > 0) EmitLocal(lm.Skirt, fallTorsoPos, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, TorsoScaleX * 1.02f, TorsoScaleY, TorsoScaleZ, lm.SkirtPat);
        if (Detail == 0)
        {
            if (lm.Shoulders.Length > 0) EmitLocal(lm.Shoulders, fallTorsoPos, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, TorsoScaleX, TorsoScaleY, TorsoScaleZ, lm.ShldPat);
            if (lm.TabardPanel.Length > 0) EmitLocal(lm.TabardPanel, fallTorsoPos, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, TorsoScaleX, TorsoScaleY, TorsoScaleZ, MeshKit3D.PatQuilt);
            if (lm.Belt.Length > 0) EmitLocal(lm.Belt, fallTorsoPos, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, TorsoScaleX, TorsoScaleY, TorsoScaleZ);
        }
        float hPitch = -22f * Ease.Out(k);
        Vector3 hU = RotX(Vector3.UnitY, hPitch), hF = RotX(Vector3.UnitZ, hPitch);
        if (!lm.FullEnclosedHelm && (Detail == 0 || lm.Helm.Length == 0))
        {
            if (lm.IsHumanFace && kit.HeadJaw.Length == 3 && kit.HeadJaw[1].Length > 0)
                EmitHumanHead(look, lm, Variety.Seed(u), new Vector3(0, HeadY, 0.01f), Vector3.UnitX, hU, hF, HeadScale);
            else
                EmitLocal(lm.Head, new Vector3(0, HeadY, 0.01f), Vector3.UnitX, hU, hF, HeadScale, HeadScale, HeadScale);
        }
        if (lm.Helm.Length > 0)
            EmitLocal(lm.Helm, new Vector3(0, HeadY, 0.01f), Vector3.UnitX, hU, hF, HeadScale, HeadScale, HeadScale);

        float limp = Ease.Out(k);
        var rSh = new Vector3(ShoulderX, ShoulderY, 0);
        var lSh = new Vector3(-ShoulderX, ShoulderY, 0);
        // Asymmetric relaxed arm collapse (one arm drops beside hip, the other folds across/beside ribs — never a stiff T-pose).
        var rHand = rSh + new Vector3(0.14f + 0.08f * limp, -0.38f - 0.08f * limp, 0.16f - 0.08f * limp);
        var lHand = lSh + new Vector3(-0.08f - 0.06f * limp, -0.34f - 0.04f * limp, 0.20f - 0.04f * limp);
        if (Detail == 0)
        {
            var rEl = Elbow(rSh, ref rHand, new Vector3(0.75f, -0.35f, -0.55f));
            var lEl = Elbow(lSh, ref lHand, new Vector3(-0.75f, -0.35f, -0.55f));
            EmitBone(lm.UArm, rSh, rEl, Vector3.UnitZ, ArmRadial, lm.LimbPat);
            EmitBone(lm.FArm, rEl, rHand, Vector3.UnitZ, ArmRadial, lm.LimbPat);
            EmitBone(lm.UArm, lSh, lEl, Vector3.UnitZ, ArmRadial, lm.LimbPat);
            EmitBone(lm.FArm, lEl, lHand, Vector3.UnitZ, ArmRadial, lm.LimbPat);
        }
        else
        {
            EmitBone(lm.FArm, rSh, rHand, Vector3.UnitZ, ArmRadial, lm.LimbPat);
            EmitBone(lm.FArm, lSh, lHand, Vector3.UnitZ, ArmRadial, lm.LimbPat);
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
        UnitMatTable.Fill(palIdx, -1, 0, false, Color.White, palCols); palDirty = true;
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
        UnitMatTable.Fill(palIdx, -1, 0, false, Color.White, palCols); palDirty = true;
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
        "scalebarding" or "mailbarding" or "lamellarbarding" or "gildedcaparison" => 1.30f,
        _ => 1.25f,
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
        bool scaleB = barding is "scalebarding" or "mailbarding" or "lamellarbarding";
        bool halfB = barding == "halfbarding";
        bool furB = barding == "furbarding";
        bool barded = barding is "barding" or "caparison" or "gildedcaparison" or "partibarding" or "crossbarding" or "chequybarding" or "tasselled";

        var bodyMesh = camel ? kit.CamelBody
            : scaleB ? kit.HorseBodyScale
            : halfB ? kit.HorseBodyHalf
            : furB ? kit.HorseBodyFur
            : barded ? kit.HorseBodyBarded
            : kit.HorseBody;
        var neckMesh = camel ? kit.CamelNeck
            : scaleB ? kit.HorseNeckScale
            : (barded || halfB) ? kit.HorseNeckBarded
            : kit.HorseNeck;
        var headMesh = camel ? kit.CamelHead
            : scaleB ? kit.HorseHeadScale
            : (barded || halfB) ? kit.HorseHeadBarded
            : kit.HorseHead;
        byte bodyPat = barding == "mailbarding" ? MeshKit3D.PatMail
            : scaleB ? MeshKit3D.PatScale
            : (barded || halfB) ? MeshKit3D.PatQuilt
            : MeshKit3D.PatSmooth;

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
        const float FrontUpper = 0.40f, FrontLower = 0.41f, HindUpper = 0.43f, HindLower = 0.42f, Sole = 0.02f;

        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1f : 1f;
            float upper = front ? FrontUpper : HindUpper;
            float lower = front ? FrontLower : HindLower;
            // The top of the leg sits up inside the barrel (forearm in front, muscular haunch behind).
            var hip = new Vector3(sd * 0.19f + shift, y - (front ? 0.04f : 0.02f) + (front ? rock : -rock), front ? 0.56f : -0.56f);
            Vector3 hoof;
            bool planted = false;
            if ((front && Rear > 0) || (i == 0 && paw > 0))
            {
                // Rearing forelegs paw the air; a standing horse paws the ground.
                float a = front && Rear > 0 ? Rad(58 * Rear) : Rad(20 * paw), fold = front && Rear > 0 ? 0.9f * Rear : 0.4f * paw;
                var k0 = hip + new Vector3(0, -upper * MathF.Cos(a), upper * MathF.Sin(a));
                hoof = k0 + new Vector3(0, -lower * MathF.Cos(a - fold), lower * MathF.Sin(a - fold));
            }
            else
            {
                float ph = anim + walkP[i] * walkK + trotP[i] * trotK + gallopP[i] * gallopK;
                ph -= MathF.Floor(ph);
                float stand = front ? 0.02f : -0.08f; // standing forelegs straight under shoulder; hind hoof slightly back
                if (ph < duty || sweep <= 0)
                {
                    planted = true;
                    float k = duty > 0 ? ph / duty : 0;
                    hoof = new Vector3(hip.X, Sole, hip.Z + stand + MathHelper.Lerp(sweep, -sweep, k)); // planted: the body moves over it
                }
                else
                {
                    float k = (ph - duty) / (1 - duty);
                    float e = k * k * (3 - 2 * k); // the swing gathers and slows into the next footfall
                    float lift = (front ? 0.11f + 0.18f * gallopK + 0.07f * trotK : 0.08f + 0.10f * gallopK + 0.05f * trotK) * MathF.Sin(k * MathF.PI);
                    hoof = new Vector3(hip.X, Sole + lift, hip.Z + stand + MathHelper.Lerp(-sweep, sweep, e) - (front ? 0.05f : 0.02f) * MathF.Sin(k * MathF.PI));
                }
            }
            // Standing/planted forelegs lock nearly straight under weight; swinging forelegs fold cleanly at the knee.
            var knee = Joint(hip, hoof, upper, lower, front);
            if (front && planted && sweep < 0.05f)
                knee = Vector3.Lerp(hip, hoof, upper / (upper + lower)) + new Vector3(0, 0, 0.015f);
            if (Detail == 0)
            {
                var into = Vector3.Normalize(knee - hoof) * 0.05f; // the cannon starts inside the forearm/gaskin: no gap at the joint
                EmitBone(kit.HorseLegLow, knee + into, hoof, Vector3.UnitZ);
                EmitBone(front ? kit.HorseLegUp : kit.HorseLegHindUp, hip, knee, Vector3.UnitZ, front ? 1.05f : 1.0f);
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
        float neckOut = gait * 0.22f + neckPump;
        float neckDrop = gait * 0.09f + neckPump * 0.45f;
        float headTurnX = Math.Clamp(turnDelta * 0.22f, -0.22f, 0.22f); // horse looks into turns
        var neckBase = new Vector3(shift, y + rock + (camel ? 0.02f : 0.14f), 0.72f);
        var poll = new Vector3(headTurnX * 0.55f, y + rock + (camel ? 0.90f : 0.78f) + nod - neckDrop, (camel ? 1.24f : 1.12f) + neckOut);
        EmitBone(neckMesh, neckBase, poll, Vector3.UnitZ, 1f, bodyPat);

        var muzzle = new Vector3(headTurnX * 1.25f, y + rock + (camel ? 0.68f : 0.48f) + nod * 1.4f - neckDrop * 1.2f, (camel ? 1.64f : 1.56f) + neckOut * 1.38f);
        Vector3 headFwd = Vector3.Normalize(muzzle - poll);
        Vector3 headRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, headFwd));
        Vector3 headUp = Vector3.Normalize(Vector3.Cross(headFwd, headRight));
        EmitLocal(headMesh, poll, headRight, headUp, headFwd);

        if (Detail == 0)
        {
            float swish = MathF.Sin(Now * 2.1f + pos.X * 0.7f) * 0.09f * (1 - gait)
                + MathF.Sin(anim * MathF.Tau) * 0.13f * gait
                - Math.Clamp(turnDelta * 0.28f, -0.28f, 0.28f);
            // Natural hanging tail at rest, streaming gracefully back & slightly lifted at trot/gallop.
            var dock = new Vector3(0, y - rock + 0.16f, -0.84f);
            float tailTipY = y - rock - 0.50f + gait * 0.44f + MathF.Sin(anim * MathF.Tau - 1.1f) * 0.07f * gait;
            float tailTipZ = -1.08f - gait * 0.62f;
            EmitBone(kit.HorseTail, dock, new Vector3(swish, tailTipY, tailTipZ), Vector3.UnitZ);
        }

        return (Saddle + bob) * hScale * Scale;
    }

    public void DrawLooseHorse(LooseHorse h, float ground)
    {
        if (h.Mount.Barding == "wolf") return;
        UnitMatTable.Fill(h.Team, -1, h.Coat, h.Mount.Barding == "camel", Color.White, palCols); palDirty = true;
        unitIdHash = Ease.Hash((int)(h.Pos.X * 13 + h.Pos.Y * 29));
        if (ShadowOnly && Detail >= 2)
        {
            Frame(h.Pos, h.Heading, ground, Scale * HorseScale(h.Mount.Barding));
            EmitLocal(kit.LodHorse, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return;
        }
        DrawHorse3D(h.Pos, ground, h.Heading, h.Anim, h.Speed, h.Mount.Barding, Ease.Hash((int)(h.Pos.X * 7) + (int)(h.Pos.Y * 13) * 31));
    }

    public void DrawDeadHorse(DeadHorse h, float ground)
    {
        if (h.Barding == "wolf") return;
        float k = Now > 0 ? (Now - h.At) / 0.7f : 1f;
        if (k >= 1f) return;
        UnitMatTable.Fill(h.Team, -1, h.Coat, h.Barding == "camel", Color.White, palCols); palDirty = true;
        unitIdHash = Ease.Hash((int)(h.Pos.X * 13 + h.Pos.Y * 29));
        float hScale = HorseScale(h.Barding);
        float drop = Ease.Out(MathF.Min(1f, k * 2f)) * 0.34f * Scale * hScale;
        float roll = 88f * Ease.In(MathF.Max(0f, k * 1.25f - 0.25f));
        Frame(h.Pos, h.Heading, ground - drop, Scale * hScale);
        Lean(roll, 0f);
        bool camel = h.Barding == "camel";
        bool scaleB = h.Barding is "scalebarding" or "mailbarding" or "lamellarbarding";
        bool halfB = h.Barding == "halfbarding";
        bool furB = h.Barding == "furbarding";
        bool barded = h.Barding is "barding" or "caparison" or "gildedcaparison" or "partibarding" or "crossbarding" or "chequybarding" or "tasselled";
        var bodyMesh = camel ? kit.CamelBody : scaleB ? kit.HorseBodyScale : halfB ? kit.HorseBodyHalf : furB ? kit.HorseBodyFur : barded ? kit.HorseBodyBarded : kit.HorseBody;
        var neckMesh = camel ? kit.CamelNeck : scaleB ? kit.HorseNeckScale : (barded || halfB) ? kit.HorseNeckBarded : kit.HorseNeck;
        var headMesh = camel ? kit.CamelHead : scaleB ? kit.HorseHeadScale : (barded || halfB) ? kit.HorseHeadBarded : kit.HorseHead;
        float y = 0.85f;
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1f : 1f;
            var hip = new Vector3(sd * 0.19f, y - 0.04f, front ? 0.56f : -0.56f);
            var hoof = new Vector3(sd * 0.22f, 0.08f + k * 0.18f, hip.Z + (front ? 0.14f : -0.14f));
            var knee = Joint(hip, hoof, 0.41f, 0.41f, front);
            EmitBone(kit.HorseLegLow, knee, hoof, Vector3.UnitZ);
            EmitBone(front ? kit.HorseLegUp : kit.HorseLegHindUp, hip, knee, Vector3.UnitZ);
        }
        EmitLocal(bodyMesh, new Vector3(0, y, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        var neckBase = new Vector3(0, y + 0.14f, 0.72f);
        var poll = new Vector3(0, y + 0.72f - k * 0.18f, 1.16f);
        EmitBone(neckMesh, neckBase, poll, Vector3.UnitZ);
        var muzzle = new Vector3(0, y + 0.44f - k * 0.22f, 1.56f);
        Vector3 headFwd = Vector3.Normalize(muzzle - poll);
        Vector3 headRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, headFwd));
        Vector3 headUp = Vector3.Normalize(Vector3.Cross(headFwd, headRight));
        EmitLocal(headMesh, poll, headRight, headUp, headFwd);
        EmitBone(kit.HorseTail, new Vector3(0, y + 0.16f, -0.84f), new Vector3(0, y - 0.45f, -1.08f), Vector3.UnitZ);
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

        var gov = new LodGovernor();
        void RunSlider(string label, Func<Unit, float> lodOf)
        {
            for (int w = 0; w < 3; w++)
            {
                verts.N = 0; p3.Now = b.Time;
                foreach (var u in b.Units) { if (u.Fled) continue; p3.Lod = lodOf(u); p3.TryDraw(u, 0); }
            }
            const int frames = 40;
            long alloc0 = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            float lodSum = 0; int cnt = 0;
            for (int f = 0; f < frames; f++)
            {
                verts.N = 0; p3.Now = b.Time + f * 0.016f;
                lodSum = 0; cnt = 0;
                foreach (var u in b.Units)
                {
                    if (u.Fled) continue;
                    float l = lodOf(u);
                    lodSum += l; cnt++;
                    p3.Lod = l;
                    p3.TryDraw(u, 0);
                }
            }
            sw.Stop();
            long allocPerFrame = (GC.GetAllocatedBytesForCurrentThread() - alloc0) / frames;
            double ms = sw.Elapsed.TotalMilliseconds / frames;
            Console.WriteLine($"  {label,-18}: {ms,6:F2} ms/frame | {verts.N / 3,8:N0} tris | avg LOD {(cnt > 0 ? lodSum / cnt : 0):0.00} | GC {allocPerFrame} B/frame");
        }

        RunSlider("Field View (dyn)", u => gov.LodFor(Vector2.Distance(u.Pos, b.Player.Pos), 1f, 1f));
        RunSlider("Command View", u => gov.LodFor(Vector2.Distance(u.Pos, b.Centroid(0)), 0.62f, 1f));
        RunSlider("Shadow Pass", _ => 0.08f);
        RunSlider("Slider 1.00", _ => 1.00f);
        RunSlider("Slider 0.75", _ => 0.75f);
        RunSlider("Slider 0.50", _ => 0.50f);
        RunSlider("Slider 0.25", _ => 0.25f);
        RunSlider("Slider 0.10", _ => 0.10f);
    }
}
