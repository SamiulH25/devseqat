using Microsoft.Xna.Framework;

namespace Ironfield;

/// What a place's state adds to its model (docs/pre-demo/SETTLEMENTS.md SM4): the works its holder has built, each on the site its
/// plan keeps for it (`WorldMap.LayWorks`), a rich town's suburbs, a poor place's roofless houses. Drawing only.
sealed partial class World
{
    /// Whether the house on lot i stands roofless: a poor place loses one in four of its houses (a village one in three).
    static bool Ruined(PlaceState s, int i, bool village) => s.Wealth == 0 && (uint)(i * 2654435761u >> 9) % (village ? 3u : 4u) == 0;

    /// How much a place's walls rise with their level (each level a little higher, towers with them).
    static float Rise(PlaceState s) => 1 + 0.12f * s.Walls;

    /// How many of a town's market stalls stand: two when poor, four, all six when rich.
    static int StallsUp(PlaceState s) => s.Wealth switch { 0 => 2, 1 => 4, _ => 6 };

    /// A house left to fall: its walls broken to uneven heights, no roof, a beam fallen across.
    void Ruin(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        var (lo, hi) = GroundUnder(lot.Pos, lot.W, lot.D, lot.Yaw);
        var wall = Kit.Mul(Jitter(st.Washes is { Length: > 0 } w ? w[r.Next(w.Length)] : st.Wall, r, 0.05f), 0.78f);
        m.Tag = Vector2.Zero;
        float t = 0.14f;
        var b = At(lot.Pos, lo - 0.1f);
        var ax = Dir3(lot.Yaw); var az = new Vector3(-ax.Z, 0, ax.X);
        float H() => hi - lo + 0.1f + 0.25f + r.NextSingle() * 0.6f;
        m.Box(b - az * (lot.D / 2 - t / 2), lot.W, t, H(), lot.Yaw, wall);
        m.Box(b + az * (lot.D / 2 - t / 2), lot.W * (0.4f + r.NextSingle() * 0.5f), t, H(), lot.Yaw, wall);
        m.Box(b - ax * (lot.W / 2 - t / 2), t, lot.D, H(), lot.Yaw, wall);
        m.Box(b + ax * (lot.W / 2 - t / 2), t, lot.D * 0.6f, H() * 0.7f, lot.Yaw, wall);
        m.Box(b + Vector3.Up * (hi - lo + 0.12f), lot.W * 0.8f, 0.09f, 0.09f, lot.Yaw + 0.5f, Kit.Mul(Weathered(st, r), 0.7f));
    }

    /// The works a town or castle has built, each on its site.
    void Works(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s)
    {
        int k = 0;
        foreach (var lot in p.Lots)
        {
            if (lot.Use != LotUse.Works) continue;
            int i = k++;
            if (lot.W <= 0 || i >= PlaceState.WorkIds.Length || !s.Has(i)) continue;
            var r = new Random(p.Seed + i * 977);
            m.Tag = Vector2.Zero;
            switch (PlaceState.WorkIds[i])
            {
                case "barracks": Range(m, st, lot, 1.15f, r, chimneys: true); break;
                case "granary": Barn(m, st, lot, r); break;
                case "yard": Tiltyard(m, st, lot, r); break;
                case "stables": Stables(m, st, lot, r); break;
                case "chapel": Chapel(m, st, lot.Pos + lot.Along * (0.475f * 0.95f), lot.Yaw, 0.95f, r); break;
                case "kennel": Kennel(m, st, lot, r); break;
                case "watchtower": Watchtower(m, st, s, lot, r); break;
                case "market": MarketHall(m, st, lot, r); break;
            }
        }
        m.Tag = Vector2.Zero;
    }

    /// A long range (barracks): the people's stone, hall or cube house drawn long and low.
    void Range(Mesh m, PlaceStyle st, Lot lot, float h, Random r, bool chimneys)
    {
        var kind = st.House is HouseKind.Cube ? HouseKind.Cube : st.Defence is Defence.Palisade ? HouseKind.Hall : HouseKind.Stone;
        Dwelling(m, kind, st with { Washes = null, Wall = Kit.Mul(st.Defence is Defence.Palisade ? st.Wall : st.Stone, 0.97f) }, lot.Pos, lot.W, lot.D, h, lot.Yaw, r, chimney: chimneys);
    }

    /// A tithe barn (granary): tall walls under a steep roof, great doors on the long side; a fortified store where the people
    /// build in mud brick.
    void Barn(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        if (st.House is HouseKind.Cube) { Dwelling(m, HouseKind.Cube, st with { Washes = null, Wall = st.Stone }, lot.Pos, lot.W, lot.D, 1.7f, lot.Yaw, r); return; }
        var (lo, hi) = GroundUnder(lot.Pos, lot.W, lot.D, lot.Yaw);
        bool stone = st.Defence is Defence.Stone or Defence.Brick;
        var wall = stone ? Jitter(st.Stone, r, 0.04f) : Weathered(st, r);
        m.Tag = stone ? Courses : Vector2.Zero;
        m.Walls(At(lot.Pos, lo - 0.15f), lot.W, lot.D, hi - lo + 1.25f, lot.Yaw, wall);
        m.Tag = Vector2.Zero;
        var eaves = At(lot.Pos, hi + 1.1f);
        m.GableRoof(eaves, lot.W, lot.D, 1.25f, lot.Yaw, Jitter(st.RoofColour, r, 0.05f), wall, 0.14f);
        var door = At(lot.Pos, hi) - new Vector3(-Dir3(lot.Yaw).Z, 0, Dir3(lot.Yaw).X) * (lot.D / 2 + 0.02f);
        m.Box(door, 0.7f, 0.05f, 0.85f, lot.Yaw, Gap);
    }

    /// A training yard: a railed list with the tilt down its middle, pells to cut at, a shed for the gear.
    void Tiltyard(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        var wood = Weathered(st, r);
        var c = lot.Corners;
        for (int e = 0; e < 4; e++) Rails(m, c[e], c[(e + 1) % 4], wood, e == 0 ? 0.35f : 0);
        Rails(m, lot.Pos - lot.Along * (lot.W * 0.38f), lot.Pos + lot.Along * (lot.W * 0.38f), Kit.Mul(wood, 0.85f), 0, tilt: true);
        for (int j = 0; j < 3; j++)
        {
            var at = lot.Pos + lot.Along * (lot.W * 0.42f) + lot.Across * ((j - 1) * 0.5f);
            m.Box(At(at, t.Height(at) - 0.05f), 0.11f, 0.11f, 0.75f, lot.Yaw, Kit.Mul(wood, 0.75f));
        }
        var shed = new Lot(lot.Pos - lot.Along * (lot.W / 2 + 0.55f), lot.Yaw, 0.9f, 0.9f, LotUse.Works);
        Dwelling(m, HouseKind.Timber, st, shed.Pos, 0.9f, 0.9f, 0.7f, lot.Yaw, r);
    }

    /// Stables: a long low stable range at the back of the site, a railed paddock before it.
    void Stables(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        var back = lot.Pos - lot.Across * (lot.D / 2 - 0.5f);
        Dwelling(m, st.Defence is Defence.Palisade or Defence.Mud ? HouseKind.Hall : HouseKind.Timber, st with { Washes = null }, back, lot.W, 0.95f, 0.8f, lot.Yaw, r);
        var wood = Weathered(st, r);
        Vector2 f0 = lot.Pos + lot.Across * (lot.D / 2) - lot.Along * (lot.W / 2), f1 = lot.Pos + lot.Across * (lot.D / 2) + lot.Along * (lot.W / 2);
        Vector2 b0 = back + lot.Across * 0.55f - lot.Along * (lot.W / 2), b1 = back + lot.Across * 0.55f + lot.Along * (lot.W / 2);
        Rails(m, b0, f0, wood, 0); Rails(m, f0, f1, wood, 0.3f); Rails(m, f1, b1, wood, 0);
    }

    /// A kennel: a low shed and a fenced run beside it.
    void Kennel(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        var shed = lot.Pos - lot.Along * (lot.W / 2 - 0.55f);
        Dwelling(m, HouseKind.Timber, st with { Washes = null }, shed, 1.0f, lot.D * 0.7f, 0.6f, lot.Yaw, r);
        var wood = Weathered(st, r);
        Vector2 a = shed + lot.Along * 0.55f, run = lot.Pos + lot.Along * (lot.W / 2);
        Vector2 a0 = a - lot.Across * (lot.D * 0.4f), a1 = a + lot.Across * (lot.D * 0.4f), r0 = run - lot.Across * (lot.D * 0.4f), r1 = run + lot.Across * (lot.D * 0.4f);
        Rails(m, a0, r0, wood, 0); Rails(m, r0, r1, wood, 0); Rails(m, r1, a1, wood, 0);
    }

    /// A watchtower out from the place: the people's tower, tall and slim, its flag up.
    void Watchtower(Mesh m, PlaceStyle st, PlaceState s, Lot lot, Random r)
    {
        Vector3 top;
        if (st.Defence == Defence.Palisade) top = TimberTower(m, st, lot.Pos, 0.45f, 3.2f * st.Tall, lot.Yaw, 1, r);
        else top = Tower(m, st, lot.Pos, 0.5f, 3.6f * st.Tall, lot.Yaw, st.Round, st.Cap, WallColour(st, r), Coursed(st), 1);
        Flags(m, s, new[] { top + Vector3.Up * (st.Cap is Cap.Cone or Cap.Pyramid ? 1.2f : 0.05f) }, 0.9f, 0.7f, lot.Yaw + 1.2f, 1);
    }

    /// A market hall on the square: an open arcade of posts carrying an upper floor under a hipped roof.
    void MarketHall(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        var (lo, hi) = GroundUnder(lot.Pos, lot.W, lot.D, lot.Yaw);
        var wood = Kit.Mul(Jitter(st.Wood, r, 0.05f), 0.9f);
        var b = At(lot.Pos, lo - 0.05f);
        var ax = Dir3(lot.Yaw); var az = new Vector3(-ax.Z, 0, ax.X);
        float post = hi - lo + 0.75f;
        foreach (float i in new[] { -0.5f, 0, 0.5f })
            foreach (float j in new[] { -0.5f, 0.5f })
                m.Box(b + ax * (i * (lot.W - 0.12f)) + az * (j * (lot.D - 0.12f)), 0.12f, 0.12f, post, lot.Yaw, wood);
        var floor = b + Vector3.Up * post;
        var wall = st.Washes is { Length: > 0 } w ? Jitter(w[r.Next(w.Length)], r, 0.04f) : Jitter(st.Wall, r, 0.04f);
        m.Box(floor, lot.W + 0.1f, lot.D + 0.1f, 0.08f, lot.Yaw, wood);
        m.Walls(floor + Vector3.Up * 0.08f, lot.W, lot.D, 0.55f, lot.Yaw, wall);
        m.HipRoof(floor + Vector3.Up * 0.63f, lot.W, lot.D, 0.55f, lot.Yaw, Jitter(st.RoofColour, r, 0.05f), 0.12f);
    }

    /// A rail fence from a to b: posts about every 0.7 and a rail between each pair (`gap`: a share of it left open in the
    /// middle, a gate); `tilt` makes the solid barrier of a tiltyard.
    void Rails(Mesh m, Vector2 a, Vector2 b, Color wood, float gap, bool tilt = false)
    {
        float len = Vector2.Distance(a, b);
        int n = Math.Max(1, (int)MathF.Ceiling(len / 0.7f));
        float yaw = MathF.Atan2(b.Y - a.Y, b.X - a.X);
        for (int i = 0; i <= n; i++)
        {
            var q = Vector2.Lerp(a, b, i / (float)n);
            if (!tilt) m.Box(At(q, t.Height(q) - 0.05f), 0.07f, 0.07f, 0.45f, yaw, wood);
            if (i == n) continue;
            float mid = (i + 0.5f) / n;
            if (gap > 0 && MathF.Abs(mid - 0.5f) < gap / 2) continue;
            var c = Vector2.Lerp(a, b, mid);
            float y = (t.Height(Vector2.Lerp(a, b, i / (float)n)) + t.Height(Vector2.Lerp(a, b, (i + 1) / (float)n))) / 2;
            m.Box(At(c, y + (tilt ? -0.05f : 0.28f)), len / n + 0.04f, tilt ? 0.08f : 0.05f, tilt ? 0.42f : 0.06f, yaw, wood);
        }
    }
}
