using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// GPU-skinned static vertex format (Position, Normal, Color, UV, BoneIndices4, BoneWeights4).
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct VertexPNCTBW : IVertexType
{
    public Vector3 Pos;
    public Vector3 Norm;
    public Color Col;
    public Vector2 UV;
    public Vector4 Bones;
    public Vector4 Weights;

    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(36, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(52, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2)
    );
    VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// Static GPU-skinned mesh + PointClamp pixel-art texture (SKN1 format). Zero per-frame CPU vertex transforms.
sealed class SkinnedMesh3D : IDisposable
{
    public readonly VertexBuffer Vbo;
    public readonly IndexBuffer Ibo;
    public readonly Texture2D? Texture;
    public readonly int TriCount;
    readonly Matrix[] invBind;
    readonly Matrix[] poseBones = new Matrix[8];

    SkinnedMesh3D(VertexBuffer vbo, IndexBuffer ibo, Texture2D? tex, int triCount, Matrix[] invBind)
    {
        Vbo = vbo; Ibo = ibo; Texture = tex; TriCount = triCount; this.invBind = invBind;
        for (int i = 0; i < 8; i++) poseBones[i] = Matrix.Identity;
    }

    public static SkinnedMesh3D? TryLoad(GraphicsDevice gd, string skinName, string? texName = null)
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var path = Path.Combine(dir, "Data", "meshes", skinName);
            if (!File.Exists(path)) continue;
            Texture2D? tex = null;
            if (texName != null)
            {
                var tp = Path.Combine(dir, "Data", "meshes", texName);
                if (File.Exists(tp))
                {
                    using var tfs = File.OpenRead(tp);
                    tex = Texture2D.FromStream(gd, tfs);
                }
            }
            return Load(gd, path, tex);
        }
        return null;
    }

    static SkinnedMesh3D Load(GraphicsDevice gd, string path, Texture2D? tex)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.UTF8);
        if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "SKN1")
            throw new InvalidDataException($"bad skin magic: {path}");
        int boneCount = br.ReadInt32();
        var invBind = new Matrix[Math.Max(8, boneCount)];
        for (int i = 0; i < invBind.Length; i++) invBind[i] = Matrix.Identity;
        for (int i = 0; i < boneCount; i++)
        {
            float bx = br.ReadSingle(), by = br.ReadSingle(), bz = br.ReadSingle();
            invBind[i] = Matrix.CreateTranslation(-bx, -by, -bz);
        }
        int vcount = br.ReadInt32();
        int icount = br.ReadInt32();
        var verts = new VertexPNCTBW[vcount];
        byte alphaPat = (5 << 4) | 0; // PatFur = 5, non-metallic
        for (int i = 0; i < vcount; i++)
        {
            var pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            var norm = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            float u = br.ReadSingle(), v = br.ReadSingle();
            byte r = br.ReadByte(), g = br.ReadByte(), b = br.ReadByte();
            _ = br.ReadByte();
            var bones = new Vector4(br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
            var weights = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            verts[i] = new VertexPNCTBW { Pos = pos, Norm = norm, Col = new Color(r, g, b, alphaPat), UV = new Vector2(u, v), Bones = bones, Weights = weights };
        }
        var idx = new ushort[icount];
        for (int i = 0; i < icount; i++) idx[i] = br.ReadUInt16();
        var vbo = new VertexBuffer(gd, VertexPNCTBW.Decl, vcount, BufferUsage.WriteOnly);
        vbo.SetData(verts);
        var ibo = new IndexBuffer(gd, IndexElementSize.SixteenBits, icount, BufferUsage.WriteOnly);
        ibo.SetData(idx);
        return new SkinnedMesh3D(vbo, ibo, tex, icount / 3, invBind);
    }

    static readonly string[] BoneNames = { "Bone0", "Bone1", "Bone2", "Bone3", "Bone4", "Bone5", "Bone6", "Bone7" };

    /// Compute 8-bone bear animation (Root, Chest, Head, Jaw, FrontL, FrontR, HindL, HindR) on GPU.
    public void DrawBear(GraphicsDevice gd, Shading sh, Unit u, float ground, float scale, string tech)
    {
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 6f, 0, 1);
        float amp = speed < 0.3f ? 0 : 0.24f + 0.38f * gait;
        float waddle = MathF.Sin(u.Anim * MathF.Tau * 1.2f) * 0.055f * gait;
        float bob = MathF.Abs(MathF.Sin(u.Anim * MathF.Tau * 1.2f)) * 0.04f * gait;
        float rear = u.Action == UnitAction.Windup ? Math.Clamp(1 - u.ActionTimer / MathF.Max(0.05f, u.Type.Windup), 0, 1)
            : u.Action == UnitAction.Recover ? Math.Clamp(u.ActionTimer / MathF.Max(0.05f, u.Type.Recover) * 2 - 1, 0, 1) : 0;
        float swipe = u.Action == UnitAction.Recover ? MathF.Sin(Math.Clamp(1f - u.ActionTimer / MathF.Max(0.05f, u.Type.Recover), 0f, 1f) * MathF.PI) : 0f;
        float rearRad = MathHelper.ToRadians(40f * rear);
        float yawSwipe = MathHelper.ToRadians(swipe * -14f + waddle * 55f);

        // Bind-space pivots: 0 Pelvis (0,0.72,-0.32), 1 Chest (0,0.80,0.26), 2 Head (0,0.87,0.52), 3 Jaw (0,0.78,0.62)
        // 4 FrontL (-0.26,0.66,0.36), 5 FrontR (0.26,0.66,0.36), 6 HindL (-0.26,0.66,-0.42), 7 HindR (0.26,0.66,-0.42)
        var rootMat = Matrix.CreateTranslation(waddle * 0.4f, bob, 0);
        var chestMat = Matrix.CreateRotationY(yawSwipe) * Matrix.CreateRotationX(-rearRad) * Matrix.CreateTranslation(waddle * 0.6f, bob + rear * 0.14f, 0);
        var headMat = Matrix.CreateRotationX(MathHelper.ToRadians(18f * rear)) * chestMat;
        var jawOpen = MathHelper.ToRadians(10f + 22f * MathF.Max(rear, swipe));
        var jawMat = Matrix.CreateRotationX(jawOpen) * headMat;

        float fL = amp * MathF.Sin(u.Anim * MathF.Tau * 1.2f);
        float fR = amp * MathF.Sin(u.Anim * MathF.Tau * 1.2f + MathF.PI);
        float hL = amp * MathF.Sin(u.Anim * MathF.Tau * 1.2f + MathF.PI);
        float hR = amp * MathF.Sin(u.Anim * MathF.Tau * 1.2f);

        poseBones[0] = invBind[0] * rootMat * Matrix.CreateTranslation(0, 0.72f, -0.32f);
        poseBones[1] = invBind[1] * chestMat * Matrix.CreateTranslation(0, 0.80f, 0.26f);
        poseBones[2] = invBind[2] * headMat * Matrix.CreateTranslation(0, 0.87f, 0.52f);
        poseBones[3] = invBind[3] * jawMat * Matrix.CreateTranslation(0, 0.78f, 0.62f);
        poseBones[4] = invBind[4] * Matrix.CreateRotationX(fL - rearRad * 0.6f) * chestMat * Matrix.CreateTranslation(-0.26f, 0.66f, 0.36f);
        poseBones[5] = invBind[5] * Matrix.CreateRotationX(fR - rearRad * 0.6f - swipe * 0.5f) * chestMat * Matrix.CreateTranslation(0.26f, 0.66f, 0.36f);
        poseBones[6] = invBind[6] * Matrix.CreateRotationX(hL) * rootMat * Matrix.CreateTranslation(-0.26f, 0.66f, -0.42f);
        poseBones[7] = invBind[7] * Matrix.CreateRotationX(hR) * rootMat * Matrix.CreateTranslation(0.26f, 0.66f, -0.42f);

        float f = u.ViewFacing;
        var fwd = new Vector3(MathF.Cos(f), 0, MathF.Sin(f));
        var right = new Vector3(fwd.Z, 0, -fwd.X);
        var world = new Matrix(
            right.X, right.Y, right.Z, 0,
            0, 1, 0, 0,
            fwd.X, fwd.Y, fwd.Z, 0,
            0, 0, 0, 1
        ) * Matrix.CreateScale(u.Type.Size * scale) * Matrix.CreateTranslation(u.Pos.X, ground, u.Pos.Y);

        var p = sh.SkinnedUnit3D.Parameters;
        p["World"]?.SetValue(world);
        for (int i = 0; i < 8; i++) p[BoneNames[i]]?.SetValue(poseBones[i]);
        if (Texture != null)
        {
            p["UnitTex"]?.SetValue(Texture);
            p["HasUnitTex"]?.SetValue(1f);
        }
        sh.SkinnedUnit3D.CurrentTechnique = sh.SkinnedUnit3D.Techniques[tech];
        sh.SkinnedUnit3D.CurrentTechnique.Passes[0].Apply();
        gd.SetVertexBuffer(Vbo);
        gd.Indices = Ibo;
        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TriCount);
    }

    /// Compute 6-bone wolf animation (Pelvis, Chest, Head, FrontL, FrontR, HindL, HindR) on GPU.
    public void DrawWolf(GraphicsDevice gd, Shading sh, Unit u, float ground, float scale, string tech)
    {
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 7f, 0, 1);
        float amp = speed < 0.3f ? 0 : 0.30f + 0.52f * gait;
        float bob = MathF.Sin(u.Anim * MathF.Tau * 1.5f) * 0.045f * gait;
        float wind = u.Action == UnitAction.Windup ? Math.Clamp(1f - u.ActionTimer / MathF.Max(0.05f, u.Type.Windup), 0f, 1f) : 0f;
        float strike = u.Action == UnitAction.Recover ? Math.Clamp(u.ActionTimer / MathF.Max(0.05f, u.Type.Recover), 0f, 1f) : 0f;
        float lunge = strike > 0 ? Ease.Out(strike) * 0.24f : -wind * 0.14f;
        float leapY = strike > 0 ? MathF.Sin(strike * MathF.PI) * 0.14f : 0f;

        // Bind pivots: 0 Pelvis (0,0.62,-0.32), 1 Chest (0,0.64,0.24), 2 Head (0,0.76,0.44)
        // 3 FrontL (-0.15,0.58,0.34), 4 FrontR (0.15,0.58,0.34), 5 HindL (-0.15,0.58,-0.38), 6 HindR (0.15,0.58,-0.38)
        var rootMat = Matrix.CreateTranslation(0, bob + leapY - wind * 0.08f, lunge * 0.85f);
        var chestMat = Matrix.CreateRotationX(MathHelper.ToRadians(wind * 10f - strike * 10f)) * rootMat;
        var headMat = Matrix.CreateRotationX(MathHelper.ToRadians(-wind * 12f + strike * 14f)) * chestMat;

        float fL = amp * MathF.Sin(u.Anim * MathF.Tau * 1.5f);
        float fR = amp * MathF.Sin(u.Anim * MathF.Tau * 1.5f + 1.38f);
        float hL = amp * MathF.Sin(u.Anim * MathF.Tau * 1.5f + 3.45f);
        float hR = amp * MathF.Sin(u.Anim * MathF.Tau * 1.5f + 4.90f);

        poseBones[0] = invBind[0] * rootMat * Matrix.CreateTranslation(0, 0.62f, -0.32f);
        poseBones[1] = invBind[1] * chestMat * Matrix.CreateTranslation(0, 0.64f, 0.24f);
        poseBones[2] = invBind[2] * headMat * Matrix.CreateTranslation(0, 0.76f, 0.44f);
        poseBones[3] = invBind[3] * Matrix.CreateRotationX(fL) * chestMat * Matrix.CreateTranslation(-0.15f, 0.58f, 0.34f);
        poseBones[4] = invBind[4] * Matrix.CreateRotationX(fR) * chestMat * Matrix.CreateTranslation(0.15f, 0.58f, 0.34f);
        poseBones[5] = invBind[5] * Matrix.CreateRotationX(hL) * rootMat * Matrix.CreateTranslation(-0.15f, 0.58f, -0.38f);
        poseBones[6] = invBind[6] * Matrix.CreateRotationX(hR) * rootMat * Matrix.CreateTranslation(0.15f, 0.58f, -0.38f);
        poseBones[7] = Matrix.Identity;

        float f = u.ViewFacing;
        var fwd = new Vector3(MathF.Cos(f), 0, MathF.Sin(f));
        var right = new Vector3(fwd.Z, 0, -fwd.X);
        var world = new Matrix(
            right.X, right.Y, right.Z, 0,
            0, 1, 0, 0,
            fwd.X, fwd.Y, fwd.Z, 0,
            0, 0, 0, 1
        ) * Matrix.CreateScale(u.Type.Size * scale) * Matrix.CreateTranslation(u.Pos.X, ground, u.Pos.Y);

        var p = sh.SkinnedUnit3D.Parameters;
        p["World"]?.SetValue(world);
        for (int i = 0; i < 8; i++) p[BoneNames[i]]?.SetValue(poseBones[i]);
        if (Texture != null)
        {
            p["UnitTex"]?.SetValue(Texture);
            p["HasUnitTex"]?.SetValue(1f);
        }
        sh.SkinnedUnit3D.CurrentTechnique = sh.SkinnedUnit3D.Techniques[tech];
        sh.SkinnedUnit3D.CurrentTechnique.Passes[0].Apply();
        gd.SetVertexBuffer(Vbo);
        gd.Indices = Ibo;
        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TriCount);
    }

    public void Dispose()
    {
        Vbo.Dispose();
        Ibo.Dispose();
        Texture?.Dispose();
    }
}
