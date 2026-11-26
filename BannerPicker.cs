using Microsoft.Xna.Framework;

namespace Ironfield;

/// Shared part-by-part heraldic banner designer UI: live swaying main banner + sample generated company banners,
/// and three focused workshop tabs (FIELD: primary/secondary colours & 10 divisions, CHARGE: charge colour & 19 emblems,
/// TRIM & SHAPE: 4 border trims & 4 banner cuts). Used by both the Character Creator (BANNER step) and the Camp menu.
static class BannerPicker
{
    static int tab; // 0 = FIELD, 1 = CHARGE, 2 = TRIM & CUT
    static readonly string[] TabNames = { "1. FIELD", "2. CHARGE", "3. TRIM / CUT" };

    public static bool Draw(Ui ui, Rectangle r, int basePalette,
        ref int arms, ref int field, ref int trim,
        ref int division, ref int field2, ref int border, ref int cut,
        float time, Random? dice = null)
    {
        int U(float v) => ui.U(v);
        bool changed = false;
        int curArms = arms >= 0 && arms < Style.EmblemCount
            ? arms
            : (int)Style.Sides[Math.Clamp(basePalette, 0, Style.Sides.Length - 1)].Charge;
        var hd = Style.BannerOf(basePalette, curArms, field, trim, division, field2, border, cut);

        // Left preview column (~31% width, clamped cleanly).
        int prevW = Math.Clamp((int)(r.Width * 0.31f), U(144), U(216));
        var prev = new Rectangle(r.X, r.Y, prevW, r.Height);
        var ctrl = new Rectangle(prev.Right + U(12), r.Y, r.Right - (prev.Right + U(12)), r.Height);

        int btnAreaH = U(54);
        var stage = new Rectangle(prev.X, prev.Y, prev.Width, Math.Max(U(130), prev.Height - btnAreaH - U(8)));
        ui.VGrad(stage, Kit.Mul(hd.Cloth, 0.32f), new Color(16, 15, 20), 2);
        ui.Frame(stage, Kit.Gilt * 0.7f, U(1));
        ui.Frame(Kit.Inset(stage, U(3)), Color.Black * 0.45f, U(1));

        // Upper preview: large swaying main banner.
        int sampleH = U(46);
        int banW = Math.Clamp((int)(stage.Width * 0.40f), U(36), U(64));
        int banH = Math.Min((int)(banW * 1.80f), Math.Max(U(46), stage.Height - sampleH - U(48)));
        ui.Banner(stage.Center.X, stage.Y + U(8), banW, banH, hd, time * 1.35f);

        // Summary labels + live preview of 3 generated company banners derived from this main banner.
        int infoY = stage.Bottom - sampleH - U(28);
        ui.Rect(new Rectangle(stage.X + U(6), infoY - U(3), stage.Width - U(12), Math.Max(1, U(1))), Kit.Gilt * 0.32f);
        string divName = Style.DivisionName((BannerDivision)Math.Clamp(division, 0, Style.BannerDivisions.Length - 1));
        string chName = Style.EmblemName((Emblem)curArms);
        ui.TextFitC(division > 0 ? $"{divName} . {chName}" : chName, stage.Center.X, infoY, stage.Width - U(12), Kit.Gilt, 1);

        int sampY = stage.Bottom - sampleH - U(14);
        ui.TextFitC("COMPANY BANNERS", stage.Center.X, sampY, stage.Width - U(12), Style.Dim, 1);
        (int id, string role, string tag)[] samples = { (101, "infantry", "FOOT"), (102, "cavalry", "HORSE"), (103, "archers", "BOW") };
        int sw = (stage.Width - U(20)) / 3;
        for (int i = 0; i < samples.Length; i++)
        {
            var sb = new Rectangle(stage.X + U(6) + i * (sw + U(4)), sampY + U(11), sw, sampleH - U(12));
            var chd = Style.CompanyBanner(hd, samples[i].id, samples[i].role, flagship: i == 0);
            int mw = Math.Min(U(22), sb.Width - U(4)), mh = Math.Min(U(26), sb.Height - U(10));
            ui.MiniBanner(new Rectangle(sb.Center.X - mw / 2, sb.Y, mw, mh), chd);
            ui.TextFitC(samples[i].tag, sb.Center.X, sb.Y + mh + U(2), sb.Width, Style.Dim, 1);
        }

        int by = stage.Bottom + U(6);
        if (ui.Button(new Rectangle(prev.X, by, prev.Width, U(24)), "RANDOMISE"))
        {
            var rng = dice ?? Random.Shared;
            division = rng.Next(Style.BannerDivisions.Length);
            field = rng.Next(Style.BannerFields.Length);
            field2 = division > 0 ? rng.Next(Style.BannerFields.Length) : -1;
            if (field2 == field) field2 = (field + 3) % Style.BannerFields.Length;
            arms = (int)Style.BannerCharges[rng.Next(1, Style.BannerCharges.Length)];
            border = rng.Next(Style.BannerBorders.Length);
            cut = rng.Next(Style.BannerCuts.Length);
            // Pick contrasting trim colour so charge reads crisply.
            int t = rng.Next(Style.BannerTrims.Length);
            var fc = Style.BannerFields[field].Cloth;
            float fLum = fc.R * 0.3f + fc.G * 0.59f + fc.B * 0.11f;
            for (int tries = 0; tries < 6; tries++)
            {
                var tc = Style.BannerTrims[t].Trim;
                float tLum = tc.R * 0.3f + tc.G * 0.59f + tc.B * 0.11f;
                if (MathF.Abs(fLum - tLum) > 65f) break;
                t = rng.Next(Style.BannerTrims.Length);
            }
            trim = t;
            changed = true;
        }
        bool customAny = field >= 0 || field2 >= 0 || trim >= 0 || division != 0 || border != 0 || cut != 0;
        if (ui.Button(new Rectangle(prev.X, by + U(28), prev.Width, U(24)), "RESET TO REALM", customAny))
        {
            field = -1; field2 = -1; trim = -1; division = 0; border = 0; cut = 0;
            changed = true;
        }

        // Right column: 3 part-by-part workshop tabs.
        int x = ctrl.X, y = ctrl.Y, w = ctrl.Width;
        int tabH = U(24), tabGap = U(4), tabW = (w - tabGap * 2) / 3;
        for (int i = 0; i < 3; i++)
        {
            var tb = new Rectangle(x + i * (tabW + tabGap), y, tabW, tabH);
            bool on = tab == i, hov = ui.Hover(tb);
            ui.VGrad(tb, on ? new Color(72, 62, 36) : hov ? new Color(46, 48, 56) : new Color(30, 32, 40), Kit.IronDark, 1);
            ui.Frame(tb, on ? Kit.Gilt : hov ? Style.Text * 0.6f : Kit.IronDark, U(on ? 2 : 1));
            ui.TextFitC(TabNames[i], tb.Center.X, tb.Y + U(7), tb.Width - U(6), on ? Kit.Gilt : Style.Text, 1);
            if (ui.Clicked(tb)) { tab = i; Ui.OnClick?.Invoke(); }
        }
        y += tabH + U(10);

        bool tight = ctrl.Height < U(320);
        int swSz = U(tight ? 16 : 20);

        if (tab == 0)
        {
            // TAB 1: FIELD — Primary colour, Secondary colour, and 10 field divisions.
            string f1Label = field >= 0 && field < Style.BannerFields.Length ? Style.BannerFields[field].Name : "REALM";
            ui.Text("PRIMARY FIELD", x, y, Kit.Gilt, 1);
            ui.TextR(f1Label, x + w, y, Style.Dim, 1);
            y += U(11);
            y = SwatchRow(ui, x, y, w, swSz, Style.BannerFields.Length, i => Style.BannerFields[i].Cloth, ref field, ref changed) + U(tight ? 7 : 10);

            string f2Label = field2 >= 0 && field2 < Style.BannerFields.Length ? Style.BannerFields[field2].Name : "DARK SHADE";
            ui.Text("SECONDARY FIELD", x, y, Kit.Gilt, 1);
            ui.TextR(f2Label, x + w, y, Style.Dim, 1);
            y += U(11);
            y = SwatchRow(ui, x, y, w, swSz, Style.BannerFields.Length, i => Style.BannerFields[i].Cloth, ref field2, ref changed, allowAuto: true, autoCol: hd.ClothDark) + U(tight ? 8 : 12);

            var divs = Style.BannerDivisions;
            ui.Text($"FIELD DIVISION ({divs.Length})", x, y, Kit.Gilt, 1);
            string? hovDiv = null;
            int gridY = y + U(12);
            int cols = 5, rows = (divs.Length + cols - 1) / cols, gap = U(6);
            int availH = Math.Max(U(72), ctrl.Bottom - gridY);
            int cellW = (w - gap * (cols - 1)) / cols;
            int cellH = (availH - gap * (rows - 1)) / rows;
            int shW = Math.Min(cellW, (int)(cellH * 0.84f));
            int shH = Math.Min(cellH, (int)(shW * 1.19f));
            int gx0 = x + Math.Max(0, (w - (cols * shW + (cols - 1) * gap)) / 2);

            for (int i = 0; i < divs.Length; i++)
            {
                int col = i % cols, row = i / cols;
                var box = new Rectangle(gx0 + col * (shW + gap), gridY + row * (shH + gap), shW, shH);
                bool on = division == i, hov = ui.Hover(box);
                if (hov) hovDiv = Style.DivisionName(divs[i]);
                if (on || hov)
                {
                    var hi = Kit.Inset(box, -U(2));
                    ui.Rect(hi, Color.Black * 0.5f);
                    ui.Frame(hi, on ? Kit.Gilt : Kit.Gilt * 0.55f, U(on ? 2 : 1));
                }
                ui.Shield(box, hd with { Division = divs[i], Charge = Emblem.None });
                if (ui.Clicked(box)) { division = i; changed = true; Ui.OnClick?.Invoke(); }
            }
            ui.TextR(hovDiv ?? divName, x + w, y, hovDiv != null ? Style.Text : Style.Dim, 1);
        }
        else if (tab == 1)
        {
            // TAB 2: CHARGE — Charge colour + 19 selectable heraldic charges (including NONE).
            string trimLabel = trim >= 0 && trim < Style.BannerTrims.Length ? Style.BannerTrims[trim].Name : "REALM";
            ui.Text("CHARGE / TRIM COLOUR", x, y, Kit.Gilt, 1);
            ui.TextR(trimLabel, x + w, y, Style.Dim, 1);
            y += U(11);
            y = SwatchRow(ui, x, y, w, swSz, Style.BannerTrims.Length, i => Style.BannerTrims[i].Trim, ref trim, ref changed) + U(tight ? 8 : 12);

            var charges = Style.BannerCharges;
            ui.Text($"HERALDIC CHARGE ({charges.Length})", x, y, Kit.Gilt, 1);
            string? hovCh = null;
            int gridY = y + U(12);
            int cols = 7, rows = (charges.Length + cols - 1) / cols, gap = U(5);
            int availH = Math.Max(U(80), ctrl.Bottom - gridY);
            int cellW = (w - gap * (cols - 1)) / cols;
            int cellH = (availH - gap * (rows - 1)) / rows;
            int shW = Math.Min(cellW, (int)(cellH * 0.84f));
            int shH = Math.Min(cellH, (int)(shW * 1.19f));
            int gx0 = x + Math.Max(0, (w - (cols * shW + (cols - 1) * gap)) / 2);

            for (int i = 0; i < charges.Length; i++)
            {
                int col = i % cols, row = i / cols;
                var box = new Rectangle(gx0 + col * (shW + gap), gridY + row * (shH + gap), shW, shH);
                bool on = curArms == (int)charges[i], hov = ui.Hover(box);
                if (hov) hovCh = Style.EmblemName(charges[i]);
                if (on || hov)
                {
                    var hi = Kit.Inset(box, -U(2));
                    ui.Rect(hi, Color.Black * 0.5f);
                    ui.Frame(hi, on ? Kit.Gilt : Kit.Gilt * 0.55f, U(on ? 2 : 1));
                }
                ui.Shield(box, hd with { Charge = charges[i] });
                if (ui.Clicked(box)) { arms = (int)charges[i]; changed = true; Ui.OnClick?.Invoke(); }
            }
            ui.TextR(hovCh ?? chName, x + w, y, hovCh != null ? Style.Text : Style.Dim, 1);
        }
        else
        {
            // TAB 3: TRIM & CUT — 4 Border styles + 4 Banner silhouettes.
            var borders = Style.BannerBorders;
            ui.Text("BORDER TRIM", x, y, Kit.Gilt, 1);
            ui.TextR(Style.BorderName((BannerBorder)Math.Clamp(border, 0, borders.Length - 1)), x + w, y, Style.Dim, 1);
            y += U(13);
            int bGap = U(6), bW = (w - bGap * (borders.Length - 1)) / borders.Length;
            int bH = Math.Min(U(94), Math.Max(U(64), (ctrl.Bottom - y - U(28)) / 2));
            for (int i = 0; i < borders.Length; i++)
            {
                var box = new Rectangle(x + i * (bW + bGap), y, bW, bH);
                bool on = border == i, hov = ui.Hover(box);
                ui.VGrad(box, on ? new Color(56, 50, 34) : new Color(28, 30, 38), Kit.IronDark, 1);
                ui.Frame(box, on ? Kit.Gilt : hov ? Style.Text * 0.6f : Color.Black * 0.6f, U(on ? 2 : 1));
                int shH = Math.Max(U(24), bH - U(22)), shW = (int)(shH * 0.84f);
                ui.Shield(new Rectangle(box.Center.X - shW / 2, box.Y + U(5), shW, shH), hd with { Border = borders[i] });
                ui.TextFitC(Style.BorderName(borders[i]), box.Center.X, box.Bottom - U(13), box.Width - U(4), on ? Kit.Gilt : Style.Text, 1);
                if (ui.Clicked(box)) { border = i; changed = true; Ui.OnClick?.Invoke(); }
            }
            y += bH + U(14);

            var cuts = Style.BannerCuts;
            ui.Text("BANNER CUT", x, y, Kit.Gilt, 1);
            ui.TextR(Style.CutName((BannerCut)Math.Clamp(cut, 0, cuts.Length - 1)), x + w, y, Style.Dim, 1);
            y += U(13);
            int cW = (w - bGap * (cuts.Length - 1)) / cuts.Length;
            int cH = Math.Max(U(68), ctrl.Bottom - y);
            for (int i = 0; i < cuts.Length; i++)
            {
                var box = new Rectangle(x + i * (cW + bGap), y, cW, cH);
                bool on = cut == i, hov = ui.Hover(box);
                ui.VGrad(box, on ? new Color(56, 50, 34) : new Color(28, 30, 38), Kit.IronDark, 1);
                ui.Frame(box, on ? Kit.Gilt : hov ? Style.Text * 0.6f : Color.Black * 0.6f, U(on ? 2 : 1));
                int bh = Math.Max(U(32), cH - U(26)), bw = Math.Min(cW - U(16), (int)(bh * 0.60f));
                ui.Banner(box.Center.X, box.Y + U(6), bw, bh, hd with { Cut = cuts[i] }, time * 1.1f + i);
                ui.TextFitC(Style.CutName(cuts[i]), box.Center.X, box.Bottom - U(13), box.Width - U(4), on ? Kit.Gilt : Style.Text, 1);
                if (ui.Clicked(box)) { cut = i; changed = true; Ui.OnClick?.Invoke(); }
            }
        }
        return changed;
    }

    static int SwatchRow(Ui ui, int x, int y, int w, int swSz, int count, Func<int, Color> colOf, ref int val, ref bool changed,
        bool allowAuto = false, Color autoCol = default)
    {
        int total = count + (allowAuto ? 1 : 0);
        int gap = Math.Max(ui.U(3), Math.Min(ui.U(6), (w - total * swSz) / Math.Max(1, total - 1)));
        int sw = Math.Min(swSz, (w - gap * (total - 1)) / total);
        int idx = 0;
        if (allowAuto)
        {
            var box = new Rectangle(x, y, sw, sw);
            ui.Rect(box, autoCol);
            bool on = val < 0;
            ui.Frame(box, on ? Kit.Gilt : ui.Hover(box) ? Style.Text : Color.Black * 0.7f, ui.U(on ? 2 : 1));
            ui.TextC("A", box.Center.X, box.Y + Math.Max(1, (sw - ui.U(7)) / 2), Color.White * 0.85f, 1);
            if (ui.Clicked(box)) { val = -1; changed = true; Ui.OnClick?.Invoke(); }
            idx = 1;
        }
        for (int i = 0; i < count; i++)
        {
            var box = new Rectangle(x + (i + idx) * (sw + gap), y, sw, sw);
            ui.Rect(box, colOf(i));
            bool on = val == i;
            ui.Frame(box, on ? Kit.Gilt : ui.Hover(box) ? Style.Text : Color.Black * 0.7f, ui.U(on ? 2 : 1));
            if (ui.Clicked(box)) { val = i; changed = true; Ui.OnClick?.Invoke(); }
        }
        return y + sw;
    }
}
