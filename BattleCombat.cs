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
                if (u.IsPlayer) riposteSwing = false;
                u.Action = UnitAction.Recover;
                u.ActionTimer = u.IsPlayer ? 0.3f * u.Type.Recover / 0.5f : u.Type.Recover; // your weapon sets your pace (P1)
                break;
            case UnitAction.Recover:
                u.Action = UnitAction.None;
                if (u.IsPlayer) lastRecovered = Time;
                if (!u.IsPlayer) u.Cooldown = MathF.Max(u.Cooldown, rng.NextSingle() * 0.5f); // desync the line
                break;
            default:
                u.Action = UnitAction.None;
                u.Guard = false;
                break;
        }
    }

    /// A rider's sword arm reaches about as far as a man's on foot: a troop's Reach is its lance or pike, not its blade.
    internal const float MountedMeleeReach = 1.9f;

    void Strike(Unit a)
    {
        float dmg = StyleBlow(a, a.Type.Damage * a.Power);
        if (!a.IsPlayer)
        {
            if (a.Type.Flying) { Sound(Cue.WyvernScreech, a.Pos); Climb(a); } // struck or missed, a wyvern climbs away to come again
            if (a.Target is not { Active: true } t) return;
            float extra = t.Mounted ? 0.5f : 0;
            // A rider's blow lands on whatever is beside the horse; a footman needs the target in front.
            float aiReach = a.Mounted ? MathF.Min(a.Type.Reach, MountedMeleeReach) : ReachOf(a);
            bool lands = a.Mounted
                ? Vector2.Distance(a.Pos, t.Pos) <= aiReach + 0.35f + extra
                : InArc(a, t, aiReach + 0.4f + extra, 0.5f);
            if (lands)
            {
                if (a.Type.Look.Face == "troll") Sound(Cue.TrollSweep, a.Pos);
                else if (a.Type.Look.Face == "wolf") Sound(Cue.WolfSnarl, a.Pos);
                else if (a.Type.Look.Face == "bear") Sound(Cue.BearRoar, a.Pos);
                else if (a.Type.Look.Face == "spider") Sound(Cue.SpiderChitter, a.Pos);
                Hit(a, t, dmg, false);
            }
            if (lands && a.Type.Has("sweep")) Sweep(a, t, dmg, aiReach + 0.4f);
            return;
        }
        // Player: thrusts reach far but hit one man; side cuts sweep a wide arc and cleave up to 3.
        var (reach, minDot, cleave) = a.Swing switch
        {
            AttackDir.Thrust => (ReachOf(a) + 0.7f, 0.8f, 1),
            AttackDir.Overhead => (ReachOf(a) + 0.3f, 0.6f, 1),
            _ => (ReachOf(a) + 0.3f, 0.2f, 3),
        };
        if (a.Mounted) { reach += 0.4f; dmg *= 1 + 0.1f * RidingEdge; } // the player rides high in the saddle and can swing further
        int hits = 0;
        foreach (var t in Near(a.Pos, reach + 1, nearBrace).Where(t => (t.Team != a.Team || Mad) && t != a && t.Active).OrderBy(t => Vector2.DistanceSquared(t.Pos, a.Pos)).ThenBy(t => t.Index).ToList())
            if (InArc(a, t, reach + (t.Mounted ? 0.6f : 0), minDot) && hits++ < cleave) Hit(a, t, dmg, false);
    }

    /// What your own hands did (P3): arrow and lance hits, ripostes, and kills from the saddle or the bow.
    void CountDeed(Unit t, bool ranged, bool lance)
    {
        if (ranged) ArrowHits++;
        if (lance) LanceHits++;
        if (riposteSwing && !ranged && !lance) Ripostes++;
        if (t.Dead) { if (ranged) ArrowKills++; else if (Player.Mounted) SaddleKills++; }
    }

    static bool InArc(Unit a, Unit t, float reach, float minDot)
    {
        var d = t.Pos - a.Pos;
        float l = d.Length();
        return l <= reach && (l < 0.01f || Vector2.Dot(d / l, a.Dir) >= minDot);
    }

    /// Mount & Blade speed bonus: blows land harder the faster the two bodies close on each other.
    /// High ground in melee: a smooth curve on how far above his foe a man stands, up to +-15%, and up to +10% more
    /// for running downhill into the blow (a charge down a slope).
    internal static float HighGround(float above, float slope, float speed) =>
        0.15f * MathF.Tanh(above / 3) + (speed > 3 ? Math.Clamp(-slope, 0, 0.1f) : 0);

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
    /// A blow from a man of a broken company (a crowd, not a line) does this much.
    const float BrokenBlow = 0.7f;

    static Side SideOf(Unit a, Unit t)
    {
        var from = a.Pos - t.Pos;
        if (from.LengthSquared() < 1e-4f) return Side.Front;
        float dot = Vector2.Dot(Vector2.Normalize(from), t.Dir);
        // A man squared up to another foe has only the corner of his eye for a second one off to his side (a beast is no such man: its sweep is no aimed blow, and it towers over all round it).
        float front = !a.Type.Huge && !t.Type.Huge && t.Target != null && t.Target != a && t.Target.Active ? 0.75f : 0.4f;
        return dot > front ? Side.Front : dot < -0.4f ? Side.Rear : Side.Flank;
    }

    /// One blow (melee, arrow or lance) from a at t: horse hits, blocks and parries, then damage multipliers.
    internal void Hit(Unit a, Unit t, float dmg, bool ranged, bool lance = false, bool plunge = false)
    {
        if (a.Team == 1) dmg *= Options.EnemyDamage;
        if (OutOfReach(a, t, ranged)) { if (a.IsPlayer) PlayerHit = ("OUT OF REACH", Time); return; } // a wyvern aloft (BattleMonsters.cs)
        if (ranged && Wall != null && SiegeWall.Inside(t.Pos) && !SiegeWall.Inside(a.Pos) && Terrain.Height(t.Pos) > Terrain.Height(a.Pos) + 2) dmg *= 0.6f; // merlons
        // Arrows and spears often take the horse instead of the rider (a footman's blade rarely does).
        if (t.Mounted && rng.NextSingle() < (ranged ? 0.55f : a.Type.Class == UnitClass.Skirmisher ? 0.55f : 0.18f))
        {
            HitHorse(t, dmg * a.Type.VsMounted * (a.Squad?.Company?.VsMounted ?? 1), a);
            return;
        }

        // Testudo (D4): a halted company of shield men under arrows raises its shields all round.
        if (ranged && t.Mods.Testudo > 0 && t.Type.HasShield && !t.IsPlayer && t.Vel.LengthSquared() < 1 && rng.NextSingle() < t.Mods.Testudo)
        {
            if (t.StuckArrows < 4) t.StuckArrows++;
            Sound(Cue.ArrowShield, t.Pos);
            return;
        }
        if (ranged && t.Mods.Veil > 0 && rng.NextSingle() < t.Mods.Veil) return; // lost in mist or smoke, turned by a ward
        var side = t.Squad?.Formation == Formation.Square && !t.Routing ? Side.Front : SideOf(a, t); // a square has no flank
        bool facing = side == Side.Front;
        if (ranged && t.Squad?.Formation == Formation.Loose && rng.NextSingle() < 0.3f) return; // open ranks: the arrow finds a gap
        if (t.IsPlayer)
        {
            if (facing && Guarding) // Unbroken Wall (K2): nothing from the front gets through
            {
                if (!ranged && !lance) Stagger(a, 0.8f);
                Sound(ranged ? Cue.ArrowShield : ShieldCue(t), t.Pos, priority: true);
                PlayerHit = ("UNBROKEN", Time);
                return;
            }
            if (facing && t.Action == UnitAction.Block)
            {
                // Matching the incoming direction just as the blow comes is a parry: free, and it throws the attacker off balance.
                if (!ranged && !lance && Input.Dir == Dirs.Mirror(a.Swing) && Time - t.BlockStart < ParryTime)
                {
                    Stagger(a, 0.9f);
                    t.Stamina = MathF.Max(0, t.Stamina - 2);
                    RiposteUntil = Time + RiposteTime * (Perk("Champion") ? 1.5f : 1); // the opening: a swing started now is quick and hard to stop (P2)
                    Parries++;
                    CounterStrike(t, a);
                    PlayerHit = (Countering ? "PARRY! COUNTER" : "PARRY! RIPOSTE", Time);
                    Sound(Cue.ParrySteel, t.Pos, priority: true);
                    return;
                }
                if (!t.Type.HasShield && !ranged && Input.Dir != Dirs.Mirror(a.Swing))
                {
                    dmg *= 0.5f; // a two-handed guard only half-stops a blow from the wrong side
                    goto Struck;
                }
                t.Stamina -= (ranged ? 4 : dmg * (lance ? 1.2f : 0.8f)) * (t.Type.HasShield ? 1 : 1.4f) * (Perk("IronGrip") ? 0.7f : 1); // a shield stops any direction, at a stamina cost
                if (t.Stamina <= 0) { t.Stamina = 0; Stagger(t, 0.8f); Sound(Cue.ShieldSplinter, t.Pos, priority: true); Log("Guard broken!"); }
                else
                {
                    Sound(ranged ? Cue.ArrowShield : ShieldCue(t), t.Pos, priority: true);
                    if (!ranged && !lance) Stagger(a, 0.35f);
                }
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
            if (Webbed(t)) chance *= 0.4f; // caught in a web, he can hardly lift his guard
            if (a.IsPlayer && !ranged && !lance) chance = riposteSwing && (t.Type.Tier <= 3 || Perk("Champion")) ? 0 : MathF.Min(0.95f, chance + 0.08f * Math.Min(Chain, 4)); // a riposte slips the guard; a rhythm is read
            if (t.Squad?.Formation == Formation.ShieldWall && t.Type.HasShield && InOrder(t)) chance = MathF.Min(0.9f, chance * (ranged ? 1.8f : 1.35f)); // locked shields
            if (!ranged) chance *= RankGuard(t) * RankEdge(a); // friends at the shoulders, foes all round (docs/archive/MELEE.md D3.7)
            if (a.IsPlayer && AuraBlade && !ranged || FoeBlade(a, ranged)) chance *= BladeGuard; // aura cuts through a guard (K1; a champion's, K6)
            chance *= StyleGuard(a, t, ranged, plunge); // a crush, a bash, an arrow from above a shield (BattleStyles.cs)
            if (t.Guard && !ranged && t.Squad != null) chance = MathF.Min(0.92f, chance + (1 - chance) * 0.5f * t.Squad.Drill); // he saw it coming
            chance = MathF.Min(0.95f, chance * (1 + t.Mods.Block + (t.Vel.LengthSquared() < 1 ? t.Mods.BlockStill : 0))); // passives: locked shields (BattleEffects.cs)
            if (rng.NextSingle() < chance)
            {
                if (t.Guard) t.Cooldown = 0; // a held guard opens the way for his own blow
                if (!ranged) StyleBlocked(t);
                t.Action = UnitAction.Block;
                t.ActionTimer = 0.35f;
                if (ranged && t.Type.HasShield && t.StuckArrows < 4) t.StuckArrows++;
                if (a.IsPlayer) PlayerHit = ("BLOCKED", Time);
                Sound(ranged ? Cue.ArrowShield : ShieldCue(t), t.Pos, priority: a.IsPlayer);
                return;
            }
        }

        Struck:
        // Chambering: striking into the enemy's blow from the mirrored side as it lands turns it aside.
        if (t.IsPlayer && !ranged && !lance && t.Action == UnitAction.Windup && t.Swing == Dirs.Mirror(a.Swing))
        {
            Stagger(a, 1.0f);
            PlayerHit = ("CHAMBER!", Time);
            Sound(Cue.ParrySteel, t.Pos, priority: true);
            return;
        }
        float speed = ranged || lance ? 0 : SpeedBonus(a, t);
        float high = ranged ? 0 : HighGround(Terrain.Height(a.Pos) - Terrain.Height(t.Pos), a.Slope, a.Vel.Length());
        float tired = Fatigue(a);
        float officer = tired * (a.Squad?.Company is Company oc ? (oc.Has("Hothead") ? 1.1f : 1) * (oc.Has("Veteran") ? 1.05f : 1) : 1);
        if (ranged && t.Squad?.Formation == Formation.Square) officer *= 1.25f;                    // a packed square is an easy mark
        if (!ranged && a.Squad is { Formation: Formation.Wedge, Order: Order.Charge }) officer *= 1.15f; // a wedge drives in
        if (!ranged && a.Squad is { Broken: true, Swarm: false } && Ranked(a.Squad) && !Overwhelms(a.Squad)) officer *= BrokenBlow; // a crowd has no room to swing (round a beast it needs none)
        float fury = a.Mods.Fury > 0 && a.Squad is { StartCount: > 0 } fs ? a.Mods.Fury * (1 - fs.Alive / (float)fs.StartCount) : 0; // battle fury: the fewer, the fiercer
        float headsman = !ranged && (t.IsLeader || t.Hero || t.Sergeant) ? a.Mods.Headsman : 0;                                    // headsmen seek the officers
        float mult = officer * (1 + a.Mods.Damage + fury + headsman) * (1 + speed) * (1 + high) * (side == Side.Rear ? 1.5f : side == Side.Flank ? 1.2f : 1) * (t.Mounted ? a.Type.VsMounted * (a.Squad?.Company?.VsMounted ?? 1) : 1)
            * (t.Type.Wild ? a.Type.VsMonster : 1)  // monster hunters
            * (ranged && t.Type.Has("bones") ? 0.3f : 1) // arrows pass between a skeleton's ribs
            * (a.IsPlayer && AuraBlade && !ranged || FoeBlade(a, ranged) ? BladeDamage : 1) // the aura blade (K1; a champion's, K6)
            * (a.IsPlayer && edgeLeft > 0 && !ranged ? 1 + edgeK : 1) // Thorn Edge (K2)
            * ClashMult(a, t) // a clash of champions (MA7)
            * (a.Type.Wild ? BeastBlow(a) : 1); // a wounded bear's rage (BattleMonsters.cs)
        if (t.Squad != null) t.Squad.HitsTaken++;
        if (side != Side.Front && a.Squad != null && !ranged) a.Squad.FlankHits++;
        if (side != Side.Front && t.Squad != null) { t.Squad.Pressure += side == Side.Rear ? 1 : 0.5f; Flanked(t.Squad, side == Side.Rear); }
        bool foeBlade = FoeBlade(a, ranged);
        float real = Damage(a, t, dmg * mult, side == Side.Rear ? 4 : side == Side.Flank ? 1.5f : 0, ranged || a.IsPlayer || foeBlade ? MathF.Min(0.9f, a.Type.Pierce + (a.IsPlayer && AuraBlade && !ranged || foeBlade ? BladePierce : 0) + (a.IsPlayer && edgeLeft > 0 && !ranged ? 0.9f : 0)) : 0, ranged); // your mace or axe bites through armour too, and the aura blade more
        if (a.IsPlayer && !ranged && AuraBlade) BladeMarks(t); // breaks a working, leaves craft marks (MA5)
        if (a.IsPlayer && !ranged) BladeLanded();
        if (foeBlade) FoeAura = MathF.Max(0, FoeAura - BladeHitCost);
        if (!ranged && a.Type.Has("knockback")) Knock(a, t);
        if (!ranged && a.Type.Wild && a.Type.Has("poison")) Envenom(a, t, dmg * mult); // a bite or sting keeps working
        StyleLanded(a, t, dmg, ranged, lance);
        if (a.IsPlayer) CountDeed(t, ranged, lance);
        Sound(HitCue(a, t, dmg * mult, ranged, lance), t.Pos, priority: a.IsPlayer || t.IsPlayer);
        if (a.IsPlayer)
        {
            var text = $"{real:0} DMG";
            if (lance) text = "COUCHED LANCE " + text;
            if (speed >= 0.1f) text += $"  +{speed * 100:0}% SPEED";
            if (side != Side.Front) text += side == Side.Rear ? "  REAR" : "  FLANK";
            if (high >= 0.05f) text += "  HIGH GROUND";
            PlayerHit = (text, Time);
            if (Blows.Count > 0 && Blows[^1].Target == t && !Blows[^1].OnPlayer) // the number over him says why it bit
                Blows[^1] = Blows[^1] with { Tag = lance ? "LANCE" : side == Side.Rear ? "REAR" : side == Side.Flank ? "FLANK" : speed >= 0.1f ? "SPEED" : high >= 0.05f ? "HIGH GROUND" : "" };
        }
    }

    /// Keeps a blow by or on the player for the numbers drawn over the man hit (drawing only).
    void NoteBlow(Unit t, float real, bool onPlayer)
    {
        Blows.RemoveAll(b => b.At < Time - 2);
        Blows.Add(new Blow(t, real, t.Dead, onPlayer, Time));
    }

    /// Armour, then hp, morale shock and interrupts. Returns the damage dealt.
    float Damage(Unit a, Unit t, float dmg, float extraShock, float pierce = 0, bool ranged = false)
    {
        float armour = t.Type.Armor + t.Mods.Armour + (ranged ? t.Mods.ArrowArmour : 0); // iron skin, a pavise (BattleEffects.cs)
        float real = MathF.Max(dmg * 0.25f, dmg - armour * (1 - pierce)); // bolts punch through part of the armour
        if (a.IsPlayer || t.IsPlayer) real = AuraSoak(a, t, real, ranged); // aura armour (K1)
        if (FoeTier > 0 && (a == EnemyCaptain || t == EnemyCaptain)) real = FoeSoak(a, t, real, ranged); // a champion's (K6)
        t.Hp -= real;
        t.HitFlash = 0.15f;
        t.HitFrom = a.Pos;
        t.HurtAt = Time;
        t.Morale -= (real * 0.16f + extraShock * 0.7f) * (1 + a.Mods.ShockDealt); // a fearsome man's blows shake more
        // Hits interrupt swings, but not a rhythm he has read: past your second chained blow he swings through it (P2).
        if ((t.Action == UnitAction.Windup || t.Charging) && !(a.IsPlayer && Chain >= 2)) Stagger(t, 0.3f);
        if (t.Hp <= 0) Kill(t, a);
        if (a.IsPlayer || t.IsPlayer) NoteBlow(t, real, !a.IsPlayer);
        return real;
    }

    internal void HitHorse(Unit r, float dmg, Unit by)
    {
        r.HorseHp -= dmg;
        r.HitFlash = 0.15f;
        if (by.IsPlayer) PlayerHit = ($"HORSE HIT {dmg:0}", Time);
        if (r.HorseHp > 0) return;
        // The horse goes down and throws its rider.
        Sound(r.Horse!.Barding == "wolf" ? Cue.WolfSnarl : Cue.Neigh, r.Pos);
        DeadHorses.Add(new DeadHorse(r.Pos, r.Heading, r.Team, r.Coat, r.Horse!.Barding, Time));
        r.Horse = null;
        r.ThrownAt = Time;
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

    internal void Kill(Unit t, Unit killer)
    {
        if (t.IsPlayer && Perk("FieldSurgeon") && !secondWind)
        {
            // Field Surgeon (P3): once a battle you get back up.
            secondWind = true;
            t.Hp = t.Type.MaxHp * 0.5f;
            Stagger(t, 1.5f);
            Log("You go down... and get back up!");
            PlayerHit = ("BACK ON YOUR FEET", Time);
            return;
        }
        if (LastStand(t, killer)) return; // P6
        t.Dead = true;
        FellFromRanks(t);
        t.DiedAt = Time;
        t.DiedFrom = killer.Pos;
        t.DiedFall = t.Mounted ? Fall.Saddle : killer.Type.Huge || killer.Type.Size > 1.5f ? Fall.Flung
            : Vector2.DistanceSquared(killer.Pos, t.Pos) > 16 ? Fall.Plain
            : killer.Swing == AttackDir.Thrust ? Fall.Thrust : killer.Swing is AttackDir.Left or AttackDir.Right ? Fall.Cut : Fall.Plain;
        t.Hp = 0;
        t.Action = UnitAction.None;
        t.Vel = t.Wish = Vector2.Zero;
        Sound(DeathCue(t), t.Pos);
        if (t.Mounted)
        {
            if (t.Horse!.Barding != "wolf") // a warg whose rider falls runs off into the wild; nobody else can ride it
                LooseHorses.Add(new LooseHorse { Mount = t.Horse!, Hp = t.HorseHp, Pos = t.Pos, Heading = t.Heading, Team = t.Team, Coat = t.Coat });
            if (killer.Squad != null && killer.Team != t.Team && t.Horse.Barding != "wolf") killer.Squad.HorseKills++;
            t.Horse = null;
            t.Pos -= new Vector2(-t.HeadingDir.Y, t.HeadingDir.X) * 1.0f; // the rider falls off to the side
        }
        if (killer.IsPlayer && t != killer) { PlayerKills++; CaptainKilled(t); } // the enemy sees it (BattleNotice.cs)
        if (t.IsPlayer) CaptainBroughtDown(killer);
        if (killer.IsPlayer && t == EnemyCaptain) ChampionFelled(); // a breakthrough in the field (MA7)
        if (killer.Squad != null && killer.Team != t.Team) killer.Squad.KillsByTier[Math.Clamp(t.Type.Tier, 0, 7)]++;
        if (killer.Squad != null && killer.Team != t.Team) killer.Squad.Kills++;
        if (killer.Team != t.Team) { killer.Kills++; if (t.IsLeader || t.Hero) killer.FelledLeader = true; }
        // Your named men are heard (L8): a veteran down, a veteran's fifth man.
        if (t.SoldierId != 0 && t.Team == 0) { Log($"{NameOf(t)} is down!"); Downed.Add(t); }
        if (killer.SoldierId != 0 && killer.Team == 0 && killer.Kills == 5) Log($"{NameOf(killer)} has felled five!");
        foreach (var u in t.IsLeader ? Units.ToArray() : Near(t.Pos, 10, nearKill).ToArray()) // a leader's fall is felt across the field
        {
            if (!u.Active) continue;
            float d = Vector2.Distance(u.Pos, t.Pos);
            if (u.Team == t.Team) { if (t.IsLeader) u.Morale -= 16; else if (d < 8 && u.Mods.Fury <= 0) u.Morale -= 3; } // battle fury: a friend's fall only angers
            else if (d < 10) u.Morale += killer.IsPlayer ? 6 : 2; // heroics inspire more
        }
        if (t.Type.Has("boss") && !BossDown[t.Team]) BossFalls(t);
        if (t.Type.Has("raise") && !Units.Any(u => u.Active && u.Team == t.Team && u.Type.Has("raise"))) MasterFalls(t);
        if (killer.Active && killer.Type.Has("feed")) killer.Hp = MathF.Min(killer.Type.MaxHp, killer.Hp + killer.Type.MaxHp * 0.3f); // a ghoul feeds
        if (t.IsPlayer) Log("You have fallen! Your troops waver.");
        else if (t.IsLeader) Log(t.Type.Wild ? $"The {t.Type.Name.ToLowerInvariant()} is slain!" : "The enemy captain is slain!");
    }

    void Shoot(Unit u, Unit t)
    {
        u.Cooldown = (u.Type.Windup + u.Type.Recover + 0.8f + rng.NextSingle() * 0.8f) * u.Type.Reload * StyleReload(u) * (1 + u.Mods.Reload); // a crossbow takes long to span
        float spread = (u.Squad?.Company?.Has("Sharpshooter") == true ? 0.65f : 1) * MathF.Max(0.2f, 1 + u.Mods.Spread); // steady aim
        if (MirageFor(u) is { } m) { Loose(u, m.Pos, null, u.Type.Damage, spread); return; } // a mirage draws their arrows (MA8)
        Loose(u, t.Pos, t, u.Type.Damage, spread);
    }

    /// An arrow from u at a point (leading the target t if there is one), scattered by distance times `spread`.
    void Loose(Unit u, Vector2 at, Unit? t, float damage, float spread)
    {
        u.Ammo--;
        if (u.Squad != null && Time - u.Squad.LooseAt > 2.5f) Sound(Cue.Volley, Vector2.Lerp(u.Pos, at, 0.5f)); // a volley whistles overhead
        if (u.Squad != null) u.Squad.LooseAt = Time;
        var kind = Look.GripOf(u.Type.Look.Weapon);
        Sound(LooseCue(u.Type.Look.Weapon, kind), u.Pos, priority: u.IsPlayer);
        u.Action = UnitAction.Recover; // shows the release pose
        u.ActionTimer = 0.25f;
        float d = Vector2.Distance(u.Pos, at);
        var aim = at + (t?.Vel ?? Vector2.Zero) * (d / ArrowSpeed); // lead the target
        aim += new Vector2(Gauss(), Gauss()) * d * 0.035f * spread * (u.Mounted && !(u.IsPlayer && Perk("HorseArcher")) && u.Mods.Gallop <= 0 ? 1.5f : 1) * Weather.Spread; // ghost riders shoot from the gallop as if standing
        float time = MathF.Max(0.1f, Vector2.Distance(u.Pos, aim) / ArrowSpeed);
        var h = (aim - u.Pos) / time;
        float from = Terrain.Height(u.Pos) + 1.5f, land = Terrain.Height(aim) + (t?.Mounted == true ? 1.5f : 1.0f) + (t?.Altitude ?? 0); // up at a flyer
        // Launch 1.5 m above the archer's ground, land at chest height on the target's ground after `time` seconds.
        Arrows.Add(new Projectile
        {
            Pos = new(u.Pos.X, from, u.Pos.Y),
            Vel = new(h.X, 0.5f * Gravity * time + (land - from) / time, h.Y),
            Shooter = u,
            Damage = damage,
            Kind = kind is Grip.Thrown or Grip.Sling or Grip.Crossbow ? kind : Grip.Bow,
        });
    }

    float Gauss() => MathF.Sqrt(-2 * MathF.Log(1 - rng.NextSingle())) * MathF.Cos(MathF.Tau * rng.NextSingle());

    /// Past this share of a bow's range an arrow comes down from above (a longbow's plunging shot, BattleStyles.cs).
    const float Plunging = 0.6f;

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
            var from = new Vector2(p.Pos.X, p.Pos.Z);
            p.Pos += p.Vel * dt;
            var flat = new Vector2(p.Pos.X, p.Pos.Z);
            float ground = Terrain.Height(flat);
            if (p.Pos.Y <= ground) { p.Pos.Y = ground; p.Stuck = 0; continue; }
            if (flyers && FlyerHit(flat, p.Pos.Y - ground, p.Shooter.Team) is Unit flier) { Hit(p.Shooter, flier, p.Damage, true); Arrows.RemoveAt(i); continue; } // a wyvern taken in the air
            if (p.Pos.Y > ground + 2.6f) continue;
            if (Covered(from, flat, p.Pos.Y - ground)) { p.Pos.Y = MathF.Max(ground, p.Pos.Y - 0.5f); p.Stuck = 0; continue; } // into a tree or a hedge
            foreach (var t in Near(flat, 1.5f, nearArrow))
            {
                if (t.Team == p.Shooter.Team || !t.Active) continue;
                float r = t.Mounted ? 0.8f : 0.4f;
                if ((!t.Mounted && p.Pos.Y > ground + 2) || Vector2.DistanceSquared(t.Pos, flat) > r * r) continue;
                if (CutsArrow(t, p.Vel)) { p.Pos.Y = ground; p.Stuck = 0; break; } // a Transcendent's blade cuts it from the air (MA7)
                Hit(p.Shooter, t, p.Damage, true, plunge: Vector2.DistanceSquared(p.Shooter.Pos, flat) > Plunging * Plunging * p.Shooter.Type.Range * p.Shooter.Type.Range); // a long shot drops from above
                Arrows.RemoveAt(i);
                break;
            }
        }
    }

    /// Does a low arrow flying from `a` to `b` this tick strike cover? Woods, crops and houses catch it in proportion to
    /// how much of them it flies through low (Data/ground.json `cover`); a hedge stops a flat shot outright, mostly.
    /// An arrow dropping steeply onto men in the open is low for only the last stride, so cover in front does not save them.
    bool Covered(Vector2 a, Vector2 b, float above)
    {
        float cover = Terrain.CoverAt(b);
        if (cover > 0 && rng.NextSingle() < cover * Vector2.Distance(a, b) * 0.12f) return true;
        return above < 1.8f && Terrain.Features.Count > 0 && Terrain.CrossesFeatureNear(a, b, mounted: false) && rng.NextSingle() < 0.7f; // stakes are no cover
    }

    /// Braced spears catch a horse that rides onto their points (their reach beats a couched lance).
    void Braces()
    {
        foreach (var b in Units)
        {
            if (!b.Active || b.BraceCd > 0 || !Braced(b)) continue;
            float reach = ReachOf(b) + BraceExtra * (1 + b.Mods.Brace); // set spears reach further
            foreach (var u in Near(b.Pos, reach + 0.5f, nearBrace))
            {
                if (!u.Active || !u.Mounted || u.Team == b.Team) continue;
                if (u.IsPlayer && b.Type.Tier < 4 && Perk("KnightOfTheCharge")) continue; // their points do not stop your charge
                var to = u.Pos - b.Pos;
                float d = to.Length();
                if (d > reach || d < 0.01f || Vector2.Dot(to / d, b.Dir) < 0.5f) continue;
                float closing = Vector2.Dot(u.Vel - b.Vel, -to / d);
                if (closing < 5) continue;
                b.BraceCd = 1.5f;
                b.Action = UnitAction.Recover; // shows the thrust
                b.Swing = AttackDir.Thrust;
                b.ActionTimer = b.Type.Recover;
                HitHorse(u, closing * (b.Type.Class == UnitClass.Skirmisher ? 8 : 4.5f) * (1 + b.Mods.Brace), b);
                Sound(Cue.LanceImpact, u.Pos, priority: u.IsPlayer);
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
                Hit(u, t, (10 + closing * 4.2f * Terrain.GroundDefAt(t.Pos).Charge) * (u.IsPlayer ? (1 + 0.2f * RidingEdge) * (Perk("Lancer") ? 1.25f : 1) : 1) * (1 + u.Mods.Charge), false, lance: true);
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

    static Cue ShieldCue(Unit t) =>
        !t.Type.HasShield ? Cue.ParrySteel
        : t.Type.Look.Shield is "buckler" or "targe" ? Cue.BlockMetal
        : Cue.BlockWood;

    static Cue HitCue(Unit a, Unit t, float rawDmg, bool ranged, bool lance)
    {
        if (t.Type.Has("bones")) return Cue.BoneHit;
        if (ranged) return t.Type.Armor >= 9 ? Cue.ArrowPlate : Cue.ArrowFlesh;
        if (lance) return rawDmg >= 42 ? Cue.LanceShatter : Cue.LanceImpact;
        var w = a.Type.Look.Weapon;
        if (w is "mace" or "warhammer" or "club" or "staff") return t.Type.Armor >= 7 ? Cue.BluntPlate : Cue.BluntFlesh;
        var grip = Look.GripOf(w);
        if (grip is Grip.Pole or Grip.Lance && a.Swing == AttackDir.Thrust) return t.Type.Armor >= 11 ? Cue.Clash : Cue.PierceHit;
        if (grip == Grip.Two || w is "halberd" or "glaive") return Cue.SlashHeavy;
        return t.Type.Armor >= 10 ? Cue.Clash : Cue.SlashLight;
    }

    static Cue DeathCue(Unit t)
    {
        if (t.Type.Has("bones")) return Cue.BoneCollapse;
        return t.Type.Look.Face switch
        {
            "goblin" => Cue.GoblinShriek,
            "troll" => Cue.TrollRoar,
            "wolf" => Cue.WolfHowl,
            "bear" => Cue.BearRoar,
            "spider" => Cue.SpiderChitter,
            "wyvern" => Cue.WyvernScreech,
            _ => Cue.Death,
        };
    }

    static Cue LooseCue(string weapon, Grip kind) => kind switch
    {
        Grip.Crossbow => Cue.CrossbowSnap,
        Grip.Sling => Cue.SlingCrack,
        Grip.Thrown => Cue.JavelinCast,
        _ => weapon == "longbow" ? Cue.LongbowLoose : Cue.Bow,
    };
}
