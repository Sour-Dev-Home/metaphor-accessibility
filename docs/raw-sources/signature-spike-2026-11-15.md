# Signature scans of the game on disk and in memory (issue #21)

- **Source:** a capture of one run of `scripts/signature-spike.ps1` at commit `87431bd`, plus the smoke-test fix
  committed with this file (the throwaway profile holds a copy of `reloaded.sharedlib.hooks`), on the maintainer's
  machine, through Reloaded-II 1.31.0. Probes: `tools/Metaphor.SignatureCheck/signature-probes.json`
  (`docs/raw-sources/crifs-v2-hook-metaphor-patterns-2026-11-15.md`).
- **Saved:** 2026-11-15.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504`.

## Steps

1. `pwsh scripts/signature-spike.ps1`, with the game and Reloaded-II closed. It ran the disk check
   (`tools/Metaphor.SignatureCheck --probes`), then the smoke test with only our mod enabled, whose mod copied the
   game's main module through `ReadProcessMemory` when Reloaded-II started it and scanned it (ADR 0002: read only).

An earlier run the same evening failed before any mod loaded. Reloaded-II's log for it said "Failed to Load
Reloaded Hooks Shared Lib Mod" and "Mod with ID (reloaded.sharedlib.hooks) not found.", because the smoke test's
throwaway profile held only the test mods. Its disk check output was identical to the one below.

## What this settles

- On this build, all 15 of CriFs V2 Hook's release patterns match exactly once in the exe on disk and exactly once in
  the running game's main module, at the same address relative to the module.
- At the time our mod starts (about 1.8 s after the game process), the code those patterns cover is the same in memory
  as on disk, and every committed page of the module could be read.
- This doesn't show whether the code changes later in the run; the scan ran once.

## Result

The memory copy was made 1831 ms after the game started: 349474816 of
349474816 bytes read, 0 failed. Disk addresses are converted from file offsets
through the exe's section table.

| Probe | Disk matches | Disk RVA | Memory matches | Memory RVAs | Verdict |
|---|---|---|---|---|---|
| `CriFs_CalculateWorkSizeForLibrary` | 1 | 0x12fefcc | 1 | 0x12fefcc | same place |
| `CriFs_InitializeLibrary` | 1 | 0x134905c0 | 1 | 0x134905c0 | same place |
| `CriFs_FinalizeLibrary` | 1 | 0x12ff384 | 1 | 0x12ff384 | same place |
| `CriFsBinder_BindCpk` | 1 | 0x134a05c0 | 1 | 0x134a05c0 | same place |
| `CriFsBinder_BindFile` | 1 | 0x1300d6c | 1 | 0x1300d6c | same place |
| `CriFsBinder_BindFiles` | 1 | 0x1300d98 | 1 | 0x1300d98 | same place |
| `CriFsBinder_Find` | 1 | 0x1301468 | 1 | 0x1301468 | same place |
| `CriFsBinder_GetSizeForBindFiles` | 1 | 0x134c5ff1 | 1 | 0x134c5ff1 | same place |
| `CriFsBinder_GetStatus` | 1 | 0x134c1087 | 1 | 0x134c1087 | same place |
| `CriFsBinder_SetPriority` | 1 | 0x1303170 | 1 | 0x1303170 | same place |
| `CriFsBinder_Unbind` | 1 | 0x1303368 | 1 | 0x1303368 | same place |
| `CriFsLoader_RegisterFile` | 1 | 0x1307788 | 1 | 0x1307788 | same place |
| `CriFsIo_Exists` | 1 | 0x130eff0 | 1 | 0x130eff0 | same place |
| `CriFsIo_Open` | 1 | 0x130f1e8 | 1 | 0x130f1e8 | same place |
| `CriFsIo_IsUtf8` | 1 | 0x130f01f | 1 | 0x130f01f | same place |

## Disk check output

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   CriFs_CalculateWorkSizeForLibrary: 1 match at 0x12fe5cc (rva 0x12fefcc)
ok   CriFs_InitializeLibrary: 1 match at 0x12b3e1c0 (rva 0x134905c0)
ok   CriFs_FinalizeLibrary: 1 match at 0x12fe984 (rva 0x12ff384)
ok   CriFsBinder_BindCpk: 1 match at 0x12b4e1c0 (rva 0x134a05c0)
ok   CriFsBinder_BindFile: 1 match at 0x130036c (rva 0x1300d6c)
ok   CriFsBinder_BindFiles: 1 match at 0x1300398 (rva 0x1300d98)
ok   CriFsBinder_Find: 1 match at 0x1300a68 (rva 0x1301468)
ok   CriFsBinder_GetSizeForBindFiles: 1 match at 0x12b73bf1 (rva 0x134c5ff1)
ok   CriFsBinder_GetStatus: 1 match at 0x12b6ec87 (rva 0x134c1087)
ok   CriFsBinder_SetPriority: 1 match at 0x1302770 (rva 0x1303170)
ok   CriFsBinder_Unbind: 1 match at 0x1302968 (rva 0x1303368)
ok   CriFsLoader_RegisterFile: 1 match at 0x1306d88 (rva 0x1307788)
ok   CriFsIo_Exists: 1 match at 0x130e5f0 (rva 0x130eff0)
ok   CriFsIo_Open: 1 match at 0x130e7e8 (rva 0x130f1e8)
ok   CriFsIo_IsUtf8: 1 match at 0x130e61f (rva 0x130f01f)
15 of 15 signatures matched exactly once
```

## Memory scan report

```json
{
  "gameBuildSha256": "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504",
  "millisecondsSinceGameStart": 1831,
  "moduleBytes": 349474816,
  "readableBytes": 349474816,
  "unreadableBytes": 0,
  "scanMilliseconds": 108,
  "probes": [
    {
      "name": "CriFs_CalculateWorkSizeForLibrary",
      "matches": 1,
      "firstRvas": [
        19918796
      ]
    },
    {
      "name": "CriFs_InitializeLibrary",
      "matches": 1,
      "firstRvas": [
        323552704
      ]
    },
    {
      "name": "CriFs_FinalizeLibrary",
      "matches": 1,
      "firstRvas": [
        19919748
      ]
    },
    {
      "name": "CriFsBinder_BindCpk",
      "matches": 1,
      "firstRvas": [
        323618240
      ]
    },
    {
      "name": "CriFsBinder_BindFile",
      "matches": 1,
      "firstRvas": [
        19926380
      ]
    },
    {
      "name": "CriFsBinder_BindFiles",
      "matches": 1,
      "firstRvas": [
        19926424
      ]
    },
    {
      "name": "CriFsBinder_Find",
      "matches": 1,
      "firstRvas": [
        19928168
      ]
    },
    {
      "name": "CriFsBinder_GetSizeForBindFiles",
      "matches": 1,
      "firstRvas": [
        323772401
      ]
    },
    {
      "name": "CriFsBinder_GetStatus",
      "matches": 1,
      "firstRvas": [
        323752071
      ]
    },
    {
      "name": "CriFsBinder_SetPriority",
      "matches": 1,
      "firstRvas": [
        19935600
      ]
    },
    {
      "name": "CriFsBinder_Unbind",
      "matches": 1,
      "firstRvas": [
        19936104
      ]
    },
    {
      "name": "CriFsLoader_RegisterFile",
      "matches": 1,
      "firstRvas": [
        19953544
      ]
    },
    {
      "name": "CriFsIo_Exists",
      "matches": 1,
      "firstRvas": [
        19984368
      ]
    },
    {
      "name": "CriFsIo_Open",
      "matches": 1,
      "firstRvas": [
        19984872
      ]
    },
    {
      "name": "CriFsIo_IsUtf8",
      "matches": 1,
      "firstRvas": [
        19984415
      ]
    }
  ],
  "error": null
}
```
