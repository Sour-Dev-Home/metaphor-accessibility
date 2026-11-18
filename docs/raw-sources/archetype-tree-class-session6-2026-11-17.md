# The Archetype Tree's screen class in capture session 6 (#227)

- **Source:** capture session 6 (capture evening 2, #250): Debug mod from branch `capture-kit-2` at `1ff25fc` through
  Reloaded-II with `opt-in-screen-class.txt`, so `ChoiceCursorGetterListener` logged a `choice` record for each new
  choice and manager pair that calls the plain getter `0xd4ab90` (`ScreenClassListeners.cs`; the lead and the
  candidates sharing that getter are in `docs/raw-sources/capture-2-field-leads-2026-11-17.md` on `capture-kit-2`).
  Read from the session's `message-log.txt` (`choice`, `screen`, `cursor`, pad `input` records, log 325–345 s) with
  grep and awk, against the narration (video 02:43–03:05) and a frame of the tree. Log, narration and recording stay
  local (game text). Video time is log time − 161 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

**The tree's own cursor still logs nothing, now through either listener.** Y on the main menu opened
`AcademiaClassGetCharaSel@ope@ui@app` (one `choice` record at 327.5 s, then 8 cursor moves to 333.2 s, matching the
narrated pick of a character; the frame shows its 7-row list beside the tree). From 333.2 s to 343.3 s the maintainer
moved the cursor around the tree (narrated, pad inputs logged): no `choice`, `cursor` or `screen` record. The next
record is `RootMenuChoice` on going back.

So the tree neither moves a `CursorManager` (session 5's result) nor calls the plain getter on a new choice: the three
`Academia*` choices that share slot 11 (`AcademiaTopChoice`, `AcademiaClassSelectChoice`, and the character select
already seen): none of them logged while on the tree. The tree's class remains `[NEEDS VERIFICATION]`; the layout class
`Tree@tree@academia@layout@ui@app` and `fclAcademiaTree@fcl@app` are the candidates left. Archetype details was not
visited this session (session 5 named it: `archetype-screen-classes-session5-2026-11-17.md`).

How sure: high that neither listener sees the tree (10 s of narrated movement, zero records); its class is not found.

## Steps

1. Print `choice`, `cursor` and `screen` records for log 325–345 s, collapsed on manager.
2. Count non-zero pad `input` records in the same window.
3. Match to the narration and a frame at video 172 s.
