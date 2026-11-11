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
    /// Battle speeds ([ and ], docs/archive/COMBAT_QOL.md Q4), and the one chosen (1 = normal).
    static readonly float[] Speeds = { 0.5f, 1, 2, 3 };
    int speed = 1;
    /// Alerts already looked at for pausing (Settings.PauseOnAlerts).
    int alertsSeen;
    /// Campaign memory (docs/archive/COMBAT_QOL.md Q6): the save the battle came from, the layout before the remembered one was
    /// laid on, the layout as the fight began, and control groups (digit -> company keys; one-off squads are negative).
    CampaignState? memory;
    List<DeploySpot> defaultDeploy = new();
    List<DeploySpot>? deployed;
    readonly Dictionary<int, List<int>> groups = new();
    int lastGroup = -1;
    float lastGroupAt;
    static readonly Color Gold = Hud.Gold, Sel = new(255, 236, 90), Friendly = Hud.Friendly, Foe = Hud.Foe, ArrowColor = new(70, 50, 30), ArrowStreak = new(150, 146, 132), StoneColor = new(60, 58, 54);

    Puppet puppet = null!;
    Hud hud = null!;
    World world = null!;
    AlphaTestEffect spriteFx = null!;
    BattleSetup? campaignBattle;               // the battle in progress came from the campaign map
    bool deploying;
    /// First times on the ground (docs/archive/TERRAIN.md T6): a river, a wood, a field with places to hide.
    void GroundTips()
    {
        var t = battle.Terrain;
        if (t.Rivers.Count > 0)
            Tips.Show("river", $"A river. Deep water is slow, tiring and breaks your ranks. Companies sent over it go by the ford\nor the bridge if one is near (pale blue on the ground map). Press {ctl.First(Act.GroundView)} for the ground map.");
        else if (battle.Places.Any(p => p.Kind == PlaceKind.WoodEdge))
            Tips.Show("wood", "A wood. It slows horse, breaks formations and stops arrows, and men inside it are seen only up close.\nHover the ground in command view to see what it does. A division given AMBUSH waits in a wood, unseen.");
        else if (battle.Places.Any(p => p.Kind is PlaceKind.Crest or PlaceKind.Knoll))
            Tips.Show("height", "High ground. Men fighting downhill hit harder, archers shoot further, and a crest hides what is behind it.\nAn enemy on good ground may wait for you to come to him.");
    }

    /// First times with aura and magic (docs/archive/MAGIC_AURA.md 7): your forms, the blade, being spent, a curse, the relic, your circle.
    void MagicTips()
    {
        var b = battle;
        if (!b.Player.Active) return;
        if (b.Fevered || b.Quenched)
            Tips.Show("cursed", "An enemy curse is on you: an Evil eye quenches your blade, Fever slows your breath.\nAura armour (Aura Expert) halves a curse. Their mages are worth hunting: your lit blade breaks a working.");
        else if (b.Spent)
            Tips.Show("spent", "Your aura is spent (under 20): your blows land lighter and your breath comes back slowly.\nIt returns twice as fast standing still with no foe within 30 m.");
        else if (b.AuraTier >= 2)
            Tips.Show("blade", $"Hold {ctl.First(Act.AuraBlade)} to light the aura blade: harder blows through armour, paid from your aura.\nAt full aura, blows you land bank a surge that your next form spends first.");
        else if (b.FormSlot.Any(f => f != null))
            Tips.Show("forms", $"Your aura is awake. {ctl.First(Act.Form1)}, {ctl.First(Act.Form2)}, {ctl.First(Act.Form3)}: your forms; {ctl.First(Act.GreatForm)}: the great form, once a battle.\nEach costs aura. The aura row by your health shows the pool.");
        if (b.HasRelic && !b.RelicRaised)
            Tips.Show("relic", $"You carry the relic. Press {ctl.First(Act.Relic)} once this battle to raise it: a Blessing on the companies\naround you and aura for you. It costs a charge.");
        if (b.Squads.Any(s => s.Team == 0 && s.Spells.Length > 0 && s.Units.Any(u => u.Active && u.Type.Spells.Length > 0)))
            Tips.Show("circle", $"Your circle casts on its own when a spell fits. In command view select it and press {ctl.First(Act.Cast)}\nto pick a spell, then click the ground to cast it there.");
    }

    /// The ground map, as you last set it (null: shown while deploying only).
    bool? groundView;
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
    // IRONFIELD_SHOTS_FROM=clash starts the script at the melee clashes (docs/archive/MELEE.md), =magic at the magic scenes,
    // =spectacle at the spectacle scenes (docs/archive/SPECTACLE.md), skipping the rest.
    int shotIndex = Environment.GetEnvironmentVariable("IRONFIELD_SHOTS_FROM") switch { "clash" => 53, "magic" => 57, "spectacle" => 61, "workings" => 67, "poses" => 83, "roster" => 89, "life" => 97, "beasts" => 99, _ => 0 }, shotFrames;
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

    /// The quick battle's field. IRONFIELD_GROUND=preset[,river] (or just "river") picks it, IRONFIELD_LAYOUT=Ridge its shape;
    /// otherwise a meadow, and a third of quick battles have a river.
    TerrainParams QuickGround(Random rng)
    {
        var env = (Environment.GetEnvironmentVariable("IRONFIELD_GROUND") ?? "").Split(',');
        bool river = env.Contains("river") || (shotDir == null && rng.NextSingle() < 0.33f);
        var ground = env.Select(TerrainParams.Named).FirstOrDefault(g => g != null) ?? TerrainParams.Meadow;
        ground = ground with { River = river || ground.River };
        return Enum.TryParse<FieldLayout>(Environment.GetEnvironmentVariable("IRONFIELD_LAYOUT"), true, out var l) ? ground with { Layouts = new[] { l } } : ground;
    }

    /// A battle handed over by the campaign map: it opens in the deployment phase.
    public void BeginCampaign(BattleSetup s, CampaignState? keep = null)
    {
        campaignBattle = s;
        memory = keep;
        deployed = null;
        battle = new Battle(s.Seed, s.Player, s.Enemy, s.Options);
        groups.Clear();
        if (keep != null) foreach (var (k, v) in keep.ControlGroups) groups[k] = v.ToList();
        defaultDeploy = battle.DeploymentOf();
        if (keep is { DeployAsLast: true, LastDeploy.Count: > 0 } && !s.Options.Duel && battle.ApplyDeployment(keep.LastDeploy) > 0)
            battle.Log("Your companies stand as they did in your last battle.");
        battle.StartRecording(s.Seed);
        replaySaved = false;
        SetWorld();
        selected.Clear();
        yaw = MathHelper.PiOver2;
        pitch = 0.3f;
        paused = menuOpen = helpOpen = wheelOpen = false;
        if (s.Options.Duel)
        {
            // Single combat (P2): no lines to draw up, straight into your own eyes.
            deploying = false;
            SetCommand(false);
            battle.Log($"Single combat with {s.EnemyName}. The armies watch.");
            return;
        }
        StartDeployment();
        battle.Log((s.Field != "" ? $"The battle of {s.Field}, against {s.EnemyName}" : $"Battle against {s.EnemyName}") + $": {s.Options.Ground?.Name.ToLowerInvariant() ?? "open"} ground, {s.Options.Weather.Describe}.");
        if (!s.Options.Scouted) battle.Log("No scouts: you cannot see their ranks until the fight starts.");
    }

    public void StartDeployment()
    {
        deploying = true;
        SetCommand(true);
        blend = 1;
        intro = shotDir == null && !Ui.Instant ? 0 : -1;
    }

    // The opening sweep over the field (seconds into it; -1 = done) and the closing beat (seconds since the winner was known).
    float intro = -1, ending = -1;
    const float IntroTime = 4.5f, EndBeat = 1.6f;
    /// The battle is decided but the result is not up yet: the field plays on at half speed and the camera rises.
    bool Ending => ending >= 0 && ending < EndBeat && shotDir == null;

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
        if (memory != null)
        {
            // Remember this battle's deployment and your groups for the next (docs/archive/COMBAT_QOL.md Q6).
            if (deployed is { Count: > 0 }) memory.LastDeploy = deployed;
            memory.ControlGroups = groups.ToDictionary(g => g.Key, g => g.Value.Where(id => id > 0).ToList());
            memory = null;
        }
        campaignBattle = null;
        deploying = false;
        OnCampaignBattleOver?.Invoke(r);
    }

    public void NewBattle()
    {
        // Stand-in for the campaign map: both armies arrive with companies upgraded to some level (1-4).
        int seed = shotDir != null ? PressSeed ?? 7 : Environment.TickCount;
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
        // IRONFIELD_AURA=tier[,order] gives the quick battle's captain an aura and forms (docs/archive/KNIGHT_ORDERS.md), e.g. 4,rose.
        var aura = (Environment.GetEnvironmentVariable("IRONFIELD_AURA") ?? "").Split(',');
        int auraTier = int.TryParse(aura[0], out var at) ? Math.Clamp(at, 0, 5) : 0;
        var forms = Forms.Generic.Concat(aura.Length > 1 ? Forms.OfOrder(aura[1]) : Enumerable.Empty<FormDef>()).Where(f => f.Tier <= auraTier)
            .OrderBy(f => f.Order == "" ? 1 : 0).Select(f => f.Id).ToArray();
        battle = new Battle(seed, Armies.Divide(WithMages(Armies.Large(rng, Math.Clamp(mine, 1, 4), 0, men), 0)), Armies.Divide(WithMages(Armies.Large(rng, Math.Clamp(theirs, 1, 4), 1, men), 1)),
            new BattleOptions
            {
                EnemyDamage = Settings.Current.EnemyDamage, Weather = weather, Cap = Math.Max(Settings.Current.BattleSize, men * 2 + 20),
                Ground = QuickGround(rng), AuraTier = auraTier, Forms = forms, AutoPlayer = PressSeed != null && shotDir != null,
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

    /// IRONFIELD_MAGES=troop:team[:count],... adds companies of mages to the quick battle (e.g. galdr_masters:0,crimson_crones:1).
    static ArmySpec WithMages(ArmySpec army, int team)
    {
        var extra = (Environment.GetEnvironmentVariable("IRONFIELD_MAGES") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Split(':')).Where(p => p.Length >= 2 && p[1] == team.ToString() && Troops.All.Any(t => t.Id == p[0]))
            .Select((p, i) => new Company(p[0], p.Length > 2 && int.TryParse(p[2], out var n) ? n : 12, "casters", team * 1000 + 900 + i)).ToList();
        return extra.Count == 0 ? army : army with { Companies = army.Companies.Concat(extra).ToList() };
    }

    void SetWorld()
    {
        Sky.Set(battle.Weather, nightFloor: 0.42f); // moonlight: dark, but you can still see who is who
        ResetMagicFx();
        ResetSpectacle();
        markCount = markNext = 0; falling.Clear(); bearers.Clear(); trauma = hitPause = 0; ending = -1; lastHitAt = lastPlayerHp = -1;
        Array.Clear(trample); trampleAt = 0; trampleDirty = true;
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
    /// Tips wait while the menu, the controls or the result is up, so they never cover its buttons.
    bool TipsHeld => Overlay;
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
        if (!TipsHeld && Pressed(Keys.Enter) && Tips.Active) Tips.Dismiss();
        else if (!TipsHeld && LmbPressed && Tips.Active && command && tipUi.Blocks(ms.Position)) Tips.Dismiss();
        if (battle.Units.Any(battle.Rescuable))
            Tips.Show("rescue", "One of your named men is down (the gold mark). Stand over him while the fight goes on\nand you carry him out alive, if you are quick.");
        if (deploying) Tips.Show("deploy", $"Deployment. Select a company (click its card or press 1-9), then hold RMB and drag to draw its line.\nYou can only deploy in your third of the field. Press {ctl.First(Act.Pause)} (or START BATTLE) to begin.");
        else if (!command && battle.Player.Active) Tips.Show("field", $"Field view: you fight. On horseback {ctl.First(Act.Forward)}/{ctl.First(Act.Back)} rein, {ctl.First(Act.Left)}/{ctl.First(Act.Right)} turn, {ctl.First(Act.Sprint)} gallops.\nMove the mouse to choose a swing direction, {ctl.First(Act.Attack)} to attack, {ctl.First(Act.Block)} to block (match the red mark to parry).\n{ctl.First(Act.CommandView)}: command view. Hold {ctl.Name(Act.OrderWheel)}: the order wheel. {ctl.First(Act.Help)}: all the keys.");
        else if (command && !deploying) Tips.Show("command", $"Command view: time slows. Select companies, RMB to move them, RMB-drag to draw a line,\nRMB on an enemy to attack it. {ctl.First(Act.CommandView)} returns to your body: you inspire the men near you.");
        if (deploying) GroundTips();
        else MagicTips();

        if (ctl.Pressed(Act.Menu)) { if (helpOpen) helpOpen = false; else if (battle.Winner < 0) menuOpen = !menuOpen; skipMouse = true; }
        if (ctl.Pressed(Act.Help)) { helpOpen = !helpOpen; skipMouse = true; }
        SyncHud();

        bool alive = battle.Player.Active;
        if (Overlay)
        {
            battle.Input = new PlayerInput { Dir = aimDir };
            wheelOpen = false;
            if (battle.Winner >= 0 && !Ending && Pressed(Keys.Enter)) { if (campaignBattle != null) FinishCampaignBattle(); else NewBattle(); }
            else if (!Ending && LmbPressed && hud.MenuAt(ms.Position) is MenuItem m) MenuChoice(m);
        }
        else
        {
            if ((!alive || deploying) && !command) SetCommand(true);
            else if (alive && !deploying && ctl.Pressed(Act.CommandView)) SetCommand(!command);
            if (deploying && (ctl.Pressed(Act.Pause) || (LmbPressed && hud.StartAt(ms.Position))))
            {
                deploying = false;
                deployed = battle.DeploymentOf(); // how you drew them up, for the next battle (Q6)
                battle.Log("The battle begins!");
            }
            else if (ctl.Pressed(Act.Pause)) paused = !paused;
            if (ctl.Pressed(Act.Slower) && speed > 0) speed--;
            if (ctl.Pressed(Act.Faster) && speed < Speeds.Length - 1) speed++;
            OrderKeys();
            if (command) CommandControls(dt); else FieldControls();
        }
        IsMouseVisible = command || Overlay;
        battle.Commanding = command;

        // The opening sweep runs until it ends or you press anything.
        if (intro >= 0) { intro += dt; if (intro > IntroTime || LmbPressed || Keyboard.GetState().GetPressedKeyCount() > 0) intro = -1; }
        if (battle.Winner >= 0) ending = ending < 0 ? 0 : ending + dt;
        float scale = paused || menuOpen || helpOpen || deploying ? 0 : command && alive ? CommandTimeScale : 1;
        scale *= Speeds[speed];
        if (Ending) scale = 0.5f; // the last moments of the fight, slowed, before the result
        if (hitPause > 0) { hitPause -= dt; scale = 0; } // your blow lands: the moment holds (the sim simply waits; replays are unchanged)
        scale *= BeatScale; // the battle's cinematic beat slows the view (docs/archive/SPECTACLE.md S1); each tick's step is recorded
        if (scale > 0 && Settings.Current.PauseOnAlerts && battle.Alerts.Count > alertsSeen
            && battle.Alerts.Skip(alertsSeen).Any(a => a.Kind is "breaking" or "horse" or "leaderless")) { paused = true; scale = 0; battle.Log("Paused: see the alert."); }
        alertsSeen = battle.Alerts.Count;
        if (scale > 0) { battle.Advance(dt * scale); PlaySounds(); Effects(dt * scale); SpectacleFrame(dt); Feel(); LeaveMarks(); Trample(); if (battle.GateBroke) world.RebuildFoliage(); }
        Soundscape();
        blend = Math.Clamp(blend + (command ? dt : -dt) / BlendTime, 0, 1);
        frameDt = dt;
        if (shotDir != null) { shotFrames++; if (PressSeed != null) PressScript(); else ShotScript(); }
        UpdateCamera();
    }

    /// Battle sound cues, heard from the camera (closest first so nearby blows always claim the frame's voice budget).
    void PlaySounds()
    {
        var audio = host.Audio;
        if (audio == null || !audio.Enabled) return;
        var right = Matrix.Invert(view).Right;
        var eye2 = new Vector2(eye.X, eye.Z);
        foreach (var (cue, p) in battle.Sounds.OrderBy(s => Vector2.DistanceSquared(s.Pos, eye2)))
        {
            float range = cue switch { Cue.Shout or Cue.Horn or Cue.Rout or Cue.Cheer => 160, Cue.Crash => 200, Cue.Ram or Cue.RamSwing => 120, Cue.Volley => 90, _ => 70 };
            bool nearPlayer = battle.Player.Active && Vector2.DistanceSquared(p, battle.Player.Pos) < 9f;
            audio.PlayAt(cue, new Vector3(p.X, battle.Terrain.Height(p) + 1, p.Y), eye, right, range, priority: nearPlayer);
        }
        var pl = battle.Player;
        if (pl.Active && !command && pl.Hp < pl.Type.MaxHp * 0.28f && (pulseTimer -= frameDt) <= 0)
        {
            pulseTimer = 0.95f;
            audio.Play(Cue.LowHpPulse, 0.55f, priority: true);
        }
        bool spanning = pl.Active && Look.GripOf(pl.Type.Look.Weapon) == Grip.Crossbow && pl.Cooldown > 0.35f;
        if (spanning && !wasSpanning) audio.PlayAt(Cue.CrossbowSpan, new Vector3(pl.Pos.X, battle.Terrain.Height(pl.Pos) + 1, pl.Pos.Y), eye, right, 35, priority: true);
        wasSpanning = spanning;
    }
    float pulseTimer;
    bool wasSpanning;

    /// Ambient beds and music: galloping and melee din by how much is near the camera; drums once the fighting starts.
    void Soundscape()
    {
        var audio = host.Audio;
        if (audio == null) return;
        var ear = new Vector2(focusPoint.X, focusPoint.Z);
        float range = 25 + 30 * blend;
        int horses = 0, fighting = 0, field = 0, marching = 0, men = 0;
        foreach (var u in battle.Units)
        {
            if (!u.Active) continue;
            men++;
            if (u.EnemyDist < 4) field++; // men in contact anywhere: the roar of the whole battle
            if (Vector2.DistanceSquared(u.Pos, ear) > range * range) continue;
            if (u.Mounted && MathF.Abs(u.Speed) > 5) horses++;
            else if (!u.Mounted && MathF.Abs(u.Speed) > 0.8f && u.EnemyDist > 4) marching++;
            if (u.Action is UnitAction.Windup or UnitAction.Recover && u.Type.Ammo == 0) fighting++;
        }
        bool quiet = paused || menuOpen || deploying;
        float hush = 1 - 0.6f * MathF.Max(BeatAmount, Sky.OmenDark); // a beat or a gathering working quiets the field
        audio.Beds(0.7f * (1 - Sky.OmenDark * 0.8f), quiet ? 0 : MathF.Min(1, horses / 5f) * hush, quiet ? 0 : MathF.Min(1, fighting / 14f) * hush,
            quiet ? 0 : MathF.Min(1, MathF.Sqrt(field / 600f)) * hush, quiet ? 0 : MathF.Min(1, marching / 30f) * hush, battle.Weather.Snow ? 0 : battle.Weather.Rain);
        audio.Intensity(men == 0 ? 0 : MathF.Min(1, 3f * field / men)); // a third of the field in contact is the full score
        audio.Music(battle.Winner == 0 ? MusicTrack.Victory : battle.Winner == 1 ? MusicTrack.Defeat : deploying ? MusicTrack.Deploy : MusicTrack.Battle);
        if (battle.Winner == 0 && !cheered) { cheered = true; audio.Play(Cue.Cheer, 0.9f); } // your men cheer the day
        if (battle.Winner < 0) cheered = false;
    }
    bool cheered;

    /// The dotted way the first chosen company would take while you draw an order (round a river by the ford, through a hedge's gap).
    List<Vector2>? route;
    Vector2? routeAt;

    /// Hand the HUD everything it shows, and lay it out (also used for clicks).
    void SyncHud()
    {
        hud.B = battle; hud.Selected = selected; hud.Ctl = ctl; hud.Minimap = world.Minimap; hud.GroundMap = world.GroundMap;
        hud.GroundView = groundView ?? deploying; // the ground map is up while you deploy, until you close it
        hud.GroundLine = command && !hud.Over(ms.Position) && dragStart == null ? Grounds.Describe(battle.Terrain.GroundAt(mouseGround)) : null;
        hud.OrderWarning = command && dragStart != null && Targets().Count > 0 ? battle.GroundWarning(Targets(), mouseGround) : null;
        if (command && dragStart != null && Targets() is { Count: > 0 } ts)
        {
            if (routeAt == null || Vector2.DistanceSquared(routeAt.Value, mouseGround) > 4) { route = battle.Route(ts[0], mouseGround); routeAt = mouseGround; }
        }
        else { route = null; routeAt = null; }
        hud.CastBy = castBy; hud.CastPick = castPick;
        hud.Command = command; hud.Paused = paused; hud.Speed = Speeds[speed]; hud.MenuOpen = menuOpen; hud.HelpOpen = helpOpen;
        hud.ShowResult = battle.Winner >= 0 && !helpOpen && !Ending;
        hud.InCampaign = campaignBattle != null;
        hud.Deploying = deploying;
        hud.DeployLast = memory is { LastDeploy.Count: > 0 } && !battle.Options.Siege && !battle.Options.Tournament ? memory.DeployAsLast : null;
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
