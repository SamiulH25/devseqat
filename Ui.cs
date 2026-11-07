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
    /// Characters typed since the last frame (the window's text input), for renaming companies.
    public static readonly Queue<char> Typed = new();

    public Ui(SpriteBatch sb, Art art, PixelFont font) { Sb = sb; this.art = art; this.font = font; }

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

    public void Panel(Rectangle r, Color? edge = null, int thick = 1)
    {
        Rect(r, Style.Panel);
        Rect(new Rectangle(r.X + U(thick), r.Y + U(thick), r.Width - 2 * U(thick), U(1)), Style.PanelLight);
        Frame(r, edge ?? Style.PanelEdge, U(thick));
        Block(r);
    }

    /// Dim everything behind a modal panel (and swallow clicks outside it).
    public void Dim(float a = 0.5f) { Rect(new Rectangle(0, 0, W, H), Color.Black * a); Block(new Rectangle(0, 0, W, H)); }

    /// Text at whole-pixel positions (a half-pixel offset makes the point-sampled glyphs lose rows).
    public void Text(string s, float x, float y, Color c, int scale = 2) => font.Draw(Sb, s, new Vector2(MathF.Floor(x), MathF.Floor(y)), c, scale * S);
    public int TW(string s, int scale = 2) => PixelFont.Width(s, scale * S);
    public void TextC(string s, float cx, float y, Color c, int scale = 2) => Text(s, cx - TW(s, scale) / 2f, y, c, scale);
    public void TextR(string s, float rx, float y, Color c, int scale = 2) => Text(s, rx - TW(s, scale), y, c, scale);

    public void Bar(Rectangle r, float frac, Color c)
    {
        Rect(r, Color.Black * 0.55f);
        Rect(new Rectangle(r.X, r.Y, (int)(r.Width * Math.Clamp(frac, 0, 1)), r.Height), c);
    }

    public bool Hover(Rectangle r) => r.Contains(Mouse);

    /// A clicked rectangle (consumes the click).
    public bool Clicked(Rectangle r)
    {
        if (!click || !r.Contains(Mouse)) return false;
        click = false;
        return true;
    }

    /// The one button: label left, key or note right; gold edge and wash on hover; dim when disabled.
    public bool Button(Rectangle r, string label, bool enabled = true, string note = "", bool active = false)
    {
        bool hover = enabled && Hover(r);
        Rect(r, active ? Style.Accent * 0.3f : hover ? Style.Accent * 0.3f : Color.White * 0.06f);
        Frame(r, active || hover ? Style.Accent : Style.PanelEdge, U(1));
        var c = enabled ? Style.Text : Style.Dim * 0.6f;
        int scale = r.Height >= U(26) ? 2 : 1;
        float ty = r.Y + (r.Height - 7 * scale * S) / 2f;
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
