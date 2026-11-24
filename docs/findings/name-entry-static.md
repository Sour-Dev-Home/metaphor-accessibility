# The name entry screen picks its title by a flag, and keeps the typed text in a 10-character array outside `.text1`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on [`yes-no-dialog-labels-2026-11-20.md`](../raw-sources/yes-no-dialog-labels-2026-11-20.md)
  and [`msg-window-show-line-2026-11-20.md`](../raw-sources/msg-window-show-line-2026-11-20.md)
- **Functions:**
  - fclNameEntry vtable | 0x17f66d0 | .udata | Menus | known only | The name entry screen's vtable.
  - fclNameEntry::vf2 | 0x4ff900 | .ecode | Menus | known only | The screen's update: switches on the state at `+0x18`.
  - fclNameEntry typing state | 0x4fef10 | .ecode | Menus | known only | Runs every frame while the player types and picks the title by the flag at `+0x481`.

For #585. The name entry screen is `fclNameEntry` (vtable `0x17f66d0`). Its `+0x481` byte is the script's
`isPlayerNameEntry` flag: set, the screen names the player and its title is `System_NameEntry_07`; clear, it names the
protagonist with `System_NameEntry_01`. While the player types, its update calls `0x4fef10` every frame, and the text so
far is in the keyboard object at `+0x20`, in a 16-bit character array at that object's `+0x20`: the count at `+0x28`
(always 10 in the game), the characters from `+0x30`, padded with zeros. The mod can read it there each frame without touching `.text1`.

## Evidence

**The screen and its states.** `fclNameEntry::vf2` (`0x4ff900`) switches on `+0x18`: state 0 runs the set-up
(`0x4ff5d0`), state 2 calls `0x4fef10`, the typing state, and later states show the confirmation (`0x4ffe60`, the
caller that [`yes-no-dialog-labels`](../raw-sources/yes-no-dialog-labels-2026-11-20.md) found). `0x4fef10`'s only
analysed caller is `vf2`.

**The flag.** The constructor (`0x4fd290`) stores at `+0x481` what `0x4aa520` returns for the string
`isPlayerNameEntry` (`0x17eac48`). `0x4fef10` passes `System_NameEntry_01` to `0x9ec320` when `+0x481` is clear and `System_NameEntry_07` when it is set, with kind 1; `0x4ffe60` picks `System_NameEntry_Confirm` or `_08` by the same
byte. The messages' order in `System_Message.msg` gives `_07` serial 1574, which
[the field-notices note](field-notices-and-map-travel-static.md) saw resolved at name entry, so `_07` is on screen in
the first naming. Which of the two routes shows the title, the `0x9ec320` window line on keyboard flag bit 7 or a layout
text by serial, is `[NEEDS VERIFICATION]`; the mod speaks the title on the first typing frame instead, from the flag.

**The keyboard.** `+0x20` holds a `NameEntryKeyboard_System` (vtable `0x22040b8`) or, when `0x7bbc30` returns 1 and the
flag is clear, a `NameEntryKeyboard_GameUI` (`0x2203ff8`); both derive from `NameEntryKeyboardBase` (`0x2203e70`).
`0x4fef10` reads its event bits through `vf20` (`0xe8c7e0`, returns `+0xa0`): bits 3 to 5 make it rebuild the box from
`vf15`, which hands `+0x20` to `0xec7dd60` in `.text1`. `vf18` (`0xe8d020`) returns `+0x20`.

**The array's layout.** The base destructor (`0xe8b600`) resets `+0x20` to `fw::ArrayBase<wchar_t>` and zeroes `+0x28`,
then `+0x48` to `fw::ArrayBase<char>` and `+0x50`. `NameEntryKeyboard_System::vf10` (`0xe8dc10`) copies 16-bit elements
from `+0x20`, bounded by the count at `+0x28`, through the array's getter (vtable `+0x10`), into a
`fw::StaticArray<wchar_t,10>`. The base constructor (`0xe8ad50`) builds `+0x20` in `.text1` (`0xec4af60`), so its
concrete type isn't readable. Three things point to `fw::StaticArray<wchar_t,10>`, with characters at `+0x10` in the
array, `+0x30` in the keyboard: the field is `0x28` bytes, that type's size; the screen's own name array at `+0x458`
is one (the constructor stores vtable `0x17f6658` there), also `0x28` bytes, and `0x4ffe60` copies its count
(`+0x460`) of 16-bit characters out of it; and that vtable's getter (`0x5005b0`) returns `this+0x10`. The mod reads
the keyboard's array only when its vtable equals the one at `+0x458`.

**In the game.** The maintainer's run of #593's Debug build (2026-11-21, this build) logged
`name-entry player=1 chars=10` once on the player's name screen, and typed letters were heard, so the hook installed
and the vtables matched. The count stayed 10 through typing and Backspace (`input keyboard` records with code `08`
between), so the array always counts 10 characters and the name is padded with zeros: the text ends at the first zero.
That the padding is zero, not another character, is inferred from deletions being silent before the fix.

**Not readable.** How a key becomes a character (`vf13`, `vf15`, `vf16` and `vf17` jump into `.text1`), and where the
typed name is filled into `<V 0>` for the confirmation (#569).

## Steps

Ghidra 12.2 headless through `scripts/dev/ghidra.ps1` (the shared project, [how-to](../contributors/how-to/query-the-ghidra-project.md)):
`global 0x17f6710` (the string `System_NameEntry_07`), `callers 0x4fef10`, `vtable app::fcl::fclNameEntry`,
`vtable app::fcl::NameEntryKeyboard_System`, `vtable app::fcl::NameEntryKeyboard_GameUI`, `vtable
app::fcl::NameEntryKeyboardBase`, `decompile` of `0x4fef10`, `0x4ff900`, `0x4ff5d0`, `0x4ffe60`, `0x4fd290`,
`0xe8b600`, `0xe8ad50`, `0xe8dc10`, `0xe8ccd0`, `0xe8c7e0`, `0xe8d020`, `global 0x17f6658`, `disasm 0x5005b0`. String
addresses and the `fw::StaticArray<wchar_t,10>` vtable (from its type descriptor at `0x2c04f20`) were found in the exe's
bytes with Python 3.12. The signature `NameEntryInputUpdate` matches once on disk
(`tools/Metaphor.SignatureCheck/name-entry-probes.json`).

In the game, with a Debug build: at each name entry the log shows `name-entry player=… chars=…` records as the name's
length changes; none means the vtable guard or the signature didn't match. The run above: `grep "name-entry\|input
keyboard"` in the mod folder's `message-log.txt`.
