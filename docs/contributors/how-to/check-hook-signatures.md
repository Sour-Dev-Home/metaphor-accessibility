# Check hook signatures

Before a hook PR, confirm each hook's signature still matches exactly once in your game exe. It reads the exe on disk
and doesn't launch the game.

```sh
bash scripts/check.sh signatures "$METAPHOR_GAME_EXE"
```

The exe can be passed or left to `METAPHOR_GAME_EXE`. The output names the **game build** it checked and says whether
that build is in [`known-game-builds.json`](../../../src/Metaphor.Core/known-game-builds.json). Each signature then gets
one line: `ok` with its file offset, or `FAIL` with no match or every match. An `ok` on a build the signature's
`gameBuilds` doesn't list says so. Add the build once the hook has been checked on it. It exits 0 when every
signature matches exactly once and 1 when one doesn't.

## Add a signature

Add an entry to [`src/Metaphor.Core/hook-signatures.json`](../../../src/Metaphor.Core/hook-signatures.json):

```json
{
  "name": "<what the hook calls it>",
  "pattern": "48 8B ?? 10",
  "gameBuilds": ["<SHA-256 of each build it matched on>"],
  "evidence": "<where it came from, e.g. a community mod's source; the date and this check's output>"
}
```

The pattern is hex bytes with `??` for any byte, as community mods write them. It needs at least one fixed byte.
`HookSignaturesTests` checks each entry, and that every build it names is a known build.

## Compare the exe on disk with the running game

Issue #21 asks whether a signature found on disk is also in the code the game runs. The probes in
[`tools/Metaphor.SignatureCheck/signature-probes.json`](../../../tools/Metaphor.SignatureCheck/signature-probes.json)
are CriFs V2 Hook's patterns for the release exe
([raw source](../../raw-sources/crifs-v2-hook-metaphor-patterns-2026-11-15.md)). They're dev tooling and never part of
the mod's build. With the game and Reloaded-II closed, run:

```sh
pwsh scripts/signature-spike.ps1
```

It takes about as long as a smoke test, and:

1. builds the mod with the probes file in its folder, which asks it for one scan;
2. scans the exe on disk (`tools/Metaphor.SignatureCheck --probes`), giving each single match's file offset and, from
   the exe's headers, its address relative to the module (`rva`);
3. runs the [smoke test](run-the-smoke-test.md) with only that mod enabled, so no other mod's hooks (CriFs V2 Hook
   rewrites the start of the functions probed) change the bytes. The mod copies the game's main module once,
   when Reloaded-II starts it, scans it and deletes the probes file. It only reads memory (ADR 0002): it skips pages
   that aren't committed and readable, and copies the rest through `ReadProcessMemory`, which fails instead of
   faulting. The smoke test collects the report, closes the game and restores your Reloaded-II config;
4. writes a raw-source draft to `artifacts/signature-spike/`: the game build, the steps, a table comparing each probe
   on disk and in memory (`same place`, `both once, different places`, `absent from both` or `differs`), and both raw
   outputs.

Review the draft, save it under `docs/raw-sources/`, and record the result on the issue it answers.

To scan for another set of signatures, pass its probes file:
`pwsh scripts/signature-spike.ps1 -Probes tools/Metaphor.SignatureCheck/mf-toolkit-message-probes.json` checks
MF.Toolkit's message signatures (#55; [result](../../raw-sources/mf-toolkit-message-signatures-2026-11-15.md)).

The scan runs once, early: when Reloaded-II starts our mod. If the game changes its code later, this scan won't see
it, so read a mismatch with the report's `millisecondsSinceGameStart` in mind.

## Read the bytes at an address

To see what is at an address in the module (an rva, as the script function list or a probe's `rva` gives it), read the
exe on disk:

```sh
dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" 0x90c820 64
```

The rva is hex after `0x`; the length is 1 to 4096 bytes, decimal or hex. The output names the game build, the
section holding the rva and its file offset (mapped through the PE section table), the bytes 16 to a line, then the
**candidate** call and jump targets among them, each with the section it lands in: `call` and `jmp` with a 32-bit
offset (`E8`, `E9`), short `jmp` (`EB`), conditional jumps with a 32-bit offset (`jcc`, `0F 80`–`8F`), and `call` or
`jmp` through a pointer (`FF 15`, `FF 25`, shown as `[address of the pointer]`). It doesn't disassemble: any byte can look
like an opcode, so a candidate is real only if the instructions before it lead there. Targets outside every section
are left out, and so are short conditional jumps, which are too common to be worth listing.

It exits 0 when it printed the bytes, 1 when the file doesn't hold them (no section, or past the section's data in the
file), and 2 on a usage error. The output holds no local path, so it can go into a raw source as it is; this is how
[`msg-request-functions-2026-11-16.md`](../../raw-sources/msg-request-functions-2026-11-16.md) was read by hand.

## Limits

- The offsets are file offsets. The `rva` beside a single match is where the PE headers say it loads, which is only
  where it is in the running game if that code isn't changed at load.
- On game build `548ddc95…` (Steam build 18330018), the code on disk matched the running game's: all 15 CriFs probes
  matched once in each, at the same address, when our mod started
  ([raw source](../../raw-sources/signature-spike-2026-11-15.md), issue #21). That was checked once, early in the run,
  and only on that build: after a game update, run the spike again before trusting a pass here.
