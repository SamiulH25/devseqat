using Microsoft.Xna.Framework;

namespace Ironfield;

/// Hud: the full-screen overlays: the menu, the after-action result (with the strength of both sides over the battle) and
/// the controls sheet, all as iron plates with a ribbon title.
sealed partial class Hud
{
    // Each side's strength sampled every second of battle time, for the chart on the result.
    readonly List<(float S0, float S1)> strength = new();
    Battle? sampled;
    float sampledAt = -1;

    /// Record both sides' strength once a second; a new battle (or a replay rewinding) starts the record again.
    void SampleStrength()
    {
        if (sampled != B || B.Time < sampledAt) { strength.Clear(); sampled = B; sampledAt = -1; }
        if (B.Winner >= 0 && strength.Count > 0) return;
        if (B.Time - sampledAt < 1 && strength.Count > 0) return;
        strength.Add((Strength(0), Strength(1)));
        sampledAt = B.Time;
    }

    void Overlay(Rectangle r, string title, Color tc)
    {
        Rect(new Rectangle(0, 0, W, H), Color.Black * 0.5f);
        kit.PlateBody(r, Kit.Gilt);
        if (title != "") kit.Ribbon(r.Center.X, r.Y - U(12), title, Kit.Mul(tc, 0.66f), 2);
    }

    void MenuButtons()
    {
        foreach (var (r, m) in menu)
            kit.Gilded(r, m switch { MenuItem.Resume => "RESUME", MenuItem.Controls => "CONTROLS", MenuItem.NewBattle => "NEW BATTLE", MenuItem.Withdraw => "WITHDRAW",
                MenuItem.Continue => "CONTINUE", MenuItem.Title => "QUIT TO TITLE", MenuItem.Settings => "SETTINGS", _ => "QUIT" }, tone: m is MenuItem.Continue or MenuItem.Resume ? Ok : null);
    }

    void Menu()
    {
        var top = menu.Count > 0 ? menu[0].R.Y : H / 2;
        var r = new Rectangle(W / 2 - U(140), top - U(30), U(280), menu.Count * U(38) + U(44));
        Overlay(r, "IRONFIELD", Gold);
        MenuButtons();
    }

    /// Where the result panel sits (Layout puts its buttons along the bottom).
    Rectangle ResultBox => new(W / 2 - U(400), H / 2 - U(176), U(800), U(352));

    /// The after-action report: the tally side against side, the strength of both over the battle, and how it went.
    void Result()
    {
        bool won = B.Winner == 0;
        var r = ResultBox;
        Rect(new Rectangle(0, 0, W, H), Color.Black * 0.5f);
        kit.PlateBody(r, won ? Kit.Gilt : Bad);
        kit.Ribbon(r.Center.X, r.Y - U(16), won ? "VICTORY" : "DEFEAT", Kit.Mul(won ? Kit.Gilt : Bad, 0.66f), 3);
        int pad = U(16), lw = U(420);
        var tally = Well(new Rectangle(r.X + pad, r.Y + U(28), lw, U(160)), "THE TALLY", Gold);
        Tally(tally);
        var chart = Well(new Rectangle(r.X + pad, r.Y + U(196), lw, r.Height - U(196) - U(54)), "STRENGTH", Friendly);
        StrengthChart(chart);
        var how = Well(new Rectangle(r.X + pad + lw + U(10), r.Y + U(28), r.Width - lw - 2 * pad - U(10), r.Height - U(82)), "HOW IT WENT", Gold);
        int ty = how.Y;
        foreach (var (text, at) in B.History.TakeLast(Math.Max(1, how.Height / U(14))))
        {
            int s = (int)at;
            var line = text.ToUpperInvariant();
            int fit = (how.Width - U(46)) / U(6);
            if (line.Length > fit) line = line[..Math.Max(1, fit - 1)] + ".";
            bool foe = line.StartsWith("ENEMY") || line.StartsWith("THE ENEMY");
            Rect(new Rectangle(how.X + U(1), ty + U(5), U(1), U(10)), Color.White * 0.1f);
            Rect(new Rectangle(how.X, ty + U(2), U(3), U(3)), foe ? Foe : Gold);
            Text($"{s / 60}:{s % 60:00}", how.X + U(8), ty, Dim, 1);
            Text(line, how.X + U(42), ty, foe ? Foe : Color.White, 1);
            ty += U(14);
        }
        MenuButtons();
    }

    /// A sunken box with a small heading; returns the inside.
    Rectangle Well(Rectangle r, string title, Color c)
    {
        Rect(r, Color.Black * 0.22f);
        Frame(r, Color.White * 0.06f, U(1));
        Rect(new Rectangle(r.X, r.Y, U(2), U(14)), c);
        Text(title, r.X + U(8), r.Y + U(4), c, 1);
        return new Rectangle(r.X + U(8), r.Y + U(18), r.Width - U(16), r.Height - U(24));
    }

    /// Both sides' shields and mirrored bars for men, fallen, fled, standing and horses lost.
    void Tally(Rectangle c)
    {
        kit.Shield(new Rectangle(c.X, c.Y, U(24), U(29)), SideArms(0));
        kit.Shield(new Rectangle(c.Right - U(24), c.Y, U(24), U(29)), SideArms(1));
        Text("YOUR ARMY", c.X + U(30), c.Y + U(4), SideArms(0).Ui);
        TextR("THE ENEMY", c.Right - U(30), c.Y + U(4), Foe);
        var rows = new (string Label, Func<int, int> F)[]
        {
            ("MEN", t => B.Units.Count(u => u.Team == t)),
            ("FALLEN", t => B.Units.Count(u => u.Team == t && u.Dead)),
            ("FLED", t => B.Units.Count(u => u.Team == t && (u.Fled || (u.Active && u.Routing)))),
            ("STANDING", t => B.Units.Count(u => u.Team == t && u.Active && !u.Routing)),
            ("HORSES LOST", t => B.DeadHorses.Count(h => h.Team == t)),
        };
        int y = c.Y + U(36), mid = c.Center.X, half = c.Width / 2 - U(44);
        int max = Math.Max(1, Math.Max(rows[0].F(0), rows[0].F(1)));
        foreach (var (label, f) in rows)
        {
            int a = f(0), b = f(1);
            TextC(label, mid, y + U(1), Dim, 1);
            int wa = (int)(half * a / (float)max), wb = (int)(half * b / (float)max);
            Rect(new Rectangle(mid - U(44) - half, y, half, U(8)), Color.Black * 0.4f);
            Rect(new Rectangle(mid + U(44), y, half, U(8)), Color.Black * 0.4f);
            kit.VGrad(new Rectangle(mid - U(44) - wa, y, wa, U(8)), Kit.Mul(SideArms(0).Ui, 1.1f), Kit.Mul(SideArms(0).Ui, 0.6f), 1);
            kit.VGrad(new Rectangle(mid + U(44), y, wb, U(8)), Kit.Mul(Foe, 1.1f), Kit.Mul(Foe, 0.6f), 1);
            Text($"{a}", mid - U(44) - half, y + U(10), Color.White, 1);
            TextR($"{b}", mid + U(44) + half, y + U(10), Color.White, 1);
            y += U(19);
        }
        int time = (int)B.Time;
        TextC($"YOUR KILLS {B.PlayerKills}   BATTLE TIME {time / 60}:{time % 60:00}", mid, c.Bottom - U(7), Gold, 1);
    }

    /// Both sides' strength over the battle as two lines on one scale.
    void StrengthChart(Rectangle c)
    {
        if (strength.Count < 2) { Text("TOO SHORT TO CHART.", c.X, c.Y, Dim, 1); return; }
        float max = MathF.Max(1, strength.Max(s => MathF.Max(s.S0, s.S1)));
        var area = new Rectangle(c.X, c.Y + U(4), c.Width, c.Height - U(16));
        for (int k = 1; k < 4; k++) Rect(new Rectangle(area.X, area.Y + area.Height * k / 4, area.Width, U(1)), Color.White * 0.05f);
        kit.Chart(area, strength.Select(s => s.S1 / max).ToArray(), Foe, false);
        kit.Chart(area, strength.Select(s => s.S0 / max).ToArray(), SideArms(0).Ui);
        int end = (int)sampledAt;
        Text("0:00", area.X, area.Bottom + U(5), Dim * 0.8f, 1);
        TextR($"{end / 60}:{end % 60:00}", area.Right, area.Bottom + U(5), Dim * 0.8f, 1);
    }

    void Help()
    {
        var r = new Rectangle(W / 2 - U(420), H / 2 - U(250), U(840), U(500));
        Overlay(r, "CONTROLS", Gold);
        var cols = new (string Title, (string Label, string Key)[] Rows)[]
        {
            ("IN THE FIELD", new[] { Act.Forward, Act.Back, Act.Left, Act.Right, Act.Sprint, Act.Attack, Act.Block, Act.Bash, Act.SwapWeapon, Act.Couch, Act.Interact, Act.Rally, Act.OrderWheel }
                .Select(a => (Controls.Labels[a], Ctl.Name(a)))
                .Append(("SWING / BLOCK DIRECTION", "MOVE MOUSE"))
                .Append(("FEINT", "BLOCK IN WINDUP"))
                .Append(("RIPOSTE", "SWING JUST AFTER A PARRY")).ToArray()),
            ("COMMAND VIEW", new (string, string)[]
            {
                ("SELECT SQUAD / BOX", "LMB / DRAG"), ("ADD TO SELECTION", Ctl.Name(Act.AddToSelection)), ("MOVE (KEEP WIDTH)", "RMB"),
                ("DRAW FORMATION LINE", "RMB DRAG"), ("ATTACK ENEMY SQUAD", "RMB ON ENEMY"), ("DIVISION: GOAL HERE / ATTACK / SCREEN", "PICK CARD, RMB"), ("PAN", $"{Ctl.First(Act.Forward)}{Ctl.First(Act.Left)}{Ctl.First(Act.Back)}{Ctl.First(Act.Right)} / SCREEN EDGES"),
                ("ROTATE", $"{Ctl.First(Act.RotateLeft)} {Ctl.First(Act.RotateRight)} / MMB DRAG"), ("ZOOM", "WHEEL"), ("JUMP CAMERA", "CLICK MINIMAP"),
                ("FOCUS SQUAD", "DOUBLE-CLICK CARD"),
            }),
            ("ORDERS", new[] { Act.Charge, Act.Follow, Act.FormHere, Act.HoldPosition, Act.Advance, Act.FallBack, Act.Spacing, Act.Formation, Act.FireAtWill, Act.SelectAll, Act.NextDivision,
                    Act.CommandView, Act.Pause, Act.Menu, Act.Help, Act.Graphics }
                .Select(a => (Controls.Labels[a], Ctl.Name(a)))
                .Prepend(("SELECT SQUAD", "1-0")).ToArray()),
        };
        int cw = (r.Width - U(40)) / 3;
        for (int i = 0; i < cols.Length; i++)
        {
            int x = r.X + U(20) + i * cw, y = r.Y + U(56);
            Text(cols[i].Title, x, y, Gold);
            y += U(24);
            foreach (var (label, key) in cols[i].Rows)
            {
                Text(label, x, y, Dim, 1);
                Text(key, x, y + U(9), Color.White, 1);
                y += U(24);
            }
        }
        TextC($"REBIND IN {Controls.FileName.ToUpperInvariant()}  (RUN WITH --WRITE-CONTROLS FOR A TEMPLATE)    {Ctl.Name(Act.Help)} / {Ctl.Name(Act.Menu)} CLOSE",
            W / 2f, r.Bottom - U(20), Dim, 1);
    }
}
