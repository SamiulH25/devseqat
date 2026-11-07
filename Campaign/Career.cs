using Microsoft.Xna.Framework;

namespace Ironfield;

/// The captain's career ladder (CAMPAIGN_OVERHAUL.md 5.2), lowest first. Each rank changes what you can do.
public enum Rank { FreeCompany, SwornSword, Vassal, Lord, Marshal, King }

/// Career ranks, the realm council, founding a realm, and winning or losing the campaign (step F5).
public sealed partial class Campaign
{
    public const float SwornDays = 30, VassalRenown = 120, VassalOpinion = 10, LordRenown = 350, MarshalRenown = 500, CouncilDays = 14, WinShare = 0.6f;
    public const int LordFiefs = 2, MarshalTerm = 4;

    /// A career beat or the council's word for the map screen to show once (not saved): a title and a few lines.
    public (string Title, string[] Lines)? Beat;

    // ---------- rank ----------

    /// Your rank, read from the state of the world: a king rules a realm, a marshal leads one's hosts, a lord is a vassal
    /// with land and renown, a vassal has sworn fealty, a sworn sword has served one realm for a month.
    public Rank Rank
    {
        get
        {
            int f = Captain.Contract;
            if (f < 0 || f >= S.Factions.Count) return Rank.FreeCompany;
            var fs = S.Factions[f];
            if (fs.Ruler == S.CaptainChar) return Rank.King;
            if (Captain.Fealty)
            {
                if (fs.Marshal == S.CaptainChar) return Rank.Marshal;
                return FiefSeats >= LordFiefs && Captain.Renown >= LordRenown ? Rank.Lord : Rank.Vassal;
            }
            return S.Hours - Captain.ContractSince >= SwornDays * 24 ? Rank.SwornSword : Rank.FreeCompany;
        }
    }

    public static string RankName(Rank r) => r switch
    {
        Rank.FreeCompany => "FREE COMPANY", Rank.SwornSword => "SWORN SWORD", Rank.Vassal => "VASSAL",
        Rank.Lord => "LORD", Rank.Marshal => "MARSHAL", _ => "KING",
    };

    /// How each rank is reached and what it opens, for the realm screen's ladder.
    public static readonly (Rank Rank, string How, string Opens)[] Ladder =
    {
        (Rank.FreeCompany, "THE START", "CONTRACTS WITH ANY REALM, BRIGAND HUNTING"),
        (Rank.SwornSword, $"SERVE ONE REALM {SwornDays:0} DAYS", "ITS RECRUITS AT HALF PRICE"),
        (Rank.Vassal, $"SWEAR FEALTY: {VassalRenown:0} RENOWN, LIEGE OPINION {VassalOpinion:+0}", "HOLD FIEFS, A VOTE IN COUNCIL"),
        (Rank.Lord, $"HOLD {LordFiefs} FIEFS, {LordRenown:0} RENOWN", "PROPOSE WARS, DECLARE INDEPENDENCE"),
        (Rank.Marshal, "THE COUNCIL'S VOTE", "SET THE REALM'S OBJECTIVE"),
        (Rank.King, "FOUND A REALM", "WAR AND PEACE, YOUR OWN COUNCIL"),
    };

    /// Towns and castles you hold (villages come with them).
    public int FiefSeats => Fiefs.Count(t => t.Kind != SettlementKind.Village);

    /// The realm you serve, if any.
    public FactionState? Realm => Captain.Contract >= 0 && Captain.Contract < S.Factions.Count ? S.Factions[Captain.Contract] : null;

    public Character? RulerOf(int f) => f >= 0 && f < S.Factions.Count ? CharacterById(S.Factions[f].Ruler) : null;
    public Character? MarshalOf(int f) => f >= 0 && f < S.Factions.Count ? CharacterById(S.Factions[f].Marshal) : null;

    /// The next rank on the road from here and what it still needs. At the top (marshal or king) it is the current rank and
    /// what winning needs; null once the war is won.
    public (Rank Rank, string Needs)? NextRank()
    {
        var r = Rank;
        switch (r)
        {
            case Rank.FreeCompany:
                if (Captain.Contract < 0) return (Rank.SwornSword, "SIGN WITH A REALM AND SERVE IT A MONTH");
                float days = SwornDays - (float)(S.Hours - Captain.ContractSince) / 24;
                return (Rank.SwornSword, $"{MathF.Ceiling(days):0} MORE DAYS IN THE {FactionNames[Captain.Contract].ToUpperInvariant()}'S SERVICE");
            case Rank.SwornSword:
                CanSwearFealty(out string why);
                return (Rank.Vassal, why == "" ? "SWEAR FEALTY" : why);
            case Rank.Vassal:
                var need = new List<string>();
                if (FiefSeats < LordFiefs) need.Add($"{LordFiefs - FiefSeats} MORE FIEF{(LordFiefs - FiefSeats == 1 ? "" : "S")}");
                if (Captain.Renown < LordRenown) need.Add($"{LordRenown - Captain.Renown:0} MORE RENOWN");
                return (Rank.Lord, string.Join(" AND ", need));
            case Rank.Lord:
                return (Rank.Marshal, Captain.Renown < MarshalRenown
                    ? $"{MarshalRenown - Captain.Renown:0} MORE RENOWN TO STAND FOR MARSHAL, OR DECLARE INDEPENDENCE FROM A TOWN YOU HOLD"
                    : "WIN THE COUNCIL'S VOTE, OR DECLARE INDEPENDENCE FROM A TOWN YOU HOLD");
            default:
                return S.Won ? null : (r, $"HOLD {Pct(WinShare)} OF THE PROVINCES ({Pct(RealmShare(Captain.Contract))} NOW)");
        }
    }

    public static string Pct(float x) => $"{x * 100:0}%";

    /// Share of all provinces a realm holds.
    public float ProvinceShare(int f) => f < 0 ? 0 : S.Provinces.Count(p => p.Faction == f) / (float)Math.Max(1, S.Provinces.Count);

    // ---------- fealty ----------

    public bool CanSwearFealty(out string why)
    {
        var ruler = RulerOf(Captain.Contract);
        why = Captain.Contract < 0 ? "SIGN WITH A REALM FIRST"
            : Captain.Fealty || Rank == Rank.King ? "YOU HAVE ALREADY SWORN"
            : Rank < Rank.SwornSword ? $"SERVE THE REALM {SwornDays:0} DAYS FIRST"
            : Captain.Renown < VassalRenown ? $"NEEDS {VassalRenown:0} RENOWN ({Captain.Renown:0})"
            : ruler == null ? "THE REALM HAS NO RULER TO SWEAR TO"
            : OpinionOfYou(ruler) < VassalOpinion ? $"{ruler.Name.ToUpperInvariant()} DOES NOT TRUST YOU ({OpinionOfYou(ruler):+0;-0;0}, NEEDS {VassalOpinion:+0})"
            : "";
        return why == "";
    }

    /// Kneel to your liege: you become a vassal, who may hold land and vote in the council.
    public string? SwearFealty()
    {
        if (!CanSwearFealty(out string why)) return why;
        Captain.Fealty = true;
        var ruler = RulerOf(Captain.Contract)!;
        Remember(ruler, Me, "you swore fealty to me", 10);
        Log($"You swear fealty to {ruler.Name} of the {FactionNames[Captain.Contract]}.");
        CheckRank();
        return null;
    }

    /// Leaving a realm you served (by contract or oath): an oath broken is remembered; a marshal's baton passes on.
    void LeaveRealm(int f)
    {
        var fs = S.Factions[f];
        if (fs.Marshal == S.CaptainChar) fs.Marshal = -1;
        fs.Pending.RemoveAll(g => g.Captor == S.CaptainChar);
        if (Captain.Fealty && RulerOf(f) is Character ruler)
        {
            Remember(ruler, Me, "you broke your oath to me", -25);
            Log($"You renounce your oath to {ruler.Name}.");
        }
        Captain.Fealty = false;
    }

    // ---------- fiefs on capture ----------

    /// Who holds a seat just taken by `f` (returns the owning party id, -1 = the realm for now). Your first fief as a vassal
    /// is your liege's gift at once; later ones, and every seat the lords take, wait for the council. A king keeps what he takes.
    int GrantOnCapture(Settlement s, int f, int owner, int captor)
    {
        if (s.Kind == SettlementKind.Village || f < 0 || f >= S.Factions.Count) return owner;
        var fs = S.Factions[f];
        fs.Pending.RemoveAll(g => g.Seat == s.Id);
        if (captor == S.CaptainChar && f == Captain.Contract)
        {
            if (Rank == Rank.King) return Player.Id;
            if (Captain.Fealty && FiefSeats == 0) return Player.Id;
            fs.Pending.Add(new Grant { Seat = s.Id, Captor = captor });
            if (!Captain.Fealty)
            {
                fs.Pending[^1].Captor = -1;
                float purse = s.Kind == SettlementKind.Town ? 300 : 200;
                S.Gold += purse;
                Log($"The {FactionNames[f]} pay you a captor's purse of {purse:0} crowns for {s.Name}.");
            }
            return -1;
        }
        fs.Pending.Add(new Grant { Seat = s.Id, Captor = captor });
        return -1;
    }

    // ---------- rulers and the council (5.3) ----------

    /// Every realm has a ruler and a marshal: when one dies, is taken, or leaves, the next lord of the realm steps up.
    internal void EnsureRulers()
    {
        for (int f = 0; f < S.Factions.Count; f++)
        {
            var fs = S.Factions[f];
            if (!Alive(f)) continue;
            var lords = S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == f).OrderBy(c => c.Id).ToList();
            bool Fit(int id) => id == S.CaptainChar ? Captain.Contract == f : lords.Any(c => c.Id == id);
            bool founding = fs.Ruler < 0;
            if (!Fit(fs.Ruler))
            {
                // A house goes on: the heir of the late ruler's party if there is one, else the eldest lord.
                var old = CharacterById(fs.Ruler);
                var heir = old != null ? lords.FirstOrDefault(c => c.PartyId == old.PartyId && c.PartyId >= 0) : null;
                heir ??= lords.OrderByDescending(c => c.Age).FirstOrDefault();
                if (heir == null) continue;
                fs.Ruler = heir.Id;
                if (!founding) Log($"{heir.Name} is the new ruler of the {FactionNames[f]}.");
            }
            // The first marshal is named with the realm; after that a vacant baton calls the council within a day.
            if (founding) fs.Marshal = lords.FirstOrDefault(c => c.Id != fs.Ruler)?.Id ?? fs.Ruler;
            else if (fs.Marshal >= 0 && (!Fit(fs.Marshal) || (fs.Marshal == S.CaptainChar && !Captain.Fealty && fs.Ruler != S.CaptainChar))) fs.Marshal = -1;
            if (fs.Marshal < 0 && fs.Ruler != S.CaptainChar) fs.NextCouncil = MathF.Min(fs.NextCouncil, (float)S.Hours + 24);
        }
    }

    /// Daily: rulers, councils that are due, rank beats, and the campaign's end.
    void CareerDaily()
    {
        EnsureRulers();
        for (int f = 0; f < S.Factions.Count; f++)
            if (Alive(f) && S.Hours >= S.Factions[f].NextCouncil) Council(f);
        CheckRank();
        CheckEnd();
    }

    /// The lords who sit in a realm's council (free, alive, with a party), with their votes' weight: one, plus one per fief.
    List<(Character Who, float Weight)> Voters(int f)
    {
        var list = new List<(Character, float)>();
        foreach (var c in S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == f))
        {
            var p = PartyById(c.PartyId);
            if (p == null || p.Captive) continue;
            float w = 1 + S.Settlements.Count(t => t.Owner == p.Id && t.Kind != SettlementKind.Village);
            if (c.Id == S.Factions[f].Ruler) w *= 3; // the liege's voice counts three times
            list.Add((c, w));
        }
        if (Captain.Contract == f && Captain.Fealty && Rank != Rank.King) list.Add((Me, 1 + FiefSeats));
        return list;
    }

    /// Seats a character's party holds.
    int SeatsOf(Character c)
    {
        int party = c.Id == S.CaptainChar ? Player.Id : c.PartyId;
        return party < 0 ? 0 : S.Settlements.Count(t => t.Owner == party && t.Kind != SettlementKind.Village);
    }

    /// A weighted vote among candidates; `score(voter, candidate)` is how much the voter likes each. You vote for yourself
    /// when you stand, else for the candidate who thinks best of you.
    Character? Vote(int f, IReadOnlyList<Character> candidates, Func<Character, Character, float> score, out Dictionary<int, float> tally)
    {
        tally = candidates.ToDictionary(c => c.Id, _ => 0f);
        if (candidates.Count == 0) return null;
        foreach (var (who, weight) in Voters(f))
        {
            Character pick = who == Me
                ? candidates.Contains(Me) ? Me : candidates.OrderByDescending(c => OpinionOfYou(c)).First()
                : candidates.OrderByDescending(c => score(who, c)).ThenBy(c => c.Id).First();
            tally[pick.Id] += weight;
        }
        var t = tally;
        return candidates.OrderByDescending(c => t[c.Id]).ThenBy(c => c.Id).First();
    }

    float Liking(Character voter, Character cand) => cand == voter ? 30 : OpinionOf(voter, cand);

    /// The council sits (5.3): it grants captured seats, elects a marshal when the baton is vacant or the term is up, sets the
    /// war objective, and votes on a motion of war. Lords passed over think less of their liege.
    void Council(int f)
    {
        var fs = S.Factions[f];
        fs.NextCouncil = (float)S.Hours + CouncilDays * 24;
        fs.Councils++;
        var said = new List<string>();
        bool mine = Captain.Contract == f;
        bool touched = false;
        var ruler = CharacterById(fs.Ruler);

        // 1. Fiefs. In your own realm the king decides: what you took is yours, what a vassal took is his.
        foreach (var g in fs.Pending.ToList())
        {
            fs.Pending.Remove(g);
            var seat = SettlementById(g.Seat);
            if (seat.Faction != f || seat.Owner >= 0) continue;
            var captor = CharacterById(g.Captor);
            Character? winner;
            if (fs.Ruler == S.CaptainChar) winner = captor is { Kind: "lord" } ? captor : Me;
            else
            {
                var cands = S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == f && PartyById(c.PartyId) is { Captive: false })
                    .OrderBy(SeatsOf).ThenBy(c => c.Id).Take(2).ToList();
                if (captor != null && !cands.Contains(captor) && (captor != Me || Captain.Fealty)) cands.Add(captor);
                if (mine && Captain.Fealty && !cands.Contains(Me)) cands.Add(Me);
                winner = Vote(f, cands, (v, c) => Liking(v, c) + (c == captor ? 20 : 0) - 10 * SeatsOf(c), out _);
                if (winner != null && ruler != null)
                    foreach (var lost in cands.Where(c => c != winner && c != Me)) Remember(lost, ruler, "the council passed me over", -4);
            }
            if (winner == null) continue;
            int owner = winner == Me ? Player.Id : winner.PartyId;
            seat.Owner = owner;
            foreach (var v in S.Settlements.Where(v => v.Province == seat.Province && v.Kind == SettlementKind.Village)) v.Owner = owner;
            if (winner != Me && PartyById(winner.PartyId) is Party lp) lp.HomeId = seat.Id;
            said.Add(winner == Me ? $"{seat.Name} is granted to you." : $"{seat.Name} is granted to {winner.Name}.");
            if (winner == Me || captor == Me) touched = true;
        }

        // 2. The marshal: when the baton is vacant or every few councils. A king is his own marshal.
        if (fs.Ruler != S.CaptainChar && (CharacterById(fs.Marshal) == null || fs.Councils % MarshalTerm == 0))
        {
            var cands = S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == f && c.Id != fs.Ruler && PartyById(c.PartyId) is { Captive: false })
                .OrderByDescending(c => PartyById(c.PartyId) is Party p ? Rules.Strength(p) : 0).Take(2).ToList();
            if (CharacterById(fs.Marshal) is Character cur && cur != Me && !cands.Contains(cur)) cands.Add(cur);
            bool standing = mine && Captain.Fealty && Captain.Renown >= MarshalRenown && FiefSeats >= LordFiefs;
            if (standing || fs.Marshal == S.CaptainChar && mine && Captain.Fealty) cands.Add(Me);
            int sitting = fs.Marshal;
            var m = Vote(f, cands, (v, c) => Liking(v, c) + (c.Id == sitting ? 10 : 0), out _);
            if (m != null && m.Id != fs.Marshal)
            {
                fs.Marshal = m.Id;
                fs.Objective = -1;
                said.Add(m == Me ? "You are elected marshal of the realm." : $"{m.Name} is elected marshal.");
                if (m == Me || sitting == S.CaptainChar) touched = true;
            }
        }

        // 3. The objective: the marshal names a seat to march on (you choose your own as marshal or king).
        if (ObjectiveOf(f) == null) fs.Objective = -1;
        if (fs.Objective < 0 && fs.Marshal != S.CaptainChar && PickObjective(f) is Settlement obj)
        {
            fs.Objective = obj.Id;
            said.Add($"The host is to march on {obj.Name}.");
        }

        // 4. A motion of war.
        if (fs.WarMotion >= 0)
        {
            int target = fs.WarMotion;
            var by = CharacterById(fs.WarMotionBy);
            fs.WarMotion = fs.WarMotionBy = -1;
            if (Alive(target) && !FactionsAtWar(f, target))
            {
                float ours = RealmStrength(f), theirs = MathF.Max(1, RealmStrength(target));
                int wars = S.Wars.Count(w => w.Active && (w.A == f || w.B == f));
                float yes = 0, no = 0;
                foreach (var (who, weight) in Voters(f))
                {
                    float s = who == Me ? 100 : (by != null ? OpinionOf(who, by) * 0.5f : 0) + (ours / theirs - 1) * 30 - wars * 15
                        + (who.Traits.Contains("Reckless") || who.Traits.Contains("Ambitious") ? 15 : 0) - (who.Traits.Contains("Cautious") ? 15 : 0);
                    if (s > 0) yes += weight; else no += weight;
                }
                if (yes > no) { DeclareWarBetween(f, target); said.Add($"The council votes for war on the {FactionNames[target]} ({yes:0} to {no:0})."); }
                else said.Add($"The council will not go to war with the {FactionNames[target]} ({yes:0} to {no:0}).");
                if (by == Me) touched = true;
            }
        }

        // 5. Lords who have had enough of their liege go over to a ruler they respect (F6).
        var gone = Defections(f);
        said.AddRange(gone);
        if (gone.Count > 0 && mine) touched = true;
        if (said.Count == 0) said.Add("Nothing of note is decided.");
        fs.LastCouncil = said;
        if (mine && Rank != Rank.King)
        {
            Log($"The council of the {FactionNames[f]} sits: {string.Join(" ", said)}");
            if (touched) Beat = ($"THE COUNCIL OF THE {FactionNames[f].ToUpperInvariant()}", said.Select(x => x.ToUpperInvariant()).ToArray());
        }
    }

    /// Fielded strength of a realm's lords.
    float RealmStrength(int f) => S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && p.Faction == f).Sum(Rules.Strength)
        + (Captain.Contract == f ? Rules.Strength(Player) : 0);

    /// The seat a realm's host marches on, while it is still worth marching on (held by an enemy at war with it).
    public Settlement? ObjectiveOf(int f)
    {
        if (f < 0 || f >= S.Factions.Count || S.Factions[f].Objective is var o && o < 0) return null;
        var s = SettlementById(S.Factions[f].Objective);
        return FactionsAtWar(f, s.Faction) ? s : null;
    }

    /// The marshal's pick: an enemy town or castle, towns and kin first, close to the realm's own seats.
    Settlement? PickObjective(int f)
    {
        var own = S.Settlements.Where(s => s.Faction == f && s.Kind != SettlementKind.Village).ToList();
        if (own.Count == 0) return null;
        return S.Settlements.Where(s => s.Kind != SettlementKind.Village && FactionsAtWar(f, s.Faction))
            .OrderByDescending(s => (s.Kind == SettlementKind.Town ? 30 : 0) + (s.Culture == Factions.CultureOf(f) ? 20 : 0)
                - own.Min(o => Vector2.Distance(o.Pos, s.Pos)) / (10 * Map.Spread))
            .FirstOrDefault();
    }

    /// Seats the marshal might name, best first (for the realm screen).
    public IEnumerable<Settlement> ObjectiveChoices(int f) =>
        S.Settlements.Where(s => s.Kind != SettlementKind.Village && FactionsAtWar(f, s.Faction))
            .OrderBy(s => S.Settlements.Where(o => o.Faction == f && o.Kind != SettlementKind.Village).Select(o => Vector2.Distance(o.Pos, s.Pos)).DefaultIfEmpty(0).Min());

    /// As marshal or king: name the seat the realm's hosts march on.
    public string? SetObjective(Settlement s)
    {
        if (Rank < Rank.Marshal) return "ONLY THE MARSHAL NAMES THE OBJECTIVE";
        if (s.Kind == SettlementKind.Village) return "NAME A TOWN OR CASTLE";
        if (!FactionsAtWar(Captain.Contract, s.Faction)) return "THE REALM IS NOT AT WAR WITH THEM";
        Realm!.Objective = s.Id;
        Log($"You order the {FactionNames[Captain.Contract]}'s hosts to march on {s.Name}.");
        return null;
    }

    /// As a lord: put a motion of war on a realm to the next council.
    public string? ProposeWar(int target)
    {
        var fs = Realm;
        if (fs == null || Rank is < Rank.Lord or Rank.King) return Rank == Rank.King ? "A KING DECLARES WAR HIMSELF" : $"ONLY A LORD MAY PROPOSE WAR ({RankName(Rank)} NOW)";
        if (target == Captain.Contract || target < 0 || target >= S.Factions.Count || !Alive(target)) return "NO SUCH REALM";
        if (FactionsAtWar(Captain.Contract, target)) return "ALREADY AT WAR";
        fs.WarMotion = target;
        fs.WarMotionBy = S.CaptainChar;
        Log($"You put a motion of war on the {FactionNames[target]} to the council.");
        return null;
    }

    // ---------- founding a realm (5.4) ----------

    public bool CanDeclareIndependence(out string why)
    {
        why = Rank == Rank.King ? "YOU ALREADY RULE"
            : Rank < Rank.Lord ? $"ONLY A LORD MAY RISE ({RankName(Rank)} NOW)"
            : !Fiefs.Any(t => t.Kind == SettlementKind.Town) ? "YOU NEED A TOWN FOR A CAPITAL"
            : "";
        return why == "";
    }

    /// Rebellion: rule your fiefs as a new realm, at war with your old liege. Lords who think well of you and ill of him follow,
    /// with their fiefs. Your capital is your largest town; its culture is your realm's.
    public string? DeclareIndependence(string? name = null)
    {
        if (!CanDeclareIndependence(out string why)) return why;
        int old = Captain.Contract;
        var oldRuler = RulerOf(old);
        var capital = Fiefs.Where(t => t.Kind == SettlementKind.Town).OrderBy(t => t.Id).First();
        int f = S.Factions.Count;
        int template = Enumerable.Range(0, Factions.All.Count).FirstOrDefault(i => Factions.All[i].Culture == capital.Culture);
        var realm = new FactionState
        {
            Id = f, Name = name is { Length: > 0 } ? name : $"Realm of {capital.Name}", Founded = true, Culture = capital.Culture, Palette = 2,
            Template = template, Capital = capital.Id, Ruler = S.CaptainChar, Marshal = S.CaptainChar, Treasury = 600,
            NextCouncil = (float)S.Hours + CouncilDays * 24,
        };
        S.Factions.Add(realm);
        Factions.Bind(S.Factions);
        // War with the old liege; a month's truce with everyone else while they take your measure.
        foreach (var g in Enumerable.Range(0, f))
            S.Wars.Add(new War { A = g, B = f, Active = g == old, TruceUntil = (float)S.Hours + B.TruceDays * 24 });
        // Your land goes with you.
        foreach (var t in Fiefs.ToList()) TakeOver(t, f);
        S.Factions[old].Pending.RemoveAll(g => g.Captor == S.CaptainChar);
        if (S.Factions[old].Marshal == S.CaptainChar) S.Factions[old].Marshal = -1;
        S.Contracts.Clear();
        Captain.Contract = f;
        Captain.ContractSince = (float)S.Hours;
        Captain.Fealty = false;
        Me.Faction = f;
        foreach (var k in Companions) k.Faction = f;
        if (oldRuler != null) Remember(oldRuler, Me, "you rebelled against me", -60);
        // Who follows you: lords who think well of you and little of their liege, unless loyal to a fault.
        var followers = new List<string>();
        foreach (var lord in S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c.Faction == old && c != oldRuler).ToList())
        {
            float you = OpinionOfYou(lord), him = oldRuler != null ? OpinionOf(lord, oldRuler) : 0;
            if (lord.Liege == S.CaptainChar || !lord.Traits.Contains("Loyal") && you >= 30 && you - him >= 20)
            {
                Defect(lord, f);
                followers.Add(lord.Name);
            }
            else Remember(lord, Me, "you rebelled against our liege", lord.Traits.Contains("Loyal") ? -25 : -10);
        }
        Log($"You declare the {realm.Name}, free of the {FactionNames[old]}. {(followers.Count > 0 ? string.Join(", ", followers) + " ride with you." : "No lord follows you yet.")}");
        AudioEngine.Current?.Play(Cue.Horn);
        CheckRank();
        return null;
    }

    /// A seat (with its province and villages) passes to another realm without a fight; its garrison stays.
    void TakeOver(Settlement s, int f)
    {
        s.Faction = f;
        S.Provinces[s.Province].Faction = f;
        foreach (var v in S.Settlements.Where(v => v.Province == s.Province && v.Kind == SettlementKind.Village)) v.Faction = f;
        s.Offers.Clear();
    }

    /// A lord changes realm, bringing his party and his fiefs.
    void Defect(Character lord, int f)
    {
        lord.Faction = f;
        if (PartyById(lord.PartyId) is not Party p) return;
        p.Faction = f;
        p.SiegeTarget = -1;
        p.ChaseId = -1;
        p.Task = "patrol";
        p.PatrolProvince = -1;
        foreach (var t in S.Settlements.Where(t => t.Owner == p.Id && t.Kind != SettlementKind.Village).ToList()) TakeOver(t, f);
        if (SettlementById(p.HomeId).Faction != f && S.Settlements.FirstOrDefault(t => t.Faction == f && t.Kind != SettlementKind.Village) is Settlement home)
            p.HomeId = home.Id;
        if (RulerOf(f) == Me) Remember(lord, Me, "you gave me a realm worth serving", 10);
    }

    // ---------- a king's diplomacy (7.2, first part) ----------

    void DeclareWarBetween(int a, int b)
    {
        var w = WarBetween(a, b);
        if (w == null) S.Wars.Add(w = new War { A = a, B = b });
        w.Active = true;
        w.ScoreA = w.ScoreB = 0;
        w.ExhA = w.ExhB = 0;
        w.Since = (float)S.Hours;
        Treaty($"The {FactionNames[a]} declare war on the {FactionNames[b]}.");
    }

    /// As king: go to war with a realm.
    public string? DeclareWar(int target)
    {
        if (Rank != Rank.King) return "ONLY A KING DECLARES WAR";
        if (target == Captain.Contract || target < 0 || target >= S.Factions.Count || !Alive(target)) return "NO SUCH REALM";
        if (FactionsAtWar(Captain.Contract, target)) return "ALREADY AT WAR";
        DeclareWarBetween(Captain.Contract, target);
        return null;
    }

    // ---------- beats, winning and losing (5.5) ----------

    static readonly Dictionary<Rank, string> BeatText = new()
    {
        [Rank.SwornSword] = "A MONTH IN ONE REALM'S PAY. ITS VILLAGES' RECRUITS NOW COST YOU HALF.",
        [Rank.Vassal] = "YOU HAVE KNELT AND RISEN A VASSAL. YOU MAY HOLD LAND, AND YOU HAVE A VOICE IN THE COUNCIL.",
        [Rank.Lord] = "TWO FIEFS AND A NAME THE REALM KNOWS. YOU MAY PROPOSE WARS, AND RISE AGAINST YOUR LIEGE IF YOU DARE.",
        [Rank.Marshal] = "THE REALM'S HOSTS ARE YOURS TO LEAD. NAME THEIR OBJECTIVE IN YOUR REALM SCREEN (R).",
        [Rank.King] = "YOU RULE IN YOUR OWN NAME. HOLD THE LAND, MAKE WAR AND PEACE, AND WIN THE REALM.",
    };

    /// A new highest rank is shown once, as a career beat.
    void CheckRank()
    {
        var r = Rank;
        if ((int)r <= Captain.BestRank) return;
        Captain.BestRank = (int)r;
        Log($"You are now {RankName(r).ToLowerInvariant()}.");
        if (BeatText.TryGetValue(r, out var text)) Beat = ($"{RankName(r)}", new[] { text });
    }

    /// The campaign is won when your realm holds most of the land while you rule or lead it, and lost when your realm falls.
    void CheckEnd()
    {
        int f = Captain.Contract;
        if (!S.Won && f >= 0 && Rank >= Rank.Marshal && RealmShare(f) >= WinShare)
        {
            S.Won = true;
            var how = Rank == Rank.King ? "AS KING" : "AS MARSHAL: THE KINGMAKER";
            Log($"The {FactionNames[f]} and their tributaries hold {Pct(RealmShare(f))} of the land. You have won the war.");
            Beat = ("THE WAR IS WON", new[]
            {
                $"THE {FactionNames[f].ToUpperInvariant()} AND THEIR TRIBUTARIES HOLD {Pct(RealmShare(f))} OF THE PROVINCES. YOU WON {how}.",
                $"DAY {Day}, {Captain.Renown:0} RENOWN, {FiefSeats} FIEFS.", "PLAY ON AS LONG AS YOU LIKE.",
            });
        }
        if (!S.GameOver && f >= 0 && Rank == Rank.King && !Alive(f))
        {
            S.GameOver = true;
            S.EndReason = $"THE {FactionNames[f].ToUpperInvariant()} HAS FALLEN: YOU HOLD NO TOWN OR CASTLE.";
            Log("Your realm has fallen.");
        }
    }

    /// Recruits of your realm cost a sworn sword (and anyone above) half.
    /// The weekly retainer a liege pays a sworn sword or better, on top of contract pay (F9): 20 crowns plus a tenth of renown.
    public float Retainer => Rank is >= Rank.SwornSword and < Rank.King ? 20 + Captain.Renown * 0.1f : 0;

    public float RecruitPriceFactor(Settlement s) => Rank >= Rank.SwornSword && s.Faction == Captain.Contract ? 0.5f : 1;
}
