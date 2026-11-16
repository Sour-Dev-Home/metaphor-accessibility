# Reloaded-II application and mod config files

- **Source:** Reloaded-II source on GitHub at commit `458987f26bc8d78fdc0f86b73fc0d6fd4b673d01` (`master` on the date
  below), read via the GitHub REST API (`gh api .../contents/<path>?ref=<commit>`). Nothing was downloaded or run.
- **Saved:** 2026-11-15.
- **Game build:** none; this is about the loader, not the game.

`B` below means <https://github.com/Reloaded-Project/Reloaded-II/blob/458987f26bc8d78fdc0f86b73fc0d6fd4b673d01>. Builds on
`reloaded-ii-config-location-2026-11-13.md` (the directory fields in `ReloadedII.json`) and
`reloaded-ii-launch-and-logs-2026-11-13.md` (`--launch` uses the application's saved `EnabledMods`). Checked for #16.

## Application config

- File name `AppConfig.json` (`B/source/Reloaded.Mod.Loader.IO/Config/ApplicationConfig.cs#L8`), read from the
  application config directory to a depth of 2 (`#L104-L110`), so one folder per application.
- Fields include `AppId`, `AppName`, `AppLocation`, `AppArguments`, `EnabledMods` (`string[]`), `SortedMods` and
  `DontInject` (`#L16-L36`).
- `AppLocation` may be relative (`GetAbsoluteAppLocation`, `#L168-L194`), then the result is made a full path:
  - a path with a drive (`C:\...`) is used as is;
  - a path whose root is just `\` is put on the drive of the folder holding `AppConfig.json`;
  - any other path is combined with that folder.

  So a copy moved to another folder needs an absolute `AppLocation`.
- `--launch` picks the application whose `GetAbsoluteAppLocation` equals `Path.GetFullPath` of the argument, an exact
  string comparison (`B/source/Reloaded.Mod.Launcher.Lib/Startup.cs#L88-L91`).

## Mod config

- File name `ModConfig.json` (`B/source/Reloaded.Mod.Loader.IO/Config/ModConfig.cs#L10`), one per mod folder.
- `ModId` is the identifier `EnabledMods` lists (`#L21`; matched in `ApplicationConfig.cs#L146-L158`). `ModDll` names
  the mod's assembly (`ModConfig.cs#L26`).
