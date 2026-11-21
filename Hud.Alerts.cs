using Microsoft.Xna.Framework;

namespace Ironfield;

/// The alerts strip under the event feed (docs/archive/COMBAT_QOL.md Q5): what needs you, newest first; in command view a click
/// jumps the camera there and selects the company.
partial class Hud
{
    readonly List<(Rectangle R, Alert A)> alertRows = new();

    public Alert? AlertAt(Point p) => Command ? alertRows.FirstOrDefault(a => a.R.Contains(p)).A : null;

    void AlertStrip(int most = 4)
    {
        alertRows.Clear();
        if (ShowResult || Deploying) return;
        int y = U(10) + (Command ? U(118) : 0), room = (W - U(604)) / 2 - U(24); // in the command view the cards hold the top
        foreach (var a in B.RecentAlerts(10, most))
        {
            float alpha = Math.Clamp((10 - (B.Time - a.Time)) / 2f, 0, 1);
            var (c, icon) = a.Kind switch
            {
                "breaking" or "worn" => (Bad, "flag"), "horse" => (Foe, "horse"), "arrows" => (Warn, "arrow"), "rally" => (Ok, "horse"),
                "tired" => (Warn, "heart"), "leaderless" => (Bad, "crown"), "pressed" => (Bad, "shield"), _ => (Warn, "shield"),
            };
            var text = kit.Fit("! " + a.Text.ToUpperInvariant(), room);
            var r = new Rectangle(U(10), y, Math.Min(room, TW(text, 1) + U(36)), U(18));
            bool hover = Command && r.Contains(Mouse);
            kit.Toast(U(10), y, text, hover ? Gold : c, icon, alpha);
            alertRows.Add((r, a));
            y += U(21);
        }
        SpokenLines(y, room);
    }

    /// What your named men have said in the last few seconds (C6): his face, his name, his words; newest first, two at most.
    void SpokenLines(int y, int room)
    {
        foreach (var said in B.Spoken.AsEnumerable().Reverse().Where(z => B.Time - z.Time < 6).Take(2))
        {
            float alpha = Math.Clamp((6 - (B.Time - said.Time)) / 1.5f, 0, 1);
            var spec = said.Squad?.Company?.Named?.FirstOrDefault(m => m.Id == said.Soldier);
            var unit = said.Squad?.Units.FirstOrDefault(u => u.SoldierId == said.Soldier);
            int fw = U(14), fh = U(18);
            var lines = kit.Wrap($"{said.Name.ToUpperInvariant()}: \"{said.Text.ToUpperInvariant()}\"", room - fw - U(16)).Take(2).ToList();
            int tw = lines.Max(l => TW(l, 1));
            kit.HGrad(new Rectangle(U(10), y, tw + fw + U(40), fh + U(2)), Color.Black * (0.55f * alpha), Color.Black * 0f);
            if (spec != null && unit != null)
            {
                var face = PortraitPainter.OfMan(spec.Id, unit.Type, spec.Wounds, spec.Age > 0 ? spec.Age : 25, false, 0, 1);
                Rect(new Rectangle(U(10), y, fw + 2, fh + 2), Gold * (0.8f * alpha));
                sb.Draw(Portraits.Sized(sb.GraphicsDevice, face, fw, fh), new Rectangle(U(10) + 1, y + 1, fw, fh), Color.White * alpha);
            }
            for (int i = 0; i < lines.Count; i++) Text(lines[i], U(10) + fw + U(8), y + (lines.Count == 1 ? U(7) : U(2) + i * U(10)), Color.Lerp(Gold, Color.White, 0.5f) * alpha, 1);
            y += fh + U(6);
        }
    }
}
