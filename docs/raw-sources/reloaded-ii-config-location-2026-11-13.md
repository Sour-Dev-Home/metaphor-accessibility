# Reloaded-II config location, portable mode and side-by-side installs

- **Source:** Reloaded-II source on GitHub, `master` branch, read via the GitHub REST API (`gh api`), and its docs in
  `docs/` of the same repository. Line numbers are as fetched on the date below. Nothing was downloaded or run.
- **Saved:** 2026-11-13.
- **Game build:** none; this is about the loader, not the game.

`B` below means <https://github.com/Reloaded-Project/Reloaded-II/blob/master>. Builds on
`reloaded-ii-launch-and-logs-2026-11-13.md` (no launch argument selects mods; the mod set is the application's saved
`EnabledMods`).

## The root config is at a fixed path

- `ReloadedII.json` lives in `<Roaming AppData>\Reloaded-Mod-Loader-II\`, built from
  `GetFolderPath(SpecialFolder.ApplicationData)` (`B/source/Reloaded.Mod.Loader.IO/Paths.cs#L26`, `#L31`). Both lines say
  "DO NOT CHANGE, C++ BOOTSTRAPPER ALSO DEFINES THIS".
- The launcher reads it from there (`B/source/Reloaded.Mod.Launcher.Lib/IoC.cs#L26-L27`), and so does the loader inside
  the game process (`B/source/Reloaded.Mod.Loader/Loader.cs#L27`).
- The native bootstrapper finds it independently: `SHGetSpecialFolderPath(..., CSIDL_APPDATA, ...)` plus
  `\Reloaded-Mod-Loader-II\ReloadedII.json`, and throws if it is missing
  (`B/source/Reloaded.Mod.Loader.Bootstrapper/LoaderConfig.cpp#L13-L32`). It then takes the loader DLL from the config's
  `LoaderPath64` (`#L100-L129`).
- No environment variable or argument overrides this path. A code search for `GetEnvironmentVariable` finds only
  `STEAM_APPID` in the loader's shared code (`B/source/Reloaded.Mod.Shared/Environment.cs#L38`); the other hits are in the
  installer and launcher utilities. The launcher's arguments are the fixed list in the existing note.
- Setting `%APPDATA%` before launch probably does not move it: both APIs resolve the known folder, not the variable.
  [NEEDS VERIFICATION]
- Logs and crash dumps are also under this fixed folder (`Paths.cs#L36`, `#L41`).

## What the root config points to (the movable part)

- `ReloadedII.json` stores the directories for apps, mods, mod user configs, misc and plugins as serialized properties
  (`B/source/Reloaded.Mod.Loader.IO/Config/LoaderConfig.cs#L54-L79`), defaulting to `Apps`, `Mods`, `User/Mods`,
  `User/Misc`, `Plugins` next to the launcher (`#L8-L12`).
- If a stored directory doesn't exist, it is reset to the default next to the launcher (`#L217-L221`, `#L273-L279`). So a
  throwaway directory must exist before launch.
- The in-game loader uses these: it finds the game's `ApplicationConfig` by exe path in `GetApplicationConfigDirectory()`
  (`B/source/Reloaded.Mod.Loader/Loader.cs#L199-L216`) and mods in `GetModConfigDirectory()` (`#L115`, `#L133`).
- Reloaded's own test harness takes this route: it backs up `ReloadedII.json`, writes a test config with its own
  directories, and restores the original on dispose (`B/source/Reloaded.Mod.Loader.Tests/SETUP/TestEnvironmoent.cs#L75-L131`).

## Portable mode (two different files)

- `portable.txt` next to the launcher: the app, mod, user and plugin directories are taken from the launcher's folder
  instead of the stored ones (`LoaderConfig.cs#L182-L190`, `#L303-L307`). The docs list it as experimental and say "Other
  global settings still apply" (`B/docs/ExperimentalFeatures.md#L10-L16`). `ReloadedII.json` itself stays in AppData.
- Inside the game the loader is not the launcher, so it locates the launcher folder (and `portable.txt`) through the
  config's `LauncherPath` (`LoaderConfig.cs#L309-L331`).
- `ReloadedPortable.txt` next to the bootstrapper is unrelated: it makes the bootstrapper restart the game through the
  launcher (`B/source/Reloaded.Mod.Loader.Bootstrapper/dllmain.cpp#L80-L83`; `B/docs/InjectionMethods.md#L43-L45`).

## Two installs share global state

- `--launch` rewrites the shared `ReloadedII.json` with the running launcher's `LauncherPath`, `LoaderPath32/64` and
  `Bootstrapper32/64Path` before starting the game (`B/source/Reloaded.Mod.Launcher.Lib/Startup.cs#L81-L83`;
  `LoaderConfig.cs#L235-L264`). Opening the GUI does the same (`B/source/Reloaded.Mod.Launcher.Lib/Setup.cs#L254-L258`).
  So a second install takes over the loader paths that ASI or bootstrapper launches use until the other launcher runs
  again.
- The GUI also sets the user environment variable `RELOADEDIIMODS` to its mods directory (`Setup.cs#L306-L307`) and
  registers protocol handlers under `HKCU\Software\Classes` (`Setup.cs#L215-L247`).
- No mutex or single-instance check turned up (code search for `Mutex` and `SingleInstance`: no hits). The bootstrapper's
  "already loaded" markers are named per process ID (`dllmain.cpp#L194-L235`), so they don't clash across games.

## Answer for #16

**Partly.** No supported way exists to point Reloaded-II at a separate `ReloadedII.json`: launcher, in-game loader and
bootstrapper all hard-code the AppData path. But that file is the only global piece the loader needs, and the folders it
names are movable.

Approach, the same one Reloaded's own tests use:

1. Back up `ReloadedII.json`.
2. Write a copy whose `ApplicationConfigDirectory`, `ModConfigDirectory`, `ModUserConfigDirectory`,
   `PluginConfigDirectory` and `MiscConfigDirectory` point to throwaway folders that already exist, holding a game
   `ApplicationConfig` with the test mods in `EnabledMods`.
3. Launch with the user's own launcher (`--launch`), so the loader paths it rewrites stay the user's.
4. Restore the backup, even on failure.

A second portable install avoids editing the directory fields but still rewrites the shared loader paths, so it needs the
same backup and restore and gains nothing.

Still to check locally: that the edited directories survive a `--launch` run and the in-game loader loads only the test
mods; that `%APPDATA%` has no effect; and how a running launcher GUI reacts to the swapped file.
[NEEDS VERIFICATION]
