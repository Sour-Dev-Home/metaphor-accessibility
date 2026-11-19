# Add a feature project

A feature is one project, `src/Metaphor.Features.<Name>`, with its own tests project. It holds plain logic over
interfaces in `Metaphor.Core`; the hook layer feeds it. The rules are
[architecture rules 1, 2 and 5](../../coding-standards.md#architecture-rules), checked by the tests in
`tests/Metaphor.Architecture.Tests`. `src/Metaphor.Features.Menus` and its tests are a small model to copy.

## What the architecture tests require

| Rule | The test fails when | So |
|---|---|---|
| Features are independent | the assembly references any other `Metaphor.Features.*` | depend on `Metaphor.Core` only; share code by moving it into Core |
| Plain library | the project targets Windows, references Reloaded-II, or uses `Microsoft.Win32` | target `net9.0`, not `net9.0-windows` |
| No raw memory | it allows unsafe code, or uses pointers, P/Invoke, `Marshal`, `MemoryMarshal` or `Unsafe` | read game memory through an interface Core defines and the hook layer implements |
| No networking | it uses `System.Net` | don't |

A feature never patches or hooks anything ([ADR 0003](../../adr/0003-thin-hook-layer.md)).

## Steps

1. Create `src/Metaphor.Features.<Name>/Metaphor.Features.<Name>.csproj`, copied from the
   [Menus project](../../../src/Metaphor.Features.Menus/Metaphor.Features.Menus.csproj): `net9.0` and one
   `ProjectReference` to `Metaphor.Core`.
2. Create `tests/Metaphor.Features.<Name>.Tests/Metaphor.Features.<Name>.Tests.csproj`, copied from the
   [Menus tests](../../../tests/Metaphor.Features.Menus.Tests/Metaphor.Features.Menus.Tests.csproj), referencing your
   project. Tag tests `[Trait("Category", "Unit")]`.
3. Add both to the solution, from the repository root:

   ```sh
   dotnet sln Metaphor.sln add src/Metaphor.Features.<Name>/Metaphor.Features.<Name>.csproj
   dotnet sln Metaphor.sln add tests/Metaphor.Features.<Name>.Tests/Metaphor.Features.<Name>.Tests.csproj
   ```

4. **Add the project to the architecture tests.** In
   [`Metaphor.Architecture.Tests.csproj`](../../../tests/Metaphor.Architecture.Tests/Metaphor.Architecture.Tests.csproj)
   add a `ProjectReference` beside the Menus one, with `ReferenceOutputAssembly="false"` and
   `OutputItemType="ScannedAssembly"`. Nothing else makes the rules see your project: without this line they pass
   without checking it.
5. Restore, to write each new project's `packages.lock.json`, and commit those files. CI restores in locked mode and
   fails when a lock file is missing or stale:

   ```sh
   dotnet restore Metaphor.sln
   ```

6. To use the feature in the mod, add a `ProjectReference` to it in
   [`Metaphor.Hooks.csproj`](../../../src/Metaphor.Hooks/Metaphor.Hooks.csproj) and wire it in
   [`HookServices.cs`](../../../src/Metaphor.Hooks/HookServices.cs). A change to a hook is a
   [hook change](add-a-hook.md), with its review and in-game evidence.
7. Run `bash scripts/check.sh full`.

## CI

There is no per-project CI wiring. The check selects test projects by their `*Tests.csproj` name and the
`ProjectReference` graph ([`what-runs.sh`](../../../scripts/ci/what-runs.sh)), and CI builds and tests the whole
solution, so the solution entry from step 3 is what puts the pair into CI. A change in a feature runs its tests and the
tests of everything that references it. [Checks](../reference/checks.md#in-ci).
