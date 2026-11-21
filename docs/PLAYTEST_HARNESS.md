# Playtest harness: agents play the game by text

Agents (LLM sessions, scripts, people at a shell) play Ironfield headless through text commands and file what they find.
One session is one game process serving a Unix socket in its own folder, so any number run side by side with nothing
shared. Every session can be replayed exactly.

Code: `Playtest.cs` (session, commands, reading the world, findings), `PlaytestBattle.cs` (fought battles),
`PlaytestServer.cs` (`call`/`get`/`set` by reflection, crash capture, the audit, help text, the socket server and script
runner; `Playtest.Run` is `--play`). Tool: `tools/playtest.py`. Test: `SelfTest.PlaytestHarness` (Tests/CampaignTests.cs).

## Quick start

```bash
dotnet build -c Release                                   # once; the tool builds if the binary is missing
tools/playtest.py start alpha --seed 7                    # ~3 s; prints the first `look`
tools/playtest.py send alpha help
tools/playtest.py send alpha choose 0                     # a new game asks how the captain fights
tools/playtest.py send alpha wait 24                      # time passes until 24 h or something needs you
tools/playtest.py stop alpha                              # or let it close itself after --idle minutes (30)
tools/playtest.py list                                    # every session: running, commands, findings
tools/playtest.py report                                  # playtest/REPORT.md from all findings
```

Start options: `--seed N`, `--realm R` (0-6), `--scenario ID` (a tester save: `01-fresh-start` ... `12-castle-muster`,
see `TesterSaves.cs`; any part of the id), `--days N` (the stand-in captain plays N days first), `--load SAVE` (a save file).

A fleet: `tools/playtest.py fleet 8 --parallel 8 --brief "Test sieges" --scenario 07-siege --model sonnet` starts eight
sessions (seeds 1000+, realms cycling, scenarios cycling), runs one `claude -p` agent on each with the prompt in
`AGENT_PROMPT` (it may only run `tools/playtest.py send <its session>`), stops each when its agent ends, then writes the
report. `--brief-file`, `--budget N` (commands each agent aims for), `--agent-cmd "..."` for another agent (`{prompt}` and
`{name}` are filled in), `--no-build`.

Without a server: `tools/playtest.py run NAME --script cmds.txt [start options]` or
`dotnet bin/Release/net10.0/Ironfield.dll --play DIR --script FILE|-` runs a command file and prints each reply.

## Commands

`help` and `help battle` print the list. In short:

| Kind | Commands |
|---|---|
| Read (never change state, not logged) | `look`, `party`, `near [r]`, `places [filter]`, `place <id\|name\|here>`, `realms`, `contracts`, `event`, `log [n] [all]`, `troop <id>`, `get <path>`, `methods [filter]`, `findings`, `scenarios` |
| Move | `goto <id\|name>`, `move x y`, `chase <party id>`, `stop`, `wait [hours]` (stops early on an encounter, event, arrival, career beat, the lists) |
| Answer | `fight` (auto-resolve), `battle` (fight it), `retreat`, `parley`, `leave` (a friendly meeting), `choose <n>`, `talk <party id> [topic args]` |
| Town | `recruit n`, `hire n`, `accept n`, `buyfood days`, `promote n [men]`, `sell prisoners\|goods`, `rest [h]` |
| Career | `contract <realm>`, `swear`, `besiege <id>`, `assault [fight]` |
| Anything else | `call <Method> [args]`: every public `Campaign` method. Ids for settlements (or a name, or `here`), parties, characters and companies; a stack is `<company id>.<index>`; a point `x,y`; a list `a,b,c`; `null` |
| Setup | `set <path> <value>` (marks the session's findings TAMPERED), `save [label]` (a real save: open it in the game with `IRONFIELD_SAVES=playtest/NAME/saves`) |
| Report | `bug [minor\|major\|blocker] <text>` (files a save with it), `note <text>` |
| Battle | `bstate`, `line <squads> x1 y1 x2 y2`, `start`, `step [s]`, `order <squads> hold\|charge [foe]\|follow\|advance [m]\|fallback [m]\|move x y\|form <shape>\|fire on\|off\|ai on\|off\|loose`, `goal <division> Post:Stance`, `auto [s]`, `withdraw` |

`get` paths start at the `Campaign`: `get S.Gold`, `get Captain`, `get Player.Companies[0].Stacks`, `get S.Settlements[14]`;
with a battle on, `get B.Squads[3]`. Lists show 40 items, objects two levels deep.

Replies are plain text: one line of state first, then lists with `#id`s the next command can use. Distances are map units
on the campaign map and metres in battle (your side at -y, the enemy at +y, deployment kept to y < -12).

## What a session leaves (playtest/NAME/, gitignored)

| File | What |
|---|---|
| `session.json` | how it started (seed, realm, scenario, days, load) |
| `commands.log` | every command that may change state, in order: the replay |
| `transcript.log` | every command and reply, reads included: what the agent saw |
| `replies.log` | replies to the logged commands only (session folder written `<session>`): what replay compares |
| `findings.jsonl` | one JSON row a finding: kind (`bug`, `note`, `crash`, `invariant`, `harness`), severity, text, day, command count, battle, tampered, save, detail |
| `crashes/crash-NNN.txt` (+ `.state.json`) | the exception, stack, last 40 commands, and the state before the command |
| `saves/` | the session's own save folder (also its settings) |
| `server.log`, `agent.log` | the server's and the agent's output |

## How it behaves

- **Crashes** are caught: filed as `crash` (or `harness` when the throw is in the harness's own code), and the campaign is
  rolled back to the state before the command (a battle is dropped). The session goes on.
- **The audit** after every command that may change state looks for numbers that cannot be right (NaN or infinite gold,
  food, renown, positions, xp, prosperity; negative men, recruits, gold, food) and files each kind once a session as `invariant`.
- **Read commands are pure**: notices (arrived, career beat, battle report, the lists) stay up until time next passes, so
  `look` changes nothing and a replay with or without reads runs the same.
- **Replay**: `tools/playtest.py replay NAME` re-runs `commands.log` from `session.json` into `NAME-replay` and compares
  every reply; it says MATCHES or shows the first line that differs. A bug report is reproduced by replaying its session.
- **Battles** start in deployment as on screen (the remembered deployment applies); allied lords' companies show as ALLY
  and take no orders; the captain stands in command view and guards himself. A decided battle plays two more seconds (the
  rout is counted), then goes back to the campaign through `FinishBattle` and its report is printed.
- **Speed**: a session is ready in ~3 s and holds ~125 MB; a campaign week takes about a second; a battle runs ~10-15x real
  time with ~800 men. Commands in one session run one at a time; sessions are separate processes, so run one per core or more.

## Gotchas

- `wait` refuses while an encounter or event waits; answer it first.
- The tool runs `bin/Release/net10.0/Ironfield.dll` directly (not `dotnet run`), so parallel sessions never fight over a
  build. Rebuild (`dotnet build -c Release`) after changing the game, and restart sessions to pick it up.
- A session closes itself after `--idle` minutes without a command (30; fleets 20), removing its socket.
- `set` is for setting up a test; findings made after it say TAMPERED.
- The harness is headless: it cannot see the screens. For UI work use the shots env vars (CLAUDE.md), and open a session's
  save in the real game to look at a state an agent reported.
