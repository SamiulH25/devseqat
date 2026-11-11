namespace Ironfield;

/// AU1 combat synthesis: weapon×armour strikes, shield/parry defences, ranged weapons, siege foley and captain close-mic cues.
static partial class Synth
{
    /// Cues that leave blood decals on the ground in `BattleScreen.LeaveMarks`.
    public static bool DrawsBlood(Cue c) => c is Cue.Hit or Cue.ArrowHit or Cue.SlashLight or Cue.SlashHeavy
        or Cue.BluntFlesh or Cue.PierceHit or Cue.LanceImpact or Cue.LanceShatter or Cue.ArrowFlesh;

    /// Heavy charge/impact cues that kick the field camera and churn trample earth in `BattleScreen.Effects`.
    public static bool HeavyImpact(Cue c) => c is Cue.Impact or Cue.LanceImpact or Cue.LanceShatter;

    static float[] SlashLight(Random r)
    {
        var b = Buf(R(r, 0.24f, 0.34f));
        var edge = new Band(R(r, 2900, 4100), 2.8f);
        var tear = new Band(R(r, 950, 1450), 1.4f);
        float k0 = R(r, 0.92f, 1.12f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            edge.Set((3600 - 4800 * MathF.Min(t, 0.18f)) * k0, 2.8f);
            float hiss = edge.Run(n) * MathF.Exp(-t * 24) * 1.6f;
            float cut = tear.Run(n) * MathF.Exp(-MathF.Max(0, t - 0.015f) * 32) * 1.4f;
            float ring = MathF.Sin(MathF.Tau * 2480 * k0 * t) * MathF.Exp(-t * 28) * 0.22f;
            b[i] = hiss + cut + ring;
        }
        Mix(b, Thud(R(r, 95, 130), 0.14f, 0.45f, r), 0.01f, 0.65f);
        return b;
    }

    static float[] SlashHeavy(Random r)
    {
        var b = Buf(R(r, 0.36f, 0.48f));
        var air = new Band(720, 1.8f);
        var bite = new Band(R(r, 1800, 2600), 2.2f);
        float k0 = R(r, 0.88f, 1.10f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            air.Set((920 - 520 * MathF.Min(1, t / 0.18f)) * k0, 1.8f);
            float whoosh = air.Run(n) * MathF.Sin(MathF.PI * MathF.Min(1, t / 0.14f)) * 1.4f;
            float cleave = t >= 0.03f ? bite.Run(n) * MathF.Exp(-(t - 0.03f) * 18) * 1.9f : 0;
            float steel = t >= 0.03f ? MathF.Sin(MathF.Tau * 1420 * k0 * (t - 0.03f)) * MathF.Exp(-(t - 0.03f) * 16) * 0.35f : 0;
            b[i] = whoosh + cleave + steel;
        }
        Mix(b, Thud(R(r, 62, 85), 0.26f, 0.85f, r), 0.03f, 0.95f);
        return b;
    }

    static float[] BluntFlesh(Random r)
    {
        var b = Thud(R(r, 58, 82), R(r, 0.24f, 0.32f), 0.9f, r);
        var crunch = new Band(R(r, 480, 760), 1.6f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            float snap = r.NextSingle() > 0.985f && t < 0.08f ? n * 0.8f : 0;
            b[i] += crunch.Run(n) * MathF.Exp(-t * 28) * 1.6f + snap;
        }
        return b;
    }

    static float[] BluntPlate(Random r)
    {
        var b = Thud(R(r, 68, 92), R(r, 0.38f, 0.50f), 0.7f, r);
        float k0 = R(r, 0.86f, 1.15f);
        float[] modes = { 410 * k0, 685 * k0, 1140 * k0, 1890 * k0 };
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), dent = 0;
            float bend = 1f - 0.06f * MathF.Exp(-t * 25); // plate caving inward slightly shifts pitch
            for (int m = 0; m < modes.Length; m++)
                dent += MathF.Sin(MathF.Tau * modes[m] * bend * t) * MathF.Exp(-t * (9 + m * 5)) / (m + 1.2f);
            b[i] += dent * 0.65f + N(r) * MathF.Exp(-t * 85) * 0.55f;
        }
        return b;
    }

    static float[] PierceHit(Random r)
    {
        var b = Buf(R(r, 0.22f, 0.30f));
        var grit = new Band(R(r, 1500, 2300), 2.2f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            grit.Set(2200 - 2800 * MathF.Min(t, 0.16f), 2.2f);
            b[i] = grit.Run(n) * MathF.Exp(-t * 26) * 1.7f;
        }
        Mix(b, Thud(R(r, 88, 118), 0.16f, 0.55f, r), 0, 0.8f);
        return b;
    }

    static float[] LanceImpact(Random r)
    {
        var b = Thud(R(r, 44, 62), 0.48f, 1.1f, r);
        var wood = new Band(R(r, 260, 380), 2.4f);
        float k0 = R(r, 0.9f, 1.12f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            b[i] += wood.Run(n) * MathF.Exp(-t * 14) * 2.1f
                + MathF.Sin(MathF.Tau * 820 * k0 * t) * MathF.Exp(-t * 22) * 0.35f;
        }
        return b;
    }

    static float[] LanceShatter(Random r)
    {
        var b = LanceImpact(r);
        var o = Buf(0.68f);
        Mix(o, b, 0, 1f);
        for (int k = 0; k < 18; k++)
        {
            float at = 0.02f + MathF.Pow(r.NextSingle(), 1.7f) * 0.42f, len = R(r, 0.015f, 0.045f);
            var crack = new Band(R(r, 1100, 3800), 3.2f);
            var s = Buf(len);
            for (int i = 0; i < s.Length; i++) s[i] = crack.Run(N(r)) * MathF.Exp(-T(i) / len * 4.5f) * 2.6f;
            Mix(o, s, at, R(r, 0.25f, 0.75f));
        }
        return o;
    }

    static float[] BlockWood(Random r) => Block(r);

    static float[] BlockMetal(Random r)
    {
        var b = Thud(R(r, 135, 180), 0.32f, 0.35f, r);
        float k0 = R(r, 0.9f, 1.15f);
        float[] f = { 620 * k0, 1080 * k0, 1790 * k0, 2640 * k0 };
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), s = 0;
            for (int m = 0; m < f.Length; m++) s += MathF.Sin(MathF.Tau * f[m] * t) * MathF.Exp(-t * (10 + m * 4)) / (m + 1.3f);
            b[i] += s * 0.65f + N(r) * MathF.Exp(-t * 110) * 0.45f;
        }
        return b;
    }

    static float[] ParrySteel(Random r)
    {
        var b = Buf(R(r, 0.44f, 0.60f));
        float k0 = R(r, 0.92f, 1.16f);
        float[] f = { 1480 * k0, 2210 * k0, 3190 * k0, 4680 * k0, 6120 * k0 };
        var scrape = new Band(R(r, 3200, 4600), 4f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), s = 0, n = N(r);
            for (int m = 0; m < f.Length; m++) s += MathF.Sin(MathF.Tau * f[m] * t) * MathF.Exp(-t * (7 + m * 3.5f)) / (m + 1.2f);
            b[i] = s * 0.72f + scrape.Run(n) * MathF.Exp(-t * 36) * 1.4f + n * MathF.Exp(-t * 130) * 0.5f;
        }
        return b;
    }

    static float[] ShieldSplinter(Random r)
    {
        var b = BlockWood(r);
        var o = Buf(0.55f);
        Mix(o, b, 0, 0.9f);
        for (int k = 0; k < 14; k++)
        {
            float at = 0.015f + r.NextSingle() * 0.32f, len = R(r, 0.015f, 0.04f);
            var sp = new Band(R(r, 950, 3400), 2.8f);
            var s = Buf(len);
            for (int i = 0; i < s.Length; i++) s[i] = sp.Run(N(r)) * MathF.Exp(-T(i) / len * 4f) * 2.4f;
            Mix(o, s, at, R(r, 0.3f, 0.75f));
        }
        return o;
    }

    static float[] BashThud(Random r)
    {
        var b = Thud(R(r, 68, 95), 0.28f, 0.8f, r);
        var rim = new Band(R(r, 280, 420), 2.2f);
        for (int i = 0; i < b.Length; i++) b[i] += rim.Run(N(r)) * MathF.Exp(-T(i) * 20) * 1.8f;
        return b;
    }

    static float[] SwingWhoosh(Random r, float fStart, float fEnd, float dur, float q)
    {
        var b = Buf(dur);
        var air = new Band(fStart, q);
        for (int i = 0; i < b.Length; i++)
        {
            float u = T(i) / dur;
            air.Set(fStart + (fEnd - fStart) * u, q);
            b[i] = air.Run(N(r)) * MathF.Pow(MathF.Sin(MathF.PI * u), 1.6f) * 1.5f;
        }
        return b;
    }

    static float[] SwingOne(Random r) => SwingWhoosh(r, R(r, 1500, 1950), R(r, 520, 760), R(r, 0.16f, 0.22f), 2.2f);
    static float[] SwingTwo(Random r) => SwingWhoosh(r, R(r, 820, 1120), R(r, 240, 360), R(r, 0.26f, 0.34f), 1.8f);
    static float[] SwingPole(Random r) => SwingWhoosh(r, R(r, 1100, 1450), R(r, 380, 520), R(r, 0.20f, 0.27f), 2.5f);

    static float[] RiposteRing(Random r)
    {
        var b = SwingOne(r);
        float f = R(r, 2650, 3200);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            b[i] += (MathF.Sin(MathF.Tau * f * t) + 0.4f * MathF.Sin(MathF.Tau * f * 1.5f * t)) * MathF.Exp(-t * 16) * 0.35f;
        }
        return b;
    }

    static float[] WeaponSwap(Random r)
    {
        var b = Buf(0.32f);
        var slide = new Band(2400, 4.5f);
        float f0 = R(r, 2100, 2600);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            slide.Set(f0 + 1800 * (t / 0.32f), 4.5f);
            b[i] = slide.Run(n) * MathF.Sin(MathF.PI * MathF.Min(1, t / 0.26f)) * 1.5f
                + MathF.Sin(MathF.Tau * (f0 * 1.2f) * t) * MathF.Exp(-t * 18) * 0.16f;
        }
        Mix(b, Thud(R(r, 120, 160), 0.08f, 0.4f, r), 0.02f, 0.4f);
        return b;
    }

    static float[] LowHpPulse(Random r)
    {
        var b = Buf(0.62f);
        Mix(b, Thud(R(r, 46, 54), 0.18f, 0.15f, r), 0.0f, 0.85f);
        Mix(b, Thud(R(r, 52, 60), 0.16f, 0.12f, r), 0.21f, 0.62f);
        return b;
    }

    static float[] LongbowLoose(Random r)
    {
        var b = Buf(0.48f);
        Pluck(b, 0, R(r, 108, 142), 0.72f, R(r, 0.986f, 0.992f), r, 0.75f);
        var swish = new Band(2600, 2.8f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            swish.Set(2900 - 4200 * MathF.Min(t, 0.38f), 2.8f);
            b[i] += swish.Run(N(r)) * MathF.Sin(MathF.PI * MathF.Min(1, t / 0.34f)) * 1.05f;
        }
        return b;
    }

    static float[] CrossbowSnap(Random r)
    {
        var b = Buf(0.36f);
        // Trigger sear click + short steel prod clang + fast high-velocity bolt hiss
        Mix(b, Click(0.025f, R(r, 2400, 3100), 0.5f, r), 0f, 0.8f);
        Pluck(b, (int)(0.008f * Rate), R(r, 310, 390), 0.75f, 0.968f, r, 0.95f);
        var bolt = new Band(3400, 3.2f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            if (t < 0.008f) continue;
            bolt.Set(3800 - 5500 * MathF.Min(t - 0.008f, 0.24f), 3.2f);
            b[i] += bolt.Run(N(r)) * MathF.Exp(-(t - 0.008f) * 14) * 1.35f;
        }
        return b;
    }

    static float[] CrossbowSpan(Random r)
    {
        var b = Buf(0.52f);
        int clicks = r.Next(6, 9);
        for (int k = 0; k < clicks; k++)
        {
            float at = k * (0.38f / clicks) + R(r, -0.004f, 0.004f);
            Mix(b, Click(0.022f, 1800 + k * 140 + R(r, -60, 60), 0.35f, r), at, 0.55f);
        }
        Mix(b, Click(0.04f, R(r, 2900, 3400), 0.45f, r), 0.43f, 0.85f); // nut latch locks home
        return b;
    }

    static float[] SlingCrack(Random r)
    {
        var b = Buf(0.44f);
        var whirr = new Band(420, 2.2f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            if (t < 0.24f)
            {
                whirr.Set(320 + 680 * (t / 0.24f), 2.2f);
                b[i] += whirr.Run(n) * (t / 0.24f) * 1.1f;
            }
        }
        // Cord whip snap at release
        Mix(b, Click(0.035f, R(r, 2800, 3800), 0.95f, r), 0.23f, 1.1f);
        Mix(b, SwingWhoosh(r, 1600, 600, 0.16f, 2.6f), 0.24f, 0.8f);
        return b;
    }

    static float[] JavelinCast(Random r)
    {
        var b = Throw(r);
        Pluck(b, (int)(0.02f * Rate), R(r, 165, 215), 0.32f, 0.975f, r, 0.5f); // ash shaft vibration
        return b;
    }

    static float[] ArrowShield(Random r)
    {
        var b = Click(0.055f, R(r, 1100, 1550), 0.5f, r);
        var board = new Band(R(r, 290, 430), 3.0f);
        var o = Buf(0.22f);
        Mix(o, b, 0, 0.9f);
        Mix(o, Thud(R(r, 165, 220), 0.15f, 0.35f, r), 0.003f, 0.85f);
        for (int i = 0; i < o.Length; i++) o[i] += board.Run(N(r)) * MathF.Exp(-T(i) * 26) * 1.1f;
        return o;
    }

    static float[] ArrowPlate(Random r)
    {
        var b = Buf(0.32f);
        float f0 = R(r, 2400, 3300);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            float glide = f0 * (1f + 0.14f * t); // ricochet whine rising slightly as it glances off steel
            b[i] = (MathF.Sin(MathF.Tau * glide * t) * 0.65f + MathF.Sin(MathF.Tau * glide * 1.63f * t) * 0.35f) * MathF.Exp(-t * 16)
                + N(r) * MathF.Exp(-t * 140) * 0.7f;
        }
        return b;
    }

    static float[] ArrowFlesh(Random r)
    {
        var b = Buf(0.18f);
        var wet = new Band(R(r, 750, 1150), 1.5f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            b[i] = wet.Run(N(r)) * MathF.Exp(-t * 38) * 1.6f;
        }
        Mix(b, Thud(R(r, 115, 155), 0.11f, 0.45f, r), 0.002f, 0.85f);
        return b;
    }

    static float[] RamSwing(Random r)
    {
        var b = Buf(0.65f);
        var creak = new Band(R(r, 420, 620), 5.5f);
        float ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            ph += (52 + 28 * MathF.Sin(t * 7)) / Rate;
            float chain = r.NextSingle() > 0.992f ? MathF.Sin(MathF.Tau * R(r, 2400, 3600) * t) * 0.45f : 0;
            b[i] = creak.Run((ph % 1) * 2 - 1) * MathF.Sin(MathF.PI * t / 0.65f) * 1.2f + chain;
        }
        return b;
    }

    static float[] LadderHook(Random r)
    {
        var b = Buf(0.48f);
        Mix(b, Thud(R(r, 130, 175), 0.18f, 0.5f, r), 0f, 0.8f);
        Mix(b, BlockMetal(r), 0.05f, 0.55f);
        Mix(b, Thud(R(r, 160, 210), 0.14f, 0.4f, r), 0.18f, 0.6f);
        return b;
    }

    static float[] FirePotShatter(Random r)
    {
        var b = Buf(0.85f);
        for (int k = 0; k < 16; k++)
            Mix(b, Click(0.03f, R(r, 1800, 4800), 0.7f, r), R(r, 0f, 0.16f), R(r, 0.3f, 0.8f));
        Mix(b, Flame(r), 0.04f, 0.9f);
        return b;
    }
}
