using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the rules of creatures (docs/CREATURES.md): a troll's sweeping, throwing blows, its healing and the sun (phase 3);
/// the undead's necromancer, who raises the fallen and whose death drops them all (phase 5).
public partial class Battle
{
    /// Open daylight: a troll is slow and weak in it and does not heal. Dawn, dusk, night and thick fog are troll weather.
    public bool Sunlit => Options.Weather is { Night: false, Hour: > 7.5f and < 18.5f, Fog: < 0.5f };

    /// Body radius on the ground (a huge creature takes more room) and its weight when bodies shove each other.
    static float BodyRadius(Unit u) => u.Mounted ? 1.0f : u.Type.Huge ? 0.375f * u.Type.Size : 0.375f;
    static float BodyMass(Unit u) => u.Mounted ? 6 : u.Type.Huge ? 8 : 1;

    /// A sweeping blow lands on its target and carries on into the men beside it (up to two more).
    void Sweep(Unit a, Unit first, float dmg, float reach)
    {
        int more = 0;
        foreach (var t in Near(a.Pos, reach + 1, nearBrace).Where(t => t != first && t.Team != a.Team && t.Active)
                     .OrderBy(t => Vector2.DistanceSquared(t.Pos, a.Pos)).ThenBy(t => t.Index).ToList())
            if (InArc(a, t, reach, 0.1f) && more++ < 2) Hit(a, t, dmg * 0.8f, false);
    }

    /// A blow from something huge throws a man off his feet and back, and shakes the men around him.
    void Knock(Unit a, Unit t)
    {
        if (!t.Active || t.Type.Huge || t.Mounted || Wall != null) return; // no throwing men through a siege wall
        var away = t.Pos - a.Pos;
        if (away.LengthSquared() < 1e-4f) away = a.Dir;
        t.Pos += Vector2.Normalize(away) * 1.3f;
        Stagger(t, 1.0f);
        foreach (var u in Near(t.Pos, 4, nearBrace))
            if (u.Active && u.Team == t.Team && Vector2.DistanceSquared(u.Pos, t.Pos) < 16) u.Morale -= 1.5f;
    }

    /// A troll heals while nothing has hurt it for a few seconds, but not under the sun.
    void Regen(Unit u, float dt)
    {
        if (Sunlit || Time - u.HurtAt < 3 || u.Hp >= u.Type.MaxHp) return;
        u.Hp = MathF.Min(u.Type.MaxHp, u.Hp + u.Type.MaxHp * 0.04f * dt);
    }

    /// How often a necromancer calls up the dead, how far his call carries, and how many he can hold up at once.
    public const float RaiseEvery = 2.5f, RaiseRange = 22;
    public const int RaiseMax = 40;
    readonly float[] raiseTimer = new float[2];
    readonly Squad?[] risen = new Squad?[2];

    /// Every few seconds each living necromancer raises up to two of the fallen near him (either side's, but not beasts,
    /// huge bodies, leaders or the undead themselves) as skeletons that charge at once.
    void RaiseDead(float dt)
    {
        for (int team = 0; team < 2; team++)
        {
            if ((raiseTimer[team] -= dt) > 0) continue;
            raiseTimer[team] = RaiseEvery;
            foreach (var n in Units.Where(u => u.Active && u.Team == team && u.Type.Has("raise")).ToList())
                foreach (var c in Units.Where(c => c.Dead && !c.Raised && Time - c.DiedAt > 1 && Raisable(c) && Vector2.DistanceSquared(c.Pos, n.Pos) < RaiseRange * RaiseRange)
                             .OrderBy(c => Vector2.DistanceSquared(c.Pos, n.Pos)).ThenBy(c => c.Index).Take(2).ToList())
                    if ((risen[team]?.StartCount ?? 0) < RaiseMax) Rise(n, c);
        }
    }

    static bool Raisable(Unit c) => !c.Type.Huge && !c.Type.Look.Beast && !c.Type.Has("raised") && !c.IsLeader && !c.IsPlayer && !c.Hero;

    void Rise(Unit master, Unit corpse)
    {
        var s = risen[master.Team];
        if (s == null)
        {
            s = risen[master.Team] = new Squad("The Risen", master.Team) { Anchor = corpse.Pos, Front = master.Dir, Order = Order.Charge, Formation = Formation.Loose };
            Squads.Add(s);
            if (!scripted) Role(master.Team, "infantry").Add(s);
            Log(master.Team == 0 ? "The dead rise to fight for you!" : "The dead rise to fight for the necromancer!");
        }
        corpse.Raised = true;
        var u = Spawn(Troops.Get("skeleton"), master.Team, s, corpse.Pos);
        u.Face(master.Dir);
        u.Heading = u.Facing;
        s.Units.Add(u);
        s.StartCount++;
        s.MarchSpeed = MathF.Min(s.MarchSpeed, u.Type.Speed);
    }

    /// The last necromancer of a side is slain: every undead he held up falls where it stands, and his foes take heart.
    void MasterFalls(Unit master)
    {
        foreach (var u in Units)
            if (u.Active && u.Team == master.Team && u.Type.Has("raised")) Collapse(u);
            else if (u.Active && u.Team != master.Team) u.Morale = MathF.Min(100, u.Morale + 10);
        Log($"The {master.Type.Name.ToLowerInvariant()} is slain! {(master.Team == 0 ? "Your" : "His")} dead fall still.");
    }

    /// A raised body falls apart: dead, but no one's kill.
    void Collapse(Unit u)
    {
        u.Dead = true;
        u.DiedAt = Time;
        u.Hp = 0;
        u.Action = UnitAction.None;
        u.Vel = u.Wish = Vector2.Zero;
    }
}
