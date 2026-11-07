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
        world.DrawShaded(sh, view, proj, eye, realTime, shadows);
        DrawBlobs(shadows ? 0.4f : 1); // with real shadows the blobs only add contact darkening
        BuildSprites(view, false);
        sh.Setup(sh.Sprite, view * proj, eye, realTime, shadows, 0.45f);
        DrawSpritesShaded(sh.Sprite, "Sprite");
        DrawLines();
        sh.PostToScreen(fxaa: Settings.Current.Fxaa);
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

    struct Mote { public Vector3 Pos, Vel; public float Life, Max, Size, Grow; public Color Col; public bool Heavy; }
    readonly List<Mote> motes = new();
    readonly Random fx = new(5);

    void Spray(Vector2 at, float height, int n, Color col, float speed, float life, float size, bool heavy, float grow = 0)
    {
        if (motes.Count > 1500) return;
        var g = battle.Terrain.Height(at);
        for (int i = 0; i < n; i++)
        {
            var dir = new Vector3(fx.NextSingle() * 2 - 1, fx.NextSingle() * 0.8f + (heavy ? 0.4f : 0.1f), fx.NextSingle() * 2 - 1);
            motes.Add(new Mote { Pos = new Vector3(at.X, g + height, at.Y), Vel = dir * speed, Life = life * (0.6f + fx.NextSingle() * 0.6f), Max = life,
                Size = size, Grow = grow, Col = col, Heavy = heavy });
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
        foreach (var u in battle.Units)
            if (u.Active && u.Mounted && u.Speed > 5.5f && fx.NextSingle() < dt * 9 && battle.Terrain.WaterDepth(u.Pos) == 0)
                Spray(u.Pos - u.HeadingDir * 0.8f, 0.1f, 1, battle.Weather.Rain > 0.3f ? new Color(96, 82, 60) : new Color(176, 160, 124), 0.6f, 1.2f, 0.3f, false, 0.7f);
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            var m = motes[i];
            m.Life -= dt;
            if (m.Life <= 0) { motes.RemoveAt(i); continue; }
            if (m.Heavy) m.Vel.Y -= 9.8f * dt; else m.Vel *= 1 - 1.5f * dt;
            m.Pos += m.Vel * dt;
            m.Size += m.Grow * dt;
            float ground = battle.Terrain.Height(new Vector2(m.Pos.X, m.Pos.Z));
            if (m.Pos.Y < ground) { m.Pos.Y = ground + 0.01f; m.Vel = Vector3.Zero; }
            motes[i] = m;
        }
    }

    /// Build every puppet's quads, billboarded toward the camera of `viewer` (the player's camera or the sun).
    void BuildSprites(Matrix viewer, bool forSun)
    {
        var t = battle.Terrain;
        sprites.N = 0;
        puppet.Begin(viewer);
        puppet.Now = battle.Time;
        // A banner over every company, carried by its first man.
        foreach (var s in battle.Squads)
        {
            if (s.Company == null || s.Alive == 0) continue;
            if (deploying && !battle.Options.Scouted && s.Team == 1) continue;
            var bearer = s.Units.FirstOrDefault(u => u.Active && !u.Routing);
            if (bearer == null) continue;
            var at = bearer.Pos - bearer.Dir * 0.35f + new Vector2(-bearer.Dir.Y, bearer.Dir.X) * 0.25f;
            puppet.Banner(at, t.Height(at) + (bearer.Mounted ? 1.1f : 0), bearer.Palette, battle.Time * 3 + s.Units.Count, s.Company.Banner);
        }
        if (!forSun)
            foreach (var m in motes) puppet.Particle(m.Pos, m.Size * (m.Heavy ? 1 : MathF.Min(1, m.Life / m.Max * 2)), m.Col);
        var eye2 = new Vector2(eye.X, eye.Z);
        foreach (var u in battle.Units)
        {
            if (!forSun && !u.IsPlayer && u.Team == 0 && blend < 0.5f && Vector2.DistanceSquared(u.Pos, eye2) < 6 * 6) continue; // friends don't fill the lens
            if (deploying && !battle.Options.Scouted && u.Team == 1 && !u.IsLeader) continue; // unscouted: banners only
            puppet.Draw(u, t.Height(u.Pos));
        }
        foreach (var h in battle.LooseHorses) puppet.DrawLooseHorse(h, t.Height(h.Pos));
        foreach (var h in battle.DeadHorses) puppet.DrawDeadHorse(h, t.Height(h.Pos));
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

    void Ring(Vector2 p, float r, Color c, int seg = 12)
    {
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathF.Tau / seg, a1 = (i + 1) * MathF.Tau / seg;
            Line(G(p + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r), G(p + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r), c);
        }
    }

    /// Rain streaks (or snowflakes) in a box around the camera, falling on a fixed pattern so nothing is stored.
    void Precipitation()
    {
        var w = battle.Weather;
        if (w.Rain < 0.05f) return;
        int n = (int)(700 * w.Rain);
        var c = eye;
        float t = realTime;
        var col = w.Snow ? new Color(236, 238, 244) : new Color(150, 162, 178);
        for (int i = 0; i < n; i++)
        {
            uint h = (uint)i * 2654435761u;
            float x = (h & 1023) / 1023f * 50 - 25, z = (h >> 10 & 1023) / 1023f * 50 - 25, ph = (h >> 20 & 1023) / 1023f;
            float fall = w.Snow ? 1.2f : 9f, y = 18 - (ph * 18 + t * fall) % 18;
            var p = new Vector3(MathF.Floor(c.X / 50) * 50 + x, c.Y - 6 + y, MathF.Floor(c.Z / 50) * 50 + z);
            if (p.X < c.X - 25) p.X += 50; if (p.X > c.X + 25) p.X -= 50;
            if (p.Z < c.Z - 25) p.Z += 50; if (p.Z > c.Z + 25) p.Z -= 50;
            var drift = w.Snow ? new Vector3(MathF.Sin(t + i) * 0.05f, -0.08f, 0) : new Vector3(0.05f, -0.7f, 0.02f);
            Line(p, p + drift, col);
        }
    }

    void DrawLines()
    {
        lines.N = 0;
        Precipitation();
        foreach (var a in battle.Arrows)
        {
            var dir = a.Vel.LengthSquared() > 0 ? Vector3.Normalize(a.Vel) : Vector3.Down;
            Line(a.Pos, a.Pos - dir * (a.Stuck >= 0 ? 0.5f : 0.8f), ArrowColor);
        }
        foreach (var s in selected)
            foreach (var u in s.Units)
                if (u.Active) Ring(u.Pos, u.Mounted ? 1.05f : 0.45f, Sel, 8);
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
