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
    public SettingsScreen SettingsPanel = null!;
    readonly GraphicsDeviceManager gdm;

    enum Mode { Title, Map, Battle, Lab, Loading }
    Mode mode = Mode.Title;
    // The screen last drawn and when it changed: each new screen rises out of black (docs/archive/VISUALS.md V6).
    Mode shown = Mode.Title;
    float fadeAt = -10;
    // The plate over a battle's opening fade: its name, then the ground and weather.
    string plateName = "", plateNote = "";
    Ui fadeUi = null!;
    BattleScreen battle = null!;
    MapScreen map = null!;
    TitleScreen title = null!;
    UiLab lab = null!;
    Campaign? campaign;
    BattleSetup? setup;

    // Debug: IRONFIELD_CAMPAIGN_SHOTS=/dir walks through the campaign screens, saving a PNG of each, then quits.
    readonly string? campaignShots = Environment.GetEnvironmentVariable("IRONFIELD_CAMPAIGN_SHOTS") is { Length: > 0 } cs ? cs : null;
    int cStage, shotFrames;
    bool shotInventory;
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
        if (Environment.GetEnvironmentVariable("IRONFIELD_ANIM") is { Length: > 0 } animDir)
        {
            AnimSheet.Write(GraphicsDevice, Art, animDir, Environment.GetEnvironmentVariable("IRONFIELD_ANIM_ONLY") ?? ""); // debug: the animation strips
            Environment.Exit(0);
        }
        Font = new PixelFont(GraphicsDevice);
        SettingsPanel = new SettingsScreen(this); // needs Sb and Font
        fadeUi = new Ui(Sb, Art, Font);
        if (Environment.GetEnvironmentVariable("IRONFIELD_QUALITY") is { Length: > 0 } q && Enum.TryParse<Quality>(q, true, out var qv)) Quality = qv;
        string? shaderError = null;
        try { Shading = new Shading(GraphicsDevice); }
        catch (Exception e) { Console.WriteLine("shaders unavailable: " + e.Message); Quality = Quality.Low; shaderError = e.Message; }
        if (GpuReport.Requested) gpuReport = new GpuReport(this, Shading != null, shaderError);

        Audio = new AudioEngine();
        AudioEngine.Current = Audio;
        Ui.OnClick = () => Audio.Play(Cue.Click, 0.6f);
        Ui.OnHover = () => Audio.Play(Cue.Hover, 0.5f);
        battle = new BattleScreen(this);
        battle.Load();
        battle.OnCampaignBattleOver = FinishCampaignBattle;
        battle.OnTitle = () => mode = Mode.Title;
        map = new MapScreen(GraphicsDevice, Sb, Art, Font, Shading, Ctl) { OnBattle = StartCampaignBattle, OnTitle = () => mode = Mode.Title, OnSettings = OpenSettings };
        title = new TitleScreen(this);
        lab = new UiLab(this) { OnBack = () => mode = Mode.Title };
        if (lab.ShotsDir != null) mode = Mode.Lab;
        if (battle.HasShots) mode = Mode.Battle;
        if (campaignShots != null) StartCampaign(3);
    }

    // ---------- modes ----------

    /// A new campaign; `making` is the captain from the character creator (it also chose the road, so no road event).
    /// The world is made on a worker thread behind the loading screen (it takes several seconds); shots runs make it at once.
    public void StartCampaign(int seed, int realm = 0, CaptainMaking? making = null)
    {
        Campaign Make()
        {
            loadStage = "SHAPING THE LAND AND ITS REALMS";
            var c = Campaign.New(seed, realm, making);
            if (campaignShots == null && making == null) c.OfferRoad(); // the first choice: how the captain fights (P5)
            return c;
        }
        if (campaignShots != null)
        {
            campaign = Make();
            if (Environment.GetEnvironmentVariable("IRONFIELD_MAP_HOUR") is { Length: > 0 } hour) campaign.S.Hours = double.Parse(hour, System.Globalization.CultureInfo.InvariantCulture); // shots at a chosen hour
            map.Open(campaign); mode = Mode.Map; return;
        }
        Load("A NEW CAMPAIGN", Make);
    }

    // ---------- loading ----------

    Task<(Campaign C, Terrain T)>? loading;
    volatile string loadStage = "";
    string loadTitle = "";
    float loadSince;

    /// Make a campaign and its map terrain off the main thread while the loading screen shows; `Update` opens it when done.
    void Load(string what, Func<Campaign> make)
    {
        loadTitle = what;
        loadStage = "";
        loadSince = Ui.Now;
        mode = Mode.Loading;
        loading = Task.Run(() =>
        {
            var c = make();
            loadStage = "RAISING THE HILLS AND RIVERS";
            return (c, MapScreen.Prepare(c));
        });
    }

    /// The loading task finished: open the map, or go back to the title with the reason it failed.
    void FinishLoading()
    {
        var task = loading!;
        loading = null;
        if (task.IsFaulted)
        {
            var e = task.Exception!.GetBaseException();
            Console.WriteLine("campaign failed to load: " + e);
            TitleNote = ("the campaign could not be made: " + e.Message).ToUpperInvariant();
            mode = Mode.Title;
            return;
        }
        campaign = task.Result.C;
        map.Open(campaign, task.Result.T);
        mode = Mode.Map;
    }

    /// The loading screen: the game's name, what is being made, a gilt bar that sweeps while it works.
    void DrawLoading()
    {
        GraphicsDevice.Clear(new Color(14, 12, 10));
        var ui = fadeUi;
        ui.Begin(GraphicsDevice.Viewport, Point.Zero, false);
        float t = Ui.Now - loadSince;
        int cx = ui.W / 2, y = ui.H / 2 - ui.U(40);
        ui.TextC("IRONFIELD", cx, y, Kit.Gilt, 4);
        ui.TextC(loadTitle, cx, y + ui.U(42), Kit.Parchment, 2);
        int w = ui.U(260), h = Math.Max(2, ui.U(3)), by = y + ui.U(70);
        ui.Rect(new Rectangle(cx - w / 2, by, w, h), Kit.Gilt * 0.18f);
        float sweep = (t * 0.55f) % 1f, seg = 0.28f;
        int x0 = (int)(cx - w / 2 + w * Math.Clamp(sweep * (1 + seg) - seg, 0, 1)), x1 = (int)(cx - w / 2 + w * Math.Clamp(sweep * (1 + seg), 0, 1));
        ui.Rect(new Rectangle(x0, by, Math.Max(0, x1 - x0), h), Kit.Gilt);
        string stage = loadStage;
        int dots = (int)(t * 2.5f) % 4;
        if (stage != "") ui.TextC(stage + new string('.', dots) + new string(' ', 3 - dots), cx, by + ui.U(14), Kit.Parchment * 0.7f, 1);
        ui.End();
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
        Load("THE CAMPAIGN GOES ON", () => { loadStage = "READING THE CHRONICLE"; return new Campaign(s); });
    }

    /// The UI lab: a gallery of interface concepts (F3 on the title screen).
    public void OpenLab() => mode = Mode.Lab;

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
        battle.BeginCampaign(s, campaign!.S);
        mode = Mode.Battle;
        plateName = s.Field != "" ? $"THE BATTLE OF {s.Field.ToUpperInvariant()}" : s.Tournament >= 0 ? "THE LISTS" : $"AGAINST {s.EnemyName.ToUpperInvariant()}";
        plateNote = $"{(s.Options.Ground?.Name ?? "open").ToUpperInvariant()} GROUND, {s.Options.Weather.Describe.ToUpperInvariant()}";
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
        Tips.InBattle = mode == Mode.Battle;
        switch (mode)
        {
            case Mode.Title:
                IsMouseVisible = true;
                title.Update();
                Audio?.Music(title.InCreator ? MusicTrack.Creator : MusicTrack.Title);
                if (title.InCreator)
                {
                    string cc = title.CreatorCulture;
                    Audio?.SetStemWeights(1f, cc is "azure" or "crimson" or "northern" ? 0.9f : 0f, cc is "southern" or "steppe" ? 0.9f : 0f, cc is "league" or "order" ? 0.9f : 0f);
                }
                Audio?.Beds(title.InCreator ? 0.25f : 0.6f, 0, 0);
                break;
            case Mode.Lab: IsMouseVisible = true; lab.Update(dt); Audio?.Music(MusicTrack.Title); break;
            case Mode.Map: IsMouseVisible = true; map.Update(dt, IsActive); map.Soundscape(Audio, dt); break;
            case Mode.Loading:
                IsMouseVisible = true;
                Audio?.Beds(0.4f, 0, 0);
                if (loading is { IsCompleted: true }) FinishLoading();
                break;
            default: battle.Update(dt); break;
        }
        if (campaignShots != null) { shotFrames++; CampaignShots(); }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        Ui.Now = (float)gameTime.TotalGameTime.TotalSeconds;
        Ui.Instant = campaignShots != null || lab.ShotsDir != null || title.ShotsDir != null || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IRONFIELD_SHOTS"));
        Overflow.Frame(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        Tips.InBattle = mode == Mode.Battle;
        switch (mode)
        {
            case Mode.Title: title.Draw(); break;
            case Mode.Lab: lab.Draw(); break;
            case Mode.Loading: DrawLoading(); break;
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
        Fade();
        if (SettingsPanel.Open) SettingsPanel.Draw();
        CaptureShot();
        if (gpuReport != null)
        {
            if (mode != Mode.Battle) QuickBattle();
            else if (gpuReport.Frame(Quality)) Exit();
        }
        Ui.FrameEnd();
        base.Draw(gameTime);
    }

    /// Black over a screen that just came up, lifting over 0.35 s (a battle holds it longer under its name plate).
    void Fade()
    {
        if (mode != shown)
        {
            if (mode != Mode.Battle) plateName = "";
            shown = mode;
            fadeAt = Ui.Now;
        }
        if (Ui.Instant) return;
        bool plate = plateName != "" && mode == Mode.Battle;
        float hold = plate ? 1.1f : 0, lift = plate ? 0.7f : 0.35f;
        float t = (Ui.Now - fadeAt - hold) / lift;
        if (t >= 1) { plateName = ""; return; }
        float a = 1 - Ease.InOut(Math.Clamp(t, 0, 1));
        var ui = fadeUi;
        ui.Begin(GraphicsDevice.Viewport, Point.Zero, false);
        ui.Rect(new Rectangle(0, 0, ui.W, ui.H), Color.Black * a);
        if (plate)
        {
            // The name comes up first, then a gilt rule draws out under it, then the ground and weather.
            float since = Ui.Now - fadeAt, ta = Ease.OutCubic(Math.Clamp(since / 0.4f, 0, 1)) * a;
            int cx = ui.W / 2, y = ui.H / 2 - ui.U(24) + (int)((1 - ta) * ui.U(6));
            ui.TextC(plateName, cx, y, Kit.Gilt * ta, 3);
            int w = (int)(ui.TW(plateName, 3) * Ease.OutCubic(Math.Clamp((since - 0.2f) / 0.5f, 0, 1)));
            ui.Rect(new Rectangle(cx - w / 2, y + ui.U(30), w, Math.Max(1, ui.U(1))), Kit.Gilt * (0.7f * a));
            ui.TextC(plateNote, cx, y + ui.U(40), Kit.Parchment * (Math.Clamp((since - 0.4f) / 0.4f, 0, 1) * a), 1);
        }
        ui.End();
    }

    void CaptureShot()
    {
        string? name = pendingShot ?? battle.PendingShot ?? lab.PendingShot ?? title.PendingShot;
        string? dir = campaignShots ?? lab.ShotsDir ?? title.ShotsDir ?? Environment.GetEnvironmentVariable("IRONFIELD_SHOTS");
        if (name == null || string.IsNullOrEmpty(dir)) return;
        var gd = GraphicsDevice;
        int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
        var data = new Color[w * h];
        gd.GetBackBufferData(data);
        using var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        Directory.CreateDirectory(dir);
        using var file = File.Create(Path.Combine(dir, name + ".png"));
        tex.SaveAsPng(file, w, h);
        Overflow.Flush(dir, name);
        // What the frame cost to draw, so a heavy effect shows up between runs (docs/archive/VISUALS.md V8).
        if (mode == Mode.Battle) File.AppendAllText(Path.Combine(dir, "render.txt"), $"{name}: {battle.RenderCost}\n");
        pendingShot = null;
        battle.PendingShot = null;
        lab.PendingShot = null;
        title.PendingShot = null;
    }

    int lifeShots;
    double dayHours;

    /// The tavern pictures in a shots run: (culture, hour, winter) for each; a culture with no town of its own borrows one.
    static readonly (string Culture, int Hour, bool Winter)[] TavernLooks =
    {
        ("league", 23, false), ("league", 11, false), ("azure", 23, false), ("azure", 11, false), ("crimson", 23, false), ("crimson", 18, false),
        ("order", 23, false), ("order", 11, false), ("northern", 23, false), ("northern", 11, true), ("southern", 23, false), ("southern", 11, false),
        ("steppe", 23, false), ("steppe", 11, false), ("azure", 23, true),
    };
    int tavernShots = -1;
    double tavernHours;
    Settlement? tavernTown;
    string tavernCulture = "";

    /// Gives back the culture a town lent to a tavern picture.
    void RestoreTavernTown()
    {
        if (tavernTown != null) tavernTown.Culture = tavernCulture;
        tavernTown = null;
    }

    /// A staged step of the shots run was refused by the campaign: stop with the reason instead of crashing later.
    void StagingRefused(string why)
    {
        Console.Error.WriteLine($"campaign shots: staged step {cStage} refused ({why})");
        throw new InvalidOperationException("campaign shots staging refused: " + why);
    }

    void CampaignShots()
    {
        var c = campaign!;
        if (shotFrames < 20 || pendingShot != null) return;
        void Shot(string name) { pendingShot = name; shotFrames = 0; cStage++; }
        if (cStage == 2 && lifeShots < 6) // the land and its life (docs/WORLD_LIFE.md WL3-WL5), in spring before the year turns
        {
            string[] stages = { "farm", "life", "village", "road", "night", "map" };
            if (lifeShots == 4) { dayHours = c.S.Hours; c.S.Hours = Math.Floor(c.S.Hours / 24) * 24 + 22.5; } // lamplight
            if (lifeShots == 5) { c.S.Hours = dayHours; lifeShots++; return; }
            map.Stage(stages[lifeShots]);
            pendingShot = "campaign-" + stages[lifeShots]; shotFrames = 0; lifeShots++;
            return;
        }
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
                foreach (var co in c.Player.Companies) co.Main!.Men = Math.Max(co.Main!.Men, (int)(20 * Balance.Get().ArmyScale));
                c.Player.Pos = castle.Pos + new Vector2(4, 0);
                if (!c.CanBesiege(castle, out var why)) StagingRefused($"besiege {castle.Name}: {why}");
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
            case 20:
            {
                // The career (F5): a lord of the realm with two fiefs, then a king.
                c.Captain.Fealty = true;
                c.Captain.Renown = 420;
                foreach (var t in c.S.Settlements.Where(t => t.Faction == c.Captain.Contract && t.Kind != SettlementKind.Village)
                             .OrderByDescending(t => t.Kind == SettlementKind.Town).Take(2)) t.Owner = c.Player.Id;
                c.S.Factions[c.Captain.Contract].LastCouncil = new() { "Uffing is granted to you.", "The host is to march on Lowmoor." };
                c.Beat = null;
                map.Stage("realm");
                Shot("campaign-realm");
                break;
            }
            case 21:
            {
                // The marshal's summons (F7), as a vassal sees it.
                var m = c.MarshalOf(c.Captain.Contract)!;
                var seat = c.S.Settlements.First(t => t.Kind != SettlementKind.Village && c.FactionsAtWar(c.Captain.Contract, t.Faction));
                c.S.Summons = new Summons { Faction = c.Captain.Contract, Seat = seat.Id, From = m.Id, Until = (float)c.S.Hours + 96 };
                c.Beat = ("THE MARSHAL SUMMONS YOU", new[] { $"{m.Name.ToUpperInvariant()} CALLS THE HOST TO {seat.Name.ToUpperInvariant()}.", "COME WITHIN 4 DAYS. ANSWERING WINS HIS FAVOUR. IGNORING HIM COSTS IT." });
                map.Stage("beat");
                Shot("campaign-summons");
                break;
            }
            case 22: c.S.Summons = null; c.DeclareIndependence(); map.Stage("beat"); Shot("campaign-beat"); break;
            case 23:
            {
                c.Beat = null;
                int me = c.Captain.Contract;
                var w = c.WarBetween(me, 0)!;
                if (w.A == me) { w.ScoreA = 1800; w.ExhB = 41; w.ExhA = 12; } else { w.ScoreB = 1800; w.ExhA = 41; w.ExhB = 12; }
                map.Stage("career");
                Shot("campaign-realm-king");
                break;
            }
            case 24:
            {
                // A fief of the new realm (F6): a market built, walls going up, a little unrest from high taxes.
                c.Beat = null;
                var fief = c.Fiefs.First(t => t.Kind == SettlementKind.Town);
                c.S.Gold = 3000;
                fief.Buildings["market"] = 1; fief.Buildings["yard"] = 2;
                c.SetTax(fief, 2);
                fief.Prosperity = 68; fief.Unrest = 34;
                c.Build(fief, "walls");
                map.Stage("fief", fief);
                Shot("campaign-fief");
                break;
            }
            case 25:
            {
                var t = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Faction != c.Captain.Contract);
                var w = new Character { Id = c.S.NextId++, Kind = "wanderer", Name = "Osric Longshanks", Culture = t.Culture, Role = "Steward", AtSettlement = t.Id };
                c.S.Characters.Add(w);
                c.S.Characters.Add(new Character { Id = c.S.NextId++, Kind = "wanderer", Name = "Wynn Ashdown", Culture = t.Culture, Role = "Surgeon", AtSettlement = t.Id + 1000 });
                c.HireWanderer(t);
                foreach (var extra in c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && s.Faction != c.Captain.Contract).Take(1)) { extra.Faction = c.Captain.Contract; extra.Owner = c.Player.Id; }
                c.RaiseVassal(c.Companions.First());
                c.SetOrder(c.Vassals.First(), "follow");
                var w2 = c.S.Characters.First(k => k.Name == "Wynn Ashdown"); w2.AtSettlement = t.Id; c.HireWanderer(t);
                map.Stage("household");
                Shot("campaign-household");
                break;
            }
            case 26:
            {
                // Events (F8): a lord asks a favour; then a brigand king rises.
                var lord = c.S.Characters.First(k => k.Kind == "lord" && !k.Dead && k.Faction != 0);
                c.S.Event = new PendingEvent { Id = "lord_favour", Lord = lord.Id };
                map.Stage("event");
                Shot("campaign-event");
                break;
            }
            case 27:
            {
                c.S.Event = null;
                var band = c.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down && p.CharId < 0);
                c.Crown(band);
                map.Stage("event");
                Shot("campaign-brigand-king");
                break;
            }
            case 28:
            {
                c.S.Event = null;
                var co = c.Player.Companies.First(x => x.Men > 0);
                if (co.Officer == "") co.Officer = "Hal Brook";
                foreach (var l in c.DivisionLeaders().Take(2).ToList()) c.MakeDivision(l.Char, l.Company);
                if (c.S.Divisions.Count > 0) foreach (var x in c.Player.Companies.Where(x => x.Division < 0).Take(2)) c.AssignDivision(x, c.S.Divisions[0].Id);
                map.Stage("divisions");
                Shot("campaign-divisions");
                break;
            }
            case 29:
            {
                // A goblin holdout (docs/archive/CREATURES.md) at your feet.
                map.Stage("map");
                var lair = c.Holdouts.OrderByDescending(h => h.Men).First();
                c.Player.Pos = lair.Pos + new Vector2(2, 0);
                c.Encounter = lair;
                map.Stage("encounter"); Shot("campaign-holdout"); break;
            }
            case 30:
            {
                // A troll, abroad by night.
                map.Stage("map");
                c.Encounter = null;
                var troll = c.Rares.First();
                c.S.Hours = Math.Floor(c.S.Hours / 24) * 24 + 23;
                c.Player.Pos = troll.Pos + new Vector2(2, 0);
                c.Encounter = troll;
                map.Stage("encounter"); Shot("campaign-troll"); break;
            }
            case 31:
            {
                // The dead of an unburied field, walking in the dark.
                map.Stage("map");
                c.Encounter = null;
                var host = c.RaiseHost(new Grave { Pos = c.Player.Pos + new Vector2(6, 4), Dead = 150, Hours = c.S.Hours });
                c.Player.Pos = host.Pos + new Vector2(2, 0);
                c.Encounter = host;
                map.Stage("encounter"); Shot("campaign-undead"); break;
            }
            case 32:
            {
                // Sites (docs/archive/SIDE_CONTENT.md S2): the country around a ruin, then the ruin's event.
                c.Encounter = null;
                foreach (var p in c.S.Parties.Where(p => p != c.Player && p.Kind != PartyKind.Lord)) p.Down = true; // a clear view
                var ruin = c.Sites.OrderByDescending(x => c.Sites.Count(y => Vector2.Distance(x.Pos, y.Pos) < 60)).First();
                c.Player.Pos = ruin.Pos + new Vector2(8, 6);
                c.S.SitesSeen.Add(ruin.Id);
                map.Stage("sites");
                Shot("campaign-sites");
                break;
            }
            case 33:
            {
                var ruin = c.Sites.First(x => x.Kind == "ruin");
                c.S.Event = new PendingEvent { Id = "site_ruin", Site = ruin.Id };
                map.Stage("event");
                Shot("campaign-site-event");
                break;
            }
            case 34:
            {
                // A caravan met on the road (S4).
                c.S.Event = null;
                var car = c.SpawnCaravan()!;
                car.Faction = c.Captain.Contract;
                car.Pos = c.Player.Pos + new Vector2(2, 0);
                c.Encounter = car;
                map.Stage("encounter");
                Shot("campaign-caravan");
                break;
            }
            case 35 when tavernShots < TavernLooks.Length:
            {
                // The tavern's picture of one town of each culture, by night and by day, in winter and at dusk (MapScreen.Townscape).
                if (tavernShots < 0) { tavernHours = c.S.Hours; tavernShots = 0; }
                RestoreTavernTown();
                var (cul, hour, winter) = TavernLooks[tavernShots++];
                var town = c.S.Settlements.FirstOrDefault(s => s.Kind == SettlementKind.Town && s.Culture == cul && !c.Refuses(s))
                    ?? c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
                if (town.Culture != cul) { tavernTown = town; tavernCulture = town.Culture; town.Culture = cul; }
                double day = Math.Floor(tavernHours / 24);
                day = Math.Floor(day / 120) * 120 + (winter ? 95 : 40); // a winter day of the same year, or one in summer
                c.S.Hours = day * 24 + hour;
                c.Encounter = null; map.Stage("tavern", town);
                Shot($"tavern-{cul}-{(winter ? "winter-" : "")}{(hour is >= 20 or < 5 ? "night" : hour >= 17 ? "dusk" : "day")}");
                cStage--;
                break;
            }
            case 35:
                RestoreTavernTown();
                c.S.Hours = tavernHours;
                c.Encounter = null; map.Stage("tavern"); Shot("campaign-tavern"); break;
            case 36: map.Stage("elders"); Shot("campaign-elders"); break;
            case 37:
            {
                // The end of the war (S7): the summary of what you did.
                c.S.GameOver = true;
                c.S.EndReason = "YOU ARE TAKEN, AND YOUR CAPTORS WILL NOT RANSOM YOU.";
                c.S.Tally["quests"] = 6; c.S.Tally["sites"] = 4; c.S.Tally["tournaments"] = 1;
                map.Stage("gameover");
                Shot("campaign-gameover");
                break;
            }
            case 38: map.Stage("atlas"); Shot("campaign-atlas"); break;
            case 39: map.Stage("armourer"); Shot("campaign-armourer"); break; // also stages the pieces the inventory shows
            case 40 when !shotInventory: map.Stage("inventory"); Shot("campaign-inventory"); cStage--; shotInventory = true; break;
            case 40:
            {
                // Single combat (P2): the encounter with a lord, then the duel itself.
                mode = Mode.Map;
                var lord = c.S.Parties.First(p => p.Kind == PartyKind.Lord && c.CharacterOf(p) != null && p.Faction != Math.Max(0, c.Captain.Contract));
                c.CharacterOf(lord)!.Traits = new List<string> { "Reckless" };
                lord.Pos = c.Player.Pos + new Vector2(2, 0);
                c.Captain.WoundedUntil = 0;
                c.Encounter = lord;
                map.Stage("encounter"); Shot("campaign-encounter-lord"); break;
            }
            case 41: StartCampaignBattle(c.PrepareDuel(c.Encounter!)); cStage++; shotFrames = 0; break;
            case 42 when battle.Battle.Time > 3: Shot("campaign-duel"); break;
            case 43:
            {
                // The roll (docs/archive/LEGACY.md L1-L3): a company's named men and its fallen.
                mode = Mode.Map;
                var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
                for (int i = 0; i < 6; i++) if (c.NameMan(co, i % 2 == 0 ? "Killed 6 against the Red Lances." : "Stood when half the company fell.") is Soldier m) { m.Battles = 3 + i * 2; m.Kills = i * 3; }
                if (co.Roll.Count > 0) co.Roll[^1].Hurt = true;
                c.S.Fallen.Add(new Soldier { Name = "Wat the Grey", Troop = co.Troop, Battles = 9, Fate = "fell", DiedAt = "the Red Lances", DiedDay = c.Day, Company = co.Id, CompanyName = co.Name });
                map.Stage("roll");
                Shot("campaign-roll");
                break;
            }
            case 44:
            {
                // The report with your named men above the companies (L3).
                var lines = c.Player.Companies.Select(k => new CompanyReport(k.Id, k.Name, k.Troop, 0, k.Men + 6, k.Men, 4, 2, 0, 0, 14, false, true)).ToList();
                c.Report = new BattleReport(true, "the Red Lances", lines, 120, 0, 0, 6, 2, LeaderFate.Unhurt) { Roll = new List<(string, string)> { ("FELL", "Wat the Grey, Levies, the company. 9 battles, 4 kills. Killed 6 against the Red Lances."), ("HURT", "Hob of Ashby, the company."), ("NAMED", "Tam Longshanks, Spearmen, the company. Killed 5 against the brigands.") } };
                map.Stage("results");
                Shot("campaign-results-roll");
                break;
            }
            case 45:
            {
                // The upgrade cards of a tier IV company: tier V choices show their attack style (docs/archive/COMPANIONS_MAGIC.md D2).
                var st = c.Player.Companies[0].Main!;
                st.Troop = "fjord_veterans";
                st.Xp = st.PoolCap;
                c.S.Gold = 5000;
                map.Stage("upgrade");
                Shot("campaign-upgrade-elite");
                break;
            }
            case 46:
            {
                // The knight orders (docs/archive/KNIGHT_ORDERS.md K4): sworn to one, at its chapter house; then THE ORDERS on the realm screen.
                var o = c.S.Orders.FirstOrDefault(x => x.Patron == c.Captain.Contract) ?? c.S.Orders[0];
                c.RaiseStanding(o.Id, 35);
                c.Swear(o.Id);
                c.Captain.AuraTier = 2;
                c.Captain.OrderRank = 1;
                map.Stage("chapter", c.SettlementById(o.Chapter));
                Shot("campaign-chapter");
                break;
            }
            case 47: map.Stage("orders"); Shot("campaign-orders"); break;
            case 48:
            {
                // The Black Flame on the captain sheet (docs/archive/KNIGHT_ORDERS.md K7): the pact, corruption, a wall to burn.
                c.S.GameOver = false; // set for the summary shot above
                c.TakePact();
                c.Captain.Corruption = 35;
                c.Captain.AuraWall = true;
                map.Stage("skills");
                Shot("campaign-flame");
                break;
            }
            case 49:
            {
                // Your own order (docs/archive/KNIGHT_ORDERS.md K8): the legend tab before founding (the vows to choose), then after.
                c.LeaveOrder();
                c.Captain.Pact = false; c.Captain.Corruption = 0; c.Captain.AuraTier = 3;
                var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
                co.Battles = Math.Max(co.Battles, 60);
                if (co.BattleHonours.Count < 3) co.BattleHonours.AddRange(new[] { "Ashby Ford", "Hollin Hill", "Wyck Moor" });
                map.Stage("legend");
                Shot("campaign-legend-found");
                break;
            }
            case 50:
            {
                c.FoundOrder(new[] { "no_rob", "no_slaves", "contract" });
                foreach (var m in c.Player.Companies.SelectMany(k => k.Roll).OrderByDescending(m => m.Battles).Take(2)) { m.Battles = Math.Max(m.Battles, 5); c.Knight(m); }
                map.Stage("legend");
                Shot("campaign-legend-order");
                break;
            }
            case 51:
                // Lords' faces and houses (docs/archive/LORDS.md LD1).
                map.Stage("lords");
                Shot("campaign-lords");
                break;
            case 52:
                // Talking to a lord, and a hall (docs/archive/LORDS.md LD3, LD5).
                map.Stage("talk");
                Shot("campaign-talk");
                break;
            case 53:
                map.Stage("hall");
                Shot("campaign-hall");
                break;
            case 54: Exit(); break;
        }
    }
}
