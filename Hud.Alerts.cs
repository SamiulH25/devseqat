using Microsoft.Xna.Framework;

namespace Ironfield;

/// The alerts strip under the event feed (docs/archive/COMBAT_QOL.md Q5): what needs you, newest first; in command view a click
/// jumps the camera there and selects the company.
partial class Hud
{
    readonly List<(Rectangle R, Alert A)> alertRows = new();

    public Alert? AlertAt(Point p) => Command ? alertRows.FirstOrDefault(a => a.R.Contains(p)).A : null;

    void AlertStrip()
    {
        alertRows.Clear();
        if (ShowResult || Deploying) return;
        int y = U(10) + 6 * U(21) + U(8), room = (W - U(472)) / 2 - U(40);
        foreach (var a in B.RecentAlerts())
        {
            float alpha = Math.Clamp((10 - (B.Time - a.Time)) / 2f, 0, 1);
            var (c, icon) = a.Kind switch
            {
                "breaking" or "worn" => (Bad, "flag"), "horse" => (Foe, "horse"), "arrows" => (Warn, "arrow"), "rally" => (Ok, "horse"),
                "tired" => (Warn, "heart"), "leaderless" => (Bad, "crown"), _ => (Warn, "shield"),
            };
            var text = kit.Fit("! " + a.Text.ToUpperInvariant(), room);
            var r = new Rectangle(U(10), y, Math.Min(room, TW(text, 1) + U(36)), U(18));
            bool hover = Command && r.Contains(Mouse);
            kit.Toast(U(10), y, text, hover ? Gold : c, icon, alpha);
            alertRows.Add((r, a));
            y += U(21);
        }
    }
}
