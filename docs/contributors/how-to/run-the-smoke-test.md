# Run the smoke test

Before a PR that touches hooking, loading or injection, run the in-game smoke test and paste its output into the PR's
Evidence section. It launches the game with only the test mods, checks each one reports ready on the running
[game build](../../../GLOSSARY.md), then closes the game and puts your Reloaded-II setup back.

You need the game registered as an application in Reloaded-II, and the game build in the
[known-builds list](add-a-game-build.md).

1. Set borderless windowed mode in the game: in fullscreen it can freeze after loading until clicked
   (`docs/raw-sources/reloaded-ii-clean-exit-2026-11-14.md`).
2. Close the game and Reloaded-II. The test won't start while either is running, because it ends the game by killing
   it and a running launcher can save over the config.
3. Build the test mods. Pass any library mods they depend on in `-ModFolders` too, since only the mods you list are
   enabled. Whether Reloaded-II offers to download a missing dependency instead is [NEEDS VERIFICATION].
4. From the repository root, in PowerShell 7:

   ```powershell
   pwsh scripts/dev/session.ps1 smoke -ModFolders <test mod folder>, <another>
   ```

   It finds the launcher and the game as [deploying a dev build](deploy-a-dev-build.md) does, and takes the same
   overrides.

It ends with an `ok: ...` line (exit code 0) when every test mod is ready with all its hooks, and a `failed: ...` line
(exit code 1) otherwise. Exit code 2 means it stopped before launching, and says why. The game is closed by ending its
process, because closing its window only asks "Exit game?".

While it runs, your `ReloadedII.json` points at a throwaway profile, and the original waits next to it as
`ReloadedII.json.session-backup`. The session puts it back when it ends, also on Ctrl+C; if the run is killed outright,
the next session restores the backup before it starts. To restore by hand, move the backup back over
`ReloadedII.json`.

The script's own tests run against the [stand-in game](../../../GLOSSARY.md#working-and-testing), with no game needed:

```powershell
Invoke-Pester tests/pester
```
