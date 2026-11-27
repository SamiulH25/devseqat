using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// Battle planning (docs/battle-overhaul/PILLAR_2_BATTLE_PLANNING.md): the glowing deployment line on the ground, companies
/// kept behind it, turned in place with Q/E and dragged with the left button, and the camera's drop onto the captain
/// when the battle begins. Drawing and input only; the rule itself is `Battle.DeployBounds` (BattleDeploy.cs).
partial class BattleScreen
{
    const float DeployFadeTime = 0.45f, DropTime = 1.1f, DeployTurnRate = 1.2f;
    /// The deployment line's light after the battle begins (1 = full, fades to 0), when a company was last held back by
    /// it (Ui.Now), and seconds into the drop onto the captain (-1 = none).
    float deployFade, deployPushed = -10, dropIn = -1;
    /// The left-button drag of chosen companies in deployment: the ground point held, and whether it has moved yet.
    Vector2? moveDrag;
    Point moveFrom;
    bool moveMoved;

    static readonly Color DeployBlue = new(110, 184, 255);

    /// A line's ends brought behind the deployment line; a glow on the line if either had to give way.
    void ClampToZone(ref Vector2 a, ref Vector2 b)
    {
        var ca = battle.ClampDeployPos(a);
        var cb = battle.ClampDeployPos(b);
        if (ca != a || cb != b) deployPushed = Ui.Now;
        a = ca; b = cb;
    }

    /// The battle begins: the horn, the line fades, and the camera drops from the planning view onto the captain.
    void BeginFight()
    {
        deploying = false;
        deployed = battle.DeploymentOf(); // how you drew them up, for the next battle (Q6)
        battle.Log("The battle begins!");
        deployFade = 1;
        moveDrag = null;
        host.Audio?.Play(Cue.HornCharge, 0.8f);
        if (!battle.Player.Active) return;
        var foe = battle.Centroid(1) - battle.Player.Pos;
        yaw = foe.LengthSquared() > 1 ? MathF.Atan2(foe.Y, foe.X) : MathHelper.PiOver2; // face their line on arrival
        pitch = 0.2f;
        SetCommand(false);
        dropIn = 0;
    }

    /// Advance the planning timers; returns the camera blend while the drop runs (null = the usual eased blend).
    float? PlanningFrame(float dt)
    {
        if (!deploying && deployFade > 0) deployFade = MathF.Max(0, deployFade - dt / DeployFadeTime);
        if (dropIn < 0) return null;
        if (command) { dropIn = -1; return null; } // back to the command view mid-drop: the usual blend takes over
        dropIn += dt;
        float t = Math.Clamp(dropIn / DropTime, 0, 1);
        if (t >= 1) dropIn = -1;
        return 1 - t; // UpdateCamera smooths it in and out
    }

    /// Q/E in deployment with companies chosen: turn them in place about their middle (else Q/E turn the camera).
    bool TurnDeployed(float dt)
    {
        if (!deploying || selected.Count == 0) return false;
        float turn = ctl.Axis(Act.RotateRight, Act.RotateLeft) * DeployTurnRate * dt;
        if (turn == 0) return true;
        var list = Targets();
        if (list.Count == 0) return true;
        var mid = Vector2.Zero;
        foreach (var s in list) mid += s.Anchor;
        mid /= list.Count;
        var rot = Matrix.CreateRotationZ(turn);
        foreach (var s in list)
            Send(s, q =>
            {
                var at = mid + Vector2.Transform(q.Anchor - mid, rot);
                var held = battle.ClampDeployPos(at);
                if (held != at) deployPushed = Ui.Now;
                q.Anchor = held;
                q.Front = Vector2.Normalize(Vector2.Transform(q.Front, rot));
                q.Order = Order.Hold;
                q.Target = null;
            });
        return true;
    }

    /// Left button down on a chosen company in deployment: take hold of the chosen companies to drag them.
    bool GrabDeployed()
    {
        if (!deploying || selected.Count == 0 || hud.Over(ms.Position)) return false;
        if (PickSquad(mouseGround, 0) is not Squad s || !selected.Contains(s)) return false;
        moveDrag = mouseGround;
        moveFrom = ms.Position;
        moveMoved = false;
        return true;
    }

    /// While the left button is held on them, the chosen companies follow the mouse, kept behind the deployment line as
    /// one body; a click without a drag still picks as before.
    void DragDeployed()
    {
        if (moveDrag is not Vector2 held) return;
        if (!deploying || ms.LeftButton != ButtonState.Pressed) { moveDrag = null; return; }
        if (!moveMoved)
        {
            if (Math.Abs(ms.X - moveFrom.X) + Math.Abs(ms.Y - moveFrom.Y) < 6) return;
            moveMoved = true;
            boxStart = null; // a drag, not a click or a box
        }
        var want = mouseGround - held;
        if (want.LengthSquared() < 0.0025f) return;
        var list = Targets();
        var (x0, x1, y0, y1) = battle.DeployBounds();
        float lx = float.MinValue, hx = float.MaxValue, ly = float.MinValue, hy = float.MaxValue;
        foreach (var s in list)
        {
            lx = MathF.Max(lx, x0 - s.Anchor.X); hx = MathF.Min(hx, x1 - s.Anchor.X);
            ly = MathF.Max(ly, y0 - s.Anchor.Y); hy = MathF.Min(hy, y1 - s.Anchor.Y);
        }
        var d = new Vector2(Math.Clamp(want.X, MathF.Min(lx, 0), MathF.Max(hx, 0)), Math.Clamp(want.Y, MathF.Min(ly, 0), MathF.Max(hy, 0)));
        if (d != want) deployPushed = Ui.Now;
        if (d.LengthSquared() < 1e-6f) return;
        foreach (var s in list) Send(s, q => { q.Anchor += d; q.Order = Order.Hold; q.Target = null; });
        moveDrag = held + d;
    }

    /// The deployment line on the ground (P2.1): a steel-blue ribbon along your front limit, its light running along it,
    /// spilling a little onto your side and fading toward the field's edges; brighter for a moment when it holds a company
    /// back, and fading out as the battle begins.
    void DeployBoundary()
    {
        float alpha = deploying ? 1 : Ease.InOut(deployFade);
        if (alpha <= 0.01f || battle.Options.Duel) return;
        var (x0, x1, _, front) = battle.DeployBounds();
        float push = MathF.Max(0, 1 - (Ui.Now - deployPushed) / 0.6f);
        float glow = alpha * (1 + 0.9f * push);
        const float step = 2f, spill = 7f, lift = 0.25f;
        for (float x = x0; x < x1; x += step)
        {
            float xm = x + step * 0.5f;
            float ends = MathHelper.SmoothStep(0, 1, MathF.Min(xm - x0, x1 - xm) / 40f);
            float run = 0.7f + 0.3f * MathF.Sin(xm * 0.12f - Ui.Now * 2.6f);
            float k = MathF.Min(1, glow * ends * run);
            if (k <= 0.01f) continue;
            Vector2 a = new(x, front), b = new(MathF.Min(x + step, x1), front);
            // The spill: a soft wash on your side of the line, bright at the line and gone a few metres back.
            var wash = DeployBlue * (0.30f * k);
            Vector3 a0 = G(a, lift), b0 = G(b, lift), a1 = G(a - Vector2.UnitY * spill, lift), b1 = G(b - Vector2.UnitY * spill, lift);
            bands.Add(new(a0, wash)); bands.Add(new(b0, wash)); bands.Add(new(b1, Color.Transparent));
            bands.Add(new(a0, wash)); bands.Add(new(b1, Color.Transparent)); bands.Add(new(a1, Color.Transparent));
            Band(G(a, lift + 0.02f), G(b, lift + 0.02f), DeployBlue * (0.35f * k), 5f); // the halo
            Band(G(a, lift + 0.04f), G(b, lift + 0.04f), Color.Lerp(DeployBlue, Color.White, 0.35f + 0.4f * push) * k, 1.4f); // the core
            // Dashes just over the line on the enemy's side: the ground you may not take.
            if (((int)MathF.Floor(x / step) & 1) == 0)
                Band(G(new Vector2(xm - 0.6f, front + 1.6f), lift), G(new Vector2(xm + 0.6f, front + 1.6f), lift), DeployBlue * (0.55f * k), 0.8f);
        }
    }
}
