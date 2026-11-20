# The player's chosen name is UTF-32LE at base+0x2ccae60, in the game-data block

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** [`game-session-1-2026-11-20.md`](../raw-sources/game-session-1-2026-11-20.md)

For #106 question 1: after name entry the typed name is at `base+0x2ccae60` (`G + 0x24770`, `G` = `base+0x2ca66f0`),
4 bytes per character, little-endian UTF-32, ended by a zero. The mod can read it there.

## Evidence

- A made-up 8-letter name typed at name entry was found once in the 37 sweep reads, at `base+0x2ccae60`, as UTF-32LE;
  no ASCII or UTF-16LE copy was in the block. The maintainer saw the default name at the same place before naming and
  the typed name again after a second new game.
- It doesn't pass through `CreateMsgFromString` whole: around name entry only 1-byte and empty strings went through,
  and none of the 181 `player-name` records points at `module+0x2ccae60`. They compared nothing (`match=unset`, no
  name file given), so a heap copy can't be ruled out.
- The size of the field, the zero terminator's place and whether the save restores it on load are
  `[NEEDS VERIFICATION]`.

## Steps

Session 1 (#496), stop 4: `pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-1/stop-4-name-entry.txt`
right after confirming the name; offline, search each read's hex for the name encoded as ASCII, UTF-16LE and
UTF-32LE (Python 3.12, `bytes.find`).
