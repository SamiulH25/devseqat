using Microsoft.Xna.Framework;

namespace Ironfield;

public enum Emblem
{
    Chevron, Saltire, Lozenge, None, Bend, Cross, Pale, Fess, Sun,
    Chevrons, Quartered, Checky, PerPale, PerFess, Bordure, Pile, Chief, Paly, Barry, BendSinister, Gyronny,
    Crescent, Star, Crown, Sword, Tower, Chalice, Axe, Rose,
}

/// Field division of a heraldic banner (part-by-part banner creator).
public enum BannerDivision { Plain, PerPale, PerFess, Quartered, Bend, BendSinister, Chevron, Paly, Barry, Checky }

/// Border / trim style of a heraldic banner.
public enum BannerBorder { None, Bordure, Checkered, Double }

/// Silhouette cut of a hanging banner.
public enum BannerCut { Swallowtail, Pennant, Square, Forked }

/// A side's heraldry: the cloth it dyes, the trim and charge painted on shields and tabards, its UI colour,
/// and optional part-by-part banner details (field division, secondary field colour, border, and cut).
public sealed record Heraldry(
    string Name, Color Cloth, Color ClothDark, Color Trim, Emblem Charge, Color Ui,
    BannerDivision Division = BannerDivision.Plain,
    Color? Field2 = null,
    BannerBorder Border = BannerBorder.None,
    BannerCut Cut = BannerCut.Swallowtail);

/// The design tokens of docs/DESIGN.md in one place, shared by the sprite painter, the 3D world and the HUD.
public static class Style
{
    // ---------- sides ----------

    /// Palette index: 0 = the Azure Crown, 1 = the Crimson Compact, 2 = you (gold, so you can always find yourself), 3 = bandits,
    /// 4 = the Northmarch clans, 5 = the Sunward Emirates, 6 = the Khaganate, 7 = the Saltmarsh League, 8 = the Order of the
    /// Ashen Sun (docs/archive/WORLD.md W3). Every palette paints a full set of sprite parts (the atlas holds them).
    public static readonly Heraldry[] Sides = Settings.Current.ColorBlind ? ColorBlind() : Standard();

    /// Colour-blind-safe sides (roadmap D3): blue against vermilion and reddish purple (Okabe-Ito hues), which stay apart
    /// under deuteranopia and protanopia. You stay gold; brigands stay grey-brown. Chosen at start-up (Settings).
    internal static Heraldry[] ColorBlind() => new Heraldry[]
    {
        new("Brevon", new(0, 96, 160), new(0, 56, 100), new(236, 228, 196), Emblem.Chevron, new(86, 180, 233)),
        new("Vespra", new(204, 92, 0), new(120, 52, 0), new(250, 236, 200), Emblem.Saltire, new(230, 159, 0)),
        new("Gold", new(210, 168, 54), new(126, 88, 30), new(44, 36, 44), Emblem.Lozenge, new(240, 228, 66)),
        new("Brigands", new(112, 98, 78), new(70, 62, 52), new(160, 150, 128), Emblem.None, new(190, 170, 130)),
        new("Skar", new(160, 84, 132), new(96, 46, 80), new(240, 236, 232), Emblem.Bend, new(204, 121, 167)),
        new("Qadir", new(0, 140, 110), new(0, 80, 64), new(240, 228, 66), Emblem.Sun, new(0, 158, 115)),
        new("Naryn", new(70, 70, 76), new(36, 36, 40), new(230, 159, 0), Emblem.Fess, new(150, 150, 160)),
        new("Brinemere", new(110, 190, 230), new(56, 110, 140), new(30, 30, 40), Emblem.Pale, new(140, 210, 245)),
        new("Tarn", new(232, 232, 226), new(150, 150, 146), new(20, 20, 24), Emblem.Cross, new(245, 245, 240)),
    };

    internal static Heraldry[] Standard() => new Heraldry[]
    {
        new("Brevon", new(54, 94, 176), new(30, 50, 108), new(230, 218, 178), Emblem.Chevron, new(112, 160, 255)),
        new("Vespra", new(172, 48, 42), new(98, 26, 28), new(222, 178, 72), Emblem.Saltire, new(236, 88, 70)),
        new("Gold", new(210, 168, 54), new(126, 88, 30), new(44, 36, 44), Emblem.Lozenge, new(240, 200, 60)),
        new("Brigands", new(112, 98, 78), new(70, 62, 52), new(160, 150, 128), Emblem.None, new(190, 170, 130)),
        new("Skar", new(46, 104, 74), new(24, 58, 42), new(226, 228, 220), Emblem.Bend, new(96, 200, 140)),
        new("Qadir", new(206, 128, 40), new(124, 70, 20), new(246, 232, 190), Emblem.Sun, new(244, 160, 70)),
        new("Naryn", new(40, 130, 140), new(20, 74, 80), new(236, 214, 120), Emblem.Fess, new(80, 196, 206)),
        new("Brinemere", new(110, 64, 150), new(62, 34, 90), new(216, 220, 228), Emblem.Pale, new(170, 120, 220)),
        new("Tarn", new(230, 228, 220), new(150, 146, 140), new(150, 30, 28), Emblem.Cross, new(240, 238, 230)),
    };

    /// The palette a campaign faction paints with (the Northmarch clans are palette 4; -1 and -2 are free captains and brigands).
    public static int PaletteOf(int faction) => faction switch { -2 or -3 => 3, < 0 => 0, _ => Factions.Get(faction).Palette };

    public static readonly int EmblemCount = Enum.GetValues<Emblem>().Length;

    /// All selectable heraldic banner designs (every Emblem except None).
    public static readonly Emblem[] BannerEmblems = Enum.GetValues<Emblem>().Where(e => e != Emblem.None).ToArray();

    /// Charges selectable in the part-by-part banner creator (separated from field divisions; includes None for division-only banners).
    public static readonly Emblem[] BannerCharges =
    {
        Emblem.None, Emblem.Chevron, Emblem.Chevrons, Emblem.Saltire, Emblem.Lozenge, Emblem.Cross,
        Emblem.Pale, Emblem.Fess, Emblem.Pile, Emblem.Chief, Emblem.Sun, Emblem.Crescent,
        Emblem.Star, Emblem.Crown, Emblem.Sword, Emblem.Tower, Emblem.Chalice, Emblem.Axe, Emblem.Rose,
    };

    /// All selectable field divisions, borders and banner cuts.
    public static readonly BannerDivision[] BannerDivisions = Enum.GetValues<BannerDivision>();
    public static readonly BannerBorder[] BannerBorders = Enum.GetValues<BannerBorder>();
    public static readonly BannerCut[] BannerCuts = Enum.GetValues<BannerCut>();

    /// Charges used when generating distinct sub-banners for companies in a party.
    static readonly Emblem[] CompanySubCharges =
    {
        Emblem.Chevron, Emblem.Saltire, Emblem.Lozenge, Emblem.Cross, Emblem.Sun,
        Emblem.Star, Emblem.Sword, Emblem.Axe, Emblem.Tower, Emblem.Crescent, Emblem.Crown, Emblem.Rose,
    };

    /// Heraldic field tinctures for custom company banners: (Name, Cloth, ClothDark, Ui).
    public static readonly (string Name, Color Cloth, Color Dark, Color Ui)[] BannerFields =
    {
        ("AZURE",    new(54, 94, 176),  new(30, 50, 108),  new(112, 160, 255)),
        ("CRIMSON",  new(172, 48, 42),  new(98, 26, 28),   new(236, 88, 70)),
        ("SABLE",    new(44, 44, 52),   new(22, 22, 28),   new(160, 164, 176)),
        ("FOREST",   new(46, 108, 72),  new(24, 60, 40),   new(96, 200, 140)),
        ("GOLD",     new(204, 160, 48), new(124, 86, 24),  new(240, 200, 60)),
        ("ROYAL",    new(112, 60, 152), new(62, 30, 90),   new(176, 122, 224)),
        ("IRON",     new(96, 102, 114), new(54, 58, 68),   new(176, 182, 196)),
        ("TEAL",     new(38, 128, 138), new(20, 72, 78),   new(80, 196, 206)),
        ("OCHRE",    new(196, 114, 38), new(118, 62, 18),  new(242, 156, 70)),
        ("ARGENT",   new(222, 218, 206),new(146, 142, 132),new(236, 234, 226)),
        ("BURGUNDY", new(118, 30, 48),  new(68, 16, 28),   new(208, 76, 98)),
        ("SLATE",    new(64, 86, 112),  new(36, 50, 68),   new(128, 162, 198)),
    };

    /// Heraldic charge / trim tinctures for custom company banners.
    public static readonly (string Name, Color Trim)[] BannerTrims =
    {
        ("CREAM",   new(234, 222, 184)),
        ("GOLD",    new(238, 196, 68)),
        ("WHITE",   new(244, 244, 240)),
        ("ONYX",    new(34, 32, 38)),
        ("SCARLET", new(196, 48, 44)),
        ("AZURE",   new(68, 124, 212)),
        ("EMERALD", new(62, 156, 96)),
        ("SILVER",  new(186, 194, 206)),
        ("COPPER",  new(212, 126, 62)),
        ("PLUM",    new(146, 82, 184)),
    };

    /// Resolve a company's full heraldry (base palette + optional custom parts: charge, field, trim, division, field2, border, cut).
    public static Heraldry BannerOf(int palette, int arms = -1, int field = -1, int trim = -1,
        int division = 0, int field2 = -1, int border = 0, int cut = 0)
    {
        var h = Sides[Math.Clamp(palette, 0, Sides.Length - 1)];
        if (arms >= 0 && arms < EmblemCount) h = h with { Charge = (Emblem)arms };
        if (field >= 0 && field < BannerFields.Length)
        {
            var f = BannerFields[field];
            h = h with { Cloth = f.Cloth, ClothDark = f.Dark, Ui = f.Ui };
        }
        if (trim >= 0 && trim < BannerTrims.Length) h = h with { Trim = BannerTrims[trim].Trim };
        var div = division >= 0 && division < BannerDivisions.Length ? (BannerDivision)division : BannerDivision.Plain;
        Color? f2 = field2 >= 0 && field2 < BannerFields.Length ? BannerFields[field2].Cloth : null;
        var brd = border >= 0 && border < BannerBorders.Length ? (BannerBorder)border : BannerBorder.None;
        var ct = cut >= 0 && cut < BannerCuts.Length ? (BannerCut)cut : BannerCut.Swallowtail;
        return h with { Division = div, Field2 = f2, Border = brd, Cut = ct };
    }

    /// Generate a distinct, deterministic sub-banner for one company in your party, using your main party banner as the base.
    /// Preserves the main banner's primary field colour and trim family while varying division, accent band, cut (by role) and charge
    /// so every company in the party can be told apart at a glance.
    public static Heraldry CompanyBanner(Heraldry main, int companyId, string role = "", int chargeOverride = -1, bool flagship = false)
    {
        var roleCut = role switch
        {
            "cavalry" => BannerCut.Pennant,
            "archers" or "skirmishers" => BannerCut.Forked,
            "casters" => BannerCut.Swallowtail,
            _ => main.Cut,
        };
        if (flagship)
        {
            var ch = chargeOverride >= 0 && chargeOverride < EmblemCount && (Emblem)chargeOverride != Emblem.None
                ? (Emblem)chargeOverride : main.Charge;
            return main with { Charge = ch, Cut = roleCut };
        }

        uint h = (uint)Rng.Mix(companyId, 17);
        // Distinct division cadency derived from companyId (keeps main's feel while ensuring adjacent companies differ).
        BannerDivision[] cadencyDivs =
        {
            BannerDivision.PerPale, BannerDivision.PerFess, BannerDivision.Quartered,
            BannerDivision.Bend, BannerDivision.BendSinister, BannerDivision.Chevron,
            BannerDivision.Paly, BannerDivision.Barry,
        };
        var div = cadencyDivs[(int)(h % (uint)cadencyDivs.Length)];
        if (div == main.Division) div = cadencyDivs[(int)((h + 3) % (uint)cadencyDivs.Length)];

        // Secondary field colour: either main's Field2, or a harmonious contrasting company accent derived from companyId.
        Color f2;
        int accentPick = (int)((h >> 4) % 5);
        f2 = accentPick switch
        {
            0 => main.Field2 ?? main.ClothDark,
            1 => Color.Lerp(main.ClothDark, Color.Black, 0.35f),
            2 => Color.Lerp(main.Cloth, main.Trim, 0.28f),
            3 => BannerFields[(int)((h >> 8) % (uint)BannerFields.Length)].Cloth,
            _ => main.Field2 != null ? Color.Lerp(main.Field2.Value, main.ClothDark, 0.4f) : Color.Lerp(main.Cloth, Color.White, 0.22f),
        };
        // Avoid identical Cloth/Field2 so the division always reads clearly.
        if (Math.Abs(f2.R - main.Cloth.R) + Math.Abs(f2.G - main.Cloth.G) + Math.Abs(f2.B - main.Cloth.B) < 42)
            f2 = main.ClothDark;

        // Company charge: use explicit override if valid & different, otherwise pick a distinct sub-charge from companyId.
        Emblem charge = chargeOverride >= 0 && chargeOverride < EmblemCount && (Emblem)chargeOverride != Emblem.None
            ? (Emblem)chargeOverride
            : CompanySubCharges[(int)((h >> 12) % (uint)CompanySubCharges.Length)];

        // Subtle border cadence so companies read even at tiny sizes.
        var border = main.Border != BannerBorder.None
            ? main.Border
            : (BannerBorder)((h >> 16) % 3); // None, Bordure, or Checkered

        return main with
        {
            Division = div,
            Field2 = f2,
            Charge = charge,
            Border = border,
            Cut = roleCut,
        };
    }

    /// True when normalized (u, v) in 0..1 lies on the secondary colour of a field division.
    public static bool OnDivision(BannerDivision div, float u, float v) => div switch
    {
        BannerDivision.PerPale => u >= 0.5f,
        BannerDivision.PerFess => v >= 0.5f,
        BannerDivision.Quartered => (u < 0.5f) ^ (v < 0.5f),
        BannerDivision.Bend => MathF.Abs(u - v * 0.85f - 0.08f) < 0.14f,
        BannerDivision.BendSinister => MathF.Abs((1f - u) - v * 0.85f - 0.08f) < 0.14f,
        BannerDivision.Chevron => v > 0.72f - MathF.Abs(u - 0.5f) * 0.88f,
        BannerDivision.Paly => ((int)(u * 4f)) % 2 == 1,
        BannerDivision.Barry => ((int)(v * 4f)) % 2 == 1,
        BannerDivision.Checky => (((int)(u * 4f)) + ((int)(v * 5f))) % 2 == 0,
        _ => false,
    };

    public static string DivisionName(BannerDivision d) => d switch
    {
        BannerDivision.PerPale => "PER PALE",
        BannerDivision.PerFess => "PER FESS",
        BannerDivision.BendSinister => "BEND SINISTER",
        _ => d.ToString().ToUpperInvariant(),
    };

    public static string BorderName(BannerBorder b) => b.ToString().ToUpperInvariant();
    public static string CutName(BannerCut c) => c.ToString().ToUpperInvariant();

    public static string EmblemName(Emblem e) => e switch
    {
        Emblem.None => "NONE",
        Emblem.Chevrons => "DOUBLE CHEVRON",
        Emblem.PerPale => "PER PALE",
        Emblem.PerFess => "PER FESS",
        Emblem.BendSinister => "BEND SINISTER",
        _ => e.ToString().ToUpperInvariant(),
    };

    /// Shared heraldic charge mask in normalized (0..1, 0..1) coordinates for shields, banners, and waving flags.
    public static bool OnCharge(Emblem charge, float u, float v, float scale = 1f)
    {
        float cx = u - 0.5f, cy = v - 0.46f;
        float r = MathF.Sqrt(cx * cx + cy * cy);
        return charge switch
        {
            Emblem.Chevron => MathF.Abs(v - (0.76f - MathF.Abs(cx) * 0.95f)) < 0.09f * scale,
            Emblem.Chevrons => MathF.Abs(v - (0.60f - MathF.Abs(cx) * 0.90f)) < 0.065f * scale
                || MathF.Abs(v - (0.84f - MathF.Abs(cx) * 0.90f)) < 0.065f * scale,
            Emblem.Saltire => MathF.Abs(cx - (v - 0.5f) * 0.84f) < 0.075f * scale || MathF.Abs(cx + (v - 0.5f) * 0.84f) < 0.075f * scale,
            Emblem.Lozenge => MathF.Abs(cx) + MathF.Abs(v - 0.46f) * 0.8f < 0.21f * scale,
            Emblem.Bend => MathF.Abs(u - v * 0.84f - 0.08f) < 0.085f * scale,
            Emblem.BendSinister => MathF.Abs((1f - u) - v * 0.84f - 0.08f) < 0.085f * scale,
            Emblem.Cross => MathF.Abs(cx) < 0.075f * scale || MathF.Abs(v - 0.42f) < 0.075f * scale,
            Emblem.Pale => MathF.Abs(cx) < 0.13f * scale,
            Emblem.Fess => MathF.Abs(v - 0.46f) < 0.115f * scale,
            Emblem.Sun => r < 0.14f * scale
                || (MathF.Abs(r - 0.23f * scale) < 0.045f * scale && ((int)((MathF.Atan2(cy, cx) + MathF.PI) / (MathF.PI / 4)) % 2 == 0)),
            Emblem.Quartered => (u < 0.5f) ^ (v < 0.48f),
            Emblem.Checky => (((int)(u * 4f)) + ((int)(v * 5f))) % 2 == 0,
            Emblem.PerPale => u >= 0.5f,
            Emblem.PerFess => v >= 0.48f,
            Emblem.Bordure => u < 0.16f || u > 0.84f || v < 0.15f || v > 0.82f,
            Emblem.Pile => v < 0.84f && MathF.Abs(cx) < (0.84f - v) * 0.42f,
            Emblem.Chief => v < 0.30f,
            Emblem.Paly => ((int)(u * 5f)) % 2 == 1,
            Emblem.Barry => ((int)(v * 6f)) % 2 == 1,
            Emblem.Gyronny => (((int)((MathF.Atan2(cy, cx) + MathF.PI) / (MathF.PI / 4))) & 1) == 0,
            Emblem.Crescent => r < 0.24f * scale && MathF.Sqrt((cx - 0.08f * scale) * (cx - 0.08f * scale) + (cy + 0.02f * scale) * (cy + 0.02f * scale)) > 0.18f * scale,
            Emblem.Star => r < (0.14f + 0.14f * MathF.Pow(MathF.Abs(MathF.Cos(3f * MathF.Atan2(cy, cx))), 1.5f)) * scale,
            Emblem.Crown => (v > 0.48f && v < 0.62f && MathF.Abs(cx) < 0.28f * scale)
                || (v >= 0.28f && v <= 0.50f && (MathF.Abs(cx) < (v - 0.26f) * 0.38f || MathF.Abs(cx + 0.20f * scale) < (v - 0.30f) * 0.32f || MathF.Abs(cx - 0.20f * scale) < (v - 0.30f) * 0.32f)),
            Emblem.Sword => (MathF.Abs(cx) < 0.045f * scale && v > 0.14f && v < 0.78f)
                || (MathF.Abs(v - 0.60f) < 0.045f * scale && MathF.Abs(cx) < 0.22f * scale)
                || (r < 0.07f * scale && v > 0.74f),
            Emblem.Tower => (MathF.Abs(cx) < 0.18f * scale && v > 0.30f && v < 0.74f && !(MathF.Abs(cx) < 0.06f * scale && v > 0.56f))
                || (v >= 0.20f && v <= 0.31f && (MathF.Abs(cx) < 0.05f * scale || (MathF.Abs(cx) > 0.11f * scale && MathF.Abs(cx) < 0.21f * scale))),
            Emblem.Chalice => (v > 0.22f && v < 0.46f && MathF.Abs(cx) < (0.24f - (v - 0.22f) * 0.45f) * scale)
                || (v >= 0.46f && v < 0.68f && MathF.Abs(cx) < 0.055f * scale)
                || (v >= 0.66f && v < 0.74f && MathF.Abs(cx) < 0.18f * scale),
            Emblem.Axe => (MathF.Abs(cx) < 0.045f * scale && v > 0.18f && v < 0.78f)
                || (v > 0.24f && v < 0.50f && MathF.Abs(cx) < 0.26f * scale && MathF.Abs(cx) > 0.04f * scale && MathF.Abs(v - 0.37f) < 0.07f + MathF.Abs(cx) * 0.42f),
            Emblem.Rose => r < 0.08f * scale
                || (r > 0.10f * scale && r < (0.19f + 0.09f * MathF.Cos(5f * MathF.Atan2(cy, cx) - MathF.PI * 0.5f)) * scale),
            _ => false,
        };
    }

    // ---------- materials (base tones; the painter builds a 5-step ramp from each) ----------

    public static readonly Color GoblinSkin = new(112, 140, 64);
    public static readonly Color Bone = new(212, 202, 174);
    public static readonly Color Skin = new(226, 180, 140), Hair = new(96, 64, 40), Leather = new(118, 80, 48),
        Wood = new(140, 98, 58), Steel = new(172, 178, 190), Iron = new(96, 100, 112), Brass = new(214, 172, 70),
        Linen = new(222, 214, 190), Blood = new(128, 22, 22), Coat = new(208, 208, 208), Mane = new(92, 92, 92), Hoof = new(64, 60, 58);

    /// Horse coats, as multipliers on the neutral grey coat sprites: bay, chestnut, black, grey, dun.
    public static readonly Vector3[] Coats =
    {
        new(0.74f, 0.5f, 0.32f), new(0.86f, 0.52f, 0.3f), new(0.34f, 0.31f, 0.31f), new(0.9f, 0.9f, 0.92f), new(0.88f, 0.74f, 0.52f),
    };

    /// A camel's sand coat, the same kind of multiplier (docs/archive/WORLD.md W4).
    public static readonly Vector3 CamelCoat = new(0.94f, 0.8f, 0.56f);

    /// Light for sprites, in sprite space (x right, y down, z toward the viewer): from the upper left, like the sun.
    public static readonly Vector3 SpriteLight = Vector3.Normalize(new Vector3(-0.55f, -0.6f, 0.62f));

    /// Pixel density of unit sprites: metres per sprite pixel (a man is ~58 px tall).
    public const float Px = 0.03f;

    // ---------- interface ----------

    public static readonly Color Accent = new(226, 184, 82);          // aged gilt: you, the selection, the highlighted choice
    public static readonly Color Friendly = Sides[0].Ui, Foe = Sides[1].Ui;
    /// The captain's aura (gauge, blade glow; docs/archive/KNIGHT_ORDERS.md).
    public static readonly Color Aura = new(120, 200, 255);
    public static readonly Color Ok = new(110, 196, 104), Warn = new(236, 186, 60), Bad = new(226, 72, 60), Orange = new(240, 140, 50);
    public static readonly Color Text = new(240, 234, 220), Dim = new(174, 168, 158), HorseBar = new(178, 124, 74);
    public static readonly Color Panel = new Color(20, 18, 21) * 0.86f, PanelEdge = new Color(146, 126, 88) * 0.55f;

    /// Type scale for the 5x7 pixel font (times the UI scale): captions, body, headings, titles.
    public const int Caption = 1, Body = 2, Heading = 3, Title = 5;

    public static readonly string[] Numerals = { "", "I", "II", "III", "IV", "V", "VI", "VII" };
}
