using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the settlement panel and its tabs (train, recruit, market, food, rest, contracts, fief).
partial class MapScreen
{
    void SettlementPanel()
    {
        var s = at!;
        var r = Modal(860, 500, s.Name.ToUpperInvariant(), SideColor(s.Faction));
        bool fief = s.Owner == C.Player.Id && s.Kind != SettlementKind.Village;
        ui.TextC($"{s.Kind.ToString().ToUpperInvariant()} OF THE {C.FactionNames[s.Faction].ToUpperInvariant()}   GOLD {C.S.Gold:0}",
            r.X + r.Width / 2f, r.Y + ui.U(40), Style.Dim, 1);
        var tabs = (s.Kind switch
        {
            SettlementKind.Village when C.Refuses(s) => new[] { "FORAGE" },
            SettlementKind.Village => new[] { "RECRUIT", "FOOD", "FORAGE", "REST" },
            SettlementKind.Castle => new[] { "REST", "FOOD", "CAPTIVES", "CONTRACT" },
            _ => new[] { "TRAIN", "RECRUIT", "MERCENARIES", "MARKET", "FOOD", "REST", "CAPTIVES", "CONTRACT" },
        }).Concat(fief ? new[] { "FIEF", "GARRISON" } : Array.Empty<string>()).ToArray();
        int tw = ui.U(tabs.Length > 7 ? 90 : 112), tx = r.X + (r.Width - tabs.Length * (tw + ui.U(6))) / 2;
        for (int i = 0; i < tabs.Length; i++)
            if (ui.Button(new Rectangle(tx + i * (tw + ui.U(6)), r.Y + ui.U(56), tw, ui.U(24)), tabs[i], true, "", tab == tabs[i])) { tab = tabs[i]; note = ""; }
        var body = new Rectangle(r.X + ui.U(24), r.Y + ui.U(92), r.Width - ui.U(48), r.Height - ui.U(160));
        switch (tab)
        {
            case "RECRUIT": RecruitTab(s, body); break;
            case "MERCENARIES": MercenariesTab(s, body); break;
            case "MARKET": MarketTab(s, body); break;
            case "TRAIN": TrainTab(s, body); break;
            case "REST": RestTab(s, body); break;
            case "FOOD": FoodTab(s, body); break;
            case "FORAGE": ForageTab(s, body); break;
            case "CAPTIVES": CaptivesTab(s, body); break;
            case "GARRISON": GarrisonTab(s, body); break;
            case "FIEF": FiefTab(s, body); break;
            case "CONTRACT": ContractTab(s, body); break;
        }
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "LEAVE")) panel = PanelKind.None;
    }

    /// Recruit tab: local levies who will take your coin.
    void RecruitTab(Settlement s, Rectangle body)
    {
        var levy = Troops.Get(C.RecruitTroopFor(s));
        int willing = C.RecruitsForYou(s);
        ui.Text($"{willing} OF {s.Recruits} MEN WILL TAKE YOUR COIN HERE ({levy.Cost} CROWNS EACH)", body.X, body.Y, Style.Text);
        if (C.HolderOf(s) is Character holder)
            ui.TextR($"{holder.Name.ToUpperInvariant()}: {OpinionWord(C.OpinionOfYou(holder))}", body.Right, body.Y + ui.U(4), OpinionColor(C.OpinionOfYou(holder)), 1);
        if (s.ThreatUntil > C.S.Hours) ui.Text("THE VILLAGE WAS THREATENED: NO RECRUITS FOR NOW", body.X, body.Y + ui.U(20), Style.Foe, 1);
        ui.Portrait(levy, new Rectangle(body.X, body.Y + ui.U(36), ui.U(60), ui.U(56)));
        ui.Text($"{levy.Name.ToUpperInvariant()} (TIER I) JOIN A COMPANY OF THEIR KIND WITH ROOM, OR FORM A NEW ONE.", body.X + ui.U(70), body.Y + ui.U(44), Style.Dim, 1);
        ui.Text($"THEY ARE {s.Culture.ToUpperInvariant()} MEN: THEY CAN TRAIN INTO {s.Culture.ToUpperInvariant()} AND COMMON LINES. NEW MEN BRING NO XP.", body.X + ui.U(70), body.Y + ui.U(58), Style.Dim, 1);
        int y = body.Y + ui.U(104);
        foreach (var (label, n) in new[] { ("RECRUIT 1", 1), ("RECRUIT 5", 5), ("RECRUIT ALL", 99) })
        {
            if (ui.Button(new Rectangle(body.X, y, ui.U(170), ui.U(28)), label, willing > 0)) note = C.RecruitLevies(s, n) ?? $"RECRUITED. {s.Recruits} LEFT";
            y += ui.U(34);
        }
    }

    /// Mercenaries tab: companies for hire, and a wanderer in the tavern.
    void MercenariesTab(Settlement s, Rectangle body)
    {
        if (s.Mercenaries.Count == 0) ui.Text("NO COMPANIES FOR HIRE THIS WEEK.", body.X, body.Y, Style.Dim);
        int y = body.Y;
        // A wanderer in the tavern, looking for a captain (3.3).
        if (C.WandererAt(s) is Character w)
        {
            ui.Portrait(UnitType.Captain, new Rectangle(body.X, y, ui.U(56), ui.U(54)));
            ui.Text($"{w.Name.ToUpperInvariant()}, A WANDERER", body.X + ui.U(66), y + ui.U(4), Style.Accent);
            ui.Text($"{w.Role.ToUpperInvariant()} ({RoleTextOf(w.Role)}). {string.Join(", ", w.Traits).ToUpperInvariant()}. LEADS A COMPANY AS A HERO.", body.X + ui.U(66), y + ui.U(24), Style.Dim, 1);
            int wp = C.WandererPrice(w);
            if (ui.Button(new Rectangle(body.Right - ui.U(190), y + ui.U(10), ui.U(190), ui.U(30)), "TAKE INTO SERVICE", C.S.Gold >= wp, $"{wp} CROWNS"))
                note = C.HireWanderer(s) ?? "HE JOINS YOUR HOUSEHOLD";
            y += ui.U(66);
        }
        foreach (var m in s.Mercenaries.ToList())
        {
            ui.Portrait(m.Type, new Rectangle(body.X, y, ui.U(56), ui.U(54)));
            ui.Text(m.Name.ToUpperInvariant(), body.X + ui.U(66), y + ui.U(4), Style.Text);
            ui.Text(string.Join(", ", m.Stacks.Select(st => $"{st.Men} {st.Type.Name.ToUpperInvariant()} {Style.Numerals[st.Type.Tier]}")) + (m.Honours.Count > 0 ? "  HONOURS: " + string.Join(", ", m.Honours.Select(h => Honours.Get(h)?.Name.ToUpperInvariant() ?? h)) : ""), body.X + ui.U(66), y + ui.U(24), Style.Dim, 1);
            int price = C.MercenaryPrice(m);
            if (ui.Button(new Rectangle(body.Right - ui.U(190), y + ui.U(10), ui.U(190), ui.U(30)), "HIRE", C.S.Gold >= price, $"{price} CROWNS"))
                note = C.Hire(s, m) ?? "HIRED";
            y += ui.U(66);
        }
    }

    /// Market tab: horses and your own gear.
    void MarketTab(Settlement s, Rectangle body)
    {
        ui.Text($"HORSES: {s.Horses} FOR SALE AT {C.HorsePrice} CROWNS. YOU HAVE {C.Player.Horses}.", body.X, body.Y, Style.Text);
        ui.Text("MEN PROMOTED INTO CAVALRY NEED A HORSE EACH (OR PAY FOR ONE WHEN PROMOTED).", body.X, body.Y + ui.U(20), Style.Dim, 1);
        if (ui.Button(new Rectangle(body.X, body.Y + ui.U(40), ui.U(150), ui.U(28)), "BUY 1", s.Horses > 0)) note = C.BuyHorses(s, 1) ?? "BOUGHT";
        if (ui.Button(new Rectangle(body.X + ui.U(160), body.Y + ui.U(40), ui.U(150), ui.U(28)), "BUY 5", s.Horses > 0)) note = C.BuyHorses(s, 5) ?? "BOUGHT";
        ui.Text("YOUR GEAR", body.X, body.Y + ui.U(92), Style.Accent);
        var gear = C.Gear();
        for (int i = 0; i < gear.Length; i++)
        {
            var (name, cost, owned, arms) = gear[i];
            var br = new Rectangle(body.X + (i % 2) * ui.U(370), body.Y + ui.U(116) + i / 2 * ui.U(36), ui.U(360), ui.U(30));
            bool able = C.Skill("Arms") >= arms;
            if (ui.Button(br, name, !owned && able, owned ? "OWNED" : able ? $"{cost} CROWNS" : $"NEEDS ARMS {arms}")) note = C.BuyGear(i) ?? "BOUGHT";
        }
    }

    /// Train tab: promote men with enough XP up their line.
    void TrainTab(Settlement s, Rectangle body)
    {
        ui.Text("MEN WITH ENOUGH XP CAN BE PROMOTED UP THEIR LINE HERE, ONE BY ONE OR ALL AT ONCE.", body.X, body.Y, Style.Dim, 1);
        int y = body.Y + ui.U(18);
        foreach (var (c, st) in C.Player.Companies.SelectMany(c => c.Stacks.Select(st => (c, st))).OrderByDescending(x => x.st.Ready).ToList())
        {
            ui.Portrait(st.Type, new Rectangle(body.X, y, ui.U(40), ui.U(38)));
            ui.Text($"{c.Name.ToUpperInvariant()}: {st.Men} {st.Type.Name.ToUpperInvariant()} {Style.Numerals[st.Type.Tier]}", body.X + ui.U(48), y + ui.U(4), Style.Text, 1);
            ui.Text(st.Type.UpgradesTo.Length == 0 ? "AT THE TOP OF ITS LINE" : $"{st.Ready} READY   POOL {st.Xp:0} / {st.PoolCap:0} XP ({st.Type.UpgradeXp} A MAN)", body.X + ui.U(48), y + ui.U(18), st.Ready > 0 ? Style.Accent : Style.Dim, 1);
            if (st.Type.UpgradesTo.Length > 0 && ui.Button(new Rectangle(body.Right - ui.U(160), y + ui.U(4), ui.U(160), ui.U(28)), "PROMOTE...", st.Ready > 0))
            { upgrading = c; upStack = st; panel = PanelKind.Upgrade; note = ""; }
            y += ui.U(42);
            if (y > body.Bottom - ui.U(30)) break;
        }
    }

    /// Rest tab: wait here to heal the wounded.
    void RestTab(Settlement s, Rectangle body)
    {
        ui.Text($"YOUR WOUNDED: {C.Player.Wounded}. THEY HEAL {(s.Kind == SettlementKind.Town ? "QUICKLY IN A TOWN" : "SLOWLY HERE")}.", body.X, body.Y, Style.Text);
        ui.Text("RESTING IN A TOWN ALSO RESTORES COHESION (UP TO 20).", body.X, body.Y + ui.U(20), Style.Dim, 1);
        if (ui.Button(new Rectangle(body.X, body.Y + ui.U(44), ui.U(200), ui.U(28)), "REST 1 DAY")) { C.Rest(24); note = $"RESTED. WOUNDED: {C.Player.Wounded}"; }
        if (ui.Button(new Rectangle(body.X + ui.U(210), body.Y + ui.U(44), ui.U(250), ui.U(28)), "REST UNTIL HEALED", C.Player.Wounded > 0))
        {
            for (int d = 0; d < 10 && C.Player.Wounded > 0 && C.Encounter == null; d++) C.Rest(24);
            note = $"RESTED. WOUNDED: {C.Player.Wounded}";
        }
    }

    /// Food tab: buy days of food.
    void FoodTab(Settlement s, Rectangle body)
    {
        ui.Text($"YOU CARRY FOOD FOR {C.Player.Food:0} DAYS. {C.Season.ToUpperInvariant()}{(C.Winter ? ": MEN EAT HALF AGAIN AS MUCH" : "")}.", body.X, body.Y, Style.Text);
        ui.Text("PRISONERS EAT TOO. A STARVING COMPANY LOSES COHESION AND MEN EVERY DAY.", body.X, body.Y + ui.U(20), Style.Dim, 1);
        int y = body.Y + ui.U(44);
        foreach (int days in new[] { 3, 7, 14 })
        {
            if (ui.Button(new Rectangle(body.X, y, ui.U(300), ui.U(28)), $"BUY {days} DAYS", C.S.Gold >= C.FoodPrice(days), $"{C.FoodPrice(days)} CROWNS")) note = C.BuyFood(days) ?? "BOUGHT";
            y += ui.U(34);
        }
    }

    /// Forage tab: take food from a village by force.
    void ForageTab(Settlement s, Rectangle body)
    {
        ui.Text(C.Refuses(s) ? "AN ENEMY VILLAGE. ITS PEOPLE BAR THEIR DOORS." : "YOUR MEN CAN TAKE WHAT THEY NEED BY FORCE.", body.X, body.Y, Style.Text);
        ui.Text("FORAGING GIVES A WEEK OF FOOD, AND LEAVES THE VILLAGE WITH NO RECRUITS FOR A WEEK.", body.X, body.Y + ui.U(20), Style.Dim, 1);
        ui.Text(C.Refuses(s) ? "RAIDING AN ENEMY'S VILLAGE COUNTS IN THE WAR." : "YOUR LIEGE'S PEOPLE WILL REMEMBER IT: -5 RENOWN.", body.X, body.Y + ui.U(34), C.Refuses(s) ? Style.Ok : Style.Warn, 1);
        if (ui.Button(new Rectangle(body.X, body.Y + ui.U(56), ui.U(240), ui.U(28)), "FORAGE", s.ThreatUntil <= C.S.Hours)) note = C.Forage(s) ?? "FORAGED";
    }

    /// Captives tab: sell prisoners, ransom or free captive lords.
    void CaptivesTab(Settlement s, Rectangle body)
    {
        int y = body.Y;
        if (C.PrisonerCount == 0) ui.Text("YOU HOLD NO PRISONERS. MEN CAPTURED IN BATTLE ARE KEPT UNTIL YOU SELL THEM HERE.", body.X, y, Style.Dim, 1);
        else
        {
            ui.Text($"{C.PrisonerCount} PRISONERS: " + string.Join(", ", C.Player.Prisoners.Select(g => $"{g.Count} {Troops.Get(g.Troop).Name.ToUpperInvariant()}")), body.X, y, Style.Text, 1);
            if (ui.Button(new Rectangle(body.X, y + ui.U(18), ui.U(340), ui.U(28)), "SELL THEM ALL", true, $"{C.PrisonerValue} CROWNS")) note = C.SellPrisoners(s) ?? "SOLD";
        }
        y += ui.U(64);
        ui.Text("LORDS YOU HOLD FOR RANSOM", body.X, y, Style.Accent, 1);
        y += ui.U(16);
        foreach (int id in C.Player.Captives.ToList())
        {
            if (C.PartyById(id) is not Party lord) continue;
            ui.Text($"{lord.Name.ToUpperInvariant()} OF THE {C.FactionNames[lord.Faction].ToUpperInvariant()}", body.X, y + ui.U(6), SideColor(lord.Faction), 1);
            if (ui.Button(new Rectangle(body.Right - ui.U(360), y, ui.U(200), ui.U(24)), "RANSOM", true, $"{C.LordRansom(lord)}")) note = C.RansomLord(lord, s) ?? "RANSOMED";
            if (ui.Button(new Rectangle(body.Right - ui.U(150), y, ui.U(150), ui.U(24)), "FREE (+RENOWN)")) C.FreeLord(lord);
            y += ui.U(30);
        }
        if (C.Player.Captives.Count == 0) ui.Text("NONE. A LORD BEATEN BY YOUR HAND MAY BE TAKEN.", body.X, y, Style.Dim, 1);
    }

    /// Garrison tab (your fief): station or withdraw companies.
    void GarrisonTab(Settlement s, Rectangle body)
    {
        ui.Text($"YOUR FIEF. IT PAYS YOU {C.Income(s) + C.S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village).Sum(C.Income):0} CROWNS A WEEK. SUPPLIES {s.Supplies:0} DAYS.", body.X, body.Y, Style.Text, 1);
        int y = body.Y + ui.U(18);
        foreach (var g in s.Garrison.ToList())
        {
            ui.Text($"{g.Name.ToUpperInvariant()}: {g.Men} {g.Type.Name.ToUpperInvariant()}", body.X, y + ui.U(6), Style.Text, 1);
            if (ui.Button(new Rectangle(body.Right - ui.U(150), y, ui.U(150), ui.U(22)), "TAKE WITH YOU")) note = C.Withdraw(g, s) ?? "";
            y += ui.U(26);
        }
        y += ui.U(8);
        ui.Text("LEAVE A COMPANY TO HOLD THE WALLS:", body.X, y, Style.Dim, 1);
        y += ui.U(16);
        foreach (var c in C.Player.Companies.ToList())
        {
            if (y > body.Bottom - ui.U(20)) break;
            if (ui.Button(new Rectangle(body.X, y, ui.U(420), ui.U(22)), $"STATION {c.Name.ToUpperInvariant()} ({c.Men})", C.Player.Companies.Count > 1)) note = C.Station(c, s) ?? "";
            y += ui.U(26);
        }
    }

    /// Contract tab: sign or end a contract, and the steward's board of jobs.
    void ContractTab(Settlement s, Rectangle body)
    {
        int cur = C.Captain.Contract;
        if (s.Kind == SettlementKind.Town && cur == s.Faction)
        {
            ui.Text("THE STEWARD'S BOARD", body.X, body.Y + ui.U(130), Style.Accent, 1);
            int y = body.Y + ui.U(146);
            foreach (var o in C.Offers(s).ToList())
            {
                ui.Text(o.Text.ToUpperInvariant(), body.X, y + ui.U(6), Style.Text, 1);
                if (ui.Button(new Rectangle(body.Right - ui.U(200), y, ui.U(200), ui.U(24)), "ACCEPT", C.S.Contracts.Count < 3, $"{o.Reward:0}")) note = C.Accept(s, o) ?? "ACCEPTED";
                y += ui.U(28);
                if (y > body.Bottom - ui.U(24)) break;
            }
        }
        ui.Text(cur >= 0 ? $"YOU SERVE THE {C.FactionNames[cur].ToUpperInvariant()}." : "YOU ARE A FREE CAPTAIN: NO PAY, NO ENEMIES BUT BRIGANDS.", body.X, body.Y, Style.Text);
        ui.Text("A CONTRACT PAYS 85% OF YOUR UPKEEP WEEKLY, AND BOUNTIES FOR BRIGANDS AND ENEMY LORDS.", body.X, body.Y + ui.U(20), Style.Dim, 1);
        ui.Text("YOUR EMPLOYER'S ENEMIES BECOME YOURS.", body.X, body.Y + ui.U(34), Style.Dim, 1);
        if (cur != s.Faction && ui.Button(new Rectangle(body.X, body.Y + ui.U(56), ui.U(380), ui.U(28)), $"SIGN WITH THE {C.FactionNames[s.Faction].ToUpperInvariant()}"))
            note = C.SignContract(s.Faction) ?? "";
        if (cur >= 0 && ui.Button(new Rectangle(body.X, body.Y + ui.U(92), ui.U(380), ui.U(28)), C.Captain.Fealty ? "RENOUNCE YOUR OATH" : "END YOUR CONTRACT")) note = C.SignContract(-1) ?? "";
    }

    /// Your accepted contracts, under the overview map.
    void Contracts()
    {
        if (C.S.Contracts.Count == 0) return;
        int w = ui.U(230), x = ui.W - w - ui.U(10), y = ui.U(194);
        var r = new Rectangle(x, y, w, ui.U(20) + C.S.Contracts.Count * ui.U(26));
        ui.Panel(r);
        ui.Text("CONTRACTS", x + ui.U(8), y + ui.U(6), Style.Accent, 1);
        y += ui.U(20);
        foreach (var c in C.S.Contracts)
        {
            string t = c.Text.ToUpperInvariant();
            if (t.Length > 36) t = t[..35] + ".";
            ui.Text(t, x + ui.U(8), y, Style.Text, 1);
            float days = (c.Deadline - (float)C.S.Hours) / 24;
            ui.Text($"{days:0.0} DAYS  {c.Reward:0} CROWNS" + (c.Kind == "defend" ? $"  {c.Progress:0}/24 H" : ""), x + ui.U(8), y + ui.U(11), days < 2 ? Style.Warn : Style.Dim, 1);
            y += ui.U(26);
        }
    }

    /// Your fief (F6): its prosperity and unrest and why they move, its taxes, and its buildings.
    void FiefTab(Settlement s, Rectangle body)
    {
        int x = body.X, y = body.Y, half = body.Width / 2 - ui.U(10);
        ui.Text($"PROSPERITY {s.Prosperity:0}", x, y, Style.Text);
        ui.TextR($"{C.ProsperityDrift(s):+0.00;-0.00} A DAY", x + half, y + ui.U(2), Style.Dim, 1);
        ui.Bar(new Rectangle(x, y + ui.U(18), half, ui.U(5)), s.Prosperity / 100f, Style.Ok);
        ui.Text($"INCOME {C.Income(s) + C.S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village && v.Owner == C.Player.Id).Sum(C.Income):0} A WEEK WITH ITS VILLAGES", x, y + ui.U(28), Style.Dim, 1);
        int ux = x + half + ui.U(20);
        var (drift, why) = C.UnrestDrift(s);
        ui.Text($"UNREST {s.Unrest:0}", ux, y, s.Unrest >= Campaign.RevoltAt ? Style.Bad : Style.Text);
        ui.TextR($"{drift:+0.00;-0.00} A DAY", ux + half, y + ui.U(2), drift > 0 ? Style.Warn : Style.Dim, 1);
        ui.Bar(new Rectangle(ux, y + ui.U(18), half, ui.U(5)), s.Unrest / 100f, s.Unrest >= Campaign.RevoltAt ? Style.Bad : Style.Warn);
        ui.Text(why.Count == 0 ? "THE PEOPLE ARE CONTENT." : string.Join("  ", why), ux, y + ui.U(28), Style.Dim, 1);
        if (C.Rebels(s) is Party reb) ui.Text($"{reb.Men} REBELS ARE IN THE FIELD: BEAT THEM TO CALM THE PLACE.", ux, y + ui.U(40), Style.Bad, 1);
        y += ui.U(56);
        ui.Text("TAXES:", x, y + ui.U(6), Style.Dim, 1);
        for (int i = 0; i < 3; i++)
            if (ui.Button(new Rectangle(x + ui.U(60) + i * ui.U(110), y, ui.U(104), ui.U(24)), Campaign.TaxNames[i], true, "", s.Tax == i)) note = C.SetTax(s, i) ?? "";
        ui.Text("LOW: INCOME -30%, CALMER, GROWS FASTER.  HIGH: INCOME +35%, UNREST, SLOWER GROWTH.", x + ui.U(400), y + ui.U(6), Style.Dim, 1);
        y += ui.U(34);
        if (s.Building != "") ui.Text($"BUILDING {Campaign.BuildingOf(s.Building)?.Name.ToUpperInvariant()}: {MathF.Max(0, (s.BuildUntil - (float)C.S.Hours) / 24):0.0} DAYS LEFT", x, y, Style.Accent, 1);
        else ui.Text("BUILD (ONE AT A TIME):", x, y, Style.Accent, 1);
        y += ui.U(16);
        int bw = (body.Width - ui.U(8)) / 2, i2 = 0;
        foreach (var d in Campaign.BuildingDefs)
        {
            var row = new Rectangle(x + (i2 % 2) * (bw + ui.U(8)), y + (i2 / 2) * ui.U(28), bw, ui.U(24));
            int lv = Campaign.Level(s, d.Id);
            bool can = C.CanBuild(s, d, out string why2, out int cost);
            string label = $"{d.Name.ToUpperInvariant()}{(d.Max > 1 ? $" {Style.Numerals[Math.Min(d.Max, lv + (lv < d.Max ? 1 : 0))]}" : "")}";
            if (ui.Button(row, lv >= d.Max ? label + " (BUILT)" : label, can, can ? $"{cost}" : why2, lv > 0)) note = C.Build(s, d.Id) ?? $"WORK BEGINS ON THE {d.Name.ToUpperInvariant()}";
            if (ui.Hover(row)) note = d.Text;
            i2++;
        }
    }
}
