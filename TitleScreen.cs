using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The title screen: a dusk scene with new campaign, continue, quick battle, settings, quit, and a card for the last save.
sealed class TitleScreen
{
    readonly ScreenHost host;
    readonly Ui ui;
    MouseState ms, pms;
    KeyboardState kb, pkb;
    bool click;
    readonly CreatorScreen creator;
    // Debug: IRONFIELD_CREATOR_SHOTS=/dir saves a PNG of each creator step, then quits.
    public readonly string? ShotsDir = Environment.GetEnvironmentVariable("IRONFIELD_CREATOR_SHOTS") is { Length: > 0 } d ? d : null;
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
        if (ShotsDir != null || Environment.GetEnvironmentVariable("IRONFIELD_TITLE_PICK") != null) creator.Show();
    }

    float lastDt = 1 / 60f;

    public void Update(float dt = 1 / 60f)
    {
        pms = ms;
        ms = Mouse.GetState();
        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) click = true;
        pkb = kb; kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.F3) && !pkb.IsKeyDown(Keys.F3)) host.OpenLab();
        lastDt = dt;
        time += dt;
    }

    int U(float v) => ui.U(v);

    public void Draw()
    {
        var gd = host.GraphicsDevice;
        creator.Prepare(lastDt); // the living captain, drawn to its texture before the frame's sprites begin
        gd.Clear(Kit.Night);
        ui.Begin(gd.Viewport, ms.Position, click);
        click = false;
        Scene();
        if (creator.Open)
        {
            creator.Draw(kb, pkb, time);
            ui.End();
            if (ShotsDir != null) Shots();
            return;
        }

        int cx = ui.W / 2, ty = (int)(ui.H * 0.13f);
        ui.TextC("IRONFIELD", cx + U(3), ty + U(4), Kit.GiltDark * 0.8f, 8);
        ui.TextC("IRONFIELD", cx, ty, Kit.Gilt, 8);
        ui.Rect(new Rectangle(cx - ui.TW("IRONFIELD", 8) / 2, ty + U(8), ui.TW("IRONFIELD", 8), U(2)), Color.White * 0.15f);
        ui.Divider(cx - U(180), ty + U(70), U(360), Kit.Gilt);
        ui.TextC("A CAPTAIN'S WAR", cx, ty + U(80), Kit.Parchment, 2);

        var save = Card();
        int mw = U(250), my = (int)(ui.H * 0.4f);
        var m = ui.Plate(new Rectangle(cx - mw / 2, my, mw, U(206)), solid: false);
        int y = m.Y;
        Rectangle B() { var r = new Rectangle(m.X, y, m.Width, U(30)); y += U(36); return r; }
        if (ui.Gilded(B(), "NEW CAMPAIGN")) creator.Show();
        if (ui.Gilded(B(), "CONTINUE", save != null ? $"DAY {save.Day}" : "", host.CanContinue)) host.ContinueCampaign();
        if (ui.Gilded(B(), "QUICK BATTLE")) host.QuickBattle();
        if (ui.Gilded(B(), "SETTINGS")) host.OpenSettings();
        if (ui.Gilded(B(), "QUIT")) host.Exit();
        float sway = time * 1.5f;
        ui.Banner(cx - mw / 2 - U(64), my - U(10), U(52), U(150), Style.Sides[0], sway);
        ui.Banner(cx + mw / 2 + U(64), my - U(10), U(52), U(150), Style.Sides[1], sway + 1.3f);
        if (host.TitleNote != "") ui.TextC(host.TitleNote, cx, my + U(214), Style.Warn, 1);

        if (save != null)
        {
            var c = ui.Plate(new Rectangle(U(24), ui.H - U(104), U(250), U(80)), null, Kit.Gilt);
            ui.Shield(new Rectangle(c.X - U(2), c.Y - U(2), U(40), U(48)), Style.Sides[save.Palette]);
            int tx = c.X + U(46);
            ui.Text(save.Name, tx, c.Y, Style.Text, 1);
            ui.Text(save.Service, tx, c.Y + U(11), Style.Dim, 1);
            ui.Icon("clock", tx, c.Y + U(26), Style.Dim, 1); ui.Text($"DAY {save.Day}", tx + U(12), c.Y + U(26), Style.Text, 1);
            ui.Icon("coin", tx + U(58), c.Y + U(26), Kit.Gilt, 1); ui.Text($"{save.Gold:N0}", tx + U(70), c.Y + U(26), Style.Text, 1);
            ui.Icon("shield", tx, c.Y + U(39), Style.Dim, 1); ui.Text($"{save.Men} MEN", tx + U(12), c.Y + U(39), Style.Text, 1);
        }
        ui.TextR("F2 GRAPHICS QUALITY   F3 UI LAB", ui.W - U(24), ui.H - U(24), Style.Dim * 0.6f, 1);
        ui.End();
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

    /// Night sky into an ember dusk, a moon, a castle on the far hill and three ranges of hills.
    void Scene()
    {
        var screen = new Rectangle(0, 0, ui.W, ui.H);
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H / 2), Kit.Night, Kit.Dusk);
        ui.VGrad(new Rectangle(0, ui.H / 2, ui.W, ui.H - ui.H / 2), Kit.Dusk, new Color(196, 110, 62));
        ui.Stars(new Rectangle(0, 0, ui.W, ui.H / 2), time);
        // The mouse leans the scene a little: far layers move less than near ones.
        float px = Ui.Instant ? 0 : (ms.X / (float)Math.Max(1, ui.W) - 0.5f) * U(18);
        var moon = new Vector2(ui.W * 0.8f - px * 0.2f, ui.H * 0.2f);
        ui.Disc(moon, U(46), Color.White * 0.04f); ui.Disc(moon, U(34), Color.White * 0.06f);
        ui.Disc(moon, U(24), new Color(240, 232, 210)); ui.Disc(moon + new Vector2(U(6), U(-4)), U(5), new Color(214, 206, 186));
        Clouds();
        int castleX = (int)(ui.W * 0.76f - px * 0.4f);
        // The castle's windows flicker like fires behind them.
        var ember = Color.Lerp(Kit.Ember, new Color(255, 214, 140), 0.25f + 0.25f * MathF.Sin(time * 7.3f) * MathF.Sin(time * 3.1f));
        ui.Castle(castleX, ui.HillY(screen, castleX, (int)(ui.H * 0.66f), U(26), 0.4f) + U(6), 3, new Color(78, 50, 70), ember);
        ui.Hills(Shifted(screen, px * 0.4f), (int)(ui.H * 0.66f), U(26), 0.4f, new Color(78, 50, 70));
        Birds();
        ui.Hills(Shifted(screen, px * 0.7f), (int)(ui.H * 0.76f), U(22), 3.1f, new Color(46, 32, 48));
        ui.Hills(Shifted(screen, px), (int)(ui.H * 0.88f), U(14), 5.7f, new Color(20, 16, 26));
        Embers();
        ui.Vignette(0.8f);
    }

    /// The screen moved sideways by `dx`, widened so the hills still cover both edges.
    static Rectangle Shifted(Rectangle r, float dx) => new(r.X - (int)MathF.Abs(dx) - 8 + (int)dx, r.Y, r.Width + 2 * (int)MathF.Abs(dx) + 16, r.Height);

    /// Long thin clouds drifting slowly across the dusk.
    void Clouds()
    {
        for (int i = 0; i < 6; i++)
        {
            float span = ui.W + U(400);
            float x = ((i * 0.37f * span + time * U(6 + i * 2)) % span) - U(200);
            float y = ui.H * (0.12f + 0.07f * i);
            var c = Color.Lerp(new Color(120, 80, 120), new Color(236, 150, 112), 0.15f * i) * (0.06f + 0.015f * i);
            for (int k = 0; k < 24; k++) // a long low streak: puffs swelling in the middle, overlapping softly
            {
                float e = MathF.Sin(k / 23f * MathF.PI);
                ui.Disc(new Vector2(x + U(k * 9), y + U(MathF.Sin(k * 0.9f + i) * 1.5f)), U(3 + 7 * e), c);
            }
        }
    }

    /// A few rooks wheeling over the far hill, wings beating.
    void Birds()
    {
        for (int i = 0; i < 5; i++)
        {
            float a = time * 0.25f + i * 1.3f;
            var at = new Vector2(ui.W * (0.8f + 0.06f * MathF.Cos(a)) + U(i * 14), ui.H * (0.46f + 0.03f * MathF.Sin(a * 1.7f)));
            float flap = MathF.Sin(time * 9 + i * 2) * U(3);
            var c = new Color(40, 26, 36);
            ui.Line(at, at + new Vector2(-U(5), -flap), c, Math.Max(1, U(1)));
            ui.Line(at, at + new Vector2(U(5), -flap), c, Math.Max(1, U(1)));
        }
    }

    /// Sparks rising from the near fields.
    void Embers()
    {
        for (int i = 0; i < 18; i++)
        {
            float life = 6 + i % 4, t = ((time + i * 0.71f) % life) / life;
            float x = ui.W * ((i * 0.618f) % 1) + MathF.Sin(t * 6 + i) * U(12);
            float y = ui.H * (0.98f - 0.5f * t);
            ui.Disc(new Vector2(x, y), Math.Max(1, U(1.5f)), Color.Lerp(new Color(255, 200, 120), Kit.Ember, t) * (1 - t) * 0.8f);
        }
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
