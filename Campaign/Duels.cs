namespace Ironfield;

/// Single combat before a battle (docs/PROGRESSION.md P2): challenge a lord to meet you between the armies. Win and his men
/// start the battle shaken; lose and you fight it wounded, commanding from the rear.
public sealed partial class Campaign
{
    /// The lord you last met in single combat (party id, -1 = none) and whether you won; read by the battle that follows.
    int duelWith = -1;
    bool duelWon;
    public const float DuelMorale = 15, DuelRenown = 10;

    /// Why this enemy will not meet you, or null. Only lords fight duels; a cautious one never does, and one whose army
    /// far outnumbers yours sees no need, unless he is reckless or honourable.
    public string? CannotChallenge(Party e)
    {
        if (e.Kind != PartyKind.Lord || CharacterOf(e) is not Character lord) return "ONLY A LORD MEETS YOU IN SINGLE COMBAT";
        string name = lord.Name.ToUpperInvariant();
        if (S.Hours < Captain.WoundedUntil) return "YOU ARE TOO HURT TO FIGHT";
        if (duelWith == e.Id) return $"YOU HAVE MET {name} ALREADY";
        if (lord.Traits.Contains("Cautious")) return $"{name} WILL NOT RISK HIMSELF";
        bool bold = lord.Traits.Contains("Reckless") || lord.Traits.Contains("Honourable");
        if (!bold && Rules.Strength(e) > Rules.Strength(Player) * 1.5f) return $"{name} HAS NO NEED TO FIGHT YOU HIMSELF";
        return null;
    }

    /// A lord's fighting form: the captain's, a little better for every tier his army has reached.
    public static UnitType LordChampion(Party e)
    {
        var t = Troops.Get("captain").Clone();
        int up = Math.Clamp(e.Tier - 2, 0, 5);
        t.MaxHp += 12 * up;
        t.Damage += 2 * up;
        t.Armor += up;
        if (up >= 2) t.Look = t.Look with { Head = "greathelm", Body = "plate", Legs = "plate", Feet = "sabatons" };
        return t;
    }

    /// The fight between the lines: you and him on flat ground, nobody else.
    public BattleSetup PrepareDuel(Party e)
    {
        var lord = CharacterOf(e)!;
        var setup = new BattleSetup { EnemyName = lord.Name, Duel = e.Id };
        setup.Seed = HashCode.Combine(S.Seed, (int)(S.Hours * 4), e.Id, 91);
        setup.Player = new ArmySpec("You", 0, new List<Company>());
        setup.Enemy = new ArmySpec(lord.Name, 0, new List<Company>());
        int mine = Style.PaletteOf(Math.Max(0, Captain.Contract)), his = Style.PaletteOf(SideOf(e));
        setup.Options = new BattleOptions
        {
            Duel = true, Couriers = false, Cap = 10, Scouted = true,
            EnemyLeader = LordChampion(e), EnemyDamage = Settings.Current.EnemyDamage,
            Ground = TerrainParams.Lists, Weather = Weather.For((float)(S.Hours % 24), SeasonIndex, HashCode.Combine(S.Seed, (int)S.Hours, e.Id)),
            Palette = new[] { mine, his == mine ? 3 : his },
        }.WithCaptain(Captain);
        return setup;
    }

    /// After the duel: renown and Arms; the battle itself is still to come (the encounter stays open).
    BattleReport FinishDuel(BattleResult r, Party e)
    {
        bool won = r.Winner == 0;
        duelWith = e.Id;
        duelWon = won;
        string name = CharacterOf(e)?.Name ?? e.Name;
        var skillsBefore = new Dictionary<string, float>(Captain.Skills);
        Practise("Arms", (won ? 2.5f : 1) + (r.Parries + r.Ripostes) * 0.15f);
        if (won && ChampionAbove(e)) BreakThrough($"You beat {name}, a champion above you, in single combat."); // K5
        AuraAfterDuel(r, won, CharacterOf(e)?.Name ?? e.Name);
        FormsAfterBattle(r);
        float renown = won ? DuelRenown : 0;
        Captain.Renown += renown;
        var fate = won ? LeaderFate.Unhurt : LeaderFate.Wounded;
        if (!won) Captain.WoundedUntil = MathF.Max(Captain.WoundedUntil, (float)S.Hours + 24 * 3);
        if (won) Deed($"You beat {name} in single combat before his army.", "duels");
        else Log($"{name} beats you in single combat. You will lead the battle from the rear.");
        Report = new BattleReport(won, $"{name} in single combat", new List<CompanyReport>(), 0, 0, 0, renown, 0, fate) { Rises = Skills.Rises(Captain, skillsBefore), Aura = TakeAuraNews() };
        return Report;
    }

    /// What the duel does to the battle after it: his men shaken, or nothing (your wound is in WoundedUntil already).
    float DuelMoraleFor(Party e) => duelWith == e.Id && duelWon ? -DuelMorale : 0;
    float DuelEdge(Party e) => duelWith == e.Id ? (duelWon ? 1.1f : 0.95f) : 1;
}
