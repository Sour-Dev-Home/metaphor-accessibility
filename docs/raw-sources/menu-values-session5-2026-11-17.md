# Menu value getters in capture session 5: what each returned, and on which screen (#155)

- **Source:** capture session 5 (#229) on the maintainer's machine: Debug mod from branch `capture-1` at `219282c`
  through Reloaded-II, `hooks 31/31`, Steam Input on, with `opt-in-menu-values.txt` and `opt-in-screen-class.txt` in
  place, so the five getter listeners of `menu-value-getters-2026-11-17.md` (on `capture-1`) were installed. Read from
  the session's `message-log.txt` (`value` and `screen` records; formats in `src/Metaphor.Core/MessageLog.cs` on
  `capture-1`) with grep and awk, and checked against three frames of the session's video read locally with ffmpeg 9.0.2.
  The log, video and narration hold game text, so they stay local; only function names, numbers and classes are kept.
  Log time is video time + 51 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Findings

All five getters fired; the money hook caused no reported misbehaviour. Three values are confirmed against the screen.

| Getter | Records | Returned | Maps to what's shown |
|---|---|---|---|
| `GetMoney` | 42 | One `int` per save: 704489 and 291282 on the two first saves, 456225 on the last | **Money total, confirmed:** the shop's money box read 456,225 (video 31:20) while `GetMoney` returned 456225. It followed play: +63 after a fight (234 s), +500 on a sale (1961 s), −350 on a purchase (1969 s). |
| `GetBagItem` | 11228 | A count per item ID | **Item counts, confirmed:** on the Item screen (`ItemMenuChoice`, 574 s) caller `0xb67617` returned the eight counts the list showed (frame at video 08:41), in list order, for IDs `0x4000` + 100, 3, 4, 6, 7, 8, 11, 12. Using one item took ID `0x4000`+3 from 7 to 6 (578 s), then 5 (582 s). |
| `UnitGetParam` | 333 | `(unit, 0…4)` → 15–97 | **St, Ma, En, Ag, Lk by index 0–4, confirmed:** on the Equipment archetype list (`EquipMenuClassChange`, caller `0xf2f576`, 605–607 s) the frame at video 09:14 showed St 40, Ma 91, En 43, Lk 48, and those values are among the leader's unit's results in those 2 s (Ag unreadable on the frame); the five change as the cursor moves between archetypes, so pairing one record with one frame is `[NEEDS VERIFICATION]`. The first number is shown; the bonus beside it was not returned. |
| `UnitAffinity` | 1102 | `(unit, archetype ID, element 0…8, 0)` → 50, 100 or 150 | **Affinity per element:** on the same frame the row showed weak on the 4th icon and resist on the 7th; the log has element 3 → 150 and element 6 → 50 for that archetype ID. So 150 weak, 50 resist, 100 neutral. Called on `EquipMenuClassChange` (606–670 s), `StatusMenuClassDetailChoice` (754–760 s) and `AcademiaClassDetailChoice` (803 s). |
| `GetPlayerUnit` | 874 | Index 1–19 → a unit address | Not a shown value: address = index 1's + (index − 1) × `0x16A8`, the same in both saves. Index 0 never asked. |

- `GetBagItem`'s other callers ask categories 0–3 and 6 (ID `>> 12`) and mostly get 1, 2, 98 or 99; what they count
  is `[NEEDS VERIFICATION]`.
- `GetMoney` callers: `0xb756d3` and `0xb6d660` with the main menu open, `0x2660df` after fights, a dozen others in
  the shop.
- Affinity kinds other than weak, resist and neutral (null, repel, drain) weren't seen `[NEEDS VERIFICATION]`.

## Still missing

No hooked getter returned these; each is `[NEEDS VERIFICATION]`:

- **Party and Status:** HP and MP, current and max (no `value` record holds the Item screen's party HP or MP numbers),
  level, EXP to next, conditions, Front/Back, tactics setting, party membership.
- **Skill:** cost, its kind, whether usable.
- **Item:** whether usable on the chosen target.
- **Equipment:** equipped item per slot (`CharaGetEquip`, not hooked), the focused item's stat change, who wears it.
- **Archetype details:** rank and EXP to next rank; the stat bonuses.
- **Mag** total.

The `.text1` natives in `menu-value-getters-2026-11-17.md` (`UnitGetLevel`, `UnitGetMaxHP`, `GetClassLevel`, `SetMag`
and the rest) are the next candidates.

## Steps

1. Count `value` records by `fn` and by `caller`; list the distinct `args`/`result` pairs per getter and window.
2. For `GetBagItem` during the Item screen (571–600 s), keep category 4 (`args >> 12 == 4`) and compare with the list.
3. Extract frames at video 08:41 (Item list), 09:14 (Equipment archetype list) and 31:20 (shop) with ffmpeg and
   compare the numbers.
4. Search every `value` record for the party's HP and MP numbers on the Item screen: 0 matches.
