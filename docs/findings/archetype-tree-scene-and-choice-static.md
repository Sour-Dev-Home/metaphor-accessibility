# The Archetype Tree is scene 0x11 in the menus' scene map, and `AcademiaClassSelectChoice` is its likely choice

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`archetype-screen-classes-session5-2026-11-17.md`](../raw-sources/archetype-screen-classes-session5-2026-11-17.md),
  [`archetype-tree-class-session6-2026-11-17.md`](../raw-sources/archetype-tree-class-session6-2026-11-17.md),
  [`menus-open-session6-2026-11-17.md`](../raw-sources/menus-open-session6-2026-11-17.md)

Static prep for #227, read in Ghidra only and not yet run against the game. Session 6 left two candidates for the
tree: `Tree@tree@academia@layout@ui@app` and `fclAcademiaTree@fcl@app`. The code says `fclAcademiaTree` is the
tree's **scene**, reachable from the menus' handle. Its **screen class** (the `ope` choice) is most likely
`AcademiaClassSelectChoice@ope@ui@app`.

## Evidence

### The scene map behind the menus' handle

The script functions `AcademiaLearnTalk_GetCharaID` (`0x903140`), `FclShopGetItemID` (`0x906720`) and four others
share one lookup:

1. `0x51fab0` reads the menus' handle `H` at RVA `0x2ee7338` (session 6: non-null while a field menu is open) and returns
   `[H+0x48]`, a pointer to a vector `{begin, end}` (corrected by game session 1: see
   [the scene-map path](menus-scene-map-path.md)).
2. The first element's `+0x8` is a map `M` keyed by a scene number. `0x4ccde0` takes the bucket at
   `[M+0x28] + (key & [M+0x40]) × 0x10`, starts from its second pointer, walks back through each node's `+8` until
   node `+0x10` equals the key, and gives node `+0x18`.
3. The caller casts node `+0x18`'s pointer from `fclAppSceneBase` to the scene class.

| Key | Scene class | Read by | Field read |
|---|---|---|---|
| `0x11` | `fclAcademiaTree@fcl@app` (vtable `0x17f7108`) | `0x902e90`, `0x9030d0`, `0x903140`, `0x9031b0`, `0x903220`, `0x903290` (`AcademiaItemGetWindowEventOpen`, `AcademiaLearnTalk_*`) | `+0xcb8` character ID, in `0x903140` |
| `0xf` | `fclSpotItemShopMenu@fcl@app` | `FclShop*`, `ItemShopChoice` | see the #196 note |
| `2` | `fclCampItemMenu@fcl@app` | `FclShopGetItemID` while the camp menu is open | `+0x90` |

Confidence: high that key `0x11` is `fclAcademiaTree`; that its pointer is the scene while the tree is on screen is
`[NEEDS VERIFICATION]`.

### The choice

`AcademiaClassSelectChoice@ope@ui@app` (vtable `0x21f7130`, `0xad8` bytes) is built by `0xd51520`, which is case
`0x67` of the choice factory `0xd5c6b0`. It is a `SingleChoiceBase_WithLayout`: slot 11 is `ChoiceGetCursor`
(`0xd4ab90`), the getter the screen-class hook uses. Its own slots 14 to 17 (`0xe49f20`, `0xe49500`, `0xe48d30`,
`0xe4b110`) override the movement:

- `vf15` takes a handle at `+0x3e4` (passed to `0xf2a720`, which jumps into `.text1`) and searches an `int` array at
  `+0x7a0`, with its count at `+0x7a8`, for the result. It then calls `ChoiceGetCursor` and sets the manager's index
  through a `.text1` function. So the tree moves by a node handle and only sometimes asks for its manager. That would
  explain why session 6 logged no `cursor` record while it moved.
- `vf14` reads `+0x7a0`, `+0x7a8`, `+0x898`, `+0x8a0`, `+0x8e8`, `+0x8f0` and `+0xac8`.

The layout parts are in `tree@academia@layout@ui@app`: `Tree`, `Cursor`, `Node` arrays, and `NodeInfoItem` (an array of
58, `archetype_select`). `Tree` has no lookup or cast in analysed code (`global 0x2c32fd8` finds none), so it isn't
the scene.

Confidence: medium that `AcademiaClassSelectChoice` is the tree's choice. It's the only Academia choice that names a
class selection and isn't one of the three details classes session 5 named. The tree node at `+0x3e4` is a lead.

### Archetype details: which manager is which tab

`ClassDetailBase<CampArchetypeDetailLayout>::vf11` (`0xd976f0`, shared by `EquipClassDetailChoice`,
`StatusMenuClassDetailChoice`, `AcademiaClassDetailChoice` and `BTL_ClassChange`) reads the tab index at `+0x1738`,
looks it up in an `int` array object at `+0x1700` (count at `+0x1708`), and returns the manager by that kind: kind 0
gives `+0x1740`, kind 1 gives `+0x1760`, and kind 2 is the remaining branch, `+0x1750` by elimination
`[NEEDS VERIFICATION]`. Which kind each on-screen tab is stays `[NEEDS VERIFICATION]`: it needs the choice's address,
which the inspector has no path to.

## Tonight

Deploy a Debug build with `opt-in-inspector.txt` ([how-to](../contributors/how-to/inspect-the-running-game.md)).

1. **Main menu, then Y for the Archetype Tree, pick a character, then the tree on screen:**
   `pwsh scripts/dev/session.ps1 inspect 'follow base+0x2ee7338 0x48 0x0 0x0 0x8 0x40'`, then `read` 8 bytes there for the mask `m`.
2. Bucket offset = `(0x11 & m) × 0x10 + 8`. Run
   `'follow base+0x2ee7338 0x48 0x0 0x0 0x8 0x28 <offset> 0x10'` and `read` 4 bytes: expect 0x11, little-endian.
   Then `'follow base+0x2ee7338 0x48 0x0 0x0 0x8 0x28 <offset> 0x18 0x0'` and `rtti` on the result: expect
   `fclAcademiaTree@fcl@app`. On another key, insert `0x8` before `0x10` or `0x18` to step one node back.
3. **Same screen:** `read <scene> 0x1000`, then `rtti` each 8-byte value that looks like a heap pointer. One naming
   `AcademiaClassSelectChoice@ope@ui@app` settles the screen class. Also `read <scene>+0xcb8 4`: the character ID.
4. **If step 3 finds the choice:** `watch <choice>+0x3e0 8` and `watch <choice>+0x7a0 0x10`, move one node, then
   `changes`. A change at `+0x3e4` on every move confirms the node handle.
5. **Back on the main menu:** step 1 again. Expect a null handle or a different key 0x11 entry, which shows the
   scene's lifetime.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query.

1. `decompile 0x902e80` and `0x902e60`: Academia's script state uses `CampStatusIsOpenBody` and `0x3b0ab0`.
2. `global 0x2c060d8` (`fclAcademiaTree`'s type descriptor), `global 0x2c32fd8` (`Tree`'s), `global 0x2c06d38`
   (`fclSpotItemShopMenu`'s). Type descriptors were located by finding each decorated name in `.debug`, minus 0x10.
3. `decompile 0x903140`, `0x51fab0`, `0x4ccde0`: the key `0x11`, the handle and the map walk.
4. `vtable app::ui::ope::AcademiaClassSelectChoice`, `global 0x21f7130`, `callers 0xe37ef0`, `decompile 0xd51520`,
   `callers 0xd51520`, `decompile 0xd5c6b0` (around `0xd5d9ff`).
5. `decompile 0xe49500`, `0xe49f20`, `0xe48d30`, `0xe4b110`, `0xf2a720`.
6. `offset 0x1740`, `0x1750`, `0x1760`, then `decompile 0xd976f0`.

The class names come from the exe's own type information. Nothing in `.text1` was decompiled: `0xf2a720` and
`vf15`'s index setter jump there and were not followed.
