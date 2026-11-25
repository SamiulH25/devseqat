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
        if (u.Type.Flying) { ThinkFlyer(u, dt); return; } // a wyvern dives and climbs (BattleMonsters.cs)
        if (u.Action != UnitAction.None && u.Action != UnitAction.Block)
        {
            if (!u.Mounted) u.Wish = Vector2.Zero; // committed to a swing; a rider keeps riding
            return;
        }

        var s = u.Squad!;
        var (enemy, d) = Nearest(u);
        if (s.Pursue && s.Order == Order.Charge && NearestRouter(u, d) is (Unit r, float rd)) (enemy, d) = (r, rd); // run down the broken
        else (enemy, d) = Notice(u, enemy, d); // the captain draws the eye (BattleNotice.cs)
        u.EnemyDist = d;
        if (!u.Mounted && GivesGround(u)) return; // rabble who just saw him kill give ground
        if (HuntThink(u, s, ref enemy, ref d)) return; // sent for you, guarding their captain (BattleHunt.cs)
        u.EnemyDist = d;
        if (u.Ammo > 0 && !u.Mounted && LoosesAtCaptain(u, s)) return; // a volley marked on you
        if (u.Mounted) { ThinkCav(u, s, enemy, d, dt); return; }
        u.Wish = Vector2.Zero;

        float toSlot = Vector2.Distance(u.Pos, u.Slot);
        if (s.BreakOffUntil > Time) // relieved (BattleRelief.cs): back through the fresh ranks, not a blow more
        {
            if (toSlot > 0.4f) { var back = Via(u, u.Slot) - u.Pos; u.Wish = back / MathF.Max(0.01f, back.Length()) * u.Type.Speed; u.Face(back); }
            return;
        }
        bool free = s.HuntCaptain || u.Lunging || s.Reckless && s.Order == Order.Charge; // sent for the captain, lunging on his own, or a reckless charge: a run, not a line
        bool charge = s.Order == Order.Charge || free;
        if (charge && s.Target != null && d > 3 && !free) (enemy, d) = Nearest(u, s.Target); // go for the ordered target

        // Archers shoot once settled in formation (or anywhere when charging).
        if (s.Target != null && !charge && u.Ammo > 0)
        {
            var (tu, td) = Nearest(u, s.Target);
            if (tu != null && td < u.Type.Range) (enemy, d) = (tu, td);
        }
        float range = MathF.Min(Weather.Vision, u.Type.Range * (enemy != null ? HeightRange(Terrain.Height(u.Pos) - Terrain.Height(enemy.Pos)) : 1)); // fog and night hide targets
        if (enemy != null && u.Ammo > 0 && s.FireAtWill && d > 4 && d < range && (charge || toSlot < 2.5f) && Visible(enemy, u.Team) && !HoldsFire(s, enemy)) // only at what your side can see
        {
            u.Face(enemy.Pos - u.Pos);
            if (u.Cooldown <= 0) Shoot(u, enemy);
            return;
        }

        float reach = enemy == null ? 0 : ReachOf(u) + (enemy.Mounted ? 0.9f : 0);
        // Brace for the charge: hold the slot and present the point instead of stepping out to meet it (a charging company
        // only stops for a horse actually coming at it, not a standing one).
        if (enemy is { Mounted: true } && CanBrace(u) && d < 16 && toSlot < 6 && (s.Order != Order.Charge || enemy.Speed > 3))
        {
            u.Face(enemy.Pos - u.Pos);
            if (d <= reach && u.Cooldown <= 0 && enemy.Speed < 4) StartSwing(u, enemy);
            return;
        }

        // Engine crews stay tucked tightly at their siege engine (only swinging if an enemy reaches them directly).
        if (s.Works is { Wrecked: false })
        {
            if (enemy != null && d <= reach + 0.6f)
            {
                u.Face(enemy.Pos - u.Pos);
                if (d <= reach && u.Cooldown <= 0) StartSwing(u, enemy);
            }
            else if (toSlot > 0.35f)
            {
                var to = Via(u, u.Slot) - u.Pos;
                u.Wish = to / MathF.Max(0.01f, to.Length()) * u.Type.Speed;
                u.Face(to);
            }
            else u.Face(s.Front);
            return;
        }
        // A company in the field fights from its ranks (BattleRanks.cs); leaders, heroes, pursuers, mobs, broken ranks, a
        // charge with no company left to hit (it goes for the captain) and one swarming a beast or a handful fight free.
        if (Ranked(s) && !free && !s.Broken && !(charge && (s.Foe == null || Overwhelms(s))) && !u.IsLeader && !u.Hero && !(s.Pursue && charge)) { FightFromRanks(u, s, enemy, d, dt); return; }

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

        // Riders strike at whatever comes into reach as they pass (not with a couched lance), down either side of the horse:
        // only a foe behind its quarters is out of his arc.
        if (enemy != null && !u.Couched && u.Cooldown <= 0
            && d <= MathF.Min(u.Type.Reach, MountedMeleeReach) + (enemy.Mounted ? 1.0f : 0.7f) + MathF.Abs(u.Speed) * 0.08f // the horse's bulk keeps foes off
            && Vector2.Dot((enemy.Pos - u.Pos) / MathF.Max(d, 0.01f), hd) > -0.7f)
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
                // In reach: rein in, turn to him and hack, as a bogged charge does. Riding on into him only carried the horse
                // through and round with its back to the foe, so holding horse lost a standing fight to the poorest foot.
                if (d <= MountedMeleeReach + 0.7f)
                {
                    u.Wish = Vector2.Zero;
                    u.Facing = MathF.Atan2(enemy.Pos.Y - u.Pos.Y, enemy.Pos.X - u.Pos.X);
                    return;
                }
                // Counter-charge whatever comes close: horses standing still are easy meat for a charge.
                var aim = Via(u, enemy.Pos);
                var toE = aim - u.Pos;
                u.Wish = toE.LengthSquared() > 1e-4f ? Vector2.Normalize(toE) * (d > 5 ? h.Gallop : h.Trot) : Vector2.Zero;
                if (u.Type.Class == UnitClass.Cavalry && u.Speed > 6.5f && d > 4 && u.Cooldown <= 0) u.Couched = true;
                return;
            }
            Ride(u, Via(u, u.Slot), toSlot > 12 ? h.Gallop * 0.8f : h.Trot, u.Slot);
            return;
        }

        var (t, td) = s.Target != null ? Nearest(u, s.Target) : (enemy, d);
        if (t == null) { (t, td) = (enemy, d); }
        if (t == null) { Ride(u, u.Slot, h.Trot); return; }

        var targetCenter = t.Squad != null ? t.Squad.Center : t.Pos;
        if (s.Rallying && u.Cav != CavState.Withdraw && (td > 6 || u.Bogged > 0.4f) && Rallies(s) && s.Units.Count(x => x.Active && !x.Routing && x.Cav == CavState.Withdraw) * 2 >= s.Alive) { u.Cav = CavState.Withdraw; u.Contact = false; u.Bogged = 0; } // most of the company is rallying: stragglers rejoin it
        switch (u.Cav)
        {
            case CavState.Withdraw:
            {
                if (Rallies(s)) { RideToRally(u, s); return; } // veterans re-form as a body before they go again
                // Ride out to charge distance away from the target company, then wheel round and charge again.
                u.CavTimer -= dt;
                u.Couched = false;
                var fromTarget = u.Pos - targetCenter;
                Vector2 dir;
                if (fromTarget.LengthSquared() > 9f)
                {
                    var away = Vector2.Normalize(fromTarget);
                    dir = Vector2.Dot(hd, away) > -0.25f ? hd * 0.4f + away * 1.6f : away;
                }
                else dir = hd; // still inside the formation: push straight out instead of U-turning in place
                if (u.Pos.Length() > MapRadius - 25) dir -= Vector2.Normalize(u.Pos) * 3; // stay on the field
                dir = dir.LengthSquared() > 1e-4f ? Vector2.Normalize(dir) : hd;
                var outAt = Via(u, u.Pos + dir * 14);
                u.Wish = (outAt - u.Pos).LengthSquared() > 1e-4f ? Vector2.Normalize(outAt - u.Pos) * h.Gallop : dir * h.Gallop;
                float clearDist = enemy != null ? MathF.Min(d, td) : td;
                if (clearDist >= 18f || u.CavTimer <= 0) { u.Cav = CavState.Charge; u.Contact = false; u.Bogged = 0; }
                return;
            }
            case CavState.Melee:
            {
                // Brief disengage fallback: never stay bogged in a standing fight on Charge.
                u.CavTimer -= dt;
                u.Couched = false;
                if (u.CavTimer <= 0) { u.Cav = CavState.Withdraw; u.CavTimer = 7f; u.Contact = false; u.Bogged = 0; }
                else
                {
                    var aimM = Via(u, t.Pos) - u.Pos;
                    u.Wish = td > MountedMeleeReach && aimM.LengthSquared() > 1e-4f ? Vector2.Normalize(aimM) * h.Trot * 0.6f : Vector2.Zero;
                }
                return;
            }
            default:
            {
                // Charge: aim past the target, punch straight through once in contact, and pull out immediately if stalled.
                if (!u.Contact && ChargeLane(u, s, t) is Vector2 lane)
                {
                    // Round to its open side first: a charge lands on a flank, not on its points.
                    u.Couched = false;
                    Ride(u, Via(u, lane), h.Gallop, lane);
                    return;
                }
                Vector2 goal;
                if (u.Contact && td < 6f)
                {
                    // Commit to punching straight through along current heading instead of swerving at adjacent men.
                    var through = u.Pos + hd * 12;
                    if (through.Length() > MapRadius - 15) through = Vector2.Normalize(through) * (MapRadius - 15);
                    goal = Terrain.House(through, 1.0f) ? t.Pos : through;
                }
                else
                {
                    var to = (t.Pos - u.Pos) / MathF.Max(td, 0.01f);
                    var past = t.Pos + to * 6;
                    goal = Terrain.House(past, 1.0f) ? t.Pos : past;
                }
                var aimAt = Via(u, goal);
                u.Wish = (aimAt - u.Pos).LengthSquared() > 1e-4f ? Vector2.Normalize(aimAt - u.Pos) * h.Gallop : Vector2.Zero;
                if (u.Type.Class == UnitClass.Cavalry && u.Speed > 6.5f && td < 30 && u.Cooldown <= 0) u.Couched = true;
                if (td < 4) u.Contact = true;
                float targetR = t.Squad != null ? t.Squad.Radius : 2f;
                bool cleared = u.Contact && (
                    (td > 4.5f && Vector2.Dot(t.Pos - u.Pos, hd) < 0) ||
                    (Vector2.Distance(u.Pos, targetCenter) > targetR + 4f && Vector2.Dot(targetCenter - u.Pos, hd) < 0));
                if (cleared)
                {
                    u.Cav = CavState.Withdraw; // broke through: ride out to recharge distance
                    u.CavTimer = 7f;
                    u.Contact = false;
                    u.Bogged = 0;
                    return;
                }
                u.Bogged = u.Speed < 2.5f && (td < 4.5f || d < 3.5f) ? u.Bogged + dt : 0;
                if (u.Bogged > 1.0f)
                {
                    u.Cav = CavState.Withdraw; // stalled in the press: pull out immediately instead of sticking in melee
                    u.CavTimer = 7f;
                    u.Bogged = 0;
                    u.Contact = false;
                    u.Couched = false;
                }
                return;
            }
        }
    }

    /// Ride toward `to` (a waypoint on the way, or the end itself), easing down only as he nears `end` (default: `to`): a
    /// flow field's waypoint is a few metres ahead, and slowing for it held a horse at a walk the whole way.
    static void Ride(Unit u, Vector2 to, float speed, Vector2? end = null)
    {
        var d = to - u.Pos;
        float l = d.Length(), left = end is Vector2 e ? Vector2.Distance(u.Pos, e) : l;
        if (left < 1.2f || l < 0.01f) { u.Wish = Vector2.Zero; u.Face(u.Squad!.Front); return; }
        u.Wish = d / l * MathF.Min(speed, left * 0.9f + 0.5f); // slow down into the slot
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
            float turnMin = u.Speed > h.Trot && MathF.Abs(diff) > 1.1f ? h.Trot * 0.6f / ws : 0.2f;
            ws *= MathF.Max(turnMin, MathF.Cos(MathF.Min(MathF.Abs(diff), MathHelper.PiOver2)));
            // No slope here: Move slows him for the slope and the ground (GroundPace). Taking it here too charged a horse
            // twice for every rise, and a charge up a stony slope crawled at a walk under the arrows.
        }
        else if (MathF.Abs(u.Speed) < 1.5f)
            u.Heading += Math.Clamp(MathHelper.WrapAngle(u.Facing - u.Heading), -rate * 0.6f * dt, rate * 0.6f * dt); // turn on the spot
        if (Time < u.BoggedUntil) ws = MathF.Min(ws, u.Cav == CavState.Withdraw ? h.Trot * 0.85f : h.Trot * 0.4f); // caught in a company's press; withdrawing riders can still pull free
        u.Speed = Approach(u.Speed, ws * Mud, (ws > u.Speed ? h.Accel : h.Accel * 1.8f) * dt);
        if (u.Action == UnitAction.None && (ws > 0.05f || u.Speed > 1.5f)) u.Facing = u.Heading;
    }

    /// An AI commander for one side (always the enemy; your side too when Options.AutoPlayer): advance as a block,
    /// charge when close, archers stop at bow range, horse waits wide on the far flank and then hunts the softest companies.
    /// The companies a side's AI general leads: all of the enemy's; of yours, only allies' (or all, in headless runs).
    List<Squad> Led(int team, string role) => Role(team, role).Where(s => s.Division == null && (team == 1 || Options.AutoPlayer || s.AiControlled)).ToList();
    /// Your companies that take your orders at once: not an ally's, not in a division.
    public IEnumerable<Squad> Commanded => SquadsOf(0).Where(s => !s.AiControlled && s.Division == null && s.FoldedInto == null);

    void General(int team)
    {
        if (Options.Duel) { foreach (var s in SquadsOf(team)) s.Order = Order.Charge; return; } // duel: every fighter engages
        if ((team == 1 || Options.AutoPlayer) && SoundRetreat(team)) return;
        if (Wall != null) { if (team == 1) SiegeDefend(); else SiegeAttack(); return; }
        int foe = 1 - team;
        var inf = Led(team, "infantry").Where(s => s.Alive > 0).ToList();
        // Keep the smallest of three or more infantry companies back as a reserve, until the line needs it.
        if (inf.Count >= 3 && reserveSquad[team] == null && !charged[team]) reserveSquad[team] = inf.OrderBy(s => s.Alive).First();
        var reserve = reserveSquad[team];
        if (reserve != null)
        {
            bool needed = reserve.Alive == 0 || reserve.BrokeRanks || (charged[team] && (Time - chargedAt[team] > 18 * NatureOf(team).ReserveWait || inf.Any(s => s != reserve && s.Alive < s.StartCount * 0.5f)));
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
        if (reserveSquad[team] is Squad { BrokeRanks: false } r && r.Alive > 0) { r.Order = Order.Hold; r.Front = dir; r.Anchor = DryAnchor(front - dir * 14); }
        // The line advances company by company: each goes in when it is close to the enemy company opposite it
        // (docs/archive/MELEE.md D4); 20 s after the first charge the rest follow.
        foreach (var s in inf) if (s.Order != Order.Charge) { s.Front = dir; s.Anchor = DryAnchor(basis + dir * (step - depth.GetValueOrDefault(s)) + side * lateral.GetValueOrDefault(s)); }
        // With several companies, the outermost swings out to take the end of the enemy line in the flank.
        if (inf.Count >= 3 && inf.OrderBy(s => lateral.GetValueOrDefault(s)).Last() is { Order: not Order.Charge } wing) wing.Anchor = DryAnchor(basis + dir * (step - 4 - depth.GetValueOrDefault(wing)) + side * (lateral.GetValueOrDefault(wing) + 14));
        foreach (var s in spears) if (s.Order != Order.Charge) { s.Front = dir; s.Anchor = DryAnchor(basis + dir * (step - depth.GetValueOrDefault(s)) + side * (16 + flankOut[team].Spears + lateral.GetValueOrDefault(s))); } // swing wide on a flank
        bool late = charged[team] && Time - chargedAt[team] > 20 * NatureOf(team).ReserveWait;
        foreach (var s in inf.Concat(spears))
        {
            if (s.Order == Order.Charge || s.Alive == 0) continue;
            var opp = Opposite(s, foe, dir, side);
            float trigger = (spears.Contains(s) ? 10 : 16) + (s.Company?.Has("Hothead") == true ? 6 : 0) + NatureOf(team).Closes;
            float gap = opp == null ? float.MaxValue : Vector2.Distance(s.Center, opp.Center) - s.Radius - opp.Radius;
            if (!late && gap >= trigger && GapTo(s, foe) >= trigger) continue;
            s.Order = Order.Charge;
            s.Target = gap < trigger * 1.5f ? opp : null; // its opposite number, or whatever came close
            if (charged[team]) continue;
            charged[team] = true;
            chargedAt[team] = Time;
            Log(team == 1 ? "The enemy line charges!" : "Your line charges!");
            Sound(Cue.Shout, front);
        }
        foreach (var s in Led(team, "casters").Where(s => s.Alive > 0)) { s.Front = dir; s.Anchor = DryAnchor(front - dir * 15); s.Order = Order.Hold; } // a necromancer works from behind his dead
        // With no one left to hold a line, archers shoot what they carry and then go in: no trips back to the wagon.
        bool bowsAlone = !Units.Any(u => u.Team == team && u.Active && !u.Routing && u.Type.Ammo == 0);
        bool seesFoe = Squads.Any(o => o.Team != team && o.Alive > 0 && Visible(o, team));
        foreach (var s in Led(team, "archers"))
        {
            if (s.Order == Order.Charge || s.Alive == 0) continue;
            if (!bowsAlone && ArcherSupply(s, team)) continue; // out of arrows: to the wagon
            s.Front = dir;
            float gap = GapTo(s, foe), range = s.Units[0].Type.Range;
            // Bows that have loosed nothing for 20 s while nothing is at blows (back rank out of reach, or the foe behind a crest) close on him.
            if (Time - s.LoosedAt > 20 && (bowsAlone || !SquadsOf(team).Any(o => o.Alive > 0 && o.InContact))) gap = MathF.Max(gap, range);
            if (gap > range - 4 || !seesFoe) s.Anchor = DryAnchor(s.Centroid() + dir * 4); // close to bow range, or until they can see someone to shoot
            else if (!charged[team] && Role(foe, "horse").Any(h => h.Alive > 0) && CannotStake(s) == null) PlantStakes(s, record: false); // in range, horse about: stakes
            s.Target = SoftTarget(s, foe); // shoot the least armoured company in reach
            if (gap < 7 || (s.Units.Count(u => u.Active && u.Ammo > 0) * 4 <= s.Alive && (WagonStock[team] <= 0 || bowsAlone))) { s.Order = Order.Charge; s.Target = null; } // or three in four out of arrows
        }
        var horse = Led(team, "horse");
        if (!horseCharged[team])
        {
            var (sign, outward) = HorseFlank(team, front, dir, side); // the other flank, or round the outside of a wood
            foreach (var s in horse) { s.Front = dir; s.Anchor = DryAnchor(basis + dir * (step - 2 - depth.GetValueOrDefault(s)) + side * (sign * (26 + outward + flankOut[team].Horse) + lateral.GetValueOrDefault(s))); }
            if (charged[team] || (Time > 35 * NatureOf(team).ReserveWait && hold == null) || horse.Any(s => s.Alive > 0 && GapTo(s, foe) < 20))
            {
                horseCharged[team] = true;
                foreach (var s in horse) s.Order = Order.Charge;
                if (horse.Count > 0) Log(team == 1 ? "Enemy horse on the flank!" : "Your horse sweeps the flank!");
            }
        }
        else foreach (var s in horse) s.Order = Order.Charge;
        if (horseCharged[team])
            foreach (var s in horse.Where(s => s.Target == null))
                s.Target = EnginePrey(s, foe) ?? new[] { "archers", "spears", "infantry", "horse" }.SelectMany(r => Role(foe, r)).Where(t => t.Alive > 0).OrderBy(t => Visible(t, team) ? 0 : 1).ThenBy(t => HorseGoing(s, t)).FirstOrDefault(); // ones it can see, on good going; soft targets first
        if (team == 1)
        {
            eGuard.Front = dir;
            if (inf.Count > 0) eGuard.Anchor = DryAnchor(front - dir * 4); // captain leads from just behind the line (with no line, front is his own guard: he stays put)
            if (charged[1] && Time - chargedAt[1] > Nature.CaptainJoins && eGuard.Order != Order.Charge) { eGuard.Order = Order.Charge; Log(EnemyCaptain.Type.Wild ? $"The {EnemyCaptain.Type.Name.ToLowerInvariant()} joins the fight!" : "The enemy captain joins the fight!"); }
            HuntGeneral(); // he hunts you (BattleHunt.cs)
        }
    }
    /// The enemy company opposite a line company: nearest along the line (sideways counts more than depth), foot first.
    Squad? Opposite(Squad s, int foe, Vector2 dir, Vector2 side)
    {
        Squad? best = null;
        float bs = float.MaxValue;
        foreach (var e in Squads)
        {
            if (e.Team != foe || e.Alive == 0) continue;
            var d = e.Center - s.Center;
            float score = MathF.Abs(Vector2.Dot(d, side)) + 0.3f * MathF.Abs(Vector2.Dot(d, dir)) + (e.Cavalry ? 30 : 0);
            if (score < bs) { bs = score; best = e; }
        }
        return best;
    }

    readonly Squad?[] reserveSquad = new Squad?[2];
    readonly float[] chargedAt = new float[2];
}
