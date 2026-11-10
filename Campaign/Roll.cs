namespace Ironfield;

/// The roll of named men in your companies (docs/archive/LEGACY.md L1): who earns a name, how a named man shares his stack's
/// fortune in battle, and how the roll follows its men when they are promoted, moved, split or merged. The roll draws
/// on its own stream (`RollRng`), so it never moves the campaign's main draws.
public sealed partial class Campaign
{
    Rng? rollRng;
    /// What happened to your named men in the battle being applied (L3), for the report.
    readonly List<(string Kind, string Text)> rollNews = new();

    List<(string, string)> RollNewsOrdered() =>
        rollNews.OrderBy(n => n.Kind switch { "FELL" => 0, "MISSING" => 1, "LOST" => 2, "HONOUR" => 3, "HURT" => 4, _ => 5 }).ToList();
    /// The named men who fell in the battle being applied (company id, soldier id), for the burial (L3).
    readonly List<(int Company, int Soldier)> rollFell = new();

    /// After a battle you won where named men fell: a burial for the longest-serving of them, if nothing else is waiting.
    void RaiseBurial()
    {
        if (S.Event != null || rollFell.Count == 0 || Events.All(e => e.Id != "bury_named")) return;
        var (co, id) = rollFell.OrderByDescending(f => S.Fallen.FirstOrDefault(m => m.Id == f.Soldier)?.Battles ?? 0).First();
        if (Player.Companies.All(c => c.Id != co)) return;
        Raise(new PendingEvent { Id = "bury_named", Company = co, Soldier = id });
    }

    static string Known(Soldier m) => m.Deeds.Count > 0 ? " " + m.Deeds[^1] : "";
    Rng RollRng => rollRng ??= new Rng(S, true);

    /// The named men a company has lost, oldest first.
    public IEnumerable<Soldier> FallenOf(CompanyRecord c) => S.Fallen.Where(m => m.Company == c.Id);

    /// Your named men in a stack who are fit to fight.
    public static IEnumerable<Soldier> FitNamed(CompanyRecord c, string troop) => c.Roll.Where(m => m.Troop == troop && !m.Hurt);

    /// A name for a new man of the company: a first name of its culture and a byname or his home, not already on its roll.
    string ManName(CompanyRecord c)
    {
        var n = Names.Get();
        var firsts = n.MenOf(c.Culture);
        string name = "";
        for (int tries = 0; tries < 12; tries++)
        {
            string first = RollRng.Pick(firsts);
            name = c.Home != "" && RollRng.Chance(0.35f) ? $"{first} of {c.Home}" : $"{first} {RollRng.Pick(n.Epithets)}";
            if (!c.Roll.Concat(FallenOf(c)).Any(m => m.Name == name)) break;
        }
        return name;
    }

    /// A fit man of the company not yet named, from a stack weighted by its unnamed men (better troops a little more often).
    string? UnnamedTroop(CompanyRecord c)
    {
        var pool = c.Stacks.Select(s => (s.Troop, Free: s.Men - FitNamed(c, s.Troop).Count(), s.Type.Tier)).Where(x => x.Free > 0).ToList();
        if (pool.Count == 0) return null;
        float total = pool.Sum(x => x.Free * (1 + x.Tier * 0.5f)), pick = RollRng.Float() * total;
        foreach (var x in pool) { pick -= x.Free * (1 + x.Tier * 0.5f); if (pick <= 0) return x.Troop; }
        return pool[^1].Troop;
    }

    /// Give a man of the company a name for a deed (null when the roll is full or no man is free).
    internal Soldier? NameMan(CompanyRecord c, string deed, string? troop = null)
    {
        if (c.Roll.Count >= B.RollSize) return null;
        troop ??= UnnamedTroop(c);
        if (troop == null || c.Stacks.FirstOrDefault(s => s.Troop == troop) is not TroopStack st || st.Men <= FitNamed(c, troop).Count()) return null;
        var m = new Soldier { Id = S.NextSoldier++, Name = ManName(c), Troop = troop, Joined = Day };
        m.Deed(deed);
        c.Roll.Add(m);
        return m;
    }

    /// A named man leaves the roll for the company's fallen: "fell", "missing" or "left".
    void Lose(CompanyRecord c, Soldier m, string fate, string where)
    {
        c.Roll.Remove(m);
        m.Fate = fate; m.DiedDay = Day; m.DiedAt = where; m.Hurt = false;
        m.Company = c.Id; m.CompanyName = c.Name;
        S.Fallen.Add(m);
        if (!Player.Companies.Contains(c)) return;
        if (fate is "fell" or "missing")
        {
            // Losing a man the company knew costs it (L3); a long-serving man is remembered in the chronicle.
            c.Cohesion = MathF.Max(0, c.Cohesion - B.FallenCohesion);
            if (fate == "fell") rollFell.Add((c.Id, m.Id));
            rollNews.Add((fate == "fell" ? "FELL" : "MISSING", $"{m.Name}, {Troops.Get(m.Troop).Name}, {c.Name}. {N(m.Battles, "battle")}, {N(m.Kills, "kill")}.{Known(m)}"));
            if (fate == "fell" && m.Battles >= 10) Deed($"{m.Name} of {c.Name} fell against {(where != "" ? where : "the enemy")}, after {m.Battles} battles.");
        }
        Log(fate switch { "fell" => $"{m.Name} of {c.Name} has fallen.", "missing" => $"{m.Name} of {c.Name} is missing.", _ => $"{m.Name} has left {c.Name}." });
    }

    void Hurt(CompanyRecord c, Soldier m)
    {
        m.Hurt = true; m.Wounds++;
        rollNews.Add(("HURT", $"{m.Name}, {c.Name}."));
    }

    /// The named men of one stack share its fortune in a battle (L1): each fit one is killed, lost or wounded with the
    /// stack's own odds, death and loss scaled by VeteranSurvival. `fitBefore` is the stack's fit men going in.
    internal void RollFates(CompanyRecord c, TroopStack st, int fitBefore, int killed, int wounded, int lost, string foe)
    {
        if (fitBefore <= 0 || c.Roll.Count == 0) return;
        // VeteranSurvival turns some of the deaths and losses into wounds.
        float pk = killed / (float)fitBefore * B.VeteranSurvival, pl = lost / (float)fitBefore * B.VeteranSurvival,
            pw = (wounded + (killed + lost) * (1 - B.VeteranSurvival)) / fitBefore;
        foreach (var m in FitNamed(c, st.Troop).ToList())
        {
            float u = RollRng.Float();
            if (u < pk) Lose(c, m, "fell", foe);
            else if (u < pk + pl) Lose(c, m, "missing", foe);
            else if (u < pk + pl + pw) Hurt(c, m);
        }
    }

    /// The named men of one stack after a fought battle (L2): each one's own fate. One who fell is wounded rather than killed
    /// more often than his stack (`pw` its wound odds, VeteranSurvival on the rest), and may be taken on a lost field; one
    /// who fled from a lost field may be missing. His kills count, and a fight worth telling becomes a deed.
    internal void RollFought(CompanyRecord c, TroopStack st, IReadOnlyList<NamedResult> named, bool won, float pw, string foe)
    {
        string against = foe != "" ? $" against {foe}" : "";
        foreach (var nr in named)
        {
            if (c.Roll.FirstOrDefault(m => m.Id == nr.Id && m.Troop == st.Troop) is not Soldier m) continue;
            m.Kills += nr.Kills;
            if (nr.FelledLeader) m.Deed($"Cut down a captain{against}.");
            else if (nr.Kills >= 5) m.Deed($"Killed {nr.Kills}{against}.");
            if (nr.Fell && nr.Saved) { m.Deed($"Pulled from the press by the captain{against}."); Hurt(c, m); }
            else if (nr.Fell)
            {
                if (RollRng.Float() >= 1 - (1 - pw) * B.VeteranSurvival) Lose(c, m, "fell", foe);
                else if (!won && RollRng.Chance(B.CaptureChance * B.VeteranSurvival)) Lose(c, m, "missing", foe);
                else Hurt(c, m);
            }
            else if (nr.Fled && !won && RollRng.Chance((1 - B.FledReturnOnDefeat) * B.VeteranSurvival)) Lose(c, m, "missing", foe);
        }
    }

    /// After a battle your company fought in: its named men count it, and a man may earn a name for what the company did.
    internal void RollAfterBattle(CompanyRecord c, CompanyResult cr, bool won, bool bloodied, bool siege, string foe)
    {
        if (!cr.Deployed || c.Men <= 0) return;
        foreach (var m in c.Roll) m.Battles++;
        string against = foe != "" ? $" against {foe}" : "";
        // In a fought battle the best unnamed man earns his name by his own hand; else by what the company did.
        if (cr.Named != null && cr.BestTroop != "" && (cr.BestFelledLeader || cr.BestKills >= 4)
            && NameMan(c, cr.BestFelledLeader ? $"Cut down a captain{against}." : $"Killed {cr.BestKills}{against}.", cr.BestTroop) is Soldier hand)
        {
            hand.Battles = 1; hand.Kills = cr.BestKills;
            rollNews.Add(("NAMED", $"{hand.Name}, {Troops.Get(hand.Troop).Name}, {c.Name}.{Known(hand)}"));
            return;
        }
        string? deed = siege && won ? $"First over the wall{against}."
            : cr.HorseKills > 0 && RollRng.Chance(0.6f) ? $"Brought down a horseman{against}."
            : bloodied ? $"Stood when half the company fell{against}."
            : !won && RollRng.Chance(0.5f) ? $"Came home from the lost field{against}."
            : won && cr.Kills > 0 && RollRng.Chance(0.35f) ? $"Fought in the front rank{against}."
            : null;
        if (deed != null && NameMan(c, deed) is Soldier named)
        {
            named.Battles = 1;
            rollNews.Add(("NAMED", $"{named.Name}, {Troops.Get(named.Troop).Name}, {c.Name}.{Known(named)}"));
        }
    }

    /// Keep the roll true to its stacks: no more hurt named men than the stack's wounded and no more fit ones than its fit men.
    /// Surplus hurt men heal if the fit have room; after a battle (`battle`) surplus fit men are wounded if the wounded have
    /// room. Any still over, or of a troop the company no longer has, leave the roll: as "fell" after a battle (`foe` is
    /// where), else "left" (desertion, hunger). A new company names its first man.
    internal void SyncRoll(CompanyRecord c, bool battle = false, string foe = "")
    {
        string surplus = battle ? "fell" : "left";
        foreach (var g in c.Roll.GroupBy(m => m.Troop).ToList())
        {
            var st = c.Stacks.FirstOrDefault(s => s.Troop == g.Key);
            if (st == null) { foreach (var m in g.ToList()) Lose(c, m, surplus, battle ? foe : ""); continue; }
            var hurt = g.Where(m => m.Hurt).ToList();
            var fit = g.Where(m => !m.Hurt).ToList();
            while (hurt.Count > st.Wounded && fit.Count < st.Men) { var m = hurt[^1]; hurt.RemoveAt(hurt.Count - 1); m.Hurt = false; fit.Add(m); }
            while (battle && fit.Count > st.Men && hurt.Count < st.Wounded) { var m = fit[^1]; fit.RemoveAt(fit.Count - 1); Hurt(c, m); hurt.Add(m); }
            foreach (var m in hurt.Skip(st.Wounded).Concat(fit.Skip(st.Men)).ToList()) Lose(c, m, surplus, battle ? foe : "");
        }
        if (!c.RollStarted && c.Men > 0)
        {
            c.RollStarted = true;
            NameMan(c, c.Home != "" ? $"One of the first to take the company's pay, at {c.Home}." : "One of the first to take the company's pay.");
        }
    }

    /// Daily: your companies' rolls follow their healing and their losses away from battle.
    void RollDaily() { foreach (var c in Player.Companies) SyncRoll(c); }

    /// A man of `from` (a `fromTroop`) is promoted to `to` in company `into`: a fit named man goes first (the senior one);
    /// else the first man of the company to reach a new tier earns a name for it.
    void RollPromoted(CompanyRecord from, CompanyRecord into, string fromTroop, string to)
    {
        var m = FitNamed(from, fromTroop).OrderByDescending(x => x.Battles).FirstOrDefault();
        if (m != null)
        {
            m.Troop = to;
            if (into != from) { from.Roll.Remove(m); into.Roll.Add(m); }
            return;
        }
        int tier = Troops.Get(to).Tier;
        if (into.Stack(to).Men == 1 && into.Stacks.All(s => s.Troop == to || s.Type.Tier < tier) && tier >= 2)
            NameMan(into, $"The first of the company made {Troops.Get(to).Name}, on day {Day}.", to);
    }

    /// Named men of a troop go with their men to another company: all of them (`share` 1) or a share of the fit ones.
    static void MoveNamed(CompanyRecord from, CompanyRecord to, string troop, float share = 1)
    {
        var men = share >= 1 ? from.Roll.Where(m => m.Troop == troop).ToList() : FitNamed(from, troop).ToList();
        int k = share >= 1 ? men.Count : (int)MathF.Round(men.Count * share);
        foreach (var m in men.Take(k)) { from.Roll.Remove(m); to.Roll.Add(m); }
    }

    // ---------- rising from the ranks (L4) ----------

    /// Where a companion came from, for the screens: "ROSE FROM THE RANKS OF X" or "".
    public string RoseFrom(Character? ch) => ch?.Ranks == null ? "" :
        $"ROSE FROM THE RANKS OF {(Player.Companies.FirstOrDefault(c => c.Id == ch.FromCompany)?.Name ?? ch.Ranks.CompanyName).ToUpperInvariant()}";

    /// A new sergeant for one of your companies: its senior named man (3+ battles) leaves the ranks to take the post. `drawn`
    /// is the name drawn as before (the draw is kept so the main stream does not move), used when no named man is ready.
    string RiseToSergeant(CompanyRecord c, string drawn)
    {
        c.OfficerMan = null;
        if (!Player.Companies.Contains(c)) return drawn;
        var m = c.Roll.Where(x => !x.Hurt && x.Battles >= 3).OrderByDescending(x => x.Battles).ThenByDescending(x => x.Kills).FirstOrDefault();
        if (m == null || c.Stacks.FirstOrDefault(s => s.Troop == m.Troop) is not TroopStack st || st.Men <= 0) return drawn;
        c.Roll.Remove(m);
        st.Take(1); // he leaves the ranks
        m.Deed($"Made sergeant of {c.Name} on day {Day}.");
        c.OfficerMan = m;
        Log($"{m.Name} rises from the ranks to be sergeant of {c.Name}.");
        return "Sgt " + m.Name;
    }

    /// A sergeant who rose from the ranks falls: he goes to the fallen with his record.
    void SergeantFell(CompanyRecord c, string where)
    {
        if (c.OfficerMan is not Soldier m) return;
        c.OfficerMan = null;
        m.Deed($"Fell as sergeant of {c.Name}.");
        LoseRecord(c, m, where);
    }

    /// A companion who rose from the ranks falls: his old record goes to the fallen of the company he came from.
    void CompanionFell(Character k, string where)
    {
        if (k.Ranks is not Soldier m) return;
        k.Ranks = null;
        m.Deed($"Fell as your {k.Role.ToLowerInvariant()}.");
        var c = Player.Companies.FirstOrDefault(x => x.Id == k.FromCompany);
        if (c != null) LoseRecord(c, m, where);
        else { m.Fate = "fell"; m.DiedDay = Day; m.DiedAt = where; S.Fallen.Add(m); }
    }

    /// Put a man who is no longer on the roll (a sergeant, a companion) among the company's fallen, with the battle's news.
    void LoseRecord(CompanyRecord c, Soldier m, string where)
    {
        m.Battles = Math.Max(m.Battles, 1);
        c.Roll.Add(m);
        Lose(c, m, "fell", where);
    }
}
