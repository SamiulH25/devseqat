namespace Ironfield;

/// The captain's armoury (docs/archive/PROGRESSION.md P1): owning, buying, selling and wearing gear from Data/gear.json.
public sealed partial class Campaign
{
    /// What the armourer pays back for a piece you sell.
    public const float SellBack = 0.4f;

    /// A save from before the armoury: its plate, helm and horse tiers become the matching gear, and every worn piece is owned.
    internal void MigrateGear()
    {
        var c = Captain;
        var k = c.Kit;
        if (c.ArmorTier > 0) k.Body = "plate";
        if (c.HelmTier > 0) k.Helm = "great_helm";
        if (c.HorseTier >= 1) { k.Horse = "warhorse"; Own("warhorse"); }
        if (c.HorseTier >= 2) k.Horse = "destrier";
        c.ArmorTier = c.HelmTier = c.HorseTier = 0;
        foreach (var id in k.Worn.ToList())
            if (Gear.Find(id) == null) Unequip(id);
            else Own(id);
        // Before pieces were things (C9): each owned id becomes a plain piece.
        foreach (var id in k.Owned) if (Gear.Find(id) != null) Own(id);
        k.Owned.Clear();
        c.Baggage.RemoveAll(i => Gear.Find(i.Def) == null);
    }

    /// The road the captain came by (P5, the Free Company's reward), chosen once at the start: the sword (Arms 20 and a
    /// kite shield), the lance (Riding 20, a warhorse, a boar spear as the second weapon) or the bow (Bowcraft 20, a
    /// hunting bow in hand and the sword at the side). Returns the line the event shows.
    public string ChooseRoad(string road) => TakeRoad(Captain, road);

    /// The road on any captain: the event above, and the character creator's preview (Origins.Shape).
    public static string TakeRoad(CaptainState c, string road)
    {
        var k = c.Kit;
        switch (road)
        {
            case "lance":
                c.Skills["Riding"] = MathF.Max(Skills.Level(c, "Riding"), 20);
                Own(c, "warhorse"); Own(c, "boar_spear");
                (k.Horse, k.Side) = ("warhorse", "boar_spear");
                return "You ride a warhorse and know how to couch a lance.";
            case "bow":
                c.Skills["Bowcraft"] = MathF.Max(Skills.Level(c, "Bowcraft"), 20);
                Own(c, "hunting_bow");
                (k.Weapon, k.Side) = ("hunting_bow", "arming_sword");
                return "You carry a hunting bow and the eye to use it.";
            case "great":
                c.Skills["Arms"] = MathF.Max(Skills.Level(c, "Arms"), 20);
                (k.Weapon, k.Side) = ("greatsword", "arming_sword");
                return "You fight with the greatsword in both hands, as you always have.";
            case "pole":
                c.Skills["Arms"] = MathF.Max(Skills.Level(c, "Arms"), 15);
                c.Skills["Tactics"] = MathF.Max(Skills.Level(c, "Tactics"), 15);
                Own(c, "halberd");
                (k.Weapon, k.Side) = ("halberd", "arming_sword");
                return "You carry a halberd, and you learned your trade in the line.";
            default:
                c.Skills["Arms"] = MathF.Max(Skills.Level(c, "Arms"), 20);
                Own(c, "kite_shield");
                k.Shield = "kite_shield";
                return "You fight on foot behind a kite shield, as you always have.";
        }
    }

    /// Offer the starting road as the first event of a new campaign (the title screen calls it).
    public void OfferRoad() => Force("start_road", new PendingEvent());

    GearItem? Own(string id, ItemQuality q = ItemQuality.Plain, string mark = "", string from = "") => Own(Captain, id, q, mark, from);
    /// Take a piece into the baggage (one of each kind; a better make of one you hold replaces it). Free plain pieces need no item.
    internal static GearItem? Own(CaptainState c, string id, ItemQuality q = ItemQuality.Plain, string mark = "", string from = "")
    {
        var g = Gear.Get(id);
        if (g.Free && q == ItemQuality.Plain && mark == "") return null;
        if (ItemOf(c, id) is GearItem had)
        {
            if (had.Quality >= q || had.Name != "") return had;
            c.Baggage.Remove(had);
        }
        var i = new GearItem { Def = id, Quality = q, From = from };
        if (mark != "") i.Marks.Add(mark);
        c.Baggage.Add(i);
        return i;
    }

    /// A worn piece the data no longer has goes back to the free piece of its slot.
    void Unequip(string id)
    {
        var k = Captain.Kit;
        if (k.Side == id) { k.Side = ""; return; }
        string Free(string slot) => Gear.InSlot(slot).First(g => g.Free).Id;
        if (k.Weapon == id) k.Weapon = Free("weapon");
        if (k.Shield == id) k.Shield = Free("shield");
        if (k.Body == id) k.Body = Free("body");
        if (k.Helm == id) k.Helm = Free("helm");
        if (k.Horse == id) k.Horse = Free("horse");
    }

    public bool Owns(string id) => Gear.Find(id) is GearDef g && (g.Free || ItemOf(Captain, id) != null);
    public bool Wears(string id) => Captain.Kit.Worn.Contains(id);

    /// Why the captain may not use this piece (the skill it needs), or null.
    public string? CannotUse(GearDef g) => Skill(g.Skill) < g.Needs ? $"NEEDS {g.Skill.ToUpperInvariant()} {g.Needs}" : null;

    /// Why the captain may not buy it: owned (as well made), rank, skill or gold.
    public string? CannotBuy(GearDef g, ItemQuality q = ItemQuality.Plain, string mark = "")
    {
        if (Owns(g.Id) && (ItemOf(Captain, g.Id) is not GearItem had || had.Quality >= q || had.Name != "")) return "OWNED";
        if ((int)Rank < g.Rank) return $"FOR A {RankName((Rank)g.Rank)}";
        int cost = PriceOf(g, q, mark == "" ? 0 : 1);
        return CannotUse(g) ?? (S.Gold < cost ? $"NEEDS {cost} CROWNS" : null);
    }

    /// The make and mark the armourer of this place offers a piece at this week: mostly plain, some fine (and in a town a few
    /// masterworks); fine and up carry a mark. Seeded like the stock, so looking again does not reroll it.
    public (ItemQuality Quality, string Mark) StockMake(Settlement s, GearDef g)
    {
        int week = (int)(S.Hours / (24 * 7));
        uint h = (uint)HashCode.Combine(S.Seed, s.Id, week, g.Id.GetHashCode(StringComparison.Ordinal), 77);
        int roll = (int)(h % 100);
        var q = s.Kind == SettlementKind.Town && roll < 8 ? ItemQuality.Masterwork : roll < (s.Kind == SettlementKind.Village ? 10 : 30) ? ItemQuality.Fine : ItemQuality.Plain;
        var marks = MarksFor(g.Slot).ToList();
        string mark = q >= ItemQuality.Fine && marks.Count > 0 ? marks[(int)(h / 100 % (uint)marks.Count)].Id : "";
        return (q, mark);
    }

    /// What the armourer of this place has this week: most of the list in a town, less in a castle, little in a village.
    /// The same all week (seeded by place and week), so looking again does not reroll it.
    public List<GearDef> ArmourerStock(Settlement s)
    {
        int week = (int)(S.Hours / (24 * 7));
        int chance = s.Kind switch { SettlementKind.Town => 70, SettlementKind.Castle => 45, _ => 20 };
        return Gear.All.Where(g => !g.Free && (s.Kind != SettlementKind.Village || g.Cost <= 300)
            && (uint)HashCode.Combine(S.Seed, s.Id, week, g.Id.GetHashCode(StringComparison.Ordinal)) % 100 < chance).ToList();
    }

    /// Buy a piece (of a make, with a mark) and wear it at once (a weapon goes in the main hand).
    public string? BuyGear(GearDef g, ItemQuality q = ItemQuality.Plain, string mark = "")
    {
        if (CannotBuy(g, q, mark) is string why) return why;
        S.Gold -= PriceOf(g, q, mark == "" ? 0 : 1);
        var i = Own(g.Id, q, mark, $"bought in {SettlementAt(Player.Pos)?.Name ?? "a market"}, day {Day}");
        Log($"You buy a {ItemName(i, g).ToLowerInvariant()}.");
        AudioEngine.Current?.Play(Cue.AnvilRing, 0.7f);
        return Equip(g.Id);
    }

    /// Wear a piece you own; `side` puts a weapon in the second hand (the one N swaps to).
    public string? Equip(string id, bool side = false)
    {
        if (Gear.Find(id) is not GearDef g) return "NO SUCH GEAR";
        if (!Owns(id)) return "NOT YOURS";
        if (CannotUse(g) is string why) return why;
        var k = Captain.Kit;
        switch (g.Slot)
        {
            case "weapon":
                if (side)
                {
                    if (k.Weapon == id) { if (k.Side == "") return "IN YOUR MAIN HAND"; k.Weapon = k.Side; }
                    k.Side = id;
                }
                else { if (k.Side == id) k.Side = k.Weapon; k.Weapon = id; }
                break;
            case "shield": k.Shield = id; break;
            case "body": k.Body = id; break;
            case "helm": k.Helm = id; break;
            default: k.Horse = id; break;
        }
        AudioEngine.Current?.Play(g.Slot == "weapon" ? Cue.EquipBlade : g.Slot == "horse" ? Cue.HorseTack : Cue.EquipArmor, 0.7f);
        return null;
    }

    /// Carry no second weapon.
    public void ClearSide() => Captain.Kit.Side = "";

    /// Sell a piece you own and do not wear, for part of its price.
    public string? SellGear(GearDef g)
    {
        if (!Owns(g.Id) || g.Free) return "NOT FOR SALE";
        if (Wears(g.Id)) return "TAKE IT OFF FIRST";
        var i = ItemOf(Captain, g.Id);
        S.Gold += SellPrice(g, i);
        Log($"You sell your {ItemName(i, g).ToLowerInvariant()}.");
        if (i != null) Captain.Baggage.Remove(i);
        return null;
    }

    /// What the armourer pays for it: part of its price for its make and marks.
    public static int SellPrice(GearDef g, GearItem? i = null) => (int)(PriceOf(g, i?.Quality ?? ItemQuality.Plain, i?.Marks.Count ?? 0) * SellBack);

    // ---------- spoils ----------

    public const float TournamentPrizeChance = 0.5f;

    /// After a win: a piece of the beaten leader's gear, sure if you cut him down, likelier from a lord than a brigand.
    /// Monsters and caravans carry nothing you would wear. Returns a gear id or "".
    string BattleSpoil(IReadOnlyList<Party> losers, BattleResult r)
    {
        bool lord = losers.Any(p => p.Kind == PartyKind.Lord), brigand = losers.Any(p => p.Kind == PartyKind.Bandit);
        if (!lord && !brigand) return "";
        bool downed = r.EnemyLeader != LeaderFate.Unhurt;
        float chance = lord ? (downed ? 1 : 0.35f) : downed ? 0.6f : 0.15f;
        return rng.Chance(chance) ? SpoilFrom(lord ? 900 : 300) : "";
    }

    /// A piece you do not own, of no rank, up to a price.
    string SpoilFrom(int maxCost)
    {
        var pool = Gear.All.Where(g => !g.Free && g.Rank == 0 && g.Cost <= maxCost && !Owns(g.Id)).ToList();
        return pool.Count == 0 ? "" : rng.Pick(pool).Id;
    }

    /// Take the spoil the last report offers (it is yours; wear it at an armourer or from the report). Returns why not.
    public string? TakeSpoil()
    {
        if (Report is not { Spoil.Length: > 0 } rep || Gear.Find(rep.Spoil) is not GearDef g) return "NOTHING TO TAKE";
        Own(g.Id, from: $"taken from {rep.Enemy}, day {Day}");
        Report = rep with { Spoil = "" };
        Log($"You take the {g.Name.ToLowerInvariant()}. Wear it from the armourer in any market.");
        Count("spoils");
        return null;
    }
}
