using Microsoft.Xna.Framework;

namespace Ironfield;

/// DEMO (docs/PIXEL_MAP.md): the campaign map drawn as pixel art, option 3 of the art rework: a fixed tilted orthographic
/// view of the drawn world, every pixel the same size. The ground is drawn column by column like a height field
/// (front to back, each column filled up to the last row drawn), coloured in banded tones from hue-shifted ramps with
/// a light ordered dither at band edges; trees, places and parties stand on it as sprites painted by code at the exact
/// size they show (never scaled or turned), sorted by depth and hidden behind nearer ground. Headless:
/// `dotnet run -c Release --no-build -- --pixel-map /dir [seed]` writes each view at 640x360 and three times that.
public static partial class PixelMap
{
    /// The camera looks north from the south, tilted down this far from level (fixed: no turning).
    const float Pitch = 0.96f;
    static readonly float SinP = MathF.Sin(Pitch), CosP = MathF.Cos(Pitch);
    public const int W = 640, H = 360, Up = 3;
    /// How much the relief is drawn up for the light (the map's hills are gentle; pixel art wants them to read).
    const float Relief = 2.2f;
    /// Toward the light: from the upper left of the screen (west and north) and high, as the sprites are lit.
    static readonly Vector3 Sun = Vector3.Normalize(new Vector3(-0.62f, 0.9f, -0.5f));

    public static void Run(string dir, int seed)
    {
        Directory.CreateDirectory(dir);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var c = Campaign.New(seed);
        c.Map.Season = c.SeasonIndex;
        var t = c.Map.BuildTerrain();
        Console.WriteLine($"pixel map: campaign and terrain in {clock.ElapsedMilliseconds} ms (ground half {c.Map.GroundHalf:0})");
        var views = Views(c);
        foreach (var (name, at, k) in views)
        {
            clock.Restart();
            var px = new View(c, t, at, k).Render();
            Save(Path.Combine(dir, name + ".png"), px, W, H, Up);
            Console.WriteLine($"  {name}: {at.X:0},{at.Y:0} at {k} px/m in {clock.ElapsedMilliseconds} ms");
        }
    }

    /// The views: where the company starts, a town of each kind of country the world has, and a wide look.
    static List<(string, Vector2, float)> Views(Campaign c)
    {
        var list = new List<(string, Vector2, float)> { ("start", c.Player.Pos, 3) };
        var seen = new HashSet<string>();
        foreach (var kind in new[] { "Coast", "Taiga", "Desert", "Highlands", "Forest", "Farmland", "Tundra", "Steppe" })
        {
            var s = c.S.Settlements.Where(s => s.Kind != SettlementKind.Village && c.Map.ProvinceAt(s.Pos).Kind == kind
                    && (kind != "Coast" || c.Map.NearSea(s.Pos + new Vector2(0, -20))))
                .OrderBy(s => s.Kind == SettlementKind.Town ? 0 : 1).ThenBy(s => s.Id).FirstOrDefault()
                ?? c.S.Settlements.Where(s => s.Kind != SettlementKind.Village && c.Map.ProvinceAt(s.Pos).Kind == kind).FirstOrDefault();
            if (s == null || !seen.Add(kind)) continue;
            list.Add((kind.ToLowerInvariant(), s.Pos + new Vector2(0, -6), 3));
        }
        list.Add(("wide", c.Player.Pos, 1.5f));
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

    // ---------- the ground ----------

    enum Stuff : byte { Land, Sea, Water, Road, Field }

    /// One view: the ground first (keeping, per pixel, how near it is and what it is), then the sprites, then the labels.
    sealed partial class View
    {
        readonly Campaign c;
        readonly Terrain t;
        readonly WorldMap m;
        readonly float k, x0, oy;
        readonly Color[] px = new Color[W * H];
        readonly float[] near = new float[W * H];   // the world Z of the ground drawn at each pixel (bigger = nearer)
        readonly Stuff[] stuff = new Stuff[W * H];
        readonly List<FieldPlot>[] plotGrid;
        const float PlotCell = 16;
        readonly int plotSide;

        public View(Campaign c, Terrain t, Vector2 at, float k)
        {
            this.c = c; this.t = t; m = c.Map; this.k = k;
            x0 = at.X - W / 2f / k;
            oy = (at.Y * SinP - t.Height(at) * CosP) * k - H / 2f;
            plotSide = (int)MathF.Ceiling(2 * t.Half / PlotCell) + 1;
            plotGrid = new List<FieldPlot>[plotSide * plotSide];
            foreach (var f in t.Fields)
            {
                float r = MathF.Max(f.Half.X, f.Half.Y) + 1;
                for (float gx = f.Center.X - r; gx <= f.Center.X + r + PlotCell; gx += PlotCell)
                    for (float gz = f.Center.Y - r; gz <= f.Center.Y + r + PlotCell; gz += PlotCell)
                        if (PlotCellOf(new Vector2(gx, gz)) is int i) (plotGrid[i] ??= new()).Add(f);
            }
        }

        int? PlotCellOf(Vector2 p)
        {
            int gx = (int)((p.X + t.Half) / PlotCell), gz = (int)((p.Y + t.Half) / PlotCell);
            return gx < 0 || gz < 0 || gx >= plotSide || gz >= plotSide ? null : gz * plotSide + gx;
        }

        /// Where a point of the world falls on the screen (float pixels).
        public Vector2 Screen(Vector2 p, float h) => new((p.X - x0) * k, (p.Y * SinP - h * CosP) * k - oy);

        public Color[] Render()
        {
            Ground();
            Edges();
            Sprites();
            Labels();
            return px;
        }

        void Ground()
        {
            float zNear = (oy + H) / (k * SinP) + 12 * CosP / SinP + 2, zFar = oy / (k * SinP) - 40 * CosP / SinP - 2;
            float dz = 0.5f / (k * SinP);
            for (int x = 0; x < W; x++)
            {
                float wx = x0 + (x + 0.5f) / k;
                int top = H;
                for (float z = zNear; z > zFar && top > 0; z -= dz)
                {
                    var p = new Vector2(wx, z);
                    float h = t.Height(p);
                    bool sea = m.Designed && h < WorldMap.SeaLevel - 0.02f;
                    float hs = sea ? WorldMap.SeaLevel : h;
                    int sy = (int)MathF.Floor((z * SinP - hs * CosP) * k - oy);
                    if (sy >= top) continue;
                    var s = Sample(p, h, sea);
                    for (int y = Math.Max(0, sy); y < top; y++)
                    {
                        px[y * W + x] = s.Colour(x, y);
                        near[y * W + x] = z;
                        stuff[y * W + x] = s.Stuff;
                    }
                    top = Math.Max(0, sy);
                }
                for (int y = 0; y < top; y++) { px[y * W + x] = SkyHaze; near[y * W + x] = float.MinValue; stuff[y * W + x] = Stuff.Sea; }
            }
        }

        static readonly Color SkyHaze = new(30, 48, 70);

        /// What the ground is at p and how it is lit.
        Sample Sample(Vector2 p, float h, bool sea)
        {
            if (sea)
            {
                float depth = WorldMap.SeaLevel - h;
                // Shallows to the deep in bands (dithered where they meet): tone 3 at the shore down to 0 offshore.
                return new Sample(Stuff.Sea, Ramps.Sea, 3, -Math.Clamp(MathF.Sqrt(MathF.Max(0, depth - 0.2f)) * 1.25f, 0, 3), p, Glint: depth > 0.6f);
            }
            float wet = t.WaterDepth(p);
            bool lake = false;
            foreach (var l in t.Lakes) if (l.Inside(p)) { lake = true; break; }
            if (wet > 0 || lake) return new Sample(Stuff.Water, Ramps.Fresh, wet < 0.5f && !lake ? 3 : 2, 0, p, Glint: true);

            // The light: the slope against the sun, banded later.
            float e = 0.8f;
            float hx = (t.Height(p + new Vector2(e, 0)) - t.Height(p - new Vector2(e, 0))) / (2 * e) * Relief;
            float hz = (t.Height(p + new Vector2(0, e)) - t.Height(p - new Vector2(0, e))) / (2 * e) * Relief;
            var n = Vector3.Normalize(new Vector3(-hx, 1, -hz));
            float flat = Vector3.Dot(Vector3.Up, Sun);
            float light = (Vector3.Dot(n, Sun) - flat) * 5.5f; // 0 on the level, + toward the sun, - away

            if (m.RoadDist(p) < 0.85f) return new Sample(Stuff.Road, Ramps.Road, 2, light * 0.7f, p);
            if (PlotCellOf(p) is int pc && plotGrid[pc] is { } plots)
                foreach (var f in plots)
                {
                    var ax = SnapAxis(f.Axis);
                    var d = p - f.Center;
                    float u = Vector2.Dot(d, ax), v = d.X * -ax.Y + d.Y * ax.X;
                    if (MathF.Abs(u) < f.Half.X && MathF.Abs(v) < f.Half.Y)
                    {
                        bool hedge = f.Hedged != 0 && f.Kind != FieldKind.Pasture && (MathF.Abs(u) > f.Half.X - 0.45f || MathF.Abs(v) > f.Half.Y - 0.45f);
                        if (hedge) return new Sample(Stuff.Field, Ramps.Hedge, 1, light, p);
                        int stripe = f.Kind is FieldKind.Ploughed or FieldKind.Grain or FieldKind.Green ? ((int)MathF.Floor(v * k / 2.4f) & 1) : 0;
                        return new Sample(Stuff.Field, Ramps.Field(f.Kind, f.Seed, m.Season), 2 - stripe, light, p,
                            Texture: f.Kind is FieldKind.Pasture or FieldKind.Fallow or FieldKind.Orchard); // grass in clumps, not a flat sheet
                    }
                }
            // The land's kind is looked up a little off the point (a warp of a few metres), so the border between two kinds
            // of country is a ragged natural edge rather than the stair steps of the province grid.
            var warp = new Vector2(Terrain.Noise(p.X * 0.13f, p.Y * 0.13f, 81) - 0.5f, Terrain.Noise(p.X * 0.13f, p.Y * 0.13f, 82) - 0.5f) * 14
                + new Vector2(Terrain.Noise(p.X * 0.6f, p.Y * 0.6f, 83) - 0.5f, Terrain.Noise(p.X * 0.6f, p.Y * 0.6f, 84) - 0.5f) * 3;
            var tint = m.Tint(p + warp, Ramps.Grass);
            return new Sample(Stuff.Land, Ramps.Land(tint), 2, light, p, Texture: true);
        }

        /// A plot's long side turned to the nearest angle pixel art draws cleanly (level, upright, 1:2, 1:1, 2:1), so its
        /// edges are even steps rather than ragged ones.
        static Vector2 SnapAxis(Vector2 a)
        {
            float ang = MathF.Atan2(a.Y, a.X), best = 0, err = float.MaxValue;
            foreach (float s in new[] { 0f, 26.565f, 45f, 63.435f, 90f, 116.565f, 135f, 153.435f, 180f, -26.565f, -45f, -63.435f, -90f, -116.565f, -135f, -153.435f, -180f })
            {
                float r = MathHelper.ToRadians(s), e = MathF.Abs(MathHelper.WrapAngle(ang - r));
                if (e < err) { err = e; best = r; }
            }
            return new Vector2(MathF.Cos(best), MathF.Sin(best));
        }

        /// Banks and verges: land against water takes its darkest tone (the bank's face), water just under land a pale
        /// line of foam; a road's edge darkens the grass beside it.
        void Edges()
        {
            var copy = (Color[])px.Clone();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    var s = stuff[i];
                    bool wet = s is Stuff.Sea or Stuff.Water;
                    if (near[i] == float.MinValue) continue;
                    if (!wet && (stuff[i + W] is Stuff.Sea or Stuff.Water) && near[i + W] != float.MinValue)
                        copy[i] = Ramps.Darken(px[i], 0.62f); // the bank's face above the water
                    else if (wet && !(stuff[i - W] is Stuff.Sea or Stuff.Water) && near[i - W] != float.MinValue)
                        copy[i] = Ramps.Foam(px[i]);
                    else if (s == Stuff.Land && (stuff[i - 1] == Stuff.Road || stuff[i + 1] == Stuff.Road || stuff[i - W] == Stuff.Road || stuff[i + W] == Stuff.Road))
                        copy[i] = Ramps.Darken(px[i], 0.84f);
                }
            Array.Copy(copy, px, px.Length);
        }
    }

    /// A ground sample: its ramp, the tone it sits on, the light on it, and the pixel pattern (ordered dither at band
    /// edges, clumps and tufts on grass, glints on water).
    readonly record struct Sample(Stuff Stuff, Color[] Ramp, int Tone, float Light, Vector2 P, bool Texture = false, bool Glint = false)
    {
        static readonly float[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public Color Colour(int x, int y)
        {
            float d = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16 - 0.5f;
            float tone = Tone + Light;
            if (Texture)
            {
                // Clumps a couple of metres across, a tone up or down; sparse tufts (a light pixel over a dark one).
                float n = Terrain.Noise(P.X * 0.45f, P.Y * 0.45f, 71) + 0.5f * Terrain.Noise(P.X * 1.3f, P.Y * 1.3f, 72);
                tone += n > 1.08f ? 0.55f : n < 0.4f ? -0.55f : 0;
                uint hsh = Hash((int)MathF.Floor(P.X * 3), (int)MathF.Floor(P.Y * 3));
                if (hsh % 23 == 0) tone += 1;
                else if (hsh % 23 == 1) tone -= 1;
            }
            if (Glint && Tone + Light > 1.1f)
            {
                // Wave crests: short broken dashes a tone lighter along rows that wander with the swell, not specks.
                float swell = Terrain.Noise(P.X * 0.05f, P.Y * 0.12f, 91) * 4;
                float row = P.Y * 0.32f + swell;
                if (row - MathF.Floor(row) < 0.09f && Hash(x / 5, (int)MathF.Floor(row)) % 3 == 0) tone += 1;
            }
            // Banded: dither only in a narrow seam where two bands meet.
            int i = Math.Clamp((int)MathF.Round(tone + d * 0.22f), 0, Ramp.Length - 1);
            return Ramp[i];
        }
    }

    static uint Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }
}
