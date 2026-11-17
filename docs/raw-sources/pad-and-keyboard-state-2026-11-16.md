# What `PadRead` and `KeyboardRead` write (issue #58)

- **Source:** our reading of the shipped exe as-is (ADR 0002): `dumpbin /disasm /range` from MSVC 14.51.36231 (Visual
  Studio 18) over `0x1410d0a00`–`0x1410d0cb0` (`PadRead`) and `0x1410d0de0`–`0x1410d107d` (`KeyboardRead`), image base
  `0x140000000`. Button and key names from the Windows SDK 10.0.26100.0 headers (`Xinput.h`: `XINPUT_GAMEPAD_*`;
  `WinUser.h`: `VK_*`; letters are their ASCII codes). Nothing was patched or run; the game was not launched.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

The functions and their callers are in `input-route-2026-11-16.md`. Addresses below are rvas.

## The pad mask

`PadRead` calls `XInputGetState` into a stack `XINPUT_STATE` (`wButtons` at `rsp+24h`), then builds the game's 16-bit
mask one `test`/`or`/`cmove` at a time (`0x10d0aec`–`0x10d0bf8`) and stores it with `mov word ptr [rdi],ax` at
`0x10d0c2a`, `rdi` being its second argument:

| XInput button (`wButtons` bit) | Game mask bit |
|---|---|
| D-pad up (`0x0001`) | `0x0010` |
| D-pad down (`0x0002`) | `0x0040` |
| D-pad left (`0x0004`) | `0x0080` |
| D-pad right (`0x0008`) | `0x0020` |
| Start (`0x0010`) | `0x0008` |
| Back (`0x0020`) | `0x0001` |
| Left thumb (`0x0040`) | `0x0002` |
| Right thumb (`0x0080`) | `0x0004` |
| LB (`0x0100`) | `0x0400` |
| RB (`0x0200`) | `0x0800` |
| A (`0x1000`) | `0x4000` |
| B (`0x2000`) | `0x2000` |
| X (`0x4000`) | `0x8000` |
| Y (`0x8000`) | `0x1000` |
| Left trigger past 30 (`rsp+26h`) | `0x0100` |
| Right trigger past 30 (`rsp+27h`) | `0x0200` |

The sticks follow at `+6`..`+9`. Whether a non-Xbox pad maps the same way through XInput is `[NEEDS VERIFICATION]`.

## The keyboard state

`KeyboardRead` calls `GetKeyboardState` into a 256-byte stack buffer at `rsp+20h` (so key `n` is at `rsp+20h+n`),
clears its 15-byte state (second argument, `rbx`) and fills it:

- **`+0`, a 32-bit mask** of fixed keys (`0x10d0e24`–`0x10d0f14`): Escape `0x800` (by `mov`, before the rest), either
  Ctrl `0x1`, either Shift `0x2`, either Alt `0x4`, either Windows key `0x8`, Up `0x10`, Down `0x20`, Left `0x40`,
  Right `0x80`, Backspace `0x100`, Delete `0x200`, Enter `0x400`, Space `0x1000`.
- If either Windows key is down (`GetKeyState(0x5B/0x5C)`), it returns 0 with only the mask filled.
- **`+4`, a 32-bit count, and `+8`..`+0xD`, up to six key codes** (`0x10d0f6f`–`0x10d100e`): a loop over virtual keys
  8 to `0xEF` appends each key that is down (`mov byte ptr [rcx+rbx+8],dil`, `inc dword ptr [rbx+4]`), stopping at a
  count of 6 (`cmp ecx,6`). Keys in two bit tables (`0xF7E71000` for codes below 32, `0x1FFFFFE003FFFF` from `0xA6`)
  are skipped, as are Left Ctrl (`0xA2`) while Right Alt is down and Right Alt (`0xA5`) while Left Ctrl is down
  (`0x10d0fab`–`0x10d0fcd`); `0xA4`/`0xA5` are asked through
  `GetKeyState`. Letters (`0x41`–`0x5A`) are in neither table, so R (`0x52`) and T (`0x54`) appear in the list when
  held.
- **`+0xE`, one byte**, copied from a global that `0x10d1090` sets; what it holds is `[NEEDS VERIFICATION]`.
- It returns 1 after the loop. When the byte at `0x2f13ded` is 0, or the float at `0x27baabc` is below the constant at
  `0x17c9824`, it clears the state and returns 0 (what those two globals mean is `[NEEDS VERIFICATION]`).

Whether the game binds R or T to anything is `[NEEDS VERIFICATION]` (#58 asks the next session to confirm both free).
