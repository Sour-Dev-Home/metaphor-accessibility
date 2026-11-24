# Find crash details

When the game closes on its own, Windows usually records why. This page finds that record with two tools every
Windows 10 and 11 computer has, Reliability Monitor and Event Viewer, without admin rights, and copies it into your
report. Do this as well as attaching `report-log.txt` ([Report a problem](install-the-mod.md#report-a-problem)): the
mod's log stops where the game stopped, and the Windows record says which part of the game or the mod failed.

Each step names the keys to press, with no mouse. If the game has already started again, the log of the run that
closed is `report-log.previous.txt`; otherwise copy `report-log.txt` first, as the mod writes it fresh each game start.

## With Reliability Monitor

Reliability Monitor lists each program that stopped working, by day.

1. Press `Windows+R`, type `perfmon /rel`, and press `Enter`. Reliability Monitor opens on a chart of the last few
   weeks.
2. Press `Tab` until you reach the link "View all problem reports", and press `Enter`. A list opens, grouped by
   program, newest first.
3. Find the game, `METAPHOR.exe`, with the arrow keys. Its entry says "Stopped working" and the date and time.
4. Press `Enter` on that entry, or choose "View technical details". The details name the faulting module: the file in
   which the crash happened, such as a game file or one of the mod's files.
5. Select the details with `Control+A`, copy them with `Control+C`, and paste them into the feedback form's first
   question with `Control+V`, after what you were doing.

## With Event Viewer

Event Viewer has the same record, and sometimes one that Reliability Monitor leaves out.

1. Press `Windows+R`, type `eventvwr.msc`, and press `Enter`.
2. In the tree on the left, open "Windows Logs" with the `Right arrow` key, then choose "Application" with the
   `Down arrow` key. The middle pane lists the newest events first.
3. To show only crashes, press `Alt`, open the "Action" menu, and choose "Filter Current Log". In "Event sources",
   type `Application Error`, or tick it in the list, and press `Enter` on OK.
4. Press `Tab` to move to the list of events. Move to the newest one whose time matches the crash. Its details start
   with "Faulting application name: METAPHOR.exe".
5. Copy it: press `Alt`, open the "Action" menu, choose "Copy", then "Copy Details as Text". Paste it into the feedback
   form with `Control+V`.
6. To show every event again, open the "Action" menu and choose "Clear Filter".

## What the details hold

The details name the program, the faulting module, an error code and an offset, which is how far into that module the
crash happened. They hold no game text and no files. They do name the folder the game and the mod are in, which can
include your user name. Read the text before you send it, and replace your user name with any word if you want.

## Not checked by ear yet

Nobody has done these steps with a screen reader yet, so what NVDA reads may differ from what this page says. Tell us
in an issue if it does.

- `[NEEDS VERIFICATION]` What NVDA reads on Reliability Monitor's chart, whether `Tab` reaches "View all problem reports"
  and the list's entries, and the exact name of the "View technical details" link.
- `[NEEDS VERIFICATION]` What NVDA reads in Event Viewer's tree, its event list and the filter window, and whether
  "Copy Details as Text" is under "Copy" in the "Action" menu on every Windows version.
- `[NEEDS VERIFICATION]` That a crash of the game is listed as `METAPHOR.exe` in both tools.
