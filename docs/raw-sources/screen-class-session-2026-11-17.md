# Menu screen classes in the game: the forward link confirmed (issue #213)

- **Source:** one run on the maintainer's machine through Reloaded-II 1.31.0, Debug mod build `b6c4f3f`
  (`hooks 20/20`: the 17 base hooks, the two dialogue window hooks, and `ChoiceCursorGetterListener` installed at
  module offset `0xd4ab90`), with `opt-in-screen-class.txt` and `opt-in-dialogue-window.txt`, Steam Input on, recorded
  with OBS and narrated. The message log, transcript and recording hold game text and stay local; only class names,
  counts and times are kept. The mod started 91 s before the recording: video time = log time − 91 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `pwsh scripts/listen.ps1 -Seconds 30 -Close` from branch `211-menu-screens` (`hooks 17/17`, survived), then the two
   opt-in files, then a launch from Reloaded-II.
2. The maintainer visited the title menu and its System submenu, loaded a save, and walked the main menu, Skill, Item,
   Equipment, Party, Status, Tactics, Follower, Quest, Calendar, Memorandum, System and the archetype screens; then
   pressed candidate details-key buttons and keys.
3. The `screen` records were grouped by owner class; times were matched to the narration.

## Results

**The forward link works.** 948 cursor inputs logged an owner; 926 named its class through RTTI, 22 had none. Each
screen's choice object holds its cursor manager, and the shared getter (`0xd4ab90`) returns it, so the owner's class
names the screen on every input. A screen's manager can change within a visit (up to 6 per class), so the class, not
the manager, identifies the screen. Every manager's own class was `CursorManager@ope@ui@app`.

| Screen | Owner class (RTTI, `@ope@ui@app` omitted) | Inputs |
|---|---|---|
| Title menu | `TitleChoice` | 31 |
| Title System submenu (video 0:31) | `ConfigTopChoicePc<ConfigTopChoicePcLayoutInit>`, then `ConfigMenuChoicePcT<0x76>` | 12 |
| Main menu | `RootMenuChoice` | 78 |
| Skill: characters, skills | `SkillCharaSelChoice`, `SkillSelectChoice` | 7, 16 |
| Item: list, target | `ItemMenuChoice`, `ItemCharaSelChoice` | 47, 24 |
| Equipment: characters, slots, items, swap | `EquipCharaSelChoice`, `EquipPartsChoice`, `EquipMenuItemList`, `EquipSwapCharaSelChoice` | 27, 7, 5, 1 |
| Party and Status | `StatusCharaSelChoice` | 12 |
| Tactics | `TacticalChangeChoice` | 24 |
| Follower: list, detail, abilities | `CooperationMenuChoice`, `CooperationDetailChoice`, `CooperationSkillMenuChoice` | 19, 1, 31 |
| Quest: list, detail | `CampQuestChoice`, `CampQuestDetailChoice` | 10, 1 |
| Calendar | `CalendarChoice` | 79 |
| Memorandum: list, entry | `GlossaryListChoice`, `GlossaryChoice` | 101, 2 |
| System: menu, tutorials, story | `SystemMenuChoice`, `SystemTutorialChoice`, `SystemStoryChoice` | 49, 32, 9 |
| Settings (in play) | `ConfigTopChoicePc<ConfigTopChoicePcLayout>`, `ConfigMenuChoicePcT<0x77>` | 56, 139 |
| Akademeia menu, likely (video 8:17–8:34, during the voice-volume test in a conversation) `[NEEDS VERIFICATION]` | `AcademiaTopChoice` | 10 |
| Dialogs | `YesNoChoiceSample`, `SingleChoiceBase`, `CommonWarningChoiceWithLayout` | 24, 1, 2 |

- **Not named:** 69 inputs whose owner's RTTI wasn't readable (`?`) and 22 with no owner, at video 0:49 (loading a
  save), 2:39–3:09 (archetype details from Equipment, and party change from Equipment) and 6:00–6:10 (System). Archetype
  details was expected here: its tabbed cursor getter (`0xd976f0`) matches three times on disk and isn't hooked
  (`docs/raw-sources/screen-class-link-2026-11-17.md`). `[NEEDS VERIFICATION]`: which of these is archetype details.
- **Not visited:** the Archetype Tree (Y on the main menu); the narration doesn't mention it.
- **The title and in-play settings differ** only in their template argument (`LayoutInit` vs `Layout`, `0x76` vs
  `0x77`), so the title's System submenu can be told apart from Settings opened in play.
- **#208 again:** the narration has "Main menu, New Game, 1 of 3" spoken in the Memorandum during play.

### Details-key candidates (narration)

- Pressing L3 or R3, the bumpers, the triggers, or both bumpers or both triggers together on the main menu did nothing.
  X did nothing in menus (the dialogue window hooks were on).
- **F1** stopped keyboard input; not usable. F2: unclear. Tab, Q and E: nothing heard on the main menu. Tabbed screens
  use the bumpers and triggers, so those are taken there.

### Also seen

- With the game's voice volume at 0, voiced lines were inaudible while the mod spoke only the speaker's name (#57,
  #103). Where the volume lives wasn't found in this run.
- Steam Input was on all session: 1 234 `pad0` and 132 `pad1` input records, plus 103 keyboard (#149).
