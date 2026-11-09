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
    static readonly string[][] HelmKin = { new[] { "cap", "hood", "none" }, new[] { "kettle", "nasal", "coif" } };
    static readonly string[][] WeaponKin = { new[] { "sword", "axe", "mace" } };

    static readonly Dictionary<Look, Dictionary<int, Look>> cache = new(ReferenceEqualityComparer.Instance);

    /// The seed of one man's looks: his Soldier id if he is named, else his place in the battle.
    public static int Seed(Unit u) => u.SoldierId != 0 ? u.SoldierId * 7919 + 1 : u.Index;

    /// How a man of troop `t` looks; the player and creatures are drawn exactly as their look says.
    public static Look Of(Unit u) => u.IsPlayer || u.Type.Look.Face != "man" ? u.Type.Look : Of(u.Type, Seed(u), keepKit: u.IsLeader || u.Hero);

    public static Look Of(UnitType t, int seed, bool keepKit = false)
    {
        var look = t.Look;
        if (look.Face != "man") return look;
        int visage = Visage(t.Culture, seed);
        int helm = keepKit ? 0 : Pick(seed, 1, look.Head, HelmKin, 0.45f);
        int weapon = keepKit ? 0 : Pick(seed, 2, look.Weapon, WeaponKin, 0.35f);
        int code = visage + Visages * (helm + 4 * weapon);
        if (!cache.TryGetValue(look, out var byCode)) cache[look] = byCode = new();
        if (byCode.TryGetValue(code, out var v)) return v;
        v = look with
        {
            Visage = visage,
            Head = helm == 0 ? look.Head : Kin(HelmKin, look.Head)[helm - 1],
            Weapon = weapon == 0 ? look.Weapon : Kin(WeaponKin, look.Weapon)[weapon - 1],
        };
        return byCode[code] = v;
    }

    /// Drawn height, 0.95 to 1.05 of the troop's.
    public static float Stature(Unit u) => u.IsPlayer ? 1 : 0.95f + 0.1f * Ease.Hash(Seed(u) * 31 + 5);

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
    static int Visage(string culture, int seed)
    {
        float[] skinW = culture switch
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
}
