# Deploy a dev build

To try a change in the game, run `scripts/deploy-dev.ps1`. It builds the mod, puts the build in your Reloaded-II mods
folder, enables it for the game, launches the game and waits until the mod reports it loaded. The game stays running.

You need the game registered as an application in Reloaded-II, and the game build in the
[known-builds list](add-a-game-build.md). The script refuses to run on any other build.

1. Close the game and Reloaded-II. The script won't start while either is running: the game holds the old build's
   files, and a running launcher can save over the enabled mods.
2. From the repository root, in PowerShell 7:

   ```powershell
   pwsh scripts/deploy-dev.ps1
   ```

   It finds Reloaded-II's launcher and the game from Reloaded-II's own config: `LauncherPath` in `ReloadedII.json`,
   and the application registered as `metaphor.exe`. To use others, set `-LauncherExe` and `-GameExe`, or the
   `METAPHOR_RELOADED_EXE` and `METAPHOR_GAME_EXE` environment variables.

It ends with `deployed and running` (exit code 0) when the mod is ready with all its hooks, and `deploy FAILED`
(exit code 1) when it refused, timed out or the game exited. Exit code 2 means nothing was launched, and says why.

What it changes in your Reloaded-II setup, and nothing else:

- the mod's folder in the mods folder named in `ReloadedII.json`, named by its mod ID. A previous deploy there is
  replaced. A folder of that name holding a different mod is left alone, and the script stops.
- the game's `AppConfig.json`, where the mod is added to `EnabledMods`. Mods you already enabled stay enabled. The
  load order (`SortedMods`) isn't touched, and the mod loads without it
  ([first run](../../raw-sources/mod-entry-point-first-run-2026-11-15.md)). The previous file is kept beside it as
  `AppConfig.json.deploy-backup`.

By default it builds the mod (`src/Metaphor.Hooks`, mod ID `metaphor.accessibility`). `-Project` builds another mod
project, whose build output must hold a `ModConfig.json`, and `-Configuration Release` a Release build.

The mod has no hooks yet, so a working deploy prints `ready on <game build>, hooks 0/0`. The mod also writes a line
naming its commit and the game build to Reloaded-II's log for that run, the newest file in
`%APPDATA%\Reloaded-Mod-Loader-II\Logs\`.

## Listening run

`scripts/listen.ps1` deploys the Debug build the same way, lets the game run, and reports what the mod's development
log saw, without printing any game text:

```powershell
pwsh scripts/listen.ps1 -Seconds 300 -Close
```

After the deploy it watches the game for `-Seconds` (default 120) and prints `the game is still running after <n> s`
or `the game exited after <n> s`. `-Close` then kills the game, as the smoke test does, so a save in progress is lost: use it on a test save. Without
it, the game is left running. Last, it counts
the records in the mod's `message-log.txt` by type, and the requests by the function they came through:

```text
message log: 12 compile (840 source lines), 310 lookup, 5 register, 44 request
requests by function: MsgRequestById 4, MsgRequestByName 40
```

The log itself holds game text, so it stays in the mod's folder and is never printed or committed. Local paths in the
output are shortened (`<mods>`, `<game>`, `~` and so on), so it can go into an issue or a raw source as it is. It takes
the deploy's parameters except `-Configuration`, and exits 0 when the game survived and the mod wrote its log, 1 when
the game exited, the deploy failed or there's no log, and 2 when the deploy couldn't start.

## The scripts' own tests

The scripts' own tests run against a stand-in launcher and game, with no game needed:

```powershell
Invoke-Pester tests/smoke
```
