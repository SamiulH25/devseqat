using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen: the aura ladder and the aura's moments (docs/SPECTACLE.md S4-S6). Each tier shows more: tier 2 leaves
/// footprints of light, tier 3 stirs the dust and the air wavers round a lit blade, tier 4 presses the grass flat in a ring
/// and the foe flinch from it, tier 5 stands in a column of light. A high tier's cut hangs in the air as a crescent; an
/// order's aura leaves its own mark (petals, frost, feathers, embers). A clash dims the field; the Black Flame burns at the
/// edge of the view. A charge of horse can take the battle's beat. Drawing only.
partial class BattleScreen
{
    Vector2 lastStep;
    bool leftFoot;
    float clashSpark, chargeLook, warpNow;

    /// The order whose aura you carry (its forms), or "".
    string AuraOrder
    {
        get
        {
            if (battle.Mad) return "hollow";
            foreach (var id in battle.Options.Forms)
                if (Forms.Has(id) && Forms.Get(id).Order is { Length: > 0 } o) return o;
            return "";
        }
    }

    /// How long a cut of the lit blade hangs in the air: a crescent that lingers at a high tier.
    float TrailLife => 0.22f + 0.12f * Math.Max(0, battle.AuraTier - 3);

    void ResetLadder() { lastStep = Vector2.Zero; clashSpark = chargeLook = warpNow = 0; }

    /// Each frame: the tier's marks round you, the clash, the Black Flame, a charge worth the beat.
    void LadderFx(float dt)
    {
        var p = battle.Player;
        int tier = battle.AuraTier;
        var col = AuraColour;
        if (p.Active && tier >= 2 && !battle.Spent)
        {
            float stride = p.Mounted ? 1.7f : 0.85f;
            if (Vector2.Distance(p.Pos, lastStep) > stride)
            {
                var dir = p.Pos - lastStep;
                lastStep = p.Pos;
                if (dir.LengthSquared() > 0) dir.Normalize();
                leftFoot = !leftFoot;
                var foot = p.Pos + new Vector2(-dir.Y, dir.X) * (leftFoot ? 0.14f : -0.14f);
                motes.Add(new Mote { Pos = G(foot, 0.03f), Life = 1.4f, Max = 1.4f, Size = 0.1f, Col = Color.Lerp(col, Color.White, 0.2f) });
                if (tier >= 3) Spray(foot, 0.05f, 2, new Color(186, 172, 140), 0.8f, 0.9f, 0.16f, false, 0.4f); // the dust stirs
            }
            if (tier >= 3 && battle.AuraBlade && Vector3.Distance(eye, G(p.Pos)) < 30) Shimmer(0.25f + 0.1f * (tier - 3));
            if (tier >= 5 && fx.NextSingle() < dt * 10) // motes climb the column
                motes.Add(new Mote { Pos = G(p.Pos + Jitter(0.6f), 0.2f), Vel = new Vector3(0, 3.5f, 0), Life = 1.6f, Max = 1.6f, Size = 0.07f, Col = Color.Lerp(col, Color.White, 0.5f) });
        }
        if (battle.Mad && p.Active && fx.NextSingle() < dt * 30) // black fire off you
            Spray(p.Pos + Jitter(0.4f), 0.4f + fx.NextSingle() * 1.4f, 1, fx.Next(3) == 0 ? new Color(20, 8, 24) : new Color(110, 40, 150), 0.3f, 0.9f, 0.12f, false, 0, -1.6f);
        warpNow += ((battle.Mad && quality != Quality.Low ? 1 : 0) - warpNow) * MathF.Min(1, dt * 2);
        ClashFx(dt);
        ChargeBeat(dt);
    }

    /// A clash of champions: the field dims round the ring, sparks leap where the blades meet.
    void ClashFx(float dt)
    {
        if (!battle.ClashOn) return;
        Omen(0.4f);
        clashSpark -= dt;
        var e = battle.EnemyCaptain;
        if (clashSpark <= 0 && battle.Player.Active && e.Active)
        {
            clashSpark = 0.45f + fx.NextSingle() * 0.4f;
            var mid = (battle.Player.Pos + e.Pos) / 2;
            Spray(mid, 1.3f, 14, Color.Lerp(AuraColour, Color.White, 0.5f), 4f, 0.35f, 0.06f, true);
            beams.Add(new Beam { At = mid, Born = battle.Time, Life = 0.25f, Radius = 0.35f, Col = Color.White });
        }
    }

    /// Your horse going in at the gallop against a formed company, close to the eye: the battle's beat, if it is unspent.
    void ChargeBeat(float dt)
    {
        chargeLook -= dt;
        if (chargeLook > 0 || beats.Spent) return;
        chargeLook = 0.25f;
        var c = new Vector2(eye.X, eye.Z);
        foreach (var s in battle.Squads)
        {
            if (s.Team != 0 || !s.Cavalry || s.Order != Order.Charge || s.Alive < 8) continue;
            if (Vector2.Distance(s.Center, c) > 70 && !selected.Contains(s)) continue;
            float speed = s.Units.Where(u => u.Active && u.Mounted).Select(u => u.Speed).DefaultIfEmpty(0).Average();
            if (speed < 8) continue;
            foreach (var f in battle.Squads)
                if (f.Team == 1 && f.Alive >= 8 && !f.Cavalry && f.Formed > 0.7f && Vector2.Distance(f.Center, s.Center) < 14)
                {
                    Beat(BeatKind.Charge, (s.Center + f.Center) / 2, 2.2f);
                    return;
                }
        }
    }

    /// An order's aura leaves its own mark where a lit blade lands.
    void Motif(string order, Vector2 at, bool big)
    {
        int n = big ? 16 : 5;
        switch (order)
        {
            case "lily": Spray(at, 1.6f, n, new Color(246, 246, 255), 1.2f, 2.2f, 0.07f, false, 0, 0.5f); break;
            case "rose": Spray(at, 1.6f, n, new Color(214, 40, 60), 1.2f, 2.2f, 0.07f, false, 0, 0.5f); break;
            case "wolf": Spray(at, 1.0f, n, new Color(210, 236, 255), 1.6f, 1.2f, 0.06f, false, 0, 0.3f); if (big) AddScar(at, 1.6f, ScarKind.Frost); break;
            case "falcon": Spray(at, 1.4f, n, new Color(150, 96, 52), 3f, 1.4f, 0.09f, false, 0, 0.8f); break;
            case "sky": Spray(at, 1.2f, n, new Color(170, 236, 255), 2.4f, 0.6f, 0.05f, false, 0, -1.5f); break;
            case "lantern": Spray(at, 1.0f, n, new Color(255, 220, 130), 0.8f, 1.6f, 0.06f, false, 0, -1.2f); break;
            case "ashen": Spray(at, 1.0f, n, new Color(255, 120, 40), 2.5f, 0.8f, 0.05f, true); Spray(at, 1.2f, n / 2, new Color(90, 86, 80), 0.4f, 1.6f, 0.2f, false, 0.4f, -0.6f); break;
            case "hollow": Spray(at, 0.8f, n, new Color(60, 16, 80), 0.5f, 1.2f, 0.12f, false, 0, -1.4f); break;
        }
    }

    /// Lines of the ladder: the column of a Transcendent (yours and a foe champion's), the two halves of a clash ring.
    void LadderLines()
    {
        var p = battle.Player;
        float t = battle.Time;
        if (p.Active && battle.AuraTier >= 5 && !battle.Spent) Column(p.Pos, AuraColour, 34);
        var e = battle.EnemyCaptain;
        if (e.Active && battle.FoeTier >= 5) Column(e.Pos, Color.Lerp(Style.Foe, Color.White, 0.3f), 26);
        if (battle.ClashOn && p.Active && e.Active)
        {
            var at = battle.ClashAt;
            var d = p.Pos - at;
            float a = MathF.Atan2(d.Y, d.X), pulse = 0.8f + 0.2f * MathF.Sin(t * 8);
            Arc(at, Battle.ClashRadius, a - MathHelper.PiOver2, MathF.PI, Color.Lerp(AuraColour, Color.Black, 1 - pulse));
            Arc(at, Battle.ClashRadius, a + MathHelper.PiOver2, MathF.PI, Color.Lerp(Style.Foe, Color.Black, 1 - pulse));
        }
    }

    /// A column of light seen across the field: shafts round a narrow core, dimming as they rise.
    void Column(Vector2 at, Color col, float tall)
    {
        float t = battle.Time;
        for (int i = 0; i < 5; i++)
        {
            float a = i * MathF.Tau / 5 + t * 0.7f;
            var q = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 0.45f;
            for (int j = 0; j < 4; j++) Line(G(q, 0.2f + tall * j / 4), G(q, 0.2f + tall * (j + 1) / 4), Color.Lerp(col, Color.Black, 0.25f + 0.17f * j));
        }
        Line(G(at, 0.2f), G(at, tall * 1.1f), Color.Lerp(col, Color.White, 0.5f));
    }
}
