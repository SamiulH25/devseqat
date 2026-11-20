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
    /// Optional 2D Puppet reference for sharing soft-particle swing glints and active Awe sources.
    public Puppet? FxPuppet;

    const float RearDeg = 38f, Saddle = 0.87f, Shank = 0.34f, UpperArm = 0.29f, Forearm = 0.28f;

    Vector3 origin, fwd, right;
    Vector3 camRight = Vector3.UnitX;
    float scale, tilt, tiltX, tiltZ, twist;
    float unitIdHash;

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

    /// Hot-path zero-allocation mesh emission into `verts.A` with packed (patternId, metallic) in Color.A and meter-scale UVs.
    void EmitLocal(MeshKit3D.Vert[] part, Vector3 pos, Vector3 rx, Vector3 uy, Vector3 fz, float sx = 1f, float sy = 1f, float sz = 1f, byte partPat = 0)
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
        var metalQ = UnitMatTable.MetalQ;

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

            byte pat = 0;
            if (partPat == MeshKit3D.PatMail && m == 5) pat = MeshKit3D.PatMail;
            else if (partPat == MeshKit3D.PatScale && (m == 4 || m == 5 || m == 6 || m == 7)) pat = MeshKit3D.PatScale;
            else if (partPat == MeshKit3D.PatQuilt && (m == 1 || m == 2)) pat = MeshKit3D.PatQuilt;
            else if (m == 4 || m == 8) pat = MeshKit3D.PatBrushed;

            byte alpha = (byte)((pat << 4) | metalQ[m]);

            dst[n++] = new VertexPNCT(
                new Vector3(px, py, pz),
                new Vector3(nx, ny, nz),
                new Color(cr, cg, cb, alpha),
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
    public bool TryDraw(Unit u, float ground)
    {
        if (u.Fled || u.Raised) return true;
        var look = Variety.Of(u);
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
        UnitMatTable.Fill(palIdx, look.Visage, u.Coat, camel, tint, palCols, look.Face);

        float size = (u.Type.Class == UnitClass.Captain ? 1.12f : 1f) * u.Type.Size * Scale * Variety.Stature(u);
        var lm = kit.Resolve(look);

        if (u.Dead)
        {
            DrawFallen3D(u, ground, size, phase, palIdx, look, lm);
            return true;
        }

        // Far LOD (Detail == 2): ~50-100 tris total, zero skeletal overhead!
        if (Detail >= 2)
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
                UnitMatTable.Fill(palIdx, look.Visage, u.Coat, false, tint, palCols, look.Face);
            }
            Frame(u.Pos, u.ViewFacing, liftFar, size);
            EmitLocal(kit.LodFoot, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
            return true;
        }

        float lift = ground;
        if (u.Mounted && u.Horse!.Barding == "wolf")
        {
            lift = ground + DrawWarg3D(u, ground, tint);
            UnitMatTable.Fill(palIdx, look.Visage, u.Coat, false, tint, palCols, look.Face);
        }
        else if (u.Mounted)
            lift = ground + DrawHorse3D(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, u.Horse!.Barding, Ease.Hash(u.Index));

        var p = Puppet.PoseOf(u, Now, phase, Winner);
        if (FxPuppet is { Awe.Count: > 0 }) p = Puppet.Awed(FxPuppet.Awe, u, p, phase);

        var at = u.Pos;
        float leanBack = 0;
        if (u.Mounted && Rear > 0)
        {
            float a = Rad(RearDeg * Rear);
            at -= new Vector2(MathF.Cos(u.ViewHeading), MathF.Sin(u.ViewHeading)) * Saddle * MathF.Sin(a) * Scale * 0.6f;
            lift += (MathF.Sin(a) * 0.7f - Saddle * (1 - MathF.Cos(a))) * Scale;
            leanBack = RearDeg * Rear * 0.8f;
        }

        Frame(at, u.ViewFacing, lift + (p.Bob - (u.Mounted ? 0 : Sink(p))) * size, size);
        Lean(p.LeanX, p.LeanZ - leanBack);

        // Spider web snare wrapping a caught unit.
        if (Now > 0 && Now < u.WebbedUntil && kit.WebSnare.Length > 0)
            EmitLocal(kit.WebSnare, new Vector3(0, u.Mounted ? 0.35f : 0f, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

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
                    EmitBone(lm.Thigh, hip, knee, Vector3.UnitZ, 1f, lm.LegPat);
                    EmitBone(lm.Shin, knee, foot, Vector3.UnitZ, 1f, lm.LegPat);
                }
                else
                {
                    EmitBone(lm.Shin, hip, foot, Vector3.UnitZ, 1f, lm.LegPat);
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
                EmitBone(lm.Thigh, hip, knee, Vector3.UnitZ, 1f, lm.LegPat);
                EmitBone(lm.Shin, knee, foot, Vector3.UnitZ, 1f, lm.LegPat);
            }
        }

        // 2. Determine combat stance twist BEFORE torso so archers turn side-on naturally!
        var grip = Look.GripOf(look.Weapon);
        bool ranged = Look.Ranged(look.Weapon);
        bool isCrossbow = p.Armed && (p.Haul >= 0 || (grip == Grip.Crossbow && !u.Mounted));
        twist = isCrossbow ? 0 : (ranged && p.Aim) ? 28f : (ranged || !p.Armed) ? 0 : Puppet.TwistOf(p);

        Vector3 tR = RotY(Vector3.UnitX, twist);
        Vector3 tF = RotY(Vector3.UnitZ, twist);
        EmitLocal(lm.Torso, Vector3.Zero, tR, Vector3.UnitY, tF, 1f, 1f, 1f, lm.TorsoPat);
        if (lm.HasTabard && Detail == 0)
        {
            var emb = EmblemOf(palIdx);
            if (emb.Length > 0)
                EmitLocal(emb, RotY(new Vector3(0, 1.03f, 0.175f), twist), tR, Vector3.UnitY, tF, 0.56f, 0.56f, 0.7f);
        }
        if (lm.HasCape && Detail == 0) EmitLocal(kit.Cape, Vector3.Zero, tR, Vector3.UnitY, tF, 1f, 1f, 1f, MeshKit3D.PatQuilt);

        // 3. Head + Helm + Plume
        float headYaw = p.Look + (ranged && p.Aim ? 0f : twist * 0.4f);
        Vector3 hR = RotY(Vector3.UnitX, headYaw);
        Vector3 hF = RotY(Vector3.UnitZ, headYaw);
        if (!lm.FullEnclosedHelm && (Detail == 0 || lm.Helm.Length == 0))
            EmitLocal(lm.Head, new Vector3(0, 1.53f, 0.01f), hR, Vector3.UnitY, hF);
        if (lm.Helm.Length > 0)
            EmitLocal(lm.Helm, new Vector3(0, 1.53f, 0.01f), hR, Vector3.UnitY, hF);
        if (lm.HasPlume && Detail == 0)
            EmitLocal(kit.Plume, new Vector3(0, 1.78f, 0.0f), hR, Vector3.UnitY, hF);

        // 4. Arms, Weapon, Shield, Quivers & Special Combat Stances
        Vector3 Shoulder(int side) => RotY(new Vector3(side * 0.23f, 1.24f, 0), twist);
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

        var wHand = ArmEnd(1, p.ArmA, p.Yaw - twist, p.Reach);
        var wDir = RotY(new Vector3(0, MathF.Sin(Rad(p.WeapB)), MathF.Cos(Rad(p.WeapB))), p.Yaw);
        if (wDir.LengthSquared() < 1e-4f) wDir = Vector3.UnitY;
        wDir.Normalize();

        DrawArm(1, wHand);

        bool shieldArm = p.Armed && u.Type.HasShield && !ranged && !p.TwoHand && !u.Couched;
        Vector3 offHand = p.TwoHand && p.Armed
            ? wHand - wDir * 0.22f
            : shieldArm
                ? (p.ShieldUp ? new Vector3(-0.08f, 1.20f, 0.36f) : new Vector3(-0.24f, 0.98f, 0.24f) + new Vector3(0, p.OffA * 0.002f, 0))
                : ArmEnd(-1, p.OffA, -twist, p.OffReach);
        DrawArm(-1, offHand);

        if (!p.Armed) return true; // routed man has thrown away his weapon and shield

        // Couched lance (heavy cavalry at gallop)
        if (u.Couched)
        {
            float rel = MathHelper.WrapAngle(u.ViewHeading - u.ViewFacing);
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
            EmitDir(lm.Weapon, wHand, wDir);

            // Blade swing motion glint wisps during active strike frames (shared soft-particle pass).
            if (Detail == 0 && FxPuppet?.Soft != null && grip is Grip.One or Grip.Two or Grip.Pole)
            {
                float strikeK = -1f;
                if (!u.IsPlayer && u.Action == UnitAction.Recover && u.Type.Recover > 0)
                {
                    float rk = 1f - u.ActionTimer / u.Type.Recover;
                    if (rk < 0.26f) strikeK = rk / 0.26f;
                }
                else if (u.IsPlayer && u.Action == UnitAction.Windup)
                {
                    float wk = 1f - u.ActionTimer / (u.Swing == AttackDir.Overhead ? 0.2f : 0.15f);
                    if (wk > 0.55f) strikeK = (wk - 0.55f) / 0.45f;
                }
                if (strikeK >= 0f)
                {
                    float reachLen = grip == Grip.Pole ? 1.55f : p.TwoHand ? 1.15f : 0.78f;
                    var tip = W(wHand + wDir * (reachLen * 0.85f));
                    var mid = W(wHand + wDir * (reachLen * 0.52f));
                    float a = MathF.Sin(strikeK * MathF.PI) * (p.TwoHand ? 0.52f : 0.36f);
                    var col = new Color(235, 242, 255);
                    FxPuppet.Haze(tip, (p.TwoHand ? 0.55f : 0.36f) * scale, col, a, strikeK * 6f + u.Index);
                    FxPuppet.Haze(mid, (p.TwoHand ? 0.38f : 0.24f) * scale, col, a * 0.65f, strikeK * 4f + u.Index);
                }
            }
        }

        if (u.Type.HasShield && lm.HasShield)
        {
            Vector3 shPos = p.ShieldUp ? offHand + new Vector3(0.05f, 0.02f, 0.12f) : offHand + new Vector3(-0.07f, 0.02f, 0.06f);
            float shYaw = p.ShieldUp ? 12f : -58f;
            EmitShield(lm, palIdx, shPos, shYaw, u.StuckArrows, false);
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

    /// Emit a shield mesh with the side's exact 3D relief heraldic emblem and any arrows stuck into its face.
    void EmitShield(MeshKit3D.LookMeshes lm, int palIdx, Vector3 pos, float yawDeg, int stuckArrows, bool backSlung)
    {
        Vector3 sR = RotY(Vector3.UnitX, yawDeg);
        Vector3 sF = RotY(Vector3.UnitZ, yawDeg);
        EmitLocal(lm.Shield, pos, sR, Vector3.UnitY, sF);

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
                Vector3 ePos = pos + Vector3.UnitY * ey + sF * ez;
                EmitLocal(emb, ePos, sR, Vector3.UnitY, sF, sx, sy, 1f);
            }
        }

        if (!backSlung && Detail == 0 && stuckArrows > 0 && kit.Arrow.Length > 0)
        {
            int count = Math.Min(stuckArrows, 4);
            for (int i = 0; i < count; i++)
            {
                float ox = (i % 2 - 0.5f) * 0.16f;
                float oy = (i / 2 - 0.5f) * 0.18f;
                Vector3 basePos = pos + sR * ox + Vector3.UnitY * oy + sF * 0.06f;
                Vector3 dir = Vector3.Normalize(sF + sR * (0.20f * (i % 2 * 2 - 1)) + Vector3.UnitY * 0.24f);
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

        float legSwing = crumple ? 30f * k : 10f;
        for (int side = -1; side <= 1; side += 2)
        {
            var hip = new Vector3(side * 0.125f, 0.68f, 0);
            float s = Rad(legSwing * side);
            var foot = hip + new Vector3(0, -0.62f * MathF.Cos(s), 0.62f * MathF.Sin(s));
            EmitBone(lm.Shin, hip, foot, Vector3.UnitZ, 1f, lm.LegPat);
        }

        EmitLocal(lm.Torso, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 1f, 1f, 1f, lm.TorsoPat);
        if (!lm.FullEnclosedHelm && (Detail == 0 || lm.Helm.Length == 0))
            EmitLocal(lm.Head, new Vector3(0, 1.53f, 0.01f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        if (lm.Helm.Length > 0)
            EmitLocal(lm.Helm, new Vector3(0, 1.53f, 0.01f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

        float armA = 40f + 60f * k, offA = 50f + 40f * k;
        var rSh = new Vector3(0.23f, 1.24f, 0);
        var lSh = new Vector3(-0.23f, 1.24f, 0);
        var rHand = rSh + new Vector3(0.08f, -MathF.Cos(Rad(armA)), MathF.Sin(Rad(armA))) * 0.50f;
        var lHand = lSh + new Vector3(-0.08f, -MathF.Cos(Rad(offA)), MathF.Sin(Rad(offA))) * 0.50f;
        EmitBone(lm.FArm, rSh, rHand, Vector3.UnitZ, 1f, lm.LimbPat);
        EmitBone(lm.FArm, lSh, lHand, Vector3.UnitZ, 1f, lm.LimbPat);

        if (k < 0.4f && lm.Weapon.Length > 0)
            EmitDir(lm.Weapon, rHand, RotY(new Vector3(0, 0.5f, 0.87f), 20f));
    }

    /// 3D company banner on the unified pixel grid: wooden pole + brass finial + 3-strip rippling 3D cloth + heraldic emblem!
    public void Banner(Vector2 pos, float ground, int palette, float wave, int charge = -1, float stream = 0, float lean = 0, Vector2 fallDir = default)
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

        verts.Reserve(strips * 24);
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

            if (i == 2)
            {
                midCenter = (tA + tB + mA + mB) * 0.25f;
                midSide = segDir;
                midNorm = segNorm;
            }
            prev = next;
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

    float DrawHorse3D(Vector2 pos, float ground, float heading, float anim, float speed, string barding, float idle = -1)
    {
        float hScale = HorseScale(barding);
        Frame(pos, heading, ground, Scale * hScale);

        if (Rear > 0)
        {
            Lean(0, -RearDeg * Rear);
            origin.Y += MathF.Sin(Rad(RearDeg * Rear)) * 0.7f * Scale * hScale;
        }

        bool camel = barding == "camel";
        bool scaleB = barding == "scalebarding";
        bool furB = barding == "furbarding";
        bool barded = barding is "barding" or "halfbarding" or "gildedcaparison";

        var bodyMesh = camel ? kit.CamelBody : scaleB ? kit.HorseBodyScale : furB ? kit.HorseBodyFur : barded ? kit.HorseBodyBarded : kit.HorseBody;
        var neckMesh = camel ? kit.CamelNeck : scaleB ? kit.HorseNeckScale : barded ? kit.HorseNeckBarded : kit.HorseNeck;
        var headMesh = camel ? kit.CamelHead : scaleB ? kit.HorseHeadScale : barded ? kit.HorseHeadBarded : kit.HorseHead;
        byte bodyPat = scaleB ? MeshKit3D.PatScale : barded ? MeshKit3D.PatQuilt : MeshKit3D.PatSmooth;

        float gait = Math.Clamp(speed / 9f, 0, 1);
        float trot = Math.Clamp(speed / 2.2f, 0, 1);
        float bob = (MathF.Sin(anim * MathF.Tau) * 0.032f + MathF.Sin(anim * MathF.Tau * 2) * 0.010f) * gait;
        float rock = MathF.Sin(anim * MathF.Tau - 0.5f) * 0.042f * gait;
        float y = 0.85f + bob;
        bool still = speed < 0.3f && idle >= 0;
        float shift = still ? MathF.Sin(Now * 0.65f + idle * 6.28f) * 0.012f : 0;
        float nod = still ? MathF.Max(0, MathF.Sin(Now * 0.55f + idle * 11f) - 0.72f) * 0.30f * MathF.Sin(Now * 4.5f) : 0;
        float paw = still ? MathF.Max(0, MathF.Sin(Now * 0.45f + idle * 17f) - 0.86f) * 7f : 0;

        float amp = speed < 0.3f ? 0 : 12f + 22f * trot + 28f * gait;
        ReadOnlySpan<float> phase = stackalloc float[] { 0f, 0.5f - 0.25f * gait, 0.25f + 0.3f * gait, 0.75f + 0.05f * gait };
        const float Upper = 0.42f, Lower = 0.46f;

        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1f : 1f;
            float a = Rad(amp) * MathF.Sin(anim * MathF.Tau + phase[i] * MathF.Tau);
            float bend = MathF.Max(0, -MathF.Cos(anim * MathF.Tau + phase[i] * MathF.Tau)) * (front ? 0.52f : 0.36f) * (trot + gait) * 0.5f;
            if (front && Rear > 0) { a = Rad(58 * Rear); bend = 0.9f * Rear; }
            if (i == 0 && paw > 0) { a = Rad(20 * paw); bend = 0.4f * paw; }

            var hip = new Vector3(sd * 0.21f + shift, y - 0.14f + (front ? rock : -rock), front ? 0.58f : -0.60f);
            var knee = hip + new Vector3(0, -Upper * MathF.Cos(a), Upper * MathF.Sin(a));
            float lowA = a + (front ? -bend : bend);
            var hoof = knee + new Vector3(0, -Lower * MathF.Cos(lowA), Lower * MathF.Sin(lowA));
            if (Detail == 0)
            {
                EmitBone(kit.HorseLegUp, hip, knee, Vector3.UnitZ);
                EmitBone(kit.HorseLegLow, knee, hoof, Vector3.UnitZ);
            }
            else
            {
                EmitBone(kit.HorseLegLow, hip, hoof, Vector3.UnitZ);
            }
        }

        EmitLocal(bodyMesh, new Vector3(shift, y, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 1f, 1f, 1f, bodyPat);

        float neckOut = gait * 0.12f;
        var neckBase = new Vector3(shift, y + rock + (camel ? 0.02f : 0.14f), 0.72f);
        var poll = new Vector3(0, y + rock + (camel ? 0.88f : 0.74f) + nod - neckOut * 0.4f, (camel ? 1.24f : 1.16f) + neckOut);
        EmitBone(neckMesh, neckBase, poll, Vector3.UnitZ, 1f, bodyPat);

        var muzzle = new Vector3(0, y + rock + (camel ? 0.62f : 0.36f) + nod * 1.5f - neckOut, (camel ? 1.62f : 1.52f) + neckOut * 1.4f);
        Vector3 headFwd = Vector3.Normalize(muzzle - poll);
        Vector3 headUp = Vector3.Normalize(Vector3.Cross(headFwd, Vector3.UnitX));
        if (headUp.Y < 0) headUp = -headUp;
        EmitLocal(headMesh, poll, Vector3.UnitX, headUp, headFwd);

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
