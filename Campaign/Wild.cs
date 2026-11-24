using Microsoft.Xna.Framework;

namespace Ironfield;

/// The Wild (docs/pre-demo/THE_WILD.md WI1-WI2): province wildness (0-100), war feeding the wild, seasonal monster shifts,
/// and lairs growing up through stages (holdout -> warren -> hold; nest -> brood -> webbed wood; bridge trolls; barrows).
public partial class Campaign
{
    public float WildnessOf(int provinceId) =>
        provinceId >= 0 && provinceId < S.Provinces.Count ? S.Provinces[provinceId].Wildness : 0;

    public float WildnessAt(Vector2 pos) => WildnessOf(Map.ProvinceAt(pos).Id);

    public static string WildnessWord(float w) => w >= 75 ? "OVERRUN" : w >= 50 ? "WILD" : w >= 25 ? "UNQUIET" : "TAMED";

    /// True when a stage-2 spider brood has webbed this province's roads (WI2: slows travel and stops caravans).
    public bool IsProvinceWebbed(int provinceId) =>
        S.Parties.Any(p => !p.Down && p.Lair && p.Breed == "spider" && p.Stage >= 2 && Map.ProvinceAt(p.Pos).Id == provinceId);

    /// True when a troll has claimed a river crossing in this province (WI2: closes the road to caravans until slain).
    public bool HasBridgeTroll(int provinceId) =>
        S.Parties.Any(p => !p.Down && p.Breed == "troll" && p.Stage >= 1 && Map.ProvinceAt(p.Pos).Id == provinceId);

    /// True when caravans refuse to route through or out of this province (high wildness, webs, or a bridge troll).
    public bool RoadClosedByWild(int provinceId) =>
        WildnessOf(provinceId) >= 80 || IsProvinceWebbed(provinceId) || HasBridgeTroll(provinceId);

    /// How much high wildness thins village recruiting (1.0 when tamed, down to 0.3 when overrun).
    public float RecruitWildFactor(Settlement s) =>
        1f - Math.Clamp((WildnessOf(s.Province) - 30f) / 70f, 0, 1) * 0.7f;

    /// What a lair is called on the map by its breed and stage (WI2).
    public static string LairStageWord(Party p) => p.Breed switch
    {
        "spider" => p.Stage >= 2 ? "WEBBED BROOD" : p.Stage == 1 ? "SPIDER BROOD" : "SPIDER NEST",
        "undead" => "BARROW",
        _ => p.Stage >= 2 ? "GOBLIN HOLD" : p.Stage == 1 ? "GOBLIN WARREN" : "GOBLIN HOLDOUT",
    };

    /// Daily (WI1): lairs, beasts and graves raise province wildness; war feeds it; patrols and garrisons push it back.
    /// High wildness drains villages, drives unrest, and spawns more monsters; seasons shift wolves and wyverns.
    internal void WildnessDaily()
    {
        var delta = new float[S.Provinces.Count];
        foreach (var p in S.Parties)
        {
            if (p.Down || p.Kind != PartyKind.Monster) continue;
            int id = Map.ProvinceAt(p.Pos).Id;
            if (id < 0 || id >= delta.Length) continue;
            delta[id] += p.Lair ? (p.Stage >= 2 ? 4.0f : p.Stage == 1 ? 2.5f : 1.5f)
                : p.Breed is "troll" or "wyvern" or "undead" ? 1.5f
                : 0.5f;
        }
        foreach (var g in S.Graves)
        {
            int id = Map.ProvinceAt(g.Pos).Id;
            if (id >= 0 && id < delta.Length) delta[id] += 1.0f;
        }
        // Garrisons and patrolling friendly lords push wildness down; war and exhaustion feed it.
        foreach (var pr in S.Provinces)
        {
            float gain = delta[pr.Id];
            if (gain > 0 && pr.Faction >= 0 && pr.Faction < S.Factions.Count)
            {
                var wars = S.Wars.Where(w => w.Active && (w.A == pr.Faction || w.B == pr.Faction)).ToList();
                if (wars.Count > 0) gain *= 1.35f;
                gain += wars.Sum(w => MathF.Min(1f, MathF.Max(0, (float)(S.Hours - w.Since)) / (90f * 24f))) * 0.5f;
            }
            int seats = S.Settlements.Count(s => s.Province == pr.Id && s.Kind != SettlementKind.Village && GarrisonMen(s) >= Scaled(20));
            int patrols = S.Parties.Count(p => !p.Down && p.Kind == PartyKind.Lord && p.Faction == pr.Faction && Map.ProvinceAt(p.Pos).Id == pr.Id);
            float calm = seats * 0.5f + patrols * 0.6f + (gain <= 0 ? 0.35f : 0);
            pr.Wildness = Math.Clamp(pr.Wildness + gain - calm, 0, 100);

            if (pr.Wildness >= 40)
            {
                float drain = 0.15f * (pr.Wildness - 30f) / 70f;
                foreach (var v in S.Settlements.Where(s => s.Province == pr.Id && s.Kind == SettlementKind.Village))
                {
                    v.Prosperity = MathF.Max(0, v.Prosperity - drain);
                    if (pr.Wildness >= 80 && v.Prosperity < 10) v.Recruits = 0; // village emptying under overrun wilds
                }
            }
            // Wildness feeds itself: very wild provinces draw fresh packs.
            if (pr.Wildness >= 60 && WolfPacks.Count() < WolfPacksWanted + 3 && rng.Chance((pr.Wildness - 50f) / 500f))
                SpawnWolfPack();
        }
        SeasonalWildDaily();
    }

    /// Seasons (WI1): wolves come down out of taiga and tundra in winter; wyverns shift roosts in summer.
    void SeasonalWildDaily()
    {
        if (SeasonIndex == 3) // Winter: northern wolves move into neighbouring provinces
        {
            foreach (var p in WolfPacks)
            {
                if (p.PatrolProvince < 0 || p.PatrolProvince >= S.Provinces.Count) continue;
                var home = S.Provinces[p.PatrolProvince];
                if (home.Kind is not ("Taiga" or "Tundra") || home.Neighbors.Count == 0 || !rng.Chance(0.15f)) continue;
                var warmer = home.Neighbors.Where(n => S.Provinces[n].Kind is not ("Taiga" or "Tundra")).ToList();
                if (warmer.Count > 0) p.PatrolProvince = rng.Pick(warmer);
            }
        }
        else if (SeasonIndex == 1) // Summer (dry season): wyverns move to new roosts
        {
            foreach (var w in Wyverns)
            {
                if (w.PatrolProvince < 0 || w.PatrolProvince >= S.Provinces.Count) continue;
                var home = S.Provinces[w.PatrolProvince];
                if (home.Neighbors.Count == 0 || !rng.Chance(0.08f)) continue;
                var next = home.Neighbors.Where(n => Wild(S.Provinces[n])).ToList();
                if (next.Count > 0) w.PatrolProvince = rng.Pick(next);
            }
        }
    }

    /// Clearing a lair or killing a monster cuts the province's wildness at once, wins village/lord gratitude,
    /// triggers cultural reactions, order standing, and aura breakthroughs (WI1, WI3).
    internal void OnMonsterCleared(Party p, bool playerWon = false)
    {
        if (p.Kind != PartyKind.Monster) return;
        int id = Map.ProvinceAt(p.Pos).Id;
        if (id < 0 || id >= S.Provinces.Count) return;
        var prov = S.Provinces[id];
        float cut = p.Lair ? (p.Stage >= 2 ? 40f : p.Stage == 1 ? 28f : 18f)
            : p.Breed is "troll" or "wyvern" or "undead" ? 12f
            : 6f;
        prov.Wildness = MathF.Max(0, prov.Wildness - cut);
        if (!playerWon) return;

        // People and lords remember who cleared their lands (WI3).
        bool major = p.Lair || p.Breed is "troll" or "wyvern" or "undead";
        if (major)
        {
            foreach (var v in S.Settlements.Where(s => s.Province == id && s.Kind == SettlementKind.Village))
                v.Prosperity = MathF.Min(100, v.Prosperity + (p.Stage >= 2 ? 6 : 4));
            if (prov.Faction >= 0)
            {
                float rep = p.Stage >= 2 || p.Breed is "troll" or "wyvern" ? 10 : 5;
                foreach (var l in S.Characters.Where(ch => ch.Kind == "lord" && !ch.Dead && ch.Faction == prov.Faction))
                    l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) + rep, -100, 100);
            }
        }
        // Peoples see monsters differently (WI3).
        string people = prov.Faction >= 0 && prov.Faction < S.Factions.Count ? Factions.PeopleOf(S.Factions[prov.Faction].Culture) : "";
        if (p.Breed == "wolf" && people == "steppe")
        {
            foreach (var l in S.Characters.Where(ch => ch.Kind == "lord" && !ch.Dead && ch.Faction == prov.Faction))
                l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) - 4, -100, 100);
            LogYours("The steppe clans hold wolves sacred; their khans frown on the hunt.");
        }
        else if (p.Breed == "bear" && people == "northern")
        {
            foreach (var l in S.Characters.Where(ch => ch.Kind == "lord" && !ch.Dead && ch.Faction == prov.Faction))
                l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) - 4, -100, 100);
            LogYours("The northern bear cults resent the slaughter.");
        }
        if (p.Breed == "undead")
            foreach (var o in S.Orders.Where(o => !o.Gone)) RaiseStanding(o.Id, p.Lair ? 12 : 6);
        if (p.Breed == "troll") Deed($"Troll-Bane: slew {p.Name} in {prov.Title}.", "wild");
        else if (p.Breed == "wyvern") Deed($"Wyrmslayer: brought down {p.Name} in {prov.Title}.", "wild");
        else if (p.Breed == "undead" && p.Lair) Deed($"Grave-Warden: laid {p.Name} to rest.", "wild");
        if (Captain.AuraWall && p.Breed is "troll" or "wyvern")
            BreakThrough($"Slaying {p.Name} broke the wall holding back your aura.");
    }

    /// Daily (WI2): lairs left alone grow up through stages; trolls can claim river crossings; barrows raise undead raiders.
    internal void LairsGrowDaily()
    {
        int gobCap = Scaled(B.HoldoutMaxMen), spCap = Scaled(B.NestMaxMen);
        foreach (var lair in Lairs.ToList())
        {
            var prov = Map.ProvinceAt(lair.Pos);
            if (lair.Breed == "goblin")
            {
                if (lair.Stage == 0 && lair.Men >= (int)(gobCap * 0.65f))
                {
                    lair.Stage = 1;
                    lair.Name = $"{prov.Name} Warren";
                    Deed($"A goblin warren grows in {prov.Title}.", "wild");
                }
                else if (lair.Stage == 1 && lair.Men >= gobCap && lair.Tier >= 4)
                {
                    lair.Stage = 2;
                    lair.Name = $"{prov.Name} Hold";
                    Deed($"A goblin hold rises in {prov.Title} — its warhost threatens the villages.", "wild");
                }
            }
            else if (lair.Breed == "spider")
            {
                if (lair.Stage == 0 && lair.Men >= (int)(spCap * 0.65f))
                {
                    lair.Stage = 1;
                    lair.Name = $"{prov.Name} Brood";
                    Deed($"A spider brood spreads through {prov.Title}.", "wild");
                }
                else if (lair.Stage == 1 && lair.Men >= spCap)
                {
                    lair.Stage = 2;
                    lair.Name = $"{prov.Name} Webs";
                    Deed($"Spider webs choke the roads of {prov.Title}.", "wild");
                }
            }
            else if (lair.Breed == "undead" && lair.Stage >= 2)
            {
                // A barrow keeps raising undead bands until stormed (WI2).
                if (lair.Men < Scaled(B.UndeadMaxMen)) Recruit(lair, "skeleton", "infantry", Scaled(1));
                if (S.Hours >= lair.NextRaid && UndeadHosts.Count() < B.UndeadMax + 2)
                {
                    lair.NextRaid = (float)S.Hours + 7 * 24;
                    var host = new Party
                    {
                        Id = S.NextId++, Kind = PartyKind.Monster, Breed = "undead", Name = $"Dead of {lair.Name}",
                        Pos = lair.Pos, Goal = lair.Pos, Task = "walk", PatrolProvince = prov.Id, Tier = 4, LairId = lair.Id,
                    };
                    host.Companies.Add(NewCompany("skeleton", "infantry", Scaled(B.UndeadMinMen), 0));
                    host.Companies.Add(NewCompany("ghoul", "infantry", Math.Max(1, Scaled(2)), 0));
                    S.Parties.Add(host);
                }
            }
        }
        // Trolls near rivers in wild provinces can claim a bridge or ford (WI2).
        foreach (var t in Rares.Where(p => p.Stage == 0))
        {
            var prov = Map.ProvinceAt(t.Pos);
            if (WildnessOf(prov.Id) >= 35 && Map.RiverDist(t.Pos) < 12 * Map.Spread && rng.Chance(0.15f))
            {
                t.Stage = 1;
                LogAll($"{t.Name} has claimed a crossing in {prov.Title}, closing the road.");
            }
        }
    }

    /// Turn an unburied grave field into a persistent Barrow lair in wild/omen-heavy provinces (WI2).
    internal Party RaiseBarrow(Grave g)
    {
        var prov = Map.ProvinceAt(g.Pos);
        int bones = Math.Clamp((int)MathF.Round(g.Dead * B.UndeadShare), Scaled(B.UndeadMinMen), Scaled(B.UndeadMaxMen));
        var barrow = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Monster, Breed = "undead", Lair = true, Stage = 2,
            Name = $"{prov.Name} Barrow", Pos = g.Pos, Goal = g.Pos, Task = "hold", PatrolProvince = prov.Id,
            Tier = 5, Gold = B.HoldoutStartHoard * 2, NextRaid = (float)S.Hours + 5 * 24,
        };
        barrow.Companies.Add(NewCompany("skeleton", "infantry", bones, 0));
        barrow.Companies.Add(NewCompany("wight", "infantry", Math.Max(1, bones / 8), 0));
        barrow.Companies.Add(NewCompany("wraith", "infantry", Math.Max(1, bones / 14), 0));
        barrow.Companies.Add(NewCompany("barrow_king", "infantry", 1, 0));
        S.Parties.Add(barrow);
        Deed($"The grave field in {prov.Title} has become a barrow.", "wild");
        return barrow;
    }

    /// Daily (WI5-WI6): populate regional wild monsters (boars, hill giants, harpies, griffons, basilisks, sand wurms,
    /// drowned dead, werewolves, wraiths) in their native countries.
    internal void SpawnWildRosterDaily()
    {
        void TrySpawn(string breed, string troop, string name, int tier, Func<Province, bool> land, int maxCount, int minMen, int maxMen, string? apex = null)
        {
            if (S.Parties.Count(p => !p.Down && p.Breed == breed) >= maxCount || !rng.Chance(0.12f)) return;
            var pool = S.Provinces.Where(land).ToList();
            if (pool.Count == 0) return;
            var prov = rng.Pick(pool);
            var at = prov.Center + new Vector2(rng.Range(-14, 14), rng.Range(-14, 14)) * Map.Spread;
            if (Map.ProvinceAt(at).Id != prov.Id || Vector2.Distance(Player.Pos, at) < 30 * Map.Spread) return;
            var p = new Party
            {
                Id = S.NextId++, Kind = PartyKind.Monster, Breed = breed, Name = $"{name} of {prov.Name}",
                Pos = at, Goal = at, Task = "roam", PatrolProvince = prov.Id, Tier = tier,
            };
            p.Companies.Add(NewCompany(troop, "infantry", Scaled(minMen + rng.Next(Math.Max(1, maxMen - minMen + 1))), 0));
            if (apex != null) p.Companies.Add(NewCompany(apex, "infantry", 1, 0));
            S.Parties.Add(p);
        }
        TrySpawn("boar", "boar", "Sounder", 2, pr => pr.Kind == "Forest", 3, 4, 8, "great_boar");
        TrySpawn("giant", "hill_giant", "Hill Giants", 5, pr => pr.Kind is "Hills" or "Highlands", 2, 1, 2);
        TrySpawn("harpy", "harpy", "Harpy Flock", 3, pr => pr.Kind == "Highlands", 2, 4, 7);
        TrySpawn("griffon", "griffon", "Crag-Griffon", 5, pr => pr.Kind == "Highlands", 1, 1, 1);
        TrySpawn("basilisk", "basilisk", "Basilisk", 4, pr => pr.Kind is "Desert" or "Oasis", 2, 1, 2);
        TrySpawn("wurm", "sand_wurm", "Sand Wurm", 6, pr => pr.Kind is "Desert" or "Badlands", 1, 1, 1);
        TrySpawn("werewolf", "werewolf", "Skinchangers", 4, pr => pr.Kind is "Taiga" or "Tundra" or "Forest", 2, 2, 4);
        if (S.Parties.Count(p => !p.Down && p.Breed == "undead" && p.Name.StartsWith("Drowned")) < 2 && rng.Chance(0.1f))
        {
            var coasts = S.Provinces.Where(pr => pr.Kind == "Coast").ToList();
            if (coasts.Count > 0)
            {
                var prov = rng.Pick(coasts);
                var at = prov.Center + new Vector2(rng.Range(-10, 10), rng.Range(-10, 10)) * Map.Spread;
                if (Map.ProvinceAt(at).Id == prov.Id && Vector2.Distance(Player.Pos, at) >= 30 * Map.Spread)
                {
                    var d = new Party { Id = S.NextId++, Kind = PartyKind.Monster, Breed = "undead", Name = $"Drowned Dead of {prov.Name}", Pos = at, Goal = at, Task = "walk", PatrolProvince = prov.Id, Tier = 3 };
                    d.Companies.Add(NewCompany("drowned_dead", "infantry", Scaled(8), 0));
                    S.Parties.Add(d);
                }
            }
        }
        if (S.Parties.Count(p => !p.Down && p.Breed == "undead" && p.Name.StartsWith("Wraiths")) < 2 && rng.Chance(0.1f))
        {
            var fens = S.Provinces.Where(pr => pr.Kind == "Marsh").ToList();
            if (fens.Count > 0)
            {
                var prov = rng.Pick(fens);
                var at = prov.Center + new Vector2(rng.Range(-10, 10), rng.Range(-10, 10)) * Map.Spread;
                if (Map.ProvinceAt(at).Id == prov.Id && Vector2.Distance(Player.Pos, at) >= 30 * Map.Spread)
                {
                    var w = new Party { Id = S.NextId++, Kind = PartyKind.Monster, Breed = "undead", Name = $"Wraiths of {prov.Name}", Pos = at, Goal = at, Task = "walk", PatrolProvince = prov.Id, Tier = 4 };
                    w.Companies.Add(NewCompany("wraith", "infantry", Scaled(4), 0));
                    S.Parties.Add(w);
                }
            }
        }
    }

    // ---------- WI4: monster contracts and the hunt ----------

    /// Plain-words stake for a monster contract (WI4).
    public string MonsterStake(Party p, Settlement near) => p.Breed switch
    {
        "troll" when p.Stage >= 1 => $"The road to {near.Name} is closed by {p.Name}. Slay it to reopen trade.",
        "spider" when p.Stage >= 2 => $"Spider webs choke the roads near {near.Name}. Burn out {p.Name}.",
        "undead" when p.Lair => $"The dead walk from {p.Name} every night. Lay the barrow to rest.",
        "goblin" when p.Stage >= 1 => $"{p.Name} threatens every village around {near.Name}. Break it.",
        "wyvern" or "griffon" or "harpy" => $"{p.Name} stoops on the herds near {near.Name}. Bring it down.",
        "giant" => $"{p.Name} hurls boulders onto the passes near {near.Name}. Rush it down.",
        "basilisk" or "wurm" => $"{p.Name} has closed the desert tracks near {near.Name}. Hunt it down.",
        "werewolf" => $"{p.Name} stalks the night roads near {near.Name}. Hunt it with fire or faith.",
        "boar" => $"{p.Name} tramples the fields around {near.Name}. Set spears and break the herd.",
        _ => $"{p.Name} plagues the country around {near.Name}. Clear it out.",
    };

    /// Tactical hint for a tracked quarry (WI4): knowing your quarry tells you how and when to fight it.
    public static string QuarryHint(Party p) => p.Breed switch
    {
        "troll" => "FIGHT BY DAYLIGHT — THE SUN STOPS ITS HEALING.",
        "wyvern" => "CATCH IT ON ITS ROOST AT NIGHT, OR BRING BOWS.",
        "harpy" => "COVER YOUR ARCHERS — HARPIES PICK OFF BOWS AND THE WOUNDED.",
        "griffon" => "KEEP TIGHT RANKS AND BRING BOWS — IT SEIZES ISOLATED MEN.",
        "boar" => "BRACE SPEARS OR STAKES — BOARS BOWL SHIELD LINES OVER.",
        "giant" => "SPREAD OUT AND RUSH IT — ITS BOULDERS SHATTER MASSES.",
        "basilisk" => "FLANK IT — ITS GAZE FREEZES MEN WHO FACE IT HEAD-ON.",
        "wurm" => "KEEP SPREAD OUT — IT BURSTS UP UNDER BUNCHED RANKS.",
        "werewolf" => "BRING HOLY BLESSING, AURA OR CRAFT MARKS TO STOP ITS HEALING.",
        "undead" => p.Companies.Any(c => c.Troop == "wraith") ? "WRAITHS SHRUG OFF STEEL — BRING MAGIC, AURA OR FAITH BLESSING."
            : "FIGHT IN OPEN DAYLIGHT AND KILL THE NECROMANCER OR BARROW KING.",
        _ => "KNOW THE GROUND AND STRIKE ON YOUR TERMS.",
    };

    public void TrackQuarry(int partyId) { if (partyId >= 0) S.TrackedQuarries.Add(partyId); }

    public bool KnowsQuarry(Party p) => p.Kind == PartyKind.Monster && (S.TrackedQuarries.Contains(p.Id) || S.Contracts.Any(c => c.Target == p.Id));

    /// Knowing your quarry gives a 10% edge in auto-resolve (and the `quarry_known` buff in fought battles, WI4).
    public float HuntEdge(Party enemy) => KnowsQuarry(enemy) ? 1.1f : 1f;
}
