using Microsoft.Xna.Framework;

namespace Ironfield;

/// The headless campaign simulation (docs/CAMPAIGN.md): time, movement, encounters, AI, economy and your actions.
/// The UI (MapScreen) reads its state and raises actions; battles come back through ApplyBattle.
public sealed partial class Campaign
{
    public readonly CampaignState S;
    public readonly WorldMap Map;
    readonly Rng rng;
    static Balance B => Balance.Get();

    /// Raised for the UI: a hostile party has caught you (or you it); you arrived where you were heading; a battle report.
    public Party? Encounter;
    public Settlement? Arrived;
    public BattleReport? Report;
    /// How many fights the world has resolved without you (lord against lord, bandits against lords): campaign telemetry.
    public int AiBattles;

    public Party Player => S.Parties[0];
    public CaptainState Captain => S.Captain;
    public int Day => (int)(S.Hours / 24) + 1;
    public static readonly string[] FactionNames = { "Azure Crown", "Crimson Compact", "Northmarch" };
    /// Party and event caps for the balance numbers scaled by how much world there is (a bigger map raises more lords and
    /// brigands). The numbers in Data/campaign.json are quoted against the original 26-province map.
    float ProvinceScale => Map.ProvinceScale;
    /// Lords spread thinner than brigands: a realm with four times the ground raises about twice the armies, not four
    /// times (village recruits are the bottleneck, and a crowd of lords starves all of them).
    public int LordsWanted => (int)MathF.Round(B.LordsPerFaction * ProvinceScale);
    public int BanditsWanted => (int)MathF.Round(B.BanditsMax * ProvinceScale);
    int BanditSpawnsWanted => Math.Max(1, (int)MathF.Round(B.BanditSpawnsPerDay * ProvinceScale));

    public Campaign(CampaignState state)
    {
        S = state;
        Map = WorldMap.Generate(state.Seed, state.MapExtent);
        rng = new Rng(state);
        // The map's settlement models follow the save (a v1 save was generated before the Northmarch had its capital).
        for (int i = 0; i < Math.Min(Map.Settlements.Count, S.Settlements.Count); i++) Map.Settlements[i].Kind = S.Settlements[i].Kind;
    }

    // ---------- a new campaign ----------

    public static Campaign New(int seed)
    {
        var s = new CampaignState { Seed = seed, Gold = B.StartGold, Ironman = Settings.Current.Ironman, MapExtent = WorldMap.Extent };
        s.Captain.Renown = B.StartRenown;
        var map = WorldMap.Generate(seed, s.MapExtent);
        s.Provinces = map.Provinces;
        s.Settlements = map.Settlements;
        int factions = s.Provinces.Max(p => p.Faction) + 1;
        s.Factions = Enumerable.Range(0, factions).Select(f => new FactionState { Id = f, Name = FactionNames[f], Treasury = 2000 }).ToList();
        // The Crown and the Compact are at war; the Northmarch keeps truces with both, for 20 and 40 days.
        s.Wars.Add(new War { A = 0, B = 1 });
        if (factions > 2)
        {
            s.Wars.Add(new War { A = 0, B = 2, Active = false, TruceUntil = 20 * 24 });
            s.Wars.Add(new War { A = 1, B = 2, Active = false, TruceUntil = 40 * 24 });
        }
        var c = new Campaign(s);
        var capital = s.Settlements.Where(t => t.Kind == SettlementKind.Town && t.Faction == 0).OrderBy(t => t.Pos.X).First();
        var player = new Party { Id = s.NextId++, Kind = PartyKind.Player, Name = "Your company", Faction = 0, Pos = capital.Pos + new Vector2(2, 0), Tier = 2 };
        player.Goal = player.Pos;
        s.Parties.Add(player);
        player.Companies.Add(c.NewCompany("levies", "infantry", 24, 0));
        player.Companies.Add(c.NewCompany("levies", "spears", 16, 0));
        foreach (var v in s.Settlements)
        {
            v.Recruits = v.Kind == SettlementKind.Village ? 4 + c.rng.Next(5) : 0;
            v.Horses = v.Kind == SettlementKind.Town ? 10 : 0;
            if (v.Kind == SettlementKind.Town) c.RefreshMercenaries(v);
            c.RaiseGarrison(v);
        }
        for (int f = 0; f < factions; f++)
            for (int i = 0; i < c.LordsWanted; i++) c.SpawnLord(f);
        // Open with a map's worth of brigands, not three: the roads are dangerous from the first day.
        for (int i = 0; i < c.BanditsWanted / 3; i++) c.SpawnBandits();
        // The guided first fight: a weak band close by, coming for you.
        var first = new Party { Id = s.NextId++, Kind = PartyKind.Bandit, Name = "Ditch Wolves", Pos = player.Pos + new Vector2(0, 16), Tier = 1, ChaseId = player.Id, Moving = true, TaskUntil = 72 };
        first.Companies.Add(c.NewCompany("levies", "infantry", 9, 0));
        s.Parties.Add(first);
        c.Log($"You sign on with the {FactionNames[0]} as a free captain. Their steward pays 85% of your upkeep each week.");
        c.Log("Levies upgrade after a battle or two: visit a town's training yard.");
        return c;
    }

    public CompanyRecord NewCompany(string troop, string role, int men, float xp) =>
        WithOfficer(new() { Id = S.NextId++, Troop = troop, Role = role, Men = men, Xp = xp, Name = Rules.CompanyName(rng, role) });

    public void Log(string text)
    {
        S.Log.Add(new LogEntry { At = S.Hours, Text = text });
        if (S.Log.Count > 60) S.Log.RemoveAt(0);
    }

    public Party? PartyById(int id) => S.Parties.FirstOrDefault(p => p.Id == id);
    public Settlement SettlementById(int id) => S.Settlements[id];

    // ---------- relations ----------

    public int SideOf(Party p) => p.Kind == PartyKind.Player ? Captain.Contract : p.Kind == PartyKind.Bandit ? -2 : p.Faction;

    public bool Hostile(Party a, Party b)
    {
        if (a == b || a.Down || b.Down) return false;
        if (a.Kind == PartyKind.Bandit || b.Kind == PartyKind.Bandit) return a.Kind != b.Kind;
        return FactionsAtWar(SideOf(a), SideOf(b));
    }

    public War? WarBetween(int a, int b) => S.Wars.FirstOrDefault(w => (w.A == a && w.B == b) || (w.A == b && w.B == a));
    public bool FactionsAtWar(int a, int b) => a >= 0 && b >= 0 && a != b && WarBetween(a, b) is { Active: true };
    public bool AtWarWithAnyone(int f) => S.Wars.Any(w => w.Active && (w.A == f || w.B == f));

    /// Credit `winner` in its war with `loser` (battles won, villages threatened, towns taken).
    void Score(int winner, int loser, float amount)
    {
        if (WarBetween(winner, loser) is not { Active: true } w) return;
        if (w.A == winner) w.ScoreA += amount; else w.ScoreB += amount;
        if (winner < S.Factions.Count) S.Factions[winner].WarScore += amount;
    }

    /// Enemy towns will not trade with you.
    public bool Refuses(Settlement s) => Captain.Contract >= 0 && FactionsAtWar(Captain.Contract, s.Faction);

    public Settlement? SettlementAt(Vector2 p, float r = 6f) => S.Settlements.Where(s => Vector2.Distance(s.Pos, p) < r).OrderBy(s => Vector2.Distance(s.Pos, p)).FirstOrDefault();
    public bool InTown(Party p) => SettlementAt(p.Pos) is { Kind: SettlementKind.Town } t && (p.Kind != PartyKind.Player || !Refuses(t));

    // ---------- time ----------

    /// Advance time; stops early when something needs you (an encounter, an arrival).
    public void Tick(float hours)
    {
        while (hours > 1e-4f && Encounter == null && Arrived == null)
        {
            float h = MathF.Min(0.25f, hours);
            hours -= h;
            double before = S.Hours;
            S.Hours += h;
            if ((int)before != (int)S.Hours) Hourly();
            if ((int)(before / 24) != (int)(S.Hours / 24)) Daily();
            if ((int)(before / 168) != (int)(S.Hours / 168)) Weekly();
            Move(h);
            Encounters();
        }
    }

    public float Speed(Party p)
    {
        bool mounted = p.Companies.Count > 0 && p.Companies.All(c => c.Men == 0 || c.Type.Mount != null);
        float s = (mounted ? B.HorseSpeed : B.FootSpeed) * (Winter ? 0.75f : 1);
        if (p.Kind == PartyKind.Player) s *= 1 - MathF.Min(0.25f, p.Prisoners.Sum(g => g.Count) / 400f);
        if (p.Kind == PartyKind.Player && Captain.Perks.Contains(Perk.Horsemaster)) s *= 1.05f;
        int men = p.Men, wounded = p.Wounded;
        if (men + wounded > 0) s *= 1 - MathF.Min(0.2f, wounded / (float)(men + wounded) * 0.5f);
        return s;
    }

    void Move(float h)
    {
        foreach (var p in S.Parties)
        {
            p.Cooldown -= h;
            if (p.Down || (p.Kind == PartyKind.Player && p.Resting)) continue;
            Vector2? target = null;
            if (p.ChaseId >= 0)
            {
                var q = PartyById(p.ChaseId);
                if (q == null || q.Down) p.ChaseId = -1; else target = q.Pos;
            }
            if (target == null && p.Moving) target = p.Goal;
            if (target is not Vector2 t) continue;
            var d = t - p.Pos;
            float dist = d.Length(), step = Speed(p) * Map.SpeedFactor(p.Pos, B.RoadBonus) * h;
            if (dist < 1e-3f) { p.Moving = false; continue; }
            p.Facing = MathF.Atan2(d.Y, d.X);
            if (dist <= step)
            {
                p.Pos = t;
                if (p.ChaseId < 0) p.Moving = false;
                if (p.Kind == PartyKind.Player && p.GoalSettlement >= 0) { Arrived = SettlementById(p.GoalSettlement); p.GoalSettlement = -1; }
            }
            else p.Pos += d / dist * step;
            p.Pos = Vector2.Clamp(p.Pos, new Vector2(-Map.ExtentUsed), new Vector2(Map.ExtentUsed));
        }
    }

    void Encounters()
    {
        // 190 parties and more on the big map: a uniform grid keeps this from being an O(n^2) sweep every tick.
        // A cell is larger than either clash radius, so checking the 3x3 neighbourhood finds every pair that can meet.
        const float Cell = 16f;
        var cells = new Dictionary<(int, int), List<Party>>(S.Parties.Count * 2);
        foreach (var p in S.Parties)
        {
            if (p.Down) continue;
            var key = ((int)MathF.Floor(p.Pos.X / Cell), (int)MathF.Floor(p.Pos.Y / Cell));
            if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Party>(2);
            list.Add(p);
        }
        foreach (var (key, here) in cells)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!cells.TryGetValue((key.Item1 + dx, key.Item2 + dy), out var there)) continue;
                    foreach (var a in here)
                        foreach (var b in there)
                        {
                            Clash(a, b);
                            if (Encounter != null) return;
                        }
                }
    }

    /// Resolve a meeting between two parties (an AI fight happens at once; a fight with you opens the encounter panel).
    void Clash(Party a, Party b)
    {
        if (a == b || a.Cooldown > 0 || b.Cooldown > 0 || !Hostile(a, b)) return;
        bool playerInvolved = a.Kind == PartyKind.Player || b.Kind == PartyKind.Player;
        // AI parties are a little slower to close than the player is, so the field is not one long chain of fights.
        float radius = playerInvolved ? B.EncounterRadius : B.AiClashRadius;
        if (Vector2.DistanceSquared(a.Pos, b.Pos) > radius * radius) return;
        if (playerInvolved)
        {
            Encounter = a.Kind == PartyKind.Player ? b : a;
            Player.Moving = false;
            Player.ChaseId = -1;
            return;
        }
        var r = Rules.AutoResolve(new[] { a }, new[] { b }, rng, false);
        var winner = r.Winner == 0 ? a : b;
        var loser = r.Winner == 0 ? b : a;
        AiBattles++;
        Apply(r, new[] { a }, new[] { b }, "");
        if (winner.Kind == PartyKind.Lord || loser.Kind == PartyKind.Lord)
            Log($"{winner.Name} defeats {loser.Name} near {Map.ProvinceAt(loser.Pos).Name}.");
    }

    // ---------- applying a battle ----------

    /// Apply a battle's result to the campaign (played or auto-resolved). Returns the report for the results screen.
    public BattleReport Apply(BattleResult r, IReadOnlyList<Party> side0, IReadOnlyList<Party> side1, string enemyName)
    {
        var lookup = new Dictionary<int, (Party P, CompanyRecord C, int Team)>();
        foreach (var p in side0) foreach (var c in p.Companies) lookup[c.Id] = (p, c, 0);
        foreach (var p in side1) foreach (var c in p.Companies) lookup[c.Id] = (p, c, 1);
        float startStrength0 = side0.Sum(Rules.Strength), startStrength1 = side1.Sum(Rules.Strength);
        var before = side0.Concat(side1).ToDictionary(p => p, p => p.Men);
        bool playerIn = side0.Contains(Player) || side1.Contains(Player);
        int playerTeam = side0.Contains(Player) ? 0 : 1;
        bool won = r.Winner == playerTeam;
        float loot = 0, ransom = 0;
        var reports = new List<CompanyReport>();

        foreach (var cr in r.Companies)
        {
            if (!lookup.TryGetValue(cr.Id, out var e)) continue;
            var (p, c, team) = e;
            bool teamWon = r.Winner == team;
            int menBefore = c.Men;
            float pw = B.WoundBase + c.Type.Armor / B.WoundArmor + (p == Player && Captain.Perks.Contains(Perk.Surgeon) ? 0.1f : 0) + (c.Traits.Contains("Surgeon") ? 0.1f : 0);
            pw = Math.Clamp(pw, B.WoundMin, B.WoundMax);
            int wounded = (int)MathF.Round(cr.Fallen * pw), killed = cr.Fallen - wounded;
            int captured = teamWon ? 0 : (int)MathF.Round(wounded * B.CaptureChance);
            wounded -= captured;
            int fledBack = teamWon ? cr.Fled : (int)MathF.Round(cr.Fled * B.FledReturnOnDefeat);
            int standing = cr.Deployed ? cr.Standing : menBefore;
            c.Men = Math.Clamp(standing + fledBack, 0, menBefore);
            c.Wounded += wounded;
            float xp = 0;
            if (cr.Deployed && c.Men > 0)
            {
                float killXp = 0;
                for (int t = 0; t < cr.KillsByTier.Length; t++) killXp += cr.KillsByTier[t] * Math.Max(1, t) * B.XpPerKillTier;
                xp = B.XpParticipation * (teamWon ? B.XpVictory : 1) + killXp / c.Men;
                c.Xp += xp;
            }
            if (cr.Deployed) { c.Battles++; c.Kills += cr.Kills; }
            float lostFrac = menBefore > 0 ? (menBefore - c.Men) / (float)menBefore : 0;
            c.Cohesion = Math.Clamp(c.Cohesion + (teamWon ? 5 : -5) - lostFrac * 10, 0, 30);
            if (cr.Deployed) OfficerAfterBattle(c, teamWon, lostFrac);
            if (playerIn && team != playerTeam && won)
            {
                loot += (cr.Fallen + cr.Fled) * c.Type.Cost * B.LootRate;
                TakePrisoners(c.Troop, captured); // held, not ransomed at once (B3)
            }
            if (playerIn) reports.Add(new CompanyReport(c.Id, c.Name, c.Troop, team == playerTeam ? 0 : 1, menBefore, c.Men, killed, wounded, captured,
                cr.Fled - fledBack, xp, Rules.UpgradeReady(c), cr.Deployed));
        }
        foreach (var p in side0.Concat(side1)) p.Companies.RemoveAll(c => c.Men + c.Wounded <= 0);

        // Aftermath for each party.
        var winners = r.Winner == 0 ? side0 : side1;
        var losers = r.Winner == 0 ? side1 : side0;
        float lostStrength = (r.Winner == 0 ? startStrength1 - side1.Sum(Rules.Strength) : startStrength0 - side0.Sum(Rules.Strength));
        foreach (var p in winners) { p.Cooldown = 6; if (p.Kind is PartyKind.Lord) p.Gold += lostStrength * 2; }
        foreach (var p in losers)
        {
            p.Cooldown = 12;
            p.ChaseId = -1;
            if (p.Kind == PartyKind.Garrison) continue;
            if (p.Kind == PartyKind.Bandit) { p.Down = true; continue; }
            if (p.Kind == PartyKind.Lord && (p.Men < before[p] * 0.25f || p.Men == 0))
            {
                p.Down = true;
                p.DownUntil = (float)S.Hours + 5 * 24;
                p.Pos = SettlementById(p.HomeId).Pos;
                p.SiegeTarget = -1;
                if (playerIn && won && rng.Chance(0.5f))
                {
                    p.Captive = true;
                    p.DownUntil = float.MaxValue;
                    Player.Captives.Add(p.Id);
                    Log($"{p.Name} is your prisoner.");
                }
                else Log($"{p.Name}'s army is scattered.");
            }
        }
        int wf = winners.Select(SideOf).FirstOrDefault(f => f >= 0, -1), lf = losers.Select(SideOf).FirstOrDefault(f => f >= 0, -1);
        if (wf >= 0 && lf >= 0) Score(wf, lf, lostStrength);
        S.Parties.RemoveAll(p => p.Kind == PartyKind.Bandit && p.Down);

        var report = new BattleReport(won, enemyName, reports, 0, 0, 0, 0, 0, r.Captain);
        if (!playerIn) return report;
        float bounty = 0, renown = 0;
        int horses = 0;
        if (won)
        {
            foreach (var e in losers)
                bounty += e.Kind == PartyKind.Bandit ? B.BountyBandits + Rules.Strength(e) / 10
                    : Captain.Contract >= 0 ? B.BountyLord + (startStrength1 + startStrength0) / 20 : 0;
            renown = B.RenownPerVictory + (playerTeam == 0 ? startStrength1 : startStrength0) / B.RenownStrengthDivisor;
            horses = r.HorsesTaken;
            S.Gold += loot + ransom + bounty;
            foreach (var e in losers) S.LastBattleFoe = e.Id;
            Captain.Renown += renown;
            Player.Horses += horses;
        }
        else if (Player.Men > 0)
        {
            // Beaten but not taken: fall back.
            var away = Player.Pos - (losers == side0 ? side1 : side0)[0].Pos;
            Player.Pos += (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 8;
        }
        if (r.Captain is LeaderFate.Wounded or LeaderFate.Captured)
            Captain.WoundedUntil = (float)S.Hours + 24 * rng.Range(3, 5);
        if (r.Captain == LeaderFate.Captured && S.Ironman)
        {
            S.GameOver = true;
            Log("You are taken, and your captors will not ransom you. Your war is over.");
        }
        else if (r.Captain == LeaderFate.Captured || (!won && Player.Men == 0))
        {
            S.Gold *= 0.7f;
            var refuge = S.Settlements.Where(t => t.Kind == SettlementKind.Town && !Refuses(t)).OrderBy(t => Vector2.Distance(t.Pos, Player.Pos)).First();
            Player.Pos = refuge.Pos + new Vector2(5, 0);
            Player.Moving = false;
            if (Player.Men == 0 && Player.Wounded == 0) Player.Companies.Add(NewCompany("levies", "infantry", 10, 0));
            Log($"You are taken, and ransomed. Your men regroup at {refuge.Name}.");
        }
        Report = report with { Loot = loot, Ransom = ransom, Bounty = bounty, Renown = renown, Horses = horses };
        Log(won ? $"Victory over {enemyName}." : $"Defeat against {enemyName}.");
        return Report;
    }

    // ---------- encounters with you ----------

    /// The parties that fight with you and against you: anyone close enough joins the side they are friendly to.
    public BattleSetup PrepareBattle(Party enemy)
    {
        var setup = new BattleSetup { EnemyName = enemy.Name };
        setup.Side0.Add(Player);
        setup.Side1.Add(enemy);
        foreach (var p in S.Parties)
        {
            if (p == Player || p == enemy || p.Down || Vector2.Distance(p.Pos, Player.Pos) > B.JoinRadius) continue;
            if (Hostile(p, enemy) && !Hostile(p, Player)) setup.Side0.Add(p);
            else if (Hostile(p, Player) && !Hostile(p, enemy)) setup.Side1.Add(p);
        }
        bool unpaid = S.UnpaidWeeks > 0;
        setup.Player = Rules.Army("Your company", setup.Side0, unpaid ? -15 : 0);
        setup.Enemy = Rules.Army(enemy.Name, setup.Side1);
        setup.Seed = HashCode.Combine(S.Seed, (int)(S.Hours * 4), enemy.Id);
        bool scouts = setup.Side0.SelectMany(p => p.Companies).Any(c => c.Men > 0 && c.Type.Mount != null);
        setup.Options = new BattleOptions
        {
            AuraRadius = Captain.Perks.Contains(Perk.Bannerman) ? 16 : 12,
            RallyBonus = Captain.Perks.Contains(Perk.WarCry) ? 35 : 25,
            RallyCooldown = Captain.Perks.Contains(Perk.WarCry) ? 9 : 12,
            IronWill = Captain.Perks.Contains(Perk.IronWill),
            PlayerWounded = S.Hours < Captain.WoundedUntil,
            Scouted = scouts || Captain.Perks.Contains(Perk.Tactician),
            PlayerType = Rules.CaptainType(Captain),
            PlayerMount = Rules.CaptainMount(Captain),
            EnemyDamage = Settings.Current.EnemyDamage,
            Ground = Map.BattleGround(Player.Pos),
            Weather = Weather.For((float)(S.Hours % 24), SeasonIndex, HashCode.Combine(S.Seed, (int)S.Hours, enemy.Id)),
            Palette = new[] { Style.PaletteOf(Math.Max(0, Captain.Contract)), Style.PaletteOf(enemy.Kind == PartyKind.Bandit ? -2 : enemy.Faction) },
        };
        return setup;
    }

    public BattleReport FinishBattle(BattleResult r, BattleSetup setup)
    {
        Encounter = null;
        var report = Apply(r, setup.Side0, setup.Side1, setup.EnemyName);
        if (setup.SiegeOf >= 0) AfterAssault(S.Settlements[setup.SiegeOf], r.Winner == 0);
        return Report ?? report;
    }

    public BattleReport AutoResolveEncounter()
    {
        var setup = PrepareBattle(Encounter!);
        var r = Rules.AutoResolve(setup.Side0, setup.Side1, rng, Captain.Perks.Contains(Perk.Tactician));
        if (r.Winner == 1 && S.Hours >= Captain.WoundedUntil && rng.Chance(0.5f)) r = r with { Captain = LeaderFate.Wounded };
        return FinishBattle(r, setup);
    }

    public bool CanRetreat(out string why)
    {
        var e = Encounter!;
        bool horse = e.Companies.Any(c => c.Men > 0 && c.Type.Mount != null);
        why = horse && Speed(e) > Speed(Player) ? "THEIR HORSE WILL RIDE YOU DOWN" : "";
        return why == "";
    }

    /// Escape, leaving a rearguard: the slowest company loses 10-25% of its men.
    public void Retreat()
    {
        var e = Encounter!;
        var rear = Player.Companies.Where(c => c.Men > 0).OrderBy(c => c.Type.Mount != null ? 1 : 0).ThenByDescending(c => c.Men).FirstOrDefault();
        if (rear != null)
        {
            int lost = Math.Max(1, (int)(rear.Men * rng.Range(0.1f, 0.25f)));
            rear.Men -= lost;
            Log($"You escape. {rear.Name} lose {lost} men covering the retreat.");
        }
        var away = Player.Pos - e.Pos;
        Player.Pos += (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 8;
        Player.Cooldown = e.Cooldown = 4;
        Player.Companies.RemoveAll(c => c.Men + c.Wounded <= 0);
        Encounter = null;
    }

    public int ParleyPrice(Party e) => e.Kind == PartyKind.Bandit ? (int)(Rules.Strength(e) * 2 + 50) : -1;

    public bool Parley()
    {
        var e = Encounter!;
        int price = ParleyPrice(e);
        if (price < 0 || S.Gold < price) return false;
        S.Gold -= price;
        e.Cooldown = Player.Cooldown = 24;
        Log($"You pay {e.Name} {price} crowns to go their way.");
        Encounter = null;
        return true;
    }

    // ---------- your actions ----------

    public void MoveTo(Vector2 p) { Player.Goal = p; Player.Moving = true; Player.ChaseId = -1; Player.GoalSettlement = -1; Player.Resting = false; }
    public void Chase(Party q) { Player.ChaseId = q.Id; Player.Moving = true; Player.GoalSettlement = -1; Player.Resting = false; }
    public void GoTo(Settlement s) { Player.Goal = s.Pos; Player.Moving = true; Player.ChaseId = -1; Player.GoalSettlement = s.Id; Player.Resting = false; }

    int Slots => Rules.CompanySlots(Captain);
    int RoomForMen => Rules.MenLimit(Captain) - Player.Men - Player.Wounded;

    public string? RecruitLevies(Settlement s, int n)
    {
        n = Math.Min(n, s.Recruits);
        if (n <= 0) return "NO RECRUITS";
        string levy = RecruitTroop(s.Faction);
        int cost = n * Troops.Get(levy).Cost;
        if (S.Gold < cost) return $"NEEDS {cost} CROWNS";
        if (n > RoomForMen) return "PARTY IS FULL (RENOWN RAISES THE LIMIT)";
        var into = Player.Companies.FirstOrDefault(c => c.Troop == levy && c.Men + n <= Rules.Cap(levy, Rules.RoleOf(c)));
        if (into == null)
        {
            if (Player.Companies.Count >= Slots) return "MAX 10 COMPANIES";
            Player.Companies.Add(into = NewCompany(levy, "infantry", 0, 0));
        }
        Rules.AddMen(into, n);
        s.Recruits -= n;
        S.Gold -= cost;
        return null;
    }

    public int MercenaryPrice(CompanyRecord m) => (int)(m.Men * m.Type.Cost * B.MercMarkup);

    public string? Hire(Settlement s, CompanyRecord m)
    {
        int price = MercenaryPrice(m);
        if (S.Gold < price) return $"NEEDS {price} CROWNS";
        if (Player.Companies.Count >= Slots) return "NO FREE COMPANY";
        if (m.Men > RoomForMen) return "PARTY IS FULL";
        S.Gold -= price;
        s.Mercenaries.Remove(m);
        Player.Companies.Add(m);
        Log($"{m.Name} ({m.Men} {m.Type.Name}) take your coin.");
        return null;
    }

    public int HorsePrice => (int)(Troops.Mount("courser").Hp * B.HorseCostRate * (Captain.Perks.Contains(Perk.Horsemaster) ? 0.7f : 1));

    public string? BuyHorses(Settlement s, int n)
    {
        n = Math.Min(n, s.Horses);
        if (n <= 0) return "NO HORSES FOR SALE";
        if (S.Gold < n * HorsePrice) return $"NEEDS {n * HorsePrice} CROWNS";
        S.Gold -= n * HorsePrice;
        s.Horses -= n;
        Player.Horses += n;
        return null;
    }

    public (string Name, int Cost, bool Owned)[] Gear() => new[]
    {
        ("PLATE ARMOUR", (int)B.ArmorCost, Captain.ArmorTier > 0),
        ("GREAT HELM", (int)B.HelmCost, Captain.HelmTier > 0),
        ("WARHORSE", (int)B.WarhorseCost, Captain.HorseTier >= 1),
        ("DESTRIER", (int)B.DestrierCost, Captain.HorseTier >= 2),
    };

    public string? BuyGear(int i)
    {
        var (name, cost, owned) = Gear()[i];
        if (owned) return "OWNED";
        if (i == 3 && Captain.HorseTier < 1) return "BUY A WARHORSE FIRST";
        if (S.Gold < cost) return $"NEEDS {cost} CROWNS";
        S.Gold -= cost;
        switch (i) { case 0: Captain.ArmorTier = 1; break; case 1: Captain.HelmTier = 1; break; case 2: Captain.HorseTier = 1; break; default: Captain.HorseTier = 2; break; }
        Log($"You buy a {name.ToLowerInvariant()}.");
        return null;
    }

    public bool CanUpgrade(CompanyRecord c, string to, out int cost, out string reason) =>
        Rules.CanUpgrade(c, to, S.Gold, Player.Horses, InTown(Player), Captain.Perks.Contains(Perk.Drillmaster), out cost, out reason);

    public string? Upgrade(CompanyRecord c, string to)
    {
        if (!CanUpgrade(c, to, out int cost, out string reason)) return reason;
        S.Gold -= cost;
        Player.Horses -= Rules.Upgrade(c, to, Player.Horses);
        Log($"{c.Name} are now {c.Type.Name}.");
        return null;
    }

    public void Merge(CompanyRecord a, CompanyRecord b) { Rules.Merge(a, b); Player.Companies.Remove(b); }

    public string? Split(CompanyRecord c)
    {
        if (Player.Companies.Count >= Slots) return "NO FREE COMPANY";
        if (c.Men < 2) return "TOO FEW MEN";
        int half = c.Men / 2;
        var n = NewCompany(c.Troop, Rules.RoleOf(c), half, c.Xp);
        n.Cohesion = c.Cohesion;
        n.Name = c.Name + " II";
        c.Men -= half;
        Player.Companies.Insert(Player.Companies.IndexOf(c) + 1, n);
        return null;
    }

    public void Disband(CompanyRecord c) { Player.Companies.Remove(c); Log($"{c.Name} are paid off and go home."); }

    public void MoveUp(CompanyRecord c)
    {
        int i = Player.Companies.IndexOf(c);
        if (i > 0) (Player.Companies[i - 1], Player.Companies[i]) = (Player.Companies[i], Player.Companies[i - 1]);
    }

    public bool TakePerk(Perk p)
    {
        if (Rules.PerkPoints(Captain) <= 0 || Captain.Perks.Contains(p)) return false;
        Captain.Perks.Add(p);
        return true;
    }

    public void SignContract(int faction)
    {
        if (faction != Captain.Contract)
            foreach (var f in S.Settlements.Where(t => t.Owner == Player.Id))
            {
                f.Owner = -1; // fiefs go back to the liege you leave
                Log($"You give {f.Name} back to the {FactionNames[f.Faction]}.");
            }
        Captain.Contract = faction;
        Log(faction < 0 ? "You are a free captain again." : $"You sign on with the {FactionNames[faction]}.");
    }

    /// Rest where you are for some hours (healing is faster in a town).
    public void Rest(float hours)
    {
        Player.Resting = true;
        Player.Moving = false;
        Tick(hours);
        Player.Resting = false;
    }

    // ---------- AI ----------

    void SpawnLord(int faction)
    {
        var towns = S.Settlements.Where(t => t.Faction == faction && t.Kind != SettlementKind.Village).ToList();
        if (towns.Count == 0) return;
        var home = rng.Pick(towns);
        var n = Names.Get();
        var lord = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Lord, Faction = faction, HomeId = home.Id, Pos = home.Pos + new Vector2(rng.Range(-4, 4), rng.Range(-4, 4)),
            Name = "Lord " + rng.Pick(n.Lords) + " " + rng.Pick(n.Epithets), Gold = 400, Tier = 1 + rng.Next(2),
        };
        lord.Goal = lord.Pos;
        int tier = Math.Clamp(1 + rng.Next(2) + Day / 30, 1, 7);
        foreach (var role in new[] { "infantry", "archers", rng.Next(2) == 0 ? "spears" : "horse" })
        {
            string troop = Armies.LineTroop(role, Math.Clamp(tier + rng.Next(-1, 1), 1, 7), faction);
            lord.Companies.Add(NewCompany(troop, role, (int)(Rules.Cap(troop, role) * rng.Range(0.7f, 1f)), 0));
        }
        S.Parties.Add(lord);
    }

    void SpawnBandits()
    {
        var villages = S.Settlements.Where(v => v.Kind == SettlementKind.Village).ToList();
        var at = rng.Pick(villages);
        var n = Names.Get();
        // Bands grow as the campaign does, so brigandry stays a threat instead of becoming free loot for a growing company.
        int tier = Math.Clamp(1 + Day / (int)MathF.Max(1, B.BanditTierPerDays), 1, 5);
        int men = B.BanditBandMin + rng.Next(Math.Max(1, B.BanditBandMax - B.BanditBandMin)) + Day / (int)MathF.Max(1, B.BanditGrowPerDays);
        string foot = Armies.LineTroop("infantry", tier, rng.Next(3));
        var band = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Bandit, Name = rng.Pick(n.Bandits), Tier = tier,
        };
        band.Pos = at.Pos + new Vector2(rng.Range(-14, 14), rng.Range(-14, 14)) * Map.Spread;
        band.Goal = band.Pos;
        band.Companies.Add(NewCompany(foot, "infantry", men, 0));
        if (rng.Chance(0.6f))
            band.Companies.Add(NewCompany(Armies.LineTroop(rng.Chance(0.6f) ? "archers" : "horse", tier, rng.Next(3)), rng.Chance(0.6f) ? "archers" : "horse", rng.Next(5, 10) + Day / 20, 0));
        foreach (var c in band.Companies) if (c.Troop == "outriders") c.Role = "horse"; else if (c.Troop == "bowmen") c.Role = "archers";
        S.Parties.Add(band);
    }

    void RefreshMercenaries(Settlement t)
    {
        t.Mercenaries.Clear();
        int offers = 1 + rng.Next(2);
        for (int i = 0; i < offers; i++)
        {
            string role = rng.Pick(new[] { "infantry", "archers", "spears", "horse" });
            int tier = Math.Clamp(2 + Day / 40 + (rng.Chance(0.35f) ? 1 : 0), 2, 6);
            string troop = Armies.LineTroop(role, tier, t.Faction);
            var m = NewCompany(troop, role, (int)(Rules.Cap(troop, role) * rng.Range(0.6f, 0.9f)), 0);
            m.Xp = rng.Range(0, Math.Max(1, m.Type.UpgradeXp) * 0.5f);
            m.Name = Rules.CompanyName(rng, role);
            t.Mercenaries.Add(m);
        }
    }

    void Hourly()
    {
        foreach (var p in S.Parties.ToList())
        {
            if (p.Kind == PartyKind.Player) continue;
            if (p.Down)
            {
                if (p.Kind == PartyKind.Lord && S.Hours >= p.DownUntil)
                {
                    p.Down = false;
                    if (p.Men < 10) p.Companies.Add(NewCompany(RecruitTroop(p.Faction), "infantry", 20, 0));
                    p.Task = "refit";
                }
                continue;
            }
            if (S.Hours < p.TaskUntil && p.Task != "refit") continue;
            if (p.Kind == PartyKind.Lord) LordThink(p); else BanditThink(p);
        }
        CheckContracts();
        SiegeHourly();
    }

    IEnumerable<Party> Visible(Party p, float range) => S.Parties.Where(q => q != p && !q.Down && Vector2.Distance(p.Pos, q.Pos) < range);

    void LordThink(Party p)
    {
        var home = SettlementById(p.HomeId);
        float me = Rules.Strength(p);
        var hostiles = Visible(p, B.Vision).Where(q => Hostile(p, q)).ToList();
        var threat = hostiles.Where(q => Rules.Strength(q) > me * 1.3f && Vector2.Distance(q.Pos, p.Pos) < 25).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault();
        // Go home to refit when the company is thin, or to spend an upgrade it can already pay for. Refit buys what the
        // purse allows and the faction treasury covers the rest, so a lord who has lost men goes home even when it is poor.
        bool needsRefit = p.Men < 45 || p.Companies.Count < 3
            || p.Companies.Any(c => Rules.UpgradeReady(c) && p.Gold >= Rules.UpgradeCost(c, c.Type.UpgradesTo[0], p.Horses));
        if (threat != null)
        {
            if (p.SiegeTarget >= 0) Log($"{p.Name} abandons the siege of {SettlementById(p.SiegeTarget).Name}.");
            p.SiegeTarget = -1;
            p.Task = "flee"; p.ChaseId = -1; p.Goal = home.Pos; p.Moving = true; p.TaskUntil = (float)S.Hours + 3;
        }
        else if (p.SiegeTarget >= 0)
        {
            p.Task = "siege"; p.ChaseId = -1; p.Moving = false; p.TaskUntil = (float)S.Hours + 4;
        }
        else if (!needsRefit && TryBesiege(p)) { }
        else if (needsRefit || p.Task == "refit")
        {
            p.Task = "refit"; p.ChaseId = -1; p.Goal = home.Pos; p.Moving = true;
            if (Vector2.Distance(p.Pos, home.Pos) < 2 * Map.Spread) { Refit(p, home); p.Task = "rest"; p.TaskUntil = (float)S.Hours + 12; }
        }
        else if (hostiles.Where(q => Rules.Strength(q) < me * (p.PatrolProvince >= 0 && S.Provinces[p.PatrolProvince].Faction != p.Faction ? 1.3f : 1f))
                     .OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey) // a lord on campaign takes even fights
        {
            p.Task = "hunt"; p.ChaseId = prey.Id; p.Moving = true; p.TaskUntil = (float)S.Hours + 6;
        }
        else
        {
            p.Task = "patrol"; p.ChaseId = -1;
            var prov = p.PatrolProvince >= 0 ? S.Provinces[p.PatrolProvince] : S.Provinces[home.Province];
            // A lord with no enemy in sight marches for the front rather than idling around home, so armies meet on their own.
            if (AtWarWithAnyone(p.Faction) && rng.Chance(B.LordAggression) && EnemyProvinceNear(prov, p.Faction) is Province front) prov = front;
            p.Goal = prov.Center + new Vector2(rng.Range(-20, 20), rng.Range(-20, 20)) * Map.Spread;
            p.Moving = true;
            p.TaskUntil = (float)S.Hours + 10;
            // A lord in an enemy province threatens its villages (they stop giving recruits for a week).
            if (FactionsAtWar(p.Faction, prov.Faction))
                foreach (var v in S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.Province == prov.Id && Vector2.Distance(v.Pos, p.Pos) < 12 * Map.Spread && v.ThreatUntil < S.Hours))
                {
                    v.ThreatUntil = (float)S.Hours + 7 * 24;
                    v.Recruits = 0;
                    Score(p.Faction, prov.Faction, 20);
                    Log($"{p.Name} threatens {v.Name}.");
                }
        }
    }

    /// The nearest province held by a faction `f` is at war with, reachable through `from` (or `from` itself when it is enemy ground).
    Province? EnemyProvinceNear(Province from, int f)
    {
        if (FactionsAtWar(f, from.Faction) && from.Faction >= 0) return from;
        var enemy = S.Provinces.Where(p => FactionsAtWar(f, p.Faction) && p.Neighbors.Contains(from.Id)).OrderBy(p => Vector2.Distance(p.Center, from.Center)).FirstOrDefault();
        if (enemy != null) return enemy;
        // Two hops out, so a lord behind its own border still finds a front to march to.
        return S.Provinces.Where(p => FactionsAtWar(f, p.Faction) && p.Neighbors.Any(n => S.Provinces[n].Neighbors.Contains(from.Id)))
            .OrderBy(p => Vector2.Distance(p.Center, from.Center)).FirstOrDefault();
    }

    /// At home: recruit levies, upgrade companies that are ready, by the same rules and prices as yours. A lord who cannot
    /// pay draws on his faction's war chest, up to a share of it per visit, so a beaten lord rebuilds instead of dying broke
    /// while the chest still has money to raise new lords.
    void Refit(Party p, Settlement home)
    {
        var fs = S.Factions[Math.Max(0, p.Faction)];
        // A beaten lord may draw on the war chest, but only his share of it: half the chest is split between the realm's
        // lords, so a run of refits costs the realm at most half of what it holds and it can still raise new lords (600).
        int lordsOfFaction = Math.Max(1, S.Parties.Count(q => q.Kind == PartyKind.Lord && q.Faction == p.Faction));
        float share = MathF.Min(250 * Map.ProvinceScale, fs.Treasury * 0.5f / lordsOfFaction);
        float budget = Math.Clamp(share, 0, 250 * Map.ProvinceScale);
        string levy = RecruitTroop(p.Faction);
        if (p.Companies.Count == 0) p.Companies.Add(NewCompany(levy, "infantry", 12, 0)); // the lord's household, raised again
        foreach (var v in S.Settlements.Where(v => v.Province == home.Province))
        {
            int affordable = (int)((p.Gold + budget) / 10);
            int n = Math.Min(v.Recruits, Math.Max(0, affordable));
            if (n <= 0) continue;
            var into = p.Companies.FirstOrDefault(c => c.Troop == levy && c.Men + n <= Rules.Cap(levy, Rules.RoleOf(c)));
            if (into == null && p.Companies.Count < 5) p.Companies.Add(into = NewCompany(levy, rng.Pick(new[] { "infantry", "spears", "archers" }), 0, 0));
            if (into == null) break;
            Rules.AddMen(into, n);
            v.Recruits -= n;
            int bill = n * 10;
            int fromChest = Math.Min(bill, (int)budget);
            fs.Treasury -= fromChest;
            budget -= fromChest;
            p.Gold -= bill - fromChest;
        }
        foreach (var c in p.Companies.Where(Rules.UpgradeReady).ToList())
        {
            var options = c.Type.UpgradesTo;
            string to = c.Type.Tier == 1 ? Armies.LineTroop(Rules.RoleOf(c), 2, p.Faction) : options[rng.Next(options.Length)];
            if (!options.Contains(to)) to = options[0];
            int cost = Rules.UpgradeCost(c, to, p.Horses);
            int fromChest = Math.Min(cost, (int)budget);
            if (p.Gold + fromChest < cost) continue;
            fs.Treasury -= fromChest;
            budget -= fromChest;
            p.Gold -= cost - fromChest;
            p.Horses -= Rules.Upgrade(c, to, p.Horses);
        }
        if (p.Companies.Count > 0) p.Tier = Math.Clamp((int)MathF.Round((float)p.Companies.Average(c => c.Type.Tier)), 1, 7);
    }

    void BanditThink(Party p)
    {
        float me = Rules.Strength(p);
        var near = Visible(p, 25).Where(q => Hostile(p, q)).ToList();
        // Brigands are opportunists, not soldiers: they run from anything near their own weight and only fall on a
        // clearly weaker party. With many more bands on the big map they would otherwise grind the lords to nothing.
        if (near.FirstOrDefault(q => Rules.Strength(q) > me * 1.05f) is Party threat)
        {
            var away = p.Pos - threat.Pos;
            p.Goal = p.Pos + (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 20 * Map.Spread;
            p.ChaseId = -1; p.Moving = true; p.TaskUntil = (float)S.Hours + 2;
        }
        else if (near.Where(q => Rules.Strength(q) < me * 0.9f).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.ChaseId = prey.Id; p.Moving = true; p.TaskUntil = (float)S.Hours + 4;
        }
        else
        {
            p.ChaseId = -1;
            p.Goal = p.Pos + new Vector2(rng.Range(-25, 25), rng.Range(-25, 25)) * Map.Spread;
            p.Moving = true; p.TaskUntil = (float)S.Hours + 8;
        }
    }

    void Daily()
    {
        foreach (var v in S.Settlements.Where(v => v.ThreatUntil <= S.Hours))
            v.Recruits = v.Kind == SettlementKind.Village ? Math.Min(B.VillageRecruitsMax, v.Recruits + 1) : Math.Min(12, v.Recruits + 2); // towns and castles raise levies too
        foreach (var p in S.Parties.Where(p => !p.Down))
        {
            bool town = InTown(p);
            foreach (var c in p.Companies)
            {
                if (c.Wounded > 0)
                {
                    int heal = Math.Min(c.Wounded, Math.Max(1, (int)MathF.Round(c.Wounded * (town ? B.HealTown : B.HealField) * (Winter ? 0.5f : 1))));
                    c.Wounded -= heal;
                    c.Men += heal;
                }
                if (town && c.Cohesion < 20) c.Cohesion = MathF.Min(20, c.Cohesion + 1);
                if (p.Kind != PartyKind.Bandit && c.Men > 0) c.Xp += (town ? B.DrillTown : B.DrillCamp) + (c.Traits.Contains("Drillmaster") ? 1 : 0); // daily drill
            }
        }
        EatDaily();
        // A company bled out away from battle (rearguards, desertion, hunger) does not leave you with nothing to command:
        // a handful of loyal men gather at the nearest friendly town, as they do after a lost battle.
        if (Player.Companies.Count == 0 && !S.GameOver)
        {
            var refuge = S.Settlements.Where(t => t.Kind == SettlementKind.Town && !Refuses(t)).OrderBy(t => Vector2.Distance(t.Pos, Player.Pos)).FirstOrDefault();
            if (refuge != null) { Player.Pos = refuge.Pos + new Vector2(5, 0); Player.Moving = false; }
            Player.Companies.Add(NewCompany(RecruitTroop(Math.Max(0, Captain.Contract)), "infantry", 10, 0));
            Log($"Your company is gone. Ten loyal men gather to your banner{(refuge != null ? " at " + refuge.Name : "")}.");
        }
        FactionAI();
        SiegesDaily();
        // Brigandry: several bands are raised a day while the map is under its cap, so a big world stays dangerous.
        for (int n = 0; n < BanditSpawnsWanted; n++)
            if (S.Parties.Count(p => p.Kind == PartyKind.Bandit && !p.Down) >= BanditsWanted || !rng.Chance(B.BanditSpawnChance)) break;
            else SpawnBandits();
        // A realm that holds close to half the seats unites the others against it: their truces with it end, and they make peace.
        int seats = S.Settlements.Count(s => s.Kind != SettlementKind.Village);
        for (int f = 0; f < S.Factions.Count && S.Factions.Count > 2; f++)
        {
            if (S.Settlements.Count(s => s.Kind != SettlementKind.Village && s.Faction == f) < seats * 0.45f) continue;
            foreach (var w in S.Wars)
            {
                bool involves = w.A == f || w.B == f;
                if (involves && !w.Active && Alive(w.A) && Alive(w.B)) { w.Active = true; w.ScoreA = w.ScoreB = 0; Log($"The realms rise against the {FactionNames[f]}: the {FactionNames[w.A == f ? w.B : w.A]} take up arms."); }
                if (!involves && w.Active) { w.Active = false; w.TruceUntil = (float)S.Hours + B.TruceDays * 24; Log($"The {FactionNames[w.A]} and the {FactionNames[w.B]} make peace against the {FactionNames[f]}."); }
            }
        }
        // War and truce, for each pair of factions.
        foreach (var w in S.Wars)
        {
            bool aDead = !Alive(w.A), bDead = !Alive(w.B);
            bool leaderInvolved = Enumerable.Range(0, S.Factions.Count).Any(f => (w.A == f || w.B == f) && S.Factions.Count > 2
                && S.Settlements.Count(s => s.Kind != SettlementKind.Village && s.Faction == f) >= seats * 0.45f);
            if (w.Active && ((MathF.Abs(w.ScoreA - w.ScoreB) > B.PeaceScore && !leaderInvolved) || aDead || bDead))
            {
                int loser = w.ScoreA < w.ScoreB ? w.A : w.B;
                w.Active = false;
                w.TruceUntil = (float)S.Hours + B.TruceDays * 24;
                w.ScoreA = w.ScoreB = 0;
                if (!aDead && !bDead) Log($"The {FactionNames[loser]} sues the {FactionNames[loser == w.A ? w.B : w.A]} for peace: a truce of {B.TruceDays} days.");
            }
            else if (!w.Active && S.Hours >= w.TruceUntil && !aDead && !bDead)
            {
                w.Active = true;
                Log($"The truce between the {FactionNames[w.A]} and the {FactionNames[w.B]} ends. War again.");
            }
        }
    }

    /// A faction lives while it holds a town or castle.
    public bool Alive(int f) => S.Settlements.Any(s => s.Faction == f && s.Kind != SettlementKind.Village);

    void FactionAI()
    {
        for (int f = 0; f < S.Factions.Count; f++)
        {
            var fs = S.Factions[f];
            if (!Alive(f)) continue;
            float income = S.Settlements.Where(t => t.Faction == f && t.Owner != Player.Id).Sum(Income);
            fs.Treasury += income / 7;
            var lords = S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == f).ToList();
            // A realm raises lords with the ground it holds: more provinces, more armies, but never more than the seats (and
            // their villages' recruits) can actually pay for, or the chest is bled dry fielding companies that starve.
            int seatsHeld = S.Settlements.Count(s => s.Faction == f && s.Kind != SettlementKind.Village && s.Owner != Player.Id);
            int affordable = Math.Max(1, seatsHeld * 2 + 2);
            int wanted = Math.Min(LordsWanted, affordable);
            // Keep a reserve in the chest before a new lord is raised, so a run of defeats does not leave the realm broke.
            if (lords.Count < wanted && fs.Treasury >= 600 + 400) { SpawnLord(f); fs.Treasury -= 600; }
            var front = S.Provinces.Where(p => p.Faction == f && p.Neighbors.Any(n => FactionsAtWar(f, S.Provinces[n].Faction))).ToList();
            var enemyFront = S.Provinces.Where(p => FactionsAtWar(f, p.Faction) && p.Neighbors.Any(n => S.Provinces[n].Faction == f)).ToList();
            var idle = lords.Where(l => !l.Down && l.Task == "patrol").ToList();
            if (idle.Count >= 2 && enemyFront.Count > 0 && rng.Chance(0.35f))
            {
                var target = rng.Pick(enemyFront);
                foreach (var l in idle.Take(5)) { l.PatrolProvince = target.Id; l.TaskUntil = 0; }
                Log($"The {FactionNames[f]} musters a host against {target.Name}.");
            }
            else
                foreach (var l in idle.Where(l => l.PatrolProvince < 0 || rng.Chance(0.15f)))
                    l.PatrolProvince = front.Count > 0 ? rng.Pick(front).Id : S.Settlements[l.HomeId].Province;
        }
    }

    void Weekly()
    {
        foreach (var p in S.Parties.Where(p => p.Kind != PartyKind.Bandit && !p.Down))
        {
            float upkeep = p.Companies.Sum(Rules.Upkeep);
            if (p == Player)
            {
                if (Captain.Perks.Contains(Perk.Quartermaster)) upkeep *= 0.9f;
                float pay = Captain.Contract >= 0 ? upkeep * B.ContractPay * Settings.Current.PayFactor : 0;
                float fiefs = S.Settlements.Where(t => t.Owner == Player.Id).Sum(Income);
                S.LastWeekBalance = pay + fiefs - upkeep;
                S.Gold += S.LastWeekBalance;
                if (S.Gold < 0)
                {
                    S.Gold = 0;
                    S.UnpaidWeeks++;
                    Desert(p);
                    Log("Your men go unpaid. Some desert.");
                }
                else S.UnpaidWeeks = 0;
            }
            else
            {
                var fs = S.Factions[p.Faction];
                int lordCount = Math.Max(1, S.Parties.Count(q => q.Kind == PartyKind.Lord && q.Faction == p.Faction));
                float wage = upkeep * B.LordWage + fs.Treasury * 0.02f / lordCount; // pay, plus a share of the war chest
                if (fs.Treasury < wage) wage = MathF.Max(0, fs.Treasury);
                fs.Treasury -= wage;
                p.Gold += wage - upkeep;
                if (p.Gold < 0) { p.Gold = 0; Desert(p); }
            }
        }
        foreach (var t in S.Settlements.Where(t => t.Kind == SettlementKind.Town))
        {
            RefreshMercenaries(t);
            t.Horses = Math.Min(20, t.Horses + 5);
        }
        GarrisonsWeekly();
        PrisonersWeekly();
    }

    static void Desert(Party p)
    {
        foreach (var c in p.Companies)
        {
            c.Men -= (int)MathF.Ceiling(c.Men * 0.05f);
            c.Cohesion = MathF.Max(0, c.Cohesion - 10);
        }
        p.Companies.RemoveAll(c => c.Men + c.Wounded <= 0);
    }
}
