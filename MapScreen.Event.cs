using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the event book (docs/pre-demo/EVENTS.md EV3). A parchment scroll unrolls over the dimmed map: a painted vignette
/// with the faces of the cast, the title, the prose in ink, and the choices as inked lines with their cost and, for a check,
/// what it tests and its odds. Choosing turns the page to the outcome: what happened, what changed, CONTINUE.
partial class MapScreen
{
    static readonly Color InkRed = new(138, 34, 24), InkGreen = new(28, 86, 36), InkFade = new(82, 62, 42);
    string bookKey = "";
    float bookAt;

    /// How far the scroll is open: it unrolls afresh for each new page.
    float BookOpen(string key)
    {
        if (key != bookKey) { bookKey = key; bookAt = Ui.Now; }
        return Ui.Instant ? 1 : Ease.Out(Math.Clamp((Ui.Now - bookAt) / 0.45f, 0, 1));
    }

    int BookWidth() => Math.Min(ui.U(580), ui.W - ui.U(40));
    int BookPicH() => Math.Clamp(ui.H / 5, ui.U(84), ui.U(130));
    int ProseH => ui.U(11);

    /// The faces of the cast, framed in the picture's corners: the man (or the lord) at the left, the other (or a companion) at the right.
    void EventFaces(Rectangle pic, PendingEvent pe)
    {
        int fs = Math.Min(pic.Height - ui.U(16), ui.U(84));
        Soldier? sa = C.LivingMan(pe.Soldier), sb = C.LivingMan(pe.Other);
        Character? cl = C.CharacterById(pe.Lord), ck = C.CharacterById(pe.Companion);
        Face? left = sa != null ? PortraitPainter.Of(sa, C.Player.Companies.FirstOrDefault(z => z.Roll.Contains(sa))) : cl != null ? PortraitPainter.Of(C, cl) : null;
        Face? right = sb != null ? PortraitPainter.Of(sb, C.Player.Companies.FirstOrDefault(z => z.Roll.Contains(sb))) : ck != null ? PortraitPainter.Of(C, ck) : null;
        string leftName = sa != null ? sa.Name : cl != null ? cl.Name : "";
        string rightName = sb != null ? sb.Name : ck != null ? ck.Name : "";
        bool both = left != null && right != null;
        foreach (var (face, name, atLeft) in new[] { (left, leftName, true), (right, rightName, false) })
        {
            if (face == null) continue;
            var fb = new Rectangle(atLeft ? pic.X + ui.U(8) : pic.Right - ui.U(8) - fs, pic.Y + ui.U(8), fs, fs);
            ui.Rect(new Rectangle(fb.X - ui.U(2), fb.Y - ui.U(2), fb.Width + ui.U(4), fb.Height + ui.U(4)), Kit.GiltDark);
            ui.FaceHead(face, fb);
            if (both && name != "")
            {
                string shortName = name.Split(' ')[0].ToUpperInvariant();
                var tag = new Rectangle(fb.X, fb.Bottom - ui.U(11), fb.Width, ui.U(11));
                ui.Rect(tag, Color.Black * 0.72f);
                ui.TextC(Kit.Fit(ui, shortName, fb.Width - ui.U(4), 1), tag.Center.X, tag.Y + ui.U(2), Style.Text, 1);
            }
        }
    }

    /// One choice laid out for the page: its words, the check line, the hint, why it is barred.
    sealed record ChoiceLines(int Index, ChoiceDef Choice, List<string> Label, string Cost, string Check, float Odds, List<string> Hint, string Why)
    {
        public int Height(int lh, int small) => Label.Count * lh + (Check != "" && Why == "" ? small + 4 : 0) + Hint.Count * small + (Why != "" ? small + 2 : 0) + 6;
    }

    ChoiceLines LayChoice(int i, ChoiceDef ch, PendingEvent pe, int cw)
    {
        string why = C.ChoiceBlocked(ch, pe);
        string cost = string.Join("  ", ch.Cost.Select(fx => C.CostLine(fx, pe).Text));
        int labelW = cw - ui.U(26) - (cost != "" && why == "" ? ui.TW(cost, 1) + ui.U(12) : 0);
        string check = "";
        float odds = -1;
        if (ch.Check is CheckDef k) { odds = C.Odds(k, pe); check = C.CheckText(k, pe); }
        var hint = ch.Hint != "" && why == "" ? ui.Wrap(C.Fill(ch.Hint, pe), cw - ui.U(18), 1) : new List<string>();
        return new(i, ch, ui.Wrap(C.Fill(ch.Label, pe), labelW, 1), cost, check, odds, hint, why);
    }

    void EventPanel()
    {
        if (C.EventOutcome is EventResult res) { OutcomePage(res); return; }
        if (C.S.Event is not PendingEvent pe || Campaign.EventOf(pe.Id) is not EventDef e) { panel = PanelKind.None; return; }
        int w = BookWidth(), cw = w - ui.U(48), lh = ProseH, small = ui.U(10), picH = BookPicH();
        var prose = ui.Wrap(C.Fill(e.Text, pe), cw, 1);
        var rows = C.OfferedChoices(pe).Select(x => LayChoice(x.Index, x.Choice, pe, cw)).ToList();
        int h = ui.U(28) + picH + ui.U(30) + prose.Count * lh + ui.U(14) + rows.Sum(c => c.Height(lh, small) + ui.U(10)) + ui.U(26);
        h = Math.Min(h, ui.H - ui.U(12));
        ui.Dim(0.55f);
        var r = new Rectangle((ui.W - w) / 2, (ui.H - h) / 2, w, h);
        int shown = ui.Scroll(r, BookOpen($"{pe.Id}:{pe.Soldier}:{pe.Lord}:{pe.Settlement}"));
        int x = r.X + ui.U(24), y = r.Y + ui.U(26);
        y = PageHead(r, x, y, cw, picH, shown, e.Id, C.Fill(e.Title, pe), pe);
        foreach (var line in prose) { if (y + lh <= shown) ui.InkProse(line, x, y, Kit.InkDeep); y += lh; }
        y += ui.U(6);
        if (y + ui.U(4) <= shown) ui.Rect(new Rectangle(x, y, cw, ui.U(1)), Kit.Ink * 0.35f);
        y += ui.U(8);
        for (int n = 0; n < rows.Count; n++)
        {
            var c = rows[n];
            int ch = c.Height(lh, small);
            if (y + ch <= shown && ChoiceRow(c, n, x, y, cw, lh, small))
            {
                note = C.Choose(c.Index) ?? "";
                if (note == "") AudioEngine.Current?.Play(Cue.ScrollUnroll, 0.6f);
            }
            y += ch + ui.U(8);
        }
        for (int n = 0; n < rows.Count && n < 9; n++)
            if (Pressed(Keys.D1 + n) && rows[n].Why == "")
            {
                note = C.Choose(rows[n].Index) ?? "";
                if (note == "") AudioEngine.Current?.Play(Cue.ScrollUnroll, 0.6f);
                break;
            }
        if (note != "" && r.Bottom - ui.U(22) <= shown) ui.Ink(note, r.Center.X, r.Bottom - ui.U(24), InkRed, 1, cx: true);
    }

    /// The head of a page: the vignette with the cast's faces, the title, a rule. Returns where the prose starts.
    int PageHead(Rectangle r, int x, int y, int cw, int picH, int shown, string id, string title, PendingEvent pe)
    {
        var pic = new Rectangle(x, y, cw, picH);
        if (pic.Bottom <= shown)
        {
            ui.Rect(new Rectangle(pic.X - ui.U(2), pic.Y - ui.U(2), pic.Width + ui.U(4), pic.Height + ui.U(4)), Kit.InkDeep);
            ui.Clip(pic, () => EventScene(pic, id));
            EventFaces(pic, pe);
        }
        y = pic.Bottom + ui.U(10);
        title = title.ToUpperInvariant();
        int ts = ui.TW(title, 2) <= cw - ui.U(16) ? 2 : 1;
        if (y + ui.U(16) <= shown) ui.Ink(title, r.Center.X, y, Kit.InkDeep, ts, cx: true);
        return y + ui.U(ts == 2 ? 24 : 16);
    }

    /// One choice on the page: numeral, words, cost at the right; under it what it tests and its odds, the hint, or why it
    /// is barred (struck through). True on a click.
    bool ChoiceRow(ChoiceLines c, int n, int x, int y, int cw, int lh, int small)
    {
        bool able = c.Why == "";
        var hit = new Rectangle(x - ui.U(6), y - ui.U(3), cw + ui.U(12), c.Height(lh, small) + ui.U(4));
        bool hover = able && ui.Hover(hit);
        ui.Rect(hit, Kit.Ink * (hover ? 0.14f : able ? 0.055f : 0.025f));
        ui.Frame(hit, hover ? InkRed * 0.8f : Kit.Ink * (able ? 0.30f : 0.15f), ui.U(1));
        if (hover) ui.Rect(new Rectangle(hit.X, hit.Y, ui.U(3), hit.Height), InkRed * 0.85f);
        var ink = able ? Kit.InkDeep : InkFade;
        var cap = new Rectangle(x - ui.U(1), y + ui.U(1), ui.U(14), ui.U(12));
        ui.Rect(cap, able ? (hover ? InkRed : Kit.InkDeep * 0.82f) : Kit.Ink * 0.22f);
        ui.TextC($"{n + 1}", cap.Center.X, cap.Y + ui.U(2), able ? Kit.Parchment : InkFade, 1);
        int lx = x + ui.U(20), ly = y + ui.U(2);
        foreach (var line in c.Label) { ui.InkProse(line, lx, ly, ink); ly += lh; }
        if (c.Cost != "" && able) ui.Ink(c.Cost, x + cw - ui.TW(c.Cost, 1), y + ui.U(2), InkRed, 1);
        if (!able) { ui.Ink(c.Why.ToUpperInvariant(), lx, ly + ui.U(1), InkRed, 1); return false; }
        if (c.Check != "")
        {
            ly += ui.U(2);
            string tests = $"TESTS {c.Check}";
            ui.Ink(tests, lx, ly, Kit.Ink, 1);
            int bx = lx + ui.TW(tests, 1) + ui.U(10), bw = ui.U(54), bh = ui.U(6);
            var bar = new Rectangle(bx, ly + ui.U(1), bw, bh);
            ui.Rect(bar, Kit.Ink * 0.22f);
            int fillW = Math.Max(ui.U(3), (int)(bw * c.Odds));
            ui.Rect(new Rectangle(bar.X, bar.Y, Math.Min(bw, fillW), bh), c.Odds >= 0.5f ? InkGreen : InkRed);
            ui.Frame(bar, Kit.Ink * 0.5f, ui.U(1));
            ui.Ink(Campaign.OddsWord(c.Odds), bx + bw + ui.U(8), ly, c.Odds >= 0.5f ? InkGreen : InkRed, 1);
            ly += small + ui.U(2);
        }
        foreach (var line in c.Hint) { ui.InkProse(line, lx, ly, InkFade); ly += small; }
        if (hover) Ui.Hovered(hit);
        bool click = ui.Clicked(hit);
        if (click) Ui.OnClick?.Invoke();
        return click;
    }

    /// The page after a choice: what came of it, what changed as pictured lines, and CONTINUE.
    void OutcomePage(EventResult res)
    {
        var pe = res.Event;
        int w = BookWidth(), cw = w - ui.U(48), lh = ProseH, picH = BookPicH();
        var prose = ui.Wrap(res.Text, cw, 1);
        var chose = ui.Wrap(res.Choice, cw - ui.U(18), 1);
        var lines = res.Lines.Where(l => l.Text != "").ToList();
        int cols = lines.Count > 4 ? 2 : 1, lineRows = (lines.Count + cols - 1) / cols;
        int h = ui.U(28) + picH + ui.U(30) + chose.Count * lh + ui.U(6) + (res.Odds >= 0 ? ui.U(20) : 0) + prose.Count * lh + ui.U(12)
            + lineRows * ui.U(14) + (res.More ? ui.U(16) : 0) + ui.U(52);
        h = Math.Min(h, ui.H - ui.U(12));
        ui.Dim(0.55f);
        var r = new Rectangle((ui.W - w) / 2, (ui.H - h) / 2, w, h);
        int shown = ui.Scroll(r, BookOpen("outcome:" + res.Id + res.Choice));
        int x = r.X + ui.U(24), y = r.Y + ui.U(26);
        y = PageHead(r, x, y, cw, picH, shown, res.Id, res.Title, pe);
        bool Seen(int at, int hh) => at + hh <= shown;
        foreach (var line in chose)
        {
            string quoted = $"\"{line}\"";
            if (Seen(y, lh)) ui.InkProse(quoted, r.Center.X - ui.TW(quoted, 1) / 2f, y, InkFade);
            y += lh;
        }
        y += ui.U(6);
        if (res.Odds >= 0)
        {
            var (word, col) = res.Result switch
            {
                "great" => ("A GREAT SUCCESS", InkGreen),
                "pass" => ("SUCCESS", InkGreen),
                "awful" => ("DISASTER", InkRed),
                _ => ("FAILURE", InkRed),
            };
            string tag = $"{word}  ({Campaign.OddsWord(res.Odds)})";
            int tw = ui.TW(tag, 1) + ui.U(18);
            var pill = new Rectangle(r.Center.X - tw / 2, y - ui.U(2), tw, ui.U(14));
            if (Seen(y, ui.U(14))) { ui.Rect(pill, col * 0.12f); ui.Frame(pill, col * 0.55f, ui.U(1)); ui.Ink(tag, r.Center.X, y + ui.U(1), col, 1, cx: true); }
            y += ui.U(20);
        }
        foreach (var line in prose) { if (Seen(y, lh)) ui.InkProse(line, x, y, Kit.InkDeep); y += lh; }
        y += ui.U(8);
        if (lines.Count > 0 && Seen(y, ui.U(2))) ui.Rect(new Rectangle(x, y - ui.U(4), cw, ui.U(1)), Kit.Ink * 0.3f);
        int colW = cw / cols;
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            int lx = x + i / lineRows * colW, ly = y + ui.U(2) + i % lineRows * ui.U(14);
            if (!Seen(ly, ui.U(12))) continue;
            var col = l.Sign > 0 ? InkGreen : l.Sign < 0 ? InkRed : Kit.Ink;
            if (l.Icon != "") ui.Icon(l.Icon, lx, ly, col, 1);
            ui.Ink(Kit.Fit(ui, l.Text.TrimEnd('.'), colW - ui.U(16), 1), lx + ui.U(12), ly, col, 1);
        }
        y += lineRows * ui.U(14) + ui.U(2);
        if (res.More) { if (Seen(y, lh)) ui.InkProse("This is not the end of it.", r.Center.X - ui.TW("This is not the end of it.", 1) / 2f, y, InkFade); y += ui.U(16); }
        var go = new Rectangle(r.Center.X - ui.U(70), r.Bottom - ui.U(44), ui.U(140), ui.U(20));
        bool done = Seen(go.Bottom, 0) && ui.Button(go, "CONTINUE", note: "ENTER");
        if (done || Pressed(Keys.Enter) || Pressed(Keys.Space))
        {
            C.EventOutcome = null;
            panel = PanelKind.None;
            note = "";
        }
    }

    // ---------- the pictures ----------

    /// The painted scene for an event: raiders burning a village, a castle and its court, the company's camp, or a market town.
    void EventScene(Rectangle p, string id)
    {
        float t = Environment.TickCount64 / 1000f;
        string scene = id == "monument_cairn" ? "site_cairn" : Campaign.EventOf(id)?.Scene ?? "";
        string kind = scene.StartsWith("site_") || scene == "shrine" ? "site" : scene != "" ? scene : "camp";
        string siteKind = scene == "shrine" ? "shrine" : scene.StartsWith("site_") ? scene[5..] : "";
        bool day = kind is "town" or "court" or "site" or "village" or "road" or "ford" or "hills" or "sea" or "forest" && siteKind != "stones"
            && !C.Holds("night");
        ui.VGrad(p, day ? new Color(70, 96, 130) : Kit.Night, day ? new Color(214, 160, 110) : kind == "burning" ? new Color(120, 54, 40) : Kit.Dusk, 2);
        if (!day) ui.Stars(new Rectangle(p.X, p.Y, p.Width, p.Height / 2), t, 30);
        var orb = new Vector2(p.X + p.Width * 0.68f, p.Y + p.Height * 0.28f); // clear of a camp event's right-hand face
        if (day) { ui.Disc(orb, ui.U(20), new Color(255, 230, 170) * 0.25f); ui.Disc(orb, ui.U(12), new Color(255, 236, 190)); }
        else
        {
            float glow = 0.05f + 0.02f * MathF.Sin(t * 0.9f);
            ui.Disc(orb, ui.U(26), new Color(236, 226, 200) * glow); ui.Disc(orb, ui.U(18), new Color(236, 226, 200) * (glow * 1.6f));
            ui.Disc(orb, ui.U(12), new Color(236, 226, 200));
        }
        // Clouds drift across, slowly; the far hills slide a little against the near ones.
        for (int i = 0; i < 3; i++)
        {
            float span = p.Width + ui.U(160), cx = p.X - ui.U(80) + (i * 0.41f * span + t * ui.U(3 + 2 * i)) % span;
            float cy = p.Y + p.Height * (0.18f + 0.13f * i);
            var cc = (day ? Color.White : new Color(120, 90, 110)) * (day ? 0.25f : 0.12f);
            if (cx < p.X + ui.U(12) || cx + ui.U(72) > p.Right) continue; // only inside the frame
            for (int k = 0; k < 5; k++) ui.Disc(new Vector2(cx + ui.U(k * 12), cy + ui.U(MathF.Sin(k * 2.1f + i) * 2)), ui.U(6 + (k * 5 + i) % 6), cc);
        }
        int ground = p.Bottom - ui.U(22);
        var far = day ? new Color(92, 100, 110) : new Color(54, 30, 36);
        var near = day ? new Color(40, 46, 40) : new Color(22, 14, 18);
        ui.Hills(p, p.Bottom - ui.U(36), ui.U(10), 0.9f + id.Length + MathF.Sin(t * 0.07f) * 0.15f, far);
        switch (kind)
        {
            case "burning": Village(p, ground, near, t, true); break;
            case "town": Village(p, ground, near, t, false, town: true); break;
            case "court": Court(p, ground, near, t, id == "tournament"); break;
            case "site": SiteScene(p, ground, near, t, siteKind); break;
            case "village": Village(p, ground, near, t, false); break;
            case "road": Road(p, ground, near, t, false); break;
            case "ford": Road(p, ground, near, t, true); break;
            case "forest": Forest(p, ground, near, t); break;
            case "hills": Hills(p, ground, near, t); break;
            case "sea": Sea(p, ground, near, t); break;
            case "field": SiteScene(p, ground, near, t, "battlefield"); break;
            case "flame": Flame(p, ground, near, t); break;
            default: Camp(p, ground, near, t); break;
        }
        ui.Hills(p, ground + ui.U(6), ui.U(4), 3.3f, near);
        if (C.Winter) for (int i = 0; i < 60; i++)
            {
                float fx = (i * 0.618f + t * 0.03f * (1 + i % 3)) % 1, fy = (i * 0.37f + t * 0.08f * (1 + i % 2)) % 1;
                ui.Rect(new Rectangle(p.X + (int)(fx * p.Width), p.Y + (int)(fy * p.Height), ui.U(1), ui.U(1)), Color.White * 0.7f);
            }
    }

    /// Houses with pitched roofs and a church tower; burning ones throw fire and smoke.
    void Village(Rectangle p, int ground, Color c, float t, bool burning, bool town = false)
    {
        int u = Math.Max(1, ui.U(1));
        for (int i = 0; i < 6; i++)
        {
            int hx = p.X + (int)(p.Width * (0.2f + i * 0.09f)), hw = ui.U(14 + i % 3 * 4), hh = ui.U(10 + i % 2 * 4);
            ui.Rect(new Rectangle(hx, ground - hh, hw, hh), c);
            for (int k = 0; k < hw / 2; k += u) ui.Rect(new Rectangle(hx + k, ground - hh - k / 2 - u, hw - 2 * k, u), c);
            if (!burning) { ui.Rect(new Rectangle(hx + hw / 2 - ui.U(1), ground - hh / 2, ui.U(3), ui.U(3)), Kit.Ember * 0.8f); continue; }
            if (i % 2 != 0) continue;
            ui.Disc(new Vector2(hx + hw / 2, ground - hh), ui.U(14), Kit.Ember * 0.12f);
            ui.Icon("fire", hx + hw / 2 - ui.U(7), ground - hh - ui.U(12), Kit.Ember, 2);
            for (int s = 0; s < 5; s++)
            {
                float k = (t * 0.3f + s * 0.2f + i) % 1;
                ui.Disc(new Vector2(hx + hw / 2 + k * ui.U(30), ground - hh - ui.U(14) - k * p.Height * 0.6f), ui.U(4) + k * ui.U(10), new Color(60, 50, 56) * (0.5f * (1 - k)));
            }
        }
        ui.Rect(new Rectangle(p.X + (int)(p.Width * 0.38f), ground - ui.U(26), ui.U(4), ui.U(26)), c);
        ui.Rect(new Rectangle(p.X + (int)(p.Width * 0.38f) - ui.U(2), ground - ui.U(30), ui.U(8), ui.U(4)), c);
        if (town)
        {
            int wx = p.X + (int)(p.Width * 0.14f), ww = (int)(p.Width * 0.72f), wh = ui.U(9);
            ui.Rect(new Rectangle(wx, ground - wh, ww, wh), c);
            for (int x = wx; x < wx + ww - ui.U(4); x += ui.U(8)) ui.Rect(new Rectangle(x, ground - wh - ui.U(3), ui.U(4), ui.U(3)), c);
            int gx = p.X + (int)(p.Width * 0.68f);
            ui.Rect(new Rectangle(gx, ground - ui.U(20), ui.U(16), ui.U(20)), c);
            ui.Rect(new Rectangle(gx + ui.U(5), ground - ui.U(8), ui.U(6), ui.U(8)), Color.Black * 0.65f);
        }
    }

    /// A castle with banners on its towers; at a tournament, a row of pavilions before it.
    void Court(Rectangle p, int ground, Color c, float t, bool lists)
    {
        int cx = p.X + p.Width / 2;
        ui.Castle(cx, ground + ui.U(2), 3, c, Kit.Ember);
        var hd = Style.Sides[Style.PaletteOf(Math.Max(0, C.Captain.Contract))];
        ui.Banner(cx - ui.U(36), p.Y + ui.U(16), ui.U(22), ui.U(46), hd, t * 1.5f);
        ui.Banner(cx + ui.U(36), p.Y + ui.U(16), ui.U(22), ui.U(46), hd, t * 1.5f + 1.3f);
        if (!lists) return;
        for (int i = 0; i < 5; i++)
        {
            int tx = p.X + (int)(p.Width * (0.12f + i * 0.19f)), tw = ui.U(22);
            var cloth = Style.Sides[i % 4].Cloth;
            for (int k = 0; k < tw / 2; k += Math.Max(1, ui.U(1))) ui.Rect(new Rectangle(tx + k, ground - ui.U(16) + (tw / 2 - k) / 2 - tw / 4, tw - 2 * k, Math.Max(1, ui.U(1))), cloth);
            ui.Rect(new Rectangle(tx + ui.U(2), ground - ui.U(16), tw - ui.U(4), ui.U(16)), Kit.Mul(cloth, 0.8f));
        }
    }

    /// The company's camp: tents around a fire, its sparks rising.
    void Camp(Rectangle p, int ground, Color c, float t)
    {
        for (int i = 0; i < 5; i++)
        {
            int tx = p.X + (int)(p.Width * (0.12f + i * 0.17f)) + (i == 2 ? ui.U(40) : 0), tw = ui.U(26 + i % 2 * 8), th = tw * 2 / 3;
            for (int k = 0; k < th; k += Math.Max(1, ui.U(1))) { int ww = tw * k / th; ui.Rect(new Rectangle(tx + (tw - ww) / 2, ground - th + k, ww, Math.Max(1, ui.U(1))), c); }
            ui.Rect(new Rectangle(tx + tw / 2, ground - th - ui.U(6), ui.U(1), ui.U(6)), c);
        }
        var fire = new Vector2(p.X + p.Width * 0.46f, ground - ui.U(4));
        float flick = 0.8f + 0.2f * MathF.Sin(t * 11) * MathF.Sin(t * 4.3f + 1);
        for (int g = 0; g < 4; g++) ui.Disc(fire, ui.U(40 - g * 9) * flick, Kit.Ember * 0.05f);
        ui.Icon("fire", (int)fire.X - ui.U(7), (int)fire.Y - ui.U(12), Kit.Ember, 2);
        for (int s = 0; s < 8; s++)
        {
            float k = (t * 0.4f + s * 0.125f) % 1;
            ui.Rect(new Rectangle((int)(fire.X + MathF.Sin(k * 9 + s) * ui.U(8)), (int)(fire.Y - ui.U(14) - k * p.Height * 0.5f), ui.U(1), ui.U(1)), Kit.Ember * (1 - k));
        }
    }

    /// The road running off between hedges to the hills, a milestone; at a ford, the water across it and a boat on its chain.
    void Road(Rectangle p, int ground, Color c, float t, bool ford)
    {
        int u = Math.Max(1, ui.U(1)), cx = p.X + p.Width * 2 / 5;
        var dirt = Color.Lerp(c, new Color(150, 120, 84), 0.45f);
        ui.Rect(new Rectangle(p.X, ground - ui.U(3), p.Width, ui.U(8)), dirt); // the road along the valley
        for (int i = 0; i < 40; i++) ui.Rect(new Rectangle(p.X + i * p.Width / 40 + i % 3 * ui.U(2), ground - ui.U(1) + i % 2 * ui.U(3), ui.U(5), u), Kit.Mul(dirt, 0.8f)); // ruts
        for (int i = 0; i < 26; i++) // a hedgerow behind it, with gaps, a tree standing out of it now and then
        {
            int hx = p.X + i * p.Width / 25;
            if (i % 9 == 4) continue;
            ui.Disc(new Vector2(hx, ground - ui.U(6)), ui.U(5 + i * 7 % 3), c);
            if (i % 7 == 2) { ui.Rect(new Rectangle(hx - u, ground - ui.U(26), 2 * u, ui.U(20)), c); ui.Disc(new Vector2(hx, ground - ui.U(28)), ui.U(10), c); }
        }
        int mx = p.X + p.Width * 3 / 4; // a milestone at the roadside
        ui.Rect(new Rectangle(mx, ground - ui.U(12), ui.U(6), ui.U(10)), Kit.Mul(dirt, 0.6f));
        ui.Rect(new Rectangle(mx + u, ground - ui.U(13), ui.U(4), u), Kit.Mul(dirt, 0.6f));
        if (!ford) return;
        int wy = ground - ui.U(4);
        var water = new Color(70, 104, 120);
        int fx = cx - ui.U(70), fw = ui.U(220); // the river crossing the road
        ui.Rect(new Rectangle(fx, wy, fw, ui.U(12)), water);
        for (int i = 0; i < 14; i++) // glints running on the current
        {
            float k = (i * 0.37f + t * 0.06f) % 1;
            ui.Rect(new Rectangle(fx + (int)(k * (fw - ui.U(6))), wy + ui.U(2 + i % 4 * 2), ui.U(6), u), Color.White * 0.35f);
        }
        int bx = cx + ui.U(60); // the toll boat, low in the water, its chain to the bank
        for (int k = 0; k < ui.U(4); k += u) ui.Rect(new Rectangle(bx + k, wy + k, ui.U(30) - 2 * k, u), c);
        ui.Rect(new Rectangle(fx, wy + ui.U(1), bx - fx, u), Color.Lerp(c, Color.Gray, 0.4f)); // its chain to the near bank
    }

    /// Dark woods close on both sides: pines in rows, the road a pale gap between them.
    void Forest(Rectangle p, int ground, Color c, float t)
    {
        int u = Math.Max(1, ui.U(1));
        void Pine(int x, int h, Color col)
        {
            for (int k = 0; k < h; k += u) { int ww = (int)(h * 0.32f * k / h) + u; ui.Rect(new Rectangle(x - ww, ground - h + k, 2 * ww, u), col); }
            ui.Rect(new Rectangle(x - u, ground - ui.U(4), 2 * u, ui.U(4)), col);
        }
        var back = Color.Lerp(c, new Color(60, 70, 66), 0.3f);
        for (int i = 0; i < 18; i++) Pine(p.X + i * p.Width / 17, ui.U(38 + (i * 7 % 5) * 6), back);
        for (int i = 0; i < 12; i++)
        {
            int x = p.X + i * p.Width / 11;
            if (Math.Abs(x - p.Center.X) < ui.U(36)) continue; // the gap the road goes through
            Pine(x, ui.U(56 + (i * 5 % 4) * 10), c);
        }
        for (int s = 0; s < 6; s++) // mist drifting low between the trunks
        {
            float k = (t * 0.02f + s / 6f) % 1;
            ui.Disc(new Vector2(p.X + k * p.Width, ground - ui.U(6)), ui.U(18), Color.White * 0.05f);
        }
    }

    /// The high country: crags and a long slope, a cairn of shepherd's stones, sheep as pale dots.
    void Hills(Rectangle p, int ground, Color c, float t)
    {
        int u = Math.Max(1, ui.U(1));
        for (int k = 0; k < ui.U(60); k += u) // a crag standing up on the right
        {
            int ww = ui.U(10) + k;
            ui.Rect(new Rectangle(p.X + p.Width * 3 / 4 - ww / 2 + (k % ui.U(9) == 0 ? ui.U(3) : 0), ground - ui.U(60) + k, ww, u), c);
        }
        ui.Hills(p, ground - ui.U(10), ui.U(14), 7.1f, Color.Lerp(c, Color.Gray, 0.15f));
        for (int i = 0; i < 9; i++)
        {
            float bob = MathF.Sin(t * 0.7f + i) * 0.5f;
            ui.Rect(new Rectangle(p.X + p.Width / 6 + i * ui.U(13) + i % 3 * ui.U(5), ground - ui.U(12) - i % 2 * ui.U(5) + (int)bob, ui.U(5), ui.U(3)), new Color(214, 208, 190));
        }
    }

    /// The shore: a grey sea to the line of the sky, waves breaking white, a cog under sail far out.
    void Sea(Rectangle p, int ground, Color c, float t)
    {
        int u = Math.Max(1, ui.U(1)), sea = p.Y + p.Height / 2;
        ui.VGrad(new Rectangle(p.X, sea, p.Width, ground - sea), new Color(70, 92, 108), new Color(44, 62, 76), 2);
        for (int i = 0; i < 24; i++)
        {
            float k = (i * 0.41f + t * 0.03f * (1 + i % 3)) % 1;
            int y = sea + ui.U(4) + i % 6 * ((ground - sea) / 6);
            ui.Rect(new Rectangle(p.X + (int)(k * p.Width), y, ui.U(4 + i % 3 * 3), u), Color.White * 0.3f);
        }
        int sx = p.X + (int)(p.Width * (0.62f + 0.03f * MathF.Sin(t * 0.1f))), sy = sea + ui.U(6);
        for (int k = 0; k < ui.U(4); k += u) ui.Rect(new Rectangle(sx + k, sy + k, ui.U(22) - 2 * k, u), new Color(40, 34, 30));
        ui.Rect(new Rectangle(sx + ui.U(10), sy - ui.U(18), u, ui.U(18)), new Color(40, 34, 30));
        ui.Rect(new Rectangle(sx + ui.U(4), sy - ui.U(16), ui.U(12), ui.U(12)), new Color(200, 190, 170));
        for (int i = 0; i < 6; i++) // rocks along the shore, and the white of the surf on them
        {
            int rx = p.X + i * p.Width / 5 + ui.U(8);
            ui.Disc(new Vector2(rx, ground), ui.U(10 + i % 3 * 4), c);
            ui.Rect(new Rectangle(rx - ui.U(10), ground - ui.U(4) - (int)(MathF.Sin(t * 1.3f + i) * ui.U(2)), ui.U(20), u), Color.White * 0.5f);
        }
    }

    /// The Black Flame: a ruined tower against a red sky, a dark fire burning without smoke, black iron of the circlet over it.
    void Flame(Rectangle p, int ground, Color c, float t)
    {
        ui.Rect(new Rectangle(p.X, p.Y, p.Width, p.Height), new Color(60, 10, 14) * 0.45f);
        int cx = p.X + p.Width / 2, u = Math.Max(1, ui.U(1));
        int[] hs = { 30, 62, 44, 80, 20 };
        for (int i = 0; i < hs.Length; i++) ui.Rect(new Rectangle(cx - ui.U(120) + i * ui.U(52), ground - ui.U(hs[i]), ui.U(22), ui.U(hs[i])), c);
        var fire = new Vector2(cx, ground - ui.U(6));
        float flick = 0.85f + 0.15f * MathF.Sin(t * 9) * MathF.Sin(t * 3.1f);
        for (int g = 0; g < 4; g++) ui.Disc(fire, ui.U(44 - g * 10) * flick, new Color(140, 20, 60) * 0.08f);
        for (int k = 0; k < 12; k++) // tongues of the flame: near black, edged in red
        {
            float a = (t * 0.6f + k / 12f) % 1, x = fire.X + MathF.Sin(k * 2.3f + t * 2) * ui.U(10);
            ui.Disc(new Vector2(x, fire.Y - a * ui.U(40)), ui.U(7) * (1 - a), new Color(160, 30, 50) * (1 - a));
            ui.Disc(new Vector2(x, fire.Y - a * ui.U(40)), ui.U(4) * (1 - a), new Color(20, 6, 12));
        }
        int ry = p.Y + ui.U(22), rw = ui.U(40); // the hollow crown, hanging in the dark above the fire
        ui.Rect(new Rectangle(cx - rw / 2, ry + ui.U(8), rw, ui.U(4)), new Color(26, 22, 26));
        for (int i = 0; i < 5; i++) ui.Rect(new Rectangle(cx - rw / 2 + i * rw / 4 - u, ry + (i % 2 == 0 ? 0 : ui.U(4)), 2 * u + u, ui.U(10)), new Color(26, 22, 26));
        ui.Rect(new Rectangle(cx - rw / 2 + ui.U(2), ry + ui.U(10), rw - ui.U(4), u), new Color(180, 44, 52) * 0.65f);
    }

    /// A site as a silhouette against the sky: each kind its own shape (docs/archive/SIDE_CONTENT.md S2).
    void SiteScene(Rectangle p, int ground, Color c, float t, string kind)
    {
        int cx = p.X + p.Width / 2, u = ui.U(1);
        void Block(int x, int w, int h) => ui.Rect(new Rectangle(cx + x * u, ground - h * u, w * u, h * u), c);
        switch (kind)
        {
            case "ruin":
                int[] hs = { 18, 34, 12, 52, 26, 8, 30 };
                for (int i = 0; i < hs.Length; i++) Block(-90 + i * 26, 18, hs[i]);
                Block(-14, 28, 70);
                ui.Rect(new Rectangle(cx - 6 * u, ground - 50 * u, 12 * u, 18 * u), Color.Black * 0.6f);
                break;
            case "shrine":
                Block(-24, 48, 34);
                for (int k = 0; k < 24; k++) Block(-24 + k, 48 - 2 * k, 34 + k);
                Block(-3, 6, 86);
                ui.Rect(new Rectangle(cx - 10 * u, ground - 80 * u, 20 * u, 4 * u), c); // the cross on the spire
                ui.Rect(new Rectangle(cx - 3 * u, ground - 20 * u, 6 * u, 8 * u), Kit.Ember * (0.6f + 0.3f * MathF.Sin(t * 5)));
                break;
            case "stones":
                for (int i = 0; i < 9; i++) Block(-110 + i * 26, 12, 30 + (i * 37 % 5) * 6);
                ui.Disc(new Vector2(cx, p.Y + p.Height * 0.3f), ui.U(26), new Color(236, 226, 200) * 0.15f);
                break;
            case "cairn": // a mound of stones over your men, a spear stood in it with the company's pennon (C7)
                {
                    // Stones heaped in courses, each its own size with a lit top edge, so the mound reads as stones, not steps.
                    var lit = Color.Lerp(c, new Color(150, 156, 164), 0.6f);
                    for (int row = 0; row < 5; row++)
                        for (int i = 0; i < 6 - row; i++)
                        {
                            int sw = 12 + (i * 5 + row * 3) % 4 * 2, sh = 7 + (i + row) % 3 * 2, sx = -48 + row * 8 + i * 16 + (row * 3 + i) % 3 - 1, top = 6 + row * 8 + sh;
                            ui.Rect(new Rectangle(cx + sx * u, ground - top * u, sw * u, sh * u), c);
                            ui.Rect(new Rectangle(cx + (sx + 1) * u, ground - top * u, (sw - 2) * u, u), lit);
                        }
                    Block(-52, 104, 6);
                }
                ui.Rect(new Rectangle(cx - u, ground - 86 * u, 2 * u, 46 * u), c);
                ui.Rect(new Rectangle(cx - u, ground - 92 * u, 2 * u, 6 * u), new Color(170, 170, 176)); // the spearhead
                ui.Banner(cx + 10 * u, ground - 84 * u, 18 * u, 14 * u, Style.Sides[Style.PaletteOf(Math.Max(0, C.Captain.Contract))], t * 0.8f);
                break;
            case "battlefield":
                for (int i = 0; i < 5; i++) ui.Disc(new Vector2(cx - 100 * u + i * 50 * u, ground), 16 * u, c);
                for (int i = 0; i < 9; i++) Block(-120 + i * 29, 3, 18 + i % 3 * 8);
                Block(70, 4, 70);
                ui.Banner(cx + 74 * u, ground - 70 * u, 26 * u, 34 * u, Style.Sides[1], t * 1.2f);
                break;
            case "mine":
                ui.Disc(new Vector2(cx, ground + 10 * u), 70 * u, c);
                Block(-20, 40, 40); // the timbered mouth, its dark shaft cut into it below
                ui.Rect(new Rectangle(cx - 12 * u, ground - 34 * u, 24 * u, 34 * u), new Color(10, 8, 8));
                ui.Rect(new Rectangle(cx + 60 * u, ground - 30 * u, 4 * u, 30 * u), c);
                ui.Disc(new Vector2(cx + 62 * u, ground - 36 * u), 8 * u, Kit.Ember * 0.3f);
                break;
            case "hermit":
                for (int k = 0; k < 30; k++) Block(-30 + k, 60 - 2 * k, 20 + k);
                ui.Rect(new Rectangle(cx - 5 * u, ground - 14 * u, 10 * u, 14 * u), Kit.Ember * 0.7f);
                for (int s = 0; s < 6; s++)
                {
                    float k = (t * 0.3f + s / 6f) % 1;
                    ui.Disc(new Vector2(cx + 10 * u + k * 20 * u, ground - 50 * u - k * p.Height * 0.4f), 3 * u + k * 6 * u, new Color(90, 90, 96) * (0.5f * (1 - k)));
                }
                break;
            case "watchtower":
                Block(-18, 36, 110);
                for (int i = 0; i < 4; i++) Block(-18 + i * 10, 6, 118);
                ui.Rect(new Rectangle(cx - 4 * u, ground - 80 * u, 8 * u, 12 * u), Color.Black * 0.6f);
                break;
            case "spring":
                ui.Disc(new Vector2(cx, ground), 46 * u, new Color(86, 124, 132));
                ui.Rect(new Rectangle(p.X, ground, p.Width, p.Bottom - ground), c);
                for (int i = 0; i < 8; i++) ui.Disc(new Vector2(cx - 56 * u + i * 16 * u, ground), 8 * u, c);
                break;
            default: // mill
                Block(-40, 60, 40);
                for (int k = 0; k < 30; k++) Block(-40 + k, 60 - 2 * k, 40 + k / 2);
                float spin = t * 0.8f;
                for (int i = 0; i < 8; i++)
                {
                    float a = spin + i * MathF.Tau / 8;
                    for (int k = 0; k < 30; k += 2) ui.Rect(new Rectangle(cx + 40 * u + (int)(MathF.Cos(a) * k * u), ground - 30 * u + (int)(MathF.Sin(a) * k * u), 2 * u, 2 * u), c);
                }
                break;
        }
    }
}
