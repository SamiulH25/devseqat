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
        Check(Origins.Pasts.All(p => Campaign.Events.Any(e => e.Trigger == "origin:" + p.Id)), "every past has its own story event");

        var m = new CaptainMaking { Name = "TESTA OF THE FORD", Company = "THE TEST BAND", Realm = 2, Birth = "guild", Past = "hedge_knight", Road = "lance", Skin = 3, Hair = 5, Beard = 2, Nose = 2, Scar = 1, Arms = (int)Emblem.Sun };
        var c = Campaign.New(77, m.Realm, m);
        var preview = Origins.Preview(m);
        Check(c.Me.Name == m.Name && c.Player.Name == m.Company && c.Me.Age == Origins.Age(m) && c.Me.Age == Origins.BaseAge + 5 + 9, "the name, the company's name and the age are the creator's");
        Check(Skills.All.All(s => Skills.Level(c.Captain, s.Id) == Skills.Level(preview, s.Id)), "the campaign's skills are the preview's");
        Check(c.Skill("Riding") == 35 && c.Skill("Stewardship") == 15, "the lance road (Riding 20) and the past (+15) add up");
        Check(c.Has("MountedLord") && c.Captain.BonusPerks.Contains("MountedLord") && Skills.Picks(c.Captain, "Riding") == 1, "the past's perk is free of the branch's marks");
        Check(c.Captain.Kit.Horse == "warhorse" && c.Owns("warhorse") && c.Captain.Kit.Side == "boar_spear", "the kit is the road's and the past's");
        Check(c.S.Gold == Balance.Get().StartGold + 450 - 100 && c.Captain.Renown == Balance.Get().StartRenown + 25, "gold and renown are the creator's sums");
        Check(c.Captain.Contract == 2 && c.Captain.Birth == "guild" && c.Captain.Origin == "hedge_knight", "you serve your homeland, and the origin is saved");
        var look = Rules.CaptainType(c.Captain).Look;
        Check(look.Visage == m.Visage && Variety.Parts(look.Visage) == (3, 5, 2) && look.NeutralHead, "the face you chose is the face you fight with");
        Check(c.S.Event == null && c.OriginNote == "Born in the Market, once a Hedge Knight", "no road event after the creator, and the K sheet names the origin");
        var back = new Campaign(CampaignState.Deserialize(c.S.Serialize()));
        Check(back.Captain.Origin == "hedge_knight" && back.Captain.Visage == m.Visage, "origin and face survive a save");
        var face = PortraitPainter.Of(back, back.Me);
        Check(face.Beard == 2 && face.Nose == 2 && face.Scar >= 1 && face.Skin == 3 && face.Charge == Emblem.Sun, "the painted face and arms are the creator's, after a save");
        Check(c.Player.Companies.All(co => co.Banner == (int)Emblem.Sun), "your first companies fly your arms");
        Check(new CaptainMaking { Cut = 4, Beard = 1 }.Style == 3 && new CaptainMaking { Cut = 4, Beard = 0 }.Style == 0, "a bald, bearded face is the bald battle head");
        foreach (var road in Origins.Roads)
        {
            var rc = Campaign.New(79, 0, new CaptainMaking { Road = road.Id });
            Check(Origins.RoadGives(road.Id).Count >= 2 && rc.CannotUse(Gear.Get(rc.Captain.Kit.Weapon)) == null, $"the {road.Id} road gives skills and kit you can use");
        }
        // Origin events: only yours, and only after the first fortnight.
        Check(c.Find("origin:hedge_knight") == null, "the past waits a fortnight");
        c.S.Hours = 15 * 24;
        Check(c.Find("origin:hedge_knight") != null && c.Find("origin:guild") != null && c.Find("origin:poacher") == null, "an origin's events come only to its captain");

        // A deserter flies their own banner, with spearmen at their back and their homeland's lords cold.
        var d = new CaptainMaking { Name = "RUNAWAY", Realm = 1, Birth = "camp", Past = "deserter", Road = "sword" };
        var dc = Campaign.New(78, d.Realm, d);
        Check(dc.Captain.Contract == -1 && dc.Me.Faction == -1, "a deserter starts as a free company");
        Check(dc.Player.Companies.Any(co => co.Stacks.Any(s => s.Troop == "spearrunners" && s.Men == dc.Scaled(6))), "the men who followed you come as a company");
        Check(dc.S.Characters.Where(l => l.Kind == "lord" && !l.Dead && l.Faction == 1).All(l => l.Opinion.GetValueOrDefault(dc.S.CaptainChar) <= -10), "your homeland's lords remember");
        Check(dc.S.Log.Any(e => e.Text.Contains("own banner")), "the log says you fly your own banner");

        // Without the creator nothing changes.
        var plain = Campaign.New(77);
        Check(plain.Captain.Origin == "" && plain.Captain.Visage == -1 && plain.Me.Name == "You" && Rules.CaptainType(plain.Captain).Look.Visage == -1, "a campaign without the creator is the plain captain");
    }
}
