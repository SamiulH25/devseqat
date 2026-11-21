using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// The playtest harness (docs/PLAYTEST_HARNESS.md): a campaign an agent plays by text commands, headless. One session is
/// one process (`--play <dir>`), so sessions run side by side with nothing shared. Every command is logged so a session can
/// be replayed exactly (`--play <dir2> --script <dir>/commands.log` with the same start), a crash is caught with its stack
/// and the state before it, and the world is checked for broken numbers after every command. Commands play through the
/// same public `Campaign` and `Battle` methods the screens use; `call`/`get`/`set` reach the rest by reflection.
public sealed partial class PlaySession
{
    public Campaign C = null!;
    public readonly string Dir;
    /// How the session began (seed, realm, scenario...), written to session.json so a run can be repeated.
    public readonly Dictionary<string, string> Start = new();
    /// The commands run so far (the replay of the session).
    readonly List<string> history = new();
    /// True once `set` changed the state by hand: findings say so.
    bool tampered;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public PlaySession(string dir) { Dir = Path.GetFullPath(dir); Directory.CreateDirectory(Dir); }

    /// Make the campaign: a tester scenario, a save file, or a new campaign (optionally played on by the stand-in captain).
    public void Begin(int seed, int realm, string? scenario, string? load, int days)
    {
        Start["seed"] = seed.ToString(Inv); Start["realm"] = realm.ToString(Inv);
        if (scenario != null)
        {
            C = TesterSaves.Build(scenario) ?? throw new ArgumentException($"no scenario matches '{scenario}': {string.Join(", ", TesterSaves.Ids.Select(i => i.Id))}");
            Start["scenario"] = scenario;
        }
        else if (load != null)
        {
            C = new Campaign(Saves.Load(load) ?? throw new ArgumentException($"cannot load {load}"));
            Start["load"] = load;
        }
        else
        {
            C = Campaign.New(seed, realm);
            if (days > 0) { new Pilot(C).Run(days * 24); Start["days"] = days.ToString(Inv); C.Encounter = null; C.Arrived = null; C.Report = null; C.Beat = null; }
            else C.OfferRoad(); // a new game asks first how the captain fights, as the title screen's NEW CAMPAIGN does
        }
        File.WriteAllText(Path.Combine(Dir, "session.json"), System.Text.Json.JsonSerializer.Serialize(Start));
    }

    // ---------- the command loop ----------

    /// Run one command line and return what the agent sees. Never throws: a crash is reported, filed and rolled back.
    public string Exec(string line)
    {
        line = line.Trim();
        if (line == "" || line.StartsWith('#')) return "";
        var args = Split(line);
        string verb = args[0].ToLowerInvariant();
        bool readOnly = ReadOnly.Contains(verb);
        string? before = null;
        if (!readOnly)
        {
            history.Add(line);
            File.AppendAllText(Path.Combine(Dir, "commands.log"), line + "\n");
            if (B == null) before = C.S.Serialize(); // rolled back to on a crash (a battle cannot be: it is dropped)
        }
        string reply;
        try { reply = Dispatch(verb, args.Skip(1).ToArray()); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (e is System.Reflection.TargetInvocationException { InnerException: { } inner }) e = inner;
            if (e is UsageException) return "ERROR: " + e.Message;
            reply = Crash(line, e, before);
        }
        if (!readOnly) reply += Audit();
        reply = reply.TrimEnd();
        File.AppendAllText(Path.Combine(Dir, "transcript.log"), $"> {line}\n{reply}\n\n");
        if (!readOnly) File.AppendAllText(Path.Combine(Dir, "replies.log"), $"> {line}\n{reply.Replace(Dir, "<session>")}\n");
        return reply;
    }

    sealed class UsageException(string m) : Exception(m);
    static UsageException Usage(string m) => new(m);

    /// Commands that only look: not logged, not audited, no snapshot.
    static readonly HashSet<string> ReadOnly = new() { "help", "look", "party", "near", "places", "place", "realms", "log", "event", "get", "methods", "bstate", "troop", "contracts", "findings", "scenarios" };

    string Dispatch(string verb, string[] a)
    {
        if (B != null && BattleVerbs.Contains(verb)) return BattleCommand(verb, a);
        if (B != null && !ReadOnly.Contains(verb) && verb is not ("note" or "bug" or "call" or "set" or "save"))
            return "ERROR: a battle is on. Battle commands: " + string.Join(" ", BattleVerbs) + " (see `help battle`)";
        return verb switch
        {
            "help" => Help(a.FirstOrDefault()),
            "look" => Look(),
            "party" => PartyView(),
            "near" => Near(a.Length > 0 ? F(a[0]) : 40),
            "places" => Places(a.FirstOrDefault()),
            "place" => PlaceView(SettlementArg(a, 0)),
            "realms" => Realms(),
            "log" => LogTail(a.FirstOrDefault(x => x != "all") is string n ? I(n) : 15, a.Contains("all")),
            "event" => EventView(),
            "contracts" => Contracts(),
            "troop" => TroopView(Need(a, 0, "troop id")),
            "scenarios" => string.Join("\n", TesterSaves.Ids.Select(i => $"{i.Id}: {i.What}")),
            "choose" => Choose(I(Need(a, 0, "choice number"))),
            "goto" => Go(SettlementArg(a, 0)),
            "move" => MoveTo(new Vector2(F(Need(a, 0, "x")), F(Need(a, 1, "y")))),
            "chase" => ChaseParty(PartyArg(a, 0)),
            "stop" => Halt(),
            "wait" => Wait(a.Length > 0 ? F(a[0]) : 24),
            "rest" => Do(() => { C.Rest(a.Length > 0 ? F(a[0]) : 24); return null; }, "rested") + Pending(),
            "fight" => Fight(),
            "battle" => StartBattle(),
            "retreat" => Retreat(),
            "parley" => Parley(),
            "leave" => Leave(),
            "recruit" => Do(() => C.RecruitLevies(Here(), I(Need(a, 0, "how many"))), "recruited"),
            "hire" => Hire(I(Need(a, 0, "mercenary number"))),
            "buyfood" => Do(() => C.BuyFood(I(Need(a, 0, "days"))), "bought food"),
            "promote" => Promote(a),
            "accept" => Accept(I(Need(a, 0, "offer number"))),
            "contract" => Do(() => C.SignContract(I(Need(a, 0, "realm id"))), "signed"),
            "swear" => Do(() => C.SwearFealty(), "sworn"),
            "sell" => Sell(Need(a, 0, "prisoners|goods")),
            "besiege" => Besiege(SettlementArg(a, 0)),
            "assault" => Assault(a.FirstOrDefault()),
            "talk" => Talk(a),
            "call" => Call(a),
            "get" => Get(a.Length > 0 ? string.Join(" ", a) : ""),
            "set" => Set(a),
            "methods" => Methods(a.FirstOrDefault()),
            "save" => SaveGame(a.Length > 0 ? string.Join(" ", a) : $"day {C.Day}"),
            "note" or "bug" => File_(verb, a),
            "findings" => File.Exists(FindingsPath) ? File.ReadAllText(FindingsPath) : "no findings yet",
            _ => $"ERROR: unknown command '{verb}'. Try `help`.",
        };
    }

    // ---------- reading the world ----------

    Party Me => C.Player;
    string Clock => $"day {C.Day} {(int)(C.S.Hours % 24):00}:{(int)(C.S.Hours * 60 % 60):00}";
    static string P(Vector2 v) => $"({v.X.ToString("0.0", Inv)},{v.Y.ToString("0.0", Inv)})";
    float Dist(Vector2 v) => Vector2.Distance(v, Me.Pos);
    string Realm(int f) => f >= 0 && f < C.FactionNames.Length ? C.FactionNames[f] : f == -3 ? "monsters" : "none";

    string Look()
    {
        var sb = new StringBuilder();
        var cap = C.Captain;
        sb.AppendLine($"{Clock} | {C.Stage} | gold {C.S.Gold:0} | food {Me.Food:0.0} days | renown {cap.Renown:0} | serving {(cap.Contract >= 0 ? Realm(cap.Contract) : "nobody")}{(cap.Fealty ? " (sworn)" : "")}");
        sb.AppendLine($"COMPANY {Me.Men} men ({Me.Wounded} wounded) in {Me.Companies.Count} companies, strength {Rules.Strength(Me):0}, limit {Rules.MenLimit(cap)} men"
            + (cap.WoundedUntil > C.S.Hours ? $" | CAPTAIN WOUNDED for {cap.WoundedUntil - C.S.Hours:0}h" : "")
            + (cap.AuraTier > 0 ? $" | aura tier {cap.AuraTier}" : ""));
        string where = C.SettlementAt(Me.Pos) is Settlement here ? $"at #{here.Id} {here.Name} ({Kind(here)}, {Realm(here.Faction)})" : $"in the field";
        string going = !Me.Moving ? (Me.Resting ? "resting" : "standing")
            : Me.ChaseId >= 0 && C.PartyById(Me.ChaseId) is Party q ? $"chasing #{q.Id} {q.Name}, {Dist(q.Pos):0} away"
            : Me.GoalSettlement >= 0 ? $"riding to #{Me.GoalSettlement} {C.SettlementById(Me.GoalSettlement).Name}, {Dist(C.SettlementById(Me.GoalSettlement).Pos):0} away"
            : $"marching to {P(Me.Goal)}, {Dist(Me.Goal):0} away";
        sb.AppendLine($"POSITION {P(Me.Pos)} {where}, {going}{(C.Besieging is Settlement b ? $" | BESIEGING #{b.Id} {b.Name}, ladders in {C.LaddersIn(b):0}h" : "")}");
        if (C.S.Contracts.Count > 0) sb.AppendLine($"CONTRACTS {C.S.Contracts.Count} (see `contracts`)");
        sb.Append(Near(30, 5));
        sb.Append(Pending());
        return sb.ToString();
    }

    /// What needs an answer now, with the commands that answer it. Reading only: the notices (arrived, career, report,
    /// tournament) are cleared when time next passes (`Wait`), so `look` changes nothing and a replay runs the same.
    string Pending()
    {
        var sb = new StringBuilder();
        if (C.S.GameOver) sb.AppendLine($"\nGAME OVER: {(C.S.Won ? "won" : "lost")}. Only reading commands are of use now.");
        if (C.Encounter is Party e)
        {
            if (C.Meeting(e)) sb.AppendLine($"\nMEETING #{e.Id} {e.Name} ({e.Kind}, {Realm(e.Faction)}): talk {e.Id} <topic> | leave");
            else
            {
                var side = C.S.Parties.Where(p => p != e && !p.Down && C.Hostile(Me, p) && Dist(p.Pos) < 8).Sum(Rules.Strength);
                string retreat = C.CanRetreat(out var why) ? "retreat" : $"(no retreat: {why})";
                int price = C.ParleyPrice(e);
                sb.AppendLine($"\nENCOUNTER #{e.Id} {e.Name} ({e.Kind}{(e.Breed != "" ? " " + e.Breed : "")}, {Realm(e.Faction)}): {e.Men} men, strength {Rules.Strength(e) * C.Edge(e):0} vs your {Rules.Strength(Me):0}"
                    + (side > 0 ? $" (+{side:0} near them)" : ""));
                sb.AppendLine($"  answer: fight (auto-resolve) | battle (fight it yourself) | {retreat}{(price > 0 ? $" | parley (pay {price})" : "")}");
            }
        }
        if (C.S.Event is PendingEvent pe && Campaign.EventOf(pe.Id) is EventDef ev)
            sb.AppendLine($"\nEVENT \"{C.Fill(ev.Title, pe)}\": see `event`, answer with `choose <n>`");
        if (C.Arrived is Settlement s) sb.AppendLine($"\nARRIVED at #{s.Id} {s.Name}: see `place {s.Id}`");
        if (C.Beat is var (title, lines)) sb.AppendLine($"\nCAREER: {title}: {string.Join(" ", lines)}");
        if (C.Report is BattleReport r) sb.AppendLine("\n" + ReportText(r));
        if (C.Lists is Settlement l) sb.AppendLine($"\nTOURNAMENT at #{l.Id} {l.Name}: `call AutoTournament {l.Id}` to enter, or carry on to pass");
        return sb.ToString();
    }

    static string Kind(Settlement s) => s.KindWord;

    string PartyView()
    {
        var sb = new StringBuilder($"{Me.Men} men, {Me.Wounded} wounded, {C.PrisonerCount} prisoners, {Me.Horses} spare horses, slots {Me.Companies.Count}/{Rules.CompanySlots(C.Captain)}\n");
        foreach (var co in Me.Companies)
        {
            sb.AppendLine($"company #{co.Id} {co.Name} [{co.Role}] {co.Men} men ({co.Wounded} wounded), cohesion {co.Cohesion:0}, battles {co.Battles}{(co.Hero != "" ? ", hero " + co.Hero : "")}{(co.Division >= 0 ? $", division {co.Division}" : "")}");
            for (int i = 0; i < co.Stacks.Count; i++)
            {
                var st = co.Stacks[i];
                sb.AppendLine($"  stack {co.Id}.{i}: {st.Troop} (tier {st.Type.Tier}) x{st.Men}{(st.Wounded > 0 ? $" +{st.Wounded} wounded" : "")}, xp {st.Xp:0}/{st.PoolCap:0}, {st.Ready} ready");
            }
        }
        var promos = C.PromotionChoices().ToList();
        if (promos.Count > 0)
        {
            sb.AppendLine("PROMOTIONS (promote <n> [men]):");
            for (int i = 0; i < promos.Count; i++)
            {
                var (co, st, to) = promos[i];
                string where = C.CanTrainHere(Me, to) ? "here" : C.TrainPlaceNeed;
                sb.AppendLine($"  {i}: {co.Name} {st.Troop} -> {to} (tier {Troops.Get(to).Tier}), {st.Ready} ready, {where}");
            }
        }
        return sb.ToString();
    }

    string Near(float radius, int max = 12)
    {
        var sb = new StringBuilder();
        var places = C.S.Settlements.Where(s => Dist(s.Pos) <= radius).OrderBy(s => Dist(s.Pos)).Take(max).ToList();
        if (places.Count > 0) sb.AppendLine("PLACES " + string.Join(" | ", places.Select(s => $"#{s.Id} {s.Name} {Kind(s)} ({Realm(s.Faction)}{(C.Refuses(s) ? ", ENEMY" : "")}) {Dist(s.Pos):0}")));
        var parties = C.S.Parties.Where(p => p != Me && !p.Down && !p.Captive && Dist(p.Pos) <= radius && C.InSight(p.Pos)).OrderBy(p => Dist(p.Pos)).Take(max).ToList();
        if (parties.Count > 0) sb.AppendLine("PARTIES " + string.Join(" | ", parties.Select(p => $"#{p.Id} {p.Name} {p.Kind}{(p.Breed != "" ? "/" + p.Breed : "")} ({Realm(p.Faction)}) {p.Men} men str {Rules.Strength(p) * C.Edge(p):0}{(C.Hostile(Me, p) ? " HOSTILE" : "")} {Dist(p.Pos):0}")));
        var sites = C.Sites.Where(x => Dist(x.Pos) <= radius).OrderBy(x => Dist(x.Pos)).Take(max).ToList();
        if (sites.Count > 0) sb.AppendLine("SITES " + string.Join(" | ", sites.Select(x => $"{x.Name} ({x.Kind}) at {P(x.Pos)} {Dist(x.Pos):0}")));
        return sb.Length == 0 ? "nothing within " + radius + "\n" : sb.ToString();
    }

    string Places(string? filter)
    {
        var list = C.S.Settlements.Where(s => filter == null || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || Kind(s).Contains(filter, StringComparison.OrdinalIgnoreCase) || Realm(s.Faction).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => Dist(s.Pos)).Take(40);
        return string.Join("\n", list.Select(s => $"#{s.Id} {s.Name} {Kind(s)} ({Realm(s.Faction)}{(C.Refuses(s) ? ", ENEMY" : "")}{(s.Besieged ? ", BESIEGED" : "")}) at {P(s.Pos)}, {Dist(s.Pos):0} away"));
    }

    string PlaceView(Settlement s)
    {
        var sb = new StringBuilder($"#{s.Id} {s.Name}: {Kind(s)} of {Realm(s.Faction)}, culture {s.Culture}, at {P(s.Pos)} ({Dist(s.Pos):0} away){(C.Refuses(s) ? ", ENEMY: will not trade with you" : "")}\n");
        sb.AppendLine($"prosperity {s.Prosperity:0}, unrest {s.Unrest:0}, garrison {s.Garrison.Sum(g => g.Men)} men{(s.Besieged ? ", BESIEGED" : "")}{(s.Owner >= 0 && C.PartyById(s.Owner) is Party o ? ", held by " + o.Name : "")}");
        sb.AppendLine($"recruits {s.Recruits} x {C.RecruitTroopFor(s)} at {C.LevyPrice(s)} each (scaled per man) | horses {s.Horses}");
        for (int i = 0; i < s.Mercenaries.Count; i++)
            sb.AppendLine($"  mercenary {i}: {s.Mercenaries[i].Name}, {s.Mercenaries[i].Men} x {s.Mercenaries[i].Troop}, {C.MercenaryPrice(s.Mercenaries[i])} crowns");
        var offers = SafeOffers(s);
        for (int i = 0; i < offers.Count; i++)
            sb.AppendLine($"  offer {i}: [{offers[i].Kind}] {offers[i].Text} reward {offers[i].Reward:0}");
        bool here = C.SettlementAt(Me.Pos) == s;
        sb.AppendLine(here ? "You are here: recruit <n>, hire <n>, accept <n>, buyfood <n>, sell prisoners|goods, promote <n>, rest <h>; `methods` lists more"
            : $"You are not here: goto {s.Id}");
        return sb.ToString();
    }

    List<Contract> SafeOffers(Settlement s) => s.Kind == SettlementKind.Village ? C.ElderOffers(s) : s.Kind == SettlementKind.Town ? C.Offers(s) : new();

    string Realms()
    {
        var sb = new StringBuilder();
        for (int f = 0; f < C.S.Factions.Count; f++)
        {
            var wars = Enumerable.Range(0, C.S.Factions.Count).Where(g => C.FactionsAtWar(f, g)).Select(Realm);
            int seats = C.S.Settlements.Count(s => s.Faction == f && s.Kind != SettlementKind.Village);
            int lords = C.S.Parties.Count(p => p.Kind == PartyKind.Lord && p.Faction == f && !p.Down);
            sb.AppendLine($"{f}: {Realm(f)}, {seats} seats, {lords} lords in the field, at war with: {string.Join(", ", wars.DefaultIfEmpty("nobody"))}");
        }
        return sb.ToString();
    }

    string LogTail(int n, bool all = true) => string.Join("\n", C.S.Log.Where(l => all || l.Level == LogLevel.Yours).TakeLast(n).Select(l => $"[day {(int)(l.At / 24) + 1} {(int)(l.At % 24):00}h] {l.Text}"));

    string EventView()
    {
        if (C.S.Event is not PendingEvent pe || Campaign.EventOf(pe.Id) is not EventDef ev) return "no event waiting";
        var sb = new StringBuilder($"EVENT {ev.Id}: {C.Fill(ev.Title, pe)}\n{C.Fill(ev.Text, pe)}\n");
        for (int i = 0; i < ev.Choices.Count; i++)
        {
            string blocked = C.ChoiceBlocked(ev.Choices[i]);
            sb.AppendLine($"  {i}: {C.Fill(ev.Choices[i].Label, pe)}{(blocked != "" ? $"  [BLOCKED: {blocked}]" : "")}");
        }
        return sb.ToString();
    }

    string Contracts() => C.S.Contracts.Count == 0 ? "no contracts" : string.Join("\n", C.S.Contracts.Select(k =>
        $"[{k.Kind}] {k.Text} | reward {k.Reward:0}, due in {(k.Deadline - C.S.Hours) / 24:0.0} days, progress {k.Progress:0.##}"
        + (C.PartyById(k.Target) is Party t && k.Kind is "clear" or "hunt" or "escort" ? $" | target #{t.Id} at {P(t.Pos)}, {Dist(t.Pos):0} away" : "")));

    static string TroopView(string id) => Dump(Troops.Get(id), 1);

    // ---------- acting ----------

    /// Run an action that returns null or the reason it failed (the map screen's convention), saying what it cost.
    string Do(Func<string?> act, string ok)
    {
        float gold = C.S.Gold, food = Me.Food;
        int men = Me.Men;
        if (act() is string why) return "REFUSED: " + why;
        var changes = new List<string>();
        if (MathF.Abs(C.S.Gold - gold) >= 0.5f) changes.Add($"gold {gold:0} -> {C.S.Gold:0}");
        if (MathF.Abs(Me.Food - food) >= 0.05f) changes.Add($"food {food:0.0} -> {Me.Food:0.0}");
        if (Me.Men != men) changes.Add($"men {men} -> {Me.Men}");
        return changes.Count == 0 ? ok : $"{ok} ({string.Join(", ", changes)})";
    }

    Settlement Here() => C.SettlementAt(Me.Pos) ?? throw Usage("you are not at a settlement");

    string Choose(int i)
    {
        if (C.S.Event == null) return "ERROR: no event waiting";
        var at = Mark;
        return Do(() => C.Choose(i), "chosen") + "\n" + LogSince(at, all: true) + Pending();
    }

    string Go(Settlement s) { if (C.Encounter != null) return "ERROR: answer the encounter first"; C.GoTo(s); return $"riding to #{s.Id} {s.Name}, {Dist(s.Pos):0} away. `wait` to travel."; }
    string MoveTo(Vector2 p) { C.MoveTo(p); return $"marching to {P(Me.Goal)}, {Dist(Me.Goal):0} away. `wait` to travel."; }
    string ChaseParty(Party p) { C.Chase(p); return $"chasing #{p.Id} {p.Name}, {Dist(p.Pos):0} away. `wait` to travel."; }
    string Halt() { Me.Moving = false; Me.Goal = Me.Pos; Me.ChaseId = -1; Me.GoalSettlement = -1; return "halted"; }

    /// Let time pass until `hours` have gone or something wants an answer (an encounter, an event, arriving, the end).
    string Wait(float hours)
    {
        if (C.Encounter != null) return "ERROR: answer the encounter first (fight, battle, retreat, parley or leave)";
        if (C.S.Event != null) return "ERROR: answer the event first (`event`, then `choose <n>`)";
        if (hours <= 0 || hours > 24 * 30) throw Usage("wait between 0 and 720 hours");
        C.Arrived = null; C.Beat = null; C.Report = null; C.Lists = null; // seen; carrying on passes the lists by
        double start = C.S.Hours, until = start + hours;
        var mark = Mark;
        while (C.S.Hours < until - 1e-6 && !C.S.GameOver)
        {
            C.Tick(MathF.Min(1, (float)(until - C.S.Hours)));
            if (C.Encounter != null || C.S.Event != null || C.Arrived != null || C.Beat != null || C.Report != null || C.Lists != null) break;
        }
        return $"{C.S.Hours - start:0.0}h passed, now {Clock}, at {P(Me.Pos)}{(Me.Moving ? ", still moving" : "")}\n" + LogSince(mark) + Pending();
    }

    /// Your own log lines after `mark` (the last line before the command); the world's news only counted (read it with `log all`).
    string LogSince(LogEntry? mark, bool all = false)
    {
        int from = mark == null ? 0 : C.S.Log.IndexOf(mark) + 1; // the log keeps 60 lines: a mark pushed out means all are new
        var since = C.S.Log.Skip(from).ToList();
        var yours = since.Where(l => all || l.Level == LogLevel.Yours).Select(l => "  " + l.Text).TakeLast(12).ToList();
        int world = since.Count - yours.Count;
        return string.Concat(yours.Select(l => l + "\n")) + (world > 0 ? $"  (+{world} lines of world news: `log all`)\n" : "");
    }

    LogEntry? Mark => C.S.Log.LastOrDefault();

    string Fight()
    {
        if (C.Encounter == null) return "ERROR: no encounter";
        if (C.Meeting(C.Encounter)) return "ERROR: a friendly meeting: `talk` or `leave`";
        var r = C.AutoResolveEncounter();
        C.Report = null;
        if (r.Spoil != "") C.TakeSpoil();
        return ReportText(r, Me) + Pending();
    }

    string Retreat()
    {
        if (C.Encounter == null) return "ERROR: no encounter";
        if (!C.CanRetreat(out var why)) return "REFUSED: " + why;
        var at = Mark;
        C.Retreat();
        return "you fall back\n" + LogSince(at) + Pending();
    }

    string Parley()
    {
        if (C.Encounter == null) return "ERROR: no encounter";
        var at = Mark;
        return (C.Parley() ? "they take the money and let you go" : "REFUSED: they will not parley") + "\n" + LogSince(at) + Pending();
    }

    string Leave()
    {
        if (C.Encounter is not Party e || !C.Meeting(e)) return "ERROR: no friendly meeting to leave";
        C.Part();
        return "you part ways" + Pending();
    }

    string Hire(int i)
    {
        var s = Here();
        if (i < 0 || i >= s.Mercenaries.Count) throw Usage($"mercenary number 0..{s.Mercenaries.Count - 1}");
        return Do(() => C.Hire(s, s.Mercenaries[i]), "hired");
    }

    string Accept(int i)
    {
        var s = Here();
        var offers = SafeOffers(s);
        if (i < 0 || i >= offers.Count) throw Usage($"offer number 0..{offers.Count - 1}");
        return Do(() => C.Accept(s, offers[i]), "accepted: " + offers[i].Text);
    }

    string Promote(string[] a)
    {
        var promos = C.PromotionChoices().ToList();
        int i = I(Need(a, 0, "promotion number (see `party`)"));
        if (i < 0 || i >= promos.Count) throw Usage($"promotion number 0..{promos.Count - 1}");
        var (co, st, to) = promos[i];
        int n = a.Length > 1 ? I(a[1]) : st.Ready;
        int had = co.Stacks.Where(x => x.Troop == to).Sum(x => x.Men);
        string r = Do(() => C.Promote(co, st, to, n), "promoted");
        int now = co.Stacks.Where(x => x.Troop == to).Sum(x => x.Men);
        return r.StartsWith("REFUSED") ? r : r.Replace("promoted", $"promoted {now - had} {st.Troop} to {to} (asked {n})");
    }

    string Sell(string what) => what switch
    {
        "prisoners" => Do(() => C.SellPrisoners(Here()), "sold the prisoners"),
        "goods" => Do(() => C.SellGoods(Here()), "sold the goods"),
        _ => throw Usage("sell prisoners|goods"),
    };

    string Besiege(Settlement s)
    {
        if (!C.CanBesiege(s, out var why)) return "REFUSED: " + why;
        C.Besiege(s);
        return $"the siege of {s.Name} begins; ladders in {C.LaddersIn(s):0}h. `wait`, then `assault` (or `assault fight`)";
    }

    string Assault(string? how)
    {
        if (C.Besieging is not Settlement s) return "ERROR: you are not besieging anything";
        if (how == "fight") return BeginBattle(C.PrepareAssault(s));
        var r = C.AutoResolveAssault(s);
        C.Report = null;
        return ReportText(r, Me) + Pending();
    }

    string Talk(string[] a)
    {
        var p = PartyArg(a, 0);
        var ch = C.CharacterOf(p) ?? throw Usage("nobody there to talk to");
        if (C.CannotTalk(ch) is string no) return "REFUSED: " + no;
        if (a.Length < 2) return "topics: " + string.Join(", ", C.TopicsFor(ch).Select(t => t.Id + (C.CannotAsk(ch, t.Id) is string w ? $" [no: {w}]" : "")));
        if (C.CannotAsk(ch, a[1]) is string why) return "REFUSED: " + why;
        var (ok, line) = C.Say(ch, a[1], a.Skip(2).ToArray());
        return $"{(ok ? "YES" : "NO")}: {line}";
    }

    string SaveGame(string label)
    {
        string path = Saves.Save(C.S, SaveKind.Named, label);
        return $"saved: {path} (open it in the game with IRONFIELD_SAVES={Saves.Root})";
    }

    /// A battle's report: your companies line by line (those of `me`, when given), allies and the enemy in a line each.
    static string ReportText(BattleReport r, Party? me = null)
    {
        var sb = new StringBuilder($"BATTLE {(r.Won ? "WON" : "LOST")} against {r.Enemy}{(r.Field != "" ? $" ({r.Field})" : "")}: loot {r.Loot:0}, ransom {r.Ransom:0}, bounty {r.Bounty:0}, renown {r.Renown:+0;-0}, captain {r.Captain}\n");
        var ours = r.Companies.Where(c => c.Team == 0 && (me == null || me.Companies.Any(k => k.Id == c.Id))).ToList();
        var allied = r.Companies.Where(c => c.Team == 0 && !ours.Contains(c)).ToList();
        foreach (var co in ours)
            sb.AppendLine($"  {co.Name} ({co.Troop}): {co.MenBefore} -> {co.MenAfter} (killed {co.Killed}, wounded {co.Wounded}, captured {co.Captured}){(co.UpgradeReady ? ", promotion ready" : "")}");
        if (allied.Count > 0) sb.AppendLine($"  allies: {allied.Count} companies, lost {allied.Sum(c => c.MenBefore - c.MenAfter)} of {allied.Sum(c => c.MenBefore)}");
        int theirs = r.Companies.Where(c => c.Team == 1).Sum(c => c.MenBefore - c.MenAfter);
        sb.AppendLine($"  the enemy lost {theirs} of {r.Companies.Where(c => c.Team == 1).Sum(c => c.MenBefore)}");
        if (r.Spoil != "") sb.AppendLine($"  spoil: {r.Spoil}");
        if (r.LastingWound != "") sb.AppendLine($"  lasting wound: {r.LastingWound}");
        foreach (var line in r.Rises.Concat(r.Goals.Select(g => $"goal {(g.Met ? "met" : "missed")}: {g.Text}")).Concat(r.Roll.Select(x => $"{x.Kind}: {x.Text}"))) sb.AppendLine("  " + line);
        return sb.ToString();
    }

    // ---------- findings ----------

    string FindingsPath => Path.Combine(Dir, "findings.jsonl");

    /// `bug <severity> <text>` or `note <text>`: filed with the day, the command count and a save to open in the game.
    string File_(string kind, string[] a)
    {
        string severity = kind == "bug" && a.Length > 0 && a[0] is "minor" or "major" or "blocker" ? a[0] : kind == "bug" ? "major" : "note";
        var text = string.Join(" ", kind == "bug" && severity == a.FirstOrDefault() ? a.Skip(1) : a);
        if (text == "") throw Usage($"{kind} {(kind == "bug" ? "[minor|major|blocker] " : "")}<what you saw>");
        string? save = null;
        if (kind == "bug" && B == null) { try { save = Saves.Save(C.S, SaveKind.Named, $"bug day {C.Day}"); } catch (Exception e) { save = "save failed: " + e.Message; } }
        Record(kind, severity, text, save: save);
        return $"filed {kind} ({severity})";
    }

    void Record(string kind, string severity, string text, string? save = null, string? detail = null)
    {
        var row = new Dictionary<string, object?>
        {
            ["kind"] = kind, ["severity"] = severity, ["text"] = text, ["day"] = C.Day, ["hours"] = Math.Round(C.S.Hours, 2),
            ["command"] = history.Count, ["battle"] = B != null, ["tampered"] = tampered, ["save"] = save, ["detail"] = detail,
            ["session"] = Path.GetFileName(Dir), ["start"] = Start,
        };
        File.AppendAllText(FindingsPath, System.Text.Json.JsonSerializer.Serialize(row) + "\n");
    }

    // ---------- argument parsing ----------

    static string[] Split(string line)
    {
        var list = new List<string>();
        var cur = new StringBuilder();
        bool quoted = false, any = false;
        foreach (char ch in line)
        {
            if (ch == '"') { quoted = !quoted; any = true; continue; }
            if (char.IsWhiteSpace(ch) && !quoted) { if (cur.Length > 0 || any) list.Add(cur.ToString()); cur.Clear(); any = false; continue; }
            cur.Append(ch);
        }
        if (cur.Length > 0 || any) list.Add(cur.ToString());
        return list.ToArray();
    }

    static string Need(string[] a, int i, string what) => i < a.Length ? a[i] : throw Usage("missing " + what);
    static int I(string s) => int.TryParse(s.TrimStart('#'), NumberStyles.Integer, Inv, out int v) ? v : throw Usage($"'{s}' is not a whole number");
    static float F(string s) => float.TryParse(s, NumberStyles.Float, Inv, out float v) ? v : throw Usage($"'{s}' is not a number");

    /// A settlement by id (`12`, `#12`), by name (prefix, any case) or `here`.
    Settlement SettlementArg(string[] a, int i)
    {
        string s = Need(a, i, "settlement id or name");
        if (s == "here") return Here();
        if (int.TryParse(s.TrimStart('#'), out int id)) return id >= 0 && id < C.S.Settlements.Count ? C.S.Settlements[id] : throw Usage($"no settlement #{id}");
        string name = string.Join(" ", a.Skip(i));
        return C.S.Settlements.Where(x => x.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)).OrderBy(x => Dist(x.Pos)).FirstOrDefault()
            ?? throw Usage($"no settlement named '{name}'");
    }

    Party PartyArg(string[] a, int i) => C.PartyById(I(Need(a, i, "party id"))) ?? throw Usage($"no party #{a[i]}");
}
