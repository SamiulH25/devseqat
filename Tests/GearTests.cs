namespace Ironfield;

/// The captain's gear as things (docs/archive/COMPANY.md C9-C11).
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
        float Arm(ItemQuality q) { cap.Baggage.RemoveAll(i => i.Def == "gambeson"); if (q != ItemQuality.Plain) cap.Baggage.Add(new GearItem { Def = "gambeson", Quality = q }); return Rules.CaptainType(cap).Armor; }
        Check(Arm(ItemQuality.Worn) < Arm(ItemQuality.Plain) && Arm(ItemQuality.Plain) < Arm(ItemQuality.Masterwork), "make scales body armour upward too");
        Check(Gear.Get("claymore").TwoHanded && Gear.Get("varangian_axe").TwoHanded, "two-handed weapons require two hands");
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

    /// C11: spoils come better made from greater foes; falling batters a piece (it fights a make lower) and a smith mends it;
    /// the smith raises a make for crowns and goods, refuses a horse, masterwork outside a great town and an unfit mark.
    static void GearSources()
    {
        var c = Campaign.New(27);
        float Mean(bool lord, bool great) => (float)Enumerable.Range(0, 400).Average(_ => (int)c.SpoilMake(lord, great, true));
        float brigand = Mean(false, false), lord = Mean(true, false), champion = Mean(true, true);
        Check(brigand < lord && lord < champion, $"spoils come better made from greater foes ({brigand:0.00} < {lord:0.00} < {champion:0.00})");
        Check(Enumerable.Range(0, 200).All(_ => c.SpoilMake(true, true, false) >= ItemQuality.Fine), "a champion's gear is fine or better");

        // Battered: a fine sword battered fights as a plain one, until mended.
        var cap = c.Captain;
        var sword = Campaign.Own(cap, "arming_sword", ItemQuality.Fine)!;
        float fine = Rules.CaptainType(cap).Damage;
        sword.Battered = true;
        float battered = Rules.CaptainType(cap).Damage;
        cap.Baggage.Remove(sword);
        float plain = Rules.CaptainType(cap).Damage;
        cap.Baggage.Add(sword);
        Check(MathF.Abs(battered - plain) < 1e-3f && battered < fine, $"a battered piece fights one make lower ({battered:0.0} against {fine:0.0})");

        // The smith: only where there is one.
        c.Player.Pos = new Microsoft.Xna.Framework.Vector2(-9999, -9999);
        Check(c.Mend(sword) == "NO SMITH HERE", "no smith out on the road");
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
        c.Player.Pos = town.Pos;
        c.S.Gold = 5000;
        Check(c.Improve(sword) == "MEND IT FIRST", "a battered piece is mended before it is bettered");
        float gold = c.S.Gold;
        Check(c.Mend(sword) == null && !sword.Battered && c.S.Gold == gold - Campaign.MendCost(Gear.Get("arming_sword")), "the smith mends it for a tenth of its price");
        // Improving: goods lots are needed; a great town's smith for masterwork.
        c.S.Goods.Clear();
        Check(c.Improve(sword)!.Contains("LOTS OF ARMS"), "raising a make needs lots of arms");
        c.S.Goods["arms"] = 10;
        town.Prosperity = 30; town.Buildings.Remove("market");
        Check(c.Improve(sword) == "MASTERWORK NEEDS A GREAT TOWN'S SMITH", "masterwork needs a great town's smith");
        town.Prosperity = 80;
        var (crowns, _, lots) = Campaign.ImproveCost(Gear.Get("arming_sword"), ItemQuality.Fine);
        gold = c.S.Gold;
        Check(c.Improve(sword) == null && sword.Quality == ItemQuality.Masterwork && c.S.Gold == gold - crowns && c.S.Goods["arms"] == 10 - lots, "a great town's smith makes it masterwork for crowns and lots");
        Check(c.Improve(sword) == "NO SMITH CAN BETTER IT", "nothing betters a masterwork");
        // Marks: one that fits, struck anew; a horse cannot be forged.
        Check(c.Remark(sword, "sure_seated") == "THAT MARK DOES NOT FIT IT", "a horse's mark does not fit a sword");
        Check(c.Remark(sword, "keen") == null && c.Remark(sword, "balanced") == null && sword.Marks.Count == 2, "a masterwork takes two marks");
        Check(c.Remark(sword, "keen") == "IT BEARS THAT MARK", "not the same mark twice");
        var horse = c.SmithItem(cap.Kit.Horse);
        Check(c.CannotImprove(horse) == "A HORSE IS BRED, NOT FORGED", "a horse is bred, not forged");
        // A free starting piece improved becomes a thing in the baggage.
        var helm = c.SmithItem(cap.Kit.Helm);
        c.S.Goods["armour"] = 5;
        Check(c.Improve(helm) == null && Campaign.ItemOf(cap, cap.Kit.Helm) is { Quality: ItemQuality.Fine }, "a plain starting helm made fine joins the baggage");
    }

    /// KT6 (See it): every soldier's kit lists piece by piece with effects, skills format cleanly, and promotions report kit changes.
    static void TroopKitDisplay()
    {
        var sh = Troops.Get("shieldmen");
        var maa = Troops.Get("men_at_arms");
        var kit = Fighter.KitList(sh);
        Check(kit.Count >= 4 && kit.Any(x => x.Slot == "WEAPON" && x.Effect.Contains("DMG"))
            && kit.Any(x => x.Slot == "SHIELD" && x.Effect.Contains("BLK"))
            && kit.Any(x => x.Slot == "BODY" && x.Effect.Contains("ARM")), "shieldmen list weapon, shield and armour pieces with effects");
        Check(Fighter.KitLine(sh).Contains("ARMING SWORD") && Fighter.KitLine(sh).Contains("GAMBESON"), "hover kit line names the worn pieces");
        Check(Fighter.SkillsLine(maa).Contains("ARMS 40") && Fighter.SkillsLine(maa).Contains("RIDING 0") && Fighter.SkillsLine(maa).Contains("BOW 0"), "skills line shows all three skills");
        var changes = Fighter.KitChanges(sh, maa);
        Check(changes.Count > 0 && changes.Any(x => x.Text.Contains("ARM +") && x.Sign > 0), "promotion from shieldmen to men-at-arms lists armour gains");
        var troll = Troops.Get("troll");
        Check(Fighter.KitList(troll).Count == 0 && Fighter.KitLine(troll) == "" && Fighter.SkillsLine(troll) == "", "wild creatures have no kit or skills lines");
    }

    /// Company storage: duplicate item instances, baggage wagons, road goods jettisoning, keep vault, and baked item sprites.
    static void CompanyStorage()
    {
        var c = Campaign.New(28);
        c.S.Gold = 5000;
        c.Captain.Skills["Arms"] = 40;
        // Duplicate item instances: own two arming swords of different make/marks, equip one, sell the other.
        var wornBlade = c.AddItem("arming_sword", ItemQuality.Worn, "", "taken from a brigand");
        var fineBlade = c.AddItem("arming_sword", ItemQuality.Fine, "keen", "forged in a town");
        Check(wornBlade.Id > 0 && fineBlade.Id > 0 && wornBlade.Id != fineBlade.Id, "each item instance gets a unique id");
        Check(c.EquipItem(fineBlade) == null && Campaign.ItemInSlot(c.Captain, "weapon") == fineBlade, "equipping a specific instance binds that exact item");
        Check(Campaign.IsWorn(c.Captain, fineBlade) && !Campaign.IsWorn(c.Captain, wornBlade), "only the equipped copy counts as worn");
        Check(Campaign.MarkMods(c.Captain, false).Damage > 0, "the equipped copy's mark applies");
        Check(c.SellGearItem(fineBlade) == "TAKE IT OFF FIRST", "cannot sell the worn copy");
        Check(c.SellGearItem(wornBlade) == null && !c.Captain.Baggage.Contains(wornBlade) && c.Captain.Baggage.Contains(fineBlade), "can sell the unworn duplicate while wearing the other");

        // Baggage wagons & load breakdown.
        var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && !c.Refuses(s));
        int baseRoom = c.BaggageRoom();
        Check(c.BuyWagon(town) == null && c.Player.Wagons == 1 && c.BaggageRoom() > baseRoom, "buying a wagon expands baggage room");
        c.S.Goods["arms"] = 25;
        var bd = c.BaggageLoadBreakdown();
        Check(bd.Goods == 3 && bd.Room == c.BaggageRoom(), "load breakdown counts goods lots cleanly");
        Check(c.DropGoods("arms", 10) == null && c.S.Goods["arms"] == 15, "excess goods can be dropped on the road");
        Check(c.SellWagon(town) == null && c.Player.Wagons == 0, "wagons can be sold back in town");

        // Keep vault at an owned settlement.
        var castle = c.S.Settlements.First(s => s.Kind == SettlementKind.Castle);
        var spare = c.AddItem("boar_spear", ItemQuality.Masterwork, "balanced", "gift");
        Check(c.StoreItem(castle, spare) != null, "cannot store in a castle you do not own");
        castle.Owner = c.Player.Id;
        c.Player.Pos = castle.Pos;
        Check(c.VaultAt() == castle, "standing at your own castle opens its vault");
        Check(c.StoreItem(castle, fineBlade) == "TAKE IT OFF FIRST", "cannot store a worn item");
        Check(c.StoreItem(castle, spare) == null && castle.Vault.Contains(spare) && !c.Captain.Baggage.Contains(spare), "spare gear moves into the keep vault");
        Check(c.StoreGoods(castle, "arms", 10) == null && castle.VaultGoods["arms"] == 10 && c.S.Goods["arms"] == 5, "goods move into the keep vault");
        // Survives save round-trip.
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        var backCastle = back.S.Settlements.First(s => s.Id == castle.Id);
        Check(backCastle.Vault.Count == 1 && backCastle.Vault[0].Def == "boar_spear" && backCastle.VaultGoods["arms"] == 10, "vault gear and goods survive a save");
        Check(back.TakeStoredItem(backCastle, backCastle.Vault[0]) == null && back.Captain.Baggage.Any(i => i.Def == "boar_spear"), "taking an item returns it to baggage");
        Check(back.TakeStoredGoods(backCastle, "arms", 10) == null && back.S.Goods["arms"] == 15, "taking goods returns them to baggage");

        // Baked item sprites exist in the atlas for every goods kind and store icon.
        var sheet = new SpriteSheet();
        foreach (var k in Campaign.GoodsKinds) Check(sheet.Rects.ContainsKey($"goods.{k.Id}/n"), $"goods sprite goods.{k.Id}/n exists");
        foreach (var k in new[] { "food", "horse", "wagon", "vault" }) Check(sheet.Rects.ContainsKey($"store.{k}/n"), $"store sprite store.{k}/n exists");
    }
}
