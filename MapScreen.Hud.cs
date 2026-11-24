using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the always-on map interface (top bar, feed, overview, captain panel, company cards, tooltips) and modal helpers.
partial class MapScreen
{
    // ---------- interface ----------

    static Color SideColor(int side) => side == -1 ? Style.Dim : Style.Sides[Style.PaletteOf(side)].Ui;
    /// Who a party is, in the tooltip and the encounter panel.
    string Allegiance(Party p) => p.Kind switch
    {
        PartyKind.Bandit => "BRIGANDS",
        PartyKind.Player => "YOU",
        PartyKind.Caravan => p.Breed == "pilgrims" ? "PILGRIMS" : $"MERCHANTS OF THE {C.FactionNames[p.Faction].ToUpperInvariant()}",
        PartyKind.Monster => p.Lair ? $"{p.Breed.ToUpperInvariant()} {Campaign.LairWord(p.Breed)}" : Campaign.Plural(p.Breed).ToUpperInvariant(),
        _ when C.OrderOf(p) is OrderDef o => $"{o.Name.ToUpperInvariant()}, FOR THE {C.FactionNames[p.Faction].ToUpperInvariant()}",
        _ => C.FactionNames[p.Faction].ToUpperInvariant(),
    };
    Color PartyColor(Party p) => p.Kind == PartyKind.Player ? Style.Accent : SideColor(C.SideOf(p));

    PanelKind lastPanel;
    float panelSince;

    void DrawUi()
    {
        if (bare) return;
        bool click = pendingClick;
        pendingClick = false;
        ui.Begin(gd.Viewport, ms.Position, click);
        try
        {
            // A panel has the screen to itself (docs/archive/PLAYTEST.md PT2a): the map's labels, clock, feed, minimap, captain's card,
            // company cards and contracts are drawn only while no panel is open (the panels show the crowns and men they need).
            if (panel == PanelKind.None)
            {
                Flags();
                TopBar();
                Feed();
                NextChip();
                Overview();
                CaptainPanel();
                Cards();
                Contracts();
                QuestMark();
                TutorialCard();
            }
            if (panel == PanelKind.None) { Tooltip(); Hints(); }
            switch (panel)
            {
                case PanelKind.Encounter: EncounterPanel(); break;
                case PanelKind.Settlement: SettlementPanel(); break;
                case PanelKind.Party: PartyPanel(); break;
                case PanelKind.Upgrade: UpgradePanel(); break;
                case PanelKind.Roll: RollPanel(); break;
                case PanelKind.Results: ResultsPanel(); break;
                case PanelKind.Menu: MenuPanel(); break;
                case PanelKind.Saves: SavesPanel(); break;
                case PanelKind.GameOver: GameOverPanel(); break;
                case PanelKind.Siege: SiegePanel(); break;
                case PanelKind.Characters: CharactersPanel(); break;
                case PanelKind.Talk: TalkPanel(); break;
                case PanelKind.Skills: SkillsPanel(); break;
                case PanelKind.Realm: RealmPanel(); break;
                case PanelKind.Beat: BeatPanel(); break;
                case PanelKind.Event: EventPanel(); break;
                case PanelKind.Divisions: DivisionsPanel(); break;
                case PanelKind.Quests: QuestsPanel(); break;
                case PanelKind.Atlas: AtlasPanel(); break;
                case PanelKind.Inventory: InventoryPanel(); break;
                case PanelKind.Camp: CampPanel(); break;
                case PanelKind.Fallen: BookPanel(); break;
                case PanelKind.Help: HelpPanel(); break;
            }
            if (panel != PanelKind.None && modalIn < 1) ui.Rect(modalRect, Kit.IronDark * (1 - modalIn)); // the panel's entrance: it comes up out of the dark
            if (panel != lastPanel) { lastPanel = panel; panelSince = Ui.Now; }
            // Tips draw last so nothing covers them; over an open panel they sit at the top, clear of its buttons. A tip that was
            // already waiting when the panel opened keeps waiting for the map (only tips about the panel itself show over it).
            if (panel == PanelKind.None) Tips.Draw(ui, ui.U(10));
            else { TutorialStrip(); if (Tips.Since >= panelSince) Tips.DrawAt(ui, ui.U(6)); }
        }
        finally { ui.End(); }
        if (panel == PanelKind.None && click && !ui.Blocks(ms.Position)) pendingClick = true; // the map gets it
    }

    Vector2? Screen(Vector3 w)
    {
        var p = gd.Viewport.Project(w, proj, view, Matrix.Identity);
        return p.Z is > 0 and < 1 ? new Vector2(p.X, p.Y) : null;
    }

    /// One map label waiting to be placed: lower Pri wins a crowded spot. Party labels that lose to another party's label fold
    /// into it as "+N" (S7).
    sealed class Label
    {
        public int Pri, More;
        public Rectangle R;
        public bool Party;
        public Action<Label> Draw = null!;
    }

    void Flags()
    {
        var all = new List<Label>();
        var pl = C.Player;
        int target = pl.ChaseId >= 0 ? pl.ChaseId : -1, goalTown = pl.GoalSettlement;
        foreach (var s in C.S.Settlements)
        {
            if (Screen(G(s.Pos, s.Kind == SettlementKind.Village ? 6 : s.Size == 3 ? 15 : 11)) is not Vector2 sp) continue;
            // A hierarchy (docs/archive/HOLDS.md HD5): towns large, castles small, villages only close in. Pulled back, villages drop out
            // first, then tower houses, and towns shrink last.
            if (s.Kind == SettlementKind.Village && zoom > 150 && s.Id != goalTown) continue;
            if (s.Size == 1 && zoom > 300 && s.Id != goalTown) continue;
            var col = SideColor(s.Faction);
            string label = s.Name.ToUpperInvariant() + (s.Besieged ? " (BESIEGED)" : "") + (s.Owner == C.Player.Id ? " *" : "");
            int scale = s.Kind == SettlementKind.Town && zoom <= 450 ? 2 : 1;
            int w = ui.TW(label, scale) + ui.U(10);
            var r = new Rectangle((int)sp.X - w / 2, (int)sp.Y - ui.U(scale == 2 ? 18 : 13), w, ui.U(scale == 2 ? 18 : 13));
            all.Add(new Label
            {
                Pri = s.Id == goalTown ? 0 : s.Kind switch { SettlementKind.Town => 1, SettlementKind.Village => 4, _ => s.Size == 1 ? 3 : 2 }, R = r,
                Draw = l =>
                {
                    ui.Rect(l.R, Color.Black * 0.45f);
                    ui.Rect(new Rectangle(l.R.X, l.R.Bottom - ui.U(2), l.R.Width, ui.U(2)), col);
                    ui.TextC(label, l.R.Center.X, l.R.Y + ui.U(3), Color.White, scale);
                },
            });
        }
        // Sites: small pale labels, only close in; a spent one is dimmed, one you have never visited is marked "?".
        if (zoom <= 200)
            foreach (var site in C.Sites)
            {
                if (Screen(G(site.Pos, 5)) is not Vector2 sp) continue;
                bool ready = C.SiteReady(site);
                string label = site.Name.ToUpperInvariant() + (C.SiteSeen(site) ? "" : " ?");
                int w = ui.TW(label, 1) + ui.U(8);
                all.Add(new Label
                {
                    Pri = site.Id == C.S.GoalSite ? 0 : 5, R = new Rectangle((int)sp.X - w / 2, (int)sp.Y - ui.U(12), w, ui.U(12)),
                    Draw = l => { ui.Rect(l.R, Color.Black * (ready ? 0.4f : 0.25f)); ui.TextC(label, l.R.Center.X, l.R.Y + ui.U(2), ready ? Kit.Parchment : Style.Dim, 1); },
                });
            }
        // Smoke rises over villages that were threatened or stripped.
        foreach (var v in C.S.Settlements.Where(v => v.ThreatUntil > C.S.Hours))
            for (int i = 0; i < 6; i++)
            {
                float ph = (time * 0.25f + i / 6f) % 1;
                if (Screen(G(v.Pos + new Vector2(MathF.Sin(ph * 3 + i) * 1.5f, ph * 2), 3 + ph * 14)) is not Vector2 sp) continue;
                int r = ui.U(4 + ph * 10 * 110 / MathF.Max(40, zoom));
                ui.Rect(new Rectangle((int)sp.X - r / 2, (int)sp.Y - r / 2, r, r), new Color(70, 66, 62) * (0.75f * (1 - ph)));
            }
        if (zoom < 240) Life();
        foreach (var sk in C.S.Skirmishes)
        {
            if (Screen(G(sk.Pos, 10)) is not Vector2 sp) continue;
            var sideA = sk.SideA.Select(C.PartyById).Where(x => x != null && !x.Down).Cast<Party>().ToList();
            var sideB = sk.SideB.Select(C.PartyById).Where(x => x != null && !x.Down).Cast<Party>().ToList();
            int mA = sideA.Sum(x => x.Men), mB = sideB.Sum(x => x.Men);
            if (mA <= 0 && mB <= 0) continue;
            string label = $"{mA} v {mB}";
            int w = Math.Max(ui.U(56), ui.TW(label, 1) + ui.U(14));
            Color colA = sideA.Count > 0 ? PartyColor(sideA[0]) : Style.Accent;
            Color colB = sideB.Count > 0 ? PartyColor(sideB[0]) : Style.Foe;
            all.Add(new Label
            {
                Pri = 1, Party = false, R = new Rectangle((int)sp.X - w / 2, (int)sp.Y - ui.U(16), w, ui.U(16)),
                Draw = l =>
                {
                    ui.Rect(l.R, Kit.IronDark * 0.94f);
                    ui.Frame(l.R, Kit.Gilt, ui.U(1));
                    ui.TextC(label, l.R.Center.X, l.R.Y + ui.U(3), Kit.Gilt, 1);
                    float frac = mA / MathF.Max(1f, mA + mB);
                    var bar = new Rectangle(l.R.X + ui.U(2), l.R.Bottom - ui.U(4), l.R.Width - ui.U(4), ui.U(2));
                    int split = (int)(bar.Width * frac);
                    ui.Rect(new Rectangle(bar.X, bar.Y, split, bar.Height), colA);
                    ui.Rect(new Rectangle(bar.X + split, bar.Y, bar.Width - split, bar.Height), colB);
                },
            });
        }
        foreach (var p in C.S.Parties)
        {
            if (p.Down || p.BattleId >= 0 || Screen(G(bearers.TryGetValue(p.Id, out var bu) ? bu.Pos : p.Pos, 9)) is not Vector2 sp) continue;
            bool mine = p == pl || p.Id == target || p == C.Encounter;
            // Far out, travellers' labels drop out unless they are your business.
            if (!mine && zoom > 260 && p.Kind == PartyKind.Caravan) continue;
            // Beyond your sight you see banners on the move, not whose (S7).
            bool seen = mine || C.InSight(p.Pos);
            if (!seen && zoom > 200) continue; // far out, unknown banners are only clutter
            string label = seen ? $"{p.Name.ToUpperInvariant()} {p.Men}" : "?";
            int w = ui.TW(label, 1) + ui.U(8);
            all.Add(new Label
            {
                Pri = mine ? 0 : !seen ? 6 : p.Kind is PartyKind.Lord ? 2 : 4, Party = true, R = new Rectangle((int)sp.X - w / 2, (int)sp.Y - ui.U(12), w, ui.U(12)),
                Draw = l =>
                {
                    string text = l.More > 0 ? $"{label} +{l.More}" : label;
                    var r = l.More > 0 ? new Rectangle(l.R.X, l.R.Y, ui.TW(text, 1) + ui.U(8), l.R.Height) : l.R;
                    r.X = Math.Min(r.X, Math.Max(ui.U(4), ui.W - ui.U(4) - r.Width));
                    ui.Rect(r, seen ? PartyColor(p) * 0.85f : Color.Black * 0.5f);
                    ui.TextC(text, r.Center.X, r.Y + ui.U(3), seen ? Color.Black : Style.Dim, 1);
                },
            });
        }
        // Place by priority (stable within one, so labels do not flicker), then draw.
        var placed = new List<Label>();
        var zones = HudZones().ToList();
        foreach (var l in all.OrderBy(l => l.Pri))
        {
            // A label cut by the edge slides in to stay whole on the screen; one wholly off it is not drawn.
            if (l.R.Right <= 0 || l.R.X >= ui.W) continue;
            if (l.Pri > 0 && zones.Any(z => z.Intersects(l.R))) continue; // under the HUD: your own goal and party stay
            l.R.X = Math.Clamp(l.R.X, ui.U(4), Math.Max(ui.U(4), ui.W - ui.U(4) - l.R.Width));
            var pad = new Rectangle(l.R.X - ui.U(2), l.R.Y - ui.U(1), l.R.Width + ui.U(4), l.R.Height + ui.U(2));
            var hit = l.Pri == 0 ? null : placed.FirstOrDefault(o => o.R.Intersects(pad));
            if (hit == null) placed.Add(l);
            else if (l.Party && hit.Party && hit.Pri > 0) hit.More++;
        }
        foreach (var l in placed.AsEnumerable().Reverse()) l.Draw(l);
    }

    /// The land lives: thin smoke from town chimneys, and dust kicked up behind parties on the move (docs/archive/VISUALS.md V4).
    void Life()
    {
        float fade = Math.Clamp((240 - zoom) / 80, 0, 1);
        foreach (var t in C.S.Settlements)
        {
            if (t.Kind == SettlementKind.Village && t.ThreatUntil > C.S.Hours) continue; // already burning
            int n = t.Kind == SettlementKind.Village ? 2 : 3;
            for (int i = 0; i < n; i++)
            {
                float ph = (time * 0.12f + i / (float)n + t.Id * 0.37f) % 1;
                var at = t.Pos + new Vector2((i - 1) * 1.6f + MathF.Sin(ph * 4 + t.Id) * 0.6f + ph * 2.5f, 0.8f * i);
                if (Screen(G(at, (t.Kind == SettlementKind.Village ? 4 : 7) + ph * 9)) is not Vector2 sp) continue;
                int r = Math.Max(1, ui.U((2 + ph * 6) * 110 / MathF.Max(40, zoom)));
                ui.Rect(new Rectangle((int)sp.X - r / 2, (int)sp.Y - r / 2, r, r), new Color(200, 196, 190) * (0.22f * (1 - ph) * fade));
            }
        }
        foreach (var p in C.S.Parties)
        {
            if (p.Down || !bearers.TryGetValue(p.Id, out var u) || u.Speed <= 0) continue;
            var back = -new Vector2(MathF.Cos(p.Facing), MathF.Sin(p.Facing));
            for (int i = 0; i < 4; i++)
            {
                float ph = (time * 1.4f + i / 4f + p.Id * 0.21f) % 1;
                if (Screen(G(p.Pos + back * (1.2f + ph * 3) + new Vector2(-back.Y, back.X) * MathF.Sin(i * 2.3f) * 0.6f, 0.4f + ph * 1.5f)) is not Vector2 sp) continue;
                int r = Math.Max(1, ui.U((2 + ph * 4) * 110 / MathF.Max(40, zoom)));
                ui.Rect(new Rectangle((int)sp.X - r / 2, (int)sp.Y - r / 2, r, r), new Color(150, 132, 104) * (0.35f * (1 - ph) * fade));
            }
        }
    }

    void TopBar()
    {
        int w = ui.U(480), x = (ui.W - w) / 2, y = ui.U(10);
        var r = new Rectangle(x, y, w, ui.U(46));
        ui.Panel(r);
        int hour = (int)(C.S.Hours % 24);
        ui.Text($"DAY {C.Day}  {hour:00}:00  {C.Season.ToUpperInvariant()}", x + ui.U(10), y + ui.U(8), C.Winter ? Style.Friendly : Style.Text);
        int me = Math.Max(0, C.Captain.Contract);
        var foes = Enumerable.Range(0, C.S.Factions.Count).Where(f => C.FactionsAtWar(me, f)).Select(f => C.FactionNames[f].Split(' ').Last().ToUpperInvariant()).ToList();
        string war = C.Captain.Contract < 0 ? "FREE CAPTAIN" : foes.Count > 0 ? "WAR: " + string.Join(", ", foes) : "AT PEACE";
        ui.Text(war, x + ui.U(10), y + ui.U(28), foes.Count > 0 ? Style.Foe : Style.Ok, 1);
        string[] labels = { "II", ">", ">>", ">>>" };
        int[] speeds = { 0, 1, 3, 8 };
        for (int i = 0; i < 4; i++)
            if (ui.Button(new Rectangle(x + w - ui.U(4 + (4 - i) * 46), y + ui.U(6), ui.U(42), ui.U(22)), labels[i], true, "", speed == speeds[i])) speed = speeds[i];
        ui.Text("UPKEEP", x + w - ui.U(188), y + ui.U(32), Style.Dim, 1);
        ui.Bar(new Rectangle(x + w - ui.U(140), y + ui.U(33), ui.U(134), ui.U(5)), (float)(C.S.Hours % 168 / 168), Style.Accent);
    }

    void Feed()
    {
        int y = ui.U(10), room = Math.Max(ui.U(80), (ui.W - ui.U(480)) / 2 - ui.U(30));
        // The log's level: a small switch above the lines (click to go from your own news, to the realm's, to everything).
        var level = Settings.Current.LogLevel;
        string[] names = { "YOURS", "NOTABLE", "EVERYTHING" };
        if (ui.Button(new Rectangle(ui.U(10), y - ui.U(4), ui.U(118), ui.U(15)), $"LOG: {names[(int)level]}"))
        {
            Settings.Current.LogLevel = (LogLevel)(((int)level + 1) % 3);
            Settings.Current.Save();
        }
        y += ui.U(18);
        // The three newest items from the last three days (older news is in the realm's chronicle, R), four lines at most;
        // an item too long for one line wraps to a second rather than being cut mid-sentence.
        var rows = new List<(string Text, Color C)>();
        foreach (var e in C.S.Log.Where(e => e.Level <= Settings.Current.LogLevel && C.S.Hours - e.At < 72).Reverse().Take(3))
        {
            float age = (float)(C.S.Hours - e.At) / 24;
            var c = Color.White * Math.Clamp(1.4f - age * 0.25f, 0.45f, 1);
            var wrapped = ui.Wrap(e.Text.ToUpperInvariant(), room);
            if (rows.Count + wrapped.Count > 4) break;
            for (int i = 0; i < wrapped.Count; i++) rows.Add(((i > 0 ? "  " : "") + wrapped[i], c));
        }
        if (rows.Count > 0)
        {
            // One dark plate behind the lines, dark enough that map names do not show through it.
            ui.Rect(new Rectangle(ui.U(10), y - ui.U(5), rows.Max(r => ui.TW(r.Text, 1)) + ui.U(12), rows.Count * ui.U(13) + ui.U(6)), Color.Black * 0.75f);
            foreach (var (t, c) in rows) { ui.Text(t, ui.U(16), y, c, 1); y += ui.U(13); }
        }
        feedBottom = y;
    }

    void Overview()
    {
        int size = ui.U(170);
        var r = new Rectangle(ui.W - size - ui.U(10), ui.U(10), size, size);
        ui.Panel(new Rectangle(r.X - ui.U(3), r.Y - ui.U(3), r.Width + ui.U(6), r.Height + ui.U(6)));
        ui.Sb.Draw(world!.Minimap, r, Color.White);
        Vector2 M(Vector2 w) { var m = World.WorldToMinimap(w, terrain.MinimapRange); return new Vector2(r.X + m.X * r.Width, r.Y + m.Y * r.Height); }
        foreach (var s in C.S.Settlements.Where(s => s.Kind != SettlementKind.Village))
        {
            var p = M(s.Pos);
            ui.Rect(new Rectangle((int)p.X - ui.U(2), (int)p.Y - ui.U(2), ui.U(5), ui.U(5)), SideColor(s.Faction));
        }
        foreach (var p in C.S.Parties.Where(p => !p.Down))
        {
            var q = M(p.Pos);
            int d = ui.U(p.Kind == PartyKind.Player ? 5 : 3);
            ui.Rect(new Rectangle((int)q.X - d / 2, (int)q.Y - d / 2, d, d), PartyColor(p));
        }
        ui.Frame(r, Style.PanelEdge, ui.U(1));
        if (ui.Clicked(r))
        {
            focus = World.MinimapToWorld(new Vector2((ui.Mouse.X - r.X) / (float)r.Width, (ui.Mouse.Y - r.Y) / (float)r.Height), terrain.MinimapRange);
            follow = false;
        }
    }

    /// The captain's card at the bottom left (docs/archive/PLAYTEST.md PT2b): rank and renown with its bar, the purse, and one row of
    /// men, food and horses. What renown buys next and whom you serve are on hover; the party screen (P) has the rest, and
    /// the next step is the chip under the clock (`NextChip`).
    void CaptainPanel()
    {
        var cap = C.Captain;
        bool wounded = C.S.Hours < cap.WoundedUntil;
        string? status = wounded ? $"WOUNDED: {(cap.WoundedUntil - C.S.Hours) / 24:0.0} DAYS"
            : C.Besieging is Settlement sg ? $"BESIEGING {sg.Name.ToUpperInvariant()} ({ctl.First(Act.SiegeScreen)})" : null;
        int h = ui.U(status != null ? 92 : 78);
        var r = new Rectangle(ui.U(10), ui.H - h - ui.U(10), ui.U(280), h);
        captainRect = r;
        ui.Panel(r);
        BuffChips(r);
        int x = r.X + ui.U(10), y = r.Y + ui.U(8), inner = r.Width - ui.U(20);
        var (unlock, at) = Rules.NextRenownUnlock(cap);
        string renown = $"RENOWN {cap.Renown:0} / {at:0}", title = "CAPTAIN  " + C.Stage;
        int room = inner - ui.U(8) - ui.TW(renown, 1), ts = ui.FitScale(title, room);
        ui.Text(ui.Fit(title, room, ts), x, y + (ts == 1 ? ui.U(3) : 0), Style.Accent, ts);
        ui.TextR(renown, r.Right - ui.U(10), y + ui.U(2), Style.Text, 1);
        // Renown is the campaign's score: the bar runs to the next thing it buys, named on hover.
        y += ui.U(17);
        ui.Bar(new Rectangle(x, y, inner, ui.U(4)), cap.Renown / MathF.Max(1, at), Style.Accent); // matches the numbers beside it
        var top = new Rectangle(r.X, r.Y, r.Width, ui.U(31));
        string contract = cap.Contract >= 0 ? $"{C.Stage} OF THE {C.FactionNames[cap.Contract].ToUpperInvariant()}" : "FREE COMPANY: NO PAY";
        if (ui.Hover(top)) ui.Tooltip(r.X + r.Width / 2, r.Y - ui.U(18), contract, $"NEXT: {unlock}", $"RENOWN {cap.Renown:0} / {at:0}");
        y += ui.U(10);
        float bal = C.S.LastWeekBalance;
        ui.Icon("coin", x, y + ui.U(1), Kit.Gilt, 2);
        ui.Text($"{C.S.Gold:0}", x + ui.U(20), y, Style.Text);
        ui.TextR($"{(bal >= 0 ? "+" : "")}{bal:0} / WEEK", r.Right - ui.U(10), y + ui.U(4), MathF.Abs(bal) < 0.5f ? Style.Dim : bal > 0 ? Style.Ok : Style.Bad, 1);
        y += ui.U(21);
        // One row: men against the cap, days of food, horses (and prisoners, when you hold any).
        int limit = Rules.MenLimit(cap), sx = x;
        void Stat(string icon, string text, Color c, string tip, string line)
        {
            int w = ui.TW(text, 1) + ui.U(18);
            var hit = new Rectangle(sx, y - ui.U(2), w, ui.U(17));
            ui.Icon(icon, sx, y, c, 2);
            ui.Text(text, sx + ui.U(18), y + ui.U(4), c, 1);
            if (ui.Hover(hit)) ui.Tooltip(hit.Center.X, hit.Y - ui.U(2), tip, line);
            sx += w + ui.U(12);
        }
        Stat("flag", C.Player.Wounded > 0 ? $"{C.Player.Men}+{C.Player.Wounded}W / {limit}" : $"{C.Player.Men} / {limit}", Style.Text,
            "MEN", $"{C.Player.Companies.Count} OF {Rules.CompanySlots(cap)} COMPANIES. {limit} MEN AT MOST.");
        Stat("bread", $"{C.Player.Food:0} DAYS", C.Player.Food < 3 ? Style.Bad : Style.Text, "FOOD", "DAYS OF FOOD LEFT. BUY MORE IN A TOWN.");
        Stat("horse", $"{C.Player.Horses}", Style.Text, "SPARE HORSES", "FOR MEN PROMOTED INTO CAVALRY.");
        if (C.PrisonerCount > 0) Stat("chain", $"{C.PrisonerCount}", Style.Text, "PRISONERS", "SELL OR RANSOM THEM IN A TOWN.");
        if (status != null)
        {
            // Besieging: the line is a button to the siege screen.
            var sr = new Rectangle(x - ui.U(3), y + ui.U(16), inner + ui.U(6), ui.U(13));
            bool siege = !wounded && C.Besieging is Settlement && C.Player.SiegeTarget >= 0 && !C.Player.Moving && C.Encounter == null && panel == PanelKind.None;
            if (siege && ui.Hover(sr)) ui.Rect(sr, Style.Warn * 0.2f);
            ui.TextFit(siege ? status.Replace($"({ctl.First(Act.SiegeScreen)})", "> OPEN") : status, x, y + ui.U(19), inner, wounded ? Style.Bad : Style.Warn, 1);
            if (siege && ui.Clicked(sr) && C.Besieging is Settlement bs) { this.at = bs; panel = PanelKind.Siege; }
        }
    }

    /// The one thing to do next (`Campaign.NextStep`), as a single chip under the clock.
    void NextChip()
    {
        string t = "NEXT: " + C.NextStep().Text.ToUpperInvariant();
        int maxW = ui.U(480);
        t = ui.Fit(t, maxW - ui.U(24));
        int w = ui.TW(t, 1) + ui.U(24), x = (ui.W - w) / 2, y = ui.U(63);
        var r = new Rectangle(x, y, w, ui.U(16));
        nextRect = r;
        ui.Rect(r, Color.Black * 0.75f);
        ui.Frame(r, Kit.Mul(Kit.Gilt, 0.8f), ui.U(1));
        ui.Icon("star", x + ui.U(5), y + ui.U(4), Kit.Gilt, 1);
        ui.Text(t, x + ui.U(16), y + ui.U(5), Style.Accent, 1);
    }

    Rectangle captainRect, nextRect;
    int feedBottom;

    /// Where the map's HUD sits this frame: a map label under any of these is not drawn, so names never peek out from
    /// behind the feed, the clock or the cards.
    IEnumerable<Rectangle> HudZones()
    {
        yield return new Rectangle(0, 0, ui.U(400), feedBottom);
        yield return new Rectangle((ui.W - ui.U(480)) / 2, 0, ui.U(480), ui.U(60));
        yield return nextRect;
        yield return new Rectangle(ui.W - ui.U(200), 0, ui.U(200), ui.U(380));
        yield return new Rectangle(captainRect.X, captainRect.Y - ui.U(18), captainRect.Width, captainRect.Height + ui.U(18));
        yield return new Rectangle(ui.U(300), ui.H - ui.U(132), ui.W - ui.U(500), ui.U(132));
    }

    /// What the company carries into its next battle (Buffs.cs, Rites.cs): a chip each above the captain's panel, red when it hurts;
    /// hover one for what it does and the days left.
    void BuffChips(Rectangle r)
    {
        int x = r.X, y = r.Y - ui.U(17);
        var ids = C.ActiveBuffs.ToList();
        if (C.RelicReady) ids.Add("relic");
        foreach (var id in ids)
        {
            var e = Effects.Get(id);
            string text = e.Name.ToUpperInvariant() + (id == "relic" ? $" {C.S.RelicCharges}" : C.Lasting(id) ? "" : $" {C.BuffDays(id):0}D");
            if (x + ui.TW(text, 1) + ui.U(20) > r.Right) break;
            int w = ui.Chip(x, y, text, e.Harmful ? Style.Bad : Kit.Gilt, e.Icon == "" ? null : e.Icon);
            if (ui.Hover(new Rectangle(x, y, w, ui.U(13)))) note = e.Line.ToUpperInvariant() + (C.Lasting(id) ? " LASTS THE WHOLE CAMPAIGN." : "");
            x += w + ui.U(4);
        }
    }

    /// How much renown the last rung of the ladder covered, so the bar runs from there to the next unlock.
    static float RenownStep(CaptainState cap, float next) => MathF.Min(cap.Renown, MathF.Max(0, next - Balance.Get().MenRenownDivisor));

    void Cards()
    {
        var list = C.Player.Companies;
        int cw = list.Count >= 10 ? ui.U(88) : ui.U(96), ch = ui.U(108), gap = list.Count >= 10 ? ui.U(4) : ui.U(6), total = list.Count * cw + Math.Max(0, list.Count - 1) * gap;
        int x0 = Math.Max(ui.U(300), (ui.W - total) / 2);
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            var r = new Rectangle(x0 + i * (cw + gap), ui.H - ch - ui.U(10), cw, ch);
            if (r.Right > ui.W - ui.U(10)) break;
            Card(c, r, i + 1);
            if (panel == PanelKind.None && ui.Clicked(r)) panel = PanelKind.Party;
        }
    }

    void Card(CompanyRecord c, Rectangle r, int index)
    {
        int ready = c.Ready;
        var main = c.Main;
        ui.Panel(r, ready > 0 ? Style.Accent : Style.PanelEdge, ready > 0 ? 2 : 1);
        ui.Text($"{index}", r.X + ui.U(5), r.Y + ui.U(5), Style.Dim, 1);
        ui.Text(Style.Numerals[Math.Clamp(c.Type.Tier, 0, 7)], r.X + ui.U(5), r.Y + ui.U(16), Style.Accent, 1);
        ui.Portrait(c.Type, new Rectangle(r.X + ui.U(20), r.Y + ui.U(4), r.Width - ui.U(40), ui.U(44)));
        string name = c.Name.ToUpperInvariant();
        ui.TextC(name.Length > 15 ? name[..15] : name, r.X + r.Width / 2f, r.Y + ui.U(50), Style.Text, 1);
        ui.TextC(c.Stacks.Count > 1 ? $"{c.Type.Name.ToUpperInvariant()} +{c.Stacks.Count - 1}" : c.Type.Name.ToUpperInvariant(), r.X + r.Width / 2f, r.Y + ui.U(60), Style.Dim, 1);
        ui.TextC(c.Wounded > 0 ? $"{c.Men}+{c.Wounded}W" : $"{c.Men}", r.X + r.Width / 2f, r.Y + ui.U(71), Style.Text);
        TierStrip(c, new Rectangle(r.X + ui.U(6), r.Y + ui.U(86), r.Width - ui.U(12), ui.U(3)));
        float cap = c.Stacks.Sum(x => x.PoolCap), pool = c.Stacks.Sum(x => x.Xp);
        ui.Bar(new Rectangle(r.X + ui.U(6), r.Y + ui.U(91), r.Width - ui.U(12), ui.U(4)), cap > 0 ? pool / cap : 1, ready > 0 ? Style.Accent : Style.Friendly);
        ui.Bar(new Rectangle(r.X + ui.U(6), r.Y + ui.U(98), r.Width - ui.U(12), ui.U(3)), c.Cohesion / 30f, Style.Ok);
        if (ready > 0) ui.TextC($"{ready} READY", r.X + r.Width / 2f, r.Y - ui.U(11), Style.Accent, 1);
    }

    /// A strip split by the company's men per tier, darker to brighter, so a mixed company reads at a glance.
    void TierStrip(CompanyRecord c, Rectangle r)
    {
        int total = Math.Max(1, c.Men + c.Wounded), x = r.X;
        foreach (var st in c.Stacks.OrderBy(t => t.Type.Tier))
        {
            int w = (int)MathF.Round(r.Width * (st.Men + st.Wounded) / (float)total);
            if (w <= 0) continue;
            ui.Rect(new Rectangle(x, r.Y, Math.Min(w, r.Right - x), r.Height), Color.Lerp(Style.Dim, Style.Accent, (st.Type.Tier - 1) / 6f));
            x += w;
        }
    }

    /// The screens a mouse can reach: a rail of buttons down the right edge under the minimap, each with its key, so the captain's
    /// card at the bottom left is only the captain.
    void Hints()
    {
        var row = new (string Label, Act Key, PanelKind Panel)[]
        {
            ("COMPANY", Act.PartyScreen, PanelKind.Party), ("SERGEANTS", Act.DivisionsScreen, PanelKind.Divisions), ("CAPTAIN", Act.SkillsScreen, PanelKind.Skills), ("KIT", Act.InventoryScreen, PanelKind.Inventory), ("CAMP", Act.CampScreen, PanelKind.Camp),
            ("QUESTS", Act.QuestsScreen, PanelKind.Quests), ("LORDS", Act.LordsScreen, PanelKind.Characters), ("REALMS", Act.RealmScreen, PanelKind.Realm), ("MAP", Act.AtlasScreen, PanelKind.Atlas), ("FIND ME", Act.FollowParty, PanelKind.None), ("KEYS", Act.Help, PanelKind.Help),
        };
        int h = ui.U(20), w = row.Max(r => ui.TW(r.Label, 1) + ui.TW(ctl.First(r.Key), 1)) + ui.U(26);
        int x = ui.W - w - ui.U(10), y = ui.U(10 + 170 + 12); // under the minimap, which is 170 high at the top right
        for (int i = 0; i < row.Length; i++)
        {
            var (label, key, kind) = row[i];
            bool lit = kind == PanelKind.None && follow;
            if (ui.Button(new Rectangle(x, y, w, h), label, !lit, ctl.First(key)))
            {
                if (kind == PanelKind.None) follow = true; // look at your company and keep it in view
                else if (kind == PanelKind.Help) panel = kind; else OpenScreen(kind);
            }
            y += h + ui.U(3);
        }
    }

    /// The map's keys (F1), laid out like the battle's controls.
    void HelpPanel()
    {
        var r = Modal(860, 470, "CONTROLS");
        string Pan() => $"{ctl.First(Act.Forward)}{ctl.First(Act.Left)}{ctl.First(Act.Back)}{ctl.First(Act.Right)}";
        var cols = new (string Title, (string Label, string Key)[] Rows)[]
        {
            ("ON THE MAP", new (string, string)[]
            {
                ("MARCH / ENTER / CHASE", "LMB"), ("PAN", $"{Pan()} / SCREEN EDGES"), ("TURN", $"{ctl.First(Act.RotateLeft)} {ctl.First(Act.RotateRight)} / MMB DRAG"),
                ("ZOOM", "WHEEL"), (Controls.Labels[Act.FollowParty], ctl.Name(Act.FollowParty)), ("PAUSE", ctl.Name(Act.Pause)),
                ("SPEED X1 / X3 / X8", $"{ctl.First(Act.Speed1)} {ctl.First(Act.Speed2)} {ctl.First(Act.Speed3)}"), ("LOOK THERE", "CLICK THE MINIMAP"),
            }),
            ("SCREENS", new[] { Act.PartyScreen, Act.SkillsScreen, Act.LordsScreen, Act.RealmScreen, Act.DivisionsScreen, Act.AtlasScreen, Act.InventoryScreen, Act.QuestsScreen, Act.CampScreen, Act.SiegeScreen }
                .Select(a => (Controls.Labels[a].Replace(" (MAP)", ""), ctl.Name(a))).ToArray()),
            ("THE GAME", new[] { Act.QuickSave, Act.QuickLoad, Act.Menu, Act.Help, Act.Graphics }
                .Select(a => (Controls.Labels[a], ctl.Name(a))).Append(("DISMISS A TIP", "ENTER")).ToArray()),
        };
        int cw = (r.Width - ui.U(40)) / 3;
        for (int i = 0; i < cols.Length; i++)
        {
            int x = r.X + ui.U(20) + i * cw, y = r.Y + ui.U(30);
            ui.Text(cols[i].Title, x, y, Kit.Gilt);
            y += ui.U(22);
            foreach (var (label, key) in cols[i].Rows)
            {
                ui.Text(label, x, y, Style.Dim, 1);
                ui.Text(key, x, y + ui.U(9), Style.Text, 1);
                y += ui.U(24);
            }
        }
        ui.TextC($"REBIND IN SETTINGS (MENU) OR {Controls.FileName.ToUpperInvariant()}", r.Center.X, r.Bottom - ui.U(56), Style.Dim, 1);
        if (ui.Button(new Rectangle(r.Center.X - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.Help))) panel = PanelKind.None;
    }

    void Tooltip()
    {
        if (ui.Blocks(ms.Position)) return;
        var (p, s, site, _) = hover;
        Cursors.Want = p != null ? (C.Hostile(C.Player, p) ? CursorKind.Attack : CursorKind.Hand) : s != null || site != null ? CursorKind.Hand : CursorKind.Move;
        if (p == null && s == null && site == null) return;
        var linesOut = new List<(string, Color)>();
        if (site != null)
        {
            linesOut.Add((site.Name.ToUpperInvariant(), Kit.Parchment));
            linesOut.Add((site.What, Style.Dim));
            linesOut.Add((C.S.Provinces[site.Province].Title.ToUpperInvariant(), Style.Dim));
            string wait = C.SiteWait(site);
            linesOut.Add((wait != "" ? wait : C.SiteSeen(site) ? "CLICK TO GO THERE" : "NOT YET VISITED: CLICK TO GO THERE", wait != "" ? Style.Dim : Style.Ok));
        }
        else if (p != null && p != C.Player && p != C.Encounter && p.Id != C.Player.ChaseId && !C.InSight(p.Pos))
        {
            linesOut.Add(("BANNERS IN THE DISTANCE", Style.Dim));
            linesOut.Add(("TOO FAR TO MAKE OUT. RIDE CLOSER, OR TAKE A SCOUT.", Style.Dim));
        }
        else if (p != null)
        {
            linesOut.Add((p.Name.ToUpperInvariant(), PartyColor(p)));
            linesOut.Add((Allegiance(p), Style.Dim));
            if (p.Lair) linesOut.Add(($"A HOARD OF ABOUT {MathF.Round(p.Gold / 50) * 50:0} CROWNS", Style.Warn));
            else if (p.Breed == "troll") linesOut.Add((p.Task == "rest" ? "ASLEEP UNTIL DUSK" : p.Task == "hunt" ? "ON THE HUNT" : p.Task == "ravage" && p.RaidOn >= 0 ? $"OUT TO RAVAGE {C.SettlementById(p.RaidOn).Name.ToUpperInvariant()}" : "ABROAD IN THE DARK", Style.Warn));
            else if (p.Breed == "undead") linesOut.Add((p.Task == "rest" ? "LIES IN THE EARTH BY DAY" : p.Task == "hunt" ? "HUNTS THE LIVING" : "WALKS IN THE DARK", Style.Warn));
            else if (p.Breed == "wolf") linesOut.Add((p.Task == "rest" ? "LYING UP IN THE WOODS" : p.Task == "hunt" ? "ON THE HUNT" : "ON THE PROWL", Style.Warn));
            else if (p.Breed == "wyvern") linesOut.Add((p.Task == "rest" ? "ROOSTING UNTIL DAWN" : p.Task == "hunt" ? "STOOPING ON ITS PREY" : p.Task == "ravage" && p.RaidOn >= 0 ? $"FLYING AT {C.SettlementById(p.RaidOn).Name.ToUpperInvariant()}" : "ON THE WING", Style.Warn));
            else if (p.Breed == "bear") linesOut.Add((p.Task == "rest" ? "ASLEEP IN ITS DEN" : p.Task == "hunt" ? "CHARGING" : "KEEPS ITS COUNTRY. DO NOT GO NEAR.", Style.Warn));
            else if (p.Kind == PartyKind.Monster && p.Task == "ravage" && p.RaidOn >= 0) linesOut.Add(($"OUT TO RAVAGE {C.SettlementById(p.RaidOn).Name.ToUpperInvariant()}", Style.Warn));
            else if (p.Kind == PartyKind.Monster && p.Task == "raid" && p.RaidOn >= 0) linesOut.Add(($"OUT TO PLUNDER {C.SettlementById(p.RaidOn).Name.ToUpperInvariant()}", Style.Warn));
            linesOut.Add(($"{p.Men} MEN IN {p.Companies.Count} COMPANIES", Style.Text));
            if (p != C.Player)
            {
                float ratio = Rules.Strength(p) / MathF.Max(1, Rules.Strength(C.Player));
                linesOut.Add((ratio > 1.25f ? "STRONGER THAN YOU" : ratio < 0.8f ? "WEAKER THAN YOU" : "A FAIR MATCH", ratio > 1.25f ? Style.Bad : ratio < 0.8f ? Style.Ok : Style.Warn));
                linesOut.Add((C.Hostile(p, C.Player) ? "HOSTILE" : "NOT HOSTILE", C.Hostile(p, C.Player) ? Style.Foe : Style.Dim));
                if (p.Kind == PartyKind.Lord) linesOut.Add(($"TASK: {p.Task.ToUpperInvariant()}", Style.Dim));
                if (C.ArmyOf(p) is LordArmy army && C.PartyById(army.Leader) is Party chief && C.S.Settlements.FirstOrDefault(s => s.Id == army.Target) is Settlement goal)
                    linesOut.Add(((chief == p ? $"LEADS AN ARMY OF {army.Members.Count}" : $"RIDES IN {chief.Name.ToUpperInvariant()}'S ARMY") + (army.Aim == "relieve" ? $" TO RELIEVE {goal.Name.ToUpperInvariant()}" : $" ON {goal.Name.ToUpperInvariant()}"), Style.Warn));
                if (C.CharacterOf(p) is Character pc)
                {
                    float op = C.OpinionOfYou(pc);
                    linesOut.Add(($"{string.Join(", ", pc.Traits).ToUpperInvariant()}", Style.Dim));
                    linesOut.Add(($"THINKS {OpinionWord(op)} OF YOU ({op:+0;-0;0})", OpinionColor(op)));
                }
            }
        }
        else
        {
            linesOut.Add((s!.Name.ToUpperInvariant(), SideColor(s.Faction)));
            linesOut.Add(($"{s.KindWord} OF THE {C.FactionNames[s.Faction].ToUpperInvariant()}", Style.Dim));
            linesOut.Add(($"{C.S.Provinces[s.Province].Title.ToUpperInvariant()}, {s.Culture.ToUpperInvariant()} PEOPLE", Style.Dim));
            float pw = C.WildnessOf(s.Province);
            if (pw >= 25) linesOut.Add(($"WILDNESS: {Campaign.WildnessWord(pw)} ({pw:0}%)", pw >= 50 ? Style.Bad : Style.Warn));
            if (s.Kind == SettlementKind.Village) linesOut.Add((s.ThreatUntil > C.S.Hours ? "THREATENED: NO RECRUITS" : $"{s.Recruits} RECRUITS", Style.Text));
            if (s.Kind == SettlementKind.Town) linesOut.Add(($"{s.Mercenaries.Count} MERCENARY COMPANIES, {s.Horses} HORSES", Style.Text));
            linesOut.Add(($"PROSPERITY {s.Prosperity:0}" + (s.Kind != SettlementKind.Village ? $"   UNREST {s.Unrest:0}" : ""), s.Unrest >= Campaign.RevoltAt ? Style.Bad : Style.Dim));
            if (s.Kind != SettlementKind.Village && s.Buildings.Count > 0)
                linesOut.Add((string.Join(", ", s.Buildings.Select(b => (Campaign.BuildingOf(b.Key)?.Name ?? b.Key) + (b.Value > 1 ? " " + Style.Numerals[b.Value] : ""))).ToUpperInvariant(), Style.Dim));
            if (s.Owner >= 0 && s.Owner != C.Player.Id && C.PartyById(s.Owner) is Party holder) linesOut.Add(($"FIEF OF {holder.Name.ToUpperInvariant()}", Style.Dim));
            if (C.IsPort(s)) linesOut.Add(("A PORT: SHIPS SAIL FROM ITS HARBOUR", Style.Text));
            if (C.Refuses(s)) linesOut.Add(("WILL NOT TRADE WITH YOU", Style.Foe));
        }
        int w = linesOut.Max(l => ui.TW(l.Item1, 1)) + ui.U(16), h = linesOut.Count * ui.U(13) + ui.U(10);
        var r = new Rectangle(Math.Min(ms.X + ui.U(16), ui.W - w - 4), Math.Min(ms.Y + ui.U(16), ui.H - h - 4), w, h);
        ui.Panel(r);
        for (int i = 0; i < linesOut.Count; i++) ui.Text(linesOut[i].Item1, r.X + ui.U(8), r.Y + ui.U(6) + i * ui.U(13), linesOut[i].Item2, 1);
    }

    /// The open modal and how far its entrance has run (0..1): the panel fades in from dark as it rises into place.
    Rectangle modalRect;
    float modalIn = 1;

    Rectangle Modal(int w, int h, string title, Color? tc = null)
    {
        ui.Dim(0.45f);
        int pw = Math.Min(ui.U(w), ui.W - ui.U(16)), ph = Math.Min(ui.U(h), ui.H - ui.U(28)); // never wider or taller than the screen
        float t = Ui.Since(HashCode.Combine(title, 0x30da1));
        modalIn = Ease.OutCubic(t / 0.12f);
        var r = new Rectangle((ui.W - pw) / 2, (ui.H - ph) / 2 + ui.U(6) + (int)((1 - modalIn) * ui.U(6)), pw, ph);
        modalRect = r;
        ui.PlateBody(r, Kit.Gilt);
        ui.Ribbon(r.Center.X, r.Y - ui.U(10), title, Kit.Mul(tc ?? Kit.Gilt, 0.66f));
        if (t < 0.7f) Kit.Shine(ui, new Rectangle(r.Center.X - ui.TW(title, 2) / 2 - ui.U(20), r.Y - ui.U(10), ui.TW(title, 2) + ui.U(40), ui.U(22)), t / 0.7f);
        return r;
    }

    void Note(Rectangle r)
    {
        if (note != "") ui.TextC(note, r.X + r.Width / 2f, r.Bottom - ui.U(54), Style.Warn, 1);
    }
}
