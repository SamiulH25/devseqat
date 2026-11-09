using Microsoft.Xna.Framework;

namespace Ironfield;

/// You and a knight order (docs/KNIGHT_ORDERS.md 4.4-4.5, K4): standing earned in the field, the trial of entry, the oath,
/// rising through the ranks, the creed's vows, penance and casting out, the chapter's brothers and training, order contracts.
public sealed partial class Campaign
{
    public static readonly string[] OrderRanks = { "Squire", "Knight-Brother", "Knight-Captain", "Knight-Commander", "Grand Master" };
    /// What each rank needs (index = rank): aura tier, standing with the order, order deeds done.
    public static readonly int[] RankAura = { 0, 1, 2, 3, 4 }, RankStanding = { 0, 30, 50, 70, 90 }, RankDeeds = { 0, 0, 1, 2, 3 };
    /// Standing an order needs before it offers you its trial; and what breaking a vow costs.
    public const float TrialStanding = 10, VowCost = 15;
    /// Orders that never take back one who left them (4.5).
    static readonly string[] NeverForgive = { "wolf", "sky" };
    /// Brothers the chapter spares you at a time, and how often (days).
    public const int LevyMen = 4;
    public const float LevyDays = 7;

    /// The trial you asked for at a chapter house, for the map screen to start (as `Lists` starts a tournament).
    public string? TrialPending;

    public OrderDef? MyOrder => Captain.Order == "" ? null : KnightOrders.Find(Captain.Order);
    public OrderState? MyOrderState => Captain.Order == "" ? null : OrderById(Captain.Order);
    public string OrderRankName => OrderRanks[Math.Clamp(Captain.OrderRank, 0, OrderRanks.Length - 1)];
    public float StandingWith(string order) => OrderById(order)?.Standing ?? 0;
    bool SwornTo(string order, int rank) => Captain.Order == order && Captain.OrderRank >= rank;

    internal void RaiseStanding(string order, float by)
    {
        if (OrderById(order) is OrderState o) o.Standing = Math.Clamp(o.Standing + by, -100, 100);
    }

    /// The order whose chapter house is this town, if any.
    public OrderState? ChapterAt(Settlement s) => S.Orders.FirstOrDefault(o => !o.Gone && o.Chapter == s.Id);

    // ---- the trial and the oath ----

    /// Why this order will not set you its trial, or null.
    public string? CannotSeekTrial(string order)
    {
        var o = OrderById(order);
        if (o == null || o.Gone) return "THAT ORDER IS GONE";
        if (S.OwnOrder != null) return "YOU LEAD AN ORDER OF YOUR OWN";
        string name = KnightOrders.Get(order).Short.ToUpperInvariant();
        if (Captain.Order == order) return "YOU ARE SWORN TO IT ALREADY";
        if (Captain.Order != "") return $"YOU ARE SWORN TO {MyOrder!.Short.ToUpperInvariant()}";
        if (Captain.Forsworn.Contains(order) && NeverForgive.Contains(order)) return $"{name} NEVER TAKE BACK ONE WHO LEFT THEM";
        if (FactionsAtWar(Captain.Contract, o.Patron)) return $"YOU SERVE A REALM AT WAR WITH {FactionNames[o.Patron].ToUpperInvariant()}";
        float need = TrialStanding + (Captain.Forsworn.Contains(order) ? 20 : 0);
        if (o.Standing < need) return $"{name} DO NOT KNOW YOU YET (STANDING {o.Standing:0} OF {need:0})";
        if (S.Hours < Captain.WoundedUntil) return "YOU ARE TOO HURT TO FIGHT";
        return null;
    }

    /// The order's knight who tests you: a knight-brother, more than a match for a green captain.
    public static UnitType TrialKnight()
    {
        var t = Troops.Get("captain").Clone();
        t.MaxHp += 24;
        t.Damage += 4;
        t.Armor += 2;
        t.Look = t.Look with { Head = "greathelm", Body = "plate", Legs = "plate", Feet = "sabatons" };
        return t;
    }

    /// The trial of entry: a duel in the chapter's yard with one of its knights.
    public BattleSetup PrepareTrial(string order)
    {
        var def = KnightOrders.Get(order);
        var o = OrderById(order)!;
        var setup = new BattleSetup { EnemyName = $"A knight of {def.Short}", Trial = order };
        setup.Seed = HashCode.Combine(S.Seed, (int)(S.Hours * 4), order.GetHashCode(StringComparison.Ordinal) & 0xffff, 93);
        setup.Player = new ArmySpec("You", 0, new List<Company>());
        setup.Enemy = new ArmySpec(setup.EnemyName, 0, new List<Company>());
        int mine = Style.PaletteOf(Math.Max(0, Captain.Contract)), his = Style.PaletteOf(o.Patron);
        setup.Options = new BattleOptions
        {
            Duel = true, Couriers = false, Cap = 10, Scouted = true,
            EnemyLeader = TrialKnight(), EnemyDamage = Settings.Current.EnemyDamage,
            Ground = TerrainParams.Lists, Weather = new Weather(11),
            Palette = new[] { mine, his == mine ? 3 : his },
        }.WithCaptain(Captain);
        return setup;
    }

    /// Without a fight (the stand-in captain, or a quick resolve): your skill at arms and your aura decide.
    public BattleReport AutoTrial(string order)
    {
        bool won = rng.Chance(0.25f + Skill("Arms") / 250f + 0.1f * AuraInBattle);
        return FinishTrial(new BattleResult(won ? 0 : 1, 0, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Unhurt, 0), order);
    }

    /// After the trial: won, you swear the oath; lost, they think a little better of you for trying, and you nurse a wound.
    BattleReport FinishTrial(BattleResult r, string order)
    {
        bool won = r.Winner == 0;
        var def = KnightOrders.Get(order);
        var skillsBefore = new Dictionary<string, float>(Captain.Skills);
        Practise("Arms", (won ? 2 : 1) + (r.Parries + r.Ripostes) * 0.15f);
        AuraAfterDuel(r, won, $"a knight of {def.Short}");
        FormsAfterBattle(r);
        if (won) Swear(order);
        else
        {
            RaiseStanding(order, 2);
            Captain.WoundedUntil = MathF.Max(Captain.WoundedUntil, (float)S.Hours + 24 * 2);
            Log($"The knight of {def.Short} puts you down in the yard. You may try again when you are healed.");
        }
        var fate = won ? LeaderFate.Unhurt : LeaderFate.Wounded;
        Report = new BattleReport(won, $"The trial of {def.Short}", new List<CompanyReport>(), 0, 0, 0, 0, 0, fate) { Rises = Skills.Rises(Captain, skillsBefore), Aura = TakeAuraNews() };
        return Report;
    }

    /// Swear the order's oath: you are its squire, and learn its first form.
    internal void Swear(string order)
    {
        var def = KnightOrders.Get(order);
        Captain.Order = order;
        Captain.OrderRank = 0;
        Captain.Penance = false;
        Captain.Forsworn.Remove(order);
        if (OrderById(order) is OrderState o) o.Standing = MathF.Max(o.Standing, TrialStanding);
        LearnOrderForms();
        DrawRival(def);
        Deed($"You take the oath of {def.Short} as its squire. {def.Creed}", "oaths");
    }

    /// The next rank's needs not yet met, or null if you may rise.
    public string? CannotRise()
    {
        if (MyOrderState is not OrderState o) return "YOU ARE SWORN TO NO ORDER";
        int next = Captain.OrderRank + 1;
        if (next >= OrderRanks.Length - 1) return "THE GRAND MASTER IS CHOSEN, NOT MADE"; // K6
        if (Captain.Penance) return "ATONE FOR YOUR BROKEN VOW FIRST";
        if (Captain.AuraTier < RankAura[next]) return $"NEEDS AURA: {AuraName(RankAura[next]).ToUpperInvariant()}";
        if (o.Standing < RankStanding[next]) return $"NEEDS STANDING {RankStanding[next]} (YOU HAVE {o.Standing:0})";
        if (Captain.OrderDeeds < RankDeeds[next]) return $"NEEDS {RankDeeds[next]} ORDER DEEDS (YOU HAVE {Captain.OrderDeeds})";
        return null;
    }

    /// Rise a rank in your order (at its chapter house); returns null or why not.
    public string? RiseInOrder()
    {
        if (CannotRise() is string why) return why;
        Captain.OrderRank++;
        LearnOrderForms();
        Captain.Renown += 5 * Captain.OrderRank;
        Deed($"{char.ToUpperInvariant(MyOrder!.Short[0])}{MyOrder.Short[1..]} name you {OrderRankName}.", "oaths");
        return null;
    }

    /// The order's forms by rank: its first as a squire, its second as a knight-captain, its great form as a knight-commander.
    void LearnOrderForms()
    {
        if (Captain.Order == "") return;
        var forms = Forms.OfOrder(Captain.Order).ToList();
        int known = Captain.OrderRank >= 3 ? 3 : Captain.OrderRank >= 2 ? 2 : 1;
        foreach (var f in forms.Take(known)) LearnForm(f.Id);
    }

    /// Leave your order: it and its friends think the worse of you, and some never take you back.
    public string? LeaveOrder()
    {
        if (MyOrder is not OrderDef def) return "YOU ARE SWORN TO NO ORDER";
        RaiseStanding(def.Id, -40);
        Captain.Forsworn.Add(def.Id);
        Captain.Order = "";
        Captain.OrderRank = 0;
        Captain.Penance = false;
        Log(NeverForgive.Contains(def.Id) ? $"You leave {def.Name}. {def.Short} will not forget it." : $"You leave {def.Name}.");
        return null;
    }

    // ---- the creed ----

    /// Does your creed hold this vow?
    public bool Vowed(string vow) => MyOrder?.Vows.Contains(vow) == true || S.OwnOrder?.Vows.Contains(vow) == true;

    /// You did what your creed forbids: standing falls and you owe a penance; break a vow again before atoning and you are
    /// cast out.
    internal void BreakVow(string vow, string what)
    {
        if (S.OwnOrder?.Vows.Contains(vow) == true) OwnVowBroken(what); // your own creed (K8)
        if (MyOrder?.Vows.Contains(vow) != true || MyOrder is not OrderDef def) return;
        RaiseStanding(def.Id, -VowCost);
        if (Captain.Penance)
        {
            Captain.Forsworn.Add(def.Id);
            Captain.Order = "";
            Captain.OrderRank = 0;
            Captain.Penance = false;
            RaiseStanding(def.Id, -25);
            Deed($"You {what} again, and {def.Name} casts you out.", "oaths");
            return;
        }
        Captain.Penance = true;
        Log($"You {what}, against your oath to {def.Short}. Win a battle in its name to atone.");
    }

    /// After a battle with you in it: standing with the orders whose foes you beat or whose armies you fought, a penance
    /// atoned for, and an order deed for a great victory over what your order hunts.
    internal void OrderAfterBattle(bool won, List<Party> foes)
    {
        foreach (var o in S.Orders.Where(o => !o.Gone))
        {
            var def = KnightOrders.Get(o.Id);
            if (foes.Any(f => f.KnightOrder == o.Id)) { RaiseStanding(o.Id, -10); continue; }
            if (!won) continue;
            bool hunted = foes.Any(f => Hunted(def, f));
            bool patronsFoe = foes.Any(f => FactionsAtWar(o.Patron, SideOf(f)));
            if (hunted) RaiseStanding(o.Id, 3);
            else if (patronsFoe) RaiseStanding(o.Id, 2);
        }
        CrusadeWon(won, foes);
        if (!won || MyOrder is not OrderDef mine) return;
        bool inName = foes.Any(f => Hunted(mine, f) || FactionsAtWar(MyOrderState!.Patron, SideOf(f)));
        if (Captain.Penance && inName) { Captain.Penance = false; Log($"Your penance is done. {mine.Short} welcome you back."); }
        if (foes.Any(f => Hunted(mine, f)) && foes.Sum(f => f.Men) >= Scaled(30)) { Captain.OrderDeeds++; Log($"An order deed: a host of {mine.Short}'s foes broken."); }
    }

    /// Is this party what the order hunts?
    static bool Hunted(OrderDef def, Party f) => def.Hunts switch
    {
        "undead" => f.Breed == "undead",
        "monsters" => f.Kind == PartyKind.Monster,
        "bandits" => f.Kind == PartyKind.Bandit,
        _ => false,
    };

    // ---- the chapter house ----

    /// Brothers from the chapter: a few of its signature troop by your rank (Knight-Brother: tier IV, Captain: V, Commander: VI).
    public string? RecruitBrothers(Settlement s)
    {
        if (ChapterAt(s) is not OrderState o || o.Id != Captain.Order) return "NOT YOUR ORDER'S CHAPTER";
        if (Captain.OrderRank < 1) return "A SQUIRE CANNOT TAKE BROTHERS: RISE TO KNIGHT-BROTHER";
        if (S.Hours < o.NextLevy) return $"THE CHAPTER HAS NO BROTHERS TO SPARE FOR {(o.NextLevy - S.Hours) / 24:0} DAYS";
        string troop = BrotherTroop;
        int price = BrothersPrice;
        if (S.Gold < price) return $"NEEDS {price} CROWNS";
        string role = Rules.TroopRole(troop);
        var into = Player.Companies.FirstOrDefault(c => c.Troop == troop && Rules.Room(c, troop) >= Scaled(LevyMen));
        if (into == null && Player.Companies.Count >= Slots) return "NO FREE COMPANY";
        if (Scaled(LevyMen) > RoomForMen) return "PARTY IS FULL";
        S.Gold -= price;
        if (into == null) Player.Companies.Add(NewCompany(troop, role, Scaled(LevyMen), 0, s));
        else Enlist(into, troop, Scaled(LevyMen), s);
        o.NextLevy = (float)S.Hours + LevyDays * 24;
        Log($"{Scaled(LevyMen)} brothers of {MyOrder!.Short} ride with you.");
        return null;
    }

    public int BrotherMen => Scaled(LevyMen);
    public string BrotherTroop => MyOrder is OrderDef d ? d.Troops[Math.Clamp(Captain.OrderRank - 1, 0, 2)] : "";
    public int BrothersPrice => BrotherTroop == "" ? 0 : (int)(Scaled(LevyMen) * Rules.PerMan(20 * Troops.Get(BrotherTroop).Tier));

    /// An order contract from the chapter: destroy the nearest of what it hunts (or an enemy of its patron) within 15 days.
    public string? TakeOrderContract(Settlement s)
    {
        if (ChapterAt(s) is not OrderState o || o.Id != Captain.Order) return "NOT YOUR ORDER'S CHAPTER";
        if (S.Contracts.Any(c => c.Order == o.Id)) return "YOU HAVE ITS CONTRACT ALREADY";
        var def = KnightOrders.Get(o.Id);
        var target = S.Parties.Where(p => !p.Down && (Hunted(def, p) || p.Kind == PartyKind.Lord && FactionsAtWar(o.Patron, p.Faction)))
            .OrderBy(p => Vector2.Distance(p.Pos, s.Pos)).FirstOrDefault();
        if (target == null) return "THE ORDER HAS NO FOE FOR YOU NOW";
        S.Contracts.Add(new Contract
        {
            Id = S.NextId++, Kind = target.Kind == PartyKind.Lord ? "hunt" : "clear", Target = target.Id, Faction = -1, Order = o.Id,
            Deadline = (float)S.Hours + 15 * 24, Reward = 100, Text = $"Destroy {target.Name} for {def.Short}.",
        });
        Log($"{def.Short} charge you: destroy {target.Name}.");
        return null;
    }

    /// An order contract done: standing and an order deed; failed, a vow broken if your creed holds contracts holy.
    void OrderContractDone(Contract c)
    {
        if (c.Order == "") return;
        RaiseStanding(c.Order, 8);
        if (c.Order == Captain.Order) Captain.OrderDeeds++;
    }

    void OrderContractLost(Contract c)
    {
        if (c.Order != "") RaiseStanding(c.Order, -5);
        BreakVow("contract", "fail a contract");
    }

    /// Your realm at war with your order's patron ends the oath (not forsworn: no blame on either side).
    void OathAndWar()
    {
        if (MyOrderState is not OrderState o || !FactionsAtWar(Captain.Contract, o.Patron)) return;
        Log($"Your realm is at war with {FactionNames[o.Patron]}. Your oath to {MyOrder!.Short} is ended.");
        Captain.Order = "";
        Captain.OrderRank = 0;
        Captain.Penance = false;
    }

    /// Resting at your chapter house trains your aura (a squire's days in the yard).
    void ChapterTraining()
    {
        if (!Player.Resting || MyOrderState is not OrderState o || SettlementById(o.Chapter) is not Settlement ch) return;
        if (Vector2.Distance(Player.Pos, ch.Pos) < 6 * Map.Spread) GainAura(1.5f);
    }
}
