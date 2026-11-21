using Microsoft.Xna.Framework;

namespace Ironfield;

/// How a people builds a house (docs/pre-demo/SETTLEMENTS.md).
public enum HouseKind
{
    Timber, // timber frame on a plinth, plastered panels between the beams
    Stone,  // rubble or ashlar walls, small windows (bastles, the Order's houses)
    Stucco, // plain plastered walls under a low roof (the Compact's cities)
    Cube,   // a whitewashed or adobe block, flat roof with a low parapet
    Hall,   // a long low plank hall under a turf roof (the fjords)
    Cabin,  // a log cabin under a steep plank roof (the taiga)
    Chalet, // a white stone ground floor, a dark timber floor above, wide eaves (the crags)
    Tall,   // a narrow house of three floors, its gable on the street (bays, canals)
    Stilt,  // a reed-thatched hut up on posts (the fens)
    Yurt,   // a round felt tent (the steppe)
    Tent,   // a long low black goat-hair tent (the dunes)
}

/// The roof a house wears.
public enum RoofKind { Gable, Hip, Flat, Turf }

/// What a place's walls are built of.
public enum Defence { Stone, Brick, Mud, Palisade }

/// What tops a tower.
public enum Cap { Cone, Pyramid, Merlons, Swallow, Dome, Onion }

/// The great building that marks a town.
public enum Landmark { Cathedral, Beacon, Harbour, Campanile, Basilica, Spire, StaveHall, OnionChurch, Mosque, Minaret, Bazaar, GreatYurt, Tomb, Windmill, ChapterHouse }

/// What the smallest hold (a tower house, HD5) is among a people.
public enum Hold { Tower, Ringfort, Kasbah, Camp }

/// One people's way of building (docs/pre-demo/SETTLEMENTS.md): the houses, the walls and towers, the landmark. Colours stay in the
/// world's muted palette; heraldry brings the strong colour (flags in the owner's colours).
public sealed record PlaceStyle(
    string Id,
    string Build, // the tavern picture's way of building (`MapScreen.LookOf`): timber, white, log or tent

    HouseKind House, RoofKind Roof, float Pitch,
    Color Wall, Color Wood, Color RoofColour, Color Stone,
    Defence Defence, bool Round, Cap Cap, Color CapColour,
    Landmark Landmark, Hold Hold)
{
    /// Village houses, when a people's villages build otherwise than its towns (black tents in the dunes, yurts by the salt sea).
    public HouseKind? Hamlet { get; init; }
    /// Walls drawn from these, a house each (stucco and tall houses in their colours); null = `Wall` jittered.
    public Color[]? Washes { get; init; }
    /// Doors and shutters (painted on the white houses of the south), else the wood.
    public Color? Trim { get; init; }
    /// An earth bank under the palisade (the fjords and the steppe).
    public bool Bank { get; init; }
    /// Towers rise this much higher than the common run, and narrow toward the top by `Taper` (the dunes' kasbahs).
    public float Tall { get; init; } = 1;
    public float Taper { get; init; }
    /// Stepped gables on the tall houses (the fens).
    public bool Stepped { get; init; }

    public HouseKind VillageHouse => Hamlet ?? House;
    public Color DoorColour => Trim ?? Wood;
}

/// The peoples' ways of building, one for each subculture of the drawn map, and the realm cultures (older and generated maps)
/// mapped to their heartland. `Of` never fails: an unknown culture builds as the Crownlanders do.
public static class Architecture
{
    static readonly Color Slate = new(84, 90, 100), BlueSlate = new(70, 86, 118), RedTile = new(152, 70, 50), Shingle = new(90, 78, 66),
        Turf = new(98, 112, 64), Thatch = new(148, 120, 74), Reed = new(140, 122, 80), Felt = new(214, 204, 182),
        Timber = new(94, 66, 44), DarkLog = new(78, 56, 38), GreyStone = new(150, 146, 136), Brick = new(150, 86, 64),
        Adobe = new(192, 150, 104), Whitewash = new(232, 226, 210);

    public static readonly IReadOnlyList<PlaceStyle> All = new PlaceStyle[]
    {
        new("azure_heart", "timber", HouseKind.Timber, RoofKind.Gable, 0.8f, new(210, 196, 164), Timber, BlueSlate, GreyStone,
            Defence.Stone, true, Cap.Cone, BlueSlate, Landmark.Cathedral, Hold.Tower) { Tall = 1.1f },
        new("azure_march", "timber", HouseKind.Stone, RoofKind.Gable, 0.55f, new(138, 132, 120), new(84, 62, 44), new(92, 94, 98), new(126, 122, 114),
            Defence.Stone, false, Cap.Merlons, new(126, 122, 114), Landmark.Beacon, Hold.Tower) { Tall = 1.2f },
        new("azure_coast", "timber", HouseKind.Tall, RoofKind.Gable, 0.85f, new(214, 202, 176), new(88, 64, 46), BlueSlate, new(160, 156, 146),
            Defence.Stone, true, Cap.Cone, BlueSlate, Landmark.Harbour, Hold.Tower)
            { Washes = new Color[] { new(216, 204, 176), new(178, 188, 196), new(206, 186, 152), new(196, 196, 184) } },
        new("crimson_patrician", "timber", HouseKind.Stucco, RoofKind.Hip, 0.38f, new(206, 162, 104), new(110, 70, 46), RedTile, new(170, 156, 134),
            Defence.Brick, false, Cap.Swallow, Brick, Landmark.Campanile, Hold.Tower)
            { Washes = new Color[] { new(206, 162, 104), new(198, 132, 94), new(222, 202, 162), new(178, 114, 80) }, Tall = 1.25f },
        new("crimson_alpine", "timber", HouseKind.Chalet, RoofKind.Gable, 0.42f, new(224, 218, 202), new(96, 66, 44), Shingle, new(150, 146, 138),
            Defence.Stone, false, Cap.Pyramid, Shingle, Landmark.Spire, Hold.Tower) { Tall = 1.2f },
        new("northern_fjord", "log", HouseKind.Hall, RoofKind.Turf, 0.6f, new(122, 94, 64), new(80, 58, 40), Turf, new(128, 126, 118),
            Defence.Palisade, false, Cap.Pyramid, Shingle, Landmark.StaveHall, Hold.Ringfort) { Bank = true },
        new("northern_taiga", "log", HouseKind.Cabin, RoofKind.Gable, 0.95f, new(104, 76, 50), DarkLog, new(86, 74, 60), new(124, 122, 116),
            Defence.Palisade, false, Cap.Pyramid, new(86, 74, 60), Landmark.OnionChurch, Hold.Ringfort) { Tall = 1.15f },
        new("southern_oasis", "white", HouseKind.Cube, RoofKind.Flat, 0, Whitewash, new(110, 80, 52), new(214, 206, 188), new(218, 206, 178),
            Defence.Mud, false, Cap.Merlons, new(218, 206, 178), Landmark.Mosque, Hold.Kasbah) { Trim = new Color(70, 110, 120) },
        new("southern_dune", "white", HouseKind.Cube, RoofKind.Flat, 0, Adobe, new(104, 74, 48), new(196, 158, 112), new(206, 166, 118),
            Defence.Mud, false, Cap.Merlons, new(206, 166, 118), Landmark.Minaret, Hold.Kasbah) { Hamlet = HouseKind.Tent, Tall = 1.3f, Taper = 0.22f },
        new("southern_rift", "white", HouseKind.Cube, RoofKind.Flat, 0, new(236, 232, 220), new(110, 80, 52), new(220, 214, 200), new(200, 190, 168),
            Defence.Stone, true, Cap.Merlons, new(200, 190, 168), Landmark.Bazaar, Hold.Tower) { Trim = new Color(60, 96, 150) },
        new("steppe_horde", "tent", HouseKind.Yurt, RoofKind.Flat, 0, Felt, new(110, 80, 52), new(196, 184, 160), new(140, 132, 116),
            Defence.Palisade, false, Cap.Pyramid, Shingle, Landmark.GreatYurt, Hold.Camp) { Bank = true, Trim = new Color(150, 62, 48) },
        new("steppe_salt", "tent", HouseKind.Cube, RoofKind.Flat, 0, new(196, 172, 132), new(104, 76, 50), new(196, 176, 138), new(204, 176, 132),
            Defence.Mud, true, Cap.Dome, new(72, 132, 140), Landmark.Tomb, Hold.Kasbah) { Hamlet = HouseKind.Yurt, Trim = new Color(64, 110, 130) },
        new("league_lagoon", "timber", HouseKind.Tall, RoofKind.Gable, 0.38f, new(210, 170, 112), new(100, 70, 48), RedTile, new(176, 164, 146),
            Defence.Brick, false, Cap.Merlons, Brick, Landmark.Basilica, Hold.Tower)
            { Washes = new Color[] { new(204, 134, 112), new(212, 172, 112), new(224, 208, 174), new(214, 152, 122), new(174, 104, 78) } },
        new("league_fen", "timber", HouseKind.Tall, RoofKind.Gable, 1.0f, Brick, new(90, 64, 44), new(120, 70, 52), new(150, 140, 126),
            Defence.Brick, true, Cap.Cone, new(120, 70, 52), Landmark.Windmill, Hold.Tower)
            { Hamlet = HouseKind.Stilt, Stepped = true, Washes = new Color[] { new(150, 86, 64), new(136, 76, 58), new(162, 100, 74), new(124, 70, 56) } },
        new("order_chapter", "timber", HouseKind.Stone, RoofKind.Gable, 0.55f, new(176, 172, 162), new(84, 66, 50), Slate, new(178, 174, 164),
            Defence.Stone, false, Cap.Merlons, new(178, 174, 164), Landmark.ChapterHouse, Hold.Tower) { Tall = 1.3f },
        new("order_warden", "timber", HouseKind.Stone, RoofKind.Gable, 0.65f, new(150, 148, 140), new(84, 66, 50), Slate, new(146, 144, 136),
            Defence.Stone, false, Cap.Pyramid, Slate, Landmark.ChapterHouse, Hold.Tower) { Tall = 1.15f },
    };

    static readonly Dictionary<string, string> Heartland = new()
    {
        ["azure"] = "azure_heart", ["crimson"] = "crimson_patrician", ["northern"] = "northern_fjord", ["southern"] = "southern_oasis",
        ["steppe"] = "steppe_horde", ["league"] = "league_lagoon", ["order"] = "order_chapter",
    };

    static readonly Dictionary<string, PlaceStyle> byId = All.ToDictionary(s => s.Id);

    /// The style a culture builds in: its own (a subculture), its heartland's (a realm culture), else the Crownlanders'.
    public static PlaceStyle Of(string culture) =>
        byId.TryGetValue(culture, out var s) ? s : byId[Heartland.GetValueOrDefault(culture) ?? Heartland.GetValueOrDefault(Factions.PeopleOf(culture)) ?? "azure_heart"];

    /// Whether a culture has a style of its own or a heartland's (`Of` falls back to the Crownlanders for anything else).
    public static bool Styled(string culture) => byId.ContainsKey(culture) || Heartland.ContainsKey(culture);
}
