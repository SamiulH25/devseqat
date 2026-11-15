using System.Collections.Concurrent;
using Microsoft.Xna.Framework.Audio;

namespace Ironfield;

/// Streams 44.1 kHz stereo music through a single `DynamicSoundEffectInstance`. A background worker renders 0.20 s
/// chunks (mixing 4 sample-locked stems, crossfades and one-shot stingers through one shared `StereoHall` reverb),
/// and the main thread submits queued PCM buffers in `Pump()` so OpenAL calls stay on the main thread.
/// Every gain change is a ramp across a chunk: a piece fades in from silence and each piece it replaces fades out from
/// wherever it was, so a change in the middle of a crossfade never jumps (a jump is a click).
sealed class AudioStreamer : IDisposable
{
    public const int ChunkFrames = Synth.Rate / 5; // 0.20 s = 8,820 stereo frames (35,280 bytes)
    const int MaxQueuedChunks = 4;

    sealed class Slot
    {
        public MusicScore? Score;
        public float[]? LegacyMono;
        public string Id = "";
        public double CursorSec;
        public bool Loop, Finished;
        /// The slot's gain now (0..1): rises to 1 while it is the current piece, falls to 0 once replaced.
        public float Gain;
    }

    /// Seconds a piece takes to fade in or out; the most pieces fading out at once (past that, the quietest is let go).
    const float FadeSec = 1.5f;
    const int MaxFading = 3;

    readonly DynamicSoundEffectInstance output;
    readonly ConcurrentQueue<byte[]> ready = new();
    readonly AutoResetEvent wake = new(true);
    readonly object stateLock = new();
    readonly StereoHall hall = new(Synth.Rate);

    readonly float[] mixBuf = new float[ChunkFrames * 2];
    readonly float[] prevBuf = new float[ChunkFrames * 2];
    readonly float[] stingBuf = new float[ChunkFrames * 2];
    readonly float[] stemCur = { 1f, 0f, 0f, 0f }, stemWant = { 1f, 0f, 0f, 0f };

    Slot? current, stinger;
    readonly List<Slot> fading = new();
    float masterVol = 0.5f, stingerGain = 0.9f, duck = 1f;
    volatile bool disposed;

    public AudioStreamer()
    {
        output = new DynamicSoundEffectInstance(Synth.Rate, AudioChannels.Stereo);
        output.Volume = 1f;
        output.Play();
        var worker = new Thread(WorkerLoop) { IsBackground = true, Name = "IronfieldMusicStreamer" };
        worker.Start();
    }

    public string CurrentId { get { lock (stateLock) return current?.Id ?? ""; } }
    public bool CurrentFinished { get { lock (stateLock) return current is { Loop: false, Finished: true }; } }
    /// How far into the current piece the music has been rendered (seconds), to resume it later.
    public double CurrentCursor { get { lock (stateLock) return current?.CursorSec ?? 0; } }

    public void SetVolume(float volume) => output.Volume = Math.Clamp(volume, 0f, 1f);

    public void SetStems(float s0, float s1, float s2, float s3)
    {
        lock (stateLock)
        {
            stemWant[0] = Math.Clamp(s0, 0f, 1.2f);
            stemWant[1] = Math.Clamp(s1, 0f, 1.2f);
            stemWant[2] = Math.Clamp(s2, 0f, 1.2f);
            stemWant[3] = Math.Clamp(s3, 0f, 1.2f);
        }
    }

    /// Fade to `id` (from `startSec` in), fading out what plays now; "" fades to silence.
    public void PlayPiece(string id, float[]? legacyFallback = null, bool? forceLoop = null, double startSec = 0)
    {
        lock (stateLock)
        {
            if (current?.Id == id && (id == "" || !current.Finished)) return;
            if (current is { Gain: > 0.001f })
            {
                fading.Add(current);
                if (fading.Count > MaxFading) fading.Remove(fading.MinBy(f => f.Gain)!);
            }
            if (string.IsNullOrEmpty(id))
            {
                current = null;
            }
            else if (MusicLibrary.Get(id) is { } score)
            {
                current = new Slot { Id = id, Score = score, Loop = forceLoop ?? score.Loop, CursorSec = Math.Max(0, startSec) };
            }
            else if (legacyFallback != null)
            {
                current = new Slot { Id = id, LegacyMono = legacyFallback, Loop = forceLoop ?? (id is "battle" or "deploy") };
            }
            else
            {
                current = null;
            }
        }
        wake.Set();
    }

    public void PlayStinger(string id, float gain = 0.9f)
    {
        var score = MusicLibrary.Get(id);
        if (score == null) return;
        lock (stateLock)
        {
            stinger = new Slot { Id = id, Score = score, Loop = false };
            stingerGain = Math.Clamp(gain, 0f, 1.2f);
        }
        wake.Set();
    }

    /// Called on the main `Update` thread: feeds queued stereo PCM buffers into OpenAL.
    public void Pump(float targetVolume)
    {
        masterVol = Math.Clamp(targetVolume, 0f, 1f);
        output.Volume = masterVol;
        if (output.State != SoundState.Playing) output.Play();
        bool popped = false;
        while (output.PendingBufferCount < 3 && ready.TryDequeue(out var pcm))
        {
            output.SubmitBuffer(pcm);
            popped = true;
        }
        if (popped || ready.Count < MaxQueuedChunks) wake.Set();
    }

    void WorkerLoop()
    {
        while (!disposed)
        {
            if (ready.Count >= MaxQueuedChunks)
            {
                wake.WaitOne(40);
                continue;
            }
            try
            {
                ready.Enqueue(RenderNextChunk());
            }
            catch (Exception e)
            {
                Console.WriteLine("music stream: " + e.Message);
                wake.WaitOne(100);
            }
        }
    }

    byte[] RenderNextChunk()
    {
        var bank = AudioBank.Shared;
        Array.Clear(mixBuf, 0, mixBuf.Length);
        float wet = 0.26f, decay = 1.8f;
        double chunkSec = ChunkFrames / (double)Synth.Rate;
        ReadOnlySpan<float> fullStems = stackalloc float[] { 1f, 1f, 1f, 1f };

        lock (stateLock)
        {
            for (int s = 0; s < 4; s++)
                stemCur[s] += (stemWant[s] - stemCur[s]) * 0.22f;

            // The music dips under a stinger, eased over a third of a second rather than stepped.
            float duck0 = duck, step = (float)(chunkSec / FadeSec);
            duck = stinger is { Finished: false } ? MathF.Max(0.48f, duck - 0.6f) : MathF.Min(1f, duck + 0.3f);

            if (current != null && !current.Finished)
            {
                if (current.Score != null) { wet = current.Score.ReverbWet; decay = current.Score.ReverbDecay; }
                float g0 = current.Gain;
                current.Gain = MathF.Min(1f, g0 + step);
                RenderSlot(bank, current, mixBuf, stemCur);
                ApplyRamp(mixBuf, g0 * duck0, current.Gain * duck);
            }

            foreach (var f in fading)
            {
                float g0 = f.Gain;
                f.Gain = MathF.Max(0f, g0 - step);
                if (f.Finished) continue;
                Array.Clear(prevBuf, 0, prevBuf.Length);
                RenderSlot(bank, f, prevBuf, stemCur);
                ApplyRamp(prevBuf, g0 * duck0, f.Gain * duck);
                for (int i = 0; i < prevBuf.Length; i++) mixBuf[i] += prevBuf[i];
            }
            fading.RemoveAll(f => f.Gain <= 0f || f.Finished);

            if (stinger != null && !stinger.Finished)
            {
                Array.Clear(stingBuf, 0, stingBuf.Length);
                RenderSlot(bank, stinger, stingBuf, fullStems);
                for (int i = 0; i < mixBuf.Length; i++) mixBuf[i] += stingBuf[i] * stingerGain;
                if (stinger.Finished) stinger = null;
            }
        }

        hall.ProcessInPlace(mixBuf, ChunkFrames, wet, decay);
        MusicScore.SoftLimitStereo(mixBuf, mixBuf.Length);

        var bytes = new byte[ChunkFrames * 4];
        for (int i = 0; i < mixBuf.Length; i++)
        {
            short v = (short)(Math.Clamp(mixBuf[i], -1f, 1f) * 32000f);
            bytes[2 * i] = (byte)v;
            bytes[2 * i + 1] = (byte)(v >> 8);
        }
        return bytes;
    }

    static void RenderSlot(AudioBank bank, Slot slot, float[] dstStereo, ReadOnlySpan<float> stems)
    {
        double dt = ChunkFrames / (double)Synth.Rate;
        if (slot.Score is { } score)
        {
            ReadOnlySpan<float> useStems = score.Category == "battle" ? stems : stackalloc float[] { 1f, 1f, 1f, 1f };
            score.RenderStereoWindow(bank, dstStereo, ChunkFrames, Synth.Rate, slot.CursorSec, useStems);
            slot.CursorSec += dt;
            if (slot.Loop && score.DurationSec > 0.1) slot.CursorSec %= score.DurationSec;
            else if (!slot.Loop && slot.CursorSec >= score.TotalPlaySec) slot.Finished = true;
        }
        else if (slot.LegacyMono is { } mono && mono.Length > 0)
        {
            int pos = (int)Math.Round(slot.CursorSec * Synth.Rate);
            for (int i = 0; i < ChunkFrames; i++)
            {
                int idx = pos + i;
                if (slot.Loop) idx %= mono.Length;
                else if (idx >= mono.Length) { slot.Finished = true; break; }
                float v = mono[idx] * 0.8f;
                dstStereo[i * 2] += v;
                dstStereo[i * 2 + 1] += v;
            }
            slot.CursorSec += dt;
        }
    }

    static void ApplyRamp(float[] stereo, float g0, float g1)
    {
        if (MathF.Abs(g0 - 1f) < 1e-4f && MathF.Abs(g1 - 1f) < 1e-4f) return;
        for (int i = 0; i < ChunkFrames; i++)
        {
            float g = g0 + (g1 - g0) * (i / (float)ChunkFrames);
            stereo[i * 2] *= g;
            stereo[i * 2 + 1] *= g;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        wake.Set();
        try { output.Stop(); output.Dispose(); } catch { }
    }
}
