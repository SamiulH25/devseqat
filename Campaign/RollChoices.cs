namespace Ironfield;

/// Your hand on the roll (docs/COMPANY.md C4): choose a new sergeant from the men who qualify, mention a man after a won
/// battle, arm a man from your baggage or the looted arms, send an old or thrice-wounded man home with a pension, and move a
/// man to another company. Each is a `string?` action (null = done, else the reason shown).
public sealed partial class Campaign
{
    /// A day to choose a sergeant before the risen man keeps the post; mentions that make a man "of the banner".
    public const int SergeantPickDays = 1, BannerMentions = 3;
    /// A man you armed dies this much as often when he falls; age or wounds that let a man be pensioned.
    public const float ArmedSurvival = 0.8f;
    public const int PensionAge = 40, PensionWounds = 3;
    /// Pool XP a recruit brings from a village a pensioned man went home to.
    public const float PensionXp = 15;

    /// The officer trait a sergeant's nature becomes, where one fits.
    public static string? NatureTrait(string nature) => nature switch
    {
        "Steady" => "Steady", "Bold" => "Hothead", "Grim" => "Stubborn", "Proud" => "Veteran",
        "Sly" => "Sharpshooter", "Gentle" => "Surgeon", "Pious" => "Steady", "Merry" => "Drillmaster", _ => null,
    };

    static float Armed(Soldier m) => m.Carries != "" ? ArmedSurvival : 1;

    /// Of the banner: mentioned three times. Read first on the roll and in the end summary.
    public static bool OfBanner(Soldier m) => m.Mentions >= BannerMentions;

    // ---------- the sergeant ----------

    /// The men you may choose as sergeant now (the risen sergeant first), or none when there is no choice open.
    public List<Soldier> SergeantChoice(CompanyRecord c)
    {
        if (c.SergeantPick.Count == 0 || Day > c.SergeantPickUntil || c.OfficerMan is not Soldier risen) return new();
        var list = new List<Soldier> { risen };
        list.AddRange(c.SergeantPick.Skip(1).Select(id => c.Roll.FirstOrDefault(m => m.Id == id)).OfType<Soldier>().Where(m => !m.Hurt));
        return list.Count > 1 ? list : new();
    }

    /// The men who may be sergeant: fit, three battles or more.
    static IEnumerable<Soldier> SergeantMen(CompanyRecord c) =>
        c.Roll.Where(x => !x.Hurt && x.Battles >= 3).OrderByDescending(x => x.Battles).ThenByDescending(x => x.Kills);

    /// A sergeant rose from the ranks: if others qualified too, you have a day to choose another (C4).
    void OpenSergeantPick(CompanyRecord c, Soldier risen)
    {
        var others = SergeantMen(c).Where(o => c.Stacks.Any(s => s.Troop == o.Troop && s.Men > 0)).Take(2).ToList();
        if (others.Count == 0) { c.SergeantPick.Clear(); return; }
        c.SergeantPick = new List<int> { risen.Id }.Concat(others.Select(o => o.Id)).ToList();
        c.SergeantPickUntil = Day + SergeantPickDays;
        Beat ??= ($"A SERGEANT FOR {c.Name.ToUpperInvariant()}", new[] { $"{risen.Name.ToUpperInvariant()} STEPS UP. {N(others.Count, "OTHER MAN").ToUpperInvariant()} COULD TAKE THE POST.", "CHOOSE ON THE COMPANY'S ROLL BEFORE TOMORROW IS OUT, OR HE KEEPS IT." });
    }

    public string? CannotChooseSergeant(CompanyRecord c, Soldier m)
    {
        var pick = SergeantChoice(c);
        if (pick.Count == 0) return "NO SERGEANT TO CHOOSE";
        if (!pick.Contains(m)) return "HE IS NOT ONE OF THEM";
        return c.Stacks.Any(s => s.Troop == m.Troop && s.Men > 0) || m == c.OfficerMan ? null : "HE IS NOT IN THE RANKS";
    }

    /// Make this man sergeant: the risen one goes back to the ranks, and the new man's nature becomes a trait where one fits.
    public string? ChooseSergeant(CompanyRecord c, Soldier m)
    {
        if (CannotChooseSergeant(c, m) is string why) return why;
        c.SergeantPick.Clear();
        if (m != c.OfficerMan && c.OfficerMan is Soldier old)
        {
            if (old.Deeds.Count > 1 && old.Deeds[^1].StartsWith("Made sergeant")) old.Deeds.RemoveAt(old.Deeds.Count - 1);
            old.Deed($"Stood aside for {m.Name}.");
            c.Roll.Add(old);
            Rules.AddMen(c, old.Troop, 1);
            c.Roll.Remove(m);
            c.Stacks.First(s => s.Troop == m.Troop).Take(1);
            m.Deed($"Made sergeant of {c.Name} on day {Day}.");
            c.OfficerMan = m;
            c.Officer = "Sgt " + m.Name;
        }
        if (NatureTrait(m.Nature) is string t && !c.Traits.Contains(t))
        {
            if (c.Traits.Count >= 3) c.Traits.RemoveAt(0);
            c.Traits.Add(t);
        }
        Log($"{m.Name} is sergeant of {c.Name}.");
        return null;
    }

    // ---------- mention a man ----------

    public string? CannotMention(Soldier m)
    {
        if (!S.MentionPool.Contains(m.Id) || LivingMan(m.Id) == null) return S.MentionPool.Count == 0 ? "ONLY AFTER A WON BATTLE" : "HE WAS NOT IN IT";
        return S.Gold < B.MentionPurse ? $"NEEDS {B.MentionPurse} CROWNS" : null;
    }

    /// Mention a man of the day: a purse, +1 cohesion for his company, the mention in his deeds. One a battle.
    public string? Mention(Soldier m)
    {
        if (CannotMention(m) is string why) return why;
        S.Gold -= B.MentionPurse;
        S.MentionPool.Clear();
        m.Mentions++;
        var c = Player.Companies.First(k => k.Roll.Contains(m));
        c.Cohesion = MathF.Min(30, c.Cohesion + 1);
        m.Deed(OfBanner(m) && m.Mentions == BannerMentions ? "Mentioned a third time: one of the banner." : $"Mentioned by the captain on day {Day}.");
        Log($"You mention {m.Name} of {c.Name} before the company.");
        return null;
    }

    // ---------- give him arms ----------

    /// The things in your baggage you could give a man: pieces you do not wear (not horses).
    public IEnumerable<GearItem> Givable() => Captain.Baggage.Where(i => !Captain.Kit.Worn.Contains(i.Def) && Gear.Find(i.Def) is { Slot: not "horse" });

    /// Why this can not go to him: `what` is a baggage item, or null for a lot of looted arms.
    public string? CannotArm(Soldier m, GearItem? what)
    {
        if (LivingMan(m.Id) == null) return "HE IS NOT ON THE ROLL";
        if (m.Carries != "") return "HE IS ARMED ALREADY";
        if (what == null) return S.Goods.GetValueOrDefault("arms") < 1 ? "NO LOOTED ARMS" : null;
        if (!Captain.Baggage.Contains(what)) return "NOT IN YOUR BAGGAGE";
        return Captain.Kit.Worn.Contains(what.Def) ? "TAKE IT OFF FIRST" : null;
    }

    /// Arm a man: a piece of yours or a lot of looted arms. He dies less often when he falls, and it is lost if he does.
    public string? Arm(Soldier m, GearItem? what)
    {
        if (CannotArm(m, what) is string why) return why;
        if (what == null)
        {
            S.Goods["arms"] -= 1;
            if (S.Goods["arms"] <= 0) S.Goods.Remove("arms");
            m.Carries = "good looted arms"; m.CarriesDef = "arms";
            m.Deed("Given good arms from the spoils.");
        }
        else
        {
            Captain.Baggage.Remove(what);
            var g = Gear.Get(what.Def);
            m.Carries = what.Name != "" ? what.Name : $"the captain's {g.Name.ToLowerInvariant()}";
            m.CarriesDef = what.Def;
            m.Deed($"Carries {m.Carries}.");
        }
        Log($"{m.Name} is armed with {m.Carries}.");
        return null;
    }

    // ---------- send him home ----------

    public string? CannotPension(Soldier m)
    {
        if (LivingMan(m.Id) == null) return "HE IS NOT ON THE ROLL";
        if (Soldier.AgeOf(m) < PensionAge && m.Wounds < PensionWounds) return "HE CAN STILL FIGHT";
        return S.Gold < B.PensionCost ? $"NEEDS {B.PensionCost} CROWNS" : null;
    }

    /// Pension a man: he goes home alive (one man fewer in his stack), the chronicle writes it, and his village sends readier men.
    public string? Pension(Soldier m)
    {
        if (CannotPension(m) is string why) return why;
        var c = Player.Companies.First(k => k.Roll.Contains(m));
        S.Gold -= B.PensionCost;
        if (c.Stacks.FirstOrDefault(s => s.Troop == m.Troop) is TroopStack st)
        {
            if (m.Hurt && st.Wounded > 0) st.Wounded--;
            else st.Take(1);
        }
        string home = m.Home != "" ? m.Home : c.Home;
        m.Deed($"Went home{(home != "" ? $" to {home}" : "")} with a pension.");
        Lose(c, m, "pensioned", "");
        if (home != "" && !S.Pensioned.Contains(home)) S.Pensioned.Add(home);
        Deed($"{m.Name} of {c.Name} went home{(home != "" ? $" to {home}" : "")} with a pension after {N(m.Battles, "battle")}.");
        c.Prune();
        return null;
    }

    // ---------- move him ----------

    public string? CannotMoveMan(CompanyRecord from, Soldier m, CompanyRecord to)
    {
        if (from == to || !Player.Companies.Contains(to) || !from.Roll.Contains(m)) return "NOT THERE";
        if (m.Hurt) return "HE IS HURT";
        if (Rules.Room(to, m.Troop) < 1) return Rules.Fits(to, m.Troop) ? "NO ROOM IN THAT COMPANY" : "HE DOES NOT FIT THAT COMPANY";
        if (to.Roll.Count >= B.RollSize) return "ITS ROLL IS FULL";
        return from.Men <= 1 ? "HE IS ALL THAT IS LEFT OF IT" : null;
    }

    /// Move one named man (and his place in the ranks, with his share of drill) to another company.
    public string? MoveMan(CompanyRecord from, Soldier m, CompanyRecord to)
    {
        if (CannotMoveMan(from, m, to) is string why) return why;
        var st = from.Stacks.First(s => s.Troop == m.Troop);
        float xp = st.Men > 0 ? st.Xp / st.Men : 0;
        st.Take(1);
        Rules.AddMen(to, m.Troop, 1);
        to.Stack(m.Troop).AddXp(xp);
        from.Roll.Remove(m);
        to.Roll.Add(m);
        m.Deed($"Moved to {to.Name} on day {Day}.");
        from.Prune();
        return null;
    }
}
