# Field state and current map: the field scene machine, the player's input lock and the map id (#144, static leads)

- **Source:** our own reading of the game executable on disk, as shipped (ADR 0002), without running the game: a
  throwaway local disassembler (Iced 1.21.0, the NuGet package, on .NET SDK 9.0.319, in a scratch project outside the
  repository) with the RTTI-to-vtable method of `ui-focus-candidates-2026-11-16.md`, and
  `tools/Metaphor.SignatureCheck --probes` (this repository at the commit that adds this file). Nothing was patched,
  unpacked or run. Script-function addresses come from `squirrel-script-functions-all-2026-11-16.md`. Community source,
  read through the GitHub API: `NM-20/Resonant` at `6b90020b2a99175e25ea7cfc0ae2dc4efc987c89` (2026-08-02, GPL-3.0),
  `program/app/field/new/database/fldDatabase.h`.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

RVAs in this build. Identifiers and offsets only (ground rule 4). Nothing here has been seen in a running game, so every
runtime claim is `[NEEDS VERIFICATION]` until session 6. The world singleton and the player are in
`field-position-2026-11-16.md`.

## What was checked first (ground rule 3)

Resonant's `fldDatabase` is a `fw::SingletonBase` of `0x10D50` bytes (`0x08 + 0xB8`, a `fldScriptData`, a `fldExcel`
data block, then `0x138` to `0x10D50`), instance pointer at `0x142EB7E48` in its (older) build. It names no field
state. No other public source for the field's state machine or the current map was found (see
`field-position-2026-11-16.md`).

## Current map

**`fldDatabase` is the singleton at `0x2EE7368`.** The `fldScene_Root` constructor (`0x5EE590`) allocates `0x10D50`
bytes there through `fw::SingletonBase` (the allocation passes the source path of `SingletonBase.h`), and the
`fldScene_Root` destructor (`0x5EF2E0`) deletes it when its `u32` at `+0x80` is `0xFFFF` (otherwise it calls
`0x5AABC0`). Same size as Resonant's, and `0x2F520` from its address as `fldWorld` is.

- **Map id:** `FldGetCurrentMapID` (`0x906CB0`) jumps to `0x71B610`, which returns `0xFFFF` when `[0x2EE7368]` is null
  and otherwise tail-calls `0x5AB4C0` (`mov eax,[rcx+48h]; ret`). So the current map id is the `u32` at
  `fldDatabase+0x48`, and `0xFFFF` means no field is loaded `[NEEDS VERIFICATION]`.
- **Zone:** `FldGetCurrentMapZoneID` (`0x906CC0`) takes that id, gets a table from `0x898FA0` (a jump into `.text1`, not
  followed) and returns the `u16` at `+0x16` of row `id × 0x2C` (row 0 for `0xFFFF`).
- **Previous map:** `FldGetPreMapID` (`0x906D00`): `0x70ACB0` → `0x3B0B20` returns `[[0x272E310]+0xAC368]`; then
  `[[that+0xA8]+0xD8]` is a `u32`. The exe also holds the string `preMapID` at `0x180CF60` (not followed).
- **Map name:** `MsgSetVariableMapName` (`0x90CC60`, a jump to `0x9EF490`) turns a map id into a message id with
  `0xA02030` and stores it in a message variable slot. `0xA02030` jumps into `.text1`, so how it looks the name up was
  not read. It is the most direct lead to a spoken area name: map id in, message id out `[NEEDS VERIFICATION]`. The
  location banner is `UUIC_Field_LocationName` and `fldLocationName` (vtable `0x1801438`), not read.
- The format string `field/f%03d_%03d.pac` (`0x17FD100`) suggests map files are named by two numbers (major, minor);
  how they relate to the map id is `[NEEDS VERIFICATION]`.

## Field state

**The field scene machine.** `0x5220C6` (inside the field's start-up, whose allocations cite `fldMain.cpp`) allocates
`0x18` bytes, runs the `fldScene_Root` constructor on them, and stores the result at **`0x2EE7348`**. The root's init
(slot 1, `0x5EFA10`) builds a state machine (`0x18` bytes, constructor `0x9758F0` in `.text1`), stores it at
`root+0x10`, and registers 17 scenes with ids `0` to `0x10` through `0x975E40`. The machine's update, `0x976220`
(`SceneMachineUpdate`), reads the current scene at `machine+0x10`, calls its slot 3 (update) and, when that returns 1,
its slot 4 (the next scene id), and hands the id to `0x9760E0` (in `.text1`) to switch.

- Id `1` is **`fldScene_Idle`** (vtable `0x1801D28`): the only scene whose constructor (`0x5EFF20`) isn't a jump into
  `.text1`. Its update (slot 3, `0x5F0310`) runs the field's per-frame subsystems; when bit 0 of `[world]+0x08` is set it
  copies `[world]+0x0C` into its `+0x14` as the next scene, clears the bit and returns 1. Its slot 4 (`0x5EFFE0`)
  returns `+0x14`. So a scene change is requested through `fldWorld+0x08`/`+0x0C`. Read as "free roam", the field's idle
  state `[NEEDS VERIFICATION]`.
- The other 16 ids are constructed in `.text1`. RTTI names exactly 16 other field scenes: `fldScene_CmdBattle`,
  `LoadResource`, `AutoRecovery_All`, `Calendar`, `Camp`, `FollowerList`, `Glossary`, `HumanParam`, `ItemList`,
  `LargeMap`, `MessageLog`, `Network`, `QuestList`, `QuickClassChange`, `QuickSave`, `SkillList`. Which id is which is
  `[NEEDS VERIFICATION]`; at run time the current scene's vtable names it. (`fldScene_Network` is an online feature: we
  read nothing of it and would only ever treat it as "not free", ground rule 5.)

So "in the field and in idle" is: `[0x2EE7348]` non-null, and the first qword of `[[root+0x10]+0x10]` equals
`base+0x1801D28`. Whether events, dialogue and full battles run inside Idle or leave the field (null root) is
`[NEEDS VERIFICATION]`.

**The player's locks.**

- `FldPlayerIsInputLock` (`0x8E51E0`) returns `player+0x34CC > 0` (unsigned): a lock count. `FldPlayerSetInputLock`
  (`0x8E7F20`) calls `0x64A3D0(player, 10, on)` (`.text1`), which reads as a lock taken per reason, here reason 10 for
  scripts `[NEEDS VERIFICATION]`.
- `FldPlayerSetActionLock` (`0x8E5E20`) sets or clears bit 8 of `player+0x34C8`.
- `FldPlayerIsAutoCtrl` (`0x8E5180`) asks the player's controller at `player+0x3110` (`0x6316C0` with `0x7FFFFFFF`),
  read as "scripted movement is driving the player".

**Lead for "free to move":** field idle (above), input lock count `0`, not auto-controlled, and no message window open
(`msg-window-functions-2026-11-16.md`). All of it `[NEEDS VERIFICATION]`. Menus opened from the field (camp, item list,
large map) look like their own scenes, so Idle alone may already exclude them.

## Byte-pattern probes

`tools/Metaphor.SignatureCheck/field-probes.json`, each one match on disk in this build: `FldGetCurrentMapZoneID`
(`0x906CC0`, whose first call is `0x71B610`), `FldPlayerIsInputLock` (`0x8E51E0`), `FieldSceneRootCreate` (`0x5220C6`,
mid-function: the `mov [rip+...],rbx` at `+0x2F` names `0x2EE7348`), `SceneMachineUpdate` (`0x976220`).

## To confirm in session 6

Read only.

1. On the title screen, read `[base+0x2EE7348]` and `[base+0x2EE7368]` (expect null). Load a save in a town: both
   non-null; read `fldDatabase+0x48` and note the map id; walk through an area change and note the new id and
   `FldGetPreMapID`'s chain.
2. Read the current scene's vtable (`[[[root]+0x10]+0x10]`) while walking (expect `base+0x1801D28`), then open the camp
   menu, the large map, the item list, start a field battle, talk to an NPC, and trigger a cutscene. Note the vtable each
   time and resolve it to an RTTI name, building the id table.
3. Read `player+0x34CC` and `+0x34C8` during the same states.
4. Log the message id `0xA02030` would produce only by observing `MsgSetVariableMapName` calls if a script uses it; don't
   call `.text1` functions from our code until a hook design is approved.
