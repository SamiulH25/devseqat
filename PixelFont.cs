using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hybrid typography engine:
/// - Crisp proportional pixel-art typography (Pixelify Sans Bold/Medium) across menus, headers, prose, and battle HUD
///   loaded directly from Data/fonts/*.ttf via FontStashSharp (no content pipeline).
/// - Tight proportional 5x7 bitmap pixel font (tabular numerals) for compact battle HUD counters, tags and fallback.
public sealed class PixelFont
{
    const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,:-/()!%+[]=><?'\u00b7";
    static readonly string[] Glyphs =
    {
        "0E11111F111111", "1E11111E11111E", "0E11101010110E", "1E11111111111E", "1F10101E10101F", "1F10101E101010",
        "0E11101711110F", "1111111F111111", "0E04040404040E", "0702020202120C", "11121418141211", "1010101010101F",
        "111B1515111111", "11111915131111", "0E11111111110E", "1E11111E101010", "0E11111115120D", "1E11111E141211",
        "0F10100E01011E", "1F040404040404", "1111111111110E", "11111111110A04", "1111111515150A", "11110A040A1111",
        "1111110A040404", "1F01020408101F",
        "0E11131519110E", "040C040404040E", "0E11010204081F", "1F02040201110E", "02060A121F0202", "1F101E0101110E",
        "0608101E11110E", "1F010204080808", "0E11110E11110E", "0E11110F01020C",
        "00000000000000", "00000000000C0C", "000000000C0408", "000C0C000C0C00", "0000001F000000", "01010204081010",
        "02040808080402", "08040202020408", "04040404040004", "18190204081303", "0004041F040400", "0E08080808080E",
        "0E02020202020E", "00001F001F0000", "08040201020408", "02040810080402", "0E110102040004", "04040800000000",
        "0000000C0C0000", // a middle dot, between the parts of a one-line card (docs/archive/OFFICERS.md OF4)
    };

    /// a-z, nine rows each (row 6 is the line the capitals stand on; rows 7-8 the descenders).
    static readonly string[] Lower =
    {
        "00000E010F110F0000", "1010161911111E0000", "00000E1010110E0000", "01010D1311110F0000", "00000E111F100E0000", "0609081C0808080000",
        "00000F1111110F010E", "101016191111110000", "04000C0404040E0000", "02000602020202120C", "101012141814120000", "0C04040404040E0000",
        "00001A151515150000", "000016191111110000", "00000E1111110E0000", "00001E1111111E1010", "00000F1111110F0101", "000016191010100000",
        "00000F100E011E0000", "08081C080809060000", "0000111111130D0000", "00001111110A040000", "0000111115150A0000", "0000110A040A110000",
        "0000111111110F010E", "00001F0204081F0000",
    };
    const int LowShadow = 16, LowPlain = 26;

    readonly Texture2D tex;
    static readonly byte[] GlyphLeft = new byte[Glyphs.Length];
    static readonly byte[] GlyphW = new byte[Glyphs.Length];
    static readonly byte[] GlyphAdv = new byte[Glyphs.Length];
    static readonly sbyte[] CharIdx = BuildCharIdx();

    readonly FontSystem? displaySys, bodySys, boldSys;
    public bool HasSerif => displaySys != null && bodySys != null;

    static sbyte[] BuildCharIdx()
    {
        var map = new sbyte[256];
        Array.Fill(map, (sbyte)Chars.IndexOf(' '));
        for (int i = 0; i < Chars.Length; i++) if (Chars[i] < 256) map[Chars[i]] = (sbyte)i;
        for (int g = 0; g < Glyphs.Length; g++)
        {
            char ch = Chars[g];
            if (ch == ' ') { GlyphLeft[g] = 0; GlyphW[g] = 0; GlyphAdv[g] = 4; continue; }
            if (ch is >= '0' and <= '9') { GlyphLeft[g] = 0; GlyphW[g] = 6; GlyphAdv[g] = 6; continue; } // tabular numerals for HUD counters
            int minC = 5, maxC = -1;
            for (int row = 0; row < 7; row++)
            {
                int bits = Convert.ToInt32(Glyphs[g].Substring(row * 2, 2), 16);
                for (int b = 0; b < 5; b++)
                    if ((bits >> (4 - b) & 1) != 0) { if (b < minC) minC = b; if (b > maxC) maxC = b; }
            }
            if (maxC < minC) { GlyphLeft[g] = 0; GlyphW[g] = 0; GlyphAdv[g] = 4; }
            else
            {
                int w = maxC - minC + 1;
                GlyphLeft[g] = (byte)minC;
                GlyphW[g] = (byte)(w + 1); // +1 for the 1px drop shadow
                GlyphAdv[g] = (byte)Math.Min(6, w + 2);
            }
        }
        return map;
    }

    static FontSystem? TryLoadFont(string file)
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var path = Path.Combine(dir, "Data", "fonts", file);
            if (!File.Exists(path)) continue;
            var fs = new FontSystem(new FontSystemSettings
            {
                FontResolutionFactor = 1f,
                KernelWidth = 0,
                KernelHeight = 0,
                TextureWidth = 512,
                TextureHeight = 512,
            });
            fs.AddFont(File.ReadAllBytes(path));
            return fs;
        }
        return null;
    }

    public PixelFont(GraphicsDevice gd)
    {
        if (Glyphs.Length != Chars.Length) throw new InvalidOperationException("font table out of sync");
        int cells = Chars.Length + 1, stride = cells * 6;
        var data = new Color[stride * 36];
        var shadow = new Color(0, 0, 0, 200);
        for (int g = 0; g < Glyphs.Length; g++)
            for (int row = 0; row < 7; row++)
            {
                int bits = Convert.ToInt32(Glyphs[g].Substring(row * 2, 2), 16);
                for (int b = 0; b < 5; b++)
                    if ((bits >> (4 - b) & 1) != 0) data[(row + 1) * stride + g * 6 + b + 1] = shadow;
            }
        for (int g = 0; g < Glyphs.Length; g++)
            for (int row = 0; row < 7; row++)
            {
                int bits = Convert.ToInt32(Glyphs[g].Substring(row * 2, 2), 16);
                for (int b = 0; b < 5; b++)
                    if ((bits >> (4 - b) & 1) != 0) data[row * stride + g * 6 + b] = data[(row + 8) * stride + g * 6 + b] = Color.White;
            }
        for (int g = 0; g < Lower.Length; g++)
            for (int row = 0; row < 9; row++)
            {
                int bits = Convert.ToInt32(Lower[g].Substring(row * 2, 2), 16);
                for (int b = 0; b < 5; b++)
                    if ((bits >> (4 - b) & 1) != 0) data[(LowShadow + row + 1) * stride + g * 6 + b + 1] = shadow;
            }
        for (int g = 0; g < Lower.Length; g++)
            for (int row = 0; row < 9; row++)
            {
                int bits = Convert.ToInt32(Lower[g].Substring(row * 2, 2), 16);
                for (int b = 0; b < 5; b++)
                    if ((bits >> (4 - b) & 1) != 0) data[(LowShadow + row) * stride + g * 6 + b] = data[(LowPlain + row) * stride + g * 6 + b] = Color.White;
            }
        for (int row = 0; row < 8; row++) for (int b = 0; b < 6; b++) data[row * stride + Chars.Length * 6 + b] = Color.White;
        tex = new Texture2D(gd, stride, 36);
        tex.SetData(data);

        bodySys = TryLoadFont("PixelifySans-Medium.ttf");
        boldSys = TryLoadFont("PixelifySans-Bold.ttf") ?? bodySys;
        displaySys = boldSys;
    }

    /// The texture holding the glyphs and a white texel: boxes drawn from it batch with text.
    public Texture2D Texture => tex;
    /// One white texel well inside the white cell (point sampling: no bleed from the neighbours).
    public Rectangle White => new(Chars.Length * 6 + 2, 3, 1, 1);

    /// Legacy fixed-cell width formula (kept for deterministic tests).
    public static int Width(string s, float scale) => (int)MathF.Ceiling(s.Length * 6 * scale);

    /// Tight proportional pixel-font width (tabular digits, narrow I/L/punctuation) for compact battle HUD counters and tags.
    public static int WidthPixel(string s, float scale)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int space = CharIdx[' '];
        int adv = 0;
        for (int k = 0; k < s.Length; k++)
        {
            char ch = s[k];
            if (ch is >= 'a' and <= 'z') ch = (char)(ch - 32);
            int i = ch < 256 ? CharIdx[ch] : space;
            adv += GlyphAdv[i];
        }
        return (int)MathF.Ceiling(adv * scale);
    }

    /// Is the font's pixel at (col 0-4, row 0-6) of this character set? For drawing a glyph block by block (the title's name).
    public static bool Lit(char ch, int col, int row)
    {
        if (col < 0 || col > 4 || row < 0 || row > 6 || ch >= 128) return false;
        return (Convert.ToInt32(Glyphs[CharIdx[ch]].Substring(row * 2, 2), 16) >> (4 - col) & 1) != 0;
    }

    // ---------- TTF medieval serif (Cinzel-Bold for display/headers, Alegreya-Medium for body/prose) ----------

    (DynamicSpriteFont Font, float YOffset) ResolveSerif(int scale, float uiScale, bool prose)
    {
        if (prose)
        {
            int px = Math.Clamp((int)MathF.Round(scale * 11.2f * uiScale), 10, 64);
            return (bodySys!.GetFont(px), -MathF.Floor(px * 0.20f));
        }
        if (scale >= 2)
        {
            int px = Math.Clamp((int)MathF.Round(scale * 8.8f * uiScale), 12, 96);
            return (displaySys!.GetFont(px), -MathF.Floor(px * 0.20f));
        }
        else
        {
            int px = Math.Clamp((int)MathF.Round(10.8f * uiScale), 9, 48);
            return (boldSys!.GetFont(px), -MathF.Floor(px * 0.24f));
        }
    }

    (DynamicSpriteFont Font, float YOffset) ResolveHud(int scale, float uiScale)
    {
        if (scale >= 2)
        {
            int px = Math.Clamp((int)MathF.Round(scale * 8.4f * uiScale), 12, 96);
            return (boldSys!.GetFont(px), -MathF.Floor(px * 0.20f));
        }
        else
        {
            int px = Math.Clamp((int)MathF.Round(10.6f * uiScale), 9, 48);
            return (bodySys!.GetFont(px), -MathF.Floor(px * 0.24f));
        }
    }

    public int MeasureHud(string s, int scale, float uiScale)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        if (!HasSerif) return WidthPixel(s, scale * uiScale);
        var (f, _) = ResolveHud(scale, uiScale);
        return (int)MathF.Ceiling(f.MeasureString(s).X);
    }

    public void DrawHud(SpriteBatch sb, string s, Vector2 pos, Color c, int scale, float uiScale)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (!HasSerif) { DrawPixel(sb, s, pos, c, scale * uiScale, false); return; }
        var (f, yOff) = ResolveHud(scale, uiScale);
        var p = new Vector2(MathF.Floor(pos.X), MathF.Floor(pos.Y) + yOff);
        float d = MathF.Max(1f, MathF.Floor(uiScale));
        f.DrawText(sb, s, p + new Vector2(d, d), new Color(0, 0, 0, (int)(c.A * 0.85f)));
        f.DrawText(sb, s, p, c);
    }

    public int MeasureSerif(string s, int scale, float uiScale, bool prose = false)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        if (!HasSerif) return WidthPixel(s, scale * uiScale);
        var (f, _) = ResolveSerif(scale, uiScale, prose);
        return (int)MathF.Ceiling(f.MeasureString(s).X);
    }

    public void DrawSerif(SpriteBatch sb, string s, Vector2 pos, Color c, int scale, float uiScale, bool shadow = true, bool prose = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (!HasSerif) { DrawPixel(sb, s, pos, c, scale * uiScale, !shadow); return; }
        var (f, yOff) = ResolveSerif(scale, uiScale, prose);
        var p = new Vector2(MathF.Floor(pos.X), MathF.Floor(pos.Y) + yOff);
        if (shadow)
        {
            float d = MathF.Max(1f, MathF.Floor(uiScale));
            f.DrawText(sb, s, p + new Vector2(d, d), new Color(0, 0, 0, (int)(c.A * 0.82f)));
        }
        f.DrawText(sb, s, p, c);
    }

    // ---------- tight proportional pixel font (for compact battle HUD) ----------

    public void DrawPixel(SpriteBatch sb, string s, Vector2 pos, Color c, float scale = 2, bool plain = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        int space = CharIdx[' '];
        int top = plain ? 8 : 0;
        float x = MathF.Floor(pos.X), y = MathF.Floor(pos.Y);
        for (int k = 0; k < s.Length; k++)
        {
            char ch = s[k];
            if (ch is >= 'a' and <= 'z') ch = (char)(ch - 32);
            int i = ch < 256 ? CharIdx[ch] : space;
            int w = GlyphW[i];
            if (w > 0)
            {
                var src = new Rectangle(i * 6 + GlyphLeft[i], top, w, 8);
                sb.Draw(tex, new Vector2(MathF.Floor(x), y), src, c, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            }
            x += GlyphAdv[i] * scale;
        }
    }

    // ---------- legacy monospace entry points (kept for compatibility) ----------

    public void Draw(SpriteBatch sb, string s, Vector2 pos, Color c, float scale = 2)
    {
        DrawPixel(sb, s, pos, c, scale, false);
    }

    public void DrawPlain(SpriteBatch sb, string s, Vector2 pos, Color c, float scale = 2) => DrawPixel(sb, s, pos, c, scale, true);

    public void DrawMixed(SpriteBatch sb, string s, Vector2 pos, Color c, float scale = 2, bool plain = false)
    {
        int space = CharIdx[' '];
        for (int k = 0; k < s.Length; k++)
        {
            char ch = s[k];
            var src = ch is >= 'a' and <= 'z' ? new Rectangle((ch - 'a') * 6, plain ? LowPlain : LowShadow, 6, 10)
                : new Rectangle((ch < 256 ? CharIdx[ch] : space) * 6, plain ? 8 : 0, 6, 8);
            sb.Draw(tex, new Vector2(MathF.Floor(pos.X), MathF.Floor(pos.Y)), src, c, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            pos.X += 6 * scale;
        }
    }
}
