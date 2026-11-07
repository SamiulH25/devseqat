using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the damage path (TickAction -> Strike -> Hit -> Kill), horses, arrows, braced spears and couched lances.
public partial class Battle
{
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

    /// A rider's sword arm reaches about as far as a man's on foot: a troop's Reach is its lance or pike, not its blade.
    internal const float MountedMeleeReach = 1.9f;

    void Strike(Unit a)
    {
        float dmg = a.Type.Damage * a.Power;
        if (!a.IsPlayer)
        {
            if (a.Target is not { Active: true } t) return;
            float extra = t.Mounted ? 0.5f : 0;
            // A rider's blow lands on whatever is beside the horse; a footman needs the target in front.
            float aiReach = a.Mounted ? MathF.Min(a.Type.Reach, MountedMeleeReach) : a.Type.Reach;
            bool lands = a.Mounted
                ? Vector2.Distance(a.Pos, t.Pos) <= aiReach + 0.35f + extra
                : InArc(a, t, aiReach + 0.4f + extra, 0.5f);
            if (lands) Hit(a, t, dmg, false);
            if (lands && a.Type.Has("sweep")) Sweep(a, t, dmg, aiReach + 0.4f);
            return;
        }
        // Player: thrusts reach far but hit one man; side cuts sweep a wide arc and cleave up to 3.
        var (reach, minDot, cleave) = a.Swing switch
        {
            AttackDir.Thrust => (a.Type.Reach + 0.7f, 0.8f, 1),
            AttackDir.Overhead => (a.Type.Reach + 0.3f, 0.6f, 1),
            _ => (a.Type.Reach + 0.3f, 0.2f, 3),
        };
        if (a.Mounted) reach += 0.4f; // the player rides high in the saddle and can swing further
        int hits = 0;
        foreach (var t in Near(a.Pos, reach + 1, nearBrace).Where(t => t.Team != a.Team && t.Active).OrderBy(t => Vector2.DistanceSquared(t.Pos, a.Pos)).ThenBy(t => t.Index).ToList())
            if (InArc(a, t, reach + (t.Mounted ? 0.6f : 0), minDot) && hits++ < cleave) Hit(a, t, dmg, false);
    }

    static bool InArc(Unit a, Unit t, float reach, float minDot)
    {
        var d = t.Pos - a.Pos;
        float l = d.Length();
        return l <= reach && (l < 0.01f || Vector2.Dot(d / l, a.Dir) >= minDot);
    }

    /// Mount & Blade speed bonus: blows land harder the faster the two bodies close on each other.
    static float SpeedBonus(Unit a, Unit t)
    {
        var n = t.Pos - a.Pos;
        float l = n.Length();
        if (l < 0.01f) return 0;
        var rel = a.Vel - t.Vel;
        float eff = 0.4f * rel.Length() + 0.6f * Vector2.Dot(rel, n / l);
        return MathF.Max(0, eff - 2) * 0.05f;
    }

    enum Side { Front, Flank, Rear }

    static Side SideOf(Unit a, Unit t)
    {
        var from = a.Pos - t.Pos;
        if (from.LengthSquared() < 1e-4f) return Side.Front;
        float dot = Vector2.Dot(Vector2.Normalize(from), t.Dir);
        return dot > 0.4f ? Side.Front : dot < -0.4f ? Side.Rear : Side.Flank;
    }

    /// One blow (melee, arrow or lance) from a at t: horse hits, blocks and parries, then damage multipliers.
    internal void Hit(Unit a, Unit t, float dmg, bool ranged, bool lance = false)
    {
        if (a.Team == 1) dmg *= Options.EnemyDamage;
        if (ranged && Wall != null && SiegeWall.Inside(t.Pos) && !SiegeWall.Inside(a.Pos) && Terrain.Height(t.Pos) > Terrain.Height(a.Pos) + 2) dmg *= 0.6f; // merlons
        // Arrows and spears often take the horse instead of the rider (a footman's blade rarely does).
        if (t.Mounted && rng.NextSingle() < (ranged ? 0.55f : a.Type.Class == UnitClass.Skirmisher ? 0.55f : 0.18f))
        {
            HitHorse(t, dmg * a.Type.VsMounted * (a.Squad?.Company?.VsMounted ?? 1), a);
            return;
        }

        var side = t.Squad?.Formation == Formation.Square && !t.Routing ? Side.Front : SideOf(a, t); // a square has no flank
        bool facing = side == Side.Front;
        if (ranged && t.Squad?.Formation == Formation.Loose && rng.NextSingle() < 0.3f) return; // open ranks: the arrow finds a gap
        if (t.IsPlayer)
        {
            if (facing && t.Action == UnitAction.Block)
            {
                // Matching the incoming direction just as the blow comes is a parry: free, and it throws the attacker off balance.
                if (!ranged && !lance && Input.Dir == Dirs.Mirror(a.Swing) && Time - t.BlockStart < ParryWindow)
                {
                    Stagger(a, 0.9f);
                    t.Stamina = MathF.Max(0, t.Stamina - 2);
                    PlayerHit = ("PARRY!", Time);
                    Sound(Cue.Clash, t.Pos);
                    return;
                }
                if (!t.Type.HasShield && !ranged && Input.Dir != Dirs.Mirror(a.Swing))
                {
                    dmg *= 0.5f; // a two-handed guard only half-stops a blow from the wrong side
                    goto Struck;
                }
                t.Stamina -= (ranged ? 4 : dmg * (lance ? 1.2f : 0.8f)) * (t.Type.HasShield ? 1 : 1.4f); // a shield stops any direction, at a stamina cost
                Sound(Cue.Block, t.Pos);
                if (t.Stamina <= 0) { t.Stamina = 0; Stagger(t, 0.8f); Log("Guard broken!"); }
                else if (!ranged && !lance) Stagger(a, 0.35f);
                return;
            }
        }
        else if (facing && t.Action is UnitAction.None or UnitAction.Block)
        {
            // Shields cover every direction; without one, thrusts and overheads are harder to read.
            float chance = t.Type.BlockChance * (lance ? 0.5f : 1) * (a.Type.Huge ? 0.5f : 1) * (t.Type.HasShield
                ? (a.Swing == AttackDir.Thrust ? 0.9f : 1)
                : a.Swing switch { AttackDir.Thrust => 0.7f, AttackDir.Overhead => 0.85f, _ => 1 });
            if (ranged && !t.Type.HasShield) chance *= 0.3f;
            if (t.Squad?.Formation == Formation.ShieldWall && t.Type.HasShield) chance = MathF.Min(0.9f, chance * (ranged ? 1.8f : 1.35f)); // locked shields
            if (rng.NextSingle() < chance)
            {
                t.Action = UnitAction.Block;
                t.ActionTimer = 0.35f;
                if (ranged && t.Type.HasShield && t.StuckArrows < 4) t.StuckArrows++;
                if (a.IsPlayer) PlayerHit = ("BLOCKED", Time);
                Sound(t.Type.HasShield ? Cue.Block : Cue.Clash, t.Pos);
                return;
            }
        }

        Struck:
        // Chambering: striking into the enemy's blow from the mirrored side as it lands turns it aside.
        if (t.IsPlayer && !ranged && !lance && t.Action == UnitAction.Windup && t.Swing == Dirs.Mirror(a.Swing))
        {
            Stagger(a, 1.0f);
            PlayerHit = ("CHAMBER!", Time);
            Sound(Cue.Clash, t.Pos);
            return;
        }
        float speed = ranged || lance ? 0 : SpeedBonus(a, t);
        // Total War high ground: up to +-15% for fighting downhill / uphill.
        float high = ranged ? 0 : Math.Clamp((Terrain.Height(a.Pos) - Terrain.Height(t.Pos)) * 0.05f, -0.15f, 0.15f);
        float tired = Fatigue(a);
        float officer = tired * (a.Squad?.Company is Company oc ? (oc.Has("Hothead") ? 1.1f : 1) * (oc.Has("Veteran") ? 1.05f : 1) : 1);
        if (ranged && t.Squad?.Formation == Formation.Square) officer *= 1.25f;                    // a packed square is an easy mark
        if (!ranged && a.Squad is { Formation: Formation.Wedge, Order: Order.Charge }) officer *= 1.15f; // a wedge drives in
        float mult = officer * (1 + speed) * (1 + high) * (side == Side.Rear ? 1.5f : side == Side.Flank ? 1.2f : 1) * (t.Mounted ? a.Type.VsMounted * (a.Squad?.Company?.VsMounted ?? 1) : 1)
            * (t.Type.Wild ? a.Type.VsMonster : 1); // monster hunters
        if (side != Side.Front && t.Squad != null) t.Squad.Pressure += side == Side.Rear ? 1 : 0.5f;
        float real = Damage(a, t, dmg * mult, side == Side.Rear ? 4 : side == Side.Flank ? 1.5f : 0, ranged ? a.Type.Pierce : 0);
        if (!ranged && a.Type.Has("knockback")) Knock(a, t);
        Sound(ranged ? Cue.ArrowHit : t.Type.Armor >= 8 ? Cue.Clash : Cue.Hit, t.Pos);
        if (a.IsPlayer)
        {
            var text = $"{real:0} DMG";
            if (lance) text = "COUCHED LANCE " + text;
            if (speed >= 0.1f) text += $"  +{speed * 100:0}% SPEED";
            if (side != Side.Front) text += side == Side.Rear ? "  REAR" : "  FLANK";
            if (high >= 0.05f) text += "  HIGH GROUND";
            PlayerHit = (text, Time);
        }
    }

    /// Armour, then hp, morale shock and interrupts. Returns the damage dealt.
    float Damage(Unit a, Unit t, float dmg, float extraShock, float pierce = 0)
    {
        float real = MathF.Max(dmg * 0.25f, dmg - t.Type.Armor * (1 - pierce)); // bolts punch through part of the armour
        t.Hp -= real;
        t.HitFlash = 0.15f;
        t.HurtAt = Time;
        t.Morale -= real * 0.16f + extraShock * 0.7f;
        if (t.Action == UnitAction.Windup || t.Charging) Stagger(t, 0.3f); // hits interrupt swings
        if (t.Hp <= 0) Kill(t, a);
        return real;
    }

    internal void HitHorse(Unit r, float dmg, Unit by)
    {
        r.HorseHp -= dmg;
        r.HitFlash = 0.15f;
        if (by.IsPlayer) PlayerHit = ($"HORSE HIT {dmg:0}", Time);
        if (r.HorseHp > 0) return;
        // The horse goes down and throws its rider.
        DeadHorses.Add(new DeadHorse(r.Pos, r.Heading, r.Team, r.Coat, r.Horse!.Barding));
        r.Horse = null;
        r.Speed = 0;
        r.Vel = Vector2.Zero;
        r.Couched = false;
        r.Morale -= 10;
        if (r.IsPlayer) Log("Your horse is slain! Find a loose one to mount.");
        Stagger(r, 1.2f);
        Damage(by, r, 12, 0);
    }

    static void Stagger(Unit u, float time)
    {
        if (u.Type.Huge) return; // a troll's swing is not stopped by a man's blow or shield
        u.Action = UnitAction.Stagger;
        u.ActionTimer = time;
        u.Charging = false;
        u.Charge = 0;
        u.Couched = false;
    }

    /// A goblin boss is down: every craven of his side loses heart at once and stops rallying; his foes take courage.
    void BossFalls(Unit boss)
    {
        BossDown[boss.Team] = true;
        foreach (var u in Units)
            if (u.Active && u.Team == boss.Team && u.Type.Bound) u.Morale -= 30;
            else if (u.Active && u.Team != boss.Team) u.Morale = MathF.Min(100, u.Morale + 8);
        Log(boss.Type.Look.Beast ? $"The {boss.Type.Name.ToLowerInvariant()} is down! The pack scatters." : $"The {boss.Type.Name.ToLowerInvariant()} is slain! {(boss.Team == 0 ? "Your" : "The")} goblins scatter.");
    }

    void Kill(Unit t, Unit killer)
    {
        t.Dead = true;
        t.DiedAt = Time;
        t.Hp = 0;
        t.Action = UnitAction.None;
        t.Vel = t.Wish = Vector2.Zero;
        if (t.Mounted)
        {
            if (t.Horse!.Barding != "wolf") // a warg whose rider falls runs off into the wild; nobody else can ride it
                LooseHorses.Add(new LooseHorse { Mount = t.Horse!, Hp = t.HorseHp, Pos = t.Pos, Heading = t.Heading, Team = t.Team, Coat = t.Coat });
            if (killer.Squad != null && killer.Team != t.Team && t.Horse.Barding != "wolf") killer.Squad.HorseKills++;
            t.Horse = null;
            t.Pos -= new Vector2(-t.HeadingDir.Y, t.HeadingDir.X) * 1.0f; // the rider falls off to the side
        }
        if (killer.IsPlayer) PlayerKills++;
        if (killer.Squad != null && killer.Team != t.Team) killer.Squad.KillsByTier[Math.Clamp(t.Type.Tier, 0, 7)]++;
        if (killer.Squad != null && killer.Team != t.Team) killer.Squad.Kills++;
        foreach (var u in t.IsLeader ? Units.ToArray() : Near(t.Pos, 10, nearKill).ToArray()) // a leader's fall is felt across the field
        {
            if (!u.Active) continue;
            float d = Vector2.Distance(u.Pos, t.Pos);
            if (u.Team == t.Team) { if (t.IsLeader) u.Morale -= 16; else if (d < 8) u.Morale -= 3; }
            else if (d < 10) u.Morale += killer.IsPlayer ? 6 : 2; // heroics inspire more
        }
        if (t.Type.Has("boss") && !BossDown[t.Team]) BossFalls(t);
        if (t.IsPlayer) Log("You have fallen! Your troops waver.");
        else if (t.IsLeader) Log("The enemy captain is slain!");
    }

    void Shoot(Unit u, Unit t)
    {
        u.Ammo--;
        Sound(Cue.Bow, u.Pos);
        u.Cooldown = (u.Type.Windup + u.Type.Recover + 0.8f + rng.NextSingle() * 0.8f) * u.Type.Reload; // a crossbow takes long to span
        u.Action = UnitAction.Recover; // shows the release pose
        u.ActionTimer = 0.25f;
        float d = Vector2.Distance(u.Pos, t.Pos);
        var aim = t.Pos + t.Vel * (d / ArrowSpeed); // lead the target
        aim += new Vector2(Gauss(), Gauss()) * d * 0.035f * (u.Squad?.Company?.Has("Sharpshooter") == true ? 0.65f : 1) * (u.Mounted ? 1.5f : 1) * Weather.Spread;
        float time = MathF.Max(0.1f, Vector2.Distance(u.Pos, aim) / ArrowSpeed);
        var h = (aim - u.Pos) / time;
        float from = Terrain.Height(u.Pos) + 1.5f, land = Terrain.Height(aim) + (t.Mounted ? 1.5f : 1.0f);
        // Launch 1.5 m above the archer's ground, land at chest height on the target's ground after `time` seconds.
        Arrows.Add(new Projectile
        {
            Pos = new(u.Pos.X, from, u.Pos.Y),
            Vel = new(h.X, 0.5f * Gravity * time + (land - from) / time, h.Y),
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
            var flat = new Vector2(p.Pos.X, p.Pos.Z);
            float ground = Terrain.Height(flat);
            if (p.Pos.Y <= ground) { p.Pos.Y = ground; p.Stuck = 0; continue; }
            if (p.Pos.Y > ground + 2.6f) continue;
            foreach (var t in Near(flat, 1.5f, nearArrow))
            {
                if (t.Team == p.Shooter.Team || !t.Active) continue;
                float r = t.Mounted ? 0.8f : 0.4f;
                if ((!t.Mounted && p.Pos.Y > ground + 2) || Vector2.DistanceSquared(t.Pos, flat) > r * r) continue;
                Hit(p.Shooter, t, p.Damage, true);
                Arrows.RemoveAt(i);
                break;
            }
        }
    }

    /// Braced spears catch a horse that rides onto their points (their reach beats a couched lance).
    void Braces()
    {
        foreach (var b in Units)
        {
            if (!b.Active || b.BraceCd > 0 || !Braced(b)) continue;
            float reach = b.Type.Reach + BraceExtra;
            foreach (var u in Near(b.Pos, reach + 0.5f, nearBrace))
            {
                if (!u.Active || !u.Mounted || u.Team == b.Team) continue;
                var to = u.Pos - b.Pos;
                float d = to.Length();
                if (d > reach || d < 0.01f || Vector2.Dot(to / d, b.Dir) < 0.5f) continue;
                float closing = Vector2.Dot(u.Vel - b.Vel, -to / d);
                if (closing < 5) continue;
                b.BraceCd = 1.5f;
                b.Action = UnitAction.Recover; // shows the thrust
                b.Swing = AttackDir.Thrust;
                b.ActionTimer = b.Type.Recover;
                HitHorse(u, closing * (b.Type.Class == UnitClass.Skirmisher ? 8 : 4.5f), b);
                if (u.Mounted) { u.Speed *= 0.25f; Stagger(u, 0.6f); }
                break;
            }
        }
    }

    /// Couched lances: the point is a segment ahead of the horse; damage comes from the closing speed alone.
    void Lances()
    {
        foreach (var u in Units)
        {
            if (!u.Active || !u.Couched || !u.Mounted) continue;
            var hd = u.HeadingDir;
            Vector2 a = u.Pos + hd * LanceFrom, b = u.Pos + hd * LanceTip;
            foreach (var t in Near((a + b) / 2, 2, nearBrace))
            {
                if (!t.Active || t.Team == u.Team) continue;
                float r = t.Mounted ? 0.9f : 0.35f;
                if (DistToSegment(t.Pos, a, b) > r) continue;
                float closing = Vector2.Dot(u.Vel - t.Vel, hd);
                if (closing < 4) continue;
                u.Swing = AttackDir.Thrust;
                Hit(u, t, 10 + closing * 4.2f, false, lance: true);
                u.Couched = false;
                u.Cooldown = 1.8f;
                u.Contact = true;
                break;
            }
        }
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float k = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * k);
    }
}
