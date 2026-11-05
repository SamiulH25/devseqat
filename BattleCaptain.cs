using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap C3 (captain combat: timed parries, chambering, shield bash, a second weapon, calling your horse),
/// C7 (fatigue, ammunition wagons) and parts of C2 (a reserve, focused archers, a general who knows when to quit).
public sealed partial class Battle
{
    public const float ParryWindow = 0.35f;
    /// The supply wagon behind each side's lines, and the arrows left in it.
    public readonly Vector2[] Wagons = { new(-8, -52), new(8, 52) };
    public readonly int[] WagonStock = { 300, 300 };
    LooseHorse? called;
    UnitType? primary, secondary;

    /// Your second weapon on foot: a two-handed blade (more damage and reach; no shield, so only parries stop a blow cleanly).
    public bool TwoHanded => Player.Type == secondary;

    void SetupCaptain()
    {
        primary = Player.Type;
        secondary = primary.Clone();
        secondary.Look = primary.Look with { Weapon = "greatsword", Shield = "none" };
        secondary.Damage *= 1.35f;
        secondary.Reach += 0.5f;
        if (!Terrain.IsFlat) Terrain.AddProp(new Foliage(FoliageKind.Wagon, Wagons[0], 1, 1.4f, 1));
        if (!Terrain.IsFlat) Terrain.AddProp(new Foliage(FoliageKind.Wagon, Wagons[1], 1, 1.4f, 2));
    }

    void SwapWeapon(Unit p)
    {
        if (p.Mounted) { Log("Two hands for the sword: dismount first."); return; }
        if (p.Action != UnitAction.None || p.Charging) return;
        p.Type = TwoHanded ? primary! : secondary!;
        p.Action = UnitAction.Stagger; // a moment to change grip
        p.ActionTimer = 0.4f;
        Log(TwoHanded ? "You take up the greatsword." : "Sword and shield.");
    }

    /// Shield bash (or a kick, with two hands): staggers a man in front of you, through his guard.
    void Bash(Unit p)
    {
        if (p.Action != UnitAction.None || p.Charging || p.Mounted || p.Stamina < 15) return;
        p.Stamina -= 15;
        p.Action = UnitAction.Recover;
        p.ActionTimer = 0.35f;
        Unit? best = null;
        float bd = 1.9f;
        foreach (var t in Units)
        {
            if (t.Team == p.Team || !t.Active || t.Mounted) continue;
            var d = t.Pos - p.Pos;
            float l = d.Length();
            if (l < bd && (l < 0.01f || Vector2.Dot(d / l, p.Dir) > 0.5f)) { bd = l; best = t; }
        }
        if (best == null) { PlayerHit = ("BASH: NOTHING THERE", Time); return; }
        Stagger(best, 1.0f);
        best.Pos += Vector2.Normalize(best.Pos - p.Pos + new Vector2(1e-4f, 0)) * 1.2f;
        best.Morale -= 3;
        Damage(p, best, 6, 2);
        Sound(Cue.Block, best.Pos);
        PlayerHit = (TwoHanded ? "KICK!" : "SHIELD BASH!", Time);
    }

    /// Whistle for a riderless horse of your side: it trots to you.
    bool CallHorse(Unit p)
    {
        called = LooseHorses.Where(h => h.Team == p.Team && Vector2.Distance(h.Pos, p.Pos) < 80).OrderBy(h => Vector2.Distance(h.Pos, p.Pos)).FirstOrDefault();
        if (called == null) return false;
        Log("You whistle for a horse.");
        return true;
    }

    void UpdateCalledHorse(float dt)
    {
        if (called == null) return;
        if (!LooseHorses.Contains(called) || Player.Mounted || !Player.Active) { called = null; return; }
        var d = Player.Pos - called.Pos;
        float l = d.Length();
        if (l < 1.8f) { called = null; return; }
        called.Pos += d / l * MathF.Min(l, 7 * dt);
        called.Heading = MathF.Atan2(d.Y, d.X);
    }

    /// Men tire running and fighting (slower, weaker) and recover standing still. Horses and you are exempt (you have stamina).
    float Fatigue(Unit u) => u.IsPlayer ? 1 : 0.7f + 0.3f * u.Stamina / 100;

    void Tire(Unit u, float dt)
    {
        if (u.IsPlayer || u.Mounted) return;
        float run = u.Vel.Length() / MathF.Max(0.1f, u.Type.Speed);
        float rate = run > 0.7f ? -2.5f * run : u.Action != UnitAction.None ? -1f : run < 0.2f ? 4f : 1f;
        u.Stamina = Math.Clamp(u.Stamina + rate * dt, 0, 100);
    }

    /// Archers standing at their own wagon draw arrows from it.
    void Resupply(float dt)
    {
        if ((resupplyTimer -= dt) > 0) return;
        resupplyTimer = 1.5f;
        foreach (var u in Units)
        {
            if (!u.Active || u.IsPlayer || u.Type.Ammo == 0 || u.Ammo >= u.Type.Ammo || WagonStock[u.Team] <= 0) continue;
            if (Vector2.Distance(u.Pos, Wagons[u.Team]) > 7) continue;
            int n = Math.Min(4, Math.Min(u.Type.Ammo - u.Ammo, WagonStock[u.Team]));
            u.Ammo += n;
            WagonStock[u.Team] -= n;
        }
    }
    float resupplyTimer;

    /// The AI's archers: out of arrows, go to the wagon if it has any; full again, come back. Returns true if it took charge of s.
    bool ArcherSupply(Squad s, int team)
    {
        bool empty = s.Units.Where(u => u.Active).All(u => u.Ammo == 0);
        if (!empty || WagonStock[team] <= 0) return false;
        s.Order = Order.Hold;
        s.Anchor = Wagons[team];
        return true;
    }

    /// The softest enemy company in reach of these archers: least armour, then nearest.
    Squad? SoftTarget(Squad archers, int foe)
    {
        var at = archers.Centroid();
        float range = archers.Units.FirstOrDefault()?.Type.Range ?? 30;
        return SquadsOf(foe).Where(s => s.Alive > 0 && Vector2.Distance(s.Centroid(), at) < range * 1.1f)
            .OrderBy(s => s.Units.First().Type.Armor + (s.Cavalry ? 3 : 0)).ThenBy(s => Vector2.Distance(s.Centroid(), at)).FirstOrDefault();
    }

    /// A beaten side sounds the retreat instead of dying to the last man (either side, so it is not an escape only the AI has).
    bool SoundRetreat(int team)
    {
        if (Time < 45 || retreated[team]) return false;
        float Str(int t) => Units.Where(u => u.Team == t && u.Active && !u.Routing).Sum(u => u.Hp + 5 * u.Type.Armor);
        float mine = Str(team), theirs = Str(1 - team);
        if (mine > theirs * 0.18f) return false;
        retreated[team] = true;
        foreach (var u in Units) if (u.Team == team && u.Active && !u.IsLeader) u.Routing = true;
        Log(team == 1 ? "The enemy sounds the retreat!" : "Your men sound the retreat!");
        Sound(Cue.Horn, centroid[team]);
        return true;
    }
    readonly bool[] retreated = new bool[2];
}
