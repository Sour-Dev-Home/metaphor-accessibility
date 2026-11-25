# A field enemy sees through three sector zones on its controller, each turned to its facing every update

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-enemy-awareness-static.md`](field-enemy-awareness-static.md),
  [`field-enemy-strikes-and-notice-static.md`](field-enemy-strikes-and-notice-static.md)
- **Functions:**
  - FldEnemyGetDegToPlayer | 0x1408dbe70 | .ecode | Field | known only | Script function: the world angle from an enemy to the player, in degrees; not its facing. | finding
  - EnemyPerceives | 0x140650070 | .ecode | Field | known only | The perception test: the zone tests and the line-of-sight test, true when the enemy sees the player. | finding
  - EnemySightZones | 0x14064c7d0 | .ecode | Field | known only | Tests the player against zone 1 or zone 2 by height difference; stores the result at controller +0x280. | finding
  - EnemyLineOfSight | 0x14064c560 | .ecode | Field | known only | A ray between the enemy's and the player's nodes; true when nothing blocks it. | finding
  - EnemyNearZone | 0x14064c660 | .ecode | Field | known only | Tests the player against zone 3; stores the result at controller +0x281. | finding
  - SectorContains | 0x1413eb400 | .ecode | Field | known only | Whether a point lies in a sector: within a radius of an axis and an angle of a forward direction. | finding
  - EnemySightZonesUpdate | 0x140659350 | .ecode | Field | known only | Moves the three zones to the enemy's position and its flattened forward direction. | finding
  - EnemyChooseState | 0x140653770 | .ecode | Field | known only | A state's next-state choice: asks for state 0x1f when the perception test passes. | finding
  - EnemyParams | 0x140637ef0 | .ecode | Field | known only | The enemy's parameter row: row u32 +0xb4 of 0xb8-byte rows in the field database's table at +0x108. | finding

For #948 (and #693, #649). An enemy's sight is three sector zones held on its controller `C = [enemy+0x3110]`
(`fldCharaCtrl_Enemy`), at `C+0x1c0`, `C+0x200` and `C+0x240`. Every update (`fldCharaCtrl_Enemy::vf4`, `0x658bc0`)
moves each zone's origin to the enemy's position plus a fixed offset and turns its forward direction to the enemy's
facing with the height dropped, so `C+0x1d0` (x, y, z floats) is the enemy's horizontal facing as its sight uses it.
The perception test passes when the player is inside zone 1 or zone 2 and the line of sight is clear, or inside zone 3
by its own test, and the state choice then asks for state `0x1f` (31). Nothing here is checked in memory: every value
is `[NEEDS VERIFICATION]` until a Debug session reads it.

| Value | Where | Confidence |
|---|---|---|
| Facing | Not `FldEnemyGetDegToPlayer` (`0x8dbe70`): that is the angle from the enemy's position to the player's, `atan2` scaled. The zones' forward `C+0x1d0` (also `+0x210`, `+0x250`) is the enemy's forward, written by `0x659350` from the enemy's transform at `enemy+0x10` (`0x96f6f0`), its y zeroed and normalised | High in code; units and sign of the forward not checked live |
| Zone layout | 16-byte vectors: origin `+0x0`, forward `+0x10`, axis `+0x20`, then floats radius `+0x30` and angle `+0x34`, as `0x13eb400` reads them (the point's distance from the axis line against the radius, then its angle from the forward against the angle, only when the angle is below a constant) | Medium: the axis is taken as up from its use; the angle's unit (degrees or radians, half or full) is `[NEEDS VERIFICATION]` |
| Zone sizes | Radius and angle are written once, in setup (`fldCharaCtrl_Enemy::vf3` writes `C+0x1f0`, `+0x234`; `vf1` writes `+0x274`) | Medium; their source row is not followed |
| Which zone | `0x64c7d0`: height difference below param `+0x1c` tests zone 1, else below `+0x28` tests zone 2; `0x64c660`: below `+0x34` tests zone 3 | High in code |
| Walls | `0x64c560` casts between the enemy's node 3 and the player's node 4 (`0x5d5150`); a hit means not seen | Medium: named by its use |
| Forced notice | byte `C+0x339` set makes the test pass at once | High in code |
| "Noticed" state | `0x653770` returns `0x1f` when the perception test passes and the level's discover script doesn't run; `FldEnemySetAIState_Discover` asks for `0x1e` | High in code; which index the player hears as "noticed" stays #649's `[NEEDS VERIFICATION]` |

## What #693 can build on this

A sight-zone sound needs, per enemy in range: the zone 1 origin, forward, radius and angle (`C+0x1c0` to `C+0x1f4`),
and the player's position. The distance to the zone's edge is then plain geometry in a plain library, and the wall
test is the mod's own navmesh or the game's ray result. Before building it, a Debug session should log the four values
for one enemy while it turns, to settle the angle's unit and that `C+0x1d0` follows the enemy's facing.

## Steps

Ghidra 12.2 with JDK 25.0.5.8 (`docs/contributors/how-to/query-the-ghidra-project.md`), the shared project built from
the exe above, on 2026-11-24, in `pwsh scripts/dev/ghidra.ps1 batch` runs: `decompile` of `0x8dbe70`, `0x650070`,
`0x64c7d0`, `0x64c560`, `0x64c660`, `0x13eb400`, `0x637ef0`, `0x64fff0`, `0x653770` and `0x659350`; `callers` of
`0x650070`, `0x64c7d0` and `0x659350`; `offset` of `0x1c0`, `0x1d0`, `0x1f0`, `0x234` and `0x274` in all analysed code,
filtered to `fldCharaCtrl_Enemy`.
