namespace Ironfield;

/// Mode A Word-by-Word Acoustic Instrument Speech renderer (docs/VOICE_DIALOGUE.md).
/// Renders clean, zero-static spoken cadences directly through `AudioBank.Shared` (`Data/audio/instruments.bank`).
/// Headless and deterministic (uses `Rng.Mix` / `Rng.Of` only, never `GetHashCode` or `Random.Shared`).
public static class VoiceSpeech
{
    public const int DefaultRate = 22050;

    readonly record struct NoteEv(int Midi, double StartSec, double DurSec, float Velocity, string? Ornament);

    /// Render a spoken line as mono float PCM at `sampleRate` Hz (normalized, warm subtle hall tail).
    /// `maxWords` caps long diplomacy speeches so they stay expressive without yapping.
    public static float[] Render(VoiceProfile profile, string text, VoiceMood mood = VoiceMood.Even, int maxWords = 12, int sampleRate = DefaultRate)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();
        var bank = AudioBank.Shared;
        string inst = bank.HasVoice(profile.Instrument) ? profile.Instrument : "lute";
        var (root, scale) = VoiceProfile.RegionScale(profile.Culture);

        var (moodShift, moodSpd, moodVel) = mood switch
        {
            VoiceMood.Hostile => (-3, 1.00f, 0.72f),
            VoiceMood.Grudge  => (-4, 0.96f, 0.76f),
            VoiceMood.Warm    => ( 2, 0.96f, 0.56f),
            VoiceMood.Command => ( 3, 1.12f, 0.82f),
            VoiceMood.Weary   => (-4, 0.80f, 0.46f),
            _                 => ( 0, 1.00f, 0.58f),
        };

        var words = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return Array.Empty<float>();
        int count = Math.Min(words.Length, Math.Max(1, maxWords));

        var notes = new List<NoteEv>(count * 2);
        double rate = Math.Clamp(profile.TempoMul * moodSpd, 0.65f, 1.45f);
        int baseMidi = root + profile.SemitoneOffset + moodShift;
        double t = 0.015;

        for (int wi = 0; wi < count; wi++)
        {
            string raw = words[wi];
            string clean = CleanLetters(raw);
            if (clean.Length == 0) continue;

            bool isQuestion = raw.EndsWith('?');
            bool isExclaim = raw.EndsWith('!');
            bool isTrail = raw.EndsWith("...");
            bool isPeriod = !isTrail && raw.EndsWith('.');
            bool isComma = raw.EndsWith(',') || raw.EndsWith(';') || raw.EndsWith(':') || raw.Contains('—') || raw.Contains("--");
            bool isEnd = isQuestion || isExclaim || isPeriod || isTrail || wi == count - 1;

            int h = Rng.Mix(profile.Seed, Rng.Of(clean), wi * 17 + 3);
            bool stressed = clean.Length >= 5 || wi == 0 || isExclaim;
            int notesInWord = clean.Length >= 7 ? 2 : 1;

            // Gentle taper on the last few words when capping a long speech
            float taper = words.Length > maxWords && wi >= count - 3
                ? 0.45f + 0.55f * ((count - 1 - wi) / 3f)
                : 1f;

            for (int ni = 0; ni < notesInWord; ni++)
            {
                bool lastNote = ni == notesInWord - 1;
                int deg = Math.Abs((h >> (ni * 4)) % Math.Max(1, scale.Length - 2)) + (stressed ? 1 : 0);
                string? orn = null;

                if (isEnd && lastNote)
                {
                    if (isQuestion) { deg = scale.Length - 1; orn = "slide"; }
                    else if (isExclaim) { deg = Math.Min(scale.Length - 1, 4); orn = "grace"; }
                    else if (isPeriod || isTrail) { deg = 0; }
                }
                else if (isComma && lastNote)
                {
                    deg = Math.Min(scale.Length - 1, 2);
                }

                int midi = Math.Clamp(baseMidi + scale[Math.Clamp(deg, 0, scale.Length - 1)], 30, 86);
                double dur = ((stressed ? 0.155 : 0.108) * (isEnd && lastNote ? 1.38 : 1.0)) / rate;
                float vel = Math.Clamp(moodVel * (stressed ? 1.08f : 0.80f) * taper, 0.12f, 0.95f);
                notes.Add(new NoteEv(midi, t, dur, vel, orn));
                t += dur + 0.014 / rate;
            }

            double pause = (isPeriod || isQuestion || isExclaim) ? 0.24
                         : isTrail ? 0.30
                         : isComma ? 0.13
                         : 0.022;
            t += pause / rate;
        }

        if (notes.Count == 0) return Array.Empty<float>();
        var voice = bank.Get(inst);
        double tail = voice != null ? AudioBank.TailSeconds(voice) : 0.22;
        double totalSec = t + tail + 0.08;
        int frames = Math.Max(1, (int)Math.Ceiling(totalSec * sampleRate));

        var stereo = new float[frames * 2];
        foreach (var n in notes)
        {
            bank.RenderNoteSlice(stereo, frames, sampleRate, 0.0, inst, n.Midi, n.StartSec, n.DurSec, n.Velocity, 0.85f, 0f, n.Ornament);
        }

        // Collapse to mono + subtle warm early-reflection tail (no noise/static)
        var mono = new float[frames];
        int d1 = (int)(0.019 * sampleRate), d2 = (int)(0.043 * sampleRate);
        float peak = 0.001f;
        for (int i = 0; i < frames; i++)
        {
            float s = 0.5f * (stereo[i * 2] + stereo[i * 2 + 1]);
            if (i > d1) s += mono[i - d1] * 0.14f;
            if (i > d2) s += mono[i - d2] * 0.07f;
            mono[i] = s;
            float a = MathF.Abs(s);
            if (a > peak) peak = a;
        }

        float norm = MathF.Min(1.6f, 0.80f / peak);
        for (int i = 0; i < frames; i++) mono[i] *= norm;
        return mono;
    }

    static string CleanLetters(string raw)
    {
        Span<char> buf = stackalloc char[Math.Min(raw.Length, 32)];
        int n = 0;
        for (int i = 0; i < raw.Length && n < buf.Length; i++)
        {
            char c = raw[i];
            if (char.IsLetter(c)) buf[n++] = char.ToLowerInvariant(c);
        }
        return new string(buf[..n]);
    }
}
