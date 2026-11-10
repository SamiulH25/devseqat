using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: lords' armies and what a lord puts first. A realm at war gathers free lords into an army under one leader (the
/// marshal when he can ride): they muster, march together at the pace of the slowest, fight side by side, and besiege an enemy
/// seat or ride to relieve one of their own. A lord with a job (an army, a siege, a refit, following you) ignores rabble
/// (brigands, monsters) unless it falls on him; a free lord at war only hunts rabble on his own realm's land.
public partial class Campaign
{
    /// The most lords in one army; how long an army lasts before it goes home; how long it waits for its lords to come in.
    public const int ArmyMax = 6;
    public const float ArmyDays = 20, ArmyMusterHours = 36, ArmyGather = 12;

    public LordArmy? ArmyOf(Party p) => p.ArmyId < 0 ? null : S.Armies.FirstOrDefault(a => a.Id == p.ArmyId);

    /// Rabble: brigands and monsters. Only a lord with nothing better to do goes after them.
    public static bool Rabble(Party q) => q.Kind is PartyKind.Bandit or PartyKind.Monster;

    /// A lord with a job that outranks hunting rabble.
    public static bool OnDuty(Party p) => p.Kind == PartyKind.Lord
        && (p.ArmyId >= 0 || p.SiegeTarget >= 0 || p.Task is "follow" or "refit" or "crusade" or "escort" or "army" or "muster");

    /// The parties that fight beside p when it meets an enemy: itself and the lords of its army close by.
    List<Party> Comrades(Party p)
    {
        var side = new List<Party> { p };
        if (ArmyOf(p) is not LordArmy a) return side;
        foreach (int id in a.Members)
            if (id != p.Id && PartyById(id) is Party m && !m.Down && m.Cooldown <= 0 && Vector2.Distance(m.Pos, p.Pos) < B.JoinRadius * Map.Spread) side.Add(m);
        return side;
    }

    /// The weight of p and the comrades who would fight beside it.
    float SideStrength(Party p) => Comrades(p).Sum(Rules.Strength);

    /// Gather lords into an army under a leader against a seat ("siege") or to relieve one of the realm's own ("relieve").
    /// Lords who think badly of the leader stay away. Null when fewer than two lords come.
    internal LordArmy? FormArmy(int f, Settlement target, string aim, IEnumerable<Party> lords)
    {
        var ready = lords.Where(l => l.Kind == PartyKind.Lord && !l.Down && l.ArmyId < 0 && l.KnightOrder == "" && l.SiegeTarget < 0
            && l.Men >= Scaled(45) && !(IsVassal(l) && l.Order is "defend" or "follow")).ToList();
        if (ready.Count < 2) return null;
        var marshal = MarshalOf(f);
        var leader = ready.FirstOrDefault(l => marshal != null && CharacterOf(l) == marshal) ?? ready.OrderByDescending(Rules.Strength).First();
        var chief = CharacterOf(leader);
        var come = new List<Party> { leader };
        foreach (var l in ready.Where(l => l != leader).OrderBy(l => Vector2.Distance(l.Pos, leader.Pos)))
        {
            if (come.Count >= ArmyMax) break;
            if (chief != null && CharacterOf(l) is Character ch && OpinionOf(ch, chief) < -20) continue; // he stays home
            come.Add(l);
        }
        if (come.Count < 2) return null;
        var army = new LordArmy { Id = S.NextId++, Faction = f, Leader = leader.Id, Target = target.Id, Aim = aim, Since = (float)S.Hours, Until = (float)S.Hours + ArmyDays * 24 };
        foreach (var l in come)
        {
            army.Members.Add(l.Id);
            l.ArmyId = army.Id;
            l.PatrolProvince = target.Province;
            l.ChaseId = -1;
            l.TaskUntil = 0;
        }
        S.Armies.Add(army);
        Log(aim == "relieve" ? $"{leader.Name} gathers an army of {come.Count} lords to relieve {target.Name}."
            : $"{leader.Name} gathers an army of {come.Count} lords to march on {target.Name}.");
        return army;
    }

    /// Send the army's lords back to their own business.
    void Disband(LordArmy a, string news = "")
    {
        foreach (int id in a.Members)
            if (PartyById(id) is Party l && l.ArmyId == a.Id)
            {
                l.ArmyId = -1;
                if (l.Task is "army" or "muster") { l.Task = "patrol"; l.TaskUntil = 0; l.ChaseId = -1; }
            }
        S.Armies.Remove(a);
        if (news != "") Log(news);
    }

    void LeaveArmy(LordArmy a, Party l)
    {
        a.Members.Remove(l.Id);
        l.ArmyId = -1;
        if (l.Task is "army" or "muster") l.Task = "patrol";
        if (a.Members.Count < 2) Disband(a);
    }

    /// Daily: armies whose leader fell, whose time is up or whose work is done (the seat taken, the war over, the siege lifted) break up.
    internal void ArmiesDaily()
    {
        foreach (var a in S.Armies.ToList())
        {
            a.Members.RemoveAll(id => PartyById(id) is not Party l || l.ArmyId != a.Id);
            var leader = PartyById(a.Leader);
            var target = S.Settlements.FirstOrDefault(s => s.Id == a.Target);
            if (leader == null || leader.Down || leader.ArmyId != a.Id) Disband(a, leader == null ? "" : $"{leader.Name}'s army breaks up.");
            else if (a.Members.Count < 2 || target == null) Disband(a);
            else if (S.Hours > a.Until) Disband(a, $"{leader.Name}'s army goes home after {ArmyDays:0} days in the field.");
            else if (a.Aim == "siege" && !FactionsAtWar(a.Faction, target.Faction)) Disband(a, target.Faction == a.Faction ? "" : $"{leader.Name}'s army goes home: there is no war with {target.Name}.");
            else if (a.Aim == "relieve" && (!target.Besieged || target.Faction != a.Faction)) Disband(a, target.Faction == a.Faction ? $"{target.Name} is safe; {leader.Name}'s army breaks up." : "");
        }
    }

    /// The realm's armies (FactionAI, daily): a seat of its own under siege is relieved first; then, with lords to spare and a
    /// target, a new army marches. Returns true when it formed one.
    bool RealmArmies(int f, List<Party> lords, List<Party> free, Province? target)
    {
        var mine = S.Armies.Where(a => a.Faction == f).ToList();
        var besieged = S.Settlements.Where(s => s.Faction == f && s.Besieged && s.Kind != SettlementKind.Village && !mine.Any(a => a.Target == s.Id))
            .OrderByDescending(s => s.Kind == SettlementKind.Town).FirstOrDefault();
        if (besieged != null)
        {
            var near = free.Where(l => Vector2.Distance(l.Pos, besieged.Pos) < 150 * Map.Spread).ToList();
            if (FormArmy(f, besieged, "relieve", near) != null) return true;
        }
        int cap = 1 + lords.Count / 8;
        if (target == null || mine.Count >= cap || free.Count < 2) return false;
        return MusterHost(f, target, free) > 0;
    }

    /// One lord in an army, each hour: the leader musters, marches, fights what is worth fighting and lays the siege;
    /// the others ride with him. Returns false when the lord is no longer in an army (left, or it broke up).
    bool ArmyThink(Party p, bool needsRefit, List<Party> hostiles)
    {
        var a = ArmyOf(p);
        if (a == null) { p.ArmyId = -1; return false; }
        var leader = PartyById(a.Leader);
        if (leader == null || leader.Down) { Disband(a); return false; }
        if (needsRefit)
        {
            if (p == leader) Disband(a, $"{p.Name} leads his army home to refit.");
            else LeaveArmy(a, p);
            return false;
        }
        if (p.SiegeTarget >= 0) { p.Task = "siege"; p.ChaseId = -1; p.Moving = false; p.TaskUntil = (float)S.Hours + 4; return true; }
        var target = SettlementById(a.Target);
        if (p != leader)
        {
            // Ride with the leader, a little off his shoulder.
            float ang = p.Id * 2.4f;
            p.Task = S.Hours < a.Since + ArmyMusterHours ? "muster" : "army";
            p.ChaseId = -1;
            p.Goal = leader.Pos + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 2.5f * Map.Spread;
            p.Moving = Vector2.Distance(p.Pos, p.Goal) > 0.5f;
            p.TaskUntil = (float)S.Hours + 1;
            return true;
        }
        // The leader: wait for the lords to come in, but not for ever.
        var members = a.Members.Select(PartyById).OfType<Party>().Where(m => !m.Down).ToList();
        if (S.Hours < a.Since + ArmyMusterHours && members.Any(m => Vector2.Distance(m.Pos, p.Pos) > ArmyGather * Map.Spread))
        {
            p.Task = "muster"; p.ChaseId = -1; p.Moving = false; p.TaskUntil = (float)S.Hours + 2;
            return true;
        }
        p.Task = "army";
        p.TaskUntil = (float)S.Hours + 2;
        // An enemy lord close by and clearly weaker than the army is worth the fight; rabble is not.
        float weight = members.Where(m => Vector2.Distance(m.Pos, p.Pos) < 20 * Map.Spread).Sum(Rules.Strength);
        if (hostiles.Where(q => q.Kind is PartyKind.Lord or PartyKind.Player && Vector2.Distance(q.Pos, p.Pos) < 20 * Map.Spread && Rules.Strength(q) < weight * 0.9f)
                .OrderBy(q => q.SiegeTarget == a.Target ? 0 : 1).ThenBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party foe)
        {
            p.ChaseId = foe.Id; p.Moving = true;
            return true;
        }
        p.ChaseId = -1;
        if (a.Aim == "siege" && Vector2.Distance(p.Pos, target.Pos) < 25 * Map.Spread)
        {
            p.PatrolProvince = target.Province;
            if (TryBesiege(p)) return true;
            // Too strong to besiege: turn on a weaker enemy seat nearby, or go home.
            float appetite = 1.6f / Factions.Get(a.Faction).Siege;
            if (S.Settlements.Where(s => s.Id != target.Id && s.Kind != SettlementKind.Village && FactionsAtWar(a.Faction, s.Faction)
                    && Vector2.Distance(s.Pos, p.Pos) < 80 * Map.Spread && Rules.Strength(GarrisonParty(s)) * appetite < weight)
                    .OrderBy(s => Vector2.Distance(s.Pos, p.Pos)).FirstOrDefault() is Settlement next)
            {
                Log($"{target.Name} is too strong; {p.Name}'s army turns on {next.Name}.");
                a.Target = next.Id;
                p.Goal = next.Pos; p.Moving = true;
                foreach (int id in a.Members) if (PartyById(id) is Party m) m.PatrolProvince = next.Province;
                return true;
            }
            Disband(a, $"{target.Name} is too strong; {p.Name}'s army turns back.");
            return false;
        }
        p.Goal = target.Pos;
        p.Moving = true;
        return true;
    }

    /// An army marches at the pace of its slowest lord: the leader's speed, capped by his lords nearby.
    float ArmyPace(Party p, float s)
    {
        if (ArmyOf(p) is not LordArmy a || a.Leader != p.Id) return s;
        foreach (int id in a.Members)
            if (id != p.Id && PartyById(id) is Party m && !m.Down && Vector2.Distance(m.Pos, p.Pos) < ArmyGather * Map.Spread) s = MathF.Min(s, BaseSpeed(m));
        return s;
    }
}
