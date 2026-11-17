#!/usr/bin/env python3
"""Map the troop trees in Data/troops.json: the trunk, each culture's ladder and branches, the bridges
between cultures, the knight orders, the mages and the wild roster. Prints markdown; --write puts it in
docs/TROOPS.md."""
import json
import os
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NUMERALS = {0: "0", 1: "I", 2: "II", 3: "III", 4: "IV", 5: "V", 6: "VI", 7: "VII"}
CLASSES = ["Infantry", "Skirmisher", "Archer", "Cavalry", "Caster", "Captain"]
CULTURE_ORDER = ["azure", "crimson", "northern", "southern", "steppe", "league", "order"]


def read(name):
    """Parse a Data/*.json file, dropping the // lines its comments live on."""
    with open(os.path.join(ROOT, "Data", name)) as f:
        text = f.read()
    return json.loads("\n".join(l for l in text.splitlines() if not l.lstrip().startswith("//")))


def numeral(tier):
    return NUMERALS.get(tier, str(tier))


def main():
    troops = {t["id"]: t for t in read("troops.json")["troops"]}
    faction_data = read("factions.json")
    orders = {o["id"]: o for o in read("orders.json")["orders"]}
    cultures = {c["id"]: c for c in faction_data["cultures"]}
    factions = faction_data["factions"]
    parents = defaultdict(list)
    for i, t in troops.items():
        for u in t.get("upgradesTo", []):
            parents[u].append(i)
    edges = sum(len(t.get("upgradesTo", [])) for t in troops.values())
    roots = [i for i in troops if not parents[i]]
    wild = {i for i, t in troops.items() if t["culture"] == "wild"}
    men = {i: t for i, t in troops.items() if t["culture"] != "wild"}
    order_of = {tid: orders[o["id"]]["name"] for o in orders.values() for tid in o["troops"]}
    out = []
    w = out.append

    def name(i):
        return troops[i]["name"]

    def from_cell(i, culture):
        ps = parents[i]
        if not ps:
            return "**root**"
        return ", ".join(f"**{name(p)}**" if troops[p]["culture"] != culture else name(p)
                         for p in sorted(ps, key=name))

    def to_cell(i):
        kids = troops[i].get("upgradesTo", [])
        return ", ".join(name(k) for k in sorted(kids, key=name)) if kids else "—"

    def mark(i):
        """The superscripts a troop's name carries in its culture's table."""
        s = ""
        if troops[i].get("stock") == "noble":
            s += " †"
        if i in order_of:
            s += " ‡"
        return s

    w("# The troop trees")
    w("")
    w("Every soldier in Ironfield, from the levy a village raises to the tier-VII company a realm fields at")
    w("the end of a campaign. Generated from `Data/troops.json`, `Data/factions.json` and `Data/orders.json`")
    w("by `tools/troopmap.py`; change the data and re-run it (`python3 tools/troopmap.py --write`) rather")
    w("than editing this page.")
    w("")
    w(f"**{len(troops)} troops, {edges} upgrade links.** {len(men)} of them are men and {len(wild)} are wild")
    w("things. Tiers run I–VII among the men, plus the tier-0 captain, who belongs to no tree. The wild")
    w("roster has no links at all. The 30 roots are `levies`, `clansmen`, `acolytes`, `esquires`,")
    w("`mounted_esquires`, the captain, and one for each wild troop.")
    w("")
    waiting = [c["id"] for c in cultures.values() if c.get("waiting")]
    if waiting:
        held = sum(1 for t in troops.values() if t.get("culture") in waiting)
        w(f"**Not in the world yet:** {held} troops in {len(waiting)} cultures marked `waiting` in `Data/factions.json`")
        w("(" + ", ".join(f"`{c}`" for c in waiting) + "). They come from the world book")
        w("(`Data/world.json`) and are kept for when that world is brought into the game; until then no realm,")
        w("settlement or band fields them, and their trees stop short of tier VII.")
        w("")
    w("Marks used below: **†** noble stock, raised at a castle rather than a village; **‡** a knight")
    w("order's signature troop; **bold** in *From* means the parent is outside the culture.")
    w("")
    w("## How the trees are shaped")
    w("")
    w("- **Tier is the rank.** Every upgrade raises the tier and must be a better soldier — `Troops.WorseUpgrade`")
    w("  allows no loss of hp, damage, armour or block (nor range and pierce, for a bow) when a troop keeps its")
    w("  class, weapon and shield, and asks for a gain in two of them. Tier I is the levy, VII the sovereign elite.")
    w("- **Two recruit roots.** `levies` (tier I, common) feed the five levy realms; `clansmen` (tier I, northern)")
    w("  feed the northern and steppe lines. Every troop but the two noble roots, the mages and the captain")
    w("  can be reached by upgrading one of those two.")
    w("- **Mages are hired, not drilled.** Every caster rises from `acolytes` (tier II), and no recruit upgrade")
    w("  leads into a caster line: a circle is hired, never grown from the ranks.")
    w("- **Wild things do not upgrade.** The monsters and beasts are a flat roster; their tier is only what an")
    w("  army of them is worth on the field.")
    w("")
    w("The trunk every realm shares:")
    w("")
    w("```")
    w("levies I (common) ─┬─ shieldmen II ───────── heavy foot of azure, crimson, order, league, southern")
    w("                   ├─ spearrunners II ───── every spear line")
    w("                   ├─ bowmen II ─────────── every bow and crossbow line")
    w("                   ├─ outriders II ──────── every horse line")
    w("                   └─ ten culture entry troops at tier II (crossbows, squires, slingers, hobilars...)")
    w("clansmen I (northern) ─┬─ axemen II ──────── northern and steppe foot")
    w("                       ├─ raiders II ────── northern hearthguard")
    w("                       ├─ hunters II ────── northern bows")
    w("                       ├─ fell riders II ── northern horse")
    w("                       ├─ herd boys II ──── steppe bows")
    w("                       ├─ herd riders II ── steppe horse")
    w("                       └─ spearrunners II, bowmen II, outriders II (shared with levies)")
    w("```")
    w("")
    w("## How a company walks it")
    w("")
    w("The data graph is a DAG; the campaign puts gates on its edges (`Companies.CanEnterLine`):")
    w("")
    w("- **Common troops are open to all.** A stack of a culture's men may only train into its own culture's")
    w("  troops or its kin's (Sunward may borrow crimson and northern; the League azure; the Ashen Sun azure")
    w("  and crimson; the Steppe northern).")
    w("- **Tier V and up need a barracks** of that culture standing in your realm — or of the men's own culture,")
    w("  when they are training along a borrowed kin line.")
    w("- **Noble birth, or a knighthood.** A company of commoners cannot be promoted into a † troop. Esquires")
    w("  are bought at castles (renown 60, 1.6× their listed price) or a named man is knighted.")
    w("- **A knight order trains only its own.** Its three signature troops (‡) open when you join or found it.")
    w("")
    w("## Cultures at a glance")
    w("")
    w("| Culture | Realm | Troops | Tier VII | Line | Kin |")
    w("|---|---|---|---|---|---|")
    for cid in CULTURE_ORDER:
        c = cultures[cid]
        own = [i for i, t in troops.items() if t["culture"] == cid]
        realm = next(f["name"] for f in factions if f["culture"] == cid)
        kin = ", ".join(cultures[k]["name"] for k in c.get("kin", [])) or "—"
        w(f"| {c['name']} | {realm} | {len(own)} | {len([i for i in own if troops[i]['tier'] == 7])} | {c['text']} | {kin} |")
    w(f"| Wild | — | {len(wild)} | 0 | beasts and monsters, no upgrades | — |")
    w("")
    counts = defaultdict(int)
    for t in men.values():
        counts[t["tier"]] += 1
    w("Men per tier: " + ", ".join(
        (f"T0 (the captain) {counts[0]}" if k == 0 else f"{numeral(k)} {counts[k]}") for k in sorted(counts)) + ".")
    w("")
    w("## The shared trunk")
    w("")
    w("| Troop | Tier | Class | Cost | Upgrades into |")
    w("|---|---|---|---|---|")
    for i in ("levies", "clansmen", "shieldmen", "spearrunners", "bowmen", "outriders", "acolytes"):
        t = troops[i]
        kids = ", ".join(f"{name(k)} ({troops[k]['culture']})" for k in sorted(t.get("upgradesTo", []), key=name))
        w(f"| **{t['name']}** | {numeral(t['tier'])} | {t['class']} | {t.get('cost', '—')} | {kids or '—'} |")
    w("")

    rank = {c: n for n, c in enumerate(CLASSES)}

    def branch_table(cid, title):
        """One culture's troops by tier: what feeds each and what it becomes."""
        own = [i for i, t in troops.items() if t["culture"] == cid]
        w(title)
        w("")
        w("| Tier | Troop | Class | Cost | From | To |")
        w("|---|---|---|---|---|---|")
        for i in sorted(own, key=lambda k: (troops[k]["tier"], rank[troops[k]["class"]], troops[k].get("cost", 0), name(k))):
            t = troops[i]
            w(f"| {numeral(t['tier'])} | {t['name']}{mark(i)} | {t['class']} | {t.get('cost', '—')} | "
              f"{from_cell(i, cid)} | {to_cell(i)} |")
        stops = [i for i in own if not troops[i].get("upgradesTo") and 0 < troops[i]["tier"] < 7]
        if stops:
            w("")
            w("**Stops short of tier VII:** " +
              ", ".join(f"{name(i)} ({numeral(troops[i]['tier'])})" for i in sorted(stops, key=lambda k: (troops[k]["tier"], name(k))))
              + ".")
        w("")

    for cid in CULTURE_ORDER:
        c = cultures[cid]
        realm = next(f for f in factions if f["culture"] == cid)
        w(f"## {c['name']} — {realm['name']}")
        w("")
        w(f"*{c['text'].capitalize()}.* Root: `{c['root']}`. " +
          (f"Kin: {', '.join(cultures[k]['name'] for k in c['kin'])}." if c.get("kin") else "No kin."))
        w("")
        w("**The realm's line** — what its lords promote along (`factions.json`). A name repeated across two")
        w("tiers means the line has no distinct troop at the lower one yet, so its companies start a step up:")
        w("")
        w("| Role | " + " | ".join(numeral(i) for i in range(1, 8)) + " |")
        w("|---|" + "---|" * 7)
        for role in ("infantry", "archers", "spears", "horse"):
            w(f"| {role.capitalize()} | " + " | ".join(troops[tid]["name"] for tid in realm["lines"][role]) + " |")
        w("")
        branch_table(cid, "**Every troop of the culture**, by tier:")

    w("## Common — the trunk, the noble roots and your own order")
    w("")
    w("The troops of no realm: what every culture shares (`levies`, `shieldmen`, `bowmen`, `outriders` and")
    w("their kin), the noble stock that hangs off castles (`esquires`, `mounted_esquires`), the player's own")
    w("knight order (`sworn_brothers`, `sworn_masters`), the few odd branches that lead nowhere else")
    w("(`billmen`, `beast_hunters`, the Hollow Crown's line), and the captain himself at tier 0.")
    w("")
    branch_table("common", "**Every common troop**, by tier:")

    w("## Bridges between cultures")
    w("")
    w("Most upgrades stay inside one culture, but a few are the roads between them — a Crimson greatsword")
    w("becomes an Azure Royal Crusader, a northern glaive a Crimson Royal Halberdier. These are the links that")
    w("make a conquered realm's veterans useful, provided the men may serve that culture (they must be kin):")
    w("")
    for i in sorted(troops, key=name):
        t = troops[i]
        for u in t.get("upgradesTo", []):
            a, b = t["culture"], troops[u]["culture"]
            if a != b and "common" not in (a, b):
                w(f"- {t['name']} ({cultures.get(a, {}).get('name', a)}) → {name(u)} ({cultures.get(b, {}).get('name', b)})")
    w("")
    w("The hubs, where many lines meet:")
    w("")
    for i in ("sworn_brothers", "esquires", "mounted_esquires", "lancers", "halberdiers", "longbowmen",
              "valkyrie_lancers", "sword_brethren"):
        if i in troops:
            src = ", ".join(sorted(name(p) for p in parents[i])) or "root"
            w(f"- **{name(i)}** ({numeral(troops[i]['tier'])}, {troops[i]['class']}) ← {src} → {to_cell(i)}")
    w("")
    w("`sworn_brothers` is the exception that proves the rule: eleven different lines feed it, because it is")
    w("the player's own knight order — every realm's veteran can become one once you have founded it.")
    w("")

    w("## Knight orders")
    w("")
    w("Eight orders in `Data/orders.json`, each with three signature troops (‡ in the tables above), plus the")
    w("order you found yourself. A troop's `order` field keeps outsiders from training into it; the Hollow")
    w("Crown is a hidden order you can uncover, and `own` is the order you found.")
    w("")
    w("| Order | Patron | Signature troops |")
    w("|---|---|---|")
    for o in sorted(orders.values(), key=lambda o: o.get("patron") or "~"):
        w(f"| {o['name']} | {o.get('patron') or '— (hidden)'} | " + ", ".join(name(t) for t in o["troops"]) + " |")
    w("| Your own order | you | Sworn Brothers, Masters of the Order |")
    w("")

    w("## Mages")
    w("")
    w("One caster line per culture, hired as `acolytes` (II) and rising at IV, V and VI. A circle costs 750,")
    w("1000 and 1350 gold a company, far above any soldier's price, and it is the one line no recruit can enter.")
    w("")
    w("| Culture | IV | V | VI |")
    w("|---|---|---|---|")
    for cid in CULTURE_ORDER:
        row = []
        for tier in (4, 5, 6):
            hit = [i for i, t in troops.items() if t["culture"] == cid and t["class"] == "Caster" and t["tier"] == tier]
            row.append("—" if not hit else f"{name(hit[0])} ({troops[hit[0]]['cost']})")
        w(f"| {cultures[cid]['name']} | " + " | ".join(row) + " |")
    w("")

    w("## The wild")
    w("")
    w("Beasts and monsters, fielded by the campaign's wild parties and by the Ashen Sun's hunts. No upgrades,")
    w("no culture but their breed; tier says what a man of them is worth next to a soldier. A band is led by")
    w("a stronger beast of its own breed (`Campaign.MonsterLeader`): a goblin holdout by its chieftain and a")
    w("hobgoblin warband by a hobgoblin, a wolf pack by an alpha, a nest by its brood mother. Beyond that,")
    w("`goblin_warlord` is placed in every goblin holdout, `necromancer` captains a host of the risen dead and")
    w("raises the fallen, and `death_knight` joins such a host when the Omen runs high.")
    w("")
    w("| Tier | Troop | Class | Breed | Cost |")
    w("|---|---|---|---|---|")
    for i in sorted(wild, key=lambda k: (troops[k]["tier"], name(k))):
        t = troops[i]
        w(f"| {numeral(t['tier'])} | {t['name']} | {t['class']} | {t.get('breed', '—')} | {t.get('cost', '—')} |")
    w("")

    w("## Where the tree stops short")
    w("")
    short = [i for i in men if not troops[i].get("upgradesTo") and 0 < troops[i]["tier"] < 7]
    long = [i for i in men if troops[i]["tier"] == 7]
    w(f"- {len(long)} troops reach tier VII. {len(short)} lines end below it: a branch the campaign can climb")
    w("  but never crown. A realm fields only its own four lines, so a side branch ending at VI is a dead end")
    w("  unless a bridge carries it into a neighbour's tier VII.")
    w("- The tree stops being a tree where lines converge: " +
      ", ".join(f"{name(i)} ({len(parents[i])} ways in)" for i in sorted(men, key=lambda k: -len(parents[k]))[:6]) + ".")
    w("- Branches that end early, culture by culture:")
    w("  " + ", ".join(f"{name(i)} ({numeral(troops[i]['tier'])}, {troops[i]['culture']})" for i in
                      sorted(short, key=lambda k: (troops[k]["tier"], troops[k]["culture"], name(k)))))
    w("- `captain` (tier 0) is the player's own man and belongs to no tree, and `beast_hunters` (III) is a")
    w("  single tier the campaign fields as it stands, with nothing above or below it.")
    w("")
    text = "\n".join(out) + "\n"
    if "--write" in sys.argv:
        path = os.path.join(ROOT, "docs/TROOPS.md")
        with open(path, "w") as f:
            f.write(text)
        print(f"wrote {path}: {len(text.splitlines())} lines")
    else:
        sys.stdout.write(text)


if __name__ == "__main__":
    main()
