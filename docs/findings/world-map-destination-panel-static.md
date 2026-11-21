# The travel map's destination panel is filled by one `.ecode` call per selection; its text isn't fetched by serial

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-notices-and-map-travel-static.md`](field-notices-and-map-travel-static.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`menu-hook-counts-2026-11-17.md`](../raw-sources/menu-hook-counts-2026-11-17.md),
  [`field-map-region-area-name-static.md`](field-map-region-area-name-static.md)

Static prep for #545, after the 2026-11-20 by-ear run found no cursor records and only five fixed serial lookups on
the world map. The exe has two map screens with a destination panel. The sortie map (the travel map the `SortieMapUI_*`
script functions open) has a panel class, `sortie::map::Detail`, that binds a name pane, a detail pane and three quest
rows. The world map (`UUIC_Common_WorldMap`) has a detail layout of follower lines and a city picture, with no name
pane. Which one the run showed is `[NEEDS VERIFICATION]`; the sortie map fits "name and info lines". On it, each
selection change calls `0xcd05f0`, which looks the destination up and calls the panel's `0xcccf50` with its name,
detail and quest texts: one `.ecode` hook candidate, matching once. Tonight's serials are in no place-name file, so
the text isn't fetched through `GetMessageBySerial`. The `ui-text-panes` listener, which saw every menu screen's text
in an earlier session, may already read the panel with no new hook.

| Piece | Sortie map | World map |
|---|---|---|
| UI component | `UUIC_Common_SortieTownMap@ui@app`, vtable `0x2250628` | `UUIC_Common_WorldMap@ui@app`, vtable `0x2257cf8` |
| Layout | `SortieTownMapLayout@ui@app`, `0x21b1958`; parts under `sortie::map`: `Main` `0x21b1808`, `Map` `0x21b1110`, `Detail` `0x21b1260`, `Icon`, `Path`, `Player` | `WorldMapLayout@ui@app`, `0x21b47e8`; `WorldMapLayout::DetailLayout` `0x21b44c8`, `worldmap::Focus` `0x21b3e00`, `worldmap::Icon` |
| Input (fcl) | `fclSortieTownMap@sortie@fcl@app`, `0x17f5b58` | `fclWorldMap@worldmap@fcl@app`, `0x17f6420` |
| Data | `SortieTownMap@dat@app`, `SortieMapSaveData@sortie@ui@app` | `datWorldMap@dat@app`, `0x17e89a8` |
| Panel panes | `font_rect_C5_name`, `font_rect_C3_detail` (and `_range`), `font_rect_C4_quest` × 3 | `font_rect_C2_follower[LIST]`, `parts_info_follower_01`–`04`, `parts_info_cityimg` |

## Evidence

### The sortie panel binds a name pane and a detail pane

Vtables come from the RTTI type descriptors (complete object locator, then the pointer to it); all their slots named
here are `.ecode`. `sortie::map::Detail::vf17` (`0xcca910`) is the panel's setup: it binds `font_rect_C5_name` to the
first text holder of the array at `Detail+0x350` and `font_rect_C3_detail` to the second (`+0x70`), both through
`0xa74ee0`, then the three `parts_rootmap_detail_quest_0n` rows and `font_rect_C4_quest` to holders at `+0xe0` on.
`Detail::vf20` (`0xcc7f10`) plays the panel's animations and clears `Detail+0xc08`. `SortieTownMapLayout::vf20` and
`vf24` (`0xcc8060`, `0xccc440`) only forward to the object at `+0x30`.

### Showing a destination fills those holders

`0xcccf50 (Detail* this, u32 kind, u8 index, text* name, text* detail, list* quests)` is the panel's "show this
point". It plays the panel's animations, copies the `0x1c`-byte `name` into the first holder at `+0x350` and `detail`
into the second (`+0x70`), fills the three quest rows from `quests`, and, on one branch of its third argument,
stores a value made from `kind` (`0xa7ebd0`) at `+0xc08` and passes it to `0xedb3d0` with the global `0x2f04ab8` (purpose unknown).

Its only analysed caller, `0xcd05f0 (owner, u32 kind, s8 index)`, is the selection change: it stores `kind` and
`index` at `owner+0x45fefc` and `+0x45ff00`; index −1 shows nothing. For `kind` under 5 the entry comes from a list
at `owner+0x43d250` (`0xcda450`); otherwise `index` is looked up in a table of `0xd0`-byte entries at
`owner+0x45a090`. It then calls `0xcccf50` on `owner+0x43ab70` with the entry's `+0xac` (name), `+0x58` (detail) and
`+0x38` (quests). Its callers are two calls in `0xcd4670`, not read, the likely input step. So the selected destination
is the pair at `owner+0x45fefc`, and the name and detail are already text when they reach the panel; what form the
`0x1c`-byte text takes (a serial with arguments, or a string) is `[NEEDS VERIFICATION]`. The owner's class wasn't
named; its size (over `0x45ff08` bytes) fits the sortie layout's whole tree.

`0xcccf50` starts in `.ecode` and matches once with `tools/Metaphor.SignatureCheck --probes`:

| Function | Pattern |
|---|---|
| `0xcccf50` | `44 88 44 24 18 89 54 24 10 48 89 4C 24 08 55 53 56 57 41 54 41 55 41 56 41 57 48 8B EC 48 83 EC 78 C5 F8 29 74 24 60 C5 F8 29 7C 24 50 49 8B F1` |

A hook on `0xcccf50` would see each destination as the pin moves, with its name and detail objects in `r9` and on
the stack; `0xcd05f0` was not given a signature.

`WorldMapLayout::DetailLayout::vf17` (`0xce7d20`) binds four follower parts with render lambdas,
`font_rect_C2_follower[LIST]` and `parts_info_cityimg`: no name or description pane.

### The place-name files aren't on tonight's serials

The message-file table (`0x27b0cd0`, `0xd0`-byte entries, name pointer at `+0x0`, serial test at `+0x18`,
[earlier note](field-notices-and-map-travel-static.md)) gives these ranges, each read from its test's `lea`/`cmp`:

| Entry | File | Serials |
|---|---|---|
| 3 | `MapName.msg` | test jumps into `.text1`; not read |
| 34 | `AccessPointName.msg` | 10873–11280 |
| 40 | `system/Big_Mapname.msg` | 10102–10124 |
| 66 | `system/WorldMap_Message.msg` | 10860–10872 |
| 51, 61–65 | `ui/Townmap.msg`, `system/Map_Martira`, `_Altavely`, `_Brilhaven`, `_Grand`, `_Virga` | no serial test |
| 5, 22 | `system/System_Message.msg`, `system/System_General.msg` | 1142–1931, 9270–9308 |

Tonight's 1420 and 1910–1912 are `System_Message.msg` labels and 9276 a `System_General.msg` one, so none is a place
name. `0xefe270` turns a type 0–6 into `WorldMap_Message` serials 10860–10865 and fetches them with
`GetMessageBySerial`; none appeared, so whatever it labels wasn't drawn. The location banner builds its name as a
composed message handed to a `.text1` setter, never one lookup ([#586's note](field-map-region-area-name-static.md));
the panel likely does the same `[NEEDS VERIFICATION]`.

### How the selection moves

No `cursor` record means the map doesn't use the shared cursor manager. On the sortie map the change lands in
`0xcd05f0`, called from `0xcd4670`; the input that drives it (likely `fclSortieTownMap`, whose slots are `.ecode`)
wasn't followed. Script functions `SortieMapUI_GetPointID` (`0x911600`) and `SortieMapUI_MoveAutoCursor` (`0x911be0`)
read and move the selected point for scripts.

## Steps

Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1`), 2026-11-20: `decompile` on `0xcca910`, `0xcca1b0`,
`0xcc7f10`, `0xcc8060`, `0xccc440`, `0xce7d20`, `0xefe270`, `0xcccf50`, `0xcd05f0`; `callers` on `0xefe270`,
`0xcccf50`, `0xcd05f0`; `offset 0xc08` (the writers of `Detail+0xc08`, which led to `0xcccf50`). Vtables and the message-file ranges: a read-only Python 3.12 walk of the exe (RTTI; the table's
entries and each serial test's first bytes). Pane strings: `grep -ao` over the exe. Entry bytes and the signature:
`tools/Metaphor.SignatureCheck --bytes` and `--probes`.

What would settle it, with `ui-text-panes`, `GetMessageBySerial` and `create-msg-from-string` logging on (Debug build),
noting log times:

1. Open the travel map and say which it is (the Gauntlet Runner's route map, or the world map from the menu). Move
   between two destinations, three times each way.
2. Look for `pane` records whose text is each destination's name and its info line, changing with the pin. If they
   come, the panel is readable from the pane listener with no new hook.
3. Look for `MES_Location_CurrentPoint` as a `request` and for serials 10102–10124, 10860–10872 or 10873–11280; none
   confirms the panel's text isn't fetched by serial.
4. Confirm a destination and cancel back: the question should be a `select` record.
