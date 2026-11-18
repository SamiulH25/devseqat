using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class PixelMap
{
    static partial class Art
    {
        // ---------- parties: little scenes made of figures, each painted on its own (so each keeps its own outline) ----------

        static readonly Mat Wood = new(new Color(96, 70, 46), Pattern.Wood);
        static readonly Mat Steel = new(new Color(150, 156, 168), Pattern.Plate);
        static readonly Mat Skin = new(new Color(222, 172, 132));
        static readonly Mat Leather = new(new Color(92, 66, 46));
        static readonly Mat Hood = new(new Color(104, 88, 66));

        static Sprite Piece(Canvas cv, float footX, float footY) => new(cv.Render(), cv.W + 2, cv.H + 2, (int)footX + 1, (int)footY + 1);

        /// A man on foot facing right: legs, a tunic widening to the hem, a belt, a head (helm, hood or bare), and his weapon:
        /// 0 none, 1 spear, 2 bow, 3 axe or club.
        static Sprite Man(float u, Mat body, Mat head, Mat legs, int weapon, Mat? shield = null)
        {
            var cv = new Canvas((int)(7.5f * u) + 1, (int)(14.5f * u) + 1);
            float x = 3.2f * u, fy = cv.H - 0.01f;
            cv.Rect(x - 1.1f * u, fy - 3.2f * u, x - 0.2f * u, fy, legs, -0.2f);
            cv.Rect(x + 0.2f * u, fy - 3.2f * u, x + 1.1f * u, fy, legs);
            cv.Body(fy - 7.8f * u, fy - 2.6f * u, y => { float a = (y - (fy - 7.8f * u)) / (5.2f * u); return (x - (1.6f + 0.4f * a) * u, x + (1.6f + 0.4f * a) * u); }, body);
            cv.Rect(x + 0.9f * u, fy - 7.2f * u, x + 1.9f * u, fy - 4.2f * u, body, -0.25f); // the near arm
            for (float xx = x - 1.9f * u; xx <= x + 1.9f * u; xx++) cv.Shade((int)xx, (int)(fy - 4.6f * u), -0.45f); // the belt
            cv.Ellipse(x, fy - 9.2f * u, 1.45f * u, 1.45f * u, head, 0.1f);
            if (head != Skin) cv.Rect(x + 0.2f * u, fy - 9.0f * u, x + 1.4f * u, fy - 8.0f * u, Skin, 0.1f); // the face below the brim
            if (shield != null) // a round shield on the near arm, its boss catching the light
            {
                float sx = x + 0.9f * u, sy = fy - 5.4f * u;
                cv.Ellipse(sx, sy, 2.0f * u, 2.0f * u, Steel, 0.2f);
                cv.Ellipse(sx, sy, 2.0f * u - 1, 2.0f * u - 1, shield, 0.05f);
                cv.Rect(sx - 0.5f * u, sy - 0.5f * u, sx + 0.5f * u, sy + 0.5f * u, Steel, 0.7f); // the boss
            }
            switch (weapon)
            {
                case 1:
                    cv.Line(x + 2.3f * u, fy - 14f * u, x + 2.3f * u, fy - 0.6f * u, Wood, 0.45f, MathF.Max(1, 0.6f * u));
                    cv.Rect(x + 2.3f * u - 0.4f, fy - 14.4f * u, x + 2.3f * u + 0.4f, fy - 12.8f * u, Steel, 0.3f);
                    break;
                case 2:
                    for (float a = -1; a <= 1; a += 0.12f) cv.Set((int)(x + 2.2f * u + (1 - a * a) * 1.3f * u), (int)(fy - 7.6f * u + a * 3.6f * u), Wood, 0.5f);
                    cv.Line(x + 2.2f * u, fy - 11.2f * u, x + 2.2f * u, fy - 4f * u, new Mat(new Color(210, 200, 180)), 0.5f);
                    break;
                case 3:
                    cv.Line(x + 1.4f * u, fy - 10.8f * u, x + 2.4f * u, fy - 4.4f * u, Wood, 0.45f, MathF.Max(1, 0.6f * u));
                    cv.Rect(x + 1.0f * u, fy - 11.4f * u, x + 2.2f * u, fy - 9.8f * u, Steel, 0.3f);
                    break;
            }
            return Piece(cv, x, fy);
        }

        /// A beast on four legs facing right (horse, ox, wolf, bear): a barrel, a neck and head, legs, a tail.
        static Canvas Beast(float u, Mat coat, float len, float tall, float neck, out float bx, out float by)
        {
            var cv = new Canvas((int)((len + 6.5f) * u) + 1, (int)((tall + neck + 3) * u) + 1);
            bx = (len / 2 + 1.2f) * u; by = cv.H - 0.01f;
            float b0 = bx - len / 2 * u, b1 = bx + len / 2 * u, top = by - tall * u, legTop = by - tall * 0.55f * u;
            float lw = MathF.Max(1, 0.75f * u);
            float x0 = b0, x1 = b1, byy = by;
            foreach (var (lx, far) in new[] { (b0 + 0.5f * u, false), (b0 + 1.7f * u, true), (b1 - 2.0f * u, true), (b1 - 0.8f * u, false) })
            {
                cv.Rect(lx, legTop, lx + lw, byy, coat, far ? -0.3f : 0);
                cv.Shade((int)lx, (int)byy, -0.5f);
            }
            cv.Columns(x0, x1, x => { float e = MathF.Min(x - x0, x1 - x); float r = MathF.Max(0, 1.2f * u - e) * 0.8f; return (top + r, legTop + 0.6f * u - r * 0.5f); }, coat);
            NeckHead(cv, b1, top, u, neck, coat, null);
            return cv;
        }

        /// A neck rising forward from the chest and a long head sloping down to the muzzle, an ear, a mane along the
        /// neck's crest: the parts that make four legs and a barrel read as a horse (or an ox, a wolf) at a few pixels.
        static void NeckHead(Canvas cv, float b1, float top, float u, float neck, Mat coat, Mat? mane)
        {
            float y0 = top - neck * u, y1 = top + 2.2f * u;
            Func<float, float> mid = y => { float a = (y - y0) / (y1 - y0); return b1 + (1.6f - 2.2f * a) * u; };
            cv.Body(y0, y1, y => { float a = (y - y0) / (y1 - y0); float r = (1.2f + 0.6f * a) * u; return (mid(y) - r, mid(y) + r); }, coat, 0.1f);
            if (mane != null) for (float y = y0 - 0.6f * u; y < y1 - u; y++) { cv.Set((int)(mid(y) - 1.1f * u), (int)y, mane, 0.3f); cv.Set((int)(mid(y) - 1.1f * u) + 1, (int)y, mane, 0.5f); } // the mane, two pixels
            float h0 = b1 + 0.8f * u, h1 = b1 + 5.0f * u;
            cv.Columns(h0, h1, x => { float a = (x - h0) / (h1 - h0); return (y0 - 0.9f * u + a * 2.6f * u, y0 + 1.2f * u + a * 1.8f * u); }, coat, 0.15f);
            cv.Shade((int)(h1 - 0.6f * u), (int)(y0 + 2.2f * u), -0.6f); // the nostril
            for (float x = h0 + 0.5f * u; x < h1; x++) { float a = (x - h0) / (h1 - h0); cv.Shade((int)x, (int)(y0 + 1.2f * u + a * 1.8f * u), -0.55f); } // the jaw's shadow
            cv.Shade((int)(h0 + 1.6f * u), (int)(y0 + 0.1f * u), -0.8f); // the eye
            cv.Rect(b1 + 1.1f * u, y0 - 1.8f * u, b1 + 1.9f * u, y0 - 0.6f * u, coat, 0.2f); // the ear
        }

        /// A rider facing right on a horse; a knight's horse wears a caparison with his charge, and his banner flies above.
        static Sprite Rider(float u, Heraldry side, Color coat, bool knight, bool helm, bool banner)
        {
            var cv = PaintRider(u, side, new Mat(coat), knight, helm, banner, (int)((banner ? 11 : 6) * u));
            return Piece(cv, (8.6f / 2 + 1.2f) * u, cv.H - 0.01f);
        }

        static Canvas PaintRider(float u, Heraldry side, Mat coat, bool knight, bool helm, bool banner, int lift)
        {
            float len = 8.6f, tallH = 7.2f, neck = 2.6f;
            var cv = new Canvas((int)((len + 6.5f) * u) + 1, (int)((tallH + neck + 3) * u) + 1 + lift);
            float bx = (len / 2 + 1.2f) * u, by = cv.H - 0.01f;
            float b0 = bx - len / 2 * u, b1 = bx + len / 2 * u, top = by - tallH * u, legTop = by - tallH * 0.45f * u;
            float lw = MathF.Max(1, 0.95f * u);
            var cloth = new Mat(side.Cloth, Pattern.Cloth);
            var trim = new Mat(side.Trim);
            var mane = new Mat(new Color(Vector3.Lerp(coat.Ramp[2].ToVector3(), new Vector3(0.1f, 0.08f, 0.08f), 0.78f)));
            foreach (var (lx, far) in new[] { (b0 + 0.5f * u, false), (b0 + 1.7f * u, true), (b1 - 2.0f * u, true), (b1 - 0.8f * u, false) })
            {
                cv.Rect(lx, legTop, lx + lw, by, coat, far ? -0.3f : 0);
                cv.Shade((int)lx, (int)by, -0.5f);
            }
            cv.Line(b0 + 0.2f * u, top + 0.6f * u, b0 - 1.0f * u, legTop + 1.5f * u, mane, 0.4f, MathF.Max(1, 1.1f * u)); // the tail
            cv.Columns(b0, b1, x => { float e = MathF.Min(x - b0, b1 - x); float r = MathF.Max(0, 1.2f * u - e) * 0.8f; return (top + r, legTop + 0.6f * u - r * 0.5f); }, coat);
            NeckHead(cv, b1, top, u, neck, coat, mane);
            if (knight) // the caparison to the knees, a pale hem, the charge on the flank
            {
                cv.Body(top - 0.2f * u, legTop + 0.9f * u, _ => (b0 - 0.2f * u, b1 - 2.2f * u), new Mat(side.ClothDark, Pattern.Cloth), 0.05f);
                cv.Rect(b0 - 0.2f * u, legTop + 0.4f * u, b1 - 2.2f * u, legTop + 0.9f * u, trim);
            }
            else
            {
                cv.Rect(bx - 2.2f * u, top - 0.2f * u, bx + 1.2f * u, top + 2.0f * u, cloth);
                cv.Rect(bx - 2.2f * u, top + 1.5f * u, bx + 1.2f * u, top + 2.0f * u, trim);
            }
            // The rider: leg, tabard, arm, head.
            float rx = bx - 0.6f * u, seat = top + 0.6f * u;
            cv.Rect(rx - 0.2f * u, seat - 0.6f * u, rx + 0.9f * u, seat + 3.0f * u, knight ? Steel : Leather);
            cv.Body(seat - 5.4f * u, seat + 0.2f * u, _ => (rx - 1.4f * u, rx + 1.5f * u), cloth, 0.05f);
            for (float y = seat - 5.4f * u; y <= seat + 0.2f * u; y++) cv.Set((int)rx, (int)y, trim, 0.45f); // the tabard's pale
            cv.Rect(rx + 1.0f * u, seat - 4.8f * u, rx + 1.9f * u, seat - 1.6f * u, cloth, -0.2f);
            cv.Ellipse(rx + 0.1f * u, seat - 6.8f * u, 1.4f * u, 1.45f * u, helm ? Steel : Skin, 0.15f);
            if (knight)
            {
                cv.Rect(rx - 0.3f * u, seat - 9.0f * u, rx + 0.6f * u, seat - 8.1f * u, trim, 0.2f); // the crest
                float sx0 = rx - 0.2f * u, sx1 = rx + 2.6f * u, sy0 = seat - 3.6f * u, sy1 = seat + 0.8f * u;
                cv.Body(sy0, sy1, y => { float a = (y - sy0) / (sy1 - sy0); float o = a > 0.55f ? (a - 0.55f) / 0.45f * (sx1 - sx0) * 0.5f : 0; return (sx0 + o, sx1 - o); }, cloth, 0.1f);
                Charge(cv, sx0 + 0.4f * u, sy0 + 0.4f * u, sx1 - 0.4f * u, sy1 - 1.2f * u, side.Charge, trim);
            }
            if (banner)
            {
                float px = rx - 1.9f * u, ftop = 0.5f, fw = 7.5f * u, fh = 5.2f * u;
                cv.Line(px, ftop, px, seat - 1.0f * u, Wood, 0.45f, MathF.Max(1, 0.7f * u));
                cv.Rect(px + 1, ftop, px + 1 + fw, ftop + fh, cloth, 0.1f);
                Charge(cv, px + 1 + 0.6f * u, ftop + 0.6f * u, px + fw - 0.4f * u, ftop + fh - 0.6f * u, side.Charge, trim);
                if (knight) // a swallow tail
                    for (float y = ftop + fh * 0.35f; y <= ftop + fh * 0.65f; y++)
                        for (float x = px + 1 + fw - 1.6f * u; x <= px + 1 + fw; x++) cv.Clear((int)x, (int)y);
            }
            return cv;
        }

        /// A charge painted in a field: the heraldry of the side's shield on its banners and caparisons.
        static void Charge(Canvas cv, float x0, float y0, float x1, float y1, Emblem e, Mat m)
        {
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, w = x1 - x0, h = y1 - y0;
            switch (e)
            {
                case Emblem.Chevron: cv.Line(x0, y1 - 1, cx, y0 + h * 0.2f, m, 0.5f); cv.Line(cx, y0 + h * 0.2f, x1, y1 - 1, m, 0.5f); break;
                case Emblem.Saltire: cv.Line(x0, y0, x1, y1, m, 0.5f); cv.Line(x1, y0, x0, y1, m, 0.5f); break;
                case Emblem.Bend: cv.Line(x0, y0, x1, y1, m, 0.5f, 1.6f); break;
                case Emblem.Cross: cv.Rect(cx - 0.5f, y0, cx + 0.5f, y1, m, 0.1f); cv.Rect(x0, cy - 0.5f, x1, cy + 0.5f, m, 0.1f); break;
                case Emblem.Pale: cv.Rect(cx - w * 0.17f, y0 - 0.6f, cx + w * 0.17f, y1 + 0.6f, m, 0.1f); break;
                case Emblem.Fess: cv.Rect(x0 - 0.6f, cy - h * 0.2f, x1 + 0.6f, cy + h * 0.2f, m, 0.1f); break;
                case Emblem.Lozenge: cv.Area((x, y) => MathF.Abs(x - cx) / (w * 0.32f) + MathF.Abs(y - cy) / (h * 0.45f) <= 1, m); break;
                case Emblem.Sun: cv.Area((x, y) => (x - cx) * (x - cx) / (w * w * 0.06f) + (y - cy) * (y - cy) / (h * h * 0.1f) <= 1, m); break;
            }
        }

        static Sprite Wagon(float u, Mat cover)
        {
            var cv = new Canvas((int)(11 * u) + 1, (int)(9 * u) + 1);
            float fy = cv.H - 0.01f;
            cv.Rect(0.5f * u, fy - 4.4f * u, 10 * u, fy - 1.8f * u, Wood);
            cv.Body(fy - 8.6f * u, fy - 4.2f * u, y => { float a = (y - (fy - 8.6f * u)) / (4.4f * u); float o = (1 - a) * (1 - a) * 1.6f * u; return (1.2f * u + o, 9.4f * u - o); }, cover, 0.1f);
            for (float x = 3 * u; x < 9 * u; x += 2.6f * u) for (float y = fy - 8.2f * u; y < fy - 4.4f * u; y++) cv.Shade((int)x, (int)y, -0.35f); // the hoops
            cv.Ellipse(2.6f * u, fy - 1.6f * u, 1.6f * u, 1.6f * u, Leather);
            cv.Ellipse(8.2f * u, fy - 1.6f * u, 1.6f * u, 1.6f * u, Leather);
            return Piece(cv, 5 * u, fy);
        }

        static Sprite Quadruped(float u, Color c, float len, float tall, float neck)
        {
            var cv = Beast(u, new Mat(c), len, tall, neck, out float bx, out float by);
            return Piece(cv, bx, by);
        }

        /// Figures in a group: each placed with its foot at an offset from the group's foot; the nearer (lower) drawn last.
        static Sprite Group(params (Sprite S, float X, float Y)[] parts)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (s, x, y) in parts)
            {
                int l = (int)MathF.Round(x) - s.FootX, tp = (int)MathF.Round(y) - s.FootY;
                minX = Math.Min(minX, l); minY = Math.Min(minY, tp); maxX = Math.Max(maxX, l + s.W); maxY = Math.Max(maxY, tp + s.H);
            }
            int w = maxX - minX, h = maxY - minY;
            var px = new Color[w * h];
            foreach (var (s, x, y) in parts.OrderBy(p => p.Y))
            {
                int l = (int)MathF.Round(x) - s.FootX - minX, tp = (int)MathF.Round(y) - s.FootY - minY;
                for (int yy = 0; yy < s.H; yy++)
                    for (int xx = 0; xx < s.W; xx++)
                        if (s.Px[yy * s.W + xx].A != 0) px[(tp + yy) * w + l + xx] = s.Px[yy * s.W + xx];
            }
            return new Sprite(px, w, h, -minX, -minY);
        }

        /// A party on the map as a little scene: a lord or the company is a rider under his banner (a knight in a
        /// caparison for the great lords and you) with men marching behind him, more for a bigger host; brigands a
        /// hooded band; merchants a wagon, its ox and a guard; monsters as what they are.
        public static Sprite Party(Party p, Campaign c, float k, bool? faceLeft = null)
        {
            int pal = p.Kind == PartyKind.Player ? 2 : Style.PaletteOf(c.SideOf(p));
            bool left = faceLeft ?? MathF.Cos(p.Facing) < 0;
            int men = Math.Clamp(p.Companies.Count, 1, 3);
            string breed = p.Kind == PartyKind.Monster ? p.Breed : "";
            return Cached($"party{p.Kind}.{breed}.{pal}.{k}.{left}.{men}.{Math.Min(p.Tier, 3)}.{p.Id % Style.Coats.Length}", () =>
            {
                float u = k / 3 * 2.2f;
                var side = Style.Sides[pal];
                var cloth = new Mat(side.Cloth, Pattern.Cloth);
                var dark = new Mat(side.ClothDark, Pattern.Cloth);
                var hose = new Mat(new Color(70, 60, 52));
                Sprite s;
                switch (p.Kind)
                {
                    case PartyKind.Bandit:
                    {
                        var rags = new Mat(new Color(110, 96, 74), Pattern.Cloth);
                        var rags2 = new Mat(new Color(88, 96, 72), Pattern.Cloth);
                        s = Group((Man(u, rags2, Hood, hose, 2), -5 * u, -3 * u), (Man(u, rags, Hood, hose, 1), 4 * u, -4 * u), (Man(u, rags, Hood, hose, 3), 0, 0));
                        break;
                    }
                    case PartyKind.Caravan:
                    {
                        var ox = Quadruped(u, new Color(140, 104, 70), 7.5f, 6.2f, 1.4f);
                        var cover = new Mat(p.Breed == "pilgrims" ? new Color(200, 196, 186) : new Color(226, 212, 176), Pattern.Cloth);
                        s = p.Breed == "pilgrims"
                            ? Group((Man(u, new Mat(new Color(150, 140, 120), Pattern.Cloth), Hood, hose, 1), -5 * u, -3 * u), (Man(u, new Mat(new Color(176, 166, 146), Pattern.Cloth), Hood, hose, 0), 0, 0), (Man(u, new Mat(new Color(130, 120, 104), Pattern.Cloth), Hood, hose, 1), 5 * u, -2 * u))
                            : Group((Wagon(u, cover), -4 * u, 0), (ox, 6.5f * u, 0), (Man(u, cloth, Steel, hose, 1), -1 * u, -5 * u));
                        break;
                    }
                    case PartyKind.Monster:
                        s = breed switch
                        {
                            "goblin" => Group((Man(u * 0.75f, new Mat(new Color(92, 78, 56)), new Mat(new Color(106, 140, 70)), hose, 1), -4 * u, -3 * u),
                                              (Man(u * 0.75f, new Mat(new Color(80, 70, 52)), new Mat(new Color(106, 140, 70)), hose, 3), 0, 0),
                                              (Man(u * 0.75f, new Mat(new Color(92, 78, 56)), new Mat(new Color(96, 132, 64)), hose, 2), 4 * u, -2 * u)),
                            "bear" => Quadruped(u * 1.2f, new Color(104, 74, 50), 9, 6.5f, 0.6f),
                            "troll" => Man(u * 1.7f, new Mat(new Color(110, 116, 96)), new Mat(new Color(120, 128, 104)), new Mat(new Color(96, 102, 84)), 3),
                            "spider" => Quadruped(u, new Color(48, 40, 44), 6, 3.5f, 0.2f),
                            "wyvern" => Quadruped(u * 1.2f, new Color(86, 104, 82), 8, 5, 4),
                            "undead" => Group((Man(u, new Mat(new Color(160, 156, 140)), new Mat(new Color(214, 208, 190)), hose, 1), -4 * u, -3 * u), (Man(u, new Mat(new Color(120, 116, 104)), new Mat(new Color(214, 208, 190)), hose, 3), 0, 0)),
                            _ => Group((Quadruped(u * 0.8f, new Color(120, 116, 110), 7, 5, 1.2f), -5 * u, -3 * u), (Quadruped(u * 0.8f, new Color(100, 96, 92), 7, 5, 1.2f), 0, 0), (Quadruped(u * 0.8f, new Color(134, 128, 120), 7, 5, 1.2f), 4 * u, -4 * u)),
                        };
                        break;
                    default:
                    {
                        bool knight = p.Kind == PartyKind.Player || p.Tier >= 3;
                        var lead = Rider(u, side, new Color(Style.Coats[p.Id % Style.Coats.Length]), knight, true, true);
                        var parts = new List<(Sprite, float, float)> { (lead, 0, 0) };
                        var foot = new[] { (-10f, -1.6f, 1), (-16.5f, -2.2f, 2), (-23f, -2.8f, 1) }; // a file behind the leader
                        for (int i = 0; i < men; i++)
                            parts.Add((Man(u, i == 1 ? dark : cloth, Steel, hose, foot[i].Item3, foot[i].Item3 == 1 ? new Mat(side.ClothDark) : null), foot[i].Item1 * u, foot[i].Item2 * u));
                        s = Group(parts.ToArray());
                        break;
                    }
                }
                if (!left) return s;
                var flip = new Color[s.Px.Length];
                for (int y = 0; y < s.H; y++) for (int x = 0; x < s.W; x++) flip[y * s.W + x] = s.Px[y * s.W + s.W - 1 - x];
                return s with { Px = flip, FootX = s.W - 1 - s.FootX };
            });
        }
    }
}
