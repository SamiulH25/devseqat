namespace Ironfield;

/// `--sounds dir`: every synthesised sound written as WAV files to listen to without running the game: each cue's
/// versions back to back (near, then far), each ambient bed, each piece of music, and the battle score at full fight.
static class SoundDump
{
    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var gap = Synth.Buf(0.35f);
        foreach (Cue c in Enum.GetValues<Cue>())
        {
            var v = Synth.Cues(c);
            var parts = v.SelectMany(b => new[] { b, gap }).ToList();
            if (Synth.InWorld(c)) parts.AddRange(v.Select(Synth.Far).SelectMany(b => new[] { b, gap }));
            Write(Path.Combine(dir, $"cue-{c.ToString().ToLowerInvariant()}.wav"), parts.SelectMany(b => b).ToArray());
        }
        foreach (var (name, b) in new[] { ("wind", Synth.Wind()), ("gallop", Synth.Gallop()), ("din", Synth.Din()), ("roar", Synth.Roar()), ("march", Synth.March()),
            ("rain", Synth.Rain()), ("birds", Synth.Birds()), ("crickets", Synth.Crickets()), ("town", Synth.Town()) })
            Write(Path.Combine(dir, $"bed-{name}.wav"), b.Concat(b).ToArray()); // twice, to hear the seam
        foreach (var id in Synth.Pieces) Write(Path.Combine(dir, $"music-{id}.wav"), Synth.Piece(id));
        var (low, high) = (Synth.Piece("battle"), Synth.Piece("battle2"));
        Write(Path.Combine(dir, "music-battle-full.wav"), Synth.Normalize(low.Zip(high, (a, b) => a + b).ToArray(), 0.85f));
        Console.WriteLine($"wrote sounds to {dir}");
    }

    /// 16-bit mono PCM WAV.
    static void Write(string path, float[] s)
    {
        using var w = new BinaryWriter(File.Create(path));
        int bytes = s.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Synth.Rate); w.Write(Synth.Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(bytes);
        foreach (var v in s) w.Write((short)(Math.Clamp(v, -1, 1) * 32000));
    }
}
