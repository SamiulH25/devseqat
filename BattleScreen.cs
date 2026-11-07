using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The battle screen: rendering, camera and input around a headless Battle. Two views with an eased camera blend between them:
/// field (third person, you fight) and command (free orbit camera, time slowed, you direct squads).
/// The world (terrain, foliage, sky) is 3D geometry from World; units are 2D sprite puppets. Hosted by ScreenHost.
sealed partial class BattleScreen
{
    readonly ScreenHost host;
    GraphicsDevice GraphicsDevice => host.GraphicsDevice;
    bool IsActive => host.IsActive;
    bool IsMouseVisible { set => host.IsMouseVisible = value; }
    void Exit() => host.Exit();
    SpriteBatch sb => host.Sb;
    Art art => host.Art;
    PixelFont font => host.Font;
    Controls ctl => host.Ctl;
    Shading? shading => host.Shading;
    Quality quality { get => host.Quality; set => host.Quality = value; }

    const float CommandTimeScale = 0.25f, BlendTime = 0.45f, DoubleClick = 0.35f;
    static readonly Color Gold = Hud.Gold, Sel = new(255, 236, 90), Friendly = Hud.Friendly, Foe = Hud.Foe, ArrowColor = new(70, 50, 30);

    Puppet puppet = null!;
    Hud hud = null!;
    World world = null!;
    AlphaTestEffect spriteFx = null!;
    BattleSetup? campaignBattle;               // the battle in progress came from the campaign map
    bool deploying;
    Vector3 focusPoint;                        // what the camera looks at (the shadow map is centred here)
    BasicEffect shadowFx = null!, lineFx = null!;
    Battle battle = null!;
    readonly HashSet<Squad> selected = new();
    /// The division you are giving goals to (cards on the left, Y cycles), or null for plain orders.
    Division? selectedDivision;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPositionTexture> shadows = new();
    readonly Verts<VertexPositionColor> lines = new();

    bool command, paused, menuOpen, helpOpen, wheelOpen, skipMouse = true;
    float yaw, pitch, fieldDist = 5.5f;       // field camera: third person behind the player
    float camYaw, camPitch, zoom = 45;        // command camera: free orbit around camFocus
    float blend, frameDt, realTime, lastCardClick;
    bool replaySaved;
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

    public BattleScreen(ScreenHost host) { this.host = host; }

    /// Called when a campaign battle is over and its result should go back to the map.
    public Action<BattleResult>? OnCampaignBattleOver;
    public Action? OnTitle;
    public Battle Battle => battle;
    public bool InCampaign => campaignBattle != null;
    public string? PendingShot { get => pendingShot; set => pendingShot = value; }
    public bool HasShots => shotDir != null;

    Ui tipUi = null!;

    public void Load()
    {
        tipUi = new Ui(sb, art, font);
        puppet = new Puppet(art, sprites);
        hud = new Hud(sb, art, font);
        spriteFx = new AlphaTestEffect(GraphicsDevice)
        {
            Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128,
            FogEnabled = true, FogColor = World.Horizon.ToVector3(), FogStart = World.FogStart, FogEnd = World.FogEnd,
        };
        shadowFx = new BasicEffect(GraphicsDevice) { TextureEnabled = true, Texture = art.Shadow };
        lineFx = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        NewBattle();
    }

    // ---------- campaign hand-off ----------

    /// A battle handed over by the campaign map: it opens in the deployment phase.
    public void BeginCampaign(BattleSetup s)
    {
        campaignBattle = s;
        battle = new Battle(s.Seed, s.Player, s.Enemy, s.Options);
        battle.StartRecording(s.Seed);
        replaySaved = false;
        SetWorld();
        selected.Clear();
        yaw = MathHelper.PiOver2;
        pitch = 0.3f;
        paused = menuOpen = helpOpen = wheelOpen = false;
        StartDeployment();
        battle.Log($"Battle against {s.EnemyName}: {s.Options.Ground?.Name.ToLowerInvariant() ?? "open"} ground, {s.Options.Weather.Describe}.");
        if (!s.Options.Scouted) battle.Log("No scouts: you cannot see their ranks until the fight starts.");
    }

    public void StartDeployment()
    {
        deploying = true;
        SetCommand(true);
        blend = 1;
    }

    /// Debug staging for the host's campaign screenshots.
    public void StageCommandView(Vector2 focus, float camPitch2, float zoom2, bool startFight)
    {
        if (startFight) deploying = false;
        SetCommand(true);
        blend = 1;
        camFocus = focus; camPitch = camPitch2; zoom = zoom2;
    }

    void FinishCampaignBattle()
    {
        var r = battle.Result();
        campaignBattle = null;
        deploying = false;
        OnCampaignBattleOver?.Invoke(r);
    }

    public void NewBattle()
    {
        // Stand-in for the campaign map: both armies arrive with companies upgraded to some level (1-4).
        int seed = shotDir != null ? 7 : Environment.TickCount;
        var rng = new Random(seed);
        var levels = (Environment.GetEnvironmentVariable("IRONFIELD_LEVELS") ?? (shotDir != null ? "4,3" : "")).Split(',');
        int mine = int.TryParse(levels[0], out var l0) ? l0 : rng.Next(1, 5);
        int theirs = levels.Length > 1 && int.TryParse(levels[1], out var l1) ? l1 : Math.Clamp(mine + rng.Next(-1, 2), 1, 4);
        // IRONFIELD_WEATHER=hour,rain,fog picks the weather; otherwise mostly fair days, sometimes dawn, dusk or rain.
        var wv = (Environment.GetEnvironmentVariable("IRONFIELD_WEATHER") ?? "").Split(',').Select(v => float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : -1).ToArray();
        var weather = wv.Length >= 1 && wv[0] >= 0 ? new Weather(wv[0], wv.Length > 1 ? Math.Max(0, wv[1]) : 0, wv.Length > 2 ? Math.Max(0, wv[2]) : 0)
            : shotDir != null ? Weather.Clear : Weather.For(rng.NextSingle() < 0.7f ? 9 + rng.Next(8) : rng.Next(2) == 0 ? 6.5f : 18.5f, rng.Next(3), seed);
        // A quick battle fills the field: about half the battle size a side (IRONFIELD_MEN=n sets the men a side).
        int men = int.TryParse(Environment.GetEnvironmentVariable("IRONFIELD_MEN"), out var m) ? m : Settings.Current.BattleSize / 2;
        battle = new Battle(seed, Armies.Divide(Armies.Large(rng, Math.Clamp(mine, 1, 4), 0, men)), Armies.Divide(Armies.Large(rng, Math.Clamp(theirs, 1, 4), 1, men)),
            new BattleOptions
            {
                EnemyDamage = Settings.Current.EnemyDamage, Weather = weather, Cap = Math.Max(Settings.Current.BattleSize, men * 2 + 20),
                // IRONFIELD_GROUND=river forces a river; otherwise a third of quick battles have one.
                Ground = TerrainParams.Meadow with { River = Environment.GetEnvironmentVariable("IRONFIELD_GROUND") == "river" || (shotDir == null && rng.NextSingle() < 0.33f) },
            });
        battle.StartRecording(seed);
        replaySaved = false;
        SetWorld();
        battle.Log($"Your company (level {mine}) faces a level {theirs} host: {weather.Describe}.");
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
        Sky.Set(battle.Weather, nightFloor: 0.3f); // moonlight: dark, but you can still fight
        motes.Clear();
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

    /// One frame of the battle (the host has already updated the controls).
    public void Update(float dt)
    {
        pkb = kb; pms = ms;
        kb = Keyboard.GetState(); ms = Mouse.GetState();
        realTime += dt;
        if (battle.Winner >= 0 && !replaySaved && shotDir == null)
        {
            // Keep the last battle as a replay (roadmap E5): `--replay` plays it back exactly.
            replaySaved = true;
            try { battle.FinishRecording()?.Save("last-battle"); } catch (Exception e) { Console.WriteLine("replay not saved: " + e.Message); }
        }
        if (Pressed(Keys.Enter) && Tips.Active) Tips.Dismiss();
        else if (LmbPressed && Tips.Active && command && tipUi.Blocks(ms.Position)) Tips.Dismiss();
        if (deploying) Tips.Show("deploy", "Deployment. Select a company (click its card or press 1-9), then hold RMB and drag to draw its line.\nYou can only deploy in your third of the field. Press Space (or START BATTLE) to begin.");
        else if (!command && battle.Player.Active) Tips.Show("field", "Field view: you fight. On horseback W/S rein, A/D turn, Shift gallops. On foot WASD.\nMove the mouse to choose a swing direction, LMB to attack, RMB to block (match the red mark to parry).\nTab: command view. Hold MMB or Z: the order wheel.");
        else if (command && !deploying) Tips.Show("command", "Command view: time slows. Select companies, RMB to move them, RMB-drag to draw a line,\nRMB on an enemy to attack it. Tab returns to your body: you inspire the men near you.");

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
            OrderKeys();
            if (command) CommandControls(dt); else FieldControls();
        }
        IsMouseVisible = command || Overlay;
        battle.Commanding = command;

        float scale = paused || menuOpen || helpOpen || deploying ? 0 : command && alive ? CommandTimeScale : 1;
        if (scale > 0) { battle.Update(dt * scale); PlaySounds(); Effects(dt * scale); if (battle.GateBroke) world.RebuildFoliage(); }
        Soundscape();
        blend = Math.Clamp(blend + (command ? dt : -dt) / BlendTime, 0, 1);
        frameDt = dt;
        if (shotDir != null) { shotFrames++; ShotScript(); }
        UpdateCamera();
    }

    /// Battle sound cues, heard from the camera.
    void PlaySounds()
    {
        var audio = host.Audio;
        if (audio == null || !audio.Enabled) return;
        var right = Matrix.Invert(view).Right;
        foreach (var (cue, p) in battle.Sounds)
            audio.PlayAt(cue, new Vector3(p.X, battle.Terrain.Height(p) + 1, p.Y), eye, right, cue is Cue.Shout or Cue.Horn or Cue.Rout ? 160 : 70);
    }

    /// Ambient beds and music: galloping and melee din by how much is near the camera; drums once the fighting starts.
    void Soundscape()
    {
        var audio = host.Audio;
        if (audio == null) return;
        var ear = new Vector2(focusPoint.X, focusPoint.Z);
        float range = 25 + 30 * blend;
        int horses = 0, fighting = 0, field = 0;
        foreach (var u in battle.Units)
        {
            if (u.Active && u.EnemyDist < 4) field++; // men in contact anywhere: the roar of the whole battle
            if (!u.Active || Vector2.DistanceSquared(u.Pos, ear) > range * range) continue;
            if (u.Mounted && MathF.Abs(u.Speed) > 5) horses++;
            if (u.Action is UnitAction.Windup or UnitAction.Recover && u.Type.Ammo == 0) fighting++;
        }
        bool quiet = paused || menuOpen || deploying;
        audio.Beds(0.7f, quiet ? 0 : MathF.Min(1, horses / 5f), quiet ? 0 : MathF.Min(1, fighting / 14f), quiet ? 0 : MathF.Min(1, MathF.Sqrt(field / 600f)));
        audio.Music(battle.Winner == 0 ? MusicTrack.Victory : battle.Winner == 1 ? MusicTrack.Defeat : deploying ? MusicTrack.Map : MusicTrack.Battle);
    }

    /// Hand the HUD everything it shows, and lay it out (also used for clicks).
    void SyncHud()
    {
        hud.B = battle; hud.Selected = selected; hud.Ctl = ctl; hud.Minimap = world.Minimap;
        hud.Command = command; hud.Paused = paused; hud.MenuOpen = menuOpen; hud.HelpOpen = helpOpen;
        hud.ShowResult = battle.Winner >= 0 && !helpOpen;
        hud.InCampaign = campaignBattle != null;
        hud.Deploying = deploying;
        if (selectedDivision != null && selectedDivision.Alive == 0) selectedDivision = null;
        hud.Div = selectedDivision;
        hud.WheelOpen = wheelOpen; hud.WheelSector = WheelSector() ?? -1;
        hud.Aim = aimDir; hud.View = view; hud.Proj = proj; hud.Mouse = ms.Position;
        hud.Box = boxStart is Point b && command ? BoxOf(b, ms.Position) : null;
        hud.HoverSquad = command && !hud.Over(ms.Position) ? PickSquad(mouseGround, 1) ?? PickSquad(mouseGround, 0) : null;
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
            case MenuItem.Settings: host.OpenSettings(); break;
            case MenuItem.Continue: FinishCampaignBattle(); break;
            case MenuItem.Title: menuOpen = false; OnTitle?.Invoke(); break;
        }
    }
}
