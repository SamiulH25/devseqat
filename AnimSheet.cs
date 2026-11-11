using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// `IRONFIELD_ANIM=/dir`: draws the soldier animations as strips of frames, large, on a plain board, and quits: one row per
/// clip (walk, run, idle, each swing from wind-up through the blow, block, a fall, a rider), eight frames a row. Written twice,
/// from three-quarter front (`anim-front.png`) and from the side (`anim-side.png`), for judging motion without playing.
/// Drawing only: the units here are never in a battle.
sealed class AnimSheet
{
    const int Cols = 8, CellW = 150, CellH = 190;

    readonly GraphicsDevice gd;
    readonly Puppet puppet;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly AlphaTestEffect fx;
    readonly BasicEffect flat;

    AnimSheet(GraphicsDevice gd, Art art)
    {
        this.gd = gd;
        puppet = new Puppet(art, sprites);
        fx = new AlphaTestEffect(gd) { Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128 };
        flat = new BasicEffect(gd) { VertexColorEnabled = true };
    }

    /// One clip: its name and how to set the man (and the clock) for frame k of n.
    record Clip(string Name, string Troop, Action<Unit, float> Set, string? Horse = null, float Span = 0);

    static Clip Swing(string name, string troop, AttackDir dir) => new(name, troop, (u, k) =>
    {
        // The first three frames wind up, the rest are the blow and the recovery.
        u.Swing = dir;
        if (k < 3 / 8f) { u.Action = UnitAction.Windup; u.ActionTimer = u.Type.Windup * (1 - (k + 1 / 8f) / (3 / 8f)); }
        else { u.Action = UnitAction.Recover; u.ActionTimer = u.Type.Recover * (1 - (k - 3 / 8f) / (5 / 8f)); }
    });

    static readonly Clip[] Clips =
    {
        new("walk", "men_at_arms", (u, k) => { u.Vel = u.Dir * 1.6f; u.Anim = k * 2; }),
        new("run", "men_at_arms", (u, k) => { u.Vel = u.Dir * 4.6f; u.Anim = k * 2; }),
        new("idle", "men_at_arms", (u, k) => { }, Span: 6),
        Swing("sword right", "men_at_arms", AttackDir.Right),
        Swing("sword overhead", "men_at_arms", AttackDir.Overhead),
        Swing("sword thrust", "men_at_arms", AttackDir.Thrust),
        Swing("great right", "greatswords", AttackDir.Right),
        Swing("great overhead", "greatswords", AttackDir.Overhead),
        Swing("pike thrust", "pikemen", AttackDir.Thrust),
        Swing("spear+shield", "spearrunners", AttackDir.Thrust),
        new("block", "men_at_arms", (u, k) => { u.Action = k < 0.5f ? UnitAction.Block : UnitAction.Stagger; u.HitFlash = k >= 0.5f ? 0.15f * (1 - (k - 0.5f) * 2) : 0; }),
        new("bow", "bowmen", (u, k) => { u.Ammo = 10; u.EnemyDist = 20; if (k > 0.75f) { u.Action = UnitAction.Recover; u.ActionTimer = 0.25f * (1 - (k - 0.75f) * 4); } else u.Cooldown = 1.1f * (1 - k / 0.75f); }),
        new("fall", "men_at_arms", (u, k) => { u.Dead = true; u.DiedAt = 0; u.DiedFall = Fall.Plain; u.DiedFrom = u.Pos + u.Dir; }, Span: 0.9f),
        new("rider cut", "knights", (u, k) => { u.Vel = u.Dir * 6; u.Anim = k * 2; u.Swing = AttackDir.Right; u.Action = k < 3 / 8f ? UnitAction.Windup : UnitAction.Recover;
            u.ActionTimer = k < 3 / 8f ? u.Type.Windup * (1 - (k + 1 / 8f) / (3 / 8f)) : u.Type.Recover * (1 - (k - 3 / 8f) / (5 / 8f)); }, Horse: "warhorse"),
    };

    public static void Write(GraphicsDevice gd, Art art, string dir, string filter)
    {
        Directory.CreateDirectory(dir);
        var sheet = new AnimSheet(gd, art);
        var clips = Clips.Where(c => filter == "" || c.Name.Contains(filter)).ToArray();
        sheet.Render(clips, 0.6f, Path.Combine(dir, "anim-front.png"));
        sheet.Render(clips, 1.45f, Path.Combine(dir, "anim-side.png"));
        Console.WriteLine($"{clips.Length} clips -> {dir}: " + string.Join(", ", clips.Select(c => c.Name)));
    }

    void Render(Clip[] clips, float camYaw, string path)
    {
        int w = Cols * CellW, h = clips.Length * CellH;
        using var rt = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24);
        gd.SetRenderTarget(rt);
        gd.Clear(new Color(118, 128, 112));
        for (int row = 0; row < clips.Length; row++)
            for (int col = 0; col < Cols; col++)
            {
                gd.Viewport = new Viewport(col * CellW, row * CellH, CellW, CellH);
                gd.Clear(ClearOptions.DepthBuffer, Color.Black, 1, 0);
                Cell(clips[row], col / (float)Cols, camYaw);
            }
        gd.SetRenderTarget(null);
        var px = new Color[w * h];
        rt.GetData(px);
        for (int row = 0; row < clips.Length; row++) // a dark line between the rows, so each strip reads on its own
            for (int x = 0; x < w; x++) px[row * CellH * w + x] = new Color(70, 76, 66);
        File.WriteAllBytes(path, SpriteDump.Png(px, w, h));
    }

    void Cell(Clip c, float k, float camYaw)
    {
        const float Facing = MathHelper.PiOver2;
        var u = new Unit { Index = 3, Palette = 0, Coat = 1, Type = Troops.Get(c.Troop), Facing = Facing, ViewFacing = Facing, Heading = Facing, ViewHeading = Facing, EnemyDist = 2 };
        if (c.Horse != null) u.Horse = Troops.Mount(c.Horse);
        c.Set(u, k);
        u.Speed = u.Vel.Length();
        puppet.Now = c.Span > 0 ? 0.001f + k * c.Span : 1;
        bool rider = u.Mounted;
        float dist = rider ? 7.5f : 4.6f, y = rider ? 1.5f : 1.0f;
        float a = Facing + camYaw;
        var target = new Vector3(0, y, 0);
        var eye = target + new Vector3(MathF.Cos(a) * dist, 0.9f, MathF.Sin(a) * dist);
        var view = Matrix.CreateLookAt(eye, target, Vector3.Up);
        var proj = Matrix.CreatePerspectiveFieldOfView(0.55f, CellW / (float)CellH, 0.1f, 100);
        Ground(view, proj);
        sprites.N = 0;
        puppet.Begin(view);
        if (Environment.GetEnvironmentVariable("IRONFIELD_ANIM_DEBUG") == c.Name) Console.WriteLine($"{k:0.00} {Puppet.PoseOf(u, puppet.Now, Ease.Hash(u.Index))}");
        puppet.Draw(u, 0);
        if (sprites.N == 0) return;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
        gd.SamplerStates[0] = SamplerState.PointClamp;
        fx.World = Matrix.Identity; fx.View = view; fx.Projection = proj;
        fx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, sprites.A, 0, sprites.N / 3);
    }

    /// A flat square of turf under him with a cross at his feet, so drift and sliding feet show.
    void Ground(Matrix view, Matrix proj)
    {
        var g = new Color(92, 110, 76);
        var m = new Color(60, 70, 50);
        var v = new List<VertexPositionColor>();
        void Q(float x0, float z0, float x1, float z1, Color c)
        {
            v.Add(new(new(x0, 0, z0), c)); v.Add(new(new(x1, 0, z0), c)); v.Add(new(new(x0, 0, z1), c));
            v.Add(new(new(x1, 0, z0), c)); v.Add(new(new(x1, 0, z1), c)); v.Add(new(new(x0, 0, z1), c));
        }
        Q(-3, -3, 3, 3, g);
        for (int i = -3; i <= 3; i++) { Q(i - 0.01f, -3, i + 0.01f, 3, m); Q(-3, i - 0.01f, 3, i + 0.01f, m); }
        gd.DepthStencilState = DepthStencilState.None;
        gd.RasterizerState = RasterizerState.CullNone;
        flat.World = Matrix.Identity; flat.View = view; flat.Projection = proj;
        flat.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, v.ToArray(), 0, v.Count / 3);
    }
}
