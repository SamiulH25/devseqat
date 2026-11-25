using Microsoft.Xna.Framework;

namespace Ironfield;

/// Loads 3D campaign-map props compiled into Data/meshes/map_props.bin (IMSH v1) and emits them into World.Mesh.
static class MapProps3D
{
    static readonly Lazy<MeshKit3D?> Kit = new(TryLoad);

    static MeshKit3D? TryLoad()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var path = Path.Combine(dir, "Data", "meshes", "map_props.bin");
            if (File.Exists(path)) return MeshKit3D.Load(path);
        }
        return null;
    }

    static MeshKit3D.Vert[] ShipPart =>
        Kit.Value is { } kit && kit.Parts.TryGetValue("map_ship", out var v) ? v : Array.Empty<MeshKit3D.Vert>();

    public static bool HasShip => ShipPart.Length > 0;

    /// Emit the low-poly medieval cog at `at` facing `yaw` (+X forward in local space), scaled by `k`.
    /// Material slot 1 (sail and pennant) is tinted by `sail`; wood/hull keeps its baked vertex colors.
    public static void EmitShip(World.Mesh m, Vector3 at, float yaw, float k, Color sail)
    {
        var verts = ShipPart;
        if (verts.Length == 0) return;
        float cos = MathF.Cos(yaw), sin = MathF.Sin(yaw);
        for (int i = 0; i < verts.Length; i++)
        {
            ref readonly var v = ref verts[i];
            var pos = new Vector3(
                at.X + (v.Pos.X * cos - v.Pos.Z * sin) * k,
                at.Y + v.Pos.Y * k,
                at.Z + (v.Pos.X * sin + v.Pos.Z * cos) * k);
            var norm = new Vector3(
                v.Norm.X * cos - v.Norm.Z * sin,
                v.Norm.Y,
                v.Norm.X * sin + v.Norm.Z * cos);
            var col = v.MatId == 1
                ? new Color((byte)(v.R * sail.R / 255), (byte)(v.G * sail.G / 255), (byte)(v.B * sail.B / 255))
                : new Color(v.R, v.G, v.B);
            m.Raw(pos, norm, col, Vector2.Zero);
        }
    }
}
