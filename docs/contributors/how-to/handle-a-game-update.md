# Handle a game update

When the game updates, its executable changes, and every hook's signature and offset must be checked again before the
mod patches anything on the new build ([ground rule 2](../../../AGENTS.md#ground-rules)). Until then the mod does
nothing on it. Never copy a hook's old build into the new one on trust.

## What the player sees

The mod hashes the game's executable at start and compares it with the
[known-builds list](../../../src/Metaphor.Core/known-game-builds.json). On a build it doesn't know:

- it speaks `This game version isn't supported yet; the mod is off`, and installs no hook. The game runs as normal.
  `mod-status.json` records the refusal with the reason `unknown game build`;
- `scripts/player/check-install.ps1` reports "The game is a version this mod does not know yet, so the mod turns itself
  off", with the game's SHA-256, and tells the player to look for a newer release
  ([player's page](../../players/how-to/install-the-mod.md#what-the-mod-says-when-it-loads)).

Every launching verb of `scripts/dev/session.ps1` refuses the same build.

## Steps

1. **See what broke.** With the new executable at `METAPHOR_GAME_EXE`, run
   `bash scripts/check.sh signatures`. Its first line names the build and says `not a known build`; each signature
   then reports `ok` or `FAIL` ([check hook signatures](check-hook-signatures.md)). Keep the output. A `FAIL` means the
   hook on that signature can't go on this build until the signature is fixed.
2. **Record the build.** Add the executable's SHA-256 and Steam build ID to the list, with the evidence line:
   [add a game build](add-a-game-build.md). This alone enables no hook, because each hook names the builds it was
   checked on, so it is safe to land first.
3. **Compare disk with memory.** Run the [signature spike](check-hook-signatures.md#compare-the-exe-on-disk-with-the-running-game)
   (`pwsh scripts/dev/session.ps1 spike`). The earlier pass
   ([raw source](../../raw-sources/signature-spike-2026-11-15.md)) holds for build `548ddc95…` only.
4. **Re-check each hook's offsets** on the new build: the signature, and the offsets a hook or a global derives from
   its match (a `GlobalSpec`'s `LoadOffset`, `DisplacementOffset` and `InstructionLength`). Read the bytes with
   `--bytes` ([read the bytes at an address](check-hook-signatures.md#read-the-bytes-at-an-address)) and, for what only
   the running game shows, [run a game session](run-a-game-session.md) with the opt-in the hook needs.
5. **Re-enable a hook per build, one at a time.** For each hook that checks out: add the new build to its signature's
   `gameBuilds` in `hook-signatures.json`, add a constant for it to
   [`GameBuilds.cs`](../../../src/Metaphor.Hooks/GameBuilds.cs) and name it in the hook's `[GameHook(...)]`
   ([architecture rule 6](../../coding-standards.md#architecture-rules)). That is a hook change, with its review and
   in-game output on the new build ([add a hook](add-a-hook.md)).
6. **Write it down.** Save the outputs as raw sources and note changed offsets in findings, each naming the new build
   ([write a finding](write-a-finding.md)). A claim you haven't re-checked on the new build is
   `[NEEDS VERIFICATION]`.
7. **Ship it.** Players get the new list only in a release ([cut a release](cut-a-release.md)).
