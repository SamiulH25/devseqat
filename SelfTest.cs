using Microsoft.Xna.Framework;

namespace Ironfield;

/// `dotnet run -- --selftest`: formation math, mechanic scenarios, and full headless battles that must reach a result.
public static partial class SelfTest
{
    public static void Run()
    {
        var s = new Squad("t", 0) { Anchor = Vector2.Zero, Front = Vector2.UnitY, Columns = 4 };
        Check(Vector2.Distance(s.Slot(0, 8) + s.Slot(3, 8), Vector2.Zero) < 1e-4f, "front rank centred on anchor");
        Check(s.Slot(4, 8).Y < 0, "second rank behind the front");
        s.SetLine(new(-5, 10), new(5, 10), new(0, 20));
        Check(s.Front.Y > 0.99f && s.Anchor == new Vector2(0, 10), "drawn line faces the target side");
        s.SetLine(new(-5, 10), new(5, 10), new(0, 0));
        Check(s.Front.Y < -0.99f, "drawn line flips to face a target behind it");

        Charge();
        CavalryBalance();
        Flanks();
        Parry();
        HorseDown();
        Ground();
        HoldFire();
        TroopTree();
        UpgradeNeverWorse();
        HigherTierWins();
        TierGap();
        StylesByTier();
        SweepHitsBeside();
        KnockThrowsBack();
        ThrustThrough();
        CrushBeatsShields();
        PlungingShot();
        StylesOnlyAdd();
        EffectsValidate();
        PassivesByTier();
        PassivesBite();
        SignaturesBite();
        AuraReachesCompanies();
        PassivesOnlyAdd();
        AbilitiesValidate();
        MagesByTier();
        SpellWarns();
        MageWill();
        BlessingBuffs();
        MistCoversArrows();
        WildfireNeedsDry();
        HorseHuntsMages();
        MagesHelp();
        StandingEffectsCarry();
        RitesWork();
        SuperMages();
        CompanionGrows();
        HoldTheLine();
        EnemyChampions();
        CompanionLeaves();
        Sprites();
        TroopVariety();
        BigBattle();
        SpearsStopHorse();
        CampaignMap();
        CampaignPersistence();
        SaveMidRun();
        ReportMatchesRecord();
        CampaignRun();
        Progression();
        Economy();
        Bridge();
        RiverRoadGivesBridge();
        RainMakesMud();
        DefenderChoosesGround();
        AutoResolveGroundEdge();
        Calibration();
        SiegeBattle();
        Formations();
        CaptainCombat();
        CaptainKit();
        SkillBouts();
        AuraBlade();
        AuraArmour();
        AuraBound();
        AuraGrows();
        FormsWork();
        FormsLearned();
        OrdersOnMap();
        OrderOath();
        CreedBroken();
        OrderRankGate();
        CoreAndTrials();
        PresenceShakes();
        ChampionsUseAura();
        OrderPolitics();
        DuelFight();
        BattleGoals();
        BannerAndLastStand();
        RangedRoles();
        FatigueAndSupply();
        Generals();
        Rivers();
        GoesToFord();
        FilesThroughGap();
        FlowCacheBound();
        ChargeFindsCaptain();
        HoldsTheCrest();
        HorseAvoidsBadGround();
        AmbushWaits();
        TakesTheFord();
        GroundForThePlayer();
        Stakes();
        Pathfinder();
        GroundLayer();
        GroundMoves();
        GroundFights();
        Layouts();
        HedgesAndBridges();
        EveryLayoutResolves();
        SightAndCover();
        CampaignWar();
        RefuseOldSave();
        Stacks();
        RollEarnsNames();
        FallenRemembered();
        RisesFromRanks();
        BannerHonours();
        FameDraws();
        RetireAndSucceed();
        BuryNamed();
        Cultures();
        Characters();
        CampaignDivisions();
        SkillTree();
        Armoury();
        Duels();
        RidingAndBowcraft();
        PerkTiers();
        SkillRises();
        LastingWounds();
        CaptainTraits();
        HouseholdGuard();
        RankRewards();
        StartingRoad();
        BalanceMatchesData();
        HorsesCostGold();
        Career();
        PilotCareer();
        FreeCrown();
        FiefLife();
        WarAndPeace();
        EventsAndQuests();
        StackBattle();
        NamedMenFight();
        AutoResolveNamedMen();
        PullBack();
        ReplayExact();
        ScaleBattle();
        DivisionGoals();
        HoldsRanks();
        RearRanksWait();
        StepsUp();
        FightsItsTarget();
        LeviesLoseRanks();
        TierWins();
        PartsByCompany();
        LeviesBreakRanks();
        RefusesFlank();
        VeteransWrap();
        ElitePincer();
        Reforms();
        HorseRegroup();
        BowsFallBack();
        MusterGrade();
        MightWeighsDrill();
        Holdouts();
        GoblinBattle();
        Wolves();
        Trolls();
        TrollsAbroad();
        PlayerCreatures();
        WolfRiders();
        Undead();
        UndeadAbroad();
        Couriers();
        WorldNames();
        Sites();
        MapIndexMatches();
        RealmsFromData();
        ClimatesAndRidges();
        SevenRealms();
        RealmTempers();
        Roads();
        Quests();
        TownLife();
        EventSpread();
        Polish();

        for (int seed = 1; seed <= 3; seed++)
        {
            var b = new Battle(seed);
            for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
            Check(b.Winner >= 0, $"battle {seed} resolves within 10 minutes");
            Console.WriteLine($"battle {seed}: team {b.Winner} won after {b.Time:0}s, {b.Units.Count(u => u.Dead)} dead, "
                + $"{b.Units.Count(u => u.Fled)} fled, {b.DeadHorses.Count} horses lost");
        }
        // Campaign-style armies at every level (both sides upgraded unevenly) still fight to a result.
        for (int level = 1; level <= 4; level++)
        {
            var rng = new Random(level);
            var b = new Battle(level, Armies.Random(rng, level, 0), Armies.Random(rng, level, 1));
            for (int step = 0; b.Winner < 0 && step < 60 * 600; step++) b.Update(1 / 60f);
            Check(b.Winner >= 0, $"level {level} battle resolves");
            Console.WriteLine($"level {level}: {string.Join(", ", b.Armies[0]!.Companies.Select(c => $"{c.Count} {Troops.Get(c.Troop).Name}"))}"
                + $" vs {string.Join(", ", b.Armies[1]!.Companies.Select(c => $"{c.Count} {Troops.Get(c.Troop).Name}"))}: team {b.Winner} won after {b.Time:0}s");
        }
        Animation(); // last: it builds battles, and the sim draws from shared counters
        Console.WriteLine("selftest ok");
    }

    public static void RunOne(string name)
    {
        var m = typeof(SelfTest).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
            ?? throw new ArgumentException("no test named " + name);
        m.Invoke(null, null);
        Console.WriteLine(name + " ok");
    }

    static Unit Lancer(Battle b)
    {
        var cav = b.AddSquad(0, UnitType.Lancer, 1, new(0, -25), 1, Vector2.UnitY);
        cav.Order = Order.Charge;
        var l = cav.Units[0];
        l.Speed = l.Horse!.Gallop;
        return l;
    }

    static void Run(Battle b, float seconds) { for (int i = 0; i < seconds * 60; i++) b.Update(1 / 60f); }

    static ArmySpec Scaled(ArmySpec a, float k, int idBase) =>
        a with { Companies = a.Companies.Select((c, i) => c with { Count = (int)(c.Count * k), Id = idBase + i }).ToList() };

    static Character MakeWanderer(Campaign c, Settlement t)
    {
        var w = new Character { Id = c.S.NextId++, Kind = "wanderer", Name = "Test Wanderer", Culture = t.Culture, Role = "Steward", AtSettlement = t.Id };
        c.S.Characters.Add(w);
        return w;
    }

    /// Let whole days pass on the map, brushing aside encounters and arrivals (Tick stops for them).
    static void Days(Campaign c, int days)
    {
        double until = c.S.Hours + days * 24;
        while (c.S.Hours < until - 1e-3)
        {
            c.Tick((float)Math.Min(1, until - c.S.Hours));
            if (c.Encounter != null) c.Encounter.Cooldown = 3; // let it pass by
            c.Encounter = null;
            c.Arrived = null;
        }
    }

    static ArmySpec Ids(ArmySpec a, int idBase) => a with { Companies = a.Companies.Select((c, i) => c with { Id = idBase + i }).ToList() };

    static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("selftest failed: " + what);
    }
}
