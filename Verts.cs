namespace Ironfield;

/// Growable vertex array reused every frame.
sealed class Verts<T> where T : struct
{
    public T[] A = new T[1024];
    public int N;
    public void Add(T v)
    {
        if (N == A.Length) Array.Resize(ref A, N * 2);
        A[N++] = v;
    }
}
