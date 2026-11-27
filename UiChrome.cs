using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Procedural "Forged Iron & Aged Brass" 9-slice UI chrome atlas, baked once per GraphicsDevice.
/// Provides warm charcoal-iron hammered panels, chamfered antique-brass double frames with corner brackets,
/// beveled metallic buttons, and recessed gauge troughs with zero external assets and minimal draw calls.
sealed class UiChrome
{
    static GraphicsDevice? cachedGd;
    static UiChrome? cached;

    public static UiChrome For(GraphicsDevice gd)
    {
        if (cached != null && cachedGd == gd && !cached.Atlas.IsDisposed) return cached;
        cachedGd = gd;
        return cached = new UiChrome(gd);
    }

    public readonly Texture2D Atlas;

    // Source rectangles inside Atlas (256 x 128)
    public static readonly Rectangle PlateFill   = new(0, 0, 64, 64);
    public static readonly Rectangle PlateFrame  = new(64, 0, 64, 64);
    public static readonly Rectangle BtnFill     = new(128, 0, 48, 48);
    public static readonly Rectangle BtnFrame    = new(176, 0, 48, 48);
    public static readonly Rectangle Trough      = new(128, 48, 32, 16);
    public static readonly Rectangle GaugeFill   = new(160, 48, 32, 16);

    const int W = 256, H = 128;

    UiChrome(GraphicsDevice gd)
    {
        var px = new Color[W * H];
        BakePlateFill(px, PlateFill);
        BakePlateFrame(px, PlateFrame);
        BakeBtnFill(px, BtnFill);
        BakeBtnFrame(px, BtnFrame);
        BakeTrough(px, Trough);
        BakeGaugeFill(px, GaugeFill);
        Atlas = new Texture2D(gd, W, H, false, SurfaceFormat.Color);
        Atlas.SetData(px);
    }

    static float Noise(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }

    static bool Chamfer(int x, int y, int w, int h, int cut)
    {
        int dx = Math.Min(x, w - 1 - x);
        int dy = Math.Min(y, h - 1 - y);
        return dx + dy < cut;
    }

    static void Set(Color[] px, Rectangle r, int x, int y, Color c) => px[(r.Y + y) * W + (r.X + x)] = c;

    /// Warm forged charcoal-iron body with subtle hammered micro-grain, smooth vertical sheen, and recessed inner shadow.
    static void BakePlateFill(Color[] px, Rectangle r)
    {
        int w = r.Width, h = r.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (Chamfer(x, y, w, h, 3)) continue;
                float v = y / (float)(h - 1);
                float sheen = MathF.Pow(1f - v, 1.35f);
                // Warm charcoal-iron ramp (umber-tinted dark steel, silky smooth even on full-screen modals)
                float cr = MathHelper.Lerp(17f, 35f, sheen) / 255f;
                float cg = MathHelper.Lerp(15f, 32f, sheen) / 255f;
                float cb = MathHelper.Lerp(17f, 34f, sheen) / 255f;

                int dEdge = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
                if (dEdge == 0) { cr *= 0.35f; cg *= 0.35f; cb *= 0.35f; }
                else if (dEdge <= 3) { cr *= 0.72f; cg *= 0.72f; cb *= 0.72f; } // recessed bevel well
                else if (y == 4 && x >= 4 && x <= w - 5) { cr += 0.06f; cg += 0.055f; cb += 0.045f; } // subtle top inner catchlight

                Set(px, r, x, y, new Color(Math.Clamp(cr, 0, 1), Math.Clamp(cg, 0, 1), Math.Clamp(cb, 0, 1), 1f));
            }
    }

    /// Tintable antique-brass frame: dark obsidian outer rim, beveled metallic border, inner hairline, and embossed L-corner brackets.
    static void BakePlateFrame(Color[] px, Rectangle r)
    {
        int w = r.Width, h = r.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int dx = Math.Min(x, w - 1 - x);
                int dy = Math.Min(y, h - 1 - y);
                int d = Math.Min(dx, dy);
                int manh = dx + dy;
                if (manh < 3) continue;

                // Outer obsidian shadow rim
                if (manh == 3 || d == 0)
                {
                    Set(px, r, x, y, new Color(0, 0, 0, 215));
                    continue;
                }
                // Primary beveled brass border (brighter at top/left, richer shade at bottom/right)
                if (d == 1 || manh == 4)
                {
                    float light = (y < h / 2 ? 1.05f : 0.78f) * (x < w / 2 ? 1.03f : 0.92f);
                    byte v = (byte)Math.Clamp((int)(215 * light), 0, 255);
                    Set(px, r, x, y, new Color(v, v, v, (byte)240));
                    continue;
                }
                // Dark recessed channel between outer rim and inner filigree
                if (d == 2)
                {
                    Set(px, r, x, y, new Color(0, 0, 0, 130));
                    continue;
                }
                // Subtle inner hairline
                if (d == 4 && dx >= 4 && dy >= 4)
                {
                    Set(px, r, x, y, new Color(255, 255, 255, 34));
                }
                // Ornate forged L-corner brackets (11px arms along each corner)
                bool inCornerArm = (dx <= 10 && dy == 3) || (dy <= 10 && dx == 3) || (dx <= 7 && dy == 2) || (dy <= 7 && dx == 2);
                if (inCornerArm && manh >= 5)
                {
                    float tip = MathF.Max(dx, dy) > 8 ? 0.65f : 1.0f;
                    byte v = (byte)Math.Clamp((int)(230 * tip), 0, 255);
                    Set(px, r, x, y, new Color(v, v, v, (byte)(210 * tip)));
                }
                // Embossed corner brass stud at (5..7, 5..7)
                if (dx >= 5 && dx <= 7 && dy >= 5 && dy <= 7)
                {
                    bool hi = (dx == 5 && dy == 5) || (dx == 6 && dy == 5);
                    bool lo = dx == 7 || dy == 7;
                    byte v = hi ? (byte)255 : lo ? (byte)110 : (byte)195;
                    Set(px, r, x, y, new Color(v, v, v, (byte)220));
                }
            }
    }

    /// Smooth metallic button body (white/greyscale so callers tint cleanly for normal / hover / active / disabled).
    static void BakeBtnFill(Color[] px, Rectangle r)
    {
        int w = r.Width, h = r.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (Chamfer(x, y, w, h, 2)) continue;
                float v = y / (float)(h - 1);
                float lum = MathHelper.Lerp(1.00f, 0.58f, MathF.Pow(v, 0.85f));
                if (y == 1) lum += 0.16f;
                else if (y == h - 2) lum *= 0.72f;
                byte b = (byte)Math.Clamp((int)(lum * 255f), 0, 255);
                Set(px, r, x, y, new Color(b, b, b, (byte)255));
            }
    }

    /// Chamfered metallic button rim with crisp corner ticks (tinted by edge colour).
    static void BakeBtnFrame(Color[] px, Rectangle r)
    {
        int w = r.Width, h = r.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int dx = Math.Min(x, w - 1 - x);
                int dy = Math.Min(y, h - 1 - y);
                int d = Math.Min(dx, dy);
                int manh = dx + dy;
                if (manh < 2) continue;
                if (manh == 2 || d == 0)
                {
                    float light = y < h / 2 ? 1.0f : 0.75f;
                    byte v = (byte)(225 * light);
                    Set(px, r, x, y, new Color(v, v, v, (byte)235));
                }
                else if (d == 1 && (dx <= 4 || dy <= 4))
                {
                    Set(px, r, x, y, new Color(255, 255, 255, 90));
                }
            }
    }

    /// Recessed dark iron gauge trough.
    static void BakeTrough(Color[] px, Rectangle r)
    {
        int w = r.Width, h = r.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int d = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
                if (y == 0 || x == 0) Set(px, r, x, y, new Color(6, 5, 7, 230));
                else if (y == h - 1 || x == w - 1) Set(px, r, x, y, new Color(70, 64, 56, 120));
                else if (d == 1) Set(px, r, x, y, new Color(10, 9, 11, 220));
                else Set(px, r, x, y, new Color(16, 14, 17, 215));
            }
    }

    /// Smooth beveled metallic gauge fill (white/greyscale, tinted by fill colour).
    static void BakeGaugeFill(Color[] px, Rectangle r)
    {
        int w = r.Width, h = r.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (float)(h - 1);
                float lum = y == 0 ? 1.0f : MathHelper.Lerp(0.95f, 0.62f, v);
                byte b = (byte)Math.Clamp((int)(lum * 255f), 0, 255);
                Set(px, r, x, y, new Color(b, b, b, (byte)255));
            }
    }

    /// Draw a 9-slice region from Atlas into dst with corner border b (scaled to screen pixels).
    public void Draw9(SpriteBatch sb, Rectangle dst, Rectangle src, int srcB, int dstB, Color col)
    {
        if (dst.Width <= 0 || dst.Height <= 0) return;
        int b = Math.Min(dstB, Math.Min(dst.Width / 2, dst.Height / 2));
        int sbB = Math.Min(srcB, Math.Min(src.Width / 2, src.Height / 2));
        int sx0 = src.X, sx1 = src.X + sbB, sx2 = src.Right - sbB;
        int sy0 = src.Y, sy1 = src.Y + sbB, sy2 = src.Bottom - sbB;
        int swM = Math.Max(1, src.Width - 2 * sbB), shM = Math.Max(1, src.Height - 2 * sbB);

        int dx0 = dst.X, dx1 = dst.X + b, dx2 = dst.Right - b;
        int dy0 = dst.Y, dy1 = dst.Y + b, dy2 = dst.Bottom - b;
        int dwM = Math.Max(0, dst.Width - 2 * b), dhM = Math.Max(0, dst.Height - 2 * b);

        // Corners
        if (b > 0)
        {
            sb.Draw(Atlas, new Rectangle(dx0, dy0, b, b), new Rectangle(sx0, sy0, sbB, sbB), col);
            sb.Draw(Atlas, new Rectangle(dx2, dy0, b, b), new Rectangle(sx2, sy0, sbB, sbB), col);
            sb.Draw(Atlas, new Rectangle(dx0, dy2, b, b), new Rectangle(sx0, sy2, sbB, sbB), col);
            sb.Draw(Atlas, new Rectangle(dx2, dy2, b, b), new Rectangle(sx2, sy2, sbB, sbB), col);
        }
        // Edges
        if (dwM > 0 && b > 0)
        {
            sb.Draw(Atlas, new Rectangle(dx1, dy0, dwM, b), new Rectangle(sx1, sy0, swM, sbB), col);
            sb.Draw(Atlas, new Rectangle(dx1, dy2, dwM, b), new Rectangle(sx1, sy2, swM, sbB), col);
        }
        if (dhM > 0 && b > 0)
        {
            sb.Draw(Atlas, new Rectangle(dx0, dy1, b, dhM), new Rectangle(sx0, sy1, sbB, shM), col);
            sb.Draw(Atlas, new Rectangle(dx2, dy1, b, dhM), new Rectangle(sx2, sy1, sbB, shM), col);
        }
        // Center
        if (dwM > 0 && dhM > 0)
            sb.Draw(Atlas, new Rectangle(dx1, dy1, dwM, dhM), new Rectangle(sx1, sy1, swM, shM), col);
    }
}
