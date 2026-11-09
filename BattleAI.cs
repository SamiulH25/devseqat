using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: unit AI (Think, ThinkCav for cavalry, steering helpers) and the AI general that gives squads orders.
public partial class Battle
{
    /// Where a man closes on his foe: straight at him, or for a pack beast in front of its foe, wide round to his flank
    /// first so it bites where he is not looking (the flank and rear bonus in Hit does the rest). It gives up circling
    /// after a few seconds (CavTimer, unused on foot, keeps count), so a man who keeps turning is still bitten.
    Vector2 PackApproach(Unit u, Unit enemy, float d, float dt, out bool circling)
    {
        circling = false;
        if (!u.Type.Has("pack") || enemy.Routing || d > 16) { u.CavTimer = 0; return enemy.Pos; }
        var dir = (u.Pos - enemy.Pos) / MathF.Max(0.01f, d);
        if (Vector2.Dot(dir, enemy.Dir) < 0.35f || u.CavTimer > 5) return enemy.Pos;
        circling = true;
        u.CavTimer += dt;
        var side = new Vector2(-enemy.Dir.Y, enemy.Dir.X);
        float s = Vector2.Dot(dir, side) >= 0 ? 1 : -1;
        return enemy.Pos + side * s * 4.5f - enemy.Dir * 2.5f; // wide, out of reach, round the end of a line
    }

    /// Height carries arrows further: up to a quarter more range, reached 6 m above the mark.
    internal static float HeightRange(float above) => 1 + Math.Clamp(above / 24, 0, 0.25f);

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
        if (s.Pursue && s.Order == Order.Charge && NearestRouter(u, d) is (Unit r, float rd)) (enemy, d) = (r, rd); // run down the broken
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
        float range = MathF.Min(Weather.Vision, u.Type.Range * (enemy != null ? HeightRange(Terrain.Height(u.Pos) - Terrain.Height(enemy.Pos)) : 1)); // fog and night hide targets
        if (enemy != null && u.Ammo > 0 && s.FireAtWill && d > 4 && d < range && (charge || toSlot < 2.5f) && Visible(enemy, u.Team)) // only at what your side can see
        {
            u.Face(enemy.Pos - u.Pos);
            if (u.Cooldown <= 0) Shoot(u, enemy);
            return;
        }

        float reach = enemy == null ? 0 : ReachOf(u) + (enemy.Mounted ? 0.9f : 0);
        // Brace for the charge: hold the slot and present the point instead of stepping out to meet it.
        if (enemy is { Mounted: true } && CanBrace(u) && d < 16 && toSlot < 6)
        {
            u.Face(enemy.Pos - u.Pos);
            if (d <= reach && u.Cooldown <= 0 && enemy.Speed < 4) StartSwing(u, enemy);
            return;
        }

        // A company in the field fights from its ranks (BattleRanks.cs); leaders, heroes, pursuers, mobs and a charge with no
        // company left to hit (it goes for the captain) fight free.
        if (Ranked(s) && !(charge && s.Foe == null) && !u.IsLeader && !u.Hero && !(s.Pursue && charge)) { FightFromRanks(u, s, enemy, d, dt); return; }

        bool leashed = !charge && toSlot > 14;
        if (enemy != null && !leashed && (charge || d < 6))
        {
            var to = Via(u, PackApproach(u, enemy, d, dt, out bool circling)) - u.Pos;
            u.Face(to);
            if (d <= reach && !circling) { if (u.Cooldown <= 0) StartSwing(u, enemy); }
            else u.Wish = to / MathF.Max(0.01f, to.Length()) * u.Type.Speed;
            return;
        }

        if (toSlot > 0.4f)
        {
            var to = Via(u, u.Slot) - u.Pos;
            u.Wish = to / MathF.Max(0.01f, to.Length()) * MathF.Min(u.Type.Speed, s.MarchSpeed) * MathF.Min(1, toSlot);
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
    List<Squad> Led(int team, string role) => Role(team, role).Where(s => s.Division == null && (team == 1 || Options.AutoPlayer || s.AiControlled)).ToList();
    /// Your companies that take your orders at once: not an ally's, not in a division.
    public IEnumerable<Squad> Commanded => SquadsOf(0).Where(s => !s.AiControlled && s.Division == null);

    void General(int team)
    {
        if (Options.Duel) { eGuard.Order = Order.Charge; return; } // single combat: he comes for you, nothing else moves
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
        // The line's centre: its infantry, else the captain's guard while he lives, else the whole side.
        var front = inf.Count > 0 ? inf.Aggregate(Vector2.Zero, (a, s) => a + s.Centroid()) / inf.Count : team == 1 && eGuard.Alive > 0 ? eGuard.Centroid() : centroid[team];
        var dir = centroid[foe] - front;
        if (dir.LengthSquared() < 1) return;
        dir.Normalize();
        var side = new Vector2(-dir.Y, dir.X);
        var spears = Led(team, "spears");
        // On better ground than the enemy, the line stays on it and lets him come (BattleGround.cs).
        var hold = HoldsGround(team, front, foe);
        var basis = hold ?? front;
        float step = hold != null ? 0 : 6;
        if (reserveSquad[team] is Squad r && r.Alive > 0) { r.Order = Order.Hold; r.Front = dir; r.Anchor = front - dir * 14; }
        if (!charged[team])
        {
            foreach (var s in inf) { s.Front = dir; s.Anchor = DryAnchor(basis + dir * (step - depth.GetValueOrDefault(s)) + side * lateral.GetValueOrDefault(s)); }
            // With several companies, the outermost swings out to take the enemy line in the flank.
            if (inf.Count >= 3) { var wing = inf.OrderBy(s => lateral.GetValueOrDefault(s)).Last(); wing.Anchor = DryAnchor(basis + dir * (step - 4 - depth.GetValueOrDefault(wing)) + side * (lateral.GetValueOrDefault(wing) + 14)); }
            foreach (var s in spears) { s.Front = dir; s.Anchor = DryAnchor(basis + dir * (step - depth.GetValueOrDefault(s)) + side * (16 + flankOut[team].Spears + lateral.GetValueOrDefault(s))); } // swing wide on a flank
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
        foreach (var s in Led(team, "casters").Where(s => s.Alive > 0)) { s.Front = dir; s.Anchor = front - dir * 15; s.Order = Order.Hold; } // a necromancer works from behind his dead
        // With no one left to hold a line, archers shoot what they carry and then go in: no trips back to the wagon.
        bool bowsAlone = !Units.Any(u => u.Team == team && u.Active && !u.Routing && u.Type.Ammo == 0);
        bool seesFoe = Squads.Any(o => o.Team != team && o.Alive > 0 && Visible(o, team));
        foreach (var s in Led(team, "archers"))
        {
            if (s.Order == Order.Charge || s.Alive == 0) continue;
            if (!bowsAlone && ArcherSupply(s, team)) continue; // out of arrows: to the wagon
            s.Front = dir;
            float gap = GapTo(s, foe), range = s.Units[0].Type.Range;
            if (gap > range - 4 || !seesFoe) s.Anchor = s.Centroid() + dir * 4; // close to bow range, or until they can see someone to shoot
            else if (!charged[team] && Role(foe, "horse").Any(h => h.Alive > 0) && CannotStake(s) == null) PlantStakes(s, record: false); // in range, horse about: stakes
            s.Target = SoftTarget(s, foe); // shoot the least armoured company in reach
            if (gap < 7 || (s.Units.Count(u => u.Active && u.Ammo > 0) * 4 <= s.Alive && (WagonStock[team] <= 0 || bowsAlone))) { s.Order = Order.Charge; s.Target = null; } // or three in four out of arrows
        }
        var horse = Led(team, "horse");
        if (!horseCharged[team])
        {
            var (sign, outward) = HorseFlank(team, front, dir, side); // the other flank, or round the outside of a wood
            foreach (var s in horse) { s.Front = dir; s.Anchor = DryAnchor(basis + dir * (step - 2 - depth.GetValueOrDefault(s)) + side * (sign * (26 + outward + flankOut[team].Horse) + lateral.GetValueOrDefault(s))); }
            if (charged[team] || (Time > 35 && hold == null) || horse.Any(s => s.Alive > 0 && GapTo(s, foe) < 20))
            {
                horseCharged[team] = true;
                foreach (var s in horse) s.Order = Order.Charge;
                if (horse.Count > 0) Log(team == 1 ? "Enemy horse on the flank!" : "Your horse sweeps the flank!");
            }
        }
        else foreach (var s in horse) s.Order = Order.Charge;
        if (horseCharged[team])
            foreach (var s in horse.Where(s => s.Target == null))
                s.Target = new[] { "archers", "spears", "infantry", "horse" }.SelectMany(r => Role(foe, r)).Where(t => t.Alive > 0).OrderBy(t => Visible(t, team) ? 0 : 1).ThenBy(t => HorseGoing(s, t)).FirstOrDefault(); // ones it can see, on good going; soft targets first
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
