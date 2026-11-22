using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// 5x7 bitmap font built at startup, so the HUD needs no SpriteFont content. The interface writes in capitals; a lowercase set
/// (5x9, two rows below the line for descenders) is used only by `DrawMixed`, for prose such as the event book.
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
    /// Texture rows of the lowercase cells: with their shadow, and plain (ink on paper). Ten rows a cell.
    const int LowShadow = 16, LowPlain = 26;

    readonly Texture2D tex;
    static readonly sbyte[] CharIdx = BuildCharIdx();

    static sbyte[] BuildCharIdx()
    {
        var map = new sbyte[256];
        Array.Fill(map, (sbyte)Chars.IndexOf(' '));
        for (int i = 0; i < Chars.Length; i++) if (Chars[i] < 256) map[Chars[i]] = (sbyte)i;
        return map;
    }

    public PixelFont(GraphicsDevice gd)
    {
        if (Glyphs.Length != Chars.Length) throw new InvalidOperationException("font table out of sync");
        // One cell a glyph, 6 x 8 texels: the white 5 x 7 glyph and, under it one texel down and right, its drop shadow (premultiplied
        // black at 200/255, so a tint scales it with the glyph). One sprite a character then draws both. The last cell is solid
        // white: rectangles are drawn from this texture too, so text and boxes batch together.
        int cells = Chars.Length + 1, stride = cells * 6;
        var data = new Color[stride * 36]; // rows 8-15: the same glyphs with no shadow, for writing on paper; 16-35: lowercase
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
    }

    /// The texture holding the glyphs and a white texel: boxes drawn from it batch with text.
    public Texture2D Texture => tex;
    /// One white texel well inside the white cell (point sampling: no bleed from the neighbours).
    public Rectangle White => new(Chars.Length * 6 + 2, 3, 1, 1);

    public static int Width(string s, int scale) => s.Length * 6 * scale;

    /// Is the font's pixel at (col 0-4, row 0-6) of this character set? For drawing a glyph block by block (the title's name).
    public static bool Lit(char ch, int col, int row)
    {
        if (col < 0 || col > 4 || row < 0 || row > 6 || ch >= 128) return false;
        return (Convert.ToInt32(Glyphs[CharIdx[ch]].Substring(row * 2, 2), 16) >> (4 - col) & 1) != 0;
    }

    public void Draw(SpriteBatch sb, string s, Vector2 pos, Color c, int scale = 2)
    {
        DrawRun(sb, s, pos, c, scale); // the glyph and its shadow are one cell of the texture
    }

    /// The glyphs alone, with no black shadow under them (ink on paper, where a dark shadow would smear dark letters).
    public void DrawPlain(SpriteBatch sb, string s, Vector2 pos, Color c, int scale = 2) => DrawRun(sb, s, pos, c, scale, 8);

    /// Capitals and lowercase as written, for prose (the event book). A line takes ten rows, not eight: leave room for the
    /// descenders. `plain` drops the shadow (ink on paper).
    public void DrawMixed(SpriteBatch sb, string s, Vector2 pos, Color c, int scale = 2, bool plain = false)
    {
        int space = CharIdx[' '];
        for (int k = 0; k < s.Length; k++)
        {
            char ch = s[k];
            var src = ch is >= 'a' and <= 'z' ? new Rectangle((ch - 'a') * 6, plain ? LowPlain : LowShadow, 6, 10)
                : new Rectangle((ch < 256 ? CharIdx[ch] : space) * 6, plain ? 8 : 0, 6, 8);
            sb.Draw(tex, pos, src, c, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            pos.X += 6 * scale;
        }
    }

    void DrawRun(SpriteBatch sb, string s, Vector2 pos, Color c, int scale, int top = 0)
    {
        int space = CharIdx[' '];
        for (int k = 0; k < s.Length; k++)
        {
            char ch = s[k];
            if (ch is >= 'a' and <= 'z') ch = (char)(ch - 32);
            int i = ch < 256 ? CharIdx[ch] : space;
            sb.Draw(tex, pos, new Rectangle(i * 6, top, 6, 8), c, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            pos.X += 6 * scale;
        }
    }
}
