# Each save file starts with a 0x228-byte slot header holding the date, weekday, time of day, playtime and leader name

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's own files offline (no ADR 0002 row covers data files, which no protection guards);
  and opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on `save-load-slot-screens.md`
  ([#707](https://github.com/Sour-Dev-Home/metaphor-accessibility/pull/707)'s branch), [`calendar-date-time-and-quests-static.md`](calendar-date-time-and-quests-static.md) and
  [`chapter1-opening-inventory-own-capture-2026-11-20.md`](../raw-sources/chapter1-opening-inventory-own-capture-2026-11-20.md)
- **Functions:**
  - SaveDataHeaderArrayDtor | 0x394a70 | .ecode | Menus | known only | Frees the 18-header array, 0x26e0 bytes. | finding

For #773. Each save slot `n` (0 to 16) is the file `save%04d.sav` of `n`, whose header names `n` itself, and the load
list's row `n` is file `n` (rows 0 and 4 match below). The file's first 0x228 bytes are the slot's header: the in-game
day, weekday and time of day (the same three the calendar keeps), the playtime in seconds, and the leader's name. The
date, weekday, time of day and playtime now match what the load list shows (section 2). Place and level are not in
the header's non-zero fields, and difficulty is not pinned. The mod reads those bytes from disk, read-only, when a slot
gets focus. A memory read would need a path to the game's header array, which static work didn't find.

## Evidence

- **The files.** `%APPDATA%\SEGA\METAPHOR\Steam\<Steam ID>\` holds `save0000.sav` to `save0016.sav`, 17 files for the
  load list's 17 rows (`save-load-slot-screens.md`). The exe holds the format `save%04d.sav` (rva `0x2262420`). The
  files are 1.4 to 2.8 MB, not encrypted or compressed at the start.
- **The header's size.** RTTI names `fw::StaticArray<app::SaveDataHeader,18>` (vtable `0x17e5448`). Its destructor
  (`0x394a70`) frees `0x26e0` bytes, which is `0x10` plus 18 × `0x228`. In every file read, offset `0x228` is where the
  header's sparse fields end and dense game data starts (`0x10000`, then `1944`, at `0x228` and `0x22c` in all six).
  So a `SaveDataHeader` is `0x228` bytes and the file starts with one. Why 18 and not 17 is open
  `[NEEDS VERIFICATION]`.
- **Fields, from six files** (slots 0, 1, 2, 8, 11 and 16), as `int32` unless said:

| Offset | Values (slots 0, 1, 2, 8, 11, 16) | Reading | How sure |
|---|---|---|---|
| `+0x00` | 22 in all | format version (`system.sav` starts with 22 too) | Likely |
| `+0x04` | 0, 1, 2, 8, 11, 16 | slot number, same as the file name | Proven by all six |
| `+0x08` | 124, 1, 1, 131, 124, 125 | in-game day count, as `G+0x311e8` | Strong: next two fields fit |
| `+0x0C` | 4, 1, 1, 1, 4, 0 | weekday, the day count mod 5, as `G+0x311ec` | Strong: all six equal `+0x08` mod 5 |
| `+0x10` | 3, 3, 3, 3, 3, 5 | time of day, as `G+0x311f0` (`System_General_Time_01`–`_07`) | Proven: matches the screen (below) |
| `+0x54` | 170330, 475, 564, 184778, 170321, 173117 | playtime in seconds | Proven: matches the screen (below) |
| `+0x58` | 567, 513, 513, 572, 567, 568 | rises with story progress; not the place (below) | Unknown |
| `+0x5C` | UTF-32, one character per `int32`, zero-padded to `+0xDC` | the leader's (protagonist's) name | Proven by eye: a name in each file |
| `+0xE4` | 22, 1, 26, 58, 2, 21 | not the level (below) | Unknown |
| `+0x1F0` | 2, 0, 2, 2, 2, 2 | unknown (0 only in the earliest save) | Unknown |
| `+0x1F4` | 1, 0, 0, 1, 1, 1 | unknown | Unknown |

Every other `int32` in `+0x14` to `+0x228` was 0 in all six. The weekday rule is from the calendar note: the
calendar's getter returns the day count mod 5. No place string is stored, so the place, if `+0x58` is it, is an id the
game looks up.

- **The game's own path.** `Savedata_HeaderStore` (`0x90f580`) is an empty function in this build.
  `Savedata_SaveStart` (`0x90f690`) hands off to thunks into `.text1`, which weren't followed. A `Signal` passing a
  `map<uint, SaveDataHeaderWithOpt>` and `SaveDataWorker` (vtable `0x210ea58`) are in RTTI. No analysed code addresses
  the header array's vtable, so its owner and a static path to it are unknown.
- **The slot rows' serials.** The 9285–9289 weekday and 9297–9303 time-of-day serials the slot list resolves
  (`save-load-slot-screens.md`) match `+0x0C` and `+0x10`'s ranges. Of 34 `GetMessageBySerial` callers scanned
  (the wrappers at `0xa67f70`–`0xa6a920`, `0x819410`, `0xb15af0`, `0xb1bea0`, `0xd41d50`), none holds 9285–9289 or
  9297–9303 as a constant (`0xa694e0` and `0xa695a0` hold 9290–9294), so the row builder adds an offset or reads a table
  `[NEEDS VERIFICATION]`.

These are the maintainer's own save files, read in place and read-only, not copied. This note gives offsets and small
numbers only: no name, Steam ID or file bytes.

### Matched with the load list (2026-11-24)

The by-ear session of 2026-11-22 (#773, mod 618cf84 Debug) noted what three rows show; its log has the serials each
opening resolved. The files of those rows were read again on 2026-11-24, all 17: `save0004.sav` is unchanged since
2026-11-21, and the autosave's values are the ones read on 2026-11-22 above.

| Row as shown | File values | Match |
|---|---|---|
| Autosave: 10/05, Idols Day, afternoon, 47:18, level 55 | `+0x08` 124, `+0x0C` 4, `+0x10` 3, `+0x54` 170330, `+0xE4` 22 | date, weekday, time and playtime match; level doesn't |
| Slot 4: 06/02, Waters Day, late night, 10 min, level 1, road to Border Fort | 1, 1, 6, 643, `+0xE4` 65535, `+0x58` 513 | date, weekday, time and playtime match; level doesn't |
| Slot 3: Royal Capital | `+0x58` 513, `+0xE4` 65535 | same as slot 4's, a different place |

- **Playtime is seconds**, shown as hours and minutes: 170330 s is 47:18:50, and 643 s is 10 min 43 s.
- **Date.** The calendar's `GetMonth` and `GetDay` ([calendar note](calendar-date-time-and-quests-static.md#1-date))
  turn day 124 into 10/05 and day 1 into 06/02, as shown.
- **Weekday.** The autosave row resolved serial 9289, which is 9285 + `+0x0C` (`System_General_Week_01` + 4), and it
  showed Idols Day; slot 4's 1 showed Waters Day. The Calendar's five names in that order are in
  `src/Metaphor.Features.Menus/CalendarFocus.cs` (#193).
- **Time of day.** The autosave row resolved serial 9300, which is 9297 + `+0x10` (`System_General_Time_01` + 3), and
  showed afternoon; slot 4's 6 showed late night. So `+0x10` is the time-of-day index, not difficulty.
- **Level is not in the header's fields:** `+0xE4` is 22 at level 55 and 65535 at level 1, and no other non-zero
  field fits. **Place is not `+0x58`:** slots 3 and 4 both hold 513 but show different places. Both are left out
  until a source is found `[NEEDS VERIFICATION]`; the `Opt` in `SaveDataHeaderWithOpt` may be where they are.
- **Empty slot:** all 17 files exist in this folder, so what an empty row's file looks like is still
  `[NEEDS VERIFICATION]`. Whether the save list's 16 rows are files 1 to 16 is `[NEEDS VERIFICATION]` too.

## Steps

Windows 11 x64, PowerShell 7, 2026-11-22. The game wasn't run.

1. SHA-256 of `METAPHOR.exe` (from `Find-MetaphorGame`): the build above.
2. For saves 0, 1, 2, 8, 11 and 16: read the first `0x228` bytes, print each non-zero `int32` and decode `+0x5C` as
   UTF-32. Print the non-zero `int32` runs up to `0x3000` to see where the header ends.
3. `grep -ao` over the exe for `SaveDataHeader`, `SaveDataWorker` and `SaveLoad`. Find the `type_info` of
   `.?AV?$StaticArray@USaveDataHeader@app@@$0BC@@fw@@`, the locator that names it, then the vtable after it.
4. Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`): `vtable 0x17e5448`, `decompile 0x394a70`, `0x90f580`,
   `0x90f690`; `global 0x17e5448`; `callers 0x9e3d30`, then `disasm` of each caller, searched for constants
   `0x2430` to `0x245f`.
5. 2026-11-24, Python 3 with `struct`: for each of the 17 files, read the first `0x228` bytes and print each non-zero
   `int32` outside the name (`+0x5C` to `+0xDF`). Compare with the 2026-11-22 read-outs in #773 and that session's
   `resolve` serials quoted there.

## What would settle it

A by-ear and file check, with the Debug build:

1. Before launching, copy the save folder aside by hand and print each `saveNNNN.sav` header's fields from that copy.
2. Open Load from the title. For each of a few slots (the autosave row, one early save, one late save, an empty row if
   any), say or note what the row shows: date, weekday, time of day, place, playtime, level, difficulty.
3. Match them to the table: playtime against `+0x54` (seconds, or something else), place against `+0x58`, level and
   difficulty against `+0xE4`, `+0x10`, `+0x1F0` and `+0x1F4`. Check that the first row is `save0000.sav` and is the
   autosave, and what an empty slot's file looks like (missing or zeroed).
4. Save to a new slot from the System menu, then compare that file's header with the screen once more.
