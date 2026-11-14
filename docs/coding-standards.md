# Coding standards

## Architecture rules

These follow from [ADR 0003](adr/0003-thin-hook-layer.md). The `Unit` tests in `tests/Metaphor.Architecture.Tests` check
each rule on every assembly built from `src/`. Each test plants a violation to prove that its rule can fail.

1. **Plain libraries stay plain.** Every project except the hook layer (`Metaphor.Hooks`) targets `net9.0` and references
   neither Reloaded-II nor Windows-only APIs (`Microsoft.Win32`). The compiler's platform check (CA1416) catches other
   Windows-only calls.
2. **Raw memory stays in the hook layer.** Outside it, there are no pointers, no function pointers, no `Marshal`,
   `NativeMemory` or `Unsafe`, and no native calls (P/Invoke).
3. **No networking.** No assembly uses `System.Net` unless an ADR approves an exception. Exceptions are listed in
   `Rules.cs` with their ADR.
4. **Native libraries load safely.** Each native call restricts its DLL search to System32
   (`[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]`, on the method or the assembly).
   `NativeLibrary.Load` and `LoadLibrary` need an ADR-approved exception that loads the library by its full path.
5. **Features are independent.** Each feature is its own project named `Metaphor.Features.<Name>`. It may depend on
   interfaces in `Metaphor.Core` but never on another feature.

A new project under `src/` must be added to the architecture test project, and a test fails until it is.
