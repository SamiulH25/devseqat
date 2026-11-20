using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// The campaign map's settlements (docs/SETTLEMENTS.md): built from their plans (`Terrain.Places`) in their own buffers, a
/// tile of the map each, so a place whose state changes (`PlaceState`: its owner's colours, ...) is rebuilt with its tile
/// alone, on a worker thread, and swapped in on the main thread. Drawing only.
sealed partial class World
{
    const float PlaceTileSize = 96;

    sealed class PlaceTile
    {
        public readonly List<PlacePlan> Plans = new();
        public BoundingBox Box;
        public VertexPNCT[]? Fresh; // built, waiting to go up to the GPU
        public VertexBuffer? Vb;
        public int Tris;
    }

    readonly List<PlaceTile> placeTiles = new();
    /// The state each place was last built in (read by the builder; written only while no build runs).
    readonly Dictionary<int, PlaceState> placeShown = new();
    Task? placeTask;
    /// Vertices of every place as built now (tests).
    internal int PlaceVerts => placeTiles.Sum(x => x.Tris * 3);
    /// Places triangles drawn last pass (render.txt).
    public (int Tiles, int Tris) PlaceCost { get; private set; }

    /// Lay the places into their tiles and build them all (the constructor, off the main thread). Their chimneys join the map's.
    void BuildPlaces(Func<int, PlaceState>? stateOf)
    {
        foreach (var g in t.Places.GroupBy(p => ((int)MathF.Floor(p.Pos.X / PlaceTileSize), (int)MathF.Floor(p.Pos.Y / PlaceTileSize))).OrderBy(g => g.Key))
        {
            var tile = new PlaceTile();
            tile.Plans.AddRange(g.OrderBy(p => p.Id));
            placeTiles.Add(tile);
        }
        foreach (var p in t.Places) placeShown[p.Id] = stateOf?.Invoke(p.Id) ?? PlaceState.None;
        var chimneys = new List<Vector3>[placeTiles.Count];
        Parallel.For(0, placeTiles.Count, i => chimneys[i] = BuildPlaceTile(placeTiles[i]));
        foreach (var c in chimneys) Chimneys.AddRange(c);
    }

    /// Build one tile's places into its `Fresh` mesh; returns their chimneys.
    List<Vector3> BuildPlaceTile(PlaceTile tile)
    {
        var m = new Mesh();
        foreach (var p in tile.Plans) Place(m, p, placeShown[p.Id], new Random(p.Seed));
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var v in m.V) { min = Vector3.Min(min, v.Position); max = Vector3.Max(max, v.Position); }
        tile.Box = m.V.Count > 0 ? new BoundingBox(min - new Vector3(1.5f), max + new Vector3(1.5f)) : default;
        tile.Tris = m.V.Count / 3;
        tile.Fresh = m.V.ToArray();
        return m.Chimneys;
    }

    /// Compare each place's state with the one it was built in, and rebuild the tiles of those that changed on a worker
    /// (`wait`: here and now, for staged shots). Call from the main thread; a build already running defers the check.
    public void UpdatePlaces(Func<int, PlaceState> stateOf, bool wait = false)
    {
        if (placeTask is { IsCompleted: false }) { if (!wait) return; placeTask.Wait(); }
        var dirty = new List<PlaceTile>();
        foreach (var tile in placeTiles)
        {
            bool changed = false;
            foreach (var p in tile.Plans)
            {
                var s = stateOf(p.Id);
                if (s == placeShown[p.Id]) continue;
                placeShown[p.Id] = s;
                changed = true;
            }
            if (changed) dirty.Add(tile);
        }
        if (dirty.Count == 0) return;
        placeTask = Task.Run(() => { foreach (var tile in dirty) BuildPlaceTile(tile); });
        if (wait) { placeTask.Wait(); FlushPlaces(); }
    }

    /// Send finished place meshes to the GPU (main thread).
    void FlushPlaces()
    {
        if (gd == null || placeTask is { IsCompleted: false }) return;
        foreach (var tile in placeTiles)
        {
            if (tile.Fresh is not { } v) continue;
            tile.Vb?.Dispose();
            tile.Vb = v.Length > 0 ? new VertexBuffer(gd, VertexPNCT.Decl, v.Length, BufferUsage.WriteOnly) : null;
            if (v.Length > 0) tile.Vb!.SetData(v);
            tile.Fresh = null;
        }
    }

    /// Draw the places' tiles inside `view` with the effect pass already applied.
    void DrawPlaces(BoundingFrustum view)
    {
        FlushPlaces();
        int tiles = 0, tris = 0;
        foreach (var tile in placeTiles)
        {
            if (tile.Vb == null || tile.Tris == 0 || !view.Intersects(tile.Box)) continue;
            gd.SetVertexBuffer(tile.Vb);
            for (int s = 0; s < tile.Tris; s += Chunk) gd.DrawPrimitives(PrimitiveType.TriangleList, s * 3, Math.Min(Chunk, tile.Tris - s));
            tiles++; tris += tile.Tris;
        }
        PlaceCost = (tiles, tris);
    }

    // ---------- the places ----------

    /// One settlement, as its plan and state say.
    void Place(Mesh m, PlacePlan p, PlaceState s, Random r)
    {
        var st = p.Look;
        var b = At(p.Pos, t.Height(p.Pos));
        switch (p.Kind)
        {
            case SettlementKind.Town: TownPlace(m, p, st, s, r); break;
            case SettlementKind.Village: Village(m, b, 1.8f, r); break; // SM3 remodels villages
            default:
                float k = Math.Clamp(p.Radius / WorldMap.FootprintOf(p.Kind, p.Size), 0.6f, 1);
                switch (p.Size)
                {
                    case 1: SmallHold(m, p, st, s, r, k); break;
                    case 3: GreatHold(m, p, st, s, r, k); break;
                    default: MidHold(m, p, st, s, r, k); break;
                }
                break;
        }
        m.Tag = Vector2.Zero;
    }

    /// The corners of a walled enclosure round the place: n of them, the front edge (corner n-1 to corner 0) square to the
    /// way the gate faces, each pulled in off water.
    Vector2[] Enclosure(PlacePlan p, int n, float radius, Random r, float jitter)
    {
        var ring = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            bool front = i == 0 || i == n - 1;
            float a = p.Front + MathF.PI / n + i * MathF.Tau / n + (front ? 0 : (r.NextSingle() - 0.5f) * 2 * jitter);
            float rad = radius * (front ? 0.96f : 0.86f + 0.14f * r.NextSingle());
            while (WetAt(p.Pos + Dir2(a) * rad, 0.8f) && rad > radius * 0.5f) rad *= 0.9f;
            ring[i] = p.Pos + Dir2(a) * rad;
        }
        return ring;
    }

    /// Flags of the place's holder on the given tops (none while it has no colours).
    static void Flags(Mesh m, PlaceState s, IEnumerable<Vector3> tops, float pole, float len, float yaw, float u)
    {
        if (s.Palette < 0 || s.Palette >= Style.Sides.Length) return;
        foreach (var top in tops) Flag(m, top, pole, len, yaw, Style.Sides[s.Palette], u);
    }

    /// The house a hold's people put in its yard: the town house, or the village one where that is all they build in timber.
    static HouseKind YardHouse(PlaceStyle st) => st.House is HouseKind.Tall ? (st.Defence == Defence.Brick ? HouseKind.Stucco : HouseKind.Timber) : st.House;

    /// A castle (size 2): a curtain with towers at its corners and a gatehouse facing the road, a keep at the back, a hall
    /// along one side, sheds against the walls. The timber peoples raise a motte and bailey instead.
    void MidHold(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, Random r, float k)
    {
        if (st.Defence == Defence.Palisade) { MotteAndBailey(m, p, st, s, r, k, great: false); return; }
        float u = k, R = p.Radius * 0.92f, wallH = 2.7f * u, thick = 0.62f * u, towerR = (st.Round ? 0.95f : 0.85f) * u;
        int n = st.Round ? 5 + r.Next(2) : 4 + r.Next(2);
        var ring = Enclosure(p, n, R, r, 0.14f);
        var col = WallColour(st, r);
        bool coursed = Coursed(st);
        var tops = new List<Vector3>();
        for (int i = 0; i < n; i++)
            tops.Add(Tower(m, st, ring[i], towerR, wallH * 1.45f * st.Tall, p.Front, st.Round, st.Cap, col, coursed, u));
        for (int i = 0; i + 1 < n; i++) Curtain(m, st, Shorten(ring[i], ring[i + 1], towerR * 0.6f), Shorten(ring[i + 1], ring[i], towerR * 0.6f), p.Pos, wallH, thick, col, u, r);
        var gate = (ring[n - 1] + ring[0]) / 2;
        var along = Vector2.Normalize(ring[0] - ring[n - 1]);
        float gateHalf = 1.05f * u + towerR * 1.3f;
        Curtain(m, st, Shorten(ring[n - 1], ring[0], towerR * 0.6f), gate - along * gateHalf, p.Pos, wallH, thick, col, u, r);
        Curtain(m, st, gate + along * gateHalf, Shorten(ring[0], ring[n - 1], towerR * 0.6f), p.Pos, wallH, thick, col, u, r);
        var gateTops = Gatehouse(m, st, gate, p.Front, wallH, u, col, r, towerR * 0.8f);
        var back = -Dir2(p.Front);
        var side = new Vector2(-back.Y, back.X) * (r.Next(2) == 0 ? 1 : -1);
        var keepTop = Keep(m, st, p.Pos + back * (R * 0.38f) + side * (R * 0.1f), 2.4f * u, wallH * 2.25f * st.Tall, p.Front, col, u, r, false);
        Dwelling(m, YardHouse(st), st, p.Pos - side * (R * 0.42f) + back * (R * 0.05f), 2.8f * u, 1.3f * u, 1.25f * u, p.Front - MathHelper.PiOver2 * MathF.Sign(Vector2.Dot(side, new Vector2(-back.Y, back.X))), r, chimney: true);
        Dwelling(m, YardHouse(st), st, p.Pos + side * (R * 0.4f) - back * (R * 0.32f), 1.4f * u, 1.0f * u, 0.9f * u, p.Front + MathHelper.PiOver2, r);
        Flags(m, s, new[] { keepTop + Vector3.Up * (st.Cap is Cap.Cone or Cap.Pyramid ? 1.6f * u : 0.1f) }, 1.5f * u, 1.2f * u, p.Front + 2.2f, u);
        Flags(m, s, gateTops.Take(1).Select(g => g + Vector3.Up * (st.Cap is Cap.Cone or Cap.Pyramid ? 1.4f * u : 0.1f)), 1.0f * u, 0.8f * u, p.Front + 2.2f, u);
    }

    /// A great castle (size 3): concentric. An outer curtain with its towers and gatehouse, an inner ward with higher walls and
    /// its own gate, the great keep with corner turrets, and the outer bailey's hall, chapel and stables.
    void GreatHold(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, Random r, float k)
    {
        if (st.Defence == Defence.Palisade) { MotteAndBailey(m, p, st, s, r, k, great: true); return; }
        float u = k, R = p.Radius * 0.93f;
        var col = WallColour(st, r);
        bool coursed = Coursed(st);
        // The outer curtain.
        int n = st.Round ? 7 : 6;
        float wallH = 2.4f * u, towerR = (st.Round ? 0.85f : 0.75f) * u;
        var ring = Enclosure(p, n, R, r, 0.1f);
        for (int i = 0; i < n; i++) Tower(m, st, ring[i], towerR, wallH * 1.4f * st.Tall, p.Front, st.Round, st.Cap, col, coursed, u);
        for (int i = 0; i + 1 < n; i++) Curtain(m, st, Shorten(ring[i], ring[i + 1], towerR * 0.6f), Shorten(ring[i + 1], ring[i], towerR * 0.6f), p.Pos, wallH, 0.6f * u, col, u, r);
        var gate = (ring[n - 1] + ring[0]) / 2;
        var along = Vector2.Normalize(ring[0] - ring[n - 1]);
        float gateHalf = 1.05f * u + towerR * 1.3f;
        Curtain(m, st, Shorten(ring[n - 1], ring[0], towerR * 0.6f), gate - along * gateHalf, p.Pos, wallH, 0.6f * u, col, u, r);
        Curtain(m, st, gate + along * gateHalf, Shorten(ring[0], ring[n - 1], towerR * 0.6f), p.Pos, wallH, 0.6f * u, col, u, r);
        var gateTops = Gatehouse(m, st, gate, p.Front, wallH, u, col, r, towerR * 0.85f);
        // The inner ward, a little back from the middle.
        var back = -Dir2(p.Front);
        var inner = p with { Pos = p.Pos + back * (R * 0.1f) };
        int ni = st.Round ? 5 : 4;
        float iwH = 3.3f * u, itR = (st.Round ? 1.0f : 0.9f) * u, iR = R * 0.52f;
        var iring = Enclosure(inner, ni, iR, r, 0.05f);
        var tops = new List<Vector3>();
        for (int i = 0; i < ni; i++) tops.Add(Tower(m, st, iring[i], itR, iwH * 1.4f * st.Tall, p.Front, st.Round, st.Cap, col, coursed, u));
        for (int i = 0; i + 1 < ni; i++) Curtain(m, st, Shorten(iring[i], iring[i + 1], itR * 0.6f), Shorten(iring[i + 1], iring[i], itR * 0.6f), inner.Pos, iwH, 0.7f * u, col, u, r);
        var igate = (iring[ni - 1] + iring[0]) / 2;
        var ialong = Vector2.Normalize(iring[0] - iring[ni - 1]);
        Curtain(m, st, Shorten(iring[ni - 1], iring[0], itR * 0.6f), igate - ialong * gateHalf, inner.Pos, iwH, 0.7f * u, col, u, r);
        Curtain(m, st, igate + ialong * gateHalf, Shorten(iring[0], iring[ni - 1], itR * 0.6f), inner.Pos, iwH, 0.7f * u, col, u, r);
        var igateTops = Gatehouse(m, st, igate, p.Front, iwH, u, col, r, itR * 0.8f);
        var keepTop = Keep(m, st, inner.Pos + back * (iR * 0.15f), 2.9f * u, iwH * 2.0f * st.Tall, p.Front, col, u, r, great: true);
        // The outer bailey: a hall, the chapel and the stables in the ring between the walls.
        var side = new Vector2(-back.Y, back.X);
        float mid = (R * 0.86f + iR) / 2;
        Dwelling(m, YardHouse(st), st, p.Pos + side * mid, 3.0f * u, 1.3f * u, 1.3f * u, p.Front, r, chimney: true);
        Chapel(m, st, p.Pos - side * mid + back * (R * 0.12f), p.Front + MathHelper.PiOver2, u * 0.85f, r);
        Dwelling(m, HouseKind.Timber == st.House ? HouseKind.Timber : YardHouse(st), st, p.Pos + back * (R * 0.78f), 2.8f * u, 1.0f * u, 0.85f * u, p.Front + MathHelper.PiOver2, r);
        Flags(m, s, new[] { keepTop + Vector3.Up * (st.Cap is Cap.Cone or Cap.Pyramid ? 2.0f * u : 0.1f) }, 1.8f * u, 1.5f * u, p.Front + 2.2f, u);
        Flags(m, s, gateTops.Concat(igateTops.Take(1)).Select(g => g + Vector3.Up * (st.Cap is Cap.Cone or Cap.Pyramid ? 1.4f * u : 0.1f)), 1.0f * u, 0.8f * u, p.Front + 2.2f, u);
    }

    /// The smallest hold (a tower house, HD5) as its people build it: a tower and its walled yard, a ringfort, a kasbah, a camp.
    void SmallHold(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, Random r, float k)
    {
        float u = k;
        var col = WallColour(st, r);
        bool coursed = Coursed(st);
        var fwd = Dir2(p.Front);
        var side = new Vector2(-fwd.Y, fwd.X);
        switch (st.Hold)
        {
            case Hold.Ringfort or Hold.Camp:
            {
                // A ring of bank and palisade with a gate tower; a hall (or the chief's yurt) inside, and a watch tower.
                int n = 9;
                float R = p.Radius * 0.85f, wallH = 2.0f * u;
                var ring = new Vector2[n];
                for (int i = 0; i < n; i++) ring[i] = p.Pos + Dir2(p.Front + MathF.PI / n + i * MathF.Tau / n) * R;
                for (int i = 0; i + 1 < n; i++) Curtain(m, st, ring[i], ring[i + 1], p.Pos, wallH, 0, col, u, r);
                var gate = (ring[n - 1] + ring[0]) / 2;
                var along = Vector2.Normalize(ring[0] - ring[n - 1]);
                Curtain(m, st, ring[n - 1], gate - along * (1.0f * u), p.Pos, wallH, 0, col, u, r);
                Curtain(m, st, gate + along * (1.0f * u), ring[0], p.Pos, wallH, 0, col, u, r);
                var gateTop = Gatehouse(m, st, gate, p.Front, wallH, u, col, r, 0);
                var tower = TimberTower(m, st, p.Pos - fwd * (R * 0.45f) + side * (R * 0.4f), 0.55f * u, 3.6f * u * st.Tall, p.Front, u, r);
                if (st.Hold == Hold.Camp)
                {
                    Chapel(m, st, p.Pos - fwd * (R * 0.15f) - side * (R * 0.15f), p.Front, u * 0.75f, r); // the chief's great yurt
                    for (int i = 0; i < 3; i++)
                    {
                        var at = p.Pos + Dir2(p.Front + 1.2f + i * 1.3f) * (R * 0.55f);
                        Dwelling(m, HouseKind.Yurt, st, at, 1.3f * u, 1.3f * u, 1.0f * u, p.Front + i, r);
                    }
                }
                else
                {
                    Dwelling(m, st.House, st, p.Pos - fwd * (R * 0.1f) - side * (R * 0.12f), 3.4f * u, 1.5f * u, 1.0f * u, p.Front + MathHelper.PiOver2, r, chimney: true);
                    Dwelling(m, st.House, st, p.Pos + fwd * (R * 0.35f) + side * (R * 0.42f), 1.5f * u, 1.0f * u, 0.8f * u, p.Front, r);
                }
                Flags(m, s, gateTop.Concat(new[] { tower + Vector3.Up * (2.0f * u) }), 1.1f * u, 0.9f * u, p.Front + 2.2f, u);
                break;
            }
            case Hold.Kasbah:
            {
                // A tighremt: a tall block of mud brick with a tapering tower at each corner, a walled yard before it.
                float w = 3.0f * u, h = 3.0f * u * st.Tall;
                var c = p.Pos - fwd * (0.6f * u);
                var (lo, hi) = GroundUnder(c, w, w, p.Front);
                var foot = At(c, lo - 0.3f);
                m.Tag = Vector2.Zero;
                m.Drum(foot, w / 2 * MathF.Sqrt(2), w / 2 * MathF.Sqrt(2) * (1 - st.Taper * 0.4f), hi + h - foot.Y, 4, p.Front + MathF.PI / 4, col);
                var top = At(c, hi + h);
                float tw = w / 2 * (1 - st.Taper * 0.4f);
                Merlons(m, top, tw, false, p.Front + MathF.PI / 4, col, false, u, false, 0.8f);
                foreach (var (n3, along3, _, _) in Faces(Dir3(p.Front + MathHelper.PiOver2), Dir3(p.Front + MathF.PI), w, w))
                    foreach (float y in new[] { 0.45f, 0.75f })
                        Window(m, At(c, hi + h * y) + n3 * (tw * 1.02f + (1 - y) * st.Taper * 0.5f * u), along3, n3, 0.2f * u, 0.32f * u, st.Trim);
                var tops = new List<Vector3>();
                for (int i = 0; i < 4; i++)
                    tops.Add(Tower(m, st, c + Dir2(p.Front + MathF.PI / 4 + i * MathHelper.PiOver2) * (w / 2 * MathF.Sqrt(2) * 0.95f), 0.6f * u, h * 1.3f, p.Front, false, Cap.Merlons, col, false, u));
                var yard = p.Pos + fwd * (1.6f * u);
                Curtain(m, st, yard - side * (1.8f * u) - fwd * (0.9f * u), yard - side * (1.8f * u) + fwd * (1.4f * u), yard, 1.3f * u, 0.35f * u, col, u, r);
                Curtain(m, st, yard + side * (1.8f * u) - fwd * (0.9f * u), yard + side * (1.8f * u) + fwd * (1.4f * u), yard, 1.3f * u, 0.35f * u, col, u, r);
                Curtain(m, st, yard - side * (1.8f * u) + fwd * (1.4f * u), yard - side * (0.6f * u) + fwd * (1.4f * u), yard, 1.3f * u, 0.35f * u, col, u, r);
                Curtain(m, st, yard + side * (0.6f * u) + fwd * (1.4f * u), yard + side * (1.8f * u) + fwd * (1.4f * u), yard, 1.3f * u, 0.35f * u, col, u, r);
                Window(m, At(c, hi + 0.65f * u) + Dir3(p.Front) * (w / 2 + 0.02f), Dir3(p.Front + MathHelper.PiOver2), Dir3(p.Front), 0.55f * u, 1.1f * u, st.DoorColour);
                Flags(m, s, tops.Take(1).Select(x => x + Vector3.Up * 0.1f), 1.2f * u, 0.9f * u, p.Front + 2.2f, u);
                break;
            }
            default:
            {
                // A tower house: a tall tower, bartizans on its corners, its yard (barmkin) walled before it with a byre inside.
                var at = p.Pos - fwd * (1.0f * u);
                float half = 1.15f * u, h = 5.0f * u * st.Tall;
                var top = Tower(m, st, at, half, h, p.Front, st.Round, st.Cap, col, coursed, u);
                if (!st.Round && st.Cap is Cap.Merlons or Cap.Swallow)
                    for (int i = 0; i < 2; i++)
                    {
                        var corner = At(at + Dir2(p.Front + MathF.PI / 4 + i * MathHelper.PiOver2 * 2) * (half * MathF.Sqrt(2)), top.Y - 0.9f * u);
                        m.Tag = coursed ? Courses : Vector2.Zero;
                        m.Drum(corner, 0.32f * u, 0.36f * u, 1.0f * u, 8, 0, col);
                        m.Tag = Vector2.Zero;
                        m.Spike(corner + Vector3.Up * (1.0f * u), 0.42f * u, 0.7f * u, 8, 0, st.Defence == Defence.Brick ? new Color(150, 70, 50) : new Color(84, 90, 100));
                    }
                var yard = p.Pos + fwd * (1.2f * u);
                float yw = 1.7f * u, yd = 1.4f * u, yh = 1.2f * u, yt = 0.32f * u;
                Curtain(m, st, yard - side * yw - fwd * yd, yard - side * yw + fwd * yd, yard, yh, yt, col, u, r);
                Curtain(m, st, yard + side * yw - fwd * yd, yard + side * yw + fwd * yd, yard, yh, yt, col, u, r);
                Curtain(m, st, yard - side * yw + fwd * yd, yard - side * (0.55f * u) + fwd * yd, yard, yh, yt, col, u, r);
                Curtain(m, st, yard + side * (0.55f * u) + fwd * yd, yard + side * yw + fwd * yd, yard, yh, yt, col, u, r);
                Dwelling(m, YardHouse(st), st, yard + side * (0.8f * u), 1.5f * u, 0.9f * u, 0.8f * u, p.Front + MathHelper.PiOver2, r, chimney: true);
                Flags(m, s, new[] { top + Vector3.Up * (st.Cap is Cap.Cone or Cap.Pyramid ? 2.2f * u : 0.1f) }, 1.3f * u, 1.0f * u, p.Front + 2.2f, u);
                break;
            }
        }
    }

    /// A timber castle: a bailey ringed by bank and palisade with timber towers and a gate tower, and at the back a motte, an
    /// earth mound with a palisade and a timber keep on top. Halls (or yurts) in the bailey. `great` is bigger, with more.
    void MotteAndBailey(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, Random r, float k, bool great)
    {
        float u = k, R = p.Radius * 0.9f, wallH = (great ? 2.4f : 2.1f) * u;
        var col = WallColour(st, r);
        var fwd = Dir2(p.Front);
        var side = new Vector2(-fwd.Y, fwd.X);
        int n = great ? 12 : 10;
        var ring = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = p.Front + MathF.PI / n + i * MathF.Tau / n, rad = R * (0.92f + 0.08f * r.NextSingle());
            while (WetAt(p.Pos + Dir2(a) * rad, 0.8f) && rad > R * 0.5f) rad *= 0.9f;
            ring[i] = p.Pos + Dir2(a) * rad;
        }
        for (int i = 0; i + 1 < n; i++) Curtain(m, st, ring[i], ring[i + 1], p.Pos, wallH, 0, col, u, r);
        var gate = (ring[n - 1] + ring[0]) / 2;
        var along = Vector2.Normalize(ring[0] - ring[n - 1]);
        Curtain(m, st, ring[n - 1], gate - along * (1.0f * u), p.Pos, wallH, 0, col, u, r);
        Curtain(m, st, gate + along * (1.0f * u), ring[0], p.Pos, wallH, 0, col, u, r);
        var gateTop = Gatehouse(m, st, gate, p.Front, wallH, u, col, r, 0);
        var flags = gateTop.ToList();
        for (int i = 1; i < n - 1; i += great ? 3 : 4)
            flags.Add(TimberTower(m, st, ring[i] + (p.Pos - ring[i]) * 0.06f, 0.55f * u, wallH * 1.6f * st.Tall, p.Front, u, r) + Vector3.Up * (1.6f * u));
        if (st.Hold == Hold.Camp)
        {
            // The khan's camp: his great yurt on a timber platform at the back, the yurts of his household round the yard.
            var seat = p.Pos - fwd * (R * 0.32f);
            var (lo0, hi0) = GroundUnder(seat, 3.4f * u, 3.4f * u, p.Front);
            m.Box(At(seat, lo0 - 0.3f), 3.4f * u, 3.4f * u, hi0 - lo0 + 0.3f + 0.35f * u, p.Front, Weathered(st, r));
            Dwelling(m, HouseKind.Yurt, st, seat, (great ? 3.0f : 2.6f) * u, (great ? 3.0f : 2.6f) * u, (great ? 2.0f : 1.7f) * u, p.Front, r, raised: hi0 + 0.35f * u);
            flags.Add(At(seat + side * (1.9f * u) + fwd * (1.2f * u), hi0));
            for (int i = 0; i < (great ? 8 : 6); i++)
            {
                var at = p.Pos + Dir2(p.Front + 0.75f + i * (MathF.Tau - 1.5f) / (great ? 7 : 5)) * (R * (0.55f + 0.12f * (i % 2)));
                if (Vector2.Distance(at, seat) < 2.6f * u) continue;
                Dwelling(m, HouseKind.Yurt, st, at, 1.3f * u, 1.3f * u, 1.0f * u, p.Front + i, r);
            }
        }
        else
        {
            // The motte at the back: an earth mound ringed with stakes, a timber keep on top.
            var motte = p.Pos - fwd * (R * 0.45f);
            float mr = (great ? 2.6f : 2.0f) * u, mh = (great ? 2.0f : 1.6f) * u;
            float ground = GroundUnder(motte, mr * 2, mr * 2, p.Front).Lo;
            m.Drum(At(motte, ground - 0.4f), mr, mr * 0.55f, mh + 0.4f, 9, p.Front, Jitter(new Color(104, 112, 70), r, 0.05f));
            float crown = ground + mh;
            for (int i = 0; i < 7; i++)
            {
                float a0 = p.Front + i * MathF.Tau / 7 + 0.4f, a1 = a0 + MathF.Tau / 7;
                var w0 = motte + Dir2(a0) * (mr * 0.5f); var w1 = motte + Dir2(a1) * (mr * 0.5f);
                Palisade(m, st with { Bank = false }, (w0 + w1) / 2, Vector2.Distance(w0, w1), MathF.Atan2(w1.Y - w0.Y, w1.X - w0.X), Vector2.Normalize((w0 + w1) / 2 - motte), crown - 0.2f, crown, 1.1f * u, u, r);
            }
            var keep = TimberTower(m, st, motte, (great ? 0.85f : 0.7f) * u, (great ? 3.2f : 2.6f) * u * st.Tall, p.Front, u, r, crown);
            flags.Add(keep + Vector3.Up * (1.8f * u));
            Dwelling(m, st.House, st, p.Pos + side * (R * 0.42f) + fwd * (R * 0.1f), 3.4f * u, 1.5f * u, 1.0f * u, p.Front, r, chimney: true);
            Dwelling(m, st.House, st, p.Pos - side * (R * 0.42f) + fwd * (R * 0.22f), 2.2f * u, 1.2f * u, 0.9f * u, p.Front, r, chimney: true);
            if (great)
            {
                Dwelling(m, st.House, st, p.Pos + fwd * (R * 0.55f) + side * (R * 0.12f), 1.6f * u, 1.0f * u, 0.8f * u, p.Front + MathHelper.PiOver2, r);
                Chapel(m, st, p.Pos - side * (R * 0.12f) + fwd * (R * 0.5f), p.Front + MathHelper.PiOver2, u * 0.75f, r);
            }
        }
        Flags(m, s, flags, 1.1f * u, 0.9f * u, p.Front + 2.2f, u);
    }

    /// a, moved toward b by `by` (a wall stops at a tower's face).
    static Vector2 Shorten(Vector2 a, Vector2 b, float by) => a + Vector2.Normalize(b - a) * by;
}
