using Microsoft.Xna.Framework;

namespace Ironfield;

/// Life on the roads (docs/SIDE_CONTENT.md S4): merchant caravans between towns and pilgrims bound for shrines. They are
/// PartyKind.Caravan parties ("caravan" or "pilgrims" in Breed) with a few guards. A caravan that arrives makes both towns
/// richer; one that is robbed makes them poorer. Brigands, monsters and lords at war with its realm hunt it; you may buy
/// from it, ask it for news, or rob it.
public sealed partial class Campaign
{
    /// About one caravan for every two towns, and a few pilgrim bands.
    public int CaravansWanted => Math.Max(2, S.Settlements.Count(t => t.Kind == SettlementKind.Town) / 2);
    public int PilgrimsWanted => Math.Max(1, (int)MathF.Round(3 * ProvinceScale / 2.3f));
    public IEnumerable<Party> Caravans => S.Parties.Where(p => p.Kind == PartyKind.Caravan && !p.Down && p.Breed == "caravan");
    public IEnumerable<Party> Pilgrims => S.Parties.Where(p => p.Kind == PartyKind.Caravan && !p.Down && p.Breed == "pilgrims");
    /// A caravan moves its town's prosperity a little: some thirty arrive a week, against a natural drift of about 0.1 a day.
    public const float CaravanProsperity = 0.25f, PilgrimAlms = 20, CaravanFoodPrice = 40;

    /// Daily: new caravans set out to keep the roads busy, and now and then a band of pilgrims.
    void RoadsDaily()
    {
        for (int i = 0; i < CaravansWanted && Caravans.Count() < CaravansWanted; i++) SpawnCaravan();
        if (Pilgrims.Count() < PilgrimsWanted && rng.Chance(0.4f)) SpawnPilgrims();
    }

    /// A caravan from a town to another town of a realm at peace with it, far enough to be worth the road.
    internal Party? SpawnCaravan(Settlement? fromTown = null)
    {
        var towns = S.Settlements.Where(t => t.Kind == SettlementKind.Town && !t.Besieged && t.Faction >= 0).ToList();
        if (towns.Count < 2) return null;
        var from = fromTown ?? rng.Pick(towns);
        var to = towns.Where(t => t != from && !FactionsAtWar(t.Faction, from.Faction))
            .Where(t => Vector2.Distance(t.Pos, from.Pos) is var d && d > 50 * Map.Spread && d < 260 * Map.Spread).ToList();
        if (to.Count == 0) return null;
        var dest = rng.Pick(to);
        var p = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Caravan, Breed = "caravan", Faction = from.Faction, HomeId = from.Id, GoalSettlement = dest.Id,
            Name = $"Merchants of {from.Name}", Pos = from.Pos + new Vector2(rng.Range(-3, 3), rng.Range(-3, 3)), Goal = dest.Pos, Moving = true,
            Task = "trade", Gold = 150 + dest.Prosperity * 2, Tier = 2,
        };
        p.Companies.Add(NewCompany(Armies.LineTroop("infantry", 2, from.Faction), "infantry", Scaled(5 + rng.Next(5)), 0, from));
        p.Companies.Add(NewCompany(Armies.LineTroop("horse", 2, from.Faction), "horse", Scaled(2 + rng.Next(2)), 0, from));
        S.Parties.Add(p);
        return p;
    }

    /// Pilgrims from a town on foot to a shrine: a handful of folk with staves.
    internal Party? SpawnPilgrims()
    {
        var shrines = Sites.Where(s => s.Kind == "shrine").ToList();
        var towns = S.Settlements.Where(t => t.Kind == SettlementKind.Town && !t.Besieged).ToList();
        if (shrines.Count == 0 || towns.Count == 0) return null;
        var shrine = rng.Pick(shrines);
        var from = towns.OrderBy(t => Vector2.Distance(t.Pos, shrine.Pos)).Skip(rng.Next(3)).First();
        var p = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Caravan, Breed = "pilgrims", Faction = from.Faction, HomeId = from.Id,
            Name = $"Pilgrims to {shrine.Name}", Pos = from.Pos + new Vector2(rng.Range(-3, 3), rng.Range(-3, 3)), Goal = shrine.Pos, Moving = true,
            Task = "pilgrimage", Gold = 30, Tier = 1,
        };
        p.Companies.Add(NewCompany(RecruitTroopFor(from), "infantry", Scaled(3 + rng.Next(3)), 0, from));
        S.Parties.Add(p);
        return p;
    }

    /// Hourly: keep to the road; run for the nearest friendly walls from anything hostile and stronger; arrive.
    void CaravanThink(Party p)
    {
        float me = Rules.Strength(p);
        if (Visible(p, 18 * Map.Spread).FirstOrDefault(q => Hostile(p, q) && Rules.Strength(q) > me * 0.8f && !q.Lair) is Party threat
            && S.Settlements.Where(s => s.Kind != SettlementKind.Village && !FactionsAtWar(s.Faction, p.Faction) && Vector2.Distance(s.Pos, threat.Pos) > Vector2.Distance(s.Pos, p.Pos))
                .OrderBy(s => Vector2.Distance(s.Pos, p.Pos)).FirstOrDefault() is Settlement refuge)
        {
            p.Goal = refuge.Pos; p.Moving = true; p.TaskUntil = (float)S.Hours + 3;
            if (p.Breed == "caravan") p.GoalSettlement = refuge.Id;
            return;
        }
        if (p.Breed == "caravan" && SettlementById(p.GoalSettlement) is Settlement dest)
        {
            if (FactionsAtWar(dest.Faction, p.Faction) && SettlementById(p.HomeId) is Settlement home && home != dest) { p.GoalSettlement = home.Id; p.Goal = home.Pos; }
            else p.Goal = dest.Pos;
            p.Moving = true;
        }
        if (Vector2.Distance(p.Pos, p.Goal) < 1.5f) Arrive(p);
    }

    /// The end of the road: a caravan sells (both towns prosper); pilgrims pray and go home (they leave the map either way).
    void Arrive(Party p)
    {
        if (p.Breed == "caravan" && SettlementById(p.GoalSettlement) is Settlement dest)
        {
            dest.Prosperity = Math.Clamp(dest.Prosperity + CaravanProsperity, 0, 100);
            if (SettlementById(p.HomeId) is Settlement home && home != dest) home.Prosperity = Math.Clamp(home.Prosperity + CaravanProsperity / 2, 0, 100);
            CaravansArrived++;
            EscortArrived(p);
        }
        p.Down = true;
        S.Parties.Remove(p);
    }

    /// A caravan beaten on the road: its home town and the one it was bound for both feel it.
    void CaravanLost(Party p)
    {
        if (p.Breed != "caravan") return;
        CaravansLost++;
        if (SettlementById(p.HomeId) is Settlement home) home.Prosperity = Math.Clamp(home.Prosperity - CaravanProsperity, 0, 100);
        if (SettlementById(p.GoalSettlement) is Settlement dest) dest.Prosperity = Math.Clamp(dest.Prosperity - CaravanProsperity / 2, 0, 100);
        if (p.Faction == Captain.Contract && Captain.Contract >= 0) Log($"{p.Name} are robbed near {Map.ProvinceAt(p.Pos).Name}.");
    }

    /// Counters for the self-test and the balance report.
    public int CaravansArrived, CaravansLost;

    // ---------- meeting travellers on the road (you ride up to one that is not your enemy) ----------

    /// True when the encounter is a peaceful meeting with travellers, not a fight.
    public bool Meeting(Party e) => e.Kind == PartyKind.Caravan && !Hostile(Player, e);

    /// Buy provisions from a caravan: four days of food.
    public string? BuyFromCaravan()
    {
        if (Encounter is not { Breed: "caravan" }) return "NOT A CARAVAN";
        if (S.Gold < CaravanFoodPrice) return $"NEEDS {CaravanFoodPrice:0} CROWNS";
        S.Gold -= CaravanFoodPrice;
        Player.Food += 4;
        return null;
    }

    /// Alms for pilgrims: a little renown, and their prayers.
    public string? GiveAlms()
    {
        if (Encounter is not { Breed: "pilgrims" }) return "NOT PILGRIMS";
        if (S.Gold < PilgrimAlms) return $"NEEDS {PilgrimAlms:0} CROWNS";
        S.Gold -= PilgrimAlms;
        Captain.Renown += 1;
        Practise("Charm", 0.5f);
        Part();
        return null;
    }

    /// What travellers have seen on the road (the parties near them), into the log.
    public void AskNews()
    {
        if (Encounter is not Party e) return;
        Log($"{e.Name}: {Spy(e.Pos).Replace("From the top you see", "on the road they saw")}");
    }

    /// Rob them: they become your enemy for this fight, and their realm's lords think less of you (much less if it is your
    /// own realm; robbing pilgrims costs renown too).
    public void Rob()
    {
        CompanionsReact("rob");
        if (Encounter is not Party e || e.Kind != PartyKind.Caravan) return;
        BreakVow("no_rob", "rob travellers on the road");
        e.Prey = true;
        float hit = e.Faction == Captain.Contract ? -14 : -6;
        foreach (var l in S.Characters.Where(x => x.Kind == "lord" && !x.Dead && x.Faction == e.Faction))
            l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) + hit, -100, 100);
        if (e.Breed == "pilgrims") Captain.Renown = MathF.Max(0, Captain.Renown - 5);
        Deed($"You fall on the {e.Name}. The {FactionNames[Math.Max(0, e.Faction)]} will hear of it.", "robbed");
    }

    /// Ride on: the travellers go their way.
    public void Part()
    {
        if (Encounter is not Party) return;
        Player.ChaseId = -1;
        Encounter = null;
    }
}
