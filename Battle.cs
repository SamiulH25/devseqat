using Microsoft.Xna.Framework;

namespace Ironfield;

public struct PlayerInput
{
    public Vector2 Move;   // world-space wish direction, length 0..1
    public float Facing;
    public bool Sprint, Attack, Block, Rally;
}

public sealed class Projectile
{
    public Vector3 Pos, Vel;
    public Unit Shooter = null!;
    public float Damage, Stuck = -1; // Stuck >= 0: seconds spent lying in the ground
}

/// Headless battle simulation. No graphics, so the self-test can run it.
public sealed class Battle
{
    public const float MapRadius = 95, Gravity = 9.8f, ArrowSpeed = 28, AuraRadius = 12, RallyRadius = 15;

    public readonly List<Unit> Units = new();
    public readonly List<Squad> Squads = new();
    public readonly List<Projectile> Arrows = new();
    public readonly List<(string Text, float At)> Events = new();
    public Unit Player = null!, EnemyCaptain = null!;
    public PlayerInput Input;
    public bool Commanding; // player is in command view: no leadership aura, body stands idle
    public int Winner = -1, PlayerKills;
    public float Time, RallyCooldown;

    readonly Random rng;
    readonly Vector2[] centroid = new Vector2[2];
    Squad eInf = null!, eArch = null!, eSkirm = null!, eGuard = null!;
    bool enemyCharged;
    float generalTimer;

    public Battle(int seed)
    {
        rng = new Random(seed);
        Player = Spawn(UnitType.Captain, 0, null, new Vector2(0, -34));
        Player.IsPlayer = Player.IsLeader = true;
        Player.Facing = MathHelper.PiOver2;
        AddSquad(0, UnitType.Shieldman, 24, new(0, -30), 12, Vector2.UnitY);
        AddSquad(0, UnitType.Bowman, 16, new(0, -38), 8, Vector2.UnitY);
        AddSquad(0, UnitType.Spearrunner, 12, new(-22, -31), 6, Vector2.UnitY);

        eInf = AddSquad(1, UnitType.Shieldman, 28, new(0, 30), 14, -Vector2.UnitY);
        eArch = AddSquad(1, UnitType.Bowman, 18, new(0, 38), 9, -Vector2.UnitY);
        eSkirm = AddSquad(1, UnitType.Spearrunner, 14, new(24, 31), 7, -Vector2.UnitY);
        eGuard = AddSquad(1, UnitType.Captain, 1, new(0, 34), 1, -Vector2.UnitY);
        EnemyCaptain = eGuard.Units[0];
        EnemyCaptain.IsLeader = true;
        UpdateCentroids();
    }

    public IEnumerable<Squad> SquadsOf(int team) => Squads.Where(s => s.Team == team);
    public Vector2 Centroid(int team) => centroid[team];
    public void Log(string text) => Events.Add((text, Time));

    Squad AddSquad(int team, UnitType type, int n, Vector2 anchor, int cols, Vector2 front)
    {
        var s = new Squad(type.Name, team) { Anchor = anchor, Columns = cols, Front = front, StartCount = n };
        for (int i = 0; i < n; i++)
        {
            var u = Spawn(type, team, s, s.Slot(i, n));
            u.Face(front);
            s.Units.Add(u);
        }
        Squads.Add(s);
        return s;
    }

    Unit Spawn(UnitType t, int team, Squad? s, Vector2 pos)
    {
        var u = new Unit { Type = t, Team = team, Squad = s, Pos = pos, Hp = t.MaxHp, Ammo = t.Ammo, Anim = rng.NextSingle() * 2 };
        Units.Add(u);
        return u;
    }

    public void Update(float dt)
    {
        Time += dt;
        RallyCooldown = MathF.Max(0, RallyCooldown - dt);
        UpdateCentroids();
        if ((generalTimer -= dt) <= 0) { generalTimer = 0.5f; EnemyGeneral(); }
        foreach (var s in Squads) UpdateSquad(s);
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            u.HitFlash = MathF.Max(0, u.HitFlash - dt);
            u.Cooldown -= dt;
            if (u.IsPlayer) UpdatePlayer(u, dt); else Think(u);
            TickAction(u, dt);
        }
        Move(dt);
        UpdateArrows(dt);
        UpdateMorale(dt);
        CheckEnd();
        Events.RemoveAll(e => Time - e.At > 6);
    }

    void UpdateCentroids()
    {
        for (int t = 0; t < 2; t++)
        {
            var sum = Vector2.Zero;
            int n = 0;
            foreach (var u in Units) if (u.Team == t && u.Active && !u.Routing) { sum += u.Pos; n++; }
            if (n > 0) centroid[t] = sum / n;
        }
    }

    void UpdateSquad(Squad s)
    {
        if (s.Order == Order.Follow && Player.Active)
        {
            var toEnemy = centroid[1 - s.Team] - Player.Pos;
            if (toEnemy.LengthSquared() > 1) s.Front = Vector2.Normalize(toEnemy);
            s.Anchor = Player.Pos - s.Front * (3 + Squads.IndexOf(s) * 5); // squads queue up behind the player
        }
        int n = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing) n++;
        s.Alive = n;
        int i = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing) { u.Slot = s.Slot(i++, n); }
    }

    (Unit? unit, float dist) Nearest(Unit u)
    {
        Unit? best = null;
        float bd = float.MaxValue;
        foreach (var o in Units)
        {
            if (o.Team == u.Team || !o.Active || o.Routing) continue;
            float d = Vector2.DistanceSquared(o.Pos, u.Pos);
            if (d < bd) { bd = d; best = o; }
        }
        return (best, MathF.Sqrt(bd));
    }

    void Think(Unit u)
    {
        u.Wish = Vector2.Zero;
        if (u.Routing)
        {
            var away = u.Pos - centroid[1 - u.Team];
            if (away.LengthSquared() < 1e-3f) away = Vector2.UnitX;
            away.Normalize();
            u.Wish = away * u.Type.Speed * 1.15f;
            u.Face(away);
            u.EnemyDist = float.MaxValue;
            if (u.Pos.Length() > MapRadius) u.Fled = true;
            return;
        }
        if (u.Action != UnitAction.None && u.Action != UnitAction.Block) return; // committed to a swing

        var s = u.Squad!;
        var (enemy, d) = Nearest(u);
        u.EnemyDist = d;
        float toSlot = Vector2.Distance(u.Pos, u.Slot);
        bool charge = s.Order == Order.Charge;

        // Archers shoot once settled in formation (or anywhere when charging).
        if (enemy != null && u.Ammo > 0 && d > 4 && d < u.Type.Range && (charge || toSlot < 2.5f))
        {
            u.Face(enemy.Pos - u.Pos);
            if (u.Cooldown <= 0) Shoot(u, enemy);
            return;
        }

        bool leashed = !charge && toSlot > 14;
        if (enemy != null && !leashed && (charge || d < 6))
        {
            var to = enemy.Pos - u.Pos;
            u.Face(to);
            if (d <= u.Type.Reach)
            {
                if (u.Cooldown <= 0) { u.Action = UnitAction.Windup; u.ActionTimer = u.Type.Windup; u.Target = enemy; }
            }
            else u.Wish = to / d * u.Type.Speed;
            return;
        }

        if (toSlot > 0.4f)
        {
            var to = u.Slot - u.Pos;
            u.Wish = to / toSlot * u.Type.Speed * MathF.Min(1, toSlot);
            u.Face(to);
        }
        else u.Face(s.Front);
    }

    void UpdatePlayer(Unit p, float dt)
    {
        var inp = Input;
        if (Commanding) { inp = default; p.Charging = false; p.Charge = 0; }
        else p.Facing = inp.Facing;

        bool busy = p.Action is UnitAction.Windup or UnitAction.Recover or UnitAction.Stagger;
        if (!busy)
        {
            if (inp.Block && !p.Charging) p.Action = UnitAction.Block;
            else if (p.Action == UnitAction.Block) p.Action = UnitAction.None;
            if (p.Action == UnitAction.None)
            {
                if (inp.Attack) { p.Charging = true; p.Charge = MathF.Min(1, p.Charge + dt / 0.7f); }
                else if (p.Charging)
                {
                    // Release: power scales with how long the swing was held.
                    p.Charging = false;
                    p.Power = 0.6f + p.Charge;
                    p.Charge = 0;
                    p.Action = UnitAction.Windup;
                    p.ActionTimer = 0.15f;
                    p.Stamina -= 8;
                }
            }
        }

        bool sprint = inp.Sprint && inp.Move != Vector2.Zero && p.Stamina > 5 && !p.Charging && p.Action == UnitAction.None;
        float speed = p.Type.Speed * (sprint ? 1.6f : 1)
            * (p.Charging || p.Action == UnitAction.Block ? 0.5f : 1)
            * (p.Action == UnitAction.Stagger ? 0 : busy ? 0.3f : 1);
        p.Wish = inp.Move * speed;
        p.Stamina = Math.Clamp(p.Stamina + (sprint ? -20 : p.Action == UnitAction.Block ? 4 : 15) * dt, 0, 100);
        if (inp.Rally && RallyCooldown <= 0 && p.Stamina >= 30) Rally(p);
    }

    void Rally(Unit p)
    {
        p.Stamina -= 30;
        RallyCooldown = 12;
        foreach (var u in Units)
            if (u.Active && u.Team == p.Team && !u.IsPlayer && Vector2.DistanceSquared(u.Pos, p.Pos) < RallyRadius * RallyRadius)
                u.Morale = MathF.Min(100, u.Morale + (u.Routing ? 45 : 25));
        Log("Rally! Hold the line!");
    }

    void TickAction(Unit u, float dt)
    {
        if (u.Action == UnitAction.None || (u.IsPlayer && u.Action == UnitAction.Block)) return;
        if ((u.ActionTimer -= dt) > 0) return;
        switch (u.Action)
        {
            case UnitAction.Windup:
                Strike(u);
                u.Action = UnitAction.Recover;
                u.ActionTimer = u.IsPlayer ? 0.3f : u.Type.Recover;
                break;
            case UnitAction.Recover:
                u.Action = UnitAction.None;
                if (!u.IsPlayer) u.Cooldown = MathF.Max(u.Cooldown, rng.NextSingle() * 0.5f); // desync the line
                break;
            default:
                u.Action = UnitAction.None;
                break;
        }
    }

    void Strike(Unit a)
    {
        float dmg = a.Type.Damage * a.Power;
        if (!a.IsPlayer)
        {
            if (a.Target is { Active: true } t && InArc(a, t, a.Type.Reach + 0.4f, 0.5f)) Hit(a, t, dmg, false);
            return;
        }
        int hits = 0; // player swings cleave up to 3 enemies in a wide arc
        foreach (var t in Units)
            if (t.Team != a.Team && t.Active && InArc(a, t, a.Type.Reach + 0.3f, 0.35f) && hits++ < 3) Hit(a, t, dmg, false);
    }

    static bool InArc(Unit a, Unit t, float reach, float minDot)
    {
        var d = t.Pos - a.Pos;
        float l = d.Length();
        return l <= reach && (l < 0.01f || Vector2.Dot(d / l, a.Dir) >= minDot);
    }

    void Hit(Unit a, Unit t, float dmg, bool ranged)
    {
        var from = a.Pos - t.Pos;
        bool facing = from.LengthSquared() > 1e-4f && Vector2.Dot(Vector2.Normalize(from), t.Dir) > 0.4f;
        if (t.IsPlayer)
        {
            if (facing && t.Action == UnitAction.Block)
            {
                t.Stamina -= ranged ? 4 : dmg * 0.8f;
                if (t.Stamina <= 0) { t.Stamina = 0; Stagger(t, 0.8f); Log("Guard broken!"); }
                else if (!ranged) Stagger(a, 0.35f); // a clean block staggers the attacker
                return;
            }
        }
        else if (facing && t.Action is UnitAction.None or UnitAction.Block && rng.NextSingle() < t.Type.BlockChance)
        {
            t.Action = UnitAction.Block;
            t.ActionTimer = 0.35f;
            return;
        }

        float real = MathF.Max(dmg * 0.25f, dmg - t.Type.Armor);
        t.Hp -= real;
        t.HitFlash = 0.15f;
        t.Morale -= real * 0.25f;
        if (t.Action == UnitAction.Windup || t.Charging) Stagger(t, 0.3f); // hits interrupt swings
        if (t.Hp <= 0) Kill(t, a);
    }

    static void Stagger(Unit u, float time)
    {
        u.Action = UnitAction.Stagger;
        u.ActionTimer = time;
        u.Charging = false;
        u.Charge = 0;
    }

    void Kill(Unit t, Unit killer)
    {
        t.Dead = true;
        t.Hp = 0;
        t.Action = UnitAction.None;
        t.Vel = t.Wish = Vector2.Zero;
        if (killer.IsPlayer) PlayerKills++;
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            float d = Vector2.Distance(u.Pos, t.Pos);
            if (u.Team == t.Team) { if (t.IsLeader) u.Morale -= 25; else if (d < 10) u.Morale -= 5; }
            else if (d < 10) u.Morale += killer.IsPlayer ? 6 : 2; // heroics inspire more
        }
        if (t.IsPlayer) Log("You have fallen! Your troops waver.");
        else if (t.IsLeader) Log("The enemy captain is slain!");
    }

    void Shoot(Unit u, Unit t)
    {
        u.Ammo--;
        u.Cooldown = u.Type.Windup + u.Type.Recover + 0.8f + rng.NextSingle() * 0.8f;
        u.Action = UnitAction.Recover; // shows the release pose
        u.ActionTimer = 0.25f;
        float d = Vector2.Distance(u.Pos, t.Pos);
        var aim = t.Pos + t.Vel * (d / ArrowSpeed); // lead the target
        aim += new Vector2(Gauss(), Gauss()) * d * 0.035f;
        float time = MathF.Max(0.1f, Vector2.Distance(u.Pos, aim) / ArrowSpeed);
        var h = (aim - u.Pos) / time;
        // Launch at 1.5 m, land at 1.0 m after `time` seconds.
        Arrows.Add(new Projectile
        {
            Pos = new(u.Pos.X, 1.5f, u.Pos.Y),
            Vel = new(h.X, 0.5f * Gravity * time - 0.5f / time, h.Y),
            Shooter = u,
            Damage = u.Type.Damage,
        });
    }

    float Gauss() => MathF.Sqrt(-2 * MathF.Log(1 - rng.NextSingle())) * MathF.Cos(MathF.Tau * rng.NextSingle());

    void UpdateArrows(float dt)
    {
        for (int i = Arrows.Count - 1; i >= 0; i--)
        {
            var p = Arrows[i];
            if (p.Stuck >= 0)
            {
                if ((p.Stuck += dt) > 10) Arrows.RemoveAt(i);
                continue;
            }
            p.Vel.Y -= Gravity * dt;
            p.Pos += p.Vel * dt;
            if (p.Pos.Y <= 0) { p.Pos.Y = 0; p.Stuck = 0; continue; }
            if (p.Pos.Y > 2) continue;
            var flat = new Vector2(p.Pos.X, p.Pos.Z);
            foreach (var t in Units)
            {
                if (t.Team == p.Shooter.Team || !t.Active || Vector2.DistanceSquared(t.Pos, flat) > 0.16f) continue;
                Hit(p.Shooter, t, p.Damage, true);
                Arrows.RemoveAt(i);
                break;
            }
        }
    }

    void Move(float dt)
    {
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            u.Vel = u.Wish;
            u.Pos += u.Vel * dt;
            u.Anim += dt * u.Vel.Length() * 0.9f; // stride-locked walk cycle
        }
        // shortcut: O(n^2) separation, fine for ~150 units; switch to a spatial grid past ~400.
        const float r = 0.75f;
        for (int i = 0; i < Units.Count; i++)
        {
            var a = Units[i];
            if (!a.Active) continue;
            for (int j = i + 1; j < Units.Count; j++)
            {
                var b = Units[j];
                if (!b.Active) continue;
                var d = b.Pos - a.Pos;
                float l2 = d.LengthSquared();
                if (l2 >= r * r) continue;
                if (l2 < 1e-6f) { d = new Vector2(0.01f, 0); l2 = 1e-4f; }
                float l = MathF.Sqrt(l2);
                var push = d / l * (r - l) * 0.5f;
                a.Pos -= push;
                b.Pos += push;
            }
        }
    }

    void UpdateMorale(float dt)
    {
        foreach (var u in Units)
        {
            if (!u.Active || u.IsLeader) continue; // leaders never break
            var lead = u.Team == 0 ? Player : EnemyCaptain;
            bool inspired = lead.Active && !(lead.IsPlayer && Commanding)
                && Vector2.DistanceSquared(lead.Pos, u.Pos) < AuraRadius * AuraRadius;
            float m = 0;
            if (inspired) m += u.Routing ? 8 : 3;
            if (u.Squad!.Alive < u.Squad.StartCount * 0.4f) m -= 1.5f;
            if (u.EnemyDist > 12 && u.Morale < 60) m += 1;
            u.Morale = Math.Clamp(u.Morale + m * dt, 0, 100);
            if (!u.Routing && u.Morale < 15) { u.Routing = true; u.Action = UnitAction.None; }
            else if (u.Routing && u.Morale > 45) u.Routing = false;
        }
    }

    /// Enemy commander: advance as a block, charge when close, archers stop at bow range.
    void EnemyGeneral()
    {
        var front = eInf.Alive > 0 ? eInf.Centroid() : eGuard.Centroid();
        var dir = centroid[0] - front;
        if (dir.LengthSquared() < 1) return;
        dir.Normalize();
        var side = new Vector2(-dir.Y, dir.X);
        if (!enemyCharged)
        {
            eInf.Front = dir;
            eInf.Anchor = front + dir * 6;
            eSkirm.Front = dir;
            eSkirm.Anchor = eInf.Anchor + side * 16; // swing wide on a flank
            if (GapTo(eInf, 0) < 16 || GapTo(eSkirm, 0) < 10)
            {
                enemyCharged = true;
                eInf.Order = eSkirm.Order = Order.Charge;
                Log("The enemy line charges!");
            }
        }
        if (eArch.Order != Order.Charge)
        {
            eArch.Front = dir;
            float gap = GapTo(eArch, 0);
            if (gap > UnitType.Bowman.Range - 4) eArch.Anchor = eArch.Centroid() + dir * 4;
            if (gap < 7 || eArch.Units.All(u => !u.Active || u.Ammo == 0)) eArch.Order = Order.Charge;
        }
        eGuard.Front = dir;
        eGuard.Anchor = front - dir * 4; // captain leads from just behind the line
    }

    float GapTo(Squad s, int team)
    {
        float best = float.MaxValue;
        foreach (var a in s.Units)
        {
            if (!a.Active) continue;
            foreach (var b in Units)
                if (b.Team == team && b.Active) best = MathF.Min(best, Vector2.DistanceSquared(a.Pos, b.Pos));
        }
        return MathF.Sqrt(best);
    }

    void CheckEnd()
    {
        if (Winner >= 0) return;
        for (int t = 0; t < 2; t++)
        {
            if (Units.Any(u => u.Team == t && u.Active && !u.Routing)) continue;
            Winner = 1 - t;
            Log(Winner == 0 ? "Victory! The field is yours." : "Defeat. Your army is broken.");
            return;
        }
    }
}
