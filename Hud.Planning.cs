using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle planning (docs/battle-overhaul/PILLAR_2_BATTLE_PLANNING.md P2.2): the BATTLE PLANNING header with START BATTLE and
/// the last-layout pill at the top, and a strip of the planning keys along the bottom. The roster, the chosen company's card
/// and the order deck are the command view's own (Hud.Tactical.cs, Hud.Deck.cs).
sealed partial class Hud
{
    Rectangle planHeader;

    /// Where the planning pieces stand; the goals list starts under the header.
    void LayoutPlanning()
    {
        planHeader = start = deployToggle = Rectangle.Empty;
        if (!Deploying) return;
        planHeader = new Rectangle(W / 2 - U(150), U(4) + Drop, U(300), U(DeployLast != null ? 76 : 58));
        start = new Rectangle(W / 2 - U(110), planHeader.Y + U(24), U(220), U(26));
        if (DeployLast != null) deployToggle = new Rectangle(W / 2 - U(100), start.Bottom + U(5), U(200), U(14));
    }

    /// Where the goals list starts while you deploy.
    int PlanningBottom => planHeader.Bottom + U(8);

    void DrawPlanning()
    {
        if (!Deploying) return;
        Panel(planHeader, Gold);
        TextC("BATTLE PLANNING", W / 2f, planHeader.Y + U(7), Gold);
        kit.Gilded(start, "START BATTLE", "ENTER");
        if (DeployLast is bool last)
        {
            // One pill, two halves: the layout in use lit, the other one click away.
            bool hover = deployToggle.Contains(Mouse);
            var l = new Rectangle(deployToggle.X, deployToggle.Y, deployToggle.Width / 2, deployToggle.Height);
            var r = new Rectangle(l.Right, deployToggle.Y, deployToggle.Width - l.Width, deployToggle.Height);
            Rect(deployToggle, Color.Black * 0.45f);
            Rect(last ? l : r, Kit.Mul(Gold, 0.45f));
            Frame(deployToggle, hover ? Gold : Kit.GiltDark, U(1));
            TextC("LAST LAYOUT", l.Center.X, l.Y + U(4), last ? Color.White : Dim, 1);
            TextC("DEFAULT", r.Center.X, r.Y + U(4), last ? Dim : Color.White, 1);
        }
        PlanningKeys();
    }

    /// The planning keys, bottom centre between the captain's corner and the order deck; the less needed drop off first
    /// when the window is narrow.
    void PlanningKeys()
    {
        var keys = new List<(string Key, string What)>
        {
            ("LMB", "SELECT / DRAG"),
            ("RMB", "DRAG PLACE & FACE"),
            ($"{Ctl.First(Act.RotateLeft)}/{Ctl.First(Act.RotateRight)}", "ROTATE"),
            ("ENTER", "START"),
            ($"{Ctl.First(Act.Forward)}{Ctl.First(Act.Left)}{Ctl.First(Act.Back)}{Ctl.First(Act.Right)}", "PAN"),
            ("WHEEL", "ZOOM"),
        };
        int left = selectedCard.IsEmpty ? player.Right : Math.Max(player.Right, selectedCard.Right);
        int right = deck.IsEmpty ? W : deck.X;
        int room = right - left - U(16), gap = U(6);
        int Width((string Key, string What) k) => TW(k.Key, 1) + TW(k.What, 1) + U(16);
        while (keys.Count > 1 && keys.Sum(Width) + gap * (keys.Count - 1) > room) keys.RemoveAt(keys.Count - 1);
        int total = keys.Sum(Width) + gap * (keys.Count - 1);
        int x = (left + right - total) / 2, y = H - U(22);
        foreach (var k in keys)
        {
            int w = Width(k), kw = TW(k.Key, 1) + U(6);
            var r = new Rectangle(x, y, w, U(15));
            Rect(r, new Color(12, 14, 18) * 0.72f);
            Frame(r, Kit.GiltDark * 0.7f, U(1));
            Rect(new Rectangle(r.X + U(1), r.Y + U(1), kw, r.Height - U(2)), Kit.Mul(Gold, 0.3f));
            Text(k.Key, r.X + U(4), r.Y + U(4), Gold, 1);
            Text(k.What, r.X + kw + U(5), r.Y + U(4), Style.Text, 1);
            x += w + gap;
        }
    }
}
