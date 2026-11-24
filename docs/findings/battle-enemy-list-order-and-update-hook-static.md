# The enemy list follows the encounter record's slot order, and `Package::Update` is a Release-safe place to read it

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-enemy-list-maps-hits-to-slots-static.md`](battle-enemy-list-maps-hits-to-slots-static.md) and
  [`battle-state-session6-2026-11-17.md`](../raw-sources/battle-state-session6-2026-11-17.md)
- **Functions:**
  - BattlePackageUpdate | 0x21e1c0 | .ecode | Battle | hooked | The battle package's once-a-frame update; its first argument is the package.
  - BattleLoadPhase | 0x239ea0 | .ecode | Battle | known only | `btl::phase::Load::vf1`: creates the units and fills the enemy list.
  - EncounterRecord | 0x3dd5a0 | .ecode | Battle | known only | Returns the encounter table's `0x1c`-byte record for an encounter id.
  - EncounterTable | 0x2dff9b8 | .debug | Battle | known only | Pointer to the encounter table that `EncounterRecord` indexes.

For #750. The package's enemy list (`+0x128`) is filled in the order of the encounter record's six enemy-id slots,
skipping empty ones. No field on the enemy's action or unit records a screen position or formation slot on the paths
read here, so whether that order is left to right on screen is still `[NEEDS VERIFICATION]`. `Package::Update`
(`0x21e1c0`, `.ecode`, a full function body) gets the package every frame and is the hook point for a snapshot.

## Evidence

- **The hook point.** `0x21e1c0` opens with a full prologue (saves registers, reserves `0xe0` bytes of stack), so it is
  a real body in `.ecode` (`0x1000`–`0x17c0000` in the section table), not a jump stub into `.text1`. Its one direct
  caller is `0xc8cbe`, in the battle task's update `0xc8c90`. A Release hook on it, signature `BtlPackageUpdate`, was
  built and passed the security review on the abandoned `247-battle` branch (commit `8be3b86`, #251). That branch was
  closed for merge conflicts, not for the hook (PR #294, 2026-11-21).
- **Who appends enemies.** `BattleAddEnemy` (`0x2100a0`) has four callers: `btl::phase::Load::vf1` (`0x23bbca`),
  `btl::phase::Series::vf1` (`0x24be47`), `0x1c6190` and `0x2b6eb0`. The Load call is the one at battle start; the
  others were not read and may add enemies mid-battle (reinforcements, a next wave) `[NEEDS VERIFICATION]`.
- **The order.** In `Load::vf1` the encounter id is package `+0x25c` (a `u16`), or for a later wave the `u32` at
  `+0x2b8 + 4 × (n − 1)` when the byte `+0x2c4` is a non-zero `n` (a later wave, by how it's used
  `[NEEDS VERIFICATION]`). `EncounterRecord` returns
  `[EncounterTable] + id × 0x1c`. Load walks six `u16` enemy ids from record `+0x8`, and for each non-zero id creates a
  unit (kind 2) and appends it to a local list. A later loop walks that local list from its head, sets each enemy up,
  and calls `BattleAddEnemy`, which appends at the package list's tail (the earlier note). So list index `i` is the
  `i`-th non-zero slot of the encounter record.
- **No position on the records.** The unit-creating function `0x173ba0` stores, among others, a serial number at action
  `+0x1b0` from the package's counter `+0x5e4` (`0x2130e0`, plus one per unit), not a slot; its third argument
  (`0xffff` from Load) goes to `0x266970` as a data id, not a position. The rest of the record (`+0x0` flags and
  `+0x14`–`+0x1b`) has no room for per-slot coordinates, so placement comes from elsewhere `[NEEDS VERIFICATION]`.
- **The by-ear report.** The mod's ordinals are the order each enemy was first hit (`HitLines`), not the list order, so
  the 2026-11-23 mismatch on #750 shows that hit order isn't screen order. It says nothing yet about the list.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-23. The game
wasn't run. Section bounds from the exe's PE section table, read as data.

1. `callers 0x21e1c0`, `disasm 0x21e1c0` (prologue only), `decompile 0xc8c90`.
2. `callers 0x2100a0`; `decompile 0x239ea0`: the encounter id read before `0x3dd5a0`, the six-slot loop, the local
   list walk ending in the call at `0x23bbca`.
3. `decompile 0x3dd5a0`, `0x173ba0`, `0x2130e0`, `0x266970`, `0x212920`, `0x21bd80` (no enemy placement found in the
   last two).

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.

## Build plan

1. **Hook.** Restore `BtlPackageUpdate` (`0x21e1c0`) from `8be3b86` as a Release `[GameHook]` observer, in the security
   review scope. When the package pointer changes, or on the first update after `Load`, walk `+0x130` and keep each
   enemy's record (`*(*(*(node+0x28)+0x30)+0x8)`) in list order, through `ModuleMemoryReader`. Retake it when `+0x2c4`
   (the wave) changes.
2. **Use it.** `HitLines` numbers a hit by its record's index in that snapshot, so the first hit already has its
   ordinal.
3. **Settle the screen order** in the same Debug session: log each enemy's list index with the record the target cursor
   or a hit shows, hitting enemies left to right in a three-enemy fight. If the list order isn't screen order, map it
   with the fixed per-count placement the log shows (slot 0 in the centre is one guess `[NEEDS VERIFICATION]`), or read
   each enemy actor's position through unit `+0x10` `[NEEDS VERIFICATION]`.
