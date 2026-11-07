using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: creatures of the wild (docs/CREATURES.md). Goblin holdouts sit in forest and hill country, grow, hoard
/// and send raiders at villages; a holdout is a monster party that never moves (Party.Lair), so fighting it is a field
/// battle and auto-resolve like any other. Wolf packs roam the forests and hunt by night. Trolls are the rare monsters:
/// a few in the world, each sleeping out the day and hunting anything it can beat by night. MonstersDaily runs from
/// Daily, MonsterThink from Hourly.
public partial class Campaign
{
    static readonly string[] LairNames = { "Warren", "Burrows", "Holdout", "Hole", "Den", "Diggings", "Pits" };

    /// Goblin holdouts the world keeps: a share of its forest and hill provinces.
    public int HoldoutsWanted => Math.Max(1, (int)MathF.Round(S.Provinces.Count(Wild) * B.HoldoutsPerWildProvince));
    public IEnumerable<Party> Holdouts => S.Parties.Where(p => p.Lair && !p.Down);
    static bool Wild(Province p) => p.Kind is "Forest" or "Hills";

    /// Wolf packs the world keeps: a share of its forest provinces.
    public int WolfPacksWanted => (int)MathF.Round(S.Provinces.Count(p => p.Kind == "Forest") * B.WolfPacksPerForest);
    public IEnumerable<Party> WolfPacks => S.Parties.Where(p => p.Breed == "wolf" && !p.Down);
    /// What a breed is called, many of them ("goblins", "wolves").
    public static string Plural(string breed) => breed switch { "wolf" => "wolves", "undead" => "undead", _ => breed + "s" };
    /// Night on the map (wolves hunt from dusk to dawn).
    bool Night => S.Hours % 24 is >= 20 or < 6;
    static readonly string[] PackNames = { "Grey Pack", "Black Pack", "Hollow Howlers", "Frost Fangs", "Moon Pack", "Ash Pack", "Red Maws",
        "Night Runners", "Bone Gnawers", "Pale Pack", "Hill Howlers", "Ember Eyes", "Thorn Pack", "Winter Teeth" };

    /// Raise a wolf pack in a forest, away from settlements and from you: wolves and the dire wolf that leads them.
    internal bool SpawnWolfPack()
    {
        var forests = S.Provinces.Where(p => p.Kind == "Forest").ToList();
        if (forests.Count == 0) return false;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var prov = rng.Pick(forests);
            var at = prov.Center + new Vector2(rng.Range(-16, 16), rng.Range(-16, 16)) * Map.Spread;
            if (Map.ProvinceAt(at).Id != prov.Id || S.Settlements.Any(s => Vector2.Distance(s.Pos, at) < 8 * Map.Spread)) continue;
            if (Vector2.Distance(Player.Pos, at) < 30 * Map.Spread) continue;
            var pack = new Party
            {
                Id = S.NextId++, Kind = PartyKind.Monster, Breed = "wolf", Name = rng.Pick(PackNames), Pos = at, Goal = at, Task = "roam", PatrolProvince = prov.Id, Tier = 2,
            };
            pack.Companies.Add(NewCompany("wolf", "infantry", Scaled(B.WolfPackMin + rng.Next(Math.Max(1, (int)(B.WolfPackMax - B.WolfPackMin))) + Day / 30f), 0));
            pack.Companies.Add(NewCompany("dire_wolf", "infantry", 1, 0));
            S.Parties.Add(pack);
            return true;
        }
        return false;
    }

    /// Wolves keep to their forest. By day they lie up; by night they hunt anything up to a little stronger than the pack.
    void WolfThink(Party p)
    {
        float me = Rules.Strength(p);
        p.TaskUntil = (float)S.Hours + 2;
        var near = Visible(p, 22).Where(q => Hostile(p, q) && !q.Lair).ToList();
        if (near.FirstOrDefault(q => Rules.Strength(q) > me * 1.8f && Vector2.Distance(q.Pos, p.Pos) < 12) is Party threat)
        {
            var away = p.Pos - threat.Pos;
            p.Goal = p.Pos + (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 12 * Map.Spread;
            p.ChaseId = -1; p.Moving = true;
            return;
        }
        if (Night && near.Where(q => Rules.Strength(q) < me * 1.3f).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.Task = "hunt"; p.ChaseId = prey.Id; p.Moving = true;
            return;
        }
        p.ChaseId = -1;
        p.Task = Night ? "prowl" : "rest";
        if (!Night) { p.Moving = false; p.TaskUntil = (float)S.Hours + 3; return; }
        var home = S.Provinces[Math.Clamp(p.PatrolProvince, 0, S.Provinces.Count - 1)];
        p.Goal = home.Center + new Vector2(rng.Range(-18, 18), rng.Range(-18, 18)) * Map.Spread;
        p.Moving = true; p.TaskUntil = (float)S.Hours + 4;
    }

    /// Rare monsters the world keeps (trolls, for now): RareStart at the start, one more every RareEvery days, at most RareMax.
    public int RaresWanted => Math.Min((int)B.RareMax, (int)B.RareStart + (int)(Day / B.RareEvery));
    public IEnumerable<Party> Rares => S.Parties.Where(p => p.Breed == "troll" && !p.Down);
    static readonly string[] TrollNames = { "Mossback", "Old Stoneteeth", "Bonecrack", "Gutmaw", "Hollowjaw", "Barrowfist", "Grimhide", "Rootgnaw" };

    /// Wake a troll in the wilds, far from settlements, from you and from the other rare monsters.
    internal bool SpawnTroll()
    {
        var wild = S.Provinces.Where(Wild).ToList();
        if (wild.Count == 0) return false;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var prov = rng.Pick(wild);
            var at = prov.Center + new Vector2(rng.Range(-14, 14), rng.Range(-14, 14)) * Map.Spread;
            if (Map.ProvinceAt(at).Id != prov.Id || S.Settlements.Any(s => Vector2.Distance(s.Pos, at) < 10 * Map.Spread)) continue;
            if (Vector2.Distance(Player.Pos, at) < 40 * Map.Spread || Rares.Any(r => Vector2.Distance(r.Pos, at) < 40 * Map.Spread)) continue;
            var troll = new Party
            {
                Id = S.NextId++, Kind = PartyKind.Monster, Breed = "troll", Name = $"{rng.Pick(TrollNames)} of {prov.Name}", Pos = at, Goal = at,
                Task = "rest", PatrolProvince = prov.Id, Tier = 5,
            };
            troll.Companies.Add(NewCompany("troll", "infantry", Scaled(B.TrollMen), 0));
            S.Parties.Add(troll);
            return true;
        }
        return false;
    }

    /// A troll sleeps out the day where it lies. By night it hunts anything it can surely beat, lords' hosts too, and keeps
    /// clear only of a host far stronger than itself.
    void TrollThink(Party p)
    {
        float me = Rules.Strength(p);
        p.TaskUntil = (float)S.Hours + 2;
        if (!Night) { p.Task = "rest"; p.ChaseId = -1; p.Moving = false; p.TaskUntil = (float)S.Hours + 3; return; }
        var near = Visible(p, 20).Where(q => Hostile(p, q) && !q.Lair).ToList();
        if (near.FirstOrDefault(q => Rules.Strength(q) > me * 2.5f && Vector2.Distance(q.Pos, p.Pos) < 10) is Party threat)
        {
            var away = p.Pos - threat.Pos;
            p.Goal = p.Pos + (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 10 * Map.Spread;
            p.Task = "prowl"; p.ChaseId = -1; p.Moving = true;
            return;
        }
        if (near.Where(q => Rules.Strength(q) < me * 0.9f).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.Task = "hunt"; p.ChaseId = prey.Id; p.Moving = true;
            return;
        }
        p.Task = "prowl"; p.ChaseId = -1;
        var home = S.Provinces[Math.Clamp(p.PatrolProvince, 0, S.Provinces.Count - 1)];
        p.Goal = home.Center + new Vector2(rng.Range(-16, 16), rng.Range(-16, 16)) * Map.Spread;
        p.Moving = true; p.TaskUntil = (float)S.Hours + 4;
    }

    /// Raise a small holdout somewhere in the wilds, away from every settlement and from you. False if no ground was found.
    internal bool SpawnHoldout()
    {
        var wild = S.Provinces.Where(Wild).ToList();
        if (wild.Count == 0) return false;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var prov = rng.Pick(wild);
            var at = prov.Center + new Vector2(rng.Range(-18, 18), rng.Range(-18, 18)) * Map.Spread;
            if (Map.ProvinceAt(at).Id != prov.Id) continue;
            if (S.Settlements.Any(s => Vector2.Distance(s.Pos, at) < B.HoldoutClearance * Map.Spread)) continue;
            if (Vector2.Distance(Player.Pos, at) < 30 * Map.Spread || Holdouts.Any(h => Vector2.Distance(h.Pos, at) < 25 * Map.Spread)) continue;
            var lair = new Party
            {
                Id = S.NextId++, Kind = PartyKind.Monster, Breed = "goblin", Lair = true, Name = $"{prov.Name} {rng.Pick(LairNames)}",
                Pos = at, Goal = at, Task = "hold", Gold = B.HoldoutStartHoard, NextRaid = (float)S.Hours + B.HoldoutRaidDays * 24 * rng.Range(0.5f, 1.5f),
            };
            lair.Companies.Add(NewCompany("goblin", "infantry", Scaled(B.HoldoutStartMen), 0));
            S.Parties.Add(lair);
            return true;
        }
        return false;
    }

    internal void MonstersDaily()
    {
        foreach (var lair in Holdouts.ToList())
        {
            Grow(lair);
            lair.Gold += B.HoldoutHoardPerDay;
            if (S.Hours >= lair.NextRaid && lair.Men >= Scaled(B.HoldoutRaidMen)) SendRaiders(lair);
        }
        if (WolfPacks.Count() < WolfPacksWanted && rng.Chance(B.WolfSpawnChance)) SpawnWolfPack();
        if (Rares.Count() < RaresWanted && rng.Chance(B.RareSpawnChance)) SpawnTroll();
        GravesDaily();
        // A few tries a day on a big map, so cleared holdouts are replaced at about the same pace everywhere.
        for (int n = Math.Max(1, HoldoutsWanted / 6); n > 0; n--)
            if (Holdouts.Count() < HoldoutsWanted && rng.Chance(B.HoldoutSpawnChance)) SpawnHoldout();
    }

    /// A holdout breeds goblins up to its cap; a big one gains hobgoblins, archers and, once, a boss.
    void Grow(Party lair)
    {
        int cap = Scaled(B.HoldoutMaxMen);
        if (lair.Men >= cap) return;
        Recruit(lair, "goblin", "infantry", Math.Min(cap - lair.Men, Math.Max(1, Scaled(B.HoldoutGrowPerDay))));
        if (lair.Men >= cap) return;
        if (lair.Men >= cap / 3 && rng.Chance(0.4f)) Recruit(lair, "goblin_archer", "archers", Scaled(1));
        if (lair.Men >= cap / 2 && rng.Chance(0.3f)) Recruit(lair, "hobgoblin", "infantry", Scaled(1));
        if (lair.Men >= cap * 2 / 3 && rng.Chance(0.25f)) Recruit(lair, "goblin_wolf_rider", "horse", Scaled(1)); // a big holdout tames wargs
        if (lair.Men >= Scaled(B.HoldoutBossMen) && !lair.Companies.Any(c => c.Stacks.Any(st => st.Troop == "goblin_boss" && st.Men > 0)))
        {
            Recruit(lair, "goblin_boss", "infantry", 1);
            lair.Tier = 4;
        }
    }

    void Recruit(Party p, string troop, string role, int men)
    {
        if (men <= 0) return;
        var c = p.Companies.FirstOrDefault(c => c.Troop == troop);
        if (c == null) p.Companies.Add(NewCompany(troop, role, men, 0));
        else c.Stack(troop).Men += men;
    }

    /// Send a band out of the holdout at the nearest village not already plundered. The band's men leave the holdout.
    internal void SendRaiders(Party lair)
    {
        lair.NextRaid = (float)S.Hours + B.HoldoutRaidDays * 24 * rng.Range(0.7f, 1.3f);
        var village = S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.ThreatUntil <= S.Hours && Vector2.Distance(v.Pos, lair.Pos) < B.HoldoutRaidRange * Map.Spread)
            .OrderBy(v => Vector2.Distance(v.Pos, lair.Pos)).FirstOrDefault();
        if (village == null) return;
        var band = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Monster, Breed = lair.Breed, Name = $"Raiders of {lair.Name}", LairId = lair.Id, RaidOn = village.Id,
            Pos = lair.Pos, Goal = village.Pos, Moving = true, Task = "raid",
        };
        int want = Math.Min(Scaled(B.HoldoutRaidBand), lair.Men - Scaled(B.HoldoutStartMen));
        foreach (var c in lair.Companies.Where(c => c.Troop is "goblin" or "goblin_archer").ToList())
        {
            int n = Math.Min(c.Men, c.Troop == "goblin" ? want * 3 / 4 : want / 4);
            if (n <= 0) continue;
            c.Stack(c.Troop).Men -= n;
            Recruit(band, c.Troop, c.Role, n);
        }
        lair.Companies.RemoveAll(c => c.Men + c.Wounded <= 0);
        if (band.Men == 0) return;
        S.Parties.Add(band);
    }

    /// Goblins are dumb: they take on anything not clearly stronger, run only from a much bigger host, plunder the
    /// village they were sent at and carry the loot home. A band with no holdout left roams and plunders as it goes.
    internal void MonsterThink(Party p)
    {
        if (p.Lair) { p.Moving = false; p.ChaseId = -1; p.TaskUntil = (float)S.Hours + 24; return; }
        if (p.Breed == "wolf") { WolfThink(p); return; }
        if (p.Breed == "troll") { TrollThink(p); return; }
        if (p.Breed == "undead") { UndeadThink(p); return; }
        float me = Rules.Strength(p);
        var near = Visible(p, 25).Where(q => Hostile(p, q) && !q.Lair).ToList();
        p.TaskUntil = (float)S.Hours + 2;
        if (near.FirstOrDefault(q => Rules.Strength(q) > me * 1.6f) is Party threat)
        {
            var away = p.Pos - threat.Pos;
            p.Goal = p.Pos + (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 15 * Map.Spread;
            p.ChaseId = -1; p.Moving = true;
            return;
        }
        if (near.Where(q => Rules.Strength(q) < me * 1.15f).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.ChaseId = prey.Id; p.Moving = true;
            return;
        }
        p.ChaseId = -1;
        var home = p.LairId >= 0 ? PartyById(p.LairId) : null;
        if (home is { Down: true } || home is { Lair: false }) home = null;
        if (p.Task == "raid" && p.RaidOn >= 0)
        {
            var v = SettlementById(p.RaidOn);
            if (Vector2.Distance(p.Pos, v.Pos) < 2.5f * Map.Spread) { Plunder(p, v); p.Task = "home"; p.RaidOn = -1; }
            else { p.Goal = v.Pos; p.Moving = true; return; }
        }
        if (p.Task == "home" && home != null)
        {
            if (Vector2.Distance(p.Pos, home.Pos) < 2 * Map.Spread) { Rejoin(p, home); return; }
            p.Goal = home.Pos; p.Moving = true;
            return;
        }
        // No holdout to go back to: wander the wilds, and plunder a village found on the way.
        p.Task = "roam";
        if (S.Settlements.FirstOrDefault(v => v.Kind == SettlementKind.Village && v.ThreatUntil <= S.Hours && Vector2.Distance(v.Pos, p.Pos) < 3 * Map.Spread) is Settlement by)
            Plunder(p, by);
        p.Goal = p.Pos + new Vector2(rng.Range(-20, 20), rng.Range(-20, 20)) * Map.Spread;
        p.Moving = true; p.TaskUntil = (float)S.Hours + 6;
    }

    /// Goblins plunder a village: it gives no recruits for a week, its prosperity falls and its seat grows restless.
    void Plunder(Party band, Settlement v)
    {
        v.ThreatUntil = (float)S.Hours + 7 * 24;
        v.Recruits = 0;
        Raided(v);
        band.Gold += B.HoldoutRaidLoot;
        // The realm answers: its lords ride out after the holdout that sent the band, for a while.
        if (PartyById(band.LairId) is { Lair: true } lair) { lair.HuntedBy = v.Faction; lair.HuntedUntil = (float)S.Hours + B.HoldoutHuntDays * 24; }
        if (v.Owner == Player.Id || v.Faction == Captain.Contract) Log($"Goblins plunder {v.Name}.");
    }

    /// A raiding band back home: its men and loot go back into the holdout.
    void Rejoin(Party band, Party lair)
    {
        foreach (var c in band.Companies) foreach (var st in c.Stacks) Recruit(lair, st.Troop, c.Role, st.Men + st.Wounded);
        lair.Gold += band.Gold;
        band.Companies.Clear();
        band.Down = true;
        S.Parties.Remove(band);
    }

    /// A holdout's host is beaten: the holdout is gone, its raiders roam leaderless, and the villages around it breathe again.
    void HoldoutCleared(Party lair, Party? taker)
    {
        foreach (var band in S.Parties.Where(b => b.LairId == lair.Id)) { band.LairId = -1; if (band.Task == "home") band.Task = "roam"; }
        foreach (var v in S.Settlements.Where(v => v.Kind == SettlementKind.Village && Vector2.Distance(v.Pos, lair.Pos) < B.HoldoutRaidRange * Map.Spread))
            SeatOf(v).Unrest = MathF.Max(0, SeatOf(v).Unrest - 3);
        if (taker == Player) Deed($"You burn out {lair.Name}.", "holdouts");
        else Log(taker != null ? $"{taker.Name} burns out {lair.Name}." : $"{lair.Name} is overrun.");
    }
}
