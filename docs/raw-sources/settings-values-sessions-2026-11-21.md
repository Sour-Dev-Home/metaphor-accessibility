# Settings pages: which working-copy dword each row's value is, from two by-ear runs (#96)

- **Source:** the mod's `message-log.txt` from the maintainer's two by-ear runs on 2026-11-21 for #648 (Debug mod
  builds from that PR, `session.ps1 run`; the first is the 999 s run of mod build 57add22 in PR #648's comment of that
  day; the second is later that day, its mod build `[NEEDS VERIFICATION]`, likely 0d55b50 from the follow-up comment).
  Re-read with grep and a throwaway Python 3 script that, for each `settings-copy` record, decodes the bytes as
  little-endian signed dwords from `+0x10` and prints those that differ from the same choice's last dump, with the last
  settled `cursor` record on one of the four pages before it. The logs hold game text, so they stay local; only labels,
  offsets, IDs, indices, counts, values and line numbers are kept. Line numbers are each log's. A third log
  (2026-11-22) has no `settings-copy` records.
- **Saved:** 2026-11-22.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## The copy's layout

- Offsets are from the dump's start, the choice's `+0x4c8` (`SettingsPages.WorkingCopyOffset`).
- All 89 dumps (76 and 13) have the dword at `+0x8` equal to `0x33` (51). `+0x0` holds what reads as a pointer.
- Every value change below moves one dword at `+0x10 + 4·ID`, so the copy reads as one signed dword per setting ID,
  IDs 0–50. The dword after ID 50 (`+0xdc`) changes with the high half of other pointers, so it isn't a setting. That
  the count is at `+0x8` and the values are by ID is our reading of the dumps, `[NEEDS VERIFICATION]` in the exe.
- A page's dump holds every page's settings: the in-play System page's first dump already has the values changed on the
  title's System and Sound pages, and Restore Defaults on the in-play System page (first log, 323314) reverted IDs 5,
  17, 18 and 27 together.

## Rows pinned to an ID

Each row below is the settled cursor (`cmd=0x1`) the change followed; the change itself came after a `cmd=0x16` input
on the same index (for example 180700–180873).

| Page, rows | Index | Row's label | ID (offset) | Seen (log, lines) |
|---|---|---|---|---|
| System, 20 | 11 | `System_Option_72` | 17 (`+0x54`) | 0→1 (first, 180873) |
| System, 20 | 12 | `System_Option_24` | 18 (`+0x58`) | 0→1 (first, 181719) |
| System, 20 | 16 | `System_subject_name_PC_04` | 27 (`+0x7c`) | 50→77 in steps of 1 and 3 (first, 182560–184983) |
| System, 20 | 17 | `System_subject_name_PC_05` | 28 (`+0x80`) | 50→49→54→52, steps of 1 (second, 184964–187925) |
| System, 24 | 1 | unnamed (fetch order only) | 5 (`+0x24`) | 0→1 (first, 312301) |
| Sound, 6 | 1 | unnamed | 1 (`+0x14`) | 100→71→96→74, steps of 1 and 3 (first, 204980–215237) |
| Sound, 6 | 3 | unnamed | 23 (`+0x6c`) | 0→1→0→1 (first, 207674–217630) |
| Display, 4 | 3 | unnamed | 47 (`+0xcc`) | 0→1→0→1 (first, 197796–198194) |

The System labels are the rows pinned in docs/raw-sources/settings-ok-and-rows-by-ear-2026-11-21.md. On the in-play
System page (24 rows) the same labels sit at rows 14, 15, 20 and 21; that they keep their IDs there follows from the
IDs being per setting, and from Restore Defaults there moving 17, 18 and 27.

## Not pinned

- Display, 4, index 1: IDs 44 and 45 move together (16→0 and 0→1, then back; first, 197132 and 197336).
- Graphics, 6, index 2: IDs 38 and 39 move together (2→0 and 2→1; first, 200343).
- Sound, 6, index 5 (the device list): ID 36 went from a large number to -1 (first, 336214).
- Some diffs pair two pages' dumps from one reused choice address rather than a change (for example Graphics index 0,
  ID 47, first, 199086, after Display's V-Sync change); and ID 57's dword, past the 51, moves with them.

## How values are said

- No `resolve` record comes with a value change: the page fetches its choice labels as it is built. `System_Option_82`,
  `_83` and `_84` resolve together, with consecutive serials, at every page build (first, 165991–165993), and are the
  message-speed row's candidate choices for values 0, 1 and 2, `[NEEDS VERIFICATION]`. No label for "on" resolved;
  `System_select_name_PC_10` ("off") resolves on the Graphics and Display pages (first, 191151), row unknown.
- So every pinned value is said as its number for now, on/off rows as 0 and 1.
