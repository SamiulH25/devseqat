using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// What the mouse is about to do, which the game's own cursor shows (see ScreenHost.DrawCursor). A screen sets `Cursors.Want`
/// each frame it knows better than the plain pointer; the host resets it after drawing.
public enum CursorKind
{
    /// A steel blade: the resting cursor.
    Pointer,
    /// A gilded gauntlet: over a button, card or anything that clicks.
    Hand,
    /// Crossed swords: a click attacks the company or party under it.
    Attack,
    /// A pennant: a click sends the selected companies (or your party) there.
    Move,
    /// A shield: one of your companies, click to select it.
    Select,
    /// A gold sight: engine crews take the spot or company you click as their mark.
    Aim,
    /// A violet sigil: a spell is chosen and the click is its target.
    Cast,
    /// Corner brackets: dragging a box to select companies.
    Box,
    /// A double arrow: the camera is being turned.
    Turn,
    /// An hourglass: loading.
    Wait,
}

/// The cursors, painted in code like the rest of the art: each shape is a few rows of fill (the letters are in `Colours`),
/// the iron outline is added round it, and the hot spot is the pixel that clicks. Headless (colours only), so `--cursors`
/// can write a sheet of them.
public static class Cursors
{
    public static CursorKind Want = CursorKind.Pointer;

    public sealed record Shape(Color[] Pixels, int W, int H, Point Hot);

    static readonly Dictionary<char, Color> Colours = new()
    {
        ['X'] = new(22, 18, 20),
        ['o'] = new(238, 242, 248), ['s'] = new(168, 178, 194), ['S'] = new(104, 112, 132),
        ['g'] = new(255, 232, 150), ['G'] = new(222, 172, 62), ['D'] = new(150, 98, 34),
        ['r'] = new(226, 84, 64), ['R'] = new(140, 40, 38),
        ['w'] = new(244, 236, 214), ['B'] = new(140, 98, 58), ['b'] = new(88, 58, 36),
        ['a'] = new(70, 124, 214), ['A'] = new(42, 78, 156),
        ['p'] = new(206, 164, 255), ['P'] = new(112, 76, 190),
    };

    static readonly Dictionary<CursorKind, Shape> cache = new();
    public static Shape Of(CursorKind k) => cache.TryGetValue(k, out var s) ? s : cache[k] = Build(k);

    sealed class Grid
    {
        public readonly char[,] C;
        public readonly int W, H;
        public Grid(int w, int h) { W = w; H = h; C = new char[w, h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) C[x, y] = '.'; }
        public void Set(int x, int y, char c) { if (x >= 0 && y >= 0 && x < W && y < H) C[x, y] = c; }
        public char At(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? C[x, y] : '.';
        public void Rows(params string[] rows) { for (int y = 0; y < rows.Length; y++) for (int x = 0; x < rows[y].Length; x++) if (rows[y][x] != '.') Set(x, y, rows[y][x]); }
        /// A blade of two columns down the diagonal that runs by (dx, 1): lit edge, shaded edge.
        public void Blade(int x0, int y0, int dx, int n, char lit = 'o', char shade = 's') { for (int i = 0; i < n; i++) { Set(x0 + dx * i, y0 + i, lit); Set(x0 + dx * i - dx, y0 + i, shade); } }
    }

    static Shape Finish(Grid g, int hx, int hy)
    {
        int w = g.W + 2, h = g.H + 2;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = g.At(x - 1, y - 1);
                if (c == '.')
                {
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++) for (int dx = -1; dx <= 1; dx++) if (g.At(x - 1 + dx, y - 1 + dy) != '.') { edge = true; break; }
                    c = edge ? 'X' : '.';
                }
                px[y * w + x] = c == '.' ? Color.Transparent : Colours[c];
            }
        return new Shape(px, w, h, new Point(hx + 1, hy + 1));
    }

    static Shape Build(CursorKind k)
    {
        switch (k)
        {
            case CursorKind.Pointer: // a sword, point at the click, hilt down and to the right
            {
                var g = new Grid(17, 17);
                for (int i = 0; i <= 9; i++) { g.Set(i, i, 'o'); if (i > 0) g.Set(i - 1, i, 's'); else g.Set(1, 0, 's'); }
                for (int j = -3; j <= 3; j++) { g.Set(10 + j, 10 - j, 'g'); g.Set(11 + j, 10 - j, 'G'); }
                g.Set(7, 13, 'D'); g.Set(14, 7, 'D');
                for (int i = 11; i <= 13; i++) { g.Set(i, i, 'B'); g.Set(i + 1, i, 'b'); }
                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) g.Set(14 + dx, 14 + dy, dx == 0 && dy == 0 ? 'g' : 'G');
                return Finish(g, 0, 0);
            }
            case CursorKind.Hand:
            {
                var g = new Grid(14, 16);
                g.Rows("...gG.........", "..gGGD........", "..gGGD........", "..gGGD........", "..gGGDgGgG....", "..gGGDGGGGDg..", "gGgGGGGGGGGDG.",
                    "gGGGGGGGGGGGD.", ".gGGGGGGGGGGD.", "..gGGGGGGGGDD.", "..DGGGGGGGGD..", "...DGGGGGGD...", "...DGGGGGDD...", "....DDDDDD....", "....sssssS....", "....oosssS....");
                return Finish(g, 4, 0);
            }
            case CursorKind.Attack: // crossed swords, the guards red
            {
                var g = new Grid(17, 17);
                for (int i = 0; i <= 9; i++) { g.Set(i, i, 'o'); g.Set(i + 1, i, 's'); g.Set(16 - i, i, 'o'); g.Set(15 - i, i, 's'); }
                for (int j = -2; j <= 2; j++) { g.Set(11 + j, 11 - j, 'r'); g.Set(12 + j, 11 - j, 'R'); g.Set(5 - j, 11 - j, 'r'); g.Set(4 - j, 11 - j, 'R'); }
                for (int i = 12; i <= 14; i++) { g.Set(i, i, 'B'); g.Set(i + 1, i, 'b'); g.Set(16 - i, i, 'B'); g.Set(17 - i, i, 'b'); }
                g.Set(15, 15, 'g'); g.Set(1, 15, 'g');
                return Finish(g, 8, 7);
            }
            case CursorKind.Move: // a pennant on a pole, the foot of the pole is the point
            {
                var g = new Grid(14, 19);
                for (int y = 1; y < 19; y++) { g.Set(1, y, 'B'); g.Set(2, y, 'b'); }
                g.Set(1, 0, 'g'); g.Set(2, 0, 'G'); g.Set(1, 18, 'D'); g.Set(2, 18, 'D');
                for (int y = 1; y <= 9; y++)
                    for (int x = 3; x <= 13; x++)
                    {
                        if (Math.Abs(y - 5) < 4 && x >= 9 + Math.Abs(y - 5)) continue; // the swallow-tail
                        g.Set(x, y, y == 1 || y == 9 ? 'g' : y < 5 ? 'a' : 'A');
                    }
                return Finish(g, 1, 18);
            }
            case CursorKind.Select: // a heater shield
            {
                var g = new Grid(11, 13);
                bool In(int x, int y) { int hw = y <= 6 ? 5 : 5 - (int)MathF.Round((y - 6) * 5f / 7f); return Math.Abs(x - 5) <= hw && y < 13; }
                for (int y = 0; y < 13; y++)
                    for (int x = 0; x < 11; x++)
                        if (In(x, y)) g.Set(x, y, !In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1) ? (y == 0 ? 'g' : 'G') : y == 4 || y == 5 ? 'g' : x < 5 ? 'a' : 'A');
                return Finish(g, 5, 6);
            }
            case CursorKind.Aim: return Reticle('G', 'g', 'r', 'D');
            case CursorKind.Cast: return Reticle('P', 'p', 'g', 'P');
            case CursorKind.Box:
            {
                var g = new Grid(17, 17);
                for (int i = 0; i < 5; i++)
                    foreach (var (x, y) in new[] { (i, 0), (0, i), (16 - i, 0), (16, i), (i, 16), (0, 16 - i), (16 - i, 16), (16, 16 - i) }) g.Set(x, y, i == 0 ? 'g' : 'G');
                for (int i = -2; i <= 2; i++) { g.Set(8 + i, 8, 'o'); g.Set(8, 8 + i, 'o'); }
                return Finish(g, 8, 8);
            }
            case CursorKind.Turn:
            {
                var g = new Grid(19, 9);
                for (int x = 4; x <= 14; x++) { g.Set(x, 3, 'g'); g.Set(x, 4, 'G'); g.Set(x, 5, 'D'); }
                for (int i = 0; i < 4; i++) for (int y = 4 - i; y <= 4 + i; y++) { g.Set(i, y, i == 0 ? 'g' : 'G'); g.Set(18 - i, y, i == 0 ? 'g' : 'G'); }
                return Finish(g, 9, 4);
            }
            default: // Wait: an hourglass
            {
                var g = new Grid(9, 14);
                for (int x = 0; x < 9; x++) { g.Set(x, 0, 'g'); g.Set(x, 13, 'G'); }
                for (int y = 1; y <= 12; y++)
                {
                    int hw = y <= 6 ? 3 - (y - 1) * 3 / 5 : (y - 7) * 3 / 5;
                    if (y == 7) hw = 0;
                    for (int x = 4 - hw; x <= 4 + hw; x++) g.Set(x, y, y is >= 3 and <= 5 or >= 9 ? 'G' : 'w');
                }
                g.Set(4, 6, 'G'); g.Set(4, 7, 'G');
                return Finish(g, 4, 7);
            }
        }
    }

    /// A ring with four ticks and a centre mark, in two colours.
    static Shape Reticle(char ring, char lit, char dot, char shade)
    {
        var g = new Grid(19, 19);
        for (int y = 0; y < 19; y++)
            for (int x = 0; x < 19; x++)
            {
                float d = MathF.Sqrt((x - 9) * (x - 9) + (y - 9) * (y - 9));
                if (d is >= 5.2f and <= 6.6f) g.Set(x, y, x + y < 18 ? lit : ring);
            }
        for (int i = 0; i < 4; i++)
            foreach (var (x, y) in new[] { (9, i), (9, 18 - i), (i, 9), (18 - i, 9) }) g.Set(x, y, i == 0 ? lit : shade == 'D' ? ring : shade);
        g.Set(9, 9, dot); g.Set(8, 9, dot); g.Set(10, 9, dot); g.Set(9, 8, dot); g.Set(9, 10, dot);
        return Finish(g, 9, 9);
    }

    /// `--cursors out.png`: every cursor on a dark board and a light one, six times up, for judging them.
    public static void Dump(string path)
    {
        var kinds = Enum.GetValues<CursorKind>();
        const int S = 6, Cell = 28;
        int w = kinds.Length * Cell * S, h = Cell * S * 2;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = y < h / 2 ? new Color(52, 34, 70) : new Color(150, 140, 112);
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < kinds.Length; i++)
            {
                var s = Of(kinds[i]);
                int ox = i * Cell * S + (Cell * S - s.W * S) / 2, oy = row * Cell * S + (Cell * S - s.H * S) / 2;
                for (int y = 0; y < s.H * S; y++) for (int x = 0; x < s.W * S; x++) { var c = s.Pixels[(y / S) * s.W + x / S]; if (c.A > 0) px[(oy + y) * w + ox + x] = c; }
                int hx = ox + s.Hot.X * S + S / 2, hy = oy + s.Hot.Y * S + S / 2; // the hot spot, a red dot
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) px[(hy + dy) * w + hx + dx] = new Color(255, 0, 80);
            }
        File.WriteAllBytes(path, Png.Encode(px, w, h));
        Console.WriteLine("cursors -> " + path);
    }

    static readonly Dictionary<CursorKind, Texture2D> textures = new();
    public static Texture2D Texture(GraphicsDevice gd, CursorKind k)
    {
        if (textures.TryGetValue(k, out var t)) return t;
        var s = Of(k);
        t = new Texture2D(gd, s.W, s.H);
        t.SetData(s.Pixels);
        return textures[k] = t;
    }
}
