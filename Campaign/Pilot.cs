using Microsoft.Xna.Framework;

namespace Ironfield;

/// A scripted stand-in captain for headless runs (roadmap F0): it plays the campaign the way a careful player would, by
/// the same public actions the map screen uses, so `--balance` can measure pacing and the self-test can check that a career
/// is reachable. It is not an AI opponent; it only drives your party.
public sealed class Pilot
{
    readonly Campaign c;
    /// Fights you won and lost, played by the pilot (auto-resolved).
    public int Battles, Won;
    /// Pacing telemetry (F9): men promoted, the day the first tier V man stood in the ranks, and seats taken by assault.
    public int Promotions, FirstT5Day = -1, Taken, RenownForVassalDay = -1;
    /// The day each career stage was first reached (stage name -> day).
    public readonly Dictionary<string, int> StageDay = new();
    float nextThink;

    /// The realm the company started in (docs/archive/WORLD.md W5): the pilot takes service there.
    readonly int StartRealm;

    public Pilot(Campaign campaign)
    {
        c = campaign; StageDay[c.Stage] = c.Day;
        StartRealm = Math.Max(0, c.Player.Faction);
        if (c.S.Hours < 1 && c.Captain.Kit.Horse == "courser") c.ChooseRoad("lance"); // the road (P5): a horse keeps the captain alive
    }

    Party Me => c.Player;
    Balance B => Balance.Get();

    /// Advance the campaign by `hours`, playing every decision on the way.
    public void Run(float hours)
    {
        double until = c.S.Hours + hours;
        while (c.S.Hours < until && !c.S.GameOver)
        {
            c.Tick(MathF.Min(1, (float)(until - c.S.Hours)));
            if (c.Encounter != null) Fight();
            if (c.Arrived is Settlement s) { c.Arrived = null; Visit(s); }
            if (c.Report is { Spoil.Length: > 0 }) c.TakeSpoil(); // a beaten chief's gear is free
            c.Report = null;
            c.Beat = null;
            if (c.S.Event != null) c.Choose(c.EventOf0()); // the stand-in takes the first choice it can afford
            if (c.Lists is Settlement lists) c.AutoTournament(lists);
            if (c.S.Hours >= nextThink) { Think(); nextThink = (float)c.S.Hours + 2; }
            if (!StageDay.ContainsKey(c.Stage)) StageDay[c.Stage] = c.Day;
            if (RenownForVassalDay < 0 && c.Captain.Renown >= Campaign.VassalRenown) RenownForVassalDay = c.Day;
            if (FirstT5Day < 0 && Me.Companies.Any(co => co.Stacks.Any(st => st.Men > 0 && st.Type.Tier >= 5))) FirstT5Day = c.Day;
        }
    }

    void Fight()
    {
        var e = c.Encounter!;
        var setup = c.PrepareBattle(e);
        float me = setup.Side0.Sum(Rules.Strength), them = setup.Side1.Sum(Rules.Strength) * c.Edge(e); // a den or the night fights for them
        // Auto-resolve swings 15% either way: take anything close to even, and pay off brigands rather than bleed.
        // Retreating again and again bleeds the rearguard dry: after two in a day, stand.
        retreats.RemoveAll(t => t < c.S.Hours - 24);
        if (me >= them * 1.05f || !c.CanRetreat(out _) || (retreats.Count >= 2 && me >= them * 0.7f))
        {
            var r = c.AutoResolveEncounter();
            Battles++;
            if (r.Won) Won++;
            return;
        }
        int price = c.ParleyPrice(e);
        if (price > 0 && price < c.S.Gold * 0.5f && c.Parley()) return;
        c.Retreat();
        retreats.Add((float)c.S.Hours);
        Refuge(e);
    }

    readonly List<float> retreats = new();

    /// Run for the nearest friendly town or castle.
    /// Skips seats under siege or with a strong enemy at the gates, and prefers ones that lie away from the threat.
    void Refuge(Party? threat = null)
    {
        float mine = Rules.Strength(Me);
        bool Safe(Settlement t) => !t.Besieged && !c.S.Parties.Any(p => !p.Down && c.Hostile(Me, p) && Rules.Strength(p) > mine * 0.9f && Vector2.Distance(p.Pos, t.Pos) < 25);
        var seat = c.S.Settlements.Where(t => t.Kind != SettlementKind.Village && !c.Refuses(t) && Safe(t))
            .OrderBy(t => Vector2.Distance(t.Pos, Me.Pos) - (threat != null ? 0.5f * Vector2.Distance(t.Pos, threat.Pos) : 0)).FirstOrDefault();
        if (seat != null) c.GoTo(seat);
    }

    /// What to do on arrival: food, wounded, promotions, recruits, contracts.
    void Visit(Settlement s)
    {
        if (c.Refuses(s)) return;
        if (Me.Food < 10) c.BuyFood(14 - (int)Me.Food);
        if (s.Kind != SettlementKind.Village && c.PrisonerCount > 0) c.SellPrisoners(s);
        foreach (int id in Me.Captives.ToList()) if (c.PartyById(id) is Party lord && s.Kind != SettlementKind.Village) c.RansomLord(lord, s);
        if (c.InTown(Me)) Promote();
        // Recruit while there is room and a reserve of gold for a few weeks' upkeep.
        float reserve = Reserve;
        int room = Rules.MenLimit(c.Captain) - Me.Men - Me.Wounded;
        float levy = MathF.Max(0.1f, Rules.PerMan(Troops.Get(c.RecruitTroopFor(s)).Cost) * c.RecruitPriceFactor(s));
        if (room > 0 && s.Recruits > 0 && c.S.Gold > reserve) c.RecruitLevies(s, Math.Min(room, (int)((c.S.Gold - reserve) / levy)));
        if (s.Kind == SettlementKind.Town)
        {
            foreach (var m in s.Mercenaries.ToList())
                if (c.S.Gold - c.MercenaryPrice(m) > reserve * 1.5f && m.Men <= room) { c.Hire(s, m); room -= m.Men; }
            // Only work it can win: brigands or a lord clearly weaker than the company, not too far away.
            if (c.Captain.Contract >= 0 && c.Captain.Contract == s.Faction && c.S.Contracts.Count < 2)
                foreach (var o in c.Offers(s).Where(o => o.Kind == "clear" && c.PartyById(o.Target) is Party t
                             && Rules.Strength(t) * 1.5f < Rules.Strength(Me) && Vector2.Distance(t.Pos, Me.Pos) < 150).Take(1).ToList()) c.Accept(s, o);
        }
        if (Me.Wounded > Me.Men / 3 && s.Kind != SettlementKind.Village) c.Rest(24);
        Kit(s);
        visited[s.Id] = (float)c.S.Hours;
    }

    /// The captain's own kit (P1): the armour and horse that do the most for the money, with plenty of gold to spare;
    /// wear the best piece owned in each slot (spoils included). Weapons stay as they are: the pilot does not fight by hand.
    void Kit(Settlement s)
    {
        foreach (var g in c.ArmourerStock(s).Where(g => g.Slot is "body" or "helm" or "horse").OrderByDescending(g => g.Cost))
            if (c.CannotBuy(g) == null && c.S.Gold - g.Cost > Reserve * 3 && Worth(g) > Worth(Gear.Get(Worn(g.Slot)))) c.BuyGear(g);
        foreach (var slot in new[] { "body", "helm", "horse" })
            if (Gear.InSlot(slot).Where(g => c.Owns(g.Id) && c.CannotUse(g) == null).MaxBy(Worth) is GearDef best && best.Id != Worn(slot)) c.Equip(best.Id);
    }

    string Worn(string slot) => slot switch { "body" => c.Captain.Kit.Body, "helm" => c.Captain.Kit.Helm, _ => c.Captain.Kit.Horse };
    static float Worth(GearDef g) => g.Slot == "horse" ? Troops.Mount(g.Mount).Hp : g.Armor;

    /// When the pilot last did its business in each settlement, so it does not camp in one.
    readonly Dictionary<int, float> visited = new();
    bool Fresh(Settlement s) => !visited.TryGetValue(s.Id, out float at) || c.S.Hours - at > 48;

    /// Gold kept back: a month of what upkeep costs after contract pay and the liege's retainer, plus a margin.
    float Reserve => MathF.Max(0, Me.Companies.Sum(Rules.Upkeep) * (c.Captain.Contract >= 0 ? 1 - B.ContractPay : 1) - c.Retainer) * 4 + 60;

    /// Promotions the purse can pay for while keeping a reserve for upkeep.
    IEnumerable<(CompanyRecord Company, TroopStack Stack, string To)> Affordable() =>
        c.PromotionChoices().Where(p => c.PromotableAnywhere(p.Company, p.Stack, p.To, c.S.Gold - Reserve, out _) > 0);

    /// Spend ready promotions, keeping a reserve for upkeep.
    void Promote()
    {
        foreach (var (co, st, to) in Affordable().ToList())
        {
            if (!co.Stacks.Contains(st)) continue;
            int n = c.Promotable(Me, co, st, to, st.Ready, c.S.Gold - Reserve, out _, out _);
            if (n > 0 && c.Promote(co, st, to, n) == null) Promotions += n;
        }
    }

    /// Take every perk the skills have opened, on the branch in order (the stand-in captain has no favourites).
    void TakePerks()
    {
        foreach (var sk in Skills.All)
            foreach (var pk in sk.Perks)
                if (Skills.Picks(c.Captain, sk.Id) > 0 && !c.Has(pk.Id)) c.TakePerk(pk.Id);
        // A rank's free choice (P5) goes to the first perk anywhere that is open to it.
        foreach (var sk in Skills.All)
            foreach (var pk in sk.Perks)
                if (c.Captain.FreePerks > 0 && Skills.Locked(c.Captain, sk, pk) == null) c.TakePerk(pk.Id);
    }

    void Think()
    {
        TakePerks();
        if (c.Captain.Contract < 0) c.SignContract(c.S.Factions.Count > 0 ? StartRealm : -1);
        if (c.CanSwearFealty(out _)) c.SwearFealty(); // the career (F5): kneel as soon as the liege will have us
        float mine = Rules.Strength(Me);
        // Danger first: a hostile party our size or bigger close by sends us behind walls.
        bool danger = c.S.Parties.Any(p => p != Me && !p.Down && c.Hostile(Me, p) && Rules.Strength(p) > mine * 0.9f && Vector2.Distance(p.Pos, Me.Pos) < 30);
        if (danger) { var near = c.S.Parties.Where(p => p != Me && !p.Down && c.Hostile(Me, p)).OrderBy(p => Vector2.Distance(p.Pos, Me.Pos)).First(); Refuge(near); return; }
        if (Me.Moving && Me.ChaseId >= 0 && c.PartyById(Me.ChaseId) is Party q && Rules.Strength(q) * 1.1f < mine) return;
        if (Siege(mine)) return;
        // The marshal's summons (F7): ride to the host.
        if (c.S.Summons is Summons sm && Me.GoalSettlement != sm.Seat) { var seat = c.SettlementById(sm.Seat); if (!c.Refuses(seat)) c.GoTo(seat); else c.MoveTo(seat.Pos + new Vector2(12, 0)); return; }
        var friendlyTowns = c.S.Settlements.Where(t => t.Kind == SettlementKind.Town && !c.Refuses(t)).ToList();
        Settlement Nearest(IEnumerable<Settlement> list) => list.OrderBy(t => Vector2.Distance(t.Pos, Me.Pos)).First();
        // Business where we stand (we may have stopped here without "arriving").
        if (c.SettlementAt(Me.Pos) is Settlement here && !c.Refuses(here) && Fresh(here)) Visit(here);
        // Running low on food: buy where we stand, else ride to the nearest friendly settlement of any kind.
        if (Me.Food < 5 && c.SettlementAt(Me.Pos) is Settlement shop && !c.Refuses(shop)) c.BuyFood(14 - (int)Me.Food);
        if (Me.Food < 5 && c.S.Settlements.Where(t => !c.Refuses(t)).OrderBy(t => Vector2.Distance(t.Pos, Me.Pos)).FirstOrDefault() is Settlement larder
            && c.SettlementAt(Me.Pos) != larder)
        { if (Me.GoalSettlement != larder.Id) { visited.Remove(larder.Id); c.GoTo(larder); } return; }
        // Badly hurt or holding promotions it can pay for: go to a town.
        bool hurt = Me.Wounded > Me.Men / 2;
        if ((hurt || Affordable().Any() || c.PrisonerCount > 20) && friendlyTowns.Count > 0)
        {
            var t = Nearest(friendlyTowns);
            if (c.SettlementAt(Me.Pos) == t) { if (hurt) c.Rest(12); else visited.Remove(t.Id); }
            else if (!Me.Moving || Me.GoalSettlement != t.Id) { c.GoTo(t); return; }
            if (hurt) return;
        }
        if (Me.Moving && Me.ChaseId < 0) return;
        // Room for men and money for them: the nearest settlement with recruits (villages, and towns and castles raise levies too).
        int room = Rules.MenLimit(c.Captain) - Me.Men - Me.Wounded;
        float reserve = Reserve;
        if (room >= 6 && c.S.Gold > reserve + 30)
        {
            var v = c.S.Settlements.Where(s => s.Recruits >= 3 * B.ArmyScale && !c.Refuses(s) && Fresh(s))
                .OrderBy(s => Vector2.Distance(s.Pos, Me.Pos)).FirstOrDefault();
            if (v != null && Vector2.Distance(v.Pos, Me.Pos) < 150) { c.GoTo(v); return; }
        }
        // A contract target we outmatch, then any hostile party we clearly outmatch.
        var prey = S(c.S.Contracts.Where(k => k.Kind is "clear" or "hunt").Select(k => c.PartyById(k.Target)))
            .Concat(c.S.Parties.Where(p => Vector2.Distance(p.Pos, Me.Pos) < B.Vision * 3)) // a player rides out after work
            .Where(p => p != Me && !p.Down && c.Hostile(Me, p) && Rules.Strength(p) * c.Edge(p) * 1.3f < mine)
            .OrderBy(p => Vector2.Distance(p.Pos, Me.Pos)).FirstOrDefault();
        if (prey != null) { c.Chase(prey); return; }
        // Otherwise ride to a friendly settlement in reach and look for work there.
        var next = c.S.Settlements.Where(s => s.Kind != SettlementKind.Village && !c.Refuses(s) && Vector2.Distance(s.Pos, Me.Pos) > 10)
            .OrderBy(s => Vector2.Distance(s.Pos, Me.Pos) + c.Rand(80)).FirstOrDefault();
        if (next != null) c.GoTo(next);
    }

    /// Sieges (F9, so the stand-in can earn fiefs and rank): once sworn, take a weakly held enemy seat in reach: besiege, wait for
    /// the ladders, then storm it on paper. Returns true while busy with a siege.
    bool Siege(float mine)
    {
        if (c.Besieging is Settlement b)
        {
            if (c.LaddersIn(b) > 0) return true;
            float odds = mine / MathF.Max(1, Rules.Strength(c.GarrisonParty(b)) * 1.4f);
            if (odds >= 1.1f) { var r = c.AutoResolveAssault(b); Battles++; if (r.Won) { Won++; Taken++; } }
            else c.LiftSiege();
            return true;
        }
        if (c.Rank < Rank.SwornSword || Me.Men < 40 * B.ArmyScale || Me.Wounded > Me.Men / 4 || Me.Food < 6) return false;
        var target = c.S.Settlements.Where(s => s.Kind != SettlementKind.Village && c.CanBesiege(s, out _) && Vector2.Distance(s.Pos, Me.Pos) < 140
                && mine > Rules.Strength(c.GarrisonParty(s)) * 2.2f
                && !c.S.Parties.Any(p => !p.Down && p.Kind == PartyKind.Lord && c.Hostile(Me, p) && Rules.Strength(p) > mine * 0.6f && Vector2.Distance(p.Pos, s.Pos) < 50))
            .OrderBy(s => Vector2.Distance(s.Pos, Me.Pos)).FirstOrDefault();
        if (target == null) return false;
        if (Vector2.Distance(target.Pos, Me.Pos) > 6) { c.MoveTo(target.Pos + Vector2.Normalize(Me.Pos - target.Pos + new Vector2(0.01f, 0)) * 4); return true; }
        c.Besiege(target);
        return true;
    }

    static IEnumerable<Party> S(IEnumerable<Party?> list) => list.Where(p => p != null)!;
}
