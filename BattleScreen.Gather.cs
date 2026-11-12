using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen: the gathering (docs/archive/SPECTACLE.md S2). Every cast with a warning gathers before it lands: a circle of its
/// tradition's marks draws itself round the spot and spins up at the end (the warning stays honest: its radius is the
/// working's), motes and dust are pulled in to the caster and a column of light rises over him. A great working also
/// darkens the sky, stills the wind, swells a low tone and asks for the battle's beat so it lands in slow time.
partial class BattleScreen
{
    /// Casts whose gathering has begun on screen (for the one-off sound).
    readonly HashSet<Cast> gathering = new();

    /// How far a cast has gathered: 0 when it starts, 1 as it lands.
    float Gathered(Cast c) => Math.Clamp((battle.Time - c.Started) / MathF.Max(0.1f, c.FireAt - c.Started), 0, 1);

    /// Where the working is gathered: its speaker, or the middle of the casting company.
    Vector2 CasterOf(Cast c) =>
        (c.Hero ? c.By.Units.FirstOrDefault(u => u.Hero && u.Active) : c.By.Units.FirstOrDefault(u => u.Active && u.Type.Class == UnitClass.Caster))?.Pos ?? c.By.Center;

    /// Each frame: the pull, the omen and the beat for every cast on its way.
    void GatherFx(float dt)
    {
        gathering.RemoveWhere(c => !battle.Casts.Contains(c));
        bool great = false;
        foreach (var c in battle.Casts)
        {
            float g = Gathered(c);
            var by = CasterOf(c);
            var col = SpellColor(c.A.Tradition);
            if (gathering.Add(c) && c.A.Great) PlayCue(Cue.Gather, by, 220);
            // The pull: motes drift in to the caster from a ring round him, faster as it gathers.
            float want = dt * (c.A.Great ? 46 : c.A.Warn >= 1 ? 16 : 5) * (0.4f + g);
            for (int i = 0; i < (int)want + (fx.NextSingle() < want % 1 ? 1 : 0); i++)
            {
                float a = fx.NextSingle() * MathF.Tau, r = 3 + fx.NextSingle() * (c.A.Great ? 7 : 3), life = 0.9f;
                var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                motes.Add(new Mote { Pos = G(by + d * r, 0.2f + fx.NextSingle() * 1.6f), Vel = new Vector3(-d.X, 0.5f, -d.Y) * (r / life),
                    Life = life, Max = life, Size = 0.07f, Col = fx.Next(3) == 0 ? new Color(176, 160, 124) : Color.Lerp(col, Color.White, fx.NextSingle() * 0.5f) });
            }
            if (!c.A.Great) continue;
            great = true;
            Omen(Ease.InOut((g - 0.2f) / 0.6f) * 0.7f);
            if (g > 0.55f) Beat(c.By.Team == 0 ? BeatKind.OwnWorking : BeatKind.FoeWorking, c.At, $"{(c.By.Team == 0 ? "YOUR" : "THE ENEMY")} CIRCLE WORKS {c.A.Name.ToUpperInvariant()}", 2.8f);
        }
        calmWant = great ? 1 : 0;
    }

    float calmWant, calm;

    /// A sound from the screen's side (not the sim's list), heard from the camera.
    void PlayCue(Cue cue, Vector2 at, float range)
    {
        var audio = host.Audio;
        if (audio == null || !audio.Enabled) return;
        audio.PlayAt(cue, new Vector3(at.X, battle.Terrain.Height(at) + 1, at.Y), eye, Matrix.Invert(view).Right, range);
    }

    /// The warning circle of a cast on its way: its tradition's marks drawn round the spot as it gathers, an inner ring
    /// turning the other way, spinning up in the last fifth; attacks keep their inner ring closing on the spot.
    void Sigil(Cast c)
    {
        float g = Gathered(c), r = MathF.Max(1, c.A.Radius), t = battle.Time;
        var col = Color.Lerp(SpellColor(c.A.Tradition), Color.White, g * g);
        float spin = t * 0.25f + (g > 0.8f ? MathF.Pow((g - 0.8f) / 0.2f, 2) * 2.5f : 0);
        float arc = Math.Clamp(g * 1.25f, 0.08f, 1) * MathF.Tau; // complete at 80%
        Arc(c.At, r, spin, arc, col * 0.78f);
        Arc(c.At, r * 0.82f, -spin * 1.3f, arc, col * 0.45f);
        int n = r < 3 ? 8 : 12;
        for (int i = 0; i < n; i++)
        {
            float a = i * MathF.Tau / n;
            if (a > arc) break;
            a += spin;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var side = new Vector2(-d.Y, d.X);
            var mid = c.At + d * r * 0.91f;
            switch (c.A.Tradition)
            {
                case "faith": // rays with a cross-bar
                    Band(G(c.At + d * r * 0.82f, 0.12f), G(c.At + d * r * 1.08f, 0.12f), col * 0.72f, 0.65f);
                    Band(G(mid - side * r * 0.05f, 0.12f), G(mid + side * r * 0.05f, 0.12f), col * 0.72f, 0.65f);
                    break;
                case "craft": // a rune: a chevron between the rings
                    Band(G(mid - side * r * 0.06f - d * r * 0.05f, 0.12f), G(mid + d * r * 0.05f, 0.12f), col * 0.72f, 0.65f);
                    Band(G(mid + d * r * 0.05f, 0.12f), G(mid + side * r * 0.06f - d * r * 0.05f, 0.12f), col * 0.72f, 0.65f);
                    break;
                case "necromancy": // hooks
                    Band(G(mid - d * r * 0.06f, 0.12f), G(mid + d * r * 0.04f, 0.12f), col * 0.72f, 0.65f);
                    Band(G(mid + d * r * 0.04f, 0.12f), G(mid + d * r * 0.04f + side * r * 0.05f, 0.12f), col * 0.72f, 0.65f);
                    break;
                default: Band(G(c.At + d * r * 0.88f, 0.12f), G(c.At + d * r, 0.12f), col * 0.72f, 0.65f); break;
            }
        }
        if (c.A.Kind is "attack" or "bolt") Ring(c.At, r * (0.8f - 0.5f * g), col * 0.75f, 12);
        PotArcs(c, g);
        // The glowing spire over the caster: taller for a great working, so the whole field sees it gather.
        var by = CasterOf(c);
        float tall = (c.A.Great ? 28 : c.A.Warn >= 1 ? 6 : 2.5f) * Ease.Out(g);
        BeamQuad(G(by, 0.15f), G(by, 0.15f + tall), c.A.Great ? 0.55f : 0.25f, 0.08f, col * 0.45f, Color.Transparent);
        BeamQuad(G(by, 0.15f), G(by, 0.15f + tall * 0.85f), c.A.Great ? 0.16f : 0.08f, 0.03f, Color.Lerp(col, Color.White, 0.6f) * 0.75f, Color.Transparent);
    }

    /// Part of a ring on the ground from angle `from`, `sweep` radians round.
    void Arc(Vector2 at, float r, float from, float sweep, Color col)
    {
        int seg = Math.Max(3, (int)(sweep / MathF.Tau * 40));
        var prev = at + new Vector2(MathF.Cos(from), MathF.Sin(from)) * r;
        for (int i = 1; i <= seg; i++)
        {
            float a = from + sweep * i / seg;
            var p = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            Band(G(prev, 0.12f), G(p, 0.12f), col, 0.7f);
            prev = p;
        }
    }

    /// The light of casts gathering: a glow over the caster that grows as it gathers.
    void GatherLights()
    {
        foreach (var c in battle.Casts)
        {
            float g = Gathered(c);
            if (c.A.Warn < 1 && !c.A.Great) continue;
            AddGlow(CasterOf(c), 1.6f, SpellColor(c.A.Tradition), (c.A.Great ? 1.2f : 0.5f) * g, c.A.Great ? 7 : 3.5f);
        }
    }
}
