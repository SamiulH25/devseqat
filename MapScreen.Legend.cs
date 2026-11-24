using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the captain's living Illuminated Legend Scroll (LG2) and the Monument Storylet when visiting a past Legend's
/// cairn/pillar on the world map (LG4).
partial class MapScreen
{
    int legendScroll;
    bool sealConfirm;

    void LegendScrollPanel()
    {
        C.SyncLegendVerses();
        int w = Math.Min(ui.U(660), ui.W - ui.U(36));
        int verseH = C.S.Verses.Count == 0 ? ui.U(96) : Math.Min(6, C.S.Verses.Count) * ui.U(46) + ui.U(6);
        int unwrittenH = C.UnwrittenVerses(3).Count > 0 ? ui.U(64) : 0;
        int h = Math.Clamp(ui.U(148) + verseH + unwrittenH, ui.U(290), ui.H - ui.U(24));
        ui.Dim(0.6f);
        var r = new Rectangle((ui.W - w) / 2, (ui.H - h) / 2, w, h);
        var data = new LegendScroll.Data(
            FullName: C.CaptainFullName,
            Rank: C.LegendRank,
            CompanyName: C.Player.Name,
            Homeland: C.Captain.Homeland >= 0 && C.Captain.Homeland < C.FactionNames.Length ? C.FactionNames[C.Captain.Homeland] : "Free Marches",
            Fate: C.S.Retired ? "Retired" : C.S.Won ? "Victorious" : "Living",
            Days: C.Day,
            Battles: C.S.Tally.GetValueOrDefault("battles", C.Player.Companies.Sum(c => c.Battles)),
            Kills: C.Player.Companies.Sum(c => c.Kills),
            Palette: C.PlayerPalette,
            Arms: C.Captain.Arms,
            Portrait: PortraitPainter.Of(C, C.Me),
            Verses: C.S.Verses,
            Unwritten: C.UnwrittenVerses(3),
            Relic: null
        );
        LegendScroll.Draw(ui, r, data, BookOpen("legend-live"), ref legendScroll, ms.ScrollWheelValue - pms.ScrollWheelValue);

        // Footer: Retire & Seal Legend + Close
        string? cannot = C.CannotSealLegend();
        int by = r.Bottom - ui.U(38), bw = ui.U(236), cw2 = ui.U(130), gap = ui.U(14);
        int bx0 = r.Center.X - (bw + gap + cw2) / 2;
        var sealBtn = new Rectangle(bx0, by, bw, ui.U(24));
        string sealLabel = sealConfirm ? "CONFIRM: SEAL AND RETIRE" : cannot == null ? $"RETIRE AND SEAL ({C.S.Verses.Count})" : $"RETIRE AND SEAL ({C.S.Verses.Count}/{Campaign.MinSealVerses})";
        if (ui.Button(sealBtn, sealLabel, cannot == null, "", cannot == null))
        {
            if (!sealConfirm) sealConfirm = true;
            else
            {
                sealConfirm = false;
                note = C.SealAndRetire() ?? "";
                if (note == "") { panel = PanelKind.GameOver; return; }
            }
        }
        if (cannot != null && ui.Hover(sealBtn)) note = cannot;
        if (ui.Button(new Rectangle(bx0 + bw + gap, by, cw2, ui.U(24)), "CLOSE", note: ctl.Name(Act.Menu)))
        { sealConfirm = false; panel = CameFrom(PanelKind.Legend); }
        if (note != "") ui.Ink(note.ToUpperInvariant(), r.Center.X, r.Bottom - ui.U(52), InkRed, 1, cx: true);
    }

    /// Monument Storylet (LG4): visiting a past sealed Legend's permanent monument on the world map.
    void MonumentPanel()
    {
        if (C.ActiveMonument is not var (site, leg)) { panel = PanelKind.None; return; }
        int w = BookWidth(), cw = w - ui.U(48), picH = BookPicH();
        bool hasRelic = leg.Relic != null && !C.S.LegendRelicsClaimed.Contains(leg.Id);
        var topVerse = leg.Verses.OrderByDescending(v => v.Weight).FirstOrDefault();
        string coName = leg.CompanyName.Equals("Your company", StringComparison.OrdinalIgnoreCase) ? "their company" : leg.CompanyName;
        string proseText = C.MonumentOutcome != "" ? C.MonumentOutcome
            : $"Weathered stones mark the deeds of {leg.FullName}, who led {coName} for {leg.Days} {(leg.Days == 1 ? "day" : "days")}. "
            + (topVerse != null ? $"Men still tell how they {char.ToLowerInvariant(topVerse.Line[0])}{topVerse.Line[1..]}" : "Their name lives on in the ballads.");
        var proseLines = ui.Wrap(proseText, cw, 1);
        int choiceCount = C.MonumentOutcome != "" ? 0 : (hasRelic ? 3 : 2);
        int h = ui.U(96) + picH + proseLines.Count * ProseH + (choiceCount > 0 ? choiceCount * ui.U(38) + ui.U(16) : ui.U(46));
        h = Math.Min(h, ui.H - ui.U(16));
        ui.Dim(0.6f);
        var r = new Rectangle((ui.W - w) / 2, (ui.H - h) / 2, w, h);
        int shown = ui.Scroll(r, BookOpen("monument:" + leg.Id + ":" + (C.MonumentOutcome != "")));
        int x = r.X + ui.U(24), y = r.Y + ui.U(26);

        // Vignette with cairn scene + past captain's portrait and heraldry
        var pic = new Rectangle(x, y, cw, picH);
        if (pic.Bottom <= shown)
        {
            ui.Rect(new Rectangle(pic.X - ui.U(2), pic.Y - ui.U(2), pic.Width + ui.U(4), pic.Height + ui.U(4)), Kit.InkDeep);
            ui.Clip(pic, () => EventScene(pic, "monument_cairn"));
            int fs = Math.Min(pic.Height - ui.U(16), ui.U(78));
            if (leg.Portrait != null)
            {
                var fb = new Rectangle(pic.X + ui.U(10), pic.Y + ui.U(8), fs, fs);
                ui.Rect(new Rectangle(fb.X - ui.U(2), fb.Y - ui.U(2), fb.Width + ui.U(4), fb.Height + ui.U(4)), Kit.GiltDark);
                ui.FaceHead(leg.Portrait, fb);
            }
            var colours = Style.Sides[Math.Clamp(leg.Palette, 0, Style.Sides.Length - 1)] with { Charge = (Emblem)Math.Clamp(leg.Arms, 0, 8) };
            ui.Shield(new Rectangle(pic.Right - ui.U(46), pic.Center.Y - ui.U(22), ui.U(36), ui.U(44)), colours);
        }
        y = pic.Bottom + ui.U(10);
        string title = site.Name.ToUpperInvariant();
        if (y + ui.U(16) <= shown) ui.Ink(title, r.Center.X, y, Kit.InkDeep, ui.TW(title, 2) <= cw ? 2 : 1, cx: true);
        y += ui.U(24);
        string sub = $"{leg.FullName.ToUpperInvariant()}  ·  {LegendScroll.FateBadge(leg.Fate)}  ·  {leg.Days} {(leg.Days == 1 ? "DAY" : "DAYS")}";
        if (y + ui.U(12) <= shown) ui.Ink(Kit.Fit(ui, sub, cw, 1), r.Center.X, y, InkFade, 1, cx: true);
        y += ui.U(16);
        foreach (var line in proseLines)
        { if (y + ProseH <= shown) ui.InkProse(line, x, y, Kit.InkDeep); y += ProseH; }
        y += ui.U(10);

        if (C.MonumentOutcome != "")
        {
            var go = new Rectangle(r.Center.X - ui.U(70), r.Bottom - ui.U(42), ui.U(140), ui.U(22));
            if ((go.Bottom <= shown && ui.Button(go, "CONTINUE", note: "ENTER")) || Pressed(Keys.Enter) || Pressed(Keys.Space))
            { C.ActiveMonument = null; C.MonumentOutcome = ""; panel = PanelKind.None; }
            return;
        }

        var choices = new List<(int Idx, string Label, string Hint)>
        {
            (0, "Pay respects at the cairn", "+4 company cohesion, blessed for 3 days, +10 renown"),
            (1, "Swear by their deeds before your men", "+20 renown and local volunteers inspired by the legend"),
        };
        if (hasRelic) choices.Add((2, $"Search the cairn for {leg.Relic!.Name}", "Claim this past Legend's Named Relic heirloom"));

        for (int i = 0; i < choices.Count; i++)
        {
            var (idx, label, hint) = choices[i];
            bool relicChoice = idx == 2;
            var hit = new Rectangle(x - ui.U(6), y, cw + ui.U(12), ui.U(33));
            if (hit.Bottom > shown) break;
            bool hover = ui.Hover(hit);
            ui.Rect(hit, relicChoice ? Kit.Gilt * (hover ? 0.28f : 0.16f) : Kit.Ink * (hover ? 0.14f : 0.06f));
            ui.Frame(hit, hover ? InkRed * 0.85f : relicChoice ? Kit.GiltDark : Kit.Ink * 0.32f, ui.U(1));
            if (relicChoice || hover) ui.Rect(new Rectangle(hit.X, hit.Y, ui.U(3), hit.Height), relicChoice ? Kit.GiltDark : InkRed * 0.85f);
            var cap = new Rectangle(x + ui.U(2), y + ui.U(6), ui.U(14), ui.U(12));
            ui.Rect(cap, hover ? InkRed : relicChoice ? Kit.GiltDark : Kit.InkDeep * 0.82f);
            ui.TextC($"{i + 1}", cap.Center.X, cap.Y + ui.U(2), Kit.Parchment, 1);
            ui.InkProse(label, x + ui.U(22), y + ui.U(6), Kit.InkDeep);
            ui.InkProse(hint, x + ui.U(22), y + ui.U(19), InkFade);
            if (relicChoice)
            {
                var badge = new Rectangle(hit.Right - ui.U(46), y + ui.U(6), ui.U(40), ui.U(12));
                ui.Rect(badge, Kit.GiltDark);
                ui.TextC("RELIC", badge.Center.X, badge.Y + ui.U(2), Kit.Parchment, 1);
            }
            if (hover) Ui.Hovered(hit);
            if (ui.Clicked(hit) || Pressed(Keys.D1 + i)) { Ui.OnClick?.Invoke(); C.ResolveMonumentChoice(idx); AudioEngine.Current?.Play(Cue.ScrollUnroll, 0.6f); break; }
            y += ui.U(38);
        }
    }
}
