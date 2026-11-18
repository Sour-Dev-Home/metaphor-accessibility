# Reading whether the message window is open, without a `.text1` hook (issue #58)

- **Source:** our reading of the shipped exe as-is (ADR 0002): `dumpbin /disasm /range` and `/headers` from MSVC
  14.51.36231 (Visual Studio 18), image base `0x140000000`; `tools/Metaphor.SignatureCheck --probes` with
  `tools/Metaphor.SignatureCheck/dialogue-window-probes.json`; .NET SDK 9 on Windows 11 x64. Script function addresses
  from `squirrel-script-functions-all-2026-11-16.md`; `MsgOpen`/`MsgClose` targets from
  `msg-request-functions-2026-11-16.md`. The `.text1` bodies were read on disk only, up to their `ret`; the bytes after
  them don't decode and were not followed. Nothing was patched or run; the game was not launched.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Addresses are rvas. Names ending in `Body`, and the names `W` and `M`, are ours. Nothing here has been seen in a
running game: every meaning below is `[NEEDS VERIFICATION]` until the session steps at the end run.

## Result

Two global pointers, both read by `.ecode` code, hold what `MsgOpen`/`MsgClose` and the window itself change. Both sit
in section 3 (named `.debug`, the data section) past its raw data, so they are zero on disk and set at run time.

| Global | rva | Read by (in `.ecode`) | What it holds |
|---|---|---|---|
| `W`, the message window | `0x2f04960` | `0x9ea970` (`MsgIsSelectWindowOpened`), `0x9ea0d0`, `0x9eaa00`, 162 reads in `0x9d0000`..`0xa10000` | `W+0x60`: the message on screen, or 0; `W+0x560`: a state number |
| `M`, the message manager | `0x2f04978` | `0x9f7ac0` (what `MsgCheckEnd` calls), `0x9f7680`, `0x9f76b0`, `0x9f7c50` | two hash maps of open handles, the ones `MsgOpen` adds and `MsgClose` removes |

Pointer chains the PadRead hook can read each frame through `ModuleMemoryReader`:

- **Window has a message:** `[[base+0x2f04960]+0x60] != 0`.
- **Window state:** `[[base+0x2f04960]+0x560]` (32-bit).
- **Open handles (what PR #153's hooks track):** `[[base+0x2f04978]+0x18]` and `[[base+0x2f04978]+0x58]`.

## Evidence: the window object `W`

- **Set when a message is shown.** In the function holding `0x9ed74b`..`0x9ed853`, which calls `0x9eddc0` at
  `0x9ed84e`: `mov [rbx+48h],edx` at `0x9ed788` (edx = 1),
  then `mov qword ptr [rsi+60h],rbx` at `0x9ed7a1` (rsi = `W`, rbx = the message) and again at `0x9ed813`.
  `0x9eddc0(W, kind, message, …)` stores `kind` to `W+0x560` at `0x9ee0f1`; it accepts kinds 1 and 5 only
  (`lea eax,[rdx-1]; test eax,0FFFFFFFBh`).
- **Cleared when it closes.** `0x9e3040`: `mov rax,[rcx+60h]; mov [rcx+68h],rax; mov qword ptr [rcx+60h],0;
  mov dword ptr [rcx+560h],6; ret`. The same three stores appear inline at `0x9ef772`..`0x9ef780` and
  `0x9efc03`..`0x9efc0f`. So `W+0x68` keeps the last message and state 6 follows a close.
- **Other states.** `W+0x560` is set to 4 at `0x9eb408` and `0x9f0212`, to 5 (with `W+0x60` set) at `0x9ebab2`, and
  restored from a saved value at `0x9f04f3` and `0x9f06d0`. `MsgIsSelectWindowOpened` (`0x9ea970`) returns false
  unless the state is 4 or 5, so 4 and 5 are the choice window's. What 1 and the restore mean is
  `[NEEDS VERIFICATION]`.
- **The game's own check.** `MsgCheckWindowClosed` jumps to `0xa6cb260` (`.text1`, read only): closed if `W` is 0 or
  `[W+0x60]` is 0; otherwise, unless the state is 5, not closed while `[[W+0x60]+0x48] != 0`, then it asks the window
  part at `W+0x318` (`0x9fb230`). That is the reading above, minus the animation check.

## Evidence: the manager `M`, and what `MsgOpen`/`MsgClose` really do

- `MsgOpenBody` (`0xa058e20`) only builds a string from its path argument and calls `0x9f7c50(name, 1)` in `.ecode`.
  `0x9f7c50` loads `M` (`mov rsi,[0x2f04978]`), looks the name up, and on a hit bumps a reference count
  (`inc dword ptr [rax+8]`); on a miss it allocates a `0x18`- or `0x40`-byte object and inserts it in one of two maps.
- `MsgCloseBody` (`0xa79deb0`) takes a handle id, hashes it (FNV-1a: `0CBF29CE484222325h`, `100000001B3h`), finds it in
  the map at `M+0x48` or the one at `M+0x08`, drops a reference (`0x13e3f30`) and, at zero, frees the object and
  erases the node (`dec qword ptr [rsi+18h]` for the `M+0x08` map).
- The maps are laid out like MSVC's `std::unordered_map` (list sentinel, then the bucket vector, then the mask):
  the `M+0x08` map's sentinel is `M+0x10`, buckets `M+0x20`, mask `M+0x38`, size `M+0x18` (the decrement above); the
  `M+0x48` map's sentinel is `M+0x50`, buckets `M+0x60`, mask `M+0x78`, so its size at `M+0x58` is by analogy,
  `[NEEDS VERIFICATION]`.
- So `MsgOpen`/`MsgClose` open and release reference-counted message *handles* (likely message files), not the window
  as such. Whether a handle's lifetime matches the dialogue window's on screen is `[NEEDS VERIFICATION]`; the window
  pointer `W+0x60` is the closer match for "a line is up".

## Signatures

Both functions are in `.ecode` and are only read, never hooked: the listener takes the rip-relative operand of each
function's first instruction (`48 8B 0D disp32`, 7 bytes; `48 83 3D disp32 00`, 8 bytes) to find the globals.

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   MsgIsSelectWindowOpenedBody: 1 match at 0x9e9f70 (rva 0x9ea970)
ok   MsgCheckEndBody: 1 match at 0x9f70c0 (rva 0x9f7ac0)
2 of 2 signatures matched exactly once
```

On disk, `0x9ea970` is `48 8B 0D E9 9F 51 02` (target `0x9ea977 + 0x2519fe9 = 0x2f04960`) and `0x9f7ac0` is
`48 83 3D B0 CE 50 02 00` (target `0x9f7ac8 + 0x250ceb0 = 0x2f04978`). Not checked in memory yet.

## The listener

`PadReadListener` (`src/Metaphor.Hooks/InputListeners.cs`, development-only) lists both signatures as `Calls`, so the
loader installs it only when both match once; it never calls them. On each pad read it formats
`DialogueWindowWatch.Describe()` and logs `msgwindow current=0x… state=N msg+48=N handles=A+B` to `message-log.txt`
only when that changes. No game text. No new hook, and none in `.text1`.

## For the session

1. `pwsh scripts/signature-spike.ps1 -Probes tools/Metaphor.SignatureCheck/dialogue-window-probes.json`: both
   probes at the same place in memory.
2. A Debug run (`pwsh scripts/listen.ps1`). The hook summary should still count `PadReadListener` as installed.
3. Talk to someone through a few lines, a choice, and out. For each, note the `msgwindow` lines against the `request`
   lines: does `current` go non-zero when a line appears and back to `0x0` when the window goes away, or between every
   line? Which `state` values show up for a line, a choice and a close? Does `msg+48` drop to 0 when the line is done?
4. Note whether `handles=A+B` changes at the start and end of a conversation, and whether it ever reaches `0+0` in the
   field (if not, some handles stay open and the counts can't stand in for "window open").
5. With #153 deployed too, compare its `MsgOpenBody`/`MsgCloseBody` events with these lines; if `current` (or the
   state) tracks the window as well, #153 can read it in its PadRead hook and drop both `.text1` hooks.
