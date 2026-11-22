# Button glyphs come from per-device tables keyed by pad-mask bit or key code; the `PAD_ICON` number's table isn't static

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  and reading the game's data files from `base.cpk` offline, as
  [`tutorial-table-maps-numbers-to-labels.md`](tutorial-table-maps-numbers-to-labels.md) did
- **Sources:** [`pad-and-keyboard-state-2026-11-16.md`](../raw-sources/pad-and-keyboard-state-2026-11-16.md),
  [`steam-input-type-enum-2026-11-22.md`](../raw-sources/steam-input-type-enum-2026-11-22.md),
  [`title-menu-item-images-2026-11-20.md`](../raw-sources/title-menu-item-images-2026-11-20.md),
  [`cri-cpk-table-layout-2026-11-21.md`](../raw-sources/cri-cpk-table-layout-2026-11-21.md)
- **Functions:**
  - ActionButtonGlyph | 0x140a7d710 | .ecode | Messages and Text | known only | Texture of the glyph for an input action: its key's glyph after keyboard or mouse input, else its pad button's glyph for the chosen device.
  - PadMaskGlyph | 0x140a7cd40 | .ecode | Messages and Text | known only | Texture of the glyph for a pad-mask bit, from the device set that setting `0x1d` picks.
  - KeyGlyph | 0x140a7de10 | .ecode | Messages and Text | known only | Texture of the glyph for a Windows virtual-key code (`keyboard/key_<vk>`).
  - PadGlyphSetFromSteam | 0x140a7aed0 | .ecode | Messages and Text | known only | Picks the auto-detect glyph set from the connected controller's Steam Input type.
  - GetActionBinding | 0x1407ade80 | .ecode | Settings | known only | Copies an action's binding (pad mask at `+0x14`, key at `+0x1c`) out of the map at `0x142ee85d0`.

For #109: the game draws a button prompt from a pad-mask bit through one table per controller family, or from a
virtual-key code when the keyboard or mouse was used last. The pad-mask-to-glyph tables are static and listed below.
Which bit or action each `<PAD_ICON n>` number stands for is not: the tag's handler isn't in code the disassembler can
read, so that last step is `[NEEDS VERIFICATION]` and needs a by-ear or log session.

## Evidence

**The glyph art.** `base.cpk` holds one button atlas per family: `COMMON/ui/ss/button_PS4.apk`, `_PS5`, `_XBOX`, `_NS`
and a plain `button.apk`, each with a UV table (`COMMON/ui/uv/button_*UV.bin`) of the same 55 sprite names, named after
PlayStation buttons (`btn_s_cross_wh`, `btn_s_L1_bk` and so on). Keyboard keys are `EN/ui/ss/keyboard.apk`, 206 sprites
`key_<vk>_wh`/`_bk`. `TextureManager::vf1` (`0x140ede590`) registers every sprite name in order, so a texture ID is the
name's position: `button_PS4` 4475–4529, `button_PS5` 4530–4584, `button_XBOX` 4585–4639, `button_NS` 4640–4694,
`keyboard` 4695–4900. `KeyGlyph`'s fallback for an unknown key is 4695/4696, the first keyboard sprite, which confirms
that IDs are positions.

**Pad-mask bit to glyph.** Static initialisers fill one hash map per family from constant `(mask, texture)` pairs at
`0x142129ba0`–`0x14212a09f`: PS4 (`0x140036410`, `0x140036320`), PS5 (`0x1400365f0`), Xbox (`0x1400367d0`), Switch
(`0x140036230`). Each family has a white and a black (`_wh`/`_bk`) map. The masks are the game's own pad mask, whose
mapping from XInput is in `pad-and-keyboard-state`. The Xbox column is what the decoded `button_XBOX` atlas shows in each
sprite (viewed as PNG, decoded as in `title-menu-item-images`). The PlayStation column is the sprite's name; that art
wasn't viewed.

| Game mask | XInput source | Sprite | Xbox atlas shows | PlayStation name |
|---|---|---|---|---|
| `0x4000` | A | `btn_s_cross` | A | Cross |
| `0x2000` | B | `btn_s_circle` | B | Circle |
| `0x8000` | X | `btn_s_square` | X | Square |
| `0x1000` | Y | `btn_s_triangle` | Y | Triangle |
| `0x0400` | LB | `btn_s_L1` | LB | L1 |
| `0x0800` | RB | `btn_s_R1` | RB | R1 |
| `0x0100` | left trigger | `btn_s_L2` | LT | L2 |
| `0x0200` | right trigger | `btn_s_R2` | RT | R2 |
| `0x0002` | left thumb | `btn_s_L3` | left stick pressed | L3 |
| `0x0004` | right thumb | `btn_s_R3` | right stick pressed | R3 |
| `0x0008` | Start | `btn_s_option` | Menu (three lines) | Options |
| `0x0001` | Back | `btn_s_touchpad` | View (two squares) | touch pad |
| `0x0010`, `0x0020`, `0x0040`, `0x0080` | D-pad up, right, down, left | `btn_s_key_up`, `_right`, `_down`, `_left` | D-pad with that arrow | same |
| `-2`, `-3` | none | `btn_s_LS`, `btn_s_RS` | left stick, right stick | same |
| `-4`, `-5` | none | `btn_s_key_updown`, `btn_s_key_LR` | D-pad up/down, D-pad left/right | same |

The negative keys are sticks and D-pad pairs that no single button produces.

**Which family.** Setting `0x1d` has five values (Auto-detect and Type 1–4 fit;
[settings inventory](settings-screens-inventory-static.md)). `PadMaskGlyph` and `ActionButtonGlyph` read it from the
settings map at `0x142ee8b80`: 1 is PS4, 2 PS5, 3 Xbox, 4 Switch (by the texture ranges of the maps each picks). Any other
value uses the auto-detect pair at `0x1427b6508`/`0x1427b6510`, which `PadGlyphSetFromSteam` and `0x140a7eda0` set from
Steam Input's `GetInputTypeForHandle`-shaped call (vtable slot `+0x128`): 5 (PS4) gives PS4, 13 (PS5) PS5, 10 (Switch
Pro) Switch, and 2, 3 (Xbox 360, Xbox One) or anything else gives Xbox. The slot's identity is `[NEEDS VERIFICATION]`;
the values match the Steam enum. So on PC, with no override, a controller Steam doesn't report as PlayStation or
Switch shows Xbox glyphs, which fits the maintainer's "A next, B back".

**Keyboard and bindings.** `ActionButtonGlyph(action, black)` takes an input-action ID (the battle command panel
passes 77 Skill, 78 Attack, 79 Synthesis, 80 Guard, 81 Pass, 82 Item, 91 Rewind; the message window passes 26). It reads
the action's binding with `GetActionBinding`: a pad mask at `+0x14` and a key code at `+0x1c`. When the input-device
global `0x1427a8bc0` is 1 or 2 (set by the keyboard and mouse readers; which is which is `[NEEDS VERIFICATION]`) and the
key isn't 10000 (unbound), it returns `KeyGlyph(key)`; otherwise the pad mask's glyph for the chosen family (actions 2
and 3 are pinned to D-pad right and left). So keyboard glyphs are the player's current key bindings, by Windows
virtual-key code, and the speech should name the key from that code (as `GetKeyNameText` does), not from a fixed table.
Where the binding map gets its defaults is `[NEEDS VERIFICATION]`: the init at `0x140022310` leaves it empty and no
binding file is in `base.cpk`.

**What isn't static.** The message compiler's tag list (`0x1409cc450`) gives `PAD_ICON` the code `0xD960`, but no
analysed code tests that code, like `CHAR_NAME`'s `0xD946`
([`title-names-voice-tutorials-static.md`](title-names-voice-tutorials-static.md)). `ActionButtonGlyph` and
`PadMaskGlyph` are both referenced from `.xcode` (`0x14c70b50`, `0x14c70b20`), which fits a caller in protected code. No
data table of 19 masks or action IDs turned up (searches of `.udata` and `.debug` for runs of mask values and of the
`msg_icon_pf` texture IDs 4956–4975). Whether message text draws from the button atlases or from `msg_icon_pf`
(`COMMON/` 16 sprites, `XBOX/` 20, named `pad_r_down`, `pad_l1` and so on) is `[NEEDS VERIFICATION]`; nothing in
analysed code loads the `msg_icon_pf` texture IDs.

**The numbers in use.** English messages use `<PAD_ICON n>` for n = 0, 1, 2, 5–16 and 18, almost all in
`System_Tutorial_2.msg`, plus 10 in `Title_System.msg` and 13 in `System_Message_PC.msg`. What the text says each one
does, as leads for the by-ear session, not as buttons:

| n | Uses | What the text says it does |
|---|---|---|
| 0 | 2 | retry a battle |
| 1 | 1 | auto-battle |
| 2 | 2 | save; Auto Recovery |
| 5 | 1 | Follower menu |
| 6 | 3 | Fae Sight |
| 7 | 4 | dash (hold); Assist |
| 8 | 6 | Order view; lock on; Map Menu; route choice |
| 9 | 7 | local map; World Map; route choice |
| 10 | 9 | Squad Battle; skills; Gallica's hint; Archetype from the Main Menu; Licenses on the title |
| 11 | 7 | select; form a group; stun from behind |
| 12 | 4 | dodge; guard; ride the blade |
| 13 | 8 | attack; Exit Game on the title |
| 14 | 2 | view details |
| 15 | 2 | Main Menu |
| 16 | 4 | move; highlight a location |
| 18 | 1 | move party members between rows |

One number covers different actions in different places (12 is dodge in the field and guard in battle), so `n` looks
like a pad button rather than one action `[NEEDS VERIFICATION]`.

## Steps

1. Game exe SHA-256 checked against the build above (PowerShell `Get-FileHash`); game found with
   `scripts/player/MetaphorInstall.psm1` `Find-MetaphorGame`.
2. `base.cpk`'s table of contents read with a Python 3.12 standard-library script following `CpkArchive.cs`; the
   `button*`, `keyboard`, `msg_icon*` UV tables and atlases extracted to the git-ignored `extracted/`. Atlases unpacked
   (`ZZZ0`, LZ4 block) and decoded with Pillow 13.0.0; sprite rectangles drawn from the UV tables' corner floats.
3. Shared Ghidra 12.2 project (`scripts/dev/ghidra.ps1`): `decompile` of `0x9cc450`, `0x36410`, `0x367d0`, `0x365f0`,
   `0x36230`, `0x36320`, `0xa7aed0`, `0xa7eda0`, `0xa7cd40`, `0xa7d710`, `0xa7de10`, `0x7ade80`, `0x22310`, `0x7abf30`;
   `callers` of `0xa7d710` and `0xa7cd40`; `global` of `0x2f04ae0`, `0x2f04be0`, `0x2f04b60`, `0x2f04c60`, `0x2f04b20`,
   `0x27b6508`, `0x2ee85d0`, `0x27a8bc0`, `0x212f658`, `0x2140a38`.
4. Python over the exe file: the `lea rax; mov [rsp+d32], rax` pairs of `TextureManager::vf1` for texture IDs; the
   constant pairs at `0x142129ba0`; byte searches for `0xD960` and the texture IDs; runs of mask values in `.udata` and
   `.debug`.
5. Python over the English `.msg` files: every `<PAD_ICON n>`, counted, with the text around it read locally (not kept).
