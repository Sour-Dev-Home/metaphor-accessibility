# Chapter 1's silent text is mostly help lines, descriptions, narration and Gallica's report lines, not dialogue

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's own data archives (CPK) offline with our read-only tool (`tools/Metaphor.CpkExtract`),
  output kept in git-ignored `extracted/`; our own Reloaded-II mod reading the running game's memory and hooking its
  functions, as those community mods do (its message log and speech transcript)
- **Sources:** none, see Steps; builds on
  [`corner-advice-report-message-open-static.md`](corner-advice-report-message-open-static.md),
  [`item-equipment-menu-names-from-resolves.md`](item-equipment-menu-names-from-resolves.md),
  [`settings-screens-inventory-static.md`](settings-screens-inventory-static.md),
  [`battle-move-banner-text-reference-static.md`](battle-move-banner-text-reference-static.md) and
  [`tutorial-table-maps-numbers-to-labels.md`](tutorial-table-maps-numbers-to-labels.md)
- **Functions:** none

For #546. In the 12 sessions of 2026-11-21 to 2026-11-23, while the player was on a chapter 1 map, the game showed or
fetched **915 distinct labels**. The mod spoke **407** of them (2 have no words to match). Of the **506** it never
spoke, 4 are dialogue and none is field dialogue. The rest are menu help and descriptions (325), names in lists the
cursor may never have reached (89), quest, shop and other menu text, and two narration files that arrive as
`MsgWindowShowLine` kind 2, which `DialogueLines` skips. Gallica's report lines (the corner) and the crowd chatter were never spoken in any session.
They aren't in the log's records, so they are listed from the files. The ranked list follows.

## Evidence

### Chapter 1's message files

From `EN/message/` in `base.cpk`. The event range is cut where Hulkenberg first speaks (`e03_018_001`), after the mine
boss and Strohl's bond (the [pilot inventory](../raw-sources/chapter1-interaction-inventory-2026-11-20.md)'s end).
That cut is `[NEEDS VERIFICATION]`. "Spoken" means a run of 6 words of the line, or the whole of a shorter one, found
in any session transcript.

| Category | Files | Lines | Spoken in a transcript |
|---|---|---|---|
| Cutscene dialogue: `event/e00_*` to `e03_017_*`, `battle/event/BE_0551`, `0562`, `0573`, `0601` to `0603` | 60 | 956 | 67 (most scenes weren't replayed; sessions start from saves) |
| Field people, capital and inn: `field/npc_0001_*`, `npc_0011_*` | 190 | 3,634 | 25 |
| Crowd chatter ("Local Chatter"): `field/gaya_0001_*`, `gaya_0011_*` | 122 | 1,239 | 1 |
| Examine and blocked-way lines: `field/col_0001_*`, `col_0011_*`, `door_0001_*`, `ini_0001_*`, `ini_0011_*` | 22 | 390 | 2 |
| The study: `field/*_0016_*` | 4 | 3,937 | 319 |
| Desert, fort and mine: `field/dng_D00`–`D02*`, `dng_npc_D0*`, `*_0150_*`, `*D02_*` | 24 | 375 | 39 |
| Report (Gallica's corner): `report/D01_01`, `D02_01`, `Report_CTY01_KING`, `Field_Common`, `Support_Field_GALICA`, `T1N*` | 9 | 119 | 0 |
| Tutorials shown in the sessions (12 ids, `TutorialIDPC.bin`) | 2 | 24 pages | 21 |

Several files span the whole game (a map's `npc_` files cover every story date), so these are upper bounds.

### What the game showed on chapter 1 maps

A label counts as shown when a `MsgWindowShowLine` request's hash, a `MsgRequestByName` label or a
`GetMessageBySerial` resolve names it while the last `map strip` or `field place` map is a chapter 1 map. A resolve is a
fetch, not proof the text was on screen: shops and menus fetch every row.

| Group | Shown | Never spoken |
|---|---|---|
| Item and equipment descriptions (`ItemText_*`, `WeaponText_*`, `ArmorText_*`, `AccessoryText_*`) | 191 | 191 |
| Settings rows and help (`System_Option_*`, `System_tips_message_PC_*`, `System_subject_name_PC_*`, keybinds) | 199 | 102 |
| Item and equipment names (list rows) | 193 | 73 |
| Skill names and descriptions (`Skill_Message_1`, `_2`) | 48 | 30 |
| Other system text: quest list, shop, stats, guide, difficulty, travel, menu prompts | 131 | 80 |
| Main menu help line (`System_EasyMenu_*`) | 9 | 8 |
| Character, Archetype and quest names | 47 | 13 |
| Narration and day count (`ui/Narration_Happening.msg`, `ui/DailyMessage.msg`) | 5 | 5 |
| Cutscene dialogue | 42 | 4 |
| Field dialogue | 50 | 0 |

Battle, for the battle work: 138 `BattleMessageShow kind=0` records logged `said=no` (move banners, #739/#750), and
the 12 tutorials shown were spoken except pages 2 to 4 of tutorial 24, "Physical Status Ailments" (perhaps not paged).

### Ranked: text the player sees that the mod never spoke

Ranked by how much a blind player loses, then by how often it was shown (times shown / sessions). Paths are where the
mod already sees the text, or the finding that names the hook.

| # | Category | File: labels | Seen | Likely hook path |
|---|---|---|---|---|
| 1 | Notice | `ui/DailyMessage.msg`: `Daily_Message_00_7_00` ("Until arrival at the Northern Border Fort: 1 day remains"), `_00_8` | 4 / 4 | `MsgWindowShowLine` kind 2, already logged; `DialogueLines` speaks kinds 0 and 1 only (`src/Metaphor.Features.Dialogue/DialogueLines.cs`) |
| 2 | Notice | `ui/Narration_Happening.msg`: `MES_Narration_Arr_Gra_Fort_00` to `_02` (arrival narration) | 4 / 4 | Same kind 2 path, through `MsgRequestByNameTwin` |
| 3 | Report/corner | `report/D01_01.msg`: `MES_D00_01_r01_*`, `r02_*` (desert and fort hints) | not logged; 0 / 30 spoken | Report-open hook (#587, #896, [note](corner-advice-report-message-open-static.md)), built; ear pending |
| 4 | Report/corner | `report/D02_01.msg` (the mine), 30 lines | not logged; 0 spoken | Same |
| 5 | Report/corner | `report/Report_CTY01_KING.msg`, `Field_Common.msg`, `Support_Field_GALICA.msg` | compiled in sessions; 0 / 35 spoken | Same |
| 6 | Menu help | `system/System_Message.msg`: `System_EasyMenu_08` to `_16` (main menu help line: "Use an item", "Save current state") | 75 / 10 | `GetMessageBySerial` resolve, already logged; speak after the row ([submenu note](system-submenu-and-shop-menu-names.md)) |
| 7 | Menu help | `MainMenu_Item_01`, `MainMenu_Equip_01`, `MainMenu_Skill_01`, `MainMenu_Party_18` (prompts: "Which item?", "Change whose gear?") | 1–4 / 1–4 | Same resolve; speak as the sub-screen's title |
| 8 | Menu help | `System_Option_29` to `_113`, `System_tips_message_PC_04` to `_72` (settings help) | up to 89 / 8 | Settings screens ([inventory](settings-screens-inventory-static.md)); #560 |
| 9 | Menu help | `system/Item_Message_2.msg`: `ItemText_Consumable_*`, `ItemText_CASH_*`, `ItemText_Scenario_*` | 29 / 5 | Item focus and shop rows ([resolves note](item-equipment-menu-names-from-resolves.md)); #216, #197 |
| 10 | Menu help | `system/Equip_Message_2.msg`: `WeaponText_*`, `ArmorText_*`, `AccessoryText_*` | 45 / 4 | Equipment focus; #217 |
| 11 | Menu help | `system/Skill_Message_2.msg`: `SkillText_*` ("Uses equipped igniter to deal weak magic fire damage…") | 3 / 1 | Skill list; #165 |
| 12 | Notice | `System_QUEST_GET` ("You have reportable quests") | 57 / 10 | Resolve logged; on screen `[NEEDS VERIFICATION]` |
| 13 | Menu | `System_Quest_list_*`, `System_QUEST_*` (quest list columns: "In progress", "Days left", "No deadline") | 14 / 2 | Quest list; #194 |
| 14 | Menu | `Shop_Other_32`, `_33` (shop greetings), `Shop_Other_17` (ATK) | 42 / 4 | Shop menu; #197 |
| 15 | Menu help | `System_mode_03`, `_04`, `_11` (difficulty descriptions, "Select difficulty") | 8 / 6 | Difficulty list ([note](difficulty-select-own-input-static.md)) |
| 16 | Menu | `System_General_01`, `_03`, `_04` (ATK, DEF, EVA comparisons) | 81 / 5 | Equipment and shop compare; #217 |
| 17 | Menu | `System_travel_map_00` to `_02` ("DAY: 1" to "3"), `System_Weather_02` | 13 / 4 | Travel map; #545, #192 |
| 18 | Menu help | `System_Battle_REWIND` ("Retry the battle from the beginning") | 16 / 7 | Retry prompt ([note](battle-escape-pass-retry-signals-logged.md)) |
| 19 | Menu | `System_Guide_110` ("Unpause"), `System_Guide_94` | 17 / 7 | Button guide bar `[NEEDS VERIFICATION]` |
| 20 | Dialogue | `event/e00_002_001.msg`: `MES_e00_002_001_008`, `_009` (opening, More) | 3 / 1 | `MsgWindowShowLine` kind 0; probably advanced past before speech `[NEEDS VERIFICATION]` |
| 21 | Dialogue | `event/e01_004_001.msg`: `MES_e01_004_001_000`, `_001` (desert, Gallica) | 1 / 1 | Same |
| 22 | Dialogue | Ambient crowd chatter, `field/gaya_0001_*`, `gaya_0011_*` | not logged; 1 / 1,239 spoken | Triggered by `GAYATBL` volumes (`collision/f_<map>.ghttbl`); the display path is unknown `[NEEDS VERIFICATION]` |
| 23 | Dialogue | Gallica's town remarks, `field/fld_GAllicA_Town.msg` (0 / 271), `fld_GAllicA_Scenario.msg` (1 / 78) | not logged | Unknown; not `MsgWindowShowLine` in any session `[NEEDS VERIFICATION]` |
| 24 | Menu | `ATCategoryName_06`, `_12`, `_17` (Archetype categories) | 22 / 2 | Archetype screens; #219, #220 |
| 25 | Notice | Loot banner, `system/jyokyo_help_Message.msg`: `jyokyo_helpText_GET_ITEM` | layout, not logged | #542 ([item-get note](item-get-banner-two-routes.md)) |
| 26 | Notice | Autosave, `System_Save_11` | layout | #543, built in #896 ([note](autosave-notice-is-a-layout-animation.md)) |
| 27 | Battle | Move banners, `BattleMessageShow kind=0` (138 records `said=no`) | 138 records | #739, #750 ([note](battle-move-banner-text-reference-static.md)) |
| 28 | Tutorial | Tutorial 24 pages 2 to 4 (`Battle_Tutorial_Title_14`) | 1 / 1 | Tutorial pop-up paging (#118); maybe not paged |

Not silent: field dialogue in the fort and desert (`dng_D00_01` 8/10, `dng_D01_01` 27/54 spoken), the study, the
recruitment questionnaire's choices, 11 of the 12 tutorials, and the Strohl ally notice.

## Steps

.NET SDK 9.0.319, Python 3.12.10, 2026-11-24, game found by `Find-MetaphorGame`, SHA-256 checked.

1. `dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- list base.cpk`, then `extract` of every
   `EN/message/` path (2,674 files) and `COMMON/ui/tutorial/TutorialIDPC.bin`, 250 paths per call, into
   `extracted/audit-ch1`.
2. From each session's `message-log.txt`, `grep -E "ms (request via|resolve via|tutorial open|map strip|field place)"`
   into a scratch folder outside the repository, next to a copy of its `transcript.txt`.
3. A throwaway Python script parsed every `@label { … }` block, hashed each label with MD5, mapped the log's hashes,
   names and resolves to labels, tracked the current map, and matched each label's text (tags stripped, lower case,
   letters and digits only) against the transcripts as above. A second pass did the same per file for the tables.
4. Tutorials: entry `id` of `TutorialIDPC.bin` (title hash `+0x4`, page hashes at `+0x14 + n × 0x32`, as in the
   [tutorial note](tutorial-table-maps-numbers-to-labels.md)) against `System_Tutorial_1.msg` and `_2.msg`.
5. Nothing from the transcripts is quoted here but game text. No personal data was read beyond grep matches.
