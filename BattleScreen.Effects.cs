using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen: blows that land (docs/archive/VISUALS.md V3): the camera's kick, the hit pause, marks left on the ground, falling
/// banners. All of it is drawing only: nothing here touches the battle.
partial class BattleScreen
{
    // ---------- camera kick and hit pause ----------

    float trauma, hitPause, lastHitAt = -1, lastPlayerHp = -1;
    Vector3 camKick, camKickVel;

    /// Directional camera kick (CR1): spring-damped impulse aligned to swing/impact vectors.
    void KickDir(Vector3 dir, float strength)
    {
        if (dir.LengthSquared() < 1e-6f) return;
        camKickVel += Vector3.Normalize(dir) * (strength * Settings.Current.CameraShake);
    }

    Vector3 UpdateCamKick()
    {
        float dt = MathF.Min(frameDt, 0.05f);
        // Critically damped spring back to zero (omega = 22 rad/s).
        const float w = 22f;
        var accel = -w * w * camKick - 2f * w * camKickVel;
        camKickVel += accel * dt;
        camKick += camKickVel * dt;
        if (camKick.LengthSquared() < 1e-6f && camKickVel.LengthSquared() < 1e-6f) { camKick = camKickVel = Vector3.Zero; }
        return camKick * (1 - MathHelper.SmoothStep(0, 1, blend));
    }

    /// Read what just happened near you: your blow landing, a blow on you, a charge striking close by.
    void Feel()
    {
        var p = battle.Player;
        if (battle.PlayerHit.At != lastHitAt)
        {
            lastHitAt = battle.PlayerHit.At;
            var text = battle.PlayerHit.Text;
            var f = new Vector3(MathF.Cos(p.Facing), 0, MathF.Sin(p.Facing));
            var r = new Vector3(-f.Z, 0, f.X);
            var swingKick = p.Swing switch
            {
                AttackDir.Left => r,
                AttackDir.Right => -r,
                AttackDir.Overhead => new Vector3(0, -1, 0),
                _ => f,
            };
            if (text.Contains("GUARD BREAK") || text.Contains("GUARD BROKEN"))
            {
                Kick(0.32f); KickDir(-f, 2.2f);
                AddSparkFlash(p.Pos + p.Dir * 0.9f, 1.3f, 0.9f, new Color(255, 220, 120));
            }
            else if (text.StartsWith("PARRY") || text.StartsWith("CHAMBER") || text.StartsWith("BLADE CLASH"))
            {
                Kick(0.20f); KickDir(-f, 1.8f);
                AddSparkFlash(p.Pos + p.Dir * 0.85f, 1.35f, 0.95f, new Color(255, 245, 195));
            }
            else if (text.Contains("DMG"))
            {
                bool heavy = battle.Blows.Count > 0 && battle.Blows[^1] is { OnPlayer: false } lb && (lb.Fatal || lb.Amount >= lb.Target.Type.MaxHp * 0.4f);
                Kick(heavy ? 0.30f : 0.20f); KickDir(swingKick, heavy ? 2.0f : 1.3f);
                if (battle.Blows.Count > 0 && battle.Blows[^1] is { OnPlayer: false } hitBlow)
                    Spray(hitBlow.Target.Pos, 1.15f, heavy ? 9 : 5, new Color(170, 26, 22), heavy ? 4.8f : 3.2f, 2.2f, 0.32f, true, 0.45f);
            }
            else if (text == "BLOCKED" || text.EndsWith("BASH!") || text == "KICK!")
            {
                Kick(0.14f); KickDir(-f, 1.2f);
                AddSparkFlash(p.Pos + p.Dir * 0.85f, 1.25f, 0.65f, new Color(235, 225, 190));
            }
        }
        // CR6: Steel clash sparks for any nearby parries, chambers, or shield blocks.
        foreach (var (cue, at) in battle.Sounds)
            if (cue is Cue.ParrySteel or Cue.ShieldSplinter && Vector2.DistanceSquared(at, p.Pos) < 22f * 22f)
                AddSparkFlash(at, 1.3f, cue == Cue.ParrySteel ? 0.85f : 0.65f, cue == Cue.ParrySteel ? new Color(255, 244, 188) : new Color(235, 200, 140));
        if (lastPlayerHp >= 0 && p.Hp < lastPlayerHp - 0.5f)
        {
            Kick(0.4f);
            var from = p.Pos - p.HitFrom;
            if (from.LengthSquared() > 1e-4f) KickDir(new Vector3(from.X, 0.25f, from.Y), 2.4f);
        }
        lastPlayerHp = p.Hp;
        foreach (var (cue, at) in battle.Sounds)
            if (Synth.HeavyImpact(cue)) { float d = Vector2.Distance(at, p.Pos); if (d < 16) Kick(0.3f * (1 - d / 16)); }
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
            if (Synth.DrawsBlood(cue) && fx.NextSingle() < 0.45f)
                AddMark(at + new Vector2(fx.NextSingle() - 0.5f, fx.NextSingle() - 0.5f) * 0.8f, 0.6f + fx.NextSingle() * 0.5f, new Color(255, 255, 255), true);
            else if (Synth.HeavyImpact(cue))
            {
                AddMark(at, 1.4f + fx.NextSingle() * 0.8f, wet ? new Color(70, 56, 40) : new Color(118, 98, 70), false);
                Spray(at, 0.1f, 8, wet ? new Color(96, 82, 60) : new Color(176, 160, 124), 4.5f, 1.6f, 0.35f, false, 1.1f); // the dust wave
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
            else if (u.Active && u.Target != null && u.Action != UnitAction.None) Stamp(u.Pos, 4); // men fighting where they stand churn it up
        foreach (var (cue, at) in battle.Sounds) if (Synth.HeavyImpact(cue)) Stamp(at, 60);
        foreach (var u in battle.Units) // a man going down tears the turf where he falls
            if (u.Dead && u.DiedAt > battle.Time - 0.5f) Stamp(u.Pos, 30);
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

    /// Riders whose horses are held at the top of a rear (the pose sheet shot).
    readonly HashSet<Unit> frozenRear = new();

    float RearOf(Unit u, bool update)
    {
        if (!u.Mounted || u.Dead || u.Horse!.Barding == "wolf") return 0;
        if (frozenRear.Contains(u)) return 1;
        if (update)
        {
            float sp = MathF.Abs(u.Speed);
            if (u.RearPeak > 7.5f && sp < 2.5f && realTime - u.RearAt > 3) u.RearAt = realTime;
            u.RearPeak = MathF.Max(sp, u.RearPeak - frameDt * 24);
        }
        float k = (realTime - u.RearAt) / 1.1f;
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

    void DrawFallingBanners(bool do3D = false)
    {
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            var f = falling[i];
            float k = (realTime - f.Born) / 0.9f;
            if (k > 1.6f) { falling.RemoveAt(i); continue; } // it lies a moment, then the next man has it up again
            float lean = MathHelper.ToRadians(84) * Ease.In(MathF.Min(1, k));
            if (do3D && puppet3D != null) puppet3D.Banner(f.Pos, f.Ground, f.Palette, 0, f.Charge, 0, lean, f.Dir);
            else puppet.Banner(f.Pos, f.Ground, f.Palette, 0, f.Charge, 0, lean, f.Dir);
        }
    }

    // ---------- 3D melee slash arcs & parry starbursts ----------

    struct SlashArc { public Vector3 Origin; public float Yaw, Reach, Born, Life; public AttackDir Dir; public Color Col; public bool Heavy; }
    struct SparkFlash { public Vector3 At; public float Born, Life, Size; public int Seed; public Color Col; }
    readonly List<SlashArc> slashArcs = new();
    readonly List<SparkFlash> sparkFlashes = new();
    readonly Dictionary<int, float> lastSlashAt = new();

    void ResetCombatFx() { slashArcs.Clear(); sparkFlashes.Clear(); lastSlashAt.Clear(); }

    void AddSparkFlash(Vector2 at, float y, float size, Color col)
    {
        if (quality == Quality.Low || sparkFlashes.Count > 48) return;
        sparkFlashes.Add(new SparkFlash
        {
            At = G(at, y),
            Born = battle.Time,
            Life = 0.15f,
            Size = size,
            Seed = fx.Next(),
            Col = col,
        });
    }

    void TrackSlashes()
    {
        if (quality == Quality.Low || slashArcs.Count > 40) return;
        float now = battle.Time;
        var eye2 = new Vector2(eye.X, eye.Z);
        float maxD2 = 42f * 42f;
        var units = battle.Units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (!u.Active || u.Action != UnitAction.Active) continue;
            if (u.Type.Class is UnitClass.Archer or UnitClass.Caster) continue;
            bool priority = u.IsPlayer || u.IsLeader || u.Hero || u.Type.Reach >= 2.2f;
            if (!priority && Vector2.DistanceSquared(u.Pos, eye2) > maxD2) continue;
            if (lastSlashAt.TryGetValue(u.Index, out float prev) && now - prev < 0.25f) continue;
            if (u.SwingProgress < 0.05f) continue;
            lastSlashAt[u.Index] = now;

            Color col = u.IsPlayer && battle.AuraBlade ? AuraColour
                : u.IsPlayer || u.IsLeader || u.Hero ? new Color(255, 232, 168)
                : new Color(224, 232, 245);
            float h = u.Mounted ? 2.05f : 1.28f;
            slashArcs.Add(new SlashArc
            {
                Origin = G(u.Pos, h),
                Yaw = u.ViewFacing,
                Reach = Math.Clamp(u.Type.Reach * 0.72f, 1.05f, 2.35f),
                Born = now,
                Life = priority ? 0.20f : 0.15f,
                Dir = u.Swing,
                Col = col,
                Heavy = priority || u.Type.Reach >= 2.1f,
            });
            if (slashArcs.Count > 40) break;
        }
    }

    /// Render tapered 3D weapon swing ribbons and steel parry starbursts in the alpha-blended ribbon pass.
    void DrawCombatFx()
    {
        float now = battle.Time;
        slashArcs.RemoveAll(s => now - s.Born > s.Life || now < s.Born);
        foreach (var s in slashArcs)
        {
            float k = (now - s.Born) / s.Life;
            float fade = Math.Clamp(1f - k * k, 0f, 1f);
            var fwd = new Vector3(MathF.Cos(s.Yaw), 0, MathF.Sin(s.Yaw));
            var right = new Vector3(-fwd.Z, 0, fwd.X);
            var up = Vector3.UnitY;

            if (s.Dir == AttackDir.Thrust)
            {
                var p0 = s.Origin + fwd * (0.35f + k * 0.45f);
                var p1 = s.Origin + fwd * (s.Reach * 1.15f + k * 0.25f);
                var core = Color.Lerp(s.Col, Color.White, 0.65f) * (0.85f * fade);
                BeamQuad(p0, p1, 0.04f * fade, 0.14f * fade, Color.Transparent, core);
                continue;
            }

            const int segs = 6;
            float r = s.Reach;
            float wBase = (s.Heavy ? 0.26f : 0.18f) * fade;
            Vector3 ArcPt(float t)
            {
                if (s.Dir == AttackDir.Overhead)
                {
                    float a = MathHelper.Lerp(-0.95f, 0.85f, t); // high overhead down to front
                    return s.Origin + fwd * (MathF.Cos(a) * r) + up * (MathF.Sin(-a) * r * 0.88f + 0.15f);
                }
                float sign = s.Dir == AttackDir.Left ? -1f : 1f;
                float ang = MathHelper.Lerp(-1.15f, 1.05f, t) * sign;
                return s.Origin + (fwd * MathF.Cos(ang) + right * MathF.Sin(ang)) * r + up * ((t - 0.5f) * 0.22f * sign);
            }

            // Sweep advances smoothly across the arc while trailing edge fades
            float headT = Math.Clamp(0.45f + k * 0.85f, 0.1f, 1f);
            float tailT = Math.Clamp(headT - 0.72f, 0f, headT);
            var prev = ArcPt(tailT);
            for (int j = 1; j <= segs; j++)
            {
                float f0 = (j - 1) / (float)segs, f1 = j / (float)segs;
                float t1 = MathHelper.Lerp(tailT, headT, f1);
                var next = ArcPt(t1);
                var c0 = Color.Lerp(s.Col, Color.White, f0 * 0.55f) * (fade * f0 * 0.78f);
                var c1 = Color.Lerp(s.Col, Color.White, f1 * 0.75f) * (fade * f1 * 0.92f);
                BeamQuad(prev, next, wBase * (0.25f + 0.75f * f0), wBase * (0.25f + 0.75f * f1), c0, c1);
                prev = next;
            }
        }

        sparkFlashes.RemoveAll(sp => now - sp.Born > sp.Life || now < sp.Born);
        foreach (var sp in sparkFlashes)
        {
            float k = (now - sp.Born) / sp.Life;
            float fade = Math.Clamp(1f - k, 0f, 1f);
            var core = Color.Lerp(sp.Col, Color.White, 0.7f) * (0.95f * fade);
            var outer = sp.Col * (0.55f * fade);
            float reach = sp.Size * (0.45f + 0.95f * Ease.OutCubic(k));
            for (int j = 0; j < 5; j++)
            {
                float a = j * MathF.Tau / 5f + (sp.Seed % 100) * 0.06f;
                float elev = ((j % 2) * 2 - 1) * 0.45f;
                var dir = Vector3.Normalize(new Vector3(MathF.Cos(a), elev, MathF.Sin(a)));
                BeamQuad(sp.At + dir * (reach * 0.15f), sp.At + dir * reach, 0.07f * fade, 0.01f, core, outer);
            }
        }
    }
}
