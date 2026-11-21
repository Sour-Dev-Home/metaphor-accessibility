# The settings screens are five pages, four on the shared cursor and key configuration on the grid cursor

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory (game session 1's existing log, re-read)
- **Sources:** [`game-session-1-2026-11-20.md`](../raw-sources/game-session-1-2026-11-20.md),
  [`chapter1-opening-inventory-own-capture-2026-11-20.md`](../raw-sources/chapter1-opening-inventory-own-capture-2026-11-20.md),
  [`screen-class-session-2026-11-17.md`](../raw-sources/screen-class-session-2026-11-17.md),
  [`ui-focus-candidates-2026-11-16.md`](../raw-sources/ui-focus-candidates-2026-11-16.md),
  [`menu-hook-counts-2026-11-17.md`](../raw-sources/menu-hook-counts-2026-11-17.md),
  [`settings-pages-resolves-by-ear-2026-11-20.md`](../raw-sources/settings-pages-resolves-by-ear-2026-11-20.md)

Static prep for #96, as #492 did for the title. Settings opens on a five-row category list, and each row opens a page
with its own screen class. The System, Sound, Graphics and Display pages use the shared cursor class `0x22045e8`. Key
Configuration uses `MultiLineCursorManager` (`0x22046a8`), the grid cursor Calendar uses. Row labels, values and help
lines are message entries in `System_Message.msg` and `System_Message_PC.msg`. Key bindings are drawn as images,
one per Windows virtual-key code, so a binding has no text to read. Anything not settled by the log or the code is
marked `[NEEDS VERIFICATION]`. [In-game checks](#in-game-checks) lists what settles it.

## Evidence

### Screens

Class names are RTTI names with `@ope@ui@app` dropped. Each vtable was found from its type descriptor and its complete
object locator. Row counts and cursor classes come from game session 1's `cursor` records (`count=`, `class=`), re-read
for this note. Every settings record there has class `0x22045e8`, except Key Configuration's 461, which have
`0x22046a8`.

| Screen | Class (vtable) | Cursor | Rows |
|---|---|---|---|
| Category list | `ConfigTopChoicePc<ConfigTopChoicePcLayoutInit>` (`0x1825cb0`), `…<ConfigTopChoicePcLayout>` (`0x18253e0`) | `0x22045e8` | 5 |
| System | `ConfigMenuChoicePcT<0x76>` (`0x1822a60`) | `0x22045e8` | 20 from the title, 24 in play |
| Sound | `ConfigMenuChoicePcT<0x77>` (`0x1822e20`) | `0x22045e8` | 6 |
| Graphics | `ConfigMenuChoicePcT<0x78>` (`0x18231e0`) | `0x22045e8` | 7 |
| Display | `ConfigMenuChoicePcT<0x79>` (`0x18235a0`) | `0x22045e8` | 4 |
| Key Configuration | `ConfigMenuChoiceKeyConfig` (`0x1820e28`) | `0x22046a8` | 23, 14, 12 and 3 |
| Text language list | owner unreadable (`screen=?`), likely `CampConfigLanguageSelectLayout` (`0x218e980`) `[NEEDS VERIFICATION]` | `0x22045e8` | 12 |
| Confirm dialogs | `YesNoChoiceSample` (`0x21c4918`) | `0x22045e8` | 2 |
| Screen brightness | `ConfigBrightnessChoice` (`0x21d5668`); `TitleConfigBrightnessChoice` (`0x21d5968`) at first launch | `[NEEDS VERIFICATION]` | none logged |
| Audio output device list | `CampConfigAudioSelectLayout` (`0x218eb70`), `UUIC_Common_CampAudioSelectPc` (`0x2250c80`) | `[NEEDS VERIFICATION]` | none logged |

- **Which page is which.** Each page's rows match the labels the game resolved while it was open (see
  [Labels](#labels-values-and-help-lines)). Sound's 6 match its six rows: four volumes, Audio Output and Voice
  Selection. The defaults table at `0x181e4b0` gives a page for each setting the PC version adds (below). Its page 4 has
  7 settings and page 3 has 4, which fits Graphics and Display.
- **The template argument names the page, not the entry point.** `0x76` to `0x79` are the four pages.
  `screen-class-session-2026-11-17.md` read `<0x76>` against `<0x77>` and `LayoutInit` against `Layout` as title against
  in-play. In session 1, Settings opened from the field's quick menu (`InstantMenuChoice`, 2366 s) is
  `ConfigTopChoicePc<…LayoutInit>` again, so `LayoutInit` against `Layout` follows the menu Settings was opened from,
  not title against play `[NEEDS VERIFICATION]`.
- **The page hierarchy.** `ConfigMenuChoicePcT<N>` derives from
  `ConfigMenuBaseChoice<CampConfigLayoutPc<ConfigPcConfigMisc>>`, which derives from `ConfigMenuChoice`.
  `ConfigMenuChoiceKeyConfig` derives from `SingleChoiceBase_WithLayout<CampConfigLayoutPc<ConfgPcConfigKey>>`. Both
  keep the shared cursor getter `ChoiceGetCursor` (`0xd4ab90`) in slot 11, so the screen-class link that names the
  other menus' screens names these too. The log confirms it: every record has a `screen=`.
- **The 12-row unnamed list** opened twice from the System page in play (2374 s and 2378 s), with a confirm dialog
  after it. The capture's row 7 shows a 12-language list. Its owner's RTTI wasn't readable.

### Labels, values and help lines

The message-file table at `0x27b0cd0` (`0xd0`-byte entries; [`title-names-voice-tutorials-static.md`](title-names-voice-tutorials-static.md)
section 5) gives each file a serial test:

- `message/system/System_Message.msg` (entry 5) holds serials 1142–1931. Its test is at `0x9f4c90`.
- `message/system/System_Message_PC.msg` (entry 47) holds serials 11453–11806. Its test is at `0x9f4c60`.

The English copies, read from `base.cpk`, hold 790 and 354 entries, exactly those spans, so serial = first serial + the
entry's position in the file. While Settings was open, session 1 logged `resolve via=GetMessageBySerial` for serials
1636–1736 and 11469–11806. Those are these groups of labels:

| What | Labels (`System_Message_PC.msg` unless noted) | Serials |
|---|---|---|
| Category names (5 used, a sixth unused) | `System_category_name_PC_01`–`_06` | 11469–11474 |
| Row names | `System_subject_name_PC_*`; console rows in `System_Message.msg` `System_Option_14`–`_28`, `_71`–`_74`, `_89`, `_91`, `_93`, `_103`, `_112` | 11633–11689, 11761–11765, 11788–11800; 1636–1651, 1695–1698, 1713, 1715, 1717, 1726, 1735 |
| Section headings, key tabs and sub-tabs | `System_mid_subject_name_key_PC_*` (Image Quality, Language, Difficulty, General System, Camera, Volume, …; Keyboard, Mouse, Controller; Common, Event, Field, Dungeon, Battle) | 11604–11618, 11745–11747, 11797–11799 |
| Value words | `System_select_name_PC_01`–`_19` (Normal, Fast, Low to High, Off, On, Fullscreen, Type 1–4, …); `System_Message.msg` `System_Option_45`/`_46` (on, off; not among the resolved serials), `_82`–`_84` | 11619–11632, 11754–11758; 1669–1670, 1706–1708 |
| Help lines | `System_tips_message_PC_*`; `System_Message.msg` `System_Option_29`–`_44`, `_75`–`_81`, `_90`, `_92`, `_94`, `_104`, `_113` | 11690–11741, 11766–11796; 1652–1668, 1699–1705, 1714, 1716, 1718, 1727, 1736 |
| Key actions | `System_Keybind_name_PC_01`–`_99` | 11477–11574, 11759 |
| Dialogs and key prompts | `System_button_message_PC_*`, `System_message_PC_*` | 11453–11468, 11575–11603, 11748–11753, 11784–11787, 11805–11806 |

- **Labels are text, not images.** Every page label, value word and help line has a message entry. Which entry each row
  uses is set by serial, not by label: the exe names only 9 of the PC labels and 23 `System_Option_*` labels as
  strings.
  Most likely they come from the layout's `ConfigInfoItem` array (`StaticArray<ConfigInfoItem@Config@layout,0x33>`, 51
  items) `[NEEDS VERIFICATION]`.
- **Resolved when a page is built, not on each move.** The resolves cluster where pages open. A focus hook can't wait
  for a resolve to learn the focused row's label; it needs the row-to-entry map (above), or the drawn row text from
  `ui-text-panes` `[NEEDS VERIFICATION]`.
- **Not every row resolves when a page opens.** In the by-ear run
  ([`settings-pages-resolves-by-ear-2026-11-20.md`](../raw-sources/settings-pages-resolves-by-ear-2026-11-20.md)),
  System (20 rows) resolves 14 serials as it opens, mostly a row name then a help line. Sound (6 rows) resolves 13.
  Each page resolves the same set again on one later move. So the order of a page's resolves can't give its row
  labels the way a plain list's can (#584); the row-to-entry map is still needed.
- **Where values are stored.** A page keeps a working copy of every value: a dword per setting ID in a container at
  `choice+0x4c8`, with its length at `+0x4d0`. `ConfigMenuChoicePcT<0x76>::vf93` (`0x805de0`) fills it from the
  globals at `base+0x2ee8d30` on. It reads `base+0x2ee8b80`, a map from setting ID to an entry whose value is at
  `+0x14`. `vf72` (`0x804a60`) resets IDs `0x2b` and `0x1d` to their defaults.
- **Value kinds.** `ConfigMenuChoicePcT<0x76>::vf85` (`0x805870`) returns how many choices a setting ID has:
  - `0x1d`: 5 (Auto-detect and Type 1–4 fit);
  - `0x26`: 4;
  - `0x27` and `0x2a`: 7 (the seven render scales fit one of them);
  - `0x2d` and `0x31`: 3 (Fullscreen, Borderless, Windowed fit `0x2d`);
  - `0x2c` and `0x2e`: counted at run time (resolutions and monitors);
  - any other ID: a shared count (`0xaf35d90`), likely 2 for on and off `[NEEDS VERIFICATION]`.

  So a value is an index into a list of message words. The volumes and the two sensitivities have defaults of 50 and
  100 in the defaults table, so they look like numeric sliders. Whether the screen shows their number is
  `[NEEDS VERIFICATION]`.
- **The defaults table** (`0x181e4b0`, 22 entries of `{id, default, page}`, read by `0x7e6f70`) covers the PC settings,
  IDs 27–49:
  - page 1: 27, 28, 29, 31, 32, 33, 34, 43;
  - page 2: 35, 36, 37;
  - page 3: 44, 45, 46, 47;
  - page 4: 38–42, 48, 49.

  The console settings (IDs below 27) take their defaults elsewhere. Which ID is which row is `[NEEDS VERIFICATION]`.
- **Help line.** The capture shows a one-line help text under the focused row. It is the `tips` entry for the row:
  the description #96 assigns to the details key `[NEEDS VERIFICATION]`.

### Key Configuration (remapping)

- **Layout.** The capture shows tabs for Keyboard, Mouse and Controller. Each has sub-tabs (Common, Event, Field,
  Dungeon, Battle) and a two-column grid of action and key. The type
  `StaticArray<StaticArray<KeyConfigListInfo,2>,0x14>` (20 rows of 2) fits two binding columns per action. The layout
  strings `keyconfig_list_1` to `_12` suggest 12 visible rows `[NEEDS VERIFICATION]`.
- **The cursor is the grid one.** `MultiLineCursorManager` (`0x22046a8`) logs `index`, `top` and `row` like the shared
  cursor (cmds `0x1` and `0x14`–`0x17`). Counts 23, 14 and 12 look like the sub-tabs' action counts; count 3 is a short
  list, maybe the device tabs `[NEEDS VERIFICATION]`. In the by-ear run one press of the pad button that steps the pages
  down a row (`0x0040`) moved `index` by 2 (0 to 2,
  then 2 to 4; [`settings-pages-resolves-by-ear-2026-11-20.md`](../raw-sources/settings-pages-resolves-by-ear-2026-11-20.md)),
  which fits `index` counting cells, two to a row `[NEEDS VERIFICATION]`. Where the column is kept is
  `[NEEDS VERIFICATION]`.
- **Bindings are images.** The exe names 103 key images, `keyboard/key_<n>_wh` and `_bk`. Each `<n>` is a Windows
  virtual-key code: 1, 2, 4, 5 and 6 are the mouse buttons, 8 Backspace, 9 Tab, 32–40 Space to the arrows, 48–57 the
  digits, 65–90 the letters, 96–111 the keypad, 112–123 F1–F12, 160–165 Shift, Ctrl and Alt, and 186–226 the
  punctuation keys. So a keyboard or mouse binding has no message text. The mod has to name the key from its code (as
  Windows' `GetKeyNameText` does, for the player's layout). Controller bindings presumably use the pad icons
  (`PAD_ICON` tags and `_btn_*` images) `[NEEDS VERIFICATION]`. Where the binding table lives in memory is
  `[NEEDS VERIFICATION]`.
- **The capture state** is a prompt, not a page. One message per device asks for the new input:
  - `System_message_PC_20`/`_21` (keyboard, with Esc or with Cancel), `_38` (mouse) and `_39` (controller);
  - `_25`–`_27`, a shorter "input the key / mouse button / button" set;
  - after the input: `_28`–`_30` (input can't be used), `_11`, `_16` and `_40`–`_44` (the input is bound elsewhere,
    overwrite?; `_40`–`_44` name up to five clashing actions through `<V n>`), and `_18` (the earlier binding was
    erased);
  - on leaving: `_22` (some keys unassigned) and `_19` (restore the defaults);
  - help lines: `System_tips_message_PC_46`, `_50` and `_51`.

  The exe names `_22` and `_40`–`_44` as strings, so it builds those itself. `ConfigMenuChoiceKeyConfig::vf65`
  (`0x7feca0`) is `_DecideNextState_InterruptChoiceEnd`: it reads an interrupt dialog's return data (`ID`,
  `IsCancel`). `vf41` (`0x7fb690`) branches on `choice+0x908` (a byte) and `choice+0x914` (1 is a mode). Which field
  marks "waiting for a key" is `[NEEDS VERIFICATION]`.

### Confirm dialogs

Session 1 logged `YesNoChoiceSample` (2 rows) after leaving Display (157.9 s) and Graphics (541.1 s), and after the
language list (2389.7 s). Candidate texts:

- leaving a page: `System_button_message_PC_05` and `_10` (save settings?), `System_message_PC_17` (return without
  saving?), `System_Message.msg` `System_Option_63`;
- Restore Defaults: `System_button_message_PC_08`, `System_tips_message_PC_74`, `System_Option_64`;
- a display change: `System_tips_message_PC_47`, a yes-or-revert with a `<V 0>`-second countdown;
- text language: `System_Option_99`–`_101` and `_105`–`_109` in `System_Message.msg`, which warn that names change;
- brightness: `System_Option_110` and `_111`.

Which dialog is which is `[NEEDS VERIFICATION]`. The exe names `System_button_message_PC_05`,
`System_tips_message_PC_47` and `_74`, and `System_Option_99`, `_101` and `_105`–`_111` as strings.

## In-game checks

1. **System page**, from the title and from the field: the four rows only in play, and whether the section headings
   (Language, Difficulty, General System, Camera) are cursor rows. With `ui-text-panes`, note whether the focused
   row's drawn text is readable.
2. **Rows and setting IDs:** inspector `watch <choice>+0x4c8`'s data while changing one value on each page; the
   dword that changes gives the row's ID.
3. **Value widgets:** a slider shows a number or only a bar; an on/off row is two words.
4. **Help line:** it changes with focus and matches the row's `tips` entry.
5. **Sub-screens:** Screen Brightness, Audio Output and Voice Selection open their own screens. Note the `cursor`
   records (class, count, `screen=`) and whether each confirms with a dialog.
6. **Dialogs:** leave each page changed and unchanged, press Restore Defaults, change Resolution. Note which dialog
   shows.
7. **Key Configuration:** switch device tab and sub-tab with the keys the screen shows and note the `cursor` records
   (does a tab switch make a new manager or only a new count?). Move across a row: does `index` step by 1 or by 2,
   and is the column logged? Rebind one key: note the prompt and what the log shows while waiting. Bind a key used
   elsewhere for the overwrite dialog. Then restore the defaults.

## Steps

1. RTTI names: `grep -aoE` for `.?AV…Config…` and related names over `METAPHOR.exe` (game build above).
2. Vtables: a Python 3.12 script (standard library only), run outside the repository. It finds each type descriptor's
   complete object locators and the vtable that points at each.
3. Ghidra 12.2, the shared project ([`query-the-ghidra-project.md`](../contributors/how-to/query-the-ghidra-project.md)):
   - `vtable 0x1822a60`;
   - `vtable app::ui::ope::ConfigMenuChoiceKeyConfig`;
   - `decompile` on `0x804a60`, `0x7e6f70`, `0x805de0`, `0x805870`, `0x7fb690`, `0x7feca0` and `0x7fffb0`.

   A class name with `<…>` fails as a target (the batch file reads `<` as a redirect), so pass the vtable address.
4. The message-file table: read `0x27b0cd0` from the exe and decode each serial test (`sub`/`lea`, `cmp`, `setbe`).
5. Message files: `EN/message/system/System_Message.msg` and `System_Message_PC.msg`, read from `base.cpk` by its
   `@UTF` table of contents (neither is compressed) with the same throwaway script. They were kept in a scratch folder
   outside the repository and not committed (rule 4). Entries were counted by `@label` lines.
6. Session 1's main-run `message-log.txt` (outside the repository): `cursor` records grouped by `screen=`, `class=` and
   `count=`, and the `resolve` serials grouped by the screen last logged before them.
