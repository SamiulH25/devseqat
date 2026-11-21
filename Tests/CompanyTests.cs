using Microsoft.Xna.Framework;

namespace Ironfield;

/// The men of the company (docs/archive/COMPANY.md C1-C7).
public static partial class SelfTest
{
    /// C1: two named men look different; one man's face is the same after a save and load; wounds scar him; and the face on
    /// the roll is the face the battle draws.
    static void MenHaveFaces()
    {
        var c = Campaign.New(28);
        var co = c.Player.Companies.First(k => k.Men >= 4);
        var a = c.NameMan(co, "stood firm")!;
        var b = c.NameMan(co, "held the ford")!;
        Check(a != null && b != null, "two men of the company earn names");
        var fa = PortraitPainter.Of(a, co);
        var fb = PortraitPainter.Of(b, co);
        Check(!PortraitPainter.Paint(fa).SequenceEqual(PortraitPainter.Paint(fb)), "two named men have different faces");
        Check(a.Age is >= 17 and <= 30, $"a man is 17 to 30 when named (he is {a.Age})");

        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        var again = back.Player.Companies.SelectMany(k => k.Roll).First(m => m.Id == a.Id);
        Check(PortraitPainter.Of(again, back.Player.Companies.First(k => k.Roll.Contains(again))) == fa, "a man's face is the same after a save and load");

        a.Wounds = 2;
        Check(PortraitPainter.Of(a, co).Scar > fa.Scar, "wounds leave a scar");

        // The battle's look of the same man: same visage, so the same skin and hair.
        var t = Troops.Get(a.Troop);
        var bt = Battle.Empty(28);
        var sq = bt.AddSquad(0, t, 1, new Vector2(0, -10), 1, Vector2.UnitY);
        sq.Units[0].SoldierId = a.Id;
        var (skin, hair, _) = Variety.Parts(Variety.Of(sq.Units[0]).Visage);
        Check(skin == fa.Skin && hair == fa.Hair, "the face on the roll is the face the battle draws");

        // He grows older with the year.
        int age = a.Age;
        c.S.Hours = Campaign.DaysPerYear * 24; // the first day of the second year
        c.AgeDaily();
        Check(a.Age == age + 1, "named men grow a year older each year");
    }

    /// C2: natures are drawn on the roll's dice (the main stream does not move); a company feels one nature in battle, its
    /// senior man's; an old save's men get theirs on load; Merry and Gentle act off the field.
    static void MenHaveNatures()
    {
        var c = Campaign.New(29);
        var co = c.Player.Companies.First(k => k.Men >= 6);
        ulong main = c.S.Rng;
        var men = Enumerable.Range(0, 4).Select(i => c.NameMan(co, i % 2 == 0 ? "Stood when half the company fell." : "Killed 5.")).OfType<Soldier>().ToList();
        Check(men.Count >= 3 && men.All(m => Campaign.Natures.Contains(m.Nature)), "every named man has a nature");
        Check(c.S.Rng == main, "natures come from the roll's dice: the main stream does not move");

        // One nature a company: the senior man's, carried into battle as the company's standing.
        foreach (var m in co.Roll) m.Battles = 1;
        var senior = co.Roll.First(m => m.Nature is not ("Merry" or "Gentle"));
        senior.Battles = 9;
        var spec = Rules.Army("you", new[] { c.Player }).Companies.First(k => k.Id == co.Id);
        Check(spec.Standing is { Length: 1 } st && st[0] == "nature_" + senior.Nature.ToLowerInvariant(), $"the company carries one nature into battle, its senior man's ({senior.Nature})");
        var b = Battle.Empty(29);
        var sq = b.AddSquad(0, Troops.Get(co.Troop), 4, new Vector2(0, -10), 4, Vector2.UnitY, spec);
        Check(sq.StandingIds.Count(id => id.StartsWith("nature_")) == 1, "one nature counted in the battle");

        // Old saves: men without a nature get one on load, the same every time.
        foreach (var m in co.Roll) m.Nature = "";
        var a1 = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        var a2 = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        var n1 = a1.Player.Companies.SelectMany(k => k.Roll).Select(m => m.Nature).ToList();
        Check(n1.All(n => n != "") && n1.SequenceEqual(a2.Player.Companies.SelectMany(k => k.Roll).Select(m => m.Nature)), "an old save's men get natures on load, the same each time");

        // Auto-resolve weighs a nature a little.
        foreach (var m in co.Roll) m.Nature = "Bold";
        float with = Rules.Might(c.Player);
        foreach (var m in co.Roll) m.Nature = "Merry";
        Check(Rules.Might(c.Player) < with, "auto-resolve weighs a battle nature a little");
    }

    /// C3: ties form by the rules (brothers named in one battle, a lad to a veteran), follow men through a merge and a save,
    /// and the fall of one marks the other: a memory, vengeance for his next battle, and the company misses him more.
    static void MenHaveTies()
    {
        var c = Campaign.New(30);
        c.S.Hours = 24 * 5; // past the first days, when the starting roll is named
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Roll.Clear();
        var a = c.NameMan(co, "Held the ford.")!;
        var b = c.NameMan(co, "Held the ford.")!;
        Check(a != null && b != null && a.Ties.Any(t => t.Other == b.Id && t.Kind == "Brothers") && b.Ties.Any(t => t.Other == a.Id), "two men named in one battle are brothers");

        // A veteran of ten battles takes a newly named man as his lad (one in two: try a few).
        var vet = c.NameMan(co, "Stood firm.")!;
        vet.Ties.Clear(); vet.Battles = 12;
        Soldier? lad = null;
        for (int i = 0; i < 12 && lad == null; i++)
        {
            c.S.Hours += 24; // another day, so not brothers
            var n = c.NameMan(co, "Killed 3.");
            if (n != null && n.Ties.Any(t => t.Other == vet.Id && t.Kind == "Lad")) lad = n;
            else if (n != null) { co.Roll.Remove(n); foreach (var m in co.Roll) m.Ties.RemoveAll(x => x.Other == n.Id); } // not his: try another
        }
        Check(lad != null && vet.Ties.Any(t => t.Kind == "Master" && t.Other == lad!.Id), "a veteran takes a newly named man as his lad");
        Check(co.Roll.All(m => m.Ties.Count <= Campaign.MostTies), "no man has more than two ties");

        // Ties follow men to another company, and through a save.
        var other = c.Player.Companies.First(k => k != co);
        Campaign.MoveNamed(co, other, b.Troop);
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        var b2 = back.Player.Companies.SelectMany(k => k.Roll).First(m => m.Id == b.Id);
        Check(b2.Ties.Any(t => t.Other == a.Id && t.Kind == "Brothers"), "a tie survives a move to another company and a save");
        Check(back.TieLine(b2).Contains(a.Name.ToUpperInvariant()), "the roll names his brother");

        // The fall of one marks the other.
        var home = c.Player.Companies.First(k => k.Roll.Contains(a));
        float coh = home.Cohesion = 50;
        c.Lose(home, a, "fell", "the Grey Ford");
        Check(b.Avenging && b.Ties.All(t => t.Other != a.Id) && b.Deeds[^1].Contains(a.Name), "his brother remembers his fall and fights to avenge him");
        Check(coh - home.Cohesion > Balance.Get().FallenCohesion * 1.2f, "a man with a brother is missed half again");
        var spec = Rules.Army("you", new[] { c.Player }).Companies.SelectMany(k => k.Named ?? Array.Empty<NamedSpec>()).FirstOrDefault(n => n.Id == b.Id);
        Check(spec != null && spec.Avenging, "the avenger carries it into battle");

        // In battle an avenger strikes harder.
        var t = Troops.Get(b.Troop);
        float dmg(bool av)
        {
            var bt = Battle.Empty(30);
            var us = bt.AddSquad(0, t, 1, new Vector2(0, 0), 1, Vector2.UnitY);
            var them = bt.AddSquad(1, t, 1, new Vector2(0, 1), 1, Vector2.UnitY); // facing away: a blow from behind is not blocked
            us.Units[0].Avenging = av;
            var v = them.Units[0]; float hp = v.Hp;
            bt.Hit(us.Units[0], v, 10, ranged: false);
            return hp - v.Hp;
        }
        Check(dmg(true) > dmg(false), "an avenger's blows land harder");

        // A master's fall: his lad takes up his ways.
        string ways = vet.Nature == "Bold" ? "Grim" : "Bold";
        vet.Nature = ways;
        c.Lose(c.Player.Companies.First(k => k.Roll.Contains(vet)), vet, "fell", "");
        Check(lad!.Nature == ways, "a lad whose master fell takes his nature");
    }

    /// Sending some men of a stack to another company: the men and their share of the pool go, the rest stay; no room
    /// and the wrong kind are refused; sending every man moves the stack and an emptied company is struck off.
    static void MoveMenBetween()
    {
        var c = Campaign.New(77);
        var a = c.Player.Companies[0];
        string? err = c.Split(a);
        Check(err == null, "a company splits in two: " + err);
        var b = c.Player.Companies[1];
        var st = a.Stacks[0];
        st.Xp = st.PoolCap * 0.5f;
        int menA = a.Men, menB = b.Men, all = c.Player.Men;
        float pool = st.Xp + b.Stack(st.Troop).Xp;
        Check(c.MoveMen(a, st, b, 5) == null, "five men go over");
        Check(a.Men == menA - 5 && b.Men == menB + 5 && c.Player.Men == all, "five left one company and joined the other, none lost");
        Check(MathF.Abs(st.Xp + b.Stack(st.Troop).Xp - pool) < 1f, "their share of the XP pool went with them");
        Check(c.MoveMen(a, st, a, 1) != null, "not into the same company");
        Check(c.MoveMen(a, st, b, 0) != null, "not no men at all");
        b.Stack(st.Troop).Men += 400; // far past its places
        Check(c.MoveMen(a, st, b, 3) == "NO ROOM", "a full company takes no more");
        b.Stack(st.Troop).Men -= 400;
        int left = st.Men, count = c.Player.Companies.Count;
        if (a.Stacks.Count == 1 && st.Wounded == 0 && Rules.Room(b, st.Troop) >= left)
        {
            Check(c.MoveMen(a, st, b, left) == null, "the last men go over");
            Check(c.Player.Companies.Count == count - 1 && c.Player.Men == all, "the emptied company is struck off, no man lost");
        }
    }

    /// C4: your hand on the roll. Each action's refusals and effects; the unanswered sergeant choice falls back to the risen
    /// man; what you did survives a save.
    /// The camp (C5): its mood from the last battle and how long ago, a fire for every company with men, the hurt apart.
    static void CampKnowsMood()
    {
        var c = Campaign.New(33);
        foreach (var k in c.Player.Companies) k.Ledger.Clear();
        Check(c.MoodOfCamp() == Campaign.CampMood.Quiet, "a fresh company's camp is quiet");
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Ledger.Add(new LedgerEntry { Day = c.Day, Won = true });
        Check(c.MoodOfCamp() == Campaign.CampMood.Won, "the men cheer a field won today");
        co.Ledger.Add(new LedgerEntry { Day = c.Day, Won = false });
        Check(c.MoodOfCamp() == Campaign.CampMood.Lost, "the last battle sets the mood");
        c.S.Hours += 24 * 5;
        Check(c.MoodOfCamp() == Campaign.CampMood.Quiet, "five days on the camp is quiet again");
        Check(c.CampFires().Count == c.Player.Companies.Count(k => k.Men > 0), "a fire for every company with men");
        co.Roll.Clear();
        var a = c.NameMan(co, "Stood firm.")!; var b = c.NameMan(co, "Held the ford.")!;
        b.Hurt = true;
        var (fit, hurt) = Campaign.AtFire(co);
        Check(fit.Contains(a) && !fit.Contains(b) && hurt.Contains(b), "the hurt lie at the surgeon's, the fit stand at the fire");
        Check(c.CampFires()[0] == co, "the company with named men keeps the first fire");
    }

    /// The camp's events (C5): the data knows the `men_*` triggers, holes and verbs; a trigger finds only living men; each verb
    /// does what it says; the realm's dice do not draw them.
    static void CampEvents()
    {
        Check(!Campaign.ValidateEvents().Any(), "events.json is valid: " + string.Join("; ", Campaign.ValidateEvents().Take(3)));
        Check(Campaign.Events.Count(e => Campaign.MenTriggers.Contains(e.Trigger)) >= 15, "the camp has its events");
        var c = Campaign.New(35);
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Roll.Clear();
        var a = c.NameMan(co, "Stood firm.")!; var b = c.NameMan(co, "Held the ford.")!;
        a.Ties.Clear(); b.Ties.Clear();
        a.Ties.Add(new SoldierTie { Other = b.Id, Kind = "Rivals" }); b.Ties.Add(new SoldierTie { Other = a.Id, Kind = "Rivals" });
        var pe = c.FindMen("men_rivals");
        Check(pe != null && (pe.Soldier == a.Id && pe.Other == b.Id || pe.Soldier == b.Id && pe.Other == a.Id), "two rivals are found as a pair");
        ulong main = c.S.Rng;
        for (int i = 0; i < 20; i++) c.FindMen("men_named");
        Check(c.S.Rng == main, "the camp's events draw on the roll's dice, not the realm's");

        // Words: {man}, {other} and {company} come out as the men's names.
        Check(c.RaiseMenEvent("men_blows") != null && c.S.Event != null, "a camp event comes up");
        string text = c.Fill(Campaign.EventOf("men_blows")!.Text, c.S.Event!);
        Check(text.Contains(a.Name) && text.Contains(b.Name) && text.Contains(co.Name), $"the event names the two men and their company: {text}");
        // tie: rivals made brothers.
        c.Choose(0);
        Check(a.Ties.Any(t => t.Other == b.Id && t.Kind == "Brothers") && b.Ties.Any(t => t.Other == a.Id && t.Kind == "Brothers"), "a night's watch together makes them brothers");
        // man_deed and man_hurt.
        a.Ties.Clear(); b.Ties.Clear();
        a.Ties.Add(new SoldierTie { Other = b.Id, Kind = "Rivals" }); b.Ties.Add(new SoldierTie { Other = a.Id, Kind = "Rivals" });
        c.RaiseMenEvent("men_blows");
        var man = c.LivingMan(c.S.Event!.Soldier)!;
        int deeds = man.Deeds.Count, wounded = co.Stacks.Sum(s => s.Wounded);
        c.Choose(1);
        Check(man.Hurt && man.Deeds[^1].StartsWith("Fought") && co.Stacks.Sum(s => s.Wounded) == wounded + 1, "letting them finish it hurts one, and he remembers it");
        Check(Campaign.AtFire(co).Hurt.Contains(man), "the hurt man goes to the surgeon's");
        // A hurt man is found by men_hurt and no longer by men_named.
        for (int i = 0; i < 10; i++) Check(c.FindMen("men_named")?.Soldier != man.Id, "a hurt man is not at the fire");
        // mention.
        var fit = a == man ? b : a;
        c.S.Event = new PendingEvent { Id = "men_watch", Soldier = fit.Id, Company = co.Id };
        c.Choose(0);
        Check(fit.Mentions == 1, "mention counts toward the banner");
        // man_leaves: he goes from the roll alive, and is never named by an event again.
        int men = co.Men;
        c.S.Event = new PendingEvent { Id = "men_going_home", Soldier = fit.Id, Company = co.Id };
        c.Choose(1);
        Check(!co.Roll.Contains(fit) && c.S.Fallen.Any(m => m.Id == fit.Id && m.Fate == "left") && co.Men == men - 1, "a man who goes home leaves the roll, and his stack");
        for (int i = 0; i < 10; i++)
            foreach (var t in Campaign.MenTriggers)
                if (c.FindMen(t) is PendingEvent f) Check(c.LivingMan(f.Soldier) != null && (f.Other < 0 || c.LivingMan(f.Other) != null), $"{t} names only living men");
        // A man gone before you answer: the event leaves him alone.
        c.S.Event = new PendingEvent { Id = "men_first_charge", Soldier = fit.Id, Company = co.Id };
        int before = fit.Deeds.Count;
        c.Choose(0);
        Check(fit.Deeds.Count == before, "a man no longer on the roll gets no deed");
    }

    /// The men's talk (C5): the lines validate; every man can answer every question, by what is true and by his nature.
    static void CampTalk()
    {
        Check(!Campaign.ValidateMenTalk().Any(), "the men's lines are valid: " + string.Join("; ", Campaign.ValidateMenTalk().Take(3)));
        Check(!Campaign.ValidateTalk().Any(), "the lords' lines still are");
        var c = Campaign.New(37);
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Roll.Clear();
        var a = c.NameMan(co, "Stood firm.")!; var b = c.NameMan(co, "Held the ford.")!;
        a.Ties.Clear(); b.Ties.Clear();
        a.Ties.Add(new SoldierTie { Other = b.Id, Kind = "Brothers" }); b.Ties.Add(new SoldierTie { Other = a.Id, Kind = "Brothers" });
        foreach (var n in Campaign.Natures)
        {
            a.Nature = n;
            foreach (var t in new[] { "man_greet" }.Concat(c.TopicsForMan(a).Select(x => x.Id)))
            {
                string line = c.ManLine(a, co, t);
                Check(line != "" && !line.Contains('{'), $"{n} answers {t}: \"{line}\"");
            }
        }
        Check(c.TopicsForMan(a).Count() == 3 && c.ManLine(a, co, "man_tie").Contains(b.Name), "he talks about his brother by name");
        c.Player.Food = 0;
        Check(c.CampState(co) == "hungry", "hunger comes first");
        a.Nature = "Steady";
        Check(c.ManLine(a, co, "man_men") == c.ManLine(a, co, "man_men"), "the same man says the same thing all day");
        Check(c.ManLine(a, co, "man_men").Contains("food") || c.ManLine(a, co, "man_men").Contains("bread") || c.ManLine(a, co, "man_men").Contains("Hungry"), "a hungry camp is told true");
    }

    static void RollChoices()
    {
        var c = Campaign.New(31);
        c.S.Gold = 1000;
        var co = c.Player.Companies.OrderByDescending(k => k.Men).First();
        co.Roll.Clear();
        var men = Enumerable.Range(0, 4).Select(_ => c.NameMan(co, "Stood firm.")!).ToList();
        foreach (var m in men) { m.Ties.Clear(); m.Battles = 4; }
        men[3].Battles = 2; // not a candidate: he stays for the pension
        men[0].Battles = 9;

        // The sergeant: the senior rises, and you may choose another for a day; his nature becomes a trait.
        co.Officer = ""; co.Traits.Clear();
        int menBefore = co.Men;
        co.Officer = c.RiseToSergeant(co, "Sgt Drawn");
        Check(co.OfficerMan == men[0] && c.SergeantChoice(co).Count >= 2, "the senior man rises and others may be chosen");
        var other = c.SergeantChoice(co)[1];
        other.Nature = "Bold";
        Check(c.CannotChooseSergeant(co, men[3]) == null || c.SergeantChoice(co).Contains(men[3]) == false, "only the candidates may be chosen");
        Check(c.ChooseSergeant(co, other) == null && co.OfficerMan == other && co.Roll.Contains(men[0]) && !co.Roll.Contains(other), "you choose another sergeant; the risen man goes back to the ranks");
        Check(co.Men == menBefore - 1 && co.Officer.Contains(other.Name), "one man leaves the ranks for the post, as before");
        Check(co.Traits.Contains("Hothead"), "a Bold sergeant makes the company Hothead");
        Check(c.SergeantChoice(co).Count == 0, "the choice is made once");
        // Unanswered: after the day the risen man keeps it.
        var co2 = c.Player.Companies.First(k => k != co && k.Men >= 4);
        co2.Roll.Clear();
        var a = c.NameMan(co2, "Held.")!; var b = c.NameMan(co2, "Held.")!;
        a.Battles = 6; b.Battles = 5; co2.Officer = "";
        co2.Officer = c.RiseToSergeant(co2, "Sgt Drawn");
        Check(c.SergeantChoice(co2).Count == 2, "a choice opens for the second company");
        c.S.Hours += 24 * (Campaign.SergeantPickDays + 1);
        Check(c.SergeantChoice(co2).Count == 0 && co2.OfficerMan == a, "unanswered, the risen man keeps the post");

        // Mention a man: only after a won battle, once a battle; three make him of the banner.
        var hero = co.Roll.First(m => m == men[0]); // back in the ranks
        Check(c.CannotMention(hero) == "ONLY AFTER A WON BATTLE", "no mention without a won battle");
        float coh = co.Cohesion = 10;
        float gold = c.S.Gold;
        for (int i = 0; i < 3; i++) { c.S.MentionPool.Add(hero.Id); Check(c.Mention(hero) == null, "mention a man of the day"); }
        Check(c.S.Gold == gold - 3 * Balance.Get().MentionPurse && co.Cohesion == coh + 3 && c.S.MentionPool.Count == 0, "a mention pays a purse and lifts the company");
        Check(Campaign.OfBanner(hero) && c.Summary().Any(l => l.Contains(hero.Name.ToUpperInvariant())), "three mentions: of the banner, and in the end summary");

        // Arms: a lot of looted arms, or a piece from your baggage you do not wear.
        c.S.Goods.Remove("arms");
        Check(c.CannotArm(hero, null) == "NO LOOTED ARMS", "no arms to give");
        c.S.Goods["arms"] = 1;
        Check(c.Arm(hero, null) == null && hero.Carries != "" && !c.S.Goods.ContainsKey("arms"), "a man takes a lot of looted arms");
        Check(c.CannotArm(hero, null) == "HE IS ARMED ALREADY", "one gift a man");
        var sword = Gear.All.First(g => g.Slot == "weapon" && !c.Captain.Kit.Worn.Contains(g.Id));
        var piece = new GearItem { Def = sword.Id, Quality = ItemQuality.Fine };
        c.Captain.Baggage.Add(piece);
        Check(c.Arm(men[2], piece) == null && !c.Captain.Baggage.Contains(piece) && men[2].CarriesDef == sword.Id, "a man carries a piece from your baggage");

        // Send him home: only the old or thrice wounded; his village sends readier men.
        var old = men[3];
        old.Age = 25; old.Wounds = 0;
        Check(c.CannotPension(old) == "HE CAN STILL FIGHT", "a young whole man is not sent home");
        old.Wounds = 3;
        int stackMen = co.Stacks.First(s => s.Troop == old.Troop).Men;
        gold = c.S.Gold;
        Check(c.Pension(old) == null && !co.Roll.Contains(old) && c.S.Fallen.Contains(old) && old.Fate == "pensioned", "a thrice-wounded man goes home");
        Check(c.S.Gold == gold - Balance.Get().PensionCost && co.Stacks.First(s => s.Troop == old.Troop).Men == stackMen - 1, "his pension is paid and his place is empty");
        string home = old.Home != "" ? old.Home : co.Home;
        Check(c.S.Pensioned.Contains(home), "his village is remembered");
        var troop = co.Stacks[0].Troop;
        var fresh = c.Player.Companies.First(k => k != co && Rules.Fits(k, troop));
        var from = new Settlement { Name = home, Culture = fresh.Culture };
        float xp0 = fresh.Stack(troop).Xp;
        c.Enlist(fresh, troop, 2, from);
        Check(fresh.Stack(troop).Xp > xp0, "his village's recruits come readier");

        // Move him: to a company he fits with room.
        var room = c.Player.Companies.First(k => k != co && Rules.Fits(k, men[2].Troop) && k.Roll.Count < Balance.Get().RollSize);
        room.Stacks.OrderByDescending(s => s.Men).First().Take(3); // make room in it
        var to = c.Player.Companies.FirstOrDefault(k => k != co && c.CannotMoveMan(co, men[2], k) == null);
        Check(to != null, "a company to move him to");
        int toMen = to!.Men, coMen = co.Men;
        Check(c.MoveMan(co, men[2], to) == null && to.Roll.Contains(men[2]) && to.Men == toMen + 1 && co.Men == coMen - 1, "a man moves with his place in the ranks");

        // A save keeps it all.
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        var h2 = back.Player.Companies.SelectMany(k => k.Roll).First(m => m.Id == hero.Id);
        Check(h2.Mentions == 3 && h2.Carries == hero.Carries && back.S.Pensioned.Contains(home), "mentions, arms and pensions survive a save");
    }

    /// Every cursor shape builds, has its hot spot on the shape, an iron outline and some colour inside it.
    static void CursorsBuild()
    {
        foreach (var k in Enum.GetValues<CursorKind>())
        {
            var s = Cursors.Of(k);
            Check(s.Pixels.Length == s.W * s.H && s.W <= 24 && s.H <= 24, $"{k} cursor is a small sprite");
            Check(s.Hot.X > 0 && s.Hot.Y > 0 && s.Hot.X < s.W - 1 && s.Hot.Y < s.H - 1, $"{k} cursor's hot spot is inside it");
            Check(s.Pixels.Count(c => c.A > 0) > 40 && s.Pixels.Any(c => c.A > 0 && c.R < 30), $"{k} cursor has body and outline");
        }
    }

    /// Fight lost battles until named men fall (C7 tests): their reports, with `before` named men topped up each round.
    static List<BattleReport> LoseUntilFallen(Campaign c, int rounds, Func<bool> done)
    {
        var rng = new Rng(new CampaignState { Seed = 5 });
        var reps = new List<BattleReport>();
        for (int i = 0; i < rounds && !done(); i++)
        {
            foreach (var k in c.Player.Companies) while (k.Roll.Count < 4 && c.NameMan(k, "test") is Soldier m) m.Battles = 12;
            var lord = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && c.Hostile(c.Player, p)).OrderByDescending(p => p.Men).FirstOrDefault();
            if (lord == null) break;
            lord.Pos = c.Player.Pos;
            c.Encounter = lord;
            var setup = c.PrepareBattle(lord);
            reps.Add(c.FinishBattle(Rules.AutoResolve(setup.Side0, setup.Side1, rng) with { Winner = 1, Captain = LeaderFate.Wounded }, setup));
            c.Captain.WoundedUntil = 0; c.Encounter = null; c.S.Event = null;
            if (c.Player.Companies.Sum(k => k.Men) < 40) foreach (var k in c.Player.Companies) Rules.AddMen(k, k.Stacks[0].Troop, 30);
        }
        return reps;
    }

    /// C7: the book of the fallen groups every lost named man under the battle he fell in (its name) or his company, newest first.
    static void BookOfTheFallen()
    {
        var c = Campaign.New(41);
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        var reps = LoseUntilFallen(c, 12, () => c.S.Fallen.Any(m => m.Fate == "fell"));
        var gone = c.S.Fallen.Last(m => m.Fate == "fell");
        var rep = reps.Last(r => r.Roll.Any(x => x.Text.Contains(gone.Name)));
        Check(gone.Field != "" && gone.Field == rep.Field, "a fallen man keeps the name of the battle he fell in");
        var book = c.BookOfTheFallen();
        Check(book.Count > 0 && book[0].Title.StartsWith(gone.Field) && book[0].Men.Contains(gone), "the book opens on the newest battle, with him under it");
        Check(book.Sum(g => g.Men.Count) == c.S.Fallen.Count(m => m.Fate is "fell" or "missing"), "every man lost is in it, once");
        var byCo = c.BookOfTheFallen(true);
        Check(byCo.Any(g => g.Title == gone.CompanyName && g.Men.Contains(gone)), "and under his company when sorted so");
        c.Player.Companies.RemoveAll(k => k.Id == gone.Company);
        Check(c.BookOfTheFallen().Any(g => g.Men.Contains(gone)), "the book keeps him when his company is gone");
        Check(CampaignState.Deserialize(c.S.Serialize()).Fallen.First(m => m.Id == gone.Id).Field == gone.Field, "the field is saved");
    }

    /// C7: a cairn where named men fell; riding by raises its event once a season (cohesion); the map keeps twelve; cairns are
    /// save state (a new campaign of the same seed has none, and its map is the same); the year's day of a costly battle.
    static void GravesOnTheMap()
    {
        var c = Campaign.New(41);
        int sites = c.Map.Sites.Count;
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        LoseUntilFallen(c, 12, () => c.S.Cairns.Count > 0);
        Check(c.S.Cairns.Count > 0, "a battle that cost named men leaves a cairn");
        var cairn = c.S.Cairns[^1];
        Check(cairn.Men.Count > 0 && cairn.Men.All(id => c.S.Fallen.Any(m => m.Id == id && m.Fate == "fell")) && c.CairnNames(cairn).Any(), "with the fallen under it");
        // Ride by it next season: its event, with the names and the field.
        c.S.Event = null; cairn.Honoured = -1;
        c.Player.Pos = cairn.Pos;
        c.CairnsHourly();
        Check(c.S.Event is PendingEvent ev && ev.Id == "cairn_visit" && ev.Cairn == cairn.Id, "riding by raises the cairn's event");
        var e = Campaign.EventOf("cairn_visit")!;
        Check(c.Fill(e.Title, c.S.Event!).Contains(cairn.Field) && c.Fill(e.Text, c.S.Event!).Contains(c.CairnNames(cairn).First()), "naming the field and the men");
        var co = c.Player.Companies.FirstOrDefault(k => k.Id == cairn.Company);
        float coh = co?.Cohesion ?? 0;
        c.Choose(0);
        if (co != null) Check(co.Cohesion > coh, "standing with them steadies the company that fought there");
        c.S.Event = null;
        c.CairnsHourly();
        Check(c.S.Event == null, "and only once a season");
        // The cap.
        for (int i = 0; i < Campaign.MaxCairns + 3; i++) c.PlaceCairn(new Cairn { Id = 9000 + i, Pos = c.Player.Pos + new Vector2(50 + i, 0), Field = $"Test {i}", Men = new() { cairn.Men[0] } });
        Check(c.S.Cairns.Count == Campaign.MaxCairns && c.S.Cairns[0].Id != cairn.Id, "the map keeps twelve, the oldest going back to grass");
        Check(c.BookOfTheFallen().Any(g => g.Men.Any(m => m.Id == cairn.Men[0])), "though the book never forgets");
        var fresh = Campaign.New(41);
        Check(fresh.S.Cairns.Count == 0 && fresh.Map.Sites.Count == sites, "cairns are save state: the seed's map is unchanged");
        Check(CampaignState.Deserialize(c.S.Serialize()).Cairns.Count == Campaign.MaxCairns, "cairns are saved");
        // The year's day: three named men of one battle.
        c.S.Event = null;
        var k0 = c.Player.Companies.First(k => k.Men > 0);
        while (k0.Roll.Count < 2) c.NameMan(k0, "test");
        for (int i = 0; i < 3; i++) c.S.Fallen.Add(new Soldier { Id = 8000 + i, Name = $"Test Man {i}", Fate = "fell", DiedDay = 2, Field = "the Ford of Test", Company = k0.Id, CompanyName = k0.Name, Troop = k0.Stacks[0].Troop });
        c.S.Hours = (2 + Campaign.DaysPerYear - 1) * 24 + 1;
        c.DayFellDaily();
        Check(c.S.Log.Any(l => l.Text.Contains("the Ford of Test")), "a year on, the day is remembered in the log");
        Check(c.S.Event is PendingEvent day && day.Id == "men_day_fell" && c.Fill(Campaign.EventOf("men_day_fell")!.Text, day).Contains("Test Man 0"), "and at the fire, by name");
    }

    /// C7: the captain can die when the new-campaign rule is on: never when it is off; on, a lost field may kill him, a companion
    /// takes command, and the dead captain goes first in the book; with no companion the campaign ends.
    static void CaptainCanDie()
    {
        var off = Campaign.New(43);
        off.Player.Cooldown = 1e6f;
        off.Tick(24); off.Encounter = null; off.Arrived = null;
        LoseUntilFallen(off, 30, () => false);
        Check(off.S.DeadCaptains.Count == 0 && !off.S.GameOver, "with the rule off the captain never dies");
        var c = Campaign.New(43);
        c.S.CaptainDeath = true;
        c.Player.Cooldown = 1e6f;
        c.Tick(24); c.Encounter = null; c.Arrived = null;
        string first = c.Me.Name;
        c.S.Characters.Add(new Character { Id = c.S.NextId++, Kind = "companion", Name = "Test Heir", Culture = "common", PartyId = c.Player.Id, CompanyId = c.Player.Companies[0].Id, Age = 33 });
        LoseUntilFallen(c, 60, () => c.S.DeadCaptains.Count > 0);
        Check(c.S.DeadCaptains.Count == 1 && c.S.DeadCaptains[0].Name == first, "with it on, a captain falling on a lost field may die");
        Check(c.Me.Name == "Test Heir" && !c.S.GameOver && c.Companions.All(k => k.Name != "Test Heir"), "and the companion takes command at once");
        Check(c.S.Deeds.Any(d => d.Text.Contains(first) && d.Text.Contains("did not rise")), "the chronicle keeps it");
        // No one left to take command.
        c.S.Characters.RemoveAll(k => k.Kind == "companion");
        LoseUntilFallen(c, 60, () => c.S.DeadCaptains.Count > 1);
        Check(c.S.DeadCaptains.Count == 2 && c.S.GameOver, "with no companion, his death ends the campaign");
        Check(CampaignState.Deserialize(c.S.Serialize()).DeadCaptains.Count == 2, "the dead captains are saved");
    }
}
