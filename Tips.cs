using Microsoft.Xna.Framework;

namespace Ironfield;

/// One-time tips for new players (A5). Each key is shown once, ever (Settings.TipsSeen); "Show tips again" in the settings resets them.
static class Tips
{
    static readonly Queue<(string Key, string Text)> queue = new();

    public static void Show(string key, string text)
    {
        if (Settings.Current.TipsSeen.Contains(key) || queue.Any(q => q.Key == key)) return;
        queue.Enqueue((key, text));
    }

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
        var lines = queue.Peek().Text.ToUpperInvariant().Split('\n');
        int w = lines.Max(l => ui.TW(l, 1)) + ui.U(40), h = lines.Length * ui.U(14) + ui.U(52);
        var r = new Rectangle((ui.W - w) / 2, ui.H - bottom - h, w, h);
        ui.Panel(r, Style.Accent);
        ui.Text("TIP", r.X + ui.U(12), r.Y + ui.U(8), Style.Accent, 1);
        for (int i = 0; i < lines.Length; i++) ui.Text(lines[i], r.X + ui.U(20), r.Y + ui.U(22) + i * ui.U(14), Style.Text, 1);
        if (ui.Button(new Rectangle(r.Right - ui.U(130), r.Bottom - ui.U(26), ui.U(120), ui.U(20)), "GOT IT (ENTER)")) Dismiss();
    }
}
