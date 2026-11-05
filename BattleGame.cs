using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// Rendering, camera and input around a headless Battle. Two views with an eased camera blend between them:
/// field (third person, you fight) and command (free orbit camera, time slowed, you direct squads).
public sealed class BattleGame : Game
{
    const float CommandTimeScale = 0.25f, BlendTime = 0.45f;
    static readonly Color Sky = new(152, 182, 206), Gold = new(240, 200, 60), Sel = new(255, 236, 90),
        Friendly = new(140, 190, 255), Foe = new(255, 110, 90), ArrowColor = new(90, 62, 34);
    static readonly SamplerState GroundSampler = new()
    {
        Filter = TextureFilter.PointMipLinear, AddressU = TextureAddressMode.Wrap, AddressV = TextureAddressMode.Wrap,
    };

    SpriteBatch sb = null!;
    Art art = null!;
    PixelFont font = null!;
    Puppet puppet = null!;
    AlphaTestEffect spriteFx = null!;
    BasicEffect groundFx = null!, shadowFx = null!, lineFx = null!;
    Battle battle = null!;
    readonly List<(Vector2 Pos, int Kind, float Scale)> props = new();
    readonly HashSet<Squad> selected = new();
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPositionTexture> shadows = new();
    readonly Verts<VertexPositionColor> lines = new();
    VertexPositionTexture[] ground = null!;

    bool command, skipMouse = true;
    float yaw, pitch;                         // field camera: third person behind the player
    float camYaw, camPitch, zoom = 45;        // command camera: free orbit around camFocus
    float blend, frameDt;                     // blend: 0 = field view, 1 = command view
    Vector2 camFocus, mouseGround, aimAcc;
    AttackDir aimDir = AttackDir.Overhead;    // picked by the direction the mouse last moved
    Vector2? dragStart;
    // Debug: IRONFIELD_SHOTS=/dir stages a few camera shots, saves them as PNGs and quits.
    readonly string? shotDir = Environment.GetEnvironmentVariable("IRONFIELD_SHOTS") is { Length: > 0 } sd ? sd : null;
    int shotIndex;
    string? pendingShot;
    KeyboardState kb, pkb;
    MouseState ms, pms;
    Matrix view, proj;

    public BattleGame()
    {
        new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720 };
        Window.AllowUserResizing = true;
        Window.Title = "Ironfield - battle prototype";
    }

    protected override void LoadContent()
    {
        sb = new SpriteBatch(GraphicsDevice);
        art = new Art(GraphicsDevice);
        if (Environment.GetEnvironmentVariable("IRONFIELD_DUMP_ATLAS") is { Length: > 0 } atlasPath)
        {
            using var file = File.Create(atlasPath); // debug: inspect the generated sprite sheet
            art.Atlas.SaveAsPng(file, art.Atlas.Width, art.Atlas.Height);
        }
        font = new PixelFont(GraphicsDevice);
        puppet = new Puppet(art, sprites);
        spriteFx = new AlphaTestEffect(GraphicsDevice)
        {
            Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128,
            FogEnabled = true, FogColor = Sky.ToVector3(), FogStart = 70, FogEnd = 190,
        };
        groundFx = new BasicEffect(GraphicsDevice)
        {
            TextureEnabled = true, Texture = art.Grass, FogEnabled = true, FogColor = Sky.ToVector3(), FogStart = 70, FogEnd = 190,
        };
        shadowFx = new BasicEffect(GraphicsDevice) { TextureEnabled = true, Texture = art.Shadow };
        lineFx = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };

        const float g = 220, uv = g / 3; // one grass tile per 3 m
        ground = new VertexPositionTexture[]
        {
            new(new(-g, 0, -g), new(-uv, -uv)), new(new(g, 0, -g), new(uv, -uv)), new(new(-g, 0, g), new(-uv, uv)),
            new(new(g, 0, -g), new(uv, -uv)), new(new(g, 0, g), new(uv, uv)), new(new(-g, 0, g), new(-uv, uv)),
        };

        var r = new Random(3);
        while (props.Count < 160)
        {
            var p = new Vector2(r.NextSingle() * 360 - 180, r.NextSingle() * 360 - 180);
            bool field = MathF.Abs(p.X) < 45 && MathF.Abs(p.Y) < 55;
            int kind = field ? 2 + r.Next(2) : r.Next(4);      // only bushes and rocks on the battlefield
            if (field && r.Next(4) != 0) continue;
            props.Add((p, kind, kind < 2 ? 3.5f + r.NextSingle() * 2 : 1 + r.NextSingle() * 0.5f));
        }
        NewBattle();
    }

    void NewBattle()
    {
        battle = new Battle(Environment.TickCount);
        selected.Clear();
        yaw = MathHelper.PiOver2;
        pitch = 0.3f;
        blend = 0;
        SetCommand(false);
    }

    void SetCommand(bool on)
    {
        command = on;
        IsMouseVisible = on;
        dragStart = null;
        if (on) { camFocus = battle.Player.Pos; camYaw = yaw; camPitch = 0.95f; }
        else skipMouse = true;
    }

    bool Pressed(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
    float Axis(Keys pos, Keys neg) => (kb.IsKeyDown(pos) ? 1 : 0) - (kb.IsKeyDown(neg) ? 1 : 0);

    protected override void Update(GameTime gameTime)
    {
        pkb = kb; pms = ms;
        kb = Keyboard.GetState(); ms = Mouse.GetState();
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 30f);
        if (Pressed(Keys.Escape)) Exit();
        if (battle.Winner >= 0 && Pressed(Keys.Enter)) NewBattle();

        bool alive = battle.Player.Active;
        if (!alive && !command) SetCommand(true);
        else if (alive && Pressed(Keys.Tab)) SetCommand(!command);

        OrderKeys();
        if (command) CommandControls(dt); else FieldControls();
        battle.Commanding = command;
        battle.Update(dt * (command && alive ? CommandTimeScale : 1));
        blend = Math.Clamp(blend + (command ? dt : -dt) / BlendTime, 0, 1);
        frameDt = dt;
        if (shotDir != null) ShotScript();
        UpdateCamera();
        base.Update(gameTime);
    }

    void ShotScript()
    {
        var b = battle;
        var cav = b.SquadsOf(1).First(s => s.Cavalry);
        switch (shotIndex)
        {
            case 0 when b.Time > 0.5f:
                b.Player.Pos = new(17, -37); b.Player.Heading = 0.35f; yaw = 0.35f; pitch = 0.12f; // beside our own lancers
                pendingShot = "field-horses"; shotIndex++; break;
            case 1 when b.Time > 13f:
                SetCommand(true); blend = 1; camFocus = cav.Centroid(); camYaw = 2.4f; camPitch = 0.55f; zoom = 26;
                pendingShot = "command-cav-charge"; shotIndex++; break;
            case 2 when b.Time > 13.6f:
                camFocus = cav.Centroid(); camYaw = 0.3f; camPitch = 0.35f; zoom = 14;
                pendingShot = "cav-impact"; shotIndex++; break;
            case 3 when b.Time > 20f:
                camFocus = b.Centroid(0); camYaw = 1.9f; camPitch = 0.9f; zoom = 45;
                pendingShot = "command-overview"; shotIndex++; break;
            case 4 when pendingShot == null: Exit(); break;
        }
    }

    // ---------- orders ----------

    List<Squad> Targets() =>
        (selected.Count > 0 ? selected : battle.SquadsOf(0)).Where(s => s.Alive > 0).ToList();

    void Issue(string what, Action<Squad> act)
    {
        var list = Targets();
        if (list.Count == 0) return;
        foreach (var s in list) act(s);
        battle.Log((selected.Count == 0 ? "All troops" : string.Join(", ", list.Select(s => s.Name))) + ": " + what);
    }

    void OrderKeys()
    {
        var mine = battle.SquadsOf(0).ToList();
        for (int i = 0; i < mine.Count && i < 9; i++)
            if (Pressed(Keys.D1 + i) && !selected.Remove(mine[i])) selected.Add(mine[i]);
        if (Pressed(Keys.D0) || Pressed(Keys.OemTilde)) selected.Clear();
        if (Pressed(Keys.F)) Issue("follow me!", s => s.Order = Order.Follow);
        if (Pressed(Keys.C)) Issue("charge!", s => { s.Order = Order.Charge; s.Target = null; });
        if (Pressed(Keys.V)) Issue("change spacing", s => s.Loose = !s.Loose);
        if (Pressed(Keys.G))
        {
            if (command) Issue("hold position", s => s.HoldInPlace());
            else
            {
                // Form up across the player's view, a few steps ahead.
                var p = battle.Player;
                var center = p.Pos + p.Dir * 3;
                LineAt(center, center + p.Dir * 10);
                battle.Log("Form up on me!");
            }
        }
    }

    /// Line of the selected squads' combined width, centred on `center`, facing `toward`.
    void LineAt(Vector2 center, Vector2 toward)
    {
        var list = Targets();
        float w = list.Sum(s => Math.Min(s.Columns, s.Alive) * s.Spacing);
        var to = toward - center;
        if (to.LengthSquared() < 1e-3f) to = Vector2.UnitY;
        to.Normalize();
        var side = new Vector2(-to.Y, to.X);
        Distribute(center - side * w / 2, center + side * w / 2, toward);
    }

    /// Split the segment a-b between target squads, proportional to their size.
    void Distribute(Vector2 a, Vector2 b, Vector2 toward)
    {
        var list = Targets();
        float total = list.Sum(s => s.Alive), acc = 0;
        foreach (var s in list)
        {
            var a1 = Vector2.Lerp(a, b, acc / total);
            acc += s.Alive;
            s.SetLine(a1, Vector2.Lerp(a, b, acc / total), toward);
        }
    }

    // ---------- controls ----------

    static Vector2 Forward(float yaw) => new(MathF.Cos(yaw), MathF.Sin(yaw));
    static Vector2 RightOf(Vector2 f) => new(-f.Y, f.X); // screen right when looking along f

    void FieldControls()
    {
        if (IsActive)
        {
            // shortcut: mouse look by warping to the centre; switch to SDL relative mode if a platform refuses warps.
            var c = new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            var d = ms.Position - c;
            if (!skipMouse)
            {
                yaw += d.X * 0.0035f;
                pitch = Math.Clamp(pitch + d.Y * 0.0035f, -0.35f, 1.1f);
                // Mount & Blade style directions: the way the mouse moves picks the swing (and the block).
                aimAcc = aimAcc * 0.8f + new Vector2(d.X, d.Y);
                if (aimAcc.LengthSquared() > 36)
                    aimDir = MathF.Abs(aimAcc.X) > MathF.Abs(aimAcc.Y)
                        ? (aimAcc.X > 0 ? AttackDir.Right : AttackDir.Left)
                        : (aimAcc.Y < 0 ? AttackDir.Overhead : AttackDir.Thrust);
            }
            skipMouse = false;
            Mouse.SetPosition(c.X, c.Y);
        }
        else skipMouse = true;

        var f = Forward(yaw);
        bool mounted = battle.Player.Mounted;
        var move = mounted ? Vector2.Zero : f * Axis(Keys.W, Keys.S) + RightOf(f) * Axis(Keys.D, Keys.A);
        if (move != Vector2.Zero) move.Normalize();
        battle.Input = new PlayerInput
        {
            Move = move,
            Throttle = mounted ? Axis(Keys.W, Keys.S) : 0, // on horseback W/S is the rein, A/D turns the horse
            Steer = mounted ? Axis(Keys.D, Keys.A) : 0,
            Dir = aimDir,
            Couch = Pressed(Keys.Q),
            Mount = Pressed(Keys.H),
            Facing = yaw, // the player looks (and strikes) where the camera looks
            Sprint = kb.IsKeyDown(Keys.LeftShift),
            Attack = IsActive && ms.LeftButton == ButtonState.Pressed,
            Block = IsActive && ms.RightButton == ButtonState.Pressed,
            Rally = Pressed(Keys.R),
        };
    }

    void CommandControls(float dt)
    {
        var f = Forward(camYaw);
        camFocus += (f * Axis(Keys.W, Keys.S) + RightOf(f) * Axis(Keys.D, Keys.A)) * zoom * 0.9f * dt;
        camYaw += Axis(Keys.E, Keys.Q) * 1.6f * dt;
        if (ms.MiddleButton == ButtonState.Pressed && pms.MiddleButton == ButtonState.Pressed)
        {
            // Middle-drag orbits: horizontal turns, vertical tilts.
            camYaw += (ms.X - pms.X) * 0.006f;
            camPitch = Math.Clamp(camPitch + (ms.Y - pms.Y) * 0.006f, 0.3f, 1.45f);
        }
        zoom = Math.Clamp(zoom - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.03f, 12, 90);
        battle.Input = new PlayerInput { Dir = aimDir };
        if (!IsActive) return;
        mouseGround = GroundUnderMouse();

        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released)
        {
            bool shift = kb.IsKeyDown(Keys.LeftShift);
            var sq = PickSquad(mouseGround, 0);
            if (sq == null) { if (!shift) selected.Clear(); }
            else if (shift) { if (!selected.Remove(sq)) selected.Add(sq); }
            else { selected.Clear(); selected.Add(sq); }
        }
        if (ms.RightButton == ButtonState.Pressed && pms.RightButton == ButtonState.Released) dragStart = mouseGround;
        if (ms.RightButton == ButtonState.Released && dragStart is Vector2 a)
        {
            dragStart = null;
            var enemy = battle.Centroid(1);
            bool click = Vector2.Distance(a, mouseGround) < 2;
            if (click && PickSquad(mouseGround, 1) is Squad foe)
            {
                Issue("attack " + foe.Name + "!", s => { s.Order = Order.Charge; s.Target = foe; }); // click an enemy: attack it
                return;
            }
            if (click) LineAt(mouseGround, enemy);    // click: move, keep width
            else Distribute(a, mouseGround, enemy);   // drag: draw the line
            battle.Log(selected.Count == 0 ? "All troops: move" : "Squads moving");
        }
    }

    Squad? PickSquad(Vector2 p, int team)
    {
        Unit? best = null;
        float bd = 2f * 2f;
        foreach (var u in battle.Units)
        {
            if (u.Team != team || u.Squad == null || !u.Active || u.Routing) continue;
            float d = Vector2.DistanceSquared(u.Pos, p);
            if (d < bd) { bd = d; best = u; }
        }
        return best?.Squad;
    }

    Vector2 GroundUnderMouse()
    {
        var vp = GraphicsDevice.Viewport;
        var near = vp.Unproject(new Vector3(ms.X, ms.Y, 0), proj, view, Matrix.Identity);
        var far = vp.Unproject(new Vector3(ms.X, ms.Y, 1), proj, view, Matrix.Identity);
        var d = far - near;
        if (MathF.Abs(d.Y) < 1e-5f) return camFocus;
        var hit = near + d * (-near.Y / d.Y);
        return new Vector2(hit.X, hit.Z);
    }

    void UpdateCamera()
    {
        // Both views are an orbit (focus, yaw, pitch, distance); the transition eases every parameter.
        var p = battle.Player.Pos;
        var shoulder = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw)) * 0.8f; // over the right shoulder
        bool mounted = battle.Player.Mounted;
        var fieldFocus = new Vector3(p.X, mounted ? 2.6f : 1.7f, p.Y) + shoulder;
        var cmdFocus = new Vector3(camFocus.X, 0, camFocus.Y);
        float t = MathHelper.SmoothStep(0, 1, blend);
        var focus = Vector3.Lerp(fieldFocus, cmdFocus, t);
        float y = yaw + MathHelper.WrapAngle(camYaw - yaw) * t; // shortest way round
        float pt = MathHelper.Lerp(pitch, camPitch, t), dist = MathHelper.Lerp(mounted ? 7f : 5.5f, zoom, t);
        var look = new Vector3(MathF.Cos(y) * MathF.Cos(pt), -MathF.Sin(pt), MathF.Sin(y) * MathF.Cos(pt));
        var eye = focus - look * dist;
        eye.Y = MathF.Max(eye.Y, 0.4f);
        proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(MathHelper.Lerp(60, 45, t)), GraphicsDevice.Viewport.AspectRatio, 0.1f, 400);
        view = Matrix.CreateLookAt(eye, focus, Vector3.Up);
    }

    // ---------- drawing ----------

    protected override void Draw(GameTime gameTime)
    {
        var gd = GraphicsDevice;
        gd.Clear(Sky);
        gd.RasterizerState = RasterizerState.CullNone;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.SamplerStates[0] = GroundSampler;
        groundFx.View = view; groundFx.Projection = proj;
        groundFx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, ground, 0, 2);

        DrawShadows();
        DrawSprites();
        DrawLines();
        DrawHud();
        if (pendingShot != null && shotDir != null)
        {
            int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
            var data = new Color[w * h];
            gd.GetBackBufferData(data);
            using var tex = new Texture2D(gd, w, h);
            tex.SetData(data);
            using var file = File.Create(Path.Combine(shotDir, pendingShot + ".png"));
            tex.SaveAsPng(file, w, h);
            pendingShot = null;
        }
        base.Draw(gameTime);
    }

    void DrawShadows()
    {
        shadows.N = 0;
        void Blob(Vector2 p, float r)
        {
            const float y = 0.03f;
            VertexPositionTexture V(float dx, float dz, float tu, float tv) => new(new(p.X + dx, y, p.Y + dz), new(tu, tv));
            shadows.Add(V(-r, -r, 0, 0)); shadows.Add(V(r, -r, 1, 0)); shadows.Add(V(-r, r, 0, 1));
            shadows.Add(V(r, -r, 1, 0)); shadows.Add(V(r, r, 1, 1)); shadows.Add(V(-r, r, 0, 1));
        }
        foreach (var u in battle.Units) if (u.Active) Blob(u.Pos, u.Mounted ? 1.1f : 0.55f);
        foreach (var h in battle.LooseHorses) Blob(h.Pos, 1.1f);
        if (shadows.N == 0) return;
        var gd = GraphicsDevice;
        gd.BlendState = BlendState.AlphaBlend;
        gd.DepthStencilState = DepthStencilState.DepthRead;
        gd.SamplerStates[0] = SamplerState.LinearClamp;
        shadowFx.View = view; shadowFx.Projection = proj;
        shadowFx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, shadows.A, 0, shadows.N / 3);
    }

    void DrawSprites()
    {
        var inv = Matrix.Invert(view);
        var right = inv.Right;
        var up = inv.Up;
        float turn = MathF.Min(1, frameDt * 12);

        sprites.N = 0;
        foreach (var (pos, kind, scale) in props)
        {
            var src = art[$"prop{kind}"];
            float w = src.Width * Art.Px * scale, h = src.Height * Art.Px * scale;
            var bl = new Vector3(pos.X, 0, pos.Y) - right * w / 2 - up * h / src.Height; // 1px border sits below ground
            AddQuad(bl, bl + right * w, bl + up * h, bl + right * w + up * h, src);
        }
        puppet.Begin(view);
        foreach (var u in battle.Units)
        {
            // Ease the drawn facing so turning is a smooth sweep.
            u.ViewFacing += MathHelper.WrapAngle(u.Facing - u.ViewFacing) * turn;
            u.ViewHeading += MathHelper.WrapAngle(u.Heading - u.ViewHeading) * turn;
            puppet.Draw(u);
        }
        foreach (var h in battle.LooseHorses) puppet.DrawLooseHorse(h);
        foreach (var (pos, heading, team) in battle.DeadHorses) puppet.DrawDeadHorse(pos, heading, team);

        var gd = GraphicsDevice;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.SamplerStates[0] = SamplerState.PointClamp;
        spriteFx.View = view; spriteFx.Projection = proj;
        spriteFx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, sprites.A, 0, sprites.N / 3);
    }

    void AddQuad(Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Rectangle src)
    {
        float aw = art.Atlas.Width, ah = art.Atlas.Height, e = 0.01f;
        float u0 = (src.Left + e) / aw, u1 = (src.Right - e) / aw, v0 = (src.Top + e) / ah, v1 = (src.Bottom - e) / ah;
        var c = Color.White;
        sprites.Add(new(tl, c, new(u0, v0))); sprites.Add(new(tr, c, new(u1, v0))); sprites.Add(new(bl, c, new(u0, v1)));
        sprites.Add(new(tr, c, new(u1, v0))); sprites.Add(new(br, c, new(u1, v1))); sprites.Add(new(bl, c, new(u0, v1)));
    }

    void Line(Vector3 a, Vector3 b, Color c) { lines.Add(new(a, c)); lines.Add(new(b, c)); }
    static Vector3 G(Vector2 p, float y = 0.06f) => new(p.X, y, p.Y);

    void Ring(Vector2 p, float r, Color c)
    {
        for (int i = 0; i < 12; i++)
        {
            float a0 = i * MathF.Tau / 12, a1 = (i + 1) * MathF.Tau / 12;
            Line(G(p + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r), G(p + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r), c);
        }
    }

    void DrawLines()
    {
        lines.N = 0;
        foreach (var a in battle.Arrows)
        {
            var dir = a.Vel.LengthSquared() > 0 ? Vector3.Normalize(a.Vel) : Vector3.Down;
            if (a.Stuck >= 0) Line(a.Pos, a.Pos - dir * 0.5f, ArrowColor);
            else Line(a.Pos, a.Pos - dir * 0.8f, ArrowColor);
        }
        foreach (var s in selected)
            foreach (var u in s.Units)
                if (u.Active) Ring(u.Pos, u.Mounted ? 1.05f : 0.45f, Sel);
        if (command)
        {
            if (battle.Player.Active) Ring(battle.Player.Pos, 0.8f, Gold);
            if (battle.EnemyCaptain.Active) Ring(battle.EnemyCaptain.Pos, 0.8f, Foe);
            foreach (var s in battle.SquadsOf(0))
            {
                if (s.Alive == 0 || s.Order != Order.Hold) continue;
                // Front-rank marker with a facing tick.
                var side = new Vector2(-s.Front.Y, s.Front.X) * (Math.Min(s.Columns, s.Alive) - 1) * s.Spacing / 2;
                var c = selected.Contains(s) ? Sel : Friendly;
                Line(G(s.Anchor - side + s.Front), G(s.Anchor + side + s.Front), c);
                Line(G(s.Anchor + s.Front), G(s.Anchor + s.Front * 2.5f), c);
            }
            foreach (var s in battle.SquadsOf(0))
                if (s.Alive > 0 && s.Order == Order.Charge && s.Target is { Alive: > 0 } tgt)
                    Line(G(s.Centroid(), 0.12f), G(tgt.Centroid(), 0.12f), Foe); // attack order
            if (PickSquad(mouseGround, 1) is Squad hover)
                foreach (var u in hover.Units) if (u.Active && !u.Routing) Ring(u.Pos, u.Mounted ? 1.1f : 0.5f, Foe);
            if (dragStart is Vector2 d) Line(G(d, 0.1f), G(mouseGround, 0.1f), Color.White);
            Ring(mouseGround, 0.4f, Color.White);
        }
        if (lines.N == 0) return;
        var gd = GraphicsDevice;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        lineFx.View = view; lineFx.Projection = proj;
        lineFx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.LineList, lines.A, 0, lines.N / 2);
    }

    void Text(string s, float x, float y, Color c, int scale = 2) => font.Draw(sb, s, new Vector2(x, y), c, scale);
    void TextRight(string s, float y, Color c) => Text(s, GraphicsDevice.Viewport.Width - PixelFont.Width(s, 2) - 12, y, c);
    void TextCenter(string s, float y, Color c, int scale = 2) => Text(s, (GraphicsDevice.Viewport.Width - PixelFont.Width(s, scale)) / 2f, y, c, scale);

    static int AvgMorale(IEnumerable<Unit> us)
    {
        var active = us.Where(u => u.Active).ToList();
        return active.Count == 0 ? 0 : (int)active.Average(u => u.Morale);
    }

    void Bar(int x, int y, int w, int h, float frac, Color c)
    {
        sb.Draw(art.Pixel, new Rectangle(x - 2, y - 2, w + 4, h + 4), new Color(0, 0, 0, 160));
        sb.Draw(art.Pixel, new Rectangle(x, y, (int)(w * Math.Clamp(frac, 0, 1)), h), c);
    }

    /// Four marks around the crosshair: gold = your swing/block direction, red = a blow coming at you (block that side).
    void DirectionMarks(int cx, int cy)
    {
        var incoming = new bool[4];
        foreach (var u in battle.Units)
            if (u.Active && u.Team != 0 && u.Target == battle.Player && u.Action == UnitAction.Windup
                && Vector2.DistanceSquared(u.Pos, battle.Player.Pos) < 16)
                incoming[(int)Dirs.Mirror(u.Swing)] = true;
        for (int i = 0; i < 4; i++)
        {
            var d = (AttackDir)i;
            var r = d switch
            {
                AttackDir.Overhead => new Rectangle(cx - 8, cy - 22, 18, 4),
                AttackDir.Thrust => new Rectangle(cx - 8, cy + 20, 18, 4),
                AttackDir.Left => new Rectangle(cx - 24, cy - 8, 4, 18),
                _ => new Rectangle(cx + 22, cy - 8, 4, 18),
            };
            var c = incoming[i] ? Foe : d == aimDir ? Gold : Color.White * 0.3f;
            sb.Draw(art.Pixel, r, c);
        }
    }

    void DrawHud()
    {
        var vp = GraphicsDevice.Viewport;
        int H = vp.Height;
        sb.Begin(samplerState: SamplerState.PointClamp);

        // Own squads.
        var mine = battle.SquadsOf(0).ToList();
        Text(selected.Count == 0 ? "ORDERS TO: ALL TROOPS"
            : "ORDERS TO: " + string.Join(" ", mine.Select((s, i) => selected.Contains(s) ? $"{i + 1}" : "").Where(x => x != "")), 12, 12, Color.White);
        for (int i = 0; i < mine.Count; i++)
        {
            var s = mine[i];
            int active = s.Units.Count(u => u.Active), routing = s.Units.Count(u => u.Active && u.Routing);
            string order = s.Order switch { Order.Follow => "FOLLOW", Order.Charge => s.Target != null ? "ATTACK" : "CHARGE", _ => "HOLD" }
                + (s.Loose ? " LOOSE" : "");
            int morale = AvgMorale(s.Units);
            string state = active == 0 ? "DESTROYED" : routing * 2 > active ? "ROUTING" : morale < 30 ? "WAVERING" : s.Pressure > 1.5f ? "FLANKED" : "STEADY";
            var text = $"[{i + 1}] {s.Name,-12} {active,2}/{s.StartCount,-2} {order,-11} {state,-9} {morale,3}";
            if (routing > 0 && state != "ROUTING") text += $" {routing} FLEEING";
            Text(text, 12, 36 + i * 20, selected.Contains(s) ? Sel : routing > 0 ? Foe : Color.White);
        }

        // Enemy summary.
        var foes = battle.Units.Where(u => u.Team == 1).ToList();
        TextRight($"ENEMY {foes.Count(u => u.Active && !u.Routing)}/{foes.Count}", 12, Foe);
        TextRight($"ENEMY MORALE {AvgMorale(foes)}", 32, Foe);
        TextRight(battle.EnemyCaptain.Active ? "ENEMY CAPTAIN STANDS" : "ENEMY CAPTAIN SLAIN", 52, Foe);

        // Player.
        var p = battle.Player;
        int by = H - 150;
        Bar(12, by, 240, 14, p.Hp / p.Type.MaxHp, new Color(200, 50, 50));
        Bar(12, by + 22, 240, 8, p.Stamina / 100, new Color(80, 180, 90));
        if (p.Charging) Bar(12, by + 38, 240, 6, p.Charge, Gold);
        if (p.Mounted)
        {
            Bar(12, by - 18, 240, 8, p.HorseHp / p.Horse!.Hp, new Color(150, 100, 60));
            Text(p.Couched ? "LANCE COUCHED" : $"{p.Horse.Name.ToUpperInvariant()} {MathF.Abs(p.Speed):0.0} M/S", 268, by - 22,
                p.Couched ? Gold : Color.White);
        }
        Text(battle.RallyCooldown > 0 ? $"RALLY {battle.RallyCooldown:0}S" : "RALLY READY", 268, by, Color.White);
        Text($"KILLS {battle.PlayerKills}", 268, by + 20, Color.White);

        if (command)
            TextCenter(p.Active ? "COMMAND VIEW - TIME X0.25 - NO LEADERSHIP AURA" : "YOU HAVE FALLEN - COMMANDING FROM AFAR", 12, Sel);
        else
        {
            sb.Draw(art.Pixel, new Rectangle(vp.Width / 2 - 6, H / 2, 12, 2), Color.White);
            sb.Draw(art.Pixel, new Rectangle(vp.Width / 2, H / 2 - 6, 2, 12), Color.White);
            if (p.Active) DirectionMarks(vp.Width / 2, H / 2);
            var (hit, at) = battle.PlayerHit;
            if (battle.Time - at < 1.2f) TextCenter(hit, H / 2f + 34, hit.StartsWith("PARRY") ? Gold : Color.White);
        }

        // Squad labels in command view.
        if (command)
            foreach (var s in battle.Squads)
            {
                if (s.Alive == 0) continue;
                var c = s.Centroid();
                var sp = vp.Project(new Vector3(c.X, 2.6f, c.Y), proj, view, Matrix.Identity);
                if (sp.Z < 0 || sp.Z > 1) continue;
                string label = s.Team == 0 ? $"{mine.IndexOf(s) + 1} {s.Name}" : s.Name;
                Text(label, sp.X - PixelFont.Width(label, 2) / 2f, sp.Y, s.Team == 1 ? Foe : selected.Contains(s) ? Sel : Friendly);
            }

        // Event feed.
        int shown = 0;
        for (int i = battle.Events.Count - 1; i >= 0 && shown < 4; i--, shown++)
            TextCenter(battle.Events[i].Text, H - 110 - shown * 20, Color.White);

        // Help.
        var dim = new Color(220, 220, 220);
        if (command)
        {
            Text("LMB SELECT (SHIFT ADD)  RMB MOVE  RMB DRAG LINE  WASD PAN  Q/E/MMB DRAG ORBIT  WHEEL ZOOM", 12, H - 44, dim);
            Text("RMB ON ENEMY ATTACK  1-4 SELECT  0 ALL  F FOLLOW  G HOLD  C CHARGE  V SPACING  TAB BACK TO BODY", 12, H - 24, dim);
        }
        else
        {
            Text(p.Mounted
                ? "W/S REIN  A/D TURN  SHIFT GALLOP  Q COUCH LANCE  H DISMOUNT  MOUSE MOVE = SWING DIR  LMB ATTACK  RMB BLOCK"
                : "WASD MOVE  SHIFT SPRINT  H MOUNT  MOUSE MOVE = SWING DIR  LMB HOLD+RELEASE ATTACK  RMB BLOCK (MATCH = PARRY)", 12, H - 44, dim);
            Text("R RALLY  TAB COMMAND  1-4 SELECT  0 ALL  F FOLLOW  G FORM UP HERE  C CHARGE  V SPACING  ESC QUIT", 12, H - 24, dim);
        }

        if (battle.Winner >= 0)
        {
            TextCenter(battle.Winner == 0 ? "VICTORY" : "DEFEAT", H / 2f - 80, battle.Winner == 0 ? Gold : Foe, 8);
            TextCenter("ENTER: NEW BATTLE   ESC: QUIT", H / 2f, Color.White);
        }
        sb.End();
    }
}

/// Growable vertex array reused every frame.
sealed class Verts<T> where T : struct
{
    public T[] A = new T[1024];
    public int N;
    public void Add(T v)
    {
        if (N == A.Length) Array.Resize(ref A, N * 2);
        A[N++] = v;
    }
}
