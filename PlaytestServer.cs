using System.Collections;
using System.Globalization;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// The playtest harness's reach and plumbing (docs/PLAYTEST_HARNESS.md): `call`/`get`/`set` by reflection, crash capture,
/// the audit after each command, and the session's server (a Unix socket, one command a connection) or script runner.
public sealed partial class PlaySession
{
    // ---------- reflection: call, get, set, methods ----------

    static readonly BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance;

    /// `call <Method> [args]`: any public method on `Campaign`. Args: numbers, words, `null`, `x,y` for a point; a settlement,
    /// party, company or character is given by id; a troop stack as `<company id>.<index>`; a string list as `a,b,c`.
    string Call(string[] a)
    {
        string name = Need(a, 0, "method name (see `methods`)");
        var args = a.Skip(1).ToArray();
        var cands = typeof(Campaign).GetMethods(Pub).Where(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !m.IsSpecialName).ToList();
        if (cands.Count == 0) return $"ERROR: no public Campaign method '{name}'. Try `methods {name[..Math.Min(4, name.Length)]}`";
        string? last = null;
        foreach (var m in cands.OrderBy(m => m.GetParameters().Length))
        {
            var ps = m.GetParameters();
            if (args.Length > ps.Length || args.Length < ps.Count(p => !p.IsOptional && !p.IsOut)) continue;
            var vals = new object?[ps.Length];
            try
            {
                for (int i = 0; i < ps.Length; i++)
                    vals[i] = ps[i].IsOut ? null : i < args.Length ? Convert(args[i], ps[i].ParameterType) : ps[i].DefaultValue;
            }
            catch (UsageException e) { last = e.Message; continue; }
            var ret = m.Invoke(C, vals);
            string outs = string.Concat(ps.Where(p => p.IsOut).Select(p => $"\n{p.Name} = {Dump(vals[p.Position], 1)}"));
            string text = m.ReturnType == typeof(void) ? "ok" : ret == null && m.ReturnType == typeof(string) ? "ok (null: no objection)" : Dump(ret, 2);
            return text + outs + Pending();
        }
        return "ERROR: no overload fits: " + (last ?? "") + "\n" + string.Join("\n", cands.Select(Sig));
    }

    string Methods(string? filter) => string.Join("\n", typeof(Campaign).GetMethods(Pub)
        .Where(m => !m.IsSpecialName && m.DeclaringType == typeof(Campaign) && (filter == null || m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(m => m.Name).Select(Sig).Distinct());

    static string Sig(MethodInfo m) => $"{Short(m.ReturnType)} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{(p.IsOut ? "out " : "")}{Short(p.ParameterType)} {p.Name}{(p.IsOptional ? "=" + (p.DefaultValue ?? "null") : "")}"))})";

    static string Short(Type t) => Nullable.GetUnderlyingType(t) is Type u ? Short(u) + "?" : t.IsGenericType
        ? $"{t.Name[..t.Name.IndexOf('`')]}<{string.Join(",", t.GetGenericArguments().Select(Short))}>" : t.Name;

    object? Convert(string s, Type t)
    {
        if (s == "null" && (!t.IsValueType || Nullable.GetUnderlyingType(t) != null)) return null;
        t = Nullable.GetUnderlyingType(t) ?? t;
        if (t.IsByRef) t = t.GetElementType()!;
        if (t == typeof(string)) return s;
        if (t == typeof(int)) return I(s);
        if (t == typeof(float)) return F(s);
        if (t == typeof(double)) return (double)F(s);
        if (t == typeof(bool)) return s is "true" or "1" or "yes" ? true : s is "false" or "0" or "no" ? false : throw Usage($"'{s}' is not true/false");
        if (t.IsEnum) return Enum.TryParse(t, s, true, out var e) ? e : throw Usage($"'{s}' is not one of {string.Join("|", Enum.GetNames(t))}");
        if (t == typeof(Vector2)) { var xy = s.Split(','); return xy.Length == 2 ? new Vector2(F(xy[0]), F(xy[1])) : throw Usage($"'{s}' is not x,y"); }
        if (t == typeof(Settlement)) return SettlementArg(new[] { s }, 0);
        if (t == typeof(Party)) return C.PartyById(I(s)) ?? throw Usage($"no party #{s}");
        if (t == typeof(Character)) return C.S.Characters.FirstOrDefault(ch => ch.Id == I(s)) ?? throw Usage($"no character #{s}");
        if (t == typeof(CompanyRecord)) return Companies().FirstOrDefault(co => co.Id == I(s)) ?? throw Usage($"no company #{s}");
        if (t == typeof(TroopStack))
        {
            var p = s.Split('.');
            var co = p.Length == 2 ? Companies().FirstOrDefault(c => c.Id == I(p[0])) : null;
            return co != null && I(p[1]) >= 0 && I(p[1]) < co.Stacks.Count ? co.Stacks[I(p[1])] : throw Usage($"'{s}' is not <company id>.<stack index>");
        }
        if (t.IsAssignableFrom(typeof(string[]))) return s.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (t.IsAssignableFrom(typeof(List<string>))) return s.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        throw Usage($"cannot pass a {Short(t)} from text");
    }

    IEnumerable<CompanyRecord> Companies() => Me.Companies.Concat(C.S.Settlements.SelectMany(x => x.Mercenaries.Concat(x.Garrison)))
        .Concat(C.S.Parties.SelectMany(p => p.Companies));

    /// `get <path>`: read anything from the campaign: `S.Gold`, `Captain`, `Player.Companies[0].Stacks`, `S.Settlements[4]`,
    /// `Map.Sites[2]`. With a battle on, a path starting `B.` reads the battle (`B.Squads[3].Units[0]`).
    string Get(string path)
    {
        if (path == "") return "get <path>: members of Campaign are: " + string.Join(" ", typeof(Campaign).GetMembers(Pub).Where(m => m is FieldInfo or PropertyInfo).Select(m => m.Name).Distinct().OrderBy(n => n));
        var (obj, _, _) = Resolve(path);
        return Dump(obj, 2);
    }

    /// `set <path> <value>`: change a field by hand (for setting up a test). Marks the session's findings as tampered.
    string Set(string[] a)
    {
        string path = Need(a, 0, "path"), value = Need(a, 1, "value");
        var (_, owner, member) = Resolve(path);
        if (owner == null || member == null) throw Usage("cannot set that");
        switch (member)
        {
            case FieldInfo f when !f.IsInitOnly: f.SetValue(owner, Convert(value, f.FieldType)); break;
            case PropertyInfo p when p.CanWrite: p.SetValue(owner, Convert(value, p.PropertyType)); break;
            default: throw Usage($"{member.Name} is read-only");
        }
        tampered = true;
        return $"{path} = {Get(path)}";
    }

    (object? Value, object? Owner, MemberInfo? Member) Resolve(string path)
    {
        object? cur = C, owner = null;
        MemberInfo? member = null;
        if (path.StartsWith("B.") || path == "B") { cur = B ?? throw Usage("no battle on"); path = path.Length > 2 ? path[2..] : ""; }
        foreach (var raw in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            string part = raw;
            var idx = new List<string>();
            while (part.EndsWith(']') && part.LastIndexOf('[') is int o and > 0) { idx.Insert(0, part[(o + 1)..^1]); part = part[..o]; }
            if (cur == null) throw Usage($"null before .{part}");
            var t = cur.GetType();
            member = (MemberInfo?)t.GetField(part, Pub | BindingFlags.IgnoreCase) ?? t.GetProperty(part, Pub | BindingFlags.IgnoreCase)
                ?? throw Usage($"{Short(t)} has no public '{part}'");
            owner = cur;
            cur = member is FieldInfo fi ? fi.GetValue(cur) : ((PropertyInfo)member).GetValue(cur);
            foreach (var k in idx)
            {
                owner = null; member = null;
                cur = cur switch
                {
                    IDictionary d => d.Contains(k) ? d[k] : int.TryParse(k, out int ki) && d.Contains(ki) ? d[ki] : throw Usage($"no key '{k}'"),
                    IList l => int.TryParse(k, out int li) && li >= 0 && li < l.Count ? l[li] : throw Usage($"index '{k}' out of 0..{l.Count - 1}"),
                    IEnumerable e => int.TryParse(k, out int ei) ? e.Cast<object>().ElementAtOrDefault(ei) : throw Usage($"index '{k}'"),
                    _ => throw Usage($"{part} cannot be indexed"),
                };
            }
        }
        return (cur, owner, member);
    }

    /// A compact text form of any value: numbers, strings, points, lists (first 40) and objects (public fields and
    /// properties, `depth` levels deep).
    static string Dump(object? v, int depth, string indent = "")
    {
        switch (v)
        {
            case null: return "null";
            case string s: return s;
            case float f: return f.ToString("0.###", CultureInfo.InvariantCulture);
            case double d: return d.ToString("0.###", CultureInfo.InvariantCulture);
            case Vector2 p: return P(p);
            case Vector3 p3: return $"({p3.X:0.0},{p3.Y:0.0},{p3.Z:0.0})";
            case Enum or bool or int or long or char: return v.ToString()!;
            case System.Runtime.CompilerServices.ITuple tu: return "(" + string.Join(", ", Enumerable.Range(0, tu.Length).Select(i => Dump(tu[i], depth - 1))) + ")";
            case IDictionary dict:
                if (depth <= 0) return $"{{{dict.Count} entries}}";
                return "{" + string.Join(", ", dict.Keys.Cast<object>().Take(40).Select(k => $"{k}: {Dump(dict[k], depth - 1, indent + "  ")}")) + (dict.Count > 40 ? ", ..." : "") + "}";
            case IEnumerable list:
            {
                var items = list.Cast<object?>().Take(41).ToList();
                if (depth <= 0) return $"[{(items.Count > 40 ? "40+" : items.Count)} items]";
                if (items.All(x => x is null or string or float or int or double or bool or Enum or Vector2))
                    return "[" + string.Join(", ", items.Take(40).Select(x => Dump(x, 0))) + (items.Count > 40 ? ", ..." : "") + "]";
                return string.Concat(items.Take(40).Select((x, i) => $"\n{indent}[{i}] {Dump(x, depth - 1, indent + "  ")}")) + (items.Count > 40 ? $"\n{indent}..." : "");
            }
        }
        var t = v.GetType();
        if (depth <= 0) return t.GetProperty("Name")?.GetValue(v) is string n ? $"<{t.Name} {n}>" : t.GetField("Name")?.GetValue(v) is string fn ? $"<{t.Name} {fn}>" : $"<{t.Name}>";
        var sb = new StringBuilder($"<{t.Name}>");
        foreach (var m in t.GetMembers(Pub).Where(m => m is FieldInfo || m is PropertyInfo { CanRead: true } pi && pi.GetIndexParameters().Length == 0))
        {
            object? val;
            try { val = m is FieldInfo f ? f.GetValue(v) : ((PropertyInfo)m).GetValue(v); }
            catch { continue; }
            sb.Append($"\n{indent}  {m.Name} = {Dump(val, depth - 1, indent + "    ")}");
        }
        return sb.ToString();
    }

    // ---------- crashes and the audit ----------

    /// File a crash with its stack and the commands that led to it; roll the campaign back to before the command.
    string Crash(string line, Exception e, string? before)
    {
        string dir = Path.Combine(Dir, "crashes");
        Directory.CreateDirectory(dir);
        string id = $"crash-{Directory.GetFiles(dir, "crash-*.txt").Length + 1:000}";
        File.WriteAllText(Path.Combine(dir, id + ".txt"), $"command: {line}\nday {C.Day}, command #{history.Count}{(B != null ? $", battle t={B.Time:0}" : "")}\n\n{e}\n\nlast commands:\n{string.Join("\n", history.TakeLast(40))}\n");
        string? state = null;
        if (before != null) { state = Path.Combine(dir, id + ".state.json"); File.WriteAllText(state, before); }
        // A throw from the harness's own code is the harness's bug, not the game's.
        bool harness = new System.Diagnostics.StackTrace(e).GetFrames().FirstOrDefault()?.GetMethod()?.DeclaringType is Type top
            && (top == typeof(PlaySession) || top.DeclaringType == typeof(PlaySession));
        Record(harness ? "harness" : "crash", harness ? "minor" : "blocker", $"{e.GetType().Name}: {e.Message} (on `{line}`)", detail: Path.Combine(dir, id + ".txt"));
        if (B != null) { B = null; setup = null; C.Encounter = null; return $"CRASH in the battle: {e.GetType().Name}: {e.Message}\nFiled as {id}. The battle is dropped; the campaign goes on (it may be off after this)."; }
        if (before != null) C = new Campaign(CampaignState.Deserialize(before));
        return $"CRASH: {e.GetType().Name}: {e.Message}\n  at {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}\nFiled as {id}; the campaign is rolled back to before the command.";
    }

    readonly HashSet<string> flagged = new();

    /// Look for numbers that cannot be right; each kind is filed once a session.
    string Audit()
    {
        var bad = new List<string>();
        static bool Ok(float f) => float.IsFinite(f);
        if (!double.IsFinite(C.S.Hours)) bad.Add("time is not finite");
        if (!Ok(C.S.Gold)) bad.Add($"gold is {C.S.Gold}");
        if (C.S.Gold < -0.5f) bad.Add($"gold below zero: {C.S.Gold:0.##}");
        if (!Ok(Me.Food) || Me.Food < -0.01f) bad.Add($"food is {Me.Food}");
        if (!Ok(C.Captain.Renown)) bad.Add($"renown is {C.Captain.Renown}");
        foreach (var p in C.S.Parties)
        {
            if (!Ok(p.Pos.X) || !Ok(p.Pos.Y)) bad.Add($"party #{p.Id} {p.Name} at {p.Pos}");
            foreach (var co in p.Companies) foreach (var st in co.Stacks)
                {
                    if (st.Men < 0 || st.Wounded < 0) bad.Add($"party #{p.Id} {co.Name} {st.Troop} has {st.Men} men, {st.Wounded} wounded");
                    if (!Ok(st.Xp)) bad.Add($"party #{p.Id} {co.Name} {st.Troop} xp {st.Xp}");
                }
        }
        foreach (var s in C.S.Settlements)
        {
            if (s.Recruits < 0) bad.Add($"{s.Name} has {s.Recruits} recruits");
            if (!Ok(s.Prosperity) || !Ok(s.Unrest)) bad.Add($"{s.Name} prosperity {s.Prosperity} unrest {s.Unrest}");
        }
        if (B != null)
            foreach (var u in B.Units)
                if (u.Active && (!Ok(u.Pos.X) || !Ok(u.Pos.Y) || !Ok(u.Hp))) { bad.Add($"battle unit of {u.Squad?.Name} at {u.Pos} hp {u.Hp}"); break; }
        var news = bad.Where(x => flagged.Add(x.Split(':', ' ')[0] + x.Split(' ').ElementAtOrDefault(1))).ToList();
        foreach (var x in news) Record("invariant", "major", x);
        return news.Count == 0 ? "" : "\nINVARIANT BROKEN (filed): " + string.Join("; ", news);
    }

    // ---------- help ----------

    static string Help(string? topic) => topic == "battle" ? HelpBattle : HelpMain;

    const string HelpMain = """
        READ   look | party | near [radius] | places [filter] | place <id|name|here> | realms | contracts | event | log [n] [all]
               troop <id> | get <path> (e.g. get Captain, get S.Settlements[3]) | methods [filter] | findings | scenarios
        MOVE   goto <id|name> | move <x> <y> | chase <party id> | stop | wait [hours=24] (stops early when something needs you)
        ANSWER fight (auto-resolve) | battle (fight it: see `help battle`) | retreat | parley | leave (a friendly meeting)
               choose <n> (an event) | talk <party id> [topic args...]
        TOWN   recruit <n> | hire <n> | accept <offer n> | buyfood <days> | promote <n> [men] | sell prisoners|goods | rest [hours]
        CAREER contract <realm id> | swear | besiege <id> | assault [fight]
        ANY    call <Method> [args] (every public Campaign method; ids for places, parties, companies; stack = co.idx; point = x,y)
               set <path> <value> (setup only: marks findings tampered) | save [label]
        REPORT bug [minor|major|blocker] <what happened, what you expected> | note <observation>
        """;

    const string HelpBattle = """
        BATTLE bstate | line <squads> x1 y1 x2 y2 (deploy or redeploy) | start (end deployment) | step [seconds=10]
               order <squads> hold | charge [enemy squad] | follow | advance [m] | fallback [m] | move x y | form Line|ShieldWall|Square|Wedge|Loose
                              | fire on|off | ai on|off | loose
               goal <division id> <Post:Stance> (Front:Hold|Advance|Charge, LeftWing/RightWing:Guard|Swing|Charge,
                              Skirmish:Loose|Harass|Screen, Reserve:Wait|Plug|Follow)
               auto [seconds] (the AI leads your side to the end) | withdraw (sound the retreat: a loss)
        <squads> = an index from bstate, a list 1,4,5, `all`, or div:<id>. Your side is at -y, the enemy at +y; metres.
        When the battle is decided the result goes back to the campaign by itself.
        """;
}

/// `--play <dir> [options]`: run one session. With `--script file` the commands are read from the file (or `-` for
/// standard input) and each reply printed; else the session serves `<dir>/play.sock` until `quit` or it sits idle.
public static class Playtest
{
    public static void Run(string[] args)
    {
        string? Opt(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
        int Int(string name, int def) => int.TryParse(Opt(name), out int v) ? v : def;
        string dir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? throw new ArgumentException("--play <session dir>");
        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        // The session's saves and settings live in its folder: nothing is shared with the real game or other sessions.
        if (Environment.GetEnvironmentVariable("IRONFIELD_SAVES") is not { Length: > 0 }) Environment.SetEnvironmentVariable("IRONFIELD_SAVES", Path.Combine(dir, "saves"));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var s = new PlaySession(dir);
        s.Begin(Int("--seed", 1), Int("--realm", 0), Opt("--scenario"), Opt("--load"), Int("--days", 0));
        Console.Error.WriteLine($"session ready in {sw.ElapsedMilliseconds} ms");
        if (Opt("--script") is string script)
        {
            var lines = script == "-" ? ReadAll(Console.In) : File.ReadAllLines(script);
            foreach (var line in lines)
            {
                if (line.Trim() is "quit" or "exit") break;
                string reply = s.Exec(line);
                if (reply != "") Console.WriteLine($"> {line}\n{reply}\n");
            }
            return;
        }
        Serve(s, Path.Combine(dir, "play.sock"), Int("--idle", 30));
    }

    static IEnumerable<string> ReadAll(TextReader r) { while (r.ReadLine() is string l) yield return l; }

    /// One command a connection: the client writes a line, the session answers and closes. Commands run one at a time.
    static void Serve(PlaySession s, string sock, int idleMinutes)
    {
        if (File.Exists(sock)) File.Delete(sock);
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(sock));
        listener.Listen(16);
        File.WriteAllText(Path.Combine(s.Dir, "pid"), Environment.ProcessId.ToString());
        Console.WriteLine($"serving {sock}");
        Console.Out.Flush();
        try
        {
            int idle = 0;
            while (true)
            {
                if (!listener.Poll(60_000_000, SelectMode.SelectRead)) { if (++idle >= idleMinutes) { Console.WriteLine("idle: closing"); break; } continue; }
                idle = 0;
                using var conn = listener.Accept();
                using var stream = new NetworkStream(conn);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string line = reader.ReadLine() ?? "";
                bool quit = line.Trim() is "quit" or "exit";
                string reply = quit ? "bye" : s.Exec(line);
                var bytes = Encoding.UTF8.GetBytes(reply + "\n");
                stream.Write(bytes);
                if (quit) break;
            }
        }
        finally { File.Delete(sock); File.Delete(Path.Combine(s.Dir, "pid")); }
    }
}
