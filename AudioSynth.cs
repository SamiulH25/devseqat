namespace Ironfield;

/// Every sound in the game made from sines, noise, plucked strings and formant voices (no audio files, like the
/// code-painted art). Pure maths on float buffers: no MonoGame here, so it can run off the main thread and the
/// self-test can check it (`Tests/AudioTests.cs`). Music is in AudioMusic.cs.
static partial class Synth
{
    public const int Rate = 22050;

    // ---------- tools ----------

    public static float[] Buf(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];
    static float N(Random r) => r.NextSingle() * 2 - 1;
    static float R(Random r, float a, float b) => a + r.NextSingle() * (b - a);
    static float T(int i) => i / (float)Rate;

    /// Add `src` into `dst` from `at` seconds, scaled by `gain`.
    static void Mix(float[] dst, float[] src, float at, float gain = 1)
    {
        int s = (int)(at * Rate);
        for (int i = 0; i < src.Length && s + i < dst.Length; i++) if (s + i >= 0) dst[s + i] += src[i] * gain;
    }

    /// A resonant band-pass (RBJ biquad): formants of a voice, the body of a shield, an arrow's whistle.
    struct Band
    {
        float b0, b2, a1, a2, x1, x2, y1, y2;
        public Band(float f, float q) { this = default; Set(f, q); }
        public void Set(float f, float q)
        {
            float w = MathF.Tau * Math.Clamp(f, 20, Rate * 0.45f) / Rate, al = MathF.Sin(w) / (2 * q), a0 = 1 + al;
            b0 = al / a0; b2 = -al / a0; a1 = -2 * MathF.Cos(w) / a0; a2 = (1 - al) / a0;
        }
        public float Run(float x)
        {
            float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    /// Scale so the loudest sample is `peak` (keeps every cue at a level set in one table).
    public static float[] Normalize(float[] b, float peak)
    {
        float m = 0;
        foreach (var v in b) m = MathF.Max(m, MathF.Abs(v));
        if (m < 1e-6f) return b;
        float k = peak / m;
        for (int i = 0; i < b.Length; i++) b[i] *= k;
        return b;
    }

    /// Room: a small Schroeder reverb (four damped combs, two all-passes). Returns a longer buffer: dry plus `wet` of
    /// the tail; `decay` is the seconds to fall 60 dB. Gives the field, the hall of the music, and distance their air.
    public static float[] Space(float[] b, float wet, float decay, float damp = 0.35f)
    {
        int tail = (int)(decay * Rate * 0.7f);
        var o = new float[b.Length + tail];
        int[] combs = { 558, 594, 638, 678 };
        var wetBuf = new float[o.Length];
        foreach (int d in combs)
        {
            var line = new float[d];
            float g = MathF.Pow(10, -3f * d / (decay * Rate)), lp = 0;
            for (int i = 0, j = 0; i < o.Length; i++, j = (j + 1) % d)
            {
                float y = line[j];
                lp += (y - lp) * (1 - damp);
                line[j] = (i < b.Length ? b[i] : 0) + lp * g;
                wetBuf[i] += y * 0.25f;
            }
        }
        foreach (int d in new[] { 278, 110 })
        {
            var line = new float[d];
            for (int i = 0, j = 0; i < o.Length; i++, j = (j + 1) % d)
            {
                float y = line[j], x = wetBuf[i];
                line[j] = x + y * 0.5f;
                wetBuf[i] = y - x * 0.5f;
            }
        }
        for (int i = 0; i < o.Length; i++) o[i] = (i < b.Length ? b[i] : 0) + wetBuf[i] * wet;
        return o;
    }

    /// The same sound from far off: the highs soaked up by distance and more of it heard as echo.
    public static float[] Far(float[] b)
    {
        var o = new float[b.Length];
        float lp = 0, lp2 = 0;
        for (int i = 0; i < b.Length; i++) { lp += (b[i] - lp) * 0.16f; lp2 += (lp - lp2) * 0.22f; o[i] = lp2 * 1.6f; }
        return Space(o, 0.55f, 1.4f, 0.6f);
    }

    /// Crossfade the last `seconds` into the start so a noise bed loops with no click.
    public static float[] Loop(float[] b, float seconds)
    {
        int n = (int)(seconds * Rate);
        var o = new float[b.Length - n];
        Array.Copy(b, o, o.Length);
        for (int i = 0; i < n; i++) { float k = i / (float)n; o[i] = b[i] * k + b[o.Length + i] * (1 - k); }
        return o;
    }

    /// Fold everything past `seconds` back onto the start: a loop whose ringing tails carry over the seam (drum loops).
    public static float[] Wrap(float[] b, float seconds)
    {
        int n = (int)(seconds * Rate);
        var o = new float[n];
        for (int i = 0; i < b.Length; i++) o[i % n] += b[i];
        return o;
    }

    // ---------- the cues ----------

    /// How many versions each cue has: picked at random so a fight never repeats one clang.
    public static int Versions(Cue c) => c switch { Cue.Thunder or Cue.Gather or Cue.Cheer or Cue.Crash => 3, Cue.Click or Cue.Hover => 3, _ => 4 };

    /// Cues heard in the world (positioned, given a far version); the rest are the interface's.
    public static bool InWorld(Cue c) => c is not (Cue.Click or Cue.Coin or Cue.Hover or Cue.Page or Cue.Order);

    /// Loudest sample of each cue: the one place the mix is set.
    static float Peak(Cue c) => c switch
    {
        Cue.Hover => 0.22f, Cue.Click => 0.45f, Cue.Page => 0.5f, Cue.Coin => 0.55f, Cue.Order => 0.6f,
        Cue.Death => 0.6f, Cue.Volley => 0.55f, Cue.Neigh => 0.6f, Cue.ArrowHit => 0.6f, Cue.Bow => 0.6f,
        Cue.Splash or Cue.Squelch or Cue.Rustle or Cue.Hollow => 0.55f,
        Cue.Horn or Cue.Rout => 0.5f, // a horn is all body: at a lower peak it is still the loudest thing on the field
        _ => 0.85f,
    };

    /// All versions of a cue, levelled; world cues get a breath of open-field air.
    public static float[][] Cues(Cue c)
    {
        var r = new Random(1000 + 31 * (int)c);
        var o = new float[Versions(c)][];
        for (int v = 0; v < o.Length; v++)
        {
            var b = Make(c, r, v);
            if (InWorld(c)) b = Space(b, 0.12f, 0.5f);
            o[v] = Normalize(b, Peak(c));
        }
        return o;
    }

    static float[] Make(Cue c, Random r, int v) => c switch
    {
        Cue.Clash => Clash(r),
        Cue.Block => Block(r),
        Cue.Hit => Hit(r, v),
        Cue.Bow => Bow(r),
        Cue.ArrowHit => ArrowHit(r, v),
        Cue.Impact => Impact(r),
        Cue.Shout => Shout(r, v),
        Cue.Horn => Horn(R(r, 205, 235), R(r, 1.15f, 1.5f), false, v % 2 == 1),
        Cue.Rout => Horn(R(r, 140, 160), R(r, 1.4f, 1.8f), true, false),
        Cue.Click => Click(0.035f, R(r, 1600, 2100), 0.15f, r),
        Cue.Hover => Click(0.018f, R(r, 2800, 3400), 0.05f, r),
        Cue.Coin => Coin(r),
        Cue.Splash => Splash(r),
        Cue.Squelch => Squelch(r),
        Cue.Rustle => Rustle(r),
        Cue.Hollow => Thud(R(r, 105, 130), 0.32f, 0.25f, r),
        Cue.Spell => Spell(r),
        Cue.Thunder => Thunder(r),
        Cue.Holy => Holy(r),
        Cue.Flame => Flame(r),
        Cue.Aura => AuraThrum(r),
        Cue.Gather => Gather(r),
        Cue.Death => Death(r, v),
        Cue.Neigh => Neigh(r),
        Cue.Volley => Volley(r),
        Cue.Ram => Ram(r),
        Cue.Crash => Crash(r),
        Cue.Cheer => Cheer(r),
        Cue.Order => Order(r, v),
        Cue.Page => Page(r),
        _ => Buf(0.05f),
    };

    /// Metal on metal: inharmonic partials ringing down, struck by a noise burst, with a short scrape.
    static float[] Clash(Random r)
    {
        var b = Buf(R(r, 0.38f, 0.55f));
        float k0 = R(r, 0.82f, 1.22f);
        float[] f = { 1180, 1730, 2390, 3160, 4420 }, k = { 9, 12, 15, 20, 26 };
        for (int p = 0; p < f.Length; p++) { f[p] *= k0 * R(r, 0.97f, 1.03f); k[p] *= R(r, 0.8f, 1.25f); }
        var scrape = new Band(R(r, 2500, 3800), 3);
        float scrapeLen = R(r, 0.03f, 0.09f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), s = 0;
            for (int p = 0; p < f.Length; p++) s += MathF.Sin(MathF.Tau * f[p] * t) * MathF.Exp(-k[p] * t) / (p + 1.5f);
            float n = N(r);
            b[i] = s * 0.7f + n * MathF.Exp(-t * 90) * 0.5f + scrape.Run(n) * (t < scrapeLen ? 1.2f : 1.2f * MathF.Exp(-(t - scrapeLen) * 60));
        }
        return b;
    }

    /// A blow on a wooden shield: a low knock and the hollow ring of the boards.
    static float[] Block(Random r)
    {
        var b = Thud(R(r, 125, 170), 0.28f, 0.45f, r);
        var body = new Band(R(r, 340, 520), 2.5f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            b[i] += body.Run(N(r)) * MathF.Exp(-t * 22) * 2.2f + (t < 0.004f ? N(r) * 0.6f : 0);
        }
        return b;
    }

    /// A blow that lands: a dull thwack, and on half of them the rattle of mail.
    static float[] Hit(Random r, int v)
    {
        var b = Thud(R(r, 75, 105), R(r, 0.18f, 0.26f), R(r, 0.7f, 1f), r);
        var flesh = new Band(R(r, 600, 900), 1.2f);
        bool mail = v % 2 == 1;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            b[i] += flesh.Run(n) * MathF.Exp(-t * 45) * 1.4f;
            if (mail) b[i] += n * 0.25f * MathF.Exp(-t * 30) * (MathF.Sin(t * 900) > 0.6f ? 1 : 0.2f);
        }
        return b;
    }

    /// A bow loosed: the string's twang and the arrow's swish away.
    static float[] Bow(Random r)
    {
        var b = Buf(0.4f);
        Pluck(b, 0, R(r, 150, 205), 0.6f, R(r, 0.982f, 0.99f), r);
        var swish = new Band(2000, 2.5f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            swish.Set(2400 - 3600 * MathF.Min(t, 0.4f), 2.5f);
            b[i] += swish.Run(N(r)) * MathF.Sin(MathF.PI * MathF.Min(1, t / 0.3f)) * 0.9f;
        }
        return b;
    }

    /// An arrow striking: a sharp click and a woody or fleshy thunk.
    static float[] ArrowHit(Random r, int v)
    {
        var b = Click(0.08f, R(r, 800, 1200), 0.6f, r);
        var thunk = Thud(v % 2 == 0 ? R(r, 200, 260) : R(r, 140, 180), 0.12f, 0.5f, r);
        Mix(b, thunk, 0.003f, 0.8f);
        return b;
    }

    /// A charge going home, a great blow: a deep thump and a rumble of bodies and earth.
    static float[] Impact(Random r)
    {
        var b = Thud(R(r, 48, 70), 0.42f, 1f, r);
        float lp = 0;
        for (int i = 0; i < b.Length; i++) { float t = T(i); lp += (N(r) - lp) * 0.06f; b[i] += lp * 1.6f * MathF.Exp(-t * 9); }
        return b;
    }

    /// A dull blow: a falling low tone and a short filtered noise (shields, bodies, hooves, drums).
    static float[] Thud(float freq, float len, float noise, Random r)
    {
        var b = Buf(len);
        float lp = 0, ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            ph += MathF.Tau * freq * (1 - t * 1.2f) / Rate;
            lp += (N(r) - lp) * 0.15f;
            b[i] = (MathF.Sin(ph) * 0.8f + lp * noise * 1.5f) * MathF.Exp(-t * 18);
        }
        return b;
    }

    static float[] Click(float len, float freq, float noise, Random r)
    {
        var b = Buf(len);
        for (int i = 0; i < b.Length; i++) { float t = T(i); b[i] = (MathF.Sin(MathF.Tau * freq * t) * 0.6f + N(r) * noise) * MathF.Exp(-t * 60); }
        return b;
    }

    /// A plucked string (Karplus-Strong): bows, the lute and harp of the map's music.
    static void Pluck(float[] b, int start, float freq, float amp, float decay, Random r, float bright = 1)
    {
        int n = Math.Max(2, (int)(Rate / freq));
        var ring = new float[n];
        float lp = 0;
        for (int i = 0; i < n; i++) { lp += (N(r) - lp) * bright; ring[i] = lp; }
        for (int i = 0, j = 0; start + i < b.Length && i < Rate * 3; i++, j = (j + 1) % n)
        {
            float next = ring[(j + 1) % n];
            ring[j] = (ring[j] + next) * 0.5f * decay;
            if (start + i >= 0) b[start + i] += ring[j] * amp;
        }
    }

    /// Throats: glottal buzz from `count` voices (each its own pitch, start and wobble) through two vowel formants.
    /// `pitch(t)` bends them all; `env(t)` shapes the whole. Shouts, cheers, cries and the murmur of a crowd.
    static float[] Voices(Random r, int count, float len, float lo, float hi, float f1, float f2, Func<float, float> pitch, Func<float, float> env, float spread, float rough = 0.02f)
    {
        var b = Buf(len);
        var f = new float[count]; var ph = new float[count]; var at = new float[count]; var wob = new float[count]; var jit = new float[count];
        for (int v = 0; v < count; v++) { f[v] = R(r, lo, hi); at[v] = R(r, 0, spread); wob[v] = R(r, 3, 7); }
        var a = new Band(f1, 4); var c = new Band(f2, 5);
        float lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), s = 0, bend = pitch(t);
            for (int v = 0; v < count; v++)
            {
                if (t < at[v]) continue;
                jit[v] += (N(r) * rough - jit[v]) * 0.01f;
                ph[v] += f[v] * bend * (1 + 0.03f * MathF.Sin((t - at[v]) * wob[v] + v) + jit[v]) / Rate;
                float k = MathF.Min(1, (t - at[v]) * 14);
                s += (1 - 2 * (ph[v] % 1)) * k;
            }
            s /= MathF.Sqrt(count);
            lp += (s - lp) * 0.25f;
            b[i] = (a.Run(s) * 1.1f + c.Run(s) * 0.7f + lp * 0.12f + N(r) * 0.03f) * env(t);
        }
        return b;
    }

    static readonly (float F1, float F2)[] Vowels = { (750, 1150), (520, 900), (600, 1700), (680, 1300) }; // ah, oh, eh, uh

    /// Many throats roaring together ("charge!"); some versions are two shouts.
    static float[] Shout(Random r, int v)
    {
        var (f1, f2) = Vowels[v % 2 == 0 ? 0 : 3];
        float len = v == 3 ? 1.3f : R(r, 0.9f, 1.1f);
        return Voices(r, 8, len, 135, 260, f1, f2, t => 1 + 0.15f * t,
            t => MathF.Min(1, t * 8) * (v == 3 ? 0.6f + 0.4f * MathF.Abs(MathF.Sin(t * MathF.PI / 0.65f)) : 1) * MathF.Exp(-MathF.Max(0, t - len * 0.5f) * 5), 0.12f);
    }

    /// A man falling: a strained cry bending down, or a short grunt.
    static float[] Death(Random r, int v)
    {
        var (f1, f2) = Vowels[v];
        bool grunt = v == 3;
        float len = grunt ? R(r, 0.22f, 0.3f) : R(r, 0.4f, 0.7f), top = grunt ? R(r, 110, 150) : R(r, 190, 290);
        return Voices(r, 1, len, top, top, f1, f2, t => 1 - 0.4f * t / len + (t < 0.05f ? 0.15f : 0),
            t => MathF.Min(1, t * 40) * MathF.Pow(1 - t / len, 1.5f) * (0.8f + 0.2f * MathF.Sin(t * 70)), 0, 0.3f);
    }

    /// A horse's whinny: a high bray, a fast shaking fall, and a snort.
    static float[] Neigh(Random r)
    {
        float len = R(r, 0.9f, 1.2f), top = R(r, 780, 980), shake = R(r, 9, 13);
        var b = Voices(r, 1, len, top, top, 820, 1900,
            t => (t < 0.15f ? 0.6f + 0.4f * t / 0.15f : 1 - 0.5f * (t - 0.15f) / len) * (1 + 0.12f * MathF.Sin(t * MathF.Tau * shake) * MathF.Min(1, t * 4)),
            t => MathF.Min(1, t * 20) * MathF.Max(0, 1 - t / len) * (0.7f + 0.3f * MathF.Sin(t * MathF.Tau * shake)), 0, 0.1f);
        var o = new float[b.Length + Rate / 4];
        Array.Copy(b, o, b.Length);
        float lp = 0;
        for (int i = 0; i < Rate / 4; i++) { float t = T(i); lp += (N(r) - lp) * 0.2f; o[b.Length - Rate / 10 + i] += lp * 0.8f * MathF.Sin(MathF.PI * t / 0.25f); }
        return o;
    }

    /// A volley overhead: a dozen arrows whistling past, each rising and falling as it goes by, a few thuds after.
    static float[] Volley(Random r)
    {
        var b = Buf(1.7f);
        int n = r.Next(10, 16);
        for (int k = 0; k < n; k++)
        {
            float at = R(r, 0, 0.55f), len = R(r, 0.45f, 0.8f), f0 = R(r, 1800, 3200);
            var w = new Band(f0, 9);
            int s = (int)(at * Rate);
            for (int i = 0; i < len * Rate && s + i < b.Length; i++)
            {
                float t = T(i) / len;
                w.Set(f0 * (1.18f - 0.36f * t), 9);
                b[s + i] += w.Run(N(r)) * MathF.Pow(MathF.Sin(MathF.PI * t), 2) * 1.6f;
            }
        }
        for (int k = 0; k < 4; k++) Mix(b, Click(0.06f, R(r, 700, 1000), 0.5f, r), R(r, 0.9f, 1.5f), R(r, 0.1f, 0.25f));
        return b;
    }

    /// The ram on the gate: a deep wooden boom, the creak of the beam and the iron bands rattling.
    static float[] Ram(Random r)
    {
        var b = Thud(R(r, 38, 50), 0.9f, 0.6f, r);
        var wood = new Band(R(r, 180, 260), 3);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            b[i] += wood.Run(n) * MathF.Exp(-t * 7) * 2.5f + n * 0.15f * MathF.Exp(-t * 25) * (MathF.Sin(t * 400) > 0.8f ? 1 : 0);
        }
        return b;
    }

    /// The gate giving way: a boom and a shower of splintering, cracking timber.
    static float[] Crash(Random r)
    {
        var b = Thud(R(r, 40, 55), 1.0f, 1f, r);
        for (int k = 0; k < 70; k++)
        {
            float at = MathF.Pow(r.NextSingle(), 2) * 1.4f, len = R(r, 0.01f, 0.05f);
            var c = new Band(R(r, 900, 4200), 3);
            var s = Buf(len);
            for (int i = 0; i < s.Length; i++) s[i] = c.Run(N(r)) * MathF.Exp(-T(i) / len * 4) * 3;
            Mix(b, s, at, R(r, 0.2f, 0.7f) * (1 - at / 1.6f));
        }
        float lp = 0;
        for (int i = 0; i < b.Length; i++) { lp += (N(r) - lp) * 0.04f; b[i] += lp * 2 * MathF.Exp(-T(i) * 3); }
        return b;
    }

    /// A cheer: a crowd of voices swelling into "hurrah", rising in pitch.
    static float[] Cheer(Random r)
    {
        float len = R(r, 2f, 2.5f);
        return Voices(r, 22, len, 150, 320, 720, 1250, t => 1 + 0.22f * MathF.Min(1, t * 2.5f),
            t => MathF.Min(1, t * 3) * (0.75f + 0.25f * MathF.Sin(t * 5)) * MathF.Max(0, 1 - MathF.Pow(t / len, 3)), 0.35f, 0.05f);
    }

    /// A horn call for an order: two or three quick notes.
    static float[] Order(Random r, int v)
    {
        int[][] calls = { new[] { 0, 7 }, new[] { 0, 7, 12 }, new[] { 0, 5, 7 }, new[] { 7, 0, 7 } };
        var b = Buf(1.0f);
        float at = 0, root = R(r, 280, 300);
        var call = calls[v % calls.Length];
        for (int k = 0; k < call.Length; k++)
        {
            float len = k == call.Length - 1 ? 0.4f : 0.16f;
            Mix(b, Horn(root * MathF.Pow(2, call[k] / 12f), len, false, false, 0.12f), at, 1);
            at += len * 0.85f;
        }
        return b;
    }

    /// A war horn: a soft-attack sawtooth with vibrato; `falling` bends down (the rout horn); `fifth` adds a second note.
    public static float[] Horn(float freq, float len, bool falling, bool fifth, float bright = 0.06f)
    {
        var b = Buf(len + (fifth ? 0.7f : 0));
        float ph = 0, ph2 = 0, lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), up = fifth && t > len * 0.45f ? 1.5f : 1;
            float f = freq * up * (falling ? 1 - 0.15f * t / len : 1) * (1 + 0.008f * MathF.Sin(t * 30) * MathF.Min(1, t * 3));
            ph += f / Rate; ph2 += f * 2 / Rate;
            float s = (ph % 1) * 2 - 1 + ((ph2 % 1) * 2 - 1) * 0.3f;
            lp += (s - lp) * (bright + 0.04f * MathF.Min(1, t * 4));
            float env = MathF.Min(1, t * 6) * MathF.Min(1, (b.Length / (float)Rate - t) * 4);
            b[i] = lp * env * 0.6f;
        }
        return b;
    }

    /// Coins: a few small metal pings.
    static float[] Coin(Random r)
    {
        var b = Buf(0.4f);
        int n = r.Next(2, 5);
        for (int k = 0; k < n; k++)
        {
            float at = k == 0 ? 0 : R(r, 0.03f, 0.18f), f = R(r, 2400, 4200);
            for (int i = (int)(at * Rate); i < b.Length; i++) { float t = T(i) - at; b[i] += (MathF.Sin(MathF.Tau * f * t) + 0.3f * MathF.Sin(MathF.Tau * f * 2.76f * t)) * MathF.Exp(-t * 22) * 0.4f; }
        }
        return b;
    }

    /// A page turned or a panel opened: a soft parchment swish.
    static float[] Page(Random r)
    {
        float len = R(r, 0.2f, 0.3f);
        var b = Buf(len);
        var w = new Band(1500, 1.5f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i) / len;
            w.Set(1200 + 2200 * t, 1.5f);
            float n = N(r);
            b[i] = w.Run(n) * MathF.Sin(MathF.PI * t) + (r.NextSingle() > 0.997f ? n * 0.5f : 0);
        }
        return b;
    }

    /// Feet in water: a bright noise burst that falls away, with a second slap.
    static float[] Splash(Random r)
    {
        var b = Buf(0.4f);
        float lp = 0, k = R(r, 0.3f, 0.55f), second = R(r, 0.08f, 0.16f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            lp += (n - lp) * k;
            b[i] = (n - lp) * 0.7f * (MathF.Exp(-t * 14) + 0.6f * MathF.Exp(-MathF.Abs(t - second) * 40));
        }
        return b;
    }

    /// A boot pulled out of mud: a low sucking sweep and dull noise.
    static float[] Squelch(Random r)
    {
        float len = R(r, 0.18f, 0.26f), f0 = R(r, 220, 300);
        var b = Buf(len);
        float lp = 0, ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            ph += MathF.Tau * (f0 - 900 * t) / Rate;
            lp += (N(r) - lp) * 0.08f;
            b[i] = (MathF.Sin(ph) * 0.35f + lp * 1.6f) * MathF.Sin(MathF.PI * t / len);
        }
        return b;
    }

    /// Men pushing through branches: soft leafy noise with a few snapping twigs.
    static float[] Rustle(Random r)
    {
        var b = Buf(0.45f);
        float lp = 0, a = R(r, 0.04f, 0.15f), c = R(r, 0.22f, 0.35f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), n = N(r);
            lp += (n - lp) * 0.3f;
            float twig = MathF.Exp(-MathF.Abs(t - a) * 300) + MathF.Exp(-MathF.Abs(t - c) * 260) * 0.7f;
            b[i] = (n - lp) * 0.35f * MathF.Sin(MathF.PI * t / 0.45f) + n * twig * 0.8f;
        }
        return b;
    }

    /// A spell: a rising shimmer of three soft tones over a breath of noise.
    static float[] Spell(Random r)
    {
        float k = R(r, 0.85f, 1.2f), len = R(r, 0.8f, 1f);
        var b = Buf(len);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Sin(MathF.PI * t / len), rise = k * (1 + 0.4f * t);
            b[i] = env * (0.18f * MathF.Sin(MathF.Tau * 440 * rise * t) + 0.12f * MathF.Sin(MathF.Tau * 660 * rise * t + 0.3f * MathF.Sin(t * 40))
                + 0.08f * MathF.Sin(MathF.Tau * 990 * rise * t) + 0.05f * N(r));
        }
        return b;
    }

    /// Thunder after lightning: a sharp crack, then a long low rumble of filtered noise that rolls.
    static float[] Thunder(Random r)
    {
        float len = R(r, 2f, 2.6f), roll = R(r, 9, 16);
        var b = Buf(len);
        float lp = 0, lp2 = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            lp += (N(r) - lp) * 0.04f;
            lp2 += (lp - lp2) * 0.03f;
            float crack = N(r) * MathF.Exp(-t * 30) * 0.8f;
            float rumble = lp2 * 4f * MathF.Min(1, t * 6) * MathF.Exp(-t * 1.4f) * (0.7f + 0.3f * MathF.Sin(t * roll));
            b[i] = crack + rumble;
        }
        return b;
    }

    /// A holy chord (smite, miracle): bright voices swelling and fading.
    static float[] Holy(Random r)
    {
        var b = Buf(1.6f);
        float k = R(r, 0.9f, 1.12f);
        float[] f = { 523 * k, 659 * k, 784 * k, 1046 * k };
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Min(1, t * 4) * MathF.Exp(-t * 1.6f), s = 0;
            for (int v = 0; v < f.Length; v++) s += MathF.Sin(MathF.Tau * f[v] * t + 0.4f * MathF.Sin(t * 6 + v)) / (v + 1.5f);
            b[i] = s * env * 0.35f;
        }
        return b;
    }

    /// Fire catching: a soft whoosh of noise rising and falling, with crackles.
    static float[] Flame(Random r)
    {
        float len = R(r, 0.9f, 1.3f);
        var b = Buf(len);
        float lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            lp += (N(r) - lp) * (0.05f + 0.15f * MathF.Min(1, t * 3));
            float crackle = r.NextSingle() > 0.995f ? N(r) * 0.6f : 0;
            b[i] = (lp * 1.2f + crackle) * MathF.Sin(MathF.PI * MathF.Min(1, t / len));
        }
        return b;
    }

    /// The aura let loose (a burst, a howl, a great form, a breakthrough): a deep thrum with a ringing edge.
    static float[] AuraThrum(Random r)
    {
        float k = R(r, 0.9f, 1.12f);
        var b = Buf(1.3f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Min(1, t * 12) * MathF.Exp(-t * 2.2f);
            b[i] = env * (0.45f * MathF.Sin(MathF.Tau * 55 * k * t + 2 * MathF.Sin(MathF.Tau * 2 * t)) + 0.2f * MathF.Sin(MathF.Tau * 110 * k * t)
                + 0.08f * MathF.Sin(MathF.Tau * 880 * k * t * (1 + 0.02f * t)) * MathF.Exp(-t * 4));
        }
        return b;
    }

    /// A great working gathering (docs/SPECTACLE.md S2): a low drone and a rising hum that swell for two seconds, then
    /// stop dead as it lands.
    static float[] Gather(Random r)
    {
        float k = R(r, 0.92f, 1.1f);
        var b = Buf(2.4f);
        float lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i), env = MathF.Pow(MathF.Min(1, t / 2.2f), 2.2f) * (t > 2.25f ? MathF.Max(0, 1 - (t - 2.25f) / 0.12f) : 1);
            float rise = (110 + 220 * t / 2.4f) * k;
            lp += (N(r) - lp) * 0.02f;
            b[i] = (MathF.Sin(MathF.Tau * 55 * k * t) * 0.5f + MathF.Sin(MathF.Tau * 82.5f * k * t) * 0.3f + MathF.Sin(MathF.Tau * rise * t) * 0.18f + lp * 1.4f) * env * 0.5f;
        }
        return b;
    }

    // ---------- ambient beds (looped) ----------

    /// Wind: slowly breathing filtered noise.
    public static float[] Wind()
    {
        var r = new Random(2);
        var b = Buf(8.5f);
        float lp = 0, lp2 = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            lp += (N(r) - lp) * 0.02f;
            lp2 += (lp - lp2) * 0.05f;
            b[i] = lp2 * 4 * (0.6f + 0.4f * MathF.Sin(t * MathF.Tau / 8 * 2) * MathF.Sin(t * 1.3f));
        }
        return Loop(b, 0.5f);
    }

    /// Horses at the gallop: several horses' four-beat hooves, each with its own stride, over two seconds.
    public static float[] Gallop()
    {
        var r = new Random(3);
        var b = Buf(3.6f);
        for (int h = 0; h < 4; h++)
        {
            float stride = 0.6f, off = R(r, 0, 0.6f);
            for (float s = off; s < off + 2.39f; s += stride)
                foreach (float at in new[] { 0f, 0.09f, 0.2f, 0.27f })
                    Mix(b, Thud(R(r, 65, 95), 0.12f, 0.9f, r), s + at + R(r, -0.01f, 0.01f), 0.5f);
        }
        return Wrap(b, 2.4f);
    }

    /// The din of melee: a murmur with scattered clashes, thuds and grunts.
    public static float[] Din()
    {
        var r = new Random(4);
        var m = Buf(6.5f);
        float lp = 0;
        for (int i = 0; i < m.Length; i++) { lp += (N(r) - lp) * 0.04f; m[i] = lp * 0.8f; }
        m = Loop(m, 0.5f);
        var b = Buf(7f);
        for (int k = 0; k < 34; k++)
        {
            var src = (k % 4) switch { 0 => Clash(r), 1 => Block(r), 2 => Hit(r, k), _ => Death(r, 3) };
            Mix(b, src, R(r, 0, 6f), R(r, 0.12f, 0.35f));
        }
        b = Wrap(b, 6f);
        for (int i = 0; i < b.Length; i++) b[i] += m[i];
        return b;
    }

    /// A distant battle: a deep rumble of many voices with far-off clashes, softened by distance (low-passed).
    public static float[] Roar()
    {
        var r = new Random(5);
        var voices = Voices(r, 30, 8.4f, 110, 240, 600, 1000, t => 1, t => 0.7f + 0.15f * MathF.Sin(t * 1.7f) + 0.15f * MathF.Sin(t * 0.63f + 1), 0.5f, 0.06f);
        var b = Buf(8.4f);
        float lp = 0, lp2 = 0, v = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            lp += (N(r) - lp) * 0.012f;
            lp2 += (lp - lp2) * 0.03f;
            v += (voices[i] - v) * 0.1f;
            b[i] = lp2 * 5 * (0.7f + 0.15f * MathF.Sin(t * 1.7f) + 0.15f * MathF.Sin(t * 0.63f + 1)) + v * 0.5f;
        }
        for (int k = 0; k < 50; k++)
        {
            var clash = Clash(r);
            int s = r.Next(b.Length - clash.Length);
            float g = R(r, 0.03f, 0.08f), f = 0;
            for (int i = 0; i < clash.Length; i++) { f += (clash[i] - f) * 0.15f; b[s + i] += f * g; } // muffled by distance
        }
        return Loop(b, 0.4f);
    }

    /// Foot on the march: the tramp of a dozen men out of step, gravel under boots and the jingle of kit.
    public static float[] March()
    {
        var r = new Random(6);
        var b = Buf(5.4f);
        for (int m = 0; m < 12; m++)
        {
            float pace = 0.55f + R(r, -0.02f, 0.02f), off = R(r, 0, pace);
            pace = 4.4f / MathF.Round(4.4f / pace); // whole steps in the loop
            for (float s = off; s < off + 4.39f; s += pace)
            {
                float at = s + R(r, -0.015f, 0.015f);
                Mix(b, Thud(R(r, 70, 110), 0.1f, 1.2f, r), at, 0.35f);
                Mix(b, Click(0.05f, R(r, 2500, 4000), 0.9f, r), at + 0.01f, 0.08f);
            }
        }
        return Wrap(b, 4.4f);
    }

    /// Rain: a hiss of fine drops, with a few heavier ones ticking near.
    public static float[] Rain()
    {
        var r = new Random(7);
        var b = Buf(6.5f);
        float lp = 0;
        for (int i = 0; i < b.Length; i++) { float n = N(r); lp += (n - lp) * 0.5f; b[i] = (n - lp) * 0.5f; }
        for (int k = 0; k < 120; k++) Mix(b, Click(0.02f, R(r, 3000, 6000), 0.4f, r), R(r, 0, 6.4f), R(r, 0.05f, 0.2f));
        return Loop(b, 0.5f);
    }

    /// Birdsong for the map by day: chirps, quick trills and a far two-note call, with gaps of quiet.
    public static float[] Birds()
    {
        var r = new Random(8);
        var b = Buf(14f);
        for (int k = 0; k < 22; k++)
        {
            float at = R(r, 0, 13), kind = r.NextSingle();
            int notes = kind < 0.5f ? r.Next(1, 4) : kind < 0.85f ? r.Next(5, 10) : 2;
            float f0 = R(r, 2600, 5200), gap = kind < 0.85f ? R(r, 0.05f, 0.12f) : 0.35f, gain = R(r, 0.15f, 0.5f);
            for (int n = 0; n < notes; n++)
            {
                float len = kind < 0.85f ? R(r, 0.03f, 0.08f) : 0.25f, sweep = kind < 0.85f ? R(r, -0.4f, 0.5f) : 0, f = kind < 0.85f ? f0 : f0 * 0.4f * (n == 0 ? 1.19f : 1);
                var s = Buf(len);
                float ph = 0;
                for (int i = 0; i < s.Length; i++) { float t = T(i) / len; ph += MathF.Tau * f * (1 + sweep * t) / Rate; s[i] = MathF.Sin(ph) * MathF.Sin(MathF.PI * t); }
                Mix(b, s, at + n * gap, gain);
            }
        }
        return Loop(Space(b, 0.2f, 0.8f), 0.6f);
    }

    /// Crickets for the map by night: several crickets chirping in pulses at their own pace.
    public static float[] Crickets()
    {
        var r = new Random(9);
        var b = Buf(8f);
        for (int c = 0; c < 4; c++)
        {
            float f = R(r, 4000, 5200), rate = R(r, 0.6f, 1.1f), gain = R(r, 0.2f, 0.5f);
            for (float at = R(r, 0, 1); at < 7.5f; at += rate * R(r, 0.9f, 1.1f))
                for (int p = 0; p < 3; p++)
                {
                    var s = Buf(0.025f);
                    for (int i = 0; i < s.Length; i++) s[i] = MathF.Sin(MathF.Tau * f * T(i)) * MathF.Sin(MathF.PI * T(i) / 0.025f);
                    Mix(b, s, at + p * 0.04f, gain);
                }
        }
        return Loop(b, 0.4f);
    }

    /// A town nearby: the murmur of a market, a smith's hammer, a cart's creak.
    public static float[] Town()
    {
        var r = new Random(10);
        var b = Voices(r, 14, 10.5f, 120, 260, 650, 1200, t => 1 + 0.05f * MathF.Sin(t * 0.7f), t => 0.6f + 0.4f * MathF.Sin(t * 0.9f) * MathF.Sin(t * 0.37f), 4, 0.08f);
        for (int i = 0; i < b.Length; i++) b[i] *= 0.35f;
        for (float at = 0.4f; at < 9.5f; at += R(r, 0.8f, 2.2f))
        {
            var ring = Buf(0.35f);
            float f = R(r, 1900, 2300);
            for (int i = 0; i < ring.Length; i++) { float t = T(i); ring[i] = (MathF.Sin(MathF.Tau * f * t) + 0.5f * MathF.Sin(MathF.Tau * f * 2.4f * t)) * MathF.Exp(-t * 14) + N(r) * MathF.Exp(-t * 200); }
            Mix(b, ring, at, 0.12f);
        }
        for (int k = 0; k < 3; k++)
        {
            float at = R(r, 0.5f, 9), len = R(r, 0.3f, 0.6f);
            var creak = Buf(len);
            var w = new Band(500, 6);
            float ph = 0;
            for (int i = 0; i < creak.Length; i++) { float t = T(i); ph += (60 + 40 * MathF.Sin(t * 9)) / Rate; creak[i] = w.Run((ph % 1) * 2 - 1) * MathF.Sin(MathF.PI * t / len); }
            Mix(b, creak, at, 0.4f);
        }
        return Loop(Space(b, 0.25f, 0.9f), 0.5f);
    }
}
