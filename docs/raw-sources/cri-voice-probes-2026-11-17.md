# CRI ADX2 volume, category and cue-sheet functions for #103, checked on disk

- **Source:** Ryo Framework, <https://github.com/RyoTune/Ryo>, commit `1d661e153ad46efa0707379bd9fd61255f357a2c`, fetched
  with `gh api` on 2026-11-17: `Ryo.Reloaded/CRI/CriAtomEx/CriAtomExPatterns.cs` (the `"metaphor"` entry) and
  `Ryo.Interfaces/Functions/CriAtomExFunctions.cs`. Licence: Ryo's "General Mod License"; only names, byte patterns and
  delegate signatures are quoted, no code is reused. The same commit as
  `docs/raw-sources/ryo-cri-atom-patterns-2026-11-16.md`, which lists these names without their patterns.
- **Tool:** `tools/Metaphor.SignatureCheck` (this repository), `--probes` and `--bytes`, run 2026-11-17.
- **Saved:** 2026-11-17, for issue #103 (voice volume, voice language, voice cues).
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Quoted from Ryo

Patterns, `"metaphor"` entry:

```text
criAtomExPlayer_SetCategoryById = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 50 48 8B F9 8B F2"
criAtomExPlayer_SetVolume = "40 53 48 83 EC 30 0F 29 74 24 ?? 0F 28 F1 48 8B D9 48 85 C9 75 ?? 48 8D 15 ?? ?? ?? ?? 41 B8 FE FF FF FF 33 C9 E8 ?? ?? ?? ?? EB ?? 0F 57 C0 F3 0F 5A C6 E8 ?? ?? ?? ?? 85 C0 74 ?? 48 8D 15 ?? ?? ?? ?? EB ?? F3 0F 11 74 24 ?? 33 D2"
criAtomExAcb_LoadAcbData = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 50 33 F6"
criAtomAwb_LoadToc = "40 53 48 83 EC 20 83 25 ?? ?? ?? ?? 00"
criAtomExCategory_GetVolumeById = "40 53 48 83 EC 20 8B D9 33 C9 E8 ?? ?? ?? ?? 85 C0 75 ?? 48 8D 15 ?? ?? ?? ?? 33 C9 E8 ?? ?? ?? ?? F3 0F 10 05"
criAtomExCategory_SetVolumeById = "40 53 48 83 EC 30 8B D9 0F 29 74 24 ?? 33 C9"
```

Delegates:

```text
public delegate nint criAtomExAcb_LoadAcbData(nint acbData, int acbDataSize, nint awbBinder, nint awbPath, void* work, int workSize);
public delegate float criAtomExCategory_GetVolumeById(uint categoryId);
public delegate void criAtomExPlayer_SetVolume(nint playerHn, float volume);
public delegate void criAtomExCategory_SetVolumeById(uint id, float volume);
public delegate void criAtomExPlayer_SetCategoryById(nint playerHn, uint id);
public delegate nint criAtomAwb_LoadToc(nint binder, nint path, void* work, int workSize);
```

## Checked on disk

`dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --probes <exe> tools/Metaphor.SignatureCheck/cri-voice-probes.json`:

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   criAtomExCategory_SetVolumeById: 1 match at 0x1275998 (rva 0x1276398)
ok   criAtomExCategory_GetVolumeById: 1 match at 0x1273ddc (rva 0x12747dc)
ok   criAtomExPlayer_SetCategoryById: 1 match at 0x1268f8c (rva 0x126998c)
ok   criAtomExPlayer_SetVolume: 1 match at 0x126aabc (rva 0x126b4bc)
ok   criAtomExAcb_LoadAcbData: 1 match at 0x1264aa8 (rva 0x12654a8)
ok   criAtomAwb_LoadToc: 1 match at 0x1277174 (rva 0x1277b74)
6 of 6 signatures matched exactly once
```

All six are in `.ecode`, unlike `criAtomExPlayer_SetCueId` (`.text1`).

Prologues from `--bytes <exe> <rva> 48`, read by hand, not disassembled:

- **`criAtomExCategory_SetVolumeById`** (`40 53 48 83 EC 30 8B D9 0F 29 74 24 20 33 C9 0F 28 F1 E8 …`): `push rbx`,
  `sub rsp, 0x30`, `mov ebx, ecx` (32-bit id), save `xmm6`, `xor ecx, ecx`, `movaps xmm6, xmm1` (the volume, second
  argument, in `xmm1`). Fits `(uint id, float volume)`.
- **`criAtomExPlayer_SetCategoryById`** (`… 57 48 83 EC 50 48 8B F9 8B F2 33 C9 E8 …`): `mov rdi, rcx` (player),
  `mov esi, edx` (32-bit id). Fits `(player, uint id)`.
- **`criAtomExAcb_LoadAcbData`** (`… 48 83 EC 50 33 F6 4C 63 F2 4D 8B F9 4D 8B E0 48 8B F9 48 85 C9 0F 84 …`):
  `movsxd r14, edx` (32-bit size), `mov r15, r9` (fourth argument, the AWB path), `mov r12, r8` (binder),
  `mov rdi, rcx` (data), then a null check on the data. Fits Ryo's six arguments; the fifth and sixth are on the stack.

## Not yet known

- That these functions are the CRI ones Ryo names in this build, and that the game sets its volume options through
  them rather than another call (AISAC, bus or per-player volume), is `[NEEDS VERIFICATION]` until a run logs them.
- Whether the game loads voice banks through `LoadAcbData`, and whether its AWB path names the voice language, is
  `[NEEDS VERIFICATION]`.

## Used for

The development-only, opt-in listeners in `src/Metaphor.Hooks/CriAudioListeners.cs` (`SetVolumeById`, `SetVolume`,
`SetCategoryById`, `LoadAcbData`), whose patterns are in `src/Metaphor.Core/hook-signatures.json`.
`GetVolumeById` (returns a float in `xmm0`, which no hook shape reads) and `LoadToc` are recorded for later.
