using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen: press shots (IRONFIELD_SHOTS=/dir with IRONFIELD_PRESS=seed). A quick battle fought by both AIs, the HUD off,
/// and a run of cinematic cameras: the armies drawn up, the captain among his men, the horse going in, the melee close and wide.
/// Pick the field with IRONFIELD_GROUND, IRONFIELD_LAYOUT and IRONFIELD_WEATHER. Debug only: the sim is untouched.
partial class BattleScreen
{
    static readonly int? PressSeed = Environment.GetEnvironmentVariable("IRONFIELD_PRESS") is { Length: > 0 } p
        ? int.TryParse(p, out var s) ? s : 7 : null;
    int pressIndex;
    bool pressWaiting;

    /// One staged shot: when (battle seconds) and how to place the camera; the setup returns false to skip it.
    record PressShot(float At, string Name, Func<bool> Setup);

    PressShot[]? pressShots;

    /// Shots with a negative time come that many seconds after the lines first meet.
    PressShot[] PressList() => new PressShot[]
    {
        new(0.6f, "01-array-low", () => Behind(0, 0.07f, 40, 0.75f)),
        new(0.6f, "02-array-enemy", () => Behind(1, 0.08f, 45, 0.85f)),
        new(0.6f, "03-array-high", () => Over(0.45f, 150, 0.6f)),
        new(0.6f, "04-captain", () => Captain(0.08f)),
        new(6f, "05-advance", () => Behind(0, 0.1f, 50, 0.8f)),
        new(9f, "06-horse", () => Horse(0, 0.06f, 60)),
        new(11f, "07-horse-enemy", () => Horse(1, 0.06f, 55)),
        new(13f, "08-volley", () => Volley()),
        new(-1f, "09-contact-close", () => Hot(0.08f, 34, 0.3f, 20)),
        new(-1f, "10-contact-wide", () => Hot(0.18f, 50, MathHelper.PiOver2)),
        new(-6f, "11-melee-close", () => Hot(0.06f, 28, 0.6f, 19)),
        new(-6f, "12-melee-mid", () => Hot(0.22f, 26, -0.9f)),
        new(-6f, "13-melee-captain", () => Captain(0.12f)),
        new(-12f, "14-melee-close2", () => Hot(0.07f, 30, 2.4f, 20)),
        new(-12f, "15-melee-line", () => Hot(0.08f, 140, MathHelper.PiOver2, 24)),
        new(-12f, "16-field-high", () => Over(0.6f, 120, 0.9f)),
        new(-20f, "17-late-close", () => Hot(0.06f, 28, -2.2f, 19)),
        new(-20f, "18-late-wide", () => Hot(0.25f, 70, 1.2f)),
        new(-30f, "19-late-close2", () => Hot(0.08f, 32, 1.0f, 21)),
        new(-30f, "20-late-low", () => Hot(0.04f, 90, -0.3f, 22)),
        new(-42f, "21-rout-close", () => Hot(0.07f, 30, 3.0f, 20)),
        new(-42f, "22-rout-wide", () => Hot(0.3f, 75, -1.6f)),
    };

    float contactAt = -1;

    void PressScript()
    {
        pressShots ??= PressList();
        hudHidden = true;
        if (pressIndex >= pressShots.Length) { if (pendingShot == null) Exit(); return; }
        if (!pressWaiting && MagicShot() is PressShot m) { pressMagic = m; paused = true; pressWaiting = m.Setup(); shotFrames = 0; return; }
        var shot = pressMagic ?? pressShots[pressIndex];
        if (!pressWaiting)
        {
            if (contactAt < 0 && battle.Units.Count(u => !u.Dead && u.EnemyDist < 2.5f) >= 40) contactAt = battle.Time;
            float at = shot.At >= 0 ? shot.At : contactAt < 0 ? float.MaxValue : contactAt - shot.At;
            if (battle.Time < at && battle.Winner < 0 && battle.Time < 240) { paused = false; return; }
            paused = true;
            if (!shot.Setup()) { pressIndex++; return; }
            pressWaiting = true; shotFrames = 0;
        }
        else if (shotFrames == 4) pendingShot = shot.Name;
        else if (shotFrames > 4 && pendingShot == null) { pressWaiting = false; if (pressMagic != null) pressMagic = null; else pressIndex++; }
    }

    PressShot? pressMagic;
    float lastMagic = -10;
    int magicShots;
    readonly HashSet<object> magicSeen = new();

    readonly List<(Vector2 At, string Id, float When, float R)> landing = new();
    static readonly string[] Showy = { "storm", "lightning", "tempest", "wildfire", "greek_fire", "fire_pots", "smite", "miracle", "crows", "smoke", "mist", "fever", "dread" };

    /// A strong spell just landed: a shot of it, close and wide in turn, one every few seconds.
    PressShot? MagicShot()
    {
        float now = battle.Time;
        foreach (var c in battle.Casts.Where(c => !magicSeen.Contains(c) && Showy.Contains(c.A.Id)))
        { magicSeen.Add(c); landing.Add((c.At, c.A.Id, c.FireAt + (magicShots % 3 == 2 ? 0.6f : 0.15f), c.A.Radius)); }
        landing.RemoveAll(l => now > l.When + 1.5f);
        if (magicShots >= 18 || now - lastMagic < 2.5f) return null;
        int i = landing.FindIndex(l => now >= l.When);
        if (i < 0) return null;
        var (at, id, _, r) = landing[i];
        landing.RemoveAt(i);
        lastMagic = now;
        bool wide = magicShots++ % 2 == 1;
        r = Math.Max(r, 4);
        var d = battle.Centroid(1) - battle.Centroid(0);
        float yaw2 = MathF.Atan2(d.Y, d.X) + (wide ? 1.3f : 0.5f) * (magicShots % 4 < 2 ? 1 : -1);
        return new PressShot(now, $"m{magicShots:00}-{id}",
            () => Aim(at, yaw2, wide ? 0.3f : 0.14f, wide ? Math.Max(50, r * 7) : Math.Max(30, r * 4.5f), wide ? 45 : 30));
    }

    /// The press camera's lens in degrees (null: the game's own); narrow from far back flattens the ranks together.
    float? pressFov;

    /// The command camera on a point, turned a little at a time until no man stands right in front of the lens.
    bool Aim(Vector2 focus, float yaw2, float pitch2, float dist, float fov = 45)
    {
        SetCommand(true); blend = 1;
        float h = battle.Terrain.Height(focus) + 0.8f;
        for (int i = 0; i < 12; i++)
        {
            float y = yaw2 + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 0.22f;
            if (!Blocked(new Vector3(focus.X, h, focus.Y), y, pitch2, dist)) { yaw2 = y; break; }
        }
        camFocus = focus; camYaw = yaw2; camPitch = pitch2; zoom = dist; pressFov = fov;
        return true;
    }

    /// Does a living man stand within the near part of the view line?
    bool Blocked(Vector3 focus, float y, float p, float dist)
    {
        var look = new Vector3(MathF.Cos(y) * MathF.Cos(p), -MathF.Sin(p), MathF.Sin(y) * MathF.Cos(p));
        var eye = focus - look * dist;
        for (float t = 0; t < 0.92f; t += 0.04f) // the ground itself: a crest between the lens and the fight
        {
            var q = eye + look * dist * t;
            if (battle.Terrain.Height(new Vector2(q.X, q.Z)) > q.Y - 0.4f) return true;
        }
        foreach (var u in battle.Units)
        {
            if (u.Dead || u.Fled) continue;
            var m = new Vector3(u.Pos.X, battle.Terrain.Height(u.Pos) + 1.1f, u.Pos.Y) - eye;
            float t = Vector3.Dot(m, look);
            if (t < 0 || t > dist * 0.55f) continue;
            if ((m - look * t).Length() < (u.Mounted ? 2.2f : 1.4f)) return true;
        }
        return false;
    }

    /// From behind one side's line, looking at the other (k: how far from that side toward the middle).
    bool Behind(int team, float pitch2, float dist, float k)
    {
        var from = battle.Centroid(team); var to = battle.Centroid(1 - team);
        var d = to - from;
        return Aim(Vector2.Lerp(to, from, k), MathF.Atan2(d.Y, d.X), pitch2, dist);
    }

    /// The whole field from above and to the side.
    bool Over(float pitch2, float dist, float turn)
    {
        var a = battle.Centroid(0); var d = battle.Centroid(1) - a;
        return Aim((a + battle.Centroid(1)) / 2, MathF.Atan2(d.Y, d.X) + turn, pitch2, dist);
    }

    /// Over the captain's shoulder toward the enemy, among his men.
    bool Captain(float pitch2)
    {
        var me = battle.Player;
        if (!me.Active) return false;
        SetCommand(false); blend = 0; pressFov = null;
        var d = battle.Centroid(1) - me.Pos;
        yaw = me.Heading = MathF.Atan2(d.Y, d.X); pitch = pitch2;
        return true;
    }

    /// A company of horse on the move, seen from the side and a little ahead.
    bool Horse(int team, float pitch2, float dist)
    {
        var q = battle.SquadsOf(team).Where(s => s.Cavalry && s.Alive > 0).OrderByDescending(s => s.Alive).FirstOrDefault();
        if (q == null) return false;
        var men = battle.Units.Where(u => u.Squad == q && !u.Dead).ToList();
        var v = men.Aggregate(Vector2.Zero, (a, u) => a + u.Vel);
        float head = v.LengthSquared() > 0.01f ? MathF.Atan2(v.Y, v.X) : MathF.Atan2(battle.Centroid(1 - team).Y - q.Centroid().Y, battle.Centroid(1 - team).X - q.Centroid().X);
        return Aim(q.Centroid() + new Vector2(MathF.Cos(head), MathF.Sin(head)) * 4, head + 2.3f, pitch2, dist, 24);
    }

    /// Arrows in the air: from behind and below, toward where they fly.
    bool Volley()
    {
        var flying = battle.Arrows.Where(a => a.Stuck < 0).ToList();
        if (flying.Count < 10) return Hot(0.2f, 40, 0.4f);
        var c = flying.Aggregate(Vector2.Zero, (s, a) => s + new Vector2(a.Pos.X, a.Pos.Z)) / flying.Count;
        var v = flying.Aggregate(Vector2.Zero, (s, a) => s + new Vector2(a.Vel.X, a.Vel.Z));
        return Aim(c, MathF.Atan2(v.Y, v.X) + 0.5f, 0.05f, 30);
    }

    /// Where the fighting is thickest: the men closest to the foe, averaged around the densest one.
    bool Hot(float pitch2, float dist, float turn, float fov = 45)
    {
        var fighting = battle.Units.Where(u => !u.Dead && !u.Fled && u.EnemyDist < 2.5f).ToList();
        if (fighting.Count < 6) return Over(pitch2 + 0.2f, dist + 30, turn);
        var core = fighting.OrderByDescending(u => fighting.Count(o => Vector2.DistanceSquared(o.Pos, u.Pos) < 64)).First();
        var near = fighting.Where(o => Vector2.DistanceSquared(o.Pos, core.Pos) < 100).ToList();
        var c = near.Aggregate(Vector2.Zero, (s, u) => s + u.Pos) / near.Count;
        var d = battle.Centroid(1) - battle.Centroid(0);
        return Aim(c, MathF.Atan2(d.Y, d.X) + turn, pitch2, dist, fov);
    }
}
