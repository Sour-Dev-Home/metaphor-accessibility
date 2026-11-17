# Field position: the player unit, its position and facing, and the camera's direction (#144, static leads)

- **Source:** our own reading of the game executable on disk, as shipped (ADR 0002), without running the game: a
  throwaway local disassembler (Iced 1.21.0, the NuGet package, on .NET SDK 9.0.319, in a scratch project outside the
  repository) and `tools/Metaphor.SignatureCheck --probes` (this repository at the commit that adds this file).
  Nothing was patched, unpacked or run. Script-function addresses come from
  `squirrel-script-functions-all-2026-11-16.md`. Community sources, read through the GitHub API: `NM-20/Resonant` at
  `6b90020b2a99175e25ea7cfc0ae2dc4efc987c89` (2026-08-02, GPL-3.0; C++ headers that reconstruct the game's classes,
  no README text) and `rirurin/opengfd` at `167739405d2e1f0520980186b7ad6fbcf3f9e559` (2026-03-19, LGPL-3.0).
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Addresses are RVAs (relative to the module) in this build. Identifiers and offsets only: no game content (ground rule
4). Everything here is read from code; nothing has been seen in a running game, so every claim about a runtime value is
`[NEEDS VERIFICATION]` until session 6.

## What was checked first (ground rule 3)

- `rirurin/opengfd` covers the GFD engine layer (`opengfd/src/object/camera.rs`: a `Camera` with a view matrix,
  projection, near/far clip, `fovy`, `roll`; `opengfd/src/kernel/global_xrd759.rs`: the engine's global for this game's
  code name, `xrd759`). It has nothing for the game's own field layer (`app::fld`). Useful later if we need the render
  camera's view matrix.
- `NM-20/Resonant` reconstructs the field layer. `program/app/field/new/world/fldWorld.h` gives `fldWorld` (a
  `fw::SingletonBase`) as: `+0x10` `fldCameraManager*`, `+0x18` unknown, `+0x20` `fldUnitManager*`, `+0x28`
  `fldInputManager*`, `+0x30` `fldAIManager*`, `+0x38` `fldBgManager*`; its instance pointer at `0x142EB7FF0`, which is
  not this build's address (see below). `fldUnitManager.h` puts `fldPlayer*` at `+0x00`; `app/unit/base/Unit.h` puts a
  model component (`UnitCpntModel`, a `mgwModel`) at `+0x10`, a collision and a navigation component after it, and
  free space from `+0x9F8`. Its build is not stated. Our reading below agrees with every offset we could check.
- No other public source found: GitHub code search for `fldWorld` and `FldGetCurrentMapID` found only Resonant and
  MF.Toolkit's script-function names (already in `squirrel-script-functions-all-2026-11-16.md`). `Lyall/MetaphorFix`
  (MIT) is a display fix with no field structures.

## Findings

**The field world is a singleton at `0x2EE7510`.** That RVA is in the writable data section named `.debug`. Every
`Fld*` script function that touches a unit reads it. The `fldScene_Root` destructor (`0x5EF2E0`) clears it. The
`fldWorld` destructor (vtable `0x180C740`, at `0x6FEC50`) frees `+0x10` (size `0xA8`), `+0x20` (size `0x558`),
`+0x18` (`0x50`), `+0x28` (`0x3F40`), `+0x30` and `+0x38` (`0x18`), which matches Resonant's member list.
Resonant's two singleton addresses (`fldWorld` `0x142EB7FF0`, `fldDatabase` `0x142EB7E48`) are both `0x2F520` below
ours (`0x142EE7510`, `0x142EE7368`), which reads as an older build with the same layout `[NEEDS VERIFICATION]`.

**The player:** `[[world]+0x20]+0x00` (`fldUnitManager::p_player`). `fldPlayer` has vtable `0x18088D8`, size
`0x5570` (its deleting destructor at `0x680CA0` frees `0x5570` bytes) and derives from `fldChara` (vtable `0x1805DD0`).

**Position:** a 16-byte vector at unit `+0x80`, x, y, z then a fourth lane. `FldPlayerGetDegToEnemy` (`0x8E5070`)
subtracts the player's `+0x80` from the enemy's `+0x80`, takes the length of the x, y, z lanes, and calls `0x14B3AA2`
(read as `atan2f`) on lanes 0 and 2. `FldNpcGetDegToPlayer` (`0x8E2D00`) does the same with an NPC. Units' `+0x80` is
`+0x70` inside the model component at `+0x10`. That y is up (lanes 0 and 2 are the ground plane), and the world unit
(metres or not), are `[NEEDS VERIFICATION]`.

**Facing:** a rotation quaternion at unit `+0x90` (`+0x80` in the model component). `0x587AB0` (we call it
`UnitTransformYaw`, `float (component*)`) rotates the vector `(0, 0, 1)` (constant at `0x17DC9A0`) by the quaternion at
`[rcx+0x80]` and tail-calls `0x14B3AA2` with the result's lanes 0 and 2. So yaw in radians is `atan2(x, z)`: `0` faces
+z and positive turns toward +x `[NEEDS VERIFICATION]`. `FldPlayerAddDeg` (`0x8E4E80`) reads it with `0x587AB0` on
`player+0x10`, adds degrees times `0x17D7118` (π/180), halves it (`0x17C5E5C` = 0.5), takes sine and cosine
(`0x14B1240`) and writes the quaternion `(0, sin, 0, cos)` through `0x9739B0`. So turning is about the y axis alone.

**Camera direction:** `[world]+0x10` holds the camera manager (Resonant: `fldCameraManager`). Its `+0x00` is the
current camera object (`0x57F860` and `0x57F890` are both `mov rax,[rcx]; ret`), and its `+0x08` is the camera type:
`FldCameraIsTypeFollow` (`0x8D89F0`) returns `[[world]+0x10]+0x08 == 5`. `FldCameraGetRelativeToPlayerDir_Yaw`
(`0x8D8870`) takes the camera object's `+0x70` minus its `+0x60`, normalises it, and compares it with the player's
facing (the quaternion at `player+0x90` applied to `(0, 0, 1)`). So the camera's horizontal direction is
`atan2(dx, dz)` of `(+0x70 − +0x60)`. Which vector is the eye and which the target, and so whether that difference
points forward or back, is `[NEEDS VERIFICATION]`. Which class the camera object is (one of `fldTypeCamera_*` /
`mgwTypeCamera_*`, which RTTI names) is unknown, and type `5` = follow is the only type id we read.

`FldCameraFollow_SetYaw` (`0x8D8460`) tail-calls `0x588D20`, which jumps into `.text1`; it was not followed.

## Byte-pattern probes

`tools/Metaphor.SignatureCheck/field-probes.json`, each one match on disk in this build:
`FldPlayerGetDegToEnemy` (`0x8E5070`), `UnitTransformYaw` (`0x587AB0`), `FldCameraGetRelativeToPlayerDir_Yaw`
(`0x8D8870`), `FldCameraIsTypeFollow` (`0x8D89F0`). The world pointer's RVA is the RIP-relative operand at `+7` of
`FldPlayerGetDegToEnemy` and `+3` of `FldCameraIsTypeFollow`.

## To confirm in session 6

Read only; no writes, no hooks beyond the existing loader's build check.

1. Stand still in a town. Read `[base+0x2EE7510]` (non-null), then `[[...]+0x20]` and its `+0x00` (the player). Check
   the player's first qword is `base+0x18088D8` (the `fldPlayer` vtable).
2. Read the player's `+0x80..+0x8C` floats while walking straight forward, then sideways: one horizontal lane pair
   should change, y should stay level on flat ground. Note the scale against a known distance.
3. Read the quaternion at `+0x90`, turn the character to face four directions, and check `atan2(x, z)` of the rotated
   `(0, 0, 1)` steps by about 90°.
4. Read `[[world]+0x10]+0x08` (expect 5 in normal control) and the camera object's `+0x60` and `+0x70`. Rotate the
   camera with the stick: work out which vector is the eye (the one that orbits the player) and the sign of yaw.
