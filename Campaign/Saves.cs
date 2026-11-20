using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// What kind of save a file is, read from its name: `auto-<stamp>.json` (rotating), `quick.json`, `named-<slug>.json`.
public enum SaveKind { Auto, Quick, Named }

/// A save's card, written beside it as `<save>.meta` so the save screens can list campaigns without reading whole saves.
public sealed class SaveMeta
{
    public string Captain = "", Company = "", Service = "", Label = "";
    public int Day, Gold, Men, Palette = 2, Version;
    public bool Ironman, GameOver;
    /// When it was written (UTC). Wall-clock time is fine here: it never reaches the campaign's dice.
    public DateTime Saved;

    /// The card of a campaign as it stands.
    public static SaveMeta Of(CampaignState s, string label = "")
    {
        int f = s.Captain.Contract;
        bool serving = f >= 0 && f < s.Factions.Count;
        return new SaveMeta
        {
            Captain = (s.Characters.FirstOrDefault(c => c.Id == s.CaptainChar)?.Name ?? "THE CAPTAIN").ToUpperInvariant(),
            Company = (s.Parties.Count > 0 ? s.Parties[0].Name : "").ToUpperInvariant(),
            Service = (!serving ? "A FREE COMPANY" : (s.Captain.Fealty ? "SWORN TO THE " : "IN THE PAY OF THE ") + s.Factions[f].Name).ToUpperInvariant(),
            Label = label,
            Day = (int)(s.Hours / 24) + 1,
            Gold = (int)s.Gold,
            Men = s.Parties.Count > 0 ? s.Parties[0].Men : 0,
            Palette = !serving ? 2 : s.Factions[f].Founded ? s.Factions[f].Palette : Factions.All[Math.Min(f, Factions.All.Count - 1)].Palette,
            Version = s.Version,
            Ironman = s.Ironman,
            GameOver = s.GameOver,
            Saved = DateTime.UtcNow,
        };
    }
}

/// One save file of a campaign. `Meta` is null when neither the card nor the save could be read (shown as unreadable).
public sealed class SaveEntry
{
    public string Path = "";
    public SaveKind Kind;
    public SaveMeta? Meta;
    public DateTime Saved;
    /// The name shown in the list.
    public string Label => Kind switch { SaveKind.Auto => "AUTOSAVE", SaveKind.Quick => "QUICKSAVE", _ => Meta?.Label is { Length: > 0 } l ? l : "SAVE" };
    /// The map picture taken when it was saved, if there is one.
    public string Thumb => System.IO.Path.ChangeExtension(Path, ".png");
}

/// A campaign's folder and its saves, newest first.
public sealed class CampaignEntry
{
    public string Id = "", Dir = "";
    public List<SaveEntry> Saves = new();
    public SaveEntry Latest => Saves[0];
    /// The newest save that could be read, for the campaign's card.
    public SaveMeta? Card => Saves.Select(e => e.Meta).FirstOrDefault(m => m != null);
}

/// Several campaigns, each in its own folder under `CampaignState.SaveDir` with its own rotating autosaves, a quicksave and
/// saves you name (docs/SAVES.md). An Ironman campaign keeps one autosave and nothing else.
public static class Saves
{
    /// Autosaves a campaign keeps; the oldest goes when another is written.
    public const int AutoKeep = 3;
    /// The longest name a save may be given.
    public const int NameMax = 24;

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    public static string Root => CampaignState.SaveDir;
    public static string DirOf(string id) => Path.Combine(Root, id);

    /// The campaign's folder name, given on its first save: its company and the moment it began.
    public static string EnsureId(CampaignState s)
    {
        if (s.CampaignId != "") return s.CampaignId;
        string stem = Slug(s.Parties.Count > 0 && s.Parties[0].Name != "" ? s.Parties[0].Name : "campaign");
        string id = $"{stem}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        for (int n = 2; Directory.Exists(DirOf(id)); n++) id = $"{stem}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{n}";
        return s.CampaignId = id;
    }

    /// Lower-case letters, digits and dashes: safe as a file name on every system.
    public static string Slug(string s)
    {
        var b = new System.Text.StringBuilder();
        foreach (char ch in s.Trim().ToLowerInvariant())
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') b.Append(ch);
            else if (b.Length > 0 && b[^1] != '-') b.Append('-');
        string r = b.ToString().Trim('-');
        return r == "" ? "save" : r.Length > 40 ? r[..40].Trim('-') : r;
    }

    /// Why this campaign may not be saved this way (null = it may). Ironman campaigns only autosave.
    public static string? CannotSave(CampaignState s, SaveKind kind, string label = "")
    {
        if (s.Ironman && kind != SaveKind.Auto) return "IRONMAN CAMPAIGNS KEEP ONE SAVE";
        if (kind == SaveKind.Named && label.Trim() == "") return "GIVE THE SAVE A NAME";
        return null;
    }

    /// The file a save of this kind is written to.
    public static string PathFor(CampaignState s, SaveKind kind, string label = "") => Path.Combine(DirOf(EnsureId(s)), kind switch
    {
        SaveKind.Auto => $"auto-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json",
        SaveKind.Quick => "quick.json",
        _ => $"named-{Slug(label)}.json",
    });

    /// Write the save and its card; returns the file written. Autosaves rotate (an Ironman campaign keeps one).
    public static string Save(CampaignState s, SaveKind kind, string label = "")
    {
        if (s.Ironman) kind = SaveKind.Auto;
        string path = PathFor(s, kind, label);
        // Two autosaves in one millisecond would share a name: step on.
        for (int n = 1; kind == SaveKind.Auto && File.Exists(path); n++) path = path[..^5] + $"-{n}.json";
        s.SaveFile(path);
        Changed();
        WriteMeta(path, SaveMeta.Of(s, kind == SaveKind.Named ? label.Trim().ToUpperInvariant() : ""));
        if (kind == SaveKind.Auto) Prune(s.CampaignId, s.Ironman ? 1 : AutoKeep, path);
        // An Ironman campaign has its one save: anything else in its folder (from before it was Ironman) goes.
        if (s.Ironman) foreach (var e in List(DirOf(s.CampaignId)).Where(e => e.Path != path)) Delete(e, keepFolder: true);
        return path;
    }

    static void WriteMeta(string path, SaveMeta m)
    {
        try { File.WriteAllText(Path.ChangeExtension(path, ".meta"), JsonSerializer.Serialize(m, Json)); }
        catch (Exception e) { Console.WriteLine("save card not written: " + e.Message); }
    }

    /// Keep the newest `keep` autosaves of a campaign (and never `keepPath`).
    static void Prune(string id, int keep, string keepPath)
    {
        foreach (var e in List(DirOf(id)).Where(e => e.Kind == SaveKind.Auto && e.Path != keepPath).Skip(keep - 1)) Delete(e, keepFolder: true);
    }

    static SaveKind? KindOf(string file)
    {
        string n = Path.GetFileName(file);
        return n == "quick.json" ? SaveKind.Quick : n.StartsWith("auto-") ? SaveKind.Auto : n.StartsWith("named-") ? SaveKind.Named : null;
    }

    /// The saves in one campaign folder, newest first.
    public static List<SaveEntry> List(string dir)
    {
        var list = new List<SaveEntry>();
        if (!Directory.Exists(dir)) return list;
        foreach (var f in Directory.GetFiles(dir, "*.json"))
        {
            if (KindOf(f) is not SaveKind k) continue;
            var meta = ReadMeta(f);
            list.Add(new SaveEntry { Path = f, Kind = k, Meta = meta, Saved = meta?.Saved is { } t && t != default ? t : File.GetLastWriteTimeUtc(f) });
        }
        return list.OrderByDescending(e => e.Saved).ThenBy(e => e.Path, StringComparer.Ordinal).ToList();
    }

    /// A save's card from its .meta file, or (no card, or a broken one) from the save itself, writing the card for next time.
    static SaveMeta? ReadMeta(string path)
    {
        string mp = Path.ChangeExtension(path, ".meta");
        try { if (File.Exists(mp) && JsonSerializer.Deserialize<SaveMeta>(File.ReadAllText(mp), Json) is { } m) return m; }
        catch (Exception) { }
        try
        {
            var m = SaveMeta.Of(CampaignState.Deserialize(File.ReadAllText(path)));
            m.Saved = File.GetLastWriteTimeUtc(path);
            WriteMeta(path, m);
            return m;
        }
        catch (Exception) { return null; }
    }

    static List<CampaignEntry>? index;
    static string indexRoot = "";

    /// Every campaign on disk that has a save, the one saved last first. Old single-slot saves are moved in first.
    /// Kept until a save is written or deleted here (the menus ask every frame).
    public static List<CampaignEntry> Campaigns()
    {
        if (index != null && indexRoot == Root) return index;
        Migrate();
        indexRoot = Root;
        return index = Scan();
    }

    /// Read the folders afresh on the next `Campaigns`.
    public static void Changed() => index = null;

    static List<CampaignEntry> Scan()
    {
        var list = new List<CampaignEntry>();
        if (!Directory.Exists(Root)) return list;
        foreach (var d in Directory.GetDirectories(Root))
        {
            var saves = List(d);
            if (saves.Count > 0) list.Add(new CampaignEntry { Id = Path.GetFileName(d), Dir = d, Saves = saves });
        }
        return list.OrderByDescending(c => c.Latest.Saved).ToList();
    }

    /// The save CONTINUE opens: the newest readable save of any campaign whose war is not over.
    public static SaveEntry? Latest() => Campaigns().SelectMany(c => c.Saves).Where(e => e.Meta is { GameOver: false })
        .OrderByDescending(e => e.Saved).FirstOrDefault();

    /// Read a save; null (with `CampaignState.LoadError`) when it cannot be. The state learns which campaign folder it is from.
    public static CampaignState? Load(string path)
    {
        var s = CampaignState.LoadFile(path);
        if (s != null) s.CampaignId = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path))) ?? s.CampaignId;
        return s;
    }

    /// Remove a save with its backup, card and picture; an emptied campaign folder goes too.
    public static void Delete(SaveEntry e, bool keepFolder = false)
    {
        Changed();
        foreach (var f in new[] { e.Path, e.Path + ".bak", e.Path + ".tmp", Path.ChangeExtension(e.Path, ".meta"), e.Thumb })
            try { if (File.Exists(f)) File.Delete(f); } catch (Exception x) { Console.WriteLine($"could not delete {f}: {x.Message}"); }
        string? dir = Path.GetDirectoryName(e.Path);
        if (!keepFolder && dir != null && Directory.Exists(dir) && List(dir).Count == 0)
            try { Directory.Delete(dir, true); } catch (Exception) { }
    }

    /// Remove a whole campaign and every save in it.
    public static void DeleteCampaign(string id)
    {
        Changed();
        string d = DirOf(id);
        if (id != "" && Directory.Exists(d)) Directory.Delete(d, true);
    }

    /// The war is over (the Ironman captain lost, or you retired): an Ironman campaign goes; another loses its autosaves,
    /// so CONTINUE does not open a war that has ended. Its quick and named saves stay.
    public static void EndCampaign(CampaignState s)
    {
        if (s.CampaignId == "") return;
        if (s.Ironman) { DeleteCampaign(s.CampaignId); return; }
        foreach (var e in List(DirOf(s.CampaignId)).Where(e => e.Kind == SaveKind.Auto)) Delete(e);
    }

    /// The map as it was when the save was made, shrunk to a thumbnail (box filter) and written beside it.
    public static void WriteThumb(string savePath, Color[] px, int w, int h, int tw = 320)
    {
        int th = Math.Max(1, tw * h / Math.Max(1, w));
        var o = new Color[tw * th];
        for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                int x0 = x * w / tw, x1 = Math.Max(x0 + 1, (x + 1) * w / tw), y0 = y * h / th, y1 = Math.Max(y0 + 1, (y + 1) * h / th);
                int r = 0, g = 0, b = 0, n = 0;
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++) { var c = px[yy * w + xx]; r += c.R; g += c.G; b += c.B; n++; }
                o[y * tw + x] = new Color(r / n, g / n, b / n, 255);
            }
        try { File.WriteAllBytes(Path.ChangeExtension(savePath, ".png"), Png.Encode(o, tw, th)); Changed(); }
        catch (Exception e) { Console.WriteLine("save picture not written: " + e.Message); }
    }

    static bool migrated;

    /// Saves from before campaign folders (`autosave.json`, `quicksave.json` in the save folder) move into a folder each,
    /// one folder for both when they are the same campaign. Saves too old to read move as well, so they can be deleted.
    public static void Migrate()
    {
        if (migrated) return;
        migrated = true;
        var moved = new Dictionary<string, string>(); // campaign key -> folder id
        foreach (var (old, kind) in new[] { ("autosave", SaveKind.Auto), ("quicksave", SaveKind.Quick) })
        {
            string p = Path.Combine(Root, old + ".json");
            if (!File.Exists(p)) continue;
            try
            {
                CampaignState? s = null;
                try { s = CampaignState.Deserialize(File.ReadAllText(p)); } catch (Exception) { }
                string key = s == null ? "old-" + old : $"{s.Seed}:{s.CaptainChar}:{(s.Parties.Count > 0 ? s.Parties[0].Name : "")}";
                if (!moved.TryGetValue(key, out var id))
                {
                    id = s != null ? EnsureId(s) : $"old-{old}-{File.GetLastWriteTimeUtc(p):yyyyMMdd-HHmmss}";
                    moved[key] = id;
                }
                string dir = DirOf(id);
                Directory.CreateDirectory(dir);
                var stamp = File.GetLastWriteTimeUtc(p);
                string to = Path.Combine(dir, kind == SaveKind.Auto ? $"auto-{stamp:yyyyMMdd-HHmmss-fff}.json" : "quick.json");
                File.Move(p, to, true);
                if (File.Exists(p + ".bak")) File.Move(p + ".bak", to + ".bak", true);
                if (s != null) { var m = SaveMeta.Of(s); m.Saved = stamp; WriteMeta(to, m); }
                Console.WriteLine($"moved the old {old} into {id}");
            }
            catch (Exception e) { Console.WriteLine($"old {old} not moved: {e.Message}"); }
        }
    }

    /// For tests: let `Migrate` look again (another save folder).
    public static void Forget() { migrated = false; Changed(); }
}
