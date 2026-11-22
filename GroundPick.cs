using Microsoft.Xna.Framework;

namespace Ironfield;

/// Where a ray from the camera meets the ground: marches along it in steps that shrink near the surface, then bisects.
/// Restored as the code `MapScreen` used before it was moved here (the pushed inventory commit called it without the file).
public static class GroundPick
{
    /// The ground point under the ray from `near` toward `far`, or null if it misses within `maxSteps` steps or `maxDist`.
    public static Vector2? March(Vector3 near, Vector3 far, Terrain terrain, int maxSteps, float maxDist)
    {
        var d = Vector3.Normalize(far - near);
        float Above(Vector3 p) => p.Y - terrain.Height(new Vector2(p.X, p.Z));
        float s = 0, step = 1;
        for (int i = 0; i < maxSteps && s < maxDist; i++)
        {
            var p = near + d * s;
            float h = Above(p);
            if (h > 0) { step = MathF.Max(0.5f, h * 0.5f); s += step; continue; }
            float lo = MathF.Max(0, s - step), hi = s;
            for (int k = 0; k < 10; k++) { float mid = (lo + hi) / 2; if (Above(near + d * mid) > 0) lo = mid; else hi = mid; }
            var hit = near + d * hi;
            return float.IsFinite(hit.X) && float.IsFinite(hit.Z) ? new Vector2(hit.X, hit.Z) : null;
        }
        return null;
    }
}
