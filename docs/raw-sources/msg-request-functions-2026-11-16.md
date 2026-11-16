# What the `MsgRequest…` script functions call (issue #55, step 2)

- **Source:** our own reading of the game executable's bytes on disk, as shipped (ADR 0002), on the maintainer's
  machine. No disassembler: a PowerShell read of the file at each address, mapped through the PE section table
  (`System.Reflection.PortableExecutable.PEHeaders`), decoded by hand. Addresses of the script functions come from
  `squirrel-script-functions-all-2026-11-16.md`.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

All addresses are relative to the game module. The inner functions' names below are ours, not the game's.

## The script functions are thin wrappers

Each `MsgRequest…` function is 40–80 bytes: it zeroes a 12-byte options block on the stack, sets a kind, and calls one
of a few shared inner functions. Example, `MsgRequestSubtitles` at `0x90c820`:

```text
48 83 EC 48                 sub  rsp, 0x48
48 8B D1                    mov  rdx, rcx            ; its one argument, 64 bits
48 C7 44 24 20 00 00 00 00  mov  qword [rsp+0x20], 0 ; options, zeroed (copied to rsp+0x30)
...
4C 8D 44 24 30              lea  r8, [rsp+0x30]      ; -> options
B9 02 00 00 00              mov  ecx, 2              ; kind
E8 E8 B9 0E 00              call 0x9f8240
```

| Script function | Address | Calls | With |
| --- | --- | --- | --- |
| `MsgRequest` | `0x90c420` | `0x9f8240` | kind 0, its argument, zeroed options |
| `MsgRequestSystemMessage` | `0x90c8b0` | `0x9f8240` | kind 1, its argument, zeroed options |
| `MsgRequestSubtitles` | `0x90c820` | `0x9f8240` | kind 2, its argument, zeroed options |
| `MsgRequestNarration` | `0x90c580` | `0x9f8240` | kind 2, its argument, options with byte 6 set to 1 |
| `MsgRequestAuto` | `0x90c4b0` | `0x9f8910` | kind 0, its argument, its second argument (in `xmm1`), zeroed options |
| `MsgRequestID` | `0x90c4f0` | `0x9f7fd0` | kind 0, its two 32-bit arguments, zeroed options |
| `MsgRequestSystemMessageID` | `0x90c8f0` | `0x9f7fd0` | not decoded |
| `MsgRequestIDAuto` | `0x90c530` | `0x9f86a0` | kind 0, its two 32-bit arguments, its third (in `xmm2`), options on the stack |
| `MsgRequestSelect` | `0x90c5d0` | `0x9f8d70` | kind 0, its two 64-bit arguments, -1, options on the stack |
| `MsgRequestSelectID` | `0x90c6c0` | `0x9f8b30` | not decoded |

- `MsgRequest`, `MsgOpen` and `MsgRequestSelect` start with a jump into the `.text1` section (`0xa05f3e0`,
  `0xa058e20`, `0xa05f510`), followed by bytes that don't decode. The code at each target is ordinary and reads like
  its neighbours; the rows above are read from there. Why they were moved is `[NEEDS VERIFICATION]`.
- `MsgOpen`'s code begins `80 39 2F` (`cmp byte [rcx], '/'`): its argument is a C string. That the single argument
  of `MsgRequest` and its siblings is a string too is `[NEEDS VERIFICATION]`.
- The width of the `xmm` argument (32-bit or 64-bit float) is `[NEEDS VERIFICATION]`: the wrappers copy the whole
  register.

## The inner functions

| Our name | Address | Arguments, as its callers and first instructions show |
| --- | --- | --- |
| `MsgRequestByName` | `0x9f8240` | kind (32 bits, `mov r14d, ecx`), a pointer (`mov rsi, rdx`), options pointer (`mov rdi, r8`) |
| `MsgRequestByNameTwin` | `0x9f8470` | the same code; who calls it is `[NEEDS VERIFICATION]` |
| `MsgRequestById` | `0x9f7fd0` | kind, two 32-bit numbers, options pointer |
| `MsgRequestByNameAuto` | `0x9f8910` | kind, a pointer, a float, options pointer |
| `MsgRequestByIdAuto` | `0x9f86a0` | kind, two 32-bit numbers, a float, options pointer (on the stack) |

- `0x9f8240` and `0x9f8470` are the same `0x230` bytes except for ten bytes of relative addresses, which resolve to
  the same targets. No position-independent pattern separates them; the probes include the first relative address
  (`F8 C6 50 02` and `C8 C4 50 02`), which is specific to this build.
- None of them sets a return value of its own. Each ends by calling one deeper function and returning: `0x9f8240`
  calls `0x9ec320` (kind, the pointer, a larger options block); `0x9f7fd0` calls `0x9ebed0` (kind, the two numbers,
  a larger options block). What those return is `[NEEDS VERIFICATION]`.
- Probes: `tools/Metaphor.SignatureCheck/msg-request-probes.json`. All five match exactly once on disk:

```text
ok   MsgRequestByName: 1 match at 0x9f7840 (rva 0x9f8240)
ok   MsgRequestByNameTwin: 1 match at 0x9f7a70 (rva 0x9f8470)
ok   MsgRequestById: 1 match at 0x9f75d0 (rva 0x9f7fd0)
ok   MsgRequestByNameAuto: 1 match at 0x9f7f10 (rva 0x9f8910)
ok   MsgRequestByIdAuto: 1 match at 0x9f7ca0 (rva 0x9f86a0)
```

- The decoded wrappers don't read a result after the call (`call; add rsp, 0x48; ret`); they pass it on.
- Whether any inner function reads an argument beyond those listed is `[NEEDS VERIFICATION]`: the lists come from
  the callers and the first instructions only.

Which of these a spoken event line goes through, and what the arguments mean, is for a listening run to answer.
