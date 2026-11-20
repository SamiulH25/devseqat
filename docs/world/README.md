# The world map

The game's world seen from above, drawn by two authors: **you** (the person) and **the model** (whichever model is
running). One file holds it; one page lets the person see it, change it and talk about it. The game builds the map every
new campaign plays on (layout 5) from this file (plan and status: `docs/archive/WORLDMAP.md`).

| File | What it is |
|---|---|
| `Data/world.json` | The map. The only source of truth. Both authors edit it; the game reads it. |
| `docs/world/index.html` | The person's page: the map from above, its layers, editing tools and the notes. No build, no libraries. |
| `tools/world.py` | Serves the page, validates the file, prints what waits for an author, saves it in the page's layout. |

## Using it (the person)

```bash
tools/world.py          # then open http://localhost:8765
```

Drag, add and remove provinces, settlements, sites, rivers, ridges, lakes, roads and sea lanes with the tools; colour by
realm, culture or land. **Notes** are pins on the map with a thread of talk: write in a note to remark or ask, and mark it
done when it is settled. **Save** (or Ctrl+S) writes `Data/world.json` and keeps the last version as `world.json.bak`.
Opened straight from disk without the server, the page asks you to pick the file once and then saves back into it
(Chromium), or downloads it (others).

## The file

```json
{
  "version": 2, "title": "...", "revision": 12, "updated": "2026-11-17", "source": "...", "extent": 620,
  "kinds": ["Meadow", "Farmland", "Coast", "...", "Sea"],
  "realms": [ {"id": 0, "name": "Azure Crown", "culture": "azure", "colour": "#365eb0"} ],
  "provinces": [ {"id": 0, "name": "Alexandria", "kind": "Coast", "realm": 3, "x": -232.1, "y": 288.3, "subculture": "southern_rift"} ],
  "settlements": [ {"id": 0, "name": "Orvalle", "kind": "town", "realm": 2, "culture": "northern", "subculture": "northern_fjord", "x": -96.9, "y": -248.2, "port": true} ],
  "sites": [ {"id": 0, "name": "Orvalle Mill", "kind": "mill", "x": -343.1, "y": -320.1} ],
  "rivers": [ {"id": 0, "width": 5.2, "points": [[-235, -115], [-255, -35]]} ],
  "ridges": [ {"id": 0, "width": 24, "points": [[-275, -145], [-215, -125]]} ],
  "lakes": [ {"id": 0, "x": -402.4, "y": -336.6, "radius": 2.9} ],
  "roads": [ {"id": 0, "minor": false, "points": [[-96.9, -248.2], [-70.7, -183.3]]} ],
  "sealanes": [ {"id": 0, "points": [[-96.9, -248.2], [-66.6, -362.5]]} ],
  "subcultures": [ {"id": "azure_heart", "parent": "azure", "name": "Crownlanders", "colour": "#4a78d0", "flavour": "..."} ],
  "notes": [ {"id": 0, "x": -294.8, "y": 95.6, "title": "...", "by": "model", "done": false,
              "thread": [ {"by": "model", "at": "2026-11-17", "text": "...", "ask": true} ]} ]
}
```

- World units: x east, y south, the middle at 0,0; everything lies within `extent` of it.
- **Provinces** are points. The land a province holds is everything nearer its centre than any other's after a noise warp
  (`warp` and `provinceAt` in the page; the game ports them exactly). A `Sea` province is water and belongs to no realm
  (`realm: -1`).
- A settlement's `culture` is its realm's; its `subculture` (an id from `subcultures`, which are cultures in
  `Data/factions.json`) is the people who live there.
- **A note waits for an author** when it is not `done` and the last message in its thread (or the note itself, if nobody
  has spoken) is the other author's. `ask: true` marks a question.
- **revision** goes up by one on every save. The server refuses a save over a newer file.

## Rules for the model (read before touching the file)

1. Start with `tools/world.py --inbox`. It prints only the notes that wait for you. Answer every one: add a message to its
   `thread` with `"by": "model"` and today's date.
2. **Never move, rename or remove what the person drew** on your own, and never change their messages. Put a change
   forward as a note pinned where it applies (`"by": "model"`, a message with `ask: true`), and make it only once they
   agree in the thread. Mark a note `done` only when the person has settled it.
3. Before changing the world's lore, realms, history or tone, ask the same way: a note, not an edit.
4. Write the file with `tools/world.py`'s `save()` (it keeps the page's layout: one line an object, so a diff shows only
   what changed). After editing: add 1 to `revision`, set `updated`, and run `tools/world.py --check`. It must print
   `world.json ok`.
5. The game reads this file: after a change, run `tools/check.sh` (the layout 5 tests build the map from it).
6. Ids are whole numbers, unique in their list, and never reused; other things refer to them.
