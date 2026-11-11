using Microsoft.Xna.Framework;

namespace Ironfield;

/// Caster: a mage (docs/archive/COMPANIONS_MAGIC.md E1): poor in melee, casts the spells in `UnitType.Spells` (BattleAbilities.cs).
public enum UnitClass { Infantry, Archer, Skirmisher, Captain, Cavalry, Caster }
public enum UnitAction { None, Windup, Recover, Block, Stagger }

/// Swing direction, named from the attacker's side (Left = the blade comes from the attacker's left).
/// The defender sees Left/Right mirrored, see <see cref="Dirs.Mirror"/>.
public enum AttackDir { Overhead, Thrust, Left, Right }

/// How the killing blow felled a man (drawing only): a plain fall away, spun round by a cut, folded over a thrust,
/// flung by a huge blow, or slid from the saddle.
public enum Fall { Plain, Cut, Thrust, Flung, Saddle }

/// How a weapon is held, which picks the man's poses (Puppet.Keys). Drawing only, except Thrown and Sling, which are shot.
public enum Grip { One, Two, Pole, Lance, Bow, Crossbow, Staff, Thrown, Sling }

/// Cavalry charge cycle: ride in, (bog down and fight), ride out, turn, ride in again.
public enum CavState { Charge, Melee, Withdraw }

public static class Dirs
{
    public static AttackDir Mirror(AttackDir d) => d switch { AttackDir.Left => AttackDir.Right, AttackDir.Right => AttackDir.Left, _ => d };
}

/// A horse. Speeds in m/s, Accel in m/s^2, TurnRate in rad/s at a standstill (it falls as speed rises).
/// Barding is what it wears: "saddle" (saddle cloth) or "caparison" (full cloth barding and a steel chanfron);
/// "wolf" is not a horse at all but a warg, drawn with the wolf rig (docs/archive/CREATURES.md).
/// "camel" is the horse rig in a sand coat with a hump (docs/archive/WORLD.md W4).
public sealed record Mount(string Id, string Name, float Hp, float Trot, float Gallop, float Accel, float TurnRate, string Barding)
{
    public static Mount Courser => Troops.Mount("courser");
    public static Mount Warhorse => Troops.Mount("warhorse");
}

/// What a troop wears and carries; every field names a sprite family in Art (e.g. Head "kettle" = "helm.kettle.f/s/b").
/// Face is whose head it is ("man", or a creature's: "goblin" = "head.goblin.f/s/b").
/// Visage is one man's own face (skin, hair, beard; see Variety), -1 for the troop's plain head. Drawing only.
public sealed record Look(string Head, string Body, string Legs, string Feet, string Hands, string Shield, string Weapon,
    bool Tabard, bool Plume, bool Cape, string Face = "man", int Visage = -1)
{
    /// "wolf", "bear", "spider" and "wyvern" are whole beasts: the puppet draws each with its own rig and sprites
    /// (w*, b*, s*, y*), ignoring the rest of the look.
    public static readonly string[] Faces = { "man", "goblin", "wolf", "troll", "skull", "ghoul", "bear", "spider", "wyvern" };
    public bool Beast => Face is "wolf" or "bear" or "spider" or "wyvern";
    /// The sprite family of the bare head. A man's own visage is painted once, in the neutral palette ("head.v12.f/n").
    public string HeadKey => Face != "man" ? "head." + Face : Visage >= 0 ? "head.v" + Visage : "head";
    public bool NeutralHead => Face == "man" && Visage >= 0;
    // "coif": a mail hood.
    public static readonly string[] Heads = { "none", "cap", "hood", "kettle", "nasal", "sallet", "greathelm", "spangen", "turban", "steppe", "coif", "spike", "barbute", "furcap", "bucket" };
    // Goblin kit: "rags" (bare green arms), "hide" (bare green legs), "bare" feet, "claw" hands.
    // Undead kit: "bones" (a ribcage and bare bone), "shroud" (a ghoul's grave wrappings over grey skin), "robe" (a necromancer's), "bone" feet and hands.
    // The new realms (docs/archive/WORLD.md W4): "lamellar" (rows of small plates), "deel" (a long wrapped steppe coat), "brigandine"
    // (cloth over riveted plates); the "turban" wound over a spiked cap, the fur-brimmed "steppe" helm; the tall "pavise".
    public static readonly string[] Bodies = { "tunic", "gambeson", "mail", "plate", "fur", "rags", "bones", "shroud", "robe", "lamellar", "deel", "brigandine" };
    public static readonly string[] LegsKinds = { "cloth", "mail", "plate", "hide", "bones", "shroud" };
    public static readonly string[] FeetKinds = { "boots", "sabatons", "bare", "bone" };
    public static readonly string[] HandsKinds = { "skin", "gauntlets", "claw", "bone" };
    public static readonly string[] Shields = { "none", "round", "kite", "heater", "pavise", "buckler", "targe", "dhal" };
    public static readonly string[] Weapons = { "sword", "longsword", "axe", "spear", "pike", "lance", "bow", "longbow", "crossbow", "greatsword", "daneaxe", "club", "staff", "mace",
        "halberd", "glaive", "javelin", "sling", "scimitar", "falchion", "warhammer" };
    public static bool Ranged(string weapon) => weapon is "bow" or "longbow" or "crossbow";
    /// Anything that is shot or thrown, so needs ammo: bows, javelins and slings.
    public static bool Missile(string weapon) => Ranged(weapon) || weapon is "javelin" or "sling";
    public static Grip GripOf(string weapon) => weapon switch
    {
        "greatsword" or "daneaxe" or "club" or "warhammer" => Grip.Two,
        "spear" or "pike" or "halberd" or "glaive" => Grip.Pole,
        "lance" => Grip.Lance,
        "bow" or "longbow" => Grip.Bow,
        "crossbow" => Grip.Crossbow,
        "staff" => Grip.Staff,
        "javelin" => Grip.Thrown,
        "sling" => Grip.Sling,
        _ => Grip.One,
    };
}

/// A troop type from Data/troops.json: stats, place in the upgrade tree, and look.
public sealed class UnitType
{
    public string Id = "", Name = "";
    /// A culture id from Data/factions.json, "common" (open to every culture) or "wild" (creatures).
    /// "wild" is a creature (docs/archive/CREATURES.md): never recruited from a village and outside every upgrade tree.
    public string Culture = "common";
    /// A knight order's signature troop (docs/archive/KNIGHT_ORDERS.md 4.1): its order id, "" for every other troop.
    public string Order = "";
    /// What a wild troop is ("goblin"); empty for men.
    public string Breed = "";
    public UnitClass Class;
    public int Tier, Cost, UpgradeXp;
    public string[] UpgradesTo = Array.Empty<string>();
    public float MaxHp, Damage, Armor, Reach, Windup, Recover, Speed, BlockChance, Range, VsMounted = 1, Pierce, Reload = 1;
    /// Damage against creatures (wild troops), like VsMounted against riders. Above 1, a monster's fear does not reach them.
    public float VsMonster = 1;
    /// Drill (docs/archive/MELEE.md D1) when the troop data sets it (0-1); -1: from its tier.
    public float Drill = -1;
    /// Drawn size (1 = a man). Goblins are small, bosses and trolls big.
    public float Size = 1;
    /// Creature traits (docs/archive/CREATURES.md): "craven" (breaks early, rallies again and again while its boss lives),
    /// "pack" (wolves: circle to a foe's flank before they bite), "boss" (his fall shakes every craven or pack
    /// creature of his side and stops their rallies: a goblin boss, a pack's alpha). A troll's: "sweep" (a blow hits
    /// the men beside its target too), "knockback" (throws men down and back), "fear" (unnerves foes near it),
    /// "regen" (heals when left alone a moment), "daylight" (slow and weak in sunlight, no healing), "fearless" (never routs).
    /// The undead's: "bones" (arrows do little to a skeleton), "raise" (a necromancer: raises the fallen around him
    /// to fight for him), "raised" (held up by a necromancer: falls apart when the last one of its side falls),
    /// "feed" (a ghoul: a kill heals it). The beasts' (docs/archive/MONSTERS.md): "poison" (a bite or sting that lands keeps
    /// hurting, through armour), "web" (a spider spits webs that pin the men they land on), "brood" (a brood mother lays
    /// spiderlings as she fights), "rage" (a wounded bear hits harder and faster), "flying" (a wyvern: out of reach of
    /// blades while aloft, it dives to strike and climbs away). Men's: "stakes" (longbowmen carry stakes to plant before their line, docs/archive/TERRAIN.md T6).
    public string[] Traits = Array.Empty<string>();
    public static readonly string[] KnownTraits = { "craven", "boss", "pack", "sweep", "knockback", "fear", "regen", "daylight", "fearless", "bones", "raised", "raise", "feed", "stakes",
        "poison", "web", "brood", "rage", "flying" };
    /// A body big enough to take more room, shove men aside and stand up to a horse (a troll).
    public bool Huge => Size >= 1.6f;
    /// Bound to its boss: loses heart when he falls.
    public bool Bound => Has("craven") || Has("pack");
    public bool Has(string trait) => Traits.Contains(trait);
    /// Has the "flying" trait (set once on load: the sim asks every tick).
    public bool Flying;
    public int Ammo;
    public bool Leader;
    /// A spear that only thrusts (the captain's boar spear): every swing is a thrust.
    public bool ThrustOnly;
    /// How this troop's blows land (docs/archive/COMPANIONS_MAGIC.md D2): tiers V-VII only, from the weapon unless troops.json
    /// names one (`attack`); StyleRank 1-3 for tiers V-VII (0: no style).
    public AttackStyle Style;
    public int StyleRank;
    /// Passive traits (effect ids, docs/archive/COMPANIONS_MAGIC.md D3): from the archetype unless troops.json names them
    /// (`passives`); weighed by tier (`Battle.PassiveWeight`). Mods = their fixed totals; HasAura: one reaches companies.
    public string[] Passives = Array.Empty<string>();
    public Mods Mods;
    public bool HasAura;
    /// A caster's spells (ability ids from Data/abilities.json, BattleAbilities.cs): tier IV knows one, V two, VI all three
    /// and its tradition's great working.
    public string[] Spells = Array.Empty<string>();
    public Mount? Mount;
    public Look Look = null!;

    public bool HasShield => Look.Shield != "none";
    public bool Wild => Culture == "wild";
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
    /// The company he started the battle in (his results go back to it even after it folds into another, Battle.Merge).
    public Squad? Home;
    public bool IsPlayer, IsLeader;
    /// A companion leading his company (F3): fights in its ranks but is not one of its men (not counted in its results).
    public bool Hero;
    /// Leads a division (drawn with a pennant; his fall leaves the division without orders for a while).
    public bool Sergeant;
    /// A named man of his company (docs/archive/LEGACY.md L2): his Soldier id, 0 for the rest.
    public int SoldierId;
    /// Enemies he has killed this battle, and whether one was a captain or a hero.
    public int Kills;
    public bool FelledLeader;
    /// A fallen named man you stood over (L8): seconds so far, and whether you got him out (wounded, not dead).
    public float Rescue;
    public bool Saved;
    public Vector2 Pos, Vel, Wish, Slot;
    public float Facing, ViewFacing; // radians on the ground plane; ViewFacing is the eased value the renderer draws
    public float Hp, Morale = 70, Stamina = 100, EnemyDist = float.MaxValue;
    /// Battle time of the last wound (a troll heals only when left alone).
    public float HurtAt = -99;
    /// A corpse a necromancer has raised: it walks again as a new man, so it is no longer drawn on the ground.
    public bool Raised;
    public int Ammo;
    public UnitAction Action;
    public AttackDir Swing;
    public float ActionTimer, Cooldown, Charge, Power = 1, HitFlash, Anim;
    /// Drawing only (the sim never reads them): when the cast this man is gathering began and fires, and where it goes.
    public float CastStart = -99, CastFire = -99;
    public Vector2 CastAim;
    public bool Charging; // player holding the attack button
    public float BlockStart; // when the player raised the guard (parries need fresh timing)
    public Unit? Target;
    public bool Dead, Fled, Routing;
    public int Routs;        // how many times this man has broken (a second rout is final)
    public float DiedAt;     // battle time of death (the fall is animated)
    public Vector2 DiedFrom; // where the killing blow came from (the body falls away from it; drawing only)
    /// Drawing only: how the killing blow felled him, where the last blow came from, when his horse fell under him.
    public Fall DiedFall;
    public Vector2 HitFrom;
    public float ThrownAt = -99;
    public int StuckArrows;  // arrows caught in the shield (drawn)
    public int Index;        // place in Battle.Units (the spatial grid orders pairs by it)

    // Mounted state. The horse moves along Heading at Speed; the rider looks (and strikes) along Facing.
    public Mount? Horse;
    public int Coat;                       // horse coat colour (index into Style.Coats)
    public int Palette;                    // heraldry to paint (Style.Sides index); the battle sets it per team
    public float HorseHp, Heading, ViewHeading, Speed;
    public bool Couched, Contact;          // lance lowered; this charge has reached the enemy
    public CavState Cav;
    public float CavTimer, Bogged, ImpactCd, BraceCd;
    /// Last tick's ground and slope on his pace (1 = level grass), and the slope along his way (rise a metre, negative downhill; BattleMove.cs).
    public float GroundPace = 1, Slope;
    /// His place in the company's ranks (BattleRanks.cs): kept for the battle, so a death moves only the man behind
    /// into the gap. Rank 0 is the front. -1: not placed (new, or back from a rout).
    public int Place = -1, Rank;
    /// A fixed 0-1 draw for this man: how readily he leaves the ranks when discipline is poor (low = first to go).
    public float Grit;
    /// His guard is up on purpose against a blow he saw coming (a drilled man's choice, BattleRanks.Guard): he blocks
    /// better, and strikes back at once if it holds.
    public bool Guard;
    /// Attack styles (BattleStyles.cs): blows struck so far (every third is a shield bash), and a riposte owed after a block.
    public int Blows;
    public bool RiposteReady;
    /// His effect totals: his troop's passives plus his company's auras (BattleEffects.cs, rebuilt every 0.5 s).
    public Mods Mods;
    /// Poison still to work (hp, dealt over a few seconds through armour) and who gave it (docs/archive/MONSTERS.md).
    public float Poison;
    public Unit? PoisonedBy;
    /// Caught in a spider's web until then: barely moves, barely guards. Spit: a spider's time until its next web.
    public float WebbedUntil = -99, Spit;
    /// A flyer's height above the ground (metres); Climbing: it has struck and is pulling away until ClimbUntil.
    public float Altitude, ClimbUntil;
    public bool Climbing;

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

/// A horse killed under its rider; `At` is the battle time it fell (the screen animates it going down).
public readonly record struct DeadHorse(Vector2 Pos, float Heading, int Team, int Coat, string Barding, float At = 0);
