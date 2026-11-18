# Where the game holds battle state: capture evening 2, session 6 (#162)

- **Source:** capture evening 2 (#250), session 6, on the maintainer's machine through Reloaded-II, Debug mod build from
  branch `capture-kit-2` (tip `1ff25fc` when analysed; the log doesn't name the deployed commit) with
  `opt-in-battle.txt`, so `BattlePackageUpdateListener` logged `battle …` records (formats in
  `battle-state-leads-2026-11-17.md` on `capture-kit-2`). Recorded with OBS (about 19 min) and narrated; the narration was transcribed
  locally with the `watch` skill. The mod's `message-log.txt` (about 22 min, 1,617 `battle` records) was read by record
  kind and offset with awk; five frames were taken with ffmpeg 9.0.2 and read by eye (party HP and MP, turn icons), then
  deleted. The log, recording and frames stay local; only offsets, numbers, class names, counts and times are kept here,
  no game text. Video time = log time − 159 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Offsets are into the objects the listener names: `Package@btl` (package), `Order@btl` (order), `Gui@btl` (gui), the
global at RVA `0x27293b0` (info) and the `0x16A8`-byte party units (unit, `party index=1..6`). Times are log seconds.

## The three fights

| Fight | Log time | Phases logged | Narrated |
|---|---|---|---|
| 1 | 374–577 | `Load`, `Beginning`, `Fight`, `Victory`, `Finalize` | turn icons, a weakness hit and its half turn, Guard, an item on an ally, enemy turn, win, results |
| 2 | 905–1095 | `Load`, `Beginning`, `Fight`, `Victory`, `Finalize` | one enemy, Pass, a long fight, win |
| 3 | 1160–1300 | `Load`, `Beginning`, `Fight`, `AllyEscape`, `Interruption`, `Finalize` | started with advantage (three field strikes), Pass, Guard, an item, then escape |

Every fight logged `Beginning` once and `Fight` once: **`Beginning` runs once per battle, not per turn** (corrects the
lead), and the whole battle, both sides' turns, runs inside `Fight`. No `AdvantageousTransition` or `PinchTransition`
phase appeared, not even in the advantage fight. No ambush was captured (no fourth `start`). The listener logged no
`object`, `object-fields` or `action-gone` record, and the only `action` records are the four `Participate@action@btl`
created at each `Load`: no per-action object was seen in the action list or at `+0x6F0`.

## Per element

| Element | Where it lives | How sure |
|---|---|---|
| Battle start | `start` record (new `Package@btl`), then phase `Load` | Confirmed: 3 of 3 fights, times match the narration. |
| Enemy count | package `+0x140` (3, 1, 2 at start; minus one per defeat); `+0x120` party count (4); `+0x160` all units; order `+0x20` the same | Confirmed by the frame of fight 1 (3 enemies) and the defeats below. Which enemies (IDs, names) `[NEEDS VERIFICATION]`. |
| Advantage or ambush | package `+0x298` = 1 and `+0x29C` = 1 only in the advantage fight (`+0x29C` 0 in fight 1, −1 in fight 2) | Lead: one advantage sample. Ambush value `[NEEDS VERIFICATION]` (not captured). Not a phase. |
| Side's turn starts | info `+0x14` and package `+0x2D8`: player turns (1, 2, 3…), set as each player turn opens; package `+0x2DC`: enemy turns; package `+0x2D4`: every side change; gui `+0x94` the player turn shown | Confirmed: the enemy turn in fight 1 (489) matches the narrated enemy skills (video 5:35); same pattern in fights 2 and 3. |
| Each action committed | package `+0x2F0`: player commands (starts at 1, +1 per command); package `+0x2F4`: enemy actions | Confirmed: `+0x2F0` steps at 404, 420, 435, 445, 465 in fight 1's first turn; `+0x2F4` steps only in enemy turns. |
| Whose turn (acting member) | `[NEEDS VERIFICATION]` | No field or ref tracked the acting member. Party unit `+0x1644` and `+0x164C` change at `Beginning` (order or row, a lead). |
| Party target of an enemy action | package `+0x788` → that member's `Participate@action@btl` | Strong lead: set at each enemy action (`+0x2F4` step) in all three fights. Participate-to-unit mapping `[NEEDS VERIFICATION]`. |
| Ally target of a player command | package `+0x7A8` → a `Participate@action@btl` | Lead: set while choosing an ally (fight 1, 451–465, the narrated item on an ally). |
| Targeted enemy | `[NEEDS VERIFICATION]` | No ref or field followed enemy targeting. `UnitAffinity` and `UUIC_Battle_TargetCursor` stay the leads. |
| Command wheel state | package `+0x1C0`: an input state (2 at the wheel, 3–9 in sub-menus and targeting); package `+0x24C` bit `0x40` and order `+0x40` toggle around each command | Lead: values cycle with each command; meanings per value `[NEEDS VERIFICATION]`. |
| Attacker, action, per-target damage, Weak/Resist/Miss | `[NEEDS VERIFICATION]` | Not in the logged objects. Needs a reader on the action objects or `UUIC_Battle_Damage`. |
| Defeats | package `+0x438` (enemies defeated, 1, 2, 3) and `+0x140` (alive) at the moment of the defeat; package `+0x3E0`–`+0x3EC`, `+0x434` add up each defeated enemy's rewards | Confirmed timing (fight 1: one at 462, two at 550). Which enemy `[NEEDS VERIFICATION]`. |
| Party HP and MP | unit `+0x0C` HP, `+0x10` MP | Confirmed against two frames (HP and MP numbers in the party panel). Logged only at phase changes; per-action reads `[NEEDS VERIFICATION]`. |
| Statuses (ailments, buffs) | unit `+0x38`: bit `0x40000` set during fight 2 on one member, cleared at `Finalize` | Lead only; which ailment `[NEEDS VERIFICATION]`. Buffs and enemy statuses not seen. |
| Turn icons, full and half | `[NEEDS VERIFICATION]` | Frames show 4 icons at a player turn's start, a half icon after the weakness hit. No logged field matches the counts (package `+0x2E8` and `+0x2E4` change at side changes but don't match). `UUIC_Battle_PressTurnIcon` stays the lead. |
| Escape | phase `AllyEscape`, then `Interruption`, `Finalize`; unit `+0x0` bit `0x200` set on the members present | Confirmed once (fight 3, narrated). |
| Results | phase `Victory`, then `Finalize`; package `+0x3E4` = money (355 in fight 1, matches the narration) | Money confirmed once; other package totals (`+0x3E0`, `+0x3E8`, `+0x3EC`, `+0x434`) don't match the narrated EXP or MAG `[NEEDS VERIFICATION]`. |
| Growth | unit `+0x24` EXP (+854 each active member in fight 1, less for the reserve); archetype pairs (rank, EXP) at member-specific offsets (`+0x158/+0x15C`, `+0x380/+0x384`, `+0x3E0` EXP, `+0x214` EXP): +311 each, matching the narrated archetype EXP | Confirmed: archetype EXP 311 matches; two archetype rank-ups seen (`+0x380` 9→10, `+0x158` 16→17). Level-up field `[NEEDS VERIFICATION]` (none happened). |

## What the next listener should add

Read per frame, not per phase: the party units' HP and MP, the four `Participate` actions' fields with a unit pointer
search, and the turn-icon and damage components (`UUIC_Battle_PressTurnIcon`, `UUIC_Battle_Damage`,
`UUIC_Battle_TargetCursor`). Capture an ambush.

## Steps

1. Count `battle` records by kind; list `start`, `phase`, `refs`, `action` and `party` records in order.
2. For package, order, gui and info, collect each offset's values over time from `fields … changes=` (awk), and diff the
   three start snapshots.
3. Line the steps up with the narration (video time + 159 s) and check HP, MP and turn icons on frames at video 219,
   246, 284, 289 and 343 s.
