using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// The character creator's living captain: the battle puppet in the captain's own kit and face, drawn into a texture that the
/// portrait well shows. Each step gives it something to do (it stands for its homeland, marches out from its birth, acts out
/// its past, shows how it fights, turns its face to you, stands ready for the oath) and the camera eases to frame it.
/// Drawing only: the unit here is never in a battle.
sealed class CreatorStage
{
    public enum Act { Stand, March, Leave, Drill, Guard, Ride, Charge, Shoot, Turn }

    readonly GraphicsDevice gd;
    readonly Shading? shading;
    readonly Func<Quality>? qualityOf;
    readonly Puppet puppet;
    readonly Puppet3D? puppet3D;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPNCT> units3D = new();
    readonly AlphaTestEffect fx;
    RenderTarget2D? rt, rtCol, rtNd;
    readonly Unit u = new() { IsPlayer = true, Index = 1, Coat = 2 };
    Act act = Act.Stand;
    float clock, beatLeft;
    int beat;
    // The camera eases toward the act's framing: the height it looks at, its distance and its angle round the captain.
    float camY = 1.05f, camDist = 5.2f, camYaw = 0.55f;
    bool started;
    // The kit as last built, rebuilt when the captain or the helm changes.
    CaptainState? built;
    bool builtBare;
    UnitType foot = null!, spear = null!, bow = null!, face = null!;
    Mount horse = null!;

    /// The captain's facing on the stage (radians on the ground plane); the camera stands round it by `camYaw`.
    const float Facing = MathHelper.PiOver2;
    const float Fov = 0.5f;

    public Texture2D? Texture => rt;
    /// Where the feet stand on the texture, in its pixels (for the shadow), and how wide the shadow is there.
    public Vector2 Feet;
    public float ShadowW;

    public CreatorStage(GraphicsDevice gd, Art art, Shading? shading = null, MeshKit3D? meshKit = null, Func<Quality>? qualityOf = null)
    {
        this.gd = gd;
        this.shading = shading;
        this.qualityOf = qualityOf;
        puppet = new Puppet(art, sprites);
        if (meshKit != null) puppet3D = new Puppet3D(meshKit, units3D) { ExactLook = true };
        fx = new AlphaTestEffect(gd) { Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128 };
    }

    /// Advance the act by `dt` and draw it into a `w` x `h` texture. `bare` takes the helm off.
    public void Render(int w, int h, CaptainState c, bool bare, Act next, float dt, int palette = 2)
    {
        if (w < 16 || h < 16) return;
        u.Palette = palette;
        if (rt == null || rt.Width != w || rt.Height != h)
        {
            rt?.Dispose();
            rt = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24);
        }
        if (built != c || builtBare != bare) Build(c, bare);
        if (next != act) { act = next; beat = 0; beatLeft = 0; u.Action = UnitAction.None; u.Charging = false; u.Charge = 0; }
        clock += dt;
        Perform(dt);
        Camera(dt, w / (float)h, out var view, out var proj, out var eye);

        var feet = new Viewport(0, 0, w, h).Project(Vector3.Zero, proj, view, Matrix.Identity);
        var side = new Viewport(0, 0, w, h).Project(new Vector3(u.Mounted ? 1.4f : 0.45f, 0, 0), proj, view, Matrix.Identity);
        Feet = new Vector2(feet.X, feet.Y);
        ShadowW = MathF.Max(8, MathF.Abs(side.X - feet.X) * 2);

        bool do3D = shading != null && puppet3D != null && Settings.Current.EffectivePixelArt && (qualityOf?.Invoke() ?? Quality.High) != Quality.Low;
        var keep = gd.GetRenderTargets();
        if (do3D)
        {
            int pScale = Settings.Current.EffectivePixelScale;
            int hw = Math.Max(8, w / pScale), hh = Math.Max(8, h / pScale);
            if (rtCol == null || rtNd == null || rtCol.Width != hw || rtCol.Height != hh)
            {
                rtCol?.Dispose(); rtNd?.Dispose();
                rtCol = new RenderTarget2D(gd, hw, hh, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
                rtNd = new RenderTarget2D(gd, hw, hh, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            }
            Sky.Set(new Weather(13, 0, 0));
            units3D.N = 0;
            puppet3D!.Now = clock;
            puppet3D.Winner = -1;
            puppet3D.Detail = 0;
            puppet3D.BeginCamera(view);
            puppet3D.TryDraw(u, 0);

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
                shading!.Setup(shading.Unit3D, view * proj, eye, clock, false, 0.45f);
                var toCam = Vector3.Normalize(eye - new Vector3(0, camY, 0));
                var camRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, toCam));
                var keyDir = -Vector3.Normalize(toCam * 0.88f - camRight * 0.26f + Vector3.Up * 0.42f);
                shading.Unit3D.Parameters["SunDir"].SetValue(keyDir);
                shading.Unit3D.Parameters["View"].SetValue(view);
                shading.Unit3D.CurrentTechnique = shading.Unit3D.Techniques["Unit3D"];
                shading.Unit3D.CurrentTechnique.Passes[0].Apply();
                units3D.Draw(gd, PrimitiveType.TriangleList, units3D.N / 3);
            }
            shading!.PixelPostTo(rt, rtCol, rtNd, pScale, Vector3.One, outlineCfg: new Vector4(0.95f, 0.85f, 0.90f, 1f));
        }
        else
        {
            sprites.N = 0;
            puppet.Now = clock;
            puppet.Winner = -1;
            puppet.Begin(view);
            puppet.Draw(u, 0);
            gd.SetRenderTarget(rt);
            gd.Clear(Color.Transparent);
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
        }
        gd.SetRenderTargets(keep);
    }

    void Build(CaptainState c, bool bare)
    {
        built = c; builtBare = bare;
        UnitType Dress(UnitType t) { if (bare) t.Look = t.Look with { Head = "none", Plume = false }; return t; }
        foot = Dress(Rules.CaptainType(c));
        // The lance: the second weapon if it is a spear, else the hand weapon (a knight rides with what he has).
        var side = c.Kit.Side != "" ? Gear.Get(c.Kit.Side) : null;
        spear = side is { Look: "spear" or "lance" or "pike" } ? Dress(Rules.CaptainType(c, side: true)) : foot;
        // The bow: the captain's own if they carry one, else a hunting bow (a poacher's past, a bowman's road).
        var ranged = Gear.Get(c.Kit.Weapon).Ranged ? Rules.CaptainType(c) : side is { Ranged: true } ? Rules.CaptainType(c, side: true) : null;
        bow = (ranged ?? foot).Clone();
        if (ranged == null) bow.Look = bow.Look with { Weapon = "bow", Shield = "none" };
        bow.Class = UnitClass.Archer;
        bow.Range = MathF.Max(bow.Range, 30);
        // Turning to show the face: the shield set down so it does not hide it.
        face = foot.Clone();
        face.Look = face.Look with { Shield = "none" };
        horse = Rules.CaptainMount(c);
    }

    /// Set the unit's state for this moment of the act: the puppet's poses read it as they do in battle.
    void Perform(float dt)
    {
        bool mounted = act is Act.Ride or Act.Charge;
        u.Type = act switch { Act.Shoot => bow, Act.Charge => spear, Act.Turn => face, _ => foot };
        u.Horse = mounted ? horse : null;
        u.Couched = act == Act.Charge && u.Type != foot;
        u.Ammo = 0; u.EnemyDist = float.MaxValue; u.HitFlash = 0;
        float yaw = Facing;
        if (act == Act.Turn) yaw += MathF.Sin(clock * 0.7f) * 0.78f; // looks left, then right
        if (act == Act.Leave) yaw += MathHelper.Pi;                 // walks away from you
        u.Facing = u.ViewFacing = u.Heading = u.ViewHeading = yaw;
        float speed = act switch { Act.March or Act.Leave => 1.5f, Act.Ride => 4.5f, Act.Charge => 11f, _ => 0 };
        u.Speed = speed;
        u.Vel = u.Dir * speed;
        u.Anim += dt * Battle.StrideRate(speed, mounted);
        switch (act)
        {
            case Act.Drill: Drill(dt); break;
            case Act.Guard: u.Action = UnitAction.Block; break;
            case Act.Shoot: Shoot(dt); break;
            default: if (!mounted) { u.Action = UnitAction.None; u.Charging = false; } break;
        }
    }

    /// Beats of a routine: (action, swing, seconds); Charging means raising the blow (the captain's own swing in battle).
    static readonly (UnitAction Act, AttackDir Swing, float Time, bool Raise)[] DrillBeats =
    {
        (UnitAction.None, AttackDir.Right, 0.45f, true), (UnitAction.Windup, AttackDir.Right, 0.15f, false), (UnitAction.Recover, AttackDir.Right, 0.3f, false),
        (UnitAction.None, AttackDir.Right, 0.35f, false),
        (UnitAction.None, AttackDir.Overhead, 0.5f, true), (UnitAction.Windup, AttackDir.Overhead, 0.2f, false), (UnitAction.Recover, AttackDir.Overhead, 0.3f, false),
        (UnitAction.None, AttackDir.Overhead, 0.3f, false),
        (UnitAction.Block, AttackDir.Overhead, 0.8f, false), (UnitAction.None, AttackDir.Right, 0.7f, false),
    };

    void Drill(float dt)
    {
        if (beatLeft <= 0) { beat = (beat + 1) % DrillBeats.Length; beatLeft = DrillBeats[beat].Time; }
        beatLeft -= dt;
        var b = DrillBeats[beat];
        u.Swing = b.Swing;
        u.Action = b.Act;
        u.ActionTimer = MathF.Max(0, beatLeft);
        u.Charging = b.Raise;
        u.Charge = b.Raise ? Ease.Out(1 - beatLeft / b.Time) : 0;
    }

    /// Nock and draw, loose, lower the bow, again.
    void Shoot(float dt)
    {
        const float draw = 1.1f;
        if (beatLeft <= 0) { beat = (beat + 1) % 3; beatLeft = beat switch { 0 => draw, 1 => 0.3f, _ => 0.6f }; }
        beatLeft -= dt;
        u.Ammo = 10;
        u.EnemyDist = beat == 2 ? float.MaxValue : 10;
        u.Action = beat == 1 ? UnitAction.Recover : UnitAction.None;
        u.Cooldown = beat == 0 ? MathF.Max(0, beatLeft) : 0;
    }

    /// Ease the camera toward the act's framing: a rider seen from the side, the whole man near for the likeness, the rest three-quarter.
    void Camera(float dt, float aspect, out Matrix view, out Matrix proj, out Vector3 eye)
    {
        var (y, dist, yaw) = act switch
        {
            Act.Ride or Act.Charge => (1.52f, 9.4f, 1.45f),
            Act.Turn => (1.15f, 4.7f, 0.25f),
            Act.Leave => (1.25f, 6.5f, 0.4f),
            Act.Drill or Act.Guard => (1.22f, 7.7f, 0.55f),
            _ => (1.22f, 5.8f, 0.55f),
        };
        if (aspect < 0.9f) dist *= 0.9f / aspect * 0.95f; // a narrow well needs a step back to fit the width
        float k = started && !Ui.Instant ? 1 - MathF.Exp(-dt * 3.5f) : 1;
        started = true;
        camY += (y - camY) * k; camDist += (dist - camDist) * k; camYaw += (yaw - camYaw) * k;
        float a = Facing + camYaw;
        var target = new Vector3(0, camY, 0);
        eye = target + new Vector3(MathF.Cos(a) * camDist, camDist * 0.06f, MathF.Sin(a) * camDist);
        view = Matrix.CreateLookAt(eye, target, Vector3.Up);
        proj = Matrix.CreatePerspectiveFieldOfView(Fov, aspect, 0.1f, 100);
    }
}
