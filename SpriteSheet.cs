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
        ["crossbow"] = 0.55f, ["greatsword"] = 0.12f, ["daneaxe"] = 0.3f, ["club"] = 0.22f, ["staff"] = 0.55f,
    };

    static readonly string[] Views = { "f", "s", "b" };

    public SpriteSheet()
    {
        for (int p = 0; p < Style.Sides.Length; p++) Side(p);
        Horse();
        Wolf();
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
        steel = new(Style.Steel); plate = new(Color.Lerp(Style.Steel, Style.Iron, 0.3f), Pattern.Plate); iron = new(Style.Iron);
        mail = new(Color.Lerp(Style.Steel, Style.Iron, 0.15f), Pattern.Mail);
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
        HorseGear(s);
    }

    // ---------- people ----------

    static bool InEl(float x, float y, float cx, float cy, float rx, float ry) => (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1;

    Canvas Head(string v)
    {
        var c = new Canvas(15, 15);
        c.Ellipse(7.5f, 7.5f, 7f, 7.3f, skin);
        switch (v)
        {
            case "f":
                c.Area((x, y) => InEl(x, y, 7.5f, 7.5f, 7f, 7.3f) && y < 4.2f, hair);
                foreach (int ex in new[] { 4, 10 }) { c.Set(ex, 7, ink); c.Set(ex, 8, ink); }
                c.Shade(7, 9, -0.35f); c.Shade(7, 10, -0.45f);
                for (int x = 6; x <= 8; x++) c.Shade(x, 12, -0.35f);
                break;
            case "s":
                c.Area((x, y) => InEl(x, y, 7.5f, 7.5f, 7f, 7.3f) && (x < 6.5f || y < 4.2f), hair);
                c.Ellipse(6.5f, 8.5f, 1.6f, 2.2f, skin, -0.15f);
                c.Set(11, 7, ink); c.Set(11, 8, ink);
                c.Set(14, 9, skin, 0.5f);
                c.Shade(12, 12, -0.3f); c.Shade(13, 12, -0.3f);
                break;
            default:
                c.Area((x, y) => InEl(x, y, 7.5f, 7.5f, 7f, 7.3f) && y < 12.5f, hair);
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
                else for (int x = 3; x < 15; x += 3) c.Set(x, 5, brass, 0.6f);
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
        if (body == "plate")
        {
            c.Body(0, 14, span, plate);
            c.Body(14, 20, span, plate, -0.1f);
            for (int y = 16; y < 21; y += 2) for (int x = 0; x < c.W; x++) c.Shade(x, y, -0.45f);
            c.Body(20, 23, span, mail);
            if (v == "f") for (int y = 2; y < 13; y++) c.Shade(10, y, 0.35f);
            if (!side) { c.Ellipse(3.2f, 3, 3.4f, 3, plate, 0.05f); c.Ellipse(16.8f, 3, 3.4f, 3, plate, 0.05f); }
            else c.Ellipse(6.5f, 3, 4, 3, plate, 0.05f);
        }
        else
        {
            c.Body(0, 23, span, m);
            if (body == "tunic") c.Body(20.5f, 23, span, clothDk);
        }
        // Belt with a buckle.
        c.Body(13.5f, 15.6f, span, leather);
        if (v == "f") { c.Set(9, 14, brass, 0.7f); c.Set(10, 14, brass, 0.7f); c.Set(9, 15, brass, 0.4f); c.Set(10, 15, brass, 0.4f); }
        if (v == "f" && body is "tunic" or "gambeson")
            foreach (var (x, y) in new[] { (9, 0), (10, 0), (9, 1), (10, 1), (10, 2) }) c.Set(x, y, skin, 0.4f);
        if (body == "gambeson") for (int x = 0; x < c.W; x++) c.Shade(x, 0, -0.3f);
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
                if (y % 3 == 0 || (x + (y / 3 % 2) * 2) % 4 == 0) c.Shade(x, y, -0.28f);
            c.Body(15.6f, 23, span, clothDk);
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
        if (side) c.Body(2, 23, y => (3.2f, 10.2f), cloth);
        else c.Body(2, 23, y => (4.6f - y * 0.03f, 15.4f + y * 0.03f), cloth);
        c.Border(trim);
        if (!side)
        {
            Emblem(c, 10, 8.5f, 4.2f);
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
            case "gambeson": c.Rect(1, 0, 7, 20, quilt); c.Rect(1, 0, 7, 2.5f, leather); break;
            case "fur": c.Rect(1.5f, 0, 6.5f, 20, leather); c.Rect(1, 0, 7, 4, fur); break;
            case "rags": c.Rect(2, 0, 6, 20, gskin); c.Rect(1.5f, 0, 6.5f, 3, rag); break; // a thin bare arm
            case "bones": c.Rect(3, 0, 5, 20, bone); c.Ellipse(4, 10, 1.6f, 1.4f, bone, 0.1f); break; // a bare arm bone and its elbow
            case "shroud": c.Rect(2.2f, 0, 5.8f, 20, pale); c.Rect(1.5f, 0, 6.5f, 5, shroud); break;
            case "robe": c.Rect(1, 0, 7, 20, robe); c.Rect(0.5f, 16, 7.5f, 20, robe, -0.15f); break; // wide sleeves
            case "mail": c.Rect(1.5f, 0, 6.5f, 20, mail); break;
            case "lamellar": c.Rect(1.5f, 0, 6.5f, 20, mail); c.Rect(1, 0, 7, 6, plate); for (int x = 0; x < 8; x++) c.Shade(x, 3, -0.3f); break;
            case "deel": c.Rect(1, 0, 7, 20, cloth); c.Rect(1, 16.5f, 7, 20, trim); break;
            case "brigandine": c.Rect(1.5f, 0, 6.5f, 20, mail); c.Rect(1, 0, 7, 7, clothDk); c.Set(3, 3, brass, 0.65f); c.Set(5, 5, brass, 0.65f); break;
            case "plate":
                c.Rect(1.5f, 0, 6.5f, 20, plate);
                c.Ellipse(4, 10, 3.4f, 2.6f, plate, 0.15f);
                for (int x = 0; x < 8; x++) { c.Shade(x, 5, -0.4f); c.Shade(x, 15, -0.4f); }
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
            case "mail": c.Rect(1.5f, 0, 6.5f, 20, mail); break;
            case "plate":
                c.Rect(1.5f, 0, 6.5f, 20, plate);
                c.Ellipse(4, 10, 3.5f, 2.6f, plate, 0.15f);
                for (int x = 0; x < 8; x++) c.Shade(x, 5, -0.4f);
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

        var neck = new Canvas(23, 13);
        neck.Columns(0, 23, x => { float h = 6 - x * 0.1f, cy = 6.6f + x * 0.03f; return (cy - h, cy + h); }, coat);
        neck.Area((x, y) => { float h = 6 - x * 0.1f, cy = 6.6f + x * 0.03f; return y >= cy - h && y < cy - h + 2.6f; }, mane);
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
        leg.Rect(0.5f, 0, 4.5f, 3, hoof);
        leg.Rect(1, 3, 4, 5, mane);
        leg.Rect(1, 5, 4, 15, coat);
        leg.Ellipse(2.5f, 15.5f, 2.2f, 2, coat, 0.05f);
        leg.Body(17, 30, y => (0.4f - (y - 17) * 0.02f, 4.6f + (y - 17) * 0.02f), coat);
        Add("hleg/n", leg);

        var tail = new Canvas(5, 20);
        tail.Body(0, 20, y => { float half = 2.4f - y * 0.06f; return (2.5f - half, 2.5f + half); }, mane);
        Add("htail/n", tail);

        // A camel (docs/WORLD.md W4) is the horse rig in a sand coat with a hump over the back.
        var hump = new Canvas(24, 12);
        hump.Fill((x, y) => InEl(x, y, 12, 12, 11.5f, 11.5f), (x, y) => new Vector3((x - 12) / 12, (y - 12) / 12, 0.7f), new Mat(Style.Coat, Pattern.Hair));
        Add("hhump/n", hump);
    }

    // ---------- beasts (docs/CREATURES.md) ----------

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
