using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The title screen: the name and the menu down the left of a painted dusk (TitleScreen.Scene.cs), and a card for the last save.
/// New campaign, continue, quick battle, settings, quit; the mouse or the arrow keys and Enter.
sealed partial class TitleScreen
{
    readonly ScreenHost host;
    readonly Ui ui;
    MouseState ms, pms;
    KeyboardState kb, pkb;
    bool click;
    readonly CreatorScreen creator;
    // Debug: IRONFIELD_CREATOR_SHOTS=/dir saves a PNG of each creator step, IRONFIELD_TITLE_SHOTS=/dir one of the title; then quits.
    static readonly string? creatorShots = Environment.GetEnvironmentVariable("IRONFIELD_CREATOR_SHOTS") is { Length: > 0 } d ? d : null;
    static readonly string? titleShots = Environment.GetEnvironmentVariable("IRONFIELD_TITLE_SHOTS") is { Length: > 0 } d ? d : null;
    public readonly string? ShotsDir = creatorShots ?? titleShots;
    public string? PendingShot;
    int shotStep = -1, shotFrames;
    float time;
    public bool InCreator => creator.Open;
    public string CreatorCulture => creator.CurrentCulture;
    // The latest save, read once per file change, for the continue card.
    DateTime saveStamp;
    SaveCard? card;

    sealed record SaveCard(string Name, string Service, int Day, int Gold, int Men, int Palette);

    public TitleScreen(ScreenHost host)
    {
        this.host = host;
        ui = new Ui(host.Sb, host.Art, host.Font);
        creator = new CreatorScreen(ui, m => host.StartCampaign(Environment.TickCount & 0xFFFF, m.Realm, m), new CreatorStage(host.GraphicsDevice, host.Art));
        if (creatorShots != null || Environment.GetEnvironmentVariable("IRONFIELD_TITLE_PICK") != null) creator.Show();
    }

    float lastDt = 1 / 60f;
    // The menu: the chosen row follows the mouse when it moves and the arrow keys otherwise.
    static readonly string[] Items = { "NEW CAMPAIGN", "CONTINUE", "QUICK BATTLE", "SETTINGS", "QUIT" };
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
        if (Hit(Keys.F3)) host.OpenLab();
        if (!creator.Open)
        {
            if (Hit(Keys.Down) || Hit(Keys.S)) nav++;
            if (Hit(Keys.Up) || Hit(Keys.W)) nav--;
            if (Hit(Keys.Enter)) go = true;
        }
        lastDt = dt;
        time = titleShots != null ? 3f : time + dt;
    }

    int U(float v) => ui.U(v);

    public void Draw()
    {
        var gd = host.GraphicsDevice;
        creator.Prepare(lastDt); // the living captain, drawn to its texture before the frame's sprites begin
        gd.Clear(Kit.Night);
        ui.Begin(gd.Viewport, ms.Position, click);
        click = false;
        if (Settings.Current.UiScale == 0 && ui.S == 1 && ui.H >= 1000) ui.S = 2; // the title is a picture: draw it large, as the creator does
        var save = Card();
        Scene();
        if (creator.Open)
        {
            nav = 0; go = false;
            creator.Draw(kb, pkb, time);
            ui.End();
            if (creatorShots != null) Shots();
            return;
        }

        int x = U(64), ty = (int)(ui.H * 0.115f), k = Math.Max(U(9), ui.H / 60);
        Logo(x, ty, k);
        Sword(x, ty + 7 * k + U(14), 53 * k);
        Spaced("A CAPTAIN'S WAR", x + U(40), ty + 7 * k + U(34), Kit.Parchment, 2, U(5));
        Menu(x, (int)(ui.H * 0.43f), save);
        if (host.TitleNote != "") ui.Text(host.TitleNote, x, (int)(ui.H * 0.43f) + U(184), Style.Warn, 1);
        if (save != null) Company(save, x, ui.H - U(92));
        ui.TextR("W/S CHOOSE   ENTER SELECT   F2 GRAPHICS   F3 UI LAB", ui.W - U(24), ui.H - U(20), Style.Text * 0.7f, 1);
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
    void Sword(int x, int y, int w)
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
        int bx = x + U(36), bw = w - U(36), tip = U(22);
        ui.Rect(new Rectangle(bx, y - U(3), bw - tip, U(3)), steel);
        ui.Rect(new Rectangle(bx, y, bw - tip, U(3)), shade);
        ui.Rect(new Rectangle(bx, y - U(3), bw - tip, u), Color.White * 0.8f);
        ui.Rect(new Rectangle(bx + U(4), y - u / 2, (bw - tip) * 3 / 4, u), new Color(86, 92, 108));
        for (int i = 0; i < 3; i++) // the point, narrowing in three steps
        {
            int h = U(2 - i) + (i == 2 ? u : 0), sx = bx + bw - tip + i * tip / 3;
            ui.Rect(new Rectangle(sx, y - h, tip / 3, h), steel);
            ui.Rect(new Rectangle(sx, y, tip / 3, h), shade);
        }
    }

    /// Text with its letters set apart by `gap` pixels.
    void Spaced(string s, float x, float y, Color c, int scale, int gap)
    {
        foreach (char ch in s) { ui.Text(ch.ToString(), x, y, c, scale); x += ui.TW("A", scale) + gap; }
    }

    /// The menu down the left: plain rows on a gilt spine; the chosen row lights and slides out.
    void Menu(int x, int y, SaveCard? save)
    {
        bool[] can = { true, host.CanContinue, true, true, true };
        if (sel < 0 || !can[sel]) sel = can[1] ? 1 : 0;
        for (; nav != 0; nav -= Math.Sign(nav))
            do sel = (sel + Math.Sign(nav) + Items.Length) % Items.Length; while (!can[sel]);
        int w = U(270), h = U(30), gap = U(5);
        ui.Rect(new Rectangle(x - U(10), y - U(2), Math.Max(1, U(1)), Items.Length * (h + gap) - gap + U(4)), Kit.Gilt * 0.35f);
        int hit = -1;
        for (int i = 0; i < Items.Length; i++)
        {
            var r = new Rectangle(x - U(10), y + i * (h + gap), w, h);
            if (can[i] && mouseMoved && ui.Hover(r)) sel = i;
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
            var c = !can[i] ? Style.Dim * 0.45f : Color.Lerp(Kit.Parchment, new Color(255, 250, 230), a);
            ui.Text(Items[i], r.X + U(26) + U(3) * a, r.Y + (h - U(14)) / 2f, c, 2);
            string note = i == 1 ? (save != null ? $"DAY {save.Day}" : can[1] ? "" : "NO SAVE") : "";
            if (note != "") ui.TextR(note, r.Right - U(12), r.Y + (h - U(7)) / 2f, Style.Dim * (can[i] ? 1 : 0.6f), 1);
            ui.Block(r);
            if (can[i] && ui.Clicked(r)) hit = i;
        }
        mouseMoved = false;
        if (go && can[sel]) hit = sel;
        go = false;
        if (hit < 0) return;
        Ui.OnClick?.Invoke();
        switch (hit)
        {
            case 0: creator.Show(); break;
            case 1: host.ContinueCampaign(); break;
            case 2: host.QuickBattle(); break;
            case 3: host.OpenSettings(); break;
            default: host.Exit(); break;
        }
    }

    /// The last save as the company's card: its arms, the captain, whom he serves, the day, the purse and the men.
    void Company(SaveCard save, int x, int y)
    {
        var c = ui.Plate(new Rectangle(x - U(10), y, U(270), U(66)), null, Kit.Gilt);
        ui.Shield(new Rectangle(c.X - U(2), c.Y - U(4), U(40), U(48)), Style.Sides[save.Palette]);
        int tx = c.X + U(48);
        ui.TextFit(save.Name, tx, c.Y - U(4), c.Right - tx, Kit.Gilt, ui.FitScale(save.Name, c.Right - tx));
        ui.Text(save.Service, tx, c.Y + U(14), Style.Dim, 1);
        ui.Rect(new Rectangle(tx, c.Y + U(25), c.Right - tx, Math.Max(1, U(1))), Kit.Gilt * 0.3f);
        ui.Icon("clock", tx, c.Y + U(31), Style.Dim, 1); ui.Text($"DAY {save.Day}", tx + U(12), c.Y + U(31), Style.Text, 1);
        ui.Icon("coin", tx + U(62), c.Y + U(31), Kit.Gilt, 1); ui.Text($"{save.Gold:N0}", tx + U(74), c.Y + U(31), Style.Text, 1);
        ui.Icon("shield", tx + U(124), c.Y + U(31), Style.Dim, 1); ui.Text($"{save.Men} MEN", tx + U(136), c.Y + U(31), Style.Text, 1);
    }

    /// The title as one PNG (IRONFIELD_TITLE_SHOTS), then quit.
    void TitleShot()
    {
        if (++shotFrames == 10) PendingShot = "title";
        if (shotFrames > 12 && PendingShot == null) host.Exit();
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

    /// The newest save as a card, re-read only when the file changes.
    SaveCard? Card()
    {
        var path = new[] { "autosave", "quicksave" }.Select(n => Path.Combine(CampaignState.SaveDir, n + ".json"))
            .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (path == null) return card = null;
        var stamp = File.GetLastWriteTimeUtc(path);
        if (stamp == saveStamp) return card;
        saveStamp = stamp;
        card = null;
        try
        {
            var s = CampaignState.Deserialize(File.ReadAllText(path));
            string name = s.Characters.FirstOrDefault(c => c.Id == s.CaptainChar)?.Name ?? "THE CAPTAIN";
            int f = s.Captain.Contract;
            bool serving = f >= 0 && f < s.Factions.Count;
            string service = !serving ? "A FREE COMPANY" : (s.Captain.Fealty ? "SWORN TO THE " : "IN THE PAY OF THE ") + s.Factions[f].Name;
            card = new SaveCard(name.ToUpperInvariant(), Fit(service.ToUpperInvariant()), (int)(s.Hours / 24) + 1, (int)s.Gold,
                s.Parties.Count > 0 ? s.Parties[0].Men : 0, !serving ? 2 : f < s.Factions.Count && s.Factions[f].Founded ? s.Factions[f].Palette : Factions.All[Math.Min(f, Factions.All.Count - 1)].Palette);
        }
        catch (Exception) { }
        return card;
    }

    string Fit(string s) { while (s.Length > 4 && ui.TW(s, 1) > U(196)) s = s[..^2]; return s; }
}
