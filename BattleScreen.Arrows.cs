using Microsoft.Xna.Framework;

namespace Ironfield;

/// Arrows you can see (docs/COMPANY.md C14). Drawing only: `Battle.Arrows` and every hit stay as they are. Near the camera
/// each missile is a real sprite turned along its flight (the bowmen's own "arrow" part, a short thick bolt, the javelin),
/// with its fletching in the shooting side's cloth; far off it thins to a light streak so a volley of hundreds reads as one
/// flight. Arrows that come down stand in the ground at the angle they fell, for the whole battle (capped, the oldest
/// fading), so a beaten zone can be read from above; a miss kicks up dust, mud or water. Low quality keeps the old lines.
sealed partial class BattleScreen
{
    /// An arrow standing where it fell: the point in the ground, the way it came down, what it was and whose.
    readonly record struct Landed(Vector3 At, Vector3 Dir, Grip Kind, int Palette, float Time);

    readonly List<Landed> landed = new();
    readonly HashSet<Projectile> seenLanding = new();
    readonly Dictionary<(int, int), int> landedIn = new();
    /// Most standing arrows kept in one 0.95 m patch of ground.
    const int LandedPerCell = 2;
    const float LandedCell = 0.95f;
    /// Most arrows left standing in the ground, and the metres within which missiles are sprites rather than streaks.
    const int LandedCap = 1200;
    const float SpriteNear = 35;

    void ResetArrows() { landed.Clear(); seenLanding.Clear(); landedIn.Clear(); }

    /// Each frame the battle runs: note arrows that have just come down (they stand there for the battle) and kick up the
    /// ground where they fell.
    void TrackArrows()
    {
        if (battle.Arrows.Count == 0 && seenLanding.Count > 0) seenLanding.Clear();
        foreach (var a in battle.Arrows)
        {
            if (a.Stuck < 0 || a.Kind == Grip.Sling || !seenLanding.Add(a)) continue;
            var dir = a.Vel.LengthSquared() > 1e-4f ? Vector3.Normalize(a.Vel) : Vector3.Down;
            dir = Steep(dir); // it stands in the ground, leaning the way it came
            var cell = ((int)MathF.Floor(a.Pos.X / LandedCell), (int)MathF.Floor(a.Pos.Z / LandedCell));
            int n = landedIn.GetValueOrDefault(cell);
            if (n < LandedPerCell) // a thick fall reads as a thicket, not a mat
            {
                landedIn[cell] = n + 1;
                landed.Add(new Landed(a.Pos, dir, a.Kind, a.Shooter.Palette, battle.Time));
                if (landed.Count > LandedCap) { var old = landed[0]; landed.RemoveAt(0); var oc = ((int)MathF.Floor(old.At.X / LandedCell), (int)MathF.Floor(old.At.Z / LandedCell)); landedIn[oc] = Math.Max(0, landedIn.GetValueOrDefault(oc) - 1); }
            }
            if (quality != Quality.Low) Puff(new Vector2(a.Pos.X, a.Pos.Z));
        }
        if (seenLanding.Count > 4000) seenLanding.RemoveWhere(p => p.Stuck < 0 || !battle.Arrows.Contains(p));
    }

    /// An arrow that comes down stands at least 55 degrees from the ground, leaning the way it flew.
    static Vector3 Steep(Vector3 dir)
    {
        const float MinDown = 0.82f; // sin 55
        if (-dir.Y >= MinDown) return dir;
        var flat = new Vector2(dir.X, dir.Z);
        flat = flat.LengthSquared() > 1e-6f ? Vector2.Normalize(flat) * MathF.Sqrt(1 - MinDown * MinDown) : Vector2.Zero;
        return new Vector3(flat.X, -MinDown, flat.Y);
    }

    /// A miss kicks up what it lands in: water, mud or dust.
    void Puff(Vector2 at)
    {
        var t = battle.Terrain;
        if (t.WaterDepth(at) > 0.05f) Spray(at, 0.05f, 3, new Color(200, 214, 220), 1.4f, 0.45f, 0.05f, true);
        else if (t.GroundAt(at) == GroundKind.Mud) Spray(at, 0.05f, 2, new Color(70, 54, 36), 1.1f, 0.5f, 0.05f, true);
        else Spray(at, 0.05f, 2, new Color(176, 160, 128), 0.7f, 0.6f, 0.09f, false, 0.4f);
    }

    /// The sprite part, length (m) and thickness scale of each kind of missile.
    static (string Part, float Len, float Thick) MissileLook(Grip kind) => kind switch
    {
        Grip.Thrown => ("javelin", 1.3f, 0.34f),
        Grip.Crossbow => ("arrow", 0.38f, 0.28f), // a short thick bolt
        _ => ("arrow", 0.65f, 0.25f),
    };

    /// Missiles near the camera as sprites (in flight and standing), in the sprite pass.
    void DrawMissiles()
    {
        if (quality == Quality.Low) return;
        var eye2 = new Vector2(eye.X, eye.Z);
        float near2 = SpriteNear * SpriteNear;
        foreach (var a in battle.Arrows)
        {
            if (a.Kind == Grip.Sling || a.Stuck >= 0 || Vector2.DistanceSquared(new Vector2(a.Pos.X, a.Pos.Z), eye2) > near2) continue;
            var dir = a.Vel.LengthSquared() > 1e-4f ? Vector3.Normalize(a.Vel) : Vector3.Down;
            var (part, len, thick) = MissileLook(a.Kind);
            puppet.Missile(a.Pos, a.Pos - dir * len, part, thick, Style.Sides[Math.Clamp(a.Shooter.Palette, 0, Style.Sides.Length - 1)].Cloth);
        }
        foreach (var l in landed)
        {
            if (Vector2.DistanceSquared(new Vector2(l.At.X, l.At.Z), eye2) > near2) continue;
            var (part, len, thick) = MissileLook(l.Kind);
            float embed = 0.16f + Ease.Hash((int)(l.At.X * 197 + l.At.Z * 503)) * 0.22f;
            var head = l.At + l.Dir * embed; // the point sunk deep into the turf
            puppet.Missile(head, head - l.Dir * len, part, thick, Style.Sides[Math.Clamp(l.Palette, 0, Style.Sides.Length - 1)].Cloth);
        }
    }

    /// Missiles as lines: everything on Low; far ones as a light streak (in flight) or a short dark stub (standing) otherwise.
    void MissileLines()
    {
        var eye2 = new Vector2(eye.X, eye.Z);
        float near2 = quality == Quality.Low ? -1 : SpriteNear * SpriteNear;
        foreach (var a in battle.Arrows)
        {
            var dir = a.Vel.LengthSquared() > 0 ? Vector3.Normalize(a.Vel) : Vector3.Down;
            if (a.Kind == Grip.Sling)
            {
                if (a.Stuck < 0) Line(a.Pos, a.Pos - dir * 0.12f, StoneColor); // a stone: a dark speck, gone once it lands
                continue;
            }
            if (a.Stuck >= 0) continue; // standing arrows are drawn from `landed`
            float len = MissileLook(a.Kind).Len;
            bool sprite = Vector2.DistanceSquared(new Vector2(a.Pos.X, a.Pos.Z), eye2) <= near2;
            if (quality == Quality.Low) { Line(a.Pos, a.Pos - dir * len, ArrowColor); continue; }
            if (!sprite) { Line(a.Pos, a.Pos - dir * 0.14f, ArrowHead); Line(a.Pos - dir * 0.14f, a.Pos - dir * (len + 0.9f), ArrowStreak); } // far: a bright head and compact streak
            else Line(a.Pos - dir * len, a.Pos - dir * (len + 0.35f), ArrowStreak); // subtle tail flicker behind near sprite arrows
        }
        foreach (var l in landed)
        {
            if (Vector2.DistanceSquared(new Vector2(l.At.X, l.At.Z), eye2) <= near2) continue;
            float len = MissileLook(l.Kind).Len * 0.6f;
            Line(l.At, l.At - l.Dir * len, ArrowColor);
        }
    }
}
