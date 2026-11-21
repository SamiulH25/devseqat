using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// The camp's men (docs/archive/COMPANY.md C5): the named men drawn as the battle draws them (the same puppet, the same face
/// from their soldier id), standing at their fires, into one transparent texture laid over the painted camp. A fixed camera
/// looks over the field from a little above, so a point on the ground can be found on the screen (`Project`) and the painted
/// fires, tents and banners stand where the men stand. Drawing only, like the creator's stage it is built after.
sealed class CampStage
{
    readonly GraphicsDevice gd;
    readonly Shading? shading;
    readonly Func<Quality>? qualityOf;
    readonly Puppet puppet;
    readonly Puppet3D? puppet3D;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPNCT> units3D = new();
    readonly AlphaTestEffect fx;
    RenderTarget2D? rt, rtCol, rtNd;
    float clock;
    Matrix view, proj;
    int w, h;

    /// The camera: it stands on the near side of the field, a little over a man's height up, and looks across at the fires.
    static readonly Vector3 Eye = new(0, 2.4f, 21f), Target = new(0, 1.85f, 0);
    const float Fov = 0.52f;

    public Texture2D? Texture => rt;

    public CampStage(GraphicsDevice gd, Art art, Shading? shading = null, MeshKit3D? meshKit = null, Func<Quality>? qualityOf = null)
    {
        this.gd = gd;
        this.shading = shading;
        this.qualityOf = qualityOf;
        puppet = new Puppet(art, sprites);
        if (meshKit != null) puppet3D = new Puppet3D(meshKit, units3D);
        fx = new AlphaTestEffect(gd) { Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128 };
    }

    /// Set the camera for a `width` x `height` picture (also done by `Render`); `Project` works from then on.
    public void Frame(int width, int height)
    {
        w = width; h = height;
        view = Matrix.CreateLookAt(Eye, Target, Vector3.Up);
        proj = Matrix.CreatePerspectiveFieldOfView(Fov, w / (float)Math.Max(1, h), 0.1f, 200);
    }

    /// Where a point of the field (X across, Z toward the camera, Y up) falls on the picture, in its pixels.
    public Vector2 Project(Vector3 p)
    {
        var v = new Viewport(0, 0, Math.Max(1, w), Math.Max(1, h)).Project(p, proj, view, Matrix.Identity);
        return new Vector2(v.X, v.Y);
    }

    /// How many of the picture's pixels a metre is, standing at depth `z`.
    public float PerMetre(float z) => Project(new Vector3(0, 0, z)).Y - Project(new Vector3(0, 1, z)).Y;

    /// How far across the field the picture reaches at depth `z` (half its width, in metres).
    public float HalfWidth(float z) => w / 2f / MathF.Max(1, PerMetre(z));

    /// Draw `men` into a `width` x `height` texture. `pixel` is the size of one art pixel (the painted camp's), `light` the
    /// hour's light on them (1 by day), `cheer` raises their arms.
    public void Render(int width, int height, IReadOnlyList<Unit> men, int pixel, float light, bool cheer, float dt)
    {
        if (width < 16 || height < 16) return;
        if (rt == null || rt.Width != width || rt.Height != height)
        {
            rt?.Dispose();
            rt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24);
        }
        Frame(width, height);
        clock += Ui.Instant ? 0 : dt;
        if (Ui.Instant) clock = 3.2f; // shots: a fixed moment of the idles
        var keep = gd.GetRenderTargets();
        var tint = new Vector3(light, light * (light < 0.8f ? 0.92f : 1), light * (light < 0.8f ? 1.06f : 1)); // night is bluer
        bool do3D = shading != null && puppet3D != null && (qualityOf?.Invoke() ?? Quality.High) != Quality.Low;
        if (do3D)
        {
            int hw = Math.Max(8, width / pixel), hh = Math.Max(8, height / pixel);
            if (rtCol == null || rtNd == null || rtCol.Width != hw || rtCol.Height != hh)
            {
                rtCol?.Dispose(); rtNd?.Dispose();
                rtCol = new RenderTarget2D(gd, hw, hh, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
                rtNd = new RenderTarget2D(gd, hw, hh, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            }
            Sky.Set(new Weather(13, 0, 0));
            units3D.N = 0;
            puppet3D!.Now = clock;
            puppet3D.Winner = cheer ? 0 : -1;
            puppet3D.Detail = 0;
            puppet3D.BeginCamera(view);
            foreach (var u in men) puppet3D.TryDraw(u, 0);
            gd.SetRenderTarget(rtNd);
            gd.Clear(Color.Transparent);
            gd.SetRenderTarget(rtCol);
            gd.Clear(Color.Transparent);
            if (units3D.N > 0)
            {
                gd.SetRenderTargets(rtCol, rtNd);
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                shading!.Setup(shading.Unit3D, view * proj, Eye, clock, false, 0.45f);
                var toCam = Vector3.Normalize(Eye - Target);
                var camRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, toCam));
                var keyDir = -Vector3.Normalize(toCam * 0.8f - camRight * 0.35f + Vector3.Up * 0.5f);
                shading.Unit3D.Parameters["SunDir"].SetValue(keyDir);
                shading.Unit3D.Parameters["View"].SetValue(view);
                shading.Unit3D.CurrentTechnique = shading.Unit3D.Techniques["Unit3D"];
                shading.Unit3D.CurrentTechnique.Passes[0].Apply();
                units3D.Draw(gd, PrimitiveType.TriangleList, units3D.N / 3);
            }
            shading!.PixelPostTo(rt, rtCol, rtNd, pixel, tint, outlineCfg: new Vector4(0.95f, 0.85f, 0.90f, 1f));
        }
        else
        {
            sprites.N = 0;
            puppet.Now = clock;
            puppet.Winner = cheer ? 0 : -1;
            puppet.Begin(view);
            foreach (var u in men) puppet.Draw(u, 0);
            gd.SetRenderTarget(rt);
            gd.Clear(Color.Transparent);
            if (sprites.N > 0)
            {
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                gd.SamplerStates[0] = SamplerState.PointClamp;
                fx.World = Matrix.Identity; fx.View = view; fx.Projection = proj;
                fx.DiffuseColor = tint;
                fx.CurrentTechnique.Passes[0].Apply();
                sprites.Draw(gd, PrimitiveType.TriangleList, sprites.N / 3);
            }
        }
        gd.SetRenderTargets(keep);
    }
}
