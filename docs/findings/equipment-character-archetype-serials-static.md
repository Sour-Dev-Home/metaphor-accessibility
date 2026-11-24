# The equipment, character and archetype name files' serials start at 2442, 3647, 0 and 9441

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on [`item-skill-message-serials-static.md`](item-skill-message-serials-static.md),
  [`field-nearby-unit-names-static.md`](field-nearby-unit-names-static.md) and
  [`item-equipment-menu-names-from-resolves.md`](item-equipment-menu-names-from-resolves.md)
- **Functions:** none

The exe's message-file table gives `Equip_Message_1.msg` (weapon, armour, gear and accessory names) serials 2442–3646
(1205), `Equip_Message_2.msg` 3647–4852 (1206), `CharaName.msg` 0–121 (122) and `ATCategoryName.msg` (the archetype
lines: Seeker, Mage, Warrior, ...) 9441–9458 (18). Each count matches the file as the game compiled it in a Debug log.
For #216, #217 and #736: the Item, Equipment and shop lists fetch these names by serial. `ClassName.msg`'s range test
is a jump into `.text1`, so its range isn't read.

## Evidence

- **The table.** Entries are 0xd0 bytes apart; `System_Message.msg`'s is at `0x27b10d8` and `Item_Message_1.msg`'s at
  `0x27b1828` (the item note), entry index = compile id − 1. Each holds a path pointer at `+0x8`, its length at
  `+0x10` and the range test at `+0x20`.
- **The range tests** (`lea eax,[rcx-first]; cmp eax,count-1; setbe al; ret`, or `cmp ecx,count-1` from 0):

  | Entry | Test | Bytes | First | Count | File |
  |---|---|---|---|---|---|
  | `0x27b0cc8` (0) | `0x9f4a70` | `83 F9 79` | 0 | 122 | `message/CharaName.msg` (the field note) |
  | `0x27b1008` (4) | `0x9f4c80` | `E9 …`, a `jmp` into `.text1` | — | — | `message/ClassName.msg` |
  | `0x27b15b8` (11) | `0x9f4d00` | `8D 81 76 F6 FF FF`, `3D B4 04 00 00` | 2442 | 1205 | `message/system/Equip_Message_1.msg` |
  | `0x27b1688` (12) | `0x9f4d10` | `8D 81 C1 F1 FF FF`, `3D B5 04 00 00` | 3647 | 1206 | `message/system/Equip_Message_2.msg` |
  | `0x27b1758` (13) | `0x9f4a80` | `8D 81 0B ED FF FF`, `3D 21 01 00 00` | 4853 | 290 | `message/system/Equip_Message_3.msg` |
  | `0x27b2458` (29) | `0x9f4b50` | `8D 81 1F DB FF FF`, `83 F8 11` | 9441 | 18 | `message/ATCategoryName.msg` |

  The files are contiguous: `PcParam_Message.msg` (entry 10, `0x27b14e8`, test `0x9f4cf0`) is 2402–2441, and
  `Item_Message_1.msg` starts at 5143.
- **Compiled counts.** In the mod's Debug `message-log.txt` of 2026-11-23 (local, not committed; the game's text),
  counting each file's messages as `MessageLibrary.Parse` does (a `}` that closes a labelled body) gives
  `Equip_Message_1` 1205, `Equip_Message_2` 1206, `Equip_Message_3` 290, `CharaName` 122 and `ATCategoryName` 18.
- **Against the 2026-11-22 runs.** The shop's weapon and armour names (2447–3405, descriptions at + 1206) and the Item
  list's equipment tabs (2697–3375) fall in `Equip_Message_1`. By `Parse` position, the tabs' rows are `WeaponName_`,
  `ArmorName_`, `SubarmorName_` and `AccessoryName_` labels, in the Weapons, Armour, Gear and Accessories tab order,
  and so are the shop's four equipment Sell tabs. The Equipment slots' worn items 2697, 2944, 3257 are a weapon, armour
  and gear in that slot order. The party serials 2, 4, 7, 8, 11, 16, 21 are `CharaName` positions (Strohl at 2). The
  Equipment character block's 94xx serial is `ATCategoryName`: index 0 (the protagonist) 9441 Seeker, index 3 (7) 9448
  Thief and index 4 (8) 9449 Faker, their starting archetypes. That the target list's rows follow the party serials'
  order is `[NEEDS VERIFICATION]` by ear.
- **What's open.** `ClassName.msg`'s range. The Equipment character list's entry also fetches 1083–1117 (unlabelled),
  just below `System_Message.msg`'s 1142: by contiguity `MapName.msg` and `ClassName.msg` would end there, but
  `EnemyName.msg` and `MapName.msg`'s tests are `.text1` jumps too, so that is `[NEEDS VERIFICATION]` and unused.

## Steps

`tools/Metaphor.SignatureCheck` (this repository at the commit that adds this note), on the build above, 2026-11-23:

1. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0x27b15b8 0x28` (and `0x27b1688`,
   `0x27b1758`, `0x27b1008`, `0x27b2458`, `0x27b14e8`): the path pointer at `+0x8`, length at `+0x10`, test at
   `+0x20`. Read the path with `--bytes <exe> <pointer − 0x140000000> 0x24`.
2. `--bytes <exe> 0x9f4d00 16`, and the same for the other tests above.
3. Count each file's messages in a Debug `message-log.txt`'s `compile` record for it, as above.

Nothing in `.text1`, `.xcode` or `.xtext` was read.
