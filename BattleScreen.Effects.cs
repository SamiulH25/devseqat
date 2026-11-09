using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen: blows that land (docs/VISUALS.md V3): the camera's kick, the hit pause, marks left on the ground, falling
/// banners. All of it is drawing only: nothing here touches the battle.
partial class BattleScreen
{
    // ---------- camera kick and hit pause ----------

    float trauma, hitPause, lastHitAt = -1, lastPlayerHp = -1;

    /// Read what just happened near you: your blow landing, a blow on you, a charge striking close by.
    void Feel()
    {
        var p = battle.Player;
        if (battle.PlayerHit.At != lastHitAt)
        {
            lastHitAt = battle.PlayerHit.At;
            var text = battle.PlayerHit.Text;
            if (text.Contains("DMG")) { Kick(0.22f); if (Settings.Current.HitPause && quality != Quality.Low) hitPause = 0.045f; }
            else if (text.StartsWith("PARRY") || text == "BLOCKED" || text.EndsWith("BASH!") || text == "KICK!") Kick(0.12f);
        }
        if (lastPlayerHp >= 0 && p.Hp < lastPlayerHp - 0.5f) Kick(0.4f);
        lastPlayerHp = p.Hp;
        foreach (var (cue, at) in battle.Sounds)
            if (cue == Cue.Impact) { float d = Vector2.Distance(at, p.Pos); if (d < 16) Kick(0.3f * (1 - d / 16)); }
    }

    void Kick(float amount) => trauma = MathF.Min(1, trauma + amount);

    /// The camera's shake this frame (field view only; the command camera stays still), in world units.
    Vector3 Shake()
    {
        trauma = MathF.Max(0, trauma - frameDt * 2.4f);
        float k = trauma * trauma * Settings.Current.CameraShake * (1 - MathHelper.SmoothStep(0, 1, blend));
        if (k <= 0) return Vector3.Zero;
        float t = realTime * 31;
        return new Vector3(MathF.Sin(t) + MathF.Sin(t * 2.3f) * 0.5f, MathF.Sin(t * 1.7f + 1) * 0.7f, MathF.Cos(t * 1.3f)) * 0.09f * k;
    }

    // ---------- marks on the ground ----------

    struct Mark { public Vector2 Pos; public float Size, Rot; public Color Col; public bool Blood; }
    readonly Mark[] marks = new Mark[700];
    int markNext, markCount;

    int MarkCap => quality switch { Quality.High => marks.Length, Quality.Medium => 250, _ => 0 };

    void AddMark(Vector2 at, float size, Color col, bool blood)
    {
        int cap = MarkCap;
        if (cap == 0) return;
        marks[markNext] = new Mark { Pos = at, Size = size, Rot = fx.NextSingle() * MathHelper.TwoPi, Col = col, Blood = blood };
        markNext = (markNext + 1) % cap;
        markCount = Math.Min(cap, markCount + 1);
    }

    /// Blood where men are cut, churned earth where a charge strikes, a wider dust wave along its front.
    void LeaveMarks()
    {
        bool wet = battle.Weather.Rain > 0.3f;
        foreach (var (cue, at) in battle.Sounds)
            switch (cue)
            {
                case Cue.Hit or Cue.ArrowHit when fx.NextSingle() < 0.45f:
                    AddMark(at + new Vector2(fx.NextSingle() - 0.5f, fx.NextSingle() - 0.5f) * 0.8f, 0.6f + fx.NextSingle() * 0.5f, new Color(255, 255, 255), true);
                    break;
                case Cue.Impact:
                    AddMark(at, 1.4f + fx.NextSingle() * 0.8f, wet ? new Color(70, 56, 40) : new Color(118, 98, 70), false);
                    Spray(at, 0.1f, 8, wet ? new Color(96, 82, 60) : new Color(176, 160, 124), 4.5f, 1.6f, 0.35f, false, 1.1f); // the dust wave
                    break;
            }
    }

    void DrawMarks()
    {
        var t = battle.Terrain;
        var eye2 = new Vector2(eye.X, eye.Z);
        float range = quality == Quality.High ? 140 : 80;
        for (int i = 0; i < markCount; i++)
        {
            ref var m = ref marks[i];
            if (Vector2.DistanceSquared(m.Pos, eye2) > range * range) continue;
            puppet.Decal(m.Pos, t.Height(m.Pos), m.Size, m.Rot, m.Col, m.Blood ? "blood" : "splat");
        }
    }

    // ---------- the trample map ----------

    /// Where men have walked and fought, on a 2 m grid over the middle 320 m of the field (red channel, 0..255): the grass
    /// shader flattens and bruises the blades and the earth shows through (Shaders/world.glsl). Stamped twice a second.
    const int TrampleN = 160;
    const float TrampleSize = 320;
    readonly byte[] trample = new byte[TrampleN * TrampleN];
    readonly Color[] tramplePx = new Color[TrampleN * TrampleN];
    Microsoft.Xna.Framework.Graphics.Texture2D? trampleTex;
    float trampleAt;
    bool trampleDirty;

    void Stamp(Vector2 p, int amount)
    {
        int x = (int)((p.X / TrampleSize + 0.5f) * TrampleN), y = (int)((p.Y / TrampleSize + 0.5f) * TrampleN);
        if ((uint)x >= TrampleN || (uint)y >= TrampleN) return;
        int i = y * TrampleN + x;
        trample[i] = (byte)Math.Min(255, trample[i] + amount);
        trampleDirty = true;
    }

    void Trample()
    {
        if (quality == Quality.Low || battle.Time - trampleAt < 0.5f) return;
        trampleAt = battle.Time;
        foreach (var u in battle.Units)
            if (u.Active && u.Vel.LengthSquared() > 0.25f) Stamp(u.Pos, u.Mounted ? 6 : 2);
        foreach (var (cue, at) in battle.Sounds) if (cue == Cue.Impact) Stamp(at, 60);
    }

    /// Where men stand (green channel), for the command view: the foliage shader thins the leaves that lie between the
    /// camera and them, so a company in a wood is not lost under the canopy. Enemies you have not seen stay hidden.
    readonly byte[] men = new byte[TrampleN * TrampleN];
    bool menOn;
    float menAt = -1;

    void MenMap(float cut)
    {
        if (cut <= 0.01f)
        {
            if (menOn) { Array.Clear(men); menOn = false; trampleDirty = true; }
            return;
        }
        if (menOn && realTime - menAt < 0.1f) return;
        menAt = realTime;
        menOn = true;
        Array.Clear(men);
        foreach (var u in battle.Units)
        {
            if (!u.Active || u.Team == 1 && !u.IsLeader && !battle.Shown(u.Squad)) continue;
            int x = (int)((u.Pos.X / TrampleSize + 0.5f) * TrampleN), y = (int)((u.Pos.Y / TrampleSize + 0.5f) * TrampleN);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((uint)(x + dx) < TrampleN && (uint)(y + dy) < TrampleN) men[(y + dy) * TrampleN + x + dx] = 255;
        }
        trampleDirty = true;
    }

    /// The trample map as a texture for the world shader (uploaded only when it changed).
    Microsoft.Xna.Framework.Graphics.Texture2D TrampleTexture()
    {
        trampleTex ??= new Microsoft.Xna.Framework.Graphics.Texture2D(GraphicsDevice, TrampleN, TrampleN);
        if (trampleDirty)
        {
            for (int i = 0; i < trample.Length; i++) tramplePx[i] = new Color((int)trample[i], (int)men[i], 0, 255);
            trampleTex.SetData(tramplePx);
            trampleDirty = false;
        }
        return trampleTex;
    }

    // ---------- rearing horses ----------

    /// Each rider's recent top speed and when his horse last reared: a horse brought from a gallop to a near stop in a
    /// moment (a charge hitting home, a collision) rears for about a second. View only.
    readonly Dictionary<Unit, (float Peak, float At)> rears = new();

    /// Riders whose horses are held at the top of a rear (the pose sheet shot).
    readonly HashSet<Unit> frozenRear = new();

    float RearOf(Unit u, bool update)
    {
        if (!u.Mounted || u.Dead || u.Horse!.Barding == "wolf") return 0;
        if (frozenRear.Contains(u)) return 1;
        if (!rears.TryGetValue(u, out var r)) r = (0, -99);
        if (update)
        {
            float sp = MathF.Abs(u.Speed);
            if (r.Peak > 7.5f && sp < 2.5f && realTime - r.At > 3) r.At = realTime;
            r.Peak = MathF.Max(sp, r.Peak - frameDt * 24);
            rears[u] = r;
        }
        float k = (realTime - r.At) / 1.1f;
        if (k < 0 || k >= 1) return 0;
        return k < 0.3f ? Ease.Out(k / 0.3f) : 1 - Ease.InOut((k - 0.3f) / 0.7f);
    }

    // ---------- banners ----------

    /// Who carried each company's banner last frame; when he falls, the banner topples before the next man takes it up.
    readonly Dictionary<Squad, Unit> bearers = new();
    struct Falling { public Vector2 Pos, Dir; public float Ground, Born; public int Palette, Charge; }
    readonly List<Falling> falling = new();

    void BannerFalls(Squad s, Unit? bearer)
    {
        if (bearers.TryGetValue(s, out var old) && old != bearer && old.Dead && s.Company != null)
        {
            var at = old.Pos - old.Dir * 0.35f;
            var dir = old.DiedFrom == Vector2.Zero || old.Pos == old.DiedFrom ? -old.Dir : Vector2.Normalize(old.Pos - old.DiedFrom);
            falling.Add(new Falling { Pos = at, Dir = dir, Ground = battle.Terrain.Height(at), Born = realTime, Palette = old.Palette, Charge = s.Company.Banner });
        }
        if (bearer != null) bearers[s] = bearer; else bearers.Remove(s);
    }

    void DrawFallingBanners()
    {
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            var f = falling[i];
            float k = (realTime - f.Born) / 0.9f;
            if (k > 1.6f) { falling.RemoveAt(i); continue; } // it lies a moment, then the next man has it up again
            puppet.Banner(f.Pos, f.Ground, f.Palette, 0, f.Charge, 0, MathHelper.ToRadians(84) * Ease.In(MathF.Min(1, k)), f.Dir);
        }
    }
}
