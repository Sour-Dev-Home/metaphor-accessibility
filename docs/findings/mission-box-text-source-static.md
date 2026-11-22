# The field's mission box takes its main mission from the story flags; its text is built in `.text1`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`mission-line-message-log-2026-11-21.md`](../raw-sources/mission-line-message-log-2026-11-21.md); none
  else, see Steps

Static follow-up on #658 (PR #665). The mission box is `app::ui::FieldMissionLayout` (vtable `0x21ab1e8`). It holds a
`MainMission` part at `+0x260` and two sub-mission parts, and fills each part's text from a function keyed on a
mission id. The current main mission's id is computed in `.ecode` from the story flags and the main mission table. Its
text, and both sub-missions' text, are built in `.text1`, which is off-limits (ADR 0010). No hooked call fetches the
text, which fits the 2026-11-21 log. So no safe read of the mission's text is pinned yet.

## Evidence

- **The refresh.** `FieldMissionLayout::vf19` (`0xc99d70`) reads the main mission id (`0xf2b060` calls `0x724130`),
  marks it seen, then calls `0xc9a340`. `0xc9a340` fills three texts. Each is a text element's id
  (`0x141c0b0`), then a string from a producer, then set on the element (`0x141ef80`). The producers:
  - `+0x970`, a sub-mission: thunk `0xa69250` into `.text1`;
  - `+0xc10`, a sub-mission, shown when `0xf34620` says so: thunk `0xa69260` into `.text1`;
  - the main mission's title, element 2 of the `MainMission` text array (`+0x260 + 0x580 + 0xe0`, drawn as
    `font_rect_C4_mission_title` by `MainMission::vf18`, `0xc998e0`): thunk `0xa69240` to `0x723e50` in `.ecode`.

  The two lines quoted in #658 are `MissionText_Field_…` and `MissionText_Dungeon…` labels (raw source). So they
  are most likely the sub-mission texts, built in `.text1` [NEEDS VERIFICATION].
- **The current main mission.** `0x724130` takes no arguments and returns a 16-bit id, `0xffff` for none. It tests
  story flags `0x120a` down to `0x1165` with the flag test `0x3b0ab0` (the `StoryFlagTest` signature: bit `n` of
  `G + 0x22c5c`). For each set flag it asks `0x3c5c80(flag, &category 0)` for that flag's row. It takes the first flag
  that has a row, falling back to `0x1174` and earlier ones. `0x3c5c80` searches the table at
  `[0x2dff848] + 0x60`: per category, a `{begin, end}` pair of 0x18 bytes, then rows of `0x30` bytes. It returns the
  index of the row whose `+0x20` holds the flag, or `0xffff`. `0x2dff848` is written only by `0x3e1ad0` (the load)
  and `0x7248b0` (cleared). Category 0 is presumably `Mission_Main_Data.bin`, whose path `app::dat::Mission::vf0`
  (`0x3c6100`) reads [NEEDS VERIFICATION].
- **Main mission id to text.** `0x723e50` repeats the scan, then calls thunk `0x3c5db0` (into `.text1`) with the id
  and two zeroes, and thunk `0x9f5c10` (into `.text1`) to make the string. Which `MissionText_MainMission_NN` label a
  row's id names is unpinned [NEEDS VERIFICATION].
- **Seen marks.** `0x3b3ef0(id)` sets the byte at `G + 0x77a5c + id`. `0xf34850` returns whether `0x3b3170(id)` says
  it is unset, which `vf19` passes on as a "new" flag [NEEDS VERIFICATION: what the HUD does with it].
- Not checked in memory: any of it.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[how-to](../contributors/how-to/query-the-ghidra-project.md)), on the exe of the build above:

1. `grep -aoE "\.\?A[VU][A-Za-z_0-9@]*Mission[A-Za-z_0-9@]*"` on the exe for the class names. Then `vtable
   app::ui::FieldMissionLayout`, `vtable app::ui::layout::field::mission::MainMission` and `vtable app::dat::Mission`.
2. `callees` on each slot; `decompile 0xc99d70`, `0xc9a340`, `0xc998e0`.
3. `decompile 0x723e50`, `0x724130`, `0x3c5c80`, `0x3b0ab0`, `0x3b3ef0`, `0xf2b060`, `0xf34850`; `callees
   0x723e50`; `callers 0x724130`, `0x3c5db0`, `0xa69240`; `global 0x2dff848`.
4. `grep -boa` for `Mission_Message_1.msg`, turned into an RVA with the PE section table (`0x2122e50`). Its only
   pointer is in a static `{pointer, 0x24}` pair at `0x27b2050`, and `global` finds no code reading either. That fits
   the files being compiled once from a list.

## What the next session must check

The Debug build's "where am I" record now ends with `mainFlags=`, the highest set flags in `0x1165`–`0x120a`. Press L
before and after a main mission changes and note the box's title. A change in the top flag beside the title confirms
the scan in memory. The row for that flag (`+0x20`) in a local copy of `Mission_Main_Data.bin` is then the lead for
the label. Never commit the copy. The sub-missions, the lines #658 quotes, still need a source outside `.text1`.
