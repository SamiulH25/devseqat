namespace Ironfield;

/// The music, composed in code: instruments (lute, harp, flute, fiddle, horns, drums) and short tunes built from
/// modes with a seeded phrase maker (A A' B A'' so a theme has a shape you can hum). Each piece is a float buffer.
static partial class Synth
{
    /// A piece of music by name. Map themes play once each, in turn, with quiet between; the battle's two layers loop
    /// together and the second rises with the fighting; victory and defeat play once.
    public static readonly string[] Pieces = { "map0", "deploy", "battle", "battle2", "victory", "defeat", "map1", "map2" };

    public static float[] Piece(string id) => id switch
    {
        "map0" => Roads(),
        "map1" => Hearth(),
        "map2" => Banners(),
        "deploy" => Deploy(),
        "battle" => BattleBase(),
        "battle2" => BattleHigh(),
        "victory" => Victory(),
        "defeat" => Defeat(),
        _ => Buf(1),
    };

    // ---------- notes and instruments ----------

    static float Hz(int midi) => 440 * MathF.Pow(2, (midi - 69) / 12f);

    static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 }, Aeolian = { 0, 2, 3, 5, 7, 8, 10 }, Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };

    /// A scale degree (0 = the root, 7 = an octave up, negative below) as a MIDI note.
    static int Deg(int root, int[] mode, int d) => root + mode[((d % 7) + 7) % 7] + 12 * (int)MathF.Floor(d / 7f);

    /// A frequency nudged to a whole number of cycles in `loop` seconds, so a held drone loops with no click.
    static float Fit(float f, float loop) => MathF.Round(f * loop) / loop;

    /// Flute: a breathy sine with a little second harmonic and vibrato that comes in as the note is held.
    static void Flute(float[] b, float at, float f, float len, float amp, Random r)
    {
        int s = (int)(at * Rate);
        float ph = 0, lp = 0;
        for (int i = 0; i < (len + 0.15f) * Rate && s + i < b.Length; i++)
        {
            float t = T(i), vib = 1 + 0.006f * MathF.Sin(t * MathF.Tau * 5) * MathF.Min(1, MathF.Max(0, t - 0.15f) * 3);
            ph += MathF.Tau * f * vib / Rate;
            lp += (N(r) - lp) * 0.3f;
            float env = MathF.Min(1, t / 0.06f) * (t > len ? MathF.Max(0, 1 - (t - len) / 0.15f) : 1);
            b[s + i] += (MathF.Sin(ph) + 0.22f * MathF.Sin(2 * ph) + 0.06f * MathF.Sin(3 * ph) + lp * 0.08f) * env * amp;
        }
    }

    /// Fiddle: a sawtooth through a soft filter, bowed in and out, with a singer's vibrato.
    static void Fiddle(float[] b, float at, float f, float len, float amp)
    {
        int s = (int)(at * Rate);
        float ph = 0, lp = 0, lp2 = 0;
        for (int i = 0; i < (len + 0.12f) * Rate && s + i < b.Length; i++)
        {
            float t = T(i);
            ph += f * (1 + 0.007f * MathF.Sin(t * MathF.Tau * 6) * MathF.Min(1, t * 4)) / Rate;
            float saw = 1 - 2 * (ph % 1);
            lp += (saw - lp) * 0.2f; lp2 += (lp - lp2) * 0.35f;
            float env = MathF.Min(1, t / 0.07f) * (t > len ? MathF.Max(0, 1 - (t - len) / 0.12f) : 1);
            b[s + i] += lp2 * env * amp;
        }
    }

    /// A held drone of a root and its fifth, swelling slowly (fitted to the loop when there is one).
    static void Drone(float[] b, float f, float amp, float loop = 0)
    {
        float f1 = loop > 0 ? Fit(f, loop) : f, f2 = loop > 0 ? Fit(f * 1.5f, loop) : f * 1.5f, sw = loop > 0 ? MathF.Tau / loop * 2 : 0.4f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = T(i);
            b[i] += (MathF.Sin(MathF.Tau * f1 * t) + 0.5f * MathF.Sin(MathF.Tau * f2 * t) + 0.2f * MathF.Sin(MathF.Tau * f1 * 2 * t)) * amp * (0.8f + 0.2f * MathF.Sin(t * sw));
        }
    }

    /// Drums: a big war drum (low), a frame drum (higher, with a slap), a rattle of snares or a tambourine's jingle.
    static void Drum(float[] b, float at, float f, float amp, Random r, float len = 0.45f) => Mix(b, Thud(f, len, 0.5f, r), at, amp);

    static void Snare(float[] b, float at, float amp, Random r, float len = 0.12f)
    {
        var s = Buf(len);
        var w = new Band(2600, 0.8f);
        for (int i = 0; i < s.Length; i++) { float t = T(i); s[i] = (w.Run(N(r)) * 1.5f + MathF.Sin(MathF.Tau * 190 * t) * 0.4f) * MathF.Exp(-t / len * 5); }
        Mix(b, s, at, amp);
    }

    static void Jingle(float[] b, float at, float amp, Random r)
    {
        var s = Buf(0.12f);
        var w = new Band(7000, 2);
        for (int i = 0; i < s.Length; i++) s[i] = w.Run(N(r)) * MathF.Exp(-T(i) * 30) * (1 + 0.5f * MathF.Sin(T(i) * 700));
        Mix(b, s, at, amp);
    }

    static void Cymbal(float[] b, float at, float amp, Random r)
    {
        var s = Buf(2.2f);
        float lp = 0;
        for (int i = 0; i < s.Length; i++) { float n = N(r); lp += (n - lp) * 0.6f; s[i] = (n - lp) * MathF.Exp(-T(i) * 1.8f); }
        Mix(b, s, at, amp);
    }

    // ---------- tunes ----------

    static readonly float[][] Rhythms =
    {
        new[] { 1f, 1, 1, 1 }, new[] { 1.5f, 0.5f, 1, 1 }, new[] { 2f, 1, 1 }, new[] { 0.5f, 0.5f, 1, 2 },
        new[] { 1f, 0.5f, 0.5f, 2 }, new[] { 1f, 1, 2 }, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 2 },
    };
    static readonly float[][] Waltz = { new[] { 1f, 1, 1 }, new[] { 2f, 1 }, new[] { 1.5f, 0.5f, 1 }, new[] { 3f }, new[] { 1f, 0.5f, 0.5f, 1 } };

    /// A melody of `bars` bars (degrees and lengths in beats), shaped A A' B A'': the same four-bar idea, an answer
    /// that ends elsewhere, a higher middle, and the idea again landing home on the root.
    static List<(float Beat, int Deg, float Len)> Tune(Random r, int beatsPerBar, float[][] rhythms, int low = 0, int high = 9)
    {
        var idea = new List<(float, int, float)>();
        int d = r.Next(2, 5);
        for (int bar = 0; bar < 4; bar++)
        {
            var rh = rhythms[r.Next(rhythms.Length)];
            float beat = bar * beatsPerBar;
            foreach (var len in rh)
            {
                int step = r.Next(10) switch { < 3 => -1, < 6 => 1, 6 => -2, 7 => 2, 8 => 0, _ => r.Next(2) == 0 ? -3 : 3 };
                d = Math.Clamp(d + step, low, high);
                idea.Add((beat, d, len));
                beat += len;
            }
        }
        var tune = new List<(float, int, float)>();
        void Phrase(int part, int shift, int end)
        {
            for (int k = 0; k < idea.Count; k++)
            {
                var (b, g, l) = idea[k];
                bool last = k == idea.Count - 1;
                tune.Add((b + part * 4 * beatsPerBar, last ? end : Math.Clamp(g + shift, low, high + 2), l));
            }
        }
        Phrase(0, 0, 4);  // A: ends on the fifth, open
        Phrase(1, 0, 2);  // A': same idea, rests on the third
        Phrase(2, 2, 4);  // B: lifted
        Phrase(3, 0, 0);  // A'': home
        return tune;
    }

    /// Chord roots (scale degrees) per bar, cycling a four-bar progression.
    static int Chord(int[] prog, int bar) => prog[bar % prog.Length];

    // ---------- the map themes ----------

    /// "The Roads": D dorian at 84 beats a minute, a lute over a drone, a plucked bass walking the chords.
    static float[] Roads()
    {
        var r = new Random(11);
        const float beat = 60f / 84;
        int[] prog = { 0, 3, 0, 6 };
        var b = Buf(16 * 4 * beat + 4);
        Drone(b, Hz(38), 0.05f);
        foreach (var (at, d, len) in Tune(r, 4, Rhythms))
            Pluck(b, (int)(at * beat * Rate), Hz(Deg(62, Dorian, d)), 0.28f, 0.9968f, r, 0.6f);
        for (int bar = 0; bar < 16; bar++)
        {
            int c = Chord(prog, bar);
            Pluck(b, (int)(bar * 4 * beat * Rate), Hz(Deg(50, Dorian, c)), 0.3f, 0.997f, r, 0.3f);
            Pluck(b, (int)((bar * 4 + 2) * beat * Rate), Hz(Deg(50, Dorian, c + 4)), 0.18f, 0.996f, r, 0.3f);
        }
        Pluck(b, (int)(64 * beat * Rate), Hz(50), 0.35f, 0.998f, r, 0.3f);
        Pluck(b, (int)(64 * beat * Rate), Hz(62), 0.25f, 0.998f, r, 0.6f);
        return Normalize(Fade(Space(b, 0.3f, 1.8f)), 0.8f);
    }

    /// "Hearth": A minor in three, harp chords rolled under a flute.
    static float[] Hearth()
    {
        var r = new Random(12);
        const float beat = 60f / 76;
        int[] prog = { 0, 5, 2, 6 };
        var b = Buf(18 * 3 * beat + 3);
        foreach (var (at, d, len) in Tune(r, 3, Waltz, 0, 8))
            Flute(b, 3 * beat + at * beat, Hz(Deg(69, Aeolian, d)), len * beat * 0.92f, 0.16f, r);
        for (int bar = 0; bar < 18; bar++)
        {
            int c = Chord(prog, bar);
            for (int k = 0; k < 3; k++)
                foreach (int n in k == 0 ? new[] { 0, 4 } : new[] { 2, 4 + 3 * (k - 1) })
                    Pluck(b, (int)((bar * 3 + k) * beat * Rate + n * 220), Hz(Deg(45, Aeolian, c + n)), k == 0 ? 0.22f : 0.12f, 0.998f, r, 0.8f);
        }
        return Normalize(Fade(Space(b, 0.4f, 2.4f)), 0.6f); // denser than the others: lower, to sit at their loudness
    }

    /// "Banners": G mixolydian at 104, a fiddle reel over a frame drum and tambourine, a drone of pipes.
    static float[] Banners()
    {
        var r = new Random(13);
        const float beat = 60f / 104;
        var b = Buf(18 * 4 * beat + 3);
        Drone(b, Hz(43), 0.045f);
        foreach (var (at, d, len) in Tune(r, 4, Rhythms))
            Fiddle(b, 2 * 4 * beat + at * beat, Hz(Deg(67, Mixolydian, d)), len * beat * 0.85f, 0.2f);
        for (int bar = 0; bar < 18; bar++)
            for (int k = 0; k < 4; k++)
            {
                float at = (bar * 4 + k) * beat;
                if (bar < 17 || k == 0) Drum(b, at, k % 2 == 0 ? 110 : 150, k == 0 ? 0.5f : 0.3f, r, 0.25f);
                if (bar < 17) Jingle(b, at + beat / 2, 0.15f, r);
            }
        return Normalize(Fade(Space(b, 0.25f, 1.5f)), 0.8f);
    }

    /// Let the last two seconds die away.
    static float[] Fade(float[] b)
    {
        int n = Math.Min(b.Length, 2 * Rate);
        for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= i / (float)n;
        return b;
    }

    // ---------- the battle ----------

    const float BattleBeat = 0.6f, BattleLoop = 8 * 4 * BattleBeat; // 8 bars at 100 beats a minute: 19.2 s

    /// Before the battle: a low drone, a slow heartbeat on the drum, a far horn and a fiddle held low.
    static float[] Deploy()
    {
        var r = new Random(14);
        const float loop = 16;
        var b = Buf(loop + 3);
        Drone(b, Hz(38), 0.07f, loop);
        for (float at = 0; at < loop; at += 2) { Drum(b, at, 55, 0.6f, r, 0.5f); Drum(b, at + 0.28f, 50, 0.35f, r, 0.5f); }
        Mix(b, Horn(Hz(50), 2.5f, false, false), 3, 0.25f);
        Mix(b, Horn(Hz(48), 2.5f, false, false), 11, 0.25f);
        Fiddle(b, 6, Hz(57), 3.5f, 0.06f);
        Fiddle(b, 13, Hz(53), 2.5f, 0.06f);
        return Normalize(Wrap(Space(b, 0.3f, 2f), loop), 0.75f);
    }

    /// The battle, first layer: war drums and a low horn moving between D and C.
    static float[] BattleBase()
    {
        var r = new Random(15);
        var b = Buf(BattleLoop + 3);
        int[] pattern = { 1, 0, 0, 1, 1, 0, 1, 0 };
        for (int bar = 0; bar < 8; bar++)
            for (int k = 0; k < 8; k++)
                if (pattern[k] == 1) Drum(b, (bar * 8 + k) * BattleBeat / 2, k == 0 ? 55 : 80, k == 0 ? 1 : 0.6f, r);
        for (int half = 0; half < 4; half++)
            Mix(b, Horn(Hz(half % 2 == 0 ? 38 : 36), 2 * 4 * BattleBeat + 0.3f, false, false), half * 2 * 4 * BattleBeat, 0.35f);
        return Normalize(Wrap(Space(b, 0.2f, 1.5f), BattleLoop), 0.8f);
    }

    /// The battle, second layer (heard as the fighting grows): rolling toms, snares on the off beat, horns calling a
    /// rising figure and brass stabs on the chord changes. Same length as the first so they loop together.
    static float[] BattleHigh()
    {
        var r = new Random(16);
        var b = Buf(BattleLoop + 3);
        for (int bar = 0; bar < 8; bar++)
        {
            float at = bar * 4 * BattleBeat;
            Snare(b, at + BattleBeat, 0.35f, r);
            Snare(b, at + 3 * BattleBeat, 0.35f, r);
            if (bar % 2 == 1) for (int k = 0; k < 6; k++) Drum(b, at + 2.5f * BattleBeat + k * BattleBeat / 4, 120 - k * 8, 0.3f + 0.08f * k, r, 0.25f);
        }
        int[] call = { 0, 4, 7, 6, 4 }; float[] lens = { 1, 1, 1.5f, 0.5f, 4 };
        for (int phrase = 0; phrase < 2; phrase++)
        {
            float at = phrase * 4 * 4 * BattleBeat;
            for (int k = 0; k < call.Length; k++)
            {
                int d = phrase == 1 && k == call.Length - 1 ? 6 : call[k];
                Mix(b, Horn(Hz(Deg(50, Dorian, d)), lens[k] * BattleBeat * 0.95f, false, false, 0.1f), at, 0.45f);
                Mix(b, Horn(Hz(Deg(50, Dorian, d + 2)), lens[k] * BattleBeat * 0.95f, false, false, 0.1f), at, 0.25f);
                at += lens[k] * BattleBeat;
            }
        }
        for (int half = 0; half < 4; half++) Mix(b, Horn(Hz(half % 2 == 0 ? 50 : 48), 0.35f, false, false, 0.14f), half * 2 * 4 * BattleBeat, 0.4f);
        return Normalize(Wrap(Space(b, 0.25f, 1.5f), BattleLoop), 0.8f);
    }

    /// Victory: a drum roll, a horn fanfare in thirds rising to the high root, the last chord held under a cymbal.
    static float[] Victory()
    {
        var r = new Random(17);
        var b = Buf(7);
        for (int k = 0; k < 16; k++) Drum(b, k * 0.05f, 140, 0.15f + 0.04f * k, r, 0.15f);
        int[] mel = { 57, 62, 66, 69 }; float[] at = { 0.85f, 1.25f, 1.55f, 1.85f }, len = { 0.35f, 0.25f, 0.25f, 2.6f };
        for (int k = 0; k < mel.Length; k++)
        {
            Mix(b, Horn(Hz(mel[k]), len[k], false, false, 0.12f), at[k], 0.6f);
            Mix(b, Horn(Hz(mel[k] - (k == 3 ? 3 : 4)), len[k], false, false, 0.1f), at[k], 0.35f);
        }
        Mix(b, Horn(Hz(50), 2.8f, false, false), 1.85f, 0.4f);
        Drum(b, 0.85f, 55, 1, r); Drum(b, 1.85f, 55, 1, r);
        Cymbal(b, 1.85f, 0.25f, r);
        return Normalize(Space(b, 0.35f, 2.2f), 0.85f);
    }

    /// Defeat: a slow dirge on low horns falling D, C, B flat, A, then the low root, a drum under each.
    static float[] Defeat()
    {
        var r = new Random(18);
        var b = Buf(8);
        int[] notes = { 50, 48, 46, 45, 38 };
        for (int k = 0; k < notes.Length; k++)
        {
            float at = k * 1.1f, len = k == notes.Length - 1 ? 2.8f : 1.05f;
            Mix(b, Horn(Hz(notes[k]), len, k == notes.Length - 1, false), at, 0.6f);
            Mix(b, Horn(Hz(notes[k] - 12), len, false, false), at, 0.3f);
            Drum(b, at, 48, 0.8f, r, 0.7f);
        }
        return Normalize(Space(b, 0.45f, 2.8f), 0.8f);
    }
}
