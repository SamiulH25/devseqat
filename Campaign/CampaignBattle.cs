using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: battles with you (PrepareBattle, FinishBattle, auto-resolve, retreat, parley) and applying any battle's result (Apply).
public partial class Campaign
{
    // ---------- applying a battle ----------

    /// Apply a battle's result to the campaign (played or auto-resolved). Returns the report for the results screen.
    public BattleReport Apply(BattleResult r, IReadOnlyList<Party> side0, IReadOnlyList<Party> side1, string enemyName, bool siegeBattle = false)
    {
        var lookup = new Dictionary<int, (Party P, CompanyRecord C, int Team)>();
        foreach (var p in side0) foreach (var c in p.Companies) lookup[c.Id] = (p, c, 0);
        foreach (var p in side1) foreach (var c in p.Companies) lookup[c.Id] = (p, c, 1);
        float startStrength0 = side0.Sum(Rules.Strength), startStrength1 = side1.Sum(Rules.Strength);
        var before = side0.Concat(side1).ToDictionary(p => p, p => p.Men);
        bool playerIn = side0.Contains(Player) || side1.Contains(Player);
        int playerTeam = side0.Contains(Player) ? 0 : 1;
        bool won = r.Winner == playerTeam;
        float loot = 0, ransom = 0;
        var reports = new List<CompanyReport>();
        rollNews.Clear();
        rollFell.Clear();
        // L5: the battle's name, and whether a win here is worth a battle honour (outnumbered, their captain slain, a wall taken).
        string field = playerIn ? BattleName(Player.Pos, siegeBattle) : "";
        float mine = playerTeam == 0 ? startStrength0 : startStrength1, theirs = playerTeam == 0 ? startStrength1 : startStrength0;
        bool notable = playerIn && won && (theirs >= 1.5f * MathF.Max(1, mine) || r.EnemyLeader == LeaderFate.Slain || siegeBattle);
        int foeFaction = (playerTeam == 0 ? side1 : side0).Select(SideOf).FirstOrDefault(f => f >= 0, -1);

        foreach (var cr in r.Companies)
        {
            if (!lookup.TryGetValue(cr.Id, out var e)) continue;
            var (p, c, team) = e;
            bool teamWon = r.Winner == team;
            int menBefore = c.Men;
            string foe = team == 0 ? side1.FirstOrDefault()?.Name ?? "" : side0.FirstOrDefault()?.Name ?? "";
            float surgeon = (p == Player ? Skill("Medicine") / 1000f + (Has("Surgeon") ? 0.1f : 0) : 0) + (c.Traits.Contains("Surgeon") ? 0.1f : 0);
            int killedAll = 0, woundedAll = 0, capturedAll = 0, fledLostAll = 0;
            // Casualties come off each stack by its own armour (2.3); a battle without a breakdown counts as the company's face.
            var parts = cr.Stacks ?? new[] { new StackResult(c.Troop, cr.Start, cr.Standing, cr.Fled, cr.Fallen) };
            foreach (var sr in parts)
            {
                var st = c.Stacks.FirstOrDefault(x => x.Troop == sr.Troop);
                if (st == null) continue;
                var (killed, wounded, captured, fledBack) = StackLosses(p, c, cr, sr, st, teamWon, surgeon, foe);
                killedAll += killed; woundedAll += wounded; capturedAll += captured; fledLostAll += sr.Fled - fledBack;
                if (playerIn && team != playerTeam && won)
                {
                    loot += (sr.Fallen + sr.Fled) * Rules.PerMan(st.Type.Cost) * B.LootRate;
                    if (!st.Type.Wild || st.Type.Breed == "goblin" && !st.Type.Has("boss")) // of the creatures, only goblins are taken alive (as thralls)
                        TakePrisoners(st.Troop, Has("Pursuit") ? (int)MathF.Round(captured * 1.25f) : captured); // held, not ransomed at once (B3)
                }
            }
            // XP into each stack's pool: participation per surviving man, and the company's kill XP split by men (2.2).
            float xpPerMan = 0;
            if (cr.Deployed && c.Men > 0)
            {
                float killXp = 0;
                for (int t = 0; t < cr.KillsByTier.Length; t++) killXp += cr.KillsByTier[t] * Math.Max(1, t) * B.XpPerKillTier;
                xpPerMan = B.XpParticipation * (teamWon ? B.XpVictory : 1) + killXp / c.Men;
                int men = c.Men;
                foreach (var st in c.Stacks) st.AddXp(st.Men * (B.XpParticipation * (teamWon ? B.XpVictory : 1)) + killXp * st.Men / men);
            }
            if (cr.Deployed) { c.Battles++; c.Kills += cr.Kills; }
            float lostFrac = menBefore > 0 ? (menBefore - c.Men) / (float)menBefore : 0;
            c.Cohesion = Math.Clamp(c.Cohesion + (teamWon ? 5 : -5) - lostFrac * 10, 0, 30);
            if (cr.Deployed) OfficerAfterBattle(c, teamWon, lostFrac, foe);
            if (cr.HeroFell && p == Player) HeroFell(c, foe);
            Chronicle(c, cr, teamWon, menBefore - c.Men, foe, siegeBattle, p, field);
            if (p == Player) BannerAfterBattle(c, cr, teamWon, notable, field, foeFaction);
            if (playerIn) reports.Add(new CompanyReport(c.Id, c.Name, c.Troop, team == playerTeam ? 0 : 1, menBefore, c.Men, killedAll, woundedAll, capturedAll,
                fledLostAll, xpPerMan, Rules.UpgradeReady(c), cr.Deployed));
            c.Prune();
            if (p == Player) SyncRoll(c, true, foe);
        }
        foreach (var p in side0.Concat(side1)) p.Companies.RemoveAll(c => c.Men + c.Wounded <= 0);
        if (playerIn)
        {
            if (r.Duration > 0) RelicSpent(); // a fought battle (an auto-resolve has no length)
            CompanionsAfterBattle(r, playerTeam); // XP, levels, bonds (Callings.cs, Bonds.cs)
            CompanionsReact(won ? "won" : "lost");
            if (r.Omen > 0) { S.Omen += r.Omen; CompanionsReact("craft"); } // craft draws Omen (C2)
            FormsAfterBattle(r);
        }

        // Aftermath for each party.
        var winners = r.Winner == 0 ? side0 : side1;
        var losers = r.Winner == 0 ? side1 : side0;
        float lostStrength = (r.Winner == 0 ? startStrength1 - side1.Sum(Rules.Strength) : startStrength0 - side0.Sum(Rules.Strength));
        foreach (var p in winners) { p.Cooldown = 6; if (p.Kind is PartyKind.Lord) p.Gold += Rules.PerMan(lostStrength * 2); }
        foreach (var p in losers)
        {
            p.Cooldown = 12;
            p.ChaseId = -1;
            if (p.Kind == PartyKind.Garrison) continue;
            if (p.Kind == PartyKind.Monster)
            {
                p.Down = true;
                if (p.Breed == "troll") Log($"{p.Name} is slain near {Map.ProvinceAt(p.Pos).Name}.");
                if (p.Breed == "undead") Log($"{p.Name} are laid to rest.");
                if (p.Lair) { var taker = winners.FirstOrDefault(w => w.Kind is PartyKind.Player or PartyKind.Lord); if (taker != null && taker != Player) taker.Gold += p.Gold; HoldoutCleared(p, taker); }
                continue;
            }
            if (p.Kind == PartyKind.Caravan) { p.Down = true; CaravanLost(p); var robber = winners.FirstOrDefault(w => w != Player); if (robber != null) robber.Gold += p.Gold; continue; }
            if (p.Kind == PartyKind.Bandit) { p.Down = true; if (p.HomeId >= 0) RebelsBeaten(p); if (p.CharId >= 0) ThreatFalls(p, playerIn && won); continue; }
            if (p.Kind == PartyKind.Lord && (p.Men < before[p] * 0.25f || p.Men == 0))
            {
                p.Down = true;
                p.DownUntil = (float)S.Hours + 5 * 24;
                p.Pos = SettlementById(p.HomeId).Pos;
                p.SiegeTarget = -1;
                if (playerIn && won && rng.Chance(0.5f))
                {
                    p.Captive = true;
                    p.DownUntil = float.MaxValue;
                    Player.Captives.Add(p.Id);
                    Log($"{p.Name} is your prisoner.");
                }
                else { Log($"{p.Name}'s army is scattered."); MaybeSlain(p, playerIn && won); }
                if (p.Captive && CharacterOf(p) is Character held) Remember(held, Me, "you took me captive", -8);
            }
        }
        if (playerIn) RelationsAfterBattle(winners, losers, won);
        var skillsBefore = new Dictionary<string, float>(Captain.Skills);
        if (playerIn) SkillsAfterBattle(r, won, playerTeam == 0 ? startStrength0 : startStrength1, playerTeam == 0 ? startStrength1 : startStrength0, before[Player], playerTeam);
        if (playerIn) OrderAfterBattle(won, playerTeam == 0 ? side1.ToList() : side0.ToList());
        if (playerIn) AuraAfterBattle(r, won, (playerTeam == 0 ? startStrength1 : startStrength0) / MathF.Max(1, playerTeam == 0 ? startStrength0 : startStrength1));
        int wf = winners.Select(SideOf).FirstOrDefault(f => f >= 0, -1), lf = losers.Select(SideOf).FirstOrDefault(f => f >= 0, -1);
        if (wf >= 0 && lf >= 0) Score(wf, lf, Rules.PerMan(lostStrength));
        AfterTheBattle(r, lookup, winners, losers, playerIn);
        S.Parties.RemoveAll(p => (Outlaw(p) || p.Kind == PartyKind.Caravan) && p.Down);

        var report = new BattleReport(won, enemyName, reports, 0, 0, 0, 0, 0, r.Captain);
        if (!playerIn) return report;
        S.LastBattleAt = (float)S.Hours;
        Count("battles");
        if (won) Count("victories");
        if (r.PlayerKills >= 5) Count("frontline");
        if (won && (playerTeam == 0 ? startStrength1 / MathF.Max(1, startStrength0) : startStrength0 / MathF.Max(1, startStrength1)) >= 1.5f) Count("outnumbered");
        float bounty = 0, renown = 0;
        int horses = 0;
        // Personal goals met (P6): renown and a lesson each.
        foreach (var id in r.GoalsMet)
        {
            Captain.Renown += GoalRenown;
            renown += GoalRenown;
            Practise(Battle.Goal(id).Skill, GoalPractice);
        }
        if (won)
        {
            foreach (var e in losers.Where(e => e.Lair || e.Kind == PartyKind.Caravan)) loot += e.Gold; // a holdout's hoard, a caravan's purse
            foreach (var e in losers)
                bounty += e.Breed == "troll" ? B.BountyTroll : e.Breed == "undead" ? B.BountyUndead
                    : e.Lair ? B.BountyHoldout + Rules.PerMan(Rules.Strength(e) / 10)
                    : Outlaw(e) ? B.BountyBandits + Rules.PerMan(Rules.Strength(e) / 10)
                    : Captain.Contract >= 0 ? B.BountyLord + Rules.PerMan((startStrength1 + startStrength0) / 20) : 0;
            renown += B.RenownPerVictory + (playerTeam == 0 ? startStrength1 : startStrength0) / (B.RenownStrengthDivisor * B.ArmyScale);
            horses = r.HorsesTaken;
            S.Gold += loot + ransom + bounty;
            foreach (var e in losers) S.LastBattleFoe = e.Id;
            Captain.Renown += renown - GoalRenown * r.GoalsMet.Count;
            Player.Horses += horses;
        }
        else if (Player.Men > 0)
        {
            // Beaten but not taken: fall back.
            var away = Player.Pos - (losers == side0 ? side1 : side0)[0].Pos;
            Player.Pos += (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 8;
        }
        string lasting = "";
        if (r.Captain is LeaderFate.Wounded or LeaderFate.Captured)
        {
            Captain.WoundedUntil = (float)S.Hours + 24 * rng.Range(3, 5) * (Has("Bonesetter") ? 0.5f : 1);
            lasting = MaybeLastingWound();
        }
        if (r.Captain == LeaderFate.Captured && S.Ironman)
        {
            S.GameOver = true;
            Log("You are taken, and your captors will not ransom you. Your war is over.");
        }
        else if (r.Captain == LeaderFate.Captured || (!won && Player.Men == 0))
        {
            S.Gold *= 0.7f;
            var refuge = S.Settlements.Where(t => t.Kind == SettlementKind.Town && !Refuses(t)).OrderBy(t => Vector2.Distance(t.Pos, Player.Pos)).First();
            Player.Pos = refuge.Pos + new Vector2(5, 0);
            Player.Moving = false;
            if (Player.Men == 0 && Player.Wounded == 0) Player.Companies.Add(NewCompany(RecruitTroop(Math.Max(0, Captain.Contract)), "infantry", 10, 0, refuge));
            Log($"You are taken, and ransomed. Your men regroup at {refuge.Name}.");
        }
        string spoil = won ? BattleSpoil(losers, r) : "";
        Report = report with { Loot = loot, Ransom = ransom, Bounty = bounty, Renown = renown, Horses = horses, Spoil = spoil, Rises = Skills.Rises(Captain, skillsBefore), LastingWound = lasting, Aura = TakeAuraNews(),
            Goals = r.Goals.Select((id, i) => (i < r.GoalTexts.Count ? r.GoalTexts[i] : Battle.Goal(id).Text, r.GoalsMet.Contains(id))).ToList(),
            Roll = RollNewsOrdered().Where(n => n.Item1 is not ("HONOUR" or "LOST")).ToList(),
            Honours = RollNewsOrdered().Where(n => n.Item1 is "HONOUR" or "LOST").ToList(), Field = field };
        if (won) RaiseBurial();
        Log(won ? $"Victory over {enemyName}." : $"Defeat against {enemyName}.");
        return Report;
    }

    // ---------- encounters with you ----------

    /// One stack's share of a battle: the fallen split into killed, wounded and taken by its armour and your surgeons,
    /// the fled who come back, the men taken off the stack, and its named men's fates. Returns the counts for the report.
    (int Killed, int Wounded, int Captured, int FledBack) StackLosses(Party p, CompanyRecord c, CompanyResult cr, StackResult sr, TroopStack st, bool teamWon, float surgeon, string foe)
    {
        int stBefore = st.Men;
        float pw = Math.Clamp(B.WoundBase + st.Type.Armor / B.WoundArmor + surgeon, B.WoundMin, B.WoundMax);
        int wounded = (int)MathF.Round(sr.Fallen * pw), killed = sr.Fallen - wounded;
        int captured = teamWon ? 0 : (int)MathF.Round(wounded * B.CaptureChance);
        wounded -= captured;
        int fledBack = teamWon ? sr.Fled : (int)MathF.Round(sr.Fled * B.FledReturnOnDefeat);
        int standing = cr.Deployed ? sr.Standing : stBefore;
        int after = Math.Clamp(standing + fledBack, 0, stBefore);
        if (after < stBefore) st.Take(stBefore - after);
        st.Wounded += wounded;
        if (p == Player && cr.Deployed)
        {
            if (cr.Named != null) RollFought(c, st, cr.Named, teamWon, pw, foe);
            else RollFates(c, st, stBefore, killed, wounded, captured + sr.Fled - fledBack, foe);
            // A named man who fell and lives is one of the wounded, even when the stack's rounded odds said killed.
            int more = Math.Min(c.Roll.Count(m => m.Troop == st.Troop && m.Hurt) - st.Wounded, killed + captured);
            if (more > 0)
            {
                int fromKilled = Math.Min(more, killed);
                killed -= fromKilled; captured -= more - fromKilled;
                wounded += more; st.Wounded += more;
            }
        }
        return (killed, wounded, captured, fledBack);
    }

    /// The parties that fight with you and against you: anyone close enough joins the side they are friendly to.
    public BattleSetup PrepareBattle(Party enemy)
    {
        var setup = new BattleSetup { EnemyName = enemy.Name, Field = BattleName(Player.Pos) };
        setup.Side0.Add(Player);
        setup.Side1.Add(enemy);
        foreach (var p in S.Parties)
        {
            if (p == Player || p == enemy || p.Down || Vector2.Distance(p.Pos, Player.Pos) > B.JoinRadius) continue;
            if (Hostile(p, enemy) && !Hostile(p, Player)) setup.Side0.Add(p);
            else if (Hostile(p, Player) && !Hostile(p, enemy)) setup.Side1.Add(p);
        }
        bool unpaid = S.UnpaidWeeks > 0;
        setup.Player = WithHeroes(WithDivisions(Rules.Army("Your company", setup.Side0, (unpaid ? -15 : 0) + (Has("Rallying") ? 5 : 0)))); // callings (Callings.cs)
        setup.Enemy = WithChampion(Armies.Divide(Rules.Army(enemy.Name, setup.Side1)), enemy);
        setup.Seed = BattleSeed(enemy);
        bool scouts = setup.Side0.SelectMany(p => p.Companies).Any(c => c.Men > 0 && c.Type.Mount != null);
        setup.Options = new BattleOptions
        {
            AuraRadius = (Has("Bannerman") ? 16 : 12) + (Rank >= Rank.Lord ? LordBanner : 0), // a lord's banner reaches further (P5)
            KingsCharge = Rank == Rank.King,
            Goals = true, Banner = true, LastStand = true, // personal goals, your banner, the last stand (P6)
            RallyBonus = Has("WarCry") ? 35 : 25,
            RallyCooldown = Has("WarCry") ? 9 : 12,
            IronWill = Has("IronWill"),
            Horns = Has("Bannerman"),
            Cap = Settings.Current.BattleSize,
            PlayerWounded = S.Hours < Captain.WoundedUntil,
            Scouted = scouts || Has("Tactician"),
            EnemyDamage = Settings.Current.EnemyDamage, EnemyMorale = DuelMoraleFor(enemy),
            Ground = GroundFor(enemy, GroundChooser(enemy, setup.Side0)), // the map's field, the rain, who chose it (Ground.cs)
            EnemyAggression = SideOf(enemy) >= 0 ? Factions.Get(SideOf(enemy)).Aggression : 1.5f, // a realm's temper; brigands do not wait long
            EnemyAura = ChampionAura(enemy), EnemyForms = ChampionFormsOf(enemy), // an order knight leads with his aura (K6)
            Weather = Weather.For((float)(S.Hours % 24), SeasonIndex, HashCode.Combine(S.Seed, (int)S.Hours, enemy.Id)),
            Palette = new[] { Style.PaletteOf(Math.Max(0, Captain.Contract)), Style.PaletteOf(SideOf(enemy)) },
            Standing = ActiveBuffs.Concat(RelicReady ? new[] { "relic" } : Array.Empty<string>()).ToArray(), // a shrine's blessing, a feast, hunger (Buffs.cs); your relic (Rites.cs)
        }.WithCaptain(Captain);
        return setup;
    }

    public BattleReport FinishBattle(BattleResult r, BattleSetup setup)
    {
        if (setup.Tournament >= 0) return FinishTournament(r, S.Settlements[setup.Tournament]);
        if (setup.Duel >= 0 && PartyById(setup.Duel) is Party foe) return FinishDuel(r, foe);
        if (setup.Trial != "") return FinishTrial(r, setup.Trial);
        Encounter = null;
        duelWith = -1;
        var report = Apply(r, setup.Side0, setup.Side1, setup.EnemyName, setup.SiegeOf >= 0);
        DivisionsAfterBattle(r);
        if (setup.SiegeOf >= 0) AfterAssault(S.Settlements[setup.SiegeOf], r.Winner == 0);
        return Report ?? report;
    }

    public BattleReport AutoResolveEncounter()
    {
        BreakVow("no_auto", "send your men to fight without you at their head");
        var setup = PrepareBattle(Encounter!);
        var r = Rules.AutoResolve(setup.Side0, setup.Side1, rng, (1 + B.AuraWeight * AuraInBattle) * TacticsEdge * BuffEdge * DuelEdge(Encounter!) * GroundEdge(setup.Options.Ground!, setup.Side0, setup.Side1, setup.Seed), Edge(Encounter!));
        if (r.Winner == 1 && S.Hours >= Captain.WoundedUntil && rng.Chance(0.5f)) r = r with { Captain = LeaderFate.Wounded };
        return FinishBattle(r, setup);
    }

    public bool CanRetreat(out string why)
    {
        var e = Encounter!;
        bool horse = e.Companies.Any(c => c.Men > 0 && c.Type.Mount != null);
        why = e.Breed == "wolf" && Speed(e) > Speed(Player) ? "THE WOLVES WILL RUN YOU DOWN"
            : horse && Speed(e) > Speed(Player) ? "THEIR HORSE WILL RIDE YOU DOWN" : "";
        return why == "";
    }

    /// Escape, leaving a rearguard: the slowest company loses 10-25% of its men.
    public void Retreat()
    {
        duelWith = -1;
        var e = Encounter!;
        if (Rules.Strength(e) < Rules.Strength(Player)) Count("fled"); // running from a weaker foe is what makes a coward (P4)
        BreakVow("no_desert", "flee from battle");
        var rear = Player.Companies.Where(c => c.Men > 0).OrderBy(c => c.Type.Mount != null ? 1 : 0).ThenByDescending(c => c.Men).FirstOrDefault();
        if (rear != null)
        {
            int lost = Math.Max(1, (int)(rear.Men * rng.Range(0.1f, 0.25f) * (Has("Ambusher") ? 0.5f : 1)));
            rear.RemoveFit(lost, false);
            rear.Prune();
            Log($"You escape. {rear.Name} lose {lost} men covering the retreat.");
        }
        var away = Player.Pos - e.Pos;
        Player.Pos += (away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX) * 8;
        Player.Cooldown = e.Cooldown = 4;
        Player.Companies.RemoveAll(c => c.Men + c.Wounded <= 0);
        Encounter = null;
    }

    public int ParleyPrice(Party e) => e.Kind == PartyKind.Bandit ? (int)(Rules.PerMan(Rules.Strength(e) * 2) + 50) : -1;

    public bool Parley()
    {
        var e = Encounter!;
        int price = ParleyPrice(e);
        if (price < 0 || S.Gold < price) return false;
        S.Gold -= price;
        e.Cooldown = Player.Cooldown = 24;
        Log($"You pay {e.Name} {price} crowns to go their way.");
        Encounter = null;
        return true;
    }
}
