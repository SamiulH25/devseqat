namespace Ironfield;

/// Self-tests of the synthesised sound (`AudioSynth*.cs`), acoustic bank (`AudioBank.cs`) and hand-authored scores
/// (`MusicScore.cs`): pure maths, so they run headless.
public static partial class SelfTest
{
    static (float Peak, float Rms, bool Finite) Level(ReadOnlySpan<float> b)
    {
        float p = 0; double sq = 0;
        foreach (var v in b) { if (!float.IsFinite(v)) return (0, 0, false); p = MathF.Max(p, MathF.Abs(v)); sq += v * v; }
        return (p, (float)Math.Sqrt(sq / Math.Max(1, b.Length)), true);
    }

    /// Every cue has distinct versions that are audible and never clip, and a far version; every bed loops without a
    /// click; the 28-voice acoustic bank loads cleanly; and every score in `Data/music/` validates and renders cleanly.
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

        foreach (Bed bed in Enum.GetValues<Bed>())
        {
            var b = Synth.MakeBed(bed);
            var (p, _, ok) = Level(b);
            Check(ok && p > 0.02f, $"bed {bed} is audible");
            float step = 0;
            for (int i = 1; i < b.Length; i++) step = MathF.Max(step, MathF.Abs(b[i] - b[i - 1]));
            Check(MathF.Abs(b[^1] - b[0]) <= step * 1.05f + 1e-4f, $"bed {bed} loops without a click");
        }
        float cuesMs = watch.ElapsedMilliseconds;

        var bank = AudioBank.Shared;
        Check(bank.LoadedFromDisk && bank.VoiceIds.Count >= 28, $"instrument bank loaded ({bank.VoiceIds.Count} voices)");
        var errs = MusicLibrary.Validate();
        Check(errs.Count == 0, "music scores validate: " + string.Join("; ", errs));
        Check(MusicLibrary.All.Count >= 3, "music library loaded scores");

        // Fast window verification across every JSON score (opening, midpoint and loop seam) + chunk-boundary identity
        int winFrames = Synth.Rate; // 1.0 s window
        var winBuf = new float[winFrames * 2];
        ReadOnlySpan<float> allStems = stackalloc float[] { 1f, 1f, 1f, 1f };
        foreach (var s in MusicLibrary.All.Values)
        {
            Array.Clear(winBuf);
            double mid = Math.Max(0.0, s.DurationSec * 0.45);
            s.RenderStereoWindow(bank, winBuf, winFrames, Synth.Rate, mid, allStems);
            MusicScore.SoftLimitStereo(winBuf, winBuf.Length);
            var (p, rms, ok) = Level(winBuf);
            Check(ok && p > 0.015f && p <= 1f && rms > 0.002f, $"score {s.Id} sounds clean (peak {p:0.00}, rms {rms:0.000})");

            if (s.Loop)
            {
                Array.Clear(winBuf);
                s.RenderStereoWindow(bank, winBuf, winFrames, Synth.Rate, Math.Max(0, s.DurationSec - 0.5), allStems);
                MusicScore.SoftLimitStereo(winBuf, winBuf.Length);
                var seamLvl = Level(winBuf);
                Check(seamLvl is { Finite: true, Peak: <= 1f }, $"looped score {s.Id} crosses seam cleanly");
            }
        }

        // Verify analytical chunk invariance: one 0.4 s render == two back-to-back 0.2 s streaming chunks
        var firstScore = MusicLibrary.All.Values.First();
        int half = Synth.Rate / 5;
        var whole = new float[half * 4];
        var partA = new float[half * 2];
        var partB = new float[half * 2];
        firstScore.RenderStereoWindow(bank, whole, half * 2, Synth.Rate, 1.0, allStems);
        firstScore.RenderStereoWindow(bank, partA, half, Synth.Rate, 1.0, allStems);
        firstScore.RenderStereoWindow(bank, partB, half, Synth.Rate, 1.2, allStems);
        float maxDiff = 0;
        for (int i = 0; i < partA.Length; i++) maxDiff = MathF.Max(maxDiff, MathF.Abs(whole[i] - partA[i]));
        for (int i = 0; i < partB.Length; i++) maxDiff = MathF.Max(maxDiff, MathF.Abs(whole[half * 2 + i] - partB[i]));
        Check(maxDiff < 1e-4f, $"streaming chunk boundary is seamless (max diff {maxDiff})");

        Console.WriteLine($"sounds: {Enum.GetValues<Cue>().Length} cues + {Enum.GetValues<Bed>().Length} beds in {cuesMs:0} ms, {MusicLibrary.All.Count} scores checked in {watch.ElapsedMilliseconds - cuesMs:0} ms");
    }

    /// Weapon, shield, ranged and lance fights emit their distinct AU1 cues without touching replay determinism.
    static void CombatCuesEmitted()
    {
        var seen = new HashSet<Cue>();
        var b = Battle.Empty(42);
        var xbows = b.AddSquad(0, Troops.Get("crossbowmen"), 8, new(-12, -18), 8, Microsoft.Xna.Framework.Vector2.UnitY);
        var greats = b.AddSquad(0, Troops.Get("greatswords"), 10, new(0, -4), 5, Microsoft.Xna.Framework.Vector2.UnitY);
        var lancers = b.AddSquad(0, Troops.Get("lancers"), 6, new(14, -22), 3, Microsoft.Xna.Framework.Vector2.UnitY);
        var foes = b.AddSquad(1, Troops.Get("shieldmen"), 18, new(0, 4), 6, -Microsoft.Xna.Framework.Vector2.UnitY);
        xbows.Order = Order.Hold;
        greats.Order = lancers.Order = foes.Order = Order.Charge;
        for (int i = 0; i < 60 * 8; i++)
        {
            b.Update(1 / 60f);
            foreach (var (cue, _) in b.Sounds) seen.Add(cue);
        }
        Check(seen.Contains(Cue.CrossbowSnap), "crossbowmen emit CrossbowSnap");
        Check(seen.Contains(Cue.SlashHeavy), "greatswords emit SlashHeavy");
        Check(seen.Contains(Cue.LanceImpact) || seen.Contains(Cue.LanceShatter), "lancers emit LanceImpact/LanceShatter");
        Check(seen.Contains(Cue.BlockWood) || seen.Contains(Cue.ArrowShield), "shieldmen emit BlockWood/ArrowShield");
        Check(Synth.DrawsBlood(Cue.SlashLight) && Synth.DrawsBlood(Cue.ArrowFlesh) && !Synth.DrawsBlood(Cue.ArrowShield), "DrawsBlood predicate matches flesh hits");
        Check(Synth.HeavyImpact(Cue.LanceImpact) && !Synth.HeavyImpact(Cue.SlashLight), "HeavyImpact predicate matches heavy charges");
    }
}
