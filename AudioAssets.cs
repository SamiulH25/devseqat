using System.Text.Json;
using System.Text.RegularExpressions;
using NVorbis;

namespace Ironfield;

/// Drop-in OGG Vorbis asset loader and multi-stem music streamer (`Data/audio/sfx/`, `Data/audio/beds/`,
/// `Data/audio/music/`, plus optional `Data/audio/mix.json`). Pure C# + NVorbis (no MonoGame types), so headless
/// `--selftest` and `--sounds` use it directly. Any cue, bed or track without an `.ogg` on disk falls back to the
/// procedural synth (`AudioSynth*.cs`) or `MusicScore`.
public static class AudioAssets
{
    public const string AudioRoot = "Data/audio";

    public sealed class CueMix { public float Gain = 1f; public float PitchSpread = -1f; public string? Alias; }
    public sealed class BedMix { public float Gain = 1f; }
    public sealed class MusicMix { public float Gain = 1f; public bool? Loop; }

    sealed class MixConfig
    {
        public Dictionary<string, CueMix> Cues = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, BedMix> Beds = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, MusicMix> Music = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class MusicAsset
    {
        public string Id = "";
        public string?[] StemPaths = new string?[4];
        public bool MultiStem;
        public double DurationSec;
        public bool Loop = true;
        public float Gain = 1f;
    }

    sealed class Catalog
    {
        public MixConfig Mix = new();
        public Dictionary<string, List<string>> Sfx = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Beds = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, MusicAsset> Music = new(StringComparer.OrdinalIgnoreCase);
    }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    static readonly Regex VariantSuffix = new(@"_\d+$", RegexOptions.Compiled);
    static readonly Regex StemSuffix = new(@"_s([0-3])$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static Catalog? cached;
    static readonly object gate = new();

    static Catalog Current
    {
        get
        {
            if (cached != null) return cached;
            lock (gate)
            {
                if (cached != null) return cached;
                cached = Scan(FindRoot());
                return cached;
            }
        }
    }

    static string? FindRoot() => new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
        .Select(d => Path.Combine(d, AudioRoot))
        .FirstOrDefault(Directory.Exists);

    static Catalog Scan(string? root)
    {
        var cat = new Catalog();
        if (root == null) return cat;

        string mixPath = Path.Combine(root, "mix.json");
        if (File.Exists(mixPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<MixConfig>(File.ReadAllText(mixPath), JsonOpts);
                if (loaded != null)
                {
                    cat.Mix.Cues = new Dictionary<string, CueMix>(loaded.Cues, StringComparer.OrdinalIgnoreCase);
                    cat.Mix.Beds = new Dictionary<string, BedMix>(loaded.Beds, StringComparer.OrdinalIgnoreCase);
                    cat.Mix.Music = new Dictionary<string, MusicMix>(loaded.Music, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception e) { Console.WriteLine("audio mix.json: " + e.Message); }
        }

        string sfxDir = Path.Combine(root, "sfx");
        if (Directory.Exists(sfxDir))
        {
            foreach (var file in Directory.GetFiles(sfxDir, "*.ogg").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                string key = VariantSuffix.Replace(stem, "");
                if (!cat.Sfx.TryGetValue(key, out var list)) cat.Sfx[key] = list = new List<string>();
                list.Add(file);
            }
        }

        string bedDir = Path.Combine(root, "beds");
        if (Directory.Exists(bedDir))
        {
            foreach (var file in Directory.GetFiles(bedDir, "*.ogg").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                cat.Beds[Path.GetFileNameWithoutExtension(file)] = file;
        }

        string musDir = Path.Combine(root, "music");
        if (Directory.Exists(musDir))
        {
            var groups = new Dictionary<string, string?[]>(StringComparer.OrdinalIgnoreCase);
            var singles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(musDir, "*.ogg").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                var m = StemSuffix.Match(name);
                if (m.Success)
                {
                    string id = name[..m.Index];
                    int s = m.Groups[1].Value[0] - '0';
                    if (!groups.TryGetValue(id, out var arr)) groups[id] = arr = new string?[4];
                    arr[s] = file;
                }
                else
                {
                    singles[name] = file;
                }
            }
            foreach (var (id, arr) in groups)
                if (BuildMusicAsset(id, arr, multiStem: true, cat.Mix) is { } asset) cat.Music[id] = asset;
            foreach (var (id, path) in singles)
            {
                if (cat.Music.ContainsKey(id)) continue;
                if (BuildMusicAsset(id, new[] { path, null, null, null }, multiStem: false, cat.Mix) is { } asset)
                    cat.Music[id] = asset;
            }
        }

        return cat;
    }

    static MusicAsset? BuildMusicAsset(string id, string?[] paths, bool multiStem, MixConfig mix)
    {
        string? probe = paths.FirstOrDefault(p => p != null);
        if (probe == null) return null;
        double dur;
        try
        {
            using var vr = new VorbisReader(probe);
            dur = vr.TotalTime.TotalSeconds;
        }
        catch (Exception e)
        {
            Console.WriteLine($"audio music '{id}': {e.Message}");
            return null;
        }
        if (dur <= 0.05) return null;

        mix.Music.TryGetValue(id, out var cfg);
        bool defaultLoop = MusicLibrary.Get(id) is { } sc
            ? sc.Loop
            : !(id.StartsWith("victory_", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("defeat_", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("sting_", StringComparison.OrdinalIgnoreCase));

        return new MusicAsset
        {
            Id = id,
            StemPaths = paths,
            MultiStem = multiStem,
            DurationSec = dur,
            Loop = cfg?.Loop ?? defaultLoop,
            Gain = cfg?.Gain ?? 1f,
        };
    }

    public static bool HasCue(Cue c)
    {
        var cat = Current;
        string name = c.ToString();
        if (cat.Mix.Cues.TryGetValue(name, out var m) && !string.IsNullOrWhiteSpace(m.Alias)) name = m.Alias;
        return cat.Sfx.ContainsKey(name);
    }

    public static float[][]? LoadCue(Cue c)
    {
        var cat = Current;
        string key = c.ToString();
        cat.Mix.Cues.TryGetValue(key, out var mix);
        string lookup = !string.IsNullOrWhiteSpace(mix?.Alias) ? mix!.Alias! : key;
        if (!cat.Sfx.TryGetValue(lookup, out var files) || files.Count == 0) return null;

        float gain = mix?.Gain ?? 1f;
        var list = new List<float[]>(files.Count);
        foreach (var path in files)
        {
            try
            {
                var pcm = DecodeMono(path, Synth.Rate);
                if (MathF.Abs(gain - 1f) > 1e-3f)
                    for (int i = 0; i < pcm.Length; i++) pcm[i] = Math.Clamp(pcm[i] * gain, -1f, 1f);
                if (pcm.Length > 0) list.Add(pcm);
            }
            catch (Exception e) { Console.WriteLine($"audio sfx '{path}': {e.Message}"); }
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    public static float CuePitchSpread(Cue c, float fallback) =>
        Current.Mix.Cues.TryGetValue(c.ToString(), out var m) && m.PitchSpread >= 0f ? m.PitchSpread : fallback;

    public static float[]? LoadBed(Bed b)
    {
        var cat = Current;
        string key = b.ToString();
        if (!cat.Beds.TryGetValue(key, out var path)) return null;
        try
        {
            var pcm = DecodeMono(path, Synth.Rate);
            float gain = cat.Mix.Beds.TryGetValue(key, out var m) ? m.Gain : 1f;
            if (MathF.Abs(gain - 1f) > 1e-3f)
                for (int i = 0; i < pcm.Length; i++) pcm[i] = Math.Clamp(pcm[i] * gain, -1f, 1f);
            return pcm.Length > 0 ? pcm : null;
        }
        catch (Exception e) { Console.WriteLine($"audio bed '{path}': {e.Message}"); return null; }
    }

    public static bool HasMusic(string id) => Current.Music.ContainsKey(id);
    public static MusicAsset? GetMusic(string id) => Current.Music.TryGetValue(id, out var a) ? a : null;
    public static IReadOnlyDictionary<string, MusicAsset> AllMusic => Current.Music;

    public static double MusicDuration(string id) =>
        Current.Music.TryGetValue(id, out var a) ? a.DurationSec
        : MusicLibrary.Get(id) is { } s ? s.DurationSec : 0;

    /// Decode an OGG Vorbis file to mono float samples at `targetRate`.
    public static float[] DecodeMono(string path, int targetRate)
    {
        using var vr = new VorbisReader(path);
        int ch = vr.Channels, sr = vr.SampleRate;
        var chunk = new float[4096 * ch];
        var mono = new List<float>((int)Math.Min(int.MaxValue, Math.Max(1024, vr.TotalSamples)));
        int read;
        while ((read = vr.ReadSamples(chunk, 0, chunk.Length)) > 0)
        {
            int frames = read / ch;
            if (ch == 1)
            {
                for (int i = 0; i < frames; i++) mono.Add(chunk[i]);
            }
            else
            {
                float inv = 1f / ch;
                for (int i = 0; i < frames; i++)
                {
                    float sum = 0;
                    for (int c = 0; c < ch; c++) sum += chunk[i * ch + c];
                    mono.Add(sum * inv);
                }
            }
        }
        var arr = mono.ToArray();
        return sr == targetRate ? arr : ResampleMono(arr, sr, targetRate);
    }

    static float[] ResampleMono(float[] src, int srcRate, int dstRate)
    {
        if (src.Length < 2 || srcRate == dstRate) return src;
        int outLen = Math.Max(1, (int)Math.Round(src.Length * (dstRate / (double)srcRate)));
        var dst = new float[outLen];
        double step = srcRate / (double)dstRate;
        for (int i = 0; i < outLen; i++)
        {
            double pos = i * step;
            int idx = (int)pos;
            float frac = (float)(pos - idx);
            if (idx >= src.Length - 1) dst[i] = src[^1];
            else dst[i] = src[idx] + (src[idx + 1] - src[idx]) * frac;
        }
        return dst;
    }
}

/// Streams a pre-rendered OGG Vorbis music asset (either a single stereo track or up to 4 sample-locked stems) into
/// interleaved 44.1 kHz stereo float chunks for `AudioStreamer`.
public sealed class OggMusicStream : IDisposable
{
    readonly VorbisReader?[] readers = new VorbisReader?[4];
    readonly float[] scratch = new float[AudioStreamer.ChunkFrames * 2];
    public readonly AudioAssets.MusicAsset Asset;

    public OggMusicStream(AudioAssets.MusicAsset asset, double startSec = 0)
    {
        Asset = asset;
        for (int s = 0; s < 4; s++)
        {
            if (asset.StemPaths[s] is not { } path) continue;
            var vr = new VorbisReader(path);
            if (startSec > 0.01 && vr.TotalTime.TotalSeconds > 0.1)
            {
                double clamped = startSec % vr.TotalTime.TotalSeconds;
                vr.TimePosition = TimeSpan.FromSeconds(clamped);
            }
            readers[s] = vr;
        }
    }

    /// Mix `frames` stereo samples at `outRate` into `dstStereo` (additive). Sets `finished` if non-looping reaches EOF.
    public void RenderChunk(float[] dstStereo, int frames, int outRate, ReadOnlySpan<float> stemGains, bool loop, out bool finished)
    {
        finished = false;
        int activeReaders = 0, endedReaders = 0;
        for (int s = 0; s < 4; s++)
        {
            var vr = readers[s];
            if (vr == null) continue;
            activeReaders++;
            float gain = Asset.Gain * (Asset.MultiStem ? (s < stemGains.Length ? stemGains[s] : 0f) : 1f);
            bool eof = ReadStem(vr, dstStereo, frames, outRate, gain, loop);
            if (eof) endedReaders++;
        }
        if (!loop && activeReaders > 0 && endedReaders == activeReaders) finished = true;
    }

    bool ReadStem(VorbisReader vr, float[] dstStereo, int frames, int outRate, float gain, bool loop)
    {
        int ch = vr.Channels;
        int srcFrames = vr.SampleRate == outRate ? frames : Math.Max(1, (int)Math.Round(frames * (vr.SampleRate / (double)outRate)));
        int needSamples = srcFrames * ch;
        var buf = needSamples <= scratch.Length ? scratch : new float[needSamples];

        int got = 0;
        bool hitEof = false;
        while (got < needSamples)
        {
            int n = vr.ReadSamples(buf, got, needSamples - got);
            if (n > 0) { got += n; continue; }
            if (loop)
            {
                vr.SamplePosition = 0;
                n = vr.ReadSamples(buf, got, needSamples - got);
                if (n <= 0) { hitEof = true; break; }
                got += n;
            }
            else
            {
                hitEof = true;
                break;
            }
        }
        if (got < needSamples) Array.Clear(buf, got, needSamples - got);
        if (gain <= 1e-4f) return hitEof;

        if (vr.SampleRate == outRate)
        {
            if (ch == 2)
            {
                for (int i = 0; i < frames * 2; i++) dstStereo[i] += buf[i] * gain;
            }
            else
            {
                for (int i = 0; i < frames; i++)
                {
                    float v = buf[i * ch] * gain;
                    dstStereo[i * 2] += v;
                    dstStereo[i * 2 + 1] += v;
                }
            }
        }
        else
        {
            double step = (srcFrames - 1) / (double)Math.Max(1, frames - 1);
            for (int i = 0; i < frames; i++)
            {
                double pos = i * step;
                int idx = Math.Min(srcFrames - 1, (int)pos);
                int idx2 = Math.Min(srcFrames - 1, idx + 1);
                float f = (float)(pos - idx);
                float l = buf[idx * ch] + (buf[idx2 * ch] - buf[idx * ch]) * f;
                float r = ch >= 2 ? buf[idx * ch + 1] + (buf[idx2 * ch + 1] - buf[idx * ch + 1]) * f : l;
                dstStereo[i * 2] += l * gain;
                dstStereo[i * 2 + 1] += r * gain;
            }
        }
        return hitEof;
    }

    public void Dispose()
    {
        for (int s = 0; s < 4; s++)
        {
            readers[s]?.Dispose();
            readers[s] = null;
        }
    }
}
