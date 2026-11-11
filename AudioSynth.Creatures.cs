namespace Ironfield;

/// AU2 synthesis: monster, beast and undead vocals & foley, magic traditions, captain aura moments and order horns.
static partial class Synth
{
    static float[] TrollRoar(Random r)
    {
        float len = R(r, 1.15f, 1.45f), f0 = R(r, 58, 76);
        var v = Voices(r, 3, len, f0, f0 * 1.14f, 360, 740,
            t => (t < 0.25f ? 0.85f + 0.25f * (t / 0.25f) : 1.1f - 0.35f * (t - 0.25f) / len),
            t => MathF.Min(1, t * 12) * MathF.Pow(MathF.Max(0, 1 - t / len), 1.1f) * (0.8f + 0.2f * MathF.Sin(t * 38)),
            0.04f, 0.14f);
        float lp = 0, kp = Pole(0.06f);
        for (int i = 0; i < v.Length; i++) { lp += (N(r) - lp) * kp; v[i] += lp * 1.1f * MathF.Sin(MathF.PI * T(i) / len); }
        return v;
    }

    static float[] TrollSweep(Random r)
    {
        var b = Buf(0.58f);
        Mix(b, SwingWhoosh(r, R(r, 420, 560), R(r, 130, 190), 0.36f, 1.5f), 0f, 1.25f);
        Mix(b, Thud(R(r, 38, 52), 0.42f, 0.95f, r), 0.14f, 1.1f);
        return b;
    }

    static float[] WolfHowl(Random r)
    {
        float len = R(r, 1.35f, 1.75f), top = R(r, 380, 490);
        return Voices(r, 2, len, top * 0.96f, top * 1.04f, 540, 1080,
            t => t < 0.35f ? 0.68f + 0.32f * (t / 0.35f) : 1.0f - 0.22f * MathF.Pow((t - 0.35f) / len, 1.4f),
            t => MathF.Min(1, t * 5) * MathF.Sin(MathF.PI * MathF.Min(1, t / len)),
            0.08f, 0.03f);
    }

    static float[] WolfSnarl(Random r)
    {
        float len = R(r, 0.34f, 0.48f);
        var b = Voices(r, 2, len, 95, 135, 480, 1350,
            t => 1f + 0.18f * MathF.Sin(t * 55),
            t => MathF.Sin(MathF.PI * t / len), 0.02f, 0.22f);
        Mix(b, Click(0.045f, R(r, 950, 1400), 0.7f, r), len * 0.55f, 0.75f); // jaw snap
        return b;
    }

    static float[] SpiderChitter(Random r)
    {
        var b = Buf(0.42f);
        int clicks = r.Next(8, 13);
        for (int k = 0; k < clicks; k++)
        {
            float at = k * (0.34f / clicks) + R(r, 0f, 0.015f);
            Mix(b, Click(0.024f, R(r, 2600, 4800), 0.65f, r), at, R(r, 0.45f, 0.95f));
        }
        var hiss = new Band(R(r, 3800, 5200), 3.5f);
        for (int i = 0; i < b.Length; i++) b[i] += hiss.Run(N(r)) * MathF.Sin(MathF.PI * T(i) / 0.42f) * 0.45f;
        return b;
    }

    static float[] WebSpit(Random r)
    {
        var b = Buf(0.36f);
        var jet = new Band(2800, 2.6f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            jet.Set(3400 - 5200 * MathF.Min(t, 0.28f), 2.6f);
            b[i] = jet.Run(N(r)) * MathF.Exp(-t * 11) * 1.6f;
        }
        Mix(b, Squelch(r), 0.08f, 0.55f);
        return b;
    }

    static float[] BearRoar(Random r)
    {
        float len = R(r, 0.85f, 1.15f), f0 = R(r, 74, 98);
        return Voices(r, 3, len, f0, f0 * 1.12f, 430, 880,
            t => (1.08f - 0.28f * t / len) * (1f + 0.06f * MathF.Sin(t * 44)),
            t => MathF.Min(1, t * 18) * MathF.Pow(MathF.Max(0, 1 - t / len), 1.2f),
            0.03f, 0.18f);
    }

    static float[] WyvernScreech(Random r)
    {
        float len = R(r, 0.75f, 1.05f), f0 = R(r, 680, 920);
        var b = Voices(r, 2, len, f0, f0 * 1.06f, 1450, 2950,
            t => (1.12f - 0.38f * t / len) * (1f + 0.04f * MathF.Sin(t * 65)),
            t => MathF.Min(1, t * 22) * MathF.Pow(MathF.Max(0, 1 - t / len), 1.3f),
            0.02f, 0.12f);
        return b;
    }

    static float[] WyvernFlap(Random r)
    {
        var b = Buf(0.62f);
        Mix(b, SwingWhoosh(r, 360, 140, 0.24f, 1.4f), 0.00f, 1.1f);
        Mix(b, Click(0.04f, R(r, 520, 820), 0.8f, r), 0.09f, 0.45f); // leather snap
        Mix(b, SwingWhoosh(r, 380, 150, 0.24f, 1.4f), 0.30f, 0.9f);
        return b;
    }

    static float[] GoblinShriek(Random r)
    {
        float len = R(r, 0.35f, 0.52f), f0 = R(r, 340, 460);
        return Voices(r, 1, len, f0, f0, 920, 2150,
            t => 1.15f - 0.55f * (t / len) + 0.08f * MathF.Sin(t * 50),
            t => MathF.Min(1, t * 35) * MathF.Pow(MathF.Max(0, 1 - t / len), 1.4f),
            0f, 0.22f);
    }

    static float[] BoneHit(Random r)
    {
        var b = Buf(0.28f);
        for (int k = 0; k < 4; k++)
        {
            float at = k * R(r, 0.022f, 0.045f), f = R(r, 780, 1650);
            Mix(b, Click(0.045f, f, 0.45f, r), at, 0.85f - k * 0.15f);
        }
        Mix(b, Thud(R(r, 180, 240), 0.12f, 0.25f, r), 0f, 0.6f);
        return b;
    }

    static float[] BoneCollapse(Random r)
    {
        var b = Buf(0.68f);
        int pieces = r.Next(9, 14);
        for (int k = 0; k < pieces; k++)
        {
            float at = MathF.Pow(r.NextSingle(), 1.5f) * 0.50f;
            Mix(b, Click(R(r, 0.03f, 0.06f), R(r, 680, 2100), 0.4f, r), at, R(r, 0.35f, 0.9f) * (1 - at));
        }
        Mix(b, Thud(R(r, 130, 175), 0.18f, 0.35f, r), 0.02f, 0.55f);
        return b;
    }

    static float[] RaiseDead(Random r)
    {
        var b = Buf(1.25f);
        var moan = Voices(r, 4, 1.2f, 82, 118, 420, 880,
            t => 0.85f + 0.28f * (t / 1.2f),
            t => MathF.Sin(MathF.PI * t / 1.2f), 0.12f, 0.1f);
        Mix(b, moan, 0f, 0.75f);
        Mix(b, BoneCollapse(r), 0.15f, 0.65f);
        return b;
    }

    static float[] CastGaldr(Random r)
    {
        float len = R(r, 0.95f, 1.2f), k0 = R(r, 0.92f, 1.12f);
        var b = Buf(len);
        var wind = new Band(900, 3.5f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Sin(MathF.PI * t / len);
            wind.Set((650 + 1400 * (t / len)) * k0, 3.5f);
            float chord = MathF.Sin(MathF.Tau * 293.6f * k0 * t) * 0.35f
                + MathF.Sin(MathF.Tau * 440f * k0 * t) * 0.28f
                + MathF.Sin(MathF.Tau * 659.2f * k0 * t) * 0.20f;
            b[i] = (wind.Run(N(r)) * 1.2f + chord) * env * 0.55f;
        }
        return b;
    }

    static float[] CastHex(Random r)
    {
        float len = R(r, 0.85f, 1.1f), f0 = R(r, 310, 370);
        var b = Buf(len);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Min(1, t * 10) * MathF.Exp(-t * 2.2f);
            // Tritone + minor second shimmer
            float s = MathF.Sin(MathF.Tau * f0 * t) + 0.75f * MathF.Sin(MathF.Tau * f0 * 1.414f * t + MathF.Sin(t * 19))
                + 0.45f * MathF.Sin(MathF.Tau * f0 * 1.06f * t);
            b[i] = (s * 0.32f + N(r) * 0.08f) * env;
        }
        return b;
    }

    static float[] CastNecro(Random r)
    {
        var b = Buf(1.15f);
        float f0 = R(r, 65, 82);
        var whisper = new Band(1650, 4f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Sin(MathF.PI * t / 1.15f);
            whisper.Set(1900 - 900 * (t / 1.15f), 4f);
            b[i] = (MathF.Sin(MathF.Tau * f0 * t) * 0.55f + MathF.Sin(MathF.Tau * f0 * 3.02f * t) * 0.2f + whisper.Run(N(r)) * 0.9f) * env * 0.6f;
        }
        return b;
    }

    static float[] WardShatter(Random r)
    {
        var b = Buf(0.75f);
        float f0 = R(r, 1320, 1680);
        for (int k = 0; k < 10; k++)
        {
            float at = k * 0.028f + R(r, 0f, 0.02f), f = f0 * R(r, 0.85f, 2.3f);
            for (int i = (int)(at * Rate); i < b.Length; i++)
            {
                float t = T(i) - at;
                b[i] += MathF.Sin(MathF.Tau * f * t) * MathF.Exp(-t * 18) * 0.22f;
            }
        }
        Mix(b, Click(0.04f, 3200, 0.8f, r), 0f, 0.85f);
        return b;
    }

    static float[] CrowSwarm(Random r)
    {
        var b = Buf(1.55f);
        for (int k = 0; k < 9; k++)
        {
            float at = R(r, 0f, 1.1f), len = R(r, 0.18f, 0.28f), f0 = R(r, 520, 740);
            var caw = Voices(r, 1, len, f0, f0, 1200, 2200, t => 1.05f - 0.35f * (t / len), t => MathF.Sin(MathF.PI * t / len), 0f, 0.28f);
            Mix(b, caw, at, R(r, 0.35f, 0.75f));
            Mix(b, SwingWhoosh(r, 1200, 500, 0.12f, 2.0f), at + 0.03f, 0.35f);
        }
        return b;
    }

    static float[] AuraIgnite(Random r)
    {
        var b = Buf(0.58f);
        float f0 = R(r, 440, 523);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Min(1, t * 28) * MathF.Exp(-t * 5.5f);
            b[i] = (MathF.Sin(MathF.Tau * f0 * t) * 0.4f + MathF.Sin(MathF.Tau * f0 * 1.5f * t) * 0.3f
                + MathF.Sin(MathF.Tau * f0 * 3f * t) * 0.2f + N(r) * MathF.Exp(-t * 30) * 0.35f) * env;
        }
        return b;
    }

    static float[] AuraBreakthrough(Random r)
    {
        var b = Buf(1.85f);
        float k0 = R(r, 0.95f, 1.06f);
        Mix(b, Thud(42, 0.65f, 0.6f, r), 0f, 0.95f);
        float[] chord = { 220 * k0, 330 * k0, 440 * k0, 659.2f * k0, 880 * k0 };
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Min(1, t * 8) * MathF.Exp(-t * 1.5f), s = 0;
            for (int c = 0; c < chord.Length; c++)
                s += MathF.Sin(MathF.Tau * chord[c] * t * (1 + 0.004f * MathF.Sin(t * 5 + c))) / (c + 1.3f);
            b[i] += s * env * 0.45f;
        }
        return b;
    }

    static float[] ChampionClash(Random r)
    {
        var b = Buf(1.10f);
        Mix(b, ParrySteel(r), 0f, 1.0f);
        Mix(b, ParrySteel(r), 0.09f, 0.8f);
        Mix(b, AuraThrum(r), 0.02f, 0.75f);
        return b;
    }

    static float[] HornCall(Random r, int[] semitones, float[] lens, float baseLo = 235, float baseHi = 265)
    {
        float total = lens.Sum() * 0.88f + 0.35f, root = R(r, baseLo, baseHi), at = 0;
        var b = Buf(total);
        for (int k = 0; k < semitones.Length; k++)
        {
            Mix(b, Horn(root * MathF.Pow(2, semitones[k] / 12f), lens[k], false, false, 0.11f), at, 1f);
            at += lens[k] * 0.86f;
        }
        return b;
    }

    static float[] HornCharge(Random r) => HornCall(r, new[] { 0, 5, 12 }, new[] { 0.16f, 0.16f, 0.52f }, 260, 290);
    static float[] HornHold(Random r) => HornCall(r, new[] { 0, 7 }, new[] { 0.28f, 0.48f }, 210, 235);
    static float[] HornRetreat(Random r) => HornCall(r, new[] { 7, 3, 0 }, new[] { 0.24f, 0.24f, 0.58f }, 195, 220);

    static float[] HorseWhistle(Random r)
    {
        var b = Buf(0.55f);
        float f0 = R(r, 1850, 2150), ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            bool gap = t is > 0.18f and < 0.24f;
            float f = t < 0.18f ? f0 * (1 + 0.22f * (t / 0.18f)) : f0 * (1.28f - 0.20f * ((t - 0.24f) / 0.31f));
            ph += MathF.Tau * f / Rate;
            float env = gap ? 0f : t < 0.18f ? MathF.Sin(MathF.PI * (t / 0.18f)) : MathF.Sin(MathF.PI * MathF.Min(1, (t - 0.24f) / 0.31f));
            b[i] = (MathF.Sin(ph) * 0.75f + N(r) * 0.04f) * env;
        }
        return b;
    }
}
