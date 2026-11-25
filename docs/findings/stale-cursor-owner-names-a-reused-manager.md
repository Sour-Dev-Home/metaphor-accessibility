# A cursor manager reused at a freed manager's address inherits its dead owner, which reads as `SingleChoiceBase`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do (the maintainer's local Debug logs, grep only); opening the shipped executable in a disassembler as-is,
  accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on [`save-load-slot-screens.md`](save-load-slot-screens.md) and
  [`screen-class-link-2026-11-17.md`](../raw-sources/screen-class-link-2026-11-17.md)
- **Functions:**
  - SystemMenuChoice::vf2 | 0xe03340 | .ecode | Menus | known only | The choice's deleting destructor: restores its base vtables, calls the base destructor in `.text1`, frees `0x478` bytes. | finding

For #732 and #622. The load list that said "x of 17" got the class `SingleChoiceBase@ope@ui@app` because its new
manager was allocated at the address of the System menu's earlier manager. The mod's `CursorOwners` still mapped that
address to the old `SystemMenuChoice`, which had been destroyed; its freed memory still held the vtable its destructor
chain leaves, the abstract base `SingleChoiceBase`, so the lookup read that class. All four `SingleChoiceBase`
managers in the saved logs are such reuses. #707's `SlotScreens` already names the slot list by its count after the
prompt "classed or not", and a later session heard "Slot 10" on that path (#732's comment, 2026-11-22 "c"), so the
symptom is fixed; the cause is in `ScreenClassLookup`, which trusts a mapping after its owner is gone.

## Evidence

- **The #732 opening.** In the 2026-11-22 session log (Debug, Steam 18330018; local, grep only), manager
  `0x41879d5450` is `SystemMenuChoice@ope@ui@app count=6` at 1 912 938 and 1 915 468 ms. At 1 927 129 ms the System menu
  reopens with a new manager, `0x41879d7070`. At 1 977 511 ms, 11 ms after serial 1634 ("Read the records so far?"),
  the load list's first cursor comes on `0x41879d5450` again, now `SingleChoiceBase@ope@ui@app count=17`.
- **Every case is a reuse.** Over all saved logs, each manager whose class first reads `SingleChoiceBase` had another
  class earlier, with a different count: `ConfigTopChoicePc` (5) then 17, `SystemMenuChoice` (6) then 17,
  `ConfigMenuChoicePcT<…>` (24) then 12, `YesNoChoiceSample` (2) then 9. None started as `SingleChoiceBase`.
- **Why that class.** `SingleChoiceBase`'s second vtable (`0x21c4130`) has slots pointing at `0x14b3a12`, an import
  jump stub (`FF 25 …`), presumably `_purecall` `[NEEDS VERIFICATION]`, so no live object is of that class.
  `SystemMenuChoice::vf2` (`0xe03340`) writes `SingleChoiceBase_WithLayout<CampSystemMenuLayout>`'s vtables at `+0x0`
  and `+0x8`, calls the base destructor `0xdf85270` (in `.text1`, not read), then frees the object; that the base
  destructor writes `SingleChoiceBase`'s vtable last is the usual C++ order, and the logs fit it
  `[NEEDS VERIFICATION]` statically. `global 0x21c3f10` and `global 0x21c4130` find no writer in analysed code, which
  fits a writer in `.text1`.
- **Why the mapping survives.** `ChoiceGetCursor`'s hook records owner by manager (`src/Metaphor.Hooks/ScreenClassHooks.cs`);
  `CursorOwners` forgets only past 256 managers (`src/Metaphor.Core/CursorOwners.cs`), and `ScreenClassLookup.Of`
  reads the owner's class live each time. A list whose owner never calls the shared getter (the slot lists) keeps
  whatever mapping its address had.
- **#622's other gaps** are not this path: the slot's place, playtime and empty state are #773, readable from the save
  files' headers ([`save-slot-header-in-save-files.md`](save-slot-header-in-save-files.md)); "Save complete" was #689.

## Steps

1. Local logs: `grep -a " cursor mgr="`, then per manager the first record of each new `screen=… count=…`, printed when
   the new class is `SingleChoiceBase`, with the class before it.
2. Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23: `vtable
   app::ui::ope::SingleChoiceBase`, `app::ui::ope::SystemMenuChoice`; `decompile 0xe03340`, `0xd44e90`; `global
   0x21c3f10`, `global 0x21c4130`. Sections from `tools/Metaphor.SignatureCheck --bytes <exe> 0xdf85270 8` and
   `0x14b3a12 8`.

## Build plan

1. **Forget dead owners.** In `ScreenClassLookup.Of`, when the owner's class reads `SingleChoiceBase@ope@ui@app`,
   drop that manager's mapping (`CursorOwners.Forget`) and return null, so the input arrives as `screen=?` as on the
   working openings. Unit test in `Metaphor.Core.Tests` on `CursorOwners.Forget`; the class test is one comparison
   in the hook layer.
2. **Keep #707's count rule.** `SlotScreens` keeps naming by count after the prompt, classed or not; its #732 test
   stays. `YesNoDialogFocusTests.AnotherScreenIsNotSpokenAsAYesNoDialog` hands its class in directly, so it is
   unaffected.
3. **By ear.** Close #732 after one run of Load from System via the main menu, late in a session, saying "Slot N";
   a `SingleChoiceBase` record in a Debug log afterwards means another reuse path.
