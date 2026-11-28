namespace Ironfield;

/// Self-tests of the character creator's origins (Origins.cs, Campaign/Making.cs, Data/origins.json).
public static partial class SelfTest
{
    /// The data validates; the creator's preview is exactly the captain the campaign makes; a deserter starts free and
    /// out of favour; a plain campaign is untouched; an origin's events come only to captains of that origin.
    static void CharacterCreator()
    {
        Check(!Origins.Validate().Any(), "origins.json validates: " + string.Join("; ", Origins.Validate()));
        var icons = Kit.IconNames.ToHashSet();
        Check(Origins.Births.Concat(Origins.Pasts).Concat(Origins.Roads).All(o => icons.Contains(o.Icon)), "every origin has a known icon");
        Check(Origins.Pasts.All(p => Campaign.Events.Any(e => e.When.Contains("origin:" + p.Id))), "every past has its own story event");

        var m = new CaptainMaking { Name = "TESTA OF THE FORD", Company = "THE TEST BAND", Realm = 2, Birth = "guild", Past = "hedge_knight", Road = "lance", Skin = 3, Hair = 5, Beard = 2, Nose = 2, Scar = 1, Arms = (int)Emblem.Sun };
        var c = Campaign.New(77, m.Realm, m);
        var preview = Origins.Preview(m);
        Check(c.Me.Name == m.Name && c.Player.Name == m.Company && c.Me.Age == Origins.Age(m) && c.Me.Age == Origins.BaseAge + 5 + 9, "the name, the company's name and the age are the creator's");
        Check(Skills.All.All(s => Skills.Level(c.Captain, s.Id) == Skills.Level(preview, s.Id)), "the campaign's skills are the preview's");
        Check(c.Skill("Riding") == 35 && c.Skill("Stewardship") == 15, "the lance road (Riding 20) and the past (+15) add up");
        Check(c.Has("MountedLord") && c.Captain.BonusPerks.Contains("MountedLord") && Skills.Picks(c.Captain, "Riding") == 1, "the past's perk is free of the branch's marks");
        Check(c.Captain.Kit.Horse == "courser" && c.Owns("courser") && c.Captain.Kit.Side == "boar_spear", "the kit is the road's and the past's");
        Check(c.S.Gold == Balance.Get().StartGold + 450 && c.Captain.Renown == Balance.Get().StartRenown + 25, "gold and renown are the creator's sums");
        Check(c.Captain.Homeland == 2 && c.Captain.Contract == -1 && c.Captain.Birth == "guild" && c.Captain.Origin == "hedge_knight", "born in your homeland as an independent free company, and the origin is saved");
        float homeOp = c.S.Characters.Where(x => x.Kind == "lord" && !x.Dead && x.Faction == 2).Average(c.OpinionOfYou);
        float foreignOp = c.S.Characters.Where(x => x.Kind == "lord" && !x.Dead && x.Faction != 2).Average(c.OpinionOfYou);
        Check(homeOp > foreignOp + 10, $"homeland lords favour you over foreign lords ({homeOp:0.0} vs {foreignOp:0.0})");
        var homeVillage = c.S.Settlements.First(s => s.Kind == SettlementKind.Village && s.Faction == 2);
        Check(c.RecruitPriceFactor(homeVillage) < 1f, "homeland villages give a favour discount on recruits");
        var look = Rules.CaptainType(c.Captain).Look;
        Check(look.Visage == m.Visage && Variety.Parts(look.Visage) == (3, 5, 2) && look.NeutralHead, "the face you chose is the face you fight with");
        Check(look.Features == Variety.PackFeatures(m.Look), "the captain's 3D head carries every portrait feature");
        var unpacked = Variety.UnpackFeatures(look.Features);
        Check(unpacked.Cut == m.Cut && unpacked.Beard == m.Beard && unpacked.Eye == m.Eye && unpacked.Brow == m.Brow && unpacked.Nose == m.Nose && unpacked.Jaw == m.Jaw && unpacked.Scar == m.Scar, "packed face features round-trip cleanly");
        var manFace = PortraitPainter.OfMan(42, Troops.Get("brevon_fyrd"), 0, 26, false, 0, 1);
        var manStyle = Variety.Parts(Variety.Visage(Troops.Get("brevon_fyrd").Culture, PortraitPainter.SoldierSeed(42))).Style;
        var manFeat = Variety.DeriveFeatures(PortraitPainter.SoldierSeed(42), manStyle, false, 26);
        Check(manFeat.Cut == manFace.Cut && manFeat.Beard == manFace.Beard && manFeat.Eye == manFace.Eye && manFeat.Brow == manFace.Brow && manFeat.Nose == manFace.Nose && manFeat.Jaw == manFace.Jaw, "3D soldier head features match PortraitPainter.OfMan");
        Check(c.S.Event == null && c.OriginNote == "Born in the Market, once a Hedge Knight", "no road event after the creator, and the K sheet names the origin");
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        Check(back.Captain.Origin == "hedge_knight" && back.Captain.Visage == m.Visage, "origin and face survive a save");
        var face = PortraitPainter.Of(back, back.Me);
        Check(face.Beard == 2 && face.Nose == 2 && face.Scar >= 1 && face.Skin == 3 && face.Charge == Emblem.Sun, "the painted face and arms are the creator's, after a save");
        Check(c.Player.Men == 0 && c.Player.Companies.Count == 0, "creator start without special-unit origins spawns with 0 men and no levies");
        Check(c.Player.Companies.All(co => co.Banner == (int)Emblem.Sun), "your first companies fly your arms");
        // Boons: the birth's and the past's map buffs, for their days; a lasting one never runs out and a renewal never shortens it.
        Check(c.HasBuff("well_bought") && c.HasBuff("heralds_cry") && MathF.Abs(c.BuffDays("well_bought") - 60) < 0.1f && !c.Lasting("heralds_cry"), "the origin's boons are laid for their days");
        Check(Origins.Boons(m).Count() == 2 && Origins.Describe(Origins.Find("guild")!, 3).Any(l => l.StartsWith("BOON: WELL-BOUGHT HARNESS (60 DAYS)")), "the creator names the boons it lays");
        Check(back.HasBuff("well_bought"), "boons survive a save");
        Check(new CaptainMaking { Cut = 4, Beard = 1 }.Style == 3 && new CaptainMaking { Cut = 4, Beard = 0 }.Style == 0, "a bald, bearded face is the bald battle head");
        foreach (var road in Origins.Roads)
        {
            var rc = Campaign.New(79, 0, new CaptainMaking { Road = road.Id });
            Check(Origins.RoadGives(road.Id).Count >= 2 && rc.CannotUse(Gear.Get(rc.Captain.Kit.Weapon)) == null, $"the {road.Id} road gives skills and kit you can use");
        }
        // Origin events: only yours, and only after the first fortnight.
        Check(Campaign.EventOf("origin_hedge_knight")!.When.Contains("day>=14") && !c.Holds("day>=14"), "the past waits a fortnight");
        c.S.Hours = 15 * 24;
        Check(c.Holds("day>=14") && c.Holds("origin:hedge_knight") && c.Holds("origin:guild") && !c.Holds("origin:poacher"), "an origin's events come only to its captain");

        // A deserter flies their own banner, with spearmen at their back and their homeland's lords cold.
        var d = new CaptainMaking { Name = "RUNAWAY", Realm = 1, Birth = "camp", Past = "deserter", Road = "sword" };
        var dc = Campaign.New(78, d.Realm, d);
        Check(dc.Captain.Contract == -1 && dc.Me.Faction == -1, "a deserter starts as a free company");
        Check(dc.Player.Companies.Count == 1 && dc.Player.Companies[0].Stacks.Any(s => s.Troop == "shire_billmen" && s.Men == dc.Scaled(6)), "deserter spawns with only its special spearrunners and no levies");
        var noLevy = Campaign.New(78, 0, new CaptainMaking { Birth = "yeoman", Past = "drillmaster", Road = "sword" });
        Check(noLevy.Player.Men == 0 && !noLevy.Player.Companies.Any(co => co.Stacks.Any(s => s.Troop == "brevon_fyrd")), "creator start never spawns levies even from levy origins");
        noLevy.Tick(26);
        Check(noLevy.Player.Men == 0, "lone-captain start does not auto-spawn 30 levies on day 1 midnight");
        Check(dc.S.Characters.Where(l => l.Kind == "lord" && !l.Dead && l.Faction == 1).All(l => l.Opinion.GetValueOrDefault(dc.S.CaptainChar) <= -10), "your homeland's lords remember");
        Check(dc.S.Log.Any(e => e.Text.Contains("own banner")), "the log says you fly your own banner");
        Check(dc.Lasting("camp_born") && dc.Lasting("run_before"), "lasting boons last the whole campaign");
        dc.AddBuff("camp_born", 2);
        dc.S.Hours += 2000 * 24;
        Check(dc.HasBuff("camp_born") && dc.ActiveBuffs.Contains("run_before"), "a renewal never shortens a lasting boon, and it is still on years later");
        Check(Origins.Births.Concat(Origins.Pasts).Count(o => Origins.Boons(o).Any()) >= 15, "most origins lay a boon");

        // Each homeland dresses the captain in its own starting kit, owned and usable, matching the preview.
        var realmKits = new HashSet<string>();
        for (int r = 0; r < Factions.All.Count; r++)
        {
            var rm = new CaptainMaking { Realm = r, Birth = "camp", Past = "sellsword", Road = "sword" };
            var rc = Campaign.New(80 + r, r, rm);
            var rp = Origins.Preview(rm);
            var k = rc.Captain.Kit;
            Check(k.Body == rp.Kit.Body && k.Helm == rp.Kit.Helm && k.Shield == rp.Kit.Shield && k.Weapon == rp.Kit.Weapon && k.Horse == rp.Kit.Horse, $"realm {r} campaign kit matches preview");
            foreach (var id in k.Worn) Check(rc.Owns(id) && rc.CannotUse(Gear.Get(id)) == null, $"realm {r} captain owns and can use {id}");
            Check(rc.PlayerPalette == Factions.All[r].Palette && PortraitPainter.Of(rm).Palette == Factions.All[r].Palette, $"realm {r} captain wears its faction palette");
            realmKits.Add($"{k.Body}|{k.Helm}|{k.Shield}|{k.Weapon}|{k.Horse}");
        }
        Check(realmKits.Count == Factions.All.Count, "every realm gives its own distinct starting kit");
        Check(dc.PlayerPalette == Factions.All[1].Palette && PortraitPainter.Of(d).Palette == Factions.All[1].Palette, "a deserter still wears their homeland's colours");
        // The start is a poor man's: no realm's kit is worth more than a few hundred crowns, and none gives a horse.
        for (int r = 0; r < Factions.All.Count; r++)
        {
            var (body, helm, shield, weapon, horse) = Origins.RealmKit(Factions.CultureOf(r));
            int worth = new[] { body, helm, shield, weapon }.Sum(id => Gear.Get(id).Cost);
            Check(horse == "" && worth <= 250, $"realm {r}'s starting kit is plain ({worth} crowns, horse '{horse}')");
        }

        // PF2: Birth, Past and Road act as day-to-day system on-ramps.
        var cloister = Campaign.New(91, 0, new CaptainMaking { Realm = 0, Birth = "cloister", Past = "pilgrim", Road = "pole" });
        Check(cloister.CanWork("faith") && cloister.Captain.Road == "pole", "cloister/pilgrim can work Faith camp rites from day 1, and Road is saved");
        var forge = Campaign.New(92, 0, new CaptainMaking { Realm = 0, Birth = "forge", Past = "sellsword", Road = "sword" });
        var swDef = Gear.Get("arming_sword");
        Check(forge.MendPrice(swDef) == 0 && forge.RemarkPrice(swDef) < Campaign.RemarkCost(swDef), "forge-born captains mend for free and pay less at the smith");
        var guild = Campaign.New(93, 0, new CaptainMaking { Realm = 0, Birth = "guild", Past = "steward", Road = "bow" });
        var plainTown = guild.S.Settlements.First(t => t.Kind == SettlementKind.Town && t.Faction == 0);
        var plainCmp = Campaign.New(93, 0);
        Check(guild.GoodsPrice(Campaign.GoodsOf("armour"), plainTown) > plainCmp.GoodsPrice(Campaign.GoodsOf("armour"), plainCmp.SettlementById(plainTown.Id)), "guild-born captains earn higher goods prices");
        var high = Campaign.New(94, 0, new CaptainMaking { Realm = 0, Birth = "highborn", Past = "tourney", Road = "lance" });
        var lord0 = high.S.Characters.First(x => x.Kind == "lord" && !x.Dead && x.Faction == 0);
        Check(high.Greeting(lord0).Contains("house's name"), "lords acknowledge highborn origin in greetings");
        Check(high.SettlementWelcome(high.S.Settlements.First(s => s.Faction == 0)).Contains("HOMELAND FAVOUR"), "homeland settlement welcomes its own");
        Check(new BattleOptions().WithCaptain(high.Captain).CaptainRoad == "lance", "Road carries into BattleOptions");
        var poach = Campaign.New(95, 0, new CaptainMaking { Realm = 0, Birth = "marcher", Past = "poacher", Road = "bow" });
        var foe = poach.S.Parties.First(p => p.Kind == PartyKind.Bandit && p.Men > 0);
        foe.ChaseId = -1;
        Check(poach.GroundChooser(foe) == 0, "poacher/marcher wildcraft wins ground choice even when attacking");
        var comp = new Character { Id = 9999, Kind = "companion", Name = "Bram", Faction = -1 };
        Check(poach.Greeting(comp).Contains("Steady eye on the bow"), "companions acknowledge the captain's road");

        // Without the creator nothing changes.
        var plain = Campaign.New(77);
        Check(plain.Captain.Origin == "" && plain.Captain.Visage == -1 && plain.Me.Name == "You" && Rules.CaptainType(plain.Captain).Look.Visage == -1, "a campaign without the creator is the plain captain");
    }
}
