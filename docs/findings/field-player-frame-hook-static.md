# The field player is a key-press read with no new hook; `fldScene_Idle::vf3` is the free-roam frame hook

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-position-facing-map-static.md`](field-position-facing-map-static.md),
  [`field-navmesh-and-path-queries-static.md`](field-navmesh-and-path-queries-static.md),
  [`field-position-camera-session6-2026-11-17.md`](../raw-sources/field-position-camera-session6-2026-11-17.md),
  [`input-route-2026-11-16.md`](../raw-sources/input-route-2026-11-16.md)
- **Functions:**
  - fldScene_Idle::vf3 | 0x5f0310 | .ecode | Field | known only | The free-roam frame update, slot 3 of the `fldScene_Idle` vtable.
  - FieldNavmeshSnap | 0x576410 | .ecode | Field | read | Called from `vf3` with the player and navmesh in hand; snaps the player to the navmesh.
  - fldScene_Idle vtable | 0x1801d28 | .udata | Field | known only | The free-roam scene's vtable, with `vf3` in slot 3.

For #178 and #181. The player is `P = [[[base+0x2ee7510]+0x20]+0x0]` (`fldWorld`, its unit manager, the manager's first
qword); position floats at `P+0x80`, facing quaternion at `P+0x90`, as session 6 read them
([position note](field-position-facing-map-static.md)). A "nearby" or "where am I" key only needs these when it is
pressed, so the mod can read them from the input frame it already runs on every pad read (`PadRead`, `KeyboardRead`,
both `.ecode` hooks in `hook-signatures.json`). If a per-frame field hook is ever needed (sonar #182, guidance #183),
the free-roam update `fldScene_Idle::vf3` and its navmesh snap `0x576410` are whole `.ecode` functions, not jump stubs,
and each signature below matches once.

| Need | Candidate | Where | Confidence |
|---|---|---|---|
| Player | `P = [[[base+0x2ee7510]+0x20]]`; `P+0x80` x, y, z; `P+0x90` quaternion x, y, z, w | memory read | High (session 6) |
| Free roam only | scene `[[[base+0x2ee7348]+0x10]+0x10]` has RTTI `fldScene_Idle` | memory read | High (session 6) |
| Read on a key | the existing input frame (`PadRead` / `KeyboardRead` hooks) | no new hook | Medium: same thread as the field update `[NEEDS VERIFICATION]` |
| Per-frame field hook | `fldScene_Idle::vf3` `0x5f0310` | `.ecode`, vtable `0x1801d28` slot 3 | Medium-high |
| Per-frame, player and navmesh in hand | `0x576410`, called from `0x5f0891` in `vf3` | `.ecode` | Medium-high |

## Evidence

- **The free-roam frame.** `fldScene_Idle`'s vtable (`0x1801d28`) has `vf3` at `0x5f0310`. Its body is in `.ecode`
  (the section ends at `0x17c0000`; `.text1` starts at `0x36c5000`) and opens with a full prologue, not a `jmp`, so the
  maintainer's rule on `.ecode` stubs that hand over to `.text1` (#586, 2026-11-21) doesn't touch it. It makes dozens
  of calls in turn, among them `0x576410` at `0x5f0891`
  ([navmesh note](field-navmesh-and-path-queries-static.md), section 3). `0x576410` loads `fldWorld`, gets `fldBg`
  through `[world+0x38]`, loads the player's `+0x80` and snaps it to the navmesh with `0x13f2930`. That `vf3` runs once
  a frame while the scene is `fldScene_Idle` is inferred from its being the idle scene's update slot and from session
  6 seeing `fldScene_Idle` only in free roam `[NEEDS VERIFICATION]`.
- **The input frame.** The mod already reads game state on each `PadRead` call (the dialogue-window flag was read on
  every pad read for 35 minutes in session 5, `hook-signatures.json`'s `MsgIsSelectWindowOpenedBody` evidence). Whether
  the pad read runs on the thread that updates the field, so the unit lists can't change mid-read, is not shown
  `[NEEDS VERIFICATION]`; #181's threading rule needs it.
- **Facing.** Heading `atan2(2(xz + wy), 1 − 2(x² + y²))`, counter-clockwise from above, from the session-6 reader
  ([position note](field-position-facing-map-static.md)). The camera's eye and look-at (`[[world+0x10]+0x0]+0x60` and
  `+0x70`) give the camera-relative clock directions #144 asks for.

### Signatures

Each matches once with `tools/Metaphor.SignatureCheck --probes` (a scratch probes file, not committed):

| Name | RVA | Pattern |
|---|---|---|
| `fldScene_Idle::vf3` | `0x5f0310` | `48 8B C4 48 89 58 08 55 56 57 41 54 41 55 41 56 41 57 48 8D 68 A1 48 81 EC 90 00 00 00 C5 F8 29 70 B8 C5 F8 29 78 A8 C5 F8 28 F9 48 8B F9 48 8B 35 ?? ?? ?? ?? 4C 8B 2D ?? ?? ?? ?? 33 C9 E8` |
| navmesh snap | `0x576410` | `48 8B C4 48 89 58 10 57 48 81 EC B0 00 00 00 48 8B D9 C5 F8 29 70 E8 48 8B 0D ?? ?? ?? ?? C5 78 29 40 C8 C5 78 28 C1 48 8B 49 38 E8 ?? ?? ?? ?? 48 8B C8 48 8B F8 48 8B 10 FF 52 38` |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project of the build above, Windows 11 x64,
2026-11-21. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says otherwise.

1. `vtable app::fld::fldScene_Idle`; `disasm 0x5f0310` (its calls, the one at `0x5f0891`); `disasm 0x576410`.
2. The section table: a read-only Python 3.12 walk of the exe's PE headers. Bytes: `tools/Metaphor.SignatureCheck
   --bytes <exe> 0x5f0310 0x40` and `0x576410`; the patterns above with `--probes`.

Nothing in `.text1` was decompiled. No decompiled output is quoted; names are the exe's own.
