# Some event scenes show their lines without MsgRequestByName

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** [`game-session-1-2026-11-20.md`](../raw-sources/game-session-1-2026-11-20.md)
- **Functions:** none

The scene after the opening cutscene, from `EN/message/event/e01_002_001.msg` and `e01_004_001.msg`, shows its lines
in a message window, but none of them is requested through `MsgRequestByName`. The dialogue reader only hears that
function, so it reads none of these lines, and its repeat key still holds the opening's last line.

## Evidence

- In both new games the two files compile, `msgwindow-hook` logs `close`, and the window pointer (`msgwindow
  current=`) turns non-null for the scene: about 64 s in the first game, about 100 s in the second. Within it: no
  `request`, `select` or `resolve` record, no `msgwindow-hook open`, and no long `pane` text.
- The opening scene before it (`e00_002_001.msg`) sent each read line as a `request`, 15 per game. Gallica's later
  field lines (`dng_D00_01.msg`) came as `request` records after `msgwindow-hook open`, and were read.
- After the scene's last `request` the transcript repeats the opening's last line (three times, then twice): the repeat
  key replaying a stale line.
- The function these lines go through is `[NEEDS VERIFICATION]`. The window object is live while they show, so reading
  it, or hooking what fills it, are the leads.

## Steps

Session 1 (#496), stop 6, with `dialogue-window` on. Offline: the `compile`, `msgwindow`, `msgwindow-hook`, `request`,
`resolve` and `pane` records between the scene's compile and the next `request` (grep, awk; counts only).
