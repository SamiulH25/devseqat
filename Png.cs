using System.Buffers.Binary;
using System.IO.Compression;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// A small headless PNG codec for the baked sprites (no graphics device, so the self-test can load them).
/// Writes 8-bit RGBA. Reads what paint programs save: 8-bit greyscale, grey+alpha, RGB, RGBA and indexed (with tRNS),
/// not interlaced. Pixels in memory are premultiplied, as the atlas is drawn; files hold straight alpha.
public static class Png
{
    static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static byte[] Encode(Color[] px, int w, int h)
    {
        var raw = new byte[h * (w * 4 + 1)];
        for (int y = 0, o = 0; y < h; y++)
        {
            raw[o++] = 0;
            for (int x = 0; x < w; x++)
            {
                var c = px[y * w + x];
                if (c.A == 0) { o += 4; continue; } // fully clear: all zero, so the files diff cleanly
                float k = 255f / c.A;
                raw[o++] = (byte)Math.Min(255, MathF.Round(c.R * k)); raw[o++] = (byte)Math.Min(255, MathF.Round(c.G * k));
                raw[o++] = (byte)Math.Min(255, MathF.Round(c.B * k)); raw[o++] = c.A;
            }
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.SmallestSize, true)) zs.Write(raw);
        var o2 = new MemoryStream();
        o2.Write(Signature);
        var hdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(hdr, w);
        BinaryPrimitives.WriteInt32BigEndian(hdr.AsSpan(4), h);
        hdr[8] = 8; hdr[9] = 6;
        Chunk(o2, "IHDR", hdr); Chunk(o2, "IDAT", z.ToArray()); Chunk(o2, "IEND", Array.Empty<byte>());
        return o2.ToArray();
    }

    /// Decodes a PNG to premultiplied pixels; throws InvalidDataException naming what it cannot read.
    public static (Color[] Px, int W, int H) Decode(byte[] file)
    {
        if (file.Length < 8 || !file.AsSpan(0, 8).SequenceEqual(Signature)) throw new InvalidDataException("not a PNG");
        int w = 0, h = 0, depth = 0, type = 0, at = 8;
        byte[]? palette = null, alpha = null;
        var idat = new MemoryStream();
        while (at + 8 <= file.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(at));
            string kind = System.Text.Encoding.ASCII.GetString(file, at + 4, 4);
            var data = file.AsSpan(at + 8, len);
            switch (kind)
            {
                case "IHDR":
                    w = BinaryPrimitives.ReadInt32BigEndian(data); h = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    depth = data[8]; type = data[9];
                    if (data[12] != 0) throw new InvalidDataException("interlaced PNGs are not supported: save without interlacing");
                    if (depth != 8) throw new InvalidDataException($"{depth}-bit PNGs are not supported: save as 8-bit");
                    break;
                case "PLTE": palette = data.ToArray(); break;
                case "tRNS": alpha = data.ToArray(); break;
                case "IDAT": idat.Write(data); break;
            }
            if (kind == "IEND") break;
            at += 12 + len;
        }
        int bpp = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"PNG colour type {type} is not supported") };
        if (type == 3 && palette == null) throw new InvalidDataException("indexed PNG without a palette");
        int stride = w * bpp;
        var raw = new byte[h * stride];
        idat.Position = 0;
        using (var zs = new ZLibStream(idat, CompressionMode.Decompress))
        {
            var row = new byte[stride + 1];
            for (int y = 0; y < h; y++)
            {
                zs.ReadExactly(row);
                Unfilter(row[0], row.AsSpan(1), y > 0 ? raw.AsSpan((y - 1) * stride, stride) : Span<byte>.Empty, bpp);
                row.AsSpan(1).CopyTo(raw.AsSpan(y * stride));
            }
        }
        var px = new Color[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int o = i * bpp;
            (int r, int g, int b, int a) = type switch
            {
                0 => (raw[o], raw[o], raw[o], 255),
                4 => (raw[o], raw[o], raw[o], raw[o + 1]),
                2 => (raw[o], raw[o + 1], raw[o + 2], 255),
                6 => (raw[o], raw[o + 1], raw[o + 2], raw[o + 3]),
                _ => (palette![raw[o] * 3], palette[raw[o] * 3 + 1], palette[raw[o] * 3 + 2], alpha != null && raw[o] < alpha.Length ? alpha[raw[o]] : 255),
            };
            px[i] = a == 0 ? default : a == 255 ? new Color(r, g, b, 255) : Color.FromNonPremultiplied(r, g, b, a);
        }
        return (px, w, h);
    }

    static void Unfilter(byte filter, Span<byte> cur, Span<byte> prev, int bpp)
    {
        for (int i = 0; i < cur.Length; i++)
        {
            int a = i >= bpp ? cur[i - bpp] : 0, b = prev.IsEmpty ? 0 : prev[i], c = i >= bpp && !prev.IsEmpty ? prev[i - bpp] : 0;
            cur[i] += filter switch
            {
                0 => 0,
                1 => (byte)a,
                2 => (byte)b,
                3 => (byte)((a + b) / 2),
                4 => Paeth(a, b, c),
                _ => throw new InvalidDataException($"bad PNG row filter {filter}"),
            };
        }
    }

    static byte Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
    }

    static void Chunk(Stream o, string type, byte[] data)
    {
        var t = System.Text.Encoding.ASCII.GetBytes(type);
        var n = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(n, data.Length); o.Write(n);
        o.Write(t); o.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(n, Crc(t.Concat(data).ToArray())); o.Write(n);
    }

    static uint Crc(byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (var b in d) { c ^= b; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; }
        return c ^ 0xFFFFFFFF;
    }
}
