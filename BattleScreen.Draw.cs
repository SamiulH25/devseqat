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
        UpdateLod();
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
        FrameProf.Begin("hud");
        if (!hudSynced) SyncHud();
        else { hud.View = view; hud.Proj = proj; hud.Mouse = shotMouse ?? (shotNoMouse ? new Point(-999, -999) : ms.Position); }
        hudSynced = false;
        if (!hudHidden)
        {
            hud.Draw();
            if (TipsHeld) return;
            tipUi.Begin(gd.Viewport, ms.Position, ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released);
            BeatCaption(tipUi);
            BattleTutorialRibbon(tipUi);
            Tips.Draw(tipUi, tipUi.U(224)); // above the order panel and its key hints at the bottom right
            tipUi.End();
        }
        FrameProf.End("hud");
    }

    /// The shader path: sun shadow map, then the scene into an off-screen target, then the post pass to the screen.
    void DrawShaded(Shading sh)
    {
        var gd = GraphicsDevice;
        bool shadows = quality == Quality.High;
        bool do3D = use3DPixel && puppet3D != null;
        sh.FogFrom = MathF.Max(0, Vector3.Distance(eye, focusPoint) - 12); // fog measured from just short of the ground in view
        sh.CutEye = new Vector4(eye, Vector2.Distance(new(eye.X, eye.Z), new(focusPoint.X, focusPoint.Z)) * MathHelper.SmoothStep(0, 1, blend)); // set before the sun's pass: thinned trees cast no shadow
        if (shadows)
        {
            float k = MathHelper.SmoothStep(0, 1, blend);
            float shRadius = MathHelper.Lerp(65, Math.Clamp(zoom * 2.2f, 65, 190), k);
            bool needShadow = !battleShadowValid || HasShots
                || Vector3.DistanceSquared(focusPoint, battleShadowFocus) > 2.25f
                || MathF.Abs(shRadius - battleShadowRad) > shRadius * 0.04f
                || realTime - battleShadowAt >= 0.033f;
            if (needShadow)
            {
                battleShadowValid = true; battleShadowFocus = focusPoint; battleShadowRad = shRadius; battleShadowAt = realTime;
                FrameProf.Begin("worldShadow");
                sh.AimSun(focusPoint, shRadius);
                gd.Textures[1] = null;
                gd.SetRenderTarget(sh.ShadowMap);
                gd.Clear(Color.White);
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                gd.BlendState = BlendState.Opaque;
                world.DrawShadowCasters(sh, realTime);
                FrameProf.End("worldShadow");
                FrameProf.Begin("shadowBuild");
                BuildSprites(sh.LightView, true); // puppets turned to face the sun, so shadows have their silhouette
                FrameProf.End("shadowBuild");
                FrameProf.Begin("shadowDraw");
                sh.Setup(sh.Sprite, Matrix.Identity, sh.LightEye, realTime, false, 0);
                sh.Sprite.Parameters["ShadowMap"].SetValue((Texture2D?)null);
                DrawSpritesShaded(sh.Sprite, "SpriteShadow");
                int shdN = do3D ? Total3DVerts() + Total3DInstances() + skinnedQueue.Count : 0;
                if (shdN > 0)
                {
                    sh.Setup(sh.Unit3D, Matrix.Identity, sh.LightEye, realTime, false, 0);
                    sh.Unit3D.Parameters["ShadowMap"].SetValue((Texture2D?)null);
                    sh.Unit3D.Parameters["View"].SetValue(sh.LightView);
                    int instTris = DrawUnits3D(sh, sh.Unit3D, "Unit3DShadow", false);
                    if (FrameProf.On) FrameProf.Count("shdTris", Total3DVerts() / 3 + instTris);
                }
                FrameProf.End("shadowDraw");
            }
        }
        gd.Textures[1] = null;
        RenderTarget2D? normDepth = null;
        FrameProf.Begin("worldDraw");
        if (do3D)
        {
            var (pCol, pNd) = sh.PixelTargets(pixelScale);
            normDepth = pNd;
            gd.SetRenderTarget(pNd);
            gd.Clear(Color.Transparent);
            gd.SetRenderTarget(pCol);
        }
        else gd.SetRenderTarget(sh.Scene());
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
        FrameProf.End("worldDraw");
        FrameProf.Begin("mainBuild");
        BuildSprites(view, false);
        FrameProf.End("mainBuild");
        int mainN = do3D ? Total3DVerts() + Total3DInstances() + skinnedQueue.Count : 0;
        if (mainN > 0)
        {
            FrameProf.Begin("u3dDraw");
            var (pCol, pNd) = sh.PixelTargets(pixelScale);
            gd.SetRenderTargets(pCol, pNd);
            sh.Setup(sh.Unit3D, view * proj, eye, realTime, shadows, 0.45f);
            sh.Unit3D.Parameters["View"].SetValue(view);
            int instTris = DrawUnits3D(sh, sh.Unit3D, "Unit3D", shadows);
            gd.SetRenderTarget(pCol);
            if (FrameProf.On) FrameProf.Count("u3dTris", Total3DVerts() / 3 + instTris);
            FrameProf.End("u3dDraw");
        }
        FrameProf.Begin("spritesAndPost");
        sh.Setup(sh.Sprite, view * proj, eye, realTime, shadows, 0.45f);
        DrawSpritesShaded(sh.Sprite, "Sprite");
        DrawSoft(sh.Sprite);
        sh.Torch = Vector4.Zero; sh.FogFrom = 0;
        Array.Clear(sh.Glow); // the lights are this battle's alone (the map shares the shaders)
        DrawLines();
        var (awe, moment) = PostMoment();
        if (do3D && normDepth != null)
            sh.PixelPostToScreen(pixelScale, Sky.Grade, 0, awe, moment, warpNow);
        else
            sh.PostToScreen(fxaa: Settings.Current.Fxaa, Sky.Grade, host.Quality == Quality.High ? 0.5f : 0, awe, moment, warpNow);
        FrameProf.End("spritesAndPost");
    }

    readonly BoundingFrustum spriteFrustum = new(Matrix.Identity);
    readonly byte[] corpseGrid = new byte[512 * 512];
    readonly List<int> corpseTouched = new();
    readonly List<Squad> goneSelected = new();
    Verts<VertexPNCT>[] workerVerts = Array.Empty<Verts<VertexPNCT>>();
    Puppet3D[] workerPuppets = Array.Empty<Puppet3D>();
    MeshAtlas3D? meshAtlas;
    InstanceBatch[] allBatches = Array.Empty<InstanceBatch>();
    const int U3dRingCount = 4;
    readonly DynamicVertexBuffer?[] u3dVbs = new DynamicVertexBuffer?[U3dRingCount];
    VertexPNCT[] u3dStage = Array.Empty<VertexPNCT>();
    int u3dVbIdx;
    bool battleShadowValid;
    Vector3 battleShadowFocus;
    float battleShadowRad, battleShadowAt;
    readonly List<(Unit U, float H, float Lod, float Rear, Look Look)> drawQueue3D = new(1024);
    int Total3DVerts() { int n = units3D.N; for (int i = 0; i < workerVerts.Length; i++) n += workerVerts[i].N; return n; }
    int Total3DInstances() { int n = 0; for (int i = 0; i < allBatches.Length; i++) n += allBatches[i].Count; return n; }

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
                case Cue.ParrySteel:
                    Spray(at, 1.35f, 14, new Color(255, 240, 175), 4.6f, 0.36f, 0.085f, true);
                    Spray(at, 1.35f, 1, new Color(255, 248, 220), 0.2f, 0.16f, 0.46f, false, 3.4f);
                    AddSparkFlash(at, 1.35f, 0.95f, new Color(255, 240, 175));
                    AddGlow(at, 1.35f, new Color(255, 234, 165), 1.3f, 5.5f);
                    break;
                case Cue.Clash: case Cue.BlockMetal: case Cue.BluntPlate:
                    Spray(at, 1.3f, 9, new Color(255, 226, 140), 3.8f, 0.30f, 0.075f, true);
                    Spray(at, 1.3f, 1, new Color(255, 236, 190), 0.3f, 0.14f, 0.32f, false, 2.6f);
                    AddSparkFlash(at, 1.3f, 0.68f, new Color(255, 222, 130));
                    break;
                case Cue.ArrowPlate:
                    Spray(at, 1.3f, 5, new Color(255, 232, 160), 3.2f, 0.24f, 0.06f, true);
                    break;
                case Cue.Block: case Cue.BlockWood: case Cue.ArrowShield:
                    Spray(at, 1.2f, 5, new Color(205, 172, 124), 2.5f, 0.26f, 0.065f, true); // wood splinters
                    break;
                case Cue.ShieldSplinter: case Cue.LanceShatter:
                    Spray(at, 1.25f, 12, new Color(185, 142, 92), 3.8f, 0.42f, 0.085f, true);
                    Spray(at, 1.2f, 3, new Color(215, 195, 160), 1.4f, 0.45f, 0.22f, false, 1.1f);
                    break;
                case Cue.Hit: case Cue.ArrowHit: case Cue.SlashLight: case Cue.PierceHit: case Cue.ArrowFlesh: case Cue.BluntFlesh:
                    Spray(at, 1.15f, 6, new Color(155, 18, 22), 2.1f, 0.52f, 0.075f, true);
                    break;
                case Cue.SlashHeavy:
                    Spray(at, 1.2f, 10, new Color(165, 16, 20), 2.7f, 0.58f, 0.085f, true);
                    Spray(at, 1.2f, 4, new Color(255, 226, 150), 2.9f, 0.22f, 0.06f, true);
                    break;
                case Cue.LanceImpact:
                    Spray(at, 1.2f, 12, new Color(165, 16, 20), 3.2f, 0.6f, 0.095f, true);
                    Spray(at, 0.35f, 10, new Color(172, 154, 120), 2.5f, 1.05f, 0.32f, false, 1.1f);
                    AddRipple(at, 0.4f, 3.8f, 0.42f, new Color(196, 178, 138), 0.14f);
                    AddSparkFlash(at, 1.3f, 0.85f, new Color(255, 232, 165));
                    break;
                case Cue.BoneHit: case Cue.BoneCollapse:
                    Spray(at, 1.1f, cue == Cue.BoneCollapse ? 11 : 6, new Color(224, 214, 188), 2.8f, 0.48f, 0.075f, true);
                    break;
                case Cue.BashThud:
                    Spray(at, 1.1f, 4, new Color(210, 192, 155), 1.8f, 0.38f, 0.20f, false, 0.9f);
                    break;
                case Cue.Impact:
                    Spray(at, 0.2f, 12, new Color(168, 150, 116), 1.9f, 1.35f, 0.28f, false, 0.85f);
                    AddRipple(at, 0.5f, 4.2f, 0.48f, new Color(188, 170, 132), 0.14f);
                    break;
            }
        GroundSteps(dt);
        TrackSlashes();
        AtmosphereFx(dt);
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

    float atmoAcc;

    /// Ambient battlefield atmosphere: siege fire smoke & embers, desert/steppe wind dust, winter snow gusts, forest leaves, rain splashes.
    void AtmosphereFx(float dt)
    {
        if (quality == Quality.Low || motes.Count > 1350) return;
        atmoAcc += dt;
        if (atmoAcc < 0.08f) return;
        float step = atmoAcc;
        atmoAcc = 0;
        var focus2 = new Vector2(focusPoint.X, focusPoint.Z);

        // Siege smoke & embers drifting from the wall/gate line
        if (battle.Options.Siege && battle.Terrain.Wall is { Gates.Count: > 0 } wall)
        {
            var gate = wall.Gates[0].Pos;
            if (fx.NextSingle() < step * 6f)
                Spray(gate + new Vector2((fx.NextSingle() - 0.5f) * 2f, (fx.NextSingle() - 0.5f) * 8f), 1.6f, 1, new Color(86, 80, 74), 0.35f, 3.6f, 0.75f, false, 0.55f, -1.3f);
            if (fx.NextSingle() < step * 9f)
                Spray(gate + new Vector2((fx.NextSingle() - 0.5f) * 1.5f, (fx.NextSingle() - 0.5f) * 6f), 0.9f, 1, new Color(255, 168, 56), 0.8f, 1.4f, 0.065f, false, 0, -1.6f);
        }

        // Biome & weather ambient particles around the camera focus
        var p = focus2 + new Vector2((fx.NextSingle() - 0.5f) * 34f, (fx.NextSingle() - 0.5f) * 34f);
        if (battle.Weather.Snow && fx.NextSingle() < step * 14f)
        {
            motes.Add(new Mote { Pos = G(p, 4.5f + fx.NextSingle() * 3f), Vel = new Vector3(1.6f, -1.3f, 0.6f), Life = 3.2f, Max = 3.2f, Size = 0.065f, Col = new Color(236, 242, 250) });
        }
        else if (battle.Weather.Rain > 0.35f && fx.NextSingle() < step * 12f)
        {
            if (battle.Terrain.WaterDepth(p) > 0.04f || battle.Terrain.GroundAt(p) == GroundKind.Mud)
                Spray(p, 0.06f, 2, new Color(190, 206, 218), 1.2f, 0.25f, 0.045f, true);
        }
        else if (battle.Terrain.Params == TerrainParams.Desert || battle.Terrain.Params == TerrainParams.Steppe)
        {
            if (fx.NextSingle() < step * 5f)
                motes.Add(new Mote { Pos = G(p, 0.45f), Vel = new Vector3(3.4f, 0.15f, 0.9f), Life = 2.1f, Max = 2.1f, Size = 0.45f, Grow = 0.35f, Col = new Color(196, 174, 132) });
        }
        else if (battle.Terrain.Params == TerrainParams.Forest && fx.NextSingle() < step * 4f)
        {
            motes.Add(new Mote { Pos = G(p, 3.8f + fx.NextSingle() * 2f), Vel = new Vector3(0.7f, -0.55f, 0.4f), Life = 3.4f, Max = 3.4f, Size = 0.08f, Col = fx.Next(2) == 0 ? new Color(164, 128, 52) : new Color(102, 132, 54) });
        }
    }

    readonly float[] stepTimer = new float[6];
    float groundStepAcc;

    /// What the ground does under moving men (docs/archive/TERRAIN.md T8): dust under galloping hooves, splashes at fords and in water,
    /// mud thrown up, leaves shaken down in woods; and near the camera, their sounds (splash, squelch, rustle, bridge, snow, gravel).
    void GroundSteps(float dt)
    {
        groundStepAcc += dt;
        if (groundStepAcc < 0.05f) return;
        dt = groundStepAcc;
        groundStepAcc = 0;
        var t = battle.Terrain;
        bool hasWater = t.Rivers.Count > 0;
        var ear = new Vector2(focusPoint.X, focusPoint.Z);
        float range = 25 + 30 * blend;
        float range2 = range * range;
        Span<int> heard = stackalloc int[6]; // splash, squelch, rustle, hollow, snow, gravel
        Span<Vector2> where = stackalloc Vector2[6];
        int dustBudget = quality == Quality.Low ? 24 : quality == Quality.Medium ? 64 : 128;
        var eye2 = new Vector2(eye.X, eye.Z);
        var units = battle.Units;
        for (int ui = 0; ui < units.Count; ui++)
        {
            var u = units[ui];
            if (!u.Active) continue;
            if (u.Routing && !u.Mounted && fx.NextSingle() < dt * 1.5f && u.Vel.LengthSquared() > 4) // men running for their lives kick up dust
                Spray(u.Pos - u.Dir * 0.4f, 0.1f, 1, battle.Weather.Rain > 0.3f ? new Color(96, 82, 60) : new Color(176, 160, 124), 0.5f, 1.0f, 0.22f, false, 0.5f);
            float speed = MathF.Abs(u.Speed);
            if (speed < 1.2f) continue;
            var kind = t.GroundAt(u.Pos);
            bool wet = hasWater && t.WaterDepth(u.Pos) > 0.05f, fast = speed > 5.5f;
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
            // Pillar 4 P4.2: pooled cavalry hoof dust wakes on dry ground (grass, road, stony) above trot speed.
            if (u.Mounted && (fast || u.Vel.LengthSquared() > 22f) && !wet && kind != GroundKind.Mud && !battle.Weather.Snow && dustBudget > 0 && motes.Count < 760
                && Vector2.DistanceSquared(u.Pos, eye2) < 140f * 140f)
            {
                dustBudget--;
                var dustCol = battle.Weather.Rain > 0.3f ? new Color(124, 106, 78)
                    : kind is GroundKind.Road or GroundKind.Street or GroundKind.Stony ? new Color(224, 206, 164)
                    : new Color(214, 196, 150);
                var hoof1 = u.Pos - u.HeadingDir * (0.95f + fx.NextSingle() * 0.55f) + new Vector2(fx.NextSingle() - 0.5f, fx.NextSingle() - 0.5f) * 0.65f;
                var hoof2 = u.Pos - u.HeadingDir * (1.85f + fx.NextSingle() * 0.75f) + new Vector2(fx.NextSingle() - 0.5f, fx.NextSingle() - 0.5f) * 0.85f;
                Spray(hoof1, 0.24f, 1, dustCol, 1.25f, 2.2f, 1.08f, false, 1.75f);
                Spray(hoof2, 0.34f, 1, dustCol, 1.15f, 2.3f, 1.15f, false, 1.85f);
                if (speed > 7.5f && fx.NextSingle() < 0.6f)
                    Spray(hoof1, 0.16f, 1, new Color(74, 84, 40), 3.0f, 0.7f, 0.11f, true, 0f); // kicked turf clod
            }
            if (cue >= 0 && Vector2.DistanceSquared(u.Pos, ear) < range2) { heard[cue] += u.Mounted ? 3 : 1; where[cue] = u.Pos; }
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
        bool do3D = use3DPixel && puppet3D != null && quality != Quality.Low && shading != null;
        units3D.N = 0;
        puppet.Begin(viewer);
        puppet.Now = battle.Time;
        puppet.Winner = battle.Winner;
        if (do3D)
        {
            puppet3D!.Now = battle.Time;
            puppet3D.Winner = battle.Winner;
            puppet3D.FxPuppet = forSun ? null : puppet;
            puppet3D.BeginCamera(viewer);
        }
        for (int i = 0; i < workerVerts.Length; i++) workerVerts[i].N = 0;
        for (int i = 0; i < allBatches.Length; i++) allBatches[i].Clear();
        if (do3D) puppet3D!.ShadowOnly = forSun;
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
        // A banner over every company, carried by its first man.
        foreach (var s in battle.Squads)
        {
            if (s.Company == null || s.Alive == 0) continue;
            if (deploying && !battle.Options.Scouted && s.Team == 1) continue;
            Unit? bearer = null;
            foreach (var u in s.Units) if (u.Active && !u.Routing) { bearer = u; break; }
            if (!forSun) BannerFalls(s, bearer);
            if (bearer == null) continue;
            if (!battle.Shown(s))
            {
                float bh = t.Height(s.LastSeen);
                if (spriteFrustum.Contains(new BoundingSphere(new Vector3(s.LastSeen.X, bh + 2f, s.LastSeen.Y), 3.5f)) != ContainmentType.Disjoint)
                {
                    if (do3D) puppet3D!.Banner(s.LastSeen, bh, bearer.Palette, 0, s.Company.Banner);
                    else puppet.Banner(s.LastSeen, bh, bearer.Palette, 0, s.Company.Banner);
                }
                continue;
            }
            var at = bearer.Pos - bearer.Dir * 0.35f + new Vector2(-bearer.Dir.Y, bearer.Dir.X) * 0.25f;
            float by = t.Height(at) + (bearer.Mounted ? 1.1f : 0);
            if (spriteFrustum.Contains(new BoundingSphere(new Vector3(at.X, by + 2f, at.Y), 3.5f)) == ContainmentType.Disjoint) continue;
            float stream = Math.Clamp((bearer.Mounted ? MathF.Abs(bearer.Speed) : bearer.Vel.Length()) / 6, 0, 1);
            if (do3D) puppet3D!.Banner(at, by, bearer.Palette, battle.Time * 3 + s.Units.Count, s.Company.Banner, stream, streamers: s.Company.Streamers, fringe: s.Company.Fringe, swallowtail: s.Cavalry);
            else puppet.Banner(at, by, bearer.Palette, battle.Time * 3 + s.Units.Count, s.Company.Banner, stream, streamers: s.Company.Streamers, fringe: s.Company.Fringe);
            // Pillar 4 P4.1: secondary flank pennon on large companies (>= 40 standing men), placed on the true opposite lateral flank.
            if (do3D && s.Alive >= 40)
            {
                var right = new Vector2(-s.Front.Y, s.Front.X);
                Unit? flanker = null;
                float bestLat = 2f;
                for (int fi = 0; fi < s.Units.Count; fi++)
                {
                    var fu = s.Units[fi];
                    if (!fu.Active || fu.Routing || fu == bearer) continue;
                    float lat = MathF.Abs(Vector2.Dot(fu.Pos - bearer.Pos, right));
                    if (lat > bestLat) { bestLat = lat; flanker = fu; }
                }
                if (flanker != null)
                {
                    var fat = flanker.Pos - flanker.Dir * 0.35f - new Vector2(-flanker.Dir.Y, flanker.Dir.X) * 0.25f;
                    float fby = t.Height(fat) + (flanker.Mounted ? 1.1f : 0);
                    if (spriteFrustum.Contains(new BoundingSphere(new Vector3(fat.X, fby + 2.5f, fat.Y), 3.5f)) != ContainmentType.Disjoint)
                    {
                        float fstream = Math.Clamp((flanker.Mounted ? MathF.Abs(flanker.Speed) : flanker.Vel.Length()) / 6, 0, 1);
                        puppet3D!.Banner(fat, fby, flanker.Palette, battle.Time * 3.2f + s.Units.Count + 1.7f, s.Company.Banner, fstream, scale: 0.82f, swallowtail: true);
                    }
                }
            }
        }
        // Pillar 4 P4.1: captain's personal retinue pennon carried by the closest standing friendly soldier within 16 m.
        if (do3D && battle.Player.Active && !battle.Options.Duel)
        {
            Unit? retinue = null;
            float bestD2 = 16f * 16f;
            foreach (var u in battle.Units)
            {
                if (u.Team != 0 || !u.Active || u.Routing || u.IsPlayer) continue;
                float d2 = Vector2.DistanceSquared(u.Pos, battle.Player.Pos);
                if (d2 > 4.5f * 4.5f && d2 < bestD2) { bestD2 = d2; retinue = u; }
            }
            if (retinue != null)
            {
                var rat = retinue.Pos - retinue.Dir * 0.35f + new Vector2(-retinue.Dir.Y, retinue.Dir.X) * 0.25f;
                float rby = t.Height(rat) + (retinue.Mounted ? 1.1f : 0);
                if (spriteFrustum.Contains(new BoundingSphere(new Vector3(rat.X, rby + 2.5f, rat.Y), 3.5f)) != ContainmentType.Disjoint)
                {
                    float rstream = Math.Clamp((retinue.Mounted ? MathF.Abs(retinue.Speed) : retinue.Vel.Length()) / 6, 0, 1);
                    puppet3D!.Banner(rat, rby, battle.Player.Palette, battle.Time * 3.4f + 0.9f, -1, rstream, scale: 0.78f, swallowtail: true);
                }
            }
        }
        DrawFallingBanners(do3D);
        if (!forSun)
        {
            DrawMarks();
            DrawScars();
            soft.N = 0;
            foreach (var m in motes)
                if (!m.Heavy && (m.Size >= 0.15f || m.Grow >= 0.3f)) // dust and smoke: a soft puff that swells and thins away smoothly
                    puppet.Haze(m.Pos, m.Size * 1.15f, m.Col, MathHelper.Lerp(0.58f, 0.34f, Math.Clamp((m.Size - 0.9f) / 1.8f, 0f, 1f)) * MathF.Min(1, m.Life / m.Max * 1.6f), m.Max * 37 + m.Grow * 11 + m.Pos.X);
                else puppet.Particle(m.Pos, m.Size * (m.Heavy ? 1 : MathF.Min(1, m.Life / m.Max * 2)), m.Col);
        }
        foreach (int idx in corpseTouched) corpseGrid[idx] = 0;
        corpseTouched.Clear();
        drawQueue3D.Clear(); skinnedQueue.Clear();
        float bNow = battle.Time;
        // Continuous per-unit 0..1 LOD slider (distance + camera zoom + dynamic FPS governor + player ModelDetail slider).
        // units3D already initialized above for 3D banners
        var allUnits = battle.Units;
        for (int ui = 0; ui < allUnits.Count; ui++)
        {
            var u = allUnits[ui];
            if (u.Fled) continue;
            if (u.Dead && bNow > 0 && bNow - u.DiedAt > (forSun ? 0.8f : 4.2f)) continue; // ascended spirits cast no shadow and clear cleanly
            if (deploying && !battle.Options.Scouted && u.Team == 1 && !u.IsLeader) continue; // unscouted: banners only
            if (u.Team == 1 && !u.IsLeader && !battle.Shown(u.Squad)) continue; // hidden by a crest or a wood
            float h = t.Height(u.Pos);
            if (!forSun && !u.IsPlayer && u.Team == 0 && blend < 0.5f && LensFill(u.Pos, h)) continue; // friends don't fill the lens
            float seenRad = u.Dead ? 5f : MathF.Max(u.Mounted ? 2.5f : 1.5f, u.Type.Size * (u.Type.Flying ? 3.5f : 1.5f));
            if (!Seen(u.Pos, h + (u.Dead ? 2.5f : u.Altitude), seenRad)) continue; // rising spirits or a wyvern on the wing
            if (do3D)
            {
                bool wisp = false;
                if (u.Dead && bNow > 0)
                {
                    float ft = (u.Type.Look.Beast ? Ironfield.Fall.Plain : u.DiedFall) switch { Ironfield.Fall.Saddle => 0.75f, Ironfield.Fall.Flung => 0.6f, _ => 0.45f + Ease.Hash(u.Index) * 0.3f };
                    if (bNow - u.DiedAt >= ft) wisp = true;
                }
                if (!wisp)
                {
                    float d3 = forSun || u.IsPlayer ? 0f : Vector3.Distance(new Vector3(u.Pos.X, h + 1f, u.Pos.Y), eye);
                    float unitLod = UnitLod(d3, u.IsPlayer, forSun);
                    EnsureSkinned();
                    if (!u.Dead && !u.Fled && u.Type.Look.Face == "bear" && skinnedBear != null)
                    {
                        skinnedQueue.Add((u, h, true));
                        continue;
                    }
                    if (!u.Dead && !u.Fled && u.Type.Look.Beast && u.Type.Breed is "wolf" or "hound" && skinnedWolf != null)
                    {
                        skinnedQueue.Add((u, h, false));
                        continue;
                    }
                    bool farProxy = unitLod < 0.14f && !u.Dead;
                    var look = farProxy || puppet3D!.ExactLook ? u.Type.Look : Variety.Of(u);
                    float rear = farProxy ? 0f : RearOf(u, !forSun);
                    drawQueue3D.Add((u, h, unitLod, rear, look));
                    continue;
                }
            }
            puppet.Rear = RearOf(u, !forSun);
            if (u.Dead && bNow - u.DiedAt > 1f)
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
        if (do3D && drawQueue3D.Count > 0)
        {
            int qCount = drawQueue3D.Count;
            if (workerPuppets.Length > 1 && qCount >= 64)
            {
                int nW = Math.Min(workerPuppets.Length, (qCount + 31) / 32);
                int winner = battle.Winner;
                Parallel.For(0, nW, w =>
                {
                    var wp = workerPuppets[w];
                    wp.Now = bNow;
                    wp.Winner = winner;
                    wp.ShadowOnly = forSun;
                    wp.BeginCamera(viewer);
                    wp.PendingHaze.Clear();
                    // Strided partitioning spreads Detail 0/1/2 units evenly across all worker cores (Units are clustered by squad).
                    for (int i = w; i < qCount; i += nW)
                    {
                        var item = drawQueue3D[i];
                        wp.Lod = item.Lod;
                        wp.Rear = item.Rear;
                        wp.TryDraw(item.U, item.H, item.Look);
                    }
                    wp.Rear = 0;
                    wp.ShadowOnly = false;
                });
                if (!forSun)
                {
                    for (int w = 0; w < nW; w++)
                    {
                        var ph = workerPuppets[w].PendingHaze;
                        for (int i = 0; i < ph.Count; i++)
                        {
                            var hItem = ph[i];
                            puppet.Haze(hItem.Pos, hItem.Size, hItem.Col, hItem.Alpha, hItem.Seed);
                        }
                        ph.Clear();
                    }
                }
            }
            else
            {
                for (int i = 0; i < qCount; i++)
                {
                    var item = drawQueue3D[i];
                    puppet3D!.Lod = item.Lod;
                    puppet3D.Rear = item.Rear;
                    puppet3D.TryDraw(item.U, item.H, item.Look);
                }
                puppet3D!.Rear = 0;
            }
        }
        if (do3D) puppet3D!.ShadowOnly = false;
        puppet.SparseCorpse = false;
        puppet.Rear = 0;
        if (!forSun) DrawMissiles(do3D); // real arrows near the camera (BattleScreen.Arrows.cs)
        if (do3D) DrawEngines3D(); // solid lit 3D siege engines (BattleScreen.Engines3D.cs)
        foreach (var h in battle.LooseHorses) { float hh = t.Height(h.Pos); if (Seen(h.Pos, hh, 2.5f)) { if (do3D && h.Mount.Barding != "wolf") puppet3D!.DrawLooseHorse(h, hh); else puppet.DrawLooseHorse(h, hh); } }
        foreach (var h in battle.DeadHorses) { if (battle.Time > 0 && battle.Time - h.At > (forSun ? 0.75f : 3.2f)) continue; float hh = t.Height(h.Pos); if (Seen(h.Pos, hh + 1.5f, 4f)) { if (do3D && h.Barding != "wolf" && (battle.Time <= 0 || battle.Time - h.At < 0.7f)) puppet3D!.DrawDeadHorse(h, hh); else puppet.DrawDeadHorse(h, hh); } }
        puppet.Detail = 0;
    }

    SkinnedMesh3D? skinnedBear, skinnedWolf;
    bool skinnedLoaded;
    readonly List<(Unit U, float H, bool Bear)> skinnedQueue = new();

    void EnsureSkinned()
    {
        if (skinnedLoaded) return;
        skinnedLoaded = true;
        skinnedBear = SkinnedMesh3D.TryLoad(GraphicsDevice, "bear_skinned.skin", "bear_tex.png");
        skinnedWolf = SkinnedMesh3D.TryLoad(GraphicsDevice, "wolf_skinned.skin", "wolf_tex.png");
    }

    int DrawUnits3D(Shading sh, Effect fx, string technique, bool drawShadows)
    {
        var gd = GraphicsDevice;
        bool shadow = technique == "Unit3DShadow";
        int instTris = 0;
        if (meshAtlas != null && Total3DInstances() > 0)
        {
            sh.Setup(sh.InstancedUnit3D, shadow ? Matrix.Identity : view * proj, shadow ? sh.LightEye : eye, realTime, !shadow && drawShadows, shadow ? 0f : 0.45f);
            if (shadow) sh.InstancedUnit3D.Parameters["ShadowMap"]?.SetValue((Texture2D?)null);
            sh.InstancedUnit3D.Parameters["View"]?.SetValue(shadow ? sh.LightView : view);
            instTris = meshAtlas.DrawBatches(gd, sh, sh.InstancedUnit3D, technique, shadow, allBatches);
        }
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
        int totalVerts = Total3DVerts();
        if (totalVerts > 0)
        {
            fx.CurrentTechnique = fx.Techniques[technique];
            fx.CurrentTechnique.Passes[0].Apply();
        }
        if (totalVerts > 0)
        {
            const int Chunk = 60000;
            int slot = u3dVbIdx++ % U3dRingCount;
            var vb = u3dVbs[slot];
            if (vb == null || vb.IsDisposed || vb.VertexCount < totalVerts)
            {
                vb?.Dispose();
                int cap = 65536;
                while (cap < totalVerts) cap *= 2;
                u3dVbs[slot] = vb = new DynamicVertexBuffer(gd, VertexPNCT.Decl, cap, BufferUsage.WriteOnly);
            }
            if (u3dStage.Length < totalVerts)
            {
                int cap = Math.Max(65536, u3dStage.Length * 2);
                while (cap < totalVerts) cap *= 2;
                u3dStage = new VertexPNCT[cap];
            }
            int dst = 0;
            if (units3D.N > 0) { Array.Copy(units3D.A, 0, u3dStage, dst, units3D.N); dst += units3D.N; }
            for (int i = 0; i < workerVerts.Length; i++)
            {
                var wv = workerVerts[i];
                if (wv.N > 0) { Array.Copy(wv.A, 0, u3dStage, dst, wv.N); dst += wv.N; }
            }
            vb.SetData(u3dStage, 0, totalVerts, SetDataOptions.Discard);
            gd.SetVertexBuffer(vb);
            int totalTris = totalVerts / 3;
            for (int s = 0; s < totalTris; s += Chunk)
                gd.DrawPrimitives(PrimitiveType.TriangleList, s * 3, Math.Min(Chunk, totalTris - s));
        }
        if (skinnedQueue.Count > 0)
        {
            sh.Setup(sh.SkinnedUnit3D, shadow ? Matrix.Identity : view * proj, shadow ? sh.LightEye : eye, realTime, !shadow && drawShadows, shadow ? 0f : 0.45f);
            if (shadow) sh.SkinnedUnit3D.Parameters["ShadowMap"]?.SetValue((Texture2D?)null);
            sh.SkinnedUnit3D.Parameters["View"]?.SetValue(shadow ? sh.LightView : view);
            foreach (var (su, shH, isBear) in skinnedQueue)
            {
                if (isBear) skinnedBear!.DrawBear(gd, sh, su, shH, 1f, technique);
                else skinnedWolf!.DrawWolf(gd, sh, su, shH, 1f, technique);
            }
        }
        return instTris;
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
    public string RenderCost => $"{sprites.N / 3} sprite tris, {lines.N / 2} lines, {markCount} marks, {battle.Units.Count(u => u.Active)} men, {world.RenderCost}";

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

    void FortressPointRings()
    {
        if (battle.Wall is not Fortress fort) return;
        Color Col(int owner, float cap) => owner == 0 ? Gold * 0.85f : cap < 0.99f ? Color.Lerp(Gold, Foe, cap) * 0.85f : Foe * 0.65f;
        // The heart: a bold ring around the square / keep door.
        Ring(fort.Heart, fort.HeartRadius, Col(fort.HeartOwner, fort.HeartCapture), 32, 1.4f);
        // Gatehouses: smaller ring just inside each gate.
        foreach (var g in fort.Gates)
            Ring(g.Pos - g.Out * 3.5f, 4.5f, Col(g.Owner, g.Capture), 16, 1.0f);
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
    /// A chosen company's ground outline: a box round its men, squared to its front, with bright corners, so which companies
    /// are chosen reads from any height. It swells with the rings when the company is first chosen.
    void SelectionBox(Squad s, float swell)
    {
        var fr = s.Front.LengthSquared() > 1e-4f ? Vector2.Normalize(s.Front) : Vector2.UnitY;
        var rt = new Vector2(-fr.Y, fr.X);
        float minR = float.MaxValue, maxR = float.MinValue, minF = float.MaxValue, maxF = float.MinValue;
        Vector2 c = Vector2.Zero;
        int n = 0;
        foreach (var u in s.Units) if (u.Active) { c += u.Pos; n++; }
        if (n == 0) return;
        c /= n;
        foreach (var u in s.Units)
        {
            if (!u.Active) continue;
            var d = u.Pos - c;
            float r = Vector2.Dot(d, rt), f = Vector2.Dot(d, fr);
            minR = MathF.Min(minR, r); maxR = MathF.Max(maxR, r); minF = MathF.Min(minF, f); maxF = MathF.Max(maxF, f);
        }
        float pad = (s.Cavalry ? 1.6f : 1.0f) * swell;
        minR -= pad; maxR += pad; minF -= pad; maxF += pad;
        Vector2 P(float r, float f) => c + rt * r + fr * f;
        var k = Sel * 0.55f;
        Span<Vector2> corners = stackalloc Vector2[] { P(minR, minF), P(maxR, minF), P(maxR, maxF), P(minR, maxF) };
        for (int i = 0; i < 4; i++)
        {
            var a = corners[i]; var b = corners[(i + 1) % 4];
            Band(G(a, 0.12f), G(b, 0.12f), k, 0.35f);
            float len = MathF.Min(2.5f, Vector2.Distance(a, b) * 0.3f);
            var dir = Vector2.Normalize(b - a);
            Band(G(a, 0.14f), G(a + dir * len, 0.14f), Sel, 0.7f); // the corners drawn bold
            Band(G(b, 0.14f), G(b - dir * len, 0.14f), Sel, 0.7f);
        }
    }

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

    int battleTutStep = -1;
    float battleTutTimer;

    /// Interactive 4-beat tutorial ribbon during the player's very first campaign battle (Campaign/Tutorial.cs).
    void BattleTutorialRibbon(Ui u)
    {
        if (battleTutStep < 0 || battle.Winner >= 0) return;
        battleTutTimer += 1 / 60f;
        switch (battleTutStep)
        {
            case 0 when !deploying: battleTutStep = 1; battleTutTimer = 0; break;
            case 1 when (command && arrows.Count > 0) || battleTutTimer > 16f: battleTutStep = 2; battleTutTimer = 0; break;
            case 2 when battle.Commanded.Any(s => s.Formation != Formation.Line) || battleTutTimer > 14f: battleTutStep = 3; battleTutTimer = 0; break;
            case 3 when (!command && battle.Player.Action != UnitAction.None) || battleTutTimer > 14f: battleTutStep = 4; battleTutTimer = 0; break;
            case 4 when battleTutTimer > 3.5f: battleTutStep = -1; return;
        }
        var (head, body) = battleTutStep switch
        {
            0 => ("BATTLE 1/4: DEPLOYMENT", "DRAG A COMPANY TO POSITION IT ON THE FIELD, THEN CLICK FIGHT AT BOTTOM-CENTER."),
            1 => ("BATTLE 2/4: COMMAND VIEW", "PRESS TAB FOR COMMAND VIEW  •  PRESS 1-2 TO SELECT  •  RIGHT-CLICK GROUND OR FOE TO ORDER."),
            2 => ("BATTLE 3/4: FORMATIONS", "WITH A COMPANY SELECTED, PRESS J TO CYCLE FORMATION (LINE / SHIELD WALL / OPEN RANKS)."),
            3 => ("BATTLE 4/4: JOIN THE FIGHT", "PRESS TAB FOR PERSONAL VIEW  •  WASD MOVE  •  LMB STRIKE  •  RMB PARRY!"),
            _ => ("FIRST BLOOD", "WELL FOUGHT, CAPTAIN! BREAK THE RAIDERS!"),
        };
        float slide = Ui.Instant ? 1f : Ease.OutCubic(Math.Clamp(battleTutTimer / 0.3f, 0f, 1f));
        float pulse = Ui.Instant ? 0.85f : 0.65f + 0.35f * MathF.Sin(Ui.Now * 4.2f);
        float flash = Ui.Instant ? 0f : MathF.Max(0f, 1f - battleTutTimer / 0.55f);

        int w = Math.Min(u.W - u.U(40), Math.Max(u.TW(body, 1) + u.U(36), u.TW(head, 1) + u.U(160)));
        int yOff = (int)((1f - slide) * -u.U(14));
        var r = new Rectangle((u.W - w) / 2, u.U(56) + yOff, w, u.U(42));
        u.Rect(new Rectangle(r.X - u.U(2), r.Y - u.U(2), r.Width + u.U(4), r.Height + u.U(4)), Kit.Gilt * (0.25f * pulse + 0.4f * flash));
        u.Rect(r, Color.Lerp(Kit.IronDark, Kit.Gilt, 0.14f * flash) * 0.95f);
        u.Panel(r, Color.Lerp(Kit.Gilt * pulse, Color.White, 0.3f * flash), u.U(2));
        u.Rect(new Rectangle(r.X, r.Y, u.U(4), r.Height), Kit.Gilt);
        u.Text(head, r.X + u.U(12), r.Y + u.U(7), Kit.Gilt, 1);

        // 4-pip beat indicator next to SKIP.
        int pipW = u.U(12), pipGap = u.U(3);
        int pipX = r.Right - u.U(64) - 4 * (pipW + pipGap);
        for (int i = 0; i < 4; i++)
        {
            var pr = new Rectangle(pipX + i * (pipW + pipGap), r.Y + u.U(9), pipW, u.U(4));
            u.Rect(pr, i < battleTutStep ? Kit.Gilt : i == battleTutStep ? Color.Lerp(Kit.Gilt, Color.White, 0.4f * pulse) : Kit.Rivet * 0.55f);
        }
        u.Text(body, r.X + u.U(12), r.Y + u.U(23), Style.Text, 1);
        if (battleTutStep < 4 && u.Button(new Rectangle(r.Right - u.U(56), r.Y + u.U(4), u.U(48), u.U(16)), "SKIP"))
            battleTutStep = -1;
    }

    /// The siege wall's torches light the night (none by day or without a wall): see `World.Torches`.
    Vector4 TorchRow()
    {
        if (battle.Terrain.Wall is not Fortress f || Sky.Night <= 0.05f) return Vector4.Zero;
        var g = f.MainGate.Pos;
        float top = battle.Terrain.Height(g - f.MainGate.Out * 2.5f) + 0.7f;
        return new Vector4(g.Y - 0.5f, top, Math.Clamp(Sky.Night * 1.8f, 0, 1), World.TorchSpacing);
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

    /// Planted stakes (they come mid-battle, after the field's meshes are built): sharpened poles leaning toward the enemy;
    /// a run ridden down from behind lies flat, pointing every way.
    void Stakes()
    {
        foreach (var f in battle.Terrain.Features)
        {
            if (f.Kind != FeatureKind.Stakes) continue;
            var d = f.B - f.A;
            float len = d.Length();
            if (len < 0.1f) continue;
            var lean = f.Facing * 0.55f;
            if (f.Facing == Vector2.Zero) // toward the far army: whoever planted them stands just behind
            {
                var far = Vector2.DistanceSquared(f.A, battle.Centroid(0)) > Vector2.DistanceSquared(f.A, battle.Centroid(1)) ? battle.Centroid(0) : battle.Centroid(1);
                lean = new Vector2(-d.Y, d.X) / len;
                lean *= Vector2.Dot(lean, far - f.A) > 0 ? 0.55f : -0.55f;
            }
            for (float s = 0; s <= len; s += 0.6f)
            {
                var p = f.A + d * (s / len);
                if (f.Broken) // trampled: a snapped stump, its pole flung down beside it at its own angle
                {
                    float r = MathF.Abs(MathF.Sin(s * 12.99f + f.A.X * 78.2f + f.A.Y * 37.7f) * 43758f) % 1, a = r * MathF.Tau;
                    var off = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    Line(G(p, 0), G(p + lean * 0.1f, 0.25f), StakeWood);
                    Line(G(p + off * 0.3f, 0.04f), G(p + off * 1.0f, 0.06f), StakeWood);
                    continue;
                }
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
        EngineLines(); // engines of war and their shots (BattleScreen.Engines.cs)
        Spells();
        if (!hudHidden)
        {
            DeployBoundary(); // the deployment line (BattleScreen.Planning.cs)
            RouteDots();
            foreach (var s in selected) PlanHook(s);
            foreach (var s in selected)
            {
                // A company just chosen: its rings swell once, so you see which it was.
                if (!selectedAt.TryGetValue(s, out float at)) selectedAt[s] = at = Ui.Now;
                float swell = 1 + 0.7f * Ease.Kick((Ui.Now - at) / 0.45f);
                foreach (var u in s.Units)
                    if (u.Active) Ring(u.Pos, (u.Mounted ? 1.05f : 0.45f) * swell, Sel * 0.85f, 8);
                SelectionBox(s, swell);
            }
            if (selectedAt.Count > 0)
            {
                goneSelected.Clear();
                foreach (var k in selectedAt.Keys) if (!selected.Contains(k)) goneSelected.Add(k);
                foreach (var k in goneSelected) selectedAt.Remove(k);
            }
            OrderArrowLines();
            FortressPointRings();
            HoldMarker(); // the fight view's hold-there marker (BattleScreen.Fight.cs)
            if (command || fieldCommand)
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
                    if (selected.Contains(s) && Vector2.DistanceSquared(s.Centroid(), s.Anchor) > 4f)
                        Band(G(s.Centroid(), 0.15f), G(s.Anchor, 0.15f), Sel * 0.55f, 0.5f);
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
                Vector2 cursorPos = shotGround ?? mouseGround;
                bool cursorOnField = shotGround != null || (!shotNoMouse && !hud.Over(ms.Position));
                Squad? hoverFoe = cursorOnField ? PickSquad(cursorPos, 1, fieldCommand ? 4.5f : 2.5f) : null;
                if (hoverFoe != null)
                    foreach (var u in hoverFoe.Units) if (u.Active && !u.Routing) Ring(u.Pos, u.Mounted ? 1.1f : 0.52f, Foe * 0.9f, 8, 1.0f);
                bool canPreview = Targets().Count > 0;
                if (dragStart is Vector2 d)
                {
                    // Formation drag preview: translucent white ground footprint discs + facing chevron.
                    var enemy = battle.Centroid(1);
                    bool click = Vector2.Distance(d, cursorPos) < 2;
                    var (a, b) = click ? LineEnds(cursorPos, enemy) : (d, cursorPos);
                    if (deploying) ClampToZone(ref a, ref b);
                    DrawFormationFootprints(a, b, enemy);
                    DrawCommandBanner(click ? cursorPos : (a + b) * 0.5f, hostile: false);
                }
                else if (cursorOnField)
                {
                    if (hoverFoe != null && !deploying)
                    {
                        // Snaps to hovered enemy centroid in crimson with crossed-swords emblem.
                        DrawCommandBanner(hoverFoe.Centroid(), hostile: true);
                    }
                    else
                    {
                        // Ground hover: tall translucent blue heraldic banner + live white ground footprints for selected companies.
                        if (canPreview && (fieldCommand || selected.Count > 0))
                        {
                            var enemy = battle.Centroid(1);
                            var (a, b) = LineEnds(cursorPos, enemy);
                            if (deploying) ClampToZone(ref a, ref b);
                            DrawFormationFootprints(a, b, enemy);
                        }
                        DrawCommandBanner(cursorPos, hostile: false);
                    }
                }
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

    void Quad3(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Color cBot, Color cTop)
    {
        bands.Add(new(bl, cBot)); bands.Add(new(br, cBot)); bands.Add(new(tr, cTop));
        bands.Add(new(bl, cBot)); bands.Add(new(tr, cTop)); bands.Add(new(tl, cTop));
    }

    /// Translucent ground-projected footprint disc elevated slightly above terrain triangles so it reads cleanly from horseback (Pillar 1 P1.3).
    void DrawFootprintDisc(Vector2 p, float r, Color fill, Color rim, float lift = 0.26f)
    {
        float y = battle.Terrain.Height(p) + lift;
        var center = new Vector3(p.X, y, p.Y);
        const int segs = 8;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * MathHelper.TwoPi / segs, a1 = (i + 1) * MathHelper.TwoPi / segs;
            var p0 = new Vector3(p.X + MathF.Cos(a0) * r, y, p.Y + MathF.Sin(a0) * r);
            var p1 = new Vector3(p.X + MathF.Cos(a1) * r, y, p.Y + MathF.Sin(a1) * r);
            bands.Add(new(center, fill)); bands.Add(new(p0, rim)); bands.Add(new(p1, rim));
        }
    }

    /// Crisp two-tone translucent white ground footprint markers + bold front baseline & facing chevron for PreviewSlots (Pillar 1 P1.3).
    void DrawFormationFootprints(Vector2 a, Vector2 b, Vector2 toward)
    {
        var shadowFill = Color.Black * 0.42f;
        var shadowRim = Color.Black * 0.08f;
        var fill = Color.White * 0.90f;
        var rim = Color.White * 0.48f;
        foreach (var slot in PreviewSlots(a, b, toward))
        {
            DrawFootprintDisc(slot, 0.56f, shadowFill, shadowRim, 0.24f);
            DrawFootprintDisc(slot, 0.42f, fill, rim, 0.28f);
        }
        // Front baseline + bold directional facing chevron so formation width & orientation read from horseback.
        var mid = (a + b) * 0.5f;
        var seg = b - a;
        var fwd = seg.LengthSquared() > 0.25f ? new Vector2(seg.Y, -seg.X) : toward - mid;
        if (fwd.LengthSquared() < 1e-4f) fwd = Vector2.UnitY;
        fwd.Normalize();
        if (Vector2.Dot(fwd, toward - mid) < 0) fwd = -fwd;
        var rt = new Vector2(-fwd.Y, fwd.X);
        Band(G(a + fwd * 0.9f, 0.30f), G(b + fwd * 0.9f, 0.30f), new Color(110, 184, 255) * 0.78f, 1.15f);
        var tip = mid + fwd * 3.2f;
        Band(G(tip - fwd * 1.35f - rt * 1.35f, 0.32f), G(tip, 0.32f), Color.White * 0.92f, 1.3f);
        Band(G(tip - fwd * 1.35f + rt * 1.35f, 0.32f), G(tip, 0.32f), Color.White * 0.92f, 1.3f);
    }

    /// 3D translucent ground-projected Command Banner (Pillar 1 P1.3): tall steel-blue heraldic standard on ground hover,
    /// snapping to crimson with crossed swords when hovering an enemy company; gently distance-scaled for readability across the valley.
    void DrawCommandBanner(Vector2 pos, bool hostile)
    {
        float gy = battle.Terrain.Height(pos);
        var basePos = new Vector3(pos.X, gy + 0.24f, pos.Y);
        var toCam3 = eye - basePos;
        float camDist = toCam3.Length();
        float sc = Math.Clamp(camDist / 26f, 0.9f, 2.4f);

        var toCam = new Vector3(eye.X - pos.X, 0, eye.Z - pos.Y);
        if (toCam.LengthSquared() < 1e-4f) toCam = Vector3.UnitZ;
        toCam.Normalize();
        var right = Vector3.Normalize(Vector3.Cross(Vector3.Up, toCam));
        var fwd = toCam * (0.05f * sc);

        Color main = hostile ? new Color(196, 34, 34) : new Color(36, 102, 212);
        Color topCol = hostile ? new Color(238, 66, 60) : new Color(76, 148, 240);
        Color trim = Gold;

        // Ground beacon rings + soft disc.
        DrawFootprintDisc(pos, (hostile ? 1.85f : 1.15f) * MathF.Min(sc, 1.6f), main * 0.44f, main * 0.08f, 0.26f);
        Ring(pos, (hostile ? 1.9f : 1.15f) * MathF.Min(sc, 1.6f), main * 0.92f, 16, 1.1f);
        DashedRing(pos, (hostile ? 2.6f : 1.75f) * MathF.Min(sc, 1.6f), trim * 0.85f);

        // Tall vertical pole.
        float poleH = (hostile ? 5.4f : 4.4f) * sc;
        float pw = 0.075f * sc;
        Quad3(basePos - right * pw, basePos + right * pw,
            basePos + right * pw + Vector3.Up * poleH, basePos - right * pw + Vector3.Up * poleH,
            trim * 0.55f, trim * 0.96f);

        // Crossbar at top of banner.
        float halfW = (hostile ? 1.05f : 0.84f) * sc;
        float barY = poleH - 0.18f * sc;
        Quad3(basePos - right * (halfW + 0.15f * sc) + Vector3.Up * (barY - 0.07f * sc),
            basePos + right * (halfW + 0.15f * sc) + Vector3.Up * (barY - 0.07f * sc),
            basePos + right * (halfW + 0.15f * sc) + Vector3.Up * (barY + 0.07f * sc),
            basePos - right * (halfW + 0.15f * sc) + Vector3.Up * (barY + 0.07f * sc),
            trim * 0.92f, trim);

        // Tall heraldic swallowtail/pointed banner cloth with high contrast against sky and clouds.
        float clothTop = barY - 0.05f * sc;
        float clothMid = clothTop - (hostile ? 1.95f : 1.65f) * sc;
        float clothTip = clothMid - 0.72f * sc;
        Quad3(basePos - right * halfW + Vector3.Up * clothMid,
            basePos + right * halfW + Vector3.Up * clothMid,
            basePos + right * halfW + Vector3.Up * clothTop,
            basePos - right * halfW + Vector3.Up * clothTop,
            main * 0.88f, topCol * 0.92f);
        bands.Add(new(basePos - right * halfW + Vector3.Up * clothMid, main * 0.88f));
        bands.Add(new(basePos + right * halfW + Vector3.Up * clothMid, main * 0.88f));
        bands.Add(new(basePos + Vector3.Up * clothTip, main * 0.62f));

        // Aged-brass side trim borders.
        float bw = 0.08f * sc;
        Quad3(basePos - right * halfW + Vector3.Up * clothMid + fwd, basePos - right * (halfW - bw) + Vector3.Up * clothMid + fwd,
            basePos - right * (halfW - bw) + Vector3.Up * clothTop + fwd, basePos - right * halfW + Vector3.Up * clothTop + fwd,
            trim * 0.85f, trim);
        Quad3(basePos + right * (halfW - bw) + Vector3.Up * clothMid + fwd, basePos + right * halfW + Vector3.Up * clothMid + fwd,
            basePos + right * halfW + Vector3.Up * clothTop + fwd, basePos + right * (halfW - bw) + Vector3.Up * clothTop + fwd,
            trim * 0.85f, trim);

        // Center emblem with subtle dark shadow backing so white emblem stays crisp over bright clouds.
        var center = basePos + Vector3.Up * ((clothTop + clothMid) * 0.5f) + fwd * 1.5f;
        var shadowOff = -Vector3.Up * (0.05f * sc) - fwd * 0.4f;
        Color emb = Color.White * 0.98f;
        Color embShadow = Color.Black * 0.55f;
        if (hostile)
        {
            void Blade(float sign, Vector3 off, Color cBot, Color cTop)
            {
                var d = Vector3.Normalize(right * sign * 0.78f + Vector3.Up * 0.82f);
                var n = Vector3.Normalize(Vector3.Cross(d, toCam)) * (0.085f * sc);
                var c = center + off;
                var a0 = c - d * (0.68f * sc);
                var a1 = c + d * (0.74f * sc);
                Quad3(a0 - n, a0 + n, a1 + n, a1 - n, cBot, cTop);
                var g0 = c - d * (0.40f * sc);
                Quad3(g0 - n * 2.7f, g0 + n * 2.7f, g0 + d * (0.10f * sc) + n * 2.7f, g0 + d * (0.10f * sc) - n * 2.7f, cBot, cBot);
            }
            Blade(1f, shadowOff, embShadow, embShadow);
            Blade(-1f, shadowOff, embShadow, embShadow);
            Blade(1f, Vector3.Zero, trim, emb);
            Blade(-1f, Vector3.Zero, trim, emb);
        }
        else
        {
            void Chev(Vector3 off, Color cBot, Color cTop)
            {
                var c = center + off;
                var apex = c + Vector3.Up * (0.34f * sc);
                var leftEnd = c - right * (0.48f * sc) - Vector3.Up * (0.16f * sc);
                var rightEnd = c + right * (0.48f * sc) - Vector3.Up * (0.16f * sc);
                var thick = Vector3.Up * (0.15f * sc);
                Quad3(leftEnd - thick, apex - thick, apex, leftEnd, cBot, cTop);
                Quad3(apex - thick, rightEnd - thick, rightEnd, apex, cBot, cTop);
            }
            Chev(shadowOff, embShadow, embShadow);
            Chev(Vector3.Zero, emb * 0.9f, emb);
        }
    }
}
