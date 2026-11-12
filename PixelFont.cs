using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// 5x7 bitmap font built at startup (uppercase only), so the HUD needs no SpriteFont content.
public sealed class PixelFont
{
    const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,:-/()!%+[]=><?'";
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
    };

    readonly Texture2D tex;
    static readonly sbyte[] CharIdx = BuildCharIdx();

    static sbyte[] BuildCharIdx()
    {
        var map = new sbyte[128];
        Array.Fill(map, (sbyte)Chars.IndexOf(' '));
        for (int i = 0; i < Chars.Length; i++) if (Chars[i] < 128) map[Chars[i]] = (sbyte)i;
        return map;
    }

    public PixelFont(GraphicsDevice gd)
    {
        if (Glyphs.Length != Chars.Length) throw new InvalidOperationException("font table out of sync");
        var data = new Color[Chars.Length * 6 * 8];
        for (int g = 0; g < Glyphs.Length; g++)
            for (int row = 0; row < 7; row++)
            {
                int bits = Convert.ToInt32(Glyphs[g].Substring(row * 2, 2), 16);
                for (int b = 0; b < 5; b++)
                    if ((bits >> (4 - b) & 1) != 0) data[row * Chars.Length * 6 + g * 6 + b] = Color.White;
            }
        tex = new Texture2D(gd, Chars.Length * 6, 8);
        tex.SetData(data);
    }

    public static int Width(string s, int scale) => s.Length * 6 * scale;

    public void Draw(SpriteBatch sb, string s, Vector2 pos, Color c, int scale = 2)
    {
        DrawRun(sb, s, pos + new Vector2(scale), new Color(0, 0, 0, 200), scale); // drop shadow
        DrawRun(sb, s, pos, c, scale);
    }

    void DrawRun(SpriteBatch sb, string s, Vector2 pos, Color c, int scale)
    {
        int space = CharIdx[' '];
        for (int k = 0; k < s.Length; k++)
        {
            char ch = s[k];
            if (ch is >= 'a' and <= 'z') ch = (char)(ch - 32);
            int i = ch < 128 ? CharIdx[ch] : space;
            sb.Draw(tex, pos, new Rectangle(i * 6, 0, 6, 8), c, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            pos.X += 6 * scale;
        }
    }
}
