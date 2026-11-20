# The one function that puts a line in the message window (issue #526)

- **Source:** our reading of the shipped exe as-is (ADR 0002) in the local Ghidra project
  (`docs/contributors/how-to/query-the-ghidra-project.md`): Ghidra 12.2 with Temurin JDK 25.0.5.8, through
  `scripts/dev/ghidra.ps1` `decompile`, `disasm` and `callers`; bytes and the signature check from
  `tools/Metaphor.SignatureCheck` (`--bytes`, then the default run over `hook-signatures.json`), .NET SDK 9 on Windows
  11 x64. Nothing was patched or run; the game was not launched. Only `.ecode` and other analysed code was read; `.text1`
  was not followed.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Addresses are rvas. `MsgWindowShowLine` and the other names without an address-derived Ghidra name are ours. `W` is
the message window global at `0x2f04960` (`dialogue-window-flag-2026-11-16.md`).

## Why

Game session 1 (`game-session-1-2026-11-20.md`, stop 6): the scene after the opening cutscene showed its lines in a live
window (`[W+0x60]` non-null) with no `MsgRequestByName` call, and no record from the by-id, auto, twin, serial or label
listeners either. So the lines reach the window by a route none of those hooks sits on.

## Result

`0x9ed360` (`MsgWindowShowLine`) is the function every message request ends in. It takes the window (`rcx`, the value of
`W`), a request block (`rdx`) and a byte flag (`r8b`).

- **What it does.** It looks up a message object by the block's first 32-bit field (`call 0x9e7460` with `W` and
  `[block]`), returns 0 unless the block's third field is an index below the object's line count (a vector at
  `+0x20`..`+0x28` with `0x70`-byte entries), and on the shown path stores the object to `W+0x60` (`0x9ed7a1` and
  `0x9ed813`, the two stores `dialogue-window-flag-2026-11-16.md` found) before calling `0x9ed9d0` or `0x9eddc0`, each
  of which has no other caller. When the window is busy (`W+0x1f8` non-zero) it hands the block to `0x9e19c0` with
  `W+0x90` instead (its only caller). Where that queue is drained is `[NEEDS VERIFICATION]`.
- **The block.** Field 1 (`+4`) is the request's kind, the first argument of every `MsgRequest…` inner function
  (`MsgRequestByName`'s `0x9ec320` stores its `param_1` there). The 16 bytes at `+0x98` (dword index `0x26`, which the
  function reads at its end) are the MD5 of the line's label.
- **The label hash is MD5.** `0x13d6e70`, which `0x9ec320` uses on the label it was passed, sets up the four MD5
  initial words (`0x67452301`, `0xefcdab89`, `0x98badcfe`, `0x10325476`), measures the C string, and calls an update and
  a final step. `0x9ec320` fills the block's hash with the MD5 of the empty string first (the four words
  `0xd98c1dd4`, `0x04b2008f`, `0x980980e9`, `0x7e42f8ec`, which are `d41d8cd9…ecf8427e` in memory order), then
  overwrites it with the label's. The window's own lookup (`0x9e60f0`, with `W`) takes the same hash.
- **Its callers.** `callers 0x9ed360` lists ten functions, each building a block and calling it once:

  | Caller | Reached from |
  |---|---|
  | `0x9ec320` | `MsgRequestByName` (`0x9f8240`) and over 100 native call sites (menus, UI classes, field code): by label |
  | `0x9ec670` | `MsgRequestByNameTwin` (`0x9f8470`) and five native callers |
  | `0x9ebed0` | `MsgRequestById` (`0x9f7fd0`) |
  | `0x9ecf20` | `MsgRequestByNameAuto` (`0x9f8910`) |
  | `0x9ecb90` | `MsgRequestByIdAuto` (`0x9f86a0`) |
  | `0x9ec910` | camp item and skill menus, UI event lambdas |
  | `0x9eb890` | the select (choice) path |
  | `0x9ec100` | no caller in analysed code; takes a 16-byte label hash and copies it to the block's `+0x98` |
  | `0x9ed120` | no caller in analysed code; takes a 16-byte label hash, copies it to `+0x98` |
  | `0x9ecd60` | no caller in analysed code |

  The by-hash routes (`0x9ec100`, `0x9ed120`) look the line up in the window by that hash, as `0x9ec320` does with the
  label's MD5, so their hash is a label's MD5 too.

## Not known

- Which caller the session-1 scene's lines took: `[NEEDS VERIFICATION]`. The scripted routes all have hooks that logged
  nothing, which leaves a native caller of `0x9ec320`/`0x9ec670`/`0x9ec910` or one of the three with no analysed caller
  (reached from code Ghidra didn't analyse). Hooking `0x9ed360` covers every one.
- The kind those lines carry, and whether a line can reach `W+0x60` without `0x9ed360` (from `.text1`, which wasn't
  read): `[NEEDS VERIFICATION]`. A Debug build logs each call as a `request via=MsgWindowShowLine kind=… hash=…` record,
  so a session answers both.

## Signature

The first 70 bytes, with the call and the `jz` targets as wildcards:

```text
48 89 5C 24 08 44 88 44 24 18 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 80 00 00 00 C5 F8 29 74 24 70 4C 8B F2
48 8B F1 8B 12 E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B 50 28 48 2B 50 20
```

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   MsgWindowShowLine: 1 match at 0x9ec960 (rva 0x9ed360)
28 of 28 signatures matched exactly once
```

Not checked in memory yet.
