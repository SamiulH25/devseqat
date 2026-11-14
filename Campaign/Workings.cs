using Microsoft.Xna.Framework;

namespace Ironfield;

/// Workings on the map (docs/ARCANA.md AR7): rites that reach past your own company. **Blight** (craft) fails an enemy
/// province's harvest: its towns and villages lose prosperity and grow restless, and the Omen rises. **Calm the dead** (faith)
/// lays a province's grave fields to rest for a month. Realms that lean on sorcery blight their enemies' fields too, a few
/// times a year, by their own reckoning (no dice).
public sealed partial class Campaign
{
    /// What a blight takes from each settlement of a province, and how long the dead stay calm.
    public const float BlightProsperity = 12, BlightUnrest = 10, CalmDays = 30;

    /// The province you stand in.
    Province? ProvinceHere => S.Provinces.FirstOrDefault(p => p.Id == Map.ProvinceAt(Player.Pos).Id);

    /// Why you cannot blight the province you stand in, or null: only a realm your liege is at war with.
    string? CannotBlight()
    {
        if (ProvinceHere is not Province pr || pr.Faction < 0) return "NO REALM HOLDS THESE FIELDS";
        if (!FactionsAtWar(Captain.Contract, pr.Faction)) return "ONLY THE FIELDS OF A REALM AT WAR WITH YOURS";
        return null;
    }

    /// A province's harvest fails: its settlements lose prosperity and grow restless.
    internal void Blight(Province pr, string by)
    {
        foreach (var s in S.Settlements.Where(s => s.Province == pr.Id))
        {
            s.Prosperity = MathF.Max(0, s.Prosperity - BlightProsperity);
            s.Unrest = MathF.Min(100, s.Unrest + BlightUnrest);
        }
        S.Blights[pr.Id] = Day;
        Log($"{by} blight the fields of {pr.Title}: the harvest fails.");
    }

    /// The grave fields of a province are calmed until when (hours; none = 0).
    public bool Calmed(Vector2 at) => S.Calmed.GetValueOrDefault(Map.ProvinceAt(at).Id) > S.Hours;

    /// Weekly: a realm of arcana 2 or more works a blight on an enemy province next to its own, every few weeks.
    void WorkingsWeekly()
    {
        int week = Day / 7;
        for (int f = 0; f < S.Factions.Count; f++)
        {
            int arcana = Factions.Get(f).Arcana;
            if (arcana < 2 || Rng.Mix(S.Seed, f, week, 71) % (12 - 3 * arcana) != 0) continue; // arcana 2 one week in six, 3 one in three
            var mine = S.Provinces.Where(p => p.Faction == f).Select(p => p.Id).ToHashSet();
            var target = S.Provinces.Where(p => p.Faction >= 0 && FactionsAtWar(f, p.Faction) && p.Neighbors.Any(mine.Contains)
                    && Day - S.Blights.GetValueOrDefault(p.Id, -999) > 60)
                .OrderBy(p => Rng.Mix(S.Seed, f, week, p.Id)).FirstOrDefault();
            if (target != null) Blight(target, $"The cunning folk of {FactionNames[f]}");
        }
    }
}
