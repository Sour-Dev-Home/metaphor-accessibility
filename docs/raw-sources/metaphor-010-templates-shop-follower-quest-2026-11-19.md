# Metaphor 010 Editor templates: shop, follower, quest and special-day tables (#192-#200)

- **Source:** `DeathChaos25/metaphor-010-templates` at commit `cfccaa2f4e4352d2881884b1c7d61c53661259d3` (last push
  2025-02-02; no licence file, so nothing is copied, only field names, order, types and sizes), read through the GitHub
  REST API (`gh api repos/DeathChaos25/metaphor-010-templates/contents/<file>?ref=<commit>`): `ShopBIN.bt`,
  `ShopTable.bt`, `FollowerTable.bt`, `FollowerLayout.bt`, `FollowerRankUp.bt`, `SpecialDay.bt`, `QuestID.bt`,
  `Reward.bt`, `metaphor_enums.bt`. The repository's file list (`git/trees/HEAD?recursive=1`) has no money, calendar,
  weather or save-data template. Nothing was run and no game file was read.
- **Saved:** 2026-11-19.
- **Game build:** the source names none. Whether its 2025-02-02 commit matches our build,
  `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018), is unknown, so every
  layout below is `[NEEDS VERIFICATION]` on it. Companion to `metaphor-010-templates-tables-2026-11-17.md`, which
  covers item, skill and affinity tables.

These describe the game's **static data files** (what the game loads), not live memory. They answer "what is sold, for
how much, who are the followers, what does a quest reward"; they do not say where today's stock, the current money or a
follower's current rank sit in memory. Offsets are "computed" from declared types unless stated.

## Shop tables (`ShopBIN.bt`, `ShopTable.bt`)

- Both files start with a 0x10-byte header (eight u16, all unnamed except one at 0x08 called `DisplayList`; the
  template's own name, meaning unknown), then fixed-size records to end of file.
- `ShopBIN.bt`: 0x10 bytes per record: item ID (12 bits) and shop type (4 bits) packed in the first u16, a `Limit`
  u16 (named by the template; its meaning, such as a stock cap, is `[NEEDS VERIFICATION]`), then four u16 with two named
  `Flag1` and `Flag2` and the rest unnamed. `ShopType` enum values in `metaphor_enums.bt`: 0 blank, 3 accessories, 4 use,
  5 key item. Weapons and armour are not in the enum.
- `ShopTable.bt`: 0x14 bytes per record, ten u16, all unnamed (likely the per-shop list header). The shop-to-item
  mapping is therefore `[NEEDS VERIFICATION]`.
- Prices are not here. They are the buy and sell u32 pair in each item table (`ItemConsumable.bt`, `ItemArmor.bt`,
  `ItemWeapon.bt`, see `metaphor-010-templates-tables-2026-11-17.md`).
- Nothing here encodes day-dependent stock; whether the game swaps shop lists by day or story flag is unknown.

## Follower tables (`FollowerTable.bt`, `FollowerRankUp.bt`, `FollowerLayout.bt`)

- Each starts with an 8-byte name, a u32 entry count and 4 skipped bytes (a 0x10 header).
- `FollowerTable.bt`: per follower, one short, a related archetype lineage (short enum) and 13 shorts, all unnamed.
  The follower ID is the row index, via the `FollowerID` enum (short; 15 names counted in the
  enum's first lines; ID 6 is skipped there).
- `FollowerRankUp.bt`: per follower, a follower ID, a u16 rank count, then per rank: four unknown u16, a "Rank ID"
  (template comment: "Probably"), an unlocked archetype, three follower-ability enums, two u16 and 96 unknown bytes.
  Ability enum has an effect-type enum with values unlock archetype, progression, in-battle effect, dungeon crawling and
  daily life. So the rank screen's per-rank ability list likely comes from here. `[NEEDS VERIFICATION]`
- `FollowerLayout.bt`: one opaque 6304-byte record per entry. Meaning unknown (name suggests where a follower stands
  in a map; not confirmed).
- Not in these templates: current rank, friendship points, today's availability or location. Those are save/runtime
  state.

## Quests (`QuestID.bt`, `Reward.bt`)

- `QuestID.bt`: 0xE0 (224) bytes per quest after a 0x10 header: mostly unnamed u16, a u32 `QuestBountyReward`, a
  `RoyalVirtueType` enum plus a u16 `RoyalVirtuesPointReward`, and eight 16-byte message hashes (`MsgHash1`..`8`) at the
  end. Eight hashes suggest the quest's text (name, description, objectives) lives in message files looked up by hash,
  the same mechanism the repository already uses for other messages. Which hash is the name or the objective is
  `[NEEDS VERIFICATION]`.
- `Reward.bt`: 8-byte records: item ID (12 bits), item category (4 bits), unnamed u16.
- No template covers quest state (accepted, in progress, done) or the quest list order.

## Calendar (`SpecialDay.bt`)

- 4 bytes per record after a 0x10 header: month (u8), day (u8), one unnamed u16. A list of dates (the template names
  its struct after another table; its role is `[NEEDS VERIFICATION]`). No template holds the deadline, the day count,
  times of day or weather.

## What this does not give us

No current-value location for money, owned counts, follower rank or availability, or the calendar's current day. For
those, use the script getters listed in `squirrel-script-functions-all-2026-11-16.md` (for example `GetMoney`,
`FollowerGetRank`, `FldFollowerIsAbsence`, `GetCalendarDay`, `GetCalendarTime`, `FldSpecialDayGet`), each marked
`[NEEDS VERIFICATION]` for what its parameters mean.
