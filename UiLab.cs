using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The UI lab (F3 on the title screen): a gallery of interface concepts drawn with mock data, for trying new looks before
/// they go into the real screens. Left/right or 1-9 change page, Esc goes back. IRONFIELD_LAB_SHOTS=/dir saves a PNG of
/// every page and quits. Pages live in UiLab.Pages.cs and UiLab.Scenes.cs; the widgets in UiLab.Kit.cs.
sealed partial class UiLab
{
    readonly ScreenHost host;
    readonly Ui ui;
    MouseState ms, pms;
    KeyboardState kb, pkb;
    bool click, held;
    int page;
    float time;

    /// Back to the title screen.
    public Action? OnBack;
    /// A screenshot the host should save this frame (shots mode only).
    public string? PendingShot;
    public readonly string? ShotsDir = Environment.GetEnvironmentVariable("IRONFIELD_LAB_SHOTS") is { Length: > 0 } d ? d : null;
    int shotFrames;
    bool shotAsked;

    readonly (string Name, Action<Rectangle> Draw)[] pages;

    public UiLab(ScreenHost host)
    {
        this.host = host;
        ui = new Ui(host.Sb, host.Art, host.Font);
        pages = new (string, Action<Rectangle>)[]
        {
            ("KIT", KitPage), ("TITLE", TitlePage), ("MUSTER", MusterPage), ("AFTER ACTION", ReportPage), ("MARKET", MarketPage),
            ("BATTLE HUD", HudPage), ("CAPTAIN", CaptainPage), ("REALMS", RealmsPage), ("EVENT", EventPage),
        };
    }

    public void Update(float dt)
    {
        time += dt;
        pms = ms; ms = Mouse.GetState();
        pkb = kb; kb = Keyboard.GetState();
        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) click = true;
        held = ms.LeftButton == ButtonState.Pressed;
        bool Hit(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
        if (Hit(Keys.Right) || Hit(Keys.Tab)) page = (page + 1) % pages.Length;
        if (Hit(Keys.Left)) page = (page + pages.Length - 1) % pages.Length;
        for (int i = 0; i < Math.Min(9, pages.Length); i++) if (Hit(Keys.D1 + i)) page = i;
        if (Hit(Keys.Escape)) OnBack?.Invoke();
        if (ShotsDir != null) Shots();
    }

    /// Shots mode: hold each page a few frames, ask for its PNG, wait until the host has saved it, then move on; quit after the last.
    void Shots()
    {
        if (PendingShot != null) return;
        if (shotAsked)
        {
            shotAsked = false; shotFrames = 0;
            if (++page >= pages.Length) host.Exit();
            page = Math.Min(page, pages.Length - 1);
        }
        else if (++shotFrames >= 4) { PendingShot = $"lab-{page + 1:00}-{pages[page].Name.ToLowerInvariant().Replace(' ', '-')}"; shotAsked = true; }
    }

    public void Draw()
    {
        var gd = host.GraphicsDevice;
        gd.Clear(Kit.IronDark);
        ui.Begin(gd.Viewport, ms.Position, click);
        click = false;
        var area = new Rectangle(ui.U(16), ui.U(34), ui.W - ui.U(32), ui.H - ui.U(50));
        pages[page].Draw(area);
        Nav();
        ui.End();
    }

    /// The page strip across the top.
    void Nav()
    {
        ui.Rect(new Rectangle(0, 0, ui.W, ui.U(22)), Color.Black * 0.6f);
        ui.Rect(new Rectangle(0, ui.U(22), ui.W, ui.U(1)), Kit.Gilt * 0.4f);
        ui.Text("UI LAB", ui.U(10), ui.U(8), Kit.Gilt, 1);
        int x = ui.U(64);
        for (int i = 0; i < pages.Length; i++)
        {
            string label = $"{i + 1} {pages[i].Name}";
            var r = new Rectangle(x, ui.U(3), ui.TW(label, 1) + ui.U(12), ui.U(17));
            bool on = i == page, hover = ui.Hover(r);
            if (on) { ui.Rect(r, Kit.Gilt * 0.22f); ui.Rect(new Rectangle(r.X, r.Bottom, r.Width, ui.U(2)), Kit.Gilt); }
            ui.Text(label, r.X + ui.U(6), r.Y + ui.U(5), on ? Kit.Gilt : hover ? Style.Text : Style.Dim, 1);
            if (ui.Clicked(r)) page = i;
            x = r.Right + ui.U(2);
        }
        ui.TextR("LEFT/RIGHT PAGE   ESC BACK", ui.W - ui.U(10), ui.U(8), Style.Dim * 0.8f, 1);
    }
}
