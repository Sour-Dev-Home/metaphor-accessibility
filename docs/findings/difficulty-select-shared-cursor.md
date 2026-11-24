# The difficulty list moves a shared cursor manager; the mod dropped it for want of an owner and of open menus

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** [`menus-open-session6-2026-11-17.md`](../raw-sources/menus-open-session6-2026-11-17.md),
  [`screen-class-session-2026-11-17.md`](../raw-sources/screen-class-session-2026-11-17.md); corrects
  [`difficulty-select-own-input-static.md`](difficulty-select-own-input-static.md)
- **Functions:**
  - DifficultySelectUpdate | 0xf50130 | .ecode | Menus | hooked | `UUIC_Common_DifficultySelect::vf13`, the list's update.
  - CursorManagerInput | 0xe98770 | .ecode | Menus | hooked | Slot 4 of `app::ui::ope::CursorManager`, the hooked cursor input.
  - app::ui::ope::CursorManager vtable | 0x22045e8 | .udata | Menus | known only | The shared cursor manager the difficulty list moves.

New Game's difficulty list (`UUIC_Common_DifficultySelect`, #540) reads its input through the shared
`CursorManager` held at its `+0x28`, so every move reaches `CursorManagerInput` and the menu cursor hook. The static
note said it never does; that was wrong. Two things kept it silent: the record had no screen class, because the list
never calls the choices' cursor getter, and it shows while the game's menus read closed, when `MenuScreens` dropped
every cursor. The yes/no dialog after it was silent for the second reason alone.

## Evidence

- **Static.** In the list's update (`UUIC_Common_DifficultySelect::vf13`, `0xf50130`), the "input reader" `0xe9acc0`
  takes `[this+0x28]`, tests the flags at its `+0xac` and calls slot 4 of its vtable. `[this+0x28]`'s class is
  `app::ui::ope::CursorManager` (vtable `0x22045e8`), whose slot 4 is `CursorManagerInput` (`0xe98770`), the hooked
  function. The `int` the note called the stored choice, `[[this+0x28]+0x60]`, is that manager's `top` field (the menu
  cursor hook's offset `0x60`), so the game keeps the difficulty as `4 - index`.
- **Live.** The maintainer's by-ear run of 2026-11-20 (mod built from #566, #567, #568 and #570, report log in the
  mod's folder): from 169.7 s, `cursor ... class=0x22045e8 ... count=5 mode=?` with `screen=?`, index 2, then 3, 4, 3,
  2 as the player moved. Nothing was spoken.
- **No owner.** `ScreenClassLookup` learns a manager's owner only from the choices' shared cursor getter (`+0x230`,
  [`screen-class-link-2026-11-17.md`](../raw-sources/screen-class-link-2026-11-17.md)); the list isn't a choice. Its
  class has type information (`.?AVUUIC_Common_DifficultySelect@ui@app@@`), so recording `this` as the owner of
  `[this+0x28]` from the update names it `UUIC_Common_DifficultySelect@ui@app`. The update's signature matches once on
  disk at rva `0xf50130` (`tools/Metaphor.SignatureCheck/difficulty-select-probes.json`).
- **Menus closed.** `MenuScreens` counted a cursor only while the camp handle at `0x2ee7338` is set, which is the title
  and the field menus; it stays null in conversations, including their yes/no dialogs
  ([`menus-open-session6-2026-11-17.md`](../raw-sources/menus-open-session6-2026-11-17.md)). In the run, the name
  confirmation (`YesNoChoiceSample`, 160–162 s) spoke, so the title's handle was still set; the yes/no after the
  difficulty list (173.6 s, a new manager) spoke nothing. The reader wasn't toggled: the transcript has no "Dialogue
  reader off", and no reader binding was pressed while the dialogue window held a message. So the handle cleared
  between 162 s and 170 s; the reply choice at 167 s falls in that span but nothing ties the two
  `[NEEDS VERIFICATION]`: the log keeps no record of the handle.
- **Names.** The run's message log has `EN/message/system/System_Message.msg` (compiled at 8 s) with
  `System_General_17` "Storyteller", `_18` "Easy", `_19` "Normal", `_20` "Hard" and `_20_1` "Regicide", and the scene's
  own file `e00_002_001.msg` with `MES_e00_002_001_014_01`, a `SEL` message whose five lines are the same names in that
  order. The list opened on index 2 ("Normal"). By ear (maintainer, 2026-11-20, #571's build) the screen shows these
  words in that order top first, but the cursor counts from the bottom: Up from "Normal" (index 2) reached "Easy" on
  index 3, so index 0 is "Regicide" and index 4 "Storyteller". While the focus moved, `GetMessageBySerial` resolved serials 1890, 1889, 1888 and 1890 for indexes 2, 3, 4,
  2: probably each item's description, labels unknown `[NEEDS VERIFICATION]`, a lead for the details key.

What to check in game with this change: the report log's `cursor` records on the list show
`screen=UUIC_Common_DifficultySelect@ui@app` and the status says one more hook installed; the list speaks
"Difficulty, Normal, 3 of 5", then each name as it moves, matching the screen; the yes/no after it speaks.

## Steps

1. Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
   [query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)): `decompile 0xf50130` (the calls
   to `0xe9acc0` on `[param_1+0x28]`), `decompile 0xe9acc0`, `vtable 0x22045e8`, `vtable 0x2251798`.
2. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0xf50130 96`, then `--probes <exe>
   tools/Metaphor.SignatureCheck/difficulty-select-probes.json`: one match.
3. In the mod's Reloaded-II folder: `grep cursor report-log.txt` (the `count=5 screen=?` records), `grep -v` the
   per-frame `selected id=-1` and pad records from `message-log.txt` between 168 s and 177 s for the requests and
   serials, and grep `message-log.txt` for the labels above. The logs hold game text and stay local.
