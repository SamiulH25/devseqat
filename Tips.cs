using Microsoft.Xna.Framework;

namespace Ironfield;

/// One-time tips for new players (A5). Each key is shown once, ever (Settings.TipsSeen); "Show tips again" in the settings resets them.
static class Tips
{
    static readonly List<(string Key, string Text, float At, bool Battle)> queue = new();

    /// Which screen is showing: a tip raised on the map waits for the map, one raised in battle waits for a battle
    /// (the welcome tip must not cover a battle). Set by `ScreenHost` each frame.
    public static bool InBattle;

    public static void Show(string key, string text)
    {
        if (Settings.Current.TipsSeen.Contains(key) || queue.Any(q => q.Key == key)) return;
        queue.Add((key, text, Ui.Now, InBattle));
    }

    /// The tip now showing on this screen, if any.
    static int Current => queue.FindIndex(q => q.Battle == InBattle);

    /// When the tip now showing was raised (interface time), or +inf with none.
    public static float Since => Current is int i and >= 0 ? queue[i].At : float.PositiveInfinity;

    public static bool Active => Current >= 0;

    public static void Dismiss()
    {
        int i = Current;
        if (i < 0) return;
        Settings.Current.FirstTime(queue[i].Key);
        queue.RemoveAt(i);
    }

    /// Draw the current tip docked at the right edge, its bottom `bottom` above the screen's: a narrow card out of the middle of
    /// the view (docs/PLAYTEST.md PT2a), so it never covers the men or the map you are reading.
    public static void Draw(Ui ui, int bottom)
    {
        if (!Active) return;
        int w = ui.U(300);
        var lines = Lines(ui, w - ui.U(24));
        int h = Height(ui, lines);
        Card(ui, new Rectangle(ui.W - w - ui.U(10), ui.H - bottom - h, w, h), lines);
    }

    /// The tip's text re-wrapped to the card: the line breaks in the text were written for a wide box, so they become spaces.
    static List<string> Lines(Ui ui, int w) => ui.Wrap(queue[Current].Text.ToUpperInvariant().Replace('\n', ' '), w);

    static int Height(Ui ui, List<string> lines) => lines.Count * ui.U(11) + ui.U(46);

    /// Draw the current tip with its top at `top` (over an open panel: at the top of the screen, clear of its buttons).
    public static void DrawAt(Ui ui, int top)
    {
        if (!Active) return;
        int w = ui.U(420);
        var lines = Lines(ui, w - ui.U(24));
        Card(ui, new Rectangle((ui.W - w) / 2, top, w, Height(ui, lines)), lines);
    }

    static void Card(Ui ui, Rectangle r, List<string> lines)
    {
        ui.Rect(r, Kit.IronDark); // solid: nothing on the field shows through the words
        ui.Panel(r, Style.Accent);
        ui.Text("TIP", r.X + ui.U(12), r.Y + ui.U(8), Style.Accent, 1);
        for (int i = 0; i < lines.Count; i++) ui.Text(lines[i], r.X + ui.U(12), r.Y + ui.U(21) + i * ui.U(11), Style.Text, 1);
        if (ui.Button(new Rectangle(r.Right - ui.U(110), r.Bottom - ui.U(22), ui.U(100), ui.U(16)), "GOT IT", true, "ENTER")) Dismiss();
    }
}
