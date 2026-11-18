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

### Summarise a log

`tools/session-log` summarises `message-log.txt` without printing game text:

```bash
tools/session-log <message-log.txt> [--from <ms>] [--to <ms>] [--offset <ms>]
```

It prints the record count by kind, each screen class with its first and last time, its `screen` records and its cursor
inputs, and, with `--from` or `--to`, the counts inside that window. `--offset` is the gap in ms between the mod's
start and the recording's start; times then also show as video times (log time minus the offset). Records with
`text=`, `label=` or quoted strings are counted, never printed, so the output can go into an issue.

### Files a run can leave in the mod's folder

These files in the deployed mod's folder (`<mods>\metaphor.accessibility`) change what a Debug run does. The folder is
outside the repository, so none is ever committed.

- `opt-in-create-msg-from-string.txt`: turns on the listener on `CreateMsgFromString` (#106), the first hook inside the
  exe's `.text1` section. It's off by default; only the file's presence counts, not its contents. Without it, Reloaded-II's
  log says `hook CreateMsgFromStringListener skipped: opt-in, and opt-in-create-msg-from-string.txt isn't in the mod's
  folder`, and the hook isn't counted in `hooks n/m`. With it, the hook still goes through the build check and signature
  scan, and counts as expected like any other. Use it for one short, separate run, then delete it.
- `opt-in-ui-text-panes.txt`: the same, for the listener on the UI text pane's draw (#93), which runs every frame and
  logs drawn text (`docs/raw-sources/ui-focus-candidates-2026-11-16.md`).
- `opt-in-dialogue-window.txt`: turns on the two dialogue window hooks (#58), in Release builds too
  ([turn on the dialogue window hooks](turn-on-the-dialogue-window-hooks.md)).
- `spike-player-name.txt`: the hero name you typed in the game, on its first line (trimmed, UTF-8). The listener compares
  each string with it and logs only `match=exact`, `contains` or `no`, never the name. Without the file it falls back to
  the `METAPHOR_SPIKE_PLAYER_NAME` environment variable, which Reloaded-II may not pass to the game.

A deploy replaces the mod's folder, removing these files, and launches the game straight after. A capture run adds the
opt-in files between the two (the listeners write to `message-log.txt` in the same folder).

### Capture run

```powershell
pwsh scripts/capture.ps1 -OptIns ui-text-panes,dialogue-window -SaveLog <folder outside the repo>
```

`scripts/capture.ps1` deploys the Debug build, closes the game it launched, creates `opt-in-<name>.txt` for each name,
and launches again through Reloaded-II. It prints `hooks n/m` from `mod-status.json` and the mod's `skipped`, `failed`
and ready lines from Reloaded-II's log of that run, so a listener that didn't go in shows at once. Without `-SaveLog`
the game is left running. With it, quit the game when the capture is done: the script then copies `message-log.txt` and
`transcript.txt` into a new timestamped folder there and prints the log's record counts. It refuses a `-SaveLog` folder
inside a git repository, since the logs hold game text. Exit codes are as for the listening run.

The spoken load message names each opt-in whose file it found, as `message strings`, `text panes` or `dialogue window`,
for example "Metaphor accessibility loaded, dev opt-ins: text panes, dialogue window". An opt-in whose hooks didn't all
go in is said with `not active`; the log says why (#223).

## The scripts' own tests

The scripts' own tests run against a stand-in launcher and game, with no game needed:

```powershell
Invoke-Pester tests/smoke
```
