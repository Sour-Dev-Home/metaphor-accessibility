# "Not enough MP!" reaches neither banner show nor any analysed code that names the situation file

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod's message log, which hooks the game's functions, as those community mods do
- **Sources:** none, see Steps; builds on
  [`battle-situation-banner-text-static.md`](battle-situation-banner-text-static.md) and
  [`battle-move-banner-text-reference-static.md`](battle-move-banner-text-reference-static.md)
- **Functions:**
  - OrderTopDecideCheck | 0xd8c440 | .ecode | Battle | known only | Tactics menu's refusal: buzzer, and for reason 2 the banner kind 2 id 29.
  - ItemDecideCheck | 0xd8cd60 | .ecode | Battle | known only | A list's refusal: buzzer, variable 0 = reason flags, message window id `0x8b1`.
  - TextRefResolveStub | 0xa67aa0 | .ecode | Messages and Text | known only | Jump stub into `.text1` (`0xabd3380`) that resolves a layout's text reference.

For #692. Message 74 of `jyokyo_help_Message.msg` (`jyokyo_helpText_FUSOKU_MP`) is not shown by either banner show
the mod hooks, by the message lookups it hooks, or by any analysed code that asks for the situation file. The skill
list's refusal is decided in code the disassembler didn't analyse (most likely protected) `[NEEDS VERIFICATION]`, so no
hookable `.ecode` point that hands over id 74 was found. Not ready to build from a hook; a fallback is below.

## Evidence

- **What the game did.** In the 2026-11-22 22.50 session's message log, after the cursor landed on Faker's Roguery in
  `BTL_SkillSelect` (1540230 ms), the confirm button was pressed 14 times (`buttons=0x4000`, 1541619 to 1553846 ms).
  Each press played cue 3 on the system archive (`acb=0x45994420`) through the UI-sound chain (`0x92e475`); there was no
  `BattleMessageShow`, `BattleMessageRequestShow`, `GetMessageBySerial`, `GetMessageByLabelIndex` or `ui-anim` record
  in that span. Mark 26 followed at 1558371 ms.
- **How a list refuses.** A battle list's "can this row be chosen" check is a lambda typed
  `pair<char, GeneralSE_ID>(fclAppUIEvent*)`: it returns "accepted" and a sound. `0xd8c440` (registered by `0xd83b20`,
  which `BTL_OrderTop::vf14` calls) reads the row's enabled byte (`+0x438` array) and reason (`+0x418` array); a
  disabled row returns sound 3, and only reason 2 also shows `BattleMessageShow(kind 2, id 29)`
  (`jyokyo_helpText_ESCAPE_IMPOSSIBLE`). So a refusal may play the buzzer with no message call at all. That cue 3 is
  `GeneralSE_ID` 3 is inferred from the matching numbers `[NEEDS VERIFICATION]`.
- **The other refusals.** `0xd8cd60` and `0xd8d500` (registered by `0xd838c0` and `0xd839e0`, whose only callers are in
  protected code) refuse with sound 3, set message variable 0 to reason flags (`0xd72360`) and open the message window
  with id `0x8b1` (`0x9ec910`). Neither uses message 74.
- **Nothing analysed names file 9 with index 74.** `callers 0x9e8010` (the message-file getter, an `.ecode` stub into
  `.text1` at `0xa655760`) lists 26 call sites; those asking for file 9 are the two banner shows and `0xd8cd60`, which
  drops the result. The others ask for 5, 6, `0x30` to `0x32`, `0x38` or `0x39`, except `0xfbf360`, which takes the
  file from its caller and shows through `UUIC_Field_Basic` (its type descriptor at `0x2bbd518`), a field component.
  The text-reference builder `0xa67db0` has 21 callers, none in the battle lists. The resolve stub
  `0xa67aa0` has 51 callers, none in the battle lists.
- **No immediate 74 nearby.** Of the 23 `mov dword [..], 0x4a` instructions in the exe, the ones in battle and UI code
  (`0xd83540`, three in `0xefe590`, `0x2e1d50`) set resource or layout ids, not a message.
- **`BTL_SkillSelect`'s own slots.** Its vtable (`0x21c9fe8`) overrides `vf14`, `vf15`, `vf16`, `vf17`, `vf44` and three
  `SkillListLayout` slots; `vf17` registers only a cursor-move lambda (`0xd8d6e0` → `0xd7c570`). Its decide check
  wasn't found among the 34 lambda vtables at `0x21d0fe0`–`0x21d17c8`.

## What would settle it

1. **Where the text draws.** A recording frame at a refusal shows whether "Not enough MP!" is the top banner (the
   `UUIC_Battle_Message` layout, reached some third way) or text inside the skill list. The 2026-11-22 session has no
   recording in the capture folder.
2. **The fallback, by log.** With the dev-only cue log on (Debug, `criAtomExPlayer_SetCueId`), in `BTL_SkillSelect`:
   confirm an affordable skill, an MP skill you can't afford, and an HP skill you can't afford. If cue 3 on the system
   archive through `0x92e475` comes only with the refusals, the mod can say "Not enough MP" or "Not enough HP" itself from
   the focused skill's cost type when a confirm in the skill list is answered by cue 3, without hooking the message.
   The skill's cost type and the unit's MP are `[NEEDS VERIFICATION]` reads.
3. **A component log.** The UI-component open log spike, if it lands, names the component that opens on the refusal.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`), build above, 2026-11-23.

1. Message log of the 2026-11-22 22.50 session (local, not committed): the records from 1526000 to 1558400 ms without
   the pane and cursor lines; the compiled `jyokyo_help_Message.msg` labels counted in order (73 `FUSOKU_HP`,
   74 `FUSOKU_MP`, 203 `FUSOKU_PRESSTURN_ICON`, 204 `FUSOKU_MONEY`, 205 `FUSOKU_CLASS`, 29 `ESCAPE_IMPOSSIBLE`).
2. `callers 0x9e8010`, `disasm 0x9e8010`, `callers 0xa655760`; `decompile 0xd8cd60`, `0xd72360`, `0x9ec910`,
   `0xf16170`, `0xf10ba0`, `0xfbf360`, `0xfbf140`; `callers 0xa67db0`, `callers 0xa67aa0`, `callers 0xaf9ea0`.
3. `vtable app::ui::ope::BTL_SkillSelect`; `decompile` its `vf14`, `vf17`, `vf44` and the `SkillListLayout` slots;
   `vtable 0x21d1050`, `decompile 0xd8d6e0`, `0xd7c570`.
4. `--bytes <exe> 0x21d0e00 0xa00` with `tools/Metaphor.SignatureCheck`, each lambda vtable's slot 2 read from it, then
   `decompile` of each; `global` on the vtables of `0xd8c440`, `0xd8cd60`, `0xd8d500`; `callers 0xd83b20`.
5. `grep -obUaP` over the exe for `mov dword [..], 0x4a`, then `disasm` at each hit in battle and UI code.
6. `vtable` of `UUIC_Battle_TacticalSituation`, `UUIC_Common_Warning` and `UUIC_Battle_Message`; `callers 0xc5ce0`.

Only `0x9e8010`'s jump target was looked at in `.text1`, to see that it is one; nothing there was decompiled.
