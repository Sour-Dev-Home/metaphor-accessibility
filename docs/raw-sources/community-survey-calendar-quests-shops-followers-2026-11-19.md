# Community survey for issues #192-#200: calendar, quests, shops, followers

- **Source:** web searches and `gh` searches on 2026-11-19 (queries: Metaphor cheat table, Metaphor modding tables,
  Metaphor shop, follower and calendar repositories, Persona 3 Reload and Persona 5 accessibility), plus the notes
  linked below. Search results are leads only; pages marked "not opened" were not read. Nothing downloaded; no game
  file, executable or cheat table touched.
- **Saved:** 2026-11-19.
- **Game build:** ours is `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).
  No community source names a build; all are older (2024-10 to 2025-06), so each is `[NEEDS VERIFICATION]` on ours.

## What the community has

| Source | Covers | Licence | Saved note |
|---|---|---|---|
| `DeathChaos25/metaphor-010-templates` @ `cfccaa2` | Static data tables: shop lists, followers and rank-ups, quests, special days | none (field names only) | `metaphor-010-templates-shop-follower-quest-2026-11-19.md` |
| Cheat tables (schutzdev "METAPHOR v9" on GamePressure and FearlessRevolution; VGTimes tables by several authors; a Playground.ru table updated 2025-06-03) | Feature names only: Money, Mag, Item Count Editor, Follower Link Level edit | unknown; download sites | `menu-values-community-leads-2026-11-17.md` (not re-read) |
| `RyoTune/MF.Toolkit` @ `b1f876d` | Script-function signatures (calendar, money, bag, follower) | AGPL-3.0 | `menu-values-community-leads-2026-11-17.md` |
| Persona 4 Golden Access | Shop state machine, Social Link rank-up overlay | GPL-3.0 | `p4g-access-shop-and-social-link-docs-2026-11-19.md` |
| Arcana (Persona 3 Reload) | Calendar, shops, quest list, social screens, all live | Zlib | `arcana-p3r-calendar-shop-quest-screens-2026-11-19.md` |

Not found: any Metaphor accessibility mod, any Metaphor save editor, any live struct or offset write-up for shop stock,
money, follower state or the calendar, and any Persona 5 or P3R screen-reader mod besides Arcana. GitHub repositories
named "Metaphor-ReFantazio-Cheat" (for example by `popmie24`, `Bonganitt`, `DarkPhong894`) have keyword-stuffed
descriptions and look like malware lures; not opened.

One unverified claim from a search snippet: the Playground.ru table's author says its script "was copied from a Persona 3
Reload cheat table" (search result text; page not opened). If true, Metaphor and P3R share some money or item structures.
`[NEEDS VERIFICATION]`

## Map to the issues

- **#196 (spike, where the data lives).** Static half is mapped: the shop item list (ID, type, a `Limit` field, two flag
  fields), the follower tables, prices (in each item table), `QuestID` rewards. Live half is not: no source gives the
  address of today's stock, current money, owned counts, follower rank, availability or location. Our script getters
  (`GetMoney`, `GetBagItem`, `FollowerGetRank`, `FldFollowerIsAbsence`, `FclShop*`; addresses in
  `squirrel-script-functions-all-2026-11-16.md`) are the best leads. The cheat tables prove money, item counts and
  follower link level sit at findable addresses, but offsets were not read. Method to copy from P4G Access and Arcana:
  find the shop's state and cursor fields live, by diffing between screens.
- **#195 (inventory).** Our own capture already names the classes (`ItemShopChoice` and others). Gap it left (rank
  screen, rank-up notices, availability list) is not answered by any community source.
- **#197 (shops).** Speech shape: P4G and P3R both speak name, price and money, and a quantity prompt with a running
  total. Prices are in the item templates; stock-left per row may exist (a P3R simple shop has it; Metaphor unknown).
- **#198 (follower list).** Follower identity and the per-rank abilities come from `FollowerTable` and
  `FollowerRankUp`. Availability and location: `FldFollowerIsAbsence` and `FldFollowerGetJumpToZoneID` exist as script
  functions; meaning `[NEEDS VERIFICATION]`. Nothing on the list screen itself.
- **#199 (rank screen and notices).** Rank-up rewards are in `FollowerRankUp`. P4G's rank-up banner needed a task-struct
  read because it is baked art; Metaphor's notice may too. `ElectionRankUp_Follower` and
  `GetFollowerRank_OnRankUpScript` are the script hooks for the notice, unverified.
- **#192 and #193 (calendar, day and time).** P3R's pattern (read the game's calendar function library, announce on
  value change) fits. Script getters `GetCalendarDay`, `GetCalendarTime`, `GetCalendarDow`, `GetDay`, `FldSpecialDayGet`
  exist; return encodings, the deadline and the objective text source are unknown. `SpecialDay.bt` is a month/day list
  only. No weather mod is needed unless a game rule makes it matter.
- **#194 (quest list).** `QuestID.bt` gives the record layout (224 bytes, reward fields, eight message hashes for text).
  Quest state, list order and which hash is the name are unknown.
- **#200 (followers available).** Depends on #198's availability source; no community help.

## Still unknown (all `[NEEDS VERIFICATION]`)

Live addresses for every value above; whether templates match our build; return values of the calendar getters; where
the deadline countdown and current objective come from; whether shops have day-dependent stock; what the `Limit`,
`Flag1` and `Flag2` shop fields mean; how the rank-up notice renders.
