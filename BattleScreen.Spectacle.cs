using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen: the spectacle pass's groundwork (docs/SPECTACLE.md S1). The battle's one cinematic beat (`Beats`), moving
/// lights, the omen over the sky, the post pass's colour drain, shimmer and flash, scars that last the battle, and men who
/// react to a working. Drawing only: nothing here touches the battle, and the slowed view replays exactly (each tick's step
/// is recorded).
partial class BattleScreen
{
    readonly Beats beats = new();

    /// A light this frame: place, colour, strength (negative takes light) and reach in metres.
    readonly List<(Vector3 At, Color Col, float Strength, float Reach)> glows = new();

    /// A source of awe: men within reach lean back from it while it lasts (real seconds).
    struct AweSource { public Vector2 At; public float Radius, Born, Life; }
    readonly List<AweSource> awes = new();

    /// What the light, the post pass and the omen are asked for this frame (the largest asker wins), and their eased values.
    float omenWant, omenNow, brightWant, brightNow, drainWant, drainNow, shimmerWant, shimmerNow, flash;
    Vector3 brightTint = Vector3.One;
    Vector2 drainAt;

    enum ScarKind { Glass, Scorch, Frost, Bloom, Crack }
    struct Scar { public Vector2 Pos; public float Size, Rot, Born; public ScarKind Kind; }
    readonly Scar[] scars = new Scar[64];
    int scarNext, scarCount;

    /// A new battle: the beat unspent, no scars, the plain sky.
    void ResetSpectacle()
    {
        beats.Reset(); glows.Clear(); awes.Clear(); puppet.Awe.Clear();
        scarNext = scarCount = 0; gathering.Clear(); calm = calmWant = 0;
        omenWant = omenNow = brightWant = brightNow = drainWant = drainNow = shimmerWant = shimmerNow = flash = 0;
    }

    /// Ask for the battle's cinematic beat (rule 6). The field camera turns toward it a little; off with the Cinematics setting.
    bool Beat(BeatKind kind, Vector2 at, float length = 2.6f) =>
        Settings.Current.Cinematics && quality != Quality.Low && shotDir == null && beats.Ask(kind, at, realTime, length);

    /// How far into a beat the view is now (0..1).
    float BeatAmount => beats.Amount(realTime);

    /// The view's time scale from the beat (1 = none).
    float BeatScale => beats.Scale(realTime);

    void AddGlow(Vector2 at, float height, Color col, float strength, float reach) =>
        glows.Add((new Vector3(at.X, battle.Terrain.Height(at) + height, at.Y), col, strength, reach));

    void AddAwe(Vector2 at, float radius, float life) => awes.Add(new AweSource { At = at, Radius = radius, Born = realTime, Life = life });

    /// A mark that lasts the whole battle (rule 4): not evicted by blood and hoofprints.
    void AddScar(Vector2 at, float size, ScarKind kind)
    {
        if (quality == Quality.Low) return;
        scars[scarNext] = new Scar { Pos = at, Size = size, Rot = fx.NextSingle() * MathHelper.TwoPi, Born = battle.Time, Kind = kind };
        scarNext = (scarNext + 1) % scars.Length;
        scarCount = Math.Min(scars.Length, scarCount + 1);
        if (kind is ScarKind.Scorch or ScarKind.Crack) Stamp(at, 160); // the grass burnt or broken flat
    }

    /// Each frame (real time): ease the omen, drain and shimmer toward what was asked, decay the flash, hand the awe to the
    /// puppets. Askers call `Omen`, `Drain`, `Shimmer` before this runs (from the magic's frame).
    void SpectacleFrame(float dt)
    {
        float since = realTime - breakAt;
        if (since < 3.2f && battle.Player.Active) Drain(since < 1.2f ? 1 : 1 - (since - 1.2f) / 2f, battle.Player.Pos); // the world holds its breath, then floods back
        float k = MathF.Min(1, dt * 2.5f);
        omenNow += (omenWant - omenNow) * k;
        brightNow += (brightWant - brightNow) * k;
        drainNow += (drainWant - drainNow) * MathF.Min(1, dt * 5);
        shimmerNow += (shimmerWant - shimmerNow) * k;
        omenWant = brightWant = drainWant = shimmerWant = 0;
        flash = MathF.Max(0, flash - dt * 6);
        calm += (calmWant - calm) * MathF.Min(1, dt * 1.5f);
        if (shading != null) shading.Calm = calm;
        if (quality == Quality.Low) omenNow = brightNow = 0;
        Sky.Omen(omenNow, brightTint, brightNow);

        puppet.Awe.Clear();
        for (int i = awes.Count - 1; i >= 0; i--)
        {
            float t = (realTime - awes[i].Born) / awes[i].Life;
            if (t >= 1) { awes.RemoveAt(i); continue; }
            puppet.Awe.Add((awes[i].At, awes[i].Radius, Ease.Kick(t)));
        }
    }

    /// Ask the sky to darken this frame (0..1).
    void Omen(float dark) => omenWant = MathF.Max(omenWant, dark);
    /// Ask the light to turn toward `tint` this frame (a miracle's dawn).
    void Brighten(float amount, Vector3 tint) { if (amount > brightWant) { brightWant = amount; brightTint = tint; } }
    /// Ask the colour to drain from everything but a circle round `at` this frame.
    void Drain(float amount, Vector2 at) { if (amount > drainWant) { drainWant = amount; drainAt = at; } }
    void Shimmer(float amount) => shimmerWant = MathF.Max(shimmerWant, amount);
    void Flash(float amount) { if (quality != Quality.Low) flash = MathF.Max(flash, amount); }

    /// Hand the strongest lights to the shaders: four on High, two on Medium, none on Low. A light counts for less the
    /// further it is from the eye, so the ones you can see win.
    void SetGlows(Shading sh)
    {
        int n = quality == Quality.High ? 4 : quality == Quality.Medium ? 2 : 0;
        Array.Clear(sh.Glow); Array.Clear(sh.GlowCol);
        LightsFx();
        if (n == 0 || glows.Count == 0) { glows.Clear(); return; }
        glows.Sort((a, b) => Weight(b).CompareTo(Weight(a)));
        for (int i = 0; i < Math.Min(n, glows.Count); i++)
        {
            var g = glows[i];
            sh.Glow[i] = new Vector4(g.At, g.Strength);
            sh.GlowCol[i] = new Vector4(g.Col.ToVector3(), g.Reach);
        }
        glows.Clear();
    }

    float Weight((Vector3 At, Color Col, float Strength, float Reach) g) => MathF.Abs(g.Strength) * g.Reach / (1 + Vector3.Distance(g.At, eye) / 60);

    /// The post pass's moment: the beat's bars, the colour drain round a point on screen, the shimmer over fire, a flash.
    (Vector4 Awe, Vector4 Moment) PostMoment()
    {
        var awe = Vector4.Zero;
        if (drainNow > 0.01f)
        {
            var vp = GraphicsDevice.Viewport;
            var p = vp.Project(new Vector3(drainAt.X, battle.Terrain.Height(drainAt) + 1, drainAt.Y), proj, view, Matrix.Identity);
            awe = p.Z is > 0 and < 1
                ? new Vector4(p.X / vp.Width, p.Y / vp.Height, 0.16f, drainNow)
                : new Vector4(0.5f, 0.5f, 0, drainNow); // behind the camera: all of it drains
        }
        return (awe, new Vector4(BeatAmount * 0.085f, shimmerNow, flash * 0.55f, 0.7f));
    }

    static Color ScarColour(ScarKind k) => k switch
    {
        ScarKind.Glass => new Color(214, 222, 226),
        ScarKind.Scorch => new Color(34, 28, 24),
        ScarKind.Frost => new Color(212, 232, 250),
        ScarKind.Bloom => new Color(126, 176, 84),
        _ => new Color(58, 48, 40),
    };

    void DrawScars()
    {
        var t = battle.Terrain;
        for (int i = 0; i < scarCount; i++)
        {
            ref var s = ref scars[i];
            var col = ScarColour(s.Kind);
            if (s.Kind == ScarKind.Scorch) // embers glow in it for a while after
                col = Color.Lerp(new Color(150, 60, 20), col, Math.Clamp((battle.Time - s.Born) / 20f, 0, 1));
            puppet.Decal(s.Pos, t.Height(s.Pos), s.Size, s.Rot, col, "splat");
            if (s.Kind is ScarKind.Glass or ScarKind.Crack) puppet.Decal(s.Pos, t.Height(s.Pos) + 0.005f, s.Size * 0.55f, -s.Rot, Color.Lerp(col, Color.White, 0.3f), "splat");
        }
    }

    /// When the wall broke in the field (real seconds), for the colour drain that follows it.
    float breakAt = -10;

    /// The lights of the magic this frame (S1.2), picked as the scene is drawn: beams and bolts flare and fade, fire
    /// flickers, a Transcendent captain shines, a lit blade glows a little at a high tier.
    void LightsFx()
    {
        glows.Clear(); // asked anew each frame
        GatherLights();
        float t = battle.Time;
        foreach (var b in beams)
        {
            float k = 1 - (t - b.Born) / b.Life;
            if (k > 0) AddGlow(b.At, b.Bolt ? 6 : 3, b.Col, (b.Bolt ? 2.4f : 1.6f) * k, b.Bolt ? 14 : 5 + b.Radius);
        }
        foreach (var f in battle.Fields)
            if (f.Effect?.Id == "burning" && f.Dps > 0)
            {
                float flick = 0.85f + 0.15f * MathF.Sin(realTime * 11 + f.Pos.X) * MathF.Sin(realTime * 6.3f + f.Pos.Y);
                bool black = f.A.Tradition == "necromancy";
                AddGlow(f.Pos, 1.2f, black ? new Color(120, 60, 170) : new Color(255, 140, 60), (black ? -0.6f : 0.9f) * flick, f.Radius * 0.8f);
            }
        var p = battle.Player;
        if (p.Active && battle.AuraTier >= 5 && !battle.Spent) AddGlow(p.Pos, 2.2f, AuraColour, 0.8f, 6);
        else if (p.Active && battle.AuraBlade && battle.AuraTier >= 3) AddGlow(p.Pos + p.Dir, 1.2f, AuraColour, 0.5f * (battle.AuraTier - 2), 3);
    }
}
