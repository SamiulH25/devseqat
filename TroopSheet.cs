using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// `IRONFIELD_TROOPS=/dir`: draws every man of the troop roster standing, three-quarter front, in the first palette, one
/// transparent PNG per troop (`<id>.png`), rendered through Puppet3D + Unit3D + PixelPost at 2x (with 2D fallback).
/// Drawing only; quits when done.
sealed class TroopSheet
{
    const int PixelScale = 2, HalfW = 80, HalfH = 100, W = HalfW * PixelScale, H = HalfH * PixelScale;
    readonly GraphicsDevice gd;
    readonly Shading? sh;
    readonly Puppet puppet;
    readonly Puppet3D? puppet3D;
    readonly SkinnedMesh3D? skinnedBear, skinnedWolf;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPNCT> units3D = new();
    readonly AlphaTestEffect fx;

    TroopSheet(GraphicsDevice gd, Art art, Shading? sh, MeshKit3D? kit, SkinnedMesh3D? skinnedBear = null, SkinnedMesh3D? skinnedWolf = null)
    {
        this.gd = gd;
        this.sh = sh;
        this.skinnedBear = skinnedBear;
        this.skinnedWolf = skinnedWolf;
        puppet = new Puppet(art, sprites);
        if (sh != null && kit != null) puppet3D = new Puppet3D(kit, units3D) { ExactLook = true };
        fx = new AlphaTestEffect(gd) { Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128 };
    }

    public static void Write(GraphicsDevice gd, Art art, Shading? sh, MeshKit3D? kit, string dir)
    {
        Directory.CreateDirectory(dir);
        Sky.Set(new Weather(13, 0, 0));
        using var skinnedBear = sh != null ? SkinnedMesh3D.TryLoad(gd, "bear_skinned.skin", "bear_tex.png") : null;
        using var skinnedWolf = sh != null ? SkinnedMesh3D.TryLoad(gd, "wolf_skinned.skin", "wolf_tex.png") : null;
        var sheet = new TroopSheet(gd, art, sh, kit, skinnedBear, skinnedWolf);
        var viewsOnly = Environment.GetEnvironmentVariable("IRONFIELD_TROOP_VIEWS");
        if (!string.IsNullOrEmpty(viewsOnly))
        {
            var wanted = new HashSet<string>(viewsOnly.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            float[] angles = { 0f, 0.65f, MathHelper.PiOver2, MathHelper.Pi, -MathHelper.PiOver2, -0.65f };
            int vCount = 0;
            foreach (var t in Troops.All.Where(t => wanted.Contains(t.Id)))
            {
                for (int i = 0; i < angles.Length; i++)
                    sheet.One(t, Path.Combine(dir, $"{t.Id}_v{i}.png"), angles[i]);
                vCount++;
            }
            Console.WriteLine($"{vCount} troops x {angles.Length} views -> {dir}");
            return;
        }
        int n = 0;
        foreach (var t in Troops.All.Where(t => t.Culture != "wild"))
            try { sheet.One(t, Path.Combine(dir, t.Id + ".png")); n++; }
            catch (Exception e) { Console.WriteLine($"skipped {t.Id}: {e.Message}"); }
        Console.WriteLine($"{n} troops -> {dir} ({(sheet.puppet3D != null ? "3D 2x" : "2D")})");
    }

    void One(UnitType t, string path, float angleOffset = 0.6f)
    {
        const float Facing = MathHelper.PiOver2;
        var u = new Unit { Index = 3, Palette = t.Culture == "wild" ? 5 : 0, Coat = 1, Type = t, Facing = Facing, ViewFacing = Facing, Heading = Facing, ViewHeading = Facing, EnemyDist = 2 };
        if (t.Mount != null) { u.Horse = t.Mount; u.HorseHp = t.Mount.Hp; }
        bool rider = u.Mounted;
        float fit = MathF.Max(1f, t.Size * (t.Flying ? 1.25f : 1.05f));
        float dist = (rider ? 9.2f : 4.6f) * fit, y = (rider ? 1.85f : 1.0f) * MathF.Max(1f, t.Size * 0.9f), a = Facing + angleOffset;
        var target = new Vector3(0, y, 0);
        var eye = target + new Vector3(MathF.Cos(a) * dist, 0.9f * fit, MathF.Sin(a) * dist);
        var view = Matrix.CreateLookAt(eye, target, Vector3.Up);
        var proj = Matrix.CreatePerspectiveFieldOfView(0.55f, W / (float)H, 0.1f, 100);

        Color[] px;
        if (sh != null && puppet3D != null)
        {
            using var rtCol = new RenderTarget2D(gd, HalfW, HalfH, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
            using var rtNd = new RenderTarget2D(gd, HalfW, HalfH, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            using var rtOut = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            gd.SetRenderTarget(rtNd); gd.Clear(Color.Transparent);
            gd.SetRenderTarget(rtCol); gd.Clear(Color.Transparent);
            units3D.N = 0;
            puppet3D.Now = 1f;
            puppet3D.Detail = 0;
            bool useSkinnedBear = skinnedBear != null && t.Look.Face == "bear";
            bool useSkinnedWolf = skinnedWolf != null && !useSkinnedBear && t.Look.Beast && t.Breed is "wolf" or "hound";
            if (!useSkinnedBear && !useSkinnedWolf)
            {
                puppet3D.BeginCamera(view);
                puppet3D.TryDraw(u, 0);
            }
            if (units3D.N > 0 || useSkinnedBear || useSkinnedWolf)
            {
                gd.SetRenderTargets(rtCol, rtNd);
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                var toCam = Vector3.Normalize(eye - target);
                var camRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, toCam));
                var keyDir = -Vector3.Normalize(toCam * 0.78f - camRight * 0.42f + Vector3.Up * 0.52f);
                if (units3D.N > 0)
                {
                    sh.Setup(sh.Unit3D, view * proj, eye, 1f, false, 0.45f);
                    sh.Unit3D.Parameters["SunDir"].SetValue(keyDir);
                    sh.Unit3D.Parameters["View"].SetValue(view);
                    sh.Unit3D.CurrentTechnique = sh.Unit3D.Techniques["Unit3D"];
                    sh.Unit3D.CurrentTechnique.Passes[0].Apply();
                    units3D.Draw(gd, PrimitiveType.TriangleList, units3D.N / 3);
                }
                if (useSkinnedBear || useSkinnedWolf)
                {
                    sh.Setup(sh.SkinnedUnit3D, view * proj, eye, 1f, false, 0.45f);
                    sh.SkinnedUnit3D.Parameters["SunDir"].SetValue(keyDir);
                    sh.SkinnedUnit3D.Parameters["View"].SetValue(view);
                    if (useSkinnedBear) skinnedBear!.DrawBear(gd, sh, u, 0, 1f, "Unit3D");
                    else skinnedWolf!.DrawWolf(gd, sh, u, 0, 1f, "Unit3D");
                }
            }
            sh.PixelPostTo(rtOut, rtCol, rtNd, PixelScale, Vector3.One, outlineCfg: new Vector4(0.95f, 0.85f, 0.90f, 1f));
            gd.SetRenderTarget(null);
            px = new Color[W * H];
            rtOut.GetData(px);
        }
        else
        {
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.Depth24);
            gd.SetRenderTarget(rt);
            gd.Clear(new Color(255, 0, 255));
            puppet.Now = 1;
            sprites.N = 0;
            puppet.Begin(view);
            puppet.Draw(u, 0);
            if (sprites.N > 0)
            {
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                gd.SamplerStates[0] = SamplerState.PointClamp;
                fx.World = Matrix.Identity; fx.View = view; fx.Projection = proj;
                fx.CurrentTechnique.Passes[0].Apply();
                sprites.Draw(gd, PrimitiveType.TriangleList, sprites.N / 3);
            }
            gd.SetRenderTarget(null);
            px = new Color[W * H];
            rt.GetData(px);
            for (int i = 0; i < px.Length; i++) if (px[i].R == 255 && px[i].G == 0 && px[i].B == 255) px[i] = Color.Transparent;
        }
        File.WriteAllBytes(path, SpriteDump.Png(px, W, H));
    }
}
