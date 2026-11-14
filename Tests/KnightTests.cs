using Microsoft.Xna.Framework;

namespace Ironfield;

/// Knight orders and the captain's aura (docs/archive/KNIGHT_ORDERS.md).
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

    static BattleResult Bout(bool won) => new(won ? 0 : 1, 30, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Unhurt, 0);

    /// K4: an order sets its trial only once you are known to it; won, you swear and learn its first form; lost, you are hurt
    /// and a little better thought of. A squire may not take its brothers; a knight-brother takes the tier IV troop.
    static void OrderOath()
    {
        var c = Campaign.New(93);
        var o = c.OrderById("lily")!;
        Check(c.CannotSeekTrial("lily") is string w && w.Contains("STANDING"), "an order that does not know you sets no trial");
        c.RaiseStanding("lily", Campaign.TrialStanding + 1);
        Check(c.CannotSeekTrial("lily") == null, "known to it, you may seek the trial: " + c.CannotSeekTrial("lily"));
        float before = o.Standing;
        c.FinishBattle(Bout(false), c.PrepareTrial("lily"));
        Check(c.Captain.Order == "" && o.Standing > before && c.S.Hours < c.Captain.WoundedUntil, "a lost trial: unsworn, hurt, a little respected");
        Check(c.CannotSeekTrial("lily") is string hurt && hurt.Contains("HURT"), "no second trial while hurt");
        c.Captain.WoundedUntil = 0;
        var report = c.FinishBattle(Bout(true), c.PrepareTrial("lily"));
        Check(report.Won && c.Captain.Order == "lily" && c.Captain.OrderRank == 0 && c.OrderRankName == "Squire", "a won trial: you swear as a squire");
        Check(c.Captain.Forms.Contains("unbroken_wall"), "the squire learns the order's first form");
        Check(c.CannotSeekTrial("rose") is string sworn && sworn.Contains("SWORN"), "one order at a time");
        var co = c.Player.Companies.First();
        string from = Troops.All.First(t => t.UpgradesTo.Contains("lily_squires")).Id;
        Check(!c.CanEnterLine(c.Player, co, from, "lily_squires", out _), "a squire may not train the order's brothers");
        c.Captain.OrderRank = 1;
        Check(c.CanEnterLine(c.Player, co, from, "lily_squires", out _), "a knight-brother trains its tier IV troop");
        Check(!c.CanEnterLine(c.Player, co, "lily_squires", "lily_knights", out _), "but not its tier V troop yet");
    }

    /// K4: a broken vow costs standing and a penance; atone by winning in the order's name; break one again before atoning
    /// and you are cast out. Vows your creed lacks cost nothing. Some orders never take back one who left.
    static void CreedBroken()
    {
        var c = Campaign.New(94);
        c.Swear("lily"); // never sell prisoners, never rob the road
        var o = c.OrderById("lily")!;
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town);
        float st = o.Standing;
        c.Player.Prisoners.Add(new PrisonerGroup { Troop = "levies", Count = 5 });
        c.SellPrisoners(town);
        Check(c.Captain.Penance && o.Standing <= st - Campaign.VowCost + 0.01f, "selling prisoners breaks the Lily's vow: a penance");
        Check(c.CannotRise() is string pen && pen.Contains("ATONE"), "no rising with a penance owed");
        var foe = new Party { Kind = PartyKind.Lord, Faction = Enumerable.Range(0, c.S.Factions.Count).FirstOrDefault(f => c.FactionsAtWar(o.Patron, f), -1) };
        if (foe.Faction >= 0)
        {
            c.OrderAfterBattle(true, new List<Party> { foe });
            Check(!c.Captain.Penance, "a battle won against the patron's foe atones");
            c.BreakVow("no_rob", "rob travellers on the road");
            Check(c.Captain.Penance, "a second vow broken: penance again");
        }
        else c.BreakVow("no_rob", "rob travellers on the road");
        c.BreakVow("no_rob", "rob travellers on the road");
        Check(c.Captain.Order == "" && c.Captain.Forsworn.Contains("lily"), "broken again before atoning: cast out");
        c.Swear("rose"); // meet every foe in person, never rob the road: selling prisoners is no sin to the Rose
        c.Player.Prisoners.Add(new PrisonerGroup { Troop = "levies", Count = 5 });
        c.SellPrisoners(town);
        Check(!c.Captain.Penance && c.Captain.Order == "rose", "a vow your creed lacks costs nothing");
        c.LeaveOrder();
        c.Swear("wolf");
        c.LeaveOrder();
        c.RaiseStanding("wolf", 200);
        Check(c.CannotSeekTrial("wolf") is string never && never.Contains("NEVER"), "the Wolfsworn never take back one who left");
        // Winning against what an order hunts makes it know you, sworn or not.
        float rose = c.StandingWith("rose");
        c.OrderAfterBattle(true, new List<Party> { new() { Kind = PartyKind.Bandit, Faction = -1 } });
        Check(c.StandingWith("rose") > rose, "beating brigands raises the Rose's regard");
        c.OrderAfterBattle(false, new List<Party> { c.OrderParties("falcon").First() });
        Check(c.StandingWith("falcon") < 0, "fighting an order's army turns it against you");
    }

    /// K4: each rank needs aura, standing and order deeds; rising teaches the order's second and great forms; the chapter
    /// spares brothers once a week; the grand master is chosen, not made.
    static void OrderRankGate()
    {
        var c = Campaign.New(95);
        c.Swear("rose");
        var cap = c.Captain;
        Check(c.CannotRise() is string a && a.Contains("AURA"), "Knight-Brother needs a kindled aura");
        cap.AuraTier = 1;
        Check(c.CannotRise() is string b && b.Contains("STANDING"), "...and standing 30");
        c.RaiseStanding("rose", 30);
        Check(c.RiseInOrder() == null && cap.OrderRank == 1, "you rise to Knight-Brother");
        cap.AuraTier = 2;
        c.RaiseStanding("rose", 30);
        Check(c.CannotRise() is string d && d.Contains("DEEDS"), "Knight-Captain needs an order deed");
        cap.OrderDeeds = 1;
        Check(c.RiseInOrder() == null && cap.OrderRank == 2 && cap.Forms.Contains("rose_cut"), "Knight-Captain learns the second form");
        cap.AuraTier = 3; cap.OrderDeeds = 2;
        c.RaiseStanding("rose", 30);
        Check(c.RiseInOrder() == null && cap.Forms.Contains("red_rose_bloom") && c.OrderRankName == "Knight-Commander", "Knight-Commander learns the great form");
        cap.AuraTier = 5; cap.OrderDeeds = 9;
        Check(c.CannotRise() is string gm && gm.Contains("CHOSEN"), "the grand master is chosen, not made");
        var chapter = c.SettlementById(c.OrderById("rose")!.Chapter)!;
        c.S.Gold = 100000;
        c.Player.Companies.RemoveAll(x => x != c.Player.Companies[0]);
        Check(c.RecruitBrothers(chapter) == null && c.Player.Companies.Any(x => x.Troop == "thorn_knights"), "a knight-commander takes the tier VI brothers: " + c.RecruitBrothers(chapter));
        Check(c.RecruitBrothers(chapter) is string spare && spare.Contains("SPARE"), "the chapter spares brothers once a week");
        Check(c.TakeOrderContract(chapter) == null && c.S.Contracts.Any(k => k.Order == "rose"), "the chapter gives an order contract");
        var other = c.S.Settlements.First(t => c.ChapterAt(t) is OrderState os && os.Id != "rose");
        Check(c.RecruitBrothers(other) != null, "only your own order's chapter");
        // War between your realm and the patron ends the oath.
        int patron = c.OrderById("rose")!.Patron;
        if (patron != c.Captain.Contract && c.Captain.Contract >= 0)
        {
            c.S.Wars.RemoveAll(x => x.A == patron && x.B == c.Captain.Contract || x.B == patron && x.A == c.Captain.Contract);
            c.S.Wars.Add(new War { A = patron, B = c.Captain.Contract, Active = true });
            Days(c, 1);
            Check(c.Captain.Order == "" && !c.Captain.Forsworn.Contains("rose"), "your realm at war with the patron ends the oath, without blame");
        }
    }

    /// K5: a vigil at a shrine, stones, old battlefield or hermit is a trial for a wall below Aura Master: it breaks the wall
    /// or cracks the core. A cracked core holds the aura a tier down until it mends (a month; less resting in a town). Grief
    /// breaks a wall in anger (at a cost) or later in mourning. A duel won against a champion above you breaks any wall.
    static void CoreAndTrials()
    {
        int broke = 0, cracked = 0;
        for (int seed = 0; seed < 12; seed++)
        {
            var c = Campaign.New(96 + seed);
            var cap = c.Captain;
            cap.AuraTier = 3; cap.AuraWall = true; cap.AuraXp = Campaign.AuraNeeds[3];
            var site = c.Sites.First(x => x.Kind is "shrine" or "stones" or "battlefield" or "hermit");
            c.S.Event = null;
            c.S.SiteUntil.Remove(site.Id);
            c.ArriveAtSite(site);
            Check(c.S.Event?.Id == "aura_vigil", "a wall and a holy place: a vigil is offered");
            c.Choose(0);
            if (cap.AuraTier == 4) broke++;
            if (cap.Wounds.Contains("core")) { cracked++; Check(cap.AuraTier == 3 && cap.AuraWall && c.AuraInBattle == 2, "a failed vigil cracks the core: a tier lower in battle"); }
        }
        Check(broke > 0 && cracked > 0 && broke + cracked == 12, $"a vigil breaks through or cracks the core ({broke} broke, {cracked} cracked)");
        // Mending: a month, or sooner resting in a town.
        var m = Campaign.New(110);
        m.Captain.AuraTier = 2;
        m.CrackCore();
        Check(new BattleOptions().WithCaptain(m.Captain).AuraTier == 1, "the battle gets the lower tier");
        Days(m, 15);
        Check(m.Captain.Wounds.Contains("core"), "half a month: still cracked");
        Days(m, 16);
        Check(!m.Captain.Wounds.Contains("core") && m.AuraInBattle == 2, "a month: the core mends");
        var r = Campaign.New(111);
        r.CrackCore();
        var town = r.S.Settlements.First(t => t.Kind == SettlementKind.Town);
        r.Player.Pos = town.Pos;
        for (int d = 0; d < 11 && r.Captain.Wounds.Contains("core"); d++) { r.Rest(24); r.Encounter = null; r.Arrived = null; }
        Check(!r.Captain.Wounds.Contains("core"), "resting in a town mends it in a third of the time");
        // Grief, in anger: through at once, with a mark of the Black Flame.
        var g = Campaign.New(112);
        g.Captain.AuraTier = 4; g.Captain.AuraWall = true;
        g.S.Event = new PendingEvent { Id = "aura_grief" };
        g.Choose(0);
        Check(g.Captain.AuraTier == 5 && g.Captain.Corruption >= 5, "grief in anger breaks even the last wall, and leaves a mark");
        // A duel won against a knight of your aura or more breaks the wall; against a lesser one it does not.
        var d2 = Campaign.New(113);
        d2.Captain.AuraTier = 3; d2.Captain.AuraWall = true;
        var army = d2.OrderParties("wolf").First();
        var knight = d2.CharacterOf(army)!;
        knight.Aura = 1;
        army.Tier = 3;
        d2.FinishBattle(new BattleResult(0, 20, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Wounded, 0), d2.PrepareDuel(army));
        Check(d2.Captain.AuraTier == 3, "beating a lesser knight breaks no high wall");
        var army2 = d2.OrderParties("wolf").Last();
        d2.CharacterOf(army2)!.Aura = 3;
        d2.FinishBattle(new BattleResult(0, 20, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Wounded, 0), d2.PrepareDuel(army2));
        Check(d2.Captain.AuraTier == 4, "beating a knight of your aura breaks the wall");
    }

    /// K5: a Master's presence shakes the foes near him and steadies his own, and does not reach the far end of the field;
    /// an Expert has none. A Transcendent may use the great form twice.
    static void PresenceShakes()
    {
        (float near, float far, float own) Shake(int tier)
        {
            var b = Battle.Empty(120, new BattleOptions { AuraTier = tier, Forms = new[] { "aura_burst" } });
            b.Player.Horse = null;
            b.EnemyCaptain.Pos = new Vector2(0, 400);
            var near = b.AddSquad(1, Troops.Get("men_at_arms"), 6, b.Player.Pos + new Vector2(0, 9), 6, -Vector2.UnitY);
            var far = b.AddSquad(1, Troops.Get("men_at_arms"), 6, b.Player.Pos + new Vector2(60, 9), 6, -Vector2.UnitY);
            var own = b.AddSquad(0, Troops.Get("men_at_arms"), 6, b.Player.Pos + new Vector2(0, -6), 6, Vector2.UnitY);
            foreach (var q in new[] { near, far, own }) q.Order = Order.Hold;
            foreach (var u in near.Units.Concat(far.Units).Concat(own.Units)) u.Morale = 50;
            b.Input = new PlayerInput { Facing = MathHelper.PiOver2 };
            Run(b, 6);
            return (near.Units.Average(u => u.Morale), far.Units.Average(u => u.Morale), own.Units.Average(u => u.Morale));
        }
        var master = Shake(4);
        var expert = Shake(3);
        Check(master.near < expert.near - 8, $"a Master shakes the company in front of him ({master.near:0} against {expert.near:0})");
        Check(MathF.Abs(master.far - expert.far) < 1, $"but not the one across the field ({master.far:0} against {expert.far:0})");
        Check(master.own > expert.own + 4, $"and steadies his own men near him ({master.own:0} against {expert.own:0})");
        var t5 = FormField(121, 5, new[] { "aura_burst" }).B;
        t5.Aura = Battle.AuraMax; Press(t5, 3);
        Check(t5.CannotForm(3) != "THE GREAT FORM IS SPENT", "a Transcendent may use the great form again");
        var t4 = FormField(122, 4, new[] { "aura_burst" }).B;
        t4.Aura = Battle.AuraMax; Press(t4, 3);
        Check(t4.CannotForm(3) == "THE GREAT FORM IS SPENT", "a Master once a battle");
    }

    /// K6: an enemy champion with aura lights his blade, wears aura armour and fires his forms by rule (a burst when men
    /// crowd him, a leap at a foe ahead); one without aura does none of it.
    static void ChampionsUseAura()
    {
        Battle Field(int seed, int aura, string[] forms, Vector2[] men)
        {
            var b = Battle.Empty(seed, new BattleOptions { EnemyAura = aura, EnemyForms = forms });
            b.Player.Pos = new Vector2(0, -300);
            var c = b.EnemyCaptain;
            c.Horse = null;
            foreach (var at in men)
            {
                var sq = b.AddSquad(0, Troops.Get("men_at_arms"), 1, c.Pos + at, 1, Vector2.UnitY);
                sq.Order = Order.Hold;
            }
            b.Input = new PlayerInput();
            return b;
        }
        var ring = new[] { new Vector2(2, 0), new Vector2(-2, 0), new Vector2(0, 2), new Vector2(0, -2), new Vector2(1.5f, 1.5f) };
        var burst = Field(130, 4, new[] { "aura_burst", "winter_howl" }, ring);
        float hp0 = burst.Units.Where(u => u.Team == 0 && !u.IsPlayer).Sum(u => u.Hp);
        Run(burst, 2.5f);
        Check(burst.FoeFormsUsed.Count > 0, "a crowded champion fires a form");
        Check(burst.Units.Where(u => u.Team == 0 && !u.IsPlayer).Sum(u => u.Hp) < hp0, "and it hurts the men round him");
        var plain = Field(130, 0, new[] { "aura_burst", "winter_howl" }, ring);
        Run(plain, 2.5f);
        Check(plain.FoeFormsUsed.Count == 0 && plain.FoeAura == 0, "a champion without aura has none of it");
        var leap = Field(131, 1, new[] { "falcon_dive" }, new[] { new Vector2(0, -7) });
        Run(leap, 1.5f);
        Check(leap.FoeFormsUsed.Contains("falcon_dive"), "a champion leaps at a foe in reach");
        // Aura armour: blows on him cost his pool.
        var armour = Field(132, 3, Array.Empty<string>(), Array.Empty<Vector2>());
        var striker = armour.AddSquad(0, Troops.Get("men_at_arms"), 1, armour.EnemyCaptain.Pos + new Vector2(0, 1.2f), 1, -Vector2.UnitY).Units[0];
        for (int i = 0; i < 12; i++) { armour.EnemyCaptain.Action = UnitAction.Stagger; armour.Hit(striker, armour.EnemyCaptain, 30, false); }
        Check(armour.FoeAura < Battle.AuraMax, "blows on an aura expert champion drain his pool");
    }

    /// K6 on the map: order knights bring their aura and forms to duels and battles and weigh in auto-resolve; the rival's
    /// chain ends in a brother or a black flame; a grand master is chosen (you, if you have earned it), and then the order rides
    /// with you; crusades march on a great host of what an order hunts.
    static void OrderPolitics()
    {
        var c = Campaign.New(97);
        var army = c.OrderParties("rose").First();
        var knight = c.CharacterOf(army)!;
        knight.Aura = 3;
        var duel = c.PrepareDuel(army);
        Check(duel.Options.EnemyAura == 3 && duel.Options.EnemyForms.Contains("thorn_edge") && duel.Options.EnemyForms.Contains("rose_cut"), "a knight duels with his aura and his order's forms");
        var lord = c.S.Parties.First(p => p.Kind == PartyKind.Lord && p.KnightOrder == "");
        Check(c.ChampionAura(lord) == 0 && c.PrepareDuel(lord).Options.EnemyAura == 0, "lords have no aura");
        Check(MathF.Abs(c.Edge(army) - (1 + Balance.Get().AuraWeight * 3)) < 1e-4f, "a knight's aura weighs in auto-resolve");
        // The rival.
        c.Swear("lily");
        var rival = c.CharacterById(c.Captain.Rival);
        Check(rival != null && rival.Order == "lily" && c.S.Flags.ContainsKey("rival_meet"), "swearing draws a rival and starts his chain");
        c.S.Event = new PendingEvent { Id = "rival_meet" };
        c.Choose(0);
        Check(c.OpinionOf(rival!, c.Me) > 0 && c.S.Flags.ContainsKey("rival_lists"), "offering your hand warms him; the lists come next");
        c.S.Event = new PendingEvent { Id = "rival_end" };
        c.Choose(0);
        Check(rival!.OrderRank == 1 && rival.Order == "lily", "a friend is knighted beside you");
        var c2 = Campaign.New(98);
        c2.Swear("wolf");
        var foe = c2.CharacterById(c2.Captain.Rival)!;
        c2.S.Event = new PendingEvent { Id = "rival_meet" };
        c2.Choose(1);
        c2.S.Event = new PendingEvent { Id = "rival_end" };
        c2.Choose(0);
        Check(foe.Order == "hollow" && c2.S.Flags.ContainsKey("rival_fallen"), "a rival who hates you goes to the Black Flame");
        // An election you win.
        var o = c.OrderById("lily")!;
        c.Captain.OrderRank = 3; c.Captain.AuraTier = 4; o.Standing = 85;
        c.CharacterById(o.GrandMaster)!.Dead = true;
        Days(c, 1);
        Check(o.GrandMaster == c.S.CaptainChar && c.Captain.OrderRank == 4 && c.OrderRankName == "Grand Master", "the commanders choose you as Grand Master");
        var mine = c.OrderParties("lily").First();
        mine.Pos = c.Player.Pos + new Vector2(30, 0);
        mine.Down = false;
        foreach (var k in mine.Companies) k.Stacks[0].Men += c.Scaled(5); // a fit army, not one that must go home to refit
        c.LordThink(mine);
        Check(mine.Task == "escort" && mine.Goal == c.Player.Pos, "your order's armies ride with their grand master");
        // An election you do not stand in.
        var other = c.OrderById("falcon")!;
        int old = other.GrandMaster;
        c.CharacterById(old)!.Dead = true;
        Days(c, 1);
        Check(other.GrandMaster != old && other.GrandMaster != c.S.CaptainChar && c.CharacterById(other.GrandMaster)!.OrderRank == 4, "an order chooses a commander when you do not stand");
        // A crusade against a great host of the dead.
        var host = new Party { Id = c.S.NextId++, Kind = PartyKind.Monster, Breed = "undead", Name = "The Test Dead", Faction = -1, Pos = c.Player.Pos + new Vector2(40, 0) };
        host.Goal = host.Pos;
        host.Companies.Add(c.NewCompany("skeleton", "infantry", c.CrusadeHost, 0));
        c.S.Parties.Add(host);
        Days(c, 1);
        Check(o.CrusadeOn == host.Id || host.Down, "the Lily calls a crusade against the dead");
        if (o.CrusadeOn == host.Id)
        {
            float before = o.Standing; int deeds = c.Captain.OrderDeeds;
            c.OrderAfterBattle(true, new List<Party> { host });
            Check(o.Standing >= before + 10 && c.Captain.OrderDeeds > deeds && o.CrusadeOn == -1, "breaking the crusade's host: standing and a deed");
        }
    }

    /// K7: the Black Flame. Corruption rises only from its own sources; the pact comes after a cracked core; it burns a wall,
    /// opens the Hollow Crown's forms and troops; each threshold draws its price; purging is a way back, once; the ending fires once.
    static void BlackFlame()
    {
        var c = Campaign.New(130);
        Days(c, 30);
        Check(c.Captain.Corruption == 0 && !c.Captain.Pact, "a month of the world leaves no corruption in you");
        Check(!c.CanEnterLine(c.Player, c.Player.Companies[0], "billmen", "hollow_squires", out _), "without the pact the Hollow Crown trains none of your men");
        c.S.Event = null;
        c.Captain.AuraTier = 2;
        c.CrackCore();
        Check(c.S.Event?.Id == "flame_bargain", "a cracked core: the Hollow Crown comes to you");
        c.Choose(0);
        Check(c.Captain.Pact && c.Captain.Corruption == Campaign.PactCost && c.Captain.Forms.Contains("black_flame") && c.Captain.Forms.Contains("devour"),
            "the pact: its price, and the Black Flame forms your aura can hold");
        Check(c.CanEnterLine(c.Player, c.Player.Companies[0], "billmen", "hollow_squires", out _), "with the pact the Hollow Crown's troops are yours to train");
        c.Captain.AuraWall = true;
        Check(c.BurnWall() == null && c.Captain.AuraTier == 3 && c.Captain.Corruption == Campaign.PactCost + Campaign.BurnCost && c.Captain.Forms.Contains("hollow_crown"),
            "the flame burns through a wall with no trial, at a price");
        Check(c.BurnWall() != null, "but only a wall that is there");
        c.Captain.Corruption = 30;
        Days(c, 1);
        Check(c.Captain.FlameSeen == 1 && c.MadnessInBattle == 0, "at 25 the men notice; no madness yet");
        c.Captain.Corruption = 55;
        Days(c, 1);
        var village = c.S.Settlements.First(t => t.Kind == SettlementKind.Village);
        Check(c.Captain.FlameSeen == 2 && c.S.Orders.All(o => o.Standing <= -50), "at 50 every order turns against you");
        Check(c.RecruitLevies(village, 5) == "THE VILLAGE BARS ITS GATES TO YOU", "and villages bar their gates");
        Days(c, 10);
        Check(c.Captain.Corruption == 55, "corruption neither fades nor grows by itself");
        c.Captain.Corruption = 80;
        Days(c, 1);
        Check(c.Captain.FlameSeen == 3 && c.MadnessInBattle > 0, "at 75 the flame can take you in battle");
        // The way back: in a town, with the relic; once only.
        Check(c.Purge() != null, "no purge out in the field");
        c.Player.Pos = c.S.Settlements.First(t => t.Kind == SettlementKind.Town).Pos;
        c.S.Relic = true; c.S.RelicCharges = 1;
        Check(c.Purge() == null && !c.Captain.Pact && c.Captain.Corruption == 30 && c.Captain.AuraTier == 2
            && !c.Captain.Forms.Any(id => Forms.Get(id).Order == "hollow"), "purging: the pact and its forms go, corruption falls, a tier is lost");
        Check(c.Purge() != null && c.S.RelicCharges == 0, "there is no second purge");
        c.S.Event = null;
        c.CrackCore();
        Check(c.S.Event?.Id != "flame_bargain", "once purged, the Hollow Crown does not ask again");
        // The ending at 100: offered, taken once, and the campaign closes.
        var e = Campaign.New(131);
        e.Captain.Pact = true; e.Captain.Corruption = 100; e.S.Event = null;
        Days(e, 1);
        Check(e.S.Event?.Id == "flame_end", "at 100 the black flame comes for its ending");
        e.Choose(1);
        Check(e.S.GameOver && e.Captain.FlameEnd == "burnt" && e.S.Event == null, "burnt out: the campaign is over");
        int deeds = e.S.Deeds.Count;
        Days(e, 3);
        Check(e.S.Event?.Id != "flame_end" && e.S.Deeds.Count == deeds, "the ending fires once");
    }

    /// K7: at 75 corruption the flame takes you in battle for a few seconds; you cut at whoever is nearest, your own men too.
    /// It draws only on the battle's seeded dice, so the same seed gives the same madness.
    static void FlameTakesYou()
    {
        (int spells, float hurt) Mad(float chance)
        {
            var b = Battle.Empty(140, new BattleOptions { Madness = chance });
            b.Player.Horse = null;
            b.EnemyCaptain.Pos = new Vector2(0, 400);
            var own = b.AddSquad(0, Troops.Get("men_at_arms"), 4, b.Player.Pos + new Vector2(0, 1.6f), 4, Vector2.UnitY);
            own.Order = Order.Hold;
            float hp = own.Units.Sum(u => u.Hp);
            Run(b, 20);
            return (b.MadSpells, hp - own.Units.Sum(u => u.Dead ? 0 : u.Hp));
        }
        var a = Mad(0.3f);
        var again = Mad(0.3f);
        var calm = Mad(0);
        Check(a.spells > 0 && a.hurt > 0, $"the flame takes you and you cut your own men ({a.spells} times, {a.hurt:0} hp)");
        Check(a == again, "the same seed, the same madness");
        Check(calm.spells == 0 && calm.hurt == 0, "without the flame you never turn on your own");
    }

    /// K8: your own order. Founding needs its gates (aura, rank or fame, a seat, no other oath); its creed binds you; your
    /// named men are knighted into it; its troops are yours to train; and your successor leads it a tier below you, with its form.
    static void OwnOrderFounded()
    {
        var c = Campaign.New(150);
        Check(c.CannotFound() != null, "a new captain cannot found an order");
        c.Captain.AuraTier = Campaign.FoundAura;
        Check(c.CannotFound()!.Contains("LORD"), "an Aura Expert with neither rank nor fame cannot");
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Battles = 60;
        co.BattleHonours.AddRange(new[] { "Ashby Ford", "Hollin Hill", "Wyck Moor", "Oxlade", "Greyfen", "Stour Bridge" });
        Check(c.Fame >= Campaign.FoundFame && c.CannotFound()!.Contains("SEAT"), "fame enough, but no seat of your own");
        c.S.Settlements.First(t => t.Kind != SettlementKind.Village).Owner = c.Player.Id;
        Check(c.CannotFound() == null, "an expert with fame and a seat may found an order");
        var form = Forms.Generic.Where(f => f.Tier <= Campaign.FoundAura).Last();
        c.LearnForm(form.Id);
        Check(c.FoundOrder(new[] { "no_rob", "no_rob", "no_slaves" }) != null, "its creed needs three different vows");
        Check(c.FoundOrder(new[] { "no_rob", "no_slaves", "contract" }) == null && c.S.OwnOrder!.Name == Campaign.OrderNameOf(co.Name) && c.S.OwnOrder.Form == form.Id,
            "the order is founded from your company and teaches your form");
        Check(c.CannotFound() != null && c.CannotSeekTrial(c.S.Orders[0].Id) != null, "one order of your own, and no other oath beside it");
        Check(c.Vowed("no_rob") && !c.Vowed("no_auto"), "your own creed binds you");
        Check(c.CanEnterLine(c.Player, co, "ironguard", "sworn_brothers", out _), "your order trains its Sworn Brothers from a tier IV line");
        var other = Campaign.New(151);
        Check(!other.CanEnterLine(other.Player, other.Player.Companies[0], "ironguard", "sworn_brothers", out _), "no one else trains them");
        // Knighting a named man.
        var m = c.NameMan(co, "test")!;
        m.Battles = 1;
        Check(c.Knight(m) != null, "a man of one battle is not knighted");
        m.Battles = 5;
        int fame = c.Fame;
        Check(c.Knight(m) == null && m.Knight && c.KnightsAlive == 1 && c.Fame == fame + 10, "a veteran is knighted, and the order's fame grows");
        Check(c.Knight(m) != null, "but only once");
        // Handing on: your successor leads the order a tier lower, with its form.
        var heir = new Character { Id = c.S.Characters.Max(k => k.Id) + 1, Name = "Aldo Fenn", Kind = "companion", Faction = -1 };
        c.S.Characters.Add(heir);
        Check(c.HandOver() == null && c.Captain.AuraTier == Campaign.FoundAura - 1 && c.Captain.Forms.Contains(form.Id) && c.S.OwnOrder.Masters == 2,
            "your successor is grand master a tier below you, with the order's form");
        Check(c.Summary().Any(l => l.Contains(c.S.OwnOrder.Name.ToUpperInvariant())), "the order goes into the story");
        // Without an order he starts two tiers below.
        var lone = Campaign.New(152);
        lone.Captain.AuraTier = 4;
        var lc = lone.Player.Companies.OrderByDescending(k => k.Men).First();
        lc.Battles = 60; lc.BattleHonours.AddRange(new[] { "Ashby Ford", "Hollin Hill", "Wyck Moor" });
        lone.S.Characters.Add(new Character { Id = lone.S.Characters.Max(k => k.Id) + 1, Name = "Bran Hale", Kind = "companion", Faction = -1 });
        Check(lone.HandOver() == null && lone.Captain.AuraTier == 2, "with no order to teach him, he starts two tiers below you");
    }

    /// MA3 (docs/archive/MAGIC_AURA.md 4.1-4.2): aura comes back slower on the move than standing still with no foe near; at full,
    /// blows bank a surge that a form spends first; spent, your breath comes back slower than a plain man's; from tier 2
    /// your company near you takes heart while you fight, one far off does not, and a plain captain inspires nobody.
    static void AuraPool()
    {
        var b = Battle.Empty(31, new BattleOptions { AuraTier = 3 });
        b.EnemyCaptain.Pos = new Vector2(0, 400);
        b.Aura = 40;
        Run(b, 5);
        float still = b.Aura - 40;
        b.Aura = 40;
        b.Input = new PlayerInput { Move = new Vector2(0, 1) };
        Run(b, 5);
        float moving = b.Aura - 40;
        Check(still > moving * 1.5f && moving > 2, $"aura comes back faster standing still ({still:0.0}) than on the move ({moving:0.0})");

        b = Battle.Empty(32, new BattleOptions { AuraTier = 3, Forms = new[] { "aura_slash" } });
        b.EnemyCaptain.Pos = new Vector2(0, 400);
        b.Aura = Battle.AuraMax;
        var foes = b.AddSquad(1, Troops.Get("levies"), 6, b.Player.Pos + new Vector2(0, 2.2f), 6, -Vector2.UnitY).Units;
        for (int i = 0; i < 20 * 60 && b.Surge < Battle.SurgeMax; i++)
        {
            b.Aura = Battle.AuraMax;
            var t = foes.Where(f => f.Active).OrderBy(f => Vector2.DistanceSquared(f.Pos, b.Player.Pos)).FirstOrDefault();
            if (t == null) break;
            t.Hp = t.Type.MaxHp; // a foe that will not fall, to strike again and again
            var d = t.Pos - b.Player.Pos;
            b.Input = new PlayerInput { Attack = i % 40 < 20, Facing = MathF.Atan2(d.Y, d.X), Move = d.Length() > 1.6f ? Vector2.Normalize(d) : Vector2.Zero };
            b.Update(1 / 60f);
        }
        Check(b.Surge > 0, $"blows landed at full aura bank a surge ({b.Surge:0})");
        float surge = b.Surge, aura = b.Aura = 60;
        b.Input = new PlayerInput { Form1 = true };
        for (int i = 0; i < 30 && b.Input.Form1; i++) { b.Update(1 / 60f); if (b.FormWait(0) > 0) b.Input = new PlayerInput(); }
        var cost = Forms.Get("aura_slash").Aura;
        Check(b.FormWait(0) > 0 && b.Surge < surge && aura - b.Aura < cost, $"a form spends the surge first (aura {aura - b.Aura:0} of {cost:0})");

        b = Battle.Empty(33, new BattleOptions { AuraTier = 3 });
        b.Aura = 5;
        b.Player.Stamina = 20;
        Run(b, 1);
        float spentBreath = b.Player.Stamina - 20;
        var plain = Battle.Empty(33);
        plain.Player.Stamina = 20;
        Run(plain, 1);
        Check(spentBreath < plain.Player.Stamina - 20, "a spent captain gets his breath back slower than a plain one");

        bool Inspires(int tier, float away)
        {
            var g = Battle.Empty(34, new BattleOptions { AuraTier = tier });
            g.EnemyCaptain.Pos = new Vector2(0, 400);
            g.Player.Horse = null;
            var mine = g.AddSquad(0, Troops.Get("shieldmen"), 8, g.Player.Pos + new Vector2(away, -4), 8, Vector2.UnitY);
            mine.Order = Order.Hold;
            var foe = g.AddSquad(1, Troops.Get("levies"), 3, g.Player.Pos + new Vector2(0, 2.2f), 3, -Vector2.UnitY);
            foe.Order = Order.Hold;
            for (int i = 0; i < 4 * 60; i++)
            {
                var t = foe.Units.FirstOrDefault(f => f.Active);
                if (t != null) { var d = t.Pos - g.Player.Pos; g.Input = new PlayerInput { Attack = i % 40 < 20, Facing = MathF.Atan2(d.Y, d.X) }; }
                g.Update(1 / 60f);
            }
            return mine.Timed.Any(t => t.Effect.Id == "inspired" && t.Until > g.Time);
        }
        Check(Inspires(2, 0), "an aura knight fighting beside his company inspires it");
        Check(!Inspires(2, 60), "a company far off is not inspired");
        Check(!Inspires(0, 0), "a plain captain inspires nobody");
    }

    /// A captain with aura at this tier, the foe's captain sent far off.
    static Battle AuraField(int seed, int tier, params string[] standing)
    {
        var b = Battle.Empty(seed, new BattleOptions { AuraTier = tier, Standing = standing });
        b.EnemyCaptain.Pos = new Vector2(0, 400);
        return b;
    }

    /// A spell lands now, worked by this company, on this spot.
    static Cast CastNow(Battle b, Squad by, string spell, Vector2 at, float after = 0)
    {
        var c = new Cast { A = Abilities.Get(spell), By = by, At = at, Started = b.Time, FireAt = b.Time + after, Power = 1 };
        b.Casts.Add(c);
        return c;
    }

    /// MA5 (docs/archive/MAGIC_AURA.md 5): a Blessing or War song over you feeds your aura; one far off does not.
    static void BlessingFeedsAura()
    {
        var b = AuraField(81, 3);
        var mages = b.AddSquad(0, Troops.Get("chaplains"), 3, b.Player.Pos + new Vector2(4, 0), 3, Vector2.UnitY);
        mages.HoldSpells = true;
        b.Aura = 50;
        CastNow(b, mages, "blessing", b.Player.Pos);
        b.Update(1 / 60f);
        Check(b.Aura >= 50 + Battle.FeedAura - 0.5f, $"a blessing over you feeds your aura ({b.Aura:0})");
        b.Aura = 50;
        CastNow(b, mages, "war_song", b.Player.Pos + new Vector2(80, 0));
        b.Update(1 / 60f);
        Check(b.Aura < 52, "one far off does not");
    }

    /// MA5: an Evil eye on you quenches your blade (half as long behind aura armour, not at all at tier 5); Fever takes your breath.
    static void CurseOnCaptain()
    {
        float Quenched(int tier)
        {
            var b = AuraField(82, tier);
            var witches = b.AddSquad(1, Troops.Get("hedge_witches"), 3, new Vector2(0, 80), 3, -Vector2.UnitY);
            witches.HoldSpells = true;
            CastNow(b, witches, "evil_eye", b.Player.Pos);
            b.Update(1 / 60f);
            b.Input = new PlayerInput { Aura = true };
            float t0 = b.Time;
            for (int i = 0; i < 10 * 60 && !b.AuraBlade; i++) { b.Aura = Battle.AuraMax; b.Update(1 / 60f); }
            return b.Time - t0;
        }
        float plain = Quenched(2), armoured = Quenched(3), master = Quenched(5);
        Check(plain > Battle.QuenchTime - 0.5f, $"an Evil eye quenches the blade ({plain:0.0} s)");
        Check(armoured < plain * 0.7f && armoured > 1, $"aura armour halves it ({armoured:0.0} s)");
        Check(master < 0.2f, "at tier 5 the curse is turned");

        var f = AuraField(83, 2);
        var crones = f.AddSquad(1, Troops.Get("war_witches"), 3, new Vector2(0, 80), 3, -Vector2.UnitY);
        crones.HoldSpells = true;
        CastNow(f, crones, "fever", f.Player.Pos);
        f.Update(1 / 60f);
        Check(f.FeverUntil > f.Time + 2, "Fever on you takes your breath for a while");
    }

    /// MA5: your lit blade breaks a working of the caster it strikes (an unlit blow does not); with craft marks it leaves fever.
    static void BladeBreaksCast()
    {
        bool Breaks(bool lit)
        {
            var b = AuraField(84, 2, "craft_marks");
            var priests = b.AddSquad(1, Troops.Get("battle_priests"), 1, b.Player.Pos + new Vector2(0, 1.8f), 1, -Vector2.UnitY);
            priests.HoldSpells = true;
            priests.Order = Order.Hold;
            var cast = CastNow(b, priests, "smite", new Vector2(0, -60), after: 6);
            var t = priests.Units[0];
            for (int i = 0; i < 5 * 60 && b.Casts.Contains(cast); i++)
            {
                b.Aura = Battle.AuraMax;
                t.Hp = t.Type.MaxHp; // he stands to take blow after blow
                var d = t.Pos - b.Player.Pos;
                b.Input = new PlayerInput { Aura = lit, Attack = i % 40 < 20, Facing = MathF.Atan2(d.Y, d.X), Move = d.Length() > 1.6f ? Vector2.Normalize(d) : Vector2.Zero };
                b.Update(1 / 60f);
            }
            if (lit) Check(priests.Timed.Any(x => x.Effect.Id == "fever"), "craft marks leave fever on the company the blade strikes");
            return !b.Casts.Contains(cast) && b.Time < cast.FireAt;
        }
        Check(Breaks(true), "the lit blade breaks the caster's working");
        Check(!Breaks(false), "a plain blow does not");
    }

    /// MA5 (3.4): the relic raised once a battle blesses the companies round you and gives aura, over the top into the surge.
    static void RelicRaised()
    {
        var b = AuraField(85, 3, "relic");
        var men = b.AddSquad(0, Troops.Get("levies"), 8, b.Player.Pos + new Vector2(5, 0), 4, Vector2.UnitY);
        b.Aura = 90;
        b.Input = new PlayerInput { Relic = true };
        b.Update(1 / 60f);
        Check(b.RelicRaised && b.Aura >= Battle.AuraMax - 0.1f && b.Surge >= 19, $"the relic gives aura, the rest as surge ({b.Surge:0})");
        Check(men.Timed.Any(x => x.Effect.Id == "blessed"), "and blesses the company beside you");
        Check(b.CannotRaise(b.Player) != null, "once a battle");
        Check(b.Result().RelicRaised, "the campaign hears of it");
        Check(AuraField(86, 3).CannotRaise(b.Player) == "YOU CARRY NO RELIC", "no relic, nothing to raise");
    }

    /// MA6 (docs/archive/MAGIC_AURA.md 4.4): an unsworn Aura Expert who parries a lot is offered his own counter, a lancer his own
    /// leap (the one leap from the saddle); the form is named for his company, slots, and goes into battle.
    static void OwnFormFromStyle()
    {
        var c = Campaign.New(87);
        c.Captain.AuraTier = 3;
        var fight = new BattleResult(0, 60, Array.Empty<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Unhurt, 0, Parries: 20, Ripostes: 8, LanceHits: 3);
        c.StyleAfterBattle(fight);
        Check(c.Captain.OwnFormOffer.Count == 2 && c.Captain.OwnFormOffer[0] == "answer" && c.Captain.OwnFormOffer[1] == "charge",
            $"a parrying captain is offered his Answer, then his Charge ({string.Join(",", c.Captain.OwnFormOffer)})");
        Check(c.ChooseOwnForm("mark") != null, "only a style he fights in");
        Check(c.ChooseOwnForm("answer") == null && c.Captain.OwnForm is { Kind: "counter" } f && f.Name.EndsWith("Answer"), "his Answer is a counter named for his company");
        Check(Campaign.SlottedForms(c.Captain)[0] == Forms.OwnId && !c.MayMakeForm, "it goes in the first slot; one form of his own");
        Check(c.Captain.OwnForm!.Damage > Forms.Get("measured_blade").Damage, "a tenth stronger than the form it grew from");
        var o = new BattleOptions().WithCaptain(c.Captain);
        var b = new Battle(5, Armies.Standard(0), Armies.Standard(1), o);
        Check(b.FormSlot[0] is { Id: Forms.OwnId, Kind: "counter" }, "it goes into battle");
        Check(c.OrderFormNow == Forms.OwnId, "and it is what his own order would teach");
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        Check(back.Captain.OwnForm?.Name == c.Captain.OwnForm.Name && Forms.Has(Forms.OwnId), "it keeps through a save");

        var l = Campaign.New(88);
        l.Captain.AuraTier = 3;
        l.StyleAfterBattle(new BattleResult(0, 60, Array.Empty<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Unhurt, 0, SaddleKills: 10, LanceHits: 20));
        Check(l.Captain.OwnFormOffer.FirstOrDefault() == "charge" && l.MakeOwnForm("charge").Kind == "leap", "a lancer is offered his Charge, a leap");
        var sworn = Campaign.New(89);
        sworn.Captain.AuraTier = 3; sworn.Captain.Order = "rose";
        sworn.StyleAfterBattle(fight);
        Check(sworn.Captain.OwnFormOffer.Count == 0, "a sworn knight fights in his order's forms");
    }

    /// MA6 (4.5): each aspect moves its number and only that; a form takes one at rank 3, once, and the battle feels it.
    static void AspectChanges()
    {
        string[] fields = { "Radius", "Range", "Duration", "Damage", "Shock", "Cooldown", "Aura", "MaxHit", "Count" };
        float Get(FormDef f, string n) => Convert.ToSingle(typeof(FormDef).GetField(n)!.GetValue(f));
        foreach (var a in Forms.Aspects)
        {
            var meant = fields.Where(n => Convert.ToSingle(typeof(AspectDef).GetField(n)!.GetValue(a)) != (n is "MaxHit" or "Count" ? 0 : 1)).ToList();
            var f = Forms.All.Where(x => x.Kind == a.Kind).FirstOrDefault(x => meant.All(n => n is "MaxHit" or "Count" || Get(x, n) != 0));
            Check(f != null && meant.Count > 0, $"aspect {a.Id} has a form whose number it moves");
            if (f == null) continue;
            var moved = fields.Where(n => MathF.Abs(Get(a.On(f), n) - Get(f, n)) > 1e-4f).ToList();
            Check(moved.SequenceEqual(meant), $"aspect {a.Id} moves {string.Join(",", meant)} only ({string.Join(",", moved)})");
        }
        var c = Campaign.New(90);
        c.Captain.AuraTier = 3;
        c.Captain.Forms = new() { "aura_slash" };
        Check(c.TakeAspect("aura_slash", "slash_wide") == "AT RANK III", "an aspect waits for rank III");
        c.Captain.FormUses["aura_slash"] = Campaign.FormRankUses * 2;
        Check(c.TakeAspect("aura_slash", "leap_far") != null, "only an aspect of its kind");
        Check(c.TakeAspect("aura_slash", "slash_wide") == null && c.TakeAspect("aura_slash", "slash_far") != null, "one aspect, for good");
        var b = new Battle(6, Armies.Standard(0), Armies.Standard(1), new BattleOptions { AuraTier = 3 }.WithCaptain(c.Captain));
        Check(b.FormSlot[0] is { } s && MathF.Abs(s.Radius - Forms.Get("aura_slash").Radius * 1.5f) < 1e-4f, "the wide slash is wider in battle");
    }

    /// MA7 (docs/archive/MAGIC_AURA.md 4.6): at your wall, felling a champion of your tier breaks it on the spot: the tier rises in the
    /// fight, the pool fills, the men near you rally, and the campaign hears of it. Not at the wall, nothing.
    static void BreakInBattle()
    {
        Battle Field(bool wall)
        {
            var b = Battle.Empty(91, new BattleOptions { AuraTier = 3, EnemyAura = 3, AtWall = wall });
            b.EnemyCaptain.Pos = b.Player.Pos + new Vector2(0, 3);
            return b;
        }
        var b = Field(true);
        var men = b.AddSquad(0, Troops.Get("levies"), 6, b.Player.Pos + new Vector2(6, 0), 3, Vector2.UnitY);
        foreach (var u in men.Units) { u.Routing = true; u.Morale = 5; }
        b.Aura = 30;
        b.Kill(b.EnemyCaptain, b.Player);
        Check(b.BrokeThrough && b.AuraTier == 4 && b.Aura >= Battle.AuraMax, $"felling a champion at the wall breaks it in the field (tier {b.AuraTier})");
        Check(men.Units.All(u => !u.Routing && u.Morale > Battle.RallyAt), "the men near you rally");
        Check(b.Result().BrokeThrough, "the campaign hears of it");
        var n = Field(false);
        n.Kill(n.EnemyCaptain, n.Player);
        Check(!n.BrokeThrough && n.AuraTier == 3, "away from the wall, no breakthrough");
    }

    /// MA7 (4.7): two lit blades close start a clash: the men keep out of the ring, and it ends within its time.
    static void ClashRing()
    {
        var b = Battle.Empty(92, new BattleOptions { AuraTier = 3, EnemyAura = 3 });
        b.EnemyCaptain.Pos = b.Player.Pos + new Vector2(0, 2.2f);
        b.AddSquad(0, Troops.Get("levies"), 12, b.Player.Pos + new Vector2(0, -4), 6, Vector2.UnitY);
        b.AddSquad(1, Troops.Get("levies"), 12, b.Player.Pos + new Vector2(0, 7), 6, -Vector2.UnitY);
        float started = -1, worst = 99;
        for (int i = 0; i < 14 * 60; i++)
        {
            b.Aura = Battle.AuraMax;
            b.Player.Hp = b.Player.Type.MaxHp; b.EnemyCaptain.Hp = b.EnemyCaptain.Type.MaxHp; // both stand to the end
            var d = b.EnemyCaptain.Pos - b.Player.Pos;
            b.Input = new PlayerInput { Aura = true, Facing = MathF.Atan2(d.Y, d.X), Attack = i % 40 < 20 };
            b.Update(1 / 60f);
            if (b.ClashOn && started < 0) started = b.Time;
            if (b.ClashOn)
                foreach (var u in b.Units)
                    if (u.Active && !u.IsPlayer && u != b.EnemyCaptain) worst = MathF.Min(worst, Vector2.Distance(u.Pos, b.ClashAt));
        }
        Check(started >= 0, "two lit blades close start a clash");
        Check(worst >= Battle.ClashRadius - 0.6f, $"the men keep out of the ring (nearest {worst:0.0} m)");
        Check(!b.ClashOn && b.ClashUntil <= started + Battle.ClashTime + 0.01f, "it ends within its time");
    }

    /// MA7 (4.8): a Transcendent's light turns curses from the companies near him, and his lit blade cuts arrows from the air.
    static void TranscendentLight()
    {
        bool Cursed(int tier)
        {
            var b = AuraField(93, tier);
            var men = b.AddSquad(0, Troops.Get("levies"), 8, b.Player.Pos + new Vector2(5, 0), 4, Vector2.UnitY);
            var witches = b.AddSquad(1, Troops.Get("war_witches"), 3, new Vector2(0, 80), 3, -Vector2.UnitY);
            witches.HoldSpells = true;
            CastNow(b, witches, "fever", men.Center);
            b.Update(1 / 60f);
            return men.Timed.Any(x => x.Effect.Id == "fever");
        }
        Check(Cursed(4) && !Cursed(5), "a Transcendent's light turns a curse from the company beside him");

        int Cut(bool lit)
        {
            var a = AuraField(94, 5);
            var bows = a.AddSquad(1, Troops.Get("bowmen"), 4, a.Player.Pos + new Vector2(0, 40), 4, -Vector2.UnitY);
            int hits = 0;
            for (int i = 0; i < 40; i++)
            {
                a.Aura = Battle.AuraMax;
                a.Player.Hp = a.Player.Type.MaxHp;
                a.Input = new PlayerInput { Aura = lit, Facing = MathF.PI / 2 }; // facing the shots
                var p = a.Player.Pos;
                a.Arrows.Add(new Projectile { Pos = new Vector3(p.X, a.Terrain.Height(p) + 1.2f, p.Y + 3), Vel = new Vector3(0, 0, -25), Shooter = bows.Units[0], Damage = 1 });
                float hp = a.Player.Hp;
                for (int k = 0; k < 15; k++) a.Update(1 / 60f);
                if (a.Player.Hp < hp) hits++;
            }
            Check(!lit || hits > 5, $"some arrows still get through ({hits} of 40)");
            return a.ArrowsCut;
        }
        int cut = Cut(true);
        Check(cut is > 8 and < 32, $"his lit blade cuts about half the arrows from the air ({cut} of 40)");
        Check(Cut(false) == 0, "an unlit blade cuts none");
    }

    /// Aura everyone can find (docs/ARCANA.md AR1): won battles wake it at the first wall (a fought one counts two), losses do
    /// not, the lists break the low walls, and the trial card names a way through and where.
    static void AwakeningByDeeds()
    {
        var c = Campaign.New(120);
        var cap = c.Captain;
        cap.AuraWall = true; cap.AuraXp = Campaign.AuraNeeds[0];
        var auto = new BattleResult(0, 0, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Unhurt, 0);
        var lost = auto with { Winner = 1 };
        var fought = auto with { Duration = 90, PlayerKills = 1 };
        for (int i = 0; i < 20; i++) c.AuraAfterBattle(lost, false, 1);
        Check(cap.AuraTier == 0 && cap.WallWins == 0, "lost battles do not wake it");
        for (int i = 0; i < Campaign.SeasonWins - 3; i++) c.AuraAfterBattle(auto, true, 0.5f);
        Check(cap.AuraTier == 0 && cap.AuraWall, "not yet");
        c.AuraAfterBattle(fought, true, 0.5f);
        c.AuraAfterBattle(auto, true, 0.5f);
        Check(cap.AuraTier == 1 && !cap.AuraWall && cap.WallWins == 0, $"twelve wins (a fought one counting two) wake it (tier {cap.AuraTier}, {cap.WallWins})");
        Check(cap.Forms.Contains("focus"), "and the first form comes with it");
        // Seasoning wakes only the first wall.
        cap.AuraWall = true; cap.AuraXp = Campaign.AuraNeeds[1];
        for (int i = 0; i < 30; i++) c.AuraAfterBattle(auto, true, 0.5f);
        Check(cap.AuraTier == 1, "won battles alone do not break the walls above");
        // The lists: a trial like a duel.
        var town = c.S.Settlements.First(t => t.Kind == SettlementKind.Town);
        int tries = 0;
        while (cap.AuraTier == 1 && tries++ < 40) c.AutoTournament(town);
        Check(cap.AuraTier == 2, $"a won tournament breaks the wall below Aura Knight ({tries} tries)");
        cap.AuraWall = true; cap.AuraXp = Campaign.AuraNeeds[2];
        for (int i = 0; i < 40; i++) c.AutoTournament(town);
        Check(cap.AuraTier == 2, "but not the walls above it");
        // The card.
        var card = c.NearestTrials();
        Console.WriteLine("trials: " + string.Join(" | ", card));
        Check(card.Any(l => l.StartsWith("VIGIL: THE ") && l.Contains(" DAY")), "the card names the nearest vigil and how far");
        Check(card.Any(l => l.StartsWith("ODDS:")) && !card.Any(l => l.StartsWith("BATTLE:")), "the trials of this wall, not the first wall's");
        cap.AuraWall = false;
        Check(c.NearestTrials().Count == 0, "no wall, no card");
    }

    /// The captain's paths (docs/ARCANA.md AR6): chosen once when the aura wakes, each changes the battle through its own
    /// system: the Sword's forms cost less, the Banner inspires from the first tier and further off, the Hearth's circle draws
    /// half from the Tide near him, takes his aura and is anchored against backlash.
    static void PathsDiffer()
    {
        Check(!Forms.Validate().Any(), "forms.json is valid: " + string.Join("; ", Forms.Validate()));
        Check(!Forms.Generic.Any(f => f.Path != "") && Forms.OfPath("banner").Count() == 3 && Forms.OfPath("hearth").Count() == 3, "path forms are their path's alone");
        var c = Campaign.New(130);
        Check(c.ChoosePath("banner") == "YOUR AURA HAS NOT WOKEN", "no path before the aura wakes");
        c.Captain.AuraTier = 1;
        Check(c.ChoosePath("banner") == null && c.Captain.Path == "banner" && c.Captain.Forms.Contains("hold_fast") && !c.Captain.Forms.Contains("steady_them"), "the Banner: its first form at once");
        Check(c.ChoosePath("sword") != null, "a path is chosen once");
        c.Captain.AuraWall = true; c.BreakThrough("t"); c.Captain.AuraWall = true; c.BreakThrough("t");
        Check(c.Captain.AuraTier == 3 && c.Captain.Forms.Contains("steady_them"), "its next form comes with the tier");
        Check(new BattleOptions().WithCaptain(c.Captain).Path == "banner", "the path goes into battle");

        // The Sword: forms cost less.
        float Spent(string path)
        {
            var b = Battle.Empty(131, new BattleOptions { AuraTier = 3, Forms = new[] { "focus" }, Path = path });
            b.Player.Horse = null;
            b.Input = new PlayerInput { Facing = MathHelper.PiOver2 };
            b.Update(1 / 60f);
            b.Aura = Battle.AuraMax; b.Surge = 0;
            Press(b, 0);
            return Battle.AuraMax - b.Aura;
        }
        float plain = Spent(""), sword = Spent("sword");
        Check(MathF.Abs(sword - plain * Battle.SwordThrift) < 0.6f, $"the Sword's forms cost less ({sword:0.0} of {plain:0.0})");

        // The Banner: inspires at the first tier and further off.
        bool Inspires(string path)
        {
            var (b, _) = FormField(132, 1, Array.Empty<string>());
            b.Options.Path = path;
            var own = b.AddSquad(0, Troops.Get("shieldmen"), 6, b.Player.Pos + new Vector2(-13, 0), 6, Vector2.UnitY);
            own.Order = Order.Hold;
            Run(b, 2);
            return own.Timed.Any(t => t.Effect.Id == "inspired");
        }
        Check(Inspires("banner") && !Inspires(""), "a Banner captain inspires a company 13 m off at the first tier; others do not");

        // The Hearth: the circle near him draws half, takes his aura, is anchored.
        var (h, _) = FormField(133, 3, new[] { "lend_aura", "anchor" });
        h.Options.Path = "hearth";
        var near = h.AddSquad(0, Troops.Get("skalds"), 3, h.Player.Pos + new Vector2(-6, -4), 3, Vector2.UnitY);
        var far = h.AddSquad(0, Troops.Get("skalds"), 3, h.Player.Pos + new Vector2(-80, -4), 3, Vector2.UnitY);
        near.Order = far.Order = Order.Hold; near.HoldSpells = far.HoldSpells = true;
        h.Update(1 / 60f);
        var storm = Abilities.Get("storm");
        Check(h.TideDraw(storm, 30, near) == h.TideDraw(storm, 30, far) * Battle.HearthDraw, "the circle near a Hearth captain draws half from the Tide");
        near.Will = 10;
        h.Aura = Battle.AuraMax;
        Press(h, 0);
        Check(near.Will > 40, $"Lend Aura fills the circle's Will ({near.Will:0})");
        Check(!h.Anchored(near), "not anchored yet");
        h.Aura = Battle.AuraMax;
        Press(h, 1);
        Check(h.Anchored(near) && !h.Anchored(far), "Anchor holds the working of the circle near him");
    }
}

