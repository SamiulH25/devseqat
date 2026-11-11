using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// BattleScreen: input in field view (you fight) and command view (you direct squads), picking and the camera.
partial class BattleScreen
{
    // ---------- controls ----------

    static Vector2 Forward(float yaw) => new(MathF.Cos(yaw), MathF.Sin(yaw));
    static Vector2 RightOf(Vector2 f) => new(-f.Y, f.X); // screen right when looking along f

    void FieldControls()
    {
        bool wasWheel = wheelOpen;
        wheelOpen = ctl.Down(Act.OrderWheel) || shotWheel;
        if (wheelOpen && !wasWheel) wheelVec = Vector2.Zero;
        if (IsActive)
        {
            // shortcut: mouse look by warping to the centre; switch to SDL relative mode if a platform refuses warps.
            var c = new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            var d = (ms.Position - c).ToVector2();
            if (!skipMouse)
            {
                if (wheelOpen)
                {
                    wheelVec += d;
                    if (wheelVec.Length() > 120) wheelVec = Vector2.Normalize(wheelVec) * 120;
                }
                else
                {
                    float k = 0.0035f * ctl.Sensitivity;
                    yaw += d.X * k;
                    pitch = Math.Clamp(pitch + d.Y * k * (ctl.InvertY ? -1 : 1), -0.35f, 1.1f);
                    // Mount & Blade style directions: the way the mouse moves picks the swing (and the block).
                    aimAcc = aimAcc * 0.8f + d;
                    if (aimAcc.LengthSquared() > 36)
                        aimDir = MathF.Abs(aimAcc.X) > MathF.Abs(aimAcc.Y)
                            ? (aimAcc.X > 0 ? AttackDir.Right : AttackDir.Left)
                            : (aimAcc.Y < 0 ? AttackDir.Overhead : AttackDir.Thrust);
                }
            }
            skipMouse = false;
            Mouse.SetPosition(c.X, c.Y);
        }
        else skipMouse = true;
        if (wasWheel && !wheelOpen && WheelSector() is int sector)
        {
            if (selectedDivision is { } dv) GoalCommand(dv, Hud.GoalWheelOrder[sector].G, Hud.GoalWheelOrder[sector].Side);
            else Command(Hud.WheelOrder[sector]);
        }
        fieldDist = Math.Clamp(fieldDist - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.004f, 2.5f, 12);

        var f = Forward(yaw);
        bool mounted = battle.Player.Mounted;
        var move = mounted ? Vector2.Zero : f * ctl.Axis(Act.Forward, Act.Back) + RightOf(f) * ctl.Axis(Act.Right, Act.Left);
        if (move != Vector2.Zero) move.Normalize();
        battle.Input = new PlayerInput
        {
            Move = move,
            Throttle = mounted ? ctl.Axis(Act.Forward, Act.Back) : 0, // on horseback forward/back is the rein, left/right turns the horse
            Steer = mounted ? ctl.Axis(Act.Right, Act.Left) : 0,
            Dir = aimDir,
            Couch = ctl.Pressed(Act.Couch),
            Mount = ctl.Pressed(Act.Interact),
            Facing = yaw, // the player looks (and strikes) where the camera looks
            Sprint = ctl.Down(Act.Sprint),
            Attack = IsActive && !wheelOpen && ctl.Down(Act.Attack),
            Block = IsActive && !wheelOpen && ctl.Down(Act.Block),
            Rally = ctl.Pressed(Act.Rally),
            Bash = ctl.Pressed(Act.Bash),
            Swap = ctl.Pressed(Act.SwapWeapon),
            Aura = IsActive && !wheelOpen && ctl.Down(Act.AuraBlade),
            Form1 = ctl.Pressed(Act.Form1), Form2 = ctl.Pressed(Act.Form2), Form3 = ctl.Pressed(Act.Form3), Great = ctl.Pressed(Act.GreatForm), Relic = ctl.Pressed(Act.Relic),
        };
    }

    int? WheelSector()
    {
        if (!wheelOpen || wheelVec.Length() < 30) return null;
        float a = MathF.Atan2(wheelVec.Y, wheelVec.X) + MathHelper.PiOver2; // 0 = up, clockwise
        int n = Hud.WheelOrder.Length;
        return ((int)MathF.Round(a / MathHelper.TwoPi * n) % n + n) % n;
    }

    void CommandControls(float dt)
    {
        wheelOpen = false;
        var vp = GraphicsDevice.Viewport;
        var f = Forward(camYaw);
        var pan = f * ctl.Axis(Act.Forward, Act.Back) + RightOf(f) * ctl.Axis(Act.Right, Act.Left);
        if (IsActive && vp.Bounds.Contains(ms.Position))
        {
            // Edge scrolling.
            const int edge = 6;
            if (ms.X < edge) pan -= RightOf(f); else if (ms.X > vp.Width - edge) pan += RightOf(f);
            if (ms.Y < edge) pan += f; else if (ms.Y > vp.Height - edge) pan -= f;
        }
        camFocus += pan * zoom * 0.9f * dt;
        if (camFocus.Length() > battle.Terrain.Half - 20) camFocus = Vector2.Normalize(camFocus) * (battle.Terrain.Half - 20);
        camYaw += ctl.Axis(Act.RotateRight, Act.RotateLeft) * 1.6f * dt;
        if (ms.MiddleButton == ButtonState.Pressed && pms.MiddleButton == ButtonState.Pressed)
        {
            // Middle-drag orbits: horizontal turns, vertical tilts.
            camYaw += (ms.X - pms.X) * 0.006f;
            camPitch = Math.Clamp(camPitch + (ms.Y - pms.Y) * 0.006f, 0.25f, 1.45f);
        }
        zoom = Math.Clamp(zoom - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.03f, 10, MathHelper.Clamp(battle.MapRadius * 1.3f, 110, 260)); // a big field lets you pull further out
        battle.Input = new PlayerInput { Dir = aimDir };
        if (!IsActive) return;
        mouseGround = GroundUnderMouse();
        bool add = ctl.Down(Act.AddToSelection);

        // Left button: HUD first (buttons, cards, minimap), then the field (click or box select).
        if (castBy != null && (castBy.Alive == 0 || !selected.Contains(castBy) && selected.Count > 0)) { castBy = null; castPick = -1; }
        if (castBy != null && ms.RightButton == ButtonState.Pressed && pms.RightButton == ButtonState.Released) { castBy = null; castPick = -1; return; }
        if (castBy != null && LmbPressed && !hud.Over(ms.Position)) { CastAt(mouseGround); return; }
        if (LmbPressed)
        {
            if (selectedDivision != null && hud.GoalAt(ms.Position) is var (gk, gs)) GoalCommand(selectedDivision, gk, gs);
            else if (hud.DivisionAt(ms.Position) is Division dc)
            {
                selectedDivision = selectedDivision == dc ? null : dc;
                if (selectedDivision != null)
                {
                    selected.Clear();
                    if (realTime - lastCardClick < DoubleClick && dc.Squads.Any(s => s.Alive > 0)) camFocus = dc.Squads.First(s => s.Alive > 0).Centroid();
                }
                lastCardClick = realTime;
            }
            else if (hud.DeployToggleAt(ms.Position) && memory != null)
            {
                memory.DeployAsLast = !memory.DeployAsLast;
                battle.ApplyDeployment(memory.DeployAsLast ? memory.LastDeploy : defaultDeploy);
                battle.Log(memory.DeployAsLast ? "As in your last battle." : "The usual array.");
            }
            else if (hud.ButtonAt(ms.Position) is Cmd cmd) Command(cmd);
            else if (hud.AlertAt(ms.Position) is Alert al)
            {
                // An alert: look there, and pick the company if it is yours to command.
                camFocus = al.Squad is { Alive: > 0 } aq ? aq.Centroid() : al.At;
                if (al.Squad is { } q && Selectable.Contains(q)) { selected.Clear(); selected.Add(q); selectedDivision = null; }
            }
            else if (hud.CardAt(ms.Position) is Squad card)
            {
                if (card == lastCardSquad && realTime - lastCardClick < DoubleClick) camFocus = card.Centroid(); // double-click: look at it
                lastCardSquad = card;
                lastCardClick = realTime;
                selectedDivision = null;
                if (add) { if (!selected.Remove(card)) selected.Add(card); }
                else { selected.Clear(); selected.Add(card); }
            }
            else if (hud.MinimapAt(ms.Position) is Vector2 w) camFocus = w;
            else if (!hud.Over(ms.Position)) boxStart = ms.Position;
        }
        if (LmbReleased && boxStart is Point start)
        {
            boxStart = null;
            var box = BoxOf(start, ms.Position);
            if (box.Width < 6 && box.Height < 6)
            {
                var sq = PickSquad(mouseGround, 0);
                if (sq?.AiControlled == true) sq = null;
                if (sq != null) selectedDivision = null;
                if (sq == null) { if (!add) selected.Clear(); }
                else if (add) { if (!selected.Remove(sq)) selected.Add(sq); }
                else { selected.Clear(); selected.Add(sq); }
            }
            else
            {
                if (!add) selected.Clear();
                foreach (var s in Selectable)
                    if (s.Units.Any(u => u.Active && !u.Routing && OnScreen(u.Pos, out var sp) && box.Contains(sp))) selected.Add(s);
            }
        }

        // Right button: move / draw a line / attack.
        if (ms.RightButton == ButtonState.Pressed && pms.RightButton == ButtonState.Released && !hud.Over(ms.Position)) dragStart = mouseGround;
        if (ms.RightButton == ButtonState.Released && dragStart is Vector2 a)
        {
            dragStart = null;
            var enemy = battle.Centroid(1);
            bool click = Vector2.Distance(a, mouseGround) < 2;
            if (selectedDivision is { } dv && !deploying)
            {
                // A division takes goals, not orders: an enemy is Attack, a friend is Screen, ground is Take.
                if (click && PickSquad(mouseGround, 1) is Squad tf) GiveGoal(dv, new Goal(GoalKind.Attack, Target: tf));
                else if (click && PickSquad(mouseGround, 0) is Squad fr && fr.Division != dv) GiveGoal(dv, new Goal(GoalKind.Screen, Target: fr));
                else GiveGoal(dv, new Goal(GoalKind.Take, mouseGround));
                foreach (var ds in dv.Squads.Where(q => q.Alive > 0)) arrows.Add((ds.Centroid(), mouseGround, Ui.Now));
                return;
            }
            if (click && PickSquad(mouseGround, 1) is Squad foe)
            {
                OrderArrows(foe.Centroid());
                Issue("attack " + foe.Name + "!", s => { s.Order = Order.Charge; s.Target = foe; }, move: true); // click an enemy: attack it
                return;
            }
            OrderArrows(click ? mouseGround : (a + mouseGround) / 2);
            if (click) LineAt(mouseGround, enemy);    // click: move, keep width
            else Distribute(a, mouseGround, enemy);   // drag: draw the line
            battle.Log((selected.Count == 0 ? "All troops: " : "Squads: ") + (queueing ? "then move there" : "move"));
        }
    }

    static Rectangle BoxOf(Point a, Point b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    bool OnScreen(Vector2 p, out Point sp)
    {
        var v = GraphicsDevice.Viewport.Project(new Vector3(p.X, battle.Terrain.Height(p) + 1, p.Y), proj, view, Matrix.Identity);
        sp = new Point((int)v.X, (int)v.Y);
        return v.Z is > 0 and < 1;
    }

    Squad? PickSquad(Vector2 p, int team)
    {
        Unit? best = null;
        float bd = 2f * 2f;
        foreach (var u in battle.Units)
        {
            if (u.Team != team || u.Squad == null || !u.Active || u.Routing) continue;
            float d = Vector2.DistanceSquared(u.Pos, p);
            if (d < bd) { bd = d; best = u; }
        }
        return best?.Squad;
    }

    /// Ray-march the mouse ray onto the terrain.
    Vector2 GroundUnderMouse()
    {
        var vp = GraphicsDevice.Viewport;
        var near = vp.Unproject(new Vector3(ms.X, ms.Y, 0), proj, view, Matrix.Identity);
        var far = vp.Unproject(new Vector3(ms.X, ms.Y, 1), proj, view, Matrix.Identity);
        var d = Vector3.Normalize(far - near);
        var t = battle.Terrain;
        float Above(Vector3 p) => p.Y - t.Height(new Vector2(p.X, p.Z));
        float s = 0, step = 1;
        for (int i = 0; i < 800 && s < 600; i++)
        {
            var p = near + d * s;
            float h = Above(p);
            if (h > 0) { step = MathF.Max(0.5f, h * 0.5f); s += step; continue; }
            float lo = MathF.Max(0, s - step), hi = s; // bisect the crossing
            for (int k = 0; k < 10; k++) { float m = (lo + hi) / 2; if (Above(near + d * m) > 0) lo = m; else hi = m; }
            var hit = near + d * hi;
            return new Vector2(hit.X, hit.Z);
        }
        return camFocus;
    }

    void UpdateCamera()
    {
        // Both views are an orbit (focus, yaw, pitch, distance); the transition eases every parameter.
        var t = battle.Terrain;
        var p = battle.Player.Pos;
        bool mounted = battle.Player.Mounted;
        var shoulder = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw)) * 0.8f; // over the right shoulder
        var fieldFocus = new Vector3(p.X, t.Height(p) + (mounted ? 2.6f : 1.7f), p.Y) + shoulder;
        var cmdFocus = new Vector3(camFocus.X, t.Height(camFocus), camFocus.Y);
        float k = MathHelper.SmoothStep(0, 1, blend);
        var focus = Vector3.Lerp(fieldFocus, cmdFocus, k);
        float y = yaw + MathHelper.WrapAngle(camYaw - yaw) * k; // shortest way round
        float pt = MathHelper.Lerp(pitch, camPitch, k), dist = MathHelper.Lerp(fieldDist + (mounted ? 1.5f : 0), zoom, k);
        if (intro >= 0)
        {
            // The opening: from high over the far side the camera swings round and down to where you deploy.
            float e = Ease.InOut(intro / IntroTime), o = 1 - e;
            focus = Vector3.Lerp(new Vector3(0, t.Height(Vector2.Zero), 0), focus, e);
            y += 2.2f * o; pt = MathHelper.Lerp(pt, 0.85f, 0.5f * o); dist *= 1 + 0.9f * o;
        }
        if (ending >= 0 && shotDir == null)
        {
            // The fight is decided: the camera draws up and back to show the field.
            float r = Ease.InOut(Math.Clamp(ending / 2.5f, 0, 1)) * (1 - k);
            dist += 7 * r; pt += 0.3f * r;
        }
        var look = new Vector3(MathF.Cos(y) * MathF.Cos(pt), -MathF.Sin(pt), MathF.Sin(y) * MathF.Cos(pt));
        var shake = Shake();
        eye = focus - look * dist + shake;
        focus += shake * 0.6f;
        focusPoint = focus;
        eye.Y = MathF.Max(eye.Y, t.Height(new Vector2(eye.X, eye.Z)) + 0.5f); // never under the ground
        proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(pressFov is float pf && k > 0.5f ? pf : MathHelper.Lerp(62, 45, k)), GraphicsDevice.Viewport.AspectRatio, 0.2f, 700);
        view = Matrix.CreateLookAt(eye, focus, Vector3.Up);
        hud.CamYaw = y;
        hud.CamFocus = camFocus;
    }
}
