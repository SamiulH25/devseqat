using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the player's own unit (input to movement and actions, mounting, rallying).
public partial class Battle
{
    void UpdatePlayer(Unit p, float dt)
    {
        var inp = Madness(p, Input, dt);
        if (Commanding && !Mad) { inp = default; inp.Dir = Input.Dir; p.Charging = false; p.Charge = 0; }
        else p.Facing = inp.Facing;

        UpdateAura(p, inp.Aura, dt);
        UpdateClash(p); // a clash of champions (MA7)
        UpdateForms(p, inp);
        if (inp.Mount) ToggleMount(p);
        if (inp.Swap) SwapWeapon(p);
        if (inp.Bash) Bash(p);

        bool busy = p.Action is UnitAction.Windup or UnitAction.Recover or UnitAction.Stagger;
        if (!busy)
        {
            if (inp.Block && p.Charging) { p.Charging = false; p.Charge = 0; } // feint: cancel the swing into a block
            if (inp.Block && p.Action != UnitAction.Block) p.BlockStart = Time;
            if (inp.Block) p.Action = UnitAction.Block;
            else if (p.Action == UnitAction.Block) p.Action = UnitAction.None;
            if (p.Action == UnitAction.None && !p.Couched && p.Type.Ammo > 0) PlayerBow(p, inp, dt);
            else if (p.Action == UnitAction.None && !p.Couched)
            {
                if (inp.Attack)
                {
                    if (!p.Charging) p.Swing = p.Type.ThrustOnly ? AttackDir.Thrust : inp.Dir; // direction locks in when the swing starts
                    p.Charging = true;
                    p.Charge = MathF.Min(1, p.Charge + dt / 0.7f);
                }
                else if (p.Charging)
                {
                    // Release: power scales with how long the swing was held.
                    p.Charging = false;
                    ReleaseSwing(p);
                }
            }
        }

        if (p.Mounted)
        {
            var h = p.Horse!;
            float rate = TurnRate(p) * 1.2f * (1 + 0.25f * RidingEdge); // Riding (P3): a surer hand on the reins
            p.Heading += inp.Steer * rate * dt;
            float target = inp.Throttle > 0 ? (inp.Sprint ? h.Gallop : h.Trot) : inp.Throttle < 0 ? -1.8f : 0;
            if (p.Action == UnitAction.Stagger) target = 0;
            bool speedingUp = MathF.Abs(target) > MathF.Abs(p.Speed) && target * p.Speed >= 0;
            p.Speed = Approach(p.Speed, target, (speedingUp ? h.Accel : h.Accel * 1.8f) * dt);
            p.Wish = p.HeadingDir * p.Speed;
            if (inp.Couch && !p.Couched && p.Speed > 6 && p.Cooldown <= 0 && p.Action == UnitAction.None && !p.Charging)
            {
                p.Couched = true;
                p.Action = UnitAction.None;
            }
            else if (inp.Couch && p.Couched) p.Couched = false;
            if (p.Couched && p.Speed < 4.5f) p.Couched = false;
            p.Stamina = Math.Clamp(p.Stamina + (p.Action == UnitAction.Block ? 4 : 15) * AuraBreath * dt, 0, 100);
        }
        else
        {
            bool sprint = inp.Sprint && inp.Move != Vector2.Zero && p.Stamina > 5 && !p.Charging && p.Action == UnitAction.None;
            float speed = p.Type.Speed * (sprint ? 1.6f : 1)
                * (p.Charging || p.Action == UnitAction.Block ? 0.5f : 1)
                * (p.Action == UnitAction.Stagger ? 0 : busy ? 0.3f : 1);
            p.Wish = inp.Move * speed;
            // Breath comes back between blows, not while swinging (P2): a captain who never pauses runs dry.
            p.Stamina = Math.Clamp(p.Stamina + (sprint ? -20 : (p.Action == UnitAction.Block ? 4 : busy || p.Charging ? 0 : 15) * AuraBreath) * dt, 0, 100);
        }
        if (inp.Rally && RallyCooldown <= 0 && p.Stamina >= 30) Rally(p);
    }

    /// Dismount (when the horse is nearly still), or mount the nearest loose horse within reach.
    void ToggleMount(Unit p)
    {
        if (p.Action is UnitAction.Windup or UnitAction.Stagger) return;
        if (p.Mounted)
        {
            if (MathF.Abs(p.Speed) > 1.5f) { Log("Slow down to dismount."); return; }
            var side = new Vector2(-p.HeadingDir.Y, p.HeadingDir.X);
            LooseHorses.Add(new LooseHorse { Mount = p.Horse!, Hp = p.HorseHp, Pos = p.Pos, Heading = p.Heading, Team = p.Team, Coat = p.Coat });
            p.Pos -= side * 1.1f;
            p.Horse = null;
            p.Speed = 0;
            p.Couched = false;
            Log("You dismount.");
            return;
        }
        LooseHorse? best = null;
        float bd = 2.8f * 2.8f;
        foreach (var lh in LooseHorses)
        {
            float d = Vector2.DistanceSquared(lh.Pos, p.Pos);
            if (d < bd) { bd = d; best = lh; }
        }
        if (best == null) { if (!CallHorse(p)) Log("No horse within reach, and none to call."); return; }
        LooseHorses.Remove(best);
        p.Horse = best.Mount;
        p.HorseHp = best.Hp;
        p.Coat = best.Coat;
        p.Heading = best.Heading;
        p.Pos = best.Pos;
        p.Speed = 0;
        p.Charging = false;
        p.Action = UnitAction.None;
        Log(best.Team == p.Team ? "You mount up." : "You seize an enemy horse!");
    }

    void Rally(Unit p)
    {
        p.Stamina -= 30;
        RallyCooldown = Options.RallyCooldown;
        foreach (var u in Units)
            if (u.Active && u.Team == p.Team && !u.IsPlayer && Vector2.DistanceSquared(u.Pos, p.Pos) < RallyRadius * RallyRadius)
            {
                u.Morale = MathF.Min(100, u.Morale + (u.Routing ? Options.RallyBonus + 20 : Options.RallyBonus));
                if (u.Routing) u.Routs = Math.Min(u.Routs, 1); // a general's horn gives even a broken company one more chance
            }
        Log("Rally! Hold the line!");
        Sound(Cue.Horn, p.Pos);
        if (Options.KingsCharge && !kingsCharged) KingsCharge(p);
    }

    bool kingsCharged;

    /// The King's Charge (P5): once a battle every man of yours, broken or not, wherever he is, takes heart.
    void KingsCharge(Unit p)
    {
        kingsCharged = true;
        foreach (var u in Units)
            if (u.Active && u.Team == p.Team && !u.IsPlayer)
            {
                u.Morale = MathF.Min(100, MathF.Max(u.Morale, RallyAt + 20) + 10);
                if (u.Routing) u.Routs = 0;
            }
        Log("The King's Charge! Every banner turns back to the fight!");
    }
}
