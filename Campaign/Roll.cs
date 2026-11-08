namespace Ironfield;

/// The roll of named men in your companies (docs/LEGACY.md L1): who earns a name, how a named man shares his stack's
/// fortune in battle, and how the roll follows its men when they are promoted, moved, split or merged. The roll draws
/// on its own stream (`RollRng`), so it never moves the campaign's main draws.
public sealed partial class Campaign
{
    Rng? rollRng;
    Rng RollRng => rollRng ??= new Rng(S, true);

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
            if (!c.Roll.Concat(c.Fallen).Any(m => m.Name == name)) break;
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
        c.Fallen.Add(m);
        if (Player.Companies.Contains(c))
            Log(fate switch { "fell" => $"{m.Name} of {c.Name} has fallen.", "missing" => $"{m.Name} of {c.Name} is missing.", _ => $"{m.Name} has left {c.Name}." });
    }

    /// The named men of one stack share its fortune in a battle (L1): each fit one is killed, lost or wounded with the
    /// stack's own odds, death and loss scaled by VeteranSurvival. `fitBefore` is the stack's fit men going in.
    internal void RollFates(CompanyRecord c, TroopStack st, int fitBefore, int killed, int wounded, int lost, string foe)
    {
        if (fitBefore <= 0 || c.Roll.Count == 0) return;
        float pk = killed / (float)fitBefore * B.VeteranSurvival, pl = lost / (float)fitBefore * B.VeteranSurvival, pw = wounded / (float)fitBefore;
        foreach (var m in FitNamed(c, st.Troop).ToList())
        {
            float u = RollRng.Float();
            if (u < pk) Lose(c, m, "fell", foe);
            else if (u < pk + pl) Lose(c, m, "missing", foe);
            else if (u < pk + pl + pw) { m.Hurt = true; m.Wounds++; }
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
            if (nr.Fell)
            {
                if (RollRng.Float() >= 1 - (1 - pw) * B.VeteranSurvival) Lose(c, m, "fell", foe);
                else if (!won && RollRng.Chance(B.CaptureChance * B.VeteranSurvival)) Lose(c, m, "missing", foe);
                else { m.Hurt = true; m.Wounds++; }
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
            return;
        }
        string? deed = siege && won ? $"First over the wall{against}."
            : cr.HorseKills > 0 && RollRng.Chance(0.6f) ? $"Brought down a horseman{against}."
            : bloodied ? $"Stood when half the company fell{against}."
            : !won && RollRng.Chance(0.5f) ? $"Came home from the lost field{against}."
            : won && cr.Kills > 0 && RollRng.Chance(0.35f) ? $"Fought in the front rank{against}."
            : null;
        if (deed != null && NameMan(c, deed) is Soldier named) named.Battles = 1;
    }

    /// Keep the roll true to its stacks: no more hurt named men than the stack's wounded (the rest are healed), no more fit
    /// ones than its fit men, none of a troop the company no longer has (they have left). A new company names its first man.
    internal void SyncRoll(CompanyRecord c)
    {
        foreach (var g in c.Roll.GroupBy(m => m.Troop).ToList())
        {
            var st = c.Stacks.FirstOrDefault(s => s.Troop == g.Key);
            if (st == null) { foreach (var m in g.ToList()) Lose(c, m, "left", ""); continue; }
            var hurt = g.Where(m => m.Hurt).ToList();
            for (int i = st.Wounded; i < hurt.Count; i++) hurt[i].Hurt = false;
            var fit = g.Where(m => !m.Hurt).ToList();
            for (int i = st.Men; i < fit.Count; i++) Lose(c, fit[i], "left", "");
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
}
