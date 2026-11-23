# Coding standards

## Judgement rules

The checks can't tell whether these hold, so reviews do.

1. **Engine claims cite or flag.** [Ground rules](../AGENTS.md#ground-rules) 1 and 2 apply everywhere, not just in
   notes: in code comments, commits and PRs too.
2. **Writing leads with the point.** The first sentence of a doc, ADR, commit body, PR or comment says what the reader
   needs; background comes after. Cut what the reader doesn't need.
3. **Link, don't copy.** State a fact once and link to it. A copy drifts out of date and then contradicts the original.
4. **Threaded tests wait on state.** A test of code that runs on another thread waits for an observable state (an event
   the code under test sets) before acting, then asserts exact values. A test that races a thread passes locally and
   fails on a slower CI runner.
5. **Game state comes from the game's identity for it.** A feature recognises a screen, window or mode by the thing the
   game uses to tell it apart (its class, its own flag), not by a side effect that also happens elsewhere, such as a
   message file compiling (#208).

## Pre-review checklist

Go through this before you hand back; reviewers kept finding these (#488). Each line is a check, not a rule to learn.

- **Environment independence.** Tests mock environment-dependent outcomes (refused writes, elevation/UAC, installed
  tools, Git layout) instead of relying on the machine; CI runs elevated and admins bypass deny ACLs. A scripted-answer
  test gives the same result whatever is installed.
- **Paths and text.** Paths with spaces and non-ASCII characters work. Windows path comparisons ignore case. UTF-8 is
  read and written explicitly, in Windows PowerShell 5.1 too. No code matches the English text of a tool's output.
- **Files and processes.** Enumerations include hidden and system files. A failed write stops the script; nothing
  prints Done after a failure. Installs write to a temp file, then rename. Cleanup can't delete outside its own temp
  folder.
- **Git and hooks.** Nothing relies on the environment git sets for hooks, and a contributor's git config can't change
  the result.
- **Player-facing text.** Plain sentences a screen reader reads well: say what to type, no `[y/N]`, no symbols. Honest
  about uncertainty ("probably"). Never advises weakening a security setting.
- **Guards.** An allow-list is keyed on what is called or loaded. A guard fails closed with a clear message. A new guard
  has a planted-violation test.
- **Delivery.** Commit subject is 72 characters or fewer. `origin/main` is merged in before review.

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
9. **Hook delegates are pointer-sized.** A delegate declared in a hook passes only `nint`, `nuint`, `long`, `ulong`,
   pointers and `double`. Windows x64 leaves the upper half of a register undefined for a 32-bit argument, so a
   narrower declaration can hand the game garbage when the hook passes the call on.
10. **Game memory is read only through `ModuleMemoryReader`.** In the hook layer, no other type reads memory by
    address (loads through a pointer or `ref`, `cpblk`, `Marshal.PtrToString*`/`PtrToStructure*`/`Read*`/`Copy`,
    `Unsafe.Read*`/`AsRef`/`Copy*`, any `MemoryMarshal` member, `Volatile.Read`, `Buffer.MemoryCopy`, or a framework
    call taking a pointer), declares a P/Invoke whose DLL and entry point the rule
    doesn't list as unable to fault, or calls through a function pointer (`calli`,
    `Marshal.GetDelegateForFunctionPointer`, `NativeLibrary.GetExport`) unless the rule lists the type. .NET can't
    catch the access violation a bad pointer from the game causes; the reader fails instead.
11. **A hook is stored before it is activated.** The loader's adapters (rule 14) call `Activate` only through the field
    that holds the hook, and the loader keeps every hook it installs referenced from before it goes in. The game can
    call the hook at once, and a collected delegate the game still calls makes .NET fail-fast the whole process
    (`docs/raw-sources/message-listening-run-2026-11-16.md`).
12. **Hooks get their dependencies through the constructor.** Every `[GameHook]` type has a public constructor taking
    `HookServices`, which the loader calls. The hook layer keeps no mutable static state: static fields are `readonly`
    or `const`, and no static property has a setter, so no rule like "set this before hooks install" is left to
    comments. A `readonly` static may still hold a mutable object, so the only such static allowed is the loader's list
    of installed hooks, which keeps them alive.
13. **One hook per game function.** No two `[GameHook]` types return the same `SignatureName`, which each returns as
    a constant. A hook decodes each call once and hands the event to observers (`MessageEvents`), so two consumers of
    one function never decode it twice or depend on install order.
14. **Hooks pass calls through the loader's adapters.** A `[GameHook]` type implements exactly one shape interface
    (`IObserves3`, `IObserves4WithDouble`, …) and only decodes its arguments in `Observe`. The adapter for that shape
    (`PassThrough`) creates, stores and activates the hook, guards `Observe` with a catch-all and always calls the
    original, so no hook calls the hooking library or catches every exception itself. A hook that lists `Calls` takes
    them in `UseCalls`. A hook that changes the game's behaviour is written the long way: it implements
    `IChangesBehaviour` instead of a shape, and creates, activates and guards its hook itself, still under rules 8–11
    (`InputHooks.cs`).
15. **Hooks in `.text1` stay opt-in.** A `[GameHook]` type whose signature is in the exe's `.text1` section, by the
    `section` in `hook-signatures.json`, sets `OptInFile` or `DevOnly = true`, in every build
    ([ADR 0010](adr/0010-text1-hooks-stay-opt-in.md)).
16. **Game globals point at a load.** Each `GlobalSpec` (#300) puts a `[rip+disp32]` mov or lea at `LoadOffset` in its
    signature's pattern in `hook-signatures.json`, four `??` bytes at `DisplacementOffset`, and ends the instruction at
    `InstructionLength`, so a typo in its offsets fails a test instead of pointing a reader at the wrong address (#321;
    the encoding is in [`x86-rip-relative-loads-2026-11-19.md`](raw-sources/x86-rip-relative-loads-2026-11-19.md)).
17. **Pipes stay in Debug builds.** No Release assembly uses `System.IO.Pipes`, and the hook layer has `LiveInspector`
    exactly when it is a Debug build, so the read-only inspector's pipe never reaches players
    ([inspect the running game](contributors/how-to/inspect-the-running-game.md), #291).
18. **Every feature can be turned off.** A feature class that speaks, plays a cue or takes a key is marked
    `[FeatureSetting(Feature.X)]`, so a player with some sight can use less of the mod (#661). `X` is a member of
    `Metaphor.Core.Settings.Feature`, whose `[SettingKey]` names its key in the settings file; the wiring
    (`HookServices.Speaking`) puts the class behind `settings.Speech(Feature.X, ...)` or `settings.Input(Feature.X, ...)`,
    which read the setting at each announcement and input read, so a changed file applies at once. A cue gets its own
    `[SettingKey]` on its `Cue` member. Every default is on: a blind player never has to configure anything. The one exception is a feature still waiting
    for its by-ear check, marked `Testing = true` on its `[SettingKey]`: off until a flip PR removes the mark
    ([workflow](agents/workflow.md#a-feature-that-needs-a-by-ear-check), #783). Each
    setting is listed in [the settings reference](players/reference/settings.md).

A new project under `src/` must be added to the architecture test project, and a test fails until it is.
