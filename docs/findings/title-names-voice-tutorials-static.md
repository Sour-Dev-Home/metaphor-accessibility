# The title's submenu, speaker names, voice language and tutorials have static leads for session 1

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`title-system-submenu-session5-2026-11-17.md`](../raw-sources/title-system-submenu-session5-2026-11-17.md),
  [`list-count-session6-2026-11-17.md`](../raw-sources/list-count-session6-2026-11-17.md),
  [`name-lookup-candidates-2026-11-16.md`](../raw-sources/name-lookup-candidates-2026-11-16.md),
  [`voice-cues-session5-2026-11-17.md`](../raw-sources/voice-cues-session5-2026-11-17.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - ChoiceGetCursor | 0xd4ab90 | .ecode | Menus | hooked | Returns the cursor manager of a choice, used for the title's submenu.
  - fldNpc::vf34 | 0x679880 | .ecode | Field | known only | A caller that resolves the field's name labels.
  - fldMember::vf3 | 0x674150 | .ecode | Field | known only | A caller that resolves the field's name labels.
  - resrcInit loader | 0x8994a0 | .ecode | Field | known only | Fills the CharaID and NpcID tables from `COMMON/init/data`.

Static prep (#492) for game session 1 (#496), read in Ghidra and the exe's bytes only, not yet run against the game.
Every behaviour below is `[NEEDS VERIFICATION]` until the session's reads agree. The batch files for each stop are in
[`docs/sessions/session-1/`](../sessions/session-1/); `docs/findings/` holds only notes.

| Question | Best lead | Confidence |
|---|---|---|
| 1. #210 submenu | `TitleChoice+0x4a8`: 1 on the title menu, 2 in the System submenu | High in code; reaching the object is a lead |
| 2. #74 name table | `CharaID.bin` at `[[base+0x2eebc18]+0x40]+0x10`, `0x6c`-byte entries, flags at `base+0x2cc934c` | High in code |
| 3. #106 player name | No static lead; a sweep of the game-data block | Low |
| 3. #106 `65535` | `0xffff` means "no entry" in the ID tables that lead to `CharaID.bin` | Medium |
| 4. #103 voice language | `base+0x27a8c78` (as set) and `base+0x2dff8e4` (as applied) | High for where; values unknown |
| 5. #118 tutorials | `System_Tutorial_1.msg` and `_2.msg`, serials `0x2810`–`0x2a6b` | Medium |

## Evidence

### 1. The title menu and its System submenu share `TitleChoice`, which holds a mode (#210)

`TitleChoice@ope@ui@app` (vtables `0x21f16b8` and `0x21f1900`, `0x4f8` bytes by its deleting destructor `vf2`,
`0xe2dc80`) holds two item lists. The destructor names their types: `+0x3e0` is an array of
`TitleMenu_InfoItem::ListInfoItem` and `+0x448` an array of `TitleSystemMenu_InfoItem::ListInfoItem`. `vf17` (`0xe2ec80`)
fills the first with 3 items and the second with 2.

Two UI-event lambdas registered in `vf17` read and set `+0x4a8`:

- `0xe2d200` (the cursor handler) returns at once when `+0x4a8` is 0. With 1 it stores the cursor's index in `+0x4ac`
  for any row; with 2 it stores it only below 2, so the list has two rows.
- `0xe2c620` (the decide and back handler) sets `+0x4a8` to 2 at `0xe2cd46` and hands the `+0x448` list to the layout
  at `+0x3d8` with the cursor at 0 (`0xe2cd53`). On back in mode 2 it sets `+0x4ac` to 2, `+0x4a8` to 1, and hands
  back the `+0x3e0` list. That matches session 5: the submenu opens on index 0 and back lands on index 2.

So `+0x4a8` reads 1 on the title menu and 2 in the submenu; 0 is before the menu takes input, by the early return.
Confidence: high for the code; the live values are `[NEEDS VERIFICATION]`.

**Reaching it.** Nothing in analysed code writes the `TitleChoice` vtables: its constructor isn't in the analysed code,
and it has no static pointer. The title scene, `fclTitle@fcl@app`, is key `0x17` in the menus' scene map: `0xf9c210`
walks it with `0x51fab0` (`H+0x48`, `H` at `0x2ee7338`) and `0x4ccde0`, the walk in the
[tree note](archetype-tree-scene-and-choice-static.md), and casts to `fclTitle` (game session 1 found the batch's chain one
dereference short; the corrected path is in [the scene-map path](menus-scene-map-path.md)). It then returns the scene's byte
`+0x81`, whose meaning is unknown. Whether the scene holds the choice, directly or deeper, is `[NEEDS VERIFICATION]`.
Once a candidate is found, `read <choice>+0x230 8` must equal the `mgr=` of the log's title `cursor` records: `+0x230` is
the manager that `ChoiceGetCursor` (`0xd4ab90`) returns.

Simpler and needing no choice: every `cursor` record now logs `count=`, `CursorManager+0x58` (#289). Session 6 logged 3
for `TitleChoice`. If the submenu logs 2, the count alone tells the two apart. Whether handing over the `+0x448` list
resets the manager's count happens in `.text1` (`0xdeaac40`'s target) and wasn't read. `[NEEDS VERIFICATION]`

### 2. The speaker-name table is `CharaID.bin`, behind a static pointer (#74)

`0x9e7ca0` jumps into `.text1`, but the `.ecode` function `0x9e8820` repeats its walk in readable code, and so does
`0xa01bc0`:

- `0x898df0` returns `[0x2eebc18]+0x40`; call it `C`. Entry `n` (up to 100) is at `C + 0x10 + n × 0x6c`.
- An entry's first 16 bytes are its default key. Five slots of `0x12` bytes follow at `+0x10 + i × 0x12`: a 16-byte key,
  then a 16-bit flag number at slot `+0x10` (entry `+0x20 + i × 0x12`), `0xffff` for none.
- The first slot whose flag number isn't `0xffff` and whose flag is set wins (`0x3b0ab0`); with none, the default key.
- Flag `f` is bit `f & 31` of the dword at `G + 0x22c5c + (f >> 5) × 4`, with `G` the pointer at `0x272e310`
  (`base+0x2ca66f0`, the [money note](money-follower-rank-and-shop-static.md)). So the flags start at `base+0x2cc934c`.

`resrcInit`'s loader (`0x8994a0`) fills `0x2eebc18` from `COMMON/init/data/CharaID.bin`, `0x2eebc38` from `NpcID.bin`,
and `0x2eebc98` from `FollowerTable.bin`. The keys of `CharaName_FO12_Mor` labels aren't in the exe (an MD5 search of
the file found none), consistent with a table loaded from data. Whether the table is loaded at the title is
`[NEEDS VERIFICATION]`.

Callers today: `fldNpc::vf34` (`0x679880`), `0x67a5f0` and `fldMember::vf3` (`0x674150`), the field's name labels.
That the dialogue box's speaker name goes through the same table is `[NEEDS VERIFICATION]`. The
`MessageSerialByHash` records the listener logs on the alias line settle it: each key should match one of the table's.

### 3. The player's name and `65535` (#106)

**Question 1.** No static lead. No script function returns the name; `FclPlayerNameEntryStart` (`0x9063f0`) jumps into
`.text1`, and `fclNameEntry@fcl@app`'s type descriptor (`0x2c04f50`) has no reference in analysed code. Two live
routes: the `create-msg-from-string` record's `at=`, where an offset from `module+0x2ca66f0` up to about `+0x25000`
(the highest field the money note reads) is inside `G`, and a sweep of `G` after the name is confirmed, searched offline for the typed name's bytes (ASCII and UTF-16LE).
Confidence: low that the name is in `G`.

**Question 2.** The ID tables use `0xffff` as "none": `NpcID.bin` (`[0x2eebc38]+0x40`, `0x20`-byte entries from `+0x10`,
up to `0x99d`) gives a `CharaID` index at `+4`, and `0x67a380` and `0x9e8820` treat `0xffff` there as "no CharaID
entry", then fall back. `FollowerTable.bin` (`0x1e`-byte entries from `+0x10`) gives a `CharaID` index at `+0`;
`0xa01bc0` follows it, and `0x9ff8d0` wraps it as a `datFollowerID` to `MSG_SERIAL_LABEL` function, the likely
`<FOLLOWER_NAME n>` handler. The `<CHAR_NAME n>` handler wasn't found: no analysed code tests its code `0xD946`
(`0x9e4f10` tests `0xd916` and `0xd924`). That `65535` in `<CHAR_NAME 65535>` means "none" is a lead by analogy.
Confidence: medium for the tables, low for the handler.

### 4. The voice language is in two static globals (#103 question 3)

The only analysed reference to the string `appChangeVoiceLanguage` (`0x181c290`) is `0x7bb010`. It first calls
`0x7bc6d0`, which stores its argument in `base+0x27a8c78`, then starts a task of that name (`0x7bab20`). On its last
step the task copies `0x7bbc40`'s value (that global) into `base+0x2dff8e4` (`0x3dcbb0`) and calls a listener.
`0x7bc6d0`'s other caller is `ConfigMenuBaseChoice<…ConfigPcConfigMisc>::vf92` (`0x807460`), the settings page that
sets it. `0x3dcc20` fills a settings block from `0x2dff8b0`, taking `+0x30` from `0x7bbc30` (`base+0x27a8c74`, likely the
text language) and `+0x34` from `0x27a8c78`.

In the file both globals hold 1. Which value means English and which Japanese, and whether the change needs a restart,
are `[NEEDS VERIFICATION]`. Confidence: high that `0x27a8c78` holds the voice language as set.

### 5. Tutorial pop-ups have their own message files (#118)

The message-file table (`0x27b0cd0`, `0xd0`-byte entries) names the files. Entry 58 is
`message/system/System_Tutorial_2.msg` (string `0x2123308`), whose serial test `0x9f4bf0` accepts `0x2810`–`0x28de`.
Entry 59 is `System_Tutorial_1.msg` (`0x2123330`), whose test `0x9f4c00` accepts `0x28df`–`0x2a6b`. Each test is a
subtract, compare and `setbe`, read as bytes. So a pop-up drawn from them should log `resolve via=GetMessageBySerial`
with a serial from 10256 to 10859.

The show function: the script function `TutorialWindowOpen` (`0x9128e0`) jumps into `.text1` (`0xa0a51f0`, not
followed); `TutorialWindowStart` (`0x912930`) returns at once and `IsTutorialWindowReady` (`0x90b2d0`) returns 1.
The window class is `UUIC_Common_TutorialWindow@ui@app` (vtable `0x22576a0`); a hook on it is a later spike. `COMMON/event/Tutorial.dat`
is loaded by `0x461020`. Confidence: medium that the pop-ups read these files; the show function is
`[NEEDS VERIFICATION]`.

## Tonight

Start with `-OptIn inspector,create-msg-from-string,ui-text-panes,dialogue-window` (#496 stop 0). At each stop, run
its batch file. A batch holds the inspector's one connection until it ends, so the follow-up commands come after it:

```powershell
pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-1/stop-2-title.txt -WaitKey -SaveLog <out>
```

`<out>` is a folder outside the repository.

Each `follow` prints an address; type the follow-ups with it. `<off>` below is the bucket offset for key `0x17`:
`0x78` when the mask `m` is under `0x10`, else `0x178`.

| Stop | Batch file | Follow-ups and the answer that confirms |
|---|---|---|
| 2, title | `stop-2-title.txt` | The log's title `cursor` records say `count=3`, the submenu's `count=2` (rejects the count lead if 3). Then `read <mask address> 8`, `read <node>+0x10 4` (expect `17 00 00 00`), `rtti <scene>` (expect `fclTitle@fcl@app`). `read <scene> 0x1000`, `rtti` each pointer for `TitleChoice@ope@ui@app`; on a hit `read <choice>+0x230 8` equals the log's `mgr=`. Then `watch <choice>+0x4a8 8`; enter the submenu, `changes`: `02 00 00 00 00 00 00 00`; back: `01 00 00 00 02 00 00 00` |
| 3, Settings | `stop-3-settings.txt` | `base+0x27a8c78` and `base+0x2dff8e4` hold the same value, English's. Note it |
| 4, name entry | `stop-4-name-entry.txt` | Offline: the typed name's ASCII or UTF-16LE bytes appear in one of the `G` reads; its offset is the lead. The `player-name` record's `at=` agrees |
| 5, alias line | `stop-5-opening-scene.txt` | Then `read <C> 0x1000`, `read <C+0x1000> 0x1000`, `read <C+0x2000> 0xa30`. Offline, each `MessageSerialByHash` key from the log matches an entry's default key or a slot's key; its slot's flag is set in the flag dump and earlier slots' aren't. The second section, once the name changes, should show a different slot winning |
| 7, tutorials | none | The log shows `GetMessageBySerial` serials 10256 to 10859 while each pop-up shows; none rejects the lead, and `ui-text-panes` is the fallback |
| 8, Japanese | `stop-8-voice.txt` | Right after switching, still in launch 1: `0x27a8c78` changes, and `0x2dff8e4` too once the task ends (no restart). Launch 2, after loading: both hold the new value |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says
otherwise. Type descriptors were found as the [tree note](archetype-tree-scene-and-choice-static.md) did: the decorated
name in `.debug`, minus `0x10`, file offset plus `0x2000`. String RVAs are file offset plus `0x1200` in `.udata`.

1. `vtable app::ui::ope::TitleChoice`; `decompile 0xe2ec80`, `0xe2e9a0`, `0xe2dc80`; `offset 0x4a8`, `0x4ac`, `0x448`,
   `0x4b0`, `0x3e8`; `decompile 0xe2c620`, `0xe2d200`; `callers 0xe2c620`, `0xe2d200`; `global 0x21f16b8` (none);
   `global 0x2c074a0` (`fclTitle`'s type descriptor); `decompile 0xf9c210`, `0x51dee0`, `0x51fab0`.
2. `callers 0x9e7ca0`; `decompile 0x679880`, `0x67a5f0`, `0x67a380`, `0x9e8820`, `0x898df0`, `0x8990c0`,
   `0x898e80`, `0x3b0ab0`, `0xa01bc0`, `0x9ff8d0`; `global 0x2eebc18`; `decompile 0x8994a0` (the file names next to
   each global's write). `disasm 0xa6529e0` returns nothing: `.text1` doesn't decode, so it wasn't read.
3. `global 0x2c04f50` (none); `global 0x2122630`; grep of `0x9e4f10`'s decompile for the tag codes.
4. `global 0x181c290`; `decompile 0x7bb010`, `0x7bab20`, `0x7bbc40`, `0x7bbc30`, `0x7bc6d0`, `0x3dcbb0`,
   `0x3dcc20`; `global 0x27a8c78`, `0x2dff8e4`, `0x27a8c74`; `callers 0x7bc6d0`. The 16 bytes at file offset
   `0x27a6c70` hold `2, 1, 1, 2` as dwords.
5. PowerShell over the exe's bytes: the table's entries 58 and 59 (file offset `0x27aecd0 + n × 0xd0`), each `+0x18`
   function's first 15 bytes. `global 0x2123308`, `0x2123330` (none: referenced from the table only), `0x17efb10`;
   `decompile 0x9128e0`, `0x912930`, `0x90b2d0`; `vtable app::ui::UUIC_Common_TutorialWindow`.

Nothing in `.text1` was decompiled; functions that jump there were not followed. The class and file names are the
exe's own; no game text is quoted.
