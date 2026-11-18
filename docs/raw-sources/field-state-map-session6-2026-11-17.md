# Free-to-move state and map id in capture session 6 (#180)

- **Source:** capture session 6 (capture evening 2, #250): Debug mod from branch `capture-kit-2` at `1ff25fc` through
  Reloaded-II with `opt-in-field.txt`; the `field` record (`FieldWatch.cs`), fields defined in
  `docs/raw-sources/capture-2-field-leads-2026-11-17.md` on `capture-kit-2`. Read from the session's `message-log.txt`
  (1050 `field` records) with grep and awk, against the narration and the OBS recording. Log, narration and recording
  stay local (game text). Video time is log time − 161 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Findings

**Scene: the class of `[[[fldScene_Root]+0x10]+0x10]` names what the field is doing.** Classes seen (`@fld@app`),
with record counts:

| Scene class | Records | When |
|---|---|---|
| `fldScene_Idle` | 1004 | free roam, and also during dialogue |
| `fldScene_Camp` | 9 | main menu open |
| `fldScene_SkillList`, `_ItemList`, `_AutoRecovery_All` | 1 each | d-pad shortcuts |
| `fldScene_LargeMap` | 1 | large map open |
| `fldScene_CmdBattle` | 8 | entering and inside a battle |
| `fldScene_LoadResource` | 17 | area change or load |
| `fldScene_HumanParam` | 2 | briefly near party members at the dungeon entry `[NEEDS VERIFICATION]` |
| none | 6 | title, save loading |

**Locks: `player+0x34CC` is 0 when free to move.** In `fldScene_Idle` it was 0 on 917 of 1004 records. Non-zero values
lined up with the narration: 16 while talking to someone (3 conversations), `0x480` then `0x80` during a talk with a
choice, `0x20000` with every menu scene, 8 as a battle starts, 1 then 2 while an area loads, `0x80000` near enemies
before a battle (log 863–902 s and 1153–1160 s) and `0x400` or `0x500` after a fast travel and around a chest
(meanings of the last two `[NEEDS VERIFICATION]`). In battle (`fldScene_CmdBattle`) it read 0, so the scene must be
checked as well.

**Free to move = scene `fldScene_Idle` and `locks` 0.** This held on every narrated case: walking (yes), menus, map,
battle, dialogue, loading (no). Cutscenes were not checked separately `[NEEDS VERIFICATION]`.

**Map: `fldDatabase+0x48` is a numeric map id that changes on every area change.** Ids seen: 371 (dungeon entry), 372
(after the door into the combat area), 2 (after loading a save), 12 (after fast travel), 285 (after loading another
save). Each change came with a `fldScene_LoadResource` and a narrated area change; frames of maps 372 and
285 show two different area names on screen. The id stays put through menus, battles and dialogue. The map's name and floor were not read: no id-to-name
table is known `[NEEDS VERIFICATION]`.

`action` (`player+0x34C8`) bits `0x4000` and `0x20`/`0x10` are set in most free roam and clear on loads; their meaning
is `[NEEDS VERIFICATION]`.

How sure: high for the scene classes and the free-to-move test on the cases above; high that the map id tracks the
area; the name and floor are not found.

## Steps

1. Collapse `field` records on change of `scene`, `map` and `action`, with video time; tabulate `locks` by scene.
2. Match each change to the narration and to frames at the narrated times.
