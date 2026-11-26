using Microsoft.Xna.Framework;

namespace Ironfield;

/// CR5: Smart dueling & 1vX tactical AI for all fighters in Options.Duel and enemies engaging near the player (<= 14m).
/// Handles weapon-reach spacing, lateral strafing, 1vX flank circling & attack-slot pacing, and tier-scaled feints/kicks/ripostes.
public partial class Battle
{
    bool UseDuelAI(Unit u, Unit? enemy, float d)
    {
        if (enemy == null || !enemy.Active || u.Mounted || u.Type.Look.Beast || u.Type.Huge || u.Ammo > 0) return false;
        if (Options.Duel) return true;
        if (d > 14f) return false;
        if (!enemy.IsPlayer)
        {
            // Nearby AI-vs-AI skirmishes (broken ranks, heroes, or unranked fighters within 16m of player) also use tactical spacing, feints & kicks.
            if (!Player.Active || Vector2.DistanceSquared(u.Pos, Player.Pos) > 16f * 16f) return false;
            return u.Squad == null || u.Squad.Broken || u.IsLeader || u.Hero;
        }
        return u.Squad == null || !Ranked(u.Squad) || u.Squad.Broken || u.IsLeader || u.Hero || Overwhelms(u.Squad);
    }

    /// Deterministically ranks this fighter among allies engaging the same target (0 = primary attacker, 1 = flank attacker, 2+ = circling flankers).
    (int Rank, int Total) DuelSlot(Unit u, Unit target)
    {
        float myD2 = Vector2.DistanceSquared(u.Pos, target.Pos);
        int rank = 0, total = 0;
        foreach (var o in Units)
        {
            if (!o.Active || o.Team != u.Team || o.Routing || o.Mounted) continue;
            float d2 = Vector2.DistanceSquared(o.Pos, target.Pos);
            if (d2 > 10f * 10f) continue;
            if (o.Target != target && !Options.Duel) continue;
            total++;
            if (o != u && (d2 < myD2 - 0.01f || (MathF.Abs(d2 - myD2) <= 0.01f && o.Index < u.Index))) rank++;
        }
        return (rank, Math.Max(1, total));
    }

    /// Pick an attack direction that exploits the opponent's open guard side based on tier.
    AttackDir SmartSwingDir(Unit u, Unit target)
    {
        if (u.Type.ThrustOnly) return AttackDir.Thrust;
        int tier = u.Type.Tier;
        // T3+ actively read the opponent's GuardDir and swing around it; T5+ almost always attack the open side.
        float readChance = tier >= 5 ? 0.85f : tier >= 3 ? 0.60f : 0.15f;
        if (rng.NextSingle() < readChance)
        {
            var blocked = Dirs.Mirror(target.GuardDir);
            // Pick one of the 3 unblocked directions deterministically via seeded rng.
            int pick = rng.Next(3);
            int cur = 0;
            for (int d = 0; d < 4; d++)
            {
                var ad = (AttackDir)d;
                if (ad == blocked) continue;
                if (cur++ == pick) return ad;
            }
        }
        return StyleSwing(u) ?? RandomSwing(u);
    }

    /// CR5: Dueling & 1vX footwork, slot pacing, feints, kicks, and ripostes.
    void DuelThink(Unit u, Unit enemy, float d, float dt)
    {
        u.Target = enemy;
        var toEnemy = enemy.Pos - u.Pos;
        if (toEnemy.LengthSquared() < 1e-4f) toEnemy = Vector2.UnitX;
        var dirTo = Vector2.Normalize(toEnemy);
        var side = new Vector2(-dirTo.Y, dirTo.X);
        u.Face(dirTo);

        // Update explicit directional guard toward incoming swings.
        UpdateGuardDir(u, enemy, dt);

        // Mid-windup feint for T3+ fighters when the target is already mirroring their swing.
        if (u.Action == UnitAction.Windup && u.WindupDuration > 0.05f && u.Type.Tier >= 3 && u.Stamina > 25f && !u.Morphed)
        {
            float prog = 1f - u.ActionTimer / u.WindupDuration;
            if (prog is > 0.25f and < 0.55f && enemy.Action == UnitAction.Block && enemy.GuardDir == Dirs.Mirror(u.Swing))
            {
                float feintChance = u.Type.Tier >= 5 ? 0.28f : 0.14f;
                if (rng.NextSingle() < feintChance * dt * 8f)
                {
                    // Feint into an unblocked thrust or opposite cut!
                    u.Morphed = true;
                    u.Swing = u.Type.Tier >= 5 ? AttackDir.Thrust : Dirs.Mirror(u.Swing);
                    if (u.Swing == Dirs.Mirror(enemy.GuardDir)) u.Swing = AttackDir.Overhead;
                    u.ActionTimer = u.Type.Windup * 0.55f;
                    u.WindupDuration = u.ActionTimer;
                    u.Stamina = MathF.Max(0, u.Stamina - 6f);
                    return;
                }
            }
        }

        if (u.Action == UnitAction.Block)
        {
            // Strafe gently while holding guard instead of freezing stiff.
            float strafeSign = ((int)((Time + u.Index * 0.7f) / 2.2f) & 1) == 0 ? 1f : -1f;
            u.Wish = side * (strafeSign * u.Type.Speed * 0.42f);
            return;
        }

        float reach = ReachOf(u) + (enemy.Mounted ? 0.9f : 0f);
        float pref = MathF.Max(1.25f, reach * 0.85f);
        var (slot, total) = DuelSlot(u, enemy);
        bool backExposed = Vector2.Dot(-dirTo, enemy.Dir) < -0.25f;

        // 1vX Flank Circling & Attack-Slot Pacing:
        // Slot 0 = primary attacker; Slot 1 = angled flank attacker; Slot 2+ = orbit flanks/rear at 3.2-4.2m and step in if back is turned.
        if (total >= 2 && slot >= 1)
        {
            float orbitSign = (slot & 1) == 1 ? 1f : -1f;
            float targetAngle = slot == 1 ? 1.15f * orbitSign : (2.15f + 0.55f * (slot - 2)) * orbitSign;
            float baseAng = MathF.Atan2(enemy.Dir.Y, enemy.Dir.X) + targetAngle;
            float orbitDist = slot == 1 ? MathF.Max(pref + 0.25f, 2.0f) : (backExposed ? pref : 3.5f);
            var flankSpot = enemy.Pos + new Vector2(MathF.Cos(baseAng), MathF.Sin(baseAng)) * orbitDist;
            var toSpot = Via(u, flankSpot) - u.Pos;
            float spotDist = toSpot.Length();

            // Slot 1 attacks when in position; Slot 2+ only steps in to punish an exposed back or staggered target.
            bool canAttack = (slot == 1 && spotDist < 1.35f) || backExposed || enemy.Action == UnitAction.Stagger;
            if (canAttack && d <= reach + 0.15f && u.Cooldown <= 0 && u.Stamina > 12f)
            {
                StartSwing(u, enemy, SmartSwingDir(u, enemy));
                return;
            }
            if (spotDist > 0.35f)
            {
                u.Wish = (toSpot / spotDist) * u.Type.Speed * (spotDist > 2.2f ? 1.0f : 0.82f);
                return;
            }
        }

        // Spacing & footwork for primary attacker (or 1v1):
        float strafeDir = ((int)((Time + u.Index * 0.7f) / 2.2f) & 1) == 0 ? 1f : -1f;
        bool lowStam = u.Stamina < 24f;
        bool recovering = u.Cooldown > 0.35f;

        // Riposte immediately after parrying!
        if (u.RiposteReady && d <= reach + 0.25f)
        {
            StartSwing(u, enemy, SmartSwingDir(u, enemy));
            return;
        }

        // T3+ kick / bash against a turtling shielded foe.
        if (u.Type.Tier >= 3 && d <= 1.95f && u.Cooldown <= 0 && enemy.Action == UnitAction.Block && enemy.Type.HasShield && u.Stamina > 32f && rng.NextSingle() < 0.22f)
        {
            u.Stamina -= 15f;
            u.Action = UnitAction.Recover;
            u.ActionTimer = 0.32f;
            u.BashedAt = Time;
            Stagger(enemy, 0.85f, guardBreak: true);
            enemy.Stamina = MathF.Max(0, enemy.Stamina - 26f);
            enemy.Pos += dirTo * 1.05f;
            Sound(Cue.BashThud, enemy.Pos, priority: enemy.IsPlayer);
            if (enemy.IsPlayer) PlayerHit = ("GUARD BROKEN!", Time);
            return;
        }

        // Strike when at preferred reach and ready.
        if (!lowStam && d <= reach + 0.1f && u.Cooldown <= 0)
        {
            StartSwing(u, enemy, SmartSwingDir(u, enemy));
            return;
        }

        // Footwork: maintain preferred weapon distance, strafe laterally, step back when low on stamina or whiffing.
        if (lowStam || recovering)
        {
            var retreat = (-dirTo * 0.75f + side * (strafeDir * 0.65f));
            u.Wish = Vector2.Normalize(retreat) * (u.Type.Speed * 0.72f);
        }
        else if (d < pref * 0.74f)
        {
            var back = (-dirTo * 0.6f + side * (strafeDir * 0.8f));
            u.Wish = Vector2.Normalize(back) * (u.Type.Speed * 0.68f);
        }
        else if (d > pref * 1.15f)
        {
            var close = Via(u, enemy.Pos) - u.Pos;
            var cDir = close.LengthSquared() > 1e-4f ? Vector2.Normalize(close) : dirTo;
            var approach = Vector2.Normalize(cDir + side * (strafeDir * 0.28f));
            u.Wish = approach * u.Type.Speed;
        }
        else
        {
            u.Wish = side * (strafeDir * u.Type.Speed * 0.62f);
        }
    }
}
