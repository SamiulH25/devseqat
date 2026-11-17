using Microsoft.Xna.Framework;

namespace Ironfield;

/// The quests screen (J, docs/PLAYTEST.md PT3): what you hold, what is offered on the boards you have seen, and what you
/// finished or failed. SHOW ME looks at the place on the map; PIN puts one quest on the HUD.
sealed partial class MapScreen
{
    int questTab;
    /// A place SHOW ME marked on the map, until another is shown.
    Vector2? questMark;

    void QuestsPanel()
    {
        var r = Modal(900, 560, "QUESTS");
        var all = C.QuestList();
        var groups = new[] { all.Where(q => q.State == "ACTIVE").ToList(), all.Where(q => q.State == "AVAILABLE").ToList(), all.Where(q => q.State is "DONE" or "FAILED").ToList() };
        var tabs = new[] { $"YOURS ({groups[0].Count})", $"ON OFFER ({groups[1].Count})", "DONE AND FAILED" };
        int pick = ui.Tabs(new Rectangle(r.X + ui.U(20), r.Y + ui.U(24), ui.U(480), ui.U(22)), tabs, questTab);
        if (pick >= 0) { questTab = pick; note = ""; }
        var list = groups[questTab];
        int x = r.X + ui.U(20), y = r.Y + ui.U(58), w = r.Width - ui.U(40), rh = ui.U(46);
        if (list.Count == 0)
            ui.Text(questTab switch
            {
                0 => "YOU HOLD NO QUESTS. A TOWN'S STEWARD, A VILLAGE'S ELDERS AND THE LORDS YOU TALK TO HAVE WORK.",
                1 => "NO OFFERS YET. VISIT A TOWN'S STEWARD OR A VILLAGE'S ELDERS TO SEE THEIR WORK.",
                _ => "NOTHING FINISHED YET.",
            }, x, y + ui.U(4), Style.Dim, 1);
        foreach (var q in list)
        {
            if (y + rh > r.Bottom - ui.U(64)) { ui.Text($"+{list.Count - list.IndexOf(q)} MORE", x, y, Style.Dim, 1); break; }
            QuestRow(q, new Rectangle(x, y, w, rh));
            y += rh + ui.U(6);
        }
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.QuestsScreen))) panel = CameFrom(PanelKind.Quests);
    }

    /// One quest: what to do and who asked on the left; reward, time left and the buttons on the right.
    void QuestRow(QuestView q, Rectangle box)
    {
        bool pinned = q.State == "ACTIVE" && C.S.PinnedQuest == q.Id;
        ui.Rect(box, pinned ? Kit.Gilt * 0.10f : Color.White * 0.04f);
        ui.Frame(box, pinned ? Kit.Gilt : Color.White * 0.10f, ui.U(1));
        int bx = box.Right - ui.U(8), by = box.Y + ui.U(12);
        // The buttons, right to left.
        if (q.State == "ACTIVE")
        {
            var pb = new Rectangle(bx - ui.U(70), by, ui.U(70), ui.U(22));
            if (ui.Button(pb, pinned ? "PINNED" : "PIN", true, "", pinned)) C.S.PinnedQuest = pinned ? -1 : q.Id;
            if (ui.Hover(pb)) note = pinned ? "ON THE MAP'S CORNER. CLICK TO TAKE IT OFF." : "PUT THIS QUEST IN THE MAP'S CORNER.";
            bx = pb.X - ui.U(6);
        }
        if (q.Place is Vector2 at)
        {
            var sb = new Rectangle(bx - ui.U(90), by, ui.U(90), ui.U(22));
            if (ui.Button(sb, "SHOW ME")) { questMark = at; focus = at; follow = false; panel = PanelKind.None; }
            if (ui.Hover(sb)) note = $"LOOK AT {q.Where.ToUpperInvariant()} ON THE MAP.";
            bx = sb.X - ui.U(6);
        }
        // Reward and time, before the buttons.
        int ix = bx - ui.U(150);
        if (q.State is "DONE" or "FAILED")
        {
            ui.Chip(ix, by + ui.U(4), q.State, q.State == "DONE" ? Style.Ok : Style.Bad);
            ui.TextR($"DAY {(int)(q.Deadline / 24) + 1}", bx, by + ui.U(7), Style.Dim, 1);
        }
        else
        {
            if (q.Reward > 0) { ui.Icon("coin", ix, by + ui.U(1), Kit.Gilt, 1); ui.Text($"{q.Reward:0}", ix + ui.U(12), by + ui.U(1), Kit.Gilt, 1); }
            float days = (q.Deadline - (float)C.S.Hours) / 24;
            ui.Icon("clock", ix, by + ui.U(13), days < 2 ? Style.Warn : Style.Dim, 1);
            ui.Text($"{days:0.0} DAYS", ix + ui.U(12), by + ui.U(13), days < 2 ? Style.Warn : Style.Dim, 1);
        }
        // What to do, and who asked and where.
        int tx = box.X + ui.U(10), tw = ix - tx - ui.U(10);
        ui.TextFit(q.Title.ToUpperInvariant(), tx, box.Y + ui.U(10), tw, Style.Text, 1);
        string sub = q.Giver.ToUpperInvariant() + (q.Where != "" ? " . " + q.Where.ToUpperInvariant() : "") + (q.MoreSteps ? " . MORE TO DO AFTER" : "");
        ui.TextFit(sub, tx, box.Y + ui.U(26), tw, Style.Dim, 1);
    }

    /// The pinned quest's place (or the last one SHOW ME marked) as a gold marker on the map.
    void QuestMark()
    {
        Vector2? at = questMark;
        if (C.S.Contracts.FirstOrDefault(c => c.Id == C.S.PinnedQuest) is Contract pin && C.PlaceOf(pin).Place is Vector2 p) at ??= p;
        if (at is not Vector2 w || !OnScreen(w, out var sp, out _)) return;
        float bob = MathF.Sin(Ui.Now * 3) * ui.U(2);
        int s = ui.U(7), cx = (int)sp.X, cy = (int)(sp.Y - ui.U(26) + bob);
        for (int i = 0; i <= s; i++) // a gold diamond with a dark edge, over the place
        {
            ui.Rect(new Rectangle(cx - i - ui.U(1), cy - s + i - ui.U(1), 2 * i + ui.U(2), ui.U(1) + 2), Color.Black * 0.7f);
            ui.Rect(new Rectangle(cx - i, cy - s + i, 2 * i + 1, ui.U(1)), Kit.Gilt);
            ui.Rect(new Rectangle(cx - i, cy + s - i, 2 * i + 1, ui.U(1)), Kit.Gilt);
        }
        ui.Rect(new Rectangle(cx - ui.U(1), cy + s, ui.U(2), ui.U(16) - (int)bob), Kit.Gilt * 0.8f);
        ui.TextC("QUEST", cx, cy - s - ui.U(12), Kit.Gilt, 1);
    }
}
