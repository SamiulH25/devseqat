using Microsoft.Xna.Framework;

namespace Ironfield;

public enum Emblem { Chevron, Saltire, Lozenge, None, Bend }

/// A side's heraldry: the cloth it dyes, the trim and charge painted on shields and tabards, and its UI colour.
public sealed record Heraldry(string Name, Color Cloth, Color ClothDark, Color Trim, Emblem Charge, Color Ui);

/// The design tokens of docs/DESIGN.md in one place, shared by the sprite painter, the 3D world and the HUD.
public static class Style
{
    // ---------- sides ----------

    /// Palette index: 0 = the Azure Crown, 1 = the Crimson Compact, 2 = you (gold, so you can always find yourself), 3 = bandits,
    /// 4 = the Northmarch clans.
    public static readonly Heraldry[] Sides = Settings.Current.ColorBlind ? ColorBlind() : Standard();

    /// Colour-blind-safe sides (roadmap D3): blue against vermilion and reddish purple (Okabe-Ito hues), which stay apart
    /// under deuteranopia and protanopia. You stay gold; brigands stay grey-brown. Chosen at start-up (Settings).
    static Heraldry[] ColorBlind() => new Heraldry[]
    {
        new("Azure", new(0, 96, 160), new(0, 56, 100), new(236, 228, 196), Emblem.Chevron, new(86, 180, 233)),
        new("Crimson", new(204, 92, 0), new(120, 52, 0), new(250, 236, 200), Emblem.Saltire, new(230, 159, 0)),
        new("Gold", new(210, 168, 54), new(126, 88, 30), new(44, 36, 44), Emblem.Lozenge, new(240, 228, 66)),
        new("Brigands", new(112, 98, 78), new(70, 62, 52), new(160, 150, 128), Emblem.None, new(190, 170, 130)),
        new("Northmarch", new(160, 84, 132), new(96, 46, 80), new(240, 236, 232), Emblem.Bend, new(204, 121, 167)),
    };

    static Heraldry[] Standard() => new Heraldry[]
    {
        new("Azure", new(54, 94, 176), new(30, 50, 108), new(230, 218, 178), Emblem.Chevron, new(112, 160, 255)),
        new("Crimson", new(172, 48, 42), new(98, 26, 28), new(222, 178, 72), Emblem.Saltire, new(236, 88, 70)),
        new("Gold", new(210, 168, 54), new(126, 88, 30), new(44, 36, 44), Emblem.Lozenge, new(240, 200, 60)),
        new("Brigands", new(112, 98, 78), new(70, 62, 52), new(160, 150, 128), Emblem.None, new(190, 170, 130)),
        new("Northmarch", new(46, 104, 74), new(24, 58, 42), new(226, 228, 220), Emblem.Bend, new(96, 200, 140)),
    };

    /// The palette a campaign faction paints with (the Northmarch clans are palette 4; -1 and -2 are free captains and brigands).
    public static int PaletteOf(int faction) => faction switch { 2 => 4, -2 => 3, < 0 => 0, _ => faction };

    // ---------- materials (base tones; the painter builds a 5-step ramp from each) ----------

    public static readonly Color Skin = new(226, 180, 140), Hair = new(96, 64, 40), Leather = new(118, 80, 48),
        Wood = new(140, 98, 58), Steel = new(172, 178, 190), Iron = new(96, 100, 112), Brass = new(214, 172, 70),
        Linen = new(222, 214, 190), Blood = new(128, 22, 22), Coat = new(208, 208, 208), Mane = new(92, 92, 92), Hoof = new(64, 60, 58);

    /// Horse coats, as multipliers on the neutral grey coat sprites: bay, chestnut, black, grey, dun.
    public static readonly Vector3[] Coats =
    {
        new(0.74f, 0.5f, 0.32f), new(0.86f, 0.52f, 0.3f), new(0.34f, 0.31f, 0.31f), new(0.9f, 0.9f, 0.92f), new(0.88f, 0.74f, 0.52f),
    };

    /// Light for sprites, in sprite space (x right, y down, z toward the viewer): from the upper left, like the sun.
    public static readonly Vector3 SpriteLight = Vector3.Normalize(new Vector3(-0.55f, -0.6f, 0.62f));

    /// Pixel density of unit sprites: metres per sprite pixel (a man is ~58 px tall).
    public const float Px = 0.03f;

    // ---------- interface ----------

    public static readonly Color Accent = new(240, 200, 60);          // gold: you, the selection, the highlighted choice
    public static readonly Color Friendly = Sides[0].Ui, Foe = Sides[1].Ui;
    public static readonly Color Ok = new(110, 196, 104), Warn = new(236, 186, 60), Bad = new(226, 72, 60), Orange = new(240, 140, 50);
    public static readonly Color Text = new(238, 236, 228), Dim = new(170, 174, 182), HorseBar = new(168, 116, 70);
    public static readonly Color Panel = new Color(16, 18, 24) * 0.82f, PanelEdge = Color.White * 0.16f, PanelLight = Color.White * 0.07f;

    /// Spacing grid: every gap and margin is a multiple of this (times the UI scale).
    public const int Grid = 4;

    /// Type scale for the 5x7 pixel font (times the UI scale): captions, body, headings, titles.
    public const int Caption = 1, Body = 2, Heading = 3, Title = 5;

    public static readonly string[] Numerals = { "", "I", "II", "III", "IV", "V", "VI", "VII" };
}
