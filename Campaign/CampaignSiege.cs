using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap B1/B2: garrisons, sieges (yours and the lords'), the capture of towns and castles, and fiefs.
public sealed partial class Campaign
{
    /// The tier-1 recruits a faction's villages give (the Northmarch raise clansmen; the others levies).
    public static string RecruitTroop(int faction) => Armies.LineTroop("infantry", 1, Math.Max(0, faction));

    /// What a settlement yields a week: to its faction's treasury, or to you when it is your fief (with its province's villages).
    public float Income(Settlement t) => t.Kind switch
    {
        SettlementKind.Town => B.TownIncome, SettlementKind.Castle => B.CastleIncome, _ => t.ThreatUntil > S.Hours ? 0 : B.VillageIncome,
    } * (t.Besieged ? 0.25f : 1);

    public IEnumerable<Settlement> Fiefs => S.Settlements.Where(t => t.Owner == Player.Id);

    // ---------- garrisons ----------

    /// Towns keep infantry, archers and spears behind their walls; castles infantry and archers.
    void RaiseGarrison(Settlement s, float fill = 0.8f)
    {
        if (s.Kind == SettlementKind.Village) return;
        int tier = Math.Clamp(2 + Day / 45, 2, 5);
        var roles = s.Kind == SettlementKind.Town ? new[] { "infantry", "archers", "spears" } : new[] { "infantry", "archers" };
        foreach (var role in roles)
        {
            if (s.Garrison.Any(c => Rules.RoleOf(c) == role)) continue;
            string troop = Armies.LineTroop(role, tier, s.Faction);
            s.Garrison.Add(NewCompany(troop, role, Math.Max(4, (int)(Rules.Cap(troop, role) * fill)), 0));
        }
    }

    /// The garrison as a party for a battle; it shares the settlement's company list, so losses stick.
    public Party GarrisonParty(Settlement s) => new()
    {
        Id = -1000 - s.Id, Kind = PartyKind.Garrison, Faction = s.Faction, Name = "the garrison of " + s.Name, Pos = s.Pos, Companies = s.Garrison,
        Tier = s.Garrison.Count == 0 ? 1 : Math.Clamp((int)MathF.Round((float)s.Garrison.Average(c => c.Type.Tier)), 1, 7), HomeId = s.Id,
    };

    public int GarrisonMen(Settlement s) => s.Garrison.Sum(c => c.Men);

    /// Weekly: each faction pays to bring its garrisons back up (not while besieged); wounded heal.
    void GarrisonsWeekly()
    {
        foreach (var s in S.Settlements.Where(s => s.Kind != SettlementKind.Village && !s.Besieged))
        {
            foreach (var c in s.Garrison) { c.Men += c.Wounded; c.Wounded = 0; }
            var fs = S.Factions[s.Faction];
            RaiseGarrison(s, 0.4f);
            foreach (var c in s.Garrison.Where(c => c.Type.Tier <= 5))
            {
                int want = (int)(Rules.Cap(c.Troop, Rules.RoleOf(c)) * 0.8f) - c.Men;
                int n = Math.Min(want, (int)(fs.Treasury / 20));
                if (n <= 0) continue;
                Rules.AddMen(c, n);
                fs.Treasury -= n * 20;
            }
            s.Supplies = MathF.Min(30, s.Supplies + 7);
        }
    }

    /// Leave one of your companies to hold your fief (it joins the garrison and costs no upkeep while there).
    public string? Station(CompanyRecord c, Settlement s)
    {
        if (s.Owner != Player.Id) return "ONLY IN YOUR OWN FIEFS";
        if (Player.Companies.Count <= 1) return "YOU NEED ONE COMPANY WITH YOU";
        Player.Companies.Remove(c);
        s.Garrison.Add(c);
        Log($"{c.Name} stay to hold {s.Name}.");
        return null;
    }

    public string? Withdraw(CompanyRecord c, Settlement s)
    {
        if (s.Owner != Player.Id) return "ONLY IN YOUR OWN FIEFS";
        if (Player.Companies.Count >= Rules.CompanySlots(Captain)) return "NO FREE COMPANY";
        if (c.Men > Rules.MenLimit(Captain) - Player.Men - Player.Wounded) return "PARTY IS FULL";
        s.Garrison.Remove(c);
        Player.Companies.Add(c);
        return null;
    }

    // ---------- your sieges ----------

    public Settlement? Besieging => Player.SiegeTarget >= 0 ? S.Settlements[Player.SiegeTarget] : null;

    public bool CanBesiege(Settlement s, out string why)
    {
        why = s.Kind == SettlementKind.Village ? "VILLAGES HAVE NO WALLS"
            : Captain.Contract < 0 ? "SIGN WITH A FACTION FIRST"
            : !FactionsAtWar(Captain.Contract, s.Faction) ? "YOU ARE NOT AT WAR WITH THEM"
            : Player.Men < 20 ? "TOO FEW MEN (20 OR MORE)"
            : Player.SiegeTarget >= 0 && Player.SiegeTarget != s.Id ? "YOU ARE BESIEGING ANOTHER PLACE" : "";
        return why == "";
    }

    public void Besiege(Settlement s)
    {
        if (!CanBesiege(s, out _)) return;
        Player.SiegeTarget = s.Id;
        Player.Moving = false;
        Player.Pos = s.Pos + Vector2.Normalize(Player.Pos - s.Pos + new Vector2(0.01f, 0)) * 4;
        if (!s.Besieged) { s.Besieged = true; s.SiegeSince = (float)S.Hours; }
        Log($"You lay siege to {s.Name}. Ladders will be ready by morning.");
    }

    public void LiftSiege()
    {
        if (Besieging is not Settlement s) return;
        Player.SiegeTarget = -1;
        if (!S.Parties.Any(p => p.SiegeTarget == s.Id && p != Player)) s.Besieged = false;
        Log($"You lift the siege of {s.Name}.");
    }

    /// Hours until your men have built ladders and a ram (0 = ready).
    public float LaddersIn(Settlement s) => MathF.Max(0, s.SiegeSince + 12 - (float)S.Hours);

    public BattleSetup PrepareAssault(Settlement s)
    {
        var g = GarrisonParty(s);
        var setup = PrepareBattle(g);
        setup.EnemyName = g.Name;
        setup.SiegeOf = s.Id;
        // Lords of the town's faction resting inside fight too (PrepareBattle adds anyone near and friendly to them).
        setup.Options.Siege = true;
        setup.Options.Ladders = LaddersIn(s) > 0 ? 0 : Player.Men >= 90 ? 4 : 2;
        setup.Options.GateHp = s.Kind == SettlementKind.Town ? 600 : 450;
        setup.Options.Ground = Map.BattleGround(s.Pos) with { Road = false };
        setup.Options.Palette[1] = Style.PaletteOf(s.Faction);
        return setup;
    }

    public BattleReport AutoResolveAssault(Settlement s)
    {
        var setup = PrepareAssault(s);
        var r = Rules.AutoResolve(setup.Side0, setup.Side1, rng, Captain.Perks.Contains(Perk.Tactician), wall: setup.Options.Ladders > 0 ? 1.4f : 1.7f);
        return FinishBattle(r, setup);
    }

    /// After an assault (played or on paper): a win takes the place; a defeat ends your siege.
    void AfterAssault(Settlement s, bool won)
    {
        if (won)
        {
            int taken = s.Garrison.Sum(c => c.Men + c.Wounded);
            foreach (var c in s.Garrison) TakePrisoners(c.Troop, (c.Men + c.Wounded) / 2);
            s.Garrison.Clear();
            Capture(s, Captain.Contract, Player.Id);
            Captain.Renown += s.Kind == SettlementKind.Town ? 25 : 15;
            if (taken > 0) Log($"{taken / 2} of the garrison are your prisoners.");
        }
        else
        {
            Player.SiegeTarget = -1;
            if (!S.Parties.Any(p => p.SiegeTarget == s.Id)) s.Besieged = false;
        }
    }

    // ---------- capture ----------

    /// `f` takes settlement s (and its province, with the villages): the new holder raises a small garrison.
    void Capture(Settlement s, int f, int owner)
    {
        int old = s.Faction;
        s.Faction = f;
        s.Owner = owner;
        s.Besieged = false;
        s.Supplies = 15;
        s.Offers.Clear();
        s.Mercenaries.Clear();
        S.Provinces[s.Province].Faction = f;
        foreach (var v in S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village)) { v.Faction = f; v.Owner = owner; }
        foreach (var p in S.Parties.Where(p => p.SiegeTarget == s.Id)) { p.SiegeTarget = -1; if (p.Kind == PartyKind.Lord) p.TaskUntil = 0; }
        RaiseGarrison(s, 0.4f);
        Score(f, old, 600);
        // Lords who lived there move to another of their faction's seats; with none left, the faction is broken.
        var seats = S.Settlements.Where(t => t.Faction == old && t.Kind != SettlementKind.Village).ToList();
        foreach (var l in S.Parties.Where(p => p.Kind == PartyKind.Lord && p.HomeId == s.Id))
        {
            if (seats.Count > 0) l.HomeId = seats.OrderBy(t => Vector2.Distance(t.Pos, l.Pos)).First().Id;
            else { l.Down = true; l.DownUntil = float.MaxValue; }
        }
        Log(owner == Player.Id ? $"{s.Name} is yours: the {FactionNames[f]} grant it to you as a fief."
            : $"The {FactionNames[f]} take {s.Name} from the {FactionNames[old]}.");
        if (!Alive(old)) Log($"The {FactionNames[old]} hold no town or castle: their realm is broken.");
        AudioEngine.Current?.Play(Cue.Horn);
    }

    // ---------- the lords' sieges ----------

    /// A lord campaigning in an enemy province besieges its seat when he and the friendly lords nearby outmatch the garrison.
    bool TryBesiege(Party p)
    {
        if (p.PatrolProvince < 0) return false;
        var seat = S.Settlements.FirstOrDefault(t => t.Province == p.PatrolProvince && t.Kind != SettlementKind.Village);
        if (seat == null || !FactionsAtWar(p.Faction, seat.Faction) || Vector2.Distance(p.Pos, seat.Pos) > 30 * Map.Spread) return false;
        var host = S.Parties.Where(q => q.Kind == PartyKind.Lord && !q.Down && q.Faction == p.Faction && Vector2.Distance(q.Pos, seat.Pos) < 30 * Map.Spread).ToList();
        if (host.Sum(Rules.Strength) < Rules.Strength(GarrisonParty(seat)) * 1.6f) return false;
        foreach (var q in host)
        {
            q.SiegeTarget = seat.Id;
            q.Task = "siege";
            q.ChaseId = -1;
            q.Goal = seat.Pos + new Vector2(rng.Range(-5, 5), rng.Range(-5, 5)) * Map.Spread;
            q.Moving = true;
        }
        if (!seat.Besieged)
        {
            seat.Besieged = true;
            seat.SiegeSince = (float)S.Hours;
            Log(seat.Owner == Player.Id ? $"Your fief {seat.Name} is besieged by the {FactionNames[p.Faction]}!" : $"The {FactionNames[p.Faction]} besiege {seat.Name}.");
        }
        return true;
    }

    /// Hourly: a siege you walk away from is over.
    void SiegeHourly()
    {
        if (Besieging is Settlement s && Vector2.Distance(Player.Pos, s.Pos) > 10) LiftSiege();
    }

    /// Daily: the besieged eat their stores, then starve; lords assault after two days.
    void SiegesDaily()
    {
        foreach (var s in S.Settlements.Where(s => s.Besieged))
        {
            var besiegers = S.Parties.Where(p => p.SiegeTarget == s.Id && !p.Down).ToList();
            if (besiegers.Count == 0) { s.Besieged = false; continue; }
            s.Supplies -= 1;
            if (s.Supplies <= 0)
            {
                s.Supplies = 0;
                foreach (var c in s.Garrison) c.Men -= (int)MathF.Ceiling(c.Men * 0.1f);
                s.Garrison.RemoveAll(c => c.Men + c.Wounded <= 0);
            }
            int f = besiegers[0] == Player ? Captain.Contract : besiegers[0].Faction;
            if (GarrisonMen(s) == 0 && f >= 0) { Log($"{s.Name}'s garrison is starved out."); if (besiegers.Contains(Player)) AfterAssault(s, true); else Capture(s, f, -1); continue; }
            var lords = besiegers.Where(p => p.Kind == PartyKind.Lord).ToList();
            if (lords.Count == 0 || S.Hours - s.SiegeSince < 48 || !rng.Chance(0.5f)) continue;
            var defenders = new List<Party> { GarrisonParty(s) };
            defenders.AddRange(S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && p.Faction == s.Faction && Vector2.Distance(p.Pos, s.Pos) < 8 * Map.Spread));
            var r = Rules.AutoResolve(lords, defenders, rng, false, wall: 1.4f);
            Apply(r, lords, defenders, "");
            if (r.Winner == 0) Capture(s, lords[0].Faction, -1);
            else
            {
                foreach (var l in lords) l.SiegeTarget = -1;
                if (!besiegers.Contains(Player)) s.Besieged = false;
                Log($"The assault on {s.Name} is thrown back.");
            }
        }
    }
}
