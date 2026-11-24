# Message Speed's values 0, 1 and 2 are Normal, Fast and Display All, and Skip Selections' 0 and 1 are Off and On

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory (the by-ear session's message log), plus the
  maintainer's confirmation of what the screen shows
- **Sources:** [`settings-value-names-confirmation-2026-11-22.md`](../raw-sources/settings-value-names-confirmation-2026-11-22.md),
  [`settings-values-sessions-2026-11-21.md`](../raw-sources/settings-values-sessions-2026-11-21.md)
- **Functions:** none

On the System page, Message Speed (value ID 17) has values 0, 1, 2 for Normal, Fast and Display All, named by the
messages `System_Option_82`, `System_Option_83` and `System_Option_84`. Skip Selections (value ID 18) has 0 for Off and 1
for On; no message for those two has been found, so the mod keeps the words itself (`SettingsPageRows`).

## Evidence

The three messages resolve together at every settings page build, in order, and the maintainer confirmed the order
against the screen (#734). The match of each message to its value was not tied to a logged value change; the maintainer's
confirmation settles it. Skip Selections' words are the maintainer's alone. Whether the game spells On and Off the same
way on screen is `[NEEDS VERIFICATION]`.

## Steps

Open Settings > System from the title, move to Message Speed and Skip Selections, change each, and compare what is
heard with the screen.

## Other System rows (#770)

Session c (same build) logged 96 `silent change=ValueChanged` lines on System rows 10 to 16 and 20 of 24
(`docs/raw-sources/settings-values-sessions-2026-11-21.md` has the pinned IDs; the log carries no values). Only IDs 5,
17, 18, 27 and 28 are pinned, so for every other row the setting ID, which row is Camera Speed, and which of
`System_select_name_PC_01`-`_19` or `System_Option_45`/`_46` name its values are all `[NEEDS VERIFICATION]`
([inventory](settings-screens-inventory-static.md)). Until pinned, the mod learns a row's ID at run time: the
one unpinned setting whose dword moved when the row changed. The row then says that number on change and on focus.
Two or none moving say nothing. Check by ear: change Camera Speed, Skip Special Animations, Dash Effects and Camera
Shake; each should say a number after 200 ms.
