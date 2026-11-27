using Microsoft.Xna.Framework;

namespace Ironfield;

/// The alerts strip under the event feed (docs/archive/COMBAT_QOL.md Q5): what needs you, newest first; in command view a click
/// jumps the camera there and selects the company.
partial class Hud
{
    readonly List<(Rectangle R, Alert A)> alertRows = new();

    public Alert? AlertAt(Point p) => (Command || FieldCommand) ? alertRows.FirstOrDefault(a => a.R.Contains(p)).A : null;

    /// Top-right morale & state event pills (Pillar 1 P1.2): dark-iron pill with role icon + single crisp state word, max 6 stacked, ~5s fade.
    void AlertStrip(int most = 6)
    {
        alertRows.Clear();
        if (ShowResult || Deploying) return;
        int pillW = U(166), pillH = U(20), gap = U(3);
        int x = Command ? minimap.X - pillW - U(10) : W - pillW - U(12);
        int y = Command ? U(12) : U(32);
        var chrome = UiChrome.For(sb.GraphicsDevice);
        int b = Math.Max(3, U(5));
        int drawn = 0;
        foreach (var a in B.RecentAlerts(5.5f, 12))
        {
            if (drawn >= most) break;
            var (word, c, showInFight) = a.Kind switch
            {
                "inspired" => ("INSPIRED", Ok, true),
                "wavering" => ("WAVERING", Warn, true),
                "broken" => ("BROKEN", Orange, true),
                "breaking" => ("ROUTING", Bad, true),
                "obliterated" => ("OBLITERATED", Bad, true),
                "arrows" => ("NO ARROWS", Warn, false),
                "leaderless" => ("LEADERLESS", Bad, false),
                _ => ("", Dim, false),
            };
            if (word == "" || (!Command && !FieldCommand && !showInFight)) continue;
            float alpha = Math.Clamp((5.5f - (B.Time - a.Time)) / 1.2f, 0, 1);
            var r = new Rectangle(x, y, pillW, pillH);
            bool hover = (Command || FieldCommand) && r.Contains(Mouse);
            chrome.Draw9(sb, r, UiChrome.PlateFill, 10, b, Color.White * (0.9f * alpha));
            chrome.Draw9(sb, r, UiChrome.PlateFrame, 10, b, (hover ? Gold : c) * (0.85f * alpha));
            string icon = a.Squad != null ? RoleIconOf(a.Squad) : a.Kind == "leaderless" ? "crown" : "flag";
            kit.Icon(icon, r.X + U(6), r.Y + U(3), c * alpha, 2);
            string who = a.Squad != null ? a.Squad.Name.ToUpperInvariant() : "DIVISION";
            int ww = TW(word, 1);
            Text(kit.Fit(who, pillW - ww - U(34)), r.X + U(23), r.Y + U(6), Style.Text * (0.9f * alpha), 1);
            TextR(word, r.Right - U(7), r.Y + U(6), c * alpha, 1);
            alertRows.Add((r, a));
            y += pillH + gap;
            drawn++;
        }
        int barkY = (Command || FieldCommand) ? (cards.Count > 0 ? cards[^1].R.Bottom + U(14) : U(14)) : U(14);
        int barkRoom = Math.Min(U(260), (W - U(520)) / 2);
        if (barkY < H - U(140)) SpokenLines(barkY, barkRoom);
    }

    /// What your named men have said in the last few seconds (C6): his face, his name, his words; newest first, two at most.
    void SpokenLines(int y, int room)
    {
        DrawWorldBarks();
        if (Settings.Current.BattleBarks == 0) return;
        foreach (var said in B.Spoken.AsEnumerable().Reverse().Where(z => B.Time - z.Time < 4.5f && (z.Key || Settings.Current.BattleBarks < 2)).Take(2))
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
