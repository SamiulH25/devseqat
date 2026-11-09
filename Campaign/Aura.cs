namespace Ironfield;

/// The captain's aura on the map (docs/KNIGHT_ORDERS.md 5.1-5.5, K1): XP from how you fought, the wall that stops it at
/// each tier, and the trials that break the wall. The battle side is BattleAura.cs.
public sealed partial class Campaign
{
    public const int AuraTop = 5;
    /// Aura XP that fills each tier (index = the tier you stand at): to Kindled, Aura Knight, Expert, Master, Transcendent.
    public static readonly float[] AuraNeeds = { 30, 80, 180, 400, 900 };
    public static readonly string[] AuraNames = { "Unawakened", "Kindled", "Aura Knight", "Aura Expert", "Aura Master", "Transcendent" };
    public static string AuraName(int tier) => AuraNames[Math.Clamp(tier, 0, AuraTop)];
    /// What each tier gives in battle, one line (the captain sheet, the breakthrough news).
    public static readonly string[] AuraGifts =
    {
        "No aura yet. Fight well and it will stir.",
        "Breath comes back faster.",
        "Hold the aura key to sheathe your blade in aura: it cuts through armour and guards.",
        "Aura armour takes part of every blow.",
        "Your presence shakes nearby foes and steadies your men. Aura comes back as you fight.",
        "The great form at its full. There is no one above you.",
    };

    /// XP still to fill the tier you stand at (0 at the top).
    public float AuraNeed => Captain.AuraTier >= AuraTop ? 0 : AuraNeeds[Captain.AuraTier];
    /// The aura you fight with: your tier, a tier less with a cracked core (K5).
    public int AuraInBattle => Math.Max(0, Captain.AuraTier - (Captain.Wounds.Contains("core") ? 1 : 0));
    /// The news of this battle's aura for the report ("" = none).
    string auraNews = "";

    /// XP into the pool until the wall stops it; the wall is told once.
    internal void GainAura(float xp)
    {
        if (xp <= 0 || Captain.AuraTier >= AuraTop || Captain.AuraWall || S.GameOver) return;
        Captain.AuraXp = MathF.Min(AuraNeed, Captain.AuraXp + xp);
        if (Captain.AuraXp < AuraNeed) return;
        Captain.AuraWall = true;
        Log(Captain.AuraTier == 0 ? "Something stirs in you in the press of battle, and will not wake. A trial will wake it (K)." : "Your aura has reached a wall. A trial will break it (K).");
        if (auraNews == "") auraNews = "YOUR AURA HAS REACHED A WALL";
    }

    /// A trial passed: the wall breaks and you rise a tier. False if there was no wall to break.
    internal bool BreakThrough(string how)
    {
        if (!Captain.AuraWall || Captain.AuraTier >= AuraTop) return false;
        Captain.AuraTier++;
        Captain.AuraXp = 0;
        Captain.AuraWall = false;
        string name = AuraName(Captain.AuraTier);
        Deed(Captain.AuraTier == 1 ? $"{how} Your aura wakes: you are Kindled." : $"{how} Your aura breaks through: you are an {name}.", "aura");
        auraNews = $"BREAKTHROUGH: {name.ToUpperInvariant()}. {AuraGifts[Captain.AuraTier].ToUpperInvariant()}";
        Captain.Renown += 5 * Captain.AuraTier;
        return true;
    }

    /// The walls a trial can break (K1): a won duel to Kindled and Aura Knight; holding against two to one up to Aura Expert.
    /// Higher walls need the harder trials (K5).
    public const int DuelBreaksBelow = 2, OddsBreakBelow = 3;

    /// After a battle you fought: XP from your kills, parries and ripostes, and the trial of holding against the odds.
    internal void AuraAfterBattle(BattleResult r, bool won, float odds)
    {
        if (r.Duration <= 0) { GainAura(won ? 1.5f : 0.5f); return; } // auto-resolved: you did your part
        if (won && odds >= 2 && r.PlayerKills >= 3 && Captain.AuraTier < OddsBreakBelow)
            BreakThrough($"You held against odds of {odds:0.0} to one with your own blade.");
        GainAura(r.PlayerKills * 0.5f + r.Parries * 0.6f + r.Ripostes * 0.8f + (won ? 2 : 0.5f)); // after the trial: it counts towards the next tier
    }

    /// After a duel: more XP than a battle, and a won duel breaks the lowest walls.
    internal void AuraAfterDuel(BattleResult r, bool won, string foe)
    {
        if (won && Captain.AuraTier < DuelBreaksBelow) BreakThrough($"You beat {foe} in single combat.");
        GainAura((won ? 6 : 2) + r.Parries * 0.6f + r.Ripostes * 0.8f);
    }

    /// The aura news for this report, cleared once read.
    string TakeAuraNews() { var s = auraNews; auraNews = ""; return s; }
}
