using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap phase B campaign systems: prisoners (B3), officers (B5), seasons and food (B7), contracts (B4).
public sealed partial class Campaign
{
    // ---------- prisoners (B3) ----------

    public void TakePrisoners(string troop, int n)
    {
        if (n <= 0) return;
        var g = Player.Prisoners.FirstOrDefault(p => p.Troop == troop);
        if (g == null) Player.Prisoners.Add(g = new PrisonerGroup { Troop = troop });
        g.Count += n;
    }

    public int PrisonerCount => Player.Prisoners.Sum(g => g.Count);

    /// Turn prisoners into a company of their own troop: green, and with no love for you (cohesion 0).
    public string? RecruitPrisoners(PrisonerGroup g)
    {
        int room = Rules.MenLimit(Captain) - Player.Men - Player.Wounded;
        if (room <= 0) return "PARTY IS FULL";
        string role = Rules.TroopRole(g.Troop);
        int n = Math.Min(g.Count, Math.Min(room, Rules.CapOf(g.Troop, role)));
        if (Player.Companies.Count >= Rules.CompanySlots(Captain)) return "NO FREE COMPANY";
        var c = NewCompany(g.Troop, role, n, 0);
        c.Cohesion = 0;
        c.Name = "Turncoat " + c.Name.Split(' ').Last();
        Player.Companies.Add(c);
        g.Count -= n;
        Player.Prisoners.RemoveAll(p => p.Count <= 0);
        Log($"{n} prisoners take your coin as {c.Name}.");
        return null;
    }

    public int PrisonerValue => (int)(Player.Prisoners.Sum(g => g.Count * Troops.Get(g.Troop).Cost * B.RansomRate) * (Has("RansomBroker") ? 1.5f : 1));

    public string? SellPrisoners(Settlement s)
    {
        if (PrisonerCount == 0) return "NO PRISONERS";
        if (s.Kind == SettlementKind.Village) return "ONLY TOWNS AND CASTLES BUY PRISONERS";
        int gold = PrisonerValue;
        S.Gold += gold;
        Log($"You sell {PrisonerCount} prisoners for {gold} crowns.");
        Player.Prisoners.Clear();
        AudioEngine.Current?.Play(Cue.Coin);
        return null;
    }

    public void FreePrisoners()
    {
        Captain.Renown += PrisonerCount * 0.5f;
        Log($"You free {PrisonerCount} prisoners. Word of your mercy spreads.");
        Player.Prisoners.Clear();
    }

    public int LordRansom(Party lord) => (int)((300 + 200 * lord.Tier) * (Has("RansomBroker") ? 1.5f : 1));

    public string? RansomLord(Party lord, Settlement s)
    {
        if (s.Kind == SettlementKind.Village) return "ONLY TOWNS AND CASTLES PAY RANSOMS";
        S.Gold += LordRansom(lord);
        Practise("Charm", LordRansom(lord) / 100f);
        if (CharacterOf(lord) is Character ch) Remember(ch, Me, "you sold me back for gold", -10);
        Release(lord);
        Log($"{lord.Name} is ransomed for {LordRansom(lord)} crowns.");
        AudioEngine.Current?.Play(Cue.Coin);
        return null;
    }

    public void FreeLord(Party lord)
    {
        Captain.Renown += 10;
        if (CharacterOf(lord) is Character ch) Remember(ch, Me, "you set me free without ransom", 25);
        Release(lord);
        Log($"You release {lord.Name} without ransom. Your honour is spoken of.");
    }

    void Release(Party lord)
    {
        lord.Captive = false;
        lord.DownUntil = (float)S.Hours + 72;
        Player.Captives.Remove(lord.Id);
    }

    /// Weekly: prisoners eat and may slip away if they outnumber their guards.
    void PrisonersWeekly()
    {
        foreach (var g in Player.Prisoners)
            if (g.Count > Player.Men / 2) g.Count -= (int)MathF.Ceiling(g.Count * 0.1f);
        Player.Prisoners.RemoveAll(g => g.Count <= 0);
    }

    // ---------- officers (B5) ----------

    public static readonly Dictionary<string, string> TraitText = new()
    {
        ["Stubborn"] = "ROUTS ONLY BELOW 10 MORALE", ["Steady"] = "+8 STARTING MORALE", ["Drillmaster"] = "+1 DRILL XP A DAY",
        ["Hothead"] = "+10% DAMAGE", ["Sharpshooter"] = "ARROWS FLY TRUER", ["Surgeon"] = "+10% OF FALLEN SURVIVE", ["Veteran"] = "+5% DAMAGE",
    };

    CompanyRecord WithOfficer(CompanyRecord c)
    {
        if (c.Officer == "" && rng.Chance(0.6f)) c.Officer = "Sgt " + rng.Pick(Names.Get().Lords);
        return c;
    }

    internal void OfficerAfterBattle(CompanyRecord c, bool won, float lostFrac)
    {
        if (c.Officer == "") { if (rng.Chance(Player.Companies.Contains(c) && Has("Sergeants") ? 0.8f : 0.4f)) { c.Officer = "Sgt " + rng.Pick(Names.Get().Lords); c.OfficerBattles = 0; } return; }
        if (lostFrac >= 0.5f && rng.Chance(0.25f))
        {
            if (Player.Companies.Contains(c)) Log($"{c.Officer} of {c.Name} has fallen.");
            c.Officer = "";
            c.Traits.Clear();
            c.OfficerBattles = 0;
            return;
        }
        c.OfficerBattles++;
        if (c.OfficerBattles >= CompanionBattles) OfficerToCompanion(c);
        if (won && c.Traits.Count < 3 && rng.Chance(0.15f))
        {
            var t = rng.Pick(TraitText.Keys.Where(k => !c.Traits.Contains(k)).ToList());
            c.Traits.Add(t);
            if (Player.Companies.Contains(c)) Log($"{c.Officer} of {c.Name} is now {t.ToLowerInvariant()}.");
        }
    }

    // ---------- seasons and food (B7) ----------

    public static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };
    public int SeasonIndex => (Day - 1) / 30 % 4;
    public string Season => Seasons[SeasonIndex];
    public bool Winter => SeasonIndex == 3;

    int Mouths => Player.Men + Player.Wounded + PrisonerCount;
    public int FoodPrice(int days) => (int)MathF.Ceiling(Mouths * days * 0.15f * (Has("Provisioner") ? 0.75f : 1)
        * (SettlementAt(Player.Pos) is Settlement here && Level(SeatOf(here), "market") > 0 ? 0.8f : 1));

    public string? BuyFood(int days)
    {
        int price = FoodPrice(days);
        if (S.Gold < price) return $"NEEDS {price} CROWNS";
        S.Gold -= price;
        Player.Food += days;
        return null;
    }

    /// Take food from a village by force: a week's worth for free, but the village gives no recruits for a week.
    /// Your liege's villages cost you renown; an enemy's count as a raid in the war.
    public string? Forage(Settlement v)
    {
        if (v.Kind != SettlementKind.Village) return "ONLY VILLAGES";
        if (v.ThreatUntil > S.Hours) return "ALREADY STRIPPED BARE";
        Player.Food += 7;
        v.ThreatUntil = (float)S.Hours + 7 * 24;
        Raided(v);
        v.Recruits = 0;
        if (Captain.Contract >= 0 && v.Faction == Captain.Contract)
        {
            Captain.Renown = MathF.Max(0, Captain.Renown - 5);
            Log($"You strip {v.Name} of its stores. Your liege's people will remember.");
        }
        else
        {
            if (Captain.Contract >= 0) Score(Captain.Contract, v.Faction, 20);
            Log($"You raid {v.Name} for food.");
        }
        return null;
    }

    /// Daily: your company eats (more in winter). Starving companies lose cohesion and men.
    void EatDaily()
    {
        // Your liege's towns and castles feed sworn companies; elsewhere in friendly country the quartermaster buys a week's worth.
        var here = SettlementAt(Player.Pos);
        if (here != null && Captain.Contract >= 0 && here.Faction == Captain.Contract && here.Kind != SettlementKind.Village) Player.Food = MathF.Max(Player.Food, 7);
        else if (Player.Food < 4 && here != null && !Refuses(here) && S.Gold >= FoodPrice(7))
        {
            BuyFood(7);
            Log($"Your quartermaster buys a week of food in {here.Name}.");
        }
        Player.Food -= (Winter ? 1.5f : 1) * (HasCompanion("Quartermaster") ? 0.85f : 1) * (Has("Forager") ? 0.8f : 1);
        if (Player.Food >= 0) return;
        Player.Food = 0;
        foreach (var c in Player.Companies)
        {
            c.Cohesion = MathF.Max(0, c.Cohesion - 5);
            c.RemoveFit((int)MathF.Ceiling(c.Men * 0.03f), true);
            c.Prune();
        }
        Player.Companies.RemoveAll(c => c.Men + c.Wounded <= 0);
        Log("Your men are starving. Buy food in a town or village.");
    }

    // ---------- contracts (B4) ----------

    /// The steward's board in a town: refreshed weekly, offers work for the faction that holds it.
    public List<Contract> Offers(Settlement s)
    {
        if (s.Kind != SettlementKind.Town) return new List<Contract>();
        s.Offers.RemoveAll(o => o.Deadline < S.Hours || !OfferValid(o));
        if (s.Offers.Count == 0) RefreshOffers(s);
        return s.Offers;
    }

    bool OfferValid(Contract o) => o.Kind switch
    {
        "hunt" or "clear" => PartyById(o.Target) is { Down: false },
        "take" => S.Settlements[o.Target].Faction != o.Faction,
        _ => true,
    };

    void RefreshOffers(Settlement s)
    {
        int f = s.Faction;
        float deadline = (float)S.Hours + 10 * 24;
        Contract Make(string kind, int target, float reward, string text) => new() { Id = S.NextId++, Kind = kind, Target = target, Faction = f, Deadline = deadline, Reward = reward, Text = text };
        if (S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && FactionsAtWar(f, p.Faction)).OrderBy(p => Vector2.Distance(p.Pos, s.Pos)).FirstOrDefault() is Party lord)
            s.Offers.Add(Make("hunt", lord.Id, 300 + Rules.Strength(lord), $"Defeat {lord.Name} in the field."));
        if (S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down).OrderBy(p => Vector2.Distance(p.Pos, s.Pos)).FirstOrDefault() is Party band)
            s.Offers.Add(Make("clear", band.Id, 150 + Rules.Strength(band) / 2, $"Destroy the {band.Name}."));
        var frontVillage = S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.Faction == f && S.Provinces[v.Province].Neighbors.Any(n => S.Provinces[n].Faction != f))
            .OrderBy(_ => rng.Float()).FirstOrDefault();
        if (frontVillage != null) s.Offers.Add(Make("defend", frontVillage.Id, 220, $"Guard {frontVillage.Name} for a day."));
        var enemyTown = S.Settlements.Where(t => t.Kind == SettlementKind.Town && FactionsAtWar(f, t.Faction)).OrderBy(t => Vector2.Distance(t.Pos, s.Pos)).FirstOrDefault();
        if (enemyTown != null) s.Offers.Add(Make("scout", enemyTown.Id, 160, $"Scout {enemyTown.Name} (ride within sight of it)."));
        var castle = S.Settlements.Where(t => t.Kind != SettlementKind.Village && FactionsAtWar(f, t.Faction)).OrderBy(t => Vector2.Distance(t.Pos, s.Pos)).FirstOrDefault();
        if (castle != null) s.Offers.Add(Make("take", castle.Id, 1200, $"Take {castle.Name} for the {FactionNames[f]}."));
    }

    public string? Accept(Settlement s, Contract c)
    {
        if (Captain.Contract != c.Faction) return "SIGN WITH THIS FACTION FIRST";
        if (S.Contracts.Count >= 3) return "YOU ALREADY HOLD THREE CONTRACTS";
        s.Offers.Remove(c);
        S.Contracts.Add(c);
        Log($"Contract: {c.Text}");
        return null;
    }

    /// Hourly: progress, completion and failure of your contracts.
    void CheckContracts()
    {
        foreach (var c in S.Contracts.ToList())
        {
            bool done = c.Kind switch
            {
                "hunt" or "clear" => S.LastBattleFoe == c.Target || (c.Kind == "clear" && PartyById(c.Target) == null && S.LastBattleFoe == c.Target),
                "defend" => (c.Progress += Vector2.Distance(Player.Pos, S.Settlements[c.Target].Pos) < 8 * Map.Spread ? 1 : 0) >= 24,
                "scout" => Vector2.Distance(Player.Pos, S.Settlements[c.Target].Pos) < 15 * Map.Spread,
                "take" => S.Settlements[c.Target].Faction == c.Faction,
                _ => false,
            };
            if (done)
            {
                S.Gold += c.Reward;
                Captain.Renown += 5;
                S.Contracts.Remove(c);
                if (CharacterById(c.Giver) is Character giver) Remember(giver, Me, "you did what I asked of you", 15);
                Log($"Contract done: {c.Text} (+{c.Reward:0} crowns).");
                AudioEngine.Current?.Play(Cue.Coin);
            }
            else if (S.Hours > c.Deadline || (c.Kind is "hunt" or "clear" && PartyById(c.Target) is null or { Down: true } && S.LastBattleFoe != c.Target))
            {
                Captain.Renown = MathF.Max(0, Captain.Renown - 5);
                S.Contracts.Remove(c);
                if (CharacterById(c.Giver) is Character giver) Remember(giver, Me, "you failed me", -8);
                Log($"Contract failed: {c.Text}");
            }
        }
    }
}
