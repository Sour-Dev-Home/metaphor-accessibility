# Where `MsgOpen` and `MsgClose` go (issue #58)

- **Source:** our reading of the shipped exe as-is (ADR 0002): `dumpbin /disasm /range` from MSVC 14.51.36231 (Visual
  Studio 18), image base `0x140000000`; `tools/Metaphor.SignatureCheck --probes` with
  `tools/Metaphor.SignatureCheck/msg-window-probes.json`; .NET SDK 9 on Windows 11 x64. Script function addresses from
  `squirrel-script-functions-all-2026-11-16.md`. Nothing was patched or run; the game was not launched.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Addresses are rvas; the names ending in `Body` are ours.

## The jumps

- `MsgOpen` (`0x90c1b0`) is `E9 6B CC 74 09`, a `jmp` to `0xa058e20` in `.text1`, followed by bytes that don't decode
  (as `msg-request-functions-2026-11-16.md` found).
- `MsgClose` (`0x90be80`) is `E9 6B B8 0E 00`, a `jmp` to `0x9f76f0`, which is itself only a `jmp` to `0xa79deb0` in
  `.text1`, followed by bytes that don't decode.

## The bodies

- **`MsgOpenBody`, `0xa058e20`:** `push rbx; sub rsp,60h; mov rbx,rcx; cmp byte ptr [rcx],2Fh; jne …`: one argument,
  a pointer to a C string (it scans it for its terminating 0 at `0xa058e90`). It reads a global from `0x118e5bd1`
  (`mov eax,[…]; add eax,…`) on the `/` path. What it opens, a window or a message file, is `[NEEDS VERIFICATION]`.
- **`MsgCloseBody`, `0xa79deb0`:** `mov [rsp+8],ecx; push rsi; push rdi; sub rsp,38h; mov rsi,[0x2f04978]; mov edi,ecx;
  test rsi,rsi; je …`: one 32-bit argument (it hashes its low byte, `movzx edx,dil`). Whether `0x9f76f0` has callers
  other than `MsgClose` is `[NEEDS VERIFICATION]`.
- Neither return value was traced: `[NEEDS VERIFICATION]`.

That the window is open from `MsgOpen` until `MsgClose` is #58's design decision, for a session to confirm.

## Signatures

Both bodies match exactly once on disk; the `add eax,…` constant and the rip-relative addresses are wildcards:

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   MsgOpenBody: 1 match at 0x9706a20 (rva 0xa058e20)
ok   MsgCloseBody: 1 match at 0x9e4bab0 (rva 0xa79deb0)
2 of 2 signatures matched exactly once
```

Not checked in memory yet. Two earlier hooks in `.text1` (`CreateMsgFromString`, `TextPaneDraw`) ran for whole
sessions with no crash (`spike-session-1-2026-11-16.md`, `spike-session-2-2026-11-16.md`).
