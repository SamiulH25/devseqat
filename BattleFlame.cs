using Microsoft.Xna.Framework;

namespace Ironfield;

/// The Black Flame in battle (docs/KNIGHT_ORDERS.md 7, K7): past 75 corruption the flame can take you for a few seconds.
/// You lose the reins: you turn on the nearest man, friend or foe, and cut at him. Drawn from the battle's seeded rng only.
public sealed partial class Battle
{
    public const float MadSeconds = 3;
    /// Until when the flame holds you (battle time), and how often it took you this battle.
    public float MadUntil = -1;
    public int MadSpells;
    public bool Mad => Time < MadUntil;
    readonly List<Unit> madNear = new();

    /// Each tick: the flame may take you (`Options.Madness` is the chance a second); while it holds you, your input is its own.
    PlayerInput Madness(Unit p, PlayerInput inp, float dt)
    {
        if (Options.Madness <= 0 || p.Dead) return inp;
        if (!Mad && rng.NextSingle() < Options.Madness * dt)
        {
            MadUntil = Time + MadSeconds;
            MadSpells++;
            Sound(Cue.Shout, p.Pos);
        }
        if (!Mad) return inp;
        var mad = new PlayerInput { Facing = p.Facing, Dir = AttackDir.Right };
        var t = Near(p.Pos, 12, madNear).Where(u => u != p && u.Active).OrderBy(u => Vector2.DistanceSquared(u.Pos, p.Pos)).ThenBy(u => u.Index).FirstOrDefault();
        if (t == null) return mad;
        var to = t.Pos - p.Pos;
        float d = to.Length();
        if (d > 0.01f) mad.Facing = MathF.Atan2(to.Y, to.X);
        bool close = d <= ReachOf(p) + 0.2f;
        if (!close && d > 0.01f) mad.Move = to / d;
        mad.Throttle = close ? 0 : 1;
        // Hold the swing and let it go once he is in reach and it is half drawn.
        mad.Attack = !(close && p.Charging && p.Charge >= 0.5f);
        return mad;
    }
}
