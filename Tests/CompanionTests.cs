using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of companions in the field and as people (docs/COMPANIONS_MAGIC.md B1-B5; Callings.cs, Bonds.cs).
public static partial class SelfTest
{
    /// A campaign with one companion leading the first company: (campaign, companion).
    static (Campaign c, Character k) WithCompanion(int seed, string traits = "Loyal")
    {
        var c = Campaign.New(seed);
        var k = new Character { Id = c.S.NextId++, Kind = "companion", Name = "Osric Test", Culture = "common", Role = "Scout", PartyId = c.Player.Id, CompanyId = c.Player.Companies[0].Id };
        k.Traits.AddRange(traits.Split(','));
        c.S.Characters.Add(k);
        c.RefreshHeroes();
        return (c, k);
    }

    /// B1-B3: a companion has a calling; XP raises his level, each level adds a talent, level 5 a second ability; all of it
    /// goes into battle with his company (its hero's abilities on the spell engine, its aura and talents as standing effects).
    static void CompanionGrows()
    {
        Check(!Callings.Validate().Any(), "companions.json validates: " + string.Join("; ", Callings.Validate()));
        var (c, k) = WithCompanion(53);
        Check(k.Calling == "Banner", $"a loyal man carries a banner ({k.Calling})");
        Check(Campaign.GiftsOf(k).Length == 1 && k.Level == 0, "he starts with one ability");
        c.GainXp(k, 100);
        Check(k.Level == 5 && Campaign.GiftsOf(k).Length == 2 && Campaign.TalentsOf(k).Count() == 6, $"at level 5 he has two abilities and five talents (level {k.Level})");
        var bandit = c.S.Parties.First(p => p.Kind == PartyKind.Bandit);
        bandit.Pos = c.Player.Pos;
        c.Encounter = bandit;
        var setup = c.PrepareBattle(bandit);
        var co = setup.Player.Companies.First(x => x.Id == k.CompanyId);
        Check(co.Hero == k.Name && co.HeroGifts!.Contains("rally_to_me") && co.Standing!.Contains("standard"), "his company goes into battle with his abilities and his standard");
        var b = new Battle(setup.Seed, setup.Player, setup.Enemy, setup.Options);
        var s = b.Squads.First(q => q.Company?.Id == k.CompanyId);
        Check(s.Spells.Any(a => a.Id == "rally_to_me") && s.ByHero.All(h => h) && s.Units.All(u => u.Mods.Shock < 0), "in battle his company knows his abilities and stands steadier under his banner");
    }

    /// C2: Hold the line when his company is fighting; Rally to me turns routing men near him.
    static void HoldTheLine()
    {
        var b = Battle.Empty(76);
        var mine = b.AddSquad(0, Troops.Get("shieldmen"), 12, new(0, 0), 6, Vector2.UnitY, new Company("shieldmen", 12, Hero: "Osric", HeroGifts: new[] { "hold_the_line", "rally_to_me" }, HeroLevel: 3));
        var foe = b.AddSquad(1, Troops.Get("shieldmen"), 16, new(0, 6), 8, -Vector2.UnitY);
        mine.Order = foe.Order = Order.Charge;
        mine.Target = foe; foe.Target = mine;
        for (int i = 0; i < 25 * 60 && !mine.Timed.Any(t => t.Effect.Id == "held"); i++) b.Update(1 / 60f);
        Check(mine.Timed.Any(t => t.Effect.Id == "held") && mine.Units.Where(u => u.Active).All(u => u.Mods.Holdfast > 0), "a Shield companion holds the line when his company is fighting");

        var r = Battle.Empty(77);
        var rs = r.AddSquad(0, Troops.Get("shieldmen"), 10, new(0, 0), 5, Vector2.UnitY, new Company("shieldmen", 10, Hero: "Osric", HeroGifts: new[] { "rally_to_me" }));
        r.AddSquad(1, Troops.Get("shieldmen"), 6, new(0, 60), 3, -Vector2.UnitY);
        Run(r, 0.2f);
        foreach (var u in rs.Units.Where(u => !u.Hero).Take(6)) { u.Routing = true; u.Routs = 1; u.Morale = 5; }
        Run(r, 9);
        Check(rs.Units.Count(u => u.Routing) < 3, $"Rally to me turns routing men near him ({rs.Units.Count(u => u.Routing)} still running)");
    }

    /// B4: an enemy lord of standing brings a champion with a calling.
    static void EnemyChampions()
    {
        var c = Campaign.New(54);
        var lords = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && c.CharacterOf(p) != null).ToList();
        bool Champion(Party lord)
        {
            lord.Pos = c.Player.Pos;
            c.Encounter = lord;
            var setup = c.PrepareBattle(lord);
            c.Encounter = null;
            return setup.Enemy.Companies.Any(co => co.Hero != "" && co.HeroGifts is { Length: > 0 } && co.HeroLevel >= 3);
        }
        var great = lords.First(p => c.CharacterOf(p)!.Id % 3 == 0);
        great.Tier = 5;
        Check(Champion(great), "a great lord brings a champion with abilities");
        var small = lords.First(p => c.CharacterOf(p)!.Id % 3 != 0);
        small.Tier = 5;
        Check(!Champion(small), "most lords bring none: champions are rare");
        great.Tier = 2;
        Check(!Champion(great), "nor does a small lord");
    }

    /// B4, B5: two companions who fight five battles together swear brotherhood (an aura when both lead); a companion at level 4
    /// takes a student, who rises in his place when he falls; a companion you wrong warns you once, then leaves.
    static void CompanionLeaves()
    {
        var (c, a) = WithCompanion(55, "Loyal");
        var b = new Character { Id = c.S.NextId++, Kind = "companion", Name = "Wulf Test", Culture = "common", PartyId = c.Player.Id, CompanyId = c.Player.Companies[^1].Id };
        b.Traits.Add("Generous");
        c.S.Characters.Add(b);
        c.RefreshHeroes();
        for (int i = 0; i < Campaign.SwornAfter; i++) c.BondsAfterBattle(new List<Character> { a, b });
        Check(a.Sworn == b.Id && b.Sworn == a.Id, "five battles side by side make sworn brothers");

        var co = c.Player.Companies.First(x => x.Id == a.CompanyId);
        if (co.Roll.Count == 0) co.Roll.Add(new Soldier { Id = 999001, Name = "Edric the Test", Troop = co.Troop });
        c.GainXp(a, 70);
        Check(a.Student != 0, "at level 4 he takes a named man as his student");
        a.Dead = true;
        int before = c.Companions.Count();
        c.StudentRises(a);
        var heir = c.Companions.FirstOrDefault(k => k.Ranks?.Id == a.Student);
        Check(heir != null && heir.Calling == a.Calling && heir.Level >= 1 && c.Companions.Count() == before + 1, "when he falls his student rises with his calling");

        b.Opinion[c.S.CaptainChar] = -90;
        c.CompanionsWeekly();
        Check(b.Warned && b.Kind == "companion", "a wronged companion warns you first");
        c.CompanionsWeekly();
        Check(b.Kind == "gone" && b.CompanyId == -1, "then leaves");
    }
}
