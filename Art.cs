using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// GPU side of the unit art: the painted sprite atlas (see SpriteSheet), the blob shadow and a white pixel.
/// (The world itself - terrain and foliage - is 3D geometry, see World.cs.)
public sealed class Art
{
    public const float Px = Style.Px; // metres per sprite pixel
    public readonly Texture2D Atlas, Shadow, Pixel;
    readonly SpriteSheet sheet;

    public Rectangle this[string key] => sheet.Rects.TryGetValue(key, out var r) ? r : throw new KeyNotFoundException("no sprite " + key);
    public bool TryGet(string key, out Rectangle r) => sheet.Rects.TryGetValue(key, out r);

    public Art(GraphicsDevice gd)
    {
        sheet = new SpriteSheet();
        Atlas = new Texture2D(gd, SpriteSheet.W, SpriteSheet.H);
        Atlas.SetData(sheet.Pixels);

        var shadow = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new(x + 0.5f, y + 0.5f), new(16, 16)) / 16;
                shadow[y * 32 + x] = new Color(0, 0, 0, (int)(110 * Math.Clamp(1 - d * d, 0, 1))); // premultiplied
            }
        Shadow = new Texture2D(gd, 32, 32);
        Shadow.SetData(shadow);

        Pixel = new Texture2D(gd, 1, 1);
        Pixel.SetData(new[] { Color.White });
    }

    public static Texture2D Mipped(GraphicsDevice gd, Color[] data, int size)
    {
        var t = new Texture2D(gd, size, size, true, SurfaceFormat.Color);
        for (int level = 0; ; level++)
        {
            t.SetData(level, null, data, 0, data.Length);
            if (size == 1) return t;
            int h = size / 2;
            var next = new Color[h * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < h; x++)
                {
                    var a = data[2 * y * size + 2 * x].ToVector4() + data[2 * y * size + 2 * x + 1].ToVector4()
                          + data[(2 * y + 1) * size + 2 * x].ToVector4() + data[(2 * y + 1) * size + 2 * x + 1].ToVector4();
                    next[y * h + x] = new Color(a / 4);
                }
            data = next;
            size = h;
        }
    }
}
