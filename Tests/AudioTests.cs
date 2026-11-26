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

    /// The map's music holds steady (MusicDirector.cs): flicking through a town's tabs starts one piece, not one a tab;
    /// an enemy host coming and going never cuts a piece; the map piece resumes after a short town visit; pieces end
    /// before the next begins, never the same twice running; lasting dread may cut one, but not at once.
    static void MapMusicSteady()
    {
        var d = new MusicDirector(7);
        string playing = ""; double at = 0, ran = 0;
        var starts = new List<(double T, string Id, double At)>();
        double clock = 0;
        const float dt = 1 / 30f;
        void Run(double secs, Func<double, string?> interior, Func<double, bool> host, float dread = 0)
        {
            for (double t = 0; t < secs; t += dt, clock += dt)
            {
                ran += dt;
                var score = MusicLibrary.Get(playing);
                bool finished = playing == "" || score != null && !score.Loop && at + ran >= score.TotalPlaySec;
                var pool = MusicDirector.Pool("azure", false, false, host(t), dread);
                if (d.Update(interior(t), pool, dread >= 0.55f, dt, finished, at + ran) is { } c)
                {
                    starts.Add((clock, c.Id, c.At));
                    (playing, at, ran) = (c.Id, c.At, 0);
                }
            }
        }

        Run(30, _ => null, _ => false);
        Check(starts.Count == 1 && starts[0].Id is "azure_day" or "azure_peace", $"the map starts one piece ({starts.Count})");
        string mapPiece = playing;

        // An enemy host passes by every 10 s for five minutes: it never cuts the piece playing.
        int before = starts.Count;
        Run(300, _ => null, t => (int)(t / 10) % 2 == 0);
        foreach (var s in starts.Skip(before))
            Check(starts[starts.IndexOf(s) - 1] is var p && MusicLibrary.Get(p.Id) is { } ps && s.T - p.T >= ps.TotalPlaySec - p.At - 0.1,
                $"{s.Id} began only after {starts[starts.IndexOf(s) - 1].Id} ended");
        for (int i = 1; i < starts.Count; i++) Check(starts[i].Id != starts[i - 1].Id, $"never the same piece twice running ({starts[i].Id})");

        // Into a town: the tabs flick every 2 s for 40 s, then 20 s in the tavern; one town piece, then the tavern's.
        before = starts.Count;
        mapPiece = playing;
        double mapAt = at + ran;
        Run(40, t => ((int)(t / 2) % 3) switch { 0 => "town_market", 1 => "tavern_reel_1", _ => "lords_hall" }, _ => false);
        Run(20, _ => "tavern_reel_1", _ => false);
        var town = starts.Skip(before).ToList();
        Check(town.Count == 2 && town[0].Id == "town_market" && town[1].Id == "tavern_reel_1", $"flicking tabs starts one piece, then the tavern's held tab ({string.Join(", ", town.Select(s => s.Id))})");

        // Back on the map within two minutes: the map piece resumes where it was left (it played on while the town settled).
        Run(5, _ => null, _ => false);
        var back = starts[^1];
        if (MusicLibrary.Get(mapPiece) is { } mp && mapAt < mp.DurationSec - 10)
            Check(back.Id == mapPiece && Math.Abs(back.At - mapAt - MusicDirector.Settle) < 0.2, $"the map piece resumes after a short visit ({back.Id} at {back.At:0.0}s, left {mapPiece} at {mapAt:0.0}s)");

        // Dread held: it cuts the piece, but only after it has lasted and not twice in a row.
        before = starts.Count;
        Run(120, _ => null, _ => false, 0.9f);
        var dread = starts.Skip(before).ToList();
        Check(dread.Count >= 1 && dread[0].Id is "black_flame" or "azure_night", $"lasting dread brings its music ({string.Join(", ", dread.Select(s => s.Id))})");
        var prior = starts[before - 1];
        bool cut = dread.Count > 0 && MusicLibrary.Get(prior.Id) is { } pr && dread[0].T - prior.T < pr.TotalPlaySec - prior.At - 0.1;
        Check(!cut || dread[0].T - prior.T >= MusicDirector.MinHold - 0.1, "dread does not cut a piece that has only just begun");
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
                if (Synth.InWorld(c))
                {
                    var (fp, _, fok) = Level(Synth.Far(b));
                    Check(fok && fp >= p * 0.85f && fp <= p * 1.1f, $"{c} far version keeps its loudness (peak {fp:0.00} vs {p:0.00})");
                }
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
        Check(MusicLibrary.All.Count >= 68, $"music library loaded {MusicLibrary.All.Count} scores (expected >= 68)");

        // Fast window verification across every JSON score (opening, midpoint, 4-stem escalation and loop seam)
        int winFrames = Synth.Rate * 2; // 2.0 s window
        var winBuf = new float[winFrames * 2];
        var deployBuf = new float[winFrames * 2];
        ReadOnlySpan<float> allStems = stackalloc float[] { 1f, 1f, 1f, 1f };
        ReadOnlySpan<float> deployOnly = stackalloc float[] { 1f, 0f, 0f, 0f };
        foreach (var s in MusicLibrary.All.Values)
        {
            Array.Clear(winBuf);
            double mid = Math.Max(0.0, s.DurationSec * 0.45);
            s.RenderStereoWindow(bank, winBuf, winFrames, Synth.Rate, mid, allStems);
            MusicScore.SoftLimitStereo(winBuf, winBuf.Length);
            var (p, rms, ok) = Level(winBuf);
            Check(ok && p > 0.015f && p <= 1f && rms > 0.002f, $"score {s.Id} sounds clean (peak {p:0.00}, rms {rms:0.000})");

            if (s.Category == "battle" && s.Id != "creator_hearth")
            {
                Array.Clear(deployBuf);
                s.RenderStereoWindow(bank, deployBuf, winFrames, Synth.Rate, mid, deployOnly);
                var depLvl = Level(deployBuf);
                Check(depLvl is { Finite: true, Rms: > 0.0015f }, $"battle score {s.Id} deploy stem is audible (rms {depLvl.Rms:0.000})");
                Check(rms > depLvl.Rms * 1.25f, $"battle score {s.Id} swells from deploy ({depLvl.Rms:0.000}) to full fight ({rms:0.000})");
            }

            if (s.Loop)
            {
                Array.Clear(winBuf);
                s.RenderStereoWindow(bank, winBuf, winFrames, Synth.Rate, Math.Max(0, s.DurationSec - 1.0), allStems);
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

        // Verify any drop-in OGG assets (SFX, beds, music) decode cleanly without NaN or clipping
        int oggCues = 0, oggBeds = 0, oggMusic = 0;
        foreach (Cue c in Enum.GetValues<Cue>())
            if (AudioAssets.LoadCue(c) is { } cv)
            {
                oggCues++;
                foreach (var b in cv) Check(Level(b) is { Finite: true, Peak: > 0.005f and <= 1.01f }, $"ogg cue {c} clean");
            }
        foreach (Bed bed in Enum.GetValues<Bed>())
            if (AudioAssets.LoadBed(bed) is { } bv)
            {
                oggBeds++;
                Check(Level(bv) is { Finite: true, Peak: > 0.005f and <= 1.01f }, $"ogg bed {bed} clean");
            }
        var oggChunk = new float[AudioStreamer.ChunkFrames * 2];
        foreach (var asset in AudioAssets.AllMusic.Values)
        {
            oggMusic++;
            using var st = new OggMusicStream(asset, 0);
            Array.Clear(oggChunk);
            st.RenderChunk(oggChunk, AudioStreamer.ChunkFrames, Synth.Rate, allStems, asset.Loop, out _);
            Check(Level(oggChunk) is { Finite: true, Peak: <= 1.05f }, $"ogg music {asset.Id} streams cleanly");
        }

        Console.WriteLine($"sounds: {Enum.GetValues<Cue>().Length} cues ({oggCues} ogg) + {Enum.GetValues<Bed>().Length} beds ({oggBeds} ogg) in {cuesMs:0} ms, {MusicLibrary.All.Count} scores + {oggMusic} ogg music checked in {watch.ElapsedMilliseconds - cuesMs:0} ms");
    }

    /// Weapon, shield, ranged and lance fights emit their distinct AU1 cues without touching replay determinism.
    static void CombatCuesEmitted()
    {
        var seen = new HashSet<Cue>();
        var b = Battle.Empty(42);
        var xbows = b.AddSquad(0, Troops.Get("crossbowmen"), 8, new(-12, -18), 8, Microsoft.Xna.Framework.Vector2.UnitY);
        var greats = b.AddSquad(0, Troops.Get("greatswords"), 10, new(0, -4), 5, Microsoft.Xna.Framework.Vector2.UnitY);
        var lancers = b.AddSquad(0, Troops.Get("brevon_lancers"), 6, new(14, -22), 3, Microsoft.Xna.Framework.Vector2.UnitY);
        var foes = b.AddSquad(1, Troops.Get("manor_foot"), 18, new(0, 4), 6, -Microsoft.Xna.Framework.Vector2.UnitY);
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

    /// Monsters, beasts and skeletons emit their distinct AU2 vocals, web-spits and bone-clatter cues.
    static void CreatureAndMagicCues()
    {
        var seen = new HashSet<Cue>();
        var b = Battle.Empty(77);
        var trolls = b.AddSquad(0, Troops.Get("troll"), 2, new(-8, -3), 2, Microsoft.Xna.Framework.Vector2.UnitY);
        var spiders = b.AddSquad(0, Troops.Get("spider"), 4, new(8, -10), 2, Microsoft.Xna.Framework.Vector2.UnitY);
        var wyverns = b.AddSquad(0, Troops.Get("wyvern"), 2, new(0, -14), 2, Microsoft.Xna.Framework.Vector2.UnitY);
        var skels = b.AddSquad(1, Troops.Get("skeleton"), 14, new(0, 3), 7, -Microsoft.Xna.Framework.Vector2.UnitY);
        trolls.Order = spiders.Order = wyverns.Order = skels.Order = Order.Charge;
        for (int i = 0; i < 60 * 10; i++)
        {
            b.Update(1 / 60f);
            foreach (var (cue, _) in b.Sounds) seen.Add(cue);
        }
        Check(seen.Contains(Cue.TrollSweep), "trolls emit TrollSweep");
        Check(seen.Contains(Cue.WebSpit) || seen.Contains(Cue.SpiderChitter), "spiders emit WebSpit/SpiderChitter");
        Check(seen.Contains(Cue.WyvernScreech) || seen.Contains(Cue.WyvernFlap), "wyverns emit WyvernScreech/WyvernFlap");
        Check(seen.Contains(Cue.BoneHit) && seen.Contains(Cue.BoneCollapse), "skeletons rattle on hit and collapse on death");
    }


    /// Battle ambience holds its level: the field asks for its beds ten times a second (BattleScreen.Soundscape),
    /// and a bed no longer asked for still fades out (AudioEngine.Update / BedStep).
    static void BedFollows()
    {
        float level = 0, want = 0;
        const float dt = 1 / 60f;
        for (int frame = 0; frame < 60 * 2; frame++)
        {
            want = frame % 6 == 0 ? 1f : MathF.Max(0, want - dt); // SetBed 10x a second, then the frame's decay
            level = AudioEngine.BedStep(level, want, dt);
        }
        Check(level > 0.85f, $"a battle bed at 10 Hz lands near its target ({level:0.00})");
        for (int frame = 0; frame < 60 * 2; frame++)
        {
            want = MathF.Max(0, want - dt);
            level = AudioEngine.BedStep(level, want, dt);
        }
        Check(level < 0.15f, $"a bed no longer asked for fades out ({level:0.00})");
    }
}
