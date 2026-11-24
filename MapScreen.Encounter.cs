using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the encounter as a scene ("The Field Between"). The field the battle would be fought on is painted full screen
/// from the real ground (`Campaign.FieldOf`: the kind of country, the layout, who holds it, the hour and the weather), your
/// companies drawn up on the left and theirs on the right as the battle's own puppets (`CampStage`, one figure for about four
/// men), each under its banner, the two lines running away from a low camera toward the horizon with the field between them
/// (the painting is `MapScreen.Encounter.Field.cs`). The leaders' plates sit in the top corners with their companies listed; the balance of
/// strength and the odds in words across the top; the choices on a bar along the bottom, each with what it would bring.
/// Drawing and input only; the forecast is `Campaign.AutoForecast`.
partial class MapScreen
{
    CampStage? encStage;
    /// The figures drawn this frame, and each company's figures and screen box (laid out by `EncounterLayout`).
    readonly List<Unit> encMen = new();
    readonly List<EncBlock> encBlocks = new();
    /// The block under the mouse (or its roster row), -1 for none; read by the scene and the plates.
    int encHover = -1;
    long encTick;
    (int Id, int Quarter, (float Win, int Few, int Many, float Mine, float Theirs) F)? encForecast;

    /// One company on the field: its side (-1 yours, +1 theirs), the record, where its men stand, its box on the screen.
    sealed record EncBlock(int Side, CompanyRecord K, List<Unit> Men, Vector2 Back)
    {
        public Rectangle Box;
    }

    const int EncShown = 5;
    int EncPixel => Math.Max(2, gd.Viewport.Height / 240);

    /// Where the `i`th company of a side stands (the middle of its front rank): one behind another, away from the camera, the
    /// biggest nearest, each a step further out so the line runs on a slant and every company shows.
    static Vector2 EncSlot(int i, int side) => new(side * (3.4f + i * 4.6f), 7.5f - i * 6.5f);

    /// Lay out both sides' figures: each company a block of files facing the foe, mounted men spaced wider.
    void EncounterLayout(Party e, int w, int h)
    {
        encStage!.Frame(w, h);
        encMen.Clear(); encBlocks.Clear();
        int index = 1;
        void Side(IEnumerable<CompanyRecord> companies, int side, int palette)
        {
            int i = 0;
            foreach (var k in companies.Where(k => k.Men > 0).OrderByDescending(k => k.Men).Take(EncShown))
            {
                var at = EncSlot(i++, side);
                bool big = k.Type.Size >= 1.4f || k.Type.Look.Beast; // a troll or a bear is shown one for one
                int per = big ? 1 : 4, n = Math.Clamp((k.Men + per - 1) / per, 1, big ? 6 : 14);
                int files = n <= 3 ? 1 : n <= 8 ? 2 : 3, ranks = (n + files - 1) / files;
                var stacks = k.Stacks.Where(s => s.Men > 0).ToList();
                var men = new List<Unit>();
                float gx = 0, gz = 0;
                for (int j = 0; j < n; j++)
                {
                    // Men of each stack in proportion to its share, so a mixed company shows its mix.
                    var t = stacks.Count == 0 ? k.Type : stacks[Math.Min(stacks.Count - 1, (int)(j / (float)n * stacks.Count))].Type;
                    float size = MathF.Max(1, t.Size * (t.Look.Beast ? 1.5f : 1));
                    gx = (t.Mount != null ? 2.1f : 1.05f) * size; gz = (t.Mount != null ? 1.5f : 0.95f) * size;
                    int file = j % files, rank = j / files;
                    float jx = (Ease.Hash(k.Id * 13 + j) - 0.5f) * 0.25f, jz = (Ease.Hash(k.Id * 17 + j) - 0.5f) * 0.25f;
                    // Files run back from the front (along the line of sight), ranks out from the field; every other file is staggered.
                    var pos = new Vector2(at.X + side * (file * gx + jx), at.Y + ranks * gz / 2 - rank * gz - (file % 2) * gz * 0.5f + jz);
                    float face = side < 0 ? 0.42f : MathF.PI - 0.42f; // toward the foe, turned a little to you
                    var u = new Unit
                    {
                        Index = index++, Type = t, Palette = palette, Coat = (k.Id * 7 + j) % Style.Coats.Length, Hp = t.MaxHp, Anim = Ease.Hash(k.Id * 31 + j) * 2,
                        Pos = pos, Facing = face, ViewFacing = face, Heading = face, ViewHeading = face, EnemyDist = float.MaxValue,
                    };
                    if (t.Mount != null) { u.Horse = t.Mount; u.HorseHp = t.Mount.Hp; }
                    men.Add(u);
                    encMen.Add(u);
                }
                // The banner stands among them, behind the first file.
                var back = new Vector2(at.X + side * gx * Math.Max(0.6f, (files - 1) * 0.6f), at.Y - gz * 0.5f);
                var b = new EncBlock(side, k, men, back);
                encBlocks.Add(b);
            }
        }
        Side(C.Player.Companies, -1, C.PlayerPalette);
        Side(e.Companies, 1, Style.PaletteOf(C.SideOf(e)));
        // The captain at the head of your side.
        var ct = Rules.CaptainType(C.Captain);
        var cap = new Unit
        {
            Index = index++, Type = ct, Palette = C.PlayerPalette, Hp = ct.MaxHp, Pos = new Vector2(-1.7f, 12.5f), EnemyDist = float.MaxValue,
            Facing = 0.6f, ViewFacing = 0.6f, Heading = 0.6f, ViewHeading = 0.6f,
        };
        if (ct.Mount != null) { cap.Horse = ct.Mount; cap.HorseHp = ct.Mount.Hp; }
        encMen.Add(cap);
        // Each block's box on the screen, for hovering.
        foreach (var b in encBlocks)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var u in b.Men)
            {
                float top = u.Mounted ? 2.7f : 2f;
                var f = encStage.Project(new Vector3(u.Pos.X, 0, u.Pos.Y)); var hd = encStage.Project(new Vector3(u.Pos.X, top, u.Pos.Y));
                float hw = encStage.PerMetre(u.Pos.Y) * (u.Mounted ? 1.2f : 0.45f);
                x0 = MathF.Min(x0, f.X - hw); x1 = MathF.Max(x1, f.X + hw); y0 = MathF.Min(y0, hd.Y); y1 = MathF.Max(y1, f.Y);
            }
            b.Box = new Rectangle((int)x0, (int)y0, (int)(x1 - x0), (int)(y1 - y0));
        }
    }

    /// Draw the figures into their texture before the frame's sprites (from `Draw`).
    void PrepareEncounter()
    {
        if (panel != PanelKind.Encounter || bare || C.Encounter is not Party e || C.Meeting(e)) return;
        encStage ??= new CampStage(gd, art, shading, campKit, () => lifeQuality, new Vector3(0, 3.4f, 27f), new Vector3(0, 2.3f, 0f), 0.56f);
        long now = Environment.TickCount64;
        float dt = encTick == 0 ? 0 : Math.Min(0.1f, (now - encTick) / 1000f);
        encTick = now;
        int w = gd.Viewport.Width, h = gd.Viewport.Height;
        EncounterLayout(e, w, h);
        var look = CampLook();
        encStage.Render(w, h, encMen, EncPixel, look.Night ? 0.45f : CampLight(look), false, dt);
    }

    /// What auto-resolve would likely bring, worked out once a quarter hour for this foe.
    (float Win, int Few, int Many, float Mine, float Theirs) Forecast(Party e)
    {
        int q = (int)(C.S.Hours * 4);
        if (encForecast is not { } f || f.Id != e.Id || f.Quarter != q) encForecast = f = (e.Id, q, C.AutoForecast(e));
        return f.F;
    }

    void EncounterPanel()
    {
        var e = C.Encounter!;
        if (C.Meeting(e))
        {
            if (e.Kind == PartyKind.Lord && C.CharacterOf(e) is { Kind: "lord" } met) OpenTalk(met, PanelKind.Encounter); // a friendly lord: talk (LD3)
            else MeetingPanel(e);
            return;
        }
        modalIn = 1;
        ui.Block(new Rectangle(0, 0, ui.W, ui.H));
        if (encStage == null || encBlocks.Count == 0 && encMen.Count == 0) { ui.Rect(new Rectangle(0, 0, ui.W, ui.H), Kit.IronDark); return; } // the first frame: the men are drawn from the next
        var l = CampLook();
        var c = ColoursOf(l);
        float t = Ui.Instant ? 3.2f : Environment.TickCount64 / 1000f;
        var (ground, layout) = C.FieldOf(e);
        var weather = C.WeatherFor(e);
        // Which company the mouse is on: a block on the field, or (below) a row of a plate.
        encHover = -1;
        for (int i = encBlocks.Count - 1; i >= 0; i--) if (ui.Hover(encBlocks[i].Box)) { encHover = i; break; }
        float s0 = ui.S;
        try { FieldScene(e, l, c, ground, layout, t); }
        finally { ui.S = s0; }
        if (encStage.Texture != null) ui.Sb.Draw(encStage.Texture, new Rectangle(0, 0, ui.W, ui.H), Color.White);
        ui.S = EncPixel;
        try { FieldWeather(l, c, weather, t); }
        finally { ui.S = s0; }
        ui.Vignette(0.35f);
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.U(170)), Color.Black * 0.62f, Color.Black * 0f, 2); // the top reads over the sky
        ui.VGrad(new Rectangle(0, ui.H - ui.U(120), ui.W, ui.U(120)), Color.Black * 0f, Color.Black * 0.78f, 2); // and the choices over the field
        int plateW = Math.Min(ui.U(330), ui.W / 3 - ui.U(16));
        var mine = SidePlate(new Rectangle(ui.U(10), ui.U(10), plateW, 0), e, -1);
        var theirs = SidePlate(new Rectangle(ui.W - ui.U(10) - plateW, ui.U(10), plateW, 0), e, 1);
        StrengthStrip(new Rectangle(mine.Right + ui.U(16), ui.U(8), theirs.X - mine.Right - ui.U(32), 0), e, ground, layout, weather, l);
        if (encHover >= 0) BlockCard(encBlocks[encHover]);
        ChoiceBar(e, theirs);
    }

    // ---------- the field ----------

    Vector2 EP(float x, float z, float y = 0) => encStage!.Project(new Vector3(x, y, z));

    // ---------- the plates ----------

    /// A side's plate in a top corner: its leader's face, its name, and its companies (hover a row to find it on the field).
    /// Returns the plate's rectangle (its height follows the rows).
    Rectangle SidePlate(Rectangle r, Party e, int side)
    {
        var blocks = encBlocks.Where(b => b.Side == side).ToList();
        var all = (side < 0 ? C.Player.Companies : e.Companies).Where(k => k.Men > 0).ToList();
        bool lord = side > 0 && e.Kind == PartyKind.Lord;
        int rowH = ui.U(26), head = ui.U(78), extra = all.Count > blocks.Count ? ui.U(14) : 0, buttons = lord ? ui.U(38) : 0;
        string flavour = side > 0 ? FoeLine(e) : "";
        int flavourH = flavour != "" ? ui.U(14) : 0;
        r = new Rectangle(r.X, r.Y, r.Width, head + flavourH + blocks.Count * rowH + extra + buttons + ui.U(8));
        ui.PlateBody(r, side < 0 ? Style.Friendly : Style.Foe, 1, 0.92f);
        // The face: a painted bust for lords and for you, else the troop at the head of the band.
        var face = new Rectangle(side < 0 ? r.X + ui.U(8) : r.Right - ui.U(8) - ui.U(56), r.Y + ui.U(8), ui.U(56), ui.U(64));
        ui.Rect(face, Color.Black * 0.4f);
        var who = side < 0 ? C.S.Characters.FirstOrDefault(k => k.Kind == "captain") : C.CharacterOf(e);
        if (who != null) ui.Face(PortraitPainter.Of(C, who), face);
        else if (all.Count > 0) ui.Portrait(all.OrderByDescending(k => k.Type.Tier).First().Type, face, palette: Style.PaletteOf(C.SideOf(e)));
        int tx = side < 0 ? face.Right + ui.U(10) : r.X + ui.U(10), tw = r.Width - face.Width - ui.U(28);
        string name = side < 0 ? "YOUR COMPANY" : e.Name.ToUpperInvariant();
        int ns = ui.FitScale(name, tw);
        ui.TextFit(name, tx, r.Y + ui.U(ns == 2 ? 10 : 14), tw, side < 0 ? Style.Friendly : PartyColor(e), ns);
        ui.TextFit(side < 0 ? $"{C.Player.Men} MEN" + (C.Player.Wounded > 0 ? $", {C.Player.Wounded} HURT" : "") : Allegiance(e), tx, r.Y + ui.U(30), tw, Style.Dim, 1);
        if (side > 0 && who != null && who.Kind == "lord")
        {
            float op = C.OpinionOfYou(who);
            ui.TextFit($"THINKS {OpinionWord(op)} OF YOU", tx, r.Y + ui.U(44), tw, OpinionColor(op), 1);
            if (who.Traits.Count > 0) ui.TextFit(string.Join(", ", who.Traits).ToUpperInvariant(), tx, r.Y + ui.U(56), tw, Style.Dim, 1);
        }
        else if (side < 0) ui.TextFit($"{C.S.Gold:0} CROWNS", tx, r.Y + ui.U(44), tw, Kit.Gilt, 1);
        int y = r.Y + head;
        if (flavour != "") { ui.TextFit(flavour, r.X + ui.U(10), y - ui.U(4), r.Width - ui.U(20), C.GrudgeOf(e) is { Length: > 0 } ? Style.Bad : Kit.Parchment, 1); y += flavourH; }
        foreach (var b in blocks)
        {
            var row = new Rectangle(r.X + ui.U(6), y, r.Width - ui.U(12), rowH - ui.U(2));
            int bi = encBlocks.IndexOf(b);
            if (ui.Hover(row)) encHover = bi;
            if (encHover == bi) ui.Rect(row, (side < 0 ? Style.Friendly : Style.Foe) * 0.18f);
            var k = b.K;
            ui.Portrait(k.Type, new Rectangle(row.X + ui.U(2), row.Y, ui.U(24), ui.U(24)), palette: side < 0 ? C.PlayerPalette : Style.PaletteOf(C.SideOf(e)));
            string label = side < 0 ? k.Name.ToUpperInvariant() : $"{k.Men} {k.Type.Name.ToUpperInvariant()}";
            ui.TextFit(label, row.X + ui.U(32), row.Y + ui.U(3), row.Width - ui.U(80), Style.Text, 1);
            string sub = side < 0 ? $"{k.Men} MEN" + (k.Wounded > 0 ? $" +{k.Wounded} HURT" : "")
                : Rules.RoleOf(k) == "casters" && C.CourtMageOf(e) is Character mage && mage.CompanyId == k.Id ? $"LED BY {mage.Name.ToUpperInvariant()}" : "";
            if (sub != "") ui.TextFit(sub, row.X + ui.U(32), row.Y + ui.U(14), row.Width - ui.U(80), sub.StartsWith("LED") ? Style.Aura : Style.Dim, 1);
            ui.TextR($"TIER {Style.Numerals[k.Type.Tier]}", row.Right - ui.U(4), row.Y + ui.U(8), Style.Dim, 1);
            y += rowH;
        }
        if (extra > 0) { ui.Text($"AND {all.Count - blocks.Count} MORE COMPANIES", r.X + ui.U(10), y + ui.U(2), Style.Dim, 1); y += extra; }
        if (lord) LordButtons(new Rectangle(r.X + ui.U(8), y + ui.U(4), r.Width - ui.U(16), ui.U(28)), e);
        return r;
    }

    /// The one line under the foe's name: he comes for you (a grudge), or how he fights (his nature).
    string FoeLine(Party e)
    {
        if (C.GrudgeOf(e) is { Length: > 0 } grudge) return grudge.ToUpperInvariant();
        if (e.Kind == PartyKind.Lord && C.CharacterOf(e) is Character ch && Nature.Line(ch.Traits) is { Length: > 0 } how) return how.ToUpperInvariant();
        return "";
    }

    /// Against a lord: meet him in single combat, or talk before steel.
    void LordButtons(Rectangle r, Party e)
    {
        bool talk = C.CharacterOf(e) is { Kind: "lord" };
        int bw = talk ? (r.Width - ui.U(8)) / 2 : r.Width;
        var duel = new Rectangle(r.X, r.Y, bw, r.Height);
        string? cant = C.CannotChallenge(e);
        if (ui.Button(duel, "CHALLENGE", cant == null)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareDuel(e)); return; }
        if (ui.Hover(duel)) note = cant ?? "MEET HIS LORD IN SINGLE COMBAT. WIN AND HIS MEN START THE BATTLE SHAKEN.";
        if (!talk) return;
        var tb = new Rectangle(r.Right - bw, r.Y, bw, r.Height);
        if (ui.Button(tb, "TALK")) OpenTalk(C.CharacterOf(e)!, PanelKind.Encounter);
        if (ui.Hover(tb)) note = "SPEAK WITH HIM BEFORE THE FIGHT: DEMAND HE YIELD, SUE FOR PEACE, OR INSULT HIM.";
    }

    /// Across the top between the plates: the field's name and weather, the balance of strength, the odds in words, and the
    /// notes on the ground, the Tide and the spells.
    void StrengthStrip(Rectangle r, Party e, TerrainParams g, FieldLayout layout, Weather w, TownLook l)
    {
        int cx = r.Center.X;
        ui.Ribbon(cx, r.Y + ui.U(4), C.BattleName(C.Player.Pos).ToUpperInvariant(), Kit.Mul(Style.Foe, 0.66f));
        string hour = l.Night ? "NIGHT" : l.Dusk ? "DUSK" : "DAY";
        string sky = w.Snow ? "SNOW" : w.Rain >= 0.3f ? "RAIN" : w.Fog > 0.3f ? "FOG" : "CLEAR";
        string soil = (g.Wet > 0.5f || w.Rain >= 0.3f) ? "SODDEN GROUND" : "DRY GROUND";
        string tide = C.TideNote(e.Pos), spells = C.SpellNote(e);
        int stripH = ui.U(94) + (tide != "" ? ui.U(14) : 0) + (spells is { Length: > 0 } ? ui.U(14) : 0);
        var back = new Rectangle(r.X + ui.U(6), r.Y + ui.U(28), r.Width - ui.U(12), stripH);
        ui.Rect(back, new Color(10, 12, 18) * 0.72f);
        ui.Frame(back, Kit.Gilt * 0.35f, ui.U(1));
        int y = r.Y + ui.U(34);
        TextOnDark($"{hour}  ·  {sky}  ·  {soil}", cx, y, Style.Text * 0.85f, 1);
        y += ui.U(18);
        var f = C.Player.Men > 0 ? Forecast(e) : (0, 0, 0, 0, 1); // the bar weighs what auto-resolve weighs, so it agrees with the words
        float me = f.Mine, them = f.Theirs, win = f.Win;
        var bar = new Rectangle(r.X + ui.U(50), y + ui.U(4), r.Width - ui.U(100), ui.U(10));
        ui.TugBar(bar, me / MathF.Max(1, me + them), Style.Friendly, Style.Foe);
        TextOnDark($"{C.Player.Men}", bar.X - ui.U(26), y + ui.U(2), Style.Friendly, 1);
        TextOnDark($"{e.Men}", bar.Right + ui.U(26), y + ui.U(2), Style.Foe, 1);
        y += ui.U(22);
        string word = win >= 0.9f ? "THEY ARE FAR WEAKER" : win >= 0.6f ? "YOU HAVE THE EDGE" : win > 0.4f ? "AN EVEN FIGHT" : win > 0.1f ? "THEY HAVE THE EDGE" : "THEY ARE FAR STRONGER";
        TextOnDark(word, cx, y, win >= 0.6f ? Style.Ok : win > 0.4f ? Style.Warn : Style.Bad, 2);
        y += ui.U(24);
        TextOnDark(C.GroundNote(e), cx, y, Kit.Gilt, 1);
        if (tide != "") { y += ui.U(14); TextOnDark(ui.Fit(tide, r.Width), cx, y, TideColour(C.TideAt(e.Pos).Tradition), 1); }
        if (spells is { Length: > 0 }) { y += ui.U(14); TextOnDark(ui.Fit(spells.ToUpperInvariant(), r.Width), cx, y, Style.Dim, 1); }
    }

    /// Centred text with a hard drop shadow, so it reads over sky or field without a box.
    void TextOnDark(string s, int cx, int y, Color col, int scale)
    {
        ui.TextC(s, cx + ui.U(1), y + ui.U(1), Color.Black * 0.85f, scale);
        ui.TextC(s, cx, y, col, scale);
    }

    /// The card of the company under the mouse, beside its block.
    void BlockCard(EncBlock b)
    {
        var k = b.K;
        var lines = new List<(string, Color)>
        {
            (b.Side < 0 ? k.Name.ToUpperInvariant() : k.Type.Name.ToUpperInvariant(), b.Side < 0 ? Style.Friendly : Style.Foe),
            ($"{k.Men} MEN, TIER {Style.Numerals[k.Type.Tier]}" + (k.Wounded > 0 ? $", {k.Wounded} HURT" : ""), Style.Text),
        };
        foreach (var s in k.Stacks.Where(s => s.Men > 0 && s.Type != k.Type).Take(3)) lines.Add(($"  {s.Men} {s.Type.Name.ToUpperInvariant()}", Style.Dim));
        if (k.Type.Mount != null) lines.Add(("MOUNTED", Style.Dim));
        if (b.Side < 0 && k.BattleHonours.Count > 0) lines.Add(($"{k.BattleHonours.Count} BATTLE HONOURS", Kit.Gilt));
        int w = lines.Max(x => ui.TW(x.Item1, 1)) + ui.U(20), h = lines.Count * ui.U(13) + ui.U(12);
        int x = b.Side < 0 ? b.Box.Right + ui.U(8) : b.Box.X - ui.U(8) - w, y = b.Box.Y - ui.U(4);
        x = Math.Clamp(x, ui.U(4), ui.W - w - ui.U(4));
        var r = new Rectangle(x, y, w, h);
        ui.PlateBody(r, b.Side < 0 ? Style.Friendly : Style.Foe, 1, 0.95f);
        for (int i = 0; i < lines.Count; i++) ui.Text(lines[i].Item1, r.X + ui.U(10), r.Y + ui.U(7) + i * ui.U(13), lines[i].Item2, 1);
    }

    // ---------- the choices ----------

    /// The bar along the bottom: auto-resolve with its forecast, ATTACK large in the middle, retreat and pay with what they cost.
    void ChoiceBar(Party e, Rectangle foePlate)
    {
        var bar = new Rectangle(0, ui.H - ui.U(78), ui.W, ui.U(78));
        ui.Divider(ui.W / 2 - ui.U(300), bar.Y, ui.U(600), Kit.Mul(Kit.Gilt, 0.7f));
        if (note != "") TextOnDark(note, ui.W / 2, bar.Y - ui.U(18), Style.Warn, 1);
        int gap = ui.U(18), aw = ui.U(220), sw = Math.Min(ui.U(170), (ui.W - aw - ui.U(80) - 3 * gap) / 3), autoW = sw + ui.U(60);
        int total = autoW + aw + 2 * sw + 3 * gap, x = (ui.W - total) / 2, by = bar.Y + ui.U(14);
        bool fit = C.Player.Men > 0;
        // AUTO-RESOLVE, with what it would likely bring.
        var auto = new Rectangle(x, by, autoW, ui.U(32));
        var (win, few, many, _, _) = fit ? Forecast(e) : (0, 0, 0, 0, 1);
        string winWord = win >= 0.95f ? "A SURE WIN" : win <= 0.05f ? "A SURE LOSS" : $"WIN {MathF.Round(win * 10):0} IN 10";
        string fall = many == 0 ? "NONE OF YOURS FALL" : few == many ? $"{many} OF YOURS FALL" : $"{few}-{many} OF YOURS FALL";
        if (ui.Button(auto, "AUTO-RESOLVE", fit)) { C.AutoResolveEncounter(); ShowResults(); return; } // Encounter is cleared now
        ui.TextC($"{winWord}, {fall}", auto.Center.X, auto.Bottom + ui.U(8), win >= 0.7f ? Style.Ok : win >= 0.4f ? Style.Warn : Style.Bad, 1);
        if (ui.Hover(auto)) note = "YOUR SERGEANTS FIGHT IT WITHOUT YOU. THE FORECAST IS FROM FORTY TRIES ON PAPER.";
        x = auto.Right + gap;
        // ATTACK: the big one.
        var atk = new Rectangle(x, by - ui.U(6), aw, ui.U(44));
        if (fit) ui.Rect(Kit.Inset(atk, -ui.U(3)), Kit.Gilt * (0.25f + 0.15f * MathF.Sin((Ui.Instant ? 0 : Environment.TickCount64 / 1000f) * 3)));
        if (ui.Button(atk, "ATTACK", fit, active: fit)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareBattle(e)); return; }
        ui.TextC("LEAD THEM YOURSELF", atk.Center.X, atk.Bottom + ui.U(4), Style.Dim, 1);
        x = atk.Right + gap;
        // RETREAT: a rearguard pays, or you cannot.
        var ret = new Rectangle(x, by, sw, ui.U(32));
        bool canRetreat = C.CanRetreat(out string why);
        if (ui.Button(ret, "RETREAT", canRetreat)) { C.Retreat(); panel = PanelKind.None; return; }
        ui.TextFitC(canRetreat ? "A REARGUARD PAYS" : why, ret.Center.X, ret.Bottom + ui.U(8), sw + gap, canRetreat ? Style.Dim : Style.Bad, 1);
        if (canRetreat && ui.Hover(ret)) note = "YOUR SLOWEST COMPANY COVERS THE RETREAT AND LOSES SOME MEN.";
        x = ret.Right + gap;
        // PAY or PARLEY.
        var pay = new Rectangle(x, by, sw, ui.U(32));
        int price = C.ParleyPrice(e);
        if (ui.Button(pay, price > 0 ? $"PAY {price}" : "PARLEY", price > 0 && C.S.Gold >= price)) { if (C.Parley()) panel = PanelKind.None; return; }
        string payLine = price < 0 ? (e.Kind == PartyKind.Monster ? $"{Campaign.Plural(e.Breed).ToUpperInvariant()} DO NOT BARGAIN" : "LORDS AT WAR DO NOT BARGAIN")
            : C.S.Gold >= price ? "THEY LET YOU BE" : "NOT ENOUGH CROWNS";
        ui.TextFitC(payLine, pay.Center.X, pay.Bottom + ui.U(8), sw + gap, price > 0 && C.S.Gold >= price ? Style.Dim : Style.Bad, 1);
    }
}
