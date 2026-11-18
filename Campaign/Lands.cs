using Microsoft.Xna.Framework;

namespace Ironfield;

/// One kind of country on the campaign map (a province's `Kind`): its name in a title, the map's tint, woods, travel,
/// rain, sites, whether monsters lair there, and the battlefield it gives (docs/WORLDMAP.md WM3).
/// `Title` is a format with {0} for the province's name. `Tint` multiplies the ground's colour; `Wash` (rgb, amount)
/// then draws it toward a colour of its own (sand, frost), which a multiply cannot do. `Trees` is a province's woods
/// at the 392 map's size; `Conifer` makes them pines. `Rain` scales the daily chance of rain (and a battle's). `Wild` = monster
/// country. `Climate`: "cold" (snow outside summer), "hot" (never snow, no white winter on the map) or "".
public sealed record LandDef(string Kind, string Title, Vector3 Tint, Vector4 Wash, int Trees, bool Conifer, float Speed, float Rain,
    bool Wild, string[] Sites, TerrainParams Field, string Climate = "")
{
    public bool Hot => Climate == "hot";
    public bool Cold => Climate == "cold";
}

public static class Lands
{
    static readonly Vector4 NoWash = Vector4.Zero;

    /// The seven kinds every generated map has, then the six the drawn world adds. The first seven keep the values the
    /// game had before the table, so layouts 0-4 generate and play exactly as before.
    public static readonly LandDef[] All =
    {
        new("Meadow", "the Vale of {0}", Vector3.One, NoWash, 14, false, 1f, 1f, false,
            new[] { "shrine", "battlefield", "mill", "spring", "ruin", "watchtower" }, TerrainParams.Meadow),
        new("Farmland", "the {0} Fields", Vector3.One, NoWash, 6, false, 1f, 1f, false, // the patchwork tint is drawn by WorldMap.KindTint
            new[] { "mill", "shrine", "battlefield", "watchtower", "spring", "ruin" }, TerrainParams.Farmland),
        new("Forest", "{0} Forest", new(0.8f, 0.88f, 0.76f), NoWash, 90, false, 0.7f, 1.2f, true,
            new[] { "ruin", "hermit", "spring", "shrine", "stones", "watchtower" }, TerrainParams.Forest),
        new("Hills", "the {0} Hills", new(1.08f, 1.04f, 0.86f), NoWash, 26, true, 0.8f, 1f, true,
            new[] { "ruin", "stones", "mine", "watchtower", "hermit", "spring" }, TerrainParams.Hills),
        new("Marsh", "the {0} Fens", new(0.76f, 0.88f, 0.82f), NoWash, 30, false, 0.6f, 1.6f, true,
            new[] { "shrine", "hermit", "ruin", "spring", "stones", "mill" }, TerrainParams.Marsh),
        new("Steppe", "the {0} Steppe", new(1.14f, 1.08f, 0.78f), NoWash, 2, false, 1.1f, 0.5f, false,
            new[] { "stones", "battlefield", "watchtower", "ruin", "shrine", "spring" }, TerrainParams.Steppe),
        new("Badlands", "the {0} Wastes", new(1.26f, 1.0f, 0.7f), NoWash, 4, false, 0.85f, 0.4f, true,
            new[] { "mine", "ruin", "watchtower", "stones", "hermit", "spring" }, TerrainParams.Badlands),
        // The drawn world's kinds (layout 5).
        new("Coast", "the {0} Shore", new(1.05f, 1.04f, 0.9f), new(0.62f, 0.62f, 0.46f, 0.25f), 8, false, 1f, 1.3f, false,
            new[] { "watchtower", "shrine", "ruin", "battlefield", "hermit", "spring" }, TerrainParams.Coast),
        new("Taiga", "the {0} Taiga", new(0.76f, 0.86f, 0.84f), new(0.17f, 0.26f, 0.19f, 0.6f), 70, true, 0.7f, 1.1f, true,
            new[] { "hermit", "ruin", "stones", "spring", "shrine", "watchtower" }, TerrainParams.Taiga, "cold"),
        new("Highlands", "the {0} Highlands", new(1.0f, 0.98f, 0.88f), new(0.5f, 0.42f, 0.3f, 0.45f), 16, true, 0.75f, 1.3f, true,
            new[] { "stones", "ruin", "watchtower", "mine", "hermit", "spring" }, TerrainParams.Highlands),
        new("Tundra", "the {0} Barrens", Vector3.One, new(0.86f, 0.9f, 0.92f, 0.95f), 6, true, 0.8f, 0.6f, true,
            new[] { "stones", "ruin", "hermit", "watchtower", "battlefield", "spring" }, TerrainParams.Tundra, "cold"),
        new("Desert", "the {0} Sands", Vector3.One, new(0.95f, 0.85f, 0.62f, 0.9f), 0, false, 0.75f, 0.1f, false,
            new[] { "ruin", "stones", "watchtower", "mine", "battlefield", "hermit" }, TerrainParams.Desert, "hot"),
        new("Oasis", "the {0} Oasis", new(0.94f, 1.04f, 0.86f), new(0.84f, 0.74f, 0.54f, 0.45f), 12, false, 0.95f, 0.3f, false,
            new[] { "spring", "shrine", "mill", "ruin", "watchtower", "hermit" }, TerrainParams.Oasis, "hot"),
    };

    static readonly Dictionary<string, LandDef> byKind = All.ToDictionary(l => l.Kind);

    /// The kind's entry (an unknown kind plays as meadow).
    public static LandDef Of(string kind) => byKind.TryGetValue(kind, out var l) ? l : All[0];
    public static bool Known(string kind) => byKind.ContainsKey(kind);
}
