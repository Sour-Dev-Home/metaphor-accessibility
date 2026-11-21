# The move-name banner hands its layout a text reference, so no message lookup the mod hooks sees it

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-turn-icons-and-ui-components-static.md`](battle-turn-icons-and-ui-components-static.md)

The banner that names a move (an enemy skill in #247's 2026-11-20 run) is `UUIC_Battle_Message`. Its show function
`0xf187d0` (`BattleMessageShow`, our name) turns a `{ id, kind }` pair into a message file and an index, builds a text
reference from them and gives it to the layout, which resolves the text when it draws. The string never passes
through `GetMessageBySerial` or the other lookups the mod hooks, which is why the run heard nothing. Hooking the show
function gives the id and kind per banner. Static only.

## Evidence

- **Who calls it.** The battle GUI function `0x326e70` gets the component with its registry getter `0xc5ce0` and calls
  `0xf187d0(component, &{ [entry+0x8], 2 }, [entry+0x14])` for each entry it handles. Just before, it formats
  `[entry+0xC]` with `"%d"` (the bytes at rva `0x17e22b0`) into slot 1 of `0xa025b0`, a second number for the
  banner whose role is `[NEEDS VERIFICATION]`.
- **One funnel.** `0xf187d0` has 19 call sites: the battle GUI (`0x326e70`, `0x32c750`, `0x32d370`, `0x3295b0`),
  `0x324c50` (below), battle code at `0x148bf0`, `0x15fc40`, `0x16a070`, `0x16e310`, a UI event lambda (`0xd8c440`)
  and the command panel's own functions (`0xf0f630` to `0xf139b0`). A hook on it sees every banner.
- **A battle-side request.** `0x324c50` (`BattleBannerRequest`, our name), `(?, action*, kind, id, duration)`, builds
  the pair from its third and fourth arguments and calls `0xf187d0` (or the component's other entry `0xf185d0`). Its
  14 callers are battle code (`0x148bf0` eight times, `0x164770`, `0x1bbe70` three times, `0x1ebb90`, `0x34b150`);
  which of them is the enemy's move is `[NEEDS VERIFICATION]`.
- **The pair.** `param_2` points at 8 bytes: a `uint` id, then a kind byte at `+4`. The third argument, a float, sets
  the display time at component `+0x20` (a default from a table when it's 0 or less).
  - kind 0, id ≠ 0: a label name from `0x3e14f0()` `+2`, resolved to (file, index) by `0x9e8250`.
  - kind 0, id 0: either the label `System_ATTRIBUTE_01` with a number, or file 6 index 0.
  - kind 1: the label name `0x3bd020(id)`, through `0x9e8250`.
  - kind 2: file `0x9e8010(9)`, index = id. The battle GUI call above uses this kind.
  - kind 3: file `0x9e8010(0x12)`, index `0x37c9a0(id)`.
- **Hand-off.** `0xa67a40` makes a text reference, `0xa67db0(ref, file, index)` fills it, and `0xaf9ea0([this+0x28],
  ref)` gives it to the layout; `+0x18` is cleared. No string is built here.
- **Signatures.** `BattleMessageShow` (the prologue up to its first call) and `BattleBannerRequest` each match once on
  disk, at rva `0xf187d0` and `0x324c50` (`tools/Metaphor.SignatureCheck/battle-turn-probes.json`).

Which `.msg` file `0x9e8010(9)` names, and whether kind 2's id is a skill id, are `[NEEDS VERIFICATION]`. The mod's
message log already records which files the game compiles in battle; the by-ear run that hooks the show function
pairs each banner's id with those files.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-20.

1. `vtable app::ui::UUIC_Battle_Message` (vtable `0x224f418`), `callers 0xc5ce0`, `decompile 0x326e70` (its getter call is at
   `0x32708b`, the show call right after).
2. `decompile 0xf187d0`: the kind branches and the hand-off. `callers 0xf187d0`, `decompile 0x324c50`,
   `callers 0x324c50`.
3. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0x17e22b0 16` (`25 64 00`, "%d"),
   `--bytes <exe> 0xf187d0 48`, then `--probes <exe> tools/Metaphor.SignatureCheck/battle-turn-probes.json`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled; the thunks at `0xf7bc160` and `0xa8cbd70` were not followed.
