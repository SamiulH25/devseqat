namespace Ironfield;

/// AU3 synthesis: 7 world/interior ambient beds (`Surf`, `River`, `Forest`, `Blizzard`, `Tavern`, `Forge`, `OmenDrone`)
/// and 11 ground, settlement, armoury, deed, scroll and creator foley cues.
static partial class Synth
{
    // ---------- AU3 ambient beds (looped) ----------

    /// Rolling surf and marsh water: slow wave swells washing up and receding, with an occasional far gull call.
    public static float[] Surf()
    {
        var r = new Random(301);
        var b = Buf(10.5f);
        float lp = 0, lp2 = 0, k1 = Pole(0.08f), k2 = Pole(0.18f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            float wave = 0.45f + 0.55f * MathF.Pow(0.5f + 0.5f * MathF.Sin(t * MathF.Tau / 5f - 0.6f), 1.8f);
            lp += (n - lp) * (k1 + k2 * wave * 0.5f);
            lp2 += (lp - lp2) * k2;
            b[i] = lp2 * wave * 2.2f;
        }
        for (int g = 0; g < 3; g++)
        {
            float at = 1.4f + g * 3.1f + R(r, -0.3f, 0.3f), len = R(r, 0.28f, 0.42f), f0 = R(r, 1550, 2100);
            var cry = Buf(len);
            float ph = 0;
            for (int i = 0; i < cry.Length; i++)
            {
                float u = T(i) / len;
                ph += MathF.Tau * f0 * (1.12f - 0.32f * u) / Rate;
                cry[i] = (MathF.Sin(ph) + 0.3f * MathF.Sin(2 * ph)) * MathF.Sin(MathF.PI * u) * 0.14f;
            }
            Mix(b, Space(cry, 0.35f, 1.1f), at, 0.5f);
        }
        return Loop(b, 0.5f);
    }

    /// Rushing river and ford rapids: steady mid-band water babble with bubbling resonant ripples.
    public static float[] River()
    {
        var r = new Random(302);
        var b = Buf(7.5f);
        var flow = new Band(680, 0.9f);
        var foam = new Band(1950, 1.4f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            b[i] = (flow.Run(n) * (0.8f + 0.2f * MathF.Sin(t * 2.3f)) + foam.Run(n) * 0.45f) * 0.85f;
        }
        for (int k = 0; k < 45; k++)
        {
            float at = R(r, 0.1f, 6.8f), len = R(r, 0.04f, 0.09f), f0 = R(r, 520, 1250);
            var blup = Buf(len);
            float ph = 0;
            for (int i = 0; i < blup.Length; i++)
            {
                float u = T(i) / len;
                ph += MathF.Tau * f0 * (0.85f + 0.45f * u) / Rate;
                blup[i] = MathF.Sin(ph) * MathF.Sin(MathF.PI * u) * 0.18f;
            }
            Mix(b, blup, at, R(r, 0.3f, 0.8f));
        }
        return Loop(b, 0.5f);
    }

    /// Woodland canopy: leafy breeze rising and falling, creaking timber boughs and soft brush.
    public static float[] Forest()
    {
        var r = new Random(303);
        var b = Buf(9.5f);
        var leaves = new Band(2400, 1.1f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), swell = 0.5f + 0.5f * MathF.Sin(t * MathF.Tau / 9f) * MathF.Cos(t * 0.9f);
            b[i] = leaves.Run(N(r)) * (0.35f + 0.65f * swell) * 0.65f;
        }
        for (int k = 0; k < 5; k++)
        {
            float at = 0.8f + k * 1.7f + R(r, -0.2f, 0.2f), len = R(r, 0.35f, 0.55f);
            var bough = Buf(len);
            var res = new Band(R(r, 340, 520), 6f);
            float ph = 0;
            for (int i = 0; i < bough.Length; i++)
            {
                float u = T(i) / len;
                ph += (45 + 25 * MathF.Sin(u * 8)) / Rate;
                bough[i] = res.Run((ph % 1) * 2 - 1) * MathF.Sin(MathF.PI * u) * 0.32f;
            }
            Mix(b, bough, at, 0.5f);
        }
        return Loop(b, 0.5f);
    }

    /// Winter blizzard: whistling high wind gusts over deep cold air and fine driven snow.
    public static float[] Blizzard()
    {
        var r = new Random(304);
        var b = Buf(8.8f);
        var howl = new Band(620, 5.5f);
        float lp = 0, kp = Pole(0.035f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            float gust = 0.5f + 0.5f * MathF.Sin(t * MathF.Tau / 8f + 0.4f);
            howl.Set(480 + 340 * gust, 5.5f);
            lp += (n - lp) * kp;
            b[i] = lp * 2.4f * (0.6f + 0.4f * gust) + howl.Run(n) * (0.35f + 0.65f * gust) * 0.9f + n * 0.04f;
        }
        return Loop(b, 0.55f);
    }

    /// Tavern interior: warm hearth crackle, low friendly crowd murmur and wooden cups clinking.
    public static float[] Tavern()
    {
        var r = new Random(305);
        var b = Voices(r, 12, 9.6f, 105, 235, 580, 1120,
            t => 1 + 0.04f * MathF.Sin(t * 0.8f),
            t => 0.65f + 0.35f * MathF.Sin(t * 1.1f) * MathF.Cos(t * 0.43f), 3.5f, 0.07f);
        float lp = 0, kp = Pole(0.09f);
        for (int i = 0; i < b.Length; i++)
        {
            float n = N(r);
            lp += (n - lp) * kp;
            float crackle = r.NextSingle() > 0.996f ? n * 0.35f : 0;
            b[i] = b[i] * 0.32f + lp * 0.45f + crackle;
        }
        for (float at = 0.6f; at < 8.8f; at += R(r, 1.1f, 2.1f))
            Mix(b, Thud(R(r, 210, 310), 0.11f, 0.25f, r), at, 0.32f); // wooden tankards
        return Loop(Space(b, 0.28f, 0.75f), 0.5f);
    }

    /// Armourer's forge: rhythmic leather bellows, coal fire roar and spaced anvil strikes.
    public static float[] Forge()
    {
        var r = new Random(306);
        var b = Buf(8.6f);
        float lp = 0, kp = Pole(0.07f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            float breath = 0.45f + 0.55f * MathF.Pow(MathF.Max(0, MathF.Sin(t * MathF.Tau / 4f)), 1.5f);
            lp += (n - lp) * kp;
            b[i] = lp * (0.9f + 1.3f * breath) + (r.NextSingle() > 0.996f ? n * 0.3f : 0);
        }
        foreach (float at in new[] { 1.1f, 1.65f, 5.1f, 5.65f, 6.2f })
        {
            var ring = AnvilRing(r);
            Mix(b, ring, at, 0.26f);
        }
        return Loop(Space(b, 0.22f, 0.7f), 0.5f);
    }

    /// Grave-fields & high Omen: eerie sub-drone, hollow wind and faint dry bone ticks.
    public static float[] OmenDrone()
    {
        var r = new Random(307);
        const float loop = 8.0f;
        var b = Buf(loop + 0.6f);
        float f1 = MathF.Round(55f * loop) / loop, f2 = MathF.Round(82.4f * loop) / loop, f3 = MathF.Round(116.5f * loop) / loop;
        var hollow = new Band(390, 4.5f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), mod = 0.7f + 0.3f * MathF.Sin(t * MathF.Tau / loop);
            float drone = MathF.Sin(MathF.Tau * f1 * t) * 0.42f + MathF.Sin(MathF.Tau * f2 * t) * 0.24f + MathF.Sin(MathF.Tau * f3 * t) * 0.14f;
            b[i] = (drone + hollow.Run(N(r)) * 0.65f) * mod * 0.55f;
        }
        for (int k = 0; k < 7; k++)
            Mix(b, Click(0.03f, R(r, 950, 1750), 0.35f, r), R(r, 0.3f, 7.6f), 0.18f);
        return Loop(b, 0.6f);
    }

    // ---------- AU3 world, campaign & UI cues ----------

    static float[] GravelStep(Random r)
    {
        var b = Buf(0.22f);
        var grit = new Band(R(r, 1600, 2600), 1.6f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            b[i] = grit.Run(n) * MathF.Exp(-t * 24) * 1.4f + (r.NextSingle() > 0.96f && t < 0.09f ? n * 0.55f : 0);
        }
        Mix(b, Thud(R(r, 95, 130), 0.10f, 0.5f, r), 0f, 0.65f);
        return b;
    }

    static float[] SnowCrunch(Random r)
    {
        float len = R(r, 0.20f, 0.28f);
        var b = Buf(len);
        var pack = new Band(R(r, 1050, 1550), 2.2f);
        var squeak = new Band(R(r, 3400, 4600), 4.0f);
        for (int i = 0; i < b.Length; i++)
        {
            float u = T(i) / len, n = N(r), env = MathF.Sin(MathF.PI * u) * MathF.Exp(-u * 1.6f);
            b[i] = (pack.Run(n) * 1.5f + squeak.Run(n) * 0.65f) * env;
        }
        return b;
    }

    static float[] RecruitCheer(Random r)
    {
        var b = Buf(0.95f);
        var v = Voices(r, 6, 0.85f, 130, 235, 700, 1200,
            t => 0.96f + 0.16f * MathF.Min(1, t * 3f),
            t => MathF.Min(1, t * 14) * MathF.Exp(-MathF.Max(0, t - 0.35f) * 4.5f), 0.06f, 0.04f);
        Mix(b, v, 0.04f, 0.85f);
        Mix(b, Thud(R(r, 75, 98), 0.18f, 0.6f, r), 0f, 0.8f); // spear-butt / boot stamp
        Mix(b, Thud(R(r, 80, 105), 0.16f, 0.5f, r), 0.12f, 0.6f);
        return b;
    }

    static float[] EquipBlade(Random r) => WeaponSwap(r);

    static float[] EquipArmor(Random r)
    {
        var b = Buf(0.38f);
        Mix(b, Thud(R(r, 95, 130), 0.20f, 0.55f, r), 0f, 0.8f);
        for (int k = 0; k < 5; k++)
            Mix(b, Click(0.028f, R(r, 2200, 3900), 0.7f, r), 0.03f + k * 0.042f, 0.45f); // mail links & buckle
        return b;
    }

    static float[] HorseTack(Random r)
    {
        var b = Buf(0.48f);
        Mix(b, Thud(R(r, 110, 145), 0.16f, 0.45f, r), 0f, 0.65f); // saddle leather
        Mix(b, Click(0.035f, R(r, 2900, 4100), 0.35f, r), 0.09f, 0.55f); // bit ring
        Mix(b, Click(0.035f, R(r, 3100, 4300), 0.35f, r), 0.16f, 0.45f);
        // Soft horse snort
        float lp = 0, kp = Pole(0.22f);
        for (int i = (int)(0.18f * Rate); i < b.Length; i++)
        {
            float u = (T(i) - 0.18f) / 0.28f;
            lp += (N(r) - lp) * kp;
            b[i] += lp * MathF.Sin(MathF.PI * MathF.Min(1, u)) * (0.6f + 0.4f * MathF.Sin(u * 45)) * 0.65f;
        }
        return b;
    }

    static float[] AnvilRing(Random r)
    {
        var b = Buf(0.62f);
        void Strike(float at, float gain)
        {
            float f = R(r, 1980, 2340);
            for (int i = (int)(at * Rate); i < b.Length; i++)
            {
                float t = T(i) - at;
                float s = MathF.Sin(MathF.Tau * f * t) + 0.52f * MathF.Sin(MathF.Tau * f * 2.41f * t) + 0.25f * MathF.Sin(MathF.Tau * f * 3.87f * t);
                b[i] += (s * MathF.Exp(-t * 13) * 0.42f + N(r) * MathF.Exp(-t * 180) * 0.55f) * gain;
            }
        }
        Strike(0f, 1.0f);
        Strike(R(r, 0.19f, 0.25f), 0.68f);
        return b;
    }

    static float[] WaxSeal(Random r)
    {
        var b = Buf(0.36f);
        Mix(b, Page(r), 0f, 0.55f);
        Mix(b, Thud(R(r, 82, 112), 0.18f, 0.35f, r), 0.12f, 0.95f); // heavy brass signet stamp into warm wax
        return b;
    }

    static float[] RiteDrum(Random r)
    {
        var b = Buf(1.10f);
        Mix(b, Thud(R(r, 56, 68), 0.55f, 0.35f, r), 0f, 0.9f);
        Mix(b, Thud(R(r, 84, 98), 0.40f, 0.30f, r), 0.28f, 0.65f);
        Mix(b, Spell(r), 0.08f, 0.55f);
        return b;
    }

    static float[] FeastToast(Random r)
    {
        var b = Buf(1.25f);
        Mix(b, Thud(R(r, 190, 250), 0.12f, 0.3f, r), 0f, 0.75f);
        Mix(b, Thud(R(r, 220, 290), 0.12f, 0.3f, r), 0.08f, 0.65f);
        var cheer = Voices(r, 10, 1.05f, 135, 280, 720, 1240,
            t => 1f + 0.18f * MathF.Min(1, t * 2.5f),
            t => MathF.Min(1, t * 8) * MathF.Exp(-MathF.Max(0, t - 0.4f) * 3.2f), 0.14f, 0.05f);
        Mix(b, cheer, 0.10f, 0.85f);
        return b;
    }

    static float[] ScrollUnroll(Random r)
    {
        float len = R(r, 0.32f, 0.42f);
        var b = Buf(len);
        var paperBand = new Band(1400, 1.6f);
        for (int i = 0; i < b.Length; i++)
        {
            float u = T(i) / len, n = N(r);
            paperBand.Set(950 + 2400 * u, 1.6f);
            b[i] = paperBand.Run(n) * MathF.Sin(MathF.PI * u) * 0.95f;
        }
        Mix(b, Thud(R(r, 165, 215), 0.09f, 0.25f, r), 0f, 0.45f); // top wooden roller tap
        Mix(b, Thud(R(r, 145, 185), 0.09f, 0.25f, r), len * 0.78f, 0.4f); // bottom roller settle
        return b;
    }
}
