using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the career and realm layers: the pilot, ranks, fiefs, diplomacy, events and quests.
public static partial class SelfTest
{
    /// Debug: a pilot's campaign with its log (`--selftest PilotTrace`).
    static void PilotTrace()
    {
        var c = Campaign.New(int.TryParse(Environment.GetEnvironmentVariable("IRONFIELD_SEED"), out int seed) ? seed : 2001);
        var pilot = new Pilot(c);
        int last = 0;
        for (int d = 0; d < 110; d++)
        {
            pilot.Run(24);
            foreach (var e in c.S.Log.Skip(Math.Max(0, c.S.Log.Count - 60)).Where(e => e.At >= (d) * 24 && !e.Text.Contains(" defeats ") && !e.Text.Contains("musters") && !e.Text.Contains("threatens") && !e.Text.Contains("besiege"))) Console.WriteLine($"  d{c.Day} {e.Text}");
            if (c.Player.Men != last) Console.WriteLine($"day {c.Day}: men {c.Player.Men} food {c.Player.Food:0} gold {c.S.Gold:0} renown {c.Captain.Renown:0} at {c.Player.Pos.X:0},{c.Player.Pos.Y:0} moving {c.Player.Moving} goal {c.Player.GoalSettlement} chase {c.Player.ChaseId}");
            last = c.Player.Men;
        }
    }

    /// Career ranks, fealty, the council, founding a realm and winning (F5).
    static void Career()
    {
        var c = Campaign.New(5005);
        Check(c.Rank == Rank.FreeCompany && c.Stage == "FREE COMPANY", "a new captain is a free company");
        Check(c.S.Factions.Take(3).All(f => c.CharacterById(f.Ruler) is { Kind: "lord" } && c.CharacterById(f.Marshal) != null), "every realm has a ruler and a marshal");
        Check(!c.CanSwearFealty(out _), "a free company cannot swear fealty");
        c.S.Hours = Campaign.SwornDays * 24 + 1;
        Check(c.Rank == Rank.SwornSword, "a month of service makes a sworn sword");
        var village = c.S.Settlements.First(s => s.Kind == SettlementKind.Village && s.Faction == 0);
        Check(c.RecruitPriceFactor(village) == 0.5f, "a sworn sword recruits at half price in his realm");
        c.Captain.Renown = 100;
        Check(!c.CanSwearFealty(out string why) && why.Contains("RENOWN"), "fealty needs renown: " + why);
        c.Captain.Renown = 200;
        var ruler = c.RulerOf(0)!;
        ruler.Opinion[c.Me.Id] = -40;
        Check(!c.CanSwearFealty(out why) && why.Contains("TRUST"), "fealty needs the liege's trust: " + why);
        ruler.Opinion[c.Me.Id] = 30;
        Check(c.SwearFealty() == null && c.Rank == Rank.Vassal && c.Beat != null, "you swear fealty and become a vassal, with a career beat");
        c.Beat = null;

        // The council grants a seat the realm took; a strong favourite among the lords wins it, and as a vassal you stand too.
        var seat = c.S.Settlements.First(s => s.Kind == SettlementKind.Castle && s.Faction == 1);
        var lords0 = c.S.Characters.Where(ch => ch.Kind == "lord" && !ch.Dead && ch.Faction == 0).ToList();
        foreach (var l in lords0) l.Opinion[c.Me.Id] = 60;
        typeof(Campaign).GetMethod("Capture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(c, new object[] { seat, 0, -1, lords0[0].Id });
        Check(seat.Faction == 0 && seat.Owner == -1 && c.S.Factions[0].Pending.Any(g => g.Seat == seat.Id), "a seat a lord takes waits for the council");
        c.S.Factions[0].NextCouncil = 0;
        Days(c, 1);
        Console.WriteLine($"career: council granted {seat.Name} to {(seat.Owner == c.Player.Id ? "you" : c.PartyById(seat.Owner)?.Name ?? "nobody")}: {string.Join(" ", c.S.Factions[0].LastCouncil)}");
        Check(seat.Owner >= 0, "the council grants the seat to someone");
        Check(c.S.Factions[0].LastCouncil.Count > 0, "the council's decisions are kept for the realm screen");

        // Lord: two fiefs and renown. Then the marshal's baton, by vote, and an objective for the hosts.
        foreach (var t in c.S.Settlements.Where(s => s.Faction == 0 && s.Kind != SettlementKind.Village).OrderByDescending(s => s.Kind == SettlementKind.Town).Take(2)) t.Owner = c.Player.Id;
        c.Captain.Renown = 400;
        Check(c.Rank == Rank.Lord, "two fiefs and renown make a lord");
        Days(c, 1);
        Check(c.Beat is { Title: "LORD" }, "becoming a lord is a career beat");
        c.Beat = null;
        var truce = c.WarBetween(0, 2)!;
        truce.Active = false;
        truce.TruceUntil = (float)c.S.Hours + 90 * 24;
        Check(c.ProposeWar(2) == null && c.S.Factions[0].WarMotion == 2, "a lord can put a motion of war to the council");
        c.S.Factions[0].Marshal = -1; // the baton is vacant: the council elects
        Check(c.NextRank()!.Value.Needs.Contains("RENOWN TO STAND"), "standing for marshal needs more renown than a lord");
        c.Captain.Renown = Campaign.MarshalRenown;
        c.S.Factions[0].NextCouncil = 0;
        Days(c, 1);
        Console.WriteLine($"career: second council: {string.Join(" ", c.S.Factions[0].LastCouncil)}");
        Check(c.Rank == Rank.Marshal, "lords who think well of you elect you marshal");
        var target = c.ObjectiveChoices(0).First();
        Check(c.SetObjective(target) == null && c.ObjectiveOf(0) == target, "the marshal names the hosts' objective");

        // Rebellion: your fiefs become a realm at war with your old liege; a lord who loves you and not his king follows.
        var follower = lords0.First(l => l.Id != c.S.Factions[0].Ruler && !l.Traits.Contains("Loyal"));
        follower.Opinion[c.Me.Id] = 90;
        follower.Opinion[c.S.Factions[0].Ruler] = -50;
        var stay = lords0.FirstOrDefault(l => l.Traits.Contains("Loyal") && l.Id != c.S.Factions[0].Ruler);
        int before = c.S.Factions.Count;
        Check(c.DeclareIndependence() == null, "a lord with a town can declare independence");
        int mine = c.Captain.Contract;
        Check(c.S.Factions.Count == before + 1 && mine == before && c.Rank == Rank.King, "a new realm, and you are its king");
        Check(c.FactionsAtWar(mine, 0) && !c.FactionsAtWar(mine, 1), "the new realm is at war with the old liege only");
        Check(c.Fiefs.All(t => t.Faction == mine) && c.S.Provinces.Count(p => p.Faction == mine) >= 2, "your fiefs and their provinces go with you");
        Check(follower.Faction == mine && c.PartyById(follower.PartyId)!.Faction == mine, "a lord who loves you follows you");
        if (stay != null) Check(stay.Faction == 0, "a loyal lord stays with his liege");
        Check(Factions.Get(mine).Name == c.S.Factions[mine].Name && Factions.CultureOf(mine) == c.S.Factions[mine].Culture, "the founded realm has its own name and culture");
        Check(Style.PaletteOf(mine) == 2, "your realm flies your colours");
        Check(c.SignContract(1) != null, "a king serves no one");
        Check(c.DeclareWar(2) == null && c.FactionsAtWar(mine, 2), "a king declares war");
        Check(c.OfferPeace(2) == null && !c.FactionsAtWar(mine, 2), "a king makes peace");
        Days(c, 3); // the new realm lives in the world for a while: its lords think, its council sits

        // A save keeps the founded realm.
        var json = c.S.Serialize();
        var back = new Campaign(CampaignState.Deserialize(json));
        Check(back.S.Factions.Count == before + 1 && back.Rank == Rank.King && back.FactionNames[mine] == c.FactionNames[mine], "a founded realm survives save and load");
        Check(back.S.Serialize() == json, "save, load, save is stable with a founded realm");

        // Winning: hold 60% of the provinces as king (two more, so a fief lost in the day of war that follows keeps it over).
        foreach (var p in c.S.Provinces.Take((int)MathF.Ceiling(c.S.Provinces.Count * Campaign.WinShare) + 2))
        {
            p.Faction = mine;
            foreach (var s in c.S.Settlements.Where(s => s.Province == p.Id)) s.Faction = mine;
        }
        c.Beat = null;
        Days(c, 1);
        Check(c.S.Won && c.Beat is { Title: "THE WAR IS WON" }, $"holding most of the land as king wins the campaign (won {c.S.Won}, beat {c.Beat?.Title}, rank {c.Rank}, share {c.RealmShare(mine):0.00}, over {c.S.GameOver})");
        // Losing: a king whose realm holds no seat has lost.
        foreach (var s in c.S.Settlements.Where(s => s.Faction == mine)) s.Faction = 0;
        Days(c, 1);
        Check(c.S.GameOver && c.S.EndReason.Contains("FALLEN"), "a king whose realm falls has lost");
    }

    /// The stand-in captain, playing by the map screen's own actions, earns a sworn sword's month and then swears fealty (F5).
    static void PilotCareer()
    {
        // One seed is luck (any change to the world reshuffles it): the pilot must make vassal in two seeds of three.
        int made = 0, tried = 0;
        foreach (int seed in new[] { 2002, 2003, 2004 })
        {
            if (made >= 2 || tried - made >= 2) break;
            tried++;
            var c = Campaign.New(seed);
            var pilot = new Pilot(c);
            for (int d = 0; d < 120 && c.Rank < Rank.Vassal; d++) pilot.Run(24);
            Console.WriteLine($"pilot career (seed {seed}): {c.Stage} on day {c.Day}, renown {c.Captain.Renown:0}, {pilot.Battles} battles ({pilot.Won} won)");
            if (c.Rank >= Rank.Vassal) made++;
        }
        Check(made >= 2, $"the pilot reaches vassal within 120 days in two seeds of three ({made} of {tried})");
    }

    /// The free road to a crown (P5): an unsworn company with renown keeps a town it storms and founds a realm.
    static void FreeCrown()
    {
        var c = Campaign.New(6010);
        int f = Enumerable.Range(0, c.S.Factions.Count).First(a => Enumerable.Range(0, c.S.Factions.Count).Any(b => c.FactionsAtWar(a, b)));
        Check(c.SignContract(f) == null || c.Captain.Contract == f, "signed with a realm at war");
        c.Captain.Contract = f;
        var town = c.S.Settlements.Where(t => t.Kind == SettlementKind.Town && c.FactionsAtWar(f, t.Faction)).OrderBy(t => t.Id).First();
        c.Captain.Renown = 200;
        c.AfterAssault(town, true);
        Check(town.Faction == f && c.S.Event == null && c.CannotClaimCrown(town)!.Contains("RENOWN"), "a small name takes a town for its paymaster and nothing more");
        var town2 = c.S.Settlements.Where(t => t.Kind == SettlementKind.Town && c.FactionsAtWar(f, t.Faction)).OrderBy(t => t.Id).First();
        c.Captain.Renown = Campaign.CrownRenown;
        c.AfterAssault(town2, true);
        Check(c.S.Event?.Id == "free_crown", "with the renown, the company is asked whether to keep it");
        Check(c.Choose(1) == null, "keep it");
        Check(c.Rank == Rank.King && town2.Faction == c.Captain.Contract && town2.Faction != f && town2.Owner == c.Player.Id, "a free company becomes a king");
        Check(c.FactionsAtWar(f, c.Captain.Contract), "at war with the realm that hired it");
        Check(c.CannotClaimCrown(town2) != null, "and a king claims no second crown");
        // Titles past 300 renown: from the captain's name first, else the deeds.
        var t = Campaign.New(6011);
        Check(t.Title == "", "no title for a small name");
        t.Captain.Renown = Campaign.TitleRenown;
        Check(t.Title == "the Bold", "a known captain is at least the Bold");
        t.S.Tally["holdouts"] = 3;
        Check(t.Title == "Goblin-Bane", "burnt goblin holes give a name");
        t.Me.Traits.Add("Lionheart");
        Check(t.Title == "the Lionheart" && t.Summary()[1].Contains("THE LIONHEART"), "a trait outranks a deed, and the summary says it");
    }

    /// Fiefs (F6): taxes and buildings change income, a neglected foreign fief revolts and defects, vassals take orders,
    /// and a lord who hates his liege goes over to a ruler he likes.
    static void FiefLife()
    {
        var c = Campaign.New(6006);
        Check(c.S.Settlements.Where(s => s.Kind == SettlementKind.Town).All(s => Campaign.Level(s, "barracks") == 1), "every town starts with a barracks");
        c.Captain.Fealty = true;
        var town = c.S.Settlements.Where(s => s.Kind == SettlementKind.Town && s.Faction == 0).OrderBy(s => s.Id).First();
        var castle = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && s.Faction == 0).OrderBy(s => s.Id).First();
        town.Owner = castle.Owner = c.Player.Id;
        float normal = c.Income(town);
        Check(c.SetTax(town, 2) == null && c.Income(town) > normal * 1.3f, "high taxes raise a fief's income");
        c.SetTax(town, 1);
        town.Prosperity = 90;
        Check(c.Income(town) > normal * 1.2f, "a prosperous fief pays more");
        town.Prosperity = 50;
        float bare = c.Income(town);
        c.S.Gold = 5000;
        Check(c.Build(town, "market") == null && town.Building == "market", "you can build a market in your town");
        Check(c.Build(town, "walls") != null, "one building at a time");
        Check(c.Build(c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Owner != c.Player.Id), "walls") != null, "only in your own fiefs");
        Check(c.Build(castle, "market") != null, "a castle has no market");
        Days(c, 9);
        Check(Campaign.Level(town, "market") == 1 && town.Building == "", "the market is finished after its days");
        Check(c.Income(town) / (0.5f + town.Prosperity / 100) > bare / 1.0f * 1.15f, "a market raises income");
        c.Build(castle, "walls");
        Days(c, 13);
        Check(Campaign.MaxSupplies(castle) > 30, "walls lay in more supplies");

        // A foreign fief, taxed hard and left bare, revolts and then goes over to its own people's realm.
        var foreign = c.S.Settlements.Where(s => s.Kind == SettlementKind.Castle && s.Faction == 1 && s.Culture == Factions.CultureOf(1)).OrderBy(s => s.Id).First();
        var kin = foreign.Faction;
        foreign.Faction = 0;
        c.S.Provinces[foreign.Province].Faction = 0;
        foreign.Owner = c.Player.Id;
        foreign.Garrison.Clear();
        c.SetTax(foreign, 2);
        Check(c.UnrestDrift(foreign).Drift > 0.3f, "foreign rule and high taxes stir unrest: " + string.Join(" ", c.UnrestDrift(foreign).Why));
        foreign.Unrest = 69;
        Party? rebels = null;
        bool rose = false;
        // A rising can be put down the same day it starts (the log still tells of it). Kept at the brink each day: the
        // world's own doings (a holdout burnt out nearby, a lord's victory) can calm it a few points first. A rising is a 10%
        // roll a day, so 60 days leave about one run in five hundred without one.
        for (int d = 0; d < 60 && rebels == null && !rose; d++)
        {
            foreign.Unrest = MathF.Max(foreign.Unrest, Campaign.RevoltAt);
            Days(c, 1);
            rebels = c.Rebels(foreign);
            rose = c.S.Log.Any(e => e.At >= c.S.Hours - 24 && e.Text.Contains($"{foreign.Name} rises in revolt"));
        }
        Check(rebels != null || rose, "a neglected fief rises in revolt");
        Console.WriteLine($"fiefs: {foreign.Name} revolts on day {c.Day} with {rebels?.Men.ToString() ?? "a band put down that day:"} rebels, unrest {foreign.Unrest:0}");
        if (rebels != null) c.S.Parties.Remove(rebels); // left in the field, the rebels could be beaten and calm the place
        // Set it at the brink just before the day turns, so nothing in the world (a holdout burnt out nearby: -3) calms it first.
        while (c.S.Hours % 24 < 23) { c.Tick(0.5f); c.Encounter = null; c.Arrived = null; }
        // Held at the brink: the daily drift (small by now, but upward) carries it over the edge within a few days.
        for (int d = 0; d < 5 && foreign.Faction != kin; d++)
        {
            foreign.Unrest = MathF.Max(foreign.Unrest, 99.9f);
            Days(c, 1);
        }
        Check(foreign.Faction == kin && foreign.Owner != c.Player.Id, "at the end of its patience a fief goes over to its own people");

        // Vassals: a lord with two fiefs raises a companion to land and gives him orders.
        c.Captain.Renown = 400;
        // Two fiefs again (one went over to its people, and the wars may have taken others): castles of the realm, unheld
        // ones first.
        foreach (var t in c.S.Settlements.Where(s => s.Faction == 0 && s.Kind == SettlementKind.Castle && s.Owner != c.Player.Id).OrderBy(s => s.Owner < 0 ? 0 : 1).ToList())
        {
            if (c.FiefSeats >= Campaign.LordFiefs) break;
            t.Owner = c.Player.Id;
        }
        Check(c.Rank >= Rank.Lord, "two fiefs and renown make a lord");
        c.S.Gold += 1000;
        var town2 = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Faction == 0);
        c.Player.Pos = town2.Pos;
        var w = c.WandererAt(town2) ?? MakeWanderer(c, town2);
        Check(c.HireWanderer(town2) == null, "a wanderer joins the household");
        var comp = c.Companions.First();
        int seatsBefore = c.FiefSeats;
        Check(c.RaiseVassal(comp) == null, "a lord raises a companion to a vassal");
        var vassal = c.Vassals.Single();
        Check(c.FiefSeats == seatsBefore - 1 && c.S.Settlements.Any(s => s.Owner == vassal.Id), "the vassal holds a fief of his own");
        Check(c.SetOrder(vassal, "follow") == null, "you give your vassal a standing order");
        vassal.Pos = c.Player.Pos + new Vector2(60, 0);
        float before = Vector2.Distance(vassal.Pos, c.Player.Pos);
        Days(c, 1);
        Console.WriteLine($"fiefs: vassal {vassal.Name} follows from {before:0} to {Vector2.Distance(vassal.Pos, c.Player.Pos):0}");
        Check(Vector2.Distance(vassal.Pos, c.Player.Pos) < before * 0.5f || vassal.Down, "a vassal told to follow you comes to you");

        // Defection at the council: a lord who hates his liege and admires another ruler changes sides.
        var realm1 = c.S.Factions[1];
        var turn = c.S.Characters.First(ch => ch.Kind == "lord" && !ch.Dead && ch.Faction == 1 && ch.Id != realm1.Ruler && !ch.Traits.Contains("Loyal"));
        turn.Opinion[realm1.Ruler] = -80;
        turn.Opinion[c.S.Factions[2].Ruler] = 80;
        realm1.NextCouncil = 0;
        Days(c, 1);
        Check(turn.Faction == 2, "a lord who hates his liege goes over to a ruler he admires: " + string.Join(" ", realm1.LastCouncil));

        // The world builds without you.
        var built = c.S.Settlements.Sum(s => s.Buildings.Values.Sum());
        Days(c, 60);
        Console.WriteLine($"fiefs: buildings in the world {built} -> {c.S.Settlements.Sum(s => s.Buildings.Values.Sum())}, prosperity avg {c.S.Settlements.Average(s => s.Prosperity):0}, chests {string.Join("/", c.S.Factions.Select(f => f.Treasury.ToString("0")))}");
        Check(c.S.Settlements.Sum(s => s.Buildings.Values.Sum()) > built, "AI realms raise buildings");
        Check(c.S.Settlements.All(s => s.Prosperity is >= 0 and <= 100 && s.Unrest is >= 0 and <= 100), "prosperity and unrest stay in range");
    }

    /// Strategic war and diplomacy (F7): a long AI run ends wars with terms and moves the border by planned campaigns; a king
    /// imposes terms only as far as he is ahead, allies against a shared enemy, and draws tribute; a vassal is summoned.
    static void WarAndPeace()
    {
        var c = Campaign.New(7007);
        var owners = c.S.Settlements.ToDictionary(s => s.Id, s => s.Faction);
        float peakExh = 0, peakShare = 0;
        int hosts = 0, fellEarly = 0;
        for (int d = 0; d < 240; d++)
        {
            Days(c, 1);
            peakShare = MathF.Max(peakShare, Enumerable.Range(0, c.S.Factions.Count).Max(f => c.S.Provinces.Count(p => p.Faction == f)) / (float)c.S.Provinces.Count);
            if (d < 120) fellEarly = Math.Max(fellEarly, Enumerable.Range(0, c.S.Factions.Count).Count(f => !c.Alive(f)));
            peakExh = MathF.Max(peakExh, c.S.Wars.Where(w => w.Active).Select(w => MathF.Max(w.ExhA, w.ExhB)).DefaultIfEmpty(0).Max());
            hosts += c.S.Log.Count(e => e.At >= c.S.Hours - 24 && e.Text.Contains("gathers an army"));
        }
        int moved = c.S.Settlements.Count(s => s.Kind != SettlementKind.Village && s.Faction != owners[s.Id]);
        var terms = c.S.Treaties.Where(t => t.Contains("sue")).ToList();
        Console.WriteLine($"war and peace: 240 days, {moved} seats changed hands, {hosts} hosts, peak weariness {peakExh:0}, {c.S.Treaties.Count} treaties recorded");
        foreach (var t in c.S.Treaties.TakeLast(8)) Console.WriteLine("  " + t);
        Check(peakExh > 30, "wars wear realms down");
        Check(terms.Count > 0, "a long war ends in peace on terms");
        Check(moved > 0 && hosts > 0, "hosts march and seats change hands");
        // The snowball guard (docs/archive/WORLD.md W5; --realms runs it on more seeds): no realm falls in the first year, none runs away.
        Console.WriteLine($"  largest realm {peakShare * 100:0}% of the provinces, {fellEarly} fallen in year one");
        Check(fellEarly == 0 && peakShare <= 0.4f, "no realm falls in the first year and none holds over 40% of the map");
        for (int f = 0; f < c.S.Factions.Count; f++)
            if (c.Alive(f))
            {
                Check(c.S.Factions[f].Treasury >= 0, $"realm {f} keeps a treasury");
                Check(c.S.Parties.Any(p => p.Kind == PartyKind.Lord && p.Faction == f), $"realm {f} still has lords");
            }

        // A king's terms: only what his standing allows.
        var k = Campaign.New(7008);
        k.Captain.Fealty = true;
        k.Captain.Renown = 400;
        foreach (var t in k.S.Settlements.Where(s => s.Faction == 0 && s.Kind != SettlementKind.Village).OrderByDescending(s => s.Kind == SettlementKind.Town).Take(2)) t.Owner = k.Player.Id;
        Check(k.DeclareIndependence() == null, "a lord rebels to test diplomacy");
        int me = k.Captain.Contract;
        var war = k.WarBetween(me, 0)!;
        Check(k.OfferPeace(0, Terms.Cede) != null, "an even war will not buy you a seat");
        war.ScoreA = war.A == me ? 3000 : 0; war.ScoreB = war.B == me ? 3000 : 0;
        if (war.A == me) war.ExhB = 60; else war.ExhA = 60;
        Check(k.Margin(war, me, 0) >= 0.9f, "winning raises your standing");
        int seats0 = k.S.Settlements.Count(s => s.Faction == 0 && s.Kind != SettlementKind.Village);
        Check(k.OfferPeace(0, Terms.Cede) == null && !k.FactionsAtWar(me, 0), "a winning king takes a seat in the peace");
        Check(k.S.Settlements.Count(s => s.Faction == 0 && s.Kind != SettlementKind.Village) == seats0 - 1, "the seat changes hands");
        // Tribute: a tributary pays each week.
        k.S.Factions[1].TributaryOf = me;
        k.S.Factions[1].TributeUntil = (float)k.S.Hours + 30 * 24;
        k.S.Factions[1].Treasury = 5000;
        float gold = k.S.Gold;
        Days(k, 8);
        Check(k.S.Gold > gold + 1 || k.S.Factions[1].Treasury < 5000, "a tributary pays its overlord");
        Check(k.RealmShare(me) > k.ProvinceShare(me), "a tributary's land counts toward your victory");
        // An alliance needs a shared enemy.
        Check(k.ProposeAlliance(2) != null || k.FactionsAtWar(2, 0), "no alliance without a shared enemy");
        k.DeclareWar(0);
        var w20 = k.WarBetween(2, 0) ?? new War { A = 2, B = 0 };
        if (!k.S.Wars.Contains(w20)) k.S.Wars.Add(w20);
        w20.Active = true;
        if (k.RulerOf(2) is Character r2) r2.Opinion[k.Me.Id] = 20;
        Check(k.ProposeAlliance(2) == null && k.Allied(me, 2), "a king allies against a shared enemy");

        // A vassal is summoned to the host, and answering wins the marshal's favour.
        var v = Campaign.New(7009);
        v.Captain.Fealty = true;
        v.S.Hours = 40 * 24;
        var marshal = v.MarshalOf(0)!;
        var target = v.S.Provinces.First(p => p.Faction == 1);
        typeof(Campaign).GetMethod("MusterHost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(v, new object[] { 0, target, v.S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == 0).ToList() });
        Check(v.S.Summons != null && v.Beat is { Title: "THE MARSHAL SUMMONS YOU" }, "a vassal is summoned when the realm musters a host");
        float before = v.OpinionOfYou(marshal);
        v.Player.Pos = v.SettlementById(v.S.Summons!.Seat).Pos + new Vector2(5, 0);
        v.Player.Cooldown = 1e6f;
        Days(v, 1);
        Check(v.S.Summons == null && v.OpinionOfYou(marshal) > before, "answering the summons pleases the marshal");
    }

    /// Events and quests (F8): the data is valid; a long campaign raises events about what happened in it; a lord's quest done
    /// wins his favour and a refusal costs it; a brigand king rises and falls; a bad revolt finds a pretender.
    static void EventsAndQuests()
    {
        var problems = Campaign.ValidateEvents().ToList();
        Check(problems.Count == 0, "events.json is valid: " + string.Join("; ", problems));
        Check(Campaign.Events.Count >= 12, "a dozen events or more");

        // A long campaign played by the stand-in captain: events come, and they name what is in its world.
        var c = Campaign.New(8008);
        var pilot = new Pilot(c);
        pilot.Run(150 * 24);
        var fired = c.S.EventsFired.Keys.ToList();
        Console.WriteLine($"events: {fired.Count} kinds fired in 150 days: {string.Join(", ", fired)}");
        Check(fired.Count >= 3, "a long campaign raises several kinds of events");

        // A lord's quest: done, he thinks better of you; refused, worse.
        var q = Campaign.New(8009);
        var lord = q.S.Characters.First(ch => ch.Kind == "lord" && !ch.Dead && ch.Faction == 0);
        lord.Opinion[q.Me.Id] = 50;
        q.S.Event = new PendingEvent { Id = "lord_favour", Lord = lord.Id };
        float before = q.OpinionOfYou(lord);
        Check(q.Choose(0) == null && q.S.Contracts.Any(k => k.Giver == lord.Id), "a lord's request becomes a quest");
        var quest = q.S.Contracts.First(k => k.Giver == lord.Id);
        // Hunt and clear quests end with a won battle; the relic chain (S3) ends when each site is reached.
        for (int step = 0; step < 3 && q.S.Contracts.FirstOrDefault(k => k.Giver == lord.Id) is Contract now; step++)
        {
            if (now.Kind == "visitsite") now.Progress = 1; else q.S.LastBattleFoe = now.Target;
            Days(q, 1);
        }
        Check(!q.S.Contracts.Contains(quest) && q.OpinionOfYou(lord) > before + 5, $"a quest done wins the lord's favour ({before:0} -> {q.OpinionOfYou(lord):0})");
        q.S.Event = new PendingEvent { Id = "lord_favour", Lord = lord.Id };
        before = q.OpinionOfYou(lord);
        q.Choose(1);
        Check(q.OpinionOfYou(lord) < before, "refusing a lord costs his favour");
        Check(q.ChoiceBlocked(Campaign.EventOf("famine")!.Choices[0]) == "" || q.S.Gold < 200, "a choice you can pay for is open");
        q.S.Gold = 10;
        Check(q.ChoiceBlocked(Campaign.EventOf("famine")!.Choices[0]) != "", "a choice you cannot pay for is shut");

        // A brigand king rises from the largest band, and dies at your hand.
        var b = Campaign.New(8010);
        b.S.Hours = 31 * 24;
        var band = b.S.Parties.First(p => p.Kind == PartyKind.Bandit && !p.Down);
        band.Companies.Add(b.NewCompany("levies", "infantry", b.Scaled(24), 0));
        band.Companies.Add(b.NewCompany("levies", "spears", b.Scaled(12), 0));
        band.Cooldown = 30; // kept out of fights for the day, so a passing lord cannot scatter it first
        Days(b, 1);
        var king = b.CharacterOf(band);
        Check(king is { Kind: "brigand" } && b.S.Event is { Id: "brigand_king" }, "the largest band crowns a brigand king");
        b.Choose(0);
        Check(b.S.Contracts.Any(k => k.Target == band.Id && k.Reward >= 400), "a bounty is posted on the brigand king");
        b.Player.Companies.Clear();
        foreach (var t in new[] { "ironguard", "ironguard", "arbalests", "phalanx_pikemen" }) b.Player.Companies.Add(b.NewCompany(t, Rules.TroopRole(t), Rules.CapOf(t, Rules.TroopRole(t)), 0));
        b.Encounter = band;
        float renown = b.Captain.Renown;
        var rep = b.AutoResolveEncounter();
        Check(!rep.Won || king!.Dead && b.Captain.Renown > renown + 20, "a brigand king beaten in the field is dead, and you are famous");

        // A bad revolt in your town finds a pretender, and his town would go over sooner.
        var r = Campaign.New(8011);
        var town = r.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Faction == 0);
        town.Owner = r.Player.Id;
        town.Unrest = 90;
        typeof(Campaign).GetMethod("Revolt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(r, new object[] { town });
        var rising = r.Rebels(town);
        Check(rising != null && r.CharacterOf(rising) is { Kind: "pretender" } && r.S.Event is { Id: "pretender" }, "a bad revolt in your town raises a pretender");
    }
}
