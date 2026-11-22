using Microsoft.Xna.Framework;

namespace Ironfield;

/// Interactive first-campaign tutorial ("The First March"): five short, playable steps led by Sergeant Alden that teach
/// marching, recruiting/provisions, fighting a hand-placed brigand band, promoting troops, and taking a contract.
/// Only runs on a player's first campaign (`!Settings.Current.TutorialDone`), saved per-campaign in `CampaignState.TutorialStep`,
/// and can be skipped at any moment (setting `Settings.Current.TutorialDone = true` so subsequent campaigns never show it).
public sealed partial class Campaign
{
    public const int TutorialSteps = 5;
    public bool TutorialActive => S.TutorialStep >= 0 && S.TutorialStep < TutorialSteps;

    /// Start the interactive tutorial on a new first-time campaign.
    public void BeginTutorial()
    {
        S.TutorialStep = 0;
        S.TutorialBanditId = -1;
        S.TutorialStartMen = Player.Men;
        S.TutorialStartFood = Player.Food;
        S.TutorialWins = 0;
        S.TutorialUpgrades = 0;
        LogYours("Sergeant Alden: \"Welcome to the road, Captain. Click the town ahead to march in and get our bearings.\"");
    }

    /// Skip the tutorial immediately and remember not to show it on future campaigns.
    public void SkipTutorial(bool persist = true)
    {
        S.TutorialStep = -1;
        S.TutorialBanditId = -1;
        if (persist) { Settings.Current.TutorialDone = true; Settings.Current.Save(); }
    }

    /// Finish the final tutorial step (called automatically on taking a contract, or when the player clicks FINISH).
    public void CompleteTutorial(bool persist = true)
    {
        if (!TutorialActive) return;
        S.TutorialStep = -1;
        S.TutorialBanditId = -1;
        S.Gold += 100;
        Captain.Renown += 15;
        LogYours("Sergeant Alden: \"You've got the measure of it, Captain! +100 crowns and +15 renown. The road is yours.\"");
        AudioEngine.Current?.Play(Cue.Horn);
        if (persist) { Settings.Current.TutorialDone = true; Settings.Current.Save(); }
    }

    /// Current tutorial prompt, hint, reward summary, and optional map beacon position.
    public (int Step, string Title, string Body, string Hint, string Reward, Vector2? Beacon, string BeaconLabel) TutorialPrompt()
    {
        var cap = NearestTown(Player.Pos);
        var raiders = S.TutorialBanditId >= 0 ? PartyById(S.TutorialBanditId) : null;
        return S.TutorialStep switch
        {
            0 => (0, "THE MARCH",
                "CLICK THE TOWN AHEAD TO MARCH IN. SPACE PAUSES TIME; 1-3 SETS SPEED.",
                "LEFT-CLICK ON THE MAP OR TOWN",
                "+60 CROWNS",
                cap?.Pos, "FIRST MARCH"),
            1 => (1, "FILL THE RANKS",
                "HIRE MERCENARIES IN TOWN, RECRUIT AT A VILLAGE, OR BUY PROVISIONS AT THE MARKET.",
                "VISIT TOWN TAVERN / MARKET OR A VILLAGE",
                "+10 RENOWN, +3 FOOD",
                cap?.Pos, "SUPPLIES"),
            2 => (2, "FIRST BLOOD",
                "THE RED-CAP RAIDERS PROWL NEARBY. CLICK THEM ON THE MAP AND RIDE THEM DOWN!",
                "CLICK THE RAIDERS TO ENGAGE",
                "+150 CROWNS, +15 RENOWN",
                raiders is { Down: false } ? raiders.Pos : cap?.Pos, "RAIDERS"),
            3 => (3, "DRILL & PROMOTE",
                "YOUR MEN EARNED EXPERIENCE! OPEN PARTY (P) AND PROMOTE A COMPANY TO ITS NEXT TIER.",
                "PRESS P -> CLICK A COMPANY -> PROMOTE",
                "+80 CROWNS, +10 RENOWN",
                null, ""),
            4 => (4, "TAKE A CONTRACT",
                "SPEAK TO A TOWN STEWARD FOR A CONTRACT, OR CLICK FINISH TO ROAM FREE AS A CAPTAIN.",
                "TOWN STEWARD TAB (OR CLICK FINISH)",
                "+100 CROWNS, +15 RENOWN",
                cap?.Pos, "CONTRACT"),
            _ => (-1, "", "", "", "", null, ""),
        };
    }

    Settlement? NearestTown(Vector2 p) =>
        S.Settlements.Where(s => s.Kind == SettlementKind.Town && !Refuses(s))
            .OrderBy(s => Vector2.DistanceSquared(s.Pos, p)).FirstOrDefault();

    /// Advance the interactive tutorial whenever the player completes the current step's objective.
    public void UpdateTutorial(bool persist = true)
    {
        if (!TutorialActive) return;
        switch (S.TutorialStep)
        {
            case 0: // Marched into a town / moved on the map
                if (Arrived != null || Player.Moving && S.Hours > 0.5f || SettlementAt(Player.Pos, 4f) != null && S.Hours > 0.2f)
                {
                    S.Gold += 60;
                    S.TutorialStartMen = Player.Men;
                    S.TutorialStartFood = Player.Food;
                    S.TutorialStep = 1;
                    LogYours("Sergeant Alden: \"Good march! +60 crowns. Now hire a few blades or lay in provisions before we take the road.\"");
                }
                break;

            case 1: // Hired/recruited men or bought food
                if (Player.Men > S.TutorialStartMen || Player.Food > S.TutorialStartFood + 0.5f)
                {
                    Captain.Renown += 10;
                    Player.Food += 3;
                    S.TutorialStep = 2;
                    SpawnTutorialRaiders();
                    LogYours("Sergeant Alden: \"Scouts report Red-Cap Raiders just outside town! Click them on the map to give battle.\"");
                }
                break;

            case 2: // Won first battle (or raiders eliminated)
                var raiders = S.TutorialBanditId >= 0 ? PartyById(S.TutorialBanditId) : null;
                if (S.TutorialWins > 0 || (S.TutorialBanditId >= 0 && (raiders == null || raiders.Down)))
                {
                    S.Gold += 150;
                    Captain.Renown += 15;
                    S.TutorialBanditId = -1;
                    // Ensure the player's first upgradable stack has enough XP so they can try promotion right away.
                    var ready = Player.Companies.SelectMany(c => c.Stacks).FirstOrDefault(st => st.Type.UpgradesTo.Length > 0 && st.Type.UpgradeXp > 0);
                    if (ready != null && ready.Ready == 0) ready.AddXp(ready.Type.UpgradeXp * Math.Min(ready.Men, 5));
                    S.TutorialStep = 3;
                    LogYours("Sergeant Alden: \"Well fought, Captain! +150 crowns. Press P to open your Party and promote your seasoned men.\"");
                }
                break;

            case 3: // Promoted at least one man
                if (S.TutorialUpgrades > 0)
                {
                    S.Gold += 80;
                    Captain.Renown += 10;
                    S.TutorialStep = 4;
                    LogYours("Sergeant Alden: \"Fine steel now! +80 crowns. Visit a town steward for a contract — or strike out on your own.\"");
                }
                break;

            case 4: // Took a contract -> graduate!
                if (S.Contracts.Count > 0)
                    CompleteTutorial(persist);
                break;
        }
    }

    /// Spawn a small, winnable tutorial brigand band ("Red-Cap Raiders") near the player on dry ground.
    void SpawnTutorialRaiders()
    {
        Vector2 at = Player.Pos + new Vector2(10, 4);
        for (int k = 0; k < 12; k++)
        {
            float a = k * MathF.Tau / 12f;
            var cand = Player.Pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 10f;
            if (!Map.Designed || Map.Landness(cand) >= 0.6f) { at = cand; break; }
        }
        var p = new Party
        {
            Id = S.NextId++,
            Kind = PartyKind.Bandit,
            Faction = -1,
            Name = "Red-Cap Raiders",
            Pos = at,
            Goal = at,
            Tier = 1,
            Gold = 80,
        };
        p.Companies.Add(NewCompany("levies", "infantry", Math.Max(10, Scaled(6)), 0));
        p.Companies.Add(NewCompany("levies", "archers", Math.Max(6, Scaled(3)), 0));
        S.Parties.Add(p);
        S.TutorialBanditId = p.Id;
    }
}
