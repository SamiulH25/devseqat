using Microsoft.Xna.Framework;

namespace Ironfield;

/// Phase 2 & 3.5 3D articulated rigs for all beasts, brutes, and non-horse mounts:
/// Wolf / Dire Wolf / Warg, Bear, Spider, Wyvern, Boar, Harpy, Griffon, Basilisk, Sand Wurm, Werewolf, and Troll/Ogre/Giant Brutes.
sealed partial class Puppet3D
{
    static float BeastRearing(Unit u) =>
        u.Action == UnitAction.Windup ? Math.Clamp(1 - u.ActionTimer / MathF.Max(0.05f, u.Type.Windup), 0, 1)
        : u.Action == UnitAction.Recover ? Math.Clamp(u.ActionTimer / MathF.Max(0.05f, u.Type.Recover) * 2 - 1, 0, 1) : 0;

    bool DrawBeast3D(Unit u, float ground, Color tint, Look look)
    {
        switch (u.Type.Breed)
        {
            case "boar": DrawBoar3D(u, ground, tint); return true;
            case "harpy": DrawHarpy3D(u, ground + u.Altitude * Scale, tint); return true;
            case "griffon": DrawGriffon3D(u, ground + u.Altitude * Scale, tint); return true;
            case "basilisk": DrawBasilisk3D(u, ground, tint); return true;
            case "wurm": DrawSandWurm3D(u, ground, tint); return true;
            case "werewolf": DrawWerewolf3D(u, ground, tint); return true;
            case "troll" or "ogre" or "giant": DrawBrute3D(u, ground, tint); return true;
        }
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
        float wind = u.Action == UnitAction.Windup ? Math.Clamp(1f - u.ActionTimer / MathF.Max(0.05f, u.Type.Windup), 0f, 1f) : 0f;
        float strike = u.Action == UnitAction.Recover ? Math.Clamp(u.ActionTimer / MathF.Max(0.05f, u.Type.Recover), 0f, 1f) : 0f;
        float lunge = strike > 0 ? Ease.Out(strike) * 0.24f : -wind * 0.14f;
        Color coat = alpha ? new Color(235, 238, 246)
            : hound ? new Color(156, 108, 66)
            : dark ? new Color(82, 78, 76)
            : new Color(148, 146, 142);
        Color mane = alpha ? new Color(200, 206, 218) : dark ? new Color(46, 42, 42) : new Color(92, 88, 84);
        UnitMatTable.FillBeast(coat, mane, new Color(42, 36, 34), tint, palCols);
        float turnDelta = MathHelper.WrapAngle(u.Facing - u.ViewFacing);
        DrawWolfRig(u.Pos, ground, u.ViewFacing, u.Anim, u.Vel.Length(), u.Type.Size * Scale, lunge, wind, strike, turnDelta);
    }

    float DrawWolfRig(Vector2 pos, float ground, float heading, float anim, float speed, float size, float lunge, float wind = 0f, float strike = 0f, float turnDelta = 0f)
    {
        Frame(pos, heading, ground, size);
        float gait = Math.Clamp(speed / 7f, 0, 1);
        float bank = Math.Clamp(turnDelta * (8f + 14f * gait), -14f, 14f);
        if (MathF.Abs(bank) > 0.2f) Lean(bank, 0f);

        float amp = speed < 0.3f ? 0 : 18 + 34 * gait;
        float bob = MathF.Sin(anim * MathF.Tau * 1.5f) * 0.045f * gait;
        float rock = MathF.Sin(anim * MathF.Tau * 1.5f - 0.5f) * 0.055f * gait;
        float crouch = wind * 0.10f;
        float leapY = strike > 0 ? MathF.Sin(strike * MathF.PI) * 0.14f : 0f;
        float y = 0.62f + bob - crouch + leapY;
        float bodyZ = lunge * 0.85f;
        float pitchDeg = -rock * 110f + wind * 12f - strike * 10f;

        // 2-bone articulated canine legs (front elbow bends back, hind hock bends back!)
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.22f, 0.55f, 0.78f };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float a = Rad(amp) * MathF.Sin(anim * MathF.Tau * 1.5f + phase[i] * MathF.Tau);
            if (strike > 0) a += Rad(front ? 32f * strike : -26f * strike);
            else if (wind > 0) a += Rad(front ? -14f * wind : 16f * wind);
            var hip = new Vector3(sd * 0.15f, y - 0.04f + (front ? rock : -rock), bodyZ + (front ? 0.34f : -0.38f));
            var foot = hip + new Vector3(0, -0.58f * MathF.Cos(a), 0.58f * MathF.Sin(a) + (front ? 0.04f : -0.06f));
            // Anatomical joint: elbow sits slightly back on forelegs, hock sits distinctly back on hindlegs
            var mid = Vector3.Lerp(hip, foot, 0.48f) + new Vector3(sd * 0.015f, 0.02f, front ? -0.09f : -0.14f);
            EmitBone(kit.QuadThigh, hip, mid, Vector3.UnitZ, 1f, MeshKit3D.PatFur);
            EmitBone(kit.QuadShin, mid, foot, Vector3.UnitZ, 1f, MeshKit3D.PatFur);
        }
        Vector3 bU = RotX(Vector3.UnitY, pitchDeg), bF = RotX(Vector3.UnitZ, pitchDeg);
        EmitLocal(kit.WolfBody, new Vector3(0, y, bodyZ), Vector3.UnitX, bU, bF, 1f, 1f, 1f, MeshKit3D.PatFur);
        float headPitch = -wind * 12f + strike * 16f - rock * 50f;
        Vector3 hU = RotX(Vector3.UnitY, headPitch), hF = RotX(Vector3.UnitZ, headPitch);
        EmitLocal(kit.WolfHead, new Vector3(0, y + 0.14f + rock * 0.7f, bodyZ + 0.40f + lunge * 0.20f), Vector3.UnitX, hU, hF, 1f, 1f, 1f, MeshKit3D.PatFur);
        if (Detail == 0)
        {
            float wag = MathF.Sin(Now * (5.5f + 4f * gait) + pos.X) * (0.09f + 0.06f * gait);
            EmitBone(kit.WolfTail, new Vector3(0, y + 0.08f - rock, bodyZ - 0.50f),
                new Vector3(wag, y - 0.14f + gait * 0.28f + strike * 0.18f, bodyZ - 0.94f - gait * 0.22f), Vector3.UnitZ, 1f, MeshKit3D.PatFur);
        }
        return (y + 0.06f) * size;
    }

    /// A ridden warg (dark dire wolf mount): returns saddle lift height in metres.
    float DrawWarg3D(Unit u, float ground, Color tint, float turnDelta = 0f)
    {
        const float wargSize = 1.5f;
        UnitMatTable.FillBeast(new Color(78, 74, 72), new Color(44, 40, 40), new Color(34, 30, 30), tint, palCols);
        float seat = DrawWolfRig(u.Pos, ground, u.ViewHeading, u.Anim, u.Speed, wargSize * Scale, 0, 0, 0, turnDelta);
        return seat - 0.63f * u.Type.Size * Scale;
    }

    void DrawBear3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        bool king = u.Type.Id == "bear_king";
        UnitMatTable.FillBeast(
            king ? new Color(92, 62, 42) : new Color(118, 82, 54),
            king ? new Color(48, 34, 24) : new Color(76, 52, 36),
            new Color(40, 32, 26), tint, palCols);
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 6f, 0, 1);
        float amp = speed < 0.3f ? 0 : 14 + 22 * gait;
        float waddle = MathF.Sin(u.Anim * MathF.Tau * 1.2f) * 0.055f * gait;
        float y = 0.72f + MathF.Abs(MathF.Sin(u.Anim * MathF.Tau * 1.2f)) * 0.04f * gait;
        float rear = BeastRearing(u);
        float swipe = u.Action == UnitAction.Recover ? MathF.Sin(Math.Clamp(1f - u.ActionTimer / MathF.Max(0.05f, u.Type.Recover), 0f, 1f) * MathF.PI) : 0f;
        float a = Rad(46f * rear), c = MathF.Cos(a), s = MathF.Sin(a);
        var pivot = new Vector3(0, y, -0.4f);
        Vector3 P(Vector3 v) { var d = v - pivot; return pivot + new Vector3(d.X, d.Y * c + d.Z * s, -d.Y * s + d.Z * c); }
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.5f, 0.5f, 0 };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float ang = Rad(amp) * MathF.Sin(u.Anim * MathF.Tau * 1.2f + phase[i] * MathF.Tau);
            var hip = new Vector3(sd * 0.26f + waddle * 0.4f, y - 0.06f, front ? 0.36f : -0.42f);
            var foot = hip + new Vector3(0, -0.66f * MathF.Cos(ang), 0.66f * MathF.Sin(ang) + (front ? 0.06f : -0.04f));
            if (front)
            {
                float pawReach = sd > 0 ? 0.56f + swipe * 0.28f : 0.40f - swipe * 0.10f;
                float pawLift = sd > 0 ? -0.10f - swipe * 0.34f : -0.28f;
                float pawOut = sd * (0.10f + (sd > 0 ? swipe * 0.24f : 0f));
                foot = Vector3.Lerp(foot, P(hip + new Vector3(pawOut, pawLift, pawReach)), s + swipe * 0.45f);
                hip = P(hip);
            }
            var mid = Vector3.Lerp(hip, foot, 0.48f) + new Vector3(sd * 0.04f, 0.02f, front ? -0.10f : -0.12f);
            EmitBone(kit.BearThigh, hip, mid, Vector3.UnitZ, 1f, MeshKit3D.PatFur);
            EmitBone(kit.BearShin, mid, foot, Vector3.UnitZ, 1f, MeshKit3D.PatFur);
        }
        float yawSwipe = swipe * -14f + waddle * 75f;
        Vector3 bR = RotY(Vector3.UnitX, yawSwipe);
        Vector3 bUp = RotY(new Vector3(0, c, -s), yawSwipe);
        Vector3 bFwd = RotY(new Vector3(0, s, c), yawSwipe);
        EmitLocal(kit.BearBody, P(new Vector3(waddle * 0.5f, y, 0)), bR, bUp, bFwd, 1f, 1f, 1f, MeshKit3D.PatFur);
        float hAng = Rad(24f * rear);
        float hc = MathF.Cos(hAng), hs = MathF.Sin(hAng);
        Vector3 hUp = RotY(new Vector3(0, hc, -hs), yawSwipe * 1.15f);
        Vector3 hFwd = RotY(new Vector3(0, hs, hc), yawSwipe * 1.15f);
        EmitLocal(kit.BearHead, P(new Vector3(waddle * 0.6f, y + 0.15f, 0.44f)), bR, hUp, hFwd, 1f, 1f, 1f, MeshKit3D.PatFur);
    }

    void DrawSpider3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        Color hide = u.Type.Id == "brood_mother" ? new Color(72, 46, 82)
            : u.Type.Id == "spiderling" ? new Color(138, 114, 88)
            : new Color(56, 48, 52);
        UnitMatTable.FillBeast(hide, new Color(150, 44, 36), new Color(32, 26, 28), tint, palCols);

        float gait = Math.Clamp(u.Vel.Length() / 6f, 0, 1);
        float rear = BeastRearing(u), lunge = u.Action == UnitAction.Recover ? 0.24f * rear : -0.10f * rear;
        float idlePulse = MathF.Sin(Now * 2.8f + u.Index) * 0.015f * (1f - gait);
        float y = 0.42f + MathF.Sin(u.Anim * MathF.Tau * 2f) * 0.022f * gait + idlePulse;

        for (int i = 0; i < 8; i++)
        {
            float sd = i % 2 == 0 ? -1 : 1;
            int pair = i / 2;
            float z = 0.20f - pair * 0.13f;
            bool set = (pair + (sd > 0 ? 1 : 0)) % 2 == 0;
            float ph = u.Anim * MathF.Tau + (set ? 0 : MathF.PI);
            float lift = MathF.Max(0, MathF.Sin(ph)) * 0.18f * gait, swing = MathF.Cos(ph) * 0.14f * gait;
            var hip = new Vector3(sd * 0.14f, y, z + lunge * 0.5f);
            var foot = new Vector3(sd * (0.98f - MathF.Abs(1.5f - pair) * 0.08f), lift, z + (1.5f - pair) * 0.42f + swing);
            if (pair == 0) foot = Vector3.Lerp(foot, new Vector3(sd * 0.56f, y + 0.58f, z + 0.62f), rear);
            else if (pair == 1 && rear > 0.1f) foot = Vector3.Lerp(foot, new Vector3(sd * 0.86f, y + 0.22f, z + 0.34f), rear * 0.65f);
            var knee = new Vector3((hip.X + foot.X) * 0.68f, y + 0.48f + lift * 0.5f + (pair == 0 ? rear * 0.18f : 0f), (hip.Z + foot.Z) * 0.5f);
            EmitBone(kit.SpiderLeg, hip, knee, Vector3.UnitY);
            EmitBone(kit.SpiderLeg, knee, foot, Vector3.UnitY);
        }
        float abdPitch = rear * 22f + MathF.Sin(u.Anim * MathF.Tau * 2f) * 5f * gait + idlePulse * 240f;
        Vector3 aU = RotX(Vector3.UnitY, abdPitch), aF = RotX(Vector3.UnitZ, abdPitch);
        EmitLocal(kit.SpiderAbdomen, new Vector3(0, y + 0.10f - rear * 0.06f, -0.46f + lunge * 0.4f), Vector3.UnitX, aU, aF, 1f, 1f, 1f, MeshKit3D.PatScale);
        Vector3 hU = RotX(Vector3.UnitY, -rear * 24f), hF = RotX(Vector3.UnitZ, -rear * 24f);
        EmitLocal(kit.SpiderHead, new Vector3(0, y + 0.06f + rear * 0.20f, 0.10f + lunge), Vector3.UnitX, hU, hF, 1f, 1f, 1f, MeshKit3D.PatScale);
    }

    void DrawWyvern3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        // Deep emerald/bronze-scaled wyvern hide, leathery warm wing membranes, pale bone horns & sting
        UnitMatTable.FillBeast(new Color(62, 102, 74), new Color(142, 114, 82), new Color(218, 206, 178), tint, palCols);

        bool aloft = u.Altitude > 0.4f;
        float rear = BeastRearing(u);
        float flapPhase = (Now * 1.7f + Ease.Hash(u.Index)) * MathF.Tau;
        float flap = MathF.Sin(flapPhase);
        float flapLag = MathF.Sin(flapPhase - 0.7f);
        if (u.Action == UnitAction.Windup) { flap = MathHelper.Lerp(flap, 1f, rear); flapLag = MathHelper.Lerp(flapLag, 0.65f, rear); }
        else if (u.Action == UnitAction.Recover) { flap = MathHelper.Lerp(flap, -0.85f, rear); flapLag = MathHelper.Lerp(flapLag, -0.6f, rear); }

        float turnDelta = MathHelper.WrapAngle(u.Facing - u.ViewFacing);
        float bankDeg = aloft ? Math.Clamp(turnDelta * 26f, -24f, 24f) : 0f;
        float pitchDeg = aloft ? flapLag * 7f - (u.Action == UnitAction.Recover ? 14f * rear : 0f) + (u.Action == UnitAction.Windup ? 10f * rear : 0f) : 0f;
        if (MathF.Abs(bankDeg) > 0.2f || MathF.Abs(pitchDeg) > 0.2f) Lean(bankDeg, pitchDeg);

        float heave = aloft ? -flapLag * 0.16f : 0f;
        float y = 0.92f + heave, lunge = u.Action == UnitAction.Recover ? 0.36f * rear : -0.16f * rear;

        // Huge articulated 3D bat-wings: dramatically mantled even when perched!
        for (int sd = -1; sd <= 1; sd += 2)
        {
            Vector3 root = new(sd * 0.22f, y + 0.14f, 0.22f);
            float span = aloft || rear > 0 ? 1.88f - MathF.Abs(flap) * 0.10f : 1.48f;
            float roll = aloft || rear > 0 ? flap * 0.68f : 0.42f;
            float yawSweep = aloft || rear > 0 ? (-0.16f - flapLag * 0.16f) : -0.34f;
            float cr = MathF.Cos(roll), sr = MathF.Sin(roll);
            float cy = MathF.Cos(yawSweep), sy = MathF.Sin(yawSweep);
            Vector3 wOut = Vector3.Normalize(new Vector3(sd * cr * cy, sr, sy * cr));
            Vector3 wUp = Vector3.Normalize(new Vector3(-sd * sr * cy, cr, -sy * sr));
            Vector3 wFwd = Vector3.Normalize(new Vector3(-sd * sy, 0, cy));
            EmitLocal(kit.WyvernWing, root, wOut, wUp, wFwd, span, 1.0f, aloft || rear > 0 ? 1.28f : 1.08f);

            var hip = new Vector3(sd * 0.18f, y - 0.06f, -0.22f);
            var foot = aloft
                ? new Vector3(sd * 0.22f, y - 0.38f - flapLag * 0.08f, -0.58f + (u.Action == UnitAction.Recover ? 0.38f * rear : 0f))
                : new Vector3(sd * 0.26f, 0, -0.12f);
            var hock = Vector3.Lerp(hip, foot, 0.50f) + new Vector3(sd * 0.04f, 0.04f, -0.16f);
            EmitBone(kit.QuadThigh, hip, hock, Vector3.UnitZ);
            EmitBone(kit.WyvernLeg, hock, foot, Vector3.UnitZ);
        }

        EmitLocal(kit.WyvernBody, new Vector3(0, y, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        var neckA = new Vector3(0, y + 0.06f, 0.40f + lunge * 0.3f);
        var neckB = new Vector3(0, y + 0.42f + rear * 0.14f - heave * 0.45f, 0.90f + lunge);
        EmitBone(kit.WyvernNeck, neckA, neckB, Vector3.UnitZ);
        float hPitch = u.Action == UnitAction.Recover ? 18f * rear : -8f * rear + flapLag * 6f;
        EmitLocal(kit.WyvernHead, neckB, Vector3.UnitX, RotX(Vector3.UnitY, hPitch), RotX(Vector3.UnitZ, hPitch));

        // Scorpion-curling venomous tail
        float sway = MathF.Sin(Now * 2.4f + u.Index) * 0.18f;
        float trailWave = aloft ? flapLag * 0.16f : 0f;
        var t0 = new Vector3(0, y, -0.55f);
        var t1 = Vector3.Lerp(new Vector3(sway, y - 0.04f + trailWave * 0.6f, -1.05f), new Vector3(0, y + 0.48f, -0.94f), rear);
        var t2 = Vector3.Lerp(new Vector3(sway * 2, y - 0.08f + trailWave * 1.1f, -1.50f), new Vector3(0, y + 1.00f, -0.66f), rear);
        var t3 = Vector3.Lerp(new Vector3(sway * 2.5f, y - 0.06f + trailWave * 1.5f, -1.84f), new Vector3(0, y + 1.16f, -0.30f), rear);
        EmitBone(kit.WyvernTail, t0, t1, Vector3.UnitY);
        EmitBone(kit.WyvernTail, t1, t2, Vector3.UnitY);
        EmitBone(kit.WyvernSting, t2, t3, Vector3.UnitY);
    }

    void DrawBoar3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        bool great = u.Type.Id == "great_boar", tusker = u.Type.Id == "tusker";
        Color hide = great ? new Color(68, 56, 52) : tusker ? new Color(96, 68, 52) : new Color(112, 82, 62);
        Color mane = great ? new Color(36, 30, 30) : new Color(56, 40, 34);
        UnitMatTable.FillBeast(hide, mane, new Color(32, 26, 24), tint, palCols);
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 7f, 0, 1);
        float amp = speed < 0.3f ? 0 : 18 + 30 * gait;
        float bob = MathF.Sin(u.Anim * MathF.Tau * 1.6f) * 0.04f * gait;
        float rear = BeastRearing(u);
        float y = 0.56f + bob;
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.25f, 0.55f, 0.80f };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float ang = Rad(amp) * MathF.Sin(u.Anim * MathF.Tau * 1.6f + phase[i] * MathF.Tau);
            var hip = new Vector3(sd * 0.20f, y - 0.04f, front ? 0.34f : -0.34f);
            var foot = hip + new Vector3(0, -0.52f * MathF.Cos(ang), 0.52f * MathF.Sin(ang));
            var mid = Vector3.Lerp(hip, foot, 0.48f) + new Vector3(sd * 0.015f, 0.02f, front ? -0.07f : -0.10f);
            EmitBone(kit.QuadThigh, hip, mid, Vector3.UnitZ);
            EmitBone(kit.BoarLeg, mid, foot, Vector3.UnitZ);
        }
        EmitLocal(kit.BoarBody, new Vector3(0, y, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        float hPitch = u.Action == UnitAction.Recover ? -24f * rear : 14f * rear;
        EmitLocal(kit.BoarHead, new Vector3(0, y + 0.04f, 0.48f + (u.Action == UnitAction.Recover ? 0.14f * rear : 0f)),
            Vector3.UnitX, RotX(Vector3.UnitY, hPitch), RotX(Vector3.UnitZ, hPitch));
    }

    void DrawHarpy3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        UnitMatTable.FillBeast(new Color(128, 96, 86), new Color(68, 52, 64), new Color(42, 34, 32), tint, palCols);
        bool aloft = u.Altitude > 0.3f;
        float rear = BeastRearing(u);
        float flap = MathF.Sin((Now * 2.4f + Ease.Hash(u.Index)) * MathF.Tau);
        float y = aloft ? 0.94f - flap * 0.12f : 0.80f;
        for (int sd = -1; sd <= 1; sd += 2)
        {
            Vector3 root = new(sd * 0.18f, y + 0.18f, 0.02f);
            float span = aloft || rear > 0 ? 1.50f : 1.32f;
            float roll = aloft || rear > 0 ? flap * 0.72f : 0.36f;
            float cr = MathF.Cos(roll), sr = MathF.Sin(roll);
            EmitLocal(kit.HarpyWing, root, new Vector3(sd * cr, sr, -0.18f), new Vector3(-sd * sr, cr, 0), Vector3.UnitZ, span, 1f, 1.08f);
            var hip = new Vector3(sd * 0.12f, y - 0.24f, -0.02f);
            var foot = aloft ? new Vector3(sd * 0.15f, y - 0.58f, 0.14f + rear * 0.24f) : new Vector3(sd * 0.14f, 0, 0.04f);
            var hock = Vector3.Lerp(hip, foot, 0.50f) + new Vector3(sd * 0.02f, 0.02f, -0.12f);
            EmitBone(kit.QuadThigh, hip, hock, Vector3.UnitZ);
            EmitBone(kit.WyvernLeg, hock, foot, Vector3.UnitZ);
        }
        float pitch = aloft ? 24f : 10f;
        EmitLocal(kit.HarpyBody, new Vector3(0, y, 0), Vector3.UnitX, RotX(Vector3.UnitY, pitch), RotX(Vector3.UnitZ, pitch));
        EmitLocal(kit.HarpyHead, new Vector3(0, y + 0.44f, 0.12f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
    }

    void DrawGriffon3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        // Warm golden-tawny lion body, deep bronze-brown eagle plumage, golden beak & eyes
        UnitMatTable.FillBeast(new Color(176, 134, 78), new Color(92, 62, 40), new Color(44, 34, 26), tint, palCols);
        bool aloft = u.Altitude > 0.4f;
        float rear = BeastRearing(u);
        float flap = MathF.Sin((Now * 1.8f + Ease.Hash(u.Index)) * MathF.Tau);
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 7f, 0, 1);
        float y = aloft ? 0.90f - flap * 0.14f : 0.68f;
        for (int sd = -1; sd <= 1; sd += 2)
        {
            Vector3 root = new(sd * 0.24f, y + 0.22f, 0.22f);
            float span = aloft || rear > 0 ? 1.88f : 1.56f;
            float roll = aloft || rear > 0 ? flap * 0.65f : 0.42f;
            float cr = MathF.Cos(roll), sr = MathF.Sin(roll);
            EmitLocal(kit.HarpyWing, root, new Vector3(sd * cr, sr, -0.22f), new Vector3(-sd * sr, cr, 0), Vector3.UnitZ, span, 1.0f, 1.28f);

            // Fore eagle legs (2-bone)
            float fAng = aloft ? -0.55f : Rad(22 * gait) * MathF.Sin(u.Anim * MathF.Tau * 1.4f + (sd > 0 ? 0.5f : 0f));
            var fHip = new Vector3(sd * 0.21f, y - 0.02f, 0.36f);
            var fFoot = fHip + new Vector3(0, -0.64f * MathF.Cos(fAng), 0.64f * MathF.Sin(fAng) + (aloft ? 0.25f * rear : 0.06f));
            var fElbow = Vector3.Lerp(fHip, fFoot, 0.48f) + new Vector3(sd * 0.02f, 0.02f, -0.10f);
            EmitBone(kit.QuadThigh, fHip, fElbow, Vector3.UnitZ);
            EmitBone(kit.WyvernLeg, fElbow, fFoot, Vector3.UnitZ);

            // Hind lion legs (2-bone)
            var hHip = new Vector3(sd * 0.20f, y - 0.04f, -0.40f);
            var hFoot = aloft ? hHip + new Vector3(0, -0.44f, -0.34f) : hHip + new Vector3(0, -0.64f, -0.02f);
            var hHock = Vector3.Lerp(hHip, hFoot, 0.48f) + new Vector3(sd * 0.02f, 0.02f, -0.15f);
            EmitBone(kit.QuadThigh, hHip, hHock, Vector3.UnitZ);
            EmitBone(kit.QuadShin, hHock, hFoot, Vector3.UnitZ);
        }
        EmitLocal(kit.GriffonBody, new Vector3(0, y, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        EmitLocal(kit.GriffonHead, new Vector3(0, y + 0.32f, 0.56f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        float wag = MathF.Sin(Now * 3f + u.Index) * 0.14f;
        EmitBone(kit.WolfTail, new Vector3(0, y + 0.04f, -0.56f), new Vector3(wag, y - 0.14f, -1.06f), Vector3.UnitZ);
    }

    void DrawBasilisk3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        // Deep venomous jade-olive scales, dark mossy carapace plates, glowing amber-gold eyes & crest
        UnitMatTable.FillBeast(new Color(64, 108, 66), new Color(36, 62, 42), new Color(28, 34, 24), tint, palCols);
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 5f, 0, 1);
        float sway = MathF.Sin(u.Anim * MathF.Tau * 1.4f + u.Index) * 0.08f;
        float rear = BeastRearing(u);
        float y = 0.38f;
        for (int i = 0; i < 6; i++)
        {
            float sd = i % 2 == 0 ? -1 : 1;
            int pair = i / 2;
            float z = 0.36f - pair * 0.36f;
            float swing = MathF.Sin(u.Anim * MathF.Tau * 1.4f + (pair + (sd > 0 ? 1 : 0)) * 1.6f) * 0.16f * gait;
            var hip = new Vector3(sd * 0.24f, y, z);
            var knee = new Vector3(sd * 0.50f, y + 0.08f, z + swing * 0.5f);
            var foot = new Vector3(sd * 0.58f, 0f, z + swing);
            EmitBone(kit.QuadThigh, hip, knee, Vector3.UnitY);
            EmitBone(kit.WyvernLeg, knee, foot, Vector3.UnitZ);
        }
        EmitLocal(kit.BasiliskBody, new Vector3(sway * 0.4f, y, 0), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        EmitLocal(kit.BasiliskHead, new Vector3(sway, y + 0.10f + rear * 0.14f, 0.62f + rear * 0.12f),
            Vector3.UnitX, RotX(Vector3.UnitY, -rear * 16f), RotX(Vector3.UnitZ, -rear * 16f));
        var t0 = new Vector3(0, y, -0.60f);
        var t1 = new Vector3(-sway * 1.8f, y * 0.65f, -1.12f);
        var t2 = new Vector3(sway * 2.2f, y * 0.35f, -1.62f);
        EmitBone(kit.WyvernTail, t0, t1, Vector3.UnitY);
        EmitBone(kit.WyvernTail, t1, t2, Vector3.UnitY);
    }

    void DrawSandWurm3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        // Sun-baked ochre/sandstone carapace, burnt umber chitin rings, dark gullet & ivory teeth
        UnitMatTable.FillBeast(new Color(184, 148, 96), new Color(118, 84, 52), new Color(46, 32, 22), tint, palCols);
        float rear = BeastRearing(u);
        float wave = MathF.Sin(Now * 2.2f + u.Index) * 0.18f;
        float strike = u.Action == UnitAction.Recover ? rear : 0f;
        var s0 = new Vector3(-wave * 0.6f, 0.10f, -1.35f);
        var s1 = new Vector3(wave * 0.8f, 0.24f, -0.82f);
        var s2 = new Vector3(-wave * 0.5f, 0.46f, -0.32f);
        var s3 = new Vector3(wave * 0.4f, 0.88f - strike * 0.24f, 0.08f + strike * 0.28f);
        var s4 = new Vector3(0, 1.24f - strike * 0.42f, 0.42f + strike * 0.52f);
        EmitBone(kit.WurmSeg, s0, s1, Vector3.UnitY);
        EmitBone(kit.WurmSeg, s1, s2, Vector3.UnitY);
        EmitBone(kit.WurmSeg, s2, s3, Vector3.UnitY);
        EmitBone(kit.WurmSeg, s3, s4, Vector3.UnitY);
        float hPitch = 18f + strike * 32f;
        EmitLocal(kit.WurmHead, s4, Vector3.UnitX, RotX(Vector3.UnitY, hPitch), RotX(Vector3.UnitZ, hPitch));
    }

    void DrawWerewolf3D(Unit u, float ground, Color tint)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        // Ash-charcoal pelage, dark iron mane, amber glowing eyes & ivory claws/fangs
        UnitMatTable.FillBeast(new Color(92, 88, 94), new Color(44, 42, 48), new Color(30, 28, 32), tint, palCols);
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 7.5f, 0, 1);
        float rear = BeastRearing(u);
        float stride = MathF.Sin(u.Anim * MathF.Tau * 1.4f) * 0.34f * gait;
        float bob = MathF.Abs(MathF.Sin(u.Anim * MathF.Tau * 1.4f)) * 0.05f * gait;
        float hipY = 0.74f + bob;
        // 2-bone digitigrade wolf legs (knee forward, hock/ankle back!)
        for (int sd = -1; sd <= 1; sd += 2)
        {
            float s = sd > 0 ? stride : -stride;
            var hip = new Vector3(sd * 0.17f, hipY, -0.04f);
            var foot = new Vector3(sd * 0.20f, 0f, s + 0.04f);
            var knee = Vector3.Lerp(hip, foot, 0.46f) + new Vector3(sd * 0.03f, 0.04f, 0.12f);
            EmitBone(kit.QuadThigh, hip, knee, Vector3.UnitZ);
            EmitBone(kit.QuadShin, knee, foot, Vector3.UnitZ);
        }
        float hunch = 18f + (u.Action == UnitAction.Recover ? 14f * rear : -8f * rear);
        EmitLocal(kit.WerewolfBody, new Vector3(0, hipY - 0.04f, 0), Vector3.UnitX, RotX(Vector3.UnitY, hunch), RotX(Vector3.UnitZ, hunch));
        // 2-bone bent slashing arms!
        for (int sd = -1; sd <= 1; sd += 2)
        {
            var sh = new Vector3(sd * 0.32f, hipY + 0.68f, 0.18f);
            float slash = sd > 0 && u.Action == UnitAction.Recover ? rear : 0f;
            float lift = sd > 0 && u.Action == UnitAction.Windup ? rear : 0f;
            var hand = new Vector3(sd * (0.38f - slash * 0.24f), hipY + 0.10f + lift * 0.65f + slash * 0.25f, 0.42f + slash * 0.48f - lift * 0.10f);
            var elbow = Vector3.Lerp(sh, hand, 0.48f) + new Vector3(sd * 0.10f, -0.06f, -0.14f);
            EmitBone(kit.QuadThigh, sh, elbow, Vector3.UnitZ);
            EmitBone(kit.WerewolfArm, elbow, hand, Vector3.UnitZ);
        }
        EmitLocal(kit.WolfHead, new Vector3(0, hipY + 0.88f, 0.32f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        float wag = MathF.Sin(Now * 4f + u.Index) * 0.10f;
        EmitBone(kit.WolfTail, new Vector3(0, hipY + 0.02f, -0.14f), new Vector3(wag, hipY - 0.28f, -0.66f), Vector3.UnitZ);
    }

    void DrawBrute3D(Unit u, float ground, Color tint)
    {
        // Bespoke hulking hunched rig for Troll, Ogre, Hill Giant, and Giant Chief!
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        Color skin, pelt, trim;
        switch (u.Type.Id)
        {
            case "troll":
                skin = new Color(92, 114, 78);    // mossy bog-green/stone hide
                pelt = new Color(64, 52, 38);     // damp peat fur & rags
                trim = new Color(196, 178, 92);   // amber eyes & tusks
                break;
            case "ogre":
                skin = new Color(156, 114, 84);   // scarred ochre-tanned brute skin
                pelt = new Color(72, 46, 34);     // rawhide loincloth & wolf pelt
                trim = new Color(188, 152, 76);
                break;
            case "giant_chief":
                skin = new Color(124, 128, 134);  // weathered mountain granite skin
                pelt = new Color(56, 46, 44);     // dark mammoth-fur mantle
                trim = new Color(214, 172, 78);   // brass-gold warband eyes & rings
                break;
            default: // hill_giant
                skin = new Color(138, 128, 116);  // weathered crag-grey/tan skin
                pelt = new Color(78, 62, 46);     // shaggy hill-pelt
                trim = new Color(190, 158, 86);
                break;
        }
        UnitMatTable.FillBeast(skin, pelt, trim, tint, palCols);

        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 5.5f, 0, 1);
        float rear = BeastRearing(u);
        float stride = MathF.Sin(u.Anim * MathF.Tau * 1.1f) * 0.30f * gait;
        float bob = MathF.Abs(MathF.Sin(u.Anim * MathF.Tau * 1.1f)) * 0.05f * gait;
        float hipY = 0.78f + bob;

        // Heavy bent 2-bone brute legs
        for (int sd = -1; sd <= 1; sd += 2)
        {
            float s = sd > 0 ? stride : -stride;
            var hip = new Vector3(sd * 0.24f, hipY, -0.02f);
            var foot = new Vector3(sd * 0.28f, 0f, s + 0.04f);
            var knee = Vector3.Lerp(hip, foot, 0.50f) + new Vector3(sd * 0.05f, 0.02f, 0.12f);
            EmitBone(kit.BruteArm, hip, knee, Vector3.UnitZ);
            EmitBone(kit.BruteLeg, knee, foot, Vector3.UnitZ);
        }

        // Hulking hunched torso + fierce head
        float smash = u.Action == UnitAction.Recover ? rear : 0f;
        float windup = u.Action == UnitAction.Windup ? rear : 0f;
        float hunch = 14f + smash * 18f - windup * 12f;
        EmitLocal(kit.BruteBody, new Vector3(0, hipY - 0.04f, 0), Vector3.UnitX, RotX(Vector3.UnitY, hunch), RotX(Vector3.UnitZ, hunch));
        var headPos = new Vector3(0, hipY + 0.90f - smash * 0.08f, 0.24f + smash * 0.10f);
        EmitLocal(kit.BruteHead, headPos, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        if (u.Type.Id == "giant_chief")
        {
            var crown = kit.Get("helm_spangen_horns");
            if (crown.Length > 0)
                EmitLocal(crown, headPos + new Vector3(0, 0.16f, -0.02f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 1.38f, 1.38f, 1.38f);
        }

        // Massive 2-bone brute arms + gnarled tree-trunk club in right hand!
        for (int sd = -1; sd <= 1; sd += 2)
        {
            var sh = new Vector3(sd * 0.44f, hipY + 0.72f, 0.14f);
            bool right = sd > 0;
            var hand = right
                ? new Vector3(0.54f - smash * 0.18f, hipY + 0.02f + windup * 0.85f - smash * 0.26f, 0.36f + smash * 0.46f - windup * 0.12f)
                : new Vector3(-0.48f, hipY - 0.04f + windup * 0.22f, 0.24f - smash * 0.12f);
            var elbow = Vector3.Lerp(sh, hand, 0.50f) + new Vector3(sd * 0.14f, -0.04f, -0.14f);
            EmitBone(kit.BruteArm, sh, elbow, Vector3.UnitZ);
            EmitBone(kit.BruteArm, elbow, hand, Vector3.UnitZ);

            if (right)
            {
                // Hold the gnarled club angled slightly outward & forward so its silhouette reads clearly!
                float clubPitch = 28f - windup * 68f + smash * 56f;
                Vector3 cR = RotY(Vector3.UnitX, -18f);
                Vector3 cU = Vector3.Normalize(RotX(Vector3.UnitY, clubPitch) + new Vector3(0.28f, 0f, 0f));
                Vector3 cF = Vector3.Normalize(Vector3.Cross(cR, cU));
                cR = Vector3.Normalize(Vector3.Cross(cU, cF));
                EmitLocal(kit.BruteClub, hand, cR, cU, cF);
            }
        }
    }
}
