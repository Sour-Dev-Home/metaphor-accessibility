# The in-game main menu's cursor, and the dialogue choice's screen class (#97)

- **Source:** the maintainer's local message log from a Debug run on 2026-11-17, after session 4
  (`docs/raw-sources/screen-class-session-2026-11-17.md`): Reloaded-II, `hooks 31/31`, with the opt-in files for screen
  classes, dialogue window, menu values, UI text panes and voice. Read offline with `grep` and `awk` on 2026-11-17. The
  log holds game text and stays local; only class names, labels, indexes, counts and times are kept here.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018), from the
  run's `mod-status.json`.

## Findings

**The main menu (`RootMenuChoice@ope@ui@app`) has 9 cursor positions, 0 to 8.** All 136 inputs whose owner was
`RootMenuChoice` had `row` 0 and `top` 0–8, so the index is `top`. Settled inputs (`cmd=0x1`) reached every index
from 0 to 8, and none past 8.

**Index order is the entry order** of `docs/raw-sources/menu-screens-inventory-2026-11-17.md` (Skill, Item, Equipment,
Party, Follower, Quest, Calendar, Memorandum, System). The main menu's last settled index before another screen class
took over, over the run:

| Last index | Next screen class | Times |
|---|---|---|
| 1 | `ItemMenuChoice` | 3 |
| 2 | `EquipCharaSelChoice` | 3 |
| 3 | `StatusCharaSelChoice` | 2 |
| 3 | `CommonWarningChoiceWithLayout` | 1 |
| 5 | `CampQuestChoice` | 1 |
| 6 | `CalendarChoice` | 1 |
| 8 | `SystemMenuChoice` | 5 |
| 2 | `CooperationMenuChoice` | 1 |

16 of 17 agree with that order. The Follower case (index 2, expected 4) is unexplained; a move straight into a select
without a settled input would do it. `[NEEDS VERIFICATION]`: Skill (0), Follower (4) and Memorandum (7) by a selection.

**The entries' names aren't messages or text panes.** No message compiled in the run has a line that is exactly
one of the nine entry names in a list of them; each name only appears alone, under labels for other uses (tutorial
categories, key bindings, options). Over the first main menu visit (134 450–140 200 ms) the pane listener saw 380
records with only two distinct texts, unchanged as the focus moved, so neither the entries nor their subtitles are
drawn as text panes. The owner's logged fields are two `CustomSprite@ss` (`+0xf0`, `+0xf8`). `[NEEDS VERIFICATION]`:
where the names come from (likely sprites); until then the main menu speaks positions only.

**A dialogue's reply choice is `CommonTalkChoice@ope@ui@app`.** Taking the first owned cursor input after each
`select` record, or `request` record naming a choice, 85 had that class and 11 had menu classes (1–4 each), and all 85
inputs with that class in the run were among them.

**No count is logged for a cursor.** The `cursor` records hold `top`, `row`, the command, result and flags only
(`docs/raw-sources/ui-focus-candidates-2026-11-16.md`), so the number of items on an unknown screen isn't known.
`[NEEDS VERIFICATION]`: where the cursor manager keeps it.

## Fixture

`tests/fixtures/sessions/main-menu-screen-class.txt`: the run's cursor records from 155 800 to 182 400 ms (main menu,
System, a Yes/No dialog, then Equipment, Party, Item and System from the main menu), written by
`tools/Metaphor.SessionFixture`.
