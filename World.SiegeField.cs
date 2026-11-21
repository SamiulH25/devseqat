using Microsoft.Xna.Framework;

namespace Ironfield;

/// The field's buildings as their people build them (docs/pre-demo/SETTLEMENTS.md SM6): an assault's wall, towers, gate, a castle's
/// keep or a town's landmark and the houses behind, and a village field's houses, all from the map's kit at battle scale.
/// `TerrainParams.Style` null keeps the old plain look (quick battles, old replays). Drawing only: the sim's wall is the
/// same for every people.
sealed partial class World
{
    /// The map kit's unit on a battlefield: the map's merlon of 0.34 u is the field's 0.75 m.
    const float FieldU = 2.2f;
    /// The map's houses at battle size (a town house of 2.4 by 1.8 is 4.5 by 3.4 m on the field).
    const float HouseU = 1.9f;

    PlaceStyle? FieldStyle => t.Params.Style is string id ? Architecture.Of(id) : null;

    /// The timber peoples' wood as it stands on the field: paler than on the map, where it would read as black in the
    /// battle's light.
    static PlaceStyle FieldWood(PlaceStyle st) => st.Defence == Defence.Palisade ? st with { Wood = Color.Lerp(st.Wood, new Color(178, 158, 126), 0.35f) } : st;

    /// A length of the assault's wall: stone, brick or mud brick on a darker footing with its merlons (swallowtails where
    /// they raise them), or for the timber peoples a face of logs with sharpened tops before an earth rampart.
    void FieldWall(Mesh m, PlaceStyle st, Vector3 b, float len, Random r)
    {
        const float h = SiegeWall.Height, d = SiegeWall.Top1 - SiegeWall.Outer;
        var c = new Vector3(b.X, b.Y - h - 2, SiegeWall.Outer + d / 2);
        if (st.Defence == Defence.Palisade)
        {
            var wood = Weathered(FieldWood(st), r);
            m.Tag = Vector2.Zero;
            m.Box(c + Vector3.UnitZ * 0.4f, len + 0.05f, d - 0.8f, h + 2.04f, 0, Jitter(Earthwork, r, 0.05f));
            float top = b.Y + 1.3f;
            m.Box(new Vector3(b.X, c.Y, SiegeWall.Outer + 0.25f), len + 0.04f, 0.5f, top - c.Y, 0, wood);
            int stakes = Math.Max(2, (int)(len / 0.5f));
            var tip = Kit.Mul(wood, 0.92f); var seam = Kit.Mul(wood, 0.66f);
            for (int k = 0; k < stakes; k++)
            {
                float x0 = b.X - len / 2 + len * k / stakes, x1 = b.X - len / 2 + len * (k + 1) / stakes, peak = 0.45f + 0.2f * r.NextSingle();
                foreach (float z in new[] { SiegeWall.Outer, SiegeWall.Outer + 0.5f })
                    m.Face(new Vector3(x0, top, z), new Vector3(x1, top, z), new Vector3((x0 + x1) / 2, top + peak, SiegeWall.Outer + 0.25f), tip, z < SiegeWall.Outer + 0.1f ? -Vector3.UnitZ : Vector3.UnitZ);
                if (k > 0) m.Panel(new Vector3(x0, (c.Y + top) / 2, SiegeWall.Outer), Vector3.UnitX, -Vector3.UnitZ, 0.07f, top - c.Y, seam, 0.01f);
            }
            for (float y = c.Y + 2.6f; y < top - 1; y += 2.2f) // the rails that bind the logs
                m.Box(new Vector3(b.X, y, SiegeWall.Outer - 0.06f), len + 0.04f, 0.14f, 0.22f, 0, Kit.Mul(wood, 0.8f));
            return;
        }
        var col = WallColour(st, r);
        m.Tag = Coursed(st) ? MasonryTag : Vector2.Zero;
        m.Box(c, len + 0.05f, d, h + 2.04f, 0, col);
        m.Box(c - Vector3.UnitZ * 0.25f, len + 0.06f, d + 0.5f, 3.1f, 0, st.Defence == Defence.Brick ? Jitter(st.Stone, r, 0.04f) : Kit.Mul(col, 0.84f));
        for (float x = -len / 2 + 0.5f; x < len / 2; x += 1.6f)
            Merlon(m, new Vector3(b.X + x, b.Y, SiegeWall.Outer + 0.3f), 0.8f, 0.6f, 0.75f, 0, Jitter(col, r, 0.03f), st.Cap == Cap.Swallow);
        m.Tag = Vector2.Zero;
    }

    /// A breach in a people's wall: its stones (or logs) heaped at the foot where the face fell.
    void FieldBreach(Mesh m, PlaceStyle st, Vector2 at, Random r)
    {
        m.Tag = Vector2.Zero;
        if (st.Defence == Defence.Palisade)
        {
            var wood = Weathered(FieldWood(st), r);
            for (int i = 0; i < 7; i++) // fallen logs, sprawled outward
            {
                var q = new Vector2(at.X - 3 + i * 1 + r.NextSingle() * 0.4f, SiegeWall.Outer - 1.4f - r.NextSingle() * 1.6f);
                var a = new Vector3(q.X, t.Height(q) + 0.2f, q.Y);
                var e = a + new Vector3((r.NextSingle() - 0.5f) * 1.6f, 0.1f, -2.4f - r.NextSingle() * 1.5f);
                m.Frustum(a, e, 0.22f, 0.2f, 5, Jitter(wood, r, 0.06f), r, capBottom: true);
            }
            return;
        }
        var stone = st.Defence == Defence.Brick ? new Color(146, 90, 70) : Kit.Mul(WallColour(st, r), 0.92f);
        for (int i = 0; i < 4; i++)
        {
            var q = new Vector2(at.X - 1.8f + i * 1.2f, SiegeWall.Outer - 0.6f + (i % 2) * 0.9f);
            float s = 1.1f + 0.3f * (i % 3);
            m.Lump(new Vector3(q.X, t.Height(q) + s * 0.1f, q.Y), new Vector3(s * 1.1f, s * 0.6f, s), Jitter(stone, r, 0.05f), r, 0.3f);
        }
        for (int i = 0; i < 10; i++)
        {
            var q = new Vector2(at.X + (r.NextSingle() - 0.5f) * 7, SiegeWall.Outer - 2.5f - r.NextSingle() * 2);
            m.Box(At(q, t.Height(q) - 0.05f), 0.5f + r.NextSingle() * 0.4f, 0.4f + r.NextSingle() * 0.3f, 0.35f + r.NextSingle() * 0.25f, r.NextSingle() * 3, Jitter(stone, r, 0.08f));
        }
    }

    /// A tower on the wall: the people's tower and cap (a timber tower for the palisade peoples), a castle's a storey
    /// higher than a town's; the gate's two fly the defenders' colours.
    void FieldTower(Mesh m, PlaceStyle st, Vector3 b, Random r, bool gate)
    {
        var p = new Vector2(b.X, SiegeWall.Outer + 1.5f);
        float rise = t.Params.Castle ? 1.3f : 1;
        Vector3 top;
        if (st.Defence == Defence.Palisade) top = TimberTower(m, FieldWood(st), p, 2.2f, 6 * rise * st.Tall, 0, FieldU, r) + Vector3.Up * (2.2f * 1.8f * st.Tall - 0.3f);
        else
        {
            const float R = 2.5f;
            top = Tower(m, st, p, R, 3.6f * rise * st.Tall, 0, st.Round, st.Cap, WallColour(st, r), Coursed(st), FieldU);
            top += Vector3.Up * CapRise(st, R * (1 - st.Taper));
        }
        if (gate && t.Params.Banner >= 0 && t.Params.Banner < Style.Sides.Length) Flag(m, top, 2.2f, 1.7f, 0, Style.Sides[t.Params.Banner], FieldU);
    }

    /// How far a tower's cap rises over its flat top (a flag stands on its peak).
    static float CapRise(PlaceStyle st, float r) => st.Cap switch
    {
        Cap.Cone => r * 2.4f * st.Tall - 0.3f,
        Cap.Pyramid => r * 1.7f * st.Tall - 0.3f,
        Cap.Dome => r * 0.9f,
        Cap.Onion => r * 1.7f,
        _ => 0,
    };

    /// A house on the field: a town's house (the steppe's towns keep a timber core among their yurts) or a village's,
    /// standing alone (no row hides its sides).
    void FieldHouse(Mesh m, PlaceStyle st, Foliage f, Random r, bool village)
    {
        var kind = village ? WorldMap.VillageKind(st) : st.House;
        if (!village && kind == HouseKind.Yurt && r.Next(3) == 0) kind = HouseKind.Cabin;
        var look = st.Defence == Defence.Palisade ? FieldWood(st) : st;
        look = kind == HouseKind.Cabin && st.House == HouseKind.Yurt ? look with { Wall = new Color(128, 100, 70), RoofColour = new Color(96, 84, 70), Washes = null } : look;
        var (w, d) = kind switch
        {
            HouseKind.Hall => (2.8f, 1.6f),
            HouseKind.Stucco => (2.5f, 2.0f),
            HouseKind.Cube => (2.1f, 1.9f),
            HouseKind.Chalet => (2.4f, 2.0f),
            HouseKind.Stilt => (1.9f, 1.6f),
            HouseKind.Yurt => (1.8f, 1.8f),
            HouseKind.Tent => (2.6f, 1.7f),
            HouseKind.Tall => (1.8f, 2.3f),
            _ => (2.4f, 1.8f),
        };
        float k = HouseU * f.Scale, yaw = r.Next(4) * MathHelper.PiOver2;
        float h = village ? VillageHeight(kind, r) : kind is HouseKind.Yurt ? 1.1f : TownHeight(kind, st, r);
        Dwelling(m, kind, look, f.Pos, w * k, d * k, h * k, yaw, r, chimney: kind is not (HouseKind.Yurt or HouseKind.Tent) && r.Next(3) == 0);
    }

    /// A castle's keep behind its curtain (a timber tower and a long hall for the palisade peoples), flying the holder's colours.
    void FieldKeep(Mesh m, PlaceStyle st, Foliage f, Random r)
    {
        Vector3 top;
        if (st.Defence == Defence.Palisade)
        {
            st = FieldWood(st);
            top = TimberTower(m, st, f.Pos + new Vector2(-3.2f, 0), 2.6f, 9 * st.Tall, 0, FieldU, r) + Vector3.Up * (2.6f * 1.8f * st.Tall - 0.3f);
            Dwelling(m, HouseKind.Hall, st, f.Pos + new Vector2(3.4f, 1), 3.5f * HouseU * 1.3f, 1.8f * HouseU * 1.3f, 0.95f * HouseU * 1.3f, MathHelper.PiOver2, r, chimney: true);
        }
        else
        {
            const float W = 9;
            top = Keep(m, st, f.Pos, W, 10.5f * st.Tall, 0, WallColour(st, r), FieldU, r, great: false);
            float rr = st.Round ? W * 0.55f : W / 2 * (1 - st.Taper * 0.5f);
            top += Vector3.Up * (st.Cap switch { Cap.Cone => rr * (st.Round ? 2.4f : 1.6f) * st.Tall, Cap.Pyramid => rr * 1.25f * st.Tall, Cap.Dome => rr * 0.85f, Cap.Onion => rr * 1.5f, _ => 0 } - 0.3f);
        }
        if (t.Params.Banner >= 0 && t.Params.Banner < Style.Sides.Length) Flag(m, top, 2.6f, 2.0f, 0, Style.Sides[t.Params.Banner], FieldU);
    }

    /// A town's great building behind its wall: its people's church, temple, hall or tomb, its front to the wall.
    void FieldLandmark(Mesh m, PlaceStyle st, Foliage f, Random r) => Chapel(m, st, f.Pos, 0, 3.0f, r);
}
