using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The campaign map screen (docs/CAMPAIGN.md 3.4 and 9): the world in the battle renderer's 3D style, parties as large banner-bearer
/// puppets, and the campaign interface (map HUD plus encounter, settlement, party, upgrade, results and menu panels).
sealed partial class MapScreen
{
    enum PanelKind { None, Encounter, Settlement, Party, Upgrade, Results, Menu, GameOver, Siege, Characters, Skills, Realm, Beat, Event }

    public Campaign C = null!;
    public Action<BattleSetup>? OnBattle;
    public Action? OnTitle, OnSettings;

    /// Ironman campaigns keep one save; others quick-save.
    void Save() { C.S.Save(C.S.Ironman ? "autosave" : "quicksave"); C.Log("Saved."); }

    readonly GraphicsDevice gd;
    readonly Art art;
    readonly Shading? shading;
    readonly Controls ctl;
    readonly Ui ui;
    readonly Puppet puppet;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPositionColor> lines = new();
    readonly AlphaTestEffect spriteFx;
    readonly BasicEffect lineFx;
    readonly Dictionary<int, Unit> bearers = new();
    World? world;
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
    static readonly HashSet<string> tabFor = new() { "TRAIN", "RECRUIT", "MARKET", "FOOD", "REST", "CONTRACT" };
    bool follow = true, pendingClick;
    /// The camera starts pulled back far enough to show a province and its neighbours on the (much larger) world map.
    float yaw = MathHelper.PiOver2, pitch = 0.95f, zoom = 170, time;
    Vector2 focus;
    Matrix view, proj;
    Vector3 eye;
    KeyboardState kb, pkb;
    MouseState ms, pms;

    public MapScreen(GraphicsDevice gd, SpriteBatch sb, Art art, PixelFont font, Shading? shading, Controls ctl)
    {
        this.gd = gd; this.art = art; this.shading = shading; this.ctl = ctl;
        ui = new Ui(sb, art, font);
        puppet = new Puppet(art, sprites) { Scale = 3 };
        spriteFx = new AlphaTestEffect(gd)
        {
            Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128,
            FogEnabled = true, FogColor = World.Horizon.ToVector3(), FogStart = 260, FogEnd = 700,
        };
        lineFx = new BasicEffect(gd) { VertexColorEnabled = true };
    }

    public void Open(Campaign c)
    {
        C = c;
        world?.Dispose();
        c.Map.Season = season = c.SeasonIndex;
        terrain = c.Map.BuildTerrain();
        world = new World(gd, terrain);
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
        time += dt;
        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) pendingClick = true;
        if (Pressed(Keys.Enter)) Tips.Dismiss();
        Tips.Show("welcome", "Welcome, captain. Click the map to march; click a town or village to enter it.\nBrigands are close by: click their banner to ride at them, or let them come to you.\nWASD pans, the wheel zooms, Space pauses. P opens your company.");
        if (C.Player.Companies.Any(Rules.UpgradeReady))
            Tips.Show("upgrade", "Some of your men have the XP for promotion (gold bar, N READY).\nOpen the party screen (P) in a town, click a troop stack, and choose how many to promote and into what.");
        if (Skills.PicksTotal(C.Captain) > 0) Tips.Show("perk", "One of your skills has reached a new mark: choose a perk on its branch (K opens your skills).\nSkills rise by doing: commanding, marching, healing, promoting, fighting, ruling and winning goodwill.");
        if (C.Captain.Renown >= 80) Tips.Show("renown", "Renown is your standing: each victory adds to it, and it raises your men cap.\nThe bar under the captain panel shows the next rise.");
        if (C.Player.Companies.Count >= Rules.CompanySlots(C.Captain)) Tips.Show("slots", "You have reached the maximum of 10 companies. Merge or disband a company to form a new one.");
        if (C.Day >= 7) Tips.Show("upkeep", "Upkeep is paid each week (the bar at the top). Your contract pays most of it;\nloot, ransoms and bounties pay the rest. Unpaid men desert.");

        if (panel == PanelKind.None)
        {
            if (ctl.Pressed(Act.Menu)) { panel = PanelKind.Menu; return; }
            if (ctl.Pressed(Act.PartyScreen)) { panel = PanelKind.Party; return; }
            if (ctl.Pressed(Act.Pause)) speed = speed == 0 ? 1 : 0;
            if (ctl.Pressed(Act.Speed1)) speed = 1;
            if (ctl.Pressed(Act.Speed2)) speed = 3;
            if (ctl.Pressed(Act.Speed3)) speed = 8;
            if (ctl.Pressed(Act.FollowParty)) follow = true;
            if (ctl.Pressed(Act.QuickSave)) Save();
            if (ctl.Pressed(Act.QuickLoad) && !C.S.Ironman)
            {
                if (CampaignState.Load("quicksave") is CampaignState loaded) { Open(new Campaign(loaded)); C.Log("Loaded."); }
                else if (CampaignState.LoadError is string err) C.Log("Quicksave not loaded: " + err + ".");
            }
            Camera(dt, active);
            if (active && pendingClick && !ui.Blocks(ms.Position)) { pendingClick = false; ClickWorld(Ground(ms.Position)); }
            if (speed > 0) C.Tick(dt / Balance.Get().HourSeconds * speed);
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
                    at = s; tab = s.Kind == SettlementKind.Village ? "RECRUIT" : s.Kind == SettlementKind.Castle ? "REST" : "TRAIN"; panel = PanelKind.Settlement; note = "";
                    // Open the tab the campaign says you need, not always the first one: arrival should feel like an answer.
                    var (_, where) = C.NextStep();
                    if (s.Kind == SettlementKind.Town && tabFor.Contains(where)) tab = where;
                    Tips.Show("settlement", "Villages give recruits (levies). Towns train companies that have the XP, sell horses and gear,\nhire out mercenary companies, and pay contracts. Resting in a town heals the wounded faster.");
                }
            }
            int week = (int)(C.S.Hours / 168);
            if (week != lastWeek) { lastWeek = week; C.S.Save("autosave"); }
            if (C.SeasonIndex != season) { var keep = (focus, follow); Open(C); (focus, follow) = keep; C.Log($"{C.Season} comes."); }
            Trails();
        }
        else if (renaming == null && ctl.Pressed(Act.Menu) && panel is PanelKind.Party or PanelKind.Menu or PanelKind.Settlement or PanelKind.Upgrade or PanelKind.Siege or PanelKind.Characters or PanelKind.Skills or PanelKind.Realm)
            panel = panel == PanelKind.Upgrade ? PanelKind.Party : PanelKind.None;
        if (renaming != null) Rename();
        else Ui.Typed.Clear();
        if (panel == PanelKind.None && Pressed(Keys.L)) { panel = PanelKind.Characters; note = ""; }
        else if (panel == PanelKind.Characters && Pressed(Keys.L)) panel = PanelKind.None;
        else if (panel is PanelKind.None or PanelKind.Party && Pressed(Keys.K)) { panel = PanelKind.Skills; note = ""; }
        else if (panel == PanelKind.Skills && Pressed(Keys.K)) panel = PanelKind.None;
        else if (panel == PanelKind.None && Pressed(Keys.R)) { panel = PanelKind.Realm; note = ""; }
        else if (panel == PanelKind.Realm && Pressed(Keys.R)) panel = PanelKind.None;
        // A career beat or the council's word waits for the map to be clear (F5).
        if (panel == PanelKind.None && C.Beat != null) { panel = PanelKind.Beat; speed = 0; }
        if (panel == PanelKind.None && C.S.GameOver) { panel = PanelKind.GameOver; speed = 0; }
        if (panel == PanelKind.None && C.S.Event != null && C.Encounter == null) { panel = PanelKind.Event; speed = 0; note = ""; }
        if (panel == PanelKind.None && C.Besieging is Settlement besieged && C.Player.SiegeTarget >= 0 && !C.Player.Moving && C.Encounter == null && Pressed(Keys.B)) { at = besieged; panel = PanelKind.Siege; }
        if (follow) focus = Vector2.Lerp(focus, C.Player.Pos, MathF.Min(1, dt * 4));
    }

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
        focus = Vector2.Clamp(focus, new Vector2(-C.Map.ExtentUsed), new Vector2(C.Map.ExtentUsed));
        yaw += ctl.Axis(Act.RotateRight, Act.RotateLeft) * 1.6f * dt;
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

    void ClickWorld(Vector2 g)
    {
        var party = C.S.Parties.Where(p => p != C.Player && !p.Down && Vector2.Distance(p.Pos, g) < 5).OrderBy(p => Vector2.Distance(p.Pos, g)).FirstOrDefault();
        var town = C.SettlementAt(g, 6);
        if (party != null) C.Chase(party);
        else if (town != null) C.GoTo(town);
        else C.MoveTo(g);
        if (speed == 0) speed = 1;
    }

    Vector2 Ground(Point m)
    {
        var vp = gd.Viewport;
        var near = vp.Unproject(new Vector3(m.X, m.Y, 0), proj, view, Matrix.Identity);
        var far = vp.Unproject(new Vector3(m.X, m.Y, 1), proj, view, Matrix.Identity);
        var d = Vector3.Normalize(far - near);
        float Above(Vector3 p) => p.Y - terrain.Height(new Vector2(p.X, p.Z));
        float s = 0, step = 1;
        for (int i = 0; i < 1200 && s < 1500; i++)
        {
            var p = near + d * s;
            float h = Above(p);
            if (h > 0) { step = MathF.Max(0.5f, h * 0.5f); s += step; continue; }
            float lo = MathF.Max(0, s - step), hi = s;
            for (int k = 0; k < 10; k++) { float mid = (lo + hi) / 2; if (Above(near + d * mid) > 0) lo = mid; else hi = mid; }
            var hit = near + d * hi;
            return new Vector2(hit.X, hit.Z);
        }
        return focus;
    }

    // ---------- drawing ----------

    public void Draw(Quality quality)
    {
        // Day and night on the map follow the campaign clock (kept readable at night); autumn and winter skies are greyer.
        Sky.Set(new Weather((float)(C.S.Hours % 24), C.SeasonIndex >= 2 ? 0.25f : 0), nightFloor: 0.5f);
        var look = new Vector3(MathF.Cos(yaw) * MathF.Cos(pitch), -MathF.Sin(pitch), MathF.Sin(yaw) * MathF.Cos(pitch));
        var f3 = new Vector3(focus.X, terrain.Height(focus), focus.Y);
        eye = f3 - look * zoom;
        eye.Y = MathF.Max(eye.Y, terrain.Height(new Vector2(eye.X, eye.Z)) + 2);
        // Pulled back for a big map, the camera must still see the ground under it: the far plane and fog follow the zoom.
        proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45), gd.Viewport.AspectRatio, 0.5f, MathF.Max(1600, zoom * 3.2f));
        view = Matrix.CreateLookAt(eye, f3, Vector3.Up);
        UpdateBearers();
        // Fog starts beyond the near ground in view, so a wide view is hazy at the horizon rather than cut off.
        float fogStart = MathF.Max(200, zoom * 1.9f), fogEnd = MathF.Max(600, zoom * 5.2f);
        world!.SetFog(fogStart, fogEnd);
        spriteFx.FogStart = fogStart;
        spriteFx.FogEnd = fogEnd;

        bool shaded = shading != null && quality != Quality.Low;
        if (shaded)
        {
            var sh = shading!;
            bool shadows = quality == Quality.High;
            var fog = sh.Fog;
            sh.Fog = new Vector2(fogStart, fogEnd);
            if (shadows)
            {
                sh.AimSun(f3, Math.Clamp(zoom * 2.4f, 80, 420));
                gd.Textures[1] = null;
                gd.SetRenderTarget(sh.ShadowMap);
                gd.Clear(Color.White);
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                world!.DrawShadowCasters(sh, time);
                BuildSprites(sh.LightView);
                sh.Setup(sh.Sprite, Matrix.Identity, sh.LightEye, time, false, 0);
                sh.Sprite.Parameters["ShadowMap"].SetValue((Texture2D?)null);
                DrawSprites(sh.Sprite, "SpriteShadow");
            }
            gd.Textures[1] = null;
            gd.SetRenderTarget(sh.Scene());
            gd.Clear(World.Horizon);
            world!.DrawShaded(sh, view, proj, eye, time, shadows);
            BuildSprites(view);
            sh.Setup(sh.Sprite, view * proj, eye, time, shadows, 1.2f);
            DrawSprites(sh.Sprite, "Sprite");
            DrawLines();
            sh.PostToScreen(Settings.Current.Fxaa);
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
                gd.DrawUserPrimitives(PrimitiveType.TriangleList, sprites.A, 0, sprites.N / 3);
            }
            DrawLines();
        }
        DrawUi();
    }

    /// Each party is drawn as its leader on horseback (bandits on foot), three times life size.
    void UpdateBearers()
    {
        foreach (var p in C.S.Parties)
        {
            if (!bearers.TryGetValue(p.Id, out var u))
            {
                var type = p.Kind == PartyKind.Player ? Rules.CaptainType(C.Captain) : p.Kind == PartyKind.Bandit ? Troops.Get("levies") : Troops.Get(p.Tier >= 3 ? "knights" : "captain");
                u = new Unit
                {
                    Type = type, Team = 1, IsPlayer = p.Kind == PartyKind.Player, Palette = Style.PaletteOf(p.Kind == PartyKind.Bandit ? -2 : p.Faction),
                    Coat = p.Id % Style.Coats.Length, Facing = p.Facing, ViewFacing = p.Facing, Heading = p.Facing, ViewHeading = p.Facing,
                };
                if (p.Kind != PartyKind.Bandit) { u.Horse = p.Kind == PartyKind.Player ? Rules.CaptainMount(C.Captain) : type.Mount ?? Mount.Courser; u.HorseHp = u.Horse.Hp; }
                bearers[p.Id] = u;
            }
            var moved = p.Pos - u.Pos;
            bool moving = moved.LengthSquared() > 1e-6f;
            u.Pos = p.Pos;
            u.Facing = u.Heading = p.Facing;
            u.ViewFacing += MathHelper.WrapAngle(u.Facing - u.ViewFacing) * 0.2f;
            u.ViewHeading = u.ViewFacing;
            u.Speed = moving ? 6 : 0;
            u.Vel = moving ? new Vector2(MathF.Cos(p.Facing), MathF.Sin(p.Facing)) * 3 : Vector2.Zero;
            u.Anim += moving ? 0.08f : 0;
            u.Dead = false;
            u.Fled = p.Down;
        }
        foreach (int id in bearers.Keys.Where(id => C.PartyById(id) == null).ToList()) bearers.Remove(id);
    }

    void BuildSprites(Matrix viewer)
    {
        sprites.N = 0;
        puppet.Begin(viewer);
        foreach (var u in bearers.Values) if (!u.Fled) puppet.Draw(u, terrain.Height(u.Pos));
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
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, sprites.A, 0, sprites.N / 3);
    }

    Vector3 G(Vector2 p, float y = 0.6f) => new(p.X, terrain.Height(p) + y, p.Y);

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
            int n = (int)(Vector2.Distance(pl.Pos, t) / 3) + 1;
            for (int i = 0; i < n; i += 2) Line(G(Vector2.Lerp(pl.Pos, t, i / (float)n)), G(Vector2.Lerp(pl.Pos, t, (i + 1) / (float)n)), Style.Accent);
            Ring(t, 2, pl.ChaseId >= 0 ? Style.Foe : Style.Accent);
        }
        Ring(pl.Pos, 3, Style.Accent);
        if (lines.N == 0) return;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        lineFx.View = view; lineFx.Projection = proj;
        lineFx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.LineList, lines.A, 0, lines.N / 2);
    }

    // ---------- debug staging (IRONFIELD_CAMPAIGN_SHOTS) ----------

    public void Stage(string what, Settlement? s = null)
    {
        switch (what)
        {
            case "siege": at = s; panel = PanelKind.Siege; break;
            case "contract":
                at = C.S.Settlements.First(t => t.Kind == SettlementKind.Town && t.Faction == C.Captain.Contract);
                tab = "CONTRACT"; panel = PanelKind.Settlement; break;
            case "map": panel = PanelKind.None; zoom = 330; pitch = 0.95f; break;
            case "close": panel = PanelKind.None; zoom = 60; pitch = 0.7f; break;
            case "party": panel = PanelKind.Party; break;
            case "skills": panel = PanelKind.Skills; note = ""; break;
            case "realm": panel = PanelKind.Realm; note = ""; break;
            case "fief": at = s; tab = "FIEF"; panel = PanelKind.Settlement; note = ""; break;
            case "household": charTab = C.S.Factions.Count; panel = PanelKind.Characters; note = ""; break;
            case "beat": panel = PanelKind.Beat; break;
            case "event": panel = PanelKind.Event; note = ""; break;
            case "settlement":
                at = C.S.Settlements.First(s => s.Kind == SettlementKind.Town && !C.Refuses(s));
                tab = "MERCENARIES"; panel = PanelKind.Settlement; break;
            case "upgrade": upgrading = C.Player.Companies[0]; upStack = upgrading.Main; panel = PanelKind.Upgrade; break;
            case "encounter": panel = PanelKind.Encounter; break;
            case "results": panel = PanelKind.Results; break;
        }
    }
}
