# Follower availability sits in a field manager's per-follower map; the shop's rows and stock left have fixed offsets

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`community-survey-calendar-quests-shops-followers-2026-11-19.md`](../raw-sources/community-survey-calendar-quests-shops-followers-2026-11-19.md),
  [`metaphor-010-templates-shop-follower-quest-2026-11-19.md`](../raw-sources/metaphor-010-templates-shop-follower-quest-2026-11-19.md)
- **Functions:**
  - FldFollowerIsAbsence | 0x8de190 | .ecode | Field | known only | Script function returning the absence byte of a follower's 40-byte copy. | finding; docs/findings/battle-target-cursor-index-static.md
  - FldFollowerGetRankupStatus | 0x8de150 | .ecode | Field | known only | Script function returning the rank-up status of the same copy. | finding; docs/findings/battle-target-cursor-index-static.md
  - FldFollowerIsLayoutForFollowerScenario | 0x8de1b0 | .ecode | Field | known only | Script function returning a layout byte of the same copy. | finding; docs/findings/battle-target-cursor-index-static.md
  - FldFollowerUpdateLayoutInfo | 0x8de250 | .ecode | Field | known only | Script function that loops over the follower map keyed by follower ID. | finding; docs/findings/battle-target-cursor-index-static.md
  - DateUI_ChangeTime | 0x904aa0 | .ecode | Field | known only | Script function that walks the same map layout for its own map at `+0x188`. | finding; docs/findings/battle-target-cursor-index-static.md

Static prep (#494) for what the [money note](money-follower-rank-and-shop-static.md) (#405) left open in #196: follower
availability and location, and the shop's stock list. Read in Ghidra only, not yet run against the game: every
meaning below is `[NEEDS VERIFICATION]` until session 3 (#498) agrees. `G` is the game-data block at `base+0x2ca66f0`.
Batch files: [`docs/sessions/session-3/`](../sessions/session-3/).

| Data item | Best lead | Confidence |
|---|---|---|
| Follower record (absence, rank-up status) | follower manager `F = [base+0x2ee73f0]`, map at `F+0x60`, one node per follower 1 to 22 | Low-medium |
| Follower location | map at `F+0x20`: a list of layout entries per follower; entry `+0x8` indexes the zone table | Medium for the route, low for which entry is today's |
| Shop rows | `ItemShopChoice +0x2d568` count, `+0x2d570` item IDs (`int` each, up to 1536) | High |
| Buy or sell | `ItemShopChoice +0x64c0`: 0 buy, 1 sell | High |
| Shop ID | `ItemShopChoice +0x64c4` | Medium |
| Stock left | per-shop records at `G+0x257a8`: 99 entries of `u16` item, `u16` left, `u16` | Medium; the stride is hidden |

## Evidence

### 1. Follower availability

`FldFollowerIsAbsence` (`0x8de190`) returns byte `+0x8` of a 40-byte copy that `0x5d44700` (`.text1`, not followed)
makes for a follower; `FldFollowerGetRankupStatus` (`0x8de150`) returns `int +0x14` of the same copy, and
`FldFollowerIsLayoutForFollowerScenario` (`0x8de1b0`) byte `+0x10`. Readable callers of the same copy (`0x5c0e60`,
`0x5c0ea0`, `0x5c0ed0`) use the same three fields, so no readable code shows where it comes from.

The static route is through the function next to it. `FldFollowerUpdateLayoutInfo` (`0x8de250` to `0x539f80`) loops over
followers 1 to 22 and, for each, stores 32 bytes from `.text1`'s `0x5d4e870(id, −1)` into the node of a map at `F+0x60`
keyed by the follower ID, at node `+0x14` to `+0x33`. `F` is the pointer at `0x2ee73f0`, written only by `0x535e60` and
`0x536410` (`global 0x2ee73f0`), one of a bank of field managers that `0x5361c0` checks. The map layout is the one
`DateUI_ChangeTime` (`0x904aa0`) walks for its own map at `+0x188`: sentinel node at `+0x8`, size at `+0x10`, buckets at
`+0x18`, mask at `+0x30`; a node is next, previous, then the key at `+0x10` and the value. So `F+0x68` is the sentinel,
`F+0x70` the follower count (expect 22), and walking `+0x0` from the sentinel visits each follower's node. The menus'
scene map keeps its buckets and mask `0x10` further in ([tree note](archetype-tree-scene-and-choice-static.md)), so
the layout is `[NEEDS VERIFICATION]` for this map: a count other than 22 at `F+0x70` means shift by `0x10`.

That `0x5d44700`'s copy is this stored record (so the absence byte is one of node `+0x14` to `+0x33`) is the lead, not
shown: the session dumps each node and looks for a byte that is 1 exactly for the followers the Followers screen shows
as unavailable. Confidence: low-medium.

### 2. Follower location

`FldFollowerGetJumpToZoneID` (`0x8de100`) gets an index from `.text1` (`0x5d5bb10`), then `0x539640(id, index)`: the
node for `id` in a second map at `F+0x20`, whose value at node `+0x18` points to a `{first, last}` list of layout entry
pointers (`0x538c80` returns its length). The entry's `int +0x8` indexes a zone table at `[[base+0x2eebc48]+0x40]`
(`0x898fa0`), `0x2c` bytes per row, whose `u16 +0x16` is the zone ID; index `0xffff` means row 0. The route is static;
which entry is today's comes from `.text1`. The 010 template's `FollowerLayout` table is the likely source of the
entries `[NEEDS VERIFICATION]`. Confidence: medium for the route, low for today's entry.

For #200, "followers available" is then the count of followers 1 to 22 whose rank (`G+0x24674 + id × 4`) is above 0 and
whose absence byte is 0 `[NEEDS VERIFICATION]`.

### 3. Shop stock list

`ItemShopChoice`'s constructor (`0xe6e750`, found by `global` on its vtable `0x2200938`) lays out fixed-size arrays,
each `fw::StaticArray<T,1536>` with its vtable at `+0x0`, its count at `+0x8` and its elements at `+0x10` (the
constructor builds the first one's elements there):

| Offset | Array | Element |
|---|---|---|
| `+0x4b0` | `CursorManagerBase::CursorItemInfo` | `0x10` |
| `+0x2d560` | `datItemID`: the rows | 4 |
| `+0x2ed70` | `SELECT_STATE`, one per row | 4 |
| `+0x30590` | `ItemShopChoice::BULK_DATA` | 8, set to `0xffff` |
| `+0x335b0` | `datItemContents_tag` | 8 |
| `+0x365c0` | `datItemID` | 4 |
| `+0x37dd0` | `SortItemExpansion<datItemContents_tag>` | `0x18` |

The rows are `+0x2d560`'s: the buy list builder (`0xe77370`) and the sell list builder (`0xe76430`) both loop over its
count at `+0x2d568`, take each row's item ID and its `SELECT_STATE`, and `0xe74c00` reads the focused row's ID from it
by the cursor (`ChoiceGetCursor`). The mode at `+0x64c0` chooses the builder: 0 buy, 1 sell, which fits #405's `+0x64b8`
from the second base. `BULK_DATA` is looked up by item ID (`0xe74430`) and its `+0x4` passed with a sell row, likely the
quantity chosen `[NEEDS VERIFICATION]`. Confidence: high for the rows, medium for the rest.

**Stock left.** For each buy row, `0xe77370` passes the item and `+0x64c4` (starts at `0xffff`; the shop's ID is the
lead) to the price helper `0xf31100` and to `0xf323c0`, and treats the latter's 99 apart from other counts.
`0xf323c0` calls `0x51b0f0`, which fills a `StaticArray<app::dat::Shop::Data,99>` (`0x14` bytes per entry) for the
shop in `.text1`, finds the item, then looks it up in the shop's live record from `0x3b1360(shop)`: `G+0x257a8 + shop ×
stride`, 99 entries of 6 bytes, `u16` item ID then `u16` left. The stride is computed through `.text1` data and stays
hidden; the new-game reset clears `0xba40` bytes there, which is `0x254` × 80, so `0x254` per shop is the lead. When the
static entry names a game flag that is off (`0x3b0ab0`, the flag bit array at `G+0x22c5c`) the count is 0.
Confidence: medium.

**Finding the choice.** The choice is a heap object of `0x40de0` bytes made through a factory (`0xd540e0`, choice type
`0x25` in `0xd5c6b0`) behind a shared pointer; no static route from the shop scene to it was found. The session's
route stays a sweep: `read <scene> 0x1000`, then `rtti` each pointer for `ItemShopChoice@ope@ui@app`.

## Tonight

Stops 3 and 4 of #498, with `pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-3/<file> -WaitKey
-SaveLog <out>`. Addresses printed by the batch are heap addresses; add offsets by hand, since `read` takes
`0x<hex>` or `base+0x<hex>` only.

| Stop | Batch | Follow-ups and what confirms |
|---|---|---|
| 3, shop | `stop-3-shop.txt` | Find the scene (key `0xf`), sweep it for the choice, then `read <choice>+0x2d568 4` (the rows on screen), `read <choice>+0x2d570 <4 × rows>` (item IDs, `ID >> 12` a category), `read <choice>+0x64c0 8` (0 in buy, 1 in sell; then the shop ID). The batch's two `G+0x257a8` dumps, before and after buying a limited item, differ in one `u16` that drops by the amount bought; its offset over `0x254` gives the shop ID |
| 4, followers | `stop-4-followers.txt` | `read <F>+0x70 8` is 22. For each printed node, `read <node> 0x38`: `+0x10` is a follower ID 1 to 22; one byte in `+0x14` to `+0x33` is 1 for exactly the followers shown as unavailable (rejects the lead if none). Location: `read <node2>+0x18 8` in the `F+0x20` map, then the list's entry `+0x8`, then the zone row at `<zone table> + index × 0x2c + 0x16` |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query.

1. `decompile 0x8de190`, `0x8de150`, `0x8de1b0`, `0x8de170` (a jump into `.text1`), `0x8de840`, `0x8de1e0`, `0x5c0e60`,
   `0x5c0ea0`, `0x5c0ed0`; `callers 0x5d44700`, `callers 0x538e40` (its thunk).
2. `decompile 0x8de250`, `0x539f80`, `0x8de100`, `0x539640`, `0x538c80`, `0x538d40`, `0x538dd0`, `0x898fa0`,
   `0x5361c0`, `0x904aa0`; `global 0x2ee73f0`; `callers 0x539640`.
3. `vtable app::ui::ope::ItemShopChoice`, `global 0x2200938`, `decompile 0xe6e750`, `0xd540e0`, `0xd5c6b0`; `callers
   0xe6e750`, `callers 0xd540e0`.
4. `offset 0x30598`, `0x30590`, `0x2d568`, `0x2d570`, `0x335b8`, `0x365c8`; `decompile 0xe76430`, `0xe775a0`,
   `0xe77370`, `0xe74430`, `0xe74c00`, `0xf323c0`, `0x51b0f0`, `0x3b1360`, `0x3b0ab0`.

Nothing in `.text1` was decompiled: `0x5d44700`, `0x5d4e870`, `0x5d5bb10`, `0x5d14630`, `0x5d0fc60` and
`FldFollowerGetUnlockStatus`'s target were noted and not followed. Class and type names are the exe's own; no game text
is quoted.
