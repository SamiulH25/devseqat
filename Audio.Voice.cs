using Microsoft.Xna.Framework.Audio;

namespace Ironfield;

/// Acoustic instrument speech playback on `AudioEngine` (docs/VOICE_DIALOGUE.md):
/// strict polyphony cap (2 voices), automatic SoundEffect disposal, and subtle music ducking.
sealed partial class AudioEngine
{
    readonly List<(SoundEffect Fx, SoundEffectInstance Inst, bool Priority)> activeVoices = new();
    readonly object voiceLock = new();

    static float VoiceVol => Master * Settings.Current.VoiceVolume;

    /// Speak `text` using `profile`'s acoustic instrument cadence. Safe to call from UI or battle presentation.
    public void Speak(VoiceProfile profile, string text, VoiceMood mood = VoiceMood.Even, float pan = 0, float volume = 1, bool priority = false, int maxWords = 12)
    {
        if (!Enabled || VoiceVol <= 0.01f || volume <= 0.01f || string.IsNullOrWhiteSpace(text)) return;
        float[] pcm = VoiceSpeech.Render(profile, text, mood, maxWords, VoiceSpeech.DefaultRate);
        if (pcm.Length < 64) return;

        lock (voiceLock)
        {
            PruneVoices();
            if (activeVoices.Count >= 2)
            {
                if (!priority) return;
                // Interrupt oldest non-priority voice (or oldest overall)
                int drop = activeVoices.FindIndex(v => !v.Priority);
                if (drop < 0) drop = 0;
                DisposeVoiceAt(drop);
            }
            else if (priority)
            {
                // Dialogue/parley restarts cleanly instead of overlapping itself
                for (int i = activeVoices.Count - 1; i >= 0; i--)
                    if (activeVoices[i].Priority) DisposeVoiceAt(i);
            }

            var bytes = new byte[pcm.Length * 2];
            for (int i = 0; i < pcm.Length; i++)
            {
                short s = (short)Math.Clamp(pcm[i] * 32767f, -32767f, 32767f);
                bytes[i * 2] = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            var fx = new SoundEffect(bytes, VoiceSpeech.DefaultRate, AudioChannels.Mono);
            var inst = fx.CreateInstance();
            inst.Volume = Math.Clamp(volume * VoiceVol, 0f, 1f);
            inst.Pan = Math.Clamp(pan, -1f, 1f);
            inst.Play();
            activeVoices.Add((fx, inst, priority));
            if (priority) Duck(0.24f, 0.45f);
        }
    }

    public void StopVoices()
    {
        lock (voiceLock)
        {
            for (int i = activeVoices.Count - 1; i >= 0; i--) DisposeVoiceAt(i);
        }
    }

    void PruneVoices()
    {
        for (int i = activeVoices.Count - 1; i >= 0; i--)
            if (activeVoices[i].Inst.State == SoundState.Stopped)
                DisposeVoiceAt(i);
    }

    void DisposeVoiceAt(int index)
    {
        var v = activeVoices[index];
        activeVoices.RemoveAt(index);
        try { v.Inst.Stop(); v.Inst.Dispose(); } catch { }
        try { v.Fx.Dispose(); } catch { }
    }
}
