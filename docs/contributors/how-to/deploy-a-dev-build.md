# Deploy a dev build

To try a change in the game, run `scripts/deploy-dev.ps1`. It builds the mod, puts the build in your Reloaded-II mods
folder, enables it for the game, launches the game and waits until the mod reports it loaded. The game stays running.

You need the game registered as an application in Reloaded-II, and the game build in the
[known-builds list](add-a-game-build.md). The script refuses to run on any other build.

1. Close the game and Reloaded-II. The script won't start while either is running: the game holds the old build's
   files, and a running launcher can save over the enabled mods.
2. From the repository root, in PowerShell 7:

   ```powershell
   $env:METAPHOR_GAME_EXE = '<game folder>\METAPHOR.exe'
   $env:METAPHOR_RELOADED_EXE = '<Reloaded-II folder>\Reloaded-II.exe'
   pwsh scripts/deploy-dev.ps1
   ```

It ends with `deployed and running` (exit code 0) when the mod is ready with all its hooks, and `deploy FAILED`
(exit code 1) when it refused, timed out or the game exited. Exit code 2 means nothing was launched, and says why.

What it changes in your Reloaded-II setup, and nothing else:

- the mod's folder in the mods folder named in `ReloadedII.json`, named by its mod ID. A previous deploy there is
  replaced. A folder of that name holding a different mod is left alone, and the script stops.
- the game's `AppConfig.json`, where the mod is added to `EnabledMods`. Mods you already enabled stay enabled. The
  load order (`SortedMods`) isn't touched; whether the loader needs the mod listed there too is
  [NEEDS VERIFICATION]. The previous file is kept beside it as `AppConfig.json.deploy-backup`.

By default it builds the mod (`src/Metaphor.Hooks`, mod ID `metaphor.accessibility`). `-Project` builds another mod
project, whose build output must hold a `ModConfig.json`, and `-Configuration Release` a Release build.

The mod has no hooks yet, so a working deploy prints `ready on <game build>, hooks 0/0`. The mod also writes a line
naming its commit and the game build to Reloaded-II's logger. Where that line shows up is [NEEDS VERIFICATION].

The script's own tests run against a stand-in launcher and game, with no game needed:

```powershell
Invoke-Pester tests/smoke
```
