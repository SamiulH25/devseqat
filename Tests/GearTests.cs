namespace Ironfield;

/// The captain's gear as things (docs/COMPANY.md C9-C11).
public static partial class SelfTest
{
    /// C9: an old save's owned ids become plain pieces; quality scales a piece's numbers and price; a smith's mark reaches the
    /// captain's Mods in battle (the weapon's only while in hand); an overloaded baggage slows the march; a weapon counts its
    /// kills and earns a name; and pieces survive a save.
    static void GearItems()
    {
        // Migration: Owned ids (before C9) become plain items, the list is emptied, unknown ids are dropped.
        var c = Campaign.New(23);
        var st = c.S;
        st.Captain.Kit.Owned.AddRange(new[] { "boar_spear", "plate", "no_such_piece" });
        st.Captain.Baggage.Clear();
        var moved = new Campaign(st);
        Check(moved.Owns("boar_spear") && moved.Owns("plate") && st.Captain.Kit.Owned.Count == 0, "an old save's owned gear becomes pieces in the baggage");
        Check(st.Captain.Baggage.All(i => i.Quality == ItemQuality.Plain && Gear.Find(i.Def) != null), "as plain pieces, and nothing unknown");

        // Quality: a fine sword hits harder than a plain one, a worn one softer, and costs more.
        c = Campaign.New(23);
        var cap = c.Captain;
        float Hit(ItemQuality q)
        {
            cap.Baggage.RemoveAll(i => i.Def == "arming_sword");
            if (q != ItemQuality.Plain) cap.Baggage.Add(new GearItem { Def = "arming_sword", Quality = q });
            return Rules.CaptainType(cap).Damage;
        }
        float worn = Hit(ItemQuality.Worn), plain = Hit(ItemQuality.Plain), fine = Hit(ItemQuality.Fine), master = Hit(ItemQuality.Masterwork);
        Check(worn < plain && plain < fine && fine < master, $"make scales the blow ({worn:0.0} < {plain:0.0} < {fine:0.0} < {master:0.0})");
        var spear = Gear.Get("boar_spear");
        Check(Campaign.PriceOf(spear, ItemQuality.Fine) > spear.Cost && Campaign.PriceOf(spear, ItemQuality.Fine, 1) > Campaign.PriceOf(spear, ItemQuality.Fine), "a finer piece and a mark cost more");
        c.S.Gold = 5000;
        Check(c.BuyGear(spear, ItemQuality.Fine, "keen") == null && Campaign.ItemOf(cap, "boar_spear") is { Quality: ItemQuality.Fine } bought && bought.Marks.Contains("keen") && bought.From.StartsWith("bought"),
            "a fine piece bought carries its mark and where it came from");
        Check(c.SellGear(Gear.Get("boar_spear")) == "TAKE IT OFF FIRST", "worn pieces are not sold");
        Check(Campaign.MarksFor("weapon").Any() && Campaign.MarksFor("horse").Any() && !Campaign.MarksFor("weapon").Any(e => e.Id == "sure_seated"), "marks fit their slots");

        // A mark reaches the battle: in hand, the keen spear's edge is on the captain; swap to the side weapon and it is not.
        c.Equip("arming_sword"); c.Equip("boar_spear", side: true);
        var o = new BattleOptions().WithCaptain(cap);
        var b = Battle.Empty(31, o);
        Check(b.Player.Mods.Damage == 0, "the sword in hand has no mark");
        c.Equip("boar_spear");
        b = Battle.Empty(31, new BattleOptions().WithCaptain(cap));
        Check(MathF.Abs(b.Player.Mods.Damage - Effects.Get("keen").Damage) < 1e-4f, "the keen spear in hand: its edge rides on the captain's Mods");

        // Baggage: over the room, the march slows; nothing is thrown away.
        c = Campaign.New(24);
        float free = c.Speed(c.Player);
        c.S.Goods["arms"] = (c.BaggageRoom() + 5) * 10;
        Check(c.BaggageLoad() > c.BaggageRoom() && c.Speed(c.Player) < free * 0.95f, $"an overloaded baggage slows the march ({c.Speed(c.Player):0.00} against {free:0.00})");
        Check(c.S.Goods["arms"] == (c.BaggageRoom() + 5) * 10, "and nothing is thrown away");
        c.Player.Horses += 6;
        Check(c.BaggageRoom() > Balance.Get().BaggageBase, "spare horses carry more");

        // A piece's story: the weapon in hand counts kills; fifty earn it a name; the name and make survive a save.
        c = Campaign.New(25);
        cap = c.Captain;
        var sword = Campaign.Own(cap, "arming_sword", ItemQuality.Fine, "keen")!;
        Check(c.NameItem(sword, "Wolfsbane") != null, "an untried blade has no name to earn");
        sword.Kills = Campaign.NameAtKills;
        Check(c.NameItem(sword, "Wolfsbane") == null && sword.Quality == ItemQuality.Named && Campaign.ItemName(sword, Gear.Get("arming_sword")) == "Wolfsbane", "fifty kills and it takes the name you give it");
        var back = CampaignState.Deserialize(c.S.Serialize());
        var again = Campaign.ItemOf(back.Captain, "arming_sword");
        Check(again is { Name: "Wolfsbane", Quality: ItemQuality.Named, Kills: Campaign.NameAtKills } && again.Marks.SequenceEqual(new[] { "keen" }), "pieces survive a save round trip");
    }

    /// C10: the kit changes anywhere: wear, carry second, take off (refused for the main hand), leave behind (refused when worn,
    /// named or free); every refusal changes nothing.
    static void InventoryActions()
    {
        var c = Campaign.New(26);
        var cap = c.Captain;
        var k = cap.Kit;
        Check(c.Wardrobe().All(g => c.Owns(g.Id)) && c.Wardrobe().Any(g => g.Id == "arming_sword"), "the wardrobe is what you own, the free kit with it");
        Check(c.TakeOff("weapon") != null && k.Weapon == "arming_sword", "the main hand always holds a weapon");
        Check(c.TakeOff("side") == null && k.Side == "" && c.TakeOff("side") != null, "the second weapon comes off, once");
        string free = Gear.InSlot("helm").First(g => g.Free).Id;
        Campaign.Own(cap, "great_helm");
        cap.Skills["Arms"] = 60;
        Check(c.Equip("great_helm") == null && k.Helm == "great_helm", "a piece in the baggage goes on away from any market");
        Check(c.DropItem("great_helm") == "TAKE IT OFF FIRST" && c.Owns("great_helm"), "a worn piece is not left behind");
        Check(c.TakeOff("helm") == null && k.Helm == free, "taking off the helm puts the plain one back on");
        Check(c.TakeOff("helm") == "NOTHING TO TAKE OFF", "nothing more to take off");
        Check(c.DropItem("great_helm") == null && !c.Owns("great_helm"), "a piece not worn can be left behind");
        Check(c.DropItem("arming_sword") != null && c.Owns("arming_sword"), "the free kit is never lost");
        var named = Campaign.Own(cap, "boar_spear")!;
        named.Kills = Campaign.NameAtKills;
        c.NameItem(named, "Greyfang");
        Check(c.DropItem("boar_spear") == "A NAMED PIECE IS NOT LEFT BEHIND", "a named piece is never left behind");
        Check(c.Equip("boar_spear", side: true) == null && k.Side == "boar_spear", "carry it second from the baggage");
    }
}
