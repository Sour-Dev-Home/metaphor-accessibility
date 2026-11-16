# Linux build spike: does the solution build and test on `ubuntu-latest`?

- **Source:** a temporary GitHub Actions workflow on branch `20-linux-build-spike`, commit
  `25b5610df0fc732120d23ae96c3586d5e6004d1b`, run <https://github.com/Sour-Dev-Home/metaphor-refantazio/actions/runs/43946935563>.
  The regular CI run on the same commit (Windows `build` job, for comparison) is
  <https://github.com/Sour-Dev-Home/metaphor-refantazio/actions/runs/43946935310>. Checked for #20; answers the two
  open items in `reloaded-ii-build-setup-2026-11-13.md`.
- **Saved:** 2026-11-16.
- **Game build:** none; this is about the build and CI. The game wasn't run.

## Environment

From the run's `Environment` step:

- Runner `ubuntu-latest`: `Ubuntu 26.04.1 LTS`, `ImageOS=ubuntu26`, `ImageVersion=20261111.196.1`.
- .NET SDK `9.0.319` (from `global.json` through `actions/setup-dotnet` v6.1.0). PowerShell `7.6.10`.

## What was tried and what happened

Each step ran with `continue-on-error`, in order, in one job. `Unit|RoundTrip` is the filter `scripts/check.sh` uses.

| Step | Command | Result |
| --- | --- | --- |
| A | `dotnet build Metaphor.sln -c Release`, no extra settings | Build succeeded |
| B | A plus `-p:EnableWindowsTargeting=true` | Build succeeded |
| C | `dotnet test Metaphor.sln -c Release --no-build --filter "Category=Unit\|Category=RoundTrip"` after B | 1 failure, below |
| D | `git clean -fdx`, then each project alone with no extra settings: `dotnet test` the three test projects, `dotnet build` `Metaphor.StandInGame` and `Metaphor.SignatureCheck` | Same single failure; everything else passed or built |
| E | `git clean -fdx`, then `bash scripts/package-mod.sh` with `EnableWindowsTargeting=true` in the environment | Succeeded; package SHA-256 `5a8cce43…494638` |
| F | `dotnet format whitespace Metaphor.sln --verify-no-changes` | Succeeded |

Test results (C and D agree):

| Test project | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Metaphor.Core.Tests` | 163 | 1 | 1 |
| `Metaphor.Features.Dialogue.Tests` | 12 | 0 | 0 |
| `Metaphor.Architecture.Tests` | 153 | 0 | 0 |

- The failure is `PinnedFileTests.ChecksAnOpenStreamSoTheCallerCanHoldTheFileUntilItLoadsIt`. It holds a file open with
  `FileShare.Read` and expects `File.WriteAllBytes` on the same path to throw `IOException`; on Linux the write didn't
  throw. Why (how .NET maps `FileShare` on Unix): [NEEDS VERIFICATION]. The behaviour the test pins down is a Windows
  one, and the mod only runs on Windows.
- The skipped test is a `RoundTrip` test, which skips without game data.

## Findings

- `Metaphor.Hooks` (`net9.0-windows`) builds on Linux even **without** `EnableWindowsTargeting` (A, and D, where
  `Metaphor.Architecture.Tests` builds it as a project reference after a clean). Why the SDK doesn't refuse it here
  (the project uses no Windows Desktop framework): [NEEDS VERIFICATION]. The package step (E) did pass with the
  setting; it wasn't tried without it.
- The plain `net9.0` projects build and their tests run on Linux; one Core test depends on Windows file sharing.
- ReadyToRun isn't used in this repository, so cross-OS ReadyToRun wasn't tested.
- The package built on Linux isn't byte-identical to the one built on Windows from the same commit: Linux
  `5a8cce4337c0f64eed3876f7915a027eb2d96d7831d844a3fc8329ae12494638`, Windows
  `18802f9a8ddb3a58b1301eb10819b96eb1714e6c0d6d7d6716f45db7efb3a3e9`. Which files differ wasn't checked:
  [NEEDS VERIFICATION]. The release package should keep coming from Windows.

## What a CI change could be (not made)

A Linux job could build the solution and run `Unit|RoundTrip` tests once the `PinnedFile` test is limited to Windows
(for example a Windows-only fact or an `OperatingSystem.IsWindows()` guard). It would add a second-OS check on the core
logic, not replace the Windows `build` job, which packages the release.
