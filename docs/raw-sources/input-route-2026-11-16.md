# How the game reads the keyboard and the pads (offline input spike)

- **Source:** our reading of the shipped exe as-is (ADR 0002), for issue #58. Tools: `tools/Metaphor.SignatureCheck`
  `--imports` (added in the commit that adds this note) and `--bytes`; `dumpbin /disasm /range` from MSVC
  14.51.36231 (Visual Studio 18); a throwaway scan, kept outside the repository, for `E8`/`E9` rel32 and rip-relative
  `lea` operands landing on a given rva in `.ecode` and `.text1`; .NET SDK 9.0.319 on Windows 11 x64. Nothing was
  patched, unpacked or run; the game was not launched.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

All addresses are rvas on this build. Code is read from the file on disk; whether it is the same in memory is
`[NEEDS VERIFICATION]` until a signature spike runs `input-probes.json` (on this build the 15 CriFs probes were,
`signature-spike-2026-11-15.md`).

## Result

The game reads input itself, once per frame, through two small functions in `.ecode`. A hook on each sees the state
after the game has turned it into its own bit masks, so the listener needs no import-slot patch.

| Name (ours) | rva | Arguments → return | What it does |
|---|---|---|---|
| `PadRead` | `0x10d0a00` | 32-bit pad number, pointer to the game's pad state → `al` | Calls `XInputGetState` for that pad, then writes the game's own 16-bit button mask at `+0` and the sticks at `+2`..`+9` (triggers become buttons past XInput's threshold of 30, `cmp byte ptr [rsp+26h],1Eh`) |
| `KeyboardRead` | `0x10d0de0` | a 32-bit value, pointer to a 15-byte keyboard state → `al` | Calls `GetKeyboardState`, clears the state (`+0` dword, `+4` qword, `+0xC` word, `+0xE` byte), then sets bits in the dword from virtual keys: `VK_ESCAPE` → `0x800`, `VK_LCONTROL`/`VK_RCONTROL` → `1`, `VK_LSHIFT`/`VK_RSHIFT` → `2`, `VK_LMENU` → `4`, and on; the rest of the state is `[NEEDS VERIFICATION]` |

Their callers:

- `PadRead` has one direct caller, `0x10c3ec5`, inside `0x10c3de0`: a loop over pad numbers that calls `PadRead`, then
  an optional callback the game keeps at `0x2f12e28` (called with the pad number, the state and the value at
  `0x2f12e30`; who sets it is `[NEEDS VERIFICATION]`), then copies the state into a per-pad table near `0x2f130e8`.
- `KeyboardRead` has one direct caller, `0x10ee365`, inside `0x10ee300`, which passes the global state at
  `0x2f13da0`.
- `0x10c3de0` (pads) and `0x10ee300` (keyboard) are called side by side at `0x1042589`/`0x104258e` and
  `0x1071245`/`0x107124a`; `0x10c3de0` also at `0x7e0348`. These are the per-frame poll sites (that they run every
  frame is `[NEEDS VERIFICATION]`).

Two signatures, each one match on disk (`--probes ... input-probes.json`, exit 0):

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   PadRead: 1 match at 0x10d0000 (rva 0x10d0a00)
ok   KeyboardRead: 1 match at 0x10d03e0 (rva 0x10d0de0)
2 of 2 signatures matched exactly once
```

## What the exe imports for input

`--imports` lists 45 DLLs. For input:

- **XInput:** `XINPUT1_4.dll` by ordinal: `ordinal 2` (slot `0x17c1348`) and `ordinal 3` (slot `0x17c1350`).
  `dumpbin /exports` of this machine's `System32\xinput1_4.dll` names ordinal 2 `XInputGetState` and ordinal 3
  `XInputSetState`. Each slot is read only by a thunk (`jmp [slot]` at `0x14b39d0`, `0x14b39d6`); the one call to the
  `XInputGetState` thunk is `0x10d0a21`, in `PadRead`.
- **Keyboard (USER32):** `GetKeyboardState` (called at `0x10d0e1c`, in `KeyboardRead`), `GetKeyState` (23 calls: `0x7b2264`,
  19 in `0x9b5c13`..`0x9cb0b3`, and three in `KeyboardRead` at `0x10d0f1b`, `0x10d0f31`, `0x10d0fed`), `GetMessageW`,
  `PeekMessageA`/`W`, `TranslateMessage`, `DispatchMessageA`/`W`, `SetWindowsHookExA`, `CallNextHookEx`.
- **Not imported:** `DINPUT8`, `xinput9_1_0`, `GetAsyncKeyState`, `GetRawInputData`, `RegisterRawInputDevices`. A
  case-insensitive search of the exe's bytes for those names found none either.
- **The game's own keyboard hook is not low-level.** The one `SetWindowsHookExA` call (`0x7ef10b`) passes
  `ecx = 2`, `WH_KEYBOARD` (`WinUser.h`, Windows SDK 10.0.26100.0: `WH_KEYBOARD 2`, `WH_KEYBOARD_LL 13`), with the
  thread from `GetCurrentThreadId` (slot `0x17c0368`, called at `0x7ef0f3`): a hook on the game's own thread, which
  Arcana's NVDA problem (`WH_KEYBOARD_LL`, `arcana-v0.1.0-alpha.3-2026-11-16.md`) doesn't apply to.
- **Steam:** `steam_api64.dll` imports include `SteamInternal_FindOrCreateUserInterface` (8 references) and the exe
  holds the string `SteamInput006`, so the game may ask Steam for `ISteamInput`. Whether it reads buttons through it
  is `[NEEDS VERIFICATION]`.
- A second set of imports, lower-case (`kernel32.dll`, `user32.dll`, `ntdll.dll`, …, slots near `0x14c00000`), sits
  apart from the game's; not examined.

## Script functions about input

From `squirrel-script-functions-all-2026-11-16.md`: `CMD_PAD_CHK_PRESS` `0x144c020`, `CMD_PAD_CHK_TRIG` `0x144c030`,
`CMD_PAD_PRESS` `0x144c040`, `CMD_PAD_TRIG` `0x144c050`, `PadGet` `0x90cfd0` (`mov ecx,0Bh; jmp 0x3b07e0`),
`PadIsTrigOK` `0x90cfe0`, `PadIsTrigOKCancel` `0x90d050`, `PadRumbleStart`/`Stop`, and the field's
`FldPlayerSetInputLock`/`FldPlayerIsInputLock`. `PadIsTrigOK` asks `0x7ae520` and `0x7b3390` with an action number
(4, then 1), so the game maps buttons to actions above the raw read; that layer is a later lead for swallowing a press,
`[NEEDS VERIFICATION]`.

## The listener

`src/Metaphor.Hooks/InputListeners.cs`, development-only, both through `IObserves4WithResult` (an existing shape;
`shared-delegate-hook-spike-shapes-6-and-result-2026-11-16.md`), so no new shape. After the read returns, each logs
to `message-log.txt` only when the state differs from the last read of that pad or of the keyboard:
`input pad<n> buttons=0x<mask> returned=<al>` and
`input keyboard first=<n> keys=0x<dword> +4=0x… +8=0x… +c=0x… returned=<al>`. No game text.

## What Steam Input changes

Arcana patched the `XInputGetState` import slot because Steam Input overwrote its Detours hook on the function body
(`arcana-v0.1.0-alpha.3-2026-11-16.md`, `src/input.cpp` L455-471). Our hooks are on the game's own functions, not on
`XInputGetState`, so Steam rewriting XInput's code shouldn't reach them; if the game reads pads through `ISteamInput`
instead of XInput when Steam Input is on, `PadRead` may see nothing. Both are `[NEEDS VERIFICATION]`: the session tests
with Steam Input on and off.

## For the session

1. `pwsh scripts/signature-spike.ps1 -Probes tools/Metaphor.SignatureCheck/input-probes.json`: both probes at the same
   place in memory.
2. A Debug run (`pwsh scripts/listen.ps1` or `deploy-dev.ps1`) with Steam Input **off** for the game, a pad connected:
   press each face button, the d-pad, shoulders, triggers, Start and Back one at a time, and each of Escape, Ctrl,
   Shift, Alt, Enter and the arrow keys; note which `input` lines change, and which bit each makes.
3. The same with Steam Input **on** (Steam: the game's Properties, Controller), and with a non-Xbox pad if one is
   at hand: does `pad0` still change, under which pad number, and do the bits match?
4. Note which buttons and keys do nothing during a dialogue line: the defaults for #58 come from those.
