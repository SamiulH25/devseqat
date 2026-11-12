using Microsoft.Xna.Framework;

namespace Ironfield;

/// Paints every unit sprite at startup (no image files) and packs them into one atlas. Headless: the self-test builds it too.
///
/// Keys are "part.variant.view/palette": e.g. "torso.mail.f/1" is a crimson mail torso seen from the front.
/// Views: f (front), s (side, facing right; mirrored for left), b (back). Palettes: 0 azure, 1 crimson, 2 gold (you),
/// n = neutral horse coat (tinted per horse at draw time). Axial parts (limbs, weapons, horse legs, tail) have their
/// top row at the far end (hand, foot, tip, hoof). Long horse parts run rear (left) to front (right), top edge up.
/// Sizes in pixels times Style.Px give the part's size in metres; the puppet skeleton in Puppet.cs relies on them.
public sealed class SpriteSheet
{
    public const int W = 1024, H = 1024;
    public readonly Color[] Pixels = new Color[W * H];
    public readonly Dictionary<string, Rectangle> Rects = new();
    readonly List<(string Key, Canvas C)> pending = new();

    public static readonly Dictionary<string, float> Grip = new()
    {
        ["sword"] = 0.233f, ["longsword"] = 0.17f, ["axe"] = 0.2f, ["spear"] = 0.3f, ["pike"] = 0.3f, ["lance"] = 0.3f, ["bow"] = 0.5f, ["longbow"] = 0.5f,
        ["crossbow"] = 0.55f, ["greatsword"] = 0.12f, ["daneaxe"] = 0.3f, ["club"] = 0.22f, ["staff"] = 0.55f, ["mace"] = 0.2f,
        ["halberd"] = 0.35f, ["glaive"] = 0.35f, ["javelin"] = 0.45f, ["sling"] = 0.1f, ["scimitar"] = 0.22f, ["falchion"] = 0.24f, ["warhammer"] = 0.3f,
    };

    static readonly string[] Views = { "f", "s", "b" };

    public SpriteSheet()
    {
        for (int p = 0; p < Style.Sides.Length; p++) Side(p);
        Horse();
        Wolf();
        Spider();
        Bear();
        Wyvern();
        Visages();
        Pack();
    }

    void Add(string key, Canvas c) => pending.Add((key, c));

    /// How many rows of the atlas the packed sprites use (of H): each palette paints a full set of parts.
    public int Filled { get; private set; }

    void Pack()
    {
        int x = 0, y = 0, rowH = 0;
        foreach (var (key, c) in pending.OrderByDescending(p => p.C.H).ThenByDescending(p => p.C.W))
        {
            int w = c.W + 2, h = c.H + 2;
            if (x + w > W) { x = 0; y += rowH; rowH = 0; }
            if (y + h > H) throw new InvalidOperationException("sprite atlas full");
            var px = c.Render();
            for (int j = 0; j < h; j++) Array.Copy(px, j * w, Pixels, (y + j) * W + x, w);
            Rects[key] = new Rectangle(x, y, w, h);
            x += w;
            rowH = Math.Max(rowH, h);
        }
        Filled = y + rowH;
        pending.Clear();
    }

    // ---------- materials for one side ----------

    Mat cloth = null!, clothDk = null!, trim = null!, quilt = null!, linen = null!, skin = null!, hair = null!, leather = null!,
        wood = null!, steel = null!, plate = null!, iron = null!, mail = null!, brass = null!, ink = null!, blood = null!, mark = null!, fur = null!,
        gskin = null!, rag = null!, glow = null!, bone = null!, pale = null!, shroud = null!, robe = null!, wisp = null!;
    Emblem charge;

    void Side(int p)
    {
        var h = Style.Sides[p];
        cloth = new(h.Cloth, Pattern.Cloth); clothDk = new(h.ClothDark, Pattern.Cloth); trim = new(h.Trim);
        quilt = new(Color.Lerp(h.Cloth, Style.Linen, 0.2f), Pattern.Quilt); linen = new(Style.Linen, Pattern.Cloth);
        skin = new(Style.Skin); hair = new(Style.Hair, Pattern.Hair); leather = new(Style.Leather); wood = new(Style.Wood, Pattern.Wood);
        steel = new(Style.Steel); plate = new(Color.Lerp(Style.Steel, Style.Iron, 0.5f), Pattern.Plate); iron = new(Style.Iron);
        mail = new(Color.Lerp(Style.Steel, Style.Iron, 0.35f), Pattern.Mail);
        brass = new(Style.Brass); fur = new(new Color(122, 98, 72), Pattern.Hair); ink = Mat.Flat(new Color(32, 24, 30)); blood = new(Style.Blood); mark = Mat.Flat(h.Trim);
        gskin = new(Style.GoblinSkin); rag = new(new Color(110, 92, 70), Pattern.Cloth); glow = Mat.Flat(new Color(236, 206, 70));
        bone = new(Style.Bone); pale = new(new Color(146, 152, 136)); shroud = new(new Color(128, 122, 106), Pattern.Cloth);
        robe = new(Color.Lerp(new Color(46, 36, 54), h.ClothDark, 0.15f), Pattern.Cloth); wisp = Mat.Flat(new Color(150, 236, 170));
        charge = h.Charge;
        string s = "/" + p;

        foreach (var v in Views)
        {
            Add($"head.{v}{s}", Head(v));
            Add($"head.goblin.{v}{s}", GoblinHead(v));
            Add($"head.troll.{v}{s}", TrollHead(v));
            Add($"head.skull.{v}{s}", SkullHead(v));
            Add($"head.ghoul.{v}{s}", GhoulHead(v));
            foreach (var helm in Look.Heads.Skip(1)) Add($"helm.{helm}.{v}{s}", Helm(helm, v));
            foreach (var body in Look.Bodies) Add($"torso.{body}.{v}{s}", Torso(body, v));
            Add($"tabard.{v}{s}", Tabard(v));
            foreach (var feet in Look.FeetKinds) Add($"boot.{feet}.{v}{s}", Boot(feet, v));
        }
        foreach (var body in Look.Bodies) Add($"sleeve.{body}{s}", Sleeve(body));
        foreach (var legs in Look.LegsKinds) Add($"leg.{legs}{s}", Leg(legs));
        foreach (var hands in Look.HandsKinds) Add($"hand.{hands}{s}", Hand(hands));
        foreach (var shield in Look.Shields.Skip(1)) Add($"shield.{shield}{s}", Shield(shield));
        foreach (var weapon in Look.Weapons) Add($"{weapon}{s}", Weapon(weapon));
        Add($"arrow{s}", Arrow());
        Add($"quiver{s}", Quiver());
        Add($"plume{s}", Plume());
        Add($"cape{s}", Cape());
        Add($"blood{s}", Blood());
        Add($"splat{s}", Splat());
        Add($"flag{s}", Flag());
        // Company banners (F1): the side's field with each company's own charge.
        foreach (var e in Enum.GetValues<Emblem>()) { var keep = charge; charge = e; Add($"flag.{(int)e}{s}", Flag()); charge = keep; }
        var white = new Canvas(4, 4);
        white.Area((_, _) => true, Mat.Flat(Color.White));
        Add($"white{s}", white);
        // A round puff for dust, smoke and mist motes (docs/archive/MAGIC_AURA.md 6): squares read as boxes when they are large.
        var puff = new Canvas(16, 16);
        puff.Area((x, y) => (x - 8) * (x - 8) + (y - 8) * (y - 8) <= 56, Mat.Flat(Color.White));
        Add($"puff{s}", puff);
        HorseGear(s);
    }

    // ---------- people ----------

    static bool InEl(float x, float y, float cx, float cy, float rx, float ry) => (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1;

    Canvas Head(string v) => Head(v, skin, hair, 0);

    /// Every man's face (Variety): each skin with each hair colour and each beard, painted once in the neutral palette.
    void Visages()
    {
        var keep = (skin, hair);
        for (int i = 0; i < Variety.Visages; i++)
        {
            var (sk, hr, style) = Variety.Parts(i);
            skin = new(Variety.Skins[sk]);
            hair = new(Variety.Hairs[hr], Pattern.Hair);
            foreach (var v in Views) Add($"head.v{i}.{v}/n", Head(v, skin, hair, style));
        }
        (skin, hair) = keep;
    }

    /// A man's head. `style`: 0 clean-shaven, 1 full beard, 2 moustache, 3 bald and bearded.
    Canvas Head(string v, Mat sk, Mat hr, int style)
    {
        var c = new Canvas(15, 15);
        bool bald = style == 3, beard = style is 1 or 3;
        bool Skull(float x, float y) => InEl(x, y, 7.5f, 7.5f, 7f, 7.3f);
        c.Ellipse(7.5f, 7.5f, 7f, 7.3f, sk);
        switch (v)
        {
            case "f":
                if (!bald) c.Area((x, y) => Skull(x, y) && y < 4.2f, hr);
                else c.Area((x, y) => Skull(x, y) && y > 3.5f && y < 7 && (x < 1.6f || x > 13.4f), hr); // a fringe over the ears
                foreach (int ex in new[] { 4, 10 }) { c.Set(ex, 7, ink); c.Set(ex, 8, ink); }
                c.Shade(7, 9, -0.35f); c.Shade(7, 10, -0.45f);
                if (beard)
                {
                    c.Area((x, y) => Skull(x, y) && y > 9.8f && !(y < 12 && x > 5.6f && x < 9.4f), hr);
                    c.Area((x, y) => Skull(x, y) && y > 7.5f && y <= 9.8f && (x < 2.4f || x > 12.6f), hr);
                    for (int x = 6; x <= 8; x++) c.Set(x, 11, hr, 0.2f);
                    c.Set(7, 12, ink, 0.3f);
                }
                else if (style == 2) { for (int x = 5; x <= 9; x++) c.Set(x, 11, hr); c.Set(4, 12, hr); c.Set(10, 12, hr); }
                else for (int x = 6; x <= 8; x++) c.Shade(x, 12, -0.35f);
                break;
            case "s":
                if (!bald) c.Area((x, y) => Skull(x, y) && (x < 6.5f || y < 4.2f), hr);
                else c.Area((x, y) => Skull(x, y) && x < 6.5f && y > 5, hr);
                c.Ellipse(6.5f, 8.5f, 1.6f, 2.2f, sk, -0.15f);
                c.Set(11, 7, ink); c.Set(11, 8, ink);
                c.Set(14, 9, sk, 0.5f);
                if (beard) c.Area((x, y) => Skull(x, y) && (y > 10.5f || (x > 5 && x < 9 && y > 8.5f)) && !(x > 12 && y < 12), hr);
                else if (style == 2) { c.Set(13, 11, hr); c.Set(12, 11, hr); c.Set(12, 12, hr); }
                else { c.Shade(12, 12, -0.3f); c.Shade(13, 12, -0.3f); }
                break;
            default:
                if (!bald) c.Area((x, y) => Skull(x, y) && y < 12.5f, hr);
                else c.Area((x, y) => Skull(x, y) && y > 6 && y < 12.5f, hr);
                if (beard) { c.Set(1, 11, hr); c.Set(13, 11, hr); c.Set(1, 12, hr); c.Set(13, 12, hr); }
                break;
        }
        return c;
    }

    /// A goblin's head: green, with long ears sticking out to the sides, yellow eyes and a wide mouth of teeth.
    Canvas GoblinHead(string v)
    {
        var c = new Canvas(21, 15);
        c.Ellipse(10.5f, 8f, 6.2f, 6.4f, gskin);
        switch (v)
        {
            case "f":
                c.Body(5, 8, y => (0.5f + (y - 5) * 1.2f, 5.5f), gskin, -0.1f);    // left ear
                c.Body(5, 8, y => (15.5f, 20.5f - (y - 5) * 1.2f), gskin, -0.1f);  // right ear
                foreach (int ex in new[] { 8, 13 }) { c.Set(ex, 7, glow); c.Set(ex, 8, ink); }
                c.Shade(10, 9, -0.35f); c.Shade(11, 9, -0.35f);                     // nose
                for (int x = 7; x <= 14; x++) c.Set(x, 11, ink);                    // grin
                foreach (int tx in new[] { 8, 10, 12 }) c.Set(tx, 11, Mat.Flat(Style.Linen));
                break;
            case "s":
                c.Body(4, 7, y => (1.5f + (y - 4) * 1.4f, 7f), gskin, -0.15f);     // ear swept back
                c.Set(14, 7, glow); c.Set(14, 8, ink);
                c.Body(8.5f, 10.5f, y => (15, 18.5f), gskin);                       // long nose
                for (int x = 12; x <= 16; x++) c.Set(x, 11, ink);
                break;
            default:
                c.Body(5, 8, y => (0.5f + (y - 5) * 1.2f, 5.5f), gskin, -0.2f);
                c.Body(5, 8, y => (15.5f, 20.5f - (y - 5) * 1.2f), gskin, -0.2f);
                for (int x = 6; x <= 15; x++) c.Shade(x, 13, -0.3f);
                break;
        }
        return c;
    }

    /// A troll's head: low and heavy, a jutting jaw with tusks, small eyes under a brow ridge, moss in the hair.
    Canvas TrollHead(string v)
    {
        var c = new Canvas(19, 17);
        c.Ellipse(9.5f, 8.5f, 7.6f, 7.8f, gskin, -0.15f);
        c.Ellipse(9.5f, 3.5f, 7f, 3.2f, fur, -0.1f); // a mat of hair and moss
        switch (v)
        {
            case "f":
                for (int x = 4; x <= 15; x++) c.Shade(x, 6, -0.35f); // brow ridge
                foreach (int ex in new[] { 7, 12 }) c.Set(ex, 7, glow);
                c.Ellipse(9.5f, 9.5f, 1.8f, 1.2f, gskin, -0.3f);    // broad nose
                c.Body(11, 16, y => (3.5f, 15.5f), gskin, 0.05f);   // jutting jaw
                for (int x = 5; x <= 14; x++) c.Set(x, 12, ink);
                foreach (int tx in new[] { 5, 14 }) { c.Set(tx, 11, Mat.Flat(Style.Linen)); c.Set(tx, 10, Mat.Flat(Style.Linen)); } // tusks
                break;
            case "s":
                c.Set(13, 7, glow); for (int x = 10; x <= 16; x++) c.Shade(x, 6, -0.35f);
                c.Body(8, 11, y => (14, 18.5f), gskin, -0.1f);      // nose
                c.Body(11, 16, y => (7, 17f), gskin, 0.05f);        // jaw
                for (int x = 12; x <= 17; x++) c.Set(x, 12, ink);
                c.Set(16, 11, Mat.Flat(Style.Linen)); c.Set(16, 10, Mat.Flat(Style.Linen));
                break;
            default:
                c.Ellipse(9.5f, 8, 7f, 6f, fur, -0.15f);
                break;
        }
        return c;
    }

    /// A skull: bare bone, dark sockets with a cold green light in them, a row of teeth and no lower lip.
    Canvas SkullHead(string v)
    {
        var c = new Canvas(15, 15);
        c.Ellipse(7.5f, 6.5f, 6.4f, 6.3f, bone);
        c.Body(10, 15, y => (3.5f, 11.5f - (y - 10) * 0.3f), bone, -0.1f); // the jaw
        switch (v)
        {
            case "f":
                foreach (int ex in new[] { 5, 10 }) { c.Ellipse(ex, 7, 1.6f, 1.5f, ink); c.Set(ex, 7, wisp); }
                c.Set(7, 10, ink); c.Set(8, 10, ink);                                // nose hole
                for (int x = 4; x <= 11; x++) c.Set(x, 12, x % 2 == 0 ? ink : bone); // teeth
                break;
            case "s":
                c.Ellipse(10, 7, 1.5f, 1.5f, ink); c.Set(10, 7, wisp);
                c.Set(13, 10, ink);
                for (int x = 9; x <= 12; x++) c.Set(x, 12, x % 2 == 0 ? ink : bone);
                c.Shade(4, 9, -0.3f); c.Shade(5, 9, -0.3f);                          // the cheekbone's hollow
                break;
            default:
                for (int x = 3; x <= 12; x++) c.Shade(x, 11, -0.3f);
                break;
        }
        return c;
    }

    /// A ghoul's head: grey, bald and gaunt, pale lights for eyes in deep sockets, a long jaw of yellow teeth.
    Canvas GhoulHead(string v)
    {
        var c = new Canvas(15, 17);
        c.Ellipse(7.5f, 7f, 6.2f, 6.6f, pale);
        c.Body(10, 17, y => (4f + (y - 10) * 0.25f, 11f - (y - 10) * 0.25f), pale, -0.1f); // a long jaw
        switch (v)
        {
            case "f":
                foreach (int ex in new[] { 5, 10 }) { c.Shade(ex - 1, 7, -0.4f); c.Shade(ex + 1, 7, -0.4f); c.Set(ex, 7, wisp); }
                c.Shade(7, 10, -0.4f); c.Shade(8, 10, -0.4f);
                for (int x = 5; x <= 10; x++) c.Set(x, 13, ink);
                foreach (int tx in new[] { 5, 7, 9 }) c.Set(tx, 13, Mat.Flat(new Color(214, 200, 140)));
                break;
            case "s":
                c.Set(10, 7, wisp); c.Shade(9, 7, -0.4f);
                c.Body(8, 11, y => (12, 14.5f), pale, -0.1f);
                for (int x = 9; x <= 12; x++) c.Set(x, 13, ink);
                break;
            default:
                for (int x = 3; x <= 12; x++) c.Shade(x, 3, 0.15f); // a bare scalp
                break;
        }
        return c;
    }

    Canvas Helm(string kind, string v)
    {
        switch (kind)
        {
            case "cap":
            {
                var c = new Canvas(18, 10);
                c.Ellipse(9, 10, 8.2f, 9.6f, linen);
                if (v == "s") c.Rect(1, 5, 5, 10, linen, -0.1f);
                for (int x = 0; x < 18; x++) c.Shade(x, 8, -0.35f);
                return c;
            }
            case "hood":
            {
                var c = new Canvas(18, 14);
                c.Ellipse(9, 7.5f, 8.5f, 7.5f, clothDk);
                c.Rect(0.5f, 7, 17.5f, 14, clothDk, -0.05f);
                if (v == "f") for (int y = 4; y < 14; y++) for (int x = 0; x < 18; x++) { if (InEl(x + 0.5f, y + 0.5f, 9, 9.8f, 5.2f, 5.6f)) c.Clear(x, y); }
                if (v == "s") for (int y = 4; y < 14; y++) for (int x = 9; x < 18; x++) { if (InEl(x + 0.5f, y + 0.5f, 15.5f, 10f, 4.6f, 5.6f)) c.Clear(x, y); }
                return c;
            }
            case "kettle":
            {
                var c = new Canvas(18, 12);
                c.Fill((x, y) => InEl(x, y, 9, 7.5f, 6.2f, 7f) && y < 7.6f, (x, y) => new Vector3((x - 9) / 7, (y - 7.5f) / 8, 0.6f), plate);
                c.Rect(0.5f, 7.6f, 17.5f, 9.6f, plate, -0.05f);
                for (int x = 2; x < 16; x++) c.Set(x, 10, iron, 0.0f);
                for (int x = 4; x < 14; x += 3) c.Set(x, 6, brass, 0.6f);
                return c;
            }
            case "nasal":
            {
                var c = new Canvas(18, 13);
                c.Body(0.3f, 9, y => (9 - 1 - y * 0.85f, 9 + 1 + y * 0.85f), plate);
                c.Rect(1, 8, 17, 10.5f, iron);
                if (v == "f") c.Rect(8, 8, 10, 13, iron, 0.1f);
                if (v == "s") c.Rect(15, 8, 17, 13, iron, 0.1f);
                if (v != "f") c.Rect(2, 10.5f, v == "s" ? 11 : 16, 13, mail);
                return c;
            }
            case "sallet":
            {
                var c = new Canvas(18, 13);
                c.Fill((x, y) => InEl(x, y, 9, 8.2f, 8, 7.7f) && y < 9.3f, (x, y) => new Vector3((x - 9) / 9, (y - 8) / 9, 0.6f), plate);
                if (v == "s") c.Body(7.5f, 12.8f, y => (0.3f, 9 - (y - 7.5f) * 0.6f), plate, -0.1f);
                else if (v == "b") c.Body(8.5f, 12.8f, y => (1 + (y - 8.5f) * 0.4f, 17 - (y - 8.5f) * 0.4f), plate, -0.1f);
                else for (int x = 1; x < 17; x++) c.Set(x, 9, iron, 0.2f);
                return c;
            }
            case "spangen":
            {
                // The northern clans' helm: a riveted cone with a nasal, brass bands and short horns.
                var c = new Canvas(24, 15);
                c.Body(2.3f, 11, y => (12 - 1 - (y - 2.3f) * 0.82f, 12 + 1 + (y - 2.3f) * 0.82f), plate);
                c.Rect(4.5f, 9.5f, 19.5f, 11.5f, brass);
                for (int y = 3; y < 10; y++) c.Set(12, y, brass, 0.4f);
                if (v == "f") c.Rect(11, 10, 13, 15, iron, 0.1f);
                if (v == "s") c.Rect(18, 10, 20, 15, iron, 0.1f);
                if (v != "s")
                    for (int i = 0; i < 6; i++) { c.Set(4 - i / 2, 8 - i, linen, 0.3f); c.Set(20 + i / 2, 8 - i, linen, 0.3f); c.Set(5 - i / 2, 8 - i, linen); c.Set(19 + i / 2, 8 - i, linen); }
                else for (int i = 0; i < 6; i++) { c.Set(11 - i / 2, 7 - i, linen, 0.3f); c.Set(12 - i / 2, 7 - i, linen); }
                return c;
            }
            case "turban":
            {
                // Linen wound in bands over a small spiked steel cap (the south).
                var c = new Canvas(20, 14);
                c.Fill((x, y) => InEl(x, y, 10, 10, 9.4f, 7.6f) && y < 12.5f && y > 3, (x, y) => new Vector3((x - 10) / 10, (y - 9) / 9, 0.6f), linen);
                for (int x = 0; x < 20; x++) for (int y = 4; y < 13; y++) if ((y + x / 3) % 3 == 0) c.Shade(x, y, -0.22f);
                c.Body(0.3f, 4.5f, y => (10 - 0.4f - y * 0.55f, 10 + 0.4f + y * 0.55f), steel);
                if (v != "b") c.Set(v == "s" ? 15 : 10, 8, brass, 0.7f);
                return c;
            }
            case "coif":
            {
                // A mail hood: rings over the crown and down to the shoulders, open for the face.
                var c = new Canvas(18, 15);
                c.Ellipse(9, 7.5f, 8.4f, 7.4f, mail);
                c.Rect(0.6f, 7, 17.4f, 15, mail, -0.05f);
                if (v == "f") for (int y = 4; y < 14; y++) for (int x = 0; x < 18; x++) { if (InEl(x + 0.5f, y + 0.5f, 9, 9.6f, 5.4f, 5.4f)) c.Clear(x, y); }
                if (v == "s") for (int y = 4; y < 14; y++) for (int x = 9; x < 18; x++) { if (InEl(x + 0.5f, y + 0.5f, 15.6f, 9.8f, 4.4f, 5.4f)) c.Clear(x, y); }
                for (int x = 1; x < 17; x++) c.Shade(x, 14, -0.3f);
                if (v != "b") for (int x = 3; x < 15; x++) c.Set(x, 3, leather, 0.35f); // the band that ties it at the brow
                return c;
            }
            case "steppe":
            {
                // A tall steel cone with a fur brim and a tassel at the point (the Khaganate).
                var c = new Canvas(20, 17);
                c.Body(2, 13, y => (10 - 0.8f - (y - 2) * 0.68f, 10 + 0.8f + (y - 2) * 0.68f), plate);
                c.Rect(1.5f, 11.5f, 18.5f, 15.5f, fur);
                for (int x = 1; x < 19; x += 2) c.Shade(x, 15, -0.3f);
                c.Body(0, 2.5f, y => (9.2f - y * 0.2f, 10.8f + y * 0.2f), trim);
                for (int y = 4; y < 11; y++) c.Set(10, y, brass, 0.4f);
                return c;
            }
            case "spike":
            {
                // A tall onion-pointed steel helm with a mail veil over the neck and cheeks (the south).
                var c = new Canvas(18, 18);
                c.Fill((x, y) => InEl(x, y, 9, 10, 7.4f, 6.6f) && y < 10.5f && y > 3.5f, (x, y) => new Vector3((x - 9) / 8, (y - 9) / 8, 0.6f), plate);
                c.Body(0.3f, 4.5f, y => (9 - 0.3f - y * 0.4f, 9 + 0.3f + y * 0.4f), steel);
                c.Rect(1.4f, 10, 16.6f, 11.5f, brass);
                if (v == "f") { c.Rect(1.6f, 11.5f, 4.5f, 18, mail); c.Rect(13.5f, 11.5f, 16.4f, 18, mail); }
                else c.Rect(v == "s" ? 1.6f : 1.6f, 11.5f, v == "s" ? 12 : 16.4f, 18, mail);
                return c;
            }
            case "barbute":
            {
                // A close steel helm down to the jaw with a T-shaped opening for the eyes and mouth (the League).
                var c = new Canvas(18, 19);
                c.Fill((x, y) => InEl(x, y, 9, 9.5f, 8.2f, 9.2f) && y > 0.3f, (x, y) => new Vector3((x - 9) / 9, (y - 8) / 10, 0.6f), plate);
                if (v == "f") { for (int x = 4; x < 14; x++) c.Set(x, 8, ink); for (int y = 8; y < 17; y++) { c.Set(8, y, ink); c.Set(9, y, ink); } }
                else if (v == "s") for (int y = 8; y < 17; y++) c.Set(16, y, ink);
                for (int y = 1; y < 8; y++) c.Set(9, y, iron, 0.3f); // the ridge
                return c;
            }
            case "furcap":
            {
                // A round fur cap with flaps over the ears, a small steel crown on top (the steppe's poor riders).
                var c = new Canvas(20, 15);
                c.Ellipse(10, 8, 9.2f, 7, fur);
                c.Rect(0.8f, 7, 19.2f, 11, fur);
                if (v == "f") { c.Rect(0.8f, 11, 4, 15, fur, -0.1f); c.Rect(16, 11, 19.2f, 15, fur, -0.1f); }
                else if (v == "s") c.Rect(5, 11, 9, 15, fur, -0.1f);
                c.Ellipse(10, 4, 4.2f, 3.4f, plate);
                for (int x = 1; x < 19; x += 2) c.Shade(x, 10, -0.3f);
                return c;
            }
            case "bucket":
            {
                // A flat-topped helm with a cross of brass over the eye slit (the Order).
                var c = new Canvas(18, 20);
                c.Rect(1.6f, 0.3f, 16.4f, 20, plate);
                for (int x = 0; x < 18; x++) c.Shade(x, 1, -0.4f);
                if (v == "f")
                {
                    for (int x = 3; x < 15; x++) if (x is not (8 or 9)) c.Set(x, 8, ink);
                    c.Rect(8, 3, 10, 18, brass, 0.1f);
                    c.Rect(3, 10, 15, 11.6f, brass, 0.1f);
                }
                else if (v == "s") for (int x = 10; x < 17; x++) c.Set(x, 8, ink);
                else c.Rect(8, 3, 10, 18, brass, 0.3f);
                return c;
            }
            default: // greathelm
            {
                var c = new Canvas(18, 21);
                c.Rect(1.3f, 0.3f, 16.7f, 21, plate);
                for (int x = 0; x < 18; x++) { c.Shade(x, 0, -0.5f); c.Shade(x, 1, -0.3f); }
                if (v == "f")
                {
                    for (int x = 3; x < 15; x++) if (x is not (8 or 9)) { c.Set(x, 8, ink); c.Set(x, 9, ink); }
                    c.Rect(8, 10, 10, 20, brass, 0.1f);
                    foreach (var (bx, by) in new[] { (12, 13), (14, 13), (12, 15), (14, 15), (12, 17), (14, 17) }) c.Set(bx, by, ink);
                }
                else if (v == "s") { for (int x = 10; x < 17; x++) { c.Set(x, 8, ink); c.Set(x, 9, ink); } }
                else
                {
                    // The back: rounded plates meeting at a riveted seam down the middle, a band round the crown and a
                    // dark lip over the neck, so a rank seen from behind reads as helms, not as one pale slab.
                    for (int y = 0; y < 21; y++)
                        for (int x = 0; x < 18; x++) c.Shade(x, y, -0.18f - MathF.Abs(x - 8.5f) * 0.035f - (y > 12 ? (y - 12) * 0.03f : 0));
                    for (int y = 2; y < 18; y++) { c.Set(8, y, ink); c.Shade(9, y, 0.35f); }
                    for (int x = 2; x < 17; x++) { c.Shade(x, 6, -0.35f); c.Shade(x, 7, 0.2f); }
                    for (int x = 2; x < 17; x++) { c.Set(x, 18, ink); c.Shade(x, 19, -0.5f); c.Shade(x, 20, -0.5f); }
                    foreach (var (bx, by) in new[] { (4, 6), (13, 6), (10, 10), (10, 14) }) c.Set(bx, by, brass, 0.6f);
                }
                return c;
            }
        }
    }

    static (float, float) TorsoSpan(float y)
    {
        float half = y < 3 ? 6.5f + y * 1.0f : y < 14 ? 9.5f - (y - 3) * 0.08f : 8.6f + (y - 14) * 0.15f;
        return (10 - half, 10 + half);
    }

    static (float, float) SideSpan(float y)
    {
        float half = y < 3 ? 3.5f + y * 0.9f : 6.2f;
        return (6.5f - half, 6.5f + half + (y > 3 && y < 12 ? 0.6f : 0));
    }

    Canvas Torso(string body, string v)
    {
        bool side = v == "s";
        var c = new Canvas(side ? 13 : 20, 23);
        Func<float, (float, float)> span = side ? SideSpan : TorsoSpan;
        var m = body switch { "gambeson" => quilt, "mail" => mail, "plate" => plate, "fur" => clothDk, "rags" => rag, "shroud" => shroud, "robe" => robe,
            "lamellar" => plate, "deel" => cloth, "brigandine" => clothDk, _ => cloth };
        if (body == "bones") return Ribs(c, span, side);
        if (body == "plate") PlateTorso(c, v);
        else
        {
            c.Body(0, 23, span, m);
            if (body == "tunic") c.Body(20.5f, 23, span, clothDk);
        }
        // Belt with a buckle.
        if (body != "plate") c.Body(13.5f, 15.6f, span, leather); // (the plate map has its own)
        if (v == "f" && body != "plate") { c.Set(9, 14, brass, 0.7f); c.Set(10, 14, brass, 0.7f); c.Set(9, 15, brass, 0.4f); c.Set(10, 15, brass, 0.4f); }
        if (v == "f" && body is "tunic" or "gambeson")
            foreach (var (x, y) in new[] { (9, 0), (10, 0), (9, 1), (10, 1), (10, 2) }) c.Set(x, y, skin, 0.4f);
        if (body == "gambeson") // a thick quilted coat with a standing collar
        {
            for (int x = 0; x < c.W; x++) { c.Shade(x, 0, 0.15f); c.Shade(x, 1, 0.1f); c.Shade(x, 2, -0.35f); }
            if (v == "f") for (int y = 3; y < 23; y++) if (y < 13 || y > 15) c.Shade(10, y, -0.3f); // laced down the front
        }
        if (body == "rags") // bare green shoulders and a torn hem, patched with the side's cloth
        {
            c.Area((x, y) => y < 3.5f && x >= span(y).Item1 && x <= span(y).Item2, gskin);
            for (int x = 1; x < c.W; x += 3) { c.Clear(x, 22); c.Clear(x + 1, 22); c.Clear(x, 21); }
            c.Rect(c.W * 0.55f, 8, c.W * 0.55f + 3.5f, 11.5f, cloth);
        }
        if (body == "shroud") // grey shoulders through a torn grave cloth
        {
            c.Area((x, y) => y < 3.5f && x >= span(y).Item1 && x <= span(y).Item2, pale);
            for (int x = 0; x < c.W; x += 2) { c.Clear(x, 22); if (x % 4 == 0) c.Clear(x, 21); }
        }
        if (body == "robe") // a dark robe to the ground, a pale cord and a hem of the side's colour
        {
            c.Body(13.5f, 15.6f, span, linen, -0.2f);
            for (int x = 0; x < c.W; x++) c.Shade(x, 22, -0.3f);
        }
        if (body == "lamellar") // rows of small laced plates, a cloth skirt below the belt
        {
            for (int y = 1; y < 14; y++) for (int x = 0; x < c.W; x++)
            {
                int row = y % 3;
                if (row == 0) c.Shade(x, y, -0.4f);                                  // the gap under each row
                else if (row == 1) c.Shade(x, y, 0.18f);                             // the lit top edge of the plates
                if (row != 0 && (x + (y / 3 % 2) * 2) % 4 == 0) c.Shade(x, y, -0.3f); // the joins, staggered row on row
            }
            for (int y = 3; y < 14; y += 3) for (int x = 2 + y / 3 % 2 * 2; x < c.W - 1; x += 4) if (c.Has(x, y)) c.Set(x, y, trim, 0.4f); // the lacing
            c.Body(15.6f, 23, span, clothDk);
            for (int x = 0; x < c.W; x++) c.Shade(x, 22, -0.25f);
        }
        if (body == "mail") // a hauberk: a padded collar showing at the neck, rings to the thighs, a ragged hem
        {
            c.Body(0, 2.2f, span, quilt, 0.05f);
            for (int x = 0; x < c.W; x += 2) if (c.Has(x, 22)) c.Clear(x, 22);
            if (v == "f") for (int y = 16; y < 23; y++) c.Shade(10, y, -0.35f); // the split skirt for walking and riding
        }
        if (body == "deel") // a long coat wrapped left over right, a trim down the overlap and a wide sash
        {
            if (v == "f") for (int y = 0; y < 23; y++) { int x = (int)(6 + y * 0.35f); c.Set(x, y, trim, 0.5f); c.Set(x + 1, y, trim, 0.35f); }
            c.Body(13, 16, span, trim, -0.1f);
            c.Area((x, y) => y < 3 && x >= span(y).Item1 && x <= span(y).Item2, fur);
        }
        if (body == "brigandine") // dark cloth over riveted plates
            for (int y = 2; y < 13; y += 3) for (int x = 2 + y % 2; x < c.W - 1; x += 3) c.Set(x, y, brass, 0.65f);
        if (body == "fur") // a shaggy mantle over the shoulders
            c.Area((x, y) => y < 6.5f + 1.2f * MathF.Sin(x * 1.7f) && x >= span(y).Item1 - 0.6f && x <= span(y).Item2 + 0.6f, fur);
        return c;
    }

    /// A plate harness, pixelled by hand (the light from the upper left): a gorget at the neck, domed pauldrons with a lame
    /// below each, a breastplate with a ridge and a gleam, the belt, two lames of faulds and a mail hem.
    /// Digits are tones of the steel (0 deep shadow .. 4 highlight); l/k leather lit/dark, b brass, m mail, '.' bare.
    static readonly string[] PlateFront =
    {
        "......23333221......",
        ".233..01111110..221.",
        "23b43.23333221.23b21",
        "34443211111111232210",
        "34433234342221232210",
        "23322134342221122110",
        "01111033342221011110",
        "23322333342221122110",
        "21110333342211201110",
        "..2334333422221110..",
        "..2334333422221110..",
        "..1233333422211100..",
        "..0111111111111110..",
        "..llllllllllllllll..",
        "..lllllllbblllllll..",
        "..kkkkkkkkkkkkkkkk..",
        ".233b333333222b2110.",
        ".011111111111111110.",
        "23333333332222221110",
        "01111111111111111110",
        "mmmmmmmmmmmmmmmmmmmm",
        "mmmmmmmmmmmmmmmmmmmm",
        "m.m.m.m.m.m.m.m.m.m.",
    };
    static readonly string[] PlateBack =
    {
        "......23333221......",
        ".233..23333221..221.",
        "23443.23333221.23321",
        "34443211111111232210",
        "34433233332221232210",
        "23322133332221122110",
        "01111033332221011110",
        "23322333332221122110",
        "21110333332211201110",
        "..2333333322221110..",
        "..2333333322221110..",
        "..1233333322211100..",
        "..0111111111111110..",
        "..llllllllllllllll..",
        "..llllllllllllllll..",
        "..kkkkkkkkkkkkkkkk..",
        ".233333333332222110.",
        ".011111111111111110.",
        "23333333332222221110",
        "01111111111111111110",
        "mmmmmmmmmmmmmmmmmmmm",
        "mmmmmmmmmmmmmmmmmmmm",
        "m.m.m.m.m.m.m.m.m.m.",
    };
    static readonly string[] PlateSide =
    {
        "....23332....",
        "...0111110...",
        "..233b43321..",
        ".23444433210.",
        ".34444333210.",
        ".23333322110.",
        ".01111111100.",
        "122333334321.",
        "1223333344321",
        "1223333344321",
        "1223333334321",
        "1122333333210",
        "0111111111110",
        "lllllllllllll",
        "llllllllllbll",
        "kkkkkkkkkkkkk",
        "2333333332210",
        "0111111111110",
        "2333333332210",
        "0111111111110",
        "mmmmmmmmmmmmm",
        "mmmmmmmmmmmmm",
        "m.m.m.m.m.m.m",
    };

    void PlateTorso(Canvas c, string v) => PixelMap(c, v == "s" ? PlateSide : v == "b" ? PlateBack : PlateFront, new Mat(plate.Ramp[2]));

    /// Paint a hand-made pixel map: tone digits in `metal`, and the letters l, k (leather), b (brass), m (mail).
    void PixelMap(Canvas c, string[] rows, Mat metal)
    {
        static float L(int tone) => 0.45f + (tone - 2) / 4f;
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                char ch = rows[y][x];
                if (ch >= '0' && ch <= '4') c.Set(x, y, metal, L(ch - '0'));
                else if (ch == 'l') c.Set(x, y, leather, L(2));
                else if (ch == 'k') c.Set(x, y, leather, L(1));
                else if (ch == 'b') c.Set(x, y, brass, L(3));
                else if (ch == 'm') c.Set(x, y, mail, L(y == rows.Length - 1 ? 1 : 2));
            }
    }

    /// A skeleton's chest: a spine, the ribs round it and the hip bones, with nothing between.
    Canvas Ribs(Canvas c, Func<float, (float, float)> span, bool side)
    {
        float mid = c.W / 2f;
        c.Rect(mid - 1, 0, mid + 1, 23, bone, -0.05f);                                   // spine
        c.Area((x, y) => y < 2.5f && x >= span(y).Item1 + 1 && x <= span(y).Item2 - 1, bone); // collar bones
        for (int y = 4; y < 14; y += 2)
        {
            var (l, r) = span(y);
            if (side) c.Line(mid, y, r - 1, y + 1, bone, 0.5f);
            else { c.Line(l + 1.5f, y + 1, mid, y, bone, 0.5f); c.Line(mid, y, r - 1.5f, y + 1, bone, 0.5f); }
        }
        c.Body(17, 21, y => (mid - 4.5f + (y - 17) * 0.6f, mid + 4.5f - (y - 17) * 0.6f), bone, -0.1f); // pelvis
        return c;
    }

    Canvas Tabard(string v)
    {
        bool side = v == "s";
        var c = new Canvas(side ? 13 : 20, 23);
        // It hangs from below the collar and inside the shoulders, so the armour shows at the neck and on the shoulders.
        if (side) c.Body(3.5f, 23, y => (3.6f, 10f), cloth);
        else c.Body(3.5f, 23, y => (5.1f - y * 0.04f, 14.9f + y * 0.04f), cloth);
        c.Border(trim);
        if (!side)
        {
            Emblem(c, 10, 9.5f, 4f);
            for (int y = 18; y < 23; y++) c.Clear(10, y);
        }
        c.Body(13.5f, 15.6f, side ? _ => (3.2f, 10.2f) : _ => (4.2f, 15.8f), leather);
        return c;
    }

    void Emblem(Canvas c, float cx, float cy, float s)
    {
        switch (charge)
        {
            case Ironfield.Emblem.Chevron:
                c.Line(cx - s, cy + s * 0.6f, cx, cy - s * 0.5f, mark, 0.45f, 3);
                c.Line(cx, cy - s * 0.5f, cx + s, cy + s * 0.6f, mark, 0.45f, 3);
                break;
            case Ironfield.Emblem.Saltire:
                c.Line(cx - s, cy - s, cx + s, cy + s, mark, 0.45f, 3);
                c.Line(cx + s, cy - s, cx - s, cy + s, mark, 0.45f, 3);
                break;
            case Ironfield.Emblem.Lozenge:
                c.Area((x, y) => MathF.Abs(x - cx) + MathF.Abs(y - cy) * 0.8f <= s, mark);
                break;
            case Ironfield.Emblem.Bend:
                c.Line(cx - s, cy - s, cx + s, cy + s, mark, 0.7f, 3);
                break;
            case Ironfield.Emblem.Cross:
                c.Line(cx, cy - s, cx, cy + s, mark, 0.5f, 3);
                c.Line(cx - s, cy - s * 0.2f, cx + s, cy - s * 0.2f, mark, 0.5f, 3);
                break;
            case Ironfield.Emblem.Pale:
                c.Line(cx, cy - s, cx, cy + s, mark, 0.8f, 3);
                break;
            case Ironfield.Emblem.Fess:
                c.Line(cx - s, cy, cx + s, cy, mark, 0.8f, 3);
                break;
            case Ironfield.Emblem.Sun:
                c.Area((x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= s * s * 0.36f, mark);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.PI / 4;
                    c.Line(cx + MathF.Cos(a) * s * 0.7f, cy + MathF.Sin(a) * s * 0.7f, cx + MathF.Cos(a) * s, cy + MathF.Sin(a) * s, mark, 0.35f, 3);
                }
                break;
            default: break; // no charge (brigands)
        }
    }

    Canvas Cape()
    {
        var c = new Canvas(20, 28);
        c.Body(0, 28, y => (2.5f - y * 0.08f, 17.5f + y * 0.08f), clothDk);
        for (int x = 0; x < 20; x += 5) { c.Clear(x, 27); c.Clear(x, 26); }
        return c;
    }

    Canvas Sleeve(string body)
    {
        var c = new Canvas(8, 20);
        switch (body)
        {
            case "gambeson":
                c.Rect(1, 0, 7, 20, quilt); c.Rect(1, 0, 7, 2.5f, leather);
                for (int y = 5; y < 20; y += 3) for (int x = 0; x < 8; x++) c.Shade(x, y, -0.3f); // quilted round the arm
                break;
            case "fur": c.Rect(1.5f, 0, 6.5f, 20, leather); c.Rect(1, 0, 7, 4, fur); break;
            case "rags": c.Rect(2, 0, 6, 20, gskin); c.Rect(1.5f, 0, 6.5f, 3, rag); break; // a thin bare arm
            case "bones": c.Rect(3, 0, 5, 20, bone); c.Ellipse(4, 10, 1.6f, 1.4f, bone, 0.1f); break; // a bare arm bone and its elbow
            case "shroud": c.Rect(2.2f, 0, 5.8f, 20, pale); c.Rect(1.5f, 0, 6.5f, 5, shroud); break;
            case "robe": c.Rect(1, 0, 7, 20, robe); c.Rect(0.5f, 16, 7.5f, 20, robe, -0.15f); break; // wide sleeves
            case "mail":
                c.Rect(1.5f, 0, 6.5f, 20, mail);
                c.Rect(1.5f, 0, 6.5f, 1.6f, leather); // a leather cuff at the wrist
                break;
            case "lamellar": c.Rect(1.5f, 0, 6.5f, 20, mail); c.Rect(1, 0, 7, 6, plate); for (int x = 0; x < 8; x++) c.Shade(x, 3, -0.3f); break;
            case "deel": c.Rect(1, 0, 7, 20, cloth); c.Rect(1, 16.5f, 7, 20, trim); break;
            case "brigandine": c.Rect(1.5f, 0, 6.5f, 20, mail); c.Rect(1, 0, 7, 7, clothDk); c.Set(3, 3, brass, 0.65f); c.Set(5, 5, brass, 0.65f); break;
            case "plate":
                // Vambrace (forearm), the elbow cop with its wing, lames and the upper cannon to the shoulder.
                c.Rect(1.5f, 0, 6.5f, 20, plate);
                c.Rect(1, 0, 7, 1.6f, plate, 0.1f);                                // the flared cuff
                c.Ellipse(4, 10, 3.6f, 2.6f, plate, 0.15f);
                c.Ellipse(1.6f, 10, 1.5f, 2f, plate, 0.25f);                      // the couter's wing
                for (int x = 0; x < 8; x++) { c.Shade(x, 2, -0.45f); c.Shade(x, 8, -0.35f); c.Shade(x, 12, -0.45f); c.Shade(x, 15, -0.4f); c.Shade(x, 17, -0.4f); }
                for (int y = 0; y < 20; y++) if (y < 8 || y > 12) c.Shade(3, y, 0.35f); // a gleam along the arm
                c.Set(4, 10, brass, 0.6f);
                break;
            default: c.Rect(1.5f, 0, 6.5f, 20, cloth); c.Rect(1.5f, 0, 6.5f, 2.5f, clothDk); break;
        }
        return c;
    }

    Canvas Leg(string legs)
    {
        var c = new Canvas(8, 20);
        switch (legs)
        {
            case "mail":
                c.Rect(1.5f, 0, 6.5f, 20, mail);
                c.Rect(1.3f, 11, 6.7f, 12.4f, leather, -0.05f); // a garter tying the chausses under the knee
                break;
            case "plate":
                // Greave (shin), the knee cop with its wing, then the cuisse up the thigh; a gleam down the lit side.
                c.Rect(1.5f, 0, 6.5f, 20, plate);
                c.Ellipse(4, 10, 3.6f, 2.6f, plate, 0.15f);
                c.Ellipse(6.4f, 10, 1.5f, 2.1f, plate, 0.25f);
                for (int x = 0; x < 8; x++) { c.Shade(x, 7, -0.45f); c.Shade(x, 13, -0.45f); c.Shade(x, 14, 0.1f); c.Shade(x, 19, -0.3f); }
                for (int y = 0; y < 20; y++) if (y < 7 || y > 13) c.Shade(3, y, 0.35f);
                c.Set(4, 10, brass, 0.6f);
                for (int y = 15; y < 19; y++) c.Set(6, y, leather, 0.3f); // the strap round the thigh
                break;
            case "bones": c.Rect(3, 0, 5, 20, bone); c.Ellipse(4, 10, 1.7f, 1.5f, bone, 0.1f); break;
            case "shroud": c.Rect(2.2f, 0, 5.8f, 20, pale); c.Rect(1.5f, 0, 6.5f, 7, shroud); break;
            case "hide": c.Rect(2, 0, 6, 20, gskin); c.Rect(1.5f, 0, 6.5f, 4, rag); for (int y = 9; y < 12; y++) c.Shade(4, y, -0.3f); break;
            default: c.Rect(1.5f, 0, 6.5f, 20, clothDk); break;
        }
        return c;
    }

    Canvas Boot(string kind, string v)
    {
        var m = kind == "sabatons" ? plate : kind == "bare" ? gskin : kind == "bone" ? bone : leather;
        if (v == "s")
        {
            var c = new Canvas(10, 8);
            c.Rect(2, 0, 7, 5, m);
            c.Body(4, 8, y => (1, 9.6f - MathF.Max(0, 5.2f - y) * 0.8f), m, -0.05f);
            if (kind == "sabatons") for (int x = 0; x < 10; x++) c.Shade(x, 5, -0.4f);
            return c;
        }
        var f = new Canvas(8, 8);
        f.Body(0, 8, y => y < 5 ? (1, 7) : (0.5f, 7.5f), m);
        if (kind == "sabatons") for (int x = 0; x < 8; x++) f.Shade(x, 4, -0.4f);
        if (v == "f") for (int x = 1; x < 7; x++) f.Shade(x, 7, -0.25f);
        return f;
    }

    Canvas Hand(string kind)
    {
        var c = new Canvas(5, 5);
        c.Ellipse(2.5f, 2.5f, kind == "bone" ? 1.8f : 2.4f, 2.4f, kind == "gauntlets" ? plate : kind == "claw" ? gskin : kind == "bone" ? bone : skin);
        if (kind is "claw" or "bone") { c.Set(0, 4, ink); c.Set(2, 4, ink); c.Set(4, 4, ink); }
        return c;
    }

    Canvas Shield(string kind)
    {
        if (kind == "pavise")
        {
            // A tall board a crossbowman stands behind: the side's cloth, a raised spine and the charge high on it.
            var pv = new Canvas(20, 34);
            pv.Fill((x, y) => y > 0.3f && x > 0.5f && x < 19.5f && (y > 2 || MathF.Abs(x - 10) < 7 + y), (x, y) => new Vector3((x - 10) / 18, 0, 1), cloth);
            pv.Rect(8.6f, 1, 11.4f, 33.7f, wood, 0.1f);
            pv.Border(iron);
            Emblem(pv, 10, 11, 5.5f);
            return pv;
        }
        var c = new Canvas(20, 25);
        switch (kind)
        {
            case "round":
            {
                c.Fill((x, y) => InEl(x, y, 10, 12.5f, 9.6f, 9.6f), (x, y) => new Vector3((x - 10) / 14, (y - 12.5f) / 14, 1), wood);
                for (int y = 0; y < 25; y++) for (int x = 0; x < 20; x++)
                    if (c.Has(x, y) && (x < 10) != (y < 12)) c.Set(x, y, cloth, Vector3.Dot(Vector3.Normalize(new Vector3((x - 10) / 14f, (y - 12.5f) / 14f, 1)), Style.SpriteLight));
                c.Border(iron);
                c.Ellipse(10, 12.5f, 2.8f, 2.8f, steel, 0.15f);
                return c;
            }
            case "buckler":
            {
                // A small steel fist-shield with a boss.
                var b = new Canvas(11, 11);
                b.Fill((x, y) => InEl(x, y, 5.5f, 5.5f, 5.2f, 5.2f), (x, y) => new Vector3((x - 5.5f) / 7, (y - 5.5f) / 7, 1), plate);
                b.Border(iron);
                b.Ellipse(5.5f, 5.5f, 1.8f, 1.8f, steel, 0.25f);
                return b;
            }
            case "targe":
            {
                // A round hide-covered board with studs in rings and a steel boss.
                var t = new Canvas(17, 17);
                t.Fill((x, y) => InEl(x, y, 8.5f, 8.5f, 8.2f, 8.2f), (x, y) => new Vector3((x - 8.5f) / 12, (y - 8.5f) / 12, 1), leather);
                for (int i = 0; i < 12; i++) { float a = i * MathF.Tau / 12; t.Set((int)(8.5f + MathF.Cos(a) * 5.6f), (int)(8.5f + MathF.Sin(a) * 5.6f), brass, 0.3f); }
                t.Ellipse(8.5f, 8.5f, 2.4f, 2.4f, steel, 0.2f);
                t.Border(trim);
                return t;
            }
            case "dhal":
            {
                // A small round domed shield in the side's cloth, four brass bosses round the centre (the south).
                var d = new Canvas(15, 15);
                d.Fill((x, y) => InEl(x, y, 7.5f, 7.5f, 7.2f, 7.2f), (x, y) => new Vector3((x - 7.5f) / 8, (y - 7.5f) / 8, 1), cloth);
                d.Border(brass);
                foreach (var (bx, by) in new[] { (6, 6), (9, 6), (6, 9), (9, 9) }) d.Set(bx, by, brass, 0.4f);
                return d;
            }
            case "kite":
            {
                float Half(float y) => y < 7 ? 9.6f - (7 - y) * (7 - y) * 0.06f : 9.6f * MathF.Pow(MathF.Max(0, 1 - (y - 7) / 18f), 0.85f);
                c.Fill((x, y) => y > 0.3f && MathF.Abs(x - 10) <= Half(y), (x, y) => new Vector3((x - 10) / 16, (y - 10) / 30, 1), cloth);
                c.Border(trim);
                Emblem(c, 10, 9.5f, 5);
                return c;
            }
            default: // heater
            {
                float Half(float y) => y < 10 ? 9.6f : 9.6f * MathF.Sqrt(MathF.Max(0, 1 - (y - 10) * (y - 10) / (14.6f * 14.6f)));
                c.Fill((x, y) => y > 0.3f && MathF.Abs(x - 10) <= Half(y), (x, y) => new Vector3((x - 10) / 16, (y - 10) / 30, 1), cloth);
                c.Border(iron);
                Emblem(c, 10, 10, 5);
                return c;
            }
        }
    }

    Canvas Weapon(string kind)
    {
        switch (kind)
        {
            case "sword": return Blade(33, 23);
            case "longsword": return Blade(40, 29);
            case "axe":
            {
                var c = new Canvas(8, 30);
                c.Rect(3, 2, 5, 30, wood);
                c.Body(2, 11, y => (0.3f, 4.2f - MathF.Abs(y - 6.5f) * 0.15f), steel);
                for (int y = 2; y < 11; y++) c.Shade(0, y, 0.5f);
                c.Rect(2.5f, 0, 5.5f, 2.5f, iron);
                return c;
            }
            case "spear": return Polearm(65, 9, 2.9f, false);
            case "pike": return Polearm(90, 7, 2.2f, false);
            case "lance": return Polearm(93, 7, 1.8f, true);
            case "bow": return Bow(33);
            case "greatsword": return Blade(58, 44);
            case "daneaxe":
            {
                var c = new Canvas(12, 62);
                c.Rect(5, 2, 7, 62, wood);
                c.Body(1, 16, y => (0.3f, 6 - MathF.Abs(y - 8.5f) * 0.2f), steel); // a broad bearded head
                c.Area((x, y) => y >= 12 && y < 20 && x >= 3 && x < 6 && y - 12 < (6 - x) * 2.5f, steel);
                for (int y = 1; y < 16; y++) c.Shade(0, y, 0.5f);
                c.Rect(4.5f, 0, 7.5f, 2.5f, iron);
                c.Rect(4.6f, 40, 7.4f, 46, leather);
                return c;
            }
            case "club":
            {
                // A tree limb, thick and knotted at the striking end.
                var c = new Canvas(14, 50);
                c.Body(0, 50, y => (7 - (y < 22 ? 6.5f - y * 0.05f : 2.2f), 7 + (y < 22 ? 6.5f - y * 0.05f : 2.2f)), wood);
                foreach (var (kx, ky) in new[] { (2, 5), (11, 9), (3, 15), (10, 19) }) { c.Ellipse(kx, ky, 1.6f, 1.6f, wood, -0.25f); c.Shade(kx, ky, -0.4f); }
                for (int y = 0; y < 22; y++) c.Shade(1, y, 0.35f);
                c.Rect(4.6f, 36, 9.4f, 44, leather);
                return c;
            }
            case "halberd":
            {
                // An axe blade, a back spike and a point on a shaft a man's height and a half (docs/archive/ANIMATION_ROSTER.md R1).
                var c = new Canvas(14, 72);
                c.Rect(6, 4, 8, 72, wood);
                c.Body(0, 6, y => (7 - 0.3f - y * 0.18f, 7 + 0.3f + y * 0.18f), steel); // the point
                c.Area((x, y) => y >= 7 && y < 17 && x >= 8 && x < 8 + 5.6f - MathF.Abs(y - 11.5f) * 0.25f + (y > 11 ? (y - 11) * 0.12f : 0), steel); // the blade, bearded below
                for (int y = 7; y < 17; y++) c.Shade(13, y, 0.45f);
                c.Area((x, y) => y >= 10 && y < 13 && x >= 7 - (13 - y) * 1.3f && x < 6, iron); // the back spike
                c.Rect(5.5f, 17, 8.5f, 19, iron);
                c.Rect(5.6f, 50, 8.4f, 55, leather);
                return c;
            }
            case "glaive":
            {
                // A long single-edged blade, curved at the tip, with a lug on the back, on a long shaft.
                var c = new Canvas(10, 68);
                c.Rect(4, 18, 6, 68, wood);
                c.Body(0, 18, y => (4 - (y < 4 ? y * 0.3f : 1.2f), 6 + MathF.Min(2.6f, 0.3f + y * 0.25f) - (y > 14 ? (y - 14) * 0.5f : 0)), steel);
                for (int y = 2; y < 17; y++) c.Shade(4, y, 0.4f);
                c.Rect(1.5f, 12, 4, 14, iron); // the lug that catches a blade
                c.Rect(3.6f, 18, 6.4f, 20, iron);
                c.Rect(3.6f, 48, 6.4f, 53, leather);
                return c;
            }
            case "javelin":
            {
                // A light throwing spear: a long thin iron head on a short shaft, a cord loop for the fingers.
                var c = new Canvas(6, 44);
                c.Rect(2.5f, 9, 3.5f, 44, wood);
                c.Body(0, 9, y => (3 - MathF.Min(1.1f, 0.3f + y * 0.3f), 3 + MathF.Min(1.1f, 0.3f + y * 0.3f)), iron);
                c.Rect(1.8f, 24, 4.2f, 26, leather);
                return c;
            }
            case "sling":
            {
                // Two cords and a leather pouch, hanging from the hand with a stone in it.
                var c = new Canvas(6, 20);
                for (int y = 4; y < 20; y++) { c.Set(2 - (19 - y) / 8, y, leather, 0.2f); c.Set(3 + (19 - y) / 8, y, leather, 0.4f); }
                c.Ellipse(3, 2.6f, 2.4f, 2.2f, leather);
                c.Set(3, 3, Mat.Flat(new Color(150, 146, 136)));
                return c;
            }
            case "scimitar":
            {
                // A curved blade widening toward the tip, the hilt with a short cross and a pommel cap (the south).
                var c = new Canvas(10, 32);
                c.Body(0, 23, y => { float bend = (23 - y) * (23 - y) * 0.006f; float half = MathF.Min(1.9f, 0.6f + (23 - MathF.Abs(y - 7)) * 0.07f); return (4 - half + bend * 1.6f, 4 + half + bend * 1.6f); }, steel);
                for (int y = 2; y < 22; y++) c.Shade(4 + (int)((23 - y) * (23 - y) * 0.01f), y, 0.35f);
                c.Rect(1, 23, 8, 24.8f, brass);
                c.Rect(3, 24.8f, 5, 30.5f, leather);
                c.Ellipse(4, 31, 1.6f, 1.0f, brass, 0.1f);
                return c;
            }
            case "falchion":
            {
                // A short heavy chopper: broad near the clipped point, straight in the back.
                var c = new Canvas(9, 30);
                c.Body(0, 21, y => (2.5f, 5.5f + MathF.Min(1.8f, y < 4 ? y * 0.45f : 1.8f - (y - 4) * 0.06f)), steel);
                for (int y = 2; y < 20; y++) c.Shade(3, y, 0.35f);
                c.Rect(0.5f, 21, 8.5f, 22.8f, iron);
                c.Rect(3, 22.8f, 5, 28.5f, leather);
                c.Ellipse(4, 29, 1.6f, 1.0f, iron, 0.1f);
                return c;
            }
            case "warhammer":
            {
                // A long-hafted hammer: a squared face one side, a curved beak the other, a spike on top.
                var c = new Canvas(14, 52);
                c.Rect(6, 3, 8, 52, wood);
                c.Body(0, 4, y => (6.4f - y * 0.1f, 7.6f + y * 0.1f), iron);
                c.Rect(2, 5, 12, 10, steel);
                c.Area((x, y) => y >= 5 && y < 13 && x >= 12 && x < 14 - (y - 5) * 0.2f, steel); // the beak
                c.Rect(0.5f, 5.5f, 2.5f, 9.5f, plate); // the face
                for (int x = 2; x < 12; x++) c.Shade(x, 9, -0.3f);
                c.Rect(5.6f, 10, 8.4f, 12, iron);
                c.Rect(5.6f, 38, 8.4f, 44, leather);
                return c;
            }
            case "mace":
            {
                // A flanged mace: a steel head of fins on a short haft, an iron spike on top.
                var c = new Canvas(9, 30);
                c.Rect(3.5f, 8, 5.5f, 30, wood);
                c.Ellipse(4.5f, 5, 3.0f, 3.6f, steel);
                for (int y = 2; y < 9; y += 2) { c.Set(0, y, plate, 0.6f); c.Set(8, y, plate, 0.3f); }
                for (int y = 2; y < 8; y++) c.Shade(4, y, 0.4f);
                c.Rect(3.5f, 0, 5.5f, 1.8f, iron);
                c.Rect(3.2f, 9, 5.8f, 10.5f, iron);
                c.Rect(3.3f, 22, 5.7f, 28, leather);
                return c;
            }
            case "staff":
            {
                // A crooked staff topped with a skull-white knob and a green light.
                var c = new Canvas(10, 54);
                c.Body(6, 54, y => (4 - MathF.Sin(y * 0.3f) * 0.6f, 6 - MathF.Sin(y * 0.3f) * 0.6f), wood);
                c.Ellipse(5, 4, 4, 4, bone);
                c.Set(4, 4, wisp); c.Set(6, 4, wisp);
                c.Rect(3.6f, 30, 6.4f, 36, leather);
                return c;
            }
            case "crossbow":
            {
                // Seen from above along the bolt: the stock, the steel prod across it, the string.
                var c = new Canvas(24, 26);
                c.Rect(10.5f, 3, 13.5f, 26, wood);
                c.Body(2, 5, y => (0.5f, 23.5f), steel);
                for (int x = 1; x < 23; x++) c.Set(x, 6 + (int)(MathF.Abs(x - 11.5f) * 0.15f), Mat.Flat(Style.Linen));
                c.Rect(10, 0, 14, 3, iron);
                c.Rect(9.5f, 14, 14.5f, 16, iron);
                return c;
            }
            default: return Bow(45); // longbow
        }
    }

    Canvas Blade(int h, int guard)
    {
        var c = new Canvas(8, h);
        c.Body(0, guard, y => { float half = MathF.Min(1.6f, 0.5f + y * 0.4f); return (4 - half, 4 + half); }, steel);
        for (int y = 3; y < guard - 1; y++) c.Shade(4, y, 0.35f);
        c.Rect(0.2f, guard, 7.8f, guard + 2, brass);
        c.Rect(3, guard + 2, 5, h - 2, leather);
        c.Ellipse(4, h - 1.2f, 1.7f, 1.3f, brass, 0.1f);
        return c;
    }

    Canvas Polearm(int h, int head, float width, bool lance)
    {
        var c = new Canvas(8, h);
        c.Rect(3, head, 5, h, wood);
        c.Body(0, head, y => { float half = MathF.Min(width / 2 + 0.3f, 0.4f + y * 0.45f) * (lance ? 1 : 1 - MathF.Max(0, y - head * 0.6f) / head); return (4 - half, 4 + half); }, steel);
        c.Rect(2.6f, head - 0.5f, 5.4f, head + 1.5f, iron);
        if (lance)
        {
            c.Area((x, y) => y > head + 1 && y < head + 11 && x >= 5 && x < 5 + (head + 11 - y) * 0.32f, cloth); // pennant
            c.Body(h * 0.66f, h * 0.66f + 7, y => { float half = 1 + (y - h * 0.66f) * 0.38f; return (4 - half, 4 + half); }, steel);
            c.Rect(3, h * 0.66f + 7, 5, h * 0.66f + 13, leather);
        }
        return c;
    }

    Canvas Bow(int h)
    {
        var c = new Canvas(10, h);
        for (int y = 0; y < h; y++)
        {
            float x = 1.5f + 6.2f * MathF.Sin(MathF.PI * (y + 0.5f) / h);
            c.Set((int)x, y, wood, 0.55f);
            c.Set((int)x + 1, y, wood, 0.25f);
            c.Set(1, y, Mat.Flat(Style.Linen));
        }
        for (int y = h / 2 - 3; y < h / 2 + 3; y++) { int x = (int)(1.5f + 6.2f); c.Set(x, y, leather); c.Set(x + 1, y, leather, 0.2f); }
        return c;
    }

    Canvas Arrow()
    {
        var c = new Canvas(8, 23);
        c.Rect(3.5f, 4, 4.5f, 23, wood);
        c.Body(0, 4, y => (3.5f - y * 0.45f, 4.5f + y * 0.45f), steel);
        for (int y = 17; y < 22; y++) { c.Set(2, y, trim); c.Set(5, y, trim); }
        return c;
    }

    Canvas Quiver()
    {
        var c = new Canvas(8, 18);
        c.Rect(1, 3, 7, 18, leather);
        c.Rect(1, 3, 7, 5, clothDk);
        foreach (var (x, y) in new[] { (2, 1), (4, 0), (6, 2), (3, 2), (5, 1) }) c.Set(x, y, trim, 0.5f);
        return c;
    }

    Canvas Plume()
    {
        var c = new Canvas(8, 14);
        c.Fill((x, y) => InEl(x, y, 4.2f + (y - 7) * 0.12f, 7, 3.2f, 6.8f), (x, y) => new Vector3((x - 4) / 5, (y - 7) / 9, 0.8f), new Mat(trim.Ramp[2], Pattern.Hair));
        return c;
    }

    /// A company's banner: the side's cloth with its trim and charge, swallow-tailed.
    Canvas Flag()
    {
        var c = new Canvas(24, 16);
        c.Area((x, y) => x < 23 - (MathF.Abs(y - 7.5f) < 3 ? 4 - MathF.Abs(y - 7.5f) : 0), cloth);
        c.Border(trim);
        Emblem(c, 10, 8, 4.5f);
        return c;
    }

    Canvas Blood()
    {
        var c = new Canvas(15, 8);
        c.Ellipse(7.5f, 4, 7, 3.4f, blood, -0.2f);
        c.Ellipse(12, 6, 2.5f, 1.6f, blood, -0.2f);
        return c;
    }

    /// A pale ragged patch for marks on the ground (churned earth, trampled grass), tinted when drawn.
    Canvas Splat()
    {
        var c = new Canvas(16, 12);
        var m = new Mat(new Color(230, 226, 220));
        c.Ellipse(8, 6, 6.5f, 4.2f, m, -0.1f);
        c.Ellipse(3.5f, 4, 2.6f, 2f, m, -0.1f);
        c.Ellipse(12.5f, 8.5f, 2.8f, 2.2f, m, -0.1f);
        return c;
    }

    // ---------- horses ----------

    /// Neutral coat parts ("/n"), tinted per horse when drawn.
    void Horse()
    {
        var coat = new Mat(Style.Coat);
        var mane = new Mat(Style.Mane, Pattern.Hair);
        var hoof = new Mat(Style.Hoof);
        var eye = Mat.Flat(new Color(30, 26, 26));

        var body = new Canvas(55, 23);
        BodyShape(body, coat);
        Add("hbody/n", body);

        // The neck arches at the crest and narrows to the throat; the mane hangs in uneven locks along it.
        var neck = new Canvas(23, 13);
        static float NeckTop(float x) => 6.6f + x * 0.03f - (6.2f - x * 0.13f) - MathF.Sin(x / 23 * MathF.PI) * 0.9f;
        static float NeckBottom(float x) => 6.6f + x * 0.03f + 6.0f - x * 0.15f;
        neck.Columns(0, 23, x => (NeckTop(x), NeckBottom(x)), coat);
        neck.Area((x, y) => y >= NeckTop(x) && y < NeckTop(x) + 2.3f + ((int)x * 7 % 3) * 0.7f, mane);
        Add("hneck/n", neck);

        var head = new Canvas(20, 10);
        head.Columns(0, 20, x => { float h = 4.6f - x * 0.07f, cy = 5.2f; return (cy - h, cy + h); }, coat);
        foreach (var (x, y) in new[] { (1, 0), (2, 0), (2, 1) }) head.Set(x, y, coat, 0.5f);
        head.Area((x, y) => x < 5 && y < 2.4f && y > 0.6f, mane);
        head.Set(5, 3, eye); head.Set(6, 3, eye);
        head.Set(18, 6, eye);
        for (int y = 0; y < 10; y++) for (int x = 15; x < 20; x++) head.Shade(x, y, -0.35f);
        Add("hhead/n", head);

        var chest = new Canvas(20, 18);
        chest.Ellipse(10, 9, 9.6f, 8.6f, coat);
        for (int y = 4; y < 16; y++) chest.Shade(10, y, -0.3f);
        Add("hchest/n", chest);

        var rump = new Canvas(20, 18);
        rump.Ellipse(10, 9, 9.6f, 8.6f, coat);
        rump.Rect(8.8f, 3, 11.2f, 18, mane);
        Add("hrump/n", rump);

        var face = new Canvas(10, 15);
        face.Body(0.5f, 15, y => { float half = y < 3 ? 3.6f : 4.6f - y * 0.12f; return (5 - half, 5 + half); }, coat);
        foreach (var (x, y) in new[] { (1, 0), (2, 0), (7, 0), (8, 0) }) face.Set(x, y, coat, 0.4f);
        face.Set(1, 6, eye); face.Set(8, 6, eye);
        face.Set(3, 13, eye); face.Set(6, 13, eye);
        face.Rect(4.2f, 3, 5.8f, 11, Mat.Flat(Style.Linen));
        Add("hface/n", face);

        var leg = new Canvas(5, 30);
        leg.Body(0, 3, y => (0.2f + y * 0.15f, 4.8f - y * 0.15f), hoof);
        leg.Body(3, 5.5f, y => (0.6f, 4.4f), mane); // the fetlock's feather
        leg.Rect(1.4f, 5.5f, 3.6f, 15, coat);
        leg.Ellipse(2.5f, 15.5f, 2.2f, 2, coat, 0.05f);
        leg.Body(17, 30, y => (0.4f - (y - 17) * 0.02f, 4.6f + (y - 17) * 0.02f), coat);
        Add("hleg/n", leg);

        // A full tail: thick at the dock, spreading into locks toward the end, with a gentle wave.
        var tail = new Canvas(7, 22);
        tail.Body(0, 22, y => { float half = 1.6f + MathF.Sin(MathF.Min(1, y / 18) * MathF.PI * 0.8f) * 1.7f, c = 3.5f + MathF.Sin(y * 0.35f) * 0.5f; return (c - half, c + half); }, mane);
        for (int x = 1; x < 6; x += 2) tail.Clear(x, 0); // ragged ends
        tail.Clear(2, 1); tail.Clear(5, 1);
        Add("htail/n", tail);

        // A camel (docs/archive/WORLD.md W4) is the horse rig in a sand coat with a hump over the back.
        var hump = new Canvas(24, 12);
        hump.Fill((x, y) => InEl(x, y, 12, 12, 11.5f, 11.5f), (x, y) => new Vector3((x - 12) / 12, (y - 12) / 12, 0.7f), new Mat(Style.Coat, Pattern.Hair));
        Add("hhump/n", hump);
    }

    // ---------- beasts (docs/archive/CREATURES.md) ----------

    /// A wolf's parts in grey fur ("/n"); the puppet tints a dire wolf darker. Long parts run tail to nose.
    void Wolf()
    {
        var fur = new Mat(new Color(150, 148, 142), Pattern.Hair);
        var pale = new Mat(new Color(198, 192, 180), Pattern.Hair);
        var dark = new Mat(new Color(78, 76, 74), Pattern.Hair);
        var eye = Mat.Flat(new Color(232, 196, 60));
        var nose = Mat.Flat(new Color(28, 24, 24));

        var body = new Canvas(30, 12);
        body.Columns(0, 30, x => { float u = (x - 15) / 15f; float top = 2.2f + 1.2f * u * u - (x > 18 ? 1.0f : 0), bot = 10.5f - 1.5f * u * u - (x < 10 ? 1.2f : 0); return (top, bot); }, fur);
        body.Area((x, y) => y > 8 && x > 6 && x < 26, pale);              // pale belly
        body.Area((x, y) => y < 4.2f && x > 16 && x < 29, dark);          // dark ruff over the shoulders
        Add("wbody/n", body);

        var head = new Canvas(16, 9);
        head.Columns(0, 16, x => { float h = x < 8 ? 4 : 4 - (x - 8) * 0.32f; float cy = x < 8 ? 4.4f : 4.4f + (x - 8) * 0.18f; return (cy - h, cy + h); }, fur);
        head.Body(0, 3, y => (1.5f + y * 0.4f, 5 - y * 0.4f), dark);       // pricked ear
        head.Set(7, 3, eye);
        head.Set(15, 5, nose); head.Set(14, 5, nose);
        for (int x = 9; x < 15; x++) head.Set(x, 7, ink);                  // the jaw line
        head.Set(11, 7, Mat.Flat(Style.Linen)); head.Set(13, 7, Mat.Flat(Style.Linen));
        Add("whead/n", head);

        var leg = new Canvas(4, 16);
        leg.Rect(0.5f, 0, 3.5f, 2, dark);
        leg.Rect(0.8f, 2, 3.2f, 11, fur);
        leg.Body(11, 16, y => (0.2f, 3.8f), fur);
        Add("wleg/n", leg);

        var tail = new Canvas(6, 15);
        tail.Body(0, 15, y => { float half = 1.2f + MathF.Sin(y / 15 * MathF.PI) * 1.6f; return (3 - half, 3 + half); }, fur);
        tail.Area((x, y) => y > 12, dark);
        Add("wtail/n", tail);
    }

    // ---------- the beasts of docs/archive/MONSTERS.md (neutral palette; the puppet tints kin apart) ----------

    /// A giant spider: a bulbous banded abdomen ("sabdomen"), the head-and-body with its eyes and fangs ("shead"), a
    /// jointed hairy leg ("sleg"), and the web that pins men ("web"). Long parts run tail to nose.
    void Spider()
    {
        var hide = new Mat(new Color(52, 42, 46), Pattern.Hair);
        var dark = new Mat(new Color(30, 24, 28), Pattern.Hair);
        var band = new Mat(new Color(150, 44, 36), Pattern.Hair);
        var pale = new Mat(new Color(176, 158, 130), Pattern.Hair);
        var eye = Mat.Flat(new Color(255, 70, 50));
        var fang = Mat.Flat(new Color(220, 210, 180));

        var abdomen = new Canvas(24, 18);
        abdomen.Ellipse(11.5f, 9.5f, 11.2f, 8.2f, hide);
        abdomen.Area((x, y) => y > 13 && (x - 11.5f) * (x - 11.5f) / 110 + (y - 9.5f) * (y - 9.5f) / 64 <= 1, dark); // the dark underside
        for (int k = 0; k < 3; k++)                                                                                    // red chevrons down the back
        {
            float cx = 6 + k * 5.2f;
            abdomen.Area((x, y) => y > 2.2f && y < 6.5f && MathF.Abs(x - cx) < 1.6f - (y - 2.2f) * 0.15f, band);
        }
        abdomen.Area((x, y) => y > 8.5f && y < 10 && x > 3 && x < 20, pale, 0.3f);                                    // a pale girdle
        abdomen.Set(0, 9, dark); abdomen.Set(0, 10, dark);                                                            // spinnerets
        Add("sabdomen/n", abdomen);

        var head = new Canvas(16, 11);
        head.Columns(0, 13, x => { float h = 3.6f + MathF.Sin(x / 13 * MathF.PI) * 1.2f; return (5 - h, 5.5f + h * 0.7f); }, hide);
        foreach (var (ex, ey) in new[] { (11, 2), (12, 3), (10, 2), (12, 1), (9, 3) }) head.Set(ex, ey, eye, 0.9f);       // a cluster of red eyes
        head.Body(6, 11, y => (12 - (y - 6) * 0.2f, 15 - (y - 6) * 0.5f), dark);                                       // the jaws
        head.Line(14, 7, 13.5f, 10.5f, fang, 0.8f);                                                                   // and the fangs
        head.Line(12.5f, 7.5f, 12.2f, 10.5f, fang, 0.6f);
        Add("shead/n", head);

        var leg = new Canvas(2, 20);
        leg.Rect(0, 0, 2, 20, hide);
        leg.Area((x, y) => y is > 6 and < 7.5f or > 13 and < 14.5f, pale, 0.4f); // pale joints
        leg.Area((x, y) => y > 18, dark);
        Add("sleg/n", leg);

        var web = new Canvas(22, 22);
        var silk = Mat.Flat(new Color(236, 236, 242));
        for (int k = 0; k < 8; k++)
        {
            float a = k * MathF.Tau / 8 + 0.2f;
            web.Line(11, 11, 11 + MathF.Cos(a) * 10.5f, 11 + MathF.Sin(a) * 10.5f, silk, 0.6f);
        }
        foreach (float r in new[] { 4f, 7f, 10f })
            for (int k = 0; k < 8; k++) // each ring sags between the spokes
            {
                float a0 = k * MathF.Tau / 8 + 0.2f, a1 = a0 + MathF.Tau / 8, rm = r * 0.86f, am = (a0 + a1) / 2;
                web.Line(11 + MathF.Cos(a0) * r, 11 + MathF.Sin(a0) * r, 11 + MathF.Cos(am) * rm, 11 + MathF.Sin(am) * rm, silk, 0.5f);
                web.Line(11 + MathF.Cos(am) * rm, 11 + MathF.Sin(am) * rm, 11 + MathF.Cos(a1) * r, 11 + MathF.Sin(a1) * r, silk, 0.5f);
            }
        Add("web/n", web);
    }

    /// A cave bear: a heavy humped body ("bbody"), a broad head with small ears and a pale snout ("bhead"), a thick leg
    /// with claws ("bleg"). Long parts run tail to nose.
    void Bear()
    {
        var fur = new Mat(new Color(104, 72, 48), Pattern.Hair);
        var dark = new Mat(new Color(62, 44, 32), Pattern.Hair);
        var snout = new Mat(new Color(150, 118, 88), Pattern.Hair);
        var claw = Mat.Flat(new Color(222, 214, 196));
        var nose = Mat.Flat(new Color(24, 20, 20));

        var body = new Canvas(32, 18);
        body.Columns(0, 32, x => { float u = (x - 16) / 16f; float top = 3.2f + 2.4f * u * u - (x > 17 && x < 27 ? 1.8f * MathF.Sin((x - 17) / 10 * MathF.PI) : 0), bot = 16.5f - 2.5f * u * u; return (top, bot); }, fur);
        body.Area((x, y) => y > 13 && x > 5 && x < 28, dark); // the darker belly
        Add("bbody/n", body);

        var head = new Canvas(20, 16);
        head.Columns(0, 20, x => { float h = x < 11 ? 6.4f : 6.4f - (x - 11) * 0.5f; float cy = x < 11 ? 8.5f : 8.5f + (x - 11) * 0.3f; return (cy - h, cy + h); }, fur);
        head.Area((x, y) => x > 12 && y > 8, snout);
        head.Ellipse(4, 2.4f, 2.4f, 2.2f, fur);                   // a round ear
        head.Ellipse(4, 2.6f, 1.2f, 1.1f, dark);
        head.Set(19, 10, nose); head.Set(18, 10, nose); head.Set(19, 11, nose); head.Set(18, 9, nose);
        head.Set(10, 6, Mat.Flat(new Color(30, 20, 16)), 0.3f);  // a small dark eye
        head.Set(11, 6, Mat.Flat(new Color(30, 20, 16)), 0.3f);
        for (int x = 12; x < 19; x++) head.Set(x, 13, ink);       // the jaw line
        head.Set(14, 14, Mat.Flat(Style.Linen)); head.Set(17, 14, Mat.Flat(Style.Linen)); // fangs
        Add("bhead/n", head);

        var leg = new Canvas(7, 16);
        leg.Body(0, 13, y => (0.2f + y * 0.03f, 6.8f - y * 0.05f), fur);
        leg.Rect(0, 13, 7, 16, dark);
        foreach (int cx in new[] { 1, 3, 5 }) leg.Set(cx, 15, claw, 0.7f);
        Add("bleg/n", leg);
    }

    /// A wyvern: a scaled body ("ybody"), a long neck ("yneck"), a horned head with a toothed jaw ("yhead"), a wing of
    /// membrane between finger bones ("ywing", leading edge along the top, root at the left), a tail joint ("ytail")
    /// ending in a sting ("ysting"), and a taloned hind leg ("yleg").
    void Wyvern()
    {
        var scale = new Mat(new Color(70, 96, 58), Pattern.Mail);
        var dark = new Mat(new Color(40, 54, 36), Pattern.Mail);
        var belly = new Mat(new Color(196, 170, 104), Pattern.Quilt);
        var web = new Mat(new Color(122, 58, 46), Pattern.Cloth);
        var webDk = new Mat(new Color(80, 36, 32), Pattern.Cloth);
        var bone = new Mat(new Color(206, 196, 168));
        var eye = Mat.Flat(new Color(255, 196, 40));

        var body = new Canvas(28, 13);
        body.Columns(0, 28, x => { float u = (x - 15) / 14f; return (2.5f + 2.6f * u * u, 11.5f - 2.8f * u * u); }, scale);
        body.Area((x, y) => y > 8.5f && x > 4 && x < 25, belly);
        for (int x = 3; x < 26; x += 3) body.Set(x, (int)(2.5f + 2.6f * ((x - 15) / 14f) * ((x - 15) / 14f)) - 1 + 1, dark, 0.7f); // a ridge of spines
        Add("ybody/n", body);

        var neck = new Canvas(16, 8);
        neck.Columns(0, 16, x => { float h = 3.4f - x * 0.1f; return (4 - h, 4 + h); }, scale);
        neck.Area((x, y) => y > 4.6f, belly);
        Add("yneck/n", neck);

        var head = new Canvas(18, 10);
        head.Columns(0, 17, x => { float h = x < 6 ? 3.6f : 3.6f - (x - 6) * 0.22f; return (4.2f - h, 4.6f + h * 0.8f); }, scale);
        head.Line(1, 2, -1, -1, bone, 0.7f); head.Line(3, 1.5f, 2, -1, bone, 0.6f);  // swept horns
        head.Body(5.5f, 10, y => (6, 17 - (y - 5.5f) * 0.6f), dark);                // the lower jaw
        for (int x = 7; x < 16; x += 2) head.Set(x, 6, bone, 0.8f);                 // teeth
        head.Set(5, 3, eye, 0.9f);
        Add("yhead/n", head);

        var wing = new Canvas(34, 22);
        // The membrane: full at the root, cut into scallops between the finger bones along the trailing edge.
        wing.Fill((x, y) =>
        {
            float u = x / 34f, edge = 2 + 18 * (1 - u * 0.75f) - 3.5f * MathF.Abs(MathF.Sin(u * MathF.PI * 3.5f));
            return y > 1.5f && y < edge;
        }, (x, y) => new Vector3(0, -0.2f, 1), web);
        wing.Area((x, y) => y > 1.5f && y < 4 && x < 33, webDk);                    // shade under the arm
        wing.Line(0, 1.5f, 33.5f, 1, bone, 0.7f, 2);                                 // the arm
        foreach (float fx in new[] { 10f, 19f, 27f }) wing.Line(fx, 2, fx * 0.55f + 2, 2 + 18 * (1 - fx / 34 * 0.75f) - 1, bone, 0.5f); // the fingers
        wing.Line(33, 0, 34, 0, bone, 0.9f);                                         // the claw on the wrist
        Add("ywing/n", wing);

        var tail = new Canvas(4, 16);
        tail.Body(0, 16, y => (0.3f + y * 0.03f, 3.7f - y * 0.03f), scale);
        Add("ytail/n", tail);

        var sting = new Canvas(8, 10);
        sting.Body(0, 10, y => { float h = 3.6f * MathF.Sin(MathF.Min(1, y / 6) * MathF.PI * 0.5f) * (y > 6 ? (10 - y) / 4 : 1); return (4 - h, 4 + h); }, dark);
        sting.Line(4, 6, 4, 10, bone, 0.8f);
        Add("ysting/n", sting);

        var leg = new Canvas(5, 14);
        leg.Body(0, 11, y => (0.4f, 4.6f - y * 0.1f), scale);
        leg.Rect(0, 11, 5, 14, dark);
        foreach (int cx in new[] { 0, 2, 4 }) leg.Set(cx, 13, bone, 0.8f);
        Add("yleg/n", leg);
    }

    static (float, float) HorseSpan(float x)
    {
        float u = (x - 27.5f) / 27;
        return (2 + 6 * u * u * u * u, 21.5f - 6 * u * u * u * u + 0.6f * (1 - u * u));
    }

    static void BodyShape(Canvas c, Mat m) => c.Columns(0.5f, 54.5f, HorseSpan, m);

    /// Saddle and barding over the coat, in the side's colours.
    void HorseGear(string s)
    {
        var saddle = new Canvas(55, 23);
        Saddle(saddle, true);
        Add($"hbody.saddle{s}", saddle);

        var cap = new Canvas(55, 23);
        cap.Columns(1.5f, 53.5f, x => { var (t, _) = HorseSpan(x); return (t + 1.5f, 22.4f - ((int)x % 6 < 3 ? 1.6f : 0)); }, cloth);
        cap.Border(trim);
        Emblem(cap, 27.5f, 13, 4.5f);
        Saddle(cap, false);
        Add($"hbody.caparison{s}", cap);

        var neck = new Canvas(23, 13);
        neck.Columns(0, 23, x => { float h = 6 - x * 0.1f, cy = 6.6f + x * 0.03f; return (cy - h + 2.2f, cy + h); }, cloth);
        neck.Border(trim);
        Add($"hneck.caparison{s}", neck);

        var head = new Canvas(20, 10);
        head.Columns(4, 18, x => { float h = 4.4f - x * 0.07f; return (5.2f - h, 5.2f + h); }, plate);
        head.Clear(5, 3); head.Clear(6, 3);
        Add($"hhead.caparison{s}", head);

        foreach (var part in new[] { "hchest", "hrump" })
        {
            var c = new Canvas(20, 18);
            c.Ellipse(10, 9.5f, 9.6f, 8.4f, cloth);
            c.Border(trim);
            if (part == "hchest") Emblem(c, 10, 9, 3.6f);
            else c.Rect(8.8f, 1, 11.2f, 6, Mat.Flat(Style.Mane));
            Add($"{part}.caparison{s}", c);
        }
    }

    void Saddle(Canvas c, bool cloth)
    {
        if (cloth)
        {
            c.Rect(19, 4, 37, 15.5f, this.cloth);
            c.Border(trim);
            c.Rect(28, 14, 30, 22, leather);
        }
        c.Body(0.8f, 6.5f, y => (22, 35), leather);
        c.Body(0, 5.5f, y => (20.5f, 23.5f), leather, 0.1f);
        c.Body(0, 4.5f, y => (33.5f, 36), leather, 0.1f);
    }
}
