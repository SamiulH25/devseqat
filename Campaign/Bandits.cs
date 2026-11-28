using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: outlaw archetypes, hideout camps, regional naming and tailored bandit AI.
/// Replaces uniform random bandit spam with clearable camps, biome-fitted archetypes
/// (highwaymen, poachers, deserters, reivers, corsairs, robber knights), and strict local density caps.
public sealed partial class Campaign
{
    static readonly string[] BrigandLine = { "brigands", "marauders", "reavers", "bandit_chieftains" };
    static readonly string[] PoacherLine = { "poachers", "forest_stalkers", "black_arrows", "master_poachers" };
    static readonly string[] ReiverLine = { "highwaymen", "freebooters", "black_riders", "outlaw_captains" };
    static readonly string[] CorsairLine = { "sea_rovers", "corsairs", "dread_corsairs", "pirate_captains" };

    /// Active stationary outlaw camps (hideouts) on the map.
    public IEnumerable<Party> BanditCamps => S.Parties.Where(p => p.Kind == PartyKind.Bandit && p.Lair && !p.Down);

    /// Target number of outlaw camps across the world (~1 per 6.5 provinces).
    public int BanditCampsWanted => Math.Max(2, (int)MathF.Round(S.Provinces.Count / 6.5f));

    /// Roaming (non-camp) bandit parties currently near the player.
    internal int RoamingBanditsNearPlayer => S.Parties.Count(p => p.Kind == PartyKind.Bandit && !p.Lair && !p.Down && Vector2.Distance(p.Pos, Player.Pos) < BanditReach);

    static string OutlawTroop(string[] line, int tier) => line[Math.Clamp(tier - 2, 0, line.Length - 1)];

    /// Choose an outlaw archetype fitting the province's terrain, wars and campaign day.
    string PickOutlawBreed(Province prov, int tier, bool forCamp)
    {
        if (!forCamp && prov.Faction >= 0 && (IsAtWar(prov.Faction) || S.Graves.Any(g => Map.ProvinceAt(g.Pos).Id == prov.Id)) && rng.Chance(0.32f))
            return "deserters";
        if (tier >= 3 && rng.Chance(RobberKnightChance)) return "robbers";
        return prov.Kind switch
        {
            "Coast" => rng.Chance(0.75f) ? "corsairs" : "highwaymen",
            "Forest" or "Taiga" or "Marsh" => rng.Chance(0.72f) ? "poachers" : "highwaymen",
            "Steppe" or "Highlands" or "Hills" or "Badlands" => rng.Chance(0.68f) ? "reivers" : "highwaymen",
            _ => rng.Chance(0.22f) ? "reivers" : rng.Chance(0.22f) ? "poachers" : "highwaymen",
        };
    }

    bool IsAtWar(int faction) => faction >= 0 && S.Wars.Any(w => w.Active && (w.A == faction || w.B == faction));

    /// Short place name for a position: nearest settlement if close, else the province's name.
    string OutlawPlaceName(Vector2 pos, Province prov)
    {
        var near = S.Settlements.OrderBy(s => Vector2.DistanceSquared(s.Pos, pos)).FirstOrDefault();
        if (near != null && Vector2.Distance(near.Pos, pos) < 22 * Map.Spread) return near.Name;
        return prov.Name;
    }

    /// Region + Archetype nameplate for roaming outlaws (e.g. "Nanteuil Poachers", "Brevon Deserters").
    string OutlawBandName(string breed, Vector2 pos, Province prov)
    {
        if (breed == "deserters" && prov.Faction >= 0 && prov.Faction < FactionNames.Length)
        {
            string rname = FactionNames[prov.Faction]
                .Replace("Kingdom of ", "").Replace("Duchy of ", "").Replace("Principality of ", "")
                .Replace("Republic of ", "").Replace("Grand ", "").Replace("The ", "");
            return $"{rname} Deserters";
        }
        string place = OutlawPlaceName(pos, prov);
        return breed switch
        {
            "poachers" => $"{place} Poachers",
            "reivers" => $"{place} Reivers",
            "corsairs" => $"{place} Sea Raiders",
            "robbers" => $"{place} Robber Knights",
            "deserters" => $"{place} Deserters",
            _ => $"{place} Highwaymen",
        };
    }

    /// Proper hideout name for a stationary outlaw camp (e.g. "Nanteuil Poacher Camp", "Presnay Corsair Cove").
    string OutlawCampName(string breed, Vector2 pos, Province prov)
    {
        string place = OutlawPlaceName(pos, prov);
        return breed switch
        {
            "poachers" => $"{place} Poacher Camp",
            "reivers" => $"{place} Reiver Camp",
            "corsairs" => $"{place} Corsair Cove",
            "robbers" => $"{place} Black Hold",
            _ => $"{place} Bandit Camp",
        };
    }

    /// Populate a bandit party or camp with companies matching its archetype and tier.
    void FillOutlawCompanies(Party p, string breed, int tier, int men, Province prov)
    {
        void AddBand(string troop, int count) => p.Companies.Add(NewCompany(troop, Rules.TroopRole(troop), count, 0));

        if (breed == "poachers")
        {
            int bows = Math.Max(Scaled(4), (int)(men * 0.65f));
            int screen = Math.Max(0, men - bows);
            AddBand(OutlawTroop(PoacherLine, tier), bows);
            if (screen >= Scaled(3)) AddBand(OutlawTroop(BrigandLine, Math.Max(2, tier - 1)), screen);
        }
        else if (breed == "reivers")
        {
            int riders = Math.Max(Scaled(4), (int)(men * 0.7f));
            AddBand(OutlawTroop(ReiverLine, tier), riders);
        }
        else if (breed == "robbers")
        {
            int knights = Math.Max(Scaled(4), (int)(men * 0.55f));
            AddBand(OutlawTroop(ReiverLine, Math.Max(4, tier)), knights);
            if (p.Lair) AddBand(OutlawTroop(PoacherLine, Math.Max(3, tier)), Math.Max(Scaled(3), men / 3));
        }
        else if (breed == "corsairs")
        {
            AddBand(OutlawTroop(CorsairLine, tier), men);
            if (p.Lair || rng.Chance(0.4f))
                AddBand(OutlawTroop(PoacherLine, Math.Max(2, tier - 1)), Math.Max(Scaled(3), men / 3));
        }
        else if (breed == "deserters")
        {
            int fac = prov.Faction >= 0 ? prov.Faction : rng.Next(S.Factions.Count);
            string foot = Armies.LineTroop("infantry", Math.Clamp(tier, 1, Troops.MaxTier), fac);
            for (int l = tier; Troops.Get(foot).Noble && l > 1;) foot = Armies.LineTroop("infantry", --l, fac);
            int main = Math.Max(Scaled(5), (int)(men * 0.65f));
            AddBand(foot, main);
            int rest = Math.Max(Scaled(3), men - main);
            string role2 = rng.Chance(0.55f) ? "archers" : "spears";
            string sup = Armies.LineTroop(role2, Math.Clamp(tier, 1, Troops.MaxTier), fac);
            for (int l = tier; Troops.Get(sup).Noble && l > 1;) sup = Armies.LineTroop(role2, --l, fac);
            AddBand(sup, rest);
        }
        else // highwaymen
        {
            int foot = Math.Max(Scaled(5), (int)(men * 0.7f));
            AddBand(OutlawTroop(BrigandLine, tier), foot);
            if (p.Lair || rng.Chance(0.55f))
                AddBand(OutlawTroop(PoacherLine, Math.Max(2, tier - 1)), Math.Max(Scaled(3), men / 3));
        }
        if (!p.Lair) CunningFolk(p);
    }

    /// Raise a stationary outlaw camp tucked away from settlements and other camps.
    internal bool SpawnBanditCamp()
    {
        if (S.Provinces.Count == 0) return false;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var prov = rng.Pick(S.Provinces);
            if (BanditCamps.Any(c => c.PatrolProvince == prov.Id)) continue;
            var at = prov.Center + new Vector2(rng.Range(-15, 15), rng.Range(-15, 15)) * Map.Spread;
            if (Map.ProvinceAt(at).Id != prov.Id) continue;
            if (S.Settlements.Any(s => Vector2.Distance(s.Pos, at) < 9 * Map.Spread)) continue;
            if (BanditCamps.Any(c => Vector2.Distance(c.Pos, at) < 26 * Map.Spread)) continue;
            int tier = Math.Clamp(2 + Day / (int)MathF.Max(1, B.BanditTierPerDays), 2, 5);
            string breed = PickOutlawBreed(prov, tier, forCamp: true);
            int men = Scaled(B.BanditBandMin + 4 + rng.Next(6) + Day / (int)MathF.Max(1, B.BanditGrowPerDays * 1.4f));
            var camp = new Party
            {
                Id = S.NextId++, Kind = PartyKind.Bandit, Breed = breed, Lair = true,
                Name = OutlawCampName(breed, at, prov),
                Pos = at, Goal = at, Task = "hold", PatrolProvince = prov.Id, Tier = tier,
                Gold = B.HoldoutStartHoard + rng.Next(40),
                NextRaid = (float)S.Hours + (2.5f + rng.Range(0f, 3.5f)) * 24,
            };
            FillOutlawCompanies(camp, breed, tier, men, prov);
            S.Parties.Add(camp);
            return true;
        }
        return false;
    }

    /// Dispatch a roaming outlaw band from an active camp.
    Party? SpawnFromBanditCamp(Party camp)
    {
        var prov = camp.PatrolProvince >= 0 && camp.PatrolProvince < S.Provinces.Count
            ? S.Provinces[camp.PatrolProvince] : Map.ProvinceAt(camp.Pos);
        int tier = Math.Clamp(camp.Tier, 1, 5);
        int men = Scaled(B.BanditBandMin + rng.Next(Math.Max(1, B.BanditBandMax - B.BanditBandMin)) + Day / (int)MathF.Max(1, B.BanditGrowPerDays));
        var pos = camp.Pos + new Vector2(rng.Range(-4, 4), rng.Range(-4, 4)) * Map.Spread;
        var band = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Bandit, Breed = camp.Breed, LairId = camp.Id,
            Name = OutlawBandName(camp.Breed, pos, prov),
            Pos = pos, Goal = pos, PatrolProvince = prov.Id, Tier = tier,
        };
        FillOutlawCompanies(band, camp.Breed, tier, men, prov);
        S.Parties.Add(band);
        camp.NextRaid = (float)S.Hours + (3.5f + rng.Range(0f, 2.5f)) * 24;
        return band;
    }

    /// Daily outlaw ecosystem: grow camps, dispatch roaming bands (respecting global & local caps),
    /// replace cleared camps slowly, and raise war-zone deserters.
    void BanditsDaily()
    {
        int totalBandits = S.Parties.Count(p => p.Kind == PartyKind.Bandit && !p.Down);
        // 1. Existing camps grow slowly, hoard loot, and send out at most 2 active roaming bands each.
        foreach (var camp in BanditCamps.ToList())
        {
            camp.Gold += B.HoldoutHoardPerDay * 0.75f;
            int cap = Scaled(B.BanditBandMax + 14 + Day / (int)MathF.Max(1, B.BanditGrowPerDays));
            if (camp.Men < cap && camp.Companies.Count > 0)
            {
                var c0 = camp.Companies[0];
                int room = Rules.Room(c0, c0.Troop);
                if (room > 0) c0.Stacks[0].Men += Math.Min(room, Math.Max(1, Scaled(1)));
            }
            if (S.Hours >= camp.NextRaid && totalBandits < BanditsWanted)
            {
                int active = S.Parties.Count(p => p.Kind == PartyKind.Bandit && !p.Lair && !p.Down && p.LairId == camp.Id);
                bool nearPlayer = Vector2.Distance(camp.Pos, Player.Pos) < BanditReach;
                if (active < 2 && (!nearPlayer || BanditsNearPlayer < B.BanditNearMax))
                {
                    SpawnFromBanditCamp(camp);
                    totalBandits++;
                }
                else camp.NextRaid = (float)S.Hours + 48;
            }
        }
        // 2. Slowly establish new camps in unguarded provinces if under target.
        for (int n = Math.Max(1, BanditCampsWanted / 6); n > 0; n--)
            if (BanditCamps.Count() < BanditCampsWanted && rng.Chance(0.28f)) SpawnBanditCamp();

        // 3. War-zone deserters & stray roaming bands (respecting both world cap and local near-player cap).
        for (int n = 0; n < BanditSpawnsWanted; n++)
        {
            if (S.Parties.Count(p => p.Kind == PartyKind.Bandit && !p.Down) >= BanditsWanted || !rng.Chance(B.BanditSpawnChance)) break;
            bool near = rng.Chance(B.BanditNearPlayer) && BanditsNearPlayer < B.BanditNearMax;
            SpawnBandits(near);
        }
        // 4. Keep at least BanditNearMin bands somewhere in the player's broad region so early play has targets.
        for (int n = BanditsNearPlayer; n < B.BanditNearMin; n++) SpawnBandits(near: true);
    }

    /// Spawn a single roaming outlaw band of an archetype suited to its location.
    void SpawnBandits(bool near = false)
    {
        var at = BanditVillage(near);
        var pos = at.Pos + new Vector2(rng.Range(-14, 14), rng.Range(-14, 14)) * Map.Spread;
        var prov = Map.ProvinceAt(pos);
        int tier = Math.Clamp(1 + Day / (int)MathF.Max(1, B.BanditTierPerDays), 1, 5);
        int men = Scaled(B.BanditBandMin + rng.Next(Math.Max(1, B.BanditBandMax - B.BanditBandMin)) + Day / (int)MathF.Max(1, B.BanditGrowPerDays));
        string breed = PickOutlawBreed(prov, tier, forCamp: false);
        var band = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Bandit, Breed = breed,
            Name = OutlawBandName(breed, pos, prov),
            Pos = pos, Goal = pos, PatrolProvince = prov.Id, Tier = tier,
        };
        FillOutlawCompanies(band, breed, tier, men, prov);
        S.Parties.Add(band);
    }

    /// Outlaw camp cleared: log it and reward the victor.
    void BanditCampCleared(Party camp, Party? taker)
    {
        if (taker == Player)
        {
            Captain.Renown += 12;
            Log($"You clear {camp.Name}, making the roads safer for a time.");
        }
        else if (taker != null)
            LogAll($"{taker.Name} clears {camp.Name}.");
    }

    /// Archetype-driven outlaw AI: camps hold fast; highwaymen stalk caravans; poachers keep to woods;
    /// reivers and robber knights range wide and strike boldly; deserters hold their nerve better.
    void BanditThink(Party p)
    {
        if (p.Lair) { p.Moving = false; p.ChaseId = -1; p.Task = "hold"; p.TaskUntil = (float)S.Hours + 24; return; }
        float me = Rules.Strength(p);
        float sight = p.Breed is "reivers" or "robbers" ? B.Vision * 1.05f : B.Vision * 0.85f;
        var near = Visible(p, sight).Where(q => Hostile(p, q)).ToList();

        float fleeRatio = p.Breed switch { "robbers" => 1.25f, "deserters" or "corsairs" => 1.15f, _ => 1.05f };
        float chaseRatio = p.Breed switch { "robbers" => 1.05f, "deserters" or "corsairs" => 0.96f, _ => 0.90f };

        if (near.FirstOrDefault(q => !q.Lair && Rules.Strength(q) * Dread(q) > me * fleeRatio) is Party threat)
        {
            var away = p.Pos - threat.Pos;
            p.Goal = p.Pos + (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 20 * Map.Spread;
            p.ChaseId = -1; p.Moving = true; p.Task = "flee"; p.TaskUntil = (float)S.Hours + 2;
            return;
        }

        // Highwaymen and reivers prioritize merchant caravans when spotted.
        if (p.Breed is "highwaymen" or "reivers" or "robbers")
        {
            var caravan = near.Where(q => q.Kind == PartyKind.Caravan && Rules.Strength(q) * Dread(q) < me * chaseRatio)
                .OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault();
            if (caravan != null)
            {
                p.ChaseId = caravan.Id; p.Moving = true; p.Task = "raid"; p.TaskUntil = (float)S.Hours + 4;
                return;
            }
        }

        if (near.Where(q => !q.Lair && q.Breed is not ("troll" or "undead" or "wyvern" or "bear") && Rules.Strength(q) * Dread(q) < me * chaseRatio)
            .OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.ChaseId = prey.Id; p.Moving = true; p.Task = "hunt"; p.TaskUntil = (float)S.Hours + 4;
            return;
        }

        p.ChaseId = -1;
        p.Task = "roam";
        // Stay roughly tethered to home camp or patrol province so outlaws don't all drift into one clump.
        Vector2 anchor = p.Pos;
        if (p.LairId >= 0 && PartyById(p.LairId) is Party camp && !camp.Down) anchor = camp.Pos;
        else if (p.PatrolProvince >= 0 && p.PatrolProvince < S.Provinces.Count) anchor = S.Provinces[p.PatrolProvince].Center;

        float roamR = p.Breed is "reivers" or "robbers" ? 28 : p.Breed == "poachers" ? 16 : 20;
        var toAnchor = anchor - p.Pos;
        if (toAnchor.Length() > roamR * Map.Spread)
            p.Goal = anchor + new Vector2(rng.Range(-10, 10), rng.Range(-10, 10)) * Map.Spread;
        else
            p.Goal = p.Pos + new Vector2(rng.Range(-18, 18), rng.Range(-18, 18)) * Map.Spread;
        p.Moving = true; p.TaskUntil = (float)S.Hours + 2.5f;
    }

    /// Short uppercase role label for tooltips and encounter panels.
    public string OutlawAllegiance(Party p)
    {
        if (p.CharId >= 0 && CharacterById(p.CharId) is Character ch)
            return ch.Kind == "pretender" ? "REBEL PRETENDER" : "BRIGAND KING";
        if (p.HomeId >= 0) return "REBELS";
        if (p.Lair)
        {
            return p.Breed switch
            {
                "poachers" => "POACHER CAMP — ARCHER HIDEOUT",
                "reivers" => "REIVER CAMP — MOUNTED HIDEOUT",
                "corsairs" => "CORSAIR COVE — SEA RAIDER HIDEOUT",
                "robbers" => "ROBBER KNIGHT HOLD",
                _ => "BANDIT CAMP — OUTLAW HIDEOUT",
            };
        }
        return p.Breed switch
        {
            "poachers" => "FOREST POACHERS — ARCHERS",
            "deserters" => p.PatrolProvince >= 0 && p.PatrolProvince < S.Provinces.Count && S.Provinces[p.PatrolProvince].Faction >= 0
                ? $"DESERTERS OF THE {FactionNames[S.Provinces[p.PatrolProvince].Faction].ToUpperInvariant()}"
                : "WAR DESERTERS",
            "reivers" => "BORDER REIVERS — MOUNTED RAIDERS",
            "corsairs" => "SEA RAIDERS — COASTAL REAVERS",
            "robbers" => "ROBBER KNIGHTS — ELITE OUTLAWS",
            _ => "HIGHWAYMEN — ROAD BANDITS",
        };
    }
}
