# The interaction prompt's target is picked in protected code; each gimmick's reach test is readable `.ecode`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-units-names-exits-static.md`](field-units-names-exits-static.md),
  [`field-nearby-unit-names-static.md`](field-nearby-unit-names-static.md),
  [`field-nearby-session6-2026-11-17.md`](../raw-sources/field-nearby-session6-2026-11-17.md)
- **Functions:**
  - fldGmcDoor::vf44 | 0x6992a0 | .ecode | Field | known only | The door's reach test: distance, height and facing angle to the player. | finding
  - fldGimmick::vf44 | 0x697d00 | .ecode | Field | known only | The gimmick's reach test, the same with fixed limits. | finding
  - fldChara::vf44 | 0x644b10 | .ecode | Field | known only | The reach test shared by `fldNpc`, `fldEnemy`, `fldMember` and `fldPlayer`. | finding
  - FldNpcIsCanOnTalk | 0x8e3260 | .ecode | Field | known only | Script function on whether an NPC can be talked to. | finding

For #178 and #181. The prompt the player sees near a door or chest is the UI part `UUIC_Field_ActionGuide`, fed by the
field singleton `fldActGuide`. Both sit behind `.text1`: the UI's slots and `fldActGuide`'s only virtual are jump
stubs, and the one `.ecode` helper found near them, a "which unit is closer to the player" comparator, is referenced
only from `.xcode`. So the mod can't read the game's choice of target. What it can read is each gimmick's own reach
test, virtual slot 44 (`+0x160`), whole `.ecode` functions with plain thresholds. A "what can I use here" answer can
apply the same thresholds to the unit lists, read-only, without calling anything.

| Piece | Where | Usable |
|---|---|---|
| Prompt UI | `UUIC_Field_ActionGuide@ui@app`, vtable `0x2258aa0`; `vf1` is a `jmp` into `.text1`, `vf13` calls a `.text1` thunk | No (ADR 0010, #586 rule) |
| Prompt's candidates | `fldActGuide@fld@app`, a singleton by its RTTI (`SingletonBase<fldActGuide>`), vtable `0x1800dd0`; its one slot is a `jmp` into `.text1` | No |
| Closer of two units | `0x5b3df0 (unitA, unitB) → bool`, distances to the player; referenced only from `.xcode` | Read its rules, don't hook |
| Door reach | `fldGmcDoor::vf44` `0x6992a0` | Yes, `.ecode`, matches once |
| Chest and other gimmick reach | `fldGimmick::vf44` `0x697d00` (also `fldGmcTreasure`'s slot 44) | Yes, `.ecode`, matches once |
| Person reach | `fldChara::vf44` `0x644b10` returns `chara+0x316c`, a state other slots set, not a reach test | Not found |
| Prompt words | `AccessPointName.msg`, serials 10873–11280 | A lookup of it would show on the prompt `[NEEDS VERIFICATION]` |

## Evidence

### The reach tests

- **`fldGmcDoor::vf44`** (`0x6992a0`) takes the door's node position (`0x96f820(door+0x10)`), the player's
  (`0x96f820([[world+0x20]]+0x10)`) and the player's facing (`0x96f6f0`). It returns 1 when the horizontal distance is
  below a limit, the height difference below `300.0` (`0x17c65ec`), and the angle between the facing and the way to the
  door (`0x5877c0`) below `π/2` (`0x17c79b4`, `0x3fc90fdb`). The distance limit is `150.0` (`0x17c6124`) or `300.0`,
  picked by `0x5ab030(fldDatabase)`: `150` when it returns 1 or 2, else `300`. What that value is (a map kind) is
  `[NEEDS VERIFICATION]`.
- **`fldGimmick::vf44`** (`0x697d00`) is the same test with `300.0` for distance and height and `π/2` for the angle.
  `fldGmcTreasure`'s vtable keeps it at slot 44, so a chest is in reach within 300 units. Session 6's nearest chest
  came to about 100 at the opening, which fits.
- **`fldChara::vf44`** (`0x644b10`), shared by `fldNpc`, `fldEnemy`, `fldMember` and `fldPlayer`, returns the `int` at
  `+0x316c`. `vf43` writes it and `fldNpc::vf7` stores 5 or 6 there, so it reads as a character state, not reach. How
  near a person must be to talk is not found `[NEEDS VERIFICATION]`. `FldNpcIsCanOnTalk` (`0x8e3260` → `0x67b500`)
  checks a flag at `npc+0x36b0` and two calls, no distance.

### The comparator

`0x5b3df0` gets the player's node position and facing, then for each of two units picks a position by its kind (`+0xa08`
equal to 2 and `+0x2884` equal to 3 or 5 take a special point, others their node) and compares their distances.
`callers 0x5b3df0` finds only a data reference at `0x14c374b4`, inside `.xcode`, so whatever sorts with it is protected.
That `fldActGuide` is what sorts, and that the nearest unit in reach becomes the prompt, are guesses from the names and
the neighbouring addresses `[NEEDS VERIFICATION]`.

### The prompt's words

`message/AccessPointName.msg` (table entry 34, serials 10873–11280; [names note](field-nearby-unit-names-static.md))
holds labels by place and action, such as `AccessPointName_DNG_GLOBAL_DOOR_ACTION`. `AccessSpotList.bin`
(`COMMON/field/source/bin/`, loaded into `fldInitDataBase<fldAccessSpotList>`) is the likely table from access points
to labels; its loader and every reference to the file name are in protected code. In the two local Debug logs at
hand (2026-11-20 and 2026-11-21) no `GetMessageBySerial` resolve falls in 10873–11280 and no `lookup` has `id=34`, so
the prompt's text either goes through `.text1` alone or wasn't on screen in those runs `[NEEDS VERIFICATION]`.

### Signatures

Each matches once with `tools/Metaphor.SignatureCheck --probes` (a scratch probes file, not committed):

| Name | RVA | Pattern |
|---|---|---|
| `fldGmcDoor::vf44` | `0x6992a0` | `48 8B C4 48 89 58 10 57 48 81 EC C0 00 00 00 C5 F8 29 70 E8 48 83 C1 10 C5 F8 29 78 D8 C5 78 29 40 C8 C5 78 29 48 B8 C5 78 29 50 A8 C5 78 29 58` |
| `fldGimmick::vf44` | `0x697d00` | `48 8B C4 48 89 58 08 57 48 81 EC A0 00 00 00 C5 F8 29 70 E8 48 83 C1 10 C5 F8 29 78 D8 C5 78 29 40 C8 C5 78 29 48 B8 C5 78 29 50 A8 48 8B 05` |
| comparator | `0x5b3df0` | `48 8B C4 48 89 58 08 48 89 70 10 48 89 78 18 4C 89 70 20 55 48 8D A8 18 FF FF FF 48 81 EC E0 01 00 00 C5 F8 29 70 E8 C5 F8 29 78 D8 C5 78 29 40` |

## What would settle it

In a Debug run with the message log on: walk up to a door until the prompt shows, back off until it goes, then do the
same facing away and at a chest. Note the times. Read the player's and the door's positions with the inspector at each
point (`follow base+0x2ee7510 0x20 0x0 0x80`; the door from `0x158`). The prompt should come and go where the distance
crosses 150 or 300 and the facing crosses 90°. Look for `lookup id=34` records or resolves in 10873–11280 at the same
times: present means the mod can also hear the prompt's words through hooks it already has.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project of the build above, Windows 11 x64,
2026-11-21. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says otherwise.

1. A byte search of the exe for `.?AV` names with `Action`, `Access`, `Guide`, `Hit`, and for the strings
   `AccessSpot`, `AccessPoint`, `ActGuide`; file offsets turned into RVAs from the section table.
2. `vtable app::fld::fldActGuide`, `disasm 0x5b3950`, `global 0x1800dd0`, `callers 0x5b3950`;
   `vtable app::ui::UUIC_Field_ActionGuide`, `decompile 0xfbf4a0`, `disasm 0xfbf120`; `global 0x17f87d8`,
   `global 0x17f8906` (none found); `decompile 0x9069d0`, `decompile 0x90c460`.
3. `callers 0x96f820`; `decompile 0x5b3df0`, `callers 0x5b3df0`; `decompile 0x6992a0`, `decompile 0x697d00`;
   `vtable app::fld::fldNpc`, `vtable app::fld::fldGmcTreasure` (slot 44); `decompile 0x644b10`, `offset 0x316c`;
   `decompile 0x8e3260`, `decompile 0x67b500`.
4. The thresholds: `tools/Metaphor.SignatureCheck --bytes <exe>` at `0x17c6124`, `0x17c65ec`, `0x17c79b4`, 8 bytes each,
   read as little-endian floats.
5. The message-file table walk of the [names note](field-nearby-unit-names-static.md); the local logs grepped for
   resolves in 10873–11280 and `lookup id=34`.

Nothing in `.text1` was decompiled; the stubs were noted as jumps and not followed. No decompiled output is quoted.
