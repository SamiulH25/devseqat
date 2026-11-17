using Microsoft.Xna.Framework;

namespace Ironfield;

/// The rest of a bust: the house's arms behind it, the dress by rank, hair, beard and a ruler's crown.
sealed partial class Bust
{
    // ---------- the arms behind ----------

    bool Charged(int x, int y)
    {
        float u = (x + 0.5f - Cx) / (W / 2f), v = (y + 0.5f - H / 2f) / (H / 2f);
        bool on = f.Charge switch
        {
            Emblem.Chevron => MathF.Abs(y - (80 - MathF.Abs(x + 0.5f - Cx) * 0.9f)) < 6,
            Emblem.Saltire => MathF.Abs(MathF.Abs(u) - MathF.Abs(v)) < 0.12f,
            Emblem.Lozenge => MathF.Abs(u) + MathF.Abs(v) < 0.55f,
            Emblem.Bend => MathF.Abs(u - v) < 0.18f,
            Emblem.Cross => MathF.Abs(u) < 0.12f || MathF.Abs(v) < 0.1f,
            Emblem.Pale => MathF.Abs(u) < 0.22f,
            Emblem.Fess => MathF.Abs(v + 0.1f) < 0.16f,
            Emblem.Sun => MathF.Abs(MathF.Sqrt(u * u + v * v) - 0.5f) < 0.08f,
            _ => false,
        };
        if (f.Field >= 2 && x < W / 2) on = !on && f.Field == 3; // a parted field
        return on;
    }

    /// The house's arms as a hung cloth: its realm's colour with a woven diaper, its own charge with a dark edge,
    /// lit from the upper left and falling away to the corners in steps.
    void Background()
    {
        Color field = f.Field % 2 == 0 ? side.ClothDark : Mul(side.Cloth, 0.7f), charge = Color.Lerp(side.Trim, field, 0.5f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f - Cx) / (W / 2f), v = (y + 0.5f - H / 2f) / (H / 2f);
                bool on = Charged(x, y);
                Color c = on ? charge : field;
                int d1 = (x + y) % 12, d2 = (x - y + 240) % 12;
                if (d1 == 0 || d2 == 0) c = Color.Lerp(c, on ? field : side.Trim, 0.08f);
                else if (d1 == 6 && d2 == 6) c = Color.Lerp(c, on ? field : side.Trim, 0.16f);
                if (on != Charged(x + 1, y) || on != Charged(x, y + 1) || on != Charged(x - 1, y) && on) c = Mul(c, 0.68f);
                float lit = 1.04f - 0.44f * (u * u + v * v) - 0.09f * (u + v);
                lit = MathF.Floor(lit / 0.07f + Bayer[(y & 3) * 4 + (x & 3)] / 16f) * 0.07f;
                Px[y * W + x] = Mul(c, lit);
            }
    }

    // ---------- dress ----------

    /// The top of the shoulders at a distance from the middle: the slope from the neck, then the round of the arm.
    float BodyTop(float a)
    {
        float nb = neckW + 1;
        if (a <= nb) return 82;
        if (a <= shW) return 82 + 11 * MathF.Pow((a - nb) / (shW - nb), 1.25f);
        float r = (a - shW) / 8f;
        return r >= 1 ? H + 1 : 93 + 27 * (1 - MathF.Sqrt(1 - r * r));
    }

    static float Noise(int x, int y) => Ease.Hash(x * 7349 + y * 9151 + 17);

    /// Dress: 0 plain cloth (a tunic, a gown), 1 a lord's cloth under fur with a chain, 2 mail under a cloak in the
    /// realm's colour, 3 plate with a tabard, 4 a soldier's padded jack. A ruler wears ermine on his shoulders whatever is under it.
    void Body()
    {
        Color cloth = f.Dress is 0 or 4 ? Color.Lerp(side.Cloth, new Color(196, 184, 160), f.Dress == 4 ? 0.55f : 0.38f) : side.Cloth;
        Color fur = f.Crown ? new Color(236, 230, 214) : new Color(132, 96, 62);
        float phase = Hs(40) * 6;
        for (int y = 80; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float fy = y + 0.5f, dx = x + 0.5f - Cx, a = MathF.Abs(dx), top = BodyTop(a);
                if (fy < top) continue;
                float t = 0.66f - dx / (shW + 8) * 0.2f + (fy - top < 3 ? 0.12f : 0) - (fy - 90) / 30f * 0.08f - 0.22f * G(dx - 5, fy - 92, 13, 7);
                float fold = MathF.Sin(a * 0.42f + (fy - 84) * 0.30f + phase);
                float tc = t + (fy > top + 4 ? fold > 0.9f ? -0.13f : fold < -0.9f ? 0.07f : 0 : 0);
                float ell = a * a / 225f + (fy - 86) * (fy - 86) / 81f;
                Color c;
                if (f.Dress == 0 && !male)
                {
                    if (fy < 86 && a < 15 || ell < 1) // the gown's scoop: skin, with the collarbones
                    {
                        float ts = 0.66f - dx / 30f * 0.2f - 0.2f * G(dx - 3, fy - 86, 10, 5) - (MathF.Abs(fy - 88 - a * 0.12f) < 0.7f && a > 4 && a < 12 ? 0.1f : 0);
                        Put(x, y, Tone(skin, Q(ts, x, y), SkinShade), NeckM);
                        continue;
                    }
                    c = ell < 1.34f ? Tone(side.Trim, Q(t, x, y) - ((x + y) % 3 == 0 ? 1 : 0)) : Tone(cloth, Q(tc, x, y));
                }
                else if (a <= neckW + 1 && fy < Neckline(a)) continue;
                else if (f.Dress == 0)
                {
                    c = Tone(cloth, Q(tc, x, y));
                    float nl = Neckline(a);
                    if (a <= neckW + 2 && fy - nl < 2) c = Tone(cloth, Q(t, x, y) - 1); // the hem of the neck
                    else if (a < 1 && fy - nl < 13) c = Tone(cloth, 0); // the slit, laced
                    else if (a < 3 && fy - nl < 13 && (y + (dx < 0 ? 0 : 2)) % 4 == 0) c = Linen;
                    else if (MathF.Abs(a - (shW - 2)) < 0.6f && y % 2 == 0) c = Tone(cloth, Q(tc, x, y) - 1); // the shoulder seam
                }
                else if (f.Dress == 4) // a padded jack, quilted in long seams
                    c = Tone(cloth, Q(tc, x, y) - ((int)(a + 0.5f) % 5 == 2 ? 1 : (int)(a + 0.5f) % 5 == 3 ? -1 : 0));
                else if (f.Dress == 1)
                {
                    bool collar = fy - top < 8 - a * 0.06f && a < shW + 3 || a < neckW + 7 - (fy - 88) * 0.32f && fy < 108;
                    float yc = 108 - a * a / 45f;
                    if (collar) c = FurAt(fur, t, x, y);
                    else if (a < 2.5f && fy > 108 && fy < 113) c = a < 1.2f && fy > 109.5f && fy < 111.5f ? new Color(186, 40, 48) : Tone(Gold, dx < 0 ? 4 : 2);
                    else if (a < 20 && MathF.Abs(fy - yc) < 1 && x % 3 != 0) c = Tone(Gold, x % 3 == 1 ? 4 : 3);
                    else c = Tone(cloth, Q(tc, x, y));
                }
                else if (f.Dress == 2)
                {
                    float cloak = a - (15 + (fy - 84) * 0.42f), yc = 95 + (1 - a * a / 361f) * 4;
                    if (cloak > 1.5f) c = Tone(cloth, Q(tc - 0.03f, x, y));
                    else if (cloak > 0) c = Tone(side.Trim, Q(t, x, y));
                    else if (a < 19 && MathF.Abs(fy - yc) < 0.8f && x % 2 == 0) c = Tone(Gold, 4);
                    else c = Tone(Steel, Q(t - 0.06f + ((x + ((y & 1) << 1)) & 3) switch { 0 => 0.16f, 2 => -0.16f, _ => 0 }, x, y, 0));
                    if (MathF.Abs(a - 18.5f) < 2 && MathF.Abs(fy - 95) < 2 && cloak > -4.5f) c = Tone(Gold, MathF.Abs(a - 18.5f) < 1 && MathF.Abs(fy - 95) < 1 ? 5 : dx < 0 ? 4 : 2); // the cloak's clasps
                }
                else
                {
                    if (a < 13) // the tabard and its mark
                    {
                        bool mark = a < 1.5f && fy > 96 || MathF.Abs(fy - 103) < 1.5f && a < 7;
                        c = a > 11 || mark ? Tone(side.Trim, Q(t + 0.05f, x, y)) : Tone(cloth, Q(tc, x, y));
                    }
                    else // the lames of a pauldron
                    {
                        float px = a - (shW - 5), py = fy - 97, rr = MathF.Sqrt(px * px * 0.6f + py * py), band = rr % 6;
                        float ts = t + 0.24f * G(dx + shW - 7, fy - 92, 5, 3) + (band < 1 ? -0.3f : band < 2.2f ? 0.14f : 0);
                        c = Tone(Steel, Q(ts, x, y, 0.015f));
                    }
                }
                if (f.Crown && f.Dress != 1 && a > neckW + 1 && fy - top < 7) c = FurAt(fur, t, x, y);
                Put(x, y, c, DressM);
            }
    }

    /// Fur: flecked by a hash; a ruler's ermine carries its black tails.
    Color FurAt(Color fur, float t, int x, int y)
    {
        if (f.Crown && (x + y / 5 * 3) % 7 == 3 && y % 5 is 1 or 2) return new Color(30, 26, 30);
        return Tone(fur, Q(t + (Noise(x, y) - 0.5f) * 0.34f, x, y, 0));
    }

    /// What stands round the neck over the skin: a shirt's edge, mail's padded collar, a gorget.
    void Collar()
    {
        if (f.Dress == 0) return;
        for (int y = 76; y < 92; y++)
            for (int x = Cx - neckW - 8; x <= Cx + neckW + 8; x++)
            {
                float fy = y + 0.5f, dx = x + 0.5f - Cx, a = MathF.Abs(dx), nl = Neckline(a);
                float half = neckW + 1.5f + MathF.Max(0, fy - 80) * 0.5f;
                if (a > half) continue;
                float t = 0.66f - dx / half * 0.28f;
                if (f.Dress == 4)
                {
                    if (fy < nl - 4.5f || fy >= nl + 1) continue;
                    Color jack = Color.Lerp(side.Cloth, new Color(196, 184, 160), 0.55f);
                    Put(x, y, Tone(jack, Q(t, x, y) - (fy < nl - 3.5f ? 1 : 0)), CollarM);
                }
                else if (f.Dress == 1) { if (fy >= nl - 2.2f && fy < nl + 0.5f && a <= neckW + 1) Put(x, y, Tone(Linen, Q(t + 0.1f, x, y)), CollarM); }
                else if (f.Dress == 2)
                {
                    if (fy < nl - 5.5f || fy >= nl + 1) continue;
                    Color c = fy < nl - 4.5f ? Tone(Style.Leather, Q(t, x, y)) : Tone(Steel, Q(t - 0.1f + ((x + ((y & 1) << 1)) & 3) switch { 0 => 0.16f, 2 => -0.16f, _ => 0 }, x, y, 0));
                    Put(x, y, c, CollarM);
                }
                else
                {
                    if (fy < nl - 8.5f || fy >= nl + 1.5f) continue;
                    float ts = t + 0.26f * G(dx + half * 0.45f, 0, 1.6f, 1) + (fy < nl - 7.5f ? 0.16f : fy > nl ? -0.22f : 0);
                    Put(x, y, Tone(Steel, Q(ts, x, y, 0.015f)), CollarM);
                }
            }
    }

    // ---------- hair ----------

    float HairVol => (f.Cut == 3 ? 1.4f : f.Cut == 2 ? 2f : f.Cut == 4 ? 0.6f : 3.2f) + (male ? 0 : 0.8f);
    int LongEnd => male ? 90 : 106;

    /// Half the width of the hair's mass at a row: the skull grown by `vol` all round.
    float HairHalf(float y, float vol)
    {
        if (y < topY - vol) return -1;
        if (y >= eyeY) return HeadHalf(MathF.Min(y, chinY)) + vol;
        float yy = MathF.Max(topY, eyeY - (eyeY - y) / (1 + vol / (eyeY - topY)));
        return HeadHalf(yy) + vol;
    }

    /// The row where hair stops and brow begins, by distance from the middle: each cut has its own line, the temples
    /// run down to the ears, and an old man's recedes.
    float Hairline(float dx)
    {
        float a = MathF.Abs(dx), hl;
        switch (f.Cut)
        {
            case 1: hl = topY + 8.5f + a * 0.4f; break; // parted in the middle
            case 2: hl = topY + 7.5f + a * a * 0.012f; break; // drawn back
            case 3: hl = topY + 9 - MathF.Max(0, 2 - a); break; // cropped, a small peak
            default:
                hl = Hs(41) < 0.5f ? topY + 12.5f - (dx + 8) * 0.14f : topY + 13.5f + (Ease.Hash((int)MathF.Floor(dx / 2) * 31 + f.Seed) - 0.5f) * 2; // swept, or a fringe
                break;
        }
        if (male && f.Age > 52) hl -= 3.2f * G(a - 9, 0, 3.5f, 1);
        if (a > wd - 7.5f) hl += (a - (wd - 7.5f)) * 3.4f;
        return hl;
    }

    void BackHair()
    {
        if (f.Cut != 1) return;
        float vol = HairVol;
        for (int y = topY + 3; y < LongEnd - 2; y++)
            for (int x = 0; x < W; x++)
            {
                float fy = y + 0.5f, dx = x + 0.5f - Cx, a = MathF.Abs(dx);
                if (a > (fy < eyeY ? HairHalf(fy, vol) : wd + vol + 1.5f + MathF.Sin(fy * 0.3f + f.Seed) * 0.7f)) continue;
                float n = Ease.Hash((x / 2 + f.Seed % 1000) * 131 + 7);
                Put(x, y, Tone(hair, Q(0.30f - dx / wd * 0.1f + (n - 0.5f) * 0.2f, x, y, 0.02f)), BackHairM);
            }
    }

    void Hair()
    {
        if (f.Cut == 4) { BaldFringe(); return; }
        float vol = HairVol, partX = f.Cut == 0 && Hs(41) < 0.5f ? 6 : 0;
        int sideEnd = eyeY + (f.Cut == 3 ? -5 : f.Cut == 2 ? -3 : 1);
        for (int y = 4; y < sideEnd; y++)
            for (int x = Cx - 30; x < Cx + 30; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f, dx = fx - Cx, a = MathF.Abs(dx);
                float cy = topY + 4, ang = MathF.Atan2(fy - cy, dx - partX), r = MathF.Sqrt((dx - partX) * (dx - partX) + (fy - cy) * (fy - cy));
                float n = Ease.Hash(((int)MathF.Floor(ang * 7 + 100) + f.Seed % 1000) * 131 + 7);
                float v2 = vol + (f.Cut is 0 or 1 ? (n - 0.5f) * 1.5f : 0);
                if (a > HairHalf(fy, v2)) continue;
                if (fy >= topY && a <= HeadHalf(fy) && fy >= Hairline(dx)) continue;
                float t = 0.60f - dx / wd * 0.2f - MathF.Max(0, fy - topY - 9) / 40f * 0.12f + (n - 0.5f) * (f.Cut == 3 ? 0.14f : 0.3f);
                if (r > 8 && r < 13 && dx < 6) t += 0.16f; // the shine across the crown
                if (f.Cut == 3 && (x * 7 + y * 13) % 5 == 0) t -= 0.1f;
                Put(x, y, Tone(hair, Math.Min(4, Q(t, x, y, 0.02f))), HairM);
            }
        if (f.Cut == 1) Curtains(vol);
        if (f.Cut == 2) { Bun(); if (!male) Braid(); }
    }

    /// Long hair falling past the face to the shoulders, over the ears and the dress.
    void Curtains(float vol)
    {
        for (int y = eyeY - 8; y < LongEnd; y++)
            for (int x = 0; x < W; x++)
            {
                float fy = y + 0.5f, dx = x + 0.5f - Cx, a = MathF.Abs(dx);
                float outer = fy < eyeY ? HairHalf(fy, vol) : wd + vol + MathF.Min(2.5f, (fy - eyeY) * 0.12f) + MathF.Sin(fy * 0.3f + f.Seed) * 0.7f;
                float inner = fy < eyeY + 4 ? HeadHalf(fy) - 2.5f : wd - 3.5f + MathF.Min(1.5f, (fy - eyeY - 4) * 0.05f);
                if (a > outer || a < inner) continue;
                int strand = x / 2 + f.Seed % 1000;
                float n = Ease.Hash(strand * 131 + 7);
                if (y >= LongEnd - (int)(Ease.Hash(strand * 53 + 3) * 5)) continue; // ragged ends
                float t = 0.58f - dx / wd * 0.18f + (n - 0.5f) * 0.34f + MathF.Sin(fy * 0.25f + strand) * 0.06f;
                Put(x, y, Tone(hair, Math.Min(4, Q(t, x, y, 0.02f))), HairM);
            }
    }

    /// Hair tied up: a man's topknot with its cord, a woman's coil.
    void Bun()
    {
        float cy = topY - (male ? 4.5f : 6), rx = male ? 4.5f : 7.5f, ry = male ? 3.8f : 5.5f;
        for (int y = 4; y < topY + 3; y++)
            for (int x = Cx - 10; x < Cx + 10; x++)
            {
                float dx = (x + 0.5f - Cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy > 1) continue;
                float n = Ease.Hash(((int)MathF.Floor(MathF.Atan2(dy, dx) * 5 + 50) + f.Seed % 1000) * 131 + 9);
                Put(x, y, Tone(hair, Math.Min(4, Q(0.6f - dx * 0.22f - dy * 0.18f + (n - 0.5f) * 0.24f, x, y, 0.02f))), HairM);
            }
        if (male) for (int y = topY - 2; y < topY; y++) for (int x = Cx - 3; x < Cx + 3; x++) Put(x, y, Tone(side.Trim, y == topY - 2 ? 3 : 2), HairM);
    }

    /// A braid over her shoulder.
    void Braid()
    {
        for (int y = eyeY + 8; y < 105; y++)
        {
            float xc = Cx - wd - 1.5f - (y - eyeY - 8) * 0.07f;
            int seg = (y - eyeY - 8) / 5, s = (y - eyeY - 8) % 5;
            float half = 3 - MathF.Abs(s - 2) * 0.6f;
            for (int x = (int)xc - 4; x <= (int)xc + 4; x++)
            {
                float d = x + 0.5f - xc;
                if (MathF.Abs(d) > half) continue;
                int tone = y >= 101 ? -1 : 2 + (seg % 2 == 0 == d < 0 ? 1 : 0) - (s == 0 ? 2 : 0);
                Put(x, y, tone < 0 ? Tone(side.Trim, 3) : Tone(hair, tone), HairM);
            }
        }
    }

    /// A bald head keeps a fringe over the ears (unless he is young and shaves it).
    void BaldFringe()
    {
        if (f.Age < 30 && Hs(42) < 0.6f) return;
        for (int y = eyeY - 11; y < eyeY + 1; y++)
            for (int x = Cx - 28; x < Cx + 28; x++)
            {
                float fy = y + 0.5f, dx = x + 0.5f - Cx, a = MathF.Abs(dx), half = HeadHalf(fy);
                if (a < half - 1.6f - MathF.Min(2.2f, (fy - eyeY + 11) * 0.3f) || a > half + 1.2f) continue;
                float n = Ease.Hash((x + f.Seed % 1000) * 131 + y / 3 * 977 + 11);
                Put(x, y, Tone(hair, Math.Min(4, Q(0.56f - dx / wd * 0.2f + (n - 0.5f) * 0.3f, x, y, 0.02f))), HairM);
            }
    }

    /// Beard: 1 full, 2 a moustache, 3 a moustache and a forked chin beard.
    void Beard()
    {
        if (f.Beard == 0) return;
        int ext = f.Beard == 1 ? 4 + (f.Age > 45 ? 3 : 0) + (int)(Hs(31) * 3) : f.Beard == 3 ? 8 : 0;
        float jawHalf = HeadHalf(chinY - 3) + 0.8f;
        for (int y = Math.Min(eyeY + 8, tipY + 3); y <= chinY + ext; y++)
            for (int x = Cx - 26; x < Cx + 26; x++)
            {
                float fy = y + 0.5f, dx = x + 0.5f - Cx, a = MathF.Abs(dx), head = HeadHalf(fy);
                bool on = false;
                if (y >= tipY + 3 && y < mouthY && a <= mouthW + 2.5f && !(y == tipY + 3 && a < 1)) on = true; // the moustache
                if (y >= mouthY && y <= mouthY + 3 && a >= mouthW && a <= mouthW + 2.8f) on = true; // and its ends
                if (f.Beard == 1 && y >= eyeY + 8)
                {
                    float bw = fy <= chinY - 3 ? head + 0.8f : MathHelper.Lerp(jawHalf, 2.5f, MathF.Pow((fy - (chinY - 3)) / (ext + 3), 1.6f));
                    if (a <= bw && (a > bw - (2.5f + (fy - (eyeY + 8)) * 0.6f) || y >= mouthY + 4)) on = true;
                }
                if (f.Beard == 3 && y >= mouthY + 4)
                {
                    float cw = y <= chinY ? 7.5f : 7.5f - (y - chinY) * 0.3f, gap = y > chinY - 1 ? (y - chinY + 1) * 0.55f : 0;
                    if (a <= cw && a >= gap) on = true;
                }
                if (!on || y >= mouthY && y <= mouthY + 2 && a <= mouthW - 1) continue; // the lips show
                int col = (int)MathF.Floor((x + 0.5f) / 1.5f) + f.Seed % 1000;
                float n = Ease.Hash(col * 131 + (int)((y + Ease.Hash(col * 71) * 5) / 5) * 1777 + 13);
                float t = 0.56f - dx / wd * 0.2f + (n - 0.5f) * 0.3f - (fy > chinY ? 0.08f : 0);
                Put(x, y, Tone(hair, Math.Min(4, Q(t, x, y, 0.02f))), BeardM);
            }
    }

    /// A ruler's circlet: a gold band round the brow with points and stones.
    void Crown()
    {
        if (!f.Crown) return;
        for (int x = Cx - 28; x < Cx + 28; x++)
        {
            float dx = x + 0.5f - Cx, a = MathF.Abs(dx);
            float yb = topY + 5.5f + a / wd * (a / wd) * 4.5f;
            if (a > HairHalf(yb + 2, HairVol) + 0.8f) continue;
            int y0 = (int)yb, p = (x - Cx + 70) % 7, spike = p == 3 ? 4 : p is 2 or 4 ? 2 : 0;
            for (int i = -spike; i < 4; i++)
                Put(x, y0 + i, Tone(Gold, (i <= 0 ? 4 : i == 3 ? 1 : 3) + (dx < -6 ? 1 : dx > 8 ? -1 : 0)), CrownM);
            if (a < 1.2f) { Put(x, y0 + 1, new Color(206, 44, 52), CrownM); Put(x, y0 + 2, new Color(140, 24, 36), CrownM); }
            if (MathF.Abs(a - 10.5f) < 1.1f) { Put(x, y0 + 1, new Color(72, 128, 206), CrownM); Put(x, y0 + 2, new Color(36, 70, 140), CrownM); }
        }
    }
}
