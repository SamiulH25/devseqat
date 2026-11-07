using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the event dialog as an illustrated page: a painted scene chosen by the event, its title on a ribbon, the text,
/// and numbered choices with their effects as chips.
partial class MapScreen
{
    void EventPanel()
    {
        if (C.S.Event is not PendingEvent pe || Campaign.EventOf(pe.Id) is not EventDef e) { panel = PanelKind.None; return; }
        string body = C.Fill(e.Text, pe).ToUpperInvariant();
        int w = Math.Min(ui.U(600), ui.W - ui.U(40));
        int big = ui.TW("M", 2) * 60 <= w - ui.U(40) ? 2 : 1;
        var lines = ui.Wrap(body, w - ui.U(40), big);
        int lh = ui.U(big == 2 ? 18 : 13), picH = Math.Clamp(ui.H / 4, ui.U(100), ui.U(170)), rowH = ui.U(32);
        int h = Math.Min(ui.H - ui.U(20), picH + ui.U(28) + lines.Count * lh + ui.U(26) + e.Choices.Count * (rowH + ui.U(6)) + ui.U(34));
        ui.Dim(0.5f);
        var r = new Rectangle((ui.W - w) / 2, (ui.H - h) / 2, w, h);
        ui.PlateBody(r, Kit.Gilt);
        var pic = new Rectangle(r.X + ui.U(6), r.Y + ui.U(6), r.Width - ui.U(12), picH);
        EventScene(pic, e.Id);
        ui.Frame(pic, Color.Black * 0.7f, ui.U(1));
        ui.Ribbon(r.Center.X, pic.Bottom - ui.U(14), C.Fill(e.Title, pe).ToUpperInvariant(), new Color(130, 50, 36), 2);
        int x = r.X + ui.U(20), cw = r.Width - ui.U(40), y = pic.Bottom + ui.U(26);
        foreach (var line in lines) { ui.Text(line, x, y, Kit.Parchment, big); y += lh; }
        y += ui.U(4);
        ui.Divider(x, y, cw, Kit.Gilt * 0.6f);
        y += ui.U(12);
        for (int i = 0; i < e.Choices.Count; i++)
        {
            var ch = e.Choices[i];
            string why = C.ChoiceBlocked(ch);
            if (EventChoice(new Rectangle(x, y, cw, rowH), i, C.Fill(ch.Label, pe).ToUpperInvariant(), ch, why))
            { note = C.Choose(i) ?? ""; if (C.S.Event == null) panel = PanelKind.None; }
            y += rowH + ui.U(6);
        }
        if (note != "") ui.TextC(note, r.Center.X, r.Bottom - ui.U(16), Style.Warn, 1);
    }

    /// One choice: a number badge, its words, and its effects as chips (or why it cannot be taken). True on click.
    bool EventChoice(Rectangle r, int i, string label, ChoiceDef ch, string why)
    {
        bool able = why == "", hover = able && ui.Hover(r);
        ui.Rect(new Rectangle(r.X + ui.U(2), r.Y + ui.U(3), r.Width, r.Height), Color.Black * 0.35f);
        ui.VGrad(r, hover ? Kit.Mul(Kit.Gilt, 0.4f) : new Color(36, 34, 36), hover ? Kit.Mul(Kit.Gilt, 0.15f) : new Color(22, 22, 26), 1);
        ui.Frame(r, hover ? Kit.Gilt : Color.Black * 0.6f, ui.U(1));
        var num = new Rectangle(r.X + ui.U(4), r.Y + ui.U(4), r.Height - ui.U(8), r.Height - ui.U(8));
        ui.Rect(num, hover ? Kit.Gilt : Color.Black * 0.6f);
        ui.TextC((i + 1).ToString(), num.Center.X + ui.U(1), num.Center.Y - ui.U(3), hover ? Kit.IronDark : able ? Kit.Gilt : Style.Dim, 1);
        int ex = r.Right - ui.U(6);
        var chips = able ? ch.Effects.Select(EffectChip).Where(c => c.Text != "").Reverse().ToList() : new() { (why.ToUpperInvariant(), Style.Dim, "chain") };
        foreach (var (text, col, icon) in chips)
        {
            int ew = ui.TW(text, 1) + ui.U(20);
            if (ex - ew < num.Right + ui.TW(label, 1) + ui.U(20)) break;
            ex -= ew;
            ui.Chip(ex, r.Y + (r.Height - ui.U(13)) / 2, text, col, icon);
            ex -= ui.U(4);
        }
        ui.Text(label, num.Right + ui.U(10), r.Y + (r.Height - ui.U(7)) / 2, able ? Style.Text : Style.Dim, 1);
        if (ui.Hover(r) && able) note = EffectsText(ch);
        ui.Block(r);
        bool hit = able && ui.Clicked(r);
        if (hit) Ui.OnClick?.Invoke();
        return hit;
    }

    /// An effect ("gold:-150") as a chip: its words, good or bad colour, and an icon.
    static (string Text, Color Colour, string Icon) EffectChip(string fx)
    {
        var parts = fx.Split(':');
        string verb = parts[0], arg = parts.Length > 1 ? parts[1] : "";
        bool num = float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
        string sign = num && v > 0 ? "+" : "";
        bool good = verb == "unrest" || verb == "exhaustion" ? v < 0 : v >= 0;
        string Name(string n) => num ? $"{n} {sign}{arg}" : n;
        return verb switch
        {
            "gold" => (Name("GOLD"), good ? Style.Ok : Style.Bad, "coin"),
            "renown" => (Name("RENOWN"), good ? Kit.Gilt : Style.Bad, "star"),
            "unrest" => (Name("UNREST"), good ? Style.Ok : Style.Bad, "fire"),
            "prosperity" => (Name("PROSPERITY"), good ? Style.Ok : Style.Bad, "up"),
            "cohesion" => (Name("COHESION"), good ? Style.Ok : Style.Bad, "flag"),
            "opinion" => (Name("OPINION"), good ? Style.Ok : Style.Bad, "heart"),
            "opinion_realm" => (Name("LORDS"), good ? Style.Ok : Style.Bad, "crown"),
            "opinion_liege" => (Name("LIEGE"), good ? Style.Ok : Style.Bad, "crown"),
            "food" => (Name("FOOD"), good ? Style.Ok : Style.Bad, "bread"),
            "horses" => (Name("HORSES"), good ? Style.Ok : Style.Bad, "horse"),
            "xp" => (Name("XP"), good ? Style.Ok : Style.Bad, "up"),
            "exhaustion" => (Name("WEARINESS"), good ? Style.Ok : Style.Bad, "skull"),
            "bounty" => (Name("BOUNTY"), Kit.Gilt, "coin"),
            "tax" => ($"TAX {(int.TryParse(arg, out int t) && t >= 0 && t < Campaign.TaxNames.Length ? Campaign.TaxNames[t] : arg)}", Style.Text, "coin"),
            "prisoners_free" => ("FREE PRISONERS", Style.Ok, "chain"),
            "prisoners_recruit" => ("RECRUIT PRISONERS", Style.Warn, "chain"),
            "gamble" => ("A PRIZE, OR NOTHING", Kit.Gilt, "star"),
            "disband" => ("MEN LEAVE", Style.Bad, "skull"),
            "quest" => ("A QUEST", Kit.Gilt, "scroll"),
            _ => (fx.Replace('_', ' ').Replace(":", " ").ToUpperInvariant(), Style.Text, "scroll"),
        };
    }

    /// A choice's effects in a few words ("GOLD -150, UNREST -20").
    static string EffectsText(ChoiceDef c) => string.Join(", ", c.Effects.Select(fx => fx.Replace('_', ' ').Replace(":", " ").ToUpperInvariant()
        .Replace("OPINION REALM", "LORDS").Replace("OPINION LIEGE", "LIEGE").Replace("PRISONERS ", "PRISONERS: ").Replace("GAMBLE", "PRIZE")));

    // ---------- the pictures ----------

    /// The painted scene for an event: raiders burning a village, a castle and its court, the company's camp, or a market town.
    void EventScene(Rectangle p, string id)
    {
        float t = Environment.TickCount64 / 1000f;
        string kind = id switch
        {
            "band_terror" or "brigand_king" => "burning",
            "lord_favour" or "lord_gift" or "pretender" or "plot" or "tournament" => "court",
            "unrest_petition" or "famine" or "fair" => "town",
            _ => "camp",
        };
        bool day = kind is "town" or "court" && id != "plot";
        ui.VGrad(p, day ? new Color(70, 96, 130) : Kit.Night, day ? new Color(214, 160, 110) : kind == "burning" ? new Color(120, 54, 40) : Kit.Dusk, 2);
        if (!day) ui.Stars(new Rectangle(p.X, p.Y, p.Width, p.Height / 2), t, 30);
        var orb = new Vector2(p.X + p.Width * 0.82f, p.Y + p.Height * 0.28f);
        if (day) { ui.Disc(orb, ui.U(20), new Color(255, 230, 170) * 0.25f); ui.Disc(orb, ui.U(12), new Color(255, 236, 190)); }
        else ui.Disc(orb, ui.U(12), new Color(236, 226, 200));
        int ground = p.Bottom - ui.U(22);
        var far = day ? new Color(92, 100, 110) : new Color(54, 30, 36);
        var near = day ? new Color(40, 46, 40) : new Color(22, 14, 18);
        ui.Hills(p, p.Bottom - ui.U(36), ui.U(10), 0.9f + id.Length, far);
        switch (kind)
        {
            case "burning": Village(p, ground, near, t, true); break;
            case "town": Village(p, ground, near, t, false); break;
            case "court": Court(p, ground, near, t, id == "tournament"); break;
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
    void Village(Rectangle p, int ground, Color c, float t, bool burning)
    {
        for (int i = 0; i < 6; i++)
        {
            int hx = p.X + (int)(p.Width * (0.2f + i * 0.09f)), hw = ui.U(14 + i % 3 * 4), hh = ui.U(10 + i % 2 * 4);
            ui.Rect(new Rectangle(hx, ground - hh, hw, hh), c);
            for (int k = 0; k < hw / 2; k += Math.Max(1, ui.U(1))) ui.Rect(new Rectangle(hx + k, ground - hh - (hw / 2 - k) / 2 - ui.U(1), hw - 2 * k, Math.Max(1, ui.U(1))), c);
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
        ui.Rect(new Rectangle(p.X + (int)(p.Width * 0.38f), ground - ui.U(24), ui.U(4), ui.U(24)), c);
        ui.Rect(new Rectangle(p.X + (int)(p.Width * 0.38f) - ui.U(2), ground - ui.U(28), ui.U(8), ui.U(4)), c);
    }

    /// A castle with banners on its towers; at a tournament, a row of pavilions before it.
    void Court(Rectangle p, int ground, Color c, float t, bool lists)
    {
        int cx = p.X + p.Width / 2;
        ui.Castle(cx, ground + ui.U(2), 3, c, Kit.Ember);
        var hd = Style.Sides[Style.PaletteOf(Math.Max(0, C.Captain.Contract))];
        ui.Banner(cx - ui.U(36), p.Y + ui.U(10), ui.U(22), ui.U(50), hd, t * 1.5f);
        ui.Banner(cx + ui.U(36), p.Y + ui.U(10), ui.U(22), ui.U(50), hd, t * 1.5f + 1.3f);
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
        for (int g = 0; g < 4; g++) ui.Disc(fire, ui.U(40 - g * 9), Kit.Ember * 0.05f);
        ui.Icon("fire", (int)fire.X - ui.U(7), (int)fire.Y - ui.U(12), Kit.Ember, 2);
        for (int s = 0; s < 8; s++)
        {
            float k = (t * 0.4f + s * 0.125f) % 1;
            ui.Rect(new Rectangle((int)(fire.X + MathF.Sin(k * 9 + s) * ui.U(8)), (int)(fire.Y - ui.U(14) - k * p.Height * 0.5f), ui.U(1), ui.U(1)), Kit.Ember * (1 - k));
        }
    }
}
