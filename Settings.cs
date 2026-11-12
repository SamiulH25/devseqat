using System.Text.Json;

namespace Ironfield;

public enum Difficulty { Easy, Normal, Hard }

/// Player settings, saved as settings.json in the user's Ironfield folder (next to saves/).
public sealed class Settings
{
    public int Width = 1280, Height = 720, UiScale; // UiScale 0 = automatic (by window height)
    public bool Fullscreen, Fxaa = true, VSync = true, Ironman, ColorBlind;
    public string Quality = "High";
    public float MasterVolume = 0.8f, SfxVolume = 1, AmbienceVolume = 0.85f, MusicVolume = 0.6f;
    /// The field camera's kick when blows land (0 off, 0.5 soft, 1 full), and the brief hold when your own blow lands.
    public float CameraShake = 1;
    public bool HitPause = true;
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

    /// The interface scale for a window height (1 below 1440p so 1080p keeps full desktop room, 2 at 1440p, 3 at 4K), unless overridden.
    public static int UiFor(int height) => Current.UiScale > 0 ? Current.UiScale : (height >= 2050 ? 3 : height >= 1350 ? 2 : 1);

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
