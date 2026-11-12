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
}
