using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// BattleScreen: rendering (shaded and basic paths, sprites, particles, rain, order lines).
partial class BattleScreen
{
    // ---------- drawing ----------

    public void Draw()
    {
        var gd = GraphicsDevice;
        EaseFacings();
        bool shaded = quality != Quality.Low && shading != null;
        if (shaded)
        {
            try { DrawShaded(shading!); }
            catch (Exception e)
            {
                // A driver that rejects the GLSL: fall back to the fixed-function path for good.
                Console.WriteLine("shader pipeline failed, falling back: " + e);
                gd.SetRenderTarget(null);
                quality = Quality.Low;
                battle.Log("Shaders unavailable: basic graphics.");
                shaded = false;
            }
        }
        if (!shaded)
        {
            gd.Clear(World.Horizon);
            world.Draw(view, proj, eye);
            DrawBlobs(1);
            BuildSprites(view, false);
            DrawSpritesBasic();
            DrawLines();
        }
        SyncHud();
        if (!hudHidden)
        {
            hud.Draw();
            if (TipsHeld) return;
            tipUi.Begin(gd.Viewport, ms.Position, false);
            Tips.Draw(tipUi, tipUi.U(124));
            tipUi.End();
        }
    }

    /// The shader path: sun shadow map, then the scene into an off-screen target, then the post pass to the screen.
    void DrawShaded(Shading sh)
    {
        var gd = GraphicsDevice;
        bool shadows = quality == Quality.High;
        if (shadows)
        {
            // Cover what the camera sees: about 90 m around you in the field, more as the command camera zooms out.
            float k = MathHelper.SmoothStep(0, 1, blend);
            sh.AimSun(focusPoint, MathHelper.Lerp(90, Math.Clamp(zoom * 2.6f, 70, 260), k));
            gd.Textures[1] = null;
            gd.SetRenderTarget(sh.ShadowMap);
            gd.Clear(Color.White);
            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.BlendState = BlendState.Opaque;
            world.DrawShadowCasters(sh, realTime);
            BuildSprites(sh.LightView, true); // puppets turned to face the sun, so shadows have their silhouette
            sh.Setup(sh.Sprite, Matrix.Identity, sh.LightEye, realTime, false, 0);
            sh.Sprite.Parameters["ShadowMap"].SetValue((Texture2D?)null);
            DrawSpritesShaded(sh.Sprite, "SpriteShadow");
        }
        gd.Textures[1] = null;
        gd.SetRenderTarget(sh.Scene());
        gd.Clear(World.Horizon);
        MenMap(blend);
        sh.GroundMap = TrampleTexture(); sh.GroundSize = TrampleSize; sh.NearDetail = true;
        sh.Torch = TorchRow();
        sh.Cut = MathHelper.SmoothStep(0, 1, blend);
        SetGlows(sh);
        world.DrawShaded(sh, view, proj, eye, realTime, shadows);
        sh.GroundMap = null; sh.NearDetail = false; sh.Cut = 0;
        DrawBlobs(shadows ? 0.4f : 1); // with real shadows the blobs only add contact darkening
        BuildSprites(view, false);
        sh.Setup(sh.Sprite, view * proj, eye, realTime, shadows, 0.45f);
        DrawSpritesShaded(sh.Sprite, "Sprite");
        sh.Torch = Vector4.Zero;
        Array.Clear(sh.Glow); // the lights are this battle's alone (the map shares the shaders)
        DrawLines();
        var (awe, moment) = PostMoment();
        sh.PostToScreen(fxaa: Settings.Current.Fxaa, Sky.Grade, host.Quality == Quality.High ? 0.5f : 0, awe, moment, warpNow);
    }

    void DrawBlobs(float alpha)
    {
        shadows.N = 0;
        var t = battle.Terrain;
        void Blob(Vector2 p, float r)
        {
            // Corners follow the ground so the blob lies on slopes.
            VertexPositionTexture V(float dx, float dz, float tu, float tv)
            {
                var q = p + new Vector2(dx, dz);
                return new(new(q.X, t.Height(q) + 0.05f, q.Y), new(tu, tv));
            }
            shadows.Add(V(-r, -r, 0, 0)); shadows.Add(V(r, -r, 1, 0)); shadows.Add(V(-r, r, 0, 1));
            shadows.Add(V(r, -r, 1, 0)); shadows.Add(V(r, r, 1, 1)); shadows.Add(V(-r, r, 0, 1));
        }
        foreach (var u in battle.Units) if (u.Active) Blob(u.Pos, u.Mounted ? 1.1f : 0.55f);
        foreach (var h in battle.LooseHorses) Blob(h.Pos, 1.1f);
        if (shadows.N == 0) return;
        var gd = GraphicsDevice;
        gd.BlendState = BlendState.AlphaBlend;
        gd.DepthStencilState = DepthStencilState.DepthRead;
        gd.SamplerStates[0] = SamplerState.LinearClamp;
        shadowFx.Alpha = alpha;
        shadowFx.View = view; shadowFx.Projection = proj;
        shadowFx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, shadows.A, 0, shadows.N / 3);
    }

    /// Ease every drawn facing toward the real one, once per frame, so turning is a smooth sweep.
    void EaseFacings()
    {
        float turn = MathF.Min(1, frameDt * 12);
        foreach (var u in battle.Units)
        {
            u.ViewFacing += MathHelper.WrapAngle(u.Facing - u.ViewFacing) * turn;
            u.ViewHeading += MathHelper.WrapAngle(u.Heading - u.ViewHeading) * turn;
        }
    }

    // ---------- particles (D1) ----------

    struct Mote { public Vector3 Pos, Vel; public float Life, Max, Size, Grow, Fall; public Color Col; public bool Heavy; }
    readonly List<Mote> motes = new();
    readonly Random fx = new(5);

    void Spray(Vector2 at, float height, int n, Color col, float speed, float life, float size, bool heavy, float grow = 0, float fall = 0)
    {
        if (motes.Count > 1500) return;
        var g = battle.Terrain.Height(at);
        for (int i = 0; i < n; i++)
        {
            var dir = new Vector3(fx.NextSingle() * 2 - 1, fx.NextSingle() * 0.8f + (heavy ? 0.4f : 0.1f), fx.NextSingle() * 2 - 1);
            motes.Add(new Mote { Pos = new Vector3(at.X, g + height, at.Y), Vel = dir * speed, Life = life * (0.6f + fx.NextSingle() * 0.6f), Max = life,
                Size = size, Grow = grow, Fall = fall, Col = col, Heavy = heavy });
        }
    }

    /// Sparks off steel, blood from wounds, dust under galloping hooves and at a charge's impact.
    void Effects(float dt)
    {
        foreach (var (cue, at) in battle.Sounds)
            switch (cue)
            {
                case Cue.Clash: Spray(at, 1.3f, 6, new Color(255, 226, 140), 3.5f, 0.25f, 0.05f, true); break;
                case Cue.Block: Spray(at, 1.2f, 3, new Color(220, 200, 160), 2f, 0.2f, 0.05f, true); break;
                case Cue.Hit: case Cue.ArrowHit: Spray(at, 1.1f, 5, new Color(140, 20, 22), 1.8f, 0.55f, 0.06f, true); break;
                case Cue.Impact: Spray(at, 0.2f, 10, new Color(168, 150, 116), 1.6f, 1.3f, 0.25f, false, 0.6f); break;
            }
        GroundSteps(dt);
        MagicFx(dt);
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            var m = motes[i];
            m.Life -= dt;
            if (m.Life <= 0) { motes.RemoveAt(i); continue; }
            if (m.Heavy) m.Vel.Y -= 9.8f * dt; else m.Vel *= 1 - 1.5f * dt;
            m.Pos += m.Vel * dt;
            m.Pos.Y -= m.Fall * dt;
            m.Size += m.Grow * dt;
            float ground = battle.Terrain.Height(new Vector2(m.Pos.X, m.Pos.Z));
            if (m.Pos.Y < ground) { m.Pos.Y = ground + 0.01f; m.Vel = Vector3.Zero; }
            motes[i] = m;
        }
    }

    readonly float[] stepTimer = new float[4];

    /// What the ground does under moving men (docs/archive/TERRAIN.md T8): dust under galloping hooves, splashes at fords and in water,
    /// mud thrown up, leaves shaken down in woods; and near the camera, their sounds (splash, squelch, rustle, the bridge's beat).
    void GroundSteps(float dt)
    {
        var t = battle.Terrain;
        var ear = new Vector2(focusPoint.X, focusPoint.Z);
        float range = 25 + 30 * blend;
        Span<int> heard = stackalloc int[4]; // splash, squelch, rustle, hollow
        Span<Vector2> where = stackalloc Vector2[4];
        foreach (var u in battle.Units)
        {
            if (u.Routing && u.Active && !u.Mounted && fx.NextSingle() < dt * 1.5f && u.Vel.LengthSquared() > 4) // men running for their lives kick up dust
                Spray(u.Pos - u.Dir * 0.4f, 0.1f, 1, battle.Weather.Rain > 0.3f ? new Color(96, 82, 60) : new Color(176, 160, 124), 0.5f, 1.0f, 0.22f, false, 0.5f);
            float speed = MathF.Abs(u.Speed);
            if (!u.Active || speed < 1.2f) continue;
            var kind = t.GroundAt(u.Pos);
            bool wet = t.WaterDepth(u.Pos) > 0.05f, fast = speed > 5.5f;
            int cue = -1;
            if (wet)
            {
                cue = 0;
                if (fx.NextSingle() < dt * (u.Mounted ? 10 : 3) * MathF.Min(1, speed / 3))
                    Spray(u.Pos, 0.15f, u.Mounted ? 4 : 2, new Color(200, 214, 220), u.Mounted ? 2.6f : 1.6f, 0.6f, 0.07f, true);
            }
            else if (kind == GroundKind.Mud)
            {
                cue = 1;
                if (fx.NextSingle() < dt * (u.Mounted ? 9 : 2))
                    Spray(u.Pos - u.HeadingDir * 0.6f, 0.1f, u.Mounted ? 3 : 1, new Color(70, 54, 36), u.Mounted ? 2.2f : 1.2f, 0.7f, 0.08f, true);
            }
            else if (kind is GroundKind.Wood or GroundKind.DenseWood or GroundKind.Scrub)
            {
                cue = 2;
                if (kind != GroundKind.Scrub && fx.NextSingle() < dt * 0.8f)
                    Spray(u.Pos + new Vector2(fx.NextSingle() - 0.5f, fx.NextSingle() - 0.5f) * 2, 3.2f, 1,
                        fx.Next(3) == 0 ? new Color(150, 120, 50) : new Color(96, 120, 52), 0.5f, 2.6f, 0.09f, false, 0, 1.2f);
            }
            else if (kind == GroundKind.Bridge) cue = 3;
            else if (u.Mounted && fast && fx.NextSingle() < dt * 9)
                Spray(u.Pos - u.HeadingDir * 0.8f, 0.1f, 1, battle.Weather.Rain > 0.3f ? new Color(96, 82, 60) : new Color(176, 160, 124), 0.6f, 1.2f, 0.3f, false, 0.7f);
            if (cue >= 0 && Vector2.DistanceSquared(u.Pos, ear) < range * range) { heard[cue] += u.Mounted ? 3 : 1; where[cue] = u.Pos; }
        }
        var audio = host.Audio;
        if (audio == null || !audio.Enabled) return;
        var right = Matrix.Invert(view).Right;
        for (int i = 0; i < 4; i++)
        {
            stepTimer[i] -= dt;
            if (heard[i] == 0 || stepTimer[i] > 0) continue;
            stepTimer[i] = MathF.Max(0.12f, 0.6f - heard[i] * 0.02f) * (0.7f + fx.NextSingle() * 0.6f);
            var c = i switch { 0 => Cue.Splash, 1 => Cue.Squelch, 2 => Cue.Rustle, _ => Cue.Hollow };
            audio.PlayAt(c, new Vector3(where[i].X, t.Height(where[i]) + 0.5f, where[i].Y), eye, right, 45);
        }
    }

    /// Build every puppet's quads, billboarded toward the camera of `viewer` (the player's camera or the sun).
    void BuildSprites(Matrix viewer, bool forSun)
    {
        var t = battle.Terrain;
        sprites.N = 0;
        puppet.Begin(viewer);
        puppet.Now = battle.Time;
        puppet.Winner = battle.Winner;
        // A banner over every company, carried by its first man.
        foreach (var s in battle.Squads)
        {
            if (s.Company == null || s.Alive == 0) continue;
            if (deploying && !battle.Options.Scouted && s.Team == 1) continue;
            var bearer = s.Units.FirstOrDefault(u => u.Active && !u.Routing);
            if (!forSun) BannerFalls(s, bearer);
            if (bearer == null) continue;
            if (!battle.Shown(s)) { puppet.Banner(s.LastSeen, t.Height(s.LastSeen), bearer.Palette, 0, s.Company.Banner); continue; } // where you last saw it
            var at = bearer.Pos - bearer.Dir * 0.35f + new Vector2(-bearer.Dir.Y, bearer.Dir.X) * 0.25f;
            float stream = Math.Clamp((bearer.Mounted ? MathF.Abs(bearer.Speed) : bearer.Vel.Length()) / 6, 0, 1);
            puppet.Banner(at, t.Height(at) + (bearer.Mounted ? 1.1f : 0), bearer.Palette, battle.Time * 3 + s.Units.Count, s.Company.Banner, stream);
        }
        DrawFallingBanners();
        if (!forSun)
        {
            DrawMarks();
            DrawScars();
            foreach (var m in motes) puppet.Particle(m.Pos, m.Size * (m.Heavy ? 1 : MathF.Min(1, m.Life / m.Max * 2)), m.Col);
        }
        var eye2 = new Vector2(eye.X, eye.Z);
        // Men outside the view (or, for the sun's pass, outside the shadow map) are skipped; men far off
        // are drawn with fewer parts. Low quality brings the detail bands closer.
        var frustum = new BoundingFrustum(forSun && shading != null ? shading.LightViewProj : view * proj);
        float near = quality == Quality.Low ? 25 : 45, far = quality == Quality.Low ? 60 : 100;
        bool Seen(Vector2 p, float h, float r)
        {
            if (frustum.Contains(new BoundingSphere(new Vector3(p.X, h + 1, p.Y), r)) == ContainmentType.Disjoint) return false;
            float d2 = Vector2.DistanceSquared(p, eye2);
            puppet.Detail = forSun ? (d2 < near * near ? 0 : 1) : d2 < near * near ? 0 : d2 < far * far ? 1 : 2;
            return true;
        }
        foreach (var u in battle.Units)
        {
            if (u.Fled) continue;
            if (!forSun && !u.IsPlayer && u.Team == 0 && blend < 0.5f && Vector2.DistanceSquared(u.Pos, eye2) < 6 * 6) continue; // friends don't fill the lens
            if (deploying && !battle.Options.Scouted && u.Team == 1 && !u.IsLeader) continue; // unscouted: banners only
            if (u.Team == 1 && !u.IsLeader && !battle.Shown(u.Squad)) continue; // hidden by a crest or a wood
            float h = t.Height(u.Pos);
            puppet.Rear = RearOf(u, !forSun);
            if (!Seen(u.Pos, h, u.Mounted ? 2.5f : 1.5f)) continue;
            puppet.Draw(u, h);
        }
        puppet.Rear = 0;
        foreach (var h in battle.LooseHorses) if (Seen(h.Pos, t.Height(h.Pos), 2.5f)) puppet.DrawLooseHorse(h, t.Height(h.Pos));
        foreach (var h in battle.DeadHorses) if (Seen(h.Pos, t.Height(h.Pos), 2.5f)) puppet.DrawDeadHorse(h, t.Height(h.Pos));
        puppet.Detail = 0;
    }

    void SpriteStates()
    {
        var gd = GraphicsDevice;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
    }

    void DrawSpritesBasic()
    {
        if (sprites.N == 0) return;
        SpriteStates();
        GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
        spriteFx.View = view; spriteFx.Projection = proj;
        spriteFx.CurrentTechnique.Passes[0].Apply();
        GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, sprites.A, 0, sprites.N / 3);
    }

    void DrawSpritesShaded(Effect fx, string technique)
    {
        if (sprites.N == 0) return;
        SpriteStates();
        fx.Parameters["Atlas"].SetValue(art.Atlas);
        fx.CurrentTechnique = fx.Techniques[technique];
        fx.CurrentTechnique.Passes[0].Apply();
        GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, sprites.A, 0, sprites.N / 3);
    }

    void Line(Vector3 a, Vector3 b, Color c) { lines.Add(new(a, c)); lines.Add(new(b, c)); }
    Vector3 G(Vector2 p, float y = 0.08f) => new(p.X, battle.Terrain.Height(p) + y, p.Y);

    /// The last frame's draw load: sprite triangles, line segments, ground marks, units on the field (shots runs log it).
    public string RenderCost => $"{sprites.N / 3} sprite tris, {lines.N / 2} lines, {markCount} marks, {battle.Units.Count(u => u.Active)} men";

    readonly Dictionary<Squad, float> selectedAt = new();
    /// Orders just given: an arrow from each company to where it was sent, drawn out and then drawn in over a second (V7).
    readonly List<(Vector2 From, Vector2 To, float At)> arrows = new();

    void OrderArrows(Vector2 to) { foreach (var s in Targets()) arrows.Add((s.Centroid(), to, Ui.Now)); }

    void OrderArrowLines()
    {
        arrows.RemoveAll(a => Ui.Now - a.At > 1.1f || Ui.Now < a.At);
        foreach (var (from, to, at) in arrows)
        {
            float t = (Ui.Now - at) / 1.1f;
            var head = Vector2.Lerp(from, to, Ease.OutCubic(Math.Min(1, t / 0.35f)));
            var tail = Vector2.Lerp(from, to, Ease.In(Math.Max(0, (t - 0.45f) / 0.55f)));
            if (Vector2.DistanceSquared(head, tail) < 0.25f) continue;
            Line(G(tail, 0.2f), G(head, 0.2f), Sel);
            var d = Vector2.Normalize(head - tail); var n = new Vector2(-d.Y, d.X);
            Line(G(head, 0.2f), G(head - d * 1.6f + n, 0.2f), Sel);
            Line(G(head, 0.2f), G(head - d * 1.6f - n, 0.2f), Sel);
        }
    }

    void Ring(Vector2 p, float r, Color c, int seg = 12)
    {
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathF.Tau / seg, a1 = (i + 1) * MathF.Tau / seg;
            Line(G(p + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r), G(p + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r), c);
        }
    }

    /// The siege wall's torches light the night (none by day or without a wall): see `World.Torches`.
    Vector4 TorchRow()
    {
        if (battle.Terrain.Wall == null || Sky.Night <= 0.05f) return Vector4.Zero;
        float top = battle.Terrain.Height(new Vector2(World.TorchSpacing, 15.25f)) + 0.7f;
        return new Vector4(SiegeWall.Outer - 0.5f, top, Math.Clamp(Sky.Night * 1.8f, 0, 1), World.TorchSpacing);
    }

    /// Rain streaks (or snowflakes) in a box around the camera, falling on a fixed pattern so nothing is stored. From the high
    /// command camera a second, wider box falls round the point you look at, so the field itself is seen in the rain.
    void Precipitation()
    {
        var w = battle.Weather;
        if (w.Rain < 0.05f) return;
        // Rain is grey streaks, dimmer at night (bright white lines read as scratches on a dark field).
        var col = w.Snow ? new Color(236, 238, 244) : Color.Lerp(new Color(150, 162, 178), new Color(70, 80, 98), Sky.Night);
        Fall(eye, 25, (int)(1100 * w.Rain), 0, w, col);
        if (eye.Y - focusPoint.Y > 30) Fall(focusPoint + new Vector3(0, 8, 0), 60, (int)(1400 * w.Rain), 1, w, col);
    }

    /// Drops in a box of half-width `half` centred on `c`, on their own pattern (`salt`).
    void Fall(Vector3 c, float half, int n, int salt, Weather w, Color col)
    {
        float t = realTime, size = half * 2;
        for (int i = 0; i < n; i++)
        {
            uint h = (uint)(i + salt * 7919) * 2654435761u;
            float x = (h & 1023) / 1023f * size - half, z = (h >> 10 & 1023) / 1023f * size - half, ph = (h >> 20 & 1023) / 1023f;
            float fall = w.Snow ? 1.2f : 11f, y = 18 - (ph * 18 + t * fall) % 18;
            var p = new Vector3(MathF.Floor(c.X / size) * size + x, c.Y - 6 + y, MathF.Floor(c.Z / size) * size + z);
            if (p.X < c.X - half) p.X += size; if (p.X > c.X + half) p.X -= size;
            if (p.Z < c.Z - half) p.Z += size; if (p.Z > c.Z + half) p.Z -= size;
            var drift = w.Snow ? new Vector3(MathF.Sin(t + i) * 0.05f, -0.08f, 0) : new Vector3(0.04f, -0.45f, 0.015f);
            Line(p, p + drift, col);
        }
    }

    static readonly Color StakeWood = new(150, 118, 78);

    /// Planted stakes (they come mid-battle, after the field's meshes are built): sharpened poles leaning toward the enemy.
    void Stakes()
    {
        foreach (var f in battle.Terrain.Features)
        {
            if (f.Kind != FeatureKind.Stakes) continue;
            var d = f.B - f.A;
            float len = d.Length();
            if (len < 0.1f) continue;
            // Toward the far army: whoever planted them stands just behind.
            var far = Vector2.DistanceSquared(f.A, battle.Centroid(0)) > Vector2.DistanceSquared(f.A, battle.Centroid(1)) ? battle.Centroid(0) : battle.Centroid(1);
            var lean = new Vector2(-d.Y, d.X) / len;
            lean *= Vector2.Dot(lean, far - f.A) > 0 ? 0.55f : -0.55f;
            for (float s = 0; s <= len; s += 0.6f)
            {
                var p = f.A + d * (s / len);
                var top = G(p + lean, f.Height);
                Line(G(p, 0), top, StakeWood);
                Line(top, top + new Vector3(lean.X, 0.15f, lean.Y) * 0.3f, Color.LightGray); // the sharpened point
            }
        }
    }

    /// Spell colours by tradition: faith gold, craft violet, necromancy sick green, a companion's steel.
    static Color SpellColor(string tradition) => tradition switch
    {
        "faith" => new Color(250, 222, 130), "craft" => new Color(190, 130, 255), "necromancy" => new Color(140, 230, 120), _ => new Color(220, 228, 240),
    };

    /// Casts on their way (a circle of marks drawing itself round the spot, your warning to move) and spell fields (a slow ring at their edge).
    void Spells()
    {
        float t = battle.Time;
        foreach (var c in battle.Casts) Sigil(c); // the gathering (docs/archive/SPECTACLE.md S2)
        foreach (var f in battle.Fields)
        {
            var col = f.Effect?.Harmful == true ? new Color(240, 120, 80) : SpellColor(f.A.Tradition);
            float pulse = 0.96f + 0.04f * MathF.Sin((t - f.Started) * 2);
            Ring(f.Pos, f.Radius * pulse, col, 20);
        }
        AuraGlow();
        MagicLines();
    }

    /// The captain's aura (docs/archive/KNIGHT_ORDERS.md K1): the lit blade as a streak along the facing, aura armour as a shimmer.
    void AuraGlow()
    {
        var p = battle.Player;
        float t = battle.Time, pulse = 0.85f + 0.15f * MathF.Sin(t * 9);
        ChampionGlow(pulse);
        if (battle.AuraTier > 0 && p.Active) PlayerGlow(p, t, pulse);
        PendingFormsDrawn(t);
    }

    /// An enemy champion's aura (K6): his lit blade in a cold colour, his aura armour as a ring.
    void ChampionGlow(float pulse)
    {
        var c = battle.EnemyCaptain;
        if (battle.FoeTier <= 0 || !c.Active) return;
        var col = Color.Lerp(Style.Foe, Color.White, 0.35f * pulse);
        if (battle.FoeTier >= 2 && battle.FoeAura >= Battle.AuraLow && c.EnemyDist < 4)
        {
            float lift = c.Mounted ? 1.2f : 0;
            Line(G(c.Pos + c.Dir * 0.35f, 1.25f + lift), G(c.Pos + c.Dir * (c.Type.Reach + 0.3f), 1.0f + lift), col);
        }
        if (battle.FoeTier >= 3 && battle.FoeAura >= Battle.AuraLow) Ring(c.Pos, 0.9f * pulse, Color.Lerp(Style.Foe, Color.Black, 0.35f), 12);
    }

    void PlayerGlow(Unit p, float t, float pulse)
    {
        // The lit blade and its trail are drawn with the rest of the magic (BattleScreen.Magic.cs).
        var aura = AuraColour;
        if (battle.AuraTier >= 3 && !battle.Spent)
        {
            Ring(p.Pos, 0.9f * pulse, Color.Lerp(aura, Color.Black, 0.35f), 12);
            Ring(p.Pos, 0.7f * pulse, Color.Lerp(aura, Color.Black, 0.55f), 10); // a shimmer, two rings breathing out of step
        }
        if (battle.Guarding) Ring(p.Pos + p.Dir * 0.7f, 0.6f, Color.White, 8);
        if (battle.AuraTier >= 4 && !battle.Spent) Ring(p.Pos, Battle.PresenceRadius * (0.96f + 0.04f * MathF.Sin(t * 2)), Color.Lerp(aura, Color.Black, 0.6f), 40);
    }

    /// Thrown forms on their way, yours and a champion's (K2, K6).
    void PendingFormsDrawn(float t)
    {
        // Thrown forms on their way (K2): a slash as a fan of streaks closing ahead, a volley as a ring on its spot, a shot as a line.
        foreach (var pf in battle.PendingForms)
        {
            float k = Math.Clamp((t - pf.Started) / MathF.Max(0.05f, pf.At - pf.Started), 0, 1);
            var col = Color.Lerp(pf.Foe ? Style.Foe : Style.Aura, Color.White, k);
            var side = new Vector2(-pf.Dir.Y, pf.Dir.X);
            switch (pf.F.Kind)
            {
                case "volley": Ring(pf.From + pf.Dir * pf.F.Range, pf.F.Radius * (1.5f - 0.5f * k), col, 16); break;
                case "shot": Line(G(pf.From, 1.3f), G(pf.From + pf.Dir * pf.F.Range, 1.0f), col); break;
                default:
                    float reach = pf.F.Range * k, w = pf.F.Radius + reach * 0.25f;
                    var tip = pf.From + pf.Dir * reach;
                    Line(G(tip - side * w, 1.0f), G(tip + side * w, 1.0f), col);
                    Line(G(pf.From, 1.1f), G(tip, 1.0f), Color.Lerp(col, Color.Black, 0.4f));
                    break;
            }
        }
    }

    static readonly Color RouteGilt = new(244, 214, 120);

    /// The order's way round as a dotted line on the ground: short dashes every other step.
    void RouteDots()
    {
        if (route == null) return;
        for (int i = 0; i + 1 < route.Count; i += 2)
        {
            var a = route[i];
            var b = Vector2.Lerp(route[i], route[i + 1], 0.6f);
            Line(G(a, 0.25f), G(b, 0.25f), RouteGilt);
        }
    }

    /// A company's own plan on the ground (docs/archive/MELEE.md D7): swinging onto a flank, a dashed hook to where its front will
    /// stand; wrapping, a dashed hook forward from each end of its line.
    void PlanHook(Squad s)
    {
        if (s.Plan != Plan.Flank || s.Foe is not { Alive: > 0 } foe || s.Alive == 0) return;
        void Dashes(Vector2 a, Vector2 b, Vector2 c)
        {
            // A curve a -> c bending through b, every other step drawn; an arrowhead at c.
            Vector2 At(float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;
            for (int i = 0; i < 10; i += 2) Line(G(At(i / 10f), 0.25f), G(At((i + 1) / 10f), 0.25f), RouteGilt);
            var d = c - At(0.9f);
            if (d.LengthSquared() < 1e-4f) return;
            d = Vector2.Normalize(d);
            var n = new Vector2(-d.Y, d.X);
            Line(G(c, 0.25f), G(c - d * 1.2f + n * 0.7f, 0.25f), RouteGilt);
            Line(G(c, 0.25f), G(c - d * 1.2f - n * 0.7f, 0.25f), RouteGilt);
        }
        if (s.Bend > 0)
        {
            var rt = new Vector2(-s.Front.Y, s.Front.X);
            float half = (Math.Clamp(s.Columns, 1, Math.Max(1, s.Alive)) - 1) / 2f * s.Spacing;
            foreach (float side in new[] { -1f, 1f })
            {
                var end = s.Anchor + rt * side * half;
                Dashes(end, end + s.Front * 3, end + s.Front * 3.5f - rt * side * 2.5f);
            }
            return;
        }
        var (at, face) = Battle.FlankPoint(s, foe, 1.5f);
        Dashes(s.FrontPos, Vector2.Lerp(s.FrontPos, at, 0.5f) - face * 4, at + face * 1.5f);
    }

    void DrawLines()
    {
        lines.N = 0;
        Precipitation();
        foreach (var a in battle.Arrows)
        {
            var dir = a.Vel.LengthSquared() > 0 ? Vector3.Normalize(a.Vel) : Vector3.Down;
            Line(a.Pos, a.Pos - dir * (a.Stuck >= 0 ? 0.5f : 0.8f), ArrowColor);
            if (a.Stuck < 0 && quality != Quality.Low) Line(a.Pos - dir * 0.8f, a.Pos - dir * 2.4f, ArrowStreak); // a faint streak behind it in flight
        }
        Stakes();
        Spells();
        RouteDots();
        foreach (var s in selected) PlanHook(s);
        foreach (var s in selected)
        {
            // A company just chosen: its rings swell once, so you see which it was.
            if (!selectedAt.TryGetValue(s, out float at)) selectedAt[s] = at = Ui.Now;
            float swell = 1 + 0.7f * Ease.Kick((Ui.Now - at) / 0.45f);
            foreach (var u in s.Units)
                if (u.Active) Ring(u.Pos, (u.Mounted ? 1.05f : 0.45f) * swell, Sel, 8);
        }
        foreach (var gone in selectedAt.Keys.Where(k => !selected.Contains(k)).ToList()) selectedAt.Remove(gone);
        OrderArrowLines();
        if (command)
        {
            if (battle.Player.Active) Ring(battle.Player.Pos, 0.8f, Gold);
            if (battle.EnemyCaptain.Active) Ring(battle.EnemyCaptain.Pos, 0.8f, Foe);
            foreach (var s in battle.Commanded)
            {
                if (s.Alive == 0) continue;
                if (s.Order == Order.Charge && s.Target is { Alive: > 0 } tgt)
                {
                    Line(G(s.Centroid(), 0.15f), G(tgt.Centroid(), 0.15f), Foe); // attack order
                    continue;
                }
                if (s.Order != Order.Hold) continue;
                // Front-rank marker with a facing tick.
                var side = new Vector2(-s.Front.Y, s.Front.X) * (Math.Min(s.Columns, s.Alive) - 1) * s.Spacing / 2;
                var c = selected.Contains(s) ? Sel : Friendly;
                Line(G(s.Anchor - side + s.Front), G(s.Anchor + side + s.Front), c);
                Line(G(s.Anchor + s.Front), G(s.Anchor + s.Front * 2.5f), c);
            }
            if (!hud.Over(ms.Position) && PickSquad(mouseGround, 1) is Squad hover)
                foreach (var u in hover.Units) if (u.Active && !u.Routing) Ring(u.Pos, u.Mounted ? 1.1f : 0.5f, Foe, 8);
            if (dragStart is Vector2 d)
            {
                // Formation preview: where every man will stand.
                var enemy = battle.Centroid(1);
                bool click = Vector2.Distance(d, mouseGround) < 2;
                var (a, b) = click ? LineEnds(mouseGround, enemy) : (d, mouseGround);
                Line(G(a, 0.12f), G(b, 0.12f), Color.White);
                foreach (var slot in PreviewSlots(a, b, enemy)) Ring(slot, 0.3f, Color.White * 0.8f, 6);
            }
            Ring(mouseGround, 0.4f, Color.White);
        }
        if (lines.N == 0) return;
        var gd = GraphicsDevice;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        lineFx.View = view; lineFx.Projection = proj;
        lineFx.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.LineList, lines.A, 0, lines.N / 2);
    }
}
