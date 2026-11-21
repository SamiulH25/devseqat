using Microsoft.Xna.Framework;

namespace Ironfield;

/// The campaign map's villages (docs/archive/SETTLEMENTS.md SM3), raised from their laid-out plans (`WorldMap.LayVillage`): the
/// people's cottages, longhalls, chalets, cubes, stilt houses, yurts or black tents, a chapel or shrine and a well. The
/// Crownlands thatch their cottages where their towns wear slate. Drawing only.
sealed partial class World
{
    static readonly Color Thatch3 = new(150, 122, 76);

    void VillagePlace(Mesh m, PlacePlan p, PlaceStyle st, PlaceState s, Random r)
    {
        if (p.Lots.Length == 0) { Village(m, At(p.Pos, t.Height(p.Pos)), 1.8f, r); return; } // a plan never laid out (no such map today)
        int i = 0, n = 0;
        foreach (var lot in p.Lots)
        {
            var lr = new Random(p.Seed + n++ * 7919); // each lot its own dice: a house looks the same whatever its neighbours show
            switch (lot.Use)
            {
                case LotUse.House when s.Burning && n % 2 == 1: // a raid: every other house burnt out (a tent just gone), the first ones still smoking
                    if (WorldMap.VillageKind(st) is not (HouseKind.Yurt or HouseKind.Tent)) Ruin(m, st with { Wall = Charred, Washes = null }, lot, lr);
                    if (n < 6) m.Fires.Add(At(lot.Pos, t.Height(lot.Pos) + 0.4f));
                    break;
                case LotUse.House when Ruined(s, n, village: true):
                    if (WorldMap.VillageKind(st) is not (HouseKind.Yurt or HouseKind.Tent)) Ruin(m, st, lot, lr); // a camp just moves on
                    break;
                case LotUse.House:
                    Cottage(m, st, lot, lr, chimney: i++ % 2 == 0);
                    break;
                case LotUse.Suburb:
                    if (s.Wealth == 2) Cottage(m, st, lot, lr, chimney: false);
                    break;
                case LotUse.Landmark:
                {
                    const float u = 0.9f;
                    float shift = st.Landmark is Landmark.Mosque or Landmark.Minaret or Landmark.Bazaar or Landmark.Tomb or Landmark.OnionChurch ? -0.42f
                        : st.Landmark is Landmark.GreatYurt or Landmark.StaveHall ? 0 : 0.475f;
                    Chapel(m, st, lot.Pos + lot.Along * (shift * u), lot.Yaw, u, r);
                    break;
                }
                case LotUse.Well: Well(m, st, lot, r); break;
            }
        }
    }

    /// A people's village house on its lot (also a rich town's suburbs): the hamlet kind, Crownlands cottages thatched.
    void Cottage(Mesh m, PlaceStyle st, Lot lot, Random r, bool chimney)
    {
        var kind = WorldMap.VillageKind(st);
        var look = st.Id.StartsWith("azure") && kind is HouseKind.Timber or HouseKind.Stone ? st with { RoofColour = Thatch3, Pitch = MathF.Max(st.Pitch, 0.85f) } : st;
        Dwelling(m, kind, look, lot.Pos, lot.W, lot.D, VillageHeight(kind, r), lot.Yaw, r, chimney: chimney);
    }

    /// How tall a village house stands to its eaves.
    static float VillageHeight(HouseKind kind, Random r) => kind switch
    {
        HouseKind.Stilt => 1.75f,
        HouseKind.Chalet => 1.5f,
        HouseKind.Cube => 1.05f + r.NextSingle() * 0.4f,
        HouseKind.Yurt or HouseKind.Tent => 1.3f,
        _ => 1.2f + r.NextSingle() * 0.25f,
    };

    /// A village well: a stone ring, two posts and a little roof over the windlass.
    void Well(Mesh m, PlaceStyle st, Lot lot, Random r)
    {
        var (lo, hi) = GroundUnder(lot.Pos, 0.7f, 0.7f, lot.Yaw);
        var foot = At(lot.Pos, lo - 0.15f);
        m.Tag = Coursed(st) ? Courses : Vector2.Zero;
        m.Drum(foot, 0.42f, 0.42f, hi - lo + 0.6f, 8, lot.Yaw, Jitter(st.Stone, r, 0.05f));
        m.Tag = Vector2.Zero;
        var top = At(lot.Pos, hi + 0.45f);
        var ax = Dir3(lot.Yaw);
        var wood = Weathered(st, r);
        foreach (float s in new[] { -1f, 1f }) m.Box(top + ax * (s * 0.38f), 0.09f, 0.09f, 0.8f, lot.Yaw, wood);
        m.GableRoof(top + Vector3.Up * 0.8f, 0.95f, 0.7f, 0.38f, lot.Yaw, Kit.Mul(wood, 0.85f), wood, 0.08f);
    }
}
