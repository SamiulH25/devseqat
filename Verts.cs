using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Growable vertex array reused every frame, backed by a dynamic GPU vertex buffer to avoid client-side array stalls.
sealed class Verts<T> : IDisposable where T : struct, IVertexType
{
    const int Chunk = 60000;
    public T[] A = new T[1024];
    public int N;
    DynamicVertexBuffer? vb;

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
        if (vb == null || vb.IsDisposed || vb.VertexCount < A.Length)
        {
            vb?.Dispose();
            vb = new DynamicVertexBuffer(gd, typeof(T), A.Length, BufferUsage.WriteOnly);
        }
        vb.SetData(A, 0, count, SetDataOptions.Discard);
        gd.SetVertexBuffer(vb);
        for (int s = 0; s < primitiveCount; s += Chunk)
            gd.DrawPrimitives(type, s * stride, Math.Min(Chunk, primitiveCount - s));
    }

    public void Dispose() { vb?.Dispose(); vb = null; }
}
