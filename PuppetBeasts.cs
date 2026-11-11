using Microsoft.Xna.Framework;

namespace Ironfield;

/// The beasts of docs/archive/MONSTERS.md as puppets: a giant spider on eight jointed legs, a cave bear that rears to
/// strike, and a wyvern on the wing (its height comes from `Unit.Altitude`). Painted parts in SpriteSheet.Spider/Bear/
/// Wyvern, all in the neutral palette; kin are told apart by tint. Drawing only.
sealed partial class Puppet
{
    /// Which rig draws a whole beast (Look.Beast): the wolf's (Beast in Puppet.cs) or one of these.
    bool OtherBeast(Unit u, float ground)
    {
        switch (look.Face)
        {
            case "spider": SpiderBody(u, ground); return true;
            case "bear": BearBody(u, ground); return true;
            case "wyvern": WyvernBody(u, ground + u.Altitude * Scale); return true;
            default: return false;
        }
    }

    /// How far into its windup (0..1) and out of its blow (1..0) a beast is: drives a rear, a lunge, a stoop.
    static float Rearing(Unit u) => u.Action == UnitAction.Windup ? Math.Clamp(1 - u.ActionTimer / MathF.Max(0.05f, u.Type.Windup), 0, 1)
        : u.Action == UnitAction.Recover ? Math.Clamp(u.ActionTimer / MathF.Max(0.05f, u.Type.Recover) * 2 - 1, 0, 1) : 0;

    /// A giant spider: eight legs in two alternating sets of four, the front pair raised as it rears to bite.
    void SpiderBody(Unit u, float ground)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        var side = pal; var plain = tint;
        pal = "/n";
        if (u.Type.Id == "brood_mother") tint = Color.Lerp(tint, new Color(130, 86, 140), 0.35f); // bloated, black-violet
        else if (u.Type.Id == "spiderling") tint = Color.Lerp(tint, new Color(210, 180, 140), 0.5f); // pale young
        float gait = Math.Clamp(u.Vel.Length() / 6, 0, 1);
        float rear = Rearing(u), lunge = u.Action == UnitAction.Recover ? 0.2f * rear : -0.08f * rear;
        float y = 0.42f + MathF.Sin(u.Anim * MathF.Tau * 2) * 0.015f * gait;
        for (int i = 0; i < 8; i++)
        {
            float sd = i % 2 == 0 ? -1 : 1;
            int pair = i / 2; // 0 front .. 3 back
            float z = 0.2f - pair * 0.13f;
            bool set = (pair + (sd > 0 ? 1 : 0)) % 2 == 0;
            float ph = u.Anim * MathF.Tau + (set ? 0 : MathF.PI);
            float lift = MathF.Max(0, MathF.Sin(ph)) * 0.16f * gait, swing = MathF.Cos(ph) * 0.12f * gait;
            var hip = new Vector3(sd * 0.1f, y, z + lunge * 0.5f);
            var foot = new Vector3(sd * (1.0f - MathF.Abs(1.5f - pair) * 0.08f), lift, z + (1.5f - pair) * 0.42f + swing);
            if (pair == 0) foot = Vector3.Lerp(foot, new Vector3(sd * 0.4f, y + 0.5f, z + 0.55f), rear); // the front legs rise to seize
            var knee = new Vector3((hip.X + foot.X) * 0.7f, y + 0.5f + lift * 0.5f, (hip.Z + foot.Z) / 2);
            Axial(knee, hip, "sleg");
            Axial(foot, knee, "sleg");
        }
        Long(new(0, y + 0.06f, -0.85f), new(0, y + 0.12f, -0.12f), "sabdomen");
        Long(new(0, y + rear * 0.12f, -0.2f + lunge), new(0, y + rear * 0.25f, 0.3f + lunge * 1.3f), "shead");
        pal = side; tint = plain;
    }

    /// A cave bear: a heavy four-legged amble; to strike it rears onto its hind legs and comes down on the blow.
    void BearBody(Unit u, float ground)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        var side = pal; pal = "/n";
        float speed = u.Vel.Length(), gait = Math.Clamp(speed / 6, 0, 1);
        float amp = speed < 0.3f ? 0 : 14 + 22 * gait;
        float y = 0.7f + MathF.Sin(u.Anim * MathF.Tau * 1.2f) * 0.03f * gait;
        float a = Rad(58 * Rearing(u)), c = MathF.Cos(a), s = MathF.Sin(a);
        var pivot = new Vector3(0, y, -0.4f);
        Vector3 P(Vector3 v) { var d = v - pivot; return pivot + new Vector3(d.X, d.Y * c + d.Z * s, -d.Y * s + d.Z * c); } // pitched up about the hind hips
        ReadOnlySpan<float> phase = stackalloc float[] { 0, 0.5f, 0.5f, 0 };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sd = i % 2 == 0 ? -1 : 1;
            float ang = Rad(amp) * MathF.Sin(u.Anim * MathF.PI * 1.2f + phase[i] * MathF.Tau);
            var hip = new Vector3(sd * 0.17f, y - 0.08f, front ? 0.36f : -0.4f);
            var foot = hip + new Vector3(0, -0.62f * MathF.Cos(ang), 0.62f * MathF.Sin(ang));
            if (front) { foot = Vector3.Lerp(foot, P(hip + new Vector3(sd * 0.05f, -0.25f, 0.45f)), s); hip = P(hip); } // paws up and out
            Axial(foot, hip, "bleg");
        }
        Long(P(new(0, y, -0.58f)), P(new(0, y + 0.06f, 0.55f)), "bbody");
        Long(P(new(0, y + 0.14f, 0.42f)), P(new(0, y + 0.04f, 1.0f)), "bhead");
        pal = side;
    }

    /// A wyvern: aloft it beats its wings with its legs trailing; it flares them high as it stoops and drives them down
    /// on the blow, its tail curled over its back to sting. On the ground its wings fold.
    void WyvernBody(Unit u, float ground)
    {
        Frame(u.Pos, u.ViewFacing, ground, u.Type.Size * Scale);
        var side = pal; pal = "/n";
        bool aloft = u.Altitude > 0.4f;
        float rear = Rearing(u);
        float flap = MathF.Sin((Now * 1.7f + Ease.Hash(u.Index)) * MathF.Tau);
        if (u.Action == UnitAction.Windup) flap = MathHelper.Lerp(flap, 1, rear);
        else if (u.Action == UnitAction.Recover) flap = MathHelper.Lerp(flap, -0.8f, rear);
        float y = 0.9f, lunge = u.Action == UnitAction.Recover ? 0.3f * rear : -0.15f * rear;
        for (int sd = -1; sd <= 1; sd += 2)
        {
            Vector3 root = new(sd * 0.18f, y + 0.12f, 0.25f), back = new(sd * 0.15f, y + 0.06f, -0.32f);
            Vector3 tip, tipBack;
            if (aloft || rear > 0) { tip = new(sd * 1.75f, y + 0.15f + flap * 0.95f, 0.05f); tipBack = new(sd * 1.45f, y + 0.05f + flap * 0.8f, -0.7f); }
            else { tip = new(sd * 0.45f, y + 0.75f, -0.15f); tipBack = new(sd * 0.4f, y + 0.25f, -0.75f); } // folded
            Sheet(root, tip, back, tipBack, "ywing");
            var hip = new Vector3(sd * 0.14f, y - 0.05f, -0.2f);
            var foot = aloft ? new Vector3(sd * 0.16f, y - 0.4f, -0.7f) : new Vector3(sd * 0.22f, 0, -0.12f);
            Axial(foot, hip, "yleg");
        }
        Long(new(0, y, -0.6f), new(0, y + 0.05f, 0.5f), "ybody");
        Long(new(0, y + 0.05f, 0.38f + lunge * 0.3f), new(0, y + 0.35f + rear * 0.1f, 0.85f + lunge), "yneck");
        Long(new(0, y + 0.38f + rear * 0.1f, 0.8f + lunge), new(0, y + 0.3f + rear * 0.1f, 1.3f + lunge * 1.2f), "yhead");
        // The tail sways behind; in a strike it curls up over the back, sting first.
        float sway = MathF.Sin(Now * 2.1f + u.Index) * 0.15f;
        var t0 = new Vector3(0, y, -0.55f);
        var t1 = Vector3.Lerp(new Vector3(sway, y - 0.05f, -1.05f), new Vector3(0, y + 0.45f, -0.95f), rear);
        var t2 = Vector3.Lerp(new Vector3(sway * 2, y - 0.1f, -1.5f), new Vector3(0, y + 0.95f, -0.7f), rear);
        var t3 = Vector3.Lerp(new Vector3(sway * 2.4f, y - 0.12f, -1.78f), new Vector3(0, y + 1.1f, -0.38f), rear);
        Axial(t0, t1, "ytail");
        Axial(t1, t2, "ytail");
        Axial(t3, t2, "ysting");
        pal = side;
    }

    /// A web over a man caught in it, drawn in front of him.
    void WebOn(Unit u, float ground, float size)
    {
        var side = pal;
        pal = "/n";
        Frame(u.Pos, u.ViewFacing, ground, size);
        Bill(new(0, u.Mounted ? 1.6f : 0.85f, 0), "web", false, 0.35f);
        pal = side;
    }

    /// A sprite spread over four points in the unit frame: its top edge from `tl` to `tr`, bottom from `bl` to `br`.
    void Sheet(Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br, string name) => Quad(W(bl), W(br), W(tl), W(tr), art[name + pal], false);

    /// A dead beast, lying as it fell (the frame is laid along the fall): a spider on its back with its legs curled up.
    bool OtherBeastCorpse(Unit u)
    {
        var side = pal; var plain = tint;
        pal = "/n";
        switch (look.Face)
        {
            case "spider":
                if (u.Type.Id == "brood_mother") tint = Color.Lerp(tint, new Color(130, 86, 140), 0.35f);
                else if (u.Type.Id == "spiderling") tint = Color.Lerp(tint, new Color(210, 180, 140), 0.5f);
                Flat(new(0, 0.05f, -0.4f), "sabdomen", true);
                Flat(new(0, 0.06f, 0.1f), "shead", true);
                for (int i = 0; i < 8; i++)
                {
                    float sd = i % 2 == 0 ? -1 : 1, z = 0.15f - i / 2 * 0.12f;
                    var hip = new Vector3(sd * 0.08f, 0.1f, z);
                    var knee = new Vector3(sd * 0.3f, 0.35f, z);
                    Axial(knee, hip, "sleg");
                    Axial(new Vector3(sd * 0.08f, 0.5f, z * 0.6f), knee, "sleg");
                }
                break;
            case "bear":
                Flat(new(0, 0.06f, 0), "bbody", true);
                Flat(new(0.05f, 0.07f, 0.7f), "bhead", true);
                Flat(new(-0.3f, 0.05f, 0.3f), "bleg", false);
                Flat(new(-0.3f, 0.05f, -0.3f), "bleg", false);
                break;
            case "wyvern":
                Sheet(new(0, 0.05f, 0.2f), new(1.6f, 0.03f, 0.4f), new(0, 0.05f, -0.3f), new(1.4f, 0.03f, -0.5f), "ywing");
                Sheet(new(0, 0.05f, 0.2f), new(-1.5f, 0.03f, 0.1f), new(0, 0.05f, -0.3f), new(-1.2f, 0.03f, -0.8f), "ywing");
                Flat(new(0, 0.08f, 0), "ybody", true);
                Flat(new(0.3f, 0.07f, 0.9f), "yhead", true);
                Axial(new(0, 0.06f, -0.5f), new(0.3f, 0.05f, -1.1f), "ytail");
                break;
            default:
                pal = side; tint = plain;
                return false;
        }
        pal = side; tint = plain;
        return true;
    }
}
