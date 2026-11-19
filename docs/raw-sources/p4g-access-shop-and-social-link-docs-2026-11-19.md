# Persona 4 Golden Access: how it reads shops, calendar and Social Links (design only)

- **Source:** `AquaRose7/Persona-4-Golden-Access` (GPL-3.0) at `dcff25536710cc777c079a02422a5dc30bcb1e87`
  (v2.2.6), files `database/SHOP_SYSTEM.md`, `database/SOCIAL_LINK_READERS.md` and `database/weather_calendar.json`
  (size only), read through `gh api repos/AquaRose7/Persona-4-Golden-Access/contents/<file>`. The project overview is
  in `persona-4-golden-access-2026-11-16.md`. No code copied; short facts quoted.
- **Saved:** 2026-11-19.
- **Game build:** none for us. Every address and offset below is Persona 4 Golden (a different game and engine) and
  does not apply to Metaphor: ReFantazio. Only the approach transfers.

## Shops (`SHOP_SYSTEM.md`)

- The doc calls the shop reader "fully state-machine driven": it hooks the shop's update function and reads a
  **state field** (which screen is up: top menu, item list, description, quantity, confirm, done, sell) and a **cursor
  field** from the shop struct, polled on a 50 ms thread, because "the game stops calling [the hook] when a sub-screen
  has focus". (Paraphrased from "Architecture of the reader".)
- List entries hold a price and a packed item ID in a fixed stride; the wallet is a global; the shop's item
  descriptions come from the game's help message file by item ID.
- Speech shape: browse = "name, price, You have N yen"; description window = name plus description plus price,
  read on open; quantity window = "How many? 1. 850 yen each", then a running total per change.
- Lesson recorded by the author: the doc says the system "burned months on misread fields", with a section of dead
  ends. For us: find the shop state field live and diff it, don't trust one capture.

## Social Link rank-up (`SOCIAL_LINK_READERS.md`)

- Rank-up banner and establish/max poem are baked art with "no dialogue-text path", so the mod reads a **task struct**
  (commu index, rank) and a **commu data table** for the arcana. Names are looked up only from a verified table:
  "NEVER guess a name — a wrong name is worse than none".
- Relevance: Metaphor's rank-up notice (#199) may be the same kind of baked or special-render overlay. Unverified.

## Calendar and weather

- `database/weather_calendar.json` is a table (about 1.4 KB) of weather codes indexed by days since 2011-04-01, which
  the author says was found at an offset in the game's `init.bin`. Persona 4 specific; not read in full.

## Licence and credit

GPL-3.0, compatible with ours. Anything adopted later needs a credit in a code comment and in `CREDITS.md`
(ground rule 7); this note records ideas only.
