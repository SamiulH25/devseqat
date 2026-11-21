namespace Ironfield;

/// A fighter's base (Data/gear.json `bases`, docs/pre-demo/KIT.md): his numbers before kit, and what each point of skill adds.
/// Factors are `X + skill * XPer...`: Arms for health, melee damage, block and swing speed; Bowcraft for missile damage,
/// reload and range; Riding for the horse's health.
public sealed class FighterBase
{
    public string Id = "";
    public float Hp, Speed, HpPerArms, Reach = 1, Damage = 1, DamagePerArms, Block = 1, BlockPerArms, Swing = 1, SwingPerArms,
        Shot = 1, ShotPerBowcraft, Reload = 1, ReloadPerBowcraft, Range = 1, RangePerBowcraft, HorsePerRiding;
}

/// A fighter's three skills (the captain's skills, `Skills.cs`; a troop's from troops.json `skills` or `Fighter.SkillsFor`).
public readonly record struct FighterSkills(float Arms, float Riding, float Bowcraft);

/// The pieces a man has on, one a slot (`Gear.Piece` by the sprite part he wears).
public sealed record FighterKit(GearDef Weapon, GearDef Shield, GearDef Body, GearDef Helm, GearDef Legs, GearDef Feet, GearDef Hands)
{
    public IEnumerable<GearDef> Armour => new[] { Body, Helm, Legs, Feet, Hands };
    public IEnumerable<GearDef> Pieces => new[] { Weapon, Shield, Body, Helm, Legs, Feet, Hands };
    /// What a man wearing this look has on; null if a part has no piece.
    public static FighterKit? Of(Look l) =>
        Gear.PieceOf("weapon", l.Weapon) is { } w && Gear.PieceOf("shield", l.Shield) is { } s && Gear.PieceOf("body", l.Body) is { } b
        && Gear.PieceOf("helm", l.Head) is { } h && Gear.PieceOf("legs", l.Legs) is { } lg && Gear.PieceOf("feet", l.Feet) is { } f
        && Gear.PieceOf("hands", l.Hands) is { } hd ? new(w, s, b, h, lg, f, hd) : null;
}

/// One builder for every fighting man's numbers: base + skills + kit (KIT.md). `Troops.Load` builds each soldier with it and
/// `Rules.CaptainType` the captain. Creatures (culture wild) keep the numbers typed in troops.json.
public static class Fighter
{
    /// The skills a troop has unless troops.json names them: 10 at tier I and 15 more a tier, in Arms for men who fight hand to hand,
    /// Bowcraft for men who shoot (with Arms at 0.6), Riding for horse (with Arms at 0.75: the horse carries some of the fight,
    /// a horse archer's Arms at 0.5), half the Arms for casters.
    public static FighterSkills SkillsFor(UnitClass c, int tier, bool shoots)
    {
        float l = 10 + 15 * Math.Max(0, tier - 1);
        return c switch
        {
            UnitClass.Caster => new(l * 0.5f, 0, 0),
            UnitClass.Cavalry => shoots ? new(l * 0.5f, l, l) : new(l * 0.75f, l, 0),
            _ => shoots ? new(l * 0.6f, 0, l) : new(l, 0, 0),
        };
    }

    /// Set `t`'s battle numbers from base, skills and kit. `quality` scales a piece's own numbers (the captain's well-made gear, C9).
    public static void Build(UnitType t, FighterBase b, FighterSkills s, FighterKit k, Func<GearDef, float>? quality = null)
    {
        float Q(GearDef g) => quality?.Invoke(g) ?? 1;
        var w = k.Weapon;
        bool shoots = w.Ammo > 0;
        float swing = b.Swing + s.Arms * b.SwingPerArms;
        t.MaxHp = b.Hp * (1 + s.Arms * b.HpPerArms);
        t.Damage = w.Damage * Q(w) * (shoots ? b.Shot + s.Bowcraft * b.ShotPerBowcraft : b.Damage + s.Arms * b.DamagePerArms);
        (t.Reach, t.Windup, t.Recover, t.Pierce, t.VsMounted) = (shoots ? w.Reach : w.Reach * b.Reach, w.Windup * swing, w.Recover * swing, w.Pierce, w.VsMounted);
        t.Ammo = w.Ammo;
        t.Range = shoots ? w.Range * (b.Range + s.Bowcraft * b.RangePerBowcraft) : w.Range;
        t.Reload = shoots ? w.Reload * (b.Reload + s.Bowcraft * b.ReloadPerBowcraft) : w.Reload;
        float guard = k.Shield.Look == "none" ? k.Shield.Block * w.Parry : k.Shield.Block * Q(k.Shield); // no shield: the weapon parries
        t.BlockChance = MathF.Min(0.95f, guard * (b.Block + s.Arms * b.BlockPerArms));
        t.Armor = k.Armour.Sum(g => g.Armor * Q(g));
        t.Speed = b.Speed + k.Body.Speed + k.Helm.Speed + k.Shield.Speed;
        if (t.Mount != null) t.Mount = t.Mount with { Hp = t.Mount.Hp * (1 + s.Riding * b.HorsePerRiding) };
    }
}
