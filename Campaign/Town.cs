using Microsoft.Xna.Framework;

namespace Ironfield;

/// A piece of tavern talk (S5) about something real on the map: where it is, and the site it names (-1 = none).
public sealed record Rumour(string Text, Vector2 At, int Site = -1);

/// Town life (docs/SIDE_CONTENT.md S5): the tavern's rumours, tournaments fought as small real battles with blunt
/// weapons, and the line that says what a town is like.
public sealed partial class Campaign
{
    /// A tournament you entered (the event's "lists" effect), waiting for the map to start the fight.
    public Settlement? Lists;
    /// The purse, and how many of your men ride into the lists beside you.
    public const float TournamentPurse = 300;
    public const int TournamentMen = 6;

    /// The fight in the lists: your captain and a few of your best men against the town's champions, on flat ground.
    public BattleSetup PrepareTournament(Settlement t)
    {
        var setup = new BattleSetup { EnemyName = $"Champions of {t.Name}", Tournament = t.Id };
        setup.Seed = HashCode.Combine(S.Seed, (int)(S.Hours * 4), t.Id, 77);
        var best = Player.Companies.SelectMany(c => c.Stacks.Where(st => st.Men > 0).Select(st => (c, st))).OrderByDescending(x => x.st.Type.Tier).ThenByDescending(x => x.st.Men).FirstOrDefault();
        var mine = new List<Company>();
        if (best.st != null) mine.Add(new Company(best.st.Troop, Math.Min(TournamentMen, best.st.Men), Rules.RoleOf(best.c), best.c.Id, best.c.Name, 85));
        setup.Player = new ArmySpec("Your champions", 0, mine);
        int tier = Math.Clamp(2 + (int)(Captain.Renown / 150), 2, 5);
        setup.Enemy = new ArmySpec(setup.EnemyName, 0, new List<Company> { new(Armies.LineTroop("infantry", tier, t.Faction), TournamentMen + 1, "infantry", -1, "Champions", 80) });
        int mePal = Style.PaletteOf(Math.Max(0, Captain.Contract)), townPal = Style.PaletteOf(t.Faction);
        setup.Options = new BattleOptions
        {
            Tournament = true, Couriers = false, Cap = 40, Scouted = true,
            PlayerWounded = S.Hours < Captain.WoundedUntil, EnemyDamage = Settings.Current.EnemyDamage,
            Ground = TerrainParams.Lists, Weather = new Weather(13),
            Palette = new[] { mePal, townPal == mePal ? 3 : townPal },
        }.WithCaptain(Captain);
        return setup;
    }

    /// Without a fight (the stand-in captain, or a quick resolve): your skill at arms decides.
    public BattleReport AutoTournament(Settlement t)
    {
        Lists = null;
        bool won = rng.Chance(0.3f + Skill("Arms") / 300f);
        return FinishTournament(new BattleResult(won ? 0 : 1, 0, new List<CompanyResult>(), LeaderFate.Unhurt, LeaderFate.Unhurt, 0), t);
    }

    /// After the lists: no one dies (blunt weapons), half of the men who fell are bruised for a while, and the winner takes
    /// the purse and the crowd's cheers.
    BattleReport FinishTournament(BattleResult r, Settlement t)
    {
        Encounter = null;
        bool won = r.Winner == 0;
        var rows = new List<CompanyReport>();
        foreach (var cr in r.Companies.Where(x => x.Team == 0))
        {
            if (Player.Companies.FirstOrDefault(c => c.Id == cr.Id) is not CompanyRecord co) continue;
            var st = co.Stacks.Where(x => x.Men > 0).OrderByDescending(x => x.Type.Tier).FirstOrDefault();
            int hurt = Math.Min(st?.Men ?? 0, cr.Fallen / 2);
            if (st != null) { st.Men -= hurt; st.Wounded += hurt; st.AddXp(cr.Kills * 15 + cr.Start * 5); }
            rows.Add(new CompanyReport(co.Id, co.Name, co.Troop, 0, cr.Start, cr.Start - hurt, 0, hurt, 0, 0, cr.Kills * 15 + cr.Start * 5, false, true));
        }
        float renown = won ? 10 : 2;
        Captain.Renown += renown;
        var skillsBefore = new Dictionary<string, float>(Captain.Skills);
        Practise("Arms", (won ? 2 : 1) + (r.Parries + r.Ripostes) * 0.15f);
        GainAura((won ? 3 : 1) + r.Parries * 0.3f);
        if (won) { S.Gold += TournamentPurse; t.Unrest = Math.Clamp(t.Unrest - 3, 0, 100); }
        var fate = r.Captain == LeaderFate.Unhurt ? LeaderFate.Unhurt : LeaderFate.Wounded;
        if (fate == LeaderFate.Wounded) Captain.WoundedUntil = MathF.Max(Captain.WoundedUntil, (float)S.Hours + 24);
        if (won) Deed($"You win the tournament at {t.Name} and its purse of {TournamentPurse:0} crowns.", "tournaments");
        else Log($"You are beaten in the lists at {t.Name}. The crowd cheers you anyway.");
        string prize = won && rng.Chance(TournamentPrizeChance) ? SpoilFrom(900) : ""; // the lord of the lists gives a prize (P1)
        Report = new BattleReport(won, $"Champions of {t.Name}", rows, won ? TournamentPurse : 0, 0, 0, renown, 0, fate, prize) { Rises = Skills.Rises(Captain, skillsBefore) };
        return Report;
    }

    // ---------- the tavern ----------

    /// What they are saying in the tavern: two or three things that are true on the map, nearest first. The talk changes
    /// day by day, not with every visit.
    public List<Rumour> Rumours(Settlement t)
    {
        var all = new List<Rumour>();
        float far = 160 * Map.Spread;
        string Where(Vector2 p) => Map.ProvinceAt(p).Id == t.Province ? $"{Bearing(t.Pos, p)}, not far from here" : $"{Bearing(t.Pos, p)}, in {Map.ProvinceAt(p).Title}";
        foreach (var h in Holdouts.Where(h => Vector2.Distance(h.Pos, t.Pos) < far))
            all.Add(new Rumour($"Goblins have dug in to the {Where(h.Pos)}. They come down at night for sheep and children.", h.Pos));
        foreach (var w in WolfPacks.Where(w => Vector2.Distance(w.Pos, t.Pos) < far))
            all.Add(new Rumour($"A wolf pack hunts to the {Where(w.Pos)}. The shepherds will not go out alone.", w.Pos));
        foreach (var b in S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down && p.CharId >= 0 && Vector2.Distance(p.Pos, t.Pos) < far * 1.5f))
            all.Add(new Rumour($"The {b.Name} ride to the {Where(b.Pos)}, and a king leads them.", b.Pos));
        foreach (var car in Caravans.Where(p => p.HomeId == t.Id && SettlementById(p.GoalSettlement) != t))
            all.Add(new Rumour($"The {car.Name} are on the road to {SettlementById(car.GoalSettlement).Name}, heavy with goods.", car.Pos));
        foreach (var site in Sites.Where(x => !SiteSeen(x) && Vector2.Distance(x.Pos, t.Pos) < far).OrderBy(x => Vector2.Distance(x.Pos, t.Pos)).Take(2))
            all.Add(new Rumour(site.Kind switch
            {
                "ruin" => $"They say the {site.Name}, to the {Bearing(t.Pos, site.Pos)}, still hold a king's gold.",
                "battlefield" => $"Swords rust in the grass at the {site.Name}, to the {Bearing(t.Pos, site.Pos)}.",
                "spring" => $"The water of the {site.Name}, to the {Bearing(t.Pos, site.Pos)}, closes wounds.",
                "hermit" => $"The {site.Name}, to the {Bearing(t.Pos, site.Pos)}, sees what others do not.",
                _ => $"Have you seen the {site.Name}? Ride {Bearing(t.Pos, site.Pos)} and you will find it.",
            }, site.Pos, site.Id));
        foreach (var l in S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && Captain.Contract >= 0 && FactionsAtWar(p.Faction, Captain.Contract) && Vector2.Distance(p.Pos, t.Pos) < far).Take(1))
            all.Add(new Rumour($"{l.Name} was seen with {l.Men} men to the {Where(l.Pos)}.", l.Pos));
        if (all.Count == 0) return new List<Rumour> { new("All is quiet on the roads, they say. Too quiet, says the innkeeper.", t.Pos) };
        // The day's talk: a stable choice for today, nearest things more likely.
        int day = (int)(S.Hours / 24);
        return all.OrderBy(x => Vector2.Distance(x.At, t.Pos) * (0.5f + (HashCode.Combine(day, t.Id, x.Text.Length) & 255) / 255f)).Take(3).ToList();
    }

    /// Ride where a rumour points (its site, if it names one).
    public void Follow(Rumour r)
    {
        if (SiteById(r.Site) is Site s) GoTo(s);
        else MoveTo(r.At);
    }

    /// One line on what a town is like: its people, its wealth, its mood and who holds it.
    public string Flavour(Settlement s)
    {
        string folk = Factions.Culture(s.Culture)?.Flavour ?? "Thatch and mud lanes";
        string wealth = s.Prosperity >= 70 ? "The streets are full and the stalls are fat" : s.Prosperity >= 40 ? "Trade is steady" : "Half the shops are shut";
        string mood = s.Unrest >= RevoltAt ? "Men talk of rebellion openly." : s.Unrest >= 40 ? "There is grumbling about the taxes." : s.Besieged ? "The walls are manned day and night." : "The people seem content.";
        string holder = s.Owner == Player.Id ? " It is yours." : HolderOf(s) is Character h ? $" {h.Name} holds it." : "";
        return $"{folk}. {wealth}. {mood}{holder}";
    }
}
