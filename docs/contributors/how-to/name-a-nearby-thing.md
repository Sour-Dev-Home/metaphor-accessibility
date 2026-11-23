# Name a nearby thing

Add a row to the names table so the nearby list says a thing's name and kind, such as "Display case" under Examine,
instead of the generic kind its game list gives it ("Person"). No code changes: one JSON file per map (#687).

## 1. Find the map and the object id

1. Deploy a Debug build and start a session ([run a game session](run-a-game-session.md)).
2. Walk up to the thing. With `]` or `[`, pick the category it's listed under now, so it's the nearest there.
3. Press `I`. The mod says "Logged", the object id and the thing, and the message log gets a line like:

   ```text
   identify map=1 id=npc-2-17 list=Person kind=Person at=(-512,24,903.5) npcKind=2 npcIndex=65535 source=17 model=401 chara=?
   ```

   `map` and `id` are the row's key. "Logged, no id" means the mod reads no id for that kind yet (enemies), so it
   can't be named.

## 2. Add the row

Open `data/names/<map>.json`, or create it, named for the map id:

```json
{
  "map": 1,
  "objects": {
    "npc-2-17": { "name": "Display case", "kind": "examine" }
  }
}
```

| Field | Holds |
|---|---|
| `map` | The map id, the same as the file's name |
| `objects` | One row per object id: `npc-<kind>-<index>`, `door-<index>` or `chest-<index>`, as the log gave it |
| `name` | What the list says, in your own words: 1 to 60 characters on one line. Never copy game text |
| `kind` | One of `person`, `door`, `exit`, `chest`, `item`, `enemy`, `examine`; it picks the category |

Nothing else is allowed in the file. The kinds file into categories: `person` under People, `door` and `exit` under
Doors and exits, `chest` and `item` under Chests and items, `enemy` under Enemies, `examine` under Examine.

## 3. Check it

`bash scripts/check.sh full` runs the field tests, which refuse a file outside the schema and say why
(`NamesTableTests.EveryNamesFileInTheRepositoryKeepsTheSchema`). Then deploy and press `N` near the thing: it says
the name. Open a PR with the row.

Whether an id stays the same across loads and saves is `[NEEDS VERIFICATION]`; if a row stops working, log the thing
again and compare.
