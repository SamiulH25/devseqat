using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the knight orders (docs/KNIGHT_ORDERS.md 4.5, K4): THE ORDERS tab on the realm screen (R) and the CHAPTER tab
/// on an order's chapter town (the trial, rising, brothers, contracts, leaving).
partial class MapScreen
{
    /// Every order: its patron, chapter, creed and vows, what it thinks of you, and where you stand in yours.
    void OrdersTab(Rectangle a)
    {
        int x = a.X + ui.U(4), w = a.Width - ui.U(8), y = a.Y;
        var cap = C.Captain;
        if (C.MyOrder is OrderDef mine)
        {
            ui.Text($"YOU ARE {C.OrderRankName.ToUpperInvariant()}: {mine.Name.ToUpperInvariant()}", x, y, Kit.Gilt, 2);
            y += ui.U(20);
            string next = C.CannotRise() is string why ? $"TO RISE: {why}" : "YOU MAY RISE A RANK AT THE CHAPTER HOUSE";
            ui.Text(ui.Fit($"ORDER DEEDS {cap.OrderDeeds}. {(cap.Penance ? "YOU OWE A PENANCE: WIN A BATTLE IN THE ORDER'S NAME. " : "")}{next}", w), x, y, cap.Penance ? Style.Warn : Style.Dim, 1);
        }
        else
        {
            ui.Text("YOU ARE SWORN TO NO ORDER", x, y, Style.Text, 2);
            y += ui.U(20);
            ui.Text(ui.Fit($"WIN BATTLES AGAINST WHAT AN ORDER HUNTS, OR ITS PATRON'S ENEMIES, AND IT WILL OFFER ITS TRIAL AT STANDING {Campaign.TrialStanding:0}.", w), x, y, Style.Dim, 1);
        }
        y += ui.U(26);
        foreach (var o in C.S.Orders)
        {
            if (y > a.Bottom - ui.U(56)) break;
            var def = KnightOrders.Get(o.Id);
            var row = new Rectangle(x - ui.U(4), y - ui.U(3), w + ui.U(8), ui.U(54));
            ui.Rect(row, o.Id == cap.Order ? Kit.Gilt * 0.12f : Color.White * 0.04f);
            ui.Shield(new Rectangle(x, y, ui.U(28), ui.U(34)), Style.Sides[Style.PaletteOf(o.Patron)]);
            int tx = x + ui.U(38);
            string where = o.Gone ? "GONE" : $"FOR THE {C.FactionNames[o.Patron].ToUpperInvariant()}, CHAPTER AT {C.SettlementById(o.Chapter)?.Name.ToUpperInvariant() ?? "NOWHERE"}";
            ui.Text(def.Name.ToUpperInvariant(), tx, y, Style.Text, 1);
            ui.Text(ui.Fit(where, w / 2), tx + ui.TW(def.Name.ToUpperInvariant(), 1) + ui.U(10), y, Style.Dim, 1);
            ui.TextR($"STANDING {o.Standing:+0;-0;0}", x + w, y, o.Standing >= Campaign.TrialStanding ? Style.Ok : o.Standing < 0 ? Style.Bad : Style.Dim, 1);
            ui.Text(ui.Fit(def.Creed.ToUpperInvariant(), w - ui.U(40)), tx, y + ui.U(14), Kit.Parchment, 1);
            string vows = string.Join(", ", def.Vows.Select(VowWord));
            string hunts = def.Hunts == "" ? "" : $"   HUNTS {def.Hunts.ToUpperInvariant()}";
            string brothers = string.Join(", ", def.Troops.Select(t => Troops.Get(t).Name.ToUpperInvariant()));
            ui.Text(ui.Fit($"VOWS: {vows}{hunts}   BROTHERS: {brothers}", w - ui.U(40)), tx, y + ui.U(28), Style.Dim, 1);
            y += ui.U(60);
        }
    }

    static string VowWord(string vow) => vow switch
    {
        "no_rob" => "NEVER ROB THE ROAD",
        "no_slaves" => "NEVER SELL PRISONERS",
        "no_auto" => "MEET EVERY FOE IN PERSON",
        "no_desert" => "NEVER FLEE A BATTLE",
        "contract" => "NEVER FAIL A CONTRACT",
        "no_craft" => "NO CRAFT",
        _ => vow.ToUpperInvariant(),
    };

    /// An order's chapter house: seek its trial, or (sworn) rise, take brothers and contracts, train by resting here, leave.
    void ChapterTab(Settlement s, Rectangle body)
    {
        var o = C.ChapterAt(s)!;
        var def = KnightOrders.Get(o.Id);
        var cap = C.Captain;
        int x = body.X, y = body.Y, w = body.Width;
        ui.Text($"CHAPTER HOUSE: {def.Name.ToUpperInvariant()}", x, y, Kit.Gilt, 2);
        y += ui.U(22);
        ui.Text(ui.Fit(def.Text.ToUpperInvariant(), w), x, y, Style.Dim, 1);
        y += ui.U(14);
        ui.Text(ui.Fit($"{def.Creed.ToUpperInvariant()}  VOWS: {string.Join(", ", def.Vows.Select(VowWord))}", w), x, y, Kit.Parchment, 1);
        y += ui.U(14);
        string gm = C.CharacterById(o.GrandMaster) is Character g && !g.Dead ? g.Name.ToUpperInvariant() : "NONE";
        ui.Text(ui.Fit($"GRAND MASTER: {gm}.   THEY THINK {o.Standing:+0;-0;0} OF YOU.", w), x, y, Style.Dim, 1);
        y += ui.U(26);
        var bw = ui.U(250);
        if (cap.Order != o.Id)
        {
            string? why = C.CannotSeekTrial(o.Id);
            ui.Text(ui.Fit(why ?? "A KNIGHT OF THE ORDER WILL MEET YOU IN THE YARD. WIN, AND YOU SWEAR THE OATH AS ITS SQUIRE.", w - bw - ui.U(12)), x, y + ui.U(8), why == null ? Style.Text : Style.Dim, 1);
            if (ui.Button(new Rectangle(x + w - bw, y, bw, ui.U(28)), "SEEK THE TRIAL", why == null))
            {
                C.TrialPending = o.Id;
                panel = PanelKind.None;
            }
            return;
        }
        ui.Text($"YOU ARE {C.OrderRankName.ToUpperInvariant()}. ORDER DEEDS {cap.OrderDeeds}.", x, y + ui.U(8), Style.Text, 1);
        string? rise = C.CannotRise();
        int next = Math.Min(cap.OrderRank + 1, Campaign.OrderRanks.Length - 1);
        if (ui.Button(new Rectangle(x + w - bw, y, bw, ui.U(28)), $"RISE: {Campaign.OrderRanks[next].ToUpperInvariant()}", rise == null, rise == null ? "" : ""))
            note = C.RiseInOrder() ?? $"YOU ARE {C.OrderRankName.ToUpperInvariant()}";
        if (rise != null) ui.Text(ui.Fit(rise, w - bw - ui.U(12)), x, y + ui.U(22), Style.Dim, 1);
        y += ui.U(44);
        string troop = C.BrotherTroop;
        string bro = cap.OrderRank < 1 ? "BROTHERS RIDE WITH A KNIGHT-BROTHER, NOT A SQUIRE" : $"{C.BrotherMen} {Troops.Get(troop).Name.ToUpperInvariant()} RIDE WITH YOU (ONCE A WEEK)";
        ui.Text(ui.Fit(bro, w - bw - ui.U(12)), x, y + ui.U(8), Style.Text, 1);
        if (ui.Button(new Rectangle(x + w - bw, y, bw, ui.U(28)), "TAKE BROTHERS", cap.OrderRank >= 1, cap.OrderRank >= 1 ? $"{C.BrothersPrice} CROWNS" : ""))
            note = C.RecruitBrothers(s) ?? "THE BROTHERS JOIN YOU";
        y += ui.U(36);
        ui.Text(ui.Fit($"AN ORDER CONTRACT: DESTROY A FOE OF {def.Short.ToUpperInvariant()}. DONE, AN ORDER DEED.", w - bw - ui.U(12)), x, y + ui.U(8), Style.Text, 1);
        if (ui.Button(new Rectangle(x + w - bw, y, bw, ui.U(28)), "TAKE A CONTRACT"))
            note = C.TakeOrderContract(s) ?? "THE ORDER NAMES YOUR FOE (CONTRACTS)";
        y += ui.U(36);
        ui.Text("REST HERE (REST TAB) AND YOUR AURA GROWS IN THE YARD.", x, y + ui.U(8), Style.Dim, 1);
        if (ui.Button(new Rectangle(x + w - bw, y, bw, ui.U(28)), "LEAVE THE ORDER"))
            note = C.LeaveOrder() ?? "YOU LEAVE THE ORDER";
    }
}
