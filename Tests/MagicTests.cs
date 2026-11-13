using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of spells and mage companies (docs/archive/COMPANIONS_MAGIC.md E1-E5; Abilities.cs, BattleAbilities.cs).
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

/// Campaign side of effects and magic (docs/archive/COMPANIONS_MAGIC.md A5, E1).
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

    /// MA1 (docs/archive/MAGIC_AURA.md 3.1): a circle holding its spells casts nothing on its own; an ordered spell is cast on your spot,
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

    /// A campaign with a circle of chaplains and a priest to teach them; a bandit stands ready to fight.
    static (Campaign c, CompanyRecord circle) CircleCampaign(int seed)
    {
        var c = Campaign.New(seed);
        var circle = c.NewCompany("chaplains", "casters", Rules.CapOf("chaplains", "casters"), 0);
        c.Player.Companies.Add(circle);
        c.S.Characters.Add(new Character { Id = c.S.NextId++, Kind = "companion", Name = "Brother Test", Culture = "common", Calling = "Priest", PartyId = c.Player.Id, CompanyId = circle.Id });
        c.S.Gold = 10000;
        return (c, circle);
    }

    /// MA4 (docs/archive/MAGIC_AURA.md 3.2): the spells you prepare, and only those, go into battle with the circle.
    static void SpellbookCarries()
    {
        var (c, circle) = CircleCampaign(91);
        var why = c.LearnSpell("smite");
        Check(why == null && Campaign.Known(circle).Contains("smite"), $"the circle learns smite from a priest ({why})");
        Check(c.Prepare("blessing", false) == null && c.Prepare("smite", true) == null, "and prepares it in place of the blessing");
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        bandit.Pos = c.Player.Pos; c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        var co = setup.Player.Companies.First(x => x.Id == circle.Id);
        Check(co.Spells != null && co.Spells.Contains("smite") && !co.Spells.Contains("blessing"), "the prepared spells go with the company");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        var s = b.Squads.First(q => q.Company?.Id == circle.Id);
        Check(s.Spells.Any(a => a.Id == "smite") && !s.Spells.Any(a => a.Id == "blessing"), "and the circle casts them in battle, not what it set aside");
        Check(c.SpellNote(true).Contains("Smite"), "the encounter panel names the prepared spells");
    }

    /// MA4: never more prepared than the tier's slots; learning needs a teacher and crowns, costs more each time, stops at two.
    static void SpellbookByTier()
    {
        var (c, circle) = CircleCampaign(92);
        int slots = Campaign.PrepareSlots(circle);
        Check(slots == Troops.Get("chaplains").Spells.Length, $"chaplains prepare as many as their tier knows ({slots})");
        foreach (var k in c.Companions.ToList()) k.Calling = "Sword";
        c.Player.Pos = c.S.Settlements[0].Pos;
        if (c.SiteAt(c.Player.Pos) == null) Check(c.CannotLearn("smite") != null, "no teacher, no new spell");
        foreach (var k in c.Companions.ToList()) k.Calling = "Priest";
        Check(c.CannotLearn("storm") != null, "a faith circle cannot learn the craft");
        c.S.Gold = 100;
        Check(c.CannotLearn("smite") == $"NEEDS {Campaign.LearnCost} CROWNS", "a spell costs crowns");
        c.S.Gold = 10000;
        Check(c.LearnSpell("smite") == null && c.S.Gold == 10000 - Campaign.LearnCost, "the first costs its price");
        Check(c.LearnSpell("ward") == null && c.S.Gold == 10000 - 3 * Campaign.LearnCost, "the second costs twice as much");
        Check(Campaign.Learnable(circle).Any() ? c.LearnSpell(Campaign.Learnable(circle).First().Id) != null : true, "no more than two beyond the line");
        Check(c.Prepare("smite", true) != null && Campaign.Prepared(circle).Length == slots, "a full spellbook must set one aside first");
        Check(c.Prepare("mend", false) == null && c.Prepare("smite", true) == null && Campaign.Prepared(circle).Length == slots, "then the new one takes its place");
        Check(Campaign.Learnable(circle).All(a => !a.Great), "great workings are never learned");
    }

    /// MA4 (3.3): the circle names a master, who rises with battles; his fall leaves it leaderless (no great working), then another rises.
    static void CircleMaster()
    {
        var (c, circle) = CircleCampaign(93);
        c.CircleDaily();
        var m = Campaign.MasterOf(circle);
        Check(m != null && m.Deeds.Any(d => d.Contains(Campaign.MasterTitle(circle))), "the circle names a master");
        Check(Campaign.MasterRank(m) == 0, "a new master has no rank");
        m!.Battles = Campaign.MasterBattles[2];
        Check(Campaign.MasterRank(m) == 3, "fifteen battles make him a master of rank 3");
        var b = Battle.Empty(94);
        var co = new Company("saints_militant", 3, "casters", circle.Id, MasterRank: 3);
        var s = b.AddSquad(0, Troops.Get("saints_militant"), 3, new(0, 0), 3, Vector2.UnitY, co);
        Check(s.Will > 100 && s.MasterRank == 3, $"his circle gathers more Will ({s.Will:0})");
        circle.Roll.Remove(m);
        c.CircleDaily();
        Check(Campaign.MasterOf(circle) == null && c.Leaderless(circle), "his fall leaves the circle leaderless");
        var lead = new Company("saints_militant", 3, "casters", circle.Id, Leaderless: true);
        var t = b.AddSquad(0, Troops.Get("saints_militant"), 3, new(10, 0), 3, Vector2.UnitY, lead);
        Check(t.GreatDone, "a leaderless circle has no great working");
        c.S.Hours += Campaign.LeaderlessDays * 24 + 1;
        c.CircleDaily();
        Check(Campaign.MasterOf(circle) is Soldier next && next.Id != m.Id, "after a while another rises");
    }

    /// MA8 (docs/archive/MAGIC_AURA.md 3.5): a company inside called mist is seen only close.
    static void MistHides()
    {
        var flat = TerrainParams.Meadow with { Layouts = null, HillsMin = 0, HillsMax = 0, Road = false, Woods = 0, LoneTrees = 0 };
        var none = new ArmySpec("a", 1, new List<Company>());
        var b = new Battle(15, none, none, new BattleOptions { Ground = flat, PlayerWounded = true });
        var hid = b.AddSquad(1, UnitType.Shieldman, 12, new(0, 20), 6, -Vector2.UnitY);
        var eyes = b.AddSquad(0, UnitType.Shieldman, 12, new(0, -40), 6, Vector2.UnitY);
        Run(b, 1.2f);
        Check(b.Visible(hid, 0), "in the open they are seen from afar");
        b.AddField(Abilities.Get("mist"), "mist", 1, hid.Units[0], hid.Center, 15, 60, 1, 0);
        Run(b, 5);
        Check(!b.Visible(hid, 0), "in the mist they are not");
        foreach (var u in eyes.Units) u.Pos += new Vector2(0, 42);
        eyes.Anchor += new Vector2(0, 42);
        Run(b, 1.2f);
        Check(b.Visible(hid, 0), $"close up ({Battle.MistSight:0} m) the mist hides nothing");
    }

    /// MA8: a mirage draws the enemy's arrows from the company it shields.
    static void MirageDrawsArrows()
    {
        int Hurt(bool mirage)
        {
            var b = Battle.Empty(16);
            var bows = b.AddSquad(1, UnitType.Bowman, 12, new(0, 28), 6, -Vector2.UnitY);
            var mark = b.AddSquad(0, UnitType.Shieldman, 12, new(0, 0), 6, Vector2.UnitY);
            bows.Target = mark;
            foreach (var u in mark.Units) u.Hp = 100000;
            if (mirage) b.AddField(Abilities.Get("mirage"), "mirage", 0, mark.Units[0], new Vector2(Battle.MirageOffset, 0), 5, 60, 1, 0);
            Run(b, 15);
            return mark.Units.Sum(u => (int)(100000 - u.Hp));
        }
        int plain = Hurt(false), drawn = Hurt(true);
        Check(drawn < plain * 0.4f, $"a mirage draws the arrows ({drawn} hurt against {plain})");
        Check(Abilities.Get("mirage").Tradition == "craft" && !Abilities.Get("mirage").Great, "a circle of the craft can learn it");
    }

    /// MA8: a Ward breaks an enemy working cast on a spot inside it.
    static void WardBreaksCast()
    {
        bool Fevered(bool ward)
        {
            var b = Battle.Empty(17);
            var men = b.AddSquad(0, Troops.Get("levies"), 8, new(0, 0), 4, Vector2.UnitY);
            var witches = b.AddSquad(1, Troops.Get("war_witches"), 3, new(0, 80), 3, -Vector2.UnitY);
            witches.HoldSpells = true;
            if (ward) b.AddField(Abilities.Get("ward"), "warded", 0, men.Units[0], men.Center, 12, 60, 1, 0);
            b.Casts.Add(new Cast { A = Abilities.Get("fever"), By = witches, At = men.Center, Started = b.Time, FireAt = b.Time, Power = 1 });
            b.Update(1 / 60f);
            return men.Timed.Any(x => x.Effect.Id == "fever");
        }
        Check(Fevered(false) && !Fevered(true), "a Ward breaks the curse cast on the men inside it");
    }

    /// MA8 (3.6): a high Omen stirs the dead sooner and raises more of them, and a third of the villages bar their gates;
    /// a Cleanse in town brings it down.
    static void OmenTeeth()
    {
        var c = Campaign.New(95);
        var village = c.S.Settlements.First(s => s.Kind == SettlementKind.Village && s.Id % 3 == 0);
        c.S.Gold = 10000;
        c.S.Omen = Campaign.OmenShuns;
        Check(c.RecruitLevies(village, 1) == "THE VILLAGE FEARS WHAT YOU WORK: IT BARS ITS GATES", "at a high Omen a village bars its gates");
        Check(c.S.Settlements.Where(s => s.Kind == SettlementKind.Village).Count(c.OmenShunned) * 2 < c.S.Settlements.Count(s => s.Kind == SettlementKind.Village), "but not most of them");
        var grave = new Grave { Pos = village.Pos, Dead = 300, Hours = c.S.Hours };
        c.S.Omen = 0;
        int calm = c.RaiseHost(grave).Companies[0].Men;
        c.S.Omen = Campaign.OmenStirs;
        int stirred = c.RaiseHost(grave).Companies[0].Men;
        Check(stirred > calm, $"a high Omen raises more of the dead ({stirred} against {calm})");
        c.S.Characters.Add(new Character { Id = c.S.NextId++, Kind = "companion", Name = "Sister Test", Culture = "common", Calling = "Priest", PartyId = c.Player.Id, CompanyId = c.Player.Companies[0].Id });
        c.S.Settlements.First(s => { c.Player.Pos = s.Pos; return c.InTown(c.Player); });
        c.S.Omen = 25;
        Check(c.Rite("cleanse") == null && MathF.Abs(c.S.Omen - 15) < 0.01f && c.S.Gold == 9900, "a Cleanse in town takes 10 off the Omen for 100 crowns");
    }

    /// MA8 (4.3): lords of your realm think better of an aura captain; brigands dread an Aura Master.
    static void AuraIsKnown()
    {
        var c = Campaign.New(96);
        var lord = c.S.Characters.First(ch => ch.Kind == "lord" && !ch.Dead);
        c.Captain.Contract = lord.Faction;
        float plain = c.OpinionOfYou(lord);
        c.Captain.AuraTier = 4;
        Check(MathF.Abs(c.OpinionOfYou(lord) - Math.Min(100, plain + 10)) < 0.01f, "a lord of your realm thinks better of an Aura Master");
        Check(c.Dread(c.Player) == Campaign.AuraDread, "brigands dread him");
        c.Captain.AuraTier = 2;
        Check(c.Dread(c.Player) == 1 && MathF.Abs(c.OpinionOfYou(lord) - plain) < 0.01f, "not an Aura Knight");
    }

    /// docs/archive/SPECTACLE.md rule 6: one cinematic beat a battle, first come first served, but a higher kind may take over a
    /// lower one while it runs; the view slows at its height and is back to normal after it.
    static void SpectacleBeat()
    {
        var b = new Beats();
        Check(b.Scale(0) == 1 && !b.Ask(BeatKind.None, Vector2.Zero, 0), "no beat: the view runs at full speed");
        Check(b.Ask(BeatKind.FoeWorking, Vector2.Zero, 1), "the first beat is granted");
        Check(!b.Ask(BeatKind.Charge, Vector2.Zero, 1.2f), "a lower kind cannot cut in");
        Check(b.Ask(BeatKind.Breakthrough, Vector2.One, 1.5f) && b.Kind == BeatKind.Breakthrough, "a higher kind takes over a running beat");
        Check(MathF.Abs(b.Scale(2.5f) - Beats.Slow) < 0.01f, $"the view slows at the beat's height ({b.Scale(2.5f):0.00})");
        Check(b.Scale(10) == 1 && !b.Running(10), "the view is back to normal after the beat");
        Check(!b.Ask(BeatKind.Breakthrough, Vector2.Zero, 20), "only one beat a battle");
        b.Reset();
        Check(b.Ask(BeatKind.Charge, Vector2.Zero, 0), "a new battle has its beat again");
    }

    /// S7 (docs/archive/SPECTACLE.md): a great working the men saw is told in the battle's result (and so the report), once; a
    /// plain spell is not, and each tells which side worked it.
    static void WondersReported()
    {
        var b = Battle.Empty(23);
        var saints = b.AddSquad(0, Troops.Get("saints_militant"), 3, new(0, -10), 3, Vector2.UnitY);
        saints.HoldSpells = true;
        b.Casts.Add(new Cast { A = Abilities.Get("blessing"), By = saints, At = saints.Center, Started = b.Time, FireAt = b.Time, Power = 1 });
        b.Update(1 / 60f);
        Check(b.Result().Wonders.Count == 0, "a plain blessing is no wonder");
        for (int i = 0; i < 2; i++)
        {
            b.Casts.Add(new Cast { A = Abilities.Get("miracle"), By = saints, At = saints.Center, Started = b.Time, FireAt = b.Time, Power = 1 });
            b.Update(1 / 60f);
        }
        var w = b.Result().Wonders;
        Check(w.Count == 1 && w[0].Contains("Miracle") && w[0].Contains("us"), $"a great working is told once, as ours ({string.Join(" / ", w)})");
    }

    /// Mages reach the campaign: towns near a shrine or hermit can offer a circle on the big map, and great lords raise
    /// circles when they refit. Before, neither ever happened in a whole campaign.
    static void MagesInTheWorld()
    {
        var c = Campaign.New(2001);
        var towns = c.S.Settlements.Where(t => t.Kind == SettlementKind.Town).ToList();
        int near = towns.Count(t => c.TideAt(t.Pos).Strength > 0);
        Check(near > 0, "some towns stand where a well reaches");
        var pilot = new Pilot(c);
        int offers = 0, lordCircles = 0;
        for (int week = 0; week < 22; week++)
        {
            pilot.Run(24 * 7);
            offers += c.S.Settlements.Sum(t => t.Mercenaries.Count(m => Rules.RoleOf(m) == "casters"));
            lordCircles = Math.Max(lordCircles, c.S.Parties.Count(p => p.Kind == PartyKind.Lord && p.Companies.Any(k => Rules.RoleOf(k) == "casters")));
        }
        Console.WriteLine($"mages: {near}/{towns.Count} towns where a well reaches; {offers} circle offers seen in 22 weeks; at most {lordCircles} lords with a circle (day {c.Day})");
        Check(offers > 0, "a circle is offered somewhere");
        Check(lordCircles > 0, "a great lord fields a circle");
    }

    /// The arcana census (docs/ARCANA.md AR0) runs, and a seeking stand-in rides to a vigil once its aura stands at a wall.
    static void ArcanaCensus()
    {
        var (p, samples) = BalanceReport.ArcanaRun(4002, true, 90);
        var last = samples.Last();
        Console.WriteLine($"arcana: day {last.Day}, {last.MagicBattles}/{last.Battles} battles with magic, {last.LordCircles}/{last.Lords} lords with a circle, {last.Offers} offers, aura {last.Aura} (kindled day {p.AuraDay[1]}), vigils {last.Vigils}");
        Check(samples.Count == 3 && last.Lords > 0 && last.Battles > 0, "the census samples a living campaign");
        Check(last.Vigils > 0, "a seeker keeps a vigil at its aura's wall");
    }

    /// Wells (docs/ARCANA.md AR2): the map's holy and old places are wells, the same on every load of a seed; the Tide reaches
    /// round them; consecrating or binding one turns and strengthens it, only in your realm and only by those who can.
    static void WellsFromSites()
    {
        var c = Campaign.New(61);
        var wells = c.Sites.Where(x => Campaign.WellTradition(x.Kind) != "").ToList();
        Check(wells.Count > 20 && wells.All(x => c.Well(x).Strength is >= 1 and <= 3), $"holy and old places are wells of strength 1-3 ({wells.Count})");
        Check(c.Sites.Where(x => x.Kind is "mill" or "mine" or "watchtower").All(x => c.Well(x).Strength == 0), "a mill is not a well");
        Check(wells.Where(x => x.Kind is "shrine" or "spring" or "hermit").All(x => c.Well(x).Tradition == "faith")
            && wells.Where(x => x.Kind is "stones" or "ruin" or "battlefield").All(x => c.Well(x).Tradition == "craft"), "shrines are faith, stones craft");
        var strengths = wells.GroupBy(x => c.Well(x).Strength).ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine($"wells: {wells.Count} ({string.Join(", ", strengths.OrderBy(kv => kv.Key).Select(kv => $"{kv.Value} of {kv.Key}"))})");
        Check(strengths.Count == 3 && strengths[1] > strengths[3], "most wells are faint, a few strong");
        var again = Campaign.New(61);
        Check(again.Sites.All(x => again.Well(x) == c.Well(x)), "a seed's wells are the same every time");
        // The tide: full at the well, a step less a little way off, none far away.
        var w = wells.First(x => c.Well(x).Strength >= 2);
        float reach = Campaign.WellReach * c.Map.Spread;
        Check(c.TideAt(w.Pos).Strength >= c.Well(w).Strength, "at a well its tide is full");
        Check(c.TideAt(w.Pos + new Vector2(reach * 2.6f, 0)).Well != w, "far off it is another well's, or none");
        // Marking: stand there, hold the province, have a priest.
        foreach (var k in c.Companions.ToList()) k.Calling = "Sword";
        c.S.Gold = 1000;
        var site = wells.First(x => c.Well(x).Tradition == "craft" && c.Well(x).Strength < 3);
        c.Player.Pos = site.Pos;
        var pr = c.S.Provinces.First(p => p.Id == site.Province);
        Check(c.Rite("consecrate") != null, "no priest, no consecration");
        c.Player.Companies.First(x => x.Men > 0).Stacks.Add(new TroopStack { Troop = "chaplains", Men = 5 });
        c.Captain.Contract = pr.Faction == 0 ? 1 : 0;
        Check(c.Rite("consecrate") == "ONLY IN A PROVINCE OF YOUR REALM", "not in another realm's land");
        c.Captain.Contract = pr.Faction;
        int before = c.Well(site).Strength;
        Check(c.Rite("consecrate") == null && c.Well(site) == ("faith", before + 1) && c.S.Gold == 880, "a stones consecrated turns to the faith, a step stronger, for 120 crowns");
        c.S.RiteDay = 0;
        Check(c.Rite("consecrate") == "THIS WELL IS CONSECRATED ALREADY", "once");
        c.Player.Pos = site.Pos + new Vector2(40 * c.Map.Spread, 0);
        c.S.RiteDay = 0;
        Check(c.Rite("consecrate") != null, "away from a well there is nothing to consecrate");
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        Check(back.Well(back.Sites.First(x => x.Id == site.Id)).Tradition == "faith", "the mark is saved");
    }

    /// The tide brings circles to hire: towns where a well reaches offer them, and more strongly near strong wells.
    static void TideFeedsOffers()
    {
        var c = Campaign.New(2001);
        var towns = c.S.Settlements.Where(t => t.Kind == SettlementKind.Town).ToList();
        int reached = towns.Count(t => c.TideAt(t.Pos).Strength > 0);
        var seen = new HashSet<int>();
        for (int week = 0; week < 7; week++)
        {
            c.Tick(24 * 7); c.Encounter = null; c.Arrived = null;
            foreach (var t in towns.Where(t => t.Mercenaries.Any(m => Rules.RoleOf(m) == "casters"))) seen.Add(t.Id);
        }
        Console.WriteLine($"tide: {reached}/{towns.Count} towns where a well reaches; {seen.Count} offered a circle in 7 weeks");
        Check(reached > towns.Count / 4, "a good share of towns lie where a well reaches");
        Check(seen.Count > 0 && seen.All(id => c.TideAt(towns.First(t => t.Id == id).Pos).Strength > 0 || towns.First(t => t.Id == id).Buildings.GetValueOrDefault("chapel") > 0), "only those (or a chapel town) offer circles");
    }

    /// The Tide (docs/ARCANA.md AR3): one pool both sides draw on. A cast takes its Will from it too (half for the well's own
    /// tradition); with it low the circles wait, but a great working or your order overreaches at twice the Will.
    static void TideShared()
    {
        var (b, m, f) = MageScene("battle_priests");
        Check(b.TideCap == Battle.TideBase * 2 && b.Tide == b.TideCap, "a quick battle's field holds a middling Tide");
        var foe = b.AddSquad(1, Troops.Get("skalds"), 3, new(20, 30), 3, -Vector2.UnitY);
        foe.Order = Order.Hold;
        float tide = b.Tide;
        Run(b, 12);
        Check(b.Casts.Count + b.Fields.Count > 0 || b.Tide < tide, "casting draws the Tide down");
        // A dry field: nobody casts but a great working.
        var (d, dm, df) = MageScene("battle_priests");
        d.Tide = 0;
        float will = dm.Will;
        for (int i = 0; i < 12 * 60; i++) { d.Tide = 0; d.Update(1 / 60f); }
        Check(d.Casts.Count == 0 && dm.Will >= will - 1, "with the Tide dry the circle waits and keeps its Will");
        // The well's own tradition draws half.
        var a = Abilities.Get("smite");
        var holy = Battle.Empty(3, new BattleOptions { Tide = 2, TideTradition = "faith" });
        var dark = Battle.Empty(3, new BattleOptions { Tide = 2, TideTradition = "craft" });
        Check(holy.TideCap == Battle.TideBase * 3 && holy.TideDraw(a, 20) == 10 && dark.TideDraw(a, 20) == 20, "a steady well holds three times the base; its own tradition draws half");
    }

    /// Overreach: an ordered cast with the Tide dry costs twice the Will and may lash back on the circle; never kills.
    static void Overreach()
    {
        int lashed = 0, cast = 0;
        for (int seed = 0; seed < 12; seed++)
        {
            var (b, m, f) = MageScene("battle_priests", 80 + seed);
            m.HoldSpells = true;
            Run(b, 10);
            int smite = Array.FindIndex(m.Spells, a => a.Id == "smite");
            b.Tide = 0;
            Check(b.WouldOverreach(m, smite) && b.CannotCast(m, smite) == null, $"with Will to spare the spell fan offers an overreach ({b.CannotCast(m, smite)}, will {m.Will:0})");
            m.CastSpell = smite; m.CastAt = f.Center; m.CastSince = b.Time;
            float will = m.Will;
            float hp = m.Units.Sum(u => u.Hp);
            for (int i = 0; i < 3 * 60 && b.Casts.Count == 0; i++) { b.Tide = 0; b.Update(1 / 60f); }
            if (b.Casts.Count == 0) continue;
            cast++;
            float cost = m.Spells[smite].Will + Battle.OrderedWill;
            Check(will - m.Will >= cost * 2 - 1, $"an overreach costs twice the Will ({will - m.Will:0} of {cost * 2:0})");
            Check(b.Overreaches[0] == 1, "it is counted");
            if (b.Backlashes[0] > 0) { lashed++; Check(m.Units.Sum(u => u.Hp) < hp && m.Units.All(u => !u.Dead), "a backlash strikes a caster down, but does not kill him"); }
        }
        Console.WriteLine($"overreach: {cast} of 12 ordered casts went off on a dry Tide, {lashed} lashed back");
        Check(cast == 12 && lashed > 0 && lashed < 12, "every ordered cast overreaches; some, not all, lash back");
        // The well reaches the battle: the campaign hands the field's Tide over.
        var c = Campaign.New(61);
        var well = c.Sites.First(x => c.Well(x).Strength == 3);
        Check(c.TideAt(well.Pos).Strength == 3, "the field at a strong well");
        Check(Campaign.TideEdge(1, new[] { c.Player }, new[] { c.Player }) == 1, "the Tide weighs nothing between equal sides");
    }
}

