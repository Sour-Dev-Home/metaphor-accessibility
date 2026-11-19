# Arcana (Persona 3 Reload): calendar, shop and quest screens (design only)

- **Source:** `ChipsAhoiMcCoy/arcana` (Zlib) at `0649b9bfb7361b94132fa5b0c361d21c4d431b20`, read through
  `gh api repos/ChipsAhoiMcCoy/arcana/contents/<file>`: `docs/menu-coverage.md`, `src/calendar.h`,
  `src/core/calendar_text.cpp` (head), and the leading comments of `src/screens/shop.cpp` and
  `src/screens/quest.cpp`. Overview and credit details: `arcana-v0.1.0-alpha.3-2026-11-16.md` and
  `arcana-research-credits-and-pace-2026-11-16.md`. No code copied.
- **Saved:** 2026-11-19.
- **Game build:** none for us. Arcana targets Persona 3 Reload (Unreal Engine); its offsets and class names are for that
  game's build and do not apply to Metaphor: ReFantazio, which runs on Atlus's own engine. Only the screen design and
  data-source approach transfer.

## What exists (from `docs/menu-coverage.md`)

The coverage table lists, all marked "live": the Social Link list and page, the Elizabeth's-requests and
missing-persons quest screens, a calendar (day grid, plans), shops (weapon, item, sell, quantity, prompts) and an
antique shop. So P3R's calendar, quest list, shops and social screens each already have a screen-reader solution.

## Approaches worth reusing (ideas, not offsets)

- **Date from the game's own calendar function library**, not from screen text: `calendar.h` says "The game's date
  (UCldBpFuncLib)" and returns month, day, weekday, time zone, holiday flag, moon age and a total day count; a
  `tick()` announces a new day or a new time of day when the value changes. Spoken form in the source comment:
  "April 6, Monday, Late Night". This is the pattern #192 describes. Metaphor has similarly named script getters
  (`GetCalendarDay`, `GetCalendarTime`, `GetCalendarDow`; MF.Toolkit `MetaphorDefinitions.cs` at
  `b1f876da5b4c1f42b5eef07fc6c2174030beb77b`, AGPL-3.0, signatures only), but their return values are
  `[NEEDS VERIFICATION]`. Whether Metaphor exposes a "days to deadline" value is unknown.
- **Shops as one state machine**: the shop's comments list a state field (top menu, buy list, quantity, confirm, sell
  list), a cursor and top-row field per list, list rows of price + item ID + "how many you can take" + (for simple
  shops) stock left, and quantity and yes/no cursors. Same idea as P4G Access; stock-left per row exists in P3R's
  simple shops, which is a hint that a shop row can carry its own stock.
- **Quest list**: a generic list-cursor helper, index = row + top row, with a state field separating list and detail.
- **Wording rule from the calendar source**: "the player's order" for the date was set by the author after testing,
  so wording is a user decision, as #193 says.

## Licence and credit

Zlib permits reuse with attribution notice; we still copy nothing. If an idea is adopted, credit by name,
repository, licence, commit and file (ground rule 7).
