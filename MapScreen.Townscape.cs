using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the town seen from the tavern's door, the tavern tab's picture. Drawing only.
/// What the tavern's picture of a town is made of: how its people build (timber, white, log, tent), the country round it
/// (a province kind), its water (canal, river or none), snow in winter, and the hour.
readonly record struct TownLook(string Culture, string Build, string Country, string Water, bool Snow, bool Night, bool Dusk)
{
    public bool Day => !Night && !Dusk;
}

/// The colours of one town's picture (`MapScreen.ColoursOf`).
readonly record struct TownColours(Color SkyTop, Color SkyLow, Color Far, Color Mid, Color Near, Color Wall, Color Roof, Color Ground, Color Snow, Color Lit, Color Rim);

partial class MapScreen
{
    /// What the picture of a town is made of: its people's way of building, the country, water, snow, the hour.
    TownLook LookOf(Settlement s, float hour)
    {
        string build = s.Culture switch { "southern" => "white", "northern" => "log", "steppe" => "tent", _ => "timber" };
        string country = s.Culture == "steppe" ? "Steppe" : C.S.Provinces[s.Province].Kind; // tents stand on open ground
        string water = s.Culture == "league" ? "canal" : country == "Marsh" || C.Map.RiverDist(s.Pos) < 10 * C.Map.Spread ? "river" : "";
        bool night = hour is >= 20 or < 5;
        return new TownLook(s.Culture, build, country, water, C.Winter, night, !night && hour >= 17);
    }

    /// The colours of a town's picture: the sky by the hour, warmed by the south and cooled by snow; walls and roofs by how its people build.
    static TownColours ColoursOf(TownLook l)
    {
        bool hot = l.Country == "Badlands" || l.Culture == "southern", open = l.Country == "Steppe";
        Color top, low, far, mid, near;
        bool cold = l.Snow || l.Build == "log" || l.Country == "Forest";
        if (l.Night)
        {
            top = Kit.Night;
            low = hot ? new(100, 54, 58) : cold ? new(44, 54, 92) : open ? new(64, 44, 80) : new(84, 40, 56);
            far = hot ? new(96, 58, 72) : cold ? new(60, 60, 96) : new(86, 52, 80);
            mid = hot ? new(70, 46, 66) : cold ? new(44, 46, 80) : new(60, 42, 76);
            near = cold ? new(30, 30, 52) : new(40, 29, 48);
        }
        else if (l.Dusk) { top = new(48, 40, 78); low = hot ? new(236, 136, 72) : new(214, 118, 68); far = new(132, 88, 100); mid = new(94, 66, 88); near = new(58, 42, 56); }
        else
        {
            top = hot ? new(96, 140, 186) : l.Snow ? new(104, 140, 190) : open ? new(80, 130, 196) : new(70, 120, 186);
            low = hot ? new(238, 210, 156) : l.Snow ? new(214, 226, 240) : open ? new(230, 220, 168) : new(228, 218, 186);
            far = hot ? new(190, 158, 120) : open ? new(168, 166, 118) : l.Snow ? new(178, 186, 204) : new(140, 146, 156);
            mid = hot ? new(170, 140, 110) : new(118, 112, 120); near = new(74, 64, 64);
        }
        if (l.Country == "Marsh" && !l.Night) far = Color.Lerp(far, new Color(130, 150, 128), 0.4f);
        var (wall, roof) = l.Build switch
        {
            "white" => (new Color(234, 226, 206), new Color(206, 190, 160)),
            "log" => (new Color(128, 90, 60), new Color(96, 108, 64)),
            "tent" => (new Color(210, 198, 170), new Color(180, 152, 112)),
            _ => (new Color(206, 188, 158), l.Culture switch
            {
                "azure" => new Color(72, 100, 152), "crimson" => new Color(160, 64, 50), "order" => new Color(104, 104, 114),
                "league" => new Color(134, 70, 52), _ => new Color(122, 86, 60),
            }),
        };
        float k = l.Night ? (l.Build == "white" ? 0.24f : 0.14f) : l.Dusk ? 0.5f : 1f; // walls take the light of the hour
        wall = Color.Lerp(near, wall, k);
        roof = Color.Lerp(near, roof, l.Night ? 0.22f : k * 0.9f);
        if (l.Snow && l.Day) wall = Color.Lerp(wall, new Color(200, 210, 230), 0.12f);
        Color ground = l.Snow ? (l.Night ? new(116, 122, 154) : l.Dusk ? new(206, 180, 176) : new(232, 236, 244))
            : hot ? Color.Lerp(near, new Color(204, 174, 124), k) : open ? Color.Lerp(near, new Color(156, 146, 94), k) : Color.Lerp(Kit.Mul(near, 0.8f), new Color(120, 104, 84), k * 0.8f);
        Color snow = l.Night ? new(176, 182, 210) : l.Dusk ? new(238, 208, 198) : new(244, 246, 252);
        Color rim = l.Night ? new(150, 128, 170) : l.Dusk ? new(250, 172, 112) : new(255, 238, 200);
        return new TownColours(top, low, far, mid, near, wall, roof, ground, snow, new Color(255, 184, 92), rim);
    }

    /// The town at the hour it is (the tavern tab's picture): sky, the country beyond, the upper town, its wall and landmark,
    /// the houses as its people build them, the tavern in front, water if it has any, snow in winter. Seeded by the town,
    /// so a town always looks the same.
    void Townscape(Rectangle p, Settlement s, float hour)
    {
        float t = Environment.TickCount64 / 1000f;
        var l = LookOf(s, hour);
        var c = ColoursOf(l);
        var rng = new Random(s.Id * 7919 + 13);
        var cloth = Kit.Mul(Style.Sides[Style.PaletteOf(s.Faction)].Cloth, l.Night ? 0.5f : 0.9f);
        int ground = p.Bottom - ui.U(l.Water != "" ? 20 : 10);
        ui.VGrad(p, c.SkyTop, c.SkyLow, 2);
        if (l.Night) ui.Stars(new Rectangle(p.X, p.Y, p.Width, p.Height / 2), t, l.Country == "Steppe" ? 110 : 40);
        if (l.Night && l.Country == "Steppe") // the open steppe sky: the river of stars across it
        {
            var band = new Random(s.Id * 11 + 2);
            for (int i = 0; i < 140; i++)
            {
                float f = band.NextSingle();
                int sx = p.X + (int)(p.Width * f), sy = p.Y + (int)(p.Height * (0.6f - f * 0.5f) + (band.NextSingle() - 0.5f) * ui.U(22));
                ui.Rect(new Rectangle(sx, sy, Math.Max(1, ui.U(1)), Math.Max(1, ui.U(1))), new Color(190, 186, 220) * (0.25f + band.NextSingle() * 0.35f));
            }
        }
        if (!l.Night) Clouds(p, l, c, t, s.Id);
        var place = new Random(s.Id * 31 + 7); // where the tavern, the keep and the moon stand, by town
        bool midTavern = place.Next(2) == 0;
        int tavernX = midTavern ? p.X + (int)(p.Width * (0.28f + place.NextSingle() * 0.16f)) : p.X + ui.U(12);
        float keepAt = 0.2f + place.NextSingle() * 0.3f, orbAt = 0.74f + place.NextSingle() * 0.16f; // the moon clear of the tavern's roof
        Orb(p, l, orbAt);
        snowCap = l.Snow ? c.Snow : Color.Transparent;
        Country(p, l, c, ground, rng);
        if (l.Build != "tent") UpperTown(p, s, ground - ui.U(30), c.Far, l.Night, c.Lit, rng, l.Build == "white", keepAt);
        if (!l.Day) ui.VGrad(new Rectangle(p.X, ground - ui.U(56), p.Width, ui.U(26)), c.Lit * 0f, c.Lit * (l.Night ? 0.12f : 0.08f), 2);
        rimLight = c.Rim;
        if (l.Build == "log") Palisade(p, ground - ui.U(28), c.Mid);
        else if (l.Build != "tent") TownWall(p, ground - ui.U(30), l.Build == "white" ? Color.Lerp(c.Mid, c.Wall, 0.4f) : c.Mid, l.Night, c.Lit, cloth, rng);
        Landmark(p, s.Culture, ground, c.Mid, l.Night, c.Lit, cloth, t);
        // A back row of houses, darker and higher, behind the front row: the town has depth.
        var back = c with { Wall = Color.Lerp(c.Wall, c.Mid, 0.6f), Roof = Color.Lerp(c.Roof, c.Mid, 0.6f) };
        HouseRow(p.X, p.Right, ground - ui.U(6), l, back, new Random(s.Id * 13 + 5), t, false);
        Ground(p, l, c, ground, t);
        var lights = new List<(int X, int W)>();
        if (tavernX - p.X > ui.U(40)) lights.AddRange(HouseRow(p.X + ui.U(4), tavernX - ui.U(14), ground, l, c, rng, t, true));
        lights.AddRange(HouseRow(tavernX + ui.U(196), p.Right - ui.U(6), ground, l, c, rng, t, true));
        if (l.Build == "tent") Herd(p, ground, c, s.Id, tavernX);
        if (l.Water == "") Folk(p, l, c, ground, s.Id, tavernX);
        lights.AddRange(Tavern(new Rectangle(tavernX, ground - ui.U(64), ui.U(160), ui.U(64)), l, c, cloth, t));
        rimLight = Color.Transparent;
        if (l.Water != "") Water(p, l, c, ground, lights, t);
        if (l.Snow) Snowfall(p, t, l.Night);
        snowCap = Color.Transparent;
    }

    /// The moon (a hard pixel disc in stepped halo bands, with its seas) or the sun, low over the town.
    void Orb(Rectangle p, TownLook l, float at)
    {
        var orb = new Vector2(p.X + p.Width * at, p.Y + p.Height * (l.Dusk ? 0.42f : 0.2f));
        var oc = l.Night ? new Color(236, 228, 204) : l.Dusk ? new Color(255, 180, 100) : new Color(255, 226, 150);
        if (!l.Night) // the sun: bigger, warmer, its glow wide and soft
        {
            ui.Disc(orb, ui.U(24), oc * 0.05f); ui.Disc(orb, ui.U(18), oc * 0.09f); ui.Disc(orb, ui.U(13), oc * 0.16f);
            ui.Disc(orb, ui.U(10), oc); ui.Disc(orb - new Vector2(ui.U(2), ui.U(2)), ui.U(5), new Color(255, 246, 214));
            return;
        }
        ui.Disc(orb, ui.U(11), oc * 0.07f); ui.Disc(orb, ui.U(9), oc * 0.12f); ui.Disc(orb, ui.U(7), oc);
        if (l.Night) { ui.Disc(orb + new Vector2(ui.U(2), -ui.U(2)), ui.U(2), Kit.Mul(oc, 0.85f)); ui.Disc(orb + new Vector2(-ui.U(3), ui.U(2)), ui.U(1), Kit.Mul(oc, 0.88f)); }
    }

    /// The country beyond the town, faint with distance: hills, forest, open steppe, the badlands' mesas, the fens' haze.
    void Country(Rectangle p, TownLook l, TownColours c, int ground, Random rng)
    {
        var col = Color.Lerp(c.Far, c.SkyLow, 0.45f);
        int y = ground - ui.U(44);
        switch (l.Country)
        {
            case "Hills": ui.Hills(p, y - ui.U(8), ui.U(16), 1.7f, col); break;
            case "Steppe": // long ridges, one behind another, the far one blue with distance
                ui.Hills(p, ground - ui.U(66), ui.U(10), 0.9f, Color.Lerp(col, c.SkyLow, 0.5f));
                ui.Hills(p, ground - ui.U(50), ui.U(8), 0.6f, Color.Lerp(col, c.SkyLow, 0.25f));
                ui.Hills(p, ground - ui.U(34), ui.U(6), 0.4f, col);
                break;
            case "Badlands":
                ui.Hills(p, y + ui.U(6), ui.U(4), 2.3f, col);
                foreach (var (fx, fw, fh) in new[] { (0.12f, 90, 12), (0.52f, 130, 9), (0.82f, 70, 14) }) // flat-topped mesas, steep sides, faint
                {
                    int mx = p.X + (int)(p.Width * fx), my = y + ui.U(6) - ui.U(fh);
                    var mc = Color.Lerp(col, c.SkyLow, 0.25f);
                    for (int k = 0; k < ui.U(fh); k += Math.Max(1, ui.U(1))) ui.Rect(new Rectangle(mx - k / 6, my + k, ui.U(fw) + k / 3, Math.Max(1, ui.U(1))), mc);
                    ui.Rect(new Rectangle(mx, my, ui.U(fw), ui.U(1)), Color.Lerp(mc, c.Rim, 0.3f));
                }
                break;
            case "Forest":
                ui.Hills(p, y, ui.U(6), 1.1f, col);
                for (int x = p.X; x < p.Right; x += ui.U(7 + rng.Next(5)))
                    Pine(x, ui.HillY(p, x, y, ui.U(6), 1.1f) + ui.U(2), ui.U(10 + rng.Next(8)), col);
                break;
            default: ui.Hills(p, y, ui.U(6), 2.1f, col); break;
        }
        if (l.Country == "Marsh") ui.VGrad(new Rectangle(p.X, ground - ui.U(30), p.Width, ui.U(30)), c.SkyLow * 0f, Color.Lerp(c.SkyLow, Color.White, 0.2f) * 0.35f, 2);
    }

    /// Slow pixel clouds by day and at dusk, lit from below at dusk; placed by the town, drifting.
    void Clouds(Rectangle p, TownLook l, TownColours c, float t, int seed)
    {
        var rng = new Random(seed * 3 + 1);
        var col = l.Dusk ? new Color(236, 150, 120) : Color.Lerp(c.SkyLow, Color.White, 0.6f);
        int n = l.Country == "Steppe" ? 5 : 3;
        for (int i = 0; i < n; i++)
        {
            float span = p.Width + ui.U(120);
            int cx = p.X - ui.U(60) + (int)((rng.NextSingle() * span + t * ui.U(2 + i)) % span), cy = p.Y + (int)(p.Height * (0.12f + rng.NextSingle() * 0.3f));
            int w = ui.U(30 + rng.Next(40));
            void Puff(Rectangle r, Color k) { r = Rectangle.Intersect(r, p); if (r.Width > 0) ui.Rect(r, k); } // kept inside the picture
            Puff(new Rectangle(cx, cy, w, ui.U(3)), col * 0.55f);
            Puff(new Rectangle(cx + w / 5, cy - ui.U(3), w * 3 / 5, ui.U(3)), col * 0.45f);
            Puff(new Rectangle(cx + w / 3, cy - ui.U(5), w / 4, ui.U(2)), col * 0.35f);
            Puff(new Rectangle(cx + ui.U(4), cy + ui.U(3), w - ui.U(8), ui.U(1)), Kit.Mul(col, 0.85f) * 0.35f);
        }
    }

    /// A fir: a stack of widening tiers.
    void Pine(int x, int baseY, int h, Color c)
    {
        int s = Math.Max(1, ui.U(1));
        for (int k = 0; k < h; k += s)
        {
            int w = (int)((h - k) * 0.45f) + (k % ui.U(4) < ui.U(1) ? ui.U(1) : 0);
            ui.Rect(new Rectangle(x - w / 2, baseY - k, Math.Max(s, w), s), snowCap.A > 0 && k % ui.U(4) == 0 ? Color.Lerp(c, snowCap, 0.4f) : c);
        }
    }

    /// The northern town's palisade: a line of pointed stakes, a gate tower here and there.
    void Palisade(Rectangle p, int top, Color c)
    {
        ui.Rect(new Rectangle(p.X, top, p.Width, p.Bottom - top), c);
        for (int x = p.X; x < p.Right; x += ui.U(4))
        {
            ui.Rect(new Rectangle(x, top - ui.U(3), ui.U(3), ui.U(3)), c);
            ui.Rect(new Rectangle(x + ui.U(1), top - ui.U(5), ui.U(1), ui.U(2)), c);
        }
        for (int x = p.X + ui.U(90); x < p.Right - ui.U(30); x += ui.U(200))
        {
            ui.Rect(new Rectangle(x, top - ui.U(16), ui.U(14), ui.U(16)), c);
            Gable(x - ui.U(2), top - ui.U(16), ui.U(18), 0, ui.U(8), c);
        }
    }

    /// The ground before the houses: a street, the canal's quay, or a river bank with reeds; snow in winter.
    void Ground(Rectangle p, TownLook l, TownColours c, int ground, float t)
    {
        var street = new Rectangle(p.X, ground, p.Width, p.Bottom - ground);
        if (l.Water == "")
        {
            ui.Rect(street, c.Ground);
            for (int i = 0; i < 12; i++) ui.Rect(new Rectangle(p.X + (int)(p.Width * ((i * 0.37f) % 1)), ground + ui.U(3 + i % 3 * 2), ui.U(10 + i % 3 * 6), ui.U(1)), Kit.Mul(c.Ground, 0.85f)); // ruts
            return;
        }
        int bank = l.Water == "river" ? ui.U(4) : 0;
        if (bank > 0) ui.Rect(new Rectangle(p.X, ground, p.Width, bank), c.Ground);
        var water = new Rectangle(p.X, ground + bank, p.Width, street.Height - bank);
        Color w1 = l.Night ? new(36, 44, 80) : l.Dusk ? new(120, 92, 110) : l.Snow ? new(120, 140, 160) : new(96, 128, 150);
        ui.VGrad(water, w1, Kit.Mul(w1, 0.45f), 2);
        if (l.Snow && !l.Night) ui.Rect(new Rectangle(p.X, water.Y, p.Width, ui.U(2)), Color.Lerp(w1, Color.White, 0.5f)); // ice along the edge
        var ripple = Color.Lerp(w1, l.Night ? new Color(110, 120, 170) : Color.White, 0.45f);
        for (int i = 0; i < 26; i++)
        {
            int ry = water.Y + ui.U(4 + i % 5 * 3), rx = p.X + (int)(((i * 0.381f + t * 0.01f * (1 + i % 3)) % 1) * p.Width);
            if (ry < water.Bottom) ui.Rect(new Rectangle(rx, ry, ui.U(6 + i % 4 * 3), ui.U(1)), ripple * 0.7f);
        }
    }

    /// The water's front: the quay edge or the reeds, every light trembling in it, and a boat.
    void Water(Rectangle p, TownLook l, TownColours c, int ground, List<(int X, int W)> lights, float t)
    {
        int top = ground + (l.Water == "river" ? ui.U(4) : 0);
        if (l.Water == "canal")
        {
            ui.Rect(new Rectangle(p.X, ground, p.Width, ui.U(1)), c.Rim);
            ui.Rect(new Rectangle(p.X, ground + ui.U(1), p.Width, ui.U(1)), Color.Black * 0.35f);
        }
        else
            for (int x = p.X + ui.U(4); x < p.Right; x += ui.U(9)) // reeds along the bank
                if ((x / Math.Max(1, ui.U(1))) % 5 < 2)
                    for (int k = 0; k < 3; k++) ui.Rect(new Rectangle(x + k * ui.U(2), top - ui.U(4 + k % 2 * 3), ui.U(1), ui.U(6 + k % 2 * 3)), Kit.Mul(c.Near, 1.2f));
        // Each lit window in the water: a few short warm streaks under it, shorter and fainter with depth, dark water between.
        if (!l.Day)
            foreach (var (lx, lw) in lights)
                for (int k = 0; k < 4; k++)
                {
                    int w = Math.Max(ui.U(2), (int)(lw * (1 - k * 0.18f))), y = top + ui.U(3 + k * 4);
                    if (y >= p.Bottom - ui.U(2)) break;
                    int dx = ((lx / Math.Max(1, ui.U(1)) * 7 + k * 5) % 3 - 1) * ui.U(1) + (int)MathF.Round(MathF.Sin(t * 1.4f + lx * 0.1f + k) * 0.6f) * ui.U(1);
                    ui.Rect(new Rectangle(lx + (lw - w) / 2 + dx, y, w, ui.U(1)), c.Lit * (0.6f - k * 0.13f));
                }
        Boat(p.X + (int)(p.Width * 0.72f), top + ui.U(10), l, c, t);
    }

    /// A boat at its mooring: a barge with a cabin on a canal, a rowing boat on a river; a lamp at night.
    void Boat(int x, int water, TownLook l, TownColours c, float t)
    {
        int bob = (int)MathF.Round(MathF.Sin(t * 1.1f) * ui.U(1)), y = water + bob, s = Math.Max(1, ui.U(1)), len = ui.U(l.Water == "canal" ? 56 : 30);
        var hull = l.Night ? new Color(70, 52, 70) : new Color(96, 66, 46);
        for (int k = 0; k < ui.U(5); k += s) ui.Rect(new Rectangle(x + k * 2, y + k, len - k * 4, s), k == 0 ? Color.Lerp(hull, c.Rim, 0.5f) : hull);
        ui.Rect(new Rectangle(x + ui.U(2), y + ui.U(5), len - ui.U(4), ui.U(1)), Color.Black * 0.3f);
        if (l.Water == "canal")
        {
            ui.Rect(new Rectangle(x + ui.U(30), y - ui.U(7), ui.U(16), ui.U(7)), hull);
            ui.Rect(new Rectangle(x + ui.U(30), y - ui.U(8), ui.U(16), ui.U(1)), Color.Lerp(hull, c.Rim, 0.5f));
            ui.Rect(new Rectangle(x + ui.U(34), y - ui.U(5), ui.U(3), ui.U(2)), l.Day ? Kit.Mul(hull, 0.6f) : c.Lit);
        }
        else ui.Line(new Vector2(x + ui.U(8), y - ui.U(1)), new Vector2(x + ui.U(24), y + ui.U(6)), hull, 1); // an oar
        if (l.Day) return;
        ui.Rect(new Rectangle(x + ui.U(4), y - ui.U(9), ui.U(1), ui.U(9)), hull);
        ui.Disc(new Vector2(x + ui.U(4), y - ui.U(10)), ui.U(5), c.Lit * 0.2f);
        ui.Rect(new Rectangle(x + ui.U(3), y - ui.U(11), ui.U(3), ui.U(3)), new Color(255, 226, 150));
        for (int k = 0; k < ui.U(6); k += ui.U(2)) ui.Rect(new Rectangle(x + ui.U(3), y + ui.U(6) + k, ui.U(2), ui.U(2)), c.Lit * (0.4f * (1 - k / (float)ui.U(6))));
    }

    /// Snow falling over the town.
    void Snowfall(Rectangle p, float t, bool night)
    {
        var col = night ? new Color(200, 206, 230) : Color.White;
        for (int i = 0; i < 70; i++)
        {
            float fx = (i * 0.618f + t * 0.02f * (1 + i % 3) + MathF.Sin(t * 0.7f + i) * 0.01f) % 1, fy = (i * 0.37f + t * 0.06f * (1 + i % 2)) % 1;
            int sz = i % 5 == 0 ? ui.U(2) : ui.U(1);
            ui.Rect(new Rectangle(p.X + (int)(fx * p.Width), p.Y + (int)(fy * p.Height), sz, sz), col * 0.8f);
        }
    }

    /// Set while the back row of houses is drawn: no smoke from it.
    bool backRow;

    /// Snow on the roofs (set while the town is drawn in winter): `Gable` and `Dome` cap their tops with it.
    Color snowCap = Color.Transparent;

    /// Edge light from the moon or sun for the near shapes (`Gable`, `StepGable`, `Crane` draw it when set).
    Color rimLight = Color.Transparent;

    /// The old town climbing the hill behind the wall to its keep, faint with distance; a few dim lights at night.
    void UpperTown(Rectangle p, Settlement town, int wallTop, Color c, bool night, Color lit, Random rng, bool flat, float keepAt)
    {
        int crest = p.X + (int)(p.Width * keepAt), peak = p.Y + (int)(p.Height * 0.3f), s = Math.Max(1, ui.U(2));
        // The hill: highest at the keep, falling away to the wall on both sides.
        for (int x = p.X; x < p.Right; x += s)
        {
            float d = Math.Abs(x - crest) / (p.Width * 0.62f);
            int top = (int)MathHelper.Lerp(peak + ui.U(14), wallTop - ui.U(4), MathF.Min(1, d * 1.5f + 0.03f * MathF.Sin(x * 0.05f)));
            ui.Rect(new Rectangle(x, top, s, wallTop - top), c);
            ui.Rect(new Rectangle(x, top, s, Math.Max(1, ui.U(1))), Kit.Mul(c, 0.8f));
        }
        // Roofs stepping down the slope.
        for (int x = p.X + ui.U(20); x < p.Right - ui.U(20); x += ui.U(14 + rng.Next(10)))
        {
            float d = Math.Abs(x - crest) / (p.Width * 0.62f);
            if (d > 0.6f || d < 0.04f) continue;
            int top = (int)MathHelper.Lerp(peak + ui.U(14), wallTop - ui.U(4), MathF.Min(1, d * 1.5f)) + ui.U(rng.Next(3));
            int w = ui.U(10 + rng.Next(6)), h = ui.U(6 + rng.Next(5));
            bool on = rng.Next(4) == 0 && night;
            if (flat) ui.Rect(new Rectangle(x, top + ui.U(2) - h - ui.U(3), w, h + ui.U(3)), c); // white towns: flat roofs
            else Gable(x, top + ui.U(2), w, h, w / 3, c);
            if (on) ui.Rect(new Rectangle(x + w / 2 - ui.U(1), top - h + ui.U(4), ui.U(2), ui.U(2)), lit * 0.4f);
        }
        // The keep on the crest, its banner over the town.
        int kx = crest - ui.U(12), ky = peak - ui.U(9);
        ui.Rect(new Rectangle(kx, ky, ui.U(24), ui.U(27)), c);
        for (int k = 0; k < ui.U(24); k += ui.U(6)) ui.Rect(new Rectangle(kx + k, ky - ui.U(3), ui.U(4), ui.U(3)), Kit.Mul(c, 1.25f));
        ui.Rect(new Rectangle(kx, ky, ui.U(24), ui.U(1)), Kit.Mul(c, 1.25f));
        ui.Rect(new Rectangle(kx + ui.U(18), ky - ui.U(12), ui.U(8), ui.U(12)), c);
        ui.Rect(new Rectangle(kx + ui.U(21), ky - ui.U(24), ui.U(1), ui.U(12)), c);
        var flag = Style.Sides[Style.PaletteOf(town.Faction)].Cloth;
        ui.Rect(new Rectangle(kx + ui.U(22), ky - ui.U(25), ui.U(9), ui.U(6)), Kit.Mul(flag, 0.4f));
        ui.Rect(new Rectangle(kx + ui.U(22), ky - ui.U(24), ui.U(8), ui.U(4)), flag);
        if (night) ui.Rect(new Rectangle(kx + ui.U(6), ky + ui.U(6), ui.U(2), ui.U(3)), lit * 0.5f);
    }

    /// The town wall behind the houses: merlons, and towers flying the holder's colours (a watch light in some at night).
    void TownWall(Rectangle p, int top, Color c, bool night, Color lit, Color cloth, Random rng)
    {
        ui.Rect(new Rectangle(p.X, top, p.Width, p.Bottom - top), c);
        for (int x = p.X; x < p.Right; x += ui.U(8)) ui.Rect(new Rectangle(x, top - ui.U(3), ui.U(4), ui.U(3)), c);
        for (int x = p.X + ui.U(70); x < p.Right - ui.U(30); x += ui.U(130 + rng.Next(50)))
        {
            int w = ui.U(16), h = ui.U(14 + rng.Next(8));
            bool watch = rng.Next(2) == 0;
            ui.Rect(new Rectangle(x, top - h, w, h), c);
            for (int k = 0; k < w; k += ui.U(5)) ui.Rect(new Rectangle(x + k, top - h - ui.U(3), ui.U(3), ui.U(3)), c);
            ui.Rect(new Rectangle(x + w / 2, top - h - ui.U(14), ui.U(1), ui.U(11)), c);
            ui.Rect(new Rectangle(x + w / 2 + ui.U(1), top - h - ui.U(14), ui.U(7), ui.U(4)), cloth);
            if (night && watch) ui.Rect(new Rectangle(x + w / 2 - ui.U(1), top - h + ui.U(5), ui.U(2), ui.U(3)), lit * 0.75f);
        }
    }

    /// The building a town of this culture is known by, standing over the roofs.
    void Landmark(Rectangle p, string culture, int ground, Color c, bool night, Color lit, Color cloth, float t)
    {
        int x = p.X + (int)(p.Width * 0.58f);
        void Glow(int gx, int gy) { if (night) ui.Rect(new Rectangle(gx, gy, ui.U(2), ui.U(4)), lit * 0.8f); }
        switch (culture)
        {
            case "league": // canals, cranes and counting houses
                StepGable(x, ground, ui.U(34), ui.U(66), c);
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                        if (night ? (row + col) % 2 == 0 : true)
                            ui.Rect(new Rectangle(x + ui.U(6 + col * 9), ground - ui.U(60 - row * 9), ui.U(3), ui.U(4)), night ? lit * 0.85f : Kit.Mul(c, 0.6f));
                Crane(x - ui.U(90), ground, c, night); Crane(x + ui.U(170), ground, c, night);
                break;
            case "southern": // white walls, a dome and a minaret, palms
                ui.Disc(new Vector2(x + ui.U(22), ground - ui.U(40)), ui.U(18), c);
                ui.Rect(new Rectangle(x, ground - ui.U(40), ui.U(44), ui.U(40)), c);
                Gable(x + ui.U(56), ground, ui.U(8), ui.U(66), ui.U(8), c); Glow(x + ui.U(59), ground - ui.U(58));
                Palm(x - ui.U(34), ground, c, 1.2f); Palm(x + ui.U(140), ground, c, 1.1f); Palm(x + ui.U(230), ground, c, 0.9f);
                break;
            case "steppe": // the khan's great tent under its standards, the herd tethered by it
                Dome(x + ui.U(30), ground - ui.U(20), ui.U(34), ui.U(24), Color.Lerp(c, rimLight, 0.12f));
                ui.Rect(new Rectangle(x - ui.U(4), ground - ui.U(20), ui.U(68), ui.U(20)), Color.Lerp(c, rimLight, 0.1f));
                ui.Rect(new Rectangle(x - ui.U(4), ground - ui.U(15), ui.U(68), ui.U(3)), cloth); // its felt band
                ui.Rect(new Rectangle(x + ui.U(46), ground - ui.U(20), ui.U(18), ui.U(20)), Color.Lerp(c, rimLight, 0.3f)); // the sunlit side
                ui.Rect(new Rectangle(x + ui.U(24), ground - ui.U(13), ui.U(12), ui.U(13)), night ? lit : Kit.Mul(c, 0.6f)); // its door
                foreach (int k in new[] { -14, 74 })
                {
                    ui.Rect(new Rectangle(x + ui.U(k), ground - ui.U(70), ui.U(2), ui.U(70)), c);
                    for (int j = 0; j < 4; j++) ui.Line(new Vector2(x + ui.U(k + 1), ground - ui.U(68)), new Vector2(x + ui.U(k - 3 + j * 3), ground - ui.U(54) + j % 2 * ui.U(2)), cloth, 1);
                }
                break;
            case "northern": // a long timber hall, horned gables, smoke
                Gable(x, ground, ui.U(70), ui.U(38), ui.U(20), c);
                {
                    var apex = new Vector2(x + ui.U(35), ground - ui.U(58));
                    ui.Line(apex, apex + new Vector2(-ui.U(5), -ui.U(7)), c, 2); // the gable horns, a V at the ridge
                    ui.Line(apex, apex + new Vector2(ui.U(5), -ui.U(7)), c, 2);
                }
                Smoke(x + ui.U(48), ground - ui.U(54), t, night);
                break;
            case "order": // the chapter house: a squat great tower with buttresses, the order's sun on its face, its long hall
                Gable(x - ui.U(50), ground, ui.U(56), ui.U(34), ui.U(12), c);
                ui.Rect(new Rectangle(x, ground - ui.U(44), ui.U(44), ui.U(44)), c); // the chapter's hall block
                foreach (int bx in new[] { -4, 44 }) ui.Line(new Vector2(x + ui.U(bx), ground), new Vector2(x + ui.U(bx < 0 ? 2 : 42), ground - ui.U(32)), c, 4);
                {
                    int th = Math.Min(ui.U(118), ground - p.Y - ui.U(22)); // the sun tower, tall and slender, the highest thing in town
                    ui.Rect(new Rectangle(x + ui.U(11), ground - th, ui.U(22), th), c);
                    ui.Rect(new Rectangle(x + ui.U(32), ground - th, ui.U(1), th), Color.Lerp(c, rimLight, 0.6f));
                    for (int k = 0; k < ui.U(14); k += Math.Max(1, ui.U(1))) ui.Rect(new Rectangle(x + ui.U(9) + k * 13 / 14, ground - th - k, ui.U(26) - k * 26 / 14, Math.Max(1, ui.U(1))), c); // its spire
                    ui.Rect(new Rectangle(x + ui.U(22), ground - th - ui.U(20), ui.U(1), ui.U(6)), c);
                    ui.Rect(new Rectangle(x + ui.U(19), ground - th + ui.U(30), ui.U(6), ui.U(9)), night ? lit : Kit.Mul(c, 0.6f)); // a lit slit
                    ui.Rect(new Rectangle(x + ui.U(43), ground - ui.U(44), ui.U(1), ui.U(44)), Color.Lerp(c, rimLight, 0.5f));
                    var sun = new Vector2(x + ui.U(22), ground - th + ui.U(14));
                    var gold = night ? new Color(200, 150, 80) : new Color(232, 190, 96);
                    for (int r = 0; r < 8; r++) { float a = r * MathF.PI / 4; ui.Line(sun, sun + new Vector2(MathF.Cos(a), MathF.Sin(a)) * ui.U(9), gold, 1); }
                    ui.Disc(sun, ui.U(5), gold);
                }
                Glow(x + ui.U(10), ground - ui.U(24)); Glow(x + ui.U(32), ground - ui.U(24));
                break;
            case "crimson": // a market hall under red banners
                Gable(x, ground, ui.U(60), ui.U(36), ui.U(18), c);
                foreach (int k in new[] { -6, 30, 66 })
                {
                    ui.Rect(new Rectangle(x + ui.U(k), ground - ui.U(70), ui.U(1), ui.U(30)), c);
                    ui.Rect(new Rectangle(x + ui.U(k + 1), ground - ui.U(70), ui.U(6), ui.U(14)), cloth);
                }
                break;
            default: // a church with a tall spire (blue roofs and quiet bells, and the rest)
                Gable(x, ground, ui.U(44), ui.U(36), ui.U(14), c);
                Gable(x + ui.U(44), ground, ui.U(16), ui.U(56), ui.U(30), c); Glow(x + ui.U(51), ground - ui.U(48));
                break;
        }
    }

    /// A house front with a pitched roof `roof` high.
    void Gable(int x, int ground, int w, int h, int roof, Color c)
    {
        ui.Rect(new Rectangle(x, ground - h, w, h), c);
        int s = Math.Max(1, ui.U(1));
        for (int k = 0; k < roof; k += s)
        {
            int inset = (int)(k / (float)roof * w / 2);
            ui.Rect(new Rectangle(x + inset, ground - h - k - s, w - 2 * inset, s), snowCap.A > 0 && k >= roof * 0.45f ? snowCap : c);
            if (rimLight.A > 0) ui.Rect(new Rectangle(x + w - inset - s, ground - h - k - s, s, s), rimLight);
        }
        if (rimLight.A > 0) ui.Rect(new Rectangle(x + w - s, ground - h, s, h), Color.Lerp(c, rimLight, 0.45f));
        if (rimLight.A > 0) ui.Rect(new Rectangle(x, ground - h, s, h), Color.Lerp(c, rimLight, 0.18f));
    }

    /// A merchant's house with a stepped gable.
    void StepGable(int x, int ground, int w, int h, Color c)
    {
        ui.Rect(new Rectangle(x, ground - h, w, h), c);
        for (int i = 1; i <= 3; i++)
        {
            int inset = i * w / 7;
            ui.Rect(new Rectangle(x + inset, ground - h - i * ui.U(4), w - 2 * inset, ui.U(4)), c);
            ui.Rect(new Rectangle(x + inset, ground - h - i * ui.U(4), ui.U(1), ui.U(4)), Kit.Mul(c, 0.7f)); // the notch's shaded riser
            if (rimLight.A > 0) ui.Rect(new Rectangle(x + w - inset - ui.U(1), ground - h - i * ui.U(4), ui.U(1), ui.U(4)), rimLight);
            if (rimLight.A > 0) ui.Rect(new Rectangle(x + inset, ground - h - i * ui.U(4), w - 2 * inset, ui.U(1)), Color.Lerp(c, rimLight, 0.6f));
        }
        if (rimLight.A > 0) ui.Rect(new Rectangle(x + w - ui.U(1), ground - h, ui.U(1), h), Color.Lerp(c, rimLight, 0.45f));
    }

    /// A harbour crane: an A-frame on the quay, a stout mast, a heavy jib, the rope with its hook and bale; a lantern at the tip by night.
    void Crane(int x, int ground, Color c, bool night)
    {
        int top = ground - ui.U(64);
        var edge = Color.Lerp(c, night ? new Color(150, 128, 170) : new Color(250, 226, 180), 0.5f);
        ui.Line(new Vector2(x - ui.U(16), ground), new Vector2(x + ui.U(1), ground - ui.U(26)), c, 3);
        ui.Line(new Vector2(x + ui.U(19), ground), new Vector2(x + ui.U(2), ground - ui.U(26)), c, 3);
        ui.Line(new Vector2(x + ui.U(19), ground), new Vector2(x + ui.U(4), ground - ui.U(23)), edge, 1);
        ui.Rect(new Rectangle(x - ui.U(10), ground - ui.U(10), ui.U(24), ui.U(2)), c);
        ui.Rect(new Rectangle(x - ui.U(1), top, ui.U(4), ground - top), c);
        ui.Line(new Vector2(x, top + ui.U(2)), new Vector2(x - ui.U(12), top + ui.U(6)), c, 3); // the tail
        ui.Rect(new Rectangle(x - ui.U(15), top + ui.U(5), ui.U(7), ui.U(7)), c); // its counterweight
        ui.Rect(new Rectangle(x - ui.U(15), top + ui.U(5), ui.U(7), ui.U(1)), edge);
        ui.Rect(new Rectangle(x + ui.U(2), top, ui.U(1), ground - top), edge);
        ui.Line(new Vector2(x + ui.U(1), top + ui.U(2)), new Vector2(x + ui.U(36), top + ui.U(14)), c, 3);
        ui.Line(new Vector2(x + ui.U(1), top), new Vector2(x + ui.U(36), top + ui.U(12)), edge, 1);
        ui.Line(new Vector2(x + ui.U(1), top + ui.U(18)), new Vector2(x + ui.U(34), top + ui.U(15)), c, 1);
        ui.Rect(new Rectangle(x + ui.U(34), top + ui.U(15), ui.U(1), ui.U(18)), c);
        ui.Rect(new Rectangle(x + ui.U(32), top + ui.U(33), ui.U(5), ui.U(1)), c);
        ui.Rect(new Rectangle(x + ui.U(30), top + ui.U(34), ui.U(9), ui.U(7)), c);
        ui.Rect(new Rectangle(x + ui.U(30), top + ui.U(34), ui.U(9), ui.U(1)), edge);
        ui.Rect(new Rectangle(x + ui.U(38), top + ui.U(34), ui.U(1), ui.U(7)), edge);
        if (!night) return;
        var tip = new Vector2(x + ui.U(36), top + ui.U(13));
        ui.Disc(tip, ui.U(7), new Color(230, 70, 50) * 0.18f); ui.Disc(tip, ui.U(4), new Color(240, 90, 60) * 0.38f);
        ui.Rect(new Rectangle((int)tip.X - ui.U(1), (int)tip.Y - ui.U(1), ui.U(2), ui.U(2)), new Color(250, 110, 70));
    }

    /// A palm: a leaning trunk and drooping fronds (`size` scales it).
    void Palm(int x, int ground, Color c, float size = 1)
    {
        int U(float n) => ui.U((int)(n * size));
        var top = new Vector2(x + U(8), ground - U(64));
        ui.Line(new Vector2(x, ground), top, Kit.Mul(c, 0.7f), 3);
        var trunk = c; c = Color.Lerp(c, new Color(76, 116, 60), 0.55f); // dusty green fronds
        foreach (var (dx, dy) in new[] { (-22, 10), (-14, -5), (20, 11), (14, -6), (2, -12), (-6, 14), (8, 15) })
        {
            var mid = top + new Vector2(U(dx * 0.55f), U(dy * 0.3f) - ui.U(3));
            ui.Line(top, mid, c, 2); ui.Line(mid, top + new Vector2(U(dx), U(dy)), c, 2);
        }
        ui.Disc(top, ui.U(2), Kit.Mul(trunk, 0.6f)); // dates
    }

    /// A horse at its tether: body, legs, neck and head, a tail.
    void Horse(int x, int y, Color c, bool left, float size = 1)
    {
        int d = left ? -1 : 1;
        int U(float n) => Math.Max(1, ui.U((int)MathF.Round(n * size)));
        ui.Rect(new Rectangle(x, y, U(12), U(5)), c);
        foreach (int lx in new[] { 1, 3, 8, 10 }) ui.Rect(new Rectangle(x + U(lx), y + U(5), U(1), U(7)), c);
        int nx = left ? x : x + U(10);
        ui.Line(new Vector2(nx + U(1), y + U(1)), new Vector2(nx + d * U(4) + U(1), y - U(5)), c, (int)(2 * size));
        ui.Rect(new Rectangle(nx + d * U(4) - (left ? U(3) : 0), y - U(6), U(5), U(2)), c);
        int tx = left ? x + U(12) : x - U(1);
        ui.Line(new Vector2(tx, y + U(1)), new Vector2(tx - d * U(2), y + U(7)), c, Math.Max(1, (int)size));
    }

    /// Horses tethered among the tents, in front of them.
    void Herd(Rectangle p, int ground, TownColours c, int seed, int tavernX)
    {
        var rng = new Random(seed * 5 + 9);
        var col = Color.Lerp(c.Near, new Color(120, 80, 52), 0.35f);
        for (int i = 0; i < 4; i++)
        {
            int x = tavernX + ui.U(200 + i * 90 + rng.Next(40));
            if (x > p.Right - ui.U(20)) break;
            bool left = rng.Next(2) == 0;
            Horse(x, ground - ui.U(18), col, left, 1.5f);
            ui.Rect(new Rectangle(x, ground - ui.U(18), ui.U(18), ui.U(1)), Color.Lerp(col, c.Rim, 0.6f)); // the light along its back
            ui.Rect(new Rectangle(x + ui.U(23), ground - ui.U(13), ui.U(1), ui.U(13)), Kit.Mul(col, 0.7f)); // the tether post
        }
    }

    /// Folk in the street before the tavern: two figures talking in its light, a handcart with its load further on.
    void Folk(Rectangle p, TownLook l, TownColours c, int ground, int seed, int tavernX)
    {
        var rng = new Random(seed * 17 + 3);
        Color[] cloths = { new(120, 60, 50), new(60, 80, 110), new(110, 96, 60), new(80, 96, 64) };
        int fx0 = tavernX + ui.U(196 + rng.Next(30)), fx1 = p.X + (int)(p.Width * (0.62f + rng.NextSingle() * 0.25f));
        foreach (int g in new[] { fx0, fx1 }) if (g < p.Right - ui.U(30) && (g == fx0 || Math.Abs(g - fx0) > ui.U(80)))
        for (int i = 0; i < 2; i++)
        {
            int fx = g;
            var cloth = cloths[rng.Next(cloths.Length)];
            if (l.Night) cloth = Kit.Mul(cloth, 0.45f); else if (l.Dusk) cloth = Kit.Mul(cloth, 0.75f);
            int x = fx + i * ui.U(9), h = ui.U(15 + rng.Next(3));
            ui.Rect(new Rectangle(x, ground - h + ui.U(5), ui.U(5), h - ui.U(8)), cloth);                       // the coat
            ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(3), ui.U(1), ui.U(3)), Kit.Mul(cloth, 0.5f));      // legs
            ui.Rect(new Rectangle(x + ui.U(3), ground - ui.U(3), ui.U(1), ui.U(3)), Kit.Mul(cloth, 0.5f));
            var face = l.Night ? new Color(150, 100, 70) : new Color(214, 170, 130);
            ui.Rect(new Rectangle(x + ui.U(1), ground - h + ui.U(1), ui.U(3), ui.U(4)), face);                   // the head
            ui.Rect(new Rectangle(x + ui.U(1), ground - h, ui.U(3), ui.U(1)), Kit.Mul(cloth, 0.6f));             // a hood
            if (!l.Day) ui.Rect(new Rectangle(x, ground - h + ui.U(5), ui.U(1), h - ui.U(8)), c.Lit * 0.6f);     // the tavern's light on them
        }
        int cx = p.X + (int)(p.Width * (0.5f + rng.NextSingle() * 0.35f));
        if (cx > tavernX - ui.U(40) && cx < tavernX + ui.U(220)) cx = tavernX + ui.U(240);
        if (cx > p.Right - ui.U(40)) return;
        var wood = l.Night ? new Color(60, 40, 36) : new Color(120, 82, 50);
        ui.Rect(new Rectangle(cx, ground - ui.U(9), ui.U(20), ui.U(4)), wood);                                    // the bed
        ui.Rect(new Rectangle(cx + ui.U(2), ground - ui.U(13), ui.U(7), ui.U(4)), Kit.Mul(wood, 1.3f));          // sacks
        ui.Rect(new Rectangle(cx + ui.U(10), ground - ui.U(12), ui.U(6), ui.U(3)), Kit.Mul(wood, 1.15f));
        ui.Line(new Vector2(cx + ui.U(20), ground - ui.U(8)), new Vector2(cx + ui.U(29), ground - ui.U(4)), wood, 1); // the shafts
        var wheel = new Vector2(cx + ui.U(7), ground - ui.U(4));
        ui.Disc(wheel, ui.U(4), Kit.Mul(wood, 0.7f)); ui.Disc(wheel, ui.U(2), wood); ui.Rect(new Rectangle((int)wheel.X, (int)wheel.Y, ui.U(1), ui.U(1)), Kit.Mul(wood, 0.5f));
    }

    /// Chimney smoke rising and thinning.
    void Smoke(int x, int y, float t, bool night)
    {
        if (backRow) return; // the back row's chimneys are lost behind the front's
        Color cool = night ? new Color(120, 106, 138) : new Color(214, 212, 218), warm = night ? new Color(196, 132, 104) : new Color(236, 214, 190);
        int seed = Math.Abs(x * 7 + y * 13) % 5; // each chimney its own plume: taller or shorter, drifting more or less
        float rise = ui.U(18 + seed * 4), drift = ui.U(8 + seed * 3), puff = ui.U(2 + seed % 3);
        for (int i = 0; i < 4; i++)
        {
            float k = (t * (0.2f + seed * 0.03f) + i * 0.25f) % 1;
            ui.Disc(new Vector2(x + k * drift, y - k * rise), puff + k * ui.U(5), Color.Lerp(warm, cool, MathF.Min(1, k * 1.6f)) * (0.85f * (1 - k)));
        }
    }
}
