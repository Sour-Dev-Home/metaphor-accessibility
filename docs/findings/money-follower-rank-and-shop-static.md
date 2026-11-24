# Money, follower ranks and bag counts sit in the static game-data block; the shop's item is on its scene

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`community-survey-calendar-quests-shops-followers-2026-11-19.md`](../raw-sources/community-survey-calendar-quests-shops-followers-2026-11-19.md),
  [`menu-values-session5-2026-11-17.md`](../raw-sources/menu-values-session5-2026-11-17.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`menus-open-session6-2026-11-17.md`](../raw-sources/menus-open-session6-2026-11-17.md)
- **Functions:**
  - GetMoney | 0x90a510 | .ecode | Menus | known only | Script function that returns the money `int` at `G+0x22c58`.
  - FollowerSetRank | 0x9074c0 | .ecode | Menus | known only | Script function that sets a follower's rank in the game-data block.
  - FollowerGetFriendship | 0x907460 | .ecode | Menus | known only | Script function that reads a follower's friendship points.
  - GetBagItem | 0x907810 | .ecode | Menus | known only | Script function that reads a bag count by item id.
  - FclShopGetSelectedMenu | 0x906810 | .ecode | Menus | known only | Script function that reads the shop menu selection from a counter.

Static prep for #196, read in Ghidra only and not yet run against the game. The community has no live addresses (the
survey). The game's own script getters lead straight to them: most live in one block, `G`, and the shop's focused item
is on the shop scene.

## Evidence

`G` is the pointer at RVA `0x272e310`. Its value in the file is `base+0x2ca66f0`, and nothing in analysed code writes
it (`global 0x272e310`: 150 instructions, no write). The [battle note](battle-turn-icons-and-ui-components-static.md)
uses the same block.

| Data item | Where | Evidence | Confidence |
|---|---|---|---|
| Money | `G+0x22c58`, `int` (`base+0x2cc9348`) | `GetMoney` (`0x90a510`) jumps to `0x3b0c30`, which returns it. Session 5 showed `GetMoney`'s result matching the shop's money box | High |
| Follower rank | `G+0x24674 + id × 4`, `int`, 23 followers (`id < 0x17`) | `FollowerSetRank` (`0x9074c0`) calls `0x3b0610`, which reads through `0x392d40` and writes through `0x3931b0`, clamped to 0 to 8. `FollowerGetRank` itself jumps into `.text1`, not followed | High for the array; that it's the shown rank is a lead |
| Follower friendship points | `G+0x246d0 + id × 4` (`G+0x24674+0x5c`), `int`, clamped to 200 | `FollowerGetFriendship` (`0x907460`) reaches `0x3b05b0` and `0x392bc0`; `FollowerSetFriendship` (`0x9074a0`) reaches `0x393160` | High for the array; meaning is a lead |
| Bag counts, category 0 | `G+0x1c758 + id`, one byte per item | `GetBagItem` (`0x907810`) reaches `0x3bcb10`, which dispatches on `id >> 12`; category 0 (`0x3bbd60`) reads this | High for category 0. Categories 1 to 6 jump into `.text1` `[NEEDS VERIFICATION]` |
| Shop menu selection | `G+0x237b4` (`G+0x233f4 + 0xf0 × 4`), `int` | `FclShopGetSelectedMenu` (`0x906810`) calls `0x3b07e0(0xf0)` | Medium: a script flag slot, meaning a lead |
| Shop's focused item ID | shop scene `+0xe4`, `int`; `0xffff` on an empty row | `ItemShopChoice`'s second-base `vf18` (`0xe77950`) writes the focused row's ID there; `FclShopGetItemID` (`0x906720`) reads it back | High |
| Buy or sell | `ItemShopChoice` `+0x64b8` (seen from its second base, so the full-object offset may differ by 8) | `vf18` treats 1 as selling: it adds the item's value (`0xf31740`, through `0x3bd290`, at least 1) to money and refuses past 9,999,999 | Medium |
| Row status | `ItemShopChoice` `+0x30578` | `vf18` sets 0, then 2 on an empty row, 3, 4 or 5 (the money cap) | Medium |
| Stock list | `ItemShopChoice`, an array of up to 1536 `BULK_DATA` (its type information) | Offset `[NEEDS VERIFICATION]` | Lead |
| Availability, location | `FldFollowerIsAbsence` (`0x8de190`) returns byte `+0x8` of a record from `0x5d44700`; `FldFollowerGetJumpToZoneID` (`0x8de100`) | Both go through `.text1` functions, not followed | `[NEEDS VERIFICATION]` |

The shop scene comes out of the same scene map as the Archetype Tree (see the
[tree note](archetype-tree-scene-and-choice-static.md)), with key `0xf`: the menus' handle at `0x2ee7338`, `+0x48`,
the first element's `+0x8`, the bucket and node walk, and a cast to `fclSpotItemShopMenu@fcl@app`. Session 6 checked
the handle on field menus only. The shop functions reading it suggest it's non-null in a shop too
`[NEEDS VERIFICATION]`.

## Tonight

Deploy a Debug build with `opt-in-inspector.txt` ([how-to](../contributors/how-to/inspect-the-running-game.md)).

1. **Field, any time:** `pwsh scripts/dev/session.ps1 inspect 'follow base+0x272e310 0x22c58' 'follow base+0x272e310 0x24674'
   'follow base+0x272e310 0x1c758'`. The first should print `base+0x2cc9348`. Then `read <money> 4` against the money
   on the main menu, `read <rank> 92` (23 ranks) against the Followers screen, and `watch` all three.
2. **Item shop, top menu:** `read base+0x2cc9ea4 4` while moving between buy and sell.
3. **Item shop, buy list:** use the tree note's steps 1 and 2 with key `0xf`. Expect `rtti` to give
   `fclSpotItemShopMenu@fcl@app`. Then `watch <scene>+0xe4 4`, move down two rows, and `changes`: one item ID per move.
4. **Buy one item:** `changes`. Money goes down by the price, and its bag byte goes up when it's category 0.
   `read <G+0x1c758> 0x1000` before and after finds the byte by diff.
5. **Sell one item:** the same. Money goes up.
6. **After a follower rank-up, or on the Followers screen:** `changes` on the rank and friendship watches
   (`follow base+0x272e310 0x246d0`, `read` 92).
7. **Shop stock and availability** stay for a listener or a sweep: `read <scene> 0x1000` and `rtti` each pointer to
   look for `ItemShopChoice@ope@ui@app`.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Script function addresses come from the squirrel registration list. Each
line is one `pwsh scripts/dev/ghidra.ps1` query.

1. `decompile 0x90a510`, then `0x3b0c30`. `global 0x272e310`. The 8 bytes at file offset
   `0x272e310 - 0x2729000 + 0x2727000` (`.debug`'s virtual and raw starts) hold `0x142ca66f0`.
2. `decompile 0x907480` (`FollowerGetRank`: a jump to `0xa045ba0` in `.text1`, stopped there), `0x907460`, `0x3b05b0`,
   `0x392bc0`, `0x9074c0`, `0x3b0610`, `0x392d40`, `0x3931b0`, `0x9074a0`, `0x3b05f0`, `0x393160`, `0x3b05d0`.
3. `decompile 0x907810`, `0x3bcb10`, `0x3bbd60`, `0x3b0b90`. `0x3bb820` and `0x3bb690` jump into `.text1`.
4. `decompile 0x906720`, `0x906810`, `0x3b07e0`, `0x51fab0`, `0x4ccde0`. `global 0x2c06d38` lists the casts to
   `fclSpotItemShopMenu`.
5. `vtable app::ui::ope::ItemShopChoice`, `decompile 0xe77950`, `0xdc1c30`, `0xf31740`.
6. `decompile 0x8de190`, `0x8de100`, `0x8de150`, `0x8de170`: each reaches `.text1` and was not followed.
