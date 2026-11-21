using Microsoft.Xna.Framework;

namespace Ironfield;

/// A place under siege and after (docs/archive/SETTLEMENTS.md SM5): the besiegers' camp on the ground its plan keeps for one
/// (`WorldMap.LayCamp`) with their tents in their colours, fires smoking and the engines they have ready; battered walls
/// with fallen tower caps, rubble at their foot and at worst a breach; a raided village's burnt houses. Drawing only.
sealed partial class World
{
    static readonly Color Charred = new(52, 45, 40), Ember = new(226, 110, 40);

    /// Whether tower i of a battered place has lost its cap and top: a quarter of them when knocked about, half when badly.
    static bool Broken(PlaceState s, int i) => s.Battered > 0 && (uint)((i + 3) * 2654435761u >> 11) % (s.Battered == 1 ? 4u : 2u) == 0;

    /// A tower's cap as the battering leaves it (broken towers end in their ragged merlons).
    static Cap CapOf(PlaceState s, int i, Cap cap) => Broken(s, i) ? Cap.Merlons : cap;

    /// A tower's height as the battering leaves it.
    static float TopOf(PlaceState s, int i) => Broken(s, i) ? 0.78f : 1;

    /// Which edge of an n-sided wall a breach opens in (one that carries no gate: never the last, the front).
    static int BreachEdge(PlacePlan p, int n) => n < 3 ? -1 : (int)((uint)(p.Seed * 2654435761u >> 13) % (uint)(n - 1));

    /// A curtain from a to b, breached in its middle when `breach`: two stretches with a gap the width of a cart, rubble in it.
    /// The whole curtain is always built (into a scratch mesh when breached) so the dice run as they would unbroken.
    void Curtain(Mesh m, PlaceStyle st, Vector2 a, Vector2 b, Vector2 inside, float h, float thick, Color col, float u, Random r, bool breach)
    {
        if (!breach) { Curtain(m, st, a, b, inside, h, thick, col, u, r); return; }
        Curtain(new Mesh(), st, a, b, inside, h, thick, col, u, r);
        var br = new Random((int)(a.X * 31 + b.Y * 17));
        float len = Vector2.Distance(a, b), gap = MathF.Min(1.7f * u, len * 0.4f);
        var dir = (b - a) / MathF.Max(len, 0.01f);
        var mid = (a + b) / 2;
        Curtain(m, st, a, mid - dir * (gap / 2), inside, h, thick, col, u, br);
        Curtain(m, st, mid + dir * (gap / 2), b, inside, h, thick, col, u, br);
        Rubble(m, st, mid, gap * 0.75f, br);
        Rubble(m, st, mid + Vector2.Normalize(mid - inside) * (0.9f * u), gap * 0.55f, br);
    }

    /// A heap of fallen stone at the foot of a wall.
    void Rubble(Mesh m, PlaceStyle st, Vector2 at, float size, Random r)
    {
        var stone = st.Defence == Defence.Palisade ? Weathered(st, r) : Kit.Mul(st.Stone, 0.9f);
        float y = t.Height(at);
        m.Tag = Vector2.Zero;
        m.Lump(new Vector3(at.X, y + size * 0.05f, at.Y), new Vector3(size * 0.6f, size * 0.3f, size * 0.6f), stone, r, 0.3f);
        for (int i = 0; i < 4; i++)
        {
            var q = at + new Vector2(r.NextSingle() - 0.5f, r.NextSingle() - 0.5f) * size * 1.2f;
            m.Box(At(q, t.Height(q) - 0.05f), 0.22f + r.NextSingle() * 0.2f, 0.18f + r.NextSingle() * 0.15f, 0.15f + r.NextSingle() * 0.12f, r.NextSingle() * 3, Jitter(stone, r, 0.08f));
        }
    }

    /// Rubble at the foot of each broken tower of a ring, outside it.
    void TowerHeaps(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, Vector2[] ring, float u, bool[]? towers = null)
    {
        if (s.Battered == 0) return;
        var r = new Random(p.Seed ^ 0x6c1);
        for (int i = 0; i < ring.Length; i++)
            if (Broken(s, i) && (towers == null || towers[i])) Rubble(m, st, ring[i] + Vector2.Normalize(ring[i] - p.Pos + new Vector2(1e-5f, 0)) * (1.1f * u), 0.9f * u, r);
    }

    /// Rubble at the foot of a battered hold's broken towers (the small holds and the timber peoples, whose walls the kit
    /// does not break).
    void BatteredHeaps(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, float radius)
    {
        if (s.Battered == 0) return;
        var r = new Random(p.Seed ^ 0x2b7);
        int heaps = s.Battered * 2;
        for (int i = 0; i < heaps; i++)
        {
            var at = p.Pos + Dir2(p.Front + 0.9f + i * 2.39996f) * (radius + 0.4f);
            Rubble(m, st, at, 0.9f, r);
        }
    }

    /// The besiegers' camp, while there is one: tents in their colours round the fires, a banner over the first fire, their
    /// ready engines on the spots facing the walls and the ram at the gate.
    void Siege(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s)
    {
        if (s.Besieger < 0) return;
        var side = Style.Sides[Math.Clamp(s.Besieger, 0, Style.Sides.Length - 1)];
        var r = new Random(p.Seed ^ 0x1e9e);
        var spots = new[] { 0, 1, 3 }.Where(b => (s.Engines >> b & 1) != 0).ToList(); // mangonel, trebuchet, tower: on the spots
        int engine = 0;
        bool banner = false;
        m.Tag = Vector2.Zero;
        foreach (var lot in p.Lots)
            switch (lot.Use)
            {
                case LotUse.Camp: Tent(m, lot, side, r); break;
                case LotUse.Fire:
                {
                    var y = t.Height(lot.Pos);
                    for (int k = 0; k < 6; k++) m.Box(At(lot.Pos + Dir2(k * MathF.Tau / 6) * 0.22f, y - 0.03f), 0.12f, 0.12f, 0.08f, k, new Color(96, 92, 86));
                    m.Box(At(lot.Pos, y), 0.18f, 0.18f, 0.16f, 0.4f, Ember);
                    m.Fires.Add(At(lot.Pos, y + 0.15f));
                    if (!banner) { banner = true; Flags(m, s with { Palette = s.Besieger }, new[] { At(lot.Pos + Dir2(p.Front) * 0.7f, y) }, 1.6f, 0.9f, p.Front + 1.4f, 1); }
                    break;
                }
                case LotUse.Engine when engine < spots.Count:
                    switch (spots[engine++])
                    {
                        case 0: Mangonel(m, lot, r); break;
                        case 1: Trebuchet(m, lot, r); break;
                        default: SiegeTower(m, lot, side, r); break;
                    }
                    break;
            }
        if ((s.Engines & 4) != 0) // the ram on the road before the gate, its nose to it
        {
            var at = p.Doorstep;
            float yaw = p.Front + MathF.PI / 2;
            Ram(m, at, yaw, r);
        }
    }

    /// A tent: a ridge tent in the camp's cloth, or (one in four) a round pavilion with a trimmed peak.
    void Tent(Mesh m, Lot lot, Heraldry side, Random r)
    {
        var (lo, hi) = GroundUnder(lot.Pos, lot.W, lot.D, lot.Yaw);
        var b = At(lot.Pos, lo - 0.02f);
        var cloth = Jitter(Color.Lerp(side.Cloth, new Color(226, 218, 200), 0.5f), r, 0.04f);
        if (r.Next(4) == 0)
        {
            m.Drum(b, 0.55f, 0.55f, hi - lo + 0.5f, 8, lot.Yaw, cloth);
            m.Spike(At(lot.Pos, hi + 0.46f), 0.64f, 0.62f, 8, lot.Yaw, side.Cloth);
            m.Box(At(lot.Pos, hi + 1.05f), 0.03f, 0.03f, 0.3f, 0, side.Trim);
        }
        else m.GableRoof(b, lot.W * 0.95f, lot.D * 0.8f, hi - lo + 0.72f, lot.Yaw, cloth, Kit.Mul(cloth, 0.85f), 0);
    }

    /// A mangonel: a low frame, two uprights and its arm cocked back over the bed.
    void Mangonel(Mesh m, Lot lot, Random r)
    {
        var wood = Jitter(new Color(120, 92, 62), r, 0.05f);
        var (lo, hi) = GroundUnder(lot.Pos, 1.6f, 1.0f, lot.Yaw);
        var b = At(lot.Pos, hi);
        var ax = Dir3(lot.Yaw + MathF.PI / 2); var az = Dir3(lot.Yaw); // ax: toward the target (the lot's front)
        m.Box(At(lot.Pos, lo - 0.05f), 0.9f, 1.6f, hi - lo + 0.25f, lot.Yaw, wood);
        foreach (float sgn in new[] { -1f, 1f }) Beam(m, b + az * (0.35f * sgn) + Vector3.Up * 0.2f, b + az * (0.3f * sgn) + Vector3.Up * 0.9f, 0.09f, wood);
        Beam(m, b + Vector3.Up * 0.55f - ax * 0.75f, b + Vector3.Up * 1.25f + ax * 0.55f, 0.08f, Kit.Mul(wood, 0.85f));
    }

    /// A trebuchet: two A-frames on a sill, the long beam across them with its counterweight hung at the short end.
    void Trebuchet(Mesh m, Lot lot, Random r)
    {
        var wood = Jitter(new Color(116, 88, 58), r, 0.05f);
        var (lo, hi) = GroundUnder(lot.Pos, 2, 1.4f, lot.Yaw);
        var b = At(lot.Pos, hi);
        var ax = Dir3(lot.Yaw + MathF.PI / 2); var az = Dir3(lot.Yaw);
        m.Box(At(lot.Pos, lo - 0.05f), 0.25f, 1.8f, hi - lo + 0.18f, lot.Yaw, wood);
        m.Box(At(lot.Pos, lo - 0.05f), 1.8f, 0.25f, hi - lo + 0.18f, lot.Yaw, wood);
        var pivot = b + Vector3.Up * 2.0f;
        foreach (float sgn in new[] { -1f, 1f })
        {
            var foot = b + az * (0.5f * sgn);
            Beam(m, foot - ax * 0.75f, pivot + az * (0.5f * sgn), 0.1f, wood);
            Beam(m, foot + ax * 0.75f, pivot + az * (0.5f * sgn), 0.1f, wood);
        }
        var shortEnd = pivot + ax * 0.75f + Vector3.Down * 0.55f; // the weight end down, toward the walls; the long arm cocked up behind
        Beam(m, shortEnd, pivot - ax * 2.0f + Vector3.Up * 1.1f, 0.09f, Kit.Mul(wood, 0.85f));
        m.Box(shortEnd + Vector3.Down * 0.6f, 0.55f, 0.55f, 0.55f, lot.Yaw, new Color(92, 84, 74));
    }

    /// A siege tower: a tall timber frame faced with hides, on a wheeled base, its drawbridge raised.
    void SiegeTower(Mesh m, Lot lot, Heraldry side, Random r)
    {
        var wood = Jitter(new Color(110, 84, 56), r, 0.05f);
        var hide = Jitter(new Color(146, 112, 80), r, 0.06f);
        var (lo, hi) = GroundUnder(lot.Pos, 1.2f, 1.2f, lot.Yaw);
        m.Walls(At(lot.Pos, lo - 0.05f), 1.1f, 1.1f, hi - lo + 3.4f, lot.Yaw, hide);
        var top = At(lot.Pos, hi + 3.35f);
        m.Box(top, 1.25f, 1.25f, 0.12f, lot.Yaw, wood);
        foreach (float i in new[] { -0.55f, 0.55f })
            foreach (float j in new[] { -0.55f, 0.55f })
                m.Box(top + Dir3(lot.Yaw) * i + Dir3(lot.Yaw + MathF.PI / 2) * j, 0.1f, 0.1f, 0.4f, lot.Yaw, wood);
        var front = -new Vector3(-Dir3(lot.Yaw).Z, 0, Dir3(lot.Yaw).X);
        m.Box(At(lot.Pos, hi + 2.6f) + front * 0.58f, 1.0f, 0.06f, 0.8f, lot.Yaw, Kit.Mul(wood, 0.9f));
        m.Box(top + Vector3.Up * 0.4f, 0.02f, 0.02f, 0.7f, 0, wood);
        m.Box(top + Vector3.Up * 0.8f + Dir3(lot.Yaw) * 0.18f, 0.36f, 0.02f, 0.24f, lot.Yaw, side.Cloth);
    }

    /// A ram: an iron-nosed trunk under a pitched hide roof on a wheeled frame.
    void Ram(Mesh m, Vector2 at, float yaw, Random r)
    {
        var (lo, hi) = GroundUnder(at, 1.8f, 0.9f, yaw);
        var hide = Jitter(new Color(132, 100, 72), r, 0.06f);
        var b = At(at, hi);
        m.Box(At(at, lo - 0.05f), 1.8f, 0.8f, hi - lo + 0.3f, yaw, new Color(92, 70, 48));
        m.GableRoof(b + Vector3.Up * 0.3f, 1.8f, 0.85f, 0.5f, yaw, hide, Kit.Mul(hide, 0.85f), 0.05f);
        m.Box(b + Vector3.Up * 0.2f + Dir3(yaw) * 0.95f, 0.3f, 0.14f, 0.14f, yaw, new Color(70, 68, 66));
    }

    /// A square beam from a to b, t thick.
    static void Beam(Mesh m, Vector3 a, Vector3 b, float t, Color col)
    {
        var d = b - a;
        if (d.LengthSquared() < 1e-6f) return;
        d.Normalize();
        var side = Vector3.Cross(d, MathF.Abs(d.Y) > 0.9f ? Vector3.UnitX : Vector3.Up); side.Normalize();
        var up = Vector3.Cross(side, d);
        side *= t / 2; up *= t / 2;
        Vector3[] ca = { a - side - up, a + side - up, a + side + up, a - side + up }, cb = { b - side - up, b + side - up, b + side + up, b - side + up };
        var mid = (a + b) / 2;
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            m.Tri(ca[i], ca[j], cb[j], col, mid); m.Tri(ca[i], cb[j], cb[i], col, mid);
        }
    }
}
