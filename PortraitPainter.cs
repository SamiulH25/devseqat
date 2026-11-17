using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// How a person's face is painted (docs/archive/LORDS.md LD1). Derived, never saved: from the character's id, culture, age, traits,
/// rank and his opinion of you, so it ages, scars and changes mood with him.
public sealed record Face(int Seed, int Skin, int Hair, int Cut, int Beard, int Eye, int Brow, int Nose, int Jaw, bool Female, int Age,
    int Scar, int Dress, bool Crown, int Mood, int Palette, Emblem Charge, int Field);

/// Painted bust portraits: 96x120 pixels in the game's pixel style, on the house's arms; this class reads a person into a
/// `Face`, `Bust` (PortraitBust.cs) paints it. Drawing only (the sim never reads it).
public static class PortraitPainter
{
    public const int W = 96, H = 120;
    /// Mood towards you: 0 warm, 1 even, 2 cold, 3 hostile.
    public static int MoodOf(float opinion) => opinion >= 30 ? 0 : opinion > -10 ? 1 : opinion > -40 ? 2 : 3;
    public static readonly Color[] EyeColours = { new(70, 96, 140), new(84, 62, 40), new(60, 110, 80), new(46, 40, 36) };

    /// A character's face as the campaign sees him now.
    public static Face Of(Campaign c, Character ch)
    {
        int seed = ch.Id * 7919 + 13;
        int visage = ch.Kind == "captain" && c.Captain.Visage >= 0 ? c.Captain.Visage : Variety.Visage(ch.Culture, seed);
        var (skin, hair, style) = Variety.Parts(visage);
        float H(int salt) => Ease.Hash(seed * 31 + salt * 977);
        bool female = ch.Female;
        int cut = female ? (H(1) < 0.7f ? 1 : 2) : style == 3 ? 4 : H(1) < 0.45f ? 0 : H(1) < 0.8f ? 3 : H(1) < 0.92f ? 2 : 1;
        int beard = female ? 0 : style switch { 0 => 0, 1 => 1, 2 => 2, _ => 1 + (H(9) < 0.5f ? 0 : 2) };
        if (!female && ch.Age < ComeOfAgeBeard) beard = 0;
        int scar = 0;
        if (!female || H(2) < 0.3f)
        {
            float risk = (ch.Traits.Contains("Cruel") ? 0.4f : 0) + (ch.Traits.Contains("Reckless") ? 0.4f : 0) + (ch.Kind is "lord" or "knight" or "captain" ? 0.15f : 0) + ch.Age / 400f;
            if (H(3) < risk) scar = 1 + (int)(H(4) * 2) % 2;
        }
        bool ruler = ch.Faction >= 0 && c.RulerOf(ch.Faction) == ch;
        int dress = ch.Kind == "knight" || ch.Order != "" ? 3 : ch.Kind is "captain" ? 2 : ch.Kind == "lord" && !female && (ch.Traits.Contains("Reckless") || ch.Traits.Contains("Ambitious") || H(5) < 0.3f) ? 2 : ch.Kind == "kin" ? 0 : 1;
        var house = c.HouseOf(ch);
        int palette = ch.Kind == "captain" ? 2 : Style.PaletteOf(ch.Faction);
        var charge = house != null ? (Emblem)(house.Charge % 9) : Style.Sides[palette].Charge;
        if (charge == Emblem.None) charge = Emblem.Fess;
        int mood = ch.Kind == "captain" ? 0 : MoodOf(c.OpinionOfYou(ch));
        if (ch.Kind == "captain" && c.Captain.Likeness is { } l) // the face chosen in the creator, and your arms behind it
            return Captain(seed, skin, hair, l, c.Captain.Arms >= 0 ? (Emblem)c.Captain.Arms : charge, ch.Age) with { Scar = Math.Max(l.Scar, scar) };
        return new Face(seed, skin, hair, cut, beard, (int)(H(6) * EyeColours.Length) % EyeColours.Length, (int)(H(7) * 3) % 3, (int)(H(8) * 3) % 3,
            (int)(H(10) * 3) % 3, female, ch.Age, scar, dress, ruler, mood, palette, charge, house?.Field ?? 0);
    }
    const int ComeOfAgeBeard = 18;

    /// The seed of a named man's face: the very seed his battle look comes from (`Variety.Seed`), so the man on the roll is
    /// the man on the field.
    public static int SoldierSeed(int soldierId) => soldierId * 7919 + 1;

    /// A named man's face (docs/COMPANY.md C1): his visage as the battle draws it, his age, a scar per two wounds, dress by
    /// his troop's tier (cloth, a padded jack, mail under a cloak, plate; a knight of your order wears its mark), his company's charge on
    /// your colours, and his mood from its cohesion.
    public static Face Of(Soldier m, CompanyRecord? co) =>
        OfMan(m.Id, Troops.Get(Troops.Has(m.Troop) ? m.Troop : "levies"), m.Wounds, Soldier.AgeOf(m), m.Knight, co?.Banner ?? 0, co == null ? 1 : CohesionMood(co.Cohesion));

    /// The same for a man on the field, who has only his troop and id (no wounds or age kept in a battle).
    public static Face OfMan(int soldierId, UnitType t, int wounds, int age, bool knight, int banner, int mood)
    {
        int seed = SoldierSeed(soldierId);
        var (skin, hair, style) = Variety.Parts(Variety.Visage(t.Culture, seed));
        float H(int salt) => Ease.Hash(seed * 31 + salt * 977);
        int cut = style == 3 ? 4 : H(1) < 0.45f ? 0 : H(1) < 0.8f ? 3 : H(1) < 0.92f ? 2 : 1;
        int beard = age < ComeOfAgeBeard ? 0 : style switch { 0 => 0, 1 => 1, 2 => 2, _ => 1 + (H(9) < 0.5f ? 0 : 2) };
        int dress = knight || t.Tier >= 7 ? 3 : t.Tier >= 5 ? 2 : t.Tier >= 3 ? 4 : 0;
        var charge = (Emblem)(banner % 9);
        if (charge == Emblem.None) charge = Style.Sides[2].Charge == Emblem.None ? Emblem.Fess : Style.Sides[2].Charge;
        return new Face(seed, skin, hair, cut, beard, (int)(H(6) * EyeColours.Length) % EyeColours.Length, (int)(H(7) * 3) % 3, (int)(H(8) * 3) % 3,
            (int)(H(10) * 3) % 3, false, age, Math.Min(2, wounds / 2), dress, false, mood, 2, charge, 0);
    }

    /// A company's cohesion as a man's mood: warm when it is high, cold when it frays.
    public static int CohesionMood(float cohesion) => cohesion >= 18 ? 0 : cohesion >= 7 ? 1 : 2;

    /// The captain as chosen in the creator: the face on the free companies' colours with your charge.
    public static Face Captain(int seed, int skin, int hair, CaptainLook l, Emblem arms, int age) =>
        new(seed, skin, hair, l.Cut, l.Beard, l.Eye, l.Brow, l.Nose, l.Jaw, false, age, l.Scar, 2, false, 0, 2, arms == Emblem.None ? Emblem.Fess : arms, 0);

    /// The creator's preview of a making.
    public static Face Of(CaptainMaking m) => Captain(7932, m.Skin, m.Hair, m.Look, (Emblem)m.Arms, Origins.Age(m));

    /// The face in pixels, W x H, opaque (painted by `Bust`, PortraitBust.cs).
    public static Color[] Paint(Face f) => new Bust(f).Paint();

    /// The middle of the picture that holds the head and shoulders (a square), for short rows.
    public static readonly Rectangle HeadCrop = new(8, 8, 80, 80);

    /// Part of a picture brought down to `dw` x `dh` by averaging the pixels each new one covers (small wells: point
    /// sampling would drop eyes and mouths).
    public static Color[] Resample(Color[] src, int sw, Rectangle crop, int dw, int dh)
    {
        var dst = new Color[dw * dh];
        float kx = crop.Width / (float)dw, ky = crop.Height / (float)dh;
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                float x0 = crop.X + x * kx, x1 = x0 + kx, y0 = crop.Y + y * ky, y1 = y0 + ky, sum = 0;
                var acc = Vector3.Zero;
                for (int sy = (int)y0; sy < y1; sy++)
                    for (int sx = (int)x0; sx < x1; sx++)
                    {
                        float wgt = (MathF.Min(x1, sx + 1) - MathF.Max(x0, sx)) * (MathF.Min(y1, sy + 1) - MathF.Max(y0, sy));
                        if (wgt <= 0) continue;
                        acc += src[sy * sw + sx].ToVector3() * wgt; sum += wgt;
                    }
                dst[y * dw + x] = new Color(acc / MathF.Max(sum, 1e-5f));
            }
        return dst;
    }
}

/// The GPU side: one texture per face and size, painted on first use and kept while the face stays the same.
public static class Portraits
{
    static readonly Dictionary<(Face, int, int, bool), Texture2D> cache = new();

    /// The face's texture, whole and at full size.
    public static Texture2D Of(GraphicsDevice gd, Face f) => Sized(gd, f, PortraitPainter.W, PortraitPainter.H);

    /// The face brought down to `w` x `h` pixels (`head`: only the head and shoulders, `PortraitPainter.HeadCrop`).
    public static Texture2D Sized(GraphicsDevice gd, Face f, int w, int h, bool head = false)
    {
        w = Math.Max(1, w); h = Math.Max(1, h);
        if (cache.TryGetValue((f, w, h, head), out var t)) return t;
        if (cache.Count > 600) { foreach (var old in cache.Values) old.Dispose(); cache.Clear(); }
        int W = PortraitPainter.W, H = PortraitPainter.H;
        var px = PortraitPainter.Paint(f);
        if (head || w != W || h != H) px = PortraitPainter.Resample(px, W, head ? PortraitPainter.HeadCrop : new Rectangle(0, 0, W, H), w, h);
        t = new Texture2D(gd, w, h);
        t.SetData(px);
        return cache[(f, w, h, head)] = t;
    }
}
