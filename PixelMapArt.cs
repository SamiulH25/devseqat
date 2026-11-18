using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class PixelMap
{
    /// What builds a place: walls, houses and roofs by the country it stands in and the people who live there.
    sealed record Build(Color Stone, Color House, Color Roof, Color Roof2, bool Flat, bool Logs, bool Tents);

    /// The world's things as pixel sprites, at one fixed size (a tile is 12 pixels): trees one tile across, a house two,
    /// a castle six by five, a town eight by six. Each is drawn pixel by pixel from its ramps, lit from the upper left,
    /// and outlined; sprites are cached by what they show.
    static class Art
    {
        // ---------- trees ----------

        /// A broadleaf tree in one tile: a crown of three lobes lit from the upper left, in clumps, on a short trunk.
        /// kind: 0 broadleaf, 1 birch, 2 olive (hot lands).
        public static Sprite Tree(int seed, int season, int kind) => Cached($"tree{seed & 3}.{season}.{kind}", () =>
        {
            var leaf = Tones(kind switch { 1 => new Color(112, 150, 66), 2 => new Color(104, 120, 64), _ => new Color(62, 110, 50) });
            if (season == 2 && kind != 2) leaf = Tones(seed % 2 == 0 ? new Color(178, 110, 44) : new Color(150, 124, 50));
            var bark = kind == 1 ? Tones(214, 208, 192) : Tones(96, 70, 48);
            var cv = new Pix(16, 18);
            int sx = (seed & 1) == 0 ? 0 : 1; // a lean of a pixel
            cv.VLine(7, 11, 15, bark[2]); cv.VLine(8, 11, 15, bark[1]);
            if (kind == 1) { cv.Set(7, 13, bark[0]); }
            Func<int, int, Color> lit = (x, y) =>
            {
                float dx = x + 0.5f - 8 - sx, dy = y + 0.5f - 6.5f;
                float v = -(dx * 0.55f + dy * 0.8f) / 5.2f;
                int tone = (int)MathF.Round(2 + v * 1.5f);
                uint h = Hash(x / 2 + seed * 7, y / 2 + (x & 1));
                if (h % 5 == 0) tone--; else if (h % 7 == 0) tone++;
                if (y >= 10) tone--;
                return leaf[Math.Clamp(tone, 0, 4)];
            };
            cv.Oval(8 + sx, 6.5f, 5.4f, 5, lit);
            cv.Oval(4.8f + sx, 9, 3.4f, 2.8f, lit);
            cv.Oval(11.2f + sx, 9, 3.4f, 2.8f, lit);
            return cv.Outline().Done(8, 15);
        });

        /// A pine in one tile: three tiers lit on the left, snow on the tips in cold country.
        public static Sprite Pine(int seed, bool cold) => Cached($"pine{seed & 3}.{cold}", () =>
        {
            var leaf = Tones(seed % 2 == 0 ? new Color(44, 86, 62) : new Color(52, 92, 58));
            var cv = new Pix(13, 20);
            cv.VLine(6, 15, 17, new Color(90, 64, 44));
            for (int y = 1; y <= 15; y++)
            {
                int tier = (y - 1) / 5, r = (y - 1) % 5;
                int half = 1 + r + tier;
                if (y <= 2) half = y - 1;
                for (int x = 6 - half; x <= 6 + half; x++)
                {
                    int tone = x < 6 ? 3 : x == 6 ? 2 : 1;
                    if (r == 4) tone--;
                    if (Hash(x + seed, y) % 6 == 0) tone++;
                    cv.Set(x, y, leaf[Math.Clamp(tone, 0, 4)]);
                }
                if (cold && r == 0 && y > 2) { cv.Set(6 - half + 1, y, new Color(232, 236, 240)); cv.Set(6 - half + 2, y, new Color(214, 222, 232)); }
            }
            return cv.Outline().Done(6, 17);
        });

        public static Sprite Bush(int seed, int season) => Cached($"bush{seed & 3}.{season}", () =>
        {
            var leaf = Tones(season == 2 ? new Color(150, 110, 52) : new Color(72, 112, 54));
            var cv = new Pix(11, 8);
            cv.Oval(5.5f, 4, 4.4f, 3.2f, (x, y) => leaf[Math.Clamp(2 + (x < 5 ? 1 : 0) - (y >= 5 ? 1 : 0) + (Hash(x + seed, y) % 5 == 0 ? 1 : 0), 0, 4)]);
            return cv.Outline().Done(5, 6);
        });

        // ---------- houses ----------

        /// One house, w pixels wide: a roof seen from above and the front (tile, thatch or shingle by the build; flat for
        /// the south), then the wall with its door and windows. great: the hall or temple at a town's heart.
        static Pix House(Build b, int w, int seed, bool great = false, bool thatch = false)
        {
            int roofH = great ? 12 : 9, wallH = great ? 10 : 7, top = 4;
            var cv = new Pix(w + 2, top + roofH + wallH + 2);
            var wall = Tones(b.Logs ? b.Stone : b.House, 0.9f);
            var roof = Tones(thatch ? b.Roof2 : b.Roof, 1.1f);
            int x0 = 1, x1 = w, wy0 = top + roofH, wy1 = wy0 + wallH - 1;
            // The wall.
            for (int y = wy0; y <= wy1; y++)
                for (int x = x0 + 1; x <= x1 - 1; x++)
                {
                    int tone = x < x0 + 4 ? 3 : x >= x1 - 2 ? 1 : 2;
                    if (y == wy0) tone = 0; // the eave's shadow
                    if (b.Logs && (y - wy0) % 2 == 0 && y != wy0) tone = Math.Max(0, tone - 1);
                    cv.Set(x, y, wall[tone]);
                }
            if (!b.Logs && !b.Flat && !great) // timber framing
            {
                var beam = Tones(92, 66, 46)[1];
                cv.VLine(x0 + 1, wy0, wy1, beam); cv.VLine(x1 - 1, wy0, wy1, beam);
                cv.HLine(x0 + 1, x1 - 1, wy0 + 3, beam);
            }
            int door = (x0 + x1) / 2 + (int)(seed % 3) - 1;
            var dark = new Color(46, 34, 30);
            if (great) { cv.Rect(door - 2, wy1 - 5, door + 2, wy1, dark); cv.HLine(door - 1, door + 1, wy1 - 6, dark); }
            else cv.Rect(door - 1, wy1 - 3, door + 1, wy1, b.Flat ? dark : Tones(110, 76, 48)[1]);
            foreach (int wx in new[] { x0 + 3, x1 - 4 })
                if (Math.Abs(wx - door) > 2)
                {
                    cv.Rect(wx, wy0 + 2, wx + 1, wy0 + 3, dark);
                    if (!b.Flat) cv.HLine(wx, wx + 1, wy0 + 4, wall[4]);
                }
            // The roof.
            if (b.Flat)
            {
                for (int y = wy0 - 4; y < wy0; y++)
                    for (int x = x0; x <= x1; x++) cv.Set(x, y, wall[y == wy0 - 1 ? 1 : y == wy0 - 4 ? 4 : 3]);
                cv.HLine(x0, x1, wy0, wall[0]);
                if (great) cv.Oval((x0 + x1) / 2f + 0.5f, wy0 - 5, 5, 5, (x, y) => wall[x < (x0 + x1) / 2 ? 4 : 2]); // the dome
                return cv;
            }
            for (int r = 0; r < roofH; r++)
            {
                int y = top + r, inset = Math.Max(0, 2 - r);
                for (int x = x0 + inset; x <= x1 - inset; x++)
                {
                    int tone = x < (x0 + x1) / 2 ? 3 : 2;
                    if (r == 0) tone = 4;
                    else if (thatch) { if (Hash(x * 3 + seed, y / 2) % 4 == 0) tone--; }
                    else if (r % 3 == 0) tone--; // a course of tiles or shingles
                    if (r == roofH - 1) tone = 1; // the eave
                    cv.Set(x, y, roof[Math.Clamp(tone, 0, 4)]);
                }
            }
            if (!great) { var st = Tones(b.Stone); cv.Rect(x1 - 5, top - 3, x1 - 4, top, st[2]); cv.Set(x1 - 5, top - 3, st[3]); } // the chimney
            return cv;
        }

        /// A village house in a 2x2 tile plot, or a yurt on the steppe.
        public static Sprite Cottage(Build b, int seed) => Cached($"cot{b.GetHashCode()}.{seed % 4}", () =>
        {
            if (b.Tents) return Tent(b, seed).Outline().Done(10, 14);
            var cv = House(b, 16 + (int)(seed % 3) * 2, seed, thatch: !b.Flat && !b.Logs && seed % 2 == 0);
            return cv.Outline().Done(cv.W / 2, cv.H - 2);
        });

        static Pix Tent(Build b, int seed)
        {
            var felt = Tones(226, 216, 190, 0.9f);
            var band = Tones(b.Roof);
            var cv = new Pix(21, 16);
            cv.Oval(10.5f, 10, 8.5f, 8, (x, y) => felt[x < 8 ? 3 : x > 14 ? 1 : 2]);
            for (int x = 0; x < 21; x++) for (int y = 12; y < 16; y++) if (y > 14) cv.Clear(x, y);
            for (int x = 2; x < 20; x++) if (cv.Has(x, 8)) cv.Set(x, 8, band[2]);
            for (int x = 2; x < 20; x++) if (cv.Has(x, 9)) cv.Set(x, 9, band[1]);
            cv.Rect(9, 3, 12, 3, band[3]);
            cv.Rect(9, 11, 11, 14, new Color(52, 40, 34));
            return cv;
        }

        // ---------- walls and towers ----------

        /// A run of wall seen from the south: merlons, the walk, then the face with courses. Logs: a palisade of stakes.
        static void WallFace(Pix cv, Build b, int xa, int xb, int top, int faceH)
        {
            var s = Tones(b.Stone);
            for (int x = xa; x <= xb; x++)
            {
                if (b.Logs)
                {
                    int stake = x % 3;
                    if (stake != 2) cv.Set(x, top + 1, s[3]);
                    for (int y = top + 2; y <= top + 2 + faceH; y++) cv.Set(x, y, s[stake == 0 ? 3 : stake == 1 ? 2 : 1]);
                    continue;
                }
                if (x % 3 != 2) cv.Set(x, top, s[4]);
                cv.Set(x, top + 1, s[3]); cv.Set(x, top + 2, s[3]);
                for (int y = top + 3; y <= top + 2 + faceH; y++)
                {
                    int r = y - top - 3, tone = 2;
                    if (r % 3 == 2) tone = 1;
                    else if ((x + (r / 3) * 2) % 5 == 0) tone = 1;
                    if (y == top + 2 + faceH) tone = 1;
                    cv.Set(x, y, s[tone]);
                }
            }
        }

        /// A wall running north-south seen from above: its walk lit, its inner side dark.
        static void WallSide(Pix cv, Build b, int x, int ya, int yb)
        {
            var s = Tones(b.Stone);
            for (int y = ya; y <= yb; y++)
            {
                cv.Set(x, y, b.Logs ? s[3] : y % 3 == 0 ? s[4] : s[3]);
                cv.Set(x + 1, y, s[3]); cv.Set(x + 2, y, s[3]);
                cv.Set(x + 3, y, s[1]); cv.Set(x + 4, y, s[0]);
            }
        }

        /// A tower standing on baseY, centred on cx: round with a cone (stone), square and flat-topped (the south), or a
        /// timber tower with a pitched cap (logs).
        static void Tower(Pix cv, Build b, int cx, int baseY, int r, int h)
        {
            var s = Tones(b.Stone);
            var roof = Tones(b.Roof);
            int topY = baseY - h;
            for (int y = topY; y <= baseY; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    float a = (x - (cx - r)) / (2f * r);
                    int tone = a < 0.3f ? 3 : a < 0.75f ? 2 : 1;
                    if (b.Logs && (y - topY) % 3 == 0) tone--;
                    else if (!b.Logs && (y - topY) % 4 == 3 && a > 0.2f && a < 0.8f) tone--;
                    cv.Set(x, y, s[Math.Max(0, tone)]);
                }
            cv.Rect(cx, topY + 5, cx, topY + 7, new Color(40, 30, 30)); // an arrow slit
            if (b.Flat)
            {
                cv.HLine(cx - r - 1, cx + r + 1, topY - 1, s[4]); cv.HLine(cx - r - 1, cx + r + 1, topY, s[2]);
                for (int x = cx - r - 1; x <= cx + r + 1; x += 2) cv.Set(x, topY - 2, s[4]);
                return;
            }
            if (b.Logs)
            {
                for (int i = 0; i < 5; i++) cv.HLine(cx - r - 1 + i, cx + r + 1 - i, topY - 1 - i, roof[i == 0 ? 1 : (cx - r + i) < cx ? 3 : 2]);
                return;
            }
            cv.HLine(cx - r - 1, cx + r + 1, topY, s[4]);
            for (int i = 0; i <= r + 4; i++) // the cone
            {
                int half = (int)MathF.Round((r + 1) * (1 - i / (float)(r + 5)));
                for (int x = cx - half; x <= cx + half; x++) cv.Set(x, topY - 1 - i, roof[x < cx ? 3 : x == cx ? 2 : 1]);
            }
        }

        static void Flag(Pix cv, int x, int y, Heraldry side, int poleH = 7)
        {
            cv.VLine(x, y, y + poleH, new Color(60, 46, 38));
            var cl = Tones(side.Cloth);
            cv.Rect(x + 1, y, x + 6, y + 3, cl[2]);
            cv.HLine(x + 1, x + 6, y, cl[3]);
            cv.Clear(x + 6, y + 1); cv.Clear(x + 6, y + 2); // the swallow tail
            cv.Set(x + 3, y + 1, side.Trim); cv.Set(x + 3, y + 2, side.Trim); cv.Set(x + 4, y + 2, side.Trim);
        }

        // ---------- places ----------

        /// A walled town, eight tiles by six: back wall, two rows of houses with the great house in the middle, the
        /// front wall and its gatehouse. Foot at the middle of the footprint's south edge.
        public static Sprite Town(Build b, Heraldry side, int seed) => Cached($"town{b.GetHashCode()}.{side.Name}.{seed % 5}", () =>
        {
            int fw = 96, fh = 72, extra = 30;
            var cv = new Pix(fw + 4, fh + extra + 2);
            int fx0 = 2, fx1 = fx0 + fw - 1, fy0 = extra, fy1 = fy0 + fh - 1, cx = (fx0 + fx1) / 2;
            var yard = Tones(b.Flat ? new Color(196, 170, 124) : new Color(146, 128, 98));
            for (int y = fy0 + 6; y <= fy1 - 4; y++)
                for (int x = fx0 + 4; x <= fx1 - 4; x++)
                {
                    uint h = Hash(x / 2, y / 2);
                    cv.Set(x, y, yard[h % 5 == 0 ? 1 : h % 7 == 0 ? 3 : 2]);
                }
            WallFace(cv, b, fx0 + 4, fx1 - 4, fy0 + 2, 8);
            WallSide(cv, b, fx0 + 1, fy0 + 4, fy1 - 12);
            WallSide(cv, b, fx1 - 5, fy0 + 4, fy1 - 12);
            Tower(cv, b, fx0 + 5, fy0 + 12, 5, 14);
            Tower(cv, b, fx1 - 5, fy0 + 12, 5, 14);
            // Two rows of houses, back to front; the great house in the middle of the back row.
            int[] rows = { fy0 + 36, fy0 + 56 };
            for (int row = 0; row < 2; row++)
            {
                int x = fx0 + 9;
                int n = 0;
                while (x < fx1 - 12)
                {
                    bool great = row == 0 && x > cx - 16 && x < cx;
                    int w = great ? 22 : 14 + (int)(Hash(x, row + seed) % 3) * 2;
                    if (x + w > fx1 - 8) break;
                    if (b.Tents && !great)
                    {
                        var tent = Tent(b, n + seed);
                        cv.Put(tent.Outline(), x - 1, rows[row] - tent.H + 2);
                        x += 22; n++;
                        continue;
                    }
                    var house = House(b, w, (int)Hash(x, row * 13 + seed), great, thatch: !b.Flat && !b.Logs && Hash(x, row + 5) % 3 == 0).Outline();
                    cv.Put(house, x - 1, rows[row] - house.H + 2);
                    if (great) Flag(cv, x + w / 2, rows[row] - house.H - 7, side, 8);
                    x += w + 2; n++;
                }
            }
            WallFace(cv, b, fx0 + 4, fx1 - 4, fy1 - 14, 11);
            Tower(cv, b, fx0 + 5, fy1, 5, 16);
            Tower(cv, b, fx1 - 5, fy1, 5, 16);
            // The gatehouse: two square towers and the arch between them.
            Tower(cv, b with { Flat = true }, cx - 9, fy1, 3, 20);
            Tower(cv, b with { Flat = true }, cx + 9, fy1, 3, 20);
            var dark = new Color(36, 28, 28);
            cv.Rect(cx - 5, fy1 - 8, cx + 5, fy1, dark);
            cv.HLine(cx - 4, cx + 4, fy1 - 9, dark); cv.HLine(cx - 2, cx + 2, fy1 - 10, dark);
            cv.Rect(cx - 4, fy1 - 6, cx - 1, fy1, Tones(110, 76, 48)[1]);
            return cv.Outline().Done(cx, fy1);
        });

        /// A castle, six tiles by five: a ring of wall with corner towers round a tall keep flying the lord's banner.
        public static Sprite Castle(Build b, Heraldry side, int seed) => Cached($"castle{b.GetHashCode()}.{side.Name}", () =>
        {
            int fw = 72, fh = 60, extra = 44;
            var cv = new Pix(fw + 4, fh + extra + 2);
            int fx0 = 2, fx1 = fx0 + fw - 1, fy0 = extra, fy1 = fy0 + fh - 1, cx = (fx0 + fx1) / 2;
            var yard = Tones(126, 116, 98);
            for (int y = fy0 + 6; y <= fy1 - 4; y++)
                for (int x = fx0 + 4; x <= fx1 - 4; x++) cv.Set(x, y, yard[Hash(x / 2, y / 2) % 5 == 0 ? 1 : 2]);
            WallFace(cv, b, fx0 + 4, fx1 - 4, fy0 + 2, 8);
            WallSide(cv, b, fx0 + 1, fy0 + 4, fy1 - 12);
            WallSide(cv, b, fx1 - 5, fy0 + 4, fy1 - 12);
            Tower(cv, b, fx0 + 5, fy0 + 12, 5, 14);
            Tower(cv, b, fx1 - 5, fy0 + 12, 5, 14);
            // The keep.
            var s = Tones(b.Stone);
            int kx0 = cx - 13, kx1 = cx + 13, kb = fy0 + 44, kt = kb - 52;
            for (int y = kt; y <= kb; y++)
                for (int x = kx0; x <= kx1; x++)
                {
                    int tone = x < kx0 + 8 ? 3 : x > kx1 - 6 ? 1 : 2;
                    if ((y - kt) % 4 == 3 && (x + (y - kt) / 4 * 3) % 6 != 0) tone = Math.Max(0, tone - 1);
                    if (y > kb - 2) tone = 1;
                    cv.Set(x, y, s[tone]);
                }
            for (int x = kx0 - 1; x <= kx1 + 1; x++) { cv.Set(x, kt, s[4]); if (x % 3 != 1) cv.Set(x, kt - 1, s[4]); }
            var dark = new Color(36, 28, 28);
            foreach (int wy in new[] { kt + 8, kt + 22 })
                foreach (int wx in new[] { kx0 + 6, cx, kx1 - 6 }) cv.Rect(wx, wy, wx, wy + 2, dark);
            cv.Rect(cx - 2, kb - 6, cx + 2, kb - 1, dark);
            Flag(cv, cx, kt - 14, side, 12);
            WallFace(cv, b, fx0 + 4, fx1 - 4, fy1 - 14, 11);
            Tower(cv, b, fx0 + 5, fy1, 5, 16);
            Tower(cv, b, fx1 - 5, fy1, 5, 16);
            cv.Rect(cx - 4, fy1 - 8, cx + 4, fy1, dark);
            cv.HLine(cx - 3, cx + 3, fy1 - 9, dark);
            cv.Rect(cx - 3, fy1 - 6, cx, fy1, Tones(110, 76, 48)[1]);
            return cv.Outline().Done(cx, fy1);
        });

        // ---------- sites ----------

        /// A site on the map in a 2x2 plot: ruin, shrine, standing stones, mill, watchtower, mine, spring or hut.
        public static Sprite Site(FoliageKind kind, int seed) => Cached($"site{kind}.{seed & 1}", () =>
        {
            var stone = Tones(140, 136, 126);
            var wood = Tones(118, 84, 54);
            var cv = new Pix(26, 30);
            int fx = 13, fy = 27;
            switch (kind)
            {
                case FoliageKind.Ruin:
                    foreach (var (x, h) in new[] { (4, 9), (8, 14), (12, 6), (17, 11), (21, 5) })
                        for (int y = fy - h; y <= fy; y++) for (int xx = x; xx < x + 4; xx++)
                            cv.Set(xx, y, stone[xx == x ? 3 : (y - fy + h) % 3 == 2 ? 1 : 2]);
                    break;
                case FoliageKind.Stones:
                    foreach (var (x, h) in new[] { (3, 8), (9, 11), (15, 9), (20, 7) })
                        for (int y = fy - h; y <= fy; y++) for (int xx = x; xx < x + 3; xx++) cv.Set(xx, y, stone[xx == x ? 3 : y == fy - h ? 4 : 2]);
                    break;
                case FoliageKind.Watch:
                    for (int y = fy - 24; y <= fy; y++) for (int x = 9; x <= 16; x++) cv.Set(x, y, wood[x < 11 ? 3 : x > 14 ? 1 : 2]);
                    for (int y = fy - 24; y <= fy; y += 4) cv.HLine(9, 16, y, wood[0]);
                    cv.Rect(7, fy - 27, 18, fy - 25, wood[3]);
                    break;
                case FoliageKind.Mill:
                {
                    var b = new Build(new(136, 132, 122), new(214, 200, 166), new(150, 108, 60), new(150, 108, 60), false, false, false);
                    var h = House(b, 14, seed, thatch: true).Outline();
                    cv.Put(h, 5, fy - h.H + 2);
                    int hx = 12, hy = fy - h.H + 5;
                    for (int i = -8; i <= 8; i++) { cv.Set(hx + i, hy + i, wood[3]); cv.Set(hx + i, hy - i, wood[2]); }
                    break;
                }
                case FoliageKind.Spring:
                    cv.Oval(13, fy - 4, 9, 4, (x, y) => stone[(x + y) % 3 == 0 ? 3 : 2]);
                    cv.Oval(13, fy - 4, 7, 2.8f, (x, y) => Tones(70, 128, 140)[y < fy - 4 ? 3 : 2]);
                    break;
                case FoliageKind.Mine:
                    cv.Oval(13, fy - 4, 11, 9, (x, y) => stone[x < 10 ? 3 : 2]);
                    for (int y = fy; y < 30; y++) for (int x = 0; x < 26; x++) cv.Clear(x, y + 1);
                    cv.Rect(10, fy - 6, 15, fy, new Color(30, 24, 26));
                    cv.HLine(9, 16, fy - 7, wood[2]); cv.VLine(9, fy - 7, fy, wood[2]); cv.VLine(16, fy - 7, fy, wood[1]);
                    break;
                default: // shrine, hut
                {
                    var b = new Build(new(140, 136, 126), kind == FoliageKind.Shrine ? new(200, 196, 184) : new(176, 150, 112), new(110, 104, 100), new(150, 108, 60), false, false, false);
                    var h = House(b, 12, seed, thatch: kind != FoliageKind.Shrine).Outline();
                    cv.Put(h, 6, fy - h.H + 2);
                    break;
                }
            }
            return cv.Outline().Done(fx, fy);
        });
    }
}
