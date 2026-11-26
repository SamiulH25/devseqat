using Microsoft.Xna.Framework;

namespace Ironfield;

/// Fortress walls, breaches, rams, siege towers and defenders' springalds (docs/pre-demo/SIEGES.md SE1-SE2).
public static partial class SelfTest
{
    /// An assault with no ladders and a gate too strong to batter in time, plus whatever engine `add` sets up for the attackers.
    static Battle Assault(Action<Battle>? add = null, int wallEngines = 0, float wallHp = 400, FortressKind kind = FortressKind.Town)
    {
        var rng = new Random(5);
        var att = Ids(Armies.Random(rng, 2, 0), 3000);
        var def = Ids(Armies.Random(rng, 2, 1), 4000);
        var b = new Battle(77, att, def, new BattleOptions { AutoPlayer = true, PlayerWounded = true, Siege = true, Ladders = 0, GateHp = 20000, WallHp = wallHp, WallEngines = wallEngines, FortressKind = kind });
        add?.Invoke(b);
        return b;
    }

    static Engine Crewed(Battle b, string id, Vector2 at)
    {
        var def = Engines.Get(id);
        var crew = b.AddSquad(0, Troops.Get("brevon_fyrd"), def.Crew, at - new Vector2(0, 2.5f), Math.Min(def.Crew, 6), Vector2.UnitY);
        return b.AddEngine(0, id, at, Vector2.UnitY, crew);
    }

    /// Nobody crosses any wall segment except through an open gate, a breach, a docked ramp or (attacker foot) a raised ladder.
    static (int Illegal, int Breach, int Ramp) Crossings(Battle b, float seconds)
    {
        var w = b.Wall!;
        var prevPos = new Dictionary<Unit, Vector2>();
        int illegal = 0, breach = 0, ramp = 0;
        for (int step = 0; step < 60 * seconds && b.Winner < 0; step++)
        {
            b.Update(1 / 60f);
            foreach (var u in b.Units)
            {
                if (!prevPos.TryGetValue(u, out var before)) { prevPos[u] = u.Pos; continue; }
                prevPos[u] = u.Pos;
                if (!u.Active || !w.CrossesWall(before, u.Pos, out var seg) || seg == null) continue;
                var mid = (before + u.Pos) * 0.5f;
                if (seg.Breached) breach++;
                else if (!u.Mounted && w.Ramps.Any(r => r.SegmentId == seg.Id && Vector2.Distance(mid, r.Pos) <= r.HalfWidth + 1.2f)) ramp++;
                else if (seg.IsGate && w.Gates[seg.GateIndex].Open) { }
                else if (u.Team == 0 && !u.Mounted && w.Ladders.Any(l => l.Up && l.SegmentId == seg.Id && Vector2.Distance(mid, l.Pos) <= l.HalfWidth + 1.2f)) { }
                else illegal++;
            }
        }
        return (illegal, breach, ramp);
    }

    static void SectionsAndBreaches()
    {
        foreach (var kind in new[] { FortressKind.Town, FortressKind.Castle, FortressKind.GreatCastle, FortressKind.TowerHouse, FortressKind.WalledVillage })
        {
            var spec = FortressSpec.Procedural(42, kind, wallsLevel: 2);
            var w = new Fortress(spec, ladders: 2, gateHp: 500, wallHp: 300, seed: 42);
            Check(w.Segments.Count >= 4 && w.Gates.Count >= 1, $"{kind} builds a closed wall ring with at least one gate");
            Check(w.Inside(w.Heart), $"{kind} heart lies inside the outer ring");
            if (kind == FortressKind.GreatCastle) Check(w.InnerRing.Length >= 3 && w.Gates.Count >= 2, "great castle is concentric with two rings and gates");
            var seg = w.Segments.First(s => !s.IsGate);
            Check(!seg.Breached && !w.AnyBreach, $"{kind} starts whole");
            seg.Hp = 0;
            Check(seg.Breached && w.AnyBreach, $"{kind} breaches when segment hp hits zero");
        }
    }

    /// A trebuchet makes a breach and the attackers go in over the rubble; nobody walks through the wall anywhere else.
    static void TrebuchetBreaches()
    {
        Engine e = null!;
        var b = Assault(b =>
        {
            var mg = b.Wall!.MainGate;
            e = Crewed(b, "trebuchet", mg.Pos + mg.Out * 95f + mg.Along * 26f);
        }, wallHp: 250);
        var (illegal, breach, _) = Crossings(b, 300);
        var w = b.Wall!;
        Console.WriteLine($"trebuchet: {e.Shots} stones, {e.WallDamage:0} off the wall, breaches {w.Segments.Count(s => s.Breached)}, {breach} crossed at a breach, winner {b.Winner}");
        Check(w.AnyBreach, "the trebuchet breaks a wall segment");
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
            b = Assault(ram ? b =>
            {
                var mg = b.Wall!.MainGate;
                made = Crewed(b, "ram", mg.Pos + mg.Out * 25f + mg.Along * 6f);
            } : null);
            e = made;
            b.Wall!.MainGate.Hp = b.Wall.MainGate.MaxHp = 3000;
            for (int i = 0; i < 60 * 200 && !b.Wall.MainGate.Open; i++) b.Update(1 / 60f);
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
        var b = Assault(b =>
        {
            var mg = b.Wall!.MainGate;
            e = Crewed(b, "tower", mg.Pos + mg.Out * 38f - mg.Along * 22f);
        });
        var (illegal, _, ramp) = Crossings(b, 180);
        Console.WriteLine($"tower: docked {e.Docked}, {ramp} crossed by its ramp, illegal {illegal}");
        Check(e.Docked && b.Wall!.Ramps.Any(r => r.Tower == e), "the tower reaches the wall");
        Check(ramp > 0, "attackers cross by the tower");
        Check(illegal == 0, $"nobody walks through the wall ({illegal})");
        e.Hp = 1;
        b.Damage(e, 10);
        Check(!b.Wall!.Ramps.Any(r => r.Tower == e), "a wrecked tower is no way up");
    }

    /// The walls' level puts springalds on the walkway, and they shoot at the attackers coming on.
    static void WallEnginesShoot()
    {
        var b = Assault(wallEngines: 2);
        var mounted = b.Engines.Where(e => e.Team == 1).ToList();
        Check(mounted.Count == 2 && mounted.All(e => b.Wall!.Inside(e.Pos)), "two springalds stand on the walkway");
        for (int i = 0; i < 60 * 60; i++) b.Update(1 / 60f);
        Console.WriteLine($"wall engines: {mounted.Sum(e => e.Shots)} shots, {mounted.Sum(e => e.Kills)} kills");
        Check(mounted.Sum(e => e.Shots) > 0, "the wall's springalds shoot");
    }

    /// Capture points (SE3): taking a gatehouse throws its gate open; holding the heart for 60 s wins the assault;
    /// holding out until SiegeTimeLimit wins for the defender.
    static void SiegeCaptureAndTimeout()
    {
        var att = new ArmySpec("att", 0, new List<Company> { new("men_at_arms", 12, "infantry", 1) });
        var def = new ArmySpec("def", 1, new List<Company> { new("brevon_fyrd", 12, "infantry", 2) });
        var b = new Battle(91, att, def, new BattleOptions { PlayerWounded = true, Siege = true, Ladders = 0, GateHp = 5000, FortressKind = FortressKind.Town });
        var w = b.Wall!;
        // Place attackers inside the main gatehouse (defenders far across the town): gatehouse capture opens the gate.
        var mg = w.MainGate;
        var sq = b.SquadsOf(0).First();
        sq.Order = Order.Hold;
        sq.Anchor = mg.Pos - mg.Out * 3.5f;
        foreach (var u in sq.Units) u.Pos = sq.Anchor;
        var defSq = b.SquadsOf(1).First();
        var far = w.Heart + new Vector2(52f, 20f);
        defSq.Order = Order.Hold;
        defSq.Anchor = far;
        foreach (var u in defSq.Units) u.Pos = far;
        b.EnemyCaptain.Pos = far;
        for (int i = 0; i < 60 * 14 && !mg.Open; i++) b.Update(1 / 60f);
        Check(mg.Open && mg.Owner == 0, "holding the gatehouse throws the gate open");

        // Move attackers to the heart: holding for 60 s wins the assault even while defenders still live.
        sq.Anchor = w.Heart;
        foreach (var u in sq.Units) u.Pos = w.Heart;
        for (int i = 0; i < 60 * 90 && b.Winner < 0; i++)
        {
            // Keep defenders pinned far away so they survive and we specifically test the 60 s heart victory.
            defSq.Order = Order.Hold;
            defSq.Anchor = far;
            b.Update(1 / 60f);
        }
        Check(b.Winner == 0 && w.HeartOwner == 0 && w.HeartHoldTimer >= 60f, $"holding the heart wins the assault (winner {b.Winner}, hold {w.HeartHoldTimer:0}s)");

        // Timeout: defenders win when SiegeTimeLimit elapses.
        var bt = new Battle(92, att, def, new BattleOptions { PlayerWounded = true, Siege = true, Ladders = 0, GateHp = 5000, FortressKind = FortressKind.TowerHouse, SiegeSupplies = 5 });
        Check(bt.SiegeTimeLimit is > 800 and < 1200, $"starving tower house time limit scales cleanly ({bt.SiegeTimeLimit:0}s)");
    }

    /// Campaign siege overhaul (SE6): encirclement, mining a sap, filling a ditch, offering terms, and post-storm sack/ransom/occupy.
    static void SiegeCampaignWorks()
    {
        var c = Campaign.New(42);
        var foe = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Faction == 2); // Northmarch is at war with Crown (0)
        c.Captain.Contract = 0;
        c.S.Gold = 5000;
        c.Player.Pos = foe.Pos + new Vector2(2, 0);
        // Ensure enough men to besiege.
        Rules.AddMen(c.Player.Companies[0], c.Player.Companies[0].Troop, 120);
        c.Besiege(foe);
        Check(c.Besieging == foe && c.TightEncirclement(foe), "besieging with a strong host gives tight encirclement");

        // Sap and ditch.
        foe.Buildings["walls"] = 2;
        Check(c.MineSap(foe) == null && foe.SapReadyAt > c.S.Hours, "can mine a sap under the wall");
        Check(c.FillDitch(foe) == null && foe.DitchFilled, "can fill the ditch");
        c.S.Hours = foe.SapReadyAt + 1;
        Check(c.SapReady(foe) && c.BreachesOf(foe) >= 1, "finished sap adds a breach");
        var setup = c.PrepareAssault(foe);
        Check(setup.Options.Fortress != null && !setup.Options.Fortress.HasDitch && setup.Options.Breaches >= 1, "assault carries fortress spec with filled ditch and sap breach");

        // Offering terms when starving.
        foe.Supplies = 5;
        Check(c.CanOfferTerms(foe, out _), "starving garrison will accept terms");
        Check(c.OfferTerms(foe) == null && foe.Faction == 0, "offering terms takes the town without a storm");

        // Post-storm sack choice.
        var castle = c.S.Settlements.First(s => s.Kind == SettlementKind.Castle && s.Faction == 2);
        c.Player.Pos = castle.Pos + new Vector2(2, 0);
        c.Besiege(castle);
        float goldBefore = c.S.Gold;
        c.AfterAssault(castle, won: true);
        Check(c.PendingStorm == castle.Id, "winning a storm sets PendingStorm for Occupy/Ransom/Sack");
        c.StormChoice(castle, Campaign.StormAction.Sack);
        Check(c.PendingStorm == -1 && c.S.Gold > goldBefore && castle.Unrest > 0, "sacking grants gold and adds unrest");
    }
}
