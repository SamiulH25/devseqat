using Microsoft.Xna.Framework;

namespace Ironfield;

/// The men of the company (docs/COMPANY.md C1-C7).
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

    /// C4: your hand on the roll. Each action's refusals and effects; the unanswered sergeant choice falls back to the risen
    /// man; what you did survives a save.
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
}
