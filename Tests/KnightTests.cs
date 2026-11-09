using Microsoft.Xna.Framework;

namespace Ironfield;

/// Knight orders and the captain's aura (docs/KNIGHT_ORDERS.md).
public static partial class SelfTest
{
    /// K1: the aura blade lights only from tier 2 and with aura enough, bites through armour and guards, and burns the pool.
    static void AuraBlade()
    {
        float Blow(int tier, bool hold)
        {
            var b = Battle.Empty(31, new BattleOptions { AuraTier = tier });
            var p = b.Player;
            p.Horse = null;
            var foe = b.AddSquad(1, Troops.Get("men_at_arms"), 1, p.Pos + new Vector2(0, 1.5f), 1, -Vector2.UnitY).Units[0];
            b.Input = new PlayerInput { Facing = MathHelper.PiOver2, Aura = hold };
            b.Update(1 / 60f);
            foe.Action = UnitAction.Stagger; // no guard: the blow itself is measured
            float hp = foe.Hp;
            b.Hit(p, foe, 30, false);
            return hp - foe.Hp;
        }
        Check(Blow(2, true) > Blow(2, false) * 1.4f, "the aura blade cuts deeper through armour");
        Check(Blow(1, true) == Blow(1, false), "a kindled captain has no blade yet");
        Check(Blow(0, true) == Blow(0, false), "nor an unawakened one");

        var k = Battle.Empty(32, new BattleOptions { AuraTier = 2 });
        k.Player.Horse = null;
        Check(k.Aura == Battle.AuraMax, "a battle starts with a full pool");
        k.Input = new PlayerInput { Aura = true };
        Run(k, 3);
        Check(k.AuraBlade && k.Aura < Battle.AuraMax - 10, "the lit blade burns aura");
        Run(k, 20);
        Check(!k.AuraBlade && k.Aura < Battle.AuraLow, "it goes out when the pool runs dry");
        Check(k.Spent, "and the captain is spent");
        k.Input = new PlayerInput();
        Run(k, 2);
        k.Input = new PlayerInput { Aura = true };
        Run(k, 0.1f);
        Check(!k.AuraBlade, "a spent captain cannot light it again at once");
        Check(Battle.Empty(33).Aura == 0, "an unawakened captain has no pool");
    }

    /// K1: aura armour takes part of a blow from tier 3, paid from the pool; spent, it fails.
    static void AuraArmour()
    {
        float Taken(int tier, float aura = -1)
        {
            var b = Battle.Empty(34, new BattleOptions { AuraTier = tier });
            var p = b.Player;
            p.Horse = null;
            if (aura >= 0) b.Aura = aura;
            var foe = b.AddSquad(1, Troops.Get("men_at_arms"), 1, p.Pos + new Vector2(0, 1.5f), 1, -Vector2.UnitY).Units[0];
            float hp = p.Hp;
            b.Hit(foe, p, 40, false);
            return hp - p.Hp;
        }
        Check(Taken(3) < Taken(2) * 0.8f, "aura armour soaks part of a blow");
        Check(Taken(3, 5) == Taken(2), "a spent captain's aura armour fails");
        var k = Battle.Empty(35, new BattleOptions { AuraTier = 3 });
        k.Player.Horse = null;
        var f = k.AddSquad(1, Troops.Get("men_at_arms"), 1, k.Player.Pos + new Vector2(0, 1.5f), 1, -Vector2.UnitY).Units[0];
        k.Hit(f, k.Player, 40, false);
        Check(k.Aura < Battle.AuraMax, "and it costs aura");
    }

    /// K1: aura is strong and bounded: an aura expert fells men a plain captain cannot, and a company still overwhelms him.
    static void AuraBound()
    {
        (int Won, int Felled) Fights(int tier, int men, int seeds)
        {
            int won = 0, felled = 0;
            for (int seed = 0; seed < seeds; seed++)
            {
                var b = Battle.Empty(700 + seed, new BattleOptions { AuraTier = tier });
                b.Player.Horse = null;
                b.EnemyCaptain.Pos = new Vector2(0, 400);
                var foes = b.AddSquad(1, Troops.Get("men_at_arms"), men, b.Player.Pos + new Vector2(0, 3), Math.Min(men, 6), -Vector2.UnitY).Units;
                if (Fight(b, foes, parry: true, aura: tier >= 2)) won++;
                felled += foes.Count(f => !f.Active);
            }
            return (won, felled);
        }
        var plain = Fights(0, 4, 8);
        var knight = Fights(3, 4, 8);
        var swarmed = Fights(3, 16, 4);
        Console.WriteLine($"aura: against four men-at-arms a plain captain wins {plain.Won}/8 felling {plain.Felled}, an aura expert {knight.Won}/8 felling {knight.Felled}; against sixteen the expert wins {swarmed.Won}/4 felling {swarmed.Felled}");
        Check(knight.Won > plain.Won && knight.Felled >= plain.Felled + 8, "an aura expert wins fights a plain captain loses, and fells many more men");
        Check(swarmed.Won == 0, "but a company still overwhelms him");
    }

    /// K1 on the map: XP stops at the wall, a trial breaks it, the walls each trial can break, and the battle reads the tier.
    static void AuraGrows()
    {
        var c = Campaign.New(88);
        var cap = c.Captain;
        c.GainAura(1000);
        Check(cap.AuraWall && cap.AuraXp == Campaign.AuraNeeds[0] && cap.AuraTier == 0, "XP stops at the wall");
        var duel = new BattleResult(0, 30, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Wounded, 0);
        c.AuraAfterDuel(duel, won: false, "Sir Test");
        Check(cap.AuraTier == 0, "a lost duel breaks no wall");
        c.AuraAfterDuel(duel, won: true, "Sir Test");
        Check(cap.AuraTier == 1 && !cap.AuraWall && cap.AuraXp > 0, "a won duel wakes the aura, and its XP counts towards the next tier");
        Check(new BattleOptions().WithCaptain(cap).AuraTier == 1, "the battle fights at your tier");
        cap.Wounds.Add("core");
        Check(new BattleOptions().WithCaptain(cap).AuraTier == 0 && c.AuraInBattle == 0, "a cracked core fights a tier lower");
        cap.Wounds.Remove("core");
        cap.AuraTier = Campaign.DuelBreaksBelow;
        c.GainAura(1000);
        c.AuraAfterDuel(duel, won: true, "Sir Test");
        Check(cap.AuraTier == Campaign.DuelBreaksBelow && cap.AuraWall, "a duel does not break the higher walls");
        var held = new BattleResult(0, 200, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Wounded, 0, PlayerKills: 5);
        c.AuraAfterBattle(held, won: true, odds: 2.5f);
        Check(cap.AuraTier == Campaign.DuelBreaksBelow + 1, "holding against two to one does");
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        Check(back.Captain.AuraTier == cap.AuraTier && back.Captain.AuraXp == cap.AuraXp, "the aura is saved");
    }

    /// A scripted fight field for forms: the captain on foot at an aura tier with these forms, and men-at-arms ahead.
    static (Battle B, List<Unit> Foes) FormField(int seed, int tier, string[] forms, int men = 3, float dist = 3)
    {
        var b = Battle.Empty(seed, new BattleOptions { AuraTier = tier, Forms = forms });
        b.Player.Horse = null;
        b.EnemyCaptain.Pos = new Vector2(0, 400);
        var foes = b.AddSquad(1, Troops.Get("men_at_arms"), men, b.Player.Pos + new Vector2(0, dist), men, -Vector2.UnitY).Units.ToList();
        foreach (var f in foes) f.Squad!.Order = Order.Hold;
        b.Input = new PlayerInput { Facing = MathHelper.PiOver2 };
        b.Update(1 / 60f);
        return (b, foes);
    }

    static void Press(Battle b, int slot)
    {
        b.Input = new PlayerInput { Facing = MathHelper.PiOver2, Form1 = slot == 0, Form2 = slot == 1, Form3 = slot == 2, Great = slot == 3 };
        b.Update(1 / 60f);
        b.Input = new PlayerInput { Facing = MathHelper.PiOver2 };
    }

    /// K2: forms load, sit in their slots, are gated by tier, aura, cooldown and once a battle, and each kind does its work.
    static void FormsWork()
    {
        Check(!Forms.Validate().Any(), "forms.json is valid: " + string.Join("; ", Forms.Validate()));
        var (b, foes) = FormField(41, 3, new[] { "aura_burst", "focus", "aura_slash" });
        Check(b.FormSlot[0]?.Id == "focus" && b.FormSlot[1]?.Id == "aura_slash" && b.FormSlot[3]?.Id == "aura_burst" && b.FormSlot[2] == null, "forms take their slots: three, and the great one apart");
        Check(b.CannotForm(3) != null, "a great form needs its tier (master)");
        // Focus: breath back, harder blows, aura spent, then a cooldown.
        b.Player.Stamina = 10;
        Press(b, 0);
        Check(b.Player.Stamina == 100 && b.Focused && b.Aura < Battle.AuraMax, "Focus brings breath back and costs aura");
        Check(b.CannotForm(0) != null, "and then waits out its cooldown");
        // Aura Slash: after its warning, the men in front take it.
        float hp0 = foes.Sum(f => f.Hp);
        Press(b, 1);
        Check(b.PendingForms.Count == 1 && foes.Sum(f => f.Hp) == hp0, "a thrown slash warns before it lands");
        Run(b, 0.5f);
        Check(foes.Sum(f => f.Hp) < hp0 - 60, "then it cuts the men in front");
        var behind = b.AddSquad(1, Troops.Get("men_at_arms"), 1, b.Player.Pos + new Vector2(0, -3), 1, Vector2.UnitY).Units[0];
        b.Aura = Battle.AuraMax;
        Run(b, 13);
        b.Aura = Battle.AuraMax;
        float back = behind.Hp;
        Press(b, 1);
        Run(b, 0.5f);
        Check(behind.Hp == back, "but not the man behind you");
        var low = FormField(42, 2, new[] { "aura_slash" }).B;
        Check(low.CannotForm(0)!.StartsWith("NEEDS AURA TIER"), "a form waits for its tier");

        // Unbroken Wall: nothing from the front gets through, and the striker reels.
        var (g, gf) = FormField(43, 1, new[] { "unbroken_wall" }, 1, 1.5f);
        Press(g, 0);
        float ghp = g.Player.Hp;
        g.Hit(gf[0], g.Player, 40, false);
        Check(g.Player.Hp == ghp && gf[0].Action == UnitAction.Stagger, "Unbroken Wall stops a blow from the front and the striker reels");

        // Thorn Edge: the next blows go through armour, three of them.
        var (e, ef) = FormField(44, 1, new[] { "thorn_edge" }, 1, 1.5f);
        ef[0].Action = UnitAction.Stagger;
        float before = ef[0].Hp;
        e.Hit(e.Player, ef[0], 20, false);
        float plain = before - ef[0].Hp;
        Press(e, 0);
        ef[0].Action = UnitAction.Stagger;
        before = ef[0].Hp;
        e.Hit(e.Player, ef[0], 20, false);
        Check(before - ef[0].Hp > plain * 1.5f && e.EdgeLeft == 2, "Thorn Edge cuts through armour, one blow spent");

        // Wolf Leap: onto the foe ahead, who takes a heavy blow.
        var (l, lf) = FormField(45, 3, new[] { "winter_howl", "wolf_leap" }, 1, 7);
        var from = l.Player.Pos;
        Press(l, 1);
        Check(Vector2.Distance(l.Player.Pos, lf[0].Pos) < 2 && Vector2.Distance(from, l.Player.Pos) > 4 && lf[0].Hp < lf[0].Type.MaxHp, "Wolf Leap springs onto the foe and strikes");
        // Winter Howl: the men around lose heart.
        float m = lf[0].Morale;
        Press(l, 0);
        Check(lf[0].Morale < m - 8, "Winter Howl shakes the men near you");

        // A great form: once a battle; Lily's Bastion steadies your companies near you.
        var (k, _) = FormField(46, 4, new[] { "lily_bastion" });
        var mine = k.AddSquad(0, Troops.Get("shieldmen"), 6, k.Player.Pos + new Vector2(4, 0), 6, Vector2.UnitY);
        Press(k, 3);
        Check(mine.Mods.Block > 0.3f, "Lily's Bastion raises your companies' shields");
        Check(k.GreatUsed && k.CannotForm(3) != null, "and a great form is spent for the battle");
        k.Aura = Battle.AuraMax;
        Run(k, 400);
        Check(k.CannotForm(3) == "THE GREAT FORM IS SPENT", "even long after");

        // Devour: life torn from the men around you comes back to you. Black Flame forms raise corruption.
        var (d, df) = FormField(47, 2, new[] { "devour" }, 3, 1.5f);
        d.Player.Hp = 40;
        Press(d, 0);
        Check(d.Player.Hp > 40 && d.CorruptionRaised > 0 && d.Result().Corruption > 0, "Devour heals you from your foes and feeds the black flame");
        Check(d.Result().FormUses["devour"] == 1, "the battle counts each form's uses");

        // Measured Blade: a parry strikes back.
        var (c, cf) = FormField(48, 1, new[] { "measured_blade" }, 1, 1.6f);
        Press(c, 0);
        cf[0].Swing = AttackDir.Left;
        c.Input = new PlayerInput { Facing = MathHelper.PiOver2, Block = true, Dir = Dirs.Mirror(cf[0].Swing) };
        c.Update(1 / 60f);
        float chp = cf[0].Hp;
        c.Hit(cf[0], c.Player, 26, false);
        Check(cf[0].Hp < chp, "under Measured Blade a parry strikes back");
    }

    /// K2 on the map: forms learned by tier, slot order, ranks from use, and the battle reads them.
    static void FormsLearned()
    {
        var c = Campaign.New(89);
        var cap = c.Captain;
        Check(cap.Forms.Count == 0, "an unawakened captain knows no form");
        c.GainAura(1000);
        var duel = new BattleResult(0, 30, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Wounded, 0);
        c.AuraAfterDuel(duel, won: true, "Sir Test");
        Check(cap.Forms.SequenceEqual(new[] { "focus" }), "the kindled captain learns Focus");
        Check(c.LearnForm("thorn_edge") && !c.LearnForm("thorn_edge"), "a form is learned once");
        Check(Campaign.SlottedForms(cap).SequenceEqual(new[] { "focus", "thorn_edge" }), "forms fill the slots in the order learned");
        c.SlotForm("thorn_edge");
        Check(Campaign.SlottedForms(cap)[0] == "thorn_edge", "a chosen form takes the first slot");
        var o = new BattleOptions().WithCaptain(cap);
        Check(o.Forms.SequenceEqual(cap.Forms) && o.FormRanks["focus"] == 1, "the battle gets your forms and their ranks");
        cap.FormUses["focus"] = Campaign.FormRankUses * 2;
        Check(Campaign.FormRank(cap, "focus") == 3 && new Battle(1, Armies.Standard(0), Armies.Standard(1), new BattleOptions().WithCaptain(cap)).FormSlot.Any(f => f?.Id == "focus"), "forms rank up with use, to three");
    }

    /// K3: the knight orders stand on a new map with chapters, grand masters and two mixed armies each; an old save gets them on
    /// load; they keep their own dice, train their own brothers, and a fallen commander's army is raised again.
    static void OrdersOnMap()
    {
        Check(KnightOrders.Validate().ToList() is var bad && bad.Count == 0, "orders.json is valid: " + string.Join("; ", KnightOrders.Validate()));
        var c = Campaign.New(91);
        Check(c.S.Orders.Count >= 6, $"the orders stand on a new map ({c.S.Orders.Count})");
        foreach (var o in c.S.Orders)
        {
            var def = KnightOrders.Get(o.Id);
            Check(c.SettlementById(o.Chapter) is Settlement ch && ch.Faction == o.Patron, $"{o.Id}: its chapter is a patron town");
            Check(c.CharacterById(o.GrandMaster) is Character gm && gm.Kind == "knight" && gm.Order == o.Id && gm.Aura >= 3, $"{o.Id}: a grand master with aura");
            var armies = c.OrderParties(o.Id).ToList();
            Check(armies.Count == Campaign.OrderArmies && armies.All(p => p.Faction == o.Patron && p.Kind == PartyKind.Lord), $"{o.Id}: two armies of the patron");
            foreach (var p in armies)
            {
                int sig = p.Companies.Where(co => co.Type.Order == o.Id).Sum(co => co.Men);
                float share = sig / (float)p.Men;
                Check(share is > 0.3f and < 0.55f, $"{o.Id}: about two in five are brothers ({share:0.00})");
                Check(c.CharacterOf(p) is Character k && k.Kind == "knight" && o.Commanders.Contains(k.Id), $"{o.Id}: led by a knight-commander");
            }
        }
        Check(c.S.Characters.Where(ch => ch.Kind == "knight").All(k => k.Faction >= 0), "knights serve a realm");
        // The player may not train an order's brothers; the order's own army may.
        var lily = c.OrderParties("lily").FirstOrDefault();
        if (lily != null)
        {
            var co = lily.Companies.First(x => x.Type.Order == "");
            Check(c.CanEnterLine(lily, co, co.Troop, "lily_squires", out _), "an order trains its own brothers");
            Check(!c.CanEnterLine(c.Player, co, co.Troop, "lily_squires", out var why) && why.Contains("LILY"), "you may not, unsworn: " + why);
        }
        // Old save: no orders yet; loading adds them without touching the main dice.
        var old = Campaign.New(92);
        old.S.Parties.RemoveAll(p => p.KnightOrder != "");
        old.S.Characters.RemoveAll(ch => ch.Kind == "knight");
        old.S.Orders.Clear();
        old.S.OrderRng = 0;
        ulong main = old.S.Rng;
        var back = new Campaign(CampaignState.Deserialize(old.S.Serialize()));
        Check(back.S.Orders.Count == c.S.Orders.Count && back.S.Rng == main, "an old save gets the orders on load, the main dice untouched");
        // A fallen commander's army comes back after a while.
        var army = back.OrderParties("wolf").First();
        back.S.Parties.Remove(army);
        Days(back, (int)Campaign.OrderRespawnDays + 3);
        Check(back.OrderParties("wolf").Count() == Campaign.OrderArmies, "the order raises a fallen commander's army again");
        string json = back.S.Serialize();
        Check(CampaignState.Deserialize(json).Serialize() == json, "orders save and load");
    }
}
