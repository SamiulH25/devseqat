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
            if (selectedDivision is { } dv) GiveGoal(dv, Hud.WheelGoals(dv)[sector]);
            else Command(Hud.WheelOrder[sector]);
        }
        fieldDist = Math.Clamp(fieldDist - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.004f, 2.5f, 12);

        bool mounted = battle.Player.Mounted;
        if (mounted) yaw += battle.Reined; // turning the horse on the reins turns the view with it (the horse follows the view: BattlePlayer.cs)
        battle.Reined = 0;
        var f = Forward(yaw);
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
            EngineAim = battle.Manned != null ? GroundUnder(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2) : null, // an engine you work looses where the crosshair is
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
        wheelOpen = shotWheel; // the ring is the fight view's; here the deck and the right button give orders
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
        if (!float.IsFinite(camFocus.X) || !float.IsFinite(camFocus.Y)) camFocus = battle.Player.Pos;
        if (camFocus.Length() > battle.Terrain.Half - 20) camFocus = Vector2.Normalize(camFocus) * (battle.Terrain.Half - 20);
        camYaw += ctl.Axis(Act.RotateRight, Act.RotateLeft) * 1.6f * dt;
        if (IsActive && ms.MiddleButton == ButtonState.Pressed && pms.MiddleButton == ButtonState.Pressed)
        {
            // Middle-drag orbits: horizontal turns, vertical tilts.
            camYaw += (ms.X - pms.X) * 0.006f;
            camPitch = Math.Clamp(camPitch + (ms.Y - pms.Y) * 0.006f, 0.25f, 1.45f);
        }
        float oldZoom = zoom;
        zoom = Math.Clamp(zoom - (ms.ScrollWheelValue - pms.ScrollWheelValue) * 0.03f, 10, MathHelper.Clamp(battle.MapRadius * 1.3f, 110, 260)); // a big field lets you pull further out
        if (zoom < oldZoom && IsActive && !hud.Over(ms.Position))
            camFocus += (GroundUnderMouse() - camFocus) * ((oldZoom - zoom) / oldZoom) * 0.65f;
        battle.Input = new PlayerInput { Dir = aimDir };
        if (!IsActive) return;
        mouseGround = GroundUnderMouse();
        bool add = ctl.Down(Act.AddToSelection);
        Cursors.Want = CommandCursor();
        if (wheelOpen) return;

        // Left button: HUD first (buttons, cards, minimap), then the field (click or box select).
        if (castBy != null && (castBy.Alive == 0 || !selected.Contains(castBy) && selected.Count > 0)) { castBy = null; castPick = -1; }
        if (castBy != null && ms.RightButton == ButtonState.Pressed && pms.RightButton == ButtonState.Released) { castBy = null; castPick = -1; return; }
        if (castBy != null && LmbPressed && !hud.Over(ms.Position)) { CastAt(mouseGround); return; }
        if (LmbPressed && CommandClick(ms.Position)) return; // tray, ADD, answers, the horn (BattleScreen.Orders.cs)
        if (attackMoveMode && IsActive)
        {
            if (ms.RightButton == ButtonState.Pressed && pms.RightButton == ButtonState.Released) { attackMoveMode = false; return; }
            if (LmbPressed)
            {
                Vector2? at = hud.MinimapAt(ms.Position) is Vector2 mw ? mw : !hud.Over(ms.Position) ? mouseGround : null;
                if (at is Vector2 pt)
                {
                    attackMoveMode = false;
                    if (selectedDivision is { } dv && !deploying) OrderDivisionCompanies(dv);
                    if (PickSquad(pt, 1) is Squad foe) { OrderArrows(foe.Centroid()); Charge(foe, Cue.Order); }
                    else
                    {
                        // March there, and charge whoever comes near on the way: the move plus the standing rule for it.
                        OrderArrows(pt); LineAt(pt, battle.Centroid(1));
                        foreach (var s in Targets().Where(s => Battle.RulesFor(s).Contains(StandingRule.ChargeWhenClose)))
                            Send(s, q => { q.Rule = StandingRule.ChargeWhenClose; q.RuleFired = false; });
                        battle.Log((selected.Count == 0 ? "All troops: " : "Squads: ") + "march there, and charge what comes near");
                    }
                    return;
                }
            }
        }
        if (ms.RightButton == ButtonState.Pressed && pms.RightButton == ButtonState.Released && hud.MinimapAt(ms.Position) is Vector2 rmw)
        {
            if (selectedDivision is { } dv && !deploying) OrderDivisionCompanies(dv);
            OrderArrows(rmw); LineAt(rmw, battle.Centroid(1));
            return;
        }
        if (LmbPressed)
        {
            if (hud.PauseAt(ms.Position)) { paused = !paused; return; }
            if (hud.SpeedAt(ms.Position) is int si) { speed = si; paused = false; return; }
            if (hud.ModeAt(ms.Position) && battle.Player.Active && !deploying) { SetCommand(false); return; }
            if (hud.CaptainAt(ms.Position))
            {
                // The captain's picture: look at him; twice quickly, step into him.
                if (realTime - lastCaptainClick < DoubleClick && battle.Player.Active && !deploying) { SetCommand(false); return; }
                lastCaptainClick = realTime;
                camFocus = battle.Player.Pos;
                return;
            }
            if (hud.GoalCloseAt(ms.Position)) selectedDivision = null;
            else if (selectedDivision != null && hud.GoalAt(ms.Position) is Goal gg) GiveGoal(selectedDivision, gg);
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
                if (deploying) { dragCard = card; dragMoved = false; }
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
        if (dragCard != null && ms.LeftButton == ButtonState.Pressed && hud.CardAt(ms.Position) != dragCard) dragMoved = true;
        if (LmbReleased && dragCard is Squad dragged)
        {
            dragCard = null;
            // Dropped on a post zone or an officer's card: it (and any companies picked with it) go under him.
            if (dragMoved && (hud.DivisionAt(ms.Position) ?? (hud.Over(ms.Position) ? null : hud.ZoneAt(mouseGround))) is Division to)
            {
                if (!selected.Contains(dragged)) { selected.Clear(); selected.Add(dragged); }
                MoveTo(to);
                return;
            }
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
                else if (sq == lastFieldSquad && realTime - lastFieldClick < DoubleClick)
                {
                    selected.Clear();
                    var kind = sq.Units.FirstOrDefault(u => u.Active)?.Type.Id;
                    foreach (var s in Selectable)
                        if (s == sq || (kind != null && s.Units.Any(u => u.Active && u.Type.Id == kind))) selected.Add(s);
                }
                else { selected.Clear(); selected.Add(sq); }
                if (sq != null) { lastFieldSquad = sq; lastFieldClick = realTime; }
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
            if (selectedDivision is { } dv && !deploying) OrderDivisionCompanies(dv); // an officer takes posts and stances; RMB orders his companies
            if (!deploying && SelectedCrews().Count > 0)
            {
                EngineOrder(mouseGround, click);
                if (!selected.Any(s => s.Works == null)) return;
            }
            if (click && PickSquad(mouseGround, 1) is Squad foe)
            {
                OrderArrows(foe.Centroid());
                Charge(foe, Cue.Order); // click an enemy: attack it (Shift: a reckless charge)
                return;
            }
            OrderArrows(click ? mouseGround : (a + mouseGround) / 2);
            if (click) LineAt(mouseGround, enemy);    // click: move, keep width
            else Distribute(a, mouseGround, enemy);   // drag: draw the line
            battle.Log((selected.Count == 0 ? "All troops: " : "Squads: ") + (queueing ? "then move there" : "move"));
        }
    }

    /// The cursor for what a click would do here: attack a company, select yours, sight an engine or a spell, send the
    /// companies to the ground, drag a box, turn the camera (the kinds are in Cursors.cs).
    CursorKind CommandCursor()
    {
        var p = ms.Position;
        if (wheelOpen) return CursorKind.Pointer;
        if (ms.MiddleButton == ButtonState.Pressed) return CursorKind.Turn;
        if (boxStart is Point bs && (Math.Abs(p.X - bs.X) > 6 || Math.Abs(p.Y - bs.Y) > 6)) return CursorKind.Box;
        if (hud.Over(p))
            return hud.CardAt(p) != null || hud.ButtonAt(p) != null || hud.PauseAt(p) || hud.SpeedAt(p) != null || hud.ModeAt(p) || hud.CaptainAt(p) || hud.AlertAt(p) != null || hud.DivisionAt(p) != null || hud.GoalAt(p) != null || hud.MinimapAt(p) != null
                ? CursorKind.Hand : CursorKind.Pointer;
        if (castBy != null) return CursorKind.Cast;
        if (attackMoveMode) return CursorKind.Attack;
        if (!deploying && SelectedCrews().Count > 0) return CursorKind.Aim;
        bool canOrder = selectedDivision != null || Targets().Count > 0;
        if (canOrder && !deploying && PickSquad(mouseGround, 1) != null) return CursorKind.Attack;
        if (PickSquad(mouseGround, 0) is { AiControlled: false }) return CursorKind.Select;
        return canOrder ? CursorKind.Move : CursorKind.Pointer;
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
    Vector2 GroundUnderMouse() => GroundUnder(ms.X, ms.Y);

    Vector2 GroundUnder(int x, int y)
    {
        var vp = GraphicsDevice.Viewport;
        var near = vp.Unproject(new Vector3(x, y, 0), proj, view, Matrix.Identity);
        var far = vp.Unproject(new Vector3(x, y, 1), proj, view, Matrix.Identity);
        if (!float.IsFinite(near.X) || !float.IsFinite(near.Y) || !float.IsFinite(near.Z)
            || !float.IsFinite(far.X) || !float.IsFinite(far.Y) || !float.IsFinite(far.Z)
            || (far - near).LengthSquared() < 1e-8f)
            return camFocus;
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
            return float.IsFinite(hit.X) && float.IsFinite(hit.Z) ? new Vector2(hit.X, hit.Z) : camFocus;
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
