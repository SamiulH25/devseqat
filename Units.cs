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
/// Barding is what it wears: "saddle" (saddle cloth) or "caparison" (full cloth barding and a steel chanfron).
public sealed record Mount(string Id, string Name, float Hp, float Trot, float Gallop, float Accel, float TurnRate, string Barding)
{
    public static Mount Courser => Troops.Mount("courser");
    public static Mount Warhorse => Troops.Mount("warhorse");
}

/// What a troop wears and carries; every field names a sprite family in Art (e.g. Head "kettle" = "helm.kettle.f/s/b").
public sealed record Look(string Head, string Body, string Legs, string Feet, string Hands, string Shield, string Weapon,
    bool Tabard, bool Plume, bool Cape)
{
    public static readonly string[] Heads = { "none", "cap", "hood", "kettle", "nasal", "sallet", "greathelm", "spangen" };
    public static readonly string[] Bodies = { "tunic", "gambeson", "mail", "plate", "fur" };
    public static readonly string[] LegsKinds = { "cloth", "mail", "plate" };
    public static readonly string[] FeetKinds = { "boots", "sabatons" };
    public static readonly string[] HandsKinds = { "skin", "gauntlets" };
    public static readonly string[] Shields = { "none", "round", "kite", "heater" };
    public static readonly string[] Weapons = { "sword", "longsword", "axe", "spear", "pike", "lance", "bow", "longbow", "crossbow", "greatsword", "daneaxe" };
    public static bool Ranged(string weapon) => weapon is "bow" or "longbow" or "crossbow";
}

/// A troop type from Data/troops.json: stats, place in the upgrade tree, and look.
public sealed class UnitType
{
    public string Id = "", Name = "";
    public UnitClass Class;
    public int Tier, Cost, UpgradeXp;
    public string[] UpgradesTo = Array.Empty<string>();
    public float MaxHp, Damage, Armor, Reach, Windup, Recover, Speed, BlockChance, Range, VsMounted = 1, Pierce, Reload = 1;
    public int Ammo;
    public bool Leader;
    public Mount? Mount;
    public Look Look = null!;

    public bool HasShield => Look.Shield != "none";
    public UnitType Clone() => (UnitType)MemberwiseClone();
    public override string ToString() => Name;

    // The troops the prototype battles and tests refer to by name.
    public static UnitType Shieldman => Troops.Get("shieldmen");
    public static UnitType Bowman => Troops.Get("bowmen");
    public static UnitType Spearrunner => Troops.Get("spearrunners");
    public static UnitType Lancer => Troops.Get("lancers");
    public static UnitType Captain => Troops.Get("captain");
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
    public float BlockStart; // when the player raised the guard (parries need fresh timing)
    public Unit? Target;
    public bool Dead, Fled, Routing;

    // Mounted state. The horse moves along Heading at Speed; the rider looks (and strikes) along Facing.
    public Mount? Horse;
    public int Coat;                       // horse coat colour (index into Style.Coats)
    public int Palette;                    // heraldry to paint (Style.Sides index); the battle sets it per team
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

/// A riderless horse standing on the field: the player can mount it.
public sealed class LooseHorse
{
    public Mount Mount = null!;
    public Vector2 Pos;
    public float Heading, Hp;
    public int Team, Coat;
}

public readonly record struct DeadHorse(Vector2 Pos, float Heading, int Team, int Coat, string Barding);
