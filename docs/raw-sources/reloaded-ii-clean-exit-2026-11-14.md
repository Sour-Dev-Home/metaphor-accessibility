# Closing the game's window asks "Exit game?"; it doesn't quit on its own

- **Source:** two runs on the maintainer's PC with Reloaded-II 1.31.0 (as in `reloaded-ii-log-on-normal-run-2026-11-14.md`),
  driven from PowerShell: `Reloaded-II.exe --launch "<game folder>\METAPHOR.exe"` to start, and
  `System.Diagnostics.Process.CloseMainWindow()` (which posts `WM_CLOSE`, as Alt+F4 or the window's X button do) to close.
  The maintainer watched the screen and clicked. Answers issue #28.
- **Saved:** 2026-11-14.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018), no mods
  enabled.

## Steps

1. Started the game with `--launch` and waited for the title screen.
2. Read the log file directly while the game ran (opened for reading with sharing, not only listed).
3. Sent `CloseMainWindow()` and waited 30 seconds for the process to exit.
4. Confirmed the in-game prompt by hand, then read the log again.
5. Repeated steps 1, 3 and 4 to see which prompt button is selected by default.

## Result

- `--launch` with a registered exe starts the game, and the launcher exits by itself.
- The game was still running 30 seconds after `CloseMainWindow()` returned `True`: it showed an "Exit game?" prompt
  instead of quitting. So closing the window can't end an unattended run by itself.
- Confirming the prompt is a clean exit: the game exited and the log's last line was
  `[13:53:52] [Reloaded] ExitProcess Hook: Log End` (and `[13:56:52]` in the second run). No crash dump was written.
- In the prompt, Confirm is selected by default. When the game window lost focus, the selection highlight disappeared.
- The log is readable live: reading the open file at the title screen gave the loader's 15 startup lines, all written at
  launch, while a folder listing taken earlier in the run had shown 0 bytes.
- The maintainer reports that in fullscreen the game can freeze after loading until its window is clicked again, and
  suggests borderless windowed mode for these tests. The cause wasn't tested: [NEEDS VERIFICATION].

## What this means for an unattended run

- Sending `WM_CLOSE` and then Enter would rely on Confirm being selected, which needs the game window to have focus.
  Nothing guarantees focus in an unattended run.
- Killing the process skips the loader's exit hook, so the `Log End` line is lost. Lines written earlier are on disk
  already (above), so only the end marker and anything after the last write are lost. Not tested.
