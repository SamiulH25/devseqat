using Microsoft.Xna.Framework;

namespace Ironfield;

/// The world's look (docs/WORLD_LIFE.md WL0): laid over what `Sky` gives for the hour and weather. `Sun` and `Ambient`
/// scale the light, `ShadowTint` colours the ambient (what shadows are lit by); `Palette` repaints the land, trees and
/// houses (not the men: their pixel palettes stay); `Grade` is the final grade. Battles keep the "muted chronicle";
/// the campaign map takes the grounded look (natural light, cool shadows, blue haze), chosen by `Grounded`, which
/// `MapScreen` sets each frame before `Sky.Set` and `BattleScreen` clears. Drawing only.
public static class Looks
{
    /// True while the campaign map draws: the grounded, natural look.
    public static bool Grounded;

    public static float Sun => Grounded ? 1.0f : 0.95f;
    public static float Ambient => Grounded ? 0.95f : 1.05f;
    public static Vector3 ShadowTint => Grounded ? new(0.93f, 0.98f, 1.08f) : new(1.0f, 0.97f, 0.93f);

    /// The world shader's palette uniform: x = saturation, y = lightness, z = warmth of the land's own colours.
    public static Vector4 Palette => Grounded ? new(0.78f, 0.97f, 0.1f, 0) : new(0.7f, 0.95f, 0.35f, 0);

    /// The post pass's grade: saturation, contrast, vignette (FXAA is added by the caller).
    public static Vector3 Grade => Grounded ? new(1.0f, 1.06f, 0.2f) : new(0.85f, 1.1f, 0.32f);
}
