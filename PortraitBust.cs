using Microsoft.Xna.Framework;

namespace Ironfield;

/// One bust being painted (docs/archive/LORDS.md LD1): the canvas, which layer owns each pixel, and the measures of this
/// head. The head is a height field lit from the upper left and cut into six tones, so brow, nose, cheek and chin are
/// modelled, not drawn flat. Drawing only, headless (colours and maths, no GPU).
/// Layers go on in this order: arms behind, back hair, dress, neck, collar, ears, head and features, beard, hair, crown, finish.
sealed partial class Bust
{
    const int W = PortraitPainter.W, H = PortraitPainter.H, Cx = W / 2;
    const byte Bg = 0, BackHairM = 1, DressM = 2, NeckM = 3, CollarM = 4, EarM = 5, HeadM = 6, BeardM = 7, HairM = 8, CrownM = 9;
    static readonly Color SkinShade = new(112, 38, 52), Cool = new(44, 28, 64), Ink = new(22, 16, 24);
    static readonly Color Gold = new(218, 172, 62), Steel = new(150, 156, 170), Linen = new(216, 206, 182);
    static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    public readonly Color[] Px = new Color[W * H];
    readonly byte[] mat = new byte[W * H];
    readonly Face f;
    readonly Heraldry side;
    readonly Color skin, hair;
    /// Head measures in pixels: half width at the eyes, the chin's share of it, how late the jaw tapers; rows of the
    /// skull top, eyes, nose tip, mouth and chin; half the gap between the eyes; half widths of mouth, neck and shoulders.
    readonly float wd, chinW, jawE;
    readonly int topY = 17, eyeY = 46, tipY, mouthY, chinY, eyeDx, mouthW, neckW, shW;
    readonly bool male;

    public Bust(Face f)
    {
        this.f = f;
        side = Style.Sides[f.Palette];
        male = !f.Female;
        skin = Variety.Skins[f.Skin];
        float grey = Math.Clamp((f.Age - 40) / 25f, 0, 0.88f);
        hair = Color.Lerp(Variety.Hairs[f.Hair], new Color(204, 202, 198), grey);
        wd = (male ? 20.5f : 18.5f) + (f.Jaw == 0 ? 0.8f : f.Jaw == 2 ? -0.8f : 0) + (Hs(20) - 0.5f) * 1.6f;
        chinW = (f.Jaw == 0 ? 0.72f : f.Jaw == 1 ? 0.60f : 0.47f) - (male ? 0 : 0.10f);
        jawE = f.Jaw == 0 ? 2.5f : f.Jaw == 1 ? 1.9f : 1.45f;
        tipY = eyeY + 9 + f.Nose * 2;
        mouthY = tipY + 7 - (f.Nose == 2 ? 1 : 0);
        chinY = eyeY + 30 + (f.Nose == 2 ? 1 : 0);
        eyeDx = (male ? 9 : 8) + (Hs(21) > 0.7f ? 1 : 0);
        mouthW = (male ? 6 : 5) + (Hs(22) > 0.6f ? 1 : 0);
        neckW = male ? (f.Jaw == 0 ? 11 : 10) : 8;
        shW = male ? 35 : 29;
    }

    public Color[] Paint()
    {
        Background();
        BackHair();
        Body();
        Neck();
        Collar();
        Ears();
        Head();
        Wrinkles();
        Eyes();
        Brows();
        Nose();
        Mouth();
        Scars();
        Beard();
        Hair();
        Crown();
        Finish();
        return Px;
    }

    // ---------- tools ----------

    float Hs(int salt) => Ease.Hash(f.Seed * 31 + salt * 977);
    static float G(float dx, float dy, float sx, float sy) => MathF.Exp(-(dx * dx / (2 * sx * sx) + dy * dy / (2 * sy * sy)));
    static Color Mul(Color c, float k) => new((int)Math.Clamp(c.R * k, 0, 255), (int)Math.Clamp(c.G * k, 0, 255), (int)Math.Clamp(c.B * k, 0, 255));

    /// A six-step ramp from one base colour (3 = the base): shadows lean to `shade` (cool violet, or blood red for skin),
    /// lights to a warm white.
    static Color Tone(Color c, int t, Color? shade = null)
    {
        var s = shade ?? Cool;
        return Math.Clamp(t, 0, 5) switch
        {
            0 => Color.Lerp(Mul(c, 0.40f), s, 0.30f),
            1 => Color.Lerp(Mul(c, 0.58f), s, 0.18f),
            2 => Color.Lerp(Mul(c, 0.79f), s, 0.08f),
            3 => c,
            4 => Color.Lerp(c, new Color(255, 240, 206), 0.28f),
            _ => Color.Lerp(c, new Color(255, 248, 228), 0.52f),
        };
    }

    /// Light (0..1) cut into a tone, with a little ordered dither where two tones meet.
    static int Q(float t, int x, int y, float dither = 0.03f)
    {
        t += (Bayer[(y & 3) * 4 + (x & 3)] / 16f - 0.47f) * dither * 2;
        return t >= 0.93f ? 5 : t >= 0.80f ? 4 : t >= 0.60f ? 3 : t >= 0.46f ? 2 : t >= 0.32f ? 1 : 0;
    }

    void Put(int x, int y, Color c, byte m)
    {
        if ((uint)x >= W || (uint)y >= H) return;
        Px[y * W + x] = c; mat[y * W + x] = m;
    }

    byte Mat(int x, int y) => (uint)x < W && (uint)y < H ? mat[y * W + x] : Bg;
    static bool IsSkin(byte m) => m is NeckM or EarM or HeadM;

    /// Darken a pixel of skin (a crease, a fold, a cast shadow); other stuff is left alone.
    void Crease(int x, int y, float k = 0.8f)
    {
        if ((uint)x >= W || (uint)y >= H || !IsSkin(mat[y * W + x])) return;
        Px[y * W + x] = Color.Lerp(Mul(Px[y * W + x], k), SkinShade, (1 - k) * 0.5f);
    }

    // ---------- the head ----------

    /// Half the head's width at a row: a round skull above the eyes, cheeks tapering to the chin below (by `Jaw`).
    float HeadHalf(float y)
    {
        if (y < topY || y > chinY) return -1;
        if (y <= eyeY) { float t = (eyeY - y) / (eyeY - topY); return wd * MathF.Pow(1 - MathF.Pow(t, 2.4f), 1 / 2.4f); }
        float s = (y - eyeY) / (chinY - eyeY);
        return wd * MathHelper.Lerp(1, chinW, MathF.Pow(s, jawE)) * MathF.Sqrt(1 - MathF.Pow(s, 8));
    }

    /// The face's height towards the viewer: a dome with brow ridge, eye sockets, nose, cheekbones, muzzle and chin on it.
    float Z(float x, float y)
    {
        float half = HeadHalf(y), dx = x - Cx, a = MathF.Abs(dx);
        if (half <= 0.5f) return -20;
        float u = dx / half, vv = (y - (eyeY - 1)) / 38f;
        float z = u * u < 1 ? 17 * MathF.Sqrt(1 - u * u) * MathF.Sqrt(MathF.Max(0.05f, 1 - vv * vv)) : -(MathF.Abs(u) - 1) * 30;
        float age = Math.Clamp((f.Age - 40) / 30f, 0, 1);
        z += (male ? 2.2f : 0.9f) * G(a - eyeDx, y - (eyeY - 6), 6.5f, 2.2f);
        z -= (2.4f + age * 0.8f) * G(a - eyeDx, y - eyeY - 0.5f, 5.5f, 3.2f);
        float n0 = eyeY - 4;
        if (y > n0 && y < tipY + 4)
        {
            float s = Math.Clamp((y - n0) / (tipY - n0), 0, 1), wN = 1.5f + 1.5f * s + (f.Nose == 0 ? 0.3f : 0);
            float fall = y <= tipY ? 1 : MathF.Exp(-(y - tipY) * (y - tipY) / 3f);
            z += (1.1f + 4.4f * s * s) * MathF.Exp(-dx * dx / (2 * wN * wN)) * fall;
        }
        z += 1.5f * G(a - 4, y - (tipY - 0.5f), 1.8f, 1.8f);
        z += (1.5f + age * 0.8f) * G(a - 13, y - (eyeY + 7), 6, 4.5f);
        z -= (0.5f + age * 1.5f + (f.Jaw == 2 ? 0.5f : 0)) * G(a - 12, y - (mouthY - 1), 4, 5);
        z += 1.3f * G(dx, y - mouthY, 9, 5);
        z += (male ? 1.6f : 1.0f) * G(dx, y - (chinY - 5), 5.5f, 3.2f);
        return z;
    }

    static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.52f, -0.46f, 0.72f));

    void Head()
    {
        bool stubble = male && f.Beard == 0 && f.Age > 22 && Hs(30) < 0.5f;
        Color shadowHair = Color.Lerp(hair, new Color(60, 60, 70), 0.5f);
        for (int y = topY - 1; y <= chinY + 1; y++)
            for (int x = Cx - 26; x < Cx + 26; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f, dx = fx - Cx, a = MathF.Abs(dx);
                if (a > HeadHalf(fy)) continue;
                var n = Vector3.Normalize(new Vector3(Z(fx - 0.5f, fy) - Z(fx + 0.5f, fy), Z(fx, fy - 0.5f) - Z(fx, fy + 0.5f), 1));
                float t = 0.20f + 0.80f * MathF.Max(0, Vector3.Dot(n, Light));
                t -= 0.15f * G(dx - 4.5f, fy - (tipY - 4), 2.2f, 5); // the nose's shadow
                t -= 0.22f * G(dx - 0.5f, fy - (tipY + 2.4f), 4, 1.1f); // under the nose
                t -= 0.08f * G(dx, fy - (mouthY + 4.6f), 4, 1.1f); // under the lip
                t -= 0.10f * G(a - eyeDx, fy - (eyeY - 3), 6, 1.5f); // under the brow
                if (f.Mood == 3) t -= 0.06f * G(a - eyeDx, fy - eyeY, 7, 4);
                if (!male || f.Age < 20) t = MathHelper.Lerp(t, 0.72f, 0.28f); // a softer face
                Color col = Tone(skin, Q(t, x, y), SkinShade);
                float blush = G(a - 12, fy - (eyeY + 9), 5, 3.5f) + 0.7f * G(dx, fy - (tipY - 1), 2.5f, 2.5f);
                if (blush > 0.45f) col = Color.Lerp(col, new Color(204, 92, 84), !male || f.Age < 22 || f.Mood == 0 ? 0.16f : 0.09f);
                if (stubble && fy > tipY + 2)
                {
                    float sb = MathF.Max(G(dx, fy - (mouthY - 2.5f), 8, 1.6f), fy > mouthY + 2 || a > mouthW + 3 ? 1 : 0);
                    if (sb > 0.4f) col = Color.Lerp(col, shadowHair, 0.14f);
                }
                Put(x, y, col, HeadM);
            }
    }

    void Ears()
    {
        for (int k = -1; k <= 1; k += 2)
        {
            float ex = Cx + k * (HeadHalf(eyeY + 4) + 0.5f);
            for (int y = eyeY - 2; y <= eyeY + 10; y++)
                for (int x = (int)ex - 4; x <= (int)ex + 4; x++)
                {
                    float dx = (x + 0.5f - ex) / 2.7f, dy = (y + 0.5f - (eyeY + 4)) / 5.4f;
                    if (dx * dx + dy * dy > 1) continue;
                    bool inner = dx * dx + dy * dy < 0.3f;
                    int tone = (k < 0 ? 3 : 2) - (inner ? 1 : 0);
                    Put(x, y, Tone(skin, Math.Max(0, tone), SkinShade), EarM);
                }
        }
    }

    void Neck()
    {
        for (int y = mouthY; y < H; y++)
            for (int x = Cx - neckW - 8; x <= Cx + neckW + 8; x++)
            {
                float dx = x + 0.5f - Cx, a = MathF.Abs(dx);
                float half = neckW + MathF.Max(0, y - 82) * 0.45f;
                if (a > half || y >= Neckline(a)) continue;
                float t = 0.64f - dx / half * 0.22f - 0.34f * Math.Clamp(1 - (y - chinY) / 9f, 0, 1);
                if (male && MathF.Abs(a - neckW * 0.45f) < 0.8f && y > chinY + 4) t -= 0.07f; // the cords of the neck
                Put(x, y, Tone(skin, Q(t, x, y), SkinShade), NeckM);
            }
    }

    /// Where the dress begins under the neck, by distance from the middle.
    float Neckline(float a)
    {
        if (f.Dress == 0 && !male) return a < 15 ? 86 + 9 * MathF.Sqrt(MathF.Max(0, 1 - a * a / 225f)) : 0; // a gown's scoop
        if (f.Dress == 0) return 88 + MathF.Max(0, neckW + 1 - a) * 0.75f; // a tunic's shallow V
        return 87 + 2 * (1 - MathF.Min(1, a / (neckW + 1)) * MathF.Min(1, a / (neckW + 1)));
    }

    // ---------- features ----------

    void Eyes()
    {
        int lid = f.Mood >= 2 ? 1 : 0, low = f.Mood == 3 ? 1 : 0;
        Color lash = Color.Lerp(Tone(skin, 0, SkinShade), Ink, male ? 0.55f : 0.9f), iris = PortraitPainter.EyeColours[f.Eye];
        for (int k = -1; k <= 1; k += 2)
        {
            int ex = Cx + k * eyeDx + (k > 0 ? -1 : 0);
            float dim = k > 0 ? 0.93f : 1;
            for (int j = -4; j <= 4; j++) // j runs outwards from the nose
            {
                int x = ex + k * j, aj = Math.Abs(j);
                int top = eyeY - 2 + (aj == 4 ? 2 : aj == 3 ? 1 : 0) + lid, bot = eyeY + 1 - (aj == 4 ? 1 : 0) - (aj <= 3 ? low : 0);
                if (j == 4 && f.Mood == 0) bot -= 0;
                if (top > bot) top = bot;
                for (int y = top; y <= bot; y++)
                {
                    Color c = Mul(new Color(244, 240, 232), dim);
                    bool inIris = aj <= 1 || aj == 2 && y >= eyeY - 1 && y <= eyeY;
                    if (inIris) c = y <= eyeY - 2 + lid ? Mul(iris, 0.62f) : y >= eyeY + 1 ? Color.Lerp(iris, Color.White, 0.3f) : Color.Lerp(iris, Color.White, 0.1f);
                    if (j == 0 && y >= eyeY - 1 && y <= eyeY) c = new Color(14, 10, 16);
                    Put(x, y, c, HeadM);
                }
                Put(x, top - 1, lash, HeadM);
                if (!male && j >= 1) Put(x, top - 2 + (j >= 3 ? 1 : 0), lash, HeadM); // a darker lash
                if (aj <= 3) Crease(x, top - 3 - (aj <= 1 ? 1 : 0), male ? 0.88f : 0.92f); // the fold of the lid
                if (j >= 0 && j <= 3) Crease(x, bot + 1, 0.80f);
                else if (j >= -2) Crease(x, bot + 1, 0.9f);
                if (f.Age > 46 && aj <= 2) Crease(x, bot + 3, 0.86f); // bags
                if (f.Mood == 0 && aj <= 2) Crease(x, bot + 2, 0.93f); // the cheek lifts in a smile
            }
            Put(ex - 1, eyeY - 1 + lid, new Color(252, 252, 248), HeadM); // the light in the eye
            if (!male) { Put(ex + k * 5, eyeY - 1 + lid, lash, HeadM); Put(ex + k * 5, eyeY - 2 + lid, lash, HeadM); }
            if (f.Age > 42) { Crease(ex + k * 6, eyeY + 1, 0.84f); Crease(ex + k * 7, eyeY + 2, 0.84f); Crease(ex + k * 6, eyeY - 2, 0.86f); }
        }
    }

    void Brows()
    {
        Color browC = Color.Lerp(hair, new Color(56, 38, 30), 0.35f);
        int by = eyeY - 7 + (f.Mood == 0 ? -1 : f.Mood >= 2 ? 1 : 0);
        for (int k = -1; k <= 1; k += 2)
        {
            int ex = Cx + k * eyeDx + (k > 0 ? -1 : 0);
            for (int j = -5; j <= 6; j++)
            {
                int y = by - (j >= -2 && j <= 4 ? 1 : 0) + (j >= 5 ? j - 4 : 0);
                if (f.Mood == 2 && j <= -2) y += 1;
                if (f.Mood == 3) y += j <= -3 ? 2 : j <= 0 ? 1 : j >= 4 ? -1 : 0;
                if (f.Mood == 0 && j <= -3) y -= 1;
                int th = f.Brow == 0 ? (j >= -3 && j <= 1 ? 2 : 1) : f.Brow == 1 ? (j >= 4 ? 1 : 2) : j >= 5 ? 2 : 3;
                if (!male) th = Math.Max(1, th - 1);
                for (int i = 0; i < th; i++) Put(ex + k * j, y - i, Tone(browC, i == th - 1 && th > 1 ? 2 : 1), HeadM);
            }
        }
    }

    void Nose()
    {
        Color dark = Tone(skin, 0, SkinShade);
        Put(Cx - 3, tipY + 1, dark, HeadM); Put(Cx - 2, tipY + 1, dark, HeadM);
        Put(Cx + 1, tipY + 1, dark, HeadM); Put(Cx + 2, tipY + 1, dark, HeadM);
        for (int y = tipY - 1; y <= tipY; y++) { Crease(Cx - 5, y, 0.84f); Crease(Cx + 4, y, 0.8f); }
        Put(Cx - 1, tipY - 2, Tone(skin, 5, SkinShade), HeadM);
        Put(Cx - 1, tipY - 3, Tone(skin, 4, SkinShade), HeadM);
    }

    void Mouth()
    {
        int my = mouthY, mw = mouthW;
        bool thin = f.Age > 56 || male && Hs(23) < 0.35f;
        Color lipU = Color.Lerp(Tone(skin, 1, SkinShade), new Color(150, 50, 56), male ? 0.30f : 0.58f);
        Color lipL = Color.Lerp(Tone(skin, 2, SkinShade), new Color(200, 88, 88), male ? 0.26f : 0.55f);
        Color line = Mul(lipU, 0.5f);
        for (int j = -mw; j < mw; j++)
        {
            int aj = j < 0 ? -j - 1 : j; // 0 at the middle pair
            int c = aj == mw - 1 ? (f.Mood == 0 ? -1 : f.Mood >= 2 ? 1 : 0) : aj == mw - 2 && f.Mood == 3 ? 1 : 0;
            Put(Cx + j, my + c, line, HeadM);
            if (aj == mw - 1 && f.Mood is 0 or 3) Put(Cx + j + (j < 0 ? -1 : 1), my + (f.Mood == 0 ? -2 : 2), line, HeadM);
            if (aj <= mw - 3) Put(Cx + j, my - 1, lipU, HeadM);
            if (!thin && aj >= 1 && aj <= mw - 5) Put(Cx + j, my - 2, lipU, HeadM);
            if (aj <= mw - 3) Put(Cx + j, my + 1, lipL, HeadM);
            if (!thin && aj <= mw - 5) Put(Cx + j, my + 2, lipL, HeadM);
        }
        Put(Cx - 1, my + 1, Color.Lerp(lipL, Color.White, 0.3f), HeadM);
        Put(Cx - 2, my + 1, Color.Lerp(lipL, Color.White, 0.18f), HeadM);
        if (f.Mood == 0) { Crease(Cx - mw - 1, my - 2, 0.84f); Crease(Cx + mw, my - 2, 0.84f); }
        if (f.Mood == 3) for (int j = -mw + 2; j < mw - 2; j++) Put(Cx + j, my + 1, Mul(lipL, 0.82f), HeadM);
    }

    void Wrinkles()
    {
        if (f.Age > 48 || f.Mood == 3)
            for (int k = -1; k <= 1; k += 2)
                for (int i = 0; i <= mouthY - tipY; i++) Crease(Cx + k * (6 + i * 3 / (mouthY - tipY + 1)) + (k > 0 ? -1 : 0), tipY + i, 0.86f);
        if (f.Age > 50)
            for (int row = 0; row < (f.Age > 62 ? 3 : 2); row++)
                for (int j = -9 + row; j <= 9 - row; j++)
                    if ((j + row * 3 + f.Seed) % 9 != 0) Crease(Cx + j, topY + 12 + row * 4 + (Math.Abs(j) > 5 ? 1 : 0), 0.9f);
        if (f.Mood == 3) { Crease(Cx - 1, eyeY - 8, 0.8f); Crease(Cx - 1, eyeY - 7, 0.8f); Crease(Cx + 1, eyeY - 8, 0.8f); Crease(Cx + 1, eyeY - 7, 0.8f); }
        if (f.Age > 60) for (int k = -1; k <= 1; k += 2) for (int i = 0; i < 5; i++) Crease(Cx + k * (11 - i / 2), mouthY - 3 + i, 0.88f);
    }

    /// Scars: a red seam with a pale healed edge. 1 runs down through a brow, 2 across a cheek.
    void Scars()
    {
        Color seam = Color.Lerp(skin, new Color(150, 56, 60), 0.7f), pale = Tone(skin, 5, SkinShade);
        void Cut(int x, int y) { if (Mat(x, y) == HeadM) { Px[y * W + x] = seam; if (Mat(x - 1, y) == HeadM) Px[y * W + x - 1] = pale; } }
        if (f.Scar == 1)
            for (int i = 0; i < 22; i++)
            {
                int x = Cx + eyeDx + 4 - i / 4, y = eyeY - 13 + i;
                if (y >= eyeY - 3 && y <= eyeY + 2) continue; // not across the eye itself
                Cut(x, y);
            }
        if (f.Scar == 2)
            for (int i = 0; i < 12; i++) Cut(Cx - 17 + i, eyeY + 7 + i * 8 / 10);
    }

    // ---------- finish ----------

    /// Outlines and depth: a dark edge where the figure meets the arms, a shadow line where one layer lies on another,
    /// the figure's shadow on the arms, then the frame.
    void Finish()
    {
        var edge = new bool[W * H]; var under = new bool[W * H]; var cast = new bool[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                byte m = mat[y * W + x];
                if (m == Bg) { cast[y * W + x] = Mat(x - 2, y - 1) != Bg || Mat(x - 1, y) != Bg || Mat(x - 3, y - 2) != Bg; continue; }
                foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if ((uint)(x + ox) >= W || (uint)(y + oy) >= H) continue;
                    byte o = mat[(y + oy) * W + x + ox];
                    if (o == Bg) edge[y * W + x] = true;
                    else if (o > m && !(IsSkin(o) && IsSkin(m) && m != NeckM)) under[y * W + x] = true;
                }
            }
        for (int i = 0; i < W * H; i++)
        {
            if (edge[i]) Px[i] = Color.Lerp(Mul(Px[i], 0.34f), Ink, 0.45f);
            else if (under[i]) Px[i] = IsSkin(mat[i]) ? Color.Lerp(Mul(Px[i], 0.74f), SkinShade, 0.12f) : Color.Lerp(Mul(Px[i], 0.6f), Cool, 0.12f);
            else if (cast[i]) Px[i] = Color.Lerp(Mul(Px[i], 0.68f), Cool, 0.1f);
        }
        // Hair throws a soft shadow on the brow below it.
        for (int y = 1; y < H; y++)
            for (int x = 0; x < W; x++)
                if (mat[y * W + x] == HeadM && !under[y * W + x] && mat[(y - 1) * W + x] == HeadM && y >= 2 && mat[(y - 2) * W + x] is HairM or CrownM && y < eyeY - 9)
                    Px[y * W + x] = Color.Lerp(Mul(Px[y * W + x], 0.88f), SkinShade, 0.05f);
        Frame();
    }

    void Frame()
    {
        Color trim = side.Trim, lo = Mul(trim, 0.6f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int d = Math.Min(Math.Min(x, y), Math.Min(W - 1 - x, H - 1 - y));
                if (d == 0) Px[y * W + x] = Ink;
                else if (d == 1) Px[y * W + x] = x == 1 || y == 1 ? Color.Lerp(trim, Color.White, 0.25f) : trim;
                else if (d == 2) Px[y * W + x] = lo;
                else if (d == 3) Px[y * W + x] = Mul(Px[y * W + x], 0.55f);
            }
    }
}
