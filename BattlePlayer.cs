using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the player's own unit (input to movement and actions, mounting, rallying).
public partial class Battle
{
    /// While you command, the captain keeps his own guard (docs/archive/PLAYTEST.md PT4): he turns to the nearest foe winding up a
    /// blow at him and blocks on the right side. He never parries and never strikes back, so a captain left in a melee
    /// still goes down; it only buys the time to look at the field. Read from the sim alone, so replays agree.
    public bool SelfGuard { get; private set; }
    AttackDir guardDir;
    /// The side the player's block covers: the mouse/key input (locked on press by controls), or the guard's own read while you command.
    AttackDir BlockDir => SelfGuard ? guardDir : Input.Dir;
    const float GuardReach = 3.5f;

    void Guard(Unit p, ref PlayerInput inp)
    {
        Unit? foe = null;
        float best = GuardReach * GuardReach;
        foreach (var u in Units)
        {
            if (!u.Active || u.Team == p.Team || u.Target != p || u.Action != UnitAction.Windup) continue;
            float d = Vector2.DistanceSquared(u.Pos, p.Pos);
            if (d < best) { best = d; foe = u; }
        }
        if (foe == null) return;
        SelfGuard = true;
        guardDir = Dirs.Mirror(foe.Swing);
        p.Face(foe.Pos - p.Pos);
        inp.Block = true;
    }

    // CR1: 150ms deterministic input buffer for Attack, Block, Dodge and Bash during busy frames.
    const float InputBufferWindow = 0.15f;
    enum BufferedAct { None, Attack, Block, Dodge, Bash }
    BufferedAct bufAct;
    float bufUntil = -1;
    AttackDir bufDir;
    Vector2 bufMove;
    bool prevAttackDown, prevBlockDown;

    void BufferInput(PlayerInput inp, bool busy)
    {
        bool atkPress = inp.Attack && !prevAttackDown;
        bool blkPress = inp.Block && !prevBlockDown;
        prevAttackDown = inp.Attack;
        prevBlockDown = inp.Block;
        if (!busy) return;
        if (inp.Dodge) { bufAct = BufferedAct.Dodge; bufUntil = Time + InputBufferWindow; bufMove = inp.Move; }
        else if (inp.Bash) { bufAct = BufferedAct.Bash; bufUntil = Time + InputBufferWindow; }
        else if (blkPress) { bufAct = BufferedAct.Block; bufUntil = Time + InputBufferWindow; bufDir = inp.Dir; }
        else if (atkPress) { bufAct = BufferedAct.Attack; bufUntil = Time + InputBufferWindow; bufDir = inp.Dir; }
    }

    void UpdatePlayer(Unit p, float dt)
    {
        var inp = Madness(p, Input, dt);
        SelfGuard = false;
        if (Commanding && !Mad) { inp = default; inp.Dir = Input.Dir; p.Charging = false; p.Charge = 0; bufAct = BufferedAct.None; Guard(p, ref inp); }
        else p.Facing = inp.Facing;

        UpdateAura(p, inp.Aura, dt);
        UpdateClash(p); // a clash of champions (MA7)
        UpdateForms(p, inp);
        if (Manned != null && (Commanding || p.Mounted)) Man(null);
        if (inp.Mount)
        {
            if (Manned != null) Man(null);
            else if (!p.Mounted && !Commanding && EngineNear(p) is Engine eng) Man(eng);
            else ToggleMount(p);
        }
        if (Manned != null) { inp.Attack = false; p.Charging = false; p.Charge = 0; } // the attack key looses the engine, not a blow
        if (inp.Swap) SwapWeapon(p);

        bool busy = p.Action is UnitAction.Windup or UnitAction.Active or UnitAction.Recover or UnitAction.Stagger;
        BufferInput(inp, busy);

        // CR2: Feint (RMB or Q during first 60% of Windup) & Morph (direction change during first 45% of Windup).
        if (p.Action == UnitAction.Windup && p.WindupDuration > 0.02f)
        {
            float prog = 1f - p.ActionTimer / p.WindupDuration;
            if ((inp.Block || inp.Feint) && prog <= 0.60f)
            {
                p.Action = inp.Block ? UnitAction.Block : UnitAction.None;
                p.ActionTimer = 0;
                p.BlockStart = Time;
                p.GuardDir = inp.Dir;
                p.Stamina = MathF.Max(0, p.Stamina - 6f);
                PlayerHit = ("FEINT", Time);
                busy = false;
            }
            else if (!p.Morphed && !p.Type.ThrustOnly && prog <= 0.45f && inp.Dir != p.Swing && p.Stamina >= 5f)
            {
                p.Swing = inp.Dir;
                p.Morphed = true;
                p.ActionTimer += 0.07f;
                p.WindupDuration = MathF.Max(p.WindupDuration, p.ActionTimer);
                p.Stamina -= 5f;
                PlayerHit = ("MORPH", Time);
            }
        }

        // CR2: Sidestep / Dodge on foot (Space, 0.22s directional burst, 22 stamina, 0.45s cd, real spatial evasion).
        if (inp.Dodge && !p.Mounted && p.DodgeCd <= 0 && p.Action != UnitAction.Stagger && p.Stamina >= 22f)
        {
            p.DodgeDir = inp.Move.LengthSquared() > 0.04f ? Vector2.Normalize(inp.Move) : -p.Dir;
            p.DodgeTimer = 0.22f;
            p.DodgeCd = 0.45f;
            p.Stamina -= 22f;
            p.Charging = false;
            p.Charge = 0;
            if (p.Action is UnitAction.Windup or UnitAction.Block) { p.Action = UnitAction.None; p.ActionTimer = 0; busy = false; }
        }

        // CR2: Fluid opposite-side combo cancel during early Recover (skips half of Recover into next swing).
        if (p.Action == UnitAction.Recover && p.Type.Ammo == 0 && !p.Couched && p.ActionTimer <= p.Type.Recover * 0.55f
            && (inp.Attack || (bufAct == BufferedAct.Attack && Time <= bufUntil)))
        {
            var nextDir = bufAct == BufferedAct.Attack && Time <= bufUntil ? bufDir : inp.Dir;
            if (nextDir != p.LastSwing || p.Type.ThrustOnly)
            {
                bufAct = BufferedAct.None;
                p.Swing = p.Type.ThrustOnly ? AttackDir.Thrust : nextDir;
                p.Charge = 0.15f;
                ReleaseSwing(p, combo: true);
                busy = true;
            }
        }

        if (!busy && bufAct != BufferedAct.None && Time <= bufUntil)
        {
            switch (bufAct)
            {
                case BufferedAct.Bash: inp.Bash = true; break;
                case BufferedAct.Dodge: inp.Dodge = true; if (inp.Move == Vector2.Zero) inp.Move = bufMove; break;
                case BufferedAct.Block: inp.Block = true; inp.Dir = bufDir; break;
                case BufferedAct.Attack: inp.Attack = true; inp.Dir = bufDir; break;
            }
            bufAct = BufferedAct.None;
        }
        else if (Time > bufUntil) bufAct = BufferedAct.None;

        if (inp.Bash) Bash(p);
        busy = p.Action is UnitAction.Windup or UnitAction.Active or UnitAction.Recover or UnitAction.Stagger;
        if (!busy)
        {
            if (inp.Block && p.Charging) { p.Charging = false; p.Charge = 0; } // feint: cancel the swing into a block
            if (inp.Block && p.Action != UnitAction.Block) p.BlockStart = Time;
            if (inp.Block) { p.Action = UnitAction.Block; p.GuardDir = SelfGuard ? guardDir : inp.Dir; }
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
            Reined += inp.Steer * rate * dt; // the camera turns with the reins (BattleScreen.Controls)
            // The horse goes where its rider looks: he can turn in the saddle to MountTwist either side, and past that the
            // horse is brought round; he cannot face further back than MountReach (he is not spun round on its back).
            float look = MathHelper.WrapAngle(p.Facing - p.Heading), over = MathF.Abs(look) - MountTwist;
            if (over > 0) p.Heading += MathF.Sign(look) * MathF.Min(over, rate * 1.5f * dt);
            look = MathHelper.WrapAngle(p.Facing - p.Heading);
            if (MathF.Abs(look) > MountReach) p.Facing = p.Heading + MathF.Sign(look) * MountReach;
            float target = inp.Throttle > 0 ? (inp.Sprint ? h.Gallop : h.Trot) : inp.Throttle < 0 ? -1.8f : 0;
            if (p.Action == UnitAction.Stagger) target = 0;
            if (Time < p.BoggedUntil) target = Math.Clamp(target, -1.8f, h.Trot * 0.4f); // caught in a company's press
            bool speedingUp = MathF.Abs(target) > MathF.Abs(p.Speed) && target * p.Speed >= 0;
            // Releasing W coasts smoothly instead of slamming the brakes; holding S actively reins back.
            float rateAccel = speedingUp ? h.Accel : inp.Throttle < 0 ? h.Accel * 2.2f : h.Accel * 0.38f;
            p.Speed = Approach(p.Speed, target, rateAccel * dt);
            p.Wish = p.HeadingDir * p.Speed;
            if (inp.Couch && !p.Couched && CanCouch(p) && p.Speed > 6 && p.Cooldown <= 0 && p.Action == UnitAction.None && !p.Charging)
            {
                p.Couched = true;
                p.Action = UnitAction.None;
            }
            else if (inp.Couch && p.Couched) p.Couched = false;
            if (p.Couched && (p.Speed < 4.5f || !CanCouch(p))) p.Couched = false;
            p.Stamina = Math.Clamp(p.Stamina + (p.Action == UnitAction.Block ? 4 : 15) * AuraBreath * dt, 0, 100);
        }
        else
        {
            var fDir = p.Dir;
            var rDir = new Vector2(-fDir.Y, fDir.X);
            float moveFwd = Vector2.Dot(inp.Move, fDir);
            float moveLat = Vector2.Dot(inp.Move, rDir);
            bool hasMove = inp.Move.LengthSquared() > 1e-4f;
            // Only sprint when moving predominantly forward (no backward sprinting).
            bool sprint = inp.Sprint && hasMove && moveFwd > 0.35f && p.Stamina > 5 && !p.Charging && p.Action == UnitAction.None;
            float dirScale = !hasMove ? 1f
                : moveFwd < -0.15f ? MathHelper.Lerp(0.84f, 0.68f, Math.Clamp(-moveFwd, 0f, 1f))
                : MathHelper.Lerp(0.86f, 1f, Math.Clamp(moveFwd, 0f, 1f));
            // CR2: responsive combat footwork (no 0.3x mud-glue crawl during swings).
            float speed = p.Type.Speed * dirScale * (sprint ? 1.6f : 1f)
                * (p.Action == UnitAction.Stagger ? 0f
                    : p.Action is UnitAction.Windup or UnitAction.Active ? 0.78f
                    : p.Action == UnitAction.Recover ? 0.72f
                    : p.Charging || p.Action == UnitAction.Block ? 0.68f : 1f);
            var targetWish = inp.Move * speed;
            if (p.DodgeTimer > 0 || p.Action == UnitAction.Stagger)
                p.Wish = targetWish;
            else
            {
                // Crisp foot acceleration & braking so starts, stops and direction changes carry weight without feeling sluggish.
                var diff = targetWish - p.Wish;
                float dist = diff.Length();
                float accel = hasMove ? (sprint ? 16f : 19f) : 24f;
                float step = accel * dt;
                p.Wish = dist <= step ? targetWish : p.Wish + diff * (step / dist);
            }
            // Breath comes back between blows, not while swinging (P2): a captain who never pauses runs dry.
            p.Stamina = Math.Clamp(p.Stamina + (sprint ? -20 : (p.Action == UnitAction.Block ? 5 : busy || p.Charging ? 0 : 16) * AuraBreath) * dt, 0, 100);
        }
        if (inp.Rally && RallyCooldown <= 0 && p.Stamina >= 30) Rally(p);
    }

    /// Only lances and polearms can be couched from horseback.
    public static bool CanCouch(Unit u) => u.Mounted && Look.GripOf(u.Type.Look.Weapon) is Grip.Lance or Grip.Pole;

    /// How far a rider turns in the saddle before his horse turns with him, and the furthest he can face from its head (radians).
    public const float MountTwist = 0.85f, MountReach = 1.65f;

    /// Heading the player's reins turned the horse since the screen last read it: the battle camera turns with it.
    public float Reined;

    /// Dismount (when the horse is nearly still), or mount the nearest loose horse within reach.
    void ToggleMount(Unit p)
    {
        if (p.Action is UnitAction.Windup or UnitAction.Stagger) return;
        if (p.Mounted)
        {
            if (MathF.Abs(p.Speed) > 3.2f) { Log("Slow down to dismount."); return; }
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
