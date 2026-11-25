# The level-up point screen is ResultDistributionPointChoice; its five stats are arrays in the choice, read from the party unit

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; the class, `count=5` and the stat names' labels are from #624's by-ear session
  (2026-11-22)
- **Functions:**
  - ResultDistributionPointChoice::vf73 | 0x140dbf260 | .ecode | Menus | known only | Sets the screen up: the member, the five stat values and the points to spend. | finding
  - ResultDistributionPointChoice::vf72 | 0x140dbfe60 | .ecode | Menus | known only | Hands the five values and the cursor to the layout (`0xc22770`). | finding
  - ResultDistributionPointChoice::vf16 | 0x140dbd1b0 | .ecode | Menus | known only | Per-member branch on the member's ID (voice cue name built with `snprintf`). | finding
  - UnitStatGrowthSum | 0x1403c7c00 | .ecode | Menus | known only | Sums a member's five per-level stat gains from level 1 to n, each capped at 99. | finding
  - UnitStat | 0x1403e6630 | .ecode | Menus | known only | Returns stat `i` of a party unit: the byte at unit `+0x2a + i`, clamped to 1..99. | finding

For #624. The screen is `ResultDistributionPointChoice@ope@ui@app` (vtable `0x21d9540`, constructor `0xdba3c0`), five
rows, one per stat in the order of `System_General_05` to `_09`. Its state is in the choice, so a focus class can read
it through the cursor's owner: the member at `+0x3e0`, the five values at `+0x438`, and the points left at `+0x490`.
All offsets are from static reading only; the meanings of the arrays are leads `[NEEDS VERIFICATION]`.

## Evidence

Offsets are from the choice's start (functions in its second vtable, `0x21d97b0`, see `this` 8 bytes in, and are
given here already adjusted).

| Field | Where | Evidence | Confidence |
|---|---|---|---|
| Layout | `+0x3d8`, pointer | `vf72` and `vf74` pass it to the layout calls `0xc22770`, `0xc218a0`, `0xc21830` | High |
| Member (chara ID) | `+0x3e0`, `u32` | `vf73` copies it to `+0x494` and passes it to `0x3b0eb0` (party unit by ID); `vf16` switches on it, cases 1 to 10 | High |
| Rows | `+0x3e8` list, count `+0x3f0` | the shared choice row list | High |
| A level | `+0x420`, `u32` | passed as `n` to `UnitStatGrowthSum` | Medium: the level before the level-up is a lead |
| Mode flag | `+0x425`, byte | `vf73` takes one of two set-up paths on it; copied to `+0x498` | Meaning `[NEEDS VERIFICATION]` |
| Five values | `StaticArray<u32,5>` at `+0x428`, count `+0x430`, data `+0x438` | written per stat by `vf73`; handed to the layout | High that the layout shows them; current value is a lead |
| Five caps | `StaticArray<u32,5>` at `+0x450`, data `+0x460` | `vf73` writes `UnitStat` clamped 1..99 | Lead |
| Five flags | `StaticArray<bool,5>` at `+0x478`, data `+0x488` | `vf73` writes "this stat's gain is not 0" | Lead |
| Points to spend | `+0x490`, `int` | `vf73` sets it to 0, then in one mode to the counter `G+0x233f4 + 0x99 × 4` (`0x3b07e0(0x99)`) | Medium |

The party unit is `G + 0x228 + id × 0x16a8` (`0x3b0eb0`,
[turn-icon note](battle-turn-icons-and-ui-components-static.md)). `UnitStat` reads the stat bytes at unit `+0x2a` to
`+0x2e`; `0x3e62d0` returns the level, a `short` at unit `+0x20`.

The results screen (#166) is a different screen: `ResultLevelUpLayout` and `ResultGrowParamLayout` animate without a
cursor. #624 is this choice. `StatusDistributionPointChoice@ope@ui@app` is a second class of the same kind, likely the
Status menu's own distribution `[NEEDS VERIFICATION]`.

## Live check

Debug session with `opt-in-inspector.txt`, unit 1 levelling up (`base+0x2ca7fc0`, the unit session 6 read; that it is
the protagonist is `[NEEDS VERIFICATION]`).

```text
[before-distribution]
# Unit 1's level (short at +0x20) and five stat bytes (+0x2a..+0x2e), on the results screen before the point screen.
read base+0x2ca7fe0 16
# The counter vf73 reads in one mode (G+0x23658).
read base+0x2cc9d48 4
[after-confirm]
# After confirming the points: each stat byte should have risen by the points put in it.
read base+0x2ca7fe0 16
read base+0x2cc9d48 4
```

The choice has no static path, so its fields need the feature's own Debug record: log owner `+0x420` to `+0x498`
(0x78 bytes) when they change, as `keyconfig-state` does for Key Configuration, and move and add points once per stat.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23.

1. `vtable app::ui::ope::ResultDistributionPointChoice`; `global 0x21d9540`.
2. `decompile` `0xdbc140`, `0xdbd9f0`, `0xdbfe60`, `0xdbf260`, `0xdbddd0`, `0xdbdd90`, `0xdbea10`, `0xdbea30`,
   `0xdbd1b0`, `0xdbc470`.
3. `decompile` `0x3c81b0`, `0x3b07e0`, `0xf2d470`, `0x3e62d0`, `0x3c7c00`, `0xf2f3f0`, `0x3e6630`, `0xc22770`,
   `0xc223c0`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
