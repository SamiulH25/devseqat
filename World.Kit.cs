using Microsoft.Xna.Framework;

namespace Ironfield;

/// The settlement kit (docs/SETTLEMENTS.md): the pieces every place is built of, in its people's style (`PlaceStyle`).
/// Detail is laid on as flat quads (a window or a beam is 2 triangles), so a town can hold a hundred houses. Drawing only.
sealed partial class World
{
    internal sealed partial class Mesh
    {
        /// A flat quad a-b-c-d (in order round its edge), lit as facing `n`.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector3 n)
        {
            V.Add(new(a, n, col, Tag)); V.Add(new(b, n, col, Tag)); V.Add(new(c, n, col, Tag));
            V.Add(new(a, n, col, Tag)); V.Add(new(c, n, col, Tag)); V.Add(new(d, n, col, Tag));
        }

        /// One triangle lit as facing `n`.
        public void Face(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 n)
        {
            V.Add(new(a, n, col, Tag)); V.Add(new(b, n, col, Tag)); V.Add(new(c, n, col, Tag));
        }

        /// A rectangle laid on a wall: `c` its middle, w along `along`, h up, facing `n`, lifted a little off the wall.
        public void Panel(Vector3 c, Vector3 along, Vector3 n, float w, float h, Color col, float lift = 0.025f)
        {
            Vector3 a = along * (w / 2), u = Vector3.Up * (h / 2), o = n * lift;
            Quad(c - a - u + o, c + a - u + o, c + a + u + o, c - a + u + o, col, n);
        }

        /// Four walls with no top or bottom (a roof covers them): w along the yaw, d across, h tall, standing on c.
        public void Walls(Vector3 c, float w, float d, float h, float yaw, Color col)
        {
            var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw)); var az = new Vector3(-ax.Z, 0, ax.X);
            Vector3 x = ax * (w / 2), z = az * (d / 2), up = Vector3.Up * h;
            Vector3 p0 = c - x - z, p1 = c + x - z, p2 = c + x + z, p3 = c - x + z;
            Quad(p0, p1, p1 + up, p0 + up, col, -az);
            Quad(p1, p2, p2 + up, p1 + up, col, ax);
            Quad(p2, p3, p3 + up, p2 + up, col, az);
            Quad(p3, p0, p0 + up, p3 + up, col, -ax);
        }

        /// A flat lid over a w x d footprint at c, turned by yaw.
        public void Lid(Vector3 c, float w, float d, float yaw, Color col)
        {
            var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw)) * (w / 2); var az = new Vector3(-ax.Z, 0, ax.X) * (d / w);
            Quad(c - ax - az, c - ax + az, c + ax + az, c + ax - az, col, Vector3.Up);
        }

        /// A prism of `sides` from radius r0 at c up to r1 at h, turned by `rot` (4 sides at rot = yaw + 45 degrees is a square
        /// tower); `top` closes it with a flat lid.
        public void Drum(Vector3 c, float r0, float r1, float h, int sides, float rot, Color col, bool top = true)
        {
            var up = Vector3.Up * h;
            float lean = (r0 - r1) / MathF.Max(h, 0.01f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = rot + i * MathF.Tau / sides, a1 = rot + (i + 1) * MathF.Tau / sides, am = (a0 + a1) / 2;
                Vector3 d0 = new(MathF.Cos(a0), 0, MathF.Sin(a0)), d1 = new(MathF.Cos(a1), 0, MathF.Sin(a1));
                var n = Vector3.Normalize(new Vector3(MathF.Cos(am), lean, MathF.Sin(am)));
                Quad(c + d0 * r0, c + d1 * r0, c + up + d1 * r1, c + up + d0 * r1, col, n);
                if (top) Face(c + up, c + up + d1 * r1, c + up + d0 * r1, col, Vector3.Up);
            }
        }

        /// A cone (many sides) or pyramid (four) of radius r at c, its point h above.
        public void Spike(Vector3 c, float r, float h, int sides, float rot, Color col)
        {
            var apex = c + Vector3.Up * h;
            for (int i = 0; i < sides; i++)
            {
                float a0 = rot + i * MathF.Tau / sides, a1 = rot + (i + 1) * MathF.Tau / sides, am = (a0 + a1) / 2;
                var n = Vector3.Normalize(new Vector3(MathF.Cos(am) * h, r * MathF.Cos(MathF.PI / sides), MathF.Sin(am) * h));
                Face(c + new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)) * r, c + new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1)) * r, apex, Shade(col, 0.04f, i), n);
            }
        }

        /// A dome of radius r at c rising h, in two bands and a crown.
        public void Dome(Vector3 c, float r, float h, int sides, float rot, Color col) =>
            Turned(c, new[] { (r, 0f), (r * 0.82f, h * 0.58f), (r * 0.42f, h * 0.92f), (0f, h) }, sides, rot, col);

        /// An onion dome: swelling out past its neck, then drawn to a point.
        public void Onion(Vector3 c, float r, float h, int sides, float rot, Color col) =>
            Turned(c, new[] { (r * 0.72f, 0f), (r, h * 0.28f), (r * 0.78f, h * 0.55f), (r * 0.3f, h * 0.8f), (0f, h) }, sides, rot, col);

        /// A shape turned on a lathe: rings of (radius, height) from the bottom up, closing on a point if the last radius is 0.
        public void Turned(Vector3 c, (float R, float Y)[] ring, int sides, float rot, Color col)
        {
            for (int k = 0; k + 1 < ring.Length; k++)
            {
                var (ra, ya) = ring[k]; var (rb, yb) = ring[k + 1];
                float slope = (ra - rb) / MathF.Max(yb - ya, 0.01f);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = rot + i * MathF.Tau / sides, a1 = rot + (i + 1) * MathF.Tau / sides, am = (a0 + a1) / 2;
                    Vector3 d0 = new(MathF.Cos(a0), 0, MathF.Sin(a0)), d1 = new(MathF.Cos(a1), 0, MathF.Sin(a1));
                    var n = Vector3.Normalize(new Vector3(MathF.Cos(am), slope, MathF.Sin(am)));
                    Vector3 p0 = c + d0 * ra + Vector3.Up * ya, p1 = c + d1 * ra + Vector3.Up * ya, q0 = c + d0 * rb + Vector3.Up * yb, q1 = c + d1 * rb + Vector3.Up * yb;
                    if (rb < 1e-4f) Face(p0, p1, q0, col, n);
                    else Quad(p0, p1, q1, q0, col, n);
                }
            }
        }

        /// A gable roof over a w x d footprint at c (eaves level): ridge along the yaw, h above the eaves, reaching `over` past
        /// the walls; the gable ends (at the wall line) in `ends`.
        public void GableRoof(Vector3 c, float w, float d, float h, float yaw, Color roof, Color ends, float over)
        {
            var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw)); var az = new Vector3(-ax.Z, 0, ax.X);
            float drop = over * h / MathF.Max(d / 2, 0.01f);
            Vector3 x = ax * (w / 2 + over * 0.6f), z = az * (d / 2 + over), sag = Vector3.Down * drop, up = Vector3.Up * h;
            Vector3 e0 = c - x - z + sag, e1 = c + x - z + sag, e2 = c + x + z + sag, e3 = c - x + z + sag, r0 = c - x + up, r1 = c + x + up;
            var nl = Vector3.Normalize(-az * h + Vector3.Up * (d / 2)); var nr = Vector3.Normalize(az * h + Vector3.Up * (d / 2));
            var save = Tag; // the ends wear the walls' tag (masonry), the roof none
            Tag = Vector2.Zero;
            Quad(e0, e1, r1, r0, roof, nl);
            Quad(e2, e3, r0, r1, Kit.Mul(roof, 0.97f), nr);
            Tag = save;
            Vector3 g = ax * (w / 2), dz = az * (d / 2);
            Face(c - g - dz, c - g + dz, c - g + up, ends, -ax);
            Face(c + g + dz, c + g - dz, c + g + up, ends, ax);
        }

        /// A hip roof: sloping on all four sides to a ridge along the longer side (a point on a square).
        public void HipRoof(Vector3 c, float w, float d, float h, float yaw, Color roof, float over)
        {
            if (d > w) { (w, d) = (d, w); yaw += MathHelper.PiOver2; }
            var ax = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw)); var az = new Vector3(-ax.Z, 0, ax.X);
            float drop = over * h / MathF.Max(d / 2, 0.01f);
            Vector3 x = ax * (w / 2 + over), z = az * (d / 2 + over), sag = Vector3.Down * drop, up = Vector3.Up * h, ridge = ax * MathF.Max(0, w / 2 - d / 2);
            Vector3 e0 = c - x - z + sag, e1 = c + x - z + sag, e2 = c + x + z + sag, e3 = c - x + z + sag, r0 = c - ridge + up, r1 = c + ridge + up;
            var nl = Vector3.Normalize(-az * h + Vector3.Up * (d / 2)); var nr = Vector3.Normalize(az * h + Vector3.Up * (d / 2));
            var nx = Vector3.Normalize(ax * h + Vector3.Up * (d / 2));
            Quad(e0, e1, r1, r0, roof, nl);
            Quad(e2, e3, r0, r1, Kit.Mul(roof, 0.97f), nr);
            Face(e1, e2, r1, Kit.Mul(roof, 0.93f), nx);
            Face(e3, e0, r0, Kit.Mul(roof, 0.93f), new Vector3(-nx.X, nx.Y, -nx.Z));
        }

        static Color Shade(Color c, float amount, int salt)
        {
            float k = 1 + ((salt * 7919 % 13) / 12f - 0.5f) * 2 * amount;
            return new Color((int)Math.Clamp(c.R * k, 0, 255), (int)Math.Clamp(c.G * k, 0, 255), (int)Math.Clamp(c.B * k, 0, 255));
        }
    }

    static readonly Vector2 WindowTag = new(0, -3), FineMasonryTag = new(0, -1.25f);
    /// The masonry tag the kit lays: finer courses on the campaign map's places than on a battlefield's wall.
    Vector2 Courses => t.Places.Count > 0 ? FineMasonryTag : MasonryTag;
    static readonly Color Glass = new(46, 40, 34), Gap = new(30, 25, 22), Earthwork = new(112, 108, 72);

    static Vector3 Dir3(float bearing) => new(MathF.Cos(bearing), 0, MathF.Sin(bearing));
    static Vector2 Dir2(float bearing) => new(MathF.Cos(bearing), MathF.Sin(bearing));
    static Vector3 At(Vector2 p, float y) => new(p.X, y, p.Y);

    /// The colour a people's walls are built in: stone, brick, mud brick or timber.
    static Color WallColour(PlaceStyle st, Random r) => Jitter(st.Defence switch
    {
        Defence.Brick => new Color(150, 86, 64),
        Defence.Palisade => st.Wood,
        _ => st.Stone,
    }, r, 0.04f);

    /// Timber that has stood out in the weather: the people's wood, greyed and lightened.
    static Color Weathered(PlaceStyle st, Random r) => Color.Lerp(Jitter(st.Wood, r, 0.05f), new Color(156, 138, 110), 0.38f);

    /// Stone and brick show their courses up close (the shader's masonry); mud brick and timber are left plain.
    static bool Coursed(PlaceStyle st) => st.Defence is Defence.Stone or Defence.Brick;

    /// The ground under a footprint: (lowest, highest) of its corners and middle.
    (float Lo, float Hi) GroundUnder(Vector2 c, float w, float d, float yaw)
    {
        var ax = Dir2(yaw) * (w / 2); var az = new Vector2(-ax.Y, ax.X) * (d / MathF.Max(w, 0.01f));
        float lo = t.Height(c), hi = lo;
        foreach (var p in new[] { c - ax - az, c + ax - az, c + ax + az, c - ax + az })
        {
            float h = t.Height(p);
            lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
        }
        return (lo, hi);
    }

    /// Water at p on the map (sea, river, lake), grown by `pad`: nothing is built there.
    bool WetAt(Vector2 p, float pad = 0.4f) =>
        t.SeaLevel is float sea && t.Height(p) < sea + 0.35f || t.Rivers.Any(r => r.Within(p, r.Width / 2 + 1 + pad)) || t.Lakes.Any(l => l.Inside(p, pad));

    // ---------- houses ----------

    /// One house of a people's kind standing on the ground at `at`: w along the yaw, d across, h to the eaves. Its floor sits at
    /// the highest corner; on a slope a footing of the people's stone fills down to the lowest. The front (door) faces -d.
    /// `lean` (a house in a row) leaves its side walls bare, where its neighbours hide them, and keeps its back plain.
    void Dwelling(Mesh m, HouseKind kind, PlaceStyle st, Vector2 at, float w, float d, float h, float yaw, Random r, bool chimney = false, float? raised = null, bool lean = false)
    {
        var (lo, hi) = GroundUnder(at, w, d, yaw);
        if (raised is float f && f > hi) lo = hi = f; // on a mound or a platform
        var b = At(at, hi);
        float sink = hi - lo + 0.12f;
        Color wall = st.Washes is { Length: > 0 } ws ? Jitter(ws[r.Next(ws.Length)], r, 0.03f) : Jitter(st.Wall, r, 0.05f);
        Color roof = Jitter(st.RoofColour, r, 0.08f), wood = Jitter(st.Wood, r, 0.06f);
        var ax = Dir3(yaw); var az = new Vector3(-ax.Z, 0, ax.X);
        m.Tag = Vector2.Zero;
        if (hi - lo > 0.3f && kind is not (HouseKind.Yurt or HouseKind.Tent or HouseKind.Stilt))
        {
            m.Tag = Courses;
            m.Walls(b - Vector3.Up * sink, w * 1.02f, d * 1.02f, sink + 0.05f, yaw, Jitter(st.Stone, r, 0.05f));
            m.Tag = Vector2.Zero;
            sink = 0.02f;
        }
        var floor = b - Vector3.Up * sink;
        switch (kind)
        {
            case HouseKind.Timber:
            {
                m.Walls(floor, w, d, h + sink, yaw, wood);
                float post = MathF.Max(0.05f, 0.085f * h);
                foreach (var (n, along, fw, fd) in Faces(ax, az, w, d))
                {
                    if (lean && n != -az && n != az) continue;
                    int cols = fw > 1.6f * h && !(lean && n == az) ? 2 : 1;
                    float pw = (fw - post * (cols + 1)) / cols;
                    for (int k = 0; k < cols; k++)
                    {
                        var fc = b + n * (fd / 2) + along * (-fw / 2 + post * (k + 1) + pw * (k + 0.5f));
                        m.Panel(fc + Vector3.Up * (h * 0.25f), along, n, pw, h * 0.38f, wall, 0.012f);
                        m.Panel(fc + Vector3.Up * (h * 0.71f), along, n, pw, h * 0.4f, wall, 0.012f);
                        if (fw > 1.2f * h || lean && n == -az) Window(m, fc + Vector3.Up * (h * 0.72f), along, n, MathF.Min(pw * 0.38f, h * 0.24f), h * 0.2f);
                        if (k == 0 && n == -az) m.Panel(fc + Vector3.Up * (h * 0.23f) + along * (pw * 0.2f), along, n, MathF.Min(pw * 0.3f, h * 0.3f), h * 0.44f, Kit.Mul(wood, 0.75f), 0.03f);
                    }
                }
                m.GableRoof(b + Vector3.Up * h, w, d, d * st.Pitch, yaw, roof, wall, d * 0.12f);
                break;
            }
            case HouseKind.Stone:
            {
                m.Tag = Courses;
                m.Walls(floor, w, d, h + sink, yaw, wall);
                m.GableRoof(b + Vector3.Up * h, w, d, d * st.Pitch, yaw, roof, wall, d * 0.08f);
                m.Tag = Vector2.Zero;
                foreach (float s in new[] { -1f, 1f })
                    Window(m, b + az * (s * d / 2) + ax * (w * 0.22f * s) + Vector3.Up * (h * 0.6f), ax, az * s, h * 0.16f, h * 0.22f);
                m.Panel(b - az * (d / 2) - ax * (w * 0.2f) + Vector3.Up * (h * 0.24f), ax, -az, h * 0.28f, h * 0.48f, Kit.Mul(wood, 0.8f), 0.03f);
                break;
            }
            case HouseKind.Stucco:
            {
                m.Walls(floor, w, d, h + sink, yaw, wall);
                foreach (var (n, along, fw, fd) in Faces(ax, az, w, d))
                {
                    if (fw < h || lean && n != -az && n != az) continue;
                    int cols = Math.Max(1, (int)(fw / (h * 0.55f)));
                    for (int k = 0; k < cols; k++)
                    {
                        var fc = b + n * (fd / 2) + along * (-fw / 2 + fw * (k + 0.5f) / cols);
                        Window(m, fc + Vector3.Up * (h * 0.72f), along, n, h * 0.13f, h * 0.2f, st.DoorColour);
                        if (k != cols / 2 || n != -az) Window(m, fc + Vector3.Up * (h * 0.3f), along, n, h * 0.13f, h * 0.2f, st.DoorColour);
                        else m.Panel(fc + Vector3.Up * (h * 0.22f), along, n, h * 0.2f, h * 0.42f, Kit.Mul(wood, 0.75f), 0.03f);
                    }
                }
                m.HipRoof(b + Vector3.Up * h, w, d, MathF.Min(w, d) * st.Pitch, yaw, roof, d * 0.1f);
                break;
            }
            case HouseKind.Cube:
            {
                m.Walls(floor, w, d, h + sink, yaw, wall);
                var top = b + Vector3.Up * h;
                m.Lid(top, w, d, yaw, Kit.Mul(wall, 0.92f));
                float lip = h * 0.13f;
                foreach (var (n, along, fw, fd) in Faces(ax, az, w, d))
                    m.Panel(top + n * (fd / 2) + Vector3.Up * (lip / 2), along, n, fw, lip, wall, 0.005f);
                m.Panel(b - az * (d / 2) + ax * (w * 0.15f) + Vector3.Up * (h * 0.24f), ax, -az, h * 0.24f, h * 0.46f, st.DoorColour, 0.03f);
                if (w > h * 1.1f) Window(m, b - az * (d / 2) - ax * (w * 0.22f) + Vector3.Up * (h * 0.62f), ax, -az, h * 0.12f, h * 0.16f);
                Window(m, b + az * (d / 2) + Vector3.Up * (h * 0.62f), ax, az, h * 0.12f, h * 0.16f);
                int roll = r.Next(6);
                if (roll == 0 && st.Landmark is Landmark.Mosque or Landmark.Bazaar or Landmark.Tomb)
                    m.Dome(top + ax * (w * 0.15f), MathF.Min(w, d) * 0.3f, MathF.Min(w, d) * 0.28f, 6, yaw, Kit.Mul(wall, 0.96f));
                else if (roll <= 1 && w > h) // an upper room on the roof
                {
                    float uw = w * 0.45f, ud = d * 0.6f, uh = h * 0.55f;
                    var uc = top + ax * (w * 0.22f) + az * (d * 0.15f);
                    m.Walls(uc, uw, ud, uh, yaw, wall);
                    m.Lid(uc + Vector3.Up * uh, uw, ud, yaw, Kit.Mul(wall, 0.92f));
                    Window(m, uc - az * (ud / 2) + Vector3.Up * (uh * 0.5f), ax, -az, uh * 0.22f, uh * 0.3f);
                }
                break;
            }
            case HouseKind.Hall:
            {
                m.Walls(floor, w, d, h + sink, yaw, wall);
                float rh = d * st.Pitch * 1.2f;
                m.GableRoof(b + Vector3.Up * h, w * 1.02f, d, rh, yaw, roof, wall, d * 0.32f);
                foreach (float s in new[] { -1f, 1f }) // crossed bargeboards at the gable peaks
                {
                    var peak = b + ax * (s * w * 0.51f) + Vector3.Up * (h + rh);
                    m.Face(peak - az * (d * 0.22f) - Vector3.Up * (rh * 0.25f), peak + az * (d * 0.05f), peak + az * (d * 0.12f) + Vector3.Up * (rh * 0.3f), Kit.Mul(wood, 0.8f), ax * s);
                    m.Face(peak + az * (d * 0.22f) - Vector3.Up * (rh * 0.25f), peak - az * (d * 0.05f), peak - az * (d * 0.12f) + Vector3.Up * (rh * 0.3f), Kit.Mul(wood, 0.8f), ax * s);
                }
                m.Panel(b - az * (d / 2) + Vector3.Up * (h * 0.4f), ax, -az, h * 0.4f, h * 0.75f, Kit.Mul(wood, 0.7f), 0.03f);
                break;
            }
            case HouseKind.Cabin:
            {
                m.Walls(floor, w, d, h + sink, yaw, wall);
                foreach (var (n, along, fw, fd) in Faces(ax, az, w, d))
                    if (!lean || n == -az || n == az)
                    foreach (float y in new[] { 0.3f, 0.68f })
                        m.Panel(b + n * (fd / 2) + Vector3.Up * (h * y), along, n, fw + 0.04f, h * 0.07f, Kit.Mul(wall, 0.72f), 0.012f);
                m.GableRoof(b + Vector3.Up * h, w, d, d * st.Pitch, yaw, roof, wall, d * 0.14f);
                Window(m, b - az * (d / 2) + ax * (w * 0.22f) + Vector3.Up * (h * 0.55f), ax, -az, h * 0.2f, h * 0.2f);
                m.Panel(b - az * (d / 2) - ax * (w * 0.18f) + Vector3.Up * (h * 0.3f), ax, -az, h * 0.3f, h * 0.6f, Kit.Mul(wood, 0.7f), 0.03f);
                break;
            }
            case HouseKind.Chalet:
            {
                var stone = Jitter(st.Wall, r, 0.04f);
                m.Walls(floor, w, d, h * 0.5f + sink, yaw, stone);
                m.Walls(b + Vector3.Up * (h * 0.5f), w * 1.04f, d * 1.04f, h * 0.5f, yaw, wood);
                m.Box(b - az * (d * 0.52f + 0.12f * d) + Vector3.Up * (h * 0.5f), w * 0.9f, d * 0.2f, h * 0.06f, yaw, Kit.Mul(wood, 0.85f)); // the balcony
                m.Panel(b - az * (d * 0.62f) + Vector3.Up * (h * 0.62f), ax, -az, w * 0.9f, h * 0.1f, Kit.Mul(wood, 0.85f), 0.0f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Window(m, b + az * (s * d * 0.52f) + ax * (w * 0.2f) + Vector3.Up * (h * 0.76f), ax, az * s, h * 0.16f, h * 0.18f);
                    Window(m, b + az * (s * d * 0.52f) - ax * (w * 0.2f) + Vector3.Up * (h * 0.76f), ax, az * s, h * 0.16f, h * 0.18f);
                }
                m.Panel(b - az * (d / 2) + Vector3.Up * (h * 0.2f), ax, -az, h * 0.22f, h * 0.38f, Kit.Mul(wood, 0.75f), 0.03f);
                m.GableRoof(b + Vector3.Up * h, w * 1.04f, d * 1.04f, d * st.Pitch, yaw, roof, wood, d * 0.3f);
                break;
            }
            case HouseKind.Tall:
            {
                // The gable faces the street (-d): the ridge runs front to back.
                m.Walls(floor, w, d, h + sink, yaw, wall);
                int rows = h > w * 1.6f ? 3 : 2;
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < 2; col++)
                    {
                        var at3 = b - az * (d / 2) + ax * (w * (col == 0 ? -0.22f : 0.22f)) + Vector3.Up * (h * (row + 0.62f) / rows);
                        if (row == 0 && col == 0) m.Panel(at3 - Vector3.Up * (h * 0.08f / rows), ax, -az, w * 0.24f, h * 0.55f / rows, Kit.Mul(wood, 0.75f), 0.03f);
                        else Window(m, at3, ax, -az, w * 0.2f, h * 0.36f / rows);
                    }
                Window(m, b + az * (d / 2) + Vector3.Up * (h * 0.66f), ax, az, w * 0.2f, h * 0.18f);
                float rh = w * st.Pitch;
                m.GableRoof(b + Vector3.Up * h, d, w, rh, yaw + MathHelper.PiOver2, roof, wall, w * 0.06f);
                if (st.Stepped) // a stepped gable on the street
                    for (int k = 0; k < 3; k++)
                        m.Panel(b - az * (d / 2) + Vector3.Up * (h + rh * (k + 0.5f) / 3), ax, -az, w * (1 - k / 3f) * 1.02f, rh / 3, wall, 0.02f);
                break;
            }
            case HouseKind.Stilt:
            {
                float legs = 0.5f * h;
                foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    var foot = At(at, lo - 0.2f) + ax * (sx * w * 0.42f) + az * (sz * d * 0.42f);
                    m.Panel(foot + Vector3.Up * ((hi - lo + 0.2f + legs) / 2), Vector3.Normalize(ax * sz - az * sx), Vector3.Normalize(ax * sx + az * sz), 0.12f * h, hi - lo + 0.2f + legs, Kit.Mul(wood, 0.8f), 0);
                }
                var deck = b + Vector3.Up * legs;
                m.Box(deck - Vector3.Up * 0.06f * h, w * 1.1f, d * 1.15f, 0.08f * h, yaw, Kit.Mul(wood, 0.9f));
                m.Walls(deck, w * 0.9f, d * 0.85f, h * 0.55f, yaw, wood);
                m.GableRoof(deck + Vector3.Up * (h * 0.55f), w * 0.9f, d * 0.85f, d * st.Pitch, yaw, Jitter(new Color(140, 122, 80), r, 0.06f), wood, d * 0.25f);
                m.Panel(deck - az * (d * 0.425f) + Vector3.Up * (h * 0.22f), ax, -az, h * 0.22f, h * 0.4f, Kit.Mul(wood, 0.6f), 0.03f);
                break;
            }
            case HouseKind.Yurt:
            {
                float rad = MathF.Min(w, d) / 2;
                float rot = (float)(r.NextDouble() * MathF.Tau);
                var felt = Jitter(st.Wall, r, 0.04f);
                m.Drum(floor, rad, rad, h * 0.62f + sink, 8, rot, felt, top: false);
                m.Drum(b + Vector3.Up * (h * 0.62f), rad * 1.01f, rad * 1.01f, h * 0.16f, 8, rot, Jitter(st.DoorColour, r, 0.06f), top: false);
                m.Spike(b + Vector3.Up * (h * 0.78f), rad * 1.04f, h * 0.42f, 8, rot, Jitter(st.RoofColour, r, 0.05f));
                var door = Dir3(yaw - MathHelper.PiOver2);
                m.Panel(b + door * (rad * 0.97f) + Vector3.Up * (h * 0.27f), Dir3(yaw), door, rad * 0.42f, h * 0.5f, Jitter(st.DoorColour, r, 0.06f), 0.03f);
                break;
            }
            case HouseKind.Tent:
            {
                var cloth = Jitter(new Color(64, 54, 48), r, 0.06f);
                float eave = h * 0.32f;
                var xs = new[] { -0.5f, -0.25f, 0f, 0.25f, 0.5f };
                var ys = new[] { 0.62f, 1f, 0.82f, 1f, 0.62f };
                for (int k = 0; k + 1 < xs.Length; k++)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 e0 = b + ax * (xs[k] * w) + az * (s * d / 2) + Vector3.Up * eave, e1 = b + ax * (xs[k + 1] * w) + az * (s * d / 2) + Vector3.Up * eave;
                        Vector3 r0 = b + ax * (xs[k] * w) + Vector3.Up * (h * ys[k]), r1 = b + ax * (xs[k + 1] * w) + Vector3.Up * (h * ys[k + 1]);
                        m.Quad(e0, e1, r1, r0, k % 2 == 0 ? cloth : Kit.Mul(cloth, 0.92f), Vector3.Normalize(az * s * h + Vector3.Up * (d / 2)));
                    }
                foreach (float s in new[] { -1f, 1f })
                {
                    m.Panel(b + az * (s * d / 2) + Vector3.Up * (eave / 2), ax, az * s, w, eave + 0.1f, Kit.Mul(cloth, s < 0 ? 0.5f : 0.85f), 0);
                    m.Face(b + ax * (s * w / 2) + az * (d / 2) + Vector3.Up * eave, b + ax * (s * w / 2) - az * (d / 2) + Vector3.Up * eave, b + ax * (s * w / 2) + Vector3.Up * (h * 0.62f), Kit.Mul(cloth, 0.8f), ax * s);
                }
                break;
            }
        }
        if (chimney && kind is HouseKind.Timber or HouseKind.Stone or HouseKind.Cabin or HouseKind.Chalet or HouseKind.Stucco or HouseKind.Tall)
        {
            var at3 = b + ax * (w * 0.3f) + az * (d * 0.1f);
            float ch = h + d * MathF.Max(st.Pitch, 0.3f) * 0.85f;
            m.Tag = Courses;
            m.Box(at3 + Vector3.Up * (h * 0.5f), d * 0.16f, d * 0.16f, ch - h * 0.5f + h * 0.15f, yaw, Jitter(st.Stone, r, 0.05f));
            m.Tag = Vector2.Zero;
            m.Chimneys.Add(at3 + Vector3.Up * (ch + h * 0.15f));
        }
        m.Tag = Vector2.Zero;
    }

    /// A house's four faces: outward normal, the way along it, its width and the house's depth through it.
    static (Vector3 N, Vector3 Along, float Width, float Through)[] Faces(Vector3 ax, Vector3 az, float w, float d) =>
        new[] { (-az, ax, w, d), (az, -ax, w, d), (ax, az, d, w), (-ax, -az, d, w) };

    /// A window: dark glass that the shader lamplights at night (WL5), with an optional painted surround.
    static void Window(Mesh m, Vector3 c, Vector3 along, Vector3 n, float w, float h, Color? frame = null)
    {
        var save = m.Tag;
        if (frame is Color f) { m.Tag = Vector2.Zero; m.Panel(c, along, n, w * 1.5f, h * 1.15f, f, 0.02f); }
        m.Tag = WindowTag;
        m.Panel(c, along, n, w, h, Glass, 0.03f);
        m.Tag = save;
    }

    // ---------- towers, walls, gates ----------

    /// A tower at p: radius r (round) or half its side (square), its top h above the highest ground under it, narrowing
    /// by the style's taper, with arrow slits and its people's cap. Returns its top (the middle of its roof platform).
    Vector3 Tower(Mesh m, PlaceStyle st, Vector2 p, float r, float h, float yaw, bool round, Cap cap, Color col, bool coursed, float u)
    {
        var (lo, hi) = GroundUnder(p, r * 2, r * 2, yaw);
        int sides = round ? 8 : 4;
        float rot = round ? yaw + MathF.PI / 8 : yaw + MathF.PI / 4, k = round ? 1 : MathF.Sqrt(2);
        float r0 = r * k * 1.05f, r1 = r * k * (1 - st.Taper);
        var foot = At(p, lo - 0.35f);
        float total = hi + h - foot.Y;
        m.Tag = coursed ? Courses : Vector2.Zero;
        m.Drum(foot, r0, r1, total, sides, rot, col);
        m.Tag = Vector2.Zero;
        var top = foot + Vector3.Up * total;
        float rr = r * (1 - st.Taper * 0.6f);
        for (int i = 0; i < 2; i++) // slits on two sides, lamplit at night
        {
            var outward = Dir3(yaw + i * MathF.PI + MathF.PI / 2);
            Window(m, top - Vector3.Up * (h * 0.42f) + outward * rr, Dir3(yaw + i * MathF.PI), outward, 0.12f * u, 0.42f * u);
        }
        CapOn(m, st, cap, top, r1 / k, sides, rot, round, col, coursed, u);
        return top;
    }

    /// What tops a tower whose flat top is at `top`, radius r (half side for a square one).
    void CapOn(Mesh m, PlaceStyle st, Cap cap, Vector3 top, float r, int sides, float rot, bool round, Color col, bool coursed, float u)
    {
        float k = round ? 1 : MathF.Sqrt(2);
        var cc = st.CapColour;
        m.Tag = Vector2.Zero;
        switch (cap)
        {
            case Cap.Cone:
                m.Spike(top - Vector3.Up * 0.05f, r * k * 1.22f, r * 2.4f * st.Tall, sides, rot, cc);
                break;
            case Cap.Pyramid:
                m.Spike(top - Vector3.Up * 0.05f, r * k * 1.2f, r * 1.7f * st.Tall, sides, rot, cc);
                break;
            case Cap.Dome:
                Merlons(m, top, r, round, rot, col, coursed, u, false, 0.7f);
                m.Dome(top, r * 0.8f, r * 0.9f, 8, rot, cc);
                m.Spike(top + Vector3.Up * (r * 0.9f), 0.08f * u, 0.35f * u, 4, rot, new Color(180, 160, 100));
                break;
            case Cap.Onion:
                m.Drum(top, r * 0.55f, r * 0.55f, r * 0.45f, 8, rot, col, top: false);
                m.Onion(top + Vector3.Up * (r * 0.45f), r * 0.72f, r * 1.4f, 8, rot, cc);
                break;
            default:
                Merlons(m, top, r, round, rot, col, coursed, u, cap == Cap.Swallow);
                break;
        }
    }

    /// A ring of merlons round a tower's top edge (four sides or eight).
    void Merlons(Mesh m, Vector3 top, float r, bool round, float rot, Color col, bool coursed, float u, bool swallow, float scale = 1)
    {
        m.Tag = coursed ? Courses : Vector2.Zero;
        float mw = 0.34f * u * scale, mt = 0.22f * u, mh = 0.38f * u * scale;
        if (round)
        {
            int n = Math.Max(6, (int)(MathF.Tau * r / (mw * 2.1f)));
            for (int i = 0; i < n; i++)
            {
                float a = rot + i * MathF.Tau / n;
                Merlon(m, top + Dir3(a) * (r - mt / 2), mw, mt, mh, a + MathHelper.PiOver2, col, swallow);
            }
        }
        else
        {
            float yaw = rot - MathF.PI / 4;
            int per = Math.Max(2, (int)(2 * r / (mw * 2.1f)) + 1);
            for (int sIdx = 0; sIdx < 4; sIdx++)
            {
                float fy = yaw + sIdx * MathHelper.PiOver2;
                var n = Dir3(fy); var along = Dir3(fy + MathHelper.PiOver2);
                for (int i = 0; i < per - 1; i++)
                    Merlon(m, top + n * (r - mt / 2) + along * (-r + mw / 2 + i * (2 * r - mw) / (per - 1)), mw, mt, mh, fy + MathHelper.PiOver2, col, swallow);
            }
        }
        m.Tag = Vector2.Zero;
    }

    /// One merlon standing on c (w along yaw), or a swallowtail's two horns.
    static void Merlon(Mesh m, Vector3 c, float w, float d, float h, float yaw, Color col, bool swallow)
    {
        if (!swallow) { m.Box(c, w, d, h, yaw, col); return; }
        var ax = Dir3(yaw);
        m.Box(c, w, d, h * 0.7f, yaw, col);
        m.Box(c - ax * (w * 0.3f) + Vector3.Up * (h * 0.7f), w * 0.36f, d, h * 0.5f, yaw, col);
        m.Box(c + ax * (w * 0.3f) + Vector3.Up * (h * 0.7f), w * 0.36f, d, h * 0.5f, yaw, col);
    }

    /// A run of curtain wall from a to b, its outside away from `inside`: built in lengths that each follow the ground, with
    /// merlons on the outer edge; a palisade (on a bank where the people raise one) for those who build in timber.
    void Curtain(Mesh m, PlaceStyle st, Vector2 a, Vector2 b, Vector2 inside, float wallH, float thick, Color col, float u, Random r)
    {
        var along = b - a; float len = along.Length();
        if (len < 0.2f) return;
        along /= len;
        var outN = new Vector2(along.Y, -along.X);
        if (Vector2.Dot(outN, (a + b) / 2 - inside) < 0) outN = -outN;
        float yaw = MathF.Atan2(along.Y, along.X);
        int segs = Math.Max(1, (int)MathF.Ceiling(len / (3.2f * u)));
        bool coursed = Coursed(st);
        for (int i = 0; i < segs; i++)
        {
            var p0 = a + along * (len * i / segs); var p1 = a + along * (len * (i + 1) / segs);
            float h0 = t.Height(p0), h1 = t.Height(p1), lo = MathF.Min(h0, h1) - 0.5f, ground = (h0 + h1) / 2;
            var mid = (p0 + p1) / 2;
            float seg = len / segs;
            if (st.Defence == Defence.Palisade)
            {
                Palisade(m, st, mid, seg, yaw, outN, lo, ground, wallH, u, r);
                continue;
            }
            float top = ground + wallH;
            m.Tag = coursed ? Courses : Vector2.Zero;
            m.Box(At(mid, lo), seg + 0.06f, thick, top - lo, yaw, col);
            float mw = 0.34f * u;
            int n = Math.Max(1, (int)(seg / (mw * 2.2f)));
            for (int k = 0; k < n; k++)
            {
                var at = p0 + along * (seg * (k + 0.5f) / n) + outN * (thick / 2 - 0.11f * u);
                Merlon(m, At(at, top), mw, 0.22f * u, 0.38f * u, yaw, col, st.Cap == Cap.Swallow);
            }
            m.Tag = Vector2.Zero;
        }
    }

    /// A length of palisade centred on `mid`: sharpened stakes along the top, on an earth bank for the peoples who raise one.
    void Palisade(Mesh m, PlaceStyle st, Vector2 mid, float len, float yaw, Vector2 outN, float lo, float ground, float wallH, float u, Random r)
    {
        var ax = Dir3(yaw); var on = new Vector3(outN.X, 0, outN.Y);
        float baseY = ground;
        if (st.Bank)
        {
            float bh = 0.55f * u, bw = 1.5f * u, tw = 0.55f * u;
            var c = At(mid, lo + 0.3f);
            Vector3 x = ax * (len / 2 + 0.05f);
            foreach (float s in new[] { -1f, 1f })
            {
                var n = Vector3.Normalize(on * s * bh + Vector3.Up * ((bw - tw) / 2));
                m.Quad(c + on * (s * bw / 2) - x - Vector3.Up * 0.3f, c + on * (s * bw / 2) + x - Vector3.Up * 0.3f, c + on * (s * tw / 2) + x + Vector3.Up * (ground - lo - 0.3f + bh), c + on * (s * tw / 2) - x + Vector3.Up * (ground - lo - 0.3f + bh), Jitter(Earthwork, r, 0.05f), n);
            }
            m.Lid(At(mid, ground + bh), len + 0.1f, tw, yaw, Kit.Mul(Earthwork, 0.95f));
            baseY = ground + bh;
        }
        float ph = wallH * (st.Bank ? 0.5f : 0.7f);
        var wood = Weathered(st, r);
        m.Box(At(mid, MathF.Min(lo, baseY - 0.3f)), len + 0.04f, 0.2f * u, baseY + ph - MathF.Min(lo, baseY - 0.3f), yaw, wood);
        int stakes = Math.Max(2, (int)(len / (0.3f * u)));
        var tipCol = Kit.Mul(wood, 0.92f);
        for (int k = 0; k < stakes; k++)
        {
            var c = At(mid, baseY + ph) + ax * (-len / 2 + len * (k + 0.5f) / stakes);
            float hw = len / stakes / 2;
            m.Face(c - ax * hw, c + ax * hw, c + Vector3.Up * (0.3f * u), tipCol, on);
            if (k % 2 == 1) m.Panel(c - Vector3.Up * (ph / 2) - ax * hw + on * (0.1f * u), ax, on, 0.04f * u, ph, Kit.Mul(wood, 0.7f), 0.01f); // the seams between stakes
        }
    }

    /// A timber tower (palisade peoples): a tall frame with a fighting platform and a pyramid roof. Returns its roof's foot.
    Vector3 TimberTower(Mesh m, PlaceStyle st, Vector2 p, float half, float h, float yaw, float u, Random r, float? floor = null)
    {
        var (lo, hi) = GroundUnder(p, half * 2, half * 2, yaw);
        if (floor is float f && f > hi) lo = hi = f;
        var wood = Weathered(st, r);
        var foot = At(p, lo - 0.3f);
        m.Tag = Vector2.Zero;
        m.Walls(foot, half * 2, half * 2, hi + h * 0.75f - foot.Y, yaw, wood);
        var deck = At(p, hi + h * 0.75f);
        m.Box(deck, half * 2.5f, half * 2.5f, h * 0.22f, yaw, Kit.Mul(wood, 1.08f));
        m.Panel(deck + Dir3(yaw + MathHelper.PiOver2) * (half * 1.26f) + Vector3.Up * (h * 0.11f), Dir3(yaw), Dir3(yaw + MathHelper.PiOver2), half * 1.6f, h * 0.08f, Gap, 0.01f);
        var roofFoot = deck + Vector3.Up * (h * 0.22f);
        m.Spike(roofFoot, half * 1.95f, half * 1.8f * st.Tall * (st.Cap == Cap.Pyramid ? 1.25f : 1), 4, yaw + MathF.PI / 4, Jitter(st.CapColour, r, 0.06f));
        return roofFoot;
    }

    /// A gatehouse at g facing out along `front`: a block over the passage, flanked by two towers (stone peoples), or a
    /// timber gate tower over the gap in a palisade. Returns the tops of the flanking towers (for flags).
    Vector3[] Gatehouse(Mesh m, PlaceStyle st, Vector2 g, float front, float wallH, float u, Color col, Random r, float towerR)
    {
        var outD = Dir2(front); var along2 = new Vector2(-outD.Y, outD.X);
        float yaw = MathF.Atan2(along2.Y, along2.X);
        var o3 = new Vector3(outD.X, 0, outD.Y); var a3 = new Vector3(along2.X, 0, along2.Y);
        if (st.Defence == Defence.Palisade)
        {
            var wood = Weathered(st, r);
            float ground = t.Height(g);
            foreach (float s in new[] { -1f, 1f })
                m.Box(At(g + along2 * (s * 0.75f * u), ground - 0.4f), 0.35f * u, 0.35f * u, wallH * 1.05f + 0.4f + (st.Bank ? 0.55f * u : 0), yaw, wood);
            var deck = At(g, ground + wallH * 1.05f + (st.Bank ? 0.55f * u : 0));
            m.Box(deck, 2.1f * u, 1.1f * u, 0.55f * u, yaw, Kit.Mul(wood, 1.06f));
            m.Spike(deck + Vector3.Up * (0.55f * u), 1.45f * u, 1.1f * u, 4, yaw + MathF.PI / 4, Jitter(st.CapColour, r, 0.06f));
            return new[] { deck + Vector3.Up * (0.55f * u) };
        }
        float gw = 2.1f * u, gd = 1.6f * u, gh = wallH * 1.3f;
        var (lo, hi) = GroundUnder(g, gw, gd, yaw);
        bool coursed = Coursed(st);
        m.Tag = coursed ? Courses : Vector2.Zero;
        m.Box(At(g, lo - 0.4f), gw, gd, hi + gh - lo + 0.4f, yaw, col);
        m.Tag = Vector2.Zero;
        foreach (float s in new[] { -1f, 1f })
        {
            var face = At(g, hi) + o3 * (s * gd / 2);
            m.Panel(face + Vector3.Up * (0.62f * u), a3, o3 * s, 0.95f * u, 1.24f * u, Gap, 0.02f);
            m.Face(face + Vector3.Up * (1.24f * u) - a3 * (0.475f * u) + o3 * (s * 0.02f), face + Vector3.Up * (1.24f * u) + a3 * (0.475f * u) + o3 * (s * 0.02f), face + Vector3.Up * (1.6f * u) + o3 * (s * 0.02f), Gap, o3 * s); // the arch's point
        }
        float mw = 0.34f * u;
        var gtop = At(g, hi + gh);
        for (int k = 0; k < 4; k++)
            Merlon(m, gtop + o3 * (gd / 2 - 0.11f * u) + a3 * (-gw / 2 + mw / 2 + k * (gw - mw) / 3), mw, 0.22f * u, 0.38f * u, yaw, col, st.Cap == Cap.Swallow);
        var tops = new Vector3[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1 : 1;
            tops[i] = Tower(m, st, g + along2 * (s * (gw / 2 + towerR * 0.55f)) + outD * (0.35f * u), towerR, gh * 1.25f, front, st.Round, st.Cap, col, coursed, u);
        }
        return tops;
    }

    /// A keep at p: a square donjon (or a round one for the round-towered peoples) with slits, merlons and its people's roof.
    /// `great` adds corner turrets. Returns the top of its roof platform.
    Vector3 Keep(Mesh m, PlaceStyle st, Vector2 p, float w, float h, float yaw, Color col, float u, Random r, bool great)
    {
        bool coursed = Coursed(st);
        if (st.Round && !great) return Tower(m, st, p, w * 0.55f, h, yaw, true, st.Cap, col, coursed, u);
        var (lo, hi) = GroundUnder(p, w, w, yaw);
        float r1 = w / 2 * (1 - st.Taper * 0.5f);
        var foot = At(p, lo - 0.4f);
        float total = hi + h - foot.Y;
        m.Tag = coursed ? Courses : Vector2.Zero;
        m.Drum(foot, w / 2 * MathF.Sqrt(2) * 1.03f, r1 * MathF.Sqrt(2), total, 4, yaw + MathF.PI / 4, col);
        m.Tag = Vector2.Zero;
        var top = foot + Vector3.Up * total;
        foreach (var (n, along, _, _) in Faces(Dir3(yaw), Dir3(yaw + MathHelper.PiOver2), w, w))
            foreach (float y in new[] { 0.5f, 0.78f })
                Window(m, top - Vector3.Up * (h * (1 - y)) + n * (r1 * (1 + (1 - y) * st.Taper)) , along, n, 0.16f * u, 0.5f * u);
        switch (st.Cap)
        {
            case Cap.Cone or Cap.Pyramid:
                Merlons(m, top, r1, false, yaw + MathF.PI / 4, col, coursed, u, false);
                m.Spike(top, r1 * 1.15f, r1 * (st.Cap == Cap.Cone ? 1.6f : 1.25f) * st.Tall, 4, yaw + MathF.PI / 4, st.CapColour);
                break;
            case Cap.Dome:
                Merlons(m, top, r1, false, yaw + MathF.PI / 4, col, coursed, u, false, 0.7f);
                m.Dome(top, r1 * 0.8f, r1 * 0.85f, 8, yaw, st.CapColour);
                break;
            case Cap.Onion:
                Merlons(m, top, r1, false, yaw + MathF.PI / 4, col, coursed, u, false, 0.7f);
                m.Onion(top, r1 * 0.75f, r1 * 1.5f, 8, yaw, st.CapColour);
                break;
            default:
                Merlons(m, top, r1, false, yaw + MathF.PI / 4, col, coursed, u, st.Cap == Cap.Swallow);
                break;
        }
        if (great)
            for (int i = 0; i < 4; i++)
            {
                var corner = p + Dir2(yaw + MathF.PI / 4 + i * MathHelper.PiOver2) * (r1 * MathF.Sqrt(2));
                var tt = At(corner, top.Y - 0.6f * u);
                m.Tag = coursed ? Courses : Vector2.Zero;
                m.Drum(tt, 0.38f * u, 0.38f * u, 1.3f * u, st.Round ? 8 : 4, yaw + MathF.PI / 4, col);
                m.Tag = Vector2.Zero;
                CapOn(m, st, st.Cap, tt + Vector3.Up * (1.3f * u), st.Round ? 0.38f * u : 0.27f * u, st.Round ? 8 : 4, yaw + MathF.PI / 4, st.Round, col, coursed, u * 0.6f);
            }
        return top;
    }

    /// A people's place of worship (a castle's chapel, SM2's landmarks build on it): a nave and the tower, spire, dome or
    /// minaret that marks it from afar. Front faces -d along yaw.
    void Chapel(Mesh m, PlaceStyle st, Vector2 at, float yaw, float u, Random r)
    {
        var ax2 = Dir2(yaw);
        var stone = Jitter(st.Defence == Defence.Palisade ? st.Wall : st.Stone, r, 0.04f);
        switch (st.Landmark)
        {
            case Landmark.Mosque or Landmark.Minaret or Landmark.Bazaar or Landmark.Tomb:
            {
                Dwelling(m, HouseKind.Cube, st with { Washes = null, Wall = stone }, at, 2.2f * u, 2.2f * u, 1.6f * u, yaw, new Random(r.Next()));
                var (_, hi) = GroundUnder(at, 2.2f * u, 2.2f * u, yaw);
                m.Dome(At(at, hi + 1.6f * u), 0.95f * u, 1.05f * u, 8, yaw, st.Landmark == Landmark.Tomb ? st.CapColour : Kit.Mul(stone, 0.97f));
                var mp = at + ax2 * (1.6f * u);
                var (lo2, _) = GroundUnder(mp, 0.6f * u, 0.6f * u, yaw);
                float mh = (st.Landmark == Landmark.Minaret ? 5.2f : 4.2f) * u;
                m.Drum(At(mp, lo2 - 0.2f), 0.34f * u * (1 + st.Taper), 0.28f * u, mh, st.Landmark == Landmark.Minaret ? 4 : 8, yaw + MathF.PI / 4, stone);
                m.Drum(At(mp, lo2 - 0.2f + mh), 0.4f * u, 0.4f * u, 0.18f * u, 8, yaw, Kit.Mul(stone, 0.9f));
                m.Spike(At(mp, lo2 - 0.2f + mh + 0.18f * u), 0.3f * u, 0.7f * u, 8, yaw, st.Landmark == Landmark.Tomb ? st.CapColour : Kit.Mul(stone, 0.95f));
                break;
            }
            case Landmark.GreatYurt:
                Dwelling(m, HouseKind.Yurt, st, at, 2.6f * u, 2.6f * u, 1.7f * u, yaw, new Random(r.Next()));
                break;
            case Landmark.OnionChurch:
            {
                Dwelling(m, HouseKind.Cabin, st, at, 2.4f * u, 1.5f * u, 1.3f * u, yaw, new Random(r.Next()));
                var tp = at + ax2 * (1.5f * u);
                var (lo2, hi2) = GroundUnder(tp, 1.1f * u, 1.1f * u, yaw);
                m.Walls(At(tp, lo2 - 0.2f), 1.1f * u, 1.1f * u, hi2 - lo2 + 2.6f * u, yaw, Jitter(st.Wall, r, 0.04f));
                m.Drum(At(tp, hi2 + 2.4f * u), 0.5f * u, 0.45f * u, 0.5f * u, 8, yaw, Jitter(st.Wall, r, 0.04f));
                m.Onion(At(tp, hi2 + 2.9f * u), 0.62f * u, 1.25f * u, 8, yaw, new Color(70, 110, 96));
                break;
            }
            case Landmark.StaveHall:
            {
                Dwelling(m, HouseKind.Hall, st with { RoofColour = new Color(70, 56, 44) }, at, 2.2f * u, 1.5f * u, 1.1f * u, yaw, new Random(r.Next()));
                var (_, hi2) = GroundUnder(at, 2.2f * u, 1.5f * u, yaw);
                var c = At(at, hi2 + 2.0f * u);
                m.Walls(c - Vector3.Up * (0.4f * u), 0.9f * u, 0.9f * u, 0.9f * u, yaw, Jitter(st.Wall, r, 0.04f));
                m.Spike(c + Vector3.Up * (0.5f * u), 0.75f * u, 1.6f * u, 4, yaw + MathF.PI / 4, new Color(70, 56, 44));
                break;
            }
            default:
            {
                // A stone church: nave and a west tower with a spire, a pyramid or a flat crenellated top.
                Dwelling(m, HouseKind.Stone, st with { Wall = stone, Pitch = 0.75f }, at, 2.6f * u, 1.4f * u, 1.4f * u, yaw, new Random(r.Next()));
                var tp = at - ax2 * (1.75f * u);
                bool campanile = st.Landmark is Landmark.Campanile or Landmark.Basilica;
                float th = (campanile ? 4.6f : 3.2f) * u;
                var top = Tower(m, st with { Taper = 0 }, tp, 0.5f * u, th, yaw, false, Cap.Merlons, stone, Coursed(st), u * 0.7f);
                Cap spire = st.Landmark switch { Landmark.Campanile or Landmark.Basilica => Cap.Pyramid, Landmark.Beacon or Landmark.ChapterHouse => Cap.Merlons, _ => Cap.Cone };
                if (spire == Cap.Cone) m.Spike(top, 0.5f * u, 2.6f * u, 8, yaw, Kit.Mul(st.RoofColour, 0.95f));
                else if (spire == Cap.Pyramid) m.Spike(top, 0.62f * u, 0.9f * u, 4, yaw + MathF.PI / 4, st.RoofColour);
                if (st.Landmark == Landmark.Basilica) m.Dome(At(at, top.Y - th + 1.4f * u + 0.1f), 0.7f * u, 0.7f * u, 8, yaw, Kit.Mul(st.RoofColour, 1.1f));
                break;
            }
        }
    }

    /// A flag on a pole in a realm's colours: the cloth sways in the wind (the shader's sway rule reads uv.x), the fly in its trim.
    static void Flag(Mesh m, Vector3 foot, float pole, float len, float yaw, Heraldry side, float u)
    {
        pole *= 1.35f; len *= 1.6f; // big enough to read the colours at the map's working zoom
        m.Tag = Vector2.Zero;
        var ax = Dir3(yaw); var az = new Vector3(-ax.Z, 0, ax.X);
        var wood = new Color(70, 56, 44);
        float pw = 0.05f * u;
        m.Quad(foot - ax * pw, foot + ax * pw, foot + ax * pw + Vector3.Up * pole, foot - ax * pw + Vector3.Up * pole, wood, az);
        m.Quad(foot - az * pw, foot + az * pw, foot + az * pw + Vector3.Up * pole, foot - az * pw + Vector3.Up * pole, wood, ax);
        float fh = len * 0.6f;
        var topY = foot + Vector3.Up * (pole - 0.05f * u);
        for (int k = 0; k < 3; k++)
        {
            float x0 = len * k / 3, x1 = len * (k + 1) / 3;
            float s0 = 0.15f + 0.35f * k, s1 = 0.15f + 0.35f * (k + 1);
            if (k == 0) s0 = 0;
            var col = k == 2 ? side.Trim : side.Cloth;
            Vector3 a = topY + ax * x0, b = topY + ax * x1, c = b - Vector3.Up * fh, d = a - Vector3.Up * fh;
            m.V.Add(new(a, az, col, new Vector2(s0, 0))); m.V.Add(new(b, az, col, new Vector2(s1, 0))); m.V.Add(new(c, az, col, new Vector2(s1, 0)));
            m.V.Add(new(a, az, col, new Vector2(s0, 0))); m.V.Add(new(c, az, col, new Vector2(s1, 0))); m.V.Add(new(d, az, col, new Vector2(s0, 0)));
        }
    }
}
