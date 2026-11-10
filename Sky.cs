using Microsoft.Xna.Framework;

namespace Ironfield;

/// The light of the moment (roadmap C4, D4): sun direction and colour, ambient, sky and fog, set from the hour and the weather.
/// The renderers read these each frame; 13:00 in clear weather gives the original fixed values.
public static class Sky
{
    public static Vector3 SunDir { get; private set; }
    public static Vector3 SunColor { get; private set; }
    public static Vector3 SkyColor { get; private set; }
    public static Vector3 GroundColor { get; private set; }
    public static Color Horizon { get; private set; }
    public static Color Zenith { get; private set; }
    /// Fog distances are multiplied by this (rain, fog and night close the view in).
    public static float FogScale { get; private set; } = 1;
    /// How dark it is (0 = day, 1 = full night): torches and the map's lamplight use it.
    public static float Night { get; private set; }
    public static float Rain { get; private set; }
    /// How strong cloud shadows are over the land (thin on a clear day, none under full overcast or at night), how wet the
    /// ground is, and snow cover (docs/VISUALS.md V4).
    public static float Clouds { get; private set; }
    public static float Wet { get; private set; }
    public static float Snow { get; private set; }
    /// The colour of the moment for the final grade: gold at dawn and dusk, cool in rain, blue at night.
    public static Vector3 Grade { get; private set; } = Vector3.One;

    static Sky() => Set(Weather.Clear);

    static readonly Vector3 NoonSun = new(0.9f, 0.84f, 0.72f), DuskSun = new(0.95f, 0.6f, 0.36f), MoonSun = new(0.26f, 0.3f, 0.44f);
    static readonly Vector3 NoonSky = new(0.42f, 0.47f, 0.56f), DuskSky = new(0.4f, 0.34f, 0.38f), NightSky = new(0.13f, 0.15f, 0.24f);
    static readonly Color NoonHorizon = new(186, 204, 218), DuskHorizon = new(226, 168, 128), NightHorizon = new(30, 36, 56);
    static readonly Color NoonZenith = new(78, 126, 194), DuskZenith = new(70, 84, 140), NightZenith = new(10, 14, 32);

    /// `nightFloor` keeps the darkest night this bright (the campaign map stays readable at night).
    public static void Set(Weather w, float nightFloor = 0)
    {
        float h = w.Hour;
        float day = MathF.Sin(MathF.PI * (h - 6) / 12);                        // 1 at noon, 0 at 6:00 and 18:00, negative at night
        float elev = MathHelper.ToRadians(53) * day / MathF.Sin(MathF.PI * 7 / 12);
        float az = MathHelper.ToRadians(38 + (h - 13) * 15);
        float k = Math.Clamp(elev / MathHelper.ToRadians(25), 0, 1);           // low sun -> high sun
        float n = Math.Clamp(-elev / MathHelper.ToRadians(8), 0, 1);           // dusk -> night
        n = MathF.Min(n, 1 - nightFloor);
        Night = n;
        Rain = w.Rain;
        float shown = elev > 0 ? elev : MathHelper.ToRadians(40);               // at night the moon lights from high up
        float showAz = elev > 0 ? az : az + MathF.PI;
        var pos = new Vector3(MathF.Cos(showAz) * MathF.Cos(shown), MathF.Sin(shown), MathF.Sin(showAz) * MathF.Cos(shown));
        SunDir = -Vector3.Normalize(pos);

        var sun = Vector3.Lerp(Vector3.Lerp(DuskSun, NoonSun, k), MoonSun, n);
        var sky = Vector3.Lerp(Vector3.Lerp(DuskSky, NoonSky, k), NightSky, n);
        var hor = Color.Lerp(Color.Lerp(DuskHorizon, NoonHorizon, k), NightHorizon, n);
        var zen = Color.Lerp(Color.Lerp(DuskZenith, NoonZenith, k), NightZenith, n);
        // Rain and fog grey everything out and dim the sun.
        float grey = Math.Clamp(w.Rain * 0.8f + w.Fog * 0.6f, 0, 0.9f);
        static Vector3 Grey(Vector3 c) { float l = (c.X + c.Y + c.Z) / 3; return new Vector3(l); }
        sun = Vector3.Lerp(sun, Grey(sun) * 0.55f, grey);
        sky = Vector3.Lerp(sky, Grey(sky) * 1.05f, grey * 0.8f);
        hor = Color.Lerp(hor, new Color(Grey(hor.ToVector3()) * 0.95f), grey);
        zen = Color.Lerp(zen, new Color(Grey(zen.ToVector3()) * 1.1f + new Vector3(0.05f)), grey);
        SunColor = sun;
        SkyColor = sky;
        GroundColor = Vector3.Lerp(new Vector3(0.3f, 0.29f, 0.24f), new Vector3(0.08f, 0.08f, 0.1f), n);
        Horizon = hor;
        Zenith = zen;
        FogScale = Math.Clamp(1 - w.Fog * 0.7f - w.Rain * 0.3f - n * 0.35f, 0.2f, 1);
        Clouds = (1 - n) * Math.Clamp(0.32f + w.Rain * 0.2f - w.Fog * 0.3f, 0, 0.45f) * (grey > 0.7f ? 0.3f : 1);
        Wet = w.Snow ? 0.15f : Math.Clamp(w.Rain, 0, 1);
        Snow = w.Snow ? 0.8f : 0;
        var gold = new Vector3(1.06f, 0.98f, 0.86f);
        var cool = new Vector3(0.94f, 0.98f, 1.05f);
        Grade = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(gold, Vector3.One, k), cool, Math.Clamp(w.Rain + w.Fog * 0.5f, 0, 1)), new Vector3(0.9f, 0.95f, 1.1f), n);
        baseSun = SunColor; baseSky = SkyColor; baseGround = GroundColor; baseHorizon = Horizon; baseZenith = Zenith; baseGrade = Grade;
        OmenDark = 0;
    }

    static Vector3 baseSun, baseSky, baseGround, baseGrade = Vector3.One;
    static Color baseHorizon, baseZenith;
    /// How far an omen has darkened the sky now (0 = none).
    public static float OmenDark { get; private set; }

    /// An omen over the light (docs/SPECTACLE.md S1): `dark` 0..1 dims the sun, sky and horizon (a great working gathering),
    /// `bright` 0..1 pulls them toward `tint` instead (a miracle's dawn). Laid over the values `Set` gave, so calling it
    /// with zeros puts the plain light back. Drawing only.
    public static void Omen(float dark, Vector3 tint, float bright = 0)
    {
        OmenDark = dark;
        float d = Math.Clamp(dark, 0, 1), b = Math.Clamp(bright, 0, 1);
        SunColor = Vector3.Lerp(baseSun * (1 - d * 0.75f), tint * 1.1f, b);
        SkyColor = Vector3.Lerp(baseSky * (1 - d * 0.5f), Vector3.Lerp(baseSky, tint, 0.5f), b);
        GroundColor = baseGround * (1 - d * 0.4f);
        Horizon = Color.Lerp(Color.Lerp(baseHorizon, new Color(baseHorizon.ToVector3() * 0.45f), d), new Color(tint), b * 0.5f);
        Zenith = Color.Lerp(baseZenith, new Color(baseZenith.ToVector3() * 0.35f), d);
        Grade = Vector3.Lerp(Vector3.Lerp(baseGrade, baseGrade * new Vector3(0.92f, 0.94f, 1.04f), d), baseGrade * Vector3.Lerp(Vector3.One, tint, 0.25f), b);
    }
}
