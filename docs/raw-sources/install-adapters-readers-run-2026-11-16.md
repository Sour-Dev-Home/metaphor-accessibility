# The hook layer after #87, #88 and #89: smoke test, signature spike and the opening scene

- **Source:** three runs on the maintainer's machine through Reloaded-II 1.31.0, mod at commit `668f2c6` (branch
  `84-install-and-readers`): the smoke test (Release), `scripts/signature-spike.ps1` (Release), and
  `scripts/deploy-dev.ps1` (Debug) with NVDA running. The transcript and `message-log.txt` hold game text, so they stay
  local; only labels and counts are kept.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `dotnet publish src/Metaphor.Hooks -c Release -o <folder>`, then `pwsh scripts/smoke-test.ps1 -ModFolders <folder>`.
2. The three probe files in `tools/Metaphor.SignatureCheck/` (`mf-toolkit-message-probes.json`,
   `mf-toolkit-squirrel-probes.json`, `msg-request-probes.json`) merged into one file of 15 probes, then
   `pwsh -File scripts/signature-spike.ps1 -Probes <merged file>` (it takes one file).
3. With NVDA on, `pwsh scripts/deploy-dev.ps1`: `hooks 8/8`. The maintainer started a new game and played the opening
   scene to `MES_e00_002_001_004`; the transcript, the message log and Reloaded-II's log were read with the game
   running, then the game was ended.

## Results

```text
metaphor.accessibility: ready on 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504, hooks 2/2
smoke test passed
```

- **Signature spike:** 15 of 15 probes match exactly once on disk. In memory, read 1020 ms after start, "349474816 of
  349474816 bytes read, 0 failed": 13 match once at the disk address. **`CompileMsgFile` and `MsgRequestByName` match
  0 times in memory.** They are the two functions the Release build's hooks patch, and the mod installs its hooks
  before the scan runs (`ModEntryPoint.StartEx`), so the scan sees the hook's jump instead of the original first bytes.
  That a Reloaded hook overwrites the function's first bytes is `[NEEDS VERIFICATION]` against Reloaded.Hooks' source;
  the timing matches: `msg-request-signature-spike-2026-11-16.md` matched `MsgRequestByName` in memory when no Release
  hook sat on it.
- **Dialogue:** each line spoken once, in order (the maintainer, by ear), and the transcript has one `More: <line>`
  per request: eight requests, kind 0, `MES_e00_002_001_000_00` to `_003`, as in
  `hook-services-observers-run-2026-11-16.md`. `_004` was shown and voiced but not spoken, as before (#82).
- **Every adapter shape that fires ran in the game:** the Debug message log has 59 `compile` records (the 5-argument
  shape), 8 `request` records (3 arguments) and 35,662 `register` records from `sq_newclosure` (3 arguments, a function
  that returns nothing). The 4-argument and `double` shapes were installed but not called, as in earlier runs; they
  are covered offline by `shared-delegate-hook-spike-shapes-2026-11-16.md`.
- **Install lines unchanged:** Reloaded-II's log has `hook <name> installed at module offset 0x…` for all eight hooks,
  at the same offsets as the signatures above.
