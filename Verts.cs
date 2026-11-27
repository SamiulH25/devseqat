using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Growable vertex array reused every frame, backed by a ring-buffered DynamicVertexBuffer using NoOverwrite
/// streaming so OpenGL only orphans the buffer on wrap-around instead of every draw call.
sealed class Verts<T> : IDisposable where T : struct, IVertexType
{
    const int Chunk = 60000;
    public T[] A = new T[1024];
    public int N;
    DynamicVertexBuffer? vb;
    int ringOffset, vertexStride;

    public void Reserve(int extra)
    {
        int need = N + extra;
        if (need <= A.Length) return;
        int cap = A.Length;
        while (cap < need) cap *= 2;
        Array.Resize(ref A, cap);
    }

    public void Add(T v)
    {
        if (N == A.Length) Array.Resize(ref A, N * 2);
        A[N++] = v;
    }

    public void Draw(GraphicsDevice gd, PrimitiveType type, int primitiveCount)
    {
        if (primitiveCount <= 0) return;
        int stride = type == PrimitiveType.LineList ? 2 : 3;
        int count = primitiveCount * stride;
        int needCap = Math.Max(4096, count * 2);
        if (vb == null || vb.IsDisposed || vb.VertexCount < count)
        {
            vb?.Dispose();
            int cap = 4096;
            while (cap < needCap) cap *= 2;
            vb = new DynamicVertexBuffer(gd, typeof(T), cap, BufferUsage.WriteOnly);
            vertexStride = vb.VertexDeclaration.VertexStride;
            ringOffset = 0;
        }
        SetDataOptions opt;
        if (ringOffset + count > vb.VertexCount) { ringOffset = 0; opt = SetDataOptions.Discard; }
        else opt = ringOffset == 0 ? SetDataOptions.Discard : SetDataOptions.NoOverwrite;
        vb.SetData(ringOffset * vertexStride, A, 0, count, vertexStride, opt);
        gd.SetVertexBuffer(vb);
        for (int s = 0; s < primitiveCount; s += Chunk)
            gd.DrawPrimitives(type, ringOffset + s * stride, Math.Min(Chunk, primitiveCount - s));
        ringOffset += count;
    }

    public void Dispose() { vb?.Dispose(); vb = null; ringOffset = 0; }
}
