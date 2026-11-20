using Microsoft.Xna.Framework;

namespace Ironfield;

/// Walls that break (docs/archive/SIEGECRAFT.md A2): breaches, rams, siege towers and the defenders' springalds.
public static partial class SelfTest
{
    /// An assault with no ladders and a gate too strong to batter in time, plus whatever engine `add` sets up for the attackers.
    static Battle Assault(Action<Battle>? add = null, int wallEngines = 0, float wallHp = 400)
    {
        var rng = new Random(5);
        var att = Ids(Armies.Random(rng, 2, 0), 3000);
        var def = Ids(Armies.Random(rng, 2, 1), 4000);
        var b = new Battle(77, att, def, new BattleOptions { AutoPlayer = true, PlayerWounded = true, Siege = true, Ladders = 0, GateHp = 20000, WallHp = wallHp, WallEngines = wallEngines });
        add?.Invoke(b);
        return b;
    }

    static Engine Crewed(Battle b, string id, Vector2 at)
    {
        var def = Engines.Get(id);
        var crew = b.AddSquad(0, Troops.Get("levies"), def.Crew, at - new Vector2(0, 2.5f), Math.Min(def.Crew, 6), Vector2.UnitY);
        return b.AddEngine(0, id, at, Vector2.UnitY, crew);
    }

    /// Nobody crosses the face except at the gate, a ladder, a tower's ramp or a breach; counts who came over a breach and a ramp.
    static (int Illegal, int Breach, int Ramp) Crossings(Battle b, float seconds)
    {
        var w = b.Wall!;
        var was = new Dictionary<Unit, bool>();
        int illegal = 0, breach = 0, ramp = 0;
        for (int step = 0; step < 60 * seconds && b.Winner < 0; step++)
        {
            b.Update(1 / 60f);
            foreach (var u in b.Units)
            {
                bool inside = SiegeWall.Inside(u.Pos);
                if (!was.TryGetValue(u, out bool before)) { was[u] = inside; continue; }
                was[u] = inside;
                if (!u.Active || inside == before) continue;
                if (w.AtBreach(u.Pos.X)) breach++;
                else if (u.Team == 0 && w.AtRamp(u.Pos.X)) ramp++;
                else if (!(w.GateOpen && w.AtGate(u.Pos.X)) && !(u.Team == 0 && w.AtLadder(u.Pos.X))) illegal++;
            }
        }
        return (illegal, breach, ramp);
    }

    static void SectionsAndBreaches()
    {
        var w = new SiegeWall(0, 500, 300);
        Check(w.SectionAt(SiegeWall.GateX) < 0 && w.SectionAt(SiegeWall.GateX + 6) < 0, "the gatehouse is not a section");
        Check(w.SectionAt(-95) == 0 && w.SectionAt(95) == SiegeWall.SectionCount - 1 && w.SectionAt(150) < 0, "sections run from -100 to 100");
        int i = w.SectionAt(42);
        Check(!w.AtBreach(42), "a whole wall has no breach");
        w.SectionHp[i] = 0;
        Check(w.AtBreach(SiegeWall.SectionX(i)) && !w.AtBreach(SiegeWall.SectionX(i) + 6) && w.Breaches.Single() == SiegeWall.SectionX(i), "a broken section is a breach at its middle");
    }

    /// A trebuchet makes a breach and the attackers go in over the rubble; nobody walks through the wall anywhere else.
    static void TrebuchetBreaches()
    {
        Engine e = null!;
        var b = Assault(b => e = Crewed(b, "trebuchet", new Vector2(45, SiegeWall.Outer - 100)), wallHp: 250);
        var (illegal, breach, _) = Crossings(b, 300);
        var w = b.Wall!;
        Console.WriteLine($"trebuchet: {e.Shots} stones, {e.WallDamage:0} off the wall, breaches {string.Join(",", w.Breaches)}, {breach} crossed at a breach, winner {b.Winner}");
        Check(w.Breaches.Any(), "the trebuchet breaks a section");
        Check(breach > 0, "men cross at the breach");
        Check(illegal == 0, $"nobody walks through the wall ({illegal})");
        Check(b.WallVersion > 0, "the screen hears the wall change");
    }

    /// A ram rolls to the gate and beats it in faster than men with their hands; arrows hardly touch the men under its roof.
    static void RamAtGate()
    {
        float Breaks(bool ram, out Engine? e, out Battle b)
        {
            Engine? made = null;
            b = Assault(ram ? b => made = Crewed(b, "ram", new Vector2(SiegeWall.GateX + 6, SiegeWall.Outer - 25)) : null);
            e = made;
            b.Wall!.GateHp = b.Wall.GateMax = 3000;
            for (int i = 0; i < 60 * 200 && !b.Wall.GateOpen; i++) b.Update(1 / 60f);
            return b.Time;
        }
        float with = Breaks(true, out var e, out var b), without = Breaks(false, out _, out _);
        Console.WriteLine($"ram: gate down at {with:0}s with it, {without:0}s without; it took {e!.GateDamage:0}");
        Check(Vector2.Distance(e.Pos, e.Goal) < 0.5f, "the ram reaches the gate");
        Check(e.GateDamage > 0 && with < without, "the ram brings the gate down sooner");
        var crewman = e.Crew.Units.First(u => u.Active && Vector2.Distance(u.Pos, e.Pos) < 3);
        var archer = b.Units.First(u => u.Team == 1 && u.Active && u.Ammo > 0);
        float hp = crewman.Hp;
        b.Hit(archer, crewman, 20, true);
        Check(crewman.Hp > hp - 20 * 0.5f, "arrows hardly hurt men under the ram's roof");
    }

    /// A siege tower rolls to the face, its men cross by it, and wrecking it takes the way away.
    static void TowerDocks()
    {
        Engine e = null!;
        var b = Assault(b => e = Crewed(b, "tower", new Vector2(-55, SiegeWall.Outer - 40)));
        var (illegal, _, ramp) = Crossings(b, 180);
        Console.WriteLine($"tower: docked {e.Docked}, {ramp} crossed by its ramp, illegal {illegal}");
        Check(e.Docked && b.Wall!.AtRamp(-55), "the tower reaches the wall");
        Check(ramp > 0, "attackers cross by the tower");
        Check(illegal == 0, $"nobody walks through the wall ({illegal})");
        e.Hp = 1;
        b.Damage(e, 10);
        Check(!b.Wall!.AtRamp(-55), "a wrecked tower is no way up");
    }

    /// The walls' level puts springalds on the walkway, and they shoot at the attackers coming on.
    static void WallEnginesShoot()
    {
        var b = Assault(wallEngines: 2);
        var mounted = b.Engines.Where(e => e.Team == 1).ToList();
        Check(mounted.Count == 2 && mounted.All(e => SiegeWall.Inside(e.Pos)), "two springalds stand on the walkway");
        for (int i = 0; i < 60 * 60; i++) b.Update(1 / 60f);
        Console.WriteLine($"wall engines: {mounted.Sum(e => e.Shots)} shots, {mounted.Sum(e => e.Kills)} kills");
        Check(mounted.Sum(e => e.Shots) > 0, "the wall's springalds shoot");
    }
}
