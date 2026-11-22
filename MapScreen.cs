using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The campaign map screen (docs/CAMPAIGN.md 3.4 and 9): the world in the battle renderer's 3D style, parties as large banner-bearer
/// puppets, and the campaign interface (map HUD plus encounter, settlement, party, upgrade, results and menu panels).
sealed partial class MapScreen
{
    enum PanelKind { None, Encounter, Settlement, Party, Upgrade, Results, Menu, GameOver, Siege, Characters, Skills, Realm, Beat, Event, Divisions, Atlas, Roll, Help, Talk, Inventory, Quests, Saves, Camp, Fallen }

    public Campaign C = null!;
    public Action<BattleSetup>? OnBattle;
    public Action? OnTitle, OnSettings;
    /// Open a save file (the host, behind the loading screen); the reason it cannot be read otherwise.
    public Func<string, string?>? OnLoadSave;
    /// SAVE AS and LOAD from the menu (SaveScreen.cs).
    readonly SaveScreen saves = new();
    /// A save just written, waiting for the next frame's map to be taken as its picture.
    string? thumbFor;

    /// Ironman campaigns keep one save; others quick-save (Campaign/Saves.cs: a folder a campaign).
    void Save() { Write(C.S.Ironman ? SaveKind.Auto : SaveKind.Quick); C.Log("Saved."); }

    /// Write a save of the campaign and take the map, as the next frame draws it, for its picture.
    void Write(SaveKind kind) => thumbFor = Saves.Save(C.S, kind);

    /// Shots: write a save as the menu would, picture and all.
    public void ShotSave(SaveKind kind, string label = "") => thumbFor = Saves.Save(C.S, kind, label);

    /// This campaign's quicksave, loaded in place (the same map).
    void QuickLoad()
    {
        var path = Path.Combine(Saves.DirOf(C.S.CampaignId), "quick.json");
        if (C.S.CampaignId != "" && Saves.Load(path) is CampaignState loaded) { Open(new Campaign(loaded)); C.Log("Loaded."); }
        else C.Log(CampaignState.LoadError is string err && File.Exists(path) ? "Quicksave not loaded: " + err + "." : "No quicksave in this campaign.");
    }

    /// The map without the interface, shrunk, as the picture of the save just written.
    void GrabThumb()
    {
        int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
        var px = new Color[w * h];
        gd.GetBackBufferData(px);
        Saves.WriteThumb(thumbFor!, px, w, h);
        thumbFor = null;
    }

    readonly GraphicsDevice gd;
    readonly Art art;
    readonly Shading? shading;
    readonly Controls ctl;
    readonly Ui ui;
    readonly Puppet puppet;
    readonly Puppet3D? puppet3D;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPNCT> units3D = new();
    readonly BoundingFrustum unitFrustum = new(Matrix.Identity);
    readonly Verts<VertexPositionColor> lines = new();
    readonly AlphaTestEffect spriteFx;
    readonly BasicEffect lineFx;
    readonly Dictionary<int, Unit> bearers = new();
    readonly HashSet<int> bearerSeen = new();
    readonly List<int> bearerDrop = new();
    /// Birds and beasts round the camera (MapLife.cs, docs/archive/WORLD_LIFE.md WL4); drawing only.
    MapLife? life;
    Quality lifeQuality = Quality.High;
    readonly List<Vector2> partySpots = new();
    World? world;
    /// What the last frame's world cost to draw (render.txt in campaign shots).
    public string? RenderCost => world?.RenderCost;
    Terrain terrain = null!;
    int season;
    readonly Dictionary<int, List<Vector2>> trails = new(); // where each party has been lately (drawn as fading tracks)

    PanelKind panel;
    Settlement? at;
    CompanyRecord? upgrading, renaming;
    /// The stack the promotion picker is for (in `upgrading`).
    TroopStack? upStack;
    string renameText = "";
    string tab = "RECRUIT", note = "";
    int speed = 1, lastWeek = -1;
    /// The town tabs a NextStep hint may open on arrival.
    bool follow = true, pendingClick;
    /// Staged place shots: the map alone, no parties and no interface.
    bool bare;
    /// Place shots that show the life about a place (its smoke): a siege, a raid (SM5).
    public bool ShotLife;
    /// The camera starts pulled back far enough to show a province and its neighbours on the (much larger) world map.
    float yaw = MathHelper.PiOver2, pitch = 0.95f, zoom = 170, time;
    Vector2 focus;
    Matrix view, proj;
    Vector3 eye;
    KeyboardState kb, pkb;
    MouseState ms, pms;

    public MapScreen(GraphicsDevice gd, SpriteBatch sb, Art art, PixelFont font, Shading? shading, MeshKit3D? meshKit, Controls ctl)
    {
        this.gd = gd; this.art = art; this.shading = shading; this.ctl = ctl; campKit = meshKit;
        ui = new Ui(sb, art, font);
        puppet = new Puppet(art, sprites) { Scale = 2.5f }; // a little smaller than the towns they ride between (WL3)
        if (meshKit != null) puppet3D = new Puppet3D(meshKit, units3D) { Scale = 2.5f };
        spriteFx = new AlphaTestEffect(gd)
        {
            Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128,
            FogEnabled = true, FogColor = World.Horizon.ToVector3(), FogStart = 260, FogEnd = 700,
        };
        lineFx = new BasicEffect(gd) { VertexColorEnabled = true };
    }

    /// The map's terrain and its world meshes, made without the GPU.
    public sealed record Prepared(Terrain Terrain, World World);

    /// The headless half of `Open`: the map's terrain and world meshes for the campaign's season. Safe off the main thread
    /// (the loading screen runs it beside `Campaign.New`): building the map's meshes takes seconds, and on the main thread
    /// the window froze and looked crashed.
    public static Prepared Prepare(Campaign c)
    {
        c.Map.Season = c.SeasonIndex;
        var terrain = c.Map.BuildTerrain();
        return new Prepared(terrain, new World(terrain, treeScale: MapTreeScale, placeState: id => PlaceStateOf(c, id)));
    }

    /// What a settlement's model shows of it (docs/archive/SETTLEMENTS.md): its holder's colours on its flags (gold for your own fiefs;
    /// villages fly none), its prosperity's band, the works built and its walls' level.
    static PlaceState PlaceStateOf(Campaign c, int id)
    {
        if (id < 0 || id >= c.S.Settlements.Count) return PlaceState.None;
        var s = c.S.Settlements[id];
        int works = 0, besieger = -1, engines = 0;
        for (int i = 0; i < PlaceState.WorkIds.Length; i++) if (Campaign.Level(s, PlaceState.WorkIds[i]) > 0) works |= 1 << i;
        if (s.Besieged && c.S.Parties.FirstOrDefault(p => p.SiegeTarget == s.Id && !p.Down) is Party camp) // SM5: the camp in its colours, its engines
        {
            besieger = Style.PaletteOf(c.SideOf(camp));
            foreach (var w in c.ReadyWorks(camp)) { int e = Array.IndexOf(PlaceState.EngineIds, w); if (e >= 0) engines |= 1 << e; }
        }
        return new PlaceState(s.Kind == SettlementKind.Village ? -1 : s.Owner >= 0 && s.Owner == c.Player.Id ? 2 : Style.PaletteOf(s.Faction),
            PlaceState.WealthOf(s.Prosperity), works, Math.Clamp(Campaign.Level(s, "walls"), 0, 3),
            PlaceState.BatteredOf(s.Battered), besieger, engines, s.Kind == SettlementKind.Village && c.S.Hours - s.RaidedAt < RaidSmokeHours);
    }

    const float MapTreeScale = 0.72f;
    /// How long a raided village shows its burnt houses and smoke (SM5).
    const float RaidSmokeHours = 96;
    int placeTick, smokeSeen;

    /// Show a campaign; `prepared` is its terrain and world from `Prepare` if already built.
    public void Open(Campaign c, Prepared? prepared = null)
    {
        C = c;
        world?.Dispose();
        season = c.SeasonIndex;
        var p = prepared ?? Prepare(c);
        terrain = p.Terrain;
        world = p.World;
        world.Upload(gd);
        FinishLife(); // the worker still on the old map's life must be done first
        life = new MapLife(terrain, terrain.Seed) { Chimneys = world.ChimneysNow, Fires = world.Fires };
        smokeSeen = world.SmokeVersion;
        lock (lifeLock) { lifeMesh = default; lifeFresh = false; }
        atlasIds = null;
        world.SetFog(260, 700);
        bearers.Clear();
        focus = c.Player.Pos;
        follow = true;
        panel = PanelKind.None;
        lastWeek = (int)(c.S.Hours / 168);
    }

    public void ShowResults()
    {
        panel = PanelKind.Results;
        speed = 0;
        if (C.Report is { Won: true }) AudioEngine.Current?.Play(Cue.Coin);
        Tips.Show("results", "After a battle, wounded men heal over the following days (faster resting in a town).\nCompanies that fought gained XP: the bar under each card fills toward the next upgrade.");
    }

    bool Pressed(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);

    // ---------- update ----------

    public void Update(float dt, bool active)
    {
        pkb = kb; pms = ms;
        kb = Keyboard.GetState(); ms = Mouse.GetState();
        hover = default;
        float run = speed == 0 ? 0 : dt; // paused: wind, water, sky, birds, boats and smoke all hold still (PT1, docs/archive/PLAYTEST.md)
        time += run;
        FrameProf.Begin("mapLife");
        UpdateLife(run);
        FrameProf.End("mapLife");
        if (++placeTick % 15 == 0) world?.UpdatePlaces(id => PlaceStateOf(C, id)); // a place that changed hands is rebuilt (World.Places.cs)
        if (world != null && life != null && world.SmokeVersion != smokeSeen) // its chimneys and fires smoke from the new build
        {
            smokeSeen = world.SmokeVersion;
            life.Chimneys = world.ChimneysNow;
            life.Fires = world.Fires;
        }
        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) pendingClick = true;
        if (Pressed(Keys.Enter)) Tips.Dismiss();
        Tips.Show("welcome", $"Welcome, captain. Click the map to march; click a town or village to enter it.\nBrigands are close by: click their banner to ride at them, or let them come to you.\n{Pan()} pans, the wheel zooms, {ctl.First(Act.Pause)} pauses. {ctl.First(Act.PartyScreen)} opens your company. {ctl.First(Act.Help)} lists the keys.");
        if (C.Player.Companies.Any(Rules.UpgradeReady))
            Tips.Show("upgrade", $"Some of your men have the XP for promotion (gold bar, N READY).\nOpen the party screen ({ctl.First(Act.PartyScreen)}), click a troop stack, and choose how many to promote and into what.\nMen rise to tier III anywhere; higher tiers need a town's training yard.");
        if (Skills.PicksTotal(C.Captain) > 0) Tips.Show("perk", $"One of your skills has reached a new mark: choose a perk on its branch ({ctl.First(Act.SkillsScreen)} opens your skills).\nSkills rise by doing: commanding, marching, healing, promoting, fighting, ruling and winning goodwill.");
        if (C.Captain.Renown >= 80) Tips.Show("renown", "Renown is your standing: each victory adds to it, and it raises your men cap.\nThe bar under the captain panel shows the next rise.");
        if (C.Player.Companies.Count >= Rules.CompanySlots(C.Captain)) Tips.Show("slots", $"You have reached the most companies you can lead ({Rules.CompanySlots(C.Captain)}). Merge or disband a company to form a new one.");
        LaterTips();
        // The arcane (docs/archive/ARCANA.md AR1-AR6).
        if (C.Captain.AuraWall) Tips.Show("aura_wall", $"Your aura stands at a wall: it grows no more until a trial breaks it.\nThe captain sheet ({ctl.First(Act.SkillsScreen)}) lists each trial and where to find one now.");
        if (C.Captain.AuraTier >= 1 && C.Captain.Path == "") Tips.Show("aura_path", $"Your aura has woken. Choose its path on the captain sheet ({ctl.First(Act.SkillsScreen)}), once:\nthe Sword (forms), the Banner (your men) or the Hearth (your circle of mages).");
        if (C.Circle is CompanyRecord circle && circle.Doctrine == "") Tips.Show("doctrine", $"Your circle can take a doctrine in its spellbook ({ctl.First(Act.PartyScreen)}, click the circle's roll):\nits spells cost less Will, and it may learn its doctrine's own.");
        if (C.TideAt(C.Player.Pos).Strength >= 2) Tips.Show("wells", $"A well of power reaches here. The realm map ({ctl.First(Act.AtlasScreen)}) shows the wells: towns near them hire out circles,\nand a battle fought near one holds a deeper Tide for every caster on the field.");
        if (C.Day >= 7) Tips.Show("upkeep", "Upkeep is paid each week (the bar at the top). Your contract pays most of it;\nloot, ransoms and bounties pay the rest. Unpaid men desert.");

        if (panel == PanelKind.None)
        {
            if (ctl.Pressed(Act.Menu)) { panel = PanelKind.Menu; return; }
            if (ctl.Pressed(Act.Help)) { panel = PanelKind.Help; return; }
            if (ctl.Pressed(Act.Pause)) speed = speed == 0 ? 1 : 0;
            if (ctl.Pressed(Act.Speed1)) speed = 1;
            if (ctl.Pressed(Act.Speed2)) speed = 3;
            if (ctl.Pressed(Act.Speed3)) speed = 8;
            if (ctl.Pressed(Act.FollowParty)) follow = true;
            if (ctl.Pressed(Act.QuickSave)) Save();
            if (ctl.Pressed(Act.QuickLoad) && !C.S.Ironman) QuickLoad();
            Camera(dt, active);
            hover = active && viewReady && panel == PanelKind.None && !ui.Blocks(ms.Position) ? PickAt(ms.Position, true) : default;
            if (active && viewReady && pendingClick && !ui.Blocks(ms.Position)) { pendingClick = false; ClickWorld(PickAt(ms.Position, false)); }
            FrameProf.Begin("campaignTick");
            if (speed > 0) C.Tick(dt / Balance.Get().HourSeconds * speed);
            FrameProf.End("campaignTick");
            if (C.Reached) { C.Reached = false; speed = 0; }
            if (C.Encounter != null)
            {
                panel = PanelKind.Encounter; note = "";
                Tips.Show("encounter", "An encounter. ATTACK to fight the battle yourself, AUTO-RESOLVE to let your sergeants do it,\nRETREAT to escape (a rearguard pays), or pay brigands to go away.");
            }
            else if (C.Arrived is Settlement s)
            {
                C.Arrived = null;
                if (C.Refuses(s))
                {
                    at = s; note = "";
                    if (s.Kind == SettlementKind.Village) { tab = "FORAGE"; panel = PanelKind.Settlement; }
                    else
                    {
                        panel = PanelKind.Siege; speed = 0;
                        Tips.Show("siege", "An enemy town or castle. BESIEGE it: after a night your men have ladders and a ram.\nThen ASSAULT the walls (climb at the ladders, batter the gate), or wait for the garrison to starve.");
                    }
                }
                else
                {
                    at = s; tab = ""; panel = PanelKind.Settlement; note = ""; // you arrive in the street (MapScreen.Street.cs)
                    Tips.Show("settlement", "Click a building to go in: the barracks trains and hires, the market sells gear, the tavern has news and work, the keep its lord.\nRest, food and captives are on the bar at the bottom. Esc steps back out to the street, then leaves.");
                }
            }
            int week = (int)(C.S.Hours / 168);
            if (week != lastWeek) { lastWeek = week; Write(SaveKind.Auto); }
            if (C.SeasonIndex != season) { var keep = (focus, follow); Open(C); (focus, follow) = keep; C.Log($"{C.Season} comes."); }
            Trails();
        }
        else if (panel == PanelKind.Help && (ctl.Pressed(Act.Help) || ctl.Pressed(Act.Menu))) panel = PanelKind.None;
        else if (panel == PanelKind.Saves) { if (Pressed(Keys.Escape) || !saves.Open) { saves.Close(); panel = PanelKind.Menu; } }
        else if (renaming == null && panel == PanelKind.Settlement && tab != "" && ctl.Pressed(Act.Menu)) { tab = ""; note = ""; } // out of the building, onto the street
        else if (panel == PanelKind.Camp && campMan != null && ctl.Pressed(Act.Menu)) campMan = null; // from a man's page back to the fires
        else if (panel == PanelKind.Fallen && ctl.Pressed(Act.Menu)) panel = bookFrom; // the book closes where it was opened
        else if (renaming == null && ctl.Pressed(Act.Menu) && panel is PanelKind.Party or PanelKind.Menu or PanelKind.Settlement or PanelKind.Upgrade or PanelKind.Siege or PanelKind.Characters or PanelKind.Skills or PanelKind.Realm or PanelKind.Divisions or PanelKind.Atlas or PanelKind.Roll or PanelKind.Quests or PanelKind.Camp)
            panel = panel is PanelKind.Upgrade or PanelKind.Roll ? PanelKind.Party : panel is PanelKind.Party or PanelKind.Menu or PanelKind.Settlement or PanelKind.Siege ? PanelKind.None : CameFrom(panel);
        if (renaming != null) Rename();
        else if (!saves.Typing) Ui.Typed.Clear();
        if (renaming == null && panel != PanelKind.Saves) ScreenKeys();
        // A career beat or the council's word waits for the map to be clear (F5).
        if (panel == PanelKind.None && C.Beat != null) { panel = PanelKind.Beat; speed = 0; }
        if (panel == PanelKind.None && C.S.GameOver) { panel = PanelKind.GameOver; speed = 0; }
        if (panel == PanelKind.None && C.S.Event != null && C.Encounter == null) { panel = PanelKind.Event; speed = 0; note = ""; }
        // You entered a tournament (S5): the fight in the lists starts at once.
        if (panel == PanelKind.None && C.Lists is Settlement lists) { C.Lists = null; speed = 0; OnBattle?.Invoke(C.PrepareTournament(lists)); }
        // ...and so does a knight order's trial of entry (K4).
        if (panel == PanelKind.None && C.TrialPending is string trial) { C.TrialPending = null; speed = 0; OnBattle?.Invoke(C.PrepareTrial(trial)); }
        if (panel == PanelKind.None && C.Besieging is Settlement besieged && C.Player.SiegeTarget >= 0 && !C.Player.Moving && C.Encounter == null && ctl.Pressed(Act.SiegeScreen)) { at = besieged; panel = PanelKind.Siege; }
        if (follow) focus = Vector2.Lerp(focus, C.Player.Pos, MathF.Min(1, dt * 4));
    }

    /// The screens a key opens from the map or from another of these screens, and closes again.
    static readonly (Act Key, PanelKind Panel)[] Screens =
    {
        (Act.PartyScreen, PanelKind.Party), (Act.LordsScreen, PanelKind.Characters), (Act.SkillsScreen, PanelKind.Skills),
        (Act.RealmScreen, PanelKind.Realm), (Act.DivisionsScreen, PanelKind.Divisions), (Act.AtlasScreen, PanelKind.Atlas), (Act.InventoryScreen, PanelKind.Inventory),
        (Act.QuestsScreen, PanelKind.Quests), (Act.CampScreen, PanelKind.Camp),
    };
    /// The screen the current one was opened from (the map or the party screen), where BACK and its key return.
    PanelKind from;

    /// Panels you can leave for another screen without losing a choice (no encounter, town, event or report open).
    bool Browsing => panel is PanelKind.None or PanelKind.Party or PanelKind.Characters or PanelKind.Skills or PanelKind.Realm
        or PanelKind.Divisions or PanelKind.Atlas or PanelKind.Roll or PanelKind.Inventory or PanelKind.Quests or PanelKind.Camp;

    /// One-time tips for the systems a campaign reaches later: named men, divisions, sites, duels, gear, fiefs, fame, retiring.
    void LaterTips()
    {
        string party = ctl.First(Act.PartyScreen);
        if (C.Player.Companies.Any(k => k.Roll.Count > 0))
            Tips.Show("roll", $"Some of your men have earned names. Each company keeps a roll of them: open your company ({party})\nand click ROLL on a card. Named men are hard to kill, and the company remembers the ones it loses.");
        if (C.S.Fallen.Count > 0)
            Tips.Show("fallen", "A named man has fallen. His company's roll keeps him among THE FALLEN,\nand his loss costs the company a little cohesion. Burying him well steadies the men.");
        if (C.S.Divisions.Count == 0 && C.Companions.Any())
            Tips.Show("divisions", $"A companion can lead a division: companies that follow his goal in battle, not your orders.\nOpen DIVISIONS ({ctl.First(Act.DivisionsScreen)}) to make one and give him companies.");
        if (C.S.SitesSeen.Count > 0)
            Tips.Show("site", "A place of note is near (the marks on the map): ruins, shrines, old battlefields, mines, hermits.\nRide to one to see what is there. Some help you, some cost you.");
        if (panel == PanelKind.Encounter && C.Encounter is Party foe && C.CannotChallenge(foe) == null)
            Tips.Show("duel", "This lord will meet you in single combat. CHALLENGE him: win and his men start shaken;\nlose and you fight the battle wounded, from the rear.");
        if (panel == PanelKind.Settlement && tab == "ARMOURER")
            Tips.Show("market", "The armourer sells arms and armour for you.\nClick a piece to see how it compares with what you wear, then buy it and put it on.");
        if (C.Fiefs.Any())
            Tips.Show("fief", "You hold a fief. Its FIEF tab sets the taxes and builds; its GARRISON tab keeps men there.\nHigh taxes and an empty garrison breed unrest, and unrest breeds revolt.");
        if (C.Fame >= 20)
            Tips.Show("fame", "Your company is spoken of. Fame comes from battles and honours: it draws recruits to you,\nand contracts pay more to a company with a name. See THE LEGEND in the realms screen.");
        if (C.CannotRetire() == null)
            Tips.Show("legend", $"A company of yours has made its legend. In THE LEGEND ({ctl.First(Act.RealmScreen)}) you may retire on it and end the\ncampaign as a win, or hand the company to a companion and play on as him.");
    }

    /// The four pan keys as one word ("WASD").
    string Pan() => $"{ctl.First(Act.Forward)}{ctl.First(Act.Left)}{ctl.First(Act.Back)}{ctl.First(Act.Right)}";

    void ScreenKeys()
    {
        if (!Browsing || panel == PanelKind.None && (C.Encounter != null || C.Beat != null || C.S.Event != null)) return;
        foreach (var (key, kind) in Screens)
            if (ctl.Pressed(key)) { if (panel == kind) panel = CameFrom(kind); else OpenScreen(kind); return; }
    }

    /// Open a screen, remembering where from (the party screen is remembered; other screens go back to the map).
    void OpenScreen(PanelKind kind)
    {
        from = panel is PanelKind.Party or PanelKind.Settlement ? panel : PanelKind.None; // the way back: the party screen, or the place (its REST tab opens the camp)
        panel = kind; note = "";
    }

    /// The map's ambience: wind, birdsong by day and crickets by night (hushed by rain), the bustle of a town as you
    /// come near one; and a page turning whenever a screen opens or closes.
    public void Soundscape(AudioEngine? audio, float dt = 1 / 60f)
    {
        if (audio == null) return;
        if (panel != heardPanel)
        {
            if (heardPanel != PanelKind.Menu || panel != PanelKind.None)
                audio.Play(panel is PanelKind.Atlas or PanelKind.Roll or PanelKind.Event ? Cue.ScrollUnroll : Cue.Page, 0.7f);
            heardPanel = panel;
        }
        float day = 1 - Sky.Night, dry = 1 - MathF.Min(1, Sky.Rain * 3);
        bool winter = C.Map.Season == 3;
        var prov = C.Map.ProvinceAt(C.Player.Pos);
        string culture = prov.Faction >= 0 && prov.Faction < C.S.Factions.Count ? C.S.Factions[prov.Faction].Culture : "azure";
        float town = 0;
        foreach (var s in C.S.Settlements)
        {
            float d = Vector2.Distance(s.Pos, C.Player.Pos), size = s.Kind == SettlementKind.Village ? 6 : 10;
            town = MathF.Max(town, 1 - d / size);
        }
        float river = Math.Clamp(1f - C.Map.RiverDist(C.Player.Pos) / 14f, 0f, 1f);
        float graveNear = 0;
        foreach (var g in C.S.Graves) graveNear = MathF.Max(graveNear, Math.Clamp(1f - Vector2.Distance(g.Pos, C.Player.Pos) / 14f, 0f, 1f));
        float omen = MathF.Max(graveNear, Math.Clamp(MathF.Max(C.S.Omen - 15f, C.Captain.Corruption - 25f) / 35f, 0f, 0.85f));
        bool feastOn = C.S.Flags.TryGetValue("hall_feast", out float fh) && C.S.Hours - fh < 24;
        bool inTavern = panel == PanelKind.Settlement && tab == "TAVERN" || panel == PanelKind.Talk && feastOn;
        bool inForge = panel == PanelKind.Settlement && tab is "MARKET" or "ARMOURER" or "TRAIN";

        bool hostNear = false;
        foreach (var p in C.S.Parties)
            if (p.Kind == PartyKind.Lord && p.Id != C.Player.Id && C.Hostile(C.Player, p) && Vector2.DistanceSquared(p.Pos, C.Player.Pos) < 24f * 24f) { hostNear = true; break; } // an enemy lord's host, not brigands

        string? interior = panel switch
        {
            PanelKind.GameOver => "chronicle_end",
            PanelKind.Talk => feastOn ? "hall_feast" : "lords_hall",
            PanelKind.Event when C.S.Event is { Site: >= 0 } => "sacred_site",
            PanelKind.Settlement when tab == "TAVERN" => ((at?.Id ?? 0) % 3) switch { 0 => "tavern_reel_1", 1 => "tavern_reel_2", _ => "tavern_ballad" },
            PanelKind.Settlement when tab == "HALL" => feastOn ? "hall_feast" : "lords_hall",
            PanelKind.Settlement when tab == "CHAPTER" => "chapter_house",
            PanelKind.Settlement => "town_market",
            _ => null,
        };
        float dreadScore = C.Captain.Corruption >= 50 || C.Captain.Pact ? 0.85f : omen;
        audio.UpdateMapMusic(interior, culture, Sky.Night > 0.5f, winter, hostNear, dreadScore, dt);

        audio.SetBed(Bed.Wind, winter ? 0.45f : 0.75f + 0.4f * Sky.Rain);
        if (winter) audio.SetBed(Bed.Blizzard, 0.55f + 0.35f * Sky.Rain);
        else audio.SetBed(Bed.Rain, MathF.Min(1, Sky.Rain * 2.5f));
        audio.SetBed(Bed.Birds, winter ? 0 : day * dry * (1 - town * 0.5f));
        audio.SetBed(Bed.Crickets, winter ? 0 : Sky.Night * dry);
        audio.SetBed(Bed.Town, town * (0.4f + 0.6f * day));
        if (river > 0.02f) audio.SetBed(Bed.River, river * 0.45f);
        if (prov.Kind == "Marsh" || culture == "league" || prov.Kind == "Coast") audio.SetBed(Bed.Surf, prov.Kind == "Marsh" ? 0.55f : prov.Kind == "Coast" ? 0.45f : 0.35f);
        if (prov.Kind is "Forest" or "Taiga") audio.SetBed(Bed.Forest, 0.55f * (1 - town * 0.5f));
        if (omen > 0.02f) audio.SetBed(Bed.OmenDrone, omen);
        if (inTavern) audio.SetBed(Bed.Tavern, 0.85f);
        if (inForge) audio.SetBed(Bed.Forge, 0.75f);
        if (panel == PanelKind.None && C.Player.Moving && speed > 0) audio.SetBed(Bed.March, 0.32f);
    }
    PanelKind heardPanel;

    /// Where closing `kind` goes: back to the party screen if it was opened from there.
    PanelKind CameFrom(PanelKind kind) => from != PanelKind.None && kind != from ? from : PanelKind.None;

    void Camera(float dt, bool active)
    {
        var f = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
        var right = new Vector2(-f.Y, f.X);
        var pan = f * ctl.Axis(Act.Forward, Act.Back) + right * ctl.Axis(Act.Right, Act.Left);
        if (active && gd.Viewport.Bounds.Contains(ms.Position))
        {
            const int edge = 6;
            if (ms.X < edge) pan -= right; else if (ms.X > gd.Viewport.Width - edge) pan += right;
            if (ms.Y < edge) pan += f; else if (ms.Y > gd.Viewport.Height - edge) pan -= f;
        }
        if (pan != Vector2.Zero) { follow = false; focus += pan * zoom * 0.9f * dt; }
        if (!(follow && C.Sailing)) focus = C.Map.InLand(focus); // the view keeps to the land, but follows your ship out
        if (!saves.Typing) yaw += ctl.Axis(Act.RotateRight, Act.RotateLeft) * 1.6f * dt; // Q and E are letters in a save's name
        if (ms.MiddleButton == ButtonState.Pressed && pms.MiddleButton == ButtonState.Pressed)
        {
            yaw += (ms.X - pms.X) * 0.006f;
            pitch = Math.Clamp(pitch + (ms.Y - pms.Y) * 0.006f, 0.35f, 1.45f);
        }
        // The map is much bigger than a battlefield: the camera pulls back far enough to see a province and its neighbours.
        zoom = Math.Clamp(zoom - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.08f, 25, 620);
    }

    /// Typing a company's new name: Enter keeps it, Escape drops it.
    void Rename()
    {
        while (Ui.Typed.Count > 0)
        {
            char ch = Ui.Typed.Dequeue();
            if (ch == '\b') { if (renameText.Length > 0) renameText = renameText[..^1]; }
            else if (ch is '\r' or '\n') { if (renameText.Trim().Length > 0) renaming!.Name = renameText.Trim(); renaming = null; return; }
            else if (ch == (char)27) { renaming = null; return; }
            else if (renameText.Length < 22 && (char.IsLetterOrDigit(ch) || ch is ' ' or '\'' or '-')) renameText += char.ToUpperInvariant(ch);
        }
        if (Pressed(Keys.Escape)) renaming = null;
    }

    void Trails()
    {
        foreach (var p in C.S.Parties)
        {
            if (!trails.TryGetValue(p.Id, out var t)) trails[p.Id] = t = new List<Vector2> { p.Pos };
            if (Vector2.Distance(t[^1], p.Pos) > 2.5f) { t.Add(p.Pos); if (t.Count > 14) t.RemoveAt(0); }
            if (p.Down) t.Clear();
            if (t.Count == 0) t.Add(p.Pos);
        }
        foreach (int id in trails.Keys.Where(id => C.PartyById(id) == null).ToList()) trails.Remove(id);
    }

    /// What is under the mouse on the map: a party, a town, a ruin or the bare ground (PT1, docs/archive/PLAYTEST.md).
    readonly record struct MapPick(Party? Party, Settlement? Town, Site? Site, Vector2 Ground);

    MapPick hover;
    bool viewReady;

    /// A world point on screen, with how many pixels a world unit is there. False when it is behind the camera.
    bool OnScreen(Vector2 p, out Vector2 sp, out float pxPerUnit)
    {
        var vp = gd.Viewport;
        float h = Surface(p) + 1;
        var a = vp.Project(new Vector3(p.X, h, p.Y), proj, view, Matrix.Identity);
        var bx = vp.Project(new Vector3(p.X + 1, h, p.Y), proj, view, Matrix.Identity);
        var bz = vp.Project(new Vector3(p.X, h, p.Y + 1), proj, view, Matrix.Identity);
        sp = new Vector2(a.X, a.Y);
        pxPerUnit = MathF.Max(Vector2.Distance(sp, new Vector2(bx.X, bx.Y)), Vector2.Distance(sp, new Vector2(bz.X, bz.Y)));
        return a.Z is > 0 and < 1;
    }

    /// The things to click are picked on screen, not on the ground: a town, a party or a ruin counts when the mouse is within
    /// its drawn size (at least a thumb's width, so it works zoomed far out), and the one the mouse is nearest to wins. Click,
    /// tooltip, cursor and the hover ring all read this one pick.
    MapPick PickAt(Point m, bool includePlayer)
    {
        var g = Ground(m);
        var mv = new Vector2(m.X, m.Y);
        Party? party = null; Settlement? town = null; Site? site = null;
        float best = 1;
        foreach (var q in C.S.Parties)
        {
            if (q.Down || !includePlayer && q == C.Player || !OnScreen(q.Pos, out var sp, out var k)) continue;
            float d = Vector2.Distance(sp, mv) / MathF.Max(22, 3 * k);
            if (d < best) { best = d; party = q; }
        }
        if (party != null) return new MapPick(party, null, null, g);
        best = 1;
        foreach (var t in C.S.Settlements)
        {
            if (!OnScreen(t.Pos, out var sp, out var k)) continue;
            float d = Vector2.Distance(sp, mv) / MathF.Max(28, k * (t.Kind == SettlementKind.Village ? 6 : t.Kind == SettlementKind.Castle ? 8 : 10));
            if (d < best) { best = d; town = t; }
        }
        if (town != null) return new MapPick(null, town, null, g);
        best = 1;
        foreach (var st in C.Map.Sites)
        {
            if (!OnScreen(st.Pos, out var sp, out var k)) continue;
            float d = Vector2.Distance(sp, mv) / MathF.Max(22, 3 * k);
            if (d < best) { best = d; site = st; }
        }
        return new MapPick(null, null, site, g);
    }

    void ClickWorld(MapPick pick)
    {
        if (pick.Party != null) C.Chase(pick.Party);
        else if (pick.Town != null) C.GoTo(pick.Town);
        else if (pick.Site != null) C.GoTo(pick.Site);
        else C.MoveTo(pick.Ground);
        if (speed == 0) speed = 1;
    }

    Vector2 Ground(Point m)
    {
        var vp = gd.Viewport;
        var near = vp.Unproject(new Vector3(m.X, m.Y, 0), proj, view, Matrix.Identity);
        var far = vp.Unproject(new Vector3(m.X, m.Y, 1), proj, view, Matrix.Identity);
        if (!float.IsFinite(near.X) || !float.IsFinite(near.Y) || !float.IsFinite(near.Z)
            || !float.IsFinite(far.X) || !float.IsFinite(far.Y) || !float.IsFinite(far.Z)
            || (far - near).LengthSquared() < 1e-8f)
            return focus;
        return GroundPick.March(near, far, terrain, 1200, 1500) ?? focus;
    }

    // ---------- drawing ----------

    public void Draw(Quality quality)
    {
        // Day and night on the map follow the campaign clock (kept readable at night); autumn and winter skies are greyer.
        // Winter tells the shaders it snows: fields, hedges, tree tops and roofs whiten and the water freezes (WL3).
        Looks.Grounded = true;
        Sky.Set(new Weather((float)(C.S.Hours % 24), C.SeasonIndex >= 2 ? 0.25f : 0, Snow: C.SeasonIndex == 3), nightFloor: 0.6f);
        var look = new Vector3(MathF.Cos(yaw) * MathF.Cos(pitch), -MathF.Sin(pitch), MathF.Sin(yaw) * MathF.Cos(pitch));
        var f3 = new Vector3(focus.X, Surface(focus), focus.Y); // at sea the view rests on the water, not the sea bed
        // The camera keeps its zoom and height at the edge of the world: only where its orbit would carry it off the ground
        // (out past the mountain rim) is it held over the rim's edge, so it looks down more steeply instead of zooming in.
        eye = f3 - look * zoom;
        float hold = C.Map.GroundHalf - 4;
        eye.X = Math.Clamp(eye.X, -hold, hold);
        eye.Z = Math.Clamp(eye.Z, -hold, hold);
        eye.Y = MathF.Max(eye.Y, terrain.Height(new Vector2(eye.X, eye.Z)) + 2);
        // Pulled back for a big map, the camera must still see the ground under it: the far plane and fog follow the zoom.
        // The near plane follows the zoom too: trees right by the camera are clipped rather than filling the screen.
        proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45), gd.Viewport.AspectRatio, Math.Clamp(zoom * 0.35f, 0.5f, 40), MathF.Max(1600, zoom * 3.2f));
        view = Matrix.CreateLookAt(eye, f3, Vector3.Up);
        viewReady = true;
        FrameProf.Begin("bearersUpd");
        UpdateBearers();
        FrameProf.End("bearersUpd");
        // Fog starts beyond the near ground in view, so a wide view is hazy at the horizon rather than cut off.
        float fogStart = MathF.Max(200, zoom * 1.9f), fogEnd = MathF.Max(600, zoom * 5.2f);
        world!.SetFog(fogStart, fogEnd);
        spriteFx.FogStart = fogStart;
        spriteFx.FogEnd = fogEnd;

        bool shaded = shading != null && quality != Quality.Low;
        lifeQuality = quality;
        
        if (shaded && life != null)
        {
            (VertexPNCT[] V, int Tris) made = default;
            bool fresh;
            lock (lifeLock) { fresh = lifeFresh; made = lifeMesh; lifeFresh = false; }
            if (fresh) world.SetExtra(made.V, made.Tris);
        }
        else world.ExtraTris = 0;
        if (bare && !ShotLife) world.ExtraTris = 0; // place shots: the models alone, no beasts, folk or smoke
        if (shaded)
        {
            var sh = shading!;
            bool shadows = quality == Quality.High;
            bool do3D = Settings.Current.EffectivePixelArt && puppet3D != null;
            int pScale = Settings.Current.EffectivePixelScale;
            var fog = sh.Fog;
            sh.Fog = new Vector2(fogStart, fogEnd);
            if (shadows)
            {
                FrameProf.Begin("worldShadow");
                sh.AimSun(f3, Math.Clamp(zoom * 2.4f, 80, 420));
                gd.Textures[1] = null;
                gd.SetRenderTarget(sh.ShadowMap);
                gd.Clear(Color.White);
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                world!.DrawShadowCasters(sh, time);
                FrameProf.End("worldShadow");
                if (do3D)
                {
                    FrameProf.Begin("u3dShadowBuild");
                    BuildUnits3D(sh.LightView, sh.LightViewProj, forSun: true);
                    FrameProf.End("u3dShadowBuild");
                    if (units3D.N > 0)
                    {
                        FrameProf.Begin("u3dShadowDraw");
                        sh.Setup(sh.Unit3D, Matrix.Identity, sh.LightEye, time, false, 0);
                        sh.Unit3D.Parameters["ShadowMap"].SetValue((Texture2D?)null);
                        sh.Unit3D.Parameters["View"].SetValue(sh.LightView);
                        DrawUnits3D(sh.Unit3D, "Unit3DShadow");
                        FrameProf.End("u3dShadowDraw");
                    }
                }
                else
                {
                    BuildSprites(sh.LightView);
                    sh.Setup(sh.Sprite, Matrix.Identity, sh.LightEye, time, false, 0);
                    sh.Sprite.Parameters["ShadowMap"].SetValue((Texture2D?)null);
                    DrawSprites(sh.Sprite, "SpriteShadow");
                }
            }
            gd.Textures[1] = null;
            if (do3D)
            {
                FrameProf.Begin("worldDraw");
                var (pCol, pNd) = sh.PixelTargets(pScale);
                gd.SetRenderTarget(pNd);
                gd.Clear(Color.Transparent);
                gd.SetRenderTarget(pCol);
                gd.Clear(World.Horizon);
                world!.DrawShaded(sh, view, proj, eye, time, shadows);
                FrameProf.End("worldDraw");
                FrameProf.Begin("u3dBuild");
                BuildUnits3D(view, view * proj, forSun: false);
                FrameProf.End("u3dBuild");
                if (units3D.N > 0)
                {
                    FrameProf.Begin("u3dDraw");
                    gd.SetRenderTargets(pCol, pNd);
                    sh.Setup(sh.Unit3D, view * proj, eye, time, shadows, 1.2f);
                    sh.Unit3D.Parameters["View"].SetValue(view);
                    DrawUnits3D(sh.Unit3D, "Unit3D");
                    gd.SetRenderTarget(pCol);
                    FrameProf.End("u3dDraw");
                }
                FrameProf.Begin("linesAndPost");
                DrawLines();
                sh.PixelPostToScreen(pScale, Sky.Grade);
                FrameProf.End("linesAndPost");
                if (FrameProf.On) { FrameProf.Count("bearers", bearers.Count); FrameProf.Count("u3dTris", units3D.N / 3); }
            }
            else
            {
                FrameProf.Begin("worldDraw");
                gd.SetRenderTarget(sh.Scene());
                gd.Clear(World.Horizon);
                world!.DrawShaded(sh, view, proj, eye, time, shadows);
                FrameProf.End("worldDraw");
                BuildSprites(view);
                FrameProf.Begin("spritesDraw");
                sh.Setup(sh.Sprite, view * proj, eye, time, shadows, 1.2f);
                DrawSprites(sh.Sprite, "Sprite");
                FrameProf.End("spritesDraw");
                FrameProf.Begin("linesAndPost");
                DrawLines();
                sh.PostToScreen(Settings.Current.Fxaa, Sky.Grade);
                FrameProf.End("linesAndPost");
            }
            sh.Fog = fog;
        }
        else
        {
            gd.Clear(World.Horizon);
            world!.Draw(view, proj, eye);
            BuildSprites(view);
            if (sprites.N > 0)
            {
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.SamplerStates[0] = SamplerState.PointClamp;
                spriteFx.View = view; spriteFx.Projection = proj;
                spriteFx.CurrentTechnique.Passes[0].Apply();
                sprites.Draw(gd, PrimitiveType.TriangleList, sprites.N / 3);
            }
            DrawLines();
        }
        if (thumbFor != null) GrabThumb(); // the map is drawn and the interface not yet
        PrepareCamp(); // the camp's men, into their texture before the interface's sprites begin
        FrameProf.Begin("drawUi");
        long calls0 = FrameProf.On ? gd.Metrics.DrawCount : 0, sprites0 = FrameProf.On ? gd.Metrics.SpriteCount : 0;
        DrawUi();
        if (FrameProf.On) { FrameProf.Count("uiDrawCalls", gd.Metrics.DrawCount - calls0); FrameProf.Count("uiSprites", gd.Metrics.SpriteCount - sprites0); }
        FrameProf.End("drawUi");
    }

    /// Each party is drawn as its leader on horseback (brigands and goblins on foot), three times life size.
    void UpdateBearers()
    {
        bearerSeen.Clear();
        foreach (var p in C.S.Parties)
        {
            if (p.Lair && p.Breed == "goblin") { bearers.Remove(p.Id); continue; }
            bearerSeen.Add(p.Id);
            if (!bearers.TryGetValue(p.Id, out var u))
            {
                var type = p.Kind switch
                {
                    PartyKind.Player => Rules.CaptainType(C.Captain),
                    PartyKind.Bandit => Troops.Get("levies"),
                    PartyKind.Caravan => Troops.Get(p.Breed == "pilgrims" ? "levies" : "outriders"),
                    PartyKind.Monster => Troops.Get(Campaign.MonsterLeader(p) is { Length: > 0 } lead ? lead : "goblin"), // the beast that leads it
                    _ => Troops.Get(p.Tier >= 3 ? "knights" : "captain"),
                };
                u = new Unit
                {
                    Type = type, Team = 1, IsPlayer = p.Kind == PartyKind.Player, Palette = Style.PaletteOf(C.SideOf(p)),
                    Coat = p.Id % Style.Coats.Length, Facing = p.Facing, ViewFacing = p.Facing, Heading = p.Facing, ViewHeading = p.Facing,
                };
                if (type.Flying) u.Altitude = 1.2f; // a wyvern on the wing
                if (!Campaign.Outlaw(p) && p.Breed != "pilgrims") { u.Horse = p.Kind == PartyKind.Player ? Rules.CaptainMount(C.Captain) : type.Mount ?? Mount.Courser; u.HorseHp = u.Horse.Hp; }
                bearers[p.Id] = u;
            }
            // A party in a town stands before its gate (on the near side as the map first faces), not on the roofs.
            var at = p.Pos;
            if (!p.Moving && C.SettlementAt(p.Pos, 3) is { } s) at = C.Map.Plans.TryGetValue(s.Id, out var plan) ? plan.Doorstep : s.Pos + new Vector2(0, s.Kind == SettlementKind.Village ? -5.5f : -9.5f);
            var moved = at - u.Pos;
            bool moving = moved.LengthSquared() > 1e-6f && moved.LengthSquared() < 4;
            u.Pos = at;
            u.Facing = u.Heading = p.Facing;
            u.ViewFacing += MathHelper.WrapAngle(u.Facing - u.ViewFacing) * 0.2f;
            u.ViewHeading = u.ViewFacing;
            u.Speed = moving ? 6 : 0;
            u.Vel = moving ? new Vector2(MathF.Cos(p.Facing), MathF.Sin(p.Facing)) * 3 : Vector2.Zero;
            u.Anim += moving ? 0.08f : 0;
            u.Dead = false;
            u.Fled = p.Down || p.Voyage.Count > 0; // at sea the ship is drawn, not the rider (MapLife.Ship)
        }
        bearerDrop.Clear();
        foreach (int id in bearers.Keys) if (!bearerSeen.Contains(id)) bearerDrop.Add(id);
        foreach (int id in bearerDrop) bearers.Remove(id);
    }

    /// The map's birds and beasts move on (they flee the parties near them; crows find the dead). Their step and their mesh
    /// are made on a worker thread from a snapshot of what they read, one frame behind: the main thread only uploads the
    /// finished mesh (`Draw`). Staged shots run it inline so a shot is the same every time.
    Task? lifeTask;
    (VertexPNCT[] V, int Tris) lifeMesh;
    bool lifeFresh;
    float lifeDt;
    (Vector2, float, Quality) lifeView;
    readonly object lifeLock = new();

    /// Wait for the worker (before the life it works on is replaced) and keep what it made.
    void FinishLife()
    {
        if (lifeTask == null) return;
        try { lifeTask.Wait(); } catch (AggregateException e) { Console.WriteLine("map life failed: " + e.InnerException); }
        lifeTask = null;
    }

    void UpdateLife(float dt)
    {
        if (life == null || C == null) return;
        lifeDt += dt;
        // Paused and the camera has not moved: nothing in the life would change, so do no work at all.
        if (dt == 0 && lifeDt == 0 && lifeFresh == false && lifeView == (focus, zoom, lifeQuality) && lifeTask is null or { IsCompleted: true, IsFaulted: false }) return;
        lifeView = (focus, zoom, lifeQuality);
        if (lifeTask is { IsCompleted: false }) return; // still at it: its step covers the time we add up meanwhile
        if (lifeTask?.IsFaulted == true) FinishLife(); // report the failure once, then carry on
        lifeTask = null;
        var spots = new List<Vector2>(partySpots.Capacity);
        foreach (var p in C.S.Parties) if (!p.Down) spots.Add(p.Pos);
        var carrion = C.S.Graves.Select(g => g.Pos).Concat(C.Map.Sites.Where(s => s.Kind == "battlefield").Select(s => s.Pos)).ToList();
        var caravans = C.S.Parties.Where(p => p.Kind == PartyKind.Caravan && !p.Down).Select(p => (p.Id, p.Pos, p.Facing, p.Breed == "pilgrims")).ToList();
        (Vector2, float, Color)? ship = C.Sailing ? (C.Player.Pos, C.Player.Facing, Style.Sides[Style.PaletteOf(C.SideOf(C.Player))].Cloth) : null;
        var holdouts = C.S.Parties.Where(p => p.Lair && !p.Down && p.Breed == "goblin").Select(p => (p.Id, p.Pos, p.Men, p.Tier)).ToList();
        var cloth = Style.Sides[Style.PaletteOf(C.SideOf(C.Player))].Cloth;
        var cairns = C.S.Cairns.Select(c => (c.Id, c.Pos, cloth)).ToList();
        var (step, at, zm, hour, quality, shaded) = (lifeDt, focus, zoom, (float)(C.S.Hours % 24), lifeQuality, shading != null && lifeQuality != Quality.Low);
        var mapLife = life;
        lifeDt = 0;
        void Work()
        {
            mapLife.SetCaravans(caravans);
            mapLife.SetHoldouts(holdouts);
            mapLife.SetCairns(cairns);
            mapLife.Ship = ship;
            mapLife.Update(step, at, zm, spots, carrion, hour, quality);
            if (shaded) { var built = mapLife.Build(); lock (lifeLock) { lifeMesh = built; lifeFresh = true; } }
        }
        if (Ui.Instant) { Work(); return; }
        lifeTask = Task.Run(Work);
    }

    void BuildSprites(Matrix viewer)
    {
        FrameProf.Begin("sprites");
        BuildSpritesInner(viewer);
        FrameProf.End("sprites"); // summed over the frame's passes? no: the last call only
    }

    void BuildSpritesInner(Matrix viewer)
    {
        sprites.N = 0;
        puppet.Begin(viewer);
        if (!bare) foreach (var u in bearers.Values) if (!u.Fled) puppet.Draw(u, terrain.Height(u.Pos));
    }

    void BuildUnits3D(Matrix viewer, Matrix frustumVp, bool forSun)
    {
        FrameProf.Begin("sprites");
        units3D.N = 0;
        unitFrustum.Matrix = frustumVp;
        puppet3D!.Now = time;
        puppet3D.BeginCamera(viewer);
        float near2 = 120f * 120f, mid2 = 260f * 260f;
        int drawn = 0;
        foreach (var u in bearers.Values)
        {
            if (u.Fled) continue;
            float y = terrain.Height(u.Pos);
            var center = new Vector3(u.Pos.X, y + 2.5f, u.Pos.Y);
            if (unitFrustum.Contains(new BoundingSphere(center, 8f)) == ContainmentType.Disjoint) continue;
            float d2 = Vector3.DistanceSquared(center, eye);
            puppet3D.Detail = forSun ? (d2 < near2 ? 1 : 2) : (d2 < near2 ? 0 : d2 < mid2 ? 1 : 2);
            puppet3D.TryDraw(u, y);
            drawn++;
        }
        if (FrameProf.On && !forSun) FrameProf.Count("u3dDrawn", drawn);
        FrameProf.End("sprites");
    }

    void DrawUnits3D(Effect fx, string technique)
    {
        if (units3D.N == 0) return;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
        fx.CurrentTechnique = fx.Techniques[technique];
        fx.CurrentTechnique.Passes[0].Apply();
        units3D.Draw(gd, PrimitiveType.TriangleList, units3D.N / 3);
    }

    void DrawSprites(Effect fx, string technique)
    {
        if (sprites.N == 0) return;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
        fx.Parameters["Atlas"].SetValue(art.Atlas);
        fx.CurrentTechnique = fx.Techniques[technique];
        fx.CurrentTechnique.Passes[0].Apply();
        sprites.Draw(gd, PrimitiveType.TriangleList, sprites.N / 3);
    }

    /// The ground, or the sea's surface over it: what a mark or a label out on the water stands on.
    float Surface(Vector2 p) => MathF.Max(terrain.Height(p), terrain.SeaLevel ?? float.MinValue);

    Vector3 G(Vector2 p, float y = 0.6f) => new(p.X, Surface(p) + y, p.Y);

    void DrawLines()
    {
        lines.N = 0;
        void Line(Vector3 a, Vector3 b, Color c) { lines.Add(new(a, c)); lines.Add(new(b, c)); }
        void Ring(Vector2 p, float r, Color c)
        {
            for (int i = 0; i < 16; i++)
            {
                float a0 = i * MathF.Tau / 16, a1 = (i + 1) * MathF.Tau / 16;
                Line(G(p + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r), G(p + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r), c);
            }
        }
        // The town or ruin the next click would go to gets a ring, so a click that is not on its centre still reads as aimed.
        if (hover.Party == null && (hover.Town?.Pos ?? hover.Site?.Pos) is Vector2 ringAt) Ring(ringAt, hover.Town is { Kind: SettlementKind.Village } ? 6 : hover.Town != null ? 10 : 4, Style.Accent);
        // Tracks: dotted lines behind every party, toward its current position.
        foreach (var (id, track) in trails)
        {
            if (C.PartyById(id) is not Party p || p.Down) continue;
            var col = PartyColor(p) * 0.55f;
            for (int i = 0; i + 1 < track.Count; i++)
                Line(G(track[i], 0.3f), G(Vector2.Lerp(track[i], track[i + 1], 0.5f), 0.3f), Color.Lerp(Color.Black, col, (i + 1f) / track.Count));
        }
        var pl = C.Player;
        Vector2? target = pl.ChaseId >= 0 ? C.PartyById(pl.ChaseId)?.Pos : pl.Moving ? pl.Goal : null;
        if (target is Vector2 t)
        {
            // Dashes along the way the party will take (its route round rivers and lakes), or straight at a quarry.
            var from = pl.Pos;
            var legs = pl.ChaseId < 0 && C.PlayerRoute.Count > 0 ? C.PlayerRoute : new[] { t };
            foreach (var to in legs)
            {
                int n = (int)(Vector2.Distance(from, to) / 3) + 1;
                for (int i = 0; i < n; i += 2) Line(G(Vector2.Lerp(from, to, i / (float)n)), G(Vector2.Lerp(from, to, (i + 1) / (float)n)), Style.Accent);
                from = to;
            }
            Ring(t, 2, pl.ChaseId >= 0 ? Style.Foe : Style.Accent);
        }
        Ring(pl.Pos, 3, Style.Accent);
        if (lines.N == 0) return;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        lineFx.View = view; lineFx.Projection = proj;
        lineFx.CurrentTechnique.Passes[0].Apply();
        lines.Draw(gd, PrimitiveType.LineList, lines.N / 2);
    }

    // ---------- debug staging (IRONFIELD_CAMPAIGN_SHOTS) ----------

    Vector2 shotHome;

    public void Stage(string what, Settlement? s = null)
    {
        hover = default; viewReady = false; // the camera jumps: no picking through the old view until Draw makes the new one
        if (what != "sail" && C.Sailing) { C.Player.Voyage.Clear(); C.Player.Pos = shotHome; C.Player.Moving = false; } // back ashore after the "sail" shot
        switch (what)
        {
            case "port": // a port's quay: its pier and the ship lying at it (docs/archive/WORLDMAP.md WM7)
            {
                var port = C.S.Settlements.Where(C.IsPort).OrderBy(t => Vector2.Distance(t.Pos, C.Map.Quays[t.Id].Berth)).First();
                panel = PanelKind.None; follow = false; zoom = 60; pitch = 0.7f; focus = C.Map.Quays[port.Id].Shore;
                break;
            }
            case "sail": // the company at sea, just out of port
            {
                var lane = C.Map.SeaLanes.OrderBy(l => Vector2.Distance(C.Map.Settlements[l.A].Pos, C.Map.Quays[l.A].Berth)).First();
                shotHome = C.Player.Pos;
                C.Player.Voyage = lane.Way.Skip(1).ToList(); C.Player.VoyageTo = lane.B;
                C.Player.Pos = lane.Way[0] + Vector2.Normalize(lane.Way[1] - lane.Way[0]) * 26; C.Player.Facing = MathF.Atan2(lane.Way[1].Y - lane.Way[0].Y, lane.Way[1].X - lane.Way[0].X);
                C.Player.Goal = C.Map.Settlements[lane.B].Pos; C.Player.Moving = true; C.Player.ChaseId = -1;
                panel = PanelKind.None; follow = true; zoom = 46; pitch = 0.7f; focus = C.Player.Pos; // following: the view leaves the land only after your ship
                break;
            }
            case "harbour": // the HARBOUR tab of a port that will have you
                at = C.S.Settlements.Where(t => C.IsPort(t) && !C.Refuses(t)).OrderByDescending(t => C.Sailings(t).Count()).First();
                C.Player.Pos = at.Pos; C.Player.Moving = false; tab = "HARBOUR"; panel = PanelKind.Settlement; note = ""; break;
            case "siege": at = s; panel = PanelKind.Siege; break;
            case "contract":
                at = C.S.Settlements.First(t => t.Kind == SettlementKind.Town && t.Faction == C.Captain.Contract);
                tab = "CONTRACT"; panel = PanelKind.Settlement; break;
            case "map": panel = PanelKind.None; zoom = 330; pitch = 0.95f; break;
            case "saveas": saves.Show(SaveScreen.Use.Save, C.S); panel = PanelKind.Saves; break;
            case "load": saves.Show(SaveScreen.Use.Load, C.S); panel = PanelKind.Saves; break;
            case "close": panel = PanelKind.None; zoom = 60; pitch = 0.7f; break;
            case "place" when s != null: // a settlement close up, from before its gate a little aside (docs/archive/SETTLEMENTS.md)
            {
                panel = PanelKind.None; follow = false; bare = true; focus = s.Pos; pitch = 0.6f;
                zoom = s.Kind == SettlementKind.Town ? 42 : s.Kind == SettlementKind.Village ? 28 : s.Size switch { 1 => 24, 3 => 40, _ => 31 };
                if (C.Map.Plans.TryGetValue(s.Id, out var plan)) yaw = plan.Front + MathF.PI + 0.55f;
                world?.UpdatePlaces(id => PlaceStateOf(C, id), wait: true); // the shots change a place's state (SM4): show it now
                break;
            }
            case "life": // a pasture and its beasts up close (WL4)
                panel = PanelKind.None; follow = false; zoom = 46; pitch = 0.72f;
                focus = terrain.Fields.Where(f => f.Kind == FieldKind.Pasture).OrderBy(f => Vector2.Distance(f.Center, C.Player.Pos)).Select(f => f.Center).DefaultIfEmpty(C.Player.Pos).First();
                break;
            case "farm": // the fields round a village (WL3)
                panel = PanelKind.None; follow = false; zoom = 95; pitch = 0.85f;
                focus = C.S.Settlements.Where(s => s.Kind == SettlementKind.Village).OrderBy(s => Vector2.Distance(s.Pos, C.Player.Pos)).First().Pos;
                break;
            case "holds": // the nearest tower house to the player (docs/archive/HOLDS.md HD5)
                panel = PanelKind.None; follow = false; zoom = 70; pitch = 0.72f;
                focus = C.S.Settlements.Where(s => s.Size == 1).OrderBy(s => Vector2.Distance(s.Pos, C.Player.Pos)).Select(s => s.Pos).DefaultIfEmpty(C.Player.Pos).First();
                break;
            case "village": // folk about a village, smoke from its chimneys (WL5); "night" is the same at lamplight
            case "night":
                panel = PanelKind.None; follow = false; zoom = 28; pitch = 0.6f;
                focus = C.S.Settlements.Where(s => s.Kind == SettlementKind.Village).OrderBy(s => Vector2.Distance(s.Pos, C.Player.Pos)).First().Pos + new Vector2(0, 6);
                break;
            case "road": // traffic on the roads: a caravan's wagons if one is near, else the busiest road by the player (WL5)
                panel = PanelKind.None; follow = false; zoom = 52; pitch = 0.66f;
                focus = C.S.Parties.Where(p => p.Kind == PartyKind.Caravan && !p.Down && p.Breed != "pilgrims").OrderBy(p => Vector2.Distance(p.Pos, C.Player.Pos))
                    .Select(p => p.Pos).DefaultIfEmpty(C.Player.Pos).First();
                break;
            case "river": // a river bending round a town and its bridges (the banks' ribbons)
                panel = PanelKind.None; follow = false; zoom = 120; pitch = 0.95f;
                focus = C.S.Settlements.OrderBy(s => C.Map.RiverDist(s.Pos) < 40 ? Vector2.Distance(s.Pos, C.Player.Pos) : float.MaxValue).First().Pos;
                break;
            case var land when land.StartsWith("lands-"): // a province of one kind of country, seen whole (docs/archive/WORLDMAP.md WM3)
                panel = PanelKind.None; follow = false; zoom = 150; pitch = 0.9f;
                focus = C.S.Provinces.Where(p => p.Kind == land[6..]).Select(p => p.Center).DefaultIfEmpty(C.Player.Pos).First();
                break;
            case "sites": panel = PanelKind.None; zoom = 120; pitch = 0.85f; focus = C.Player.Pos; follow = true; break;
            case "party": panel = PanelKind.Party; break;
            case "roll":
                rollOf = C.Player.Companies.OrderByDescending(k => k.Roll.Count).First(); panel = PanelKind.Roll; note = "";
                C.StageTies(rollOf); // show the ties (C3) on a fresh campaign's roll
                rollPick = rollOf.Roll.OrderByDescending(m => m.Battles).Skip(1).FirstOrDefault(); // and the action bar (C4)
                if (rollPick != null) { C.S.MentionPool.Add(rollPick.Id); rollPick.Mentions = 1; }
                break;
            case "campmen": C.StageCamp(); break;
            case "camp": panel = PanelKind.Camp; campMan = null; note = ""; break;
            case "campman":
                panel = PanelKind.Camp; note = "";
                campManOf = C.CampFires().First(k => k.Roll.Count > 0);
                campMan = campManOf.Roll.OrderByDescending(m => m.Ties.Count).ThenByDescending(m => m.Battles).First();
                break;
            case "divisions": panel = PanelKind.Divisions; note = ""; break;
            case "quests": C.StageQuests(); panel = PanelKind.Quests; note = ""; break;
            case "questmap": // the pinned quest in the HUD's corner and its marker on the map
                panel = PanelKind.None; follow = false; zoom = 120; pitch = 0.85f; questMark = null;
                if (C.S.Contracts.FirstOrDefault(k => k.Id == C.S.PinnedQuest) is Contract pin && C.PlaceOf(pin).Place is Vector2 qp) focus = qp;
                break;
            case "skills": panel = PanelKind.Skills; note = ""; break;
            case "inventory":
            {
                // Standing in the town so its smith shows (C11); a battered helm, spare gear of mixed makes, goods and a wagon.
                var town = C.S.Settlements.First(s => s.Kind == SettlementKind.Town && !C.Refuses(s));
                C.Player.Pos = town.Pos + new Vector2(1, 0);
                C.Player.Wagons = Math.Max(1, C.Player.Wagons);
                C.S.Goods["arms"] = Math.Max(14, C.S.Goods.GetValueOrDefault("arms"));
                C.S.Goods["armour"] = Math.Max(8, C.S.Goods.GetValueOrDefault("armour"));
                C.S.Goods["silver"] = Math.Max(4, C.S.Goods.GetValueOrDefault("silver"));
                C.S.Goods["furs"] = Math.Max(11, C.S.Goods.GetValueOrDefault("furs"));
                if (!C.Captain.Baggage.Any(i => i.Def == "longsword")) C.AddItem("longsword", ItemQuality.Fine, "keen", "won in a tournament");
                if (!C.Captain.Baggage.Any(i => i.Def == "kite_shield")) C.AddItem("kite_shield", ItemQuality.Masterwork, "well_tempered", "taken from a lord");
                if (!C.Captain.Baggage.Any(i => i.Def == "war_axe")) C.AddItem("war_axe", ItemQuality.Worn, "", "taken from a brigand");
                var helm = C.SmithItem(C.Captain.Kit.Helm);
                helm.Battered = true;
                if (!C.Captain.Baggage.Contains(helm)) C.Captain.Baggage.Add(helm);
                C.EnsureItemIds();
                panel = PanelKind.Inventory; invItem = helm; invPick = helm.Def; invGoods = ""; invTab = 0; invScroll = 0; note = ""; break;
            }
            case "realm": realmTab = 0; panel = PanelKind.Realm; note = ""; break;
            case "atlas": panel = PanelKind.Atlas; note = ""; break;
            case "career": realmTab = 1; panel = PanelKind.Realm; note = ""; break;
            case "orders": realmTab = 3; panel = PanelKind.Realm; note = ""; break;
            case "legend": realmTab = 2; panel = PanelKind.Realm; note = ""; break;
            case "chapter": at = s; tab = "CHAPTER"; panel = PanelKind.Settlement; note = ""; break;
            case "fief": at = s; tab = "FIEF"; panel = PanelKind.Settlement; note = ""; break;
            case "household": charTab = C.S.Factions.Count; panel = PanelKind.Characters; note = ""; break;
            case "lords": charTab = 0; panel = PanelKind.Characters; note = ""; break;
            case "talk":
                if (C.S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction >= 0).OrderByDescending(c => c.Children.Count).FirstOrDefault() is Character lord)
                {
                    if (lord.Want == "") { lord.Want = "wealth"; }
                    lord.WantKnown = true;
                    OpenTalk(lord, PanelKind.None);
                    talkTopic = "council"; talkArgs.Add("honour");
                }
                break;
            case "hall":
                at = C.S.Settlements.Where(s => s.Kind != SettlementKind.Village).OrderByDescending(s => C.HallFolk(s).Count).First();
                C.Player.Pos = at.Pos; C.Player.Moving = false;
                tab = "HALL"; panel = PanelKind.Settlement; note = ""; break;
            case "esquires":
                at = s!; C.Player.Pos = at.Pos; C.Player.Moving = false;
                tab = "ESQUIRES"; panel = PanelKind.Settlement; note = ""; break;
            case "beat": panel = PanelKind.Beat; break;
            case "gameover": panel = PanelKind.GameOver; break;
            case "book": OpenBook(PanelKind.None); break;
            case "cairn": panel = PanelKind.None; zoom = 26; pitch = 0.6f; follow = false; if (C.S.Cairns.Count > 0) focus = C.S.Cairns[^1].Pos; break;
            case "event": panel = PanelKind.Event; note = ""; break;
            case var w when w.StartsWith("tab:"): // any tab of a given place (the town screen's shots)
                at = s!; C.Player.Pos = at.Pos; C.Player.Moving = false; tab = w[4..]; panel = PanelKind.Settlement; note = ""; break;
            case "settlement":
                at = C.S.Settlements.First(s => s.Kind == SettlementKind.Town && !C.Refuses(s));
                tab = "MERCENARIES"; panel = PanelKind.Settlement; break;
            case "street": // the bare street of a town (the per-culture town shots)
                at = s ?? C.S.Settlements.First(t => t.Kind == SettlementKind.Town && !C.Refuses(t));
                tab = ""; panel = PanelKind.Settlement; note = ""; break;
            case "tavern" or "elders":
                at = what == "tavern" ? s ?? C.S.Settlements.First(t => t.Kind == SettlementKind.Town && !C.Refuses(t))
                    : C.S.Settlements.OrderBy(s => Vector2.Distance(s.Pos, C.Player.Pos)).First(s => s.Kind == SettlementKind.Village && !C.Refuses(s));
                tab = what.ToUpperInvariant(); panel = PanelKind.Settlement; note = ""; break;
            case "armourer" or "market":
                at = C.S.Settlements.First(s => s.Kind == SettlementKind.Town && !C.Refuses(s));
                C.S.Gold = 2000;
                foreach (var (k, n) in new[] { ("arms", 14), ("armour", 3), ("silver", 2) }) C.S.Goods[k] = n; // goods to sell, staged
                // Pieces as things (C9): a fine keen sword with a past, a taken masterwork spear, a heavy baggage.
                var blade = Campaign.Own(C.Captain, "arming_sword", ItemQuality.Fine, "keen")!;
                (blade.Kills, blade.Battles, blade.From) = (23, 9, "bought in a market, day 4");
                Campaign.Own(C.Captain, "boar_spear", ItemQuality.Masterwork, "balanced", "taken from Lord Renaud Ironside, day 88");
                C.S.Goods["arms"] = 160;
                tab = what == "market" ? "MARKET" : "ARMOURER"; panel = PanelKind.Settlement; note = ""; break;
            case "upgrade": upgrading = C.Player.Companies[0]; upStack = upgrading.Main; panel = PanelKind.Upgrade; break;
            case "encounter": panel = PanelKind.Encounter; break;
            case "results": panel = PanelKind.Results; break;
        }
    }
}
