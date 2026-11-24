# The field map's name strip composes its region and area name from an id in `.ecode`, without a serial lookup

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-position-facing-map-static.md`](field-position-facing-map-static.md),
  [`field-notices-and-map-travel-static.md`](field-notices-and-map-travel-static.md),
  [`menu-hook-counts-2026-11-17.md`](../raw-sources/menu-hook-counts-2026-11-17.md)
- **Functions:**
  - field::map::Name vtable | 0x21a9c08 | .udata | Field | known only | The field map's name strip layout class.
  - Name strip id setter | 0xc93070 | .ecode | Field | known only | Stores a map id in the layout at `+0x7e8`.
  - Name strip text composer | 0xc94130 | .ecode | Field | known only | Turns the stored id into a text object through the region and area name list.
  - Name serial list builder | 0xefe180 | .ecode | Field | known only | Builds the list of three message serials: region, separator and area name.

Static prep for #586, also for "where am I" (#181, #192). The field map's name strip is the layout
`app::ui::layout::field::map::Name`, which `FieldMapNameLayout` extends. `0xc93070` stores an id in the layout at
`+0x7e8`; `0xc94130` turns that id into a text object through `0xefe180`, which lists three message serials: the
id's name, serial 9295 (`System_General.msg`) and a second name from the id. The serials go to a `.text1` composer,
not `GetMessageBySerial`, which fits the silent log of 2026-11-20. All three functions start in `.ecode` and match
once. The likely reading is that the list is "region, separator, area", but that, the id's kind and the text object's
layout are all `[NEEDS VERIFICATION]`.

| Piece | Where | Hookable outside `.text1` |
|---|---|---|
| Field map component | `UUIC_Field_Map@ui@app`, vtable `0x2259260` | Not followed |
| Name strip | `field::map::Name`, vtable `0x21a9c08`; `FieldMapNameLayout`, vtable `0x21aa108` | Setup slots `vf17` (`0xc926c0`), `vf18` (`0xc92910`): no, `jmp` into `.text1` |
| Set the id | `0xc93070 (layout, const u32* id)`: `[layout+0x7e8] = *id`, then two `.text1` lookups from it into `+0x7f0` and `+0x7ec` | Yes |
| Compose the name | `0xc94130 (layout)`: id from `+0x7e8`, result into the first text holder of `[layout+0x660]` | Yes |
| Name, separator, name | `0xefe180 (const u32* id, out)`: serials from `0xa022d0(id)`, 9295, `0xa8e74c0(id)`; composed by `.text1` `0xa601970` | Yes |
| Arrival banner | `UUIC_Field_LocationName@ui@app` (`0x2258f48`); text lambda `0xfc7030` | Yes |

## Evidence

### Setting and composing the name

Vtables were found from the RTTI type descriptors (complete object locator, then the pointer to it). `0xc93070`'s
first instructions copy `[rdx]` into `[rcx+0x7e8]`; it then fills `+0x7f0` and `+0x7ec` from two `.text1` stubs given
the same id, and shows or hides parts by those values (`< 5`, `< 0x59`). `0xc94130`, its callee (call at `0xc93333`),
reads `+0x7e8` and, unless a `.text1` test (`0xac8df50`) is true, calls `0xefe180` with it and the first holder of the
object at `+0x660`; the other branch uses a `.text1` composer (`0xf2313b0`). It stores the result in the holder and
refreshes it.

`0xefe180` builds a list of three `u32` serials: one from `0xa022d0` (a `jmp` stub into `.text1`, the same map-id
lookup the arrival banner uses), `0x244f` (9295, in `System_General.msg`, 9270–9308), and one from `0xa8e74c0` (a
`.text1` stub). It hands the list to `0xa601970` (a `.text1` stub) and returns the result. Its other analysed callers
are `0xefd9f0` and `0xefc730`, not read. That serial 9295 is a comma separator is a guess from the screen
("Tradia Desert, Sandstorm Barrens").

The setup slots bind panes `font_rect_C7_fieldname` and `font_rect_C4_areaname` to the holders at `+0x660`, but both
slots jump into `.text1` (to `0xd12ef90` and `0xd130070`), so which holder feeds which pane is `[NEEDS VERIFICATION]`.

### The arrival banner composes the same way

`UUIC_Field_LocationName`'s code (`0xfc6300`–`0xfc6930`) holds `0xfc65c0`, which wraps a map id in a lambda whose call
operator is `0xfc7030` (vtable `0x2259110`). The lambda reads the map's row in the table `0x898fa0` returns
(`0x2c`-byte rows, the `MapData` guess of the [map note](field-position-facing-map-static.md)), takes its `u16` at
`+0x12`, indexes a second table (`0x898d30`, `0x24`-byte rows) for a format serial, and lists the map's name serial
(`0xa022d0`), 10102 and 10103 (`Big_Mapname.msg`). It passes the layout and the list to `0xc93720`, a `jmp` stub to
`0xd193eb0` in `.text1`, so that call is no hook (ADR 0010). The stub's only analysed callers are four such lambdas
(`0xfc6c10`, `0xfc6de0`, `0xfc6f30`, `0xfc7030`). `0xa022d0` is also called from `0x4c2590`, the `MapNameTable` reader's
caller in the [map note](field-position-facing-map-static.md).

### Signatures

Each matches once in `.ecode` with `tools/Metaphor.SignatureCheck --probes`:

| Function | Pattern |
|---|---|
| `0xc93070` | `48 89 5C 24 18 55 56 57 41 56 41 57 48 83 EC 20 8B 02 48 8B F1 89 81 E8 07 00 00 48 8B FA` |
| `0xc94130` | `48 89 5C 24 08 48 89 74 24 20 57 48 83 EC 20 48 8B D9 E8 ?? ?? ?? ?? 8B B3 E8 07 00 00` |
| `0xefe180` | `48 89 5C 24 08 48 89 7C 24 10 55 48 8B EC 48 83 EC 40 48 8B FA 48 8B D9 C5 F9 EF C0 C5 FA 7F 45 E0 48 C7 45 F0 00 00 00 00 E8` |
| `0xfc7030` | `48 89 5C 24 10 48 89 74 24 18 55 57 41 56 48 8B EC 48 83 EC 40 48 8D 79 08 48 8B 32 48 63 1F 41 BE FF FF 00 00 E8` |

A hook on `0xc93070` gets the id each time the strip changes; the name itself is in the text object `0xefe180`
returns, whose layout isn't known. Reading the drawn panes through `ui-text-panes` needs no new hook.

## Steps

Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1`), 2026-11-20: `decompile` on `0xc926c0`, `0xc92910`,
`0xc951e0`, `0xc906a0`, `0xc93070`, `0xc94130`, `0xefe180`, `0xfc65c0`, `0xfc7030`; `disasm 0xfc7030`; `callers` on
`0xc94130`, `0xefe180`, `0xc93720`, `0xa022d0`, `0xfc65c0`; `rtti 0xfc7030`; `global 0x2259110`. Vtables and the
message-file ranges: a read-only Python 3.12 walk of the exe (RTTI; the message-file table at `0x27b0cd0`). Pane
strings: `grep -ao` over the exe. Entry bytes and signatures: `tools/Metaphor.SignatureCheck --bytes` and `--probes`.

What would settle it, with `ui-text-panes`, `GetMessageBySerial` and `create-msg-from-string` logging on (Debug build):

1. In the first dungeon, open the field map, close it, and open it again. Look for `pane` records whose text is the
   region and the area shown, as one string or two; none means the strip isn't drawn as a text pane.
2. Walk into a new area and open the map again: the pane text should change with the area.
3. Note whether the arrival banner on entering an area logs a `pane` record with the area's name, and whether serial
   9295 or 10102–10103 appear as `resolve` records (expected: none).
