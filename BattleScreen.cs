using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The battle screen: rendering, camera and input around a headless Battle. Two views with an eased camera blend between them:
/// field (third person, you fight) and command (free orbit camera, time slowed, you direct squads).
/// The world (terrain, foliage, sky) is 3D geometry from World; units are 2D sprite puppets. Hosted by ScreenHost.
sealed class BattleScreen
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
        battle = new Battle(seed, Armies.Random(rng, Math.Clamp(mine, 1, 4), 0), Armies.Random(rng, Math.Clamp(theirs, 1, 4), 1),
            new BattleOptions
            {
                EnemyDamage = Settings.Current.EnemyDamage, Weather = weather,
                // IRONFIELD_GROUND=river forces a river; otherwise a third of quick battles have one.
                Ground = TerrainParams.Meadow with { River = Environment.GetEnvironmentVariable("IRONFIELD_GROUND") == "river" || (shotDir == null && rng.NextSingle() < 0.33f) },
            });
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
        int horses = 0, fighting = 0;
        foreach (var u in battle.Units)
        {
            if (!u.Active || Vector2.DistanceSquared(u.Pos, ear) > range * range) continue;
            if (u.Mounted && MathF.Abs(u.Speed) > 5) horses++;
            if (u.Action is UnitAction.Windup or UnitAction.Recover && u.Type.Ammo == 0) fighting++;
        }
        bool quiet = paused || menuOpen || deploying;
        audio.Beds(0.7f, quiet ? 0 : MathF.Min(1, horses / 5f), quiet ? 0 : MathF.Min(1, fighting / 14f));
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
                selected.Add(b.Commanded.First());
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
            case 15 when pendingShot == null:
                Gallery(new[] { "clansmen", "axemen", "huscarls", "greatswords", "crossbowmen", "horse_archers" }, 4);
                shotFrames = 0; shotIndex++; break;
            case 16 when shotFrames > 20:
                pendingShot = "gallery-north"; shotIndex++; break;
            case 17 when pendingShot == null:
                zoom = 8.5f; camPitch = 0.08f; camFocus = new Vector2(-9.8f, 0.6f);
                pendingShot = "gallery-north-close"; shotIndex++; break;
            case 18 when pendingShot == null: Exit(); break;
        }
    }

    /// Debug lineup of every troop, left to right along each upgrade line, facing the camera.
    void Gallery(string[]? only = null, int palette = 0)
    {
        battle = Battle.Empty(1);
        SetWorld();
        var order = only ?? new[] { "levies", "shieldmen", "men_at_arms", "ironguard", "spearrunners", "pikemen", "bowmen", "longbowmen",
            "outriders", "lancers", "knights", "captain" };
        float x = 0;
        foreach (var id in order)
        {
            var t = Troops.Get(id);
            x += t.Mount != null ? 2.2f : 1.4f;
            var u = battle.AddSquad(0, t, 1, new Vector2(-x, 0), 1, -Vector2.UnitY).Units[0];
            u.ViewFacing = u.ViewHeading = u.Facing;
            u.Palette = palette;
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
        (selected.Count > 0 ? selected : battle.Commanded).Where(s => s.Alive > 0).ToList();

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
            case Cmd.Formation:
            {
                var list = Targets();
                if (list.Count == 0) break;
                // Cycle from the first company's formation; each company takes the next one it can (horse has no shield wall).
                var first = list[0].Choices(list[0].Units[0].Type.HasShield);
                var next = first[(Array.IndexOf(first, list[0].Formation) + 1) % first.Length];
                foreach (var s in list)
                {
                    var ok = s.Choices(s.Units[0].Type.HasShield);
                    s.Formation = ok.Contains(next) ? next : Formation.Line;
                }
                battle.Log((selected.Count == 0 ? "All troops" : string.Join(", ", list.Select(s => s.Name))) + ": " + next switch
                {
                    Formation.ShieldWall => "shield wall!", Formation.Square => "form square!", Formation.Wedge => "wedge!", Formation.Loose => "open ranks!", _ => "form line!",
                });
                break;
            }
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
        var mine = battle.Commanded.ToList();
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
            Bash = ctl.Pressed(Act.Bash),
            Swap = ctl.Pressed(Act.SwapWeapon),
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
                foreach (var s in battle.Commanded)
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

    public void Draw()
    {
        var gd = GraphicsDevice;
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
        if (!hudHidden)
        {
            hud.Draw();
            tipUi.Begin(gd.Viewport, ms.Position, false);
            Tips.Draw(tipUi, tipUi.U(124));
            tipUi.End();
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
        sh.PostToScreen(fxaa: Settings.Current.Fxaa);
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

    // ---------- particles (D1) ----------

    struct Mote { public Vector3 Pos, Vel; public float Life, Max, Size, Grow; public Color Col; public bool Heavy; }
    readonly List<Mote> motes = new();
    readonly Random fx = new(5);

    void Spray(Vector2 at, float height, int n, Color col, float speed, float life, float size, bool heavy, float grow = 0)
    {
        if (motes.Count > 1500) return;
        var g = battle.Terrain.Height(at);
        for (int i = 0; i < n; i++)
        {
            var dir = new Vector3(fx.NextSingle() * 2 - 1, fx.NextSingle() * 0.8f + (heavy ? 0.4f : 0.1f), fx.NextSingle() * 2 - 1);
            motes.Add(new Mote { Pos = new Vector3(at.X, g + height, at.Y), Vel = dir * speed, Life = life * (0.6f + fx.NextSingle() * 0.6f), Max = life,
                Size = size, Grow = grow, Col = col, Heavy = heavy });
        }
    }

    /// Sparks off steel, blood from wounds, dust under galloping hooves and at a charge's impact.
    void Effects(float dt)
    {
        foreach (var (cue, at) in battle.Sounds)
            switch (cue)
            {
                case Cue.Clash: Spray(at, 1.3f, 6, new Color(255, 226, 140), 3.5f, 0.25f, 0.05f, true); break;
                case Cue.Block: Spray(at, 1.2f, 3, new Color(220, 200, 160), 2f, 0.2f, 0.05f, true); break;
                case Cue.Hit: case Cue.ArrowHit: Spray(at, 1.1f, 5, new Color(140, 20, 22), 1.8f, 0.55f, 0.06f, true); break;
                case Cue.Impact: Spray(at, 0.2f, 10, new Color(168, 150, 116), 1.6f, 1.3f, 0.25f, false, 0.6f); break;
            }
        foreach (var u in battle.Units)
            if (u.Active && u.Mounted && u.Speed > 5.5f && fx.NextSingle() < dt * 9 && battle.Terrain.WaterDepth(u.Pos) == 0)
                Spray(u.Pos - u.HeadingDir * 0.8f, 0.1f, 1, battle.Weather.Rain > 0.3f ? new Color(96, 82, 60) : new Color(176, 160, 124), 0.6f, 1.2f, 0.3f, false, 0.7f);
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            var m = motes[i];
            m.Life -= dt;
            if (m.Life <= 0) { motes.RemoveAt(i); continue; }
            if (m.Heavy) m.Vel.Y -= 9.8f * dt; else m.Vel *= 1 - 1.5f * dt;
            m.Pos += m.Vel * dt;
            m.Size += m.Grow * dt;
            float ground = battle.Terrain.Height(new Vector2(m.Pos.X, m.Pos.Z));
            if (m.Pos.Y < ground) { m.Pos.Y = ground + 0.01f; m.Vel = Vector3.Zero; }
            motes[i] = m;
        }
    }

    /// Build every puppet's quads, billboarded toward the camera of `viewer` (the player's camera or the sun).
    void BuildSprites(Matrix viewer, bool forSun)
    {
        var t = battle.Terrain;
        sprites.N = 0;
        puppet.Begin(viewer);
        puppet.Now = battle.Time;
        // A banner over every company, carried by its first man.
        foreach (var s in battle.Squads)
        {
            if (s.Company == null || s.Alive == 0) continue;
            if (deploying && !battle.Options.Scouted && s.Team == 1) continue;
            var bearer = s.Units.FirstOrDefault(u => u.Active && !u.Routing);
            if (bearer == null) continue;
            var at = bearer.Pos - bearer.Dir * 0.35f + new Vector2(-bearer.Dir.Y, bearer.Dir.X) * 0.25f;
            puppet.Banner(at, t.Height(at) + (bearer.Mounted ? 1.1f : 0), bearer.Palette, battle.Time * 3 + s.Units.Count);
        }
        if (!forSun)
            foreach (var m in motes) puppet.Particle(m.Pos, m.Size * (m.Heavy ? 1 : MathF.Min(1, m.Life / m.Max * 2)), m.Col);
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

    /// Rain streaks (or snowflakes) in a box around the camera, falling on a fixed pattern so nothing is stored.
    void Precipitation()
    {
        var w = battle.Weather;
        if (w.Rain < 0.05f) return;
        int n = (int)(700 * w.Rain);
        var c = eye;
        float t = realTime;
        var col = w.Snow ? new Color(236, 238, 244) : new Color(150, 162, 178);
        for (int i = 0; i < n; i++)
        {
            uint h = (uint)i * 2654435761u;
            float x = (h & 1023) / 1023f * 50 - 25, z = (h >> 10 & 1023) / 1023f * 50 - 25, ph = (h >> 20 & 1023) / 1023f;
            float fall = w.Snow ? 1.2f : 9f, y = 18 - (ph * 18 + t * fall) % 18;
            var p = new Vector3(MathF.Floor(c.X / 50) * 50 + x, c.Y - 6 + y, MathF.Floor(c.Z / 50) * 50 + z);
            if (p.X < c.X - 25) p.X += 50; if (p.X > c.X + 25) p.X -= 50;
            if (p.Z < c.Z - 25) p.Z += 50; if (p.Z > c.Z + 25) p.Z -= 50;
            var drift = w.Snow ? new Vector3(MathF.Sin(t + i) * 0.05f, -0.08f, 0) : new Vector3(0.05f, -0.7f, 0.02f);
            Line(p, p + drift, col);
        }
    }

    void DrawLines()
    {
        lines.N = 0;
        Precipitation();
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
            foreach (var s in battle.Commanded)
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
