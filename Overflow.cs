using Microsoft.Xna.Framework;

namespace Ironfield;

/// The layout guard for staged screenshots (docs/archive/POLISH.md Q3): while a shots run is on, every panel, plate and button drawn
/// this frame is remembered, and any text that runs past the one it starts in (or past the screen) is noted.
/// `Flush` writes the notes for each shot to overflow.txt next to the PNGs; the aim is an empty file at 1280x720.
static class Overflow
{
    /// On only in shots runs, so normal play pays nothing.
    public static readonly bool On = new[] { "IRONFIELD_SHOTS", "IRONFIELD_CAMPAIGN_SHOTS", "IRONFIELD_LAB_SHOTS" }
        .Any(v => Environment.GetEnvironmentVariable(v) is { Length: > 0 });

    static readonly List<Rectangle> boxes = new();
    /// This frame's text: what it says, where it lies, and how many boxes had been drawn before it.
    static readonly List<(string S, Rectangle R, int Before, Rectangle? In)> texts = new();
    static readonly SortedSet<string> found = new();
    static int screenW, screenH;

    /// A new frame: forget last frame's boxes.
    public static void Frame(int width, int height) { if (!On) return; boxes.Clear(); texts.Clear(); screenW = width; screenH = height; }

    /// A panel, plate or button text may be drawn inside.
    public static void Box(Rectangle r) { if (On) boxes.Add(r); }

    /// Text `s` drawn at (x, y), `w` pixels wide: note it if it leaves the box it starts in.
    public static void Text(string s, float x, float y, int w)
    {
        // Labels over things off the screen (flags, map names) are not laid out by hand: only text that starts on it counts.
        if (!On || s.Length == 0 || x < 0 || x >= screenW || y < 0 || y >= screenH) return;
        var at = new Point((int)x + 1, (int)y + 1);
        // The last box drawn under the text's start is the one it sits on (a button over its panel, an overlay over the HUD).
        Rectangle? box = null;
        for (int i = boxes.Count - 1; i >= 0 && box == null; i--) if (boxes[i].Contains(at)) box = boxes[i];
        int left = box?.Left ?? 0, right = box?.Right ?? screenW;
        texts.Add((s, new Rectangle((int)x + 1, (int)y + 1, Math.Max(1, w - 2), 4), boxes.Count, box));
        if (x + w > right + 1 || x < left - 1) found.Add($"\"{s}\" at {x:0},{y:0} ({w} px) leaves {(box == null ? "the screen" : $"the box {box.Value.X},{box.Value.Y} {box.Value.Width}x{box.Value.Height}")}");
    }

    /// After a shot: write what was found since the last one under the shot's name, then start again.
    public static void Flush(string dir, string shot)
    {
        if (!On) return;
        // Text that runs under a button or plate drawn later inside the same panel (a long line beneath its row's button).
        foreach (var (s, r, before, home) in texts)
            for (int i = before; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (b.Contains(r.Location) || !b.Intersects(r) || home is not Rectangle h || !h.Contains(b)) continue;
                found.Add($"\"{s}\" at {r.X - 1},{r.Y - 1} runs under the box {b.X},{b.Y} {b.Width}x{b.Height}");
                break;
            }
        var path = Path.Combine(dir, "overflow.txt");
        if (found.Count > 0) File.AppendAllLines(path, found.Select(f => $"{shot}: {f}"));
        else if (!File.Exists(path)) File.WriteAllText(path, "");
        found.Clear();
    }
}
