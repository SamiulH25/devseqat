using Microsoft.Xna.Framework;

namespace Ironfield;

/// Solid, lit 3D medieval siege engines emitted directly into `units3D` (VertexPNCT) so they receive directional sunlight,
/// crisp pixel-art outlines, real sun shadows, and proper depth occlusion against soldiers and fortress walls.
sealed partial class BattleScreen
{
    static readonly Color OakLight = new(134, 96, 58), OakMid = new(106, 74, 44), OakDark = new(72, 49, 29),
        IronLight = new(132, 138, 148), IronDark = new(74, 78, 86), HideBase = new(126, 98, 68),
        RopeCol = new(186, 166, 122), StoneCol = new(124, 120, 112);

    void DrawEngines3D()
    {
        if (battle.Engines.Count == 0) return;
        var eye2 = new Vector2(eye.X, eye.Z);
        foreach (var e in battle.Engines)
        {
            if (Vector2.DistanceSquared(e.Pos, eye2) > 320 * 320) continue;
            var fr = new EFrame(G(e.Pos, 0), e.Front);
            int pal = e.Crew.Units.Count > 0 ? e.Crew.Units[0].Palette : e.Team;
            var h = Style.Sides[Math.Clamp(pal, 0, Style.Sides.Length - 1)];
            if (e.Wrecked) { Wreck3D(e, fr); continue; }
            switch (e.Def.Shot)
            {
                case "ram": Ram3D(e, fr, h.Cloth, h.Trim); break;
                case "tower": Tower3D(e, fr, h.Cloth, h.Trim); break;
                case "stone": Thrower3D(e, fr, h.Cloth, h.Trim, e.Def.Id == "trebuchet"); break;
                default: Bolter3D(e, fr, h.Cloth, h.Trim, e.Def.Id == "springald" ? 1.4f : 1f); break;
            }
        }
    }

    void Quad3D(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color col)
    {
        units3D.Reserve(6);
        var dst = units3D.A;
        int i = units3D.N;
        dst[i] = new VertexPNCT(a, n, col, Vector2.Zero);
        dst[i + 1] = new VertexPNCT(b, n, col, Vector2.Zero);
        dst[i + 2] = new VertexPNCT(c, n, col, Vector2.Zero);
        dst[i + 3] = new VertexPNCT(a, n, col, Vector2.Zero);
        dst[i + 4] = new VertexPNCT(c, n, col, Vector2.Zero);
        dst[i + 5] = new VertexPNCT(d, n, col, Vector2.Zero);
        units3D.N = i + 6;
    }

    void Tri3D(Vector3 a, Vector3 b, Vector3 c, Vector3 n, Color col)
    {
        units3D.Reserve(3);
        var dst = units3D.A;
        int i = units3D.N;
        dst[i] = new VertexPNCT(a, n, col, Vector2.Zero);
        dst[i + 1] = new VertexPNCT(b, n, col, Vector2.Zero);
        dst[i + 2] = new VertexPNCT(c, n, col, Vector2.Zero);
        units3D.N = i + 3;
    }

    /// Solid axis-aligned box in an engine's local frame (r0..r1 right, u0..u1 up, f0..f1 forward).
    void Box3D(EFrame fr, float r0, float u0, float f0, float r1, float u1, float f1, Color col)
    {
        var p000 = fr.At(r0, u0, f0); var p100 = fr.At(r1, u0, f0);
        var p010 = fr.At(r0, u1, f0); var p110 = fr.At(r1, u1, f0);
        var p001 = fr.At(r0, u0, f1); var p101 = fr.At(r1, u0, f1);
        var p011 = fr.At(r0, u1, f1); var p111 = fr.At(r1, u1, f1);
        Quad3D(p010, p110, p111, p011, Vector3.UnitY, col);       // top
        Quad3D(p001, p101, p100, p000, -Vector3.UnitY, col);      // bottom
        Quad3D(p001, p011, p111, p101, fr.F, col);                // front
        Quad3D(p100, p110, p010, p000, -fr.F, col);               // back
        Quad3D(p101, p111, p110, p100, fr.R, col);                // right
        Quad3D(p000, p010, p011, p001, -fr.R, col);               // left
    }

    /// Solid 4-sided timber beam from a to b with half-thickness hw.
    void Beam3D(Vector3 a, Vector3 b, float hw, Color col)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 1e-4f) return;
        d /= len;
        var up = MathF.Abs(d.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        var s1 = Vector3.Normalize(Vector3.Cross(d, up)) * hw;
        var s2 = Vector3.Normalize(Vector3.Cross(d, s1)) * hw;
        var n1 = Vector3.Normalize(s1);
        var n2 = Vector3.Normalize(s2);
        var a0 = a - s1 - s2; var a1 = a + s1 - s2; var a2 = a + s1 + s2; var a3 = a - s1 + s2;
        var b0 = b - s1 - s2; var b1 = b + s1 - s2; var b2 = b + s1 + s2; var b3 = b - s1 + s2;
        Quad3D(a0, a1, b1, b0, -n2, col);
        Quad3D(a1, a2, b2, b1, n1, col);
        Quad3D(a2, a3, b3, b2, n2, col);
        Quad3D(a3, a0, b0, b3, -n1, col);
        Quad3D(b0, b1, b2, b3, d, col);
        Quad3D(a3, a2, a1, a0, -d, col);
    }

    /// Solid 3D spoked wooden wheel with iron rim and hub.
    void Wheel3D(EFrame fr, float side, float f, float radius, float phase, float thick = 0.14f)
    {
        const int segs = 8;
        var hub = fr.At(side, radius, f);
        float ht = thick * 0.5f;
        var rimOut = fr.R * ht;
        for (int i = 0; i < segs; i++)
        {
            float a0 = phase + i * MathF.Tau / segs, a1 = phase + (i + 1) * MathF.Tau / segs;
            var d0 = fr.F * MathF.Cos(a0) + Vector3.UnitY * MathF.Sin(a0);
            var d1 = fr.F * MathF.Cos(a1) + Vector3.UnitY * MathF.Sin(a1);
            var p0 = hub + d0 * radius; var p1 = hub + d1 * radius;
            var i0 = hub + d0 * (radius * 0.72f); var i1 = hub + d1 * (radius * 0.72f);
            var nOut = Vector3.Normalize(d0 + d1);
            Quad3D(p0 - rimOut, p1 - rimOut, p1 + rimOut, p0 + rimOut, nOut, IronDark); // iron tire
            Quad3D(i0 + rimOut, i1 + rimOut, p1 + rimOut, p0 + rimOut, fr.R, OakMid);
            Quad3D(p0 - rimOut, p1 - rimOut, i1 - rimOut, i0 - rimOut, -fr.R, OakMid);
            if (i % 2 == 0) Beam3D(hub, hub + d0 * (radius * 0.8f), 0.045f, OakDark); // spokes
        }
        Box3D(fr, side - ht * 1.3f, radius - 0.14f, f - 0.14f, side + ht * 1.3f, radius + 0.14f, f + 0.14f, IronLight);
    }

    void Pennant3D(EFrame fr, float r, float f, float y0, float h, Color cloth, Color trim)
    {
        var b = fr.At(r, y0, f);
        var t = fr.At(r, y0 + h, f);
        Beam3D(b, t, 0.04f, OakDark);
        float wag = MathF.Sin(realTime * 4.5f + fr.O.X * 0.3f) * 0.16f;
        var p0 = t - Vector3.UnitY * 0.05f;
        var p1 = t - Vector3.UnitY * 0.55f;
        var tip = t - Vector3.UnitY * 0.3f - fr.F * 1.15f + fr.R * wag;
        Tri3D(p0, p1, tip, fr.R, cloth);
        Tri3D(p1, p0, tip, -fr.R, cloth);
        var stripe = Vector3.Lerp(p0, tip, 0.45f);
        Beam3D(p0, stripe, 0.045f, trim);
    }

    /// Solid pitched-roof battering ram with rawhide-clad timber shed, iron strapping, spoked wheels and swinging iron-capped beam.
    void Ram3D(Engine e, EFrame fr, Color cloth, Color trim)
    {
        float half = 1.25f, len = 3.2f, eave = 1.75f, ridge = 2.65f;
        var hide = Color.Lerp(HideBase, cloth, 0.28f);

        // Heavy timber chassis + 4 iron-rimmed wheels
        foreach (float s in new[] { -half, half })
        {
            Box3D(fr, s - 0.12f, 0.32f, -len, s + 0.12f, 0.52f, len, OakDark);
            for (int i = 0; i <= 3; i++)
            {
                float z = -len + 0.25f + i * (len * 2 - 0.5f) / 3f;
                Box3D(fr, s - 0.1f, 0.5f, z - 0.1f, s + 0.1f, eave, z + 0.1f, OakMid);
            }
            Box3D(fr, s - 0.11f, eave - 0.12f, -len, s + 0.11f, eave + 0.08f, len, OakMid);
            Wheel3D(fr, s + MathF.Sign(s) * 0.16f, -len * 0.55f, 0.44f, (e.Pos.X + e.Pos.Y) / 0.44f);
            Wheel3D(fr, s + MathF.Sign(s) * 0.16f, len * 0.55f, 0.44f, (e.Pos.X + e.Pos.Y) / 0.44f);
            // Low protective side skirts (oak planks)
            Box3D(fr, s - 0.06f, 0.55f, -len + 0.15f, s + 0.06f, 1.35f, len - 0.15f, OakLight);
        }
        // Cross sills
        Box3D(fr, -half, 0.34f, -len + 0.1f, half, 0.50f, -len + 0.34f, OakDark);
        Box3D(fr, -half, 0.34f, len - 0.34f, half, 0.50f, len - 0.1f, OakDark);

        // Solid pitched timber + rawhide roof (both slopes, front/back gable caps, and ridge beam)
        float thick = 0.12f;
        var lBotB = fr.At(-half - 0.15f, eave, -len - 0.1f); var lBotF = fr.At(-half - 0.15f, eave, len + 0.1f);
        var rBotB = fr.At(half + 0.15f, eave, -len - 0.1f); var rBotF = fr.At(half + 0.15f, eave, len + 0.1f);
        var topB = fr.At(0, ridge, -len - 0.1f); var topF = fr.At(0, ridge, len + 0.1f);
        var nL = Vector3.Normalize(-fr.R * (ridge - eave) + Vector3.UnitY * (half + 0.15f));
        var nR = Vector3.Normalize(fr.R * (ridge - eave) + Vector3.UnitY * (half + 0.15f));
        Quad3D(lBotB, topB, topF, lBotF, nL, hide);
        Quad3D(lBotF - Vector3.UnitY * thick, topF - Vector3.UnitY * thick, topB - Vector3.UnitY * thick, lBotB - Vector3.UnitY * thick, -nL, OakDark);
        Quad3D(topB, rBotB, rBotF, topF, nR, hide);
        Quad3D(rBotF - Vector3.UnitY * thick, rBotB - Vector3.UnitY * thick, topB - Vector3.UnitY * thick, topF - Vector3.UnitY * thick, -nR, OakDark);
        // Front gable shield & iron ridge beam
        Tri3D(lBotF, rBotF, topF, fr.F, OakDark);
        Box3D(fr, -0.11f, ridge - 0.08f, -len - 0.12f, 0.11f, ridge + 0.12f, len + 0.12f, OakDark);
        // Iron strapping bands across the roof
        foreach (float z in new[] { -len * 0.65f, 0f, len * 0.65f })
        {
            Beam3D(fr.At(-half - 0.16f, eave + 0.04f, z), fr.At(0, ridge + 0.05f, z), 0.055f, IronDark);
            Beam3D(fr.At(half + 0.16f, eave + 0.04f, z), fr.At(0, ridge + 0.05f, z), 0.055f, IronDark);
        }
        // Faction cloth banner panels along roof eaves
        Box3D(fr, -half - 0.16f, eave - 0.22f, -len * 0.45f, -half - 0.08f, eave + 0.02f, len * 0.45f, cloth);
        Box3D(fr, half + 0.08f, eave - 0.22f, -len * 0.45f, half + 0.16f, eave + 0.02f, len * 0.45f, cloth);

        // Swinging heavy oak ram trunk with forged iron head
        bool beating = Vector2.Distance(e.Pos, e.Goal) < 0.8f && battle.Terrain.Wall != null;
        float swing = beating ? MathF.Sin(e.Reload / 1.4f * MathF.Tau) * 0.85f : MathF.Sin(realTime * 1.5f) * 0.05f;
        float y0 = 1.05f;
        Box3D(fr, -0.20f, y0 - 0.20f, -len * 0.72f + swing, 0.20f, y0 + 0.20f, len + 0.45f + swing, OakLight);
        Box3D(fr, -0.26f, y0 - 0.26f, len + 0.25f + swing, 0.26f, y0 + 0.26f, len + 0.85f + swing, IronLight);
        foreach (float z in new[] { -len * 0.35f, len * 0.35f })
            Beam3D(fr.At(0, ridge - 0.1f, z), fr.At(0, y0 + 0.18f, z + swing * 0.85f), 0.035f, IronDark);
        Pennant3D(fr, 0, -len + 0.2f, ridge, 1.8f, cloth, trim);
    }

    /// Solid 3-story timber-and-rawhide siege tower with crenellations, iron bands, wheels, and hinged drawbridge.
    void Tower3D(Engine e, EFrame fr, Color cloth, Color trim)
    {
        float half = 2.3f, h = 8.4f;
        var hide = Color.Lerp(HideBase, cloth, 0.22f);

        // Heavy timber base deck + 4 large wheels
        Box3D(fr, -half - 0.1f, 0.55f, -half - 0.1f, half + 0.1f, 0.95f, half + 0.1f, OakDark);
        foreach (float s in new[] { -half, half })
            foreach (float f in new[] { -half * 0.68f, half * 0.68f })
                Wheel3D(fr, s + MathF.Sign(s) * 0.18f, f, 0.58f, (e.Pos.X + e.Pos.Y) / 0.58f, 0.18f);

        // 3 tapered solid stories (front, left, right solid rawhide/oak walls; back open at bottom for crew shelter)
        for (int lv = 0; lv < 3; lv++)
        {
            float y0 = 0.95f + lv * (h - 0.95f) / 3f;
            float y1 = 0.95f + (lv + 1) * (h - 0.95f) / 3f;
            float s0 = half * (1f - 0.05f * lv);
            float wt = 0.22f;
            // Solid floor deck
            Box3D(fr, -s0, y0, -s0, s0, y0 + 0.18f, s0, OakDark);
            // Front wall (facing castle)
            Box3D(fr, -s0, y0, s0 - wt, s0, y1, s0, hide);
            // Left & right walls
            Box3D(fr, -s0, y0, -s0, -s0 + wt, y1, s0, OakLight);
            Box3D(fr, s0 - wt, y0, -s0, s0, y1, s0, OakLight);
            // Rear wall on upper two stories (ground floor stays open for crew pushing inside)
            if (lv > 0) Box3D(fr, -s0, y0, -s0, s0, y1, -s0 + wt, OakMid);
            // Horizontal iron strapping band around the story
            Box3D(fr, -s0 - 0.04f, y1 - 0.16f, -s0 - 0.04f, s0 + 0.04f, y1 + 0.02f, s0 + 0.04f, IronDark);
        }

        // Heavy corner oak posts
        foreach (float r in new[] { -half, half })
            foreach (float f in new[] { -half, half })
                Beam3D(fr.At(r, 0.8f, f), fr.At(r * 0.88f, h + 0.1f, f * 0.88f), 0.15f, OakDark);

        // Faction heraldry banners hanging on left & right sides
        Box3D(fr, -half * 0.95f - 0.06f, 4.2f, -0.75f, -half * 0.95f + 0.04f, 6.8f, 0.75f, cloth);
        Box3D(fr, half * 0.95f - 0.04f, 4.2f, -0.75f, half * 0.95f + 0.06f, 6.8f, 0.75f, cloth);
        Box3D(fr, -half * 0.95f - 0.07f, 5.25f, -0.75f, -half * 0.95f + 0.05f, 5.65f, 0.75f, trim);
        Box3D(fr, half * 0.95f - 0.05f, 5.25f, -0.75f, half * 0.95f + 0.07f, 5.65f, 0.75f, trim);

        // Top fighting deck & timber crenellations
        float topS = half * 0.88f;
        Box3D(fr, -topS, h - 0.18f, -topS, topS, h, topS, OakDark);
        for (int i = 0; i < 4; i++)
        {
            float m = -topS + 0.25f + i * (topS * 2 - 0.5f) / 3f;
            Box3D(fr, m - 0.26f, h, topS - 0.18f, m + 0.26f, h + 0.9f, topS + 0.05f, OakMid);
            Box3D(fr, -topS - 0.05f, h, m - 0.26f, -topS + 0.18f, h + 0.8f, m + 0.26f, OakMid);
            Box3D(fr, topS - 0.18f, h, m - 0.26f, topS + 0.05f, h + 0.8f, m + 0.26f, OakMid);
        }

        // Hinged plank drawbridge (lowered onto parapet when docked, raised against front face while rolling)
        float bridgeY = h - 0.7f;
        if (e.Docked)
        {
            Box3D(fr, -1.05f, bridgeY - 0.14f, topS, 1.05f, bridgeY + 0.04f, topS + 3.4f, OakLight);
            Beam3D(fr.At(-1.0f, bridgeY + 1.1f, topS), fr.At(-1.0f, bridgeY + 0.05f, topS + 3.2f), 0.04f, IronDark);
            Beam3D(fr.At(1.0f, bridgeY + 1.1f, topS), fr.At(1.0f, bridgeY + 0.05f, topS + 3.2f), 0.04f, IronDark);
        }
        else
        {
            Box3D(fr, -1.05f, bridgeY - 0.1f, topS + 0.05f, 1.05f, bridgeY + 2.8f, topS + 0.24f, OakLight);
            Box3D(fr, -1.05f, bridgeY + 0.6f, topS + 0.22f, 1.05f, bridgeY + 0.76f, topS + 0.28f, IronDark);
            Box3D(fr, -1.05f, bridgeY + 2.0f, topS + 0.22f, 1.05f, bridgeY + 2.16f, topS + 0.28f, IronDark);
        }
        Pennant3D(fr, 0, -topS + 0.2f, h, 2.2f, cloth, trim);
    }

    /// Solid 3D trebuchet or mangonel with timber A-frame, swinging arm, counterweight box and stone.
    void Thrower3D(Engine e, EFrame fr, Color cloth, Color trim, bool great)
    {
        float wide = great ? 1.85f : 0.95f, len = great ? 3.5f : 1.75f, h = great ? 4.3f : 1.95f;
        float a = ArmAngle(e);
        foreach (float s in new[] { -wide, wide })
        {
            Box3D(fr, s - 0.15f, 0.05f, -len, s + 0.15f, 0.34f, len, OakDark);
            Beam3D(fr.At(s, 0.3f, -len * 0.75f), fr.At(s, h, 0), great ? 0.14f : 0.09f, OakMid);
            Beam3D(fr.At(s, 0.3f, len * 0.75f), fr.At(s, h, 0), great ? 0.14f : 0.09f, OakMid);
            Beam3D(fr.At(s, 0.3f + h * 0.45f, -len * 0.42f), fr.At(s, 0.3f + h * 0.45f, len * 0.42f), 0.08f, OakDark);
            if (!great) Wheel3D(fr, s + MathF.Sign(s) * 0.15f, len * 0.55f, 0.38f, (e.Pos.X + e.Pos.Y) / 0.38f);
        }
        Box3D(fr, -wide, 0.08f, -len, wide, 0.32f, -len + 0.32f, OakDark);
        Box3D(fr, -wide, 0.08f, len - 0.32f, wide, 0.32f, len, OakDark);
        Beam3D(fr.At(-wide - 0.15f, h, 0), fr.At(wide + 0.15f, h, 0), great ? 0.11f : 0.07f, IronLight);

        var pivot = fr.At(0, h, 0);
        var dir = fr.F * MathF.Sin(a) + Vector3.UnitY * MathF.Cos(a);
        float longEnd = great ? 5.8f : 2.5f, shortEnd = great ? 1.6f : 0.8f;
        var tip = pivot + dir * longEnd;
        var back = pivot - dir * shortEnd;
        Beam3D(back, tip, great ? 0.14f : 0.09f, OakLight);

        if (great)
        {
            var cw = back - Vector3.UnitY * 1.15f;
            Beam3D(back, cw + Vector3.UnitY * 0.45f, 0.05f, IronDark);
            var cwFr = new EFrame(cw, e.Front);
            Box3D(cwFr, -0.75f, -0.55f, -0.55f, 0.75f, 0.55f, 0.55f, OakDark);
            Box3D(cwFr, -0.78f, -0.1f, -0.58f, 0.78f, 0.1f, 0.58f, IronDark);
        }
        else
        {
            var cwFr = new EFrame(back, e.Front);
            Box3D(cwFr, -0.3f, -0.25f, -0.25f, 0.3f, 0.25f, 0.25f, OakDark);
        }

        bool loaded = e.Reload <= 0 || (e.Def.Reload - e.Reload) > 0.4f;
        if (loaded)
        {
            float sr = great ? 0.34f : 0.22f;
            var stFr = new EFrame(tip - Vector3.UnitY * sr, e.Front);
            Box3D(stFr, -sr, -sr, -sr, sr, sr, sr, StoneCol);
        }
        Pennant3D(fr, wide, -len * 0.7f, 0.3f, h * 0.75f, cloth, trim);
    }

    /// Solid 3D springald / scorpion bolt-thrower.
    void Bolter3D(Engine e, EFrame fr, Color cloth, Color trim, float sc)
    {
        float h = 1.15f * sc, len = 1.5f * sc, bow = 1.35f * sc;
        Box3D(fr, -0.55f * sc, 0.05f, -0.6f * sc, 0.55f * sc, 0.25f * sc, 0.6f * sc, OakDark);
        Box3D(fr, -0.14f * sc, 0.2f * sc, -0.14f * sc, 0.14f * sc, h, 0.14f * sc, OakMid);
        Box3D(fr, -0.16f * sc, h, -len * 0.6f, 0.16f * sc, h + 0.22f * sc, len * 0.65f, OakLight);
        Box3D(fr, -bow * 0.45f, h - 0.1f * sc, len * 0.35f, bow * 0.45f, h + 0.35f * sc, len * 0.6f, IronDark);
        Beam3D(fr.At(-bow * 0.4f, h + 0.14f * sc, len * 0.48f), fr.At(-bow, h + 0.14f * sc, len * 0.22f), 0.06f * sc, OakLight);
        Beam3D(fr.At(bow * 0.4f, h + 0.14f * sc, len * 0.48f), fr.At(bow, h + 0.14f * sc, len * 0.22f), 0.06f * sc, OakLight);
        float draw = MathHelper.Lerp(len * 0.3f, -len * 0.45f, Wound(e));
        Beam3D(fr.At(-bow, h + 0.14f * sc, len * 0.22f), fr.At(0, h + 0.14f * sc, draw), 0.025f * sc, RopeCol);
        Beam3D(fr.At(bow, h + 0.14f * sc, len * 0.22f), fr.At(0, h + 0.14f * sc, draw), 0.025f * sc, RopeCol);
        if (Wound(e) > 0.25f)
            Beam3D(fr.At(0, h + 0.22f * sc, draw), fr.At(0, h + 0.22f * sc, len * 0.85f), 0.045f * sc, IronLight);
        Pennant3D(fr, 0.5f * sc, -0.5f * sc, 0.2f * sc, 1.3f * sc, cloth, trim);
    }

    void Wreck3D(Engine e, EFrame fr)
    {
        float s = e.Def.Shot == "tower" ? 2.1f : e.Def.Shot == "ram" ? 1.5f : 1.1f;
        for (int i = 0; i < 6; i++)
        {
            float a = Ease.Hash(e.Index * 31 + i * 7) * MathF.Tau;
            float d = Ease.Hash(e.Index * 17 + i * 13) * 1.4f * s;
            float len = (0.8f + Ease.Hash(i * 5 + e.Index) * 1.1f) * s;
            var c = fr.At(MathF.Cos(a) * d, 0.18f, MathF.Sin(a) * d);
            var dv = fr.R * MathF.Cos(a + i) + fr.F * MathF.Sin(a + i);
            Beam3D(c - dv * len * 0.5f, c + dv * len * 0.5f + Vector3.UnitY * (i % 2 == 0 ? 0.35f : 0.05f), 0.11f, i % 2 == 0 ? OakDark : OakMid);
        }
    }
}
