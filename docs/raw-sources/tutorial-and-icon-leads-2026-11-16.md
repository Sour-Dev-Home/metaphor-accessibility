# Tutorial pop-ups and button icons: leads from the exe and the session-1 log (issues #118, #109)

- **Source:** our own reading of the game executable's bytes on disk, as shipped (ADR 0002): `Metaphor.SignatureCheck
  --bytes` and `--probes` (this repository at the commit adding this file), decoded by hand, plus two throwaway
  PowerShell scans kept outside the repository (every `E8`/`E9` rel32 call or jump to a given address; every 4 bytes that
  read as a rip-relative displacement to a given address, a heuristic). Also the maintainer's local `message-log.txt`
  from spike session 1, part B (`spike-session-1-2026-11-16.md`): its `compile` records and the logged message source,
  of which only paths, labels, timings and tag numbers are kept here.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

All addresses are relative to the game module. Function names are ours unless they are script names from
`squirrel-script-functions-all-2026-11-16.md`.

## #118: the tutorial files and the open function

- **Tutorial text is in two message files, compiled when a tutorial opens.** Session 1 part B compiled
  `EN/message/system/System_Tutorial_1.msg` (15,100 chars) and `System_Tutorial_2.msg` (73,749 chars) as a pair four
  times, at 845.5 s, 924.9 s, 1142.7 s and 1161.0 s, each time with new ids (71/70, 74/73, 91/90, 93/92); no other
  file compiles that way. That each pair is one pop-up is our reading of the timing `[NEEDS VERIFICATION]`. `_1` holds
  titles (labels `<Family>_Tutorial_Title_NN`, 219 of them), `_2` the pages (`<Family>_Tutorial_Page_NN_MM`, some
  `…_PC`); the families are `Battle`, `Dungeon`, `Field`, `Follower`, `Gaisensya`, `Party_Comrade` and `System`.
- **No known message lookup ran while they were shown.** Between 845.5 s and 862.9 s the log has only sound cues: no
  `MsgRequestByName`, `GetMessageByLabelIndex`, `GetMessageBySerial` or `MessageSerialByLabel` record. One
  `GetMessageBySerial` (serial 1154, flags 7) came 31 ms before the first compile.
- **Script functions:** `TutorialWindowOpen` `0x9128e0`, `TutorialWindowStart` `0x912930`, `TutorialWindowEnd`
  `0x912890`, `TutorialWindowIsEnd` `0x9128c0`, `IsTutorialWindowReady` `0x90b2d0`. `TutorialWindowStart` is
  `C2 00 00` (`ret 0`) and `IsTutorialWindowReady` is `B0 01 C3` (`mov al, 1; ret`): stubs in this build.
- **`TutorialWindowOpen`** jumps into `.text1` at `0xa0a51f0` (one other jump there, from `.text1` `0x882acbb`),
  ordinary code:

  ```text
  48 83 EC 28                sub  rsp, 0x28
  66 C7 44 24 3C 00 00       mov  word [rsp+0x3c], 0
  C6 44 24 3E 00             mov  byte [rsp+0x3e], 0
  89 4C 24 38                mov  [rsp+0x38], ecx     ; its argument, 32 bits: the request's first field
  E8 …                       call 0xf348f0            ; returns a flag in al (its code jumps into .text1, not read)
  …                          ; byte [rsp+0x3d] = 1 if al != 0
  E8 …                       call 0x321e60            ; a manager object (TutorialWindowIsEnd calls it too)
  48 8D 54 24 38             lea  rdx, [rsp+0x38]     ; -> request
  48 89 C1                   mov  rcx, rax
  E8 …                       call 0xfae680
  ```

- **`TutorialWindowOpenRequest` `0xfae680`** (`.ecode`): `(manager*, request*)`, request = a 32-bit number then three
  bytes (`+4`, `+5` the flag above, `+6`). It reads the number (`mov ecx, [rdx]`), calls `0x3e2a70` with it, takes a
  separate path when that returns 6, else looks the number up in a hash table; it returns a byte (`mov al, 1` on the
  first path). Callers: the script function's code above and ten direct calls in `.ecode` (`0x4b8544`, `0x4ca57f`,
  `0x4d6a8d`, `0x4e4862`, `0x4e9df3`, `0x504a6d`, `0x5050ee`, `0xe43cb5`, `0xfaa32a`, `0xfaa7f0`), so native code opens
  tutorials too. That the number is a tutorial id is our reading `[NEEDS VERIFICATION]`. The exe also names
  `COMMON/event/Tutorial.dat`, `COMMON/ui/tutorial/TutorialIDPC.bin` and the class `UIC_Common_TutorialWindow`, likely
  the id-to-pages data and the window `[NEEDS VERIFICATION]`.
- **`MessageLocationByLabel` `0x9e81e0`** (`.ecode`), next to `MessageSerialByLabel`: `(char* label, int* file,
  int* index)`. It sets both outputs to -1, hashes the label with the MD5 routine at `0x13d6e70`, looks it up in the
  table at `[0x2f04960]` (`0x9e60f0`), and stores the entry's low 32 bits as the file and the high 32 as the index. 46
  direct callers. Whether the tutorial window resolves its pages through it is `[NEEDS VERIFICATION]`.
- **Signatures**, `tools/Metaphor.SignatureCheck/tutorial-probes.json`: both match once on disk
  (`TutorialWindowOpenRequest` at file `0xfadc80`, `MessageLocationByLabel` at `0x9e77e0`); not yet checked in memory.

## #109: the icon tag and the pages that use it

- **`PAD_ICON` compiles to code `0xD960`, one argument.** The compiler's tag table (`0x9ccb00`–`0x9ccde0`, read in
  `name-lookup-candidates-2026-11-16.md`) builds the entry at `0x9ccfca`: `mov rax, "PAD_ICON"`, length 8, then
  `mov dword [rbp+0x460], 0xD960`. `0x142cf80` returns `(code >> 8) & 7` for a code in `0xD800`–`0xDFFF`, 1 here, and
  `0x142cf60` returns the code itself or 0 for an ordinary character.
- **Where `0xD960` is drawn wasn't found.** In `.ecode` the constant appears only in that table. The one `.text1` hit
  near code (`0xa63f7cc`) is in transformed code that doesn't read as ordinary instructions, so it wasn't followed
  (ground rule 5). The token walker at `0x1478480`, where most of `0x142cf60`'s `.ecode` callers are, tests for
  `0xD830` and `0xD82E` there, not icons.
- **A table of 45 page labels at `0x224e280`** (`.udata`, 32 bytes each): 39 are pages whose source has `<PAD_ICON n>`;
  `Dungeon_Tutorial_Page_05_03`, `_08_02`, `Field_Tutorial_Page_43_03`, `_57_01` are in it without one, and six pages
  with icons are not. What it is for is `[NEEDS VERIFICATION]`; its only rip-relative reference found is in `.text1`.
- **The button art is per device:** `COMMON/effect/icon/{PS4,PS5,XBOX,NS,Keyboard}_icon_0061.EPL`, and the setting
  `PC_BUTTON_TYPE`.
- **Icon numbers per page**, from `System_Tutorial_2.msg`'s logged source, in order of appearance (labels and numbers
  only). Used: 0, 1, 2, 5–16, 18.

| Page | `PAD_ICON` | Page | `PAD_ICON` |
|---|---|---|---|
| `Battle_Tutorial_Page_01_01` | 13, 10 | `Dungeon_Tutorial_Page_17_02` | 11 |
| `Battle_Tutorial_Page_02_01` | 0 | `Dungeon_Tutorial_Page_17_03` | 6 |
| `Battle_Tutorial_Page_03_01` | 7 | `Dungeon_Tutorial_Page_18_01` | 16 |
| `Battle_Tutorial_Page_07_01` | 12 | `Dungeon_Tutorial_Page_19_01` | 14 |
| `Battle_Tutorial_Page_08_03` | 18 | `Dungeon_Tutorial_Page_20_02` | 14 |
| `Battle_Tutorial_Page_22_01` | 11 | `Field_Tutorial_Page_01_01` | 12, 7 |
| `Battle_Tutorial_Page_23_02` | 1 | `Field_Tutorial_Page_13_01` | 16, 11 |
| `Battle_Tutorial_Page_23_03` | 8 | `Field_Tutorial_Page_13_01_PC` | 11 |
| `Battle_Tutorial_Page_31_01` | 8 | `Field_Tutorial_Page_13_02` | 16, 11 |
| `Battle_Tutorial_Page_34_02` | 0 | `Field_Tutorial_Page_13_02_PC` | 11 |
| `Dungeon_Tutorial_Page_01_01` | 16, 7, 12 | `Field_Tutorial_Page_14_01` | 9 |
| `Dungeon_Tutorial_Page_01_01_PC` | 7, 12 | `Field_Tutorial_Page_15_01` | 9, 10 |
| `Dungeon_Tutorial_Page_01_03` | 15 | `Field_Tutorial_Page_20_01` | 9, 9 |
| `Dungeon_Tutorial_Page_02_01` | 6 | `Field_Tutorial_Page_20_01_PC` | 9, 8 |
| `Dungeon_Tutorial_Page_03_01` | 10 | `Field_Tutorial_Page_21_01` | 8 |
| `Dungeon_Tutorial_Page_03_02` | 10, 13 | `Field_Tutorial_Page_31_01` | 9 |
| `Dungeon_Tutorial_Page_03_03` | 13 | `Field_Tutorial_Page_43_01` | 11 |
| `Dungeon_Tutorial_Page_04_01` | 6 | `Field_Tutorial_Page_49_03` | 5 |
| `Dungeon_Tutorial_Page_05_01` | 13 | `Field_Tutorial_Page_55_01` | 10 |
| `Dungeon_Tutorial_Page_06_01` | 10 | `Gaisensya_Tutorial_PAGE_08_01` | 8, 9 |
| `Dungeon_Tutorial_Page_06_02` | 10, 13 | `System_Tutorial_PAGE_01_01` | 15 |
| `Dungeon_Tutorial_Page_07_01` | 13, 10 | `System_Tutorial_PAGE_02_01` | 2 |
| `Dungeon_Tutorial_Page_08_01` | 8 | `Dungeon_Tutorial_Page_09_01` | 13 |
| `Dungeon_Tutorial_Page_12_01` | 2 | | |

The same file also has `<SHOP_ICON 0>`–`<SHOP_ICON 3>`, once each.

## Dev listeners (`src/Metaphor.Hooks/TutorialListeners.cs`, Debug only, no opt-in)

- `TutorialWindowOpenRequest`: after each call, `request via=TutorialWindowOpenRequest id=<n> flags=<a>,<b>,<c>
  result=<byte>`.
- `MessageLocationByLabel`: after each call, `resolve via=MessageLocationByLabel text="<label>" file=<n> index=<n>`;
  the same label and answer at most once per 2 s; a label holding the typed player name is logged by length only.

A display-time listener for the icon number needs the draw found above; until then, the session matches by sight.

## For the session

1. Signature spike with `tutorial-probes.json`, then a Debug run past a tutorial pop-up (or reopen one from the
   camp menu's tutorial list, if the game has one there `[NEEDS VERIFICATION]`).
2. #118: does each pop-up log one `TutorialWindowOpenRequest` beside the `System_Tutorial` compile pair, and do its
   page labels show up as `MessageLocationByLabel` records? If no labels appear, fall back to the text-pane hook.
3. #109: with an Xbox controller, then the keyboard (`_PC` pages), note the button drawn for each icon on pages in the
   table above, page by page.
