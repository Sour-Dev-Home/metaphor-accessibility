# The in-turn situation banner is a position in jyokyo_help_Message.msg, shown through two banner functions

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-move-banner-text-reference-static.md`](battle-move-banner-text-reference-static.md) and
  [`field-nearby-unit-names-static.md`](field-nearby-unit-names-static.md)
- **Functions:**
  - BattleMessageShow | 0xf187d0 | .ecode | Battle | hooked | Shows a banner; kind 2 picks the situation file's position. | finding; src/Metaphor.Hooks/BattleHooks.cs
  - BattleMessageRequestShow | 0xf185d0 | .ecode | Battle | hooked | The second show function, called only from the battle-side request. | finding; src/Metaphor.Hooks/BattleHooks.cs

For #692, #626 and #695. `UUIC_Battle_Message`'s banners of kind 2 are the situation messages ("Attack rose!", "Not
enough MP!", "`<V 0>` is powering up!", "Gained one Turn Icon!"): message-file table entry 9,
`message/system/jyokyo_help_Message.msg`, at position id. The component has two show functions, and a hook on both
sees every banner. Static only; nothing here has been heard in play.

## Evidence

- **Kind 2 is table entry 9.** Both show functions, `0xf187d0` (`BattleMessageShow`) and `0xf185d0`
  (`BattleMessageRequestShow`, our name), branch on the kind byte at pair `+4`; kind 2 passes 9 to `0x9e8010` and the id
  as the position. `0x9e8010(int* entry)` returns field `+0x78` of the entry's `0xb8`-byte run-time record.
- **Entry 9 is the situation file.** The table at `0x27b0cd0` has `0xd0`-byte entries; entry 9's name pointer (at
  `0x27b1420`) is `0x142122bf8`, whose bytes spell `message/system/jyokyo_help_Message.msg`, length `0x26`.
- **Its compile id.** Compile ids are the entry index plus one for the early files (field note above). The mod's Debug
  message log of 2026-11-21 (local, not committed) has `compile id=10` for `jyokyo_help_Message.msg`, 259 messages,
  among them every banner text above. The mod finds the file by name, so it doesn't depend on that id.
- **The second show.** `0xf185d0`, `(component*, request*)`, reads the id at request `+0`, the kind at `+4` and the
  seconds (a float) at `+0x10`. Its only caller is the battle-side request `0x324c50`, which calls one show or the
  other.
- **Signatures.** Both prologues match once on disk (`tools/Metaphor.SignatureCheck/battle-turn-probes.json`).

## Not pinned

- The name a banner fills in (`<V 0>`) and the number `0x326e70` formats with `"%d"` are message variables 0 and 1,
  see [`battle-banner-name-variable-static.md`](battle-banner-name-variable-static.md). "Not enough MP!" doesn't come
  through either show: [`battle-not-enough-mp-path-static.md`](battle-not-enough-mp-path-static.md).
- The advantage banners at battle start (#626) are not among the situation messages; whether they are kind 0, 1 or 3,
  or art with no text, is `[NEEDS VERIFICATION]`. A Debug build logs each banner's kind and id to settle it. Leads
  since: two layouts, [battle-start-and-turn-notice-layouts-static.md](battle-start-and-turn-notice-layouts-static.md).
- The turn-icon count (#695): the fields at `G+0x25460` are still leads. A Debug build logs them with each banner and
  each hit. Where they change: [turn-icon-count-changes-static.md](turn-icon-count-changes-static.md).

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-22.

1. `decompile 0xf185d0`, `disasm 0xf185d0`, `callers 0xf185d0`, `decompile 0x9e8010`.
2. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0x27b1420 16`, then
   `--bytes <exe> 0x2122bf8 48`; `--bytes <exe> 0xf185d0 64`; `--probes <exe>
   tools/Metaphor.SignatureCheck/battle-turn-probes.json`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled; neither function is a jump stub.
