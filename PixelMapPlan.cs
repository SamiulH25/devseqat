using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class PixelMap
{
    /// What a tile is used for. Each tile holds one thing: that is the rule that keeps the map from overlapping.
    enum Use : byte { Free, Water, Road, Steep, Place, House, Site, Field, Tree, Party }

    sealed partial class View
    {
        /// Tiles kept beyond each side of the screen, so things just off it still claim their ground.
        const int Margin = 10;
        const int GI = (W + T - 1) / T + 2 * Margin, GJ = (H + T - 1) / T + 2 * Margin + 4;
        readonly int gi0, gj0;
        readonly Use[] use;
        readonly sbyte[] crop;
        readonly List<(Sprite S, Vector2 Foot, int Shadow)> things = new();
        readonly List<(Settlement S, Rectangle R)> placeRects = new();   // screen rects of whole place sprites
        readonly List<(Settlement S, Rectangle R)> villageRects = new(); // screen rects round a village's houses
        readonly List<Rectangle> partyRects = new();

        static (int, int) TileOf(Vector2 p) => ((int)MathF.Floor(p.X / Tm), (int)MathF.Floor(p.Y / Tm));
        static Vector2 TileCentre(int i, int j) => new((i + 0.5f) * Tm, (j + 0.5f) * Tm);
        int Idx(int i, int j) { i -= gi0; j -= gj0; return i < 0 || j < 0 || i >= GI || j >= GJ ? -1 : j * GI + i; }
        Use UseAt(int i, int j) { int k = Idx(i, j); return k < 0 ? Use.Water : use[k]; }
        void Claim(int i, int j, Use u) { int k = Idx(i, j); if (k >= 0) use[k] = u; }

        /// True if every tile of the block is one of the allowed uses.
        bool Free(int i0, int j0, int w, int h, params Use[] ok)
        {
            for (int j = j0; j < j0 + h; j++)
                for (int i = i0; i < i0 + w; i++)
                {
                    int k = Idx(i, j);
                    if (k < 0 || Array.IndexOf(ok, use[k]) < 0) return false;
                }
            return true;
        }

        /// True if any tile in the ring round the block is one of these uses (a party keeps a tile clear of walls and houses).
        bool Near(int i0, int j0, int w, int h, params Use[] uses)
        {
            for (int j = j0 - 1; j <= j0 + h; j++)
                for (int i = i0 - 1; i <= i0 + w; i++)
                    if ((i < i0 || j < j0 || i >= i0 + w || j >= j0 + h) && Array.IndexOf(uses, UseAt(i, j)) >= 0) return true;
            return false;
        }

        void ClaimBlock(int i0, int j0, int w, int h, Use u) { for (int j = j0; j < j0 + h; j++) for (int i = i0; i < i0 + w; i++) Claim(i, j, u); }

        /// The screen point of a tile corner.
        Vector2 Corner(int i, int j) => Screen(new Vector2(i * Tm, j * Tm));

        /// The plan, in order of who claims first: water and roads, places, village houses, sites, fields, trees,
        /// then parties on whatever land is left.
        void Plan()
        {
            Base();
            Places();
            Sites();
            Fields();
            Trees();
            Parties();
        }

        void Base()
        {
            for (int j = gj0; j < gj0 + GJ; j++)
                for (int i = gi0; i < gi0 + GI; i++)
                {
                    var cn = TileCentre(i, j);
                    Use u = Use.Free;
                    int lv = int.MinValue;
                    for (int s = 0; s < 5 && u == Use.Free; s++)
                    {
                        var q = s == 4 ? cn : cn + new Vector2((s & 1) == 0 ? -1.6f : 1.6f, (s & 2) == 0 ? -1.6f : 1.6f);
                        float h = t.Height(q);
                        if ((m.Designed && (h < WorldMap.SeaLevel + 0.05f || m.Landness(q) < 0.97f)) || t.WaterDepth(q) > 0) u = Use.Water;
                        else foreach (var l in t.Lakes) if (l.Inside(q, 1)) { u = Use.Water; break; }
                        int ql = LevelOf(h);
                        if (u == Use.Free && lv != int.MinValue && ql != lv) u = Use.Steep;
                        lv = ql;
                    }
                    if (u == Use.Free && m.RoadDist(cn) < 2.6f) u = Use.Road;
                    Claim(i, j, u);
                }
        }

        static Build BuildOf(Settlement s, LandDef land)
        {
            string cu = s.Culture;
            if (land.Hot || cu.Contains("south") || cu.Contains("sun"))
                return new(new(176, 134, 88), new(232, 224, 204), new(196, 176, 140), new(176, 104, 64), true, false, false);
            if (cu.Contains("steppe") || land.Kind == "Steppe")
                return new(new(140, 106, 70), new(226, 216, 190), new(170, 70, 50), new(120, 94, 64), false, true, true);
            if (land.Cold || cu.Contains("north"))
                return new(new(118, 86, 56), new(150, 108, 68), new(84, 64, 50), new(70, 62, 52), false, true, false);
            return new(new(140, 136, 126), new(216, 202, 168), new(166, 76, 54), new(156, 114, 64), false, false, false);
        }

        /// Towns (8x6 tiles) and castles (6x5) take their whole footprint; a village is three or four houses, each on a
        /// free 2x2 plot round its centre, with a tile of ground between them.
        void Places()
        {
            var inView = c.S.Settlements.Where(s => Idx(TileOf(s.Pos).Item1, TileOf(s.Pos).Item2) >= 0).ToList();
            foreach (var s in inView.Where(s => s.Kind != SettlementKind.Village))
            {
                var (si, sj) = TileOf(s.Pos);
                bool town = s.Kind == SettlementKind.Town;
                int w = town ? 8 : 6, h = town ? 6 : 5, i0 = si - w / 2, j0 = sj - h / 2;
                // Two places the map sets close together are pushed apart to the nearest clear footprint.
                for (int r = 0, found = 0; r <= 8 && found == 0; r++)
                    for (int dj = -r; dj <= r && found == 0; dj++)
                        for (int di = -r; di <= r && found == 0; di++)
                            if (Math.Max(Math.Abs(di), Math.Abs(dj)) == r && Free(si - w / 2 + di - 1, sj - h / 2 + dj - 1, w + 2, h + 2, Use.Free, Use.Road, Use.Steep))
                            { i0 = si - w / 2 + di; j0 = sj - h / 2 + dj; found = 1; }
                ClaimBlock(i0 - 1, j0, w + 2, h + 1, Use.Place); // a tile of open ground before the gate and at the sides
                ClaimBlock(i0 - 1, j0, 1, h + 1, Use.Free); ClaimBlock(i0 + w, j0, 1, h + 1, Use.Free); ClaimBlock(i0, j0 + h, w, 1, Use.Road);
                var land = Lands.Of(m.ProvinceAt(s.Pos).Kind);
                var side = Style.Sides[Style.PaletteOf(s.Faction)];
                var b = BuildOf(s, land);
                var spr = town ? Art.Town(b, side, s.Id) : Art.Castle(b, side, s.Id);
                var foot = Corner(i0, j0 + h) + new Vector2(w * T / 2f, -1);
                things.Add((spr, foot, 0));
                var tl = foot - new Vector2(spr.FootX, spr.FootY);
                placeRects.Add((s, new Rectangle((int)tl.X, (int)tl.Y, spr.W, spr.H)));
            }
            (int, int)[] slots = { (-3, -2), (1, -2), (-1, 1), (3, 1), (-5, 1), (-3, 4), (1, 4), (5, -2) };
            foreach (var s in inView.Where(s => s.Kind == SettlementKind.Village))
            {
                var (si, sj) = TileOf(s.Pos);
                var b = BuildOf(s, Lands.Of(m.ProvinceAt(s.Pos).Kind));
                int want = 3 + s.Id % 2, n = 0;
                var box = Rectangle.Empty;
                for (int k = 0; k < slots.Length && n < want; k++)
                {
                    var (di, dj) = slots[(k + s.Id) % slots.Length];
                    int i0 = si + di, j0 = sj + dj;
                    if (!Free(i0, j0, 2, 2, Use.Free)) continue;
                    ClaimBlock(i0, j0, 2, 2, Use.House);
                    var spr = Art.Cottage(b, s.Id * 7 + k);
                    var foot = Corner(i0, j0 + 2) + new Vector2(T, -2);
                    things.Add((spr, foot, 1));
                    var r = new Rectangle((int)foot.X - spr.FootX, (int)foot.Y - spr.FootY, spr.W, spr.H);
                    box = box == Rectangle.Empty ? r : Rectangle.Union(box, r);
                    n++;
                }
                if (n > 0) villageRects.Add((s, box));
            }
        }

        void Sites()
        {
            foreach (var f in t.Foliage)
            {
                if (f.Kind is not (FoliageKind.Ruin or FoliageKind.Shrine or FoliageKind.Stones or FoliageKind.Mine or FoliageKind.Hut
                    or FoliageKind.Watch or FoliageKind.Spring or FoliageKind.Mill)) continue;
                var (i, j) = TileOf(f.Pos);
                if (Idx(i, j) < 0 || !Free(i, j - 1, 2, 2, Use.Free)) continue;
                ClaimBlock(i, j - 1, 2, 2, Use.Site);
                things.Add((Art.Site(f.Kind, f.Seed), Corner(i, j + 1) + new Vector2(T, -2), 1));
            }
        }

        static readonly FieldKind[] Crops = { FieldKind.Grain, FieldKind.Grain, FieldKind.Grain, FieldKind.Green, FieldKind.Green, FieldKind.Ploughed, FieldKind.Ploughed, FieldKind.Fallow, FieldKind.Pasture, FieldKind.Pasture };

        /// Strips one tile wide and three long, on the same grid as everything else, worked by the nearest place within
        /// its reach; lanes on uneven columns and rows break them into blocks of many sizes.
        void Fields()
        {
            for (int cj = (int)MathF.Floor(gj0 / 3f); cj * 3 < gj0 + GJ; cj++)
                for (int ci = (int)MathF.Floor(gi0 / 2f); ci * 2 < gi0 + GI; ci++)
                {
                    if (Hash(ci, 977) % 5 == 0 || Hash(cj, 4111) % 6 == 0) continue;
                    if (!Free(ci * 2, cj * 3, 2, 3, Use.Free)) continue;
                    var cn = new Vector2((ci * 2 + 1) * Tm, (cj * 3 + 1.5f) * Tm);
                    Settlement? best = null;
                    float bd = float.MaxValue;
                    foreach (var s in c.S.Settlements)
                    {
                        float d = Vector2.Distance(s.Pos, cn);
                        if (d < bd) { bd = d; best = s; }
                    }
                    if (best == null) continue;
                    var land = Lands.Of(m.ProvinceAt(best.Pos).Kind);
                    float reach = best.Kind switch { SettlementKind.Town => 60, SettlementKind.Village => 44, _ => 30 } * (0.45f + 0.55f * land.Fields);
                    float core = best.Kind switch { SettlementKind.Town => 22, SettlementKind.Castle => 18, _ => 10 };
                    if (bd < core || bd > reach * (0.7f + 0.6f * Terrain.Noise(cn.X * 0.04f, cn.Y * 0.04f, 61))) continue;
                    bool wood = false;
                    for (int j = cj * 3; j < cj * 3 + 3; j++) for (int i = ci * 2; i < ci * 2 + 2; i++) wood |= TreeTiles.Contains((i, j));
                    if (wood) continue;
                    uint h = Hash(ci * 31 + 7, cj * 17 + 3);
                    var kind = land.Barren ? ((h & 1) == 0 ? FieldKind.Ploughed : FieldKind.Fallow) : Crops[h % (uint)Crops.Length];
                    for (int j = cj * 3; j < cj * 3 + 3; j++)
                        for (int i = ci * 2; i < ci * 2 + 2; i++) { Claim(i, j, Use.Field); crop[Idx(i, j)] = (sbyte)kind; }
                }
        }

        HashSet<(int, int)>? treeTiles;
        HashSet<(int, int)> TreeTiles => treeTiles ??= t.Foliage
            .Where(f => f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine or FoliageKind.Birch)
            .Select(f => TileOf(f.Pos)).ToHashSet();

        /// One tree to a tile: where the map grows one, and in stands where the country is wooded (its kind's tree
        /// count read as a density, through a patchy noise), never hard against a place.
        void Trees()
        {
            for (int j = gj0; j < gj0 + GJ; j++)
                for (int i = gi0; i < gi0 + GI; i++)
                {
                    if (UseAt(i, j) != Use.Free) continue;
                    bool nearPlace = false;
                    for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++) nearPlace |= UseAt(i + di, j + dj) is Use.Place or Use.House or Use.Site;
                    if (nearPlace || UseAt(i, j - 1) == Use.Field) continue; // a crown would rise over the field behind
                    var cn = TileCentre(i, j);
                    var land = Lands.Of(m.ProvinceAt(cn).Kind);
                    float dens = Math.Min(1, land.Trees / 90f);
                    float n = Terrain.Noise(cn.X * 0.03f, cn.Y * 0.03f, 211) * 0.7f + Terrain.Noise(cn.X * 0.11f, cn.Y * 0.11f, 212) * 0.3f;
                    uint hh = Hash(i * 13 + 5, j * 7 + 1);
                    bool grow = n < dens * 0.62f - 0.04f ? hh % 7 != 0 : hh % 400 < dens * 14;
                    if (!grow) continue;
                    Claim(i, j, Use.Tree);
                    bool pine = land.Conifer ? hh % 5 != 0 : hh % 6 == 0;
                    var spr = pine ? Art.Pine((int)hh, land.Cold) : Art.Tree((int)hh, m.Season, land.Hot ? 2 : land.Kind == "Taiga" ? 1 : 0);
                    things.Add((spr, Corner(i, j + 1) + new Vector2(T / 2 + (int)(hh % 3) - 1, -2 - (int)(hh / 3 % 2)), 2));
                }
            foreach (var f in t.Foliage)
            {
                if (f.Kind is not (FoliageKind.Broadleaf or FoliageKind.Pine or FoliageKind.Birch or FoliageKind.Bush)) continue;
                var (i, j) = TileOf(f.Pos);
                if (Idx(i, j) < 0 || UseAt(i, j) != Use.Free || UseAt(i, j - 1) == Use.Field) continue;
                Claim(i, j, Use.Tree);
                var land = Lands.Of(m.ProvinceAt(f.Pos).Kind);
                var spr = f.Kind switch
                {
                    FoliageKind.Pine => Art.Pine(f.Seed, land.Cold),
                    FoliageKind.Bush => Art.Bush(f.Seed, m.Season),
                    FoliageKind.Birch => Art.Tree(f.Seed, m.Season, 1),
                    _ => Art.Tree(f.Seed, m.Season, land.Hot ? 2 : 0),
                };
                things.Add((spr, Corner(i, j + 1) + new Vector2(T / 2, -2), 2));
            }
        }

        /// Each party on the nearest free land to where it is (roads allowed, never water, fields, woods or walls): for a
        /// party at a place, just east of it, facing it. One party shows at a place, yours first, then the greatest.
        void Parties()
        {
            var shownAt = new HashSet<int>();
            foreach (var p in c.S.Parties.OrderBy(p => p.Kind == PartyKind.Player ? 0 : 1).ThenByDescending(p => p.Tier))
            {
                if (p.Down || p.Captive || p.Kind == PartyKind.Garrison) continue;
                var (ai, aj) = TileOf(p.Pos);
                if (Idx(ai, aj) < 0) continue;
                bool left = MathF.Cos(p.Facing) < 0;
                var home = c.S.Settlements.FirstOrDefault(s => Vector2.Distance(s.Pos, p.Pos) < (s.Kind == SettlementKind.Village ? 10 : 18));
                if (home != null)
                {
                    if (!shownAt.Add(home.Id)) continue;
                    var (si, sj) = TileOf(home.Pos);
                    ai = si + (home.Kind switch { SettlementKind.Town => 6, SettlementKind.Castle => 5, _ => 3 });
                    aj = sj + (home.Kind == SettlementKind.Village ? 1 : 2);
                    left = true;
                }
                var spr = Folk.Party(p, c, left);
                int w = (spr.W + T - 1) / T, h = Math.Max(2, (spr.H - 6 + T - 1) / T);
                (int, int)? spot = null;
                for (int r = 0; r <= 9 && spot == null; r++)
                    for (int dj = -r; dj <= r && spot == null; dj++)
                        for (int di = -r; di <= r && spot == null; di++)
                        {
                            if (Math.Max(Math.Abs(di), Math.Abs(dj)) != r) continue;
                            int i0 = ai + di - w / 2, j0 = aj + dj - h + 1;
                            if (Free(i0, j0, w, h, Use.Free, Use.Road) && !Near(i0, j0, w, h, Use.Place, Use.House, Use.Site, Use.Party)) spot = (i0, j0);
                        }
                if (spot is not { } sp) continue;
                ClaimBlock(sp.Item1, sp.Item2, w, h, Use.Party);
                var foot = Corner(sp.Item1, sp.Item2 + h) + new Vector2(w * T / 2f, -2);
                things.Add((spr, foot, 2));
                partyRects.Add(new Rectangle((int)foot.X - spr.FootX, (int)foot.Y - spr.FootY, spr.W, spr.H));
            }
        }

        /// The field at p: hedges round a block's outside, a dark parting between strips, furrows along (or, one strip
        /// in three, across) the strip.
        Sample FieldAt(Vector2 p, int ti, int tj)
        {
            var kind = (FieldKind)crop[Idx(ti, tj)];
            float u = p.X - ti * Tm, w = p.Y - tj * Tm;
            float px1 = 1 / K;
            bool hedge = (u < px1 * 1.5f && UseAt(ti - 1, tj) != Use.Field) || (u > Tm - px1 * 1.5f && UseAt(ti + 1, tj) != Use.Field)
                      || (w < px1 * 1.5f && UseAt(ti, tj - 1) != Use.Field) || (w > Tm - px1 * 1.5f && UseAt(ti, tj + 1) != Use.Field);
            if (hedge) return new Sample(Stuff.Field, Ramps.Hedge, 1, 0, p);
            int strip = tj - (int)MathF.Floor(tj / 3f) * 3, cx = (int)MathF.Floor(ti / 2f);
            var ramp = Ramps.Field(kind, (int)Hash(cx, tj - strip), m.Season);
            if ((ti == cx * 2 && u < px1) || (strip == 0 && w < px1)) return new Sample(Stuff.Field, ramp, 0, 0, p);
            bool across = Hash(cx * 3 + 1, (tj - strip) * 5 + 2) % 3 == 0;
            int furrow = kind is FieldKind.Ploughed or FieldKind.Grain or FieldKind.Green ? ((int)MathF.Floor((across ? w : u) * K / 2) & 1) : 0;
            return new Sample(Stuff.Field, ramp, 2 - furrow, 0, p, Texture: kind is FieldKind.Pasture or FieldKind.Fallow);
        }

        // ---------- drawing ----------

        void Sprites()
        {
            foreach (var th in things) Shadow(th);
            foreach (var th in things.OrderBy(x => x.Foot.Y).ThenBy(x => x.Foot.X)) Blit(th.S, th.Foot);
        }

        /// The shadow, down and right of the thing (the sun is to the upper left): a place's along its east wall and
        /// south face; a house, tree or party's as an oval at its foot.
        void Shadow((Sprite S, Vector2 Foot, int Kind) th)
        {
            var (s, foot, kind) = th;
            if (kind == 0)
            {
                int x1 = (int)foot.X - s.FootX + s.W - 2, yTop = (int)foot.Y - s.FootY + s.H / 3;
                for (int y = yTop; y <= (int)foot.Y + 2; y++)
                    for (int x = x1; x <= x1 + 3 && x < W; x++) Dim(x, y);
                for (int x = (int)foot.X - s.FootX + 4; x <= x1; x++) { Dim(x, (int)foot.Y + 1); Dim(x, (int)foot.Y + 2); }
                return;
            }
            float rx = kind == 1 ? s.W * 0.5f : MathF.Max(4, s.W * 0.42f), ry = kind == 1 ? 3 : 2;
            var at = foot + new Vector2(kind == 1 ? 3 : 2, 0);
            for (int y = (int)(at.Y - ry); y <= (int)(at.Y + ry); y++)
                for (int x = (int)(at.X - rx); x <= (int)(at.X + rx); x++)
                {
                    float dx = (x + 0.5f - at.X) / rx, dy = (y + 0.5f - at.Y) / ry;
                    if (dx * dx + dy * dy <= 1) Dim(x, y);
                }
        }

        void Dim(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            if (stuff[i] is Stuff.Sea or Stuff.Water) return;
            px[i] = Ramps.Darken(px[i], 0.74f);
        }

        void Blit(Sprite s, Vector2 foot)
        {
            int ox = (int)MathF.Round(foot.X) - s.FootX, oy = (int)MathF.Round(foot.Y) - s.FootY;
            for (int y = 0; y < s.H; y++)
                for (int x = 0; x < s.W; x++)
                {
                    var col = s.Px[y * s.W + x];
                    if (col.A != 0) Put(ox + x, oy + y, col);
                }
        }

        /// Names in the game's pixel font on a dark plate, over the place (or under it, if the space over it is taken),
        /// never over another place, a party or another name.
        void Labels()
        {
            var taken = new List<Rectangle>(partyRects);
            var all = placeRects.Concat(villageRects).ToList();
            foreach (var (s, r) in all.OrderBy(x => x.S.Kind switch { SettlementKind.Town => 0, SettlementKind.Castle => 1, _ => 2 }))
            {
                string name = s.Name.ToUpperInvariant();
                int w = name.Length * 6 - 1;
                int lx = r.Center.X - w / 2;
                Rectangle? box = null;
                int topAt = s.Kind == SettlementKind.Village ? r.Top - 12 : r.Top + (s.Kind == SettlementKind.Town ? 14 : 24) - 12;
                foreach (int ly in new[] { topAt, r.Bottom + 4, topAt - 15, r.Bottom + 19 })
                {
                    var b = new Rectangle(lx - 3, ly - 2, w + 6, 15);
                    if (taken.Any(t => t.Intersects(b)) || all.Any(pl => pl.S != s && pl.R.Intersects(b))) continue;
                    box = b;
                    break;
                }
                if (box is not { } bx || bx.Right < 0 || bx.Left > W || bx.Bottom < 0 || bx.Top > H) continue;
                taken.Add(bx);
                int ly0 = bx.Y + 2;
                var ui = Style.Sides[Style.PaletteOf(s.Faction)].Ui;
                var ink = s.Kind == SettlementKind.Village ? new Color(214, 206, 180) : new Color(246, 236, 206);
                for (int y = ly0 - 2; y <= ly0 + 8; y++)
                    for (int x = lx - 3; x <= lx + w + 2; x++)
                        if (x >= 0 && y >= 0 && x < W && y < H && !((y == ly0 - 2 || y == ly0 + 8) && (x == lx - 3 || x == lx + w + 2)))
                            px[y * W + x] = Ramps.Darken(px[y * W + x], 0.4f);
                for (int ci = 0; ci < name.Length; ci++)
                    for (int row = 0; row < 7; row++)
                        for (int col = 0; col < 5; col++)
                            if (PixelFont.Lit(name[ci], col, row)) Put(lx + ci * 6 + col, ly0 + row, ink);
                if (s.Kind != SettlementKind.Village)
                    for (int x = lx - 1; x <= lx + w; x++) { Put(x, ly0 + 9, new Color(20, 18, 26)); Put(x, ly0 + 10, ui); Put(x, ly0 + 11, new Color(20, 18, 26)); }
            }
        }
    }
}
