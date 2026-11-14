# Reloaded-II command-line launch, logs and ASI loading

- **Source:** Reloaded-II source on GitHub, `master` branch, read via the GitHub REST API (`gh api`), and its docs at
  <https://reloaded-project.github.io/Reloaded-II/InjectionMethods/>. Line numbers are as fetched on the date below.
  Nothing was downloaded or run.
- **Saved:** 2026-11-13.
- **Game build:** none; this is about the loader, not the game.

`B` below means <https://github.com/Reloaded-Project/Reloaded-II/blob/master>.

## Command-line launch

- The launcher's arguments are exactly `--kill`, `--launch`, `--arguments`, `--working-directory`, `--download`,
  `--r2pack` and `--r2packdl` (`B/source/Reloaded.Mod.Launcher.Lib/Misc/Constants.cs#L9-L15`).
- `Startup.cs` handles them (`B/source/Reloaded.Mod.Launcher.Lib/Startup.cs#L26-L60`). `--launch "<path\Game.exe>"` calls
  `LaunchApplicationAndExit` (`#L78-L108`), which matches the exe to a configured application, appends that application's
  saved `AppArguments`, and starts the game with injection (`#L182-L187`).
- No argument selects mods. The mod set is the application's saved `EnabledMods`
  (`B/source/Reloaded.Mod.Loader.IO/Config/ApplicationConfig.cs#L22`), so launching a chosen set means editing that config
  first.
- `Startup.cs#L94` dereferences `application!`, so launching an exe that isn't registered as an application probably
  throws. This is a reading of the code, not a run: [NEEDS VERIFICATION].
- The FAQ documents the `--launch "PathToGame\Game.exe"` form for a Steam shortcut (`B/docs/FAQ.md#L37-L39`).

## Logs

- Logs go to `%APPDATA%\Reloaded-Mod-Loader-II\Logs`, with older logs in `Logs\OldLogs.zip`
  (`B/source/Reloaded.Mod.Loader.IO/Paths.cs#L26`, `#L36`, `#L46`).
- File name: `{UTC yyyy-MM-dd HH.mm.ss} ~ {exeName}.txt`, with a ` [n]` suffix on a clash
  (`B/source/Reloaded.Mod.Loader/Logging/LogWriter.cs#L31-L47`). The `LogWriter` is created in
  `B/source/Reloaded.Mod.Loader/Loader.cs#L39`.
- Mod output reaches the file: the loader's `Logger` implements `ILogger` (`B/source/Reloaded.Mod.Loader/Logging/Logger.cs#L3`),
  mods get it through `GetLogger()` (`B/source/Reloaded.Mod.Loader/Mods/LoaderAPI.cs#L65`), and `LogWriter` subscribes to its
  `OnWrite`/`OnWriteLine` (`LogWriter.cs#L37-L38`).
- `LogWriter`'s comments call it a writer "used for storing crash logs" (`#L7`) that can flush "in the event of a crash"
  (`#L25`). Whether a normal, crash-free run also writes the file: yes; see
  [`reloaded-ii-log-on-normal-run-2026-11-14.md`](reloaded-ii-log-on-normal-run-2026-11-14.md).

## Loading without the GUI (ASI)

- The bootstrapper can be loaded by Ultimate ASI Loader: copy `Loader/X64/Bootstrapper` into the game and rename
  `Reloaded.Mod.Loader.Bootstrapper.dll` to `.asi` in the ASI loader's scripts or plugins folder
  (`B/docs/InjectionMethods.md#L30-L69`, step at `#L57`).
- Since 1.1.0 the bootstrapper exports `InitializeASI`, and `ReloadedPortable.txt` should not be added (`#L63`).
- The launcher can install this itself: Edit Application -> Advanced Tools & Options -> Deploy ASI Loader (`#L68-L69`;
  `B/docs/FAQ.md#L45-L46`; `B/source/Reloaded.Mod.Launcher.Lib/Utility/AsiLoaderDeployer.cs`).

## Load messages

- Reloaded-II writes `Loading {n} Mod(s).`, then for each mod `Loading: {ModName}`, `- AppId   : {ModId}`,
  `- Location: {path}` and `- LoadTime: {ms}ms` (`B/source/Reloaded.Mod.Loader/Mods/PluginManager.cs#L84`, `#L102-L106`).
- MF.Toolkit documents no load message. Its `MLogger`
  (<https://github.com/RyoTune/MF.Toolkit/blob/main/MF.Toolkit.Reloaded/Common/MLogger.cs#L15-L43>) forwards to
  `RyoTune.Reloaded.Log`, whose prefix format wasn't checked.
- mrfpc.modloader wraps `context.Logger` (`Mod.cs#L64`), but its `Logger` class comes from a dependency, so no message
  pattern was found.
