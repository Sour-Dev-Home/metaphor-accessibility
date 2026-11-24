# Tutorial pop-ups don't fetch their text by serial; their files compile as each one opens

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** [`game-session-1-2026-11-20.md`](../raw-sources/game-session-1-2026-11-20.md)
- **Functions:**
  - UUIC_Common_TutorialWindow vtable | 0x22576a0 | .udata | Menus | known only | The tutorial window component, a lead for how a page's text reaches the window.

For #118: the pop-ups' text took none of the routes the mod listens to. No `GetMessageBySerial` call asked for a tutorial
serial (10256 to 10859), no line was requested by name, and no text pane held the text. What the log does show is
`System_Tutorial_1.msg` and `_2.msg` compiling together each time a pop-up opened: a usable "pop-up opened" signal, but
not the page's text.

## Evidence

- Four pop-ups, at log times 1985, 2202, 2338 and 2339 s: each starts with both files compiling.
- Between them: 0 tutorial-range `resolve` records (0 of 3579 in the run), no `request`, and `pane` text no longer
  than 25 characters (the panes carry short labels, not the pages).
- #109: the files' text uses `PAD_ICON` (61 times), `WEATHER_ICON` (6) and `SHOP_ICON` (4). On screen (maintainer):
  Menu to skip, A next, B back after the first page; the icons follow the last device used, keyboard or pad.
- How a page's text reaches the window is `[NEEDS VERIFICATION]`. Leads: `UUIC_Common_TutorialWindow@ui@app` (vtable
  `0x22576a0`) and `TutorialWindowOpen`, which jumps into `.text1`
  ([static note](title-names-voice-tutorials-static.md)).

## Steps

Session 1 (#496), stop 7, with `create-msg-from-string`, `ui-text-panes` and `dialogue-window` on. Offline: count
`resolve` serials in range, and `compile`, `pane` and `request` records between the tutorial compiles (grep, awk,
Python 3.12; counts only).
