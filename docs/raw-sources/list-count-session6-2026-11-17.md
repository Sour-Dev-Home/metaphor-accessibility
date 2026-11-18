# A cursor list's item count in capture session 6 (#249)

- **Source:** capture session 6 (capture evening 2, #250): Debug mod from branch `capture-kit-2` at `1ff25fc` through
  Reloaded-II with `opt-in-list-count.txt` and `opt-in-screen-class.txt`; the `list-count` record
  (`ListCountProbe.cs`: `count` is `CursorManager+0x58`, `items` the length of the vector at `+0x88`..`+0x90`, `flag8`
  and `flag9` the items with byte `+8` or `+9` set), defined in `docs/raw-sources/capture-2-field-leads-2026-11-17.md`
  on `capture-kit-2`. Read from the session's `message-log.txt` (75 `list-count` records, each after a `cursor` record)
  with grep and awk; rows counted by eye on frames of the OBS recording (ffmpeg 9.0.2). Log and recording stay local
  (game text). Video time is log time − 161 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

**`CursorManager+0x58` is the list's item count.** It matched the rows on screen on six screens of four lengths (2, 7, 8, 9):

| Screen class (`@ope@ui@app`) | `count` | Rows on screen | `items` |
|---|---|---|---|
| `YesNoChoiceSample` | 2 | not framed | 0 |
| `CommonTalkChoice` | 2 | 2 | 0 |
| `AcademiaClassGetCharaSel` | 7 | 7 | 19 |
| `SkillCharaSelChoice` | 8 | 8 | 19 |
| `EquipCharaSelChoice` | 8 | 8 | 19 |
| `RootMenuChoice` | 9 | 9 | 0 |
| `SkillSelectChoice` | 9 | 9 (no scrolling seen) | 22 |
| `ItemMenuChoice` | 145 | scrolls; not counted | 0 |

Other counts logged: `TitleChoice` 3 (`items` 3), the config top 5, `SystemMenuChoice` 6, a title sub-list 17, battle
lists 2 to 16. The count held for a screen's whole life; three manager addresses were reused by a later screen and then
read that screen's count (17 then 2, 2 then 7, 2 then 6), so read it per cursor event, not once per address.

**The vector at `+0x88` is not the list:** its length was 0, 19, 22 or 999 regardless of the rows, so `items`,
`flag8` and `flag9` can't stand in for the count. The per-item flags' meaning stays `[NEEDS VERIFICATION]`.

How sure: high for lists that fit on screen (6 screens agree, 0 disagree). A scrolling list's total (the 145-item
inventory) was not counted by eye `[NEEDS VERIFICATION]`; nor was a list whose length changes while open.

## Steps

1. Print each `list-count` record with the `screen` owner class of the same manager.
2. Extract frames of each screen (video 128–172 s, 98 s, and the title), count the rows by eye.
