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
    };

    static readonly string[] Views = { "f", "s", "b" };

    public SpriteSheet()
    {
        for (int p = 0; p < Style.Sides.Length; p++) Side(p);
        Horse();
        Pack();
    }

    void Add(string key, Canvas c) => pending.Add((key, c));

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
        pending.Clear();
    }

    // ---------- materials for one side ----------

    Mat cloth = null!, clothDk = null!, trim = null!, quilt = null!, linen = null!, skin = null!, hair = null!, leather = null!,
        wood = null!, steel = null!, plate = null!, iron = null!, mail = null!, brass = null!, ink = null!, blood = null!, mark = null!;
    Emblem charge;

    void Side(int p)
    {
        var h = Style.Sides[p];
        cloth = new(h.Cloth, Pattern.Cloth); clothDk = new(h.ClothDark, Pattern.Cloth); trim = new(h.Trim);
        quilt = new(Color.Lerp(h.Cloth, Style.Linen, 0.2f), Pattern.Quilt); linen = new(Style.Linen, Pattern.Cloth);
        skin = new(Style.Skin); hair = new(Style.Hair, Pattern.Hair); leather = new(Style.Leather); wood = new(Style.Wood, Pattern.Wood);
        steel = new(Style.Steel); plate = new(Color.Lerp(Style.Steel, Style.Iron, 0.3f), Pattern.Plate); iron = new(Style.Iron);
        mail = new(Color.Lerp(Style.Steel, Style.Iron, 0.15f), Pattern.Mail);
        brass = new(Style.Brass); ink = Mat.Flat(new Color(32, 24, 30)); blood = new(Style.Blood); mark = Mat.Flat(h.Trim);
        charge = h.Charge;
        string s = "/" + p;

        foreach (var v in Views)
        {
            Add($"head.{v}{s}", Head(v));
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
        var m = body switch { "gambeson" => quilt, "mail" => mail, "plate" => plate, _ => cloth };
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
            default:
                c.Area((x, y) => MathF.Abs(x - cx) + MathF.Abs(y - cy) * 0.8f <= s, mark);
                break;
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
            case "mail": c.Rect(1.5f, 0, 6.5f, 20, mail); break;
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
            default: c.Rect(1.5f, 0, 6.5f, 20, clothDk); break;
        }
        return c;
    }

    Canvas Boot(string kind, string v)
    {
        var m = kind == "sabatons" ? plate : leather;
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
        c.Ellipse(2.5f, 2.5f, 2.4f, 2.4f, kind == "gauntlets" ? plate : skin);
        return c;
    }

    Canvas Shield(string kind)
    {
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

    Canvas Blood()
    {
        var c = new Canvas(15, 8);
        c.Ellipse(7.5f, 4, 7, 3.4f, blood, -0.2f);
        c.Ellipse(12, 6, 2.5f, 1.6f, blood, -0.2f);
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
