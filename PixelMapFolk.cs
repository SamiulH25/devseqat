using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class PixelMap
{
    /// Parties as pixel sprites, every pixel placed by hand rules at one size: a man is 13 pixels tall, a horse 20 long.
    /// All face right; a party facing left is mirrored whole.
    static class Folk
    {
        static readonly Color[] Steel = Tones(176, 182, 190, 1.0f);
        static readonly Color[] Skin = Tones(222, 172, 132, 0.8f);
        static readonly Color[] Hose = Tones(78, 66, 58);
        static readonly Color[] Wood = Tones(116, 82, 52);
        static readonly Color Boot = new(46, 36, 34);

        /// A man on foot, base row 16: head (0 helm, 1 hood, 2 bare), weapon (0 none, 1 spear, 2 bow, 3 axe), an
        /// optional round shield on the near arm.
        static Pix Man(Color[] cloth, int head, int weapon, Color? shield = null, Color[]? headTones = null, Color[]? skin = null)
        {
            var cv = new Pix(13, 19);
            int B = 17;
            skin ??= Skin;
            var hd = headTones ?? (head == 0 ? Steel : head == 1 ? cloth : Tones(70, 50, 36));
            // Legs and boots.
            cv.VLine(4, B - 3, B - 1, Hose[2]); cv.VLine(6, B - 3, B - 1, Hose[1]);
            cv.Set(4, B, Boot); cv.Set(6, B, Boot); cv.Set(7, B, Boot);
            // Body: lit on the left, a belt, the near arm.
            for (int y = B - 8; y <= B - 4; y++)
                for (int x = 3; x <= 7; x++)
                {
                    int tone = x <= 4 ? 3 : x <= 6 ? 2 : 1;
                    if (y == B - 8 && x <= 5) tone = 4;
                    if (y == B - 4) tone--;
                    cv.Set(x, y, cloth[Math.Max(0, tone)]);
                }
            cv.HLine(3, 7, B - 5, Tones(96, 70, 48)[1]);
            cv.VLine(8, B - 8, B - 6, cloth[1]); cv.Set(8, B - 5, skin[2]);
            // Head.
            cv.Rect(4, B - 10, 6, B - 9, skin[3]); cv.Set(4, B - 10, skin[1]); cv.Set(4, B - 9, skin[1]);
            switch (head)
            {
                case 0: // a kettle hat
                    cv.HLine(4, 6, B - 12, hd[3]); cv.Set(6, B - 12, hd[2]);
                    cv.HLine(3, 7, B - 11, hd[2]); cv.Set(3, B - 11, hd[3]); cv.Set(7, B - 11, hd[1]);
                    cv.Set(4, B - 10, hd[1]);
                    break;
                case 1: // a hood
                    cv.HLine(4, 6, B - 12, hd[3]);
                    cv.HLine(3, 6, B - 11, hd[2]); cv.Set(3, B - 11, hd[3]);
                    cv.VLine(3, B - 10, B - 9, hd[2]); cv.Set(4, B - 10, hd[1]); cv.Set(4, B - 9, hd[1]);
                    break;
                default: // bare: hair
                    cv.HLine(4, 6, B - 11, hd[2]); cv.Set(4, B - 10, hd[1]);
                    break;
            }
            switch (weapon)
            {
                case 1: cv.VLine(10, 1, B - 1, Wood[2]); cv.VLine(10, 1, 2, Steel[4]); cv.Set(10, 3, Steel[2]); break;
                case 2:
                    cv.VLine(10, B - 10, B - 5, Wood[2]); cv.Set(9, B - 11, Wood[2]); cv.Set(9, B - 4, Wood[2]);
                    cv.VLine(8, B - 11, B - 4, new Color(206, 198, 176));
                    break;
                case 3: cv.VLine(9, B - 10, B - 5, Wood[2]); cv.Rect(10, B - 11, 11, B - 9, Steel[3]); cv.Set(11, B - 9, Steel[1]); break;
            }
            if (shield is { } sc)
            {
                var sh = Tones(sc);
                cv.Oval(7.5f, B - 6.5f, 2.6f, 2.6f, (x, y) => Steel[x <= 6 ? 4 : 2]);
                cv.Oval(7.5f, B - 6.5f, 1.6f, 1.6f, (x, y) => sh[x <= 6 ? 3 : 2]);
                cv.Set(7, B - 7, Steel[4]);
            }
            return cv.Outline();
        }

        /// A rider on a horse, base row 30: horse with mane, head carried low, tail; a knight's horse wears a caparison
        /// in the realm's dark cloth with its charge; the rider in his colours under a swallow-tailed banner.
        static Pix Rider(Heraldry side, Color coat, bool knight)
        {
            var cv = new Pix(24, 32);
            int B = 30;
            var c = Tones(coat);
            var mane = Tones(new Color(Vector3.Lerp(coat.ToVector3(), new Vector3(0.12f, 0.09f, 0.08f), 0.7f)));
            var cloth = Tones(side.Cloth);
            var dark = Tones(side.ClothDark);
            // Legs: far ones dark, near ones lit.
            foreach (var (x, near) in new[] { (5, false), (7, true), (13, false), (15, true) })
            {
                cv.VLine(x, B - 3, B - 1, near ? c[2] : c[0]);
                cv.Set(x, B, Boot);
            }
            // Body.
            for (int y = B - 8; y <= B - 4; y++)
                for (int x = 5; x <= 15; x++)
                {
                    int tone = y == B - 8 ? 3 : y == B - 4 ? 1 : x < 9 ? 3 : 2;
                    cv.Set(x, y, c[tone]);
                }
            cv.Clear(5, B - 8); cv.Clear(5, B - 4);
            // Neck and head.
            foreach (var (y, a, b) in new[] { (B - 9, 14, 16), (B - 10, 15, 17), (B - 11, 15, 17), (B - 12, 16, 18) })
                for (int x = a; x <= b; x++) cv.Set(x, y, x == a ? mane[2] : c[x == a + 1 ? 3 : 2]);
            cv.HLine(16, 18, B - 13, c[3]); cv.Set(16, B - 14, c[2]); // the ear
            cv.HLine(17, 20, B - 12, c[2]); cv.HLine(18, 20, B - 11, c[2]); cv.Set(20, B - 11, c[0]);
            cv.Set(18, B - 12, Boot); // the eye
            cv.Set(17, B - 13, mane[1]);
            // The tail.
            cv.VLine(4, B - 8, B - 5, mane[2]); cv.VLine(3, B - 6, B - 3, mane[1]);
            if (knight)
            {
                for (int y = B - 8; y <= B - 3; y++)
                    for (int x = 5; x <= 13; x++) cv.Set(x, y, y == B - 3 ? side.Trim : dark[x < 8 ? 3 : 2]);
                Charge(cv, 7, B - 7, 11, B - 5, side.Charge, side.Trim);
            }
            else cv.Rect(8, B - 8, 11, B - 6, cloth[2]);
            // The rider.
            cv.VLine(10, B - 7, B - 5, knight ? Steel[2] : Hose[2]); cv.Set(10, B - 4, Boot); cv.Set(11, B - 4, Boot);
            for (int y = B - 13; y <= B - 8; y++)
                for (int x = 8; x <= 11; x++) cv.Set(x, y, cloth[x <= 9 ? 3 : x == 10 ? 2 : 1]);
            if (knight) cv.VLine(9, B - 12, B - 8, side.Trim);
            cv.Rect(12, B - 12, 12, B - 10, cloth[1]); cv.Set(13, B - 10, Skin[2]); // the arm on the reins
            cv.Rect(9, B - 16, 11, B - 14, Steel[3]); cv.Set(11, B - 16, Steel[2]); cv.HLine(9, 11, B - 17, Steel[4]);
            if (knight) { cv.HLine(10, 11, B - 15, Boot); cv.HLine(9, 10, B - 18, side.Trim); } // the visor, the crest
            else { cv.Set(11, B - 15, Skin[3]); cv.Set(10, B - 15, Skin[2]); }
            // The banner.
            cv.VLine(7, 1, B - 9, Wood[1]); cv.Set(7, 0, Steel[4]);
            for (int y = 2; y <= 8; y++)
                for (int x = 8; x <= 17; x++)
                {
                    if (x >= 16 && y >= 4 && y <= 6) continue; // the swallow tail
                    cv.Set(x, y, cloth[y == 2 ? 3 : x >= 15 ? 1 : 2]);
                }
            Charge(cv, 9, 3, 15, 7, side.Charge, side.Trim);
            return cv.Outline();
        }

        /// A beast on four legs facing right, base row 13: wolf, bear or ox.
        static Pix Beast(string kind, Color coat)
        {
            var c = Tones(coat);
            var cv = new Pix(19, 15);
            int B = 13;
            switch (kind)
            {
                case "bear":
                    for (int y = B - 8; y <= B - 2; y++) for (int x = 2; x <= 13; x++) cv.Set(x, y, c[y <= B - 7 ? 3 : y >= B - 3 ? 1 : x < 6 ? 3 : 2]);
                    cv.Clear(2, B - 8); cv.Clear(13, B - 8);
                    cv.Rect(13, B - 7, 16, B - 4, c[2]); cv.Set(16, B - 4, c[0]); cv.Set(13, B - 8, c[3]); cv.Set(15, B - 6, Boot);
                    foreach (var x in new[] { 3, 5, 10, 12 }) { cv.Rect(x, B - 1, x + 1, B, x is 5 or 12 ? c[2] : c[0]); }
                    break;
                case "ox":
                    for (int y = B - 7; y <= B - 3; y++) for (int x = 2; x <= 12; x++) cv.Set(x, y, c[y == B - 7 ? 3 : y == B - 3 ? 1 : 2]);
                    cv.Rect(13, B - 6, 15, B - 3, c[2]); cv.Set(15, B - 3, c[0]); cv.Set(14, B - 5, Boot);
                    cv.Set(13, B - 7, new Color(226, 218, 196)); cv.Set(15, B - 7, new Color(226, 218, 196)); // the horns
                    foreach (var x in new[] { 3, 5, 10, 12 }) cv.VLine(x, B - 2, B, x is 5 or 12 ? c[2] : c[0]);
                    cv.VLine(1, B - 7, B - 4, c[1]);
                    break;
                default: // a wolf
                    for (int y = B - 5; y <= B - 3; y++) for (int x = 3; x <= 11; x++) cv.Set(x, y, c[y == B - 5 ? 3 : y == B - 3 ? 1 : 2]);
                    cv.Rect(11, B - 7, 13, B - 5, c[3]); cv.HLine(13, 15, B - 6, c[2]); cv.Set(15, B - 6, c[0]);
                    cv.Set(11, B - 8, c[2]); cv.Set(12, B - 7, Boot);
                    cv.HLine(1, 2, B - 6, c[2]); cv.Set(0, B - 7, c[3]);
                    foreach (var x in new[] { 4, 5, 10, 11 }) cv.VLine(x, B - 2, B, x is 5 or 11 ? c[2] : c[0]);
                    break;
            }
            return cv.Outline();
        }

        static Pix Spider()
        {
            var c = Tones(52, 42, 48);
            var cv = new Pix(19, 12);
            int B = 10;
            for (int i = 0; i < 4; i++)
            {
                int lx = 3 + i * 4;
                cv.Set(lx, B - 4, c[2]); cv.Set(lx - 1, B - 3, c[2]); cv.VLine(lx - 2, B - 2, B, c[1]);
                cv.Set(lx + 1, B - 4, c[1]); cv.Set(lx + 2, B - 3, c[1]); cv.VLine(lx + 3, B - 2, B, c[0]);
            }
            cv.Oval(7, B - 5, 4.5f, 3, (x, y) => c[x < 6 ? 3 : 2]);
            cv.Oval(12.5f, B - 4.5f, 2.5f, 2, c[2]);
            cv.Set(13, B - 5, new Color(200, 60, 50)); cv.Set(14, B - 5, new Color(200, 60, 50));
            return cv.Outline();
        }

        static Pix Wyvern()
        {
            var c = Tones(84, 104, 80);
            var wing = Tones(110, 92, 70);
            var cv = new Pix(26, 22);
            int B = 20;
            for (int i = 0; i < 10; i++) cv.HLine(6 + i, 12 + i / 2, B - 10 - i, wing[i % 3 == 0 ? 3 : 2]); // the wing raised
            for (int y = B - 6; y <= B - 3; y++) for (int x = 5; x <= 15; x++) cv.Set(x, y, c[y == B - 6 ? 3 : y == B - 3 ? 1 : 2]);
            cv.Rect(15, B - 10, 17, B - 6, c[2]); cv.HLine(17, 21, B - 10, c[3]); cv.HLine(17, 20, B - 9, c[2]); cv.Set(18, B - 10, Boot);
            cv.HLine(1, 5, B - 5, c[1]); cv.Set(0, B - 6, c[2]);
            cv.VLine(7, B - 2, B, c[1]); cv.VLine(13, B - 2, B, c[2]);
            return cv.Outline();
        }

        static Pix Wagon(Color cover)
        {
            var cl = Tones(cover, 0.9f);
            var cv = new Pix(19, 17);
            int B = 15;
            for (int y = B - 11; y <= B - 6; y++)
                for (int x = 2; x <= 15; x++)
                {
                    int dy = y - (B - 11);
                    if (dy == 0 && (x < 4 || x > 13)) continue;
                    cv.Set(x, y, cl[x < 6 ? 3 : (x - 2) % 4 == 3 ? 1 : 2]);
                }
            cv.Rect(1, B - 5, 16, B - 3, Wood[2]); cv.HLine(1, 16, B - 3, Wood[1]);
            foreach (var wx in new[] { 4, 13 }) { cv.Rect(wx - 1, B - 2, wx + 1, B, Wood[1]); cv.Set(wx, B - 1, Wood[3]); }
            return cv.Outline();
        }

        /// A party as one sprite, base at its foot: the company or a lord under his banner with men behind; brigands a
        /// hooded band; a caravan its wagon, ox and guard; monsters as what they are.
        public static Sprite Party(Party p, Campaign c, bool left)
        {
            int pal = p.Kind == PartyKind.Player ? 2 : Style.PaletteOf(c.SideOf(p));
            int men = Math.Clamp(p.Companies.Count, 1, 3);
            string breed = p.Kind == PartyKind.Monster ? p.Breed : p.Kind == PartyKind.Caravan ? p.Breed ?? "" : "";
            bool knight = p.Kind == PartyKind.Player || p.Tier >= 3;
            return Cached($"party{p.Kind}.{breed}.{pal}.{left}.{men}.{knight}.{p.Id % Style.Coats.Length}", () =>
            {
                var side = Style.Sides[pal];
                var cloth = Tones(side.Cloth);
                var parts = new List<(Pix, int, int)>(); // canvas, left x, base y offset (0 = the front)
                switch (p.Kind)
                {
                    case PartyKind.Bandit:
                        parts.Add((Man(Tones(104, 96, 72), 1, 2), 0, -2));
                        parts.Add((Man(Tones(118, 98, 72), 1, 1), 7, -1));
                        parts.Add((Man(Tones(96, 88, 70), 1, 3), 13, 0));
                        break;
                    case PartyKind.Caravan when breed == "pilgrims":
                        parts.Add((Man(Tones(150, 140, 120), 1, 1), 0, -1));
                        parts.Add((Man(Tones(178, 168, 148), 1, 0), 7, 0));
                        parts.Add((Man(Tones(130, 120, 104), 1, 1), 13, -1));
                        break;
                    case PartyKind.Caravan:
                        parts.Add((Man(cloth, 0, 1), 0, -1));
                        parts.Add((Wagon(new Color(226, 212, 176)), 9, 0));
                        parts.Add((Beast("ox", new Color(140, 104, 70)), 26, 0));
                        break;
                    case PartyKind.Monster:
                        switch (breed)
                        {
                            case "goblin":
                            {
                                var g = Tones(108, 142, 70);
                                for (int i = 0; i < 3; i++) parts.Add((Man(Tones(90, 76, 56), 2, i == 1 ? 3 : 1, null, g, g), i * 7, i == 1 ? 0 : -1));
                                break;
                            }
                            case "undead":
                            {
                                var bone = Tones(214, 208, 190);
                                for (int i = 0; i < 3; i++) parts.Add((Man(Tones(120, 116, 104), 2, i == 1 ? 3 : 1, null, bone, bone), i * 7, i == 1 ? 0 : -1));
                                break;
                            }
                            case "troll": parts.Add((Grown(Man(Tones(110, 116, 96), 2, 3, null, Tones(120, 128, 104), Tones(120, 128, 104)), 2), 0, 0)); break;
                            case "bear": parts.Add((Beast("bear", new Color(104, 74, 50)), 0, 0)); break;
                            case "spider": parts.Add((Spider(), 0, 0)); parts.Add((Spider(), 14, -3)); break;
                            case "wyvern": parts.Add((Wyvern(), 0, 0)); break;
                            default:
                                parts.Add((Beast("wolf", new Color(120, 116, 110)), 0, -3));
                                parts.Add((Beast("wolf", new Color(100, 96, 92)), 8, 0));
                                parts.Add((Beast("wolf", new Color(138, 132, 124)), 16, -2));
                                break;
                        }
                        break;
                    default:
                    {
                        for (int i = 0; i < men; i++)
                            parts.Add((Man(i == 1 ? Tones(side.ClothDark) : cloth, 0, i == 1 ? 2 : 1, i == 1 ? null : side.ClothDark), (men - 1 - i) * 7, -1 + (i & 1)));
                        parts.Add((Rider(side, new Color(Style.Coats[p.Id % Style.Coats.Length]), knight), men * 7 - 4, 0));
                        break;
                    }
                }
                // Lay the parts on one canvas, the far (higher) ones first.
                // Every part's base row is two above its canvas's bottom (the outline under the feet, one spare).
                int w = parts.Max(q => q.Item2 + q.Item1.W), baseY = parts.Max(q => q.Item1.H - 2 - q.Item3);
                var cv = new Pix(w, baseY + 2);
                foreach (var (pc, x, dy) in parts.OrderBy(q => q.Item3))
                    cv.Put(pc, x, baseY - (pc.H - 2) + dy);
                var px = cv.P;
                if (left)
                {
                    var flip = new Color[px.Length];
                    for (int y = 0; y < cv.H; y++) for (int x = 0; x < cv.W; x++) flip[y * cv.W + x] = px[y * cv.W + cv.W - 1 - x];
                    px = flip;
                }
                return new Sprite(px, cv.W, cv.H, cv.W / 2, cv.H - 2);
            });
        }

        /// A canvas grown by whole pixels (a troll is a man twice over).
        static Pix Grown(Pix s, int n)
        {
            var g = new Pix(s.W * n, s.H * n);
            for (int y = 0; y < g.H; y++) for (int x = 0; x < g.W; x++) g.P[y * g.W + x] = s.P[y / n * s.W + x / n];
            return g;
        }
    }
}
