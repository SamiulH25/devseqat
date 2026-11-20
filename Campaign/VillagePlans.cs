using Microsoft.Xna.Framework;

namespace Ironfield;

public sealed partial class WorldMap
{
    /// The house a people's villages build: its hamlet's (tents, yurts, stilt houses), else its town house, a tall town house
    /// becoming a cottage (or a stucco farmhouse where they build in brick).
    public static HouseKind VillageKind(PlaceStyle st) => st.Hamlet ?? st.House switch
    {
        HouseKind.Tall => st.Defence == Defence.Brick ? HouseKind.Stucco : HouseKind.Timber,
        var k => k,
    };

    /// A village's layout (SM3), by its people: a street village strung along its road, or houses round a green facing it;
    /// the nomads' camp a loose ring round its middle. A chapel or shrine where the people raise one, and a well.
    PlacePlan LayVillage(PlacePlan p)
    {
        var st = p.Look;
        var r = new Random(p.Seed ^ 0x7a11);
        var roads = LocalRoads(p.Pos, p.Radius + 4);
        var kind = VillageKind(st);
        float R = MathF.Max(4.2f, p.Radius + 0.5f);
        var centre = p.Pos;
        for (int k = 0; k < 16 && Wet(centre, 0.8f); k++) centre = p.Pos + PlacePlan.Dir(k * 2.4f) * (0.6f + k * 0.25f);
        var lots = new List<Lot>();
        var near = Neighbours(p);
        float reach = R + 0.8f; // grows when the middle is full (a village at a crossroads)
        bool Fits(Lot lot, float pad = 0.35f)
        {
            foreach (var c in lot.Corners.Append(lot.Pos))
                if (Wet(c, 0.25f) || OnRoad(roads, c, 0.85f) || Vector2.Distance(c, centre) > reach || !Owns(p, near, c)) return false;
            return !lots.Any(o => Lot.Overlap(o, lot, pad));
        }
        var (w0, d0) = kind switch
        {
            HouseKind.Hall => (3.5f, 1.8f),
            HouseKind.Stucco => (2.5f, 2.0f),
            HouseKind.Cube => (2.1f, 1.9f),
            HouseKind.Chalet => (2.4f, 2.0f),
            HouseKind.Stilt => (1.9f, 1.6f),
            HouseKind.Yurt => (1.8f, 1.8f),
            HouseKind.Tent => (2.6f, 1.7f),
            _ => (2.4f, 1.8f),
        };
        Lot House(Vector2 at, Vector2 front) =>
            new(at, Lot.Facing(front), w0 * (0.85f + 0.3f * r.NextSingle()), d0 * (0.9f + 0.2f * r.NextSingle()), LotUse.House);
        bool nomads = kind is HouseKind.Yurt or HouseKind.Tent;
        int want = 5 + r.Next(4);

        // The chapel (or the people's shrine) first, by the middle; then the well.
        if (!nomads && r.Next(2) == 0)
            for (int k = 0; k < 24; k++)
            {
                var dir = PlacePlan.Dir(p.Front + MathF.PI + k * 0.9f);
                var lot = new Lot(centre + dir * (1.8f + k * 0.12f), Lot.Facing(-dir), 3.4f, 2.0f, LotUse.Landmark);
                if (Fits(lot)) { lots.Add(lot); break; }
            }
        for (int k = 0; k < 40; k++) // the well by the middle, off the road
        {
            var at = centre + PlacePlan.Dir(p.Front + MathF.PI / 2 + k * 1.3f) * (0.4f + k * 0.1f);
            var lot = new Lot(at, 0, 0.85f, 0.85f, LotUse.Well);
            if (Fits(lot, 0.5f)) { lots.Add(lot); break; }
        }

        var road = roads.OrderBy(x => LineDist(x.Pts, centre)).FirstOrDefault();
        bool street = !nomads && road.Pts != null && LineDist(road.Pts, centre) < 2.5f && r.Next(10) < 6;
        if (street)
        {
            // A street village: houses either side of the road, facing it, with yards between.
            var line = Resample(road.Pts, 0.25f).Where(q => Vector2.Distance(q, centre) < R).ToList();
            foreach (int side in new[] { 1, -1 })
                for (int i = 0; i + 1 < line.Count && lots.Count(l => l.Use == LotUse.House) < want;)
                {
                    float w = w0 * (0.85f + 0.3f * r.NextSingle());
                    int j = Math.Min(line.Count - 1, i + Math.Max(1, (int)MathF.Round(w / 0.25f)));
                    var t = Vector2.Normalize(line[j] - line[i] + new Vector2(1e-5f, 0));
                    var nrm = new Vector2(-t.Y, t.X) * side;
                    float dd = d0 * (0.9f + 0.2f * r.NextSingle());
                    var lot = new Lot((line[i] + line[j]) / 2 + nrm * (road.Half * 0.85f + 0.15f + dd / 2), Lot.Facing(-nrm), Vector2.Distance(line[i], line[j]) - 0.05f, dd, LotUse.House);
                    if (lot.W > 0.8f && Fits(lot)) { lots.Add(lot); i = j + (int)((0.6f + r.NextSingle() * 0.9f) / 0.25f); }
                    else i += 2;
                }
        }
        // Round a green (or the camp's ring), then any gaps filled where a house fits, facing the middle.
        for (int tries = 0; lots.Count(l => l.Use == LotUse.House) < want && tries < 240; tries++)
        {
            // The first round evenly about the green, then anywhere (a golden-angle walk), reaching further out as it goes.
            float a = tries < want ? p.Front + MathF.PI / want + tries * MathF.Tau / want + (r.NextSingle() - 0.5f) * 0.5f : p.Front + tries * 2.39996f;
            reach = R + 0.8f + MathF.Min(3, tries / 60f);
            float rad = (nomads ? 2.6f : 3.0f) + r.NextSingle() * (nomads ? 2.0f : 1.4f) + MathF.Min(3.5f, tries / 50f);
            var at = centre + PlacePlan.Dir(a) * rad;
            var lot = House(at, Vector2.Normalize(centre - at + new Vector2(1e-5f, 0)));
            if (kind == HouseKind.Tent) lot = lot with { Yaw = lot.Yaw + (r.NextSingle() - 0.5f) * 1.2f };
            if (Fits(lot)) lots.Add(lot);
        }
        // A few more where the village has room, shown when it does well (SM4).
        int built = lots.Count(l => l.Use == LotUse.House);
        for (int tries = 0; lots.Count(l => l.Use == LotUse.Suburb) < 3 && tries < 120; tries++)
        {
            float a = p.Front + 1.1f + tries * 2.39996f;
            var at = centre + PlacePlan.Dir(a) * ((nomads ? 3.4f : 3.8f) + r.NextSingle() * 1.6f + MathF.Min(2.5f, tries / 40f));
            reach = R + 1.8f + MathF.Min(2.5f, tries / 40f);
            var lot = House(at, Vector2.Normalize(centre - at + new Vector2(1e-5f, 0))) with { Use = LotUse.Suburb };
            if (Fits(lot)) lots.Add(lot);
        }
        return p with { Lots = lots.ToArray(), Square = centre };
    }
}
