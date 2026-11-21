using Microsoft.Xna.Framework;

namespace Ironfield;

/// The book of the fallen (docs/pre-demo/COMPANY.md C7): every named man lost, his face greyed, under the battle he fell in or
/// his company; what he was known for and whom he left behind. Captains who died in the field come first. Opened from THE
/// LEGEND and from a company's roll.
partial class MapScreen
{
    bool bookByCompany;
    int bookPage;
    PanelKind bookFrom = PanelKind.None;

    void OpenBook(PanelKind from) { bookFrom = from; bookPage = 0; panel = PanelKind.Fallen; note = ""; }

    /// One line of the book: a heading, a dead captain, or a row of up to four men.
    abstract record BookLine;
    sealed record BookHead(string Text, int Count) : BookLine;
    sealed record BookCaptain(DeadCaptain Captain) : BookLine;
    sealed record BookRow(List<Soldier> Men) : BookLine;

    void BookPanel()
    {
        var r = Modal(980, 640, "THE BOOK OF THE FALLEN");
        int x = r.X + ui.U(24), w = r.Width - ui.U(48), y = r.Y + ui.U(34);
        var groups = C.BookOfTheFallen(bookByCompany);
        int count = groups.Sum(g => g.Men.Count);
        ui.Text(count == 0 && C.S.DeadCaptains.Count == 0 ? "NO NAMED MAN OF YOURS HAS FALLEN. MAY IT STAY SO." : $"{Campaign.N(count, "NAMED MAN").ToUpperInvariant()} LOST" + (C.S.DeadCaptains.Count > 0 ? $", AND {Campaign.N(C.S.DeadCaptains.Count, "CAPTAIN").ToUpperInvariant()}" : ""), x, y + ui.U(4), Style.Text, 1);
        if (ui.Button(new Rectangle(r.Right - ui.U(24) - ui.U(260), y, ui.U(126), ui.U(20)), "BY BATTLE", true, "", !bookByCompany)) { bookByCompany = false; bookPage = 0; }
        if (ui.Button(new Rectangle(r.Right - ui.U(24) - ui.U(128), y, ui.U(128), ui.U(20)), "BY COMPANY", true, "", bookByCompany)) { bookByCompany = true; bookPage = 0; }
        y += ui.U(30);
        // Lay the book out as lines, then show the page of them that fits.
        const int perRow = 3;
        var lines = new List<BookLine>();
        foreach (var dc in C.S.DeadCaptains.AsEnumerable().Reverse()) lines.Add(new BookCaptain(dc));
        foreach (var (title, men) in groups)
        {
            lines.Add(new BookHead(title, men.Count));
            for (int i = 0; i < men.Count; i += perRow) lines.Add(new BookRow(men.Skip(i).Take(perRow).ToList()));
        }
        int Height(BookLine l) => l switch { BookHead => ui.U(22), BookCaptain => ui.U(40), _ => ui.U(84) };
        int room = r.Bottom - ui.U(56) - y;
        var pages = new List<int> { 0 };
        for (int i = 0, h = 0; i < lines.Count; i++)
        {
            if (h + Height(lines[i]) > room && h > 0) { pages.Add(i); h = 0; }
            h += Height(lines[i]);
        }
        bookPage = Math.Clamp(bookPage, 0, pages.Count - 1);
        int end = bookPage + 1 < pages.Count ? pages[bookPage + 1] : lines.Count;
        for (int i = pages[bookPage]; i < end; i++)
        {
            switch (lines[i])
            {
                case BookHead hd:
                    ui.Text(hd.Text.ToUpperInvariant(), x, y + ui.U(6), Kit.Gilt, 1);
                    ui.TextR($"{hd.Count} FALLEN", x + w, y + ui.U(6), Style.Dim, 1);
                    ui.Divider(x, y + ui.U(18), w, Kit.Gilt * 0.35f);
                    break;
                case BookCaptain bc:
                    ui.Rect(new Rectangle(x, y, w, ui.U(34)), Color.Black * 0.3f);
                    ui.Frame(new Rectangle(x, y, w, ui.U(34)), Kit.Mul(Kit.Gilt, 0.6f), ui.U(1));
                    ui.Icon("crown", x + ui.U(10), y + ui.U(12), Kit.Gilt, 1);
                    ui.Text($"{bc.Captain.Name.ToUpperInvariant()}, YOUR CAPTAIN", x + ui.U(28), y + ui.U(6), Kit.Gilt, 1);
                    ui.Text($"FELL AT {bc.Captain.Field.ToUpperInvariant()} ON DAY {bc.Captain.Day}" + (bc.Captain.Age > 0 ? $", AGED {bc.Captain.Age}." : "."), x + ui.U(28), y + ui.U(19), Style.Dim, 1);
                    break;
                case BookRow row:
                    int cw = (w - ui.U(8) * (perRow - 1)) / perRow;
                    for (int j = 0; j < row.Men.Count; j++) FallenCard(row.Men[j], new Rectangle(x + j * (cw + ui.U(8)), y + ui.U(4), cw, ui.U(76)));
                    break;
            }
            y += Height(lines[i]);
        }
        if (pages.Count > 1)
        {
            int by = r.Bottom - ui.U(42);
            if (ui.Button(new Rectangle(x, by, ui.U(90), ui.U(26)), "< BACK", bookPage > 0)) bookPage--;
            ui.TextC($"PAGE {bookPage + 1} OF {pages.Count}", x + ui.U(150), by + ui.U(9), Style.Dim, 1);
            if (ui.Button(new Rectangle(x + ui.U(210), by, ui.U(90), ui.U(26)), "ON >", bookPage < pages.Count - 1)) bookPage++;
        }
        if (ui.Button(new Rectangle(r.Right - ui.U(24) - ui.U(180), r.Bottom - ui.U(42), ui.U(180), ui.U(30)), "CLOSE THE BOOK", note: ctl.Name(Act.Menu))) panel = bookFrom;
    }

    /// One fallen man: his face greyed, name, troop and record, what he was known for, whom he left behind.
    void FallenCard(Soldier m, Rectangle b)
    {
        ui.Rect(b, Color.White * 0.035f);
        var k = C.Player.Companies.FirstOrDefault(c => c.Id == m.Company);
        var fb = new Rectangle(b.X + ui.U(4), b.Y + ui.U(4), ui.U(52), ui.U(64));
        ui.Rect(new Rectangle(fb.X - 1, fb.Y - 1, fb.Width + 2, fb.Height + 2), Campaign.OfBanner(m) ? Kit.Mul(Kit.Gilt, 0.7f) : Kit.IronDark);
        ui.FaceHead(PortraitPainter.Of(m, k) with { Mood = 1 }, fb, new Color(120, 122, 128)); // greyed: he is gone
        int tx = fb.Right + ui.U(8), tw = b.Right - tx - ui.U(4);
        ui.TextFit(m.Name.ToUpperInvariant(), tx, b.Y + ui.U(5), tw, m.Fate == "fell" ? Style.Text : Style.Dim, 1);
        string troop = Troops.Has(m.Troop) ? Troops.Get(m.Troop).Name.ToUpperInvariant() : "";
        ui.TextFit($"{troop}. {Campaign.N(m.Battles, "BATTLE").ToUpperInvariant()}, {Campaign.N(m.Kills, "KILL").ToUpperInvariant()}" + (m.Fate == "missing" ? ". MISSING" : ""), tx, b.Y + ui.U(17), tw, m.Fate == "missing" ? Style.Warn : Style.Dim, 1);
        if (m.Deeds.Count > 0) // his deed, on two lines if it needs them
        {
            var deed = ui.Wrap(m.Deeds[^1].ToUpperInvariant(), tw);
            for (int i = 0; i < Math.Min(2, deed.Count); i++) ui.TextFit(deed[i], tx, b.Y + ui.U(31 + i * 12), tw, Kit.Mul(Kit.Parchment, 0.85f), 1);
        }
        var left = m.Ties.Select(t => (C.LivingMan(t.Other)?.Name ?? C.S.Fallen.FirstOrDefault(f => f.Id == t.Other)?.Name, Alive: C.LivingMan(t.Other) != null)).Where(t => t.Item1 != null).ToList();
        if (left.Count > 0) ui.TextFit("LEFT BEHIND: " + string.Join(", ", left.Select(t => t.Item1!.ToUpperInvariant() + (t.Alive ? "" : " (GONE)"))), tx, b.Y + ui.U(59), tw, new Color(156, 190, 230), 1);
        else if (bookByCompany && m.Field != "") ui.TextFit("AT " + m.Field.ToUpperInvariant() + $", DAY {m.DiedDay}", tx, b.Y + ui.U(59), tw, Style.Dim, 1);
    }
}
