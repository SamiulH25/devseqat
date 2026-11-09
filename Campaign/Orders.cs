using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// One knight order from Data/orders.json (docs/KNIGHT_ORDERS.md 4).
public sealed class OrderDef
{
    public string Id = "", Name = "", Short = "", Patron = "", Hunts = "", Creed = "", Text = "";
    public string[] Troops = Array.Empty<string>(), Vows = Array.Empty<string>();
    public bool Hidden;
}

/// The knight order registry, loaded once from Data/orders.json.
public static class KnightOrders
{
    public const string FileName = "Data/orders.json";
    static List<OrderDef>? all;

    public static IReadOnlyList<OrderDef> All { get { Load(); return all!; } }
    public static OrderDef? Find(string id) { Load(); return all!.FirstOrDefault(o => o.Id == id); }
    public static OrderDef Get(string id) => Find(id) ?? throw new KeyNotFoundException("no order " + id);

    /// The vows a creed may hold (Campaign.Vows, K4).
    public static readonly string[] VowKinds = { "no_rob", "no_slaves", "no_auto", "no_desert", "contract", "no_craft" };

    sealed class FileDto { public List<OrderDef> Orders = new(); }

    static void Load()
    {
        if (all != null) return;
        string path = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(d => Path.Combine(d, FileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("order data not found", FileName);
        var opts = new JsonSerializerOptions
        {
            IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        all = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), opts)!.Orders;
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("orders.json: " + string.Join("; ", problems));
    }

    /// Data checks: ids unique, the patron a realm, three signature troops of this order rising in tier, vows known.
    public static IEnumerable<string> Validate()
    {
        Load();
        if (all!.Select(o => o.Id).Distinct().Count() != all!.Count) yield return "order ids repeat";
        foreach (var o in all!)
        {
            if (o.Name == "" || o.Short == "" || o.Creed == "") yield return $"{o.Id}: needs a name, a short name and a creed";
            if (!o.Hidden && Factions.IndexOf(o.Patron) < 0) yield return $"{o.Id}: unknown patron {o.Patron}";
            if (o.Hunts is not ("" or "undead" or "monsters" or "bandits")) yield return $"{o.Id}: unknown hunt {o.Hunts}";
            if (o.Troops.Length != 3) { yield return $"{o.Id}: needs three signature troops"; continue; }
            for (int i = 0; i < 3; i++)
            {
                if (!Ironfield.Troops.All.Any(t => t.Id == o.Troops[i])) { yield return $"{o.Id}: unknown troop {o.Troops[i]}"; continue; }
                var t = Ironfield.Troops.Get(o.Troops[i]);
                if (t.Order != o.Id) yield return $"{o.Troops[i]}: must carry order {o.Id}";
                if (t.Tier != 4 + i) yield return $"{o.Troops[i]}: signature troops are tiers IV, V and VI";
            }
            foreach (var v in o.Vows) if (!VowKinds.Contains(v)) yield return $"{o.Id}: unknown vow {v}";
        }
    }
}

/// Knight orders on the map (docs/KNIGHT_ORDERS.md 4, K3): each has a chapter house in a patron town, a grand master and two
/// knight-commanders whose armies ride with the patron's lords, their ranks part signature troops, part the patron's line.
/// Their dice come from their own stream (CampaignState.OrderRng), so adding them leaves the rest of the campaign's draws alone.
public sealed partial class Campaign
{
    Rng? orderRng;
    Rng ORng => orderRng ??= Rng.Orders(S);
    /// Knight-commanders' armies an order keeps in the field.
    public const int OrderArmies = 2;
    /// The share of an order army that is its own brothers (the rest: the patron's line troops).
    public const float SignatureShare = 0.4f;
    /// Days before an order raises a fallen commander's army again.
    public const float OrderRespawnDays = 10;

    public OrderState? OrderById(string id) => S.Orders.FirstOrDefault(o => o.Id == id);
    public OrderDef? OrderOf(Party p) => p.KnightOrder == "" ? null : KnightOrders.Find(p.KnightOrder);
    public IEnumerable<Party> OrderParties(string id) => S.Parties.Where(p => p.KnightOrder == id);

    /// Run with the orders' dice standing in for the main ones (company names, banners, sergeants), so raising an order's men
    /// leaves the campaign's own draws as they were.
    void OnOrderDice(Action f)
    {
        var main = rng;
        rng = ORng;
        try { f(); } finally { rng = main; }
    }

    /// Bring the save's orders in line with the data: a new order gets its chapter, grand master and armies (old saves too).
    void SyncOrders()
    {
        if (S.Settlements.Count == 0 || S.Parties.Count == 0) return;
        OnOrderDice(AddOrders);
    }

    void AddOrders()
    {
        foreach (var def in KnightOrders.All.Where(d => !d.Hidden))
        {
            if (OrderById(def.Id) != null) continue;
            int patron = S.Factions.FindIndex(f => f.Name == def.Patron && !f.Founded);
            if (patron < 0 || !Alive(patron)) continue;
            var o = new OrderState { Id = def.Id, Patron = patron, Chapter = ChapterIn(patron) };
            S.Orders.Add(o);
            var gm = MakeKnight(def, patron, 4);
            gm.Name = "Grand Master " + gm.Name;
            o.GrandMaster = gm.Id;
            for (int i = 0; i < OrderArmies; i++) SpawnOrderArmy(o);
        }
    }

    /// The patron's chapter town: its first town, so the choice never moves.
    int ChapterIn(int patron) =>
        S.Settlements.Where(s => s.Faction == patron && s.Kind != SettlementKind.Village).OrderBy(s => s.Id).Select(s => s.Id).DefaultIfEmpty(-1).First();

    /// A knight of the order: rank 2 (knight-commander) or 4 (grand master), with an aura to match.
    Character MakeKnight(OrderDef def, int faction, int rank)
    {
        string culture = Factions.CultureOf(faction);
        var n = Names.Get();
        var ch = new Character
        {
            Id = S.NextId++, Kind = "knight", Faction = faction, Culture = culture, Age = 28 + ORng.Next(30),
            Name = "Sir " + ORng.Pick(n.LordsOf(culture)) + " " + ORng.Pick(n.Epithets), Order = def.Id, OrderRank = rank, Aura = rank >= 4 ? 3 + ORng.Next(2) : 1 + ORng.Next(2),
        };
        var virtues = new List<string> { "Honourable", "Pious", "Loyal", "Reckless" }; // none clash with another (TraitClashes)
        for (int i = 0; i < 2; i++) { var t = ORng.Pick(virtues); ch.Traits.Add(t); virtues.Remove(t); }
        if (S.CaptainChar >= 0 && Captain.Contract >= 0 && faction == Captain.Contract) ch.Opinion[S.CaptainChar] = 15; // as a lord of your liege
        S.Characters.Add(ch);
        return ch;
    }

    /// A knight-commander rides out of the chapter with the order's brothers and the patron's men.
    Party? SpawnOrderArmy(OrderState o)
    {
        var def = KnightOrders.Get(o.Id);
        var home = SettlementById(o.Chapter);
        if (home == null) return null;
        var p = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Lord, Faction = o.Patron, HomeId = home.Id, KnightOrder = o.Id, Gold = 400,
            Pos = home.Pos + new Vector2(ORng.Range(-4, 4), ORng.Range(-4, 4)),
        };
        p.Goal = p.Pos;
        var knight = MakeKnight(def, o.Patron, 2);
        knight.PartyId = p.Id;
        p.CharId = knight.Id;
        p.Name = knight.Name;
        o.Commanders.Add(knight.Id);
        int tier = Math.Clamp(3 + ORng.Next(2) + Day / 40, 3, 7);
        // Their own brothers (40%), then the patron's line (60%): two or three companies of foot, bows and spears.
        string sig = def.Troops[Math.Clamp(tier - 4, 0, 2)];
        string sigRole = Rules.TroopRole(sig);
        int sigMen = Rules.CapOf(sig, sigRole);
        p.Companies.Add(NewCompany(sig, sigRole, sigMen, 0, home));
        int lineMen = (int)(sigMen * (1 - SignatureShare) / SignatureShare);
        var roles = new[] { "infantry", "archers", "spears" };
        foreach (var role in roles)
        {
            if (lineMen <= 0) break;
            string troop = Armies.LineTroop(role, Math.Clamp(tier - 1, 1, 7), o.Patron);
            int n = Math.Min(lineMen, Rules.CapOf(troop, role));
            p.Companies.Add(NewCompany(troop, role, n, 0, home));
            lineMen -= n;
        }
        p.Tier = Math.Clamp((int)MathF.Round((float)p.Companies.Average(c => c.Type.Tier)), 1, 7);
        S.Parties.Add(p);
        return p;
    }

    /// The aura of the man leading this party: an order knight's (lords have none, docs/KNIGHT_ORDERS.md 11).
    public int ChampionAura(Party p) => CharacterOf(p) is { Kind: "knight", Dead: false } k ? k.Aura : 0;

    /// The forms he fights with: his order's within his tier, then the generic ones.
    public string[] ChampionFormsOf(Party p)
    {
        if (CharacterOf(p) is not { Kind: "knight" } k || k.Aura <= 0) return Array.Empty<string>();
        return Forms.OfOrder(k.Order).Concat(Forms.Generic).Where(f => f.Tier <= k.Aura).Select(f => f.Id).ToArray();
    }

    /// A beaten order army refits at its chapter: the brothers' company is raised again if it was cut down.
    void RefitOrder(Party p, Settlement home)
    {
        if (OrderOf(p) is not OrderDef def) return;
        if (p.Companies.Any(c => c.Type.Order == def.Id)) return;
        string sig = def.Troops[0];
        string role = Rules.TroopRole(sig);
        p.Companies.Insert(0, NewCompany(sig, role, Rules.CapOf(sig, role) / 2, 0, home));
    }

    /// May this party train men into an order's troop? The order's own armies may, and so may the realm whose line it is
    /// (the Ashen Sun's); you, once sworn and risen (K4).
    bool TrainsOrder(Party p, UnitType t, out string reason)
    {
        reason = "";
        if (p.KnightOrder == t.Order) return true;
        if (p == Player && t.Order == "hollow") { reason = Captain.Pact ? "" : "ONLY THE HOLLOW CROWN TRAIN THESE"; return Captain.Pact; } // K7
        int realm = p == Player ? Captain.Contract : p.Faction;
        if (realm >= 0 && Factions.Get(realm).Lines.Values.Any(l => l.Contains(t.Id))) return true;
        if (p == Player && SwornTo(t.Order, t.Tier - 3)) return true; // Knight-Brother: tier IV, Captain: V, Commander: VI
        reason = $"ONLY {KnightOrders.Find(t.Order)?.Short.ToUpperInvariant() ?? "ITS ORDER"} TRAIN THESE";
        return false;
    }

    /// Each day: an order whose patron fell goes to whoever holds its chapter; a lost chapter moves to a patron town; fallen
    /// commanders' armies are raised again after a while.
    void OrdersDaily()
    {
        ChapterTraining();
        OathAndWar();
        OrderPoliticsDaily();
        foreach (var o in S.Orders.Where(o => !o.Gone))
        {
            var chapter = SettlementById(o.Chapter);
            if (!Alive(o.Patron))
            {
                int taker = chapter?.Faction ?? -1;
                if (taker < 0 || !Alive(taker)) { o.Gone = true; Log($"{KnightOrders.Get(o.Id).Name} has no realm left to serve, and is gone."); continue; }
                o.Patron = taker;
                foreach (var p in OrderParties(o.Id)) p.Faction = taker;
                Log($"{KnightOrders.Get(o.Id).Name} kneels to {FactionNames[taker]}, who hold its chapter.");
            }
            else if (chapter == null || chapter.Faction != o.Patron)
            {
                o.Chapter = ChapterIn(o.Patron);
                foreach (var p in OrderParties(o.Id)) p.HomeId = o.Chapter;
            }
            o.Commanders.RemoveAll(id => CharacterById(id) is not Character k || k.Dead || !S.Parties.Any(p => p.CharId == id && p.KnightOrder == o.Id));
            int armies = OrderParties(o.Id).Count();
            if (armies < OrderArmies && S.Hours >= o.NextArmy)
            {
                if (o.NextArmy > 0) OnOrderDice(() => SpawnOrderArmy(o));
                o.NextArmy = (float)S.Hours + OrderRespawnDays * 24;
            }
        }
    }

    /// An order army's lord falls: a brother knight takes up the banner (not a lord's heir).
    Character KnightHeir(Party p)
    {
        var def = KnightOrders.Get(p.KnightOrder);
        var k = MakeKnight(def, p.Faction, 2);
        k.PartyId = p.Id;
        p.CharId = k.Id;
        p.Name = k.Name;
        OrderById(def.Id)?.Commanders.Add(k.Id);
        return k;
    }
}
