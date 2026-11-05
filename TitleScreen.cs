using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The title screen: new campaign, continue, quick battle, settings, quit.
sealed class TitleScreen
{
    readonly ScreenHost host;
    readonly Ui ui;
    MouseState ms, pms;
    bool click;

    public TitleScreen(ScreenHost host) { this.host = host; ui = new Ui(host.Sb, host.Art, host.Font); }

    public void Update()
    {
        pms = ms;
        ms = Mouse.GetState();
        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) click = true;
    }

    public void Draw()
    {
        var gd = host.GraphicsDevice;
        gd.Clear(new Color(18, 20, 26));
        ui.Begin(gd.Viewport, ms.Position, click);
        click = false;
        ui.Rect(new Rectangle(0, ui.H / 2 - ui.U(150), ui.W, ui.U(4)), Style.Accent * 0.5f);
        ui.TextC("IRONFIELD", ui.W / 2f, ui.H / 2f - ui.U(130), Style.Accent, 8);
        ui.TextC("A CAPTAIN'S WAR", ui.W / 2f, ui.H / 2f - ui.U(60), Style.Dim, 2);
        int y = ui.H / 2 - ui.U(20), w = ui.U(280);
        Rectangle B() { var r = new Rectangle((ui.W - w) / 2, y, w, ui.U(34)); y += ui.U(42); return r; }
        if (ui.Button(B(), "NEW CAMPAIGN")) host.StartCampaign(Environment.TickCount & 0xFFFF);
        if (ui.Button(B(), "CONTINUE", host.CanContinue)) host.ContinueCampaign();
        if (ui.Button(B(), "QUICK BATTLE")) host.QuickBattle();
        if (ui.Button(B(), "SETTINGS")) host.OpenSettings();
        if (ui.Button(B(), "QUIT")) host.Exit();
        if (host.TitleNote != "") ui.TextC(host.TitleNote, ui.W / 2f, y + ui.U(8), Style.Warn, 1);
        ui.TextC("F2 GRAPHICS QUALITY", ui.W / 2f, ui.H - ui.U(30), Style.Dim * 0.7f, 1);
        ui.End();
    }
}
