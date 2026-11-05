using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The campaign map screen (docs/CAMPAIGN.md 3.4 and 9): the world in the battle renderer's 3D style, parties as large banner-bearer
/// puppets, and the campaign interface (map HUD plus encounter, settlement, party, upgrade, results and menu panels).
sealed class MapScreen
{
    enum PanelKind { None, Encounter, Settlement, Party, Upgrade, Results, Menu, GameOver, Siege }

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

    PanelKind panel;
    Settlement? at;
    CompanyRecord? upgrading, renaming;
    string renameText = "";
    string tab = "RECRUIT", note = "";
    int speed = 1, lastWeek = -1;
    bool follow = true, pendingClick;
    float yaw = MathHelper.PiOver2, pitch = 0.95f, zoom = 110, time;
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
            Tips.Show("upgrade", "A company has the XP to upgrade (gold bar, UPGRADE READY).\nOpen the party screen (P) or a town's training yard, and choose its next troops.");
        if (Rules.PerkPoints(C.Captain) > 0) Tips.Show("perk", "Your renown has earned a perk. Take it in the party screen (P).");
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
            if (ctl.Pressed(Act.QuickLoad) && !C.S.Ironman && CampaignState.Load("quicksave") is CampaignState loaded) { Open(new Campaign(loaded)); C.Log("Loaded."); }
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
                    Tips.Show("settlement", "Villages give recruits (levies). Towns train companies that have the XP, sell horses and gear,\nhire out mercenary companies, and pay contracts. Resting in a town heals the wounded faster.");
                }
            }
            int week = (int)(C.S.Hours / 168);
            if (week != lastWeek) { lastWeek = week; C.S.Save("autosave"); }
        }
        else if (renaming == null && ctl.Pressed(Act.Menu) && panel is PanelKind.Party or PanelKind.Menu or PanelKind.Settlement or PanelKind.Upgrade or PanelKind.Siege)
            panel = panel == PanelKind.Upgrade ? PanelKind.Party : PanelKind.None;
        if (renaming != null) Rename();
        else Ui.Typed.Clear();
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
        focus = Vector2.Clamp(focus, new Vector2(-WorldMap.Extent), new Vector2(WorldMap.Extent));
        yaw += ctl.Axis(Act.RotateRight, Act.RotateLeft) * 1.6f * dt;
        if (ms.MiddleButton == ButtonState.Pressed && pms.MiddleButton == ButtonState.Pressed)
        {
            yaw += (ms.X - pms.X) * 0.006f;
            pitch = Math.Clamp(pitch + (ms.Y - pms.Y) * 0.006f, 0.35f, 1.45f);
        }
        zoom = Math.Clamp(zoom - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.08f, 25, 260);
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
        proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45), gd.Viewport.AspectRatio, 0.5f, 1600);
        view = Matrix.CreateLookAt(eye, f3, Vector3.Up);
        UpdateBearers();

        bool shaded = shading != null && quality != Quality.Low;
        if (shaded)
        {
            var sh = shading!;
            bool shadows = quality == Quality.High;
            var fog = sh.Fog;
            sh.Fog = new Vector2(260, 700);
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

    // ---------- interface ----------

    static Color SideColor(int side) => side == -1 ? Style.Dim : Style.Sides[Style.PaletteOf(side)].Ui;
    Color PartyColor(Party p) => p.Kind == PartyKind.Player ? Style.Accent : SideColor(C.SideOf(p));

    void DrawUi()
    {
        bool click = pendingClick;
        pendingClick = false;
        ui.Begin(gd.Viewport, ms.Position, click);
        Flags();
        TopBar();
        Feed();
        Overview();
        CaptainPanel();
        Cards();
        Contracts();
        if (panel == PanelKind.None) { Tooltip(); Hints(); }
        Tips.Draw(ui, ui.U(130));
        switch (panel)
        {
            case PanelKind.Encounter: EncounterPanel(); break;
            case PanelKind.Settlement: SettlementPanel(); break;
            case PanelKind.Party: PartyPanel(); break;
            case PanelKind.Upgrade: UpgradePanel(); break;
            case PanelKind.Results: ResultsPanel(); break;
            case PanelKind.Menu: MenuPanel(); break;
            case PanelKind.GameOver: GameOverPanel(); break;
            case PanelKind.Siege: SiegePanel(); break;
        }
        ui.End();
        if (panel == PanelKind.None && click && !ui.Blocks(ms.Position)) pendingClick = true; // the map gets it
    }

    Vector2? Screen(Vector3 w)
    {
        var p = gd.Viewport.Project(w, proj, view, Matrix.Identity);
        return p.Z is > 0 and < 1 ? new Vector2(p.X, p.Y) : null;
    }

    void Flags()
    {
        foreach (var s in C.S.Settlements)
        {
            if (Screen(G(s.Pos, s.Kind == SettlementKind.Village ? 6 : 11)) is not Vector2 sp) continue;
            if (s.Kind == SettlementKind.Village && zoom > 170) continue;
            var col = SideColor(s.Faction);
            string label = s.Name.ToUpperInvariant() + (s.Besieged ? " (BESIEGED)" : "") + (s.Owner == C.Player.Id ? " *" : "");
            int scale = s.Kind == SettlementKind.Village ? 1 : 2;
            int w = ui.TW(label, scale) + ui.U(10);
            var r = new Rectangle((int)sp.X - w / 2, (int)sp.Y - ui.U(scale == 2 ? 18 : 13), w, ui.U(scale == 2 ? 18 : 13));
            ui.Rect(r, Color.Black * 0.45f);
            ui.Rect(new Rectangle(r.X, r.Bottom - ui.U(2), r.Width, ui.U(2)), col);
            ui.TextC(label, sp.X, r.Y + ui.U(3), Color.White, scale);
        }
        foreach (var p in C.S.Parties)
        {
            if (p.Down || Screen(G(p.Pos, 9)) is not Vector2 sp) continue;
            string label = $"{p.Name.ToUpperInvariant()} {p.Men}";
            int w = ui.TW(label, 1) + ui.U(8);
            var r = new Rectangle((int)sp.X - w / 2, (int)sp.Y - ui.U(12), w, ui.U(12));
            ui.Rect(r, PartyColor(p) * 0.85f);
            ui.TextC(label, sp.X, r.Y + ui.U(3), Color.Black, 1);
        }
    }

    void TopBar()
    {
        int w = ui.U(480), x = (ui.W - w) / 2, y = ui.U(10);
        var r = new Rectangle(x, y, w, ui.U(46));
        ui.Panel(r);
        int hour = (int)(C.S.Hours % 24);
        ui.Text($"DAY {C.Day}  {hour:00}:00  {C.Season.ToUpperInvariant()}", x + ui.U(10), y + ui.U(8), C.Winter ? Style.Friendly : Style.Text);
        int me = Math.Max(0, C.Captain.Contract);
        var foes = Enumerable.Range(0, C.S.Factions.Count).Where(f => C.FactionsAtWar(me, f)).Select(f => Campaign.FactionNames[f].Split(' ').Last().ToUpperInvariant()).ToList();
        string war = C.Captain.Contract < 0 ? "FREE CAPTAIN" : foes.Count > 0 ? "WAR: " + string.Join(", ", foes) : "AT PEACE";
        ui.Text(war, x + ui.U(10), y + ui.U(28), foes.Count > 0 ? Style.Foe : Style.Ok, 1);
        string[] labels = { "II", ">", ">>", ">>>" };
        int[] speeds = { 0, 1, 3, 8 };
        for (int i = 0; i < 4; i++)
            if (ui.Button(new Rectangle(x + w - ui.U(4 + (4 - i) * 46), y + ui.U(6), ui.U(42), ui.U(22)), labels[i], true, "", speed == speeds[i])) speed = speeds[i];
        ui.Text("UPKEEP", x + w - ui.U(188), y + ui.U(32), Style.Dim, 1);
        ui.Bar(new Rectangle(x + w - ui.U(140), y + ui.U(33), ui.U(134), ui.U(5)), (float)(C.S.Hours % 168 / 168), Style.Accent);
    }

    void Feed()
    {
        int y = ui.U(10), room = (ui.W - ui.U(480)) / 2 - ui.U(30), chars = Math.Max(10, room / (6 * ui.S));
        foreach (var e in C.S.Log.AsEnumerable().Reverse().Take(6))
        {
            float age = (float)(C.S.Hours - e.At) / 24;
            var c = Color.White * Math.Clamp(1.4f - age * 0.25f, 0.45f, 1);
            string t = e.Text.ToUpperInvariant();
            if (t.Length > chars) t = t[..(chars - 2)] + "..";
            ui.Rect(new Rectangle(ui.U(10), y - ui.U(3), ui.TW(t, 1) + ui.U(10), ui.U(13)), Color.Black * 0.4f);
            ui.Text(t, ui.U(15), y, c, 1);
            y += ui.U(15);
        }
    }

    void Overview()
    {
        int size = ui.U(170);
        var r = new Rectangle(ui.W - size - ui.U(10), ui.U(10), size, size);
        ui.Panel(new Rectangle(r.X - ui.U(3), r.Y - ui.U(3), r.Width + ui.U(6), r.Height + ui.U(6)));
        ui.Sb.Draw(world!.Minimap, r, Color.White);
        Vector2 M(Vector2 w) { var m = World.WorldToMinimap(w, terrain.MinimapRange); return new Vector2(r.X + m.X * r.Width, r.Y + m.Y * r.Height); }
        foreach (var s in C.S.Settlements.Where(s => s.Kind != SettlementKind.Village))
        {
            var p = M(s.Pos);
            ui.Rect(new Rectangle((int)p.X - ui.U(2), (int)p.Y - ui.U(2), ui.U(5), ui.U(5)), SideColor(s.Faction));
        }
        foreach (var p in C.S.Parties.Where(p => !p.Down))
        {
            var q = M(p.Pos);
            int d = ui.U(p.Kind == PartyKind.Player ? 5 : 3);
            ui.Rect(new Rectangle((int)q.X - d / 2, (int)q.Y - d / 2, d, d), PartyColor(p));
        }
        ui.Frame(r, Style.PanelEdge, ui.U(1));
        if (ui.Clicked(r))
        {
            focus = World.MinimapToWorld(new Vector2((ui.Mouse.X - r.X) / (float)r.Width, (ui.Mouse.Y - r.Y) / (float)r.Height), terrain.MinimapRange);
            follow = false;
        }
    }

    void CaptainPanel()
    {
        var cap = C.Captain;
        var r = new Rectangle(ui.U(10), ui.H - ui.U(132), ui.U(300), ui.U(122));
        ui.Panel(r);
        int x = r.X + ui.U(10), y = r.Y + ui.U(8);
        ui.Text("CAPTAIN", x, y, Style.Accent);
        ui.TextR($"RENOWN {cap.Renown:0}", r.Right - ui.U(10), y + ui.U(2), Style.Text, 1);
        y += ui.U(22);
        float bal = C.S.LastWeekBalance;
        ui.Text($"{C.S.Gold:0} CROWNS", x, y, Style.Text);
        ui.TextR($"{(bal >= 0 ? "+" : "")}{bal:0} / WEEK", r.Right - ui.U(10), y + ui.U(2), bal >= 0 ? Style.Ok : Style.Bad, 1);
        y += ui.U(20);
        string contract = cap.Contract >= 0 ? "SWORN TO THE " + Campaign.FactionNames[cap.Contract].ToUpperInvariant() : "FREE CAPTAIN (NO PAY)";
        ui.Text(contract, x, y, cap.Contract >= 0 ? SideColor(cap.Contract) : Style.Dim, 1);
        y += ui.U(14);
        ui.Text($"MEN {C.Player.Men}+{C.Player.Wounded}W / {Rules.MenLimit(cap)}   COMPANIES {C.Player.Companies.Count}/{Rules.CompanySlots(cap)}   HORSES {C.Player.Horses}", x, y, Style.Dim, 1);
        y += ui.U(14);
        ui.Text($"FOOD {C.Player.Food:0} DAYS" + (C.PrisonerCount > 0 ? $"   PRISONERS {C.PrisonerCount}" : "") + (C.Player.Captives.Count > 0 ? $"   LORDS HELD {C.Player.Captives.Count}" : "")
            + (C.Fiefs.Any() ? $"   FIEFS {C.Fiefs.Count(f => f.Kind != SettlementKind.Village)}" : ""), x, y, C.Player.Food < 3 ? Style.Bad : Style.Dim, 1);
        y += ui.U(14);
        if (C.S.Hours < cap.WoundedUntil) ui.Text($"WOUNDED: {(cap.WoundedUntil - C.S.Hours) / 24:0.0} DAYS", x, y, Style.Bad, 1);
        else if (C.Besieging is Settlement sg) ui.Text($"BESIEGING {sg.Name.ToUpperInvariant()} (B)", x, y, Style.Warn, 1);
        else if (Rules.PerkPoints(cap) > 0) ui.Text("A NEW PERK IS READY (P)", x, y, Style.Accent, 1);
        else if (C.S.UnpaidWeeks > 0) ui.Text("YOUR MEN ARE UNPAID", x, y, Style.Bad, 1);
    }

    void Cards()
    {
        var list = C.Player.Companies;
        int cw = ui.U(96), ch = ui.U(108), gap = ui.U(6), total = list.Count * cw + Math.Max(0, list.Count - 1) * gap;
        int x0 = Math.Max(ui.U(320), (ui.W - total) / 2);
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            var r = new Rectangle(x0 + i * (cw + gap), ui.H - ch - ui.U(10), cw, ch);
            if (r.Right > ui.W - ui.U(10)) break;
            Card(c, r, i + 1);
            if (panel == PanelKind.None && ui.Clicked(r)) panel = PanelKind.Party;
        }
    }

    void Card(CompanyRecord c, Rectangle r, int index)
    {
        bool ready = Rules.UpgradeReady(c);
        ui.Panel(r, ready ? Style.Accent : Style.PanelEdge, ready ? 2 : 1);
        ui.Text($"{index}", r.X + ui.U(5), r.Y + ui.U(5), Style.Dim, 1);
        ui.Text(Style.Numerals[Math.Clamp(c.Type.Tier, 0, 5)], r.X + ui.U(5), r.Y + ui.U(16), Style.Accent, 1);
        ui.Portrait(c.Type, new Rectangle(r.X + ui.U(20), r.Y + ui.U(4), r.Width - ui.U(40), ui.U(44)));
        string name = c.Name.ToUpperInvariant();
        ui.TextC(name.Length > 15 ? name[..15] : name, r.X + r.Width / 2f, r.Y + ui.U(50), Style.Text, 1);
        ui.TextC(c.Type.Name.ToUpperInvariant(), r.X + r.Width / 2f, r.Y + ui.U(60), Style.Dim, 1);
        ui.TextC(c.Wounded > 0 ? $"{c.Men}+{c.Wounded}W" : $"{c.Men}", r.X + r.Width / 2f, r.Y + ui.U(71), Style.Text);
        float need = c.Type.UpgradeXp;
        ui.Bar(new Rectangle(r.X + ui.U(6), r.Y + ui.U(90), r.Width - ui.U(12), ui.U(4)), need > 0 ? c.Xp / need : 1, ready ? Style.Accent : Style.Friendly);
        ui.Bar(new Rectangle(r.X + ui.U(6), r.Y + ui.U(97), r.Width - ui.U(12), ui.U(3)), c.Cohesion / 30f, Style.Ok);
        if (ready) ui.TextC("UPGRADE READY", r.X + r.Width / 2f, r.Y - ui.U(11), Style.Accent, 1);
    }

    void Hints()
    {
        ui.Text("LMB MOVE / ENTER / CHASE   P PARTY   SPACE PAUSE   1-3 SPEED", ui.U(12), ui.H - ui.U(160), Color.White * 0.6f, 1);
        ui.Text("WASD PAN   Q/E TURN   WHEEL ZOOM   F FOLLOW   F5 SAVE  F9 LOAD   ESC MENU", ui.U(12), ui.H - ui.U(146), Color.White * 0.6f, 1);
    }

    void Tooltip()
    {
        if (ui.Blocks(ms.Position)) return;
        var g = Ground(ms.Position);
        var p = C.S.Parties.Where(q => !q.Down && Vector2.Distance(q.Pos, g) < 5).OrderBy(q => Vector2.Distance(q.Pos, g)).FirstOrDefault();
        var s = p == null ? C.SettlementAt(g, 6) : null;
        if (p == null && s == null) return;
        var linesOut = new List<(string, Color)>();
        if (p != null)
        {
            linesOut.Add((p.Name.ToUpperInvariant(), PartyColor(p)));
            linesOut.Add((p.Kind == PartyKind.Bandit ? "BRIGANDS" : p.Kind == PartyKind.Player ? "YOU" : Campaign.FactionNames[p.Faction].ToUpperInvariant(), Style.Dim));
            linesOut.Add(($"{p.Men} MEN IN {p.Companies.Count} COMPANIES", Style.Text));
            if (p != C.Player)
            {
                float ratio = Rules.Strength(p) / MathF.Max(1, Rules.Strength(C.Player));
                linesOut.Add((ratio > 1.25f ? "STRONGER THAN YOU" : ratio < 0.8f ? "WEAKER THAN YOU" : "A FAIR MATCH", ratio > 1.25f ? Style.Bad : ratio < 0.8f ? Style.Ok : Style.Warn));
                linesOut.Add((C.Hostile(p, C.Player) ? "HOSTILE" : "NOT HOSTILE", C.Hostile(p, C.Player) ? Style.Foe : Style.Dim));
                if (p.Kind == PartyKind.Lord) linesOut.Add(($"TASK: {p.Task.ToUpperInvariant()}", Style.Dim));
            }
        }
        else
        {
            linesOut.Add((s!.Name.ToUpperInvariant(), SideColor(s.Faction)));
            linesOut.Add(($"{s.Kind.ToString().ToUpperInvariant()} OF THE {Campaign.FactionNames[s.Faction].ToUpperInvariant()}", Style.Dim));
            linesOut.Add(($"{C.S.Provinces[s.Province].Kind.ToUpperInvariant()} COUNTRY", Style.Dim));
            if (s.Kind == SettlementKind.Village) linesOut.Add((s.ThreatUntil > C.S.Hours ? "THREATENED: NO RECRUITS" : $"{s.Recruits} RECRUITS", Style.Text));
            if (s.Kind == SettlementKind.Town) linesOut.Add(($"{s.Mercenaries.Count} MERCENARY COMPANIES, {s.Horses} HORSES", Style.Text));
            if (C.Refuses(s)) linesOut.Add(("WILL NOT TRADE WITH YOU", Style.Foe));
        }
        int w = linesOut.Max(l => ui.TW(l.Item1, 1)) + ui.U(16), h = linesOut.Count * ui.U(13) + ui.U(10);
        var r = new Rectangle(Math.Min(ms.X + ui.U(16), ui.W - w - 4), Math.Min(ms.Y + ui.U(16), ui.H - h - 4), w, h);
        ui.Panel(r);
        for (int i = 0; i < linesOut.Count; i++) ui.Text(linesOut[i].Item1, r.X + ui.U(8), r.Y + ui.U(6) + i * ui.U(13), linesOut[i].Item2, 1);
    }

    Rectangle Modal(int w, int h, string title, Color? tc = null)
    {
        ui.Dim(0.45f);
        var r = new Rectangle((ui.W - ui.U(w)) / 2, (ui.H - ui.U(h)) / 2, ui.U(w), ui.U(h));
        ui.Panel(r);
        ui.TextC(title, r.X + r.Width / 2f, r.Y + ui.U(12), tc ?? Style.Accent, 3);
        return r;
    }

    void Note(Rectangle r)
    {
        if (note != "") ui.TextC(note, r.X + r.Width / 2f, r.Bottom - ui.U(54), Style.Warn, 1);
    }

    void EncounterPanel()
    {
        var e = C.Encounter!;
        var r = Modal(680, 380, "ENCOUNTER", Style.Foe);
        int x = r.X + ui.U(24), y = r.Y + ui.U(52);
        ui.Text(e.Name.ToUpperInvariant(), x, y, PartyColor(e));
        ui.Text(e.Kind == PartyKind.Bandit ? "BRIGANDS" : Campaign.FactionNames[e.Faction].ToUpperInvariant(), x, y + ui.U(20), Style.Dim, 1);
        float me = Rules.Strength(C.Player), them = Rules.Strength(e);
        ui.TextR($"YOU {C.Player.Men}   THEM {e.Men}", r.Right - ui.U(24), y + ui.U(2), Style.Text);
        var bar = new Rectangle(x, y + ui.U(36), r.Width - ui.U(48), ui.U(10));
        ui.Rect(bar, Style.Foe);
        ui.Rect(new Rectangle(bar.X, bar.Y, (int)(bar.Width * me / MathF.Max(1, me + them)), bar.Height), Style.Friendly);
        y += ui.U(60);
        foreach (var c in e.Companies.Where(c => c.Men > 0).Take(6))
        {
            ui.Portrait(c.Type, new Rectangle(x, y, ui.U(32), ui.U(30)));
            ui.Text($"{c.Men} {c.Type.Name.ToUpperInvariant()}", x + ui.U(40), y + ui.U(4), Style.Text, 1);
            ui.Text($"TIER {Style.Numerals[c.Type.Tier]}", x + ui.U(40), y + ui.U(16), Style.Dim, 1);
            x += ui.U(190);
            if (x > r.Right - ui.U(180)) { x = r.X + ui.U(24); y += ui.U(36); }
        }
        Note(r);
        int bw = ui.U(156), by = r.Bottom - ui.U(40), bx = r.X + (r.Width - 4 * bw - 3 * ui.U(8)) / 2;
        bool fit = C.Player.Men > 0;
        if (ui.Button(new Rectangle(bx, by, bw, ui.U(30)), "ATTACK", fit)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareBattle(e)); }
        if (ui.Button(new Rectangle(bx + bw + ui.U(8), by, bw, ui.U(30)), "AUTO-RESOLVE", fit)) { C.AutoResolveEncounter(); ShowResults(); }
        bool canRetreat = C.CanRetreat(out string why);
        if (ui.Button(new Rectangle(bx + 2 * (bw + ui.U(8)), by, bw, ui.U(30)), "RETREAT", canRetreat)) { C.Retreat(); panel = PanelKind.None; }
        if (!canRetreat && ui.Hover(new Rectangle(bx + 2 * (bw + ui.U(8)), by, bw, ui.U(30)))) note = why;
        int price = C.ParleyPrice(e);
        if (ui.Button(new Rectangle(bx + 3 * (bw + ui.U(8)), by, bw, ui.U(30)), price > 0 ? $"PAY {price}" : "PARLEY", price > 0 && C.S.Gold >= price))
        {
            if (C.Parley()) panel = PanelKind.None;
        }
        if (price < 0 && ui.Hover(new Rectangle(bx + 3 * (bw + ui.U(8)), by, bw, ui.U(30)))) note = "LORDS AT WAR DO NOT BARGAIN";
    }

    void SettlementPanel()
    {
        var s = at!;
        var r = Modal(860, 500, s.Name.ToUpperInvariant(), SideColor(s.Faction));
        bool fief = s.Owner == C.Player.Id && s.Kind != SettlementKind.Village;
        ui.TextC($"{s.Kind.ToString().ToUpperInvariant()} OF THE {Campaign.FactionNames[s.Faction].ToUpperInvariant()}   GOLD {C.S.Gold:0}",
            r.X + r.Width / 2f, r.Y + ui.U(40), Style.Dim, 1);
        var tabs = (s.Kind switch
        {
            SettlementKind.Village when C.Refuses(s) => new[] { "FORAGE" },
            SettlementKind.Village => new[] { "RECRUIT", "FOOD", "FORAGE", "REST" },
            SettlementKind.Castle => new[] { "REST", "FOOD", "CAPTIVES", "CONTRACT" },
            _ => new[] { "TRAIN", "RECRUIT", "MERCENARIES", "MARKET", "FOOD", "REST", "CAPTIVES", "CONTRACT" },
        }).Concat(fief ? new[] { "GARRISON" } : Array.Empty<string>()).ToArray();
        int tw = ui.U(tabs.Length > 7 ? 90 : 112), tx = r.X + (r.Width - tabs.Length * (tw + ui.U(6))) / 2;
        for (int i = 0; i < tabs.Length; i++)
            if (ui.Button(new Rectangle(tx + i * (tw + ui.U(6)), r.Y + ui.U(56), tw, ui.U(24)), tabs[i], true, "", tab == tabs[i])) { tab = tabs[i]; note = ""; }
        var body = new Rectangle(r.X + ui.U(24), r.Y + ui.U(92), r.Width - ui.U(48), r.Height - ui.U(160));
        switch (tab)
        {
            case "RECRUIT":
            {
                var levy = Troops.Get(Campaign.RecruitTroop(s.Faction));
                ui.Text($"{s.Recruits} MEN WILL TAKE YOUR COIN HERE ({levy.Cost} CROWNS EACH)", body.X, body.Y, Style.Text);
                if (s.ThreatUntil > C.S.Hours) ui.Text("THE VILLAGE WAS THREATENED: NO RECRUITS FOR NOW", body.X, body.Y + ui.U(20), Style.Foe, 1);
                ui.Portrait(levy, new Rectangle(body.X, body.Y + ui.U(36), ui.U(60), ui.U(56)));
                ui.Text($"{levy.Name.ToUpperInvariant()} (TIER I) JOIN A COMPANY OF THEIR KIND WITH ROOM, OR FORM A NEW ONE.", body.X + ui.U(70), body.Y + ui.U(44), Style.Dim, 1);
                ui.Text("NEW MEN ARE GREEN: THEY LOWER A COMPANY'S AVERAGE XP.", body.X + ui.U(70), body.Y + ui.U(58), Style.Dim, 1);
                int y = body.Y + ui.U(104);
                foreach (var (label, n) in new[] { ("RECRUIT 1", 1), ("RECRUIT 5", 5), ("RECRUIT ALL", 99) })
                {
                    if (ui.Button(new Rectangle(body.X, y, ui.U(170), ui.U(28)), label, s.Recruits > 0)) note = C.RecruitLevies(s, n) ?? $"RECRUITED. {s.Recruits} LEFT";
                    y += ui.U(34);
                }
                break;
            }
            case "MERCENARIES":
            {
                if (s.Mercenaries.Count == 0) ui.Text("NO COMPANIES FOR HIRE THIS WEEK.", body.X, body.Y, Style.Dim);
                int y = body.Y;
                foreach (var m in s.Mercenaries.ToList())
                {
                    ui.Portrait(m.Type, new Rectangle(body.X, y, ui.U(56), ui.U(54)));
                    ui.Text(m.Name.ToUpperInvariant(), body.X + ui.U(66), y + ui.U(4), Style.Text);
                    ui.Text($"{m.Men} {m.Type.Name.ToUpperInvariant()}, TIER {Style.Numerals[m.Type.Tier]}, {m.Xp:0} XP", body.X + ui.U(66), y + ui.U(24), Style.Dim, 1);
                    int price = C.MercenaryPrice(m);
                    if (ui.Button(new Rectangle(body.Right - ui.U(190), y + ui.U(10), ui.U(190), ui.U(30)), "HIRE", C.S.Gold >= price, $"{price} CROWNS"))
                        note = C.Hire(s, m) ?? "HIRED";
                    y += ui.U(66);
                }
                break;
            }
            case "MARKET":
            {
                ui.Text($"HORSES: {s.Horses} FOR SALE AT {C.HorsePrice} CROWNS. YOU HAVE {C.Player.Horses}.", body.X, body.Y, Style.Text);
                ui.Text("COMPANIES THAT BECOME CAVALRY NEED ONE HORSE PER MAN (OR PAY FOR THEM WHEN UPGRADING).", body.X, body.Y + ui.U(20), Style.Dim, 1);
                if (ui.Button(new Rectangle(body.X, body.Y + ui.U(40), ui.U(150), ui.U(28)), "BUY 1", s.Horses > 0)) note = C.BuyHorses(s, 1) ?? "BOUGHT";
                if (ui.Button(new Rectangle(body.X + ui.U(160), body.Y + ui.U(40), ui.U(150), ui.U(28)), "BUY 5", s.Horses > 0)) note = C.BuyHorses(s, 5) ?? "BOUGHT";
                ui.Text("YOUR GEAR", body.X, body.Y + ui.U(92), Style.Accent);
                var gear = C.Gear();
                for (int i = 0; i < gear.Length; i++)
                {
                    var (name, cost, owned) = gear[i];
                    var br = new Rectangle(body.X + (i % 2) * ui.U(370), body.Y + ui.U(116) + i / 2 * ui.U(36), ui.U(360), ui.U(30));
                    if (ui.Button(br, name, !owned, owned ? "OWNED" : $"{cost} CROWNS")) note = C.BuyGear(i) ?? "BOUGHT";
                }
                break;
            }
            case "TRAIN":
            {
                ui.Text("COMPANIES WITH ENOUGH XP CAN BE TRAINED UP THEIR LINE HERE.", body.X, body.Y, Style.Dim, 1);
                int y = body.Y + ui.U(18);
                foreach (var c in C.Player.Companies.ToList())
                {
                    ui.Portrait(c.Type, new Rectangle(body.X, y, ui.U(40), ui.U(38)));
                    ui.Text($"{c.Name.ToUpperInvariant()}: {c.Men} {c.Type.Name.ToUpperInvariant()}", body.X + ui.U(48), y + ui.U(4), Style.Text, 1);
                    ui.Text(c.Type.UpgradesTo.Length == 0 ? "AT THE TOP OF ITS LINE" : $"XP {c.Xp:0} / {c.Type.UpgradeXp}", body.X + ui.U(48), y + ui.U(18), Rules.UpgradeReady(c) ? Style.Accent : Style.Dim, 1);
                    if (c.Type.UpgradesTo.Length > 0 && ui.Button(new Rectangle(body.Right - ui.U(160), y + ui.U(4), ui.U(160), ui.U(28)), "UPGRADE...", true))
                    { upgrading = c; panel = PanelKind.Upgrade; note = ""; }
                    y += ui.U(42);
                    if (y > body.Bottom - ui.U(30)) break;
                }
                break;
            }
            case "REST":
            {
                ui.Text($"YOUR WOUNDED: {C.Player.Wounded}. THEY HEAL {(s.Kind == SettlementKind.Town ? "QUICKLY IN A TOWN" : "SLOWLY HERE")}.", body.X, body.Y, Style.Text);
                ui.Text("RESTING IN A TOWN ALSO RESTORES COHESION (UP TO 20).", body.X, body.Y + ui.U(20), Style.Dim, 1);
                if (ui.Button(new Rectangle(body.X, body.Y + ui.U(44), ui.U(200), ui.U(28)), "REST 1 DAY")) { C.Rest(24); note = $"RESTED. WOUNDED: {C.Player.Wounded}"; }
                if (ui.Button(new Rectangle(body.X + ui.U(210), body.Y + ui.U(44), ui.U(250), ui.U(28)), "REST UNTIL HEALED", C.Player.Wounded > 0))
                {
                    for (int d = 0; d < 10 && C.Player.Wounded > 0 && C.Encounter == null; d++) C.Rest(24);
                    note = $"RESTED. WOUNDED: {C.Player.Wounded}";
                }
                break;
            }
            case "FOOD":
            {
                ui.Text($"YOU CARRY FOOD FOR {C.Player.Food:0} DAYS. {C.Season.ToUpperInvariant()}{(C.Winter ? ": MEN EAT HALF AGAIN AS MUCH" : "")}.", body.X, body.Y, Style.Text);
                ui.Text("PRISONERS EAT TOO. A STARVING COMPANY LOSES COHESION AND MEN EVERY DAY.", body.X, body.Y + ui.U(20), Style.Dim, 1);
                int y = body.Y + ui.U(44);
                foreach (int days in new[] { 3, 7, 14 })
                {
                    if (ui.Button(new Rectangle(body.X, y, ui.U(300), ui.U(28)), $"BUY {days} DAYS", C.S.Gold >= C.FoodPrice(days), $"{C.FoodPrice(days)} CROWNS")) note = C.BuyFood(days) ?? "BOUGHT";
                    y += ui.U(34);
                }
                break;
            }
            case "FORAGE":
            {
                ui.Text(C.Refuses(s) ? "AN ENEMY VILLAGE. ITS PEOPLE BAR THEIR DOORS." : "YOUR MEN CAN TAKE WHAT THEY NEED BY FORCE.", body.X, body.Y, Style.Text);
                ui.Text("FORAGING GIVES A WEEK OF FOOD, AND LEAVES THE VILLAGE WITH NO RECRUITS FOR A WEEK.", body.X, body.Y + ui.U(20), Style.Dim, 1);
                ui.Text(C.Refuses(s) ? "RAIDING AN ENEMY'S VILLAGE COUNTS IN THE WAR." : "YOUR LIEGE'S PEOPLE WILL REMEMBER IT: -5 RENOWN.", body.X, body.Y + ui.U(34), C.Refuses(s) ? Style.Ok : Style.Warn, 1);
                if (ui.Button(new Rectangle(body.X, body.Y + ui.U(56), ui.U(240), ui.U(28)), "FORAGE", s.ThreatUntil <= C.S.Hours)) note = C.Forage(s) ?? "FORAGED";
                break;
            }
            case "CAPTIVES":
            {
                int y = body.Y;
                if (C.PrisonerCount == 0) ui.Text("YOU HOLD NO PRISONERS. MEN CAPTURED IN BATTLE ARE KEPT UNTIL YOU SELL THEM HERE.", body.X, y, Style.Dim, 1);
                else
                {
                    ui.Text($"{C.PrisonerCount} PRISONERS: " + string.Join(", ", C.Player.Prisoners.Select(g => $"{g.Count} {Troops.Get(g.Troop).Name.ToUpperInvariant()}")), body.X, y, Style.Text, 1);
                    if (ui.Button(new Rectangle(body.X, y + ui.U(18), ui.U(340), ui.U(28)), "SELL THEM ALL", true, $"{C.PrisonerValue} CROWNS")) note = C.SellPrisoners(s) ?? "SOLD";
                }
                y += ui.U(64);
                ui.Text("LORDS YOU HOLD FOR RANSOM", body.X, y, Style.Accent, 1);
                y += ui.U(16);
                foreach (int id in C.Player.Captives.ToList())
                {
                    if (C.PartyById(id) is not Party lord) continue;
                    ui.Text($"{lord.Name.ToUpperInvariant()} OF THE {Campaign.FactionNames[lord.Faction].ToUpperInvariant()}", body.X, y + ui.U(6), SideColor(lord.Faction), 1);
                    if (ui.Button(new Rectangle(body.Right - ui.U(360), y, ui.U(200), ui.U(24)), "RANSOM", true, $"{C.LordRansom(lord)}")) note = C.RansomLord(lord, s) ?? "RANSOMED";
                    if (ui.Button(new Rectangle(body.Right - ui.U(150), y, ui.U(150), ui.U(24)), "FREE (+RENOWN)")) C.FreeLord(lord);
                    y += ui.U(30);
                }
                if (C.Player.Captives.Count == 0) ui.Text("NONE. A LORD BEATEN BY YOUR HAND MAY BE TAKEN.", body.X, y, Style.Dim, 1);
                break;
            }
            case "GARRISON":
            {
                ui.Text($"YOUR FIEF. IT PAYS YOU {C.Income(s) + C.S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village).Sum(C.Income):0} CROWNS A WEEK. SUPPLIES {s.Supplies:0} DAYS.", body.X, body.Y, Style.Text, 1);
                int y = body.Y + ui.U(18);
                foreach (var g in s.Garrison.ToList())
                {
                    ui.Text($"{g.Name.ToUpperInvariant()}: {g.Men} {g.Type.Name.ToUpperInvariant()}", body.X, y + ui.U(6), Style.Text, 1);
                    if (ui.Button(new Rectangle(body.Right - ui.U(150), y, ui.U(150), ui.U(22)), "TAKE WITH YOU")) note = C.Withdraw(g, s) ?? "";
                    y += ui.U(26);
                }
                y += ui.U(8);
                ui.Text("LEAVE A COMPANY TO HOLD THE WALLS:", body.X, y, Style.Dim, 1);
                y += ui.U(16);
                foreach (var c in C.Player.Companies.ToList())
                {
                    if (y > body.Bottom - ui.U(20)) break;
                    if (ui.Button(new Rectangle(body.X, y, ui.U(420), ui.U(22)), $"STATION {c.Name.ToUpperInvariant()} ({c.Men})", C.Player.Companies.Count > 1)) note = C.Station(c, s) ?? "";
                    y += ui.U(26);
                }
                break;
            }
            case "CONTRACT":
            {
                int cur = C.Captain.Contract;
                if (s.Kind == SettlementKind.Town && cur == s.Faction)
                {
                    ui.Text("THE STEWARD'S BOARD", body.X, body.Y + ui.U(130), Style.Accent, 1);
                    int y = body.Y + ui.U(146);
                    foreach (var o in C.Offers(s).ToList())
                    {
                        ui.Text(o.Text.ToUpperInvariant(), body.X, y + ui.U(6), Style.Text, 1);
                        if (ui.Button(new Rectangle(body.Right - ui.U(200), y, ui.U(200), ui.U(24)), "ACCEPT", C.S.Contracts.Count < 3, $"{o.Reward:0}")) note = C.Accept(s, o) ?? "ACCEPTED";
                        y += ui.U(28);
                        if (y > body.Bottom - ui.U(24)) break;
                    }
                }
                ui.Text(cur >= 0 ? $"YOU SERVE THE {Campaign.FactionNames[cur].ToUpperInvariant()}." : "YOU ARE A FREE CAPTAIN: NO PAY, NO ENEMIES BUT BRIGANDS.", body.X, body.Y, Style.Text);
                ui.Text("A CONTRACT PAYS 85% OF YOUR UPKEEP WEEKLY, AND BOUNTIES FOR BRIGANDS AND ENEMY LORDS.", body.X, body.Y + ui.U(20), Style.Dim, 1);
                ui.Text("YOUR EMPLOYER'S ENEMIES BECOME YOURS.", body.X, body.Y + ui.U(34), Style.Dim, 1);
                if (cur != s.Faction && ui.Button(new Rectangle(body.X, body.Y + ui.U(56), ui.U(380), ui.U(28)), $"SIGN WITH THE {Campaign.FactionNames[s.Faction].ToUpperInvariant()}"))
                    C.SignContract(s.Faction);
                if (cur >= 0 && ui.Button(new Rectangle(body.X, body.Y + ui.U(92), ui.U(380), ui.U(28)), "END YOUR CONTRACT")) C.SignContract(-1);
                break;
            }
        }
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "LEAVE")) panel = PanelKind.None;
    }

    void PartyPanel()
    {
        var r = Modal(980, 560, "YOUR COMPANY");
        int x = r.X + ui.U(20), y = r.Y + ui.U(50);
        var list = C.Player.Companies;
        foreach (var c in list.ToList())
        {
            var row = new Rectangle(x, y, ui.U(660), ui.U(46));
            ui.Rect(row, Color.White * 0.04f);
            ui.Portrait(c.Type, new Rectangle(row.X + ui.U(4), row.Y + ui.U(2), ui.U(44), ui.U(42)));
            var nameBox = new Rectangle(row.X + ui.U(52), row.Y + ui.U(3), ui.U(200), ui.U(12));
            if (renaming == c) { ui.Rect(nameBox, Style.Accent * 0.25f); ui.Text(renameText + (time % 1 < 0.5f ? "_" : ""), row.X + ui.U(54), row.Y + ui.U(5), Style.Accent, 1); }
            else ui.Text(c.Name.ToUpperInvariant(), row.X + ui.U(54), row.Y + ui.U(5), Style.Text, 1);
            if (renaming == null && ui.Clicked(nameBox)) { renaming = c; renameText = c.Name.ToUpperInvariant(); Ui.Typed.Clear(); }
            if (ui.Hover(nameBox) && renaming == null) note = "CLICK TO RENAME (ENTER KEEPS IT)";
            ui.Text($"{c.Type.Name.ToUpperInvariant()} {Style.Numerals[c.Type.Tier]}", row.X + ui.U(54), row.Y + ui.U(17), Style.Accent, 1);
            if (c.Officer != "")
            {
                var off = new Rectangle(row.X + ui.U(150), row.Y + ui.U(16), ui.U(104), ui.U(10));
                ui.Text(c.Officer.ToUpperInvariant() + (c.Traits.Count > 0 ? $" ({c.Traits.Count})" : ""), off.X, off.Y + 1, Style.Dim, 1);
                if (ui.Hover(off)) note = c.Traits.Count == 0 ? $"{c.Officer.ToUpperInvariant()}: NO TRAITS YET (WON BATTLES MAY GIVE THEM)"
                    : $"{c.Officer.ToUpperInvariant()}: " + string.Join(", ", c.Traits.Select(t => $"{t.ToUpperInvariant()} - {Campaign.TraitText[t]}"));
            }
            ui.Text($"{c.Men}{(c.Wounded > 0 ? $"+{c.Wounded}W" : "")}/{Rules.Cap(c.Troop, Rules.RoleOf(c))}  {c.Battles} BATTLES {c.Kills} KILLS", row.X + ui.U(54), row.Y + ui.U(30), Style.Dim, 1);
            float need = c.Type.UpgradeXp;
            ui.Text("XP", row.X + ui.U(262), row.Y + ui.U(8), Style.Dim, 1);
            ui.Bar(new Rectangle(row.X + ui.U(282), row.Y + ui.U(9), ui.U(100), ui.U(5)), need > 0 ? c.Xp / need : 1, Rules.UpgradeReady(c) ? Style.Accent : Style.Friendly);
            ui.Text("COH", row.X + ui.U(256), row.Y + ui.U(24), Style.Dim, 1);
            ui.Bar(new Rectangle(row.X + ui.U(282), row.Y + ui.U(25), ui.U(100), ui.U(5)), c.Cohesion / 30f, Style.Ok);
            int bx = row.X + ui.U(392), bw = ui.U(62), bh = ui.U(20);
            if (ui.Button(new Rectangle(bx, row.Y + ui.U(3), bw, bh), "UP", list.IndexOf(c) > 0)) C.MoveUp(c);
            if (ui.Button(new Rectangle(bx + bw + 4, row.Y + ui.U(3), bw, bh), "SPLIT", c.Men >= 2)) note = C.Split(c) ?? "";
            var mate = list.FirstOrDefault(o => Rules.CanMerge(c, o));
            if (ui.Button(new Rectangle(bx + 2 * (bw + 4), row.Y + ui.U(3), bw, bh), "MERGE", mate != null)) C.Merge(c, mate!);
            if (ui.Button(new Rectangle(bx, row.Y + ui.U(24), bw * 2 + 4, bh), "UPGRADE...", c.Type.UpgradesTo.Length > 0)) { upgrading = c; panel = PanelKind.Upgrade; note = ""; }
            if (ui.Button(new Rectangle(bx + 2 * (bw + 4), row.Y + ui.U(24), bw, bh), "DISBAND", list.Count > 1)) C.Disband(c);
            y += ui.U(50);
        }
        // The captain and perks.
        int px = r.X + ui.U(700), py = r.Y + ui.U(50);
        var cap = C.Captain;
        ui.Text("THE CAPTAIN", px, py, Style.Accent);
        ui.Text($"RENOWN {cap.Renown:0}   PERK POINTS {Rules.PerkPoints(cap)}", px, py + ui.U(22), Style.Text, 1);
        ui.Text($"{Rules.CompanySlots(cap)} COMPANIES, {Rules.MenLimit(cap)} MEN AT MOST", px, py + ui.U(36), Style.Dim, 1);
        py += ui.U(56);
        foreach (Perk perk in Enum.GetValues<Perk>())
        {
            bool has = cap.Perks.Contains(perk);
            var br = new Rectangle(px, py, ui.U(260), ui.U(24));
            if (ui.Button(br, perk.ToString().ToUpperInvariant(), !has && Rules.PerkPoints(cap) > 0, has ? "TAKEN" : "")) C.TakePerk(perk);
            if (ui.Hover(br)) note = Rules.PerkText[perk];
            py += ui.U(28);
        }
        // Prisoners: turn them to your cause (green, cohesion 0) or let them go.
        py += ui.U(8);
        ui.Text($"PRISONERS {C.PrisonerCount}", px, py, Style.Accent);
        py += ui.U(20);
        foreach (var g in C.Player.Prisoners.ToList())
        {
            if (py > r.Bottom - ui.U(90)) break;
            if (ui.Button(new Rectangle(px, py, ui.U(260), ui.U(22)), $"RECRUIT {g.Count} {Troops.Get(g.Troop).Name.ToUpperInvariant()}")) note = C.RecruitPrisoners(g) ?? "";
            py += ui.U(26);
        }
        if (C.PrisonerCount > 0 && ui.Button(new Rectangle(px, py, ui.U(260), ui.U(22)), "FREE THEM ALL (+RENOWN)")) C.FreePrisoners();
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE")) { panel = PanelKind.None; renaming = null; }
    }

    /// Your accepted contracts, under the overview map.
    void Contracts()
    {
        if (C.S.Contracts.Count == 0) return;
        int w = ui.U(230), x = ui.W - w - ui.U(10), y = ui.U(194);
        var r = new Rectangle(x, y, w, ui.U(20) + C.S.Contracts.Count * ui.U(26));
        ui.Panel(r);
        ui.Text("CONTRACTS", x + ui.U(8), y + ui.U(6), Style.Accent, 1);
        y += ui.U(20);
        foreach (var c in C.S.Contracts)
        {
            string t = c.Text.ToUpperInvariant();
            if (t.Length > 36) t = t[..35] + ".";
            ui.Text(t, x + ui.U(8), y, Style.Text, 1);
            float days = (c.Deadline - (float)C.S.Hours) / 24;
            ui.Text($"{days:0.0} DAYS  {c.Reward:0} CROWNS" + (c.Kind == "defend" ? $"  {c.Progress:0}/24 H" : ""), x + ui.U(8), y + ui.U(11), days < 2 ? Style.Warn : Style.Dim, 1);
            y += ui.U(26);
        }
    }

    /// At the walls of an enemy town or castle: besiege, assault, wait or leave.
    void SiegePanel()
    {
        var s = at!;
        var r = Modal(680, 400, (C.Besieging == s ? "THE SIEGE OF " : "") + s.Name.ToUpperInvariant(), SideColor(s.Faction));
        int x = r.X + ui.U(24), y = r.Y + ui.U(50);
        ui.Text($"{s.Kind.ToString().ToUpperInvariant()} OF THE {Campaign.FactionNames[s.Faction].ToUpperInvariant()}", x, y, Style.Dim, 1);
        y += ui.U(18);
        ui.Text($"GARRISON {C.GarrisonMen(s)} MEN   SUPPLIES {s.Supplies:0} DAYS", x, y, Style.Text);
        y += ui.U(24);
        foreach (var g in s.Garrison.Take(4))
        {
            ui.Portrait(g.Type, new Rectangle(x, y, ui.U(30), ui.U(28)));
            ui.Text($"{g.Men} {g.Type.Name.ToUpperInvariant()}", x + ui.U(36), y + ui.U(10), Style.Text, 1);
            x += ui.U(148);
        }
        x = r.X + ui.U(24);
        y += ui.U(40);
        var helpers = C.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && p.Faction == s.Faction && Vector2.Distance(p.Pos, s.Pos) < 10).ToList();
        if (helpers.Count > 0) ui.Text("INSIDE: " + string.Join(", ", helpers.Select(h => h.Name.ToUpperInvariant())), x, y, Style.Foe, 1);
        y += ui.U(18);
        bool besieging = C.Besieging == s;
        if (besieging)
        {
            float wait = C.LaddersIn(s);
            ui.Text(wait > 0 ? $"LADDERS AND A RAM IN {wait:0} HOURS. ASSAULTING NOW MEANS THE GATE ALONE." : "LADDERS ARE BUILT AND THE RAM IS READY.", x, y, wait > 0 ? Style.Warn : Style.Ok, 1);
            y += ui.U(14);
            ui.Text("STARVING THEM OUT: EACH DAY COSTS THEM A DAY OF SUPPLIES, THEN MEN.", x, y, Style.Dim, 1);
        }
        else ui.Text("YOUR MEN WILL NEED A NIGHT TO BUILD LADDERS AND A RAM.", x, y, Style.Dim, 1);
        Note(r);
        int bw = ui.U(156), by = r.Bottom - ui.U(40), bx = r.X + (r.Width - 4 * bw - 3 * ui.U(8)) / 2;
        Rectangle Bt(int i) => new(bx + i * (bw + ui.U(8)), by, bw, ui.U(30));
        if (!besieging)
        {
            bool ok = C.CanBesiege(s, out string why);
            if (ui.Button(Bt(0), "BESIEGE", ok)) { C.Besiege(s); }
            if (!ok && ui.Hover(Bt(0))) note = why;
        }
        else
        {
            if (ui.Button(Bt(0), "ASSAULT", C.Player.Men > 0)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareAssault(s)); return; }
            if (ui.Button(Bt(1), "AUTO-RESOLVE", C.Player.Men > 0)) { C.AutoResolveAssault(s); ShowResults(); return; }
            if (ui.Button(Bt(2), "WAIT 12 H")) { C.Rest(12); if (C.Encounter != null) panel = PanelKind.None; }
        }
        if (ui.Button(Bt(3), besieging ? "LIFT SIEGE" : "LEAVE")) { if (besieging) C.LiftSiege(); panel = PanelKind.None; }
    }

    void UpgradePanel()
    {
        var c = upgrading!;
        var r = Modal(860, 440, "UPGRADE " + c.Name.ToUpperInvariant());
        ui.TextC($"{c.Men + c.Wounded} {c.Type.Name.ToUpperInvariant()}, XP {c.Xp:0} / {c.Type.UpgradeXp}. THE WHOLE COMPANY CHANGES; XP STARTS AGAIN.",
            r.X + r.Width / 2f, r.Y + ui.U(44), Style.Dim, 1);
        var options = c.Type.UpgradesTo;
        int cw = ui.U(190), gap = ui.U(12), x = r.X + (r.Width - options.Length * cw - (options.Length - 1) * gap) / 2, y = r.Y + ui.U(66);
        foreach (var to in options)
        {
            var t = Troops.Get(to);
            var card = new Rectangle(x, y, cw, ui.U(290));
            bool ok = C.CanUpgrade(c, to, out int cost, out string reason);
            ui.Panel(card, ok ? Style.Accent : Style.PanelEdge, ok ? 2 : 1);
            ui.Portrait(t, new Rectangle(card.X + ui.U(40), card.Y + ui.U(8), cw - ui.U(80), ui.U(70)));
            ui.TextC(t.Name.ToUpperInvariant(), card.X + cw / 2f, card.Y + ui.U(86), Style.Text);
            ui.TextC($"TIER {Style.Numerals[t.Tier]}  {t.Class.ToString().ToUpperInvariant()}", card.X + cw / 2f, card.Y + ui.U(106), Style.Accent, 1);
            int sy = card.Y + ui.U(124);
            foreach (var (label, value) in new[] { ("HEALTH", $"{t.MaxHp:0}"), ("ARMOUR", $"{t.Armor:0}"), ("DAMAGE", $"{t.Damage:0}"), ("REACH", $"{t.Reach:0.0} M"),
                         t.Range > 0 ? ("RANGE", $"{t.Range:0} M") : ("SPEED", $"{t.Speed:0.0}"), ("HORSE", t.Mount?.Name.ToUpperInvariant() ?? "-") })
            {
                ui.Text(label, card.X + ui.U(14), sy, Style.Dim, 1);
                ui.TextR(value, card.Right - ui.U(14), sy, Style.Text, 1);
                sy += ui.U(14);
            }
            ui.TextC($"{cost} CROWNS", card.X + cw / 2f, card.Bottom - ui.U(64), ok ? Style.Text : Style.Dim);
            if (!ok) ui.TextC(reason, card.X + cw / 2f, card.Bottom - ui.U(44), Style.Warn, 1);
            if (ui.Button(new Rectangle(card.X + ui.U(14), card.Bottom - ui.U(32), cw - ui.U(28), ui.U(24)), "UPGRADE", ok)) { note = C.Upgrade(c, to) ?? ""; panel = PanelKind.Party; }
            x += cw + gap;
        }
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "BACK")) panel = at != null && C.SettlementAt(C.Player.Pos) == at ? PanelKind.Settlement : PanelKind.Party;
    }

    void ResultsPanel()
    {
        var rep = C.Report;
        if (rep == null) { panel = PanelKind.None; return; }
        var r = Modal(900, 520, rep.Won ? "VICTORY" : "DEFEAT", rep.Won ? Style.Accent : Style.Bad);
        ui.TextC("AGAINST " + rep.Enemy.ToUpperInvariant(), r.X + r.Width / 2f, r.Y + ui.U(44), Style.Dim, 1);
        int x = r.X + ui.U(24), y = r.Y + ui.U(64);
        string[] heads = { "COMPANY", "MEN", "KILLED", "WOUNDED", "LOST", "XP" };
        int[] cols = { 0, 330, 450, 540, 640, 720 };
        for (int i = 0; i < heads.Length; i++) ui.Text(heads[i], x + ui.U(cols[i]), y, Style.Dim, 1);
        y += ui.U(16);
        foreach (var c in rep.Companies.Where(c => c.Team == 0).Take(12))
        {
            ui.Text($"{c.Name.ToUpperInvariant()} ({Troops.Get(c.Troop).Name.ToUpperInvariant()})", x, y, Style.Text, 1);
            ui.Text($"{c.MenBefore} -> {c.MenAfter}", x + ui.U(cols[1]), y, Style.Text, 1);
            ui.Text($"{c.Killed}", x + ui.U(cols[2]), y, c.Killed > 0 ? Style.Bad : Style.Dim, 1);
            ui.Text($"{c.Wounded}", x + ui.U(cols[3]), y, Style.Warn, 1);
            ui.Text($"{c.Captured + c.FledLost}", x + ui.U(cols[4]), y, Style.Dim, 1);
            ui.Text(c.Deployed ? $"+{c.XpGain:0}" : "RESERVE", x + ui.U(cols[5]), y, Style.Ok, 1);
            if (c.UpgradeReady) ui.Text("UPGRADE READY", x + ui.U(770), y, Style.Accent, 1);
            y += ui.U(16);
        }
        y += ui.U(10);
        if (rep.Won)
            ui.Text($"LOOT {rep.Loot:0}   RANSOMS {rep.Ransom:0}   BOUNTY {rep.Bounty:0}   RENOWN +{rep.Renown:0}   HORSES +{rep.Horses}", x, y, Style.Accent);
        else ui.Text("YOUR ARMY FALLS BACK. WOUNDED LEFT ON THE FIELD MAY BE TAKEN.", x, y, Style.Bad);
        y += ui.U(22);
        if (rep.Captain == LeaderFate.Wounded) ui.Text("YOU ARE WOUNDED: FOR A FEW DAYS YOU COMMAND FROM THE REAR.", x, y, Style.Warn, 1);
        if (rep.Captain == LeaderFate.Captured) ui.Text("YOU WERE TAKEN AND RANSOMED (30% OF YOUR GOLD).", x, y, Style.Bad, 1);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CONTINUE"))
        { panel = C.S.GameOver ? PanelKind.GameOver : PanelKind.None; C.Report = null; }
    }

    /// Ironman: the captain was taken and executed. The campaign (and its save) ends here.
    void GameOverPanel()
    {
        var r = Modal(560, 260, "YOUR WAR IS OVER", Style.Bad);
        ui.TextC($"YOU LED YOUR COMPANY FOR {C.Day} DAYS AND WON {C.Captain.Renown:0} RENOWN.", r.X + r.Width / 2f, r.Y + ui.U(70), Style.Text, 1);
        ui.TextC("THE ENEMY DID NOT RANSOM YOU.", r.X + r.Width / 2f, r.Y + ui.U(90), Style.Dim, 1);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(110), r.Bottom - ui.U(50), ui.U(220), ui.U(32)), "TO THE TITLE"))
        {
            var p = Path.Combine(CampaignState.SaveDir, "autosave.json");
            if (File.Exists(p)) File.Delete(p);
            OnTitle?.Invoke();
        }
    }

    void MenuPanel()
    {
        var r = Modal(300, 310, "PAUSED");
        int y = r.Y + ui.U(54);
        Rectangle B() { var b = new Rectangle(r.X + ui.U(40), y, r.Width - ui.U(80), ui.U(30)); y += ui.U(38); return b; }
        if (ui.Button(B(), "RESUME")) panel = PanelKind.None;
        if (ui.Button(B(), $"SAVE ({ctl.Name(Act.QuickSave)})")) { Save(); panel = PanelKind.None; }
        if (ui.Button(B(), $"LOAD ({ctl.Name(Act.QuickLoad)})", !C.S.Ironman && CampaignState.Exists("quicksave")) && CampaignState.Load("quicksave") is CampaignState s) Open(new Campaign(s));
        if (ui.Button(B(), "SETTINGS")) OnSettings?.Invoke();
        if (ui.Button(B(), "QUIT TO TITLE")) { C.S.Save("autosave"); OnTitle?.Invoke(); }
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
            case "map": panel = PanelKind.None; zoom = 130; pitch = 0.95f; break;
            case "close": panel = PanelKind.None; zoom = 45; pitch = 0.7f; break;
            case "party": panel = PanelKind.Party; break;
            case "settlement":
                at = C.S.Settlements.First(s => s.Kind == SettlementKind.Town && !C.Refuses(s));
                tab = "MERCENARIES"; panel = PanelKind.Settlement; break;
            case "upgrade": upgrading = C.Player.Companies[0]; panel = PanelKind.Upgrade; break;
            case "encounter": panel = PanelKind.Encounter; break;
            case "results": panel = PanelKind.Results; break;
        }
    }
}
