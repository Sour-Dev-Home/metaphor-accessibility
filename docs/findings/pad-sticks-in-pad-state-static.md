# The pad read writes both sticks as 16-bit values centred on 128

- **Game build:** 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
- **Method:** static reading of the shipped exe (ADR 0002): the shared Ghidra project's `decompile`, and the exe's
  bytes on disk at three constants
- **Sources:** [`pad-and-keyboard-state-2026-11-16.md`](../raw-sources/pad-and-keyboard-state-2026-11-16.md) (the button
  mask, and "the sticks follow"), [`input-route-2026-11-16.md`](../raw-sources/input-route-2026-11-16.md) (`PadRead`)
- **Functions:**
  - PadRead | 0x10d0a00 | .ecode | Settings | hooked | Fills the pad state as five 16-bit values after `XInputGetState`; the sticks are centred on 128.

`PadRead` (`0x10d0a00`) fills its second argument as five 16-bit values after `XInputGetState`. The button mask at `+0`
is in the raw source above; the sticks are new here (#701, #184).

## Steps

1. `pwsh scripts/dev/ghidra.ps1 decompile 0x10d0a00` (Ghidra 12.2, the project built for this game build).
2. Read the four floats it loads as constants from the exe file on disk (section table, then the bytes at each RVA):
   `0x180d328` (the centre), `0x22691a8` (the scale), `0x22691ac` and `0x22691b0` (the dead zones).

## What it writes

| Offset | Value |
|---|---|
| `+2` | right stick x: `128 + x × scale` |
| `+4` | right stick y: `128 − y × scale` |
| `+6` | left stick x: `128 + x × scale` |
| `+8` | left stick y: `128 − y × scale` |

- `x` and `y` are XInput's signed 16-bit thumb values (`sThumbLX` at `+8` of its state, then `LY`, `RX`, `RY`), so right
  and up are positive there; the game's y is flipped, so pushing **up gives less than 128**.
- The centre is `128.0` and the scale `0.003875851` (127 / 32767), and each value is truncated to an integer
  (`vcvttss2si`), so the values run from 0 to 255.
- Inside a dead zone (the stick's length at most `7849` for the left, `8689` for the right, XInput's own documented
  values) both of that stick's values are exactly `0x80`: the disassembly (`ghidra.ps1 disasm 0x10d0a00`) stores the
  same register, `r9w`, for both (`0x10d0c7b`–`0x10d0c87` for the left, `0x10d0cd9`–`0x10d0ce2` for the right).

## Not known

- Which of the game's readers take the sticks from this state, and whether field movement does: `[NEEDS VERIFICATION]`.
  The mod's auto-walk (#184) and stick scan (#701) rely on it; a session confirms it.
- Whether keyboard movement goes through it: probably not, since `KeyboardRead` has its own state; with no pad
  connected `XInputGetState` fails and the read isn't made `[NEEDS VERIFICATION]`.
