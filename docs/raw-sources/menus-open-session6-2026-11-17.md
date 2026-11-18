# Menus open or closed in capture session 6 (#268)

- **Source:** capture session 6 (capture evening 2, #250) on the maintainer's machine: Debug mod from branch
  `capture-kit-2` at `1ff25fc` through Reloaded-II, with `opt-in-field.txt`, `opt-in-list-count.txt` and
  `opt-in-screen-class.txt` in place. Record formats and the static lead are in
  `docs/raw-sources/capture-2-field-leads-2026-11-17.md` on `capture-kit-2` (`menu-state` from `MenuStateWatch.cs`,
  read on each pad read). Read from the session's `message-log.txt` (`menu-state`, `field`, `choice`, `screen` records)
  with grep and awk, against the maintainer's narration and the OBS recording (ffmpeg 9.0.2 frames, 2 per second). The
  log, narration and recording hold game text, so they stay local; only classes, ids, counts and times are kept here.
  Video time is log time − 161 s (measured on the menu toggles below; the brief's 159 s is 2 s off).
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018), the build
  the kit's signature check names.

## Finding

**The global at `0x2ee7338` (the `CampStatus_IsClosed` handle) is non-null exactly while a field menu is open, and
null again on every close.** 31 `menu-state` records. Every change agrees with the field scene class (`field scene=`)
switching between `fldScene_Idle` and a menu scene, and with `RootMenuChoice` appearing:

| Event | Count | `menu-state` | Agrees | Misses |
|---|---|---|---|---|
| Main menu opened from the field | 9 | handle, `kind=2` | 9 (`fldScene_Camp`) | 0 |
| Shortcut skill list, shortcut item list, large map opened | 3 | handle, `kind=2` | 3 (`fldScene_SkillList`, `_ItemList`, `_LargeMap`) | 0 |
| Closed back to the field | 9 | `camp=none` | 9 (`fldScene_Idle`) | 0 |
| Closed by loading a save from the System menu | 2 | `camp=none` | 2 | 0 |
| Closed by returning to the title | 1 | `camp=none` | 1 | 0 |

- The handle stays the same through every sub-screen (Item, Skill, Equipment, the Archetype Tree, System): no false
  close while moving between menu screens (log 275–350 s).
- `kind` turns from 2 to 3 about 0.1 s before the handle clears, on 3 of the 12 closes (a closing state
  `[NEEDS VERIFICATION]`). `alive` was always 1, `class=?` and `node=-` on every record: the null test is the signal,
  the task-list walk adds nothing.
- **Battles:** 3 battles (two started from the field at log 375 s and 1160 s, one scripted). `camp=none` throughout,
  including every `BTL_*` command, skill and item list: no false open.
- **Dialogue:** 4 conversations (the field `locks` value 16 or `0x480`), and two yes/no choices outside the menus
  (`CommonTalkChoice`, `YesNoChoiceSample`). `camp=none` throughout.
- **The title screen reads open** (`kind=2`, from `TitleChoice` until the save loads). A reader that means "field menus"
  must ignore it; the title is a menu, so for #268 this is harmless.
- Leaving the menus straight into a battle or a dialogue never happened: both start from the field, after the handle
  has already cleared. So "menus closed" is one transition (handle to null), and battles and dialogues are covered by
  the handle staying null.
- **Video check:** log 349–356 s, three closes and two opens toggled quickly; every frame matched the reading
  (menu on screen while non-null, field while null) at the 161 s offset.

How sure: high for the field menus, the shortcut lists and the large map (24 of 24 transitions, 0 misses). Not tested:
the shop, fusion and other menus opened from an NPC or object, and cutscenes started from the menu
`[NEEDS VERIFICATION]`.

## Steps

1. `grep " ms menu-state "` and print each record with its video time.
2. Merge with `field` (scene, map, `action`), `choice` and `screen` records, collapsed on change; count opens and closes
   and the scene each falls in.
3. Extract frames at 2 per second over video 189.5–196.5 s, tile them, and compare menu or field on screen with the
   reading.
