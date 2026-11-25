# A banner's `<V 0>` is slot 0 of the message manager's variable buffer, set just before the banner shows

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-situation-banner-text-static.md`](battle-situation-banner-text-static.md),
  [`message-serials-from-file-positions.md`](message-serials-from-file-positions.md) and
  [`global-query-finds-the-reader-loads.md`](global-query-finds-the-reader-loads.md)
- **Functions:**
  - MsgManagerCreate | 0x9eb670 | .ecode | Messages and Text | known only | Creates `app::msg::Manager` and stores it at `0x2f04960`. | finding
  - MsgManagerCtor | 0x9dfd00 | .ecode | Messages and Text | known only | Builds the manager; its `+0x210` is a 5-slot variable buffer. | finding
  - MsgVariableSetInt | 0xd72360 | .ecode | Messages and Text | known only | Sets variable slot n to an int (tag 1). | finding
  - MsgVariableSetSerial | 0xa02550 | .ecode | Messages and Text | known only | Sets variable slot n to a message serial (tag 2). | finding
  - MsgVariableSetString | 0xa025b0 | .ecode | Messages and Text | known only | Sets variable slot n to a string (tag 3). | finding
  - BattleBannerQueueStep | 0x326e70 | .ecode | Battle | known only | Fills slots 0 to 2 from a queued banner, then calls `BattleMessageShow` kind 2. | finding

For #739. The `<V n>` tags in a banner's text are the message variables: five 0x28-byte slots inside the message
manager, at `[base+0x2f04960] + 0x220 + n*0x28`. The battle banner queue writes the unit's name into slot 0 just before
it calls `BattleMessageShow`, so the mod's existing hook can read slot 0 when a kind 2 banner shows. A party member's or
enemy's name is stored as a message serial (tag 2), and one unit's as a string (tag 3). Static only.

## Evidence

- **The buffer.** `0x9eb670` allocates `0x5d0` bytes, builds them with `0x9dfd00` and stores the pointer at
  `0x2f04960`, the global that `GetMessageBySerial` and the message window read. `0x9dfd00` sets the vtable
  `app::msg::Manager::vftable`, and at `+0x210` the vtable `fw::StaticArray<fw::VariableBuffer<5>::Data,5>::vftable`
  with a count of 5 at `+0x218` and five `0x28`-byte elements built from `+0x220` (`_eh_vector_constructor_iterator_`,
  size `0x28`, count 5). `0x9e9740`, which every setter calls, is a `.ecode` jump stub; Ghidra's decompile of it
  reads as `[0x2f04960] + 0x210`, which matches the constructor, but its target is protected code
  `[NEEDS VERIFICATION]`.
- **One slot.** Each setter takes the slot index and indexes the array by `0x28`. The byte at `+0x20` is the tag: `1`
  for an int at `+0` (`0xd72360`), `2` for a `u32` serial at `+0` (`0xa02550`), `3` for a string at `+0` (`0xa025b0`
  assigns with the `std::string` assign `0x1ecf80`). The array is reached through the vtable's `+0x10`; that this
  returns the inline elements at `+0x220` is `[NEEDS VERIFICATION]` (the live check below).
- **Tag 2 is a serial.** `0xa021f0`, which the queue uses for slot 1 when its flag `0x10` is set, returns a label index
  plus `0x1a5d` (6749). The logged skill names sit in that range (`SkillName_IceMagic_02` is serial 6764 in the
  2026-11-22 session log), so tag 2 holds a serial `GetMessageBySerial` takes `[NEEDS VERIFICATION]` for names.
- **The queue fills slot 0.** In `0x326e70`, for each queued entry (record at node `+0x10`): flag bit `2` at `+0x1c`
  puts the int at `+0xc` in slot 0. Otherwise the unit index at `+0x4` (a `u16`) picks the name. With the `u16` at `+0x2`
  equal to 1, unit 1 gets a string (`0xa8bd6a0` builds it, `0xa8f74b0` stores it in slot 0); other units get the serial
  from `0xa8ac0f0(unit)` through `0xa02550(0, …)`. With any other value at `+0x2`, the serial comes from
  `0xa8b0f70(unit)`. Then slot 1 gets the number at `+0xc` (as `"%d"`, an int, or a skill-name serial by flags `1`,
  `0x20`, `0x10`), slot 2 a number when `+0x10` isn't -1, and `BattleMessageShow(component, { [+0x8], 2 }, [+0x14])`
  shows it. Which `+0x2` value means the party, and that unit 1 is the protagonist, are `[NEEDS VERIFICATION]`; the
  string for unit 1 is the guess.
- **Not through a hooked lookup.** At the three stun banners in the 2026-11-22 session log (`BattleMessageShow kind=2
  id=215` at 1343069, 1344429 and 1345790 ms) no `GetMessageBySerial` resolve is logged, so the name's text is built in
  the banner's text resolve (`.text1`, see the situation-banner note), not through anything the mod hooks.

The three name functions (`0xa8ac0f0`, `0xa8b0f70`, `0xa8bd6a0`) are in `.text1` and weren't decompiled. The mod
doesn't need them: it reads slot 0 after they ran.

## For the mod

On `BattleMessageShow` with kind 2, read the tag at `[base+0x2f04960]+0x240`. Tag 2: the `u32` at `+0x220` is a serial,
turned into text by `MessageSerials` once its file is known (which file holds unit names is `[NEEDS VERIFICATION]`).
Tag 3: an MSVC `std::string` at `+0x220` (size at `+0x230`, capacity at `+0x238`, the text inline when the capacity is
below 16, else a pointer at `+0x220`). Only the queue `0x326e70` is shown to fill slot 0; the other callers of
`BattleMessageShow` (`0x324c50` and the rest) are `[NEEDS VERIFICATION]`, so the mod should check the tag, not assume
it.

## Live check

Inspector batch, Debug build with `-OptIn inspector`, during a battle where a unit gets stunned:

```text
# #739: what fills <V 0> in "<V 0> cannot move due to Stun!" (docs/findings/battle-banner-name-variable-static.md).
# Game build 548ddc95... (Steam 18330018).
# Follow-ups: rtti <buffer> (expect StaticArray<VariableBuffer<5>::Data,5>), read <buffer>+0x8 4 (expect 05 00 00 00),
# read <buffer>+0x10 0xc8 (the five slots; slot 0's tag is byte 0x20). For tag 2, look the u32 up in the message log's
# GetMessageBySerial lines; for tag 3, the size is at slot+0x10.
[stun-banner]
base
# The manager, then its variable buffer (+0x210).
follow base+0x2f04960 0x0
follow base+0x2f04960 0x210
[enemy-banner]
# Again after a banner naming an enemy ("<V 0> is powering up!"), to see the other branch's tag.
follow base+0x2f04960 0x210
```

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`), build above, 2026-11-23.

1. `decompile 0x326e70` (the fill before `BattleMessageShow`); `decompile 0xa025b0`, `0xa02550`, `0xd72360`, `0x9e9740`,
   `0xa021f0`; `callers 0xa025b0`.
2. `global 0x2f04960` (writes only: `0x9e6290`, `0x9eb670`); `decompile 0x9eb670`, `decompile 0x9dfd00`.
3. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> <rva> 8` for each function's section.
4. The 2026-11-22 22.50 session's message log (local, not committed): `grep "kind=2 id=215"` and the lines around it.

Only the stub `0x9e9740` was followed into its target; nothing else in `.text1`, `.xcode` or `.xtext` was decompiled.
