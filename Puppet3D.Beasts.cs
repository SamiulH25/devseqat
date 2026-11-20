using Microsoft.Xna.Framework;

namespace Ironfield;

/// Phase 2 3D articulated rigs for all beasts and non-horse mounts:
/// Wolf / War Hound / Dire Wolf / Warg mount, Cave Bear, Giant Spider, Wyvern, and Camel!
sealed partial class Puppet3D
{
    static float BeastRearing(Unit u) =>
        u.Action == UnitAction.Windup ? Math.Clamp(1 - u.ActionTimer / MathF.Max(0.05f, u.Type.Windup), 0, 1)
        : u.Action == UnitAction.Recover ? Math.Clamp(u.ActionTimer / MathF.Max(0.05f, u.Type.Recover) * 2 - 1, 0, 1) : 0;

    bool DrawBeast3D(Unit u, float ground, Color tint, Look look)
    {
        switch (look.Face)
        {
            case "spider": DrawSpider3D(u, ground, tint); return true;
            case "bear": DrawBear3D(u, ground, tint); return true;
            case "wyvern": DrawWyvern3D(u, ground + u.Altitude * Scale, tint); return true;
            default: DrawWolfUnit3D(u, ground, tint); return true;
        }
    }

    void DrawWolfUnit3D(Unit u, float ground, Color tint)
    {
        bool alpha = u.Type.Id == "alpha_wolf";
        bool hound = u.Type.Breed == "hound";
        bool dark = u.Type.Size > 1.1f;
        float lunge = u.Action == UnitAction.Windup ? -0.12f : u.Action == UnitAction.Recover ? 0.28f : 0;
        Color coat = alpha ? new Color(235, 238, 246)
            : hound ? new Color(156, 108, 66)
            : dark ? new Color(82, 78, 76)
            : new Color(148, 146, 142);
        Color mane = alpha ? new Color(200, 206, 218) : dark ? new Color(46, 42, 42) : new Color(92, 88, 84);
        UnitMatTable.FillBeast(coat, mane, new Color(42, 36, 34), tint, palCols);
        DrawWolfRig(u.Pos, ground, u.ViewFacing, u.Anim, u.Vel.Length(), u.Type.Size * Scale, lunge);
    }

    float DrawWolfRig(Vector2 pos, float ground, float heading, float anim, float speed, float size, float lunge)
    {
        Frame(pos, heading, ground, size);
        float gait = Math.Clamp(speed / 7f, 0, 1);
        float amp = speed < 0.3f ? 0 : 18 + 30 * gait;
        float bob = MathF.Sin(anim * MathF.Tau * 1.5f) * 0.03f * gait;
        float y = 0.60f + bob;
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.5f, 0.3f, 0.8f };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float a = Rad(amp) * MathF.Sin(anim * MathF.PI * 1.5f + phase[i] * MathF.Tau);
            var hip = new Vector3(sd * 0.14f, y - 0.05f, front ? 0.36f : -0.38f);
            var foot = hip + new Vector3(0, -0.56f * MathF.Cos(a), 0.56f * MathF.Sin(a));
            EmitBone(kit.WolfLeg, hip, foot, Vector3.UnitZ);
        }
        EmitLocal(kit.WolfBody, new Vector3(0, y, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        EmitLocal(kit.WolfHead, new Vector3(0, y + 0.14f, 0.44f + lunge), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        if (Detail == 0)
            EmitBone(kit.WolfTail, new Vector3(0, y + 0.08f, -0.50f), new Vector3(0, y - 0.20f + gait * 0.22f, -0.88f - gait * 0.2f), Vector3.UnitZ);
        return (y + 0.06f) * size;
    }

    /// A ridden warg (dark dire wolf mount): returns saddle lift height in metres.
    float DrawWarg3D(Unit u, float ground, Color tint)
    {
        const float wargSize = 1.5f;
        UnitMatTable.FillBeast(new Color(78, 74, 72), new Color(44, 40, 40), new Color(34, 30, 30), tint, palCols);
        float seat = DrawWolfRig(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, wargSize * Scale, 0);
        return seat - 0.63f * u.Type.Size * Scale;
    }

    void DrawBear3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        UnitMatTable.FillBeast(new Color(118, 82, 54), new Color(76, 52, 36), new Color(40, 32, 26), tint, palCols);
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 6f, 0, 1);
        float amp = speed < 0.3f ? 0 : 14 + 22 * gait;
        float y = 0.70f + MathF.Sin(u.Anim * MathF.Tau * 1.2f) * 0.03f * gait;
        float a = Rad(58 * BeastRearing(u)), c = MathF.Cos(a), s = MathF.Sin(a);
        var pivot = new Vector3(0, y, -0.4f);
        Vector3 P(Vector3 v) { var d = v - pivot; return pivot + new Vector3(d.X, d.Y * c + d.Z * s, -d.Y * s + d.Z * c); }
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.5f, 0.5f, 0 };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float ang = Rad(amp) * MathF.Sin(u.Anim * MathF.PI * 1.2f + phase[i] * MathF.Tau);
            var hip = new Vector3(sd * 0.24f, y - 0.06f, front ? 0.36f : -0.40f);
            var foot = hip + new Vector3(0, -0.64f * MathF.Cos(ang), 0.64f * MathF.Sin(ang));
            if (front) { foot = Vector3.Lerp(foot, P(hip + new Vector3(sd * 0.06f, -0.25f, 0.48f)), s); hip = P(hip); }
            EmitBone(kit.BearLeg, hip, foot, Vector3.UnitZ);
        }
        Vector3 bUp = new(0, c, -s), bFwd = new(0, s, c);
        EmitLocal(kit.BearBody, P(new Vector3(0, y, 0)), Vector3.UnitX, bUp, bFwd);
        EmitLocal(kit.BearHead, P(new Vector3(0, y + 0.14f, 0.44f)), Vector3.UnitX, bUp, bFwd);
    }

    void DrawSpider3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        Color hide = u.Type.Id == "brood_mother" ? new Color(72, 46, 82)
            : u.Type.Id == "spiderling" ? new Color(138, 114, 88)
            : new Color(56, 48, 52);
        UnitMatTable.FillBeast(hide, new Color(150, 44, 36), new Color(32, 26, 28), tint, palCols);

        float gait = Math.Clamp(u.Vel.Length() / 6f, 0, 1);
        float rear = BeastRearing(u), lunge = u.Action == UnitAction.Recover ? 0.2f * rear : -0.08f * rear;
        float y = 0.42f + MathF.Sin(u.Anim * MathF.Tau * 2f) * 0.015f * gait;

        for (int i = 0; i < 8; i++)
        {
            float sd = i % 2 == 0 ? -1 : 1;
            int pair = i / 2;
            float z = 0.20f - pair * 0.13f;
            bool set = (pair + (sd > 0 ? 1 : 0)) % 2 == 0;
            float ph = u.Anim * MathF.Tau + (set ? 0 : MathF.PI);
            float lift = MathF.Max(0, MathF.Sin(ph)) * 0.16f * gait, swing = MathF.Cos(ph) * 0.12f * gait;
            var hip = new Vector3(sd * 0.14f, y, z + lunge * 0.5f);
            var foot = new Vector3(sd * (0.98f - MathF.Abs(1.5f - pair) * 0.08f), lift, z + (1.5f - pair) * 0.42f + swing);
            if (pair == 0) foot = Vector3.Lerp(foot, new Vector3(sd * 0.42f, y + 0.52f, z + 0.55f), rear);
            var knee = new Vector3((hip.X + foot.X) * 0.68f, y + 0.48f + lift * 0.5f, (hip.Z + foot.Z) * 0.5f);
            EmitBone(kit.SpiderLeg, hip, knee, Vector3.UnitY);
            EmitBone(kit.SpiderLeg, knee, foot, Vector3.UnitY);
        }
        EmitLocal(kit.SpiderAbdomen, new Vector3(0, y + 0.10f, -0.46f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        EmitLocal(kit.SpiderHead, new Vector3(0, y + 0.06f + rear * 0.16f, 0.10f + lunge), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
    }

    void DrawWyvern3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        // Deep emerald/bronze-scaled wyvern hide, leathery warm wing membranes, pale bone horns & sting!
        UnitMatTable.FillBeast(new Color(62, 102, 74), new Color(142, 114, 82), new Color(218, 206, 178), tint, palCols);

        bool aloft = u.Altitude > 0.4f;
        float rear = BeastRearing(u);
        float flap = MathF.Sin((Now * 1.7f + Ease.Hash(u.Index)) * MathF.Tau);
        if (u.Action == UnitAction.Windup) flap = MathHelper.Lerp(flap, 1, rear);
        else if (u.Action == UnitAction.Recover) flap = MathHelper.Lerp(flap, -0.8f, rear);

        float y = 0.90f, lunge = u.Action == UnitAction.Recover ? 0.30f * rear : -0.15f * rear;

        // Huge articulated 3D bat-wings (flapping symmetrically aloft or folded cleanly on the ground!)
        for (int sd = -1; sd <= 1; sd += 2)
        {
            Vector3 root = new(sd * 0.22f, y + 0.14f, 0.24f);
            float span = aloft || rear > 0 ? 1.85f : 0.72f;
            float roll = aloft || rear > 0 ? flap * 0.62f : 1.05f; // flap angle around forward Z axis
            float yawSweep = aloft || rear > 0 ? -0.18f : -0.75f;  // sweep back toward tail
            float cr = MathF.Cos(roll), sr = MathF.Sin(roll);
            float cy = MathF.Cos(yawSweep), sy = MathF.Sin(yawSweep);
            // Local wing +X points outward along sd, rolled up/down by `roll` and swept back by `yawSweep`
            Vector3 wOut = new(sd * cr * cy, sr, cy < 0 ? sy : sy);
            wOut = Vector3.Normalize(new Vector3(sd * cr * cy, sr, sy * cr));
            Vector3 wUp = Vector3.Normalize(new Vector3(-sd * sr * cy, cr, -sy * sr));
            Vector3 wFwd = Vector3.Normalize(new Vector3(-sd * sy, 0, cy));
            EmitLocal(kit.WyvernWing, root, wOut, wUp, wFwd, span, 1.0f, aloft || rear > 0 ? 1.25f : 0.85f);

            var hip = new Vector3(sd * 0.16f, y - 0.05f, -0.20f);
            var foot = aloft ? new Vector3(sd * 0.18f, y - 0.42f, -0.72f) : new Vector3(sd * 0.24f, 0, -0.12f);
            EmitBone(kit.WyvernLeg, hip, foot, Vector3.UnitZ);
        }

        EmitLocal(kit.WyvernBody, new Vector3(0, y, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        var neckA = new Vector3(0, y + 0.06f, 0.40f + lunge * 0.3f);
        var neckB = new Vector3(0, y + 0.38f + rear * 0.1f, 0.86f + lunge);
        EmitBone(kit.WyvernNeck, neckA, neckB, Vector3.UnitZ);
        EmitLocal(kit.WyvernHead, neckB, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

        // Scorpion-curling venomous tail!
        float sway = MathF.Sin(Now * 2.1f + u.Index) * 0.15f;
        var t0 = new Vector3(0, y, -0.55f);
        var t1 = Vector3.Lerp(new Vector3(sway, y - 0.05f, -1.05f), new Vector3(0, y + 0.45f, -0.95f), rear);
        var t2 = Vector3.Lerp(new Vector3(sway * 2, y - 0.10f, -1.50f), new Vector3(0, y + 0.95f, -0.70f), rear);
        var t3 = Vector3.Lerp(new Vector3(sway * 2.4f, y - 0.12f, -1.82f), new Vector3(0, y + 1.12f, -0.38f), rear);
        EmitBone(kit.WyvernTail, t0, t1, Vector3.UnitY);
        EmitBone(kit.WyvernTail, t1, t2, Vector3.UnitY);
        EmitBone(kit.WyvernSting, t2, t3, Vector3.UnitY);
    }
}
