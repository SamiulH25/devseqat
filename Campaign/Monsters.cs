using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: creatures of the wild (docs/CREATURES.md). Goblin holdouts sit in forest and hill country, grow, hoard
/// and send raiders at villages; a holdout is a monster party that never moves (Party.Lair), so fighting it is a field
/// battle and auto-resolve like any other. MonstersDaily runs from Daily, MonsterThink from Hourly.
public partial class Campaign
{
    static readonly string[] LairNames = { "Warren", "Burrows", "Holdout", "Hole", "Den", "Diggings", "Pits" };

    /// Goblin holdouts the world keeps: a share of its forest and hill provinces.
    public int HoldoutsWanted => Math.Max(1, (int)MathF.Round(S.Provinces.Count(Wild) * B.HoldoutsPerWildProvince));
    public IEnumerable<Party> Holdouts => S.Parties.Where(p => p.Lair && !p.Down);
    static bool Wild(Province p) => p.Kind is "Forest" or "Hills";

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
        Log(taker == Player ? $"You burn out {lair.Name}." : taker != null ? $"{taker.Name} burns out {lair.Name}." : $"{lair.Name} is overrun.");
    }
}
