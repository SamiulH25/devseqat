#!/usr/bin/env python3
"""Text-mode helper for editing the baked unit sprites in Data/sprites (docs/DEVELOPING.md 7 "Sprites as PNGs").

  tools/sprite_tool.py show KEY...      print each part as a character grid with its colour legend
                                        (KEY like helm.kettle.f or neutral/hbody; '*' marks team colours)
  tools/sprite_tool.py team             the palette-0 team colours: these, and only these, change per side
  tools/sprite_tool.py write KEY FILE   write a part from a grid file in the `show` format (legend + grid)
  tools/sprite_tool.py lint             check every PNG changed since HEAD: same size, alpha only 0 or 255
"""
import json, os, subprocess, sys, warnings
warnings.filterwarnings("ignore", category=DeprecationWarning)
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Data", "sprites")
CHARS = "#@%&$ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+=?!<>/\\|~^"


def path(key):
    if key.endswith(".png"): key = key[:-4]
    if "/" in key: return os.path.join(ROOT, key + ".png")
    p = os.path.join(ROOT, "parts", key + ".png")
    return p if os.path.exists(p) else os.path.join(ROOT, "neutral", key + ".png")


def team():
    m = json.load(open(os.path.join(ROOT, "manifest.json")))
    return set(m["Schemes"]["standard"]["Swaps"][1].keys())


def show(key):
    im = Image.open(path(key)).convert("RGBA")
    w, h = im.size
    px = list(im.getdata())
    tc = team()
    cols = sorted({(r, g, b) for r, g, b, a in px if a})
    sym = {c: CHARS[i % len(CHARS)] for i, c in enumerate(cols)}
    print(f"{key}  {w}x{h}  ('.' clear)")
    for c in cols:
        hx = "%02x%02x%02x" % c
        print(f"  {sym[c]} {hx}{' *team' if hx in tc else ''}")
    for y in range(h):
        print("  " + "".join(sym[px[y * w + x][:3]] if px[y * w + x][3] else "." for x in range(w)))


def write(key, grid_file):
    """Reads a `show`-style file: legend lines `  X rrggbb` then grid lines; '.' is clear. Size must match."""
    p = path(key)
    w, h = Image.open(p).size
    legend, rows = {}, []
    for line in open(grid_file):
        s = line.rstrip("\n").strip()
        parts = s.split()
        if len(parts) >= 2 and len(parts[0]) == 1 and len(parts[1]) == 6 and all(ch in "0123456789abcdefABCDEF" for ch in parts[1]):
            legend[parts[0]] = tuple(int(parts[1][i:i + 2], 16) for i in (0, 2, 4)); continue
        if s and len(s) == w and all(ch == "." or ch in legend for ch in s): rows.append(s)
    if len(rows) != h: sys.exit(f"{key}: grid has {len(rows)} rows of width {w}, needs {h}")
    im = Image.new("RGBA", (w, h))
    im.putdata([(0, 0, 0, 0) if ch == "." else legend[ch] + (255,) for row in rows for ch in row])
    im.save(p)
    print(f"wrote {p}")


def lint():
    out = subprocess.run(["git", "diff", "--name-only", "HEAD", "--", ROOT], capture_output=True, text=True, cwd=ROOT).stdout.split()
    top = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, cwd=ROOT).stdout.strip()
    bad = 0
    for f in out:
        if not f.endswith(".png"): continue
        rel = os.path.relpath(os.path.join(top, f), top)
        old = subprocess.run(["git", "show", f"HEAD:{rel}"], capture_output=True, cwd=top).stdout
        new = Image.open(os.path.join(top, f)).convert("RGBA")
        import io
        was = Image.open(io.BytesIO(old)).size if old else None
        alphas = {a for *_, a in new.getdata()}
        if was and was != new.size: print(f"BAD size {f}: {new.size} was {was}"); bad += 1
        if alphas - {0, 255}: print(f"BAD alpha {f}: partial alpha {sorted(alphas - {0, 255})[:5]}"); bad += 1
    print(f"{len(out)} changed, {bad} problems")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    a = sys.argv[1:]
    if not a: print(__doc__); sys.exit(0)
    if a[0] == "show": [show(k) for k in a[1:]]
    elif a[0] == "team": print(" ".join(sorted(team())))
    elif a[0] == "write": write(a[1], a[2])
    elif a[0] == "lint": lint()
    else: print(__doc__)
