# The turn side and icons, an ambushed start and a battle's end show in the Debug message logs

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** none, see Steps; builds on
  [`battle-turn-icons-and-ui-components-static.md`](battle-turn-icons-and-ui-components-static.md) and
  [`battle-start-and-turn-notice-layouts-static.md`](battle-start-and-turn-notice-layouts-static.md)

For #252, #626 and #166. Four Debug session logs (2026-11-22 and 2026-11-23) confirm three leads: the int at `G+0x25460`
is 0 on the player's turn and 1 on the enemy's, with the full icons at `+0x25464`; `BattleCutInWipeLayout` opens with
animation 910 when the enemy strikes first; and `TurnInfoLayout` plays 2572, 2570 or 2571 once, as a battle is won,
escaped or lost. `BattleNoticeLayout` (0xa66, 0xa67) plays before each action, not as an advantage banner.

## Evidence

- **Side and icons.** The 2026-11-22 16.44 log's `icons=side,A,B,...` field (logged with each banner and hit by that
  day's Debug build) reads `0,4,0` at a player turn's start, then `0,3,1`, `0,2,1`... as allies act, and `1,3,0` on
  enemy actions, whose hits land on `side=Ally` records. So side 0 is the player's turn and A the full icons left, as
  the static note leads. B reads as half icons (one appears after a weakness hit); C was 0 throughout.
- **Action notice.** `BattleNoticeLayout` anim 2662 (`0xa66`, side 0) comes about 1.3 s after each ally action's
  move banner, 2663 (`0xa67`) before each enemy action: 151 and 81 records over four logs. 2669 and 2670 (`0xa6d`,
  `0xa6e`) appear only in the 2026-11-22 13.05 log's tutorial fights. None marks a battle's start.
- **Ambushed start.** `BattleCutInWipeLayout` opens every battle: anim 906 ten times, 910 twice. Both 910 battles
  open with an enemy action (`BattleNoticeLayout` 2663, or a move banner with `icons=1,...`); the 906 ones open with an
  ally's, including the 2026-11-23 start against stunned enemies. So 910 is an enemy-first start. A player-advantage
  start is not told apart from a plain one by this layout `[NEEDS VERIFICATION]`.
- **End.** `TurnInfoLayout` plays (with `KingLayout` 1011 or 1012) once a battle, never at a turn change: 2572 right
  after the last enemy's "defeated" hit line (three battles), 2570 after an "Escape" banner with the field's layouts
  next (three), 2571 after the protagonist's hit to 0 HP (two). So 2572 is a win, 2570 an escape and 2571 a loss. The
  results screen after a win draws its numbers as panes with no message text; only the spoils' item names come
  through `GetMessageBySerial` `[NEEDS VERIFICATION]`.

## Steps

Debug builds of the mod as deployed by `scripts/dev/session.ps1` on the dates above; the logs stay local (never
committed). In each session's `message-log.txt`:

1. `grep -o -E "class=(TurnInfoLayout|BattleNoticeLayout|BattleCutInWipeLayout)@ui@app at=[0-9a-fx]+ anim=[0-9]+"`,
   counted by class and animation.
2. For each `TurnInfoLayout` and `BattleCutInWipeLayout` record, the records 15 s either side
   (`BattleHitResult`, `BattleMessageShow`, other `ui-anim` classes) and the session's `transcript.txt` lines.
3. `grep -o -E "via=Battle(HitResult|MessageShow).*icons=[0-9,-]+"` in the 2026-11-22 16.44 log, read in order.
