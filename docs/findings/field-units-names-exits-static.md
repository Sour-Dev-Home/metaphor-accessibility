# Field units hang off fldWorld; names go through NpcID, exits through link rows

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-actors-2026-11-16.md`](../raw-sources/field-actors-2026-11-16.md),
  [`field-nearby-session6-2026-11-17.md`](../raw-sources/field-nearby-session6-2026-11-17.md),
  [`field-inventory-2026-11-17.md`](../raw-sources/field-inventory-2026-11-17.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - FldHitCollisGetLinkPosID | 0x8df940 | .ecode | Field | known only | Script function that walks the hit list by id to find a link position.
  - FldDoorGetUidByClosestOnPlayer | 0x8dad90 | .ecode | Field | known only | Script function that finds the door closest to the player.
  - FldNpcGetCharaID body | 0x67a380 | .ecode | Field | known only | Looks the NpcID index up in the NpcID table and returns the entry's character id.
  - FldDoorGetLinkFlag_ED | 0x8dabb0 | .ecode | Field | known only | Script function that returns a door's link row flag.
  - FldDoorGetType | 0x698c10 | .ecode | Field | known only | Returns the door's type.

Static prep (#495) for #178 and #176 in game session 4 (#499). The session-6 reader (`FieldWatch`, branch
`capture-kit-2`) isn't on `main`, so this restates its paths as inspector commands and adds what it lacked. Every
runtime value is `[NEEDS VERIFICATION]` until the session's reads agree.

| Question | Best lead | Confidence |
|---|---|---|
| `fldUnitManager` | `M = [[base+0x2ee7510]+0x20]`; list `i` (0 to 20) at `M+0x08+i×0x40`, `begin`/`end` at list `+0x10`/`+0x18` | High (session 6 read it) |
| A unit's position | unit `+0x80` (x, y, z floats) for characters | High (session 6) |
| A door's position | `[door+0x58]` (else `[door+0x30]`), floats at `+0x50`; session 6 dereferenced `door+0x10` by mistake | High in code |
| An NPC's name | `npc+0x3630` says where its NpcID index is; NpcID entry `+0x4` is a CharaID index; CharaID is the speaker-name table | Medium |
| Which doors or `scriptHit`s are exits | a door's link row index at `+0x28c4`, a `scriptHit`'s at `+0x2894`; `-1` means no link | Low-medium |
| Shops | no list; a shopkeeper is an `fldNpc` | Low |

## Evidence

### 1. The manager and its lists

Unchanged from [`field-actors-2026-11-16.md`](../raw-sources/field-actors-2026-11-16.md) and confirmed by session 6
([`field-nearby-session6-2026-11-17.md`](../raw-sources/field-nearby-session6-2026-11-17.md)): `fldWorld` is the global
`0x2ee7510`, the manager is `[world+0x20]`, the player is `[M]`, and the lists follow in Resonant's order. In inspector
terms, `follow base+0x2ee7510 0x20 0x0` gives `M`; reading `M` for `0x558` bytes gives all 21 `begin`/`end` pairs, and
`(end − begin) / 8` is each list's count. `follow base+0x2ee7510 0x20 <begin offset> 0x0 0x0` gives a list's first unit,
for example `0x98` for `fldNpc`, `0x158` for `fldGmcDoor`, `0x118` for `fldGmcTreasure`, `0x18` for `fldEnemy`,
`0x3d8` for `fldScriptHit`. `FldHitCollisGetLinkPosID` (`0x8df940`) walks `M+0x3d8`/`+0x3e0` by `+0x9f8`, confirming
that list's offsets.

### 2. The door position bug

`FldDoorGetUidByClosestOnPlayer` (`0x8dad90`) loads each door into `rbx` and passes `lea rcx,[rbx+0x10]` to `0x96f820`
(at `0x8dae28`): the address of a component embedded in the door, not a pointer stored there. `0x96f820` then takes
the qword at its `+0x48` (else `+0x20`) and reads floats at that node's `+0x50` and `+0x58`. So the node is
`[door+0x58]`, or `[door+0x30]` when that is null. `FieldWatch.DoorNode` on `capture-kit-2` read `[door+0x10]` first
and then `+0x48` of that, one dereference too many, which explains session 6's constant huge door distances. Session 6
found the door's `+0x80` sane, so either source may do; the node is what the game itself uses for "closest door".

### 3. An NPC's name

`FldNpcGetCharaID`'s body `0x67a380` gets the NpcID index from `0x67a650`, looks it up in the table `0x8990c0` returns
(`[0x2eebc38]+0x40`, the `NpcID.bin` table of the [session-1 note](title-names-voice-tutorials-static.md); entries of
`0x20` bytes from `+0x10`, up to `0x99d`), and returns the entry's `u16` at `+0x4`, or `0xffff`. `0x67a650` switches on
the `int` at `npc+0x3630`:

| `npc+0x3630` | NpcID index from |
|---|---|
| 0 | row `[npc+0x3638]` of the table at `fldDatabase+0x10950`, its `int` at `+0x1e0` |
| 1 | `0x55d360(npc+0x363c, npc+0x3640)`, its `+0x10` |
| 5 | `0x539640(npc+0x3650, npc+0x3654)`, its `+0xc` |
| 6 | row `[npc+0x3658]` of the table at `fldDatabase+0x10bb8`, its `+0x8` |
| 7 | the `int` at `npc+0x365c` directly |

A `fldDatabase` table row is `[[table+0x8] + index × 8]` (`0x58e2f0`). The CharaID index then names the speaker-name
entry (`[0x2eebc18]+0x40`, `0x6c` bytes from `+0x10`), whose key turns into text through the message system the
session-1 note follows. `fldNpc::vf34` is one of that table's callers, so the field's own name labels use it. Which kinds
occur in a town, and whether a nameless passer-by gives `0xffff`, are the session's questions. Confidence: medium.

### 4. Exits

- Doors: `FldDoorGetLinkFlag_ED` (`0x8dabb0` → `0x699c00`) takes the index at `door+0x28c4` (`-1` none) into the table at
  `fldDatabase+0x109b8` and returns the row's `+0x58`. The door's type (`FldDoorGetType`, `0x698c10`) is the `int` at
  `+0x56e0` of its layout record from `fldBg`, the record found by the index at `door+0x2888` (`0x6982e0`,
  `0x577f10`), when the record's top six bits are `0xe`.
- `scriptHit`s: `FldHitCollisGetLinkPosID` returns the `int` at `+0x88` of the row at index `scriptHit+0x2894` in the
  table at `fldDatabase+0x10a18` (`0x6b1cc0`). A position id that names where you arrive fits an exit or warp trigger.
- RTTI names link data among the field's tables (`fldMapLink`, `fldScriptHitLink`, `fldActionHitLink`,
  `fldTravelPath`), so a link row is likely where a destination map is `[NEEDS VERIFICATION]`. The lead is the
  contrast tonight: an exit door or trigger against a door between two rooms of the same map.

### 5. Shops

No unit list holds shops (session 6), and nothing read here marks a shopkeeper. The
[session-3 prep](follower-availability-and-shop-stock-static.md) has the shop's stock once it is open. Note the kind
(`+0x3630`) and NpcID index of a shopkeeper beside an ordinary NPC tonight. Confidence: low.

## Tonight

Stops 1 and 3 of #499. Run each batch with
`pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-4/<file> -WaitKey -SaveLog <out>`, `<out>` a folder
outside the repository, then type the reads the batch's comments list, with the addresses it printed.

| Stop | Batch | What confirms | What rejects |
|---|---|---|---|
| 1, town | `stop-1-town.txt`, `[town-units]` | `rtti` of the first NPC says `fldNpc`, of the first door `fldGmcDoor`. `read <M> 0x558`: counts like session 6 (91 to 140 people). The door node's floats lie within a few hundred units of the door's `+0x80` | The node's floats are huge or zero for every door (the chain is still wrong) |
| 1, beside a named person, then a passer-by | `[town-units]`, run at each | `read <npc>+0x3630 0x30` gives a kind of 0, 1, 5, 6 or 7; for kind 7, the NpcID entry at `T + index × 0x20` has a `u16` at `+0x4` below 100 for the named person and `0xffff` for the passer-by | Every NPC has the same index and entry |
| 1, at a town exit and at an inner door | `[exits]` | `read <door>+0x2880 0x50`: `+0x28c4` differs between the exit and the inner door (one `-1`, or two rows whose `+0x58` differ) | Both doors identical in `+0x2888` and `+0x28c4` |
| 3, beside a chest, then after opening it | `stop-3-chest-enemies.txt`, `[before-chest]`, `[after-chest]` | `rtti` says `fldGmcTreasure`; one chest's position is within about 100 of the player (session 6's nearest); `changes` lists bytes of that chest's `+0x2880` block; whether the list's `begin`/`end` move too says if an opened chest leaves the list | No `fldGmcTreasure` in the list on a map with a chest |
| 3, an enemy has noticed you | `[enemy-noticed]` | `rtti` says `fldEnemy`; the nearest enemy's position closes on the player's between reads | |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query.

1. `disasm 0x96f820`; `disasm 0x8dad90` (the `lea rcx,[rbx+0x10]` at `0x8dae28`). `FieldWatch.cs` on `capture-kit-2`
   (`git grep` of `DoorNode`) for the reader's chain.
2. `decompile 0x67a380`, `decompile 0x67a650`, `disasm 0x8990c0`, `disasm 0x58e2f0`.
3. `decompile 0x699c00`, `decompile 0x6982e0`, `disasm 0x698c10`, `disasm 0x8844f0`, `disasm 0x577f10`;
   `decompile 0x8df940`, `decompile 0x6b1cc0`.

Nothing in `.text1` was decompiled. No decompiled output is quoted; names are the exe's own.
