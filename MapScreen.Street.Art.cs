using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: hero architecture, ground textures, foliage, harbour masts and figures for the settlement street. Drawing only.
partial class MapScreen
{
    /// A detailed stone keep: curtain wall, twin flanking towers, arched gatehouse with portcullis and torches, and a tall central donjon.
    /// `hero` paints the full foreground castle (ashlar stone, motte, stone steps, turret roofs, glowing torches); otherwise an atmospheric hilltop keep.
    void Keep(int cx, int baseY, int scale, TownLook l, TownColours c, Color cloth, float t, bool hero)
    {
        if (l.Build == "log") { HallFort(cx, baseY, scale, l, c, cloth, t, hero); return; } // the north builds in timber
        if (l.Build == "white") { Kasbah(cx, baseY, scale, l, c, cloth, t, hero); return; } // the south in rammed earth
        if (l.Build == "tent") { KhanOrdu(cx, baseY, scale, l, c, cloth, t, hero); return; } // the steppe's khan keeps a great tent
        if (l.Culture == "azure") { Chateau(cx, baseY, scale, l, c, cloth, t, hero); return; } // the Azure build a château under blue slate
        int k = ui.U(scale), s1 = Math.Max(1, ui.U(1));
        int wave = (int)MathF.Round(MathF.Sin(t * 3.2f) * s1);
        Color dayStone = l.Build switch { "white" => new(220, 210, 192), "log" => new(138, 126, 114), _ => new(168, 160, 148) };
        Color stone = hero ? HeroWall(l, c, dayStone) : Color.Lerp(c.Far, c.Mid, 0.52f);
        Color shade = Kit.Mul(stone, hero ? 0.72f : 0.82f);
        Color dark = Kit.Mul(stone, hero ? 0.52f : 0.68f);
        Color litEdge = Color.Lerp(stone, c.Rim, hero ? 0.45f : 0.25f);
        Color roof = l.Night ? Color.Lerp(c.Near, c.Roof, 0.45f) : c.Roof;
        bool pitchedTurrets = l.Build is not ("white" or "tent");

        KeepBase(cx, baseY, k, l, c, hero, stone, shade, dark, litEdge);

        // Helper for stone blocks in keep-scale units.
        void Box(int x, int y, int w, int h, Color col) => ui.Rect(new Rectangle(cx + x * k, baseY - y * k, w * k, h * k), col);

        // 1. Central Great Donjon (behind the curtain wall).
        Box(-5, 22, 10, 22, Kit.Mul(stone, 0.94f));
        Box(4, 22, 1, 22, shade); // right shade
        Box(-5, 22, 1, 22, Color.Lerp(stone, litEdge, 0.5f)); // left highlight
        // Buttresses on the donjon.
        Box(-6, 17, 1, 17, shade); Box(5, 17, 1, 17, dark);
        if (hero)
        {
            for (int ry = 12; ry < 22; ry += 2)
                ui.Rect(new Rectangle(cx - 5 * k, baseY - ry * k, 10 * k, s1), Kit.Mul(stone, 0.86f));
            // Machicolation corbel line under donjon parapet.
            Box(-5, 21, 10, 1, dark);
        }
        // Donjon parapet & merlons.
        for (int i = -5; i <= 4; i += 2) Box(i, 23, 1, 1, stone);
        if (pitchedTurrets)
        {
            // Steep tiled roof rising behind the donjon battlements.
            int rw = 9 * k, rh = 5 * k, rx = cx - 4 * k - k / 2, ry = baseY - 22 * k;
            Gable(rx, ry, rw, 0, rh, hero ? roof : Color.Lerp(c.Far, roof, 0.55f));
            if (hero)
                for (int ty = ui.U(3); ty < rh; ty += ui.U(4))
                {
                    int inset = (int)(ty / (float)rh * rw / 2);
                    ui.Rect(new Rectangle(rx + inset, ry - ty, rw - 2 * inset, s1), Kit.Mul(roof, 0.72f));
                }
        }
        // Tall hall windows on the donjon.
        Color winCol = l.Day ? new Color(34, 30, 34) : c.Lit;
        if (hero)
        {
            foreach (int wx in new[] { -3, 1 })
            {
                var wr = new Rectangle(cx + wx * k, baseY - 18 * k, 2 * k, 3 * k);
                if (!l.Day) ui.Rect(new Rectangle(wr.X - s1, wr.Y - s1, wr.Width + 2 * s1, wr.Height + 2 * s1), c.Lit * 0.2f);
                ui.Rect(wr, winCol);
                ui.Rect(new Rectangle(wr.X + k / 2, wr.Y - s1, k, s1), winCol); // pointed arch top
            }
        }
        else if (!l.Day)
        {
            ui.Rect(new Rectangle(cx - k, baseY - 17 * k, k, 2 * k), c.Lit * 0.9f);
            ui.Rect(new Rectangle(cx + k, baseY - 16 * k, k, 2 * k), c.Lit * 0.75f);
        }

        // Flagpole and waving faction standard rising cleanly from the donjon roof apex.
        int poleBase = baseY - (pitchedTurrets ? 27 : 22) * k;
        int poleTop = poleBase - 5 * k;
        ui.Rect(new Rectangle(cx, poleTop, s1, 5 * k + s1), dark);
        ui.Rect(new Rectangle(cx + s1, poleTop, 5 * k, 2 * k + wave), cloth);
        ui.Rect(new Rectangle(cx + s1 + 5 * k, poleTop + s1, k, k + wave), cloth); // swallowtail tip
        ui.Rect(new Rectangle(cx + s1, poleTop + k, 5 * k, s1), Color.Lerp(cloth, Kit.Gilt, 0.45f)); // gold stripe

        // 2. Curtain wall.
        Box(-11, 11, 22, 11, stone);
        if (hero)
        {
            for (int ry = 2; ry < 11; ry += 2)
                ui.Rect(new Rectangle(cx - 11 * k, baseY - ry * k, 22 * k, s1), Kit.Mul(stone, 0.88f));
            // Scattered ashlar tone variation.
            foreach (var (ax, ay) in new[] { (-9, 8), (-6, 4), (4, 8), (7, 5), (-8, 2), (6, 2) })
                Box(ax, ay, 2, 1, Kit.Mul(stone, 0.9f));
            Box(-11, 10, 22, 1, dark); // machicolation shadow
        }
        for (int i = -11; i < 11; i += 2) Box(i, 12, 1, 1, stone);
        ui.Rect(new Rectangle(cx - 11 * k, baseY - 11 * k, 22 * k, s1), litEdge);

        // 3. Twin flanking towers.
        foreach (int side in new[] { -1, 1 })
        {
            int tx = side < 0 ? -15 : 10;
            Box(tx, 17, 5, 17, side < 0 ? stone : Kit.Mul(stone, 0.94f));
            Box(tx + 4, 17, 1, 17, shade);
            Box(tx, 17, 1, 17, Color.Lerp(stone, litEdge, 0.4f));
            if (hero)
            {
                for (int ry = 3; ry < 17; ry += 3)
                    ui.Rect(new Rectangle(cx + tx * k, baseY - ry * k, 5 * k, s1), Kit.Mul(stone, 0.87f));
                Box(tx, 16, 5, 1, dark); // corbel band
            }
            for (int i = 0; i < 5; i += 2) Box(tx + i, 18, 1, 1, stone);
            if (pitchedTurrets)
            {
                int rw = 6 * k, rh = 4 * k, rx = cx + tx * k - k / 2, ry = baseY - 17 * k;
                Gable(rx, ry, rw, 0, rh, hero ? roof : Color.Lerp(c.Far, roof, 0.5f));
                // Gilded finial on turret peak.
                if (hero) ui.Rect(new Rectangle(rx + rw / 2, ry - rh - ui.U(3), s1, ui.U(3)), Kit.Gilt);
            }
            // Arrow slits.
            int sx = cx + (tx + 2) * k;
            foreach (int sy in new[] { 13, 8 })
            {
                var slit = new Rectangle(sx, baseY - sy * k, k, 2 * k);
                ui.Rect(slit, l.Day ? new Color(34, 30, 34) : c.Lit * 0.8f);
            }
        }

        // 4. Gatehouse arch, portcullis, banners & torches (hero castle).
        if (hero)
        {
            // Projecting gatehouse frame.
            Box(-4, 12, 8, 12, Kit.Mul(stone, 1.03f));
            Box(3, 12, 1, 12, shade);
            for (int i = -4; i <= 3; i += 2) Box(i, 13, 1, 1, stone);
            // Stone arch & portcullis.
            var gate = new Rectangle(cx - 2 * k, baseY - 7 * k, 4 * k, 7 * k);
            ui.Rect(new Rectangle(gate.X - ui.U(2), gate.Y - ui.U(2), gate.Width + ui.U(4), gate.Height + ui.U(2)), dark);
            ui.Rect(new Rectangle(gate.X + ui.U(2), gate.Y - ui.U(4), gate.Width - ui.U(4), ui.U(2)), dark);
            ui.Rect(gate, l.Day ? new Color(28, 24, 24) : Color.Lerp(new Color(62, 34, 22), c.Lit, 0.42f));
            for (int gx = gate.X + ui.U(3); gx < gate.Right; gx += ui.U(4))
                ui.Rect(new Rectangle(gx, gate.Y, s1, gate.Height - ui.U(4)), new Color(68, 64, 66));
            for (int gy = gate.Y + ui.U(4); gy < gate.Bottom - ui.U(4); gy += ui.U(5))
                ui.Rect(new Rectangle(gate.X, gy, gate.Width, s1), new Color(68, 64, 66));
            // Crimson banners flanking the gatehouse.
            foreach (int side in new[] { -1, 1 })
            {
                int bx = cx + (side < 0 ? -8 : 6) * k;
                ui.Rect(new Rectangle(bx - s1, baseY - 9 * k, 2 * k + 2 * s1, s1), dark);
                ui.Rect(new Rectangle(bx, baseY - 9 * k + s1, 2 * k, 4 * k), cloth);
                ui.Rect(new Rectangle(bx + k / 2, baseY - 5 * k + s1, k, k), cloth);
                ui.Rect(new Rectangle(bx + k / 2, baseY - 8 * k, k, 2 * k), Color.Lerp(cloth, Kit.Gilt, 0.55f));
            }
            // Wall torches flanking the gate (glowing warmly at night/dusk).
            if (!l.Day)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    var tp = new Vector2(cx + side * (2 * k + ui.U(5)), baseY - 5 * k);
                    ui.Rect(new Rectangle((int)tp.X, (int)tp.Y, s1, ui.U(4)), dark);
                    ui.Disc(tp, ui.U(9), c.Lit * 0.14f);
                    ui.Disc(tp, ui.U(4), c.Lit * 0.28f);
                    ui.Rect(new Rectangle((int)tp.X - s1, (int)tp.Y - ui.U(2), ui.U(2), ui.U(2)), new Color(255, 228, 148));
                }
            }
        }
    }

    /// What a keep stands on: by the street (hero) a rocky motte with a stepped ramp up to the gate; on the town's hill a battered
    /// terrace of rock under the wall, `k` pixels to a keep unit.
    void KeepBase(int cx, int baseY, int k, TownLook l, TownColours c, bool hero, Color stone, Color shade, Color dark, Color litEdge)
    {
        int s1 = Math.Max(1, ui.U(1));
        if (hero)
        {
            // Rocky, grass-topped motte rising from the bailey ground (baseY + 14u) to baseY.
            int mh = ui.U(14), ground = baseY + mh;
            var earth = l.Snow ? c.Snow : l.Night ? Color.Lerp(c.Near, new Color(46, 48, 42), 0.45f) : new Color(92, 98, 66);
            var rock = Kit.Mul(stone, 0.65f);
            for (int m = 0; m < mh; m += s1)
            {
                float f = m / (float)mh;
                int hw = ui.U(86) + (int)(f * ui.U(42));
                var rowCol = m < ui.U(3) ? earth : Color.Lerp(rock, c.Ground, f * 0.8f);
                ui.Rect(new Rectangle(cx - hw, baseY + m, hw * 2, s1), rowCol);
            }
            // Retaining stone blocks along the motte face.
            for (int bx = cx - ui.U(74); bx < cx + ui.U(70); bx += ui.U(16))
            {
                int bh = ui.U(4 + Math.Abs(bx / Math.Max(1, ui.U(7))) % 3);
                ui.Rect(new Rectangle(bx, baseY + ui.U(3), ui.U(10), bh), Kit.Mul(rock, 0.85f));
                ui.Rect(new Rectangle(bx, baseY + ui.U(3), ui.U(10), s1), Color.Lerp(rock, c.Rim, 0.25f));
            }
            // Stepped stone ramp leading up to the gatehouse.
            for (int st = 0; st < 5; st++)
            {
                int sy = baseY + st * ui.U(3), sw = ui.U(24) + st * ui.U(4);
                ui.Rect(new Rectangle(cx - sw / 2, sy, sw, ui.U(3)), Color.Lerp(stone, shade, 0.25f + st * 0.08f));
                ui.Rect(new Rectangle(cx - sw / 2, sy, sw, s1), litEdge);
            }
        }
        else
        {
            // Battered stone terrace and rocky crag anchoring the hilltop keep into the slope below (no floating towers).
            Color crag = c.Far;
            for (int m = 0; m < 10 * k; m += s1)
            {
                float f = m / (float)(10 * k);
                int hw = 15 * k + (int)(f * 10 * k);
                var rowCol = m < 2 * k ? Color.Lerp(shade, crag, 0.45f) : Color.Lerp(dark, crag, MathF.Min(1f, f * 1.15f));
                ui.Rect(new Rectangle(cx - hw, baseY + m, hw * 2, s1), rowCol);
            }
            ui.Rect(new Rectangle(cx - 15 * k, baseY, 30 * k, s1), dark); // shadow seam under wall base
        }
    }

    /// The elders' hall in a village: a handsome timbered hall with stone foundation, tiled roof and smoking chimney,
    /// central arched porch, shuttered windows, hanging lantern and the realm's pennant.
    void EldersHall(Rectangle r, TownLook l, TownColours c, Color cloth, float t, List<(int X, int W)> lights)
    {
        if (l.Build == "white") { Majlis(r, l, c, cloth, t, lights); return; }
        if (l.Culture == "azure" && l.Build == "timber") { HotelDeVille(r, l, c, cloth, t, lights); return; }
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        int wallH = ui.U(34), roofH = ui.U(22), wallTop = ground - wallH;
        Color wall = HeroWall(l, c, l.Build == "log" ? new Color(138, 98, 66) : l.Build == "white" ? new Color(234, 226, 206) : new Color(212, 194, 164));
        Color timber = l.Night ? new Color(34, 24, 30) : new Color(74, 50, 36);
        Color stone = HeroWall(l, c, new Color(138, 132, 124));
        Color roof = l.Night ? Color.Lerp(c.Near, c.Roof, 0.42f) : c.Roof;
        int wave = (int)MathF.Round(MathF.Sin(t * 2.8f) * s1);

        // Chimney behind the roof slope.
        int chimX = r.Right - ui.U(18), chimY = wallTop - roofH + ui.U(4);
        ui.Rect(new Rectangle(chimX, chimY, ui.U(7), ui.U(14)), stone);
        ui.Rect(new Rectangle(chimX - s1, chimY, ui.U(9), ui.U(2)), Kit.Mul(stone, 0.8f));
        Smoke(chimX + ui.U(3), chimY - ui.U(2), t + r.X * 0.01f, l.Night);

        // Pitched roof with tile courses and eaves shadow.
        Gable(r.X - ui.U(4), wallTop, r.Width + ui.U(8), 0, roofH, roof);
        if (l.Build == "log") Turf(r.X - ui.U(4), wallTop, r.Width + ui.U(8), roofH, roof, l.Snow);
        else
            for (int ty = ui.U(4); ty < roofH; ty += ui.U(4))
            {
                int inset = (int)(ty / (float)roofH * (r.Width + ui.U(8)) / 2);
                ui.Rect(new Rectangle(r.X - ui.U(4) + inset, wallTop - ty, r.Width + ui.U(8) - 2 * inset, s1), Kit.Mul(roof, 0.72f));
            }
        ui.Rect(new Rectangle(r.X - ui.U(5), wallTop - s1, r.Width + ui.U(10), ui.U(2)), Color.Lerp(roof, c.Rim, 0.35f));
        if (l.Build == "log") // the north crosses its barge boards at the peak and carves them into dragons
        {
            int apex = r.X - ui.U(4) + (r.Width + ui.U(8)) / 2;
            var carve = l.Night ? new Color(96, 70, 44) : new Color(176, 128, 70);
            Dragon(apex - ui.U(1), wallTop - roofH + ui.U(2), -1, 1, NorthDark(l), carve);
            Dragon(apex + ui.U(1), wallTop - roofH + ui.U(2), 1, 1, NorthDark(l), carve);
        }

        // Pennant pole on the left roof slope (clear of the ELDERS sign over the ridge).
        int poleX = r.X + ui.U(11), poleY = wallTop - roofH + ui.U(1);
        ui.Rect(new Rectangle(poleX, poleY, s1, ui.U(13)), timber);
        ui.Rect(new Rectangle(poleX + s1, poleY, ui.U(11), ui.U(5) + wave), cloth);
        ui.Rect(new Rectangle(poleX + s1, poleY + ui.U(2), ui.U(11), s1), Color.Lerp(cloth, Kit.Gilt, 0.45f));

        // Main wall + stone plinth foundation + eave shadow.
        ui.Rect(new Rectangle(r.X, wallTop, r.Width, wallH), wall);
        ui.Rect(new Rectangle(r.X, wallTop, r.Width, ui.U(2)), Color.Black * 0.28f); // eave shadow
        ui.Rect(new Rectangle(r.X, ground - ui.U(6), r.Width, ui.U(6)), stone);
        ui.Rect(new Rectangle(r.X, ground - ui.U(6), r.Width, s1), Kit.Mul(stone, 0.75f));
        for (int sx = r.X + ui.U(8); sx < r.Right - ui.U(4); sx += ui.U(10))
            ui.Rect(new Rectangle(sx, ground - ui.U(5), s1, ui.U(5)), Kit.Mul(stone, 0.8f));

        // Timber framing (or log courses for log build).
        if (l.Build == "log")
        {
            for (int ly = wallTop + ui.U(3); ly < ground - ui.U(6); ly += ui.U(3))
                ui.Rect(new Rectangle(r.X, ly, r.Width, s1), Kit.Mul(wall, 0.72f));
        }
        else if (l.Build != "white")
        {
            ui.Rect(new Rectangle(r.X, wallTop + ui.U(12), r.Width, ui.U(2)), timber);
            foreach (int px in new[] { r.X, r.X + ui.U(22), r.Right - ui.U(24), r.Right - ui.U(2) })
                ui.Rect(new Rectangle(px, wallTop, ui.U(2), wallH - ui.U(6)), timber);
            ui.Line(new Vector2(r.X + ui.U(2), wallTop + ui.U(12)), new Vector2(r.X + ui.U(9), wallTop + ui.U(2)), timber, 1);
            ui.Line(new Vector2(r.Right - ui.U(2), wallTop + ui.U(12)), new Vector2(r.Right - ui.U(9), wallTop + ui.U(2)), timber, 1);
        }
        Edges(r.X, wallTop, r.Width, wallH, wall);

        // Shuttered mullioned windows on either side of the door.
        foreach (int wx in new[] { r.X + ui.U(6), r.Right - ui.U(18) })
        {
            var win = new Rectangle(wx, ground - ui.U(21), ui.U(12), ui.U(10));
            ui.Rect(new Rectangle(win.X - ui.U(3), win.Y, ui.U(2), win.Height), Kit.Mul(cloth, 0.85f));
            ui.Rect(new Rectangle(win.Right + s1, win.Y, ui.U(2), win.Height), Kit.Mul(cloth, 0.85f));
            TavernWindow(win, l, c, timber, true);
            ui.Rect(new Rectangle(win.X - ui.U(2), win.Bottom + s1, win.Width + ui.U(4), ui.U(2)), timber); // sill
            if (!l.Day) lights.Add((win.X, win.Width));
        }

        // Attic gable window.
        TavernWindow(new Rectangle(r.Center.X - ui.U(4), wallTop - ui.U(11), ui.U(8), ui.U(7)), l, c, timber, true);

        // Central arched doorway with pitched porch canopy.
        var door = new Rectangle(r.Center.X - ui.U(7), ground - ui.U(20), ui.U(14), ui.U(20));
        int py = door.Y - ui.U(4), pw = door.Width + ui.U(12), px0 = door.X - ui.U(6);
        Gable(px0, py, pw, 0, ui.U(6), roof);
        ui.Rect(new Rectangle(px0, py, pw, s1), Color.Lerp(roof, c.Rim, 0.4f));
        ui.Rect(new Rectangle(px0 + ui.U(1), py + s1, ui.U(2), ground - py - s1), timber);
        ui.Rect(new Rectangle(px0 + pw - ui.U(3), py + s1, ui.U(2), ground - py - s1), timber);
        TavernDoor(door, ground, l, c, timber, lights, lamp: false);
        if (!l.Day) Lantern(new Vector2(px0 - ui.U(2), door.Y + ui.U(3)), c, timber);

        // Stacked firewood beside the hall.
        int fx = r.Right + ui.U(2);
        var logCol = l.Night ? new Color(68, 48, 36) : new Color(130, 90, 56);
        ui.Rect(new Rectangle(fx, ground - ui.U(7), ui.U(8), ui.U(7)), logCol);
        for (int ly = 0; ly < 3; ly++)
            for (int lx = 0; lx < 3; lx++)
                ui.Rect(new Rectangle(fx + ui.U(1 + lx * 2), ground - ui.U(6 - ly * 2), s1, s1), Kit.Mul(logCol, 1.3f));
    }

    /// Cobblestone street for towns and castles: curb shadow under the buildings, staggered stone courses and subtle wheel ruts.
    void CobbleStreet(Rectangle p, TownLook l, TownColours c, int ground, int seed)
    {
        int s1 = Math.Max(1, ui.U(1)), h = p.Bottom - ground;
        Color baseCol = l.Snow ? c.Snow : Color.Lerp(c.Ground, l.Night ? new Color(36, 32, 46) : new Color(118, 108, 98), 0.55f);
        ui.Rect(new Rectangle(p.X, ground, p.Width, h), baseCol);
        // Contact shadow & curb line along the building feet.
        ui.Rect(new Rectangle(p.X, ground, p.Width, ui.U(2)), Color.Black * 0.28f);
        ui.Rect(new Rectangle(p.X, ground + ui.U(2), p.Width, s1), Color.Lerp(baseCol, c.Rim, 0.22f));
        if (l.Snow) // trodden: grey ruts where the carts go, and the cobbles showing through where feet have worn it away
        {
            var trod = Color.Lerp(c.Snow, l.Night ? new Color(70, 74, 104) : new Color(150, 160, 186), 0.45f);
            var stone = Color.Lerp(c.Ground, l.Night ? new Color(40, 38, 54) : new Color(118, 112, 110), 0.6f);
            var sr = new Random(seed * 41 + 9);
            foreach (int ty in new[] { 9, 14 })
                for (int x = p.X; x < p.Right; x += ui.U(6))
                    if (sr.Next(5) > 0 && ground + ui.U(ty) < p.Bottom) ui.Rect(new Rectangle(x, ground + ui.U(ty), ui.U(4 + sr.Next(4)), s1 * 2), trod);
            for (int i = 0; i < 9; i++)
            {
                int x = p.X + sr.Next(Math.Max(1, p.Width)), y = ground + ui.U(4 + sr.Next(3) * 4);
                for (int j = 0; j < 3; j++) ui.Rect(new Rectangle(x + j * ui.U(5), y + (j % 2) * ui.U(2), ui.U(4), ui.U(2)), stone);
                ui.Rect(new Rectangle(x - ui.U(2), y - s1, ui.U(18), s1), trod);
            }
            return;
        }
        // Staggered cobble courses.
        var rng = new Random(seed * 41 + 9);
        for (int row = 0, y = ground + ui.U(4); y < p.Bottom - ui.U(2); row++, y += ui.U(4))
        {
            int off = (row % 2) * ui.U(5);
            for (int x = p.X - off; x < p.Right; x += ui.U(10))
            {
                int w = ui.U(7 + rng.Next(2));
                var tone = rng.Next(3) switch { 0 => Kit.Mul(baseCol, 0.88f), 1 => Kit.Mul(baseCol, 1.07f), _ => baseCol };
                ui.Rect(new Rectangle(x, y, w, ui.U(3)), tone);
                ui.Rect(new Rectangle(x, y, w, s1), Color.Lerp(tone, c.Rim, 0.12f));
            }
        }
        // Subtle cart wheel tracks.
        foreach (int ty in new[] { 9, 14 })
            if (ground + ui.U(ty) < p.Bottom)
                ui.Rect(new Rectangle(p.X, ground + ui.U(ty), p.Width, s1), Color.Black * 0.14f);
    }

    /// Village lane: grassy verge with pixel tufts along the cottages, then a worn dirt road with pebbles and ruts.
    void VillageLane(Rectangle p, TownLook l, TownColours c, int ground, int seed)
    {
        int s1 = Math.Max(1, ui.U(1)), h = p.Bottom - ground;
        ui.Rect(new Rectangle(p.X, ground, p.Width, h), c.Ground);
        // Grassy verge along the top of the lane (snow bank in winter).
        bool dry = l.Build == "white" || Lands.Of(l.Country).Hot; // a sandy verge with few tufts in the hot lands
        Color grass = l.Snow ? c.Snow : dry ? (l.Night ? Color.Lerp(c.Near, new Color(90, 76, 56), 0.5f) : l.Dusk ? new Color(150, 116, 84) : new Color(206, 178, 126)) : l.Night ? Color.Lerp(c.Near, new Color(42, 66, 40), 0.55f) : l.Dusk ? new Color(86, 94, 54) : new Color(96, 126, 62);
        ui.Rect(new Rectangle(p.X, ground, p.Width, ui.U(4)), grass);
        ui.Rect(new Rectangle(p.X, ground, p.Width, s1), Color.Black * 0.22f);
        var rng = new Random(seed * 31 + 7);
        for (int x = p.X + ui.U(2); x < p.Right; x += ui.U(5 + rng.Next(5)))
        {
            int th = ui.U(2 + rng.Next(2));
            if (!dry || rng.Next(4) == 0) ui.Rect(new Rectangle(x, ground + ui.U(4), ui.U(2), th), dry ? Color.Lerp(grass, new Color(110, 120, 70), l.Day ? 0.6f : 0.3f) : grass);
            if (!l.Snow && rng.Next(6) == 0)
                ui.Rect(new Rectangle(x + s1, ground + ui.U(1), s1, s1), l.Night ? new Color(140, 130, 80) : new Color(226, 206, 110)); // wildflower dot
        }
        // Dirt road ruts and pebbles.
        for (int i = 0; i < 18; i++)
        {
            int rx = p.X + (int)(p.Width * ((i * 0.29f + rng.NextSingle() * 0.05f) % 1));
            int ry = ground + ui.U(7 + i % 4 * 2);
            if (ry < p.Bottom) ui.Rect(new Rectangle(rx, ry, ui.U(8 + i % 3 * 5), s1), Kit.Mul(c.Ground, 0.84f));
        }
    }

    /// A leafy deciduous tree (oak/elm): gnarled trunk and layered shaded pixel canopy.
    void Tree(int x, int ground, int h, TownLook l, TownColours c, Random rng)
    {
        if (l.Build == "white") { Palm(x, ground, Color.Lerp(c.Near, new Color(70, 110, 60), l.Night ? 0.25f : 0.8f), h / (float)ui.U(60)); return; } // the south grows date palms
        int s1 = Math.Max(1, ui.U(1));
        Color trunk = l.Night ? new Color(42, 30, 28) : new Color(86, 60, 40);
        Color leaf = l.Snow ? Color.Lerp(c.Snow, new Color(110, 126, 118), 0.45f)
            : l.Night ? Color.Lerp(c.Near, new Color(34, 56, 38), 0.6f)
            : l.Dusk ? new Color(76, 86, 52)
            : new Color(74, 108, 54);
        int tw = Math.Max(ui.U(3), h / 10), crownY = ground - h * 2 / 3;
        ui.Rect(new Rectangle(x - tw / 2, crownY, tw, ground - crownY), trunk);
        ui.Rect(new Rectangle(x - tw / 2, crownY, s1, ground - crownY), Color.Lerp(trunk, c.Rim, 0.2f));
        int r = h / 3;
        // Stepped pixel foliage tiers (shadowed base, mid body, sunlit crown tufts).
        Color darkLeaf = Kit.Mul(leaf, 0.72f), hiLeaf = Color.Lerp(leaf, c.Rim, l.Night ? 0.18f : 0.3f);
        ui.Rect(new Rectangle(x - r, crownY - r / 2, r * 2, r * 2 / 3), darkLeaf);
        ui.Rect(new Rectangle(x - r - ui.U(2), crownY - r / 4, r * 2 + ui.U(4), r / 2), darkLeaf);
        ui.Rect(new Rectangle(x - r * 4 / 5, crownY - r * 5 / 6, r * 8 / 5, r * 2 / 3), leaf);
        ui.Rect(new Rectangle(x - r / 2, crownY - r - ui.U(2), r, r / 2), leaf);
        // Sunlit/moonlit top-right pixel leaf clusters.
        ui.Rect(new Rectangle(x - r / 3, crownY - r - ui.U(1), r * 2 / 3, ui.U(3)), hiLeaf);
        ui.Rect(new Rectangle(x + r / 6, crownY - r * 3 / 4, r / 2, ui.U(3)), hiLeaf);
        ui.Rect(new Rectangle(x - r * 2 / 3, crownY - r / 2, r / 3, ui.U(2)), hiLeaf);
        // Dark leaf dither notches for organic silhouette.
        ui.Rect(new Rectangle(x - r / 2, crownY - ui.U(1), ui.U(4), ui.U(2)), Kit.Mul(leaf, 0.58f));
        ui.Rect(new Rectangle(x + r / 4, crownY - ui.U(2), ui.U(5), ui.U(2)), Kit.Mul(leaf, 0.58f));
    }

    /// A golden haystack beside a village lane.
    void Haystack(int x, int ground, TownLook l, TownColours c)
    {
        int s1 = Math.Max(1, ui.U(1));
        Color hay = l.Night ? new Color(96, 80, 52) : l.Dusk ? new Color(172, 134, 72) : new Color(204, 174, 96);
        int w = ui.U(20), h = ui.U(14);
        Dome(x + w / 2, ground - ui.U(3), w / 2, h, hay);
        ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(4), w - ui.U(2), ui.U(4)), Kit.Mul(hay, 0.84f));
        for (int i = 0; i < 4; i++)
            ui.Rect(new Rectangle(x + ui.U(3 + i * 4), ground - ui.U(6 + i % 2 * 3), ui.U(3), s1), Kit.Mul(hay, 0.72f));
    }

    /// A wooden split-rail fence between village cottages.
    void SplitFence(int x0, int x1, int ground, TownLook l)
    {
        if (x1 - x0 < ui.U(8)) return;
        int s1 = Math.Max(1, ui.U(1));
        Color wood = l.Night ? new Color(58, 42, 34) : new Color(114, 82, 52);
        for (int x = x0; x <= x1; x += ui.U(8))
            ui.Rect(new Rectangle(x, ground - ui.U(8), ui.U(2), ui.U(8)), wood);
        ui.Rect(new Rectangle(x0, ground - ui.U(7), x1 - x0 + ui.U(2), s1), Kit.Mul(wood, 1.1f));
        ui.Rect(new Rectangle(x0, ground - ui.U(4), x1 - x0 + ui.U(2), s1), wood);
    }

    /// Distant ship masts and sails peeking over the wall/horizon of a port settlement.
    void HarbourMasts(Rectangle p, int horizonY, TownLook l, TownColours c, Color cloth, int seed)
    {
        int s1 = Math.Max(1, ui.U(1));
        var rng = new Random(seed * 53 + 11);
        Color mast = Color.Lerp(c.Far, c.Mid, 0.45f);
        Color sail = l.Night ? Color.Lerp(c.Far, new Color(140, 136, 150), 0.35f) : Color.Lerp(c.SkyLow, new Color(242, 232, 210), 0.65f);
        foreach (float fx in new[] { 0.24f, 0.31f, 0.48f })
        {
            int mx = p.X + (int)(p.Width * (fx + (rng.NextSingle() - 0.5f) * 0.03f));
            int mh = ui.U(26 + rng.Next(12));
            ui.Rect(new Rectangle(mx, horizonY - mh, s1, mh), mast);
            // Yardarm & furled sail.
            int yw = ui.U(12 + rng.Next(5)), yy = horizonY - mh + ui.U(6);
            ui.Rect(new Rectangle(mx - yw / 2, yy, yw, s1), mast);
            ui.Rect(new Rectangle(mx - yw / 2 + s1, yy + s1, yw - 2 * s1, ui.U(2)), sail);
            // Shrouds.
            ui.Line(new Vector2(mx, horizonY - mh + ui.U(3)), new Vector2(mx - ui.U(5), horizonY), mast * 0.6f, 1);
            ui.Line(new Vector2(mx, horizonY - mh + ui.U(3)), new Vector2(mx + ui.U(5), horizonY), mast * 0.6f, 1);
            // Masthead pennant.
            ui.Rect(new Rectangle(mx + s1, horizonY - mh, ui.U(5), ui.U(2)), Color.Lerp(cloth, c.Far, 0.35f));
        }
    }

    /// A standing guard in the faction's livery with steel helm, belted tabard, ground shadow and halberd/spear.
    void Guard(int x, int ground, TownLook l, TownColours c, Color cloth, bool halberd)
    {
        int s1 = Math.Max(1, ui.U(1));
        float dim = l.Night ? 0.55f : l.Dusk ? 0.8f : 1f;
        Color steel = Kit.Mul(new Color(182, 188, 198), dim);
        Color dark = Kit.Mul(new Color(52, 46, 44), dim);
        Color face = Kit.Mul(new Color(214, 170, 130), dim);
        // Ground shadow.
        ui.Rect(new Rectangle(x - ui.U(1), ground, ui.U(8), ui.U(2)), Color.Black * 0.3f);
        // Legs / boots.
        ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(4), ui.U(2), ui.U(4)), dark);
        ui.Rect(new Rectangle(x + ui.U(4), ground - ui.U(4), ui.U(2), ui.U(4)), dark);
        // Tabard & belt.
        ui.Rect(new Rectangle(x, ground - ui.U(12), ui.U(6), ui.U(8)), cloth);
        ui.Rect(new Rectangle(x, ground - ui.U(7), ui.U(6), s1), dark);
        ui.Rect(new Rectangle(x + ui.U(2), ground - ui.U(11), ui.U(2), ui.U(3)), Color.Lerp(cloth, Kit.Gilt, 0.45f));
        // Head & kettle helm.
        ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(16), ui.U(4), ui.U(4)), face);
        ui.Rect(new Rectangle(x, ground - ui.U(17), ui.U(6), ui.U(2)), steel);
        ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(18), ui.U(4), s1), Color.Lerp(steel, c.Rim, 0.35f));
        // Halberd or spear.
        int px = x + ui.U(6);
        ui.Rect(new Rectangle(px, ground - ui.U(24), s1, ui.U(24)), Kit.Mul(new Color(110, 78, 48), dim));
        ui.Rect(new Rectangle(px, ground - ui.U(28), s1, ui.U(4)), steel);
        if (halberd) ui.Rect(new Rectangle(px + s1, ground - ui.U(26), ui.U(3), ui.U(3)), steel);
    }

    /// A tethered warhorse with arched neck, mane, ears, hooves, flowing tail, crimson caparison and hitching post (facing right).
    void Warhorse(int x, int ground, TownLook l, TownColours c, Color cloth)
    {
        int s1 = Math.Max(1, ui.U(1));
        float dim = l.Night ? 0.55f : l.Dusk ? 0.8f : 1f;
        Color coat = Kit.Mul(new Color(124, 78, 46), dim);
        Color coatHi = Color.Lerp(coat, c.Rim, l.Night ? 0.2f : 0.28f);
        Color coatShade = Kit.Mul(coat, 0.72f);
        Color mane = Kit.Mul(new Color(44, 30, 24), dim);
        Color hoof = Kit.Mul(new Color(34, 28, 26), dim);
        Color leather = Kit.Mul(new Color(74, 46, 28), dim);
        Color wood = Kit.Mul(new Color(108, 76, 48), dim);
        Color gilt = Color.Lerp(cloth, Kit.Gilt, 0.55f);

        // Ground shadow.
        ui.Rect(new Rectangle(x + ui.U(1), ground, ui.U(18), ui.U(2)), Color.Black * 0.3f);

        // Hitching post to the right.
        int postX = x + ui.U(24);
        ui.Rect(new Rectangle(postX, ground - ui.U(13), ui.U(2), ui.U(13)), wood);
        ui.Rect(new Rectangle(postX - s1, ground - ui.U(13), ui.U(4), ui.U(2)), Kit.Mul(wood, 1.15f));

        // Flowing tail (left).
        ui.Rect(new Rectangle(x - ui.U(1), ground - ui.U(14), ui.U(2), ui.U(5)), mane);
        ui.Rect(new Rectangle(x - ui.U(2), ground - ui.U(10), ui.U(2), ui.U(5)), mane);

        // Back-side legs (slightly darker, offset for natural stance).
        ui.Rect(new Rectangle(x + ui.U(4), ground - ui.U(8), ui.U(2), ui.U(7)), coatShade);
        ui.Rect(new Rectangle(x + ui.U(4), ground - ui.U(2), ui.U(2), ui.U(2)), hoof);
        ui.Rect(new Rectangle(x + ui.U(13), ground - ui.U(8), ui.U(2), ui.U(7)), coatShade);
        ui.Rect(new Rectangle(x + ui.U(13), ground - ui.U(2), ui.U(2), ui.U(2)), hoof);

        // Front-side legs (tapered thigh/shoulder to canon bone & hoof).
        ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(10), ui.U(3), ui.U(4)), coat);
        ui.Rect(new Rectangle(x + ui.U(2), ground - ui.U(6), ui.U(2), ui.U(5)), coat);
        ui.Rect(new Rectangle(x + ui.U(2), ground - ui.U(2), ui.U(2), ui.U(2)), hoof);
        ui.Rect(new Rectangle(x + ui.U(14), ground - ui.U(10), ui.U(3), ui.U(4)), coat);
        ui.Rect(new Rectangle(x + ui.U(15), ground - ui.U(6), ui.U(2), ui.U(5)), coat);
        ui.Rect(new Rectangle(x + ui.U(15), ground - ui.U(2), ui.U(2), ui.U(2)), hoof);

        // Barrel / torso (muscular hindquarters, chest, belly shadow & top highlight).
        ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(16), ui.U(16), ui.U(7)), coat);
        ui.Rect(new Rectangle(x, ground - ui.U(15), ui.U(2), ui.U(5)), coat); // rounded croup
        ui.Rect(new Rectangle(x + ui.U(16), ground - ui.U(15), ui.U(2), ui.U(6)), coat); // chest
        ui.Rect(new Rectangle(x + ui.U(2), ground - ui.U(10), ui.U(14), ui.U(1)), coatShade); // belly shade
        ui.Rect(new Rectangle(x + ui.U(2), ground - ui.U(16), ui.U(13), s1), coatHi); // back highlight

        // Crimson caparison / saddlecloth & leather saddle.
        ui.Rect(new Rectangle(x + ui.U(4), ground - ui.U(16), ui.U(10), ui.U(7)), cloth);
        ui.Rect(new Rectangle(x + ui.U(4), ground - ui.U(10), ui.U(10), s1), gilt); // gold hem
        ui.Rect(new Rectangle(x + ui.U(6), ground - ui.U(17), ui.U(6), ui.U(2)), leather); // saddle
        ui.Rect(new Rectangle(x + ui.U(6), ground - ui.U(18), ui.U(2), s1), leather); // cantle
        ui.Rect(new Rectangle(x + ui.U(11), ground - ui.U(18), s1, s1), leather); // pommel

        // Arched neck & dark mane.
        ui.Rect(new Rectangle(x + ui.U(14), ground - ui.U(20), ui.U(4), ui.U(5)), coat);
        ui.Rect(new Rectangle(x + ui.U(16), ground - ui.U(22), ui.U(4), ui.U(4)), coat);
        ui.Rect(new Rectangle(x + ui.U(13), ground - ui.U(20), ui.U(2), ui.U(5)), mane);
        ui.Rect(new Rectangle(x + ui.U(15), ground - ui.U(23), ui.U(2), ui.U(3)), mane);

        // Horse head: forehead, angled muzzle, pointed ears, eye & leather bridle.
        ui.Rect(new Rectangle(x + ui.U(17), ground - ui.U(24), ui.U(4), ui.U(3)), coat);
        ui.Rect(new Rectangle(x + ui.U(20), ground - ui.U(22), ui.U(3), ui.U(3)), coat); // muzzle
        ui.Rect(new Rectangle(x + ui.U(22), ground - ui.U(21), s1, ui.U(2)), coatShade); // nose
        ui.Rect(new Rectangle(x + ui.U(17), ground - ui.U(26), s1, ui.U(2)), coat); // back ear
        ui.Rect(new Rectangle(x + ui.U(19), ground - ui.U(26), s1, ui.U(2)), coatHi); // front ear
        ui.Rect(new Rectangle(x + ui.U(19), ground - ui.U(23), s1, s1), hoof); // eye
        ui.Rect(new Rectangle(x + ui.U(20), ground - ui.U(21), ui.U(3), s1), leather); // noseband
        // Reins tied to the hitching post.
        ui.Line(new Vector2(x + ui.U(21), ground - ui.U(20)), new Vector2(postX + s1, ground - ui.U(11)), leather, 1);
    }
}
