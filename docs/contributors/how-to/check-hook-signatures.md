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

## Limits

- The offsets are file offsets, not addresses in the running game.
- Whether code on disk matches the code the game runs under Denuvo is [NEEDS VERIFICATION], tracked in issue #21.
  Until then, a pass here doesn't prove the hook finds its function in the game.
