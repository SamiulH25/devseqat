using Microsoft.Xna.Framework;

namespace Ironfield;

/// The field's buildings as their people build them (docs/archive/SETTLEMENTS.md SM6): an assault's wall, towers, gate, a castle's
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
