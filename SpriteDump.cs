using System.IO.Compression;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// `--sprites out.png [filter...]`: paints the sprite sheet headlessly and writes the parts whose key contains any filter
/// (palette 0 and neutral only), enlarged 6x on a grey board in rows, for checking pixel art without running the game.
/// The keys are printed in the order drawn (left to right, top to bottom).
public static class SpriteDump
{
    const int Zoom = 6, Gap = 4, RowW = 900;

    public static void Run(string path, string[] filters)
    {
        var sheet = new SpriteSheet();
        var keys = sheet.Rects.Keys.Where(k => (k.EndsWith("/0") || k.EndsWith("/n")) && (filters.Length == 0 || filters.Any(k.Contains))).OrderBy(k => k).ToList();
        var places = new List<(string Key, int X, int Y)>();
        int x = Gap, y = Gap, rowH = 0;
        foreach (var k in keys)
        {
            var r = sheet.Rects[k];
            if (x + r.Width * Zoom > RowW && x > Gap) { x = Gap; y += rowH + Gap; rowH = 0; }
            places.Add((k, x, y));
            x += r.Width * Zoom + Gap;
            rowH = Math.Max(rowH, r.Height * Zoom);
        }
        int w = RowW, h = y + rowH + Gap;
        var px = Enumerable.Repeat(new Color(128, 140, 128), w * h).ToArray();
        foreach (var (k, ox, oy) in places)
        {
            var r = sheet.Rects[k];
            for (int j = 0; j < r.Height * Zoom; j++)
                for (int i = 0; i < r.Width * Zoom; i++)
                {
                    var c = sheet.Pixels[(r.Y + j / Zoom) * SpriteSheet.W + r.X + i / Zoom];
                    if (c.A > 0 && oy + j < h && ox + i < w) px[(oy + j) * w + ox + i] = c;
                }
        }
        File.WriteAllBytes(path, Png(px, w, h));
        Console.WriteLine($"{places.Count} sprites -> {path}: " + string.Join(", ", places.Select(p => p.Key)));
    }

    /// `--portraits out.png [seed]`: the faces of a new campaign's lords and kin (each in all four moods for the first row),
    /// 3x on one PNG, for checking portrait variety (docs/archive/LORDS.md LD1).
    public static void Portraits(string path, int seed)
    {
        const int Z = 3, Cols = 12;
        var c = Campaign.New(seed);
        var faces = new List<Face>();
        var lords = c.S.Characters.Where(x => !x.Dead && x.Kind is "lord" or "kin" or "knight").GroupBy(x => x.Faction)
            .SelectMany(g => g.Select((x, i) => (x, i))).OrderBy(p => p.i).ThenBy(p => p.x.Faction).Select(p => p.x).ToList();
        var first = PortraitPainter.Of(c, lords[0]);
        for (int m = 0; m < 4; m++) faces.Add(first with { Mood = m });
        foreach (var ch in lords.Skip(1).Take(Cols * 5 - 4)) faces.Add(PortraitPainter.Of(c, ch));
        // A last row of named men (docs/COMPANY.md C1): tiers I, III, V and VII, fresh and then scarred.
        while (faces.Count % Cols != 0) faces.Add(faces[0] with { Seed = -1 });
        int id = 40 + seed;
        foreach (int tier in new[] { 1, 3, 5, 7 })
            if (Troops.All.Where(t => t.Tier == tier && !t.Wild && t.Look.Face == "man").OrderBy(t => t.Id).FirstOrDefault() is UnitType t)
            {
                faces.Add(PortraitPainter.OfMan(id, t, 0, 19 + tier * 2, false, tier, 0));
                faces.Add(PortraitPainter.OfMan(id++, t, 4, 30 + tier * 2, false, tier, 1));
            }
        int cw = PortraitPainter.W * Z + Gap, ch2 = PortraitPainter.H * Z + Gap, rows = (faces.Count + Cols - 1) / Cols;
        int w = Cols * cw + Gap, h = rows * ch2 + Gap;
        var px = Enumerable.Repeat(new Color(60, 60, 64), w * h).ToArray();
        for (int i = 0; i < faces.Count; i++)
        {
            if (faces[i].Seed == -1) continue; // a gap before the named men's row
            var src = PortraitPainter.Paint(faces[i]);
            int ox = Gap + i % Cols * cw, oy = Gap + i / Cols * ch2;
            for (int y = 0; y < PortraitPainter.H * Z; y++)
                for (int x = 0; x < PortraitPainter.W * Z; x++) px[(oy + y) * w + ox + x] = src[y / Z * PortraitPainter.W + x / Z];
        }
        File.WriteAllBytes(path, Png(px, w, h));
        Console.WriteLine($"{faces.Count} portraits -> {path}");
    }

    /// A minimal PNG encoder (8-bit RGB, no filtering).
    internal static byte[] Png(Color[] px, int w, int h)
    {
        var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < w; x++) { var c = px[y * w + x]; raw.WriteByte(c.R); raw.WriteByte(c.G); raw.WriteByte(c.B); }
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) raw.WriteTo(zs);
        var o = new MemoryStream();
        o.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string type, byte[] data)
        {
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            o.Write(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(data.Length)));
            o.Write(t); o.Write(data);
            o.Write(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(Crc(t.Concat(data).ToArray()))));
        }
        var hdr = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(hdr, w);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(hdr.AsSpan(4), h);
        hdr[8] = 8; hdr[9] = 2;
        Chunk("IHDR", hdr); Chunk("IDAT", z.ToArray()); Chunk("IEND", Array.Empty<byte>());
        return o.ToArray();
    }

    static uint Crc(byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (var b in d) { c ^= b; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; }
        return ~c;
    }
}
