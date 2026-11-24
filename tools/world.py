#!/usr/bin/env python3
"""The world model (docs/world/): one JSON file that is the game's world seen from above, and a page that draws it.

  tools/world.py                 serve the page at http://localhost:8765 (it loads and saves Data/world.json)
  tools/world.py --check         validate world.json; exit 1 on a fault
  tools/world.py --summary       the world in a few lines: each realm's provinces, towns, castles, villages; the totals
  tools/world.py --inbox [you]   the notes that wait for the model (or for the person)
  tools/world.py --shot out.png  a picture of the map as the page draws it (needs chromium), so a model can look at it
  --file PATH (or IRONFIELD_WORLD=PATH) works on another map file, e.g. the 4x draft Data/world6.json (docs/pre-demo/WORLD_SCALE.md)

Only the standard library. The file's shape and the rules for changing it are in docs/world/README.md.
"""
import json, os, sys, shutil, subprocess, threading, http.server

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "docs", "world")
# The map is game data (the game builds layout 5 from it, docs/archive/WORLDMAP.md); the page lives beside its README.
FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Data", "world.json")
if "--file" in sys.argv:
    FILE = os.path.abspath(sys.argv.pop(sys.argv.index("--file") + 1)); sys.argv.remove("--file")
elif os.environ.get("IRONFIELD_WORLD"):
    FILE = os.path.abspath(os.environ["IRONFIELD_WORLD"])
AUTHORS = ["you", "model"]
SETTLEMENTS = ["town", "castle", "village"]
POINTS = ["provinces", "settlements", "sites", "lakes", "notes"]
LINES = ["rivers", "ridges", "roads", "sealanes", "cliffs", "uplands"]
UPLANDS = ["plateau", "massif", "hills"]


def load():
    with open(FILE, encoding="utf-8") as f:
        return json.load(f)


def dump(w):
    """The file's text as the page writes it: one line a key, one compact line an object in a list (a save makes no churn)."""
    def val(v):
        if isinstance(v, list) and v and isinstance(v[0], dict):
            return "[\n" + ",\n".join("    " + json.dumps(o, separators=(",", ":"), ensure_ascii=False) for o in v) + "\n  ]"
        return json.dumps(v, ensure_ascii=False)
    return "{\n" + ",\n".join(f"  {json.dumps(k)}: " + val(v) for k, v in w.items()) + "\n}\n"


def save(w):
    """Write the file as the page does (for a script or a model editing it): revision is the caller's to raise."""
    with open(FILE, "w", encoding="utf-8") as f:
        f.write(dump(w))


def faults(w):
    """Every fault in the file, as plain lines."""
    out = []
    ext = w.get("extent", 0)
    if not isinstance(ext, (int, float)) or ext <= 0:
        return ["no extent (half the width of the land, in world units)"]
    realms = {r.get("id") for r in w.get("realms", [])} | {-1}
    kinds = set(w.get("kinds", []))
    if len(realms) - 1 != len(w.get("realms", [])):
        out.append("two realms share an id")
    if not w.get("provinces"):
        out.append("no provinces: the land is made of them")
    for name in POINTS + LINES:
        seen = set()
        for o in w.get(name, []):
            where = f"{name} {o.get('id')}" + (f" '{o.get('name')}'" if o.get("name") else "")
            if not isinstance(o.get("id"), int):
                out.append(f"{where}: id must be a whole number")
            if o.get("id") in seen:
                out.append(f"{where}: id used twice")
            seen.add(o.get("id"))
            if name in POINTS:
                if not all(isinstance(o.get(k), (int, float)) for k in ("x", "y")):
                    out.append(f"{where}: no x, y")
                elif abs(o["x"]) > ext or abs(o["y"]) > ext:
                    out.append(f"{where}: at {o['x']}, {o['y']}, off the land (extent {ext})")
            else:
                p = o.get("points", [])
                if len(p) < 2 or not all(isinstance(q, list) and len(q) == 2 for q in p):
                    out.append(f"{where}: needs at least two [x, y] points")
                elif any(not isinstance(q[0], (int, float)) or not isinstance(q[1], (int, float)) or abs(q[0]) > ext or abs(q[1]) > ext for q in p):
                    out.append(f"{where}: a point is off the land (extent {ext})")
            if "realm" in o and o["realm"] not in realms:
                out.append(f"{where}: realm {o['realm']} does not exist")
            if name in ("provinces", "settlements", "sites") and not o.get("name"):
                out.append(f"{where}: no name")
    for r in w.get("realms", []):
        if not r.get("name"):
            out.append(f"realm {r.get('id')}: no name")
        if not r.get("culture"):
            out.append(f"realm {r.get('id')}: no culture")
        if not isinstance(r.get("colour"), str) or not r["colour"].startswith("#"):
            out.append(f"realm {r.get('id')}: bad colour")
    subs = {sc.get("id") for sc in w.get("subcultures", []) if sc.get("id")}
    for sc in w.get("subcultures", []):
        if not sc.get("id") or not sc.get("name") or not sc.get("parent"):
            out.append(f"subculture {sc.get('id')}: missing id, parent or name")
        if not isinstance(sc.get("colour"), str) or not sc["colour"].startswith("#"):
            out.append(f"subculture {sc.get('id')}: bad colour")
    for p in w.get("provinces", []):
        if kinds and p.get("kind") not in kinds:
            out.append(f"province '{p.get('name')}': land '{p.get('kind')}' is not one of {', '.join(sorted(kinds))}")
        if p.get("kind") == "Sea" and p.get("realm", -1) != -1:
            out.append(f"province '{p.get('name')}': sea cannot belong to a realm")
        if subs and p.get("subculture") and p["subculture"] not in subs:
            out.append(f"province '{p.get('name')}': unknown subculture '{p['subculture']}'")
    for s in w.get("settlements", []):
        if s.get("kind") not in SETTLEMENTS:
            out.append(f"settlement '{s.get('name')}': kind '{s.get('kind')}' is not town, castle or village")
        if s.get("kind") == "castle" and s.get("rank", 2) not in (1, 2, 3):
            out.append(f"settlement '{s.get('name')}': rank {s.get('rank')!r} is not 1 (tower house), 2 (castle) or 3 (great castle)")
        if s.get("kind") != "castle" and "rank" in s:
            out.append(f"settlement '{s.get('name')}': only castles have a rank")
        if subs and s.get("subculture") and s["subculture"] not in subs:
            out.append(f"settlement '{s.get('name')}': unknown subculture '{s['subculture']}'")
        if w.get("provinces") and all(isinstance(s.get(k), (int, float)) for k in ("x", "y")):
            if nearest(w, s["x"], s["y"]).get("kind") == "Sea":
                out.append(f"settlement '{s.get('name')}': sits in the sea")
    for r in w.get("ridges", []):
        if w.get("provinces") and any(nearest(w, q[0], q[1]).get("kind") == "Sea" for q in r.get("points", [])):
            out.append(f"ridge {r.get('id')}: sits in the sea")
    for c in w.get("cliffs", []):
        if not isinstance(c.get("height"), (int, float)) or c["height"] <= 0:
            out.append(f"cliff {c.get('id')}: no height (how far the land drops, in world units)")
    for u in w.get("uplands", []):
        if len(u.get("points", [])) < 3:
            out.append(f"upland {u.get('id')}: an area needs at least three points")
        if not isinstance(u.get("height"), (int, float)) or u["height"] <= 0:
            out.append(f"upland {u.get('id')}: no height")
        if u.get("kind") not in UPLANDS:
            out.append(f"upland {u.get('id')}: kind '{u.get('kind')}' is not one of {', '.join(UPLANDS)}")
    for s in w.get("sites", []):
        if w.get("provinces") and all(isinstance(s.get(k), (int, float)) for k in ("x", "y")):
            if nearest(w, s["x"], s["y"]).get("kind") == "Sea":
                out.append(f"site '{s.get('name')}': sits in the sea")
    for l in w.get("lakes", []):
        if not isinstance(l.get("radius"), (int, float)) or l["radius"] <= 0:
            out.append(f"lake {l.get('id')}: no radius")
    for n in w.get("notes", []):
        if n.get("by") not in AUTHORS:
            out.append(f"note {n.get('id')}: by '{n.get('by')}'")
        for m in n.get("thread", []):
            if m.get("by") not in AUTHORS or not m.get("text"):
                out.append(f"note {n.get('id')}: a message with no author or no words")
    return out


def waits_for(n, who):
    """Does a note wait on `who`? Not settled, and the last word (or the note itself, if nobody has spoken) is the other's."""
    if n.get("done"):
        return False
    t = n.get("thread", [])
    return (t[-1].get("by") if t else n.get("by")) != who


def nearest(w, x, y):
    return min(w["provinces"], key=lambda p: (p["x"] - x) ** 2 + (p["y"] - y) ** 2)


def inbox(w, who):
    rows = [n for n in w.get("notes", []) if waits_for(n, who)]
    print(f"{len(rows)} notes wait for {who} (revision {w.get('revision')})")
    for n in rows:
        p = nearest(w, n["x"], n["y"])
        print(f"\n[note {n['id']}] {n.get('title', '')}  at {n['x']}, {n['y']} (in {p['name']}, province {p['id']})")
        for m in n.get("thread", [])[-4:]:
            print(f"  {m['by']} {m.get('at', '')}{' ASKS' if m.get('ask') else ''}: {m['text']}")


def summary(w):
    print(f"{w.get('title')}: revision {w.get('revision')}, {2 * w['extent']:.0f} units across, from {w.get('source', '?')}")
    print(f"{'realm':28}{'prov':>6}{'town':>6}{'cast':>6}{'vill':>6}  culture")
    for r in w.get("realms", []) + [{"id": -1, "name": "(no realm)", "culture": ""}]:
        c = lambda k: sum(1 for s in w["settlements"] if s["realm"] == r["id"] and s["kind"] == k)
        p = sum(1 for q in w["provinces"] if q["realm"] == r["id"])
        if p or c("town") or c("castle") or c("village"):
            print(f"{r['name'][:27]:28}{p:>6}{c('town'):>6}{c('castle'):>6}{c('village'):>6}  {r.get('culture', '')}")
    land = {}
    sea = 0
    for p in w["provinces"]:
        if p["kind"] == "Sea":
            sea += 1
        else:
            land[p["kind"]] = land.get(p["kind"], 0) + 1
    print("land: " + ", ".join(f"{k} {v}" for k, v in sorted(land.items(), key=lambda t: -t[1])) + (f" | sea: {sea}" if sea else ""))
    print(f"{len(w['provinces']) - sea} land provinces ({sea} sea), {len(w['settlements'])} settlements ({sum(1 for s in w['settlements'] if s.get('port'))} ports), {len(w.get('subcultures', []))} subcultures, {len(w.get('rivers', []))} rivers, {len(w.get('lakes', []))} lakes, "
          f"{len(w.get('ridges', []))} ridges, {len(w.get('cliffs', []))} cliffs, {len(w.get('uplands', []))} uplands, {len(w.get('roads', []))} roads, {len(w.get('sealanes', []))} sea lanes, {len(w.get('sites', []))} sites, {len(w.get('notes', []))} notes")


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **k):
        super().__init__(*a, directory=ROOT, **k)

    def translate_path(self, path):
        # The page asks for world.json beside it; the file lives in Data/.
        return os.path.abspath(FILE) if path.split("?")[0] == "/world.json" else super().translate_path(path)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_POST(self):
        if self.path != "/save":
            self.send_error(404)
            return
        try:
            text = self.rfile.read(int(self.headers.get("Content-Length", 0))).decode("utf-8")
            w = json.loads(text)
            bad = faults(w)
            if bad:
                raise ValueError("; ".join(bad[:4]))
            # Refuse to overwrite a newer file (the model saved while the page was open).
            if os.path.exists(FILE) and load().get("revision", 0) > w.get("revision", 0) - 1:
                raise ValueError("the file on disk is newer than the page: reload, then make the change again")
            if os.path.exists(FILE):
                shutil.copyfile(FILE, FILE + ".bak")
            with open(FILE, "w", encoding="utf-8") as f:
                f.write(text)  # as the page laid it out: one thing a line
            body = json.dumps({"ok": True, "revision": w["revision"]}).encode()
            self.send_response(200)
        except Exception as x:  # the page shows the reason
            body = json.dumps({"ok": False, "error": str(x)}).encode()
            self.send_response(400)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *a):
        pass


def shot(path, port=8766):
    """A picture of the page (the map and its panels) through headless chromium."""
    exe = next((e for e in ("chromium", "chromium-browser", "google-chrome-stable", "google-chrome") if shutil.which(e)), None)
    if not exe:
        sys.exit("no chromium found: open the page in a browser instead")
    srv = http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    out = os.path.abspath(path)
    subprocess.run([exe, "--headless=new", "--no-sandbox", "--disable-gpu", "--hide-scrollbars", "--window-size=1600,1000",
                    "--virtual-time-budget=4000", f"--screenshot={out}", f"http://127.0.0.1:{port}/"],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=90, cwd=os.path.dirname(out) or ".")
    srv.shutdown()
    print(f"map -> {out}" if os.path.exists(out) else "no picture was made")


if __name__ == "__main__":
    a = sys.argv[1:]
    if "--check" in a:
        bad = faults(load())
        for b in bad[:40]:
            print(b)
        print("world.json ok" if not bad else f"{len(bad)} faults")
        sys.exit(1 if bad else 0)
    if "--summary" in a:
        summary(load())
    elif "--inbox" in a:
        i = a.index("--inbox")
        inbox(load(), a[i + 1] if len(a) > i + 1 else "model")
    elif "--shot" in a:
        i = a.index("--shot")
        shot(a[i + 1] if len(a) > i + 1 else "world.png")
    else:
        port = 8765
        print(f"the world, from above: http://localhost:{port}  (Ctrl+C to stop)")
        http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
