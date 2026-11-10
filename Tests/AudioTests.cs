namespace Ironfield;

/// Self-tests of the synthesised sound (AudioSynth.cs, AudioMusic.cs): pure maths, so they run headless.
public static partial class SelfTest
{
    /// Peak and RMS of a buffer; false if any sample is not a number.
    static (float Peak, float Rms, bool Finite) Level(float[] b)
    {
        float p = 0; double sq = 0;
        foreach (var v in b) { if (!float.IsFinite(v)) return (0, 0, false); p = MathF.Max(p, MathF.Abs(v)); sq += v * v; }
        return (p, (float)Math.Sqrt(sq / Math.Max(1, b.Length)), true);
    }

    /// Every cue has several different versions that are audible and never clip, and a far version; every bed loops
    /// without a click; every piece of music is sound, long enough, and the battle's two layers loop together.
    static void SoundsMade()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (Cue c in Enum.GetValues<Cue>())
        {
            var v = Synth.Cues(c);
            Check(v.Length >= 3, $"{c} has several versions");
            foreach (var b in v)
            {
                var (p, rms, ok) = Level(b);
                Check(ok && p > 0.1f && p <= 1f && rms > 0.004f, $"{c} is audible and does not clip (peak {p:0.00}, rms {rms:0.000})");
                if (Synth.InWorld(c)) Check(Level(Synth.Far(b)) is { Finite: true, Peak: > 0.01f }, $"{c} has a far version");
            }
            Check(!v[0].AsSpan().SequenceEqual(v[1]), $"{c}'s versions differ");
        }
        float[][] beds = { Synth.Wind(), Synth.Gallop(), Synth.Din(), Synth.Roar(), Synth.March(), Synth.Rain(), Synth.Birds(), Synth.Crickets(), Synth.Town() };
        foreach (var b in beds)
        {
            var (p, _, ok) = Level(b);
            Check(ok && p > 0.02f, "a bed is audible");
            float step = 0;
            for (int i = 1; i < b.Length; i++) step = MathF.Max(step, MathF.Abs(b[i] - b[i - 1]));
            Check(MathF.Abs(b[^1] - b[0]) <= step, $"bed {Array.IndexOf(beds, b)} loops without a click (a seam no bigger than its own steps)");
        }
        float cues = watch.ElapsedMilliseconds;
        var music = Synth.Pieces.ToDictionary(id => id, Synth.Piece);
        foreach (var (id, b) in music)
        {
            var (p, rms, ok) = Level(b);
            Check(ok && p <= 1f && rms > 0.01f, $"music {id} is sound (peak {p:0.00}, rms {rms:0.000})");
            Check(b.Length > Synth.Rate * (id is "victory" or "defeat" ? 4 : 15), $"music {id} is long enough");
        }
        Check(music["battle"].Length == music["battle2"].Length, "the battle's two music layers are the same length");
        Console.WriteLine($"sounds: {Enum.GetValues<Cue>().Length} cues in {cues:0} ms, music in {watch.ElapsedMilliseconds - cues:0} ms");
    }
}
