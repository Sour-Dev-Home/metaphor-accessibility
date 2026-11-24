# Skill, condition, class, enemy and virtue tags name a position in their file; items and icons aren't pinned

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do (the Debug message log); and opening the shipped executable in a disassembler as-is, accepting that protected
  parts may be unreadable
- **Sources:** none, see Steps; tag counts in
  [`message-listening-run-2026-11-16.md`](../raw-sources/message-listening-run-2026-11-16.md)

For #108 and #109. In `<SKILL n f>` and `<CONDITION n f>`, `n` is the message's position in
`message/system/Skill_Message_1.msg` and `message/system/BadStatus_Message_1.msg`, and form `f` 2 shows the name. An
item's and a skill's form 1 is an icon the text names right after it. By the lines around them (2026-11-23): `<CLASS n
2>` is position `n − 1` of `message/ClassName.msg`, `<ENEMY_NAME n>` position `n` of `message/EnemyName.msg`, and
`<CHAR_PARAM n f>` (forms 0 and 2) position `n` of `message/system/PcParam_Message.msg`, a Royal Virtue's name; none is
heard yet `[NEEDS VERIFICATION]` by ear. Item and attribute numbers, `<V n>`, `<CHAR_PARAM_LEVEL>` and the icon tags
(`PAD_ICON` and the rest) are `[NEEDS VERIFICATION]`, so the mod says a stand-in for them.

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
  ([`item-skill-message-serials-static.md`](item-skill-message-serials-static.md)); the 14 has no second source
  `[NEEDS VERIFICATION]`. The mod keeps a file only at that count.
- **Classes, counted from 1.** In the 2026-11-23 evening log, five numbers fit position `n − 1` of `ClassName.msg` (60
  messages) and none fits `n`: `<CLASS 58 2>` "is an Archetype exclusive to the protagonist" (57, Prince; 58 is the
  stub `@ClassName_18_02`); `<CLASS 47 2>` "can equip different masks" (46, Masked Dancer); "the famed merchant" for
  `<CLASS 44 2>` (43, Merchant); "someone more mechanical" for `<CLASS 27 2>` (26, Gunner); "the captain of the guard
  corps" for `<CLASS 39 2>` (38, General) fits less plainly. No `<CLASS 0 2>` appears. `ClassName.msg`'s range test is a
  `.text1` jump ([`equipment-character-archetype-serials-static.md`](equipment-character-archetype-serials-static.md)),
  so its count has no second source `[NEEDS VERIFICATION]`.
- **Enemies.** Position `n` of `EnemyName.msg` (421 messages) fits every line checked: `<ENEMY_NAME 7>` "obtained its
  horn" (Guptauron, a minotaur), `<ENEMY_NAME 16>s` "there's a lot of" (Archer Goborn Esto), `<ENEMY_NAME 110>` "lurks
  in the Everfrost Forest" (Blue Elmenta), `<ENEMY_NAME 121>` "can kill anyone" (Moldilich), `<ENEMY_NAME 329>` "goes
  into a rage when its underlings are defeated" (Mask of Ire). Its range test is a `.text1` jump too, so 421 has no
  second source `[NEEDS VERIFICATION]`.
- **Royal Virtues.** `PcParam_Message.msg` holds 40 messages, matching the exe's range 2402–2441
  ([`equipment-character-archetype-serials-static.md`](equipment-character-archetype-serials-static.md)): positions 0–4
  are Courage, Wisdom, Tolerance, Eloquence and Imagination, then five level names for each. "Your Royal Virtue of
  `<CHAR_PARAM 0 2>` increased by `<V 0>`!" and the file's own `PcParam_01_Quest` to `_05_Quest`, "Increases
  `<CHAR_PARAM 0 0>`" to `<CHAR_PARAM 4 0>`, put virtue `n` at position `n`. `<CHAR_PARAM_LEVEL 4 3>` ("if my
  `<CHAR_PARAM 4 2>` were ...") names a level, but whether its second number counts from 0 or 1 is open.
- **Not pinned.**
  - `ITEM`: numbers are `0x4000` or `0x5000` plus a small index (16497 is `0x4071`, 20637 `0x509D`), but the index
    isn't a position in `Item_Message_1.msg`. Read as one, some lines fit (20485 "buy a ... in Komero" is Miraculous
    Medicine) and others don't (20636 "an ingredient known as" would be Priestess White Mask; 20488 for "the requester
    with anxiety about cavities" would be Blue Cheese). `<ITEM 16497 1>` is followed by "Bewitching Lip Rouge", which
    isn't in `Item_Message_1.msg` or `Equip_Message_1.msg`. So the number goes through a table: none of the 75
    callers of `GetMessageBySerial` decompiled on 2026-11-23 holds the item files' first serials (5143, 2442).
    Lead: in a Debug session, open a quest whose text has an `<ITEM>` tag and read which `ItemName_` serial the
    message log's `resolve` records fetch as it draws.
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
4. Classes, enemies, virtues and items (2026-11-23): the Debug message log of the 2026-11-23 evening session (build
   4d936b2 of this mod, local, never committed). Each file's messages were numbered from its `compile` record with an
   `awk` one-liner (a label line straight after a `//--------` line starts a message, as `Parse` counts them), and
   each tag's lines listed with `grep -o ".\{0,90\}<CLASS 58 2>.\{0,60\}"` and the like, then matched by hand.
5. Items, static: `pwsh scripts/dev/ghidra.ps1 callers GetMessageBySerial` lists 75 calling functions; a `batch` of
   `decompile` on each was searched for `0x1417` (5143), `0x98a` (2442), `0x5000` and `0xfff`, with no item lookup
   found. Nothing in `.text1`, `.xcode` or `.xtext` was read.
