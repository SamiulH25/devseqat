namespace Ironfield;

/// How well a piece is made (docs/COMPANY.md C9): each step scales the piece's own numbers a little and raises its price.
/// Named is one of a kind: a piece that earned its name, or came so from a famous hand.
public enum ItemQuality { Worn, Plain, Fine, Masterwork, Named }

/// A piece of gear the captain owns (docs/COMPANY.md C9): what it is (`Def`, a Data/gear.json id), how well it is made,
/// the smith's marks it carries (effect ids from Data/effects.json tagged "mark"), the name it earned, where it came from,
/// and what it has done.
public sealed class GearItem
{
    public string Def = "", Name = "", From = "";
    public ItemQuality Quality = ItemQuality.Plain;
    public List<string> Marks = new();
    public int Kills, Battles;
}

/// The captain's pieces as things: quality, marks, the baggage they ride in and the story each one gathers.
public sealed partial class Campaign
{
    /// Kills a weapon needs before you may give it a name.
    public const int NameAtKills = 50;
    /// Most marks a piece may carry.
    public const int MostMarks = 2;

    /// How much a quality scales a piece's own numbers (damage, block, armour, a horse's health).
    public static float QualityScale(ItemQuality q) => q switch
    {
        ItemQuality.Worn => 0.9f, ItemQuality.Fine => 1.06f, ItemQuality.Masterwork => 1.12f, ItemQuality.Named => 1.15f, _ => 1,
    };

    /// What a quality does to a piece's price.
    public static float QualityPrice(ItemQuality q) => q switch
    {
        ItemQuality.Worn => 0.6f, ItemQuality.Fine => 1.6f, ItemQuality.Masterwork => 2.5f, ItemQuality.Named => 4, _ => 1,
    };

    public static string QualityName(ItemQuality q) => q switch
    {
        ItemQuality.Worn => "worn", ItemQuality.Fine => "fine", ItemQuality.Masterwork => "masterwork", ItemQuality.Named => "named", _ => "",
    };

    /// The price of a piece of this make with these marks: each mark adds a fifth of the plain price.
    public static int PriceOf(GearDef g, ItemQuality q, int marks = 0) => (int)(g.Cost * (QualityPrice(q) + 0.2f * marks));

    /// The marks a piece of this slot may carry.
    public static IEnumerable<EffectDef> MarksFor(string slot) => Effects.All.Where(e => e.Tags.Contains("mark") && e.Tags.Contains(slot));

    /// The piece of this gear the captain holds, or null (free starting gear needs none: it is plain).
    public static GearItem? ItemOf(CaptainState c, string def) => def == "" ? null : c.Baggage.FirstOrDefault(i => i.Def == def);

    /// A piece's name for the screens: its own name, else its make and kind ("fine arming sword").
    public static string ItemName(GearItem? i, GearDef g)
    {
        if (i == null) return g.Name;
        if (i.Name != "") return i.Name;
        string q = QualityName(i.Quality);
        return q == "" ? g.Name : $"{char.ToUpperInvariant(q[0])}{q[1..]} {g.Name.ToLowerInvariant()}";
    }

    /// The worn pieces as items (free plain pieces have none).
    public static IEnumerable<GearItem> WornItems(CaptainState c) => c.Kit.Worn.Select(id => ItemOf(c, id)).OfType<GearItem>();

    /// The marks of what the captain wears into battle; the weapon's only while it is in hand.
    public static Mods MarkMods(CaptainState c, bool side)
    {
        var k = c.Kit;
        var m = new Mods();
        string hand = side && k.Side != "" ? k.Side : k.Weapon;
        foreach (var id in new[] { hand, k.Shield, k.Body, k.Helm, k.Horse })
            if (ItemOf(c, id) is GearItem i)
                foreach (var mark in i.Marks) if (Effects.Has(mark)) m.Add(Effects.Get(mark));
        return m;
    }

    // ---------- the inventory (C10): kit changes anywhere outside a battle ----------

    /// The kit's slots in the order the inventory shows them.
    public static readonly string[] KitSlots = { "weapon", "side", "shield", "body", "helm", "horse" };

    /// The gear id worn in a kit slot ("" for no second weapon).
    public static string InSlot(CaptainKit k, string slot) => slot switch
    {
        "weapon" => k.Weapon, "side" => k.Side, "shield" => k.Shield, "body" => k.Body, "helm" => k.Helm, _ => k.Horse,
    };

    /// Take off what a slot holds: the second weapon goes back in the baggage, the rest give way to the plain starting piece.
    /// The main hand always holds something.
    public string? TakeOff(string slot)
    {
        var k = Captain.Kit;
        if (slot == "side") { if (k.Side == "") return "NO SECOND WEAPON"; k.Side = ""; return null; }
        if (slot == "weapon") return "YOUR HAND HOLDS A WEAPON: WEAR ANOTHER";
        var free = Gear.InSlot(slot).First(g => g.Free).Id;
        if (InSlot(k, slot) == free) return "NOTHING TO TAKE OFF";
        return Equip(free);
    }

    /// Leave a piece behind for good: not one you wear, nor a named one; free starting pieces cannot be lost.
    public string? DropItem(string def)
    {
        if (Gear.Find(def) is not GearDef g) return "NO SUCH GEAR";
        if (Wears(def)) return "TAKE IT OFF FIRST";
        if (ItemOf(Captain, def) is not GearItem i) return g.Free ? "YOU KEEP YOUR OLD KIT" : "NOT YOURS";
        if (i.Name != "") return "A NAMED PIECE IS NOT LEFT BEHIND";
        Captain.Baggage.Remove(i);
        Log($"You leave the {ItemName(i, g).ToLowerInvariant()} behind.");
        return null;
    }

    /// Everything the captain can put on: the pieces in the baggage and the free starting kit.
    public IEnumerable<GearDef> Wardrobe() => Gear.All.Where(g => Owns(g.Id)).OrderBy(g => Array.IndexOf(Gear.Slots, g.Slot)).ThenBy(g => g.Cost);

    // ---------- baggage ----------

    /// What the baggage can carry: a base, ten more for each spare horse you keep (to six), and a fifth more with the Quartermaster.
    public int BaggageRoom() => (int)((B.BaggageBase + B.BaggagePerHorse * Math.Min(6, Player.Horses)) * (Has("Quartermaster") ? 1.2f : 1));

    /// What it carries now: each piece of gear not worn (armour 2, the rest 1; a horse walks itself) and each ten lots of goods.
    public int BaggageLoad()
    {
        var worn = Captain.Kit.Worn.ToHashSet();
        int gear = Captain.Baggage.Where(i => !worn.Contains(i.Def) && Gear.Find(i.Def) is GearDef g && g.Slot != "horse")
            .Sum(i => Gear.Get(i.Def).Slot == "body" ? 2 : 1);
        return gear + (S.Goods.Values.Sum() + 9) / 10;
    }

    /// The march over the baggage's room: 2 % slower for each piece over, up to a quarter. Nothing is thrown away.
    public float BaggageDrag()
    {
        int over = BaggageLoad() - BaggageRoom();
        return over <= 0 ? 1 : 1 - MathF.Min(0.25f, over * 0.02f);
    }

    // ---------- a piece's story ----------

    /// After a battle you fought in: each worn piece counts the battle; the weapon in your main hand takes your kills.
    void ItemsAfterBattle(BattleResult r)
    {
        foreach (var i in WornItems(Captain)) i.Battles++;
        if (ItemOf(Captain, Captain.Kit.Weapon) is GearItem w)
        {
            bool could = w.Kills >= NameAtKills;
            w.Kills += r.PlayerKills;
            if (!could && w.Kills >= NameAtKills && w.Name == "")
                Log($"Your {Gear.Get(w.Def).Name.ToLowerInvariant()} has taken {w.Kills} lives. It has earned a name: give it one at the armourer.");
        }
    }

    /// Names a blade may take (offered in turn at the armourer; the screen has no keyboard entry).
    public static readonly string[] BladeNames =
    {
        "Wolfsbane", "Greyfang", "Oathkeeper", "Widowmaker", "Ashtongue", "Ravenshade", "Lastword", "Hearthguard", "Thornwake",
        "Morrow", "Stormcall", "Blackbriar", "Kingsbane", "Mercy", "Old Faithful", "Gravesong", "Brightwater", "Ironvow",
    };

    /// Whether this piece may be named now (a weapon of fifty kills, not named yet).
    public static bool MayName(GearItem i) => i.Name == "" && i.Kills >= NameAtKills && Gear.Find(i.Def)?.Slot == "weapon";

    /// Give a weapon that earned it a name: it becomes one of a kind.
    public string? NameItem(GearItem i, string name)
    {
        name = name.Trim();
        if (!MayName(i)) return "IT HAS NOT EARNED A NAME";
        if (name.Length is < 2 or > 24) return "A NAME OF 2 TO 24 LETTERS";
        i.Name = name;
        i.Quality = ItemQuality.Named;
        Log($"You name your {Gear.Get(i.Def).Name.ToLowerInvariant()} {name}.");
        Deed($"named the blade {name}");
        return null;
    }
}
