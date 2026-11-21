using Microsoft.Xna.Framework;

namespace Ironfield;

/// The quests screen (J, docs/archive/PLAYTEST.md PT3): what you hold, what is offered on the boards you have seen, and what you
/// finished or failed. SHOW ME looks at the place on the map; PIN puts one quest on the HUD.
sealed partial class MapScreen
{
    int questTab;
    /// A place SHOW ME marked on the map, until another is shown.
    Vector2? questMark;

    void QuestsPanel()
    {
        var all = C.QuestList();
        var groups = new[] { all.Where(q => q.State == "ACTIVE").ToList(), all.Where(q => q.State == "AVAILABLE").ToList(), all.Where(q => q.State is "DONE" or "FAILED").ToList() };
        // As tall as the open tab needs, within the screen.
        var r = Modal(900, Math.Clamp(58 + Math.Max(1, groups[questTab].Count) * 52 + 96, 220, 640), "QUESTS");
        var tabs = new[] { $"YOURS ({groups[0].Count})", $"ON OFFER ({groups[1].Count})", "DONE AND FAILED" };
        int pick = ui.Tabs(new Rectangle(r.X + ui.U(20), r.Y + ui.U(24), r.Width - ui.U(40), ui.U(22)), tabs, questTab);
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
            ui.Text(days < 2 ? $"{days:0.0} DAYS" : $"{days:0} DAYS", ix + ui.U(12), by + ui.U(13), days < 2 ? Style.Warn : Style.Text, 1);
        }
        // What to do, and who asked and where.
        int tx = box.X + ui.U(10), tw = ix - tx - ui.U(10);
        ui.TextFit(q.Title.ToUpperInvariant(), tx, box.Y + ui.U(10), tw, Style.Text, 1);
        // Who asked, and where (left out when the task already names the place).
        bool named = q.Where != "" && q.Title.Contains(q.Where, StringComparison.OrdinalIgnoreCase);
        string sub = q.Giver.ToUpperInvariant() + (q.Where != "" && !named ? " . AT " + q.Where.ToUpperInvariant() : "") + (q.MoreSteps ? " . MORE TO DO AFTER" : "");
        ui.TextFit(sub, tx, box.Y + ui.U(26), tw, Color.Lerp(Style.Dim, Style.Text, 0.45f), 1);
    }

    /// The pinned quest's place (or the last one SHOW ME marked) as a gold marker on the map.
    void QuestMark()
    {
        Vector2? at = questMark;
        if (C.S.Contracts.FirstOrDefault(c => c.Id == C.S.PinnedQuest) is Contract pin && C.PlaceOf(pin).Place is Vector2 p) at ??= p;
        if (at is not Vector2 w || !OnScreen(w, out var sp, out _)) return;
        // A gold ring on the ground round the place, breathing, with QUEST under it: the labels sit above a place, so below is clear.
        float k = 1 + 0.12f * MathF.Sin(Ui.Now * 3);
        float rx = ui.U(18) * k, ry = ui.U(8) * k;
        for (int i = 0; i < 32; i++)
        {
            float a2 = i / 32f * MathF.Tau;
            int px = (int)(sp.X + MathF.Cos(a2) * rx), py = (int)(sp.Y + MathF.Sin(a2) * ry);
            ui.Rect(new Rectangle(px - ui.U(2), py - ui.U(2), ui.U(5), ui.U(5)), Color.Black * 0.7f);
            ui.Rect(new Rectangle(px - ui.U(1), py - ui.U(1), ui.U(3), ui.U(3)), Kit.Gilt);
        }
        var lr = new Rectangle((int)sp.X - ui.TW("QUEST", 1) / 2 - ui.U(4), (int)(sp.Y + ry + ui.U(4)), ui.TW("QUEST", 1) + ui.U(8), ui.U(12));
        ui.Rect(new Rectangle(lr.X - ui.U(1), lr.Y - ui.U(1), lr.Width + ui.U(2), lr.Height + ui.U(2)), Color.Black * 0.8f);
        ui.Rect(lr, Kit.Gilt); // a gold plate, unlike the map's party tags
        ui.TextC("QUEST", sp.X, lr.Y + ui.U(3), Kit.IronDark, 1);
    }
}
