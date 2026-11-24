using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// Title screen overlay: the Hall of Legends (docs/pre-demo/LEGENDS.md LG2). Browse every past sealed Legend's illuminated
/// scroll, toggle whether each Legend populates new worlds, or remove an entry.
sealed class LegendScreen
{
    public bool Open;
    int selected, scrollOffset, listScroll;
    string confirmDelete = "";
    float openedAt;

    public void Show()
    {
        Open = true;
        selected = 0;
        scrollOffset = 0;
        listScroll = 0;
        confirmDelete = "";
        openedAt = Ui.Now;
    }

    public void Close() { Open = false; confirmDelete = ""; }

    public void Draw(Ui ui, int wheel, KeyboardState kb, KeyboardState pkb)
    {
        if (!Open) return;
        bool Hit(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
        if (Hit(Keys.Escape)) { Close(); return; }

        var list = LegendArchive.Load();
        if (list.Count == 0) { Close(); return; }
        selected = Math.Clamp(selected, 0, list.Count - 1);

        ui.Dim(0.72f);
        int margin = ui.U(18), top = ui.U(14);
        ui.Ribbon(ui.W / 2, top, "HALL OF LEGENDS", Kit.Gilt, 2);

        int bodyY = top + ui.U(34), bodyH = ui.H - bodyY - ui.U(48);
        int listW = Math.Min(ui.U(310), ui.W / 3);
        var left = new Rectangle(margin, bodyY, listW, bodyH);
        var right = new Rectangle(left.Right + ui.U(14), bodyY, ui.W - left.Right - ui.U(14) - margin, bodyH);

        // Left: sealed Legends list
        var inner = ui.Plate(left, $"SEALED CHRONICLES ({list.Count})", Kit.Gilt);
        int rowH = ui.U(52), gap = ui.U(6);
        int vis = Math.Max(1, inner.Height / (rowH + gap));
        if (inner.Contains(ui.Mouse) && wheel != 0) listScroll -= Math.Sign(wheel);
        listScroll = Math.Clamp(listScroll, 0, Math.Max(0, list.Count - vis));

        for (int i = listScroll; i < Math.Min(list.Count, listScroll + vis); i++)
        {
            var leg = list[i];
            var row = new Rectangle(inner.X, inner.Y + (i - listScroll) * (rowH + gap), inner.Width, rowH);
            bool on = i == selected, hover = ui.Hover(row);
            ui.Rect(row, on ? Kit.Mul(Kit.Gilt, 0.25f) : hover ? Color.White * 0.06f : Color.Black * 0.3f);
            ui.Frame(row, on ? Kit.Gilt : Style.PanelEdge, ui.U(on ? 2 : 1));
            var colours = Style.Sides[Math.Clamp(leg.Palette, 0, Style.Sides.Length - 1)] with { Charge = (Emblem)Math.Clamp(leg.Arms, 0, 8) };
            ui.Shield(new Rectangle(row.X + ui.U(6), row.Y + ui.U(6), ui.U(30), ui.U(38)), colours);
            int tx = row.X + ui.U(44), tw = row.Right - tx - ui.U(8);
            ui.TextFit(leg.FullName.ToUpperInvariant(), tx, row.Y + ui.U(6), tw, on ? Kit.Gilt : Style.Text, 1);
            ui.TextFit($"{LegendScroll.FateBadge(leg.Fate)}  ·  {leg.Verses.Count}V", tx, row.Y + ui.U(19), tw, Style.Dim, 1);
            ui.TextFit(leg.ActiveInWorld ? "ACTIVE IN NEW WORLDS" : "ARCHIVED ONLY", tx, row.Y + ui.U(32), tw, leg.ActiveInWorld ? Style.Ok : Style.Dim * 0.7f, 1);
            if (ui.Clicked(row)) { selected = i; scrollOffset = 0; confirmDelete = ""; openedAt = Ui.Now; Ui.OnClick?.Invoke(); }
        }

        // Right: Illuminated Scroll preview of the selected Legend
        var cur = list[selected];
        float open = Ui.Instant ? 1 : Ease.Out(Math.Clamp((Ui.Now - openedAt) / 0.45f, 0, 1));
        var data = new LegendScroll.Data(
            cur.FullName, cur.Rank, cur.CompanyName, cur.Homeland, cur.Fate,
            cur.Days, cur.Battles, cur.Kills, cur.Palette, cur.Arms, cur.Portrait,
            cur.Verses, null, cur.Relic
        );
        LegendScroll.Draw(ui, right, data, open, ref scrollOffset, wheel);

        // Footer bar
        int fy = ui.H - ui.U(38), fh = ui.U(26);
        if (ui.Gilded(new Rectangle(margin, fy, ui.U(130), fh), "BACK", "ESC")) Close();
        int toggleW = ui.U(230);
        if (ui.Button(new Rectangle(right.X, fy, toggleW, fh), cur.ActiveInWorld ? "WORLD IMPACT: ON" : "WORLD IMPACT: OFF", true, "", cur.ActiveInWorld))
            LegendArchive.SetActive(cur.Id, !cur.ActiveInWorld);
        int delW = ui.U(150);
        if (ui.Button(new Rectangle(right.Right - delW, fy, delW, fh), confirmDelete == cur.Id ? "SURE? DELETE" : "DELETE LEGEND"))
        {
            if (confirmDelete != cur.Id) confirmDelete = cur.Id;
            else { LegendArchive.Delete(cur.Id); confirmDelete = ""; }
        }
    }
}
