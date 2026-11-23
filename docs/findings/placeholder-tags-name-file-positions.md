# A skill or condition tag's number is its message's position in its file; items, classes and icons aren't pinned

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do (the Debug message log); and opening the shipped executable in a disassembler as-is, accepting that protected
  parts may be unreadable
- **Sources:** none, see Steps; tag counts in
  [`message-listening-run-2026-11-16.md`](../raw-sources/message-listening-run-2026-11-16.md)

For #108 and #109. In `<SKILL n f>` and `<CONDITION n f>`, `n` is the message's position in
`message/system/Skill_Message_1.msg` and `message/system/BadStatus_Message_1.msg`, and form `f` 2 shows the name. An
item's and a skill's form 1 is an icon the text names right after it. Item, class and attribute numbers, `<V n>` and the
icon tags (`PAD_ICON` and the rest) are `[NEEDS VERIFICATION]`, so the mod says a stand-in for them.

## Evidence

- **Skills.** Weapon descriptions read `[<SKILL 131 1>Slash]`, `[<SKILL 157 1>Pierce]` and `[<SKILL 173 1> Strike]`.
  Positions 131, 157 and 173 of `Skill_Message_1.msg` are `SkillName_ATK_ZAN_04`, `SkillName_ATK_TOTSU_03` and
  `SkillName_ATK_KAI_03`: zan, totsu and kai are the slash, pierce and strike attack types, each by its label. Form 1
  shows the type's icon, since the word after it is the type, not the skill's name. Form 2 appears after "Required
  Skill:" with numbers 1, 2, 14, 15, 27, 28 and 41, all magic skills (`SkillName_FireMagic_01` and so on).
- **Conditions.** `<CONDITION 0 2>`, `1` and `2` are each followed by a description: HP depleting, stats lowered, a
  chance of not acting. Positions 0, 1 and 2 of `BadStatus_Message_1.msg` are Poison, Malady and Paralysis.
- **Counts.** As the mod's parser reads them (`MessageLibrary.Parse`, which skips a body line starting with `@`), the
  logged files hold 1086 and 14 messages. 1086 matches the exe's message-file table for `Skill_Message_1.msg`
  (#631's static note `item-skill-message-serials-static.md`, not yet on `main`); the 14 has no second source
  `[NEEDS VERIFICATION]`. The mod keeps a file only at that count.
- **Not pinned.**
  - `ITEM`: numbers such as 16497 and 20637 are past `Item_Message_1.msg`'s 773 messages; `<ITEM 16497 1>` is followed
    by an accessory's name that isn't in `Item_Message_1.msg`. How the number picks a file and position is open.
  - `CLASS`: `<CLASS 58 2>` "is an Archetype exclusive to the protagonist", but position 58 of `ClassName.msg` is an
    untranslated stub (`@ClassName_18_02`) and position 57 is Prince, so the number may count from 1. Unresolved.
  - `ATTRIBUTE` (only 65534 and 65535) and `CLASS 65535 0` look like values filled in at run time.
  - `<V n>`: the clash dialog `System_message_PC_40` (#564) fills it at run time with the clashing action.
  - Icons: the compiler's tag table gives `PAD_ICON` code `0xd972`, `SHOP_ICON` `0xd96a` and `WEATHER_ICON`
    `0xd971`. The code `0xd972` appears nowhere else in `.ecode`, so where the window draws the icon, and how it picks
    Xbox, PlayStation or keyboard art, wasn't found. On screen the icons follow the last device used (#109's comment of
    2026-11-20); how the game picks between pad icon sets is open.
  - `PAD_ICON n` names an action, not a button: the tutorials say "Press `<PAD_ICON 12>` to guard" and "Hold
    `<PAD_ICON 7>` while moving to dash", and the keyboard versions of the same tips say "the movement keys" where the
    pad's say `<PAD_ICON 16>`. Which button each action shows, per device, is open.

## Steps

1. A Debug build's message log from a session on this build (2026-11-21, local, never committed) holds each compiled
   file's source. Each file's messages were parsed as `MessageLibrary.Parse` does (a throwaway Python 3.12 script kept
   outside the repository) and the positions above printed with their labels.
2. Tags with their context were listed with `grep -o` on the same log and matched by hand to those positions.
3. Static: `pwsh scripts/dev/ghidra.ps1 global 0x2121c58` (the `SHOP_ICON` string) finds one read, in the function at
   `0x9cc450`; `disasm 0x9cc450` shows the tag table's entries, each a code (`mov dword [rbp+x],0xd96a`) and then its
   name, `PAD_ICON` as the immediate `0x4e4f43495f444150` at `0x9ccfca`. A byte search of `.ecode` for `72 d9 00 00`
   finds only that table's entry (`0x9ccfaa`). Nothing in `.text1`, `.xcode` or `.xtext` was read.
