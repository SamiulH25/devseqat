using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Ironfield;

public enum Cue
{
    Clash, Block, Hit, Bow, ArrowHit, Impact, Shout, Horn, Rout, Click, Coin, Splash, Squelch, Rustle, Hollow, Spell, Thunder, Holy, Flame, Aura, Gather,
    Death, Neigh, Volley, Ram, Crash, Cheer, Order, Hover, Page, Throw,
    // AU1: combat weapon×armour, defences, captain close-mic, ranged split & siege
    SlashLight, SlashHeavy, BluntFlesh, BluntPlate, PierceHit, LanceImpact, LanceShatter,
    BlockWood, BlockMetal, ParrySteel, ShieldSplinter, BashThud,
    SwingOne, SwingTwo, SwingPole, RiposteRing, WeaponSwap, LowHpPulse,
    LongbowLoose, CrossbowSnap, CrossbowSpan, SlingCrack, JavelinCast, ArrowShield, ArrowPlate, ArrowFlesh,
    RamSwing, LadderHook, FirePotShatter,
    // engines of war (docs/archive/SIEGECRAFT.md A6)
    EngineWinch, EngineRelease, WallFall,
    // AU2: creatures, beasts, undead, magic traditions, aura moments & order horns
    TrollRoar, TrollSweep, WolfHowl, WolfSnarl, SpiderChitter, WebSpit, BearRoar, WyvernScreech, WyvernFlap, GoblinShriek,
    BoneHit, BoneCollapse, RaiseDead,
    CastGaldr, CastHex, CastNecro, WardShatter, CrowSwarm, AuraIgnite, AuraBreakthrough, ChampionClash,
    HornCharge, HornHold, HornRetreat, HorseWhistle,
    // AU3: ground, world, settlements, armoury, deeds, scrolls & creator
    GravelStep, SnowCrunch, RecruitCheer, EquipBlade, EquipArmor, HorseTack, AnvilRing, WaxSeal, RiteDrum, FeastToast, ScrollUnroll,
}

public enum MusicTrack { None, Title, Creator, Map, Battle, Victory, Defeat, Deploy, Chronicle }

/// Looping ambience: battle beds by what is near the camera, map beds by hour, biome, season and settlement tab.
public enum Bed
{
    Wind, Gallop, Din, Roar, March, Rain, Birds, Crickets, Town,
    Surf, River, Forest, Blizzard, Tavern, Forge, OmenDrone,
}

/// Sound and music engine (AU0–AU6): 44.1 kHz procedural cues and looping beds (`AudioSynth.cs`), plus 44.1 kHz stereo
/// score streaming (`AudioStreamer.cs`, `MusicScore.cs`, `AudioBank.cs`). If there is no audio device, everything
/// quietly does nothing.
sealed partial class AudioEngine
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
    float duckAmount, duckHold;
    /// What the campaign map plays and when (MusicDirector.cs).
    readonly MusicDirector director = new(Environment.TickCount);

    public AudioEngine()
    {
        try
        {
            using (var probe = new SoundEffect(new byte[128], Synth.Rate, AudioChannels.Mono)) { }
            streamer = new AudioStreamer();
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
                    var v = AudioAssets.LoadCue(c) ?? Synth.Cues(c);
                    cueData[c] = (v, Synth.InWorld(c) ? v.Select(Synth.Far).ToArray() : null);
                }
            }
            void Beds(params Bed[] bs) { foreach (var b in bs) bedData[b] = AudioAssets.LoadBed(b) ?? Synth.MakeBed(b); }
            Cues(Cue.Click, Cue.Hover, Cue.Page, Cue.Coin);
            Beds(Bed.Wind);
            Cues(Enum.GetValues<Cue>().Where(c => !cueData.ContainsKey(c)).ToArray());
            _ = AudioBank.Shared;
            _ = MusicLibrary.All;
            Beds(Bed.Birds, Bed.Crickets, Bed.Town, Bed.Rain);
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

    static float Chance(Cue c) => c switch { Cue.Death or Cue.GoblinShriek => 0.5f, Cue.Neigh or Cue.BoneCollapse => 0.8f, _ => 1f };

    static int Cap(Cue c) => c is Cue.Death or Cue.GoblinShriek or Cue.BoneCollapse or Cue.Volley or Cue.Cheer
        or Cue.Order or Cue.HornCharge or Cue.HornHold or Cue.HornRetreat or Cue.Page or Cue.ScrollUnroll
        or Cue.Hover or Cue.LowHpPulse ? 1 : 4;

    /// Broad cue family so simultaneous variants (e.g. sword clash + axe clash + block) share a clean frame budget.
    static int Family(Cue c) => c switch
    {
        Cue.Clash or Cue.Block or Cue.Hit or Cue.SlashLight or Cue.SlashHeavy or Cue.BluntFlesh or Cue.BluntPlate
            or Cue.PierceHit or Cue.BlockWood or Cue.BlockMetal or Cue.ParrySteel or Cue.BoneHit => 1,
        Cue.Bow or Cue.Throw or Cue.ArrowHit or Cue.LongbowLoose or Cue.CrossbowSnap or Cue.SlingCrack
            or Cue.JavelinCast or Cue.ArrowShield or Cue.ArrowPlate or Cue.ArrowFlesh or Cue.WebSpit => 2,
        Cue.Shout or Cue.Death or Cue.Cheer or Cue.GoblinShriek or Cue.WolfSnarl or Cue.SpiderChitter => 3,
        _ => 0,
    };

    static int FamilyCap(int fam) => fam switch { 1 => 6, 2 => 5, 3 => 3, _ => 99 };

    static bool TriggersDuck(Cue c) => c is Cue.Horn or Cue.Rout or Cue.Order or Cue.HornCharge or Cue.HornHold
        or Cue.HornRetreat or Cue.Gather or Cue.Thunder or Cue.AuraBreakthrough or Cue.ChampionClash;

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
        float spread = AudioAssets.CuePitchSpread(c, Synth.InWorld(c) ? 0.12f : 0.04f);
        set[v].Play(Math.Clamp(volume * Sfx, 0, 1), (pick.NextSingle() - 0.5f) * spread, Math.Clamp(pan, -1, 1));
    }

    public void PlayAt(Cue c, Vector3 at, Vector3 listener, Vector3 right, float range = 70, bool priority = false)
    {
        var d = at - listener;
        float dist = d.Length(), x = dist / range;
        if (x >= 1f) return;
        float v = MathF.Max(0, 1 - x);
        Play(c, v * v * 1.2f, dist > 0.1f ? Vector3.Dot(d / dist, right) * 0.8f : 0, x > 0.5f, priority);
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
        beds[b] = (e.I, e.Level, Math.Clamp(level, 0, 1));
    }

    /// One frame of a bed's level: eased toward the level asked for, so a bed asked for ten times a second (the battle)
    /// holds its level, and one no longer asked for fades out over about a second.
    public static float BedStep(float level, float want, float dt) => level + (want - level) * MathF.Min(1, dt * 2.5f);

    static float BedGain(Bed b) => b switch
    {
        Bed.Wind => 0.35f, Bed.Gallop => 0.8f, Bed.Din => 0.6f, Bed.Roar => 0.5f, Bed.March => 0.45f, Bed.Rain => 0.4f,
        Bed.Birds => 0.35f, Bed.Crickets => 0.25f, Bed.Surf => 0.38f, Bed.River => 0.36f, Bed.Forest => 0.34f,
        Bed.Blizzard => 0.44f, Bed.Tavern => 0.42f, Bed.Forge => 0.38f, Bed.OmenDrone => 0.36f, _ => 0.4f,
    };

    public void Beds(float windLevel, float gallopLevel, float dinLevel, float roarLevel = 0, float marchLevel = 0, float rain = 0)
    {
        SetBed(Bed.Wind, windLevel); SetBed(Bed.Gallop, gallopLevel); SetBed(Bed.Din, dinLevel);
        SetBed(Bed.Roar, roarLevel); SetBed(Bed.March, marchLevel); SetBed(Bed.Rain, rain);
    }

    // ---------- music & stems ----------

    public void SetStemWeights(float deploy, float skirmish, float melee, float crisis) =>
        streamer?.SetStems(deploy, skirmish, melee, crisis);

    public void PlayStinger(string id, float gain = 0.9f)
    {
        if (!Enabled) return;
        streamer?.PlayStinger(id, gain);
    }

    /// The campaign map's music (AU4): `MusicDirector` chooses, this starts what it asks for. Coming back from a battle,
    /// the victory or defeat piece plays out before the map's own music.
    public void UpdateMapMusic(string? interiorPiece, string culture, bool night, bool winter, bool hostNear, float dread, float dt)
    {
        if (!Enabled) return;
        if (track != MusicTrack.Map)
        {
            if (track is MusicTrack.Victory or MusicTrack.Defeat && streamer is { CurrentFinished: false }) director.Adopt(requestedPiece);
            else director.Reset();
            track = MusicTrack.Map;
        }
        var cue = director.Update(interiorPiece, MusicDirector.Pool(culture, night, winter, hostNear, dread), dread >= 0.55f, dt,
            streamer?.CurrentFinished ?? true, streamer?.CurrentCursor ?? 0);
        if (cue is { } c)
        {
            requestedPiece = c.Id;
            StartPiece(c.Id, c.At);
        }
    }

    /// Contextual 4-stem battle & outcome music director (AU5): deploying plays stem 0 of `suiteId` so deployment flows
    /// seamlessly into combat on the same score; victory/defeat switch to the chosen outcome piece.
    public void UpdateBattleMusic(string suiteId, bool victory, bool defeat, string outcomeId, float s0, float s1, float s2, float s3)
    {
        if (!Enabled) return;
        if (victory || defeat)
        {
            var mode = victory ? MusicTrack.Victory : MusicTrack.Defeat;
            string fallback = victory ? "victory" : "defeat";
            string id = !string.IsNullOrEmpty(outcomeId) && HasPiece(outcomeId) ? outcomeId : fallback;
            if (track == mode && requestedPiece == id) return;
            track = mode;
            requestedPiece = id;
            StartPiece(id);
            return;
        }
        SetStemWeights(s0, s1, s2, s3);
        string target = !string.IsNullOrEmpty(suiteId) && HasPiece(suiteId) ? suiteId : "battle";
        if (track == MusicTrack.Battle && requestedPiece == target && streamer is not { CurrentFinished: true }) return;
        track = MusicTrack.Battle;
        requestedPiece = target;
        StartPiece(target);
    }

    public void Music(MusicTrack t)
    {
        if (!Enabled || t == track) return;
        track = t;
        if (t == MusicTrack.Map) { director.Reset(); return; } // UpdateMapMusic starts the map's piece
        string id = t switch
        {
            MusicTrack.Title => HasPiece("title_ironfield") ? "title_ironfield" : "map0",
            MusicTrack.Creator => HasPiece("creator_hearth") ? "creator_hearth" : "map1",
            MusicTrack.Chronicle => HasPiece("chronicle_end") ? "chronicle_end" : "map1",
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

    static bool HasPiece(string id) => AudioAssets.HasMusic(id) || MusicLibrary.Has(id);

    void StartPiece(string id, double at = 0)
    {
        if (streamer == null) return;
        if (id == "") { streamer.PlayPiece(""); return; }
        if (HasPiece(id))
        {
            streamer.PlayPiece(id, startSec: at);
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

    /// The frame while the game is paused under an overlay (the settings panel skips `Update`): start a fresh frame's
    /// cue counts, or the per-frame caps never clear and clicks and hover ticks stop after the first few; and keep the
    /// music fed, or the streamer's queue runs dry and it cuts out. Beds keep their level; a new volume applies at once.
    public void Overlay()
    {
        playedThisFrame.Clear();
        familyThisFrame.Clear();
        if (Enabled) streamer?.Pump(Mus * 0.78f * (1f - duckAmount * 0.45f));
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

        foreach (var b in beds.Keys.ToList())
        {
            var (i, level, want) = beds[b];
            want = MathF.Max(0, want - dt); // asked again by the screen every 0.1 s in battle, else fade over ~1 s
            level = BedStep(level, want, dt);
            i.Volume = Math.Clamp(level * BedGain(b) * Amb * bedDuck, 0, 1);
            beds[b] = (i, level, want);
        }
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
