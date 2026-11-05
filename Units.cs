using Microsoft.Xna.Framework;

namespace Ironfield;

public enum UnitClass { Infantry, Archer, Skirmisher, Captain, Cavalry }
public enum UnitAction { None, Windup, Recover, Block, Stagger }

/// Swing direction, named from the attacker's side (Left = the blade comes from the attacker's left).
/// The defender sees Left/Right mirrored, see <see cref="Dirs.Mirror"/>.
public enum AttackDir { Overhead, Thrust, Left, Right }

/// Cavalry charge cycle: ride in, (bog down and fight), ride out, turn, ride in again.
public enum CavState { Charge, Melee, Withdraw }

public static class Dirs
{
    public static AttackDir Mirror(AttackDir d) => d switch { AttackDir.Left => AttackDir.Right, AttackDir.Right => AttackDir.Left, _ => d };
}

/// A horse. Speeds in m/s, Accel in m/s^2, TurnRate in rad/s at a standstill (it falls as speed rises).
public sealed record Mount(string Name, float Hp, float Trot, float Gallop, float Accel, float TurnRate)
{
    public static readonly Mount Warhorse = new("Warhorse", 140, 5.2f, 11.5f, 4.2f, 1.7f);
    public static readonly Mount Courser = new("Courser", 120, 5.8f, 12.5f, 5.0f, 2.1f);
}

// shortcut: troop stats live in code; move to JSON data files once the campaign layer needs troop trees.
public sealed record UnitType(string Name, UnitClass Class, float MaxHp, float Damage, float Armor,
    float Reach, float Windup, float Recover, float Speed, float BlockChance, float Range, int Ammo,
    Mount? Mount = null, float VsMounted = 1)
{
    public static readonly UnitType Shieldman = new("Shieldmen", UnitClass.Infantry, 100, 22, 6, 1.7f, 0.45f, 0.6f, 3.6f, 0.55f, 0, 0);
    public static readonly UnitType Bowman = new("Bowmen", UnitClass.Archer, 70, 14, 2, 1.5f, 0.4f, 0.7f, 3.9f, 0.1f, 34, 30);
    public static readonly UnitType Spearrunner = new("Spearrunners", UnitClass.Skirmisher, 80, 26, 3, 2.3f, 0.35f, 0.7f, 5.0f, 0.15f, 0, 0, VsMounted: 1.8f);
    public static readonly UnitType Lancer = new("Lancers", UnitClass.Cavalry, 90, 20, 7, 2.6f, 0.4f, 0.8f, 3.8f, 0.2f, 0, 0, Mount.Warhorse);
    public static readonly UnitType Captain = new("Captain", UnitClass.Captain, 220, 30, 10, 2.1f, 0.4f, 0.5f, 4.4f, 0.6f, 0, 0);

    public bool HasShield => Class is UnitClass.Infantry or UnitClass.Captain;
}

/// One soldier (or the player). Positions are on the ground plane: Pos.X = world X, Pos.Y = world Z.
public sealed class Unit
{
    public UnitType Type = null!;
    public int Team;
    public Squad? Squad;
    public bool IsPlayer, IsLeader;
    public Vector2 Pos, Vel, Wish, Slot;
    public float Facing, ViewFacing; // radians on the ground plane; ViewFacing is the eased value the renderer draws
    public float Hp, Morale = 70, Stamina = 100, EnemyDist = float.MaxValue;
    public int Ammo;
    public UnitAction Action;
    public AttackDir Swing;
    public float ActionTimer, Cooldown, Charge, Power = 1, HitFlash, Anim;
    public bool Charging; // player holding the attack button
    public Unit? Target;
    public bool Dead, Fled, Routing;

    // Mounted state. The horse moves along Heading at Speed; the rider looks (and strikes) along Facing.
    public Mount? Horse;
    public float HorseHp, Heading, ViewHeading, Speed;
    public bool Couched, Contact;          // lance lowered; this charge has reached the enemy
    public CavState Cav;
    public float CavTimer, Bogged, ImpactCd, BraceCd;

    public bool Active => !Dead && !Fled;
    public bool Mounted => Horse != null;
    public Vector2 Dir => new(MathF.Cos(Facing), MathF.Sin(Facing));
    public Vector2 HeadingDir => new(MathF.Cos(Heading), MathF.Sin(Heading));
    public void Face(Vector2 d) { if (d.LengthSquared() > 1e-6f) Facing = MathF.Atan2(d.Y, d.X); }
}

/// A riderless horse standing on the field: the player can mount it (H).
public sealed class LooseHorse
{
    public Mount Mount = null!;
    public Vector2 Pos;
    public float Heading, Hp;
    public int Team;
}
