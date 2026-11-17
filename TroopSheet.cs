using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// `IRONFIELD_TROOPS=/dir`: draws every man of the troop roster standing, three-quarter front, in the first palette, one
/// transparent PNG per troop (`<id>.png`), for the troop-tree artifact and docs. Drawing only; quits when done.
sealed class TroopSheet
{
    const int W = 150, H = 190;
    readonly GraphicsDevice gd;
    readonly Puppet puppet;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly AlphaTestEffect fx;

    TroopSheet(GraphicsDevice gd, Art art)
    {
        this.gd = gd;
        puppet = new Puppet(art, sprites);
        fx = new AlphaTestEffect(gd) { Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128 };
    }

    public static void Write(GraphicsDevice gd, Art art, string dir)
    {
        Directory.CreateDirectory(dir);
        var sheet = new TroopSheet(gd, art);
        int n = 0;
        foreach (var t in Troops.All.Where(t => t.Culture != "wild"))
            try { sheet.One(t, Path.Combine(dir, t.Id + ".png")); n++; }
            catch (Exception e) { Console.WriteLine($"skipped {t.Id}: {e.Message}"); }
        Console.WriteLine($"{n} troops -> {dir}");
    }

    void One(UnitType t, string path)
    {
        const float Facing = MathHelper.PiOver2;
        using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.Depth24);
        gd.SetRenderTarget(rt);
        gd.Clear(new Color(255, 0, 255));
        var u = new Unit { Index = 3, Palette = 0, Coat = 1, Type = t, Facing = Facing, ViewFacing = Facing, Heading = Facing, ViewHeading = Facing, EnemyDist = 2 };
        if (t.Mount != null) { u.Horse = t.Mount; u.HorseHp = t.Mount.Hp; }
        puppet.Now = 1;
        bool rider = u.Mounted;
        float dist = rider ? 9.5f : 4.6f, y = rider ? 1.9f : 1.0f, a = Facing + 0.6f;
        var target = new Vector3(0, y, 0);
        var view = Matrix.CreateLookAt(target + new Vector3(MathF.Cos(a) * dist, 0.9f, MathF.Sin(a) * dist), target, Vector3.Up);
        var proj = Matrix.CreatePerspectiveFieldOfView(0.55f, W / (float)H, 0.1f, 100);
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
        var px = new Color[W * H];
        rt.GetData(px);
        for (int i = 0; i < px.Length; i++) if (px[i].R == 255 && px[i].G == 0 && px[i].B == 255) px[i] = Color.Transparent;
        File.WriteAllBytes(path, Png.Encode(px, W, H));
    }
}
