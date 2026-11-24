# Each map's layout files name its people and examine points, so the nearby list could read names from game data

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's own data archives (CPK) offline with our read-only tool (`tools/Metaphor.CpkExtract`),
  output kept in git-ignored `extracted/`; the live check is our own Reloaded-II mod's message log
- **Sources:** none, see Steps; builds on [`field-nearby-unit-names-static.md`](field-nearby-unit-names-static.md),
  [`field-npc-kinds-objects-static.md`](field-npc-kinds-objects-static.md) and
  [`field-units-names-exits-static.md`](field-units-names-exits-static.md)

For #687. The names the contributors' table (`data/names/<map>.json`, `src/Metaphor.Features.Field/NamesTable.cs`)
fills by hand are mostly in the game's own files. People: `COMMON/field/source/layout/npc/f_<a>_<b>.npc` has one row
per placed character, a layout name and a position. The name maps to an `NpcName.msg` label by string. Examine
points and exits: `collision/f_<a>_<b>.hittbl` rows hold the MD5 of an `AccessPointName.msg` or `MapName.msg` label.
Doors: `door/f_<a>_<b>.doortbl` holds `AccessPointName_Door_*` hashes. **So yes, the mod could read most names at run
time, as it reads the navmesh.** It would load the map's files from `base.cpk` and match a nearby unit to a row by
position. Three logged people on map 1 sit exactly on a `.npc` row. The table should stay as the override, and as the
only source for things the files don't name (crates, display props: `npcKind` 2 to 4).

## Evidence

Map 1 (Sunlumeo Street, `f_0001_001`), English files:

| File | Header | Rows | What a row holds |
|---|---|---|---|
| `layout/npc/f_0001_001.npc` (18,096 bytes) | `NPC\0`, count 113 at `+0xc` | `0xa0` bytes from `+0x10`, exactly 113 | `+0x4` name, 64 bytes (`npc_0001_001_KING_01_01`); `+0x44` position, three `float`s; then a yaw in degrees and a scale of 1, 1, 1 `[NEEDS VERIFICATION]` |
| `layout/npc/f_0001_001.npctbl` (52,940) | `NPCTBL`, count `0x83` | not decoded | the same names, with `talk01`/`talk02` and flag-like `u16`s (`0x1184`, `0x117b`) `[NEEDS VERIFICATION]` |
| `layout/collision/f_0001_001.hittbl` (8,336) | `HITTBL`, count `0x41` (65) | `0x80` bytes from `+0x10` | `+0x0` name (`collision_action_001_00`), `+0x20` second name (`Col_Action01`), `+0x40` label MD5, `+0x50` a second MD5 (empty string's when unused) |
| `layout/collision/f_0001_001.hit` (9,936) | none read | the same `collision_*` names | shapes for those names `[NEEDS VERIFICATION]` |
| `layout/collision/f_0001_001.ghttbl` (15,724) | `GAYATBL` | not decoded | crowd-chatter volumes (`collision_gaya_000` → `gaya_0001_001_KING_01`) |
| `layout/door/f_0001_001.doortbl` (3,600) | `DORTBL` | not decoded | `KING01_INN02` and others; 3 `AccessPointName_Door_Town01_*` hashes |

- **People.** 57 of the 113 `.npc` rows are named `npc_<rest>` where `CharaName_NPC_<rest>` is a label of
  `NpcName.msg` (935 labels). Others are `mob_*` crowd rows, named story characters (`npc_bergitta_02`) and some with no
  label (`npc_0001_001_KING_32_02`).
- **Position match.** In the maintainer's sessions' `field nearby` records on map 1, three `npcKind=0` people sit on a
  `.npc` row within 0.1 units: `(-5784,25,8500.3)` on `npc_0001_001_KING_07_01` ("Country Noble"),
  `(-5876.6,26,8474)` on `KING_07_02` ("Country Lady"), and `(-1546.1,24,8793.1)` on `KING_32_02` (no label). The
  `npcKind` 2, 3 and 4 units logged nearby are 270 units or more from any row, which fits their being objects
  ([kinds note](field-npc-kinds-objects-static.md)). A person who walks a route would drift off its row, so the match
  needs a radius or the unit's own row index `[NEEDS VERIFICATION]`.
- **Examine points.** The 65 `hittbl` rows hold 40 label hashes: 37 `AccessPointName.msg` labels (`_Action_Town01_01`
  "Glass Window", `_Action_Town01_02` "Shop Igniter", the 2026-11-22 display case's prompt in #687) and 3 `MapName.msg`
  labels (exits to Sunshade Row and the Cathedral). Tying a row to a nearby unit needs the shape's position from `.hit`,
  not decoded `[NEEDS VERIFICATION]`.
- Other maps have the same files: the archive lists 153 `.npc`, 209 `.hittbl` and 151 `.doortbl` files under
  `COMMON/field/source/layout/`. Whether every chapter 1 map's rows match as well is `[NEEDS VERIFICATION]`.

Whether reading game names at run time fits the maintainer's 2026-11-22 decision ("only names contributors write,
never game text") is the maintainer's call. That decision is about the committed table. Run-time reads commit nothing,
as `GameNames` already does for speakers.

## Steps

.NET SDK 9.0.319, Python 3.12.10, 2026-11-24; game found by `Find-MetaphorGame`, SHA-256 checked.

1. `dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- extract base.cpk
   COMMON/field/source/layout/npc/f_0001_001.npc COMMON/field/source/layout/npc/f_0001_001.npctbl
   COMMON/field/source/layout/collision/f_0001_001.hittbl COMMON/field/source/layout/collision/f_0001_001.hit
   COMMON/field/source/layout/collision/f_0001_001.ghttbl COMMON/field/source/layout/door/f_0001_001.doortbl
   --out extracted/audit-ch1`, with `EN/message/` extracted as in
   [the silent-text audit](chapter1-silent-text-audit-logged.md).
2. A throwaway Python script outside the repository searched each file for the MD5 of every English label and for
   ASCII names, decoded the `.npc` rows, and found the nearest row to each logged `field nearby kind=Person` position.
