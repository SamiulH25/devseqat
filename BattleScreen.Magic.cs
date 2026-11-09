using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen: the look of magic and aura (docs/MAGIC_AURA.md 6). Drawn only: it reads `Battle.Flashes` (moments the sim
/// marks) and the live state (fields, timed effects, the blade, presence), and turns them into motes, ripples, pillars and
/// bolts. Lines are opaque, so a ripple fades by darkening.
partial class BattleScreen
{
    /// A ring that grows (or shrinks) from r0 to r1 over its life.
    struct Ripple { public Vector2 At; public float R0, R1, Born, Life, Y; public Color Col; }
    /// A beam drawn for a moment: a pillar of light (straight up) or a bolt (jagged, from the sky).
    struct Beam { public Vector2 At; public float Born, Life, Radius; public Color Col; public bool Bolt; public int Seed; }

    readonly List<Ripple> ripples = new();
    readonly List<Beam> beams = new();
    /// The aura blade's tip over the last moments, for its trail.
    readonly List<(Vector3 Tip, Vector3 Base, float At)> bladeTrail = new();
    float presencePulse;

    /// Your aura's colour: your order's if you fight with one of its forms, else the plain aura blue.
    Color AuraColour
    {
        get
        {
            foreach (var id in battle.Options.Forms)
                if (Forms.Has(id) && Forms.Get(id).Order is { Length: > 0 } o) return OrderColour(o);
            return Style.Aura;
        }
    }

    static Color OrderColour(string order) => order switch
    {
        "lily" => new Color(232, 236, 250), "rose" => new Color(240, 80, 90), "wolf" => new Color(170, 215, 255),
        "falcon" => new Color(255, 176, 64), "sky" => new Color(150, 232, 255), "lantern" => new Color(255, 214, 120),
        "ashen" => new Color(255, 150, 70), "hollow" => new Color(130, 50, 170), "own" => new Color(245, 210, 110),
        _ => Style.Aura,
    };

    /// The colour of a moment: a tradition's for spells, an order's (or yours) for forms; the foe's own forms run cold.
    Color FlashColour(string what, string tag, int team)
    {
        if (what.StartsWith("form:") || what.StartsWith("land:") || what is "blade" or "soak")
        {
            var c = tag != "" ? OrderColour(tag) : team == 0 ? AuraColour : Style.Aura;
            return team == 0 ? c : Color.Lerp(c, Style.Foe, 0.5f);
        }
        return SpellColor(tag);
    }

    /// A new battle: nothing of the last one's magic stays on screen.
    void ResetMagicFx() { ripples.Clear(); beams.Clear(); bladeTrail.Clear(); motes.Clear(); presencePulse = 0; }

    void AddRipple(Vector2 at, float r0, float r1, float life, Color col, float y = 0.12f) =>
        ripples.Add(new Ripple { At = at, R0 = r0, R1 = r1, Born = battle.Time, Life = life, Col = col, Y = y });

    /// Rising motes over a disc (blessings, healing, black fire): light, slow, drifting up.
    void Rise(Vector2 at, float radius, int n, Color col, float size = 0.09f, float life = 1.4f)
    {
        for (int i = 0; i < n; i++)
        {
            float a = fx.NextSingle() * MathF.Tau, r = MathF.Sqrt(fx.NextSingle()) * radius;
            Spray(at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r, 0.3f + fx.NextSingle() * 1.2f, 1, col, 0.3f, life, size, false, 0, -1.4f);
        }
    }

    /// Each frame: take the sim's moments, then the steady effects (fields, buffs, the blade, presence).
    void MagicFx(float dt)
    {
        foreach (var f in battle.Flashes) FlashFx(f.What, f.At, f.Dir, f.Radius, f.Team, f.Tag);
        battle.Flashes.Clear();
        if (motes.Count > 1200) return; // a crowded sky: the steady effects wait
        FieldFx(dt);
        BuffFx(dt);
        AuraFx(dt);
    }

    void FlashFx(string what, Vector2 at, Vector2 dir, float radius, int team, string tag)
    {
        var col = FlashColour(what, tag, team);
        float near = Vector2.Distance(at, new Vector2(eye.X, eye.Z));
        switch (what)
        {
            case "cast": Rise(at, 0.8f, 8, col, 0.07f, 0.9f); AddRipple(at, 2.2f, 0.4f, 0.6f, col); break;
            case "breakthrough": // the wall breaks in the field (MA7): a white shock, a rising column, a moment held
                AddRipple(at, 0.5f, radius, 1.4f, Color.White, 0.4f);
                AddRipple(at, 0.5f, radius * 0.6f, 1.0f, col);
                Rise(at, 2.5f, 80, col, 0.12f, 1.6f);
                Kick(0.6f);
                if (Settings.Current.HitPause && quality != Quality.Low) hitPause = 0.3f;
                break;
            case "clash": AddRipple(at, radius, radius, 8f, col, 0.08f); AddRipple(at, 0.3f, radius * 1.6f, 0.6f, Color.White, 0.5f); Kick(0.3f); break; // the ring the men give (MA7)
            case "broken": Spray(at, 1.2f, 24, col, 5f, 0.5f, 0.06f, false); AddRipple(at, radius, 0.3f, 0.35f, Color.White); break; // a working shattered (MA5)
            case "spell:attack":
                if (tag == "faith") { beams.Add(new Beam { At = at, Born = battle.Time, Life = 0.7f, Radius = MathF.Max(1, radius), Col = col }); Spray(at, 0.4f, 24, new Color(255, 244, 200), 3f, 0.7f, 0.08f, true); }
                else { Spray(at, 0.3f, 26, new Color(255, 140, 40), 3.2f, 0.9f, 0.1f, true); Spray(at, 0.6f, 10, new Color(60, 52, 46), 0.8f, 2.2f, 0.4f, false, 0.9f); }
                AddRipple(at, 0.5f, radius * 1.4f, 0.5f, col);
                if (near < 60) Kick(0.12f);
                break;
            case "bolt":
                beams.Add(new Beam { At = at, Born = battle.Time, Life = 0.35f, Col = new Color(230, 240, 255), Bolt = true, Seed = fx.Next() });
                Spray(at, 0.5f, 18, new Color(220, 235, 255), 4f, 0.4f, 0.07f, true);
                AddRipple(at, 0.3f, 3f, 0.3f, Color.White);
                if (near < 80) Kick(0.25f);
                break;
            case "spell:heal": Rise(at, radius, 26, new Color(150, 240, 150)); break;
            case "spell:buff": case "spell:rally": Rise(at, radius * 0.8f, 30, col); AddRipple(at, 1, radius, 0.9f, col); break;
            case "spell:debuff":
                for (int i = 0; i < 18; i++) Spray(at + new Vector2(fx.NextSingle() - 0.5f, fx.NextSingle() - 0.5f) * radius, 3.5f, 1, new Color(70, 30, 90), 0.4f, 1.6f, 0.11f, false, 0, 1.5f);
                AddRipple(at, radius, 1, 0.9f, new Color(150, 70, 190));
                break;
            case "spell:field": case "spell:officer": AddRipple(at, 0.5f, MathF.Max(2, radius), 0.8f, col); break;
            case "form:shout":
                for (int i = 0; i < 3; i++) ripples.Add(new Ripple { At = at, R0 = 1, R1 = radius, Born = battle.Time + i * 0.12f, Life = 0.7f, Col = col, Y = 0.15f });
                Spray(at, 0.1f, 14, new Color(176, 160, 124), 2.5f, 1f, 0.25f, false, 0.6f);
                if (team == 0) Kick(0.2f);
                break;
            case "form:burst":
                AddRipple(at, 0.5f, radius * 1.2f, 0.45f, Color.White, 0.6f);
                AddRipple(at, 0.5f, radius, 0.7f, col);
                Spray(at, 1.0f, 50, col, 5f, 0.6f, 0.12f, true);
                Spray(at, 0.1f, 16, new Color(176, 160, 124), 3f, 1.2f, 0.3f, false, 0.7f);
                if (team == 0) Kick(0.35f);
                break;
            case "form:buff": AddRipple(at, 1, radius, 1.1f, col); Rise(at, MathF.Min(radius, 12), 36, col); break;
            case "form:focus": case "form:edge": case "form:counter": case "form:guard":
                for (int i = 0; i < 12; i++) { float a = i * MathF.Tau / 12; Spray(at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 1.6f, 1.1f, 1, col, 0.6f, 0.5f, 0.07f, false); }
                AddRipple(at, 1.6f, 0.3f, 0.4f, col, 1.1f);
                break;
            case "form:drain": AddRipple(at, radius, 0.5f, 0.8f, new Color(150, 40, 60)); Rise(at, 1, 16, new Color(190, 40, 60)); break;
            case "form:burn": Spray(at + dir * 4, 0.3f, 20, new Color(120, 40, 160), 2f, 1.2f, 0.12f, false, 0, -1f); break;
            case "land:leap":
                for (int i = 0; i < 10; i++) Spray(at - dir * (radius * i / 10f), 1.0f, 1, col, 0.4f, 0.6f, 0.08f, false);
                AddRipple(at, 0.5f, 3f, 0.5f, col);
                Spray(at, 0.1f, 14, new Color(176, 160, 124), 2.4f, 1.1f, 0.28f, false, 0.6f);
                if (team == 0) Kick(0.2f);
                break;
            case "land:slash":
            {
                var side = new Vector2(-dir.Y, dir.X);
                for (int i = 0; i <= 14; i++)
                {
                    float s = i / 14f * 2 - 1, reach = radius * (0.75f + 0.25f * (1 - s * s));
                    Spray(at + dir * reach + side * s * radius * 0.45f, 1.0f, 2, col, 1.2f, 0.45f, 0.08f, true);
                }
                break;
            }
            case "land:volley": Spray(at, 0.6f, 30, col, 4f, 0.6f, 0.08f, true); AddRipple(at, 0.5f, radius * 1.3f, 0.5f, col); break;
            case "soak": Spray(at, 1.2f, 7, col, 2.6f, 0.3f, 0.06f, true); AddRipple(at, 0.6f, 1.4f, 0.25f, col, 1.0f); break;
            case "blade": Spray(at, 1.1f, 6, Color.Lerp(col, Color.White, 0.4f), 3f, 0.3f, 0.06f, true); break;
        }
    }

    /// Fields burn, drift or shine: fire and smoke over burning ground, grey banks of mist and smoke, light at a ward's edge.
    void FieldFx(float dt)
    {
        foreach (var f in battle.Fields)
        {
            string id = f.Effect?.Id ?? "";
            // Motes a square metre a second: fire is thick, mist and smoke are few but large and long-lived.
            float rate = id switch { "burning" => 1.2f, "mist" or "smoke" => 0.12f, "warded" => 0.5f, "storm" => 0.6f, "mirage" => 1.2f, _ => 0.15f };
            if (id == "burning" && f.Dps <= 0) rate = 0;
            float want = MathF.Min(600, MathF.PI * f.Radius * f.Radius) * rate * dt;
            int n = (int)want + (fx.NextSingle() < want % 1 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                float a = fx.NextSingle() * MathF.Tau, r = MathF.Sqrt(fx.NextSingle()) * f.Radius;
                var p = f.Pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                switch (id)
                {
                    case "burning":
                        bool black = f.A.Tradition == "necromancy";
                        Spray(p, 0.1f, 1, black ? new Color(110, 40, 150) : fx.Next(3) == 0 ? new Color(255, 220, 110) : new Color(255, 120, 30), 0.4f, 0.9f, 0.3f, false, -0.2f, -1.8f);
                        if (fx.Next(5) == 0) Spray(p, 1.4f, 1, new Color(84, 78, 72), 0.3f, 2f, 0.18f, false, 0.2f, -0.9f);
                        break;
                    case "mist" or "smoke":
                        Spray(p, 0.3f + fx.NextSingle() * 1.2f, 1, id == "mist" ? new Color(196, 206, 204) : new Color(120, 118, 114), 0.1f, 4f, 0.9f, false, 0.35f);
                        break;
                    case "warded":
                        if (r > f.Radius * 0.8f) Spray(p, 0.2f, 1, new Color(255, 246, 210), 0.2f, 1.5f, 0.07f, false, 0, -1.2f);
                        break;
                    case "mirage": // the faint shapes of men that are not there (MA8): a shimmer at head height, never a sprite
                        Spray(p, 0.9f + fx.NextSingle() * 0.8f, 1, new Color(200, 190, 255), 0.05f, 0.7f, 0.06f, false, 0, -0.2f);
                        break;
                    case "storm":
                        Spray(p, 6f, 1, new Color(170, 190, 210), 0.1f, 0.6f, 0.05f, false, 0, 9f);
                        break;
                    default:
                        if (f.Effect?.Harmful == true) Spray(p, 0.3f, 1, new Color(90, 40, 110), 0.2f, 1.2f, 0.09f, false, 0, -0.8f);
                        break;
                }
            }
        }
    }

    /// A company under a blessing glitters now and then; under a curse, dark motes hang over it.
    void BuffFx(float dt)
    {
        foreach (var s in battle.Squads)
        {
            if (s.Alive == 0 || s.Timed.Count == 0 || fx.NextSingle() > dt * 6) continue;
            var t = s.Timed[fx.Next(s.Timed.Count)];
            if (t.Until <= battle.Time) continue;
            var u = s.Units[fx.Next(s.Units.Count)];
            if (!u.Active) continue;
            var col = t.Effect.Harmful ? new Color(110, 50, 140) : new Color(250, 222, 130);
            Spray(u.Pos, 1.8f, 1, col, 0.2f, 1.1f, 0.07f, false, 0, t.Effect.Harmful ? 0.6f : -0.9f);
        }
    }

    /// Your aura: sparks off the lit blade and its trail, a stir of dust round a Master's presence.
    void AuraFx(float dt)
    {
        var p = battle.Player;
        var col = AuraColour;
        float lift = p.Mounted ? 1.2f : 0;
        if (battle.AuraBlade && p.Active)
        {
            var tip = G(p.Pos + p.Dir * (p.Type.Reach + 0.3f), 1.0f + lift);
            var root = G(p.Pos + p.Dir * 0.35f, 1.25f + lift);
            bladeTrail.Add((tip, root, battle.Time));
            for (int i = 0; i < 3; i++) // a haze of sparks along the edge, thicker at a higher tier
                if (fx.NextSingle() < dt * (8 + 6 * battle.AuraTier)) motes.Add(new Mote { Pos = Vector3.Lerp(root, tip, fx.NextSingle()) + new Vector3(fx.NextSingle() - 0.5f, fx.NextSingle() - 0.5f, fx.NextSingle() - 0.5f) * 0.12f, Vel = new Vector3(0, 0.6f, 0), Life = 0.45f, Max = 0.45f, Size = 0.06f, Col = Color.Lerp(col, Color.White, fx.NextSingle() * 0.5f) });
        }
        bladeTrail.RemoveAll(b => battle.Time - b.At > 0.22f || battle.Time < b.At);
        if (battle.AuraTier >= 4 && !battle.Spent && p.Active)
        {
            for (int i = 0; i < 2; i++)
            {
                if (fx.NextSingle() > dt * 12) continue;
                float a = fx.NextSingle() * MathF.Tau, r = 2 + fx.NextSingle() * (Battle.PresenceRadius - 2);
                var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                motes.Add(new Mote { Pos = G(p.Pos + d * r, 0.1f), Vel = new Vector3(d.X, 0.2f, d.Y) * 1.6f, Life = 1f, Max = 1f, Size = 0.07f, Grow = 0.05f, Col = new Color(196, 184, 150) });
            }
            presencePulse += dt;
            if (presencePulse > 2) { presencePulse = 0; AddRipple(p.Pos, 1, Battle.PresenceRadius, 1.4f, Color.Lerp(col, Color.Black, 0.3f)); }
        }
    }

    /// Lines for the magic: ripples, beams, the blade and its trail.
    void MagicLines()
    {
        float now = battle.Time;
        ripples.RemoveAll(r => now - r.Born > r.Life || now < r.Born - 1);
        foreach (var r in ripples)
        {
            if (now < r.Born) continue;
            float k = (now - r.Born) / r.Life, rad = MathHelper.Lerp(r.R0, r.R1, Ease.OutCubic(k));
            var c = Color.Lerp(r.Col, Color.Black, k * 0.8f);
            int seg = Math.Clamp((int)(rad * 4), 10, 48);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * MathF.Tau / seg, a1 = (i + 1) * MathF.Tau / seg;
                Line(G(r.At + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * rad, r.Y), G(r.At + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * rad, r.Y), c);
            }
        }
        beams.RemoveAll(b => now - b.Born > b.Life || now < b.Born);
        foreach (var b in beams)
        {
            float k = (now - b.Born) / b.Life;
            var c = Color.Lerp(b.Col, Color.Black, k * k * 0.7f);
            if (b.Bolt)
            {
                // A jagged bolt from the sky: the same shape every frame of its life, a fork half way down.
                var rnd = new Random(b.Seed);
                var top = G(b.At, 32);
                var prev = top;
                for (int i = 1; i <= 8; i++)
                {
                    float y = 32 * (1 - i / 8f);
                    var next = i == 8 ? G(b.At, 0.2f) : G(b.At + new Vector2(rnd.NextSingle() - 0.5f, rnd.NextSingle() - 0.5f) * 3.5f, y);
                    Line(prev, next, c);
                    Line(prev + Vector3.Right * 0.08f, next + Vector3.Right * 0.08f, c);
                    if (i == 4) Line(next, next + new Vector3(rnd.NextSingle() * 6 - 3, -6, rnd.NextSingle() * 6 - 3), Color.Lerp(c, Color.Black, 0.3f));
                    prev = next;
                }
            }
            else
            {
                // A pillar of light: shafts round its edge from the ground up, bright at the foot and dimming as they rise.
                int n = 16;
                float rad = b.Radius * (1 - 0.4f * k), h = 9 * (1 - 0.3f * k);
                for (int i = 0; i < n; i++)
                {
                    float a = i * MathF.Tau / n + now;
                    var foot = b.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rad * (0.7f + 0.3f * (i % 2));
                    for (int j = 0; j < 4; j++)
                        Line(G(foot, 0.1f + h * j / 4), G(foot, 0.1f + h * (j + 1) / 4), Color.Lerp(Color.Lerp(c, Color.White, 0.3f), Color.Black, 0.18f * j + 0.5f * k));
                }
                Line(G(b.At, 0.1f), G(b.At, h * 1.3f), Color.White);
            }
        }
        // The lit blade: a bright edge and a fading ribbon of where its tip has been.
        var col = AuraColour;
        if (battle.AuraBlade && battle.Player.Active && bladeTrail.Count > 0)
        {
            var (tip, root, _) = bladeTrail[^1];
            float pulse = 0.85f + 0.15f * MathF.Sin(now * 9);
            var edge = Color.Lerp(col, Color.White, 0.35f * pulse);
            Line(root, tip, edge);
            Line(root + Vector3.Up * 0.05f, tip + Vector3.Up * 0.05f, edge);
            Line(root - Vector3.Up * 0.05f, tip - Vector3.Up * 0.05f, Color.Lerp(col, Color.Black, 0.2f));
        }
        for (int i = 1; i < bladeTrail.Count; i++)
        {
            float k = (now - bladeTrail[i].At) / 0.22f;
            var c = Color.Lerp(col, Color.Black, 0.3f + 0.7f * k);
            Line(bladeTrail[i - 1].Tip, bladeTrail[i].Tip, c);
            Line(Vector3.Lerp(bladeTrail[i - 1].Base, bladeTrail[i - 1].Tip, 0.6f), Vector3.Lerp(bladeTrail[i].Base, bladeTrail[i].Tip, 0.6f), Color.Lerp(c, Color.Black, 0.3f));
        }
    }
}
