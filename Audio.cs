using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Ironfield;

/// Sound cues the headless battle reports (Battle.Sounds); the audio engine turns them into positional sound.
public enum Cue { Clash, Block, Hit, Bow, ArrowHit, Impact, Shout, Horn, Rout, Click, Coin }

public enum MusicTrack { None, Map, Battle, Victory, Defeat }

/// Sound and music, synthesised in code at startup (in keeping with the code-painted art: no audio files).
/// If there is no audio device, everything here quietly does nothing.
sealed class AudioEngine
{
    const int Rate = 22050;
    public bool Enabled { get; private set; }
    public static AudioEngine? Current;
    readonly Dictionary<Cue, SoundEffect> cues = new();
    readonly Dictionary<MusicTrack, SoundEffect> music = new();
    readonly Dictionary<Cue, int> playedThisFrame = new();
    SoundEffectInstance? wind, gallop, din, current, previous;
    MusicTrack track = MusicTrack.None;
    float fade = 1;
    static readonly Random Noise = new(1);

    public AudioEngine()
    {
        try
        {
            cues[Cue.Clash] = Make(Clash());
            cues[Cue.Block] = Make(Thud(150, 0.25f, 0.5f));
            cues[Cue.Hit] = Make(Thud(90, 0.2f, 0.8f));
            cues[Cue.Bow] = Make(Bow());
            cues[Cue.ArrowHit] = Make(Click(0.1f, 900, 0.7f));
            cues[Cue.Impact] = Make(Thud(60, 0.35f, 1f));
            cues[Cue.Shout] = Make(Shout());
            cues[Cue.Horn] = Make(Horn(220, 1.3f, false));
            cues[Cue.Rout] = Make(Horn(150, 1.6f, true));
            cues[Cue.Click] = Make(Click(0.04f, 1800, 0.2f));
            cues[Cue.Coin] = Make(Coin());
            music[MusicTrack.Map] = Make(MapTheme());
            music[MusicTrack.Battle] = Make(Drums());
            music[MusicTrack.Victory] = Make(Fanfare(true));
            music[MusicTrack.Defeat] = Make(Fanfare(false));
            wind = Make(Wind()).CreateInstance();
            wind.IsLooped = true; wind.Volume = 0; wind.Play();
            gallop = Make(Gallop()).CreateInstance();
            gallop.IsLooped = true; gallop.Volume = 0; gallop.Play();
            din = Make(Din()).CreateInstance();
            din.IsLooped = true; din.Volume = 0; din.Play();
            Enabled = true;
        }
        catch (Exception e)
        {
            Console.WriteLine("no audio: " + e.Message);
            Enabled = false;
        }
    }

    static float Master => Settings.Current.MasterVolume;
    static float Sfx => Master * Settings.Current.SfxVolume;
    static float Mus => Master * Settings.Current.MusicVolume;

    // ---------- playing ----------

    public void Play(Cue c, float volume = 1, float pan = 0)
    {
        if (!Enabled || volume <= 0.01f) return;
        playedThisFrame.TryGetValue(c, out int n);
        if (n >= 3) return; // a crowd of identical sounds is just noise
        playedThisFrame[c] = n + 1;
        cues[c].Play(Math.Clamp(volume * Sfx, 0, 1), (Noise.NextSingle() - 0.5f) * 0.16f, Math.Clamp(pan, -1, 1));
    }

    /// A sound somewhere in the world, heard from the camera: quieter with distance, panned left or right.
    public void PlayAt(Cue c, Vector3 at, Vector3 listener, Vector3 right, float range = 70)
    {
        var d = at - listener;
        float dist = d.Length();
        float v = MathF.Max(0, 1 - dist / range);
        Play(c, v * v * 1.2f, dist > 0.1f ? Vector3.Dot(d / dist, right) * 0.8f : 0);
    }

    /// Ambient beds: wind always, plus galloping and melee din by how much of each is near the camera (0..1).
    public void Beds(float windLevel, float gallopLevel, float dinLevel)
    {
        if (!Enabled) return;
        wind!.Volume = Math.Clamp(windLevel * 0.35f * Sfx, 0, 1);
        gallop!.Volume = Math.Clamp(gallopLevel * 0.8f * Sfx, 0, 1);
        din!.Volume = Math.Clamp(dinLevel * 0.6f * Sfx, 0, 1);
    }

    public void Music(MusicTrack t)
    {
        if (!Enabled || t == track) return;
        track = t;
        previous?.Stop();
        previous = current;
        current = null;
        if (t != MusicTrack.None)
        {
            current = music[t].CreateInstance();
            current.IsLooped = t is MusicTrack.Map or MusicTrack.Battle;
            current.Volume = 0;
            current.Play();
        }
        fade = 0;
    }

    /// Call once per frame: crossfades the music and resets the per-frame sound limits.
    public void Update(float dt)
    {
        playedThisFrame.Clear();
        if (!Enabled) return;
        fade = MathF.Min(1, fade + dt / 1.5f);
        if (current != null) current.Volume = Math.Clamp(fade * Mus * 0.7f, 0, 1);
        if (previous != null)
        {
            previous.Volume = Math.Clamp((1 - fade) * Mus * 0.7f, 0, 1);
            if (fade >= 1) { previous.Stop(); previous = null; }
        }
    }

    // ---------- synthesis ----------

    static SoundEffect Make(float[] s)
    {
        var bytes = new byte[s.Length * 2];
        for (int i = 0; i < s.Length; i++)
        {
            short v = (short)(Math.Clamp(s[i], -1, 1) * 32000);
            bytes[2 * i] = (byte)v;
            bytes[2 * i + 1] = (byte)(v >> 8);
        }
        return new SoundEffect(bytes, Rate, AudioChannels.Mono);
    }

    static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];
    static float Rnd() => Noise.NextSingle() * 2 - 1;

    /// Metal on metal: inharmonic partials ringing down, struck by a noise burst.
    static float[] Clash()
    {
        var b = Buf(0.45f);
        float[] f = { 1180, 1730, 2390, 3160, 4420 }, k = { 9, 12, 15, 20, 26 };
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate, s = 0;
            for (int p = 0; p < f.Length; p++) s += MathF.Sin(MathF.Tau * f[p] * t) * MathF.Exp(-k[p] * t) / (p + 1.5f);
            b[i] = s * 0.7f + Rnd() * MathF.Exp(-t * 90) * 0.5f;
        }
        return b;
    }

    /// A dull blow: a falling low tone and a short filtered noise (shields, bodies, horses).
    static float[] Thud(float freq, float len, float noise)
    {
        var b = Buf(len);
        float lp = 0, ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            ph += MathF.Tau * freq * (1 - t * 1.2f) / Rate;
            lp += (Rnd() - lp) * 0.15f;
            b[i] = (MathF.Sin(ph) * 0.8f + lp * noise * 1.5f) * MathF.Exp(-t * 18);
        }
        return b;
    }

    static float[] Click(float len, float freq, float noise)
    {
        var b = Buf(len);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            b[i] = (MathF.Sin(MathF.Tau * freq * t) * 0.6f + Rnd() * noise) * MathF.Exp(-t * 60);
        }
        return b;
    }

    /// A plucked string (Karplus-Strong): bows and the map's lute.
    static void Pluck(float[] b, int start, float freq, float amp, float decay = 0.996f)
    {
        int n = Math.Max(2, (int)(Rate / freq));
        var ring = new float[n];
        for (int i = 0; i < n; i++) ring[i] = Rnd();
        for (int i = 0, j = 0; start + i < b.Length && i < Rate * 3; i++, j = (j + 1) % n)
        {
            float next = ring[(j + 1) % n];
            ring[j] = (ring[j] + next) * 0.5f * decay;
            b[start + i] += ring[j] * amp;
        }
    }

    static float[] Bow()
    {
        var b = Buf(0.35f);
        Pluck(b, 0, 170, 0.6f, 0.985f);
        float lp = 0;
        for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; lp += (Rnd() - lp) * 0.3f; b[i] += lp * MathF.Exp(-t * 25) * 0.4f; }
        return b;
    }

    /// Many throats: detuned buzzing voices through a soft filter, rising and falling ("charge!").
    static float[] Shout()
    {
        var b = Buf(1.0f);
        float[] f = { 148, 163, 182, 205, 231, 252 };
        var ph = new float[f.Length];
        float lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate, s = 0;
            for (int v = 0; v < f.Length; v++)
            {
                ph[v] += f[v] * (1 + 0.06f * MathF.Sin(t * 5 + v)) * (1 + t * 0.15f) / Rate;
                s += (ph[v] % 1) * 2 - 1;
            }
            lp += (s / f.Length - lp) * 0.08f;
            float env = MathF.Min(1, t * 8) * MathF.Exp(-MathF.Max(0, t - 0.5f) * 5);
            b[i] = lp * env * 1.6f + Rnd() * env * 0.05f;
        }
        return b;
    }

    /// A war horn: a soft-attack sawtooth with vibrato; `falling` bends down (the rout horn).
    static float[] Horn(float freq, float len, bool falling)
    {
        var b = Buf(len);
        float ph = 0, ph2 = 0, lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate, f = freq * (falling ? 1 - 0.15f * t / len : 1) * (1 + 0.008f * MathF.Sin(t * 30));
            ph += f / Rate; ph2 += f * 1.5f / Rate;
            float s = ((ph % 1) * 2 - 1) + ((ph2 % 1) * 2 - 1) * 0.4f;
            lp += (s - lp) * 0.06f;
            float env = MathF.Min(1, t * 6) * MathF.Min(1, (len - t) * 4);
            b[i] = lp * env * 0.6f;
        }
        return b;
    }

    static float[] Coin()
    {
        var b = Buf(0.3f);
        foreach (var (at, f) in new[] { (0f, 2600f), (0.08f, 3300f) })
            for (int i = (int)(at * Rate); i < b.Length; i++) { float t = i / (float)Rate - at; b[i] += MathF.Sin(MathF.Tau * f * t) * MathF.Exp(-t * 25) * 0.4f; }
        return b;
    }

    /// Wind: slowly breathing filtered noise, made seamless by crossfading its ends.
    static float[] Wind()
    {
        var b = Buf(8);
        float lp = 0, lp2 = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            lp += (Rnd() - lp) * 0.02f;
            lp2 += (lp - lp2) * 0.05f;
            b[i] = lp2 * 4 * (0.6f + 0.4f * MathF.Sin(t * MathF.Tau / 8 * 2) * MathF.Sin(t * 1.3f));
        }
        return Loop(b, 0.5f);
    }

    static float[] Gallop()
    {
        var b = Buf(0.6f);
        foreach (float at in new[] { 0f, 0.09f, 0.2f, 0.27f })
        {
            var hit = Thud(70 + Noise.Next(20), 0.12f, 0.9f);
            int s = (int)(at * Rate);
            for (int i = 0; i < hit.Length && s + i < b.Length; i++) b[s + i] += hit[i] * 0.8f;
        }
        return b;
    }

    /// The din of melee: a murmur with scattered clashes and thuds.
    static float[] Din()
    {
        var b = Buf(4);
        float lp = 0;
        for (int i = 0; i < b.Length; i++) { lp += (Rnd() - lp) * 0.04f; b[i] = lp * 0.8f; }
        var clash = Clash();
        var thud = Thud(110, 0.2f, 0.7f);
        for (int k = 0; k < 18; k++)
        {
            var src = k % 3 == 0 ? clash : thud;
            int s = Noise.Next(b.Length - src.Length);
            float g = 0.15f + Noise.NextSingle() * 0.25f;
            for (int i = 0; i < src.Length; i++) b[s + i] += src[i] * g;
        }
        return Loop(b, 0.2f);
    }

    /// The map: a low drone and a lute wandering a pentatonic scale.
    static float[] MapTheme()
    {
        var b = Buf(32);
        float[] scale = { 146.8f, 174.6f, 196f, 220f, 261.6f, 293.7f, 349.2f, 392f };
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            b[i] = (MathF.Sin(MathF.Tau * 73.4f * t) * 0.18f + MathF.Sin(MathF.Tau * 110f * t) * 0.1f) * (0.75f + 0.25f * MathF.Sin(t * 0.4f));
        }
        var r = new Random(7);
        int note = 3;
        for (float at = 0.5f; at < 31; at += 0.5f)
        {
            if (r.Next(10) < 3) continue;
            note = Math.Clamp(note + r.Next(-2, 3), 0, scale.Length - 1);
            Pluck(b, (int)(at * Rate), scale[note], 0.22f, 0.9965f);
        }
        return Loop(b, 1);
    }

    /// Battle: war drums and a low horn drone.
    static float[] Drums()
    {
        var b = Buf(9.6f); // 4 bars at 100 bpm
        float beat = 0.6f;
        int[] pattern = { 1, 0, 0, 1, 1, 0, 1, 0 };
        for (int bar = 0; bar < 4; bar++)
            for (int k = 0; k < 8; k++)
            {
                if (pattern[k] == 0) continue;
                var hit = Thud(k == 0 ? 55 : 80, 0.45f, 0.5f);
                int s = (int)((bar * 8 + k) * beat / 2 * Rate);
                for (int i = 0; i < hit.Length && s + i < b.Length; i++) b[s + i] += hit[i] * (k == 0 ? 1 : 0.6f);
            }
        var horn = Horn(73.4f, 9.6f, false);
        for (int i = 0; i < b.Length; i++) b[i] += horn[i] * 0.35f;
        return b;
    }

    static float[] Fanfare(bool victory)
    {
        var b = Buf(3.2f);
        float[] notes = victory ? new[] { 220f, 277.2f, 329.6f, 440f } : new[] { 220f, 207.7f, 174.6f, 146.8f };
        for (int n = 0; n < notes.Length; n++)
        {
            var h = Horn(notes[n], n == notes.Length - 1 ? 1.6f : 0.6f, !victory && n == notes.Length - 1);
            int s = (int)(n * 0.5f * Rate);
            for (int i = 0; i < h.Length && s + i < b.Length; i++) b[s + i] += h[i];
        }
        return b;
    }

    /// Crossfade the last `seconds` into the start so a loop has no click.
    static float[] Loop(float[] b, float seconds)
    {
        int n = (int)(seconds * Rate);
        var o = new float[b.Length - n];
        Array.Copy(b, o, o.Length);
        for (int i = 0; i < n; i++) { float k = i / (float)n; o[i] = b[i] * k + b[o.Length + i] * (1 - k); }
        return o;
    }
}
