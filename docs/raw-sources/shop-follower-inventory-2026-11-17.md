# Shop and follower screens: fields shown (issue #195)

- **Source:** capture evening 1 (#229), session 5: the same run, recording and method as
  `field-inventory-2026-11-17.md` (Debug mod build `219282c`, OBS recording read with ffmpeg 9.0.2 frames and the
  `watch` skill's WhisperX narration, `message-log.txt` counted by record kind). Only labels, class names, counts and
  timestamps are kept; no game text. Video time = log time − 51 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

The maintainer visited a gear shop (31:09–31:58) and a food stall (32:13–32:16) in a town, selling and buying, then
opened the follower screen with D-pad left (32:30–32:48).

## Results

| Screen | Class | `screen` / `cursor` records | Seen | Fields shown | What changes |
|---|---|---|---|---|---|
| Shop menu | `ItemShopMenuChoice` | 14 / 12 | 31:12, 32:15 | Shop name; Buy, Sell, Listen. A Confirm, B Back. | Focused entry. |
| Buy or sell list | `ItemShopChoice` | 203 / 200 | 31:20, 31:40 | The mode (Sell); category tabs as icons (LB/RB), the focused one named; per item icon, name, price, count (Sellable Amount when selling). On focus a description with DEF and EVA. Left: a strip of party rows with each member's DEF and EVA for the focused gear. Money total. X Select, Y Available Archetypes, A Confirm, B Back. | Focused item's description and the party strip. Gear a member wears can't be sold (31:05). Y highlights the archetypes that can use it. |
| Quantity | `ItemShopConfirmChoice` | 2 / 0 | 31:55 | Item, price, count owned (Have); a quantity spinner (up/down) and Total; Buy, Cancel. | Total follows the quantity. |
| Follower card | `CooperationMenuChoice` | 15 / 14 | 32:31 | Name, archetype, rank badge (a number), a description, 3 attribute lines with icons, a Synopsis. LB/RB switches follower. Y Ability Details, B Back. | Switching changes the whole card. |
| Follower abilities | `CooperationSkillMenuChoice` | 31 / 30 | 32:35 | Title Ability; 8 rows, each with a learned/locked icon and a name. | Focused ability's description. |
| Follower event check | `CooperationDetailChoice` | 2 / 0 | 32:33, 32:41 | A tells whether an event with the follower is available today (narration 32:28); here none was, and a line played. | — |

The game compiled `FollowerText_Message.msg` when the follower screen opened (32:30) and
`npc_<map>_SHOP_01.msg` near the gear shop (32:02).

## Not seen

Today's availability and location in a list (the card shows one follower at a time), the rank screen and rank-up
notices. The second shop (a food stall) was opened but not walked.
