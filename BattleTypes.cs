namespace Ironfield;

/// Everything about a battle that is not the two armies: who you are, what you have learned, and the ground.
public sealed class BattleOptions
{
    /// Most men on the field at once (both sides together); the rest wait as reinforcements.
    public int Cap = 400;
    public float AuraRadius = 12, RallyBonus = 25, RallyCooldown = 12;
    /// Your Arms skill (0-100; a new captain has 15): quicker blows, a wider parry window, less stamina a swing (P2).
    public float Arms = 15;
    /// Your Riding and Bowcraft skills (P3; a new captain has 5) and the perks you hold that act in battle.
    public float Riding = 5, Bowcraft = 5;
    public HashSet<string> Perks = new();
    /// The captain's lasting wounds (P4); the battle reads "eye" (a shorter parry window).
    public HashSet<string> Wounds = new();
    /// Iron Will perk: companies near you will not rout while a third of them stand.
    public bool IronWill;
    /// A king's first rally of the battle calls every broken company back, wherever it runs (P5).
    public bool KingsCharge;
    /// You are wounded: you command from the rear (command view only).
    public bool PlayerWounded;
    /// An AI general commands your side too (headless tests and auto-resolve calibration).
    public bool AutoPlayer;
    /// Your companies get to see the enemy during deployment (Outriders or the Tactician perk).
    public bool Scouted = true;
    public UnitType? PlayerType;
    /// Your second weapon (N swaps to it). Null with a PlayerType means none; with neither, the quick battle's greatsword.
    public UnitType? PlayerSide;
    public Mount? PlayerMount;
    public TerrainParams? Ground;
    /// Heraldry palette per team (Style.Sides index): your colours and theirs (3 = bandits).
    public int[] Palette = { 0, 1 };
    /// Difficulty: the enemy's blows are multiplied by this.
    public float EnemyDamage = 1;
    /// An assault on a walled town (roadmap B1): team 1 holds the wall; ladders built and the gate's strength.
    public bool Siege;
    public int Ladders = 2;
    public float GateHp = 500;
    public Weather Weather = Weather.Clear;
    /// The Horns perk: orders reach your sergeants 30% sooner.
    public bool Horns;
    /// Orders to divisions ride out as couriers, who can be cut down on the way (a new one then sets out). Off: plain delays.
    public bool Couriers = true;
    /// A tournament (S5): blunt weapons, so your captain is never taken and the champion who leads them is not slain.
    public bool Tournament;
    /// Single combat before a battle (P2): you on foot against the enemy leader, nobody else; a fall is a yield, not a death.
    public bool Duel;
    /// The enemy leader's own type (a lord's champion in a duel); null = the plain captain.
    public UnitType? EnemyLeader;
    /// Added to every enemy man's morale at the start (a duel lost by his lord: -15).
    public float EnemyMorale;
}

/// When and in what weather a battle is fought (roadmap C4). The campaign passes its hour and season.
public sealed record Weather(float Hour = 13, float Rain = 0, float Fog = 0, bool Snow = false)
{
    /// Weather for a campaign battle: the hour, and rain, snow or fog by season (deterministic from the seed).
    public static Weather For(float hour, int season, int seed)
    {
        var r = new Random(seed);
        float wet = season switch { 0 => 0.25f, 1 => 0.1f, 2 => 0.35f, _ => 0.3f };
        float rain = r.NextSingle() < wet ? 0.4f + r.NextSingle() * 0.6f : 0;
        float fog = (hour is > 4 and < 10 ? 0.35f : 0.08f) > r.NextSingle() ? 0.4f + r.NextSingle() * 0.5f : 0;
        return new Weather(hour, rain, fog, season == 3 && rain > 0);
    }

    public static readonly Weather Clear = new();
    public bool Night => Hour < 5.5f || Hour > 20.5f;
    /// Rain wets bowstrings and fog hides the mark: arrows scatter more.
    public float Spread => 1 + Rain * 0.6f + Fog * 0.25f;
    /// Mud slows horses.
    public float MudFactor => 1 - Rain * 0.2f;
    /// How far archers can pick out a target.
    public float Vision => MathF.Min(Night ? 40 : 200, Fog > 0 ? 140 - Fog * 95 : 200);
    public string Describe => (Night ? "night" : Hour < 8 ? "dawn" : Hour > 18 ? "dusk" : "day") + (Rain > 0.3f ? Snow ? ", snow" : ", rain" : "") + (Fog > 0.3f ? ", fog" : "");
}

public enum LeaderFate { Unhurt, Wounded, Captured, Slain }

/// One company's battle: Standing at the end, Fled (ran, alive), Fallen (killed or wounded: the campaign decides which).
/// Stacks breaks the same counts down by troop type, so the campaign takes casualties off the right stack (CAMPAIGN_OVERHAUL.md 2.3).
public sealed record CompanyResult(int Team, int Id, int Start, int Standing, int Fled, int Fallen, int Kills, int[] KillsByTier, bool Deployed,
    IReadOnlyList<StackResult>? Stacks = null, int HorseKills = 0, bool Routed = false, bool HeroFell = false);

/// One troop type's part of a company's battle.
public sealed record StackResult(string Troop, int Start, int Standing, int Fled, int Fallen);

/// What a battle hands back to the campaign. Winner: 0 you, 1 the enemy (also when you withdraw).
/// PlayerKills counts all your kills; of them SaddleKills from horseback and ArrowKills with your bow. LanceHits, ArrowHits,
/// Parries and Ripostes are what you did with your own hands (P3: each feeds a skill).
public sealed record BattleResult(int Winner, float Duration, IReadOnlyList<CompanyResult> Companies, LeaderFate Captain, LeaderFate EnemyLeader, int HorsesTaken,
    int PlayerKills = 0, int SaddleKills = 0, int ArrowKills = 0, int LanceHits = 0, int ArrowHits = 0, int Parries = 0, int Ripostes = 0);
