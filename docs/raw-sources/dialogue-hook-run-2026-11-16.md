# The dialogue hooks in the game: smoke test and the opening scene (issue #56)

- **Source:** two runs on the maintainer's machine through Reloaded-II 1.31.0, mod at commit `0d678d3`: the smoke test
  (Release) and `scripts/deploy-dev.ps1` (Debug), with NVDA running. The transcript and `message-log.txt` hold game
  text, so they stay local; only labels and a short quote are kept.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `dotnet publish src/Metaphor.Hooks -c Release -o <folder>`, then
   `pwsh scripts/smoke-test.ps1 -ModFolders <folder>`.
2. With NVDA on, `pwsh scripts/deploy-dev.ps1`: `hooks 10/10` (the two dialogue hooks and eight development-only ones).
3. The maintainer started a new game and played the opening scene, advancing each line by hand, some quickly. NVDA may
   have been turned off partway.
4. The transcript and the `request` records of the message log were read; the game was then ended.

## Results

```text
metaphor.accessibility: ready on 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504, hooks 2/2
smoke test passed
```

- **Each line was spoken once, in order** (the maintainer, by ear, over the voice acting), and the transcript has one
  line per request, in request order, as `More: <line>`: nine requests, kind 0, `MES_e00_002_001_000_00` to `_000_03`,
  then `_000`, `_001`, `_002`, `_003` and `_006`.
- **The label without a `_NN` suffix is an ordinary line.** `@MES_e00_002_001_000` in `EN/message/event/e00_002_001.msg`
  holds the line after `_000_03` (speaker `<NS>More<NE>`), which closes the question left open in
  `msg-request-run-2026-11-16.md`.
- **Two messages between `_003` and `_006` didn't go through `MsgRequestByName`:** `_004`, whose body is only a voice
  tag and `<WAIT>`, and `_005`, declared `@MES_e00_002_001_005 SEL 0` (a choice, by its form). How the game shows them
  is `[NEEDS VERIFICATION]`; `MsgRequestSelect` (`msg-request-functions-2026-11-16.md`) is the likely route for the
  choice. A choice isn't spoken yet.
- **The spoken name is the `<NS>` text, "More"; the screen shows "Scheming Man"** (`message-listening-run-2026-11-16.md`).
- A read of the transcript while the game was still running, about a minute after `_001` to `_003` were requested,
  didn't yet show them; a later read did, in order. Why they were late is `[NEEDS VERIFICATION]`.
- No crash: nothing in the Reloaded-II log after loading, and no application error event.
