using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// Battle Lab / Quick Battle setup screen: a proper dev & playtester tool for configuring armies, custom companies,
/// factions, siege fortresses & engines, terrain layouts, weather, sergeants/divisions, captain kit, and AI vs AI tests.
sealed class QuickBattleScreen
{
    readonly Ui ui;
    readonly Action<QuickBattleConfig> launch;
    public bool Open;
    public QuickBattleConfig C = QuickBattleConfig.FromPreset("OPEN FIELD");

    // Troop picker modal state (-1 closed, 0 adding to Player, 1 adding to Enemy).
    int pickerSide = -1;
    int pickerFilter; // 0 ALL, 1 FACTION, 2 INFANTRY, 3 ARCHERS, 4 HORSE, 5 CASTERS, 6 MONSTERS
    int pickerScroll;
    readonly int[] sideScroll = new int[2];

    static readonly string[] PickerTabs = { "ALL", "FACTION", "INFANTRY", "ARCHERS", "HORSE", "CASTERS", "MONSTERS" };

    public QuickBattleScreen(Ui ui, Action<QuickBattleConfig> launch) { this.ui = ui; this.launch = launch; }

    public void Show(QuickBattleConfig? existing = null)
    {
        if (existing != null) C = existing;
        Open = true;
        pickerSide = -1;
    }

    int U(float v) => ui.U(v);

    public void Draw(KeyboardState kb, KeyboardState pkb, int wheel)
    {
        bool Hit(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
        if (Hit(Keys.Escape))
        {
            if (pickerSide >= 0) pickerSide = -1;
            else Open = false;
            return;
        }
        if (Hit(Keys.Enter) && pickerSide < 0) { Launch(); return; }

        DrawBody(wheel);
    }

    void Launch()
    {
        Open = false;
        pickerSide = -1;
        AudioEngine.Current?.Play(Cue.WaxSeal, 0.75f);
        launch(C);
    }

    void DrawBody(int wheel)
    {
        ui.Dim(0.68f);
        int margin = U(14), top = U(12);
        ui.Ribbon(ui.W / 2, top, "BATTLE LAB: QUICK BATTLE", Kit.Gilt, 2);

        // Scenario presets strip along the top.
        int py = top + U(34);
        int pw = ui.W - 2 * margin;
        int cols = QuickBattleConfig.PresetNames.Length;
        int bw = (pw - U(4) * (cols - 1)) / cols;
        for (int i = 0; i < cols; i++)
        {
            string name = QuickBattleConfig.PresetNames[i];
            var br = new Rectangle(margin + i * (bw + U(4)), py, bw, U(20));
            if (ui.Button(br, ui.Fit(name, bw - U(6)), active: C.Preset == name) && pickerSide < 0)
                C = QuickBattleConfig.FromPreset(name, C.Seed);
        }

        int bodyY = py + U(34), footH = U(32), bodyH = ui.H - bodyY - footH - U(16);
        int avail = pw - 2 * U(10);
        int sideW = (int)(avail * 0.31f), midW = avail - 2 * sideW;
        var left = new Rectangle(margin, bodyY, sideW, bodyH);
        var mid = new Rectangle(left.Right + U(10), bodyY, midW, bodyH);
        var right = new Rectangle(mid.Right + U(10), bodyY, sideW, bodyH);

        DrawSide(left, C.Player, 0, "YOUR ARMY (TEAM 0)", wheel);
        DrawCenter(mid);
        DrawSide(right, C.Enemy, 1, "ENEMY ARMY (TEAM 1)", wheel);
        DrawFooter(new Rectangle(margin, ui.H - footH - U(10), pw, footH));

        if (pickerSide >= 0) DrawTroopPicker(wheel);
    }

    void DrawSide(Rectangle r, QuickSideConfig s, int side, string title, int wheel)
    {
        int pal = QuickBattleConfig.PaletteOf(s.Faction);
        var inner = ui.Plate(r, title, Style.Sides[pal].Ui);
        int x = inner.X, w = inner.Width, y = inner.Y;
        bool block = pickerSide >= 0;

        // Faction selector with heraldic shield.
        int sh = U(22);
        ui.Shield(new Rectangle(x, y, sh * 5 / 6, sh), Style.Sides[pal]);
        int fx = x + sh + U(4), fw = w - sh - U(4);
        int totalFacs = Factions.All.Count + 2; // 0..N-1 realms, -1 bandits, -2 monsters
        int fi = s.Faction >= 0 ? s.Faction : Factions.All.Count + (-1 - s.Faction);
        int nfi = Stepper(fx, y, fw, "", QuickBattleConfig.FactionName(s.Faction), fi, totalFacs, block);
        if (nfi != fi)
        {
            s.Faction = nfi < Factions.All.Count ? nfi : -(nfi - Factions.All.Count + 1);
            C.Preset = "CUSTOM";
        }
        y += U(26);

        // Mode toggle: FACTION HOST vs CUSTOM COMPANIES
        int hw = (w - U(6)) / 2;
        if (ui.Button(new Rectangle(x, y, hw, U(20)), "FACTION HOST", active: !s.Custom) && !block) s.Custom = false;
        if (ui.Button(new Rectangle(x + hw + U(6), y, hw, U(20)), "CUSTOM ROSTER", active: s.Custom) && !block)
        {
            s.Custom = true;
            if (s.Companies.Count == 0) s.Companies = QuickBattleConfig.BuildFactionCompanies(s.Faction, s.Men, s.Tier, new Random(C.Seed + side));
        }
        y += U(26);

        if (!s.Custom)
        {
            s.Men = NumStepper(x, y, w, "MEN", s.Men, 20, 600, 20, block); y += U(24);
            s.Tier = NumStepper(x, y, w, "TROOP TIER", s.Tier, 1, 6, 1, block); y += U(24);
            // Preview what companies this generates
            var preview = QuickBattleConfig.BuildFactionCompanies(s.Faction, s.Men, s.Tier, new Random(C.Seed + side));
            ui.Text($"COMPANIES ({preview.Count}, {preview.Sum(c => c.Count)} MEN):", x, y, Kit.Gilt, 1); y += U(13);
            int maxRows = Math.Max(2, (inner.Bottom - y - U(side == 0 ? 114 : 74)) / U(13));
            foreach (var (tid, cnt) in preview.Take(maxRows))
            {
                var t = Troops.Get(tid);
                ui.TextR($"{cnt}", x + U(26), y, Style.Text, 1);
                ui.TextFit($"{t.Name.ToUpperInvariant()} (T{t.Tier})", x + U(32), y, w - U(32), Style.Dim, 1);
                y += U(13);
            }
            if (preview.Count > maxRows) { ui.Text($"... +{preview.Count - maxRows} MORE", x + U(32), y, Style.Dim, 1); y += U(13); }
        }
        else
        {
            int btnW = (w - U(6)) / 2;
            if (ui.Button(new Rectangle(x, y, btnW, U(20)), "+ ADD TROOP") && !block) { pickerSide = side; pickerScroll = 0; }
            if (ui.Button(new Rectangle(x + btnW + U(6), y, btnW, U(20)), "FILL FACTION") && !block)
                s.Companies = QuickBattleConfig.BuildFactionCompanies(s.Faction, s.Men, s.Tier, new Random(C.Seed + side));
            y += U(24);

            int bottomReserve = U(side == 0 ? 120 : 84);
            var listBox = new Rectangle(x, y, w, Math.Max(U(60), inner.Bottom - y - bottomReserve));
            ui.Rect(listBox, Color.Black * 0.25f);
            ui.Frame(listBox, Style.PanelEdge, U(1));

            int rowH = U(22);
            int vis = Math.Max(1, listBox.Height / rowH);
            if (!block && listBox.Contains(ui.Mouse) && wheel != 0) sideScroll[side] -= Math.Sign(wheel);
            sideScroll[side] = Math.Clamp(sideScroll[side], 0, Math.Max(0, s.Companies.Count - vis));

            for (int i = 0; i < vis && i + sideScroll[side] < s.Companies.Count; i++)
            {
                int idx = i + sideScroll[side];
                var (tid, cnt) = s.Companies[idx];
                var rr = new Rectangle(listBox.X + U(2), listBox.Y + U(2) + i * rowH, listBox.Width - U(4), rowH - U(2));
                var t = Troops.Get(tid);
                ui.TextFit(t.Name.ToUpperInvariant(), rr.X + U(4), rr.Y + U(6), rr.Width - U(104), Style.Text, 1);
                int bx = rr.Right - U(96);
                if (ui.Button(new Rectangle(bx, rr.Y + U(1), U(18), rr.Height - U(2)), "-") && !block)
                    s.Companies[idx] = (tid, Math.Max(1, cnt - (cnt > 20 ? 10 : 1)));
                ui.TextC($"{cnt}", bx + U(30), rr.Y + U(6), Kit.Gilt, 1);
                if (ui.Button(new Rectangle(bx + U(42), rr.Y + U(1), U(18), rr.Height - U(2)), "+") && !block)
                    s.Companies[idx] = (tid, Math.Min(200, cnt + (cnt >= 20 ? 10 : 1)));
                if (ui.Button(new Rectangle(bx + U(66), rr.Y + U(1), U(24), rr.Height - U(2)), "X") && !block)
                { s.Companies.RemoveAt(idx); break; }
            }
            if (s.Companies.Count == 0) ui.TextC("CLICK + ADD TROOP", listBox.Center.X, listBox.Center.Y - U(4), Style.Dim, 1);
            else ui.Text($"{s.Companies.Count} COMPANIES · {s.Companies.Sum(c => c.Count)} MEN", x + U(4), listBox.Bottom + U(4), Kit.Gilt, 1);
        }

        // Bottom controls pinned to the bottom of the column.
        int by = inner.Bottom - U(side == 0 ? 96 : 62);
        ui.Divider(x, by, w, Kit.Gilt * 0.45f); by += U(8);
        s.Divisions = Toggle(x, by, w, "SERGEANT DIVISIONS", s.Divisions, block); by += U(22);
        if (s.Divisions) { s.OfficerSkill = NumStepper(x, by, w, "OFFICER SKILL", s.OfficerSkill, 1, 9, 1, block); by += U(22); }
        else by += U(22);

        if (side == 0)
        {
            int ri = Math.Max(0, Array.IndexOf(QuickBattleConfig.Roads, C.Road));
            ri = Stepper(x, by, w, "CAPTAIN KIT", C.Road.ToUpperInvariant(), ri, QuickBattleConfig.Roads.Length, block);
            C.Road = QuickBattleConfig.Roads[ri];
            by += U(22);
            C.AuraTier = NumStepper(x, by, w, "AURA TIER", C.AuraTier, 0, 5, 1, block);
        }
        else
        {
            int ti = Math.Max(0, Array.IndexOf(QuickBattleConfig.Traits, s.Trait));
            ti = Stepper(x, by, w, "AI GENERAL", s.Trait == "" ? "BALANCED" : s.Trait.ToUpperInvariant(), ti, QuickBattleConfig.Traits.Length, block);
            s.Trait = QuickBattleConfig.Traits[ti];
        }
    }

    void DrawCenter(Rectangle r)
    {
        var inner = ui.Plate(r, "BATTLEFIELD AND RULES", Kit.Gilt);
        int x = inner.X, w = inner.Width, y = inner.Y;
        bool block = pickerSide >= 0;

        // Mode tabs: FIELD / SIEGE / DUEL
        int tw = (w - U(8)) / 3;
        var modes = new[] { (QuickBattleMode.Field, "FIELD"), (QuickBattleMode.Siege, "SIEGE"), (QuickBattleMode.Duel, "DUEL") };
        for (int i = 0; i < 3; i++)
            if (ui.Button(new Rectangle(x + i * (tw + U(4)), y, tw, U(22)), modes[i].Item2, active: C.Mode == modes[i].Item1) && !block)
            { C.Mode = modes[i].Item1; C.Preset = "CUSTOM"; }
        y += U(28);

        // Ground preset
        int gi = Math.Max(0, Array.IndexOf(QuickBattleConfig.Grounds, C.Ground));
        gi = Stepper(x, y, w, "TERRAIN", C.Ground.ToUpperInvariant(), gi, QuickBattleConfig.Grounds.Length, block);
        C.Ground = QuickBattleConfig.Grounds[gi];
        y += U(24);

        if (C.Mode == QuickBattleMode.Field)
        {
            int li = Math.Max(0, Array.IndexOf(QuickBattleConfig.Layouts, C.Layout));
            string lName = C.Layout is { } fl ? SplitCaps(fl.ToString()) : "AUTO";
            li = Stepper(x, y, w, "LAYOUT", lName, li, QuickBattleConfig.Layouts.Length, block);
            C.Layout = QuickBattleConfig.Layouts[li];
            y += U(24);
            C.River = Toggle(x, y, w, "RIVER AND FORDS", C.River, block);
            y += U(24);
        }
        else if (C.Mode == QuickBattleMode.Siege)
        {
            int fi = Math.Max(0, Array.IndexOf(QuickBattleConfig.Fortresses, C.FortressKind));
            fi = Stepper(x, y, w, "FORTRESS", SplitCaps(C.FortressKind.ToString()), fi, QuickBattleConfig.Fortresses.Length, block);
            C.FortressKind = QuickBattleConfig.Fortresses[fi];
            y += U(22);
            C.WallsLevel = NumStepper(x, y, w, "WALLS LEVEL", C.WallsLevel, 1, 3, 1, block); y += U(22);
            C.Ladders = NumStepper(x, y, w, "LADDERS", C.Ladders, 0, 6, 1, block); y += U(22);
            C.Breaches = NumStepper(x, y, w, "PRE-BREACHES", C.Breaches, 0, 3, 1, block); y += U(22);
            C.WallEngines = NumStepper(x, y, w, "WALL ENGINES", C.WallEngines, 0, 4, 1, block); y += U(22);
            var engBox = new Rectangle(x - U(4), y + U(2), w + U(8), U(84));
            ui.Rect(engBox, Color.Black * 0.28f); ui.Frame(engBox, Style.PanelEdge, U(1));
            y += U(6);
            ui.Text("ATTACKER ENGINES", x + U(2), y, Kit.Gilt, 1); y += U(14);
            int half = (w - U(8)) / 2;
            C.Rams = MiniNum(x, y, half, "RAM", C.Rams, 0, 2, block);
            C.Towers = MiniNum(x + half + U(8), y, half, "TOWER", C.Towers, 0, 3, block); y += U(21);
            C.Mangonels = MiniNum(x, y, half, "MANGONEL", C.Mangonels, 0, 3, block);
            C.Trebuchets = MiniNum(x + half + U(8), y, half, "TREBUCHET", C.Trebuchets, 0, 3, block); y += U(21);
            C.Springalds = MiniNum(x, y, half, "SPRINGALD", C.Springalds, 0, 3, block); y += U(26);
        }
        else
        {
            ui.Text("1V1 SINGLE COMBAT ON FOOT", x, y + U(4), Style.Dim, 1);
            y += U(24);
        }

        ui.Divider(x, y, w, Kit.Gilt * 0.45f); y += U(8);

        // Time & Weather
        int hi = Array.IndexOf(QuickBattleConfig.Hours, C.Hour);
        if (hi < 0) hi = 2;
        hi = Stepper(x, y, w, "TIME", QuickBattleConfig.HourNames[hi], hi, QuickBattleConfig.Hours.Length, block);
        C.Hour = QuickBattleConfig.Hours[hi];
        y += U(22);

        C.WeatherKind = Stepper(x, y, w, "WEATHER", QuickBattleConfig.WeatherNames[C.WeatherKind], C.WeatherKind, QuickBattleConfig.WeatherNames.Length, block);
        y += U(22);

        C.Tide = NumStepper(x, y, w, "MAGIC TIDE", C.Tide, 0, 3, 1, block);
        y += U(24);

        ui.Divider(x, y, w, Kit.Gilt * 0.45f); y += U(8);

        // Testing flags
        C.AutoPlayer = Toggle(x, y, w, "AI VS AI (SPECTATE)", C.AutoPlayer, block); y += U(22);
        C.Scouted = Toggle(x, y, w, "SCOUTED (NO FOG)", C.Scouted, block); y += U(22);
        C.SkipDeploy = Toggle(x, y, w, "SKIP DEPLOYMENT", C.SkipDeploy, block); y += U(22);

        // Seed control
        int sw = U(46);
        C.Seed = NumStepper(x, y, w - sw - U(4), "SEED", C.Seed, 1, 99999, 1, block);
        if (ui.Button(new Rectangle(x + w - sw, y, sw, U(20)), "RND") && !block) C.Seed = Random.Shared.Next(1, 99999);
    }

    void DrawTroopPicker(int wheel)
    {
        ui.Dim(0.55f);
        int pw = Math.Min(U(720), ui.W - U(40)), ph = Math.Min(U(500), ui.H - U(40));
        var box = new Rectangle((ui.W - pw) / 2, (ui.H - ph) / 2, pw, ph);
        var sideCfg = pickerSide == 0 ? C.Player : C.Enemy;
        var inner = ui.Plate(box, $"ADD TROOP TO {(pickerSide == 0 ? "YOUR ARMY" : "ENEMY ARMY")}", Kit.Gilt);
        int x = inner.X, w = inner.Width, y = inner.Y;

        int tw = (w - U(4) * (PickerTabs.Length - 1)) / PickerTabs.Length;
        for (int i = 0; i < PickerTabs.Length; i++)
            if (ui.Button(new Rectangle(x + i * (tw + U(4)), y, tw, U(20)), PickerTabs[i], active: pickerFilter == i))
            { pickerFilter = i; pickerScroll = 0; }
        y += U(26);

        string facCul = QuickBattleConfig.CultureOf(sideCfg.Faction);
        var troops = Troops.All.Where(t => t.Id != "captain" && pickerFilter switch
        {
            1 => t.Culture == facCul || t.Culture.StartsWith(facCul + "_"),
            2 => t.Class is UnitClass.Infantry or UnitClass.Skirmisher && !t.Wild,
            3 => t.Class == UnitClass.Archer && !t.Wild,
            4 => t.Class == UnitClass.Cavalry && !t.Wild,
            5 => t.Class == UnitClass.Caster,
            6 => t.Wild || t.Culture == "wild",
            _ => true,
        }).OrderBy(t => t.Tier).ThenBy(t => t.Name).ToList();

        int cols = 3, gap = U(6), cw = (w - gap * (cols - 1)) / cols, ch = U(42);
        var area = new Rectangle(x, y, w, inner.Bottom - y - U(32));
        int visRows = Math.Max(1, area.Height / (ch + gap));
        int totalRows = (troops.Count + cols - 1) / cols;
        if (area.Contains(ui.Mouse) && wheel != 0) pickerScroll -= Math.Sign(wheel);
        pickerScroll = Math.Clamp(pickerScroll, 0, Math.Max(0, totalRows - visRows));

        int pal = QuickBattleConfig.PaletteOf(sideCfg.Faction);
        for (int r = 0; r < visRows; r++)
            for (int c = 0; c < cols; c++)
            {
                int idx = (r + pickerScroll) * cols + c;
                if (idx >= troops.Count) break;
                var t = troops[idx];
                var cr = new Rectangle(x + c * (cw + gap), area.Y + r * (ch + gap), cw, ch);
                if (ui.Button(cr, ""))
                {
                    int defCount = t.Huge ? 2 : t.Class == UnitClass.Caster ? 12 : t.Class == UnitClass.Cavalry ? 24 : 36;
                    sideCfg.Companies.Add((t.Id, defCount));
                    pickerSide = -1;
                    return;
                }
                var port = new Rectangle(cr.X + U(4), cr.Y + U(4), ch - U(8), ch - U(8));
                ui.Portrait(t, port, Math.Max(1, port.Height / U(32)) * ui.S, palette: pal);
                int tx = port.Right + U(6), tw2 = cr.Right - tx - U(4);
                ui.TextFit(t.Name.ToUpperInvariant(), tx, cr.Y + U(6), tw2, Style.Text, 1);
                ui.TextFit($"T{t.Tier} {t.Class.ToString().ToUpperInvariant()} . {t.Culture.ToUpperInvariant()}", tx, cr.Y + U(20), tw2, Style.Dim, 1);
            }

        if (ui.Button(new Rectangle(inner.Center.X - U(70), inner.Bottom - U(26), U(140), U(24)), "CLOSE")) pickerSide = -1;
    }

    void DrawFooter(Rectangle r)
    {
        bool block = pickerSide >= 0;
        if (ui.Gilded(new Rectangle(r.X, r.Y, U(140), r.Height), "BACK", "ESC") && !block) Open = false;
        int mid = r.Center.X;
        if (ui.Gilded(new Rectangle(mid - U(175), r.Y, U(165), r.Height), "SWAP SIDES") && !block)
            (C.Player, C.Enemy) = (C.Enemy, C.Player);
        if (ui.Gilded(new Rectangle(mid + U(10), r.Y, U(165), r.Height), "RANDOM SEED") && !block)
            C.Seed = Random.Shared.Next(1, 99999);
        if (ui.Gilded(new Rectangle(r.Right - U(270), r.Y, U(270), r.Height), "LAUNCH BATTLE", "ENTER", tone: Style.Ok) && !block)
            Launch();
    }

    static string SplitCaps(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++) { if (i > 0 && char.IsUpper(s[i])) sb.Append(' '); sb.Append(char.ToUpperInvariant(s[i])); }
        return sb.ToString();
    }

    int Stepper(int x, int y, int w, string label, string value, int idx, int count, bool block)
    {
        int lx = x;
        if (label != "") { ui.Text(label, x, y + U(6), Kit.Gilt, 1); lx = x + U(96); }
        int bw = U(20), vw = w - (lx - x) - bw * 2 - U(8);
        if (ui.Button(new Rectangle(lx, y, bw, U(20)), "<") && !block) idx = (idx + count - 1) % count;
        var mid = new Rectangle(lx + bw + U(4), y, vw, U(20));
        ui.Rect(mid, Color.Black * 0.4f);
        ui.Frame(mid, Style.PanelEdge, U(1));
        ui.TextFitC(value, mid.Center.X, y + U(6), vw - U(6), Style.Text, 1);
        if (ui.Button(new Rectangle(mid.Right + U(4), y, bw, U(20)), ">") && !block) idx = (idx + 1) % count;
        return idx;
    }

    int NumStepper(int x, int y, int w, string label, int val, int min, int max, int step, bool block)
    {
        ui.Text(label, x, y + U(6), Kit.Gilt, 1);
        int lx = x + U(96), bw = U(20), vw = w - U(96) - bw * 2 - U(8);
        if (ui.Button(new Rectangle(lx, y, bw, U(20)), "-") && !block) val = Math.Max(min, val - step);
        var mid = new Rectangle(lx + bw + U(4), y, vw, U(20));
        ui.Rect(mid, Color.Black * 0.4f);
        ui.Frame(mid, Style.PanelEdge, U(1));
        ui.TextC($"{val}", mid.Center.X, y + U(6), Style.Text, 1);
        if (ui.Button(new Rectangle(mid.Right + U(4), y, bw, U(20)), "+") && !block) val = Math.Min(max, val + step);
        return val;
    }

    int MiniNum(int x, int y, int w, string label, int val, int min, int max, bool block)
    {
        ui.TextFit(label, x, y + U(6), w - U(60), Style.Dim, 1);
        int bx = x + w - U(58);
        if (ui.Button(new Rectangle(bx, y, U(18), U(18)), "-") && !block) val = Math.Max(min, val - 1);
        ui.TextC($"{val}", bx + U(28), y + U(5), val > 0 ? Kit.Gilt : Style.Text, 1);
        if (ui.Button(new Rectangle(bx + U(38), y, U(18), U(18)), "+") && !block) val = Math.Min(max, val + 1);
        return val;
    }

    bool Toggle(int x, int y, int w, string label, bool val, bool block)
    {
        ui.Text(label, x, y + U(6), Kit.Gilt, 1);
        var br = new Rectangle(x + w - U(64), y, U(64), U(18));
        if (ui.Button(br, val ? "ON" : "OFF", active: val) && !block) val = !val;
        return val;
    }
}
