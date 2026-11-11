using System.IO.Compression;
using System.Text;

namespace Ironfield;

/// Multi-zone acoustic instrument bank (`Data/audio/instruments.bank`, built by `tools/build_bank.py` from the
/// MIT-licensed FluidR3_GM SoundFont) plus a stateless Hermite-interpolated note renderer for `MusicScore` and
/// `AudioStreamer`. Pure maths and BCL I/O: no MonoGame types, so headless `--selftest` and `--sounds` use it directly.
public sealed class AudioBank
{
    public const string BankFile = "Data/audio/instruments.bank";

    public sealed class Zone
    {
        public byte RootMidi, LowMidi, HighMidi;
        public int SampleRate, LoopStart, LoopEnd;
        public float[] Samples = Array.Empty<float>();
    }

    public sealed class Voice
    {
        public string Id = "";
        public byte Category;
        public float Attack, Release, VibDepth, VibRate;
        public Zone[] Zones = Array.Empty<Zone>();
    }

    static readonly Lazy<AudioBank> shared = new(Load);
    public static AudioBank Shared => shared.Value;

    readonly Dictionary<string, Voice> voices = new(StringComparer.OrdinalIgnoreCase);
    public bool LoadedFromDisk { get; private set; }
    public IReadOnlyCollection<string> VoiceIds => voices.Keys;
    public bool HasVoice(string id) => voices.ContainsKey(id);
    public Voice? Get(string id) => voices.TryGetValue(id, out var v) ? v : null;

    static AudioBank Load()
    {
        var bank = new AudioBank();
        string? path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
            .Select(d => Path.Combine(d, BankFile))
            .FirstOrDefault(File.Exists);
        if (path != null)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var ms = new MemoryStream();
                gz.CopyTo(ms);
                ms.Position = 0;
                using var br = new BinaryReader(ms, Encoding.ASCII);
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "IFBK") throw new InvalidDataException("bad magic");
                ushort ver = br.ReadUInt16(), count = br.ReadUInt16();
                if (ver != 1) throw new InvalidDataException("unsupported bank version " + ver);
                for (int vi = 0; vi < count; vi++)
                {
                    int idLen = br.ReadByte();
                    string id = Encoding.ASCII.GetString(br.ReadBytes(idLen));
                    byte cat = br.ReadByte();
                    float atk = br.ReadSingle(), rel = br.ReadSingle(), vibD = br.ReadSingle(), vibR = br.ReadSingle();
                    int zCount = br.ReadByte();
                    var zones = new Zone[zCount];
                    for (int zi = 0; zi < zCount; zi++)
                    {
                        byte root = br.ReadByte(), lo = br.ReadByte(), hi = br.ReadByte();
                        int sr = br.ReadUInt16(), ls = br.ReadInt32(), le = br.ReadInt32(), len = br.ReadInt32();
                        byte[] loBytes = br.ReadBytes(len), hiBytes = br.ReadBytes(len);
                        var samples = new float[len];
                        short acc = 0;
                        for (int i = 0; i < len; i++)
                        {
                            short d = (short)(loBytes[i] | (hiBytes[i] << 8));
                            acc = (short)(acc + d);
                            samples[i] = acc / 32768f;
                        }
                        zones[zi] = new Zone { RootMidi = root, LowMidi = lo, HighMidi = hi, SampleRate = sr, LoopStart = ls, LoopEnd = le, Samples = samples };
                    }
                    bank.voices[id] = new Voice { Id = id, Category = cat, Attack = atk, Release = rel, VibDepth = vibD, VibRate = vibR, Zones = zones };
                }
                bank.LoadedFromDisk = true;
                return bank;
            }
            catch (Exception e)
            {
                Console.WriteLine("audio bank fallback: " + e.Message);
            }
        }
        bank.BuildFallback();
        return bank;
    }

    /// Minimal procedural fallback if `instruments.bank` is missing in an isolated run.
    void BuildFallback()
    {
        string[] all =
        {
            "lute", "harp", "oud", "cittern", "harpsichord", "qanun",
            "fiddle", "nyckelharpa", "morin_khuur", "viols", "strings_spiccato", "contrabass_drone",
            "recorder", "ney", "shawm", "low_whistle", "pipes_drone",
            "war_horn", "french_horn", "low_brass", "choir_aah", "throat_drone",
            "war_drum", "frame_drum", "darbuka", "snare_march", "tambourine_riq", "tubular_bell",
        };
        const int sr = 22050;
        var s = new float[sr];
        double f0 = 440.0 * Math.Pow(2, (60 - 69) / 12.0);
        int period = (int)Math.Round(sr / f0);
        int len = period * 80;
        s = new float[len];
        for (int i = 0; i < len; i++)
        {
            float ph = (i % period) / (float)period;
            s[i] = MathF.Sin(MathF.Tau * ph) * 0.6f + MathF.Sin(2 * MathF.Tau * ph) * 0.25f + MathF.Sin(3 * MathF.Tau * ph) * 0.1f;
        }
        foreach (var id in all)
        {
            bool perc = id is "war_drum" or "frame_drum" or "darbuka" or "snare_march" or "tambourine_riq";
            voices[id] = new Voice
            {
                Id = id, Category = perc ? (byte)4 : (byte)1, Attack = perc ? 0.002f : 0.03f, Release = 0.18f, VibDepth = perc ? 0 : 0.003f, VibRate = 5f,
                Zones = new[] { new Zone { RootMidi = 60, LowMidi = 0, HighMidi = 127, SampleRate = sr, LoopStart = perc ? 0 : period * 10, LoopEnd = perc ? 0 : len, Samples = s } },
            };
        }
    }

    public static float TailSeconds(Voice v) => MathF.Max(0.12f, v.Release + 0.08f);

    /// Statelessly mix the slice of a single note overlapping `[winStartSec, winStartSec + frames / outRate)` into an
    /// interleaved stereo buffer `dst` (`frames * 2` floats). Analytical phase integration guarantees zero chunk-seam clicks.
    public void RenderNoteSlice(
        float[] dst, int frames, int outRate, double winStartSec,
        string voiceId, int midi, double noteStartSec, double durSec,
        float velocity, float trackGain, float pan, string? ornament)
    {
        if (!voices.TryGetValue(voiceId, out var voice) || voice.Zones.Length == 0) return;
        var zone = PickZone(voice, midi);
        double relEnd = noteStartSec + durSec + voice.Release;
        double winEnd = winStartSec + frames / (double)outRate;
        if (relEnd <= winStartSec || noteStartSec >= winEnd) return;

        int i0 = Math.Max(0, (int)Math.Floor((noteStartSec - winStartSec) * outRate));
        int i1 = Math.Min(frames, (int)Math.Ceiling((relEnd - winStartSec) * outRate));
        if (i1 <= i0) return;

        bool isRoll = ornament == "roll";
        bool isSwell = ornament == "swell";
        int ordKind = ornament switch { "grace" => 1, "mordent" => 2, "slide" => 3, _ => 0 };

        // Percussion zones map root pitch gently unless pitched timpani/bell
        double semiShift = voice.Category == 4 && voiceId is not ("tubular_bell" or "war_drum")
            ? (midi - zone.RootMidi) * 0.35
            : (midi - zone.RootMidi);
        double baseStep = Math.Pow(2.0, semiShift / 12.0) * (zone.SampleRate / (double)outRate);

        float vel = Math.Clamp(velocity, 0.05f, 1.25f);
        // Subtle stateless brightness blend: soft velocities average adjacent samples for darker tone
        float dull = Math.Clamp((0.88f - vel) * 0.7f, 0f, 0.55f);
        float amp = vel * trackGain;
        float panClamped = Math.Clamp(pan, -1f, 1f);
        float angle = (panClamped + 1f) * (MathF.PI * 0.25f);
        float gainL = MathF.Cos(angle) * amp, gainR = MathF.Sin(angle) * amp;

        float atk = isSwell ? MathF.Max(voice.Attack, (float)(durSec * 0.42)) : voice.Attack;
        float rel = MathF.Max(0.02f, voice.Release);
        double vibW = Math.Tau * Math.Max(0.1, voice.VibRate);
        double vibScale = voice.VibRate > 0.05f ? voice.VibDepth / vibW : 0.0;

        var smp = zone.Samples;
        int nSmp = smp.Length, ls = zone.LoopStart, le = zone.LoopEnd;
        bool looped = le > ls + 8 && le <= nSmp;
        int loopSpan = le - ls;

        double invRate = 1.0 / outRate;
        for (int i = i0; i < i1; i++)
        {
            double u = winStartSec + i * invRate - noteStartSec;
            if (u < 0) continue;

            // Envelope
            float env;
            if (isRoll && u < durSec)
            {
                double strikePeriod = 1.0 / 13.5;
                double localU = u % strikePeriod;
                float swell = 0.45f + 0.55f * (float)Math.Min(1.0, u / Math.Max(0.08, durSec));
                env = swell * (float)Math.Exp(-localU * 9.0);
                u = localU; // re-trigger sample head cleanly for tremolando/drum roll
            }
            else
            {
                float aEnv = atk > 1e-4f ? (float)Math.Min(1.0, u / atk) : 1f;
                float rEnv = u <= durSec ? 1f : (float)Math.Max(0.0, 1.0 - (u - durSec) / rel);
                if (isSwell && u <= durSec && durSec > 0.1)
                    rEnv *= 0.35f + 0.65f * MathF.Sin((float)(Math.PI * Math.Min(1.0, u / durSec)));
                env = aEnv * rEnv * rEnv;
            }
            if (env <= 1e-5f) continue;

            // Analytical pitch integral (u + ornament bend integral + vibrato integral)
            double phaseTime = u + OrnamentIntegral(ordKind, u);
            if (vibScale > 0 && u > 0.12)
            {
                double uv = u - 0.12;
                double ramp = Math.Min(1.0, uv * 3.0);
                phaseTime += vibScale * ramp * (1.0 - Math.Cos(vibW * uv));
            }

            double pos = phaseTime * outRate * baseStep;
            if (!looped && pos >= nSmp - 2) continue;

            float s0 = SampleHermite(smp, nSmp, looped, ls, le, loopSpan, pos);
            if (dull > 0.01f && pos >= 1.0)
            {
                float sPrev = SampleLinear(smp, nSmp, looped, ls, le, loopSpan, pos - 1.0);
                s0 = s0 * (1f - dull) + 0.5f * (s0 + sPrev) * dull;
            }

            float vOut = s0 * env;
            int di = i * 2;
            dst[di] += vOut * gainL;
            dst[di + 1] += vOut * gainR;
        }
    }

    static Zone PickZone(Voice v, int midi)
    {
        foreach (var z in v.Zones)
            if (midi >= z.LowMidi && midi <= z.HighMidi) return z;
        return midi < v.Zones[0].LowMidi ? v.Zones[0] : v.Zones[^1];
    }

    /// Exact time-warping integral for grace (+2 st for 50 ms), mordent (0 -> +2 -> 0 over 100 ms), and slide (-2 -> 0 over 90 ms).
    static double OrnamentIntegral(int kind, double u)
    {
        const double up2 = 1.122462048309373 - 1.0;   // 2^(2/12) - 1
        const double dn2 = 0.8908987181403393 - 1.0;  // 2^(-2/12) - 1
        return kind switch
        {
            1 => Math.Min(u, 0.050) * up2,
            2 => u <= 0.035 ? 0.0 : (Math.Min(u, 0.085) - 0.035) * up2,
            3 => u < 0.090 ? dn2 * (u - u * u / 0.180) : dn2 * 0.045,
            _ => 0.0,
        };
    }

    static int WrapIdx(int idx, int nSmp, bool looped, int ls, int le, int loopSpan)
    {
        if (idx < 0) return 0;
        if (!looped) return idx < nSmp ? idx : nSmp - 1;
        if (idx < le) return idx;
        return ls + (idx - ls) % loopSpan;
    }

    static float SampleLinear(float[] smp, int nSmp, bool looped, int ls, int le, int loopSpan, double pos)
    {
        int idx = (int)pos;
        float frac = (float)(pos - idx);
        float a = smp[WrapIdx(idx, nSmp, looped, ls, le, loopSpan)];
        float b = smp[WrapIdx(idx + 1, nSmp, looped, ls, le, loopSpan)];
        return a + (b - a) * frac;
    }

    static float SampleHermite(float[] smp, int nSmp, bool looped, int ls, int le, int loopSpan, double pos)
    {
        int idx = (int)pos;
        float t = (float)(pos - idx);
        float y0 = smp[WrapIdx(idx - 1, nSmp, looped, ls, le, loopSpan)];
        float y1 = smp[WrapIdx(idx, nSmp, looped, ls, le, loopSpan)];
        float y2 = smp[WrapIdx(idx + 1, nSmp, looped, ls, le, loopSpan)];
        float y3 = smp[WrapIdx(idx + 2, nSmp, looped, ls, le, loopSpan)];
        float c0 = y1;
        float c1 = 0.5f * (y2 - y0);
        float c2 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
        float c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);
        return ((c3 * t + c2) * t + c1) * t + c0;
    }
}
