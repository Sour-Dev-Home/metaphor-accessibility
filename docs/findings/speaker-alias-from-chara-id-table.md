# CharaID.bin's slots and story flags predict the opening speaker's alias

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** [`game-session-1-2026-11-20.md`](../raw-sources/game-session-1-2026-11-20.md),
  [`name-lookup-candidates-2026-11-16.md`](../raw-sources/name-lookup-candidates-2026-11-16.md)

#74: entry 51 of `CharaID.bin` holds the source name's label and two aliases, each behind a story flag. In the
opening scene the first alias's flag is set and the second's clear, so the table picks `CharaName_FO12_Mor_00`, the
alias the screen shows. The mod can do the same lookup itself: entry, slots, flags, then the label by its MD5.
Whether the dialogue box asks this table is `[NEEDS VERIFICATION]`: the hash listener logged nothing.

## Evidence

- **Layout.** `follow base+0x2eebc18 0x40 0x10` gives entry 0 (`0x5e7c1ad0` in both new games). Entries are `0x6c`
  bytes: a 16-byte default key, then five slots of a 16-byte key and a 16-bit flag number (`0xffff` none), as the
  [static note](title-names-voice-tutorials-static.md#2-the-speaker-name-table-is-charaidbin-behind-a-static-pointer-74)
  read in code. Keys are MD5 of a `CharaName.msg` label; unused keys are MD5 of the empty string. 61 of 100 entries
  are used.
- **Entry 51.** Default key `MD5("CharaName_FO12_Mor")`; slot 0 `MD5("CharaName_FO12_Mor_00")`, flag `0x978`; slot 1
  `MD5("CharaName_FO12_Mor_01")`, flag `0x979`.
- **Flags** (`base+0x2cc934c`, bit `f & 31` of dword `f >> 5`): `0x978` set and `0x979` clear in all four dumps, after
  the scene in the first game and at its end in the second. The first slot with a set flag wins, so slot 0. The
  maintainer saw the alias for the whole scene.
- **Not seen.** `MessageSerialByHashListener` (installed at `0x9e8e60`) logged no record, and no `GetMessageBySerial`
  record falls on the scene's lines. So the box gets its name another way, or through `.text1` code the listener
  doesn't cover. When `0x978` is set and when `0x979` takes over are `[NEEDS VERIFICATION]`.

## Steps

Session 1 (#496), stop 5: the `stop-5-opening-scene.txt` batch, then `read 0x5e7c1ad0 0x1000`, `read 0x5e7c2ad0
0x1000`, `read 0x5e7c3ad0 0xa30`. Offline (Python 3.12): split the dump into `0x6c`-byte entries, compare each key
with `hashlib.md5(label)` for the three labels above, and test the slot flags' bits in the flag dumps.
