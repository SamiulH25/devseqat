using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The settings overlay (video, audio, controls with key rebinding, game), opened from the title, map or battle menus.
sealed class SettingsScreen
{
    readonly ScreenHost host;
    readonly Ui ui;
    string tab = "VIDEO";
    Act? listening;
    int page;
    MouseState ms, pms;
    bool click;
    public bool Open;

    static readonly (int W, int H)[] Resolutions = { (1280, 720), (1600, 900), (1920, 1080), (2560, 1440) };

    public SettingsScreen(ScreenHost host) { this.host = host; ui = new Ui(host.Sb, host.Art, host.Font); }

    public void Update()
    {
        pms = ms;
        ms = Mouse.GetState();
        if (listening is Act a)
        {
            // Waiting for a key: the first key or mouse button pressed becomes the binding (Escape cancels).
            if (host.Ctl.AnyPressed() is Bind b)
            {
                if (b.Key != Keys.Escape) { host.Ctl.Binds[a] = new List<Bind> { b }; host.Ctl.SaveUser(); }
                listening = null;
            }
            return;
        }
        if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) click = true;
        if (host.Ctl.Pressed(Act.Menu)) Close();
    }

    void Close()
    {
        Open = false;
        Settings.Current.Quality = host.Quality.ToString();
        Settings.Current.Save();
        host.Ctl.SaveUser();
    }

    public void Draw()
    {
        var gd = host.GraphicsDevice;
        ui.Begin(gd.Viewport, ms.Position, click);
        click = false;
        ui.Dim(0.6f);
        var r = new Rectangle((ui.W - ui.U(820)) / 2, (ui.H - ui.U(540)) / 2, ui.U(820), ui.U(540));
        ui.Panel(r);
        ui.TextC("SETTINGS", r.X + r.Width / 2f, r.Y + ui.U(12), Style.Accent, 3);
        string[] tabs = { "VIDEO", "AUDIO", "CONTROLS", "GAME" };
        int tw = ui.U(150), tx = r.X + (r.Width - tabs.Length * (tw + ui.U(6))) / 2;
        for (int i = 0; i < tabs.Length; i++)
            if (ui.Button(new Rectangle(tx + i * (tw + ui.U(6)), r.Y + ui.U(50), tw, ui.U(26)), tabs[i], true, "", tab == tabs[i])) { tab = tabs[i]; page = 0; }
        var body = new Rectangle(r.X + ui.U(30), r.Y + ui.U(92), r.Width - ui.U(60), r.Height - ui.U(150));
        var s = Settings.Current;
        int y = body.Y;
        void Label(string t) => ui.Text(t, body.X, y + ui.U(6), Style.Text);
        bool Choice(int i, string label, bool on) => ui.Button(new Rectangle(body.X + ui.U(260) + i * ui.U(124), y, ui.U(118), ui.U(26)), label, true, "", on);
        void Row() => y += ui.U(36);

        switch (tab)
        {
            case "VIDEO":
                Label("RESOLUTION");
                for (int i = 0; i < Resolutions.Length; i++)
                    if (Choice(i, $"{Resolutions[i].W}X{Resolutions[i].H}", s.Width == Resolutions[i].W && s.Height == Resolutions[i].H))
                    { s.Width = Resolutions[i].W; s.Height = Resolutions[i].H; host.ApplyVideo(); }
                Row();
                Label("FULLSCREEN");
                if (Choice(0, "OFF", !s.Fullscreen)) { s.Fullscreen = false; host.ApplyVideo(); }
                if (Choice(1, "ON", s.Fullscreen)) { s.Fullscreen = true; host.ApplyVideo(); }
                Row();
                Label("INTERFACE SCALE");
                for (int i = 0; i < 4; i++) if (Choice(i, i == 0 ? "AUTO" : $"X{i}", s.UiScale == i)) s.UiScale = i;
                Row();
                Label("GRAPHICS");
                foreach (var (q, i) in new[] { (Quality.High, 0), (Quality.Medium, 1), (Quality.Low, 2) })
                    if (Choice(i, q.ToString().ToUpperInvariant(), host.Quality == q) && (host.Shading != null || q == Quality.Low)) host.Quality = q;
                Row();
                Label("EDGE SMOOTHING (FXAA)");
                if (Choice(0, "OFF", !s.Fxaa)) s.Fxaa = false;
                if (Choice(1, "ON", s.Fxaa)) s.Fxaa = true;
                Row();
                ui.Text("HIGH: SHADERS, SUN SHADOWS, POST. MEDIUM: NO SHADOWS. LOW: BASIC (ALSO THE FALLBACK).", body.X, y + ui.U(6), Style.Dim, 1);
                break;
            case "AUDIO":
                void Volume(string label, Func<float> get, Action<float> set)
                {
                    Label(label);
                    if (ui.Button(new Rectangle(body.X + ui.U(260), y, ui.U(40), ui.U(26)), "-")) set(MathF.Max(0, get() - 0.1f));
                    ui.Bar(new Rectangle(body.X + ui.U(310), y + ui.U(9), ui.U(260), ui.U(8)), get(), Style.Accent);
                    if (ui.Button(new Rectangle(body.X + ui.U(580), y, ui.U(40), ui.U(26)), "+")) set(MathF.Min(1, get() + 0.1f));
                    ui.Text($"{get() * 100:0}%", body.X + ui.U(632), y + ui.U(6), Style.Text);
                    Row();
                }
                Volume("MASTER", () => s.MasterVolume, v => s.MasterVolume = v);
                Volume("EFFECTS", () => s.SfxVolume, v => s.SfxVolume = v);
                Volume("MUSIC", () => s.MusicVolume, v => s.MusicVolume = v);
                if (host.Audio == null || !host.Audio.Enabled) ui.Text("NO AUDIO DEVICE FOUND: THE GAME IS SILENT.", body.X, y + ui.U(6), Style.Warn, 1);
                break;
            case "CONTROLS":
            {
                var c = host.Ctl;
                Label("MOUSE SENSITIVITY");
                if (ui.Button(new Rectangle(body.X + ui.U(260), y, ui.U(40), ui.U(26)), "-")) c.Sensitivity = MathF.Max(0.2f, c.Sensitivity - 0.1f);
                ui.Text($"{c.Sensitivity:0.0}", body.X + ui.U(316), y + ui.U(6), Style.Text);
                if (ui.Button(new Rectangle(body.X + ui.U(360), y, ui.U(40), ui.U(26)), "+")) c.Sensitivity = MathF.Min(4, c.Sensitivity + 0.1f);
                if (ui.Button(new Rectangle(body.X + ui.U(430), y, ui.U(150), ui.U(26)), c.InvertY ? "INVERT Y: ON" : "INVERT Y: OFF")) c.InvertY = !c.InvertY;
                if (ui.Button(new Rectangle(body.Right - ui.U(170), y, ui.U(170), ui.U(26)), "RESET DEFAULTS")) { c.ResetDefaults(); c.SaveUser(); }
                Row();
                var acts = Enum.GetValues<Act>();
                const int per = 9;
                int pages = (acts.Length + per - 1) / per;
                foreach (var a in acts.Skip(page * per).Take(per))
                {
                    ui.Text(Controls.Labels.TryGetValue(a, out var l) ? l : a.ToString().ToUpperInvariant(), body.X, y + ui.U(5), Style.Dim, 1);
                    bool waiting = listening == a;
                    if (ui.Button(new Rectangle(body.X + ui.U(330), y, ui.U(200), ui.U(22)), waiting ? "PRESS A KEY..." : c.Name(a), true, "", waiting)) listening = a;
                    y += ui.U(27);
                }
                if (ui.Button(new Rectangle(body.X, body.Bottom - ui.U(24), ui.U(100), ui.U(24)), "< PREV", page > 0)) page--;
                ui.TextC($"PAGE {page + 1} / {pages}", body.X + body.Width / 2f, body.Bottom - ui.U(18), Style.Dim, 1);
                if (ui.Button(new Rectangle(body.Right - ui.U(100), body.Bottom - ui.U(24), ui.U(100), ui.U(24)), "NEXT >", page < pages - 1)) page++;
                break;
            }
            case "GAME":
                Label("DIFFICULTY");
                foreach (var (d, i) in new[] { (Difficulty.Easy, 0), (Difficulty.Normal, 1), (Difficulty.Hard, 2) })
                    if (Choice(i, d.ToString().ToUpperInvariant(), s.Difficulty == d)) s.Difficulty = d;
                Row();
                ui.Text("EASY: ENEMIES HIT 20% SOFTER, CONTRACTS PAY 15% MORE. HARD: THE OPPOSITE.", body.X, y, Style.Dim, 1);
                y += ui.U(20);
                Label("IRONMAN (NEW CAMPAIGNS)");
                if (Choice(0, "OFF", !s.Ironman)) s.Ironman = false;
                if (Choice(1, "ON", s.Ironman)) s.Ironman = true;
                Row();
                ui.Text("IRONMAN: ONE AUTOSAVE, NO QUICK LOAD, AND A CAPTURED CAPTAIN IS EXECUTED: THE CAMPAIGN ENDS.", body.X, y, Style.Dim, 1);
                y += ui.U(26);
                if (ui.Button(new Rectangle(body.X, y, ui.U(240), ui.U(26)), "SHOW TIPS AGAIN", s.TipsSeen.Count > 0)) { s.TipsSeen.Clear(); s.Save(); }
                break;
        }
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(44), ui.U(180), ui.U(30)), "CLOSE")) Close();
        ui.End();
    }
}
