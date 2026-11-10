using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen: each strong working lands in its own way (docs/archive/SPECTACLE.md S3). The sim marks which one fired with a
/// `work:<id>` flash (its Dir points back to the caster); this turns it into a lasting moment: smite's shaft from the sky,
/// a miracle's dawn and petals, a tempest turning the clouds, wildfire's front and its smoke, pots of Greek fire in the
/// air, a flock of crows, a mist rolling out, a dread that drains the colour from the cursed, a ward's dome. Drawing only.
partial class BattleScreen
{
    /// A working that lingers on screen after it lands (battle seconds, so a pause holds it).
    struct Working { public string Id; public Vector2 At, From; public float Born, Life, Radius; public int Team, Seed; }
    readonly List<Working> workings = new();
    /// Fields seen last frame, so one that ends can go out with a flourish (a mirage breaking into sparkles).
    readonly HashSet<Field> fieldsSeen = new();
    float nextSkyBolt, swirlWant, swirlNow;

    void ResetWorkings() { workings.Clear(); fieldsSeen.Clear(); nextSkyBolt = 0; swirlWant = swirlNow = 0; Sky.Swirl = Vector2.Zero; }

    float Age(in Working w) => (battle.Time - w.Born) / w.Life;

    void Linger(string id, Vector2 at, Vector2 from, float radius, int team, float life) =>
        workings.Add(new Working { Id = id, At = at, From = from, Born = battle.Time, Life = life, Radius = radius, Team = team, Seed = fx.Next() });

    /// A working has just landed.
    void WorkFx(string id, Vector2 at, Vector2 from, float radius, int team)
    {
        float near = Vector2.Distance(at, new Vector2(eye.X, eye.Z));
        switch (id)
        {
            case "smite": Linger(id, at, from, radius, team, 1.3f); Flash(near < 80 ? 0.35f : 0.15f); break;
            case "miracle":
                Linger(id, at, from, radius, team, 6.5f);
                beams.Add(new Beam { At = at, Born = battle.Time, Life = 1.6f, Radius = MathF.Max(2, radius * 0.35f), Col = new Color(255, 236, 170) });
                for (int i = 0; i < 4; i++) AddScar(at + Jitter(radius * 0.6f), 2 + fx.NextSingle() * 2, ScarKind.Bloom);
                AddAwe(at, radius * 1.6f, 2.6f);
                Flash(0.25f);
                break;
            case "tempest": case "storm": Linger(id, at, from, radius, team, id == "tempest" ? 2.5f : 1.5f); AddRipple(at, 0.5f, radius, 1.2f, new Color(170, 200, 230), 3); break;
            case "wildfire":
                Linger(id, at, from, radius, team, 2.4f);
                for (int i = 0; i < 6; i++) AddScar(at + Jitter(radius * 0.8f), 2.5f + fx.NextSingle() * 2.5f, ScarKind.Scorch);
                AddAwe(at, radius * 1.4f, 1.6f);
                if (near < 70) Kick(0.15f);
                break;
            case "greek_fire":
                Spray(at, 0.5f, 70, new Color(255, 150, 40), 6f, 1.1f, 0.14f, true);
                Spray(at, 1.0f, 30, new Color(255, 230, 140), 3f, 0.6f, 0.1f, true);
                Spray(at, 1.5f, 18, new Color(40, 34, 30), 1.2f, 3.5f, 0.6f, false, 1.2f, -1.4f);
                for (int i = 0; i < 5; i++) AddScar(at + Jitter(radius * 0.7f), 2.5f + fx.NextSingle() * 2, ScarKind.Scorch);
                AddRipple(at, 0.5f, radius * 1.6f, 0.7f, new Color(255, 190, 90), 0.4f);
                AddAwe(at, radius * 2.5f, 1.8f);
                Flash(near < 100 ? 0.4f : 0.2f);
                if (near < 90) Kick(0.3f);
                break;
            case "crows": Linger(id, at, from, radius, team, 7f); break;
            case "mist": case "smoke": Linger(id, at, from, radius, team, 2.6f); break;
            case "dread": case "dread_drums": Linger(id, at, from, radius, team, 2.4f); AddAwe(at, radius + 6, 2f, 1 - team); break;
            case "ward": Linger(id, at, from, radius, team, 1.2f); AddRipple(at, 0.5f, radius, 0.6f, new Color(255, 246, 210), 1.5f); break;
            case "mirage": Rise(at, radius * 0.8f, 30, new Color(210, 200, 255), 0.06f, 1f); break;
        }
    }

    Vector2 Jitter(float r)
    {
        float a = fx.NextSingle() * MathF.Tau, d = MathF.Sqrt(fx.NextSingle()) * r;
        return new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
    }

    /// Each frame: what the lingering workings ask of the light, the sky and the motes; storms overhead; fields that end.
    void WorkingsFx(float dt)
    {
        workings.RemoveAll(w => Age(w) >= 1 || battle.Time < w.Born);
        float near(Vector2 p) => Vector2.Distance(p, new Vector2(eye.X, eye.Z));
        foreach (var w in workings)
        {
            float k = Age(w);
            switch (w.Id)
            {
                case "smite": Brighten(0.35f * (1 - k), new Vector3(1.1f, 1.05f, 0.9f)); break;
                case "miracle": // a dawn over the field, and petals of light drifting down on it
                    Brighten(0.85f * MathF.Min(1, k * 6) * (1 - Ease.In(k)), new Vector3(1.3f, 0.92f, 0.6f));
                    if (fx.NextSingle() < dt * 40 * (1 - k)) Spray(w.At + Jitter(w.Radius), 7 + fx.NextSingle() * 5, 1, fx.Next(3) == 0 ? Color.White : new Color(255, 226, 150), 0.6f, 4f, 0.08f, false, 0, 1.3f);
                    break;
                case "wildfire": // the front runs out from where it caught
                {
                    float r = w.Radius * Ease.Out(k);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = fx.NextSingle() * MathF.Tau;
                        Spray(w.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r, 0.2f, 1, fx.Next(2) == 0 ? new Color(255, 200, 90) : new Color(255, 110, 30), 0.8f, 0.8f, 0.3f, false, -0.2f, -2.4f);
                    }
                    break;
                }
                case "mist": case "smoke": // a tide rolling outward from the spot
                {
                    float r = w.Radius * Ease.Out(k);
                    var col = w.Id == "mist" ? new Color(200, 210, 208) : new Color(120, 118, 114);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = fx.NextSingle() * MathF.Tau;
                        var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                        motes.Add(new Mote { Pos = G(w.At + d * r, 0.4f + fx.NextSingle()), Vel = new Vector3(d.X, 0, d.Y) * 3, Life = 2.5f, Max = 2.5f, Size = 0.8f, Grow = 0.4f, Col = col });
                    }
                    break;
                }
                case "dread": case "dread_drums": // the colour goes out of the men it falls on
                    Drain(0.75f * Ease.Kick(k), w.At, -0.14f);
                    if (fx.NextSingle() < dt * 4) AddRipple(w.At, w.Radius, 0.5f, 0.8f, new Color(90, 40, 110));
                    break;
                case "tempest": Omen(0.3f * (1 - k)); break;
            }
        }
        foreach (var f in battle.Fields) FieldLook(f, dt, near(f.Pos));
        // A field that has just ended: a mirage breaks into sparkles, a ward's dome falls in light.
        foreach (var f in fieldsSeen)
            if (!battle.Fields.Contains(f))
                switch (f.Effect?.Id)
                {
                    case "mirage": Rise(f.Pos, f.Radius, 50, new Color(220, 210, 255), 0.07f, 1.1f); Spray(f.Pos, 1.2f, 20, Color.White, 3f, 0.5f, 0.05f, false); break;
                    case "warded": Rise(f.Pos, f.Radius, 24, new Color(255, 246, 210), 0.06f, 0.8f); break;
                }
        fieldsSeen.Clear();
        foreach (var f in battle.Fields) fieldsSeen.Add(f);
        StormSky(dt);
    }

    /// The steady look of a field beyond its motes: a great storm turns the sky and drives the rain sideways and throws its
    /// own lightning; big fires raise a column of smoke and the air wavers over them.
    void FieldLook(Field f, float dt, float near)
    {
        switch (f.Effect?.Id)
        {
            case "storm":
                bool great = f.A.Great;
                swirlWant = MathF.Max(swirlWant, great ? 1 : 0.35f);
                if (great) Omen(0.45f);
                if (great) calmWant = MathF.Min(calmWant, -1); // the wind howls
                int n = (int)(MathF.Min(400, f.Radius * f.Radius) * dt * (great ? 1.5f : 0.5f));
                for (int i = 0; i < n; i++) // the rain driven sideways
                    motes.Add(new Mote { Pos = G(f.Pos + Jitter(f.Radius), 5 + fx.NextSingle() * 3), Vel = new Vector3(5f, -9f, 2f), Life = 0.6f, Max = 0.6f, Size = 0.04f, Col = new Color(176, 196, 214) });
                if (great && fx.NextSingle() < dt * 0.9f) SkyBolt(f.Pos + Jitter(f.Radius * 1.3f), near < 120 ? 0.35f : 0.15f);
                break;
            case "burning" when f.Dps > 0 && f.A.Tradition != "necromancy":
                if (near < 90) Shimmer(f.Radius > 6 ? 0.9f : 0.45f);
                if (f.Radius > 6 && fx.NextSingle() < dt * 4) // the tall column of smoke the whole field sees
                    Spray(f.Pos + Jitter(f.Radius * 0.3f), 2.5f, 1, new Color(70, 66, 62), 0.3f, 7f, 0.4f, false, 0.45f, -3f);
                break;
        }
    }

    /// Lightning that is only seen and heard: a storm's or heavy rain's, never touching a man.
    void SkyBolt(Vector2 at, float flash)
    {
        if (quality == Quality.Low) return;
        beams.Add(new Beam { At = at, Born = battle.Time, Life = 0.3f, Col = new Color(220, 232, 255), Bolt = true, Seed = fx.Next() });
        Flash(flash);
        PlayCue(Cue.Thunder, at, 500);
    }

    /// Heavy rain (S6): far lightning now and then, the wind coming in gusts.
    void StormSky(float dt)
    {
        var w = battle.Options.Weather;
        if (w.Rain >= 0.6f && !w.Snow)
        {
            if (calmWant <= 0) calmWant = MathF.Min(calmWant, -MathF.Max(0, MathF.Sin(realTime * 0.4f)) * 0.8f);
            if (battle.Time > nextSkyBolt)
            {
                if (nextSkyBolt > 0)
                {
                    var c = new Vector2(eye.X, eye.Z);
                    float a = fx.NextSingle() * MathF.Tau;
                    SkyBolt(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (90 + fx.NextSingle() * 120), 0.2f);
                }
                nextSkyBolt = battle.Time + 9 + fx.NextSingle() * 12;
            }
        }
        swirlNow += (swirlWant - swirlNow) * MathF.Min(1, dt * 0.8f);
        swirlWant = 0;
        Sky.Swirl = quality == Quality.Low ? Vector2.Zero : new Vector2(swirlNow * 0.6f, swirlNow * 0.8f);
    }

    /// Lines of the workings: smite's shaft, the crows, a ward's dome, and crows over a company they hound.
    void WorkingLines()
    {
        float t = battle.Time;
        foreach (var w in workings)
        {
            float k = Age(w);
            switch (w.Id)
            {
                case "smite": // a shaft falls from the sky in a tenth of a second, then burns out
                {
                    float down = MathF.Min(1, (t - w.Born) / 0.12f), top = 48, bottom = top * (1 - down);
                    var c = Color.Lerp(Color.White, new Color(255, 214, 120), k);
                    c = Color.Lerp(c, Fade, k * k * 0.8f);
                    float r = MathF.Max(0.6f, w.Radius * 0.22f) * (1 - 0.5f * k);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * MathF.Tau / 6 + t * 2;
                        var p = w.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                        Line(G(p, bottom + 0.1f), G(p, top), c);
                    }
                    Line(G(w.At, bottom + 0.1f), G(w.At, top), Color.White);
                    break;
                }
                case "crows": Flock(w, k); break;
                case "ward": WardDome(w.At, w.Radius, 1 - k); break;
            }
        }
        foreach (var f in battle.Fields) if (f.Effect?.Id == "warded") WardDome(f.Pos, f.Radius, 0);
        foreach (var s in battle.Squads) // a few crows circle over a company they hound
            if (s.Alive > 0 && s.Timed.Any(x => x.Effect.Id == "crows" && x.Until > t))
                for (int i = 0; i < (quality == Quality.High ? 7 : 4); i++)
                {
                    float a = t * (0.9f + i * 0.07f) + i * 0.9f;
                    var p = s.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (3 + i % 3);
                    Bird(G(p, 6 + (i % 3) * 0.8f), new Vector3(-MathF.Sin(a), 0, MathF.Cos(a)), t * 9 + i);
                }
    }

    /// A great flock: it gathers high, wheels round the spot, funnels down and dives into the men.
    void Flock(in Working w, float k)
    {
        int n = quality == Quality.High ? 48 : quality == Quality.Medium ? 24 : 0;
        float t = battle.Time;
        for (int i = 0; i < n; i++)
        {
            float h1 = Hash(w.Seed, i, 1), h2 = Hash(w.Seed, i, 2), h3 = Hash(w.Seed, i, 3);
            float start = h3 * 0.15f; // they do not all come at once
            if (k < start) continue;
            float kk = (k - start) / (1 - start);
            float funnel = Ease.InOut(Math.Clamp((kk - 0.55f) / 0.3f, 0, 1)), dive = Math.Clamp((kk - 0.85f) / 0.15f, 0, 1);
            float r = w.Radius * (0.5f + h1) * (1 - 0.8f * funnel) * (1 - dive);
            float a = h2 * MathF.Tau + t * (1.2f + h1 * 0.6f) * (1 + funnel);
            float y = MathHelper.Lerp(16 + h1 * 6, 4, funnel) * (1 - dive) + 0.5f;
            var p = w.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            var fwd = new Vector3(-MathF.Sin(a), -dive * 2, MathF.Cos(a));
            if (kk < 0.08f) y += (0.08f - kk) * 200; // they arrive out of the sky
            Bird(G(p, y), fwd, t * 10 + i);
        }
    }

    static float Hash(int seed, int i, int salt) => (uint)HashCode.Combine(seed, i, salt) / (float)uint.MaxValue;

    /// A dark bird: two strokes in a V, the wings beating.
    void Bird(Vector3 at, Vector3 fwd, float phase)
    {
        if (fwd.LengthSquared() < 1e-4f) return;
        fwd.Normalize();
        var side = Vector3.Normalize(Vector3.Cross(fwd, Vector3.Up) + new Vector3(1e-4f, 0, 0));
        float flap = MathF.Sin(phase) * 0.25f;
        var col = new Color(26, 22, 28);
        Line(at, at - fwd * 0.25f + side * 0.45f + Vector3.Up * flap, col);
        Line(at, at - fwd * 0.25f - side * 0.45f + Vector3.Up * flap, col);
    }

    /// A ward's dome: faint lines of light over the ground it guards, bright as it rises.
    void WardDome(Vector2 at, float r, float flare)
    {
        var col = Color.Lerp(new Color(120, 112, 84), new Color(255, 246, 210), flare);
        float rise = flare > 0 ? 1 - flare * flare : 1, h = r * 0.55f * rise;
        const int meridians = 8, steps = 6;
        for (int m = 0; m < meridians; m++)
        {
            float a = m * MathF.Tau / meridians + battle.Time * 0.1f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var prev = G(at + d * r, 0.1f);
            for (int s = 1; s <= steps; s++)
            {
                float th = s / (float)steps * MathHelper.PiOver2;
                var next = G(at + d * r * MathF.Cos(th), 0.1f + h * MathF.Sin(th));
                Line(prev, next, col);
                prev = next;
            }
        }
    }

    /// Pots of fire in the air as a fire working gathers: they leave the caster and land as it fires.
    void PotArcs(Cast c, float g)
    {
        if (c.A.Id is not ("greek_fire" or "fire_pots") || g < 0.6f) return;
        var from = CasterOf(c);
        int pots = c.A.Great ? 5 : 3;
        for (int j = 0; j < pots; j++)
        {
            float u = (g - 0.6f) / 0.4f - j * 0.04f;
            if (u <= 0) continue;
            var to = c.At + new Vector2(MathF.Cos(j * 2.4f), MathF.Sin(j * 2.4f)) * c.A.Radius * 0.5f * (j % 2 == 0 ? 0.4f : 1);
            float peak = Vector2.Distance(from, to) * 0.35f + 3;
            Vector3 At(float s) => Vector3.Lerp(G(from, 1.6f), G(to, 0.2f), s) + Vector3.Up * (peak * 4 * s * (1 - s));
            var col = new Color(255, 150 + j * 15, 50);
            float s0 = MathF.Max(0, u - 0.15f);
            for (int i = 0; i < 3; i++) Line(At(MathHelper.Lerp(s0, MathF.Min(1, u), i / 3f)), At(MathHelper.Lerp(s0, MathF.Min(1, u), (i + 1) / 3f)), Color.Lerp(col, Color.Black, 0.5f - i * 0.2f));
        }
    }
}
