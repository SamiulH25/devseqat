using System.Text.Json;

namespace Ironfield;

/// A hand-authored multi-track musical score loaded from `Data/music/*.json`, compiled into time-sorted notes and
/// rendered window-by-window through `AudioBank` (pure maths, no MonoGame types).
public sealed class MusicScore
{
    public string Id = "", Title = "", Category = "map", Culture = "common";
    public float Bpm = 84;
    public int BeatsPerBar = 4, Bars = 16;
    public bool Loop;
    public float ReverbWet = 0.28f, ReverbDecay = 1.8f, MasterGain = 0.85f;
    public List<TrackDef> Tracks = new();

    public sealed class TrackDef
    {
        public string Voice = "lute";
        public float Pan, Gain = 0.65f, HumanizeMs = 8f;
        public int Stem; // 0 = base/deploy, 1 = skirmish/sub-lead, 2 = melee, 3 = crisis/climax
        /// Optional pattern tiling: `[startBar, endBar, patternLenBars]`; `Notes` are given in `0 .. patternLenBars * BeatsPerBar`.
        public int[]? RepeatBars;
        /// Optional semitone shift per bar of the tiled section (cycles over its length).
        public int[]? BarTransposes;
        public List<JsonElement[]> Notes = new();
    }

    public readonly record struct CompiledNote(
        string Voice, int Midi, double StartSec, double EndWithTailSec, double DurSec,
        float Velocity, float Gain, float Pan, int Stem, string? Ornament);

    CompiledNote[] compiled = Array.Empty<CompiledNote>();
    public double BeatSec => 60.0 / Math.Max(20f, Bpm);
    public double DurationSec => Bars * BeatsPerBar * BeatSec;
    public double TotalPlaySec => Loop ? DurationSec : DurationSec + 2.4;
    public IReadOnlyList<CompiledNote> CompiledNotes => compiled;

    public void Compile(AudioBank bank)
    {
        var list = new List<CompiledNote>(256);
        double beatSec = BeatSec;
        int seedBase = StableHash(Id);

        for (int ti = 0; ti < Tracks.Count; ti++)
        {
            var t = Tracks[ti];
            var voice = bank.Get(t.Voice);
            float tail = voice != null ? AudioBank.TailSeconds(voice) : 0.3f;
            var rng = new Random(seedBase + ti * 7919);

            void Emit(double beat, int midi, double durBeats, float vel, string? orn)
            {
                double jitter = t.HumanizeMs > 0 ? (rng.NextDouble() * 2.0 - 1.0) * (t.HumanizeMs * 0.001) : 0.0;
                // Keep beat 0.0 non-negative so loop seams never clip the very first sample
                double start = Math.Max(0.0, beat * beatSec + jitter);
                double dur = Math.Max(0.04, durBeats * beatSec);
                float vJit = vel * (float)(0.96 + rng.NextDouble() * 0.08);
                list.Add(new CompiledNote(t.Voice, Math.Clamp(midi, 18, 108), start, start + dur + tail, dur, vJit, t.Gain, t.Pan, t.Stem, orn));
            }

            if (t.RepeatBars is { Length: >= 3 } rep)
            {
                int bStart = rep[0], bEnd = rep[1], patBars = Math.Max(1, rep[2]);
                for (int bar = bStart; bar < bEnd; bar += patBars)
                {
                    foreach (var raw in t.Notes)
                    {
                        var (nb, midi, dur, vel, orn) = ParseNote(raw);
                        int barInPat = (int)Math.Floor(nb / Math.Max(1, BeatsPerBar));
                        int absBar = bar + barInPat;
                        if (absBar >= bEnd) continue;
                        int shift = t.BarTransposes is { Length: > 0 } bt ? bt[(absBar - bStart) % bt.Length] : 0;
                        Emit(bar * BeatsPerBar + nb, midi + shift, dur, vel, orn);
                    }
                }
            }
            else
            {
                foreach (var raw in t.Notes)
                {
                    var (nb, midi, dur, vel, orn) = ParseNote(raw);
                    Emit(nb, midi, dur, vel, orn);
                }
            }
        }

        list.Sort((a, b) => a.StartSec.CompareTo(b.StartSec));
        compiled = list.ToArray();
    }

    static (double Beat, int Midi, double Dur, float Vel, string? Orn) ParseNote(JsonElement[] a)
    {
        if (a.Length < 4) throw new InvalidDataException("note requires at least [beat, midi, dur, vel]");
        double beat = a[0].GetDouble();
        int midi = a[1].GetInt32();
        double dur = a[2].GetDouble();
        float vel = a[3].GetSingle();
        string? orn = a.Length >= 5 && a[4].ValueKind == JsonValueKind.String ? a[4].GetString() : null;
        return (beat, midi, dur, vel, orn);
    }

    static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    /// Mix `frames` stereo samples starting at `startSec` into `stereoDst` (`frames * 2`, cleared or additive).
    /// `stemGains` has 4 weights (`0..3`); null treats stem 0 as 1 and stems 1..3 as 1.
    public void RenderStereoWindow(AudioBank bank, float[] stereoDst, int frames, int rate, double startSec, ReadOnlySpan<float> stemGains)
    {
        double winDur = frames / (double)rate;
        if (Loop && DurationSec > 0.1)
        {
            double dur = DurationSec;
            double wrapped = startSec % dur;
            if (wrapped < 0) wrapped += dur;

            // If window crosses the loop boundary, split cleanly at the seam
            if (wrapped + winDur > dur)
            {
                int firstFrames = Math.Clamp((int)Math.Round((dur - wrapped) * rate), 1, frames - 1);
                RenderSlicePass(bank, stereoDst, 0, firstFrames, rate, wrapped, stemGains);
                // Wrap-over tails from the end of the loop into the start of the loop
                RenderSlicePass(bank, stereoDst, 0, firstFrames, rate, wrapped + dur, stemGains);

                int remFrames = frames - firstFrames;
                var tailSlice = new float[remFrames * 2];
                RenderSlicePass(bank, tailSlice, 0, remFrames, rate, 0.0, stemGains);
                RenderSlicePass(bank, tailSlice, 0, remFrames, rate, dur, stemGains);
                for (int i = 0; i < tailSlice.Length; i++) stereoDst[firstFrames * 2 + i] += tailSlice[i];
                return;
            }
            RenderSlicePass(bank, stereoDst, 0, frames, rate, wrapped, stemGains);
            if (wrapped < 3.0)
                RenderSlicePass(bank, stereoDst, 0, frames, rate, wrapped + dur, stemGains);
        }
        else
        {
            if (startSec >= TotalPlaySec) return;
            RenderSlicePass(bank, stereoDst, 0, frames, rate, startSec, stemGains);
        }
    }

    void RenderSlicePass(AudioBank bank, float[] stereoDst, int offsetFrames, int frames, int rate, double winStart, ReadOnlySpan<float> stemGains)
    {
        double winEnd = winStart + frames / (double)rate;
        float master = MasterGain;
        // Binary search first note that could start before winEnd
        int hi = UpperBoundStart(winEnd);
        for (int i = 0; i < hi; i++)
        {
            ref readonly var n = ref compiled[i];
            if (n.EndWithTailSec <= winStart) continue;
            int st = Math.Clamp(n.Stem, 0, 3);
            float sg = stemGains.Length > st ? stemGains[st] : 1f;
            if (sg <= 0.002f) continue;
            if (offsetFrames == 0)
                bank.RenderNoteSlice(stereoDst, frames, rate, winStart, n.Voice, n.Midi, n.StartSec, n.DurSec, n.Velocity, n.Gain * sg * master, n.Pan, n.Ornament);
        }
    }

    int UpperBoundStart(double maxStart)
    {
        int lo = 0, hi = compiled.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (compiled[mid].StartSec < maxStart) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// Render the complete piece (with stereo hall reverb and fade/loop seam) into an interleaved stereo buffer for WAV export or tests.
    public float[] RenderFullStereo(AudioBank bank, int rate, ReadOnlySpan<float> stemGains)
    {
        double dur = Loop ? DurationSec : TotalPlaySec;
        int totalFrames = Math.Max(1, (int)Math.Round(dur * rate));
        var dry = new float[totalFrames * 2];
        const int chunk = 8192;
        var tmp = new float[chunk * 2];
        for (int pos = 0; pos < totalFrames; pos += chunk)
        {
            int f = Math.Min(chunk, totalFrames - pos);
            Array.Clear(tmp, 0, f * 2);
            RenderStereoWindow(bank, tmp, f, rate, pos / (double)rate, stemGains);
            Array.Copy(tmp, 0, dry, pos * 2, f * 2);
        }
        var hall = new StereoHall(rate);
        if (Loop)
        {
            // Prime the hall with the final 2.5 seconds so the loop seam includes natural reverb carry
            int primeFrames = Math.Min(totalFrames, (int)(2.5 * rate));
            var discard = new float[primeFrames * 2];
            Array.Copy(dry, (totalFrames - primeFrames) * 2, discard, 0, primeFrames * 2);
            hall.ProcessInPlace(discard, primeFrames, ReverbWet, ReverbDecay);
        }
        hall.ProcessInPlace(dry, totalFrames, ReverbWet, ReverbDecay);
        if (!Loop && totalFrames > rate)
        {
            int fadeFrames = Math.Min(totalFrames / 3, (int)(1.8 * rate));
            for (int i = 0; i < fadeFrames; i++)
            {
                float k = i / (float)fadeFrames;
                int fi = (totalFrames - 1 - i) * 2;
                dry[fi] *= k;
                dry[fi + 1] *= k;
            }
        }
        SoftLimitStereo(dry, totalFrames * 2);
        return dry;
    }

    public static void SoftLimitStereo(float[] buf, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = buf[i];
            if (MathF.Abs(x) > 0.75f)
                buf[i] = MathF.Sign(x) * (0.75f + 0.23f * MathF.Tanh((MathF.Abs(x) - 0.75f) * 2.2f));
        }
    }
}

/// Stateful stereo Schroeder/Moorer hall reverb (decorrelated L/R comb + allpass lines) shared by the live music
/// streamer and offline `RenderFullStereo`. Pure maths, zero allocations per chunk after construction.
public sealed class StereoHall
{
    readonly float[][] combL, combR, apL, apR;
    readonly int[] combPosL, combPosR, apPosL, apPosR;
    readonly float[] lpL, lpR;
    readonly int rate;
    float cachedDecay = -1;
    readonly float[] fbL = new float[4], fbR = new float[4];

    static readonly int[] BaseCombsL = { 1307, 1459, 1607, 1777 };
    static readonly int[] BaseCombsR = { 1361, 1511, 1663, 1831 };
    static readonly int[] BaseApL = { 389, 137 };
    static readonly int[] BaseApR = { 419, 157 };

    public StereoHall(int sampleRate)
    {
        rate = sampleRate;
        double scale = sampleRate / 44100.0;
        combL = BaseCombsL.Select(n => new float[Math.Max(16, (int)Math.Round(n * scale))]).ToArray();
        combR = BaseCombsR.Select(n => new float[Math.Max(16, (int)Math.Round(n * scale))]).ToArray();
        apL = BaseApL.Select(n => new float[Math.Max(8, (int)Math.Round(n * scale))]).ToArray();
        apR = BaseApR.Select(n => new float[Math.Max(8, (int)Math.Round(n * scale))]).ToArray();
        combPosL = new int[4]; combPosR = new int[4];
        apPosL = new int[2]; apPosR = new int[2];
        lpL = new float[4]; lpR = new float[4];
    }

    public void ProcessInPlace(float[] stereo, int frames, float wet, float decaySec, float damp = 0.36f)
    {
        if (wet <= 0.001f) return;
        float dSec = Math.Clamp(decaySec, 0.3f, 5.0f);
        if (MathF.Abs(dSec - cachedDecay) > 0.02f)
        {
            cachedDecay = dSec;
            for (int c = 0; c < 4; c++)
            {
                fbL[c] = MathF.Pow(10f, -3f * combL[c].Length / (dSec * rate));
                fbR[c] = MathF.Pow(10f, -3f * combR[c].Length / (dSec * rate));
            }
        }
        float dampKeep = 1f - damp;
        for (int i = 0; i < frames; i++)
        {
            float inL = stereo[i * 2], inR = stereo[i * 2 + 1];
            float sendL = inL * 0.8f + inR * 0.2f, sendR = inR * 0.8f + inL * 0.2f;
            float wL = 0, wR = 0;
            for (int c = 0; c < 4; c++)
            {
                var lineL = combL[c]; int pL = combPosL[c];
                float yL = lineL[pL];
                lpL[c] += (yL - lpL[c]) * dampKeep;
                lineL[pL] = sendL + lpL[c] * fbL[c];
                combPosL[c] = pL + 1 == lineL.Length ? 0 : pL + 1;
                wL += yL;

                var lineR = combR[c]; int pR = combPosR[c];
                float yR = lineR[pR];
                lpR[c] += (yR - lpR[c]) * dampKeep;
                lineR[pR] = sendR + lpR[c] * fbR[c];
                combPosR[c] = pR + 1 == lineR.Length ? 0 : pR + 1;
                wR += yR;
            }
            wL *= 0.22f; wR *= 0.22f;
            for (int a = 0; a < 2; a++)
            {
                var lL = apL[a]; int pL = apPosL[a];
                float dL = lL[pL]; lL[pL] = wL + dL * 0.5f; wL = dL - wL * 0.5f;
                apPosL[a] = pL + 1 == lL.Length ? 0 : pL + 1;

                var lR = apR[a]; int pR = apPosR[a];
                float dR = lR[pR]; lR[pR] = wR + dR * 0.5f; wR = dR - wR * 0.5f;
                apPosR[a] = pR + 1 == lR.Length ? 0 : pR + 1;
            }
            stereo[i * 2] = inL + wL * wet;
            stereo[i * 2 + 1] = inR + wR * wet;
        }
    }
}

/// Registry of all `Data/music/*.json` score packs.
public static class MusicLibrary
{
    public const string FolderName = "Data/music";

    sealed class ScorePack { public List<MusicScore> Pieces = new(); }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    static Dictionary<string, MusicScore>? loaded;
    static readonly object gate = new();

    public static IReadOnlyDictionary<string, MusicScore> All
    {
        get
        {
            if (loaded != null) return loaded;
            lock (gate)
            {
                if (loaded != null) return loaded;
                var map = new Dictionary<string, MusicScore>(StringComparer.OrdinalIgnoreCase);
                string? dir = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
                    .Select(d => Path.Combine(d, FolderName))
                    .FirstOrDefault(Directory.Exists);
                if (dir != null)
                {
                    var bank = AudioBank.Shared;
                    foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                    {
                        var pack = JsonSerializer.Deserialize<ScorePack>(File.ReadAllText(file), JsonOpts)
                            ?? throw new InvalidDataException("empty score pack: " + file);
                        foreach (var p in pack.Pieces)
                        {
                            if (string.IsNullOrWhiteSpace(p.Id)) throw new InvalidDataException($"score missing id in {file}");
                            if (map.ContainsKey(p.Id)) throw new InvalidDataException($"duplicate score id '{p.Id}' in {file}");
                            p.Compile(bank);
                            map[p.Id] = p;
                        }
                    }
                }
                loaded = map;
                return loaded;
            }
        }
    }

    public static MusicScore? Get(string id) => All.TryGetValue(id, out var s) ? s : null;
    public static bool Has(string id) => All.ContainsKey(id);

    public static IEnumerable<MusicScore> ByCategory(string category, string? culture = null) =>
        All.Values.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
            && (culture == null || s.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase)));

    /// Schema and musical sanity check run by `Tests/AudioTests.cs`.
    public static List<string> Validate()
    {
        var errors = new List<string>();
        var bank = AudioBank.Shared;
        var validOrnaments = new HashSet<string> { "grace", "mordent", "slide", "roll", "swell" };
        foreach (var s in All.Values)
        {
            if (s.Bpm < 35 || s.Bpm > 220) errors.Add($"{s.Id}: bpm {s.Bpm} out of [35, 220]");
            if (s.BeatsPerBar < 2 || s.BeatsPerBar > 9) errors.Add($"{s.Id}: invalid beatsPerBar {s.BeatsPerBar}");
            if (s.Bars < 2 || s.Bars > 128) errors.Add($"{s.Id}: invalid bars {s.Bars}");
            if (s.Tracks.Count == 0) errors.Add($"{s.Id}: has no tracks");
            if (s.CompiledNotes.Count == 0) errors.Add($"{s.Id}: compiled to 0 notes");
            foreach (var t in s.Tracks)
            {
                if (!bank.HasVoice(t.Voice)) errors.Add($"{s.Id}: unknown voice '{t.Voice}'");
                if (t.Stem < 0 || t.Stem > 3) errors.Add($"{s.Id}: track stem {t.Stem} out of 0..3");
                if (t.Pan < -1f || t.Pan > 1f) errors.Add($"{s.Id}: track pan {t.Pan} out of -1..1");
            }
            foreach (var n in s.CompiledNotes)
            {
                if (n.Midi < 18 || n.Midi > 108) errors.Add($"{s.Id}: note midi {n.Midi} out of 18..108");
                if (n.Ornament != null && !validOrnaments.Contains(n.Ornament))
                    errors.Add($"{s.Id}: invalid ornament '{n.Ornament}'");
            }
        }
        return errors;
    }
}
