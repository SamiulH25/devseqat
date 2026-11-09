namespace Ironfield;

/// A wound the captain carries for good (docs/PROGRESSION.md P4).
public sealed record WoundDef(string Id, string Name, string Text);

/// Campaign: lasting wounds. When the captain falls there is a chance of one; a healing spring may mend it.
partial class Campaign
{
    /// The chance a fall leaves a lasting wound, before Medicine and a surgeon.
    public const float LastingWoundChance = 0.15f;

    /// Scars never mend; the others cost something in battle (`Rules.CaptainType`, `Battle.ParryTime`).
    public static readonly WoundDef[] LastingWounds =
    {
        new("scar", "Scarred", "A FACE MEN REMEMBER: +5 RENOWN WHEN EARNED"),
        new("knee", "Bad Knee", "SPEED ON FOOT -10%"),
        new("hand", "Broken Hand", "YOUR BLOWS -10%"),
        new("eye", "Lost Eye", "PARRY WINDOW 0.05 S SHORTER"),
    };

    /// A trial failed (K5): the aura held a tier down until it mends (Campaign/Aura.cs); not drawn from a fall.
    public static readonly WoundDef CoreWound = new("core", "Cracked Core", "YOUR AURA FIGHTS A TIER LOWER UNTIL IT MENDS");

    public static WoundDef? WoundOf(string id) => id == "core" ? CoreWound : LastingWounds.FirstOrDefault(w => w.Id == id);

    /// Medicine lowers the odds (to half at 100), and so does a surgeon riding with you.
    public float LastingWoundOdds => LastingWoundChance * (1 - Skill("Medicine") / 200f) * (HasCompanion("Surgeon") ? 0.5f : 1);

    /// After the captain falls: perhaps a lasting wound he does not carry yet. Returns its id, or "".
    internal string MaybeLastingWound()
    {
        if (!rng.Chance(LastingWoundOdds)) return "";
        var free = LastingWounds.Where(w => !Captain.Wounds.Contains(w.Id)).ToList();
        if (free.Count == 0) return "";
        var w = free[rng.Next(free.Count)];
        Captain.Wounds.Add(w.Id);
        if (w.Id == "scar") Captain.Renown += 5;
        Log($"The fall leaves its mark: {w.Name.ToLowerInvariant()}.");
        return w.Id;
    }

    /// Mend one lasting wound (the healing spring's `mend` effect); scars stay.
    internal string? MendWound()
    {
        var w = Captain.Wounds.Where(id => id != "scar").Select(WoundOf).OfType<WoundDef>().FirstOrDefault();
        if (w == null) return null;
        Captain.Wounds.Remove(w.Id);
        return $"The water does what surgeons could not: your {w.Name.ToLowerInvariant()} is mended.";
    }
}
