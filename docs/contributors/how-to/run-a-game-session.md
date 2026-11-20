# Run a game session

A game session runs a Debug build of the mod in the real game, to listen to what a hook sees, record a play-through or
ask the running game questions. You need the game registered as an application in Reloaded-II and its
[build in the known-builds list](add-a-game-build.md); the deploy refuses any other build.

1. **Pick the opt-ins.** An opt-in is an empty file, `opt-in-<name>.txt`, in the deployed mod's folder that turns on a
   dev-only hook. The names and what each does are
   [listed here](deploy-a-dev-build.md#files-a-run-can-leave-in-the-mods-folder): `create-msg-from-string`,
   `ui-text-panes`, `dialogue-window` and `inspector`; `pwsh scripts/dev/session.ps1 status` lists the ones the mod
   reads. A session that needs no listener needs none.
2. **Close the game and Reloaded-II.** The session won't start while either runs.
3. **Deploy, add the opt-ins and launch**, from the repository root in PowerShell 7:

   ```powershell
   pwsh scripts/dev/session.ps1 run -Seconds 3600 -OptIn ui-text-panes,inspector -SaveLog <a folder outside the repo>
   ```

   It deploys the Debug build, creates the opt-in files, launches through Reloaded-II and prints `hooks n/m`. Check
   that `n` is what you expect and that no line says `skipped: opt-in` or `failed` for a listener you asked for; if
   one does, quit and fix it before playing. Pick `-Seconds` longer than you mean to play: at the end the game is
   killed, and unsaved progress is lost. Details and exit codes: [run](deploy-a-dev-build.md#run).
   For a check with no play-through, `pwsh scripts/dev/session.ps1 run -Seconds 300` deploys, watches for that long,
   closes the game and fails if it exited.
4. **Play.** Do what the issue's steps say, and narrate what you do and hear. To keep a recording, start OBS first
   and save it outside the repository; it is optional, and the log alone answers many questions. The Debug mod writes
   its development log, `message-log.txt`, in the mod's folder; it holds game text.
5. **Ask the running game** (only if you used the `inspector` opt-in), in a second terminal while the game runs:

   ```powershell
   pwsh scripts/dev/session.ps1 inspect 'base' 'read base+0x1000 16'
   ```

   The commands and exit codes: [inspect the running game](inspect-the-running-game.md#commands).

   To ask the same things at several game states without typing mid-play, write the requests in a file before you
   start, one per line, `#` for comments and a `[name]` line before each state's requests:

   ```text
   # states.txt
   [title-screen]
   base
   read base+0x1000 16
   [first-battle]
   follow base+0x2a8f0 0x8 0x10
   ```

   Then start it once with `-WaitKey`. At each state, switch to the terminal and press Enter to run the next section,
   or type `q` and Enter to stop:

   ```powershell
   pwsh scripts/dev/session.ps1 inspect -Batch states.txt -WaitKey -SaveLog <a folder outside the repo>
   ```

   It checks every command against the inspector's own list before the first state, so a typo fails at once. Each
   answer is printed and appended to `inspector-log.txt` in that folder, as the time, the label (`-Label`, default the
   file's name, then `/` and the section), the request and the response, separated by tabs. Without `-WaitKey` it runs
   every section at once. It holds the inspector's one connection until it ends.
6. **Quit the game** when you are done. With `-SaveLog`, the session then copies `message-log.txt` and `transcript.txt`
   into a new timestamped folder there, prints the record counts and removes the opt-in files. It refuses a folder
   inside a git repository, also one reached through a link or subst drive, and needs git to check. Without
   `-SaveLog`, copy those two files out of the mod's folder yourself before the next deploy, which replaces it.
7. **Summarise the log** without printing game text, with
   [`tools/Metaphor.SessionLog`](deploy-a-dev-build.md#summarise-a-log). Pass `--offset` when you have a recording.

## Where the results go

- **Logs, recordings, memory reads of game text:** nowhere in git. They are game content
  ([rule 4](../../../AGENTS.md#ground-rules)); the checks block them by folder, extension, size and hash
  ([game content block](a-check-or-ci-failed.md#a-game-content-block)).
- **What you learned:** a raw source with no game text (counts, offsets, the steps and the tool versions), then a
  finding that cites it. [Write a finding](write-a-finding.md).
- **A listener that didn't go in, or a hook that failed:** a comment on the issue, with the `hooks n/m` line and the
  Reloaded-II log lines the script printed. Its output shortens local paths, so it can be pasted as it is.
