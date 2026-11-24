# A field NPC's kind says which table made it; kinds 2 to 4 have no character

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-actors-2026-11-16.md`](../raw-sources/field-actors-2026-11-16.md), and Steps
- **Functions:**
  - fldNpc::vf5 | 0x67b9e0 | .ecode | Field | known only | Sets the NPC's kind from its creation flags.
  - FldNpcGetNpcID body | 0x67a650 | .ecode | Field | known only | Returns the NpcID index, or `0xffff` for kinds 2, 3 and 4.

Every unit on the people list is an `fldNpc`, so the nearby list can't tell a person from an object that talks, such
as a display case (#687). The kind at `npc+0x3630` says which table made the unit: kinds 0, 1, 5, 6 and 7 come from a
character table and have an NpcID; kinds 2, 3 and 4 come from another table and have none. Which of those are
objects, and whether the model id at `npc+0x3634` tells them apart, is `[NEEDS VERIFICATION]` in a session.

## Evidence

- `fldNpc::vf5` (`0x67b9e0`) sets the kind from its creation flags: 7 with the NpcID index at `+0x365c`; 1 with its
  index at `+0x363c`; 0 with a row at `+0x3638`; 5 at `+0x3650`; 6 at `+0x3658`. Otherwise it takes the row at
  `+0x3648` of the table at `fldDatabase+0x10b40` and sets kind 2, 3 or 4 from that row's first `int`.
- It sets `+0x3634` for every kind: from the character entry for 0, 1, 5, 6 and 7; from the row's `+0x30` for 2 and
  3; at random from a list for 4. Its later switch names the tables of 0, 1, 5 and 6 (`npcTable`, `questNpcTalbe`,
  `followTalbe`, `hourNpcTalbe`, as spelt in the exe) and treats 2, 3 and 4 apart.
- `0x67a650` (`FldNpcGetNpcID`'s body) returns `0xffff` for kinds 2, 3 and 4, so they have no character name.
- A Debug session on 2026-11-22 logged 106 people in a town with kinds 0, 2, 3 and 5, none named; one of them was a
  display case the list called a person. Which unit it was isn't in the log.

So kind alone isn't enough: a passer-by may be kind 2 or 4 as well. The mod logs the kind, the row and the model id
(`I`, Debug builds) and names known objects through the [names table](../contributors/how-to/name-a-nearby-thing.md).
The object id it makes is the kind and the index that kind keeps (`npc-2-17`), and a door's or chest's layout index at
`+0x2888` (`door-4`). Whether those stay the same across loads is `[NEEDS VERIFICATION]`.

## Steps

Ghidra 12.2 on the shared project ([query the Ghidra project](../contributors/how-to/query-the-ghidra-project.md)):

1. `pwsh scripts/dev/ghidra.ps1 offset 0x3630` lists the kind's writers, all in `fldNpc::vf5` (`0x67b9e0`).
2. `pwsh scripts/dev/ghidra.ps1 decompile 0x67b9e0`: the flag tests, the rows and the switch above.
3. `pwsh scripts/dev/ghidra.ps1 decompile 0x67a650`: the kinds with an NpcID.

Session check: press `I` beside the display case and beside a passer-by; compare `npcKind`, `source` and `model`.
