# Capture evening 2, field part: static leads and what each log record means (#250)

- **Source:** our own reading of the game exe on disk, as shipped (ADR 0002: nothing patched, unpacked or run): a
  throwaway local disassembler (Iced 1.21.0, the NuGet package, on .NET SDK 9.0, in a scratch folder outside the
  repository, not committed) with an RTTI-to-vtable lister added; and `tools/Metaphor.SignatureCheck --probes` with
  `field-probes.json` and the full hook signature check (this repository at the commit that adds this file). Builds on
  `field-position-2026-11-16.md`, `field-actors-2026-11-16.md`, `field-state-and-map-2026-11-16.md`,
  `field-inventory-2026-11-17.md`, `archetype-detail-cursors-2026-11-17.md`,
  `archetype-screen-classes-session5-2026-11-17.md` and `ui-focus-candidates-2026-11-16.md`.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Addresses are RVAs in this build. Identifiers, offsets and class names only: no game content (ground rule 4). Nothing
here has been seen in a running game: every claim about a runtime value is `[NEEDS VERIFICATION]` until capture evening
2 logs it. No new hook goes in `.text1`, or anywhere: every reader below is a read through `ModuleMemoryReader`.

## New static findings

**A cursor list's item count (#249): `CursorManager+0x58`.** `CursorManager` (vtable `0x22045e8`) slot 9 (`0xe99670`)
and slot 10 (`0xe996e0`) are plain `.ecode`. Both take a vector from `+0x88` (begin) to `+0x90` (end) of 16-byte
elements, the first qword of each a pointer to an item (`CursorItemInfo@CursorManagerBase`, by RTTI), and loop over
the 32-bit count at `+0x58`: slot 10 walks forward from 0 and returns the first index whose item has byte `+8` set or
(when bit `0x20` of the manager's flags at `+0xAC` is set) byte `+9` clear; slot 9 does the same from `[+0x58]−1`
backwards. Slots 11 (`0xe99b00`) and 12 (`0xe99ad0`) compare the cursor's index (`+0x60` + `+0x64`, minus or plus 1)
with them. So `+0x58` reads as the list's length and the two bytes as per-item flags (one maybe "selectable"); what
they mean on screen, and whether `+0x58` equals the vector length, are `[NEEDS VERIFICATION]`. Slot 10's first 35 bytes
match once on disk (rva `0xe996e0`); it is not used as a signature, since the probe reads only managers the cursor hook
hands it.

**The camp menu's open state (#250, new ticket under #211).** The script function `CampStatus_IsClosed` (`0x903ab0`)
calls `0x51f370` → `0x51fad0` with `-1`, which loads the global at **`0x2ee7338`** and jumps to `0x7c0380`: null
means not open; otherwise `0x10701a0` reads the handle's 32-bit kind at `+0` and, for kinds 1, 2 and 3, walks one of
three linked lists (heads at `0x2f12da0` for kind 1 and `0x2f12dd0` for kind 3, linked at `+0x68`; `0x2f12db0` for kind 2,
linked at `+0x78`) for a node whose qword at `+0xB0` equals the handle's `+0xB0`. Found means open. The global is
written at `0x51f683`. The lists read as the game's live tasks or processes, and the handle as the camp menu's
`[NEEDS VERIFICATION]`. `SeqCallCamp` (`0x9100f0`) and `SeqCallStartMenu` (`0x9101b0`) go through `0x7bf890`, a jump
into `.text1`, so how the menu is started was not read.

**The Archetype Tree's class (#227): candidates share the plain getter.** By RTTI, `AcademiaTopChoice` (vtable
`0x21f2e18`), `AcademiaClassSelectChoice` (`0x21f7130`) and `AcademiaClassGetCharaSel` (`0x21eac68`) all have slot 11 =
`0xd4ab90`, the getter `ChoiceCursorGetterListener` already hooks, and differ only in slots 0–2. Session 5 logged nothing
on the tree because the probe writes only on a cursor input, and the tree's cursor sent none. If the tree's choice calls
the getter at all, a record on each new choice and manager pair names it; the layout class
`Tree@tree@academia@layout@ui@app` (vtable `0x2157730`) and `fclAcademiaTree@fcl@app` (`0x17f7108`) are the other
candidates `[NEEDS VERIFICATION]`.

## Signatures (all `.ecode`, one match on disk each, read only, never hooked)

| Name | RVA | Global read | Where |
|---|---|---|---|
| `FldCameraIsTypeFollow` | `0x8d89f0` | `fldWorld` `0x2ee7510` | `mov rax,[rip+…]` at `+0` |
| `FieldSceneRootCreate` | `0x5220c6` | `fldScene_Root` `0x2ee7348` | `mov [rip+…],rbx` at `+0x2F` |
| `FldCurrentMapIdBody` | `0x71b610` | `fldDatabase` `0x2ee7368` | `mov rcx,[rip+…]` at `+0` (new) |
| `CampStatusIsOpenBody` | `0x51fad0` | camp handle `0x2ee7338` | `mov rcx,[rip+…]` at `+9` (new) |
| `TaskHandleAlive` | `0x10701a0` | list heads above | at `+0x16`, `+0x43`, `cmove` at `+0x4D` (new) |

`SignatureCheck --probes … field-probes.json`: 14 of 14 matched exactly once; the full check: 42 of 42.

## What the kit logs

All Debug-only, to the local `message-log.txt`, numbers and class names only (no game text, no layout or map names, no
game function called).

- `opt-in-field.txt` (no hook of its own; read on each pad read by `PadReadHook`, which then also needs the five
  signatures above):
  - `field scene=<class> map=<id> player=<class> pos=x,y,z facing=<deg> locks=<n> action=0x<bits> cam=<type>
    cam60=x,y,z cam70=x,y,z camHeading=<deg>`, at most every 250 ms, on change. `scene` is the class of
    `[[[root]+0x10]+0x10]` (#180: `fldScene_Idle` for free roam?); `map` is `fldDatabase+0x48`; `player` is
    `[[world+0x20]]`'s class (expect `fldPlayer`); `pos` is `player+0x80`; `facing` is `atan2(x, z)` of `(0, 0, 1)`
    rotated by the quaternion at `player+0x90`; `locks` is `player+0x34CC`, `action` is `player+0x34C8`; `cam` is
    `[world+0x10]+0x08` (5 = follow); `cam60`/`cam70` are the camera object's `+0x60`/`+0x70` and `camHeading` the heading
    of `cam70 − cam60` (#177).
  - `field-near <list>=<count>(<first unit's class>)[<id>@<distance>/<heading> …]`, at most every second, on change: each
    non-empty list of the 21 in `fldUnitManager` (`0x40` apart from `+0x08`), with its three nearest units by
    `+0x80`, id at `+0x9F8`; doors add `/node<distance>/<heading>` from the model node (#178). Headings are world
    headings, as `facing`.
  - `menu-state camp=<handle> class=<class> kind=<n> id=0x<id> alive=<0|1|?> node=<class>` (or `camp=none`), at most
    every 100 ms, on change: the camp test above, replayed.
- `opt-in-list-count.txt` (no hook): `list-count mgr=0x… count=<+0x58> items=<vector length> flag8=<n> flag9=<n>`
  after each `cursor` record whose manager's numbers changed (#249).
- `opt-in-screen-class.txt` (the existing two hooks): adds `choice owner=0x… ownerClass=<class> mgr=0x…` once a minute
  at most per new choice and manager pair (#227).

## Not prepared

- **#74, #106 (names):** no new listener. The always-on Debug name listeners (`NameLookupListeners.cs`, from
  `name-lookup-candidates-2026-11-16.md`) already log the speaker-name lookups, and the player's name as lengths and
  match flags only when `spike-player-name.txt` holds the typed name.
- **#109 (button icons):** no static lead for where the game picks an icon set; the pad and keyboard `input` records
  already show which device is in use.

## To confirm in the session

1. **#177:** stand still, walk forward, sideways; turn to four directions; rotate the camera with the right stick. Check
   `pos` moves in x/z, `facing` steps by about 90°, and which of `cam60`/`cam70` orbits the player.
2. **#178:** walk past people, up to a door, a chest and enemies; compare `field-near` counts and nearest ids with the
   screen.
3. **#180:** note `scene` and `locks` while walking, in the camp menu, the large map, a battle, talking to someone and
   a cutscene; note `map` on each area change against the area name on screen.
4. **Menus closed:** open the menu, close it to the field; open it, close it, enter a battle from the field; talk to
   someone. Note `menu-state` `alive` each time.
5. **#249:** on three lists of different lengths, count the rows and compare with `count` and `items`.
6. **#227:** open the Archetype Tree; note any `choice` record between the character select and the details screen.
