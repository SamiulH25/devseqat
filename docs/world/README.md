# The world book

How the final game world is going to be, worked out by two authors: **you** (the person) and **the model** (whichever
model is running). One file holds everything; one page lets the person read and answer it.

| File | What it is |
|---|---|
| `world.json` | The book. The only source of truth. Both authors edit it. |
| `index.html` | The person's page: read, answer, agree, turn down, add, edit. No build, no libraries. |
| `../../tools/world.py` | Serves the page, validates the file, prints what waits for an author. |

## Using it (the person)

```bash
tools/world.py          # then open http://localhost:8765
```

The page opens on **Waiting for you**: what the model has put forward or asked. On each card: write in the box and press
**Say** (a remark) or **Ask** (a question the model must answer); **Agree** or **Turn down** a proposal; **Edit** anything.
**Save** (or Ctrl+S) writes `world.json` and keeps the last version as `world.json.bak`. Opened straight from disk without
the server, the page asks you to pick `world.json` once and then saves back into it (Chromium), or downloads it (others).

## The file

```json
{
  "version": 1, "title": "...", "revision": 7, "updated": "2026-11-16",
  "authors":  { "you": "You", "model": "The model" },
  "statuses": { "built": "...", "agreed": "...", "proposed": "...", "open": "...", "rejected": "..." },
  "sections": [ { "id": "realms", "title": "Realms", "blurb": "..." } ],
  "entries":  [ {
      "id": "realm-azure-crown", "section": "realms", "title": "Azure Crown",
      "status": "built", "by": "model",
      "body": "What the entry says, in plain sentences.",
      "facts": { "culture": "Azure" },
      "links": [ "culture-azure" ],
      "source": "Data/factions.json",
      "thread": [ { "by": "model", "at": "2026-11-16", "text": "...", "ask": true } ]
  } ]
}
```

- **status**: `built` (in the game today), `agreed` (decided, not built), `proposed` (put forward, waiting for the other
  author), `open` (a question with no answer yet), `rejected` (turned down, kept for the record).
- **by**: who wrote the entry, `you` or `model`.
- **thread**: the talk on the entry, oldest first. `ask: true` marks a question.
- **An entry waits for an author** when the last message in its thread is the other author's, or when it has no thread and
  the other author put it forward as `proposed` or `open`. Rejected entries wait for nobody.
- **revision** goes up by one on every save. The server refuses a save over a newer file.

## Rules for the model (read before touching the file)

1. Start with `tools/world.py --inbox`. It prints only what waits for you, so you do not need to read the whole file.
   Answer every item: add a message to its `thread` with `"by": "model"` and today's date.
2. **Never change the person's words**: not their entries' `body`, not their messages. Reply in the thread instead.
3. **Never set `agreed` or `rejected` yourself** on something the person has not ruled on. New ideas go in as
   `"status": "proposed", "by": "model"`. A question for the person is an entry in the `questions` section with
   `"status": "open"` and a thread message with `"ask": true`.
4. When the person agrees an answer to a question, write the result up as its own entry in the right section
   (`"status": "agreed"`, with `links` back to the question) and say so in the question's thread.
5. `built` means it is in the game's data or code now. Give the `source` (a file). Do not mark an idea `built`.
   When something agreed is built, change its status to `built` and name the source. When the game's data changes,
   bring the matching `built` entries up to date.
6. Keep entries short and concrete: what is true of the world, not how to code it. One idea an entry. Put numbers and
   names in `facts`. Link related entries by `id`.
7. Ids are lower-case words with hyphens, unique, and never changed once made (other entries link to them).
8. After editing: add 1 to `revision`, set `updated`, and run `tools/world.py --check`. It must print `world.json ok`.
9. Do not delete entries. Turn-downs stay as `rejected`, so the same idea is not proposed twice.
