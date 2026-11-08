using Microsoft.Xna.Framework;

namespace Ironfield;

/// One-time tips for new players (A5). Each key is shown once, ever (Settings.TipsSeen); "Show tips again" in the settings resets them.
static class Tips
{
    static readonly Queue<(string Key, string Text, float At)> queue = new();

    public static void Show(string key, string text)
    {
        if (Settings.Current.TipsSeen.Contains(key) || queue.Any(q => q.Key == key)) return;
        queue.Enqueue((key, text, Ui.Now));
    }

    /// When the tip now showing was raised (interface time), or +inf with none.
    public static float Since => queue.Count > 0 ? queue.Peek().At : float.PositiveInfinity;

    public static bool Active => queue.Count > 0;

    public static void Dismiss()
    {
        if (queue.Count == 0) return;
        Settings.Current.FirstTime(queue.Dequeue().Key);
    }

    /// Draw the current tip above the bottom of the screen; `bottom` is the space to leave free below it.
    public static void Draw(Ui ui, int bottom)
    {
        if (queue.Count == 0) return;
        DrawAt(ui, ui.H - bottom - Height(ui));
    }

    static int Height(Ui ui) => queue.Peek().Text.Split('\n').Length * ui.U(14) + ui.U(52);

    /// Draw the current tip with its top at `top` (over an open panel: at the top of the screen, clear of its buttons).
    public static void DrawAt(Ui ui, int top)
    {
        if (queue.Count == 0) return;
        var lines = queue.Peek().Text.ToUpperInvariant().Split('\n');
        int w = lines.Max(l => ui.TW(l, 1)) + ui.U(40), h = Height(ui);
        var r = new Rectangle((ui.W - w) / 2, top, w, h);
        ui.Panel(r, Style.Accent);
        ui.Text("TIP", r.X + ui.U(12), r.Y + ui.U(8), Style.Accent, 1);
        for (int i = 0; i < lines.Length; i++) ui.Text(lines[i], r.X + ui.U(20), r.Y + ui.U(22) + i * ui.U(14), Style.Text, 1);
        if (ui.Button(new Rectangle(r.Right - ui.U(130), r.Bottom - ui.U(26), ui.U(120), ui.U(20)), "GOT IT (ENTER)")) Dismiss();
    }
}
