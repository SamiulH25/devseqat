namespace Ironfield;

/// Everything about a battle that is not the two armies: who you are, what you have learned, and the ground.
public sealed class BattleOptions
{
    /// Most men on the field at once (both sides together); the rest wait as reinforcements.
    public int Cap = 200;
    public float AuraRadius = 12, RallyBonus = 25, RallyCooldown = 12;
    /// Iron Will perk: companies near you will not rout while a third of them stand.
    public bool IronWill;
    /// You are wounded: you command from the rear (command view only).
    public bool PlayerWounded;
    /// An AI general commands your side too (headless tests and auto-resolve calibration).
    public bool AutoPlayer;
    /// Your companies get to see the enemy during deployment (Outriders or the Tactician perk).
    public bool Scouted = true;
    public UnitType? PlayerType;
    public Mount? PlayerMount;
    public TerrainParams? Ground;
    /// Heraldry palette per team (Style.Sides index): your colours and theirs (3 = bandits).
    public int[] Palette = { 0, 1 };
}

public enum LeaderFate { Unhurt, Wounded, Captured, Slain }

/// One company's battle: Standing at the end, Fled (ran, alive), Fallen (killed or wounded: the campaign decides which).
public sealed record CompanyResult(int Team, int Id, int Start, int Standing, int Fled, int Fallen, int Kills, int[] KillsByTier, bool Deployed);

/// What a battle hands back to the campaign. Winner: 0 you, 1 the enemy (also when you withdraw).
public sealed record BattleResult(int Winner, float Duration, IReadOnlyList<CompanyResult> Companies, LeaderFate Captain, LeaderFate EnemyLeader, int HorsesTaken);
