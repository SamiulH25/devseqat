using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// The character creator, opened by NEW CAMPAIGN on the title screen: six steps (homeland, birth, past, arms, likeness,
/// the oath) beside a living portrait of the captain and a ledger of what the choices give. Every number on it comes
/// from `Origins.Preview`, the same call the new campaign makes, so the ledger never lies.
sealed class CreatorScreen
{
    readonly Ui ui;
    readonly Action<CaptainMaking> begin;
    readonly Random dice = new();
    public bool Open;
    public CaptainMaking M = new();
    public int Step;
    public static readonly string[] Steps = { "HOMELAND", "BIRTH", "PAST", "ARMS", "LIKENESS", "THE OATH" };
    /// Which text field takes the keyboard: 0 none, 1 the captain's name, 2 the company's.
    int typing;
    bool helm = true;
    float time, stepAt;
    // The preview captain, rebuilt only when the making changes.
    string previewKey = "";
    CaptainState preview = new();
    UnitType look = UnitType.Captain;

    static readonly string[] BeardNames = { "CLEAN-SHAVEN", "FULL BEARD", "MOUSTACHE", "BALD, BEARDED" };
    static readonly string[] SkinNames = { "FAIR", "OLIVE", "TAWNY", "DARK" };
    static readonly string[] HairNames = { "BLACK", "BROWN", "CHESTNUT", "FLAXEN", "RED", "GREY" };
    static readonly string[] CompanyFirst = { "GREY", "IRON", "BLACK", "RED", "ASHEN", "WOLF", "SILVER", "LANTERN", "THORN", "RAVEN", "BROKEN SPEAR", "WHITE" };
    static readonly string[] CompanyLast = { "COMPANY", "LANCES", "BAND", "BROTHERHOOD", "BANNER", "FREE COMPANY", "HOST", "COMPANY" };

    // The living captain in the well (null: the still portrait), the well's size last frame, and the choices last seen.
    readonly CreatorStage? stage;
    Rectangle well;
    string choiceKey = "";

    public CreatorScreen(Ui ui, Action<CaptainMaking> begin, CreatorStage? stage = null) { this.ui = ui; this.begin = begin; this.stage = stage; }

    /// What the captain does on this step: stands for the homeland, marches out, acts out the past, shows the road,
    /// turns its face to you, raises its blade for the oath.
    CreatorStage.Act Act => Step switch
    {
        0 => CreatorStage.Act.Stand,
        1 => CreatorStage.Act.March,
        2 => M.Past switch
        {
            "hedge_knight" => CreatorStage.Act.Ride, "poacher" => CreatorStage.Act.Shoot, "deserter" => CreatorStage.Act.Leave,
            "disinherited" => CreatorStage.Act.Salute, "surgeon" => CreatorStage.Act.Stand, "drillmaster" => CreatorStage.Act.Guard, _ => CreatorStage.Act.Drill,
        },
        3 => M.Road switch { "lance" => CreatorStage.Act.Charge, "bow" => CreatorStage.Act.Shoot, _ => CreatorStage.Act.Drill },
        4 => CreatorStage.Act.Turn,
        _ => CreatorStage.Act.Salute,
    };

    /// Draw the living captain into its texture; called before the frame's sprites begin (a render target cannot change
    /// in the middle of them). Uses the well's size from the last frame.
    public void Prepare(float dt)
    {
        if (!Open || stage == null || well.Width <= 0) return;
        Refresh();
        stage.Render(well.Width, well.Height, preview, !helm, Act, dt);
    }

    /// Open on a fresh, randomly named captain of the first realm.
    public void Show()
    {
        Open = true;
        Step = 0;
        typing = 0;
        M = new CaptainMaking { Skin = 1, Hair = 2, Beard = 1 };
        M.Name = RandomName();
        M.Company = RandomCompany();
        stepAt = time;
    }

    int U(float v) => ui.U(v);
    string Culture => Factions.All[Math.Clamp(M.Realm, 0, Factions.All.Count - 1)].Culture;

    string RandomName()
    {
        var n = Names.Get();
        var firsts = n.LordsOf(Culture);
        string first = firsts.Count > 0 ? firsts[dice.Next(firsts.Count)] : "Aldric";
        return (dice.Next(3) == 0 || n.Epithets.Count == 0 ? first : first + " " + n.Epithets[dice.Next(n.Epithets.Count)]).ToUpperInvariant();
    }

    string RandomCompany() => $"THE {CompanyFirst[dice.Next(CompanyFirst.Length)]} {CompanyLast[dice.Next(CompanyLast.Length)]}";

    /// Every choice drawn at random (the dice button); the name and company too.
    void Randomise()
    {
        M.Realm = dice.Next(Factions.All.Count);
        M.Birth = Origins.Births[dice.Next(Origins.Births.Count)].Id;
        M.Past = Origins.Pasts[dice.Next(Origins.Pasts.Count)].Id;
        M.Road = Origins.Roads[dice.Next(Origins.Roads.Count)].Id;
        (M.Skin, M.Hair, M.Beard) = (dice.Next(Variety.Skins.Length), dice.Next(Variety.Hairs.Length), dice.Next(Variety.Styles));
        M.Name = RandomName();
        M.Company = RandomCompany();
    }

    public void Go(int step)
    {
        if (step == Step) return;
        Step = Math.Clamp(step, 0, Steps.Length - 1);
        typing = 0;
        stepAt = time;
        helm = Step != 4; // the likeness step shows the face bare
    }

    void Refresh()
    {
        string choices = $"{M.Realm}|{M.Birth}|{M.Past}|{M.Road}|{M.Visage}";
        if (choices != choiceKey) { if (choiceKey != "") stage?.React(); choiceKey = choices; } // a choice made: the captain cheers
        string key = $"{choices}|{helm}";
        if (key == previewKey) return;
        previewKey = key;
        preview = Origins.Preview(M);
        var t = Rules.CaptainType(preview);
        if (!helm) t.Look = t.Look with { Head = "none", Plume = false };
        look = t;
    }

    /// Keys and typing: Escape steps back (or closes), Enter steps on; a focused field takes the letters.
    void Keys(KeyboardState kb, KeyboardState pkb)
    {
        bool Pressed(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
        if (typing != 0)
        {
            string text = typing == 1 ? M.Name : M.Company;
            int field = typing;
            while (Ui.Typed.Count > 0)
            {
                char ch = Ui.Typed.Dequeue();
                if (ch == '\b') { if (text.Length > 0) text = text[..^1]; }
                else if (ch is '\r' or '\n' or (char)27) typing = 0;
                else if (text.Length < 24 && (char.IsLetterOrDigit(ch) || ch is ' ' or '\'' or '-')) text += char.ToUpperInvariant(ch);
            }
            if (field == 1) M.Name = text; else M.Company = text;
            if (Pressed(Microsoft.Xna.Framework.Input.Keys.Escape)) typing = 0;
            return;
        }
        Ui.Typed.Clear();
        if (Pressed(Microsoft.Xna.Framework.Input.Keys.Escape)) { if (Step == 0) Open = false; else Go(Step - 1); }
        else if (Pressed(Microsoft.Xna.Framework.Input.Keys.Enter)) { if (Step < Steps.Length - 1) Go(Step + 1); else Begin(); }
        else if (Pressed(Microsoft.Xna.Framework.Input.Keys.Right)) Go(Step + 1);
        else if (Pressed(Microsoft.Xna.Framework.Input.Keys.Left)) Go(Step - 1);
    }

    void Begin()
    {
        if (M.Name.Trim() == "") { Go(4); typing = 1; return; }
        Open = false;
        begin(M);
    }

    public void Draw(KeyboardState kb, KeyboardState pkb, float now)
    {
        time = now;
        Keys(kb, pkb);
        Refresh();
        ui.Dim(0.55f);

        int margin = U(16), top = U(16);
        // Title and the steps along the top.
        ui.Ribbon(ui.W / 2, top, "RAISE YOUR CAPTAIN", Kit.Gilt, 2);
        var tabs = new Rectangle(margin + U(40), top + U(40), ui.W - 2 * margin - U(80), U(20));
        var labels = Steps.Select((s, i) => $"{i + 1}  {s}").ToArray();
        int hit = ui.Tabs(tabs, labels, Step);
        if (hit >= 0) Go(hit);

        int bodyY = tabs.Bottom + U(14), footH = U(34), bodyH = ui.H - bodyY - footH - U(18);
        int avail = ui.W - 2 * margin - 2 * U(10);
        int leftW = (int)(avail * 0.27f), rightW = (int)(avail * 0.24f), midW = avail - leftW - rightW;
        var left = new Rectangle(margin, bodyY, leftW, bodyH);
        var mid = new Rectangle(left.Right + U(10), bodyY, midW, bodyH);
        var right = new Rectangle(mid.Right + U(10), bodyY, rightW, bodyH);

        Likeness(left);
        Ledger(right);
        // The middle plate slides in a little each time the step changes.
        float t = Ui.Instant ? 1 : Ease.Out(Math.Clamp((time - stepAt) / 0.25f, 0, 1));
        var m = new Rectangle(mid.X + (int)((1 - t) * U(24)), mid.Y, mid.Width, mid.Height);
        var inner = ui.Plate(m, Steps[Step], Kit.Gilt);
        switch (Step)
        {
            case 0: Homeland(inner); break;
            case 1: Choices(inner, Origins.Births, M.Birth, id => M.Birth = id, "HOW WERE YOU RAISED?"); break;
            case 2: Choices(inner, Origins.Pasts, M.Past, id => M.Past = id, "WHAT WERE YOU BEFORE THE COMPANY?"); break;
            case 3: Choices(inner, Origins.Roads, M.Road, id => M.Road = id, "HOW DO YOU FIGHT?"); break;
            case 4: Face(inner); break;
            default: Oath(inner); break;
        }
        Footer(new Rectangle(margin, ui.H - footH - U(12), ui.W - 2 * margin, footH));
    }

    // ---------- the portrait ----------

    /// The captain as they stand now: a dusk well with their banner behind, the portrait breathing, name and origin beneath.
    void Likeness(Rectangle r)
    {
        var inner = ui.Plate(r, null, Kit.Gilt);
        var def = Factions.All[M.Realm];
        var colours = Origins.Free(M) ? Style.Sides[2] : Style.Sides[def.Palette];
        int wellH = Math.Min((int)(inner.Height * 0.7f), U(440));
        var well = new Rectangle(inner.X, inner.Y, inner.Width, wellH);
        ui.VGrad(well, Kit.Mul(colours.Cloth, 0.45f), new Color(16, 12, 14), 2);
        ui.Stars(new Rectangle(well.X, well.Y, well.Width, well.Height / 2), time, 24);
        ui.Banner(well.Right - U(34), well.Y + U(10), U(34), U(96), colours, time * 1.4f);
        ui.Hills(well, well.Bottom - U(30), U(10), 2.3f + M.Realm, new Color(40, 30, 34));
        ui.Hills(well, well.Bottom - U(14), U(6), 5.1f, new Color(22, 18, 20));
        // Embers drifting up through the well.
        for (int i = 0; i < 10; i++)
        {
            float life = 4 + i % 3, f = ((time + i * 0.83f) % life) / life;
            var p = new Vector2(well.X + well.Width * ((i * 0.618f) % 1) + MathF.Sin(f * 5 + i) * U(6), well.Bottom - well.Height * 0.8f * f);
            ui.Disc(p, Math.Max(1, U(1.5f)), Color.Lerp(new Color(255, 200, 120), Kit.Ember, f) * (1 - f) * 0.7f);
        }
        if (stage?.Texture is { } tex && this.well.Size == well.Size)
        {
            // The living captain: a soft shadow at their feet, then the puppet drawn into the stage's texture.
            var f = stage.Feet + well.Location.ToVector2();
            for (int i = 0; i < 6; i++)
            {
                float t = i / 6f, hw = stage.ShadowW * 0.5f * MathF.Sqrt(1 - t * t);
                ui.Rect(new Rectangle((int)(f.X - hw), (int)(f.Y - U(3) + i * U(1)), (int)(hw * 2), U(1)), Color.Black * 0.22f);
                ui.Rect(new Rectangle((int)(f.X - hw), (int)(f.Y - U(3) - i * U(1)), (int)(hw * 2), U(1)), Color.Black * 0.12f);
            }
            ui.Sb.Draw(tex, well, Color.White);
        }
        else
        {
            int k = Math.Max(1, (int)(well.Height / (1.75f / Art.Px)));
            int breathe = Ui.Instant ? 0 : (int)MathF.Round(MathF.Sin(time * 1.6f) * U(1));
            ui.Portrait(look, new Rectangle(well.X, well.Y + U(4) + breathe, well.Width, well.Height - U(4)), k, 2.3f);
        }
        this.well = well;
        ui.Frame(well, Color.Black * 0.6f, U(1));
        ui.Shield(new Rectangle(well.X + U(6), well.Bottom - U(46), U(32), U(40)), colours);

        int y = well.Bottom + U(10);
        string name = M.Name.Trim() == "" ? "NAMELESS" : M.Name;
        int sc = ui.FitScale(name, inner.Width);
        ui.TextFit(name, inner.X, y, inner.Width, M.Name.Trim() == "" ? Style.Dim : Style.Text, sc); y += U(sc == 2 ? 20 : 12);
        var birth = Origins.Find(M.Birth); var past = Origins.Find(M.Past);
        ui.TextFit($"{(birth?.Name ?? "").ToUpperInvariant()}", inner.X, y, inner.Width, Kit.Gilt, 1); y += U(10);
        ui.TextFit($"ONCE A {(past?.Name ?? "").ToUpperInvariant()}", inner.X, y, inner.Width, Kit.Gilt, 1); y += U(14);
        int cx = inner.X;
        cx += ui.Chip(cx, y, $"AGE {Origins.Age(M)}", Style.Dim, "clock") + U(4);
        ui.Chip(cx, y, Origins.Free(M) ? "FREE COMPANY" : $"FOR THE {def.Name.ToUpperInvariant()}", Origins.Free(M) ? Style.Warn : colours.Ui, "flag");
        y += U(18);
        cx = inner.X;
        cx += ui.Chip(cx, y, $"{Kit.N((int)Gold)}", Kit.Gilt, "coin") + U(4);
        cx += ui.Chip(cx, y, $"{Renown:0} RENOWN", Kit.Gilt, "star") + U(4);
        if (cx + U(60) < inner.Right) ui.Chip(cx, y, $"{Men} MEN", Style.Text, "shield");
        else ui.Chip(inner.X, y + U(16), $"{Men} MEN", Style.Text, "shield");
        y += U(34);
        if (y + U(12) < inner.Bottom) ui.TextFit(M.Company.Trim() == "" ? "YOUR COMPANY" : M.Company, inner.X, y, inner.Width, Style.Dim, 1);
    }

    float Gold => MathF.Max(0, Balance.Get().StartGold + Origins.Sum(M, "gold"));
    float Renown => MathF.Max(0, Balance.Get().StartRenown + Origins.Sum(M, "renown"));
    int Men => (int)MathF.Round((24 + 15 + Origins.Men(M).Sum(x => x.Men)) * Balance.Get().ArmyScale);

    // ---------- the ledger ----------

    /// Skills as bars (the usual start dim, what your choices add in gilt), then perks and the kit you carry.
    void Ledger(Rectangle r)
    {
        var inner = ui.Plate(r, "LEDGER", Kit.Rivet);
        var plain = new CaptainState();
        Campaign.StartingSkills(plain);
        int y = inner.Y;
        int rowH = Math.Clamp((int)(inner.Height * 0.46f) / Math.Max(1, Skills.All.Count), U(13), U(24));
        foreach (var s in Skills.All)
        {
            float was = Skills.Level(plain, s.Id), now = Skills.Level(preview, s.Id);
            ui.Text(s.Name.ToUpperInvariant(), inner.X, y, now > was ? Style.Text : Style.Dim, 1);
            ui.TextR($"{now:0}", inner.Right, y, now > was ? Kit.Gilt : Style.Dim, 1);
            var bar = new Rectangle(inner.X, y + U(9), inner.Width, U(3));
            ui.Rect(bar, Color.Black * 0.6f);
            const float span = 50; // the bars read to 50: no starting captain comes near 100
            int wasW = (int)(bar.Width * MathF.Min(1, was / span));
            float shown = Ui.Anim(Ui.Key(bar, s.Id.Length), now, 10);
            int nowW = (int)(bar.Width * MathF.Min(1, shown / span));
            ui.Rect(new Rectangle(bar.X, bar.Y, nowW, bar.Height), Kit.Gilt);
            ui.Rect(new Rectangle(bar.X, bar.Y, Math.Min(wasW, nowW), bar.Height), Kit.Mul(Kit.Rivet, 1.1f));
            y += rowH;
        }
        y += U(6);
        ui.Divider(inner.X, y, inner.Width, Kit.Gilt * 0.6f); y += U(10);
        ui.Text("PERKS", inner.X, y, Kit.Gilt, 1); y += U(12);
        foreach (var id in Origins.Perks(M))
            if (Skills.Perk(id) is PerkDef p && y + U(14) < inner.Bottom)
            {
                ui.Chip(inner.X, y, p.Name.ToUpperInvariant(), Kit.Gilt, "up");
                if (ui.Hover(new Rectangle(inner.X, y, inner.Width, U(13)))) ui.Tooltip(ui.Mouse.X + U(12), ui.Mouse.Y, p.Name.ToUpperInvariant(), p.Text);
                y += U(16);
            }
        y += U(4);
        ui.Text("KIT", inner.X, y, Kit.Gilt, 1); y += U(12);
        var kit = preview.Kit;
        foreach (var (slot, id) in new[] { ("HAND", kit.Weapon), ("SIDE", kit.Side), ("SHIELD", Gear.Get(kit.Weapon).TwoHanded ? "" : kit.Shield), ("BODY", kit.Body), ("HORSE", kit.Horse) })
        {
            if (id == "" || y + U(10) > inner.Bottom) continue;
            ui.Text(slot, inner.X, y, Style.Dim, 1);
            ui.TextFit(Gear.Get(id).Name.ToUpperInvariant(), inner.X + U(44), y, inner.Width - U(44), Style.Text, 1);
            y += U(10);
        }
        y += U(8);
        if (y + U(24) > inner.Bottom) return;
        ui.Text("COMPANY", inner.X, y, Kit.Gilt, 1); y += U(12);
        foreach (var (troop, men) in Muster())
        {
            if (y + U(10) > inner.Bottom) break;
            ui.TextR($"{men}", inner.X + U(26), y, Style.Text, 1);
            ui.TextFit(Troops.Get(troop).Name.ToUpperInvariant(), inner.X + U(34), y, inner.Width - U(34), Style.Dim, 1);
            y += U(10);
        }
    }

    /// The men you start with, by troop: the homeland's own and the levies every captain has, then those your past brings.
    List<(string Troop, int Men)> Muster()
    {
        float scale = Balance.Get().ArmyScale;
        var list = new List<(string Troop, int Men)> { (Factions.Root(Factions.CultureOf(M.Realm)), 24), ("levies", 15) };
        foreach (var (troop, men) in Origins.Men(M))
        {
            int i = list.FindIndex(x => x.Troop == troop);
            if (i >= 0) list[i] = (troop, list[i].Men + men); else list.Add((troop, men));
        }
        return list.GroupBy(x => x.Troop).Select(g => (g.Key, (int)MathF.Round(g.Sum(x => x.Men) * scale))).ToList();
    }

    // ---------- the steps ----------

    /// Seven realms down the left; the one under the mouse (or chosen) drawn large on the right: shield, people, how they fight.
    void Homeland(Rectangle r)
    {
        ui.Text("WHERE DOES YOUR COMPANY START?", r.X, r.Y, Style.Dim, 1);
        int listW = (int)(r.Width * 0.46f), y = r.Y + U(14);
        int rowH = Math.Min(U(36), (r.Height - U(14)) / Math.Max(1, Factions.All.Count));
        int show = M.Realm;
        for (int f = 0; f < Factions.All.Count; f++)
        {
            var def = Factions.All[f];
            var row = new Rectangle(r.X, y, listW, rowH - U(4));
            if (ui.Hover(row)) show = f;
            if (ui.Button(row, "", active: f == M.Realm)) { M.Realm = f; }
            ui.Shield(new Rectangle(row.X + U(4), row.Y + U(2), U(20), row.Height - U(4)), Style.Sides[def.Palette]);
            ui.TextFit(def.Name.ToUpperInvariant(), row.X + U(30), row.Y + (row.Height - U(7)) / 2, row.Width - U(34), f == M.Realm ? new Color(255, 246, 222) : Style.Text, 1);
            y += rowH;
        }
        var d = Factions.All[show];
        var pane = new Rectangle(r.X + listW + U(12), r.Y + U(14), r.Width - listW - U(12), r.Height - U(14));
        ui.Rect(pane, Color.Black * 0.25f);
        ui.Frame(pane, Kit.Rivet * 0.5f, U(1));
        int bw = Math.Min(U(70), pane.Width / 3);
        ui.Banner(pane.Center.X, pane.Y + U(10), bw, (int)(bw * 1.9f), Style.Sides[d.Palette], time * 1.3f);
        int py = pane.Y + U(20) + (int)(bw * 1.9f);
        ui.TextFitC(d.Name.ToUpperInvariant(), pane.Center.X, py, pane.Width - U(12), Kit.Gilt, ui.FitScale(d.Name.ToUpperInvariant(), pane.Width - U(12))); py += U(22);
        var culture = Factions.Culture(d.Culture);
        if (culture != null)
        {
            ui.TextFitC($"{culture.Name.ToUpperInvariant()} PEOPLE", pane.Center.X, py, pane.Width - U(12), Style.Text, 1); py += U(14);
            foreach (var line in ui.Wrap(culture.Text.ToUpperInvariant(), pane.Width - U(20)))
            { ui.TextC(line, pane.Center.X, py, Style.Dim, 1); py += U(10); }
            py += U(4);
            foreach (var line in ui.Wrap(culture.Flavour.ToUpperInvariant() + ".", pane.Width - U(20)))
            { ui.TextC(line, pane.Center.X, py, Kit.Parchment * 0.8f, 1); py += U(10); }
        }
        py += U(8);
        foreach (var line in ui.Wrap("YOUR FIRST COMPANY IS RAISED FROM ITS PEOPLE, AND ITS LORDS KNOW YOUR NAME.", pane.Width - U(20)))
        { ui.TextC(line, pane.Center.X, py, Style.Dim, 1); py += U(10); }
        py += U(8);
        // Its soldiers at their prime: foot, bow and horse.
        string[] roles = { "infantry", "archers", "horse" };
        int ps = Math.Min((pane.Width - U(32)) / 3, pane.Bottom - py - U(24));
        if (ps < U(30)) return;
        int px = pane.Center.X - (3 * ps + 2 * U(6)) / 2;
        for (int k = 0; k < 3; k++)
        {
            var pr = new Rectangle(px + k * (ps + U(6)), py, ps, ps);
            var troop = Troops.Get(Factions.Line(show, roles[k], 4));
            ui.VGrad(pr, Kit.Mul(Style.Sides[d.Palette].Cloth, 0.5f), Kit.IronDark, 2);
            ui.Portrait(troop, pr, Math.Max(1, ps / U(40)) * ui.S);
            ui.Frame(pr, Color.Black * 0.6f, U(1));
            ui.TextFitC(troop.Name.ToUpperInvariant(), pr.Center.X, pr.Bottom + U(5), ps + U(4), Style.Dim, 1);
        }
    }

    /// A list of origins on the left, the one under the mouse (or chosen) told in full on the right with what it gives.
    void Choices(Rectangle r, IReadOnlyList<OriginDef> list, string chosen, Action<string> pick, string question)
    {
        ui.Text(question, r.X, r.Y, Style.Dim, 1);
        int listW = (int)(r.Width * 0.42f), y = r.Y + U(14);
        int rowH = Math.Min(U(58), (r.Height - U(14)) / Math.Max(1, list.Count));
        var show = list.FirstOrDefault(o => o.Id == chosen) ?? list[0];
        // One size for every name in the list: large when they all fit.
        int nameScale = rowH - U(4) >= U(44) && list.All(o => ui.TW(o.Name.ToUpperInvariant(), 2) <= listW - U(36)) ? 2 : 1;
        foreach (var o in list)
        {
            var row = new Rectangle(r.X, y, listW, rowH - U(4));
            if (ui.Hover(row)) show = o;
            bool on = o.Id == chosen;
            if (ui.Button(row, "", active: on)) pick(o.Id);
            if (o.Icon != "") ui.Icon(o.Icon, row.X + U(8), row.Y + (row.Height - U(14)) / 2, on ? new Color(255, 246, 222) : Kit.Gilt, 2);
            int tx = row.X + U(32);
            string sub = Skills.Perk(o.Perk) is PerkDef pk ? "PERK: " + pk.Name.ToUpperInvariant()
                : string.Join(", ", Origins.Describe(o, 1).Take(2)) is { Length: > 0 } gives ? gives : o.Text.ToUpperInvariant();
            bool two = row.Height >= U(30);
            int sc = two ? nameScale : 1;
            int nameY = two ? row.Y + (row.Height - U(7 * sc + 14)) / 2 : row.Y + (row.Height - U(7)) / 2;
            ui.TextFit(o.Name.ToUpperInvariant(), tx, nameY, row.Right - tx - U(4), on ? new Color(255, 246, 222) : Style.Text, sc);
            if (two) ui.TextFit(sub, tx, nameY + U(7 * sc + 7), row.Right - tx - U(4), on ? Kit.Parchment : Style.Dim, 1);
            y += rowH;
        }
        Detail(new Rectangle(r.X + listW + U(12), r.Y + U(14), r.Width - listW - U(12), r.Height - U(14)), show, show.Id == chosen);
    }

    /// One origin told in full: its icon large, its story on parchment, and what it gives as gilt lines.
    void Detail(Rectangle pane, OriginDef o, bool chosen)
    {
        ui.Rect(pane, Color.Black * 0.25f);
        ui.Frame(pane, (chosen ? Kit.Gilt : Kit.Rivet) * 0.6f, U(1));
        int x = pane.X + U(12), w = pane.Width - U(24), y = pane.Y + U(12);
        if (o.Icon != "") ui.Icon(o.Icon, x, y, Kit.Gilt, 4);
        int sc = ui.FitScale(o.Name.ToUpperInvariant(), w - U(40));
        ui.TextFit(o.Name.ToUpperInvariant(), x + U(40), y + U(sc == 2 ? 2 : 6), w - U(40), Style.Text, sc);
        ui.Text(chosen ? "CHOSEN" : "CLICK TO CHOOSE", x + U(40), y + U(18), chosen ? Kit.Gilt : Style.Dim, 1);
        y += U(40);
        ui.Divider(x, y, w, Kit.Gilt * 0.5f); y += U(10);
        // The story on a strip of parchment.
        int ts = ui.Wrap(o.Text.ToUpperInvariant(), w - U(16), 2).Count * U(18) < pane.Height / 3 ? 2 : 1, lh = U(ts == 2 ? 18 : 10);
        var lines = ui.Wrap(o.Text.ToUpperInvariant(), w - U(16), ts);
        var parch = new Rectangle(x, y, w, lines.Count * lh + U(12));
        ui.VGrad(parch, Kit.Parchment, Kit.Mul(Kit.Parchment, 0.85f), 2);
        ui.Frame(parch, Kit.Mul(Kit.Ink, 0.8f), U(1));
        int ly = parch.Y + U(7);
        foreach (var line in lines) { ui.Text(line, x + U(8), ly, Kit.Ink, ts); ly += lh; }
        y = parch.Bottom + U(12);
        if (o.Age > 0) { ui.Text($"+{o.Age} YEARS", x, y, Style.Dim, 1); y += U(14); }
        ui.Text("GIVES", x, y, Kit.Gilt, 1); y += U(12);
        foreach (var g in Origins.Describe(o, Balance.Get().ArmyScale))
        {
            if (y + U(10) > pane.Bottom - U(18)) break;
            bool bad = g.StartsWith("-") || g.Contains(" -") && !g.StartsWith("PERK") || g.StartsWith("NO ");
            ui.Rect(new Rectangle(x, y + U(2), U(3), U(3)), bad ? Style.Warn : Kit.Gilt);
            foreach (var line in ui.Wrap(g, w - U(8))) { ui.Text(line, x + U(8), y, bad ? Style.Warn : g.StartsWith("PERK") ? Kit.Gilt : Style.Text, 1); y += U(10); }
            y += U(2);
        }
        y += U(10);
        if (o.Line != "" && y + U(30) < pane.Bottom - U(20))
        {
            ui.Divider(x, y, w, Kit.Gilt * 0.4f); y += U(12);
            foreach (var line in ui.Wrap($"\"{o.Line.ToUpperInvariant()}\"", w - U(20)))
            { ui.TextC(line, pane.Center.X, y, Kit.Parchment * 0.85f, 1); y += U(10); }
        }
        if (Campaign.Events.Any(e => e.Trigger == "origin:" + o.Id))
            ui.TextC("YOUR PAST MAY CATCH UP WITH YOU.", pane.Center.X, pane.Bottom - U(16), Style.Dim, 1);
    }

    /// The captain's face and names: two fields, skin and hair swatches, the cut of the beard, and the helm on or off.
    void Face(Rectangle r)
    {
        int x = r.X, w = r.Width, y = r.Y;
        y = Field(x, y, w, 1, "YOUR NAME", M.Name, () => M.Name = RandomName());
        y = Field(x, y, w, 2, "YOUR COMPANY", M.Company, () => M.Company = RandomCompany());
        y += U(4);
        ui.Text("SKIN", x, y, Kit.Gilt, 1); ui.TextR(SkinNames[M.Skin], x + w, y, Style.Dim, 1); y += U(12);
        M.Skin = Swatches(x, y, w, Variety.Skins, M.Skin); y += U(34);
        ui.Text("HAIR", x, y, Kit.Gilt, 1); ui.TextR(HairNames[M.Hair], x + w, y, Style.Dim, 1); y += U(12);
        M.Hair = Swatches(x, y, w, Variety.Hairs, M.Hair); y += U(34);
        ui.Text("BEARD", x, y, Kit.Gilt, 1); y += U(12);
        int bw = (w - U(6) * (Variety.Styles - 1)) / Variety.Styles;
        for (int i = 0; i < Variety.Styles; i++)
            if (ui.Button(new Rectangle(x + i * (bw + U(6)), y, bw, U(22)), ui.Fit(BeardNames[i], bw - U(6)), active: M.Beard == i)) M.Beard = i;
        y += U(32);
        if (ui.Button(new Rectangle(x, y, U(150), U(22)), helm ? "TAKE OFF THE HELM" : "PUT ON THE HELM")) helm = !helm;
        if (ui.Button(new Rectangle(x + U(160), y, U(150), U(22)), "ANOTHER FACE"))
            (M.Skin, M.Hair, M.Beard) = (dice.Next(Variety.Skins.Length), dice.Next(Variety.Hairs.Length), dice.Next(Variety.Styles));
        y += U(34);
        foreach (var line in ui.Wrap("THE FACE IS YOURS IN EVERY BATTLE. YOUR KIT CHANGES AS YOU BUY BETTER AT THE ARMOURER.", w))
        { ui.Text(line, x, y, Style.Dim, 1); y += U(10); }
        y += U(8);
        // A close look at the face, bare-headed whatever the helm toggle says.
        int size = Math.Min(r.Bottom - y, w / 2);
        if (size < U(60)) return;
        var close = new Rectangle(x + (w - size) / 2, y, size, size);
        ui.VGrad(close, new Color(60, 46, 40), new Color(20, 16, 16), 2);
        var bare = look.Clone();
        bare.Look = bare.Look with { Head = "none", Plume = false, Cape = false };
        int k = Math.Max(1, (int)(size / (0.62f / Art.Px)));
        ui.Portrait(bare, close, k, 1.84f);
        ui.Frame(close, Kit.Gilt * 0.7f, U(1));
        ui.Frame(Kit.Inset(close, U(3)), Color.Black * 0.5f, U(1));
    }

    /// A text box with a dice button; click it to type (Enter or Escape ends).
    int Field(int x, int y, int w, int which, string label, string value, Action roll)
    {
        ui.Text(label, x, y, Kit.Gilt, 1); y += U(12);
        var box = new Rectangle(x, y, w - U(80), U(24));
        bool on = typing == which;
        ui.Rect(box, Color.Black * (on ? 0.7f : 0.5f));
        ui.Frame(box, on ? Kit.Gilt : ui.Hover(box) ? Kit.Mul(Kit.Gilt, 0.7f) : Style.PanelEdge, U(1));
        string shown = value + (on && (Ui.Instant || time % 1 < 0.55f) ? "_" : "");
        ui.TextFit(shown == "" ? "TYPE A NAME" : shown, box.X + U(8), box.Y + U(5), box.Width - U(16), shown == "" ? Style.Dim : on ? Kit.Gilt : Style.Text, 2);
        if (ui.Clicked(box)) { typing = on ? 0 : which; Ui.Typed.Clear(); }
        if (ui.Button(new Rectangle(box.Right + U(8), y, U(72), U(24)), "RANDOM")) { roll(); typing = 0; }
        return y + U(34);
    }

    int Swatches(int x, int y, int w, Color[] colours, int chosen)
    {
        int s = Math.Min(U(26), (w - U(8) * (colours.Length - 1)) / colours.Length);
        for (int i = 0; i < colours.Length; i++)
        {
            var r = new Rectangle(x + i * (s + U(8)), y, s, s);
            bool on = i == chosen, hover = ui.Hover(r);
            ui.Rect(new Rectangle(r.X + U(2), r.Y + U(3), r.Width, r.Height), Color.Black * 0.4f);
            ui.VGrad(r, Color.Lerp(colours[i], Color.White, 0.15f), Kit.Mul(colours[i], 0.75f), 2);
            ui.Frame(r, on ? Kit.Gilt : hover ? Kit.Mul(Kit.Gilt, 0.6f) : Color.Black * 0.7f, U(on ? 2 : 1));
            if (ui.Clicked(r)) { chosen = i; Ui.OnClick?.Invoke(); }
        }
        return chosen;
    }

    /// The last step: the whole making read back as an oath on parchment, then BEGIN.
    void Oath(Rectangle r)
    {
        var def = Factions.All[M.Realm];
        var birth = Origins.Find(M.Birth)!; var past = Origins.Find(M.Past)!; var road = Origins.Find(M.Road)!;
        string name = M.Name.Trim() == "" ? "A CAPTAIN WITH NO NAME" : M.Name.Trim();
        string company = M.Company.Trim() == "" ? "MY COMPANY" : M.Company.Trim();
        string service = Origins.Free(M) ? "UNDER NO KING'S COLOURS, ONLY MY OWN" : $"IN THE PAY OF THE {def.Name.ToUpperInvariant()}";
        string text = $"I, {name}, {birth.Name.ToUpperInvariant()} AND ONCE A {past.Name.ToUpperInvariant()}, TAKE UP THE CAPTAIN'S STAFF AT {Origins.Age(M)} YEARS. " +
            $"I FIGHT WITH {road.Name.ToUpperInvariant()}. I LEAD {company}, {Men} MEN, {service}. LET THE ROLL OF THE COMPANY BEGIN WITH ME.";
        var parch = new Rectangle(r.X + U(10), r.Y + U(6), r.Width - U(20), r.Height - U(60));
        ui.VGrad(parch, Kit.Parchment, Kit.Mul(Kit.Parchment, 0.82f), 2);
        ui.Frame(parch, Kit.Mul(Kit.Ink, 0.8f), U(2));
        ui.Frame(Kit.Inset(parch, U(6)), Kit.Mul(Kit.Ink, 0.4f), U(1));
        int sc = parch.Width >= U(420) ? 2 : 1, lh = U(sc == 2 ? 22 : 11);
        var oath = ui.Wrap(text, parch.Width - U(56), sc);
        var tale = ui.Wrap(string.Join(" ", new[] { birth.Line, past.Line }.Where(l => l != "")).ToUpperInvariant(), parch.Width - U(60));
        int height = U(40) + oath.Count * lh + U(16) + tale.Count * U(12);
        int y = Math.Max(parch.Y + U(18), parch.Center.Y - height / 2 - U(10));
        ui.TextC("THE OATH OF THE COMPANY", parch.Center.X, y, Kit.Ink, 2); y += U(24);
        ui.Divider(parch.X + U(40), y, parch.Width - U(80), Kit.Mul(Kit.Ink, 0.9f)); y += U(16);
        foreach (var line in oath) { ui.TextC(line, parch.Center.X, y, Kit.Ink, sc); y += lh; }
        y += U(16);
        foreach (var line in tale) if (y + U(40) < parch.Bottom) { ui.TextC(line, parch.Center.X, y, Kit.Mul(Kit.Ink, 1.5f), 1); y += U(12); }
        // A wax seal in the realm's colour.
        var colours = Origins.Free(M) ? Style.Sides[2] : Style.Sides[def.Palette];
        var seal = new Vector2(parch.Right - U(44), parch.Bottom - U(40));
        ui.Disc(seal + new Vector2(U(2), U(3)), U(20), Color.Black * 0.25f);
        ui.Disc(seal, U(20), Kit.Mul(colours.Cloth, 0.8f));
        ui.Disc(seal, U(15), Kit.Mul(colours.Cloth, 0.6f));
        ui.Shield(new Rectangle((int)seal.X - U(9), (int)seal.Y - U(11), U(18), U(22)), colours);
        if (ui.Gilded(new Rectangle(r.Center.X - U(130), parch.Bottom + U(14), U(260), U(34)), "BEGIN THE CAMPAIGN", "", M.Name.Trim() != "")) Begin();
    }

    void Footer(Rectangle r)
    {
        if (ui.Gilded(new Rectangle(r.X, r.Y, U(150), r.Height), Step == 0 ? "CANCEL" : "BACK", "ESC")) { if (Step == 0) Open = false; else Go(Step - 1); }
        if (ui.Gilded(new Rectangle(r.Center.X - U(96), r.Y, U(192), r.Height), "ROLL THE DICE")) Randomise();
        bool last = Step == Steps.Length - 1;
        if (!last && ui.Gilded(new Rectangle(r.Right - U(150), r.Y, U(150), r.Height), "NEXT", "ENTER")) Go(Step + 1);
        if (last && ui.Gilded(new Rectangle(r.Right - U(150), r.Y, U(150), r.Height), "BEGIN", "ENTER", M.Name.Trim() != "")) Begin();
    }
}
