using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Placeholder pixel art, generated at startup so the prototype needs no asset pipeline.
///
/// Units are puppets (see Puppet.cs): every body part is its own small sprite, written below as ASCII pixel art.
/// View-dependent parts have ".f" (front), ".s" (side, facing right) and ".b" (back) variants.
/// Each sprite is built once per palette and keyed "name/palette"; the atlas packs them with a 1px outline border.
/// Swapping in real art later = replacing these strings with PNGs that keep the same names.
public sealed class Art
{
    public const float Px = 0.075f; // metres per sprite pixel: a soldier is ~24 px tall

    public readonly Texture2D Atlas, Shadow, Grass, Pixel;
    public Rectangle this[string key] => rects[key];

    static readonly (Color Main, Color Dark)[] Palettes =
    {
        (new(60, 100, 200), new(32, 54, 124)),  // team 0
        (new(188, 58, 46), new(112, 32, 26)),   // team 1
        (new(218, 176, 56), new(142, 100, 26)), // player
    };
    static readonly Color Ink = new(22, 20, 26), Wood = new(124, 84, 44);

    // . clear  M team colour  D team dark  S skin  K ink  H hair  T metal  W wood  B boot  P plume  C cord  R blood
    // N horse coat  E mane / tail
    static Color Map(char ch, (Color Main, Color Dark) pal) => ch switch
    {
        'M' => pal.Main, 'D' => pal.Dark, 'S' => new(232, 190, 150), 'K' => Ink, 'H' => new(92, 60, 36),
        'T' => new(178, 184, 196), 'W' => Wood, 'B' => new(58, 40, 26), 'P' => new(240, 240, 240),
        'C' => new(225, 225, 215), 'R' => new(130, 20, 20), 'N' => new(124, 84, 52), 'E' => new(52, 34, 24),
        _ => Color.Transparent,
    };

    static readonly Dictionary<string, string[]> Sprites = new()
    {
        ["head.f"] = new[] { ".SSSS.", "SSSSSS", "SKSSKS", "SSSSSS", "SSSSSS", ".SSSS." },
        ["head.s"] = new[] { ".SSSS.", "SSSSSS", "SSSSKS", "SSSSSS", "SSSSSS", ".SSS.." },
        ["head.b"] = new[] { ".HHHH.", "HHHHHH", "HHHHHH", "HHHHHH", "HSSSSH", ".SSSS." },
        ["helm.f"] = new[] { ".TTTTT.", "TTTTTTT", "TTTTTTT", "T.....T" },
        ["helm.s"] = new[] { ".TTTTT.", "TTTTTTT", "TTTTTTT", "TT....." },
        ["helm.b"] = new[] { ".TTTTT.", "TTTTTTT", "TTTTTTT", "TTTTTTT" },
        ["hood.f"] = new[] { ".DDDDD.", "DDDDDDD", "DD...DD", "D.....D", "D.....D" },
        ["hood.s"] = new[] { ".DDDD..", "DDDDDDD", "DDDD...", "DDD....", "DDD...." },
        ["hood.b"] = new[] { ".DDDDD.", "DDDDDDD", "DDDDDDD", "DDDDDDD", "DDDDDDD" },
        ["plume"] = new[] { ".P", "PP", "PP", "PP" },
        ["torso.f"] = new[] { ".MMMMMM.", "MMMMMMMM", "MMMDDMMM", "MMMDDMMM", "MMMMMMMM", "MMMMMMMM", "WWWWTWWW", "MMMMMMMM", "MMMMMMMM" },
        ["torso.s"] = new[] { ".MMMM", "MMMMM", "MMMMM", "MMMMM", "MMMMM", "MMMMM", "WWWWW", "MMMMM", "MMMMM" },
        ["torso.b"] = new[] { ".MMMMMM.", "MMMMMMMM", "MMMDMMMM", "MMMDMMMM", "MMMDMMMM", "MMMMMMMM", "WWWWWWWW", "MMMMMMMM", "MMMMMMMM" },
        ["cape"] = new[] { "DDDDDDDD", "DDDDDDDD", "DDDDDDDD", "DDDDDDDD", "DDDDDDDD", "DDDDDDDD", "DDDDDDDD", "DDDDDDDD", "DDDDDDDD", "D.DD.DDD", "..D...D." },
        ["sleeve"] = new[] { "MMM", "MMM", "MMM", "MMM", "MMM", "MMM", "MMM", "MMM" },
        ["leg"] = new[] { "DDD", "DDD", "DDD", "DDD", "DDD", "DDD", "DDD", "DDD" },
        ["hand"] = new[] { "SS", "SS" },
        ["boot.f"] = new[] { "BBB", "BBB", "BBB" },
        ["boot.s"] = new[] { ".BB.", "BBB.", "BBBB" },
        ["boot.b"] = new[] { "BBB", "BBB", "BBB" },
        ["sword"] = new[] { ".T.", ".T.", ".T.", ".T.", ".T.", ".T.", ".T.", ".T.", ".T.", ".T.", "WWW", ".W.", ".W." },
        ["spear"] = new[] { ".T.", "TTT", ".T.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W." },
        ["bow"] = new[] { "CW..", "C.W.", "C.W.", "C..W", "C..W", "C..W", "C..W", "C..W", "C..W", "C..W", "C.W.", "C.W.", "CW.." },
        ["arrow"] = new[] { ".T.", "TTT", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", "W.W" },
        ["shield"] = new[] { ".DDDDDD.", "DMMMMMMD", "DMMMMMMD", "DMMTTMMD", "DMMTTMMD", "DMMMMMMD", "DMMMMMMD", ".DMMMMD.", "..DMMD..", "...DD..." },
        ["quiver"] = new[] { "T.T", "WWW", "WWW", "WWW", "WWW", "WWW", "WWW" },
        ["blood"] = new[] { ".RRRR.", "RRRRRR", ".RRR.." },
        ["lance"] = new[] { ".T.", ".T.", "TTT", ".TM", ".WM", ".WM", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.",
            ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", "TTT", ".W.", ".W.", ".W.", ".W.", ".W.", ".W." },

        // Horse parts. "Long" parts run left (rear joint) to right (front joint) with their top edge up;
        // axial legs and tail have their top row at the far (hoof / tip) end.
        ["hbody"] = new[]
        {
            "..NNNNNNNWWWWNNNNNNN..",
            ".NNNNNNNWWWWWWNNNNNNN.",
            "NNNNNNNNNMMMMNNNNNNNNN",
            "NNNNNNNNNMMMMNNNNNNNNN",
            "MMMMMMMMMMMMMMMMMMMMMM",
            "MDMDMDMDMDMDMDMDMDMDMD",
            "NNNNNNNNNNNNNNNNNNNNNN",
            ".NNNNNNNNNNNNNNNNNNNN.",
            "..NNNNN........NNNNN..",
        },
        ["hneck"] = new[] { "EEEEEEEEE", "NNNNNNNNN", "NNNNNNNNN", "NNNNNNNNN", ".NNNNNNN." },
        ["hhead"] = new[] { "EKNNNN..", "NNNKNNNN", "NNNNNNNN", ".NNNNNNK" },
        ["hchest"] = new[] { ".NNNNNN.", "NNNNNNNN", "NNNNNNNN", "MMMMMMMM", "MDMDMDMD", "NNNNNNNN", ".NNNNNN." },
        ["hrump"] = new[] { ".NNNNNN.", "NNNEENNN", "NNNEENNN", "MMMEEMMM", "MDMEEDMD", "NNNEENNN", ".NN..NN." },
        ["hface"] = new[] { "E..E", "NNNN", "KNNK", "NNNN", "NNNN", ".NN." },
        ["hleg"] = new[] { "KK", "NN", "NN", "NN", "NN", "NN", "NN", "NN", "NN", "NN", "NN", "NN" },
        ["htail"] = new[] { "E.", "EE", "EE", "EE", "EE", "EE", "EE", "EE" },
    };

    const int AW = 512, AH = 256;
    const int FW = 16, FH = 24; // prop painting area
    readonly Color[] px = new Color[AW * AH];
    readonly Dictionary<string, Rectangle> rects = new();
    int packX, packY, rowH, ox, oy;

    public Art(GraphicsDevice gd)
    {
        for (int p = 0; p < Palettes.Length; p++)
            foreach (var (name, rows) in Sprites)
            {
                var r = Alloc(rows.Max(row => row.Length) + 2, rows.Length + 2); // +2: outline border
                for (int y = 0; y < rows.Length; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                        px[(r.Y + 1 + y) * AW + r.X + 1 + x] = Map(rows[y][x], Palettes[p]);
                Outline(r);
                rects[$"{name}/{p}"] = r;
            }
        for (int k = 0; k < 4; k++)
        {
            var r = Alloc(FW + 2, FH + 2);
            ox = r.X + 1;
            oy = r.Y + 1;
            Prop(k);
            Outline(r);
            rects[$"prop{k}"] = r;
        }
        Atlas = new Texture2D(gd, AW, AH);
        Atlas.SetData(px);

        var shadow = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new(x + 0.5f, y + 0.5f), new(16, 16)) / 16;
                shadow[y * 32 + x] = new Color(0, 0, 0, (int)(110 * Math.Clamp(1 - d * d, 0, 1))); // premultiplied
            }
        Shadow = new Texture2D(gd, 32, 32);
        Shadow.SetData(shadow);

        var rng = new Random(11);
        var grass = new Color[64 * 64];
        for (int i = 0; i < grass.Length; i++)
        {
            int n = rng.Next(-8, 9), blade = rng.Next(12) == 0 ? -18 : 0;
            grass[i] = new Color(74 + n + blade, 122 + n + blade, 58 + n / 2);
        }
        Grass = Mipped(gd, grass, 64);

        Pixel = new Texture2D(gd, 1, 1);
        Pixel.SetData(new[] { Color.White });
    }

    // Shelf packer.
    Rectangle Alloc(int w, int h)
    {
        if (packX + w > AW) { packX = 0; packY += rowH; rowH = 0; }
        if (packY + h > AH) throw new InvalidOperationException("sprite atlas full");
        var r = new Rectangle(packX, packY, w, h);
        packX += w;
        rowH = Math.Max(rowH, h);
        return r;
    }

    // Dark 1px outline around every opaque shape, kept inside its own rectangle.
    void Outline(Rectangle r)
    {
        var add = new List<int>();
        for (int y = r.Top; y < r.Bottom; y++)
            for (int x = r.Left; x < r.Right; x++)
            {
                int i = y * AW + x;
                if (px[i].A != 0) continue;
                bool edge = (x > r.Left && px[i - 1].A != 0) || (x < r.Right - 1 && px[i + 1].A != 0)
                         || (y > r.Top && px[i - AW].A != 0) || (y < r.Bottom - 1 && px[i + AW].A != 0);
                if (edge) add.Add(i);
            }
        foreach (int i in add) px[i] = Ink;
    }

    static Texture2D Mipped(GraphicsDevice gd, Color[] data, int size)
    {
        var t = new Texture2D(gd, size, size, true, SurfaceFormat.Color);
        for (int level = 0; ; level++)
        {
            t.SetData(level, null, data, 0, data.Length);
            if (size == 1) return t;
            int h = size / 2;
            var next = new Color[h * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < h; x++)
                {
                    var a = data[2 * y * size + 2 * x].ToVector4() + data[2 * y * size + 2 * x + 1].ToVector4()
                          + data[(2 * y + 1) * size + 2 * x].ToVector4() + data[(2 * y + 1) * size + 2 * x + 1].ToVector4();
                    next[y * h + x] = new Color(a / 4);
                }
            data = next;
            size = h;
        }
    }

    // ---------- hand-painted props ----------

    void R(int x, int y, int w, int h, Color c)
    {
        for (int j = Math.Max(0, y); j < Math.Min(FH, y + h); j++)
            for (int i = Math.Max(0, x); i < Math.Min(FW, x + w); i++) px[(oy + j) * AW + ox + i] = c;
    }

    void Disc(int cx, int cy, int rx, int ry, Color c)
    {
        for (int y = -ry; y <= ry; y++)
            for (int x = -rx; x <= rx; x++)
                if (x * x * ry * ry + y * y * rx * rx <= rx * rx * ry * ry) R(cx + x, cy + y, 1, 1, c);
    }

    void Prop(int k)
    {
        Color leaf = new(56, 122, 52), leafHi = new(84, 150, 70), pine = new(34, 92, 56), stone = new(122, 122, 128);
        switch (k)
        {
            case 0: R(7, 13, 2, 11, Wood); Disc(8, 8, 7, 6, leaf); Disc(7, 6, 4, 3, leafHi); break;
            case 1:
                R(7, 18, 2, 6, Wood);
                for (int y = 1; y < 19; y++) { int w = 1 + y * 7 / 18; R(8 - w, y, w * 2, 1, pine); }
                break;
            case 2: Disc(8, 19, 7, 4, leaf); Disc(7, 18, 4, 2, leafHi); break;
            default: Disc(8, 20, 6, 3, stone); Disc(7, 19, 3, 2, new Color(160, 160, 166)); break;
        }
    }
}

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
        foreach (char ch in s.ToUpperInvariant())
        {
            int i = Chars.IndexOf(ch);
            if (i < 0) i = Chars.IndexOf(' ');
            sb.Draw(tex, pos, new Rectangle(i * 6, 0, 6, 8), c, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            pos.X += 6 * scale;
        }
    }
}
