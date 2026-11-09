namespace Ironfield;

/// Companions as people (docs/COMPANIONS_MAGIC.md B4, B5): they approve or not of what you do (their opinion of you, with
/// its memories as the reasons), quarrel, swear brotherhood, have a story to settle, teach a student, and may leave.
/// No dice here: quarrels come on a fixed day of each pair's fortnight, so the campaign's draws stay as they were.
public sealed partial class Campaign
{
    public const int SwornAfter = 5, StoryWinsNeeded = 3, StoryDays = 60, LeaveBelow = -30;

    /// How a companion takes a deed of yours, by his traits and calling (B4): (deed, who, weight, what he remembers).
    static readonly (string Deed, string Who, float Weight, string What)[] Reactions =
    {
        ("rob", "Honourable", -8, "you robbed travellers on the road"), ("rob", "Greedy", 3, "you took a rich purse"),
        ("rob", "Priest", -5, "you robbed pilgrims and merchants"),
        ("sell_prisoners", "Honourable", -4, "you sold men like cattle"), ("sell_prisoners", "Greedy", 2, "you got good gold for prisoners"),
        ("bury", "Pious", 5, "you buried the dead"), ("bury", "Priest", 6, "you gave the dead their rites"),
        ("craft", "Pious", -4, "you used hedge-craft"), ("craft", "Priest", -5, "you used hedge-craft"), ("craft", "Cunning", 2, "you let the old ways work"),
        ("won", "Reckless", 2, "a good fight, won"), ("won", "Sword", 2, "a good fight, won"), ("lost", "Ambitious", -3, "a defeat under you"),
        ("flame", "Pious", -10, "you took the black flame"), ("flame", "Priest", -12, "you took the black flame"), ("flame", "Honourable", -5, "you took the black flame"),
        ("feast", "Generous", 3, "you feasted the men"), ("feast", "Greedy", -1, "you wasted coin on a feast"),
    };

    /// Your companions react to a deed of yours (trait or calling).
    public void CompanionsReact(string deed)
    {
        foreach (var k in Companions.ToList())
            foreach (var (d, who, w, what) in Reactions)
                if (d == deed && (k.Traits.Contains(who) || k.Calling == who)) Remember(k, Me, what, w);
    }

    /// Companions who took the field together count it; at five battles two become sworn brothers (an aura when both lead).
    internal void BondsAfterBattle(List<Character> fought)
    {
        foreach (var a in fought)
            foreach (var b in fought)
            {
                if (a == b) continue;
                a.Together[b.Id] = a.Together.GetValueOrDefault(b.Id) + 1;
                if (a.Sworn < 0 && b.Sworn < 0 && a.Together[b.Id] >= SwornAfter && a.Id < b.Id && !Clash(a, b))
                {
                    a.Sworn = b.Id; b.Sworn = a.Id;
                    Log($"{a.Name} and {b.Name} have fought side by side {SwornAfter} times and swear brotherhood.");
                }
            }
    }

    static bool Clash(Character a, Character b) => TraitClashes.Any(x => a.Traits.Contains(x.A) && b.Traits.Contains(x.B) || a.Traits.Contains(x.B) && b.Traits.Contains(x.A));

    /// A sworn brother falls: the other grieves (he remembers it of you, and swears no other oath).
    void BrotherFell(Character k)
    {
        if (k.Sworn < 0 || CharacterById(k.Sworn) is not Character b || b.Dead) return;
        b.Sworn = -2;
        Remember(b, Me, $"my sworn brother {k.Name} fell under your banner", -6);
        Log($"{b.Name} grieves for his sworn brother {k.Name}.");
    }

    // ---------- his story ----------

    /// At level 3 he tells you his story (B4): something to settle that needs three victories with him at the head of his company.
    void OpenStory(Character k)
    {
        k.Story = 1; k.StoryWins = 0; k.StoryDay = Day;
        Log($"{k.Name} tells you of {StoryOf(k)}. Win three battles with him leading and it is settled.");
    }

    public static string StoryOf(Character k) => k.Calling switch
    {
        "Sword" => "a rival he swore to best",
        "Shield" => "a line he once broke and must never break again",
        "Banner" => "a family banner lost in a rout",
        "Bow" => "a brother lost to raiders in the hills",
        "Lance" => "a family castle taken from his father",
        "Priest" => "a vow to guard the faithful",
        "Cunning" => "a village that burnt his mother for a witch",
        _ => "an old wrong",
    };

    void StoryWin(Character k)
    {
        if (++k.StoryWins < StoryWinsNeeded) return;
        k.Story = 2;
        Remember(k, Me, "you helped me settle my story", 15);
        GainXp(k, Math.Max(10, XpToNext(k)));
        Log($"{k.Name}'s story is settled: {StoryOf(k)}. He is yours to the end.");
    }

    // ---------- his student ----------

    /// From level 4 he takes the first named man of his company as his student (B5).
    void TakeStudent(Character k)
    {
        var c = Player.Companies.FirstOrDefault(x => x.Id == k.CompanyId);
        if (c?.Roll.FirstOrDefault(m => !m.Hurt && !Companions.Any(o => o.Student == m.Id)) is not Soldier m) return;
        k.Student = m.Id;
        m.Deed($"Taught by {k.Name}.");
        Log($"{k.Name} takes {m.Name} of {c.Name} as his student.");
    }

    /// He fell: his student, if he is on the roll still, rises as a companion of the same calling with half his levels.
    internal void StudentRises(Character k)
    {
        if (k.Student == 0 || Companions.Count() >= MaxCompanions) return;
        var c = Player.Companies.FirstOrDefault(x => x.Roll.Any(m => m.Id == k.Student));
        if (c == null) return;
        var m = c.Roll.First(x => x.Id == k.Student);
        c.Roll.Remove(m);
        var ch = new Character { Id = S.NextId++, Kind = "companion", Name = m.Name, Faction = Captain.Contract, Culture = c.Culture == "" ? Me.Culture : c.Culture, Age = 20 + m.Battles };
        ch.Traits.AddRange(k.Traits.Take(1));
        ch.Role = k.Role; ch.Calling = k.Calling; ch.Temper = k.Temper; ch.Command = Math.Max(2, k.Command / 2);
        ch.Xp = k.Level >= 2 ? LevelXp[k.Level / 2 - 1] : 0; ch.Level = LevelFor(ch.Xp);
        ch.CompanyId = c.Id; ch.PartyId = Player.Id; ch.Ranks = m; ch.FromCompany = c.Id;
        ch.Opinion[S.CaptainChar] = 30;
        S.Characters.Add(ch);
        m.Deed($"Rose as a companion when {k.Name} fell.");
        Log($"{m.Name}, {k.Name}'s student, takes up his master's {k.Calling.ToLowerInvariant()} and joins your household.");
        RefreshHeroes();
    }

    // ---------- weekly: quarrels, stories that wait too long, leaving ----------

    internal void CompanionsWeekly()
    {
        var all = Companions.ToList();
        // Two whose traits clash quarrel on their own day of the fortnight.
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                if (Clash(all[i], all[j]) && (Day / 7 + all[i].Id + all[j].Id) % 2 == 0)
                {
                    Remember(all[i], all[j], "we quarrelled", -5);
                    Remember(all[j], all[i], "we quarrelled", -5);
                    Log($"{all[i].Name} and {all[j].Name} quarrel by the fire.");
                }
        foreach (var k in all)
        {
            if (k.Story == 1 && Day - k.StoryDay > StoryDays) { k.Story = 3; Remember(k, Me, "you never helped me settle my story", -15); Log($"{k.Name} speaks no more of {StoryOf(k)}."); }
            if (OpinionOfYou(k) >= LeaveBelow) { k.Warned = false; continue; }
            if (!k.Warned)
            {
                k.Warned = true;
                string why = MemoriesOf(k, Me).Where(m => m.Weight < 0).Select(m => m.What).FirstOrDefault() ?? "how things go";
                Log($"{k.Name} is unhappy ({why}). If nothing changes he will leave you.");
                continue;
            }
            Leave(k);
        }
    }

    /// He leaves on bad terms: out of your household, perhaps to turn up as an enemy champion.
    void Leave(Character k)
    {
        k.Kind = "gone";
        k.CompanyId = -1;
        k.PartyId = -1;
        k.Faction = S.Factions.Count == 0 ? -1 : (k.Id + Day) % S.Factions.Count;
        if (k.Sworn >= 0 && CharacterById(k.Sworn) is Character b) b.Sworn = -1;
        Log($"{k.Name} leaves your service.");
        RefreshHeroes();
    }
}
