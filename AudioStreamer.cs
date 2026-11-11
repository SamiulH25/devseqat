using System.Collections.Concurrent;
using Microsoft.Xna.Framework.Audio;

namespace Ironfield;

/// Streams 44.1 kHz stereo music through a single `DynamicSoundEffectInstance`. A background worker renders 0.20 s
/// chunks (mixing 4 sample-locked stems, crossfades and one-shot stingers through one shared `StereoHall` reverb),
/// and the main thread submits queued PCM buffers in `Pump()` so OpenAL calls stay on the main thread.
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
    }

    readonly DynamicSoundEffectInstance output;
    readonly ConcurrentQueue<byte[]> ready = new();
    readonly AutoResetEvent wake = new(true);
    readonly object stateLock = new();
    readonly StereoHall hall = new(Synth.Rate);

    readonly float[] mixBuf = new float[ChunkFrames * 2];
    readonly float[] prevBuf = new float[ChunkFrames * 2];
    readonly float[] stingBuf = new float[ChunkFrames * 2];
    readonly float[] stemCur = { 1f, 0f, 0f, 0f }, stemWant = { 1f, 0f, 0f, 0f };

    Slot? current, previous, stinger;
    float crossfade = 1f, masterVol = 0.5f, stingerGain = 0.9f;
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

    public void PlayPiece(string id, float[]? legacyFallback = null, bool? forceLoop = null)
    {
        lock (stateLock)
        {
            if (current?.Id == id && (id == "" || !current.Finished)) return;
            previous = current;
            if (string.IsNullOrEmpty(id))
            {
                current = null;
            }
            else if (MusicLibrary.Get(id) is { } score)
            {
                current = new Slot { Id = id, Score = score, Loop = forceLoop ?? score.Loop };
            }
            else if (legacyFallback != null)
            {
                current = new Slot { Id = id, LegacyMono = legacyFallback, Loop = forceLoop ?? (id is "battle" or "deploy") };
            }
            else
            {
                current = null;
            }
            crossfade = 0f;
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

            float stingDuck = stinger is { Finished: false } ? 0.48f : 1f;
            float fadeStart = crossfade;
            float fadeEnd = MathF.Min(1f, crossfade + (float)(chunkSec / 1.5));
            crossfade = fadeEnd;

            if (current != null && !current.Finished)
            {
                if (current.Score != null) { wet = current.Score.ReverbWet; decay = current.Score.ReverbDecay; }
                RenderSlot(bank, current, mixBuf, stemCur);
                ApplyRamp(mixBuf, fadeStart * stingDuck, fadeEnd * stingDuck);
            }

            if (previous != null)
            {
                Array.Clear(prevBuf, 0, prevBuf.Length);
                RenderSlot(bank, previous, prevBuf, stemCur);
                for (int i = 0; i < ChunkFrames; i++)
                {
                    float t = i / (float)ChunkFrames;
                    float g = (1f - (fadeStart + (fadeEnd - fadeStart) * t)) * stingDuck;
                    mixBuf[i * 2] += prevBuf[i * 2] * g;
                    mixBuf[i * 2 + 1] += prevBuf[i * 2 + 1] * g;
                }
                if (fadeEnd >= 0.999f) previous = null;
            }

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
