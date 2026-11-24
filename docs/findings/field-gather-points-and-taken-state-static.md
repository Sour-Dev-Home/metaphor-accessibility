# Pickups are a list the nearby list doesn't read, and a taken pickup is a bit in a game-data bank; an opened chest's state is not pinned

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  reading the mod's own code
- **Sources:** [`field-actors-2026-11-16.md`](../raw-sources/field-actors-2026-11-16.md),
  [`field-nearby-session6-2026-11-17.md`](../raw-sources/field-nearby-session6-2026-11-17.md),
  [`game-names-static-2026-11-20.md`](../raw-sources/game-names-static-2026-11-20.md)
- **Functions:**
  - FldGatherSetIsAcquired | 0x1408de550 | .ecode | Field | known only | Finds a gather point by id in the list at manager +0x1d8 and sets its taken state.
  - GatherSetTaken | 0x14069b5c0 | .ecode | Field | known only | Sets a gather point's taken bit in bank +0x1f8, or acts on its linked object.
  - FldGmcIsActivated | 0x1408defc0 | .ecode | Field | known only | Finds a gimmick by id in the list at manager +0x218 and tests its activated bit.
  - GimmickIsActivated | 0x14069ffb0 | .ecode | Field | known only | Tests a gimmick's bit in bank +0x348, its row from the database table at +0x10710.
  - FlagBankSet | 0x14070cf20 | .ecode | Field | known only | Sets or clears bit n of the byte array at bank +0xa0.
  - FlagBankTest | 0x14070d3f0 | .ecode | Field | known only | Tests bit n of the byte array at bank +0xa0.
  - FlagBanks | 0x1403b0b20 | .ecode | Field | known only | Returns the pointer at game data +0xac368.
  - DatabaseRow | 0x14058e2f0 | .ecode | Field | known only | Returns row n of a database table (+0x8 array), or its +0x58 default for -1.

For #949 and #950. The nearby list already reads the game afresh on every press (`FieldControls.ReadNamed` calls
`IFieldReads.Read`, which walks the game's lists each time), so nothing is cached on the mod's side. Two gaps remain,
both in what the game keeps: an opened chest stays in the game's treasure list, and pickups (`fldGmcGather`) are a
list the mod doesn't read. A pickup's taken state is a bit the code below pins statically; a chest's opened state is
not found. Nothing here is checked in memory: every offset is `[NEEDS VERIFICATION]` until a Debug session reads it.

## Evidence

- **Opened chests stay listed.** Session 6 found every list's count set on load and stable after, except enemies
  (`field-nearby-session6-2026-11-17.md`), and it walked past a chest it opened. So an opened chest is still a
  `fldGmcTreasure` in the list at manager `+0x118`, and the mod lists it as "Chest" until a read of its opened state
  filters it. Which field or bit that is: `[NEEDS VERIFICATION]`, not found in this pass.
- **Pickups are the list at manager `+0x1d8`/`+0x1e0`.** The raw source names it from `FldGatherGetUidByLayoutName`
  (`0x8de460`); `FldGatherSetIsAcquired` (`0x8de550`) walks the same list and matches the unit's id at `+0x9f8`.
  `FieldReads.Lists` reads only the enemy, person, chest and door lists.
- **A taken pickup is a bit.** `0x8de550` passes the gather point and the flag to `0x69b5c0`. When the point's
  `int +0x28c4` is -1, that takes row `int +0x28c0` of the field database's table at `fldDatabase + 0x107d0`
  (`0x58e2f0`: the row pointer is `[[table+0x8] + n×8]`), and sets bit `u32 row+0xc` in the bank at
  `[[game data + 0xac368] + 0x1f8]` (`0x3b0b20` returns `[game data + 0xac368]`; `0x70cf20` sets bit n of the byte
  array at `[bank + 0xa0]`). Game data is the global the story flags already read (`0x272e310`). When `+0x28c4` is
  not -1, the point is linked to the `fldGmcObject` (list `+0x198`) whose `+0x2888` matches, and the call acts on that
  object instead (`0x69e5e0`), not on a bit.
- **Activated gimmicks use the same scheme.** `FldGmcIsActivated` (`0x8defc0`) walks the list at `+0x218` and calls
  `0x69ffb0`, which tests the bit `u32 row+0xc` of row `+0x28c0` of the table at `fldDatabase + 0x10710`, in the bank at
  `+0x348` (`0x70d3f0`). Which class that list holds is not checked here.

## What a fix needs

1. A Debug read of the gather list's units (position at `+0x80`, `+0x28c0`, `+0x28c4`) and the taken bit by the chain
   above, logged before and after taking a pickup on a chapter 1 map. If the bit flips, the list can add pickups as
   "Item" and leave out taken ones (#949, #950).
2. A chest's opened state: a Debug byte diff of the chest's `+0x2880` block before and after opening it, as the
   [units note](field-units-names-exits-static.md)'s stop 3 plans.

## Steps

Ghidra 12.2 with JDK 25.0.5.8 (`docs/contributors/how-to/query-the-ghidra-project.md`), the shared project built from
the exe above, on 2026-11-24: one `pwsh scripts/dev/ghidra.ps1 batch` run with `decompile` of `0x8de550`, `0x8defc0`,
`0x8df050`, `0x8de460`, `0x69b5c0`, `0x69ffb0`, `0x70cf20`, `0x70d3f0`, `0x3b0b20` and `0x58e2f0`. The script names
come from the Debug message log's `register name=... rva=...` lines of the 2026-11-24 session. The mod's read path is
`src/Metaphor.Hooks/FieldReads.cs` (`Read`, `Lists`) and `src/Metaphor.Features.Field/FieldControls.cs` (`ReadNamed`).
