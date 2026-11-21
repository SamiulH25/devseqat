using Microsoft.Xna.Framework;

namespace Ironfield;

/// The campaign map's towns (docs/pre-demo/SETTLEMENTS.md SM2), raised from their laid-out plans (`WorldMap.LayTown`): the wall round
/// them with its towers and gatehouses, the houses lot by lot in their people's way, the landmark on the square, the market
/// stalls, the port's warehouses and crane, the fens' windmill. Drawing only.
sealed partial class World
{
    /// A town as its plan lays it out (the old model where a plan has no wall: never on today's maps).
    void TownPlace(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, Random r)
    {
        if (p.Ring.Length < 3) { Town(m, At(p.Pos, t.Height(p.Pos)), 2.5f, r); return; }
        var tops = TownWall(m, p, st, s, r);
        int n = 0, stalls = 0;
        foreach (var lot in p.Lots)
        {
            int i = n++;
            var lr = new Random(p.Seed + i * 7919); // each lot its own dice: a house looks the same whatever the state shows elsewhere
            switch (lot.Use)
            {
                case LotUse.Works: continue;
                case LotUse.Suburb: if (s.Wealth == 2) Cottage(m, st, lot, lr, chimney: i % 3 == 0); continue;
                case LotUse.Stall when stalls++ >= StallsUp(s): continue;
                case LotUse.House when Ruined(s, i, village: false): Ruin(m, st, lot, lr); continue;
            }
            Building(m, p, st, lot, lr);
        }
        Works(m, p, st, s);
        Siege(m, p, st, s);
        Flags(m, s, tops, 1.1f, 0.9f, p.Front + 2.2f, 1);
    }

    /// The town wall: a tower at each corner clear of the gates, curtains between cut where the gates stand, a gatehouse at
    /// each gate (a timber gate tower in a palisade). Returns the gates' tops for the flags.
    List<Vector3> TownWall(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, Random r)
    {
        const float u = 1;
        var ring = p.Ring;
        int n = ring.Length;
        bool timber = st.Defence == Defence.Palisade;
        float wallH = (timber ? 2.0f : 2.2f) * Rise(s), thick = 0.5f, towerR = st.Round ? 0.78f : 0.7f;
        float gateHalf = timber ? 1.0f : 1.05f + towerR * 1.1f;
        var col = WallColour(st, r);
        bool coursed = Coursed(st);
        var tower = new bool[n];
        for (int i = 0; i < n; i++) tower[i] = p.Gates.All(g => Vector2.Distance(g.At, ring[i]) > gateHalf + towerR + 0.6f);
        for (int i = 0; i < n; i++)
        {
            if (!tower[i]) continue;
            float outward = MathF.Atan2(ring[i].Y - p.Pos.Y, ring[i].X - p.Pos.X);
            if (timber) { if (i % 2 == 0) TimberTower(m, st, Vector2.Lerp(ring[i], p.Pos, 0.05f), 0.5f * u, wallH * 1.55f * st.Tall, outward, u, r); }
            else Tower(m, st, ring[i], towerR, wallH * 1.45f * st.Tall * TopOf(s, i), outward, st.Round, CapOf(s, i, st.Cap), col, coursed, u);
        }
        // A breach (SM5) opens in the longest stretch of one edge, chosen by the plan's seed among those it could be.
        int breachAt = s.Battered == 3 ? (int)((uint)(p.Seed * 2654435761u >> 13) % (uint)n) : -1;
        for (int e = 0; e < n; e++)
        {
            Vector2 a = ring[e], b = ring[(e + 1) % n];
            float len = Vector2.Distance(a, b);
            if (len < 0.3f) continue;
            var dir = (b - a) / len;
            var cuts = new List<(float From, float To)>();
            if (tower[e] && !timber) cuts.Add((-1, towerR * 0.6f));
            if (tower[(e + 1) % n] && !timber) cuts.Add((len - towerR * 0.6f, len + 1));
            foreach (var g in p.Gates)
            {
                // Cut the stretch of this edge nearer the gate than its half-width (a gate by a corner cuts both edges).
                float along = Vector2.Dot(g.At - a, dir), perp = MathF.Abs(Vector2.Dot(g.At - a, new Vector2(-dir.Y, dir.X)));
                if (perp >= gateHalf) continue;
                float reach = MathF.Sqrt(gateHalf * gateHalf - perp * perp);
                cuts.Add((along - reach, along + reach));
            }
            cuts.Sort();
            float from = 0;
            var pieces = new List<(float From, float To)>();
            foreach (var (c0, c1) in cuts.Append((len + 2, len + 3)))
            {
                float to = MathF.Min(c0, len);
                if (to - from > 0.3f) pieces.Add((from, to));
                from = MathF.Max(from, c1);
            }
            int widest = e == breachAt && pieces.Count > 0 && pieces.Max(x => x.To - x.From) > 3.5f ? pieces.IndexOf(pieces.MaxBy(x => x.To - x.From)) : -1;
            if (e == breachAt && widest < 0) breachAt = (breachAt + 1) % n; // too short here: the next edge
            for (int k = 0; k < pieces.Count; k++) Curtain(m, st, a + dir * pieces[k].From, a + dir * pieces[k].To, p.Pos, wallH, thick, col, u, r, breach: k == widest);
        }
        if (!timber) TowerHeaps(m, p, st, s, ring, u, tower);
        else BatteredHeaps(m, p, st, s, ring.Min(v => Vector2.Distance(v, p.Pos)));
        var tops = new List<Vector3>();
        foreach (var g in p.Gates)
        {
            var gt = Gatehouse(m, st, g.At, g.Out, wallH, u, col, r, towerR * 0.85f);
            tops.Add(gt[0] + Vector3.Up * (timber || st.Cap is not (Cap.Cone or Cap.Pyramid) ? 0.1f : 1.4f));
        }
        return tops;
    }

    /// One lot's building.
    void Building(Mesh m, PlacePlan p, PlaceStyle st, Lot lot, Random r)
    {
        switch (lot.Use)
        {
            case LotUse.House:
            {
                // The steppe's towns keep a timber core among their yurts.
                bool core = st.House == HouseKind.Yurt;
                var kind = core ? HouseKind.Cabin : st.House;
                var look = core ? st with { Wall = new Color(128, 100, 70), RoofColour = new Color(96, 84, 70), Washes = null } : st;
                float near = 1 - Math.Clamp(Vector2.Distance(lot.Pos, p.Square) / MathF.Max(p.Radius, 1), 0, 1); // taller toward the square
                Dwelling(m, kind, look, lot.Pos, lot.W, lot.D, TownHeight(kind, st, r) * (1 + 0.22f * near), lot.Yaw, r, chimney: r.Next(4) == 0, lean: true);
                break;
            }
            case LotUse.Yurt:
                Dwelling(m, HouseKind.Yurt, st, lot.Pos, lot.W, lot.D, 1.1f, lot.Yaw, r);
                break;
            case LotUse.Landmark:
            {
                float u = st.Landmark == Landmark.Cathedral ? 1.65f : 1.4f;
                float shift = st.Landmark switch
                {
                    Landmark.Mosque or Landmark.Minaret or Landmark.Bazaar or Landmark.Tomb or Landmark.OnionChurch => -0.42f,
                    Landmark.GreatYurt or Landmark.StaveHall => 0,
                    _ => 0.475f,
                };
                Chapel(m, st, lot.Pos + lot.Along * (shift * u), lot.Yaw, u, r);
                break;
            }
            case LotUse.Warehouse:
                Dwelling(m, st.House is HouseKind.Cube ? HouseKind.Cube : st.Defence is Defence.Palisade ? HouseKind.Hall : HouseKind.Stone,
                    st with { Washes = null, Wall = Kit.Mul(st.Stone, 0.95f) }, lot.Pos, lot.W, lot.D, 1.2f, lot.Yaw, r, lean: true);
                break;
            case LotUse.Crane: Crane(m, st, lot, r); break;
            case LotUse.Stall: Stall(m, lot, r); break;
            case LotUse.Mill: Windmill(m, st, lot, r); break;
        }
    }

    /// How tall a people's town houses stand to the eaves.
    static float TownHeight(HouseKind kind, PlaceStyle st, Random r) => kind switch
    {
        HouseKind.Tall => 2.0f + r.NextSingle() * 0.8f,
        HouseKind.Timber => 1.25f + r.NextSingle() * 0.5f,
        HouseKind.Stone => 1.1f + r.NextSingle() * 0.35f,
        HouseKind.Stucco => (1.3f + r.NextSingle() * 0.6f) * MathF.Min(st.Tall, 1.15f),
        HouseKind.Cube => 0.95f + r.NextSingle() * 0.9f,
        HouseKind.Chalet => 1.4f + r.NextSingle() * 0.25f,
        HouseKind.Hall => 0.95f,
        _ => 1.05f + r.NextSingle() * 0.15f,
    };

    /// A harbour crane by the quay: a timber housing on the shore, its jib out over the water and a load on the rope.
    void Crane(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        var (lo, hi) = GroundUnder(lot.Pos, 1, 1, lot.Yaw);
        var wood = Weathered(st, r);
        var b = At(lot.Pos, hi);
        var outward = new Vector3(-lot.Across.X, 0, -lot.Across.Y);
        m.Tag = Vector2.Zero;
        m.Walls(At(lot.Pos, lo - 0.2f), 0.9f, 0.9f, hi - lo + 1.1f, lot.Yaw, wood);
        m.GableRoof(b + Vector3.Up * 0.9f, 0.9f, 0.9f, 0.45f, lot.Yaw, Kit.Mul(wood, 0.8f), wood, 0.08f);
        m.Box(b + Vector3.Up * 0.9f, 0.22f, 0.22f, 1.6f, lot.Yaw, Kit.Mul(wood, 0.9f));
        var jib = b + Vector3.Up * 2.35f + outward * 0.9f;
        m.Box(jib, 0.16f, 1.9f, 0.16f, lot.Yaw, Kit.Mul(wood, 0.9f));
        var end = b + Vector3.Up * 2.35f + outward * 1.8f;
        m.Panel(end - Vector3.Up * 0.6f, new Vector3(lot.Along.X, 0, lot.Along.Y), outward, 0.03f, 1.2f, Gap, 0);
        m.Box(end - Vector3.Up * 1.45f, 0.4f, 0.4f, 0.3f, lot.Yaw + 0.3f, new Color(120, 92, 60));
    }

    static readonly Color[] Awnings = { new(176, 70, 56), new(196, 160, 84), new(84, 120, 82), new(76, 98, 140), new(214, 204, 178) };

    /// A market stall: a trestle under a striped awning.
    void Stall(Mesh m, Lot lot, Random r)
    {
        var (_, hi) = GroundUnder(lot.Pos, lot.W, lot.D, lot.Yaw);
        var b = At(lot.Pos, hi + 0.32f); // on the road's level, where the square lies under the ribbon
        var ax = new Vector3(lot.Along.X, 0, lot.Along.Y); var az = new Vector3(lot.Across.X, 0, lot.Across.Y);
        m.Tag = Vector2.Zero;
        m.Box(b - Vector3.Up * 0.32f, lot.W * 0.9f, lot.D * 0.6f, 0.72f, lot.Yaw, new Color(118, 88, 58));
        var cloth = Awnings[r.Next(Awnings.Length)];
        Vector3 f0 = b - az * (lot.D * 0.55f) + Vector3.Up * 0.72f, back = b + az * (lot.D * 0.45f) + Vector3.Up * 0.9f, x = ax * (lot.W * 0.55f);
        var n = Vector3.Normalize(-az * 0.25f + Vector3.Up);
        for (int k = 0; k < 3; k++)
        {
            float s0 = -1 + k * 2 / 3f, s1 = -1 + (k + 1) * 2 / 3f;
            m.Quad(f0 + x * s0, f0 + x * s1, back + x * s1, back + x * s0, k % 2 == 0 ? cloth : new Color(226, 218, 196), n);
        }
        foreach (float s in new[] { -1f, 1f }) m.Panel(b - az * (lot.D * 0.5f) + x * (s * 0.95f) + Vector3.Up * 0.35f, ax, -az, 0.05f, 0.75f, new Color(90, 68, 46), 0);
    }

    /// A tower windmill (the fens): a tapering brick tower, a thatched cap, four sails toward the wind.
    void Windmill(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        var (lo, hi) = GroundUnder(lot.Pos, 1.2f, 1.2f, lot.Yaw);
        var foot = At(lot.Pos, lo - 0.3f);
        float h = hi - foot.Y + 2.8f;
        m.Tag = Courses;
        m.Drum(foot, 0.62f, 0.44f, h, 8, lot.Yaw, Jitter(new Color(150, 86, 64), r, 0.04f));
        m.Tag = Vector2.Zero;
        var top = foot + Vector3.Up * h;
        m.Spike(top, 0.58f, 0.75f, 8, lot.Yaw, Jitter(new Color(140, 122, 80), r, 0.05f));
        var face = new Vector3(-lot.Across.X, 0, -lot.Across.Y); var side = new Vector3(lot.Along.X, 0, lot.Along.Y);
        var hub = top + Vector3.Up * 0.2f + face * 0.62f;
        var sail = new Color(222, 212, 188); var frame = new Color(96, 74, 50);
        for (int k = 0; k < 4; k++)
        {
            float a = MathF.PI / 4 + k * MathHelper.PiOver2 + (float)(r.NextDouble() * 0.3);
            var arm = side * MathF.Cos(a) + Vector3.Up * MathF.Sin(a);
            var across = side * MathF.Cos(a + MathHelper.PiOver2) + Vector3.Up * MathF.Sin(a + MathHelper.PiOver2);
            Vector3 p0 = hub + arm * 0.2f, p1 = hub + arm * 1.7f;
            m.Quad(p0, p1, p1 + across * 0.34f, p0 + across * 0.34f, sail, face);
            m.Quad(p0 - across * 0.03f, p1 - across * 0.03f, p1 + across * 0.03f, p0 + across * 0.03f, frame, face);
        }
        m.Panel(foot + Vector3.Up * (hi - foot.Y + 0.35f) + face * 0.6f, side, face, 0.3f, 0.55f, Kit.Mul(st.Wood, 0.75f), 0.03f);
    }
}
