using Microsoft.Xna.Framework;

namespace Ironfield;

/// One company's line in a battle report (also the results screen).
public sealed record CompanyReport(int Id, string Name, string Troop, int Team, int MenBefore, int MenAfter, int Killed, int Wounded, int Captured,
    int FledLost, float XpGain, bool UpgradeReady, bool Deployed);

/// Everything a finished battle changed, for the results screen.
public sealed record BattleReport(bool Won, string Enemy, IReadOnlyList<CompanyReport> Companies, float Loot, float Ransom, float Bounty,
    float Renown, int Horses, LeaderFate Captain, string Spoil = "")
{
    /// The captain's skills that rose in this battle, one line each (P3).
    public IReadOnlyList<string> Rises { get; init; } = Array.Empty<string>();
    /// A lasting wound the captain took in this battle (P4), or "".
    public string LastingWound { get; init; } = "";
    /// What the battle did to your aura (docs/KNIGHT_ORDERS.md K1): a wall reached, a breakthrough, or "".
    public string Aura { get; init; } = "";
    /// The battle's personal goals and whether each was met (P6).
    public IReadOnlyList<(string Text, bool Met)> Goals { get; init; } = Array.Empty<(string, bool)>();
    /// Your named men in this battle (docs/LEGACY.md L3): the fallen and missing first, then the hurt, then new names.
    public IReadOnlyList<(string Kind, string Text)> Roll { get; init; } = Array.Empty<(string, string)>();
    /// The battle's name ("Ashby Ford", L5), "" when it has none (a duel, the lists).
    public string Field { get; init; } = "";
    /// Banner honours this battle won, lost or won back (L5): HONOUR or LOST, and the line.
    public IReadOnlyList<(string Kind, string Text)> Honours { get; init; } = Array.Empty<(string, string)>();
}

/// A battle about to be fought between map parties: the armies, options, and how its companies map back to campaign records.
public sealed class BattleSetup
{
    public int Seed;
    public ArmySpec Player = null!, Enemy = null!;
    public BattleOptions Options = new();
    public readonly List<Party> Side0 = new(), Side1 = new();
    public string EnemyName = "";
    /// The battle's name ("Ashby Ford", L5), for its opening line; "" for duels and the lists.
    public string Field = "";
    /// An assault on this settlement (-1: a battle in the field).
    public int SiegeOf = -1;
    /// A tournament in this settlement (S5; -1: a real fight).
    public int Tournament = -1;
    /// A duel with this party's lord (P2), -1 = none.
    public int Duel = -1;
}

/// Company rules (caps, strength, XP, upgrades), auto-resolve, and the battle bridge (docs/CAMPAIGN.md 4-6).
public static class Rules
{
    static Balance B => Balance.Get();

    public static string RoleOf(CompanyRecord c) => c.Role is { Length: > 0 } r ? r : TroopRole(c.Troop);

    /// A troop's role from its class (the same rule as Armies.RoleOf): archers, spears (skirmishers), horse, or infantry.
    public static string TroopRole(string troop) => Armies.RoleOf(new Company(troop, 1));

    /// May this troop stand in this company? Any troop of the company's role, from any tree and tier (2.1); raw tier I recruits
    /// also drill in any foot company until they are promoted into a line. Horse only joins horse, so a company is all-mounted or all-foot.
    public static bool Fits(CompanyRecord c, string troop)
    {
        string role = RoleOf(c), tr = TroopRole(troop);
        if (tr == role) return true;
        return Troops.Get(troop).Tier == 1 && role != "horse" && tr != "horse";
    }

    /// How many places one man of a tier takes in a company (2.1): elites fill a company sooner *(tune)*.
    public static float TierWeight(int tier) => tier switch { <= 1 => 0.8f, 2 => 1f, 3 => 1.15f, 4 => 1.3f, 5 => 1.4f, 6 => 1.5f, _ => 1.6f };

    /// A company's places, from its role (shared by all its stacks).
    /// Mages are a circle of a few, not a company of many (super units): three or four at ArmyScale 3, one or two at 1.
    public static float Cap(string role) => role == "casters" ? B.ArmyScale * 1.2f + 1
        : B.ArmyScale * role switch { "archers" => 16, "spears" => 12, "horse" => 10, _ => 24 };

    /// A price per man (wage, levy, loot, ransom, promotion): the troop price spread over ArmyScale men.
    public static float PerMan(float cost) => cost / B.ArmyScale;

    /// The places a company's men (fit and wounded) take.
    public static float Places(CompanyRecord c) => c.Stacks.Sum(s => (s.Men + s.Wounded) * TierWeight(s.Type.Tier));

    /// How many more men of this troop the company has room for (0 if the troop does not fit it).
    public static int Room(CompanyRecord c, string troop) =>
        !Fits(c, troop) ? 0 : Math.Max(0, (int)MathF.Floor((Cap(RoleOf(c)) - Places(c)) / TierWeight(Troops.Get(troop).Tier) + 1e-4f));

    /// A full company of one troop: the most men of it a fresh company can hold.
    public static int CapOf(string troop, string role) => (int)MathF.Floor(Cap(role) / TierWeight(Troops.Get(troop).Tier) + 1e-4f);

    public static float Strength(UnitType t, int men)
    {
        float cls = t.Class switch { UnitClass.Cavalry => 1.35f, UnitClass.Archer => 0.9f, UnitClass.Caster => 1 + 0.35f * t.Spells.Length, _ => 1f }; // a mage is worth his spells, not his staff
        if (t.Huge) cls *= 2f; // a troll's blows sweep through several men, and it heals
        return men * (t.MaxHp + 5 * t.Armor + 2 * t.Damage) * cls / 100f;
    }

    public static float Strength(CompanyRecord c) => c.Stacks.Sum(s => Strength(s.Type, s.Men));

    /// Strength with drill weighed in, as a fought battle weighs it (docs/MELEE.md D8, --agree): who wins an auto-resolve.
    public static float Might(UnitType t, int men) => MightAt(t, men, B.DrillWeight, B.TraitWeight);
    /// Might at a chosen drill and trait weight (--agree tries other weights): drill, and a tier V-VII troop's style and traits.
    public static float MightAt(UnitType t, int men, float drillWeight, float traitWeight) =>
        Strength(t, men) * (1 + drillWeight * Battle.DrillOf(t)) * (1 + traitWeight * t.StyleRank);
    public static float Might(Party p) => p.Companies.Sum(c => c.Stacks.Sum(s => Might(s.Type, s.Men)));
    public static float Strength(Party p) => p.Companies.Sum(Strength);

    public static int Upkeep(CompanyRecord c) => (int)MathF.Ceiling(c.Stacks.Sum(s => (s.Men + s.Wounded) * PerMan(s.Type.Cost) * B.UpkeepRate));

    /// Has any stack a man ready for promotion?
    public static bool UpgradeReady(CompanyRecord c) => c.Ready > 0;

    /// What promoting one man from `from` to `to` costs: the difference in troop price, and a horse when he moves into the
    /// saddle (from the party's horses if `hasHorse`, else bought at the horse price).
    public static int PromoteCost(string from, string to, bool hasHorse)
    {
        var a = Troops.Get(from);
        var b = Troops.Get(to);
        float cost = Math.Max(0, b.Cost - a.Cost);
        if (b.Mount != null && a.Mount == null && !hasHorse) cost += b.Mount.Hp * B.HorseCostRate;
        return (int)MathF.Ceiling(PerMan(cost));
    }

    public static bool NeedsHorse(string from, string to) => Troops.Get(to).Mount != null && Troops.Get(from).Mount == null;

    /// Add men of a troop to a company: they arrive with no XP (the pool is untouched, so veterans are not diluted).
    public static void AddMen(CompanyRecord c, string troop, int n)
    {
        if (n <= 0) return;
        c.Stack(troop).Men += n;
    }

    public static bool CanMerge(CompanyRecord a, CompanyRecord b) =>
        a != b && RoleOf(a) == RoleOf(b) && b.Stacks.All(s => Fits(a, s.Troop)) && Places(a) + Places(b) <= Cap(RoleOf(a)) + 1e-3f;

    public static void Merge(CompanyRecord into, CompanyRecord from)
    {
        int men = into.Men + from.Men;
        into.Cohesion = men > 0 ? (into.Cohesion * into.Men + from.Cohesion * from.Men) / men : into.Cohesion;
        foreach (var s in from.Stacks)
        {
            var t = into.Stack(s.Troop);
            t.Men += s.Men;
            t.Wounded += s.Wounded;
            t.Xp += s.Xp;
            t.Xp = Math.Clamp(t.Xp, 0, t.PoolCap);
        }
        into.Battles = Math.Max(into.Battles, from.Battles);
        into.Kills += from.Kills;
        into.HorseKills += from.HorseKills;
        into.Assaults = Math.Max(into.Assaults, from.Assaults);
        foreach (var h in from.Honours) if (!into.Honours.Contains(h) && into.Honours.Count < 3) into.Honours.Add(h);
        into.Roll.AddRange(from.Roll); from.Roll.Clear(); // the named men keep their names (L1); the roll may run past RollSize
        into.RollStarted |= from.RollStarted;
    }

    public static string CompanyName(Rng rng, string role)
    {
        var n = Names.Get();
        var nouns = n.Nouns.TryGetValue(role, out var l) ? l : n.Nouns["infantry"];
        return rng.Pick(n.Places) + " " + rng.Pick(nouns);
    }

    // ---------- the captain ----------

    /// Your troop type in battle: the captain's data dressed in the kit (P1), with the second weapon in hand if `side`.
    public static UnitType CaptainType(CaptainState c, bool side = false)
    {
        var k = c.Kit;
        var w = Gear.Get(side && k.Side != "" ? k.Side : k.Weapon);
        var shield = Gear.Get(w.TwoHanded ? "no_shield" : k.Shield);
        GearDef body = Gear.Get(k.Body), helm = Gear.Get(k.Helm);
        var t = Troops.Get("captain").Clone();
        t.Look = t.Look with { Weapon = w.Look, Shield = shield.Look, Body = body.Look, Legs = body.Legs, Feet = body.Feet, Head = helm.Look };
        (t.Damage, t.Reach, t.Windup, t.Recover, t.Pierce) = (w.Damage, w.Reach, w.Windup, w.Recover, w.Pierce);
        (t.Ammo, t.Range, t.Reload, t.ThrustOnly) = (w.Ammo, w.Range, w.Reload, w.Thrust);
        t.BlockChance = shield.Block;
        t.Armor += body.Armor + helm.Armor;
        t.Speed += body.Speed + helm.Speed + shield.Speed;
        // Arms (F4): +2% health per 10 points, Hardened +20%, Duellist +15% damage.
        t.MaxHp *= 1 + Skills.Level(c, "Arms") / 500f + (c.Perks.Contains("Hardened") ? 0.2f : 0);
        if (c.Perks.Contains("Duellist")) t.Damage *= 1.15f;
        if (w.Ranged && c.Perks.Contains("Marksman")) t.Damage *= 1.2f;   // Bowcraft perks (P3)
        if (w.Ranged && c.Perks.Contains("Fletcher")) t.Ammo = (int)(t.Ammo * 1.5f);
        if (c.Perks.Contains("HorseBreaker")) t.VsMounted *= 1.25f;      // Riding
        if (c.Wounds.Contains("knee")) t.Speed *= 0.9f;                  // lasting wounds (P4)
        if (c.Wounds.Contains("hand")) t.Damage *= 0.9f;
        return t;
    }

    /// Your horse: Riding adds 1% health per 4 points; Mounted Lord +30% health, Saddle-Fast +10% gallop.
    public static Mount CaptainMount(CaptainState c)
    {
        var m = Troops.Mount(Gear.Get(c.Kit.Horse).Mount);
        m = m with { Hp = m.Hp * (1 + Skills.Level(c, "Riding") / 400f + (c.Perks.Contains("MountedLord") ? 0.3f : 0)) };
        return c.Perks.Contains("SaddleFast") ? m with { Gallop = m.Gallop * 1.1f } : m;
    }

    /// The captain's part of a battle's options: skills, battle perks, kit and horse.
    public static BattleOptions WithCaptain(this BattleOptions o, CaptainState c)
    {
        o.Arms = Skills.Level(c, "Arms");
        o.Riding = Skills.Level(c, "Riding");
        o.Bowcraft = Skills.Level(c, "Bowcraft");
        o.Perks = c.Perks.ToHashSet();
        o.Wounds = c.Wounds.ToHashSet();
        o.AuraTier = Math.Max(0, c.AuraTier - (c.Wounds.Contains("core") ? 1 : 0)); // a cracked core holds you a tier down (K5)
        o.PlayerType = CaptainType(c);
        o.PlayerSide = c.Kit.Side != "" ? CaptainType(c, side: true) : null;
        o.PlayerMount = CaptainMount(c);
        return o;
    }

    public static int CompanySlots(CaptainState c) => Math.Clamp((int)B.CompanySlotsBase, 1, 10) + (Campaign.GuardFor(c.BestRank).Men > 0 ? 1 : 0); // the guard's own (P5)
    /// Men you may lead: a base, renown, Leadership (+1 per 5 points) and the Born Captain perk.
    public static int MenLimit(CaptainState c) => (int)(B.ArmyScale * (B.MenBase + (int)(c.Renown / B.MenRenownDivisor) + Skills.Level(c, "Leadership") / 5
        + (c.Perks.Contains("BornCaptain") ? 10 : 0) + Campaign.GuardFor(c.BestRank).Men)); // the household guard does not crowd your ranks (P5)

    /// The next rung of the renown ladder, for the panel: renown now raises only the men cap (perks come from skills, F4).
    public static (string What, float At) NextRenownUnlock(CaptainState c)
    {
        float renownPart = c.Renown / B.MenRenownDivisor;
        float at = ((int)renownPart + 1) * B.MenRenownDivisor;
        return ($"MEN CAP {MenLimit(c) + (int)B.ArmyScale}", at);
    }

    // ---------- auto-resolve (5.4) ----------

    /// Fight it out on paper: the result has the same shape as a played battle.
    /// `wall` multiplies side 1's strength: defenders behind walls (1.4 against ladders, more without them).
    public static BattleResult AutoResolve(IReadOnlyList<Party> side0, IReadOnlyList<Party> side1, Rng rng, float edge0 = 1, float wall = 1)
    {
        float s0 = side0.Sum(Might) * edge0 * rng.Range(0.85f, 1.15f);
        float s1 = side1.Sum(Might) * wall * rng.Range(0.85f, 1.15f);
        int t0 = side0.Max(p => p.Tier), t1 = side1.Max(p => p.Tier);
        s0 *= 1 + 0.1f * (t0 - t1);
        int winner = s0 >= s1 ? 0 : 1;
        float ratio = winner == 0 ? s1 / MathF.Max(1, s0) : s0 / MathF.Max(1, s1); // loser / winner
        float loserLoss = rng.Range(0.45f, 0.75f), winnerLoss = Math.Clamp(ratio * 0.45f, 0.03f, 0.6f);
        var list = new List<CompanyResult>();
        var kills = new float[2];
        for (int team = 0; team < 2; team++)
        {
            var side = team == 0 ? side0 : side1;
            float loss = team == winner ? winnerLoss : loserLoss;
            // Losses fall on the lower tiers: each stack's share is its men over its tier.
            var stacks = side.SelectMany(p => p.Companies).Where(c => c.Men > 0).SelectMany(c => c.Stacks.Where(st => st.Men > 0).Select(st => (c, st))).ToList();
            float weight = stacks.Sum(x => x.st.Men / (float)x.st.Type.Tier);
            int totalLoss = (int)MathF.Round(stacks.Sum(x => x.st.Men) * loss);
            foreach (var co in stacks.GroupBy(x => x.c))
            {
                var parts = co.Select(x =>
                {
                    int fallen = Math.Min(x.st.Men, (int)MathF.Round(totalLoss * (x.st.Men / (float)x.st.Type.Tier) / MathF.Max(1, weight)));
                    int fled = team == winner ? 0 : (int)((x.st.Men - fallen) * 0.15f);
                    return new StackResult(x.st.Troop, x.st.Men, x.st.Men - fallen - fled, fled, fallen);
                }).ToList();
                int men = parts.Sum(p => p.Start), fallenAll = parts.Sum(p => p.Fallen), fledAll = parts.Sum(p => p.Fled);
                kills[1 - team] += fallenAll;
                list.Add(new CompanyResult(team, co.Key.Id, men, men - fallenAll - fledAll, fledAll, fallenAll, 0, new int[8], true, parts,
                    Routed: team != winner && fledAll + fallenAll > men / 2));
            }
        }
        // Kills go to each side's companies by strength, as victims of the enemy's average tier.
        for (int team = 0; team < 2; team++)
        {
            var side = team == 0 ? side0 : side1;
            var foes = (team == 0 ? side1 : side0).SelectMany(p => p.Companies).Where(c => c.Men > 0).ToList();
            int foeTier = foes.Count == 0 ? 1 : (int)MathF.Round((float)foes.Average(c => c.Type.Tier));
            float foeMounted = foes.Count == 0 ? 0 : foes.Sum(c => c.Stacks.Where(s => s.Type.Mount != null).Sum(s => s.Men)) / (float)Math.Max(1, foes.Sum(c => c.Men));
            float total = side.Sum(Strength);
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r.Team != team) continue;
                var c = side.SelectMany(p => p.Companies).First(c => c.Id == r.Id);
                int k = (int)MathF.Round(kills[team] * Strength(c) / MathF.Max(1, total));
                var byTier = new int[8];
                byTier[Math.Clamp(foeTier, 1, 7)] = k;
                list[i] = r with { Kills = k, KillsByTier = byTier, HorseKills = (int)MathF.Round(k * foeMounted) };
            }
        }
        return new BattleResult(winner, 0, list, LeaderFate.Unhurt, LeaderFate.Unhurt, 0);
    }

    // ---------- the bridge (5.2, 5.5) ----------

    public static ArmySpec Army(string name, IEnumerable<Party> parties, float moraleBonus = 0, bool siege = false) => new(name, 0,
        parties.SelectMany(p => p.Companies).Where(c => c.Men > 0)
            .Select(c =>
            {
                var h = Honours.Of(c);
                var mix = c.Stacks.Where(s => s.Men > 0).Select(s => new StackSpec(s.Troop, s.Men)).ToArray();
                return new Company(c.Troop, c.Men, RoleOf(c), c.Id, c.Name,
                    Math.Clamp(70 + c.Cohesion + moraleBonus + (c.Traits.Contains("Steady") ? 8 : 0) + h.Morale + (siege ? h.SiegeMorale : 0), 20, 100), c.Traits.ToArray(),
                    Allied: parties.First(p => p.Companies.Contains(c)).Kind == PartyKind.Lord, Mix: mix.Length > 1 ? mix : null,
                    RoutBonus: h.RoutBonus, VsMounted: h.VsMounted, Banner: c.Banner, Hero: c.Hero,
                    Streamers: c.BattleHonours.Count, Fringe: c.Battles >= 10, Cohesion: c.Cohesion,
                    Named: c.Roll.Count > 0 ? c.Roll.Where(m => !m.Hurt).Select(m => new NamedSpec(m.Id, m.Troop, m.Name)).ToArray() : null);
            }).ToList());
}
