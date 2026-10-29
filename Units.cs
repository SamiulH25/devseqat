using Microsoft.Xna.Framework;

namespace Ironfield;

public enum UnitClass { Infantry, Archer, Skirmisher, Captain }
public enum UnitAction { None, Windup, Recover, Block, Stagger }

// shortcut: troop stats live in code; move to JSON data files once the campaign layer needs troop trees.
public sealed record UnitType(string Name, UnitClass Class, float MaxHp, float Damage, float Armor,
    float Reach, float Windup, float Recover, float Speed, float BlockChance, float Range, int Ammo)
{
    public static readonly UnitType Shieldman = new("Shieldmen", UnitClass.Infantry, 100, 22, 6, 1.7f, 0.45f, 0.6f, 3.6f, 0.55f, 0, 0);
    public static readonly UnitType Bowman = new("Bowmen", UnitClass.Archer, 70, 14, 2, 1.5f, 0.4f, 0.7f, 3.9f, 0.1f, 34, 30);
    public static readonly UnitType Spearrunner = new("Spearrunners", UnitClass.Skirmisher, 80, 26, 3, 2.3f, 0.35f, 0.7f, 5.0f, 0.15f, 0, 0);
    public static readonly UnitType Captain = new("Captain", UnitClass.Captain, 220, 30, 10, 2.1f, 0.4f, 0.5f, 4.4f, 0.6f, 0, 0);
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
    public float ActionTimer, Cooldown, Charge, Power = 1, HitFlash, Anim;
    public bool Charging; // player holding the attack button
    public Unit? Target;
    public bool Dead, Fled, Routing;

    public bool Active => !Dead && !Fled;
    public Vector2 Dir => new(MathF.Cos(Facing), MathF.Sin(Facing));
    public void Face(Vector2 d) { if (d.LengthSquared() > 1e-6f) Facing = MathF.Atan2(d.Y, d.X); }
}
