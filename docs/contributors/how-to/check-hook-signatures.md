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
the mod's build. To compare them:

1. In Reloaded-II, disable every mod for the game except ours. A mod that hooks first, such as CriFs V2 Hook or a mod
   that depends on it, rewrites the start of the functions the probes look for. The deploy warns when other mods
   are enabled.
2. On disk:

   ```sh
   dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --probes "$METAPHOR_GAME_EXE"
   ```

   Each single match gives its file offset and, from the exe's section table, its address relative to the module
   (`rva`).
3. In the running game, with the game closed first:

   ```sh
   pwsh scripts/deploy-dev.ps1 -ScanSignatures
   ```

   This puts the probes file in the deployed mod's folder. The mod copies the game's main module once, when Reloaded-II
   starts it, scans it, and deletes the probes file, so later launches don't scan. It only reads memory (ADR 0002): it
   skips pages that aren't committed and readable, and copies the rest through `ReadProcessMemory`, which fails
   instead of faulting. The script prints the report, `signature-scan.json`: each probe's match count and first
   addresses relative to the module, the bytes read and the bytes that failed, and how long after the game's start
   the copy was made.
4. Compare each probe's count, and for single matches its `rva` on disk with its address in memory. Save both outputs,
   with the game build and the enabled mods, as a raw source, and record the result on #21.

The scan runs once, early: when Reloaded-II starts our mod. If the game changes its code later, this scan won't see
it, so read a mismatch with the report's `millisecondsSinceGameStart` in mind.

## Limits

- The offsets are file offsets, not addresses in the running game.
- Whether code on disk matches the code the game runs under Denuvo is [NEEDS VERIFICATION], tracked in issue #21.
  Until then, a pass here doesn't prove the hook finds its function in the game.
