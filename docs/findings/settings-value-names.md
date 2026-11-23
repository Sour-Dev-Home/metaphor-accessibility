# Message Speed's values 0, 1 and 2 are Normal, Fast and Display All, and Skip Selections' 0 and 1 are Off and On

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory (the by-ear session's message log), plus the
  maintainer's confirmation of what the screen shows
- **Sources:** [`settings-value-names-confirmation-2026-11-22.md`](../raw-sources/settings-value-names-confirmation-2026-11-22.md),
  [`settings-values-sessions-2026-11-21.md`](../raw-sources/settings-values-sessions-2026-11-21.md)

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
