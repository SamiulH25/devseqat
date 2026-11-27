using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Per-instance 64-byte transform + palette/pattern metadata (stream 1, step rate = 1).
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Instance3D : IVertexType
{
    public Vector4 Row0, Row1, Row2, Meta;

    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4));

    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// Thread-local collector filled by each Parallel.For worker in BattleScreen.Draw.cs (zero locks, zero per-frame heap allocs).
sealed class InstanceBatch
{
    public int[] PartIds = new int[2048];
    public Instance3D[] Insts = new Instance3D[2048];
    public int Count;

    /// Per-unit 14-slot material palettes collected by this worker (R,G,B = albedo, A = MetalQ).
    public Color[] PalRows = new Color[UnitMatTable.Slots * 256];
    public int PalCount;
    public int CurrentPalRow;

    public void Clear() { Count = 0; PalCount = 0; CurrentPalRow = 0; }

    /// Record the active unit's 14-slot palette into this worker's local palette buffer and return its local row index.
    public int PushPalette(ReadOnlySpan<Vector3> cols)
    {
        int row = PalCount++;
        int need = PalCount * UnitMatTable.Slots;
        if (need > PalRows.Length) Array.Resize(ref PalRows, Math.Max(need, PalRows.Length * 2));
        int off = row * UnitMatTable.Slots;
        var metalQ = UnitMatTable.MetalQ;
        for (int m = 0; m < UnitMatTable.Slots; m++)
        {
            var c = cols[m];
            int r = (int)(c.X * 255f + 0.5f); if ((uint)r > 255u) r = r < 0 ? 0 : 255;
            int g = (int)(c.Y * 255f + 0.5f); if ((uint)g > 255u) g = g < 0 ? 0 : 255;
            int b = (int)(c.Z * 255f + 0.5f); if ((uint)b > 255u) b = b < 0 ? 0 : 255;
            PalRows[off + m] = new Color((byte)r, (byte)g, (byte)b, metalQ[m]);
        }
        return CurrentPalRow = row;
    }

    public void Add(int partId, in Instance3D inst)
    {
        if (Count == Insts.Length)
        {
            int cap = Insts.Length * 2;
            Array.Resize(ref PartIds, cap);
            Array.Resize(ref Insts, cap);
        }
        PartIds[Count] = partId;
        Insts[Count] = inst;
        Count++;
    }
}

/// Static GPU mesh atlas holding every MeshKit3D part and LOD slice once in VRAM, drawn via hardware DrawInstancedPrimitives.
sealed class MeshAtlas3D : IDisposable
{
    public readonly record struct PartSlice(int Id, int BaseVertex, int TriCount);

    readonly Dictionary<MeshKit3D.Vert[], PartSlice> map = new(ReferenceEqualityComparer.Instance);
    readonly PartSlice[] slices;
    readonly VertexBuffer atlasVb;
    readonly IndexBuffer atlasIb;

    const int MaxPalRows = 4096;
    readonly Texture2D palTex;
    readonly Color[] palUpload = new Color[UnitMatTable.Slots * MaxPalRows];

    DynamicVertexBuffer? instVb;
    Instance3D[] staged = new Instance3D[4096];
    readonly int[] counts, offsets, activeParts;

    public int PartCount => slices.Length;

    public MeshAtlas3D(GraphicsDevice gd, MeshKit3D kit)
    {
        var all = new List<MeshKit3D.Vert[]>();
        int totalVerts = 0, maxPartVerts = 0;
        foreach (var arr in kit.AllPartArrays())
        {
            if (arr.Length < 3) continue;
            all.Add(arr);
            totalVerts += arr.Length;
            if (arr.Length > maxPartVerts) maxPartVerts = arr.Length;
        }

        slices = new PartSlice[all.Count];
        var verts = new VertexPNCT[Math.Max(3, totalVerts)];
        int cursor = 0;
        for (int p = 0; p < all.Count; p++)
        {
            var src = all[p];
            int tris = src.Length / 3;
            int vCount = tris * 3;
            var slice = new PartSlice(p, cursor, tris);
            slices[p] = slice;
            map[src] = slice;
            for (int i = 0; i < vCount; i++)
            {
                ref readonly var v = ref src[i];
                verts[cursor++] = new VertexPNCT(v.Pos, v.Norm, new Color(v.R, v.G, v.B, v.MatId), new Vector2(v.U, v.V));
            }
        }

        atlasVb = new VertexBuffer(gd, VertexPNCT.Decl, verts.Length, BufferUsage.WriteOnly);
        atlasVb.SetData(verts);

        var idx = new int[Math.Max(3, totalVerts)];
        for (int i = 0; i < idx.Length; i++) idx[i] = i;
        atlasIb = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, idx.Length, BufferUsage.WriteOnly);
        atlasIb.SetData(idx);

        palTex = new Texture2D(gd, UnitMatTable.Slots, MaxPalRows, false, SurfaceFormat.Color);
        counts = new int[all.Count];
        offsets = new int[all.Count];
        activeParts = new int[all.Count];
    }

    public bool TryGetPart(MeshKit3D.Vert[] part, out int partId)
    {
        if (map.TryGetValue(part, out var s)) { partId = s.Id; return true; }
        partId = -1;
        return false;
    }

    /// Merge worker batches, upload the tiny palette strip + per-instance buffer, and issue one DrawInstancedPrimitives per active part.
    /// Returns total triangles drawn.
    public int DrawBatches(GraphicsDevice gd, Shading sh, Effect fx, string technique, bool shadow, ReadOnlySpan<InstanceBatch> batches)
    {
        int totalInst = 0, totalPal = 0;
        for (int b = 0; b < batches.Length; b++)
        {
            totalInst += batches[b].Count;
            totalPal += batches[b].PalCount;
        }
        if (totalInst == 0) return 0;

        // 1. Upload compact per-unit palette texture (skipped on shadow pass).
        Span<int> palBase = stackalloc int[batches.Length];
        if (!shadow)
        {
            int row = 0;
            for (int b = 0; b < batches.Length; b++)
            {
                palBase[b] = row;
                int take = Math.Min(batches[b].PalCount, MaxPalRows - row);
                if (take > 0)
                {
                    Array.Copy(batches[b].PalRows, 0, palUpload, row * UnitMatTable.Slots, take * UnitMatTable.Slots);
                    row += take;
                }
            }
            if (row > 0) palTex.SetData(0, new Rectangle(0, 0, UnitMatTable.Slots, row), palUpload, 0, row * UnitMatTable.Slots);
            fx.Parameters["PalTex"]?.SetValue(palTex);
        }

        // 2. Two-pass O(N) counting sort by PartId into flat staged instance array.
        Array.Clear(counts, 0, counts.Length);
        for (int b = 0; b < batches.Length; b++)
        {
            var ids = batches[b].PartIds;
            int n = batches[b].Count;
            for (int i = 0; i < n; i++) counts[ids[i]]++;
        }

        int activeCount = 0, off = 0;
        for (int p = 0; p < counts.Length; p++)
        {
            int c = counts[p];
            if (c == 0) continue;
            activeParts[activeCount++] = p;
            offsets[p] = off;
            off += c;
        }

        if (staged.Length < totalInst)
        {
            int cap = Math.Max(4096, staged.Length * 2);
            while (cap < totalInst) cap *= 2;
            staged = new Instance3D[cap];
        }

        float invMaxPal = 1f / MaxPalRows;
        for (int b = 0; b < batches.Length; b++)
        {
            var batch = batches[b];
            var ids = batch.PartIds;
            var src = batch.Insts;
            int n = batch.Count;
            int pBase = palBase[b];
            for (int i = 0; i < n; i++)
            {
                int pid = ids[i];
                var inst = src[i];
                if (!shadow)
                {
                    int globalRow = Math.Min(MaxPalRows - 1, pBase + (int)inst.Meta.X);
                    inst.Meta.X = (globalRow + 0.5f) * invMaxPal;
                }
                staged[offsets[pid]++] = inst;
            }
        }

        // 3. Single GPU upload of compact 64-byte instances.
        if (instVb == null || instVb.IsDisposed || instVb.VertexCount < totalInst)
        {
            instVb?.Dispose();
            int cap = 4096;
            while (cap < totalInst) cap *= 2;
            instVb = new DynamicVertexBuffer(gd, Instance3D.Decl, cap, BufferUsage.WriteOnly);
        }
        instVb.SetData(staged, 0, totalInst, SetDataOptions.Discard);

        // 4. Bind static mesh atlas + index buffer once, draw each active part in one instanced call.
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
        if (!shadow) gd.SamplerStates[3] = SamplerState.PointClamp;
        fx.CurrentTechnique = fx.Techniques[technique];
        fx.CurrentTechnique.Passes[0].Apply();
        gd.Indices = atlasIb;

        int totalTris = 0;
        var baseBinding = new VertexBufferBinding(atlasVb, 0, 0);
        for (int a = 0; a < activeCount; a++)
        {
            int pid = activeParts[a];
            int c = counts[pid];
            int start = offsets[pid] - c;
            var sl = slices[pid];
            gd.SetVertexBuffers(baseBinding, new VertexBufferBinding(instVb, start, 1));
            gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, sl.BaseVertex, sl.TriCount, c);
            totalTris += sl.TriCount * c;
        }
        gd.SetVertexBuffer(null);
        gd.Indices = null;
        return totalTris;
    }

    public void Dispose()
    {
        atlasVb.Dispose();
        atlasIb.Dispose();
        palTex.Dispose();
        instVb?.Dispose();
    }
}
