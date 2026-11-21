using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: a settlement as a place you stand in (the settlement screen). The town is painted across the whole screen at the
/// hour it is, and its buildings are the menu: click the barracks, the market, the tavern or the keep and its business comes up
/// from below, with that building's few tabs along its top. Errands that are one thing (rest, food, captives, ships) sit on the
/// bar at the bottom. Drawing and input only; the tabs themselves are in MapScreen.Settlement.cs.
partial class MapScreen
{
    /// The buildings of each kind of place and the tabs each holds. A tab a place has that no building holds is an errand on the bar.
    static readonly (string Place, string Icon, string[] Tabs)[] TownPlaces =
    {
        ("BARRACKS", "helm", new[] { "TRAIN", "RECRUIT", "MERCENARIES" }),
        ("MARKET", "scales", new[] { "MARKET", "ARMOURER", "WORKSHOP" }),
        ("TAVERN", "mug", new[] { "TAVERN", "CONTRACT" }),
        ("KEEP", "crown", new[] { "HALL", "CHAPTER", "FIEF", "GARRISON" }),
    };
    static readonly (string Place, string Icon, string[] Tabs)[] CastlePlaces =
    {
        ("YARD", "helm", new[] { "MUSTER", "TRAIN", "ESQUIRES" }),
        ("KEEP", "crown", new[] { "HALL", "CONTRACT", "CHAPTER", "FIEF", "GARRISON" }),
    };
    static readonly (string Place, string Icon, string[] Tabs)[] VillagePlaces =
    {
        ("GREEN", "helm", new[] { "RECRUIT" }),
        ("ELDERS", "house", new[] { "ELDERS" }),
    };

    static (string Place, string Icon, string[] Tabs)[] PlacesOf(Settlement s) =>
        s.Kind switch { SettlementKind.Village => VillagePlaces, SettlementKind.Castle => CastlePlaces, _ => TownPlaces };

    /// The building a tab is in here (null: an errand on the bar).
    static string? PlaceOfTab(Settlement s, string t) => PlacesOf(s).FirstOrDefault(p => p.Tabs.Contains(t)).Place;

    /// Where each building stood when last drawn (screen pixels), and the tab last open in each.
    readonly List<(string Place, Rectangle R)> spots = new();
    readonly Dictionary<string, string> lastTab = new();
    /// The place the spots were laid for: another place's buildings are not this one's.
    Settlement? spotsAt;

    void SettlementPanel()
    {
        var s = at!;
        bool fief = s.Owner == C.Player.Id && s.Kind != SettlementKind.Village;
        var tabs = TownTabs(s, fief);
        if (tab != "" && Array.IndexOf(tabs, tab) < 0) tab = ""; // a tab this place lacks, or one with nothing in it now: the street
        modalIn = 1;
        ui.Block(new Rectangle(0, 0, ui.W, ui.H));
        var bar = new Rectangle(0, ui.H - ui.U(48), ui.W, ui.U(48));
        int pw = Math.Min(ui.U(980), ui.W - ui.U(16)), ph = Math.Min(ui.U(440), bar.Y - ui.U(170));
        var well = new Rectangle((ui.W - pw) / 2, bar.Y - ui.U(6) - ph, pw, ph);
        bool open = tab != "";
        // The view lifts as a building's business comes up, so the street stays in sight above it.
        float lift = Ui.Anim(0x57ee7, open ? 1 : 0, 10);
        int ground = (int)MathHelper.Lerp(bar.Y - ui.U(46), well.Y + ui.U(12), lift);
        string? openPlace = open ? PlaceOfTab(s, tab) : null;
        if (spotsAt != s) { spots.Clear(); lastTab.Clear(); spotsAt = s; } // a new place: last place's buildings and tabs do not carry over
        string? hover = null;
        foreach (var (p, r) in spots)
            if (r.Contains(ui.Mouse) && !bar.Contains(ui.Mouse) && !(open && well.Contains(ui.Mouse)) && Available(s, p, tabs)) hover = p;
        spots.Clear();
        int s0 = ui.S;
        ui.S = Math.Max(2, ui.H / 240); // the street is painted big, by the screen's height (about 430 of its pixels across at 16:9): the town fills the screen
        try { Street(s, ground, hover, openPlace); }
        finally { ui.S = s0; }
        Signs(s, tabs, openPlace, hover);
        if (!open) // inside a building the street is short and its signs high: the plates give way
        {
            PlacePlate(s, new Rectangle(ui.U(10), ui.U(10), Math.Min(ui.U(420), ui.W / 2), ui.U(112)), true);
            StandingPlate(s, new Rectangle(ui.W - ui.U(178), ui.U(10), ui.U(168), ui.U(64)));
        }
        if (open) Business(s, new Rectangle(well.X, well.Y + (int)((1 - lift) * ui.U(30)), well.Width, well.Height), tabs, openPlace);
        ErrandBar(s, bar, tabs);
    }

    bool Available(Settlement s, string place, string[] tabs) => PlacesOf(s).FirstOrDefault(p => p.Place == place).Tabs?.Any(tabs.Contains) == true;

    // ---------- the signs over the buildings ----------

    /// A sign over each building you can go into: its picture, its name and a count of what waits there; under the mouse it lifts
    /// and says what is inside. A click opens the building, a second one shuts it.
    void Signs(Settlement s, string[] tabs, string? openPlace, string? hover)
    {
        foreach (var (place, r) in spots)
        {
            var def = PlacesOf(s).FirstOrDefault(p => p.Place == place);
            var inside = def.Tabs?.Where(tabs.Contains).ToArray() ?? Array.Empty<string>();
            if (inside.Length == 0) continue;
            bool on = place == openPlace, over = place == hover;
            int n = inside.Sum(t => TabCount(t, s));
            int w = ui.TW(place, 2) + ui.U(38) + (n > 0 ? ui.TW($"{n}", 1) + ui.U(8) : 0), h = ui.U(24);
            int x = Math.Clamp(r.Center.X - w / 2, ui.U(4), ui.W - w - ui.U(4)), y = Math.Max(ui.U(4), r.Y - h - ui.U(14) - (over ? ui.U(3) : 0));
            var plate = new Rectangle(x, y, w, h);
            ui.Rect(new Rectangle(plate.X + ui.U(2), plate.Y + ui.U(3), plate.Width, plate.Height), Color.Black * 0.45f);
            ui.VGrad(plate, on ? Kit.Mul(Kit.Gilt, 0.42f) : over ? new Color(58, 56, 50) : new Color(30, 32, 40), on ? Kit.Mul(Kit.Gilt, 0.14f) : Kit.IronDark, 1);
            ui.Frame(plate, on || over ? Kit.Gilt : Kit.Mul(Kit.GiltDark, 0.8f), ui.U(on || over ? 2 : 1));
            // The bracket it hangs from: a short stem down to the roof.
            int stem = Math.Max(ui.U(2), r.Y - plate.Bottom);
            ui.Rect(new Rectangle(r.Center.X - ui.U(1), plate.Bottom, ui.U(2), Math.Min(stem, ui.U(14))), on || over ? Kit.Gilt : Kit.Mul(Kit.GiltDark, 0.8f));
            var col = on || over ? Kit.Gilt : Kit.Mul(Kit.Parchment, 0.9f);
            ui.Icon(def.Icon, plate.X + ui.U(8), plate.Y + ui.U(5), col, 2);
            ui.Text(place, plate.X + ui.U(28), plate.Y + ui.U(5), col, 2);
            if (n > 0)
            {
                string num = n > 999 ? "999" : n.ToString();
                var b = new Rectangle(plate.Right - ui.TW(num, 1) - ui.U(9), plate.Y + ui.U(7), ui.TW(num, 1) + ui.U(4), ui.U(11));
                ui.Rect(b, Kit.Mul(Kit.Gilt, 0.55f));
                ui.Text(num, b.X + ui.U(2), b.Y + ui.U(2), Color.White, 1);
            }
            if (over)
            {
                // What is inside, under the sign.
                string what = string.Join("  ", inside.Select(t => TabLook.GetValueOrDefault(t, ("", t)).Item2));
                int tw = ui.TW(what, 1) + ui.U(12);
                var tip = new Rectangle(Math.Clamp(r.Center.X - tw / 2, ui.U(4), ui.W - tw - ui.U(4)), plate.Bottom + ui.U(3), tw, ui.U(15));
                ui.Rect(tip, Color.Black * 0.7f);
                ui.Text(what, tip.X + ui.U(6), tip.Y + ui.U(4), Style.Text, 1);
                Ui.Hovered(r);
            }
            if (over || plate.Contains(ui.Mouse)) Ui.Hovered(plate);
            if (ui.Clicked(plate) || over && ui.Clicked(r)) Enter(s, place, inside);
        }
    }

    /// Go into a building (its last open tab, else its first) or, if it is the one open, back out to the street.
    void Enter(Settlement s, string place, string[] inside)
    {
        note = "";
        if (PlaceOfTab(s, tab) == place) { tab = ""; return; }
        tab = lastTab.TryGetValue(place, out var t) && inside.Contains(t) ? t : inside[0];
    }

    // ---------- the plates over the street ----------

    /// The place's name plate at the top left: its arms, name, whose it is, the holder's mind of you and, on the street, a word
    /// about it.
    void PlacePlate(Settlement s, Rectangle p, bool flavour)
    {
        ui.Rect(p, Kit.IronDark * 0.86f);
        ui.Frame(p, Kit.Mul(Kit.GiltDark, 0.8f), ui.U(1));
        int x = p.X + ui.U(10), y = p.Y + ui.U(8);
        int sh = Math.Min(ui.U(54), p.Height - ui.U(16));
        ui.Shield(new Rectangle(x, y, sh * 5 / 6, sh), Style.Sides[Style.PaletteOf(s.Faction)]);
        int tx = x + sh * 5 / 6 + ui.U(12), tw = p.Right - ui.U(10) - tx;
        string name = s.Name.ToUpperInvariant();
        int ns = ui.TW(name, 3) <= tw ? 3 : 2;
        ui.Text(name, tx, y - ui.U(1), Style.Text, ns);
        int cx = tx, cy = y + ui.U(ns == 3 ? 24 : 18);
        void Tag(string text, Color col, string? icon = null)
        {
            int w = ui.TW(text, 1) + ui.U(icon == null ? 8 : 18);
            if (cx > tx && cx + w > tx + tw) { cx = tx; cy += ui.U(15); }
            if (cy > p.Bottom - ui.U(14)) return;
            cx += ui.Chip(cx, cy, text, col, icon) + ui.U(4);
        }
        Tag(s.KindWord, Style.Text, s.Kind == SettlementKind.Village ? "house" : "tower");
        Tag(C.FactionNames[s.Faction].ToUpperInvariant(), SideColor(s.Faction), "flag");
        if (C.IsPort(s)) Tag("PORT", Style.Text, "anchor");
        if (s.Owner == C.Player.Id) Tag("YOUR FIEF", Kit.Gilt, "crown");
        if (C.Refuses(s)) Tag("HOSTILE", Style.Bad);
        if (C.HolderOf(s) is Character holder)
        {
            float o = C.OpinionOfYou(holder);
            Tag(ui.Fit($"{holder.Name.ToUpperInvariant()}: {OpinionWord(o)}", tw), OpinionColor(o), "heart");
        }
        if (!flavour) return;
        int fy = Math.Max(cy + ui.U(18), y + sh + ui.U(6));
        foreach (var line in ui.Wrap(C.Flavour(s).ToUpperInvariant(), p.Width - ui.U(20)).Take(Math.Max(0, (p.Bottom - ui.U(4) - fy) / ui.U(11))))
        { ui.Text(line, x, fy, Kit.Mul(Kit.Parchment, 0.9f), 1); fy += ui.U(11); }
    }

    /// Prosperity and unrest as two rings on a plate at the top right.
    void StandingPlate(Settlement s, Rectangle plate)
    {
        ui.Rect(plate, Kit.IronDark * 0.86f);
        ui.Frame(plate, Kit.Mul(Kit.GiltDark, 0.8f), ui.U(1));
        bool revolt = s.Unrest >= Campaign.RevoltAt;
        ui.Ring(new Vector2(plate.X + ui.U(42), plate.Y + ui.U(25)), ui.U(17), s.Prosperity / 100f, Style.Ok, $"{s.Prosperity:0}");
        ui.TextC(s.Prosperity >= 70 ? "PROSPEROUS" : s.Prosperity >= 40 ? "PROSPERITY" : "POOR", plate.X + ui.U(42), plate.Y + ui.U(49), Style.Dim, 1);
        ui.Ring(new Vector2(plate.Right - ui.U(42), plate.Y + ui.U(25)), ui.U(17), s.Unrest / 100f, revolt ? Style.Bad : Style.Warn, $"{s.Unrest:0}");
        ui.TextC(revolt ? "REVOLT" : "UNREST", plate.Right - ui.U(42), plate.Y + ui.U(49), revolt ? Style.Bad : Style.Dim, 1);
    }

    /// The business of the building you went into, come up from below: the building's name, its tabs, the tab, a way out.
    void Business(Settlement s, Rectangle r, string[] tabs, string? place)
    {
        modalRect = r;
        ui.PlateBody(r, Kit.Gilt);
        var def = place == null ? default : PlacesOf(s).FirstOrDefault(p => p.Place == place);
        var (icon, label) = place != null && def.Tabs != null ? (def.Icon, place) : TabLook.GetValueOrDefault(tab, ("scroll", tab));
        ui.Icon(icon, r.X + ui.U(16), r.Y + ui.U(14), Kit.Gilt, 2);
        ui.Text(label, r.X + ui.U(36), r.Y + ui.U(14), Kit.Gilt, 2);
        int top = r.Y + ui.U(42);
        if (place != null && def.Tabs != null)
        {
            var inside = def.Tabs.Where(tabs.Contains).ToArray();
            if (inside.Length > 1)
            {
                int lx = r.X + ui.U(40) + ui.TW(label, 2) + ui.U(16);
                top = PlaceRail(new Rectangle(lx, r.Y + ui.U(8), r.Right - ui.U(56) - lx, ui.U(38)), inside, s) + ui.U(6);
            }
            lastTab[place] = tab;
        }
        var shut = new Rectangle(r.Right - ui.U(38), r.Y + ui.U(10), ui.U(26), ui.U(24));
        if (ui.Button(shut, "X", note: "")) { tab = ""; note = ""; return; }
        var body = new Rectangle(r.X + ui.U(14), top, r.Width - ui.U(28), r.Bottom - ui.U(14) - top);
        ui.Rect(body, Color.Black * 0.22f);
        ui.Frame(body, Color.White * 0.06f, ui.U(1));
        if (tab is "REST" or "FOOD" or "FORAGE" or "RECRUIT" or "ESQUIRES" or "CAPTIVES" or "TRAIN" or "ELDERS" or "WORKSHOP" or "HARBOUR") Watermark(body);
        TabBody(s, Kit.Inset(body, ui.U(12)));
        Note(r);
    }

    /// The bar along the bottom: the purse, the errands that are one thing (rest, food, captives, ships), and the way out.
    void ErrandBar(Settlement s, Rectangle bar, string[] tabs)
    {
        ui.VGrad(bar, new Color(30, 32, 40), Kit.IronDark, 1);
        ui.Rect(new Rectangle(bar.X, bar.Y, bar.Width, ui.U(1)), Kit.GiltDark);
        Purse(new Rectangle(bar.X, bar.Y, bar.Width, ui.U(48)));
        var leave = new Rectangle(bar.Right - ui.U(190), bar.Y + ui.U(10), ui.U(176), ui.U(28));
        var errands = tabs.Where(t => PlaceOfTab(s, t) == null).ToArray();
        if (errands.Length > 0)
        {
            int w = errands.Length * ui.U(84);
            PlaceRail(new Rectangle(leave.X - ui.U(14) - w, bar.Y + ui.U(5), w, ui.U(38)), errands, s);
        }
        if (ui.Gilded(leave, "LEAVE", ctl.Name(Act.Menu))) panel = PanelKind.None;
    }

    // ---------- the street ----------

    /// The place painted across the screen (big: the caller sets `ui.S` by the screen's height), its buildings
    /// noted in `spots`. A town: sky, the country, the upper town with its keep, the wall, the houses, and in front the barracks,
    /// the market and the tavern. A castle: its keep on the mound and the yard before it. A village: its green and the elders' house.
    void Street(Settlement s, int ground, string? hover, string? open)
    {
        float t = Environment.TickCount64 / 1000f, hour = (float)(C.S.Hours % 24);
        var l = LookOf(s, hour);
        var c = ColoursOf(l);
        var rng = new Random(s.Id * 7919 + 13);
        var cloth = Kit.Mul(Style.Sides[Style.PaletteOf(s.Faction)].Cloth, l.Night ? 0.5f : 0.9f);
        var full = new Rectangle(0, 0, ui.W, ui.H);
        var sky = new Rectangle(0, 0, ui.W, ground);
        ui.VGrad(sky, c.SkyTop, c.SkyLow, 2);
        if (l.Night) ui.Stars(new Rectangle(0, 0, ui.W, ground * 2 / 3), t, l.Country == "Steppe" ? 110 : 50);
        else Clouds(sky, l, c, t, s.Id);
        float orbAt = s.Kind == SettlementKind.Castle ? 0.84f : s.Kind == SettlementKind.Town ? 0.6f : 0.7f; // clear of the keep (0.65 at a castle, 0.82 in a town) and its sign
        Orb(sky, l, orbAt + (float)new Random(s.Id * 31 + 7).NextDouble() * 0.06f * (s.Kind == SettlementKind.Castle ? 1 : 2));
        snowCap = l.Snow ? c.Snow : Color.Transparent;
        // A far range behind the near country, pale with distance, so the sky above the place is not bare.
        if (l.Country != "Coast")
        {
            var far = Color.Lerp(Color.Lerp(c.Far, c.SkyTop, 0.5f), c.SkyLow, 0.35f); // blue with distance
            ui.Hills(full, ground - ui.U(96), ui.U(18), 2.1f + s.Id % 5, far);
            ui.Hills(full, ground - ui.U(74), ui.U(12), 0.7f + s.Id % 3, Color.Lerp(far, c.Far, 0.35f));
        }
        Country(full, l, c, ground, rng);
        switch (s.Kind)
        {
            case SettlementKind.Town: TownStreet(s, l, c, cloth, rng, ground, t, hover, open); break;
            case SettlementKind.Castle: CastleStreet(s, l, c, cloth, rng, ground, t, hover, open); break;
            default: VillageStreet(s, l, c, cloth, rng, ground, t, hover, open); break;
        }
        if (l.Snow) Snowfall(full, t, l.Night);
        snowCap = Color.Transparent;
        rimLight = Color.Transparent;
    }

    /// A faint warm glow behind the building under the mouse, drawn before it (the open one has its gilt sign and footing).
    void Glow(Rectangle r, string place, string? hover, string? open)
    {
        if (place != hover || place == open) return;
        var at = new Vector2(r.Center.X, r.Bottom - r.Height * 0.45f);
        for (int i = 0; i < 3; i++) ui.Disc(at, r.Width * (0.62f - i * 0.12f), Kit.Gilt * 0.035f);
    }

    /// A gilt line on the street under the building under the mouse (or open), drawn after it.
    void Footing(Rectangle r, int ground, string place, string? hover, string? open)
    {
        if (place != hover && place != open) return;
        ui.Rect(new Rectangle(r.X, ground + ui.U(1), r.Width, ui.U(1)), place == open ? Kit.Gilt : Kit.Gilt * 0.6f);
    }

    void TownStreet(Settlement s, TownLook l, TownColours c, Color cloth, Random rng, int ground, float t, string? hover, string? open)
    {
        var full = new Rectangle(0, 0, ui.W, ui.H);
        // The front: the barracks, a gap of houses, the market, houses, the tavern, houses to the edge.
        int bw = ui.U(100), mw = ui.U(96), tw = ui.U(160), edge = ui.U(10);
        int room = Math.Max(0, ui.W - edge - bw - mw - tw);
        int bx = edge, mx = bx + bw + room / 5, tx = mx + mw + room / 4;
        int bh = l.Build == "tent" ? 58 : 70; // the war camp's lookout stands lower than a tower: its sign comes down with it
        var rB = new Rectangle(bx, ground - ui.U(bh), bw, ui.U(bh));
        var rM = new Rectangle(mx, ground - ui.U(48), mw, ui.U(48)); // three stalls of 32
        var rT = new Rectangle(tx, ground - ui.U(64), tw, ui.U(64));
        // The upper town on its hill at the right, the keep on the crest as a castle of its own.
        float keepAt = 0.82f;
        var upper = new Rectangle(0, ground - ui.U(150), ui.W, ui.U(150));
        if (l.Build != "tent") UpperTown(upper, s, ground - ui.U(30), c.Far, l.Night, c.Lit, rng, l.Build == "white", keepAt, drawKeep: false);
        int crest = (int)(ui.W * keepAt), hill = l.Build == "tent" ? ground - ui.U(58) : upper.Y + (int)(upper.Height * 0.3f) + ui.U(14); // tents have no upper town: the hold stands on a rise behind the camp
        int k = ui.U(2);
        int kh = l.Build switch { "white" => 36, "tent" => 25, _ => 30 }; // the kasbah's gate tower and its flag stand taller: its sign goes over them
        var rK = new Rectangle(crest - 15 * k, hill - kh * k, 30 * k, kh * k);
        Glow(rK, "KEEP", hover, open);
        if (l.Build == "tent") // the rise: a low mound sloping away both sides
            for (int m = 0, s1 = Math.Max(1, ui.U(1)); hill + m < ground; m += s1)
                ui.Rect(new Rectangle(crest - ui.U(60) - m * 2, hill + m, ui.U(120) + m * 4, s1), Color.Lerp(c.Far, c.Mid, 0.25f));
        Keep(crest, hill, 2, l, c, cloth, t, hero: false);
        if (C.IsPort(s)) HarbourMasts(full, ground - ui.U(30), l, c, cloth, s.Id);
        if (!l.Day) ui.VGrad(new Rectangle(0, ground - ui.U(56), ui.W, ui.U(26)), c.Lit * 0f, c.Lit * (l.Night ? 0.12f : 0.08f), 2);
        rimLight = c.Rim;
        if (l.Build == "log") Palisade(full, ground - ui.U(28), c.Mid);
        else if (l.Build != "tent") TownWall(full, ground - ui.U(30), l.Build == "white" ? Color.Lerp(c.Mid, c.Wall, 0.4f) : c.Mid, l.Night, c.Lit, cloth, rng);
        // The landmark stands behind the houses between the market and the tavern.
        int lmX = (mx + mw + tx) / 2 - ui.U(17);
        Landmark(new Rectangle(lmX - (int)(ui.W * 0.58f), 0, ui.W, ui.H), l.Culture, ground, c.Mid, l.Night, c.Lit, cloth, t);
        var back = c with { Wall = Color.Lerp(c.Wall, c.Mid, 0.6f), Roof = Color.Lerp(c.Roof, c.Mid, 0.6f) };
        HouseRow(0, ui.W, ground - ui.U(6), l, back, new Random(s.Id * 13 + 5), t, false);
        if (l.Water == "") { if (l.Build == "tent") VillageLane(full, l, c, ground, s.Id); else CobbleStreet(full, l, c, ground, s.Id); } // a camp has no paving
        else Ground(full, l, c, ground, t);
        var lights = new List<(int X, int W)>();
        if (mx - (bx + bw) > ui.U(30)) lights.AddRange(HouseRow(bx + bw + ui.U(6), mx - ui.U(6), ground, l, c, rng, t, true));
        if (tx - (mx + mw) > ui.U(30)) lights.AddRange(HouseRow(mx + mw + ui.U(6), tx - ui.U(8), ground, l, c, rng, t, true));
        lights.AddRange(HouseRow(tx + tw + ui.U(10), ui.W - ui.U(4), ground, l, c, rng, t, true));
        if (l.Build == "tent") Herd(full, ground, c, s.Id, tx);
        if (l.Water == "") Folk(full, l, c, ground, s.Id, tx);
        Glow(rB, "BARRACKS", hover, open); Barracks(rB, l, c, cloth, t);
        Guard(rB.X + ui.U(26), ground, l, c, cloth, true);
        Glow(rM, "MARKET", hover, open); Market(rM, l, c, t, lights);
        Glow(rT, "TAVERN", hover, open); lights.AddRange(Tavern(rT, l, c, cloth, t));
        rimLight = Color.Transparent;
        if (l.Water != "") Water(full, l, c, ground, lights, t);
        foreach (var (p, r) in new[] { ("BARRACKS", rB), ("MARKET", rM), ("TAVERN", rT) }) Footing(r, ground, p, hover, open);
        Spot("BARRACKS", rB); Spot("MARKET", rM); Spot("TAVERN", rT); Spot("KEEP", rK);
    }

    void CastleStreet(Settlement s, TownLook l, TownColours c, Color cloth, Random rng, int ground, float t, string? hover, string? open)
    {
        var full = new Rectangle(0, 0, ui.W, ui.H);
        int ks = l.Build is "log" or "tent" ? 4 : 3, keepX = ui.W * 13 / 20, k = ui.U(ks); // the north's hall-fort is low: drawn bigger to hold the screen
        int kh = l.Build switch { "white" => 36, "tent" => 20, _ => 31 }; // the sign sits just over what stands highest
        var rK = new Rectangle(keepX - 15 * k, ground - ui.U(14) - kh * k, 30 * k, kh * k + ui.U(14));
        int yh = l.Build == "tent" ? 58 : 70; // the war camp's lookout stands lower
        var rY = new Rectangle(ui.U(16), ground - ui.U(yh), ui.U(100), ui.U(yh));
        if (C.IsPort(s)) HarbourMasts(full, ground - ui.U(24), l, c, cloth, s.Id);
        var back = c with { Wall = Color.Lerp(c.Wall, c.Mid, 0.6f), Roof = Color.Lerp(c.Roof, c.Mid, 0.6f) };
        HouseRow(rY.Right, ui.W, ground - ui.U(6), l, back, new Random(s.Id * 13 + 5), t, false); // the bailey houses under its walls
        if (l.Country is not ("Desert" or "Badlands" or "Tundra"))
        {
            Tree(keepX - ui.U(96), ground - ui.U(4), ui.U(34), l, c, rng);
            Tree(keepX + ui.U(98), ground - ui.U(4), ui.U(38), l, c, rng);
        }
        Glow(rK, "KEEP", hover, open);
        if (l.Water == "") { if (l.Build == "tent") VillageLane(full, l, c, ground, s.Id); else CobbleStreet(full, l, c, ground, s.Id); } // a camp has no paving
        else Ground(full, l, c, ground, t);
        Keep(keepX, ground - ui.U(14), ks, l, c, cloth, t, hero: true);
        rimLight = c.Rim;
        var lights = new List<(int X, int W)>();
        if (keepX - ui.U(110) > rY.Right + ui.U(64)) lights.AddRange(HouseRow(rY.Right + ui.U(42), keepX - ui.U(110), ground, l, c, rng, t, true));
        if (ui.W - ui.U(8) > keepX + ui.U(130)) lights.AddRange(HouseRow(keepX + ui.U(120), ui.W - ui.U(8), ground, l, c, rng, t, true));
        Glow(rY, "YARD", hover, open); Barracks(rY, l, c, cloth, t);
        // Men-at-arms flanking the keep's stone ramp and a warhorse tethered by the yard.
        Warhorse(rY.Right + ui.U(12), ground, l, c, cloth);
        Guard(keepX - ui.U(34), ground, l, c, cloth, true);
        Guard(keepX + ui.U(26), ground, l, c, cloth, true);
        rimLight = Color.Transparent;
        Footing(rY, ground, "YARD", hover, open); Footing(new Rectangle(keepX - ui.U(60), 0, ui.U(120), 0), ground, "KEEP", hover, open);
        Spot("YARD", rY); Spot("KEEP", rK);
    }

    void VillageStreet(Settlement s, TownLook l, TownColours c, Color cloth, Random rng, int ground, float t, string? hover, string? open)
    {
        var full = new Rectangle(0, 0, ui.W, ui.H);
        bool leafy = l.Country is not ("Desert" or "Badlands" or "Tundra");
        if (leafy)
        {
            Tree(ui.U(18), ground - ui.U(4), ui.U(44), l, c, rng);
            Tree(ui.W * 2 / 5, ground - ui.U(8), ui.U(40), l, c, rng);
            Tree(ui.W * 3 / 4, ground - ui.U(8), ui.U(46), l, c, rng);
            Tree(ui.W - ui.U(16), ground - ui.U(4), ui.U(42), l, c, rng);
        }
        var back = c with { Wall = Color.Lerp(c.Wall, c.Mid, 0.6f), Roof = Color.Lerp(c.Roof, c.Mid, 0.6f) };
        HouseRow(ui.W / 5, ui.W - ui.U(30), ground - ui.U(6), l, back, new Random(s.Id * 13 + 5), t, false);
        if (l.Water == "") VillageLane(full, l, c, ground, s.Id);
        else Ground(full, l, c, ground, t);
        rimLight = c.Rim;
        var rG = new Rectangle(ui.U(28), ground - ui.U(46), ui.U(88), ui.U(46));
        int ew = ui.U(74), eh = ui.U(34), ex = ui.W * 11 / 20;
        var rE = new Rectangle(ex, ground - eh - ui.U(24), ew, eh + ui.U(24));
        var lights = new List<(int X, int W)>();
        SplitFence(rG.Right + ui.U(4), ex - ui.U(12), ground, l);
        if (ex - ui.U(16) > rG.Right + ui.U(46)) lights.AddRange(HouseRow(rG.Right + ui.U(22), ex - ui.U(16), ground, l, c, rng, t, true));
        lights.AddRange(HouseRow(ex + ew + ui.U(16), ui.W - ui.U(8), ground, l, c, rng, t, true));
        Haystack(rG.Right + ui.U(4), ground, l, c);
        if (l.Build == "tent") Herd(full, ground, c, s.Id, ex - ui.U(180));
        Glow(rE, "ELDERS", hover, open);
        if (l.Build == "tent") ElderGer(rE, l, c, cloth, t, lights);
        else EldersHall(rE, l, c, cloth, t, lights);
        Glow(rG, "GREEN", hover, open); Green(rG, l, c, cloth, t);
        rimLight = Color.Transparent;
        Footing(rG, ground, "GREEN", hover, open); Footing(new Rectangle(ex, 0, ew, 0), ground, "ELDERS", hover, open);
        Spot("GREEN", rG); Spot("ELDERS", rE);
    }

    void Spot(string place, Rectangle r) => spots.Add((place, r));

    // ---------- the buildings ----------

    /// The barracks (a castle's yard): a stone hall with a tower flying the realm's colours, an arched gate, banners, and a
    /// fenced yard with a weapon rack and a straw man to cut at.
    void Barracks(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        if (l.Build == "log") { NorthBarracks(r, l, c, cloth, t); return; }
        if (l.Build == "white") { Ribat(r, l, c, cloth, t); return; }
        if (l.Build == "tent") { WarCamp(r, l, c, cloth, t); return; }
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        var day = l.Build switch { "white" => new Color(214, 204, 184), "log" => new Color(132, 96, 62), "tent" => new Color(156, 132, 100), _ => new Color(152, 144, 130) };
        var stone = HeroWall(l, c, day);
        var shade = Kit.Mul(stone, 0.72f);
        var hall = new Rectangle(r.X + ui.U(20), ground - ui.U(44), ui.U(52), ui.U(44));
        ui.Rect(hall, stone);
        for (int y = hall.Y + ui.U(8); y < ground; y += ui.U(8)) ui.Rect(new Rectangle(hall.X, y, hall.Width, s1), Kit.Mul(stone, 0.88f)); // courses
        foreach (var (ax, ay) in new[] { (4, 10), (38, 18), (6, 26), (40, 32) })
            ui.Rect(new Rectangle(hall.X + ui.U(ax), hall.Y + ui.U(ay), ui.U(7), ui.U(5)), Kit.Mul(stone, 0.92f));
        for (int x = hall.X; x < hall.Right - ui.U(3); x += ui.U(8)) ui.Rect(new Rectangle(x, hall.Y - ui.U(4), ui.U(5), ui.U(4)), stone);
        ui.Rect(new Rectangle(hall.X, hall.Y, hall.Width, s1), Color.Lerp(stone, c.Rim, 0.5f));
        ui.Rect(new Rectangle(hall.X, hall.Y + ui.U(2), hall.Width, s1), shade); // corbel shadow
        ui.Rect(new Rectangle(hall.Right - ui.U(3), hall.Y, ui.U(3), hall.Height), shade);
        // The tower at its left, with slits and the flag.
        var tower = new Rectangle(r.X, ground - ui.U(66), ui.U(24), ui.U(66));
        ui.Rect(tower, Kit.Mul(stone, 0.92f));
        for (int x = tower.X; x < tower.Right - ui.U(2); x += ui.U(6)) ui.Rect(new Rectangle(x, tower.Y - ui.U(4), ui.U(4), ui.U(4)), Kit.Mul(stone, 0.92f));
        ui.Rect(new Rectangle(tower.Right - ui.U(3), tower.Y, ui.U(3), tower.Height), shade);
        foreach (int sy in new[] { 12, 28, 44 })
            ui.Rect(new Rectangle(tower.X + ui.U(10), tower.Y + ui.U(sy), ui.U(3), ui.U(8)), l.Day ? new Color(34, 30, 30) : c.Lit * 0.85f);
        int pole = tower.X + ui.U(11);
        ui.Rect(new Rectangle(pole, tower.Y - ui.U(22), s1, ui.U(18)), Kit.Mul(stone, 0.5f));
        int wave = (int)MathF.Round(MathF.Sin(t * 3) * ui.U(1));
        ui.Rect(new Rectangle(pole + s1, tower.Y - ui.U(22), ui.U(14), ui.U(7) + wave), cloth);
        ui.Rect(new Rectangle(pole + s1, tower.Y - ui.U(22) + ui.U(3), ui.U(14), s1), Kit.Mul(cloth, 0.7f));
        // The gate: dark, a portcullis, firelight within after dusk.
        var gate = new Rectangle(hall.X + ui.U(18), ground - ui.U(24), ui.U(16), ui.U(24));
        ui.Rect(new Rectangle(gate.X - ui.U(2), gate.Y - ui.U(2), gate.Width + ui.U(4), gate.Height + ui.U(2)), Kit.Mul(stone, 0.8f));
        ui.Rect(new Rectangle(gate.X + ui.U(2), gate.Y - ui.U(4), gate.Width - ui.U(4), ui.U(2)), Kit.Mul(stone, 0.8f));
        ui.Rect(gate, l.Day ? new Color(36, 30, 28) : Color.Lerp(new Color(60, 36, 24), c.Lit, 0.35f));
        for (int x = gate.X + ui.U(2); x < gate.Right; x += ui.U(4)) ui.Rect(new Rectangle(x, gate.Y, s1, gate.Height - ui.U(6)), new Color(70, 66, 64));
        ui.Rect(new Rectangle(gate.X, gate.Y + ui.U(6), gate.Width, s1), new Color(70, 66, 64));
        if (!l.Day)
            foreach (int tx in new[] { gate.X - ui.U(5), gate.Right + ui.U(4) })
            {
                var tp = new Vector2(tx, gate.Y + ui.U(7));
                ui.Rect(new Rectangle(tx, gate.Y + ui.U(7), s1, ui.U(4)), shade);
                ui.Disc(tp, ui.U(7), c.Lit * 0.14f); ui.Disc(tp, ui.U(3), c.Lit * 0.28f);
                ui.Rect(new Rectangle(tx - s1, gate.Y + ui.U(5), ui.U(2), ui.U(2)), new Color(255, 228, 148));
            }
        // Banners either side of the gate, pointed at the foot.
        foreach (int bx in new[] { hall.X + ui.U(6), hall.Right - ui.U(17) })
        {
            ui.Rect(new Rectangle(bx - s1, hall.Y + ui.U(5), ui.U(10), s1), Kit.Mul(stone, 0.5f));
            ui.Rect(new Rectangle(bx, hall.Y + ui.U(6), ui.U(8), ui.U(16)), cloth);
            ui.Rect(new Rectangle(bx + ui.U(2), hall.Y + ui.U(22), ui.U(4), ui.U(2)), cloth);
            ui.Rect(new Rectangle(bx + ui.U(3), hall.Y + ui.U(11), ui.U(2), ui.U(6)), Kit.Mul(cloth, 1.5f));
        }
        Yard(hall.Right + ui.U(2), r.Right, ground, l, cloth);
    }

    /// The barracks' yard from `yx` to `yr`: a fence, the rack of spears with a shield against it, a straw man.
    void Yard(int yx, int yr, int ground, TownLook l, Color cloth)
    {
        int s1 = Math.Max(1, ui.U(1));
        var wood = l.Night ? new Color(64, 46, 36) : new Color(120, 84, 52);
        for (int x = yx; x < yr; x += ui.U(7)) ui.Rect(new Rectangle(x, ground - ui.U(9), ui.U(2), ui.U(9)), wood);
        ui.Rect(new Rectangle(yx, ground - ui.U(7), yr - yx, s1), wood);
        int rack = yx + ui.U(4);
        ui.Rect(new Rectangle(rack, ground - ui.U(14), ui.U(12), ui.U(2)), wood);
        for (int i = 0; i < 3; i++)
        {
            int sx = rack + ui.U(1 + i * 4);
            ui.Rect(new Rectangle(sx, ground - ui.U(26), s1, ui.U(26)), Kit.Mul(wood, 1.2f));
            ui.Rect(new Rectangle(sx - s1, ground - ui.U(29), ui.U(2) + s1, ui.U(3)), l.Night ? new Color(120, 124, 140) : new Color(200, 204, 214));
        }
        ui.Rect(new Rectangle(rack + ui.U(7), ground - ui.U(10), ui.U(6), ui.U(9)), cloth);
        ui.Rect(new Rectangle(rack + ui.U(7), ground - ui.U(10), ui.U(6), s1), Color.Lerp(cloth, Color.White, 0.3f));
        int dx = yr - ui.U(5);
        var straw = l.Night ? new Color(110, 96, 60) : new Color(206, 178, 104);
        ui.Rect(new Rectangle(dx, ground - ui.U(22), ui.U(2), ui.U(22)), wood);
        ui.Rect(new Rectangle(dx - ui.U(6), ground - ui.U(18), ui.U(14), ui.U(2)), wood);
        ui.Rect(new Rectangle(dx - ui.U(3), ground - ui.U(17), ui.U(8), ui.U(9)), straw);
        ui.Disc(new Vector2(dx + ui.U(1), ground - ui.U(22)), ui.U(3), straw);
    }

    /// The market: three stalls under striped awnings, their counters heaped with goods, a trader behind each, barrels in
    /// front; a lantern on the middle one after dusk.
    void Market(Rectangle r, TownLook l, TownColours c, float t, List<(int X, int W)> lights)
    {
        int ground = r.Bottom, sw = r.Width / 3, s1 = Math.Max(1, ui.U(1));
        float dim = l.Night ? 0.45f : l.Dusk ? 0.75f : 1;
        Color D(Color k) => Kit.Mul(k, dim);
        bool north = l.Build == "log"; // the north hangs plain wool, ochre, slate and moss, and sells fish, amber and furs
        bool steppe = l.Build == "tent"; // the steppe hangs felt with the hooked band, and sells rolled felts, hides and milk
        bool south = l.Build == "white"; // the south's souk: plain cloth in terracotta, saffron and indigo with tassels; heaps of spice
        Color[] stripes = north ? new Color[] { new(150, 110, 60), new(78, 92, 112), new(84, 104, 70) }
            : south ? new Color[] { new(178, 88, 56), new(214, 158, 62), new(60, 64, 124) }
            : steppe ? new Color[] { new(214, 200, 170), new(170, 70, 50), new(70, 84, 130) } : new Color[] { new(176, 58, 48), new(56, 92, 156), new(72, 126, 70) };
        var cream = D(new Color(230, 218, 190));
        var wood = D(new Color(124, 86, 52));
        Color[] goods = south ? new Color[] { new(196, 72, 40), new(222, 172, 52), new(130, 92, 58), new(96, 124, 62), new(224, 184, 92), new(170, 60, 50) }
            : north ? new Color[] { new(196, 200, 210), new(214, 150, 60), new(110, 80, 56), new(150, 156, 170), new(176, 140, 96), new(90, 66, 50) }
            : new Color[] { new(196, 52, 44), new(110, 160, 70), new(214, 176, 100), new(150, 110, 180), new(230, 150, 60), new(200, 200, 210) };
        var rng = new Random(r.X / Math.Max(1, ui.U(1)) + 5);
        for (int i = 0; i < 3; i++)
        {
            int x = r.X + i * sw, top = ground - ui.U(i == 1 ? 40 : 34);
            ui.Rect(new Rectangle(x + ui.U(2), top, ui.U(2), ground - top), wood);
            ui.Rect(new Rectangle(x + sw - ui.U(5), top, ui.U(2), ground - top), wood);
            // The trader behind the counter.
            var coat = D(goods[rng.Next(goods.Length)]);
            int px = x + sw / 2 - ui.U(3);
            ui.Rect(new Rectangle(px, ground - ui.U(24), ui.U(6), ui.U(10)), coat);
            ui.Rect(new Rectangle(px + ui.U(1), ground - ui.U(29), ui.U(4), ui.U(5)), D(new Color(214, 170, 130)));
            // The awning: stripes, with a scalloped edge.
            var st = D(stripes[i]);
            if (steppe) // a felt sheet on poles, the hooked band along its edge
            {
                ui.Rect(new Rectangle(x - s1, top - ui.U(7), sw + s1, ui.U(7)), st);
                ui.Rect(new Rectangle(x - s1, top - ui.U(7), sw + s1, s1), Color.Lerp(st, c.Rim, 0.4f));
                Ornament(x + ui.U(1), x + sw - ui.U(2), top - ui.U(5), D(i == 0 ? new Color(170, 70, 50) : new Color(230, 210, 160)), s1);
                if (snowCap.A > 0) ui.Rect(new Rectangle(x - s1, top - ui.U(9), sw + s1, ui.U(2)), snowCap); // snow lying on the felt
            }
            else if (south) // a cloth stretched flat on poles, its edge hung with tassels
            {
                ui.Rect(new Rectangle(x - s1, top - ui.U(6), sw + s1, ui.U(6)), st);
                ui.Rect(new Rectangle(x - s1, top - ui.U(6), sw + s1, s1), Color.Lerp(st, c.Rim, 0.4f));
                ui.Rect(new Rectangle(x - s1, top - s1, sw + s1, s1), Kit.Mul(st, 0.7f));
                for (int k = 0; k < sw; k += ui.U(4)) ui.Rect(new Rectangle(x + k, top, s1, ui.U(3)), D(new Color(230, 200, 120))); // tassels
            }
            else if (north) // wool panels sewn together, a dark hem, hanging in a shallow sag
            {
                ui.Rect(new Rectangle(x, top - ui.U(7), sw - ui.U(1), ui.U(8)), st);
                for (int k = ui.U(8); k < sw - ui.U(2); k += ui.U(8)) ui.Rect(new Rectangle(x + k, top - ui.U(7), s1, ui.U(8)), Kit.Mul(st, 0.8f));
                ui.Rect(new Rectangle(x, top, sw - ui.U(1), ui.U(2)), Kit.Mul(st, 0.62f));
                ui.Rect(new Rectangle(x + ui.U(4), top + ui.U(2), sw - ui.U(9), s1), Kit.Mul(st, 0.62f));
            }
            else
                for (int k = 0; k < sw - ui.U(1); k += ui.U(4))
                {
                    var col = (k / ui.U(4)) % 2 == 0 ? st : cream;
                    ui.Rect(new Rectangle(x + k, top - ui.U(7), Math.Min(ui.U(4), sw - ui.U(1) - k), ui.U(8)), col);
                    ui.Rect(new Rectangle(x + k + s1, top + ui.U(1), Math.Min(ui.U(2), sw - ui.U(1) - k), ui.U(2)), col);
                }
            ui.Rect(new Rectangle(x, top - ui.U(7), sw - ui.U(1), s1), Color.Lerp(st, c.Rim, 0.5f));
            if (snowCap.A > 0) ui.Rect(new Rectangle(x - s1, top - ui.U(9), sw + s1, ui.U(2)), snowCap); // snow lies on the awning
            // The counter and what is on it.
            ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(14), sw - ui.U(4), ui.U(3)), Kit.Mul(wood, 1.25f));
            ui.Rect(new Rectangle(x + ui.U(2), ground - ui.U(11), sw - ui.U(6), ui.U(11)), wood);
            ui.Rect(new Rectangle(x + ui.U(2), ground - ui.U(11), sw - ui.U(6), s1), Kit.Mul(wood, 0.7f));
            if (steppe) // rolled felts and hides on the counter
            {
                Color[] felts = { new(220, 206, 176), new(170, 70, 50), new(120, 84, 56), new(70, 84, 130) };
                for (int g = x + ui.U(3), j = i; g < x + sw - ui.U(9); g += ui.U(7), j++)
                {
                    var fc = D(felts[j % felts.Length]);
                    ui.Rect(new Rectangle(g, ground - ui.U(18), ui.U(6), ui.U(4)), fc);
                    ui.Rect(new Rectangle(g + ui.U(5), ground - ui.U(18), s1, ui.U(4)), Kit.Mul(fc, 0.7f)); // the roll's end
                }
                continue;
            }
            if (south) // the spices in cone heaps, each its colour
            {
                for (int g = x + ui.U(4); g < x + sw - ui.U(8); g += ui.U(6))
                {
                    var gc = D(goods[(i * 2 + g / Math.Max(1, ui.U(6))) % goods.Length]);
                    for (int hgt = 0; hgt < ui.U(4); hgt += s1) ui.Rect(new Rectangle(g + hgt / 2, ground - ui.U(14) - hgt - s1, ui.U(5) - hgt, s1), gc);
                }
                continue;
            }
            for (int g = x + ui.U(3); g < x + sw - ui.U(7); g += ui.U(4))
            {
                var gc = D(goods[(i * 2 + g / Math.Max(1, ui.U(4))) % goods.Length]);
                int gh = ui.U(2 + rng.Next(3));
                ui.Rect(new Rectangle(g, ground - ui.U(14) - gh, ui.U(3), gh), gc);
            }
        }
        // Barrels and a crate before the stalls.
        foreach (int bx in new[] { r.X - ui.U(6), r.Right - ui.U(4) })
        {
            ui.Rect(new Rectangle(bx, ground - ui.U(9), ui.U(7), ui.U(9)), Kit.Mul(wood, 1.1f));
            ui.Rect(new Rectangle(bx, ground - ui.U(7), ui.U(7), s1), Kit.Mul(wood, 0.6f));
            ui.Rect(new Rectangle(bx, ground - ui.U(3), ui.U(7), s1), Kit.Mul(wood, 0.6f));
        }
        if (!l.Day)
        {
            var at = new Vector2(r.X + sw + sw / 2, ground - ui.U(33));
            Lantern(at, c, wood);
            lights.Add(((int)at.X - ui.U(3), ui.U(6)));
        }
    }

    /// The village green: the muster pole with the realm's colours, the well under its little roof, and a few men waiting.
    void Green(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        int wave = (int)MathF.Round(MathF.Sin(t * 2.8f) * s1);
        var wood = l.Night ? new Color(64, 46, 36) : new Color(120, 84, 52);
        var stone = HeroWall(l, c, new Color(150, 142, 128));
        int pole = r.X + ui.U(6);
        ui.Rect(new Rectangle(pole - ui.U(2), ground - ui.U(2), ui.U(6), ui.U(2)), stone); // stone socket
        ui.Rect(new Rectangle(pole, ground - ui.U(44), ui.U(2), ui.U(42)), wood);
        ui.Rect(new Rectangle(pole + ui.U(2), ground - ui.U(43), ui.U(16), ui.U(10) + wave), cloth);
        ui.Rect(new Rectangle(pole + ui.U(2), ground - ui.U(38), ui.U(16), s1), Color.Lerp(cloth, Kit.Gilt, 0.45f));
        int wx = r.X + ui.U(24);
        if (l.Build == "tent") WellSweep(wx + ui.U(14), ground, l, c); // the steppe draws its water with a sweep
        else
        {
            ui.Rect(new Rectangle(wx, ground - ui.U(10), ui.U(22), ui.U(10)), stone);
            ui.Rect(new Rectangle(wx - s1, ground - ui.U(11), ui.U(22) + 2 * s1, ui.U(2)), Color.Lerp(stone, c.Rim, 0.4f));
            for (int x = wx + ui.U(3); x < wx + ui.U(20); x += ui.U(6)) ui.Rect(new Rectangle(x, ground - ui.U(7), ui.U(4), s1), Kit.Mul(stone, 0.8f));
            ui.Rect(new Rectangle(wx + ui.U(2), ground - ui.U(28), ui.U(2), ui.U(18)), wood);
            ui.Rect(new Rectangle(wx + ui.U(18), ground - ui.U(28), ui.U(2), ui.U(18)), wood);
            ui.Rect(new Rectangle(wx + ui.U(2), ground - ui.U(22), ui.U(18), s1), wood);
            if (l.Build == "white") Dome(wx + ui.U(11), ground - ui.U(28), ui.U(13), ui.U(7), Kit.Mul(c.Wall, 1.08f)); // the south's well under a little plastered dome
            else Gable(wx - ui.U(2), ground - ui.U(28), ui.U(26), 0, ui.U(7), c.Roof);
            ui.Rect(new Rectangle(wx + ui.U(11), ground - ui.U(22), s1, ui.U(7)), Kit.Mul(wood, 0.8f));
            ui.Rect(new Rectangle(wx + ui.U(9), ground - ui.U(15), ui.U(4), ui.U(4)), Kit.Mul(wood, 1.2f));
            if (!l.Day) Lantern(new Vector2(wx + ui.U(20), ground - ui.U(21)), c, wood);
        }
        Color[] coats = { new(146, 56, 48), new(60, 84, 116), new(116, 98, 62), new(80, 96, 64) };
        for (int i = 0; i < 3; i++)
        {
            var coat = coats[i];
            if (l.Night) coat = Kit.Mul(coat, 0.48f); else if (l.Dusk) coat = Kit.Mul(coat, 0.78f);
            int x = r.X + ui.U(54 + i * 10), h = ui.U(16 + i % 2 * 2);
            ui.Rect(new Rectangle(x - s1, ground, ui.U(7), ui.U(2)), Color.Black * 0.25f); // ground shadow
            ui.Rect(new Rectangle(x, ground - h + ui.U(5), ui.U(5), h - ui.U(8)), coat);
            ui.Rect(new Rectangle(x, ground - ui.U(6), ui.U(5), s1), Kit.Mul(coat, 0.55f)); // belt
            ui.Rect(new Rectangle(x + ui.U(1), ground - ui.U(3), s1, ui.U(3)), Kit.Mul(coat, 0.45f));
            ui.Rect(new Rectangle(x + ui.U(3), ground - ui.U(3), s1, ui.U(3)), Kit.Mul(coat, 0.45f));
            ui.Rect(new Rectangle(x + ui.U(1), ground - h + ui.U(1), ui.U(3), ui.U(4)), l.Night ? new Color(150, 100, 70) : new Color(214, 170, 130));
            ui.Rect(new Rectangle(x, ground - h, ui.U(5), s1), i == 2 ? (l.Night ? new Color(110, 96, 60) : new Color(196, 168, 96)) : Kit.Mul(coat, 0.65f)); // hat/hood
            if (i == 1)
            {
                ui.Rect(new Rectangle(x + ui.U(5), ground - h - ui.U(4), s1, h + ui.U(1)), wood); // pitchfork
                ui.Rect(new Rectangle(x + ui.U(4), ground - h - ui.U(5), ui.U(3), s1), Kit.Mul(wood, 1.3f));
                ui.Rect(new Rectangle(x + ui.U(4), ground - h - ui.U(7), s1, ui.U(2)), Kit.Mul(wood, 1.3f));
                ui.Rect(new Rectangle(x + ui.U(6), ground - h - ui.U(7), s1, ui.U(2)), Kit.Mul(wood, 1.3f));
            }
        }
    }
}
