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
            puppet.Soft = null; // no soft pass without the shaders: dust stays in plain motes
            BuildSprites(view, false);
            puppet.Soft = soft;
            DrawSpritesBasic();
            DrawLines();
        }
        SyncHud();
        if (!hudHidden)
        {
            hud.Draw();
            if (TipsHeld) return;
            tipUi.Begin(gd.Viewport, ms.Position, false);
            BeatCaption(tipUi);
            Tips.Draw(tipUi, tipUi.U(142));
            tipUi.End();
        }
    }

    /// The shader path: sun shadow map, then the scene into an off-screen target, then the post pass to the screen.
    void DrawShaded(Shading sh)
    {
        var gd = GraphicsDevice;
        bool shadows = quality == Quality.High;
        sh.CutEye = new Vector4(eye, Vector2.Distance(new(eye.X, eye.Z), new(focusPoint.X, focusPoint.Z)) * MathHelper.SmoothStep(0, 1, blend)); // set before the sun's pass: thinned trees cast no shadow
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
        FieldLifeMesh();
        world.DrawShaded(sh, view, proj, eye, realTime, shadows);
        sh.GroundMap = null; sh.NearDetail = false; sh.Cut = 0; sh.CutEye = Vector4.Zero;
        DrawBlobs(shadows ? 0.4f : 1); // with real shadows the blobs only add contact darkening
        BuildSprites(view, false);
        sh.Setup(sh.Sprite, view * proj, eye, realTime, shadows, 0.45f);
        DrawSpritesShaded(sh.Sprite, "Sprite");
        DrawSoft(sh.Sprite);
        sh.Torch = Vector4.Zero;
        Array.Clear(sh.Glow); // the lights are this battle's alone (the map shares the shaders)
        DrawLines();
        var (awe, moment) = PostMoment();
        sh.PostToScreen(fxaa: Settings.Current.Fxaa, Sky.Grade, host.Quality == Quality.High ? 0.5f : 0, awe, moment, warpNow);
    }

    readonly BoundingFrustum spriteFrustum = new(Matrix.Identity);
    readonly byte[] corpseGrid = new byte[512 * 512];
    readonly List<int> corpseTouched = new();
    readonly List<Squad> goneSelected = new();

    void DrawBlobs(float alpha)
    {
        shadows.N = 0;
        var t = battle.Terrain;
        spriteFrustum.Matrix = view * proj;
        var eye2 = new Vector2(eye.X, eye.Z);
        float maxD2 = alpha < 0.5f ? 75f * 75f : float.MaxValue;
        void Blob(Vector2 p, float r)
        {
            if (Vector2.DistanceSquared(p, eye2) > maxD2) return;
            float h0 = t.Height(p);
            if (spriteFrustum.Contains(new BoundingSphere(new Vector3(p.X, h0 + 0.5f, p.Y), r + 0.5f)) == ContainmentType.Disjoint) return;
            if (Vector2.DistanceSquared(p, eye2) > 35f * 35f)
            {
                float y = h0 + 0.05f;
                shadows.Add(new(new(p.X - r, y, p.Y - r), new(0, 0))); shadows.Add(new(new(p.X + r, y, p.Y - r), new(1, 0))); shadows.Add(new(new(p.X - r, y, p.Y + r), new(0, 1)));
                shadows.Add(new(new(p.X + r, y, p.Y - r), new(1, 0))); shadows.Add(new(new(p.X + r, y, p.Y + r), new(1, 1))); shadows.Add(new(new(p.X - r, y, p.Y + r), new(0, 1)));
                return;
            }
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
        shadows.Draw(gd, PrimitiveType.TriangleList, shadows.N / 3);
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
                case Cue.Clash: Spray(at, 1.3f, 7, new Color(255, 226, 140), 3.5f, 0.3f, 0.07f, true); Spray(at, 1.3f, 1, new Color(255, 236, 190), 0.3f, 0.14f, 0.3f, false, 2.5f); break; // sparks and a flash
                case Cue.Block: Spray(at, 1.2f, 3, new Color(220, 200, 160), 2f, 0.2f, 0.05f, true); break;
                case Cue.Hit: case Cue.ArrowHit: Spray(at, 1.1f, 6, new Color(140, 20, 22), 1.9f, 0.55f, 0.075f, true); break;
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
            if (m.Pos.Y < ground) { m.Pos.Y = ground + 0.01f; m.Vel = Vector3.Zero; if (m.Heavy) m.Life = MathF.Min(m.Life, 0.08f); } // a drop or spark that lands is gone (blood stays as the ground mark)
            motes[i] = m;
        }
    }

    readonly float[] stepTimer = new float[6];

    /// What the ground does under moving men (docs/archive/TERRAIN.md T8): dust under galloping hooves, splashes at fords and in water,
    /// mud thrown up, leaves shaken down in woods; and near the camera, their sounds (splash, squelch, rustle, bridge, snow, gravel).
    void GroundSteps(float dt)
    {
        var t = battle.Terrain;
        var ear = new Vector2(focusPoint.X, focusPoint.Z);
        float range = 25 + 30 * blend;
        Span<int> heard = stackalloc int[6]; // splash, squelch, rustle, hollow, snow, gravel
        Span<Vector2> where = stackalloc Vector2[6];
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
            else if (battle.Weather.Snow) cue = 4;
            else if (kind is GroundKind.Road or GroundKind.Street or GroundKind.Stony) cue = 5;
            else if (u.Mounted && fast && fx.NextSingle() < dt * 9)
                Spray(u.Pos - u.HeadingDir * 0.8f, 0.1f, 1, battle.Weather.Rain > 0.3f ? new Color(96, 82, 60) : new Color(176, 160, 124), 0.6f, 1.2f, 0.3f, false, 0.7f);
            if (cue >= 0 && Vector2.DistanceSquared(u.Pos, ear) < range * range) { heard[cue] += u.Mounted ? 3 : 1; where[cue] = u.Pos; }
        }
        var audio = host.Audio;
        if (audio == null || !audio.Enabled) return;
        var right = Matrix.Invert(view).Right;
        for (int i = 0; i < 6; i++)
        {
            stepTimer[i] -= dt;
            if (heard[i] == 0 || stepTimer[i] > 0) continue;
            stepTimer[i] = MathF.Max(0.12f, 0.6f - heard[i] * 0.02f) * (0.7f + fx.NextSingle() * 0.6f);
            var c = i switch { 0 => Cue.Splash, 1 => Cue.Squelch, 2 => Cue.Rustle, 3 => Cue.Hollow, 4 => Cue.SnowCrunch, _ => Cue.GravelStep };
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
            Unit? bearer = null;
            foreach (var u in s.Units) if (u.Active && !u.Routing) { bearer = u; break; }
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
            soft.N = 0;
            foreach (var m in motes)
                if (!m.Heavy && (m.Size >= 0.15f || m.Grow >= 0.3f)) // dust and smoke: a soft puff that swells and thins away
                    puppet.Haze(m.Pos, m.Size * 1.15f, m.Col, (m.Size > 1.5f ? 0.24f : 0.55f) * MathF.Min(1, m.Life / m.Max * 1.6f), m.Max * 37 + m.Grow * 11 + m.Pos.X);
                else puppet.Particle(m.Pos, m.Size * (m.Heavy ? 1 : MathF.Min(1, m.Life / m.Max * 2)), m.Col);
        }
        var eye2 = new Vector2(eye.X, eye.Z);
        // Men outside the view (or, for the sun's pass, outside the shadow map) are skipped; men far off
        // are drawn with fewer parts. Low quality brings the detail bands closer.
        spriteFrustum.Matrix = forSun && shading != null ? shading.LightViewProj : view * proj;
        float near = quality == Quality.Low ? 25 : 45, far = quality == Quality.Low ? 60 : 100;
        bool Seen(Vector2 p, float h, float r)
        {
            if (spriteFrustum.Contains(new BoundingSphere(new Vector3(p.X, h + 1, p.Y), r)) == ContainmentType.Disjoint) return false;
            float d2 = Vector2.DistanceSquared(p, eye2);
            puppet.Detail = forSun ? (d2 < 20 * 20 ? 0 : d2 < 45 * 45 ? 1 : 2) : d2 < near * near ? 0 : d2 < far * far ? 1 : 2;
            return true;
        }
        foreach (int idx in corpseTouched) corpseGrid[idx] = 0;
        corpseTouched.Clear();
        foreach (var u in battle.Units)
        {
            if (u.Fled) continue;
            if (deploying && !battle.Options.Scouted && u.Team == 1 && !u.IsLeader) continue; // unscouted: banners only
            if (u.Team == 1 && !u.IsLeader && !battle.Shown(u.Squad)) continue; // hidden by a crest or a wood
            float h = t.Height(u.Pos);
            if (!forSun && !u.IsPlayer && u.Team == 0 && blend < 0.5f && LensFill(u.Pos, h)) continue; // friends don't fill the lens
            float rear = RearOf(u, !forSun);
            if (!Seen(u.Pos, h + (u.Dead ? 0 : u.Altitude), MathF.Max(u.Mounted ? 2.5f : 1.5f, u.Type.Size * (u.Type.Flying ? 3.5f : 1.5f)))) continue; // a wyvern on the wing, a huge beast's bulk
            puppet.Rear = rear;
            if (u.Dead && battle.Time - u.DiedAt > 1f)
            {
                int cx = Math.Clamp((int)MathF.Floor(u.Pos.X / 0.9f) + 256, 0, 511);
                int cz = Math.Clamp((int)MathF.Floor(u.Pos.Y / 0.9f) + 256, 0, 511);
                int cidx = cz * 512 + cx;
                byte seen = corpseGrid[cidx];
                if (seen == 0) corpseTouched.Add(cidx);
                if (seen < 255) corpseGrid[cidx] = (byte)(seen + 1);
                puppet.SparseCorpse = seen >= 2;
            }
            else puppet.SparseCorpse = false;
            puppet.Draw(u, h);
        }
        puppet.SparseCorpse = false;
        puppet.Rear = 0;
        if (!forSun) DrawMissiles(); // real arrows near the camera (BattleScreen.Arrows.cs)
        foreach (var h in battle.LooseHorses) { float hh = t.Height(h.Pos); if (Seen(h.Pos, hh, 2.5f)) puppet.DrawLooseHorse(h, hh); }
        foreach (var h in battle.DeadHorses) { float hh = t.Height(h.Pos); if (Seen(h.Pos, hh, 2.5f)) puppet.DrawDeadHorse(h, hh); }
        puppet.Detail = 0;
    }

    /// A man so close to the field camera that his body would fill the lens. Only these are left out:
    /// the camera stands a few metres behind the captain, so a wider ring hid the ranks around him.
    bool LensFill(Vector2 p, float h) => Vector3.DistanceSquared(new Vector3(p.X, h + 1, p.Y), eye) < LensRing * LensRing;
    const float LensRing = 1.5f;

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
        sprites.Draw(GraphicsDevice, PrimitiveType.TriangleList, sprites.N / 3);
    }

    void DrawSpritesShaded(Effect fx, string technique)
    {
        if (sprites.N == 0) return;
        SpriteStates();
        fx.Parameters["Atlas"].SetValue(art.Atlas);
        fx.CurrentTechnique = fx.Techniques[technique];
        fx.CurrentTechnique.Passes[0].Apply();
        sprites.Draw(GraphicsDevice, PrimitiveType.TriangleList, sprites.N / 3);
    }

    /// Dust and smoke over the men: blended, depth-tested but not written, so puffs overlap softly.
    void DrawSoft(Effect fx)
    {
        if (soft.N == 0) return;
        var gd = GraphicsDevice;
        gd.BlendState = BlendState.AlphaBlend; // the shader writes premultiplied colour
        gd.DepthStencilState = DepthStencilState.DepthRead;
        gd.RasterizerState = RasterizerState.CullNone;
        fx.CurrentTechnique = fx.Techniques["SpriteSoft"];
        fx.CurrentTechnique.Passes[0].Apply();
        soft.Draw(gd, PrimitiveType.TriangleList, soft.N / 3);
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
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

    void Ring(Vector2 p, float r, Color c, int seg = 12, float width = 1)
    {
        seg = Math.Max(seg, Math.Min(64, (int)(r * 3))); // a wide ring stays round
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathF.Tau / seg, a1 = (i + 1) * MathF.Tau / seg;
            Band(G(p + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r), G(p + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r), c, width);
        }
    }

    /// Broken arc segments around a circle on the ground (magical field boundaries without hard neon hoops).
    void DashedRing(Vector2 p, float r, Color c, int dashes = 14, float width = 0.75f)
    {
        dashes = Math.Max(dashes, Math.Min(40, (int)(r * 2)));
        float step = MathF.Tau / dashes;
        for (int i = 0; i < dashes; i++)
        {
            float a0 = i * step, a1 = a0 + step * 0.55f;
            Band(G(p + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r), G(p + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r), c, width);
        }
    }

    /// A mark painted on the ground (rings, order lines): a ribbon a few pixels wide at any distance, its edges a little
    /// see-through, instead of a one-pixel line that reads as an editor gizmo. Drawn blended after the lines.
    void Band(Vector3 a, Vector3 b, Color c, float width = 1)
    {
        var d = new Vector2(b.X - a.X, b.Z - a.Z);
        if (d.LengthSquared() < 1e-6f) return;
        d = Vector2.Normalize(d);
        float W(Vector3 p) => Math.Clamp(Vector3.Distance(p, eye) * 0.0028f, 0.04f, 0.5f) * width;
        Vector3 Side(Vector3 p) => new Vector3(-d.Y, 0, d.X) * W(p);
        var core = c * 0.85f; // premultiplied: a touch of the ground shows through
        var edge = c * 0.0f;
        Vector3 sa = Side(a), sb = Side(b);
        // Three strips across: soft edge, solid core, soft edge.
        void Strip(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Color c0, Color c1)
        {
            bands.Add(new(a0, c0)); bands.Add(new(b0, c0)); bands.Add(new(a1, c1));
            bands.Add(new(b0, c0)); bands.Add(new(b1, c1)); bands.Add(new(a1, c1));
        }
        Strip(a - sa, a - sa * 0.35f, b - sb, b - sb * 0.35f, edge, core);
        Strip(a - sa * 0.35f, a + sa * 0.35f, b - sb * 0.35f, b + sb * 0.35f, core, core);
        Strip(a + sa * 0.35f, a + sa, b + sb * 0.35f, b + sb, core, edge);
    }

    /// Camera-facing translucent energy ribbon with soft feathered edges (premultiplied alpha), for magic beams, lightning and light trails.
    void BeamQuad(Vector3 a, Vector3 b, float wa, float wb, Color ca, Color cb)
    {
        var axis = b - a;
        if (axis.LengthSquared() < 1e-8f) return;
        var mid = (a + b) * 0.5f;
        var side = Vector3.Cross(axis, eye - mid);
        if (side.LengthSquared() < 1e-8f) side = Vector3.Cross(axis, Vector3.UnitZ);
        if (side.LengthSquared() < 1e-8f) side = Vector3.UnitX;
        side = Vector3.Normalize(side);
        Vector3 sa = side * wa, sb = side * wb;
        Color ea = ca * 0f, eb = cb * 0f;
        void Strip(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Color ac0, Color ac1, Color bc0, Color bc1)
        {
            bands.Add(new(a0, ac0)); bands.Add(new(b0, bc0)); bands.Add(new(a1, ac1));
            bands.Add(new(b0, bc0)); bands.Add(new(b1, bc1)); bands.Add(new(a1, ac1));
        }
        Strip(a - sa, a - sa * 0.32f, b - sb, b - sb * 0.32f, ea, ca, eb, cb);
        Strip(a - sa * 0.32f, a + sa * 0.32f, b - sb * 0.32f, b + sb * 0.32f, ca, ca, cb, cb);
        Strip(a + sa * 0.32f, a + sa, b + sb * 0.32f, b + sb, ca, ea, cb, eb);
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
            if (f.Effect?.Id is "mist" or "smoke") continue; // mist and smoke banks read through their haze, not a drawn hoop
            var col = f.Effect?.Harmful == true ? new Color(235, 115, 75) : SpellColor(f.A.Tradition);
            float pulse = 0.96f + 0.04f * MathF.Sin((t - f.Started) * 2);
            DashedRing(f.Pos, f.Radius * pulse, col * 0.48f, 16, 0.72f);
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
            BeamQuad(G(c.Pos + c.Dir * 0.35f, 1.25f + lift), G(c.Pos + c.Dir * (c.Type.Reach + 0.3f), 1.0f + lift), 0.12f, 0.06f, col * 0.85f, Color.White * 0.9f);
        }
        if (battle.FoeTier >= 3 && battle.FoeAura >= Battle.AuraLow) Ring(c.Pos, 0.9f * pulse, Style.Foe * 0.55f, 12, 0.8f);
    }

    void PlayerGlow(Unit p, float t, float pulse)
    {
        // The lit blade and its trail are drawn with the rest of the magic (BattleScreen.Magic.cs).
        var aura = AuraColour;
        if (battle.AuraTier >= 3 && !battle.Spent)
        {
            Ring(p.Pos, 0.9f * pulse, aura * 0.55f, 12, 0.75f);
            Ring(p.Pos, 0.7f * pulse, aura * 0.35f, 10, 0.65f); // a shimmer, two rings breathing out of step
        }
        if (battle.Guarding) Ring(p.Pos + p.Dir * 0.7f, 0.6f, Color.White * 0.8f, 8);
        if (battle.AuraTier >= 4 && !battle.Spent) DashedRing(p.Pos, Battle.PresenceRadius * (0.96f + 0.04f * MathF.Sin(t * 2)), aura * 0.35f, 24, 0.7f);
    }

    /// Thrown forms on their way, yours and a champion's (K2, K6).
    void PendingFormsDrawn(float t)
    {
        // Thrown forms on their way (K2): a slash as a fan of streaks closing ahead, a volley as a ring on its spot, a shot as a glowing bolt.
        foreach (var pf in battle.PendingForms)
        {
            float k = Math.Clamp((t - pf.Started) / MathF.Max(0.05f, pf.At - pf.Started), 0, 1);
            var col = Color.Lerp(pf.Foe ? Style.Foe : Style.Aura, Color.White, k);
            var side = new Vector2(-pf.Dir.Y, pf.Dir.X);
            switch (pf.F.Kind)
            {
                case "volley": Ring(pf.From + pf.Dir * pf.F.Range, pf.F.Radius * (1.5f - 0.5f * k), col * 0.7f, 16); break;
                case "shot": BeamQuad(G(pf.From, 1.3f), G(pf.From + pf.Dir * pf.F.Range, 1.0f), 0.14f, 0.07f, col * 0.75f, Color.White * 0.9f); break;
                default:
                    float reach = pf.F.Range * k, w = pf.F.Radius + reach * 0.25f;
                    var tip = pf.From + pf.Dir * reach;
                    BeamQuad(G(tip - side * w, 1.0f), G(tip + side * w, 1.0f), 0.14f, 0.14f, col * 0.7f, col * 0.7f);
                    BeamQuad(G(pf.From, 1.1f), G(tip, 1.0f), 0.12f, 0.22f, col * 0.25f, col * 0.65f);
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
        bands.N = 0;
        Precipitation();
        MissileLines(); // arrows, bolts, javelins and stones (BattleScreen.Arrows.cs)
        Stakes();
        Spells();
        if (!hudHidden)
        {
            RouteDots();
            foreach (var s in selected) PlanHook(s);
            foreach (var s in selected)
            {
                // A company just chosen: its rings swell once, so you see which it was.
                if (!selectedAt.TryGetValue(s, out float at)) selectedAt[s] = at = Ui.Now;
                float swell = 1 + 0.7f * Ease.Kick((Ui.Now - at) / 0.45f);
                foreach (var u in s.Units)
                    if (u.Active) Ring(u.Pos, (u.Mounted ? 1.05f : 0.45f) * swell, Sel * 0.85f, 8);
            }
            if (selectedAt.Count > 0)
            {
                goneSelected.Clear();
                foreach (var k in selectedAt.Keys) if (!selected.Contains(k)) goneSelected.Add(k);
                foreach (var k in goneSelected) selectedAt.Remove(k);
            }
            OrderArrowLines();
            if (command)
            {
                if (battle.Player.Active) Ring(battle.Player.Pos, 0.75f, Gold * 0.65f, 12, 0.75f);
                if (battle.EnemyCaptain.Active) Ring(battle.EnemyCaptain.Pos, 0.75f, Foe * 0.65f, 12, 0.75f);
                foreach (var s in battle.Commanded)
                {
                    if (s.Alive == 0) continue;
                    // Queued orders (docs/archive/COMBAT_QOL.md Q3): a line from step to step, a tick at each place.
                    var at = s.Order == Order.Hold ? s.Anchor : s.Centroid();
                    foreach (var st in s.Queue)
                    {
                        var to = st.Target >= 0 && st.Target < battle.Squads.Count ? battle.Squads[st.Target].Centroid() : st.Anchor;
                        var qc = (selected.Contains(s) ? Sel : Friendly) * 0.7f;
                        Band(G(at, 0.2f), G(to, 0.2f), st.Target >= 0 ? Foe * 0.7f : qc, 0.8f);
                        Band(G(to, 0.2f), G(to + st.Front * 2, 0.2f), qc, 0.8f);
                        at = to;
                    }
                    if (s.Order == Order.Charge && s.Target is { Alive: > 0 } tgt)
                    {
                        Band(G(s.Centroid(), 0.15f), G(tgt.Centroid(), 0.15f), Foe * 0.65f, 0.8f); // attack order
                        continue;
                    }
                    if (s.Order != Order.Hold) continue;
                    // Front-rank bracket ticks with a compact center chevron.
                    float halfW = Math.Max(0.8f, (Math.Min(s.Columns, s.Alive) - 1) * s.Spacing / 2);
                    var rt = new Vector2(-s.Front.Y, s.Front.X);
                    bool isSel = selected.Contains(s);
                    var c = (isSel ? Sel : Friendly) * (isSel ? 0.78f : 0.42f);
                    var lineL = s.Anchor - rt * halfW + s.Front;
                    var lineR = s.Anchor + rt * halfW + s.Front;
                    float tick = Math.Min(1.2f, halfW * 0.3f);
                    Band(G(lineL), G(lineL + rt * tick), c, 0.7f);
                    Band(G(lineL), G(lineL - s.Front * 0.45f), c, 0.7f);
                    Band(G(lineR), G(lineR - rt * tick), c, 0.7f);
                    Band(G(lineR), G(lineR - s.Front * 0.45f), c, 0.7f);
                    var tip = s.Anchor + s.Front * 1.55f;
                    Band(G(tip - s.Front * 0.5f - rt * 0.45f), G(tip), c, 0.75f);
                    Band(G(tip - s.Front * 0.5f + rt * 0.45f), G(tip), c, 0.75f);
                }
                if (!hud.Over(ms.Position) && PickSquad(mouseGround, 1) is Squad hover)
                    foreach (var u in hover.Units) if (u.Active && !u.Routing) Ring(u.Pos, u.Mounted ? 1.1f : 0.5f, Foe * 0.8f, 8);
                if (dragStart is Vector2 d)
                {
                    // Formation preview: where every man will stand.
                    var enemy = battle.Centroid(1);
                    bool click = Vector2.Distance(d, mouseGround) < 2;
                    var (a, b) = click ? LineEnds(mouseGround, enemy) : (d, mouseGround);
                    Band(G(a, 0.12f), G(b, 0.12f), Color.White * 0.8f);
                    foreach (var slot in PreviewSlots(a, b, enemy)) Ring(slot, 0.3f, Color.White * 0.7f, 6, 0.7f);
                }
                Ring(mouseGround, 0.35f, Color.White * 0.55f, 10, 0.65f);
            }
        }
        var gd = GraphicsDevice;
        lineFx.View = view; lineFx.Projection = proj;
        if (lines.N > 0)
        {
            gd.BlendState = BlendState.Opaque;
            gd.DepthStencilState = DepthStencilState.Default;
            lineFx.CurrentTechnique.Passes[0].Apply();
            lines.Draw(gd, PrimitiveType.LineList, lines.N / 2);
        }
        if (bands.N > 0)
        {
            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.DepthRead;
            gd.RasterizerState = RasterizerState.CullNone;
            lineFx.CurrentTechnique.Passes[0].Apply();
            bands.Draw(gd, PrimitiveType.TriangleList, bands.N / 3);
            gd.BlendState = BlendState.Opaque;
            gd.DepthStencilState = DepthStencilState.Default;
        }
    }
}
