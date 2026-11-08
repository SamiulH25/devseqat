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
    float time;
    // The latest save, read once per file change, for the continue card.
    DateTime saveStamp;
    SaveCard? card;

    sealed record SaveCard(string Name, string Service, int Day, int Gold, int Men, int Palette);

    public TitleScreen(ScreenHost host) { this.host = host; ui = new Ui(host.Sb, host.Art, host.Font); }

    public void Update()
    {
        pms = ms;
        ms = Mouse.GetState();
        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) click = true;
        pkb = kb; kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.F3) && !pkb.IsKeyDown(Keys.F3)) host.OpenLab();
        time += 1 / 60f;
    }

    int U(float v) => ui.U(v);

    public void Draw()
    {
        var gd = host.GraphicsDevice;
        gd.Clear(Kit.Night);
        ui.Begin(gd.Viewport, ms.Position, click);
        click = false;
        Scene();

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
        if (ui.Gilded(B(), "NEW CAMPAIGN")) host.StartCampaign(Environment.TickCount & 0xFFFF);
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

    /// Night sky into an ember dusk, a moon, a castle on the far hill and three ranges of hills.
    void Scene()
    {
        var screen = new Rectangle(0, 0, ui.W, ui.H);
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H / 2), Kit.Night, Kit.Dusk);
        ui.VGrad(new Rectangle(0, ui.H / 2, ui.W, ui.H - ui.H / 2), Kit.Dusk, new Color(196, 110, 62));
        ui.Stars(new Rectangle(0, 0, ui.W, ui.H / 2), time);
        var moon = new Vector2(ui.W * 0.8f, ui.H * 0.2f);
        ui.Disc(moon, U(46), Color.White * 0.04f); ui.Disc(moon, U(34), Color.White * 0.06f);
        ui.Disc(moon, U(24), new Color(240, 232, 210)); ui.Disc(moon + new Vector2(U(6), U(-4)), U(5), new Color(214, 206, 186));
        int castleX = (int)(ui.W * 0.76f);
        ui.Castle(castleX, ui.HillY(screen, castleX, (int)(ui.H * 0.66f), U(26), 0.4f) + U(6), 3, new Color(78, 50, 70), Kit.Ember);
        ui.Hills(screen, (int)(ui.H * 0.66f), U(26), 0.4f, new Color(78, 50, 70));
        ui.Hills(screen, (int)(ui.H * 0.76f), U(22), 3.1f, new Color(46, 32, 48));
        ui.Hills(screen, (int)(ui.H * 0.88f), U(14), 5.7f, new Color(20, 16, 26));
        ui.Vignette(0.8f);
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
