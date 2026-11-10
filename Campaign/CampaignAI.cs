using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: AI parties and realms (spawning lords and bandits, LordThink, BanditThink, refits, FactionAI).
public partial class Campaign
{
    // ---------- AI ----------

    void SpawnLord(int faction)
    {
        var towns = S.Settlements.Where(t => t.Faction == faction && t.Kind != SettlementKind.Village).ToList();
        if (towns.Count == 0) return;
        var home = rng.Pick(towns);
        var n = Names.Get();
        var lord = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Lord, Faction = faction, HomeId = home.Id, Pos = home.Pos + new Vector2(rng.Range(-4, 4), rng.Range(-4, 4)),
            Gold = 400, Tier = 1 + rng.Next(2),
        };
        MakeLord(lord);
        lord.Goal = lord.Pos;
        int tier = Math.Clamp(1 + rng.Next(2) + Day / 30, 1, 7);
        foreach (var role in new[] { "infantry", "archers", rng.Next(2) == 0 ? "spears" : "horse" })
        {
            string troop = Armies.LineTroop(role, Math.Clamp(tier + rng.Next(-1, 1), 1, 7), faction);
            lord.Companies.Add(NewCompany(troop, role, (int)(Rules.CapOf(troop, role) * rng.Range(0.7f, 1f)), 0, home));
        }
        // Mages are super units: only a great lord (army level 5 up), one in five, brings a circle of his realm's mages (E1); no
        // dice, so the campaign's draws stay as they were.
        if (tier >= 5 && lord.Id % 5 == 0)
        {
            string mage = Armies.MageTroop(Factions.CultureOf(faction), tier - 1);
            lord.Companies.Add(NewCompany(mage, "casters", (int)(Rules.CapOf(mage, "casters") * 0.8f), 0, home));
        }
        S.Parties.Add(lord);
    }

    /// A band name no band on the map carries now, if one is left (S1).
    string BandName()
    {
        var all = Names.Get().Bandits;
        var free = all.Where(b => !S.Parties.Any(p => !p.Down && p.Name == b)).ToList();
        return rng.Pick(free.Count > 0 ? free : all);
    }

    void SpawnBandits()
    {
        var villages = S.Settlements.Where(v => v.Kind == SettlementKind.Village).ToList();
        var at = rng.Pick(villages);
        var n = Names.Get();
        // Bands grow as the campaign does, so brigandry stays a threat instead of becoming free loot for a growing company.
        int tier = Math.Clamp(1 + Day / (int)MathF.Max(1, B.BanditTierPerDays), 1, 5);
        int men = Scaled(B.BanditBandMin + rng.Next(Math.Max(1, B.BanditBandMax - B.BanditBandMin)) + Day / (int)MathF.Max(1, B.BanditGrowPerDays));
        string foot = Armies.LineTroop("infantry", tier, rng.Next(3));
        var band = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Bandit, Name = BandName(), Tier = tier,
        };
        band.Pos = at.Pos + new Vector2(rng.Range(-14, 14), rng.Range(-14, 14)) * Map.Spread;
        band.Goal = band.Pos;
        band.Companies.Add(NewCompany(foot, "infantry", men, 0));
        if (rng.Chance(0.6f))
            band.Companies.Add(NewCompany(Armies.LineTroop(rng.Chance(0.6f) ? "archers" : "horse", tier, rng.Next(3)), rng.Chance(0.6f) ? "archers" : "horse", Scaled(rng.Next(5, 10) + Day / 20), 0));
        foreach (var c in band.Companies) c.Role = Rules.TroopRole(c.Troop) is "horse" or "archers" ? Rules.TroopRole(c.Troop) : c.Role;
        S.Parties.Add(band);
    }

    void RefreshMercenaries(Settlement t)
    {
        t.Mercenaries.Clear();
        int offers = 1 + rng.Next(2);
        for (int i = 0; i < offers; i++)
        {
            string role = rng.Pick(new[] { "infantry", "archers", "spears", "horse" });
            int tier = Math.Clamp(2 + Day / 40 + (rng.Chance(0.35f) ? 1 : 0), 2, 6);
            // Sellswords come from every realm: a foreign company is the other way to field a culture's elite lines (4.2).
            int from = rng.Chance(0.5f) ? t.Faction : rng.Next(S.Factions.Count);
            string troop = Armies.LineTroop(role, tier, from);
            var m = NewCompany(troop, role, (int)(Rules.CapOf(troop, role) * rng.Range(0.6f, 0.9f)), 0);
            m.Culture = Factions.CultureOf(from);
            m.Stacks[0].AddXp(rng.Range(0, Math.Max(1, m.Type.UpgradeXp) * 0.5f) * m.Men);
            m.Name = Rules.CompanyName(rng, role);
            t.Mercenaries.Add(m);
        }
        // Mages are rare: a town with a chapel, or a shrine or hermit near it, has a circle to hire one week in six (its own
        // week, no dice); Acolytes at first, from day 60 its culture's mages (E1).
        if ((Level(t, "chapel") > 0 || Sites.Any(x => x.Kind is "shrine" or "hermit" && Vector2.Distance(x.Pos, t.Pos) < 40)) && (t.Id + Day / 7) % 6 == 0)
        {
            string mage = Day >= 60 ? Armies.MageTroop(t.Culture, 4) : "acolytes";
            var m = NewCompany(mage, "casters", (int)(Rules.CapOf(mage, "casters") * 0.8f), 0);
            m.Name = Day >= 60 ? $"The {t.Name} {Troops.Get(mage).Name}" : $"Acolytes of {t.Name}";
            t.Mercenaries.Add(m);
        }
        // Now and then a few ogres sell their clubs (docs/CREATURES.md phase 4): costly, and the only big beasts for hire.
        if (rng.Chance(B.OgreOfferChance))
        {
            var o = NewCompany("ogre", Rules.TroopRole("ogre"), Scaled(B.OgreMen), 0);
            o.Name = "Ogre Sellswords";
            t.Mercenaries.Add(o);
        }
    }

    IEnumerable<Party> Visible(Party p, float range) => S.Parties.Where(q => q != p && !q.Down && Vector2.Distance(p.Pos, q.Pos) < range);

    /// Lords of a realm whose temper sends them after monsters first (docs/WORLD.md W5).
    /// A knight order's armies hunt what their order hunts: monsters and the dead (docs/KNIGHT_ORDERS.md 4.4).
    bool MonsterHunter(Party p) => p.Faction >= 0 && Factions.Get(p.Faction).Hunts == "monsters" || OrderOf(p)?.Hunts is "monsters" or "undead";

    internal void LordThink(Party p)
    {
        var home = SettlementById(p.HomeId);
        float me = Rules.Strength(p);
        var hostiles = Visible(p, B.Vision).Where(q => Hostile(p, q)).ToList();
        // Traits drive the odds a lord takes (3.1): the Reckless stand against worse, the Cautious run sooner and hunt only easy prey.
        var who = CharacterOf(p);
        float runAt = who?.Traits.Contains("Reckless") == true ? 1.7f : who?.Traits.Contains("Cautious") == true ? 1.1f : 1.3f;
        float preyAt = who?.Traits.Contains("Reckless") == true ? 1.25f : who?.Traits.Contains("Cautious") == true ? 0.75f : 1f;
        var threat = hostiles.Where(q => !q.Lair && Rules.Strength(q) > me * runAt && Vector2.Distance(q.Pos, p.Pos) < 25).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault();
        // Go home to refit when the company is thin, or to spend an upgrade it can already pay for. Refit buys what the
        // purse allows and the faction treasury covers the rest, so a lord who has lost men goes home even when it is poor.
        bool needsRefit = p.Men < Scaled(45) || p.Companies.Count < 3
            || p.Companies.Sum(c => c.Ready) >= Scaled(6) && p.Gold >= 150;
        if (threat != null)
        {
            if (p.SiegeTarget >= 0) Log($"{p.Name} abandons the siege of {SettlementById(p.SiegeTarget).Name}.");
            p.SiegeTarget = -1;
            p.Task = "flee"; p.ChaseId = -1; p.Goal = home.Pos; p.Moving = true; p.TaskUntil = (float)S.Hours + 3;
        }
        else if (!needsRefit && OrderMarches(p)) { } // a crusade, or riding with you as its grand master (K6)
        else if (p.SiegeTarget >= 0)
        {
            p.Task = "siege"; p.ChaseId = -1; p.Moving = false; p.TaskUntil = (float)S.Hours + 4;
        }
        else if (p.Order == "follow" && IsVassal(p) && p.Men >= Scaled(15) && Vector2.Distance(p.Pos, Player.Pos) > 6) // follows unless nearly broken
        {
            p.Task = "follow"; p.ChaseId = -1; p.Goal = Player.Pos + new Vector2(-3, 3); p.Moving = true; p.TaskUntil = (float)S.Hours + 1;
        }
        else if (!needsRefit && TryBesiege(p)) { }
        else if (needsRefit || p.Task == "refit")
        {
            p.Task = "refit"; p.ChaseId = -1; p.Goal = home.Pos; p.Moving = true;
            if (Vector2.Distance(p.Pos, home.Pos) < 2 * Map.Spread) { Refit(p, home); p.Task = "rest"; p.TaskUntil = (float)S.Hours + 12; }
        }
        // A holdout is dug in: a lord goes after one only when its raiders have plundered his realm, and he is clearly the stronger.
        // A realm that hunts monsters (the Order, W5) digs out any holdout it outweighs and goes after beasts before men.
        else if (hostiles.Where(q => !q.Lair || (q.HuntedBy == p.Faction && S.Hours < q.HuntedUntil || MonsterHunter(p)) && Rules.Strength(q) * B.HoldoutDefence * 1.2f < me * preyAt)
                     .Where(q => Rules.Strength(q) * (q.Breed is "troll" or "undead" ? 2 : 1) < me * preyAt * (p.PatrolProvince >= 0 && S.Provinces[p.PatrolProvince].Faction != p.Faction ? 1.3f : 1f)) // a troll only with twice its weight
                     .OrderBy(q => MonsterHunter(p) && q.Kind == PartyKind.Monster ? 0 : 1).ThenBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey) // a lord on campaign takes even fights
        {
            p.Task = "hunt"; p.ChaseId = prey.Id; p.Moving = true; p.TaskUntil = (float)S.Hours + 6;
        }
        else
        {
            p.Task = "patrol"; p.ChaseId = -1;
            var prov = p.PatrolProvince >= 0 ? S.Provinces[p.PatrolProvince] : S.Provinces[home.Province];
            var ordered = OrderedProvince(p); // your vassals keep to their standing orders (F6)
            if (ordered != null) prov = ordered;
            // A lord with no enemy in sight marches for the front rather than idling around home, so armies meet on their own.
            else if (AtWarWithAnyone(p.Faction) && rng.Chance(MathF.Min(0.95f, B.LordAggression * Factions.Get(p.Faction).Aggression)) && EnemyProvinceNear(prov, p.Faction) is Province front) prov = front;
            p.Goal = prov.Center + new Vector2(rng.Range(-20, 20), rng.Range(-20, 20)) * Map.Spread;
            p.Moving = true;
            p.TaskUntil = (float)S.Hours + 10;
            // A lord in an enemy province threatens its villages (they stop giving recruits for a week).
            if (FactionsAtWar(p.Faction, prov.Faction))
                foreach (var v in S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.Province == prov.Id && Vector2.Distance(v.Pos, p.Pos) < 12 * Map.Spread && v.ThreatUntil < S.Hours))
                {
                    v.ThreatUntil = (float)S.Hours + 7 * 24;
                    v.Recruits = 0;
                    Raided(v);
                    Score(p.Faction, prov.Faction, 20);
                    Log($"{p.Name} threatens {v.Name}.");
                }
        }
    }

    /// The nearest province held by a faction `f` is at war with, reachable through `from` (or `from` itself when it is enemy ground).
    Province? EnemyProvinceNear(Province from, int f)
    {
        if (FactionsAtWar(f, from.Faction) && from.Faction >= 0) return from;
        var enemy = S.Provinces.Where(p => FactionsAtWar(f, p.Faction) && p.Neighbors.Contains(from.Id)).OrderBy(p => Vector2.Distance(p.Center, from.Center)).FirstOrDefault();
        if (enemy != null) return enemy;
        // Two hops out, so a lord behind its own border still finds a front to march to.
        return S.Provinces.Where(p => FactionsAtWar(f, p.Faction) && p.Neighbors.Any(n => S.Provinces[n].Neighbors.Contains(from.Id)))
            .OrderBy(p => Vector2.Distance(p.Center, from.Center)).FirstOrDefault();
    }

    /// At home: recruit levies, upgrade companies that are ready, by the same rules and prices as yours. A lord who cannot
    /// pay draws on his faction's war chest, up to a share of it per visit, so a beaten lord rebuilds instead of dying broke
    /// while the chest still has money to raise new lords.
    void Refit(Party p, Settlement home)
    {
        var fs = S.Factions[Math.Max(0, p.Faction)];
        // A beaten lord may draw on the war chest, but only his share of it: half the chest is split between the realm's
        // lords, so a run of refits costs the realm at most half of what it holds and it can still raise new lords (600).
        int lordsOfFaction = Math.Max(1, S.Parties.Count(q => q.Kind == PartyKind.Lord && q.Faction == p.Faction));
        float share = MathF.Min(250 * Map.PartyScale, fs.Treasury * 0.5f / lordsOfFaction);
        float budget = Math.Clamp(share, 0, 250 * Map.PartyScale);
        string levy = RecruitTroop(p.Faction);
        if (p.Companies.Count == 0) p.Companies.Add(NewCompany(levy, "infantry", Scaled(12), 0, home)); // the lord's household, raised again
        if (p.KnightOrder != "") RefitOrder(p, home); // a knight order's own brothers come from its chapter
        foreach (var v in S.Settlements.Where(v => v.Province == home.Province))
        {
            int affordable = (int)((p.Gold + budget) / Rules.PerMan(10));
            int n = Math.Min(v.Recruits, Math.Max(0, affordable));
            if (n <= 0) continue;
            levy = RecruitTroop(v); // each village raises its own culture's men (4.2)
            // Recruits fill the green companies; a lord keeps his veterans' companies for promoted men.
            var into = p.Companies.Where(c => c.Type.Tier <= 1 && Rules.Room(c, levy) > 0).OrderByDescending(c => Rules.Room(c, levy)).FirstOrDefault();
            if (into == null && p.Companies.Count < 5) p.Companies.Add(into = NewCompany(levy, rng.Pick(new[] { "infantry", "spears", "archers" }), 0, 0, v));
            if (into == null) break;
            n = Math.Min(n, Rules.Room(into, levy));
            if (n <= 0) break;
            Enlist(into, levy, n, v);
            v.Recruits -= n;
            int bill = (int)MathF.Ceiling(n * Rules.PerMan(10));
            int fromChest = Math.Min(bill, (int)budget);
            fs.Treasury -= fromChest;
            budget -= fromChest;
            p.Gold -= bill - fromChest;
        }
        // Promotions come from the lord's own purse; the chest only tops up a lord who came home beaten (thin), or a run of
        // refits for a few ready men would bleed the realm dry.
        if (p.Men >= Scaled(45) && p.Companies.Count >= 3) budget = 0;
        PromoteAll(p, ref budget);
        if (p.Companies.Count > 0) p.Tier = Math.Clamp((int)MathF.Round((float)p.Companies.Average(c => c.Type.Tier)), 1, 7);
    }

    /// How much bigger brigands think a party is: an Aura Master's name goes before him (MA8), so they flee one up to half again their weight.
    public const float AuraDread = 1.5f;
    internal float Dread(Party q) => q.Kind == PartyKind.Player && Captain.AuraTier >= 4 ? AuraDread : 1;

    void BanditThink(Party p)
    {
        float me = Rules.Strength(p);
        var near = Visible(p, 25).Where(q => Hostile(p, q)).ToList();
        // Brigands are opportunists, not soldiers: they run from anything near their own weight and only fall on a
        // clearly weaker party. With many more bands on the big map they would otherwise grind the lords to nothing.
        if (near.FirstOrDefault(q => !q.Lair && Rules.Strength(q) * Dread(q) > me * 1.05f) is Party threat)
        {
            var away = p.Pos - threat.Pos;
            p.Goal = p.Pos + (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 20 * Map.Spread;
            p.ChaseId = -1; p.Moving = true; p.TaskUntil = (float)S.Hours + 2;
        }
        else if (near.Where(q => !q.Lair && q.Breed is not ("troll" or "undead") && Rules.Strength(q) * Dread(q) < me * 0.9f).OrderBy(q => Vector2.Distance(q.Pos, p.Pos)).FirstOrDefault() is Party prey)
        {
            p.ChaseId = prey.Id; p.Moving = true; p.TaskUntil = (float)S.Hours + 4;
        }
        else
        {
            p.ChaseId = -1;
            p.Goal = p.Pos + new Vector2(rng.Range(-25, 25), rng.Range(-25, 25)) * Map.Spread;
            p.Moving = true; p.TaskUntil = (float)S.Hours + 8;
        }
    }

    /// A faction lives while it holds a town or castle.
    public bool Alive(int f) => S.Settlements.Any(s => s.Faction == f && s.Kind != SettlementKind.Village);

    void FactionAI()
    {
        for (int f = 0; f < S.Factions.Count; f++)
        {
            var fs = S.Factions[f];
            if (!Alive(f)) continue;
            float income = S.Settlements.Where(t => t.Faction == f && t.Owner != Player.Id).Sum(Income);
            fs.Treasury += income / 7;
            var lords = S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == f).ToList();
            int orderArmies = lords.Count(p => p.KnightOrder != ""); // the knight orders' armies ride with the realm but are not its lords
            // A realm raises lords with the ground it holds: more provinces, more armies, but never more than the seats (and
            // their villages' recruits) can actually pay for, or the chest is bled dry fielding companies that starve.
            int seatsHeld = S.Settlements.Count(s => s.Faction == f && s.Kind != SettlementKind.Village && s.Owner != Player.Id);
            int affordable = Math.Max(1, seatsHeld * 2 + 2);
            int wanted = Math.Min(LordsWanted, affordable);
            // Keep a reserve in the chest before a new lord is raised, so a run of defeats does not leave the realm broke.
            if (lords.Count - orderArmies < wanted && fs.Treasury >= 600 + 400) { SpawnLord(f); fs.Treasury -= 600; }
            var front = S.Provinces.Where(p => p.Faction == f && p.Neighbors.Any(n => FactionsAtWar(f, S.Provinces[n].Faction))).ToList();
            var enemyFront = S.Provinces.Where(p => FactionsAtWar(f, p.Faction) && p.Neighbors.Any(n => S.Provinces[n].Faction == f)).ToList();
            var idle = lords.Where(l => !l.Down && l.Task == "patrol" && !(IsVassal(l) && l.Order is "defend" or "follow")).ToList();
            // The marshal's objective (F5) draws the host; without one the realm picks a province on the front.
            // An ally's host marches on your objective when it has none of its own (joint hosts, F7).
            var objective = ObjectiveOf(f) ?? S.Pacts.Where(p => p.Until > S.Hours && (p.A == f || p.B == f)).Select(p => ObjectiveOf(p.A == f ? p.B : p.A))
                .FirstOrDefault(o => o != null && FactionsAtWar(f, o.Faction));
            if (idle.Count >= 2 && (objective != null || enemyFront.Count > 0) && rng.Chance(objective != null ? 0.5f : 0.35f))
            {
                var target = objective != null ? S.Provinces[objective.Province] : rng.Pick(enemyFront);
                MusterHost(f, target, idle);
            }
            else
                foreach (var l in idle.Where(l => l.PatrolProvince < 0 || rng.Chance(0.15f)))
                    l.PatrolProvince = front.Count > 0 ? rng.Pick(front).Id : S.Settlements[l.HomeId].Province;
        }
    }
}
