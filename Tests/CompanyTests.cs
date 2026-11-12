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
}
