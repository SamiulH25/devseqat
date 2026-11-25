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

    void FieldControls(float dt)
    {
        if (ctl.Pressed(Act.CameraView))
        {
            camMode = (camMode + 1) % 3;
            battle.Log(camMode switch { 1 => "Camera: Close Duel", 2 => "Camera: First Person", _ => "Camera: Over-the-Shoulder" });
        }

        // Soft lock-on (CR1): tap MMB (when shared with OrderWheel) toggles lock-on; hold MMB > 0.18s opens OrderWheel.
        bool wasWheel = wheelOpen, wasWheelDown = wheelDown, lockTap = false;
        bool shareLock = ctl.Binds.TryGetValue(Act.LockTarget, out var lb) && ctl.Binds.TryGetValue(Act.OrderWheel, out var wb) && lb.Any(wb.Contains);
        if (shareLock)
        {
            wheelDown = ctl.Down(Act.OrderWheel) && !capWheel;
            if (wheelDown && !wasWheelDown) wheelSince = Ironfield.Ui.Now;
            // Non-shared key (e.g. Z) opens OrderWheel immediately; shared MMB waits 0.18s so a quick tap toggles lock-on.
            bool lockKeyDown = ctl.Down(Act.LockTarget);
            wheelOpen = shotWheel || (wheelDown && (!lockKeyDown || Ironfield.Ui.Now - wheelSince > 0.18f));
            if (wasWheelDown && !wheelDown && !wasWheel && Ironfield.Ui.Now - wheelSince <= 0.22f) lockTap = true;
        }
        else
        {
            wheelOpen = ctl.Down(Act.OrderWheel) || shotWheel;
            lockTap = ctl.Pressed(Act.LockTarget);
        }
        if (wheelOpen && !wasWheel) wheelVec = Vector2.Zero;
        if (lockTap) ToggleLockTarget();

        // Validate active soft lock-on target.
        if (lockTarget != null && (!lockTarget.Active || lockTarget.Routing || Vector2.DistanceSquared(lockTarget.Pos, battle.Player.Pos) > 24f * 24f))
            lockTarget = null;

        // The captain's ring: hold its key a moment and it opens; a tap is the couch (when it shares the couch key).
        bool wasCap = capWheel, wasCapDown = capDown, couchTap = false;
        capDown = ctl.Down(Act.CaptainWheel) && !wheelOpen;
        if (capDown && !wasCapDown) capSince = Ironfield.Ui.Now;
        capWheel = shotCapWheel || capDown && Ironfield.Ui.Now - capSince > 0.18f;
        if (capWheel && !wasCap) wheelVec = Vector2.Zero;
        if (wasCapDown && !capDown && !wasCap && ctl.First(Act.CaptainWheel) == ctl.First(Act.Couch)) couchTap = true;

        bool attackDown = IsActive && !wheelOpen && !capWheel && ctl.Down(Act.Attack);
        bool blockDown = IsActive && !wheelOpen && !capWheel && ctl.Down(Act.Block);
        // Lock-on-press (CR1): while holding attack or block in mouse-flick mode, camera panning does not flip your direction.
        bool dirLocked = !Settings.Current.MoveDirControl && lockTarget == null && (attackDown || blockDown);

        if (IsActive)
        {
            // shortcut: mouse look by warping to the centre; switch to SDL relative mode if a platform refuses warps.
            var c = new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            var d = (ms.Position - c).ToVector2();
            if (!skipMouse)
            {
                if (wheelOpen || capWheel)
                {
                    wheelVec += d;
                    if (wheelVec.Length() > 120) wheelVec = Vector2.Normalize(wheelVec) * 120;
                }
                else if (lockTarget != null)
                {
                    // Locked on: mouse movement is dedicated cleanly to 4-way attack/guard direction without fighting camera yaw.
                    if (!Settings.Current.MoveDirControl)
                    {
                        aimAcc *= MathF.Exp(-12f * dt);
                        if (d.LengthSquared() >= 4f) aimAcc += d;
                        if (aimAcc.LengthSquared() > 36f)
                        {
                            aimDir = MathF.Abs(aimAcc.X) > MathF.Abs(aimAcc.Y)
                                ? (aimAcc.X > 0 ? AttackDir.Right : AttackDir.Left)
                                : (aimAcc.Y < 0 ? AttackDir.Overhead : AttackDir.Thrust);
                            aimAcc = Vector2.Zero;
                        }
                    }
                }
                else
                {
                    float k = 0.0035f * ctl.Sensitivity;
                    yaw += d.X * k;
                    pitch = Math.Clamp(pitch + d.Y * k * (ctl.InvertY ? -1 : 1), -0.35f, 1.1f);
                    // Decoupled mouse-flick direction (CR1): deadzone + idle decay + lock-on-press.
                    if (!Settings.Current.MoveDirControl)
                    {
                        aimAcc *= MathF.Exp(-10f * dt);
                        if (!dirLocked && d.LengthSquared() >= 4.8f)
                        {
                            aimAcc += d;
                            if (aimAcc.LengthSquared() > 64f)
                            {
                                aimDir = MathF.Abs(aimAcc.X) > MathF.Abs(aimAcc.Y)
                                    ? (aimAcc.X > 0 ? AttackDir.Right : AttackDir.Left)
                                    : (aimAcc.Y < 0 ? AttackDir.Overhead : AttackDir.Thrust);
                                aimAcc = Vector2.Zero;
                            }
                        }
                    }
                }
            }
            skipMouse = false;
            Mouse.SetPosition(c.X, c.Y);
        }
        else skipMouse = true;

        // Smoothly frame locked-on target.
        if (lockTarget != null)
        {
            var to = lockTarget.Pos - battle.Player.Pos;
            if (to.LengthSquared() > 1e-4f)
            {
                float targetYaw = MathF.Atan2(to.Y, to.X);
                float alpha = 1f - MathF.Exp(-12f * dt);
                yaw += MathHelper.WrapAngle(targetYaw - yaw) * alpha;
                pitch = MathHelper.Lerp(pitch, 0.22f, 1f - MathF.Exp(-8f * dt));
            }
        }

        if (wasCap && !capWheel && CapSector(wheelVec) is int cs) capPending = Hud.CaptainRing[cs];
        if (wasWheel && !wheelOpen && WheelSector() is int sector)
        {
            if (selectedDivision is { } dv) GiveGoal(dv, Hud.WheelGoals(dv)[sector]);
            else Command(Hud.WheelOrder[sector]);
        }

        int wheelDelta = ms.ScrollWheelValue - pms.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            if (lockTarget != null) CycleLockTarget(wheelDelta > 0 ? 1 : -1);
            else fieldDist = Math.Clamp(fieldDist - wheelDelta * 0.0035f, 2.4f, 10f);
        }

        bool mounted = battle.Player.Mounted;
        if (mounted) yaw += battle.Reined; // turning the horse on the reins turns the view with it (the horse follows the view: BattlePlayer.cs)
        battle.Reined = 0;
        var f = Forward(yaw);
        float fwdAxis = ctl.Axis(Act.Forward, Act.Back), rightAxis = ctl.Axis(Act.Right, Act.Left);
        var move = mounted ? Vector2.Zero : f * fwdAxis + RightOf(f) * rightAxis;
        if (move != Vector2.Zero) move.Normalize();
        if (Settings.Current.MoveDirControl && !mounted && (fwdAxis != 0 || rightAxis != 0))
        {
            aimDir = MathF.Abs(rightAxis) > MathF.Abs(fwdAxis)
                ? (rightAxis > 0 ? AttackDir.Right : AttackDir.Left)
                : (fwdAxis > 0 ? AttackDir.Overhead : AttackDir.Thrust);
        }

        battle.Input = new PlayerInput
        {
            Move = move,
            Throttle = mounted ? fwdAxis : 0, // on horseback forward/back is the rein, left/right turns the horse
            Steer = mounted ? rightAxis : 0,
            Dir = aimDir,
            Couch = couchTap || ctl.Pressed(Act.Couch) && ctl.First(Act.Couch) != ctl.First(Act.CaptainWheel) || capPending == Hud.CapCmd.Couch,
            Mount = ctl.Pressed(Act.Interact) || capPending == Hud.CapCmd.Mount,
            Facing = yaw, // the player looks (and strikes) where the camera looks
            EngineAim = battle.Manned != null ? GroundUnder(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2) : null, // an engine you work looses where the crosshair is
            Sprint = ctl.Down(Act.Sprint),
            Attack = attackDown,
            Block = blockDown,
            Dodge = IsActive && !wheelOpen && !capWheel && !mounted && ctl.Pressed(Act.Dodge),
            Feint = IsActive && !wheelOpen && !capWheel && couchTap,
            Rally = ctl.Pressed(Act.Rally),
            Bash = ctl.Pressed(Act.Bash),
            Swap = ctl.Pressed(Act.SwapWeapon) || capPending == Hud.CapCmd.Swap,
            Aura = IsActive && !wheelOpen && !capWheel && ctl.Down(Act.AuraBlade),
            Form1 = ctl.Pressed(Act.Form1) || capPending == Hud.CapCmd.Form1, Form2 = ctl.Pressed(Act.Form2) || capPending == Hud.CapCmd.Form2,
            Form3 = ctl.Pressed(Act.Form3) || capPending == Hud.CapCmd.Form3, Great = ctl.Pressed(Act.GreatForm) || capPending == Hud.CapCmd.Great,
            Relic = ctl.Pressed(Act.Relic) || capPending == Hud.CapCmd.Relic,
        };
        capPending = null;
    }

    void ToggleLockTarget()
    {
        if (lockTarget != null) { lockTarget = null; return; }
        lockTarget = PickLockTarget();
    }

    void ToggleOrCycleLock()
    {
        if (lockTarget == null) { lockTarget = PickLockTarget(); return; }
        var prev = lockTarget;
        CycleLockTarget(1);
        if (lockTarget == prev) lockTarget = null;
    }

    Unit? PickLockTarget()
    {
        var p = battle.Player;
        var fwd = Forward(yaw);
        Unit? best = null;
        float bestScore = float.MaxValue;
        foreach (var u in battle.Units)
        {
            if (!u.Active || u.Team == p.Team || u.Routing) continue;
            var to = u.Pos - p.Pos;
            float d = to.Length();
            if (d > 18f || d < 0.01f) continue;
            float dot = Vector2.Dot(to / d, fwd);
            if (dot < -0.25f) continue;
            float score = d * (1.4f - 0.6f * dot);
            if (score < bestScore) { bestScore = score; best = u; }
        }
        return best;
    }

    void CycleLockTarget(int dir)
    {
        var p = battle.Player;
        var foes = new List<(Unit u, float ang)>();
        foreach (var u in battle.Units)
        {
            if (!u.Active || u.Team == p.Team || u.Routing) continue;
            var to = u.Pos - p.Pos;
            if (to.LengthSquared() > 20f * 20f || to.LengthSquared() < 1e-4f) continue;
            foes.Add((u, MathHelper.WrapAngle(MathF.Atan2(to.Y, to.X) - yaw)));
        }
        if (foes.Count <= 1) return;
        foes.Sort((a, b) => a.ang.CompareTo(b.ang));
        int idx = foes.FindIndex(f => f.u == lockTarget);
        if (idx < 0) { lockTarget = foes[0].u; return; }
        lockTarget = foes[(idx + (dir >= 0 ? 1 : -1) + foes.Count) % foes.Count].u;
    }

    int? WheelSector() => wheelOpen ? Sector(wheelVec, Hud.WheelOrder.Length) : capWheel ? CapSector(wheelVec) : null;
    static int? CapSector(Vector2 v) => Sector(v, Hud.CaptainRing.Length);
    static int? Sector(Vector2 v, int n)
    {
        if (v.Length() < 30) return null;
        float a = MathF.Atan2(v.Y, v.X) + MathHelper.PiOver2; // 0 = up, clockwise
        return ((int)MathF.Round(a / MathHelper.TwoPi * n) % n + n) % n;
    }

    void CommandControls(float dt)
    {
        wheelOpen = shotWheel; // the ring is the fight view's; here the deck and the right button give orders
        capWheel = capDown = false;
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
        // The turn buttons under the minimap turn the view while held.
        if (ms.LeftButton == ButtonState.Pressed && hud.MapButtonAt(ms.Position) is 0 or 2 && hud.MapButtonAt(ms.Position) is int turn)
        {
            camYaw += (turn == 0 ? -1 : 1) * 1.6f * dt;
            return;
        }
        if (LmbPressed)
        {
            if (hud.MapButtonAt(ms.Position) == 1) { groundView = !(groundView ?? deploying); return; }
            if (hud.DeckTitleAt(ms.Position)) { selected.Clear(); selectedDivision = null; return; }
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
        return GroundPick.March(near, far, battle.Terrain, 800, 600) ?? camFocus; // bisects the crossing
    }

    void UpdateCamera()
    {
        // Both views are an orbit (focus, yaw, pitch, distance); the transition eases every parameter.
        var t = battle.Terrain;
        var p = battle.Player.Pos;
        bool mounted = battle.Player.Mounted;
        float dt = MathF.Min(MathF.Max(frameDt, 1f / 120f), 0.05f);

        // Critically damped focus height (CR1): eliminates terrain-ripple jitter while keeping 1:1 mouse-look responsiveness.
        float targetY = t.Height(p) + (camMode == 2 ? (mounted ? 2.55f : 1.68f) : (mounted ? 2.5f : 1.65f));
        float targetShoulder = camMode == 2 ? 0f : camMode == 1 ? 0.48f : 0.68f;
        if (!camInit || shotDir != null)
        {
            camFocusY = targetY;
            camFocusYVel = 0;
            camShoulder = targetShoulder;
            camFov = 62f;
            camInit = true;
        }
        else
        {
            const float w = 14f;
            float acc = w * w * (targetY - camFocusY) - 2f * w * camFocusYVel;
            camFocusYVel += acc * dt;
            camFocusY += camFocusYVel * dt;
            camShoulder = MathHelper.Lerp(camShoulder, targetShoulder, 1f - MathF.Exp(-10f * dt));
        }

        var shoulder = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw)) * camShoulder; // over the right shoulder
        var fieldFocus = new Vector3(p.X, camFocusY, p.Y) + shoulder;
        var cmdFocus = new Vector3(camFocus.X, t.Height(camFocus), camFocus.Y);
        float k = MathHelper.SmoothStep(0, 1, blend);
        var focus = Vector3.Lerp(fieldFocus, cmdFocus, k);
        float y = yaw + MathHelper.WrapAngle(camYaw - yaw) * k; // shortest way round
        float pt = MathHelper.Lerp(pitch, camPitch, k);
        float baseDist = camMode == 2 ? 0.05f : camMode == 1 ? 2.7f : fieldDist;
        float dist = MathHelper.Lerp(baseDist + (mounted && camMode != 2 ? 1.4f : 0), zoom, k);
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
        var shake = Shake() + UpdateCamKick();
        var rawEye = focus - look * dist;
        if (k < 0.5f && dist > 0.25f) rawEye = PullCameraIn(focus, rawEye);
        eye = rawEye + shake;
        focus += shake * 0.6f;
        focusPoint = focus;
        eye.Y = MathF.Max(eye.Y, t.Height(new Vector2(eye.X, eye.Z)) + 0.45f); // never under the ground

        // Dynamic FOV (CR1): +5 deg on sprint/gallop, -4 deg on charged swings.
        bool sprinting = battle.Player.Active && (mounted ? MathF.Abs(battle.Player.Speed) > 7f : battle.Input.Sprint && battle.Input.Move != Vector2.Zero);
        float targetFov = 62f + (sprinting ? 5f : 0f) - (battle.Player.Charging ? 4f * battle.Player.Charge : 0f);
        camFov = MathHelper.Lerp(camFov, targetFov, 1f - MathF.Exp(-8f * dt));
        float fov = pressFov is float pf && k > 0.5f ? pf : MathHelper.Lerp(camFov, 45f, k);

        proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(fov), GraphicsDevice.Viewport.AspectRatio, 0.15f, 700);
        view = Matrix.CreateLookAt(eye, focus, Vector3.Up);
        hud.CamYaw = y;
        hud.CamFocus = camFocus;
    }

    /// Wall & building pull-in (CR1): clip segment from focus to eye against Fortress walls, towers and buildings.
    Vector3 PullCameraIn(Vector3 focus, Vector3 desiredEye)
    {
        var wall = battle.Wall;
        if (wall == null) return desiredEye;
        var a = new Vector2(focus.X, focus.Z);
        var b = new Vector2(desiredEye.X, desiredEye.Z);
        var d = b - a;
        if (d.LengthSquared() < 0.04f) return desiredEye;
        float bestT = 1f;

        // Fortress walls (skip breached segments and open gates).
        foreach (var seg in wall.Segments)
        {
            if (seg.Breached) continue;
            if (seg.IsGate && seg.GateIndex >= 0 && seg.GateIndex < wall.Gates.Count && wall.Gates[seg.GateIndex].Open) continue;
            float hitT = SegSegT(a, b, seg.A, seg.B);
            if (hitT < bestT)
            {
                float yAtHit = MathHelper.Lerp(focus.Y, desiredEye.Y, hitT);
                float groundAtHit = battle.Terrain.Height(Vector2.Lerp(a, b, hitT));
                if (yAtHit < groundAtHit + seg.Height + 0.4f) bestT = hitT;
            }
        }
        // Fortress towers.
        foreach (var tw in wall.Towers)
        {
            float hitT = SegCircleT(a, b, tw.Pos, tw.Radius + 0.35f);
            if (hitT < bestT) bestT = hitT;
        }
        // Interior buildings / village houses.
        foreach (var bld in wall.Buildings)
        {
            float hitT = SegObbT(a, b, bld.Pos, bld.Along, bld.Across, bld.W * 0.5f + 0.35f, bld.D * 0.5f + 0.35f);
            if (hitT < bestT) bestT = hitT;
        }
        if (bestT >= 0.999f) return desiredEye;
        return Vector3.Lerp(focus, desiredEye, Math.Clamp(bestT * 0.9f, 0.15f, 1f));
    }

    static float SegSegT(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
    {
        var r = p2 - p;
        var s = q2 - q;
        float denom = r.X * s.Y - r.Y * s.X;
        if (MathF.Abs(denom) < 1e-6f) return 1f;
        var qp = q - p;
        float t = (qp.X * s.Y - qp.Y * s.X) / denom;
        float u = (qp.X * r.Y - qp.Y * r.X) / denom;
        return t is > 0.02f and < 1f && u is >= 0f and <= 1f ? t : 1f;
    }

    static float SegCircleT(Vector2 a, Vector2 b, Vector2 c, float r)
    {
        var d = b - a;
        var f = a - c;
        float qa = Vector2.Dot(d, d);
        if (qa < 1e-6f) return 1f;
        float qb = 2f * Vector2.Dot(f, d);
        float qc = Vector2.Dot(f, f) - r * r;
        float disc = qb * qb - 4f * qa * qc;
        if (disc < 0) return 1f;
        float t = (-qb - MathF.Sqrt(disc)) / (2f * qa);
        return t is > 0.02f and < 1f ? t : 1f;
    }

    static float SegObbT(Vector2 a, Vector2 b, Vector2 c, Vector2 along, Vector2 across, float hx, float hy)
    {
        var da = a - c;
        var db = b - c;
        float ax = Vector2.Dot(da, along), ay = Vector2.Dot(da, across);
        float bx = Vector2.Dot(db, along), by = Vector2.Dot(db, across);
        float dx = bx - ax, dy = by - ay;
        float tmin = 0f, tmax = 1f;
        if (MathF.Abs(dx) < 1e-6f) { if (MathF.Abs(ax) > hx) return 1f; }
        else
        {
            float t1 = (-hx - ax) / dx, t2 = (hx - ax) / dx;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tmin = MathF.Max(tmin, t1); tmax = MathF.Min(tmax, t2);
            if (tmin > tmax) return 1f;
        }
        if (MathF.Abs(dy) < 1e-6f) { if (MathF.Abs(ay) > hy) return 1f; }
        else
        {
            float t1 = (-hy - ay) / dy, t2 = (hy - ay) / dy;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tmin = MathF.Max(tmin, t1); tmax = MathF.Min(tmax, t2);
            if (tmin > tmax) return 1f;
        }
        return tmin is > 0.02f and < 1f ? tmin : 1f;
    }
}
