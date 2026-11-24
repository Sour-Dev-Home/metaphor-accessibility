# Each main mission row names its title by a label's MD5, beside the flag that starts it

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; follows [`mission-box-text-source-static.md`](mission-box-text-source-static.md)
- **Functions:**
  - MissionTableRow | 0x3c5c80 | .ecode | Field | read | The row search: matches the story flag against the mission rows' `+0x20`.
  - Mission::vf1 | 0x3c64e0 | .ecode | Field | known only | Builds the mission rows once the four files are read.

The main mission table the mission box reads (#658) is built from `Mission_Main_Data.bin` into rows of `0x30` bytes. A
row's first 16 bytes are copied from the file and its next 16 are the MD5 of no bytes, the empty label. The message
window and the name tables find a message by its label's MD5
([`msg-window-show-line-2026-11-20.md`](../raw-sources/msg-window-show-line-2026-11-20.md)), so the first 16 bytes are
most likely the MD5 of the title's label, a `MissionText_MainMission_NN` message [NEEDS VERIFICATION]. The mod reads
the row of the highest set flag and looks its hash up in the messages the game compiled.

## Evidence

- **The table.** The row search `0x3c5c80` (signature `MissionTableRow`) loads `M = [0x2dff848]` at `+0x19`. `M+0x60`
  is a `fw::StaticArray<std::vector<app::dat::Mission::Data>, 4>`: the exe's RTTI type descriptor at `0x2bf9670`, its
  object locator `0x23a79f8` and its vtable `0x17e7380`, whose slot 2 (`0x3c7450`) is `lea rax,[rcx+0x10]; ret`. So
  the vectors start at `M+0x70`, `0x18` bytes each (`{begin, end, capacity}`); the first is the main missions, whose
  `begin` and `end` are at `M+0x70` and `M+0x78`. The searches index them only while the index is below the dword at
  `M+0x68`; 4 vectors of `0x18` from `M+0x70` end at `M+0xd0`, the byte `Mission::vf1` tests before it builds them.
- **The rows.** `0x3c6600`, called by `Mission::vf1` (`0x3c64e0`) once the four files are read, builds each
  category's rows. For the first file, `COMMON/mission/source/bin/Mission_Main_Data.bin` (`Mission::vf0`,
  `0x3c6100`), it takes the count at file `+0x8` and entries of `0x14` bytes from `+0x10`. Each row gets the entry's
  bytes `0x0`–`0xf` at `+0x00`, the 16 bytes at `0x17e7168` at `+0x10`, the entry's 16-bit value at `+0x10` as a dword
  at `+0x20`, and `0xffff` in the three dwords after it. `0x3c5c80` matches the flag against `+0x20`.
- **The empty hash.** The 16 bytes at `0x17e7168` are `D4 1D 8C D9 8F 00 B2 04 E9 80 09 98 EC F8 42 7E`, the MD5 of
  no bytes. The other files' rows copy two 16-byte fields from their entries instead, so each field is one label's
  hash, and the main table has one.
- **The title.** `0x723e50`, the main mission's producer, passes the row index to thunks into `.text1` that build the
  string, so the label isn't seen there [NEEDS VERIFICATION]: that `+0x00` is the title, not a description.
- Not checked in memory: any of it. The Debug build writes `mission table <state>` on each change in what a read found
  (`count=… rows=…`, or which check failed) and `mission flag=0x… label=… changed` for each new top row.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[how-to](../contributors/how-to/query-the-ghidra-project.md)), on the exe of the build above:

1. `global 0x2dff848`; `decompile 0x3c5c80`, `0x3c5ef0`, `0x3c5f50`, `0x3c6100`, `0x3c64e0`, `0x3c6600`, `0x723e50`.
2. `grep -aoE "StaticArray@[A-Za-z_0-9@?\$]{0,120}"` on the exe for the array's type name. Its type descriptor is the
   name's file offset, as an RVA, minus 16. The object locator is the dword pattern `1, …, …, <descriptor RVA>, …,
   <its own RVA>`, and the vtable is 8 bytes after the qword that points to the locator (read with a short script over
   the file and its section table). `disasm 0x3c7450` for slot 2.
3. `tools/Metaphor.SignatureCheck --bytes <exe> 0x17e7160 0x20` for the empty hash, and `--bytes <exe> 0x3c5c80 0x40`
   for the signature; `bash scripts/check.sh signatures` finds it once.

## What the next session must check

Press L in the field with the Debug build. The log should show `mission table count=4 rows=N` and the L record's
mission; a `mission flag=… label=(unknown) hash=…` record means the hash names no compiled message. Then cross a story
step and hear `Mission: <title>` against the box on screen.
