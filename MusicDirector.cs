namespace Ironfield;

/// The campaign map's music director (headless: `AudioEngine.UpdateMapMusic` drives it each frame and starts what it asks
/// for; `SelfTest.MapMusicSteady` checks it). Kept steady on purpose, because a piece cut short or swapped every few
/// seconds is worse than silence:
/// - an open-map piece always plays to its end, then a short pause, then the next from the pool (never the same twice
///   running); only dread that has lasted `DreadHold` s may cut one short, and not within `MinHold` s of the last change;
///   an enemy host near only colours the next pick;
/// - a town, hall or site screen starts its piece once it has stayed open `Settle` s; another screen of the same visit
///   (the tavern after the market) takes over only once it has stayed open `InteriorSwitch` s;
/// - back on the map after a short visit, the map piece resumes where it was left instead of starting again.
public sealed class MusicDirector
{
    public const float Settle = 1.5f, InteriorSwitch = 6f, DreadHold = 6f, MinHold = 30f, GapMin = 5f, GapMax = 9f, ResumeWithin = 120f;

    /// A piece to start, `At` seconds in.
    public readonly record struct Cue(string Id, double At);

    readonly Random rng;
    int cursor;
    string interior = "", want = "", resumeId = "";
    double resumeAt;
    float wantFor, interiorFor, sinceChange, dreadFor, gap = -1;

    /// The piece the director last started (or adopted); "" before the first.
    public string Playing { get; private set; } = "";

    public MusicDirector(int seed)
    {
        rng = new Random(seed);
        cursor = rng.Next(3);
    }

    /// The open map's pool for where the captain stands: his culture's day, peace and night pieces, the host piece when
    /// an enemy lord's host is near, and the dread pieces when graves, the Omen or the Black Flame weigh on him.
    public static string[] Pool(string culture, bool night, bool winter, bool hostNear, float dread)
    {
        string p = culture switch
        {
            "crimson" => "crimson", "northern" => "north", "southern" => "sunward",
            "steppe" => "steppe", "league" => "league", "order" => "order", _ => "azure",
        };
        return dread >= 0.55f ? new[] { dread >= 0.8f ? "black_flame" : "omen_graves", $"{p}_night" }
            : hostNear ? new[] { $"{p}_host", $"{p}_day" }
            : winter ? (night ? new[] { "winter_night", $"{p}_night" } : new[] { "winter_march", $"{p}_day", $"{p}_peace" })
            : night ? new[] { $"{p}_night", $"{p}_peace" }
            : new[] { $"{p}_day", $"{p}_peace" };
    }

    /// Forget what was playing: the next `Update` starts a fresh map piece at once (coming from the title or a battle).
    public void Reset()
    {
        Playing = interior = want = resumeId = "";
        gap = -1;
    }

    /// Take a piece already playing (the victory or defeat from a battle) as the open map's: it plays out, then the pause
    /// and the map's own music.
    public void Adopt(string id)
    {
        Reset();
        Playing = id;
        sinceChange = 0;
    }

    /// One frame. `interiorPiece` is the screen's own piece (null on the open map), `pool` the open map's (`Pool`),
    /// `finished` whether the piece playing has ended, `cursorSec` how far into it it is. Returns a piece to start, or null.
    public Cue? Update(string? interiorPiece, string[] pool, bool dreadful, float dt, bool finished, double cursorSec)
    {
        sinceChange += dt;
        string w = interiorPiece ?? "";
        if (w != want) { want = w; wantFor = 0; }
        wantFor += dt;

        if (interior != "")
        {
            interiorFor += dt;
            if (want == "") return wantFor >= Settle ? BackToMap(pool) : null;
            if (want != interior && wantFor >= InteriorSwitch) { interior = want; return Start(want); }
            return null;
        }
        if (want != "" && wantFor >= Settle)
        {
            (resumeId, resumeAt) = finished || Playing == "" ? ("", 0) : (Playing, cursorSec);
            interior = want;
            interiorFor = 0;
            return Start(want);
        }
        if (Playing == "") return Start(Next(pool));

        dreadFor = dreadful && !pool.Contains(Playing) ? dreadFor + dt : 0;
        if (dreadFor >= DreadHold && sinceChange >= MinHold) return Start(Next(pool));
        if (!finished) { gap = -1; return null; }
        if (gap < 0) gap = GapMin + rng.NextSingle() * (GapMax - GapMin);
        if ((gap -= dt) > 0) return null;
        return Start(Next(pool));
    }

    Cue BackToMap(string[] pool)
    {
        interior = "";
        string id = resumeId;
        resumeId = "";
        if (id != "" && interiorFor <= ResumeWithin && MusicLibrary.Get(id) is { } s && resumeAt < s.DurationSec - 10)
            return Start(id, resumeAt);
        return Start(Next(pool));
    }

    string Next(string[] pool)
    {
        if (pool.Length == 0) return "map" + (++cursor % 3);
        for (int tries = 0; tries < pool.Length; tries++)
        {
            string id = pool[++cursor % pool.Length];
            if (id != Playing && MusicLibrary.Has(id)) return id;
        }
        return MusicLibrary.Has(pool[cursor % pool.Length]) ? pool[cursor % pool.Length] : "map" + (cursor % 3);
    }

    Cue Start(string id, double at = 0)
    {
        Playing = id;
        sinceChange = 0;
        dreadFor = 0;
        gap = -1;
        return new Cue(id, at);
    }
}
