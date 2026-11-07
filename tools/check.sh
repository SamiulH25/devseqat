#!/bin/bash
# The one command to run before every commit:  tools/check.sh
# Builds, checks the rules below, and runs the self-test. Ends with "ALL CHECKS PASSED" or stops at the first failure.
#   tools/check.sh --quick   skip the self-test (about a minute); use while iterating, never as the final check.
set -uo pipefail
cd "$(dirname "$0")/.."
fail() { echo; echo "CHECK FAILED: $1"; exit 1; }

echo "== build"
out=$(dotnet build -c Release 2>&1)
echo "$out" | grep -E ' error ' | sort -u
echo "$out" | grep -q ' 0 Error(s)' || fail "build errors (listed above)"

# Headless code: the battle and campaign sims, tests and tools that run with --selftest/--balance (no window).
headless=$(ls Battle*.cs Squad.cs Units.cs Troops.cs Terrain.cs Factions.cs Replay.cs BalanceReport.cs SelfTest.cs Campaign/*.cs Tests/*.cs | grep -v '^BattleScreen')
echo "== headless code uses no MonoGame graphics, audio or input"
grep -nE 'Xna\.Framework\.(Graphics|Audio|Input)|GraphicsDevice|SpriteBatch|Texture2D|Keyboard\.|Mouse\.' $headless \
  && fail "headless files above use MonoGame graphics/audio/input (move that code to a screen or Hud file)"

echo "== battle sim is deterministic (seeded rng only)"
sim=$(ls Battle*.cs Squad.cs Units.cs Terrain.cs | grep -v '^BattleScreen')
grep -nE 'Random\.Shared|new Random\(\)|DateTime\.|Stopwatch|Environment\.TickCount' $sim \
  && fail "battle sim above uses unseeded randomness or the clock (use the battle's own rng; replays must reproduce)"

echo "== docs/CODEMAP.md is current"
tools/codemap.sh --check >/dev/null || fail "docs/CODEMAP.md is stale: run tools/codemap.sh and commit it (new files must be 'git add'ed first)"

if [ "${1:-}" != "--quick" ]; then
  echo "== self-test (about a minute)"
  dotnet run -c Release --no-build -- --selftest 2>&1 | tee /tmp/ironfield-selftest.log | grep -iE 'fail|exception|selftest ok'
  tail -1 /tmp/ironfield-selftest.log | grep -q 'selftest ok' || fail "self-test failed: see /tmp/ironfield-selftest.log"
fi
echo
echo "ALL CHECKS PASSED"
