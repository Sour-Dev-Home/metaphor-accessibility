# A new day should show in the game-data block's calendar, flag, counter, quest and shop regions

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`calendar-hud-inventory-2026-11-17.md`](../raw-sources/calendar-hud-inventory-2026-11-17.md)

Static prep (#494) for #190, #192 and #200: what to watch so that ending the day in game session 3 (#498, stop 5)
shows what changes. The code that moves to the next day is not readable: the only analysed writers of the day count
are the script setters and the scheduler's set-up (below), and the rest runs in `.text1`. So the plan is a wide net
over the regions of `G` (`base+0x2ca66f0`) that the readable code names, set at stop 1 and read at stop 5. Every
expectation below is `[NEEDS VERIFICATION]`.

## Evidence

The new-game reset `0x3b2840` clears `G`'s `0xac338` bytes, then lays down the regions in order. With the getters
that read them:

| Region | `G` offset (`base+`) | Size | Read by | Expect at the day change |
|---|---|---|---|---|
| Bag counts, category 0 | `0x1c758` (`0x2cc2e48`) | `0x1000` | `GetBagItem` ([money note](money-follower-rank-and-shop-static.md)) | No change |
| Money | `0x22c58` (`0x2cc9348`) | 4 | `GetMoney` | No change, unless a day event pays |
| Flag bits | `0x22c5c` (`0x2cc934c`) | `0x798`, up to the counters | `0x3b0ab0` (bit `n` of `int` `n >> 5`); the reset clears 19 bits from `0x390d` | Some bits: the day's events |
| Counters | `0x233f4` (`0x2cc9ae4`) | 4 each; read `0x400` | `CounterGet` (`0x3b07e0`); Mag is counter 2, the shop menu `0xf0`, the quest order `0xf4` | Day-based counters move `[NEEDS VERIFICATION]` |
| Follower rank, friendship | `0x24674`, `0x246d0` | 92 each | `FollowerSetRank`, `FollowerGetFriendship` | Unchanged overnight |
| Quest records | `0x247b4` (`0x2ccaea4`) | `0xca8` | `0x3b1340` ([calendar note](calendar-date-time-and-quests-static.md)) | State 5 where a quest expired |
| Shop live stock | `0x257a8` (`0x2ccbe98`) | `0xba40`, up to the day count | `0x3b1360` ([shop note](follower-availability-and-shop-stock-static.md)) | Restocked counts, if stock is per day |
| Day count, weekday, time | `0x311e8` (`0x2cd78d8`) | `0x14` | `0x3b0390`, `0x3b03e0` | Day `+1`, weekday `+1 mod 5`, time back to morning |
| Scheduler's last time | `0x711fc` (`0x2d178ec`) | 4 | `0x3b03f0` | Follows the time once the day's script runs |

`G+0x311f8` is set by `0x3b0420` and cleared to −1 by `0x3b0330`; both also set byte `+0x8` of the calendar object `[base+0x2ca3318]`, a
"changed" flag by its look; the object's `+0x5c` gets the old day count when the readable setters set a new one. Both
are inside the day count's watch or one follow away.

Day events the script list names (`FldAccessSpotInitEveryDay`, `FldMorningMsgRequest`,
`FldMorningQuestEventUnexecutedDaysCountUp`, `Quest_Daily_Process`, `FldFollowerSetRemindLastDay`) reach `.text1` or
script state, so whichever of the regions they touch shows up in the diff.

For #200, the follower records under `F+0x60` (shop note, section 1) are heap: their watches are set by hand at stop 4
once the batch prints the node addresses.

## Tonight

`stop-1-town.txt` sets the watches (30 of the inspector's 64) and reads the large regions once; `stop-5-day-end.txt`
runs `changes` and reads them again. The inspector log keeps both reads, and the agent diffs them afterwards.

| Stop | What confirms |
|---|---|
| 1 | Every watch answers `ok <id>`. The day count's `read` matches the HUD date by the calendar note's formula |
| 4 | Watches on the follower nodes: `watch <node>+0x10 0x28` for two followers, one available and one not |
| 5, after the day-change announcement | `changes` lists the day count watch with `d + 1`; the time watch's value is the one stop 1 saw in the morning; anything else listed is what the day changed, and the region names it. The follower node watches show which byte flips for a follower who became available or absent; the count of available followers matches the Followers screen |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query.

1. `decompile 0x3b2840` (the new-game reset: region order and sizes), `0x3b0330`, `0x3b0420`, `0x3b0ab0`, `0x3b07e0`,
   `0x3b39e0`, `0x3b1340`, `0x3b1360`.
2. `callers 0x3b0400` (`0x7299a0`, the scheduler's set-up, `0x72b210`, `0x72b320`, `0x72b5e0`), `decompile 0x7299a0`,
   `0x72b5e0`, `0x72b630`; `offset 0x311f8`.
3. `decompile 0x90db00`, `0x9038e0`: each a jump into `.text1`, stopped there.

Nothing in `.text1` was decompiled. No game text is quoted.
