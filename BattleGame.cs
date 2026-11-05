using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// Rendering, camera and input around a headless Battle. Two views with an eased camera blend between them:
/// field (third person, you fight) and command (free orbit camera, time slowed, you direct squads).
/// The world (terrain, foliage, sky) is 3D geometry from World; units are 2D sprite puppets.
public sealed class BattleGame : Game
{
    const float CommandTimeScale = 0.25f, BlendTime = 0.45f, DoubleClick = 0.35f;
    static readonly Color Gold = Hud.Gold, Sel = new(255, 236, 90), Friendly = Hud.Friendly, Foe = Hud.Foe, ArrowColor = new(70, 50, 30);

    SpriteBatch sb = null!;
    Art art = null!;
    PixelFont font = null!;
    Puppet puppet = null!;
    Hud hud = null!;
    World world = null!;
    readonly Controls ctl = Controls.Load();
    AlphaTestEffect spriteFx = null!;
    Shading? shading;                          // GLSL pipeline (null if it could not be created)
    enum Mode { Title, Map, Battle }
    Mode mode = Mode.Title;
    Campaign? campaign;
    MapScreen map = null!;
    Ui titleUi = null!;
    BattleSetup? campaignBattle;               // the battle in progress came from the campaign map
    bool deploying, titleClick;
    readonly string? campaignShots = Environment.GetEnvironmentVariable("IRONFIELD_CAMPAIGN_SHOTS") is { Length: > 0 } cs ? cs : null;
    int cStage;
    Quality quality = Quality.High;
    Vector3 focusPoint;                        // what the camera looks at (the shadow map is centred here)
    BasicEffect shadowFx = null!, lineFx = null!;
    Battle battle = null!;
    readonly HashSet<Squad> selected = new();
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPositionTexture> shadows = new();
    readonly Verts<VertexPositionColor> lines = new();

    bool command, paused, menuOpen, helpOpen, wheelOpen, skipMouse = true;
    float yaw, pitch, fieldDist = 5.5f;       // field camera: third person behind the player
    float camYaw, camPitch, zoom = 45;        // command camera: free orbit around camFocus
    float blend, frameDt, realTime, lastCardClick;
    Squad? lastCardSquad;
    Vector2 camFocus, mouseGround, aimAcc, wheelVec;
    AttackDir aimDir = AttackDir.Overhead;    // picked by the direction the mouse last moved
    Vector2? dragStart;                       // RMB drag on the ground (command view)
    Point? boxStart;                          // LMB drag on the screen (command view)
    KeyboardState kb, pkb;
    MouseState ms, pms;
    Matrix view, proj;
    Vector3 eye;

    // Debug: IRONFIELD_SHOTS=/dir stages a few camera shots, saves them as PNGs and quits.
    readonly string? shotDir = Environment.GetEnvironmentVariable("IRONFIELD_SHOTS") is { Length: > 0 } sd ? sd : null;
    int shotIndex, shotFrames;
    string? pendingShot;
    bool shotWheel, hudHidden;

    public BattleGame()
    {
        new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720,
            GraphicsProfile = GraphicsProfile.HiDef,
        };
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
        hud = new Hud(sb, art, font);
        spriteFx = new AlphaTestEffect(GraphicsDevice)
        {
            Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128,
            FogEnabled = true, FogColor = World.Horizon.ToVector3(), FogStart = World.FogStart, FogEnd = World.FogEnd,
        };
        shadowFx = new BasicEffect(GraphicsDevice) { TextureEnabled = true, Texture = art.Shadow };
        lineFx = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        if (Environment.GetEnvironmentVariable("IRONFIELD_QUALITY") is { Length: > 0 } q && Enum.TryParse<Quality>(q, true, out var qv)) quality = qv;
        try { shading = new Shading(GraphicsDevice); }
        catch (Exception e) { Console.WriteLine("shaders unavailable: " + e.Message); quality = Quality.Low; }
        NewBattle();
        map = new MapScreen(GraphicsDevice, sb, art, font, shading, ctl) { OnBattle = StartCampaignBattle, OnTitle = () => mode = Mode.Title };
        titleUi = new Ui(sb, art, font);
        if (shotDir != null) mode = Mode.Battle;
        if (campaignShots != null) StartCampaign(3);
    }

    // ---------- title and campaign ----------

    void StartCampaign(int seed)
    {
        campaign = Campaign.New(seed);
        map.Open(campaign);
        mode = Mode.Map;
    }

    void ContinueCampaign()
    {
        string? latest = new[] { "autosave", "quicksave" }.Where(CampaignState.Exists)
            .OrderByDescending(n => File.GetLastWriteTimeUtc(Path.Combine(CampaignState.SaveDir, n + ".json"))).FirstOrDefault();
        if (latest == null || CampaignState.Load(latest) is not CampaignState s) return;
        campaign = new Campaign(s);
        map.Open(campaign);
        mode = Mode.Map;
    }

    void StartCampaignBattle(BattleSetup s)
    {
        campaign!.S.Save("autosave");
        campaignBattle = s;
        battle = new Battle(s.Seed, s.Player, s.Enemy, s.Options);
        SetWorld();
        selected.Clear();
        yaw = MathHelper.PiOver2;
        pitch = 0.3f;
        paused = menuOpen = helpOpen = wheelOpen = false;
        deploying = true;
        mode = Mode.Battle;
        SetCommand(true);
        blend = 1;
        battle.Log($"Battle against {s.EnemyName}: {s.Options.Ground?.Name.ToLowerInvariant() ?? "open"} ground.");
        if (!s.Options.Scouted) battle.Log("No scouts: you cannot see their ranks until the fight starts.");
    }

    void FinishCampaignBattle()
    {
        campaign!.FinishBattle(battle.Result(), campaignBattle!);
        campaignBattle = null;
        deploying = false;
        mode = Mode.Map;
        map.ShowResults();
    }

    void DrawTitle()
    {
        var gd = GraphicsDevice;
        gd.Clear(new Color(18, 20, 26));
        titleUi.Begin(gd.Viewport, ms.Position, titleClick);
        titleClick = false;
        var u = titleUi;
        u.Rect(new Rectangle(0, u.H / 2 - u.U(150), u.W, u.U(4)), Style.Accent * 0.5f);
        u.TextC("IRONFIELD", u.W / 2f, u.H / 2f - u.U(130), Style.Accent, 8);
        u.TextC("A CAPTAIN'S WAR", u.W / 2f, u.H / 2f - u.U(60), Style.Dim, 2);
        int y = u.H / 2 - u.U(20), w = u.U(280);
        Rectangle B() { var r = new Rectangle((u.W - w) / 2, y, w, u.U(34)); y += u.U(42); return r; }
        if (u.Button(B(), "NEW CAMPAIGN")) StartCampaign(Environment.TickCount & 0xFFFF);
        if (u.Button(B(), "CONTINUE", CampaignState.Exists("autosave") || CampaignState.Exists("quicksave"))) ContinueCampaign();
        if (u.Button(B(), "QUICK BATTLE")) { NewBattle(); mode = Mode.Battle; deploying = true; SetCommand(true); blend = 1; }
        if (u.Button(B(), "QUIT")) Exit();
        u.TextC("F2 GRAPHICS QUALITY", u.W / 2f, u.H - u.U(30), Style.Dim * 0.7f, 1);
        u.End();
    }

    /// Debug: IRONFIELD_CAMPAIGN_SHOTS=/dir walks through the campaign screens, saving a PNG of each, then quits.
    void CampaignShots()
    {
        var c = campaign!;
        if (shotFrames < 20 || pendingShot != null) return;
        void Shot(string name) { pendingShot = name; shotFrames = 0; cStage++; }
        switch (cStage)
        {
            case 0: map.Stage("map"); Shot("campaign-map"); break;
            case 1: map.Stage("close"); Shot("campaign-close"); break;
            case 2: c.Player.Companies[0].Xp = 70; c.Captain.Renown = 120; map.Stage("party"); Shot("campaign-party"); break;
            case 3: map.Stage("upgrade"); Shot("campaign-upgrade"); break;
            case 4: map.Stage("settlement"); Shot("campaign-town"); break;
            case 5:
            {
                var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
                bandit.Pos = c.Player.Pos + new Vector2(2, 0);
                c.Encounter = bandit;
                map.Stage("encounter"); Shot("campaign-encounter"); break;
            }
            case 6: c.AutoResolveEncounter(); map.Stage("results"); Shot("campaign-results"); break;
            case 7:
            {
                map.Stage("map");
                var lord = c.S.Parties.First(p => p.Kind == PartyKind.Lord && p.Faction == 1);
                lord.Pos = c.Player.Pos + new Vector2(2, 0);
                c.Encounter = lord;
                StartCampaignBattle(c.PrepareBattle(lord));
                cStage++; shotFrames = 0; break;
            }
            case 8: Shot("campaign-deploy"); break;
            case 9: deploying = false; cStage++; break;
            case 10 when battle.Time > 7: SetCommand(true); blend = 1; camFocus = battle.Centroid(0); camPitch = 0.8f; zoom = 50; Shot("campaign-battle"); break;
            case 11: Exit(); break;
        }
    }

    void NewBattle()
    {
        // Stand-in for the campaign map: both armies arrive with companies upgraded to some level (1-4).
        int seed = shotDir != null ? 7 : Environment.TickCount;
        var rng = new Random(seed);
        var levels = (Environment.GetEnvironmentVariable("IRONFIELD_LEVELS") ?? (shotDir != null ? "4,3" : "")).Split(',');
        int mine = int.TryParse(levels[0], out var l0) ? l0 : rng.Next(1, 5);
        int theirs = levels.Length > 1 && int.TryParse(levels[1], out var l1) ? l1 : Math.Clamp(mine + rng.Next(-1, 2), 1, 4);
        battle = new Battle(seed, Armies.Random(rng, Math.Clamp(mine, 1, 4), 0), Armies.Random(rng, Math.Clamp(theirs, 1, 4), 1));
        SetWorld();
        battle.Log($"Your company (level {mine}) faces a level {theirs} host.");
        foreach (var p in ctl.Problems) battle.Log(p);
        selected.Clear();
        yaw = MathHelper.PiOver2;
        pitch = 0.3f;
        blend = 0;
        paused = menuOpen = helpOpen = wheelOpen = false;
        SetCommand(false);
    }

    void SetWorld()
    {
        world?.Dispose();
        world = new World(GraphicsDevice, battle.Terrain);
    }

    void SetCommand(bool on)
    {
        command = on;
        dragStart = null;
        boxStart = null;
        if (on) { camFocus = battle.Player.Pos; camYaw = yaw; camPitch = 0.95f; }
        else skipMouse = true;
    }

    bool Pressed(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
    bool Overlay => menuOpen || helpOpen || battle.Winner >= 0;
    bool LmbPressed => ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released;
    bool LmbReleased => ms.LeftButton == ButtonState.Released && pms.LeftButton == ButtonState.Pressed;

    protected override void Update(GameTime gameTime)
    {
        pkb = kb; pms = ms;
        kb = Keyboard.GetState(); ms = Mouse.GetState();
        ctl.Update(kb, ms);
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 30f);
        realTime += dt;
        if (ctl.Pressed(Act.Graphics) && mode != Mode.Battle) quality = shading == null ? Quality.Low : (Quality)(((int)quality + 1) % 3);
        if (mode == Mode.Title)
        {
            IsMouseVisible = true;
            if (LmbPressed) titleClick = true;
            base.Update(gameTime);
            return;
        }
        if (mode == Mode.Map)
        {
            IsMouseVisible = true;
            map.Update(dt, IsActive);
            if (campaignShots != null) { shotFrames++; CampaignShots(); }
            base.Update(gameTime);
            return;
        }

        if (ctl.Pressed(Act.Menu)) { if (helpOpen) helpOpen = false; else if (battle.Winner < 0) menuOpen = !menuOpen; skipMouse = true; }
        if (ctl.Pressed(Act.Help)) { helpOpen = !helpOpen; skipMouse = true; }
        SyncHud();

        bool alive = battle.Player.Active;
        if (Overlay)
        {
            battle.Input = new PlayerInput { Dir = aimDir };
            wheelOpen = false;
            if (battle.Winner >= 0 && Pressed(Keys.Enter)) { if (campaignBattle != null) FinishCampaignBattle(); else NewBattle(); }
            else if (LmbPressed && hud.MenuAt(ms.Position) is MenuItem m) MenuChoice(m);
        }
        else
        {
            if ((!alive || deploying) && !command) SetCommand(true);
            else if (alive && !deploying && ctl.Pressed(Act.CommandView)) SetCommand(!command);
            if (deploying && (ctl.Pressed(Act.Pause) || (LmbPressed && hud.StartAt(ms.Position))))
            {
                deploying = false;
                battle.Log("The battle begins!");
            }
            else if (ctl.Pressed(Act.Pause)) paused = !paused;
            if (ctl.Pressed(Act.Graphics))
            {
                quality = shading == null ? Quality.Low : (Quality)(((int)quality + 1) % 3);
                battle.Log("Graphics: " + quality switch { Quality.High => "high (shadows, post)", Quality.Medium => "medium (no shadows)", _ => "low (basic)" });
            }
            OrderKeys();
            if (command) CommandControls(dt); else FieldControls();
        }
        IsMouseVisible = command || Overlay;
        battle.Commanding = command;

        float scale = paused || menuOpen || helpOpen || deploying ? 0 : command && alive ? CommandTimeScale : 1;
        if (scale > 0) battle.Update(dt * scale);
        blend = Math.Clamp(blend + (command ? dt : -dt) / BlendTime, 0, 1);
        frameDt = dt;
        if (shotDir != null) { shotFrames++; ShotScript(); }
        if (campaignShots != null) { shotFrames++; CampaignShots(); }
        UpdateCamera();
        base.Update(gameTime);
    }

    /// Hand the HUD everything it shows, and lay it out (also used for clicks).
    void SyncHud()
    {
        hud.B = battle; hud.Selected = selected; hud.Ctl = ctl; hud.Minimap = world.Minimap;
        hud.Command = command; hud.Paused = paused; hud.MenuOpen = menuOpen; hud.HelpOpen = helpOpen;
        hud.ShowResult = battle.Winner >= 0 && !helpOpen;
        hud.InCampaign = campaignBattle != null;
        hud.Deploying = deploying;
        hud.WheelOpen = wheelOpen; hud.WheelSector = WheelSector() ?? -1;
        hud.Aim = aimDir; hud.View = view; hud.Proj = proj; hud.Mouse = ms.Position;
        hud.Box = boxStart is Point b && command ? BoxOf(b, ms.Position) : null;
        if (hud.Box is { Width: < 6, Height: < 6 }) hud.Box = null;
        hud.Layout(GraphicsDevice.Viewport);
    }

    void MenuChoice(MenuItem m)
    {
        switch (m)
        {
            case MenuItem.Resume: menuOpen = false; skipMouse = true; break;
            case MenuItem.Controls: helpOpen = true; break;
            case MenuItem.NewBattle: NewBattle(); break;
            case MenuItem.Quit: Exit(); break;
            case MenuItem.Withdraw: battle.Withdraw(); menuOpen = false; break;
            case MenuItem.Continue: FinishCampaignBattle(); break;
            case MenuItem.Title: menuOpen = false; mode = Mode.Title; break;
        }
    }

    void ShotScript()
    {
        var b = battle;
        var cav = b.SquadsOf(1).FirstOrDefault(s => s.Cavalry) ?? b.SquadsOf(1).First();
        switch (shotIndex)
        {
            case 0 when b.Time > 0.5f:
                b.Player.Pos = new(17, -37); b.Player.Heading = 0.35f; yaw = 0.35f; pitch = 0.12f; // beside our own lancers
                pendingShot = "field-horses"; shotIndex++; break;
            case 1 when b.Time > 2f:
                b.Player.Pos = new(0, -34); yaw = MathHelper.PiOver2; pitch = 0.2f; aimDir = AttackDir.Right; // looking across the field
                pendingShot = "field-view"; shotIndex++; break;
            case 2 when b.Time > 3f:
                shotWheel = wheelOpen = true; wheelVec = new Vector2(60, -60);
                pendingShot = "order-wheel"; shotIndex++; break;
            case 3 when b.Time > 13f:
                shotWheel = wheelOpen = false;
                SetCommand(true); blend = 1; camFocus = cav.Centroid(); camYaw = 2.4f; camPitch = 0.55f; zoom = 26;
                selected.Add(b.SquadsOf(0).First());
                pendingShot = "command-cav-charge"; shotIndex++; break;
            case 4 when b.Time > 20f:
                camFocus = b.Centroid(0); camYaw = 1.9f; camPitch = 0.9f; zoom = 60;
                pendingShot = "command-overview"; shotIndex++; break;
            case 5 when b.Time > 20.5f:
                camFocus = new Vector2(0, -60); camYaw = MathHelper.PiOver2; camPitch = 0.32f; zoom = 90;
                pendingShot = "landscape"; shotIndex++; break;
            case 6 when b.Time > 21f:
                helpOpen = true;
                pendingShot = "controls"; shotIndex++; break;
            case 7 when pendingShot == null:
                helpOpen = false;
                Gallery();
                shotFrames = 0; shotIndex++; break;
            case 8 when shotFrames > 20:
                pendingShot = "gallery-front"; shotIndex++; break;
            case >= 9 and <= 11 when pendingShot == null:
                // Close-ups: foot, foot, horse.
                zoom = 7.5f; camPitch = 0.08f;
                camFocus = new Vector2(new[] { -5.6f, -16.8f, -31f }[shotIndex - 9], 0.6f);
                pendingShot = $"gallery-close{shotIndex - 8}"; shotIndex++; break;
            case 12 when pendingShot == null:
                foreach (var u in b.Units.Where(u => !u.IsLeader)) { u.Team = 1; u.Facing = u.Heading = 0; }
                zoom = 21; camPitch = 0.1f; camFocus = new Vector2(-19, 0);
                shotFrames = 0; shotIndex++; break;
            case 13 when shotFrames > 20:
                pendingShot = "gallery-side"; shotIndex++; break;
            case 14 when pendingShot == null:
                zoom = 7.5f; camPitch = 0.08f; camFocus = new Vector2(-31f, 0.6f);
                pendingShot = "gallery-side-close"; shotIndex++; break;
            case 15 when pendingShot == null: Exit(); break;
        }
    }

    /// Debug lineup of every troop, left to right along each upgrade line, facing the camera.
    void Gallery()
    {
        battle = Battle.Empty(1);
        SetWorld();
        var order = new[] { "levies", "shieldmen", "men_at_arms", "ironguard", "spearrunners", "pikemen", "bowmen", "longbowmen",
            "outriders", "lancers", "knights", "captain" };
        float x = 0;
        foreach (var id in order)
        {
            var t = Troops.Get(id);
            x += t.Mount != null ? 2.2f : 1.4f;
            var u = battle.AddSquad(0, t, 1, new Vector2(-x, 0), 1, -Vector2.UnitY).Units[0];
            u.ViewFacing = u.ViewHeading = u.Facing;
            x += t.Mount != null ? 2.2f : 1.4f;
        }
        paused = hudHidden = true;
        SetCommand(true);
        blend = 1;
        camFocus = new Vector2(-x / 2, 0);
        camYaw = MathHelper.PiOver2;
        camPitch = 0.1f;
        zoom = 21;
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

    void Command(Cmd c)
    {
        var enemy = battle.Centroid(1);
        switch (c)
        {
            case Cmd.Charge: Issue("charge!", s => { s.Order = Order.Charge; s.Target = null; }); break;
            case Cmd.Follow: Issue("follow me!", s => { s.Order = Order.Follow; s.Target = null; }); break;
            case Cmd.Hold: Issue("hold position!", s => s.HoldInPlace()); break;
            case Cmd.Advance: Issue("advance!", s => s.Shift(enemy, 15)); break;
            case Cmd.FallBack: Issue("fall back!", s => s.Shift(enemy, -20)); break;
            case Cmd.Spacing: Issue("change spacing", s => s.Loose = !s.Loose); break;
            case Cmd.FireAtWill:
            {
                var archers = Targets().Where(s => s.Units.Any(u => u.Type.Ammo > 0)).ToList();
                if (archers.Count == 0) { battle.Log("No archers selected."); break; }
                bool fire = !archers[0].FireAtWill;
                foreach (var s in archers) s.FireAtWill = fire;
                battle.Log(fire ? "Archers: fire at will!" : "Archers: hold your arrows!");
                break;
            }
            case Cmd.FormHere:
            {
                var p = battle.Player;
                if (!p.Active) break;
                var center = p.Pos + p.Dir * 3;
                LineAt(center, center + p.Dir * 10);
                battle.Log("Form up on me!");
                break;
            }
        }
    }

    void OrderKeys()
    {
        var mine = battle.SquadsOf(0).ToList();
        for (int i = 0; i < mine.Count && i < 9; i++)
            if (Pressed(Keys.D1 + i) && !selected.Remove(mine[i])) selected.Add(mine[i]);
        if (ctl.Pressed(Act.SelectAll)) selected.Clear();
        foreach (var (cmd, (_, act)) in Hud.Orders)
            if (ctl.Pressed(act)) Command(cmd);
    }

    /// Line of the selected squads' combined width, centred on `center`, facing `toward`.
    void LineAt(Vector2 center, Vector2 toward)
    {
        var (a, b) = LineEnds(center, toward);
        Distribute(a, b, toward);
    }

    (Vector2, Vector2) LineEnds(Vector2 center, Vector2 toward)
    {
        float w = Targets().Sum(s => Math.Min(s.Columns, s.Alive) * s.Spacing);
        var to = toward - center;
        if (to.LengthSquared() < 1e-3f) to = Vector2.UnitY;
        to.Normalize();
        var side = new Vector2(-to.Y, to.X);
        return (center - side * w / 2, center + side * w / 2);
    }

    /// Split the segment a-b between target squads, proportional to their size.
    void Distribute(Vector2 a, Vector2 b, Vector2 toward)
    {
        if (deploying) { a.Y = MathF.Min(a.Y, -12); b.Y = MathF.Min(b.Y, -12); } // your third of the field
        foreach (var (s, a1, b1) in Split(a, b).ToList()) s.SetLine(a1, b1, toward);
    }

    IEnumerable<(Squad S, Vector2 A, Vector2 B)> Split(Vector2 a, Vector2 b)
    {
        var list = Targets();
        float total = list.Sum(s => s.Alive), acc = 0;
        foreach (var s in list)
        {
            var a1 = Vector2.Lerp(a, b, acc / total);
            acc += s.Alive;
            yield return (s, a1, Vector2.Lerp(a, b, acc / total));
        }
    }

    /// Where each man would stand if the line a-b were ordered now (drawn while dragging).
    IEnumerable<Vector2> PreviewSlots(Vector2 a, Vector2 b, Vector2 toward)
    {
        foreach (var (s, a1, b1) in Split(a, b))
        {
            var ghost = new Squad(s.Name, s.Team) { Cavalry = s.Cavalry, Loose = s.Loose, Columns = s.Columns, Front = s.Front, Anchor = s.Anchor };
            ghost.SetLine(a1, b1, toward);
            for (int i = 0; i < s.Alive; i++) yield return ghost.Slot(i, s.Alive);
        }
    }

    // ---------- controls ----------

    static Vector2 Forward(float yaw) => new(MathF.Cos(yaw), MathF.Sin(yaw));
    static Vector2 RightOf(Vector2 f) => new(-f.Y, f.X); // screen right when looking along f

    void FieldControls()
    {
        bool wasWheel = wheelOpen;
        wheelOpen = ctl.Down(Act.OrderWheel) || shotWheel;
        if (wheelOpen && !wasWheel) wheelVec = Vector2.Zero;
        if (IsActive)
        {
            // shortcut: mouse look by warping to the centre; switch to SDL relative mode if a platform refuses warps.
            var c = new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            var d = (ms.Position - c).ToVector2();
            if (!skipMouse)
            {
                if (wheelOpen)
                {
                    wheelVec += d;
                    if (wheelVec.Length() > 120) wheelVec = Vector2.Normalize(wheelVec) * 120;
                }
                else
                {
                    float k = 0.0035f * ctl.Sensitivity;
                    yaw += d.X * k;
                    pitch = Math.Clamp(pitch + d.Y * k * (ctl.InvertY ? -1 : 1), -0.35f, 1.1f);
                    // Mount & Blade style directions: the way the mouse moves picks the swing (and the block).
                    aimAcc = aimAcc * 0.8f + d;
                    if (aimAcc.LengthSquared() > 36)
                        aimDir = MathF.Abs(aimAcc.X) > MathF.Abs(aimAcc.Y)
                            ? (aimAcc.X > 0 ? AttackDir.Right : AttackDir.Left)
                            : (aimAcc.Y < 0 ? AttackDir.Overhead : AttackDir.Thrust);
                }
            }
            skipMouse = false;
            Mouse.SetPosition(c.X, c.Y);
        }
        else skipMouse = true;
        if (wasWheel && !wheelOpen && WheelSector() is int sector) Command(Hud.WheelOrder[sector]);
        fieldDist = Math.Clamp(fieldDist - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.004f, 2.5f, 12);

        var f = Forward(yaw);
        bool mounted = battle.Player.Mounted;
        var move = mounted ? Vector2.Zero : f * ctl.Axis(Act.Forward, Act.Back) + RightOf(f) * ctl.Axis(Act.Right, Act.Left);
        if (move != Vector2.Zero) move.Normalize();
        battle.Input = new PlayerInput
        {
            Move = move,
            Throttle = mounted ? ctl.Axis(Act.Forward, Act.Back) : 0, // on horseback forward/back is the rein, left/right turns the horse
            Steer = mounted ? ctl.Axis(Act.Right, Act.Left) : 0,
            Dir = aimDir,
            Couch = ctl.Pressed(Act.Couch),
            Mount = ctl.Pressed(Act.Interact),
            Facing = yaw, // the player looks (and strikes) where the camera looks
            Sprint = ctl.Down(Act.Sprint),
            Attack = IsActive && !wheelOpen && ctl.Down(Act.Attack),
            Block = IsActive && !wheelOpen && ctl.Down(Act.Block),
            Rally = ctl.Pressed(Act.Rally),
        };
    }

    int? WheelSector()
    {
        if (!wheelOpen || wheelVec.Length() < 30) return null;
        float a = MathF.Atan2(wheelVec.Y, wheelVec.X) + MathHelper.PiOver2; // 0 = up, clockwise
        int n = Hud.WheelOrder.Length;
        return ((int)MathF.Round(a / MathHelper.TwoPi * n) % n + n) % n;
    }

    void CommandControls(float dt)
    {
        wheelOpen = false;
        var vp = GraphicsDevice.Viewport;
        var f = Forward(camYaw);
        var pan = f * ctl.Axis(Act.Forward, Act.Back) + RightOf(f) * ctl.Axis(Act.Right, Act.Left);
        if (IsActive && vp.Bounds.Contains(ms.Position))
        {
            // Edge scrolling.
            const int edge = 6;
            if (ms.X < edge) pan -= RightOf(f); else if (ms.X > vp.Width - edge) pan += RightOf(f);
            if (ms.Y < edge) pan += f; else if (ms.Y > vp.Height - edge) pan -= f;
        }
        camFocus += pan * zoom * 0.9f * dt;
        if (camFocus.Length() > Terrain.Half - 20) camFocus = Vector2.Normalize(camFocus) * (Terrain.Half - 20);
        camYaw += ctl.Axis(Act.RotateRight, Act.RotateLeft) * 1.6f * dt;
        if (ms.MiddleButton == ButtonState.Pressed && pms.MiddleButton == ButtonState.Pressed)
        {
            // Middle-drag orbits: horizontal turns, vertical tilts.
            camYaw += (ms.X - pms.X) * 0.006f;
            camPitch = Math.Clamp(camPitch + (ms.Y - pms.Y) * 0.006f, 0.25f, 1.45f);
        }
        zoom = Math.Clamp(zoom - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.03f, 10, 110);
        battle.Input = new PlayerInput { Dir = aimDir };
        if (!IsActive) return;
        mouseGround = GroundUnderMouse();
        bool add = ctl.Down(Act.AddToSelection);

        // Left button: HUD first (buttons, cards, minimap), then the field (click or box select).
        if (LmbPressed)
        {
            if (hud.ButtonAt(ms.Position) is Cmd cmd) Command(cmd);
            else if (hud.CardAt(ms.Position) is Squad card)
            {
                if (card == lastCardSquad && realTime - lastCardClick < DoubleClick) camFocus = card.Centroid(); // double-click: look at it
                lastCardSquad = card;
                lastCardClick = realTime;
                if (add) { if (!selected.Remove(card)) selected.Add(card); }
                else { selected.Clear(); selected.Add(card); }
            }
            else if (hud.MinimapAt(ms.Position) is Vector2 w) camFocus = w;
            else if (!hud.Over(ms.Position)) boxStart = ms.Position;
        }
        if (LmbReleased && boxStart is Point start)
        {
            boxStart = null;
            var box = BoxOf(start, ms.Position);
            if (box.Width < 6 && box.Height < 6)
            {
                var sq = PickSquad(mouseGround, 0);
                if (sq == null) { if (!add) selected.Clear(); }
                else if (add) { if (!selected.Remove(sq)) selected.Add(sq); }
                else { selected.Clear(); selected.Add(sq); }
            }
            else
            {
                if (!add) selected.Clear();
                foreach (var s in battle.SquadsOf(0))
                    if (s.Units.Any(u => u.Active && !u.Routing && OnScreen(u.Pos, out var sp) && box.Contains(sp))) selected.Add(s);
            }
        }

        // Right button: move / draw a line / attack.
        if (ms.RightButton == ButtonState.Pressed && pms.RightButton == ButtonState.Released && !hud.Over(ms.Position)) dragStart = mouseGround;
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

    static Rectangle BoxOf(Point a, Point b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    bool OnScreen(Vector2 p, out Point sp)
    {
        var v = GraphicsDevice.Viewport.Project(new Vector3(p.X, battle.Terrain.Height(p) + 1, p.Y), proj, view, Matrix.Identity);
        sp = new Point((int)v.X, (int)v.Y);
        return v.Z is > 0 and < 1;
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

    /// Ray-march the mouse ray onto the terrain.
    Vector2 GroundUnderMouse()
    {
        var vp = GraphicsDevice.Viewport;
        var near = vp.Unproject(new Vector3(ms.X, ms.Y, 0), proj, view, Matrix.Identity);
        var far = vp.Unproject(new Vector3(ms.X, ms.Y, 1), proj, view, Matrix.Identity);
        var d = Vector3.Normalize(far - near);
        var t = battle.Terrain;
        float Above(Vector3 p) => p.Y - t.Height(new Vector2(p.X, p.Z));
        float s = 0, step = 1;
        for (int i = 0; i < 800 && s < 600; i++)
        {
            var p = near + d * s;
            float h = Above(p);
            if (h > 0) { step = MathF.Max(0.5f, h * 0.5f); s += step; continue; }
            float lo = MathF.Max(0, s - step), hi = s; // bisect the crossing
            for (int k = 0; k < 10; k++) { float m = (lo + hi) / 2; if (Above(near + d * m) > 0) lo = m; else hi = m; }
            var hit = near + d * hi;
            return new Vector2(hit.X, hit.Z);
        }
        return camFocus;
    }

    void UpdateCamera()
    {
        // Both views are an orbit (focus, yaw, pitch, distance); the transition eases every parameter.
        var t = battle.Terrain;
        var p = battle.Player.Pos;
        bool mounted = battle.Player.Mounted;
        var shoulder = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw)) * 0.8f; // over the right shoulder
        var fieldFocus = new Vector3(p.X, t.Height(p) + (mounted ? 2.6f : 1.7f), p.Y) + shoulder;
        var cmdFocus = new Vector3(camFocus.X, t.Height(camFocus), camFocus.Y);
        float k = MathHelper.SmoothStep(0, 1, blend);
        var focus = Vector3.Lerp(fieldFocus, cmdFocus, k);
        float y = yaw + MathHelper.WrapAngle(camYaw - yaw) * k; // shortest way round
        float pt = MathHelper.Lerp(pitch, camPitch, k), dist = MathHelper.Lerp(fieldDist + (mounted ? 1.5f : 0), zoom, k);
        var look = new Vector3(MathF.Cos(y) * MathF.Cos(pt), -MathF.Sin(pt), MathF.Sin(y) * MathF.Cos(pt));
        eye = focus - look * dist;
        focusPoint = focus;
        eye.Y = MathF.Max(eye.Y, t.Height(new Vector2(eye.X, eye.Z)) + 0.5f); // never under the ground
        proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(MathHelper.Lerp(62, 45, k)), GraphicsDevice.Viewport.AspectRatio, 0.2f, 700);
        view = Matrix.CreateLookAt(eye, focus, Vector3.Up);
        hud.CamYaw = y;
        hud.CamFocus = camFocus;
    }

    // ---------- drawing ----------

    protected override void Draw(GameTime gameTime)
    {
        var gd = GraphicsDevice;
        if (mode != Mode.Battle)
        {
            if (mode == Mode.Title) DrawTitle();
            else
            {
                try { map.Draw(quality); }
                catch (Exception e) when (quality != Quality.Low)
                {
                    Console.WriteLine("shader pipeline failed on the map, falling back: " + e);
                    gd.SetRenderTarget(null);
                    quality = Quality.Low;
                }
            }
            CaptureShot();
            base.Draw(gameTime);
            return;
        }
        EaseFacings();
        bool shaded = quality != Quality.Low && shading != null;
        if (shaded)
        {
            try { DrawShaded(shading!); }
            catch (Exception e)
            {
                // A driver that rejects the GLSL: fall back to the fixed-function path for good.
                Console.WriteLine("shader pipeline failed, falling back: " + e);
                gd.SetRenderTarget(null);
                quality = Quality.Low;
                battle.Log("Shaders unavailable: basic graphics.");
                shaded = false;
            }
        }
        if (!shaded)
        {
            gd.Clear(World.Horizon);
            world.Draw(view, proj, eye);
            DrawBlobs(1);
            BuildSprites(view, false);
            DrawSpritesBasic();
            DrawLines();
        }
        SyncHud();
        if (!hudHidden) hud.Draw();

        CaptureShot();
        base.Draw(gameTime);
    }

    void CaptureShot()
    {
        var gd = GraphicsDevice;
        var dir = shotDir ?? campaignShots;
        if (pendingShot != null && dir != null)
        {
            int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
            var data = new Color[w * h];
            gd.GetBackBufferData(data);
            using var tex = new Texture2D(gd, w, h);
            tex.SetData(data);
            using var file = File.Create(Path.Combine(dir, pendingShot + ".png"));
            tex.SaveAsPng(file, w, h);
            pendingShot = null;
        }
    }

    /// The shader path: sun shadow map, then the scene into an off-screen target, then the post pass to the screen.
    void DrawShaded(Shading sh)
    {
        var gd = GraphicsDevice;
        bool shadows = quality == Quality.High;
        if (shadows)
        {
            // Cover what the camera sees: about 90 m around you in the field, more as the command camera zooms out.
            float k = MathHelper.SmoothStep(0, 1, blend);
            sh.AimSun(focusPoint, MathHelper.Lerp(90, Math.Clamp(zoom * 2.6f, 70, 260), k));
            gd.Textures[1] = null;
            gd.SetRenderTarget(sh.ShadowMap);
            gd.Clear(Color.White);
            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.BlendState = BlendState.Opaque;
            world.DrawShadowCasters(sh, realTime);
            BuildSprites(sh.LightView, true); // puppets turned to face the sun, so shadows have their silhouette
            sh.Setup(sh.Sprite, Matrix.Identity, sh.LightEye, realTime, false, 0);
            sh.Sprite.Parameters["ShadowMap"].SetValue((Texture2D?)null);
            DrawSpritesShaded(sh.Sprite, "SpriteShadow");
        }
        gd.Textures[1] = null;
        gd.SetRenderTarget(sh.Scene());
        gd.Clear(World.Horizon);
        world.DrawShaded(sh, view, proj, eye, realTime, shadows);
        DrawBlobs(shadows ? 0.4f : 1); // with real shadows the blobs only add contact darkening
        BuildSprites(view, false);
        sh.Setup(sh.Sprite, view * proj, eye, realTime, shadows, 0.45f);
        DrawSpritesShaded(sh.Sprite, "Sprite");
        DrawLines();
        sh.PostToScreen(fxaa: true);
    }

    void DrawBlobs(float alpha)
    {
        shadows.N = 0;
        var t = battle.Terrain;
        void Blob(Vector2 p, float r)
        {
            // Corners follow the ground so the blob lies on slopes.
            VertexPositionTexture V(float dx, float dz, float tu, float tv)
            {
                var q = p + new Vector2(dx, dz);
                return new(new(q.X, t.Height(q) + 0.05f, q.Y), new(tu, tv));
            }
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
        shadowFx.Alpha = alpha;
        shadowFx.View = view; shadowFx.Projection = proj;
        shadowFx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, shadows.A, 0, shadows.N / 3);
    }

    /// Ease every drawn facing toward the real one, once per frame, so turning is a smooth sweep.
    void EaseFacings()
    {
        float turn = MathF.Min(1, frameDt * 12);
        foreach (var u in battle.Units)
        {
            u.ViewFacing += MathHelper.WrapAngle(u.Facing - u.ViewFacing) * turn;
            u.ViewHeading += MathHelper.WrapAngle(u.Heading - u.ViewHeading) * turn;
        }
    }

    /// Build every puppet's quads, billboarded toward the camera of `viewer` (the player's camera or the sun).
    void BuildSprites(Matrix viewer, bool forSun)
    {
        var t = battle.Terrain;
        sprites.N = 0;
        puppet.Begin(viewer);
        var eye2 = new Vector2(eye.X, eye.Z);
        foreach (var u in battle.Units)
        {
            if (!forSun && !u.IsPlayer && u.Team == 0 && blend < 0.5f && Vector2.DistanceSquared(u.Pos, eye2) < 6 * 6) continue; // friends don't fill the lens
            if (deploying && !battle.Options.Scouted && u.Team == 1 && !u.IsLeader) continue; // unscouted: banners only
            puppet.Draw(u, t.Height(u.Pos));
        }
        foreach (var h in battle.LooseHorses) puppet.DrawLooseHorse(h, t.Height(h.Pos));
        foreach (var h in battle.DeadHorses) puppet.DrawDeadHorse(h, t.Height(h.Pos));
    }

    void SpriteStates()
    {
        var gd = GraphicsDevice;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
    }

    void DrawSpritesBasic()
    {
        if (sprites.N == 0) return;
        SpriteStates();
        GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
        spriteFx.View = view; spriteFx.Projection = proj;
        spriteFx.CurrentTechnique.Passes[0].Apply();
        GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, sprites.A, 0, sprites.N / 3);
    }

    void DrawSpritesShaded(Effect fx, string technique)
    {
        if (sprites.N == 0) return;
        SpriteStates();
        fx.Parameters["Atlas"].SetValue(art.Atlas);
        fx.CurrentTechnique = fx.Techniques[technique];
        fx.CurrentTechnique.Passes[0].Apply();
        GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, sprites.A, 0, sprites.N / 3);
    }

    void Line(Vector3 a, Vector3 b, Color c) { lines.Add(new(a, c)); lines.Add(new(b, c)); }
    Vector3 G(Vector2 p, float y = 0.08f) => new(p.X, battle.Terrain.Height(p) + y, p.Y);

    void Ring(Vector2 p, float r, Color c, int seg = 12)
    {
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathF.Tau / seg, a1 = (i + 1) * MathF.Tau / seg;
            Line(G(p + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r), G(p + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r), c);
        }
    }

    void DrawLines()
    {
        lines.N = 0;
        foreach (var a in battle.Arrows)
        {
            var dir = a.Vel.LengthSquared() > 0 ? Vector3.Normalize(a.Vel) : Vector3.Down;
            Line(a.Pos, a.Pos - dir * (a.Stuck >= 0 ? 0.5f : 0.8f), ArrowColor);
        }
        foreach (var s in selected)
            foreach (var u in s.Units)
                if (u.Active) Ring(u.Pos, u.Mounted ? 1.05f : 0.45f, Sel, 8);
        if (command)
        {
            if (battle.Player.Active) Ring(battle.Player.Pos, 0.8f, Gold);
            if (battle.EnemyCaptain.Active) Ring(battle.EnemyCaptain.Pos, 0.8f, Foe);
            foreach (var s in battle.SquadsOf(0))
            {
                if (s.Alive == 0) continue;
                if (s.Order == Order.Charge && s.Target is { Alive: > 0 } tgt)
                {
                    Line(G(s.Centroid(), 0.15f), G(tgt.Centroid(), 0.15f), Foe); // attack order
                    continue;
                }
                if (s.Order != Order.Hold) continue;
                // Front-rank marker with a facing tick.
                var side = new Vector2(-s.Front.Y, s.Front.X) * (Math.Min(s.Columns, s.Alive) - 1) * s.Spacing / 2;
                var c = selected.Contains(s) ? Sel : Friendly;
                Line(G(s.Anchor - side + s.Front), G(s.Anchor + side + s.Front), c);
                Line(G(s.Anchor + s.Front), G(s.Anchor + s.Front * 2.5f), c);
            }
            if (!hud.Over(ms.Position) && PickSquad(mouseGround, 1) is Squad hover)
                foreach (var u in hover.Units) if (u.Active && !u.Routing) Ring(u.Pos, u.Mounted ? 1.1f : 0.5f, Foe, 8);
            if (dragStart is Vector2 d)
            {
                // Formation preview: where every man will stand.
                var enemy = battle.Centroid(1);
                bool click = Vector2.Distance(d, mouseGround) < 2;
                var (a, b) = click ? LineEnds(mouseGround, enemy) : (d, mouseGround);
                Line(G(a, 0.12f), G(b, 0.12f), Color.White);
                foreach (var slot in PreviewSlots(a, b, enemy)) Ring(slot, 0.3f, Color.White * 0.8f, 6);
            }
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
