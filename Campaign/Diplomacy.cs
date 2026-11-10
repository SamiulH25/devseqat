using Microsoft.Xna.Framework;

namespace Ironfield;

/// What a peace settles (CAMPAIGN_OVERHAUL.md 7.2): a plain truce, gold over four weeks, a seat handed over, or vassalage.
public enum Terms { White, Gold, Cede, Tributary }

/// Strategic war and diplomacy (step F7): war exhaustion, peace with terms, wars declared on purpose, tributaries, alliances,
/// marshal-led hosts and the summons.
public sealed partial class Campaign
{
    public const float TributeDays = 90, AllianceDays = 60, SummonsDays = 4;

    // ---------- exhaustion ----------

    public float ExhaustionOf(War w, int f) => w.A == f ? w.ExhA : w.ExhB;
    public float ScoreOf(War w, int f) => w.A == f ? w.ScoreA : w.ScoreB;

    void AddExhaustion(War w, int f, float x)
    {
        if (w.A == f) w.ExhA = Math.Clamp(w.ExhA + x, 0, 100); else w.ExhB = Math.Clamp(w.ExhB + x, 0, 100);
    }

    /// A realm's worst exhaustion in any war it fights (for the realm screen).
    public float Exhaustion(int f) => S.Wars.Where(w => w.Active && (w.A == f || w.B == f)).Select(w => ExhaustionOf(w, f)).DefaultIfEmpty(0).Max();

    /// How far `w` (the winner) is ahead of `l` in their war: score and the other's weariness. Above 0.35 it can ask for gold,
    /// above 0.9 for a seat, above 1.6 for vassalage.
    public float Margin(War war, int w, int l) => (ScoreOf(war, w) - ScoreOf(war, l)) / 1500f + (ExhaustionOf(war, l) - ExhaustionOf(war, w)) / 40f;

    int Seats(int f) => S.Settlements.Count(s => s.Faction == f && s.Kind != SettlementKind.Village);

    /// The best terms the winner of a war can impose on the loser.
    public Terms BestTerms(War war, int w, int l)
    {
        float m = Margin(war, w, l);
        if (m >= 1.6f && Seats(l) >= 3 && S.Factions[l].TributaryOf < 0 && !(S.Factions[w].TributaryOf == l)) return Terms.Tributary;
        if (m >= 0.9f && Seats(l) >= 2) return Terms.Cede;
        if (m >= 0.35f) return Terms.Gold;
        return Terms.White;
    }

    public static string TermsText(Terms t) => t switch
    {
        Terms.Gold => "GOLD OVER FOUR WEEKS", Terms.Cede => "A SEAT HANDED OVER", Terms.Tributary => "VASSALAGE AND TRIBUTE", _ => "A PLAIN TRUCE",
    };

    // ---------- making peace ----------

    /// End a war: `w` imposes `terms` on `l`; a truce follows. Returns a line for the record.
    string MakePeace(War war, int w, int l, Terms terms)
    {
        string what = "";
        switch (terms)
        {
            case Terms.Gold:
            {
                float total = MathF.Round(MathF.Min(1600, MathF.Max(200, PurseOf(l) * 0.5f)) / 10) * 10;
                S.Payments.Add(new Payment { From = l, To = w, Weeks = 4, PerWeek = total / 4 });
                what = $"pay {total:0} crowns over four weeks";
                break;
            }
            case Terms.Cede:
            {
                var theirs = S.Settlements.Where(s => s.Faction == l && s.Kind != SettlementKind.Village).ToList();
                var ours = S.Settlements.Where(s => s.Faction == w && s.Kind != SettlementKind.Village).ToList();
                var seat = theirs.OrderBy(s => ours.Count == 0 ? 0 : ours.Min(o => Vector2.Distance(o.Pos, s.Pos))).First();
                seat.Garrison.Clear();
                Capture(seat, w, -1);
                what = $"hand over {seat.Name}";
                break;
            }
            case Terms.Tributary:
                S.Factions[l].TributaryOf = w;
                S.Factions[l].TributeUntil = (float)S.Hours + TributeDays * 24;
                what = $"become their tributary for {TributeDays:0} days";
                break;
        }
        war.Active = false;
        war.TruceUntil = (float)S.Hours + B.TruceDays * 24;
        war.ScoreA = war.ScoreB = 0;
        war.ExhA = war.ExhB = 0;
        // Captive lords of the loser held by you go home with the peace when you are the winner's king.
        string line = terms == Terms.White
            ? $"The {FactionNames[w]} and the {FactionNames[l]} make peace: a truce of {B.TruceDays:0} days."
            : $"The {FactionNames[l]} sue the {FactionNames[w]} for peace and {what}.";
        Treaty(line);
        return line;
    }

    void Treaty(string line)
    {
        Log(line);
        S.Treaties.Add($"Day {Day}: {line}");
        if (S.Treaties.Count > 20) S.Treaties.RemoveAt(0);
    }

    /// A realm's purse: your gold when it is your realm, else its treasury.
    float PurseOf(int f) => RulerOf(f) == Me && Rank == Rank.King ? S.Gold : S.Factions[f].Treasury;

    /// What a realm can spare for payments: an AI realm keeps a 300-crown floor in its chest, as for its lords' wages.
    float Spare(int f) => RulerOf(f) == Me && Rank == Rank.King ? MathF.Max(0, S.Gold) : MathF.Max(0, S.Factions[f].Treasury - 300);

    void Pay(int f, float amount)
    {
        if (RulerOf(f) == Me && Rank == Rank.King) S.Gold += amount; else S.Factions[f].Treasury += amount;
    }

    // ---------- the daily round of war and peace ----------

    /// Daily: wars wear realms down; the weary sue for peace on the winner's terms; realms at rest pick new wars; tributaries
    /// follow their overlords; alliances lapse; summons are answered or ignored.
    void DiplomacyDaily()
    {
        foreach (var w in S.Wars.ToList())
        {
            bool aDead = !Alive(w.A), bDead = !Alive(w.B);
            if (!w.Active) continue;
            if (aDead || bDead) { w.Active = false; w.TruceUntil = (float)S.Hours + B.TruceDays * 24; w.ExhA = w.ExhB = 0; continue; }
            AddExhaustion(w, w.A, 0.12f);
            AddExhaustion(w, w.B, 0.12f);
            if (S.Hours - w.Since < 20 * 24 || LeaderWar(w)) continue;
            // You decide your own realm's peace (from the realm screen), though a weary enemy may sue for it (S7); the AI decides the rest.
            if (RulerOf(w.A) == Me || RulerOf(w.B) == Me) { SuePeace(w); continue; }
            float strengthA = Rules.PerMan(Strength(w.A) / 1500) - w.ExhA / 40, strengthB = Rules.PerMan(Strength(w.B) / 1500) - w.ExhB / 40;
            int win = w.ScoreA / 1500 - w.ExhA / 40 >= w.ScoreB / 1500 - w.ExhB / 40 ? w.A : w.B, lose = win == w.A ? w.B : w.A;
            // A realm that takes terms readily (its temper's "tribute", W5) feels the war sooner.
            float le = ExhaustionOf(w, lose) * Factions.Get(lose).Tribute, we = ExhaustionOf(w, win);
            bool allied = S.Pacts.Any(p => p.Until > S.Hours && (p.A == lose || p.B == lose) && FactionsAtWar(p.A == lose ? p.B : p.A, win));
            bool wants = le > (allied ? 70 : 50) || le > 30 && Margin(w, win, lose) > 0.5f || we > 60 && le > 40;
            if (wants && rng.Chance(0.3f)) MakePeace(w, win, lose, BestTerms(w, win, lose));
        }
        // Realms at rest look for a war: the ruler's temper, their strength and old grudges decide.
        for (int f = 0; f < S.Factions.Count; f++)
        {
            if (!Alive(f) || RulerOf(f) == Me && Rank == Rank.King || S.Factions[f].TributaryOf >= 0) continue;
            int wars = S.Wars.Count(w => w.Active && (w.A == f || w.B == f));
            if (wars >= 2) continue;
            var ruler = RulerOf(f);
            float chance = (wars == 0 ? 0.05f : 0.01f) * (ruler?.Traits.Contains("Ambitious") == true || ruler?.Traits.Contains("Reckless") == true ? 2 : 1)
                * (ruler?.Traits.Contains("Cautious") == true ? 0.5f : 1) * Factions.Get(f).Aggression;
            if (!rng.Chance(chance)) continue;
            // Only a neighbour (W5): with seven realms a war across the map is a war nobody can fight.
            var target = Enumerable.Range(0, S.Factions.Count)
                .Where(g => g != f && Alive(g) && !FactionsAtWar(f, g) && (WarBetween(f, g)?.TruceUntil ?? 0) <= S.Hours
                    && S.Factions[g].TributaryOf != f && S.Factions[f].TributaryOf != g && !Allied(f, g) && Strength(f) > Strength(g) * 0.8f && Borders(f, g))
                .OrderByDescending(g => WarAppeal(f, g)).Cast<int?>().FirstOrDefault();
            if (target is int t) DeclareWarBetween(f, t);
        }
        AlliancesDaily();
        // Tributaries march with their overlords and are freed when the term ends.
        foreach (var fs in S.Factions.Where(fs => fs.TributaryOf >= 0))
        {
            int over = fs.TributaryOf;
            if (S.Hours >= fs.TributeUntil || !Alive(over) || !Alive(fs.Id))
            {
                fs.TributaryOf = -1;
                if (Alive(fs.Id)) Treaty($"The {FactionNames[fs.Id]} are no longer tributary to the {FactionNames[over]}.");
                continue;
            }
            if (FactionsAtWar(fs.Id, over)) { var w = WarBetween(fs.Id, over)!; w.Active = false; w.TruceUntil = fs.TributeUntil; }
            foreach (var g in Enumerable.Range(0, S.Factions.Count).Where(g => g != fs.Id && g != over && FactionsAtWar(over, g) && !FactionsAtWar(fs.Id, g)))
                if (Alive(g)) DeclareWarBetween(fs.Id, g);
        }
        S.Pacts.RemoveAll(p => p.Until <= S.Hours || !Alive(p.A) || !Alive(p.B));
        SummonsDaily();
    }

    /// The great coalition war against a realm grown too strong is not settled by terms.
    bool LeaderWar(War w) => Leading(w.A) || Leading(w.B);

    /// The share of the seats that unites the other realms against one: 45% of three realms' seats, less with more realms
    /// (30% of seven), so no realm runs away with the map (docs/archive/WORLD.md W5).
    public float LeaderShare
    {
        get
        {
            int alive = Enumerable.Range(0, S.Factions.Count).Count(Alive);
            return alive <= 3 ? 0.45f : MathF.Max(0.3f, 2.1f / alive);
        }
    }

    /// Does this realm hold enough of the seats to unite the others against it?
    public bool Leading(int f)
    {
        if (S.Factions.Count <= 2 || f < 0) return false;
        int seats = S.Settlements.Count(s => s.Kind != SettlementKind.Village);
        return Seats(f) >= seats * LeaderShare;
    }

    /// How much a realm wants a war with a neighbour: a long border, a weaker foe, and one already busy with other wars.
    float WarAppeal(int f, int g) =>
        MathF.Min(BorderLength(f, g), 6) / 6f + MathF.Min(2, Strength(f) / MathF.Max(1, Strength(g))) * 0.5f
        + 0.2f * S.Wars.Count(w => w.Active && (w.A == g || w.B == g));

    /// Provinces of one realm that touch the other's.
    int BorderLength(int a, int b) => S.Provinces.Count(p => p.Faction == a && p.Neighbors.Any(n => S.Provinces[n].Faction == b));

    /// Two realms at war with the same stronger realm tend to ally against it (F7, W5). You make your own realm's alliances.
    void AlliancesDaily()
    {
        for (int a = 0; a < S.Factions.Count; a++)
            for (int b = a + 1; b < S.Factions.Count; b++)
            {
                if (!Alive(a) || !Alive(b) || Allied(a, b) || FactionsAtWar(a, b) || S.Factions[a].TributaryOf >= 0 || S.Factions[b].TributaryOf >= 0) continue;
                if (Rank == Rank.King && (a == Captain.Contract || b == Captain.Contract)) continue;
                float stronger = MathF.Max(Strength(a), Strength(b));
                int foe = Enumerable.Range(0, S.Factions.Count).FirstOrDefault(c => c != a && c != b && FactionsAtWar(a, c) && FactionsAtWar(b, c) && Strength(c) > stronger, -1);
                if (foe < 0 || !rng.Chance(0.05f)) continue;
                S.Pacts.Add(new Pact { A = a, B = b, Until = (float)S.Hours + AllianceDays * 24 });
                Treaty($"The {FactionNames[a]} and the {FactionNames[b]} ally against the {FactionNames[foe]} for {AllianceDays:0} days.");
            }
    }

    float Strength(int f) => RealmStrength(f);

    public bool Allied(int a, int b) => S.Pacts.Any(p => p.Until > S.Hours && (p.A == a && p.B == b || p.A == b && p.B == a));

    bool Borders(int a, int b) => S.Provinces.Any(p => p.Faction == a && p.Neighbors.Any(n => S.Provinces[n].Faction == b));

    /// Weekly: gold owed under terms changes hands; tributaries pay a quarter of their income.
    void TributeWeekly()
    {
        foreach (var p in S.Payments)
        {
            float amt = MathF.Min(p.PerWeek, Spare(p.From));
            Pay(p.From, -amt);
            Pay(p.To, amt);
            p.Weeks--;
        }
        S.Payments.RemoveAll(p => p.Weeks <= 0);
        foreach (var fs in S.Factions.Where(fs => fs.TributaryOf >= 0 && Alive(fs.Id)))
        {
            float due = S.Settlements.Where(t => t.Faction == fs.Id).Sum(Income) * 0.25f;
            due = MathF.Min(due, Spare(fs.Id));
            Pay(fs.Id, -due);
            Pay(fs.TributaryOf, due);
        }
    }

    /// Share of the provinces a realm holds with its tributaries: what winning counts (5.5).
    public float RealmShare(int f) => f < 0 ? 0 : S.Provinces.Count(p => p.Faction == f || p.Faction >= 0 && p.Faction < S.Factions.Count && S.Factions[p.Faction].TributaryOf == f)
        / (float)Math.Max(1, S.Provinces.Count);

    // ---------- a king's diplomacy ----------

    /// As king: offer peace on terms. They accept what their position forces on them: a plain truce if they are weary or behind,
    /// harsher terms only as far as you are ahead. Freeing their captive lords sweetens it.
    public string? OfferPeace(int target, Terms terms)
    {
        if (Rank != Rank.King) return "ONLY A KING MAKES PEACE";
        int me = Captain.Contract;
        var w = WarBetween(me, target);
        if (w is not { Active: true }) return "YOU ARE NOT AT WAR WITH THEM";
        var held = Player.Captives.Select(PartyById).Where(p => p != null && p.Faction == target).ToList();
        float sweet = held.Count * 0.15f;
        float m = Margin(w, me, target) + sweet + (Factions.Get(target).Tribute - 1) * 0.4f; // some realms take terms sooner (W5)
        float need = terms switch { Terms.Tributary => 1.6f, Terms.Cede => 0.9f, Terms.Gold => 0.35f, _ => -0.5f };
        if (terms == Terms.White && ExhaustionOf(w, target) < 20 && m < -0.5f) return "THEY WILL NOT TALK WHILE THEY ARE WINNING";
        if (m < need) return $"THEY REFUSE {TermsText(terms)} (YOUR STANDING {m:+0.00;-0.00}, NEEDS {need:+0.00;-0.00})";
        if (terms == Terms.Cede && Seats(target) < 2) return "IT WOULD LEAVE THEM NOTHING";
        if (terms == Terms.Tributary && (Seats(target) < 3 || S.Factions[target].TributaryOf >= 0)) return "THEY WILL NOT KNEEL";
        foreach (var p in held) { Player.Captives.Remove(p!.Id); p.Captive = false; p.DownUntil = (float)S.Hours + 24; }
        MakePeace(w, me, target, terms);
        return null;
    }

    public string? OfferPeace(int target) => OfferPeace(target, Terms.White);

    /// A weary enemy sues you, the king, for peace (S7): the forced event "peace_offer", at most every 20 days.
    void SuePeace(War w)
    {
        int me = Captain.Contract, them = w.A == me ? w.B : w.A;
        if (Rank != Rank.King || RulerOf(them) is not Character ruler || S.Event != null) return;
        if (S.EventsFired.TryGetValue("peace_offer", out float at) && S.Hours - at < 20 * 24) return;
        float tired = ExhaustionOf(w, them);
        if (!(tired > 50 || tired > 30 && Margin(w, me, them) > 0.5f) || !rng.Chance(0.25f)) return;
        Force("peace_offer", new PendingEvent { Lord = ruler.Id });
    }

    /// Their offer accepted: a truce, and gold besides if you are well ahead.
    string? AcceptPeace(int them)
    {
        int me = Captain.Contract;
        if (WarBetween(me, them) is not { Active: true } w) return "The war is already over.";
        var terms = Margin(w, me, them) > 0.35f && Spare(them) > 100 ? Terms.Gold : Terms.White;
        MakePeace(w, me, them, terms);
        return terms == Terms.Gold ? "They pay to end it." : "The war is over.";
    }

    /// As king: an alliance against an enemy you share; their ruler must not think ill of you.
    public string? ProposeAlliance(int target)
    {
        if (Rank != Rank.King) return "ONLY A KING MAKES ALLIANCES";
        int me = Captain.Contract;
        if (target == me || !Alive(target)) return "NO SUCH REALM";
        if (FactionsAtWar(me, target)) return "MAKE PEACE FIRST";
        if (Allied(me, target)) return "ALREADY ALLIES";
        bool shared = Enumerable.Range(0, S.Factions.Count).Any(g => FactionsAtWar(me, g) && FactionsAtWar(target, g));
        if (!shared) return "YOU SHARE NO ENEMY";
        if (RulerOf(target) is Character r && OpinionOfYou(r) < -10) return $"{r.Name.ToUpperInvariant()} DOES NOT TRUST YOU";
        S.Pacts.Add(new Pact { A = me, B = target, Until = (float)S.Hours + AllianceDays * 24 });
        Treaty($"The {FactionNames[me]} and the {FactionNames[target]} ally for {AllianceDays:0} days.");
        return null;
    }

    // ---------- hosts and summons (7.1) ----------

    /// The realm raises a host against a province: lords who think well enough of the marshal answer; if you are a vassal you
    /// are summoned too. Returns how many came.
    int MusterHost(int f, Province target, List<Party> idle)
    {
        var marshal = MarshalOf(f);
        int came = 0;
        foreach (var l in idle.Take(6))
        {
            if (marshal != null && CharacterOf(l) is Character ch && ch != marshal && OpinionOf(ch, marshal) < -20) continue; // he stays home
            l.PatrolProvince = target.Id;
            l.TaskUntil = 0;
            came++;
        }
        if (came > 0) Log($"The {FactionNames[f]} musters a host of {came} lords against {target.Name}.");
        // Summon you, if you are sworn to the realm and do not lead it.
        if (f == Captain.Contract && Captain.Fealty && Rank is Rank.Vassal or Rank.Lord && S.Summons == null && S.Hours - S.LastSummons > 7 * 24
            && marshal != null && S.Settlements.FirstOrDefault(s => s.Province == target.Id && s.Kind != SettlementKind.Village) is Settlement seat)
        {
            S.Summons = new Summons { Faction = f, Seat = seat.Id, From = marshal.Id, Until = (float)S.Hours + SummonsDays * 24 };
            S.LastSummons = (float)S.Hours;
            Beat = ("THE MARSHAL SUMMONS YOU", new[] { $"{marshal.Name.ToUpperInvariant()} CALLS THE HOST TO {seat.Name.ToUpperInvariant()}.", $"COME WITHIN {SummonsDays:0} DAYS. ANSWERING WINS HIS FAVOUR. IGNORING HIM COSTS IT." });
        }
        return came;
    }

    /// Hourly: you answer the summons by reaching the host.
    void SummonsHourly()
    {
        if (S.Summons is not Summons sm) return;
        var seat = SettlementById(sm.Seat);
        if (Vector2.Distance(Player.Pos, seat.Pos) > 35 * Map.Spread) return;
        if (CharacterById(sm.From) is Character m) Remember(m, Me, "you answered my summons", 8);
        Log($"You join the host before {seat.Name}.");
        S.Summons = null;
    }

    /// Daily: a summons not answered in time is remembered.
    void SummonsDaily()
    {
        if (S.Summons is not Summons sm || S.Hours < sm.Until) return;
        if (CharacterById(sm.From) is Character m) Remember(m, Me, "you ignored my summons", -10);
        Log("You did not answer the marshal's summons.");
        S.Summons = null;
    }

    /// As marshal or king: summon the realm's lords to march on your objective.
    public string? CallHost()
    {
        if (Rank < Rank.Marshal) return "ONLY THE MARSHAL CALLS THE HOST";
        int f = Captain.Contract;
        if (ObjectiveOf(f) is not Settlement obj) return "NAME AN OBJECTIVE FIRST";
        var lords = S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction == f && !p.Down && !(IsVassal(p) && p.Order == "defend")).ToList();
        int came = 0, refused = 0;
        foreach (var l in lords)
        {
            if (CharacterOf(l) is Character ch && OpinionOfYou(ch) < -20) { refused++; continue; }
            l.PatrolProvince = obj.Province;
            l.TaskUntil = 0;
            came++;
        }
        if (came == 0) return refused > 0 ? "NO LORD WILL FOLLOW YOU" : "THE REALM HAS NO LORDS IN THE FIELD";
        Log($"You summon the host to {obj.Name}: {came} lords answer{(refused > 0 ? $", {refused} stay home" : "")}.");
        return null;
    }
}
