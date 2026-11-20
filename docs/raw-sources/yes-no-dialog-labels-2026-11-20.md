# Yes/no dialog labels: the name-entry confirmation's choices are `System_Choice_YESNO`'s lines (#549)

- **Source:** the shared Ghidra project (Ghidra 12.2, `scripts/dev/ghidra.ps1`: `rtti`, `vtable`, `global`,
  `decompile`), the exe's strings located by byte search and mapped to RVAs from its section table, and the
  maintainer's local `message-log.txt` from the 2026-11-20 by-ear check of #547 and #548 (Debug mod build, Reloaded-II),
  read with grep and awk. The log holds game text, so it stays local; only labels, addresses, counts, indexes and times
  are kept here.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

The name-entry confirmation is a system select message whose choices are the lines of `System_Choice_YESNO` in
`system/System_Message.msg`: line 1 `Yes`, line 2 `No`. Its cursor's screen class is `YesNoChoiceSample@ope@ui@app`,
and the cursor index is the line: index 0 is Yes, index 1 No. The dialog opens on index 1.

## Method

1. **The label.** The function at `0x4ffe60` names both strings. It calls `0x9ee5e0` as
   `(1, "System_NameEntry_Confirm", "System_Choice_YESNO", 1, options)` on one branch and with `System_NameEntry_08` on
   the other; `System_NameEntry_Error` is its third message. `System_NameEntry_Confirm` (`.udata` `0x17f6758`) has no
   other reader in analysed code. `System_Choice_YESNO` (`.udata` `0x17f4688`) has 28 readers, among them the Squirrel
   function `MsgSystemConfirm` (`0x90cc70`, which passes it to `0xf98170`) and `UUIC_Common_DifficultySelect::vf13`,
   `UUIC_Common_Network::vf13` and `UUIC_Common_TitleVoiceSelect::vf13`.
2. **The text.** The log's compile of `EN/message/system/System_Message.msg` holds `@System_Choice_YESNO SEL 0` with
   two lines, `Yes` (with a colour reset) then `No`, and `@System_NameEntry_Confirm`, a question about the name.
3. **The class.** `rtti 0x21c4918`: the vtable of `app::ui::ope::YesNoChoiceSample`. Its slot 11 is `ChoiceGetCursor`
   (`0xd4ab90`), the shared getter the screen-class link uses; slots 14–16, 27, 41, 63 and 72 are
   `YesNoChoiceBase<SystemChoiceLayout>`'s, 73–75 its own. `global 0x21c4918` found no instruction loading the vtable in
   analysed code, so the class is tied to this dialog by the log, not statically.
4. **The log.** Name entry: keyboard input from 282.7 s to the Enter at 284.6 s, then at 287.187 s a message window
   opens and at 287.205 s the first `cursor` record with `screen=YesNoChoiceSample@ope@ui@app count=2`, index 1. A move
   (`cmd=0x15`) and a settle put it on index 0 at 288.040 s; the player confirmed there and the scene went on
   (`MES_e00_002_001_000_03` at 289.140 s) with no second name entry, so index 0 accepted the name: Yes. A second
   `YesNoChoiceSample` dialog at 297.705 s did the same (index 1, then 0). Every `YesNoChoiceSample` record in the log
   has `count=2`, and this run logged no other screen class during either dialog.
5. **Opening index.** The fourth argument to `0x9ee5e0` is 1 at both name-entry calls and both dialogs opened on index 1.
   That this argument is the opening index is `[NEEDS VERIFICATION]`.

## Not established

- Whether every `YesNoChoiceSample` dialog takes its choices from `System_Choice_YESNO`, in this order: the settings
  confirmations (#562) are logged with this class, but their choice label isn't known (`System_Message_PC.msg` has
  `System_button_message_PC_06` and `_07`, `Yes` and `No`, as separate messages). `[NEEDS VERIFICATION]`, by ear.
- The question (`System_NameEntry_Confirm`) is shown in a message window the mod doesn't log as a line request: no
  `request` record falls between 284.6 s and 287.2 s.
