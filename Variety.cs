using Microsoft.Xna.Framework;

namespace Ironfield;

/// Two men of the same troop need not look alike: each gets his own face (skin, hair, beard), and some a
/// different helm of the same kind or another one-handed weapon, and a little more or less height.
/// Drawing only: the sim never reads it (the troop's stats stay the troop's), so replays are untouched.
/// A named man keeps his looks from battle to battle (seeded by his Soldier id).
public static class Variety
{
    public static readonly Color[] Skins = { new(238, 198, 162), Style.Skin, new(196, 148, 106), new(146, 100, 68) };
    public static readonly Color[] Hairs = { new(38, 30, 28), new(70, 46, 30), Style.Hair, new(196, 158, 94), new(150, 72, 36), new(152, 148, 140) };
    /// 0 clean-shaven, 1 full beard, 2 moustache, 3 bald and bearded.
    public const int Styles = 4;
    public static int Visages => Skins.Length * Hairs.Length * Styles;
    public static (int Skin, int Hair, int Style) Parts(int visage) => (visage % Skins.Length, visage / Skins.Length % Hairs.Length, visage / (Skins.Length * Hairs.Length));

    // Helms and weapons that can stand in for one another (one family each). A realm's own helms (spangen, turban,
    // steppe) and the knightly ones stay as the troop has them.
    static readonly string[][] HelmKin =
    {
        new[] { "cap", "hood", "brigand_hat", "none" },
        new[] { "kettle", "kettle_brimmed", "nasal", "coif", "morion" },
        new[] { "sallet", "sallet_visored", "sallet_bevor", "sallet_crest", "bascinet", "armet" },
        new[] { "barbute", "barbute_crest" },
        new[] { "greathelm", "greathelm_sugarloaf", "greathelm_visor", "greathelm_horns", "greathelm_wings" },
        new[] { "spangen", "spangen_spectacle", "spangen_horns", "spangen_wolf", "gjermundbu" },
        new[] { "steppe", "steppe_mask", "steppe_horns" },
        new[] { "spike", "kulah" },
    };
    static readonly string[][] BodyKin =
    {
        new[] { "tunic", "leather", "gambeson", "gambeson_fur" },
        new[] { "mail", "mail_hauberk", "coatofplates", "brigandine", "brigandine_gilded" },
        new[] { "mail_fur", "fur" },
        new[] { "deel", "deel_fur" },
        new[] { "plate", "plate_fluted", "plate_surcoat", "plate_gilded", "breastplate" },
        new[] { "lamellar", "scale", "scale_iron", "lamellar_gilded" },
    };
    static readonly string[][] LegKin =
    {
        new[] { "cloth", "furlegs" },
        new[] { "mail", "splint" },
        new[] { "plate", "splint" },
    };
    static readonly string[][] BootKin =
    {
        new[] { "boots", "ridingboots", "furboots" },
    };
    static readonly string[][] ShieldKin =
    {
        new[] { "round", "round_bossed", "round_iron", "targe" },
        new[] { "heater", "heater_gilded", "heater_quartered", "kite" },
        new[] { "dhal", "kalkan" },
    };
    static readonly string[][] WeaponKin =
    {
        new[] { "sword", "sword_viking", "sword_knightly", "axe", "axe_bearded", "mace", "morningstar", "falchion" },
        new[] { "longsword", "longsword_estoc", "longsword_gilded" },
        new[] { "greatsword", "greatsword_claymore", "flamberge" },
        new[] { "daneaxe", "daneaxe_bearded", "bardiche" },
        new[] { "halberd", "billhook", "glaive" },
        new[] { "lance", "pennonlance" },
        new[] { "spear", "spear_winged" },
        new[] { "bow", "bow_horn", "recurve" },
        new[] { "crossbow", "crossbow_heavy", "arbalest" },
    };

    static readonly Dictionary<Look, Dictionary<int, Look>> cache = new(ReferenceEqualityComparer.Instance);

    /// The seed of one man's looks: his Soldier id if he is named, else his place in the battle.
    public static int Seed(Unit u) => u.SoldierId != 0 ? u.SoldierId * 7919 + 1 : u.Index;

    /// How a man of troop `t` looks; the player and creatures are drawn exactly as their look says.
    public static Look Of(Unit u)
    {
        if (u.IsPlayer || u.Type.Look.Face != "man") return u.Type.Look;
        if (u.RenderLook != null && ReferenceEquals(u.RenderLookFor, u.Type)) return u.RenderLook;
        u.RenderLookFor = u.Type;
        return u.RenderLook = Of(u.Type, Seed(u), keepKit: u.IsLeader || u.Hero);
    }

    public static Look Of(UnitType t, int seed, bool keepKit = false)
    {
        var look = t.Look;
        if (look.Face != "man") return look;
        int visage = Visage(t.Culture, seed);
        int helm = keepKit ? 0 : Pick(seed, 1, look.Head, HelmKin, 0.45f);
        int body = keepKit ? 0 : Pick(seed, 3, look.Body, BodyKin, 0.40f);
        int shield = keepKit ? 0 : Pick(seed, 4, look.Shield, ShieldKin, 0.40f);
        int legs = keepKit ? 0 : Pick(seed, 5, look.Legs, LegKin, 0.35f);
        int feet = keepKit ? 0 : Pick(seed, 6, look.Feet, BootKin, 0.40f);
        int weapon = keepKit ? 0 : Pick(seed, 2, look.Weapon, WeaponKin, 0.35f);
        int code = visage + Visages * (helm + 8 * (weapon + 10 * (body + 8 * (shield + 6 * (legs + 4 * feet)))));
        if (!cache.TryGetValue(look, out var byCode)) cache[look] = byCode = new();
        if (byCode.TryGetValue(code, out var v)) return v;
        v = look with
        {
            Visage = visage,
            Head = helm == 0 ? look.Head : Kin(HelmKin, look.Head)[helm - 1],
            Body = body == 0 ? look.Body : Kin(BodyKin, look.Body)[body - 1],
            Shield = shield == 0 ? look.Shield : Kin(ShieldKin, look.Shield)[shield - 1],
            Legs = legs == 0 ? look.Legs : Kin(LegKin, look.Legs)[legs - 1],
            Feet = feet == 0 ? look.Feet : Kin(BootKin, look.Feet)[feet - 1],
            Weapon = weapon == 0 ? look.Weapon : Kin(WeaponKin, look.Weapon)[weapon - 1],
        };
        return byCode[code] = v;
    }

    /// Drawn height, 0.95 to 1.05 of the troop's.
    public static float Stature(Unit u) => u.IsPlayer ? 1 : (u.RenderStature >= 0 ? u.RenderStature : (u.RenderStature = 0.95f + 0.1f * Ease.Hash(Seed(u) * 31 + 5)));

    static string[] Kin(string[][] families, string item) => families.FirstOrDefault(f => f.Contains(item)) ?? Array.Empty<string>();

    /// 0 = keep the troop's own, else 1 + the index of a kin item (which may be the same; then it is kept too).
    static int Pick(int seed, int salt, string item, string[][] families, float chance)
    {
        var kin = Kin(families, item);
        if (kin.Length == 0 || Ease.Hash(seed * 17 + salt * 101) >= chance) return 0;
        int i = (int)(Ease.Hash(seed * 23 + salt * 211) * kin.Length) % kin.Length;
        return kin[i] == item ? 0 : i + 1;
    }

    /// A face: skin leans by the troop's culture (fair in the north, darker in the south and on the steppe), hair by skin.
    internal static int Visage(string culture, int seed)
    {
        string baseCulture = culture.Contains('_') ? culture[..culture.IndexOf('_')] : culture;
        float[] skinW = baseCulture switch
        {
            "northern" => new[] { 5f, 4, 1, 0 },
            "southern" => new[] { 0f, 2, 4, 4 },
            "steppe" => new[] { 0f, 3, 5, 2 },
            _ => new[] { 2.5f, 4.5f, 2, 1 },
        };
        int skin = Weighted(skinW, Ease.Hash(seed * 13 + 7));
        float[] hairW = skin >= 2 ? new[] { 6f, 3, 1, 0, 0, 1 } : skin == 0 ? new[] { 1f, 2, 3, 3, 2, 1 } : new[] { 2f, 3, 3, 1, 1, 1 };
        int hair = Weighted(hairW, Ease.Hash(seed * 29 + 3));
        int style = Weighted(new[] { 4f, 3, 2, 1 }, Ease.Hash(seed * 41 + 11));
        return skin + Skins.Length * (hair + Hairs.Length * style);
    }

    static int Weighted(float[] w, float r)
    {
        float t = r * w.Sum();
        for (int i = 0; i < w.Length; i++) { t -= w[i]; if (t < 0) return i; }
        return w.Length - 1;
    }

    /// Modular facial features shared between 2D portraits (PortraitPainter) and 3D heads (Puppet3D).
    public readonly record struct FaceFeatures(int Cut, int Beard, int Eye, int Brow, int Nose, int Jaw, int Scar, bool Female = false, int Age = 26);

    /// Pack facial features into a non-negative int for `Look.Features` (zero heap allocation, value equality).
    public static int PackFeatures(int cut, int beard, int eye, int brow, int nose, int jaw, int scar, bool female = false) =>
        (Math.Clamp(cut, 0, 4) & 7)
        | ((Math.Clamp(beard, 0, 3) & 3) << 3)
        | ((Math.Clamp(eye, 0, 3) & 3) << 5)
        | ((Math.Clamp(brow, 0, 2) & 3) << 7)
        | ((Math.Clamp(nose, 0, 2) & 3) << 9)
        | ((Math.Clamp(jaw, 0, 2) & 3) << 11)
        | ((Math.Clamp(scar, 0, 2) & 3) << 13)
        | ((female ? 1 : 0) << 15);

    public static int PackFeatures(CaptainLook l) => PackFeatures(l.Cut, l.Beard, l.Eye, l.Brow, l.Nose, l.Jaw, l.Scar);

    public static FaceFeatures UnpackFeatures(int code, int age = 26) => new(
        Cut: Math.Clamp(code & 7, 0, 4),
        Beard: Math.Clamp((code >> 3) & 3, 0, 3),
        Eye: Math.Clamp((code >> 5) & 3, 0, 3),
        Brow: Math.Clamp((code >> 7) & 3, 0, 2),
        Nose: Math.Clamp((code >> 9) & 3, 0, 2),
        Jaw: Math.Clamp((code >> 11) & 3, 0, 2),
        Scar: Math.Clamp((code >> 13) & 3, 0, 2),
        Female: ((code >> 15) & 1) != 0,
        Age: age);

    /// Derive portrait-identical facial features from a man's seed and visage style (single source of truth for PortraitPainter & Puppet3D).
    public static FaceFeatures DeriveFeatures(int seed, int style, bool female = false, int age = 26)
    {
        float H(int salt) => Ease.Hash(seed * 31 + salt * 977);
        int cut = female ? (H(1) < 0.7f ? 1 : 2) : style == 3 ? 4 : H(1) < 0.45f ? 0 : H(1) < 0.8f ? 3 : H(1) < 0.92f ? 2 : 1;
        int beard = female || age < 18 ? 0 : style switch { 0 => 0, 1 => 1, 2 => 2, _ => 1 + (H(9) < 0.5f ? 0 : 2) };
        int eye = (int)(H(6) * 4) % 4;
        int brow = (int)(H(7) * 3) % 3;
        int nose = (int)(H(8) * 3) % 3;
        int jaw = (int)(H(10) * 3) % 3;
        return new FaceFeatures(cut, beard, eye, brow, nose, jaw, 0, female, age);
    }
}
