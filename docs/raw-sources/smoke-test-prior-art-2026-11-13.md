# Prior art for a game smoke test with setup and cleanup

- **Source:** the pages below, read with a web fetch that summarises each page, so quotes are short and taken from those
  summaries. Nothing was downloaded or run.
  - Valheim.Testing.NativeSmoke 0.4.0: <https://www.nuget.org/packages/Valheim.Testing.NativeSmoke>, source
    <https://github.com/tvongaza/ValheimTesting>.
  - ExecutionSteps 0.0.2: <https://www.powershellgallery.com/packages/ExecutionSteps/0.0.2/Content/ExecutionSteps.psm1>.
  - Pester `AfterAll`: <https://pester.dev/docs/v6/commands/AfterAll>.
  - BugNet, game smoke tests: <https://bugnet.io/blog/how-to-automate-smoke-tests-for-your-game>.
- **Saved:** 2026-11-13.
- **Game build:** none; this is about test tooling, not the game.

Checked for issue #16 (smoke test), per ground rule 3.

## Valheim.Testing.NativeSmoke (closest match)

A .NET tool that smoke-tests a mod in the real game (Valheim, BepInEx).
- Launches the owned game, waits for world entry under deadlines (`--join-seconds`, default 180), scans logs, writes
  `result.json` with `PASS` or `FAIL`.
- States its limit: "A pass proves the plugins loaded and the game entered a world, not the mod's gameplay."
- Avoids restoring user config: "Source configs and the source install are never edited." Each run stages a disposable
  loader profile.
- Survives interrupted runs: a journal of everything the run created, used by `env recover` and `env teardown`.
- One in-place restore: "The client's recent-servers list is moved aside before launch and put back after."
- License not stated in the fetched text.

## ExecutionSteps (PowerShell)

Each step has `Run`, `Cleanup` and `Final` script blocks. On failure, `Cleanup` runs for the failing step and every
earlier one in reverse (`for ($j = $i; $j -ge 0; --$j)`); `Cleanup` doesn't run on success. `Final` runs in reverse
either way. A cleanup that throws stops the loop (`break`), so later cleanups are skipped.

## Pester

`AfterAll` and `AfterEach` are guaranteed to run, so tests of the smoke test can rely on them for their own cleanup. No
`DeferCleanup` command was found.

## BugNet

Generic advice: launch after the build with a timeout, fail on error-level log lines, keep logs and crash dumps as
artifacts.

## Takeaways for #16

- Prefer an isolated loader profile over editing and restoring the user's config.
- Where a restore is unavoidable: each undo in its own `try/catch` so one failure doesn't skip the rest, plus an on-disk
  journal so a killed run can be recovered.
- Whether Reloaded-II can run from a separate config location: [NEEDS VERIFICATION]. Known so far: no launch argument
  selects mods; the set is the application's saved `EnabledMods` (`reloaded-ii-launch-and-logs-2026-11-13.md`).
