using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of spells and mage companies (docs/COMPANIONS_MAGIC.md E1-E5; Abilities.cs, BattleAbilities.cs).
public static partial class SelfTest
{
    /// The spell data is sound and its icons can be drawn; every caster's spells exist.
    static void AbilitiesValidate()
    {
        Check(!Abilities.Validate().Any(), "abilities.json validates: " + string.Join("; ", Abilities.Validate()));
        Check(Abilities.All.All(a => Kit.IconNames.Contains(a.Icon)), "every spell's icon is one the kit can draw");
        Check(Troops.All.Where(t => t.Class == UnitClass.Caster).All(t => t.Spells.All(Abilities.Has)), "every caster's spells are in the data");
    }

    /// E1, D0: every mage line climbs: each tier knows all its parent's spells and more, the top tier a great working, and
    /// its spells land harder; every culture has a line, reached from Acolytes.
    static void MagesByTier()
    {
        var mages = Troops.All.Where(t => t.Class == UnitClass.Caster).ToList();
        foreach (var t in mages)
            foreach (var n in t.UpgradesTo.Select(Troops.Get))
            {
                Check(t.Spells.All(n.Spells.Contains) && n.Spells.Length > t.Spells.Length, $"{n.Name} know every spell {t.Name} do, and more");
                Check(n.MaxHp > t.MaxHp && n.Armor >= t.Armor, $"{n.Name} are tougher than {t.Name}");
            }
        Check(mages.Where(t => t.Tier == 6).All(t => t.Spells.Any(id => Abilities.Get(id).Great)), "every tier VI mage knows a great working");
        Check(mages.Where(t => t.Tier < 6).All(t => !t.Spells.Any(id => Abilities.Get(id).Great)), "and none below");
        foreach (var c in Factions.All.Select(f => f.Culture).Distinct())
            Check(Troops.Get("acolytes").UpgradesTo.Any(id => Troops.Get(id).Culture == c), $"Acolytes can rise into the {c} mage line");
        Check(!Troops.Get("levies").UpgradesTo.Contains("acolytes"), "levies cannot become Acolytes: mages are hired, rare");
        Console.WriteLine($"magic: {mages.Count} mage troops, {Abilities.All.Count} spells and abilities");
    }

    /// A mage company of `troop` at the origin and a static levy company 25 m in front (both holding); (battle, mages, foes).
    static (Battle b, Squad mages, Squad foes) MageScene(string troop, int seed = 71, int foes = 12, BattleOptions? options = null)
    {
        var b = options == null ? Battle.Empty(seed) : Battle.Empty(seed, options);
        var m = b.AddSquad(0, Troops.Get(troop), 3, new(0, 0), 3, Vector2.UnitY); // a circle: mages are few
        var f = b.AddSquad(1, Troops.Get("levies"), foes, new(0, 25), 6, -Vector2.UnitY);
        m.Order = f.Order = Order.Hold;
        return (b, m, f);
    }

    /// E3, E4: an attack spell glows first; a company that steps off the glow takes nothing, one that stays is struck.
    static void SpellWarns()
    {
        float Hurt(bool step)
        {
            var (b, m, f) = MageScene("battle_priests");
            for (int i = 0; i < 20 * 60 && !b.Casts.Any(c => c.A.Id == "smite"); i++) b.Update(1 / 60f);
            var cast = b.Casts.FirstOrDefault(c => c.A.Id == "smite");
            Check(cast != null && cast.FireAt > b.Time + 0.5f, "a smite shows its glow before it falls");
            if (cast == null) return 0;
            if (step) { f.Anchor += new Vector2(25, 0); foreach (var u in f.Units) u.Pos += new Vector2(25, 0); } // they see the light gathering and move off
            float hp = f.Units.Sum(u => u.Hp);
            Run(b, 3);
            return hp - f.Units.Sum(u => u.Hp);
        }
        float stayed = Hurt(false), moved = Hurt(true);
        Console.WriteLine($"magic: a smite takes {stayed:0} hp from a company that stays under it, {moved:0} from one that steps off");
        Check(stayed > 20 && moved < 1, "a smite strikes those under the glow, not those who moved off it");
    }

    /// E2: casting spends Will; it comes back; a company under 20 Will casts nothing; a mage with a blade at him does not cast.
    static void MageWill()
    {
        var (b, m, f) = MageScene("battle_priests");
        Run(b, 12);
        Check(m.Will < 100, $"casting spends Will ({m.Will:0} left)");
        float after = m.Will;
        foreach (var u in f.Units) u.Pos += new Vector2(400, 0); // nothing left to cast at
        Run(b, 6);
        Check(m.Will > after, "Will comes back while they rest");

        (b, m, f) = MageScene("battle_priests");
        m.Will = 0;
        Run(b, 3);
        Check(b.Casts.Count == 0 && m.Will < 20, "a drained company cannot cast");

        (b, m, f) = MageScene("battle_priests");
        var guard = b.AddSquad(1, Troops.Get("ashen_saints"), m.Units.Count, new(0, 1.2f), m.Units.Count, -Vector2.UnitY); // mages kill levies now: tier VII at every throat
        guard.Order = Order.Hold;
        foreach (var (u, i) in m.Units.Select((u, i) => (u, i))) guard.Units[i].Pos = u.Pos + new Vector2(0, 1.2f);
        Run(b, 3);
        Check(!b.Casts.Any(c => c.By == m) && m.Will >= 100 - 1e-3f, "a mage in melee does not cast");
    }

    /// E3: a Blessing reaches your companies near the caster as they close, and ends after its time.
    static void BlessingBuffs()
    {
        var b = Battle.Empty(72);
        var m = b.AddSquad(0, Troops.Get("chaplains"), 8, new(0, -6), 4, Vector2.UnitY);
        var inf = b.AddSquad(0, Troops.Get("shieldmen"), 12, new(0, 4), 6, Vector2.UnitY);
        var far = b.AddSquad(0, Troops.Get("shieldmen"), 12, new(90, 4), 6, Vector2.UnitY);
        var foe = b.AddSquad(1, Troops.Get("shieldmen"), 12, new(0, 26), 6, -Vector2.UnitY);
        foreach (var s in b.Squads) s.Order = Order.Hold;
        for (int i = 0; i < 20 * 60 && !inf.Timed.Any(t => t.Effect.Id == "blessed"); i++) b.Update(1 / 60f);
        Check(inf.Timed.Any(t => t.Effect.Id == "blessed") && inf.Units.All(u => u.Mods.Shock < 0), "a Blessing falls on the company near the chaplains as the foe comes close");
        Check(!far.Timed.Any() && far.Units.All(u => u.Mods.Shock == 0), "but not on one far off");
        Check(foe.Units.All(u => u.Mods.Shock == 0), "nor on the enemy");
        Run(b, 21);
        foreach (var u in m.Units) b.Kill(u, foe.Units[0]); // no second blessing
        Run(b, 22);
        Check(!inf.Timed.Any(t => t.Effect.Id == "blessed") && inf.Units.All(u => u.Mods.Shock == 0), "and it fades after its time");
    }

    /// C3/E3 fields: arrows into mist lose their men; a ward burns the dead inside it but not men.
    static void MistCoversArrows()
    {
        int Landed(bool mist)
        {
            var (b, a, _) = StyleScene("bowmen");
            var t = b.AddSquad(1, Troops.Get("shieldmen"), 1, new(0, 20), 1, Vector2.UnitY).Units[0];
            b.Update(0);
            if (mist) b.AddField(Abilities.Get("mist"), "mist", 1, t, t.Pos, 15, 60, 1, 0);
            b.Update(0.5f / 60); // its totals
            for (int k = 0; k < 30; k++) b.Update(1 / 60f);
            int got = 0;
            for (int i = 0; i < 200; i++) { t.Pos = new(0, 20); t.Hp = t.Type.MaxHp; t.Action = UnitAction.None; b.Hit(a, t, 1, true); if (t.Hp < t.Type.MaxHp) got++; }
            return got;
        }
        int clear = Landed(false), misted = Landed(true);
        Check(misted < clear * 0.7f, $"arrows into mist lose their men ({misted} of 200 land, {clear} in the clear)");

        var w = Battle.Empty(73);
        var priests = w.AddSquad(0, Troops.Get("battle_priests"), 4, new(0, -20), 4, Vector2.UnitY);
        var men = w.AddSquad(0, Troops.Get("shieldmen"), 6, new(-3, 0), 3, Vector2.UnitY);
        var dead = w.AddSquad(1, Troops.Get("skeleton"), 6, new(3, 0), 3, Vector2.UnitY);
        foreach (var s in w.Squads) s.Order = Order.Hold;
        w.AddField(Abilities.Get("ward"), "warded", 0, priests.Units[0], Vector2.Zero, 12, 30, 1, 6);
        float menHp = men.Units.Sum(u => u.Hp), deadHp = dead.Units.Sum(u => u.Hp);
        for (int i = 0; i < 3 * 60; i++) { w.Update(1 / 60f); foreach (var (u, k) in men.Units.Concat(dead.Units).Select((u, k) => (u, k))) { u.Pos = new(-6 + (k % 6) * 2, k < 6 ? -4 : 4); u.Vel = Vector2.Zero; } }
        Check(dead.Units.Sum(u => u.Hp) < deadHp - 10 && Math.Abs(men.Units.Sum(u => u.Hp) - menHp) < 0.01f, "a ward burns the dead inside it, not your men");
    }

    /// E4: wildfire needs dry ground and no rain; lightning needs rain or a storm called over the spot.
    static void WildfireNeedsDry()
    {
        var (dry, _, _) = MageScene("hedge_witches");
        var (wet, _, _) = MageScene("hedge_witches", options: new BattleOptions { Weather = new Weather(Rain: 0.8f) });
        Check(dry.Dry(new(0, 25)) && !wet.Dry(new(0, 25)), "dry meadow burns, not in rain");
        // Crones know wildfire: in rain they never light it; in the dry they do.
        bool Lit(BattleOptions? o)
        {
            var (b, _, _) = MageScene("crimson_crones", options: o);
            for (int i = 0; i < 40 * 60; i++) { b.Update(1 / 60f); if (b.Fields.Any(f => f.A.Id == "wildfire")) return true; }
            return false;
        }
        Check(Lit(null), "crones set dry grass alight under the enemy");
        Check(!Lit(new BattleOptions { Weather = new Weather(Rain: 0.8f) }), "but not in rain");
        bool Struck(BattleOptions? o)
        {
            var (b, _, _) = MageScene("galdr_masters", options: o);
            for (int i = 0; i < 30 * 60; i++) { b.Update(1 / 60f); if (b.Casts.Any(c => c.A.Id == "lightning")) return true; }
            return false;
        }
        Check(Struck(new BattleOptions { Weather = new Weather(Rain: 0.8f) }), "galdr-masters call lightning in rain");
    }

    /// E4: horse that reach mages win, so horse hunt them first; casters' craft adds Omen; the great working comes once.
    static void HorseHuntsMages()
    {
        var b = Battle.Empty(74);
        var horse = b.AddSquad(0, Troops.Get("lancers"), 8, new(0, 0), 4, Vector2.UnitY);
        var foot = b.AddSquad(1, Troops.Get("shieldmen"), 12, new(-20, 40), 6, -Vector2.UnitY);
        var mages = b.AddSquad(1, Troops.Get("magisters"), 8, new(30, 70), 4, -Vector2.UnitY);
        b.AddSquad(0, Troops.Get("shieldmen"), 12, new(-20, 38), 6, Vector2.UnitY);
        Run(b, 0.5f);
        Check(b.HorsePrey(horse) == mages, "horse go for the enemy's mages first");

        var (c, m, f) = MageScene("crimson_crones");
        Run(c, 30);
        Check(c.OmenRaised > 0, $"craft raises Omen ({c.OmenRaised:0.0})");
        foreach (var u in f.Units) u.Morale = 30; // a losing day: the great working
        var g = Battle.Empty(75);
        var gm = g.AddSquad(0, Troops.Get("saints_militant"), 8, new(0, 0), 4, Vector2.UnitY);
        var gs = g.AddSquad(0, Troops.Get("shieldmen"), 6, new(0, 10), 3, Vector2.UnitY);
        g.AddSquad(1, Troops.Get("shieldmen"), 30, new(0, 30), 10, -Vector2.UnitY);
        foreach (var u in gs.Units) u.Morale = 25;
        Run(g, 10);
        Check(gm.GreatDone && gs.Timed.Any(t => t.Effect.Id == "miracle"), "Saints Militant call a Miracle when the day goes against them");
    }

    /// D0 for mages: a company of foot fights better with mages behind it than with as many levies standing there.
    static void MagesHelp()
    {
        foreach (var mage in new[] { "battle_priests", "skalds", "war_witches", "alchemists" })
        {
            float Score(string behind)
            {
                float score = 0;
                for (int seed = 1; seed <= 3; seed++)
                {
                    var b = Battle.Empty(980 + seed);
                    var sa = b.AddSquad(0, Troops.Get("men_at_arms"), 20, new(0, -10), 10, Vector2.UnitY);
                    var back = b.AddSquad(0, Troops.Get(behind), behind == "levies" ? 8 : 3, new(0, -22), 4, Vector2.UnitY);
                    var sb = b.AddSquad(1, Troops.Get("men_at_arms"), 24, new(0, 14), 12, -Vector2.UnitY);
                    sa.Order = sb.Order = Order.Charge;
                    back.Order = Order.Hold;
                    sa.Target = sb; sb.Target = sa;
                    for (int i = 0; i < 40 * 60; i++) b.Update(1 / 60f);
                    score += Standing(sa) + sb.StartCount - Standing(sb);
                }
                return score;
            }
            float with = Score(mage), without = Score("levies");
            Console.WriteLine($"magic: 20 men-at-arms against 24 with three {Troops.Get(mage).Name} behind: {with:0}; with eight levies there: {without:0}");
            Check(with > without, $"a circle of three {Troops.Get(mage).Name} behind the line helps more than eight levies");
        }
    }
}

/// Campaign side of effects and magic (docs/COMPANIONS_MAGIC.md A5, E1).
public static partial class SelfTest
{
    /// A5: a buff laid on the map rides into the next battle on every company of yours, ends after its days, and a feast
    /// costs gold and cannot be stacked; a town near a shrine offers Acolytes.
    static void StandingEffectsCarry()
    {
        var c = Campaign.New(52);
        c.AddBuff("shrine_blessing", 2);
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        bandit.Pos = c.Player.Pos;
        c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        Check(setup.Options.Standing.Contains("shrine_blessing"), "a shrine's blessing goes into the battle");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        Check(b.Squads.Where(s => s.Team == 0 && s.Units.Count > 0).All(s => s.Units.All(u => u.Mods.Shock < 0)) && b.Squads.Where(s => s.Team == 1).All(s => s.Units.All(u => u.Mods.Shock == 0)),
            "every company of yours carries it, and no enemy");
        Check(c.BuffEdge > 1, $"it counts in an auto-resolve too ({c.BuffEdge:0.00})");
        c.Encounter = null;
        c.S.Parties.Remove(bandit);
        double until = c.S.Hours + 3 * 24;
        for (int h = 0; h < 30 * 24 && c.S.Hours < until; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; }
        Check(!c.HasBuff("shrine_blessing"), "and it ends after its days");

        c.AddBuff("hungry", 1);
        Check(c.BuffEdge < 1, "hunger weakens an auto-resolve");
        var town = c.S.Settlements.First(s => { c.Player.Pos = s.Pos; return c.InTown(c.Player); }); // a town that lets you in
        c.S.Gold = c.FeastPrice + 10;
        Check(c.Feast(town) == null && c.HasBuff("feasted") && c.S.Gold == 10, "a feast in town costs its price and feeds the men");
        Check(c.Feast(town) != null, "but one feast at a time");
        Check(Troops.Get("acolytes").Class == UnitClass.Caster && Rules.TroopRole("acolytes") == "casters", "Acolytes are a mage company");
    }

    /// Camp rites need someone who can work them, cost what they cost, one a day; craft draws the Omen and dark omens follow;
    /// the relic goes into battle while it has charges.
    static void RitesWork()
    {
        var c = Campaign.New(53);
        foreach (var k in c.Companions.ToList()) k.Calling = "Sword";
        c.S.Gold = 500;
        Check(c.Rite("bless") != null, "no priest, no blessing");
        var co = c.Player.Companies.First(x => x.Men > 0);
        co.Stacks.Add(new TroopStack { Troop = "chaplains", Men = 5 });
        Check(c.CanWork("faith") && !c.CanWork("craft"), "a company of chaplains can work faith, not craft");
        Check(c.Rite("bless") == null && c.HasBuff("bless_rite") && c.S.Gold == 450, "the blessing costs 50 and lays its buff");
        Check(c.Rite("poultices") == "ONE RITE A DAY", "one rite a day");
        Check(c.Rite("bones") != null, "chaplains cannot read the bones");
        co.Stacks.Add(new TroopStack { Troop = "hedge_witches", Men = 5 });
        c.S.RiteDay = 0;
        float omen = c.S.Omen;
        Check(c.Rite("bones") == null && c.S.FarSightUntil > c.S.Hours && c.S.Omen == omen + 3, "the bones show far and draw the Omen");
        c.S.Omen = Campaign.OmenEvents;
        Check(Campaign.Events.Count(e => e.Trigger == "dark_omens") >= 3, "dark omens have events");

        c.S.Relic = true; c.S.RelicCharges = 1;
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        bandit.Pos = c.Player.Pos; c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        Check(setup.Options.Standing.Contains("relic"), "the relic goes into battle");
        Check(Troops.Get("necromancer").Spells.Contains("dread"), "the necromancer casts dread");
    }

    /// Mages are super units: a circle of a few, each worth more than any soldier of his tier; one circle a captain; hired, rarely.
    static void SuperMages()
    {
        foreach (var t in Troops.All.Where(t => t.Class == UnitClass.Caster))
        {
            int n = Rules.CapOf(t.Id, "casters");
            Check(n is >= 1 and <= 4, $"a circle of {t.Name} is a few men ({n})");
            float best = Troops.All.Where(o => o.Tier == t.Tier && o.Class != UnitClass.Caster && !o.Wild && !o.Leader).Select(o => Rules.Strength(o, 1)).DefaultIfEmpty(0).Max();
            Check(Rules.Strength(t, 1) > best, $"one {t.Name} is worth more than any soldier of tier {Style.Numerals[t.Tier]}");
        }
        var c = Campaign.New(56);
        c.S.Gold = 100000;
        var town = c.S.Settlements.First(s => { c.Player.Pos = s.Pos; return c.InTown(c.Player); });
        CompanyRecord Circle() { var m = c.NewCompany("chaplains", "casters", Rules.CapOf("chaplains", "casters"), 0); town.Mercenaries.Add(m); return m; }
        Check(c.Hire(town, Circle()) == null && c.HasCircle, "a captain can hire a circle");
        Check(c.Hire(town, Circle()) == Campaign.OneCircle, "but only one");
        Check(c.Split(c.Player.Companies.First(x => Rules.RoleOf(x) == "casters")) == Campaign.OneCircle, "and cannot split it in two");
    }

    /// MA1 (docs/MAGIC_AURA.md 3.1): a circle holding its spells casts nothing on its own; an ordered spell is cast on your spot,
    /// still warns first, and costs more Will; out of range the circle closes in; a spell that needs rain is given up in the dry.
    static void CastOnOrder()
    {
        var (b, m, f) = MageScene("battle_priests");
        m.HoldSpells = true;
        Run(b, 10);
        Check(b.Casts.Count == 0 && m.Will >= 99, "a circle that holds its spells casts nothing on its own");
        int smite = Array.FindIndex(m.Spells, a => a.Id == "smite");
        var spot = f.Center + new Vector2(2, 0);
        m.CastSpell = smite; m.CastAt = spot; m.CastSince = b.Time;
        float will = m.Will;
        for (int i = 0; i < 3 * 60 && b.Casts.Count == 0; i++) b.Update(1 / 60f);
        var c = b.Casts.FirstOrDefault();
        Check(c != null && c.A.Id == "smite" && Vector2.Distance(c.At, spot) < 0.01f, "the ordered spell is cast on your spot");
        Check(c != null && c.FireAt > b.Time + 0.5f, "an ordered attack still glows before it falls");
        Check(will - m.Will >= m.Spells[smite].Will + Battle.OrderedWill - 1, $"an ordered spell costs more Will ({will - m.Will:0} spent)");
        Check(m.CastSpell == -1, "the order is done once cast");
        float hp = f.Units.Sum(u => u.Hp);
        Run(b, 3);
        Check(f.Units.Sum(u => u.Hp) < hp - 10, "the ordered smite strikes the men under it");

        (b, m, f) = MageScene("battle_priests");
        m.HoldSpells = true;
        Run(b, 8);
        smite = Array.FindIndex(m.Spells, a => a.Id == "smite");
        var far = new Vector2(0, 120);
        m.CastSpell = smite; m.CastAt = far; m.CastSince = b.Time;
        float before = Vector2.Distance(m.Anchor, far);
        Run(b, 1);
        Check(Vector2.Distance(m.Anchor, far) < before - 20 && b.Casts.Count == 0, "out of range, the circle closes in before it casts");

        (b, m, f) = MageScene("skalds");
        m.HoldSpells = true;
        Run(b, 8);
        var (b2, m2, f2) = (b, m, f);
        m2.CastSpell = 99; m2.CastSince = b2.Time;
        Run(b2, 1);
        Check(m2.CastSpell == -1 && b2.Casts.Count == 0, "an order for a spell the circle does not know is given up");
        m2.CastSpell = Array.FindIndex(m2.Spells, a => a.Id == "storm"); m2.CastAt = f2.Center; m2.CastSince = b2.Time - Battle.OrderLasts - 1;
        Run(b2, 1);
        Check(m2.CastSpell == -1, "an order that could not be done in time lapses");
    }
}
