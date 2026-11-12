using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Position + normal + colour + uv: lit, vertex-coloured (optionally textured) world geometry.
public struct VertexPNCT : IVertexType
{
    public Vector3 Position, Normal;
    public Color Color;
    public Vector2 UV;

    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

    public VertexPNCT(Vector3 p, Vector3 n, Color c, Vector2 uv) { Position = p; Normal = n; Color = c; UV = uv; }
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// The 3D world around the 2D units: lit heightmap terrain, low-poly foliage meshes (trees, pines, bushes, rocks, grass tufts)
/// and a gradient sky dome. Everything is built once per battle into static vertex buffers.
sealed partial class World : IDisposable
{
    public const float MinimapRange = 110; // the minimap shows +-this many metres around the centre
    public const float FogStart = 110, FogEnd = 330;
    public static Color Horizon => Sky.Horizon;
    public static Color Zenith => Sky.Zenith;
    public static Vector3 SunDir => Sky.SunDir; // light travels this way
    const int Chunk = 60000; // primitives per draw call

    public readonly Texture2D Minimap;
    /// The ground map (Act.GroundView, docs/archive/TERRAIN.md T6): each kind of ground in its own flat colour, height contours every
    /// 2 m, hedges, fences and houses. Same framing as the minimap.
    public readonly Texture2D GroundMap;
    readonly GraphicsDevice gd;
    readonly Terrain t;
    readonly VertexBuffer terrainVb, skyVb;
    VertexBuffer foliageVb;
    readonly IndexBuffer terrainIb;
    readonly int terrainTris, skyTris;
    readonly Texture2D detail;
    readonly BasicEffect terrainFx, foliageFx, skyFx;
    static readonly SamplerState DetailSampler = new()
    {
        Filter = TextureFilter.LinearMipPoint, AddressU = TextureAddressMode.Wrap, AddressV = TextureAddressMode.Wrap,
    };

    /// Trees and bushes drawn at this share of their size: the campaign map shrinks them so towns stand out (docs/archive/VISUALS.md V4).
    readonly float treeScale;

    public World(GraphicsDevice gd, Terrain terrain, float treeScale = 1)
    {
        this.gd = gd;
        t = terrain;
        this.treeScale = treeScale;
        (terrainVb, terrainIb, terrainTris) = BuildTerrain();
        (foliageVb, _) = BuildFoliage();
        (skyVb, skyTris) = BuildSky();
        detail = t.Grass ? BuildDetail() : BuildMaterials(); // the campaign map (no grass) gets its ground materials
        Minimap = BuildMinimap();
        GroundMap = BuildGroundMap();

        terrainFx = Lit(new BasicEffect(gd) { TextureEnabled = true, Texture = detail });
        foliageFx = Lit(new BasicEffect(gd));
        skyFx = new BasicEffect(gd) { VertexColorEnabled = true };
    }

    /// Build the foliage meshes again (after the gate of a besieged town breaks).
    public void RebuildFoliage()
    {
        foliageVb.Dispose();
        (foliageVb, _) = BuildFoliage();
    }

    BasicEffect Lit(BasicEffect fx)
    {
        fx.VertexColorEnabled = true;
        fx.LightingEnabled = true;
        fx.AmbientLightColor = Sky.SkyColor * 1.1f;
        fx.DirectionalLight0.Enabled = true;
        fx.DirectionalLight0.Direction = SunDir;
        fx.DirectionalLight0.DiffuseColor = Sky.SunColor * 0.8f;
        fx.DirectionalLight0.SpecularColor = Vector3.Zero;
        fx.DirectionalLight1.Enabled = fx.DirectionalLight2.Enabled = false;
        fx.SpecularPower = 1;
        fx.FogEnabled = true;
        fx.FogColor = Horizon.ToVector3();
        fx.FogStart = FogStart;
        fx.FogEnd = FogEnd;
        return fx;
    }

    public void Draw(Matrix view, Matrix proj, Vector3 camPos)
    {
        gd.RasterizerState = RasterizerState.CullNone;
        gd.BlendState = BlendState.Opaque;

        gd.DepthStencilState = DepthStencilState.None;
        skyFx.World = Matrix.CreateTranslation(camPos);
        skyFx.View = view; skyFx.Projection = proj;
        skyFx.CurrentTechnique.Passes[0].Apply();
        gd.SetVertexBuffer(skyVb);
        gd.DrawPrimitives(PrimitiveType.TriangleList, 0, skyTris);

        gd.DepthStencilState = DepthStencilState.Default;
        gd.SamplerStates[0] = DetailSampler;
        terrainFx.View = view; terrainFx.Projection = proj;
        terrainFx.CurrentTechnique.Passes[0].Apply();
        gd.SetVertexBuffer(terrainVb);
        gd.Indices = terrainIb;
        for (int s = 0; s < terrainTris; s += Chunk)
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, s * 3, Math.Min(Chunk, terrainTris - s));

        foliageFx.View = view; foliageFx.Projection = proj;
        foliageFx.CurrentTechnique.Passes[0].Apply();
        drawFrustum.Matrix = view * proj;
        DrawFoliage(drawFrustum, false);
    }

    /// Fog distances for the fixed-function path (the campaign map sees much further than a battle).
    public void SetFog(float start, float end)
    {
        terrainFx.FogStart = foliageFx.FogStart = start;
        terrainFx.FogEnd = foliageFx.FogEnd = end;
    }

    /// Shaded path: sky shader, then lit and shadowed terrain and foliage (see Shaders/world.glsl, sky.glsl).
    public void DrawShaded(Shading sh, Matrix view, Matrix proj, Vector3 camPos, float time, bool shadows)
    {
        gd.RasterizerState = RasterizerState.CullNone;
        gd.BlendState = BlendState.Opaque;

        gd.DepthStencilState = DepthStencilState.None;
        var sky = sh.Sky.Parameters;
        sky["ViewProj"].SetValue(Matrix.CreateTranslation(camPos) * view * proj);
        sky["SunDir"].SetValue(SunDir);
        sky["SunColor"].SetValue(Shading.SunColor);
        sky["Horizon"].SetValue(Horizon.ToVector3());
        sky["Zenith"].SetValue(Zenith.ToVector3());
        sky["Time"].SetValue(time);
        sky["Swirl"]?.SetValue(Sky.Swirl);
        sh.Sky.CurrentTechnique.Passes[0].Apply();
        gd.SetVertexBuffer(skyVb);
        gd.DrawPrimitives(PrimitiveType.TriangleList, 0, skyTris);

        gd.DepthStencilState = DepthStencilState.Default;
        var fx = sh.World;
        sh.Setup(fx, view * proj, camPos, time, shadows, 0.15f + sh.ShadowTexelMetres);
        fx.Parameters["Detail"].SetValue(detail);
        drawFrustum.Matrix = view * proj;
        DrawGeometry(fx, "Terrain", "Foliage", drawFrustum, false);
    }

    /// Terrain and foliage into the sun's shadow map (hills and trees cast shadows).
    public void DrawShadowCasters(Shading sh, float time)
    {
        var fx = sh.World;
        sh.Setup(fx, Matrix.Identity, sh.LightEye, time, false, 0);
        fx.Parameters["ShadowMap"].SetValue((Texture2D?)null);
        fx.Parameters["Detail"].SetValue(detail);
        drawFrustum.Matrix = sh.LightViewProj;
        DrawGeometry(fx, "TerrainShadow", "FoliageShadow", drawFrustum, true);
    }

    void DrawGeometry(Effect fx, string terrain, string foliage, BoundingFrustum view, bool shadow)
    {
        fx.CurrentTechnique = fx.Techniques[terrain];
        fx.CurrentTechnique.Passes[0].Apply();
        gd.SetVertexBuffer(terrainVb);
        gd.Indices = terrainIb;
        for (int s = 0; s < terrainTris; s += Chunk)
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, s * 3, Math.Min(Chunk, terrainTris - s));
        fx.CurrentTechnique = fx.Techniques[foliage];
        fx.CurrentTechnique.Passes[0].Apply();
        DrawFoliage(view, shadow);
        if (ExtraTris > 0)
        {
            int verts = ExtraTris * 3;
            if (extraVb == null || extraVb.IsDisposed || extraVb.VertexCount < Extra.Length)
            {
                extraVb?.Dispose();
                extraVb = new DynamicVertexBuffer(gd, typeof(VertexPNCT), Extra.Length, BufferUsage.WriteOnly);
                extraDirty = true;
            }
            if (extraDirty)
            {
                extraVb.SetData(Extra, 0, verts, SetDataOptions.Discard);
                extraDirty = false;
            }
            gd.SetVertexBuffer(extraVb);
            for (int s = 0; s < ExtraTris; s += Chunk)
                gd.DrawPrimitives(PrimitiveType.TriangleList, s * 3, Math.Min(Chunk, ExtraTris - s));
        }
    }

    /// Moving things lit like the foliage (the map's birds and beasts, MapLife.cs): rebuilt by their owner each frame,
    /// drawn in the shaded passes only (Low quality has none).
    public VertexPNCT[] Extra = Array.Empty<VertexPNCT>();
    public int ExtraTris;
    DynamicVertexBuffer? extraVb;
    bool extraDirty;
    readonly BoundingFrustum drawFrustum = new(Matrix.Identity);

    public void SetExtra(VertexPNCT[] v, int tris) { Extra = v; ExtraTris = tris; extraDirty = true; }

    public void Dispose()
    {
        terrainVb.Dispose(); terrainIb.Dispose(); foliageVb.Dispose(); skyVb.Dispose(); extraVb?.Dispose();
        detail.Dispose(); Minimap.Dispose();
        terrainFx.Dispose(); foliageFx.Dispose(); skyFx.Dispose();
    }

    // ---------- terrain ----------

    static float Smooth(float a, float b, float x)
    {
        float k = Math.Clamp((x - a) / (b - a), 0, 1);
        return k * k * (3 - 2 * k);
    }

    /// Ground colour: meadow greens, drier on high ground, dirt on steep slopes and the road.
    public Vector3 GroundColor(Vector2 p)
    {
        int s = t.Seed;
        float n = Terrain.Noise(p.X * 0.045f, p.Y * 0.045f, s + 7), n2 = Terrain.Noise(p.X * 0.19f, p.Y * 0.19f, s + 8);
        var c = Vector3.Lerp(new Vector3(0.36f, 0.52f, 0.24f), new Vector3(0.47f, 0.6f, 0.28f), 0.5f + 0.5f * n);
        c = Vector3.Lerp(c, new Vector3(0.58f, 0.6f, 0.34f), Smooth(4, 12, t.Height(p)) * 0.6f);           // dry hilltops
        c = Vector3.Lerp(c, new Vector3(0.48f, 0.42f, 0.32f), Smooth(0.35f, 0.7f, t.Gradient(p).Length())); // bare slopes
        float rd = t.RoadDist(p);
        if (t.RoadLines.Count > 0) c = Vector3.Lerp(c, new Vector3(0.5f, 0.47f, 0.33f), 0.45f * (1 - Smooth(2.6f, 6f + n2, rd))); // worn verges (the road itself is a ribbon)
        else c = Vector3.Lerp(c, new Vector3(0.56f, 0.47f, 0.34f), 1 - Smooth(1.8f, 3.4f + n2 * 0.6f, rd));   // road
        c = t.GroundAt(p) switch // the ground layer (TerrainGround.cs): you can see where a wood or a bog starts
        {
            GroundKind.Mud => Vector3.Lerp(c, new Vector3(0.34f, 0.29f, 0.2f), 0.7f + 0.1f * n),
            GroundKind.Stony => Vector3.Lerp(c, new Vector3(0.52f, 0.5f, 0.45f), 0.55f + 0.15f * n2),
            GroundKind.Scrub => Vector3.Lerp(c, new Vector3(0.33f, 0.37f, 0.19f), 0.5f),
            GroundKind.Wood => Vector3.Lerp(c, new Vector3(0.28f, 0.34f, 0.18f), 0.45f),
            GroundKind.DenseWood => Vector3.Lerp(c, new Vector3(0.2f, 0.26f, 0.14f), 0.65f),
            GroundKind.Sand => Vector3.Lerp(c, new Vector3(0.72f, 0.64f, 0.45f), 0.7f),
            GroundKind.Crops => Vector3.Lerp(c, new Vector3(0.64f, 0.57f, 0.3f), 0.7f),
            GroundKind.Street => Vector3.Lerp(c, new Vector3(0.5f, 0.45f, 0.38f), 0.7f),
            GroundKind.Bridge => new Vector3(0.45f, 0.33f, 0.21f), // the deck, as the minimap shows it
            _ => c,
        };
        if (t.Tint != null) c = t.Tint(p, c);
        if (t.WaterDepth(p) > 0) c = Vector3.Lerp(c, new Vector3(0.24f, 0.38f, 0.46f), t.WaterDepth(p) > 0.5f ? 0.9f : 0.6f); // the minimap shows water
        return c * (1 + n2 * 0.05f);
    }

    (VertexBuffer, IndexBuffer, int) BuildTerrain()
    {
        int n = t.N;
        var v = new VertexPNCT[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(-t.Half + x * t.Step, -t.Half + z * t.Step);
                float hl = t.GridHeight(Math.Max(0, x - 1), z), hr = t.GridHeight(Math.Min(n - 1, x + 1), z);
                float hd = t.GridHeight(x, Math.Max(0, z - 1)), hu = t.GridHeight(x, Math.Min(n - 1, z + 1));
                var normal = Vector3.Normalize(new Vector3(hl - hr, 2 * t.Step, hd - hu));
                var col = new Color(GroundColor(p));
                if (!t.Grass) col.A = (byte)(255 * (1 - Cavity(x, z))); // the map's shade in hollows (World.Materials.cs)
                v[z * n + x] = new VertexPNCT(new Vector3(p.X, t.GridHeight(x, z), p.Y), normal, col, p / 4);
            }
        // A big map needs 32-bit indices (the campaign map has far more than 65536 vertices).
        bool wide = v.Length > ushort.MaxValue;
        var idx = new int[(n - 1) * (n - 1) * 6];
        int k = 0;
        for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                // Split along (x+1, z)-(x, z+1), matching Terrain.Height.
                int a = z * n + x, b = a + 1, c = a + n, d = a + n + 1;
                idx[k++] = a; idx[k++] = b; idx[k++] = c;
                idx[k++] = d; idx[k++] = c; idx[k++] = b;
            }
        var vb = new VertexBuffer(gd, VertexPNCT.Decl, v.Length, BufferUsage.WriteOnly);
        vb.SetData(v);
        var ib = new IndexBuffer(gd, wide ? IndexElementSize.ThirtyTwoBits : IndexElementSize.SixteenBits, idx.Length, BufferUsage.WriteOnly);
        if (wide) ib.SetData(idx);
        else ib.SetData(idx.Select(i => (ushort)i).ToArray());
        return (vb, ib, idx.Length / 3);
    }

    Texture2D BuildDetail()
    {
        var rng = new Random(t.Seed + 5);
        var data = new Color[64 * 64];
        for (int i = 0; i < data.Length; i++)
        {
            int g = 214 + rng.Next(-22, 23) + (rng.Next(9) == 0 ? -30 : 0);
            data[i] = new Color(g, g, g);
        }
        return Art.Mipped(gd, data, 64);
    }

    Texture2D BuildMinimap()
    {
        // One texel every couple of metres on a battlefield, coarser on the campaign map (it covers hundreds of metres).
        float metresPerTexel = t.MinimapRange > 256 ? 4f : Terrain.MinimapMetresPerTexel;
        int S = Math.Clamp((int)MathF.Round(2 * t.MinimapRange / metresPerTexel), 128, 512);
        var data = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var w = MinimapToWorld(new Vector2((x + 0.5f) / S, (y + 0.5f) / S), t.MinimapRange);
                var g = t.Gradient(w);
                var nrm = Vector3.Normalize(new Vector3(-g.X, 1, -g.Y));
                float shade = 0.65f + 0.55f * MathF.Max(0, Vector3.Dot(nrm, -SunDir));
                data[y * S + x] = new Color(GroundColor(w) * shade * (Contour(w, metresPerTexel) ? 0.86f : 1));
            }
        MapFeatures(data, S, new Color(40, 56, 30), new Color(70, 52, 34));
        foreach (var f in t.Foliage)
        {
            if (f.Kind is not (FoliageKind.Broadleaf or FoliageKind.Pine)) continue;
            var m = WorldToMinimap(f.Pos, t.MinimapRange) * S;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = (int)m.X + dx, py = (int)m.Y + dy;
                    if (px >= 0 && py >= 0 && px < S && py < S && (dx == 0 || dy == 0)) data[py * S + px] = new Color(34, 70, 36);
                }
        }
        var tex = new Texture2D(gd, S, S);
        tex.SetData(data);
        return tex;
    }

    /// Minimap coordinates in 0..1 (x right, y down). It is drawn as seen from your side: enemy at the top, world -X to the right.
    public static Vector2 WorldToMinimap(Vector2 w, float range = MinimapRange) => new((-w.X + range) / (2 * range), (-w.Y + range) / (2 * range));
    public static Vector2 MinimapToWorld(Vector2 m, float range = MinimapRange) => new(range - m.X * 2 * range, range - m.Y * 2 * range);

    // ---------- sky ----------

    (VertexBuffer, int) BuildSky()
    {
        const int seg = 24, rings = 8;
        const float r = 300;
        var v = new List<VertexPositionColor>();
        Vector3 P(int i, float e) => new(MathF.Cos(i * MathF.Tau / seg) * MathF.Cos(e) * r, MathF.Sin(e) * r, MathF.Sin(i * MathF.Tau / seg) * MathF.Cos(e) * r);
        Color C(float e) => e <= 0 ? Horizon : Color.Lerp(Horizon, Zenith, MathF.Pow(e / MathHelper.PiOver2, 0.55f));
        for (int k = 0; k < rings; k++)
        {
            float e0 = MathHelper.Lerp(-0.35f, MathHelper.PiOver2, k / (float)rings), e1 = MathHelper.Lerp(-0.35f, MathHelper.PiOver2, (k + 1) / (float)rings);
            for (int i = 0; i < seg; i++)
            {
                VertexPositionColor A = new(P(i, e0), C(e0)), B = new(P(i + 1, e0), C(e0)), Cc = new(P(i, e1), C(e1)), D = new(P(i + 1, e1), C(e1));
                v.Add(A); v.Add(B); v.Add(Cc);
                v.Add(B); v.Add(D); v.Add(Cc);
            }
        }
        var vb = new VertexBuffer(gd, VertexPositionColor.VertexDeclaration, v.Count, BufferUsage.WriteOnly);
        vb.SetData(v.ToArray());
        return (vb, v.Count / 3);
    }

    // ---------- foliage ----------

    (VertexBuffer, int) BuildFoliage()
    {
        var m = new Mesh();
        tiles.Clear();
        grassTiles.Clear();
        Chimneys.Clear();
        // Foliage is built tile by tile (roadmap E4) so each frame draws only the tiles in view.
        const float TileSize = 48;
        foreach (var tile in t.Foliage.GroupBy(f => ((int)MathF.Floor(f.Pos.X / TileSize), (int)MathF.Floor(f.Pos.Y / TileSize))))
        {
            int start = m.V.Count;
            foreach (var f in tile) Item(f);
            if (m.V.Count == start) continue;
            var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
            for (int i = start; i < m.V.Count; i++) { min = Vector3.Min(min, m.V[i].Position); max = Vector3.Max(max, m.V[i].Position); }
            tiles.Add((start, (m.V.Count - start) / 3, new BoundingBox(min - new Vector3(1.5f), max + new Vector3(1.5f)))); // room for wind sway
        }
        Fields(m); // tiled too (World.Land.cs)
        Roads(m); // tiled like the foliage, so only the roads in view are drawn (World.Water.cs)
        alwaysStart = m.V.Count;
        Water(m);
        Features(m);
        if (t.Wall != null) Torches(m, t);
        if (!t.Grass) Skirt(m);
        else if (t.Wall == null) Outskirts(m); // a farmstead out past the fighting (World.Land.cs, WL6)
        if (m.V.Count == 0) m.Tri(Vector3.Zero, Vector3.Zero, Vector3.Zero, Color.Black, Vector3.Down); // keep the buffer valid on a bare field
        alwaysTris = (m.V.Count - alwaysStart) / 3;
        Grass(m);
        var vb = new VertexBuffer(gd, VertexPNCT.Decl, Math.Max(3, m.V.Count), BufferUsage.WriteOnly);
        if (m.V.Count > 0) vb.SetData(m.V.ToArray());
        return (vb, m.V.Count / 3);

        void Item(Foliage f)
        {
            var rng = new Random(f.Seed);
            var b = new Vector3(f.Pos.X, t.Height(f.Pos), f.Pos.Y);
            switch (f.Kind)
            {
                case FoliageKind.Broadleaf: Broadleaf(m, b, f.Scale * treeScale, rng); break;
                case FoliageKind.Pine: Pine(m, b, f.Scale * treeScale, rng); break;
                case FoliageKind.Bush: Bush(m, b, f.Scale * treeScale, rng); break;
                case FoliageKind.Birch: Birch(m, b, f.Scale * treeScale, rng); break;
                case FoliageKind.Village: Village(m, b, f.Scale, rng); break;
                case FoliageKind.Town: Town(m, b, f.Scale, rng); break;
                case FoliageKind.Castle: Castle(m, b, f.Scale, rng); break;
                case FoliageKind.Wall: WallStones(m, b, f.Scale, rng); break;
                case FoliageKind.Tower: Tower(m, b, rng); break;
                case FoliageKind.Gate: GateDoor(m, b, t.Wall?.GateOpen == true, rng); break;
                case FoliageKind.Ladder: Ladder(m, b); break;
                case FoliageKind.House: TownHouse(m, b, f.Scale, rng); break;
                case FoliageKind.Wagon: Wagon(m, b, rng); break;
                case FoliageKind.Ruin or FoliageKind.Shrine or FoliageKind.Stones or FoliageKind.Field or FoliageKind.Mine
                    or FoliageKind.Hut or FoliageKind.Watch or FoliageKind.Spring or FoliageKind.Mill: SiteModel(m, f.Kind, b, f.Scale, rng); break;
                default: Rock(m, b, f.Scale, rng); break;
            }
        }
    }

    readonly List<(int Start, int Tris, BoundingBox Box)> tiles = new();
    readonly List<(int Start, int Tris, BoundingBox Box)> grassTiles = new();
    int alwaysStart, alwaysTris;
    /// Foliage triangles drawn last frame (for the performance check).
    public int FoliageDrawn { get; private set; }

    /// Draw the foliage tiles inside `view` (a camera's or the sun's frustum), then the water and grass.
    void DrawFoliage(BoundingFrustum view, bool shadow)
    {
        gd.SetVertexBuffer(foliageVb);
        int drawn = 0;
        void Range(int start, int tris)
        {
            for (int s = 0; s < tris; s += Chunk) gd.DrawPrimitives(PrimitiveType.TriangleList, start + s * 3, Math.Min(Chunk, tris - s));
            drawn += tris;
        }
        foreach (var (start, tris, box) in tiles) if (view.Intersects(box)) Range(start, tris);
        if (!shadow) foreach (var (start, tris, box) in grassTiles) if (view.Intersects(box)) Range(start, tris);
        if (alwaysTris > 0) Range(alwaysStart, alwaysTris);
        FoliageDrawn = drawn;
    }

    static Color Jitter(Color c, Random r, float amount)
    {
        float k = 1 + (r.NextSingle() * 2 - 1) * amount;
        return new Color(c.ToVector3() * k);
    }

    static void Broadleaf(Mesh m, Vector3 b, float s, Random r)
    {
        float th = (2.0f + r.NextSingle() * 1.0f) * s;
        m.Tag = Vector2.Zero;
        m.Frustum(b - Vector3.Up * 0.4f, b + Vector3.Up * th, 0.27f * s, 0.16f * s, 6, Jitter(new Color(96, 70, 46), r, 0.1f), r);
        m.Tag = new Vector2(0.35f, 1);
        var leaf = Color.Lerp(new Color(58, 104, 46), new Color(92, 132, 56), r.NextSingle());
        m.Blob(b + new Vector3(0, th + 1.1f * s, 0), new Vector3(1.9f, 1.55f, 1.9f) * s, leaf, r, 0.18f);
        int extra = 1 + r.Next(2);
        for (int i = 0; i < extra; i++)
        {
            float a = r.NextSingle() * MathF.Tau;
            var off = new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * 0.95f * s + Vector3.Up * (th + (0.5f + r.NextSingle() * 0.8f) * s);
            m.Blob(b + off, new Vector3(1.3f, 1.1f, 1.3f) * s, Jitter(leaf, r, 0.08f), r, 0.2f);
        }
    }

    static void Pine(Mesh m, Vector3 b, float s, Random r)
    {
        m.Tag = Vector2.Zero;
        m.Frustum(b - Vector3.Up * 0.4f, b + Vector3.Up * 1.8f * s, 0.22f * s, 0.12f * s, 6, Jitter(new Color(90, 64, 42), r, 0.1f), r);
        m.Tag = new Vector2(0.25f, 0.7f);
        var needles = Color.Lerp(new Color(34, 78, 50), new Color(52, 98, 58), r.NextSingle());
        int tiers = 4;
        for (int i = 0; i < tiers; i++)
        {
            float y0 = (1.1f + i * 1.3f) * s, rad = (2.1f - i * 0.42f) * s;
            m.Frustum(b + Vector3.Up * y0, b + Vector3.Up * (y0 + 2.5f * s), rad, 0, 9, Jitter(needles, r, 0.08f), r, capBottom: true);
        }
    }

    static void Bush(Mesh m, Vector3 b, float s, Random r)
    {
        var leaf = Color.Lerp(new Color(52, 98, 44), new Color(86, 122, 52), r.NextSingle());
        m.Tag = new Vector2(0.2f, 1);
        m.Blob(b + Vector3.Up * 0.3f * s, new Vector3(0.95f, 0.7f, 0.95f) * s, leaf, r, 0.22f);
        if (r.Next(2) == 0)
            m.Blob(b + new Vector3(0.6f * s, 0.2f * s, 0.2f * s), new Vector3(0.6f, 0.5f, 0.6f) * s, Jitter(leaf, r, 0.1f), r, 0.2f);
    }

    static void Rock(Mesh m, Vector3 b, float s, Random r)
    {
        var stone = Jitter(new Color(128, 126, 122), r, 0.12f);
        m.Tag = Vector2.Zero;
        m.Blob(b + Vector3.Up * 0.12f * s, new Vector3(1.0f, 0.62f, 0.85f) * s, stone, r, 0.28f);
    }

    // ---------- settlements (campaign map) ----------

    static readonly Color Stone = new(150, 146, 136), Wattle = new(196, 176, 136), Thatch = new(150, 118, 66), Tile = new(150, 66, 48), Slate = new(84, 92, 106);

    /// A cottage: stone plinth, timber-framed walls, framed door/windows and an overhanging gable roof with ridge beam.
    static void House(Mesh m, Vector3 c, float w, float d, float h, float yaw, Color wall, Color roof)
    {
        m.Tag = Vector2.Zero;
        float plinth = MathF.Min(h * 0.18f, 0.45f), post = MathF.Max(0.06f, MathF.Min(w, d) * 0.045f);
        var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        var az = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw));
        var beam = new Color(Timber.ToVector3() * 0.85f);
        m.Box(c - Vector3.Up * 0.1f, w * 1.03f, d * 1.03f, plinth + 0.1f, yaw, Stone);
        m.Box(c + Vector3.Up * plinth, w, d, h - plinth, yaw, wall);
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                m.Box(c + ax * (sx * (w / 2 - post * 0.35f)) + az * (sz * (d / 2 - post * 0.35f)), post * 1.3f, post * 1.3f, h, yaw, beam);
        m.Box(c + Vector3.Up * (h - post * 0.9f), w + 0.06f, d + 0.06f, post, yaw, beam);
        if (w > 3.5f)
        {
            m.Box(c + Vector3.Up * (h * 0.52f), w + 0.04f, d + 0.04f, post * 0.85f, yaw, beam);
            foreach (float sz in new[] { -1f, 1f })
                m.Box(c + az * (sz * (d / 2 + 0.01f)), post * 0.9f, 0.05f, h - plinth, yaw, beam);
            float dw = MathF.Min(w * 0.16f, h * 0.42f), dh = h * 0.58f;
            var doorAt = c + az * (d / 2 + 0.02f);
            m.Box(doorAt, dw + post * 0.9f, 0.05f, dh + post * 0.5f, yaw, beam);
            m.Box(doorAt + az * 0.02f, dw, 0.04f, dh, yaw, new Color(Timber.ToVector3() * 0.65f));
        }
        m.Gable(c + Vector3.Up * (h - 0.04f), w * 1.14f, d * 1.22f, h * 0.72f, yaw, roof);
        m.Box(c + Vector3.Up * (h + h * 0.68f - post * 0.5f), w * 1.16f, post * 1.35f, post * 1.2f, yaw, beam);
        Windows(m, c, w, d, h, yaw);
    }

    /// Small framed windows on a house's long walls, tagged so the shader shows them dark by day and lamplit at night (WL5).
    static void Windows(Mesh m, Vector3 c, float w, float d, float h, float yaw)
    {
        var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        var az = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw));
        float ww = MathF.Min(w * 0.16f, h * 0.38f), wh = h * 0.34f, y = c.Y + h * 0.42f;
        var glass = new Color(46, 40, 34);
        var frame = new Color(Timber.ToVector3() * 0.75f);
        foreach (float side in new[] { -1f, 1f })
            foreach (float along in new[] { -0.26f, 0.26f })
            {
                var n = az * side;
                var mid = new Vector3(c.X, y, c.Z) + ax * along * w + n * (d / 2 + 0.02f);
                m.Box(mid - Vector3.Up * (wh * 0.12f), ww + 0.12f, 0.05f, wh + 0.12f, yaw, frame);
                Vector3 a = mid - ax * ww / 2, b = mid + ax * ww / 2, up = Vector3.Up * wh;
                var gn = n * 0.028f;
                var tag = new Vector2(0, -3);
                m.Raw(a + gn, n, glass, tag); m.Raw(b + gn, n, glass, tag); m.Raw(b + up + gn, n, glass, tag);
                m.Raw(a + gn, n, glass, tag); m.Raw(b + up + gn, n, glass, tag); m.Raw(a + up + gn, n, glass, tag);
                if (w > 3.5f)
                {
                    m.Box(mid + gn * 1.15f, 0.06f, 0.03f, wh, yaw, frame);
                    m.Box(mid + Vector3.Up * (wh * 0.48f) + gn * 1.15f, ww, 0.03f, 0.06f, yaw, frame);
                }
            }
    }

    /// Where smoke rises from the map's chimneys (drawn by `MapLife`, WL5).
    public readonly List<Vector3> Chimneys = new();

    /// A chimney stack at the end of a cottage's ridge.
    void Chimney(Mesh m, Vector3 c, float w, float h, float yaw)
    {
        var at = c + new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw)) * w * 0.32f + Vector3.Up * h * 1.1f;
        m.Tag = Vector2.Zero;
        m.Box(at, w * 0.12f, w * 0.12f, h * 0.75f, yaw, Stone);
        Chimneys.Add(at + Vector3.Up * h * 0.75f);
    }

    void Village(Mesh m, Vector3 b, float s, Random r)
    {
        // Cottages round a green, big enough to hold their own beside the fields and the beasts (WL3).
        int n = 5 + r.Next(3);
        for (int i = 0; i < n; i++)
        {
            float a = (i + r.NextSingle() * 0.6f) * MathF.Tau / n, d = (1.3f + r.NextSingle() * 1.4f) * s, yaw = a + MathF.PI / 2 + (r.NextSingle() - 0.5f) * 0.6f;
            var at = b + new Vector3(MathF.Cos(a) * d, -0.1f, MathF.Sin(a) * d);
            House(m, at, 1.35f * s, 0.9f * s, 0.75f * s, yaw, Jitter(Wattle, r, 0.08f), Jitter(Thatch, r, 0.1f));
            if (i % 2 == 0) Chimney(m, at, 1.35f * s, 0.75f * s, yaw);
        }
    }

    void Town(Mesh m, Vector3 b, float s, Random r)
    {
        m.Tag = Vector2.Zero;
        float rad = 3.6f * s, wallH = 1.0f * s;
        const int segs = 14;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * MathF.Tau / segs, a1 = (i + 1) * MathF.Tau / segs;
            var p0 = b + new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)) * rad;
            var p1 = b + new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1)) * rad;
            m.Box((p0 + p1) / 2 - Vector3.Up * 0.3f * s, Vector3.Distance(p0, p1) * 1.04f, 0.35f * s, wallH + 0.3f * s, MathF.Atan2(p1.Z - p0.Z, p1.X - p0.X), Stone);
            if (i % 3 == 0)
            {
                m.Frustum(p0 - Vector3.Up * 0.3f * s, p0 + Vector3.Up * wallH * 1.5f, 0.45f * s, 0.42f * s, 8, Stone, r);
                m.Frustum(p0 + Vector3.Up * wallH * 1.5f, p0 + Vector3.Up * wallH * 2.3f, 0.55f * s, 0, 8, Tile, r, capBottom: true);
            }
        }
        for (int i = 0; i < 12; i++)
        {
            float a = r.NextSingle() * MathF.Tau, d = (0.9f + r.NextSingle() * 2.0f) * s;
            float hh = (0.6f + r.NextSingle() * 0.4f) * s, yaw = r.NextSingle() * MathF.Tau;
            var at = b + new Vector3(MathF.Cos(a) * d, -0.1f, MathF.Sin(a) * d);
            House(m, at, 0.8f * s, 0.6f * s, hh, yaw, Jitter(Wattle, r, 0.06f), Jitter(Tile, r, 0.12f));
            if (i % 4 == 0) Chimney(m, at, 0.8f * s, hh, yaw);
        }
        // Church or hall in the middle.
        House(m, b, 1.4f * s, 0.8f * s, 1.1f * s, 0.3f, Stone, Slate);
        m.Frustum(b + new Vector3(0.6f * s, 0, 0), b + new Vector3(0.6f * s, 2.2f * s, 0), 0.3f * s, 0.28f * s, 6, Stone, r);
        m.Frustum(b + new Vector3(0.6f * s, 2.2f * s, 0), b + new Vector3(0.6f * s, 3.0f * s, 0), 0.36f * s, 0, 6, Slate, r, capBottom: true);
    }

    // ---------- sites (campaign map, docs/archive/SIDE_CONTENT.md S2) ----------

    static readonly Color Moss = new(96, 110, 78), Earth = new(96, 78, 58), Iron = new(70, 72, 80);

    /// A site's model: small enough to sit between the villages, but each with a shape you can tell from the others.
    static void SiteModel(Mesh m, FoliageKind kind, Vector3 b, float s, Random r)
    {
        m.Tag = Vector2.Zero;
        switch (kind)
        {
            case FoliageKind.Ruin: // broken walls of uneven height around a fallen tower
                for (int i = 0; i < 7; i++)
                {
                    float a = i * MathF.Tau / 7, h = (0.3f + r.NextSingle() * 1.1f) * s;
                    m.Box(b + new Vector3(MathF.Cos(a), -0.1f, MathF.Sin(a)) * 2.2f * s, 1.6f * s, 0.35f * s, h, a + MathF.PI / 2, Jitter(Stone, r, 0.1f));
                }
                m.Frustum(b - Vector3.Up * 0.1f, b + Vector3.Up * 1.9f * s, 0.7f * s, 0.6f * s, 7, Jitter(Stone, r, 0.08f), r);
                for (int i = 0; i < 4; i++) m.Blob(b + new Vector3(r.NextSingle() * 3 - 1.5f, 0, r.NextSingle() * 3 - 1.5f) * s, new Vector3(0.4f, 0.25f, 0.4f) * s, Moss, r, 0.3f);
                break;
            case FoliageKind.Shrine: // a little chapel with a spire
                House(m, b, 1.0f * s, 0.7f * s, 0.8f * s, r.NextSingle() * MathF.Tau, Stone, Slate);
                m.Frustum(b + Vector3.Up * 1.1f * s, b + Vector3.Up * 2.2f * s, 0.18f * s, 0, 5, Slate, r);
                m.Box(b + new Vector3(1.3f, 0, 0.8f) * s, 0.12f * s, 0.12f * s, 0.9f * s, 0, Timber);
                m.Box(b + new Vector3(1.3f, 0.6f, 0.8f) * s, 0.5f * s, 0.12f * s, 0.12f * s, 0, Timber);
                break;
            case FoliageKind.Stones: // a ring of standing stones
                for (int i = 0; i < 9; i++)
                {
                    float a = i * MathF.Tau / 9;
                    m.Box(b + new Vector3(MathF.Cos(a), -0.1f, MathF.Sin(a)) * 2.0f * s, 0.45f * s, 0.3f * s, (1.0f + r.NextSingle() * 0.7f) * s, a, Jitter(new Color(140, 140, 136), r, 0.08f));
                }
                m.Box(b + new Vector3(0, -0.1f, 0), 1.0f * s, 0.6f * s, 0.35f * s, r.NextSingle(), new Color(120, 120, 118));
                break;
            case FoliageKind.Field: // old grave mounds, stakes and a leaning banner pole
                for (int i = 0; i < 5; i++) m.Blob(b + new Vector3(i * 0.9f - 1.8f, 0, (i % 2) * 1.2f - 0.6f) * s, new Vector3(0.5f, 0.25f, 0.9f) * s, Earth, r, 0.15f);
                for (int i = 0; i < 6; i++)
                {
                    var foot = b + new Vector3(r.NextSingle() * 4 - 2, 0, r.NextSingle() * 4 - 2) * s;
                    m.Frustum(foot, foot + new Vector3(r.NextSingle() - 0.5f, 1.1f, r.NextSingle() - 0.5f) * s, 0.06f * s, 0.02f * s, 4, Timber, r);
                }
                m.Frustum(b + new Vector3(2.2f, 0, -1.4f) * s, b + new Vector3(2.6f, 2.4f, -1.2f) * s, 0.06f * s, 0.05f * s, 4, Timber, r);
                m.Box(b + new Vector3(2.6f, 1.6f, -1.2f) * s, 0.8f * s, 0.05f * s, 0.6f * s, 0.2f, new Color(130, 40, 34));
                break;
            case FoliageKind.Mine: // a timbered adit in a spoil heap, with a cart
                m.Blob(b, new Vector3(2.4f, 1.3f, 1.8f) * s, Earth, r, 0.2f);
                m.Box(b + new Vector3(0, 0, 1.6f) * s, 1.0f * s, 0.4f * s, 0.9f * s, 0, new Color(24, 22, 22));
                m.Box(b + new Vector3(0, 0.9f, 1.7f) * s, 1.3f * s, 0.3f * s, 0.18f * s, 0, Timber);
                m.Box(b + new Vector3(1.6f, 0.15f, 2.4f) * s, 0.7f * s, 0.5f * s, 0.4f * s, 0.4f, Iron);
                break;
            case FoliageKind.Hut: // a low round hut of wattle with a turf roof, and a woodpile
                m.Frustum(b - Vector3.Up * 0.1f, b + Vector3.Up * 0.6f * s, 0.8f * s, 0.75f * s, 8, Wattle, r);
                m.Frustum(b + Vector3.Up * 0.6f * s, b + Vector3.Up * 1.4f * s, 1.0f * s, 0, 8, Moss, r, capBottom: true);
                m.Box(b + new Vector3(1.3f, 0, 0.2f) * s, 0.8f * s, 0.4f * s, 0.35f * s, 0.5f, Timber);
                break;
            case FoliageKind.Watch: // a tall square tower with merlons
                m.Box(b - Vector3.Up * 0.2f, 1.3f * s, 1.3f * s, 4.2f * s, 0.3f, Jitter(Stone, r, 0.05f));
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 * 2 - 1) * 0.5f, z = (i / 2 * 2 - 1) * 0.5f;
                    m.Box(b + new Vector3(x, 4.0f, z) * s, 0.35f * s, 0.35f * s, 0.35f * s, 0.3f, Stone);
                }
                break;
            case FoliageKind.Spring: // a pool ringed with stones
                m.Box(b + Vector3.Up * 0.02f, 2.0f * s, 1.6f * s, 0.06f, 0.4f, new Color(86, 124, 132));
                for (int i = 0; i < 10; i++)
                {
                    float a = i * MathF.Tau / 10;
                    m.Blob(b + new Vector3(MathF.Cos(a) * 1.3f, 0.05f, MathF.Sin(a) * 1.05f) * s, new Vector3(0.3f, 0.2f, 0.3f) * s, new Color(132, 130, 124), r, 0.25f);
                }
                break;
            default: // the mill: a house with a big wheel on its side
                House(m, b, 1.4f * s, 1.0f * s, 1.0f * s, 0, Wattle, Thatch);
                for (int i = 0; i < 12; i++) // the wheel's rim, standing against the end wall
                {
                    float a = i * MathF.Tau / 12;
                    m.Box(b + new Vector3(0.95f * s + 0.1f, 0.85f * s + MathF.Sin(a) * 0.75f * s - 0.1f * s, MathF.Cos(a) * 0.75f * s), 0.2f * s, 0.25f * s, 0.2f * s, 0, Timber);
                }
                break;
        }
    }

    // ---------- a besieged town (Terrain.SiegeWall) ----------

    static readonly Color WallStone = new(150, 146, 136), Timber = new(104, 72, 44), Footing = new(112, 110, 100);
    /// Foliage UV tags the world shader reads (leafiness below zero): dressed stone (courses drawn in the shader) and
    /// flames (unlit, flickering).
    static readonly Vector2 MasonryTag = new(0, -1), FlameTag = new(0, -2);

    /// A length of wall: its top is the walkway (the terrain carries the units), faced in stone, with merlons on the outer edge
    /// and a darker battered footing where the rain splashes and the moss grows.
    static void WallStones(Mesh m, Vector3 b, float len, Random r)
    {
        m.Tag = MasonryTag;
        const float h = SiegeWall.Height, d = SiegeWall.Top1 - SiegeWall.Outer;
        var c = new Vector3(b.X, b.Y - h - 2, SiegeWall.Outer + d / 2);
        m.Box(c, len + 0.05f, d, h + 2.04f, 0, Jitter(WallStone, r, 0.05f));
        m.Box(c - Vector3.UnitZ * 0.25f, len + 0.06f, d + 0.5f, 3.1f, 0, Jitter(Footing, r, 0.04f));
        for (float x = -len / 2 + 0.5f; x < len / 2; x += 1.6f)
            m.Box(new Vector3(b.X + x, b.Y, SiegeWall.Outer + 0.3f), 0.8f, 0.6f, 0.75f, 0, Jitter(WallStone, r, 0.06f));
        m.Tag = Vector2.Zero;
    }

    static void Tower(Mesh m, Vector3 b, Random r)
    {
        m.Tag = MasonryTag;
        var foot = new Vector3(b.X, b.Y - SiegeWall.Height - 2, SiegeWall.Outer + 1.5f);
        m.Box(foot, 4.2f, 5.4f, SiegeWall.Height + 5.5f, 0, Jitter(WallStone, r, 0.04f));
        m.Box(foot, 4.7f, 5.9f, 3.2f, 0, Jitter(Footing, r, 0.04f));
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 * 2 - 1) * 1.7f, z = (i / 2 * 2 - 1) * 2.3f;
            m.Box(foot + new Vector3(x, SiegeWall.Height + 5.5f, z), 0.8f, 0.8f, 0.8f, 0, WallStone);
        }
        m.Tag = Vector2.Zero;
    }

    /// Torches every `TorchSpacing` m along the outer face of the wall (and over the gate). The world and sprite shaders
    /// light the wall, the ground and the men from the same fixed row (`Shading.Torch`), so no light list is needed.
    public const float TorchSpacing = 10;
    static readonly float[] TowerXs = { SiegeWall.GateX - 5.2f, SiegeWall.GateX + 5.2f, -60, -30, 30, 60, -95, 95 };

    static void Torches(Mesh m, Terrain t)
    {
        var rng = new Random(5);
        for (float x = -240; x <= 240; x += TorchSpacing)
        {
            bool gate = t.Wall!.AtGate(x), tower = TowerXs.Any(tx => MathF.Abs(tx - x) < 2.4f);
            float top = t.Height(new Vector2(gate ? x + TorchSpacing : x, 15.25f));
            float z = tower ? SiegeWall.Outer - 1.25f : gate ? SiegeWall.Outer + 0.25f : SiegeWall.Outer - 0.02f;
            var a = new Vector3(x, top - 1.3f, z);
            var head = new Vector3(x, top + 0.35f, z - 0.45f);
            m.Tag = Vector2.Zero;
            m.Frustum(a, head, 0.05f, 0.06f, 4, Timber, rng);
            m.Box(head - Vector3.Up * 0.12f, 0.2f, 0.2f, 0.16f, 0, new Color(50, 46, 44)); // the iron cup
            m.Tag = FlameTag;
            m.Frustum(head + Vector3.Up * 0.02f, head + Vector3.Up * 0.62f, 0.15f, 0, 5, new Color(255, 170, 60), rng);
            m.Frustum(head + Vector3.Up * 0.02f, head + Vector3.Up * 0.38f, 0.09f, 0, 4, new Color(255, 236, 150), rng);
        }
        m.Tag = Vector2.Zero;
    }

    static void GateDoor(Mesh m, Vector3 b, bool broken, Random r)
    {
        m.Tag = Vector2.Zero;
        if (!broken)
        {
            // Planks of slightly different wood, iron bands and studs across them.
            float w = SiegeWall.GateHalf * 2 + 0.6f;
            m.Box(b, w, 0.5f, SiegeWall.Height - 0.4f, 0, new Color(Timber.ToVector3() * 0.7f));
            int planks = 9;
            for (int i = 0; i < planks; i++)
                m.Box(b + new Vector3(-w / 2 + (i + 0.5f) * w / planks, 0, -0.27f), w / planks - 0.06f, 0.06f, SiegeWall.Height - 0.45f - r.NextSingle() * 0.15f, 0, Jitter(Timber, r, 0.1f));
            for (int i = 0; i < 3; i++)
            {
                var band = b + new Vector3(0, 0.8f + i * 1.3f, -0.33f);
                m.Box(band, SiegeWall.GateHalf * 2 + 0.4f, 0.08f, 0.2f, 0, new Color(70, 72, 80));
                for (float x = -w / 2 + 0.4f; x < w / 2; x += 0.65f) m.Box(band + new Vector3(x, 0.05f, -0.05f), 0.08f, 0.06f, 0.1f, 0, new Color(96, 98, 104));
            }
        }
        else
            for (int i = 0; i < 5; i++) // splintered planks on the ground
                m.Box(b + new Vector3(r.NextSingle() * 5 - 2.5f, 0, 1 + r.NextSingle() * 3), 1.6f, 0.3f, 0.12f, r.NextSingle() * MathF.Tau, Timber);
        m.Tag = MasonryTag;
        m.Box(b + new Vector3(0, SiegeWall.Height - 0.4f, 0.9f), SiegeWall.GateHalf * 2 + 2, 2.4f, 1.2f, 0, WallStone); // the arch over the way
        m.Tag = Vector2.Zero;
    }

    /// Two rails leaning from the ground to the wall top, with rungs.
    static void Ladder(Mesh m, Vector3 b)
    {
        m.Tag = Vector2.Zero;
        var foot = new Vector3(b.X, b.Y, SiegeWall.Outer - 2.2f);
        var top = new Vector3(b.X, b.Y + SiegeWall.Height + 0.6f, SiegeWall.Outer + 0.05f);
        var rng = new Random(1);
        foreach (float side in new[] { -0.4f, 0.4f })
            m.Frustum(foot + Vector3.UnitX * side, top + Vector3.UnitX * side, 0.06f, 0.06f, 4, Timber, rng);
        for (float k = 0.08f; k < 1; k += 0.09f)
            m.Box(Vector3.Lerp(foot, top, k), 0.8f, 0.07f, 0.07f, 0, Timber);
    }

    /// An upright spoked wooden wheel with an iron tire, centred at `c` (axle along `yaw`'s cross axis).
    internal static void Wheel(Mesh m, Vector3 c, float r, float w, float yaw, Color wood, Color iron)
    {
        var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        var az = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw)) * (w / 2);
        const int Segs = 10;
        float ri = r * 0.78f;
        void Quad(Vector3 a, Vector3 b, Vector3 c2, Vector3 d, Color col, Vector3 refPt) { m.Tri(a, b, c2, col, refPt); m.Tri(a, c2, d, col, refPt); }
        for (int i = 0; i < Segs; i++)
        {
            float a0 = i * MathF.Tau / Segs, a1 = (i + 1) * MathF.Tau / Segs;
            Vector3 d0 = ax * MathF.Cos(a0) + Vector3.Up * MathF.Sin(a0);
            Vector3 d1 = ax * MathF.Cos(a1) + Vector3.Up * MathF.Sin(a1);
            Vector3 o0 = c + d0 * r, o1 = c + d1 * r, i0 = c + d0 * ri, i1 = c + d1 * ri;
            var midOut = c + (d0 + d1) * (r * 0.6f);
            Quad(o0 - az, o1 - az, o1 + az, o0 + az, iron, c);
            Quad(i0 - az, i1 - az, i1 + az, i0 + az, wood, midOut);
            Quad(i0 - az, o0 - az, o1 - az, i1 - az, wood, c + az);
            Quad(i0 + az, i1 + az, o1 + az, o0 + az, wood, c - az);
        }
        m.Box(c - Vector3.Up * (r * 0.14f), r * 0.28f, w * 1.4f, r * 0.28f, yaw, iron);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathHelper.PiOver4;
            var dir = ax * MathF.Cos(a) + Vector3.Up * MathF.Sin(a);
            var perp = (-ax * MathF.Sin(a) + Vector3.Up * MathF.Cos(a)) * (r * 0.065f);
            var sz = az * 0.52f;
            Vector3 p0 = c - dir * ri, p1 = c + dir * ri;
            Quad(p0 - perp - sz, p1 - perp - sz, p1 + perp - sz, p0 + perp - sz, wood, c + az);
            Quad(p0 - perp + sz, p0 + perp + sz, p1 + perp + sz, p1 - perp + sz, wood, c - az);
            Quad(p0 + perp - sz, p1 + perp - sz, p1 + perp + sz, p0 + perp + sz, wood, c - perp);
            Quad(p0 - perp - sz, p0 - perp + sz, p1 - perp + sz, p1 - perp - sz, wood, c + perp);
        }
    }

    /// The supply wagon: upright spoked wheels, slatted timber bed, hitch shafts, arched hoop tilt over the front, and arrow sheaves in the back.
    static void Wagon(Mesh m, Vector3 b, Random r)
    {
        m.Tag = Vector2.Zero;
        float yaw = 0.14f;
        var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        var az = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw));
        Vector3 At(float x, float y, float z) => b + ax * x + Vector3.Up * y + az * z;
        var darkWood = new Color(Timber.ToVector3() * 0.72f);
        var plankWood = Jitter(Timber, r, 0.06f);
        var canvas = new Color(204, 194, 166);
        var canvasShade = new Color(184, 174, 148);
        const float wr = 0.48f;
        foreach (float x in new[] { -1.05f, 1.05f })
        {
            m.Box(At(x, wr - 0.06f, 0), 0.12f, 1.78f, 0.12f, yaw, darkWood);
            foreach (float z in new[] { -0.84f, 0.84f })
                Wheel(m, At(x, wr, z), wr, 0.11f, yaw, plankWood, Iron);
        }
        // Bed planks, corner/side stakes and slatted side rails.
        m.Box(At(0, 0.56f, 0), 3.1f, 1.48f, 0.16f, yaw, darkWood);
        foreach (float z in new[] { -0.7f, 0.7f })
        {
            foreach (float y in new[] { 0.82f, 1.06f }) m.Box(At(0, y, z), 3.05f, 0.07f, 0.12f, yaw, plankWood);
            for (int i = 0; i < 4; i++) m.Box(At(-1.42f + i * 0.95f, 0.7f, z), 0.09f, 0.09f, 0.52f, yaw, darkWood);
        }
        foreach (float y in new[] { 0.82f, 1.04f }) m.Box(At(1.48f, y, 0), 0.08f, 1.44f, 0.12f, yaw, plankWood);
        // Front shafts resting forward.
        foreach (float z in new[] { -0.42f, 0.42f })
            m.Frustum(At(1.5f, 0.62f, z), At(2.55f, 0.34f, z), 0.045f, 0.035f, 5, darkWood, r);
        m.Box(At(2.35f, 0.36f, 0), 0.07f, 0.92f, 0.07f, yaw, darkWood);
        // Open-ended arched canvas tilt over the front two-thirds (three curved hoop facets + dark hoop ribs).
        float x0 = -0.35f, x1 = 1.42f;
        float[] hoopZ = { -0.74f, -0.58f, -0.22f, 0.22f, 0.58f, 0.74f };
        float[] hoopY = { 1.12f, 1.66f, 2.02f, 2.02f, 1.66f, 1.12f };
        for (int i = 0; i < hoopZ.Length - 1; i++)
        {
            Vector3 p00 = At(x0, hoopY[i], hoopZ[i]), p01 = At(x1, hoopY[i], hoopZ[i]);
            Vector3 p10 = At(x0, hoopY[i + 1], hoopZ[i + 1]), p11 = At(x1, hoopY[i + 1], hoopZ[i + 1]);
            var mid = At((x0 + x1) / 2, 1.1f, 0);
            var col = i is 0 or 4 ? canvasShade : canvas;
            m.Tri(p00, p01, p11, col, mid); m.Tri(p00, p11, p10, col, mid);
        }
        foreach (float hx in new[] { x0, (x0 + x1) / 2, x1 })
            for (int i = 0; i < hoopZ.Length - 1; i++)
                m.Frustum(At(hx, hoopY[i], hoopZ[i]), At(hx, hoopY[i + 1], hoopZ[i + 1]), 0.04f, 0.04f, 5, darkWood, r);
        // Rear cargo: supply barrels with iron hoops + arrow bundles in a wooden chest.
        foreach (var (bx, bz) in new[] { (-1.15f, -0.36f), (-1.15f, 0.34f) })
        {
            m.Frustum(At(bx, 0.72f, bz), At(bx, 1.38f, bz), 0.26f, 0.24f, 8, plankWood, r, capBottom: true);
            m.Frustum(At(bx, 0.86f, bz), At(bx, 0.92f, bz), 0.27f, 0.27f, 8, Iron, r);
            m.Frustum(At(bx, 1.22f, bz), At(bx, 1.28f, bz), 0.26f, 0.26f, 8, Iron, r);
        }
        m.Box(At(-0.66f, 0.72f, 0), 0.56f, 1.0f, 0.38f, yaw, darkWood);
        for (int i = 0; i < 6; i++)
        {
            var foot = At(-0.66f + (i % 2 - 0.5f) * 0.22f, 0.78f, (i / 2 - 1) * 0.32f);
            var tip = foot + ax * -0.18f + Vector3.Up * 0.72f + az * ((i % 2 - 0.5f) * 0.14f);
            m.Frustum(foot, tip, 0.03f, 0.025f, 4, new Color(172, 146, 98), r);
            m.Frustum(tip - Vector3.Up * 0.08f, tip + Vector3.Up * 0.04f, 0.045f, 0.02f, 4, Style.Sides[0].Trim, r);
        }
    }

    static void TownHouse(Mesh m, Vector3 b, float s, Random r)
    {
        m.Tag = Vector2.Zero;
        float yaw = r.Next(4) * MathHelper.PiOver2;
        House(m, b, 4.5f * s, 3.4f * s, 2.8f * s, yaw, Jitter(new Color(204, 192, 166), r, 0.08f), Jitter(new Color(140, 74, 52), r, 0.1f));
    }

    static void Castle(Mesh m, Vector3 b, float s, Random r)
    {
        m.Tag = Vector2.Zero;
        float h = 1.3f * s, half = 1.8f * s;
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathHelper.PiOver2;
            var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            m.Box(b + dir * half - Vector3.Up * 0.3f * s, 2 * half, 0.4f * s, h + 0.3f * s, a + MathHelper.PiOver2, Stone);
            var corner = b + new Vector3(MathF.Cos(a + MathHelper.PiOver4), 0, MathF.Sin(a + MathHelper.PiOver4)) * half * 1.414f;
            m.Frustum(corner - Vector3.Up * 0.3f * s, corner + Vector3.Up * h * 1.6f, 0.5f * s, 0.46f * s, 8, Stone, r);
            m.Frustum(corner + Vector3.Up * h * 1.6f, corner + Vector3.Up * h * 2.3f, 0.6f * s, 0, 8, Slate, r, capBottom: true);
        }
        m.Box(b - Vector3.Up * 0.3f * s, 1.5f * s, 1.5f * s, h * 2.2f, 0, Jitter(Stone, r, 0.05f)); // keep
        m.Gable(b + Vector3.Up * (h * 2.2f - 0.3f * s), 1.6f * s, 1.6f * s, 0.8f * s, 0, Slate);
    }

    void Grass(Mesh m)
    {
        if (!t.Grass) return;
        var r = new Random(t.Seed + 13);
        const int count = 9000; // clumps of 2-4 tufts
        var flowers = new[] { new Color(236, 214, 80), new Color(240, 240, 232), new Color(176, 120, 200) };
        var buckets = new Dictionary<(int, int), Mesh>();
        for (int i = 0; i < count; i++)
        {
            float a = r.NextSingle() * MathF.Tau, d = MathF.Sqrt(r.NextSingle()) * 150;
            var p = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (t.RoadDist(p) < 2.6f) continue;
            var key = ((int)MathF.Floor(p.X / 48), (int)MathF.Floor(p.Y / 48));
            if (!buckets.TryGetValue(key, out var gm)) buckets[key] = gm = new Mesh();
            var ground = GroundColor(p);
            var baseCol = new Color(ground * 0.78f);
            var tip = new Color(Vector3.Lerp(ground, new Vector3(0.62f, 0.72f, 0.36f), 0.5f));
            int tufts = 2 + r.Next(3);
            for (int c = 0; c < tufts; c++)
            {
                var q = c == 0 ? p : p + new Vector2(r.NextSingle() - 0.5f, r.NextSingle() - 0.5f) * 1.1f;
                var b = new Vector3(q.X, t.Height(q) - 0.02f, q.Y);
                for (int k = 0; k < 3; k++)
                {
                    float ang = r.NextSingle() * MathF.Tau, w = 0.04f + r.NextSingle() * 0.04f, h = 0.22f + r.NextSingle() * 0.3f;
                    var side = new Vector3(MathF.Cos(ang), 0, MathF.Sin(ang)) * w;
                    var lean = new Vector3(r.NextSingle() - 0.5f, 0, r.NextSingle() - 0.5f) * 0.25f;
                    var o = new Vector3(r.NextSingle() - 0.5f, 0, r.NextSingle() - 0.5f) * 0.25f;
                    gm.TriUp(b + o - side, b + o + side, b + o + Vector3.Up * h + lean, baseCol, baseCol, tip);
                }
            }
            var b0 = new Vector3(p.X, t.Height(p) - 0.02f, p.Y);
            if (r.Next(14) == 0)
            {
                var top = b0 + Vector3.Up * (0.3f + r.NextSingle() * 0.15f);
                var col = flowers[r.Next(flowers.Length)];
                float f = 0.06f;
                gm.TriUp(top + new Vector3(-f, 0, 0), top + new Vector3(f, 0, 0), top + new Vector3(0, f, 0), col, col, col, 0.9f);
                gm.TriUp(top + new Vector3(0, 0, -f), top + new Vector3(0, 0, f), top + new Vector3(0, f, 0), col, col, col, 0.9f);
            }
        }
        foreach (var gm in buckets.Values)
        {
            if (gm.V.Count == 0) continue;
            int start = m.V.Count;
            m.V.AddRange(gm.V);
            var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
            for (int i = start; i < m.V.Count; i++) { min = Vector3.Min(min, m.V[i].Position); max = Vector3.Max(max, m.V[i].Position); }
            grassTiles.Add((start, (m.V.Count - start) / 3, new BoundingBox(min - new Vector3(1.5f), max + new Vector3(1.5f))));
        }
    }

    /// Beyond the campaign map's edge: the country goes on, rolling then rising into a far range of rock and snow that fades
    /// into the haze, so the world does not end in the sky's colour. The inner ring meets the terrain's own edge.
    void Skirt(Mesh m)
    {
        // The land runs on past the map's edge: low rolling country first, rising slowly into a far range that fades into
        // the haze, smooth-shaded so it reads as land going on rather than a wall (the grounded look, WL4b).
        const int Rings = 12, Around = 64;
        float half = t.Half - 0.5f;
        int n = Around * 4;
        var pts = new Vector3[Rings + 1, n];
        var dist = new float[Rings + 1];
        for (int i = 0; i < n; i++)
        {
            int side = i / Around;
            float u = (i % Around) / (float)Around * 2 - 1;
            var edge = side switch { 0 => new Vector2(u, -1), 1 => new Vector2(1, u), 2 => new Vector2(-u, 1), _ => new Vector2(-1, -u) } * half;
            var outward = Vector2.Normalize(edge);
            float h0 = t.Height(edge);
            pts[0, i] = new Vector3(edge.X, h0, edge.Y);
            for (int k = 1; k <= Rings; k++)
            {
                float d = k * k * 9 + k * 25;
                dist[k] = d;
                var p = edge + outward * d;
                float s = MathHelper.SmoothStep(0, 1, Math.Clamp((d - 60) / 900, 0, 1));
                float roll = (Terrain.Noise(p.X * 0.03f, p.Y * 0.03f, t.Seed + 24) - 0.5f) * 6 * MathF.Min(1, d / 120);
                float peak = Terrain.Noise(p.X * 0.008f, p.Y * 0.008f, t.Seed + 22) * 110 + Terrain.Noise(p.X * 0.03f, p.Y * 0.03f, t.Seed + 23) * 28;
                pts[k, i] = new Vector3(p.X, MathHelper.Lerp(h0, h0 * 0.75f, MathF.Min(1, d / 200)) + roll + 0.03f * d + peak * s, p.Y);
            }
        }
        Vector3 Normal(int k, int i)
        {
            int kk = Math.Min(k + 1, Rings), kp = Math.Max(k - 1, 0);
            var across = pts[k, (i + 1) % n] - pts[k, (i + n - 1) % n];
            var outv = pts[kk, i] - pts[kp, i];
            var nn = Vector3.Normalize(Vector3.Cross(outv, across));
            return nn.Y < 0 ? -nn : nn;
        }
        var haze = new Color(150, 162, 180);
        Color Col(int k, int i, Vector3 nn)
        {
            var q = pts[k, i];
            float rise = q.Y - pts[0, i].Y;
            var near = new Color(GroundColor(new Vector2(pts[0, i].X, pts[0, i].Z)));
            var hill = Color.Lerp(near, new Color(92, 104, 66), Math.Clamp(dist[k] / 300, 0, 1));
            var c = Color.Lerp(hill, new Color(112, 108, 100), Math.Clamp((1 - nn.Y) * 3 - 0.4f + rise / 160, 0, 1)); // rock on steep, high ground
            if (q.Y > 95 + Terrain.Noise(q.X * 0.02f, q.Z * 0.02f, t.Seed + 25) * 25) c = new Color(228, 232, 238);
            return Color.Lerp(c, haze, MathF.Min(0.82f, dist[k] / 1500));
        }
        m.Tag = Vector2.Zero;
        for (int k = 0; k < Rings; k++)
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                void V(int kk, int ii) { var nn = Normal(kk, ii); m.Raw(pts[kk, ii], nn, Col(kk, ii, nn), Vector2.Zero); }
                V(k, i); V(k, j); V(k + 1, j);
                V(k, i); V(k + 1, j); V(k + 1, i);
            }
    }

    /// Flat-shaded triangle soup with a few low-poly primitives.
    internal sealed class Mesh
    {
        public readonly List<VertexPNCT> V = new();
        public Vector2 Tag; // goes in the UV of foliage: (wind sway, leafiness) for the world shader

        /// One vertex with its own normal and UV (ribbons: roads, and water whose UV tells the shader where it flows).
        public void Raw(Vector3 p, Vector3 n, Color c, Vector2 uv) => V.Add(new(p, n, c, uv));
        static readonly (Vector3[] P, int[] F) Sphere = Icosphere();

        /// A triangle with its own face normal, flipped to point away from `center` (so closed shapes light from outside).
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 center)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.LengthSquared() < 1e-12f) n = Vector3.Up; else n.Normalize();
            if (Vector3.Dot(n, (a + b + c) / 3 - center) < 0) n = -n;
            V.Add(new(a, n, col, Tag)); V.Add(new(b, n, col, Tag)); V.Add(new(c, n, col, Tag));
        }

        /// A triangle lit as if facing up (grass blades look the same from both sides).
        /// Rooted at a and b (no sway), free at c (sways `tipSway`): grass blades and flowers.
        public void TriUp(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, float tipSway = 1)
        {
            V.Add(new(a, Vector3.Up, ca, new Vector2(0, 1))); V.Add(new(b, Vector3.Up, cb, new Vector2(0, 1))); V.Add(new(c, Vector3.Up, cc, new Vector2(tipSway, 1)));
        }

        /// A box standing on `c` (bottom centre): w along its yaw, d across, h tall. No bottom face.
        public void Box(Vector3 c, float w, float d, float h, float yaw, Color col)
        {
            var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw)) * w / 2;
            var az = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw)) * d / 2;
            var up = Vector3.Up * h;
            Vector3 p0 = c - ax - az, p1 = c + ax - az, p2 = c + ax + az, p3 = c - ax + az, mid = c + up / 2;
            void Quad(Vector3 a, Vector3 b2, Vector3 c2, Vector3 d2) { Tri(a, b2, c2, col, mid); Tri(a, c2, d2, col, mid); }
            Quad(p0, p1, p1 + up, p0 + up); Quad(p1, p2, p2 + up, p1 + up); Quad(p2, p3, p3 + up, p2 + up); Quad(p3, p0, p0 + up, p3 + up);
            Quad(p0 + up, p1 + up, p2 + up, p3 + up);
        }

        /// A gable roof on top of a w x d footprint at `c`, ridge `h` above it, running along the yaw.
        public void Gable(Vector3 c, float w, float d, float h, float yaw, Color col)
        {
            var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw)) * w / 2;
            var az = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw)) * d / 2;
            var up = Vector3.Up * h;
            Vector3 p0 = c - ax - az, p1 = c + ax - az, p2 = c + ax + az, p3 = c - ax + az, r0 = c - ax + up, r1 = c + ax + up, mid = c + up * 0.3f;
            Tri(p0, p1, r1, col, mid); Tri(p0, r1, r0, col, mid);
            Tri(p3, p2, r1, col, mid); Tri(p3, r1, r0, col, mid);
            var gable = new Color(col.ToVector3() * 0.8f);
            Tri(p0, p3, r0, gable, mid); Tri(p1, p2, r1, gable, mid);
        }

        /// A tapered prism from a to b (r1 = 0 makes a cone).
        public void Frustum(Vector3 a, Vector3 b, float r0, float r1, int sides, Color col, Random rng, bool capBottom = false)
        {
            float rot = rng.NextSingle() * MathF.Tau;
            var center = (a + b) / 2;
            for (int i = 0; i < sides; i++)
            {
                float a0 = rot + i * MathF.Tau / sides, a1 = rot + (i + 1) * MathF.Tau / sides;
                Vector3 d0 = new(MathF.Cos(a0), 0, MathF.Sin(a0)), d1 = new(MathF.Cos(a1), 0, MathF.Sin(a1));
                var c = Jitter(col, rng, 0.06f);
                Tri(a + d0 * r0, a + d1 * r0, b + d0 * r1, c, center);
                if (r1 > 0) Tri(a + d1 * r0, b + d1 * r1, b + d0 * r1, c, center);
                if (capBottom) Tri(a, a + d1 * r0, a + d0 * r0, new Color(col.ToVector3() * 0.7f), b);
            }
        }

        /// A lumpy squashed sphere (subdivided icosahedron with jittered radius), each face shaded a little differently.
        public void Blob(Vector3 c, Vector3 size, Color col, Random rng, float lump)
        {
            var (pts, faces) = Sphere;
            var p = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++) p[i] = c + pts[i] * size * (1 + (rng.NextSingle() * 2 - 1) * lump);
            if (Tag.Y >= 0.5f) // leaves: normals from the crown's centre, so a canopy shades round and soft, not faceted
            {
                var inv = new Vector3(1 / (size.X * size.X), 1 / (size.Y * size.Y), 1 / (size.Z * size.Z));
                for (int f = 0; f < faces.Length; f += 3)
                {
                    var fc = Jitter(col, rng, 0.04f);
                    for (int k = 0; k < 3; k++) { var q = p[faces[f + k]]; V.Add(new(q, Vector3.Normalize((q - c) * inv), fc, Tag)); }
                }
                return;
            }
            for (int f = 0; f < faces.Length; f += 3)
                Tri(p[faces[f]], p[faces[f + 1]], p[faces[f + 2]], Jitter(col, rng, 0.07f), c);
        }

        static (Vector3[], int[]) Icosphere()
        {
            float g = (1 + MathF.Sqrt(5)) / 2;
            var pts = new List<Vector3>
            {
                new(-1, g, 0), new(1, g, 0), new(-1, -g, 0), new(1, -g, 0), new(0, -1, g), new(0, 1, g),
                new(0, -1, -g), new(0, 1, -g), new(g, 0, -1), new(g, 0, 1), new(-g, 0, -1), new(-g, 0, 1),
            };
            for (int i = 0; i < pts.Count; i++) pts[i] = Vector3.Normalize(pts[i]);
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new Dictionary<(int, int), int>();
            int Mid(int a, int b)
            {
                var key = a < b ? (a, b) : (b, a);
                if (mid.TryGetValue(key, out int i)) return i;
                pts.Add(Vector3.Normalize(pts[a] + pts[b]));
                return mid[key] = pts.Count - 1;
            }
            var sub = new List<int>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                int a = faces[f], b = faces[f + 1], c = faces[f + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                sub.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            return (pts.ToArray(), sub.ToArray());
        }
    }
}
