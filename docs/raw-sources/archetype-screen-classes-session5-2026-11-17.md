# Screen classes of archetype details and the Archetype Tree in capture session 5 (#227)

- **Source:** capture session 5 (#229) on the maintainer's machine: Debug mod from branch `capture-1` at `219282c`
  through Reloaded-II, `hooks 31/31`, Steam Input on, `opt-in-screen-class.txt` in place, so both
  `ChoiceCursorGetterListener` and `ClassDetailCursorSetupListener` (`archetype-detail-cursors-2026-11-17.md` on
  `capture-1`) were installed. Read from the session's `message-log.txt` (`screen`, `cursor` and `input` records; formats
  in `src/Metaphor.Core/MessageLog.cs` on `capture-1`) with grep and awk, against the maintainer's narration. The log
  and narration hold game text, so they stay local; only classes, addresses, indexes and times are kept. Log time is
  video time + 51 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Findings

**Archetype details is one class per entry point, the same class on all three tabs; the tab is told by which of its
three cursor managers moves.** The slot-10 hook named it: every archetype details input logged an owner class.

| Opened from | Screen class (`@ope@ui@app`) | `screen` records | Tabs seen |
|---|---|---|---|
| Equipment (details button, pad `0x1000`) | `EquipClassDetailChoice` | 25 | 3 managers, cycled by RB (`0x0800`): 1→2→3→1 at 609–611 s |
| Status | `StatusMenuClassDetailChoice` | 30 | 3 managers; RB at 757 s moves to the second |
| Archetype Tree | `AcademiaClassDetailChoice` | 13 | 2 of 3 seen: RB at 809.6 s, LB (`0x0400`) back at 810.8 s |

- Each opening of details makes three new managers (the `+0x1740`, `+0x1750`, `+0x1760` fields the setup function
  fills); the owner class stays the same when the tab changes. Which field is which tab, and the tabs' names, are
  `[NEEDS VERIFICATION]`: the log records the owner, not the field.
- Cursor indexes reached up to 5 on one tab (Status and Tree details), so at least one tab is a list of 6 rows or more.
- The list before it on Equipment is `EquipMenuClassChange` (and `EquipMenuClassChangeConfirm`); on Status,
  `StatusCharaSelChoice`.

**The Archetype Tree has no cursor-manager class.** Y on the main menu (pad `0x1000`, 776.6 s) opens
`AcademiaClassGetCharaSel@ope@ui@app` (pick whose tree; 1 record). Confirm (779.8 s) opens the tree; the maintainer then
moved the tree cursor with the d-pad and stick from 783.4 s to 784.2 s, and no `cursor` or `screen` record was logged
until details opened at 803.0 s. The tree's cursor doesn't go through a `CursorManager`, so neither listener can name
it. Its class is `[NEEDS VERIFICATION]`; the RTTI candidates stay those in `archetype-detail-cursors-2026-11-17.md`.
From the tree, confirm on an archetype gives a choice of at least 4 items whose manager has no owner (`owner=none`, 815.7 s).

## Steps

1. `awk` over `screen` records, collapsed by manager and owner class, for log time 600–900 s.
2. For each `*ClassDetail*` owner, the managers in order of first use, with the last non-zero pad mask before each and
   the highest cursor index each reached.
3. `cursor`, `screen` and pad `input` records for 770–830 s, to place the tree's character select, the tree and details.
4. Match the times to the narration at video 09:01–12:43.
