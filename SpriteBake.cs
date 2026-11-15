using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// The unit sprites as files (docs/DEVELOPING.md "Sprites as PNGs"). SpriteSheet's painting code is the pipeline;
/// `--bake-sprites` writes its output to Data/sprites, and the game loads those PNGs (Load), not the code.
///
/// Data/sprites/
///   parts/KEY.png            one master per team-coloured part, painted in the Azure palette (0)
///   neutral/KEY.png          one-palette parts: horse and beast coats (tinted at draw time), men's faces
///   sides/SCHEME/P/KEY.png   a palette's own copy of a part a colour swap cannot make (its charge differs, say)
///   manifest.json            the parts in paint order and, per scheme (standard, colorblind) and palette, the colour
///                            swap from the master's colours to that palette's
/// Every other palette is the master with its colours swapped, so an edit to a master shows on every side as long as it
/// uses the master's team colours for team cloth. A part's size is the rig's (Puppet.cs reads metres from pixels):
/// keep a PNG the size it was baked at; SpritesBaked fails otherwise.
public static class SpriteBake
{
    public const string Folder = "Data/sprites";

    sealed class Manifest
    {
        public int Version = 1;
        public List<string> Parts = new(), Neutral = new();
        public Dictionary<string, Scheme> Schemes = new();
    }

    sealed class Scheme
    {
        /// Per palette: master colour -> this palette's colour, both "rrggbb".
        public List<Dictionary<string, string>> Swaps = new();
        /// Per palette: the parts read from sides/SCHEME/P/ instead of swapped.
        public List<List<string>> Own = new();
    }

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    static string SchemeName(bool colorBlind) => colorBlind ? "colorblind" : "standard";

    /// The baked folder in the working directory (so a PNG edited in the source tree shows at once, even with --no-build),
    /// else the copy next to the game; null when none is baked.
    static string? Find()
    {
        foreach (var dir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var p = Path.Combine(dir, Folder);
            if (File.Exists(Path.Combine(p, "manifest.json"))) return p;
        }
        return null;
    }

    /// The baked parts in paint order for this scheme, or null when Data/sprites has none (then the code paints them).
    public static List<SpriteSheet.Part>? Load(bool colorBlind, string? root = null)
    {
        root ??= Find();
        if (root == null) return null;
        var m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(root, "manifest.json")), Json)!;
        var scheme = m.Schemes[SchemeName(colorBlind)];
        var masters = m.Parts.ToDictionary(k => k, k => Read(Path.Combine(root, "parts", k + ".png")));
        var parts = new List<SpriteSheet.Part>();
        for (int p = 0; p < scheme.Swaps.Count; p++)
        {
            var swap = scheme.Swaps[p].ToDictionary(kv => Hex(kv.Key), kv => Hex(kv.Value));
            var own = scheme.Own[p].ToHashSet();
            foreach (var k in m.Parts)
            {
                var (px, w, h) = own.Contains(k) ? Read(Path.Combine(root, "sides", SchemeName(colorBlind), p.ToString(), k + ".png")) : Swap(masters[k], swap);
                parts.Add(new($"{k}/{p}", px, w, h));
            }
        }
        foreach (var k in m.Neutral)
        {
            var (px, w, h) = Read(Path.Combine(root, "neutral", k + ".png"));
            parts.Add(new($"{k}/n", px, w, h));
        }
        return parts;
    }

    static (Color[], int, int) Read(string path)
    {
        try { return Png.Decode(File.ReadAllBytes(path)); }
        catch (Exception e) when (e is IOException or InvalidDataException) { throw new InvalidDataException($"sprite {path}: {e.Message}", e); }
    }

    static (Color[], int, int) Swap((Color[] Px, int W, int H) m, Dictionary<uint, uint> swap)
    {
        var px = new Color[m.Px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            var c = m.Px[i];
            px[i] = c.A == 255 && swap.TryGetValue(Rgb(c), out var to) ? new Color((int)(to >> 16), (int)(to >> 8 & 255), (int)(to & 255)) : c;
        }
        return (px, m.W, m.H);
    }

    static uint Rgb(Color c) => (uint)(c.R << 16 | c.G << 8 | c.B);
    static uint Hex(string s) => Convert.ToUInt32(s, 16);
    static string Hex(uint c) => c.ToString("x6");

    /// `--bake-sprites [--force] [filter...]`: paints every part from code and writes the PNGs and manifest into
    /// ./Data/sprites. Existing PNGs are kept (they may be hand-edited) unless --force, which repaints those whose key
    /// contains a filter (all of them with no filter). The manifest is always rewritten.
    /// `--bake-sprites --check`: lists the parts whose PNG differs from what the code paints now (edited by hand, or
    /// the code changed since the bake).
    public static void Run(string[] args)
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), Folder);
        if (args.Contains("--check")) { Check(root); return; }
        bool force = args.Contains("--force");
        var filters = args.Where(a => !a.StartsWith("--")).ToArray();
        bool Repaint(string k) => force && (filters.Length == 0 || filters.Any(k.Contains));

        var standard = SpriteSheet.Paint(Style.Standard());
        var m = new Manifest();
        var masters = new Dictionary<string, SpriteSheet.Part>();
        foreach (var part in standard)
        {
            var (name, pal) = Split(part.Key);
            if (pal == "n") m.Neutral.Add(name);
            else if (pal == "0") { m.Parts.Add(name); masters[name] = part; }
        }
        int written = 0, kept = 0;
        void Write(string path, SpriteSheet.Part part, string key)
        {
            if (File.Exists(path) && !Repaint(key)) { kept++; return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Png.Encode(part.Px, part.W, part.H));
            written++;
        }
        foreach (var k in m.Parts) Write(Path.Combine(root, "parts", k + ".png"), masters[k], k);
        foreach (var part in standard.Where(p => p.Key.EndsWith("/n"))) Write(Path.Combine(root, "neutral", Split(part.Key).Name + ".png"), part, part.Key);

        foreach (bool cb in new[] { false, true })
        {
            var painted = (cb ? SpriteSheet.Paint(Style.ColorBlind()) : standard).ToDictionary(p => p.Key);
            int sides = (cb ? Style.ColorBlind() : Style.Standard()).Length;
            var scheme = new Scheme();
            for (int p = 0; p < sides; p++)
            {
                var swap = SwapFor(m.Parts.Select(k => (masters[k], painted[$"{k}/{p}"])));
                var own = new List<string>();
                foreach (var k in m.Parts)
                {
                    var want = painted[$"{k}/{p}"];
                    if (Same(Swap((masters[k].Px, masters[k].W, masters[k].H), swap), want)) continue;
                    own.Add(k);
                    Write(Path.Combine(root, "sides", SchemeName(cb), p.ToString(), k + ".png"), want, $"{k}/{p}");
                }
                scheme.Swaps.Add(swap.OrderBy(kv => kv.Key).ToDictionary(kv => Hex(kv.Key), kv => Hex(kv.Value)));
                scheme.Own.Add(own);
            }
            m.Schemes[SchemeName(cb)] = scheme;
            Console.WriteLine($"{SchemeName(cb)}: {sides} palettes, {scheme.Own.Sum(o => o.Count)} parts with their own copy");
        }
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(m, Json));
        Console.WriteLine($"{m.Parts.Count} team parts, {m.Neutral.Count} neutral parts: {written} PNGs written, {kept} kept -> {root}");
    }

    /// The colour swap from master to one palette: for each master colour, the palette colour it most often becomes.
    static Dictionary<uint, uint> SwapFor(IEnumerable<(SpriteSheet.Part Master, SpriteSheet.Part Side)> pairs)
    {
        var votes = new Dictionary<uint, Dictionary<uint, int>>();
        foreach (var (a, b) in pairs)
        {
            if (a.W != b.W || a.H != b.H) continue;
            for (int i = 0; i < a.Px.Length; i++)
            {
                if (a.Px[i].A != 255 || b.Px[i].A != 255) continue;
                uint from = Rgb(a.Px[i]), to = Rgb(b.Px[i]);
                if (!votes.TryGetValue(from, out var v)) votes[from] = v = new();
                v[to] = v.GetValueOrDefault(to) + 1;
            }
        }
        return votes.Select(kv => (kv.Key, To: kv.Value.MaxBy(t => t.Value).Key)).Where(t => t.Key != t.To).ToDictionary(t => t.Key, t => t.To);
    }

    static bool Same((Color[] Px, int W, int H) a, SpriteSheet.Part b) => a.W == b.W && a.H == b.H && a.Px.AsSpan().SequenceEqual(b.Px);

    static (string Name, string Pal) Split(string key)
    {
        int i = key.LastIndexOf('/');
        return (key[..i], key[(i + 1)..]);
    }

    /// Reports the baked parts that differ from what the code paints now, and any part the code has that is not baked.
    static void Check(string root)
    {
        foreach (bool cb in new[] { false, true })
        {
            var baked = Load(cb, root)?.ToDictionary(p => p.Key) ?? throw new FileNotFoundException("nothing baked: run --bake-sprites", root);
            var painted = SpriteSheet.Paint(cb ? Style.ColorBlind() : Style.Standard());
            var missing = painted.Where(p => !baked.ContainsKey(p.Key)).Select(p => p.Key).ToList();
            var differ = painted.Where(p => baked.TryGetValue(p.Key, out var b) && !Same((b.Px, b.W, b.H), p)).Select(p => p.Key).ToList();
            Console.WriteLine($"{SchemeName(cb)}: {painted.Count} parts, {missing.Count} not baked, {differ.Count} differ from the code");
            foreach (var k in missing.Take(40)) Console.WriteLine("  not baked: " + k);
            foreach (var k in differ.Where(k => k.EndsWith("/0") || k.EndsWith("/n")).Take(80)) Console.WriteLine("  differs: " + k);
        }
    }
}
