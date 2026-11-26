using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the damage path (TickAction -> Strike -> Hit -> Kill), horses, arrows, braced spears and couched lances.
public partial class Battle
{
    /// CR2: Active swing duration by weapon weight (0.12s - 0.20s).
    static float ActiveTimeOf(Unit u)
    {
        float t = Look.GripOf(u.Type.Look.Weapon) switch
        {
            Grip.Two => 0.18f,
            Grip.Pole => u.Type.Look.Weapon is "pike" or "halberd" ? 0.19f : 0.16f,
            _ => u.Type.Look.Weapon is "sword" or "sword_viking" or "sword_knightly" or "scimitar" or "falchion" ? 0.13f : 0.15f,
        };
        return u.Swing == AttackDir.Thrust ? MathF.Max(0.12f, t - 0.02f) : t;
    }

    /// Two fidelity tiers (COMBAT_REWORK.md): full per-tick angular sweep in duels, for the player, and within 18m of the player.
    bool HighFidelity(Unit u) => Options.Duel || u.IsPlayer || (Player.Active && Vector2.DistanceSquared(u.Pos, Player.Pos) <= 18f * 18f);

    /// CR4: Weapon class helpers shared by both player and AI troops.
    internal static bool IsCrushWeapon(Unit a) => a.Type.Style == AttackStyle.Crush
        || a.Type.Look.Weapon is "axe" or "axe_bearded" or "daneaxe" or "daneaxe_bearded" or "mace" or "morningstar" or "warhammer" or "club" or "bardiche";

    internal static bool IsPoleWeapon(Unit a) => Look.GripOf(a.Type.Look.Weapon) == Grip.Pole;

    internal static bool IsTwoHandSword(Unit a) => !a.Type.HasShield
        && a.Type.Look.Weapon is "greatsword" or "greatsword_claymore" or "flamberge" or "longsword" or "longsword_estoc" or "longsword_gilded";

    internal static bool IsSwordAndShield(Unit a) => a.Type.HasShield
        && a.Type.Look.Weapon is "sword" or "sword_viking" or "sword_knightly" or "scimitar" or "falchion" or "longsword" or "longsword_gilded";

    /// CR3: Skill factor (0..1) from troop tier and squad drill governing guard-switch speed, parry window and stamina recovery.
    internal static float GuardSkill(Unit u) => Math.Clamp((u.Type.Tier - 1) / 5f * 0.65f + (u.Squad?.Drill ?? 0.45f) * 0.35f, 0f, 1f);

    /// CR3: Update an AI fighter's explicit GuardDir toward an incoming threat at tier/drill-scaled reaction speed.
    internal void UpdateGuardDir(Unit u, Unit? threat, float dt)
    {
        if (u.GuardSwitchCd > 0) u.GuardSwitchCd = MathF.Max(0, u.GuardSwitchCd - dt);
        if (threat == null || !threat.Active || u.Type.Look.Beast || u.Type.Huge || u.Type.BlockChance <= 0) return;
        bool incoming = threat.Action is UnitAction.Windup or UnitAction.Active || (threat.IsPlayer && threat.Charging);
        if (!incoming) return;
        if (threat.Target != u && !threat.IsPlayer) return;
        if (Vector2.DistanceSquared(u.Pos, threat.Pos) > 4.2f * 4.2f) return;

        float skill = GuardSkill(u);
        var want = Dirs.Mirror(threat.Swing);
        if (u.GuardDir != want && u.GuardSwitchCd <= 0)
        {
            u.GuardDir = want;
            u.BlockStart = Time;
            u.GuardSwitchCd = MathHelper.Lerp(0.82f, 0.16f, skill);
        }
        // Trained troops raise active guard against an incoming windup when not swinging.
        if (u.Action == UnitAction.None && u.Stamina > 18f && threat.Action == UnitAction.Windup
            && (u.Type.Tier >= 3 || (u.Type.HasShield && u.Type.Tier >= 2))
            && u.GuardDir == want && u.GuardSwitchCd <= MathHelper.Lerp(0.55f, 0.16f, skill))
        {
            u.Action = UnitAction.Block;
            u.ActionTimer = threat.ActionTimer + 0.18f;
            u.BlockStart = Time;
            u.Guard = true;
            u.Face(threat.Pos - u.Pos);
        }
    }
    static bool HeavyCleaver(Unit u) => u.Type.Look.Weapon is "greatsword" or "greatsword_claymore" or "flamberge" or "bardiche" or "glaive";

    static int CleaveCap(Unit u) => u.Swing switch
    {
        AttackDir.Thrust => u.Type.Style == AttackStyle.Thrust ? 2 : 1,
        AttackDir.Overhead => HeavyCleaver(u) ? 2 : 1,
        _ => HeavyCleaver(u) ? 3 : (u.IsPlayer ? 2 : 1),
    };

    void TickAction(Unit u, float dt)
    {
        if (u.Action == UnitAction.None || (u.IsPlayer && u.Action == UnitAction.Block)) return;
        if (u.Action == UnitAction.Active)
        {
            float prev = u.SwingProgress;
            u.ActionTimer -= dt;
            u.SwingProgress = Math.Clamp(1f - MathF.Max(0, u.ActionTimer) / MathF.Max(0.01f, u.ActiveDuration), 0f, 1f);
            TickActive(u, prev, u.SwingProgress);
            if (u.Action != UnitAction.Active) return; // clash, stagger, or cleave-stop ended Active early
            if (u.ActionTimer > 0) return;
            if (u.IsPlayer) riposteSwing = false;
            u.LastSwing = u.Swing;
            u.Action = UnitAction.Recover;
            float whiffMult = u.HitCount == 0 && IsCrushWeapon(u) ? 1.15f : 1f; // CR4: axes/maces have heavier whiff recovery
            u.ActionTimer = (u.IsPlayer ? 0.28f * u.Type.Recover / 0.5f : u.Mounted ? MathF.Min(u.Type.Recover, SaddleRecover) : u.Type.Recover) * whiffMult;
            return;
        }
        if ((u.ActionTimer -= dt) > 0) return;
        switch (u.Action)
        {
            case UnitAction.Windup:
                u.Action = UnitAction.Active;
                u.ActiveDuration = ActiveTimeOf(u);
                u.ActionTimer = u.ActiveDuration;
                u.SwingProgress = 0;
                u.ClearHits();
                if (!u.IsPlayer && !u.Mounted && !u.Type.Flying && HighFidelity(u) && u.DodgeTimer <= 0)
                {
                    u.LungeVel = u.Dir * 2.4f;
                    u.LungeTimer = 0.11f;
                }
                break;
            case UnitAction.Recover:
                u.Action = UnitAction.None;
                if (u.IsPlayer) lastRecovered = Time;
                if (!u.IsPlayer) u.Cooldown = MathF.Max(u.Cooldown, rng.NextSingle() * 0.45f); // desync the line
                break;
            default:
                u.Action = UnitAction.None;
                u.Guard = false;
                u.GuardBroken = false;
                break;
        }
    }

    void TickActive(Unit a, float prevProg, float curProg)
    {
        if (a.Type.Flying)
        {
            if (!a.MassSwung && curProg >= 0.45f) { a.MassSwung = true; Strike(a); }
            return;
        }
        if (!HighFidelity(a))
        {
            if (!a.MassSwung && curProg >= 0.45f) { a.MassSwung = true; Strike(a); }
            return;
        }
        SweepActive(a, prevProg, curProg);
    }

    /// CR2: Full per-tick angular sweep with accels/drags, sequential cleave, cleave-stop, and blade clashes/chambers.
    void SweepActive(Unit a, float prevProg, float curProg)
    {
        float baseReach = a.Mounted && !a.IsPlayer ? MathF.Min(a.Type.Reach, MountedMeleeReach) : ReachOf(a);
        float reach = a.Swing switch
        {
            AttackDir.Thrust => baseReach + (a.IsPlayer ? 0.7f : 0.45f),
            AttackDir.Overhead => baseReach + 0.35f,
            _ => baseReach + 0.35f,
        };
        if (a.Mounted) reach += 0.45f;

        float minAng, maxAng, effReach = reach;
        if (a.Swing == AttackDir.Thrust)
        {
            if (curProg < 0.12f || prevProg > 0.90f) return;
            minAng = -0.34f; maxAng = 0.34f;
            effReach = reach * MathHelper.Lerp(0.6f, 1.0f, Math.Clamp(curProg / 0.75f, 0f, 1f));
        }
        else if (a.Swing == AttackDir.Overhead)
        {
            if (curProg < 0.20f || prevProg > 0.92f) return;
            minAng = -0.48f; maxAng = 0.48f;
        }
        else
        {
            float halfArc = IsTwoHandSword(a) ? 1.28f : a.Mounted ? 1.25f : 1.12f; // CR4: greatswords & mounted cuts sweep wider
            float sign = a.Swing == AttackDir.Left ? 1f : -1f; // Left sweeps -halfArc -> +halfArc; Right sweeps +halfArc -> -halfArc
            float a0 = MathHelper.Lerp(-halfArc, halfArc, prevProg) * sign;
            float a1 = MathHelper.Lerp(-halfArc, halfArc, curProg) * sign;
            minAng = MathF.Min(a0, a1) - 0.32f;
            maxAng = MathF.Max(a0, a1) + 0.32f;
        }

        float dmg = StyleBlow(a, (a.IsPlayer ? a.Type.Damage : a.Type.MeleeDamage) * a.Power);
        if (a.Mounted && a.IsPlayer) dmg *= 1 + 0.1f * RidingEdge;
        int cap = CleaveCap(a);
        bool heavy = HeavyCleaver(a);

        // Deterministic candidate list sorted by distance then index.
        var near = Near(a.Pos, effReach + 1.2f, nearBrace);
        List<Unit>? hits = null;
        for (int i = 0; i < near.Count; i++)
        {
            var t = near[i];
            if ((t.Team == a.Team && !(a.IsPlayer && Mad)) || t == a || !t.Active || a.AlreadyHit(t.Index)) continue;
            var d = t.Pos - a.Pos;
            float len = d.Length();
            float targetReach = effReach + (t.Mounted ? 0.55f : 0f) + (a.Mounted ? 0.25f : 0f);
            if (len > targetReach) continue;
            if (len > 0.01f)
            {
                float rel = MathHelper.WrapAngle(MathF.Atan2(d.Y, d.X) - a.Facing);
                // Mounted cuts still obey swing direction (left cuts hit left, right cuts hit right) with a slight stirrup-side margin.
                float pad = a.Mounted ? 0.22f : 0f;
                if (rel < minAng - pad || rel > maxAng + pad) continue;
            }
            (hits ??= new List<Unit>(4)).Add(t);
        }
        if (hits == null) return;
        if (hits.Count > 1) hits.Sort((x, y) =>
        {
            int c = Vector2.DistanceSquared(x.Pos, a.Pos).CompareTo(Vector2.DistanceSquared(y.Pos, a.Pos));
            return c != 0 ? c : x.Index.CompareTo(y.Index);
        });

        for (int i = 0; i < hits.Count; i++)
        {
            var t = hits[i];
            if (!t.Active) continue;

            // Blade Clash / Chambering (CR2): intersecting swings from mirrored directions clash steel!
            if (!a.Type.Huge && !t.Type.Huge && !a.Type.Look.Beast && !t.Type.Look.Beast
                && t.Action is UnitAction.Active or UnitAction.Windup
                && t.Swing == Dirs.Mirror(a.Swing)
                && SideOf(a, t) == Side.Front && SideOf(t, a) == Side.Front)
            {
                TriggerClash(a, t);
                return;
            }

            float taper = a.HitCount switch { 0 => 1f, 1 => 0.72f, _ => 0.50f };
            a.RecordHit(t.Index);
            if (!a.IsPlayer)
            {
                if (a.Type.Look.Face == "troll") Sound(Cue.TrollSweep, a.Pos);
                else if (a.Type.Look.Face == "wolf") Sound(Cue.WolfSnarl, a.Pos);
                else if (a.Type.Look.Face == "bear") Sound(Cue.BearRoar, a.Pos);
                else if (a.Type.Look.Face == "spider") Sound(Cue.SpiderChitter, a.Pos);
            }
            bool stopped = Hit(a, t, dmg * taper, false);
            if (!a.IsPlayer && a.Type.Has("sweep") && a.HitCount == 1) Sweep(a, t, dmg, effReach + 0.3f);

            // Cleave-stop: 1H/axes stop on blocks or heavy armor unless charged; cap ends swing.
            bool armorStop = !heavy && (stopped || (t.Type.Armor >= 14f && a.Power < 1.35f));
            if (a.Action != UnitAction.Active || a.HitCount >= cap || armorStop)
            {
                if (a.Action == UnitAction.Active)
                {
                    if (a.IsPlayer) riposteSwing = false;
                    a.LastSwing = a.Swing;
                    a.Action = UnitAction.Recover;
                    a.ActionTimer = a.IsPlayer ? 0.28f * a.Type.Recover / 0.5f : a.Mounted ? MathF.Min(a.Type.Recover, SaddleRecover) : a.Type.Recover;
                }
                return;
            }
        }
    }

    /// CR2: Two mirrored swings collide -> steel ring, spark flash, recoil pushback, and fast counter-windup on chamber.
    void TriggerClash(Unit a, Unit t)
    {
        var mid = (a.Pos + t.Pos) * 0.5f;
        var sep = t.Pos - a.Pos;
        var n = sep.LengthSquared() > 1e-4f ? Vector2.Normalize(sep) : a.Dir;
        if (!a.Mounted) a.Pos -= n * 0.35f;
        if (!t.Mounted) t.Pos += n * 0.35f;
        a.ClashedAt = t.ClashedAt = Time;
        bool chamber = t.Action == UnitAction.Windup; // t swung into a's active blade = chamber!
        Sound(Cue.ParrySteel, mid, priority: a.IsPlayer || t.IsPlayer);
        Flash("clash", mid, 1.4f, a.Team, chamber ? "chamber" : "clash", n);
        if (chamber)
        {
            Stagger(a, 0.55f);
            t.ActionTimer = MathF.Min(t.ActionTimer, 0.06f); // fast counter-windup!
            if (t.IsPlayer) { RiposteUntil = Time + RiposteTime; Parries++; PlayerHit = ("CHAMBER! COUNTER", Time); }
            else { t.RiposteReady = true; if (a.IsPlayer) PlayerHit = ("CHAMBERED!", Time); }
        }
        else
        {
            // Mutual blade clash: both recoil briefly and reset to neutral.
            a.Action = UnitAction.Recover; a.ActionTimer = 0.18f;
            t.Action = UnitAction.Recover; t.ActionTimer = 0.18f;
            if (a.IsPlayer || t.IsPlayer) PlayerHit = ("BLADE CLASH!", Time);
        }
    }

    /// A rider's sword arm reaches about as far as a man's on foot: a troop's Reach is its lance or pike, not its blade.
    internal const float MountedMeleeReach = 1.9f;

    void Strike(Unit a)
    {
        float dmg = StyleBlow(a, (a.IsPlayer ? a.Type.Damage : a.Type.MeleeDamage) * a.Power); // a shooter hand to hand has only his sidearm
        if (!a.IsPlayer)
        {
            if (a.Type.Flying) { Sound(Cue.WyvernScreech, a.Pos); Climb(a); } // struck or missed, a wyvern climbs away to come again
            if (a.Target is not { Active: true } t) return;
            float extra = (t.Mounted ? 0.5f : 0) + (a.Mounted ? 0.5f : 0); // a horse's bulk stands foes off: both ways
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

    /// A rider in a standing fight with men on foot (not a lance charge): he strikes down from above, so his blow is
    /// harder to guard and lands heavier, and he fights with his sidearm at its pace, not the lance's.
    internal const float SaddleGuard = 0.75f, SaddleBlow = 1.15f, SaddleRecover = 0.6f;
    /// How far off an archer's lead may be, as a share of how far his mark moves while the arrow flies.
    internal const float LeadError = 0.32f;
    /// An arrow or bolt in a horse does this share of its damage: a big beast takes many before it falls (at full damage six
    /// longbow arrows killed a warhorse, and its rider was shot on foot).
    internal const float HorseArrow = 0.6f;

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
    /// Returns true if the blow was blocked, parried, or stopped cleanly.
    internal bool Hit(Unit a, Unit t, float dmg, bool ranged, bool lance = false, bool plunge = false, bool directRider = false)
    {
        if (a.Team == 1) dmg *= Options.EnemyDamage;
        if (OutOfReach(a, t, ranged)) { if (a.IsPlayer) PlayerHit = ("OUT OF REACH", Time); return true; } // a wyvern aloft (BattleMonsters.cs)
        if (ranged && Wall != null && Wall.Inside(t.Pos) && !Wall.Inside(a.Pos) && Terrain.Height(t.Pos) > Terrain.Height(a.Pos) + 2) dmg *= 0.6f; // merlons
        if (ranged && Engines.Count > 0 && Sheltered(t)) dmg *= 0.25f; // under a ram's roof, behind a tower (BattleEngines.cs)
        // Melee/unaimed strikes may hit the horse; aimed projectiles already resolved horse vs rider by impact height.
        if (t.Mounted && !directRider && rng.NextSingle() < (ranged ? 0.55f : a.Type.Class == UnitClass.Skirmisher ? 0.55f : 0.18f))
        {
            HitHorse(t, dmg * a.Type.VsMounted * (a.Squad?.Company?.VsMounted ?? 1) * (ranged ? HorseArrow : 1), a); // a horse carries many arrows before it falls
            return false;
        }

        // Testudo (D4): a halted company of shield men under arrows raises its shields all round.
        if (ranged && t.Mods.Testudo > 0 && t.Type.HasShield && !t.IsPlayer && t.Vel.LengthSquared() < 1 && rng.NextSingle() < t.Mods.Testudo)
        {
            if (t.StuckArrows < 4) t.StuckArrows++;
            Sound(Cue.ArrowShield, t.Pos);
            return true;
        }
        if (ranged && t.Mods.Veil > 0 && rng.NextSingle() < t.Mods.Veil) return true; // lost in mist or smoke, turned by a ward
        var side = t.Squad?.Formation == Formation.Square && !t.Routing ? Side.Front : SideOf(a, t); // a square has no flank
        bool facing = side == Side.Front;
        if (ranged && t.Squad?.Formation == Formation.Loose && rng.NextSingle() < 0.3f) return true; // open ranks: the arrow finds a gap
        if (t.IsPlayer)
        {
            if (facing && Guarding) // Unbroken Wall (K2): nothing from the front gets through
            {
                if (!ranged && !lance) Stagger(a, 0.8f);
                Sound(ranged ? Cue.ArrowShield : ShieldCue(t), t.Pos, priority: true);
                PlayerHit = ("UNBROKEN", Time);
                return true;
            }
            if (facing && t.Action == UnitAction.Block)
            {
                // Matching the incoming direction just as the blow comes is a parry: free, and it throws the attacker off balance.
                if (!ranged && !lance && !SelfGuard && BlockDir == Dirs.Mirror(a.Swing) && Time - t.BlockStart < ParryTime)
                {
                    Stagger(a, 0.9f);
                    t.Stamina = MathF.Max(0, t.Stamina - 2);
                    RiposteUntil = Time + RiposteTime * (Perk("Champion") ? 1.5f : 1); // the opening: a swing started now is quick and hard to stop (P2)
                    Parries++;
                    CounterStrike(t, a);
                    PlayerHit = (Countering ? "PARRY! COUNTER" : "PARRY! RIPOSTE", Time);
                    Sound(Cue.ParrySteel, t.Pos, priority: true);
                    Flash("parry", (a.Pos + t.Pos) * 0.5f, 1.2f, t.Team, "parry");
                    return true;
                }
                if (!t.Type.HasShield && !ranged && BlockDir != Dirs.Mirror(a.Swing))
                {
                    dmg *= 0.55f; // a two-handed guard only half-stops a blow from the wrong side
                    goto Struck;
                }
                float pCrush = !ranged && IsCrushWeapon(a) ? 1.6f : 1f; // CR3/CR4: axes & maces crush guard posture
                float pLance = lance ? 1.6f : 1f; // CR4: couched lance shatters shields
                t.Stamina -= (ranged ? 4 : dmg * 0.78f * pCrush * pLance) * (t.Type.HasShield ? 1f : 1.35f) * (Perk("IronGrip") ? 0.7f : 1f);
                if (t.Stamina <= 0) { t.Stamina = 0; Stagger(t, 0.9f, guardBreak: true); Sound(Cue.ShieldSplinter, t.Pos, priority: true); PlayerHit = ("GUARD BROKEN!", Time); Log("Guard broken!"); }
                else
                {
                    Sound(ranged ? Cue.ArrowShield : ShieldCue(t), t.Pos, priority: true);
                    if (!ranged && !lance) Stagger(a, 0.35f);
                }
                return true;
            }
        }
        else if (facing && t.Action is UnitAction.None or UnitAction.Block)
        {
            if (ranged)
            {
                // Ranged arrows/bolts: shields and formations still cover the front cone.
                float rChance = t.Type.BlockChance * (t.Type.HasShield ? 1f : 0.28f);
                if (Webbed(t)) rChance *= 0.4f;
                if (t.Squad?.Formation == Formation.ShieldWall && t.Type.HasShield && InOrder(t)) rChance = MathF.Min(0.9f, rChance * 1.8f);
                rChance *= StyleGuard(a, t, true, plunge);
                rChance = MathF.Min(0.95f, rChance * (1 + t.Mods.Block + (t.Vel.LengthSquared() < 1 ? t.Mods.BlockStill : 0)));
                if (rng.NextSingle() < rChance)
                {
                    t.Action = UnitAction.Block;
                    t.ActionTimer = 0.3f;
                    if (t.Type.HasShield && t.StuckArrows < 4) t.StuckArrows++;
                    Sound(Cue.ArrowShield, t.Pos, priority: a.IsPlayer);
                    return true;
                }
            }
            else if (!t.Type.Look.Beast && !t.Type.Huge && !Webbed(t) && (t.Action == UnitAction.Block || t.Type.BlockChance > 0))
            {
                // CR3: Unified explicit GuardDir + universal stamina & guard-break (replaces pure RNG melee blocking).
                bool riposteBypass = a.IsPlayer && !lance && riposteSwing && (t.Type.Tier <= 3 || Perk("Champion"));
                if (!riposteBypass)
                {
                    bool matched = t.GuardDir == Dirs.Mirror(a.Swing);
                    float skill = GuardSkill(t);
                    bool auraCut = (a.IsPlayer && AuraBlade) || FoeBlade(a, false);
                    float crushMult = (IsCrushWeapon(a) ? 1.6f : 1f) * (lance ? 1.6f : 1f) * (auraCut ? 1.5f : 1f) * (a.Type.Huge ? 2.0f : 1f);
                    bool wallLock = t.Squad?.Formation == Formation.ShieldWall && t.Type.HasShield && InOrder(t);

                    // Timed Parry: matching GuardDir freshly raised triggers a free parry + riposte opening!
                    float aiParryWin = 0.16f + 0.18f * skill;
                    if (matched && t.Action == UnitAction.Block && !lance && !a.Type.Huge && !auraCut
                        && Time - t.BlockStart <= aiParryWin && t.Type.Tier >= 3)
                    {
                        t.Stamina = MathF.Max(0, t.Stamina - 2f);
                        t.RiposteReady = true;
                        t.Cooldown = 0;
                        Stagger(a, 0.55f);
                        Sound(Cue.ParrySteel, t.Pos, priority: a.IsPlayer);
                        Flash("parry", (a.Pos + t.Pos) * 0.5f, 1.1f, t.Team, "parry");
                        if (a.IsPlayer) PlayerHit = ("PARRIED!", Time);
                        return true;
                    }

                    // Active Block always engages stamina/guard-break; idle (UnitAction.None) defenders only catch blows on
                    // a skill/shield reflex roll (so low-tier shields & unguarded flanks don't turtle-stalemate).
                    float styleBypass = StyleGuard(a, t, false, false);
                    bool passiveCatch = t.Action == UnitAction.None && t.Stamina > 12f && !lance && !a.Type.Huge
                        && rng.NextSingle() < MathF.Min(0.82f, t.Type.BlockChance * styleBypass * (wallLock ? 1.45f : 1f) * (matched ? 1.15f : 0.72f) * (1f + t.Mods.Block));

                    // Shielded defender: active guard covers front cone at stamina cost; idle defenders rely on reflex roll.
                    if (t.Type.HasShield && (t.Action == UnitAction.Block || passiveCatch))
                    {
                        float cost = dmg * 0.78f * crushMult * (matched ? 0.65f : 1f) * (wallLock ? 0.65f : 1f) / MathF.Max(0.75f, RankGuard(t));
                        t.Stamina -= cost;
                        if (t.Stamina <= 0)
                        {
                            t.Stamina = 0;
                            Stagger(t, 0.9f, guardBreak: true);
                            Sound(Cue.ShieldSplinter, t.Pos, priority: a.IsPlayer);
                            if (a.IsPlayer) PlayerHit = ("GUARD BREAK!", Time);
                            dmg *= 0.45f; // shattered guard lets partial blow through
                            goto Struck;
                        }
                        if (t.Guard) t.Cooldown = 0;
                        StyleBlocked(t);
                        t.Action = UnitAction.Block;
                        t.ActionTimer = 0.25f;
                        if (a.IsPlayer) PlayerHit = ("BLOCKED", Time);
                        Sound(ShieldCue(t), t.Pos, priority: a.IsPlayer);
                        return true;
                    }

                    // Unshielded / 2H defender: MUST match GuardDir to block cleanly; wrong-side guard takes heavy bleed-through.
                    if (!t.Type.HasShield)
                    {
                        if (matched && !auraCut && (t.Action == UnitAction.Block || (passiveCatch && t.Stamina > 15f)))
                        {
                            float cost = dmg * 0.85f * crushMult * (a.Mounted && !t.Mounted ? 1.25f : 1f);
                            t.Stamina -= cost;
                            if (t.Stamina <= 0)
                            {
                                t.Stamina = 0;
                                Stagger(t, 0.9f, guardBreak: true);
                                Sound(Cue.ShieldSplinter, t.Pos, priority: a.IsPlayer);
                                if (a.IsPlayer) PlayerHit = ("GUARD BREAK!", Time);
                                dmg *= 0.5f;
                                goto Struck;
                            }
                            if (t.Guard) t.Cooldown = 0;
                            StyleBlocked(t);
                            t.Action = UnitAction.Block;
                            t.ActionTimer = 0.25f;
                            if (a.IsPlayer) PlayerHit = ("BLOCKED", Time);
                            Sound(Cue.ParrySteel, t.Pos, priority: a.IsPlayer);
                            return true;
                        }
                        if (t.Action == UnitAction.Block && !matched)
                        {
                            dmg *= 0.7f; // wrong-side unshielded guard: heavy bleed-through
                            goto Struck;
                        }
                    }
                }
            }
        }

        Struck:
        // Chambering: striking into the enemy's blow from the mirrored side as it lands turns it aside.
        if (t.IsPlayer && !ranged && !lance && t.Action is UnitAction.Windup or UnitAction.Active && t.Swing == Dirs.Mirror(a.Swing))
        {
            Stagger(a, 1.0f);
            RiposteUntil = Time + RiposteTime;
            Parries++;
            PlayerHit = ("CHAMBER!", Time);
            Sound(Cue.ParrySteel, t.Pos, priority: true);
            Flash("clash", (a.Pos + t.Pos) * 0.5f, 1.3f, t.Team, "chamber");
            return true;
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
        // Division command (PF3): companies led by an active division officer fight more cohesively (more under a Companion or banner sergeant).
        if (a.Squad?.Division is { Leader.Active: true } div) officer *= div.Distinguished || div.Leader.Hero ? 1.08f : 1.05f;
        // Captain's Road identity (PF2): distinct personal fighting edge in battles and duels.
        if (a.IsPlayer)
        {
            officer *= Options.CaptainRoad switch
            {
                "sword" when !ranged && a.Type.Look.Shield is not ("" or "none") => 1.10f,
                "lance" when !ranged && (lance || a.Mounted) => 1.15f,
                "bow" when ranged => 1.15f,
                "great" when !ranged && a.Type.Look.Shield is "" or "none" => 1.12f,
                "pole" when !ranged => t.Mounted ? 1.20f : 1.08f,
                _ => 1f,
            };
        }
        float saddle = !ranged && !lance && a.Mounted && !a.IsPlayer && !t.Mounted ? SaddleBlow : 1; // struck down from horseback
        float mult = saddle * officer * (a.Avenging ? 1.1f : 1) * (t.Avenging ? 1.1f : 1) * (1 + a.VeteranBonus + (a.Armed ? 0.15f : 0)) * (1 + a.Mods.Damage + fury + headsman) * (1 + speed) * (1 + high) * (side == Side.Rear ? 1.5f : side == Side.Flank ? 1.2f : 1) * (t.Mounted ? a.Type.VsMounted * (a.Squad?.Company?.VsMounted ?? 1) : 1)
            * (t.Type.Wild ? a.Type.VsMonster : 1)  // monster hunters
            * (ranged && t.Type.Has("bones") ? 0.3f : 1) // arrows pass between a skeleton's ribs
            * (a.IsPlayer && AuraBlade && !ranged || FoeBlade(a, ranged) ? BladeDamage : 1) // the aura blade (K1; a champion's, K6)
            * (a.IsPlayer && edgeLeft > 0 && !ranged ? 1 + edgeK : 1) // Thorn Edge (K2)
            * ClashMult(a, t) // a clash of champions (MA7)
            * (a.Type.Wild ? BeastBlow(a) : 1); // a wounded bear's rage (BattleMonsters.cs)
        if (t.Squad != null) t.Squad.HitsTaken++;
        if (side != Side.Front && a.Squad != null && !ranged) a.Squad.FlankHits++;
        if (side != Side.Front && t.Squad != null) { t.Squad.Pressure += side == Side.Rear ? 1 : 0.5f; Flanked(t.Squad, side == Side.Rear); }
        // CR4: Weapon class damage & spacing mechanics (polearm tip sweet-spot, crush armor pen, polearm sweep pushback).
        float poleMult = 1f;
        bool poleSweet = false;
        if (!ranged && !lance && IsPoleWeapon(a))
        {
            float dist = Vector2.Distance(a.Pos, t.Pos);
            float r = ReachOf(a);
            if (a.Swing == AttackDir.Thrust && dist >= r * 0.62f) { poleMult = 1.22f; poleSweet = true; }
            else if (dist < r * 0.42f) poleMult = 0.75f; // crowded face-to-face: haft hits weaker
            else if (a.Swing != AttackDir.Thrust && !t.Mounted && !t.Type.Huge)
            {
                var push = t.Pos - a.Pos;
                if (push.LengthSquared() > 1e-4f) t.Pos += Vector2.Normalize(push) * 0.7f; // reset spacing!
            }
        }
        float crushPierce = !ranged && IsCrushWeapon(a) ? 0.15f : 0f;
        bool foeBlade = FoeBlade(a, ranged);
        float pierce = MathF.Min(0.9f, a.Type.Pierce + crushPierce + (a.IsPlayer && AuraBlade && !ranged || foeBlade ? BladePierce : 0) + (a.IsPlayer && edgeLeft > 0 && !ranged ? 0.9f : 0));
        float real = Damage(a, t, dmg * mult * poleMult, side == Side.Rear ? 4 : side == Side.Flank ? 1.5f : 0, pierce, ranged); // maces, axes, bolts and the aura blade bite through armour
        if (a.IsPlayer && !ranged && AuraBlade) BladeMarks(t); // breaks a working, leaves craft marks (MA5)
        if (a.IsPlayer && !ranged) BladeLanded();
        if (foeBlade) FoeAura = MathF.Max(0, FoeAura - BladeHitCost);
        if (!ranged && a.Type.Has("knockback")) Knock(a, t);
        if (!ranged && a.Type.Has("trample")) Knock(a, t, 1.8f, 1.3f, 2f); // WI5: charging boars bowl shield lines over
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
            if (poleSweet) text += "  SWEET SPOT";
            PlayerHit = (text, Time);
            if (Blows.Count > 0 && Blows[^1].Target == t && !Blows[^1].OnPlayer) // the number over him says why it bit
                Blows[^1] = Blows[^1] with { Tag = lance ? "LANCE" : poleSweet ? "SWEET SPOT" : side == Side.Rear ? "REAR" : side == Side.Flank ? "FLANK" : speed >= 0.1f ? "SPEED" : high >= 0.05f ? "HIGH GROUND" : "" };
        }
        return false;
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
        float armour = t.Type.Armor + t.Mods.Armour + (t.Armed ? 4 : 0) + (ranged ? t.Mods.ArrowArmour : 0); // iron skin, armed veteran, a pavise (BattleEffects.cs)
        float real = MathF.Max(dmg * 0.25f, dmg - armour * (1 - pierce)); // bolts punch through part of the armour
        real *= WildCombatMult(a, t, ranged); // WI5-WI6: spectral, silver_weak, damp, seize, burrow, trample
        if (a.IsPlayer || t.IsPlayer) real = AuraSoak(a, t, real, ranged); // aura armour (K1)
        if (FoeTier > 0 && (a == EnemyCaptain || t == EnemyCaptain)) real = FoeSoak(a, t, real, ranged); // a champion's (K6)
        t.Hp -= real;
        t.HitFlash = 0.15f;
        t.HitFrom = a.Pos;
        t.HurtAt = Time;
        t.Morale -= (real * 0.16f + extraShock * 0.7f) * (1 + a.Mods.ShockDealt); // a fearsome man's blows shake more
        // Hits interrupt swings, but not a rhythm he has read: past your second chained blow he swings through it (P2).
        // A rider's swing is not broken by a footman's blow: he strikes down from the saddle, and three men round his horse
        // otherwise kept every swing from landing.
        // CR4: 2H greatsword overhead has brief poise/hyper-armor in late windup & active so light blows don't interrupt it.
        bool poise = IsTwoHandSword(t) && t.Swing == AttackDir.Overhead && (t.Action == UnitAction.Active || (t.Action == UnitAction.Windup && t.ActionTimer < t.WindupDuration * 0.45f));
        if (!poise && (t.Action == UnitAction.Windup || t.Charging) && !(t.Mounted && !a.Mounted && t.Action == UnitAction.Windup)) Stagger(t, 0.3f);
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

    static void Stagger(Unit u, float time, bool guardBreak = false)
    {
        if (u.Type.Huge) return; // a troll's swing is not stopped by a man's blow or shield
        u.Action = UnitAction.Stagger;
        u.ActionTimer = time;
        u.GuardBroken = guardBreak;
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
        bool wasMounted = t.Mounted;
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
        NamedKill(t, killer, wasMounted); // their words (C6)
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
        if (u.Squad != null) u.Squad.LoosedAt = Time;
        u.Cooldown = (u.Type.Windup + u.Type.Recover + 0.8f + rng.NextSingle() * 0.8f) * u.Type.Reload * StyleReload(u) * (1 + u.Mods.Reload); // a crossbow takes long to span
        float spread = (u.Squad?.Company?.Has("Sharpshooter") == true ? 0.65f : 1) * MathF.Max(0.2f, 1 + u.Mods.Spread); // steady aim
        if (MirageFor(u) is { } m) { Loose(u, m.Pos, null, u.Type.Damage, spread); return; } // a mirage draws their arrows (MA8)
        Loose(u, t.Pos, t, u.Type.Damage, spread);
    }

    /// Manual 3D aimed shot (player): launches directly along camera yaw and elevation angle with real ballistic drop.
    void LooseAimed(Unit u, float yaw, float elev, float speed, float damage, float angSpread)
    {
        u.Ammo--;
        var kind = Look.GripOf(u.Type.Look.Weapon);
        Sound(LooseCue(u.Type.Look.Weapon, kind), u.Pos, priority: u.IsPlayer);
        u.Action = UnitAction.Recover;
        u.ActionTimer = 0.25f;
        float sy = yaw + Gauss() * angSpread;
        float se = elev + Gauss() * angSpread * 0.85f;
        float ce = MathF.Cos(se);
        var vel = new Vector3(MathF.Cos(sy) * ce * speed, MathF.Sin(se) * speed, MathF.Sin(sy) * ce * speed);
        float from = Terrain.Height(u.Pos) + (u.Mounted ? 2.25f : 1.55f);
        Arrows.Add(new Projectile
        {
            Pos = new(u.Pos.X, from, u.Pos.Y),
            Vel = vel,
            Shooter = u,
            Damage = damage,
            Kind = kind is Grip.Thrown or Grip.Sling or Grip.Crossbow ? kind : Grip.Bow,
        });
    }

    /// An arrow from u at a point (leading the target t if there is one), scattered by distance, tier skill and `spread`.
    void Loose(Unit u, Vector2 at, Unit? t, float damage, float spread)
    {
        u.Ammo--;
        if (u.Squad != null && Time - u.Squad.LooseAt > 2.5f) Sound(Cue.Volley, Vector2.Lerp(u.Pos, at, 0.5f)); // a volley whistles overhead
        if (u.Squad != null) u.Squad.LooseAt = Time;
        var kind = Look.GripOf(u.Type.Look.Weapon);
        Sound(LooseCue(u.Type.Look.Weapon, kind), u.Pos, priority: u.IsPlayer);
        u.Action = UnitAction.Recover; // shows the release pose
        u.ActionTimer = 0.25f;
        float spd = ProjectileSpeed(kind, u.Type.Look.Weapon);
        float tierSpread = u.Type.Tier switch { <= 1 => 1.65f, 2 => 1.35f, 3 => 1.10f, 4 => 0.90f, _ => 0.72f };
        float d = Vector2.Distance(u.Pos, at);
        var aim = at + (t?.Vel ?? Vector2.Zero) * (d / spd); // lead the target
        if (t != null) aim += new Vector2(Gauss(), Gauss()) * t.Vel.Length() * (d / spd) * LeadError * tierSpread; // fast marks are harder to lead
        float mountedPen = u.Mounted && !(u.IsPlayer && Perk("HorseArcher")) && u.Mods.Gallop <= 0 ? 1.55f : 1f;
        aim += new Vector2(Gauss(), Gauss()) * d * 0.052f * tierSpread * spread * mountedPen * Weather.Spread;
        float time = MathF.Max(0.1f, Vector2.Distance(u.Pos, aim) / spd * (1f + Gauss() * 0.035f * tierSpread));
        var h = (aim - u.Pos) / time;
        float from = Terrain.Height(u.Pos) + (u.Mounted ? 2.2f : 1.5f), land = Terrain.Height(aim) + (t?.Mounted == true ? 1.5f : 1.0f) + (t?.Altitude ?? 0); // up at a flyer
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
            float prevY = p.Pos.Y;
            var from = new Vector2(p.Pos.X, p.Pos.Z);
            p.Pos += p.Vel * dt;
            var flat = new Vector2(p.Pos.X, p.Pos.Z);
            float ground = Terrain.Height(flat);
            if (p.Pos.Y <= ground) { p.Pos.Y = ground; p.Stuck = 0; continue; }
            if (flyers && FlyerHit(flat, p.Pos.Y - ground, p.Shooter.Team) is Unit flier) { Hit(p.Shooter, flier, p.Damage, true); Arrows.RemoveAt(i); continue; } // a wyvern taken in the air
            if (MathF.Min(prevY, p.Pos.Y) > ground + 2.9f) continue;
            if (Covered(from, flat, p.Pos.Y - ground)) { p.Pos.Y = MathF.Max(ground, p.Pos.Y - 0.5f); p.Stuck = 0; continue; } // into a tree or a hedge
            var segMid = (from + flat) * 0.5f;
            float segSearch = Vector2.Distance(from, flat) * 0.5f + 1.2f;
            foreach (var t in Near(segMid, segSearch, nearArrow))
            {
                if (t.Team == p.Shooter.Team || !t.Active) continue;
                float r = t.Mounted ? 0.8f : 0.42f;
                if (DistToSegmentK(t.Pos, from, flat, out float hitK) > r) continue;
                float hitH = MathHelper.Lerp(prevY, p.Pos.Y, hitK) - Terrain.Height(t.Pos);
                float maxH = t.Mounted ? 2.85f : 2.08f;
                if (hitH < 0f || hitH > maxH) continue;
                if (CutsArrow(t, p.Vel)) { p.Pos.Y = ground; p.Stuck = 0; break; } // a Transcendent's blade cuts it from the air (MA7)
                // Height-based hit zones: low hits on cavalry strike the horse; high hits strike the head.
                if (t.Mounted && hitH < 1.38f)
                {
                    HitHorse(t, p.Damage * p.Shooter.Type.VsMounted * (p.Shooter.Squad?.Company?.VsMounted ?? 1f) * HorseArrow, p.Shooter);
                    Arrows.RemoveAt(i);
                    break;
                }
                bool headshot = t.Mounted ? hitH >= 2.28f : hitH >= 1.50f;
                float shotDmg = headshot ? p.Damage * 1.55f : p.Damage;
                bool plunge = Vector2.DistanceSquared(p.Shooter.Pos, flat) > Plunging * Plunging * p.Shooter.Type.Range * p.Shooter.Type.Range;
                if (headshot && p.Shooter.IsPlayer) PlayerHit = ("HEADSHOT!", Time);
                Hit(p.Shooter, t, shotDmg, true, plunge: plunge, directRider: t.Mounted);
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
        if (!anyMounted0 && !anyMounted1) return;
        var units = Units;
        for (int ui = 0; ui < units.Count; ui++)
        {
            var b = units[ui];
            if (!b.Active || b.BraceCd > 0) continue;
            if (b.Team == 0 ? !anyMounted1 : !anyMounted0) continue;
            if (!Braced(b)) continue;
            float reach = ReachOf(b) + BraceExtra * (1 + b.Mods.Brace); // set spears reach further
            float sr = reach + 0.5f;
            var bp = b.Pos;
            int x0 = CellOf(bp.X - sr), x1 = CellOf(bp.X + sr), z0 = CellOf(bp.Y - sr), z1 = CellOf(bp.Y + sr);
            bool hit = false;
            for (int z = z0; z <= z1 && !hit; z++)
            {
                int row = z * GridN;
                for (int x = x0; x <= x1 && !hit; x++)
                {
                    var cell = cells[row + x];
                    int cnt = cell.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        var u = cell[i];
                        if (!u.Active || !u.Mounted || u.Team == b.Team) continue;
                        if (u.IsPlayer && b.Type.Tier < 4 && Perk("KnightOfTheCharge")) continue; // their points do not stop your charge
                        var to = u.Pos - bp;
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
                        hit = true;
                        break;
                    }
                }
            }
        }
    }

    /// Couched lances: the point is a segment ahead of the horse; damage comes from the closing speed alone.
    void Lances()
    {
        if (!anyMounted0 && !anyMounted1) return;
        var units = Units;
        for (int ui = 0; ui < units.Count; ui++)
        {
            var u = units[ui];
            if (!u.Active || !u.Couched || !u.Mounted) continue;
            float aimOff = u.IsPlayer ? Math.Clamp(MathHelper.WrapAngle(u.Facing - u.Heading), -0.26f, 0.26f) : 0f;
            float lanceYaw = u.Heading + aimOff;
            var hd = new Vector2(MathF.Cos(lanceYaw), MathF.Sin(lanceYaw));
            Vector2 a = u.Pos + hd * LanceFrom, b = u.Pos + hd * LanceTip;
            var mid = (a + b) * 0.5f;
            int x0 = CellOf(mid.X - 2), x1 = CellOf(mid.X + 2), z0 = CellOf(mid.Y - 2), z1 = CellOf(mid.Y + 2);
            bool hit = false;
            for (int z = z0; z <= z1 && !hit; z++)
            {
                int row = z * GridN;
                for (int x = x0; x <= x1 && !hit; x++)
                {
                    var cell = cells[row + x];
                    int cnt = cell.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        var t = cell[i];
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
                        hit = true;
                        break;
                    }
                }
            }
        }
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b) => DistToSegmentK(p, a, b, out _);

    static float DistToSegmentK(Vector2 p, Vector2 a, Vector2 b, out float k)
    {
        var ab = b - a;
        float len2 = ab.LengthSquared();
        k = len2 < 1e-8f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
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
