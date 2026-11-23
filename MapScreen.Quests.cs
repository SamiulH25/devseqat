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

    int lastTutStep = -1;
    float tutAnimAt = -99f;
    string tutToast = "";
    float tutToastAt = -99f;

    /// The pinned quest's place (or the last one SHOW ME marked, or the first-campaign tutorial beacon) as a gold marker on the map.
    void QuestMark()
    {
        Vector2? at = questMark;
        string tag = "QUEST";
        bool tutBeacon = false;
        if (C.S.Contracts.FirstOrDefault(c => c.Id == C.S.PinnedQuest) is Contract pin && C.PlaceOf(pin).Place is Vector2 p) at ??= p;
        if (at == null && C.TutorialActive)
        {
            var tp = C.TutorialPrompt();
            if (tp.Beacon is Vector2 tb) { at = tb; tag = tp.BeaconLabel != "" ? tp.BeaconLabel : "TUTORIAL"; tutBeacon = true; }
        }
        if (at is not Vector2 w) return;
        if (!OnScreen(w, out var sp, out _))
        {
            if (!tutBeacon) return;
            // Off-screen tutorial beacon: pin a pulsing gilt pointer at the screen edge toward the target.
            var dir = sp - new Vector2(ui.W * 0.5f, ui.H * 0.5f);
            if (dir.LengthSquared() < 1) dir = Vector2.UnitX;
            dir.Normalize();
            float pad = ui.U(52);
            var edge = new Vector2(
                Math.Clamp(ui.W * 0.5f + dir.X * (ui.W * 0.45f), pad, ui.W - pad),
                Math.Clamp(ui.H * 0.5f + dir.Y * (ui.H * 0.45f), ui.U(120), ui.H - pad));
            float pulse = Ui.Instant ? 1f : 0.65f + 0.35f * MathF.Sin(Ui.Now * 5f);
            int ew = ui.TW(tag, 1) + ui.U(22), eh = ui.U(18);
            var er = new Rectangle((int)edge.X - ew / 2, (int)edge.Y - eh / 2, ew, eh);
            ui.Rect(er, Kit.IronDark * 0.94f);
            ui.Frame(er, Kit.Gilt * pulse, ui.U(2));
            ui.TextC($"> {tag} <", edge.X, er.Y + ui.U(5), Kit.Gilt, 1);
            return;
        }
        // A gold ring on the ground round the place, breathing, with a gold plate under it.
        float k = 1 + 0.14f * MathF.Sin(Ui.Now * 3.5f);
        float rx = ui.U(18) * k, ry = ui.U(8) * k;
        for (int i = 0; i < 32; i++)
        {
            float a2 = i / 32f * MathF.Tau;
            int px = (int)(sp.X + MathF.Cos(a2) * rx), py = (int)(sp.Y + MathF.Sin(a2) * ry);
            ui.Rect(new Rectangle(px - ui.U(2), py - ui.U(2), ui.U(5), ui.U(5)), Color.Black * 0.7f);
            ui.Rect(new Rectangle(px - ui.U(1), py - ui.U(1), ui.U(3), ui.U(3)), Kit.Gilt);
        }
        if (tutBeacon)
        {
            // Expanding outer ripple ring + bobbing pointer arrow above the tutorial target.
            float rip = Ui.Instant ? 0.5f : (Ui.Now * 0.85f) % 1f;
            float rrx = ui.U(18 + 26 * rip), rry = ui.U(8 + 12 * rip);
            float ra = (1f - rip) * 0.85f;
            for (int i = 0; i < 32; i++)
            {
                float a2 = i / 32f * MathF.Tau;
                int px = (int)(sp.X + MathF.Cos(a2) * rrx), py = (int)(sp.Y + MathF.Sin(a2) * rry);
                ui.Rect(new Rectangle(px - ui.U(1), py - ui.U(1), ui.U(3), ui.U(3)), Kit.Gilt * ra);
            }
            float bob = Ui.Instant ? 0f : MathF.Sin(Ui.Now * 5.5f) * ui.U(4);
            int ay = (int)(sp.Y - ry - ui.U(26) + bob);
            for (int row = 0; row < 6; row++)
            {
                int hw = ui.U(6 - row);
                ui.Rect(new Rectangle((int)sp.X - hw, ay + row * ui.U(2), hw * 2 + ui.U(1), ui.U(2)), Kit.Gilt);
            }
        }
        var lr = new Rectangle((int)sp.X - ui.TW(tag, 1) / 2 - ui.U(6), (int)(sp.Y + ry + ui.U(4)), ui.TW(tag, 1) + ui.U(12), ui.U(14));
        ui.Rect(new Rectangle(lr.X - ui.U(1), lr.Y - ui.U(1), lr.Width + ui.U(2), lr.Height + ui.U(2)), Color.Black * 0.85f);
        ui.Rect(lr, Kit.Gilt); // a gold plate, unlike the map's party tags
        ui.TextC(tag, sp.X, lr.Y + ui.U(3), Kit.IronDark, 1);
    }

    /// Interactive first-campaign tutorial banner docked top-center under the clock so it is always front-and-center.
    void TutorialCard()
    {
        int prev = C.S.TutorialStep;
        if (!C.TutorialActive)
        {
            if (Ui.Now - tutToastAt < 2.6f && tutToast != "") DrawTutToast(ui.U(88));
            return;
        }
        C.UpdateTutorial();
        if (C.S.TutorialStep != prev)
        {
            tutAnimAt = Ui.Now;
            if (prev >= 0)
            {
                tutToast = prev switch
                {
                    0 => "STEP 1 COMPLETE! +60 CROWNS",
                    1 => "STEP 2 COMPLETE! +10 RENOWN, +3 FOOD",
                    2 => "STEP 3 COMPLETE! +150 CROWNS, +15 RENOWN",
                    3 => "STEP 4 COMPLETE! +80 CROWNS, +10 RENOWN",
                    _ => "FIRST MARCH COMPLETE! +100 CROWNS, +15 RENOWN",
                };
                tutToastAt = Ui.Now;
            }
        }
        if (!C.TutorialActive)
        {
            if (Ui.Now - tutToastAt < 2.6f && tutToast != "") DrawTutToast(ui.U(88));
            return;
        }
        var (step, title, body, hint, reward, _, _) = C.TutorialPrompt();
        if (lastTutStep != step) { lastTutStep = step; tutAnimAt = Ui.Now; }
        float age = Ui.Instant ? 99f : Ui.Now - tutAnimAt;
        float slide = Ease.OutCubic(Math.Clamp(age / 0.35f, 0f, 1f));
        float pulse = Ui.Instant ? 0.85f : 0.65f + 0.35f * MathF.Sin(Ui.Now * 4.2f);
        float flash = Ui.Instant ? 0f : MathF.Max(0f, 1f - age / 0.6f);

        int w = Math.Min(ui.W - ui.U(420), ui.U(520));
        w = Math.Max(w, ui.U(360));
        var lines = ui.Wrap(body, w - ui.U(28));
        int h = ui.U(84) + lines.Count * ui.U(12);
        int yOff = (int)((1f - slide) * -ui.U(18));
        var r = new Rectangle((ui.W - w) / 2, ui.U(86) + yOff, w, h);

        // Outer glow & solid backing so it stands out cleanly over the map.
        ui.Rect(new Rectangle(r.X - ui.U(2), r.Y - ui.U(2), r.Width + ui.U(4), r.Height + ui.U(4)), Kit.Gilt * (0.28f * pulse + 0.45f * flash));
        ui.Rect(r, Color.Lerp(Kit.IronDark, Kit.Gilt, 0.14f * flash) * 0.96f);
        ui.Panel(r, Color.Lerp(Kit.Gilt * pulse, Color.White, 0.35f * flash), ui.U(2));
        ui.Rect(new Rectangle(r.X, r.Y, ui.U(4), r.Height), Kit.Gilt);

        // Header + 5-step progress pips.
        ui.Icon("star", r.X + ui.U(12), r.Y + ui.U(9), Kit.Gilt, 1);
        ui.Text($"FIRST MARCH  {step + 1}/{Campaign.TutorialSteps}:  {title}", r.X + ui.U(24), r.Y + ui.U(9), Kit.Gilt, 1);
        int pipW = ui.U(14), pipGap = ui.U(4), pipX = r.Right - ui.U(12) - Campaign.TutorialSteps * (pipW + pipGap) + pipGap;
        for (int i = 0; i < Campaign.TutorialSteps; i++)
        {
            var pr = new Rectangle(pipX + i * (pipW + pipGap), r.Y + ui.U(10), pipW, ui.U(5));
            Color pc = i < step ? Kit.Gilt : i == step ? Color.Lerp(Kit.Gilt, Color.White, 0.4f * pulse) : Kit.Rivet * 0.55f;
            ui.Rect(pr, pc);
        }

        // Body instructions.
        int ty = r.Y + ui.U(26);
        for (int i = 0; i < lines.Count; i++) { ui.Text(lines[i], r.X + ui.U(14), ty, Style.Text, 1); ty += ui.U(12); }

        // Highlighted DO: action bar with animated arrow.
        int actY = ty + ui.U(4);
        var actR = new Rectangle(r.X + ui.U(12), actY, r.Width - ui.U(24), ui.U(18));
        ui.Rect(actR, Kit.Gilt * (0.14f + 0.08f * pulse));
        ui.Frame(actR, Kit.Gilt * 0.65f, ui.U(1));
        int nudge = Ui.Instant ? 0 : (int)(MathF.Sin(Ui.Now * 6f) * ui.U(2));
        ui.Text(">", actR.X + ui.U(7) + nudge, actR.Y + ui.U(5), Kit.Gilt, 1);
        ui.Text($"DO: {hint}", actR.X + ui.U(18), actR.Y + ui.U(5), Color.White, 1);

        // Footer: reward + FINISH (final step) + SKIP.
        int footY = r.Bottom - ui.U(24);
        ui.Text($"REWARD: {reward}", r.X + ui.U(14), footY + ui.U(4), Style.Ok, 1);
        if (step == Campaign.TutorialSteps - 1)
        {
            var fr = new Rectangle(r.Right - ui.U(138), footY, ui.U(68), ui.U(18));
            if (ui.Button(fr, "FINISH", true)) C.CompleteTutorial();
        }
        if (ui.Button(new Rectangle(r.Right - ui.U(64), footY, ui.U(52), ui.U(18)), "SKIP")) C.SkipTutorial();

        if (Ui.Now - tutToastAt < 2.6f && tutToast != "") DrawTutToast(r.Bottom + ui.U(6));
    }

    void DrawTutToast(int y)
    {
        float age = Ui.Now - tutToastAt;
        float a = Math.Clamp(1f - MathF.Max(0f, age - 1.8f) / 0.8f, 0f, 1f);
        int w = ui.TW(tutToast, 1) + ui.U(28);
        var r = new Rectangle((ui.W - w) / 2, y, w, ui.U(20));
        ui.Rect(r, Kit.IronDark * (0.94f * a));
        ui.Frame(r, Style.Ok * a, ui.U(1));
        ui.TextC(tutToast, ui.W * 0.5f, r.Y + ui.U(6), Style.Ok * a, 1);
    }

    /// Compact 1-line tutorial reminder at top-center while inside a town or party modal.
    void TutorialStrip()
    {
        if (!C.TutorialActive) return;
        C.UpdateTutorial();
        if (!C.TutorialActive) return;
        var (step, title, _, hint, _, _, _) = C.TutorialPrompt();
        float pulse = Ui.Instant ? 0.85f : 0.65f + 0.35f * MathF.Sin(Ui.Now * 4.2f);
        string msg = $"FIRST MARCH {step + 1}/{Campaign.TutorialSteps} ({title})  •  DO: {hint}";
        int w = Math.Min(ui.W - ui.U(40), ui.TW(msg, 1) + ui.U(92));
        var r = new Rectangle((ui.W - w) / 2, ui.U(6), w, ui.U(22));
        ui.Rect(r, Kit.IronDark * 0.96f);
        ui.Frame(r, Kit.Gilt * pulse, ui.U(1));
        ui.Rect(new Rectangle(r.X, r.Y, ui.U(3), r.Height), Kit.Gilt);
        ui.Text(msg, r.X + ui.U(10), r.Y + ui.U(7), Kit.Gilt, 1);
        if (ui.Button(new Rectangle(r.Right - ui.U(54), r.Y + ui.U(3), ui.U(48), ui.U(16)), "SKIP")) C.SkipTutorial();
    }
}
