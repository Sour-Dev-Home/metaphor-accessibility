# MF.Toolkit's message signatures on disk and in memory (issue #55, step 1)

- **Source:** a capture of one run of `scripts/signature-spike.ps1 -Probes
  tools/Metaphor.SignatureCheck/mf-toolkit-message-probes.json` at commit `d5cb025`, plus that probes file and the
  spike's probes-header change, committed with this file; on the maintainer's machine, through Reloaded-II 1.31.0.
  Probes: `mf-toolkit-message-probes.json` (`docs/raw-sources/game-text-hooks-community-2026-11-15.md`; patterns
  checked identical to `MF.Toolkit.Reloaded/Messages/MessageService.cs` at MF.Toolkit commit `b1f876da`).
- **Saved:** 2026-11-15.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504`.

## Steps

1. `pwsh scripts/signature-spike.ps1`, with the game and Reloaded-II closed. It ran the disk check
   (`tools/Metaphor.SignatureCheck --probes`), then the smoke test with only our mod enabled, whose mod copied the
   game's main module through `ReadProcessMemory` when Reloaded-II started it and scanned it (ADR 0002: read only).

## What this settles

- On this build, all six of MF.Toolkit's message signatures match exactly once on disk and in the running game's main
  module, at the same address. MF.Toolkit's sources name no game build; this pins them to `548ddc95…`.
- `GetItemNameMsg_Caller` and `GetItemEffectMsg_Caller` locate a caller, from which MF.Toolkit follows a `call` at
  `+0x2d` and `+0x8e` to the function it hooks. These scans confirm the callers, not where those calls lead.
- None of these is known to be the function that *displays* a dialogue line; that is #55's step 2.

## Result

The memory copy was made 1848 ms after the game started: 349474816 of
349474816 bytes read, 0 failed. Disk addresses are converted from file offsets
through the exe's section table.

| Probe | Disk matches | Disk RVA | Memory matches | Memory RVAs | Verdict |
|---|---|---|---|---|---|
| `CreateMsgFromString` | 1 | 0x13c9f6d0 | 1 | 0x13c9f6d0 | same place |
| `GetItemNameMsg_Caller` | 1 | 0xa69650 | 1 | 0xa69650 | same place |
| `GetItemEffectMsg_Caller` | 1 | 0xb51f40 | 1 | 0xb51f40 | same place |
| `GetItemDescriptionMsg` | 1 | 0xf21efa0 | 1 | 0xf21efa0 | same place |
| `CompileMsgFile` | 1 | 0x9eaa70 | 1 | 0x9eaa70 | same place |
| `GetMessageByLabelIndex` | 1 | 0x9e3b70 | 1 | 0x9e3b70 | same place |

## Disk check output

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   CreateMsgFromString: 1 match at 0x1334d2d0 (rva 0x13c9f6d0)
ok   GetItemNameMsg_Caller: 1 match at 0xa68c50 (rva 0xa69650)
ok   GetItemEffectMsg_Caller: 1 match at 0xb51540 (rva 0xb51f40)
ok   GetItemDescriptionMsg: 1 match at 0xe8ccba0 (rva 0xf21efa0)
ok   CompileMsgFile: 1 match at 0x9ea070 (rva 0x9eaa70)
ok   GetMessageByLabelIndex: 1 match at 0x9e3170 (rva 0x9e3b70)
6 of 6 signatures matched exactly once
```

## Memory scan report

```json
{
  "gameBuildSha256": "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504",
  "millisecondsSinceGameStart": 1848,
  "moduleBytes": 349474816,
  "readableBytes": 349474816,
  "unreadableBytes": 0,
  "scanMilliseconds": 196,
  "probes": [
    {
      "name": "CreateMsgFromString",
      "matches": 1,
      "firstRvas": [
        332003024
      ]
    },
    {
      "name": "GetItemNameMsg_Caller",
      "matches": 1,
      "firstRvas": [
        10917456
      ]
    },
    {
      "name": "GetItemEffectMsg_Caller",
      "matches": 1,
      "firstRvas": [
        11870016
      ]
    },
    {
      "name": "GetItemDescriptionMsg",
      "matches": 1,
      "firstRvas": [
        253882272
      ]
    },
    {
      "name": "CompileMsgFile",
      "matches": 1,
      "firstRvas": [
        10398320
      ]
    },
    {
      "name": "GetMessageByLabelIndex",
      "matches": 1,
      "firstRvas": [
        10369904
      ]
    }
  ],
  "error": null
}
```
