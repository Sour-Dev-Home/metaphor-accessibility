# Reloaded-II writes its log on a normal, crash-free run

- **Source:** a run on the maintainer's PC with Reloaded-II 1.31.0 (`1.31.0+458987f26bc8d78fdc0f86b73fc0d6fd4b673d01`,
  `Release.zip` from <https://github.com/Reloaded-Project/Reloaded-II/releases/tag/1.31.0>, SHA-256
  `c893da3ba8596d9266826bd5ca8aacd25fe3b53d09314324ab65c7b618a05f8c`, matching GitHub's published digest). Folders were
  listed with PowerShell `Get-ChildItem`. Answers the open question in `reloaded-ii-launch-and-logs-2026-11-13.md`.
- **Saved:** 2026-11-14.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (SHA-256 of `METAPHOR.exe`), Steam
  build 18330018 (`buildid` and `TargetBuildID` in `appmanifest_2679460.acf`). The game shows no version number.

## Steps

1. Before: Reloaded-II had never run on this Windows account, so `%APPDATA%\Reloaded-Mod-Loader-II` didn't exist.
2. Extracted `Release.zip` to `%LOCALAPPDATA%\Programs\Reloaded-II`, ran the launcher once and added `METAPHOR.exe` as an
   application. No mods were enabled.
3. Launched the game from the launcher and reached the title screen.
4. While the game ran, listed `%APPDATA%\Reloaded-Mod-Loader-II\Logs`.
5. Quit from the game's own menu, then listed the folder again and read the log.

## Result

- A normal run writes the log. The file `2026-11-14 18.38.08 ~ METAPHOR.txt` was created when the game started.
- While the game ran, the folder listing showed 0 bytes. That was the listing lagging, not the file: in a second run the
  same day (see `reloaded-ii-clean-exit-2026-11-14.md`), reading the open file directly gave all 15 startup lines within
  a second of launch, while a listing taken earlier had also shown 0 bytes. Read the file, not the listing.
- After the normal quit it held 17 lines (1,211 bytes). The last line is
  `[13:40:56] [Reloaded] ExitProcess Hook: Log End`, written by the loader's hook on the game's exit.
- The file name uses UTC (18.38.08). The lines inside use local time (13:38:08).
- The first run created `CrashDumps`, `Logs` (with an empty 22-byte `OldLogs.zip`), `ProfileOptimization` and
  `ReloadedII.json` under `%APPDATA%\Reloaded-Mod-Loader-II`. `CrashDumps` stayed empty.

Log excerpt (local paths replaced with placeholders):

```text
[13:38:08] [Reloaded] Create Loader | Time: 23ms
[13:38:08] [Reloaded] Loading 1 Mod(s).
[13:38:08] [Reloaded] Loading: Reloaded II Shared Lib: Reloaded.Hooks
[13:38:08] [Reloaded] Loaded via: <Reloaded-II>\Loader\x64\Bootstrapper\Reloaded.Mod.Loader.Bootstrapper.dll
[13:38:08] [Reloaded] Current Process Location: <game folder>\METAPHOR.exe
[13:38:08] [R-II SteamHook] Found Steam Library Entry with Id 2679460. Dropping steam_appid.txt.
[13:38:08] [Reloaded] Total Loader Initialization Time: 178ms
[13:40:56] [Reloaded] ExitProcess Hook: Log End
```

## Side effects outside the log folder

- The loader always loads its bundled `reloaded.sharedlib.hooks` mod (in `<Reloaded-II>\Mods`), even with no mods
  enabled.
- Its Steam hook wrote `steam_appid.txt` (7 bytes) into the game folder at launch, and the file stayed after the quit. A
  smoke test that restores the game folder has to account for it.
