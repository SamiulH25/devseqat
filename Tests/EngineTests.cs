using Microsoft.Xna.Framework;

namespace Ironfield;

/// Engines of war (docs/archive/SIEGECRAFT.md A1): data, crews, bolts, stones and gates.
public static partial class SelfTest
{
    static void EnginesValidate()
    {
        var problems = Engines.Validate().ToList();
        Check(problems.Count == 0, "engines.json is sound: " + string.Join("; ", problems));
        Check(Engines.All.Any(e => e.Shot == "bolt") && Engines.All.Any(e => e.Shot == "stone"), "there are bolt and stone engines");
    }

    /// A crewed scorpion shoots a company at range, its bolts run through more than one man, and it never hits its own side.
    static void EngineShoots()
    {
        var b = Battle.Empty(11);
        var crew = b.AddSquad(0, Troops.Get("levies"), 3, new Vector2(0, -42), 3, Vector2.UnitY);
        var own = b.AddSquad(0, Troops.Get("levies"), 12, new Vector2(0, -30), 6, Vector2.UnitY);
        var foe = b.AddSquad(1, Troops.Get("levies"), 30, new Vector2(0, 40), 3, -Vector2.UnitY); // a deep column: bolts go down its files
        foe.Order = own.Order = Order.Hold;
        var e = b.AddEngine(0, "scorpion", new Vector2(0, -40), Vector2.UnitY, crew);
        Check(b.CrewAt(e) == 3, "the crew stands at its engine");
        float before = foe.Units.Sum(u => u.Hp);
        int multi = 0;
        for (int i = 0; i < 60 * 40; i++)
        {
            b.Update(1 / 60f);
            multi = Math.Max(multi, b.EngineShots.Max(s => (int?)s.Struck.Count) ?? 0);
        }
        Console.WriteLine($"scorpion: {e.Shots} shots, {e.Kills} kills, most men through by one bolt {multi}");
        Check(e.Shots >= 4, "the scorpion keeps shooting");
        Check(foe.Units.Sum(u => u.Hp) < before, "its bolts hurt the foe");
        Check(multi >= 2, "a bolt runs through more than one man");
        Check(own.Units.All(u => u.Hp == u.Type.MaxHp) && crew.Units.All(u => u.Hp == u.Type.MaxHp), "bolts never strike their own side");
    }

    /// Orders to an engine (docs/archive/SIEGECRAFT.md A5): a company chosen as its mark, a spot of ground, and a place out of reach refused.
    static void EngineOrders()
    {
        var b = Battle.Empty(21);
        var crew = b.AddSquad(0, Troops.Get("levies"), 8, new Vector2(0, -62), 4, Vector2.UnitY);
        var near = b.AddSquad(1, Troops.Get("levies"), 12, new Vector2(-20, 30), 6, -Vector2.UnitY);
        var far = b.AddSquad(1, Troops.Get("levies"), 12, new Vector2(30, 60), 6, -Vector2.UnitY);
        near.Order = far.Order = Order.Hold;
        var e = b.AddEngine(0, "mangonel", new Vector2(0, -60), Vector2.UnitY, crew);
        b.AimEngine(e, far, null);
        for (int i = 0; i < 60 * 40 && e.Shots < 2; i++) b.Update(1 / 60f);
        Check(e.Shots >= 1 && Vector2.Distance(e.Aim, far.Center) < 20, "an engine shoots the company it was told to");
        var spot = new Vector2(60, 10);
        b.AimEngine(e, null, spot);
        Check(e.Mark == spot && e.Crew.Target == null, "a spot of ground becomes the mark");
        int shots = e.Shots;
        for (int i = 0; i < 60 * 40 && e.Shots < shots + 2; i++) b.Update(1 / 60f);
        Check(e.Shots > shots && Vector2.Distance(e.Aim, spot) < 0.01f, "it shoots at the spot");
        b.AimEngine(e, null, new Vector2(0, 200));
        Check(e.Mark == null, "a mark out of reach is refused (and clears the old one)");
        b.AimEngine(e, null, new Vector2(0, -50));
        Check(e.Mark == null, "a mark inside its least range is refused");
    }

    /// You work an engine yourself (A5): it holds its fire for you, and looses where you aim when you attack.
    static void MannedEngine()
    {
        var b = Battle.Empty(22);
        var crew = b.AddSquad(0, Troops.Get("levies"), 3, new Vector2(0, -42), 3, Vector2.UnitY);
        var foe = b.AddSquad(1, Troops.Get("levies"), 20, new Vector2(0, 40), 5, -Vector2.UnitY);
        foe.Order = Order.Hold;
        var e = b.AddEngine(0, "scorpion", new Vector2(0, -40), Vector2.UnitY, crew);
        b.Player.Pos = e.Pos + new Vector2(1.5f, 0);
        b.Input = new PlayerInput { Mount = true };
        b.Update(1 / 60f);
        Check(b.Manned == e, "the use key takes the engine you stand by");
        var aim = new Vector2(0, 30);
        b.Input = new PlayerInput { EngineAim = aim };
        for (int i = 0; i < 60 * 20; i++) b.Update(1 / 60f);
        Check(e.Shots == 0, "an engine you work does not shoot on its own");
        b.Input = new PlayerInput { EngineAim = aim, Attack = true };
        for (int i = 0; i < 60 * 12 && e.Shots == 0; i++) b.Update(1 / 60f);
        Check(e.Shots == 1 && Vector2.Distance(e.Aim, aim) < 0.01f, "it looses where you aim when you attack");
        Check(b.EngineShots.All(s => s.Shooter == b.Player), "the shot is yours");
        b.Player.Pos = e.Pos + new Vector2(20, 0);
        b.Input = new PlayerInput();
        b.Update(1 / 60f);
        Check(b.Manned == null, "walking away leaves it to the crew");
    }

    /// No crew at it, no shots; a foe standing at it wrecks it.
    static void EngineNeedsCrew()
    {
        var b = Battle.Empty(12);
        var crew = b.AddSquad(0, Troops.Get("levies"), 3, new Vector2(0, -42), 3, Vector2.UnitY);
        var foe = b.AddSquad(1, Troops.Get("levies"), 12, new Vector2(0, 40), 6, -Vector2.UnitY);
        foe.Order = Order.Hold;
        var e = b.AddEngine(0, "scorpion", new Vector2(0, -40), Vector2.UnitY, crew);
        foreach (var u in crew.Units) { u.Dead = true; u.Hp = 0; }
        for (int i = 0; i < 60 * 20; i++) b.Update(1 / 60f);
        Check(e.Shots == 0, "an engine with no crew stands silent");
        var raider = b.AddSquad(1, Troops.Get("levies"), 4, new Vector2(0, -38), 4, -Vector2.UnitY);
        raider.Order = Order.Hold; raider.Anchor = e.Pos;
        for (int i = 0; i < 60 * 30 && !e.Wrecked; i++) b.Update(1 / 60f);
        Check(e.Wrecked, "foes at an engine hack it apart");
    }

    /// A mangonel's stone lands among a packed company: several men hurt by one stone, men near it shaken.
    static void StoneSplashes()
    {
        var b = Battle.Empty(13);
        var crew = b.AddSquad(0, Troops.Get("levies"), 8, new Vector2(0, -62), 4, Vector2.UnitY);
        var foe = b.AddSquad(1, Troops.Get("levies"), 40, new Vector2(0, 40), 8, -Vector2.UnitY);
        foe.Order = Order.Hold;
        var e = b.AddEngine(0, "mangonel", new Vector2(0, -60), Vector2.UnitY, crew);
        for (int i = 0; i < 60 * 60; i++) b.Update(1 / 60f);
        int hurt = foe.Units.Count(u => u.Hp < u.Type.MaxHp || u.Dead);
        Console.WriteLine($"mangonel: {e.Shots} stones, {hurt} men hurt, {e.Kills} kills");
        Check(e.Shots >= 3, "the mangonel keeps throwing");
        Check(hurt > e.Shots, "a stone hurts more than one man");
        var near = Battle.Empty(14);
        var c2 = near.AddSquad(0, Troops.Get("levies"), 8, new Vector2(0, -12), 4, Vector2.UnitY);
        var close = near.AddSquad(1, Troops.Get("levies"), 20, new Vector2(0, 0), 5, -Vector2.UnitY);
        close.Order = Order.Hold;
        var e2 = near.AddEngine(0, "mangonel", new Vector2(0, -10), Vector2.UnitY, c2);
        bool tooClose = false;
        for (int i = 0; i < 60 * 30; i++) { near.Update(1 / 60f); tooClose |= Vector2.Distance(e2.Aim, e2.Pos) is > 0.01f and < 25; }
        Check(!tooClose, "a mangonel never throws inside its least range");
    }

    /// In an assault, a mangonel before the gate throws at it and takes it down faster than the ram alone.
    static void EngineBreaksGate()
    {
        // When the gate falls (or, if it stands at the minute, a minute plus what is left of it): the ram breaks it either way.
        float GateFalls(bool engine, int seed)
        {
            var rng = new Random(5);
            var att = Ids(Armies.Random(rng, 2, 0), 3000);
            var def = Ids(Armies.Random(rng, 2, 1), 4000);
            var b = new Battle(seed, att, def, new BattleOptions { AutoPlayer = true, PlayerWounded = true, Siege = true, Ladders = 0, GateHp = 2000 });
            Engine? e = null;
            if (engine)
            {
                var mg = b.Wall!.MainGate;
                var crew = b.AddSquad(0, Troops.Get("levies"), 8, mg.Pos + mg.Out * 72f + mg.Along * 20f, 4, Vector2.UnitY);
                e = b.AddEngine(0, "mangonel", mg.Pos + mg.Out * 70f + mg.Along * 20f, Vector2.UnitY, crew);
            }
            for (int i = 0; i < 60 * 60 && b.Winner < 0 && b.Wall!.MainGate.Hp > 0; i++) b.Update(1 / 60f);
            float at = b.Wall!.MainGate.Hp > 0 ? 60 + b.Wall.MainGate.Hp : b.Time;
            Console.WriteLine($"gate: {(e != null ? $"mangonel {e.Shots} stones, {e.GateDamage:0} off the gate" : "no engine")}, gate {b.Wall.MainGate.Hp:0}, score {at:0.0}");
            if (e != null) Check(e.GateDamage > 0, "the mangonel's stones strike the gate");
            return at;
        }
        // One battle is too chaotic to tell (the crew alone moves the fall by ten seconds either way): take the most of five.
        int sooner = 0;
        for (int seed = 77; seed < 82; seed++) if (GateFalls(true, seed) < GateFalls(false, seed)) sooner++;
        Console.WriteLine($"gate: falls sooner under stones in {sooner} of 5 battles");
        Check(sooner >= 3, $"the gate falls faster under stones ({sooner} of 5)");
    }
}
