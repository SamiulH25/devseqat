using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the attack styles of tier V-VII troops (docs/archive/COMPANIONS_MAGIC.md D2, BattleStyles.cs).
public static partial class SelfTest
{
    /// One man of `troop` at the origin facing +Y, and enemy levies at `foes`, their backs to him (no block), after one
    /// tick so the spatial grid knows them: (battle, attacker, foes).
    static (Battle b, Unit a, List<Unit> foes) StyleScene(string troop, params Vector2[] foes) => StyleScene(Troops.Get(troop), foes);

    static (Battle b, Unit a, List<Unit> foes) StyleScene(UnitType troop, params Vector2[] foes)
    {
        var b = Battle.Empty(61);
        var a = b.AddSquad(0, troop, 1, Vector2.Zero, 1, Vector2.UnitY).Units[0];
        var list = foes.Select(p => b.AddSquad(1, Troops.Get("levies"), 1, p, 1, Vector2.UnitY).Units[0]).ToList();
        b.Update(1 / 60f);
        a.Pos = Vector2.Zero;
        a.Face(Vector2.UnitY);
        foreach (var (u, p) in list.Zip(foes)) { u.Pos = p; u.Face(Vector2.UnitY); u.Vel = Vector2.Zero; }
        b.Update(0); // rebuild the grid at the set places
        return (b, a, list);
    }

    /// A troop with its stats but no style: the same man without the drill.
    static UnitType Plain(string troop) { var t = Troops.Get(troop).Clone(); t.Style = AttackStyle.None; t.StyleRank = 0; return t; }

    /// D2: every tier V-VII troop has a style, from its weapon or named in the data; tiers I-IV have none; rank follows tier.
    static void StylesByTier()
    {
        foreach (var t in Troops.All.Where(t => !t.Wild && !t.Leader && t.Class != UnitClass.Caster)) // a mage's tier shows in his spells (MagesByTier)
        {
            if (t.Tier < 5) Check(t.Style == AttackStyle.None && t.StyleRank == 0, $"{t.Name} (tier {t.Tier}) has no attack style");
            else Check(t.StyleRank == t.Tier - 4, $"{t.Name}'s style rank follows its tier");
            foreach (var n in t.UpgradesTo.Select(Troops.Get).Where(n => t.Style != AttackStyle.None && n.Look.Weapon == t.Look.Weapon && n.HasShield == t.HasShield))
                Check(n.Style == t.Style && n.StyleRank > t.StyleRank, $"{n.Name} keeps {t.Name}'s style and raises it");
        }
        int styled = Troops.All.Count(t => t.Style != AttackStyle.None);
        Check(Troops.All.Where(t => !t.Wild && !t.Leader && t.Tier >= 5 && t.Class != UnitClass.Caster).All(t => t.Style != AttackStyle.None), "every tier V-VII troop has an attack style");
        Check(Enum.GetValues<AttackStyle>().Skip(1).All(s => Battle.StyleText(s).name != ""), "every style has a name for the tooltip");
        Console.WriteLine($"styles: {styled} troops with an attack style");
    }

    /// D2: a greatsword's blow carries into the man beside the target; the same man without the style hits one.
    static void SweepHitsBeside()
    {
        foreach (var (troop, sweeps) in new[] { (Troops.Get("grand_blademasters"), true), (Plain("grand_blademasters"), false) })
        {
            var (b, a, foes) = StyleScene(troop, new(0, 1.3f), new(0.9f, 1.2f));
            float hp = foes[1].Hp;
            b.Hit(a, foes[0], 20, false);
            Check((foes[1].Hp < hp) == sweeps, sweeps ? "a sweep carries into the man beside" : "a plain blow hits only its man");
        }
    }

    /// D2: a shield bash shoves and staggers a man; a lance and a close bolt throw him back.
    static void KnockThrowsBack()
    {
        var (b, a, foes) = StyleScene("allfather_guard", new Vector2(0, 1.3f));
        a.Blows = 3; // his third blow: a bash
        b.Hit(a, foes[0], 10, false);
        Check(foes[0].Pos.Y > 1.4f && foes[0].Action == UnitAction.Stagger, $"a shield bash shoves a man ({foes[0].Pos.Y - 1.3f:0.00} m) and staggers him");
        (b, a, foes) = StyleScene("allfather_guard", new Vector2(0, 1.3f));
        a.Blows = 2;
        b.Hit(a, foes[0], 10, false);
        Check(foes[0].Pos.Y < 1.35f, "the blows between bashes do not");

        (b, a, foes) = StyleScene("grail_knights", new Vector2(0, 2.5f), new Vector2(0, 4.0f));
        float behind = foes[1].Hp;
        b.Hit(a, foes[0], 40, false, lance: true);
        Check(foes[0].Pos.Y > 3.5f || foes[0].Dead, "a lance rides a man down");
        Check(foes[1].Hp < behind, "and the horse tramples the man behind");

        (b, a, foes) = StyleScene("grand_arbalests", new Vector2(0, 12));
        b.Hit(a, foes[0], 5, true);
        Check(foes[0].Pos.Y > 12.4f, "a close bolt punches a man back");
    }

    /// D2: a pike thrust that lands carries on to the man behind; the plain pike stops at the first.
    static void ThrustThrough()
    {
        foreach (var (troop, through) in new[] { (Troops.Get("imperial_pikemen"), true), (Plain("imperial_pikemen"), false) })
        {
            var (b, a, foes) = StyleScene(troop, new(0, 2), new(0, 3.2f), new(1.5f, 3.2f));
            float hp1 = foes[1].Hp, hp2 = foes[2].Hp;
            b.Hit(a, foes[0], 20, false);
            Check((foes[1].Hp < hp1) == through, through ? "a pike thrust goes on into the man behind" : "a plain thrust stops at the first man");
            Check(foes[2].Hp == hp2, "but not into a man off its line");
        }
    }

    /// D2: Dane axes crush their way through a shield line faster than the same men swinging plainly.
    static void CrushBeatsShields()
    {
        int Kills(UnitType axes)
        {
            int dead = 0;
            for (int seed = 1; seed <= 3; seed++)
            {
                var b = Battle.Empty(940 + seed);
                var sa = b.AddSquad(0, axes, 16, new(0, -12), 8, Vector2.UnitY);
                var sb = b.AddSquad(1, Troops.Get("citadel_sentinels"), 16, new(0, 12), 8, -Vector2.UnitY);
                sa.Order = sb.Order = Order.Charge;
                sa.Target = sb; sb.Target = sa;
                for (int i = 0; i < 25 * 60; i++) b.Update(1 / 60f);
                dead += sb.Units.Count(u => u.Dead);
            }
            return dead;
        }
        int crush = Kills(Troops.Get("doomcleavers")), plain = Kills(Plain("doomcleavers"));
        Console.WriteLine($"styles: Doomcleavers kill {crush} shield men in three fights, {plain} without the crush");
        Check(crush > plain, "a crush gets through shields faster than a plain axe");
    }

    /// D0 for styles: each melee style makes its men fight at least as well as the same men without it, 20 against 30
    /// tier IV-VI foes, three seeds (score: own men standing plus foes out of the fight, dead or running; one man's slack for the dice).
    static void StylesOnlyAdd()
    {
        foreach (var (troop, foe) in new[] { ("allfather_guard", "iron_legionaries"), ("doomcleavers", "citadel_sentinels"), ("grand_blademasters", "citadel_sentinels"),
                     ("imperial_pikemen", "grand_phalanx"), ("grail_knights", "paladin_knights"), ("tempest_spears", "iron_legionaries"), ("emir_mamluks", "storm_riders") })
        {
            float Score(UnitType t)
            {
                float score = 0;
                for (int seed = 1; seed <= 3; seed++)
                {
                    var b = Battle.Empty(950 + seed);
                    var sa = b.AddSquad(0, t, 20, new(0, -12), 10, Vector2.UnitY);
                    var sb = b.AddSquad(1, Troops.Get(foe), 30, new(0, 12), 10, -Vector2.UnitY);
                    sa.Order = sb.Order = Order.Charge;
                    sa.Target = sb; sb.Target = sa;
                    for (int i = 0; i < 40 * 60; i++) b.Update(1 / 60f);
                    score += Standing(sa) + sb.StartCount - Standing(sb); // foes dead or running both count
                }
                return score;
            }
            var styled = Troops.Get(troop);
            float with = Score(styled), without = Score(Plain(troop));
            Console.WriteLine($"styles: {styled.Name} ({Battle.StyleText(styled.Style).name}) against 30 {Troops.Get(foe).Name}: {with:0} with the style, {without:0} without");
            Check(with >= without - 1, $"{Battle.StyleText(styled.Style).name} does not make {styled.Name} fight worse");
        }
    }

    /// D2: at long range a longbow's arrows drop past a shield held to the front; a plain bow's do not.
    static void PlungingShot()
    {
        float Through(UnitType bow, bool plunge)
        {
            var (b, a, foes) = StyleScene(bow, new Vector2(0, 30));
            var t = foes[0];
            t.Face(-Vector2.UnitY); // facing the archer, shield up
            int got = 0;
            for (int i = 0; i < 400; i++)
            {
                t.Hp = t.Type.MaxHp; t.Action = UnitAction.None; t.Dead = false;
                b.Hit(a, t, 1, true, plunge: plunge);
                if (t.Hp < t.Type.MaxHp) got++;
            }
            return got / 400f;
        }
        var shields = Troops.Get("levies");
        Check(shields.HasShield, "levies carry a shield for this test");
        float far = Through(Troops.Get("sovereign_longbowmen"), true), near = Through(Troops.Get("sovereign_longbowmen"), false), plain = Through(Plain("sovereign_longbowmen"), true);
        Console.WriteLine($"styles: arrows past a front shield: longbow long shot {far:P0}, short shot {near:P0}, plain bow long shot {plain:P0}");
        Check(far > near + 0.05f, "a long longbow shot gets past a front shield more often than a short one");
        Check(MathF.Abs(plain - near) < 0.06f, "a plain bow's long shot is no better than its short one");
    }
}
