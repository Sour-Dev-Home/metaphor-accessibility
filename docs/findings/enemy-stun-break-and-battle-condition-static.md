# A field enemy's stun is its "break" byte, not an AI state; in battle a stun lands in a hit result's condition bits

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
  (`FldEnemyIsBreak 0x8dc160`); builds on [`field-enemy-strikes-and-notice-static.md`](field-enemy-strikes-and-notice-static.md),
  [`battle-hit-result-hp-left-static.md`](battle-hit-result-hp-left-static.md),
  [`battle-enemy-list-maps-hits-to-slots-static.md`](battle-enemy-list-maps-hits-to-slots-static.md),
  [`menu-values-party-unit-static.md`](menu-values-party-unit-static.md) and
  [`placeholder-tags-name-file-positions.md`](placeholder-tags-name-file-positions.md)
- **Functions:**
  - FldEnemyIsBreak | 0x8dc160 | .ecode | Field | known only | Script function: returns the byte `+0x36d0` of the field enemy with the given id. | finding
  - FieldEnemySetBreak | 0x662af0 | .ecode | Field | known only | Our name: writes `+0x36d0`; on clear, refills the stun gauge. | finding
  - FieldEnemyBreakUpdate | 0x6646a0 | .ecode | Field | known only | Our name: per-enemy update that ends the break after a time and refills the gauge. | finding

For #772. In the field, a stunned enemy is one whose byte `enemy+0x36d0` is 1, the value the game's own script function
`FldEnemyIsBreak` returns. It is set when a strike empties the enemy's stun gauge (`int enemy+0x36e0`) and cleared when
the stun times out or the enemy is hit again. No AI state is written: of the 40 state names none is a stun, and the
stagger's state `0x24` in the earlier note is another path. In battle, the moment a condition lands is a
`BattleHitResult` call whose row has condition bits; that stun is bit 13 (`0x2000`) is a lead only. Static only.

| Value | Where | Confidence |
|---|---|---|
| Field: stunned | byte `enemy+0x36d0`, 1 while stunned; `enemy` is an entry of the list `FldEnemyIsBreak` walks (`[[base+0x2ee7510]+0x20]+0x18`, matched by `+0x9f8`) | High in code; that "break" is the stun the player sees `[NEEDS VERIFICATION]` |
| Field: stun gauge | `int enemy+0x36e0`; its full value from `0x65f040` | High in code |
| Field: the stun lands | in `fldEnemy::vf58` (`0x65d7a0`, field damage), when a hit takes the gauge to 0 or below | High in code |
| Field: it ends | `0x662af0(enemy, 0)` from the update `0x6646a0` (a timer past a threshold), from `vf58` (a hit while `+0x36d1` is set) and from `fldCharaAct_Damage_Fly::vf2` (`0x613a50`) | High in code; which ending the player sees most `[NEEDS VERIFICATION]` |
| Battle: a condition lands | `BattleHitResult` (`0x3231a0`): row `index` of `result` (`0x20` bytes each), `int +0xC`, is that hit's condition bits; the target's record is the hit unit | High that the field is a condition mask; that it holds only the conditions just inflicted `[NEEDS VERIFICATION]` |
| Battle: stun's bit | `0x2000` (bit 13), from one `0x82000` test and `<CONDITION 13 2>` in the stun banner | Low: a lead |
| Battle: the unit's conditions | unit record `+0x1c`, the same bit space (bit 19 `0x80000` knocked out) | High for the mask, low for bit 13 |

## Evidence

- **Break byte.** `FldEnemyIsBreak` (`0x8dc160`) walks the pointers from `[[0x2ee7510]+0x20]+0x18` to `+0x20`, finds
  the enemy whose `+0x9f8` equals its argument and returns its byte `+0x36d0`. The only writer is `0x662af0`. On 1 it
  only stores; on 0 it sets `+0x36e0` to `0x65f040`'s value (adding the carry at `+0x36dc` when that isn't
  `0x7fffffff`), clears `+0x36ec` and `+0x36f0`, and calls a thunk (not followed) on `+0xde0` with 3 and 299.
- **Set.** `fldCharaAct_Damage_Fly::vf1` (`0x613b30`) sets `+0x69c` and calls `0x662af0(enemy, 1)` (`0x613b8a`,
  `mov dl,1`). `fldEnemy::vf58` lowers `+0x36e0` by the hit's value (`0x65da44`) and, when the result is 0 or less,
  keeps the overflow in `+0x36dc`, zeroes the gauge and marks a local flag (`0x65da68`). That flag (`0x65dffd`) leads to
  `0x6035a0(enemy+0x2880, 0x39)`, `0xfeaba0(enemy+0xde0, 3, 0x134)`, and a duration read by tier (`enemy+0x3778`: fields
  `+0xb4`, `+0xb8`, `+0xbc` of a table from `[0x2ee7368]+0xfe48`). That action `0x39` is `Damage_Fly` is
  `[NEEDS VERIFICATION]`; the earlier gauge tests skip a hit while action `0x39` or `0x3b` runs (`0x587cb0`).
- **Clear.** `0x6646a0` (called from `fldEnemy::vf4`, `0x65e740`) clears the break when `0x645210(enemy, 0)` passes a
  constant and a test fails, and refills the gauge over time while not broken (`+0x36e4`, `+0x36e8`). `vf58` clears it
  at `0x65e3b8` when `+0x36d0` and `+0x36d1` are both set. `Damage_Fly::vf2` sets `+0x36d1` and clears it when
  `enemy+0x3168` is 3.
- **Not an AI state.** The 40 names in `field-enemy-awareness-static.md` hold no stun, faint or break, and the break code
  above writes no `C+0x2ec`. The `0x24` request in `vf58` (stagger count `+0x37d8`) is separate.
- **Battle hit.** In `0x3231a0`, row `index`'s `+0xC` equal to `0x80000` sends popup tag `0xb`, and any of its low 19
  bits sends tag 8, through `0xf1ab10`, before the number popup. `0x80000` is the knocked-out bit of the unit record's
  `+0x1c` (`menu-values-party-unit-static.md`), so the row uses the same bits. The target's record is
  `*(*(target+0x18)+0x8)`, which the mod's `BattleHitResultHook` already logs as `record=`; it doesn't log row `+0xC`.
- **Bit 13.** A byte scan of `.ecode` for calls to the mask test `0x3e4010` with an immediate `edx` found 181 calls.
  Only two masks hold bit 13: `0x82000` (knocked out or bit 13, `0x76389`, on a battle unit's record) and `0x6000`
  (bits 13 and 14, in `btl::Animation::vf1`, `0xc5a26`). Sibling tests of the same shape use `0x9d000` (`0x80ba2`)
  and `0x99000` (`0x3406eb`), which leave bit 13 out, so the scan doesn't pin it. `<CONDITION n 2>` names position `n`
  of `BadStatus_Message_1.msg`, and the stun banner (`@jyokyo_helpText_KIZETSU`, #772) uses 13; that position `n` is
  bit `n` is `[NEEDS VERIFICATION]` (positions 0 to 2 are Poison, Malady and Paralysis; their bits weren't checked).
- **Not found.** The code that fills row `+0xC` and sets the unit's `+0x1c` bits wasn't located; the condition test
  `0x3e4010` and the clear `0x3e4310` are the only `+0x1c` helpers pinned. Where the stun banner (id 215) is requested
  wasn't traced.

## What would settle it

[`stop-2-stun.txt`](../sessions/session-5/stop-2-stun.txt), first dungeon, Debug build with the inspector:

1. **Field.** Run its `[field]` section near unhit enemies, then `read <addr> 0x28` for each address before a strike,
   after one, while the enemy is stunned and once it moves again. Break 1 with the gauge at 0 only while stunned
   settles the field state.
2. **Battle.** Stun an enemy in battle, take `record=` from the last `BattleHitResult` line and `read <record>+0x1c 4`;
   again at its "cannot move" banner. `0x2000` set both times, and clear before, settles the bit. Logging row `+0xC`
   in a Debug build would show the landing hit directly.
3. **Start of battle.** Strike a stunned field enemy into battle and read its record the same way: whether a field stun
   carries into battle as this condition is `[NEEDS VERIFICATION]`.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-22. Each item is one `pwsh scripts/dev/ghidra.ps1` query unless it says so.

1. `decompile 0x8dc160`; `offset 0x36d0`; `disasm 0x662af0`, `decompile 0x662af0`; `callers 0x662af0`; `disasm`
   `0x613a50` and `0x613b30` before each call; `decompile 0x6646a0`; `callers 0x6646a0`, `0x613b30`.
2. `offset 0x36e0`; `disasm 0x65d7a0` around `0x65da25`, `0x65dffd`, `0x65e3b8` and `0x65e4c2`.
3. `decompile 0x3231a0`, the lines before its `0xf1ab10` calls; `decompile 0x3e4010`, `0x3e4310`.
4. A byte scan of the exe's `.ecode` section (file offset `0x600`, RVA `0x1000`) for `E8` calls whose target is
   `0x3e4010`, taking the `mov edx, imm32` (`BA`) within 24 bytes before each; then `disasm` `0x76389`, `0xc5a26`,
   `0x3406eb`, `0x80ba2`.
5. A byte search of the exe for `.?AV` names and strings with `Stun`, `Faint`, `Kizetsu`, `BadStatus`; `global` on the
   `Check_ID_BADSTATUS` and `Check_BADSTATUS` strings (`0x17c4b38`, `0x17c4b50`), `decompile 0xb6f50`, `0x76f90`,
   `0x88d50` (the AI test filters by ID through a lambda not followed).

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled. No decompiled output is quoted.
