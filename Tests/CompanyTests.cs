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
}
