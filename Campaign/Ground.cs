using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: the ground a battle is fought on (docs/TERRAIN.md T7). The map cell gives the field's shape, the weather of the
/// last days how wet it is, the season its fields; the side that was attacked, or the better scout, chooses where to stand.
public sealed partial class Campaign
{
    /// Each day it rains or not in each province (by season and land; a fixed roll, so the main random stream is untouched).
    /// Rain soaks the ground; dry days drain it.
    void RainDaily()
    {
        float season = SeasonIndex switch { 0 => 0.3f, 1 => 0.15f, 2 => 0.35f, _ => 0.25f };
        foreach (var pr in S.Provinces)
        {
            float land = pr.Kind switch { "Marsh" => 1.6f, "Forest" => 1.2f, "Steppe" => 0.5f, "Badlands" => 0.4f, _ => 1 };
            bool rained = Roll(S.Seed, Day, pr.Id) < season * land;
            pr.Wet = Math.Clamp(pr.Wet * 0.75f + (rained ? 0.3f : 0), 0, 1);
        }
    }

    /// A steady number in [0, 1) from three ints (the same every run, unlike HashCode).
    static float Roll(int a, int b, int c)
    {
        uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
        h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    /// How wet the ground is at p (its province's rain memory).
    public float WetAt(Vector2 p)
    {
        int id = Map.ProvinceAt(p).Id;
        return id >= 0 && id < S.Provinces.Count && S.Provinces[id].Id == id ? S.Provinces[id].Wet : S.Provinces.FirstOrDefault(x => x.Id == id)?.Wet ?? 0;
    }

    /// Who chooses the ground against this party: the side that was set upon, plus a point for scouts (horse) and for
    /// the Tactician perk; you win a tie. 0 = you, 1 = them.
    public int GroundChooser(Party enemy, IEnumerable<Party>? side0 = null)
    {
        bool theyCame = enemy.ChaseId == Player.Id;
        bool myScouts = (side0 ?? new[] { Player }).SelectMany(p => p.Companies).Any(c => c.Men > 0 && c.Type.Mount != null);
        bool theirScouts = enemy.Companies.Any(c => c.Men > 0 && c.Type.Mount != null);
        int mine = (theyCame ? 2 : 0) + (myScouts ? 1 : 0) + (Has("Tactician") ? 1 : 0), theirs = (theyCame ? 0 : 2) + (theirScouts ? 1 : 0);
        return mine >= theirs ? 0 : 1;
    }

    /// The field a fight with this party would be on: the map's ground, wet from the rain, in this season, chosen by `favour`.
    public TerrainParams GroundFor(Party enemy, int favour) =>
        Map.BattleGround(Player.Pos, WetAt(Player.Pos), SeasonIndex) with { Favour = favour };

    /// The seed a battle with this party would have now (PrepareBattle uses it).
    int BattleSeed(Party enemy) => HashCode.Combine(S.Seed, (int)(S.Hours * 4), enemy.Id);

    /// One line for the encounter panel on the ground and who holds it: "YOU HOLD THE HIGH GROUND."
    public string GroundNote(Party enemy)
    {
        int favour = GroundChooser(enemy);
        var g = GroundFor(enemy, favour);
        var layout = Terrain.LayoutFor(BattleSeed(enemy), g);
        string who = favour == 0 ? "YOU HOLD" : "THEY HOLD";
        string line = layout switch
        {
            FieldLayout.Ridge => $"{who} THE HIGH GROUND.",
            FieldLayout.TwoHills => $"{who} THE HILLS.",
            FieldLayout.Village => $"{who} THE VILLAGE.",
            FieldLayout.RiverLine => favour == 0 ? "A RIVER BETWEEN YOU. YOU CHOSE YOUR BANK." : "A RIVER BETWEEN YOU. THEY HOLD THE CROSSING.",
            FieldLayout.Pass => "A NARROW PASS BETWEEN HIGH SIDES.",
            FieldLayout.ForestClearing => "A CLEARING IN THE WOODS.",
            FieldLayout.WoodedFlank => "A WOOD ON ONE FLANK.",
            FieldLayout.Hedgerows => "HEDGED FIELDS.",
            FieldLayout.Fen => "SOFT FEN: BAD GROUND FOR HORSE.",
            FieldLayout.Broken => "BROKEN GROUND.",
            _ => favour == 0 ? "OPEN GROUND, YOUR CHOICE OF IT." : "OPEN GROUND, THEIR CHOICE OF IT.",
        };
        if (g.Wet > 0.5f) line += " THE GROUND IS SODDEN.";
        return line;
    }

    /// The ground's weight in auto-resolve (T7): the side that chose its ground fights a little better, and horse loses
    /// some of its worth on wet, wooded or fen ground. Above 1 favours you.
    public static float GroundEdge(TerrainParams g, IEnumerable<Party> side0, IEnumerable<Party> side1, int seed)
    {
        float edge = g.Favour switch { 0 => 1.08f, 1 => 1 / 1.08f, _ => 1 };
        var layout = Terrain.LayoutFor(seed, g);
        float bad = MathF.Max(g.Wet, layout is FieldLayout.Fen ? 0.9f : layout is FieldLayout.ForestClearing or FieldLayout.WoodedFlank ? 0.5f : g.Name == "Forest" ? 0.5f : 0);
        float Horse(IEnumerable<Party> side)
        {
            int men = side.Sum(p => p.Men), horse = side.SelectMany(p => p.Companies).Where(c => c.Type.Mount != null).Sum(c => c.Men);
            return men == 0 ? 0 : horse / (float)men;
        }
        return edge * (1 - Horse(side0) * bad * 0.3f) / (1 - Horse(side1) * bad * 0.3f);
    }
}
