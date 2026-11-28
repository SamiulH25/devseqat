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
        float l = tier <= 1 ? 14 : 10 + 15 * (tier - 1);
        return c switch
        {
            UnitClass.Caster => new(l * 0.5f, 0, 0),
            UnitClass.Cavalry => shoots ? new(l * 0.5f, l, l) : new(l * 0.75f, l, 0),
            _ => shoots ? new(l * 0.6f, 0, l) : new(l, 0, 0),
        };
    }

    /// A shooter's sidearm (a knife or short sword) as a weapon's damage: about half an arming sword's 30.
    public const float Sidearm = 16;

    /// Set `t`'s battle numbers from base, skills and kit. `quality` scales a piece's own numbers (the captain's well-made gear, C9).
    public static void Build(UnitType t, FighterBase b, FighterSkills s, FighterKit k, Func<GearDef, float>? quality = null)
    {
        float Q(GearDef g) => quality?.Invoke(g) ?? 1;
        var w = k.Weapon;
        bool shoots = w.Ammo > 0;
        float swing = b.Swing + s.Arms * b.SwingPerArms;
        t.MaxHp = b.Hp * (1 + s.Arms * b.HpPerArms);
        t.Damage = w.Damage * Q(w) * (shoots ? b.Shot + s.Bowcraft * b.ShotPerBowcraft : b.Damage + s.Arms * b.DamagePerArms);
        t.SidearmDamage = shoots ? Sidearm * (b.Damage + s.Arms * b.DamagePerArms) : 0; // a knife or short sword, wielded with his Arms
        (t.Reach, t.Windup, t.Recover, t.Pierce, t.VsMounted) = (shoots ? w.Reach : w.Reach * b.Reach, w.Windup * swing, w.Recover * swing, w.Pierce, w.VsMounted);
        t.Ammo = w.Ammo;
        t.Range = shoots ? w.Range * (b.Range + s.Bowcraft * b.RangePerBowcraft) : w.Range;
        t.Reload = shoots ? w.Reload * (b.Reload + s.Bowcraft * b.ReloadPerBowcraft) : w.Reload;
        float guard = k.Shield.Look == "none" ? k.Shield.Block * w.Parry : k.Shield.Block * Q(k.Shield); // no shield: the weapon parries
        t.BlockChance = MathF.Min(0.95f, guard * (b.Block + s.Arms * b.BlockPerArms));
        t.Armor = k.Armour.Sum(g => g.Armor * Q(g));
        t.Hits = Armour.TryBlow(w.Hits, out var hits) ? hits : BlowKind.Cut;
        t.Coat = Armour.TryCoat(k.Body.Kind, out var coat) ? coat : CoatKind.Cloth;
        t.Speed = b.Speed + k.Body.Speed + k.Helm.Speed + k.Shield.Speed;
        if (t.Mount != null) t.Mount = t.Mount with { Hp = t.Mount.Hp * (1 + s.Riding * b.HorsePerRiding) };
    }

    /// Short summary of what one piece of kit gives (KT6: promotion screen, troop details).
    public static string PieceEffect(GearDef g) => g.Slot switch
    {
        "weapon" when g.Ranged => $"DMG {g.Damage:0}  RNG {g.Range:0}" + (g.Pierce > 0 ? $"  PRC {g.Pierce:P0}" : ""),
        "weapon" => $"DMG {g.Damage:0}  RCH {g.Reach:0.0}" + (g.Pierce > 0 ? $"  PRC {g.Pierce:P0}" : g.VsMounted > 1.05f ? $"  VS HORSE x{g.VsMounted:0.0}" : ""),
        "shield" => g.Look == "none" ? "" : $"BLK {g.Block:P0}" + (g.Speed < -0.01f ? $"  SPD {g.Speed:+0.#;-0.#}" : ""),
        _ => $"ARM {g.Armor:0.#}" + (MathF.Abs(g.Speed) > 0.01f ? $"  SPD {g.Speed:+0.##;-0.##}" : ""),
    };

    /// The pieces a soldier wears (weapon, shield if carried, body, helm if worn, plus armoured legs/feet/hands) with what each gives.
    public static List<(string Slot, GearDef Piece, string Effect)> KitList(UnitType t)
    {
        var list = new List<(string, GearDef, string)>();
        if (t.Wild || FighterKit.Of(t.Look) is not { } k) return list;
        list.Add(("WEAPON", k.Weapon, PieceEffect(k.Weapon)));
        if (k.Shield.Look != "none") list.Add(("SHIELD", k.Shield, PieceEffect(k.Shield)));
        list.Add(("BODY", k.Body, PieceEffect(k.Body)));
        if (k.Helm.Look != "none") list.Add(("HELM", k.Helm, PieceEffect(k.Helm)));
        if (k.Legs.Armor > 0) list.Add(("LEGS", k.Legs, PieceEffect(k.Legs)));
        if (k.Feet.Armor > 0) list.Add(("FEET", k.Feet, PieceEffect(k.Feet)));
        if (k.Hands.Armor > 0) list.Add(("HANDS", k.Hands, PieceEffect(k.Hands)));
        return list;
    }

    /// One-line list of a soldier's visible kit for hover cards (KT6).
    public static string KitLine(UnitType t)
    {
        var parts = KitList(t).Select(x => x.Piece.Name.ToUpperInvariant()).ToList();
        if (t.Mount != null) parts.Add(t.Mount.Name.ToUpperInvariant());
        return string.Join(", ", parts);
    }

    /// A soldier's three skills as one line (KT6).
    public static string SkillsLine(UnitType t) => t.Wild ? "" : $"ARMS {t.Skills.Arms:0}  RIDING {t.Skills.Riding:0}  BOW {t.Skills.Bowcraft:0}";

    /// What changes in kit and mount on promotion from `from` to `to` (KT6: e.g. "+ MAIL HAUBERK: ARM +4.5").
    public static List<(string Text, int Sign)> KitChanges(UnitType from, UnitType to)
    {
        var list = new List<(string, int)>();
        if (from.Wild || to.Wild || FighterKit.Of(from.Look) is not { } a || FighterKit.Of(to.Look) is not { } b) return list;
        if (a.Weapon.Id != b.Weapon.Id)
        {
            float dd = b.Weapon.Damage - a.Weapon.Damage, dr = b.Weapon.Reach - a.Weapon.Reach;
            string d = MathF.Abs(dd) >= 0.5f ? $": DMG {dd:+0;-0}" : MathF.Abs(dr) >= 0.05f ? $": RCH {dr:+0.0;-0.0}" : "";
            list.Add(($"+ {b.Weapon.Name.ToUpperInvariant()}{d}", dd > 0.5f ? 1 : dd < -0.5f ? -1 : 0));
        }
        if (a.Shield.Id != b.Shield.Id)
        {
            if (b.Shield.Look == "none") list.Add(($"- {a.Shield.Name.ToUpperInvariant()}", -1));
            else
            {
                float db = a.Shield.Look == "none" ? b.Shield.Block : b.Shield.Block - a.Shield.Block;
                list.Add(($"+ {b.Shield.Name.ToUpperInvariant()}: BLK {(a.Shield.Look == "none" ? "" : db >= 0 ? "+" : "")}{db * 100:0}%", db >= 0 ? 1 : -1));
            }
        }
        foreach (var (pa, pb) in new[] { (a.Body, b.Body), (a.Helm, b.Helm), (a.Legs, b.Legs), (a.Feet, b.Feet), (a.Hands, b.Hands) })
        {
            if (pa.Id == pb.Id) continue;
            float da = pb.Armor - pa.Armor;
            if (pb.Look == "none") list.Add(($"- {pa.Name.ToUpperInvariant()}: ARM {da:0.#}", -1));
            else if (MathF.Abs(da) >= 0.05f) list.Add(($"+ {pb.Name.ToUpperInvariant()}: ARM {da:+0.#;-0.#}", da > 0 ? 1 : -1));
            else list.Add(($"+ {pb.Name.ToUpperInvariant()}", 0));
        }
        if (from.Mount?.Name != to.Mount?.Name)
        {
            if (to.Mount != null && from.Mount == null) list.Add(($"+ {to.Mount.Name.ToUpperInvariant()}", 1));
            else if (to.Mount != null && from.Mount != null)
            {
                float dh = to.Mount.Hp - from.Mount.Hp;
                list.Add(($"+ {to.Mount.Name.ToUpperInvariant()}" + (MathF.Abs(dh) >= 1 ? $": HP {dh:+0;-0}" : ""), dh >= 0 ? 1 : -1));
            }
            else if (from.Mount != null) list.Add(($"- {from.Mount.Name.ToUpperInvariant()}", -1));
        }
        return list;
    }
}
