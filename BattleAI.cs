using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: unit AI (Think, ThinkCav for cavalry, steering helpers) and the AI general that gives squads orders.
public partial class Battle
{
    void Think(Unit u, float dt)
    {
        if (u.Routing)
        {
            var away = u.Pos - centroid[1 - u.Team];
            if (away.LengthSquared() < 1e-3f) away = Vector2.UnitX;
            away.Normalize();
            u.Couched = false;
            u.Wish = away * (u.Mounted ? u.Horse!.Gallop : u.Type.Speed * 1.15f);
            if (!u.Mounted) u.Face(away);
            u.EnemyDist = float.MaxValue;
            if (u.Pos.Length() > MapRadius) u.Fled = true;
            return;
        }
        if (u.Action != UnitAction.None && u.Action != UnitAction.Block)
        {
            if (!u.Mounted) u.Wish = Vector2.Zero; // committed to a swing; a rider keeps riding
            return;
        }

        var s = u.Squad!;
        var (enemy, d) = Nearest(u);
        u.EnemyDist = d;
        if (u.Mounted) { ThinkCav(u, s, enemy, d, dt); return; }
        u.Wish = Vector2.Zero;

        float toSlot = Vector2.Distance(u.Pos, u.Slot);
        bool charge = s.Order == Order.Charge;
        if (charge && s.Target != null && d > 3) (enemy, d) = Nearest(u, s.Target); // go for the ordered target

        // Archers shoot once settled in formation (or anywhere when charging).
        if (s.Target != null && !charge && u.Ammo > 0)
        {
            var (tu, td) = Nearest(u, s.Target);
            if (tu != null && td < u.Type.Range) (enemy, d) = (tu, td);
        }
        float range = MathF.Min(Weather.Vision, u.Type.Range * (enemy != null && Terrain.Height(u.Pos) > Terrain.Height(enemy.Pos) + 2 ? 1.2f : 1)); // height carries arrows further; fog and night hide targets
        if (enemy != null && u.Ammo > 0 && s.FireAtWill && d > 4 && d < range && (charge || toSlot < 2.5f))
        {
            u.Face(enemy.Pos - u.Pos);
            if (u.Cooldown <= 0) Shoot(u, enemy);
            return;
        }

        float reach = enemy == null ? 0 : u.Type.Reach + (enemy.Mounted ? 0.9f : 0);
        // Brace for the charge: hold the slot and present the point instead of stepping out to meet it.
        if (enemy is { Mounted: true } && CanBrace(u) && d < 16 && toSlot < 6)
        {
            u.Face(enemy.Pos - u.Pos);
            if (d <= reach && u.Cooldown <= 0 && enemy.Speed < 4) StartSwing(u, enemy);
            return;
        }

        bool leashed = !charge && toSlot > 14;
        if (enemy != null && !leashed && (charge || d < 6))
        {
            var to = Via(u, enemy.Pos) - u.Pos;
            u.Face(to);
            if (d <= reach) { if (u.Cooldown <= 0) StartSwing(u, enemy); }
            else u.Wish = to / MathF.Max(0.01f, to.Length()) * u.Type.Speed;
            return;
        }

        if (toSlot > 0.4f)
        {
            var to = Via(u, u.Slot) - u.Pos;
            u.Wish = to / toSlot * MathF.Min(u.Type.Speed, s.MarchSpeed) * MathF.Min(1, toSlot);
            u.Face(to);
        }
        else u.Face(s.FaceAt(u.Slot));
    }

    /// Cavalry AI. Wish is the desired velocity; Steer() turns it into heading and speed with momentum.
    void ThinkCav(Unit u, Squad s, Unit? enemy, float d, float dt)
    {
        var h = u.Horse!;
        var hd = u.HeadingDir;

        // Riders strike at whatever comes into reach as they pass (not with a couched lance).
        if (enemy != null && !u.Couched && u.Cooldown <= 0
            && d <= MathF.Min(u.Type.Reach, MountedMeleeReach) + (enemy.Mounted ? 0.5f : 0.2f) + MathF.Abs(u.Speed) * 0.08f
            && Vector2.Dot((enemy.Pos - u.Pos) / MathF.Max(d, 0.01f), hd) > -0.3f)
            StartSwing(u, enemy);

        // Horse archers loose from the saddle at anything in range, whatever their order.
        if (u.Ammo > 0 && enemy != null && s.FireAtWill && d > 5 && d < u.Type.Range && u.Cooldown <= 0 && u.Action == UnitAction.None)
        {
            Shoot(u, enemy);
            u.Cooldown *= 1.3f; // slower from horseback
        }
        if (u.Ammo > 0 && s.Order == Order.Charge && enemy != null)
        {
            // Skirmish: ride in to bow range, wheel away when they come close, circle the enemy's flank.
            var away = u.Pos - enemy.Pos;
            var side = new Vector2(-away.Y, away.X) / MathF.Max(0.01f, d);
            var dir = d < u.Type.Range * 0.45f ? away / MathF.Max(0.01f, d) + side * 0.4f : d > u.Type.Range * 0.8f ? -away / MathF.Max(0.01f, d) : side;
            if (u.Pos.Length() > MapRadius - 20) dir -= Vector2.Normalize(u.Pos);
            u.Wish = Vector2.Normalize(Via(u, u.Pos + dir * 10) - u.Pos + new Vector2(1e-4f, 0)) * (h.Trot + h.Gallop) / 2;
            u.Couched = false;
            return;
        }
        if (s.Order != Order.Charge)
        {
            u.Cav = CavState.Charge;
            u.Contact = u.Couched = false;
            float toSlot = Vector2.Distance(u.Pos, u.Slot);
            if (enemy != null && d < 15 && toSlot < 25)
            {
                // Counter-charge whatever comes close: horses standing still are easy meat for a charge.
                u.Wish = (enemy.Pos - u.Pos) / MathF.Max(d, 0.01f) * (d > 5 ? h.Gallop : h.Trot);
                if (u.Type.Class == UnitClass.Cavalry && u.Speed > 6.5f && d > 4 && u.Cooldown <= 0) u.Couched = true;
                return;
            }
            Ride(u, Via(u, u.Slot), toSlot > 12 ? h.Gallop * 0.8f : h.Trot);
            return;
        }

        var (t, td) = s.Target != null ? Nearest(u, s.Target) : (enemy, d);
        if (t == null) { (t, td) = (enemy, d); }
        if (t == null) { Ride(u, u.Slot, h.Trot); return; }

        switch (u.Cav)
        {
            case CavState.Withdraw:
            {
                // Ride out, away from the enemy mass, then turn for another charge.
                u.CavTimer -= dt;
                u.Couched = false;
                var away = u.Pos - centroid[1 - u.Team];
                away = away.LengthSquared() > 1e-3f ? Vector2.Normalize(away) : hd;
                var dir = hd + away * 1.5f;
                if (u.Pos.Length() > MapRadius - 25) dir -= Vector2.Normalize(u.Pos) * 3; // stay on the field
                u.Wish = Vector2.Normalize(dir) * h.Gallop;
                if (u.CavTimer <= 0) u.Cav = CavState.Charge;
                return;
            }
            case CavState.Melee:
            {
                // Bogged down: stand and hack, then break off.
                u.CavTimer -= dt;
                u.Wish = td > MountedMeleeReach ? (t.Pos - u.Pos) / td * h.Trot * 0.6f : Vector2.Zero;
                if (u.CavTimer <= 0) { u.Cav = CavState.Withdraw; u.CavTimer = 4f; }
                return;
            }
            default:
            {
                // Charge: aim a few metres past the target to ride through it.
                var to = (t.Pos - u.Pos) / MathF.Max(td, 0.01f);
                var aimAt = Via(u, t.Pos + to * 6);
                u.Wish = (aimAt - u.Pos).LengthSquared() > 1e-4f ? Vector2.Normalize(aimAt - u.Pos) * h.Gallop : Vector2.Zero;
                if (u.Type.Class == UnitClass.Cavalry && u.Speed > 6.5f && td < 30 && u.Cooldown <= 0) u.Couched = true;
                if (td < 4) u.Contact = true;
                if (u.Contact && td > 4 && Vector2.Dot(t.Pos - u.Pos, hd) < 0)
                {
                    u.Cav = CavState.Withdraw; // rode through: wheel away
                    u.CavTimer = 4f;
                    u.Contact = false;
                }
                u.Bogged = u.Speed < 2.5f && td < 4 ? u.Bogged + dt : 0;
                if (u.Bogged > 1.2f) { u.Cav = CavState.Melee; u.CavTimer = 5f; u.Bogged = 0; u.Couched = false; }
                return;
            }
        }
    }

    static void Ride(Unit u, Vector2 to, float speed)
    {
        var d = to - u.Pos;
        float l = d.Length();
        if (l < 1.2f) { u.Wish = Vector2.Zero; u.Face(u.Squad!.Front); return; }
        u.Wish = d / l * MathF.Min(speed, l * 0.9f + 0.5f); // slow down into the slot
    }

    static float Approach(float v, float target, float step) => v < target ? MathF.Min(target, v + step) : MathF.Max(target, v - step);
    float Mud => Weather.MudFactor;
    static float TurnRate(Unit u) => u.Horse!.TurnRate * (1.25f - 0.6f * MathF.Min(1, MathF.Abs(u.Speed) / u.Horse.Gallop));

    /// AI horses: turn toward Wish at a limited rate, slow down for sharp turns, accelerate and brake with inertia.
    void Steer(Unit u, float dt)
    {
        var h = u.Horse!;
        float ws = u.Action == UnitAction.Stagger ? 0 : u.Wish.Length(), rate = TurnRate(u);
        if (ws > 0.05f)
        {
            float diff = MathHelper.WrapAngle(MathF.Atan2(u.Wish.Y, u.Wish.X) - u.Heading);
            u.Heading += Math.Clamp(diff, -rate * dt, rate * dt);
            ws *= MathF.Max(0.2f, MathF.Cos(MathF.Min(MathF.Abs(diff), MathHelper.PiOver2)));
        }
        else if (MathF.Abs(u.Speed) < 1.5f)
            u.Heading += Math.Clamp(MathHelper.WrapAngle(u.Facing - u.Heading), -rate * 0.6f * dt, rate * 0.6f * dt); // turn on the spot
        u.Speed = Approach(u.Speed, ws * Mud, (ws > u.Speed ? h.Accel : h.Accel * 1.8f) * dt);
        if (u.Action == UnitAction.None && (ws > 0.05f || u.Speed > 1.5f)) u.Facing = u.Heading;
    }

    /// An AI commander for one side (always the enemy; your side too when Options.AutoPlayer): advance as a block,
    /// charge when close, archers stop at bow range, horse waits wide on the far flank and then hunts the softest companies.
    /// The companies a side's AI general leads: all of the enemy's; of yours, only allies' (or all, in headless runs).
    List<Squad> Led(int team, string role) => Role(team, role).Where(s => team == 1 || Options.AutoPlayer || s.AiControlled).ToList();
    public IEnumerable<Squad> Commanded => SquadsOf(0).Where(s => !s.AiControlled);

    void General(int team)
    {
        if ((team == 1 || Options.AutoPlayer) && SoundRetreat(team)) return;
        if (Wall != null) { if (team == 1) SiegeDefend(); else SiegeAttack(); return; }
        int foe = 1 - team;
        var inf = Led(team, "infantry").Where(s => s.Alive > 0).ToList();
        // Keep the smallest of three or more infantry companies back as a reserve, until the line needs it.
        if (inf.Count >= 3 && reserveSquad[team] == null && !charged[team]) reserveSquad[team] = inf.OrderBy(s => s.Alive).First();
        var reserve = reserveSquad[team];
        if (reserve != null)
        {
            bool needed = reserve.Alive == 0 || (charged[team] && (Time - chargedAt[team] > 18 || inf.Any(s => s != reserve && s.Alive < s.StartCount * 0.5f)));
            if (needed)
            {
                reserveSquad[team] = null;
                if (reserve.Alive > 0) { reserve.Order = Order.Charge; Log(team == 1 ? "The enemy commits his reserve!" : "Your reserve goes in!"); }
            }
            else inf.Remove(reserve);
        }
        var front = inf.Count > 0 ? inf.Aggregate(Vector2.Zero, (a, s) => a + s.Centroid()) / inf.Count : team == 1 ? eGuard.Centroid() : centroid[team];
        var dir = centroid[foe] - front;
        if (dir.LengthSquared() < 1) return;
        dir.Normalize();
        var side = new Vector2(-dir.Y, dir.X);
        var spears = Led(team, "spears");
        if (reserveSquad[team] is Squad r && r.Alive > 0) { r.Order = Order.Hold; r.Front = dir; r.Anchor = front - dir * 14; }
        if (!charged[team])
        {
            foreach (var s in inf) { s.Front = dir; s.Anchor = front + dir * 6 + side * lateral.GetValueOrDefault(s); }
            // With several companies, the outermost swings out to take the enemy line in the flank.
            if (inf.Count >= 3) { var wing = inf.OrderBy(s => lateral.GetValueOrDefault(s)).Last(); wing.Anchor = front + dir * 2 + side * (lateral.GetValueOrDefault(wing) + 14); }
            foreach (var s in spears) { s.Front = dir; s.Anchor = front + dir * 6 + side * (16 + lateral.GetValueOrDefault(s)); } // swing wide on a flank
            if (inf.Any(s => GapTo(s, foe) < 16) || spears.Any(s => s.Alive > 0 && GapTo(s, foe) < 10))
            {
                charged[team] = true;
                chargedAt[team] = Time;
                foreach (var s in inf.Concat(spears)) s.Order = Order.Charge;
                Log(team == 1 ? "The enemy line charges!" : "Your line charges!");
                Sound(Cue.Shout, front);
            }
        }
        else foreach (var s in inf.Concat(spears)) s.Order = Order.Charge; // late arrivals join the charge
        foreach (var s in Led(team, "archers"))
        {
            if (s.Order == Order.Charge || s.Alive == 0) continue;
            if (ArcherSupply(s, team)) continue; // out of arrows: to the wagon
            s.Front = dir;
            float gap = GapTo(s, foe), range = s.Units[0].Type.Range;
            if (gap > range - 4) s.Anchor = s.Centroid() + dir * 4;
            s.Target = SoftTarget(s, foe); // shoot the least armoured company in reach
            if (gap < 7 || (s.Units.All(u => !u.Active || u.Ammo == 0) && WagonStock[team] <= 0)) { s.Order = Order.Charge; s.Target = null; }
        }
        var horse = Led(team, "horse");
        if (!horseCharged[team])
        {
            foreach (var s in horse) { s.Front = dir; s.Anchor = front + dir * 4 - side * (26 - lateral.GetValueOrDefault(s)); } // the other flank
            if (charged[team] || Time > 35 || horse.Any(s => s.Alive > 0 && GapTo(s, foe) < 20))
            {
                horseCharged[team] = true;
                foreach (var s in horse) s.Order = Order.Charge;
                if (horse.Count > 0) Log(team == 1 ? "Enemy horse on the flank!" : "Your horse sweeps the flank!");
            }
        }
        else foreach (var s in horse) s.Order = Order.Charge;
        if (horseCharged[team])
            foreach (var s in horse.Where(s => s.Target == null))
                s.Target = new[] { "archers", "spears", "infantry", "horse" }.SelectMany(r => Role(foe, r)).FirstOrDefault(t => t.Alive > 0); // soft targets first
        if (team == 1)
        {
            eGuard.Front = dir;
            eGuard.Anchor = front - dir * 4; // captain leads from just behind the line
            if (charged[1] && Time - chargedAt[1] > 12 && eGuard.Order != Order.Charge) { eGuard.Order = Order.Charge; Log("The enemy captain joins the fight!"); }
        }
    }
    readonly Squad?[] reserveSquad = new Squad?[2];
    readonly float[] chargedAt = new float[2];
}
