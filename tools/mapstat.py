#!/usr/bin/env python3
"""Print what a WorldMap seed produces: provinces, settlements, roads, and the ground's size."""
import random
import math
import sys

LEGACY = 196.0
EXTENT = float(sys.argv[2]) if len(sys.argv) > 2 else 392.0
SEED = int(sys.argv[1]) if len(sys.argv) > 1 else 11


def generate(seed, extent):
    rng = random.Random(seed)
    span = extent / LEGACY
    spacing = 66 * span ** 0.3
    centres = []
    for _ in range(60000):
        if len(centres) >= 400:
            break
        p = ((rng.random() * 2 - 1) * extent * 0.92, (rng.random() * 2 - 1) * extent * 0.92)
        if all(math.hypot(p[0] - c[0], p[1] - c[1]) > spacing for c in centres):
            centres.append(p)
    n = len(centres)
    link = 160 * span
    neighbours = [[] for _ in range(n)]
    for i in range(n):
        near = sorted(range(n), key=lambda j: math.hypot(centres[i][0] - centres[j][0], centres[i][1] - centres[j][1]))[1:4]
        for j in near:
            if math.hypot(centres[i][0] - centres[j][0], centres[i][1] - centres[j][1]) < link:
                if j not in neighbours[i]:
                    neighbours[i].append(j)
                if i not in neighbours[j]:
                    neighbours[j].append(i)
    seen = {0}
    stack = [0]
    while stack:
        k = stack.pop()
        for j in neighbours[k]:
            if j not in seen:
                seen.add(j)
                stack.append(j)
    degrees = [len(a) for a in neighbours if a]
    return centres, neighbours, len(seen), degrees


centres, neighbours, reached, degrees = generate(SEED, EXTENT)
print(f"seed {SEED}, extent {EXTENT:.0f}: {len(centres)} provinces, {reached} reachable by road, "
      f"neighbours avg {sum(degrees) / max(1, len(degrees)):.2f}")
road = sum(math.hypot(centres[i][0] - centres[j][0], centres[i][1] - centres[j][1])
           for i in range(len(centres)) for j in neighbours[i] if j > i)
print(f"road length {road:,.0f} units ({road * 0.75:,.0f} km)")
