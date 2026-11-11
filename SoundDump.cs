namespace Ironfield;

/// `--sounds dir [filter]`: writes synthesised cues, ambient beds and stereo music scores as 44.1 kHz WAV files.
static class SoundDump
{
    public static void Run(string dir, string? filter = null)
    {
        Directory.CreateDirectory(dir);
        bool Match(string name) => string.IsNullOrEmpty(filter) || name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        var gap = Synth.Buf(0.35f);
        int wrote = 0;

        foreach (Cue c in Enum.GetValues<Cue>())
        {
            string tag = $"cue-{c.ToString().ToLowerInvariant()}";
            if (!Match(tag)) continue;
            var v = Synth.Cues(c);
            var parts = v.SelectMany(b => new[] { b, gap }).ToList();
            if (Synth.InWorld(c)) parts.AddRange(v.Select(Synth.Far).SelectMany(b => new[] { b, gap }));
            WriteMono(Path.Combine(dir, $"{tag}.wav"), parts.SelectMany(b => b).ToArray());
            wrote++;
        }

        foreach (Bed bed in Enum.GetValues<Bed>())
        {
            string tag = $"bed-{bed.ToString().ToLowerInvariant()}";
            if (!Match(tag)) continue;
            var b = Synth.MakeBed(bed);
            WriteMono(Path.Combine(dir, $"{tag}.wav"), b.Concat(b).ToArray());
            wrote++;
        }

        var bank = AudioBank.Shared;
        ReadOnlySpan<float> fullStems = stackalloc float[] { 1f, 1f, 1f, 1f };
        foreach (var score in MusicLibrary.All.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            string tag = $"score-{score.Id}";
            if (!Match(tag) && !Match(score.Id)) continue;
            WriteStereo(Path.Combine(dir, $"{tag}.wav"), score.RenderFullStereo(bank, Synth.Rate, fullStems));
            wrote++;
        }

        foreach (var id in Synth.Pieces)
        {
            if (MusicLibrary.Has(id)) continue;
            string tag = $"music-{id}";
            if (!Match(tag)) continue;
            WriteMono(Path.Combine(dir, $"{tag}.wav"), Synth.Piece(id));
            wrote++;
        }

        Console.WriteLine($"wrote {wrote} sound files to {dir}");
    }

    static void WriteMono(string path, float[] s)
    {
        using var w = new BinaryWriter(File.Create(path));
        int bytes = s.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Synth.Rate); w.Write(Synth.Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(bytes);
        foreach (var v in s) w.Write((short)(Math.Clamp(v, -1, 1) * 32000));
    }

    static void WriteStereo(string path, float[] interleavedStereo)
    {
        using var w = new BinaryWriter(File.Create(path));
        int bytes = interleavedStereo.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2);
        w.Write(Synth.Rate); w.Write(Synth.Rate * 4); w.Write((short)4); w.Write((short)16);
        w.Write("data"u8); w.Write(bytes);
        foreach (var v in interleavedStereo) w.Write((short)(Math.Clamp(v, -1, 1) * 32000));
    }
}
