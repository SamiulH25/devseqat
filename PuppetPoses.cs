using Microsoft.Xna.Framework;

namespace Ironfield;

/// The poses: where a man's limbs and weapon are at each moment, from his action state and how he holds his weapon
/// (docs/archive/VISUALS.md V2, docs/archive/ANIMATION_ROSTER.md). Headless and drawing only: the sim never reads a pose.
sealed partial class Puppet
{
    /// A body pose: leg swing, weapon arm (alpha, weapon pitch, swing-plane yaw), shield arm, the lean (degrees, x right, z forward),
    /// the bob (metres), and for archers how far the string is drawn.
    internal record struct Pose(float Leg, float ArmA, float WeapB, float Yaw, float OffA, bool ShieldUp, bool Aim,
        float LeanX = 0, float LeanZ = 0, float Bob = 0, float Draw = 0, bool Armed = true)
    {
        /// The off hand holds the weapon too, below the weapon hand.
        public bool TwoHand { get; init; }
        /// A crossbowman spanning: how far up the stock his hands have hauled the string (0..1), else -1.
        public float Haul { get; init; } = -1;
        /// A slinger whirls the sling over his head.
        public bool Whirl { get; init; }
        /// The javelin or stone has left his hand.
        public bool Empty { get; init; }
        /// A caster gathering a spell (0..1 up to the release), then 1..2 through the release; 0 when not casting.
        public float Cast { get; init; }
        /// How far he bends his knees (0 standing, 1 deep): the body sinks so his feet stay on the ground.
        public float Crouch { get; init; }
        /// Down on one knee (a front-rank shooter).
        public bool Kneel { get; init; }
        /// The head turned from the body (degrees, + to his right): a man looking round.
        public float Look { get; init; }
        /// How far out the weapon hand is (1 the arm straight, less with the elbow bent); the off hand's the same.
        public float Reach { get; init; } = 0.88f;
        public float OffReach { get; init; } = 0.88f;
        /// The shoulders turned (degrees, + toward his right); NaN: follow the weapon arm (Puppet.TwistOf).
        public float Twist { get; init; } = float.NaN;
        /// How much the knee folds as the foot comes through behind (more at a run).
        public float Knees { get; init; } = 0.7f;
    }
    /// A weapon-arm key: the arm's swing (alpha up from hanging, yaw the swing plane turned to his right), the weapon's pitch
    /// (0 level forward, 90 up, past 90 back over him), and how far out the hand is.
    internal record struct Key(float Alpha, float Pitch, float Yaw, float Reach = 0.88f);

    static Key Lerp(Key x, Key y, float t) =>
        new(MathHelper.Lerp(x.Alpha, y.Alpha, t), MathHelper.Lerp(x.Pitch, y.Pitch, t), MathHelper.Lerp(x.Yaw, y.Yaw, t), MathHelper.Lerp(x.Reach, y.Reach, t));

    /// The shoulders follow the weapon arm: swung out to his right the body turns that way, across to his left it turns back.
    internal static float TwistOf(Pose p) => float.IsNaN(p.Twist) ? Math.Clamp(p.Yaw * (p.TwoHand ? 0.5f : 0.38f), -32, 32) : p.Twist;

    /// Whether a man holds his weapon in both hands: two-handed weapons always, a polearm when he has no shield; never in the saddle.
    internal static bool TwoHanded(Grip grip, bool shield, bool mounted) =>
        !mounted && (grip is Grip.Two || grip is Grip.Pole && !shield);

    /// Key poses (idle, raised, struck) for how the weapon is held and the swing. A rider's sword cuts down from the saddle
    /// whatever the sim's swing (a thrust drawn as a cut, or a backhand when `backhand`).
    internal static (Key idle, Key raise, Key strike) Keys(Grip grip, AttackDir swing, bool mounted = false, bool shield = true, bool backhand = false)
    {
        if (grip is Grip.Lance || mounted && grip is Grip.Pole)
            return (new(20, 80, 0, 0.8f), new(10, 5, 15, 0.5f), new(70, -5, 0, 1)); // couched or overarm, it always thrusts
        if (mounted && grip is Grip.One or Grip.Two)
        {
            var rest = new Key(30, 70, 0, 0.8f);
            return swing == AttackDir.Overhead
                ? (rest, new(160, 140, 10, 0.6f), new(60, -50, 0, 1)) // a straight chop down at a man on foot
                : backhand
                    ? (rest, new(120, 120, -70, 0.55f), new(75, -35, 45, 1)) // the backhand across the horse's neck
                    : (rest, new(120, 125, 70, 0.6f), new(75, -40, -35, 1)); // the cut down and across
        }
        if (grip is Grip.Pole)
        {
            if (shield)
                return swing == AttackDir.Overhead
                    ? (new(20, 80, 0, 0.8f), new(150, -10, 15, 0.55f), new(110, -12, 0, 1)) // overarm over the shield rim, point down
                    : (new(20, 80, 0, 0.8f), new(15, 5, 20, 0.45f), new(70, -5, 0, 1)); // underarm, drawn back then driven
            var upright = new Key(30, 80, -20, 0.75f); // both hands on the shaft, point up
            return swing == AttackDir.Overhead
                ? (upright, new(150, 130, -10, 0.6f), new(75, -8, -10, 1)) // the chop: a halberd or glaive comes down on a helm
                : swing == AttackDir.Thrust
                    ? (upright, new(35, 3, 25, 0.45f), new(80, 0, -10, 1)) // drawn back level at the hip, then driven in
                    : (upright, new(35, 8, swing == AttackDir.Right ? 45 : -45, 0.45f), new(75, 0, swing == AttackDir.Right ? -25 : 20, 1)); // a thrust slanting in
        }
        if (grip is Grip.Staff)
        {
            var planted = new Key(25, 90, 0, 0.8f); // the staff's foot on the ground beside him
            return swing == AttackDir.Thrust
                ? (planted, new(35, 5, 20, 0.4f), new(92, 0, 0, 1))
                : (planted, new(160, 150, 0, 0.6f), new(75, -35, 0, 1)); // a staff knocks down
        }
        if (grip is Grip.Two)
        {
            var guard = new Key(45, 60, -20, 0.7f); // hands at the belly, the blade up and forward across the body
            return swing switch
            {
                AttackDir.Right => (guard, new(115, 115, 85, 0.55f), new(80, -10, -65, 1)), // cocked over the right shoulder, swept across
                AttackDir.Left => (guard, new(120, 115, -75, 0.55f), new(80, -10, 60, 1)),
                AttackDir.Thrust => (guard, new(40, 5, 15, 0.4f), new(90, -3, -5, 1)),
                _ => (guard, new(165, 160, -10, 0.55f), new(65, -50, -10, 1)), // raised over the head, brought straight down
            };
        }
        var idle = new Key(24, 40, 28, 0.82f); // the sword low at his right side, point up and out
        return swing switch
        {
            AttackDir.Right => (idle, new(115, 125, 80, 0.55f), new(80, -5, -50, 1)), // the blade cocked by the right ear, then swept across
            AttackDir.Left => (idle, new(115, 120, -70, 0.5f), new(85, 0, 50, 1)),    // drawn back over the left shoulder, a backhand
            AttackDir.Thrust => (idle, new(35, 5, 20, 0.35f), new(92, 0, 0, 1)),      // the hand drawn back to the hip, then driven out
            _ => (idle, new(160, 140, 10, 0.6f), new(72, -38, 0, 1)),                 // raised over the head, brought down
        };
    }

    /// Continuous pose from the unit's action state (docs/archive/VISUALS.md V2). `now` is the battle clock and `phase` a per-man
    /// 0..1 number, so idles and strides never move in step; `winner` is the team that has won (it cheers), or -1.
    /// Windups ease in and hold at the top; the strike snaps through and overshoots (follow-through); the body leans into it.
    internal static Pose PoseOf(Unit u, float now, float phase, int winner = -1)
    {
        float speed = u.Vel.Length();
        bool moving = speed > 0.32f;
        float run = Math.Clamp((speed - 2.2f) / 2.5f, 0, 1); // 0 walking, 1 running
        float stride = MathF.Sin(u.Anim * MathF.PI);
        float leg = moving && !u.Mounted ? stride * (24 + 12 * run) : 0;
        float swing = moving && !u.Mounted ? -stride * (10 + 14 * run) : 0; // arms swing against the legs
        float bob = moving && !u.Mounted ? MathF.Abs(stride) * (0.025f + 0.03f * run) : 0;
        float leanZ = moving ? 3 + 9 * run : 0, leanX = moving && !u.Mounted ? stride * (1.5f + run) : 0; // the hips sway over the planted foot
        float knees = 0.7f + 1.1f * run;
        var t = u.Type;
        if (!moving && !u.Mounted)
        {
            bob = MathF.Sin(now * 1.7f + phase * 9) * 0.008f; // breathing
            leanX = MathF.Sin(now * 0.31f + phase * 17) * 1.6f; // shifting his weight
        }
        if (t.Size > 1.5f && moving) leanX += stride * 5; // a troll or an ogre lumbers from foot to foot
        float turning = MathF.Abs(MathHelper.WrapAngle(u.Facing - u.ViewFacing));
        if (!moving && !u.Mounted && turning > 0.12f) leg = MathF.Sin(now * 14 + phase * 6) * 16 * MathF.Min(1, turning * 3); // he steps round, not slides
        float hurt = u.HitFlash > 0 ? Ease.Kick(1 - u.HitFlash / 0.15f) : 0; // a flinch away from the blow, the way it came
        var from = u.Pos - u.HitFrom;
        if (hurt > 0 && u.HitFrom != Vector2.Zero && from.LengthSquared() > 1e-4f)
        {
            from.Normalize();
            Vector2 f = new(MathF.Cos(u.ViewFacing), MathF.Sin(u.ViewFacing)), r = new(-f.Y, f.X);
            leanX += Vector2.Dot(from, r) * hurt * 14;
            leanZ += Vector2.Dot(from, f) * hurt * 12;
        }
        else leanZ -= hurt * 12;
        float unhorsed = (now - u.ThrownAt) / 1.2f;
        if (!u.Mounted && now > 0 && unhorsed >= 0 && unhorsed < 1)
        {
            // His horse fell under him (A6): pitched forward onto his hands, then back on his feet.
            float up = Ease.InOut(unhorsed);
            return new Pose(0, 115 - 70 * up, 0, 0, 115 - 70 * up, false, false, 0, 70 * (1 - up), 0, 0, unhorsed > 0.6f) { Crouch = 0.8f * (1 - up) };
        }
        if (u.Routing)
            return new Pose(leg * 1.2f, 150 + swing * 2, 0, 0, 140 - swing * 2, false, false, leanX, 14, bob, 0, false); // arms up, running for his life
        if (winner >= 0 && u.Team == winner && !moving && u.Action == UnitAction.None && now > 0)
        {
            // The battle is won: the men raise their weapons and cheer, each in his own time.
            float c = MathF.Max(0, MathF.Sin(now * 4.5f + phase * 6.3f));
            return new Pose(0, 140 + c * 30, 60 + c * 40, 0, t.HasShield ? 40 + c * 50 : 20, false, false, 0, -4 * c, c * 0.06f);
        }
        var grip = Look.GripOf(t.Look.Weapon);
        if (grip is Grip.Crossbow && !u.Mounted && t.Class == UnitClass.Archer) return CrossbowPose(u, now, phase, moving, leg, swing, leanX, leanZ, bob);
        if (grip is Grip.Thrown or Grip.Sling && !u.Mounted && ThrowPose(u, now, phase, grip, moving, leg, leanX, leanZ, bob) is { } thrown) return thrown;
        if (t.Class == UnitClass.Caster && CastPose(u, now, leg, leanX, leanZ, bob) is { } cast) return cast;
        if (t.Class == UnitClass.Archer)
        {
            bool aim = u.Action == UnitAction.Recover || (u.Ammo > 0 && u.EnemyDist < t.Range && !moving);
            if (!aim) return Life(u, new Pose(leg, 15 + swing - leg * 0.3f, 0, 0, 10 - swing + leg * 0.3f, false, false, leanX, leanZ, bob), now, phase, moving);
            if (u.Mounted) leanZ -= 9 * MathF.Max(0, -MathF.Cos(MathHelper.WrapAngle(u.ViewFacing - u.ViewHeading))); // shooting back over the horse's tail, he sits back
            bool kneel = FrontOfShooters(u, moving);
            if (u.Action == UnitAction.Recover) return new Pose(leg, 100, 0, 0, 92, false, true, leanX, leanZ - 2, bob, 0) { Kneel = kneel }; // the release: the hand flies back
            float draw = Ease.InOut(1 - u.Cooldown / 1.1f); // nock low, then draw as the next shot comes up
            return new Pose(leg, 62 + 28 * draw, 0, 0, 88, false, true, leanX, leanZ - 3 * draw, bob, draw) { Kneel = kneel };
        }
        bool two = TwoHanded(grip, t.HasShield, u.Mounted);
        var (idle, raise, strike) = grip is Grip.Thrown
            ? Keys(Grip.Pole, u.Swing, u.Mounted, true, phase > 0.5f) // out of javelins, he fights with the last one like a spear
            : Keys(grip, u.Swing, u.Mounted, t.HasShield, phase > 0.5f);
        idle = idle with { Alpha = idle.Alpha + (phase - 0.5f) * 14 + MathF.Sin(now * 0.4f + phase * 11) * 3 }; // each man rests his weapon his own way
        var arm = idle;
        bool shield = false;
        float lean = two ? 1.6f : 1; // a two-handed blow throws the whole body into it
        float side = u.Swing switch { AttackDir.Right => 1, AttackDir.Left => -1, _ => 0 };
        switch (u.Action)
        {
            case UnitAction.Windup:
            {
                float k = 1 - u.ActionTimer / (u.IsPlayer ? (u.Swing == AttackDir.Overhead ? 0.2f : 0.15f) : t.Windup);
                if (u.IsPlayer) arm = Lerp(raise, strike, Ease.In(k)); // player already raised while charging: the blow accelerates
                else
                {
                    float up = Ease.InOut(k / 0.85f); // held at the top for the last moment
                    arm = Lerp(idle, raise, up);
                    leanZ -= 5 * up * lean;
                    if (two) leanX += 6 * side * up; // wound up to the side the blow comes from
                }
                break;
            }
            case UnitAction.Recover:
            {
                float k = 1 - u.ActionTimer / (u.IsPlayer ? 0.3f : t.Recover);
                const float hit = 0.25f;
                float f;
                if (!u.IsPlayer && k < hit) { f = Ease.OutBack(k / hit); arm = Lerp(raise, strike, f); }
                else { float back = Ease.InOut(u.IsPlayer ? k : (k - hit) / (1 - hit)); arm = Lerp(strike, idle, back); f = 1 - back; }
                leanZ += 9 * f * lean;
                if (two) leanX += 6 * side * (1 - 2 * Math.Clamp(f, 0, 1)); // the weight swings through to the far side
                break;
            }
            case UnitAction.Block:
                arm = two ? new(75, 20, -55, 0.65f) : t.HasShield ? new(45, 75, 10, 0.75f) : new(95, 70, -30, 0.7f); // a two-hander holds his haft across, a shield man raises it
                shield = t.HasShield; leanZ -= 3;
                break;
            case UnitAction.Stagger:
            {
                arm = new(10 + 25 * hurt, 90, 20, 0.75f);
                leanZ -= 10 + MathF.Sin(now * 9 + phase) * 3; // stumbling back
                break;
            }
            default:
                if (u.Charging) { arm = Lerp(idle, raise, Ease.Out(u.Charge)); leanZ -= 4 * u.Charge; }
                else if (moving) arm = arm with { Alpha = arm.Alpha + swing * (two ? 0.25f : 0.6f) };
                else if (Idle(u, moving) && IdleOf(u.Index, now) is (1, var w) && w > 0)
                {
                    // At ease (A5): a pole stood on its butt, a two-hander leant on point down, a sword rested on the shoulder.
                    var rest = grip is Grip.Pole && two ? new Key(25, 90, 0, 0.8f) : two ? new Key(35, -80, 0, 0.75f) : new Key(80, 140, -25, 0.45f);
                    arm = Lerp(arm, rest, w);
                    leanX += 3 * w;
                }
                break;
        }
        if (t.Look.Body == "bones") arm = arm with { Alpha = arm.Alpha + MathF.Sin(now * 23 + phase * 40) * 3 }; // dry joints rattle
        float crouch = u.Action == UnitAction.Stagger ? 0.3f : 0;
        if (!moving && !u.Mounted && u.Action is UnitAction.None or UnitAction.Block && u.Squad is { } sq && t.Size < 1.5f)
        {
            // Stances in a line (docs/archive/ANIMATION_ROSTER.md A4): a shield wall crouches behind its shields, the first two ranks
            // of a pike or halberd company set their points at a foe close by while the ranks behind hold theirs up.
            if (sq.Formation == Formation.ShieldWall && t.HasShield) { shield = true; crouch = 0.35f; }
            else if (two && grip is Grip.Pole && u.Rank <= 1 && u.EnemyDist < 12 && u.Action == UnitAction.None)
            {
                float set = Math.Clamp((12 - u.EnemyDist) / 4, 0, 1);
                arm = Lerp(arm, new Key(40, 8, -12, 0.6f), set);
                crouch = 0.45f * set * (u.Rank == 0 ? 1 : 0.5f);
            }
        }
        if (u.Action == UnitAction.Recover && two) crouch = MathF.Max(crouch, 0.25f); // the weight goes down into a big blow
        return Life(u, new Pose(leg, arm.Alpha, arm.Pitch, arm.Yaw, shield ? 85 : 15 + leg * 0.4f - swing * 0.6f, shield, false, leanX, leanZ, bob)
        { TwoHand = two && u.Action != UnitAction.Stagger, Crouch = crouch, Reach = arm.Reach, Knees = knees }, now, phase, moving);
    }

    /// A man at ease: standing, on foot, doing nothing, with no foe within 25 m.
    static bool Idle(Unit u, bool moving) =>
        !moving && !u.Mounted && !u.IsPlayer && !u.Charging && u.Action == UnitAction.None && u.EnemyDist > 25 && !u.Routing && u.Type.Size < 1.5f;

    /// Idle life (docs/archive/ANIMATION_ROSTER.md A5): every 8 s each man may take up one of the idles, eased in and out:
    /// 1 his grip's rest, 2 a look round, 3 a roll of the shoulders (0: none). Returns the idle and how far into it (0..1).
    internal static (int Kind, float W) IdleOf(int index, float now)
    {
        const float Period = 8;
        float t = now / Period + Ease.Hash(index * 13 + 3) * 10;
        int slot = (int)MathF.Floor(t);
        float f = t - slot;
        int kind = (int)(Ease.Hash(index * 7919 + slot * 31) * 6);
        return (kind < 3 ? 0 : kind - 2, Ease.InOut(Math.Clamp(MathF.Min(f, 1 - f) * Period / 1.2f, 0, 1)));
    }

    /// The idles every kind of man shares: a look round and a roll of the shoulders.
    static Pose Life(Unit u, Pose p, float now, float phase, bool moving)
    {
        if (!Idle(u, moving)) return p;
        var (kind, w) = IdleOf(u.Index, now);
        if (kind == 2) return p with { Look = MathF.Sin(now * 0.8f + phase * 5) * 60 * w };
        if (kind == 3)
        {
            float roll = MathF.Sin(now * 3 + phase * 4) * w;
            return p with { ArmA = p.ArmA + roll * 8, OffA = p.OffA - roll * 8, LeanX = p.LeanX + roll * 2, Bob = p.Bob + MathF.Abs(roll) * 0.015f };
        }
        return p;
    }

    /// The front rank of a company of bows or crossbows with men behind it goes down on one knee to shoot.
    static bool FrontOfShooters(Unit u, bool moving) => !moving && !u.Mounted && u.Rank == 0 && u.Squad is { } sq && sq.Alive > sq.Columns && sq.Columns > 1;

    /// A crossbowman (docs/archive/ANIMATION_ROSTER.md A3): the bow levelled at the shoulder, a kick on the shot, then bent over it with
    /// a foot in the stirrup hauling the string up the stock, and over the last second raising it again. `Draw` is how level
    /// the bow is (0 nose down, 1 aimed); `Aim` holds it up.
    static Pose CrossbowPose(Unit u, float now, float phase, bool moving, float leg, float swing, float leanX, float leanZ, float bob)
    {
        var t = u.Type;
        bool shooting = u.Action == UnitAction.Recover || (u.Ammo > 0 && u.EnemyDist < t.Range && !moving);
        if (!shooting) return new Pose(leg, 15 + swing - leg * 0.3f, 0, 0, 10 - swing + leg * 0.3f, false, false, leanX, leanZ, bob);
        if (u.Action == UnitAction.Recover)
            return new Pose(leg, 0, 0, 0, 0, false, true, leanX, leanZ - 6 * Ease.Kick(1 - u.ActionTimer / 0.25f), bob, 1) { Kneel = FrontOfShooters(u, moving) }; // the kick
        if (u.Cooldown > 1)
        {
            // Spanning: two hands drag the string up from the stirrup, again and again, each man at his own pace.
            float haul = 0.5f - 0.5f * MathF.Cos(now * 3.2f + phase * 9);
            return new Pose(0, 0, 0, 0, 0, false, false, leanX * 0.3f, leanZ + 16 - 6 * haul, bob) { Haul = haul, Crouch = 0.3f + 0.15f * (1 - haul) };
        }
        float up = Ease.InOut(1 - MathF.Max(0, u.Cooldown)); // bolt laid; up to the shoulder
        return new Pose(leg, 0, 0, 0, 0, false, true, leanX, leanZ - 2 * up, bob, up) { Kneel = FrontOfShooters(u, moving) && up > 0.5f };
    }

    /// A javelin man or a slinger with something to throw at a foe in range: the javelin drawn back over the shoulder or the
    /// sling whirled overhead as the shot comes up, then the arm whips through (docs/archive/ANIMATION_ROSTER.md A3). Null when he is
    /// not throwing, so he walks or fights as usual.
    static Pose? ThrowPose(Unit u, float now, float phase, Grip grip, bool moving, float leg, float leanX, float leanZ, float bob)
    {
        var t = u.Type;
        bool released = u.Action == UnitAction.Recover && u.ActionTimer <= 0.25f && u.EnemyDist > 3;
        if (released)
        {
            float k = Ease.Out(1 - u.ActionTimer / 0.25f);
            return new Pose(leg, MathHelper.Lerp(200, 70, k), grip is Grip.Sling ? 20 : 10, 0, MathHelper.Lerp(110, 30, k), false, true, leanX, leanZ + 10 * k, bob)
            { Empty = grip is Grip.Thrown };
        }
        if (u.Ammo <= 0 || moving || u.Action != UnitAction.None || u.EnemyDist > t.Range || u.EnemyDist < 4) return null;
        float ready = Math.Clamp(1 - u.Cooldown / 0.8f, 0, 1);
        if (grip is Grip.Sling)
        {
            // The sling goes round faster as the shot comes; before that it hangs ready from the raised hand.
            return ready <= 0
                ? new Pose(0, 30, -90, 0, 20, false, false, leanX, leanZ, bob)
                : new Pose(0, 165, 0, 0, 40, false, true, leanX, leanZ - 3 * ready, bob, ready) { Whirl = true };
        }
        float back = Ease.InOut(ready); // the javelin drawn back level, the off arm pointing at the mark
        return new Pose(leg * 0.3f, MathHelper.Lerp(25, 205, back), MathHelper.Lerp(80, 8, back), 0, MathHelper.Lerp(20, 105, back), false, true,
            leanX, leanZ - 7 * back, bob);
    }

    /// A caster gathering a spell: the staff lifted high and the free hand reaching toward where it will land, leaning back
    /// from the weight of it; on the release the staff drives forward. Idle casters stand on their staffs. Null when he is not casting.
    static Pose? CastPose(Unit u, float now, float leg, float leanX, float leanZ, float bob)
    {
        if (u.CastFire < 0 || now < u.CastStart || now > u.CastFire + 0.4f || u.Action != UnitAction.None) return null;
        if (now < u.CastFire)
        {
            float k = Ease.Out(Math.Clamp((now - u.CastStart) / MathF.Max(0.1f, u.CastFire - u.CastStart), 0, 1));
            float tremble = MathF.Sin(now * 31) * 2 * k;
            return new Pose(leg, MathHelper.Lerp(22, 160, MathF.Min(1, k * 2.5f)) + tremble, MathHelper.Lerp(90, 105, k), 0, MathHelper.Lerp(15, 100, k), false, false,
                leanX, leanZ - 7 * k, bob + 0.02f * k) { Cast = k };
        }
        float f = Ease.OutBack((now - u.CastFire) / 0.4f);
        return new Pose(leg, MathHelper.Lerp(160, 105, f), MathHelper.Lerp(105, 15, f), 0, MathHelper.Lerp(100, 40, f), false, false, leanX, leanZ + 9 * f, bob) { Cast = 1 + f };
    }
}
