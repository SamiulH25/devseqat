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
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPNCT> units3D = new();
    readonly AlphaTestEffect fx;

    TroopSheet(GraphicsDevice gd, Art art, Shading? sh, MeshKit3D? kit)
    {
        this.gd = gd;
        this.sh = sh;
        puppet = new Puppet(art, sprites);
        if (sh != null && kit != null) puppet3D = new Puppet3D(kit, units3D) { ExactLook = true };
        fx = new AlphaTestEffect(gd) { Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128 };
    }

    public static void Write(GraphicsDevice gd, Art art, Shading? sh, MeshKit3D? kit, string dir)
    {
        Directory.CreateDirectory(dir);
        Sky.Set(new Weather(13, 0, 0));
        var sheet = new TroopSheet(gd, art, sh, kit);
        int n = 0;
        foreach (var t in Troops.All.Where(t => t.Culture != "wild"))
            try { sheet.One(t, Path.Combine(dir, t.Id + ".png")); n++; }
            catch (Exception e) { Console.WriteLine($"skipped {t.Id}: {e.Message}"); }
        Console.WriteLine($"{n} troops -> {dir} ({(sheet.puppet3D != null ? "3D 2x" : "2D")})");
    }

    void One(UnitType t, string path)
    {
        const float Facing = MathHelper.PiOver2;
        var u = new Unit { Index = 3, Palette = 0, Coat = 1, Type = t, Facing = Facing, ViewFacing = Facing, Heading = Facing, ViewHeading = Facing, EnemyDist = 2 };
        if (t.Mount != null) { u.Horse = t.Mount; u.HorseHp = t.Mount.Hp; }
        bool rider = u.Mounted;
        float dist = rider ? 9.2f : 4.6f, y = rider ? 1.85f : 1.0f, a = Facing + 0.6f;
        var target = new Vector3(0, y, 0);
        var eye = target + new Vector3(MathF.Cos(a) * dist, 0.9f, MathF.Sin(a) * dist);
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
            puppet3D.BeginCamera(view);
            puppet3D.TryDraw(u, 0);
            if (units3D.N > 0)
            {
                gd.SetRenderTargets(rtCol, rtNd);
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                sh.Setup(sh.Unit3D, view * proj, eye, 1f, false, 0.45f);
                var toCam = Vector3.Normalize(eye - target);
                var camRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, toCam));
                var keyDir = -Vector3.Normalize(toCam * 0.78f - camRight * 0.42f + Vector3.Up * 0.52f);
                sh.Unit3D.Parameters["SunDir"].SetValue(keyDir);
                sh.Unit3D.Parameters["View"].SetValue(view);
                sh.Unit3D.CurrentTechnique = sh.Unit3D.Techniques["Unit3D"];
                sh.Unit3D.CurrentTechnique.Passes[0].Apply();
                units3D.Draw(gd, PrimitiveType.TriangleList, units3D.N / 3);
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
