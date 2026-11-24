# No command-battle screen or command for Assist was found; the exe's Assist classes are field and navigator ones

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do (the
  maintainer's local Debug logs, grep only)
- **Sources:** [`battle-inventory-2026-11-17.md`](../raw-sources/battle-inventory-2026-11-17.md) (rows 47, 80, 91);
  builds on [`battle-command-panel-static.md`](battle-command-panel-static.md)
- **Functions:**
  - BattleCommandOpenSub | 0xf10ba0 | .ecode | Battle | known only | Opens a screen for each command code of the battle command panel; none of them is Assist.
  - btl::InfoSupport vtable | 0x17e2608 | .udata | Battle | known only | The navigator's support model, 46 slots, holding the Assist thinking state.

For #263. **Not found.** The battle inventory already says Assist (RT) is "not used in a command battle" (row 80): it
is on the bottom bar, and RT assists in real-time field combat (row 91). The exe agrees as far as static reading goes:
no `BTL_` screen, command-panel button or battle layout carries an Assist name. The names that do are
`UUIC_Field_AssistMenu@ui@app` and `fldScriptEventAssist@fld@app` (field), and `Assist_ThinkingState@InfoSupportModelState@btl`,
a state of `btl::InfoSupport` (vtable `0x17e2608`, 46 slots), which reads as the navigator's support model
`[NEEDS VERIFICATION]`.

## Evidence

- **RTTI names.** `grep -aoE "[A-Za-z_0-9]*Assist[A-Za-z_0-9]*"` over the exe gives only the four classes above and the
  field script functions `FldScriptEventAssist*` (seven names). Every `BTL_` class name (14) and every
  `CommandBattle*Layout` (seven) was listed; none is Assist.
- **The command panel.** Over all the maintainer's saved Debug logs, the command panel's `ui-anim` records name seven
  parts, 183 records each: `Command@attack`, `@synthesis`, `@item`, `@pass`, `@guard`, `@rewind` and
  `ClassLineLayout@Main`, all `@panel@command@layout@ui@app`. No Assist part.
- **The panel's command codes.** `BattleCommandOpenSub` (`0xf10ba0`) opens a screen for codes 2, 6, 7, 9, `0xF` and
  `0x13`, and none for 3 to 5 ([panel note](battle-command-panel-static.md)); none of those has been tied to Assist,
  and the codes with no reading (2, 3 to 5, `0x13`) are the only places one could hide `[NEEDS VERIFICATION]`.

## Steps

1. `grep -aoE` over the exe for the Assist names and for `.?AV[...](BTL_|CommandBattle)[...]`.
2. Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23: `vtable btl::InfoSupport`.
3. Local logs: `grep -rhoa "ui-anim class=[A-Za-z_@]*"`, counted by class.

## Not found; next lead

In a Debug session, in a command battle, press RT on the command wheel, then hold it, and note the log time. Look for a
`cursor` record with a new `screen=`, a `ui-anim` class with `Assist`, `Support` or `Navi` in it, a `select` record, or
a `BattleCommandOpenSub` code. If nothing comes and the screen shows nothing, Assist has no command-battle use on this
build, and #263 reduces to the field's RT assist (`UUIC_Field_AssistMenu`; #186). If something opens, its class names
the next static target.
