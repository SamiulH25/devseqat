using Microsoft.Xna.Framework;

namespace Ironfield;

/// Draws a siege fortress (`Terrain.Wall` = `Fortress`, docs/pre-demo/SIEGES.md SE1) on the battlefield:
/// closed wall ring(s) of rotated segments, breaches as rubble/logs, corner and gatehouse towers flying their owner's banner,
/// gatehouses, raised ladders, interior buildings (houses, keep, landmark) in the people's style, siege camp tents/fires, and torches.
sealed partial class World
{
    void BuildFortress(Mesh m)
    {
        if (t.Wall is not Fortress fort) return;
        var st = FieldStyle;
        var rng = new Random(t.Seed ^ 0x51e9);

        foreach (var seg in fort.Segments)
        {
            if (seg.IsGate) continue;
            if (seg.Breached) FortressBreach(m, seg, st, rng);
            else FortressWallSeg(m, seg, st, rng);
        }
        foreach (var tw in fort.Towers)
            FortressTower(m, tw, st, rng);
        foreach (var g in fort.Gates)
            FortressGateMesh(m, g, st, rng);
        foreach (var l in fort.Ladders)
            if (l.Up) FortressLadder(m, l, rng);
        foreach (var b in fort.Buildings)
            FortressBuildingMesh(m, b, st, fort.Kind);
        foreach (var c in fort.CampLots)
            FortressCampLot(m, c, rng);
        FortressTorches(m, fort);
    }

    void FortressWallSeg(Mesh m, WallSegment seg, PlaceStyle? st, Random r)
    {
        float h = seg.Height, d = Fortress.WalkwayDepth;
        // Centre of wall box sits d/2 behind the outer face along -seg.Out.
        var mid2 = seg.Mid - seg.Out * (d * 0.5f);
        float baseY = t.BaseHeight(seg.Mid + seg.Out * 1.0f);
        float topY = baseY + h;
        var c = new Vector3(mid2.X, baseY - 2f, mid2.Y);
        float yaw = seg.Yaw; // Mesh.Box ax = (cos(yaw), 0, sin(yaw)) = seg.Along

        bool palisade = seg.IsPalisade || st?.Defence == Defence.Palisade;
        if (palisade)
        {
            var wood = st != null ? Weathered(FieldWood(st), r) : Jitter(Timber, r, 0.06f);
            m.Tag = Vector2.Zero;
            // Earth rampart behind the logs.
            var bank2 = seg.Mid - seg.Out * (d * 0.55f);
            m.Box(new Vector3(bank2.X, baseY - 2f, bank2.Y), seg.Length + 0.1f, d - 0.7f, h + 2.04f, yaw, Jitter(Earthwork, r, 0.05f));
            // Palisade log wall along the outer face.
            var face2 = seg.Mid - seg.Out * 0.25f;
            float logTop = topY + 1.1f;
            m.Box(new Vector3(face2.X, baseY - 2f, face2.Y), seg.Length + 0.06f, 0.52f, logTop - (baseY - 2f), yaw, wood);
            int stakes = Math.Max(3, (int)(seg.Length / 0.65f));
            var tip = Kit.Mul(wood, 0.92f);
            for (int k = 0; k < stakes; k++)
            {
                float s0 = (k + 0.5f) / stakes * seg.Length;
                var sp = seg.A + seg.Along * s0 - seg.Out * 0.25f;
                float peak = 0.45f + 0.18f * r.NextSingle();
                m.Frustum(new Vector3(sp.X, logTop, sp.Y), new Vector3(sp.X, logTop + peak, sp.Y), 0.24f, 0.03f, 4, tip, r);
            }
            return;
        }

        var col = st != null ? WallColour(st, r) : Jitter(WallStone, r, 0.05f);
        var foot = st == null ? Jitter(Footing, r, 0.04f) : st.Defence == Defence.Brick ? Jitter(st.Stone, r, 0.04f) : Kit.Mul(col, 0.84f);
        m.Tag = st == null || Coursed(st) ? MasonryTag : Vector2.Zero;
        m.Box(c, seg.Length + 0.12f, d, h + 2.04f, yaw, col);
        var foot2 = seg.Mid - seg.Out * (d * 0.5f - 0.2f);
        m.Box(new Vector3(foot2.X, baseY - 2f, foot2.Y), seg.Length + 0.14f, d + 0.45f, 3.1f, yaw, foot);
        // Merlons along the outer parapet.
        bool swallow = st?.Cap == Cap.Swallow;
        for (float s = 0.8f; s < seg.Length - 0.5f; s += 1.65f)
        {
            var mp = seg.A + seg.Along * s - seg.Out * 0.35f;
            Merlon(m, new Vector3(mp.X, topY, mp.Y), 0.8f, 0.6f, 0.78f, yaw, Jitter(col, r, 0.03f), swallow);
        }
        m.Tag = Vector2.Zero;
    }

    void FortressBreach(Mesh m, WallSegment seg, PlaceStyle? st, Random r)
    {
        m.Tag = Vector2.Zero;
        bool palisade = seg.IsPalisade || st?.Defence == Defence.Palisade;
        if (palisade)
        {
            var wood = st != null ? Weathered(FieldWood(st), r) : Jitter(Timber, r, 0.06f);
            for (int i = 0; i < 7; i++)
            {
                float s = seg.Length * (0.15f + 0.7f * i / 6f);
                var q = seg.A + seg.Along * s + seg.Out * (0.6f + r.NextSingle() * 1.4f);
                var a = new Vector3(q.X, t.Height(q) + 0.2f, q.Y);
                var dir = seg.Out * (2.2f + r.NextSingle() * 1.4f) + seg.Along * ((r.NextSingle() - 0.5f) * 1.5f);
                m.Frustum(a, a + new Vector3(dir.X, 0.05f, dir.Y), 0.22f, 0.19f, 5, Jitter(wood, r, 0.06f), r, capBottom: true);
            }
            return;
        }
        var stone = st == null ? Jitter(WallStone, r, 0.05f) : st.Defence == Defence.Brick ? new Color(146, 90, 70) : Kit.Mul(WallColour(st, r), 0.92f);
        for (int i = 0; i < 5; i++)
        {
            float s = seg.Length * (0.15f + 0.7f * i / 4f);
            var q = seg.A + seg.Along * s + seg.Out * (0.4f - (i % 2) * 0.8f);
            float sz = 1.1f + 0.35f * (i % 3);
            m.Lump(new Vector3(q.X, t.Height(q) + sz * 0.12f, q.Y), new Vector3(sz * 1.1f, sz * 0.65f, sz), Jitter(stone, r, 0.05f), r, 0.3f);
        }
        for (int i = 0; i < 10; i++)
        {
            var q = seg.Mid + seg.Along * ((r.NextSingle() - 0.5f) * seg.Length * 0.85f) + seg.Out * (1.2f + r.NextSingle() * 2.6f);
            m.Box(At(q, t.Height(q) - 0.05f), 0.55f + r.NextSingle() * 0.4f, 0.45f + r.NextSingle() * 0.3f, 0.35f + r.NextSingle() * 0.25f, r.NextSingle() * 3, Jitter(stone, r, 0.08f));
        }
    }

    void FortressTower(Mesh m, WallTower tw, PlaceStyle? st, Random r)
    {
        float baseY = t.Height(tw.Pos + tw.Out * 2.5f);
        float yaw = MathF.Atan2(-tw.Out.X, tw.Out.Y);
        int pal = tw.Owner == 0 ? 0 : t.Params.Banner; // team 0 = player blue/gold, team 1 = defender banner
        if (st != null)
        {
            float rise = (t.Params.Castle ? 1.25f : 1f) * (tw.Silenced ? 0.65f : 1f);
            Vector3 top;
            if (st.Defence == Defence.Palisade)
                top = TimberTower(m, FieldWood(st), tw.Pos, 2.2f, 6f * rise * st.Tall, yaw, FieldU, r) + Vector3.Up * (2.2f * 1.8f * st.Tall - 0.3f);
            else
            {
                const float R = 2.5f;
                top = Tower(m, st, tw.Pos, R, 3.6f * rise * st.Tall, yaw, st.Round, st.Cap, WallColour(st, r), Coursed(st), FieldU);
                top += Vector3.Up * CapRise(st, R * (1 - st.Taper));
            }
            if (!tw.Silenced && pal >= 0 && pal < Style.Sides.Length && (tw.IsGateTower || tw.HasStairs))
                Flag(m, top, 2.2f, 1.7f, yaw, Style.Sides[pal], FieldU);
            return;
        }
        m.Tag = MasonryTag;
        float h = (Fortress.WallHeight + 5.5f) * (tw.Silenced ? 0.6f : 1f);
        var foot = new Vector3(tw.Pos.X, baseY - 2f, tw.Pos.Y);
        m.Box(foot, 4.4f, 4.8f, h + 2f, yaw, Jitter(WallStone, r, 0.04f));
        m.Box(foot, 4.9f, 5.3f, 3.2f, yaw, Jitter(Footing, r, 0.04f));
        m.Tag = Vector2.Zero;
    }

    void FortressGateMesh(Mesh m, FortressGate g, PlaceStyle? st, Random r)
    {
        float baseY = t.Height(g.Pos);
        float yaw = MathF.Atan2(g.Along.Y, g.Along.X);
        var b = new Vector3(g.Pos.X, baseY, g.Pos.Y);
        float w = g.HalfWidth * 2f + 0.6f;
        float h = Fortress.WallHeight;

        m.Tag = Vector2.Zero;
        if (!g.Open)
        {
            m.Box(b, w, 0.55f, h - 0.4f, yaw, new Color(Timber.ToVector3() * 0.7f));
            for (int i = 0; i < 3; i++)
            {
                var band = b + new Vector3(g.Out.X * 0.32f, 0.8f + i * 1.25f, g.Out.Y * 0.32f);
                m.Box(band, w - 0.2f, 0.08f, 0.2f, yaw, new Color(70, 72, 80));
            }
        }
        else if (g.Broken)
        {
            for (int i = 0; i < 5; i++)
            {
                var sp = g.Pos + g.Along * (r.NextSingle() * 5f - 2.5f) - g.Out * (1f + r.NextSingle() * 3f);
                m.Box(new Vector3(sp.X, t.Height(sp), sp.Y), 1.6f, 0.3f, 0.12f, r.NextSingle() * MathF.Tau, Timber);
            }
        }

        // Arch or timber lintel over the gate opening.
        var archPos = g.Pos - g.Out * 0.9f;
        var ab = new Vector3(archPos.X, baseY + h - 0.4f, archPos.Y);
        if (st?.Defence == Defence.Palisade)
            m.Box(ab, w + 1.4f, 1.4f, 1.1f, yaw, Weathered(FieldWood(st), r));
        else
        {
            m.Tag = st == null || Coursed(st) ? MasonryTag : Vector2.Zero;
            m.Box(ab, w + 1.6f, 2.6f, 1.3f, yaw, st == null ? WallStone : WallColour(st, r));
        }
        m.Tag = Vector2.Zero;
    }

    void FortressLadder(Mesh m, WallLadder l, Random r)
    {
        m.Tag = Vector2.Zero;
        var foot = new Vector3(l.Foot.X, t.BaseHeight(l.Foot), l.Foot.Y);
        var topPt = l.Pos - l.Out * 0.1f;
        var top = new Vector3(topPt.X, t.BaseHeight(l.Foot) + Fortress.WallHeight + 0.5f, topPt.Y);
        var side = new Vector3(-l.Out.Y, 0, l.Out.X);
        foreach (float s in new[] { -0.4f, 0.4f })
            m.Frustum(foot + side * s, top + side * s, 0.06f, 0.06f, 4, Timber, r);
        float yaw = MathF.Atan2(-l.Out.X, l.Out.Y);
        for (float k = 0.08f; k < 1f; k += 0.09f)
            m.Box(Vector3.Lerp(foot, top, k), 0.8f, 0.07f, 0.07f, yaw, Timber);
    }

    void FortressBuildingMesh(Mesh m, FortressBuilding b, PlaceStyle? st, FortressKind kind)
    {
        var r = new Random(b.Seed);
        var f = new Foliage(FoliageKind.House, b.Pos, 1f, b.Radius, b.Seed);
        if (b.Use == LotUse.Landmark)
        {
            bool isCastle = kind is FortressKind.Castle or FortressKind.GreatCastle or FortressKind.TowerHouse;
            var style = st ?? Architecture.Of("azure_heart");
            if (isCastle) FieldKeep(m, style, f, r);
            else FieldLandmark(m, style, f, r);
            return;
        }
        if (st != null)
        {
            var look = st.Defence == Defence.Palisade ? FieldWood(st) : st;
            var hk = kind == FortressKind.WalledVillage ? WorldMap.VillageKind(st) : st.House;
            float h = hk is HouseKind.Yurt ? 2.1f : TownHeight(hk, st, r) * HouseU;
            Dwelling(m, hk, look, b.Pos, b.W, b.D, h, b.Yaw, r, chimney: hk is not (HouseKind.Yurt or HouseKind.Tent) && r.Next(3) == 0);
        }
        else
        {
            var basePt = new Vector3(b.Pos.X, t.Height(b.Pos), b.Pos.Y);
            TownHouse(m, basePt, MathF.Max(b.W, b.D) / 5.5f, r);
        }
    }

    void FortressCampLot(Mesh m, FortressLotSpec c, Random r)
    {
        float y = t.Height(c.Pos);
        if (c.Use == LotUse.Fire)
        {
            m.Tag = Vector2.Zero;
            for (int i = 0; i < 4; i++)
                m.Box(new Vector3(c.Pos.X + (r.NextSingle() - 0.5f) * 0.6f, y, c.Pos.Y + (r.NextSingle() - 0.5f) * 0.6f), 0.6f, 0.16f, 0.14f, i * 0.8f, Timber);
            m.Tag = FlameTag;
            m.Frustum(new Vector3(c.Pos.X, y + 0.08f, c.Pos.Y), new Vector3(c.Pos.X, y + 0.95f, c.Pos.Y), 0.24f, 0, 5, new Color(255, 165, 55), r);
            m.Tag = Vector2.Zero;
            return;
        }
        // Siege camp tent.
        m.Tag = Vector2.Zero;
        var cloth = new Color(188, 178, 156);
        var roof = new Color(148, 136, 114);
        m.Box(new Vector3(c.Pos.X, y, c.Pos.Y), c.W * 0.75f, c.D * 0.75f, 1.6f, c.Yaw, Jitter(cloth, r, 0.06f));
        m.Frustum(new Vector3(c.Pos.X, y + 1.6f, c.Pos.Y), new Vector3(c.Pos.X, y + 2.9f, c.Pos.Y), MathF.Max(c.W, c.D) * 0.42f, 0.08f, 4, Jitter(roof, r, 0.05f), r);
    }

    void FortressTorches(Mesh m, Fortress fort)
    {
        var rng = new Random(5);
        foreach (var seg in fort.Segments)
        {
            if (seg.Ring != 0 || seg.IsGate || seg.Breached) continue;
            var p = seg.Mid + seg.Out * 0.08f;
            float top = t.Height(seg.Mid - seg.Out * 2f);
            var a = new Vector3(p.X, top - 1.2f, p.Y);
            var head = new Vector3(p.X + seg.Out.X * 0.45f, top + 0.35f, p.Y + seg.Out.Y * 0.45f);
            m.Tag = Vector2.Zero;
            m.Frustum(a, head, 0.05f, 0.06f, 4, Timber, rng);
            m.Box(head - Vector3.Up * 0.12f, 0.2f, 0.2f, 0.16f, 0, new Color(50, 46, 44));
            m.Tag = FlameTag;
            m.Frustum(head + Vector3.Up * 0.02f, head + Vector3.Up * 0.62f, 0.15f, 0, 5, new Color(255, 170, 60), rng);
            m.Frustum(head + Vector3.Up * 0.02f, head + Vector3.Up * 0.38f, 0.09f, 0, 4, new Color(255, 236, 150), rng);
        }
        m.Tag = Vector2.Zero;
    }
}
