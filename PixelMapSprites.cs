using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class PixelMap
{
    /// A painted sprite: its pixels and where its foot stands (in its own pixels).
    sealed record Sprite(Color[] Px, int W, int H, int FootX, int FootY);

    /// What builds a place: walls, houses and roofs by the country it stands in and the people who live there.
    sealed record Build(Color Stone, Color House, Color Roof, Color Roof2, bool Flat, bool Logs, bool Tents);

    sealed partial class View
    {
        readonly record struct Placed(Sprite S, Vector2 Foot, float Z, float Shadow);

        void Sprites()
        {
            var list = new List<Placed>();
            float margin = 40;
            foreach (var f in t.Foliage)
            {
                float h = t.Height(f.Pos);
                var sp = Screen(f.Pos, h);
                if (sp.X < -margin || sp.X > W + margin || sp.Y < -margin || sp.Y > H + margin * 2) continue;
                if (m.Designed && h < WorldMap.SeaLevel) continue;
                var s = SpriteFor(f);
                if (s == null) continue;
                float shadow = f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine or FoliageKind.Birch or FoliageKind.Bush ? s.W * 0.42f : s.W * 0.48f;
                list.Add(new Placed(s, sp, f.Pos.Y, shadow));
            }
            foreach (var p in c.S.Parties)
            {
                if (p.Down || p.Captive) continue;
                var at = p.Pos;
                if (!p.Moving && c.SettlementAt(p.Pos, 3) is { } st) at = st.Pos + new Vector2(st.Kind == SettlementKind.Village ? 9 : 18, st.Kind == SettlementKind.Village ? 6 : 12); // beside the gate, not on it
                var sp = Screen(at, t.Height(at));
                if (sp.X < -20 || sp.X > W + 20 || sp.Y < -20 || sp.Y > H + 30) continue;
                list.Add(new Placed(Art.Party(p, c, k), sp, at.Y + 0.01f, 3 * k));
            }
            foreach (var pl in list) Shadow(pl);
            foreach (var pl in list.OrderBy(p => p.Foot.Y).ThenBy(p => p.Foot.X)) Blit(pl);
        }

        Sprite? SpriteFor(Foliage f)
        {
            var land = Lands.Of(m.ProvinceAt(f.Pos).Kind);
            switch (f.Kind)
            {
                case FoliageKind.Broadleaf: return Art.Tree(Px(f.Scale * 1.55f), f.Seed & 3, m.Season, land.Kind is "Oasis" or "Desert" ? 2 : 0);
                case FoliageKind.Birch: return Art.Tree(Px(f.Scale * 1.55f), f.Seed & 3, m.Season, 1);
                case FoliageKind.Pine: return Art.Pine(Px(f.Scale * 1.7f), f.Seed & 3, m.Season, land.Cold);
                case FoliageKind.Bush: return Art.Bush(Px(f.Scale * 0.55f), f.Seed & 3, m.Season);
                case FoliageKind.Town or FoliageKind.Castle or FoliageKind.Village:
                {
                    var s = c.S.Settlements.MinBy(s => Vector2.DistanceSquared(s.Pos, f.Pos))!;
                    return Art.Place(s.Kind, BuildOf(s, land), Style.Sides[Style.PaletteOf(s.Faction)], k, s.Id);
                }
                case FoliageKind.Ruin or FoliageKind.Shrine or FoliageKind.Stones or FoliageKind.Mine or FoliageKind.Hut
                    or FoliageKind.Watch or FoliageKind.Spring or FoliageKind.Mill:
                    return Art.Site(f.Kind, k, f.Seed);
                default: return null;
            }
        }

        int Px(float metres) => Math.Max(4, (int)MathF.Round(metres * k));

        static Build BuildOf(Settlement s, LandDef land)
        {
            string cu = s.Culture;
            if (land.Hot || cu.Contains("south") || cu.Contains("sun"))
                return new(new(170, 128, 84), new(236, 228, 206), new(196, 176, 140), new(176, 104, 64), true, false, false); // ochre walls round white houses
            if (cu.Contains("steppe") || land.Kind == "Steppe")
                return new(new(150, 120, 84), new(226, 216, 190), new(170, 70, 50), new(120, 94, 64), false, false, true);
            if (land.Cold || cu.Contains("north"))
                return new(new(112, 84, 56), new(150, 108, 68), new(84, 58, 42), new(70, 62, 52), false, true, false); // logs, dark shingle
            return new(new(136, 132, 122), new(214, 200, 166), new(164, 74, 52), new(150, 108, 60), false, false, false); // red tile, brown thatch
        }

        /// A soft drop shadow down and right of each thing standing (the sun is up and to the left), on the ground only.
        void Shadow(Placed pl)
        {
            float rx = pl.Shadow, ry = MathF.Max(1.5f, pl.Shadow * 0.36f);
            var at = pl.Foot + new Vector2(pl.Shadow * 0.35f, -ry * 0.3f);
            for (int y = (int)(at.Y - ry); y <= (int)(at.Y + ry); y++)
                for (int x = (int)(at.X - rx); x <= (int)(at.X + rx); x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    float dx = (x + 0.5f - at.X) / rx, dy = (y + 0.5f - at.Y) / ry;
                    if (dx * dx + dy * dy > 1) continue;
                    int i = y * W + x;
                    if (stuff[i] is Stuff.Sea or Stuff.Water || near[i] == float.MinValue) continue;
                    px[i] = Ramps.Darken(px[i], 0.78f);
                }
        }

        void Blit(Placed pl)
        {
            var s = pl.S;
            int ox = (int)MathF.Round(pl.Foot.X) - s.FootX, oy0 = (int)MathF.Round(pl.Foot.Y) - s.FootY;
            for (int y = 0; y < s.H; y++)
                for (int x = 0; x < s.W; x++)
                {
                    var col = s.Px[y * s.W + x];
                    if (col.A == 0) continue;
                    int sx = ox + x, sy = oy0 + y;
                    if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                    int i = sy * W + sx;
                    if (near[i] > pl.Z + 1.2f) continue; // nearer ground (a hill in front) hides it
                    px[i] = col;
                }
        }

        /// Names of towns and castles in the map's pixel font, cream with a dark rim, over the place.
        void Labels()
        {
            var taken = new List<Rectangle>();
            float uu = k / 3 * 1.35f;
            var places = c.S.Settlements.Where(s => s.Kind != SettlementKind.Village).Select(s => // a name may lie over a hamlet, not over a town or castle
            {
                var sp = Screen(s.Pos, t.Height(s.Pos));
                var (w, h) = s.Kind switch { SettlementKind.Town => (58, 50), SettlementKind.Castle => (40, 60), _ => (40, 30) };
                return (s.Id, R: new Rectangle((int)(sp.X - w * uu / 2), (int)(sp.Y - h * uu), (int)(w * uu), (int)(h * uu)));
            }).ToList();
            foreach (var s in c.S.Settlements.OrderBy(s => s.Kind switch { SettlementKind.Town => 0, SettlementKind.Castle => 1, _ => 2 }))
            {
                if (s.Kind == SettlementKind.Village && k < 3) continue;
                var sp = Screen(s.Pos, t.Height(s.Pos));
                string name = s.Name.ToUpperInvariant();
                int w = name.Length * 6 - 1, lift = (int)((s.Kind switch { SettlementKind.Village => 40, SettlementKind.Castle => 82, _ => 66 }) * k / 3);
                int lx = (int)sp.X - w / 2, ly = (int)sp.Y - lift - 9;
                if (lx < -w || lx > W || ly < -10 || ly > H) continue;
                var box = new Rectangle(lx - 3, ly - 2, w + 6, 15);
                if (taken.Any(r => r.Intersects(box)) || places.Any(pl => pl.Id != s.Id && pl.R.Intersects(box))) continue;
                taken.Add(box);
                var ui = Style.Sides[Style.PaletteOf(s.Faction)].Ui;
                var ink = s.Kind == SettlementKind.Village ? new Color(206, 198, 172) : new Color(244, 234, 204);
                for (int y = ly - 2; y <= ly + 8; y++) // a dark plate behind the name, so it reads over busy ground
                    for (int x = lx - 3; x <= lx + w + 2; x++)
                        if (x >= 0 && y >= 0 && x < W && y < H && !((y == ly - 2 || y == ly + 8) && (x == lx - 3 || x == lx + w + 2)))
                            px[y * W + x] = Ramps.Darken(px[y * W + x], 0.42f);
                for (int pass = 0; pass < 2; pass++)
                    for (int ci = 0; ci < name.Length; ci++)
                        for (int row = 0; row < 7; row++)
                            for (int col = 0; col < 5; col++)
                            {
                                if (!PixelFont.Lit(name[ci], col, row)) continue;
                                int gx = lx + ci * 6 + col, gy = ly + row;
                                if (pass == 0)
                                {
                                    for (int dy = -1; dy <= 1; dy++)
                                        for (int dx = -1; dx <= 1; dx++) Put(gx + dx, gy + dy, new Color(20, 18, 26));
                                }
                                else Put(gx, gy, ink);
                            }
                if (s.Kind != SettlementKind.Village)
                    for (int x = lx - 1; x <= lx + w; x++) { Put(x, ly + 9, new Color(20, 18, 26)); Put(x, ly + 10, ui); Put(x, ly + 11, new Color(20, 18, 26)); }
            }
        }

        void Put(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }
    }

    /// The sprites, painted by code with the soldiers' painter (Painter.cs: lit shapes, five-tone hue-shifted ramps, a
    /// selective outline) at the exact pixel size they are drawn, cached.
    static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new();

        static Sprite Cached(string key, Func<Sprite> make)
        {
            lock (cache)
            {
                if (!cache.TryGetValue(key, out var s)) cache[key] = s = make();
                return s;
            }
        }

        static Sprite Done(Canvas cv) => new(cv.Render(), cv.W + 2, cv.H + 2, (cv.W + 2) / 2, cv.H + 1);

        static uint Hsh(int a, int b) => Hash(a * 7919 + 13, b * 104729 + 7);

        /// Leaves in clumps: a pixel's light nudged by a coarse hash, so a crown reads as masses rather than one smooth ball.
        static void Leafy(Canvas cv, int seed)
        {
            for (int y = 0; y < cv.H; y++)
                for (int x = 0; x < cv.W; x++)
                {
                    uint h = Hsh(x / 2 + seed, y / 2 + (x & 1) * 3);
                    if (h % 5 == 0) cv.Shade(x, y, -0.22f);
                    else if (h % 7 == 0) cv.Shade(x, y, 0.16f);
                }
        }

        static Color Leaves(int season, int kind, int v)
        {
            if (kind == 1) return season == 2 ? new Color(206, 168, 64) : new Color(128, 160, 70);
            if (kind == 2) return new Color(96, 132, 58);
            return season switch
            {
                2 => v % 2 == 0 ? new Color(178, 102, 44) : new Color(196, 142, 50),
                3 => new Color(104, 92, 76),
                _ => v switch { 0 => new Color(74, 112, 46), 1 => new Color(66, 104, 50), 2 => new Color(84, 118, 44), _ => new Color(60, 98, 44) },
            };
        }

        /// A broadleaf (kind 0), a birch (1) or a palm-green southern tree (2): a trunk and a crown of lit lumps.
        public static Sprite Tree(int h, int v, int season, int kind) => Cached($"tree{h}.{v}.{season}.{kind}", () =>
        {
            h = Math.Max(h, 6);
            int w = Math.Max(5, (int)MathF.Round(h * (kind == 1 ? 0.55f : 0.8f)));
            var cv = new Canvas(w, h);
            var bark = kind == 1 ? new Mat(new Color(222, 218, 204)) : new Mat(new Color(92, 66, 44));
            float cx = w / 2f;
            cv.Rect(cx - (h > 12 ? 1.2f : 0.7f), h * 0.6f, cx + (h > 12 ? 1.2f : 0.7f), h, bark);
            if (kind == 1) for (int y = (int)(h * 0.62f); y < h; y += 2) cv.Shade((int)cx, y, -0.9f);
            var leaf = new Mat(Leaves(season, kind, v));
            float r = w * 0.5f, cy = h * 0.38f;
            cv.Ellipse(cx, cy, r, h * 0.36f, leaf);
            float s = (v & 1) == 0 ? 1 : -1;
            cv.Ellipse(cx - s * r * 0.45f, cy + h * 0.1f, r * 0.62f, h * 0.24f, leaf, -0.05f);
            cv.Ellipse(cx + s * r * 0.4f, cy - h * 0.12f, r * 0.55f, h * 0.22f, leaf, 0.08f);
            Leafy(cv, v * 31 + h);
            return Done(cv);
        });

        /// A pine: tiers of boughs, each darker along its lower edge, on a short trunk; frosted in cold country.
        public static Sprite Pine(int h, int v, int season, bool cold) => Cached($"pine{h}.{v}.{season}.{cold}", () =>
        {
            h = Math.Max(h, 7);
            int w = Math.Max(5, (int)MathF.Round(h * (0.56f + 0.06f * (v & 1))));
            var cv = new Canvas(w, h);
            float cx = w / 2f;
            cv.Rect(cx - 0.7f, h * 0.8f, cx + 0.7f, h, new Mat(new Color(84, 60, 42)));
            var needles = new Mat(cold ? new Color(46, 76, 66) : new Color(44, 84, 56));
            int tiers = h >= 14 ? 4 : 3;
            float top = 0, bottom = h * 0.86f, step = (bottom - top) / (tiers + 0.4f);
            for (int i = tiers - 1; i >= 0; i--)
            {
                float t0 = top + i * step * 0.95f, t1 = t0 + step * 1.45f, half = w / 2f * (0.42f + 0.58f * (i + 1) / tiers);
                cv.Body(t0, t1, y => { float a = (y - t0) / (t1 - t0); return (cx - half * a, cx + half * a); }, needles, 0.04f * (tiers - i));
                for (int x = 0; x < w; x++) cv.Shade(x, (int)t1, -0.35f);
            }
            if (season == 3 || cold && season != 1)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (Hsh(x + v, y) % 4 == 0 && cv.Has(x, y) && !cv.Has(x, y - 1)) cv.Shade(x, y, 0.9f);
            return Done(cv);
        });

        public static Sprite Bush(int h, int v, int season) => Cached($"bush{h}.{v}.{season}", () =>
        {
            h = Math.Max(h, 3);
            int w = Math.Max(4, (int)MathF.Round(h * 1.5f));
            var cv = new Canvas(w, h);
            cv.Ellipse(w / 2f, h * 0.55f, w / 2f, h * 0.5f, new Mat(Leaves(season, 0, v + 1)));
            Leafy(cv, v);
            return Done(cv);
        });

        // ---------- places ----------

        /// A house seen from the south, tilted: the front wall, then the roof's slope above it (pitched or flat), a door.
        static void House(Canvas cv, float x, float y, float w, float wall, float roof, Build b, int seed, bool big = false)
        {
            var wallM = new Mat(b.Logs ? b.House : b.House, b.Logs ? Pattern.Wood : Pattern.None);
            var roofM = new Mat((seed & 1) == 0 ? b.Roof : b.Roof2, b.Logs ? Pattern.Hair : Pattern.None);
            if (b.Flat)
            {
                cv.Rect(x, y - wall - roof * 0.5f, x + w, y, wallM);
                var top = new Mat(Ramps.Ramp(b.House.ToVector3(), 1)[1]);
                cv.Rect(x, y - wall - roof * 0.5f, x + w, y - wall + 0.4f, top, 0.25f); // the roof seen from above, a shade under the wall
                for (float xx = x; xx <= x + w; xx++) cv.Shade((int)xx, (int)(y - wall + 0.4f), -0.6f); // the parapet's shadow
                if (big) cv.Ellipse(x + w / 2, y - wall - roof * 0.5f, w * 0.3f, roof * 0.55f, new Mat(b.Roof)); // a dome
            }
            else
            {
                cv.Rect(x, y - wall, x + w, y, wallM);
                for (float xx = x; xx <= x + w; xx++) cv.Shade((int)xx, (int)y, -0.5f); // a dark footing
                float r0 = y - wall - roof, r1 = y - wall + 0.6f;
                cv.Body(r0, r1, yy => { float a = (yy - r0) / (r1 - r0); float o = (1 - a) * w * 0.12f; return (x - 0.6f + o, x + w + 0.6f - o); }, roofM, 0.1f);
                for (float xx = x; xx <= x + w; xx++) cv.Shade((int)xx, (int)r1, -0.45f); // the eave's shadow
                if (!b.Logs && (seed & 2) != 0) // timber framing
                    for (float xx = x + 2; xx < x + w - 1; xx += 3) for (float yy = y - wall; yy < y; yy++) cv.Shade((int)xx, (int)yy, -0.5f);
            }
            int dx = (int)(x + w * (0.3f + 0.4f * ((seed >> 2) & 1)));
            cv.Rect(dx, y - MathF.Min(2.5f, wall * 0.6f), dx + 0.9f, y, new Mat(new Color(60, 44, 34)));
            // Each house reads on its own: its east end in shadow (the sun is to the west), a dark window or two.
            for (float yy = y - wall - (b.Flat ? roof * 0.5f : 0); yy < y; yy++) { cv.Shade((int)(x + w), (int)yy, -0.7f); cv.Shade((int)(x + w) - 1, (int)yy, -0.3f); }
            if (wall >= 3)
                for (float wx = x + 1.5f; wx < x + w - 1; wx += 3)
                    if ((int)wx != dx && (int)wx != dx + 1) cv.Rect(wx, y - wall + 1, wx + 0.9f, y - wall + 1.9f, new Mat(new Color(54, 46, 50)));
        }

        /// A tent of the steppe: a round felt dome on a low wall, a dark door.
        static void Tent(Canvas cv, float x, float y, float w, Build b, int seed)
        {
            // A low drum of felt, a shallow cone of roof with a dark crown ring, a coloured band where they meet, its east
            // side in shadow, a dark door: small, so a camp reads as many tents rather than one white mass.
            var felt = new Mat((seed & 1) == 0 ? b.House : new Color(206, 188, 150));
            float drum = MathF.Max(2, w * 0.32f), cone = MathF.Max(2, w * 0.36f);
            cv.Rect(x, y - drum, x + w, y, felt);
            cv.Body(y - drum - cone, y - drum + 0.5f, yy => { float a = (yy - (y - drum - cone)) / (cone + 0.5f); return (x + w / 2 - (0.12f + 0.88f * a) * w / 2, x + w / 2 + (0.12f + 0.88f * a) * w / 2); }, felt, 0.12f);
            cv.Rect(x, y - drum, x + w, y - drum + 0.9f, new Mat(b.Roof));
            cv.Rect(x + w / 2 - 0.6f, y - drum - cone, x + w / 2 + 0.6f, y - drum - cone + 0.9f, new Mat(new Color(70, 52, 40)));
            for (float yy = y - drum - cone * 0.5f; yy < y; yy++) cv.Shade((int)(x + w), (int)yy, -0.6f);
            cv.Rect(x + w * 0.4f, y - drum * 0.75f, x + w * 0.58f, y, new Mat(new Color(60, 44, 34)));
        }

        static void Tower(Canvas cv, float cx, float y0, float y1, float r, Mat stone, Mat? cap)
        {
            cv.Body(y0, y1, _ => (cx - r, cx + r), stone);
            for (float x = cx - r; x <= cx + r; x += 2) cv.Clear((int)x, (int)y0); // merlons
            if (cap != null) cv.Body(y0 - r * 1.6f, y0 + 0.5f, yy => { float a = (yy - (y0 - r * 1.6f)) / (r * 1.6f + 0.5f); return (cx - (r + 0.6f) * a, cx + (r + 0.6f) * a); }, cap, 0.1f);
        }

        static void Wall(Canvas cv, float x0, float x1, float y0, float y1, Mat stone)
        {
            cv.Rect(x0, y0, x1, y1, stone);
            for (float x = x0 + 1; x <= x1; x += 2) cv.Clear((int)x, (int)y0);
            for (float x = x0; x <= x1; x++) cv.Shade((int)x, (int)y0 + 1, 0.25f); // the walk catches the light
        }

        static void Flag(Canvas cv, float x, float top, float u, Heraldry side)
        {
            cv.Rect(x, top, x + 0.8f, top + 6 * u, new Mat(new Color(70, 52, 38)));
            cv.Rect(x + 1, top, x + 1 + 4.5f * u, top + 2.6f * u, new Mat(side.Cloth, Pattern.Cloth));
            cv.Rect(x + 1, top + 2.6f * u - 0.6f, x + 1 + 4.5f * u, top + 2.6f * u, new Mat(side.Trim));
        }

        public static Sprite Place(SettlementKind kind, Build b, Heraldry side, float k, int id) => Cached($"place{kind}.{b}.{side.Name}.{k}.{id}", () =>
        {
            float u = k / 3 * 1.35f;
            var stone = new Mat(b.Stone, b.Logs ? Pattern.Wood : Pattern.None);
            Canvas cv;
            if (kind == SettlementKind.Town)
            {
                int w = (int)(58 * u), h = (int)(46 * u);
                cv = new Canvas(w, h);
                float back = h * 0.36f, front = h * 0.8f;
                if (!b.Tents) { Wall(cv, 4 * u, w - 4 * u, back - 3 * u, back + 1 * u, stone); }
                // Houses in rows, back to front, and the great house (keep, temple) among them.
                int rows = 4;
                for (int r = 0; r < rows; r++)
                {
                    float y = back + 3 * u + (front - back - 2 * u) * (r + 1) / (rows + 0.5f);
                    float x = 6 * u + (r % 2) * 3 * u;
                    int n = 0;
                    while (x < w - 10 * u)
                    {
                        uint hh = Hsh(id * 17 + r, n);
                        float hw = (6 + hh % 4) * u;
                        if (r == 1 && n == 2)
                        {
                            float gw = 10 * u;
                            House(cv, x, y, gw, 10 * u, 6 * u, b, (int)hh, true);
                            if (!b.Flat) Tower(cv, x + gw / 2, y - 20 * u, y - 9 * u, 2.2f * u, new Mat(b.Stone), new Mat(b.Roof2));
                            Flag(cv, x + gw / 2 - 0.4f, b.Flat ? y - 22 * u : y - 30 * u, u, side);
                            x += gw + 1;
                        }
                        else if (b.Tents) { if (((hh >> 4) & 3) != 0) Tent(cv, x, y, hw * 0.72f, b, (int)hh); x += hw + 2.5f * u; }
                        else { House(cv, x, y, hw, (3 + hh % 2) * u, (3 + (hh >> 3) % 2) * u, b, (int)hh); x += hw + 0.5f * u; }
                        n++;
                    }
                }
                if (!b.Tents)
                {
                    cv.Line(4 * u, back, 2 * u, front, stone, 0.45f, 2 * u);
                    cv.Line(w - 4 * u, back, w - 2 * u, front, stone, 0.3f, 2 * u);
                    Wall(cv, 2 * u, w - 2 * u, front - 1 * u, front + 4 * u, stone);
                    cv.Rect(w / 2f - 2.5f * u, front + 0.5f * u, w / 2f + 2.5f * u, front + 4 * u, new Mat(new Color(40, 30, 28))); // the gate
                    foreach (var (tx, ty0, ty1) in new[] { (4 * u, back - 7 * u, back + 1 * u), (w - 4 * u, back - 7 * u, back + 1 * u), (3 * u, front - 5 * u, front + 5 * u), (w - 3 * u, front - 5 * u, front + 5 * u),
                                 (w / 2f - 4.5f * u, front - 4 * u, front + 4.5f * u), (w / 2f + 4.5f * u, front - 4 * u, front + 4.5f * u) })
                        Tower(cv, tx, ty0, ty1, 2.4f * u, stone, b.Flat ? null : new Mat(b.Roof2));
                }
            }
            else if (kind == SettlementKind.Castle)
            {
                int w = (int)(40 * u), h = (int)(46 * u);
                cv = new Canvas(w, h);
                float back = h * 0.42f, front = h * 0.82f;
                Wall(cv, 4 * u, w - 4 * u, back - 3 * u, back + 1 * u, stone);
                float kx = w / 2f - 7 * u;
                cv.Rect(kx, back - 16 * u, kx + 14 * u, front - 3 * u, stone); // the keep
                for (float x = kx; x <= kx + 14 * u; x += 2) cv.Clear((int)x, (int)(back - 16 * u));
                // The keep reads as a block: its east face in shadow, courses of stone, a lit wall-walk.
                for (float y = back - 16 * u; y < front - 3 * u; y++)
                {
                    for (float x = kx + 10.5f * u; x <= kx + 14 * u; x++) cv.Shade((int)x, (int)y, -0.45f);
                    if (((int)y & 3) == 0) for (float x = kx; x <= kx + 14 * u; x += 1) if (((int)x + (int)y / 4) % 5 != 0) cv.Shade((int)x, (int)y, -0.12f);
                }
                for (float x = kx; x <= kx + 14 * u; x++) cv.Shade((int)x, (int)(back - 16 * u) + 1, 0.35f);
                for (float y = back - 12 * u; y < front - 6 * u; y += 4 * u) cv.Rect(kx + 3 * u, y, kx + 4 * u, y + 1.5f * u, new Mat(new Color(36, 30, 32)));
                for (float y = back - 12 * u; y < front - 6 * u; y += 4 * u) cv.Rect(kx + 10 * u, y, kx + 11 * u, y + 1.5f * u, new Mat(new Color(36, 30, 32)));
                Tower(cv, kx + 11.5f * u, back - 22 * u, back - 14 * u, 2.2f * u, stone, b.Flat ? null : new Mat(b.Roof2));
                Flag(cv, kx + 11.1f * u, back - 34 * u, u, side);
                cv.Line(4 * u, back, 2 * u, front, stone, 0.45f, 2 * u);
                cv.Line(w - 4 * u, back, w - 2 * u, front, stone, 0.3f, 2 * u);
                Wall(cv, 2 * u, w - 2 * u, front - 1 * u, front + 4 * u, stone);
                cv.Rect(w / 2f - 2 * u, front + 0.5f * u, w / 2f + 2 * u, front + 4 * u, new Mat(new Color(40, 30, 28)));
                foreach (var (tx, ty0, ty1) in new[] { (4 * u, back - 8 * u, back + 1 * u), (w - 4 * u, back - 8 * u, back + 1 * u), (3 * u, front - 6 * u, front + 5 * u), (w - 3 * u, front - 6 * u, front + 5 * u) })
                    Tower(cv, tx, ty0, ty1, 2.6f * u, stone, b.Flat ? null : new Mat(b.Roof2));
            }
            else
            {
                // A few real houses on set plots (two behind, two in front), a fenced yard and a little smoke: a hamlet,
                // not a heap of blocks.
                int w = (int)(40 * u), h = (int)(30 * u);
                cv = new Canvas(w, h);
                var spots = new[] { (2f, 15f), (19f, 13f), (9f, 27f), (26f, 28f) }
                    .Select((s, i) => (X: (s.Item1 + Hsh(id, i) % 4) * u, Y: s.Item2 * u)).ToList();
                var fence = new Mat(new Color(120, 92, 62));
                float fy = h - 1;
                for (float x = 0; x < w * 0.45f; x += 2 * u) cv.Rect(x, fy - 1.5f * u, x + 0.8f, fy, fence);
                cv.Rect(0, fy - 1.1f * u, w * 0.45f, fy - 0.6f * u, fence);
                foreach (var (x, y) in spots)
                {
                    uint hh = Hsh((int)x, (int)y);
                    float hw = (8 + hh % 3) * u;
                    if (b.Tents) Tent(cv, x, y, hw * 0.8f, b, (int)hh);
                    else House(cv, x, y, hw, 4 * u, 4 * u, b, (int)hh);
                }
                if (!b.Tents) // smoke from one chimney
                    for (int i = 0; i < 4; i++) cv.Set((int)(spots[1].X + 3 * u + (i % 2)), (int)(spots[1].Y - 9 * u - i * 1.5f * u), new Mat(new Color(196, 196, 200)), 0.5f + i * 0.05f);
            }
            return Done(cv);
        });

        public static Sprite Site(FoliageKind kind, float k, int seed) => Cached($"site{kind}.{k}", () =>
        {
            float u = k / 3;
            var stone = new Mat(new Color(132, 128, 120));
            var cv = new Canvas((int)(16 * u) + 2, (int)(16 * u) + 2);
            float w = cv.W, h = cv.H;
            switch (kind)
            {
                case FoliageKind.Stones:
                    for (int i = 0; i < 4; i++) cv.Body(h * 0.35f + (i % 2) * 2 * u, h - (i % 2) * u, _ => (1 + i * w / 4.2f, 1 + i * w / 4.2f + 2 * u), stone, 0.05f * i);
                    break;
                case FoliageKind.Watch: Tower(cv, w / 2, 1, h, 2.6f * u, stone, new Mat(new Color(120, 66, 48))); break;
                case FoliageKind.Ruin:
                    cv.Rect(1, h * 0.4f, w * 0.35f, h, stone); cv.Rect(w * 0.5f, h * 0.65f, w - 1, h, stone, -0.05f);
                    for (float x = 1; x < w; x += 2) { cv.Clear((int)x, (int)(h * 0.4f)); cv.Clear((int)x + 1, (int)(h * 0.65f)); }
                    break;
                case FoliageKind.Shrine:
                    cv.Rect(w * 0.3f, h * 0.35f, w * 0.7f, h, stone);
                    cv.Ellipse(w / 2, h * 0.32f, w * 0.18f, h * 0.12f, new Mat(new Color(214, 176, 72)), 0.2f);
                    break;
                case FoliageKind.Mill:
                    House(cv, w * 0.2f, h, w * 0.6f, 4 * u, 3 * u, new Build(new(136, 132, 122), new(214, 200, 166), new(176, 146, 84), new(176, 146, 84), false, false, false), seed);
                    cv.Line(w * 0.15f, h * 0.15f, w * 0.85f, h * 0.65f, new Mat(new Color(196, 182, 150)), 0.5f, 1.5f * u);
                    cv.Line(w * 0.85f, h * 0.15f, w * 0.15f, h * 0.65f, new Mat(new Color(196, 182, 150)), 0.4f, 1.5f * u);
                    break;
                case FoliageKind.Spring:
                    cv.Ellipse(w / 2, h * 0.75f, w * 0.4f, h * 0.18f, new Mat(new Color(68, 130, 150)), 0.1f);
                    break;
                case FoliageKind.Mine:
                    cv.Body(h * 0.35f, h, y => { float a = (y - h * 0.35f) / (h * 0.65f); return (w / 2 - a * w * 0.48f, w / 2 + a * w * 0.48f); }, stone);
                    cv.Rect(w * 0.4f, h * 0.7f, w * 0.6f, h, new Mat(new Color(30, 24, 24)));
                    break;
                default:
                    House(cv, w * 0.25f, h, w * 0.5f, 3 * u, 3 * u, new Build(new(136, 132, 122), new(150, 120, 84), new(120, 100, 66), new(120, 100, 66), false, true, false), seed);
                    break;
            }
            return Done(cv);
        });

        // ---------- parties ----------

        /// A party as its leader with its banner: lords and the company on horseback, brigands on foot, merchants with a wagon.
        public static Sprite Party(Party p, Campaign c, float k)
        {
            int pal = p.Kind == PartyKind.Player ? 2 : Style.PaletteOf(c.SideOf(p));
            string kind = p.Kind switch { PartyKind.Bandit or PartyKind.Monster => "foot", PartyKind.Caravan => "wagon", _ => "rider" };
            bool left = MathF.Cos(p.Facing) < 0;
            return Cached($"party{kind}.{pal}.{k}.{left}.{p.Id % 4}", () =>
            {
                float u = k / 3 * 1.3f;
                var side = Style.Sides[pal];
                var cloth = new Mat(side.Cloth, Pattern.Cloth);
                var skin = new Mat(new Color(222, 172, 132));
                var steel = new Mat(new Color(150, 156, 166), Pattern.Plate);
                var cv = new Canvas((int)(16 * u) + 2, (int)(20 * u) + 2);
                float w = cv.W, h = cv.H, cx = w / 2;
                if (kind == "rider")
                {
                    // A horse in profile facing right: barrel, a raised neck and head, four legs, a tail; the rider in his
                    // side's cloth, and the banner high behind him so the party reads by its colour.
                    var coat = new Mat(new Color(Style.Coats[p.Id % Style.Coats.Length]));
                    float by = h - 4.2f * u, bx = cx - 0.5f * u;
                    foreach (float lx in new[] { -3.6f, -2.2f, 2.0f, 3.3f })
                        cv.Rect(bx + lx * u, by, bx + lx * u + 0.9f * u, h, coat, lx is -2.2f or 2.0f ? -0.25f : 0);
                    cv.Ellipse(bx, by - 0.6f * u, 4.8f * u, 2.3f * u, coat);
                    cv.Body(by - 6.2f * u, by - 0.8f * u, y => { float a = (y - (by - 6.2f * u)) / (5.4f * u); return (bx + (3.0f + 0.4f * a) * u, bx + (4.4f + 0.8f * a) * u); }, coat, 0.1f);
                    cv.Body(by - 6.6f * u, by - 4.6f * u, _ => (bx + 4.0f * u, bx + 6.4f * u), coat, 0.15f); // the head
                    cv.Line(bx - 4.8f * u, by - 1.6f * u, bx - 5.8f * u, by + 1.2f * u, new Mat(new Color(54, 40, 32)), 0.4f, 1.2f * u); // the tail
                    cv.Rect(bx - 2.6f * u, by - 2.8f * u, bx + 2.2f * u, by - 0.6f * u, cloth); // the saddle cloth
                    cv.Body(by - 7.6f * u, by - 2.4f * u, y => { float a = (y - (by - 7.6f * u)) / (5.2f * u); return (bx - (1.1f + 0.4f * a) * u, bx + (1.1f + 0.4f * a) * u); }, cloth, 0.1f);
                    cv.Ellipse(bx, by - 8.6f * u, 1.3f * u, 1.3f * u, p.Tier >= 3 || p.Kind == PartyKind.Player ? steel : skin, 0.15f);
                    float fx = bx - 2.4f * u;
                    cv.Rect(fx, 1, fx + MathF.Max(0.9f, 0.7f * u), by - 2 * u, new Mat(new Color(76, 56, 40)));
                    cv.Rect(fx + 1, 1, fx + 1 + 6 * u, 1 + 4 * u, new Mat(side.Cloth, Pattern.Cloth), 0.1f);
                    cv.Rect(fx + 1 + 2.4f * u, 1, fx + 1 + 3.4f * u, 1 + 4 * u, new Mat(side.Trim)); // a pale on the flag
                }
                else if (kind == "foot")
                {
                    float by = h;
                    cv.Rect(cx - 1.4f * u, by - 3.5f * u, cx - 0.4f * u, by, new Mat(new Color(70, 58, 46)));
                    cv.Rect(cx + 0.4f * u, by - 3.5f * u, cx + 1.4f * u, by, new Mat(new Color(70, 58, 46)));
                    cv.Body(by - 9 * u, by - 3 * u, y => { float a = (y - (by - 9 * u)) / (6 * u); return (cx - (1.3f + a) * u, cx + (1.3f + a) * u); }, cloth);
                    cv.Ellipse(cx, by - 10.3f * u, 1.3f * u, 1.3f * u, new Mat(new Color(110, 92, 70)), 0.1f); // a hood
                    cv.Line(cx + 2 * u, by - 12 * u, cx + 3 * u, by - 5 * u, new Mat(new Color(96, 70, 46)), 0.4f, 1.2f * u);
                }
                else
                {
                    float by = h - 1.5f * u;
                    cv.Rect(cx - 5 * u, by - 5 * u, cx + 4 * u, by - 1.5f * u, new Mat(new Color(120, 86, 54), Pattern.Wood));
                    cv.Ellipse(cx - 0.5f * u, by - 6 * u, 4.6f * u, 2.8f * u, new Mat(new Color(222, 210, 180), Pattern.Cloth), 0.1f);
                    cv.Ellipse(cx - 3 * u, by - 0.5f * u, 1.6f * u, 1.6f * u, new Mat(new Color(70, 52, 38)));
                    cv.Ellipse(cx + 2.5f * u, by - 0.5f * u, 1.6f * u, 1.6f * u, new Mat(new Color(70, 52, 38)));
                    cv.Rect(cx - 0.4f * u, by - 13 * u, cx + 0.4f * u + 0.5f, by - 7 * u, new Mat(new Color(70, 52, 38)));
                    cv.Rect(cx + 0.9f * u, by - 13 * u, cx + 4.5f * u, by - 10.5f * u, cloth);
                }
                var s = Done(cv);
                if (!left) return s;
                var flip = new Color[s.Px.Length];
                for (int y = 0; y < s.H; y++) for (int x = 0; x < s.W; x++) flip[y * s.W + x] = s.Px[y * s.W + s.W - 1 - x];
                return s with { Px = flip };
            });
        }
    }
}
