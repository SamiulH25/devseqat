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
        detail = BuildDetail();
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
        DrawFoliage(new BoundingFrustum(view * proj));
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
        DrawGeometry(fx, "Terrain", "Foliage", new BoundingFrustum(view * proj));
    }

    /// Terrain and foliage into the sun's shadow map (hills and trees cast shadows).
    public void DrawShadowCasters(Shading sh, float time)
    {
        var fx = sh.World;
        sh.Setup(fx, Matrix.Identity, sh.LightEye, time, false, 0);
        fx.Parameters["ShadowMap"].SetValue((Texture2D?)null);
        fx.Parameters["Detail"].SetValue(detail);
        DrawGeometry(fx, "TerrainShadow", "FoliageShadow", new BoundingFrustum(sh.LightViewProj));
    }

    void DrawGeometry(Effect fx, string terrain, string foliage, BoundingFrustum view)
    {
        fx.CurrentTechnique = fx.Techniques[terrain];
        fx.CurrentTechnique.Passes[0].Apply();
        gd.SetVertexBuffer(terrainVb);
        gd.Indices = terrainIb;
        for (int s = 0; s < terrainTris; s += Chunk)
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, s * 3, Math.Min(Chunk, terrainTris - s));
        fx.CurrentTechnique = fx.Techniques[foliage];
        fx.CurrentTechnique.Passes[0].Apply();
        DrawFoliage(view);
    }

    public void Dispose()
    {
        terrainVb.Dispose(); terrainIb.Dispose(); foliageVb.Dispose(); skyVb.Dispose();
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
        c = Vector3.Lerp(c, new Vector3(0.56f, 0.47f, 0.34f), 1 - Smooth(1.8f, 3.4f + n2 * 0.6f, rd));      // road
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
                v[z * n + x] = new VertexPNCT(new Vector3(p.X, t.GridHeight(x, z), p.Y), normal, new Color(GroundColor(p)), p / 4);
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
        alwaysStart = m.V.Count;
        Water(m);
        Features(m);
        if (t.Wall != null) Torches(m, t);
        if (!t.Grass) Skirt(m);
        Grass(m);
        if (m.V.Count == 0) m.Tri(Vector3.Zero, Vector3.Zero, Vector3.Zero, Color.Black, Vector3.Down); // keep the buffer valid on a bare field
        alwaysTris = (m.V.Count - alwaysStart) / 3;
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
    int alwaysStart, alwaysTris;
    /// Foliage triangles drawn last frame (for the performance check).
    public int FoliageDrawn { get; private set; }

    /// Draw the foliage tiles inside `view` (a camera's or the sun's frustum), then the water and grass.
    void DrawFoliage(BoundingFrustum view)
    {
        gd.SetVertexBuffer(foliageVb);
        int drawn = 0;
        void Range(int start, int tris)
        {
            for (int s = 0; s < tris; s += Chunk) gd.DrawPrimitives(PrimitiveType.TriangleList, start + s * 3, Math.Min(Chunk, tris - s));
            drawn += tris;
        }
        foreach (var (start, tris, box) in tiles) if (view.Intersects(box)) Range(start, tris);
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

    /// A cottage: walls with a gable roof, at `c` on the ground, turned `yaw`.
    static void House(Mesh m, Vector3 c, float w, float d, float h, float yaw, Color wall, Color roof)
    {
        m.Tag = Vector2.Zero;
        m.Box(c, w, d, h, yaw, wall);
        m.Gable(c + Vector3.Up * h, w * 1.1f, d * 1.15f, h * 0.7f, yaw, roof);
    }

    static void Village(Mesh m, Vector3 b, float s, Random r)
    {
        int n = 4 + r.Next(3);
        for (int i = 0; i < n; i++)
        {
            float a = r.NextSingle() * MathF.Tau, d = (0.8f + r.NextSingle() * 1.6f) * s;
            House(m, b + new Vector3(MathF.Cos(a) * d, -0.1f, MathF.Sin(a) * d), 0.9f * s, 0.6f * s, 0.5f * s, r.NextSingle() * MathF.Tau,
                Jitter(Wattle, r, 0.08f), Jitter(Thatch, r, 0.1f));
        }
    }

    static void Town(Mesh m, Vector3 b, float s, Random r)
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
            House(m, b + new Vector3(MathF.Cos(a) * d, -0.1f, MathF.Sin(a) * d), 0.8f * s, 0.6f * s, (0.6f + r.NextSingle() * 0.4f) * s, r.NextSingle() * MathF.Tau,
                Jitter(Wattle, r, 0.06f), Jitter(Tile, r, 0.12f));
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

    // ---------- water ----------

    static readonly Color WaterDeep = new(52, 88, 112), WaterShallow = new(86, 124, 132);

    /// Each river as a flat ribbon a little below its lower bank; shallower and paler over the fords.
    void Water(Mesh m)
    {
        m.Tag = Vector2.Zero;
        foreach (var r in t.Rivers)
            for (int i = 0; i + 1 < r.Path.Length; i++)
            {
                Vector2 a = r.Path[i], b = r.Path[i + 1], d = Vector2.Normalize(b - a), n = new(-d.Y, d.X);
                float half = r.Width / 2 + 1.2f, bank = r.Width / 2 + 3.5f;
                float Level(Vector2 p) => MathF.Min(t.Height(p + n * bank), t.Height(p - n * bank)) - 0.3f;
                float la = Level(a), lb = Level(b);
                Color ca = r.AtFord(a) ? WaterShallow : WaterDeep, cb = r.AtFord(b) ? WaterShallow : WaterDeep;
                Vector3 V(Vector2 p, float y) => new(p.X, y, p.Y);
                var up = new Vector3((a.X + b.X) / 2, -1000, (a.Y + b.Y) / 2);
                m.Tri(V(a - n * half, la), V(a + n * half, la), V(b + n * half, lb), ca, up);
                m.Tri(V(a - n * half, la), V(b + n * half, lb), V(b - n * half, lb), cb, up);
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

    /// The supply wagon: a bed on four wheels under a canvas tilt, with arrow sheaves.
    static void Wagon(Mesh m, Vector3 b, Random r)
    {
        m.Tag = Vector2.Zero;
        m.Box(b + Vector3.Up * 0.55f, 3.2f, 1.5f, 0.45f, 0, Timber);
        foreach (var (x, z) in new[] { (-1.1f, -0.8f), (1.1f, -0.8f), (-1.1f, 0.8f), (1.1f, 0.8f) })
            m.Frustum(b + new Vector3(x, 0.45f, z - 0.06f), b + new Vector3(x, 0.45f, z + 0.06f), 0.45f, 0.45f, 8, new Color(80, 58, 38), r, capBottom: true);
        m.Gable(b + Vector3.Up * 1.0f, 3.0f, 1.6f, 0.8f, 0, new Color(214, 204, 176));
        for (int i = 0; i < 3; i++) m.Box(b + new Vector3(-0.9f + i * 0.6f, 1.0f, 0), 0.25f, 0.25f, 0.6f, 0, new Color(150, 120, 80));
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
        var r = new Random(t.Seed + 13);
        int count = !t.Grass ? 0 : 9000; // clumps of 2-4 tufts
        var flowers = new[] { new Color(236, 214, 80), new Color(240, 240, 232), new Color(176, 120, 200) };
        for (int i = 0; i < count; i++)
        {
            float a = r.NextSingle() * MathF.Tau, d = MathF.Sqrt(r.NextSingle()) * 150;
            var p = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (t.RoadDist(p) < 2.6f) continue;
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
                    m.TriUp(b + o - side, b + o + side, b + o + Vector3.Up * h + lean, baseCol, baseCol, tip);
                }
            }
            var b0 = new Vector3(p.X, t.Height(p) - 0.02f, p.Y);
            if (r.Next(14) == 0)
            {
                var top = b0 + Vector3.Up * (0.3f + r.NextSingle() * 0.15f);
                var col = flowers[r.Next(flowers.Length)];
                float f = 0.06f;
                m.TriUp(top + new Vector3(-f, 0, 0), top + new Vector3(f, 0, 0), top + new Vector3(0, f, 0), col, col, col, 0.9f);
                m.TriUp(top + new Vector3(0, 0, -f), top + new Vector3(0, 0, f), top + new Vector3(0, f, 0), col, col, col, 0.9f);
            }
        }
    }

    /// Beyond the campaign map's edge: rings of rock and snow peaks running out to the haze, so the world does not end in
    /// the sky's colour when you look over the mountain rim. The inner ring meets the terrain's own edge.
    void Skirt(Mesh m)
    {
        const int Rings = 7, Around = 48;
        float half = t.Half - 0.5f;
        var r = new Random(t.Seed + 21);
        var pts = new Vector3[Rings + 1, Around * 4];
        int n = Around * 4;
        for (int i = 0; i < n; i++)
        {
            // Walk the square edge: four sides of `Around` steps each.
            int side = i / Around;
            float u = (i % Around) / (float)Around * 2 - 1;
            var edge = side switch { 0 => new Vector2(u, -1), 1 => new Vector2(1, u), 2 => new Vector2(-u, 1), _ => new Vector2(-1, -u) } * half;
            var outward = Vector2.Normalize(edge);
            float h0 = t.Height(edge);
            pts[0, i] = new Vector3(edge.X, h0, edge.Y);
            for (int k = 1; k <= Rings; k++)
            {
                var p = edge + outward * (k * k * 22 + k * 30);
                float peak = Terrain.Noise(p.X * 0.012f, p.Y * 0.012f, t.Seed + 22) * 55 + Terrain.Noise(p.X * 0.04f, p.Y * 0.04f, t.Seed + 23) * 18;
                pts[k, i] = new Vector3(p.X, h0 + 12 * k + peak * MathF.Min(1, k / 2f), p.Y);
            }
        }
        var rock = new Color(118, 114, 108);
        for (int k = 0; k < Rings; k++)
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = pts[k, i], b = pts[k, j], c = pts[k + 1, j], d = pts[k + 1, i];
                var below = (a + b + c + d) / 4 - Vector3.Up * 50;
                Color Col(Vector3 q) => q.Y > 62 ? Jitter(new Color(226, 230, 236), r, 0.04f) : Jitter(rock, r, 0.08f);
                m.Tri(a, b, c, Col((a + b + c) / 3), below);
                m.Tri(a, c, d, Col((a + c + d) / 3), below);
            }
    }

    /// Flat-shaded triangle soup with a few low-poly primitives.
    sealed class Mesh
    {
        public readonly List<VertexPNCT> V = new();
        public Vector2 Tag; // goes in the UV of foliage: (wind sway, leafiness) for the world shader
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
