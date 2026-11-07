using Microsoft.Xna.Framework;

namespace Ironfield;

/// More quests (docs/SIDE_CONTENT.md S3), on the contract machinery (CampaignWar.cs): village elders' jobs, escorting a
/// caravan, carrying a ransom, rescuing a captive, a lord's lost sword, and quests of more than one step (Contract.Then).
public sealed partial class Campaign
{
    /// Hours a militia must drill, and days of food a hungry village needs brought.
    public const int MilitiaHours = 12, GrainDays = 3;

    /// What the village elders ask of any captain (no contract needed). The board refreshes when it runs dry.
    public List<Contract> ElderOffers(Settlement v)
    {
        if (v.Kind != SettlementKind.Village || Refuses(v)) return new List<Contract>();
        v.Offers.RemoveAll(o => o.Deadline < S.Hours || !OfferValid(o));
        if (v.Offers.Count == 0 && S.Hours >= v.OffersAt) { RefreshElders(v); v.OffersAt = (float)S.Hours + 4 * 24; }
        return v.Offers;
    }

    void RefreshElders(Settlement v)
    {
        float deadline = (float)S.Hours + 8 * 24, reach = 70 * Map.Spread;
        var jobs = new List<Contract>();
        Contract Job(string kind, int target, float reward, string text) => new() { Id = S.NextId++, Kind = kind, Target = target, Faction = -1, From = v.Id, Deadline = deadline, Reward = reward, Text = text };
        Party? Nearest(IEnumerable<Party> ps) => ps.Where(p => !p.Down && Vector2.Distance(p.Pos, v.Pos) < reach).OrderBy(p => Vector2.Distance(p.Pos, v.Pos)).FirstOrDefault();
        if (Nearest(WolfPacks) is Party pack) jobs.Add(Job("clear", pack.Id, 80 + Rules.PerMan(Rules.Strength(pack) / 3), $"Drive off the {pack.Name} that take {v.Name}'s sheep."));
        if (Nearest(S.Parties.Where(p => p.Breed == "goblin")) is Party gob)
            jobs.Add(Job("clear", gob.Id, 100 + Rules.PerMan(Rules.Strength(gob) / 3), gob.Lair ? $"Burn out {gob.Name} for {v.Name}." : $"Drive off the {gob.Name} for {v.Name}."));
        if (Nearest(S.Parties.Where(p => p.Kind == PartyKind.Bandit && p.CharId < 0 && p.HomeId < 0)) is Party band)
            jobs.Add(Job("rescue", band.Id, 120 + Rules.PerMan(Rules.Strength(band) / 3), $"The {band.Name} took the headman's daughter from {v.Name}. Bring her home."));
        if (S.Settlements.Where(t => t.Kind == SettlementKind.Town && !FactionsAtWar(t.Faction, v.Faction) && !Refuses(t)).OrderBy(t => Vector2.Distance(t.Pos, v.Pos)).FirstOrDefault() is Settlement market)
        {
            var bring = Job("bring", v.Id, 90, $"Bring {GrainDays} days of grain back to {v.Name}.");
            var fetch = Job("visit", market.Id, 0, $"Fetch grain for {v.Name} from the market at {market.Name}.");
            fetch.Then = bring;
            jobs.Add(fetch);
        }
        jobs.Add(Job("militia", v.Id, 70, $"Drill the militia of {v.Name} for {MilitiaHours} hours."));
        foreach (var j in jobs.OrderBy(_ => rng.Next(1000)).Take(3)) v.Offers.Add(j);
    }

    /// Town jobs added to the steward's board (S3): escort a caravan out of town, carry a ransom to a far town.
    void MoreTownOffers(Settlement s, Func<string, int, float, string, Contract> make)
    {
        if (S.Settlements.Count(t => t.Kind == SettlementKind.Town && t != s && !FactionsAtWar(t.Faction, s.Faction)) > 0 && rng.Chance(0.7f))
            s.Offers.Add(make("escort", -1, 0, $"Escort a caravan out of {s.Name} to its market."));
        var far = S.Settlements.Where(t => t.Kind == SettlementKind.Town && t != s && !FactionsAtWar(t.Faction, s.Faction) && Vector2.Distance(t.Pos, s.Pos) > 80 * Map.Spread).ToList();
        if (far.Count > 0 && rng.Chance(0.6f))
        {
            var t = rng.Pick(far);
            s.Offers.Add(make("deliver", t.Id, 100 + Vector2.Distance(t.Pos, s.Pos) * 0.8f, $"Carry a ransom to {t.Name} and bring the captive's thanks."));
        }
    }

    /// Accepting a job may set it up in the world: an escort gets its caravan. Returns why it cannot be taken, else null.
    string? SetUp(Settlement s, Contract c)
    {
        if (c.Kind != "escort") return null;
        if (s.Besieged || SpawnCaravan(s) is not Party car) return "NO CARAVAN IS LEAVING";
        c.Target = car.Id;
        c.Reward = 120 + Vector2.Distance(car.Goal, s.Pos) * 0.6f;
        c.Text = $"Escort the {car.Name} to {SettlementById(car.GoalSettlement).Name}.";
        return null;
    }

    /// Whether a job of the new kinds is done now (null for the older kinds, which CheckContracts handles).
    bool? QuestDone(Contract c) => c.Kind switch
    {
        "rescue" => S.LastBattleFoe == c.Target,
        "visit" or "deliver" => Vector2.Distance(Player.Pos, S.Settlements[c.Target].Pos) < 6 * Map.Spread,
        "visitsite" => c.Progress >= 1,
        "escort" => c.Progress >= 1,
        "militia" => (c.Progress += Vector2.Distance(Player.Pos, S.Settlements[c.Target].Pos) < 8 * Map.Spread ? 1 : 0) >= MilitiaHours,
        "bring" => Vector2.Distance(Player.Pos, S.Settlements[c.Target].Pos) < 6 * Map.Spread && Player.Food >= GrainDays + 1 && (Player.Food -= GrainDays) >= 0,
        _ => null,
    };

    /// Whether a job of the new kinds has failed before its deadline (its quarry escaped or its caravan was lost).
    bool QuestLost(Contract c) => c.Kind switch
    {
        "rescue" => PartyById(c.Target) is null or { Down: true } && S.LastBattleFoe != c.Target,
        "escort" => c.Progress < 1 && PartyById(c.Target) is null or { Down: true },
        _ => false,
    };

    /// A finished step: elders' thanks for a village job, and the next step of a chain goes on your list.
    void QuestStepDone(Contract c)
    {
        if (c.From >= 0 && c.From < S.Settlements.Count && S.Settlements[c.From] is { Kind: SettlementKind.Village } v && c.Then == null)
        {
            v.Prosperity = Math.Clamp(v.Prosperity + 3, 0, 100);
            if (c.Kind == "militia") { v.ThreatUntil = 0; Practise("Training", 1); }
        }
        if (c.Then is Contract next)
        {
            next.Deadline = (float)S.Hours + 6 * 24;
            next.Giver = c.Giver;
            S.Contracts.Add(next);
            Log($"Next: {next.Text}");
        }
    }

    /// A caravan you escort has arrived (called from Arrive): it counts only if you rode in with it.
    void EscortArrived(Party caravan)
    {
        if (Vector2.Distance(Player.Pos, caravan.Pos) > 25 * Map.Spread) return;
        foreach (var c in S.Contracts.Where(c => c.Kind == "escort" && c.Target == caravan.Id)) c.Progress = 1;
    }

    /// You reached a site a quest sent you to (called from ArriveAtSite).
    void SiteQuests(Site site)
    {
        foreach (var c in S.Contracts.Where(c => c.Kind == "visitsite" && c.Target == site.Id)) c.Progress = 1;
    }

    /// A lord's lost sword (S3): ask the hermit nearby, then search the ruin; or straight to the ruin when no hermit is near.
    internal Contract? RelicQuest(Character giver, Vector2 at)
    {
        var ruin = Sites.Where(x => x.Kind == "ruin" && Vector2.Distance(x.Pos, at) < 140 * Map.Spread).OrderBy(x => Vector2.Distance(x.Pos, at)).FirstOrDefault();
        if (ruin == null) return null;
        var search = new Contract { Id = S.NextId++, Faction = Captain.Contract, Kind = "visitsite", Target = ruin.Id, Reward = 150, Text = $"Find {giver.Name.Replace("Lord ", "")}'s father's sword in the {ruin.Name}." };
        var hermit = Sites.Where(x => x.Kind == "hermit" && Vector2.Distance(x.Pos, ruin.Pos) < 120 * Map.Spread).OrderBy(x => Vector2.Distance(x.Pos, ruin.Pos)).FirstOrDefault();
        if (hermit == null) return search;
        return new Contract { Kind = "visitsite", Target = hermit.Id, Reward = 30, Then = search, Text = $"Ask the {hermit.Name} where {giver.Name.Replace("Lord ", "")}'s father fell." };
    }
}
