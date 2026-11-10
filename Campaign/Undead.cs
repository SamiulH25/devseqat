using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: the dead rise (docs/archive/CREATURES.md phase 5). A big battle leaves a grave field (CampaignState.Graves) unless
/// its dead are buried: a realm often buries the dead in its own lands, and you are asked after a big fight of yours
/// (the forced event "bury_dead"). Some nights later an unburied field can rise as an undead host led by a necromancer.
/// The host lies in the earth by day and walks by night, hunting the living; each fight it wins swells its ranks.
public partial class Campaign
{
    public IEnumerable<Party> UndeadHosts => S.Parties.Where(p => p.Breed == "undead" && !p.Down);

    /// After a battle: an undead host that won raises some of the fallen into its ranks; otherwise a big battle leaves a
    /// grave field, unless the realm whose land it is buries its dead. After a big fight of yours you are asked.
    void AfterTheBattle(BattleResult r, Dictionary<int, (Party P, CompanyRecord C, int Team)> lookup, IReadOnlyList<Party> winners, IReadOnlyList<Party> losers, bool playerIn)
    {
        int fallen = r.Companies.Where(cr => lookup.TryGetValue(cr.Id, out var e) && !Troops.Get(e.C.Troop).Wild).Sum(cr => cr.Fallen); // bodies of men
        if (winners.FirstOrDefault(w => w.Breed == "undead") is Party host)
        {
            int room = Scaled(B.UndeadMaxMen) - host.Men, more = Math.Min(room, (int)MathF.Round(fallen * B.UndeadGrowShare));
            if (more > 0) Recruit(host, "skeleton", "infantry", more);
            return;
        }
        if (losers.Any(l => l.Breed == "undead") || fallen < Scaled(B.GraveMin)) return;
        var at = (losers.Count > 0 ? losers[0] : winners[0]).Pos;
        var prov = Map.ProvinceAt(at);
        if (!playerIn && prov.Faction >= 0 && rng.Chance(B.RealmBuryChance)) return; // the realm buries its dead
        var grave = new Grave { Id = S.NextId++, Pos = at, Dead = fallen, Hours = S.Hours };
        S.Graves.Add(grave);
        if (playerIn && S.Settlements.OrderBy(s => Vector2.Distance(s.Pos, at)).FirstOrDefault() is Settlement near)
            Force("bury_dead", new PendingEvent { Grave = grave.Id, Settlement = near.Id });
    }

    /// The burial event's choice: the field is laid to rest and will not rise.
    void Bury(int graveId) => S.Graves.RemoveAll(g => g.Id == graveId);

    /// Daily: an old enough grave field may rise in the night (while the world holds fewer than UndeadMax hosts);
    /// a field that stays quiet for GraveDays rests for good.
    void GravesDaily()
    {
        foreach (var g in S.Graves.ToList())
        {
            double days = (S.Hours - g.Hours) / 24;
            if (days > B.GraveDays) { S.Graves.Remove(g); continue; }
            if (days < B.GraveRiseDays * (S.Omen >= OmenStirs ? 0.5f : 1) || UndeadHosts.Count() >= B.UndeadMax || !rng.Chance(B.GraveRiseChance)) continue;
            S.Graves.Remove(g);
            RaiseHost(g);
        }
    }

    /// A grave field rises: skeletons from a share of its dead, a few ghouls, and the necromancer who woke them.
    internal Party RaiseHost(Grave g)
    {
        var prov = Map.ProvinceAt(g.Pos);
        int bones = Math.Clamp((int)MathF.Round(g.Dead * B.UndeadShare), Scaled(B.UndeadMinMen), Scaled(B.UndeadMaxMen));
        if (S.Omen >= OmenStirs) bones += bones / 3; // a high Omen raises another rank (MA8)
        var host = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Monster, Breed = "undead", Name = $"The Dead of {prov.Name}", Pos = g.Pos, Goal = g.Pos,
            Task = "rest", PatrolProvince = prov.Id, Tier = 4,
        };
        host.Companies.Add(NewCompany("skeleton", "infantry", bones, 0));
        host.Companies.Add(NewCompany("ghoul", "infantry", Math.Max(1, bones / 6), 0));
        host.Companies.Add(NewCompany("necromancer", "casters", 1, 0));
        S.Parties.Add(host);
        Log($"The dead of the battle in {prov.Name} rise from their graves.");
        return host;
    }

    /// The undead lie in the earth by day. By night they hunt the nearest living host they can match, never run,
    /// and otherwise walk the land where they rose.
    void UndeadThink(Party p)
    {
        float me = Rules.Strength(p);
        p.TaskUntil = (float)S.Hours + 2;
        if (!Night) { p.Task = "rest"; p.ChaseId = -1; p.Moving = false; p.TaskUntil = (float)S.Hours + 3; return; }
        if (Visible(p, 20).Where(q => Hostile(p, q) && !q.Lair && Rules.Strength(q) < me * B.UndeadPreyAt)
                .OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.Task = "hunt"; p.ChaseId = prey.Id; p.Moving = true;
            return;
        }
        p.Task = "walk"; p.ChaseId = -1;
        var home = S.Provinces[Math.Clamp(p.PatrolProvince, 0, S.Provinces.Count - 1)];
        p.Goal = home.Center + new Vector2(rng.Range(-16, 16), rng.Range(-16, 16)) * Map.Spread;
        p.Moving = true; p.TaskUntil = (float)S.Hours + 4;
    }
}
