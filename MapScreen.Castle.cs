using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the castle's MUSTER tab (docs/archive/HOLDS.md HD2): the demesne's recruits pooled, the stables, the granary and the yard.
partial class MapScreen
{
    void MusterTab(Settlement s, Rectangle body)
    {
        var B = Balance.Get();
        string? why = C.CannotMuster(s);
        var villages = C.Demesne(s).ToList();
        int pool = C.MusterRecruits(s), cap = Math.Max(1, Math.Max(pool, villages.Sum(v => v.Recruits)));
        var levy = Troops.Get(villages.Count > 0 ? C.RecruitTroopFor(villages.MaxBy(v => v.Recruits)!) : Factions.Root(s.Culture));
        var row = new Rectangle(body.X, body.Y, body.Width, ui.U(104));
        TroopOffer(row, levy, Style.PaletteOf(s.Faction), $"{villages.Count} VILLAGE{(villages.Count == 1 ? "" : "S")}", pool, cap, C.MusterPrice(s), new[]
        {
            ("house", "THE MEN OF ALL ITS VILLAGES, IN ONE PLACE", 1),
            ("coin", $"THE CASTELLAN TAKES {(B.MusterMarkup - 1) * 100:0}% ON TOP", -1),
        }, why == null && pool > 0, n => C.Muster(s, n), () => $"MUSTERED. {C.MusterRecruits(s)} LEFT");
        int gap = ui.U(12), cw = (body.Width - gap * 2) / 3, cy = row.Bottom + gap, ch = Math.Min(ui.U(112), body.Bottom - cy - ui.U(30));
        Rectangle Col(int i) => new(body.X + i * (cw + gap), cy, cw, ch);

        // Stables: remounts bred at the castle.
        var k = Card(Col(0), s.Horses > 0 ? Kit.Gilt : null, true);
        ui.Text("STABLES", k.X, k.Y, Kit.Gilt, 1);
        Stat(k.X, k.Y + ui.U(16), "horse", $"{s.Horses}", "HORSES", s.Horses > 0 ? Style.Text : Style.Dim);
        Price(k.X, k.Y + ui.U(46), C.HorsePrice, "EACH");
        int bw = (k.Width - ui.U(6)) / 2;
        foreach (var (label, n, i) in new[] { ("BUY 1", 1, 0), ("BUY 5", 5, 1) })
            if (ui.Button(new Rectangle(k.X + i * (bw + ui.U(6)), k.Bottom - ui.U(24), bw, ui.U(24)), label, why == null && s.Horses > 0))
                note = C.BuyHorses(s, n) ?? $"BOUGHT. {s.Horses} LEFT";

        // Granary: food from the castle's stores, fed by its villages.
        int spare = C.GranaryDays(s);
        k = Card(Col(1), spare > 0 ? Kit.Gilt : null, true);
        ui.Text("GRANARY", k.X, k.Y, Kit.Gilt, 1);
        Stat(k.X, k.Y + ui.U(16), "bread", $"{spare}", "DAYS TO SPARE", spare > 0 ? Style.Text : Style.Dim);
        ui.TextFit($"YOU HAVE {C.Player.Food:0} DAYS", k.X, k.Y + ui.U(48), k.Width, Style.Dim, 1);
        foreach (var (days, i) in new[] { (3, 0), (7, 1) })
        {
            int price = C.GranaryPrice(Math.Min(days, Math.Max(1, spare)));
            if (ui.Button(new Rectangle(k.X + i * (bw + ui.U(6)), k.Bottom - ui.U(24), bw, ui.U(24)), $"+{days} DAYS", why == null && spare > 0 && C.S.Gold >= price, $"{price}"))
                note = C.BuyFromGranary(s, days) ?? "BOUGHT";
        }

        // The yard: promotion past the camp's tier.
        int ready = C.Player.Companies.Sum(c => c.Stacks.Where(st => st.Type.UpgradesTo.Any(t => C.CanTrainHere(C.Player, t))).Sum(st => st.Ready));
        k = Card(Col(2), ready > 0 ? Kit.Gilt : null, true);
        ui.Text("THE YARD", k.X, k.Y, Kit.Gilt, 1);
        Stat(k.X, k.Y + ui.U(16), "up", $"TIER {Style.Numerals[Math.Clamp(B.CastleYardTier, 1, 7)]}", "TRAINS UP TO", Style.Text);
        ui.TextFit($"{ready} MEN READY HERE", k.X, k.Y + ui.U(48), k.Width, ready > 0 ? Kit.Gilt : Style.Dim, 1);
        if (ui.Button(new Rectangle(k.X, k.Bottom - ui.U(24), k.Width, ui.U(24)), "TO THE YARD", why == null, "", ready > 0)) { tab = "TRAIN"; note = ""; }

        Outcome(body.X, cy + ch + ui.U(8), body.Width, why == null ? "flag" : "chain",
            why ?? "HIGHER TIERS, MERCENARIES, TRADE AND FINE GEAR STILL NEED A TOWN", why == null ? 0 : -1);
    }
}
