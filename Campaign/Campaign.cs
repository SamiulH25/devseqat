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
    /// The realms' names by faction id, including realms founded in play (F5).
    public string[] FactionNames => names is { } n && n.Length == S.Factions.Count ? n : names = S.Factions.Select(f => f.Name).ToArray();
    string[]? names;
    /// Party and event caps for the balance numbers scaled by how much world there is (a bigger map raises more lords and
    /// brigands). The numbers in Data/campaign.json are quoted against the original 26-province map.
    float ProvinceScale => Map.PartyScale;
    /// Lords spread thinner than brigands: a realm with four times the ground raises about twice the armies, not four
    /// times (village recruits are the bottleneck, and a crowd of lords starves all of them).
    /// LordsPerFaction is quoted for the three realms of the original map: more realms share the same land, so each raises
    /// fewer lords and the world's total follows the size of the map, not the number of realms (docs/WORLD.md W0).
    public int LordsWanted => (int)MathF.Round(B.LordsPerFaction * ProvinceScale * LegacyRealms / Math.Max(1, S.DataRealms));
    public const int LegacyRealms = 3;
    public int BanditsWanted => (int)MathF.Round(B.BanditsMax * ProvinceScale);
    int BanditSpawnsWanted => Math.Max(1, (int)MathF.Round(B.BanditSpawnsPerDay * ProvinceScale));

    public Campaign(CampaignState state)
    {
        S = state;
        Map = WorldMap.Generate(state.Seed, state.MapExtent, state.Layout, state.DataRealms);
        rng = new Rng(state);
        Factions.Bind(S.Factions);
        MigrateGear();
        LearnForms();
        MigrateSkills();
        // Saves from before F6 have no buildings: give their towns the barracks their elite lines need.
        if (S.Settlements.Count > 0 && S.Settlements.All(t => t.Buildings.Count == 0)) SeedBuildings();
        // The map's settlement models follow the save (a v1 save was generated before the Northmarch had its capital).
        for (int i = 0; i < Math.Min(Map.Settlements.Count, S.Settlements.Count); i++) { Map.Settlements[i].Kind = S.Settlements[i].Kind; Map.Settlements[i].Name = S.Settlements[i].Name; }
        // ...and so do the names (a save keeps the names it was made with, whatever the name lists say now).
        for (int i = 0; i < Math.Min(Map.Provinces.Count, S.Provinces.Count); i++) Map.Provinces[i].Name = S.Provinces[i].Name;
    }

    // ---------- a new campaign ----------

    /// A new campaign; `realm` is the realm you start in (docs/WORLD.md W5): its capital, its villages' recruits.
    public static Campaign New(int seed, int realm = 0)
    {
        var s = new CampaignState { Seed = seed, Gold = B.StartGold, Ironman = Settings.Current.Ironman, MapExtent = WorldMap.Extent,
            Layout = WorldMap.CurrentLayout, DataRealms = Factions.All.Count };
        s.Captain.Renown = B.StartRenown;
        StartingSkills(s.Captain);
        var map = WorldMap.Generate(seed, s.MapExtent, s.Layout, s.DataRealms);
        s.Provinces = map.Provinces;
        s.Settlements = map.Settlements;
        int factions = s.Provinces.Max(p => p.Faction) + 1;
        s.Factions = Enumerable.Range(0, factions).Select(f => new FactionState { Id = f, Name = Factions.All[f].Name, Treasury = 2000, NextCouncil = (7 + 3 * f) * 24 }).ToList();
        // The opening wars and truces come from Data/factions.json (border wars: the Crown and the Northmarch, the Compact and the Khaganate).
        foreach (var r in Factions.Relations)
        {
            int a = Factions.IndexOf(r.A), b = Factions.IndexOf(r.B);
            if (a < 0 || b < 0 || a >= factions || b >= factions) continue;
            s.Wars.Add(r.War ? new War { A = a, B = b } : new War { A = a, B = b, Active = false, TruceUntil = r.TruceDays * 24 });
        }
        var c = new Campaign(s);
        realm = Math.Clamp(realm, 0, factions - 1);
        // The Crown's westernmost town (as it always was); another realm's town nearest the middle of its land.
        var towns = s.Settlements.Where(t => t.Kind == SettlementKind.Town && t.Faction == realm).ToList();
        var middle = s.Provinces.Where(p => p.Faction == realm).Aggregate(Vector2.Zero, (a, p) => a + p.Center) / Math.Max(1, s.Provinces.Count(p => p.Faction == realm));
        var capital = realm == 0 ? towns.OrderBy(t => t.Pos.X).First() : towns.OrderBy(t => Vector2.Distance(t.Pos, middle)).First();
        var player = new Party { Id = s.NextId++, Kind = PartyKind.Player, Name = "Your company", Faction = realm, Pos = capital.Pos + new Vector2(2, 0), Tier = 2 };
        player.Goal = player.Pos;
        s.Parties.Add(player);
        if (s.Captain.Contract >= 0) s.Captain.Contract = realm; // you start in the pay of the realm you chose
        player.Companies.Add(c.NewCompany(Factions.Root(Factions.CultureOf(realm)), "infantry", c.Scaled(24), 0, capital));
        player.Companies.Add(c.NewCompany("levies", "spears", c.Scaled(15), 0, capital));
        c.MakeCaptainCharacter(capital.Culture);
        c.SeedBuildings();
        foreach (var v in s.Settlements)
        {
            v.Recruits = v.Kind == SettlementKind.Village ? c.Scaled(4 + c.rng.Next(5)) : 0;
            v.Horses = v.Kind == SettlementKind.Town ? c.Scaled(10) : 0;
            if (v.Kind == SettlementKind.Town) c.RefreshMercenaries(v);
            c.RaiseGarrison(v);
        }
        for (int f = 0; f < factions; f++)
            for (int i = 0; i < c.LordsWanted; i++) c.SpawnLord(f);
        // Open with a map's worth of brigands, not three: the roads are dangerous from the first day.
        for (int i = 0; i < c.BanditsWanted / 3; i++) c.SpawnBandits();
        // The wilds already have their goblins, some of them well dug in.
        for (int i = 0; i < c.HoldoutsWanted; i++)
            if (c.SpawnHoldout()) for (int d = c.rng.Next(40); d > 0; d--) c.Grow(c.S.Parties[^1]);
        for (int i = 0; i < c.WolfPacksWanted; i++) c.SpawnWolfPack();
        for (int i = 0; i < c.RaresWanted; i++) c.SpawnTroll();
        c.WanderersWeekly();
        c.EnsureRulers();
        // The guided first fight: a weak band close by, coming for you.
        var first = new Party { Id = s.NextId++, Kind = PartyKind.Bandit, Name = "Ditch Wolves", Pos = player.Pos + new Vector2(0, 16), Tier = 1, ChaseId = player.Id, Moving = true, TaskUntil = 72 };
        first.Companies.Add(c.NewCompany("levies", "infantry", c.Scaled(9), 0));
        s.Parties.Add(first);
        c.Log($"You sign on with the {c.FactionNames[0]} as a free captain. Their steward pays 85% of your upkeep each week.");
        c.Log("Levies upgrade after a battle or two: visit a town's training yard.");
        return c;
    }

    public void Log(string text)
    {
        S.Log.Add(new LogEntry { At = S.Hours, Text = text });
        if (S.Log.Count > 60) S.Log.RemoveAt(0);
    }

    public Party? PartyById(int id) => S.Parties.FirstOrDefault(p => p.Id == id);
    public Settlement SettlementById(int id) => S.Settlements[id];

    // ---------- relations ----------

    public int SideOf(Party p) => p.Kind switch { PartyKind.Player => Captain.Contract, PartyKind.Bandit => -2, PartyKind.Monster => -3, _ => p.Faction };
    /// Brigands and monsters: parties outside any realm, with no pay, no drill and no prisoners to ransom.
    public static bool Outlaw(Party p) => p.Kind is PartyKind.Bandit or PartyKind.Monster;

    public bool Hostile(Party a, Party b)
    {
        if (a == b || a.Down || b.Down) return false;
        if (a.Prey && b.Kind == PartyKind.Player || b.Prey && a.Kind == PartyKind.Player) return true;
        if (a.Kind == PartyKind.Monster || b.Kind == PartyKind.Monster) return a.Kind != b.Kind; // goblins fall on men and brigands alike
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
        // Exhaustion (F7): the side that loses men, land or villages tires four times faster than the side that wins.
        AddExhaustion(w, loser, amount / 60f);
        AddExhaustion(w, winner, amount / 240f);
        if (winner < S.Factions.Count) S.Factions[winner].WarScore += amount;
    }

    /// Enemy towns will not trade with you.
    public bool Refuses(Settlement s) => Captain.Contract >= 0 && FactionsAtWar(Captain.Contract, s.Faction);

    /// How far you can make out who a party is (S7): a Scout sees a quarter further, a watchtower's view doubles it for a while.
    /// Your fiefs watch the country around them too.
    public float PlayerSight => B.Vision * 1.6f * Map.Spread * (HasCompanion("Scout") ? 1.25f : 1) * (S.Hours < S.FarSightUntil ? 2 : 1);
    public bool InSight(Vector2 p) => Vector2.Distance(p, Player.Pos) < PlayerSight || Fiefs.Any(f => f.Kind != SettlementKind.Village && Vector2.Distance(f.Pos, p) < 30 * Map.Spread);

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
            Probe?.Invoke(-1);
            if ((int)before != (int)S.Hours) Hourly();
            Probe?.Invoke(0);
            if ((int)(before / 24) != (int)(S.Hours / 24)) Daily();
            Probe?.Invoke(1);
            if ((int)(before / 168) != (int)(S.Hours / 168)) Weekly();
            Probe?.Invoke(2);
            Move(h);
            Probe?.Invoke(3);
            Encounters();
            Probe?.Invoke(4);
        }
    }

    /// Set by the campaign bench (`--campaign-bench`): called at the end of each tick stage (index into <see cref="Stages"/>;
    /// -1 = step start).
    public Action<int>? Probe;
    public static readonly string[] Stages = { "hourly", "daily", "weekly", "move", "encounters" };

    public float Speed(Party p)
    {
        bool mounted = p.Breed == "wolf" || p.Companies.Count > 0 && p.Companies.All(c => c.Men == 0 || c.Type.Mount != null); // wolves run as fast as horses
        float s = (mounted ? B.HorseSpeed : B.FootSpeed) * (Winter ? 0.75f : 1);
        if (p.Kind == PartyKind.Player) s *= 1 - MathF.Min(0.25f, p.Prisoners.Sum(g => g.Count) / 400f);
        if (p.Kind == PartyKind.Player)
            s *= (1 + Skill("Logistics") / 2000f) * (Has("Horsemaster") ? 1.05f : 1) * (Has("Pathfinder") ? 1.05f : 1) * (Has("ForcedMarch") ? 1.1f : 1);
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
                if (p.Kind == PartyKind.Player && SiteById(S.GoalSite) is Site site) { S.GoalSite = -1; ArriveAtSite(site); }
            }
            else p.Pos += d / dist * step;
            if (p.Kind == PartyKind.Player) Practise("Logistics", MathF.Min(dist, step) * 0.006f); // days on the march (F4)
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
        if (a == b || a.Cooldown > 0 || b.Cooldown > 0) return;
        bool playerInvolved = a.Kind == PartyKind.Player || b.Kind == PartyKind.Player;
        // Riding up to travellers who are not your enemy is a meeting, not a fight (S4).
        if (playerInvolved && (a.Kind == PartyKind.Player ? b : a) is { Kind: PartyKind.Caravan } trav && Player.ChaseId == trav.Id && !Hostile(a, b))
        {
            if (Vector2.DistanceSquared(a.Pos, b.Pos) > B.EncounterRadius * B.EncounterRadius) return;
            Encounter = trav; Player.Moving = false;
            return;
        }
        if (!Hostile(a, b)) return;
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
        if (a.Lair || a.Breed is "troll" or "undead") (a, b) = (b, a); // a holdout fights dug in, from side 1; so do a troll and the dead
        var r = Rules.AutoResolve(new[] { a }, new[] { b }, rng, 1, Edge(b));
        var winner = r.Winner == 0 ? a : b;
        var loser = r.Winner == 0 ? b : a;
        AiBattles++;
        Apply(r, new[] { a }, new[] { b }, "");
        if (winner.Kind == PartyKind.Lord || loser.Kind == PartyKind.Lord)
            Log($"{winner.Name} defeats {loser.Name} near {Map.ProvinceAt(loser.Pos).Name}.");
    }

    /// How much harder a party fights than its strength says, on the defending side of an auto-resolve: a holdout dug in,
    /// a troll in the dark (and the sun makes it slow and weak, as in a battle); the dead by night (by day they lie in the earth).
    /// How much harder a party fights than its strength says: a holdout's den, a troll by night, the dead by night.
    public float Edge(Party p) => p.Lair ? B.HoldoutDefence : p.Breed == "troll" ? (Night ? B.TrollNight : B.TrollDay) : p.Breed == "undead" ? (Night ? B.UndeadNight : B.UndeadDay) : 1;

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
                    if (p.Men < Scaled(10)) p.Companies.Add(NewCompany(RecruitTroop(p.Faction), "infantry", Scaled(20), 0, SettlementById(p.HomeId)));
                    p.Task = "refit";
                }
                continue;
            }
            if (S.Hours < p.TaskUntil && p.Task != "refit") continue;
            if (p.Kind == PartyKind.Lord) LordThink(p); else if (p.Kind == PartyKind.Monster) MonsterThink(p); else if (p.Kind == PartyKind.Caravan) CaravanThink(p); else BanditThink(p);
        }
        CheckContracts();
        SiegeHourly();
        SummonsHourly();
    }

    void Daily()
    {
        AgeDaily();
        BuffsDaily();
        foreach (var v in S.Settlements.Where(v => v.ThreatUntil <= S.Hours))
        {
            int more = (v.Owner == Player.Id && Has("JustRuler") ? 2 : 1) * (v.Prosperity >= 70 ? 2 : 1);
            if (v.Prosperity < 20 && rng.Chance(0.5f)) continue; // a poor village raises men slowly
            v.Recruits = v.Kind == SettlementKind.Village ? Math.Min(Scaled(B.VillageRecruitsMax), v.Recruits + Scaled(more)) : Math.Min(Scaled(12), v.Recruits + Scaled(2 * more)); // towns and castles raise levies too
        }
        foreach (var p in S.Parties.Where(p => !p.Down))
        {
            bool town = InTown(p);
            foreach (var c in p.Companies)
            {
                foreach (var st in c.Stacks)
                {
                    if (st.Wounded > 0)
                    {
                        float surgeon = p == Player ? (HasCompanion("Surgeon") ? 1.3f : 1) * (!town && Has("FieldHospital") ? 1.5f : 1) * (HasBuff("poultices") ? 1.5f : 1) : 1;
                        int heal = Math.Min(st.Wounded, Math.Max(1, (int)MathF.Round(st.Wounded * (town ? B.HealTown : B.HealField) * (Winter ? 0.5f : 1) * surgeon)));
                        if (p == Player) Practise("Medicine", Rules.PerMan(heal * 0.1f));
                        st.Wounded -= heal;
                        st.Men += heal;
                    }
                    // Daily drill: per fit man, into each pool (Old Hands drill harder).
                    if (!Outlaw(p) && st.Men > 0)
                        st.AddXp(st.Men * ((town ? B.DrillTown : B.DrillCamp) + (c.Traits.Contains("Drillmaster") ? 1 : 0)) * Honours.Of(c).DrillMul
                            * (p == Player ? (HasCompanion("Drillmaster") ? 1.25f : 1) * (1 + Skill("Training") / 200f) * (Has("Academy") ? 1.5f : 1) : 1));
                }
                if (town && c.Cohesion < 20) c.Cohesion = MathF.Min(20, c.Cohesion + 1);
            }
        }
        RefreshHeroes();
        RollDaily();
        EatDaily();
        // A company bled out away from battle (rearguards, desertion, hunger) does not leave you with nothing to command:
        // a handful of loyal men gather at the nearest friendly town, as they do after a lost battle.
        if (Player.Companies.Count == 0 && !S.GameOver)
        {
            var refuge = S.Settlements.Where(t => t.Kind == SettlementKind.Town && !Refuses(t)).OrderBy(t => Vector2.Distance(t.Pos, Player.Pos)).FirstOrDefault();
            if (refuge != null) { Player.Pos = refuge.Pos + new Vector2(5, 0); Player.Moving = false; }
            Player.Companies.Add(NewCompany(RecruitTroop(Math.Max(0, Captain.Contract)), "infantry", Scaled(10), 0));
            Log($"Your company is gone. Ten loyal men gather to your banner{(refuge != null ? " at " + refuge.Name : "")}.");
        }
        FactionAI();
        SiegesDaily();
        RainDaily();
        FiefsDaily();
        CareerDaily();
        EventsDaily();
        // Brigandry: several bands are raised a day while the map is under its cap, so a big world stays dangerous.
        for (int n = 0; n < BanditSpawnsWanted; n++)
            if (S.Parties.Count(p => p.Kind == PartyKind.Bandit && !p.Down) >= BanditsWanted || !rng.Chance(B.BanditSpawnChance)) break;
            else SpawnBandits();
        MonstersDaily();
        RoadsDaily();
        // A realm that holds too many of the seats (LeaderShare: close to half of three realms', less with more) unites the others
        // against it: their truces with it end, its neighbours take up arms, and they make peace among themselves.
        for (int f = 0; f < S.Factions.Count && S.Factions.Count > 2; f++)
        {
            if (!Leading(f)) continue;
            for (int g = 0; g < S.Factions.Count; g++)
                if (g != f && Alive(g) && WarBetween(f, g) == null && Borders(f, g) && S.Factions[g].TributaryOf != f)
                    S.Wars.Add(new War { A = g, B = f, Active = false });
            foreach (var w in S.Wars)
            {
                bool involves = w.A == f || w.B == f;
                if (involves && !w.Active && Alive(w.A) && Alive(w.B) && S.Factions[w.A == f ? w.B : w.A].TributaryOf != f) { w.Active = true; w.ScoreA = w.ScoreB = 0; w.Since = (float)S.Hours; Log($"The realms rise against the {FactionNames[f]}: the {FactionNames[w.A == f ? w.B : w.A]} take up arms."); }
                if (!involves && w.Active) { w.Active = false; w.TruceUntil = (float)S.Hours + B.TruceDays * 24; Log($"The {FactionNames[w.A]} and the {FactionNames[w.B]} make peace against the {FactionNames[f]}."); }
            }
        }
        // War and peace, for each pair of factions (F7: exhaustion, terms, wars declared on purpose).
        DiplomacyDaily();
    }

    void Weekly()
    {
        RelationsWeekly();
        CompanionsWeekly();
        WanderersWeekly();
        TraitsWeekly();
        GuardWeekly();
        BuildWeekly(); // before wages: lords' pay takes a share of the chest down to its floor
        foreach (var p in S.Parties.Where(p => !Outlaw(p) && !p.Down))
        {
            float upkeep = p.Companies.Sum(Rules.Upkeep);
            if (p == Player)
            {
                if (Has("Quartermaster")) upkeep *= 0.9f;
                if (HasCompanion("Quartermaster")) upkeep *= 0.95f;
                float pay = Captain.Contract >= 0 && Rank != Rank.King ? upkeep * B.ContractPay * Settings.Current.PayFactor : 0;
                // A sworn man's liege keeps him in the field (F9): a retainer that lets a beaten company rebuild.
                pay += Retainer;
                float fiefs = S.Settlements.Where(t => t.Owner == Player.Id).Sum(Income) * (HasCompanion("Steward") ? 1.15f : 1)
                    * (1 + Skill("Stewardship") / 500f) * (Has("TaxRoll") ? 1.15f : 1);
                if (fiefs > 0) Practise("Stewardship", 0.5f + fiefs / 40f);
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
                // The chest keeps a floor: a realm that cannot pay its lords sees them thin out, not go bankrupt.
                wage = MathF.Min(wage, MathF.Max(0, fs.Treasury - 300));
                fs.Treasury -= wage;
                p.Gold += wage - upkeep;
                if (p.Gold < 0) { p.Gold = 0; Desert(p); }
            }
        }
        foreach (var t in S.Settlements.Where(t => t.Kind == SettlementKind.Town))
        {
            RefreshMercenaries(t);
            t.Horses = Math.Min(Scaled(Level(t, "stables") > 0 ? 35 : 20), t.Horses + Scaled(Level(t, "stables") > 0 ? 10 : 5));
        }
        foreach (var t in S.Settlements.Where(t => Level(t, "kennel") > 0)) t.Hounds = Math.Min(Scaled(B.KennelMax), t.Hounds + Scaled(B.KennelPerWeek));
        GarrisonsWeekly();
        PrisonersWeekly();
        PiousWeekly();
        TributeWeekly();
    }

    internal static void Desert(Party p)
    {
        foreach (var c in p.Companies.Where(c => !c.Traits.Contains(HouseholdTrait))) // the household never deserts
        {
            c.RemoveFit((int)MathF.Ceiling(c.Men * 0.05f), true); // the lowest tiers leave first
            c.Prune();
            c.Cohesion = MathF.Max(0, c.Cohesion - 10);
        }
        p.Companies.RemoveAll(c => c.Men + c.Wounded <= 0);
    }
}
