# A banner's enemy name serial is `EnemyName.msg`'s, whose serials run 122 to 542, right after the character names

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** none, see Steps; builds on
  [`battle-banner-name-variable-static.md`](battle-banner-name-variable-static.md) and
  [`equipment-character-archetype-serials-static.md`](equipment-character-archetype-serials-static.md)
- **Functions:**
  - Banner queue | 0x326e70 | .ecode | Battle | known only | Takes the name serial for each banner from the unit, for an enemy or a party member.

For #739. The message files between `CharaName.msg` (serials 0–121) and `System_Message.msg` (from 1142) are
`EnemyName.msg`, `MapName.msg` and `ClassName.msg`, in the message-file table's order, and their compiled counts (421,
539 and 60) fill that gap exactly: `EnemyName.msg` is 122–542. The three tag 2 values the 2026-11-23 evening session
logged in a banner's slot 0 for enemies, `0x97`, `0x9f` and `0xa7`, are its positions 29, 37 and 45: the sword, lancer
and bow `Skeleton06` labels, the same variant of three kinds. Confidence: medium. The ranges come from contiguity, not
from the exe's range tests, which are in protected code; a live check is below.

## Evidence

- **The table.** Entries 1 to 4 of the message-file table (`0x27b0d98`, `0x27b0e68`, `0x27b0f38`, `0x27b1008`, `0xd0`
  apart after `CharaName.msg`'s `0x27b0cc8`) name `message/NpcName.msg`, `message/EnemyName.msg`, `message/MapName.msg`
  and `message/ClassName.msg` by the path pointer at `+0x8`. `NpcName.msg`'s range-test pointer at `+0x20` is 0, so it
  owns no serials. `EnemyName.msg`'s test `0x9f4cb0` and `MapName.msg`'s `0x9f4ad0` are `jmp`s into `.text1`, not
  followed; `ClassName.msg`'s is too (the equipment note).
- **The counts.** In the mod's Debug message log of 2026-11-23 19.22 (local, not committed; the game's text), counting
  messages as `MessageLibrary.Parse` does gives `EnemyName.msg` 421, `MapName.msg` 539 and `ClassName.msg` 60. 122 + 421
  + 539 + 60 = 1142, `System_Message.msg`'s first serial. The equipment list's unlabelled fetches 1083–1117 fall in
  `ClassName.msg`'s 1082–1141 by the same count.
- **The values.** The review on #739 logged slot 0 at `BattleMessageShow` with tag 2, stable per enemy kind across
  battles: `0x9f` with the 1320-HP enemy, `0x97` with the 1440-HP one, `0xa7` with the 1200-HP one on a stun banner. 151,
  159 and 167 less 122 are positions 29, 37 and 45: `EnemyName_sword_Skeleton06`, `EnemyName_lancer_Skeleton06` and
  `EnemyName_arrow_Skeleton06`, eight apart as each kind's eight variants are. On allies, `0x4` is `CharaName.msg`
  position 4, `CharaName_PC03_Hulkenberg`. Which enemy on screen had which value is `[NEEDS VERIFICATION]`.
- **Who fills it.** The banner queue `0x326e70` takes the serial from `0xa8b0f70(unit)` for an enemy and `0xa8ac0f0(unit)`
  for a party member (the name-variable note); both are in `.text1` and weren't read.

## For the mod

`MessageSerials` keeps `EnemyName.msg` at 122 with 421 messages, dropping it when another count compiles. The banners
say a tag 2 serial's text only when it is in `CharaName.msg` or `EnemyName.msg`, behind the testing setting
`speech.battleBannerNames`. A Debug build's `BattleMessageShow` line still ends with ` slot0=0x.. tag=..`, and a tag 3
name's text, so a session can match each spoken name to the screen.

## Steps

`tools/Metaphor.SignatureCheck` (this repository at the commit that adds this note), on the build above, 2026-11-23:

1. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0x27b0d98 0x28` (and `0x27b0e68`,
   `0x27b0f38`, `0x27b0cc8`); read each path with `--bytes <exe> 0x2122ad8 0x60`; `--bytes <exe> 0x9f4cb0 16` and
   `0x9f4ad0 16`.
2. In the Debug `message-log.txt`, count each file's messages in its `compile` record as `Parse` does (a `}` line that
   closes a labelled body), and list `EnemyName.msg`'s labels by position.
3. `pwsh scripts/dev/ghidra.ps1 decompile 0x326e70` (Ghidra 12.2 shared project) for the queue's two name calls.

Nothing in `.text1`, `.xcode` or `.xtext` was read.
