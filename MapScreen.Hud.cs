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
        PartyKind.Monster => p.Lair ? $"{p.Breed.ToUpperInvariant()} HOLDOUT" : Campaign.Plural(p.Breed).ToUpperInvariant(),
        _ => C.FactionNames[p.Faction].ToUpperInvariant(),
    };
    Color PartyColor(Party p) => p.Kind == PartyKind.Player ? Style.Accent : SideColor(C.SideOf(p));

    PanelKind lastPanel;
    float panelSince;

    void DrawUi()
    {
        bool click = pendingClick;
        pendingClick = false;
        ui.Begin(gd.Viewport, ms.Position, click);
        Flags();
        TopBar();
        Feed();
        Overview();
        CaptainPanel();
        Cards();
        Contracts();
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
            case PanelKind.GameOver: GameOverPanel(); break;
            case PanelKind.Siege: SiegePanel(); break;
            case PanelKind.Characters: CharactersPanel(); break;
            case PanelKind.Skills: SkillsPanel(); break;
            case PanelKind.Realm: RealmPanel(); break;
            case PanelKind.Beat: BeatPanel(); break;
            case PanelKind.Event: EventPanel(); break;
            case PanelKind.Divisions: DivisionsPanel(); break;
            case PanelKind.Atlas: AtlasPanel(); break;
            case PanelKind.Help: HelpPanel(); break;
        }
        if (panel != PanelKind.None && modalIn < 1) ui.Rect(modalRect, Kit.IronDark * (1 - modalIn)); // the panel's entrance: it comes up out of the dark
        if (panel != lastPanel) { lastPanel = panel; panelSince = Ui.Now; }
        // Tips draw last so nothing covers them; over an open panel they sit at the top, clear of its buttons. A tip that was
        // already waiting when the panel opened keeps waiting for the map (only tips about the panel itself show over it).
        if (panel == PanelKind.None) Tips.Draw(ui, ui.U(130));
        else if (Tips.Since >= panelSince) Tips.DrawAt(ui, ui.U(6));
        ui.End();
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
            if (Screen(G(s.Pos, s.Kind == SettlementKind.Village ? 6 : 11)) is not Vector2 sp) continue;
            // Pulled back over a bigger world, the small labels would collide: villages drop out first, then towns shrink.
            if (s.Kind == SettlementKind.Village && zoom > 230 && s.Id != goalTown) continue;
            var col = SideColor(s.Faction);
            string label = s.Name.ToUpperInvariant() + (s.Besieged ? " (BESIEGED)" : "") + (s.Owner == C.Player.Id ? " *" : "");
            int scale = s.Kind == SettlementKind.Village || zoom > 300 ? 1 : 2;
            int w = ui.TW(label, scale) + ui.U(10);
            var r = new Rectangle((int)sp.X - w / 2, (int)sp.Y - ui.U(scale == 2 ? 18 : 13), w, ui.U(scale == 2 ? 18 : 13));
            all.Add(new Label
            {
                Pri = s.Id == goalTown ? 0 : s.Kind == SettlementKind.Village ? 3 : 1, R = r,
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
        foreach (var p in C.S.Parties)
        {
            if (p.Down || Screen(G(bearers.TryGetValue(p.Id, out var bu) ? bu.Pos : p.Pos, 9)) is not Vector2 sp) continue;
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
        foreach (var l in all.OrderBy(l => l.Pri))
        {
            // A label cut by the edge slides in to stay whole on the screen; one wholly off it is not drawn.
            if (l.R.Right <= 0 || l.R.X >= ui.W) continue;
            l.R.X = Math.Clamp(l.R.X, ui.U(4), Math.Max(ui.U(4), ui.W - ui.U(4) - l.R.Width));
            var pad = new Rectangle(l.R.X - ui.U(2), l.R.Y - ui.U(1), l.R.Width + ui.U(4), l.R.Height + ui.U(2));
            var hit = l.Pri == 0 ? null : placed.FirstOrDefault(o => o.R.Intersects(pad));
            if (hit == null) placed.Add(l);
            else if (l.Party && hit.Party && hit.Pri > 0) hit.More++;
        }
        foreach (var l in placed.AsEnumerable().Reverse()) l.Draw(l);
    }

    /// The land lives: thin smoke from town chimneys, and dust kicked up behind parties on the move (docs/VISUALS.md V4).
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
        foreach (var e in C.S.Log.AsEnumerable().Reverse().Take(6))
        {
            float age = (float)(C.S.Hours - e.At) / 24;
            var c = Color.White * Math.Clamp(1.4f - age * 0.25f, 0.45f, 1);
            string t = ui.Fit(e.Text.ToUpperInvariant(), room);
            // Dark enough that map names under the log do not show through it.
            ui.Rect(new Rectangle(ui.U(10), y - ui.U(3), ui.TW(t, 1) + ui.U(10), ui.U(13)), Color.Black * 0.62f);
            ui.Text(t, ui.U(15), y, c, 1);
            y += ui.U(15);
        }
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

    void CaptainPanel()
    {
        var cap = C.Captain;
        var r = new Rectangle(ui.U(10), ui.H - ui.U(146), ui.U(300), ui.U(136));
        ui.Panel(r);
        BuffChips(r);
        int x = r.X + ui.U(10), y = r.Y + ui.U(8);
        string renown = $"RENOWN {cap.Renown:0}", title = "CAPTAIN  " + C.Stage;
        int room = r.Width - ui.U(28) - ui.TW(renown, 1), ts = ui.FitScale(title, room);
        ui.Text(ui.Fit(title, room, ts), x, y + (ts == 1 ? ui.U(3) : 0), Style.Accent, ts);
        ui.TextR(renown, r.Right - ui.U(10), y + ui.U(2), Style.Text, 1);
        // Renown is the campaign's score, so show the ladder: how far to the next thing it buys.
        var (unlock, at) = Rules.NextRenownUnlock(cap);
        y += ui.U(18);
        float from = RenownStep(cap, at);
        ui.Text($"NEXT: {unlock}", x, y, Style.Dim, 1);
        ui.TextR($"{cap.Renown:0} / {at:0}", r.Right - ui.U(10), y, Style.Dim, 1);
        y += ui.U(10);
        ui.Bar(new Rectangle(x, y, r.Width - ui.U(20), ui.U(4)), (cap.Renown - from) / MathF.Max(1, at - from), Style.Accent);
        y += ui.U(12);
        float bal = C.S.LastWeekBalance;
        ui.Text($"{C.S.Gold:0} CROWNS", x, y, Style.Text);
        ui.TextR($"{(bal >= 0 ? "+" : "")}{bal:0} / WEEK", r.Right - ui.U(10), y + ui.U(2), bal >= 0 ? Style.Ok : Style.Bad, 1);
        y += ui.U(18);
        string contract = cap.Contract >= 0 ? $"{C.Stage} OF THE {C.FactionNames[cap.Contract].ToUpperInvariant()}" : "FREE COMPANY (NO PAY)";
        ui.TextFit(contract, x, y, r.Width - ui.U(20), cap.Contract >= 0 ? SideColor(cap.Contract) : Style.Dim, 1);
        y += ui.U(12);
        ui.Text($"MEN {C.Player.Men}+{C.Player.Wounded}W / {Rules.MenLimit(cap)}   COMPANIES {C.Player.Companies.Count}/{Rules.CompanySlots(cap)}", x, y, Style.Dim, 1);
        y += ui.U(12);
        ui.Text($"HORSES {C.Player.Horses}   FOOD {C.Player.Food:0} DAYS" + (C.PrisonerCount > 0 ? $"   PRISONERS {C.PrisonerCount}" : ""), x, y, C.Player.Food < 3 ? Style.Bad : Style.Dim, 1);
        y += ui.U(12);
        if (C.S.Hours < cap.WoundedUntil) ui.Text($"WOUNDED: {(cap.WoundedUntil - C.S.Hours) / 24:0.0} DAYS", x, y, Style.Bad, 1);
        else if (C.Besieging is Settlement sg) ui.TextFit($"BESIEGING {sg.Name.ToUpperInvariant()} ({ctl.First(Act.SiegeScreen)})", x, y, r.Width - ui.U(20), Style.Warn, 1);
        else
        {
            // The next step can be long: two lines, then cut.
            var lines = ui.Wrap(C.NextStep().Text.ToUpperInvariant(), r.Width - ui.U(20));
            for (int i = 0; i < Math.Min(2, lines.Count); i++)
                ui.Text(i == 1 && lines.Count > 2 ? ui.Fit(lines[1] + " " + lines[2], r.Width - ui.U(20)) : lines[i], x, y + i * ui.U(10), Style.Accent, 1);
        }
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
            string text = e.Name.ToUpperInvariant() + (id == "relic" ? $" {C.S.RelicCharges}" : $" {C.BuffDays(id):0}D");
            if (x + ui.TW(text, 1) + ui.U(20) > r.Right) break;
            int w = ui.Chip(x, y, text, e.Harmful ? Style.Bad : Kit.Gilt, e.Icon == "" ? null : e.Icon);
            if (ui.Hover(new Rectangle(x, y, w, ui.U(13)))) note = e.Line.ToUpperInvariant();
            x += w + ui.U(4);
        }
    }

    /// How much renown the last rung of the ladder covered, so the bar runs from there to the next unlock.
    static float RenownStep(CaptainState cap, float next) => MathF.Min(cap.Renown, MathF.Max(0, next - Balance.Get().MenRenownDivisor));

    void Cards()
    {
        var list = C.Player.Companies;
        int cw = list.Count >= 10 ? ui.U(88) : ui.U(96), ch = ui.U(108), gap = list.Count >= 10 ? ui.U(4) : ui.U(6), total = list.Count * cw + Math.Max(0, list.Count - 1) * gap;
        int x0 = Math.Max(ui.U(320), (ui.W - total) / 2);
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

    /// The screens a mouse can reach, one button each above the captain card, with its key.
    void Hints()
    {
        var row = new (string Label, Act Key, PanelKind Panel)[]
        {
            ("COMPANY", Act.PartyScreen, PanelKind.Party), ("CAPTAIN", Act.SkillsScreen, PanelKind.Skills), ("LORDS", Act.LordsScreen, PanelKind.Characters),
            ("REALMS", Act.RealmScreen, PanelKind.Realm), ("MAP", Act.AtlasScreen, PanelKind.Atlas), ("KEYS", Act.Help, PanelKind.Help),
        };
        int x = ui.U(10), y = ui.H - ui.U(172), h = ui.U(20);
        foreach (var (label, key, kind) in row)
        {
            string k = ctl.First(key);
            int w = ui.TW(label, 1) + ui.TW(k, 1) + ui.U(20);
            if (ui.Button(new Rectangle(x, y, w, h), label, true, k)) { if (kind == PanelKind.Help) panel = kind; else OpenScreen(kind); }
            x += w + ui.U(4);
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
            ("SCREENS", new[] { Act.PartyScreen, Act.SkillsScreen, Act.LordsScreen, Act.RealmScreen, Act.DivisionsScreen, Act.AtlasScreen, Act.SiegeScreen }
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
        var g = Ground(ms.Position);
        var p = C.S.Parties.Where(q => !q.Down && Vector2.Distance(q.Pos, g) < 5).OrderBy(q => Vector2.Distance(q.Pos, g)).FirstOrDefault();
        var s = p == null ? C.SettlementAt(g, 6) : null;
        var site = p == null && s == null ? C.SiteAt(g) : null;
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
            else if (p.Breed == "troll") linesOut.Add((p.Task == "rest" ? "ASLEEP UNTIL DUSK" : p.Task == "hunt" ? "ON THE HUNT" : "ABROAD IN THE DARK", Style.Warn));
            else if (p.Breed == "undead") linesOut.Add((p.Task == "rest" ? "LIES IN THE EARTH BY DAY" : p.Task == "hunt" ? "HUNTS THE LIVING" : "WALKS IN THE DARK", Style.Warn));
            else if (p.Breed == "wolf") linesOut.Add((p.Task == "rest" ? "LYING UP IN THE WOODS" : p.Task == "hunt" ? "ON THE HUNT" : "ON THE PROWL", Style.Warn));
            else if (p.Kind == PartyKind.Monster && p.Task == "raid" && p.RaidOn >= 0) linesOut.Add(($"OUT TO PLUNDER {C.SettlementById(p.RaidOn).Name.ToUpperInvariant()}", Style.Warn));
            linesOut.Add(($"{p.Men} MEN IN {p.Companies.Count} COMPANIES", Style.Text));
            if (p != C.Player)
            {
                float ratio = Rules.Strength(p) / MathF.Max(1, Rules.Strength(C.Player));
                linesOut.Add((ratio > 1.25f ? "STRONGER THAN YOU" : ratio < 0.8f ? "WEAKER THAN YOU" : "A FAIR MATCH", ratio > 1.25f ? Style.Bad : ratio < 0.8f ? Style.Ok : Style.Warn));
                linesOut.Add((C.Hostile(p, C.Player) ? "HOSTILE" : "NOT HOSTILE", C.Hostile(p, C.Player) ? Style.Foe : Style.Dim));
                if (p.Kind == PartyKind.Lord) linesOut.Add(($"TASK: {p.Task.ToUpperInvariant()}", Style.Dim));
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
            linesOut.Add(($"{s.Kind.ToString().ToUpperInvariant()} OF THE {C.FactionNames[s.Faction].ToUpperInvariant()}", Style.Dim));
            linesOut.Add(($"{C.S.Provinces[s.Province].Title.ToUpperInvariant()}, {s.Culture.ToUpperInvariant()} PEOPLE", Style.Dim));
            if (s.Kind == SettlementKind.Village) linesOut.Add((s.ThreatUntil > C.S.Hours ? "THREATENED: NO RECRUITS" : $"{s.Recruits} RECRUITS", Style.Text));
            if (s.Kind == SettlementKind.Town) linesOut.Add(($"{s.Mercenaries.Count} MERCENARY COMPANIES, {s.Horses} HORSES", Style.Text));
            linesOut.Add(($"PROSPERITY {s.Prosperity:0}" + (s.Kind != SettlementKind.Village ? $"   UNREST {s.Unrest:0}" : ""), s.Unrest >= Campaign.RevoltAt ? Style.Bad : Style.Dim));
            if (s.Kind != SettlementKind.Village && s.Buildings.Count > 0)
                linesOut.Add((string.Join(", ", s.Buildings.Select(b => (Campaign.BuildingOf(b.Key)?.Name ?? b.Key) + (b.Value > 1 ? " " + Style.Numerals[b.Value] : ""))).ToUpperInvariant(), Style.Dim));
            if (s.Owner >= 0 && s.Owner != C.Player.Id && C.PartyById(s.Owner) is Party holder) linesOut.Add(($"FIEF OF {holder.Name.ToUpperInvariant()}", Style.Dim));
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
