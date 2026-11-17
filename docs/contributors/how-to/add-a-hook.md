# Add a hook

From "the feature needs to know when the game does X" to a hook that is in the mod, pinned to a **game build**, and proven
in the game. Each step ends in evidence: a raw source, a passing check, or a run. Work in an issue, one step at a time;
the dialogue hook (#52, #55, #56) went this way.

The rules behind it: [ground rules](../../../AGENTS.md#ground-rules) 1–5, [ADR 0002](../../adr/0002-analysis-methods-under-denuvo.md)
(what analysis is allowed), [ADR 0003](../../adr/0003-thin-hook-layer.md) (only the hook layer touches the game) and
[ADR 0006](../../adr/0006-hooks-through-reloaded-shared-hooks.md) (how hooks are installed).

## 1. Find the function

Cheapest first, and write down where each fact came from as you go:

1. **Community mods' source.** Save what you use as a raw source, with the commit
   ([example](../../raw-sources/game-text-hooks-community-2026-11-15.md)). Their signatures name no game build, so treat
   them as leads until step 2.
2. **What the game tells you at runtime.** Its script functions are known by name and address
   ([list](../../raw-sources/squirrel-script-functions-all-2026-11-16.md)). A development-only listening hook can log
   when a candidate fires (step 4, with `DevOnly = true`).
3. **The exe's bytes, as-is** (ADR 0002): read what is at an address with
   [`--bytes`](check-hook-signatures.md#read-the-bytes-at-an-address), or open the exe in a disassembler. Never anything
   that defeats the protection; if a step would need that, stop and note it.

## 2. Write and check its signature

A byte pattern with `??` wildcards that matches the function exactly once
([format](check-hook-signatures.md#add-a-signature)).

1. Put it in a probes file in `tools/Metaphor.SignatureCheck/` that cites the raw source, and check it on disk:
   `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --probes "$METAPHOR_GAME_EXE" <probes file>`.
2. Check it in the running game too: `pwsh scripts/signature-spike.ps1 -Probes <probes file>`
   ([how](check-hook-signatures.md#compare-the-exe-on-disk-with-the-running-game)). Save the draft as a raw source.
3. Copy it into `src/Metaphor.Core/hook-signatures.json` with the builds it was checked on and evidence pointing to
   those raw sources. `HookSignaturesTests` checks the entry.

## 3. Pin down how it's called

From the source you used, or the disassembly: the arguments, their widths and the return value. Cite it in the hook's
comments. Unknown widths are `[NEEDS VERIFICATION]` until the disassembly or a run settles them. Declare integers
`nint` whatever the width, even where a community mod used `int` (architecture rule 9).

## 4. Write the hook

A class in `src/Metaphor.Hooks` ([example](../../../src/Metaphor.Hooks/MessageListeningHooks.cs)):

- `[GameHook(GameBuilds.<build>)]`, and `DevOnly = true` for a spike: the loader installs it only in Debug builds.
  A risky spike hook also takes `OptInFile = "<bare-name>.txt"`: it installs only when that file is in the mod's
  folder ([files a run can leave](deploy-a-dev-build.md#files-a-run-can-leave-in-the-mods-folder)). A hook on a
  function in `.text1` keeps one of the two in every build (architecture rule 15, ADR 0010).
- Implements `IGameHook`: `SignatureName`, and `Calls` and `UseCalls` for any game function it calls (each needs its
  own signature). Only the loader installs hooks, after the build check and a full scan.
- Implements the one shape interface in [`PassThrough.cs`](../../../src/Metaphor.Hooks/PassThrough.cs) that matches its
  arguments, such as `IObserves3`. The loader's adapter for that shape hooks the function, guards `Observe` and calls the
  original (rule 14). The shapes fit functions that return an integer, a pointer or nothing; if none fits, add one
  there.
- A public constructor taking `HookServices`: it hands the hook everything it uses (rule 12). Keep what the hook needs
  in its own fields, not in statics.
- In `Observe`, decode the arguments, reading game memory only with `ModuleMemoryReader` (rule 10).
- Keep the logic in a plain library (`Metaphor.Core` or a feature project) and unit-test it there with fakes; the hook
  only adapts.
- One hook per game function (rule 13). If the function already has a hook, add an observer behind it
  ([`MessageEvents`](../../../src/Metaphor.Core/Messages/MessageEvents.cs)) instead of a second hook.

`scripts/check.sh full` must pass. Its [architecture rules](../../coding-standards.md#architecture-rules) 6 and 8–14
check the points above.

## 5. Prove it in the game

1. **Smoke test** ([how](run-the-smoke-test.md)): the status shows `hooks n/n` on the build. It closes the game within
   seconds, so it proves the hook installs, not what it does later.
2. **A real run**: `pwsh scripts/deploy-dev.ps1` leaves the game running. Watch it for at least a minute before
   playing; the first run of #55's hooks crashed seven seconds in. A
   [listening run](deploy-a-dev-build.md#listening-run) (`pwsh scripts/listen.ps1`) does that watch and summarises the
   development log by record type. Then do what the feature needs: play the scene,
   listen with a screen reader (say so in advance if someone else is listening), read the transcript.
3. If it crashed, Windows' Application event log names the faulting module and exception code; `coreclr.dll` with
   `0x80131623` is .NET failing fast, not the game.
4. Save what the run showed as a raw source (method, game build, steps), and record it on the issue.
5. Turn each finding into a replay test (#149) before the log is overwritten: cut the scenario out of the mod's
   `message-log.txt` with `dotnet run --project tools/Metaphor.SessionFixture -- <log> <from ms> <to ms> <kinds> <game
   build> <raw source> "<behaviour>"` into `tests/fixtures/sessions/<behaviour>.txt`, one small scenario per behaviour.
   It keeps only labels, numbers and times, and refuses a record that looks like text. Replay it with
   `SessionFixture.Replay` through the seams the hooks feed, and assert what was announced and in what order, not the
   wording (`TitleMenuReplayTests`). The session's raw source ends with a list of the tests added for its findings.

## 6. Review and merge

Run the `security-reviewer` agent before committing hook code, fix what it finds, and put the run's evidence in the PR
(the `/pr` shape). A feature's hook ships only without `DevOnly`, after its own smoke test and run.

## Traps already hit

- **A hook object nobody references gets collected, and the game crashes.** The loader keeps installed hooks alive; don't
  create hooks or callbacks outside it.
- **`pwsh -File` splits a list argument into separate arguments**, which then bind to other parameters. Pass lists
  through `pwsh -Command "& ./script.ps1 -Name a,b"`.
- **An empty log isn't a broken hook.** The function may not have been called yet: run the game long enough, and reach
  the moment the feature needs.
