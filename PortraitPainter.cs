using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// How a person's face is painted (docs/LORDS.md LD1). Derived, never saved: from the character's id, culture, age, traits,
/// rank and his opinion of you, so it ages, scars and changes mood with him.
public sealed record Face(int Seed, int Skin, int Hair, int Cut, int Beard, int Eye, int Brow, int Nose, int Jaw, bool Female, int Age,
    int Scar, int Dress, bool Crown, int Mood, int Palette, Emblem Charge, int Field);

/// Painted bust portraits: 48x60 pixels in the game's pixel style, on the house's arms. Drawing only (the sim never reads it).
public static class PortraitPainter
{
    public const int W = 48, H = 60;
    /// Mood towards you: 0 warm, 1 even, 2 cold, 3 hostile.
    public static int MoodOf(float opinion) => opinion >= 30 ? 0 : opinion > -10 ? 1 : opinion > -40 ? 2 : 3;
    static readonly Color[] Eyes = { new(70, 96, 140), new(84, 62, 40), new(60, 110, 80), new(46, 40, 36) };

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
        return new Face(seed, skin, hair, cut, beard, (int)(H(6) * Eyes.Length) % Eyes.Length, (int)(H(7) * 3) % 3, (int)(H(8) * 3) % 3,
            (int)(H(10) * 3) % 3, female, ch.Age, scar, dress, ruler, mood, palette, charge, house?.Field ?? 0);
    }
    const int ComeOfAgeBeard = 18;

    /// The face in pixels, W x H, opaque.
    public static Color[] Paint(Face f)
    {
        var px = new Color[W * H];
        var side = Style.Sides[f.Palette];
        Background(px, f, side);
        var fig = new bool[W * H];
        void Put(int x, int y, Color col) { if (x >= 0 && y >= 0 && x < W && y < H) { px[y * W + x] = col; fig[y * W + x] = true; } }

        // Shoulders and dress.
        Color cloth = f.Dress == 0 ? Color.Lerp(side.Cloth, new Color(200, 190, 170), 0.35f) : side.Cloth;
        for (int y = 44; y < H; y++)
        {
            int half = 12 + (y - 44) * 2 / 3 + (y > 50 ? 3 : 0);
            for (int x = 24 - half; x < 24 + half; x++)
            {
                float lit = x < 24 ? 1.08f : 0.82f;
                Color col = f.Dress switch
                {
                    2 => ((x + y) % 2 == 0 ? new Color(150, 152, 158) : new Color(110, 112, 120)), // mail
                    3 => Math.Abs(x - 24) < 7 ? side.Cloth : new Color(176, 180, 188), // plate under a tabard
                    _ => cloth,
                };
                if (x == 24 - half || x == 24 + half - 1) col = Shade(col, 0.6f);
                Put(x, y, Shade(col, lit));
            }
        }
        if (f.Dress == 3) for (int y = 48; y < H; y++) { Put(24, y, side.Trim); if (y == 52) for (int x = 20; x < 29; x++) Put(x, y, side.Trim); }
        if (f.Dress == 1 || f.Crown) for (int x = 12; x < 36; x++) for (int y = 43; y < 46; y++) Put(x, y, Shade(new Color(190, 170, 140), (x + y) % 3 == 0 ? 0.85f : 1)); // a fur or cloth collar
        if (f.Dress == 0 && f.Female) for (int x = 18; x < 30; x++) Put(x, 45, side.Trim);

        Color skin = Variety.Skins[f.Skin];
        float grey = Math.Clamp((f.Age - 40) / 25f, 0, 0.85f);
        Color hair = Color.Lerp(Variety.Hairs[f.Hair], new Color(196, 194, 188), grey);
        Color skinDark = Shade(skin, 0.78f), line = Shade(skin, 0.62f);

        // Neck.
        for (int y = 36; y < 45; y++) for (int x = 20; x < 28; x++) Put(x, y, x >= 25 ? skinDark : skin);

        // Long hair falls behind the head and shoulders.
        if (f.Cut is 1 or 2)
            for (int y = 16; y < (f.Cut == 1 ? (f.Female ? 46 : 40) : f.Female ? 38 : 31); y++)
                for (int x = 12; x < 36; x++)
                    if (Math.Abs(x - 24) > 7 || y < 22) Put(x, y, Shade(hair, x < 24 ? 0.9f : 0.75f));

        // The head: an oval, narrower at the jaw.
        int rx = f.Female ? 9 : 10;
        for (int y = 12; y < 40; y++)
            for (int x = 12; x < 37; x++)
            {
                float dy = (y - 25.5f) / 13.5f;
                float w = rx * (y > 27 ? 1 - (y - 27) / 13f * (0.35f + f.Jaw * 0.08f) * (f.Female ? 1.2f : 1) : 1);
                float dx = (x + 0.5f - 24) / w;
                if (dx * dx + dy * dy * (y > 27 ? 0.6f : 1) > 1) continue;
                Put(x, y, x >= 27 ? skinDark : skin);
            }
        // Ears.
        for (int y = 24; y < 29; y++) { Put(13, y, skinDark); Put(34, y, Shade(skinDark, 0.9f)); }

        // Hair on top.
        if (f.Cut != 4)
            for (int y = 11; y < 21; y++)
                for (int x = 13; x < 36; x++)
                {
                    float dx = (x + 0.5f - 24) / 11.5f, dy = (y - 21f) / 10f;
                    bool fringe = y >= 17 && Math.Abs(x - 24) < 7 && f.Cut != 3 && (x + f.Seed) % 3 != 0 && y < 18;
                    if (dx * dx + dy * dy <= 1 && (y < 17 || fringe || Math.Abs(x - 24) > 8))
                        Put(x, y, Shade(hair, x < 22 ? 1.08f : 0.8f));
                }
        else // bald: a fringe round the sides only
            for (int y = 19; y < 26; y++) { Put(14, y, hair); Put(33, y, Shade(hair, 0.8f)); }
        if (f.Cut == 2) for (int y = 13; y < 16; y++) Put(24, y - 2, Shade(hair, 0.7f)); // tied up

        // Brows and eyes, shaped by mood.
        Color browC = Shade(hair, 0.7f);
        for (int k = 0; k < 2; k++)
        {
            int ex = k == 0 ? 19 : 28, inner = k == 0 ? 1 : -1;
            int lift = f.Mood == 0 ? -1 : 0;
            for (int i = -2; i <= 2; i++)
            {
                int dy = f.Mood >= 2 && i * inner > 0 ? (f.Mood == 3 ? 2 : 1) : 0;
                Put(ex + i, 22 + lift + dy, browC);
                if (f.Brow == 2 || f.Mood == 3) Put(ex + i, 21 + lift + dy, browC);
            }
            Put(ex - 1, 25, new Color(236, 232, 224));
            Put(ex, 25, Eyes[f.Eye]);
            Put(ex + 1, 25, f.Mood == 3 ? Eyes[f.Eye] : new Color(236, 232, 224));
            Put(ex - 1, 24, line); Put(ex, 24, line); Put(ex + 1, 24, line);
            if (f.Age > 38) { Put(ex + 3 * -inner, 26, line); }
        }
        // Nose.
        int nl = 27 + f.Nose;
        for (int y = 25; y < nl + 2; y++) Put(25, y, skinDark);
        Put(23, nl + 1, line); Put(24, nl + 2, line); Put(25, nl + 2, line);
        // Lines of age.
        if (f.Age > 45) { for (int x = 20; x < 28; x += 2) Put(x, 18, line); Put(20, nl + 3, line); Put(28, nl + 3, line); }
        // Mouth.
        int my = 34;
        Color lip = f.Female ? new Color(168, 82, 76) : Shade(skin, 0.55f);
        for (int x = 21; x < 28; x++) Put(x, my, lip);
        if (f.Mood == 0) { Put(20, my - 1, lip); Put(28, my - 1, lip); }
        else if (f.Mood >= 2) { Put(20, my + 1, lip); Put(28, my + 1, lip); if (f.Mood == 3) for (int x = 22; x < 27; x++) Put(x, my + 1, Shade(lip, 0.8f)); }
        // Beard.
        if (f.Beard == 1)
            for (int y = 30; y < 41; y++)
                for (int x = 14; x < 35; x++)
                {
                    if (!fig[y * W + x] || px[y * W + x] != skin && px[y * W + x] != skinDark) continue;
                    if (y == my && x is > 19 and < 29) continue;
                    if (y < 32 && Math.Abs(x - 24) < 6) continue;
                    Put(x, y, Shade(hair, x < 24 ? 0.95f : 0.75f));
                }
        if (f.Beard is 1 or 2) for (int x = 20; x < 29; x++) { Put(x, my - 1, Shade(hair, 0.85f)); if (f.Beard == 2 && (x == 20 || x == 28)) { Put(x, my, hair); Put(x, my + 1, hair); } }
        if (f.Beard == 3) for (int y = my + 2; y < 40; y++) for (int x = 22; x < 27; x++) Put(x, y, Shade(hair, 0.85f));
        // Scars.
        if (f.Scar == 1) for (int i = 0; i < 9; i++) Put(28 + i / 3, 21 + i, new Color(196, 120, 110));
        if (f.Scar == 2) for (int i = 0; i < 7; i++) Put(18 - i / 4, 27 + i, new Color(196, 120, 110));
        // A ruler's circlet.
        if (f.Crown)
        {
            for (int x = 14; x < 35; x++) Put(x, 16, (x % 4 == 0) ? new Color(255, 236, 140) : new Color(214, 170, 60));
            Put(24, 15, new Color(200, 40, 40)); Put(18, 15, new Color(214, 170, 60)); Put(30, 15, new Color(214, 170, 60));
        }

        // A dark outline round the figure.
        var outline = new List<int>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (!fig[y * W + x] && (x > 0 && fig[y * W + x - 1] || x < W - 1 && fig[y * W + x + 1] || y > 0 && fig[(y - 1) * W + x] || y < H - 1 && fig[(y + 1) * W + x]))
                    outline.Add(y * W + x);
        foreach (int i in outline) px[i] = new Color(24, 20, 22);
        return px;
    }

    /// The house's arms behind the head: its realm's cloth (or trim, by the house's field) with its own charge.
    static void Background(Color[] px, Face f, Heraldry side)
    {
        Color field = f.Field % 2 == 0 ? side.ClothDark : Shade(side.Cloth, 0.7f), charge = Color.Lerp(side.Trim, field, 0.55f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x - 24) / 24f, v = (y - 30) / 30f;
                bool on = f.Charge switch
                {
                    Emblem.Chevron => Math.Abs(y - (40 - Math.Abs(x - 24) * 0.9f)) < 3,
                    Emblem.Saltire => Math.Abs(Math.Abs(u) - Math.Abs(v)) < 0.12f,
                    Emblem.Lozenge => Math.Abs(u) + Math.Abs(v) < 0.55f,
                    Emblem.Bend => Math.Abs(u - v) < 0.18f,
                    Emblem.Cross => Math.Abs(u) < 0.12f || Math.Abs(v) < 0.1f,
                    Emblem.Pale => Math.Abs(u) < 0.22f,
                    Emblem.Fess => Math.Abs(v + 0.1f) < 0.16f,
                    Emblem.Sun => Math.Abs(MathF.Sqrt(u * u + v * v) - 0.5f) < 0.08f,
                    _ => false,
                };
                if (f.Field >= 2 && x < W / 2) on = !on && f.Field == 3; // a parted field
                float vig = 1 - 0.35f * (u * u + v * v);
                px[y * W + x] = Shade(on ? charge : field, vig);
            }
        for (int x = 0; x < W; x++) { px[x] = side.Trim; px[(H - 1) * W + x] = side.Trim; }
        for (int y = 0; y < H; y++) { px[y * W] = side.Trim; px[y * W + W - 1] = side.Trim; }
    }

    static Color Shade(Color c, float k) => new((int)Math.Clamp(c.R * k, 0, 255), (int)Math.Clamp(c.G * k, 0, 255), (int)Math.Clamp(c.B * k, 0, 255));
}

/// The GPU side: one small texture per face, painted on first use and kept while the face stays the same.
public static class Portraits
{
    static readonly Dictionary<(Face, bool), Texture2D> cache = new();

    /// The face's texture; `half` is a 24x30 version averaged from the full one, for small wells (point sampling would drop pixels).
    public static Texture2D Of(GraphicsDevice gd, Face f, bool half = false)
    {
        if (cache.TryGetValue((f, half), out var t)) return t;
        if (cache.Count > 600) { foreach (var old in cache.Values) old.Dispose(); cache.Clear(); }
        int W = PortraitPainter.W, H = PortraitPainter.H;
        var px = PortraitPainter.Paint(f);
        if (half)
        {
            var small = new Color[W / 2 * (H / 2)];
            for (int y = 0; y < H / 2; y++)
                for (int x = 0; x < W / 2; x++)
                    small[y * (W / 2) + x] = new Color((px[2 * y * W + 2 * x].ToVector4() + px[2 * y * W + 2 * x + 1].ToVector4() + px[(2 * y + 1) * W + 2 * x].ToVector4() + px[(2 * y + 1) * W + 2 * x + 1].ToVector4()) / 4);
            px = small; W /= 2; H /= 2;
        }
        t = new Texture2D(gd, W, H);
        t.SetData(px);
        return cache[(f, half)] = t;
    }
}
