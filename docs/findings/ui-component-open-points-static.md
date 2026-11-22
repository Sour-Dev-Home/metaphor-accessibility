# UI layouts open through one shared start-an-animation, and the item-get banner through its own open

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; the script function names are from
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)

No single hookable point sees every `UUIC_*` component open: they are created, registered and sent their requests from
`.xcode` and `.text1`. Their layouts' opens do share one function in `.ecode`, `0xa70010`, which starts an animation on
a layout; the item-get banner opens through `0xf560d0` instead. #691 hooks both, development-only. Read in Ghidra only;
nothing here has run against the game yet.

## Evidence

### Components: no common point outside the protection

- `UUIC_Parts_Base` (vtable `0x224e950`) is the components' base. Its slots 3, 4 and 10 are empty and slot 6 returns 0,
  each shared by over 115 vtables.
- Slot 5 takes a request: `UUIC_Common_EventItemGet::vf5` (`0xf55f60`), `UUIC_Common_Date::vf5` and
  `UUIC_Common_MoviePause::vf5` read it through `0xf484a0`, which checks a `type_info` at request `+0x38` and a
  has-value byte at `+0x40` (a `std::any` shape, [NEEDS VERIFICATION]). On it EventItemGet calls its layout's slot 20
  (`+0xa0`) or slot 21 (`+0xa8`) and sets its state at `+0x18` to 1 or 2. Slot 5 is an override per class, so it is no
  common point.
- No analysed code calls a component constructor or sends it a request: `callers 0xf55c80` (which sets EventItemGet's vtable,
  then removes its handler from the list below) gives one reference from `.xcode` (`0x14cb0ac8`). The handlers are keyed by layout type in a list at
  `[0x2f0c668]+0x48`; `UUIC_Common_Saving`'s destructor (`0xf8a390`) removes three, for `SavingLayout`,
  `AutoSavingLayout` and `SystemSavingLayout` (`type_info` at `0x2c63f30`, `0x2c63f60`, `0x2c63f90`). No other analysed
  code names those three types, nor `ItemGetNoticeLayout` (`0x2c64450`).
- The registry the battle finding found (`0x2ca3248`,
  [battle-turn-icons-and-ui-components-static.md](battle-turn-icons-and-ui-components-static.md)) is read inline at
  492 sites, with no shared getter.
- `UUIC_Common_Saving::vf5` (`0xf8a6f0`, the #543 lead) starts with `E9`, a jump to `0x1028e450` in `.text1`: a jump
  stub into `.text1`, which takes no hook.

### Layouts: `0xa70010`, the shared start-an-animation

- `0xa6ffd0` is `(layout, player, const u32* animation)`: it zeroes the fourth argument and a stack byte and calls
  `0xa70010`. `0xa70010` passes the player, the animation and both extras to the `ss::Player` thunk `0x14f0052a0`, then
  hands the player a finished-callback lambda holding the layout. Its other analysed caller is `0xa6fff0`.
- `0xa6ffd0` has 141 calling functions; 92 of them are some layout's slot 20, the slot the components call to open.
  Among them: `AutoSavingLayout::vf20` (`0xd1e250`), `SavingLayout::vf20` (`0xd1e410`) and
  `SystemSavingLayout::vf20` (`0xd1e460`), each `0xa6ffd0(this, this+0x28, &id)`. The 49 other callers may start other
  animations (close, loops), so the hook logs every call with its layout class and animation number.
- `ItemGetNotice::vf20` (`0xc1df90`, `+0xa0`) calls the `ss::Player` thunk itself, not `0xa6ffd0`.

### The item-get banner: `0xf560d0`

`0xf560d0` is `(UUIC_Common_EventItemGet*, items*)`: it copies up to five item ids and counts into a
`StaticArray<ItemGetNotice::ListInfoItem,5>` and fills the layout at component `+0x20`. Its callers are
`ItemGetTreasure` (`0x90b3c0`), `ItemGetWindowEventOpen` (`0x90b4b0`), `AcademiaItemGetWindowEventOpen` (`0x902e90`),
`0x3fb190`, two component updates (`UUIC_Common_Cooking::vf13`, `UUIC_Battle_ResultLevelUp::vf13`) and one `.xcode`
reference.

### The Memorandum notice

Not found. No component or layout class names it (the exe's `.?AV` names hold `Memorandom@dat@app` only). Leads:
`UUIC_Field_Reward` (`vf5` `0xfcb860`), `UUIC_Common_Reward` (`vf5` `0xf86cb0`) and `QuestNotice@reward@field@layout`.
If its layout opens through `0xa6ffd0`, the `ui-anim` record names it.

### Signatures

`LayoutAnimationPlay` (`0xa70010`) and `EventItemGetOpen` (`0xf560d0`), in `.ecode`: each matched once on disk with
`tools/Metaphor.SignatureCheck/ui-component-probes.json`. Neither is a jump stub. Not yet checked in memory.

## Steps

Ghidra 12.2 with JDK 25.0.5+8, the shared project from `pwsh scripts/dev/ghidra.ps1 build` on the build above,
2026-11-21. Each line is a `pwsh scripts/dev/ghidra.ps1` query.

1. `vtable 0x2251f10`, `vtable app::ui::UUIC_Common_Saving`, `vtable app::ui::UUIC_Parts_Base`; `decompile` of
   `0xf04750`, `0xf045a0`, `0xf04600`, `0xf04720`, `0xf55f60`, `0xf484a0`, `0xf560b0`, `0xf56090`, `0xf56080`.
2. `global 0x2251f10`, `0x2255948`, `0x224e950`; `callers 0xf55c80`, `0xf8a390`, `0xf484a0`; `decompile 0xf55c80`,
   `0xf8a390`; `global 0x2c63f38`, `0x2c63f68`, `0x2c63f98`, `0x2c64458`, `0x2f0c668`, `0x2ca3248`.
3. `vtable app::ui::layout::common::ItemGetNotice`, `app::ui::AutoSavingLayout`, `app::ui::SavingLayout`,
   `app::ui::SystemSavingLayout`, `fw::ui::LayoutArrayBase`; `decompile 0xc1df90`, `0xc1e0b0`, `0xd1e250`, `0xd1e410`,
   `0xd1e460`, `0xa6ffd0`, `0xa70010`; `callers 0xa6ffd0`, `0xa70010`, `0xa6fff0`.
4. `decompile 0xf560d0`; `callers 0xf560d0`, `0xf8a6f0`.
5. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" <rva> <n>` on
   `0xf8a6f0`, `0x1028e450`, `0x14cb0ac8`, `0xa6ffd0`, `0xa70010`, `0xf560d0`, and on the `type_info` names at
   `0x2c63f30` and `0x2c64450`; then `--probes` with `ui-component-probes.json`.
6. `grep -aoE` over the exe for `.?AVUUIC_*@ui@app@@` (143 names) and for names with `Notice`, `Saving`, `Memorand`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
