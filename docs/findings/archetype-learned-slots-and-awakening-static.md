# A new archetype is a slot filled in the member's unit by one learn function, which scripts reach through AcquireClass

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
  (the script names and addresses)
- **Functions:**
  - LearnClass | 0x140389c70 | .ecode | Menus | known only | Stores a class record in the first free of a member's 60 archetype slots, or returns the one already there.
  - AcquireClass | 0x140903550 | .ecode | Menus | known only | Script function: finds the class record for a member and calls LearnClass.
  - SetPartyMemberAwakened | 0x140910610 | .ecode | Menus | known only | Script function: sets or clears game flag 0x1121 + member through `0x3c85e0`, inverting its second argument.

For #625. Every path that gives a member an archetype found in analysed code goes through `LearnClass` (`0x389c70`):
the script function `AcquireClass`, the Akademeia learn path (`0xf5ac20`) and the New Game Plus carry-over
(`0xf034c0`, under `LearnRoyalClassOnPreviousRound`). It fills a slot in the member's party unit, so "a new archetype"
is a slot whose learned bit went from 0 to 1. The awakening's words on screen were not found statically.

## Evidence

- **The slots.** `LearnClass(member, record*)` gets the unit with `0x3b0eb0(member)` (`G + 0x228 + member × 0x16a8`)
  and scans 60 (`0x3c`) slots of `0x5c` bytes from unit `+0x98`: bit 0 of the slot's first byte is "used", and the
  `int` at slot `+0x4` is the class ID. A class not found is copied into the first free slot, which gets bit 0 set.
  When game flag `0x2f4` is set it then calls `0x387f80` on the new slot `[NEEDS VERIFICATION: what that does]`.
- **The callers.** `callers 0x389c70`: `AcquireClass` (`0x903550`), `0xf5ac20` (called twice from `0xf5bc30`) and
  `0xf034c0`, plus one `.xcode` reference. `AcquireClass` is reached only through the script command table
  (`nAppScript::UniversalCommandDomain_Common::vf1`), as the raw source's address says.
- **Protagonist extra.** After learning, `AcquireClass` and `0xf5ac20` call `0x3c78c0` when the member's ID is
  `0x14` `[NEEDS VERIFICATION: what 0x14 and that call are]`.
- **Awakened flags.** `0x3c85e0(member, on)` works for members 1 to 19 on game flag `0x1121 + member`: bit
  `member + 1` of the dword at `G + 0x22e80` (`G + 0x22c5c + 0x89 × 4`, the flag array other notes use). Setting it
  also updates a table at `0x3b1010`. The script wrapper passes `arg == 0` as `on`, so which way the script argument
  reads is `[NEEDS VERIFICATION]`.
- **The name.** The archetype's name by class ID is in `message/ClassName.msg`, whose serial range isn't read
  statically ([equipment-character-archetype-serials-static.md](equipment-character-archetype-serials-static.md)).
  How a class ID maps to a position in that file is `[NEEDS VERIFICATION]`.

## Live check

Debug session with `opt-in-inspector.txt`, before and after a scene that gives an archetype (chapter 1's awakening).
Unit 1, whose HP session 6 read, is `base+0x2ca7fc0` (that it is the protagonist is `[NEEDS VERIFICATION]`); its slots start at `base+0x2ca8058` and run `0x1590` bytes.

```text
[before-awakening]
read base+0x2ca8058 0x1000
read base+0x2ca9058 0x590
# Awakened flags, bits 2..20 for members 1..19.
read base+0x2cc9570 4
[after-awakening]
# Expect one more 0x5c-byte slot with bit 0 set, and its class ID at slot +0x4.
read base+0x2ca8058 0x1000
read base+0x2ca9058 0x590
read base+0x2cc9570 4
```

The message log of the same scene shows whether the awakening's words already arrive as dialogue (`resolve` and the
shown lines), which settles the issue's first box.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23.

1. `decompile` `0x903550`, `0x903de0`, `0x903f60`, `0x910610`, `0x3c85e0`, `0x389c70`, `0xf5ac20`, `0xf034c0`.
2. `callers` `0x389c70`, `0x903550`, `0xf5ac20`, `0xf034c0`, `0x3c85e0`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled; `0x3c85e0` also touches an obfuscated byte in `.text1`, not
followed.
