# Coding standards

## Judgement rules

The checks can't tell whether these hold, so reviews do.

1. **Engine claims cite or flag.** [Ground rules](../AGENTS.md#ground-rules) 1 and 2 apply everywhere, not just in
   notes: in code comments, commits and PRs too.
2. **Writing leads with the point.** The first sentence of a doc, ADR, commit body, PR or comment says what the reader
   needs; background comes after. Cut what the reader doesn't need.
3. **Link, don't copy.** State a fact once and link to it. A copy drifts out of date and then contradicts the original.

## Architecture rules

These follow from [ADR 0003](adr/0003-thin-hook-layer.md). The `Unit` tests in `tests/Metaphor.Architecture.Tests` check
each rule on every assembly built from `src/`. Each test plants a violation to prove that its rule can fail.

1. **Plain libraries stay plain.** No project except the hook layer (`Metaphor.Hooks`) targets Windows, references
   Reloaded-II, uses `Microsoft.Win32`, or marks code Windows-only. The compiler's platform check (CA1416) catches other
   Windows-only calls.
2. **Raw memory stays in the hook layer.** Outside it, no project allows unsafe code or uses pointers, native calls
   (P/Invoke), or raw-memory APIs such as `Marshal`, `MemoryMarshal` or `Unsafe`. `Rules.cs` has the full list.
3. **No networking.** No assembly uses `System.Net` unless an ADR approves an exception. Exceptions are listed in
   `Rules.cs` with their ADR.
4. **Native libraries load safely.** Each native call restricts its DLL search to exactly System32
   (`[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]`, on the method or the assembly). Loading a library
   any other way (`NativeLibrary.Load`, a DLL import resolver, `LoadLibrary`) needs an ADR-approved exception that loads
   it by full path. The exception doesn't lift the System32 rule. The one exception so far: the hook layer loads
   `prism.dll` (ADR 0005).
5. **Features are independent.** Each feature is its own project named `Metaphor.Features.<Name>`. It may depend on
   interfaces in `Metaphor.Core` but never on another feature.
6. **Hooks name known builds.** Each hook is a class marked `[GameHook(...)]` that names the game builds it was checked
   on, each listed in `src/Metaphor.Core/known-game-builds.json`. This records which builds a hook supports; checking
   the running build before patching ([ground rule 2](../AGENTS.md#ground-rules)) is the hook loader's job
   (`HookLoader`, ADR 0006).
   [Adding a build](contributors/how-to/add-a-game-build.md) is a one-file change.
7. **Prism backends come from the fixed list.** No assembly names Prism's own backend choice
   (`prism_registry_create_best`, `acquire_best`), its availability poll, or the ZDSR, PC-Talker and Boy PC Reader
   backends, which load a DLL by bare name (ADR 0005).
8. **Only hooks patch the game.** A hook-layer type that uses the hooking library (`Reloaded.Hooks`) or writes memory
   by address is marked `[GameHook]` itself (the attribute isn't inherited) or is one of the hook loader's own types, so
   no patch skips the loader's build check (ADR 0006).

A new project under `src/` must be added to the architecture test project, and a test fails until it is.
