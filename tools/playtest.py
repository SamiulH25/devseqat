#!/usr/bin/env python3
"""Ironfield playtest harness: run headless game sessions that agents play by text commands (docs/PLAYTEST_HARNESS.md).

Each session is one game process (`Ironfield.dll --play <dir>`) serving a Unix socket in its own folder under
playtest/ (gitignored): its own saves, settings, command log, findings and crashes. Nothing is shared, so any number run
side by side.

  tools/playtest.py start NAME [--seed N] [--realm R] [--scenario ID] [--days N] [--load SAVE]
  tools/playtest.py send NAME <command...>        e.g. send alpha wait 24     (`send alpha help` lists commands)
  tools/playtest.py stop NAME|--all
  tools/playtest.py list
  tools/playtest.py run NAME --script FILE [...]  one-shot: run a command file, print replies, no server
  tools/playtest.py replay NAME                   re-run a session's commands.log from the same start, compare findings
  tools/playtest.py fleet N [--brief TEXT|--brief-file F] [--parallel P] [--scenario ID ...] [--model M] [--agent-cmd CMD]
                                                  start N sessions and an agent (claude -p) on each, then write the report
  tools/playtest.py report                        merge every session's findings and crashes into playtest/REPORT.md
"""
import argparse, json, os, shlex, signal, socket, subprocess, sys, time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DLL = ROOT / "bin" / "Release" / "net10.0" / "Ironfield.dll"
HOME = Path(os.environ.get("IRONFIELD_PLAYTEST", ROOT / "playtest"))


def session_dir(name):
    if not name.replace("-", "").replace("_", "").isalnum():
        sys.exit(f"bad session name '{name}': letters, digits, - and _ only")
    return HOME / name


def build(force=False):
    if DLL.exists() and not force:
        return
    print("building (Release)...", file=sys.stderr)
    subprocess.run(["dotnet", "build", "-c", "Release", "-v", "q", "-nologo"], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)


def game_args(a):
    out = []
    for flag in ("seed", "realm", "scenario", "days", "load"):
        v = getattr(a, flag, None)
        if v is not None:
            out += [f"--{flag}", str(v)]
    return out


def env_for(d):
    e = dict(os.environ, IRONFIELD_SAVES=str(d / "saves"), DOTNET_CLI_TELEMETRY_OPTOUT="1",
             ALSOFT_DRIVERS="null", SDL_AUDIODRIVER="dummy", DOTNET_gcServer="0", DOTNET_GCHeapHardLimit="0x60000000")
    return e


def alive(d):
    pid = d / "pid"
    if not pid.exists():
        return False
    try:
        os.kill(int(pid.read_text()), 0)
        return True
    except (OSError, ValueError):
        return False


def start(name, extra, idle=30, quiet=False):
    build()
    d = session_dir(name)
    if alive(d):
        sys.exit(f"{name} is already running ({d})")
    if d.exists():
        for f in ("commands.log", "findings.jsonl", "transcript.log", "replies.log", "play.sock", "pid"):
            (d / f).unlink(missing_ok=True)
    d.mkdir(parents=True, exist_ok=True)
    log = open(d / "server.log", "w")
    p = subprocess.Popen(["dotnet", str(DLL), "--play", str(d), "--idle", str(idle)] + extra, cwd=ROOT, env=env_for(d),
                         stdout=log, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL, start_new_session=True)
    sock = d / "play.sock"
    deadline = time.time() + 600
    while not sock.exists():
        if p.poll() is not None:
            sys.exit(f"{name} failed to start:\n{(d / 'server.log').read_text()[-3000:]}")
        if time.time() > deadline:
            p.kill()
            sys.exit(f"{name}: no socket after 10 minutes")
        time.sleep(0.1)
    if not quiet:
        print(f"{name} ready (pid {p.pid}, {d})")
        print(send(name, "look"))
    return d


def send(name, command, timeout=900):
    d = session_dir(name)
    sock = d / "play.sock"
    if not sock.exists():
        return f"ERROR: session {name} is not running (tools/playtest.py start {name})"
    s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    s.settimeout(timeout)
    try:
        s.connect(str(sock))
        s.sendall((command.replace("\n", " ") + "\n").encode())
        chunks = []
        while True:
            b = s.recv(65536)
            if not b:
                break
            chunks.append(b)
        return b"".join(chunks).decode(errors="replace").rstrip()
    except (ConnectionRefusedError, FileNotFoundError):
        return f"ERROR: session {name} is not answering (crashed? see {d / 'server.log'})"
    finally:
        s.close()


def stop(name):
    d = session_dir(name)
    if (d / "play.sock").exists():
        send(name, "quit", timeout=30)
    for _ in range(50):
        if not alive(d):
            break
        time.sleep(0.1)
    if alive(d):
        os.kill(int((d / "pid").read_text()), signal.SIGTERM)
    print(f"{name} stopped")


def sessions():
    return sorted(p for p in HOME.iterdir() if p.is_dir() and (p / "session.json").exists()) if HOME.exists() else []


def findings(d):
    f = d / "findings.jsonl"
    return [json.loads(l) for l in f.read_text().splitlines() if l.strip()] if f.exists() else []


def cmd_list(_):
    for d in sessions():
        fs = findings(d)
        n = sum(1 for _ in open(d / "commands.log")) if (d / "commands.log").exists() else 0
        print(f"{d.name:24} {'RUNNING' if alive(d) else 'stopped':8} {n:5} commands  {len(fs):3} findings  {json.loads((d / 'session.json').read_text())}")


def report(_=None):
    rows = [f for d in sessions() for f in findings(d)]
    order = {"crash": 0, "invariant": 1, "bug": 2, "harness": 3, "note": 4}
    sev = {"blocker": 0, "major": 1, "minor": 2, "note": 3}
    rows.sort(key=lambda r: (order.get(r["kind"], 9), sev.get(r["severity"], 9), r["session"]))
    out = ["# Playtest report", "", f"{len(sessions())} sessions, {len(rows)} findings.", ""]
    for kind in order:
        group = [r for r in rows if r["kind"] == kind]
        if not group:
            continue
        out += [f"## {kind} ({len(group)})", ""]
        for r in group:
            where = f"{r['session']} day {r['day']}, command {r['command']}" + (", in battle" if r.get("battle") else "") + (", TAMPERED" if r.get("tampered") else "")
            out.append(f"- **{r['severity']}** {r['text']}  \n  _{where}_" + (f"  \n  save: `{r['save']}`" if r.get("save") else "") + (f"  \n  detail: `{r['detail']}`" if r.get("detail") else ""))
        out.append("")
    HOME.mkdir(parents=True, exist_ok=True)
    (HOME / "REPORT.md").write_text("\n".join(out))
    print("\n".join(out[:4]) + f"written {HOME / 'REPORT.md'}")


AGENT_PROMPT = """You are a playtester for Ironfield, a medieval battle and campaign game. You play it through a text harness.
Your session is named {name}. Send every game command with exactly this shell command:

    tools/playtest.py send {name} <command>

Start with `tools/playtest.py send {name} help`, then `look`. Answer what the game asks (events, encounters), and play
as a real player would. Use `help battle` before your first fought battle.

YOUR BRIEF: {brief}

Report what you find, as you find it:
  - `send {name} bug [minor|major|blocker] <what happened, what you expected, how to get there>` for anything broken,
    confusing, unfair or plainly wrong (numbers that make no sense, an action refused for no reason, something stuck).
  - `send {name} note <text>` for balance, pacing and fun observations.
The harness files crashes and broken numbers by itself. Do not read or edit the game's source; only play.
Play for about {budget} commands, then send `note SUMMARY: ...` with your overall impressions, and stop."""


def fleet(a):
    build(force=not a.no_build)
    brief = Path(a.brief_file).read_text() if a.brief_file else a.brief
    scenarios = a.scenario or [None]
    names = []
    jobs = []
    for i in range(a.n):
        name = f"{a.prefix}{i + 1:02d}"
        sc = scenarios[i % len(scenarios)]
        extra = ["--seed", str(a.seed + i), "--realm", str(i % 7 if a.realm is None else a.realm)] + (["--scenario", sc] if sc else [])
        names.append(name)
        jobs.append((name, extra))

    def one(job):
        name, extra = job
        d = start(name, extra, idle=a.idle, quiet=True)
        prompt = AGENT_PROMPT.format(name=name, brief=brief, budget=a.budget)
        toks = shlex.split(a.agent_cmd.replace("{name}", name))
        cmd = [prompt if t == "{prompt}" else t for t in toks] + ([] if "{prompt}" in toks else [prompt])
        t0 = time.time()
        with open(d / "agent.log", "w") as log:
            rc = subprocess.run(cmd, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL).returncode
        stop(name)
        return f"{name}: agent exit {rc} after {time.time() - t0:.0f}s, {len(findings(d))} findings"

    with ThreadPoolExecutor(max_workers=a.parallel) as pool:
        for line in pool.map(one, jobs):
            print(line, flush=True)
    report()


def replay(a):
    """Re-run a session's commands from the same start into NAME-replay and say whether its findings match."""
    src = session_dir(a.name)
    meta = json.loads((src / "session.json").read_text())
    extra = sum(([f"--{k}", v] for k, v in meta.items()), [])
    dst = session_dir(a.name + "-replay")
    dst.mkdir(parents=True, exist_ok=True)
    for f in ("commands.log", "findings.jsonl", "transcript.log", "replies.log"):
        (dst / f).unlink(missing_ok=True)
    build()
    out = subprocess.run(["dotnet", str(DLL), "--play", str(dst), "--script", str(src / "commands.log")] + extra,
                         cwd=ROOT, env=env_for(dst), capture_output=True, text=True)
    (dst / "replay.txt").write_text(out.stdout + out.stderr)
    a_, b_ = (src / "replies.log").read_text().splitlines(), (dst / "replies.log").read_text().splitlines()
    diff = next((i for i, (x, y) in enumerate(zip(a_, b_)) if x != y), None if len(a_) == len(b_) else min(len(a_), len(b_)))
    n = sum(1 for _ in open(src / "commands.log"))
    if diff is None:
        print(f"replayed {n} commands into {dst}: every reply MATCHES (the session is reproducible)")
    else:
        print(f"replayed {n} commands into {dst}: replies DIFFER from line {diff + 1}:\n  was: {a_[diff] if diff < len(a_) else '<end>'}\n  now: {b_[diff] if diff < len(b_) else '<end>'}")
        sys.exit(1)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    def game_opts(p):
        p.add_argument("--seed", type=int)
        p.add_argument("--realm", type=int)
        p.add_argument("--scenario", help="a tester scenario id (or part): 01-fresh-start ... 12-castle-muster")
        p.add_argument("--days", type=int, help="let the stand-in captain play this many days first")
        p.add_argument("--load", help="start from a save file")

    p = sub.add_parser("start"); p.add_argument("name"); game_opts(p); p.add_argument("--idle", type=int, default=30, help="minutes idle before it closes")
    p = sub.add_parser("send"); p.add_argument("name"); p.add_argument("command", nargs=argparse.REMAINDER)
    p = sub.add_parser("stop"); p.add_argument("name", nargs="?"); p.add_argument("--all", action="store_true")
    sub.add_parser("list")
    sub.add_parser("report")
    p = sub.add_parser("run"); p.add_argument("name"); p.add_argument("--script", required=True); game_opts(p)
    p = sub.add_parser("replay"); p.add_argument("name")
    p = sub.add_parser("fleet"); p.add_argument("n", type=int)
    p.add_argument("--brief", default="Play the campaign from the start for a while: travel, recruit, fight, take work, rise in rank. Find bugs and rough edges.")
    p.add_argument("--brief-file")
    p.add_argument("--parallel", type=int, default=max(1, (os.cpu_count() or 4) // 2))
    p.add_argument("--scenario", action="append", help="repeatable: sessions cycle through these scenarios")
    p.add_argument("--seed", type=int, default=1000)
    p.add_argument("--realm", type=int)
    p.add_argument("--prefix", default="pt")
    p.add_argument("--budget", type=int, default=150, help="commands each agent aims to play")
    p.add_argument("--idle", type=int, default=20)
    p.add_argument("--model", default=None)
    p.add_argument("--agent-cmd", default=None, help="the agent command; {prompt} is replaced by the prompt (else appended), {name} by the session")
    p.add_argument("--no-build", action="store_true", help="use the built Release binary as is")
    a = ap.parse_args()

    if a.cmd == "start":
        start(a.name, game_args(a), idle=a.idle)
    elif a.cmd == "send":
        if not a.command:
            sys.exit("send NAME <command>")
        print(send(a.name, " ".join(a.command)))
    elif a.cmd == "stop":
        if not a.all and not a.name:
            sys.exit("stop NAME or stop --all")
        for d in (sessions() if a.all else [session_dir(a.name)]):
            if alive(d) or (d / "play.sock").exists():
                stop(d.name)
    elif a.cmd == "list":
        cmd_list(a)
    elif a.cmd == "report":
        report()
    elif a.cmd == "run":
        build()
        d = session_dir(a.name)
        d.mkdir(parents=True, exist_ok=True)
        sys.exit(subprocess.run(["dotnet", str(DLL), "--play", str(d), "--script", a.script] + game_args(a), cwd=ROOT, env=env_for(d)).returncode)
    elif a.cmd == "replay":
        replay(a)
    elif a.cmd == "fleet":
        if a.agent_cmd is None:
            me = Path(__file__).name
            a.agent_cmd = (f"claude -p {{prompt}} --allowedTools 'Bash(tools/{me} send {{name}}:*)'"
                           + (f" --model {a.model}" if a.model else ""))
        fleet(a)


if __name__ == "__main__":
    main()
