using Microsoft.Xna.Framework;

namespace Ironfield;

/// The Wild (docs/pre-demo/THE_WILD.md WI7-WI8):
/// WI7: Legendary named apex monsters (one per region) that hunt lords too, pay top spoils, and grant chronicle & banner honours.
/// WI8: The World Crisis endgame ("The Long Night" or "The Goblin Horde") — warnings/omens, outbreak with coalition truces,
/// crisis hosts marching on towns, and resolution when the crisis seat falls (or world fall if too many towns are lost).
public partial class Campaign
{
    // ---------- WI7: legendary named apexes ----------

    static readonly (string Key, string Breed, string NameFmt, string Apex, string Pack, int PackCount, string[] Lands)[] LegendaryDefs =
    {
        ("spider_wood",  "spider", "the Grey Widow of {0}",    "brood_mother", "spider",      8, new[] { "Forest", "Marsh" }),
        ("giant_hills",  "giant",  "Grimhorn of {0}",          "giant_chief",  "hill_giant",  2, new[] { "Hills", "Highlands" }),
        ("bear_north",   "bear",   "Old Ironhide of {0}",      "bear_king",    "bear",        3, new[] { "Taiga", "Tundra" }),
        ("wyvern_crag",  "wyvern", "Ashwing the Dread of {0}", "wyvern",       "griffon",     2, new[] { "Highlands", "Badlands" }),
        ("wurm_dune",    "wurm",   "the Dune-Devourer of {0}", "sand_wurm",    "basilisk",    2, new[] { "Desert", "Oasis" }),
        ("wolf_taiga",   "wolf",   "Fenris-Eye of {0}",        "alpha_wolf",   "dire_wolf",   6, new[] { "Forest", "Taiga" }),
    };

    public IEnumerable<Party> Legendaries => S.Parties.Where(p => !p.Down && p.Legendary && !p.Crisis);

    /// Daily (WI7): wake one named apex per region over the course of the campaign.
    internal void SpawnLegendariesDaily()
    {
        if (Day < 7 || Legendaries.Count() >= 4) return;
        foreach (var def in LegendaryDefs)
        {
            if (S.LegendRegions.Contains(def.Key)) continue;
            if (!rng.Chance(0.06f)) continue;
            if (SpawnLegendary(def.Key) != null) break;
        }
    }

    /// Spawn a specific legendary apex by region key (used by SpawnLegendariesDaily and tests).
    public Party? SpawnLegendary(string key)
    {
        var def = LegendaryDefs.FirstOrDefault(d => d.Key == key);
        if (string.IsNullOrEmpty(def.Key) || S.LegendRegions.Contains(key)) return null;
        var pool = S.Provinces.Where(pr => def.Lands.Contains(pr.Kind)).ToList();
        if (pool.Count == 0) pool = S.Provinces.Where(Wild).ToList();
        if (pool.Count == 0) return null;
        var prov = rng.Pick(pool);
        var at = prov.Center + new Vector2(rng.Range(-10, 10), rng.Range(-10, 10)) * Map.Spread;
        if (Map.ProvinceAt(at).Id != prov.Id) at = prov.Center;
        var boss = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Monster, Breed = def.Breed, Legendary = true,
            Name = string.Format(def.NameFmt, prov.Name), Pos = at, Goal = at,
            Task = "hunt", PatrolProvince = prov.Id, Tier = 6, Gold = 500,
        };
        boss.Companies.Add(NewCompany(def.Apex, "infantry", 1, 0));
        boss.Companies.Add(NewCompany(def.Pack, "infantry", Math.Max(1, Scaled(def.PackCount)), 0));
        S.Parties.Add(boss);
        S.LegendRegions.Add(key);
        LogAll($"A terror stalks {prov.Title}: {boss.Name}.");
        Deed($"Word spreads of {boss.Name} prowling {prov.Title}.", "wild");
        return boss;
    }

    /// Legendary apex AI (WI7): hunts lords' hosts as well as travellers, changing the war around it.
    void LegendaryThink(Party p)
    {
        float me = Rules.Might(p);
        p.TaskUntil = (float)S.Hours + 2;
        var near = Visible(p, 28).Where(q => Hostile(p, q) && !q.Lair).ToList();
        if (near.FirstOrDefault(q => Rules.Might(q) > me * 2.4f && Vector2.Distance(q.Pos, p.Pos) < 10 * Map.Spread) is Party threat)
        {
            Flee(p, threat, 14);
            return;
        }
        // Hunts lords and armies it can beat (WI7).
        if (near.Where(q => Rules.Might(q) < me * 1.25f).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.Task = "hunt"; p.ChaseId = prey.Id; p.RaidOn = -1; p.Moving = true;
            return;
        }
        if (Ravage(p, 26)) return;
        Wander(p, 18, "prowl");
    }

    /// Top spoils, chronicle entry, banner honour, and aura breakthrough when a legendary apex falls (WI7).
    void OnLegendarySlain(Party p, Province prov, bool playerWon)
    {
        if (!playerWon)
        {
            LogAll($"{p.Name} has been slain in {prov.Title}.");
            return;
        }
        string spoil = p.Breed switch
        {
            "spider" => "spider_silk",
            "bear" => "bear_pelt",
            "wyvern" => "wyvern_sting",
            "wolf" => "dire_fang",
            "basilisk" or "wurm" => "basilisk_eye",
            "troll" or "giant" => "troll_hide",
            _ => "grave_dust",
        };
        S.Goods[spoil] = S.Goods.GetValueOrDefault(spoil) + 3;
        S.Gold += 500;
        Captain.Renown += 80;
        Deed($"Legend Slain: {p.Name} fell to your banner in {prov.Title}.", "wild");
        LogYours($"You slew {p.Name}! Took 3x {spoil.Replace('_', ' ')} and 500 crowns.");
        // Sew the legendary beast's name onto your foremost company's banner (WI7).
        var lead = Player.Companies.OrderByDescending(c => c.Men).FirstOrDefault();
        string honour = $"Slayer of {p.Name}";
        if (lead != null && !lead.BattleHonours.Contains(honour))
        {
            lead.BattleHonours.Add(honour);
            LogYours($"{lead.Name} carry a new banner honour: {honour}.");
        }
        if (Captain.AuraWall)
            BreakThrough($"Slaying the legendary {p.Name} shattered the wall holding back your aura.");
    }

    // ---------- WI8: the world crisis (endgame) ----------

    public const int CrisisTriggerDay = 180, CrisisWarnDays = 14, CrisisTownLossLimit = 4;

    public float AverageWildness => S.Provinces.Count == 0 ? 0 : S.Provinces.Average(p => p.Wildness);

    /// Short HUD banner text when a world crisis is gathering or active ("" when dormant or ended).
    public string CrisisBannerText() => S.Crisis.Stage switch
    {
        1 => S.Crisis.Kind == "long_night"
            ? "OMEN: THE LONG NIGHT GATHERS — BARROWS STIR ACROSS THE LAND"
            : "OMEN: THE GOBLIN HORDE GATHERS — WAR-HORNS SOUND IN THE WILDS",
        2 => S.Crisis.Kind == "long_night"
            ? $"CRISIS: THE LONG NIGHT — DESTROY THE LICH'S CITADEL ({S.Crisis.TownsFallen}/{CrisisTownLossLimit} FALLEN)"
            : $"CRISIS: THE GOBLIN HORDE — BREAK THE WARLORD'S GREAT HOLD ({S.Crisis.TownsFallen}/{CrisisTownLossLimit} FALLEN)",
        _ => "",
    };

    /// Daily (WI8): check crisis trigger, advance from warnings to outbreak, send crisis hosts, keep coalition truce.
    internal void CrisisDaily()
    {
        if (S.GameOver || S.Crisis.Stage == 3) return;

        if (S.Crisis.Stage == 0)
        {
            if (Day >= CrisisTriggerDay || AverageWildness >= 45f)
                TriggerCrisis();
            return;
        }

        if (S.Crisis.Stage == 1)
        {
            if (Day - S.Crisis.StartedDay >= CrisisWarnDays)
                BreakOutCrisis();
            return;
        }

        // Stage 2: active outbreak
        var seat = PartyById(S.Crisis.SeatId);
        if (seat == null || seat.Down)
        {
            // Fallback safety if seat disappeared without OnCrisisSeatFallen
            S.Crisis.Stage = 3;
            S.Crisis.EndedDay = Day;
            return;
        }

        // Keep realms truced against each other while the crisis rages (WI8).
        foreach (var w in S.Wars.Where(w => w.Active))
        {
            w.Active = false;
            w.TruceUntil = (float)S.Hours + 30 * 24;
        }

        // Spawn crisis warhosts marching on towns (up to 3 at once).
        int activeHosts = S.Parties.Count(p => !p.Down && p.Crisis && !p.Lair);
        if (activeHosts < 3 && S.Hours >= seat.NextRaid)
        {
            seat.NextRaid = (float)S.Hours + 6 * 24;
            SpawnCrisisHost(seat);
        }
    }

    /// Start Stage 1 (warnings & omens) of the world crisis. `kind` may be "long_night" or "goblin_horde" (null = auto).
    public void TriggerCrisis(string? kind = null)
    {
        if (S.Crisis.Stage != 0) return;
        bool longNight = kind != null ? kind == "long_night"
            : S.Graves.Count >= 2 || Lairs.Any(l => l.Breed == "undead") ? true : rng.Chance(0.5f);
        S.Crisis.Kind = longNight ? "long_night" : "goblin_horde";
        S.Crisis.Stage = 1;
        S.Crisis.StartedDay = Day;
        string msg = longNight
            ? "OMEN: Cold mists rise from every grave field. The Long Night is coming."
            : "OMEN: Smoke climbs from every hill hold. A Goblin Warlord is uniting the tribes.";
        LogAll(msg);
        Deed(msg, "wild");
    }

    /// Start Stage 2 (outbreak): raise the Crisis Seat, call coalition truces across all realms, and launch the first host.
    public Party BreakOutCrisis()
    {
        if (S.Crisis.Stage == 0) TriggerCrisis();
        bool longNight = S.Crisis.Kind == "long_night";
        var wilds = S.Provinces.Where(Wild).OrderByDescending(p => p.Wildness).ToList();
        var prov = wilds.Count > 0 ? wilds[0] : S.Provinces[0];
        var at = prov.Center;

        var seat = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Monster, Breed = longNight ? "undead" : "goblin",
            Lair = true, Crisis = true, Legendary = true, Stage = 2, Tier = 7,
            Name = longNight ? "The Lich's Citadel" : "The Warlord's Great Hold",
            Pos = at, Goal = at, Task = "hold", PatrolProvince = prov.Id,
            Gold = 2000, NextRaid = (float)S.Hours + 3 * 24,
        };
        if (longNight)
        {
            seat.Companies.Add(NewCompany("necromancer", "casters", 1, 0));
            seat.Companies.Add(NewCompany("barrow_king", "infantry", 2, 0));
            seat.Companies.Add(NewCompany("wraith", "infantry", Math.Max(4, Scaled(6)), 0));
            seat.Companies.Add(NewCompany("wight", "infantry", Math.Max(6, Scaled(10)), 0));
            seat.Companies.Add(NewCompany("skeleton", "infantry", Math.Max(20, Scaled(35)), 0));
        }
        else
        {
            seat.Companies.Add(NewCompany("goblin_warlord", "infantry", 1, 0));
            seat.Companies.Add(NewCompany("goblin_chief", "infantry", 2, 0));
            seat.Companies.Add(NewCompany("goblin_shaman", "casters", Math.Max(2, Scaled(3)), 0));
            seat.Companies.Add(NewCompany("hobgoblin", "infantry", Math.Max(14, Scaled(24)), 0));
            seat.Companies.Add(NewCompany("goblin_wolf_rider", "horse", Math.Max(8, Scaled(14)), 0));
        }
        S.Parties.Add(seat);
        S.Crisis.SeatId = seat.Id;
        S.Crisis.Stage = 2;

        // Coalition truce: all realms cease fighting one another to face the crisis (WI8).
        foreach (var w in S.Wars.Where(w => w.Active))
        {
            w.Active = false;
            w.TruceUntil = (float)S.Hours + 60 * 24;
        }
        string news = longNight
            ? $"THE LONG NIGHT BEGINS! {seat.Name} rises in {prov.Title}. The realms call a truce to stand together."
            : $"THE GOBLIN HORDE MARCHES! {seat.Name} unites the tribes in {prov.Title}. The realms call a truce.";
        LogAll(news);
        Deed(news, "wild");
        SpawnCrisisHost(seat);
        return seat;
    }

    Party SpawnCrisisHost(Party seat)
    {
        bool longNight = S.Crisis.Kind == "long_night";
        var target = S.Settlements.Where(s => s.Kind != SettlementKind.Village)
            .OrderBy(s => Vector2.Distance(s.Pos, seat.Pos)).FirstOrDefault();
        var host = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Monster, Breed = seat.Breed,
            Crisis = true, LairId = seat.Id, Tier = 6,
            Name = longNight ? "Host of the Long Night" : "Horde of the Warlord",
            Pos = seat.Pos, Goal = target?.Pos ?? seat.Pos, GoalSettlement = target?.Id ?? -1,
            Moving = true, Task = "crisis", PatrolProvince = seat.PatrolProvince,
        };
        if (longNight)
        {
            host.Companies.Add(NewCompany("barrow_king", "infantry", 1, 0));
            host.Companies.Add(NewCompany("wraith", "infantry", Math.Max(2, Scaled(3)), 0));
            host.Companies.Add(NewCompany("skeleton", "infantry", Math.Max(16, Scaled(26)), 0));
        }
        else
        {
            host.Companies.Add(NewCompany("goblin_chief", "infantry", 1, 0));
            host.Companies.Add(NewCompany("goblin_shaman", "casters", 1, 0));
            host.Companies.Add(NewCompany("hobgoblin", "infantry", Math.Max(12, Scaled(20)), 0));
            host.Companies.Add(NewCompany("goblin_wolf_rider", "horse", Math.Max(4, Scaled(8)), 0));
        }
        S.Parties.Add(host);
        return host;
    }

    /// Crisis host AI (WI8): marches relentlessly toward towns/castles and sacks weak ones, driving the crisis stakes.
    void CrisisHostThink(Party p)
    {
        p.TaskUntil = (float)S.Hours + 2;
        var near = Visible(p, 22).Where(q => Hostile(p, q) && !q.Lair).ToList();
        float me = Rules.Might(p);
        if (near.Where(q => Rules.Might(q) < me * 1.15f).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.ChaseId = prey.Id; p.Moving = true;
            return;
        }
        p.ChaseId = -1;
        var goal = p.GoalSettlement >= 0 ? SettlementById(p.GoalSettlement) : null;
        if (goal == null || goal.ThreatUntil > S.Hours)
        {
            goal = S.Settlements.Where(s => s.Kind != SettlementKind.Village && s.ThreatUntil <= S.Hours)
                .OrderBy(s => Vector2.Distance(s.Pos, p.Pos)).FirstOrDefault();
            p.GoalSettlement = goal?.Id ?? -1;
        }
        if (goal == null) { Wander(p, 20, "crisis"); return; }
        p.Goal = goal.Pos; p.Moving = true;
        if (Vector2.Distance(p.Pos, goal.Pos) < 3f * Map.Spread)
        {
            // Sacks the town/castle if its garrison is weaker than the crisis host.
            float gMight = goal.Garrison.Sum(g => Rules.Might(Troops.Get(g.Troop), g.Men));
            if (me > gMight * 1.1f)
            {
                goal.Prosperity = MathF.Max(0, goal.Prosperity - 35);
                goal.ThreatUntil = (float)S.Hours + 14 * 24;
                goal.Garrison.Clear();
                S.Crisis.TownsFallen++;
                LogAll($"{goal.Name} has been sacked by {p.Name}! ({S.Crisis.TownsFallen}/{CrisisTownLossLimit} seats fallen)");
                Deed($"{goal.Name} fell to {p.Name}.", "wild");
                p.GoalSettlement = -1;
                if (S.Crisis.TownsFallen >= CrisisTownLossLimit && !S.GameOver)
                {
                    S.GameOver = true;
                    S.EndReason = S.Crisis.Kind == "long_night"
                        ? "THE LONG NIGHT SWALLOWED THE REALMS. THE LIVING ARE BROKEN."
                        : "THE GOBLIN HORDE OVERRAN THE REALMS. THE CROWNS HAVE FALLEN.";
                }
            }
            else
            {
                // Repelled by the garrison — take losses and fall back
                foreach (var c in p.Companies) c.RemoveFit(Math.Max(1, c.Men / 3), true);
                p.GoalSettlement = -1;
            }
        }
    }

    /// Ending the world crisis when its seat falls (WI8).
    void OnCrisisSeatFallen(Party seat, bool playerWon)
    {
        S.Crisis.Stage = 3;
        S.Crisis.EndedDay = Day;
        // Scatter remaining crisis hosts and calm wildness across the world.
        foreach (var h in S.Parties.Where(p => p.Crisis && !p.Down && p.Id != seat.Id).ToList())
            h.Down = true;
        foreach (var pr in S.Provinces)
            pr.Wildness = MathF.Max(0, pr.Wildness - 25f);

        bool longNight = S.Crisis.Kind == "long_night";
        string title = longNight ? "Bane of the Long Night" : "Breaker of the Horde";
        if (playerWon)
        {
            Captain.Renown += 250;
            S.Gold += 2000;
            foreach (var l in S.Characters.Where(ch => ch.Kind == "lord" && !ch.Dead))
                l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) + 25, -100, 100);
            foreach (var o in S.Orders.Where(o => !o.Gone))
                RaiseStanding(o.Id, 25);
            var lead = Player.Companies.OrderByDescending(c => c.Men).FirstOrDefault();
            if (lead != null && !lead.BattleHonours.Contains(title)) lead.BattleHonours.Add(title);
            S.Won = true;
            Deed($"{title}: destroyed {seat.Name} and saved the realms.", "wild");
            LogAll($"VICTORY! You destroyed {seat.Name} and ended the crisis. The realms hail you as {title}!");
        }
        else
        {
            LogAll($"{seat.Name} has been destroyed. The crisis is over.");
        }
    }
}
