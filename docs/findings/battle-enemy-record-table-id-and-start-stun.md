# A battle enemy's unit record holds its enemy table id at +0x8, and bit 0x40000 of +0x1c marks a stun at the start

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** none, see Steps; builds on
  [`battle-enemy-list-maps-hits-to-slots-static.md`](battle-enemy-list-maps-hits-to-slots-static.md),
  [`enemy-tables-affinities-exp-drops.md`](enemy-tables-affinities-exp-drops.md) and
  [`enemy-stun-break-and-battle-condition-static.md`](enemy-stun-break-and-battle-condition-static.md)
- **Functions:** none

For #921, #772 and #626. An enemy's unit record (the one the battle package's enemy list and each hit name) keeps its
`Enemy.TBL` row id as a `u32` at `+0x8`, and an enemy that starts a battle stunned has bit `0x40000` set in the
condition word at `+0x1c`. Confidence: high for the id (three of three enemies), medium for the stun bit (two battles,
every enemy stunned in both; a battle with only some enemies stunned wasn't seen).

## Evidence

From the maintainer's by-ear session of 2026-11-24 (mod build 2fdf9f63, Debug), whose `BtlPackageUpdate` lines log
each enemy record's first `0x60` bytes (`head=`):

- **Table id.** The three records the mod's enemy tables told apart by name or HP logged ids 57 (two records) and 49;
  each record's bytes `+0x8` to `+0xb` were `39 00 00 00` (57) and `31 00 00 00` (49). The third enemy of both battles,
  never told apart, had `41 00 00 00` (65), and its max HP 1,200 is row 65's in `Enemy.TBL`
  (`enemy-tables-affinities-exp-drops.md`).
- **Stun at the start.** A field strike said "Enemy stunned" (4,271,260 ms), a party member said the enemies had
  fainted, and the battle opened with the plain start wipe (anim 906, 4,273,825 ms). The list logged just before
  (4,272,756 ms) had `00 00 04 00` at `+0x1c` on all three enemies. The battle before, an ambush (anim 910), logged
  `0` on all three at its start. Its last list, after two enemies fell, logged `0x80000`, the knocked-out bit.
- **A second sample.** The 2026-11-23 session read `+0x1c` of three enemies stunned at a battle's start through the
  live inspector: `0x40000` on all three (#772's thread).

That `0x40000` means stunned rather than another start condition is `[NEEDS VERIFICATION]`: no battle with some
enemies stunned and some not was logged.

## Steps

1. Run a Debug build with `scripts/dev/session.ps1 run -SaveLog` and fight two battles: one opened by striking a
   stunned field enemy, one plain.
2. In `message-log.txt`, take each `request via=BtlPackageUpdate enemy index=` line's `head=` hex: bytes 8 to 11 are
   the table id, bytes 28 to 31 the condition word, both little-endian.
3. Match the ids against the `EnemyAffinities` lines' `id=` for the same `record=`.
