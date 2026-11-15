# First run of the mod entry point in the game

- **Source:** a capture of one run on the maintainer's machine. Reloaded-II 1.31.0 (`Reloaded-II.exe` product version
  `1.31.0+458987f26bc8d78fdc0f86b73fc0d6fd4b673d01`, the commit the Reloaded-II raw sources cite). The mod was built from
  commit `1b38808` (branch `feat/43-mod-entry-point`, issue #43).
- **Saved:** 2026-11-15.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. With the game and Reloaded-II closed, set `METAPHOR_GAME_EXE` to the game exe and `METAPHOR_RELOADED_EXE` to the
   launcher named by `LauncherPath` in `ReloadedII.json`. That launcher existed, and the game was registered in
   Reloaded-II with `AppId` `metaphor.exe`.
2. From the repository root: `pwsh scripts/deploy-dev.ps1`.
3. Read `mod-status.json` in the deployed mod folder, and the newest file in
   `<Roaming AppData>\Reloaded-Mod-Loader-II\Logs\`.

## What happened

- The game started, with the deployed mod loaded. `mod-status.json` was written about 2 seconds after the game process
  started:

  ```json
  {"modId":"metaphor.accessibility","state":"ready","gameBuildSha256":"548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504","hooksLoaded":0,"hooksExpected":0,"reason":null}
  ```

- Reloaded-II's per-run log for the game (`<date> ~ METAPHOR.txt` in the folder above) listed the mod and then the line
  the entry point writes through the loader's logger:

  ```text
  [11:35:06] [Reloaded] - AppId   : metaphor.accessibility
  [11:35:06] [metaphor.accessibility] build 1b388082f3d9b4b2683d2a75fc30acb372c7d3ee: Ready on game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504
  ```

## What this settles

- A mod compiled against `Reloaded.Mod.Interfaces` 2.4.0 (compile-only) loads and starts in Reloaded-II 1.31.0.
- The deploy added the mod to the game's `EnabledMods` only, not `SortedMods`, and it still loaded.
- Inside the game, `Environment.ProcessPath` is the game exe, and its SHA-256 matches the known-builds entry, which was
  hashed from the file on disk.
- The loader's `ILogger.WriteLine` output lands in the per-run log file. Whether it also shows in a console window wasn't
  checked.
