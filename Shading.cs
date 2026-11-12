using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Graphics quality: High = shaders, sun shadows and post; Medium = shaders and post, no shadows;
/// Low = the original fixed-function effects (also the fallback if the GLSL shaders fail on a driver).
public enum Quality { High, Medium, Low }

/// The shader pipeline: GLSL effects from Shaders/ (see GlslEffect), the sun's shadow map, the off-screen scene target
/// and the post pass. Light and fog values come from the design tokens so the shaded and plain paths match.
sealed class Shading : IDisposable
{
    public const int ShadowSize = 2048;
    public static Vector3 SunColor => Ironfield.Sky.SunColor;
    public static Vector3 SkyColor => Ironfield.Sky.SkyColor;
    public static Vector3 GroundColor => Ironfield.Sky.GroundColor;
    public static readonly Vector2 SpriteLight = new(1.05f, 0.62f); // sprite brightness: sky part, sun part (lit ~1.0, shade ~0.5)
    public static readonly Vector4 Wind = new(0.8f, 0.6f, 0.12f, 0.25f); // direction xz, strength, steady lean

    public readonly Effect World, Sprite, Sky, Post;
    public readonly RenderTarget2D ShadowMap;
    public Matrix LightView, LightViewProj;
    public Vector3 LightEye;
    RenderTarget2D? scene;
    readonly GraphicsDevice gd;
    readonly VertexPositionTexture[] quad =
    {
        new(new Vector3(-1, 1, 0), new Vector2(0, 0)), new(new Vector3(1, 1, 0), new Vector2(1, 0)), new(new Vector3(-1, -1, 0), new Vector2(0, 1)),
        new(new Vector3(1, 1, 0), new Vector2(1, 0)), new(new Vector3(1, -1, 0), new Vector2(1, 1)), new(new Vector3(-1, -1, 0), new Vector2(0, 1)),
    };

    static readonly SamplerState ShadowSampler = new() { Filter = TextureFilter.Point, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp };
    static readonly SamplerState GroundSampler = new() { Filter = TextureFilter.Linear, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp };
    static readonly SamplerState DetailSampler = new() { Filter = TextureFilter.LinearMipPoint, AddressU = TextureAddressMode.Wrap, AddressV = TextureAddressMode.Wrap };

    static string Source(string file)
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var p = Path.Combine(dir, "Shaders", file);
            if (File.Exists(p)) return File.ReadAllText(p);
        }
        throw new FileNotFoundException("shader not found", file);
    }

    public Shading(GraphicsDevice gd)
    {
        this.gd = gd;
        string common = Source("common.glsl");
        var lit = new[]
        {
            Uniform.Mat4("ViewProj"), Uniform.Mat4("LightViewProj"), Uniform.Vec3("CameraPos"), Uniform.Vec3("SunDir"), Uniform.Vec3("SunColor"),
            Uniform.Vec3("SkyColor"), Uniform.Vec3("GroundColor"), Uniform.Vec3("FogColor"), Uniform.Vec4("FogParams"), Uniform.Vec4("ShadowParams"),
            Uniform.Float("Time"), Uniform.Vec4("Wind"), Uniform.Vec2("SpriteLight"), Uniform.Vec4("Field"), Uniform.Vec4("Look"), Uniform.Vec4("Torch"), Uniform.Float("Cut"), Uniform.Vec4("CutEye"), Uniform.Vec4("Palette"),
            Uniform.Vec4("Glow0"), Uniform.Vec4("Glow1"), Uniform.Vec4("Glow2"), Uniform.Vec4("Glow3"),
            Uniform.Vec4("GlowCol0"), Uniform.Vec4("GlowCol1"), Uniform.Vec4("GlowCol2"), Uniform.Vec4("GlowCol3"),
        };
        var shadowTex = new TextureSlot("ShadowMap", 1, ShadowSampler);

        World = GlslEffect.Create(gd, "world", common + Source("world.glsl"), lit,
            new[] { new TextureSlot("Detail", 0, DetailSampler), shadowTex, new TextureSlot("GroundMap", 2, GroundSampler) },
            new[] { new Attribute("aPos", VertexElementUsage.Position), new Attribute("aNormal", VertexElementUsage.Normal),
                    new Attribute("aColor", VertexElementUsage.Color), new Attribute("aUV", VertexElementUsage.TextureCoordinate) },
            new GlslEffect.Technique("Terrain", "TERRAIN"), new GlslEffect.Technique("Foliage", "FOLIAGE"),
            new GlslEffect.Technique("TerrainShadow", "TERRAIN", "SHADOW"), new GlslEffect.Technique("FoliageShadow", "FOLIAGE", "SHADOW"));

        Sprite = GlslEffect.Create(gd, "sprite", common + Source("sprite.glsl"), lit,
            new[] { new TextureSlot("Atlas", 0, SamplerState.PointClamp), shadowTex },
            new[] { new Attribute("aPos", VertexElementUsage.Position), new Attribute("aColor", VertexElementUsage.Color),
                    new Attribute("aUV", VertexElementUsage.TextureCoordinate) },
            new GlslEffect.Technique("Sprite"), new GlslEffect.Technique("SpriteShadow", "SHADOW"), new GlslEffect.Technique("SpriteSoft", "SOFT"));

        Sky = GlslEffect.Create(gd, "sky", Source("sky.glsl"),
            new[] { Uniform.Mat4("ViewProj"), Uniform.Vec3("SunDir"), Uniform.Vec3("SunColor"), Uniform.Vec3("Horizon"), Uniform.Vec3("Zenith"), Uniform.Float("Time"), Uniform.Vec2("Swirl") },
            Array.Empty<TextureSlot>(), new[] { new Attribute("aPos", VertexElementUsage.Position) },
            new GlslEffect.Technique("Sky"));

        Post = GlslEffect.Create(gd, "post", Source("post.glsl"),
            new[] { Uniform.Vec2("Texel"), Uniform.Vec4("Grade"), Uniform.Vec4("Tint"), Uniform.Vec4("Awe"), Uniform.Vec4("Moment"), Uniform.Float("Time"), Uniform.Float("Warp") },
            new[] { new TextureSlot("Scene", 0, SamplerState.LinearClamp) },
            new[] { new Attribute("aPos", VertexElementUsage.Position), new Attribute("aUV", VertexElementUsage.TextureCoordinate) },
            new GlslEffect.Technique("Post"));

        ShadowMap = new RenderTarget2D(gd, ShadowSize, ShadowSize, false, SurfaceFormat.Single, DepthFormat.Depth24);
    }

    /// Aim the sun's orthographic shadow camera at `focus`, covering `size` metres; snapped to whole texels so
    /// shadows do not shimmer as the camera moves.
    public void AimSun(Vector3 focus, float size)
    {
        var dir = Ironfield.World.SunDir;
        var view = Matrix.CreateLookAt(Vector3.Zero, dir, MathF.Abs(dir.Y) > 0.99f ? Vector3.UnitZ : Vector3.Up);
        var f = Vector3.Transform(focus, view);
        float texel = size / ShadowSize;
        f.X = MathF.Floor(f.X / texel) * texel;
        f.Y = MathF.Floor(f.Y / texel) * texel;
        var snapped = Vector3.Transform(f, Matrix.Invert(view));
        LightEye = snapped - dir * 250;
        LightView = Matrix.CreateLookAt(LightEye, snapped, MathF.Abs(dir.Y) > 0.99f ? Vector3.UnitZ : Vector3.Up);
        LightViewProj = LightView * Matrix.CreateOrthographic(size, size, 1, 500);
        ShadowTexel = 1f / ShadowSize;
        // Depth is normalised over the 499 m depth range; the bias (in metres) grows with the texel size.
        ShadowBias = (0.12f + texel * 1.5f) / 499f;
        ShadowTexelMetres = texel;
    }

    public float ShadowTexel, ShadowBias, ShadowTexelMetres;
    /// The ground map (trampled grass, red channel) and its size in metres, or null; set by the battle screen.
    public Texture2D? GroundMap;
    public float GroundSize = 1;
    /// Close-up ground detail (the battle's field camera; off on the campaign map's far view).
    public bool NearDetail;
    /// A besieged town's torch row (docs/archive/VISUALS.md): x = its z, y = its height, z = strength (0 = none), w = spacing.
    /// Set by the battle screen while it draws a siege at night; zero everywhere else.
    public Vector4 Torch;
    /// How still the air is (0 = the usual breeze, 1 = dead calm): a great working gathering holds its breath.
    public float Calm;
    /// Moving lights (docs/archive/SPECTACLE.md S1): xyz place, w strength (negative takes light); colours rgb + reach in w.
    /// Set by the battle screen each frame; all zero elsewhere.
    public readonly Vector4[] Glow = new Vector4[4], GlowCol = new Vector4[4];
    static readonly string[] GlowNames = { "Glow0", "Glow1", "Glow2", "Glow3", "GlowCol0", "GlowCol1", "GlowCol2", "GlowCol3" };
    /// How far leaves over men are cut away (the command view's blend, 0 = none); reads the ground map's green channel.
    public float Cut;
    /// Command view: the camera (xyz) and its distance across the ground to what it looks at (w; 0 = off). Trees much nearer are thinned away.
    public Vector4 CutEye;
    /// Fog start and end (the campaign map pushes them out for its far camera).
    public Vector2 Fog = new(Ironfield.World.FogStart, Ironfield.World.FogEnd);

    /// Set the shared lighting parameters on a lit effect.
    public void Setup(Effect fx, Matrix viewProj, Vector3 camPos, float time, bool shadows, float normalOffset)
    {
        var p = fx.Parameters;
        p["ViewProj"]?.SetValue(viewProj);
        p["LightViewProj"]?.SetValue(LightViewProj);
        p["CameraPos"]?.SetValue(camPos);
        p["SunDir"]?.SetValue(Ironfield.World.SunDir);
        p["SunColor"]?.SetValue(SunColor);
        p["SkyColor"]?.SetValue(SkyColor);
        p["GroundColor"]?.SetValue(GroundColor);
        p["FogColor"]?.SetValue(Ironfield.World.Horizon.ToVector3());
        p["FogParams"]?.SetValue(new Vector4(Fog.X * Ironfield.Sky.FogScale, Fog.Y * Ironfield.Sky.FogScale, 0.55f, 0));
        p["ShadowParams"]?.SetValue(new Vector4(ShadowTexel, ShadowBias, shadows ? 1 : 0, normalOffset));
        p["Time"]?.SetValue(time);
        p["Wind"]?.SetValue(new Vector4(Wind.X, Wind.Y, Wind.Z * (1 - Calm * 0.9f), Wind.W * (1 - Calm * 0.6f)));
        p["SpriteLight"]?.SetValue(SpriteLight);
        p["ShadowMap"]?.SetValue(ShadowMap);
        p["Field"]?.SetValue(new Vector4(1f / GroundSize, GroundMap != null ? 1 : 0, Ironfield.Sky.Clouds, Ironfield.Sky.Wet));
        p["Torch"]?.SetValue(Torch);
        p["Cut"]?.SetValue(Cut);
        p["CutEye"]?.SetValue(CutEye);
        for (int i = 0; i < 4; i++) { p[GlowNames[i]]?.SetValue(Glow[i]); p[GlowNames[i + 4]]?.SetValue(GlowCol[i]); }
        p["Palette"]?.SetValue(Looks.Palette);
        p["Look"]?.SetValue(new Vector4(Ironfield.Sky.Snow, NearDetail ? 1 : 0, Ironfield.Sky.Night, Ironfield.Sky.Rain));
        if (p["GroundMap"] is { } gm) gm.SetValue(GroundMap ?? Blank(gd));
    }

    static Texture2D? blank;
    /// A 1x1 black texture: a ground map with nothing on it.
    static Texture2D Blank(GraphicsDevice gd)
    {
        if (blank == null) { blank = new Texture2D(gd, 1, 1); blank.SetData(new[] { Color.Black }); }
        return blank;
    }

    /// The off-screen colour target the scene is drawn into before the post pass (recreated on resize).
    public RenderTarget2D Scene()
    {
        var pp = gd.PresentationParameters;
        if (scene == null || scene.Width != pp.BackBufferWidth || scene.Height != pp.BackBufferHeight)
        {
            scene?.Dispose();
            scene = new RenderTarget2D(gd, pp.BackBufferWidth, pp.BackBufferHeight, false, SurfaceFormat.Color, DepthFormat.Depth24);
        }
        return scene;
    }

    /// Draw the scene target to the screen through the post shader: `tint` grades the light by hour and weather
    /// (Sky.Grade; One = none), `bloom` lets bright highlights glow (0 = off; High quality only). `awe` drains colour
    /// outside a circle (xy its centre in screen uv, z its radius, w how much); `moment`: x = letterbox bars (fraction of the
    /// height each), y = heat shimmer, z = a white flash, w = the shimmer's height on screen (docs/archive/SPECTACLE.md S1).
    public void PostToScreen(bool fxaa, Vector3? tint = null, float bloom = 0, Vector4 awe = default, Vector4 moment = default, float warp = 0)
    {
        var s = Scene();
        gd.SetRenderTarget(null);
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.None;
        gd.RasterizerState = RasterizerState.CullNone;
        Post.Parameters["Scene"].SetValue(s);
        Post.Parameters["Texel"].SetValue(new Vector2(1f / s.Width, 1f / s.Height));
        var g = Looks.Grade;
        Post.Parameters["Grade"].SetValue(new Vector4(g.X, g.Y, g.Z, fxaa ? 1 : 0)); // saturation, contrast, vignette, FXAA (Looks)
        Post.Parameters["Tint"].SetValue(new Vector4(tint ?? Vector3.One, bloom));
        Post.Parameters["Awe"].SetValue(awe);
        Post.Parameters["Moment"].SetValue(moment);
        Post.Parameters["Warp"]?.SetValue(warp);
        Post.Parameters["Time"].SetValue((float)(Environment.TickCount64 % 100000) / 1000f);
        Post.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, quad, 0, 2);
    }

    public void Dispose()
    {
        World.Dispose(); Sprite.Dispose(); Sky.Dispose(); Post.Dispose();
        ShadowMap.Dispose(); scene?.Dispose();
    }
}
