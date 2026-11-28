using Microsoft.Xna.Framework;

namespace Ironfield;

/// Goblin holdouts on the campaign map: a spiked timber palisade around a dug-out warren mound, ramshackle hide huts,
/// a smoking campfire and tiny sentries, scaled by the holdout's men and tier. Drawing only (`World.Extra`).
sealed partial class MapLife
{
    readonly List<(int Id, Vector2 Pos, int Men, int Tier)> holdouts = new();
    readonly List<(int Id, Vector2 Pos, int Men, int Tier, Color Accent)> banditCamps = new();

    static readonly Color
        GoblinEarth = new(82, 68, 50),
        GoblinMud = new(66, 56, 42),
        GoblinTimber = new(92, 68, 44),
        GoblinHide = new(104, 80, 56),
        GoblinHideDark = new(76, 60, 44),
        GoblinSkin = new(96, 120, 66),
        GoblinRag = new(68, 54, 42),
        GoblinBone = new(206, 198, 178),
        GoblinBanner = new(148, 38, 30),
        GoblinEmber = new(220, 88, 28);

    /// Active goblin holdouts on the map this frame.
    public void SetHoldouts(IEnumerable<(int Id, Vector2 Pos, int Men, int Tier)> list)
    {
        holdouts.Clear();
        holdouts.AddRange(list);
    }

    /// Active outlaw camps on the map this frame.
    public void SetBanditCamps(IEnumerable<(int Id, Vector2 Pos, int Men, int Tier, Color Accent)> list)
    {
        banditCamps.Clear();
        banditCamps.AddRange(list);
    }

    void BuildHoldouts()
    {
        foreach (var h in holdouts) DrawHoldout(h.Id, h.Pos, h.Men, h.Tier);
        foreach (var c in banditCamps) DrawBanditCamp(c.Id, c.Pos, c.Men, c.Tier, c.Accent);
    }

    void DrawBanditCamp(int id, Vector2 pos, int men, int tier, Color accent)
    {
        var r = new Random(id * 131 + 29);
        float s = 0.95f + Math.Clamp((men - 15) / 50f, 0f, 0.35f);
        float rad = 2.9f * s;
        float gate = r.NextSingle() * MathF.Tau;
        var gf = new Vector2(MathF.Cos(gate), MathF.Sin(gate));
        var gs = new Vector2(-gf.Y, gf.X);
        var b = new Vector3(pos.X, t.Height(pos), pos.Y);
        mesh.Tag = Vector2.Zero;

        // Trampled dirt clearing.
        mesh.Blob(b - Vector3.Up * 0.05f, new Vector3(rad * 1.1f, 0.16f, rad * 1.1f), GoblinMud, r, 0.12f);

        // Low log breastwork / stakes around the rear arc.
        int logs = 10;
        for (int i = 0; i < logs; i++)
        {
            float a = gate + MathF.PI * (0.35f + 1.3f * i / (logs - 1));
            var lp = pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rad;
            var lb = new Vector3(lp.X, t.Height(lp) - 0.1f, lp.Y);
            mesh.Box(lb, 0.95f * s, 0.24f * s, 0.65f * s, a + MathF.PI / 2, Jitter(GoblinTimber, r));
        }

        // Weathered canvas tents pitched around the campfire.
        int tents = 2 + Math.Clamp(men / 22, 0, 3);
        var canvas = new Color(172, 158, 134);
        var canvasDark = new Color(128, 114, 94);
        for (int i = 0; i < tents; i++)
        {
            float ta = gate + MathF.PI * (0.45f + 1.1f * i / Math.Max(1, tents - 1)) + (r.NextSingle() - 0.5f) * 0.2f;
            float td = rad * 0.58f;
            var tp = pos + new Vector2(MathF.Cos(ta), MathF.Sin(ta)) * td;
            var tb = new Vector3(tp.X, t.Height(tp) - 0.08f, tp.Y);
            float ts = (0.78f + r.NextSingle() * 0.22f) * s;
            var col = i % 2 == 0 ? Jitter(canvas, r) : Jitter(canvasDark, r);
            mesh.Frustum(tb, tb + Vector3.Up * (1.15f * ts), 0.72f * ts, 0.06f * ts, 4, col, r, capBottom: true);
        }

        // Archetype-colored outlaw pennant pole.
        var bp = pos + gf * (rad * 0.45f) - gs * (0.95f * s);
        var bb = new Vector3(bp.X, t.Height(bp) - 0.1f, bp.Y);
        float bh = (tier >= 4 ? 2.5f : 2.1f) * s;
        mesh.Box(bb, 0.09f * s, 0.09f * s, bh, gate, GoblinTimber);
        mesh.Box(bb + Vector3.Up * (bh - 0.55f * s), 0.78f * s, 0.06f * s, 0.48f * s, gate + 0.35f, accent);

        // Glowing central campfire.
        var fp = pos + gf * (0.35f * s);
        var fb = new Vector3(fp.X, t.Height(fp), fp.Y);
        mesh.Box(fb, 0.48f * s, 0.48f * s, 0.14f * s, 0, GoblinEmber);

        // Two camp sentries.
        var sentry = new Person { Shirt = new Color(62, 54, 46), Hose = new Color(46, 42, 38), Hat = true };
        DrawPerson(pos + gf * (rad * 0.75f) + gs * (0.45f * s), gate, clock * 1.1f + id, false, 0, sentry, 0.85f * s);
        if (men >= 20)
            DrawPerson(fp - gs * (0.7f * s), gate - 1.2f, clock + id + 3, false, 0.4f, sentry, 0.82f * s);
    }

    void DrawHoldout(int id, Vector2 pos, int men, int tier)
    {
        var r = new Random(id * 97 + 13);
        float s = 1.0f + Math.Clamp((men - 10) / 45f, 0f, 0.45f) + (tier >= 4 ? 0.12f : 0f);
        float rad = 3.2f * s;
        float gate = r.NextSingle() * MathF.Tau;
        float ground = t.Height(pos);
        var b = new Vector3(pos.X, ground, pos.Y);
        mesh.Tag = Vector2.Zero;

        // Cleared mud apron and central dug-out warren mound.
        mesh.Blob(b - Vector3.Up * 0.05f, new Vector3(rad * 1.15f, 0.22f, rad * 1.15f), GoblinMud, r, 0.14f);
        var gf = new Vector2(MathF.Cos(gate), MathF.Sin(gate));
        var gs = new Vector2(-gf.Y, gf.X);
        var moundPos = pos - gf * (0.45f * s);
        var mb = new Vector3(moundPos.X, t.Height(moundPos), moundPos.Y);
        mesh.Blob(mb + Vector3.Up * (0.15f * s), new Vector3(1.45f, 0.95f, 1.35f) * s, GoblinEarth, r, 0.22f);

        // Timbered burrow mouth cut into the front of the mound.
        var mouthPos = moundPos + gf * (0.95f * s);
        var mouthB = new Vector3(mouthPos.X, t.Height(mouthPos), mouthPos.Y);
        mesh.Box(mouthB, 0.72f * s, 0.36f * s, 0.78f * s, gate + MathF.PI / 2, new Color(20, 18, 18));
        mesh.Box(mouthB + Vector3.Up * (0.76f * s), 1.05f * s, 0.32f * s, 0.18f * s, gate + MathF.PI / 2, GoblinTimber);
        foreach (float side in new[] { -0.44f * s, 0.44f * s })
        {
            var postPos = mouthPos + gs * side;
            mesh.Box(new Vector3(postPos.X, t.Height(postPos), postPos.Y), 0.16f * s, 0.18f * s, 0.85f * s, gate, GoblinTimber);
        }

        // Spiked timber palisade ring with an open gate.
        int stakes = 16;
        for (int i = 0; i < stakes; i++)
        {
            float a = i * MathF.Tau / stakes;
            float diff = MathF.Abs(MathHelper.WrapAngle(a - gate));
            if (diff < 0.34f) continue; // gate opening
            var sp = pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rad;
            float sh = (1.05f + r.NextSingle() * 0.45f) * s;
            var sb = new Vector3(sp.X, t.Height(sp) - 0.15f, sp.Y);
            var col = Jitter(GoblinTimber, r);
            mesh.Box(sb, 0.22f * s, 0.22f * s, sh, a, col);
            mesh.Frustum(sb + Vector3.Up * sh, sb + Vector3.Up * (sh + 0.35f * s), 0.15f * s, 0, 4, col, r);
        }

        // Taller gate posts and bone trophy / lintel.
        foreach (float side in new[] { -0.95f * s, 0.95f * s })
        {
            var gp = pos + gf * (rad * 0.98f) + gs * side;
            var gb = new Vector3(gp.X, t.Height(gp) - 0.15f, gp.Y);
            mesh.Box(gb, 0.26f * s, 0.26f * s, 1.75f * s, gate, GoblinTimber);
            mesh.Box(gb + Vector3.Up * (1.75f * s), 0.24f * s, 0.22f * s, 0.24f * s, gate, GoblinBone);
        }

        // Sharpened stakes outside the gate.
        for (int i = 0; i < 4; i++)
        {
            float side = (i < 2 ? -1 : 1) * (1.35f + (i % 2) * 0.65f) * s;
            var tp = pos + gf * (rad + 0.75f * s) + gs * side;
            var tb = new Vector3(tp.X, t.Height(tp) - 0.1f, tp.Y);
            mesh.Frustum(tb, tb + Vector3.Up * (0.85f * s) + new Vector3(gf.X, 0, gf.Y) * (0.45f * s), 0.11f * s, 0.02f * s, 4, GoblinTimber, r);
        }

        // Ramshackle hide and wattle huts inside the palisade.
        int hutCount = 2 + Math.Clamp(men / 14, 0, 3);
        for (int i = 0; i < hutCount; i++)
        {
            float ha = gate + MathF.PI * (0.42f + 1.16f * i / Math.Max(1, hutCount - 1)) + (r.NextSingle() - 0.5f) * 0.22f;
            float hd = rad * (0.56f + r.NextSingle() * 0.14f);
            var hp = pos + new Vector2(MathF.Cos(ha), MathF.Sin(ha)) * hd;
            var hb = new Vector3(hp.X, t.Height(hp) - 0.1f, hp.Y);
            float hs = (0.82f + r.NextSingle() * 0.25f) * s;
            var roofCol = i % 2 == 0 ? Jitter(GoblinHide, r) : Jitter(GoblinHideDark, r);
            mesh.Frustum(hb, hb + Vector3.Up * (0.52f * hs), 0.64f * hs, 0.56f * hs, 6, Jitter(GoblinEarth, r), r);
            mesh.Frustum(hb + Vector3.Up * (0.52f * hs), hb + Vector3.Up * (1.28f * hs), 0.82f * hs, 0, 6, roofCol, r, capBottom: true);
        }

        // Boss / warlord banner pole when the holdout has grown strong.
        if (tier >= 4)
        {
            var bp = pos + gf * (rad * 0.65f) - gs * (1.15f * s);
            var bb = new Vector3(bp.X, t.Height(bp) - 0.1f, bp.Y);
            float bh = (tier >= 6 ? 2.9f : 2.3f) * s;
            mesh.Box(bb, 0.1f * s, 0.1f * s, bh, gate, GoblinTimber);
            mesh.Box(bb + Vector3.Up * (bh - 0.65f * s), 0.85f * s, 0.06f * s, 0.55f * s, gate + 0.4f, GoblinBanner);
        }

        // Campfire in the yard before the burrow (soft smoke is drawn by MapScreen).
        var fp = pos + gf * (1.35f * s);
        var fb = new Vector3(fp.X, t.Height(fp), fp.Y);
        mesh.Box(fb, 0.52f * s, 0.52f * s, 0.14f * s, 0, GoblinEmber);

        // A couple of small goblin sentries proportional to the camp (not giant map-tokens).
        var sentry = new Person { Shirt = GoblinRag, Hose = GoblinSkin, Hat = false };
        DrawPerson(pos + gf * (rad * 0.85f) + gs * (0.42f * s), gate, clock * 1.2f + id, false, 0, sentry, 0.85f * s);
        if (men >= 18)
            DrawPerson(fp - gs * (0.65f * s), gate - 1.1f, clock + id + 2, false, 0.4f, sentry, 0.82f * s);
    }
}
