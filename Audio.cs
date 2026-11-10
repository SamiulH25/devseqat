using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Ironfield;

public enum Cue
{
    Clash, Block, Hit, Bow, ArrowHit, Impact, Shout, Horn, Rout, Click, Coin, Splash, Squelch, Rustle, Hollow, Spell, Thunder, Holy, Flame, Aura, Gather,
    Death, Neigh, Volley, Ram, Crash, Cheer, Order, Hover, Page, Throw,
}

public enum MusicTrack { None, Map, Battle, Victory, Defeat, Deploy }

/// Looping ambience: battle beds by what is near the camera, map beds by hour and place. Each fades to silence when no
/// screen asks for it, so going from map to battle needs no bookkeeping.
public enum Bed { Wind, Gallop, Din, Roar, March, Rain, Birds, Crickets, Town }

/// Sound and music, synthesised in code (Synth, AudioSynth.cs and AudioMusic.cs; no audio files). Cues come in
/// several versions picked at random, with a far version for distant sounds. Music is made on a background thread at
/// startup and starts once ready. If there is no audio device, everything here quietly does nothing.
sealed class AudioEngine
{
    public bool Enabled { get; private set; }
    public static AudioEngine? Current;
    readonly Dictionary<Cue, SoundEffect[]> near = new(), far = new();
    readonly Dictionary<Cue, int> playedThisFrame = new(), lastVersion = new();
    readonly Dictionary<Bed, (SoundEffectInstance I, float Level, float Want)> beds = new();
    readonly ConcurrentDictionary<string, float[]> made = new();
    readonly Dictionary<string, SoundEffect> pieces = new();
    readonly Random pick = new(1);

    MusicTrack track = MusicTrack.None;
    Playing? current, previous;
    float fade = 1, intensity, wantIntensity, mapGap;
    int mapTheme;

    /// A piece of music sounding: its main instance and, for the battle, the layer that rises with the fighting.
    sealed class Playing { public SoundEffectInstance? Main, Layer; public string Id = ""; }

    readonly ConcurrentDictionary<Cue, (float[][] Near, float[][]? Far)> cueData = new();
    readonly ConcurrentDictionary<Bed, float[]> bedData = new();

    public AudioEngine()
    {
        try
        {
            using (var probe = new SoundEffect(new byte[64], Synth.Rate, AudioChannels.Mono)) { } // fails here when there is no device
            mapTheme = pick.Next(3);
            Task.Run(MakeAll);
            Enabled = true;
        }
        catch (Exception e)
        {
            Console.WriteLine("no audio: " + e.Message);
            Enabled = false;
        }
    }

    /// Make every sound on a background thread, the first heard first: the interface, the title's wind and theme, then
    /// the rest. A cue asked for before it is made is skipped; a bed or piece starts once ready.
    void MakeAll()
    {
        try
        {
            void Cues(params Cue[] cs) { foreach (var c in cs) { var v = Synth.Cues(c); cueData[c] = (v, Synth.InWorld(c) ? v.Select(Synth.Far).ToArray() : null); } }
            void Beds(params Bed[] bs) { foreach (var b in bs) bedData[b] = b switch
            {
                Bed.Wind => Synth.Wind(), Bed.Gallop => Synth.Gallop(), Bed.Din => Synth.Din(), Bed.Roar => Synth.Roar(),
                Bed.March => Synth.March(), Bed.Rain => Synth.Rain(), Bed.Birds => Synth.Birds(), Bed.Crickets => Synth.Crickets(),
                _ => Synth.Town(),
            }; }
            Cues(Cue.Click, Cue.Hover, Cue.Page, Cue.Coin);
            Beds(Bed.Wind);
            made["map0"] = Synth.Piece("map0");
            Beds(Bed.Birds, Bed.Crickets, Bed.Town, Bed.Rain);
            Cues(Enum.GetValues<Cue>().Where(c => !cueData.ContainsKey(c)).ToArray());
            Beds(Enum.GetValues<Bed>().Where(b => !bedData.ContainsKey(b)).ToArray());
            foreach (var id in Synth.Pieces) if (!made.ContainsKey(id)) made[id] = Synth.Piece(id);
        }
        catch (Exception e) { Console.WriteLine("audio: " + e.Message); }
    }

    /// A cue's sounds once made (built on the main thread from the background's buffers).
    bool Ready(Cue c)
    {
        if (near.ContainsKey(c)) return true;
        if (!cueData.TryGetValue(c, out var d)) return false;
        near[c] = d.Near.Select(Make).ToArray();
        if (d.Far != null) far[c] = d.Far.Select(Make).ToArray();
        return true;
    }

    static float Master => Settings.Current.MasterVolume;
    static float Sfx => Master * Settings.Current.SfxVolume;
    static float Mus => Master * Settings.Current.MusicVolume;

    // ---------- cues ----------

    /// Most deaths go quietly in the din; only some are heard as a cry.
    static float Chance(Cue c) => c switch { Cue.Death => 0.5f, Cue.Neigh => 0.8f, _ => 1 };

    /// Most of one cue in one frame: a crowd of identical sounds is just noise.
    static int Cap(Cue c) => c is Cue.Death or Cue.Volley or Cue.Cheer or Cue.Order or Cue.Page or Cue.Hover ? 1 : 3;

    public void Play(Cue c, float volume = 1, float pan = 0, bool distant = false)
    {
        if (!Enabled || volume <= 0.01f) return;
        playedThisFrame.TryGetValue(c, out int n);
        if (n >= Cap(c) || !Ready(c) || pick.NextSingle() > Chance(c)) return;
        playedThisFrame[c] = n + 1;
        var set = distant && far.TryGetValue(c, out var f) ? f : near[c];
        lastVersion.TryGetValue(c, out int last);
        int v = pick.Next(set.Length);
        if (v == last && set.Length > 1) v = (v + 1) % set.Length; // never the same one twice running
        lastVersion[c] = v;
        float spread = Synth.InWorld(c) ? 0.12f : 0.04f;
        set[v].Play(Math.Clamp(volume * Sfx, 0, 1), (pick.NextSingle() - 0.5f) * spread, Math.Clamp(pan, -1, 1));
    }

    /// A sound somewhere in the world, heard from the camera: quieter with distance, panned left or right, and past a
    /// third of its range heard as its far version (muffled, with more echo).
    public void PlayAt(Cue c, Vector3 at, Vector3 listener, Vector3 right, float range = 70)
    {
        var d = at - listener;
        float dist = d.Length(), x = dist / range;
        float v = MathF.Max(0, 1 - x);
        Play(c, v * v * 1.2f, dist > 0.1f ? Vector3.Dot(d / dist, right) * 0.8f : 0, x > 0.33f);
    }

    // ---------- beds ----------

    /// Ask for a bed at `level` (0..1) this frame; beds not asked for fade out.
    public void SetBed(Bed b, float level)
    {
        if (!Enabled) return;
        if (!beds.TryGetValue(b, out var e))
        {
            if (!bedData.TryGetValue(b, out var data)) return;
            var i = Make(data).CreateInstance();
            i.IsLooped = true; i.Volume = 0; i.Play();
            beds[b] = e = (i, 0, 0);
        }
        beds[b] = (e.I, e.Level, MathF.Max(e.Want, Math.Clamp(level, 0, 1)));
    }

    /// How loud each bed is at full level, against the cues.
    static float BedGain(Bed b) => b switch
    {
        Bed.Wind => 0.35f, Bed.Gallop => 0.8f, Bed.Din => 0.6f, Bed.Roar => 0.5f, Bed.March => 0.45f, Bed.Rain => 0.4f,
        Bed.Birds => 0.35f, Bed.Crickets => 0.25f, _ => 0.4f,
    };

    /// Battle beds: wind always, galloping, melee din and marching by how much of each is near the camera (0..1), and
    /// the roar of the whole battle by how many men are fighting anywhere on the field (0..1).
    public void Beds(float windLevel, float gallopLevel, float dinLevel, float roarLevel = 0, float marchLevel = 0, float rain = 0)
    {
        SetBed(Bed.Wind, windLevel); SetBed(Bed.Gallop, gallopLevel); SetBed(Bed.Din, dinLevel);
        SetBed(Bed.Roar, roarLevel); SetBed(Bed.March, marchLevel); SetBed(Bed.Rain, rain);
    }

    /// How hard the battle is going (0 calm .. 1 the whole field fighting): raises the battle music's second layer.
    public void Intensity(float x) => wantIntensity = Math.Clamp(x, 0, 1);

    // ---------- music ----------

    public void Music(MusicTrack t)
    {
        if (!Enabled || t == track) return;
        track = t;
        mapGap = 0;
        Start(t switch
        {
            MusicTrack.Map => "map" + mapTheme, MusicTrack.Battle => "battle", MusicTrack.Victory => "victory",
            MusicTrack.Defeat => "defeat", MusicTrack.Deploy => "deploy", _ => "",
        });
    }

    /// Crossfade to piece `id` ("" = silence). If it is still being made it starts once ready (see Update).
    void Start(string id)
    {
        previous?.Main?.Stop(); previous?.Layer?.Stop();
        previous = current;
        current = id == "" ? null : new Playing { Id = id };
        fade = 0;
        TryStart();
    }

    /// Start the current piece if its music is ready (it is made on a background thread at startup).
    void TryStart()
    {
        if (current == null || current.Main != null) return;
        var main = Sound(current.Id);
        var layer = current.Id == "battle" ? Sound("battle2") : null;
        if (main == null || current.Id == "battle" && layer == null) return; // the battle waits for both layers so they start together
        current.Main = main.CreateInstance();
        current.Main.IsLooped = current.Id is "battle" or "deploy";
        current.Main.Volume = 0;
        if (layer != null)
        {
            current.Layer = layer.CreateInstance();
            current.Layer.IsLooped = true;
            current.Layer.Volume = 0;
            current.Layer.Play();
        }
        current.Main.Play();
        fade = 0;
    }

    SoundEffect? Sound(string id)
    {
        if (pieces.TryGetValue(id, out var s)) return s;
        if (!made.TryGetValue(id, out var b)) return null;
        return pieces[id] = Make(b);
    }

    /// Call once per frame: crossfades the music, plays the next map theme after a pause, fades the beds and resets the
    /// per-frame sound limits.
    public void Update(float dt)
    {
        playedThisFrame.Clear();
        if (!Enabled) return;
        TryStart();
        fade = MathF.Min(1, fade + dt / 1.5f);
        intensity += (wantIntensity - intensity) * MathF.Min(1, dt * 0.6f); // the music swells over a few seconds
        float vol = Mus * 0.7f;
        if (current?.Main is { } m) m.Volume = Math.Clamp(fade * vol, 0, 1);
        if (current?.Layer is { } l) l.Volume = Math.Clamp(fade * vol * intensity, 0, 1);
        if (previous != null)
        {
            float k = Math.Clamp((1 - fade) * vol, 0, 1);
            if (previous.Main != null) previous.Main.Volume = k;
            if (previous.Layer != null) previous.Layer.Volume = k * intensity;
            if (fade >= 1) { previous.Main?.Stop(); previous.Layer?.Stop(); previous = null; }
        }
        NextMapTheme(dt);
        foreach (var b in beds.Keys.ToList())
        {
            var (i, level, want) = beds[b];
            level += (want - level) * MathF.Min(1, dt * 2.5f);
            i.Volume = Math.Clamp(level * BedGain(b) * Sfx, 0, 1);
            beds[b] = (i, level, 0);
        }
    }

    /// On the map the themes take turns: when one ends, a quiet spell of ambience, then the next.
    void NextMapTheme(float dt)
    {
        if (track != MusicTrack.Map || current?.Main == null || current.Main.State != SoundState.Stopped) return;
        if (mapGap <= 0) mapGap = 8 + pick.NextSingle() * 12;
        if ((mapGap -= dt) > 0.01f) return;
        mapGap = 0;
        mapTheme = (mapTheme + 1) % 3;
        Start("map" + mapTheme);
    }

    static SoundEffect Make(float[] s)
    {
        var bytes = new byte[s.Length * 2];
        for (int i = 0; i < s.Length; i++)
        {
            short v = (short)(Math.Clamp(s[i], -1, 1) * 32000);
            bytes[2 * i] = (byte)v;
            bytes[2 * i + 1] = (byte)(v >> 8);
        }
        return new SoundEffect(bytes, Synth.Rate, AudioChannels.Mono);
    }
}
