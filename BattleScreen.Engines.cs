using Microsoft.Xna.Framework;

namespace Ironfield;

/// Engines of war you can see (docs/archive/SIEGECRAFT.md A6). Drawing only: every engine is built in code from a few dozen beams (thick
/// camera-facing ribbons, so a frame reads as timber from far away and as a frame up close) in its own pose: a stone-thrower's arm
/// swings through its throw and is wound back as it reloads, a bolt-thrower's string is drawn back and loaded, a ram swings its trunk,
/// a siege tower lowers its bridge when it docks. Shots in flight are drawn too, and (for your own) the reload, the reach and the mark.
sealed partial class BattleScreen
{
    static readonly Color EngWood = new(128, 94, 58), EngDark = new(84, 60, 38), EngIron = new(118, 122, 130), EngRope = new(196, 178, 134), EngStone = new(112, 108, 100);

    /// An engine's own frame: origin on the ground, right, up and forward (the way it faces).
    readonly struct EFrame
    {
        public readonly Vector3 O, R, F;
        public EFrame(Vector3 o, Vector2 front) { O = o; F = new Vector3(front.X, 0, front.Y); R = new Vector3(-front.Y, 0, front.X); }
        public Vector3 At(float r, float u, float f) => O + R * r + Vector3.UnitY * u + F * f;
    }

    /// A timber: a ribbon of half-width w (never thinner than a few pixels, so it holds at a distance).
    void Timber(Vector3 a, Vector3 b, float w, Color c)
    {
        float k = Vector3.Distance((a + b) * 0.5f, eye) * 0.0016f;
        w = MathF.Max(w, k);
        BeamQuad(a, b, w, w, c, c);
    }

    void EngineLines()
    {
        if (battle.Engines.Count == 0 && battle.EngineShots.Count == 0) return;
        var eye2 = new Vector2(eye.X, eye.Z);
        foreach (var e in battle.Engines)
        {
            if (Vector2.DistanceSquared(e.Pos, eye2) > 320 * 320) continue;
            DrawEngine(e);
        }
        foreach (var s in battle.EngineShots)
        {
            var dir = s.Vel.LengthSquared() > 1e-4f ? Vector3.Normalize(s.Vel) : Vector3.Down;
            if (s.From.Def.Shot == "stone")
            {
                float r = 0.2f + s.From.Def.Splash * 0.05f;
                Timber(s.Pos - Vector3.UnitX * r, s.Pos + Vector3.UnitX * r, r * 0.9f, EngStone);
                Timber(s.Pos - Vector3.UnitY * r, s.Pos + Vector3.UnitY * r, r * 0.9f, EngStone);
                Line(s.Pos, s.Pos - dir * 1.8f, new Color(150, 144, 132)); // the streak it leaves
            }
            else
            {
                float len = s.From.Def.Id == "springald" ? 2.2f : 1.6f;
                Timber(s.Pos, s.Pos - dir * len, 0.05f, EngWood);
                Timber(s.Pos, s.Pos - dir * 0.3f, 0.07f, EngIron); // the head
                Line(s.Pos - dir * len, s.Pos - dir * (len + 1.6f), ArrowStreak);
            }
        }
        EngineMarks();
    }

    void DrawEngine(Engine e)
    {
        var fr = new EFrame(G(e.Pos, 0), e.Front);
        int pal = e.Crew.Units.Count > 0 ? e.Crew.Units[0].Palette : e.Team;
        var cloth = Style.Sides[Math.Clamp(pal, 0, Style.Sides.Length - 1)].Cloth;
        if (e.Wrecked) { Wreck(e, fr); return; }
        switch (e.Def.Shot)
        {
            case "ram": Ram(e, fr, cloth); break;
            case "tower": Tower(e, fr, cloth); break;
            case "stone": Thrower(e, fr, cloth, e.Def.Id == "trebuchet"); break;
            default: Bolter(e, fr, cloth, e.Def.Id == "springald" ? 1.45f : 1); break;
        }
    }

    /// How wound a bolt-thrower is, 0 just loosed to 1 loaded.
    static float Wound(Engine e) => e.Reload <= 0 ? 1 : 1 - Math.Clamp(e.Reload / e.Def.Reload, 0, 1);

    /// A thrower's arm: the angle from straight up (long end ahead of that), cocked back and down while loaded, flung forward in
    /// 0.4 s at the loose, then wound slowly back as the crew work.
    static float ArmAngle(Engine e)
    {
        const float Cocked = -2.2f, Flung = 1.3f, T = 0.4f;
        if (e.Reload <= 0) return Cocked;
        float since = Math.Clamp(e.Def.Reload - e.Reload, 0, e.Def.Reload);
        return since < T ? MathHelper.Lerp(Cocked, Flung, since / T)
            : MathHelper.Lerp(Flung, Cocked, Math.Clamp((since - T) / MathF.Max(0.1f, e.Def.Reload - T), 0, 1));
    }

    void Wheel(EFrame fr, float side, float f, float radius, float phase, float w = 0.05f)
    {
        const int n = 8;
        var hub = fr.At(side, radius, f);
        Vector3 Rim(int i) { float a = phase + i * MathF.Tau / n; return hub + Vector3.UnitY * MathF.Cos(a) * radius + fr.F * MathF.Sin(a) * radius; }
        for (int i = 0; i < n; i++)
        {
            Timber(Rim(i), Rim(i + 1), w, EngDark);
            if (i % 2 == 0) Timber(Rim(i), Rim(i + n / 2), w * 0.7f, EngWood); // spokes
        }
    }

    void Pennant(EFrame fr, float r, float f, float top, Color cloth, float k = 1)
    {
        Timber(fr.At(r, top - 1.1f * k, f), fr.At(r, top, f), 0.03f, EngDark);
        float sway = MathF.Sin(realTime * 4 + f * 3) * 0.12f * k;
        Timber(fr.At(r, top - 0.05f * k, f), fr.At(r + sway, top - 0.3f * k, f - 0.7f * k), 0.12f * k, cloth);
    }

    /// Scorpion and springald: a cart with a stock, two bow arms and a string drawn back to a loaded bolt.
    void Bolter(Engine e, EFrame fr, Color cloth, float s)
    {
        float wound = Wound(e);
        Wheel(fr, -0.6f * s, -0.2f * s, 0.4f * s, (e.Pos.X + e.Pos.Y) / (0.4f * s));
        Wheel(fr, 0.6f * s, -0.2f * s, 0.4f * s, (e.Pos.X + e.Pos.Y) / (0.4f * s));
        Timber(fr.At(-0.6f * s, 0.4f * s, -0.2f * s), fr.At(0.6f * s, 0.4f * s, -0.2f * s), 0.05f, EngDark); // axle
        Timber(fr.At(0, 0.4f * s, -1.1f * s), fr.At(0, 0.7f * s, -0.2f * s), 0.05f, EngDark); // the trail leg
        Timber(fr.At(0, 0.8f * s, -1.1f * s), fr.At(0, 0.9f * s, 1.1f * s), 0.09f * s, EngWood); // the stock
        Timber(fr.At(0, 0.4f * s, -0.2f * s), fr.At(0, 0.85f * s, -0.2f * s), 0.06f * s, EngDark); // the post it turns on
        Timber(fr.At(0, 0.9f * s, 0.9f * s), fr.At(0, 1.35f * s, 0.9f * s), 0.05f * s, EngDark); // the frame the arms are set in
        float tip = 0.9f * s - 0.4f * s * wound; // the arms flex back as it is wound
        var l = fr.At(-1.15f * s, 0.95f * s, tip);
        var r = fr.At(1.15f * s, 0.95f * s, tip);
        var mid = fr.At(0, 0.95f * s, 0.9f * s);
        Timber(mid, l, 0.07f * s, EngWood);
        Timber(mid, r, 0.07f * s, EngWood);
        var slide = fr.At(0, 0.97f * s, MathHelper.Lerp(0.85f * s, -0.7f * s, wound));
        Timber(l, slide, 0.012f, EngRope);
        Timber(r, slide, 0.012f, EngRope);
        if (wound > 0.9f) { Timber(slide, fr.At(0, 1.0f * s, 1.25f * s), 0.035f * s, EngWood); Timber(fr.At(0, 1.0f * s, 1.05f * s), fr.At(0, 1.0f * s, 1.3f * s), 0.05f * s, EngIron); } // the bolt, laid on
        if (s > 1.2f)
        {
            Timber(fr.At(-0.6f * s, 0.4f * s, -0.2f * s), fr.At(0, 0.9f * s, 0.9f * s), 0.04f, EngDark); // braces to the frame
            Timber(fr.At(0.6f * s, 0.4f * s, -0.2f * s), fr.At(0, 0.9f * s, 0.9f * s), 0.04f, EngDark);
        }
        Pennant(fr, 0, -0.9f * s, 2.0f * s, cloth, 0.7f);
    }

    /// Mangonel and trebuchet: a frame, an arm on an axle, a sling and a stone; the trebuchet's counterweight hangs from the short end.
    void Thrower(Engine e, EFrame fr, Color cloth, bool great)
    {
        float k = great ? 3.2f : 1, a = ArmAngle(e);
        float h = 1.3f * k, wide = great ? 1.3f : 0.75f, len = great ? 2.4f : 1.5f;
        // the base and the two side frames
        foreach (float side in new[] { -wide, wide })
        {
            Timber(fr.At(side, 0.2f, -len * 1.2f), fr.At(side, 0.2f, len * 1.2f), 0.1f, EngDark);
            Timber(fr.At(side, 0.2f, -len * 0.7f), fr.At(side, h, 0), 0.08f, EngWood);
            Timber(fr.At(side, 0.2f, len * 0.7f), fr.At(side, h, 0), 0.08f, EngWood);
            Timber(fr.At(side, 0.2f + h * 0.4f, -len * 0.7f * 0.6f), fr.At(side, 0.2f + h * 0.4f, len * 0.7f * 0.6f), 0.05f, EngDark); // the tie
        }
        Timber(fr.At(-wide, 0.2f, -len * 1.2f), fr.At(wide, 0.2f, -len * 1.2f), 0.09f, EngDark);
        Timber(fr.At(-wide, 0.2f, len * 1.2f), fr.At(wide, 0.2f, len * 1.2f), 0.09f, EngDark);
        Timber(fr.At(-wide, h, 0), fr.At(wide, h, 0), 0.07f, EngIron); // the axle
        var pivot = fr.At(0, h, 0);
        var dir = fr.F * MathF.Sin(a) + Vector3.UnitY * MathF.Cos(a);
        float longEnd = great ? 6.4f : 2.7f, shortEnd = great ? 1.7f : 0.9f;
        var tip = pivot + dir * longEnd;
        var back = pivot - dir * shortEnd;
        Timber(back, tip, great ? 0.14f : 0.1f, EngWood);
        if (great)
        {
            // the counterweight: a box that hangs from the short end
            var c = back - Vector3.UnitY * 1.0f;
            Timber(back, c + Vector3.UnitY * 0.5f, 0.03f, EngIron);
            Timber(c - fr.R * 0.7f, c + fr.R * 0.7f, 0.55f, EngDark);
            Timber(c - fr.R * 0.7f + Vector3.UnitY * 0.5f, c + fr.R * 0.7f + Vector3.UnitY * 0.5f, 0.1f, EngIron);
        }
        else Timber(back - fr.R * 0.3f, back + fr.R * 0.3f, 0.2f, EngDark); // a bucket of stones
        // the sling and its stone
        float sl = great ? 3.2f : 1.4f;
        var sd = Vector3.Normalize(-dir + fr.F * 0.8f - Vector3.UnitY * 0.9f);
        var end = tip + sd * sl;
        float ground = battle.Terrain.Height(new Vector2(end.X, end.Z)) + 0.15f;
        if (end.Y < ground) end.Y = ground;
        Timber(tip, end, 0.02f, EngRope);
        bool loaded = e.Reload <= 0 || (e.Def.Reload - e.Reload) > 0.4f;
        if (loaded) { float r = great ? 0.28f : 0.2f; Timber(end - fr.R * r, end + fr.R * r, r, EngStone); }
        // the windlass behind, and the rope that draws the arm down
        var wind = fr.At(0, 0.5f, -len * 1.2f);
        Timber(wind - fr.R * 0.4f, wind + fr.R * 0.4f, 0.07f, EngDark);
        if (loaded) Timber(tip, wind, 0.015f, EngRope);
        Pennant(fr, wide, 0, h + 1.4f * (great ? 1.4f : 1), cloth, great ? 1.4f : 1);
        if (!great) Wheel(fr, wide + 0.12f, len * 1.2f, 0.35f, (e.Pos.X + e.Pos.Y) / 0.35f);
    }

    /// A ram: a hide roof on wheels with the trunk slung under it, beating at the gate when it gets there.
    void Ram(Engine e, EFrame fr, Color cloth)
    {
        float roofLo = 1.7f, roofHi = 2.5f, half = 1.15f, len = 3.6f;
        foreach (float side in new[] { -half, half })
        {
            Timber(fr.At(side, 0.35f, -len), fr.At(side, 0.35f, len), 0.09f, EngDark);
            for (int i = 0; i <= 3; i++) Timber(fr.At(side, 0.35f, -len + i * len * 2 / 3), fr.At(side, roofLo, -len + i * len * 2 / 3), 0.07f, EngWood);
            Timber(fr.At(side, roofLo, -len), fr.At(side, roofLo, len), 0.08f, EngWood);
            Wheel(fr, side * 1.08f, -len * 0.6f, 0.4f, (e.Pos.X + e.Pos.Y) / 0.4f);
            Wheel(fr, side * 1.08f, len * 0.6f, 0.4f, (e.Pos.X + e.Pos.Y) / 0.4f);
        }
        Timber(fr.At(0, roofHi, -len), fr.At(0, roofHi, len), 0.1f, EngWood);
        for (int i = -4; i <= 4; i++)
        {
            float r = i * half / 4.2f, y = roofLo + (roofHi - roofLo) * (1 - MathF.Abs(r) / half);
            Timber(fr.At(r, y, -len), fr.At(r, y, len), 0.17f, Color.Lerp(cloth, new Color(150, 120, 86), 0.55f)); // hides, stained with the side's colour
        }
        bool beating = Vector2.Distance(e.Pos, e.Goal) < 0.5f && battle.Terrain.Wall != null;
        float swing = beating ? MathF.Sin(e.Reload / 1.4f * MathF.Tau) * 0.9f : MathF.Sin(realTime * 1.5f) * 0.05f;
        float y0 = 1.1f;
        Timber(fr.At(0, y0, -len * 0.8f + swing), fr.At(0, y0, len + 0.6f + swing), 0.17f, EngWood);
        Timber(fr.At(0, y0, len + 0.35f + swing), fr.At(0, y0, len + 0.75f + swing), 0.21f, EngIron); // the iron head
        foreach (float f in new[] { -len * 0.4f, len * 0.4f }) // slung from the ridge
        {
            Timber(fr.At(0, roofHi, f), fr.At(0, y0 + 0.15f, f + swing * 0.9f), 0.02f, EngRope);
        }
        Pennant(fr, 0, -len, roofHi + 1.4f, cloth);
    }

    /// A siege tower: a tall timber frame clad in hides, three floors; its bridge stands up against the face until the tower docks.
    void Tower(Engine e, EFrame fr, Color cloth)
    {
        float half = 2.4f, h = 9, hide = 0.28f;
        var skin = Color.Lerp(cloth, new Color(150, 120, 86), 0.55f);
        foreach (float rr in new[] { -half, half })
            foreach (float ff in new[] { -half, half })
            {
                Timber(fr.At(rr, 0.8f, ff), fr.At(rr * 0.85f, h, ff * 0.85f), 0.12f, EngWood); // the posts lean in a little
                Wheel(fr, rr * 1.08f, ff, 0.5f, (e.Pos.X + e.Pos.Y) / 0.5f);
            }
        for (int lv = 0; lv <= 3; lv++)
        {
            float y = 0.8f + lv * (h - 0.8f) / 3, sc = 1 - 0.15f * y / h;
            Timber(fr.At(-half * sc, y, -half * sc), fr.At(half * sc, y, -half * sc), 0.09f, EngDark);
            Timber(fr.At(-half * sc, y, half * sc), fr.At(half * sc, y, half * sc), 0.09f, EngDark);
            Timber(fr.At(-half * sc, y, -half * sc), fr.At(-half * sc, y, half * sc), 0.09f, EngDark);
            Timber(fr.At(half * sc, y, -half * sc), fr.At(half * sc, y, half * sc), 0.09f, EngDark);
        }
        for (float y = 1.2f; y < h - 0.3f; y += 0.55f)
        {
            float sc = 1 - 0.15f * y / h;
            Timber(fr.At(-half * sc, y, half * sc), fr.At(half * sc, y, half * sc), hide, skin);   // the face
            Timber(fr.At(-half * sc, y, -half * sc), fr.At(half * sc, y, -half * sc), hide, skin);  // the back
            Timber(fr.At(-half * sc, y, -half * sc), fr.At(-half * sc, y, half * sc), hide, skin); // the sides
            Timber(fr.At(half * sc, y, -half * sc), fr.At(half * sc, y, half * sc), hide, skin);
        }
        Timber(fr.At(-half, 0.9f, half), fr.At(half * 0.85f, h, half * 0.85f), 0.06f, EngDark); // braces
        Timber(fr.At(half, 0.9f, half), fr.At(-half * 0.85f, h, half * 0.85f), 0.06f, EngDark);
        float top = h - 0.9f, fy = half * 0.85f;
        for (float r = -half * 0.85f; r <= half * 0.85f; r += 0.6f) Timber(fr.At(r, h, fy), fr.At(r, h + 0.55f, fy), 0.1f, EngWood); // the crenels
        if (e.Docked)
        {
            Timber(fr.At(0, top, fy), fr.At(0, top - 0.1f, fy + 3.3f), 0.8f, EngWood); // the bridge, down on the walkway
            Timber(fr.At(-0.85f, top + 0.5f, fy), fr.At(-0.85f, top + 0.4f, fy + 3.3f), 0.05f, EngRope);
            Timber(fr.At(0.85f, top + 0.5f, fy), fr.At(0.85f, top + 0.4f, fy + 3.3f), 0.05f, EngRope);
        }
        else Timber(fr.At(0, top - 2.6f, fy + 0.1f), fr.At(0, top + 0.5f, fy + 0.1f), 0.8f, EngWood); // raised against the face
        Pennant(fr, 0, -half * 0.6f, h + 2.2f, cloth, 1.4f);
    }

    /// What is left of a wrecked engine: broken timbers on the ground and a bent frame.
    void Wreck(Engine e, EFrame fr)
    {
        float s = e.Def.Shot is "tower" ? 2.2f : e.Def.Shot == "ram" ? 1.6f : e.Def.Id == "trebuchet" ? 1.9f : 1;
        for (int i = 0; i < 9; i++)
        {
            float a = Ease.Hash(e.Index * 31 + i * 7) * MathF.Tau, d = Ease.Hash(e.Index * 17 + i * 13) * 1.8f * s, len = (0.6f + Ease.Hash(i * 5 + e.Index) * 1.2f) * s;
            var c = fr.At(MathF.Cos(a) * d, 0.15f, MathF.Sin(a) * d);
            var dv = fr.R * MathF.Cos(a + i) + fr.F * MathF.Sin(a + i);
            Timber(c - dv * len * 0.5f, c + dv * len * 0.5f + Vector3.UnitY * (i % 3 == 0 ? 0.5f : 0.05f), 0.08f, i % 2 == 0 ? EngWood : EngDark);
        }
        Timber(fr.At(0, 0.1f, 0), fr.At(0.3f * s, 0.9f * s, 0.2f * s), 0.1f, EngDark);
    }

    /// For your own engines: the reload over each, and (for the ones you have picked, or work yourself) their reach, their mark and,
    /// when you work one, where the shot will fall and the arc it flies.
    void EngineMarks()
    {
        foreach (var e in battle.Engines)
        {
            if (e.Team != 0 || e.Wrecked || e.Def.Rolls) continue;
            var top = G(e.Pos, e.Def.Id == "trebuchet" ? 8.4f : e.Def.Shot == "stone" ? 3 : 2.3f);
            float ready = Wound(e);
            var left = top - Vector3.Normalize(Vector3.Cross(Vector3.UnitY, eye - top)) * 0.7f;
            var right = top + Vector3.Normalize(Vector3.Cross(Vector3.UnitY, eye - top)) * 0.7f;
            Timber(left, right, 0.08f, new Color(20, 20, 20));
            Timber(left, Vector3.Lerp(left, right, ready), 0.06f, ready >= 1 ? Gold : Friendly);
            bool mine = battle.Manned == e, picked = selected.Contains(e.Crew);
            if (!mine && !picked) continue;
            DashedRing(e.Pos, e.Def.Range, (mine ? Gold : Sel) * 0.5f, 28, 0.7f);
            if (e.Def.MinRange > 0) DashedRing(e.Pos, e.Def.MinRange, Foe * 0.45f, 18, 0.6f);
            if (e.Mark is Vector2 m)
            {
                Band(G(e.Pos, 0.2f), G(m, 0.2f), Sel * 0.6f, 0.7f);
                Ring(m, MathF.Max(1, e.Def.Splash), Sel * 0.85f, 14);
            }
            else if (e.Crew.Target is { Alive: > 0 } t)
            {
                Band(G(e.Pos, 0.2f), G(t.Centroid(), 0.2f), Foe * 0.65f, 0.7f);
                Ring(t.Centroid(), 3, Foe * 0.8f, 14);
            }
            if (mine && battle.Input.EngineAim is Vector2 want)
            {
                var to = want - e.Pos;
                float r = to.Length();
                bool short_ = r < e.Def.MinRange;
                if (r > e.Def.Range) { want = e.Pos + to / r * e.Def.Range; r = e.Def.Range; }
                var col = short_ ? Foe : ready >= 1 ? Gold : Color.White * 0.7f;
                Ring(want, MathF.Max(1, e.Def.Splash), col * 0.9f, 14, 1.1f);
                // the arc it would fly (the shot's own flight, see Battle.Launch)
                float time = MathF.Max(0.1f, r / e.Def.Speed), stone = e.Def.Shot == "stone" ? 4 : 1.4f;
                float h0 = battle.Terrain.Height(e.Pos) + stone, h1 = battle.Terrain.Height(want) + (e.Def.Shot == "stone" ? 0 : 1.1f);
                var prev = new Vector3(e.Pos.X, h0, e.Pos.Y);
                for (int i = 1; i <= 18; i++)
                {
                    float u = i / 18f;
                    var p = e.Pos + (want - e.Pos) * u;
                    var next = new Vector3(p.X, h0 + (h1 - h0) * u + 0.5f * Battle.Gravity * time * time * u * (1 - u), p.Y);
                    if (i % 2 == 1) Line(prev, next, col);
                    prev = next;
                }
            }
        }
    }
}
