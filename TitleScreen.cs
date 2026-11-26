using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The title screen: the name and the menu down the left of a painted dusk (TitleScreen.Scene.cs), and a card for the last save.
/// New campaign, continue, load (the save screen, SaveScreen.cs), quick battle, settings, quit; the mouse or the arrow keys and Enter.
sealed partial class TitleScreen
{
    readonly ScreenHost host;
    readonly Ui ui;
    MouseState ms, pms;
    KeyboardState kb, pkb;
    bool click;
    readonly CreatorScreen creator;
    readonly QuickBattleScreen quickBattle;
    // Debug: IRONFIELD_CREATOR_SHOTS=/dir saves a PNG of each creator step, IRONFIELD_TITLE_SHOTS=/dir one of the title; then quits.
    static readonly string? creatorShots = Environment.GetEnvironmentVariable("IRONFIELD_CREATOR_SHOTS") is { Length: > 0 } d ? d : null;
    static readonly string? titleShots = Environment.GetEnvironmentVariable("IRONFIELD_TITLE_SHOTS") is { Length: > 0 } d ? d : null;
    static readonly string? qbShots = Environment.GetEnvironmentVariable("IRONFIELD_QB_SHOTS") is { Length: > 0 } d ? d : null;
    public readonly string? ShotsDir = creatorShots ?? titleShots ?? qbShots;
    public string? PendingShot;
    int shotStep = -1, shotFrames;
    float time;
    public bool InCreator => creator.Open;
    public void OpenQuickBattle(QuickBattleConfig? cfg = null) => quickBattle.Show(cfg);
    public string CreatorCulture => creator.CurrentCulture;
    /// LOAD: every campaign and its saves.
    readonly SaveScreen saves = new();
    readonly LegendScreen legends = new();

    public TitleScreen(ScreenHost host)
    {
        this.host = host;
        ui = new Ui(host.Sb, host.Art, host.Font);
        saves.OnLoad = p => { if (host.LoadSave(p) is string why) host.TitleNote = why; };
        creator = new CreatorScreen(ui, m => host.StartCampaign(Environment.TickCount & 0xFFFF, m.Realm, m), new CreatorStage(host.GraphicsDevice, host.Art, host.Shading, host.MeshKit, () => host.Quality));
        quickBattle = new QuickBattleScreen(ui, host.QuickBattle);
        if (creatorShots != null || Environment.GetEnvironmentVariable("IRONFIELD_TITLE_PICK") != null) creator.Show();
        if (qbShots != null) quickBattle.Show();
    }

    float lastDt = 1 / 60f;
    // The menu: the chosen row follows the mouse when it moves and the arrow keys otherwise.
    static readonly string[] Items = { "NEW CAMPAIGN", "CONTINUE", "LOAD", "LEGENDS", "QUICK BATTLE", "SETTINGS", "QUIT" };
    int sel = -1, nav;
    bool go, mouseMoved;

    public void Update(float dt = 1 / 60f)
    {
        pms = ms;
        ms = Mouse.GetState();
        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) click = true;
        if (ms.Position != pms.Position) mouseMoved = true;
        pkb = kb; kb = Keyboard.GetState();
        bool Hit(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
        if (saves.Open) { if (Hit(Keys.Escape)) saves.Close(); }
        else if (legends.Open) { if (Hit(Keys.Escape)) legends.Close(); }
        else if (!creator.Open && !quickBattle.Open)
        {
            if (Hit(Keys.Down) || Hit(Keys.S)) nav++;
            if (Hit(Keys.Up) || Hit(Keys.W)) nav--;
            if (Hit(Keys.Enter)) go = true;
        }
        lastDt = dt;
        time = titleShots != null ? 3f : time + dt;
    }

    int U(float v) => ui.U(v);

    /// Start a frame of the picture (the title, the loading screen).
    void BeginPicture()
    {
        var gd = host.GraphicsDevice;
        gd.Clear(Kit.Night);
        ui.Begin(gd.Viewport, ms.Position, click);
        click = false;
    }

    public void Draw()
    {
        if (titleShots != null && shotFrames >= 24) { DrawLoading("SHAPING THE LAND AND ITS REALMS"); TitleShot(); return; }
        creator.Prepare(lastDt); // the living captain, drawn to its texture before the frame's sprites begin
        BeginPicture();
        var save = Saves.Latest()?.Meta;
        Scene();
        if (creator.Open)
        {
            nav = 0; go = false;
            creator.Draw(kb, pkb, time);
            ui.End();
            if (creatorShots != null) Shots();
            return;
        }
        if (quickBattle.Open)
        {
            nav = 0; go = false;
            quickBattle.Draw(kb, pkb, ms.ScrollWheelValue - pms.ScrollWheelValue);
            ui.End();
            if (qbShots != null) QbShots();
            return;
        }

        int x = U(64), ty = (int)(ui.H * 0.115f), k = Math.Max(U(9), ui.H / 60);
        Logo(x, ty, k);
        Sword(x, ty + 7 * k + U(14), 53 * k);
        Spaced("A CAPTAIN'S WAR", x + U(40), ty + 7 * k + U(34), Kit.Parchment, 2, U(5));
        if (saves.Open) nav = 0;
        int menuY = (int)(ui.H * 0.39f);
        Menu(x, menuY, save);
        if (host.TitleNote != "") ui.Text(host.TitleNote, x, menuY + Items.Length * U(35) + U(10), Style.Warn, 1);
        if (save != null) Company(save, x, ui.H - U(100));
        if (saves.Open) saves.Draw(ui, ms.ScrollWheelValue - pms.ScrollWheelValue, kb, pkb);
        else if (legends.Open) legends.Draw(ui, ms.ScrollWheelValue - pms.ScrollWheelValue, kb, pkb);
        string hints = "W/S CHOOSE   ENTER SELECT   F2 GRAPHICS";
        int hw = ui.TW(hints, 1) + U(16);
        var hPill = new Rectangle(ui.W - U(18) - hw, ui.H - U(24), hw, U(15));
        ui.Rect(hPill, Color.Black * 0.52f); ui.Frame(hPill, Kit.Gilt * 0.25f, U(1));
        ui.TextR(hints, ui.W - U(24), ui.H - U(20), Style.Text * 0.88f, 1);
        ui.End();
        if (titleShots != null) TitleShot();
    }

    /// The name in tall gilt letters: each pixel of the font a bevelled block, bright at the top and bronze at the foot,
    /// standing on a dark extrusion, with a gleam that crosses it now and then.
    void Logo(int x, int y, int k)
    {
        const string word = "IRONFIELD";
        int b = Math.Max(1, k / 6);
        float gleam = Ui.Instant ? -99 : (time % 9f) * 30 - 12; // in font pixels, left to right
        bool On(int ch, int col, int row) => ch >= 0 && ch < word.Length && PixelFont.Lit(word[ch], col, row);
        for (int pass = 0; pass < 3; pass++)
            for (int ch = 0; ch < word.Length; ch++)
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                    {
                        if (!On(ch, col, row)) continue;
                        int px = x + (ch * 6 + col) * k, py = y + row * k;
                        if (pass == 0) { ui.Rect(new Rectangle(px + k * 3 / 4, py + k, k, k), Color.Black * 0.35f); continue; }
                        if (pass == 1) { ui.Rect(new Rectangle(px + k / 3, py + k / 2, k, k), new Color(52, 30, 18)); continue; }
                        float f = row / 6f;
                        var c = f < 0.45f ? Color.Lerp(new Color(255, 238, 172), Kit.Gilt, f / 0.45f) : Color.Lerp(Kit.Gilt, new Color(150, 100, 40), (f - 0.45f) / 0.55f);
                        float g = MathF.Max(0, 1 - MathF.Abs(ch * 6 + col + row * 0.6f - gleam) / 2.5f);
                        c = Color.Lerp(c, new Color(255, 252, 232), g * 0.7f);
                        ui.Rect(new Rectangle(px, py, k, k), c);
                        if (!On(ch, col, row - 1)) ui.Rect(new Rectangle(px, py, k, b), Color.Lerp(c, Color.White, 0.55f));
                        if (!On(ch, col - 1, row)) ui.Rect(new Rectangle(px, py, b, k), Color.Lerp(c, Color.White, 0.3f));
                        if (!On(ch, col + 1, row)) ui.Rect(new Rectangle(px + k - b, py, b, k), Kit.Mul(c, 0.72f));
                        if (!On(ch, col, row + 1)) ui.Rect(new Rectangle(px, py + k - b, k, b), Kit.Mul(c, 0.55f));
                    }
    }

    /// A sword laid under the name, hilt to the left: pommel, grip, cross and a long blade tapering to its point.
    /// `lit` under 1 (the loading screen) leaves the blade past that share of its length in cold iron, a flare where the light ends.
    void Sword(int x, int y, int w, float lit = 1)
    {
        int u = Math.Max(1, U(1));
        var steel = new Color(206, 212, 222); var shade = new Color(128, 136, 152);
        ui.Rect(new Rectangle(x + U(4), y + U(5), w - U(10), U(2)), Color.Black * 0.35f);
        ui.Disc(new Vector2(x + U(5), y), U(5), Kit.GiltDark); ui.Disc(new Vector2(x + U(5), y - u), U(3.5f), Kit.Gilt);
        ui.Rect(new Rectangle(x + U(9), y - U(2), U(22), U(5)), new Color(84, 52, 36));
        for (int i = 0; i < 5; i++) ui.Rect(new Rectangle(x + U(11 + i * 4), y - U(2), u, U(5)), new Color(52, 32, 24));
        ui.Rect(new Rectangle(x + U(31), y - U(11), U(5), U(23)), Kit.GiltDark);
        ui.Rect(new Rectangle(x + U(31), y - U(11), U(3), U(22)), Kit.Gilt);
        ui.Rect(new Rectangle(x + U(31), y - U(11), U(5), U(2)), new Color(255, 232, 160));
        int bx = x + U(36), bw = w - U(36), tip = U(22), body = bx + bw - tip;
        // The blade from `from` on to its point.
        void Blade(int from, Color top, Color under, Color edge, Color fuller)
        {
            void Run(int x0, int x1, int ry, int rh, Color c) { x0 = Math.Max(x0, from); if (x1 > x0) ui.Rect(new Rectangle(x0, ry, x1 - x0, rh), c); }
            Run(bx, body, y - U(3), U(3), top);
            Run(bx, body, y, U(3), under);
            Run(bx, body, y - U(3), u, edge);
            Run(bx + U(4), bx + U(4) + (bw - tip) * 3 / 4, y - u / 2, u, fuller);
            for (int i = 0; i < 3; i++) // the point, narrowing in three steps
            {
                int h = U(2 - i) + (i == 2 ? u : 0), sx = body + i * tip / 3;
                Run(sx, sx + tip / 3, y - h, h, top);
                Run(sx, sx + tip / 3, y, h, under);
            }
        }
        Blade(bx, steel, shade, Color.White * 0.8f, new Color(86, 92, 108));
        if (lit >= 1) return;
        int head = bx + (int)(bw * Math.Clamp(lit, 0, 1));
        Blade(head, new Color(58, 62, 76), new Color(36, 40, 52), new Color(84, 90, 106), new Color(24, 26, 34));
        Flare(head, y);
    }

    /// Text with its letters set apart by `gap` pixels.
    void Spaced(string s, float x, float y, Color c, int scale, int gap)
    {
        foreach (char ch in s) { ui.Text(ch.ToString(), x, y, c, scale); x += ui.TW("A", scale) + gap; }
    }

    /// The menu down the left: plain rows on a gilt spine; the chosen row lights and slides out.
    void Menu(int x, int y, SaveMeta? save)
    {
        bool hasSaves = Saves.Campaigns().Count > 0;
        int legCount = LegendArchive.Load().Count;
        bool[] can = { true, host.CanContinue, hasSaves, legCount > 0, true, true, true };
        if (sel < 0 || !can[sel]) sel = can[1] ? 1 : 0;
        for (; nav != 0; nav -= Math.Sign(nav))
            do sel = (sel + Math.Sign(nav) + Items.Length) % Items.Length; while (!can[sel]);
        int w = U(270), h = U(30), gap = U(5), totalH = Items.Length * (h + gap) - gap;
        ui.HGrad(new Rectangle(x - U(22), y - U(10), w + U(96), totalH + U(20)), Color.Black * 0.64f, Color.Black * 0f, 4);
        ui.HGrad(new Rectangle(x - U(10), y - U(10), w + U(40), Math.Max(1, U(1))), Kit.Gilt * 0.48f, Kit.Gilt * 0f, 4);
        ui.HGrad(new Rectangle(x - U(10), y + totalH + U(9), w + U(40), Math.Max(1, U(1))), Kit.Gilt * 0.36f, Kit.Gilt * 0f, 4);
        ui.Rect(new Rectangle(x - U(10), y - U(10), Math.Max(1, U(2)), totalH + U(20)), Kit.Gilt * 0.50f);
        int hit = -1;
        for (int i = 0; i < Items.Length; i++)
        {
            var r = new Rectangle(x - U(10), y + i * (h + gap), w, h);
            if (can[i] && mouseMoved && !saves.Open && !legends.Open && ui.Hover(r)) sel = i;
            bool hot = sel == i;
            float a = Ui.Anim(0x7171 + i, hot ? 1 : 0, 16);
            Overflow.Box(r);
            if (a > 0.01f)
            {
                ui.HGrad(r, Kit.Gilt * (0.42f * a), Kit.Gilt * 0f, 4);
                ui.HGrad(new Rectangle(r.X, r.Y, r.Width, Math.Max(1, U(1))), Kit.Gilt * a, Kit.Gilt * 0f, 4);
                ui.HGrad(new Rectangle(r.X, r.Bottom - Math.Max(1, U(1)), r.Width, Math.Max(1, U(1))), Kit.Gilt * (0.6f * a), Kit.Gilt * 0f, 4);
                ui.Rect(new Rectangle(r.X, r.Y, U(3), r.Height), Color.Lerp(Kit.Gilt, new Color(255, 238, 172), 0.5f) * a);
                for (int j = 0; j < 4; j++) // the pointer: a small gilt arrowhead
                    ui.Rect(new Rectangle(r.X + U(10 + j), r.Center.Y - U(4 - j), U(2), U(8 - 2 * j)), new Color(255, 238, 172) * a);
            }
            var c = !can[i] ? Style.Dim * 0.72f : Color.Lerp(Kit.Parchment, new Color(255, 250, 230), a);
            ui.Text(Items[i], r.X + U(26) + U(3) * a, r.Y + (h - U(14)) / 2f, c, 2);
            string note = i == 1 ? (save != null ? $"DAY {save.Day}" : can[1] ? "" : "NO SAVE")
                : i == 3 ? (legCount > 0 ? $"{legCount} SEALED" : "NONE YET") : "";
            if (note != "") ui.TextR(note, r.Right - U(12), r.Y + (h - U(7)) / 2f, i == 3 && can[i] ? Kit.Gilt : Style.Dim * (can[i] ? 1 : 0.78f), 1);
            ui.Block(r);
            if (can[i] && !saves.Open && !legends.Open && ui.Clicked(r)) hit = i;
        }
        mouseMoved = false;
        if (go && can[sel] && !saves.Open && !legends.Open) hit = sel;
        go = false;
        if (hit < 0) return;
        Ui.OnClick?.Invoke();
        switch (hit)
        {
            case 0: creator.Show(); break;
            case 1: host.ContinueCampaign(); break;
            case 2: saves.Show(SaveScreen.Use.Load, null); break;
            case 3: legends.Show(); break;
            case 4: quickBattle.Show(); break;
            case 5: host.OpenSettings(); break;
            default: host.Exit(); break;
        }
    }

    /// The last save as the company's card: its arms, the captain, whom he serves, the day, the purse and the men.
    void Company(SaveMeta save, int x, int y)
    {
        var c = ui.Plate(new Rectangle(x - U(10), y, U(296), U(78)), null, Kit.Gilt);
        ui.Shield(new Rectangle(c.X - U(2), c.Y + U(2), U(42), U(50)), Style.BannerOf(save.Palette, save.Arms, save.BannerField, save.BannerTrim));
        int tx = c.X + U(50);
        ui.TextFit(save.Captain, tx, c.Y - U(2), c.Right - tx, Kit.Gilt, ui.FitScale(save.Captain, c.Right - tx));
        string loc = save.Place != "" ? $"{save.Place} · {TimeLabel(save.Hour)}" : $"{save.Biome.ToUpperInvariant()} · {TimeLabel(save.Hour)}";
        ui.TextFit(save.Service, tx, c.Y + U(16), c.Right - tx, Style.Dim, 1);
        ui.TextFit(loc, tx, c.Y + U(26), c.Right - tx, Kit.Parchment * 0.76f, 1);
        ui.Rect(new Rectangle(tx, c.Y + U(37), c.Right - tx, Math.Max(1, U(1))), Kit.Gilt * 0.3f);
        int sy = c.Y + U(43);
        ui.Icon("clock", tx, sy, Style.Dim, 1); ui.Text($"DAY {save.Day}", tx + U(12), sy, Style.Text, 1);
        ui.Icon("coin", tx + U(72), sy, Kit.Gilt, 1); ui.Text($"{save.Gold:N0}", tx + U(84), sy, Style.Text, 1);
        ui.Icon("shield", tx + U(144), sy, Style.Dim, 1); ui.Text($"{save.Men} MEN", tx + U(156), sy, Style.Text, 1);
    }

    /// The title as one PNG and the loading screen, part done, as another (IRONFIELD_TITLE_SHOTS), then quit.
    void TitleShot()
    {
        if (++shotFrames == 10) PendingShot = "title";
        if (shotFrames == 12 && Saves.Campaigns().Count > 0) saves.Show(SaveScreen.Use.Load, null);
        if (shotFrames == 18 && saves.Open) PendingShot = "title-load";
        if (shotFrames == 20) saves.Close();
        if (shotFrames == 24) { BeginLoading("A NEW CAMPAIGN"); loadBar = 0.62f; }
        if (shotFrames == 34) PendingShot = "loading";
        if (shotFrames > 36 && PendingShot == null) host.Exit();
    }

    void QbShots()
    {
        if (PendingShot != null) return;
        shotFrames++;
        if (shotStep < 0) { quickBattle.C = QuickBattleConfig.FromPreset("OPEN FIELD"); shotStep = 0; shotFrames = 0; }
        if (shotStep == 0 && shotFrames == 4) { PendingShot = "qb-field"; shotStep = 1; shotFrames = 0; }
        else if (shotStep == 1 && shotFrames == 1) quickBattle.C = QuickBattleConfig.FromPreset("TOWN SIEGE");
        else if (shotStep == 1 && shotFrames == 4) { PendingShot = "qb-siege"; shotStep = 2; shotFrames = 0; }
        else if (shotStep == 2 && shotFrames == 1) { quickBattle.C = QuickBattleConfig.FromPreset("ARCANA"); quickBattle.C.Player.Custom = true; }
        else if (shotStep == 2 && shotFrames == 4) { PendingShot = "qb-custom"; shotStep = 3; shotFrames = 0; }
        else if (shotStep == 3 && shotFrames == 1) quickBattle.C = QuickBattleConfig.FromDuel(1, 3);
        else if (shotStep == 3 && shotFrames == 4) { PendingShot = "qb-duel"; shotStep = 4; shotFrames = 0; }
        else if (shotStep >= 4) host.Exit();
    }

    /// The creator's steps one by one as PNGs (IRONFIELD_CREATOR_SHOTS), with a made-up captain, then quit.
    void Shots()
    {
        if (PendingShot != null) return;
        shotFrames++;
        if (shotStep < 0) { creator.M.Name = "ALDRIC THE GREY"; creator.M.Company = "THE LANTERN COMPANY"; creator.M.Past = "hedge_knight"; creator.M.Birth = "highborn"; creator.M.Realm = 1; shotStep = 0; shotFrames = 0; }
        if (shotStep >= CreatorScreen.Steps.Length) { host.Exit(); return; }
        if (shotFrames == 1) creator.Go(shotStep);  // show the step, let it settle, then take it
        if (shotFrames < 12) return;
        PendingShot = $"creator-{shotStep + 1}-{CreatorScreen.Steps[shotStep].ToLowerInvariant().Replace(' ', '-')}";
        shotStep++;
        shotFrames = 0;
    }
}
