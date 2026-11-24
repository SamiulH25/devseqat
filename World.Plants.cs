using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// On a big map (docs/pre-demo/WORLD_SCALE.md WS4) the plants and rocks are not built at load: their items are laid into
/// tiles of their own, and a tile's mesh is built on a worker when the camera first sees it, sent to its own buffer, and
/// let go again when the buffers run past `PlantBudget` and it has not been seen for a while. The 4x draft has 41M
/// vertices of plants (1.5 GB); the drawn world (under `StreamFrom` items) is built at load as it always was.
/// Shots (`Ui.Instant`) build what they see at once. Drawing only.
sealed partial class World
{
    /// Foliage items past which a map's plants are streamed.
    public const int StreamFrom = 40000;
    const float PlantTileSize = 96;
    /// Vertices of plants kept on the GPU at once (about 290 MB), and sent up in one frame while playing.
    const int PlantBudget = 8_000_000, PlantUploadPerFrame = 1_500_000;
    /// Tiles a worker builds in one go, nearest the camera first.
    const int PlantBatch = 48;

    sealed class PlantTile
    {
        public Foliage[] Items = Array.Empty<Foliage>();
        public BoundingBox Box;
        public VertexPNCT[]? Fresh;
        public VertexBuffer? Vb;
        public int Tris, Used;
        public bool Queued;
    }

    readonly List<PlantTile> plantTiles = new();
    Task? plantTask;
    int plantFrame, plantResident;
    Vector3 plantEye;

    static bool Plant(FoliageKind k) => k is FoliageKind.Broadleaf or FoliageKind.Pine or FoliageKind.Bush or FoliageKind.Birch
        or FoliageKind.Shrub or FoliageKind.Tussock or FoliageKind.Heather or FoliageKind.Reed or FoliageKind.DeadTree
        or FoliageKind.Palm or FoliageKind.Flowers or FoliageKind.Marram or FoliageKind.Driftwood or FoliageKind.Rock;

    /// The foliage built at load. On a big map the plants are laid into streamed tiles instead and left out.
    IEnumerable<Foliage> LayPlants()
    {
        plantTiles.Clear();
        plantResident = 0;
        if (t.Foliage.Count < StreamFrom) return t.Foliage;
        foreach (var g in t.Foliage.Where(f => Plant(f.Kind))
                     .GroupBy(f => ((int)MathF.Floor(f.Pos.X / PlantTileSize), (int)MathF.Floor(f.Pos.Y / PlantTileSize))).OrderBy(g => g.Key))
        {
            var tile = new PlantTile { Items = g.ToArray() };
            // A box sure to hold what the items will build (the tallest tree is under 12 of its scale), before they are built.
            var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
            foreach (var f in tile.Items)
            {
                float s = MathF.Max(1, f.Scale) * treeScale, y = t.Height(f.Pos);
                min = Vector3.Min(min, new Vector3(f.Pos.X - 3 * s, y - 1, f.Pos.Y - 3 * s));
                max = Vector3.Max(max, new Vector3(f.Pos.X + 3 * s, y + 12 * s, f.Pos.Y + 3 * s));
            }
            tile.Box = new BoundingBox(min - new Vector3(1.5f), max + new Vector3(1.5f));
            plantTiles.Add(tile);
        }
        return t.Foliage.Where(f => !Plant(f.Kind));
    }

    void BuildPlantTile(PlantTile tile)
    {
        var m = new Mesh();
        foreach (var f in tile.Items) Item(m, f);
        tile.Fresh = m.V.ToArray();
    }

    /// Send built tiles to the GPU (main thread), a frame's share at a time unless a shot waits.
    void FlushPlants(bool all)
    {
        if (plantTask is { IsCompleted: false }) return;
        int sent = 0;
        foreach (var tile in plantTiles)
        {
            if (tile.Fresh is not { } v || (!all && sent > PlantUploadPerFrame)) continue;
            tile.Vb?.Dispose();
            tile.Vb = v.Length > 0 ? new VertexBuffer(gd, VertexPNCT.Decl, v.Length, BufferUsage.WriteOnly) : null;
            if (v.Length > 0) tile.Vb!.SetData(v);
            tile.Tris = v.Length / 3;
            tile.Fresh = null;
            tile.Queued = false;
            tile.Used = plantFrame;
            sent += v.Length;
            plantResident += v.Length;
        }
        if (plantResident > PlantBudget) Evict();
    }

    /// Let go of the tiles seen longest ago until the plants are back under budget.
    void Evict()
    {
        foreach (var tile in plantTiles.Where(x => x.Vb != null && x.Used < plantFrame - 2).OrderBy(x => x.Used).ToList())
        {
            if (plantResident <= PlantBudget * 3 / 4) break;
            plantResident -= tile.Tris * 3;
            tile.Vb!.Dispose();
            tile.Vb = null;
            tile.Tris = 0;
        }
    }

    /// Draw the streamed tiles inside `view` and ask for those not yet built (the camera's view only: the sun's sees too far).
    (int Tris, int Calls, int Tiles) DrawPlants(BoundingFrustum view, bool shadow)
    {
        int drawn = 0, calls = 0, seen = 0;
        if (plantTiles.Count == 0 || gd == null) return default;
        if (!shadow) plantFrame++;
        FlushPlants(Ui.Instant);
        List<PlantTile>? want = null;
        foreach (var tile in plantTiles)
        {
            if (!view.Intersects(tile.Box)) continue;
            if (tile.Vb == null)
            {
                if (!tile.Queued && tile.Fresh == null && (!shadow || Ui.Instant)) (want ??= new()).Add(tile);
                continue;
            }
            Send(tile);
        }
        if (want == null) return (drawn, calls, seen);
        if (Ui.Instant)
        {
            // A shot: build what it sees here and now.
            try { plantTask?.Wait(); } catch (AggregateException) { }
            Parallel.ForEach(want, BuildPlantTile);
            FlushPlants(true);
            foreach (var tile in want) if (tile.Vb != null) Send(tile);
            return (drawn, calls, seen);
        }
        if (plantTask is { IsCompleted: false }) return (drawn, calls, seen);
        var eye = new Vector2(plantEye.X, plantEye.Z);
        var batch = want.OrderBy(x => Vector2.DistanceSquared(eye, new Vector2((x.Box.Min.X + x.Box.Max.X) / 2, (x.Box.Min.Z + x.Box.Max.Z) / 2)))
            .Take(PlantBatch).ToList();
        foreach (var tile in batch) tile.Queued = true;
        plantTask = Task.Run(() => Parallel.ForEach(batch, BuildPlantTile));
        return (drawn, calls, seen);

        void Send(PlantTile tile)
        {
            tile.Used = plantFrame;
            if (tile.Tris == 0) return;
            gd.SetVertexBuffer(tile.Vb);
            for (int s = 0; s < tile.Tris; s += Chunk, calls++) gd.DrawPrimitives(PrimitiveType.TriangleList, s * 3, Math.Min(Chunk, tile.Tris - s));
            drawn += tile.Tris;
            seen++;
        }
    }

    /// Streamed plant tiles and their items (`--world-bench`).
    public (int Tiles, int Items) Streamed => (plantTiles.Count, plantTiles.Sum(x => x.Items.Length));
}
