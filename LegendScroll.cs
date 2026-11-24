using Microsoft.Xna.Framework;

namespace Ironfield;

/// Shared Illuminated Legend Scroll renderer (docs/pre-demo/LEGENDS.md LG2). Used both in-campaign (`MapScreen.Legend.cs`)
/// and in the Title Screen Hall of Legends (`LegendScreen.cs`).
static class LegendScroll
{
    static readonly Color InkRed = new(138, 34, 24), InkGreen = new(28, 86, 36), InkFade = new(66, 48, 32);
    static readonly Color SealLiving = new(186, 126, 34), SealGold = new(202, 158, 44), SealFallen = new(152, 34, 30), SealCrowned = new(44, 88, 156);

    public sealed record Data(
        string FullName,
        string Rank,
        string CompanyName,
        string Homeland,
        string Fate,
        int Days,
        int Battles,
        int Kills,
        int Palette,
        int Arms,
        Face? Portrait,
        IReadOnlyList<LegendVerse> Verses,
        IReadOnlyList<VerseDef>? Unwritten,
        LegendGear? Relic
    );

    public static Color SealColor(string fate) => fate switch
    {
        "Fallen" => SealFallen,
        "Crowned" => SealCrowned,
        "Retired" or "Victorious" or "HandedOver" => SealGold,
        _ => SealLiving,
    };

    public static string FateBadge(string fate) => fate switch
    {
        "Fallen" => "FALLEN IN BATTLE",
        "Crowned" => "CROWNED SOVEREIGN",
        "Victorious" => "VICTORIOUS",
        "Retired" => "RETIRED IN HONOUR",
        "HandedOver" => "PASSED THE BANNER",
        _ => "LIVING CHRONICLE",
    };

    /// Draws the illuminated manuscript scroll inside `r` and returns the bottom Y of the drawn content (for footer placement).
    public static int Draw(Ui ui, Rectangle r, Data d, float open, ref int scrollOffset, int wheel)
    {
        int shown = ui.Scroll(r, open);
        int pad = ui.U(24), x = r.X + pad, cw = r.Width - pad * 2, y = r.Y + ui.U(24);

        // Delicate inner manuscript frame
        var innerFrame = new Rectangle(r.X + ui.U(14), r.Y + ui.U(16), r.Width - ui.U(28), r.Height - ui.U(32));
        if (innerFrame.Bottom <= shown) ui.Frame(innerFrame, Kit.Ink * 0.25f, ui.U(1));

        // ---- Header: Portrait | Title & Stats | Heraldry + Wax Seal ----
        int ps = ui.U(54);
        if (y + ps <= shown)
        {
            if (d.Portrait != null)
            {
                var pBox = new Rectangle(x, y, ps, ps);
                ui.Rect(new Rectangle(pBox.X - ui.U(2), pBox.Y - ui.U(2), pBox.Width + ui.U(4), pBox.Height + ui.U(4)), Kit.GiltDark);
                ui.FaceHead(d.Portrait, pBox);
            }
            var colours = Style.Sides[Math.Clamp(d.Palette, 0, Style.Sides.Length - 1)] with { Charge = (Emblem)Math.Clamp(d.Arms, 0, 8) };
            int midY = y + ps / 2;
            var shBox = new Rectangle(x + cw - ui.U(40), midY - ui.U(22), ui.U(36), ui.U(44));
            ui.Shield(shBox, colours);

            // Wax seal beside the shield, aligned on the same header midline
            var sealPos = new Vector2(shBox.X - ui.U(22), midY);
            var sc = SealColor(d.Fate);
            ui.Disc(sealPos + new Vector2(ui.U(1), ui.U(2)), ui.U(15), Color.Black * 0.22f);
            ui.Disc(sealPos, ui.U(15), sc);
            ui.Disc(sealPos, ui.U(11), Kit.Mul(sc, 0.78f));
            ui.Icon(d.Fate == "Fallen" ? "skull" : d.Fate == "Crowned" ? "crown" : "star", (int)sealPos.X - ui.U(4), (int)sealPos.Y - ui.U(4), Kit.Parchment, 1);

            int tx = x + ps + ui.U(12), tw = (int)sealPos.X - ui.U(20) - tx;
            ui.Ink($"THE LEGEND OF  ·  {FateBadge(d.Fate)}", tx, y + ui.U(1), InkFade, 1);
            string name = d.FullName.ToUpperInvariant();
            int ns = ui.TW(name, 2) <= tw ? 2 : 1;
            ui.Ink(Kit.Fit(ui, name, tw, ns), tx, y + ui.U(12), Kit.InkDeep, ns);
            string sub = $"{d.Rank}  ·  {d.CompanyName.ToUpperInvariant()}";
            ui.Ink(Kit.Fit(ui, sub, tw, 1), tx, y + ui.U(ns == 2 ? 30 : 24), InkRed, 1);
            string stats = $"DAY {d.Days}  ·  {d.Battles} BATTLES  ·  {d.Kills} SLAIN  ·  {d.Verses.Count} {(d.Verses.Count == 1 ? "VERSE" : "VERSES")}";
            ui.Ink(Kit.Fit(ui, stats, tw, 1), tx, y + ui.U(ns == 2 ? 42 : 36), InkFade, 1);
        }
        y += ps + ui.U(14);
        if (y <= shown) ui.Divider(x, y, cw, Kit.Ink * 0.5f);
        y += ui.U(10);

        // ---- Reserve bottom space for Unwritten Verses / Relic + Footer buttons ----
        int unwrittenH = d.Unwritten is { Count: > 0 } ? ui.U(20) + Math.Min(3, d.Unwritten.Count) * ui.U(13) : 0;
        int relicH = d.Relic != null ? ui.U(32) : 0;
        int footerH = ui.U(46) + unwrittenH + relicH;
        var body = new Rectangle(x, y, cw, Math.Max(ui.U(80), r.Bottom - y - footerH));

        // ---- Chronicle Verses ----
        if (d.Verses.Count == 0)
        {
            int ey = body.Y + ui.U(18);
            if (ey + ui.U(24) <= shown)
            {
                ui.Ink("THE PARCHMENT WAITS", body.Center.X, ey, InkFade, 2, cx: true);
                ey += ui.U(22);
                foreach (var line in ui.Wrap("Win a notable battle, hunt a great beast, storm a walled hold, or knight a veteran from your roll to ink the first verse of your Legend.", body.Width - ui.U(24), 1))
                {
                    if (ey + ui.U(11) <= shown) ui.InkProse(line, body.Center.X - ui.TW(line, 1) / 2f, ey, InkFade);
                    ey += ui.U(12);
                }
            }
        }
        else
        {
            int rowH = ui.U(40), gap = ui.U(6);
            int maxVis = Math.Max(1, body.Height / (rowH + gap));
            int maxScroll = Math.Max(0, d.Verses.Count - maxVis);
            if (body.Contains(ui.Mouse) && wheel != 0) scrollOffset -= Math.Sign(wheel);
            scrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);

            int vy = body.Y;
            for (int i = scrollOffset; i < Math.Min(d.Verses.Count, scrollOffset + maxVis); i++)
            {
                var v = d.Verses[i];
                var card = new Rectangle(body.X, vy, body.Width, rowH);
                if (card.Bottom > shown) break;
                ui.Rect(card, Kit.Ink * 0.06f);
                ui.Frame(card, Kit.Ink * 0.28f, ui.U(1));
                // Illuminated icon medallion
                var med = new Rectangle(card.X + ui.U(6), card.Y + ui.U(6), ui.U(28), ui.U(28));
                ui.Rect(med, InkRed * 0.85f);
                ui.Frame(med, Kit.GiltDark, ui.U(1));
                ui.Icon(v.Icon, med.X + ui.U(7), med.Y + ui.U(7), Kit.Parchment, 2);

                int vx = med.Right + ui.U(10), vw = card.Right - vx - ui.U(8);
                string meta = $"DAY {v.Day}  ·  {v.Place.ToUpperInvariant()}";
                ui.Ink(v.Title, vx, card.Y + ui.U(6), Kit.InkDeep, 1);
                ui.Ink(Kit.Fit(ui, meta, vw - ui.TW(v.Title, 1) - ui.U(10), 1), card.Right - ui.U(8) - ui.TW(Kit.Fit(ui, meta, vw - ui.TW(v.Title, 1) - ui.U(10), 1), 1), card.Y + ui.U(6), InkFade, 1);
                ui.InkProse(Kit.Fit(ui, v.Line, vw, 1), vx, card.Y + ui.U(20), Kit.InkDeep);
                vy += rowH + gap;
            }
            if (maxScroll > 0 && body.Bottom <= shown)
                ui.Ink($"SCROLL {scrollOffset + 1}-{Math.Min(d.Verses.Count, scrollOffset + maxVis)} OF {d.Verses.Count}", body.Center.X, body.Bottom - ui.U(8), InkFade, 1, cx: true);
        }

        int fy = body.Bottom + ui.U(6);
        if (d.Relic != null && fy + relicH <= shown)
        {
            var rBox = new Rectangle(x, fy, cw, ui.U(28));
            ui.Rect(rBox, Kit.Gilt * 0.14f);
            ui.Frame(rBox, Kit.GiltDark * 0.7f, ui.U(1));
            ui.Icon("sword", rBox.X + ui.U(8), rBox.Y + ui.U(7), InkRed, 2);
            ui.Ink($"HEIRLOOM IN FUTURE WORLDS: {d.Relic.Name.ToUpperInvariant()}", rBox.X + ui.U(28), rBox.Y + ui.U(5), Kit.InkDeep, 1);
            ui.InkProse(Kit.Fit(ui, d.Relic.From, cw - ui.U(36), 1), rBox.X + ui.U(28), rBox.Y + ui.U(16), InkFade);
            fy = rBox.Bottom + ui.U(6);
        }
        if (d.Unwritten is { Count: > 0 } && fy + ui.U(16) <= shown)
        {
            ui.Divider(x, fy, cw, Kit.Ink * 0.35f);
            fy += ui.U(6);
            ui.Ink("UNWRITTEN VERSES (DEEDS THAT CALL TO YOU)", x, fy, InkFade, 1);
            fy += ui.U(12);
            foreach (var u in d.Unwritten.Take(3))
            {
                if (fy + ui.U(11) > shown) break;
                ui.Ink(Kit.Fit(ui, $"·  {u.UnwrittenHint}", cw, 1), x + ui.U(6), fy, InkFade, 1);
                fy += ui.U(12);
            }
        }
        return fy;
    }
}
