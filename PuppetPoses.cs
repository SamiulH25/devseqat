using Microsoft.Xna.Framework;

namespace Ironfield;

/// The poses: where a man's limbs and weapon are at each moment, from his action state and how he holds his weapon
/// (docs/archive/VISUALS.md V2, docs/ANIMATION_ROSTER.md). Headless and drawing only: the sim never reads a pose.
sealed partial class Puppet
{
    /// A body pose: leg swing, weapon arm (alpha, weapon pitch, swing-plane yaw), shield arm, the lean (degrees, x right, z forward),
    /// the bob (metres), and for archers how far the string is drawn.
    internal record struct Pose(float Leg, float ArmA, float WeapB, float Yaw, float OffA, bool ShieldUp, bool Aim,
        float LeanX = 0, float LeanZ = 0, float Bob = 0, float Draw = 0, bool Armed = true)
    {
        /// The off hand holds the weapon too, below the weapon hand.
        public bool TwoHand { get; init; }
    }
    internal record struct Key(float Alpha, float Pitch, float Yaw);

    static Key Lerp(Key x, Key y, float t) =>
        new(MathHelper.Lerp(x.Alpha, y.Alpha, t), MathHelper.Lerp(x.Pitch, y.Pitch, t), MathHelper.Lerp(x.Yaw, y.Yaw, t));

    /// Whether a man holds his weapon in both hands: two-handed weapons always, a polearm when he has no shield; never in the saddle.
    internal static bool TwoHanded(Grip grip, bool shield, bool mounted) =>
        !mounted && (grip is Grip.Two || grip is Grip.Pole && !shield);

    /// Key poses (idle, raised, struck) for how the weapon is held and the swing. A rider's sword cuts down from the saddle
    /// whatever the sim's swing (a thrust drawn as a cut, or a backhand when `backhand`).
    internal static (Key idle, Key raise, Key strike) Keys(Grip grip, AttackDir swing, bool mounted = false, bool shield = true, bool backhand = false)
    {
        if (grip is Grip.Lance || mounted && grip is Grip.Pole)
            return (new(15, 80, 0), new(-25, 5, 0), new(65, -5, 0)); // couched or overarm, it always thrusts
        if (mounted && grip is Grip.One or Grip.Two)
        {
            var rest = new Key(25, 70, 0);
            return swing == AttackDir.Overhead
                ? (rest, new(165, 150, 0), new(60, -50, 0)) // a straight chop down at a man on foot
                : backhand
                    ? (rest, new(125, 110, -70), new(70, -35, 45)) // the backhand across the horse's neck
                    : (rest, new(130, 120, 65), new(70, -40, -35)); // the cut down and across
        }
        if (grip is Grip.Pole)
        {
            if (shield)
                return swing == AttackDir.Overhead
                    ? (new(15, 80, 0), new(150, -10, 0), new(110, -12, 0)) // overarm over the shield rim, point down
                    : (new(15, 80, 0), new(-25, 5, 0), new(65, -5, 0)); // underarm
            var upright = new Key(25, 80, -20); // both hands on the shaft, point up
            return swing == AttackDir.Overhead
                ? (upright, new(150, 130, -10), new(75, -8, -10)) // the chop: a halberd or glaive comes down on a helm
                : swing == AttackDir.Thrust
                    ? (upright, new(5, 5, -15), new(75, 0, -10)) // drawn back level, then driven in
                    : (upright, new(10, 10, swing == AttackDir.Right ? 40 : -60), new(70, 0, swing == AttackDir.Right ? -30 : 20)); // a thrust slanting in
        }
        if (grip is Grip.Two or Grip.Staff)
        {
            var guard = new Key(35, 55, -25); // across the body, point up and forward
            return swing switch
            {
                AttackDir.Right => (guard, new(110, 35, 85), new(80, -15, -65)),
                AttackDir.Left => (guard, new(125, 30, -80), new(80, -15, 60)),
                AttackDir.Thrust => (guard, new(25, 0, -15), new(95, -5, -5)),
                _ => (guard, new(175, 165, -10), new(65, -50, -10)),
            };
        }
        var idle = new Key(20, 60, 0);
        return swing switch
        {
            AttackDir.Right => (idle, new(95, 15, 75), new(85, 0, -45)),
            AttackDir.Left => (idle, new(115, 20, -75), new(85, 0, 50)),
            AttackDir.Thrust => (idle, new(35, 5, 10), new(95, 0, 0)),
            _ => (idle, new(160, 150, 0), new(75, -35, 0)),
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
        float leanZ = moving ? 3 + 7 * run : 0, leanX = 0;
        var t = u.Type;
        if (!moving && !u.Mounted)
        {
            bob = MathF.Sin(now * 1.7f + phase * 9) * 0.008f; // breathing
            leanX = MathF.Sin(now * 0.31f + phase * 17) * 1.6f; // shifting his weight
        }
        if (t.Size > 1.5f && moving) leanX += stride * 5; // a troll or an ogre lumbers from foot to foot
        float hurt = u.HitFlash > 0 ? Ease.Kick(1 - u.HitFlash / 0.15f) : 0; // a flinch away from the blow
        leanZ -= hurt * 12;
        if (u.Routing)
            return new Pose(leg * 1.2f, 150 + swing * 2, 0, 0, 140 - swing * 2, false, false, leanX, 14, bob, 0, false); // arms up, running for his life
        if (winner >= 0 && u.Team == winner && !moving && u.Action == UnitAction.None && now > 0)
        {
            // The battle is won: the men raise their weapons and cheer, each in his own time.
            float c = MathF.Max(0, MathF.Sin(now * 4.5f + phase * 6.3f));
            return new Pose(0, 140 + c * 30, 60 + c * 40, 0, t.HasShield ? 40 + c * 50 : 20, false, false, 0, -4 * c, c * 0.06f);
        }
        var grip = Look.GripOf(t.Look.Weapon);
        if (t.Class == UnitClass.Archer)
        {
            bool aim = u.Action == UnitAction.Recover || (u.Ammo > 0 && u.EnemyDist < t.Range && !moving);
            if (!aim) return new Pose(leg, 15 + swing - leg * 0.3f, 0, 0, 10 - swing + leg * 0.3f, false, false, leanX, leanZ, bob);
            if (u.Action == UnitAction.Recover) return new Pose(leg, 100, 0, 0, 92, false, true, leanX, leanZ - 2, bob, 0); // the release: the hand flies back
            float draw = Ease.InOut(1 - u.Cooldown / 1.1f); // nock low, then draw as the next shot comes up
            return new Pose(leg, 62 + 28 * draw, 0, 0, 88, false, true, leanX, leanZ - 3 * draw, bob, draw);
        }
        bool two = TwoHanded(grip, t.HasShield, u.Mounted);
        var (idle, raise, strike) = Keys(grip, u.Swing, u.Mounted, t.HasShield, phase > 0.5f);
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
                arm = two ? new(55, 15, -60) : new(30, 70, 0); // a two-hander holds his haft across, a shield man raises it
                shield = t.HasShield; leanZ -= 3;
                break;
            case UnitAction.Stagger:
            {
                arm = new(-10 + 25 * hurt, 90, 0);
                leanZ -= 10 + MathF.Sin(now * 9 + phase) * 3; // stumbling back
                break;
            }
            default:
                if (u.Charging) { arm = Lerp(idle, raise, Ease.Out(u.Charge)); leanZ -= 4 * u.Charge; }
                else if (moving) arm = arm with { Alpha = arm.Alpha + swing * (two ? 0.25f : 0.6f) };
                break;
        }
        if (t.Look.Body == "bones") arm = arm with { Alpha = arm.Alpha + MathF.Sin(now * 23 + phase * 40) * 3 }; // dry joints rattle
        return new Pose(leg, arm.Alpha, arm.Pitch, arm.Yaw, shield ? 85 : 15 + leg * 0.4f - swing * 0.6f, shield, false, leanX, leanZ, bob)
        { TwoHand = two && u.Action != UnitAction.Stagger };
    }
}
