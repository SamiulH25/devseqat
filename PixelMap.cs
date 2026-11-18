using Microsoft.Xna.Framework;

namespace Ironfield;

/// DEMO (docs/PIXEL_MAP.md): the campaign map as flat pixel art at a fixed angle. The ground is seen from straight
/// above on one tile grid (a tile is 4 m and 12 pixels); things stand up on it in a three-quarter view, as in a
/// classic pixel-art map. Relief is only terraces: the ground is cut into steps, and each step up shows a short earth
/// face on its south side. Every house, field, tree, site and party claims whole tiles on the grid, so nothing is ever
/// laid over anything else, and parties stand only on free land. Headless:
/// `dotnet run -c Release --no-build -- --pixel-map /dir [seed]` writes each view at 640x360 and three times that.
public static partial class PixelMap
{
    public const int W = 640, H = 360, Up = 3;
    /// A tile: 4 m of ground drawn 12 pixels across.
    const int T = 12;
    const float Tm = 4, K = T / Tm;
    /// The ground rises in terraces this high; a step up shows an earth face this many pixels tall.
    const float Step = 2.2f;
    const int Face = 3;

    public static void Run(string dir, int seed)
    {
        Directory.CreateDirectory(dir);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var c = Campaign.New(seed);
        c.Map.Season = c.SeasonIndex;
        var t = c.Map.BuildTerrain();
        Console.WriteLine($"pixel map: campaign and terrain in {clock.ElapsedMilliseconds} ms");
        foreach (var (name, at) in Views(c))
        {
            clock.Restart();
            var px = new View(c, t, at).Render();
            Save(Path.Combine(dir, name + ".png"), px, W, H, Up);
            Console.WriteLine($"  {name}: {at.X:0},{at.Y:0} in {clock.ElapsedMilliseconds} ms");
        }
    }

    /// The views: where the company starts, and a town of each kind of country the world has.
    static List<(string, Vector2)> Views(Campaign c)
    {
        var list = new List<(string, Vector2)> { ("start", c.Player.Pos) };
        foreach (var kind in new[] { "Coast", "Taiga", "Desert", "Highlands", "Forest", "Farmland", "Tundra", "Steppe" })
        {
            var s = c.S.Settlements.Where(s => s.Kind != SettlementKind.Village && c.Map.ProvinceAt(s.Pos).Kind == kind
                    && (kind != "Coast" || c.Map.NearSea(s.Pos + new Vector2(0, -20))))
                .OrderBy(s => s.Kind == SettlementKind.Town ? 0 : 1).ThenBy(s => s.Id).FirstOrDefault()
                ?? c.S.Settlements.Where(s => s.Kind != SettlementKind.Village && c.Map.ProvinceAt(s.Pos).Kind == kind).FirstOrDefault();
            if (s != null) list.Add((kind.ToLowerInvariant(), s.Pos));
        }
        return list;
    }

    static void Save(string path, Color[] px, int w, int h, int up)
    {
        var big = new Color[w * up * h * up];
        for (int y = 0; y < h * up; y++)
            for (int x = 0; x < w * up; x++) big[y * w * up + x] = px[y / up * w + x / up];
        File.WriteAllBytes(path, Png.Encode(big, w * up, h * up));
        File.WriteAllBytes(Path.ChangeExtension(path, ".1x.png"), Png.Encode(px, w, h));
    }

    enum Stuff : byte { Land, Sea, Water, Road, Field, Yard }

    /// One view: the plan of the tiles (PixelMapPlan.cs), the ground, the sprites, then the labels.
    sealed partial class View
    {
        readonly Campaign c;
        readonly Terrain t;
        readonly WorldMap m;
        /// The world point at the screen's top left, on a tile corner, so tiles fall on whole pixels.
        readonly float x0, z0;
        readonly Color[] px = new Color[W * H];
        readonly Stuff[] stuff = new Stuff[W * H];
        readonly sbyte[] level = new sbyte[W * H];

        public View(Campaign c, Terrain t, Vector2 at)
        {
            this.c = c; this.t = t; m = c.Map;
            x0 = MathF.Floor((at.X - W / 2f / K) / Tm) * Tm;
            z0 = MathF.Floor((at.Y - H / 2f / K) / Tm) * Tm;
            gi0 = (int)MathF.Round(x0 / Tm) - Margin;
            gj0 = (int)MathF.Round(z0 / Tm) - Margin;
            use = new Use[GI * GJ];
            crop = new sbyte[GI * GJ];
        }

        /// Where a point of the world falls on the screen.
        Vector2 Screen(Vector2 p) => new((p.X - x0) * K, (p.Y - z0) * K);
        Vector2 World(int x, int y) => new(x0 + (x + 0.5f) / K, z0 + (y + 0.5f) / K);

        public Color[] Render()
        {
            Plan();
            Ground();
            Terraces();
            Edges();
            Sprites();
            Labels();
            return px;
        }

        static sbyte LevelOf(float h) => h < WorldMap.SeaLevel ? (sbyte)-1 : (sbyte)Math.Min(100, (int)MathF.Floor(h / Step));

        void Ground()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = World(x, y);
                    float h = t.Height(p);
                    int i = y * W + x;
                    var s = Sample(p, h);
                    px[i] = s.Colour(x, y);
                    stuff[i] = s.Stuff;
                    level[i] = s.Stuff is Stuff.Sea or Stuff.Water ? (sbyte)-1 : LevelOf(h);
                }
        }

        /// What the ground is at p.
        Sample Sample(Vector2 p, float h)
        {
            if (m.Designed && h < WorldMap.SeaLevel - 0.02f)
            {
                float depth = WorldMap.SeaLevel - h;
                return new Sample(Stuff.Sea, Ramps.Sea, 3, -Math.Clamp(MathF.Sqrt(MathF.Max(0, depth - 0.2f)) * 1.25f, 0, 3), p, Glint: depth > 0.6f);
            }
            float wet = t.WaterDepth(p);
            bool lake = false;
            foreach (var l in t.Lakes) if (l.Inside(p)) { lake = true; break; }
            if (wet > 0 || lake) return new Sample(Stuff.Water, Ramps.Fresh, wet < 0.5f && !lake ? 3 : 2, 0, p, Glint: true);
            var (ti, tj) = TileOf(p);
            var u = UseAt(ti, tj);
            if (u == Use.Place) return new Sample(Stuff.Yard, Ramps.Road, 2, 0, p, Texture: true);
            if (u == Use.Field) return FieldAt(p, ti, tj);
            if (m.RoadDist(p) < 1.0f) return new Sample(Stuff.Road, Ramps.Road, 2, 0, p);
            // The land's kind looked up a little off the point, so borders between kinds are ragged, not stepped.
            var warp = new Vector2(Terrain.Noise(p.X * 0.13f, p.Y * 0.13f, 81) - 0.5f, Terrain.Noise(p.X * 0.13f, p.Y * 0.13f, 82) - 0.5f) * 14
                + new Vector2(Terrain.Noise(p.X * 0.6f, p.Y * 0.6f, 83) - 0.5f, Terrain.Noise(p.X * 0.6f, p.Y * 0.6f, 84) - 0.5f) * 3;
            var tint = m.Tint(p + warp, Ramps.Grass);
            // Higher terraces a shade lighter, so the steps read even away from their faces.
            float lift = Math.Clamp(LevelOf(h) * 0.12f, 0, 0.5f) - 0.15f;
            return new Sample(Stuff.Land, Ramps.Land(tint), 2, lift, p, Texture: true);
        }

        /// The steps: where the ground just south of a pixel (nearer, lower on screen) lies on a lower terrace, the
        /// pixels above the edge become the step's face (earth, darkest at the foot, a lit lip of turf on top); a step
        /// to the side gets a one-pixel shade.
        void Terraces()
        {
            var earth = Ramps.Land(new Vector3(0.5f, 0.4f, 0.28f));
            var copy = (Color[])px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (stuff[i] is Stuff.Sea or Stuff.Water or Stuff.Yard) continue;
                    int lv = level[i];
                    int d = 0;
                    for (int k = 1; k <= Face && y + k < H; k++)
                        if (level[i + k * W] < lv) { d = k; break; }
                    if (d > 0)
                    {
                        bool lowIsWater = stuff[i + d * W] is Stuff.Sea or Stuff.Water;
                        copy[i] = d == Face ? Ramps.Darken(px[i], 1.12f) : earth[lowIsWater ? (d == 1 ? 0 : 1) : (d == 1 ? 1 : 2)];
                        continue;
                    }
                    if ((x > 0 && level[i - 1] < lv) || (x < W - 1 && level[i + 1] < lv)) copy[i] = Ramps.Darken(px[i], 0.8f);
                }
            Array.Copy(copy, px, px.Length);
        }

        /// Foam on water under land; darker verges beside roads.
        void Edges()
        {
            var copy = (Color[])px.Clone();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    var s = stuff[i];
                    bool wet = s is Stuff.Sea or Stuff.Water;
                    if (wet && !(stuff[i - W] is Stuff.Sea or Stuff.Water)) copy[i] = Ramps.Foam(px[i]);
                    else if (s == Stuff.Land && (stuff[i - 1] == Stuff.Road || stuff[i + 1] == Stuff.Road || stuff[i - W] == Stuff.Road || stuff[i + W] == Stuff.Road))
                        copy[i] = Ramps.Darken(px[i], 0.84f);
                }
            Array.Copy(copy, px, px.Length);
        }

        void Put(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }
    }

    /// A ground sample: its ramp, the tone it sits on, and the pixel pattern (clumps and tufts on grass, wave crests
    /// on water); tones are banded, with a little ordered dither only where two bands meet.
    readonly record struct Sample(Stuff Stuff, Color[] Ramp, int Tone, float Light, Vector2 P, bool Texture = false, bool Glint = false)
    {
        static readonly float[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public Color Colour(int x, int y)
        {
            float d = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16 - 0.5f;
            float tone = Tone + Light;
            if (Texture)
            {
                float n = Terrain.Noise(P.X * 0.45f, P.Y * 0.45f, 71) + 0.5f * Terrain.Noise(P.X * 1.3f, P.Y * 1.3f, 72);
                tone += n > 1.08f ? 0.55f : n < 0.4f ? -0.55f : 0;
                uint hsh = Hash((int)MathF.Floor(P.X * 3), (int)MathF.Floor(P.Y * 3));
                if (hsh % 23 == 0) tone += 1;
                else if (hsh % 23 == 1) tone -= 1;
            }
            if (Glint && Tone + Light > 1.1f)
            {
                float swell = Terrain.Noise(P.X * 0.05f, P.Y * 0.12f, 91) * 4;
                float row = P.Y * 0.32f + swell;
                if (row - MathF.Floor(row) < 0.09f && Hash(x / 5, (int)MathF.Floor(row)) % 3 == 0) tone += 1;
            }
            int i = Math.Clamp((int)MathF.Round(tone + d * 0.22f), 0, Ramp.Length - 1);
            return Ramp[i];
        }
    }
}
