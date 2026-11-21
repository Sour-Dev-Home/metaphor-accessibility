# A nearby unit's kind is its list; people, enemies and places each have a name route to a message file

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** [`field-units-names-exits-static.md`](field-units-names-exits-static.md),
  [`field-nearby-session6-2026-11-17.md`](../raw-sources/field-nearby-session6-2026-11-17.md),
  [`field-actors-2026-11-16.md`](../raw-sources/field-actors-2026-11-16.md),
  [`message-serials-from-file-positions.md`](message-serials-from-file-positions.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)

For #178 and #181. The unit manager `M = [[base+0x2ee7510]+0x20]` holds every unit of the map in 21 lists, one class
per list (session 6), so a unit's kind is the list it came from and its position is `+0x80`. This note adds the name
routes the [units note](field-units-names-exits-static.md) lacked for enemies and places, and the message-file numbers
that tie a name to text the mod already keeps. Everything at run time is `[NEEDS VERIFICATION]` unless it says
session 6.

| Kind (list `begin`/`end` in `M`) | Position | Name route | Confidence |
|---|---|---|---|
| Enemy `fldEnemy` (`+0x18`/`+0x20`) | `+0x80` | enemy id = `u16` at `[base+0x2dff9b8] + u16[enemy+0x3634] × 0x1c + 0x8`; text in `EnemyName.msg` | Medium for the id, low for id → label |
| Person `fldNpc` (`+0x98`/`+0xa0`) | `+0x80` | `npc+0x3630` kind → NpcID index → CharaID index → `CharaName.msg` key, which `CharacterEntry` already reads | Medium ([units note](field-units-names-exits-static.md)) |
| Door `fldGmcDoor` (`+0x158`/`+0x160`) | `[door+0x58]` (else `[door+0x30]`) node `+0x50`, or `+0x80` | none per door; the prompt's words are `AccessPointName.msg` labels | Low |
| Chest `fldGmcTreasure` (`+0x118`/`+0x120`) | `+0x80` | none read | High for position (session 6) |
| Ladder `fldGmcLadder` (`+0x258`/`+0x260`) | `+0x80` | none read | Medium |
| Script trigger `fldScriptHit` (`+0x3d8`/`+0x3e0`) | `+0x80` `[NEEDS VERIFICATION]` | link row `+0x2894` | Low-medium |
| Where am I | map id `u32` at `[base+0x2ee7368]+0x48` | `MapName.msg`, looked up by label when the banner or map strip shows | Medium |

Every list's walker matches units by the `int` at `+0x9f8` (session 6: a per-list index, not a name).

## Evidence

### Enemies

`FldEnemyGetEnemyID` (`0x8dbf20`) walks `M+0x18`/`+0x20` for the unit whose `+0x9f8` equals its argument and jumps to
`0x65f8c0`. That reads the `u16` at `enemy+0x3634`, passes it to `0x3dd5a0` (`index × 0x1c + [0x2dff9b8]`: a table of
`0x1c`-byte rows behind the global `0x2dff9b8`) and returns the row's `u16` at `+0x8`. So the enemy id is one row read
away from the unit, with no call. That the id is the label index of `EnemyName.msg` (message-file table entry 2, its
serial test a `.text1` stub) is `[NEEDS VERIFICATION]`; the by-ear check below compares it with an enemy-name lookup.
`FldEnemyGetTier` (`0x8dbfa0`) and `FldEnemyIsDark` (`0x8dc200`) are the same walker shape, not read.

### Message files the names live in

The message-file table (`0x27b0cd0`, `0xd0`-byte entries, name pointer `+0x0`, serial test `+0x18`) gives, by entry
index:

| Entry | File | Serial test |
|---|---|---|
| 0 | `message/CharaName.msg` | `cmp ecx,0x79; setbe`: serials 0–121 |
| 1 | `message/NpcName.msg` | none (null) |
| 2 | `message/EnemyName.msg` | `jmp` into `.text1` |
| 3 | `message/MapName.msg` | `jmp` into `.text1` |
| 34 | `message/AccessPointName.msg` | `lea eax,[rcx-0x2a79]; cmp eax,0x197`: serials 10873–11280, 408 messages |
| 35 | `message/EnemyCategoryName.msg` | serials 11281–11295 |

The mod's Debug log of 2026-11-21 (local, not committed) shows two things that fit this table. Its `compile` records
give each of these files the entry index plus one as its compile id (`CharaName` 1, `NpcName` 2, `EnemyName` 3, `MapName`
4, `AccessPointName` 35, `EnemyCategoryName` 36), and `AccessPointName.msg` compiles with 408 labels. Its 18 `lookup`
records (`GetMessageByLabelIndex`) all have `id=3`, and each sits beside a `GetMessageBySerial` resolve of serial
`544 + label` and a map-strip record: `id` is the entry index, so those were `MapName.msg` lookups. So the mod can turn
a `lookup id=<entry> label=<n>` into text through the source it kept under compile id `<entry> + 1`, position `<n>`
(PR #633's `MessageFilesById`) `[NEEDS VERIFICATION]` beyond these files.

### Doors and exits

The [units note](field-units-names-exits-static.md) has the door's node, its link row (`+0x28c4`) and the
`scriptHit`'s link row (`+0x2894`); nothing here changes them. The prompt's words come from `AccessPointName.msg`,
whose labels group by place and action (`AccessPointName_DNG_GLOBAL_DOOR_ACTION`, `_DOOR_CHECK`, `_DOOR_UNLOCK`, in the
compile record); how a door picks its label is in the [prompt note](field-interaction-prompt-static.md).

### Where am I

The map id (`fldDatabase+0x48`, session 6) stays the reliable read. `MapName.msg` lookups happen when the arrival banner
or the map strip shows, so a `GetMessageByLabelIndex` hook that already exists hears the area's name without any
`.text1` code. The three lookups beside map-strip records paired label 137 with strip id 138, label 1 with strip id 1
and label 131 with strip id 137, so the label isn't the map id `[NEEDS VERIFICATION]`.

## What would settle it

- **Enemy names.** Before a fight, read the nearest enemy's id with the inspector (`follow base+0x2ee7510 0x20 0x18 0x0
  0x3634`, a `u16`; then `read` the row at `[base+0x2dff9b8] + index × 0x1c`, its `u16` at `+0x8`). After the fight
  starts, look for a `lookup id=2` record (`EnemyName.msg`) and compare its label with the id.
- **People's names.** Beside a named person and a passer-by, the [units note](field-units-names-exits-static.md)'s
  `+0x3630` reads.
- **Where am I.** On each area change, the map id (`follow base+0x2ee7368 0x48`, a `u32`) beside the `lookup id=3`
  record's label and the banner the screen shows.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project of the build above, Windows 11 x64,
2026-11-21. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says otherwise.

1. `disasm 0x8dbf20`, `decompile 0x65f8c0`, `decompile 0x3dd5a0`.
2. The message-file table: a read-only Python 3.12 walk of the exe (entries at `0x27b0cd0`, the name pointer and the
   serial test's first bytes decoded).
3. The local `message-log.txt` of the Debug mod (2026-11-21, 01:55 run): `grep` for `compile` records naming these
   files, for `lookup` records, and the lines either side of each lookup. Only ids, labels and counts are quoted.
4. Signature: `FldEnemyIdOf` (`0x65f8c0`) `48 83 EC 28 0F B7 89 34 36 00 00 E8 ?? ?? ?? ?? 0F B7 40 08 48 83 C4 28 C3`
   matches once with `tools/Metaphor.SignatureCheck --probes`; it is a read candidate only, since reading the two
   `u16`s needs no call.

Nothing in `.text1` was decompiled. No decompiled output is quoted; names and file paths are the exe's own.
