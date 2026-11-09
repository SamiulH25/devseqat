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
}
