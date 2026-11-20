# The getter that reads the applied voice language (issue #538)

- **Source:** our reading of the shipped exe as-is (ADR 0002) in the local Ghidra project
  (`docs/contributors/how-to/query-the-ghidra-project.md`): Ghidra 12.2 with Temurin JDK 25.0.5.8, through
  `scripts/dev/ghidra.ps1` `global`, `disasm` and `callers`; bytes and the signature check from
  `tools/Metaphor.SignatureCheck` (`--bytes`, then `bash scripts/check.sh signatures`), .NET SDK 9 on Windows 11 x64.
  The 32-bit `mov` row is from Félix Cloutier's x86 reference, extracted from Intel's Software Developer's Manual,
  vol. 2: <https://www.felixcloutier.com/x86/mov>, read with a web fetch tool. Nothing was patched or run; the game was
  not launched. `.text1` was not followed.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Addresses are rvas. `VoiceLanguageGetter` is our name.

## Why

Game session 1 found the applied voice language at `base+0x2dff8e4`: 2 for English, 1 for Japanese, changed at once by
the setting and kept after a relaunch (`game-session-1-2026-11-20.md`, `docs/findings/voice-language-values.md`). The
static note (`docs/findings/title-names-voice-tutorials-static.md`, section 4) has the writers. The mod should find the
global from code that reads it, as it does the dialogue window and menus-open globals (#300), not from a fixed offset.

## What the exe holds

`global 0x2dff8e4` lists three instructions in analysed code: one read and two writes.

| At | Kind | Function |
|---|---|---|
| `0x3dc620` | read: `mov eax,[rip+disp32]` | `FUN_1403dc620`, which is that one instruction then `ret` |
| `0x3dcbb0` | write: `mov [rip+disp32],ecx` | `FUN_1403dcbb0`, the copy at the end of `appChangeVoiceLanguage` |
| `0x3dcd73` | write: `mov [rip+disp32],eax` | `FUN_1403dcc20`, which fills the settings block |

`callers 0x3dc620`: `0x73321b` in `FUN_1407331d0` and `0x733c74` in `FUN_1407338a0`.

The bytes at `0x3dc610` to `0x3dc65f` (`--bytes`, 0x60 bytes from `0x3dc600`) are three such getters, each 7 bytes
padded with `CC` to 16, then a function that starts `40 53 48 83 EC 30 48 8B D9 48 8B D1 48 8D 0D`. The getter at
`0x3dc620` is `8B 05 BE 32 A2 02 C3`: its displacement `0x02a232be` plus its end, `0x3dc626`, is `0x2dff8e4`.

## The signature

`VoiceLanguageGetter`, in `.ecode`: the getter, its padding, the next getter and its padding, and the next function's
first 15 bytes, with both displacements wildcards:

```text
8B 05 ?? ?? ?? ?? C3 CC CC CC CC CC CC CC CC CC 8B 05 ?? ?? ?? ?? C3 CC CC CC CC CC CC CC CC CC 40 53 48 83 EC 30 48 8B D9 48 8B D1 48 8D 0D
```

`bash scripts/check.sh signatures`: `ok   VoiceLanguageGetter: 1 match at 0x3dbc20 (rva 0x3dc620)`, 29 of 29
signatures matched exactly once. Load at 0, `disp32` at 2, length 6.

The getter alone (7 bytes) is too short to be unique; the pattern spans its neighbours, so a build that moves them
breaks it. The build check turns the reader off then, and the voice language reads as unknown, as before #538.

## The instruction

Félix Cloutier's MOV page: "8B /r | MOV r32, r/m32 | RM | Valid | Valid | Move r/m32 to r32.", and "In 64-bit mode,
the instruction's default operation size is 32 bits." So `8B` without a REX prefix, with a ModR/M byte of mod 00 and r/m
101 (`05` for `eax`), is a 32-bit `mov reg,[rip+disp32]` of 6 bytes, the shape `x86-rip-relative-loads-2026-11-19.md`
describes for the 64-bit form, less the REX byte.

Not checked: that the getter's two callers read it for voice cues. `[NEEDS VERIFICATION]` in memory, the same as the
other signatures' "not yet checked in memory": the session-1 reads were at the address, not through this signature.
