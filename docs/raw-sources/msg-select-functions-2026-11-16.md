# What `MsgRequestSelect` and `MsgGetSelectedID` call (issues #93 lead 2, #82)

- **Source:** our own reading of the game executable's bytes on disk, as shipped (ADR 0002), on the maintainer's
  machine, with `tools/Metaphor.SignatureCheck --bytes` (this repository, commit `e549dda`), decoded by hand. Script
  function addresses come from `squirrel-script-functions-all-2026-11-16.md`.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Addresses are relative to the game module. The inner functions' names are ours, not the game's.

## Steps

```sh
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" 0x90c5d0 32
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" 0xa05f510 112
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" 0x9f8d70 256
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" 0x9f8e70 1024
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" 0x90bf80 96
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" 0x9e89f0 96
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" 0x9fb150 128
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --probes "$METAPHOR_GAME_EXE" tools/Metaphor.SignatureCheck/msg-select-probes.json
```

## `MsgRequestSelect` (`0x90c5d0`)

It starts `E9 3B 2F 75 09`, a jump to `0xa05f510` in `.text1`; the bytes after it don't decode (as in
`msg-request-functions-2026-11-16.md`). The code there:

```text
48 83 EC 58           sub   rsp, 0x58
45 31 C0              xor   r8d, r8d
44 88 44 24 38        mov   [rsp+0x38], r8b
8B 44 24 38           mov   eax, [rsp+0x38]
4C 89 44 24 30        mov   [rsp+0x30], r8
C5 FB 10 44 24 30     vmovsd xmm0, [rsp+0x30]          ; 8 zero bytes
66 44 89 44 24 28     mov   [rsp+0x28], r8w            ; 6th argument: 16 bits, 0
45 8D 48 FF           lea   r9d, [r8-1]                ; 4th argument: 32 bits, -1
49 89 D0              mov   r8, rdx                    ; 3rd: the script's 2nd argument, 64 bits
89 44 24 48           mov   [rsp+0x48], eax            ; options bytes 8-11
48 89 CA              mov   rdx, rcx                   ; 2nd: the script's 1st argument, 64 bits
48 8D 44 24 40        lea   rax, [rsp+0x40]
31 C9                 xor   ecx, ecx                   ; 1st: kind 0
48 89 44 24 20        mov   [rsp+0x20], rax            ; 5th: options pointer
C5 FB 11 44 24 40     vmovsd [rsp+0x40], xmm0          ; options bytes 0-7
E8 1A 98 99 F6        call  0x9f8d70
48 83 C4 58 C3        add rsp, 0x58; ret               ; passes on what it returned
```

So `0x9f8d70` takes **six** arguments, not five: kind, two 64-bit values, -1, an options pointer, and a 16-bit 0 on the
stack. (`msg-request-functions-2026-11-16.md` listed "-1, options on the stack" and didn't mention the sixth.)

## `MsgRequestSelectByName` (`0x9f8d70`)

Prologue `48 8B C4 … 48 8D A8 38 FF FF FF 48 81 EC A0 01 00 00` (`rbp` = entry `rsp` - 0xc8), then:

```text
41 8B F9              mov   edi, r9d     ; 4th, 32 bits
49 8B F0              mov   rsi, r8      ; 3rd, 64 bits
4C 8B F2              mov   r14, rdx     ; 2nd, 64 bits
44 8B F9              mov   r15d, ecx    ; kind, 32 bits
```

- `0x9f8efa`: `48 8B 8D F0 00 00 00` `mov rcx, [rbp+0xf0]` reads the 5th argument (entry `rsp`+0x28), then tests bytes
  4, 7 and 8 of the block it points to.
- `0x9f8f94`: `44 0F B7 8D F8 00 00 00` `movzx r9d, word [rbp+0xf8]` reads the 6th argument (entry `rsp`+0x30), 16 bits.
- `0x9f8f9b`: `4C 8B C6 49 8B D6 41 8B CF` passes the 2nd and 3rd arguments and the kind on unchanged, with a pointer
  to a larger block on its stack, to `call 0x9ee5e0`; it then restores registers and returns without touching `rax`,
  so it returns what `0x9ee5e0` returns. What that is `[NEEDS VERIFICATION]`.
- The 4th argument is stored (`89 7C 24 64`, `89 7D F4`); what it means is `[NEEDS VERIFICATION]`.
- That the 2nd and 3rd arguments are C strings (the prompt's and the choice's labels) is `[NEEDS VERIFICATION]`: it is
  #82's lead, by analogy with `MsgRequestByName`.

## `MsgGetSelectedID` (`0x90bf80`)

`E9 6B CA 0D 00`: a single jump to `0x9e89f0` (also `.ecode`; the next bytes are `CC` padding). At `0x9e89f0`:

```text
48 8B 0D 69 BF 51 02  mov   rcx, [rip+0x251bf69]   ; a global object
48 85 C9 74 0C        test  rcx, rcx; jz +0x0c
48 81 C1 18 03 00 00  add   rcx, 0x318
E9 48 27 01 00        jmp   0x9fb150
B8 FF FF FF FF C3     mov   eax, -1; ret           ; no object: -1
```

It takes no argument and returns a 32-bit value; -1 when the object is missing. The listener hooks `0x9fb150`, not
`0x9e89f0`: the latter is 30 bytes and starts with a RIP-relative load, and whether Reloaded.Hooks relocates that
correctly is `[NEEDS VERIFICATION]`.

## `MsgSelectedId` (`0x9fb150`)

```text
48 83 EC 28           sub   rsp, 0x28
8B 81 00 02 00 00     mov   eax, [rcx+0x200]   ; one argument: a pointer
83 F8 06 75 0B        cmp eax, 6; jne
8B 81 04 02 00 00     mov   eax, [rcx+0x204]
83 F8 06 74 16        cmp eax, 6; je  -> call 0x507d10; jmp 0xf48c80
83 F8 05 75 11        cmp eax, 5; jne -> the same
E8 …                  call 0x8fe9c0; mov rcx, rax; add rsp, 0x28; jmp 0xf97c10
```

One pointer argument; it picks one of two objects by a state at `+0x200`/`+0x204` and tail-jumps to that object's
function, whose return it passes on. The return is read as 32 bits, after the `mov eax, -1` path above; its width
from the tail targets is `[NEEDS VERIFICATION]`. It sets no register but `rcx` before its calls, and its only caller on this path
(`0x9e89f0`) sets only `rcx`, so a hook that passes `rcx` on drops nothing the code reads. Whether code other than `MsgGetSelectedID` calls `0x9fb150` is
`[NEEDS VERIFICATION]`.

## Signatures

`tools/Metaphor.SignatureCheck/msg-select-probes.json`, on disk:

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   MsgRequestSelectByName: 1 match at 0x9f8370 (rva 0x9f8d70)
ok   MsgSelectedId: 1 match at 0x9fa750 (rva 0x9fb150)
2 of 2 signatures matched exactly once
```

Not yet checked in the running game (`scripts/signature-spike.ps1`).
