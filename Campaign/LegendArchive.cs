using System.Text.Json;

namespace Ironfield;

/// Persistent world chronicle of sealed Legends (docs/pre-demo/LEGENDS.md LG3). Stored in `Saves.Root/legends.json` so every
/// new campaign inherits past captains' monuments, relics and folk memory, while respecting `IRONFIELD_SAVES` isolation.
public static class LegendArchive
{
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };
    static List<SealedLegend>? cache;
    static string cacheRoot = "";

    public static string FilePath => Path.Combine(Saves.Root, "legends.json");

    /// Clear the in-memory cache (used by tests after switching `IRONFIELD_SAVES`).
    public static void Forget() { cache = null; cacheRoot = ""; }

    /// All sealed Legends in this saves folder, newest first.
    public static List<SealedLegend> Load()
    {
        if (cache != null && cacheRoot == Saves.Root) return cache;
        cacheRoot = Saves.Root;
        if (!File.Exists(FilePath)) return cache = new();
        try
        {
            var list = JsonSerializer.Deserialize<List<SealedLegend>>(File.ReadAllText(FilePath), Json);
            cache = list ?? new();
        }
        catch (Exception) { cache = new(); }
        return cache;
    }

    public static void Save(List<SealedLegend> list)
    {
        cache = list;
        cacheRoot = Saves.Root;
        Directory.CreateDirectory(Saves.Root);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(list, Json));
        File.Move(tmp, FilePath, overwrite: true);
    }

    /// Seal a Legend into the archive (replacing any prior entry with the same Id).
    public static void Add(SealedLegend legend)
    {
        var list = Load();
        list.RemoveAll(l => l.Id == legend.Id);
        list.Insert(0, legend);
        Save(list);
    }

    /// Active sealed Legends that should populate new worlds.
    public static List<SealedLegend> Active() => Load().Where(l => l.ActiveInWorld).ToList();

    public static void SetActive(string id, bool active)
    {
        var list = Load();
        var item = list.FirstOrDefault(l => l.Id == id);
        if (item == null) return;
        item.ActiveInWorld = active;
        Save(list);
    }

    public static void Delete(string id)
    {
        var list = Load();
        if (list.RemoveAll(l => l.Id == id) > 0) Save(list);
    }
}
