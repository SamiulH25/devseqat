using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The MonoGame Game: owns the device and everything the screens share (art, font, shaders, controls),
/// switches between the title, campaign map and battle screens, and hands battles between the campaign and the battle screen.
sealed class ScreenHost : Game
{
    public SpriteBatch Sb = null!;
    public Art Art = null!;
    public PixelFont Font = null!;
    public Shading? Shading;
    public readonly Controls Ctl = Controls.Load();
    GpuReport? gpuReport;
    public Quality Quality = Enum.TryParse<Quality>(Settings.Current.Quality, out var q0) ? q0 : Quality.High;
    public AudioEngine? Audio;
    public readonly SettingsScreen SettingsPanel;
    readonly GraphicsDeviceManager gdm;

    enum Mode { Title, Map, Battle }
    Mode mode = Mode.Title;
    BattleScreen battle = null!;
    MapScreen map = null!;
    TitleScreen title = null!;
    Campaign? campaign;
    BattleSetup? setup;

    // Debug: IRONFIELD_CAMPAIGN_SHOTS=/dir walks through the campaign screens, saving a PNG of each, then quits.
    readonly string? campaignShots = Environment.GetEnvironmentVariable("IRONFIELD_CAMPAIGN_SHOTS") is { Length: > 0 } cs ? cs : null;
    int cStage, shotFrames;
    string? pendingShot;
    KeyboardState kb;
    MouseState ms;

    public ScreenHost()
    {
        var s = Settings.Current;
        gdm = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = s.Width, PreferredBackBufferHeight = s.Height, GraphicsProfile = GraphicsProfile.HiDef, IsFullScreen = s.Fullscreen,
        };
        Window.AllowUserResizing = true;
        Window.Title = "Ironfield";
        Window.TextInput += (_, e) => { if (Ui.Typed.Count < 32) Ui.Typed.Enqueue(e.Character); };
        SettingsPanel = new SettingsScreen(this);
    }

    /// Apply resolution and fullscreen from the settings.
    public void ApplyVideo()
    {
        var s = Settings.Current;
        gdm.PreferredBackBufferWidth = s.Width;
        gdm.PreferredBackBufferHeight = s.Height;
        gdm.IsFullScreen = s.Fullscreen;
        gdm.ApplyChanges();
    }

    public void OpenSettings() => SettingsPanel.Open = true;

    protected override void LoadContent()
    {
        Sb = new SpriteBatch(GraphicsDevice);
        Art = new Art(GraphicsDevice);
        if (Environment.GetEnvironmentVariable("IRONFIELD_DUMP_ATLAS") is { Length: > 0 } atlasPath)
        {
            using var file = File.Create(atlasPath); // debug: inspect the generated sprite sheet
            Art.Atlas.SaveAsPng(file, Art.Atlas.Width, Art.Atlas.Height);
        }
        Font = new PixelFont(GraphicsDevice);
        if (Environment.GetEnvironmentVariable("IRONFIELD_QUALITY") is { Length: > 0 } q && Enum.TryParse<Quality>(q, true, out var qv)) Quality = qv;
        string? shaderError = null;
        try { Shading = new Shading(GraphicsDevice); }
        catch (Exception e) { Console.WriteLine("shaders unavailable: " + e.Message); Quality = Quality.Low; shaderError = e.Message; }
        if (GpuReport.Requested) gpuReport = new GpuReport(this, Shading != null, shaderError);

        Audio = new AudioEngine();
        AudioEngine.Current = Audio;
        Ui.OnClick = () => Audio.Play(Cue.Click, 0.6f);
        battle = new BattleScreen(this);
        battle.Load();
        battle.OnCampaignBattleOver = FinishCampaignBattle;
        battle.OnTitle = () => mode = Mode.Title;
        map = new MapScreen(GraphicsDevice, Sb, Art, Font, Shading, Ctl) { OnBattle = StartCampaignBattle, OnTitle = () => mode = Mode.Title, OnSettings = OpenSettings };
        title = new TitleScreen(this);
        if (battle.HasShots) mode = Mode.Battle;
        if (campaignShots != null) StartCampaign(3);
    }

    // ---------- modes ----------

    public void StartCampaign(int seed)
    {
        campaign = Campaign.New(seed);
        map.Open(campaign);
        mode = Mode.Map;
    }

    /// A message for the title screen (why a save would not load).
    public string TitleNote = "";

    public bool CanContinue => CampaignState.Exists("autosave") || CampaignState.Exists("quicksave");

    public void ContinueCampaign()
    {
        string? latest = new[] { "autosave", "quicksave" }.Where(CampaignState.Exists)
            .OrderByDescending(n => File.GetLastWriteTimeUtc(Path.Combine(CampaignState.SaveDir, n + ".json"))).FirstOrDefault();
        if (latest == null) return;
        if (CampaignState.Load(latest) is not CampaignState s) { TitleNote = (CampaignState.LoadError ?? "the save could not be read").ToUpperInvariant(); return; }
        TitleNote = "";
        campaign = new Campaign(s);
        map.Open(campaign);
        mode = Mode.Map;
    }

    public void QuickBattle()
    {
        battle.NewBattle();
        battle.StartDeployment();
        mode = Mode.Battle;
    }

    void StartCampaignBattle(BattleSetup s)
    {
        campaign!.S.Save("autosave");
        setup = s;
        battle.BeginCampaign(s);
        mode = Mode.Battle;
    }

    void FinishCampaignBattle(BattleResult r)
    {
        campaign!.FinishBattle(r, setup!);
        setup = null;
        mode = Mode.Map;
        map.ShowResults();
    }

    public void CycleQuality() => Quality = Shading == null ? Quality.Low : (Quality)(((int)Quality + 1) % 3);

    // ---------- the loop ----------

    protected override void Update(GameTime gameTime)
    {
        kb = Keyboard.GetState(); ms = Mouse.GetState();
        Ctl.Update(kb, ms);
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 30f);
        if (SettingsPanel.Open)
        {
            IsMouseVisible = true;
            SettingsPanel.Update();
            base.Update(gameTime);
            return;
        }
        if (Ctl.Pressed(Act.Graphics))
        {
            CycleQuality();
            if (mode == Mode.Battle) battle.Battle.Log("Graphics: " + Quality switch { Quality.High => "high (shadows, post)", Quality.Medium => "medium (no shadows)", _ => "low (basic)" });
        }
        Audio?.Update(dt);
        switch (mode)
        {
            case Mode.Title: IsMouseVisible = true; title.Update(); Audio?.Music(MusicTrack.Map); Audio?.Beds(0.6f, 0, 0); break;
            case Mode.Map: IsMouseVisible = true; map.Update(dt, IsActive); Audio?.Music(MusicTrack.Map); Audio?.Beds(1, 0, 0); break;
            default: battle.Update(dt); break;
        }
        if (campaignShots != null) { shotFrames++; CampaignShots(); }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        switch (mode)
        {
            case Mode.Title: title.Draw(); break;
            case Mode.Map:
                try { map.Draw(Quality); }
                catch (Exception e) when (Quality != Quality.Low)
                {
                    Console.WriteLine("shader pipeline failed on the map, falling back: " + e);
                    GraphicsDevice.SetRenderTarget(null);
                    Quality = Quality.Low;
                }
                break;
            default: battle.Draw(); break;
        }
        if (SettingsPanel.Open) SettingsPanel.Draw();
        CaptureShot();
        if (gpuReport != null)
        {
            if (mode != Mode.Battle) QuickBattle();
            else if (gpuReport.Frame(Quality)) Exit();
        }
        base.Draw(gameTime);
    }

    void CaptureShot()
    {
        string? name = pendingShot ?? battle.PendingShot;
        string? dir = campaignShots ?? Environment.GetEnvironmentVariable("IRONFIELD_SHOTS");
        if (name == null || string.IsNullOrEmpty(dir)) return;
        var gd = GraphicsDevice;
        int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
        var data = new Color[w * h];
        gd.GetBackBufferData(data);
        using var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        using var file = File.Create(Path.Combine(dir, name + ".png"));
        tex.SaveAsPng(file, w, h);
        pendingShot = null;
        battle.PendingShot = null;
    }

    void CampaignShots()
    {
        var c = campaign!;
        if (shotFrames < 20 || pendingShot != null) return;
        void Shot(string name) { pendingShot = name; shotFrames = 0; cStage++; }
        switch (cStage)
        {
            case 0: map.Stage("map"); Shot("campaign-map"); break;
            case 1: map.Stage("close"); Shot("campaign-close"); break;
            case 2: { var st0 = c.Player.Companies[0].Main!; st0.Xp = st0.PoolCap * 0.6f; } c.Captain.Renown = 120; map.Stage("party"); Shot("campaign-party"); break;
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
                var lord = c.S.Parties.First(p => p.Kind == PartyKind.Lord && p.Faction != Math.Max(0, c.Captain.Contract));
                lord.Pos = c.Player.Pos + new Vector2(2, 0);
                c.Encounter = lord;
                StartCampaignBattle(c.PrepareBattle(lord));
                cStage++; shotFrames = 0; break;
            }
            case 8: Shot("campaign-deploy"); break;
            case 9: battle.StageCommandView(battle.Battle.Centroid(0), 0.8f, 50, true); cStage++; break;
            case 10 when battle.Battle.Time > 7: battle.StageCommandView(battle.Battle.Centroid(0), 0.8f, 50, false); Shot("campaign-battle"); break;
            case 11:
            {
                mode = Mode.Map;
                c.TakePrisoners("levies", 12);
                map.Stage("contract");
                Shot("campaign-contract");
                break;
            }
            case 12:
            {
                var castle = c.S.Settlements.Where(s => s.Kind != SettlementKind.Village && c.FactionsAtWar(c.Captain.Contract, s.Faction)).OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).First();
                foreach (var co in c.Player.Companies) co.Main!.Men = Math.Max(co.Main!.Men, 20);
                c.Player.Pos = castle.Pos + new Vector2(4, 0);
                c.Besiege(castle);
                castle.SiegeSince -= 13;
                map.Stage("siege", castle);
                Shot("campaign-siege-panel");
                break;
            }
            case 13: StartCampaignBattle(c.PrepareAssault(c.Besieging!)); cStage++; shotFrames = 0; break;
            case 14: Shot("campaign-siege-deploy"); break;
            case 15: battle.StageCommandView(new Vector2(0, 0), 0.9f, 60, true); cStage++; break;
            case 16 when battle.Battle.Time > 10: battle.StageCommandView(new Vector2(-10, 6), 0.75f, 45, false); Shot("campaign-siege"); break;
            case 17:
            {
                // Winter at midday, with a village burning nearby.
                mode = Mode.Map;
                c.Player.SiegeTarget = -1;
                c.S.Hours = 100 * 24 + 12;
                c.Encounter = null; c.Player.Cooldown = 1e6f;
                var v = c.S.Settlements.Where(s => s.Kind == SettlementKind.Village).OrderBy(s => Vector2.Distance(s.Pos, c.Player.Pos)).First();
                v.ThreatUntil = (float)c.S.Hours + 100;
                map.Stage("map");
                cStage++; shotFrames = 0; break;
            }
            case 18: Shot("campaign-winter"); break;
            case 19:
            {
                c.Captain.Skills["Arms"] = 62; c.Captain.Skills["Leadership"] = 41; c.Captain.Skills["Logistics"] = 27;
                c.TakePerk("WarCry");
                map.Stage("skills");
                Shot("campaign-skills");
                break;
            }
            case 20: Exit(); break;
        }
    }
}
