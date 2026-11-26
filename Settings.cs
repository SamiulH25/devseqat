using System.Text.Json;

namespace Ironfield;

public enum Difficulty { Easy, Normal, Hard }

/// Player settings, saved as settings.json in the user's Ironfield folder (next to saves/).
public sealed class Settings
{
    public int Width = 1280, Height = 720;
    public float UiScale; // 0 = automatic (0.25x steps fitted to window width & height)
    public static readonly float[] UiScales = { 0f, 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f, 3f };
    public bool Fullscreen, Fxaa = true, VSync = true, Ironman, ColorBlind;
    /// New campaigns: a captain who falls on a lost field may die, and a companion takes command (docs/archive/COMPANY.md C7).
    public bool CaptainDeath;
    public string Quality = "High";
    /// Real-time 3D pixel-art renderer (false = classic 2D sprite puppets; Low quality always falls back to 2D).
    public bool PixelArt = true;
    /// Pixel-art pixel size (1 = native, 2 = classic 2x pixel grid, 3 = chunky 3x).
    public int PixelScale = 2;
    /// Continuous 3D model detail slider (0.20..1.00) + automatic FPS-driven dynamic LOD scaling.
    public float ModelDetail = 1f;
    public bool DynamicLod = true;

    static readonly bool? EnvPixelArt = Environment.GetEnvironmentVariable("IRONFIELD_3D_UNITS") is { Length: > 0 } e3d ? e3d != "0" : null;
    static readonly int? EnvPixelScale = int.TryParse(Environment.GetEnvironmentVariable("IRONFIELD_PIXEL_SCALE"), out var eps) && eps is >= 1 and <= 4 ? eps : null;

    public bool EffectivePixelArt => EnvPixelArt ?? PixelArt;
    public int EffectivePixelScale => EnvPixelScale ?? Math.Clamp(PixelScale, 1, 3);
    public float MasterVolume = 0.8f, SfxVolume = 1, AmbienceVolume = 0.85f, MusicVolume = 0.6f, VoiceVolume = 0.9f;
    /// Battle flavor bubbles & voice callouts: 2 = All, 1 = Key Only (sergeants & duels), 0 = Off.
    public int BattleBarks = 2;
    /// The field camera's kick when blows land (0 off, 0.5 soft, 1 full), and the brief hold when your own blow lands.
    public float CameraShake = 1;
    public bool HitPause = true;
    /// Melee direction control: false = mouse-flick (default, lock-on-press), true = movement keys (WASD).
    public bool MoveDirControl;
    /// How much of the news the campaign feed shows (Campaign.Log levels): Yours, Notable or Everything.
    public LogLevel LogLevel = LogLevel.Notable;
    /// The battle's one cinematic moment (docs/archive/SPECTACLE.md): a great working, a breakthrough or a clash slows the view
    /// for a breath and draws bars top and bottom.
    public bool Cinematics = true;
    /// Pause the battle when a company breaks, enemy horse reaches a flank, or a division loses its sergeant (docs/archive/COMBAT_QOL.md Q5).
    public bool PauseOnAlerts;
    public Difficulty Difficulty = Difficulty.Normal;
    /// Most men on a battlefield at once, both sides together (the rest wait as reinforcements).
    public int BattleSize = 1500;
    public static readonly int[] BattleSizes = { 400, 800, 1500, 3000 };
    public HashSet<string> TipsSeen = new();
    /// Set once the player completes or skips the first-campaign interactive tutorial (never shown again unless reset in Settings).
    public bool TutorialDone;

    public static string Folder => Environment.GetEnvironmentVariable("IRONFIELD_SAVES") is { Length: > 0 } d ? Path.GetDirectoryName(Path.GetFullPath(d)) ?? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ironfield");
    static string FilePath => Path.Combine(Folder, "settings.json");
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    public static Settings Current { get; private set; } = Load();

    static Settings Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings(); }
        catch (Exception e) { Console.WriteLine("settings.json unreadable, using defaults: " + e.Message); }
        return new Settings();
    }

    public void Save()
    {
        try { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json)); }
        catch (Exception e) { Console.WriteLine("could not save settings: " + e.Message); }
    }

    /// The interface scale for a window size, in 0.25x steps from 1.0x (720p) to 3.0x (4K), fitted to both axes so
    /// 900p (1.25x), 1080p (1.5x), 1440p (2.0x) and 4K (3.0x) keep consistent proportions without jumping 1 -> 2 -> 3.
    public static float UiFor(int width, int height)
    {
        if (Current.UiScale > 0) return Math.Clamp(Current.UiScale, 0.75f, 3f);
        float fit = MathF.Min(width / 1280f, height / 720f);
        return Math.Clamp(MathF.Floor(fit * 4f + 0.05f) / 4f, 1f, 3f);
    }
    public static float UiFor(int height) => UiFor(height * 16 / 9, height);

    /// Difficulty knobs: how hard enemies hit in battle, and how well contracts pay.
    public float EnemyDamage => Difficulty switch { Difficulty.Easy => 0.8f, Difficulty.Hard => 1.2f, _ => 1f };
    public float PayFactor => Difficulty switch { Difficulty.Easy => 1.15f, Difficulty.Hard => 0.85f, _ => 1f };

    /// Show a one-time tip: true the first time a key is asked for.
    public bool FirstTime(string key)
    {
        if (!TipsSeen.Add(key)) return false;
        Save();
        return true;
    }
}

/// Crash safety: unhandled exceptions are written to crash logs next to the saves.
public static class CrashLog
{
    public static void Install() => AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(e.ExceptionObject as Exception);

    public static string? Write(Exception? e)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            var path = Path.Combine(Settings.Folder, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, $"Ironfield crashed at {DateTime.Now:u}\n{Environment.OSVersion}, .NET {Environment.Version}\n\n{e}");
            Console.Error.WriteLine("crash log written to " + path);
            return path;
        }
        catch { return null; }
    }
}
