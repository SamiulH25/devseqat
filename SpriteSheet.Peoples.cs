using Microsoft.Xna.Framework;

namespace Ironfield;

/// The peoples' own kit (docs/archive/WORLDMAP.md WM5): helms, harness and horse barding that mark one people of a realm from
/// another at tiers V-VII, so the Heartland's knights, the March's riders and the Coast's marines are told apart at a glance.
public sealed partial class SpriteSheet
{
    /// Helms only a people wears; `Look.Heads` lists them, `Puppet.HelmTop` places them.
    public static readonly string[] PeopleHelms =
        { "frogmouth", "galea", "ibex", "dragon", "bearhead", "spike_turban", "litham", "corsair", "tug", "faceplate", "lion", "chapel", "greathelm_sun", "winged_sallet", "whitebear" };

    /// Helms a people's tier VII wears gilt: the same helm with a rim of brass round its edge ("<helm>_gilt").
    public static readonly string[] GiltHelms =
        { "frogmouth", "winged_sallet", "morion", "galea", "ibex", "dragon", "spike_turban", "litham", "corsair", "tug", "faceplate", "chapel", "bascinet" };

    /// Every helm this file adds, gilt ones included, for `Look.Heads`.
    public static IEnumerable<string> AllPeopleHelms => PeopleHelms.Concat(GiltHelms.Select(h => h + "_gilt"));

    /// The helm a gilt one is painted from (and placed as).
    public static string HelmBase(string kind) => kind.EndsWith("_gilt") ? kind[..^5] : kind;

    /// Harness only a people wears; `Look.Bodies` lists them.
    public static readonly string[] PeopleBodies = { "muscle", "kaftan", "mirror", "jupon", "lamellar_dark", "burnous" };

    /// Horse barding by people: full ones cover chest, rump, neck and head; half ones only the neck and head.
    public static readonly string[] PeopleBardings = { "halfbarding", "mailbarding", "furbarding", "lamellarbarding", "partibarding", "crossbarding", "tasselled", "chequybarding" };

    /// Every barding a mount may name for the rig (`Puppet`); "wolf" and "camel" are drawn their own way.
    public static readonly string[] Bardings = new[] { "saddle", "steppetack", "caparison", "gildedcaparison", "scalebarding" }.Concat(PeopleBardings).ToArray();

    static readonly Mat Bronze = new(new Color(178, 126, 70), Pattern.Plate), Pelt = new(new Color(122, 88, 58), Pattern.Hair),
        Horsehair = new(new Color(40, 34, 32), Pattern.Hair), Muzzle = new(new Color(178, 142, 104)), Silver = new(new Color(170, 176, 186)),
        Lacquer = new(new Color(70, 50, 50), Pattern.Plate), Horn = new(new Color(196, 182, 148)), Fleece = new(new Color(214, 206, 188), Pattern.Hair);

    /// A people's helm, or null for the common ones.
    Canvas? PeopleHelm(string kind, string v)
    {
        if (kind.EndsWith("_gilt"))
        {
            var g = Helm(HelmBase(kind), v);
            // Two pixels of gilt round the edge: the outer ring, then the ring inside it on the upper half.
            var ring = new List<(int, int)>();
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                    if (g.Has(x, y) && y < g.H * 0.6f && g.Has(x - 1, y) && g.Has(x + 1, y) && g.Has(x, y - 1) && g.Has(x, y + 1)
                        && (!g.Has(x - 2, y) || !g.Has(x + 2, y) || !g.Has(x, y - 2))) ring.Add((x, y));
            g.Border(brass, 0.62f);
            foreach (var (x, y) in ring) g.Set(x, y, brass, 0.42f);
            return g;
        }
        switch (kind)
        {
            case "winged_sallet":
            {
                // The March's: a sallet with its tail over the neck and a pair of steel wings rising from the sides.
                var c = new Canvas(26, 19);
                int ox = 4, top = 6;
                c.Fill((x, y) => InEl(x, y, ox + 9, top + 8.2f, 8, 7.7f) && y < top + 9.3f, (x, y) => new Vector3((x - ox - 9) / 9, (y - top - 8) / 9, 0.6f), plate);
                if (v == "s") c.Body(top + 7.5f, top + 12.8f, y => (ox + 0.3f, ox + 9 - (y - top - 7.5f) * 0.6f), plate, -0.1f);
                else if (v == "b") c.Body(top + 8.5f, top + 12.8f, y => (ox + 1 + (y - top - 8.5f) * 0.4f, ox + 17 - (y - top - 8.5f) * 0.4f), plate, -0.1f);
                else for (int x = ox + 1; x < ox + 17; x++) c.Set(x, top + 9, iron, 0.2f);
                for (int i = 0; i < 7; i++)
                {
                    int wy = top + 4 - i;
                    if (v == "s") for (int k = 0; k < 3; k++) c.Set(ox + 6 - i / 2 - k, wy, linen, 0.72f - k * 0.12f);
                    else for (int k = 0; k < 3; k++) { c.Set(ox + 1 - i / 2 - k + 2, wy, linen, 0.72f - k * 0.12f); c.Set(ox + 16 + i / 2 + k - 2, wy, linen, 0.6f - k * 0.12f); }
                }
                return c;
            }
            case "frogmouth":
            {
                // The Heartland's tourney helm: a tall skull, the sight a dark slit under its brow, the jaw plate jutting out below.
                var c = new Canvas(20, 22);
                c.Fill((x, y) => x > 1.5f && x < (v == "s" ? 15.5f : 18.5f) && y > 0.3f && InEl(x, y, 10, 9, 9, 9.5f) || y > 8 && x > 1.5f && x < 18.5f,
                    (x, y) => new Vector3((x - 10) / 10, (y - 8) / 12, 0.6f), plate);
                if (v == "f")
                {
                    for (int x = 2; x < 18; x++) { c.Set(x, 6, ink); c.Set(x, 7, ink); c.Set(x, 8, brass, 0.55f); }
                    c.Body(9, 21.5f, y => (2.2f + (y - 9) * 0.1f, 17.8f - (y - 9) * 0.1f), plate, 0.18f); // the jaw plate, lit
                    for (int y = 1; y < 7; y++) c.Set(10, y, plate, 0.8f);
                    for (int y = 10; y < 20; y++) c.Set(10, y, plate, 0.85f);
                    c.Rect(2.4f, 17.5f, 17.6f, 19, brass, 0.3f); // a gilt band across the jaw
                    foreach (var (bx, by) in new[] { (4, 11), (16, 11), (4, 17), (16, 17) }) c.Set(bx, by, brass, 0.65f);
                    for (int x = 6; x < 15; x += 2) c.Set(x, 14, ink);
                }
                else if (v == "s")
                {
                    c.Body(8, 21.5f, y => (2, 19.6f - MathF.Max(0, y - 12) * 0.25f), plate, 0.18f); // jutting forward under the slit
                    for (int x = 8; x < 19; x++) { c.Set(x, 6, ink); c.Set(x, 7, ink); c.Set(x, 8, brass, 0.55f); }
                    c.Set(16, 12, brass, 0.65f); c.Set(16, 16, brass, 0.65f);
                }
                else for (int y = 2; y < 20; y++) c.Shade(10, y, -0.25f);
                c.Rect(2, 20.5f, 18, 22, brass, 0.2f); // a gilt rim
                return c;
            }
            case "galea":
            {
                // The Patricians' crested helm: a bronze bowl, hinged cheek guards and a crest across the crown in the side's cloth.
                var c = new Canvas(22, 20);
                c.Fill((x, y) => InEl(x, y, 11, 12, 7.4f, 7.2f) && y < 13, (x, y) => new Vector3((x - 11) / 8, (y - 11) / 8, 0.6f), Bronze);
                c.Rect(3.4f, 12, 18.6f, 13.6f, brass, 0.2f);
                if (v == "f") { c.Body(13, 19.5f, y => (3.6f, 6.4f - (y - 13) * 0.1f), Bronze, 0.1f); c.Body(13, 19.5f, y => (15.6f + (y - 13) * 0.1f, 18.4f), Bronze, 0.1f); }
                else if (v == "s") { c.Body(13, 19.5f, y => (11, 15.5f), Bronze, 0.1f); c.Rect(3.6f, 13, 9, 17, Bronze, -0.1f); }
                else c.Rect(3.6f, 13, 18.4f, 17, Bronze, -0.1f);
                if (v == "s") c.Body(0.5f, 6.5f, y => (9.5f, 12.5f), plumeCloth); // seen edge on
                else
                {
                    c.Body(0.5f, 6.5f, y => (11 - 9.5f + MathF.Abs(y - 3.5f) * 0.9f, 11 + 9.5f - MathF.Abs(y - 3.5f) * 0.9f), plumeCloth);
                    for (int x = 2; x < 20; x += 2) c.Shade(x, 1, -0.25f);
                }
                c.Rect(9.5f, 5.5f, 12.5f, 7, brass, 0.3f); // the crest's holder
                return c;
            }
            case "ibex":
            {
                // The Alpine guard's helm: a round steel skull with an ibex's ridged horns sweeping up and back.
                var c = new Canvas(26, 22);
                c.Fill((x, y) => InEl(x, y, 13, 14, 7.6f, 7.4f) && y < 15, (x, y) => new Vector3((x - 13) / 8, (y - 13) / 8, 0.6f), plate);
                c.Rect(4.8f, 14, 21.2f, 15.8f, iron, 0.05f);
                if (v == "f") c.Rect(12, 15, 14, 20, iron, 0.15f); // the nasal
                if (v == "s") c.Rect(18.5f, 15, 20.5f, 20, iron, 0.15f);
                if (v == "s")
                    for (int i = 0; i < 12; i++) { float t = i / 11f; int hx = (int)(14 - t * 9 + t * t * 3), hy = (int)(8 - t * 8 + t * t * 2); c.Set(hx, hy, Horn, i % 2 == 0 ? 0.6f : 0.35f); c.Set(hx + 1, hy, Horn, 0.3f); c.Set(hx, hy + 1, Horn, 0.2f); }
                else
                    for (int sd = -1; sd <= 1; sd += 2)
                        for (int i = 0; i < 12; i++)
                        {
                            float t = i / 11f; int hx = (int)(13 + sd * (4 + t * 8 - t * t * 3)), hy = (int)(9 - t * 9 + t * t * 1.5f);
                            c.Set(hx, hy, Horn, i % 2 == 0 ? 0.62f : 0.36f); c.Set(hx - sd, hy, Horn, 0.3f); c.Set(hx, hy + 1, Horn, 0.2f);
                        }
                return c;
            }
            case "dragon":
            {
                // The Fjord's chosen: a riveted cone, a nasal, and a carved dragon's neck curling up over the crown like a prow.
                var c = new Canvas(24, 21);
                c.Body(7.3f, 16, y => (12 - 1 - (y - 7.3f) * 0.82f, 12 + 1 + (y - 7.3f) * 0.82f), plate);
                c.Rect(4.5f, 14.5f, 19.5f, 16.5f, brass);
                if (v == "f") c.Rect(11, 15, 13, 21, iron, 0.1f);
                if (v == "s") c.Rect(18, 15, 20, 21, iron, 0.1f);
                if (v == "s")
                {
                    // An S from the crown back and up, the head looking forward.
                    foreach (var (x, y) in new[] { (11, 7), (10, 6), (9, 5), (9, 4), (10, 3), (11, 2), (12, 2), (13, 1), (14, 1), (15, 1), (16, 2) })
                    { c.Set(x, y, brass, 0.55f); c.Set(x, y + 1, brass, 0.25f); }
                    c.Set(17, 2, brass, 0.4f); c.Set(16, 3, brass, 0.2f); c.Set(14, 2, ink);
                }
                else
                {
                    c.Rect(11, 2, 13, 7.5f, brass, 0.35f);
                    c.Ellipse(12, 2, 3.4f, 2, brass, 0.2f);
                    c.Set(9, 0, brass, 0.6f); c.Set(15, 0, brass, 0.4f);
                    c.Set(10, 2, ink); c.Set(14, 2, ink);
                    for (int y = 3; y < 8; y += 2) c.Shade(12, y, -0.3f);
                }
                return c;
            }
            case "bearhead":
            case "whitebear":
            {
                // The Taiga's: a whole bear's head and pelt worn as a hood, the snout over the brow and the fur down to the shoulders.
                var c = new Canvas(24, 22);
                var Pelt = kind == "whitebear" ? Fleece : SpriteSheet.Pelt;
                c.Ellipse(12, 9.5f, 10, 8, Pelt);
                if (v != "s") { c.Rect(1.6f, 9, 5, 22, Pelt, -0.1f); c.Rect(19, 9, 22.4f, 22, Pelt, -0.1f); }
                else c.Rect(2, 9, 9, 22, Pelt, -0.1f);
                if (v == "b") c.Rect(2, 9, 22, 22, Pelt, -0.05f);
                foreach (int ex in v == "s" ? new[] { 9 } : new[] { 5, 19 }) { c.Ellipse(ex, 2.5f, 2.4f, 2.2f, Pelt, 0.1f); c.Set(ex, 3, Muzzle, 0.2f); }
                if (v == "f")
                {
                    c.Ellipse(12, 13, 4, 2.8f, Muzzle, 0.1f);
                    c.Rect(11, 11, 13, 12.5f, ink);
                    c.Set(8, 9, ink); c.Set(16, 9, ink);
                    for (int x = 7; x < 18; x++) c.Shade(x, 15, -0.3f);
                }
                else if (v == "s")
                {
                    c.Body(9.5f, 15, y => (14, 23.5f - MathF.Abs(y - 12) * 0.5f), Muzzle, 0.05f);
                    c.Set(23, 11, ink); c.Set(17, 10, ink);
                }
                return c;
            }
            case "spike_turban":
            {
                // The Oasis's: linen wound thick round a tall spiked helm, a mail veil at the cheeks.
                var c = new Canvas(20, 22);
                c.Body(0.3f, 9, y => (10 - 0.4f - y * 0.5f, 10 + 0.4f + y * 0.5f), plate);
                c.Rect(9.5f, 0, 10.5f, 2, brass, 0.6f);
                c.Fill((x, y) => InEl(x, y, 10, 15.5f, 9.4f, 7) && y < 18.5f && y > 8, (x, y) => new Vector3((x - 10) / 10, (y - 14) / 9, 0.6f), linen);
                for (int x = 0; x < 20; x++) for (int y = 8; y < 19; y++) if ((y + x / 3) % 3 == 0) c.Shade(x, y, -0.22f);
                if (v != "b") c.Ellipse(v == "s" ? 15 : 10, 13, 1.6f, 1.6f, brass, 0.4f); // a jewel on the wrap
                if (v == "f") { c.Rect(1.4f, 17, 4.4f, 22, mail); c.Rect(15.6f, 17, 18.6f, 22, mail); }
                else c.Rect(1.4f, 17, v == "s" ? 12 : 18.6f, 22, mail);
                return c;
            }
            case "litham":
            {
                // The Dune's: a long cloth wound over the head and face, only the eyes showing, its tail hanging behind.
                var c = new Canvas(20, 21);
                c.Fill((x, y) => InEl(x, y, 10, 9, 8.6f, 8.6f) || y > 8 && x > 1.6f && x < 18.4f && y < 20, (x, y) => new Vector3((x - 10) / 10, (y - 8) / 12, 0.6f), clothDk);
                for (int x = 0; x < 20; x++) for (int y = 1; y < 20; y++) if ((y + x / 2) % 4 == 0) c.Shade(x, y, -0.2f);
                c.Rect(2, 5, 18, 6.6f, trim, 0.1f);
                if (v == "f")
                {
                    c.Rect(4, 8, 16, 11.6f, skin);
                    c.Set(7, 9, ink); c.Set(12, 9, ink); c.Set(7, 10, ink); c.Set(12, 10, ink);
                }
                else if (v == "s") { c.Rect(13, 8.5f, 18.4f, 10.5f, skin); c.Set(16, 9, ink); c.Rect(0, 10, 3.5f, 21, clothDk, -0.15f); }
                else c.Rect(8, 10, 12, 21, clothDk, -0.15f);
                return c;
            }
            case "corsair":
            {
                // The Rift's: a steel cap with a scarf in the side's cloth knotted round it, the ends hanging loose.
                var c = new Canvas(22, 17);
                c.Fill((x, y) => InEl(x, y, 11, 9.5f, 7.4f, 7) && y < 10.5f, (x, y) => new Vector3((x - 11) / 8, (y - 8) / 8, 0.6f), plate);
                c.Body(7, 10.6f, y => (11 - 7.8f, 11 + 7.8f), cloth);
                for (int x = 3; x < 20; x += 3) c.Shade(x, 9, -0.3f);
                for (int y = 3; y < 7; y++) c.Set(11, y, plate, 0.75f);
                if (v == "s") { c.Ellipse(3.5f, 9, 2, 1.8f, cloth, 0.1f); c.Line(3, 10, 1, 16, cloth, 0.3f, 2); c.Line(4, 10, 5, 15, cloth, 0.2f, 2); }
                else if (v == "f") { c.Ellipse(18, 9, 2, 1.8f, cloth, 0.1f); c.Line(18, 10, 20, 16, cloth, 0.3f, 2); c.Line(17, 10, 17, 15, cloth, 0.2f, 2); c.Set(4, 12, brass, 0.8f); }
                else { c.Ellipse(11, 9.5f, 2, 1.8f, cloth, 0.1f); c.Line(10, 10, 9, 16, cloth, 0.3f, 2); c.Line(12, 10, 13, 16, cloth, 0.2f, 2); }
                return c;
            }
            case "tug":
            {
                // The Horde's banner helm: the tall steel cone with its fur brim, a long tail of black horsehair streaming from the point.
                var c = new Canvas(24, 23);
                int oy = 6;
                c.Body(oy + 2, oy + 13, y => (12 - 0.8f - (y - oy - 2) * 0.68f, 12 + 0.8f + (y - oy - 2) * 0.68f), plate);
                c.Rect(3.5f, oy + 11.5f, 20.5f, oy + 15.5f, fur);
                for (int x = 3; x < 21; x += 2) c.Shade(x, oy + 15, -0.3f);
                for (int y = oy + 4; y < oy + 11; y++) c.Set(12, y, brass, 0.4f);
                c.Rect(10.5f, oy - 0.5f, 13.5f, oy + 2.5f, brass, 0.4f);
                int dir = v == "s" ? -1 : 1;
                for (int i = 0; i < 16; i++)
                {
                    float t = i / 15f; int hx = (int)(12 + dir * (t * 10)), hy = (int)(oy - 1 - MathF.Sin(t * MathF.PI * 0.8f) * 5 + t * 12);
                    for (int k = -1; k <= 1; k++) c.Set(hx, hy + k, Horsehair, 0.45f + k * 0.1f);
                }
                return c;
            }
            case "faceplate":
            {
                // The Salt Khans' helm: a rounded bowl with a polished steel face, brows and lips worked in brass, mail behind.
                var c = new Canvas(19, 21);
                c.Fill((x, y) => InEl(x, y, 9.5f, 9, 7.8f, 8.6f) && y > 0.3f, (x, y) => new Vector3((x - 9.5f) / 8, (y - 8) / 9, 0.6f), plate);
                c.Rect(1.5f, 12, 17.5f, 21, mail);
                for (int y = 0; y < 3; y++) c.Set(9, y, brass, 0.5f);
                if (v == "f")
                {
                    c.Fill((x, y) => InEl(x, y, 9.5f, 12, 5.6f, 7.4f) && y > 6.5f, (x, y) => new Vector3((x - 9.5f) / 6, (y - 11) / 8, 0.7f), Silver);
                    for (int x = 5; x < 15; x++) if (x is not (9 or 10)) c.Set(x, 8, brass, 0.6f);
                    c.Set(7, 10, ink); c.Set(12, 10, ink);
                    for (int y = 10; y < 15; y++) c.Set(9, y, Silver, 0.6f);
                    for (int x = 8; x < 12; x++) c.Set(x, 16, brass, 0.4f);
                }
                else if (v == "s")
                {
                    c.Body(7, 19, y => (13, 18.2f - MathF.Abs(y - 12) * 0.25f), Silver, 0.1f);
                    c.Set(16, 10, ink); c.Set(17, 13, Silver, 0.6f);
                }
                c.Rect(2, 6.5f, 17, 7.8f, brass, 0.2f);
                return c;
            }
            case "lion":
            {
                // The Lagoon's gilded barbute: a close helm under a mane of gilt brass round the crown and down the cheeks.
                var c = new Canvas(22, 24);
                int ox = 2, top = 4;
                c.Fill((x, y) => InEl(x, y, ox + 9, top + 9.5f, 8.2f, 9.2f) && y > top + 0.3f, (x, y) => new Vector3((x - ox - 9) / 9, (y - top - 8) / 10, 0.6f), plate);
                if (v == "f") { for (int x = ox + 4; x < ox + 14; x++) c.Set(x, top + 8, ink); for (int y = top + 8; y < top + 17; y++) { c.Set(ox + 8, y, ink); c.Set(ox + 9, y, ink); } }
                else if (v == "s") for (int y = top + 8; y < top + 17; y++) c.Set(ox + 16, y, ink);
                // The mane: tufts round the top edge, longer at the sides, each lit on its left.
                for (int i = 0; i < 18; i++)
                {
                    float a = MathF.PI * (0.12f + i / 17f * 0.76f);
                    float cx = 11 - MathF.Cos(a) * 8.6f, cy = top + 9 - MathF.Sin(a) * 9.0f;
                    int len = i % 2 == 0 ? 3 : 2;
                    for (int k = 0; k < len; k++) c.Set((int)(cx - MathF.Cos(a) * k * 0.6f), (int)(cy - MathF.Sin(a) * k * 0.8f), brass, 0.65f - k * 0.12f);
                }
                if (v == "f") { for (int x = ox + 2; x < ox + 16; x++) c.Set(x, top + 17, brass, 0.5f); } // a gilt lip at the jaw
                return c;
            }
            case "chapel":
            {
                // The Fen's: a broad-brimmed iron hat and a plate bevor up to the nose.
                var c = new Canvas(26, 18);
                c.Fill((x, y) => InEl(x, y, 13, 9, 7.2f, 7.4f) && y < 9.5f, (x, y) => new Vector3((x - 13) / 8, (y - 8) / 8, 0.6f), iron);
                for (int x = 0; x < 26; x++)
                {
                    float droop = MathF.Max(0, MathF.Abs(x - 12.5f) - 6) * 0.3f;
                    c.Set(x, (int)(9 + droop), iron, 0.35f);
                    c.Set(x, (int)(10 + droop), iron, 0.1f);
                }
                for (int y = 2; y < 9; y++) c.Set(13, y, iron, 0.7f); // the comb's gleam
                if (v == "f") c.Body(12.5f, 18, y => (7.5f, 18.5f), plate, 0.05f);
                else if (v == "s") c.Body(12.5f, 18, y => (12, 19.5f), plate, 0.05f);
                if (v != "b") for (int x = 8; x < 19; x += 3) c.Set(x, 13, brass, 0.55f);
                return c;
            }
        }
        return null;
    }

    /// The Order's chapter helm: a greathelm under a gilt sun with rays (painted into the ornamented greathelm's headroom).
    void SunCrest(Canvas c, int ox, int oy, string v)
    {
        float cx = ox + 9, cy = oy - 2.5f;
        int n = v == "s" ? 5 : 9;
        for (int i = 0; i < n; i++)
        {
            float a = MathF.PI * (i + 0.5f) / n;
            float w = v == "s" ? 0.45f : 1f;
            c.Line(cx - MathF.Cos(a) * 3 * w, cy - MathF.Sin(a) * 3, cx - MathF.Cos(a) * 7 * w, cy - MathF.Sin(a) * 6.5f, brass, 0.6f);
        }
        c.Area((x, y) => InEl(x, y, cx, cy + 0.5f, v == "s" ? 1.8f : 3.4f, 3.2f) && y < oy + 0.5f, brass, 0.7f);
    }

    /// A people's harness, or null for the common ones.
    Canvas? PeopleTorso(string body, string v)
    {
        if (Array.IndexOf(PeopleBodies, body) < 0) return null;
        bool side = v == "s";
        var c = new Canvas(side ? 13 : 20, 23);
        Func<float, (float, float)> span = side ? SideSpan : TorsoSpan;
        float mid = (c.W - 1) / 2f;
        switch (body)
        {
            case "muscle":
                // A bronze cuirass shaped to the chest and belly, a row of leather strips over a cloth skirt.
                c.Body(0, 23, span, cloth);
                c.Fill((x, y) => y < 14 && x >= span(y).Item1 && x <= span(y).Item2, (x, y) => new Vector3((x - mid) / 10, (y - 6) / 12, 0.6f), Bronze);
                if (v == "f")
                {
                    c.Ellipse(6.5f, 6, 3.2f, 2.4f, Bronze, 0.3f); c.Ellipse(13.5f, 6, 3.2f, 2.4f, Bronze, 0.15f);
                    for (int x = 3; x < 17; x++) if (x is not (10)) c.Shade(x, 8, -0.4f);       // under the chest
                    for (int y = 3; y < 13; y++) c.Shade(10, y, -0.4f);                            // down the middle
                    foreach (int y in new[] { 10, 12 }) for (int x = 6; x < 15; x++) c.Shade(x, y, -0.3f); // the belly
                    c.Body(1, 2, span, brass, 0.3f);
                }
                c.Body(13, 14.6f, span, brass, 0.2f);
                for (int x = 1; x < c.W - 1; x += 2) for (int y = 15; y < 21; y++) if (c.Has(x, y)) c.Set(x, y, leather, 0.4f - (y - 15) * 0.03f);
                for (int x = 0; x < c.W; x += 2) if (c.Has(x, 22)) c.Shade(x, 22, -0.3f);
                break;
            case "kaftan":
                // A long pale coat over mail, frogs of the side's cloth across the chest and a wide sash of it.
                c.Body(0, 23, span, linen);
                c.Body(0, 2.4f, span, mail);
                if (v == "f")
                {
                    for (int y = 3; y < 13; y += 2) for (int x = 7; x < 14; x++) c.Set(x, y, cloth, 0.35f + (x == 7 || x == 13 ? 0.3f : 0));
                    for (int y = 16; y < 23; y++) c.Shade(10, y, -0.35f);
                }
                c.Body(12.5f, 16.2f, span, cloth, -0.05f);
                for (int x = 0; x < c.W; x++) { c.Set(x, 21, cloth, 0.3f); }
                for (int x = 0; x < c.W; x++) c.Shade(x, 22, -0.25f);
                break;
            case "mirror":
                // Mail with a great polished disc on the chest and smaller ones at the flanks, held by straps.
                c.Body(0, 23, span, mail);
                c.Body(15.6f, 23, span, clothDk);
                if (!side)
                {
                    c.Ellipse(mid + 0.5f, 7.5f, 4.6f, 4.6f, Silver, 0.15f);
                    c.Ellipse(mid + 0.5f, 7.5f, 1.6f, 1.6f, brass, 0.4f);
                    c.Line(1, 3, mid - 3, 5, leather); c.Line(c.W - 2, 3, mid + 4, 5, leather);
                    c.Ellipse(2.6f, 11, 1.8f, 2.2f, Silver); c.Ellipse(c.W - 3.4f, 11, 1.8f, 2.2f, Silver);
                }
                else c.Ellipse(mid, 8, 3.6f, 4.4f, Silver, 0.15f);
                c.Body(13.5f, 15.6f, span, leather);
                break;
            case "jupon":
                // A quilted coat parted down the middle: the side's cloth on the right shoulder, its trim on the left.
                c.Body(0, 23, span, cloth);
                if (!side) c.Area((x, y) => x < mid + 0.5f && x >= span(y).Item1 && x <= span(y).Item2, trim);
                else c.Area((x, y) => y < 11.5f && x >= span(y).Item1 && x <= span(y).Item2, trim);
                for (int y = 2; y < 23; y += 3) for (int x = 0; x < c.W; x++) c.Shade(x, y, -0.2f);
                for (int y = 0; y < 23; y++) for (int x = 1; x < c.W; x += 4) c.Shade(x, y, -0.12f);
                c.Body(0, 2.2f, span, plate, 0.05f);
                c.Body(13.5f, 15.6f, span, leather);
                if (v == "f") { c.Set(9, 14, brass, 0.7f); c.Set(10, 14, brass, 0.7f); }
                break;
            case "lamellar_dark":
                // Black lacquered lamellar laced in the side's cloth, a cloth skirt below.
                c.Body(0, 23, span, Lacquer);
                for (int y = 1; y < 14; y++) for (int x = 0; x < c.W; x++)
                {
                    int row = y % 3;
                    if (row == 0) c.Shade(x, y, -0.35f); else if (row == 1) c.Shade(x, y, 0.22f);
                }
                for (int y = 3; y < 14; y += 3) for (int x = 1 + y / 3 % 2 * 2; x < c.W - 1; x += 3) if (c.Has(x, y)) c.Set(x, y, cloth, 0.5f);
                c.Body(13.5f, 15.6f, span, trim, -0.05f);
                c.Body(15.6f, 23, span, cloth);
                for (int x = 0; x < c.W; x++) c.Shade(x, 22, -0.25f);
                break;
            case "burnous":
                // A pale robe striped in the side's colours, a mail shirt showing at the neck.
                c.Body(0, 23, span, linen);
                for (int x = 0; x < c.W; x++) if (x % 5 is 1 or 2) for (int y = 3; y < 23; y++) if (c.Has(x, y)) c.Set(x, y, x % 10 < 5 ? cloth : clothDk, 0.4f);
                c.Body(0, 3, span, mail);
                c.Body(13.5f, 15.6f, span, leather);
                if (v == "f") { for (int y = 3; y < 23; y++) c.Shade(10, y, -0.3f); c.Set(9, 14, brass, 0.7f); c.Set(10, 14, brass, 0.7f); }
                for (int x = 0; x < c.W; x++) c.Shade(x, 22, -0.25f);
                break;
        }
        return c;
    }

    /// A people's sleeve (wrist at row 0), or null.
    Canvas? PeopleSleeve(string body)
    {
        if (Array.IndexOf(PeopleBodies, body) < 0) return null;
        var c = new Canvas(8, 20);
        switch (body)
        {
            case "muscle": c.Rect(1.5f, 0, 6.5f, 20, leather); c.Rect(1.5f, 0, 6.5f, 6, Bronze); c.Rect(1, 15, 7, 20, cloth); for (int y = 16; y < 20; y += 2) for (int x = 1; x < 7; x++) c.Shade(x, y, -0.25f); break;
            case "kaftan": c.Rect(1, 0, 7, 20, linen); c.Rect(1, 0, 7, 2.5f, cloth); c.Rect(1.5f, 2.5f, 6.5f, 4, mail); break;
            case "mirror": c.Rect(1.5f, 0, 6.5f, 20, mail); c.Rect(1, 0, 7, 6, Silver, 0.1f); c.Rect(1, 15, 7, 20, Silver, 0.05f); break;
            case "jupon": c.Rect(1, 0, 7, 20, cloth); c.Rect(1, 0, 7, 2.5f, plate); for (int y = 4; y < 20; y += 3) for (int x = 0; x < 8; x++) c.Shade(x, y, -0.25f); break;
            case "lamellar_dark": c.Rect(1.5f, 0, 6.5f, 20, mail); c.Rect(1, 0, 7, 7, Lacquer); for (int x = 0; x < 8; x++) { c.Shade(x, 2, -0.35f); c.Shade(x, 5, -0.35f); } c.Set(4, 3, cloth, 0.5f); break;
            case "burnous": c.Rect(0.6f, 0, 7.4f, 20, linen); c.Rect(0.6f, 0, 7.4f, 2, cloth); for (int y = 4; y < 20; y += 5) for (int x = 0; x < 8; x++) c.Set(x, y, cloth, 0.4f); break;
        }
        return c;
    }

    /// The peoples' horse barding: every one has a body piece; the full ones also chest, rump, neck and head, the half
    /// ones a neck and head (crinet and chanfron) over a saddle cloth.
    void PeopleBarding(string s)
    {
        // Half barding: steel over the neck and face, a saddle cloth in the side's colour. (The March and the Alps.)
        var half = new Canvas(55, 23);
        Saddle(half, true);
        Add($"hbody.halfbarding{s}", half);
        Crinet(s, "halfbarding", plate, iron, true);

        // Mail barding: rings to the knees, a steel face. (The Fjord and the Rift.)
        Full(s, "mailbarding", mail, leather, false, c => { for (int x = 2; x < c.W - 2; x += 5) for (int y = 0; y < c.H; y++) c.Shade(x, y, -0.15f); });

        // Fur: a heavy pelt for a saddle cloth, a mane braided with the side's cloth. (The Taiga.)
        var fur = new Canvas(55, 23);
        fur.Columns(12, 44, x => { var (t, _) = HorseSpan(x); return (t + 1.2f, 20.5f + MathF.Sin(x * 1.3f) * 1.4f); }, Fleece);
        for (int x = 12; x < 44; x += 2) { fur.Shade(x, 19, -0.3f); fur.Shade(x + 1, 20, -0.35f); }
        fur.Border(Pelt);
        Saddle(fur, false);
        fur.Rect(26, 6, 32, 8, cloth);
        fur.Soft = true;
        Add($"hbody.furbarding{s}", fur);

        // Lacquered lamellar laced in the side's cloth. (The Horde.)
        Full(s, "lamellarbarding", Lacquer, cloth, true, c =>
        {
            for (int y = 2; y < c.H; y++) for (int x = 0; x < c.W; x++)
            {
                if (!c.Has(x, y)) continue;
                int row = y % 3;
                if (row == 0) c.Shade(x, y, -0.35f); else if (row == 1) c.Shade(x, y, 0.2f);
                if (row == 2 && x % 4 == y / 3 % 2 * 2) c.Set(x, y, cloth, 0.45f);
            }
        });

        // Parti-coloured cloth, gilt edged: the side's cloth to the front, its trim behind. (The Lagoon.)
        Full(s, "partibarding", cloth, brass, false, c =>
        {
            for (int y = 0; y < c.H; y++) for (int x = 0; x < c.W * 0.5f; x++) if (c.Has(x, y)) c.Set(x, y, trim, 0.45f);
        });

        // Chequered in the side's cloth and trim, steel at the face. (The March.)
        Full(s, "chequybarding", cloth, iron, false, c =>
        {
            for (int y = 0; y < c.H; y++) for (int x = 0; x < c.W; x++) if (c.Has(x, y) && (x / 4 + y / 4) % 2 == 0) c.Set(x, y, trim, 0.45f);
        });

        // White with a great cross in the side's cloth. (The Chapter.)
        Full(s, "crossbarding", linen, cloth, false, c =>
        {
            float cx = c.W * 0.5f, cy = c.H * 0.52f, s = MathF.Min(c.W, c.H) * 0.28f;
            c.Line(cx, cy - s, cx, cy + s, cloth, 0.45f, 3);
            c.Line(cx - s, cy - s * 0.15f, cx + s, cy - s * 0.15f, cloth, 0.45f, 3);
        });

        // A saddle with a breast collar and a fringe of tassels in the side's colours. (The Oasis.)
        var tas = new Canvas(55, 23);
        tas.Columns(15, 42, x => { var (t, _) = HorseSpan(x); return (t + 2f, 16.5f); }, cloth);
        for (int x = 15; x < 42; x++) { tas.Set(x, 16, trim, 0.5f); tas.Set(x, 17, trim, 0.3f); }
        Saddle(tas, false);
        for (int x = 36; x < 53; x++) { var (t, _) = HorseSpan(x); int y = (int)(t + 4 + (x - 36) * 0.3f); tas.Set(x, y, trim, 0.5f); tas.Set(x, y + 1, trim, 0.3f); }
        for (int x = 16; x < 53; x += 2)
        {
            var (t, _) = HorseSpan(x);
            int y0 = x < 42 ? 18 : (int)(t + 6 + (x - 36) * 0.3f);
            for (int k = 0; k < 4; k++) tas.Set(x, y0 + k, (x / 2) % 2 == 0 ? cloth : trim, 0.5f - k * 0.08f);
        }
        Add($"hbody.tasselled{s}", tas);
        Crinet(s, "tasselled", cloth, trim, false);
    }

    /// A full barding set in one stuff and edging; `work` decorates each piece.
    void Full(string s, string tag, Mat body, Mat edge, bool rows, Action<Canvas> work)
    {
        var steelMat = rows ? Lacquer : plate;
        var cap = new Canvas(55, 23);
        cap.Columns(1.5f, 53.5f, x => { var (t, _) = HorseSpan(x); return (t + 1.5f, 22.0f - ((int)x % 6 < 3 ? 1.4f : 0)); }, body);
        work(cap);
        if (!rows)
        {
            // Draped vertical cloth pleats so heraldic caparisons read as heavy woven cloth.
            for (int x = 6; x < 50; x += 5)
                for (int y = 6; y < 21; y++)
                    if (cap.Has(x, y)) { cap.Shade(x, y, -0.20f); if (cap.Has(x + 1, y)) cap.Shade(x + 1, y, 0.12f); }
        }
        cap.Border(edge);
        Saddle(cap, false);
        cap.Soft = true;
        Add($"hbody.{tag}{s}", cap);
        foreach (var part in new[] { "hchest", "hrump" })
        {
            var c = new Canvas(24, 21);
            c.Ellipse(12, 10.8f, 11.4f, 9.6f, body);
            work(c);
            if (part == "hchest")
            {
                // Armored peytral breastplate across the upper chest with central keel & rivets.
                c.Body(2, 9.5f, y => { float hw = 5.2f + (y - 2) * 0.65f; return (12 - hw, 12 + hw); }, steelMat);
                for (int y = 3; y < 9; y++) { c.Shade(12, y, 0.28f); c.Shade(11, y, -0.18f); }
                for (int x = 5; x <= 19; x += 3) if (c.Has(x, 9)) c.Set(x, 9, edge);
            }
            else
            {
                for (int x = 5; x <= 19; x += 4) for (int y = 5; y < 19; y++) if (c.Has(x, y)) c.Shade(x, y, -0.20f);
                c.Rect(10.8f, 1, 13.2f, 7, Mat.Flat(Style.Mane));
            }
            c.Border(edge);
            c.Soft = true;
            Add($"{part}.{tag}{s}", c);
        }
        var neck = new Canvas(23, 13);
        neck.Columns(0, 23, x => (HorseNeckTop(x) + 1.8f, HorseNeckBottom(x)), body);
        work(neck);
        neck.Columns(1, 22, x => (HorseNeckTop(x) + 1.6f, HorseNeckTop(x) + 4.8f), steelMat);
        for (int x = 3; x < 21; x += 3)
            for (int y = 1; y < 11; y++)
                if (neck.Has(x, y)) { neck.Shade(x, y, -0.32f); if (neck.Has(x + 1, y)) neck.Shade(x + 1, y, 0.20f); }
        neck.Border(edge);
        neck.Soft = true;
        Add($"hneck.{tag}{s}", neck);
        Chanfron(s, tag, steelMat, edge);
    }

    /// The neck (crinet) and face (chanfron) of a half barding.
    void Crinet(string s, string tag, Mat neckMat, Mat edge, bool plates)
    {
        var neck = new Canvas(23, 13);
        neck.Columns(0, 23, x => (HorseNeckTop(x) + 1.6f, HorseNeckTop(x) + 6.5f), neckMat);
        for (int x = 3; x < 22; x += 3)
            for (int y = 0; y < 13; y++)
                if (neck.Has(x, y)) { neck.Shade(x, y, plates ? -0.36f : -0.24f); if (neck.Has(x + 1, y)) neck.Shade(x + 1, y, plates ? 0.24f : 0.14f); }
        neck.Border(edge);
        neck.Soft = true;
        Add($"hneck.{tag}{s}", neck);
        Chanfron(s, tag, plates ? plate : neckMat, edge);
    }

    void Chanfron(string s, string tag, Mat m, Mat edge)
    {
        var head = new Canvas(22, 12);
        head.Columns(4.5f, 20.5f, x => (2.6f + (x - 5) * 0.15f, 9.6f - (x - 5) * 0.16f), m);
        head.Clear(8, 4); head.Clear(9, 4); // eye opening aligned with sculpted warhorse head
        head.Set(7, 4, edge); head.Set(10, 4, edge); head.Set(8, 3, edge); head.Set(9, 3, edge); // flanged eye-guard
        for (int x = 6; x < 20; x++)
        {
            int ridge = (int)(3.4f + (x - 5) * 0.15f);
            if (head.Has(x, ridge)) head.Shade(x, ridge, 0.34f); // gleaming nasal ridge
            if (head.Has(x, ridge + 2)) head.Shade(x, ridge + 2, -0.24f); // cheek bevel
        }
        head.Set(6, 5, edge); head.Set(6, 6, edge); // brow rondel
        for (int y = 4; y <= 8; y++) if (head.Has(5, y)) head.Set(5, y, edge); // poll trim
        Add($"hhead.{tag}{s}", head);
    }
}
