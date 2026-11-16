# The dialogue hooks after #85 and #86: smoke test and the opening scene

- **Source:** two runs on the maintainer's machine through Reloaded-II 1.31.0, mod at commit `9647b16` (branch
  `84-hook-layer-deepening`): the smoke test (Release) and `scripts/deploy-dev.ps1` (Debug), with NVDA running. The
  transcript and `message-log.txt` hold game text, so they stay local; only labels and short quotes are kept.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `dotnet publish src/Metaphor.Hooks -c Release -o <folder>`, then `pwsh scripts/smoke-test.ps1 -ModFolders <folder>`.
2. With NVDA on, `pwsh scripts/deploy-dev.ps1`: `hooks 8/8` (the two dialogue hooks and six development-only ones).
3. The maintainer started a new game and played the opening scene past `MES_e00_002_001_003`, then the transcript and
   the message log were read with the game still running, and the game was ended.

## Results

```text
metaphor.accessibility: ready on 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504, hooks 2/2
smoke test passed
```

- **The same lines as before #85 and #86, each spoken once, in order** (the maintainer, by ear), and the transcript
  has one `More: <line>` per request: eight requests, kind 0, `MES_e00_002_001_000_00` to `_000_03`, then `_000`,
  `_001`, `_002` and `_003`, matching `dialogue-hook-run-2026-11-16.md`.
- **The Debug message log still has its records**, now written by the log observer: one `compile` record for
  `EN/message/event/e00_002_001.msg` and one `request` record per line, as `request via=MsgRequestByName kind=0
  text="<label>"`, without the options bytes.
- **Correction to `dialogue-hook-run-2026-11-16.md`:** `@MES_e00_002_001_004` is a spoken line, not only a voice tag
  and `<WAIT>`: `<NS>More<NE>`, `<VOICE "v_e00_002_001_004_c051">`, then "Is fantasy limited to the confines of
  imagination? …". The maintainer saw it on screen and it wasn't spoken, as it doesn't go through `MsgRequestByName`.
  The choice after it, `@MES_e00_002_001_005 SEL 0`, has two options, one per line ("That's right." and "Not
  always."). How the game shows both is `[NEEDS VERIFICATION]` (#82).
