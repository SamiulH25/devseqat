using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Ironfield;

public enum Cue
{
    Clash, Block, Hit, Bow, ArrowHit, Impact, Shout, Horn, Rout, Click, Coin, Splash, Squelch, Rustle, Hollow, Spell, Thunder, Holy, Flame, Aura, Gather,
    Death, Neigh, Volley, Ram, Crash, Cheer, Order, Hover, Page, Throw,
}

public enum MusicTrack { None, Title, Creator, Map, Battle, Victory, Defeat, Deploy, Chronicle }

/// Looping ambience: battle beds by what is near the camera, map beds by hour and place. Each fades to silence when no
/// screen asks for it, so going from map to battle needs no bookkeeping.
public enum Bed { Wind, Gallop, Din, Roar, March, Rain, Birds, Crickets, Town }

/// Sound and music engine (AU0–AU6): 44.1 kHz procedural cues and looping beds (`AudioSynth.cs`), plus 44.1 kHz stereo
/// score streaming (`AudioStreamer.cs`, `MusicScore.cs`, `AudioBank.cs`). If there is no audio device, everything
/// quietly does nothing.
sealed class AudioEngine
{
    public bool Enabled { get; private set; }
    public static AudioEngine? Current;
    readonly Dictionary<Cue, SoundEffect[]> near = new(), far = new();
    readonly Dictionary<Cue, int> playedThisFrame = new(), lastVersion = new();
    readonly Dictionary<int, int> familyThisFrame = new();
    readonly Dictionary<Bed, (SoundEffectInstance I, float Level, float Want)> beds = new();
    readonly ConcurrentDictionary<string, float[]> legacyMade = new();
    readonly Random pick = new(1);

    readonly ConcurrentDictionary<Cue, (float[][] Near, float[][]? Far)> cueData = new();
    readonly ConcurrentDictionary<Bed, float[]> bedData = new();

    AudioStreamer? streamer;
    MusicTrack track = MusicTrack.None;
    string requestedPiece = "";
    float mapGap, duckAmount, duckHold;
    int mapCursor;

    public AudioEngine()
    {
        try
        {
            using (var probe = new SoundEffect(new byte[128], Synth.Rate, AudioChannels.Mono)) { }
            streamer = new AudioStreamer();
            mapCursor = pick.Next(3);
            Task.Run(MakeAll);
            Enabled = true;
        }
        catch (Exception e)
        {
            Console.WriteLine("no audio: " + e.Message);
            Enabled = false;
        }
    }

    /// Build cues and beds on a background thread (UI and title sounds first) and warm up the instrument bank & scores.
    void MakeAll()
    {
        try
        {
            void Cues(params Cue[] cs)
            {
                foreach (var c in cs)
                {
                    var v = Synth.Cues(c);
                    cueData[c] = (v, Synth.InWorld(c) ? v.Select(Synth.Far).ToArray() : null);
                }
            }
            void Beds(params Bed[] bs) { foreach (var b in bs) bedData[b] = Synth.MakeBed(b); }
            Cues(Cue.Click, Cue.Hover, Cue.Page, Cue.Coin);
            Beds(Bed.Wind);
            _ = AudioBank.Shared;
            _ = MusicLibrary.All;
            Beds(Bed.Birds, Bed.Crickets, Bed.Town, Bed.Rain);
            Cues(Enum.GetValues<Cue>().Where(c => !cueData.ContainsKey(c)).ToArray());
            Beds(Enum.GetValues<Bed>().Where(b => !bedData.ContainsKey(b)).ToArray());
        }
        catch (Exception e) { Console.WriteLine("audio: " + e.Message); }
    }

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
    static float Amb => Master * Settings.Current.AmbienceVolume;
    static float Mus => Master * Settings.Current.MusicVolume;

    // ---------- cues & priority mix ----------

    static float Chance(Cue c) => c switch { Cue.Death => 0.5f, Cue.Neigh => 0.8f, _ => 1f };

    static int Cap(Cue c) => c is Cue.Death or Cue.Volley or Cue.Cheer or Cue.Order or Cue.Page or Cue.Hover ? 1 : 4;

    /// Broad cue family so simultaneous variants (e.g. sword clash + axe clash + block) share a clean frame budget.
    static int Family(Cue c) => c switch
    {
        Cue.Clash or Cue.Block or Cue.Hit => 1,
        Cue.Bow or Cue.Throw or Cue.ArrowHit => 2,
        Cue.Shout or Cue.Death or Cue.Cheer => 3,
        _ => 0,
    };

    static int FamilyCap(int fam) => fam switch { 1 => 6, 2 => 5, 3 => 3, _ => 99 };

    static bool TriggersDuck(Cue c) => c is Cue.Horn or Cue.Rout or Cue.Order or Cue.Gather or Cue.Thunder;

    /// Briefly duck ambient beds and music so horns, great workings and cinematic beats cut cleanly through a 3,000-man din.
    public void Duck(float amount = 0.42f, float holdSec = 0.55f)
    {
        duckAmount = MathF.Max(duckAmount, Math.Clamp(amount, 0f, 0.8f));
        duckHold = MathF.Max(duckHold, holdSec);
    }

    public void Play(Cue c, float volume = 1, float pan = 0, bool distant = false, bool priority = false)
    {
        if (!Enabled || volume <= 0.01f) return;
        playedThisFrame.TryGetValue(c, out int n);
        int cap = priority ? Cap(c) + 2 : Cap(c);
        if (n >= cap || !Ready(c)) return;
        int fam = Family(c);
        if (!priority && fam != 0)
        {
            familyThisFrame.TryGetValue(fam, out int fn);
            if (fn >= FamilyCap(fam)) return;
            familyThisFrame[fam] = fn + 1;
        }
        if (!priority && pick.NextSingle() > Chance(c)) return;
        playedThisFrame[c] = n + 1;
        if (TriggersDuck(c) && !distant) Duck(0.38f, 0.45f);
        var set = distant && far.TryGetValue(c, out var f) ? f : near[c];
        lastVersion.TryGetValue(c, out int last);
        int v = pick.Next(set.Length);
        if (v == last && set.Length > 1) v = (v + 1) % set.Length;
        lastVersion[c] = v;
        float spread = Synth.InWorld(c) ? 0.12f : 0.04f;
        set[v].Play(Math.Clamp(volume * Sfx, 0, 1), (pick.NextSingle() - 0.5f) * spread, Math.Clamp(pan, -1, 1));
    }

    public void PlayAt(Cue c, Vector3 at, Vector3 listener, Vector3 right, float range = 70, bool priority = false)
    {
        var d = at - listener;
        float dist = d.Length(), x = dist / range;
        if (x >= 1f) return;
        float v = MathF.Max(0, 1 - x);
        Play(c, v * v * 1.2f, dist > 0.1f ? Vector3.Dot(d / dist, right) * 0.8f : 0, x > 0.33f, priority);
    }

    // ---------- beds ----------

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

    static float BedGain(Bed b) => b switch
    {
        Bed.Wind => 0.35f, Bed.Gallop => 0.8f, Bed.Din => 0.6f, Bed.Roar => 0.5f, Bed.March => 0.45f, Bed.Rain => 0.4f,
        Bed.Birds => 0.35f, Bed.Crickets => 0.25f, _ => 0.4f,
    };

    public void Beds(float windLevel, float gallopLevel, float dinLevel, float roarLevel = 0, float marchLevel = 0, float rain = 0)
    {
        SetBed(Bed.Wind, windLevel); SetBed(Bed.Gallop, gallopLevel); SetBed(Bed.Din, dinLevel);
        SetBed(Bed.Roar, roarLevel); SetBed(Bed.March, marchLevel); SetBed(Bed.Rain, rain);
    }

    // ---------- music & stems ----------

    /// Legacy 1-parameter battle intensity (maps onto the 4-stem mixer until AU5 wires granular battle stems).
    public void Intensity(float x)
    {
        float k = Math.Clamp(x, 0f, 1f);
        SetStemWeights(1f, Math.Clamp(k * 1.8f, 0f, 1f), k, Math.Clamp((k - 0.55f) * 2.2f, 0f, 1f));
    }

    public void SetStemWeights(float deploy, float skirmish, float melee, float crisis) =>
        streamer?.SetStems(deploy, skirmish, melee, crisis);

    public void PlayStinger(string id, float gain = 0.9f)
    {
        if (!Enabled) return;
        streamer?.PlayStinger(id, gain);
    }

    /// Play a specific score ID directly (used by contextual map/battle directors in AU4–AU5).
    public void RequestPiece(string id, MusicTrack mode = MusicTrack.Map)
    {
        if (!Enabled || string.IsNullOrEmpty(id)) return;
        track = mode;
        if (requestedPiece == id && streamer is { CurrentFinished: false }) return;
        requestedPiece = id;
        mapGap = 0;
        StartPiece(id);
    }

    public void Music(MusicTrack t)
    {
        if (!Enabled || t == track) return;
        track = t;
        mapGap = 0;
        string id = t switch
        {
            MusicTrack.Title => MusicLibrary.Has("title_ironfield") ? "title_ironfield" : "map0",
            MusicTrack.Creator => MusicLibrary.Has("creator_hearth") ? "creator_hearth" : "map1",
            MusicTrack.Chronicle => MusicLibrary.Has("chronicle_end") ? "chronicle_end" : "map1",
            MusicTrack.Map => PickMapPiece(),
            MusicTrack.Deploy => "deploy",
            MusicTrack.Battle => "battle",
            MusicTrack.Victory => "victory",
            MusicTrack.Defeat => "defeat",
            _ => "",
        };
        requestedPiece = id;
        if (t == MusicTrack.Deploy) SetStemWeights(1f, 0f, 0f, 0f);
        StartPiece(id);
    }

    string PickMapPiece() => "map" + (mapCursor % 3);

    void StartPiece(string id)
    {
        if (streamer == null) return;
        if (id == "") { streamer.PlayPiece(""); return; }
        if (MusicLibrary.Has(id))
        {
            streamer.PlayPiece(id);
            return;
        }
        // Lazy fallback for legacy procedural pieces while AU0–AU3 run
        if (legacyMade.TryGetValue(id, out var mono))
        {
            streamer.PlayPiece(id, mono);
        }
        else
        {
            Task.Run(() =>
            {
                var buf = Synth.Piece(id);
                legacyMade[id] = buf;
                if (requestedPiece == id) streamer.PlayPiece(id, buf);
            });
        }
    }

    public void Update(float dt)
    {
        playedThisFrame.Clear();
        familyThisFrame.Clear();
        if (!Enabled) return;

        if (duckHold > 0) duckHold -= dt;
        else duckAmount = MathF.Max(0, duckAmount - dt * 0.7f);
        float bedDuck = 1f - duckAmount;
        float musDuck = 1f - duckAmount * 0.45f;

        streamer?.Pump(Mus * 0.78f * musDuck);
        AdvancePlaylist(dt);

        foreach (var b in beds.Keys.ToList())
        {
            var (i, level, want) = beds[b];
            level += (want - level) * MathF.Min(1, dt * 2.5f);
            i.Volume = Math.Clamp(level * BedGain(b) * Amb * bedDuck, 0, 1);
            beds[b] = (i, level, 0);
        }
    }

    void AdvancePlaylist(float dt)
    {
        if (track != MusicTrack.Map || streamer == null || !streamer.CurrentFinished) return;
        if (mapGap <= 0) mapGap = 7 + pick.NextSingle() * 9;
        if ((mapGap -= dt) > 0.01f) return;
        mapGap = 0;
        mapCursor = (mapCursor + 1) % 3;
        requestedPiece = PickMapPiece();
        StartPiece(requestedPiece);
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
