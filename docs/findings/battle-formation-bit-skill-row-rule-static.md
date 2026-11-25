# A skill's row rule reads the Formation bit, and the battle's row toggle turns one input action into each direction

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-party-swap-and-formation-static.md`](battle-party-swap-and-formation-static.md) and
  [`pad-icon-glyph-tables-static.md`](pad-icon-glyph-tables-static.md)
- **Functions:**
  - UnitFormationBit | 0x1403e8230 | .ecode | Battle | known only | Returns a unit's `+0x0` bit 22, the Formation bit. | finding
  - SkillRowAllows | 0x1403d3fc0 | .ecode | Battle | known only | For skill IDs 1 to 999, whether a skill's row flags allow it with the Formation bit given. | finding
  - SkillUsableCheck | 0x140f31eb0 | .ecode | Battle | known only | A skill-usability check that returns 3 when the row rule refuses the skill. | finding

For #259. Which value of the Formation bit is the back row is still not pinned, but two readers narrow it. A skill's
table entry has two row flags in its byte 2: `0x4` allows it with the bit clear and `0x8` with the bit set; with
neither, any row will do. `BTL_CommandSelectChoice::vf27` toggles the row on input action `0x5a` only when the bit is
clear and on `0x59` only when it is set. So one of actions 89 and 90 always moves the member one way: if 89 is D-pad
Up and 90 Down, a clear bit is Front `[NEEDS VERIFICATION]`.

## Evidence

- **The bit.** `0x3e8230` returns bit 22 of the unit's first `u32`, which `FormationGet` (`0xf32ed0`) jumps to. It has
  about 35 callers, most in battle setup and AI code.
- **The row rule.** `0xf31eb0(unit, skill*)` asks `0x3e60e0(unit)` (the `u16` at unit `+0x4`) and, when that is 1,
  calls `0x3d3fc0(*skill, 0x3e8230(unit))`; a false answer makes it return 3. `0xf28800` makes the same call. For a
  skill ID from 1 to 999, `0x3d3fc0` reads byte 2 of the entry `0x3e13f0` returns (the skill's table entry, a lead:
  the decompiler shows no argument); with `0x4` or `0x8` set, it returns whether `0x8` is set when the bit is set, and
  whether `0x4` is set when it is clear; otherwise 1. That `+0x4` = 1 means a party member, and which skills carry
  either flag, are `[NEEDS VERIFICATION]`: the skill table's byte 2 wasn't read.
- **The toggle.** `BTL_CommandSelectChoice::vf27` (`0xd89dc0`), when `0x3b0ab0(0x3942)` is false (a flag test, which
  flag `[NEEDS VERIFICATION]`), asks `0x7ae520(0x5a, 1, 0xffff)` and then `0x7ae520(0x59, 1, 0xffff)`, the input-action check `PadIsTrigOK` uses
  ([input route](../raw-sources/input-route-2026-11-16.md)). With `0x5a` and the bit of the member at `+0x3e0` clear,
  or `0x59` and the bit set, it sets a `Mode` value to 2 and runs `0xd451c0`, the change. The battle command panel's
  actions are 77 to 82 and 91 ([glyph note](pad-icon-glyph-tables-static.md)); which buttons 89 and 90 bind to is
  `[NEEDS VERIFICATION]`: the binding map's defaults weren't found.

Next, live: on a Debug build, press D-pad Up then Down on a member in battle and read that member's bit after each
(`FormationWatch`'s log does this). The press that sets the bit is action 89's direction.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-24:

1. `callers 0x3e8230`; `decompile 0x3e8230`.
2. `decompile` `0x570c0`, `0x5a450`, `0x66b30`, `0x672f0`, `0x334750`, `0xacdf0`, `0xf28800`, `0xf31eb0`, `0xd89dc0`,
   `0x3835e0`, `0x2b2170` (the callers), each read where it uses the bit.
3. `decompile 0x3d3fc0`; `decompile 0x3e60e0`.

All addresses are in `.ecode` (`0x1000` to `0x17c0000`, [target cursor note](battle-target-cursor-index-static.md)).
Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
