using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The save screen (docs/SAVES.md), shared by the title (LOAD) and the campaign's menu (SAVE AS, LOAD): campaigns down the
/// left, the chosen campaign's saves on the right under a card of the chosen save (the map as it was, the captain, the day,
/// the purse, the men). Saving gives a name; loading asks the host to open the file; deleting asks twice.
sealed class SaveScreen
{
    public enum Use { Load, Save }

    public bool Open { get; private set; }
    public Use Mode { get; private set; }
    /// The save chosen to load (the host opens it).
    public Action<string>? OnLoad;
    /// A save was written here (the map takes its picture).
    public Action<string>? OnSaved;
    /// The campaign being played (from the map), or null on the title.
    CampaignState? current;
    string campaignId = "", savePath = "", confirm = "", note = "", name = "";
    int campaignScroll, saveScroll;
    readonly Dictionary<string, (DateTime Stamp, Texture2D Tex)> thumbs = new();

    /// True while a save's name is being typed: the map keeps its keys to itself.
    public bool Typing => Open && Mode == Use.Save;

    public void Show(Use use, CampaignState? playing)
    {
        Open = true; Mode = use; current = playing;
        Saves.Changed();
        var all = Saves.Campaigns();
        campaignId = playing?.CampaignId is { Length: > 0 } id && all.Any(c => c.Id == id) ? id : all.FirstOrDefault()?.Id ?? "";
        savePath = ""; confirm = ""; note = ""; name = ""; campaignScroll = saveScroll = 0;
        Ui.Typed.Clear();
    }

    public void Close()
    {
        Open = false;
        foreach (var t in thumbs.Values) t.Tex.Dispose();
        thumbs.Clear();
    }

    /// Draw it and act on clicks. `wheel` is the mouse wheel's move this frame; `kb`/`pkb` the keys now and before.
    public void Draw(Ui ui, int wheel, KeyboardState kb, KeyboardState pkb)
    {
        int U(float v) => ui.U(v);
        bool Hit(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
        var all = Saves.Campaigns();
        var shown = Mode == Use.Save ? all.Where(c => c.Id == current?.CampaignId).ToList() : all;
        var camp = shown.FirstOrDefault(c => c.Id == campaignId) ?? shown.FirstOrDefault();
        campaignId = camp?.Id ?? "";
        var save = camp?.Saves.FirstOrDefault(e => e.Path == savePath) ?? camp?.Saves.FirstOrDefault();
        savePath = save?.Path ?? "";

        ui.Dim(0.55f);
        int pw = Math.Min(U(900), ui.W - U(16)), ph = Math.Min(U(500), ui.H - U(28));
        var r = new Rectangle((ui.W - pw) / 2, (ui.H - ph) / 2 + U(6), pw, ph);
        ui.PlateBody(r, Kit.Gilt);
        ui.Ribbon(r.Center.X, r.Y - U(10), Mode == Use.Save ? "SAVE THE CAMPAIGN" : "LOAD A CAMPAIGN", Kit.Mul(Kit.Gilt, 0.66f));
        var inner = new Rectangle(r.X + U(16), r.Y + U(26), r.Width - U(32), r.Height - U(26) - U(58));
        int leftW = Math.Min(U(300), inner.Width * 2 / 5);
        var left = new Rectangle(inner.X, inner.Y, leftW, inner.Height);
        var right = new Rectangle(left.Right + U(14), inner.Y, inner.Right - left.Right - U(14), inner.Height);

        // ---- the campaigns ----
        if (Mode == Use.Load || camp != null)
        {
            ui.Text(Mode == Use.Save ? "THIS CAMPAIGN" : $"CAMPAIGNS ({all.Count})", left.X, left.Y, Kit.Gilt, 1);
            int rowH = U(58), top = left.Y + U(14), fit = Math.Max(1, (left.Bottom - top) / rowH);
            if (ui.Hover(left)) campaignScroll -= Math.Sign(wheel);
            campaignScroll = Math.Clamp(campaignScroll, 0, Math.Max(0, shown.Count - fit));
            for (int i = campaignScroll; i < shown.Count && i < campaignScroll + fit; i++)
            {
                var c = shown[i];
                var row = new Rectangle(left.X, top + (i - campaignScroll) * rowH, left.Width, rowH - U(4));
                if (CampaignRow(ui, row, c, c.Id == campaignId)) { campaignId = c.Id; savePath = ""; confirm = ""; saveScroll = 0; }
            }
            if (shown.Count > fit) ui.TextR($"{campaignScroll + 1}-{Math.Min(shown.Count, campaignScroll + fit)} OF {shown.Count}", left.Right, left.Y, Style.Dim, 1);
        }
        if (shown.Count == 0)
            ui.Text(Mode == Use.Save ? "NOT SAVED YET: NAME THE FIRST SAVE BELOW." : "NO SAVED CAMPAIGNS.", left.X, left.Y + U(18), Style.Dim, 1);

        // ---- the chosen save, then the campaign's saves ----
        int cardH = Math.Min(U(200), right.Height / 2);
        if (save != null) Card(ui, new Rectangle(right.X, right.Y, right.Width, cardH), save);
        int listTop = right.Y + cardH + U(10);
        if (camp != null)
        {
            ui.Text($"SAVES ({camp.Saves.Count})", right.X, listTop, Kit.Gilt, 1);
            int rowH = U(26), top = listTop + U(14), fit = Math.Max(1, (right.Bottom - top) / rowH);
            var list = new Rectangle(right.X, top, right.Width, right.Bottom - top);
            if (ui.Hover(list)) saveScroll -= Math.Sign(wheel);
            saveScroll = Math.Clamp(saveScroll, 0, Math.Max(0, camp.Saves.Count - fit));
            int at = camp.Saves.IndexOf(save!);
            if (Hit(Keys.Down) && at + 1 < camp.Saves.Count) { savePath = camp.Saves[at + 1].Path; confirm = ""; }
            if (Hit(Keys.Up) && at > 0) { savePath = camp.Saves[at - 1].Path; confirm = ""; }
            for (int i = saveScroll; i < camp.Saves.Count && i < saveScroll + fit; i++)
            {
                var e = camp.Saves[i];
                var row = new Rectangle(right.X, top + (i - saveScroll) * rowH, right.Width, rowH - U(3));
                if (SaveRow(ui, row, e, e.Path == savePath))
                {
                    savePath = e.Path; confirm = "";
                    if (Mode == Use.Save && e.Kind == SaveKind.Named) name = e.Label;
                }
            }
        }

        // ---- the buttons ----
        int by = r.Bottom - U(46), bh = U(30);
        var back = new Rectangle(r.Right - U(16) - U(120), by, U(120), bh);
        if (ui.Button(back, "BACK")) { Close(); return; }
        int x = r.X + U(16);
        Rectangle Next(int w) { var b = new Rectangle(x, by, U(w), bh); x += U(w) + U(8); return b; }
        if (Mode == Use.Save)
        {
            TypeName();
            var box = Next(260);
            NameBox(ui, box);
            bool exists = camp?.Saves.Any(e => e.Kind == SaveKind.Named && e.Label == name.Trim()) ?? false;
            string? cannot = current == null ? "NOTHING TO SAVE" : Saves.CannotSave(current, SaveKind.Named, name);
            if ((ui.Button(Next(140), exists ? "OVERWRITE" : "SAVE", cannot == null, active: cannot == null) || (Hit(Keys.Enter) && cannot == null)) && current != null)
            {
                savePath = Saves.Save(current, SaveKind.Named, name);
                OnSaved?.Invoke(savePath);
                campaignId = current.CampaignId;
                note = $"SAVED AS {name.Trim()}.";
                name = "";
            }
            else if (cannot != null && name != "" && Hit(Keys.Enter)) note = cannot;
        }
        else
        {
            bool readable = save?.Meta != null;
            if (ui.Button(Next(140), "LOAD", readable, active: readable) || (Hit(Keys.Enter) && readable)) { var p = save!.Path; Close(); OnLoad?.Invoke(p); return; }
        }
        bool playing = camp != null && camp.Id == current?.CampaignId;
        if (save != null && ui.Button(Next(150), confirm == save.Path ? "SURE? DELETE" : "DELETE SAVE", !(playing && current!.Ironman)))
        {
            if (confirm != save.Path) confirm = save.Path;
            else { Saves.Delete(save); confirm = ""; savePath = ""; note = "SAVE DELETED."; }
        }
        if (Mode == Use.Load && camp != null && ui.Button(Next(190), confirm == camp.Id ? "SURE? DELETE ALL" : "DELETE CAMPAIGN", !playing))
        {
            if (confirm != camp.Id) confirm = camp.Id;
            else { Saves.DeleteCampaign(camp.Id); confirm = ""; campaignId = ""; savePath = ""; note = "CAMPAIGN DELETED."; }
        }
        if (note != "") ui.TextFit(note, x, by + bh / 2 - U(4), back.X - x - U(8), Style.Dim, 1);
    }

    /// A campaign: its arms, the captain, the company, the day reached and how many saves; Ironman and ended marked.
    bool CampaignRow(Ui ui, Rectangle r, CampaignEntry c, bool on)
    {
        int U(float v) => ui.U(v);
        var m = c.Card;
        bool hover = ui.Hover(r);
        ui.Rect(r, on ? Kit.Mul(Kit.Gilt, 0.28f) : hover ? Color.White * 0.06f : Color.Black * 0.25f);
        ui.Frame(r, on ? Kit.Gilt : Color.White * 0.1f, Math.Max(1, U(1)));
        ui.Shield(new Rectangle(r.X + U(6), r.Y + U(6), U(34), U(40)), Style.Sides[Math.Clamp(m?.Palette ?? 2, 0, Style.Sides.Length - 1)]);
        int tx = r.X + U(48), tw = r.Right - tx - U(6);
        ui.TextFit(m?.Captain ?? "UNREADABLE", tx, r.Y + U(6), tw, on ? Kit.Gilt : Style.Text, 1);
        ui.TextFit(m == null ? "TOO OLD OR DAMAGED" : m.Company != "" ? m.Company : m.Service, tx, r.Y + U(19), tw, Style.Dim, 1);
        string tags = m == null ? "" : m.GameOver ? "  ENDED" : m.Ironman ? "  IRONMAN" : "";
        ui.Text(m == null ? $"{c.Saves.Count} FILES" : $"DAY {m.Day}  {c.Saves.Count} SAVE{(c.Saves.Count == 1 ? "" : "S")}", tx, r.Y + U(33), Style.Text, 1);
        if (tags != "") ui.TextR(tags.Trim(), r.Right - U(6), r.Y + U(33), m!.GameOver ? Style.Bad : Style.Warn, 1);
        if (c.Id == current?.CampaignId) ui.TextR("PLAYING", r.Right - U(6), r.Y + U(6), Style.Ok, 1);
        ui.Block(r);
        bool hit = ui.Clicked(r);
        if (hit) Ui.OnClick?.Invoke();
        return hit;
    }

    /// One save in the list: its name, the day, when it was made.
    bool SaveRow(Ui ui, Rectangle r, SaveEntry e, bool on)
    {
        int U(float v) => ui.U(v);
        bool hover = ui.Hover(r);
        ui.Rect(r, on ? Kit.Mul(Kit.Gilt, 0.28f) : hover ? Color.White * 0.06f : Color.Black * 0.22f);
        if (on) ui.Rect(new Rectangle(r.X, r.Y, U(3), r.Height), Kit.Gilt);
        var kc = e.Kind == SaveKind.Named ? Kit.Parchment : Style.Dim;
        int ty = r.Y + (r.Height - U(7)) / 2;
        ui.TextFit(e.Label, r.X + U(10), ty, r.Width / 2 - U(10), e.Meta == null ? Style.Bad : kc, 1);
        ui.TextR(Ago(e.Saved), r.Right - U(8), ty, Style.Dim, 1);
        if (e.Meta != null) ui.TextR($"DAY {e.Meta.Day}", r.Right - U(100), ty, Style.Text, 1);
        ui.Block(r);
        bool hit = ui.Clicked(r);
        if (hit) Ui.OnClick?.Invoke();
        return hit;
    }

    /// The chosen save large: the map as it was (or the arms, when there is no picture) and what the company had.
    void Card(Ui ui, Rectangle r, SaveEntry e)
    {
        int U(float v) => ui.U(v);
        ui.Rect(r, Color.Black * 0.3f);
        ui.Frame(r, Kit.Gilt * 0.5f, Math.Max(1, U(1)));
        int picH = r.Height - U(8), picW = Math.Min(r.Width / 2, picH * 16 / 9);
        var pic = new Rectangle(r.X + U(4), r.Y + U(4), picW, picH);
        var m = e.Meta;
        if (Thumb(ui.Sb.GraphicsDevice, e) is Texture2D t)
        {
            float k = Math.Min(pic.Width / (float)t.Width, pic.Height / (float)t.Height);
            int w = (int)(t.Width * k), h = (int)(t.Height * k);
            ui.Sb.Draw(t, new Rectangle(pic.Center.X - w / 2, pic.Center.Y - h / 2, w, h), Color.White);
        }
        else
        {
            ui.Rect(pic, Kit.IronDark);
            int sw = Math.Min(pic.Width, pic.Height) / 2;
            ui.Shield(new Rectangle(pic.Center.X - sw / 2, pic.Center.Y - sw * 6 / 10, sw, sw * 6 / 5), Style.Sides[Math.Clamp(m?.Palette ?? 2, 0, Style.Sides.Length - 1)]);
        }
        int tx = pic.Right + U(12), tw = r.Right - tx - U(8), y = r.Y + U(8);
        ui.TextFit(e.Label, tx, y, tw, Kit.Gilt, ui.FitScale(e.Label, tw)); y += U(20);
        if (m == null)
        {
            ui.TextFit((CampaignState.TooOld + ": CANNOT BE LOADED").ToUpperInvariant(), tx, y, tw, Style.Bad, 1);
            return;
        }
        ui.TextFit(m.Captain, tx, y, tw, Style.Text, 1); y += U(12);
        if (m.Company != "") { ui.TextFit(m.Company, tx, y, tw, Style.Dim, 1); y += U(12); }
        ui.TextFit(m.Service, tx, y, tw, Style.Dim, 1); y += U(18);
        ui.Rect(new Rectangle(tx, y, tw, Math.Max(1, U(1))), Kit.Gilt * 0.3f); y += U(8);
        ui.Icon("clock", tx, y, Style.Dim, 1); ui.Text($"DAY {m.Day}", tx + U(12), y, Style.Text, 1); y += U(14);
        ui.Icon("coin", tx, y, Kit.Gilt, 1); ui.Text($"{Kit.N(m.Gold)} CROWNS", tx + U(12), y, Style.Text, 1); y += U(14);
        ui.Icon("shield", tx, y, Style.Dim, 1); ui.Text($"{m.Men} MEN", tx + U(12), y, Style.Text, 1); y += U(18);
        ui.TextFit($"SAVED {Ago(e.Saved)}", tx, y, tw, Style.Dim, 1); y += U(12);
        if (m.Ironman) ui.Text("IRONMAN", tx, y, Style.Warn, 1);
        if (m.GameOver) ui.Text("THE WAR IS OVER", tx, y, Style.Bad, 1);
    }

    /// The name being typed for a save: letters, digits, spaces, apostrophes and dashes, in capitals.
    void TypeName()
    {
        while (Ui.Typed.Count > 0)
        {
            char ch = Ui.Typed.Dequeue();
            if (ch == '\b') { if (name.Length > 0) name = name[..^1]; }
            else if (name.Length < Saves.NameMax && (char.IsLetterOrDigit(ch) || ch is ' ' or '\'' or '-')) name += char.ToUpperInvariant(ch);
        }
    }

    void NameBox(Ui ui, Rectangle r)
    {
        int U(float v) => ui.U(v);
        ui.Rect(r, Color.Black * 0.45f);
        ui.Frame(r, Kit.Gilt * 0.7f, Math.Max(1, U(1)));
        int ty = r.Y + (r.Height - U(7)) / 2;
        bool caret = Ui.Instant || (int)(Ui.Now * 2) % 2 == 0;
        if (name == "") ui.Text("TYPE A NAME" + (caret ? "_" : ""), r.X + U(8), ty, Style.Dim * 0.7f, 1);
        else ui.TextFit(name + (caret ? "_" : ""), r.X + U(8), ty, r.Width - U(16), Style.Text, 1);
        ui.Block(r);
    }

    /// The save's map picture as a texture, read once and kept while the screen is open.
    Texture2D? Thumb(GraphicsDevice gd, SaveEntry e)
    {
        string p = e.Thumb;
        if (!File.Exists(p)) return null;
        var stamp = File.GetLastWriteTimeUtc(p);
        if (thumbs.TryGetValue(p, out var t) && t.Stamp == stamp) return t.Tex;
        try
        {
            var (px, w, h) = Png.Decode(File.ReadAllBytes(p));
            var tex = new Texture2D(gd, w, h);
            tex.SetData(px);
            if (thumbs.TryGetValue(p, out var old)) old.Tex.Dispose();
            thumbs[p] = (stamp, tex);
            return tex;
        }
        catch (Exception) { return null; }
    }

    /// How long ago, in words: JUST NOW, 5 MIN AGO, 3 H AGO, 2 DAYS AGO, or the date.
    public static string Ago(DateTime utc)
    {
        var d = DateTime.UtcNow - utc;
        if (d.TotalMinutes < 1) return "JUST NOW";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} MIN AGO";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours} H AGO";
        if (d.TotalDays < 7) return $"{(int)d.TotalDays} DAY{((int)d.TotalDays == 1 ? "" : "S")} AGO";
        return utc.ToLocalTime().ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
    }
}
