# Signature scans of the game on disk and in memory

- **Source:** a capture of one run of `scripts/signature-spike.ps1` at commit `162ecd6`, on the maintainer's
  machine. Probes: `msg-request-probes.json` (`docs/raw-sources/msg-request-functions-2026-11-16.md`).
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504`.

## Steps

1. `pwsh scripts/signature-spike.ps1`, with the game and Reloaded-II closed. It ran the disk check
   (`tools/Metaphor.SignatureCheck --probes`), then the smoke test with only our mod enabled, whose mod copied the
   game's main module through `ReadProcessMemory` when Reloaded-II started it and scanned it (ADR 0002: read only).

## Result

The memory copy was made 1995 ms after the game started: 349474816 of
349474816 bytes read, 0 failed. Disk addresses are converted from file offsets
through the exe's section table.

| Probe | Disk matches | Disk RVA | Memory matches | Memory RVAs | Verdict |
|---|---|---|---|---|---|
| `MsgRequestByName` | 1 | 0x9f8240 | 1 | 0x9f8240 | same place |
| `MsgRequestByNameTwin` | 1 | 0x9f8470 | 1 | 0x9f8470 | same place |
| `MsgRequestById` | 1 | 0x9f7fd0 | 1 | 0x9f7fd0 | same place |
| `MsgRequestByNameAuto` | 1 | 0x9f8910 | 1 | 0x9f8910 | same place |
| `MsgRequestByIdAuto` | 1 | 0x9f86a0 | 1 | 0x9f86a0 | same place |

## Disk check output

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   MsgRequestByName: 1 match at 0x9f7840 (rva 0x9f8240)
ok   MsgRequestByNameTwin: 1 match at 0x9f7a70 (rva 0x9f8470)
ok   MsgRequestById: 1 match at 0x9f75d0 (rva 0x9f7fd0)
ok   MsgRequestByNameAuto: 1 match at 0x9f7f10 (rva 0x9f8910)
ok   MsgRequestByIdAuto: 1 match at 0x9f7ca0 (rva 0x9f86a0)
5 of 5 signatures matched exactly once
```

## Memory scan report

```json
{
  "gameBuildSha256": "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504",
  "millisecondsSinceGameStart": 1995,
  "moduleBytes": 349474816,
  "readableBytes": 349474816,
  "unreadableBytes": 0,
  "scanMilliseconds": 109,
  "probes": [
    {
      "name": "MsgRequestByName",
      "matches": 1,
      "firstRvas": [
        10453568
      ]
    },
    {
      "name": "MsgRequestByNameTwin",
      "matches": 1,
      "firstRvas": [
        10454128
      ]
    },
    {
      "name": "MsgRequestById",
      "matches": 1,
      "firstRvas": [
        10452944
      ]
    },
    {
      "name": "MsgRequestByNameAuto",
      "matches": 1,
      "firstRvas": [
        10455312
      ]
    },
    {
      "name": "MsgRequestByIdAuto",
      "matches": 1,
      "firstRvas": [
        10454688
      ]
    }
  ],
  "error": null
}
```
