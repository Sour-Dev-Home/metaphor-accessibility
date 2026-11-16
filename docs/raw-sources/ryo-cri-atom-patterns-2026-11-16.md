# Ryo Framework's CRI ADX2 patterns for Metaphor: ReFantazio, checked on disk

- **Source:** Ryo Framework (Reloaded-II audio and movie framework), <https://github.com/RyoTune/Ryo>, commit
  `1d661e153ad46efa0707379bd9fd61255f357a2c` (2026-11-04), fetched with `gh api`. Files:
  `Ryo.Reloaded/CRI/CriAtomEx/CriAtomExPatterns.cs` (last changed in `116995ad`), `Ryo.Interfaces/Functions/CriAtomExFunctions.cs`,
  `Ryo.Reloaded/Audio/AudioService.cs`. Licence: Ryo's own "General Mod License" (`LICENSE.txt`); only names and
  byte patterns are quoted here, no code is reused.
- **Saved:** 2026-11-16, for issues #93 (lead 1) and #103.
- **Game build:** Ryo names none (its pattern sets are keyed by exe name, `"metaphor"`). Checked below on
  `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## What else was looked for

A web search and `gh search code criAtomExPlayer_Start` found Ryo (with per-game sets for P3R, P5R, P4G, P3P,
SMT V, Metaphor and others) and two Ryo game add-ons; no other mod with Metaphor ADX2 patterns. CriFs V2 Hook covers
the file system, not playback (`crifs-v2-hook-metaphor-patterns-2026-11-15.md`).

## Quoted from Ryo

`CriAtomExPatterns.cs`, the `"metaphor"` entry (lines 76–99), the patterns used here:

```text
criAtomExPlayer_Start = "48 89 5C 24 ?? 57 48 83 EC 20 48 89 CF 48 85 C9 75 ?? 44 8D 41"
criAtomExPlayer_SetCueId = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 49 63 F8 48 89 D6"
criAtomExAcb_GetCueInfoById = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 4C 89 C7 89 D6"
```

The entry also has `criAtomExPlayer_Create`, `SetFormat`, `SetNumChannels`, `SetSamplingRate`, `SetCategoryById`,
`SetVolume`, `ResetParameters`, `SetFile` (commented `// ???`), `SetWaveId`, `criAtomExAcb_LoadAcbData`,
`criAtomAwb_LoadToc`, `criAtomExCategory_GetVolumeById`, `SetVolumeById`. It has no `criAtomExPlayer_SetCueName`.

`CriAtomExFunctions.cs`, the delegates:

```text
public delegate void criAtomExPlayer_SetCueId(nint playerHn, nint acbHn, int cueId);
public delegate uint criAtomExPlayer_Start(nint playerHn);
public delegate void criAtomExPlayer_SetCueName(nint playerHn, nint acbHn, byte* cueName);
```

`AudioService.cs` hooks `SetCueId`, `SetCueName`, `SetFile`, `SetWaveId` and `SetData` (lines 45–52) and, inside the
`SetCueId` hook, calls `criAtomExAcb_GetCueInfoById(acbHn, cueId, cueInfo)` to get the cue's name (lines 116–125). It
only wraps `Start`, never hooks it.

## Checked on disk

Tool: `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --probes <exe> tools/Metaphor.SignatureCheck/cri-atom-probes.json`,
then `--bytes <exe> <rva> <len>`, on 2026-11-16. Output:

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
FAIL criAtomExPlayer_Start: 3 matches, at 0x11cbf594, 0x11d0b860, 0x11e78ae6
ok   criAtomExPlayer_SetCueId: 1 match at 0x11cf14d0 (rva 0x126438d0)
ok   criAtomExAcb_GetCueInfoById: 1 match at 0x11c41cb8 (rva 0x125940b8)
```

**`criAtomExPlayer_SetCueId`, rva `0x126438d0` (`.text1`):**

```text
0x126438d0  48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57
0x126438e0  48 83 EC 20 49 63 F8 48 89 D6 48 89 CB 48 85 C9
0x126438f0  74 7B 45 85 C0 78 76 E8 60 A4 C2 EE 89 C5 48 85
```

Read by hand, not disassembled: `movsxd rdi, r8d` (third argument, sign-extended from 32 bits), `mov rsi, rdx`,
`mov rbx, rcx`, `test rcx, rcx` / `je`, `test r8d, r8d` / `js`. That fits Ryo's `(player, acb, int cueId)`: a null
player or a negative cue id returns early.

**`criAtomExPlayer_Start`: not resolved.** Ryo's pattern matches three functions (rvas `0x12611994`, `0x1265dc60`,
`0x127caee6`, mapped with the same `.text1` file-to-rva difference). All three open with the same null-player check and
pass error code -2 and an error string to the same function (`.ecode` `0x1317104`); the strings are CRI error ids
`E2010021545`, `E2010022750` and `E2009102004`, which no public source maps to function names. None returns
`0xFFFFFFFF` after the error (`83 C8 FF`), which the other games' `Start` pattern in Ryo expects; the second and
third return nothing there. Which one, if any, is `Start` is `[NEEDS VERIFICATION]`. Ryo never calls its `Start`
wrapper for Metaphor, so its pattern there is untested by Ryo too.

## Used for

`criAtomExPlayer_SetCueId` is in `src/Metaphor.Core/hook-signatures.json` with the development-only
`CriCueSetListener` (`src/Metaphor.Hooks/CriAudioListeners.cs`). It is not yet checked in memory
(`scripts/signature-spike.ps1 -Probes tools/Metaphor.SignatureCheck/cri-atom-probes.json`).
