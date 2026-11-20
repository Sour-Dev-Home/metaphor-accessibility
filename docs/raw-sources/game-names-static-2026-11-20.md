# Where the game keeps the names its speaker box and name tags show: static reading (#81, #107)

- **Source:** our own reading of the game executable on disk, as shipped (ADR 0002): the local Ghidra project
  (`scripts/dev/ghidra.ps1`, Ghidra 12.2, `disasm`, `decompile`, `offset`, `global`, `callers`) and
  `tools/Metaphor.SignatureCheck --bytes` and `--probes` with `tools/Metaphor.SignatureCheck/game-names-probes.json`,
  this repository at the commit that adds this file. Nothing was patched, unpacked or run. Tag counts come from our own
  mod's message log of a play session (the `MessageListeningHooks` dev log, not committed).
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Adds to [`name-lookup-candidates-2026-11-16.md`](name-lookup-candidates-2026-11-16.md), the
[static note](../findings/title-names-voice-tutorials-static.md) and session 1's
[`game-session-1-2026-11-20.md`](game-session-1-2026-11-20.md). Addresses are RVAs.

## The name tables' globals

- `0x898df0` is `jmp 0x97d4e80` (`.text1`): `mov rcx,[0x2eebc18]; test rcx,rcx; jnz 0x1486fc0; xor eax,eax; ret`, and
  `0x1486fc0` is `mov rax,[rcx+0x40]; ret`. So `CharaID.bin`'s entries start at `[[0x2eebc18]+0x40]+0x10`.
- `0x898e80` is `jmp 0x97f50a0`, the same shape on `[0x2eebc98]`: `FollowerTable.bin`'s entries start at
  `[[0x2eebc98]+0x40]+0x10`.
- `0xa01bc0` (decompiled) reads follower `n`'s character as the `ushort` at `F + 0x10 + n × 0x1e`, rejects a character
  over 100 (`100 < c`, and elsewhere `c < 0x65`), then walks the five `0x12`-byte slots at entry `+0x10`, their flag
  word at `+0x20 + i × 0x12`, `-1` for none, testing each with `0x3b0ab0`. So characters 0 to 100 are valid.
- Both globals are loaded by `lea rcx,[rip+disp32]` in `0x89b3b0` (`.ecode`), which tests each resource holder in
  turn: `0x2eebc18` at `+0x4`, `0x2eebc98` at `+0xa4`, each step 20 bytes (`lea`, `call`, `test al,al`, `jz rel32`).
  Probe `ResourceTablesLoaded`: one match on disk, at `0x89b3b0`.
- `0x3b0ab0` (`.ecode`) is the flag test: `mov rcx,[rip+disp32]` at `+0x15` loads `[0x272e310]` (`G`), then
  `mov edx,[rcx+rdx*4+0x22c5c]; bt edx,eax; setc al; ret`. Probe `StoryFlagTest`, which keeps the `0x22c5c` bytes: one
  match on disk, at `0x3b0ab0`.

## The two name fields in G

- `0x3b2840` (new-game reset) zeroes `0x44` bytes at `G+0x2472c`, calls `0xa02230(G+0x2472c, 0x11)`, then zeroes
  `0x44` bytes at `G+0x24770` (two 32-byte stores and a dword at `+0x247b0`).
- `0xa02230` fills `n` 4-byte units at its first argument from message serial `0x2442`, after zeroing `n × 4` bytes:
  each field is 17 UTF-32 units (`0x44` bytes).
- `offset 0x24770`: one write in analysed code (the reset above); `offset 0x2472c`: the reset and `0x3b01f0`, which
  copies the 17 units to a new `0x44`-byte buffer. `0x3b3900` copies `G+0x2472c` to `G+0xac2a4`.
- `0xa02230`'s other callers include two `app::AppLang` lambdas (`0x7de6b0`, `0x7de910`), each resetting a name to the
  default on a language change. The message log has two such lines: "The protagonist's name is now `<HERO_NAME>`…" and
  "The player name is now `<PLAYER_NAME>`…".
- Session 1 found the name typed at the prompt that asks who will guide the protagonist at `G+0x24770`. The other
  field, `G+0x2472c`, is the protagonist's by elimination `[NEEDS VERIFICATION]`.

## Tags and speaker IDs in the message log

- `#SPEAKER_ID "<n>"` comes before a message's `@label`. Forms: 1 or 2 decimal digits (3102 times), `N` and 3 or 4
  digits (543). `"51"` stands before More's opening line, and entry 51 holds More's labels (session 1), so the digits
  are a `CharaID.bin` index. `"11"` stands before Gallica's lines. The `N` form is likely an `NpcID.bin` index
  `[NEEDS VERIFICATION]`.
- `<CHAR_NAME n>`: n is 2 to 97 or 65535. `<FOLLOWER_NAME n>`: 1 to 15 or 65535. `<HERO_NAME>` stands in lines
  that name the protagonist; `<PLAYER_NAME>` only in the language-change lines above.
- `CharaName.msg` has `CharaName_PC01_Hero` = "Protagonist", and the hero's lines carry `#SPEAKER_ID "1"` and
  `<NS>Protagonist<NE>`.

## Not found

The handlers of `<CHAR_NAME n>`, `<FOLLOWER_NAME n>`, `<HERO_NAME>` and `<PLAYER_NAME>` (codes `0xD946`, `0xD94E`,
`0xD845`, `0xD865`), and the code that names the speaker box: `0x3b3dd0`, which sets a name, is protected
(`.text1` jump table) and wasn't followed. That `<CHAR_NAME n>` takes a `CharaID.bin` index and `<FOLLOWER_NAME n>` a
`FollowerTable.bin` one is `[NEEDS VERIFICATION]`.
