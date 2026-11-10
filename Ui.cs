using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Immediate-mode interface kit for the campaign screens, in the DESIGN.md panel style: draw and handle a widget in one call
/// (`if (ui.Button(...))`). Panels remember their rectangles so a click on the interface never reaches the map beneath.
sealed class Ui
{
    public readonly SpriteBatch Sb;
    readonly Art art;
    readonly PixelFont font;
    public int S = 1, W, H;
    public Point Mouse;
    bool click;
    List<Rectangle> blockers = new(), last = new();

    /// Every button click calls this (the host plays a click).
    public static Action? OnClick;
    /// Called when the mouse comes onto a new button (a soft tick).
    public static Action? OnHover;
    static Rectangle hoverWas;
    static bool hoverSeen;

    /// The mouse is on button `r` this frame: tick once when it arrives on a new one.
    static void Hovered(Rectangle r)
    {
        if (hoverSeen) return;
        hoverSeen = true;
        if (r != hoverWas) { hoverWas = r; OnHover?.Invoke(); }
    }

    /// End of a frame: forget the hovered button once the mouse has left every button.
    public static void FrameEnd() { if (!hoverSeen) hoverWas = Rectangle.Empty; hoverSeen = false; }
    /// Characters typed since the last frame (the window's text input), for renaming companies.
    public static readonly Queue<char> Typed = new();

    public Ui(SpriteBatch sb, Art art, PixelFont font) { Sb = sb; this.art = art; this.font = font; }

    // ---------- motion (docs/archive/VISUALS.md V5) ----------

    /// Real time in seconds (not the sim's), set by the host each frame; every interface animation runs on it.
    public static float Now;
    /// Screenshot runs: every animation is at its end at once, so staged shots never catch a panel half faded in.
    public static bool Instant;
    static readonly Dictionary<int, (float V, float Lag, float First, float Last)> anims = new();

    static (float V, float Lag, float First, float Last) Entry(int key, float start)
    {
        if (anims.TryGetValue(key, out var e) && Now - e.Last < 0.2f) return e;
        if (anims.Count > 4000) foreach (var k in anims.Where(a => Now - a.Value.Last > 5).Select(a => a.Key).ToList()) anims.Remove(k);
        return (start, start, Now, Now); // new, or not drawn for a while: start fresh
    }

    /// A value that follows `target` smoothly (`rate` per second: 25 is about 80 ms, 8 about a quarter second). A key not seen
    /// in the last frames starts at the target, so a panel that opens shows its values as they are.
    public static float Anim(int key, float target, float rate = 12)
    {
        var e = Entry(key, target);
        float dt = Math.Clamp(Now - e.Last, 0, 0.1f);
        e.V = Instant ? target : e.V + (target - e.V) * (1 - MathF.Exp(-rate * dt));
        e.Last = Now;
        anims[key] = e;
        return e.V;
    }

    /// Like Anim, and also a slow trailing value that only falls (the "lost" part of a bar drains after a drop).
    public static (float Shown, float Lag) AnimLag(int key, float target)
    {
        var e = Entry(key, target);
        float dt = Math.Clamp(Now - e.Last, 0, 0.1f);
        e.V = Instant ? target : e.V + (target - e.V) * (1 - MathF.Exp(-14 * dt));
        if (Instant || target >= e.Lag) e.Lag = e.V; // rising (or settled): nothing lost to show
        else e.Lag = MathF.Max(e.V, e.Lag + (target - e.Lag) * (1 - MathF.Exp(-2.5f * dt)));
        e.Last = Now;
        anims[key] = e;
        return (e.V, e.Lag);
    }

    /// Seconds since `key` began to be drawn, frame after frame (a gap restarts it): drives fade-ins and tooltip delays.
    public static float Since(int key)
    {
        if (Instant) return 99;
        var e = Entry(key, 0);
        e.Last = Now;
        anims[key] = e;
        return Now - e.First;
    }

    /// A number that counts up from 0 to `value` over about a second when first shown (results screens).
    public static int Count(int key, int value)
    {
        if (Instant) return value;
        float t = Since(key ^ 0x5bd1e995);
        return (int)MathF.Round(value * Ease.OutCubic(t / 1.1f));
    }

    public static int Key(Rectangle r, int salt = 0) => HashCode.Combine(r.X, r.Y, r.Width, r.Height, salt);

    /// Start a frame for a caller that runs its own SpriteBatch (the battle HUD): scale, size and mouse, no clicks.
    public void Reset(int scale, int w, int h, Point mouse) { S = scale; W = w; H = h; Mouse = mouse; click = false; blockers = new List<Rectangle>(); }

    public void Begin(Viewport vp, Point mouse, bool clicked)
    {
        W = vp.Width; H = vp.Height;
        S = Settings.UiFor(H);
        Mouse = mouse;
        click = clicked;
        blockers = new List<Rectangle>();
        Sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
    }

    public void End() { Sb.End(); last = blockers; }

    /// Was the interface (as drawn last frame) under p?
    public bool Blocks(Point p) => last.Any(r => r.Contains(p));
    public void Block(Rectangle r) => blockers.Add(r);
    public int U(float v) => (int)(v * S);

    public void Rect(Rectangle r, Color c) => Sb.Draw(art.Pixel, r, c);
    /// The 1x1 white texture, for drawing rotated lines.
    public Texture2D Pixel => art.Pixel;

    public void Frame(Rectangle r, Color c, int t)
    {
        Rect(new Rectangle(r.X, r.Y, r.Width, t), c);
        Rect(new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        Rect(new Rectangle(r.X, r.Y, t, r.Height), c);
        Rect(new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    /// The iron plate (Kit.PlateBody): the one panel. Emphasis comes from the edge colour and weight.
    public void Panel(Rectangle r, Color? edge = null, int thick = 1) => this.PlateBody(r, edge ?? Kit.Rivet, thick, 0.92f);

    /// Dim everything behind a modal panel (and swallow clicks outside it).
    public void Dim(float a = 0.5f) { Rect(new Rectangle(0, 0, W, H), Color.Black * (a * Ease.Out(Since(0x0d1a) / 0.15f))); Block(new Rectangle(0, 0, W, H)); }

    /// Text at whole-pixel positions (a half-pixel offset makes the point-sampled glyphs lose rows).
    public void Text(string s, float x, float y, Color c, int scale = 2)
    {
        Overflow.Text(s, MathF.Floor(x), MathF.Floor(y), TW(s, scale));
        font.Draw(Sb, s, new Vector2(MathF.Floor(x), MathF.Floor(y)), c, scale * S);
    }
    public int TW(string s, int scale = 2) => PixelFont.Width(s, scale * S);
    public void TextC(string s, float cx, float y, Color c, int scale = 2) => Text(s, cx - TW(s, scale) / 2f, y, c, scale);
    public void TextR(string s, float rx, float y, Color c, int scale = 2) => Text(s, rx - TW(s, scale), y, c, scale);

    /// A bar on a dark trough, the fill shaded top to bottom with a lit top line once it is tall enough to show one.
    /// The fill slides to a new value; a drop leaves a pale "lost" stretch that drains after it.
    public void Bar(Rectangle r, float frac, Color c)
    {
        Rect(r, Color.Black * 0.6f);
        var (shown, lag) = AnimLag(Key(r, 0xba2), Math.Clamp(frac, 0, 1));
        var f = new Rectangle(r.X, r.Y, (int)(r.Width * shown), r.Height);
        int lost = (int)(r.Width * lag) - f.Width;
        if (lost > 0) Rect(new Rectangle(f.Right, r.Y, lost, r.Height), Color.Lerp(c, new Color(255, 236, 220), 0.55f) * 0.8f);
        if (f.Width <= 0) return;
        if (r.Height < U(4)) { Rect(f, c); return; }
        this.VGrad(f, Kit.Mul(c, 1.12f), Kit.Mul(c, 0.7f), 1);
        Rect(new Rectangle(f.X, f.Y, f.Width, U(1)), Color.White * 0.3f);
    }

    public bool Hover(Rectangle r) => r.Contains(Mouse);

    /// A clicked rectangle (consumes the click).
    public bool Clicked(Rectangle r)
    {
        if (!click || !r.Contains(Mouse)) return false;
        click = false;
        return true;
    }

    /// The one button, a bevelled plate: label centred (or left, with a key or note on the right); gold edge and wash on
    /// hover or when active; dark and dim when disabled.
    public bool Button(Rectangle r, string label, bool enabled = true, string note = "", bool active = false)
    {
        bool hover = enabled && Hover(r);
        if (hover) Hovered(r);
        float h = Anim(Key(r, 0xb7), hover ? 1 : 0, 25); // hover brightens over about 80 ms
        Overflow.Box(r);
        if (r.Height >= U(18)) Rect(new Rectangle(r.X + U(1), r.Y + U(2), r.Width, r.Height), Color.Black * 0.35f);
        var (top, bottom) = active ? (Kit.Mul(Kit.Gilt, 0.5f), Kit.Mul(Kit.Gilt, 0.22f))
            : enabled ? (Color.Lerp(new Color(46, 44, 46), Kit.Mul(Kit.Gilt, 0.4f), h), Color.Lerp(new Color(24, 24, 28), Kit.Mul(Kit.Gilt, 0.15f), h))
            : (new Color(30, 30, 34), new Color(20, 20, 24));
        this.VGrad(r, top, bottom, 1);
        Frame(r, active ? Kit.Gilt : enabled ? Color.Lerp(Kit.Mul(Kit.Rivet, 0.85f), Kit.Gilt, h) : Color.White * 0.1f, U(1));
        Rect(new Rectangle(r.X + U(1), r.Y + U(1), r.Width - U(2), U(1)), Color.White * (active ? 0.3f : 0.12f + 0.18f * h));
        if (r.Height >= U(18)) Rect(new Rectangle(r.X + U(1), r.Bottom - U(2), r.Width - U(2), U(1)), Color.Black * 0.35f);
        var c = enabled ? (hover || active ? new Color(255, 246, 222) : Style.Text) : Style.Dim * 0.5f;
        int scale = r.Height >= U(26) ? 2 : 1;
        int ty = r.Y + (r.Height - 7 * scale * S) / 2;
        if (note == "") TextC(label, r.X + r.Width / 2f, ty, c, scale);
        else { Text(label, r.X + U(6), ty, c, scale); TextR(note, r.Right - U(6), r.Y + (r.Height - 7 * S) / 2f, Style.Dim, 1); }
        Block(r);
        bool hit = enabled && Clicked(r);
        if (hit) OnClick?.Invoke();
        return hit;
    }

    /// A troop portrait cropped to `box`; `k` is the pixel scale (default: the UI scale).
    public void Portrait(UnitType t, Rectangle box, int k = 0) => Hud.Portrait(Sb, art, t, box, k > 0 ? k : S);
}
