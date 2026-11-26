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

    static MeshKit3D.Vert[] Part(string name) =>
        Kit.Value is { } kit && kit.Parts.TryGetValue(name, out var v) ? v : Array.Empty<MeshKit3D.Vert>();

    public static bool Has(string name) => Part(name).Length > 0;
    public static bool HasShip => Has("map_ship");

    /// Emit any IMSH part at `at` rotated by `yaw` (+X forward in local space), with non-uniform scale `(sx, sy, sz)`.
    public static void Emit(World.Mesh m, string name, Vector3 at, float yaw, float sx, float sy, float sz, Color? tintSlot1 = null)
    {
        var verts = Part(name);
        if (verts.Length == 0) return;
        float cos = MathF.Cos(yaw), sin = MathF.Sin(yaw);
        for (int i = 0; i < verts.Length; i++)
        {
            ref readonly var v = ref verts[i];
            float lx = v.Pos.X * sx, ly = v.Pos.Y * sy, lz = v.Pos.Z * sz;
            var pos = new Vector3(
                at.X + lx * cos - lz * sin,
                at.Y + ly,
                at.Z + lx * sin + lz * cos);
            var norm = Vector3.Normalize(new Vector3(
                v.Norm.X * cos - v.Norm.Z * sin,
                v.Norm.Y,
                v.Norm.X * sin + v.Norm.Z * cos));
            var col = tintSlot1 is { } tint && v.MatId == 1
                ? new Color((byte)(v.R * tint.R / 255), (byte)(v.G * tint.G / 255), (byte)(v.B * tint.B / 255))
                : new Color(v.R, v.G, v.B);
            m.Raw(pos, norm, col, Vector2.Zero);
        }
    }

    /// Emit the low-poly medieval cog at `at` facing `yaw` (+X forward in local space), scaled by `k`.
    public static void EmitShip(World.Mesh m, Vector3 at, float yaw, float k, Color sail) =>
        Emit(m, "map_ship", at, yaw, k, k, k, sail);

    /// Emit the spoked-wheel covered medieval wagon at `at` facing `yaw`, scaled by `k`.
    public static void EmitCart(World.Mesh m, Vector3 at, float yaw, float k) =>
        Emit(m, "map_cart", at, yaw, k, k, k);

    /// Emit an arched stone or cambered timber bridge spanning `[-reach, +reach]` along `yaw`, deck at `at.Y`.
    public static void EmitBridge(World.Mesh m, bool stone, Vector3 at, float yaw, float reach, float heightScale) =>
        Emit(m, stone ? "map_bridge_stone" : "map_bridge_wood", at, yaw, reach, heightScale, 1f);

    /// Emit a hero wayside site prop (`map_mill` or `map_ruin`) at `at` facing `yaw`, scaled by `k`.
    public static void EmitSite(World.Mesh m, string name, Vector3 at, float yaw, float k) =>
        Emit(m, name, at, yaw, k, k, k);
}
