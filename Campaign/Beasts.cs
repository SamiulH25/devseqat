using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: the beasts of docs/archive/MONSTERS.md, beside the goblins, wolves and trolls of Monsters.cs. Who leads a
/// monster host into battle (never a man); spider nests in the woods and fens that send broods out by night; cave bears
/// that keep their hills by day and fall on whoever comes near; wyverns, rare and terrible, that hunt from the sky by
/// day and fall on villages' herds; and trolls and wyverns that ravage villages when no prey is about.
public partial class Campaign
{
    /// Who leads a monster host into battle in place of a captain: a goblin chieftain over a holdout (a hobgoblin over a
    /// raiding band), the alpha over a wolf pack, an elder troll, a brood mother over a nest, the biggest bear, a wyvern,
    /// a wight over the dead. Empty for anything that is not a monster party.
    public static string MonsterLeader(Party p) => p.Kind != PartyKind.Monster ? "" : p.Breed switch
    {
        "goblin" => p.Lair ? "goblin_chief" : "hobgoblin",
        "wolf" => "alpha_wolf",
        "troll" => "troll_elder",
        "spider" => p.Lair ? "brood_mother" : "spider",
        "bear" => "bear",
        "wyvern" => "wyvern",
        "undead" => "wight",
        _ => "",
    };

    /// The leader's troop for a battle against this party, or null for men (a captain leads them).
    static UnitType? LeaderOf(Party p) => MonsterLeader(p) is { Length: > 0 } id ? Troops.Get(id) : null;

    /// What a lair is called on the map ("GOBLIN HOLDOUT", "SPIDER NEST").
    public static string LairWord(string breed) => breed == "spider" ? "NEST" : "HOLDOUT";

    // ---------- spider nests ----------

    static readonly string[] NestNames = { "Nest", "Webs", "Silk Hollow", "Brood", "Cocoons", "Spinnings" };
    /// Spider nests the world keeps: a share of its forest and fen provinces.
    public int NestsWanted => Math.Max(1, (int)MathF.Round(S.Provinces.Count(p => p.Kind is "Forest" or "Marsh") * B.NestsPerWood));
    public IEnumerable<Party> Nests => S.Parties.Where(p => p.Lair && !p.Down && p.Breed == "spider");
    /// Every lair: goblin holdouts and spider nests.
    public IEnumerable<Party> Lairs => S.Parties.Where(p => p.Lair && !p.Down);

    /// Spin a nest deep in a wood or fen, away from settlements, other lairs and you.
    internal bool SpawnNest()
    {
        var woods = S.Provinces.Where(p => p.Kind is "Forest" or "Marsh").ToList();
        if (woods.Count == 0) return false;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var prov = rng.Pick(woods);
            var at = prov.Center + new Vector2(rng.Range(-16, 16), rng.Range(-16, 16)) * Map.Spread;
            if (Map.ProvinceAt(at).Id != prov.Id) continue;
            if (S.Settlements.Any(s => Vector2.Distance(s.Pos, at) < B.HoldoutClearance * Map.Spread)) continue;
            if (Vector2.Distance(Player.Pos, at) < 30 * Map.Spread || Lairs.Any(h => Vector2.Distance(h.Pos, at) < 25 * Map.Spread)) continue;
            var nest = new Party
            {
                Id = S.NextId++, Kind = PartyKind.Monster, Breed = "spider", Lair = true, Name = $"{prov.Name} {rng.Pick(NestNames)}", Tier = 3,
                Pos = at, Goal = at, Task = "hold", Gold = B.HoldoutStartHoard, NextRaid = (float)S.Hours + B.HoldoutRaidDays * 24 * rng.Range(0.5f, 1.5f),
            };
            nest.Companies.Add(NewCompany("spider", "infantry", Scaled(B.NestStartMen), 0));
            S.Parties.Add(nest);
            return true;
        }
        return false;
    }

    /// A nest hatches spiders up to its cap.
    void GrowNest(Party nest)
    {
        int cap = Scaled(B.NestMaxMen);
        if (nest.Men < cap) Recruit(nest, "spider", "infantry", Math.Min(cap - nest.Men, Math.Max(1, Scaled(B.NestGrowPerDay))));
    }

    // ---------- cave bears ----------

    static readonly string[] BearNames = { "Old Grizzle", "Black Maul", "Scarback", "Stonepaw", "Redclaw", "Ironhide", "Mossjaw", "Greymuzzle", "Bonebreaker" };
    /// Cave bears the world keeps: a share of its hill and forest provinces.
    public int BearsWanted => (int)MathF.Round(S.Provinces.Count(p => p.Kind is "Hills" or "Forest") * B.BearsPerWild);
    public IEnumerable<Party> Bears => S.Parties.Where(p => p.Breed == "bear" && !p.Down);

    /// Wake a sleuth of cave bears in hill or forest country, away from settlements and from you.
    internal bool SpawnBears()
    {
        var wild = S.Provinces.Where(p => p.Kind is "Hills" or "Forest").ToList();
        if (wild.Count == 0) return false;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var prov = rng.Pick(wild);
            var at = prov.Center + new Vector2(rng.Range(-16, 16), rng.Range(-16, 16)) * Map.Spread;
            if (Map.ProvinceAt(at).Id != prov.Id || S.Settlements.Any(s => Vector2.Distance(s.Pos, at) < 8 * Map.Spread)) continue;
            if (Vector2.Distance(Player.Pos, at) < 30 * Map.Spread) continue;
            var bears = new Party
            {
                Id = S.NextId++, Kind = PartyKind.Monster, Breed = "bear", Name = $"{rng.Pick(BearNames)} of {prov.Name}", Pos = at, Goal = at, Task = "roam", PatrolProvince = prov.Id, Tier = 4,
            };
            bears.Companies.Add(NewCompany("bear", "infantry", Scaled(B.BearMen + Day / 90f), 0));
            S.Parties.Add(bears);
            return true;
        }
        return false;
    }

    /// Bears sleep by night. By day they keep to their country and fall on anyone who comes close and is not clearly the
    /// stronger, and shamble off from a stronger band; they do not hunt far.
    void BearThink(Party p)
    {
        float me = Rules.Might(p);
        p.TaskUntil = (float)S.Hours + 2;
        if (Night) { p.Task = "rest"; p.ChaseId = -1; p.Moving = false; p.TaskUntil = (float)S.Hours + 3; return; }
        var near = Visible(p, 10).Where(q => Hostile(p, q) && !q.Lair).ToList();
        if (near.FirstOrDefault(q => Rules.Might(q) > me * 1.8f && Vector2.Distance(q.Pos, p.Pos) < 8 * Map.Spread) is Party threat) { Flee(p, threat, 10); return; }
        if (near.Where(q => Rules.Might(q) < me * 1.3f && Vector2.Distance(q.Pos, p.Pos) < 8 * Map.Spread).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party trespasser)
        {
            p.Task = "hunt"; p.ChaseId = trespasser.Id; p.Moving = true;
            return;
        }
        Wander(p, 14, "roam");
    }

    // ---------- wyverns ----------

    static readonly string[] WyvernNames = { "Ashwing", "the Red Drake", "Gallowscreech", "Old Scald", "Thornspine", "the Grey Widow", "Stormtail", "Carrionqueen" };
    /// Wyverns the world keeps: WyvernStart at the start, one more every WyvernEvery days, at most WyvernMax.
    public int WyvernsWanted => Math.Min((int)B.WyvernMax, (int)B.WyvernStart + (int)(Day / B.WyvernEvery));
    public IEnumerable<Party> Wyverns => S.Parties.Where(p => p.Breed == "wyvern" && !p.Down);

    /// A wyvern comes to roost in high country (hills or badlands, else any wild land), far from settlements, you and
    /// the other wyverns.
    internal bool SpawnWyvern()
    {
        var high = S.Provinces.Where(p => p.Kind is "Hills" or "Badlands").ToList();
        if (high.Count == 0) high = S.Provinces.Where(Wild).ToList();
        if (high.Count == 0) return false;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var prov = rng.Pick(high);
            var at = prov.Center + new Vector2(rng.Range(-14, 14), rng.Range(-14, 14)) * Map.Spread;
            if (Map.ProvinceAt(at).Id != prov.Id || S.Settlements.Any(s => Vector2.Distance(s.Pos, at) < 10 * Map.Spread)) continue;
            if (Vector2.Distance(Player.Pos, at) < 45 * Map.Spread || Wyverns.Any(r => Vector2.Distance(r.Pos, at) < 50 * Map.Spread)) continue;
            var w = new Party
            {
                Id = S.NextId++, Kind = PartyKind.Monster, Breed = "wyvern", Name = $"{rng.Pick(WyvernNames)} of {prov.Name}", Pos = at, Goal = at,
                Task = "rest", PatrolProvince = prov.Id, Tier = 6,
            };
            w.Companies.Add(NewCompany("wyvern", "infantry", Scaled(B.WyvernMen), 0));
            S.Parties.Add(w);
            return true;
        }
        return false;
    }

    /// A wyvern roosts by night. By day it sees far from the sky and stoops on anything it can beat, lords' hosts too;
    /// with nothing in sight it may fall on a village's herds; else it soars over its country.
    void WyvernThink(Party p)
    {
        float me = Rules.Might(p);
        p.TaskUntil = (float)S.Hours + 2;
        if (Night) { p.Task = "rest"; p.ChaseId = -1; p.RaidOn = -1; p.Moving = false; p.TaskUntil = (float)S.Hours + 3; return; }
        var near = Visible(p, 30).Where(q => Hostile(p, q) && !q.Lair).ToList();
        if (near.FirstOrDefault(q => Rules.Might(q) > me * 3f && Vector2.Distance(q.Pos, p.Pos) < 10) is Party threat) { Flee(p, threat, 16); return; }
        if (near.Where(q => Rules.Might(q) < me * 1.1f).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.Task = "hunt"; p.ChaseId = prey.Id; p.RaidOn = -1; p.Moving = true;
            return;
        }
        if (Ravage(p, 30)) return;
        Wander(p, 20, "soar");
    }

    // ---------- shared ----------

    /// Off and away from a threat, `far` map units.
    void Flee(Party p, Party threat, float far)
    {
        var away = p.Pos - threat.Pos;
        p.Goal = p.Pos + (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * far * Map.Spread;
        p.Task = "flee"; p.ChaseId = -1; p.RaidOn = -1; p.Moving = true;
    }

    /// Off to a spot in its home province.
    void Wander(Party p, float spread, string task)
    {
        p.Task = task; p.ChaseId = -1;
        var home = S.Provinces[Math.Clamp(p.PatrolProvince, 0, S.Provinces.Count - 1)];
        p.Goal = home.Center + new Vector2(rng.Range(-spread, spread), rng.Range(-spread, spread)) * Map.Spread;
        p.Moving = true; p.TaskUntil = (float)S.Hours + 4;
    }

    /// A big beast with no prey about goes for a village within `range`: on its way (RaidOn) it keeps going; once there it
    /// plunders it. Now and then it picks a new one (MonsterRavageChance an hour). True while it is about it.
    bool Ravage(Party p, float range)
    {
        if (p.RaidOn < 0 && rng.Chance(B.MonsterRavageChance)
            && S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.ThreatUntil <= S.Hours && Vector2.Distance(v.Pos, p.Pos) < range * Map.Spread)
                .OrderBy(v => Vector2.Distance(v.Pos, p.Pos)).FirstOrDefault() is Settlement pick)
            p.RaidOn = pick.Id;
        if (p.RaidOn < 0) return false;
        var v = SettlementById(p.RaidOn);
        p.ChaseId = -1;
        if (Vector2.Distance(p.Pos, v.Pos) < 2.5f * Map.Spread) { Plunder(p, v); p.RaidOn = -1; return false; }
        p.Task = "ravage"; p.Goal = v.Pos; p.Moving = true;
        return true;
    }

    /// What the land hears when a village is plundered by these beasts.
    static string PlunderNews(string breed, string village) => breed switch
    {
        "spider" => $"Spiders carry off the folk of {village} in the night.",
        "troll" => $"A troll ravages {village}.",
        "wyvern" => $"A wyvern falls on the herds of {village}.",
        "bear" => $"Bears maul the herders of {village}.",
        _ => $"Goblins plunder {village}.",
    };
}
